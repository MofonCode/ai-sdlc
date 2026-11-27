using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Drafts;
using Sdlc.Core.Git;
using Sdlc.Core.Harvest;
using Sdlc.Core.Leads;
using Sdlc.Core.Reports;
using Sdlc.Core.Series;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

namespace Sdlc.Tests;

/// <summary>
/// A series: wave after wave into <c>series/&lt;id&gt;</c>, its record, the one report a run ends on, and a rejected
/// series abandoned in one step.
/// </summary>
[TestClass]
public sealed class SeriesTests
{
    private const string Run = "20261121_0930_15";
    private const string Branch = "series/" + Run;
    private const string First = "20261121_1000_00";
    private const string Second = "20261121_1000_01";
    private const string Third = "20261121_1000_02";
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 9, 30, 15, TimeSpan.Zero);

    [TestMethod]
    public void A_record_reads_back_what_it_wrote_and_an_older_one_without_its_lists_still_reads()
    {
        SeriesRecord record = SeriesRecord.Start(Run, _now, 3, ApprovalRequirement.None)
            .WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "totals", null), new SeriesItem(SeriesItemKind.Parked, Second, Second)]);

        string json = record.Write();
        StringAssert.Contains(json, "\"ref\": \"totals\"");
        StringAssert.Contains(json, "\"kind\": \"successor\"");
        StringAssert.Contains(json, "\"approval\": \"none\"");
        StringAssert.Contains(json, "\"cycle\": null", "An unresolved item says so rather than leaving the key out.");

        SeriesRecord read = SeriesRecord.Parse(json, "series.json").Value;
        Assert.AreEqual(json, read.Write());
        Assert.AreEqual(1, read.Waves.Single().Number);
        Assert.IsFalse(read.Finished);

        Result<SeriesRecord> old = SeriesRecord.Parse("{ \"id\": \"" + Run + "\", \"limit\": 2, \"approval\": \"required\" }", "series.json");
        Assert.IsTrue(old.IsSuccess, string.Join("; ", old.Errors.Select(e => e.Message)));
        Assert.AreEqual(0, old.Value.Waves.Count);
        Assert.AreEqual(ApprovalRequirement.Required, old.Value.Approval);
        Assert.AreEqual(SeriesRecord.DefaultWaveSize, old.Value.WaveSize, "A record from before the wave size reads as the default (D-228).");
        StringAssert.Contains(json, "\"wave_size\": 3");

        Assert.IsTrue(SeriesRecord.Parse("{ not json", "series.json").IsFailed);
    }

    [TestMethod]
    public async Task Under_approval_none_a_wave_takes_every_successor_and_parked_cycle_ready_against_the_series_branch()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        SuccessorTests.Write(workspace, "totals", First);
        SuccessorTests.Write(workspace, "chart", "successor:totals");
        Opened(workspace, Second);
        Park(workspace, Second, First);
        Record(workspace, SeriesRecord.Start(Run, _now, 3, ApprovalRequirement.None));
        FakeRepository git = OnSeries();
        git.Files[Branch + ":" + Folder(First) + "verify.md"] = "---\ncycle: " + First + "\noutcome: pass\n---\n\n# v\n";

        NextWave.Response taken = await Wave(workspace, git);

        Assert.IsNotNull(taken.Wave);
        Assert.AreEqual(1, taken.Wave.Number);
        CollectionAssert.AreEquivalent(new[] { "totals", Second }, taken.Wave.Items.Select(item => item.Ref).ToArray());
        Assert.IsFalse(taken.Wave.Items.Any(item => item.Ref == "chart"), "A successor still waiting is not taken.");

        string text = NextWave.Render(taken);
        StringAssert.Contains(text, "/sdlc:intent --from successor:totals --unattended");
        StringAssert.Contains(text, "sdlc unpark " + Second + " --onto " + Branch);

        SeriesRecord written = (await SeriesRecord.Read(workspace.Root, Run, CancellationToken.None)).Value;
        Assert.AreEqual(1, written.Waves.Count, "The wave is recorded.");
    }

    [TestMethod]
    public async Task A_wave_takes_no_more_than_the_wave_size_parked_cycles_first_and_leaves_the_rest_ready()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        SuccessorTests.Write(workspace, "totals", First);
        SuccessorTests.Write(workspace, "chart", First);
        Opened(workspace, Second);
        Park(workspace, Second, First);
        Record(workspace, SeriesRecord.Start(Run, _now, 3, ApprovalRequirement.None, waveSize: 2));
        FakeRepository git = OnSeries();
        git.Files[Branch + ":" + Folder(First) + "verify.md"] = "---\ncycle: " + First + "\noutcome: pass\n---\n\n# v\n";

        NextWave.Response taken = await Wave(workspace, git);

        Assert.IsNotNull(taken.Wave);
        Assert.AreEqual(2, taken.Wave.Items.Count, "Three were ready; the wave size is two (D-228).");
        Assert.AreEqual(Second, taken.Wave.Items[0].Ref, "The parked cycle was planned once already, so it goes first.");
        Assert.AreEqual(1, taken.Left);

        string text = NextWave.Render(taken);
        StringAssert.Contains(text, "1 more is ready, past this series' wave size of 2");
        StringAssert.Contains(text, "one after another, never in parallel");

        await WaveDone(workspace, git);
        NextWave.Response next = await Wave(workspace, git);

        Assert.IsNotNull(next.Wave);
        Assert.AreEqual(2, next.Wave.Number);
        Assert.AreEqual(1, next.Wave.Items.Count, "What the first wave left is still ready for the second.");
        Assert.AreEqual(0, next.Left);
        Assert.IsFalse(taken.Wave.Items.Any(item => item.Ref == next.Wave.Items[0].Ref));
    }

    [TestMethod]
    public async Task A_wave_never_takes_a_held_successor_even_once_what_it_waits_on_merged()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        SuccessorTests.Write(workspace, "totals", First);
        await File.WriteAllTextAsync(Path.Combine(Successor.DirectoryOf(workspace.Root), "pause.md"), SuccessorTests.File(First, "held: 2026-11-26\n"));
        Record(workspace, SeriesRecord.Start(Run, _now, 3, ApprovalRequirement.None));
        FakeRepository git = OnSeries();
        git.Files[Branch + ":" + Folder(First) + "verify.md"] = "---\ncycle: " + First + "\noutcome: pass\n---\n\n# v\n";

        NextWave.Response taken = await Wave(workspace, git);

        Assert.IsNotNull(taken.Wave);
        CollectionAssert.AreEqual(new[] { "totals" }, taken.Wave.Items.Select(item => item.Ref).ToArray(), "The held one is a person's to start (D-220).");
    }

    [TestMethod]
    public async Task Under_approval_required_a_wave_takes_only_approved_cycles_no_earlier_wave_took()
    {
        using CycleWorkspace workspace = Approved();
        SuccessorTests.Write(workspace, "totals", workspace.Cycle.Id.ToString());
        Record(workspace, SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.Required));
        FakeRepository git = OnSeries();

        NextWave.Response taken = await Wave(workspace, git);

        Assert.AreEqual(SeriesItemKind.Approved, taken.Wave!.Items.Single().Kind);
        Assert.AreEqual(workspace.Cycle.Id.ToString(), taken.Wave.Items.Single().Cycle);
        StringAssert.Contains(NextWave.Render(taken), "build " + workspace.Cycle.Id);

        // The second wave does not take it again, and with nothing else approved the series is finished early.
        await WaveDone(workspace, git);
        NextWave.Response again = await Wave(workspace, git);

        Assert.IsNull(again.Wave);
        Assert.AreEqual(1, again.Record.Waves.Count, "Nothing was recorded.");
        StringAssert.Contains(NextWave.Render(again), "finished early");
    }

    [TestMethod]
    public async Task Under_approval_required_an_approved_parked_cycle_ready_against_the_series_branch_is_taken_back_and_built()
    {
        using CycleWorkspace workspace = Approved();
        Opened(workspace, Second);
        Park(workspace, First, Second);
        Record(workspace, SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.Required));

        // Waiting on a cycle that has not passed on the series branch: nothing to take.
        Assert.IsNull((await Wave(workspace, OnSeries())).Wave);

        FakeRepository git = OnSeries();
        git.Files[Branch + ":" + Folder(Second) + "verify.md"] = "---\ncycle: " + Second + "\noutcome: pass\n---\n\n# v\n";

        NextWave.Response taken = await Wave(workspace, git);

        SeriesItem item = taken.Wave!.Items.Single();
        Assert.AreEqual(SeriesItemKind.Parked, item.Kind);
        Assert.AreEqual(First, item.Cycle);
        string text = NextWave.Render(taken);
        StringAssert.Contains(text, First + " (approved, parked) -- sdlc unpark " + First + " --onto " + Branch + ", then build " + First);
    }

    [TestMethod]
    public void A_slug_lists_only_a_cycle_opened_once_its_wave_was_taken_and_only_while_the_item_names_no_cycle()
    {
        CycleId older = Parse("20261121_0800_00");
        CycleId newer = Parse(Second);
        CycleId other = Parse(Third);
        SeriesRecord unresolved = SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.None)
            .WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "totals", null)]);

        Assert.IsTrue(unresolved.Lists(newer, "successor:totals"));
        Assert.IsFalse(unresolved.Lists(older, "successor:totals"), "An older start of the same successor is not the one the series ran.");

        SeriesRecord resolved = SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.None)
            .WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "totals", Second)]);

        Assert.IsTrue(resolved.Lists(newer, "successor:totals"));
        Assert.IsFalse(resolved.Lists(other, "successor:totals"), "Once the item names its cycle, another start of the slug is not listed.");

        // Resolving skips the older start for the one opened once the wave was taken.
        SeriesRecord read = unresolved.Resolved(new Dictionary<string, IReadOnlyList<CycleId>>(StringComparer.Ordinal) { ["totals"] = [older, newer] });
        Assert.AreEqual(Second, read.Waves[0].Items[0].Cycle);
        Assert.IsNull(unresolved.Resolved(new Dictionary<string, IReadOnlyList<CycleId>>(StringComparer.Ordinal) { ["totals"] = [older] }).Waves[0].Items[0].Cycle);
    }

    [TestMethod]
    public async Task A_resolved_successor_is_not_taken_over_by_an_older_abandoned_start_when_the_record_is_read()
    {
        // An abandoned older start never resolves the slug, and a live one opened before the wave does not either.
        using CycleWorkspace workspace = Solution();
        Lead(workspace, "20261121_0800_00", "totals");
        Lead(workspace, Second, "totals");
        Record(workspace, SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.None).WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "totals", null)]));

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(OnSeries()));
        Result<SeriesStatus.Response> status = await gate.Send<SeriesStatus.Query, SeriesStatus.Response>(new SeriesStatus.Query());

        Assert.IsTrue(status.IsSuccess, string.Join("; ", status.Errors.Select(e => e.Message)));
        Assert.AreEqual(Second, status.Value.Items.Single().Item.Cycle);
    }

    [TestMethod]
    public async Task A_wave_is_refused_once_the_limit_is_reached_and_off_a_series_branch()
    {
        using CycleWorkspace workspace = Solution();
        Record(workspace, SeriesRecord.Start(Run, _now, 1, ApprovalRequirement.None).WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "totals", null)]).WithWaveDone(_now));

        Result<NextWave.Response> finished = await SendWave(workspace, OnSeries());

        Assert.IsTrue(finished.IsFailed);
        StringAssert.Contains(finished.Errors[0].Message, "is finished");

        Result<NextWave.Response> elsewhere = await SendWave(workspace, new FakeRepository { Branch = "development" });

        Assert.IsTrue(elsewhere.IsFailed);
        StringAssert.Contains(elsewhere.Errors[0].Message, "series/<id>, and this is development");
    }

    [TestMethod]
    public async Task A_wave_the_series_stopped_partway_through_is_handed_back_with_what_is_left_of_each_item_even_at_its_last_wave()
    {
        // Wave one of one: a successor never started, one started and planned no further, a parked cycle not yet taken
        // back, and one built. The limit is reached, yet the stopped wave comes back rather than "finished" (D-230).
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Lead(workspace, Third, "chart");
        Opened(workspace, Second);
        Park(workspace, Second, First);
        Lead(workspace, "20261121_1000_03", "audit");
        Verify(workspace, "20261121_1000_03", "pass");
        Record(workspace, SeriesRecord.Start(Run, _now, 1, ApprovalRequirement.None).WithWave(_now,
        [
            new SeriesItem(SeriesItemKind.Successor, "totals", null),
            new SeriesItem(SeriesItemKind.Successor, "chart", null),
            new SeriesItem(SeriesItemKind.Parked, Second, Second),
            new SeriesItem(SeriesItemKind.Successor, "audit", null),
        ]));
        FakeRepository git = OnSeries();
        git.Files[Branch + ":" + Folder(First) + "verify.md"] = "---\ncycle: " + First + "\noutcome: pass\n---\n\n# v\n";

        NextWave.Response open = await Wave(workspace, git);

        Assert.IsNotNull(open.Open);
        Assert.AreEqual(1, open.Wave!.Number);
        CollectionAssert.AreEqual(new[] { NextWave.NotStarted, SeriesStatus.BeingPlanned, NextWave.ParkedReady, "built: pass" }, open.Open.ToArray());

        string text = NextWave.Render(open);
        StringAssert.Contains(text, "Wave 1 of 1, on " + Branch + ", is not finished");
        StringAssert.Contains(text, "successor:totals -- /sdlc:intent --from successor:totals --unattended");
        StringAssert.Contains(text, "successor:chart -- cycle " + Third + ", being planned: to-plan takes it on");
        StringAssert.Contains(text, Second + " (parked) -- sdlc unpark " + Second + " --onto " + Branch);
        StringAssert.Contains(text, "built: pass: nothing left to do");
        StringAssert.Contains(text, "sdlc series wave --done");

        SeriesRecord written = (await SeriesRecord.Read(workspace.Root, Run, CancellationToken.None)).Value;
        Assert.AreEqual(1, written.Waves.Count, "Handing a wave back takes no new one.");
        Assert.AreEqual(Third, written.Waves[0].Items[1].Cycle, "What it resolved is written back.");

        // Recorded built, the series is finished as before.
        NextWave.Recorded done = await WaveDone(workspace, git);
        Assert.IsFalse(done.Already);
        StringAssert.Contains(NextWave.Render(done), "open the series' pull request");
        Result<NextWave.Response> finished = await SendWave(workspace, git);
        Assert.IsTrue(finished.IsFailed);
        StringAssert.Contains(finished.Errors[0].Message, "is finished");
    }

    [TestMethod]
    public async Task An_open_wave_with_nothing_left_in_it_is_recorded_built_as_the_next_is_taken()
    {
        // The series stopped between building the wave and recording it: nothing is lost, and nothing is done twice.
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        SuccessorTests.Write(workspace, "chart", First);
        Lead(workspace, Third, "totals");
        Verify(workspace, Third, "blocked");
        Record(workspace, SeriesRecord.Start(Run, _now, 3, ApprovalRequirement.None).WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "totals", null)]));
        FakeRepository git = OnSeries();
        git.Files[Branch + ":" + Folder(First) + "verify.md"] = "---\ncycle: " + First + "\noutcome: pass\n---\n\n# v\n";

        NextWave.Response taken = await Wave(workspace, git);

        Assert.IsNull(taken.Open, "A blocked verification is an outcome: its wave has nothing left.");
        Assert.AreEqual(2, taken.Wave!.Number);
        Assert.AreEqual(_now, taken.Record.Waves[0].Done);
        Assert.IsNull(taken.Record.Waves[1].Done);
    }

    [TestMethod]
    public async Task Recording_a_wave_built_is_refused_before_any_wave_and_off_a_series_branch_and_said_once()
    {
        using CycleWorkspace workspace = Solution();
        Record(workspace, SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.None));

        Result<NextWave.Recorded> none = await SendDone(workspace, OnSeries());
        Assert.IsTrue(none.IsFailed);
        StringAssert.Contains(none.Errors[0].Message, "has taken no wave yet");

        Result<NextWave.Recorded> elsewhere = await SendDone(workspace, new FakeRepository { Branch = "development" });
        Assert.IsTrue(elsewhere.IsFailed);
        StringAssert.Contains(elsewhere.Errors[0].Message, "series/<id>, and this is development");

        Record(workspace, SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.None).WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "totals", null)]));
        Assert.IsFalse((await WaveDone(workspace, OnSeries())).Already);

        NextWave.Recorded again = await WaveDone(workspace, OnSeries());
        Assert.IsTrue(again.Already);
        StringAssert.Contains(NextWave.Render(again), "was already recorded built");
        StringAssert.Contains(NextWave.Render(again), "'sdlc series wave' takes the next");
    }

    [TestMethod]
    public void A_wave_recorded_built_reads_back_and_an_older_one_reads_as_open()
    {
        SeriesRecord record = SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.None)
            .WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "totals", null)])
            .WithWaveDone(_now);

        string json = record.Write();
        StringAssert.Contains(json, "\"done\": \"2026-11-21T09:30:15+00:00\"");
        Assert.AreEqual(json, SeriesRecord.Parse(json, "series.json").Value.Write());
        Assert.AreSame(record, record.WithWaveDone(_now.AddHours(1)), "A wave already done keeps its time.");

        string older = System.Text.RegularExpressions.Regex.Replace(json, ",\\s*\"done\": \"[^\"]*\"", string.Empty);
        Assert.AreNotEqual(json, older);
        Assert.IsNull(SeriesRecord.Parse(older, "series.json").Value.Waves[0].Done);
    }

    [TestMethod]
    public async Task Nothing_ready_finishes_the_series_early_and_records_no_wave()
    {
        using CycleWorkspace workspace = Solution();
        Record(workspace, SeriesRecord.Start(Run, _now, 3, ApprovalRequirement.None));

        NextWave.Response taken = await Wave(workspace, OnSeries());

        Assert.IsNull(taken.Wave);
        Assert.AreEqual(0, (await SeriesRecord.Read(workspace.Root, Run, CancellationToken.None)).Value.Waves.Count);
    }

    [TestMethod]
    public async Task A_started_successor_resolves_to_its_cycle_when_the_status_reads_the_record()
    {
        using CycleWorkspace workspace = Solution();
        Record(workspace, SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.None).WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "totals", null)]));
        Lead(workspace, Third, "totals");
        Verify(workspace, Third, "blocked");
        FakeRepository git = OnSeries();

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<SeriesStatus.Response> status = await gate.Send<SeriesStatus.Query, SeriesStatus.Response>(new SeriesStatus.Query());

        Assert.IsTrue(status.IsSuccess, string.Join("; ", status.Errors.Select(e => e.Message)));
        Assert.AreEqual(Third, status.Value.Items.Single().Item.Cycle);
        Assert.AreEqual("blocked", status.Value.Items.Single().State);
        Assert.AreEqual(Third, (await SeriesRecord.Read(workspace.Root, Run, CancellationToken.None)).Value.Waves[0].Items[0].Cycle, "Written back.");

        string text = SeriesStatus.Render(status.Value);
        StringAssert.Contains(text, "successor:totals, cycle " + Third + " -- blocked");
        StringAssert.Contains(text, "Judged against " + Branch);
    }

    [TestMethod]
    public async Task Starting_a_series_refuses_fewer_than_one_wave_and_otherwise_opens_its_worktree_and_commits_its_record()
    {
        using CycleWorkspace workspace = Solution();
        FakeRepository git = new() { Branch = "development" };
        git.Refs.Add("origin/development");

        using var gate = Gate.For(workspace.Root, services => services
            .AddSingleton<IRepository>(git)
            .AddSingleton<TimeProvider>(new FakeClock(_now)));

        Result<StartSeries.Response> none = await gate.Send<StartSeries.Command, StartSeries.Response>(new StartSeries.Command(0));

        Assert.IsTrue(none.IsFailed);
        StringAssert.Contains(none.Errors[0].Message, "at least one wave");
        Assert.AreEqual(0, git.Ran.Count, "Nothing was opened.");

        Result<StartSeries.Response> empty = await gate.Send<StartSeries.Command, StartSeries.Response>(new StartSeries.Command(2, 0));

        Assert.IsTrue(empty.IsFailed);
        StringAssert.Contains(empty.Errors[0].Message, "at least one item");
        Assert.AreEqual(0, git.Ran.Count, "Nothing was opened.");

        Result<StartSeries.Response> started = await gate.Send<StartSeries.Command, StartSeries.Response>(new StartSeries.Command(2));

        Assert.IsTrue(started.IsSuccess, string.Join("; ", started.Errors.Select(e => e.Message)));
        Assert.AreEqual(Branch, started.Value.Branch);
        StringAssert.Contains(git.Ran.Single().Arguments, "worktree add --no-track -b " + Branch);
        StringAssert.EndsWith(git.Ran.Single().Arguments, " origin/development");
        StringAssert.Contains(started.Value.Worktree.Replace('\\', '/'), ".series/" + Run);
        Assert.AreEqual(".sdlc/series/" + Run + "/series.json", git.Commits.Single().Single());

        SeriesRecord record = (await SeriesRecord.Read(started.Value.Worktree, Run, CancellationToken.None)).Value;
        Assert.AreEqual(2, record.Limit);
        Assert.AreEqual(SeriesRecord.DefaultWaveSize, record.WaveSize, "Given no size, a wave takes the default.");
        Assert.AreEqual(ApprovalRequirement.Required, record.Approval, "Read from solution.md, whose default is required.");
        StringAssert.Contains(StartSeries.Render(started.Value), "of at most 3 items each, one after another");
        StringAssert.Contains(StartSeries.Render(started.Value), "builds only cycles a person has already approved");

        // The worktree's folder beside the scratch solution, <root>.series, and nothing above it.
        string series = Path.GetDirectoryName(started.Value.Worktree)!;
        Assert.AreEqual(Path.GetFullPath(workspace.Root) + ".series", series);
        Directory.Delete(series, recursive: true);
    }

    [TestMethod]
    public async Task A_series_report_has_a_section_per_wave_an_entry_per_cycle_what_stopped_and_what_each_id_says()
    {
        using CycleWorkspace workspace = Solution();
        Lead(workspace, First, "totals", "## Why\n\nFinance adds it up by hand (inferred).\n");
        Verify(workspace, First, "pass");
        Lead(workspace, Second, "chart-data");
        Verify(workspace, Second, "blocked");
        SuccessorTests.Write(workspace, "chart", Second);
        Lead(workspace, Third, "chart-axes");
        Park(workspace, Third, "successor:chart");
        Record(workspace, SeriesRecord.Start(Run, _now, 3, ApprovalRequirement.None)
            .WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "totals", First), new SeriesItem(SeriesItemKind.Successor, "export", null)])
            .WithWave(_now, [new SeriesItem(SeriesItemKind.Approved, Second, Second)]));

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(new FakeRepository { Branch = Branch }));
        Result<RunReport.Response> read = await gate.Send<RunReport.Query, RunReport.Response>(
            new RunReport.Query(RunKind.Series, Run, ["src/Thing.cs", Folder(First) + "verify.md", Folder(Second) + "verify.md"]));

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));
        string text = RunReport.Render(read.Value);

        StringAssert.StartsWith(text, "## Series " + Run);
        StringAssert.Contains(text, "2 cycles on " + Branch + ": 1 passed, 1 blocked.");
        StringAssert.Contains(text, "### Wave 1");
        StringAssert.Contains(text, "Forecast: successor:totals, successor:export.");
        StringAssert.Contains(text, "Did not run: successor:export.");
        StringAssert.Contains(text, "### Wave 2");
        StringAssert.Contains(text, "#### " + First);
        StringAssert.Contains(text, "Outcome: pass. From successor:totals.");
        StringAssert.Contains(text, "**Not confirmed by anyone**");
        StringAssert.Contains(text, "Finance adds it up by hand (inferred).");
        StringAssert.Contains(text, "### What stopped");
        StringAssert.Contains(text, "- " + Second + " (blocked): held back successor:chart, " + Third + " (parked).");
        StringAssert.Contains(text, "Named:");
        StringAssert.Contains(text, "successor:chart — Finance sees the month's totals beside the export.");
        Assert.IsTrue(text.IndexOf("### Wave 1", StringComparison.Ordinal) < text.IndexOf("#### " + First, StringComparison.Ordinal));
        Assert.IsTrue(text.IndexOf("### Wave 2", StringComparison.Ordinal) < text.IndexOf("#### " + Second, StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("## Report", StringComparison.Ordinal), "No cycle's own report is printed.");
        Assert.IsFalse(read.Value.Unsound);
    }

    [TestMethod]
    public async Task A_rejected_series_abandons_every_cycle_its_record_lists_and_they_are_completed_for_harvest()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Lead(workspace, Third, "totals");
        Opened(workspace, Second);
        Record(workspace, SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.None)
            .WithWave(_now, [new SeriesItem(SeriesItemKind.Parked, First, First), new SeriesItem(SeriesItemKind.Successor, "totals", null)]));

        Cycle first = new(Parse(First), workspace.Root);
        Cycle second = new(Parse(Second), workspace.Root);
        Cycle third = new(Parse(Third), workspace.Root);

        Assert.IsFalse(first.IsAbandoned, "Not until a person places the abandonment.");

        CycleWorkspace.WriteRaw(SeriesRecord.AbandonedPathIn(workspace.Root, Run), "---\nseries: " + Run + "\nby: Peter Almanzar\nat: 2026-11-21T12:00:00Z\n---\n\n# Why this series stopped\n\nThe chart was the wrong shape.\n");

        Assert.IsTrue(first.IsAbandoned, "Named by id.");
        Assert.IsTrue(third.IsAbandoned, "Named as the successor its intent records as its lead.");
        Assert.IsFalse(second.IsAbandoned, "The series never listed it.");
        Assert.AreEqual(SeriesRecord.AbandonedPathIn(workspace.Root, Run), first.AbandonmentPath);
        Assert.IsTrue(await Completion.IsFinished(third, CancellationToken.None));
        Assert.IsFalse(await Completion.IsFinished(second, CancellationToken.None));
    }

    [TestMethod]
    public async Task A_rejected_series_does_not_abandon_a_cycle_it_took_from_the_integration_branch()
    {
        // Approved before the series started, so it came from the integration branch: it stays as it stands there,
        // to be built again, even in a working copy that holds the rejected series' folder beside it.
        const string Before = "20261120_0900_00";
        using CycleWorkspace workspace = Solution();
        Opened(workspace, Before);
        Opened(workspace, First);
        Record(workspace, SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.Required)
            .WithWave(_now, [new SeriesItem(SeriesItemKind.Approved, Before, Before), new SeriesItem(SeriesItemKind.Parked, First, First)]));
        CycleWorkspace.WriteRaw(SeriesRecord.AbandonedPathIn(workspace.Root, Run), "---\nseries: " + Run + "\nby: Peter Almanzar\nat: 2026-11-21T12:00:00Z\n---\n\n# Why this series stopped\n\nThe chart was the wrong shape.\n");

        Assert.IsFalse(new Cycle(Parse(Before), workspace.Root).IsAbandoned);
        Assert.IsTrue(new Cycle(Parse(First), workspace.Root).IsAbandoned);
        Assert.IsTrue(new Cycle(Parse(Before), workspace.Root).InSeries, "It is still one the series ran.");
        Assert.IsFalse(await Completion.IsFinished(new Cycle(Parse(Before), workspace.Root), CancellationToken.None), "Nor is it completed for a harvest.");
    }

    [TestMethod]
    public async Task A_series_abandonment_draft_lists_only_the_cycles_it_covers_and_names_the_rest()
    {
        const string Before = "20261120_0900_00";
        const string Merged = "20261121_1000_05";
        using CycleWorkspace workspace = Solution();
        Record(workspace, SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.Required)
            .WithWave(_now, [new SeriesItem(SeriesItemKind.Approved, Before, Before), new SeriesItem(SeriesItemKind.Parked, First, First), new SeriesItem(SeriesItemKind.Approved, Merged, Merged)]));
        FakeRepository git = OnSeries();
        git.Refs.Add("development");
        git.PathsAt.Add("development:.sdlc/cycles/" + Merged);

        using var gate = Gate.For(workspace.Root, services => services
            .AddSingleton<IRepository>(git)
            .AddSingleton<TimeProvider>(new FakeClock(_now)));
        Result<DraftResult> drafted = await gate.Send<AbandonText.SeriesQuery, DraftResult>(new AbandonText.SeriesQuery(Run, "Peter Almanzar"));

        Assert.IsTrue(drafted.IsSuccess && drafted.Value.MayDraft, drafted.ValueOrDefault?.Instructions);
        StringAssert.Contains(drafted.Value.Entry, "  - " + First);
        Assert.IsFalse(drafted.Value.Entry!.Contains(Before, StringComparison.Ordinal), "Opened before the series started.");
        Assert.IsFalse(drafted.Value.Entry.Contains(Merged, StringComparison.Ordinal), "Its folder is on the integration branch.");
        StringAssert.Contains(drafted.Value.Instructions, Before + ", " + Merged + " came from the integration branch and are not stopped");
    }

    [TestMethod]
    public async Task A_series_abandonment_at_a_ref_skips_a_cycle_on_the_integration_branch_and_lists_the_series_once_per_request()
    {
        const string Merged = "20261121_1000_05";
        using CycleWorkspace workspace = Solution();
        FakeRepository git = new();
        git.Refs.Add("development");
        git.Refs.Add(Branch);
        git.PathsAt.Add(Branch + ":.sdlc/series/" + Run + "/abandoned.md");
        git.Files[Branch + ":.sdlc/series/" + Run + "/series.json"] = SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.None)
            .WithWave(_now, [new SeriesItem(SeriesItemKind.Parked, First, First), new SeriesItem(SeriesItemKind.Parked, Merged, Merged)])
            .Write();
        git.PathsAt.Add("development:.sdlc/cycles/" + Merged);

        Assert.AreEqual(Branch, Completion.AbandonedOn(git, workspace.Root, "development", Parse(First)));
        Assert.IsNull(Completion.AbandonedOn(git, workspace.Root, "development", Parse(Merged)), "Its folder is on the integration branch, so the series does not abandon it.");

        // A harvest asks it of every cycle a branch names: the series branches are listed for the request, not per cycle.
        foreach (string id in new[] { Second, Third, "20261121_1000_03", "20261121_1000_04" })
        {
            git.Refs.Add("cycle/" + id + "/build");
        }

        git.ListedBy.Clear();
        Result<GatherAbandoned.Response> gathered = await Send<GatherAbandoned.Command, GatherAbandoned.Response>(workspace, git, new GatherAbandoned.Command());

        Assert.IsTrue(gathered.IsSuccess, string.Join("; ", gathered.Errors.Select(e => e.Message)));
        Assert.IsTrue(git.ListedBy.GetValueOrDefault(RunBranch.SeriesPrefix) <= 2, "Listed " + git.ListedBy.GetValueOrDefault(RunBranch.SeriesPrefix) + " times.");
    }

    [TestMethod]
    public async Task A_series_abandonment_drafted_for_a_person_reads_back_once_they_write_a_reason()
    {
        using CycleWorkspace workspace = Solution();
        Record(workspace, SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.None).WithWave(_now, [new SeriesItem(SeriesItemKind.Parked, First, First)]));

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<TimeProvider>(new FakeClock(_now)));
        Result<DraftResult> drafted = await gate.Send<AbandonText.SeriesQuery, DraftResult>(new AbandonText.SeriesQuery(Run, "Peter Almanzar"));

        Assert.IsTrue(drafted.IsSuccess && drafted.Value.MayDraft, drafted.ValueOrDefault?.Instructions);
        string entry = drafted.Value.Entry!;
        StringAssert.Contains(entry, "series: " + Run);
        StringAssert.Contains(entry, "  - " + First);
        StringAssert.Contains(entry, "# Why this series stopped");
        StringAssert.Contains(drafted.Value.Instructions, ".sdlc");

        Assert.IsTrue(AbandonRecord.Read(entry, "abandoned.md").IsFailed, "Placed as drafted, it says nothing about why.");

        Result<AbandonRecord> placed = AbandonRecord.Read(entry + "\nThe chart was the wrong shape.\n", "abandoned.md");
        Assert.IsTrue(placed.IsSuccess, string.Join("; ", placed.Errors.Select(e => e.Message)));
        Assert.AreEqual(Run, placed.Value.Series);
        Assert.AreEqual("Peter Almanzar", placed.Value.By);

        // Placed, a second draft is refused.
        CycleWorkspace.WriteRaw(SeriesRecord.AbandonedPathIn(workspace.Root, Run), entry + "\nThe chart was the wrong shape.\n");
        Assert.IsFalse((await gate.Send<AbandonText.SeriesQuery, DraftResult>(new AbandonText.SeriesQuery(Run, "Peter Almanzar"))).Value.MayDraft);
    }

    [TestMethod]
    public void The_series_process_is_asked_for_its_playbook_unattended_with_every_skill_a_wave_and_a_build_run()
    {
        IReadOnlyList<string> arguments = SeriesRun.ArgumentsFor(Run, "/plugin", ["make"]);

        Assert.AreEqual("/sdlc:series " + Run + " --unattended", arguments[3]);

        foreach (string skill in new[] { "intent", "to-plan", "spec", "plan", "interrogate", "refine", "build", "implement", "verify", "remediate", "pr", "resolve-conflict" })
        {
            CollectionAssert.Contains(arguments.ToList(), "Skill(sdlc:" + skill + ")");
        }

        CollectionAssert.Contains(arguments.ToList(), "Bash(make *)");
        CollectionAssert.DoesNotContain(arguments.ToList(), "--permission-mode");
        Assert.AreEqual("/sdlc:build --run --unattended", BuildRun.PromptFor(null, run: true));
    }

    [TestMethod]
    public void A_waves_branch_opens_from_its_sandbox_beside_the_repository()
    {
        CycleId sandbox = Parse(First);
        string root = Path.Combine(Path.GetTempPath(), "shop");

        Assert.AreEqual("git worktree add --no-track -b wave/" + First + " ../shop.waves/" + First + " sandbox/" + First, RunBranch.OpenWave(root, sandbox, exists: false));
        Assert.AreEqual("git worktree add ../shop.waves/" + First + " wave/" + First, RunBranch.OpenWave(root, sandbox, exists: true));
    }

    [TestMethod]
    public async Task An_intent_a_series_lists_keeps_its_inferred_marks_and_any_other_intent_is_still_refused_them()
    {
        // A series asks nobody, so the marks stay for the run's report. The chain reads shape the way the status
        // panel does, so both let them stand for a cycle a series lists and for no other.
        using var workspace = CycleWorkspace.Create(First);
        workspace.WriteArtifact(ArtifactKind.Intent, "The totals reach finance by noon. (inferred)");
        string text = await File.ReadAllTextAsync(workspace.Cycle.PathOf(ArtifactKind.Intent));

        Assert.IsFalse(workspace.Cycle.InSeries);
        Assert.IsFalse(ArtifactSchema.Validate(ArtifactKind.Intent, text, workspace.Cycle.Id, unasked: workspace.Cycle.InSeries).IsValid);
        Assert.IsFalse(Shape(await Chain(workspace)).Passed, "Outside a series a mark left behind is refused.");

        // Listed by a series as a successor it took: the intent's lead names the slug.
        CycleWorkspace.WriteRaw(workspace.Cycle.PathOf(ArtifactKind.Intent), text.Replace("type: feature addition", "type: feature addition\nlead: successor:totals", StringComparison.Ordinal));
        Record(workspace, SeriesRecord.Start(Run, _now, 2, ApprovalRequirement.None).WithWave(_now, [new SeriesItem(SeriesItemKind.Successor, "totals", null)]));

        Assert.IsTrue(workspace.Cycle.InSeries);
        VerifyChain.ChainCheck shape = Shape(await Chain(workspace));
        Assert.IsTrue(shape.Passed, shape.Detail);

        // Another cycle in the same working copy is still held to it.
        Assert.IsFalse(new Cycle(Parse(Second), workspace.Root).InSeries);
    }

    [TestMethod]
    public async Task A_successor_whose_predecessor_passed_only_on_the_run_branch_is_ready_there_or_onto_it_and_nowhere_else()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Opened(workspace, Second);
        SuccessorTests.Write(workspace, "totals", First);
        FakeRepository onSeries = OnSeries();
        onSeries.Files[Branch + ":" + Folder(First) + "verify.md"] = "---\ncycle: " + First + "\noutcome: pass\n---\n\n# v\n";

        FakeRepository elsewhere = new();
        elsewhere.Refs.Add(Branch);
        foreach (KeyValuePair<string, string> file in onSeries.Files)
        {
            elsewhere.Files[file.Key] = file.Value;
        }

        LeadReference totals = new(LeadSource.Successor, "totals");

        Assert.IsTrue((await Send<ReadLead.Query, ReadLead.Response>(workspace, elsewhere, new ReadLead.Query(totals))).IsFailed, "Off the run branch, the integration branch judges, and it has not merged there.");
        Result<ReadLead.Response> shown = await Send<ReadLead.Query, ReadLead.Response>(workspace, onSeries, new ReadLead.Query(totals));
        Assert.IsTrue(shown.IsSuccess, string.Join("; ", shown.Errors.Select(e => e.Message)));
        Result<ReadLead.Response> named = await Send<ReadLead.Query, ReadLead.Response>(workspace, elsewhere, new ReadLead.Query(totals, Branch));
        Assert.IsTrue(named.IsSuccess, string.Join("; ", named.Errors.Select(e => e.Message)));

        Assert.IsTrue((await Send<StartSuccessor.Command, StartSuccessor.Response>(workspace, elsewhere, new StartSuccessor.Command("totals", Parse(Second)))).IsFailed);
        Result<StartSuccessor.Response> started = await Send<StartSuccessor.Command, StartSuccessor.Response>(workspace, onSeries, new StartSuccessor.Command("totals", Parse(Second)));
        Assert.IsTrue(started.IsSuccess, string.Join("; ", started.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public async Task On_a_run_branch_the_cycles_ready_to_build_are_the_approved_unbuilt_ones_it_carries()
    {
        // The wave skill under approval: required, and the build skill's run form, ask this on wave/<id> and take
        // what it lists; a cycle already on the integration branch is not the run's.
        using CycleWorkspace workspace = Approved();
        Opened(workspace, Second);
        Cycle merged = new(Parse(Third), workspace.Root);
        Directory.CreateDirectory(merged.Directory);
        CycleWorkspace.WriteRaw(merged.PathOf(ArtifactKind.Intent), await File.ReadAllTextAsync(workspace.Cycle.PathOf(ArtifactKind.Intent)));

        FakeRepository git = new() { Branch = "wave/" + First };
        git.Refs.Add("origin/development");
        git.PathsAt.Add("origin/development:" + Folder(Third) + "intent.md");

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<ResolveCycle.Response> resolved = await gate.Send<ResolveCycle.Query, ResolveCycle.Response>(new ResolveCycle.Query(NextStep.Build));

        Assert.IsTrue(resolved.IsSuccess, string.Join("; ", resolved.Errors.Select(e => e.Message)));
        CollectionAssert.AreEquivalent(new[] { First, Second }, resolved.Value.Carried.Select(candidate => candidate.Id.ToString()).ToArray(), "What the integration branch already holds is not carried.");
        Assert.AreEqual(Parse(First), resolved.Value.Only, "Only the approved cycle is ready to build; the unplanned one is not.");
    }

    private static async Task<Result<TResponse>> Send<TRequest, TResponse>(CycleWorkspace workspace, FakeRepository git, TRequest request)
        where TRequest : MofonCode.Core.IRequest<TResponse>
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        return await gate.Send<TRequest, TResponse>(request);
    }

    private static async Task<VerifyChain.Response> Chain(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);
        Result<VerifyChain.Response> result = await gate.Send<VerifyChain.Query, VerifyChain.Response>(new VerifyChain.Query(workspace.Cycle.Id));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static VerifyChain.ChainCheck Shape(VerifyChain.Response verdict) =>
        verdict.Checks.Single(check => check.Name == "every artifact's shape is sound");

    private static CycleWorkspace Solution() => CycleWorkspace.Create("20261120_0800_00");

    private static CycleWorkspace Approved()
    {
        var workspace = CycleWorkspace.Create(First);
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

    private static void Opened(CycleWorkspace workspace, string id) =>
        Directory.CreateDirectory(new Cycle(Parse(id), workspace.Root).Directory);

    private static void Park(CycleWorkspace workspace, string id, string waitsOn) =>
        CycleWorkspace.WriteRaw(Parked.PathOf(new Cycle(Parse(id), workspace.Root)), "---\nwaits_on: " + waitsOn + "\n---\n\nIt needs what " + waitsOn + " creates.\n");

    /// <summary>A cycle started from a successor: its intent records the slug as its lead.</summary>
    private static void Lead(CycleWorkspace workspace, string id, string slug, string body = "") =>
        CycleWorkspace.WriteRaw(
            new Cycle(Parse(id), workspace.Root).PathOf(ArtifactKind.Intent),
            "---\ncycle: " + id + "\ntype: feature addition\nlead: successor:" + slug + "\n---\n\n# Finance sees the totals\n\n" + body);

    private static void Verify(CycleWorkspace workspace, string id, string outcome) =>
        CycleWorkspace.WriteRaw(
            new Cycle(Parse(id), workspace.Root).PathOf(ArtifactKind.Verify),
            "---\ncycle: " + id + "\noutcome: " + outcome + "\n---\n\n# v\n");

    private static void Record(CycleWorkspace workspace, SeriesRecord record) =>
        CycleWorkspace.WriteRaw(SeriesRecord.PathIn(workspace.Root, record.Id), record.Write());

    private static FakeRepository OnSeries()
    {
        FakeRepository git = new() { Branch = Branch };
        git.Refs.Add(Branch);
        return git;
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

    private static async Task<NextWave.Recorded> WaveDone(CycleWorkspace workspace, FakeRepository git)
    {
        Result<NextWave.Recorded> result = await SendDone(workspace, git);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static async Task<Result<NextWave.Recorded>> SendDone(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services
            .AddSingleton<IRepository>(git)
            .AddSingleton<TimeProvider>(new FakeClock(_now)));

        return await gate.Send<NextWave.Done, NextWave.Recorded>(new NextWave.Done());
    }

    private static string Folder(string id) => ".sdlc/cycles/" + id + "/";

    private static CycleId Parse(string id)
    {
        Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed));
        return parsed.Value;
    }
}
