using FluentResults;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;

namespace Sdlc.Tests;

/// <summary>
/// The gate's refusals, each exercised against a real directory.
/// </summary>
/// <remarks>
/// These are the assertions that make the local gate more than a claim. The two that matter most are
/// approve-then-edit and a blocker raised after approval: both leave an approval that still says yes
/// while describing something that is no longer true.
/// </remarks>
[TestClass]
public sealed class ApprovalGateTests
{
    [TestMethod]
    public async Task A_cycle_with_no_directory_is_refused_and_told_how_to_start()
    {
        using var workspace = CycleWorkspace.Create();
        Directory.Delete(workspace.Cycle.Directory, recursive: true);

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(GateRefusal.NoCycle, verdict.Refusal);
        Assert.IsFalse(verdict.MayBuild);
        StringAssert.Contains(verdict.Explanation, "intent");
    }

    [TestMethod]
    public async Task An_abandoned_cycle_is_refused_even_when_it_also_holds_an_approval()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        workspace.Abandon();

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(
            GateRefusal.Abandoned,
            verdict.Refusal,
            "Abandonment is terminal: a cycle carrying both files must not build, or the two would "
            + "need rules for which hand-written file wins.");
        StringAssert.Contains(verdict.Explanation, "terminal");
    }

    [TestMethod]
    public async Task A_missing_planning_document_is_refused_and_named()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteArtifact(ArtifactKind.Spec);

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(GateRefusal.MissingDocument, verdict.Refusal);
        StringAssert.Contains(verdict.Explanation, "plan.md");
    }

    [TestMethod]
    public async Task A_cycle_with_no_ledger_is_refused_and_told_which_command_to_run()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(GateRefusal.NotApproved, verdict.Refusal);
        StringAssert.Contains(verdict.Explanation, "cycle-approval");
    }

    [TestMethod]
    public async Task A_standing_refusal_is_reported_with_the_reason_that_was_given()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteApproval("changes_requested", reason: "the spec has no criterion for delivery");

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(GateRefusal.Refused, verdict.Refusal);
        StringAssert.Contains(verdict.Explanation, "changes requested");
        StringAssert.Contains(verdict.Explanation, "no criterion for delivery");
    }

    [TestMethod]
    public async Task An_approval_the_documents_match_lets_the_build_run()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(GateRefusal.None, verdict.Refusal, verdict.Explanation);
        Assert.IsTrue(verdict.MayBuild);
        StringAssert.Contains(verdict.Explanation, "Peter Almanzar");
    }

    [TestMethod]
    public async Task Editing_a_document_after_approval_is_refused_and_the_document_is_named()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        workspace.WriteArtifact(ArtifactKind.Spec, "an extra criterion nobody approved");

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(GateRefusal.HashMismatch, verdict.Refusal);
        StringAssert.Contains(
            verdict.Explanation,
            "spec.md",
            "A refusal that does not say which of the three changed leaves nothing to act on.");
        Assert.IsFalse(
            verdict.Explanation.Contains("intent.md", StringComparison.Ordinal),
            "Only the document that changed should be named.");
    }

    [TestMethod]
    public async Task Whitespace_and_line_endings_alone_do_not_look_like_an_edit()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();

        // What a checkout on another platform, or an editor that strips a trailing space, would do.
        string path = workspace.Cycle.PathOf(ArtifactKind.Plan);
        string original = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, original.Replace("\n", "\r\n", StringComparison.Ordinal) + "\r\n\r\n");

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(
            GateRefusal.None,
            verdict.Refusal,
            "A hash that moves when git changes a line ending would refuse every cycle on a "
            + "teammate's machine, which costs more than the narrower check buys.");
    }

    [TestMethod]
    public async Task A_blocker_raised_after_approval_is_refused_because_nothing_accepted_it()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 2, id: "F_07", cites: "CON_04");

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(GateRefusal.UnacceptedBlocker, verdict.Refusal);
        StringAssert.Contains(verdict.Explanation, "spec.F_07");
        StringAssert.Contains(verdict.Explanation, "CON_04");
    }

    [TestMethod]
    public async Task A_blocker_the_approval_accepts_by_name_does_not_refuse_the_build()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);
        workspace.WriteCleanRound(ArtifactKind.Spec);
        workspace.WriteBlockingRound(ArtifactKind.Plan, id: "F_02");

        workspace.WriteApproval(accepted: ["plan.F_02"]);

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(
            GateRefusal.None,
            verdict.Refusal,
            "A person may override the machine as long as the override is recorded. " + verdict.Explanation);
    }

    [TestMethod]
    public async Task A_blocker_answered_in_a_later_round_no_longer_refuses_the_build()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);
        workspace.WriteCleanRound(ArtifactKind.Plan);
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 1);
        workspace.WriteAnsweredRound(ArtifactKind.Spec, round: 2);

        workspace.WriteApproval();

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(
            GateRefusal.None,
            verdict.Refusal,
            "A finding keeps its id across rounds, so the latest round that mentions it says where it "
            + "has got to. " + verdict.Explanation);
    }

    [TestMethod]
    public async Task An_approval_recording_no_hash_for_a_document_is_refused_rather_than_trusted()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        // An entry that records a hash for the intent and omits the other two would otherwise read as
        // approving whatever happens to be on disk for them.
        string hash = ContentHash.Of(await File.ReadAllTextAsync(workspace.Cycle.PathOf(ArtifactKind.Intent)));
        await File.WriteAllTextAsync(
            workspace.Cycle.ApprovalPath,
            $"""
            ---
            cycle: {workspace.Cycle.Id}
            decisions:
              - decision: approved
                by: Peter Almanzar
                at: 2026-10-01T12:00:00Z
                hashes:
                  intent.md: {hash}
            ---
            """);

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(GateRefusal.HashMismatch, verdict.Refusal);
        StringAssert.Contains(verdict.Explanation, "records no hash");
        StringAssert.Contains(verdict.Explanation, "spec.md");
    }

    private static async Task<GateVerdict> Check(CycleWorkspace workspace)
    {
        Result<GateVerdict> result = await new ApprovalGateHandler(workspace.Root)
            .Handle(new ApprovalGateQuery(workspace.Cycle.Id), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
