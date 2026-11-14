using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// A recorded run keeps how long it took and its slowest tests, beside the summary and out of every reader's
/// prompt (D-169).
/// </summary>
[TestClass]
public sealed class RunTimingsTests
{
    private const string Trx = """
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Times creation="2026-11-13T20:00:00.000+00:00" start="2026-11-13T20:00:05.000+00:00" finish="2026-11-13T22:31:05.000+00:00" />
          <Results>
            <UnitTestResult testName="Charts_a_stock" outcome="Passed" duration="00:12:03.5000000" />
            <UnitTestResult testName="Rolls" outcome="Passed" duration="00:00:00.0420000" />
            <UnitTestResult testName="Keyboard_rolls" outcome="Passed" duration="00:01:30.0000000" />
          </Results>
        </TestRun>
        """;

    [TestMethod]
    public void A_results_file_gives_the_run_its_span_and_its_slowest_tests_slowest_first()
    {
        var timings = RunTimings.FromTrx([(Trx, "tests.trx")]);

        Assert.IsNotNull(timings);
        Assert.AreEqual(TimeSpan.FromMinutes(151), timings.Wall);
        Assert.AreEqual("2h 31m", RunTimings.Say(timings.Wall!.Value));
        CollectionAssert.AreEqual(new[] { "Charts_a_stock", "Keyboard_rolls", "Rolls" }, timings.Slowest.Select(test => test.Name).ToArray());
        Assert.AreEqual(723.5, timings.Slowest[0].Seconds);
        Assert.AreEqual(3, timings.Files.Single().Tests);
    }

    [TestMethod]
    public void Two_results_files_run_together_span_from_the_first_start_to_the_last_finish()
    {
        string second = Trx
            .Replace("20:00:05", "20:10:00", StringComparison.Ordinal)
            .Replace("22:31:05", "23:00:05", StringComparison.Ordinal);

        var timings = RunTimings.FromTrx([(Trx, "tests.1.trx"), (second, "tests.2.trx")]);

        Assert.IsNotNull(timings);
        Assert.AreEqual(TimeSpan.FromHours(3), timings.Wall);
        Assert.AreEqual(2, timings.Files.Count);
    }

    [TestMethod]
    public void A_results_file_with_no_times_leaves_no_timings()
    {
        Assert.IsNull(RunTimings.FromTrx([("<TestRun><Results><UnitTestResult testName=\"T\" outcome=\"Passed\" /></Results></TestRun>", "tests.trx")]));
    }

    [TestMethod]
    public async Task Recording_a_run_keeps_its_timings_beside_the_summary_and_says_where_the_time_went()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);
        string trx = Path.Combine(workspace.Root, "results.trx");
        CycleWorkspace.WriteRaw(trx, Trx);

        Result<RecordEvidence.Response> recorded;
        using (var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(new FakeRepository())))
        {
            recorded = await gate.Send<RecordEvidence.Command, RecordEvidence.Response>(new RecordEvidence.Command(workspace.Cycle.Id, Trx: trx));
        }

        Assert.IsTrue(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(e => e.Message)));
        CollectionAssert.Contains(recorded.Value.Written.ToArray(), RunTimings.FileName);

        string summary = await File.ReadAllTextAsync(Path.Combine(workspace.Cycle.EvidenceDirectory, RecordEvidence.SummaryFileName));
        Assert.IsFalse(summary.Contains("seconds", StringComparison.Ordinal), "The summary every reader is handed carries no duration.");

        string printed = EvidenceRenderer.Render(recorded.Value);
        StringAssert.Contains(printed, "the run took 2h 31m");
        StringAssert.Contains(printed, "12m 3s  Charts_a_stock");
        StringAssert.Contains(printed, "no reader is handed it");
    }
}
