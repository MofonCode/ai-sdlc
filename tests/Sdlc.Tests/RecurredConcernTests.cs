using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Git;
using Sdlc.Core.Harvest;

namespace Sdlc.Tests;

/// <summary>
/// A concern a harvest already answered, raised again by a later cycle's reader (D-221): matched by the reconciler,
/// recorded in the cycle's own ledger as recurring rather than as a suggestion to answer, and folded into
/// <c>harvested.json</c> only when the cycle closes, each (concern, cycle, document, round) once.
/// </summary>
/// <remarks>
/// The failures guarded against are a person asked again about what they already answered, a merge of two branches
/// counting one recurrence twice, a blocker set aside because it resembles an old suggestion, and one reader's gap
/// raised three times in one round standing as three suggestions.
/// </remarks>
[TestClass]
public sealed class RecurredConcernTests
{
    private const string Gist = "the chart needs a keyboard path";
    private const string Silent = """{ "findings": [] }""";

    [TestMethod]
    public void A_key_is_the_same_for_the_same_text_on_every_read_and_differs_by_kind()
    {
        Assert.AreEqual(HarvestedConcerns.KeyOf(HarvestedConcerns.Ignored, Gist), HarvestedConcerns.KeyOf(HarvestedConcerns.Ignored, " " + Gist + " "));
        Assert.AreNotEqual(HarvestedConcerns.KeyOf(HarvestedConcerns.Ignored, Gist), HarvestedConcerns.KeyOf(HarvestedConcerns.Watch, Gist));
        StringAssert.Matches(HarvestedConcerns.KeyOf(HarvestedConcerns.Ignored, Gist), new System.Text.RegularExpressions.Regex("^ignored:[0-9a-f]{8}$"));
    }

    [TestMethod]
    public void Declines_and_covers_are_shown_once_per_answer_not_once_per_suggestion()
    {
        HarvestLedger ledger = HarvestLedger.Empty with
        {
            Sittings =
            [
                new HarvestSitting(
                    DateTimeOffset.UnixEpoch,
                    HarvestAspect.Suggestions,
                    ["20261001_0900_01"],
                    [],
                    [new HarvestDecline("20261001_0900_01/a.S_01", "not wanted yet"), new HarvestDecline("20261001_0900_01/a.S_02", "not wanted yet")],
                    [new HarvestCover("20261001_0900_01/b.S_01", "successor:keyboard-chart", "the successor adds it"), new HarvestCover("20261001_0900_01/b.S_02", "successor:keyboard-chart", "the successor adds it")]),
            ],
            Ignored = [new Watched(Gist, ["20261001_0900_01"])],
        };

        IReadOnlyList<HarvestedConcern> concerns = HarvestedConcerns.From(ledger, [("keyboard-chart", "A keyboard path through the chart")]);

        CollectionAssert.AreEqual(
            new[] { HarvestedConcerns.Ignored, HarvestedConcerns.Declined, HarvestedConcerns.Covered, HarvestedConcerns.Successor },
            concerns.Select(concern => concern.Kind).ToArray());
        Assert.AreEqual("successor:keyboard-chart", concerns[^1].Key);
        StringAssert.Contains(concerns[2].Gist, "covered by successor:keyboard-chart");
    }

    [TestMethod]
    public void Folding_the_same_recurrence_twice_or_reading_a_merge_that_carries_it_twice_counts_it_once()
    {
        string key = HarvestedConcerns.KeyOf(HarvestedConcerns.Ignored, Gist);
        HarvestedRecurrence once = new(key, Gist, "20261003_0900_01", "plan", 2);
        HarvestLedger ledger = HarvestLedger.Empty.With(Sitting(), [], [new Watched(Gist, ["20261001_0900_01", "20261002_0900_01"])]);

        HarvestLedger folded = ledger.Recurring([once]).Recurring([once, once with { Round = 3 }]);
        Assert.AreEqual(2, folded.Recurred!.Count, "Keyed by concern, cycle, document and round.");

        // Two close branches merged by hand: the file carries one recurrence twice.
        string merged = (folded with { Recurred = [.. folded.Recurred, once] }).Write();
        HarvestLedger read = HarvestLedger.Parse(merged, HarvestLedger.FileName).Value;

        Assert.AreEqual(2, read.Recurred!.Count);
        IReadOnlyList<string> seen = read.SeenIn(HarvestedConcerns.Ignored, read.Ignored!.Single());
        Assert.AreEqual(3, seen.Count, "Distinct cycles: two a person ignored it in, one whose reader raised it again.");
        CollectionAssert.AreEqual(new[] { "20261001_0900_01", "20261002_0900_01" }, read.Ignored!.Single().Cycles.ToArray(), "A person's own cycles are untouched.");
        StringAssert.Contains(HarvestRenderer.RenderHistory(read), "3 of 3 -- " + Gist);
        StringAssert.Contains(HarvestRenderer.RenderHistory(read), "[1 of them a reader raising it again]");
    }

    [TestMethod]
    public void A_ledger_written_before_has_no_recurred_key_and_writes_none()
    {
        HarvestLedger read = HarvestLedger.Parse(HarvestLedger.Empty.Write(), HarvestLedger.FileName).Value;

        Assert.IsNull(read.Recurred);
        Assert.IsFalse(read.Write().Contains("recurred", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task The_reconciler_is_shown_each_harvested_concern_by_its_key()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await WriteIgnored(workspace, "20261001_0900_01");
        string directory = await Answers(workspace, security: """{ "findings": [ { "id": "S_01", "severity": "suggestion", "statement": "Give the chart a keyboard path." } ] }""");

        Result<ReaderPrompts.Response> reconcile = await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Intent, directory, "reconcile");

        Assert.IsTrue(reconcile.IsSuccess, string.Join("; ", reconcile.Errors.Select(error => error.Message)));
        string prompt = await File.ReadAllTextAsync(reconcile.Value.Prompts[0].Path);
        StringAssert.Contains(prompt, "- " + HarvestedConcerns.KeyOf(HarvestedConcerns.Ignored, Gist) + " (ignored): " + Gist);
        StringAssert.Contains(prompt, "another finding of this round");
    }

    [TestMethod]
    public async Task A_suggestion_restating_a_harvested_concern_is_recorded_as_recurring_and_asked_of_nobody()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await WriteIgnored(workspace, "20261001_0900_01");
        string key = HarvestedConcerns.KeyOf(HarvestedConcerns.Ignored, Gist);
        string directory = await Answers(workspace, security: """{ "findings": [ { "id": "S_01", "severity": "suggestion", "statement": "Give the chart a keyboard path.", "gist": "No keyboard path." } ] }""");
        await Answer(directory, "reconcile", "{ \"restatements\": [ { \"finding\": \"security.S_01\", \"standing\": \"" + key + "\" } ], \"pairs\": [] }");

        CollectRound.Response drafted = (await Collect(workspace, directory)).Value;

        Assert.AreEqual(0, drafted.Raised.Count, "Not a new suggestion.");
        Assert.AreEqual(key, drafted.Recurred!["security.S_01"]);
        StringAssert.Contains(FindingsRenderer.RenderCollected(drafted), "a concern a harvest already answered");

        await RecordDraft(workspace, drafted.Path);
        SuggestionLedger ledger = (await FindingsReader.ReadSuggestions(workspace.Cycle, CancellationToken.None)).Value;

        Assert.AreEqual(0, ledger.Suggestions.Count, "Nothing for status, 'sdlc suggestions' or a harvest to ask about.");
        RecurredSuggestion recurred = ledger.Recurred!.Single();
        Assert.AreEqual(key, recurred.Restates);
        Assert.AreEqual("intent", recurred.Artifact);
        Assert.AreEqual(1, recurred.Round);
        Assert.AreEqual("security", recurred.Perspective);
        Assert.IsFalse((await File.ReadAllTextAsync(HarvestLedger.PathIn(workspace.Root))).Contains("recurred", StringComparison.Ordinal), "A round never writes harvested.json.");
    }

    [TestMethod]
    public async Task A_blocker_restating_a_harvested_concern_stays_a_new_blocker()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await WriteIgnored(workspace, "20261001_0900_01");
        string key = HarvestedConcerns.KeyOf(HarvestedConcerns.Ignored, Gist);
        string directory = await Answers(workspace, rules: """
            { "findings": [ { "id": "F_01", "severity": "blocking",
              "cites": { "id": "CON_01", "heading": "An intent states the problem, not the solution" },
              "gist": "The problem names the fix.", "statement": "It prescribes a button.", "locus": "Problem" } ] }
            """);
        await Answer(directory, "reconcile", "{ \"restatements\": [ { \"finding\": \"F_01\", \"standing\": \"" + key + "\" } ], \"pairs\": [] }");

        CollectRound.Response drafted = (await Collect(workspace, directory)).Value;

        Assert.AreEqual(Severity.Blocking, drafted.Raised.Single().Severity);
        Assert.AreEqual(0, drafted.Recurred!.Count);
    }

    [TestMethod]
    public async Task One_gap_raised_twice_in_one_round_is_one_finding_and_a_blocker_is_kept_over_a_suggestion()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string directory = await Answers(
            workspace,
            rules: """
                { "findings": [ { "id": "F_01", "severity": "blocking",
                  "cites": { "id": "CON_01", "heading": "An intent states the problem, not the solution" },
                  "gist": "The problem names the fix.", "statement": "It prescribes a button.", "locus": "Problem" } ] }
                """,
            security: """
                { "findings": [ { "id": "S_01", "severity": "suggestion", "statement": "Respect reduced motion." },
                                { "id": "S_02", "severity": "suggestion", "statement": "Reduced motion again, in other words." },
                                { "id": "S_03", "severity": "suggestion", "statement": "The problem prescribes a button." } ] }
                """);
        await Answer(directory, "reconcile", """
            { "restatements": [ { "finding": "security.S_02", "standing": "security.S_01" },
                                { "finding": "security.S_03", "standing": "F_01" } ], "pairs": [] }
            """);

        CollectRound.Response drafted = (await Collect(workspace, directory)).Value;

        Assert.AreEqual(2, drafted.Raised.Count, string.Join(", ", drafted.Raised.Select(finding => finding.Id)));
        Assert.AreEqual(1, drafted.Raised.Count(finding => finding.Severity == Severity.Blocking));
        Assert.AreEqual(1, drafted.Raised.Count(finding => finding.Severity == Severity.Suggestion && finding.Statement == "Respect reduced motion."));
    }

    [TestMethod]
    public async Task Close_folds_each_closing_cycles_recurrences_into_the_ledger_and_the_report_counts_them_before()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteVerify("pass");
        await WriteIgnored(workspace, "20261001_0900_01", "20261002_0900_01");
        string key = HarvestedConcerns.KeyOf(HarvestedConcerns.Ignored, Gist);

        SuggestionLedger restated = SuggestionLedger.Empty(workspace.Cycle.Id.ToString())
            .Recurring(ArtifactKind.Plan, 1, [new RecurredSuggestion(key, string.Empty, 0, "accessibility", "Give the chart a keyboard path.")])
            .Recurring(ArtifactKind.Plan, 1, [new RecurredSuggestion(key, string.Empty, 0, "accessibility", "Give the chart a keyboard path.")]);
        Assert.AreEqual(1, restated.Recurred!.Count, "Recording one round's restatement twice adds nothing.");
        Directory.CreateDirectory(workspace.Cycle.FindingsDirectory);
        await File.WriteAllTextAsync(workspace.Cycle.SuggestionsLedgerPath, FindingsReader.Write(restated));

        FakeRepository git = Merged(workspace, branch: "development");
        HarvestReport.Response report = await Report(workspace, git, HarvestAspect.Suggestions);
        string rendered = HarvestReport.Render(report, "proposed.md");

        StringAssert.Contains(rendered, "ignored before, 3 of 3 cycles: " + Gist);
        StringAssert.Contains(rendered, "do not ignore it again", "Raised again in an open cycle, it reaches the bar in the sitting that sees it.");

        foreach (HarvestAspect aspect in HarvestAspects.All)
        {
            Result<RecordHarvest.Response> recorded = await Record(workspace, git, aspect, await Proposals(workspace, aspect == HarvestAspect.Suggestions ? "## Covered\n\n" : string.Empty));
            Assert.IsTrue(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(error => error.Message)));
        }

        Result<CloseHarvest.Response> closed = await Close(workspace, git);
        Assert.IsTrue(closed.IsSuccess, string.Join("; ", closed.Errors.Select(error => error.Message)));

        HarvestLedger ledger = (await HarvestLedger.Read(workspace.Root, CancellationToken.None)).Value;
        HarvestedRecurrence folded = ledger.Recurred!.Single();
        Assert.AreEqual(key, folded.Key);
        Assert.AreEqual(workspace.Cycle.Id.ToString(), folded.Cycle);
        Assert.AreEqual("plan", folded.Document);
        Assert.AreEqual(3, ledger.SeenIn(HarvestedConcerns.Ignored, ledger.Ignored!.Single()).Count);
    }

    private static HarvestSitting Sitting() => new(DateTimeOffset.UnixEpoch, HarvestAspect.Suggestions, [], [], []);

    private static Task WriteIgnored(CycleWorkspace workspace, params string[] cycles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(HarvestLedger.PathIn(workspace.Root))!);
        return File.WriteAllTextAsync(HarvestLedger.PathIn(workspace.Root), HarvestLedger.Empty.With(Sitting(), [], [new Watched(Gist, cycles)]).Write());
    }

    private static async Task<string> Answers(CycleWorkspace workspace, string rules = Silent, string security = Silent)
    {
        string directory = Path.Combine(workspace.Root, "round");
        Assert.IsTrue((await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Intent, directory)).IsSuccess);

        await Answer(directory, "rules", rules);
        await Answer(directory, "security", security);
        await Answer(directory, "operability", Silent);
        await Answer(directory, "maintainability", Silent);

        return directory;
    }

    private static Task Answer(string directory, string reader, string body) =>
        File.WriteAllTextAsync(ReaderAnswers.AnswerPath(directory, reader), ReaderPromptsTests.Envelope(body));

    private static async Task<Result<CollectRound.Response>> Collect(CycleWorkspace workspace, string directory)
    {
        using var gate = Gate.For(workspace.Root);
        return await gate.Send<CollectRound.Command, CollectRound.Response>(
            new CollectRound.Command(workspace.Cycle.Id, ArtifactKind.Intent, directory, "claude"));
    }

    private static async Task RecordDraft(CycleWorkspace workspace, string path)
    {
        using var gate = Gate.For(workspace.Root);
        Result<RecordRound.Response> recorded = await gate.Send<RecordRound.Command, RecordRound.Response>(
            new RecordRound.Command(workspace.Cycle.Id, ArtifactKind.Intent, path));
        Assert.IsTrue(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(error => error.Message)));
    }

    private static FakeRepository Merged(CycleWorkspace workspace, string branch)
    {
        FakeRepository git = new() { Branch = branch };
        git.Refs.Add("origin/development");
        git.History["origin/development:.sdlc/cycles/" + workspace.Cycle.Id] = [("first", DateTimeOffset.UnixEpoch), ("merged", DateTimeOffset.UnixEpoch.AddDays(1))];
        return git;
    }

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

    private static async Task<HarvestReport.Response> Report(CycleWorkspace workspace, FakeRepository git, HarvestAspect aspect)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<HarvestReport.Response> report = await gate.Send<HarvestReport.Query, HarvestReport.Response>(new HarvestReport.Query(aspect));
        Assert.IsTrue(report.IsSuccess, string.Join("; ", report.Errors.Select(error => error.Message)));
        return report.Value;
    }
}
