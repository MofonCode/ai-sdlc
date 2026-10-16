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

    private static CycleWorkspace Approved()
    {
        var workspace = CycleWorkspace.Create();

        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteSpec(criteria: 3);
        workspace.WritePlan(proofs: 3);

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        return workspace;
    }

    private static async Task<ListQueuedCycles.Response> Read(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        Result<ListQueuedCycles.Response> result = await gate
            .Send<ListQueuedCycles.Query, ListQueuedCycles.Response>(new ListQueuedCycles.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
