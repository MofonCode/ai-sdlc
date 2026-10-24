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

        VerifyChain.Response verdict = await Check(workspace);

        Assert.IsTrue(verdict.Holds, Describe(verdict));
    }

    [TestMethod]
    public async Task A_cycle_with_no_directory_says_there_is_no_chain_rather_than_listing_checks()
    {
        using var workspace = CycleWorkspace.Create();
        Assert.IsTrue(CycleId.TryParse("20991231_2359_59", out CycleId? absent));

        using var gate = Gate.For(workspace.Root);

        Result<VerifyChain.Response> result = await gate
            .Send<VerifyChain.Query, VerifyChain.Response>(new VerifyChain.Query(absent.Value));

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

        VerifyChain.Response verdict = await Check(workspace);

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

        VerifyChain.Response verdict = await Check(workspace);

        VerifyChain.ChainCheck proof = verdict.Checks.Single(check => check.Name.Contains("proof", StringComparison.Ordinal));

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

        VerifyChain.Response verdict = await Check(workspace);

        VerifyChain.ChainCheck proof = verdict.Checks.Single(check => check.Name.Contains("proof", StringComparison.Ordinal));

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

        VerifyChain.Response verdict = await Check(workspace);

        VerifyChain.ChainCheck proof = verdict.Checks.Single(check => check.Name.Contains("proof", StringComparison.Ordinal));

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

        VerifyChain.Response verdict = await Check(workspace);

        VerifyChain.ChainCheck verified = verdict.Checks.Single(check => check.Name.Contains("verified", StringComparison.Ordinal));

        Assert.IsFalse(verified.Passed);
        StringAssert.Contains(verified.Detail, outcome);
    }

    [TestMethod]
    [DataRow("\"pass\"", DisplayName = "quoted")]
    [DataRow("PASS", DisplayName = "upper case")]
    public async Task A_pass_the_schema_accepts_is_a_pass_to_the_chain(string outcome)
    {
        // A quoted scalar is valid YAML and ArtifactSchema accepted it, while the chain check compared
        // the raw text after `outcome:` and saw the quotes. So a legitimately passing cycle could not
        // merge, and `pr-ready` read the outcome through the same check and agreed with the wrong
        // answer. Both now go through Frontmatter.Key. F-06. The quoted row holds that the chain still
        // asks that reader, whose own cases -- quotes, padding, comments, block scalars -- are
        // FrontmatterTests'; the upper-case one is the chain's comparison, not the reader's.
        using CycleWorkspace workspace = Sound();
        workspace.WriteVerify(outcome);

        VerifyChain.Response verdict = await Check(workspace);

        VerifyChain.ChainCheck verified = verdict.Checks
            .Single(check => check.Name.Contains("verified", StringComparison.Ordinal));

        Assert.IsTrue(verified.Passed, verified.Detail);
    }

    [TestMethod]
    public async Task A_missing_verify_document_fails_the_chain_without_also_failing_the_outcome()
    {
        // One problem reported once. A missing document that also failed the outcome check would read as
        // two defects and send somebody looking for the second.
        using CycleWorkspace workspace = Sound();
        File.Delete(workspace.Cycle.PathOf(ArtifactKind.Verify));

        VerifyChain.Response verdict = await Check(workspace);

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

        VerifyChain.Response verdict = await Check(workspace);

        VerifyChain.ChainCheck gate = verdict.Checks.Single(check => check.Name.Contains("gate", StringComparison.Ordinal));

        Assert.IsFalse(gate.Passed);
        StringAssert.Contains(gate.Detail, "StaleRound");
    }

    [TestMethod]
    public async Task A_criterion_the_verify_does_not_adjudicate_breaks_the_chain()
    {
        // The exit D-03 names, pointed at the one artifact CON_14 does not cover. A criterion whose
        // proof did not reach is recorded under `## Open` as a limit of the proof rather than
        // adjudicated as unmet, and before this check every other one still passed: the plan has its
        // proof entry, the hashes match, the rounds are current and the outcome says pass. CON_08 does
        // not reach it either -- it governs claims in the criteria section, not absences recorded
        // elsewhere -- and verify is the last judgement before the pull request, so nothing downstream catches
        // what D-27 assumes something will.
        using CycleWorkspace workspace = Sound();

        workspace.WriteVerifyAdjudicating("pass", 1, 2);

        VerifyChain.Response response = await Check(workspace);

        VerifyChain.ChainCheck adjudicated = response.Checks.Single(check => check.Name == "every criterion is adjudicated");

        Assert.IsFalse(adjudicated.Passed, adjudicated.Detail);
        StringAssert.Contains(adjudicated.Detail, "3", "The refusal has to name which criterion.");
        StringAssert.Contains(
            adjudicated.Detail,
            "not moved to '## Open'",
            "And say what the author should have done instead, or it reads as a format complaint.");
    }

    [TestMethod]
    public async Task A_verify_that_adjudicates_every_criterion_holds()
    {
        // The other half, and the one that matters for whether this check is safe to put in the
        // non-bypassable layer: a verify doing its job must not be refused by it.
        using CycleWorkspace workspace = Sound();

        VerifyChain.Response response = await Check(workspace);

        VerifyChain.ChainCheck adjudicated = response.Checks.Single(check => check.Name == "every criterion is adjudicated");

        Assert.IsTrue(adjudicated.Passed, adjudicated.Detail);
        StringAssert.Contains(adjudicated.Detail, "3 criteria");
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

    [TestMethod]
    public async Task A_deleted_findings_directory_fails_the_chain()
    {
        // The layer the promise rests on. verify-chain delegates the gate to ApprovalGate's handler,
        // so F-04's two holes were holes here too: moving findings/ aside left the chain holding and
        // `pr-ready` saying the cycle was verified. Asserted here as well as at the gate, because this
        // is the check nothing can get past.
        using CycleWorkspace workspace = Sound();

        Assert.IsTrue((await Check(workspace)).Holds, "The chain holds to start with.");

        Directory.Delete(workspace.Cycle.FindingsDirectory, recursive: true);

        VerifyChain.Response verdict = await Check(workspace);

        Assert.IsFalse(verdict.Holds, Describe(verdict));
    }

    [TestMethod]
    public async Task A_round_file_damaged_to_nothing_fails_the_chain()
    {
        using CycleWorkspace workspace = Sound();
        await File.WriteAllTextAsync(
            workspace.Cycle.FindingsPath(ArtifactKind.Spec, 1),
            "{}",
            TestContext.CancellationToken);

        VerifyChain.Response verdict = await Check(workspace);

        Assert.IsFalse(verdict.Holds, Describe(verdict));
    }

    /// <summary>The test context, for the cancellation token the analysers require to be passed on.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("dependabot/nuget/Serilog-4.1.0")]
    [DataRow("revert-1234-bad-merge")]
    [DataRow("hotfix/prod-down")]
    [DataRow("main")]
    public void A_branch_that_is_not_a_cycle_is_told_so_as_a_verdict(string reference)
    {
        // The pipeline hands github.head_ref in on every pull request, so this command meets branches
        // nobody opened as a cycle. It used to report them as a malformed argument, which on a bot's
        // pull request reads as a misconfigured tool -- and the response to a misconfigured tool is to
        // delete the step, which is the only layer nobody can bypass. D-45, F-28.
        string rendered = ChainRenderer.RenderUngoverned(reference);

        StringAssert.Contains(rendered, reference, "A reader has to see which branch this was about.");
        StringAssert.Contains(rendered, "no chain", "It says what is wrong in governance terms.");
        StringAssert.Contains(rendered, "branch policy", "And names the exemption that is a decision.");
        StringAssert.Contains(
            rendered,
            "not the same as deleting this step",
            "The one response that must not look equivalent is deleting the check.");

        Assert.IsFalse(
            rendered.Contains("is not a cycle id. They are written", StringComparison.Ordinal),
            "That is the usage complaint this replaces.");

        Assert.IsTrue(rendered.All(char.IsAscii), rendered);
    }

    [TestMethod]
    [DataRow("2026-10-01T10:00:00Z", false)]
    [DataRow("2026-10-04T10:00:00Z", true)]
    public async Task After_a_reopen_the_verdict_has_to_follow_the_approval_that_stands(string verifiedAt, bool holds)
    {
        // A reopen leaves the evidence and the verdict in place while the documents change; a verdict
        // judged before the new approval adjudicated documents nobody approves any more.
        using CycleWorkspace workspace = Sound();
        string hashes = string.Join('\n', Cycle.PlanningDocuments.Select(kind =>
            $"      {Cycle.FileNameOf(kind)}: {workspace.HashOf(kind)}"));
        CycleWorkspace.WriteRaw(workspace.Cycle.ApprovalPath, $"""
            ---
            cycle: {workspace.Cycle.Id}
            framework_version: 1.0.0
            decisions:
              - decision: approved
                by: Peter Almanzar
                at: 2026-10-01T09:00:00Z
                hashes:
            {hashes}
              - decision: reopened
                by: Peter Almanzar
                at: 2026-10-02T09:00:00Z
                hashes:
            {hashes}
                reason: the plan draws its own tables.
              - decision: approved
                by: Peter Almanzar
                at: 2026-10-03T09:00:00Z
                hashes:
            {hashes}
            ---

            """);
        CycleWorkspace.WriteRaw(workspace.Cycle.FindingsPath(ArtifactKind.Verify, 1), $$"""
            { "framework_version": "1.0.0", "cycle": "{{workspace.Cycle.Id}}", "artifact": "verify.md",
              "upstream_sha256": "{{workspace.HashOf(ArtifactKind.Plan)}}", "interrogated_at": "{{verifiedAt}}",
              "round": 1, "findings": [] }
            """);

        VerifyChain.Response verdict = await Check(workspace);

        VerifyChain.ChainCheck follows = verdict.Checks.Single(check => check.Name == "the verdict follows the approval that stands");
        Assert.AreEqual(holds, follows.Passed, follows.Detail);
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

        VerifyChain.Response verdict = await Check(workspace);

        VerifyChain.ChainCheck proven = verdict.Checks.Single(check => check.Name.Contains("named test", StringComparison.Ordinal));

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

        VerifyChain.Response verdict = await Check(workspace);

        VerifyChain.ChainCheck proven = verdict.Checks.Single(check => check.Name.Contains("named test", StringComparison.Ordinal));

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

        VerifyChain.Response verdict = await Check(workspace);

        VerifyChain.ChainCheck proven = verdict.Checks.Single(check => check.Name.Contains("named test", StringComparison.Ordinal));

        Assert.IsFalse(proven.Passed, proven.Detail);
        StringAssert.Contains(proven.Detail, "proof 3 names no test");
    }

    [TestMethod]
    public async Task A_plan_alone_is_admitted_without_a_verify()
    {
        // D-87: a plan reaches the integration branch before it is built, so its pull request has no
        // verify to show. Held to everything a plan can be held to, and nothing a build produces.
        using CycleWorkspace workspace = Planned();

        VerifyChain.Response verdict = await Check(workspace, PlanningPaths(workspace));

        Assert.IsTrue(verdict.Admission, Describe(verdict));
        Assert.IsTrue(verdict.Holds, Describe(verdict));
        Assert.IsFalse(
            verdict.Checks.Any(check => check.Name.Contains("verified", StringComparison.Ordinal)),
            "The admission check asks for no verify: " + Describe(verdict));
    }

    [TestMethod]
    public async Task A_plan_admitted_without_an_approval_does_not_hold()
    {
        using CycleWorkspace workspace = Planned();
        File.Delete(workspace.Cycle.ApprovalPath);

        VerifyChain.Response verdict = await Check(workspace, PlanningPaths(workspace));

        Assert.IsTrue(verdict.Admission, Describe(verdict));
        Assert.IsFalse(verdict.Holds, "A plan nobody approved is not admitted: " + Describe(verdict));
    }

    [TestMethod]
    public async Task One_file_outside_the_plan_brings_back_the_full_chain()
    {
        // The diff decides, not the branch name: a plan's pull request carrying one source file is held
        // to the whole chain, and with no verify it fails.
        using CycleWorkspace workspace = Planned();

        VerifyChain.Response verdict = await Check(
            workspace,
            [.. PlanningPaths(workspace), "src/Orders/OrderService.cs"]);

        Assert.IsFalse(verdict.Admission, Describe(verdict));
        Assert.IsFalse(verdict.Holds, Describe(verdict));
    }

    [TestMethod]
    [DataRow("verify.md")]
    [DataRow("evidence/tests.json")]
    [DataRow("abandoned.md")]
    public void A_build_or_a_stop_is_not_planning_paperwork(string inside)
    {
        Assert.IsTrue(CycleId.TryParse("20260929_1456_32", out CycleId? id));

        Assert.IsFalse(VerifyChain.AdmitsAsPlan(id.Value, [".sdlc/cycles/20260929_1456_32/" + inside]));
    }

    [TestMethod]
    public void Another_cycles_plan_and_an_empty_diff_are_not_admitted()
    {
        Assert.IsTrue(CycleId.TryParse("20260929_1456_32", out CycleId? id));

        Assert.IsFalse(VerifyChain.AdmitsAsPlan(id.Value, [".sdlc/cycles/20260929_1456_33/plan.md"]));
        Assert.IsFalse(VerifyChain.AdmitsAsPlan(id.Value, []));
        Assert.IsTrue(VerifyChain.AdmitsAsPlan(
            id.Value,
            [".sdlc\\cycles\\20260929_1456_32\\findings\\plan.r1.json", ".sdlc/cycles/20260929_1456_32/approval.md"]));
    }

    private static CycleWorkspace Planned()
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

    private static string[] PlanningPaths(CycleWorkspace workspace)
    {
        string root = ".sdlc/cycles/" + workspace.Cycle.Id + "/";
        return [root + "intent.md", root + "spec.md", root + "plan.md", root + "approval.md", root + "findings/plan.r1.json"];
    }

    private static async Task<VerifyChain.Response> Check(CycleWorkspace workspace, IReadOnlyList<string> changed)
    {
        using var gate = Gate.For(workspace.Root);

        Result<VerifyChain.Response> result = await gate
            .Send<VerifyChain.Query, VerifyChain.Response>(new VerifyChain.Query(workspace.Cycle.Id, changed));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static async Task<VerifyChain.Response> Check(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        Result<VerifyChain.Response> result = await gate
            .Send<VerifyChain.Query, VerifyChain.Response>(new VerifyChain.Query(workspace.Cycle.Id));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static string Describe(VerifyChain.Response verdict) =>
        string.Join("; ", verdict.Checks.Select(c => (c.Passed ? "ok " : "NO ") + c.Name + ": " + c.Detail));
}
