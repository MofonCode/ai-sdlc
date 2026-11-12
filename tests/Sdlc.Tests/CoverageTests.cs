using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// Coverage (D-103, D-157): the changed lines a cycle answers for, and the whole codebase held to its
/// baseline under the solution's floor.
/// </summary>
[TestClass]
public sealed class CoverageTests
{
    private const string Patch = """
        diff --git a/src/Thing.cs b/src/Thing.cs
        --- a/src/Thing.cs
        +++ b/src/Thing.cs
        @@ -1,3 +1,5 @@
         namespace Thing;
        +public static class Added
        +{
         public static class Kept
        @@ -10,2 +12,3 @@
         line twelve
        +line thirteen
         line fourteen
        """;

    [TestMethod]
    public void The_lines_a_patch_adds_are_numbered_as_the_new_file_numbers_them()
    {
        IReadOnlyDictionary<string, IReadOnlySet<int>> added = Coverage.AddedLines(Patch);

        CollectionAssert.AreEquivalent(new[] { 2, 3, 13 }, added["src/Thing.cs"].ToArray());
    }

    [TestMethod]
    public void A_line_is_covered_when_any_report_ran_it()
    {
        string root = Path.GetFullPath("solution-root");
        string first = Report(root, ("src/Thing.cs", 2, 0), ("src/Thing.cs", 3, 0), ("src/Thing.cs", 13, 1), ("src/Other.cs", 1, 1));
        string second = Report(root, ("src/Thing.cs", 2, 4));

        IReadOnlyDictionary<string, IReadOnlyDictionary<int, int>> lines = Coverage.Read([(first, "a"), (second, "b")], root).Value;
        CoverageSummary summary = Coverage.Summarise(lines, Patch, baseline: 0.5);

        Assert.AreEqual(3, summary.ChangedCoverable);
        Assert.AreEqual(2, summary.ChangedCovered, "Line 2 ran in the second report; line 3 ran in neither.");
        Assert.AreEqual(4, summary.Valid);
        Assert.AreEqual(3, summary.Covered);
        Assert.IsFalse(summary.ChangedMeetsFloor, "Two of three is under seventy per cent.");
        Assert.IsTrue(summary.HeldTheBaseline);
    }

    [TestMethod]
    public void Generated_code_under_build_output_is_not_counted()
    {
        string root = Path.GetFullPath("solution-root");
        string report = Report(root, ("src/Api/obj/Debug/net10.0/Gen/OpenApi.generated.cs", 1, 0), ("src/Api/Program.cs", 1, 1));

        IReadOnlyDictionary<string, IReadOnlyDictionary<int, int>> lines = Coverage.Read([(report, "a")], root).Value;

        CollectionAssert.AreEqual(new[] { "src/Api/Program.cs" }, lines.Keys.ToArray());
    }

    [TestMethod]
    public void A_change_with_no_coverable_line_meets_the_floor_and_a_first_measurement_holds()
    {
        CoverageSummary summary = new(0, 0, 100, 10, null);

        Assert.IsNull(summary.ChangedRate);
        Assert.IsTrue(summary.ChangedMeetsFloor);
        Assert.IsTrue(summary.HeldTheBaseline, "An adopted solution at ten per cent can still merge, and climbs from there.");
    }

    [TestMethod]
    [DataRow(10, 10, 800, 0.8, true, true)]
    [DataRow(10, 6, 800, 0.8, false, true)]
    [DataRow(10, 10, 790, 0.8, true, false)]
    public async Task The_chain_holds_the_changed_lines_to_the_floor_and_the_codebase_to_its_baseline(
        int coverable, int covered, int rate, double baseline, bool changedHolds, bool baselineHolds)
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCoverage(coverable, covered, rate, baseline);

        using var gate = Gate.For(workspace.Root);
        VerifyChain.Response verdict = (await gate.Send<VerifyChain.Query, VerifyChain.Response>(
            new VerifyChain.Query(workspace.Cycle.Id))).Value;

        Assert.AreEqual(changedHolds, verdict.Checks.Single(check => check.Name == "the changed lines are covered").Passed);
        Assert.AreEqual(baselineHolds, verdict.Checks.Single(check => check.Name == CodebaseCheck).Passed);
    }

    [TestMethod]
    [DataRow(null, 906, 0.989, true, DisplayName = "the default floor lets a high codebase fall")]
    [DataRow("95%", 906, 0.989, false, DisplayName = "under a higher floor the baseline binds again")]
    [DataRow("80%", 750, 0.760, false, DisplayName = "under the floor a fall is refused")]
    [DataRow("70%", 750, 0.760, true, DisplayName = "a person lowering the floor answers the fall")]
    public async Task Above_the_solutions_floor_the_codebase_need_not_hold_its_baseline(
        string? floor, int rate, double baseline, bool holds)
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteSolution(SolutionOrigin.Scaffolded, coverageFloor: floor);
        workspace.WriteCoverage(rate: rate, baseline: baseline);

        using var gate = Gate.For(workspace.Root);
        VerifyChain.Response verdict = (await gate.Send<VerifyChain.Query, VerifyChain.Response>(
            new VerifyChain.Query(workspace.Cycle.Id))).Value;

        VerifyChain.ChainCheck check = verdict.Checks.Single(check => check.Name == CodebaseCheck);
        Assert.AreEqual(holds, check.Passed, check.Detail);
    }

    [TestMethod]
    public async Task A_fall_under_the_floor_says_the_baseline_is_not_a_target_and_how_a_person_answers_it()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteSolution(SolutionOrigin.Scaffolded);
        workspace.WriteCoverage(rate: 711, baseline: 0.989);

        using var gate = Gate.For(workspace.Root);
        VerifyChain.Response verdict = (await gate.Send<VerifyChain.Query, VerifyChain.Response>(
            new VerifyChain.Query(workspace.Cycle.Id))).Value;

        VerifyChain.ChainCheck check = verdict.Checks.Single(check => check.Name == CodebaseCheck);
        Assert.IsFalse(check.Passed);
        StringAssert.Contains(check.Detail, "not a target");
        StringAssert.Contains(check.Detail, "how coverage is measured", "A measurement change moves every figure, and the agent should look there first.");
        StringAssert.Contains(check.Detail, "coverage_floor");
    }

    [TestMethod]
    public async Task With_no_solution_document_there_is_no_floor_and_the_baseline_binds()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCoverage(rate: 906, baseline: 0.989);

        using var gate = Gate.For(workspace.Root);
        VerifyChain.Response verdict = (await gate.Send<VerifyChain.Query, VerifyChain.Response>(
            new VerifyChain.Query(workspace.Cycle.Id))).Value;

        Assert.IsFalse(verdict.Checks.Single(check => check.Name == CodebaseCheck).Passed, "Could not tell reads as the stricter check.");
    }

    [TestMethod]
    public async Task No_recorded_coverage_fails_the_chain()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        using var gate = Gate.For(workspace.Root);
        VerifyChain.Response verdict = (await gate.Send<VerifyChain.Query, VerifyChain.Response>(
            new VerifyChain.Query(workspace.Cycle.Id))).Value;

        VerifyChain.ChainCheck check = verdict.Checks.Single(check => check.Name == "the changed lines are covered");
        Assert.IsFalse(check.Passed);
        StringAssert.Contains(check.Detail, "--coverage");
    }

    [TestMethod]
    public async Task Recording_reads_the_baseline_at_the_base_commit_and_carries_it_forward_when_it_held()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        // A working-tree baseline the cycle lowered, which must not be what it is measured against.
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", Coverage.BaselineFileName), Coverage.WriteBaseline(0.1));

        string report = Path.Combine(workspace.Root, "coverage.cobertura.xml");
        await File.WriteAllTextAsync(report, Report(workspace.Root, ("src/Thing.cs", 2, 1), ("src/Thing.cs", 3, 1), ("src/Thing.cs", 13, 1), ("src/Other.cs", 1, 0)));

        FakeRepository git = new() { Patch = Patch, Changed = ["src/Thing.cs"] };
        git.Files[git.Base + ":.sdlc/coverage.json"] = Coverage.WriteBaseline(0.7);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<RecordEvidence.Response> result = await gate.Send<RecordEvidence.Command, RecordEvidence.Response>(
            new RecordEvidence.Command(workspace.Cycle.Id, Coverage: report));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        CoverageSummary summary = result.Value.Coverage!;
        Assert.AreEqual(0.7, summary.Baseline, "Read at the base commit, not from the working tree.");
        Assert.AreEqual(3, summary.ChangedCovered);
        Assert.IsTrue(summary.HeldTheBaseline, "Three of four is 75%, over 70%.");
        Assert.AreEqual(0.75, Coverage.ReadBaseline(await File.ReadAllTextAsync(Path.Combine(workspace.Root, ".sdlc", Coverage.BaselineFileName))));
        Assert.IsTrue(File.Exists(Path.Combine(workspace.Cycle.EvidenceDirectory, Coverage.SummaryFileName)));
    }

    [TestMethod]
    [DataRow("70%", 0.75, DisplayName = "at or above the floor a fall becomes the baseline")]
    [DataRow("80%", 0.9, DisplayName = "under the floor a fall leaves the baseline where it was")]
    public async Task Recording_carries_the_rate_forward_whenever_the_codebase_holds_its_coverage(string floor, double expected)
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteSolution(SolutionOrigin.Scaffolded, coverageFloor: floor);
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", Coverage.BaselineFileName), Coverage.WriteBaseline(0.9));

        string report = Path.Combine(workspace.Root, "coverage.cobertura.xml");
        await File.WriteAllTextAsync(report, Report(workspace.Root, ("src/Thing.cs", 2, 1), ("src/Thing.cs", 3, 1), ("src/Thing.cs", 13, 1), ("src/Other.cs", 1, 0)));

        FakeRepository git = new() { Patch = Patch, Changed = ["src/Thing.cs"] };
        git.Files[git.Base + ":.sdlc/coverage.json"] = Coverage.WriteBaseline(0.9);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<RecordEvidence.Response> result = await gate.Send<RecordEvidence.Command, RecordEvidence.Response>(
            new RecordEvidence.Command(workspace.Cycle.Id, Coverage: report));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.IsFalse(result.Value.Coverage!.HeldTheBaseline, "Three of four is 75%, under the 90% recorded.");
        Assert.AreEqual(expected, Coverage.ReadBaseline(await File.ReadAllTextAsync(Path.Combine(workspace.Root, ".sdlc", Coverage.BaselineFileName))));
    }

    [TestMethod]
    [DataRow(null, 0.80)]
    [DataRow("85%", 0.85)]
    [DataRow("85", 0.85)]
    [DataRow("0%", 0.0)]
    [DataRow("100%", 1.0)]
    public void The_floor_is_a_whole_percentage_and_absent_reads_as_eighty(string? value, double expected)
    {
        string text = "---\norigin: adopted\n" + (value is null ? string.Empty : "coverage_floor: " + value + "\n") + "---\n";

        Assert.AreEqual(expected, SolutionDocument.Read(text, "solution.md").Value.CoverageFloor, 1e-9);
    }

    [TestMethod]
    [DataRow("0.8", DisplayName = "a fraction is most likely 80% meant, and is not guessed at")]
    [DataRow("120%")]
    [DataRow("-5%")]
    [DataRow("eighty")]
    public void A_floor_that_is_not_a_whole_percentage_fails_rather_than_reading_as_either_end(string value)
    {
        Result<SolutionDocument> read = SolutionDocument.Read("---\norigin: adopted\ncoverage_floor: " + value + "\n---\n", "solution.md");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "coverage_floor");
    }

    [TestMethod]
    public void The_key_block_always_carries_the_floor_so_a_team_can_see_the_setting_it_has()
    {
        CollectionAssert.Contains(
            SolutionDocument.Keys(SolutionOrigin.Adopted, SolutionStandard.Existing, Sdlc.Core.Init.Platform.GitHub),
            "coverage_floor: 80%");
        CollectionAssert.Contains(
            SolutionDocument.Keys(SolutionOrigin.Adopted, SolutionStandard.Existing, Sdlc.Core.Init.Platform.GitHub, coverageFloor: 0.9),
            "coverage_floor: 90%");
    }

    private const string CodebaseCheck = "the codebase holds its coverage";



    private static string Report(string root, params (string File, int Line, int Hits)[] lines)
    {
        IEnumerable<string> classes = lines
            .GroupBy(line => line.File)
            .Select(file => $"""
                <class name="C" filename="{file.Key}"><lines>{string.Concat(file.Select(line => $"<line number=\"{line.Line}\" hits=\"{line.Hits}\"/>"))}</lines></class>
                """);

        return $"""
            <?xml version="1.0"?>
            <coverage line-rate="0"><sources><source>{root}</source></sources>
            <packages><package name="p"><classes>{string.Concat(classes)}</classes></package></packages></coverage>
            """;
    }
}
