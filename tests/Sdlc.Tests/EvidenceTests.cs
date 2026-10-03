using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// What a build records, and what the declared-file comparison makes of it.
/// </summary>
/// <remarks>
/// Two things are being defended here. That the gate never learns what a test runner's format looks like --
/// normalising happens at this boundary and nowhere else. And that an unknown outcome never reads as a pass,
/// because a criterion proven by a test that errored is worse than one with no proof at all.
/// </remarks>
[TestClass]
public sealed class EvidenceTests
{
    private const string Trx = """
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testName="Dice.Tests.RollTests.Rolls_within_one_and_six" outcome="Passed" />
            <UnitTestResult testName="Dice.Tests.RollTests.Two_requests_differ" outcome="Failed">
              <Output><ErrorInfo><Message>Both draws returned 4.</Message></ErrorInfo></Output>
            </UnitTestResult>
            <UnitTestResult testName="Dice.Tests.RollTests.The_route_answers" outcome="NotExecuted" />
          </Results>
        </TestRun>
        """;

    [TestMethod]
    public void A_runner_result_file_normalises_to_names_and_outcomes()
    {
        Result<IReadOnlyList<RecordedTest>> read = TestSummaryReader.ReadTrx(Trx, "results.trx");

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));
        Assert.AreEqual(3, read.Value.Count);
        Assert.AreEqual(TestOutcome.Passed, read.Value[0].Outcome);
        Assert.AreEqual(TestOutcome.Failed, read.Value[1].Outcome);
        StringAssert.Contains(read.Value[1].Detail!, "Both draws returned 4.");
    }

    [TestMethod]
    [DataRow("NotExecuted", TestOutcome.Skipped)]
    [DataRow("Inconclusive", TestOutcome.Skipped)]
    [DataRow("Pending", TestOutcome.Skipped)]
    [DataRow("something nobody mapped", TestOutcome.Skipped)]
    [DataRow("Error", TestOutcome.Failed)]
    [DataRow("Timeout", TestOutcome.Failed)]
    public void An_outcome_word_nobody_mapped_never_reads_as_a_pass(string word, TestOutcome expected)
    {
        // A runner word nobody translated is a test whose state is unknown, and treating unknown as passed
        // is how a criterion gets proven by a test that errored.
        string xml = $"""
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results><UnitTestResult testName="T" outcome="{word}" /></Results>
            </TestRun>
            """;

        Result<IReadOnlyList<RecordedTest>> read = TestSummaryReader.ReadTrx(xml, "results.trx");

        Assert.IsTrue(read.IsSuccess);
        Assert.AreEqual(expected, read.Value.Single().Outcome);
    }

    [TestMethod]
    public void A_results_file_with_no_results_is_rejected_rather_than_read_as_a_clean_run()
    {
        // A run that produced none is a run that did not happen, which is not the same as a run with
        // nothing to report. The trial lost a whole cycle to exactly this: dotnet test exited zero without
        // running the suite.
        Result<IReadOnlyList<RecordedTest>> read = TestSummaryReader.ReadTrx(
            """<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results /></TestRun>""",
            "results.trx");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "did not happen");
    }

    [TestMethod]
    public void A_summary_round_trips_through_the_framework_format()
    {
        // Everything downstream reads this rather than the runner's own output, so the two have to agree
        // after a write and a read.
        TestSummary written = new(
            Cycle.FrameworkVersion,
            "20260929_1456_32",
            DateTimeOffset.Parse("2026-10-02T09:00:00Z", null),
            [new RecordedTest("A_test", TestOutcome.Passed), new RecordedTest("B_test", TestOutcome.Failed, "why")]);

        Result<TestSummary> read = TestSummaryReader.Read(
            TestSummaryReader.Write(written),
            "tests.json");

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));
        Assert.AreEqual(1, read.Value.Passed);
        Assert.AreEqual(1, read.Value.Failed);
        Assert.AreEqual("20260929_1456_32", read.Value.Cycle);
    }

    [TestMethod]
    public void A_plan_names_a_test_without_its_namespace_and_the_summary_still_finds_it()
    {
        // A plan names a test at planning time, before the namespace exists. Requiring the fully qualified
        // name would make every plan carry something it has no way to know.
        TestSummary summary = new(
            null,
            null,
            null,
            [new RecordedTest("Dice.Tests.RollTests.Rolls_within_one_and_six", TestOutcome.Passed)]);

        Assert.IsTrue(summary.DidPass("Rolls_within_one_and_six"));
        Assert.IsTrue(summary.DidPass("Dice.Tests.RollTests.Rolls_within_one_and_six"));
        Assert.IsFalse(summary.DidPass("Rolls_within_one_and_seven"));
    }

    [TestMethod]
    public void A_failing_test_does_not_count_as_passing_under_any_spelling()
    {
        TestSummary summary = new(null, null, null, [new RecordedTest("A.B.C_test", TestOutcome.Failed)]);

        Assert.IsFalse(summary.DidPass("C_test"));
        Assert.IsFalse(summary.DidPass("A.B.C_test"));
    }

    [TestMethod]
    public void The_declared_files_are_read_from_backticks_in_the_files_section()
    {
        IReadOnlyList<string> declared = DeclaredFiles.Read("""
            ---
            cycle: 20260929_1456_32
            ---

            # a plan

            ## Shape and departures

            This mentions `src/NotDeclared.cs` outside the files section.

            ## Files

            | File | Change |
            |---|---|
            | `src/Dice/RollService.cs` | new |
            | `src/Dice/Pages/Index.razor` | reads it |

            ## Open
            """);

        CollectionAssert.AreEqual(
            new[] { "src/Dice/Pages/Index.razor", "src/Dice/RollService.cs" },
            declared.ToArray(),
            "A path named in another section is not declared: only the Files section declares.");
    }

    [TestMethod]
    public void A_plan_with_no_files_section_declares_nothing_rather_than_failing()
    {
        Assert.AreEqual(0, DeclaredFiles.Read("# a plan with no sections at all").Count);
    }

    [TestMethod]
    public void The_comparison_names_both_directions()
    {
        FileComparison comparison = DeclaredFiles.Compare(
            ["src/A.cs", "src/B.cs"],
            ["src/A.cs", "src/C.cs"]);

        CollectionAssert.AreEqual(new[] { "src/B.cs" }, comparison.Undelivered.ToArray());
        CollectionAssert.AreEqual(new[] { "src/C.cs" }, comparison.Undeclared.ToArray());
        Assert.IsFalse(comparison.Agrees);
    }

    [TestMethod]
    public void A_cycle_s_own_artifacts_are_not_part_of_the_change_it_describes()
    {
        // Without this every cycle reports its own intent as undeclared, which is noise that teaches people
        // to ignore the line. A leading-dot bug once defeated the filter and did exactly that.
        FileComparison comparison = DeclaredFiles.Compare(
            ["src/A.cs"],
            [
                "src/A.cs",
                ".sdlc/cycles/20260929_1456_32/plan.md",
                ".sdlc/cycles/20260929_1456_32/evidence/diff.patch",
            ]);

        Assert.IsTrue(comparison.Agrees, string.Join("; ", comparison.Undeclared));
    }

    [TestMethod]
    public void A_path_written_either_way_round_compares_equal()
    {
        FileComparison comparison = DeclaredFiles.Compare(
            ["./src/A.cs"],
            ["src\\A.cs"]);

        Assert.IsTrue(comparison.Agrees, string.Join("; ", comparison.Undeclared.Concat(comparison.Undelivered)));
    }

    [TestMethod]
    public async Task Recording_evidence_writes_the_diff_its_base_and_the_normalised_summary()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);

        string trx = Path.Combine(workspace.Root, "results.trx");
        CycleWorkspace.WriteRaw(trx, Trx);

        RecordEvidence.Response report = await Record(workspace, trx, changed: ["src/Thing.cs"]);

        foreach (string expected in (string[])
        [
            RecordEvidence.BaseFileName,
            RecordEvidence.DiffFileName,
            RecordEvidence.SummaryFileName,
            RecordEvidence.TrxFileName,
        ])
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(workspace.Cycle.EvidenceDirectory, expected)),
                expected + " was not written.");
        }

        Assert.IsNotNull(report.Summary);
        Assert.AreEqual(1, report.Summary.Passed);
    }

    [TestMethod]
    public async Task The_runners_own_output_is_kept_beside_the_summary_rather_than_replaced()
    {
        // So a verify claim can cite either, and CON_08 is satisfied by both. Replacing it would make the
        // normalisation lossy in a way nobody could check.
        //
        // Under a fixed name, not the runner's. The runner's output is usually called a GUID, which
        // made a cycle's evidence directory unpredictable for the people and the verify claims that
        // read it -- and §16 documented a `tests.trx` all along. F-38.
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);

        string trx = Path.Combine(workspace.Root, "results.trx");
        CycleWorkspace.WriteRaw(trx, Trx);

        await Record(workspace, trx, changed: []);

        string kept = await File.ReadAllTextAsync(
            Path.Combine(workspace.Cycle.EvidenceDirectory, RecordEvidence.TrxFileName));

        StringAssert.Contains(kept, "UnitTestResult");
        Assert.IsFalse(
            File.Exists(Path.Combine(workspace.Cycle.EvidenceDirectory, "results.trx")),
            "The runner's own filename is not what the evidence directory is keyed by.");
    }

    [TestMethod]
    public async Task Recording_with_no_test_results_still_records_the_diff()
    {
        // The build may want to record a diff before the suite has run, and refusing would make the only
        // path to a diff go through a test run.
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);

        RecordEvidence.Response report = await Record(workspace, trx: null, changed: ["src/Thing.cs"]);

        Assert.IsNull(report.Summary);
        Assert.IsTrue(report.Written.Contains(RecordEvidence.DiffFileName, StringComparer.Ordinal));
    }

    [TestMethod]
    public async Task A_missing_results_file_is_a_failure_rather_than_an_empty_summary()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);

        using Gate gate = Recorder(workspace, new FakeRepository());

        Result<RecordEvidence.Response> result = await gate
            .Send<RecordEvidence.Command, RecordEvidence.Response>(new RecordEvidence.Command(
                workspace.Cycle.Id,
                Path.Combine(workspace.Root, "absent.trx")));

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "did not happen");
    }

    [TestMethod]
    public async Task Recording_refuses_when_git_cannot_say_where_the_branch_diverged()
    {
        // The diff is measured from that commit. Measuring from the default branch's tip would show every
        // commit somebody else landed while this cycle was open as part of this change.
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);

        using Gate gate = Recorder(workspace, new FakeRepository { Base = null });

        Result<RecordEvidence.Response> result = await gate
            .Send<RecordEvidence.Command, RecordEvidence.Response>(
                new RecordEvidence.Command(workspace.Cycle.Id));

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "diverged");
    }

    [TestMethod]
    public async Task Recording_refuses_when_there_is_no_plan_to_compare_against()
    {
        using var workspace = CycleWorkspace.Create();

        using Gate gate = Recorder(workspace, new FakeRepository());

        Result<RecordEvidence.Response> result = await gate
            .Send<RecordEvidence.Command, RecordEvidence.Response>(
                new RecordEvidence.Command(workspace.Cycle.Id));

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "no plan");
    }

    [TestMethod]
    public async Task The_report_says_the_comparison_is_a_signal_even_when_the_sets_agree()
    {
        // A reader who met that line only on a disagreement would have no way to know whether it was about
        // to stop them.
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);

        string rendered = EvidenceRenderer.Render(
            await Record(workspace, trx: null, changed: ["src/Thing.cs"]));

        StringAssert.Contains(rendered, "every declared file changed");
        StringAssert.Contains(rendered, "Verify reads tests.json and the diff, and nothing else.");
    }

    [TestMethod]
    public async Task The_report_calls_a_disagreement_a_suggestion_and_says_what_blocks_instead()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);

        string rendered = EvidenceRenderer.Render(
            await Record(workspace, trx: null, changed: ["src/Unexpected.cs"]));

        StringAssert.Contains(rendered, "undeclared  src/Unexpected.cs");
        StringAssert.Contains(rendered, "undelivered src/Thing.cs");
        StringAssert.Contains(rendered, "not a refusal");
        StringAssert.Contains(rendered, "outcome-based");
    }

    /// <summary>
    /// The gate with git substituted, which is all these tests need to steer.
    /// </summary>
    /// <remarks>
    /// The clock is not substituted: recording stamps a time nothing here asserts on, so the real
    /// one is the honest default and a fixed one would be ceremony. It is resolvable now either way
    /// -- while the registration closed over <c>TimeProvider.System</c>, it was not.
    /// </remarks>
    [TestMethod]
    public async Task An_empty_patch_beside_a_changed_file_is_refused_rather_than_recorded()
    {
        // The two answers have to agree. They did not: `git diff` cannot see an untracked file while
        // ChangedFiles asks for them, so a cycle that created files wrote an empty diff.patch and the
        // panel still said "every declared file changed, and nothing else did". §4.5 has verify reading
        // the evidence and nothing else, so an empty patch leaves a claim with nothing to cite --
        // which CON_08 forbids -- while verify-chain's proof check reads only tests.json and holds
        // regardless. Diff now stages untracked paths, so this state means a defect, and it says so.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        using Gate gate = Recorder(
            workspace,
            new FakeRepository { Changed = ["src/Roll.cs"], Patch = string.Empty });

        Result<RecordEvidence.Response> result = await gate
            .Send<RecordEvidence.Command, RecordEvidence.Response>(
                new RecordEvidence.Command(workspace.Cycle.Id, null));

        Assert.IsTrue(result.IsFailed, "A panel saying everything is in order is the failure mode.");
        StringAssert.Contains(
            string.Join(" ", result.Errors.Select(error => error.Message)),
            "cannot both be true");
    }

    private static Gate Recorder(CycleWorkspace workspace, FakeRepository git) =>
        Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

    private static async Task<RecordEvidence.Response> Record(
        CycleWorkspace workspace,
        string? trx,
        IReadOnlyList<string> changed)
    {
        using Gate gate = Recorder(workspace, new FakeRepository { Changed = changed });

        Result<RecordEvidence.Response> result = await gate
            .Send<RecordEvidence.Command, RecordEvidence.Response>(
                new RecordEvidence.Command(workspace.Cycle.Id, trx));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
