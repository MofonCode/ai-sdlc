using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Reports;
using Sdlc.Core.Series;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

namespace Sdlc.Tests;

/// <summary>
/// A series that adds its own fix: a test that passed in one recorded run and failed in another is fixed first, in a
/// cycle of its own, and holds what it stopped until it passes (D-232).
/// </summary>
[TestClass]
public sealed class SeriesFixTests
{
    private const string Run = "20261121_0930_15";
    private const string Branch = "series/" + Run;
    private const string Passed = "20261121_1000_00";
    private const string Stopped = "20261121_1000_01";
    private const string Fix = "20261121_1000_02";
    private const string Test = "Charts.ChartCacheTests.Sharers_are_told_the_failure";
    private const string Slug = "steady-sharers-are-told-the-failure";
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 9, 30, 15, TimeSpan.Zero);

    [TestMethod]
    public void A_fix_is_named_for_the_test_alone()
    {
        Assert.AreEqual(Slug, AddFix.SlugOf(Test));
        Assert.AreEqual("steady-rolls-within-range", AddFix.SlugOf("Dice.Tests.Rolls_within_range(6)"));
        Assert.IsTrue(Successor.IsSlug(AddFix.SlugOf("A." + new string('x', 90))), "Long names are cut to a slug.");
    }

    [TestMethod]
    public async Task A_fix_goes_first_in_the_open_wave_and_on_the_list_and_holds_what_it_stopped()
    {
        using CycleWorkspace workspace = Stopped_on_a_flaky_test(withList: true);
        FakeRepository git = OnSeries();

        AddFix.Response added = await Add(workspace, git);

        Assert.AreEqual(Slug, added.Fix.Successor);
        Assert.AreEqual(Passed, added.Fix.PassedIn, "The cycle whose recorded run passed it.");
        StringAssert.Contains(added.Fix.Detail!, "found 404");

        SeriesRecord record = (await SeriesRecord.Read(workspace.Root, Run, CancellationToken.None)).Value;
        Assert.AreEqual(Slug, record.Waves[^1].Items[0].Ref, "First in the wave, so it is built before what it holds.");
        Assert.AreEqual(3, record.Waves[^1].Items.Count, "An added fix is not counted against the wave size.");
        Assert.AreEqual(Slug, record.Scope![0].Ref, "On the list, so the series may take it (D-231).");
        Assert.AreEqual(1, record.Fixes!.Count);

        string successor = await File.ReadAllTextAsync(Path.Combine(Successor.DirectoryOf(workspace.Root), Slug + ".md"));
        Assert.IsTrue(Successor.Parse(Slug, successor).IsSuccess, "The binary writes a successor that reads like any other.");
        StringAssert.Contains(successor, "succeeds: " + Passed);
        StringAssert.Contains(successor, "D-216");
        Assert.IsTrue(File.Exists(Path.Combine(workspace.Root, added.Fix.Results!)), "The failing run is kept for the fix to read.");

        NextWave.Response handed = await Wave(workspace, git);
        Assert.IsNotNull(handed.Open, "The wave is not finished: the fix is still to build.");

        using (var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git).AddSingleton<TimeProvider>(new FakeClock(_now))))
        {
            Result<NextWave.Recorded> done = await gate.Send<NextWave.Done, NextWave.Recorded>(new NextWave.Done());
            Assert.IsTrue(done.IsFailed, "A wave is not recorded built before the fix it added.");
            StringAssert.Contains(done.Errors[0].Message, "successor:" + Slug);
        }

        string text = NextWave.Render(handed);
        StringAssert.Contains(text, "/sdlc:intent --from successor:" + Slug + " --unattended");
        StringAssert.Contains(text, AddFix.WaitsOnFix);
    }

    [TestMethod]
    public async Task What_a_fix_holds_waits_for_it_then_is_built_again_and_the_wave_ends_once_it_was()
    {
        using CycleWorkspace workspace = Stopped_on_a_flaky_test(withList: false);
        FakeRepository git = OnSeries();
        await Add(workspace, git);

        // The fix opened, and passed, with its evidence recorded at its head.
        Lead(workspace, Fix, Slug);
        Verify(workspace, Fix, "pass");
        Head(workspace, Fix, "fixhead");

        NextWave.Response handed = await Wave(workspace, git);
        Assert.IsNotNull(handed.Open);
        Assert.AreEqual(SeriesStatus.Planned, handed.Open[^1], "Its fix passed, so the held cycle is the wave's build's again.");

        // Blocked on the fault before the fix: built again on top of it.
        Verify(workspace, Stopped, "blocked");
        Head(workspace, Stopped, "oldhead");
        handed = await Wave(workspace, git);
        Assert.IsNotNull(handed.Open);
        Assert.AreEqual(AddFix.BuildAgain, handed.Open[^1]);
        StringAssert.Contains(NextWave.Render(handed), "the wave's build remediates it");

        // Recorded again on top of the fix and still blocked: the verdict stands, and the wave can end.
        Head(workspace, Stopped, "newhead");
        git.Ancestors.Add(("fixhead", "newhead"));
        Result<NextWave.Response> next = await SendWave(workspace, git);
        Assert.IsTrue(next.IsFailed || next.Value.Open is null, "Nothing is left in the wave.");
    }

    [TestMethod]
    public async Task A_fix_that_does_not_pass_leaves_what_it_held_stopped()
    {
        using CycleWorkspace workspace = Stopped_on_a_flaky_test(withList: false);
        FakeRepository git = OnSeries();
        await Add(workspace, git);
        Lead(workspace, Fix, Slug);
        Verify(workspace, Fix, "escalated");

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        SeriesStatus.Response status = (await gate.Send<SeriesStatus.Query, SeriesStatus.Response>(new SeriesStatus.Query(Run))).Value;

        Assert.AreEqual(AddFix.FixStopped, status.Items.Single(item => item.Item.Cycle == Stopped).State);
        Assert.IsFalse(NextWave.IsOpen(AddFix.FixStopped), "A held cycle whose fix stopped does not keep the wave open.");

        string text = SeriesStatus.Render(status);
        StringAssert.Contains(text, "Added by the series");
        StringAssert.Contains(text, "successor:" + Slug + " -- " + Test + " passed in " + Passed);
    }

    [TestMethod]
    public async Task The_run_report_says_what_the_series_added_and_why()
    {
        using CycleWorkspace workspace = Stopped_on_a_flaky_test(withList: false);
        FakeRepository git = OnSeries();
        await Add(workspace, git);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<RunReport.Response> read = await gate.Send<RunReport.Query, RunReport.Response>(new RunReport.Query(RunKind.Series, Run, []));

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));
        string text = RunReport.Render(read.Value);
        StringAssert.Contains(text, "### Added by the series");
        StringAssert.Contains(text, "held " + Stopped + " until it passed");
    }

    [TestMethod]
    public async Task A_fix_is_refused_unless_the_evidence_shows_the_test_comes_and_goes_and_the_bounds_allow_it()
    {
        using CycleWorkspace workspace = Stopped_on_a_flaky_test(withList: false);
        FakeRepository git = OnSeries();
        string results = Path.Combine(workspace.Root, "run.trx");

        await Refused(workspace, git, new AddFix.Command("Charts.Other_test", results, [Stopped]), "is not in the run");
        await Refused(workspace, git, new AddFix.Command(Test, results, ["20261121_1100_00"]), "is not a cycle of wave 1");
        await Refused(workspace, git, new AddFix.Command(Test, results, []), "--holds");

        await File.WriteAllTextAsync(results, Trx("Passed"));
        await Refused(workspace, git, new AddFix.Command(Test, results, [Stopped]), "did not fail");

        await File.WriteAllTextAsync(results, Trx("Failed"));
        File.Delete(Path.Combine(new Cycle(Parse(Passed), workspace.Root).EvidenceDirectory, RecordEvidence.SummaryFileName));
        await Refused(workspace, git, new AddFix.Command(Test, results, [Stopped]), "No cycle's recorded evidence");

        using CycleWorkspace required = Stopped_on_a_flaky_test(withList: false, ApprovalRequirement.Required);
        await Refused(required, git, new AddFix.Command(Test, Path.Combine(required.Root, "run.trx"), [Stopped]), "approval: required");

        using CycleWorkspace twice = Stopped_on_a_flaky_test(withList: false);
        await Add(twice, git);
        await Refused(twice, git, new AddFix.Command(Test, Path.Combine(twice.Root, "run.trx"), [Stopped]), "already added a fix for");
        await Refused(twice, git, new AddFix.Command("Charts.Another", Path.Combine(twice.Root, "run.trx"), [Stopped]), "one fix a wave");

        using CycleWorkspace closed = Stopped_on_a_flaky_test(withList: false);
        CycleWorkspace.WriteRaw(SeriesRecord.PathIn(closed.Root, Run), (await SeriesRecord.Read(closed.Root, Run, CancellationToken.None)).Value.WithWaveDone(_now).Write());
        await Refused(closed, git, new AddFix.Command(Test, Path.Combine(closed.Root, "run.trx"), [Stopped]), "no wave open");
    }

    // A series in its first wave: one cycle passed with the test in its evidence, one stopped when the test failed.
    private static CycleWorkspace Stopped_on_a_flaky_test(bool withList, ApprovalRequirement approval = ApprovalRequirement.None)
    {
        var workspace = CycleWorkspace.Create("20261120_0800_00");
        Lead(workspace, Passed, "chart-cache");
        Verify(workspace, Passed, "pass");
        CycleWorkspace.WriteRaw(
            Path.Combine(new Cycle(Parse(Passed), workspace.Root).EvidenceDirectory, RecordEvidence.SummaryFileName),
            TestSummaryReader.Write(new TestSummary(Passed, _now, [new RecordedTest(Test, TestOutcome.Passed)])));
        Lead(workspace, Stopped, "loading-feedback");
        CycleWorkspace.WriteRaw(new Cycle(Parse(Stopped), workspace.Root).PathOf(ArtifactKind.Plan), "---\ncycle: " + Stopped + "\n---\n\n# p\n");
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, "run.trx"), Trx("Failed"));

        SeriesRecord record = SeriesRecord.Start(Run, _now, 3, approval, waveSize: 2)
            .WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "chart-cache", Passed), new SeriesItem(SeriesItemKind.Successor, "loading-feedback", Stopped)]);

        if (withList)
        {
            record = record.WithScope([new SeriesScopeItem(SeriesItemKind.Successor, "chart-cache", 1), new SeriesScopeItem(SeriesItemKind.Successor, "loading-feedback", 1)]);
        }

        CycleWorkspace.WriteRaw(SeriesRecord.PathIn(workspace.Root, Run), record.Write());
        return workspace;
    }

    private static string Trx(string outcome) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testName="{Test}" outcome="{outcome}">
              <Output><ErrorInfo><Message>Expected no 404 in the body, found 404 in trace 9f404a.</Message></ErrorInfo></Output>
            </UnitTestResult>
          </Results>
        </TestRun>
        """;

    private static async Task<AddFix.Response> Add(CycleWorkspace workspace, FakeRepository git)
    {
        Result<AddFix.Response> added = await Send(workspace, git, new AddFix.Command("Sharers_are_told_the_failure", Path.Combine(workspace.Root, "run.trx"), [Stopped]));

        Assert.IsTrue(added.IsSuccess, string.Join("; ", added.Errors.Select(e => e.Message)));
        return added.Value;
    }

    private static async Task Refused(CycleWorkspace workspace, FakeRepository git, AddFix.Command command, string why)
    {
        Result<AddFix.Response> added = await Send(workspace, git, command);

        Assert.IsTrue(added.IsFailed, "Expected a refusal saying: " + why);
        StringAssert.Contains(string.Join(" ", added.Errors.Select(e => e.Message)), why);
    }

    private static async Task<Result<AddFix.Response>> Send(CycleWorkspace workspace, FakeRepository git, AddFix.Command command)
    {
        using var gate = Gate.For(workspace.Root, services => services
            .AddSingleton<IRepository>(git)
            .AddSingleton<TimeProvider>(new FakeClock(_now)));

        return await gate.Send<AddFix.Command, AddFix.Response>(command);
    }

    private static async Task<NextWave.Response> Wave(CycleWorkspace workspace, FakeRepository git)
    {
        Result<NextWave.Response> result = await SendWave(workspace, git);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static async Task<Result<NextWave.Response>> SendWave(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services
            .AddSingleton<IRepository>(git)
            .AddSingleton<TimeProvider>(new FakeClock(_now)));

        return await gate.Send<NextWave.Command, NextWave.Response>(new NextWave.Command());
    }

    private static FakeRepository OnSeries()
    {
        FakeRepository git = new() { Branch = Branch };
        git.Refs.Add(Branch);
        return git;
    }

    private static void Lead(CycleWorkspace workspace, string id, string slug) =>
        CycleWorkspace.WriteRaw(
            new Cycle(Parse(id), workspace.Root).PathOf(ArtifactKind.Intent),
            "---\ncycle: " + id + "\ntype: feature addition\nlead: successor:" + slug + "\n---\n\n# Charts load once\n");

    private static void Verify(CycleWorkspace workspace, string id, string outcome) =>
        CycleWorkspace.WriteRaw(new Cycle(Parse(id), workspace.Root).PathOf(ArtifactKind.Verify), "---\ncycle: " + id + "\noutcome: " + outcome + "\n---\n\n# v\n");

    private static void Head(CycleWorkspace workspace, string id, string head) =>
        CycleWorkspace.WriteRaw(Path.Combine(new Cycle(Parse(id), workspace.Root).EvidenceDirectory, RecordEvidence.HeadFileName), head + "\n");

    private static CycleId Parse(string id)
    {
        Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed));
        return parsed.Value;
    }
}
