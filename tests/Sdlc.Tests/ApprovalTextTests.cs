using System.Globalization;
using FluentResults;
using Sdlc.Core.Approvals;
using Sdlc.Core.Cycles;

namespace Sdlc.Tests;

/// <summary>
/// What the approval printer produces, and whether a person pasting it ends up approved.
/// </summary>
/// <remarks>
/// The round-trip test here exists because the first version of the printer emitted aligned columns
/// without their colons: it read correctly, and parsed as nothing. Anything that prints YAML for a
/// person to paste has to be read back by the thing that will read it.
/// </remarks>
[TestClass]
public sealed class ApprovalTextTests
{
    [TestMethod]
    public async Task The_printed_entry_parses_as_the_approval_it_claims_to_be()
    {
        using CycleWorkspace workspace = Ready();

        ApprovalTextResult text = await Print(workspace);

        Assert.IsTrue(text.MayApprove, text.Instructions);

        // Exactly what a person does: the header, then the entry pasted under it.
        string pasted = string.Create(
            CultureInfo.InvariantCulture,
            $"---\ncycle: {workspace.Cycle.Id}\nframework_version: 1.0.0\ndecisions:\n{text.Entry}---\n");

        Result<ApprovalLedger> read = ApprovalLedger.Read(pasted, "pasted");

        Assert.IsTrue(
            read.IsSuccess,
            "The entry the printer produced does not parse: " + string.Join("; ", read.Errors.Select(e => e.Message)));

        GateEntry entry = read.Value.Decisions.Single();
        Assert.AreEqual(GateDecision.Approved, entry.Decision);
        Assert.AreEqual("Peter Almanzar", entry.By);
        Assert.AreEqual(3, entry.Hashes.Count, "All three documents are approved as one act.");
    }

    [TestMethod]
    public async Task Pasting_the_printed_entry_satisfies_the_gate()
    {
        using CycleWorkspace workspace = Ready();

        ApprovalTextResult text = await Print(workspace);
        await File.WriteAllTextAsync(
            workspace.Cycle.ApprovalPath,
            string.Create(
                CultureInfo.InvariantCulture,
                $"---\ncycle: {workspace.Cycle.Id}\ndecisions:\n{text.Entry}---\n"));

        Result<GateVerdict> verdict = await new ApprovalGateHandler(workspace.Root)
            .Handle(new ApprovalGateQuery(workspace.Cycle.Id), CancellationToken.None);

        Assert.IsTrue(verdict.IsSuccess);
        Assert.AreEqual(
            GateRefusal.None,
            verdict.Value.Refusal,
            "Printing an entry the gate then refuses would make the whole flow a dead end. "
            + verdict.Value.Explanation);
    }

    [TestMethod]
    public async Task The_entry_records_the_hash_of_each_document_as_it_stands()
    {
        using CycleWorkspace workspace = Ready();

        ApprovalTextResult text = await Print(workspace);

        foreach (ArtifactState artifact in text.Status.Artifacts)
        {
            StringAssert.Contains(
                text.Entry!,
                artifact.Hash!,
                $"The entry must carry {Cycle.FileNameOf(artifact.Kind)}'s real hash, or the gate has nothing to recompute against.");
        }
    }

    [TestMethod]
    public async Task An_open_blocker_refuses_the_entry_and_names_what_has_to_be_answered()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);
        workspace.WriteCleanRound(ArtifactKind.Spec);
        workspace.WriteBlockingRound(ArtifactKind.Plan, id: "F_02", cites: "CON_06");

        ApprovalTextResult text = await Print(workspace);

        Assert.IsFalse(
            text.MayApprove,
            "There is no entry to print. An approval standing over an open blocker would assert "
            + "something untrue, and printing one would be the tool proposing it.");

        Assert.IsNull(text.Entry);
        StringAssert.Contains(text.Instructions, "plan.F_02");
        StringAssert.Contains(
            text.Instructions,
            "sdlc refine",
            "A refusal at the point someone wanted to approve has to say what to do instead.");
    }

    [TestMethod]
    public async Task A_printed_entry_carries_no_field_for_findings_it_let_through()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        ApprovalTextResult text = await Print(workspace);

        Assert.IsTrue(text.MayApprove);
        Assert.IsFalse(
            text.Entry!.Contains("accepted_findings", StringComparison.Ordinal),
            "The field is gone from the model, so printing it would invite someone to fill it in and "
            + "believe the gate was reading it.");
    }

    [TestMethod]
    public async Task A_cycle_missing_a_document_is_refused_with_the_document_named()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteArtifact(ArtifactKind.Spec);

        ApprovalTextResult text = await Print(workspace);

        Assert.IsFalse(text.MayApprove);
        Assert.IsNull(text.Entry);
        StringAssert.Contains(text.Instructions, "plan.md");
    }

    [TestMethod]
    public async Task A_cycle_nothing_has_interrogated_is_refused_and_told_which_commands_to_run()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        ApprovalTextResult text = await Print(workspace);

        Assert.IsFalse(
            text.MayApprove,
            "Approving three documents nothing has argued with is the single gate resting on nothing.");
        StringAssert.Contains(text.Instructions, "sdlc interrogate");
        StringAssert.Contains(text.Instructions, "intent");
    }

    [TestMethod]
    public async Task An_abandoned_cycle_is_refused()
    {
        using CycleWorkspace workspace = Ready();
        workspace.Abandon();

        ApprovalTextResult text = await Print(workspace);

        Assert.IsFalse(text.MayApprove);
        StringAssert.Contains(text.Instructions, "terminal");
    }

    [TestMethod]
    public async Task The_instructions_say_create_when_there_is_no_ledger_and_open_when_there_is()
    {
        using CycleWorkspace workspace = Ready();

        ApprovalTextResult first = await Print(workspace);
        StringAssert.Contains(first.Instructions, "Create");

        workspace.WriteApproval("changes_requested", reason: "needs more thought");

        ApprovalTextResult second = await Print(workspace);
        StringAssert.Contains(
            second.Instructions,
            "Open",
            "A first entry is a file creation and a revisit is an edit; saying which is what keeps the "
            + "flow from feeling inconsistent.");
    }

    [TestMethod]
    public async Task The_rendering_shows_an_abbreviated_hash_and_the_round_history()
    {
        using CycleWorkspace workspace = Ready();

        string rendered = ApprovalRenderer.Render(await Print(workspace));

        StringAssert.Contains(rendered, "intent");
        StringAssert.Contains(rendered, "r1, clean");
        StringAssert.Contains(rendered, "Paste this:");
    }

    [TestMethod]
    [DataRow(GateDecision.ChangesRequested, "changes_requested")]
    [DataRow(GateDecision.Denied, "denied")]
    [DataRow(GateDecision.Reopened, "reopened")]
    public async Task A_refusal_is_drafted_on_the_same_terms_as_an_approval(
        GateDecision decision,
        string written)
    {
        using CycleWorkspace workspace = Ready();

        ApprovalTextResult text = await Print(workspace, decision);

        Assert.IsTrue(
            text.MayApprove,
            "Rejecting is what a person does when they are least inclined to look a format up. "
            + text.Instructions);

        StringAssert.Contains(text.Entry!, "decision: " + written);
        Assert.AreEqual(decision, text.Decision);
    }

    [TestMethod]
    public async Task A_drafted_refusal_leaves_the_reason_empty_so_an_unfilled_one_fails()
    {
        using CycleWorkspace workspace = Ready();

        ApprovalTextResult text = await Print(workspace, GateDecision.Denied);

        StringAssert.Contains(
            text.Entry!,
            "reason:",
            "The key has to be on the page, or a person has to know the schema to refuse a cycle.");

        string pasted = string.Create(
            CultureInfo.InvariantCulture,
            $"---\ncycle: {workspace.Cycle.Id}\ndecisions:\n{text.Entry}---\n");

        Result<ApprovalLedger> read = ApprovalLedger.Read(pasted, "pasted");

        Assert.IsTrue(
            read.IsFailed,
            "An unfilled refusal has to fail rather than stand. A placeholder would have passed this "
            + "check forever while saying nothing, which is what CON_10 forbids.");

        StringAssert.Contains(read.Errors[0].Message, "why");
    }

    [TestMethod]
    public async Task A_filled_refusal_round_trips_and_leaves_the_cycle_unapproved()
    {
        using CycleWorkspace workspace = Ready();

        ApprovalTextResult text = await Print(workspace, GateDecision.ChangesRequested);

        // What a person does: paste, then write the reason on the empty line.
        string filled = text.Entry!.Replace(
            "    reason:",
            "    reason: the spec has no criterion for delivery as a page at all",
            StringComparison.Ordinal);

        string pasted = string.Create(
            CultureInfo.InvariantCulture,
            $"---\ncycle: {workspace.Cycle.Id}\ndecisions:\n{filled}---\n");

        Result<ApprovalLedger> read = ApprovalLedger.Read(pasted, "pasted");

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));
        Assert.AreEqual(GateDecision.ChangesRequested, read.Value.Current!.Decision);
        Assert.IsFalse(read.Value.IsApproved);
        Assert.AreEqual(3, read.Value.Current.Hashes.Count, "A refusal records what it looked at.");
    }

    [TestMethod]
    public async Task A_blocked_cycle_can_still_be_refused_even_though_it_cannot_be_approved()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);
        workspace.WriteCleanRound(ArtifactKind.Spec);
        workspace.WriteBlockingRound(ArtifactKind.Plan, id: "F_02", cites: "CON_06");

        Assert.IsFalse((await Print(workspace)).MayApprove);

        ApprovalTextResult sent_back = await Print(workspace, GateDecision.ChangesRequested);

        Assert.IsTrue(
            sent_back.MayApprove,
            "A blocked cycle is exactly the one a person wants to send back. Refusing to help with "
            + "that would push them into writing YAML by hand at the worst moment.");
    }

    [TestMethod]
    public async Task A_stale_round_refuses_the_entry_but_not_a_send_back()
    {
        using CycleWorkspace workspace = Ready();
        workspace.WriteArtifact(ArtifactKind.Intent, "a different problem entirely");

        ApprovalTextResult approving = await Print(workspace);

        Assert.IsFalse(approving.MayApprove);
        StringAssert.Contains(approving.Instructions, "spec.md");
        StringAssert.Contains(
            approving.Instructions,
            "sdlc interrogate",
            "The fix is a fresh round, not an edit, so the refusal names the interrogation.");

        Assert.IsTrue(
            (await Print(workspace, GateDecision.ChangesRequested)).MayApprove,
            "A judgement made against a document that has moved is often exactly what the person is "
            + "sending the cycle back about.");
    }

    [TestMethod]
    public async Task An_uninterrogated_cycle_can_be_refused_but_not_approved()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        Assert.IsFalse((await Print(workspace)).MayApprove);
        Assert.IsTrue((await Print(workspace, GateDecision.Denied)).MayApprove);
    }

    [TestMethod]
    public async Task A_cycle_missing_a_document_cannot_be_refused_either()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);

        ApprovalTextResult text = await Print(workspace, GateDecision.Denied);

        Assert.IsFalse(
            text.MayApprove,
            "Every decision records three hashes, so there is nothing to refuse until all three "
            + "documents exist.");
    }

    private static CycleWorkspace Ready()
    {
        var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        return workspace;
    }

    private static async Task<ApprovalTextResult> Print(
        CycleWorkspace workspace,
        GateDecision decision = GateDecision.Approved)
    {
        Result<ApprovalTextResult> result = await new ApprovalTextHandler(workspace.Root)
            .Handle(
                new ApprovalTextQuery(workspace.Cycle.Id, "Peter Almanzar", decision),
                CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
