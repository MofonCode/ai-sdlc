using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Hooks;

namespace Sdlc.Tests;

/// <summary>
/// The turn-end gate, one test per rule.
/// </summary>
/// <remarks>
/// Most of these assert that it says nothing. That is the point worth testing: the gate's value is
/// entirely in the one case it catches, and a governance hook that fired on a person finishing a
/// thought would be switched off within a day — at which point the case it catches goes uncaught too.
/// </remarks>
[TestClass]
public sealed class TurnEndTests
{
    private const string CycleBranch = "cycle/20260929_1456_32";

    [TestMethod]
    public async Task A_turn_already_continuing_is_let_go_even_over_a_red_suite()
    {
        // The loop guard, and it is first for a reason: the host sets this flag when it is already
        // continuing because the gate sent it back, so a second refusal is a turn with no end.
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);
        solution.WriteTestSummary(proofs: 1, failing: 1);

        HookVerdict verdict = await Decide(solution, alreadyContinuing: true);

        Assert.AreEqual(HookDecision.Allow, verdict.Decision);
    }

    [TestMethod]
    public async Task A_turn_off_a_cycle_branch_is_let_go()
    {
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);
        solution.WriteTestSummary(proofs: 1, failing: 1);

        HookVerdict verdict = await Decide(solution, branch: "master");

        Assert.AreEqual(
            HookDecision.Allow,
            verdict.Decision,
            "A spike, a document or a configuration change is not governed work.");
    }

    [TestMethod]
    [DataRow("cycle/not-an-id")]
    [DataRow("cycles/20260929_1456_32")]
    [DataRow("feature/cycle/20260929_1456_32")]
    public async Task A_branch_that_only_looks_like_a_cycle_branch_is_let_go(string branch)
    {
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);
        solution.WriteTestSummary(proofs: 1, failing: 1);

        HookVerdict verdict = await Decide(solution, branch: branch);

        Assert.AreEqual(HookDecision.Allow, verdict.Decision);
    }

    [TestMethod]
    public async Task A_cycle_branch_with_no_cycle_directory_is_let_go()
    {
        // Whether a branch without a cycle is a problem is what 'sdlc gate' answers. Answering it
        // here as well would be two rules about one thing, and they would disagree eventually.
        using var solution = CycleWorkspace.Create();
        Directory.Delete(solution.Cycle.Directory, recursive: true);

        HookVerdict verdict = await Decide(solution);

        Assert.AreEqual(HookDecision.Allow, verdict.Decision);
    }

    [TestMethod]
    public async Task An_abandoned_cycle_is_let_go()
    {
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);
        solution.WriteTestSummary(proofs: 1, failing: 1);
        solution.Abandon();

        HookVerdict verdict = await Decide(solution);

        Assert.AreEqual(
            HookDecision.Allow,
            verdict.Decision,
            "A cycle stopped deliberately has nothing left to prove.");
    }

    [TestMethod]
    public async Task A_cycle_that_has_not_reached_a_plan_is_let_go()
    {
        // The artifact loop runs uncapped rounds and every one of them ends a turn. A gate demanding
        // evidence before a plan exists would make interrogation impossible to run.
        using var solution = CycleWorkspace.Create();
        solution.WriteArtifact(ArtifactKind.Intent);
        solution.WriteSpec(criteria: 1);

        HookVerdict verdict = await Decide(solution);

        Assert.AreEqual(HookDecision.Allow, verdict.Decision);
    }

    [TestMethod]
    public async Task A_failing_suite_sends_the_turn_back()
    {
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 3);
        solution.WriteTestSummary(proofs: 3, failing: 2);

        HookVerdict verdict = await Decide(solution);

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(verdict.Reason, "1 of 3 tests failed");
        StringAssert.Contains(
            verdict.Reason,
            "adjusting a test to make it pass",
            "The refusal has to name the inversion, because rewording a test is the easier fix.");
    }

    [TestMethod]
    public async Task Changes_with_no_recorded_evidence_send_the_turn_back()
    {
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);

        HookVerdict verdict = await Decide(solution);

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(verdict.Reason, "no recorded evidence");
    }

    [TestMethod]
    public async Task A_file_written_and_never_staged_still_sends_the_turn_back()
    {
        // The patch is empty because 'git diff' cannot see an untracked file, and writing new files
        // is most of what building a feature is. Asking the patch instead of the changed-file list
        // is what let this through on the first run against a real repository.
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);

        HookVerdict verdict = await Decide(
            solution,
            patch: string.Empty,
            changed: ["src/Thing.cs"]);

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(verdict.Reason, "no recorded evidence");
    }

    [TestMethod]
    public async Task A_change_confined_to_the_cycles_own_files_is_let_go()
    {
        // A cycle's artifacts live in the repository and are untracked until somebody commits them,
        // so writing an intent makes the change list non-empty. The first run against a real
        // repository demanded build evidence from a planning turn for exactly this reason.
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);

        HookVerdict verdict = await Decide(
            solution,
            patch: string.Empty,
            changed: [".sdlc/cycles/20260929_1456_32/plan.md", ".sdlc/techstack.md"]);

        Assert.AreEqual(HookDecision.Allow, verdict.Decision);
    }

    [TestMethod]
    public async Task A_verify_document_written_after_the_evidence_does_not_send_the_turn_back()
    {
        // The verify stage writes verify.md after the build recorded its evidence, which moves the
        // raw patch. Refusing there would fire in the ordinary flow of every cycle, and a rule that
        // fires during normal work is one somebody switches off -- taking the real case with it.
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);
        solution.WriteTestSummary(proofs: 1);
        WriteRecordedDiff(solution, "diff --git a/src/Thing.cs b/src/Thing.cs\n+code\n");

        HookVerdict verdict = await Decide(
            solution,
            patch: "diff --git a/src/Thing.cs b/src/Thing.cs\n+code\n"
                + "diff --git a/.sdlc/cycles/20260929_1456_32/verify.md b/.sdlc/cycles/20260929_1456_32/verify.md\n+pass\n");

        Assert.AreEqual(HookDecision.Allow, verdict.Decision);
    }

    [TestMethod]
    public async Task Code_moving_is_still_caught_when_the_cycles_own_files_moved_too()
    {
        // The filter must not be a way through. A patch carrying both a governance change and a code
        // change still has to compare its code half.
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);
        solution.WriteTestSummary(proofs: 1);
        WriteRecordedDiff(solution, "diff --git a/src/Thing.cs b/src/Thing.cs\n+code\n");

        HookVerdict verdict = await Decide(
            solution,
            patch: "diff --git a/src/Thing.cs b/src/Thing.cs\n+code\n+more code\n"
                + "diff --git a/.sdlc/cycles/20260929_1456_32/verify.md b/.sdlc/cycles/20260929_1456_32/verify.md\n+pass\n");

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(verdict.Reason, "has moved since the evidence");
    }

    [TestMethod]
    public async Task No_changes_and_no_evidence_is_let_go()
    {
        // Nothing was built, so there is nothing to record. This is the turn that reads a plan and
        // asks a question, and it must not be refused.
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);

        HookVerdict verdict = await Decide(solution, patch: string.Empty, changed: []);

        Assert.AreEqual(HookDecision.Allow, verdict.Decision);
    }

    [TestMethod]
    public async Task Evidence_that_matches_the_working_tree_is_let_go()
    {
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);
        solution.WriteTestSummary(proofs: 1);
        WriteRecordedDiff(solution, "diff --git a/src/Thing.cs b/src/Thing.cs\n");

        HookVerdict verdict = await Decide(
            solution,
            patch: "diff --git a/src/Thing.cs b/src/Thing.cs\n");

        Assert.AreEqual(HookDecision.Allow, verdict.Decision);
    }

    [TestMethod]
    public async Task A_tree_that_moved_since_the_evidence_sends_the_turn_back()
    {
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);
        solution.WriteTestSummary(proofs: 1);
        WriteRecordedDiff(solution, "diff --git a/src/Thing.cs b/src/Thing.cs\n");

        HookVerdict verdict = await Decide(
            solution,
            patch: "diff --git a/src/Thing.cs b/src/Thing.cs\ndiff --git a/src/Other.cs b/src/Other.cs\n");

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(verdict.Reason, "has moved since the evidence");
    }

    [TestMethod]
    public async Task A_difference_of_line_endings_alone_does_not_send_the_turn_back()
    {
        // The recorded patch went through a file write and git's output did not. Comparing them raw
        // would refuse every turn on Windows and no turn anywhere else, which is the shape of rule
        // that gets switched off rather than fixed.
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);
        solution.WriteTestSummary(proofs: 1);
        WriteRecordedDiff(solution, "diff --git a/src/Thing.cs b/src/Thing.cs\r\n");

        HookVerdict verdict = await Decide(
            solution,
            patch: "diff --git a/src/Thing.cs b/src/Thing.cs\n");

        Assert.AreEqual(HookDecision.Allow, verdict.Decision);
    }

    [TestMethod]
    public async Task An_unreadable_summary_is_not_read_as_a_red_suite()
    {
        // Saying a malformed file means the tests failed would be a claim about the tests that the
        // file does not support. The diff rules still catch the case by another road.
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);
        Directory.CreateDirectory(solution.Cycle.EvidenceDirectory);
        CycleWorkspace.WriteRaw(
            Path.Combine(solution.Cycle.EvidenceDirectory, RecordEvidence.SummaryFileName),
            "not json at all");
        WriteRecordedDiff(solution, "diff --git a/src/Thing.cs b/src/Thing.cs\n");

        HookVerdict verdict = await Decide(
            solution,
            patch: "diff --git a/src/Thing.cs b/src/Thing.cs\n");

        Assert.AreEqual(HookDecision.Allow, verdict.Decision);
    }

    [TestMethod]
    public async Task Every_refusal_names_the_command_that_answers_it()
    {
        // CON_10.2 holds the framework's own gate to this, and a hook is read in worse conditions
        // than a gate verdict: it arrives in the middle of somebody else's work.
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);
        solution.WriteTestSummary(proofs: 1, failing: 1);

        HookVerdict verdict = await Decide(solution);

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(verdict.Reason, "sdlc evidence 20260929_1456_32");
        StringAssert.Contains(verdict.Reason, "sdlc status 20260929_1456_32");
    }

    [TestMethod]
    public async Task Git_that_cannot_say_where_the_branch_diverged_lets_the_turn_go()
    {
        // There is nothing to compare against, and the build stage reports this properly when it
        // runs. A refusal here would be one nobody can act on from where they are standing.
        using var solution = CycleWorkspace.Create();
        solution.WritePlan(proofs: 1);

        using var gate = Gate.For(
            solution.Root,
            services => services.AddSingleton<IRepository>(
                new FakeRepository { Branch = CycleBranch, Base = null }));

        Result<HookVerdict> result = await gate.Send<TurnEndGate.Request, HookVerdict>(
            new TurnEndGate.Request(new TurnEnd(solution.Root, AlreadyContinuing: false)));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(HookDecision.Allow, result.Value.Decision);
    }

    private static void WriteRecordedDiff(CycleWorkspace solution, string patch)
    {
        Directory.CreateDirectory(solution.Cycle.EvidenceDirectory);

        CycleWorkspace.WriteRaw(
            Path.Combine(solution.Cycle.EvidenceDirectory, RecordEvidence.DiffFileName),
            patch);
    }

    private static async Task<HookVerdict> Decide(
        CycleWorkspace solution,
        string branch = CycleBranch,
        bool alreadyContinuing = false,
        string? patch = null,
        IReadOnlyList<string>? changed = null)
    {
        using var gate = Gate.For(
            solution.Root,
            services => services.AddSingleton<IRepository>(new FakeRepository
            {
                Branch = branch,
                Patch = patch ?? "diff --git a/src/Thing.cs b/src/Thing.cs\n",
                Changed = changed ?? ["src/Thing.cs"],
            }));

        Result<HookVerdict> result = await gate.Send<TurnEndGate.Request, HookVerdict>(
            new TurnEndGate.Request(new TurnEnd(solution.Root, alreadyContinuing)));

        Assert.IsTrue(
            result.IsSuccess,
            "The gate itself failed: " + string.Join("; ", result.Errors.Select(error => error.Message)));

        return result.Value;
    }
}
