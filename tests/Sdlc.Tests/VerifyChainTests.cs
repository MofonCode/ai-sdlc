using FluentResults;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;

namespace Sdlc.Tests;

/// <summary>
/// The non-bypassable layer: what a pull-request check re-establishes from the repository.
/// </summary>
/// <remarks>
/// This is the only one of the three enforcement layers that is a guarantee, so the tests that matter
/// are the ones asserting it cannot be talked round -- and the one asserting it does not claim more than
/// it proves, because a check that overstated itself would stop a team looking for what it was not
/// providing.
/// </remarks>
[TestClass]
public sealed class VerifyChainTests
{
    [TestMethod]
    public async Task A_complete_approved_cycle_holds()
    {
        using CycleWorkspace workspace = Sound();

        ChainVerdict verdict = await Check(workspace);

        Assert.IsTrue(verdict.Holds, Describe(verdict));
    }

    [TestMethod]
    public async Task A_cycle_with_no_directory_says_there_is_no_chain_rather_than_listing_checks()
    {
        using var workspace = CycleWorkspace.Create();
        Assert.IsTrue(CycleId.TryParse("20991231_2359_59", out CycleId? absent));

        using var gate = Gate.For(workspace.Root);

        Result<ChainVerdict> result = await gate
            .Send<VerifyChain.Request, ChainVerdict>(new VerifyChain.Request(absent.Value));

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value.Holds);
        Assert.AreEqual(
            1,
            result.Value.Checks.Count,
            "Five failing checks about a cycle that does not exist is five times the noise of one.");
    }

    [TestMethod]
    public async Task Every_failing_check_is_reported_rather_than_the_first()
    {
        // A pull request sent back once with three problems costs less than three round trips, and the
        // checks are independent of each other.
        using CycleWorkspace workspace = Sound();
        File.Delete(workspace.Cycle.ApprovalPath);
        workspace.WriteVerify(outcome: "blocked");

        ChainVerdict verdict = await Check(workspace);

        Assert.IsTrue(verdict.Failures.Count >= 2, Describe(verdict));
        Assert.IsTrue(
            verdict.Failures.Any(check => check.Name.Contains("gate", StringComparison.Ordinal)),
            Describe(verdict));
        Assert.IsTrue(
            verdict.Failures.Any(check => check.Name.Contains("verified", StringComparison.Ordinal)),
            Describe(verdict));
    }

    [TestMethod]
    public async Task A_criterion_added_after_the_plan_has_no_proof_and_fails()
    {
        using CycleWorkspace workspace = Sound();
        workspace.WriteSpec(criteria: 4);

        ChainVerdict verdict = await Check(workspace);

        ChainCheck proof = verdict.Checks.Single(check => check.Name.Contains("proof", StringComparison.Ordinal));

        Assert.IsFalse(proof.Passed, proof.Detail);
        StringAssert.Contains(proof.Detail, "criteria with no proof: 4");
    }

    [TestMethod]
    public async Task A_proof_citing_a_criterion_the_spec_no_longer_has_fails()
    {
        // The other direction, and the one easier to miss: a criterion deleted or renumbered after the
        // plan was written leaves a proof that looks complete and is proving something else.
        using CycleWorkspace workspace = Sound();
        workspace.WriteSpec(criteria: 2);

        ChainVerdict verdict = await Check(workspace);

        ChainCheck proof = verdict.Checks.Single(check => check.Name.Contains("proof", StringComparison.Ordinal));

        Assert.IsFalse(proof.Passed, proof.Detail);
        StringAssert.Contains(proof.Detail, "the spec does not have: 3");
    }

    [TestMethod]
    public async Task A_spec_with_no_numbered_criteria_fails_rather_than_passing_vacuously()
    {
        // Comparing two empty sets succeeds, which would make a spec asserting nothing the easiest spec
        // to get through the chain.
        using CycleWorkspace workspace = Sound();
        workspace.WriteSpec(criteria: 0);

        ChainVerdict verdict = await Check(workspace);

        ChainCheck proof = verdict.Checks.Single(check => check.Name.Contains("proof", StringComparison.Ordinal));

        Assert.IsFalse(proof.Passed, proof.Detail);
        StringAssert.Contains(proof.Detail, "cannot be met");
    }

    [TestMethod]
    [DataRow("blocked")]
    [DataRow("escalated")]
    public async Task Only_a_pass_merges(string outcome)
    {
        using CycleWorkspace workspace = Sound();
        workspace.WriteVerify(outcome);

        ChainVerdict verdict = await Check(workspace);

        ChainCheck verified = verdict.Checks.Single(check => check.Name.Contains("verified", StringComparison.Ordinal));

        Assert.IsFalse(verified.Passed);
        StringAssert.Contains(verified.Detail, outcome);
    }

    [TestMethod]
    public async Task A_missing_verify_document_fails_the_chain_without_also_failing_the_outcome()
    {
        // One problem reported once. A missing document that also failed the outcome check would read as
        // two defects and send somebody looking for the second.
        using CycleWorkspace workspace = Sound();
        File.Delete(workspace.Cycle.PathOf(ArtifactKind.Verify));

        ChainVerdict verdict = await Check(workspace);

        Assert.IsFalse(verdict.Holds);
        Assert.IsFalse(
            verdict.Checks.Any(check => check.Name.Contains("verified", StringComparison.Ordinal)),
            Describe(verdict));
    }

    [TestMethod]
    public async Task A_stale_round_fails_the_chain_even_though_every_hash_matches()
    {
        // The case the three approval hashes cannot catch: they prove three documents were approved, not
        // that the spec was derived from this intent.
        using CycleWorkspace workspace = Sound();
        workspace.WriteArtifact(ArtifactKind.Intent, "a different problem entirely");
        workspace.WriteApproval();

        ChainVerdict verdict = await Check(workspace);

        ChainCheck gate = verdict.Checks.Single(check => check.Name.Contains("gate", StringComparison.Ordinal));

        Assert.IsFalse(gate.Passed);
        StringAssert.Contains(gate.Detail, "StaleRound");
    }

    [TestMethod]
    public async Task The_chain_says_what_it_cannot_prove_even_when_it_holds()
    {
        // Stated on a pass rather than only on a failure: a team reads this at the moment they are
        // deciding how much the green tick was worth.
        using CycleWorkspace workspace = Sound();

        string rendered = ChainRenderer.Render(await Check(workspace));

        StringAssert.Contains(rendered, "The chain holds.");
        StringAssert.Contains(rendered, "cannot prove a second person approved");
        StringAssert.Contains(rendered, "Code-owner review");
    }

    [TestMethod]
    public async Task The_rendered_report_is_ascii_and_names_every_failure_at_the_end()
    {
        // A pipeline log is read in a web view, truncated from the top, and not reliably UTF-8.
        using CycleWorkspace workspace = Sound();
        workspace.WriteVerify(outcome: "blocked");

        string rendered = ChainRenderer.Render(await Check(workspace));

        Assert.IsTrue(rendered.All(char.IsAscii), rendered);
        StringAssert.Contains(rendered, "The chain does not hold.");
        Assert.IsTrue(
            rendered.LastIndexOf("the change was verified", StringComparison.Ordinal)
            > rendered.IndexOf("the change was verified", StringComparison.Ordinal),
            "A failure named only at the top is lost in a truncated log.");
    }

    private static CycleWorkspace Sound()
    {
        var workspace = CycleWorkspace.Create();

        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteSpec(criteria: 3);
        workspace.WritePlan(proofs: 3);
        workspace.WriteVerify(outcome: "pass");
        workspace.WriteTestSummary(proofs: 3);

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        return workspace;
    }

    [TestMethod]
    public async Task A_proof_whose_named_test_failed_fails_the_chain()
    {
        // This is the check that blocks. A proof entry existing is bookkeeping; the criterion is met when
        // the test it names passed, read from the evidence rather than from anything verify claims.
        using CycleWorkspace workspace = Sound();
        workspace.WriteTestSummary(proofs: 3, failing: 2);

        ChainVerdict verdict = await Check(workspace);

        ChainCheck proven = verdict.Checks.Single(check => check.Name.Contains("named test", StringComparison.Ordinal));

        Assert.IsFalse(proven.Passed, proven.Detail);
        StringAssert.Contains(proven.Detail, "proof 2");
    }

    [TestMethod]
    public async Task A_chain_with_no_test_summary_at_all_fails_rather_than_passing()
    {
        // A proof nothing ran is not a proof, and a chain that held without one would mean "approved"
        // rather than "met".
        using CycleWorkspace workspace = Sound();
        File.Delete(Path.Combine(workspace.Cycle.EvidenceDirectory, "tests.json"));

        ChainVerdict verdict = await Check(workspace);

        ChainCheck proven = verdict.Checks.Single(check => check.Name.Contains("named test", StringComparison.Ordinal));

        Assert.IsFalse(proven.Passed);
        StringAssert.Contains(proven.Detail, "rather than 'met'");
    }

    [TestMethod]
    public async Task A_proof_that_names_no_test_is_reported_rather_than_skipped()
    {
        // "Covered by the integration suite" commits to nothing a reader can check, which is the shape an
        // inadequate proof takes when it is written in good faith.
        using CycleWorkspace workspace = Sound();

        string plan = workspace.Cycle.PathOf(ArtifactKind.Plan);
        string text = await File.ReadAllTextAsync(plan);

        CycleWorkspace.WriteRaw(
            plan,
            text.Replace(
                "3. `Thing_does_the_thing_3` asserts it.",
                "3. Covered by the integration suite.",
                StringComparison.Ordinal));

        ChainVerdict verdict = await Check(workspace);

        ChainCheck proven = verdict.Checks.Single(check => check.Name.Contains("named test", StringComparison.Ordinal));

        Assert.IsFalse(proven.Passed, proven.Detail);
        StringAssert.Contains(proven.Detail, "proof 3 names no test");
    }

    private static async Task<ChainVerdict> Check(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ChainVerdict> result = await gate
            .Send<VerifyChain.Request, ChainVerdict>(new VerifyChain.Request(workspace.Cycle.Id));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static string Describe(ChainVerdict verdict) =>
        string.Join("; ", verdict.Checks.Select(c => (c.Passed ? "ok " : "NO ") + c.Name + ": " + c.Detail));
}
