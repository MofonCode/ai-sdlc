using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Factory;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// The factory's queue: what it would build, and every cycle it would pass over with why (D-85).
/// </summary>
[TestClass]
public sealed class QueueTests
{
    [TestMethod]
    public async Task An_approved_cycle_with_no_verify_and_no_branch_on_the_remote_is_queued()
    {
        using CycleWorkspace workspace = Approved();

        ListQueuedCycles.Response queue = await Read(workspace, new FakeRepository());

        Assert.AreEqual(1, queue.Cycles.Count);
        Assert.AreEqual(workspace.Cycle.Id, queue.Cycles[0].Id);
        Assert.AreEqual(0, queue.Held.Count);
        Assert.IsTrue(queue.RemoteRead);
    }

    [TestMethod]
    [DataRow("plan", "may still be moving")]
    [DataRow("build", "by hand")]
    [DataRow("build-unattended", "factory run has taken it")]
    public async Task Any_branch_of_the_cycle_on_the_remote_holds_it_back_and_says_why(string kind, string why)
    {
        // A held-back cycle is named rather than dropped: a forgotten plan branch would otherwise starve its
        // cycle silently, night after night.
        using CycleWorkspace workspace = Approved();
        FakeRepository git = new() { Remote = ["cycle/" + workspace.Cycle.Id + "/" + kind, "cycle/20200101_0000_00/plan"] };

        ListQueuedCycles.Response queue = await Read(workspace, git);

        Assert.AreEqual(0, queue.Cycles.Count);
        Assert.AreEqual(1, queue.Held.Count);
        StringAssert.Contains(queue.Held[0].Reason, why);
        StringAssert.Contains(queue.Held[0].Reason, "cycle/" + workspace.Cycle.Id + "/" + kind);
    }

    [TestMethod]
    public async Task A_fix_for_a_release_is_held_back_from_a_batch()
    {
        // D-88: built by hand from the release branch, never by the factory from the integration branch.
        using CycleWorkspace workspace = Approved(release: "release/1.4");

        ListQueuedCycles.Response queue = await Read(workspace, new FakeRepository());

        Assert.AreEqual(0, queue.Cycles.Count);
        StringAssert.Contains(queue.Held.Single().Reason, "release/1.4");
    }

    [TestMethod]
    public async Task A_verified_cycle_is_not_queued()
    {
        using CycleWorkspace workspace = Approved();
        workspace.WriteVerify(outcome: "pass");

        ListQueuedCycles.Response queue = await Read(workspace, new FakeRepository());

        Assert.AreEqual(0, queue.Cycles.Count);
    }

    [TestMethod]
    public async Task A_plan_edited_after_its_approval_is_held_back_rather_than_vanishing()
    {
        using CycleWorkspace workspace = Approved();
        workspace.WriteArtifact(ArtifactKind.Spec, "an extra criterion nobody approved");

        ListQueuedCycles.Response queue = await Read(workspace, new FakeRepository());

        Assert.AreEqual(0, queue.Cycles.Count);
        Assert.AreEqual(1, queue.Held.Count);
        StringAssert.Contains(queue.Held[0].Reason, "gate refuses it");
    }

    [TestMethod]
    public async Task With_no_remote_to_read_nothing_is_held_back_and_the_report_says_so()
    {
        using CycleWorkspace workspace = Approved();

        ListQueuedCycles.Response queue = await Read(workspace, new FakeRepository { Remote = null });

        Assert.AreEqual(1, queue.Cycles.Count);
        Assert.IsFalse(queue.RemoteRead);
        StringAssert.Contains(QueuedRenderer.Render(queue), "could not be read");
    }

    [TestMethod]
    public async Task A_queued_cycle_names_every_successor_waiting_on_it_even_behind_another()
    {
        // What merging it starts moving, and why it is built first: a run that stops on its time limit has
        // then spent its hours where they unblock the most.
        using CycleWorkspace workspace = Approved();
        SuccessorTests.Write(workspace, "totals", workspace.Cycle.Id.ToString());
        SuccessorTests.Write(workspace, "chart", "successor:totals");
        SuccessorTests.Write(workspace, "unrelated", "20200101_0000_00");

        ListQueuedCycles.Response queue = await Read(workspace, new FakeRepository());

        CollectionAssert.AreEqual(new[] { "totals", "chart" }, queue.Cycles.Single().Unlocks.ToArray());
        StringAssert.Contains(QueuedRenderer.Render(queue), "merging it starts: successor:totals, successor:chart");
    }

    [TestMethod]
    public async Task A_run_named_for_one_queued_cycle_gets_that_cycle_alone()
    {
        using CycleWorkspace workspace = Approved();

        ListQueuedCycles.Response queue = await Read(workspace, new FakeRepository(), workspace.Cycle.Id);

        Assert.AreEqual(workspace.Cycle.Id, queue.Cycles.Single().Id);
        Assert.AreEqual(0, queue.Held.Count);
    }

    [TestMethod]
    public async Task A_run_named_for_a_cycle_that_is_not_queued_is_refused_with_why()
    {
        // The factory's one input. A run started for a cycle it cannot build stops before it builds anything,
        // rather than building everything else instead.
        using CycleWorkspace workspace = Approved();
        FakeRepository git = new() { Remote = ["cycle/" + workspace.Cycle.Id + "/build-unattended"] };

        Result<ListQueuedCycles.Response> taken = await Send(workspace, git, workspace.Cycle.Id);
        Assert.IsTrue(CycleId.TryParse("20200101_0000_00", out CycleId? missing));
        Result<ListQueuedCycles.Response> nowhere = await Send(workspace, new FakeRepository(), missing);

        Assert.IsTrue(taken.IsFailed);
        StringAssert.Contains(taken.Errors[0].Message, "factory run has taken it");
        Assert.IsTrue(nowhere.IsFailed);
        StringAssert.Contains(nowhere.Errors[0].Message, "no such cycle");
    }

    private static CycleWorkspace Approved(string? release = null)
    {
        var workspace = CycleWorkspace.Create();

        workspace.WriteArtifact(ArtifactKind.Intent);

        if (release is not null)
        {
            // Before the approval, so the hash covers it, as it would for a person who wrote it in.
            string path = workspace.Cycle.PathOf(ArtifactKind.Intent);
            string intent = File.ReadAllText(path);
            int close = intent.IndexOf("\n---", 3, StringComparison.Ordinal);
            CycleWorkspace.WriteRaw(path, intent[..close] + "\nrelease: " + release + intent[close..]);
        }
        workspace.WriteSpec(criteria: 3);
        workspace.WritePlan(proofs: 3);

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        return workspace;
    }

    private static async Task<ListQueuedCycles.Response> Read(CycleWorkspace workspace, FakeRepository git, CycleId? only = null)
    {
        Result<ListQueuedCycles.Response> result = await Send(workspace, git, only);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static async Task<Result<ListQueuedCycles.Response>> Send(CycleWorkspace workspace, FakeRepository git, CycleId? only)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        return await gate.Send<ListQueuedCycles.Query, ListQueuedCycles.Response>(new ListQueuedCycles.Query(only));
    }
}
