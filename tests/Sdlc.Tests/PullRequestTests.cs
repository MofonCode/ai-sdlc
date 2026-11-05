using System.Globalization;
using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.PullRequests;

namespace Sdlc.Tests;

/// <summary>
/// Whether a cycle may open a pull request, and the remediation bound that decides it.
/// </summary>
/// <remarks>
/// Iteration 3 called bounded remediation its largest untested claim: the state machine existed entirely on
/// paper and the check had never refused a cycle. Everything unattended rests on it -- a batch is only safe
/// to run unwatched because a cycle that cannot be made to pass stops instead of opening a pull request. So
/// these tests drive it to escalated on purpose, which is the thing that had never been done.
/// </remarks>
[TestClass]
public sealed class PullRequestTests
{
    [TestMethod]
    public async Task A_verified_cycle_whose_chain_holds_may_open_a_pull_request()
    {
        using CycleWorkspace workspace = Passing();

        PullRequest.Response answer = await Ask(workspace);

        Assert.IsTrue(answer.MayOpen, answer.Explanation);
    }

    [TestMethod]
    public async Task A_cycle_nobody_verified_may_not_open_one()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        PullRequest.Response answer = await Ask(workspace);

        Assert.AreEqual(PullRequest.PullRequestRefusal.NotVerified, answer.Refusal);
    }

    [TestMethod]
    [DataRow("blocked")]
    [DataRow("escalated")]
    public async Task A_cycle_that_did_not_pass_may_not_open_one(string outcome)
    {
        // The verdict is the only thing standing between unattended building and unreviewed work reaching a
        // pull request.
        using CycleWorkspace workspace = Passing();
        workspace.WriteVerify(outcome);

        PullRequest.Response answer = await Ask(workspace);

        Assert.AreEqual(PullRequest.PullRequestRefusal.NotPassed, answer.Refusal, answer.Explanation);
    }

    [TestMethod]
    public async Task A_cycle_whose_passes_stop_reducing_anything_is_escalated_and_never_opens_one()
    {
        // The state iteration 3 never reached. Four verify rounds with the same blocker standing through
        // all of them: the last two passes reduced nothing, so the loop is not getting anywhere.
        using CycleWorkspace workspace = Passing();

        for (int round = 1; round <= 4; round++)
        {
            workspace.WriteBlockingRound(ArtifactKind.Verify, round, "F_01", "CON_08");
        }

        PullRequest.Response answer = await Ask(workspace);

        Assert.AreEqual(PullRequest.PullRequestRefusal.Escalated, answer.Refusal, answer.Explanation);
        StringAssert.Contains(answer.Explanation, "the last two passes reduced nothing");
        StringAssert.Contains(
            answer.Explanation,
            "makes an unattended batch safe",
            "The refusal has to say why it exists, or it reads as the tool being obstructive.");
    }

    [TestMethod]
    public async Task One_pass_that_reduced_nothing_is_blocked_rather_than_escalated()
    {
        // One flat pass is not a stall. A pass that fixes one thing and uncovers another of equal size
        // has made progress the count cannot see, so it takes two in a row.
        using CycleWorkspace workspace = Passing();

        workspace.WriteBlockingRound(ArtifactKind.Verify, round: 1, id: "F_01", cites: "CON_08");
        workspace.WriteBlockingRound(ArtifactKind.Verify, round: 2, id: "F_01", cites: "CON_08");

        Result<RemediationState> state = await RemediationBudget.Read(
            workspace.Cycle,
            CancellationToken.None);

        Assert.IsTrue(state.IsSuccess);
        Assert.AreEqual(1, state.Value.Spent, "The first round is the verification, not a pass.");
        Assert.IsTrue(state.Value.Blocked);
        Assert.IsFalse(state.Value.Stalled);
        Assert.IsFalse(state.Value.Escalated);
        StringAssert.Contains(
            RemediationBudget.Describe(state.Value),
            "making progress",
            "A cycle that is still moving says so, in the one place that phrase is built.");
    }

    [TestMethod]
    public async Task A_pass_that_undoes_earlier_work_escalates_at_once()
    {
        // The code-side shape of oscillation, and the one signal that does not wait: a loop breaking what
        // it already fixed is not going to finish, and every further pass is spend against a moving
        // target. Answered in round two, raised again in round three.
        using CycleWorkspace workspace = Passing();

        workspace.WriteBlockingRound(ArtifactKind.Verify, round: 1, id: "F_01", cites: "CON_08");
        workspace.WriteAnsweredRound(ArtifactKind.Verify, round: 2, id: "F_01", cites: "CON_08");
        workspace.WriteBlockingRound(ArtifactKind.Verify, round: 3, id: "F_02", cites: "CON_08");

        Result<RemediationState> state = await RemediationBudget.Read(
            workspace.Cycle,
            CancellationToken.None);

        Assert.IsTrue(state.IsSuccess);
        Assert.IsTrue(state.Value.Regressed);
        Assert.IsTrue(state.Value.Escalated);
        StringAssert.Contains(RemediationBudget.Describe(state.Value), "a pass undid earlier work");
    }

    [TestMethod]
    public async Task A_cycle_that_keeps_reducing_what_is_open_never_escalates_however_many_passes_it_takes()
    {
        // There is no ceiling. Six passes, each one clearing a finding, is a remediation that is working
        // -- and the blind three this replaced would have stopped the cycle on its fourth.
        using CycleWorkspace workspace = Passing();

        // Each pass answers one finding by name. Leaving it out of the round would not do: silence is not an
        // answer (D-49), and progress is read from what each round left standing (D-107).
        for (int round = 1; round <= 7; round++)
        {
            IEnumerable<string> findings = Enumerable.Range(0, 8 - round).Select(Blocking);

            workspace.WriteRound(
                ArtifactKind.Verify,
                round,
                string.Join(",", round == 1 ? findings : findings.Append(Answered(8 - round))));
        }

        Result<RemediationState> state = await RemediationBudget.Read(
            workspace.Cycle,
            CancellationToken.None);

        Assert.IsTrue(state.IsSuccess);
        Assert.AreEqual(6, state.Value.Spent);
        Assert.IsTrue(state.Value.Blocked, "One finding is still open.");
        Assert.IsFalse(state.Value.Stalled);
        Assert.IsFalse(state.Value.Regressed);
        Assert.IsFalse(
            state.Value.Escalated,
            "A loop still reducing what is open has not failed, however long it takes.");
    }

    /// <summary>One open blocking finding, numbered, for a round that holds several.</summary>
    /// <param name="index">Which one.</param>
    /// <returns>The finding as JSON, without surrounding brackets.</returns>
    private static string Blocking(int index) => string.Create(
        CultureInfo.InvariantCulture,
        $$"""
          {
            "id": "F_{{index:D2}}",
            "severity": "blocking",
            "disposition": "open",
            "cites": { "id": "CON_08", "heading": "a verify claim rests on evidence that exists and supports it" },
            "statement": "something that must be answered."
          }
          """);

    /// <summary>The same finding as <see cref="Blocking"/>, answered.</summary>
    /// <param name="index">Which one.</param>
    /// <returns>The finding as JSON, without surrounding brackets.</returns>
    private static string Answered(int index) => Blocking(index).Replace(
        "\"disposition\": \"open\"",
        "\"disposition\": \"answered\"",
        StringComparison.Ordinal);

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

        PullRequest.Response answer = await Ask(workspace);

        Assert.AreEqual(PullRequest.PullRequestRefusal.ChainBroken, answer.Refusal, answer.Explanation);
        StringAssert.Contains(answer.Explanation, "saves a round trip");
    }

    [TestMethod]
    public async Task A_broken_chain_is_refused_with_what_each_failed_check_found()
    {
        // The check's name alone -- "every named test passed" -- sent cycle-02 to a second command to
        // learn which test. The refusal is where the person already is, so the detail belongs in it.
        using CycleWorkspace workspace = Passing();
        workspace.WriteTestSummary(proofs: 1, failing: 1);

        PullRequest.Response answer = await Ask(workspace);

        Assert.AreEqual(PullRequest.PullRequestRefusal.ChainBroken, answer.Refusal, answer.Explanation);
        StringAssert.Contains(answer.Explanation, "every named test passed");
        StringAssert.Contains(answer.Explanation, "proof 1: Thing_does_the_thing_1 did not run and pass.");
    }

    [TestMethod]
    public async Task The_sweep_lists_every_cycle_and_not_only_the_ones_it_would_open_one_for()
    {
        // A list of only the eligible ones hides the surprise that matters most: a cycle from last week
        // still sitting there verified, which the sweep picks up because every passed cycle is eligible.
        using CycleWorkspace passing = Passing();

        PullRequest.Response ready = await Ask(passing);
        PullRequest.Response skipped = new(passing.Cycle, PullRequest.PullRequestRefusal.Escalated, "spent its budget", null);

        string rendered = PullRequestRenderer.RenderSweep([ready, skipped]);

        StringAssert.Contains(rendered, "1 ready of 2 examined");
        StringAssert.Contains(rendered, "pr    ");
        StringAssert.Contains(rendered, "skip  ");
        StringAssert.Contains(rendered, "from last week");
    }

    [TestMethod]
    public void A_sweep_with_nothing_ready_says_so_rather_than_printing_a_warning()
    {
        using var workspace = CycleWorkspace.Create();

        string rendered = PullRequestRenderer.RenderSweep(
            [new PullRequest.Response(workspace.Cycle, PullRequest.PullRequestRefusal.NotVerified, "not verified", null)]);

        StringAssert.Contains(rendered, "Nothing ready for a pull request.");
        Assert.IsFalse(
            rendered.Contains("Each of these opens a pull request", StringComparison.Ordinal),
            "A confirmation about pushing nothing is a confirmation that teaches people to click through.");
    }

    [TestMethod]
    public void Every_sweep_line_is_ascii()
    {
        using var workspace = CycleWorkspace.Create();

        string rendered = PullRequestRenderer.RenderSweep(
            [new PullRequest.Response(workspace.Cycle, PullRequest.PullRequestRefusal.None, "fine", null)]);

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
        workspace.WriteCoverage();

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        return workspace;
    }

    private static async Task<PullRequest.Response> Ask(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        Result<PullRequest.Response> result = await gate
            .Send<PullRequest.Query, PullRequest.Response>(new PullRequest.Query(workspace.Cycle.Id));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
