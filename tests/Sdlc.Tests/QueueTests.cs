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

    [TestMethod]
    [DataRow("cycle/20261101_0900_00/plan", DisplayName = "on its own plan branch")]
    [DataRow("slices/sandbox-20261101_0900_00/plan", DisplayName = "on a sandbox's admission")]
    public async Task A_named_cycle_admitted_only_on_this_machine_is_queued(string admission)
    {
        // D-178: build it admits the plan locally, pushing nothing, and starts the run from the person's own
        // checkout, which does not carry the plan yet.
        using CycleWorkspace workspace = Approved();
        FakeRepository git = Admitted(admission, approved: true);

        ListQueuedCycles.Response queue = await Read(workspace, git, LocalId);

        Assert.AreEqual(LocalId, queue.Cycles.Single().Id);
        Assert.AreEqual("Roll a die", queue.Cycles.Single().Headline);
    }

    [TestMethod]
    public async Task A_local_admission_that_is_unapproved_verified_or_taken_is_refused_with_why()
    {
        using CycleWorkspace workspace = Approved();
        string admission = "cycle/20261101_0900_00/plan";

        FakeRepository unapproved = Admitted(admission, approved: false);
        FakeRepository verified = Admitted(admission, approved: true);
        verified.PathsAt.Add(admission + ":.sdlc/cycles/20261101_0900_00/verify.md");
        FakeRepository taken = Admitted(admission, approved: true);
        taken.Remote = ["cycle/20261101_0900_00/build-unattended"];

        StringAssert.Contains((await Send(workspace, unapproved, LocalId)).Errors[0].Message, "not approved there");
        StringAssert.Contains((await Send(workspace, verified, LocalId)).Errors[0].Message, "already has a verify");
        StringAssert.Contains((await Send(workspace, taken, LocalId)).Errors[0].Message, "factory run has taken it");
    }

    [TestMethod]
    public async Task The_unnamed_queue_never_reads_a_local_admission()
    {
        // The factory's queue is what the integration branch holds; a plan on one person's machine is not on it.
        using CycleWorkspace workspace = Approved();

        ListQueuedCycles.Response queue = await Read(workspace, Admitted("cycle/20261101_0900_00/plan", approved: true));

        Assert.IsFalse(queue.Cycles.Any(cycle => cycle.Id == LocalId));
    }

    private static CycleId LocalId => CycleId.TryParse("20261101_0900_00", out CycleId? id) ? id.Value : throw new InvalidOperationException();

    private static FakeRepository Admitted(string admission, bool approved)
    {
        const string Directory = ".sdlc/cycles/20261101_0900_00/";
        FakeRepository git = new();

        git.Refs.Add(admission);
        git.PathsAt.Add(admission + ":" + Directory + "plan.md");
        git.Files[admission + ":" + Directory + "intent.md"] = "---\ncycle: 20261101_0900_00\n---\n\n# Roll a die\n";
        git.Files[admission + ":" + Directory + "approval.md"] = approved
            ? "---\ncycle: 20261101_0900_00\ndecisions:\n  - decision: approved\n    by: Peter Almanzar\n    at: 2026-11-01T12:00:00Z\n---\n"
            : "---\ncycle: 20261101_0900_00\ndecisions:\n  - decision: changes_requested\n    by: Peter Almanzar\n    at: 2026-11-01T12:00:00Z\n    reason: The plan names no proof.\n---\n";

        return git;
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
