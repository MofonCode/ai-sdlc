using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
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

        ApprovalText.Response text = await Print(workspace);

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

        ApprovalText.Response text = await Print(workspace);
        await File.WriteAllTextAsync(
            workspace.Cycle.ApprovalPath,
            string.Create(
                CultureInfo.InvariantCulture,
                $"---\ncycle: {workspace.Cycle.Id}\ndecisions:\n{text.Entry}---\n"));

        using var gate = Gate.For(workspace.Root);

        Result<ApprovalGate.Response> verdict = await gate
            .Send<ApprovalGate.Query, ApprovalGate.Response>(new ApprovalGate.Query(workspace.Cycle.Id));

        Assert.IsTrue(verdict.IsSuccess);
        Assert.AreEqual(
            ApprovalGate.GateRefusal.None,
            verdict.Value.Refusal,
            "Printing an entry the gate then refuses would make the whole flow a dead end. "
            + verdict.Value.Explanation);
    }

    [TestMethod]
    public async Task The_entry_records_the_hash_of_each_document_as_it_stands()
    {
        using CycleWorkspace workspace = Ready();

        ApprovalText.Response text = await Print(workspace);

        foreach (ReadCycleStatus.ArtifactState artifact in text.Status.Artifacts)
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

        ApprovalText.Response text = await Print(workspace);

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

        ApprovalText.Response text = await Print(workspace);

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

        ApprovalText.Response text = await Print(workspace);

        Assert.IsFalse(text.MayApprove);
        Assert.IsNull(text.Entry);
        StringAssert.Contains(text.Instructions, "plan.md");
    }

    [TestMethod]
    public async Task A_cycle_nothing_has_interrogated_is_refused_and_told_which_commands_to_run()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        ApprovalText.Response text = await Print(workspace);

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

        ApprovalText.Response text = await Print(workspace);

        Assert.IsFalse(text.MayApprove);
        StringAssert.Contains(text.Instructions, "terminal");
    }

    [TestMethod]
    public async Task A_revisit_carries_the_standing_decisions_forward_so_the_rename_loses_none()
    {
        using CycleWorkspace workspace = Ready();

        ApprovalText.Response first = await Print(workspace);
        Assert.AreEqual(0, first.CarriesForward, "There is no ledger yet to carry anything forward.");

        workspace.WriteApproval("changes_requested", reason: "needs more thought, and a comma");

        ApprovalText.Response second = await Print(workspace);

        Assert.AreEqual(
            1,
            second.CarriesForward,
            "The ledger is append-only and the decision is placed by renaming the draft over it, so a "
            + "draft that did not reproduce what stands would discard it on the rename.");

        Result<ApprovalLedger> read = ApprovalLedger.Read(second.FileText!, "draft");

        Assert.IsTrue(
            read.IsSuccess,
            "The draft does not parse: " + string.Join("; ", read.Errors.Select(e => e.Message)));

        Assert.AreEqual(2, read.Value.Decisions.Count);
        Assert.AreEqual(GateDecision.ChangesRequested, read.Value.Decisions[0].Decision);
        Assert.AreEqual(GateDecision.Approved, read.Value.Decisions[1].Decision);

        Assert.AreEqual(
            "needs more thought, and a comma",
            read.Value.Decisions[0].Reason,
            "A reason is prose a person wrote. It is carried forward as bytes rather than re-emitted, "
            + "because a YAML round trip is where punctuation becomes a different document.");
    }

    [TestMethod]
    public async Task A_ledger_that_cannot_be_read_does_not_block_the_replacement_for_it()
    {
        using CycleWorkspace workspace = Ready();

        // Exactly the file a mis-placed entry leaves: the header indented, the key under it not. It
        // reads correctly and parses as nothing.
        await File.WriteAllTextAsync(
            workspace.Cycle.ApprovalPath,
            string.Create(
                CultureInfo.InvariantCulture,
                $"---\n  cycle: {workspace.Cycle.Id}\n  framework_version: 1.0.0\ndecisions:\n  - decision: approved\n---\n"));

        ApprovalText.Response text = await Print(workspace);

        Assert.IsTrue(
            text.MayApprove,
            "The file a person writes is the file most likely to be malformed, so the command that "
            + "replaces it has to survive one. " + text.Instructions);

        Result<ApprovalLedger> read = ApprovalLedger.Read(text.FileText!, "draft");

        Assert.IsTrue(read.IsSuccess, "The replacement does not parse either.");
        Assert.AreEqual(
            1,
            read.Value.Decisions.Count,
            "Nothing was carried forward from a file that will not parse: it is a failed paste rather "
            + "than a decision somebody took.");

        StringAssert.Contains(text.Instructions, "cannot be read");
    }

    [TestMethod]
    public async Task The_draft_is_written_where_the_instructions_say_it_is()
    {
        using CycleWorkspace workspace = Ready();

        using var gate = Gate.For(workspace.Root);

        Result<ApprovalText.Response> result = await gate
            .Send<WriteApprovalDraft.Command, ApprovalText.Response>(
                new WriteApprovalDraft.Command(workspace.Cycle.Id, "Peter Almanzar"));

        Assert.IsTrue(result.IsSuccess);

        ApprovalText.Response text = result.Value;

        Assert.AreEqual(
            workspace.Cycle.ApprovalDraftPath("approved"),
            text.DraftPath,
            "The decision is in the name, so the rename names what is being recorded.");

        Assert.IsTrue(File.Exists(text.DraftPath!), "Nothing was written for a person to rename.");

        Assert.AreEqual(
            text.FileText,
            await File.ReadAllTextAsync(text.DraftPath!),
            "What was shown and what is on disk have to be the same bytes, or the reader approved "
            + "something other than what the rename puts in place.");

        Assert.IsFalse(
            File.Exists(workspace.Cycle.ApprovalPath),
            "Drafting is not approving. Only the rename records a decision, and no command does it.");
    }

    [TestMethod]
    public async Task The_rendering_shows_an_abbreviated_hash_and_the_round_history()
    {
        using CycleWorkspace workspace = Ready();

        string rendered = ApprovalRenderer.Render(await Print(workspace));

        StringAssert.Contains(rendered, "intent");
        StringAssert.Contains(rendered, "r1, clean");
        StringAssert.Contains(rendered, "This file:");
        StringAssert.Contains(rendered, "Rename it to record the decision:");
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

        ApprovalText.Response text = await Print(workspace, decision);

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

        ApprovalText.Response text = await Print(workspace, GateDecision.Denied);

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

        ApprovalText.Response text = await Print(workspace, GateDecision.ChangesRequested);

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

        ApprovalText.Response sent_back = await Print(workspace, GateDecision.ChangesRequested);

        Assert.IsTrue(
            sent_back.MayApprove,
            "A blocked cycle is exactly the one a person wants to send back. Refusing to help with "
            + "that would push them into writing YAML by hand at the worst moment.");
    }

    [TestMethod]
    public async Task The_printed_header_declares_the_framework_version()
    {
        using CycleWorkspace workspace = Ready();

        ApprovalText.Response text = await Print(workspace);

        StringAssert.Contains(
            text.FileText,
            "framework_version: " + Cycle.FrameworkVersion,
            "The envelope requires the field on every artifact. A printed header that omitted it would "
            + "make a first entry the one file in the cycle that does not declare its format.");
    }

    [TestMethod]
    public async Task A_stale_round_refuses_the_entry_but_not_a_send_back()
    {
        using CycleWorkspace workspace = Ready();
        workspace.WriteArtifact(ArtifactKind.Intent, "a different problem entirely");

        ApprovalText.Response approving = await Print(workspace);

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

        ApprovalText.Response text = await Print(workspace, GateDecision.Denied);

        Assert.IsFalse(
            text.MayApprove,
            "Every decision records three hashes, so there is nothing to refuse until all three "
            + "documents exist.");
    }

    [TestMethod]
    public async Task The_entry_is_stamped_with_the_clock_the_container_supplied()
    {
        // The reason the handler takes a TimeProvider. Read from a static this line could only be
        // asserted around -- a regex, or a range either side of now -- and the format is the part that
        // matters, because the ledger is parsed back and 'at' is what orders two decisions.
        using CycleWorkspace workspace = Ready();
        using var gate = Gate.For(
            workspace.Root,
            services => services.AddSingleton<TimeProvider>(
                new FakeClock(new DateTimeOffset(2026, 10, 3, 14, 7, 9, 512, TimeSpan.Zero))));

        Result<ApprovalText.Response> result = await gate
            .Send<ApprovalText.Query, ApprovalText.Response>(
                new ApprovalText.Query(workspace.Cycle.Id, "Peter Almanzar"));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        StringAssert.Contains(result.Value.Entry, "    at: 2026-10-03T14:07:09Z");
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

    private static async Task<ApprovalText.Response> Print(
        CycleWorkspace workspace,
        GateDecision decision = GateDecision.Approved)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ApprovalText.Response> result = await gate
            .Send<ApprovalText.Query, ApprovalText.Response>(
                new ApprovalText.Query(workspace.Cycle.Id, "Peter Almanzar", decision));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
