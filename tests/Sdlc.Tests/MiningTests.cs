using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Drafts;
using Sdlc.Core.Git;
using Sdlc.Core.Hooks;
using Sdlc.Core.Mining;
using Sdlc.Core.Proposals;
using Sdlc.Core.Successors;

namespace Sdlc.Tests;

/// <summary>
/// Mining a cycle to a husk (D-152): what it leaves in the ledger, when its folder may go, and what still
/// reads it once it has.
/// </summary>
/// <remarks>
/// The failure guarded against is a folder deleted with something still needing it: a count toward the bar,
/// a successor waiting on it, a decision citing it. So most of these are about what survives.
/// </remarks>
[TestClass]
public sealed class MiningTests
{
    private const string Integration = "origin/development";

    [TestMethod]
    public void The_ledger_reads_back_what_it_wrote_and_a_watched_concern_gains_cycles_rather_than_repeating()
    {
        MinedCycle cycle = Husk("20261001_0900_01");
        MinedLedger ledger = MinedLedger.Empty
            .With([cycle], Run(), [new Watched("the log level is missing", ["20261001_0900_01"])])
            .With([], Run(), [new Watched("the log level is missing", ["20261002_0900_01"])]);

        MinedLedger read = MinedLedger.Parse(ledger.Write(), "mined.json").Value;

        Assert.AreEqual("abc123", read.Find("20261001_0900_01")!.Merge);
        Assert.AreEqual(1, read.Watch.Count, "One concern, watched across sittings.");
        CollectionAssert.AreEquivalent(new[] { "20261001_0900_01", "20261002_0900_01" }, read.Watch[0].Cycles.ToArray());
        Assert.AreEqual(2, read.Runs.Count);
    }

    [TestMethod]
    public async Task A_sitting_is_not_recorded_while_a_draft_is_still_beside_its_file()
    {
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", "decisions.draft.md"), "# d\n");
        string proposals = await Proposals(workspace, string.Empty);

        Result<RecordMining.Response> refused = await Record(workspace, new FakeRepository(), proposals);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "decisions.draft.md");
    }

    [TestMethod]
    public async Task A_finished_merged_cycle_is_recorded_as_a_husk_and_one_still_in_flight_or_unmerged_is_left()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteVerify("pass");

        FakeRepository git = new();
        git.Refs.Add(Integration);
        string folder = ".sdlc/cycles/" + workspace.Cycle.Id;
        git.History[Integration + ":" + folder] = [("first", DateTimeOffset.UnixEpoch), ("merged", DateTimeOffset.UnixEpoch.AddDays(1))];

        string proposals = await Proposals(workspace, "## Watch\n\n- the log level is missing — " + workspace.Cycle.Id + "\n\n## Declined\n\n- security.S_01: not this solution's concern\n");
        RecordMining.Response recorded = (await Record(workspace, git, proposals)).Value;

        MinedCycle husk = recorded.Recorded.Single();
        Assert.AreEqual("merged", husk.Merge, "The newest commit on the integration branch that touched the folder.");

        MinedLedger ledger = (await MinedLedger.Read(workspace.Root, CancellationToken.None)).Value;
        Assert.IsNotNull(ledger.Find(workspace.Cycle.Id.ToString()));
        Assert.AreEqual("the log level is missing", ledger.Watch.Single().Gist);
        Assert.AreEqual("security.S_01", ledger.Runs.Single().Declined.Single().Id);

        string rendered = MiningRenderer.RenderRecorded(recorded);
        StringAssert.Contains(rendered, "git rm -r -q .sdlc/cycles/" + workspace.Cycle.Id);
        StringAssert.Contains(rendered, "git switch -c mining/");
    }

    [TestMethod]
    public async Task A_cycle_not_on_the_integration_branch_is_not_a_husk_yet()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteVerify("pass");

        RecordMining.Response recorded = (await Record(workspace, new FakeRepository(), await Proposals(workspace, string.Empty))).Value;

        Assert.AreEqual(0, recorded.Recorded.Count);
        CollectionAssert.AreEqual(new[] { workspace.Cycle.Id }, recorded.Unmerged.ToArray());
    }

    [TestMethod]
    public async Task A_cycle_in_flight_is_left_alone()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        RecordMining.Response recorded = (await Record(workspace, new FakeRepository(), await Proposals(workspace, string.Empty))).Value;

        CollectionAssert.AreEqual(new[] { workspace.Cycle.Id }, recorded.Unfinished.ToArray());
    }

    [TestMethod]
    public void An_agent_writing_the_ledger_is_denied_by_path_and_by_command()
    {
        Assert.IsTrue(ProtectedPaths.IsProtected(".sdlc/mined.json", out ProtectedFile? byPath));
        StringAssert.Contains(byPath!.Reason, "sdlc mine record");

        Assert.IsTrue(ProtectedPaths.CommandTouchesProtected("echo {} > .sdlc/mined.json", out ProtectedFile? byRedirect));
        Assert.AreSame(ProtectedPaths.Mined, byRedirect);
        Assert.IsTrue(ProtectedPaths.CommandTouchesProtected("cp other.json .sdlc/mined.json", out _));
        Assert.IsFalse(ProtectedPaths.CommandTouchesProtected("cat .sdlc/mined.json", out _), "Reading it is allowed.");
    }

    [TestMethod]
    [DataRow(".sdlc/decisions.md", true)]
    [DataRow(".sdlc/mined.json", true)]
    [DataRow(".sdlc/areas.md", true)]
    [DataRow(".sdlc/successors/import-trades.md", true)]
    [DataRow(".sdlc/cycles/20261001_0900_01/plan.md", true)]
    [DataRow("src/Thing.cs", false)]
    public void A_mining_pull_request_is_known_by_what_it_carries(string path, bool mining)
    {
        Assert.AreEqual(mining, VerifyMining.Carries([path]));
    }

    [TestMethod]
    public async Task A_mining_pull_request_holds_when_every_cycle_it_touches_is_a_husk_it_deletes()
    {
        using var workspace = CycleWorkspace.Create();
        await WriteLedger(workspace, Husk("20261001_0900_01"));

        VerifyUpgrade.Response holds = await Verify(workspace, [".sdlc/mined.json", ".sdlc/cycles/20261001_0900_01/plan.md"]);
        Assert.IsTrue(holds.Holds, VerifyMining.Render(holds));

        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", "cycles", "20261002_0900_01", "plan.md"), "plan");
        VerifyUpgrade.Response refused = await Verify(workspace, [".sdlc/cycles/20261002_0900_01/plan.md"]);

        Assert.IsFalse(refused.Holds, "A cycle the ledger does not record, and whose file is still there, is not a husk.");
        StringAssert.Contains(VerifyMining.Render(refused), "20261002_0900_01");
    }

    [TestMethod]
    public async Task A_successor_waiting_on_a_husk_is_ready_once_the_folder_is_gone()
    {
        using var workspace = CycleWorkspace.Create();
        MinedCycle husk = Husk("20261001_0900_01");
        await WriteLedger(workspace, husk);
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
        ProposeConstraints.Response report = (await gate.Send<ProposeConstraints.Query, ProposeConstraints.Response>(new ProposeConstraints.Query())).Value;

        Recurrence recurrence = report.Recurrences.Single();
        Assert.AreEqual("CON_02", recurrence.Cited.ToString());
        Assert.AreEqual(3, recurrence.Cycles.Count);
    }

    [TestMethod]
    public async Task A_mined_suggestion_becomes_a_successor_with_its_text_copied_in()
    {
        using var workspace = CycleWorkspace.Create();
        string proposals = Path.Combine(workspace.Root, "proposed.md");
        await File.WriteAllTextAsync(
            proposals,
            "## Successors\n\n### [x] add keyboard-chart — the chart is read by keyboard\n**Succeeds:** 20261001_0900_01\n\n**Outcome:** a keyboard user reads the chart.\n\n**Why it matters:** accessibility.S_07 raised it: a canvas chart gives a screen-reader user nothing.\n");

        using var gate = Gate.For(workspace.Root);
        DraftResult drafted = (await gate.Send<DraftMining.Command, DraftResult>(new DraftMining.Command(proposals))).Value;

        Assert.IsTrue(drafted.MayDraft, drafted.Instructions);
        string written = await File.ReadAllTextAsync(Path.Combine(Successor.DirectoryOf(workspace.Root), "keyboard-chart.md"));
        StringAssert.Contains(written, "succeeds: 20261001_0900_01");
        StringAssert.Contains(written, "a canvas chart gives a screen-reader user nothing");
    }

    [TestMethod]
    public void The_history_says_what_was_mined_and_what_is_watched()
    {
        MinedLedger ledger = MinedLedger.Empty.With([Husk("20261001_0900_01")], Run(), [new Watched("a concern", ["20261001_0900_01"])]);

        string history = MiningRenderer.RenderHistory(ledger);

        StringAssert.Contains(history, "20261001_0900_01  passed, merged at abc123");
        StringAssert.Contains(history, "1 of 3 -- a concern");
    }

    [TestMethod]
    public void An_ignored_concern_is_tallied_across_sittings_and_is_no_decision()
    {
        MinedLedger ledger = MinedLedger.Empty
            .With([], Run(), [], [new Watched("the chart needs a keyboard path", ["20261001_0900_01"])])
            .With([], Run(), [], [new Watched("the chart needs a keyboard path", ["20261002_0900_01"])]);

        MinedLedger read = MinedLedger.Parse(ledger.Write(), "mined.json").Value;

        CollectionAssert.AreEquivalent(new[] { "20261001_0900_01", "20261002_0900_01" }, read.Ignored!.Single().Cycles.ToArray());
        Assert.IsTrue(read.Runs.All(run => run.Declined.Count == 0), "Ignoring declines nothing.");
        StringAssert.Contains(MiningRenderer.RenderHistory(read), "2 of 3 -- the chart needs a keyboard path");
    }

    [TestMethod]
    public void A_ledger_written_before_ignored_concerns_reads_as_having_none()
    {
        MinedLedger read = MinedLedger.Parse("""{ "cycles": [], "watch": [], "runs": [] }""", "mined.json").Value;

        Assert.AreEqual(0, read.Ignored!.Count);
    }

    [TestMethod]
    public async Task A_sitting_records_what_it_ignored_and_says_when_a_concern_reaches_the_bar()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteVerify("pass");
        await File.WriteAllTextAsync(
            MinedLedger.PathIn(workspace.Root),
            MinedLedger.Empty.With([], Run(), [], [new Watched("the chart needs a keyboard path", ["20261001_0900_01", "20261002_0900_01"])]).Write());

        FakeRepository git = new();
        git.Refs.Add(Integration);
        git.History[Integration + ":.sdlc/cycles/" + workspace.Cycle.Id] = [("merged", DateTimeOffset.UnixEpoch)];

        string proposals = await Proposals(workspace, "## Ignored\n\n- the chart needs a keyboard path — " + workspace.Cycle.Id + "\n");
        RecordMining.Response recorded = (await Record(workspace, git, proposals)).Value;

        MinedLedger ledger = (await MinedLedger.Read(workspace.Root, CancellationToken.None)).Value;
        Assert.AreEqual(3, ledger.Ignored!.Single().Cycles.Count);
        Assert.AreEqual(0, ledger.Watch.Count, "Ignored is its own tally, not a watched concern.");
        StringAssert.Contains(MiningRenderer.RenderRecorded(recorded), "ignored in 3 cycles, at the bar: the chart needs a keyboard path");
    }

    [TestMethod]
    public async Task The_report_lists_a_finished_cycles_open_suggestions_and_what_was_ignored_before()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteSuggestionRound(ArtifactKind.Plan, id: "maintainability.S_03", cites: null);
        await File.WriteAllTextAsync(
            MinedLedger.PathIn(workspace.Root),
            MinedLedger.Empty.With([], Run(), [], [new Watched("the chart needs a keyboard path", ["20261001_0900_01", "20261002_0900_01", "20261003_0900_01"])]).Write());

        ProposeMining.Response inFlight = await Report(workspace);
        Assert.AreEqual(0, inFlight.Suggestions!.Count, "A cycle in flight answers its own suggestions through steer.");

        workspace.WriteVerify("pass");
        ProposeMining.Response finished = await Report(workspace);
        string rendered = ProposeMining.Render(finished, "proposed.md");

        StringAssert.Contains(rendered, workspace.Cycle.Id + "/maintainability.S_03");
        StringAssert.Contains(rendered, "ignored before, 3 of 3 cycles: the chart needs a keyboard path");
        StringAssert.Contains(rendered, "do not ignore it again");
        StringAssert.Contains(rendered, "## Ignored");
    }

    [TestMethod]
    public async Task A_sitting_limited_to_some_cycles_reads_and_records_those_alone()
    {
        // D-166. A first sitting over a long history would cost millions of tokens, so a backlog is mined a few
        // cycles at a time: the rest stay unmined, and none becomes a husk nobody read.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteVerify("pass");
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

        using (var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(new FakeRepository())))
        {
            ProposeMining.Response limited = (await gate.Send<ProposeMining.Query, ProposeMining.Response>(new ProposeMining.Query([named.Value]))).Value;

            CollectionAssert.AreEqual(new[] { named.Value }, limited.Sitting!.ToArray());
            CollectionAssert.AreEqual(new[] { named.Value }, limited.Record!.Select(record => record.Cycle).ToArray(), "The record is the sitting's cycles alone.");
            string rendered = ProposeMining.Render(limited, "proposed.md");
            StringAssert.Contains(rendered, "This sitting   " + other);
            StringAssert.Contains(rendered, "## Cycles");
            StringAssert.Contains(rendered, "                  - " + other);

            Assert.IsTrue(CycleId.TryParse("20200101_0000_00", out CycleId? missing));
            Result<ProposeMining.Response> refused = await gate.Send<ProposeMining.Query, ProposeMining.Response>(new ProposeMining.Query([missing.Value]));
            Assert.IsTrue(refused.IsFailed);
            StringAssert.Contains(refused.Errors[0].Message, "20200101_0000_00 is not a finished cycle here");
        }

        FakeRepository git = new();
        git.Refs.Add(Integration);
        git.History[Integration + ":.sdlc/cycles/" + workspace.Cycle.Id] = [("one", DateTimeOffset.UnixEpoch)];
        git.History[Integration + ":.sdlc/cycles/" + other] = [("two", DateTimeOffset.UnixEpoch)];

        RecordMining.Response recorded = (await Record(workspace, git, await Proposals(workspace, "## Cycles\n\n- " + other + "\n"))).Value;

        CollectionAssert.AreEqual(new[] { other }, recorded.Recorded.Select(cycle => cycle.Cycle).ToArray());
        CollectionAssert.AreEqual(new[] { workspace.Cycle.Id }, recorded.Later!.ToArray(), "A finished cycle the sitting did not read waits for a later one.");
        StringAssert.Contains(MiningRenderer.RenderRecorded(recorded), "1 finished cycle this sitting did not read, left for a later one");
    }

    private static MinedCycle Husk(string id) =>
        new(id, "abc123", DateTimeOffset.UnixEpoch, false, ["src/Thing.cs"], ["src/Thing.cs"], [], [], new Dictionary<string, int> { ["intent"] = 1 }, 0, ["pass"]);

    private static MiningRun Run() => new(DateTimeOffset.UnixEpoch, [], [], []);

    private static async Task WriteLedger(CycleWorkspace workspace, params MinedCycle[] cycles) =>
        await File.WriteAllTextAsync(MinedLedger.PathIn(workspace.Root), MinedLedger.Empty.With(cycles, Run(), []).Write());

    private static async Task<string> Proposals(CycleWorkspace workspace, string body)
    {
        string path = Path.Combine(workspace.Root, "proposed.md");
        await File.WriteAllTextAsync(path, "# Mining proposals\n\n" + body);
        return path;
    }

    private static async Task<Result<RecordMining.Response>> Record(CycleWorkspace workspace, FakeRepository git, string proposals)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        return await gate.Send<RecordMining.Command, RecordMining.Response>(new RecordMining.Command(proposals));
    }

    private static async Task<ProposeMining.Response> Report(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(new FakeRepository()));
        Result<ProposeMining.Response> report = await gate.Send<ProposeMining.Query, ProposeMining.Response>(new ProposeMining.Query());
        Assert.IsTrue(report.IsSuccess, string.Join("; ", report.Errors.Select(error => error.Message)));
        return report.Value;
    }

    private static async Task<VerifyUpgrade.Response> Verify(CycleWorkspace workspace, IReadOnlyList<string> changed)
    {
        using var gate = Gate.For(workspace.Root);
        return (await gate.Send<VerifyMining.Query, VerifyUpgrade.Response>(new VerifyMining.Query(changed))).Value;
    }
}
