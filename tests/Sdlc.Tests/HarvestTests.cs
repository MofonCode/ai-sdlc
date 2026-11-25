using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Drafts;
using Sdlc.Core.Git;
using Sdlc.Core.Harvest;
using Sdlc.Core.Hooks;
using Sdlc.Core.Proposals;
using Sdlc.Core.Successors;

namespace Sdlc.Tests;

/// <summary>
/// Harvesting a completed cycle (D-189..D-192): which cycles are read, how each aspect is answered on its own, when a
/// cycle may close, and what still reads it once its folder has gone (D-152).
/// </summary>
/// <remarks>
/// The failures guarded against are a cycle read before a person accepted it, and a folder deleted with something
/// still needing it: an aspect nobody answered, a count toward the bar, a successor waiting on it.
/// </remarks>
[TestClass]
public sealed class HarvestTests
{
    private const string Integration = "origin/development";

    [TestMethod]
    public void The_ledger_reads_back_what_it_wrote_and_says_which_aspects_each_cycle_has_answered()
    {
        HarvestLedger ledger = HarvestLedger.Empty
            .With(Sitting(HarvestAspect.Decisions, "20261001_0900_01"), [new Watched("the log level is missing", ["20261001_0900_01"])])
            .With(Sitting(HarvestAspect.Constraints, "20261001_0900_01"), [new Watched("the log level is missing", ["20261002_0900_01"])])
            .Closing([Husk("20261002_0900_01")]);

        HarvestLedger read = HarvestLedger.Parse(ledger.Write(), HarvestLedger.FileName).Value;

        Assert.AreEqual("abc123", read.Find("20261002_0900_01")!.Merge);
        Assert.AreEqual(1, read.Watch.Count, "One concern, watched across sittings.");
        CollectionAssert.AreEquivalent(new[] { "20261001_0900_01", "20261002_0900_01" }, read.Watch[0].Cycles.ToArray());
        CollectionAssert.AreEquivalent(new[] { HarvestAspect.Constraints, HarvestAspect.Decisions }, read.Answered("20261001_0900_01").ToArray());
        CollectionAssert.AreEqual(
            new[] { HarvestAspect.Suggestions, HarvestAspect.Stack, HarvestAspect.Areas },
            read.Missing("20261001_0900_01").ToArray(),
            "The aspects left, in the order the playbook runs them.");
        StringAssert.Contains(ledger.Write(), "\"aspect\": \"decisions\"", "The ledger names an aspect as a person types it.");
    }

    [TestMethod]
    public async Task A_cycle_in_flight_is_not_read_and_one_that_passed_verify_waits_until_it_merges()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        HarvestReport.Response inFlight = await Report(workspace, new FakeRepository());
        Assert.AreEqual(0, inFlight.Open.Count, "A cycle in flight is nobody's to harvest.");
        Assert.AreEqual(0, inFlight.Unmerged.Count);

        workspace.WriteVerify("pass");
        HarvestReport.Response unmerged = await Report(workspace, new FakeRepository());
        Assert.AreEqual(0, unmerged.Open.Count, "Passing verify is not acceptance: the pull request is (D-181).");
        CollectionAssert.AreEqual(new[] { workspace.Cycle.Id }, unmerged.Unmerged.ToArray());
        StringAssert.Contains(HarvestReport.RenderOverview(unmerged), "passed verify but is not merged into development yet");

        HarvestReport.Response merged = await Report(workspace, Merged(workspace));
        HarvestReport.OpenCycle open = merged.Open.Single();
        CollectionAssert.AreEqual(HarvestAspects.All.ToArray(), open.Missing.ToArray(), "Every aspect is still to answer.");
        StringAssert.Contains(HarvestReport.RenderOverview(merged), "Next: 'sdlc harvest constraints'");
    }

    [TestMethod]
    public async Task An_abandoned_cycle_is_completed_whether_or_not_any_of_it_merged()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.Abandon();

        HarvestReport.OpenCycle merged = (await Report(workspace, Merged(workspace))).Open.Single();
        HarvestReport.OpenCycle never = (await Report(workspace, new FakeRepository())).Open.Single();

        Assert.IsTrue(merged.Abandoned);
        Assert.IsTrue(never.Abandoned, "An abandonment is the person ending the cycle; it waits for no merge (D-190).");
    }

    [TestMethod]
    public async Task An_abandoned_cycle_that_never_merged_is_brought_in_from_its_branch()
    {
        using var workspace = CycleWorkspace.Create();
        string id = workspace.Cycle.Id.ToString();
        string branch = "cycle/" + id + "/plan";
        FakeRepository git = Merged(workspace, branch: "development");
        git.Refs.Add(branch);
        git.PathsAt.Add(branch + ":.sdlc/cycles/" + id + "/abandoned.md");

        GatherAbandoned.Response gathered = await Gather(workspace, git);

        Assert.AreEqual(branch, gathered.Cycles.Single().Branch);
        Assert.IsTrue(git.Ran.Any(run => run.Arguments == "restore --source=\"" + branch + "\" --worktree -- \".sdlc/cycles/" + id + "\""), "Restored untracked: nothing checked out, nothing staged.");

        git.PathsAt.Add(Integration + ":.sdlc/cycles/" + id + "/abandoned.md");
        Assert.AreEqual(0, (await Gather(workspace, git)).Cycles.Count, "An abandonment on the integration branch is read from there.");

        git.PathsAt.Remove(Integration + ":.sdlc/cycles/" + id + "/abandoned.md");
        await WriteLedger(workspace, Husk(id));
        Assert.AreEqual(0, (await Gather(workspace, git)).Cycles.Count, "A closed cycle is not brought back.");
    }

    [TestMethod]
    public async Task A_cycle_read_from_its_branch_closes_at_its_branch_and_leaves_the_working_tree()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.Abandon();
        string id = workspace.Cycle.Id.ToString();
        string branch = "cycle/" + id + "/plan";
        FakeRepository git = new() { Branch = "development" };
        git.Refs.Add(Integration);
        git.Refs.Add(branch);
        git.PathsAt.Add(branch + ":.sdlc/cycles/" + id + "/abandoned.md");

        Assert.AreEqual(branch, (await Report(workspace, git)).Open.Single().Branch);

        foreach (HarvestAspect aspect in HarvestAspects.All)
        {
            Assert.IsTrue((await Record(workspace, git, aspect, await Proposals(workspace, string.Empty))).IsSuccess);
        }

        CloseHarvest.Response closed = (await Close(workspace, git)).Value;

        Assert.AreEqual(branch, closed.Closed.Single().Merge, "Its folder is in git at its branch's last commit, never on the integration branch.");
        CollectionAssert.AreEqual(new[] { branch }, closed.ReadFrom.ToArray());
        Assert.IsTrue(git.Ran.Any(run => run.Arguments.StartsWith("rm -r -q -f --ignore-unmatch -- ", StringComparison.Ordinal)));
        Assert.IsFalse(Directory.Exists(workspace.Cycle.Directory), "What git never tracked is deleted from disk.");
        StringAssert.Contains(HarvestRenderer.RenderClosed(closed), "'sdlc tidy' lists the branch");
    }

    [TestMethod]
    public async Task A_sitting_is_not_recorded_while_its_draft_is_still_beside_its_file()
    {
        using CycleWorkspace workspace = Completed();
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", "decisions.draft.md"), "# d\n");
        string proposals = await Proposals(workspace, string.Empty);

        Result<RecordHarvest.Response> refused = await Record(workspace, Merged(workspace), HarvestAspect.Decisions, proposals);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "decisions.draft.md");
        Assert.IsTrue((await Record(workspace, Merged(workspace), HarvestAspect.Stack, proposals)).IsSuccess, "Another aspect's draft is not this one's to wait for.");
    }

    [TestMethod]
    public async Task Recording_an_aspect_answers_it_for_the_sitting_and_the_same_aspect_cannot_be_answered_twice()
    {
        using CycleWorkspace workspace = Completed();
        string proposals = await Proposals(workspace, "## Watch\n\n- the log level is missing — " + workspace.Cycle.Id + "\n");

        RecordHarvest.Response recorded = (await Record(workspace, Merged(workspace), HarvestAspect.Constraints, proposals)).Value;

        CollectionAssert.AreEqual(new[] { workspace.Cycle.Id }, recorded.Recorded.ToArray());
        HarvestLedger ledger = (await HarvestLedger.Read(workspace.Root, CancellationToken.None)).Value;
        Assert.AreEqual(HarvestAspect.Constraints, ledger.Sittings.Single().Aspect);
        Assert.AreEqual("the log level is missing", ledger.Watch.Single().Gist);
        Assert.IsNull(ledger.Find(workspace.Cycle.Id.ToString()), "Recording one aspect closes nothing.");
        StringAssert.Contains(HarvestRenderer.RenderRecorded(recorded), "still needs decisions, suggestions, stack, areas");

        Result<RecordHarvest.Response> again = await Record(workspace, Merged(workspace), HarvestAspect.Constraints, proposals);
        Assert.IsTrue(again.IsFailed);
        StringAssert.Contains(again.Errors[0].Message, "No completed cycle still needs constraints");
    }

    [TestMethod]
    public async Task An_aspects_report_reads_only_the_cycles_that_have_not_answered_it()
    {
        using CycleWorkspace workspace = Completed();
        await Record(workspace, Merged(workspace), HarvestAspect.Decisions, await Proposals(workspace, string.Empty));

        HarvestReport.Response decisions = await Report(workspace, Merged(workspace), HarvestAspect.Decisions);
        HarvestReport.Response stack = await Report(workspace, Merged(workspace), HarvestAspect.Stack);

        Assert.AreEqual(0, decisions.Sitting.Count);
        StringAssert.Contains(HarvestReport.Render(decisions, "proposed.md"), "every completed cycle has answered it");
        CollectionAssert.AreEqual(new[] { workspace.Cycle.Id }, stack.Sitting.ToArray());
        StringAssert.Contains(HarvestReport.Render(stack, "proposed.md"), "'sdlc harvest record stack proposed.md'");
    }

    [TestMethod]
    public async Task Close_refuses_until_every_aspect_is_answered_and_names_what_is_missing()
    {
        using CycleWorkspace workspace = Completed();
        FakeRepository git = Merged(workspace, branch: "development");
        await Record(workspace, git, HarvestAspect.Constraints, await Proposals(workspace, string.Empty));

        Result<CloseHarvest.Response> refused = await Close(workspace, git);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, workspace.Cycle.Id + " still needs decisions, suggestions, stack, areas");
        Assert.AreEqual(0, git.Created.Count, "Nothing is branched while nothing can close.");
    }

    [TestMethod]
    public async Task Close_records_the_husk_and_commits_its_removal_on_a_branch_of_its_own()
    {
        using CycleWorkspace workspace = Completed();
        FakeRepository git = Merged(workspace, branch: "development");

        foreach (HarvestAspect aspect in HarvestAspects.All)
        {
            Assert.IsTrue((await Record(workspace, git, aspect, await Proposals(workspace, string.Empty))).IsSuccess);
        }

        CloseHarvest.Response closed = (await Close(workspace, git)).Value;

        HarvestedCycle husk = closed.Closed.Single();
        Assert.AreEqual("merged", husk.Merge, "The newest commit on the integration branch that touched the folder.");
        StringAssert.StartsWith(closed.Branch, CloseHarvest.BranchPrefix);
        CollectionAssert.AreEqual(new[] { closed.Branch }, git.Created);
        Assert.IsTrue(git.Ran.Any(run => run.Arguments.Contains("rm -r -q --ignore-unmatch -- \".sdlc/cycles/" + workspace.Cycle.Id + "\"", StringComparison.Ordinal)));
        CollectionAssert.Contains(git.Commits.Single().ToArray(), ".sdlc/" + HarvestLedger.FileName);
        Assert.IsNotNull((await HarvestLedger.Read(workspace.Root, CancellationToken.None)).Value.Find(workspace.Cycle.Id.ToString()));
        StringAssert.Contains(HarvestRenderer.RenderClosed(closed), "git push -u origin " + closed.Branch);
    }

    [TestMethod]
    public async Task Close_runs_from_the_integration_branch_and_only_once_every_draft_is_placed()
    {
        using CycleWorkspace workspace = Completed();
        FakeRepository elsewhere = Merged(workspace, branch: "cycle/" + workspace.Cycle.Id);

        foreach (HarvestAspect aspect in HarvestAspects.All)
        {
            await Record(workspace, elsewhere, aspect, await Proposals(workspace, string.Empty));
        }

        Result<CloseHarvest.Response> offBranch = await Close(workspace, elsewhere);
        Assert.IsTrue(offBranch.IsFailed);
        StringAssert.Contains(offBranch.Errors[0].Message, "Close from development");

        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", "areas.draft.md"), "# a\n");
        Result<CloseHarvest.Response> drafted = await Close(workspace, Merged(workspace, branch: "development"));
        Assert.IsTrue(drafted.IsFailed);
        StringAssert.Contains(drafted.Errors[0].Message, "areas.draft.md");
    }

    [TestMethod]
    public void An_agent_writing_the_ledger_is_denied_by_path_and_by_command()
    {
        Assert.IsTrue(ProtectedPaths.IsProtected(".sdlc/harvested.json", out ProtectedFile? byPath));
        StringAssert.Contains(byPath!.Reason, "sdlc harvest record");

        Assert.IsTrue(ProtectedPaths.CommandTouchesProtected("echo {} > .sdlc/harvested.json", out ProtectedFile? byRedirect));
        Assert.AreSame(ProtectedPaths.Harvested, byRedirect);
        Assert.IsTrue(ProtectedPaths.CommandTouchesProtected("cp other.json .sdlc/harvested.json", out _));
        Assert.IsFalse(ProtectedPaths.CommandTouchesProtected("cat .sdlc/harvested.json", out _), "Reading it is allowed.");
        Assert.IsFalse(ProtectedPaths.CommandTouchesProtected("sdlc harvest decisions", out _), "Naming an aspect is not naming its file.");
    }

    [TestMethod]
    public void An_agent_writing_a_series_record_is_denied_by_path_and_by_command_and_may_still_commit_it()
    {
        Assert.IsTrue(ProtectedPaths.IsProtected(".sdlc/series/20261121_0930_15/series.json", out ProtectedFile? byPath));
        Assert.AreSame(ProtectedPaths.SeriesRecord, byPath);
        StringAssert.Contains(byPath!.Reason, "sdlc series wave");

        Assert.IsTrue(ProtectedPaths.CommandTouchesProtected("echo {} > .sdlc/series/20261121_0930_15/series.json", out ProtectedFile? byRedirect));
        Assert.AreSame(ProtectedPaths.SeriesRecord, byRedirect);
        Assert.IsTrue(ProtectedPaths.CommandTouchesProtected("cp other.json .sdlc/series/20261121_0930_15/series.json", out _));
        Assert.IsFalse(ProtectedPaths.CommandTouchesProtected("cat .sdlc/series/20261121_0930_15/series.json", out _), "Reading it is allowed.");

        // A solution's own series.json -- a chart's data -- is not the record: only a command naming a series' folder is.
        Assert.IsFalse(ProtectedPaths.CommandTouchesProtected("cp fixtures/prices.json src/charts/series.json", out _));
        Assert.IsFalse(ProtectedPaths.CommandTouchesProtected("echo {} > series.json", out _));

        // Committing what the binary wrote is how the series playbook keeps it, as the harvest ledger is kept.
        foreach (string ledger in new[] { ".sdlc/series/20261121_0930_15/series.json", ".sdlc/harvested.json" })
        {
            Assert.IsFalse(ProtectedPaths.CommandTouchesProtected("git add " + ledger, out _), ledger);
            Assert.IsFalse(ProtectedPaths.CommandTouchesProtected("git add .sdlc && git commit -m \"Series 20261121_0930_15: wave 1 built\"", out _), ledger);
        }
    }

    [TestMethod]
    [DataRow(".sdlc/decisions.md", true)]
    [DataRow(".sdlc/harvested.json", true)]
    [DataRow(".sdlc/framework-harvest.json", true)]
    [DataRow(".sdlc/areas.md", true)]
    [DataRow(".sdlc/successors/import-trades.md", true)]
    [DataRow(".sdlc/cycles/20261001_0900_01/plan.md", true)]
    [DataRow(".sdlc/series/20261121_0930_15/series.json", true)]
    [DataRow("src/Thing.cs", false)]
    public void A_harvest_pull_request_is_known_by_what_it_carries(string path, bool harvest)
    {
        Assert.AreEqual(harvest, VerifyHarvest.Carries([path]));
    }

    [TestMethod]
    public async Task A_harvest_pull_request_holds_when_every_cycle_it_touches_is_a_husk_it_deletes()
    {
        using var workspace = CycleWorkspace.Create();
        await WriteLedger(workspace, Husk("20261001_0900_01"));

        VerifyUpgrade.Response holds = await Verify(workspace, [".sdlc/harvested.json", ".sdlc/cycles/20261001_0900_01/plan.md"]);
        Assert.IsTrue(holds.Holds, VerifyHarvest.Render(holds));

        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", "cycles", "20261002_0900_01", "plan.md"), "plan");
        VerifyUpgrade.Response refused = await Verify(workspace, [".sdlc/cycles/20261002_0900_01/plan.md"]);

        Assert.IsFalse(refused.Holds, "A cycle the ledger does not record, and whose file is still there, is not a husk.");
        StringAssert.Contains(VerifyHarvest.Render(refused), "20261002_0900_01");
    }

    [TestMethod]
    public async Task A_harvest_pull_request_may_delete_a_series_folder_and_never_change_one()
    {
        using var workspace = CycleWorkspace.Create();
        await WriteLedger(workspace, Husk("20261001_0900_01"));
        string record = ".sdlc/series/20261121_0930_15/series.json";

        VerifyUpgrade.Response holds = await Verify(workspace, [".sdlc/harvested.json", ".sdlc/cycles/20261001_0900_01/plan.md", record]);
        Assert.IsTrue(holds.Holds, VerifyHarvest.Render(holds));

        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", "series", "20261121_0930_15", "series.json"), "{}");
        VerifyUpgrade.Response refused = await Verify(workspace, [".sdlc/harvested.json", record]);

        Assert.IsFalse(refused.Holds, "A series' record still there is a change to it, which only its run makes.");
        StringAssert.Contains(VerifyHarvest.Render(refused), record);
    }

    [TestMethod]
    public async Task Close_deletes_a_series_folder_with_the_last_cycle_its_record_lists_and_keeps_one_still_listing_an_open_cycle()
    {
        using CycleWorkspace workspace = Completed();
        FakeRepository git = Merged(workspace, branch: "development");
        string id = workspace.Cycle.Id.ToString();
        DateTimeOffset now = DateTimeOffset.UnixEpoch;

        // One series ran only this cycle, as the successor its intent leads with; another also ran a cycle still open.
        CycleWorkspace.WriteRaw(
            workspace.Cycle.PathOf(ArtifactKind.Intent),
            (await File.ReadAllTextAsync(workspace.Cycle.PathOf(ArtifactKind.Intent))).Replace("cycle: " + id, "cycle: " + id + "\nlead: successor:totals", StringComparison.Ordinal));
        Sdlc.Core.Series.SeriesRecord done = Sdlc.Core.Series.SeriesRecord.Start("20261121_0930_15", now, 1, Sdlc.Core.Solutions.ApprovalRequirement.None)
            .WithWave(now, [new Sdlc.Core.Series.SeriesItem(Sdlc.Core.Series.SeriesItemKind.Successor, "totals", null)]);
        Sdlc.Core.Series.SeriesRecord open = Sdlc.Core.Series.SeriesRecord.Start("20261121_0930_16", now, 1, Sdlc.Core.Solutions.ApprovalRequirement.None)
            .WithWave(now, [new Sdlc.Core.Series.SeriesItem(Sdlc.Core.Series.SeriesItemKind.Parked, id, id), new Sdlc.Core.Series.SeriesItem(Sdlc.Core.Series.SeriesItemKind.Parked, "20991231_0000_00", "20991231_0000_00")]);
        CycleWorkspace.WriteRaw(Sdlc.Core.Series.SeriesRecord.PathIn(workspace.Root, done.Id), done.Write());
        CycleWorkspace.WriteRaw(Sdlc.Core.Series.SeriesRecord.PathIn(workspace.Root, open.Id), open.Write());

        foreach (HarvestAspect aspect in HarvestAspects.All)
        {
            Assert.IsTrue((await Record(workspace, git, aspect, await Proposals(workspace, string.Empty))).IsSuccess);
        }

        CloseHarvest.Response closed = (await Close(workspace, git)).Value;

        CollectionAssert.AreEqual(new[] { done.Id }, closed.Series.ToArray());
        Assert.IsTrue(git.Ran.Any(run => run.Arguments == "rm -r -q -f --ignore-unmatch -- \".sdlc/series/" + done.Id + "\""));
        Assert.IsFalse(File.Exists(Sdlc.Core.Series.SeriesRecord.PathIn(workspace.Root, done.Id)), "Removed with its last cycle, whether git tracked it or it was gathered from a branch.");
        Assert.IsTrue(File.Exists(Sdlc.Core.Series.SeriesRecord.PathIn(workspace.Root, open.Id)), "A cycle it lists is still open.");
        StringAssert.Contains(HarvestRenderer.RenderClosed(closed), "series " + done.Id);
    }

    [TestMethod]
    public async Task A_successor_waiting_on_a_husk_is_ready_once_the_folder_is_gone()
    {
        using var workspace = CycleWorkspace.Create();
        await WriteLedger(workspace, Husk("20261001_0900_01"));
        CycleWorkspace.WriteRaw(
            Path.Combine(Successor.DirectoryOf(workspace.Root), "import-trades.md"),
            "---\nsucceeds: 20261001_0900_01\n---\n\n# Import trades\n\n## Outcome\n\nTrades arrive.\n\n## Why\n\nAnalysis needs them.\n");

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(new FakeRepository()));
        ReadSuccessors.Response read = (await gate.Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query())).Value;

        Assert.AreEqual(SuccessorState.Ready, read.Successors.Single().State);
    }

    [TestMethod]
    public async Task A_rule_cited_in_a_deleted_husk_still_counts_toward_three_cycles()
    {
        using var workspace = CycleWorkspace.Create();
        await WriteLedger(
            workspace,
            Husk("20261001_0900_01") with { Cited = ["CON_02"] },
            Husk("20261002_0900_01") with { Cited = ["CON_02"] },
            Husk("20261003_0900_01") with { Cited = ["CON_02"] });

        using var gate = Gate.For(workspace.Root);
        ProposeConstraints.Response report = (await gate.Send<ProposeConstraints.Query, ProposeConstraints.Response>(new ProposeConstraints.Query([]))).Value;

        Recurrence recurrence = report.Recurrences.Single();
        Assert.AreEqual("CON_02", recurrence.Cited.ToString());
        Assert.AreEqual(3, recurrence.Cycles.Count);
    }

    [TestMethod]
    public async Task A_harvested_suggestion_becomes_a_successor_with_its_text_copied_in()
    {
        using var workspace = CycleWorkspace.Create();
        string proposals = Path.Combine(workspace.Root, "proposed.md");
        await File.WriteAllTextAsync(
            proposals,
            "## Successors\n\n### [x] add keyboard-chart — the chart is read by keyboard\n**Succeeds:** 20261001_0900_01\n\n**Outcome:** a keyboard user reads the chart.\n\n**Why it matters:** accessibility.S_07 raised it: a canvas chart gives a screen-reader user nothing.\n");

        using var gate = Gate.For(workspace.Root);
        DraftResult drafted = (await gate.Send<DraftHarvest.Command, DraftResult>(new DraftHarvest.Command(proposals))).Value;

        Assert.IsTrue(drafted.MayDraft, drafted.Instructions);
        string written = await File.ReadAllTextAsync(Path.Combine(Successor.DirectoryOf(workspace.Root), "keyboard-chart.md"));
        StringAssert.Contains(written, "succeeds: 20261001_0900_01");
        StringAssert.Contains(written, "a canvas chart gives a screen-reader user nothing");
    }

    [TestMethod]
    public async Task A_covered_line_is_recorded_with_what_covers_it_and_its_concern_leaves_the_tallies()
    {
        using CycleWorkspace workspace = Covering();
        string cycle = workspace.Cycle.Id.ToString();
        await File.WriteAllTextAsync(
            HarvestLedger.PathIn(workspace.Root),
            HarvestLedger.Empty
                .With(Sitting(HarvestAspect.Suggestions), [new Watched("a watched concern", ["20261001_0900_01"])], [new Watched("the chart needs a keyboard path", ["20261001_0900_01", "20261002_0900_01"])])
                .Write());

        string proposals = await Proposals(
            workspace,
            "## Covered\n\n"
            + "### Already handled by a rule\n\n"
            + $"- {cycle}/security.S_01, {cycle}/security.S_02: LOC_01 — rule 1 already forbids it\n"
            + "  Concern: the chart needs a keyboard path\n"
            + $"- {cycle}/security.S_03: CON_01 — an intent never names the fix\n\n"
            + "### Handled elsewhere\n\n"
            + $"- {cycle}/maintainability.S_04: DEC_01 — settled by the outbox decision\n"
            + $"- {cycle}/maintainability.S_05: successor:keyboard-chart — the successor builds it\n"
            + $"- {cycle}/maintainability.S_06: `docs/checklist.md` — the checklist covers it\n"
            + "  Concern: a watched concern\n");

        Result<RecordHarvest.Response> recorded = await Record(workspace, Merged(workspace), HarvestAspect.Suggestions, proposals);
        Assert.IsTrue(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(error => error.Message)));

        HarvestLedger ledger = (await HarvestLedger.Read(workspace.Root, CancellationToken.None)).Value;
        IReadOnlyList<HarvestCover> covered = ledger.Sittings[^1].Covered!;

        CollectionAssert.AreEqual(
            new[] { "LOC_01", "LOC_01", "CON_01", "DEC_01", "successor:keyboard-chart", "docs/checklist.md" },
            covered.Select(cover => cover.By).ToArray(),
            "One entry per suggestion, each with what covers it as written.");
        Assert.AreEqual(cycle + "/security.S_02", covered[1].Id);
        Assert.AreEqual("rule 1 already forbids it", covered[0].Reason);
        Assert.AreEqual(0, ledger.Ignored!.Count, "A covered concern stops counting toward the bar.");
        Assert.AreEqual(0, ledger.Watch.Count, "A covered concern leaves the watch list too.");
        Assert.AreEqual(0, ledger.Sittings[^1].Declined.Count, "Covering declines nothing.");

        string history = HarvestRenderer.RenderHistory(ledger);
        StringAssert.Contains(history, "0 suggestions declined, 6 covered");
        StringAssert.Contains(history, "covered " + cycle + "/maintainability.S_04 by DEC_01: settled by the outbox decision");
        CollectionAssert.AreEqual(
            new[] { "LOC_01", "CON_01", "DEC_01", "successor:keyboard-chart" },
            HarvestRenderer.NamedIn(ledger).ToArray(),
            "The history names each id its answers rely on, once.");
    }

    [TestMethod]
    public async Task A_covered_line_naming_nothing_real_refuses_the_whole_sitting_and_writes_nothing()
    {
        using CycleWorkspace workspace = Covering();
        string cycle = workspace.Cycle.Id.ToString();

        string proposals = await Proposals(
            workspace,
            "## Covered\n\n"
            + $"- {cycle}/security.S_01: LOC_01 — a real rule\n"
            + $"- {cycle}/security.S_02: LOC_09 — no such rule\n"
            + $"- {cycle}/security.S_03: DEC_07 — no such decision\n"
            + $"- {cycle}/security.S_04: successor:nowhere — no such successor\n"
            + $"- {cycle}/security.S_05: docs/missing.md — no such file\n"
            + $"- {cycle}/security.S_06: LOC_01 with no reason\n\n"
            + "## For the framework\n\n### reports — a note that would otherwise be kept\n\nKind: cost\n");

        Result<RecordHarvest.Response> refused = await Record(workspace, Merged(workspace), HarvestAspect.Suggestions, proposals);

        Assert.IsTrue(refused.IsFailed, "A covered answer naming nothing real is refused.");
        string message = refused.Errors[0].Message;
        StringAssert.Contains(message, "LOC_09 is not a rule in force here");
        StringAssert.Contains(message, "DEC_07 is not a decision recorded here");
        StringAssert.Contains(message, "successor:nowhere names no successor");
        StringAssert.Contains(message, "docs/missing.md is not a rule, a decision, successor:<slug>, or a file or directory in this solution");
        StringAssert.Contains(message, "LOC_01 with no reason' does not read");
        Assert.IsFalse(message.Contains("a real rule", StringComparison.Ordinal), "A line that resolves is not listed.");
        Assert.IsFalse(File.Exists(HarvestLedger.PathIn(workspace.Root)), "Nothing is recorded.");
        Assert.IsFalse(File.Exists(FrameworkHarvest.PathIn(workspace.Root)), "Not even the framework's notes.");
    }

    [TestMethod]
    public async Task A_covered_concern_worded_unlike_any_tallied_one_refuses_the_sitting()
    {
        using CycleWorkspace workspace = Covering();
        string cycle = workspace.Cycle.Id.ToString();
        string before = HarvestLedger.Empty
            .With(Sitting(HarvestAspect.Suggestions), [], [new Watched("the chart needs a keyboard path", ["20261001_0900_01"])])
            .Write();
        await File.WriteAllTextAsync(HarvestLedger.PathIn(workspace.Root), before);

        string proposals = await Proposals(
            workspace,
            "## Covered\n\n"
            + $"- {cycle}/security.S_01: LOC_01 — rule 1 covers it\n"
            + "  Concern: the chart needs keyboard access\n"
            + $"- {cycle}/security.S_02: LOC_01 — closes one this sitting ignores\n"
            + "  Concern: a concern raised now\n\n"
            + $"## Ignored\n\n- a concern raised now — {cycle}\n");

        Result<RecordHarvest.Response> refused = await Record(workspace, Merged(workspace), HarvestAspect.Suggestions, proposals);

        Assert.IsTrue(refused.IsFailed, "A concern worded differently closes nothing and is refused.");
        StringAssert.Contains(refused.Errors[0].Message, "'Concern: the chart needs keyboard access' matches no concern ignored or watched");
        StringAssert.Contains(refused.Errors[0].Message, "must match the report's 'ignored before' or watch line exactly");
        Assert.IsFalse(refused.Errors[0].Message.Contains("'Concern: a concern raised now'", StringComparison.Ordinal), "One this sitting tallies is known.");
        Assert.AreEqual(before, await File.ReadAllTextAsync(HarvestLedger.PathIn(workspace.Root)), "Nothing is recorded.");
    }

    [TestMethod]
    public void A_ledger_written_before_covered_still_parses_and_a_sitting_that_covers_nothing_writes_no_key()
    {
        const string before = """
            {
              "cycles": [],
              "watch": [],
              "sittings": [
                {
                  "recorded": "2026-10-01T09:00:00+00:00",
                  "aspect": "suggestions",
                  "cycles": [ "20261001_0900_01" ],
                  "accepted": [],
                  "declined": [ { "id": "20261001_0900_01/security.S_01", "reason": "not ours" } ]
                }
              ]
            }
            """;

        Result<HarvestLedger> read = HarvestLedger.Parse(before, HarvestLedger.FileName);

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(error => error.Message)));
        Assert.IsNull(read.Value.Sittings.Single().Covered);
        StringAssert.Contains(HarvestRenderer.RenderHistory(read.Value), "1 suggestions declined, 0 covered");
        Assert.IsFalse(HarvestLedger.Empty.With(Sitting(HarvestAspect.Suggestions), []).Write().Contains("\"covered\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void A_declined_line_may_name_several_suggestions_and_a_theme_changes_nothing_recorded()
    {
        RecordHarvest.Answers answers = RecordHarvest.Read(
            "## Declined\n\n### Settled by a recorded decision\n\n- a/x.S_01, `a/x.S_02`: DEC_01 settles it\n\n### Another theme\n\n- b/y.S_03: not ours\n\n"
            + "## Ignored\n\n### Slips\n\n- wrong counts — a, b\n  Why: prevents nothing\n");

        CollectionAssert.AreEqual(new[] { "a/x.S_01", "a/x.S_02", "b/y.S_03" }, answers.Declined.Select(decline => decline.Id).ToArray());
        Assert.AreEqual("DEC_01 settles it", answers.Declined[1].Reason);
        Assert.AreEqual("wrong counts", answers.Ignored.Single().Gist);
        Assert.AreEqual("prevents nothing", answers.Ignored.Single().Why);
        Assert.AreEqual(0, answers.Accepted.Count, "A theme heading is not a proposal.");
    }

    [TestMethod]
    public void The_summary_counts_each_answer_by_theme_and_lists_what_the_person_approves()
    {
        HarvestSummary.Response summary = HarvestSummary.Read(
            "# Harvest proposals\n\n## Successors\n\n### [x] add keyboard-chart — a keyboard path\n\n### [ ] add export-csv — an export\n\n"
            + "## Declined\n\n### Settled by a recorded decision\n\n- a/x.S_01, a/x.S_02: DEC_01 settles it\n- a/x.S_03: DEC_01 again\n\n- a/x.S_09: before any theme? no, after\n\n"
            + "## Covered\n\n- a/x.S_04: LOC_01 — unthemed\n\n### Already handled by a rule\n\n- a/x.S_05, a/x.S_06: LOC_01 — the rule\n  Concern: wrong counts\n- a/x.S_07: `docs/checklist.md` — the checklist\n\n"
            + "## Ignored\n\n### Slips fixed in a later round\n\n- wrong counts — a, b\n- vague criteria — a\n");

        Assert.AreEqual(2, summary.Proposals.Count);
        Assert.IsTrue(summary.Proposals[0].Marked);
        Assert.AreEqual("add keyboard-chart — a keyboard path", summary.Proposals[0].Heading);

        HarvestSummary.Answer declined = summary.Answers.Single(answer => answer.Kind == "Declined");
        Assert.AreEqual(4, declined.Total, "Declined counts suggestion ids, several to a line.");
        Assert.AreEqual(4, declined.Themes.Single().Count);

        HarvestSummary.Answer covered = summary.Answers.Single(answer => answer.Kind == "Covered");
        Assert.AreEqual(4, covered.Total);
        Assert.AreEqual(HarvestSummary.NoTheme, covered.Themes[0].Name, "A line before any theme counts under none.");
        HarvestSummary.Theme rule = covered.Themes.Single(theme => theme.Name == "Already handled by a rule");
        Assert.AreEqual(3, rule.Count);
        CollectionAssert.AreEqual(new[] { "LOC_01", "docs/checklist.md" }, rule.Refs.ToArray());

        HarvestSummary.Answer ignored = summary.Answers.Single(answer => answer.Kind == "Ignored");
        Assert.AreEqual(2, ignored.Total, "Ignored counts concerns.");

        string rendered = HarvestSummary.Render(summary);
        StringAssert.Contains(rendered, "Proposals: 2, 1 marked");
        StringAssert.Contains(rendered, "  1. [x] add keyboard-chart — a keyboard path", "Each proposal is numbered, so a person can answer it by number.");
        StringAssert.Contains(rendered, "  2. [ ] add export-csv — an export");
        StringAssert.Contains(rendered, "Covered: 4 suggestions");
        StringAssert.Contains(rendered, "  Already handled by a rule — 3 (by LOC_01, docs/checklist.md)");
        StringAssert.Contains(rendered, "Ignored: 2 concerns");
        StringAssert.Contains(rendered, "  Slips fixed in a later round — 2");
    }

    [TestMethod]
    public async Task The_summary_names_every_id_in_the_file_with_what_it_says()
    {
        using CycleWorkspace workspace = Covering();
        string proposals = await Proposals(
            workspace,
            "## Covered\n\n- a/x.S_01: LOC_01 — rule\n- a/x.S_02: DEC_01 — decision\n- a/x.S_03: successor:keyboard-chart — successor\n\n"
            + "## Declined\n\n- a/x.S_04: LOC_09 and successor:nowhere say nothing here, CON_01 does\n");

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(Merged(workspace)));
        Result<HarvestSummary.Response> summary = await gate.Send<HarvestSummary.Query, HarvestSummary.Response>(new HarvestSummary.Query(proposals));

        Assert.IsTrue(summary.IsSuccess, string.Join("; ", summary.Errors.Select(error => error.Message)));
        string rendered = HarvestSummary.Render(summary.Value);

        StringAssert.Contains(rendered, "Named:");
        StringAssert.Contains(rendered, "  LOC_01 — rule 1");
        StringAssert.Contains(rendered, "  DEC_01 — publishing goes through an outbox");
        StringAssert.Contains(rendered, "  successor:keyboard-chart — Finance sees the month's totals beside the export.");
        StringAssert.Contains(rendered, "  LOC_09 — not found");
        StringAssert.Contains(rendered, "  successor:nowhere — not found");
        StringAssert.Contains(rendered, "  CON_01 — An intent states the problem, not the solution");
        Assert.AreEqual(6, summary.Value.Named.Count, "Each id once.");
    }

    [TestMethod]
    public void The_history_shows_each_id_its_answers_name_with_its_text()
    {
        HarvestLedger ledger = HarvestLedger.Empty.With(
            new HarvestSitting(DateTimeOffset.UnixEpoch, HarvestAspect.Suggestions, ["a"], [], [new HarvestDecline("a/x.S_01", "DEC_01 settles it")], [new HarvestCover("a/x.S_02", "LOC_01", "the rule")]),
            []);

        string history = HarvestRenderer.RenderHistory(ledger, [new Sdlc.Core.Reports.IdTexts.Named("DEC_01", "an outbox", true), new Sdlc.Core.Reports.IdTexts.Named("LOC_01", null, false)]);

        StringAssert.Contains(history, "covered a/x.S_02 by LOC_01: the rule");
        StringAssert.Contains(history, "Named:\n  DEC_01 — an outbox".ReplaceLineEndings());
        StringAssert.Contains(history, "  LOC_01 — not found");
    }

    [TestMethod]
    public void The_history_says_what_closed_what_each_sitting_answered_and_what_is_watched()
    {
        HarvestLedger ledger = HarvestLedger.Empty
            .With(Sitting(HarvestAspect.Decisions, "20261001_0900_01"), [new Watched("a concern", ["20261001_0900_01"])])
            .Closing([Husk("20261001_0900_01")]);

        string history = HarvestRenderer.RenderHistory(ledger);

        StringAssert.Contains(history, "20261001_0900_01  passed, merged at abc123");
        StringAssert.Contains(history, "decisions: 1 cycles");
        StringAssert.Contains(history, "1 of 3 -- a concern");
    }

    [TestMethod]
    public void A_concern_carries_its_reason_past_its_folders_and_a_later_sitting_may_sharpen_it()
    {
        // D-194: the case a person weighs at the bar is kept beside the count, not left in git.
        (IReadOnlyList<Watched> watch, _, _, IReadOnlyList<Watched> ignored, _) = RecordHarvest.Read(
            "## Watch\n\n- the log level is missing — 20261001_0900_01\n  Why: prevents a silent failure; promotes logs a person on call can read\n- no reason given — 20261001_0900_01\n\n"
            + "## Ignored\n\n- the chart needs a keyboard path — 20261001_0900_01\n  Why: prevents a screen a keyboard cannot reach\n");

        Assert.AreEqual("prevents a silent failure; promotes logs a person on call can read", watch[0].Why);
        Assert.IsNull(watch[1].Why, "A concern without a reason still counts.");
        Assert.AreEqual("prevents a screen a keyboard cannot reach", ignored.Single().Why);

        HarvestLedger ledger = HarvestLedger.Empty
            .With(Sitting(HarvestAspect.Decisions), watch)
            .With(Sitting(HarvestAspect.Decisions), [new Watched("the log level is missing", ["20261002_0900_01"])]);
        HarvestLedger read = HarvestLedger.Parse(ledger.Write(), HarvestLedger.FileName).Value;

        Assert.AreEqual("prevents a silent failure; promotes logs a person on call can read", read.Watch[0].Why, "A sitting that gives no reason keeps the one held.");
        StringAssert.Contains(HarvestRenderer.RenderHistory(read), "why: prevents a silent failure");

        HarvestLedger sharpened = read.With(Sitting(HarvestAspect.Decisions), [new Watched("the log level is missing", ["20261003_0900_01"], "prevents an outage nobody can diagnose")]);

        Assert.AreEqual("prevents an outage nobody can diagnose", sharpened.Watch[0].Why);
        Assert.AreEqual(3, sharpened.Watch[0].Cycles.Count);
    }

    [TestMethod]
    public void An_ignored_concern_is_tallied_across_sittings_and_is_no_decision()
    {
        HarvestLedger ledger = HarvestLedger.Empty
            .With(Sitting(HarvestAspect.Suggestions), [], [new Watched("the chart needs a keyboard path", ["20261001_0900_01"])])
            .With(Sitting(HarvestAspect.Suggestions), [], [new Watched("the chart needs a keyboard path", ["20261002_0900_01"])]);

        HarvestLedger read = HarvestLedger.Parse(ledger.Write(), HarvestLedger.FileName).Value;

        CollectionAssert.AreEquivalent(new[] { "20261001_0900_01", "20261002_0900_01" }, read.Ignored!.Single().Cycles.ToArray());
        Assert.IsTrue(read.Sittings.All(sitting => sitting.Declined.Count == 0), "Ignoring declines nothing.");
        StringAssert.Contains(HarvestRenderer.RenderHistory(read), "2 of 3 -- the chart needs a keyboard path");
    }

    [TestMethod]
    public async Task A_sitting_records_what_it_ignored_and_says_when_a_concern_reaches_the_bar()
    {
        using CycleWorkspace workspace = Completed();
        await File.WriteAllTextAsync(
            HarvestLedger.PathIn(workspace.Root),
            HarvestLedger.Empty.With(Sitting(HarvestAspect.Suggestions), [], [new Watched("the chart needs a keyboard path", ["20261001_0900_01", "20261002_0900_01"])]).Write());

        string proposals = await Proposals(workspace, "## Ignored\n\n- the chart needs a keyboard path — " + workspace.Cycle.Id + "\n\n## Declined\n\n- security.S_01: not this solution's concern\n");
        RecordHarvest.Response recorded = (await Record(workspace, Merged(workspace), HarvestAspect.Suggestions, proposals)).Value;

        HarvestLedger ledger = (await HarvestLedger.Read(workspace.Root, CancellationToken.None)).Value;
        Assert.AreEqual(3, ledger.Ignored!.Single().Cycles.Count);
        Assert.AreEqual(0, ledger.Watch.Count, "Ignored is its own tally, not a watched concern.");
        Assert.AreEqual("security.S_01", ledger.Sittings[^1].Declined.Single().Id);
        StringAssert.Contains(HarvestRenderer.RenderRecorded(recorded), "ignored in 3 cycles, at the bar: the chart needs a keyboard path");
    }

    [TestMethod]
    public async Task The_suggestions_report_lists_a_completed_cycles_open_suggestions_and_what_was_ignored_before()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteSuggestionRound(ArtifactKind.Plan, id: "maintainability.S_03", cites: null);
        await File.WriteAllTextAsync(
            HarvestLedger.PathIn(workspace.Root),
            HarvestLedger.Empty.With(Sitting(HarvestAspect.Suggestions), [], [new Watched("the chart needs a keyboard path", ["20261001_0900_01", "20261002_0900_01", "20261003_0900_01"])]).Write());

        HarvestReport.Response inFlight = await Report(workspace, Merged(workspace), HarvestAspect.Suggestions);
        Assert.AreEqual(0, inFlight.Suggestions.Count, "A cycle in flight answers its own suggestions through steer.");

        workspace.WriteVerify("pass");
        HarvestReport.Response completed = await Report(workspace, Merged(workspace), HarvestAspect.Suggestions);
        string rendered = HarvestReport.Render(completed, "proposed.md");

        StringAssert.Contains(rendered, workspace.Cycle.Id + "/maintainability.S_03");
        StringAssert.Contains(rendered, "ignored before, 3 of 3 cycles: the chart needs a keyboard path");
        StringAssert.Contains(rendered, "do not ignore it again");
        StringAssert.Contains(rendered, "## Ignored");
        Assert.IsFalse(rendered.Contains("## Rules", StringComparison.Ordinal), "One aspect's report shapes only its own proposals.");
    }

    [TestMethod]
    public async Task A_sitting_limited_to_some_cycles_reads_and_records_those_alone()
    {
        // D-166. A first sitting over a long history would cost millions of tokens, so a backlog is harvested a few
        // cycles at a time: the rest keep the aspect unanswered.
        using CycleWorkspace workspace = Completed();
        const string other = "20261001_0900_01";
        string otherFolder = Path.Combine(Path.GetDirectoryName(workspace.Cycle.Directory)!, other);
        Directory.CreateDirectory(otherFolder);

        foreach (string file in Directory.GetFiles(workspace.Cycle.Directory))
        {
            await File.WriteAllTextAsync(
                Path.Combine(otherFolder, Path.GetFileName(file)),
                (await File.ReadAllTextAsync(file)).Replace(workspace.Cycle.Id.ToString(), other, StringComparison.Ordinal));
        }

        Assert.IsTrue(CycleId.TryParse(other, out CycleId? named));
        FakeRepository git = Merged(workspace);
        git.History[Integration + ":.sdlc/cycles/" + other] = [("two", DateTimeOffset.UnixEpoch)];

        using (var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git)))
        {
            HarvestReport.Response limited = (await gate.Send<HarvestReport.Query, HarvestReport.Response>(new HarvestReport.Query(HarvestAspect.Constraints, [named.Value]))).Value;

            CollectionAssert.AreEqual(new[] { named.Value }, limited.Sitting.ToArray());
            CollectionAssert.AreEqual(new[] { named.Value }, limited.Record.Select(record => record.Cycle).ToArray(), "The record is the sitting's cycles alone.");
            string rendered = HarvestReport.Render(limited, "proposed.md");
            StringAssert.Contains(rendered, "This sitting reads the cycles named above alone");
            StringAssert.Contains(rendered, "## Cycles");
            StringAssert.Contains(rendered, "                  - " + other);

            Assert.IsTrue(CycleId.TryParse("20200101_0000_00", out CycleId? missing));
            Result<HarvestReport.Response> refused = await gate.Send<HarvestReport.Query, HarvestReport.Response>(new HarvestReport.Query(HarvestAspect.Constraints, [missing.Value]));
            Assert.IsTrue(refused.IsFailed);
            StringAssert.Contains(refused.Errors[0].Message, "20200101_0000_00 is not a completed cycle here");
        }

        RecordHarvest.Response recorded = (await Record(workspace, git, HarvestAspect.Constraints, await Proposals(workspace, "## Cycles\n\n- " + other + "\n"))).Value;

        CollectionAssert.AreEqual(new[] { named.Value }, recorded.Recorded.ToArray());
        CollectionAssert.Contains(
            recorded.Open.Single(open => open.Cycle == workspace.Cycle.Id).Missing.ToArray(),
            HarvestAspect.Constraints,
            "A completed cycle the sitting did not read still needs the aspect.");
    }

    private static CycleWorkspace Completed()
    {
        var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteVerify("pass");
        return workspace;
    }

    /// <summary>A completed cycle in a solution with a rule, a decision, a successor and a file a covered line may name.</summary>
    private static CycleWorkspace Covering()
    {
        CycleWorkspace workspace = Completed();
        workspace.WriteSolutionConstraints(1);
        CycleWorkspace.WriteRaw(
            Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, Sdlc.Core.Decisions.DecisionParser.FileName),
            "# This solution's decisions\n\n## DEC_01 — publishing goes through an outbox\n\n"
            + "**Decided:** the handler writes to an outbox and a dispatcher publishes from it\n\n"
            + "**Because:** the broker cannot join our transaction\n\n"
            + "**Instead of:** a direct publish from the handler, which costs the atomicity\n\n"
            + "**From:** 20260901_0900_01\n");
        SuccessorTests.Write(workspace, "keyboard-chart", workspace.Cycle.Id.ToString());
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, "docs", "checklist.md"), "# Checklist\n");
        return workspace;
    }

    private static FakeRepository Merged(CycleWorkspace workspace, string branch = "master")
    {
        FakeRepository git = new() { Branch = branch };
        git.Refs.Add(Integration);
        git.History[Integration + ":.sdlc/cycles/" + workspace.Cycle.Id] = [("first", DateTimeOffset.UnixEpoch), ("merged", DateTimeOffset.UnixEpoch.AddDays(1))];
        return git;
    }

    private static HarvestedCycle Husk(string id) =>
        new(id, "abc123", DateTimeOffset.UnixEpoch, false, ["src/Thing.cs"], ["src/Thing.cs"], [], [], new Dictionary<string, int> { ["intent"] = 1 }, 0, ["pass"]);

    private static HarvestSitting Sitting(HarvestAspect aspect, params string[] cycles) => new(DateTimeOffset.UnixEpoch, aspect, cycles, [], []);

    private static async Task WriteLedger(CycleWorkspace workspace, params HarvestedCycle[] cycles) =>
        await File.WriteAllTextAsync(HarvestLedger.PathIn(workspace.Root), HarvestLedger.Empty.Closing(cycles).Write());

    private static async Task<string> Proposals(CycleWorkspace workspace, string body)
    {
        string path = Path.Combine(workspace.Root, "proposed.md");
        await File.WriteAllTextAsync(path, "# Harvest proposals\n\n" + body);
        return path;
    }

    private static async Task<Result<RecordHarvest.Response>> Record(CycleWorkspace workspace, FakeRepository git, HarvestAspect aspect, string proposals)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        return await gate.Send<RecordHarvest.Command, RecordHarvest.Response>(new RecordHarvest.Command(aspect, proposals));
    }

    private static async Task<GatherAbandoned.Response> Gather(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<GatherAbandoned.Response> gathered = await gate.Send<GatherAbandoned.Command, GatherAbandoned.Response>(new GatherAbandoned.Command());
        Assert.IsTrue(gathered.IsSuccess, string.Join("; ", gathered.Errors.Select(error => error.Message)));
        return gathered.Value;
    }

    private static async Task<Result<CloseHarvest.Response>> Close(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        return await gate.Send<CloseHarvest.Command, CloseHarvest.Response>(new CloseHarvest.Command());
    }

    private static async Task<HarvestReport.Response> Report(CycleWorkspace workspace, FakeRepository git, HarvestAspect? aspect = null)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<HarvestReport.Response> report = await gate.Send<HarvestReport.Query, HarvestReport.Response>(new HarvestReport.Query(aspect));
        Assert.IsTrue(report.IsSuccess, string.Join("; ", report.Errors.Select(error => error.Message)));
        return report.Value;
    }

    private static async Task<VerifyUpgrade.Response> Verify(CycleWorkspace workspace, IReadOnlyList<string> changed)
    {
        using var gate = Gate.For(workspace.Root);
        return (await gate.Send<VerifyHarvest.Query, VerifyUpgrade.Response>(new VerifyHarvest.Query(changed))).Value;
    }
}
