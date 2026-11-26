using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Findings;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// What a document cites by number is held to what exists, a suggestion is one concern under one id, and a new
/// working copy says what of this checkout's record it will not hold (D-223).
/// </summary>
[TestClass]
public sealed class CitationsTests
{
    private const string Record = """
        # Decisions

        ## DEC_01 — Charts are drawn on a canvas

        **Decided:** a.

        **Because:** b.

        **Instead of:** c.

        **From:** d.

        ## DEC_02 — Prices come from one source

        **Decided:** a.

        **Because:** b.

        **Instead of:** c.

        **From:** d.
        """;

    [TestMethod]
    public void A_decision_the_record_does_not_hold_is_named_once_however_often_it_is_cited()
    {
        IReadOnlyList<SchemaViolation> bad = Citations.Decisions(ArtifactKind.Intent, Intent("Rests on DEC_09 and DEC_01; DEC_09 again."), Decisions());

        SchemaViolation only = bad.Single();
        StringAssert.Contains(only.Problem, "cites DEC_09, and decisions.md here holds DEC_01 to DEC_02");
        StringAssert.Contains(only.Problem, "not yet on the integration branch");
    }

    [TestMethod]
    public void A_departure_line_and_a_fenced_block_are_left_to_what_reads_them()
    {
        string intent = Intent("- Departs from DEC_07 (charts): an SVG, because the person said so.\n\n```\nDEC_08 in a quoted log\n```");

        Assert.AreEqual(0, Citations.Decisions(ArtifactKind.Intent, intent, Decisions()).Count, "The departure check names DEC_07; a fence quotes rather than cites.");
    }

    [TestMethod]
    public void With_no_record_every_cited_decision_points_at_nothing()
    {
        StringAssert.Contains(Citations.Decisions(ArtifactKind.Plan, Intent("Keeps DEC_03."), []).Single().Problem, "holds none");
    }

    [TestMethod]
    public void A_plan_answering_a_criterion_the_spec_does_not_have_is_named()
    {
        string plan = "## Proof\n\n1. `A` asserts it.\n2. `B` asserts it.\n3. `C` asserts it.\n";

        SchemaViolation bad = Citations.Criteria(ArtifactKind.Plan, plan, Spec(2)).Single();

        Assert.AreEqual("## Proof", bad.Where);
        StringAssert.Contains(bad.Problem, "criterion 3, and the spec has criteria 1 to 2");
    }

    [TestMethod]
    [DataRow("as criterion 4 says", 4, DisplayName = "one number")]
    [DataRow("criteria 1, 2a and 5", 5, DisplayName = "a list with a case")]
    [DataRow("criteria 2-4", 4, DisplayName = "a range read out")]
    [DataRow("Criterion #6 covers it", 6, DisplayName = "a hash and a capital")]
    public void A_reference_to_a_criterion_the_spec_does_not_have_is_named(string text, int missing)
    {
        SchemaViolation bad = Citations.Criteria(ArtifactKind.Verify, "## Summary\n\n" + text + ".\n", Spec(3)).Single();

        StringAssert.Contains(bad.Problem, "cites criterion " + missing.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",");
    }

    [TestMethod]
    [DataRow("restates another cycle's criterion 9")]
    [DataRow("as 20261001_0900_00's criterion 9 does")]
    [DataRow("successor:charts keeps criterion 9")]
    public void A_reference_naming_another_cycle_is_left_alone(string text)
    {
        Assert.AreEqual(0, Citations.Criteria(ArtifactKind.Spec, Spec(3).Replace("Nothing else.", text, StringComparison.Ordinal), null).Count);
    }

    [TestMethod]
    public void A_spec_numbering_its_criteria_out_of_order_is_named()
    {
        string spec = Spec(3).Replace("2. ", "4. ", StringComparison.Ordinal);

        StringAssert.Contains(Citations.Criteria(ArtifactKind.Spec, spec, null).Single().Problem, "criterion 3 after criterion 4");
    }

    [TestMethod]
    public async Task A_round_is_not_composed_for_an_intent_citing_a_decision_the_record_does_not_hold()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent, "Rests on DEC_25.");
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", "decisions.md"), Record);

        Result<ReaderPrompts.Response> refused = await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Intent, Path.Combine(workspace.Root, "round"));

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "intent.md cites what does not exist, so no reader is started");
        StringAssert.Contains(refused.Errors[0].Message, "DEC_25");
    }

    [TestMethod]
    public async Task A_cycle_opened_before_the_release_is_not_refused_for_it()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent, "Rests on DEC_25.");
        workspace.WriteRound(ArtifactKind.Intent, 1, findings: string.Empty, release: "0.57.0");

        Result<ReaderPrompts.Response> composed = await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Intent, Path.Combine(workspace.Root, "round"));

        Assert.IsTrue(composed.IsSuccess, string.Join("; ", composed.Errors.Select(error => error.Message)));
    }

    [TestMethod]
    public async Task Two_readers_raising_the_same_words_are_one_suggestion()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string directory = Path.Combine(workspace.Root, "round");
        Assert.IsTrue((await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Intent, directory)).IsSuccess);
        await Answer(directory, "rules", """{ "findings": [] }""");
        await Answer(directory, "security", """{ "findings": [ { "id": "S_01", "severity": "suggestion", "statement": "Say who may roll." } ] }""");
        await Answer(directory, "operability", """{ "findings": [ { "id": "S_01", "severity": "suggestion", "statement": "say who  may roll." } ] }""");
        await Answer(directory, "maintainability", """{ "findings": [ { "id": "S_01", "severity": "suggestion", "statement": "Name the roll's owner.", "gist": " " } ] }""");
        await Answer(directory, "reconcile", """{ "restatements": [], "pairs": [] }""");

        CollectRound.Response drafted = (await Collect(workspace, directory)).Value;

        Assert.AreEqual(2, drafted.Raised.Count, "The copy is the first one's concern, whatever the reconciler said.");
        Assert.AreEqual("security.S_01", drafted.Restated["operability.S_01"]);
        Assert.IsNull(drafted.Raised.Single(finding => finding.Perspective == "maintainability").Gist, "An empty gist is no gist.");
    }

    [TestMethod]
    public async Task A_suggestion_in_the_words_of_one_the_ledger_holds_is_carried_under_its_id()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string first = Path.Combine(workspace.Root, "first");
        await RoundOf(workspace, first, """{ "findings": [ { "id": "S_01", "severity": "suggestion", "statement": "Say who may roll." } ] }""");
        Result<CollectRound.Response> once = await Collect(workspace, first);
        Assert.IsTrue((await RecordIt(workspace, once.Value.Path)).IsSuccess);

        workspace.WriteArtifact(ArtifactKind.Intent, "moved");
        string second = Path.Combine(workspace.Root, "second");
        await RoundOf(workspace, second, """{ "findings": [ { "id": "S_01", "severity": "suggestion", "statement": "Say who may roll." } ] }""");

        CollectRound.Response drafted = (await Collect(workspace, second)).Value;

        Assert.AreEqual("security.S_01", drafted.Raised.Single().Id, "The reconciler named nothing, and the same words are a lookup.");
    }

    [TestMethod]
    public void The_ledger_refuses_a_new_suggestion_with_nothing_to_read_or_in_another_ones_words()
    {
        var ledger = SuggestionLedger.Empty("20260929_1456_32");
        HashSet<string> taken = [];

        StringAssert.Contains(Refusal(ledger.Record(ArtifactKind.Plan, 1, [Suggestion("S_01", " ")], taken)), "has no statement");
        StringAssert.Contains(Refusal(ledger.Record(ArtifactKind.Plan, 1, [Suggestion("S_01", "Say it.") with { Gist = "" }], taken)), "has an empty gist");
        StringAssert.Contains(
            Refusal(ledger.Record(ArtifactKind.Plan, 1, [Suggestion("S_01", "Say it."), Suggestion("S_02", "say  it.")], taken)),
            "S_02 says word for word what S_01 says");
    }

    [TestMethod]
    public async Task A_new_sandbox_says_which_of_this_checkouts_record_it_will_not_hold()
    {
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", "decisions.md"), Record);
        FakeRepository git = new();
        git.Refs.Add("origin/development");
        git.Files["origin/development:.sdlc/decisions.md"] = Record.Replace("## DEC_02", "## DEC_03", StringComparison.Ordinal);

        StartBranch.Response start = await Start(workspace, git);

        StringAssert.Contains(start.Why, "This checkout's decisions.md differs from origin/development's");

        git.Files["origin/development:.sdlc/decisions.md"] = Record;
        Assert.IsFalse((await Start(workspace, git)).Why.Contains("differs", StringComparison.Ordinal), "The same record says nothing.");
    }

    private static IReadOnlyList<Decision> Decisions() => DecisionParser.Parse(Record, "decisions.md").Value;

    private static string Intent(string body) => "---\ncycle: 20260929_1456_32\n---\n\n# An intent\n\n## Problem\n\n" + body + "\n";

    private static string Spec(int criteria) =>
        "---\ncycle: 20260929_1456_32\n---\n\n# A spec\n\n## Acceptance criteria\n\n"
        + string.Concat(Enumerable.Range(1, criteria).Select(number => number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ". Something happens.\n"))
        + "\n## Out of scope\n\nNothing else.\n";

    private static Finding Suggestion(string id, string statement) =>
        new(id, Severity.Suggestion, Disposition.Open, null, statement);

    private static string Refusal(Result<SuggestionLedger> result)
    {
        Assert.IsTrue(result.IsFailed);
        return result.Errors[0].Message;
    }

    private static async Task RoundOf(CycleWorkspace workspace, string directory, string security)
    {
        Assert.IsTrue((await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Intent, directory)).IsSuccess);

        foreach (string reader in (string[])["rules", "operability", "maintainability"])
        {
            await Answer(directory, reader, """{ "findings": [] }""");
        }

        await Answer(directory, "security", security);
        await Answer(directory, "reconcile", """{ "restatements": [], "pairs": [] }""");
    }

    private static Task Answer(string directory, string reader, string body) =>
        File.WriteAllTextAsync(ReaderAnswers.AnswerPath(directory, reader), ReaderPromptsTests.Envelope(body));

    private static async Task<Result<CollectRound.Response>> Collect(CycleWorkspace workspace, string directory)
    {
        using var gate = Gate.For(workspace.Root);
        return await gate.Send<CollectRound.Command, CollectRound.Response>(new CollectRound.Command(workspace.Cycle.Id, ArtifactKind.Intent, directory, "claude"));
    }

    private static async Task<Result<RecordRound.Response>> RecordIt(CycleWorkspace workspace, string from)
    {
        using var gate = Gate.For(workspace.Root);
        return await gate.Send<RecordRound.Command, RecordRound.Response>(new RecordRound.Command(workspace.Cycle.Id, ArtifactKind.Intent, from));
    }

    private static async Task<StartBranch.Response> Start(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<StartBranch.Response> start = await gate.Send<StartBranch.Query, StartBranch.Response>(
            new StartBranch.Query(workspace.Cycle.Id, CycleBranchKind.Plan, Sandbox: true));

        Assert.IsTrue(start.IsSuccess, string.Join("; ", start.Errors.Select(error => error.Message)));
        return start.Value;
    }
}
