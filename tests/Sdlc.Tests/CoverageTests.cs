using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// Coverage (D-103): the changed lines a cycle answers for, and the whole codebase held to its baseline.
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
        Assert.AreEqual(baselineHolds, verdict.Checks.Single(check => check.Name == "the codebase is no less covered than it was").Passed);
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
