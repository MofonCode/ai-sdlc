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
        StringAssert.Contains(verdict.Explanation, "draft approval");
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
    public async Task A_blocker_raised_after_approval_refuses_the_build()
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

        Assert.AreEqual(GateRefusal.OpenBlocker, verdict.Refusal);
        StringAssert.Contains(verdict.Explanation, "spec.F_07");
        StringAssert.Contains(verdict.Explanation, "CON_04");
    }

    [TestMethod]
    public async Task An_approval_standing_over_an_open_blocker_refuses_the_build_with_no_way_past()
    {
        // The hard rule, asserted at the gate rather than only at the printer. The printer refuses to
        // produce an entry while a blocker is open, so an approval in this state was hand-written -- and
        // a hand-written one must not buy anything a printed one could not.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);
        workspace.WriteCleanRound(ArtifactKind.Spec);
        workspace.WriteBlockingRound(ArtifactKind.Plan, id: "F_02");

        workspace.WriteApproval();

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(
            GateRefusal.OpenBlocker,
            verdict.Refusal,
            "A blocking finding cannot be waived, accepted or overridden. " + verdict.Explanation);

        StringAssert.Contains(verdict.Explanation, "plan.F_02");
        StringAssert.Contains(
            verdict.Explanation,
            "cannot be waived",
            "The refusal has to say there is no flag, or someone goes looking for one.");
    }

    [TestMethod]
    public async Task A_reopen_after_an_approval_refuses_the_build_again()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval(
            decision: "reopened",
            reason: "the plan proves criterion 3 against a stub");

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(GateRefusal.Refused, verdict.Refusal);
        StringAssert.Contains(
            verdict.Explanation,
            "reopened",
            "The refusal names the standing decision, so a reopen does not read as a plain denial.");
        StringAssert.Contains(verdict.Explanation, "criterion 3");
    }

    [TestMethod]
    public async Task An_intent_edited_after_a_clean_spec_refuses_the_build()
    {
        // The three approval hashes cannot catch this. They record that three documents were read, not
        // that the spec was derived from this intent, so re-approving after the edit leaves every hash
        // matching and a spec judged against an intent that no longer exists.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteArtifact(ArtifactKind.Intent, "a different problem entirely");
        workspace.WriteApproval();

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(
            GateRefusal.StaleRound,
            verdict.Refusal,
            "Every approval hash matches what is on disk, so only the round knows. " + verdict.Explanation);

        StringAssert.Contains(verdict.Explanation, "spec.md");
        StringAssert.Contains(verdict.Explanation, "intent.md");
    }

    [TestMethod]
    public async Task Re_interrogating_the_downstream_clears_the_staleness()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteArtifact(ArtifactKind.Intent, "a different problem entirely");

        // What the fix is: judge the spec against the intent as it stands now.
        workspace.WriteCleanRound(ArtifactKind.Spec, round: 2);
        workspace.WriteApproval();

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(GateRefusal.None, verdict.Refusal, verdict.Explanation);
    }

    [TestMethod]
    public async Task A_round_that_records_no_upstream_at_all_refuses_the_build()
    {
        // Fail closed. A round that cannot say what it judged is not evidence that it judged the right
        // thing, and treating a missing field as fine would make the check skippable by omitting it.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);
        workspace.WriteCleanRound(ArtifactKind.Plan);

        CycleWorkspace.WriteRaw(
            workspace.Cycle.FindingsPath(ArtifactKind.Spec, 1),
            $$"""
              {
                "cycle": "{{workspace.Cycle.Id}}",
                "artifact": "spec.md",
                "round": 1,
                "findings": []
              }
              """);

        workspace.WriteApproval();

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(GateRefusal.StaleRound, verdict.Refusal, verdict.Explanation);
        StringAssert.Contains(
            verdict.Explanation,
            "did not record",
            "A round that recorded nothing and one that recorded the wrong hash need different words, "
            + "or a person goes hunting for an edit that never happened.");
    }

    [TestMethod]
    public async Task An_intent_is_never_stale_because_it_has_no_upstream()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();

        GateVerdict verdict = await Check(workspace);

        Assert.AreEqual(
            GateRefusal.None,
            verdict.Refusal,
            "The intent's round records no upstream because there is none. Requiring one would refuse "
            + "every cycle. " + verdict.Explanation);
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
        using var gate = Gate.For(workspace.Root);

        Result<GateVerdict> result = await gate
            .Send<ApprovalGate.Request, GateVerdict>(new ApprovalGate.Request(workspace.Cycle.Id));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
