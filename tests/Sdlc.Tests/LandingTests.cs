using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Landing;

namespace Sdlc.Tests;

/// <summary>
/// Whether a cycle may be landed, and the remediation bound that decides it.
/// </summary>
/// <remarks>
/// Iteration 3 called bounded remediation its largest untested claim: the state machine existed entirely on
/// paper and landing had never refused a cycle. Everything unattended rests on it -- a batch is only safe to
/// run unwatched because a cycle that cannot be made to pass stops instead of landing. So these tests drive
/// it to escalated on purpose, which is the thing that had never been done.
/// </remarks>
[TestClass]
public sealed class LandingTests
{
    [TestMethod]
    public async Task A_verified_cycle_whose_chain_holds_may_be_landed()
    {
        using CycleWorkspace workspace = Passing();

        Land.Response landable = await Ask(workspace);

        Assert.IsTrue(landable.MayLand, landable.Explanation);
    }

    [TestMethod]
    public async Task A_cycle_nobody_verified_may_not_be_landed()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        Land.Response landable = await Ask(workspace);

        Assert.AreEqual(Land.LandRefusal.NotVerified, landable.Refusal);
    }

    [TestMethod]
    [DataRow("blocked")]
    [DataRow("escalated")]
    public async Task A_cycle_that_did_not_pass_may_not_be_landed(string outcome)
    {
        // The verdict is the only thing standing between unattended building and unreviewed work reaching a
        // pull request.
        using CycleWorkspace workspace = Passing();
        workspace.WriteVerify(outcome);

        Land.Response landable = await Ask(workspace);

        Assert.AreEqual(Land.LandRefusal.NotPassed, landable.Refusal, landable.Explanation);
    }

    [TestMethod]
    public async Task A_cycle_that_spent_its_remediation_budget_is_escalated_and_never_lands()
    {
        // The state iteration 3 never reached. Four verify rounds: the first is the verification, the three
        // after it are the passes, and a blocker still stands.
        using CycleWorkspace workspace = Passing();

        for (int round = 1; round <= RemediationBudget.Limit + 1; round++)
        {
            workspace.WriteBlockingRound(ArtifactKind.Verify, round, "F_01", "CON_08");
        }

        Land.Response landable = await Ask(workspace);

        Assert.AreEqual(Land.LandRefusal.Escalated, landable.Refusal, landable.Explanation);
        StringAssert.Contains(landable.Explanation, "3 remediation passes spent");
        StringAssert.Contains(
            landable.Explanation,
            "makes an unattended batch safe",
            "The refusal has to say why it exists, or it reads as the tool being obstructive.");
    }

    [TestMethod]
    public async Task A_cycle_still_inside_its_budget_is_blocked_rather_than_escalated()
    {
        using CycleWorkspace workspace = Passing();

        workspace.WriteBlockingRound(ArtifactKind.Verify, round: 1, id: "F_01", cites: "CON_08");
        workspace.WriteBlockingRound(ArtifactKind.Verify, round: 2, id: "F_01", cites: "CON_08");

        Result<RemediationState> state = await RemediationBudget.Read(
            workspace.Cycle,
            CancellationToken.None);

        Assert.IsTrue(state.IsSuccess);
        Assert.AreEqual(1, state.Value.Spent, "The first round is the verification, not a pass.");
        Assert.IsTrue(state.Value.Blocked);
        Assert.IsFalse(state.Value.Escalated);
        Assert.AreEqual(RemediationBudget.Limit, state.Value.Limit);
        StringAssert.Contains(
            RemediationBudget.Describe(state.Value),
            "spent of 3",
            "A cycle inside its budget says how much of it is left, in the one place that phrase is built.");
    }

    [TestMethod]
    public async Task A_blocker_answered_within_the_budget_clears_the_cycle()
    {
        using CycleWorkspace workspace = Passing();

        workspace.WriteBlockingRound(ArtifactKind.Verify, round: 1, id: "F_01", cites: "CON_08");
        workspace.WriteAnsweredRound(ArtifactKind.Verify, round: 2, id: "F_01");

        Result<RemediationState> state = await RemediationBudget.Read(
            workspace.Cycle,
            CancellationToken.None);

        Assert.IsTrue(state.IsSuccess);
        Assert.IsFalse(state.Value.Blocked);
        Assert.IsFalse(state.Value.Escalated);
        StringAssert.Contains(RemediationBudget.Describe(state.Value), "cleared after 1 pass");
    }

    [TestMethod]
    public async Task A_cycle_with_no_verify_rounds_at_all_has_spent_nothing()
    {
        using CycleWorkspace workspace = Passing();

        Result<RemediationState> state = await RemediationBudget.Read(
            workspace.Cycle,
            CancellationToken.None);

        Assert.IsTrue(state.IsSuccess);
        Assert.AreEqual(0, state.Value.Spent);
        Assert.AreEqual("nothing to remediate", RemediationBudget.Describe(state.Value));
    }

    [TestMethod]
    public async Task A_verified_cycle_whose_chain_broke_afterwards_is_refused_before_it_reaches_CI()
    {
        // A verdict recorded an hour ago says nothing about what is on disk now, and the pull-request check
        // would refuse it anyway -- so refusing here costs nothing and saves a round trip.
        using CycleWorkspace workspace = Passing();
        File.Delete(workspace.Cycle.ApprovalPath);

        Land.Response landable = await Ask(workspace);

        Assert.AreEqual(Land.LandRefusal.ChainBroken, landable.Refusal, landable.Explanation);
        StringAssert.Contains(landable.Explanation, "saves a round trip");
    }

    [TestMethod]
    public async Task The_sweep_lists_every_cycle_and_not_only_the_ones_it_would_land()
    {
        // A list of only the eligible ones hides the surprise that matters most: a cycle from last week
        // still sitting there verified, which the sweep picks up because every passed cycle is eligible.
        using CycleWorkspace passing = Passing();

        Land.Response ready = await Ask(passing);
        Land.Response skipped = new(passing.Cycle, Land.LandRefusal.Escalated, "spent its budget", null);

        string rendered = LandRenderer.RenderSweep([ready, skipped]);

        StringAssert.Contains(rendered, "1 to land of 2 examined");
        StringAssert.Contains(rendered, "land  ");
        StringAssert.Contains(rendered, "skip  ");
        StringAssert.Contains(rendered, "from last week");
    }

    [TestMethod]
    public void A_sweep_with_nothing_ready_says_so_rather_than_printing_a_warning()
    {
        using var workspace = CycleWorkspace.Create();

        string rendered = LandRenderer.RenderSweep(
            [new Land.Response(workspace.Cycle, Land.LandRefusal.NotVerified, "not verified", null)]);

        StringAssert.Contains(rendered, "Nothing to land.");
        Assert.IsFalse(
            rendered.Contains("Each of these opens a pull request", StringComparison.Ordinal),
            "A confirmation about pushing nothing is a confirmation that teaches people to click through.");
    }

    [TestMethod]
    public void Every_sweep_line_is_ascii()
    {
        using var workspace = CycleWorkspace.Create();

        string rendered = LandRenderer.RenderSweep(
            [new Land.Response(workspace.Cycle, Land.LandRefusal.None, "fine", null)]);

        Assert.IsTrue(rendered.All(char.IsAscii), rendered);
    }

    /// <summary>A cycle that verified clean and whose chain holds.</summary>
    private static CycleWorkspace Passing()
    {
        var workspace = CycleWorkspace.Create();

        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteSpec(criteria: 1);
        workspace.WritePlan(proofs: 1);
        workspace.WriteVerify(outcome: "pass");
        workspace.WriteTestSummary(proofs: 1);

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        return workspace;
    }

    private static async Task<Land.Response> Ask(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        Result<Land.Response> result = await gate
            .Send<Land.Query, Land.Response>(new Land.Query(workspace.Cycle.Id));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
