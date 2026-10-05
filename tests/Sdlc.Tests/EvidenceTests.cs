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
    public void A_plan_names_a_parameterised_test_and_its_cases_are_found()
    {
        // A runner records one result per case and puts the arguments in the name. A plan names the test,
        // because the cases are the plan's business only when it says so. Matching nothing here is what
        // pushed a trial cycle into collapsing two browser engines into a loop inside one test, which cost
        // the per-engine result the verify document was citing.
        TestSummary summary = new(
            null,
            null,
            null,
            [
                new RecordedTest("Page_shows_a_face (\"chromium\")", TestOutcome.Passed),
                new RecordedTest("Page_shows_a_face (\"webkit\")", TestOutcome.Passed),
            ]);

        Assert.IsTrue(summary.DidPass("Page_shows_a_face"));
    }

    [TestMethod]
    public void One_passing_case_does_not_answer_for_a_failing_one()
    {
        // The reason this is every case rather than any case. The ambiguity check at recording time cannot
        // catch this pair: it groups by the recorded name, and two cases of one test have different ones.
        TestSummary summary = new(
            null,
            null,
            null,
            [
                new RecordedTest("Page_shows_a_face (\"chromium\")", TestOutcome.Passed),
                new RecordedTest("Page_shows_a_face (\"webkit\")", TestOutcome.Failed, "the face was blank"),
            ]);

        Assert.IsFalse(summary.DidPass("Page_shows_a_face"));
    }

    [TestMethod]
    public void A_parameterised_case_is_found_under_its_namespace_too()
    {
        // Both relaxations at once, which is the shape a real runner reports: the fully qualified name a
        // plan has no way to know, and the arguments it has no reason to carry.
        TestSummary summary = new(
            null,
            null,
            null,
            [new RecordedTest("Dice.Tests.DicePageTests.Page_shows_a_face(engine: \"webkit\")", TestOutcome.Passed)]);

        Assert.IsTrue(summary.DidPass("Page_shows_a_face"));
        Assert.IsFalse(summary.DidPass("Page_shows_a_fade"));
    }

    [TestMethod]
    public void A_skipped_case_does_not_prove_the_test()
    {
        // A skipped case is not evidence of anything, and a proof whose second engine never ran is a proof
        // of one engine. TestOutcome keeps the value distinct precisely so this can be refused.
        TestSummary summary = new(
            null,
            null,
            null,
            [
                new RecordedTest("Page_shows_a_face (\"chromium\")", TestOutcome.Passed),
                new RecordedTest("Page_shows_a_face (\"webkit\")", TestOutcome.Skipped),
            ]);

        Assert.IsFalse(summary.DidPass("Page_shows_a_face"));
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
    public void A_file_that_judges_the_cycle_is_reported_even_when_the_plan_declared_it()
    {
        // The dice-roller cycle declared this edit and was entirely in order. The point is that a change
        // to the rules reaches a reviewer as a change to the rules, not as one row in a file table.
        FileComparison comparison = DeclaredFiles.Compare(
            ["tests/Dice.Architecture.Tests/StructureTests.cs", "src/Dice/Roll.cs"],
            ["tests/Dice.Architecture.Tests/StructureTests.cs", "src/Dice/Roll.cs"]);

        Assert.IsTrue(comparison.Agrees, "A declared governing file is not a disagreement.");

        CollectionAssert.AreEqual(
            new[] { "tests/Dice.Architecture.Tests/StructureTests.cs" },
            comparison.Governing.ToArray());
    }

    [TestMethod]
    public void The_governing_set_is_what_the_framework_writes_or_scaffolds()
    {
        FileComparison comparison = DeclaredFiles.Compare(
            [],
            [
                "Directory.Build.props",
                "tests/Directory.Build.props",
                "BannedSymbols.txt",
                ".editorconfig",
                ".claude/settings.json",
                ".agents/hooks.json",
                ".github/hooks/sdlc.json",
                ".github/workflows/ci.yml",
                ".sdlc/constraints.md",
                ".sdlc/techstack.md",
                ".sdlc/hooks/pre-push",
                "src/Dice/Roll.cs",
                "Directory.Packages.props",
            ]);

        CollectionAssert.AreEqual(
            new[]
            {
                ".agents/hooks.json",
                ".claude/settings.json",
                ".editorconfig",
                ".github/hooks/sdlc.json",
                ".github/workflows/ci.yml",
                ".sdlc/constraints.md",
                ".sdlc/hooks/pre-push",
                ".sdlc/techstack.md",
                "BannedSymbols.txt",
                "Directory.Build.props",
                "tests/Directory.Build.props",
            },
            comparison.Governing.ToArray(),
            "A package version is not a rule, and source is not a rule.");
    }

    [TestMethod]
    public void A_governing_file_under_sdlc_survives_the_filter_that_drops_a_cycle_s_paperwork()
    {
        // constraints.md and techstack.md live under .sdlc/, which the comparison otherwise drops whole.
        // Dropping them first would hide the two changes most worth a second look.
        FileComparison comparison = DeclaredFiles.Compare(
            [],
            [".sdlc/techstack.md", ".sdlc/cycles/20260929_1456_32/plan.md"]);

        CollectionAssert.AreEqual(new[] { ".sdlc/techstack.md" }, comparison.Governing.ToArray());
        Assert.AreEqual(0, comparison.Undeclared.Count, "The paperwork is still not part of the change.");
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
    public async Task Every_results_file_a_run_produced_reaches_the_summary()
    {
        // A run with two test projects writes two results files, and this took one: a cycle recorded 14
        // of 26 results and the verification adjudicated against the 14 without knowing. The half that
        // went missing was the architecture tests, which is the half holding the rules.
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);

        string results = Path.Combine(workspace.Root, "TestResults");
        Directory.CreateDirectory(Path.Combine(results, "unit"));
        Directory.CreateDirectory(Path.Combine(results, "architecture"));

        CycleWorkspace.WriteRaw(Path.Combine(results, "unit", "a.trx"), Trx);
        CycleWorkspace.WriteRaw(
            Path.Combine(results, "architecture", "b.trx"),
            Trx.Replace("Dice.Tests.RollTests", "Dice.Architecture.Tests.StructureTests", StringComparison.Ordinal));

        RecordEvidence.Response report = await Record(workspace, results, changed: ["src/Thing.cs"]);

        Assert.AreEqual(2, report.TestSources.Count, "Both results files have to be found.");
        Assert.AreEqual(6, report.Summary!.Tests.Count, "Three tests from each file.");

        Assert.IsTrue(
            report.Summary.Tests.Any(test =>
                test.Name == "Dice.Architecture.Tests.StructureTests.Two_requests_differ"),
            "A failure in the second project has to reach the summary a verification reads.");

        // Numbered rather than one of them claiming the fixed name, because one of two cannot be the one.
        Assert.IsTrue(File.Exists(Path.Combine(
            workspace.Cycle.EvidenceDirectory,
            RecordEvidence.TrxFileNameAt(1))));

        Assert.IsTrue(File.Exists(Path.Combine(
            workspace.Cycle.EvidenceDirectory,
            RecordEvidence.TrxFileNameAt(2))));
    }

    [TestMethod]
    public async Task A_test_name_reported_with_two_outcomes_refuses_rather_than_letting_one_answer()
    {
        // verify-chain matches a plan's named test on the end of what the runner reported, so the same
        // name passing in one project and failing in another would let the pass answer for the failure.
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);

        string results = Path.Combine(workspace.Root, "TestResults");
        Directory.CreateDirectory(results);

        CycleWorkspace.WriteRaw(Path.Combine(results, "a.trx"), Trx);
        CycleWorkspace.WriteRaw(
            Path.Combine(results, "b.trx"),
            Trx.Replace("outcome=\"Failed\"", "outcome=\"Passed\"", StringComparison.Ordinal));

        using Gate gate = Recorder(workspace, new FakeRepository { Changed = ["src/Thing.cs"] });

        Result<RecordEvidence.Response> result = await gate
            .Send<RecordEvidence.Command, RecordEvidence.Response>(
                new RecordEvidence.Command(workspace.Cycle.Id, results));

        Assert.IsTrue(result.IsFailed, "A name with two outcomes cannot be recorded as either.");
        StringAssert.Contains(result.Errors[0].Message, "Two_requests_differ");
    }

    [TestMethod]
    public async Task A_directory_with_no_results_file_is_a_failure_rather_than_an_empty_summary()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);

        string results = Path.Combine(workspace.Root, "TestResults");
        Directory.CreateDirectory(results);

        using Gate gate = Recorder(workspace, new FakeRepository());

        Result<RecordEvidence.Response> result = await gate
            .Send<RecordEvidence.Command, RecordEvidence.Response>(
                new RecordEvidence.Command(workspace.Cycle.Id, results));

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "did not happen");
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

    [TestMethod]
    public async Task Recording_a_new_run_under_a_standing_verdict_is_refused()
    {
        // A verdict adjudicates one recorded run. A trial cycle passed verification, renamed a test,
        // re-recorded the evidence and left verify.md citing 26 tests where tests.json then held 24, and
        // two per-engine results that no longer existed. Nothing downstream could tell: the chain check
        // reads the plan's named tests against tests.json, and both of those moved together.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteVerify(outcome: "pass");

        using Gate gate = Recorder(workspace, new FakeRepository { Changed = ["src/Roll.cs"] });

        Result<RecordEvidence.Response> result = await gate
            .Send<RecordEvidence.Command, RecordEvidence.Response>(
                new RecordEvidence.Command(workspace.Cycle.Id, null));

        Assert.IsTrue(result.IsFailed, "A verdict about evidence that is gone is the failure mode.");

        string said = string.Join(" ", result.Errors.Select(error => error.Message));

        StringAssert.Contains(said, "already has a verdict");
        StringAssert.Contains(said, "verify.md", "A refusal says which file has to move.");
    }

    [TestMethod]
    public async Task The_cycles_own_paperwork_is_left_out_of_the_recorded_patch()
    {
        // The patch is what a verify reads and nothing else, so what it opens with matters. It used to
        // open with the intent, the spec, the plan and every findings round -- and on a second recording
        // it contained the previous diff.patch, which doubles its size each time. The declared-file
        // comparison has always excluded these; the patch did not, and the two readers now agree.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        var git = new FakeRepository { Changed = ["src/Roll.cs"] };

        using Gate gate = Recorder(workspace, git);

        Result<RecordEvidence.Response> result = await gate
            .Send<RecordEvidence.Command, RecordEvidence.Response>(
                new RecordEvidence.Command(workspace.Cycle.Id, null));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.IsNotNull(git.Excluded, "The patch has to be asked for with an exclusion.");
        CollectionAssert.Contains(git.Excluded.ToArray(), Cycle.SdlcDirectoryName);
    }

    [TestMethod]
    public async Task A_run_that_touched_only_the_cycles_own_files_records_rather_than_refusing()
    {
        // The interaction the exclusion creates. ChangedFiles still reports the artifacts, so counting
        // every changed path against an empty patch would refuse a recording for a disagreement that is
        // now the design rather than a defect.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        using Gate gate = Recorder(
            workspace,
            new FakeRepository
            {
                Changed = [Cycle.SdlcDirectoryName + "/cycles/20260929_1456_32/plan.md"],
                Patch = string.Empty,
            });

        Result<RecordEvidence.Response> result = await gate
            .Send<RecordEvidence.Command, RecordEvidence.Response>(
                new RecordEvidence.Command(workspace.Cycle.Id, null));

        Assert.IsTrue(
            result.IsSuccess,
            "An empty patch is correct when every changed file is one the patch excludes: "
            + string.Join("; ", result.Errors.Select(e => e.Message)));
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
