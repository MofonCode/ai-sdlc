using System.Diagnostics;
using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Reports;
using Sdlc.Core.Runs;
using Sdlc.Core.Series;

namespace Sdlc.Tests;

/// <summary>
/// A run's cycles built side by side, each on a branch of its own, and merged into the run's branch by the binary one
/// at a time, each merge tested where it lands (D-234); a cycle that does not make it is taken out (D-235); cycles that
/// share a file never build at once (D-236).
/// </summary>
/// <remarks>
/// <strong>Real git, a stand-in suite and stand-in builds.</strong> The merge, its undoing and the worktrees are what is
/// under test, so they run for real; what a build writes and what a suite reports are told to them.
/// </remarks>
[TestClass]
public sealed class RunMergeTests
{
    private const string Run = "20261121_0930_15";
    private const string Branch = "series/" + Run;
    private const string First = "20261121_1000_00";
    private const string Second = "20261121_1000_01";
    private const string Third = "20261121_1000_02";

    [TestMethod]
    public async Task A_cycle_merged_with_the_suite_green_on_the_result_keeps_the_merge_and_its_evidence()
    {
        using var scratch = Scratch.Create();
        scratch.Cycle(First, "src/First.cs");

        MergeIntoRun.Response merged = await Merge(scratch, new Suite(), First);

        Assert.AreEqual(MergeIntoRun.Outcome.Merged, merged.Outcome, merged.Detail);
        Assert.IsTrue(File.Exists(Path.Combine(scratch.Root, "src", "First.cs")), "The cycle's change is on the run's branch.");

        MergeRun? evidence = (await MergeEvidence.Read(scratch.Root, RunKind.Series, Run, CancellationToken.None)).Value;
        Assert.IsNotNull(evidence);
        Assert.AreEqual(merged.Head, evidence.Head, "The evidence names the merge commit the suite ran on.");
        Assert.AreEqual(2, evidence.Summary.Passed);
        Assert.AreEqual(string.Empty, scratch.Git("status --porcelain").Trim(), "The evidence is committed on top of the merge.");
    }

    [TestMethod]
    public async Task A_merge_the_suite_fails_on_is_undone_and_the_failing_tests_named()
    {
        using var scratch = Scratch.Create();
        scratch.Cycle(First, "src/First.cs");
        string before = scratch.Head();

        MergeIntoRun.Response merged = await Merge(scratch, new Suite(failing: "Thing.Tests.Totals_add_up"), First);

        Assert.AreEqual(MergeIntoRun.Outcome.Red, merged.Outcome);
        CollectionAssert.Contains(merged.Files.ToList(), "Thing.Tests.Totals_add_up");
        StringAssert.Contains(merged.Detail, "the merge was undone");
        Assert.AreEqual(before, scratch.Head(), "Two cycles passing alone and failing together leave the branch where it was.");
        Assert.IsFalse(File.Exists(Path.Combine(scratch.Root, "src", "First.cs")));
    }

    [TestMethod]
    public async Task A_merge_that_conflicts_is_abandoned_and_its_files_named()
    {
        using var scratch = Scratch.Create();
        scratch.Cycle(First, "README.md");
        scratch.Commit("README.md", "# the run's own\n");
        string before = scratch.Head();

        MergeIntoRun.Response merged = await Merge(scratch, new Suite(), First);

        Assert.AreEqual(MergeIntoRun.Outcome.Conflict, merged.Outcome);
        CollectionAssert.Contains(merged.Files.ToList(), "README.md");
        Assert.AreEqual(before, scratch.Head());
        Assert.AreEqual(string.Empty, scratch.Git("status --porcelain").Trim(), "Nothing is left half merged.");
    }

    [TestMethod]
    public async Task A_wave_builds_its_cycles_side_by_side_merges_each_that_passes_and_leaves_one_that_does_not()
    {
        using var scratch = Scratch.Create();
        var builds = new Builds(blocked: Second);

        WaveBuild.Response built = await Build(scratch, builds, parallel: 2, First, Second, Third);

        Assert.AreEqual(3, built.Cycles.Count);
        CollectionAssert.AreEquivalent(new[] { First, Third }, built.Cycles.Where(c => c.Merged).Select(c => c.Id.ToString()).ToArray());
        WaveBuild.Built stopped = built.Stopped.Single();
        Assert.AreEqual(Second, stopped.Id.ToString());
        Assert.AreEqual("cycle/" + Second + "/build-unattended", stopped.Branch);
        Assert.IsFalse(stopped.Pushed, "There is no remote to push it to.");
        Assert.AreEqual(2, builds.Most, "Two built at once, never more.");
        Assert.IsTrue(File.Exists(Path.Combine(scratch.Root, "src", First + ".cs")));
        Assert.IsTrue(File.Exists(Path.Combine(scratch.Root, "src", Third + ".cs")));
        Assert.IsFalse(File.Exists(Path.Combine(scratch.Root, "src", Second + ".cs")), "A cycle that did not pass is not merged.");
    }

    [TestMethod]
    public async Task A_cycle_that_passed_on_its_branch_before_the_run_stopped_is_merged_without_building_again()
    {
        using var scratch = Scratch.Create();
        scratch.Cycle(First, "src/First.cs", verified: true);
        var builds = new Builds();

        WaveBuild.Response built = await Build(scratch, builds, parallel: 2, First);

        Assert.IsTrue(built.Cycles.Single().Merged, built.Cycles.Single().Detail);
        Assert.AreEqual(0, builds.Started, "Its build's process was never started again.");
    }

    [TestMethod]
    public void Cycles_whose_plans_declare_the_same_file_wait_on_the_earlier_one()
    {
        CycleId first = Id(First);
        CycleId second = Id(Second);
        CycleId third = Id(Third);

        IReadOnlyDictionary<CycleId, IReadOnlyList<CycleId>> waits = WaveBuild.WaitsOn(
            [first, second, third],
            new Dictionary<CycleId, IReadOnlyList<string>>
            {
                [first] = ["src/Totals.cs", "src/Roll.cs"],
                [second] = ["src/Chart.cs"],
                [third] = ["src/roll.cs"],
            });

        Assert.IsEmpty(waits[first]);
        Assert.IsEmpty(waits[second], "Nothing in common, so it builds beside the first.");
        CollectionAssert.AreEqual(new[] { first }, waits[third].ToArray(), "It shares a file with the first, so it is taken from code that already holds it.");
    }

    [TestMethod]
    public void A_cycle_the_series_took_out_has_left_its_wave_and_a_fix_taken_out_stops_what_it_holds()
    {
        using var workspace = CycleWorkspace.Create(First);
        var taken = new TakenOut(First, 1, "Cycle did not pass its verification.", "cycle/" + First + "/build-unattended", Pushed: true);
        SeriesRecord record = SeriesRecord.Start(Run, DateTimeOffset.UnixEpoch, 2, Sdlc.Core.Solutions.ApprovalRequirement.None)
            .WithWave(DateTimeOffset.UnixEpoch, [new SeriesItem(SeriesItemKind.Successor, "steady-totals", Second), new SeriesItem(SeriesItemKind.Parked, First, First)])
            .WithTakenOut([taken]);

        string state = AddFix.StateOf(record, First, SeriesStatus.Planned, workspace.Root, new FakeRepository());

        Assert.AreEqual(SeriesStatus.TakenOutState, state, "Its files still read as planned on the series branch; the record says it left.");
        Assert.IsFalse(NextWave.IsOpen(state), "A wave is never held open by a cycle that left it.");

        SeriesRecord held = record with
        {
            TakenOut = [taken with { Cycle = Second }],
            Fixes = [new SeriesFix("steady-totals", "Totals_add_up", 1, DateTimeOffset.UnixEpoch, First, null, [Third], null)],
        };

        Assert.AreEqual(AddFix.FixStopped, AddFix.StateOf(held, Third, SeriesStatus.Planned, workspace.Root, new FakeRepository()));
    }

    [TestMethod]
    public void The_runs_report_names_what_it_took_out_why_and_where_its_work_is()
    {
        RunReport.Response report = new(
            RunKind.Wave,
            Run,
            null,
            [],
            [],
            [],
            [new TakenOut(Second, 1, "Cycle " + Second + " conflicts with wave/" + Run + " in src/Totals.cs.", "cycle/" + Second + "/build-unattended", Pushed: true)]);

        string text = RunReport.Render(report);

        StringAssert.Contains(text, "### Taken out");
        StringAssert.Contains(text, "- " + Second + ": Cycle " + Second + " conflicts with wave/" + Run + " in src/Totals.cs. Branch: cycle/" + Second + "/build-unattended, pushed.");
        Assert.IsFalse(text.Contains(", wave 1", StringComparison.Ordinal), "A wave that stands alone names no wave number.");
    }

    [TestMethod]
    public async Task A_wave_record_reads_back_what_it_took_out_and_a_new_wave_builds_two_at_once()
    {
        using var scratch = Scratch.Create();

        WaveRecord fresh = (await WaveRecord.Read(scratch.Root, Run, CancellationToken.None)).Value;
        Assert.AreEqual(WaveRecord.DefaultParallel, fresh.Parallel);
        Assert.IsEmpty(fresh.TakenOut);

        await (fresh with { Parallel = 1, TakenOut = [new TakenOut(First, 1, "why", "cycle/" + First + "/build-unattended", false)] }).Write(scratch.Root, CancellationToken.None);
        WaveRecord read = (await WaveRecord.Read(scratch.Root, Run, CancellationToken.None)).Value;

        Assert.AreEqual(1, read.Parallel);
        Assert.AreEqual(First, read.TakenOut.Single().Cycle);
    }

    [TestMethod]
    public async Task A_series_driven_from_the_binary_builds_its_wave_merges_it_records_it_and_opens_one_pull_request()
    {
        using var scratch = Scratch.Create();
        scratch.Series(limit: 1, First);
        var builds = new Builds();

        DriveSeries.Response driven = await Drive(scratch, builds);

        Assert.IsNull(driven.Stopped, driven.Stopped);
        Assert.IsNotNull(driven.Record.Waves.Single().Done, "The wave is recorded built.");
        Assert.IsTrue(File.Exists(Path.Combine(scratch.Root, "src", First + ".cs")), "The cycle merged into the series' branch.");
        CollectionAssert.AreEqual(new[] { "/sdlc:build " + First + " --unattended --no-pr", "/sdlc:pr --run" }, builds.Prompts.ToArray(), "Nothing to plan, one build, then the pull request.");
        Assert.IsNotNull((await MergeEvidence.Read(scratch.Root, RunKind.Series, Run, CancellationToken.None)).Value, "The merge left its evidence for the run's check.");
    }

    [TestMethod]
    public async Task A_series_takes_out_a_cycle_that_stopped_after_asking_whether_a_fix_answers_it_and_carries_on()
    {
        using var scratch = Scratch.Create();
        scratch.Series(limit: 1, First, Second);
        var builds = new Builds(blocked: Second);

        DriveSeries.Response driven = await Drive(scratch, builds);

        Assert.IsNull(driven.Stopped, driven.Stopped);
        TakenOut taken = driven.Record.TakenOut!.Single();
        Assert.AreEqual(Second, taken.Cycle);
        Assert.AreEqual(1, taken.Wave);
        StringAssert.Contains(taken.Reason, "did not pass its verification");
        Assert.IsTrue(builds.Prompts.Any(prompt => prompt.StartsWith("/sdlc:series " + Run + " --unattended --step stops", StringComparison.Ordinal)), "The stop was offered to the series before it was taken out.");
        Assert.IsNotNull(driven.Record.Waves.Single().Done, "A cycle taken out never holds its wave open.");
        Assert.IsTrue(File.Exists(Path.Combine(scratch.Root, "src", First + ".cs")), "Its sibling merged.");
        Assert.AreEqual("/sdlc:pr --run", builds.Prompts[^1]);
    }

    private static CycleId Id(string id)
    {
        Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed));
        return parsed.Value;
    }

    private static async Task<MergeIntoRun.Response> Merge(Scratch scratch, Suite suite, string cycle)
    {
        using var gate = Gate.For(scratch.Root, services => services.AddSingleton<ISuiteRunner>(suite));

        Result<MergeIntoRun.Response> merged = await gate.Send<MergeIntoRun.Command, MergeIntoRun.Response>(
            new MergeIntoRun.Command(RunKind.Series, Run, "cycle/" + cycle + "/build-unattended", "cycle " + cycle));

        Assert.IsTrue(merged.IsSuccess, string.Join("; ", merged.Errors.Select(e => e.Message)));
        return merged.Value;
    }

    private static async Task<DriveSeries.Response> Drive(Scratch scratch, Builds builds)
    {
        using var gate = Gate.For(scratch.Root, services =>
        {
            services.AddSingleton<ISuiteRunner>(new Suite());
            services.AddSingleton<IAgentProcess>(builds);
        });

        Result<DriveSeries.Response> driven = await gate.Send<DriveSeries.Command, DriveSeries.Response>(new DriveSeries.Command(Run, "/plugin"));

        Assert.IsTrue(driven.IsSuccess, string.Join("; ", driven.Errors.Select(e => e.Message)));
        return driven.Value;
    }

    private static async Task<WaveBuild.Response> Build(Scratch scratch, Builds builds, int parallel, params string[] cycles)
    {
        using var gate = Gate.For(scratch.Root, services =>
        {
            services.AddSingleton<ISuiteRunner>(new Suite());
            services.AddSingleton<IAgentProcess>(builds);
        });

        Result<WaveBuild.Response> built = await gate.Send<WaveBuild.Command, WaveBuild.Response>(
            new WaveBuild.Command(RunKind.Series, Run, [.. cycles.Select(Id)], parallel, "/plugin"));

        Assert.IsTrue(built.IsSuccess, string.Join("; ", built.Errors.Select(e => e.Message)));
        return built.Value;
    }

    private static string Verify(string outcome) => "---\ncycle: x\noutcome: " + outcome + "\n---\n\n## Criteria\n\n1. Met.\n";

    /// <summary>A suite that reports two tests, passing unless named as failing, in a results file of its own.</summary>
    private sealed class Suite(string? failing = null) : ISuiteRunner
    {
        public async Task<SuiteRun> Run(string workingDirectory, string resultsDirectory, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(resultsDirectory);
            string file = Path.Combine(resultsDirectory, "run.trx");
            await File.WriteAllTextAsync(file, $"""
                <?xml version="1.0" encoding="utf-8"?>
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                  <Results>
                    <UnitTestResult testName="Thing.Tests.Rolls_within_range" outcome="Passed" />
                    <UnitTestResult testName="Thing.Tests.Totals_add_up" outcome="{(failing == "Thing.Tests.Totals_add_up" ? "Failed" : "Passed")}" />
                  </Results>
                </TestRun>
                """, cancellationToken);

            return new SuiteRun(true, [file], string.Empty);
        }
    }

    /// <summary>Each cycle's build: a file of its own and a verdict, committed on its branch, after a moment.</summary>
    private sealed class Builds(string? blocked = null) : IAgentProcess
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> prompts = new();
        private int running;
        private int most;
        private int started;

        public int Most => most;

        public int Started => started;

        public IReadOnlyList<string> Prompts => [.. prompts];

        public async Task<AgentExit> Run(AgentLaunch launch, CancellationToken cancellationToken)
        {
            string prompt = launch.Arguments[launch.Arguments.ToList().IndexOf("-p") + 1];
            prompts.Enqueue(prompt);

            if (!prompt.StartsWith("/sdlc:build ", StringComparison.Ordinal))
            {
                return new AgentExit(0, false, null);
            }

            Interlocked.Increment(ref started);
            int now = Interlocked.Increment(ref running);
            InterlockedMax(ref most, now);

            try
            {
                string id = prompt.Split(' ')[1];

                await Task.Delay(300, cancellationToken);

                Scratch.Write(launch.WorkingDirectory, "src/" + id + ".cs", "public static class C" + id + " { }\n");
                Scratch.Write(launch.WorkingDirectory, ".sdlc/cycles/" + id + "/verify.md", Verify(id == blocked ? "blocked" : "pass"));
                Scratch.Run(launch.WorkingDirectory, "add -A");
                Scratch.Run(launch.WorkingDirectory, "commit -m \"Build " + id + "\"");

                return new AgentExit(0, false, null);
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int seen;

            do
            {
                seen = target;

                if (value <= seen)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, value, seen) != seen);
        }
    }

    /// <summary>A repository on the run's branch, inside a folder of its own so the cycles' worktrees land beside it.</summary>
    private sealed class Scratch : IDisposable
    {
        private readonly string folder;

        private Scratch(string folder)
        {
            this.folder = folder;
            Root = Path.Combine(folder, "shop");
        }

        public string Root { get; }

        public static Scratch Create()
        {
            var scratch = new Scratch(Path.Combine(Path.GetTempPath(), "sdlc-runs", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(scratch.Root);
            scratch.Git("init -b development");
            scratch.Git("config user.email suite@example.invalid");
            scratch.Git("config user.name Suite");
            scratch.Git("config core.autocrlf false");
            Write(scratch.Root, ".sdlc/solution.md", "---\norigin: scaffolded\n---\n\n# a solution\n");
            scratch.Commit("README.md", "# scratch\n");
            scratch.Git("switch -c " + Branch);
            return scratch;
        }

        /// <summary>A cycle's build branch from the run's branch, changing one file, optionally verified.</summary>
        public void Cycle(string id, string path, bool verified = false)
        {
            Git("switch -c cycle/" + id + "/build-unattended");
            Write(Root, path, "// " + id + "\n");

            if (verified)
            {
                Write(Root, ".sdlc/cycles/" + id + "/verify.md", Verify("pass"));
            }

            Git("add -A");
            Git("commit -m \"Build " + id + "\"");
            Git("switch " + Branch);
        }

        /// <summary>A series on its branch with one open wave of cycles already planned, so nothing is left to plan.</summary>
        public void Series(int limit, params string[] cycles)
        {
            SeriesRecord record = SeriesRecord.Start(RunMergeTests.Run, DateTimeOffset.UnixEpoch, limit, Sdlc.Core.Solutions.ApprovalRequirement.None)
                .WithWave(DateTimeOffset.UnixEpoch, [.. cycles.Select(id => new SeriesItem(SeriesItemKind.Approved, id, id))]);

            foreach (string id in cycles)
            {
                Write(Root, ".sdlc/cycles/" + id + "/plan.md", "# plan\n");
            }

            Write(Root, SeriesRecord.RelativeDirectory(RunMergeTests.Run) + "/" + SeriesRecord.FileName, record.Write());
            Git("add -A");
            Git("commit -m \"Series\"");
        }

        public void Commit(string path, string content)
        {
            Write(Root, path, content);
            Git("add -A");
            Git("commit -m \"" + path + "\"");
        }

        public string Head() => Git("rev-parse HEAD").Trim();

        public string Git(string arguments) => Run(Root, arguments);

        public static void Write(string root, string path, string content)
        {
            string full = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        public static string Run(string workingDirectory, string arguments)
        {
            using Process process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            })!;

            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, string.Create(CultureInfo.InvariantCulture, $"git {arguments}: {error.Result}"));
            return output.Result;
        }

        public void Dispose()
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(folder, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
