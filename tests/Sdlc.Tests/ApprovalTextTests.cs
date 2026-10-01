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
    public async Task An_open_blocker_is_pre_listed_as_accepted_so_carrying_it_is_a_visible_act()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);
        workspace.WriteCleanRound(ArtifactKind.Spec);
        workspace.WriteBlockingRound(ArtifactKind.Plan, id: "F_02", cites: "CON_06");

        ApprovalTextResult text = await Print(workspace);

        Assert.IsTrue(text.MayApprove);
        StringAssert.Contains(text.Entry!, "accepted_findings: [plan.F_02]");
        StringAssert.Contains(
            text.Instructions,
            "plan.F_02",
            "The instructions have to say what pasting the entry agrees to carry.");
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

    private static async Task<ApprovalTextResult> Print(CycleWorkspace workspace)
    {
        Result<ApprovalTextResult> result = await new ApprovalTextHandler(workspace.Root)
            .Handle(new ApprovalTextQuery(workspace.Cycle.Id, "Peter Almanzar"), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
