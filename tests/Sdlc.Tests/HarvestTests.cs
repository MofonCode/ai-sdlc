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
        StringAssert.Contains(HarvestReport.RenderOverview(unmerged), "is finished but not merged into development yet");

        HarvestReport.Response merged = await Report(workspace, Merged(workspace));
        HarvestReport.OpenCycle open = merged.Open.Single();
        CollectionAssert.AreEqual(HarvestAspects.All.ToArray(), open.Missing.ToArray(), "Every aspect is still to answer.");
        StringAssert.Contains(HarvestReport.RenderOverview(merged), "Next: 'sdlc harvest constraints'");
    }

    [TestMethod]
    public async Task An_abandoned_cycle_is_completed_once_merged()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.Abandon();

        HarvestReport.OpenCycle open = (await Report(workspace, Merged(workspace))).Open.Single();

        Assert.IsTrue(open.Abandoned);
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
        Assert.IsTrue(git.Ran.Any(run => run.Arguments.Contains("rm -r -q -- \".sdlc/cycles/" + workspace.Cycle.Id + "\"", StringComparison.Ordinal)));
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
    [DataRow(".sdlc/decisions.md", true)]
    [DataRow(".sdlc/harvested.json", true)]
    [DataRow(".sdlc/areas.md", true)]
    [DataRow(".sdlc/successors/import-trades.md", true)]
    [DataRow(".sdlc/cycles/20261001_0900_01/plan.md", true)]
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
