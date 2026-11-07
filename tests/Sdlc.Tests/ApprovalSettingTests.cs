using FluentResults;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// The <c>approval</c> key in <c>solution.md</c> and what the gate does under each value (D-137).
/// </summary>
/// <remarks>
/// The assertions that matter most are the ones that hold under <c>none</c> exactly as under an approval: a
/// blocker, an uninterrogated document and a document edited after its round all still stop the build. A
/// setting that switched any of those off would be the waiver D-27 says no mechanism may be.
/// </remarks>
[TestClass]
public sealed class ApprovalSettingTests
{
    [TestMethod]
    public void An_absent_key_reads_as_required_so_every_solution_adopted_before_it_keeps_its_gate()
    {
        Result<SolutionDocument> read = SolutionDocument.Read("---\norigin: adopted\n---\n", "solution.md");

        Assert.IsTrue(read.IsSuccess);
        Assert.AreEqual(ApprovalRequirement.Required, read.Value.Approval);
    }

    [TestMethod]
    public void None_is_read_and_a_value_nobody_recognises_fails_rather_than_reading_as_either()
    {
        Assert.AreEqual(
            ApprovalRequirement.None,
            SolutionDocument.Read("---\norigin: adopted\napproval: none\n---\n", "solution.md").Value.Approval);

        Result<SolutionDocument> typo = SolutionDocument.Read("---\norigin: adopted\napproval: no\n---\n", "solution.md");

        Assert.IsTrue(typo.IsFailed);
        StringAssert.Contains(typo.Errors[0].Message, "required or none");
    }

    [TestMethod]
    public void The_key_block_always_carries_approval_so_a_team_can_see_the_setting_it_has()
    {
        string[] keys = SolutionDocument.Keys(SolutionOrigin.Adopted, SolutionStandard.Existing, Sdlc.Core.Init.Platform.GitHub);

        CollectionAssert.Contains(keys, "approval: required");
        CollectionAssert.Contains(
            SolutionDocument.Keys(SolutionOrigin.Adopted, SolutionStandard.Existing, Sdlc.Core.Init.Platform.GitHub, approval: ApprovalRequirement.None),
            "approval: none");
    }

    [TestMethod]
    public async Task Under_none_a_cycle_whose_rounds_are_clean_builds_and_says_nobody_approved_it()
    {
        using CycleWorkspace workspace = Declared();
        JudgedClean(workspace);

        ApprovalGate.Response verdict = await Check(workspace);

        Assert.AreEqual(ApprovalGate.GateRefusal.None, verdict.Refusal, verdict.Explanation);
        Assert.IsTrue(verdict.MayBuildUnapproved);
        Assert.IsNull(verdict.Ledger);
        StringAssert.Contains(verdict.Explanation, "approval: none");
        Assert.IsTrue(verdict.Hashes.All(hash => hash.Matches), "Each document is compared with what its round judged.");
    }

    [TestMethod]
    public async Task Under_none_an_open_blocker_still_stops_the_build()
    {
        using CycleWorkspace workspace = Declared();
        JudgedClean(workspace);
        workspace.WriteBlockingRound(ArtifactKind.Plan, round: 2);

        ApprovalGate.Response verdict = await Check(workspace);

        Assert.AreEqual(ApprovalGate.GateRefusal.OpenBlocker, verdict.Refusal);
        Assert.IsTrue(verdict.Declared);
        StringAssert.Contains(verdict.Explanation, "waives");
    }

    [TestMethod]
    public async Task Under_none_a_document_edited_after_its_round_is_refused_as_stale()
    {
        using CycleWorkspace workspace = Declared();
        JudgedClean(workspace);
        workspace.WriteArtifact(ArtifactKind.Plan, "edited after it was judged");

        ApprovalGate.Response verdict = await Check(workspace);

        Assert.AreEqual(ApprovalGate.GateRefusal.StaleRound, verdict.Refusal);
        StringAssert.Contains(verdict.Explanation, "plan.md");
    }

    [TestMethod]
    public async Task Under_none_a_round_that_recorded_no_subject_is_refused_because_nothing_else_records_it()
    {
        using CycleWorkspace workspace = Declared();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        ApprovalGate.Response verdict = await Check(workspace);

        Assert.AreEqual(ApprovalGate.GateRefusal.StaleRound, verdict.Refusal);
        StringAssert.Contains(verdict.Explanation, "did not record");
    }

    [TestMethod]
    public async Task Under_none_a_document_never_interrogated_is_refused()
    {
        using CycleWorkspace workspace = Declared();
        workspace.WriteAllArtifacts();
        workspace.WriteJudgedCleanRound(ArtifactKind.Intent);

        ApprovalGate.Response verdict = await Check(workspace);

        Assert.AreEqual(ApprovalGate.GateRefusal.Uninterrogated, verdict.Refusal);
        Assert.IsFalse(verdict.Explanation.Contains("draft approval", StringComparison.Ordinal), "No approval is needed, so none is asked for.");
    }

    [TestMethod]
    public async Task Under_none_a_refusal_a_person_placed_is_honoured()
    {
        using CycleWorkspace workspace = Declared();
        JudgedClean(workspace);
        workspace.WriteApproval("changes_requested", reason: "the spec has no criterion for delivery");

        ApprovalGate.Response verdict = await Check(workspace);

        Assert.AreEqual(ApprovalGate.GateRefusal.Refused, verdict.Refusal);
        Assert.IsFalse(verdict.Declared);
    }

    [TestMethod]
    public async Task A_solution_document_that_cannot_be_read_requires_an_approval()
    {
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(SolutionDocument.PathIn(workspace.Root), "---\norigin: adopted\napproval: off\n---\n");
        JudgedClean(workspace);

        ApprovalGate.Response verdict = await Check(workspace);

        Assert.AreEqual(
            ApprovalGate.GateRefusal.NotApproved,
            verdict.Refusal,
            "Could not tell must never read as the permissive answer.");
    }

    [TestMethod]
    public async Task Required_declared_in_full_refuses_a_cycle_with_no_approval_as_absent_does()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted, approval: "required");
        JudgedClean(workspace);

        Assert.AreEqual(ApprovalGate.GateRefusal.NotApproved, (await Check(workspace)).Refusal);
    }

    [TestMethod]
    public void The_chain_states_that_no_person_approved_in_place_of_its_caveat_about_an_approval()
    {
        using var workspace = CycleWorkspace.Create();
        VerifyChain.ChainCheck held = new("the gate is satisfied", true, "no approval needed");

        string unapproved = ChainRenderer.Render(new VerifyChain.Response(workspace.Cycle, [held], Unapproved: true));
        string approved = ChainRenderer.Render(new VerifyChain.Response(workspace.Cycle, [held]));

        StringAssert.Contains(unapproved, "No person approved this plan");
        Assert.IsFalse(unapproved.Contains("approval entry exists", StringComparison.Ordinal));
        StringAssert.Contains(approved, "approval entry exists");
    }

    [TestMethod]
    public async Task Settings_shows_approval_as_declared_or_default_and_never_blank()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted);

        ReadSettings.Setting defaulted = (await Settings(workspace)).Settings.Single(setting => setting.Name == "approval");

        Assert.AreEqual("required", defaulted.Value);
        Assert.AreEqual(ReadSettings.SettingSource.Default, defaulted.Source);

        workspace.WriteSolution(SolutionOrigin.Adopted, approval: "none");
        ReadSettings.Setting declared = (await Settings(workspace)).Settings.Single(setting => setting.Name == "approval");

        Assert.AreEqual("none", declared.Value);
        Assert.AreEqual(ReadSettings.SettingSource.Declared, declared.Source);
    }

    [TestMethod]
    public async Task Settings_over_an_unreadable_document_is_incomplete_and_shows_no_defaults()
    {
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(SolutionDocument.PathIn(workspace.Root), "---\norigin: adopted\napproval: off\n---\n");

        ReadSettings.Response view = await Settings(workspace);

        Assert.IsFalse(view.Complete);
        Assert.AreEqual(ReadSettings.SettingSource.Unreadable, view.Settings.Single(setting => setting.Name == "approval").Source);
        StringAssert.Contains(SettingsRenderer.Render(view), "cannot be read");
    }

    [TestMethod]
    public async Task Settings_points_at_decisions_rather_than_listing_them()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted);

        ReadSettings.Setting decisions = (await Settings(workspace)).Settings.Single(setting => setting.Name == "decisions");

        Assert.AreEqual(ReadSettings.SettingGroup.Elsewhere, decisions.Group);
        Assert.AreEqual("sdlc decisions", decisions.Change);
    }

    private static CycleWorkspace Declared()
    {
        var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted, approval: "none");
        return workspace;
    }

    private static void JudgedClean(CycleWorkspace workspace)
    {
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteJudgedCleanRound(kind);
        }
    }

    private static async Task<ApprovalGate.Response> Check(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ApprovalGate.Response> result = await gate
            .Send<ApprovalGate.Query, ApprovalGate.Response>(new ApprovalGate.Query(workspace.Cycle.Id));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static async Task<ReadSettings.Response> Settings(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ReadSettings.Response> result = await gate
            .Send<ReadSettings.Query, ReadSettings.Response>(new ReadSettings.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
