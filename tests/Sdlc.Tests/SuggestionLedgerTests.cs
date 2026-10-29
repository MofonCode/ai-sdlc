using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Hooks;

namespace Sdlc.Tests;

/// <summary>
/// A cycle's suggestions in one ledger, each concern once, with a history of events per document (D-115).
/// </summary>
[TestClass]
public sealed class SuggestionLedgerTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 29, 18, 0, 0, TimeSpan.Zero);

    private const string Statement = "A canvas chart gives a screen-reader user nothing to read.";

    [TestMethod]
    public async Task A_suggestion_lands_in_the_cycle_ledger_and_still_reads_in_its_round()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);

        Result<RecordRound.Response> written = await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01"));

        Assert.IsTrue(written.IsSuccess, Errors(written));
        Assert.IsTrue(File.Exists(workspace.Cycle.SuggestionsLedgerPath), "The suggestion has a ledger of its own.");
        Assert.IsFalse(
            (await File.ReadAllTextAsync(workspace.Cycle.FindingsLedgerPath(ArtifactKind.Intent))).Contains("S_01", StringComparison.Ordinal),
            "The intent's ledger keeps its blockers only.");
        Assert.AreEqual("accessibility.S_01", written.Value.Written.Findings.Single().Id, "The round's view still shows it.");

        IReadOnlyList<FindingsRound> history = (await FindingsReader.ReadHistory(workspace.Cycle, ArtifactKind.Intent, CancellationToken.None)).Value;
        Assert.AreEqual(1, FindingsReader.StandingSuggestions(history).Count, "Every consumer reads it through the rounds.");
    }

    [TestMethod]
    public async Task A_restatement_where_it_stands_records_nothing_and_another_document_raising_it_does()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01"));
        await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01"));
        workspace.WriteArtifact(ArtifactKind.Spec);

        Result<RecordRound.Response> spec = await Record(workspace, ArtifactKind.Spec, Raise("accessibility.S_01"));

        Assert.IsTrue(spec.IsSuccess, Errors(spec));
        LedgerSuggestion suggestion = (await Ledger(workspace)).Suggestions.Single();
        CollectionAssert.AreEqual(
            new[] { "intent 1 Raised", "spec 1 Raised" },
            suggestion.History.Select(line => $"{line.Artifact} {line.Round} {line.Event}").ToArray());
    }

    [TestMethod]
    public async Task Answered_and_raised_again_are_lines_in_the_order_they_happened()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01"));
        await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01", "answered"));
        await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01"));

        LedgerSuggestion suggestion = (await Ledger(workspace)).Suggestions.Single();

        CollectionAssert.AreEqual(
            new[] { SuggestionEventKind.Raised, SuggestionEventKind.Answered, SuggestionEventKind.Raised },
            suggestion.History.Select(line => line.Event).ToArray());
        Assert.IsTrue(suggestion.IsOpen);
    }

    [TestMethod]
    public async Task One_document_still_raising_it_keeps_it_open()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01"));
        workspace.WriteArtifact(ArtifactKind.Spec);
        await Record(workspace, ArtifactKind.Spec, Raise("accessibility.S_01"));
        workspace.WriteArtifact(ArtifactKind.Intent, "answered it");
        await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01", "answered"));

        Assert.IsTrue((await Ledger(workspace)).Suggestions.Single().IsOpen, "The intent going quiet does not answer what the spec raised.");
    }

    [TestMethod]
    public async Task A_new_suggestion_under_a_blocker_style_id_is_refused_and_named_the_next_free_one()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01"));

        Result<RecordRound.Response> refused = await Record(workspace, ArtifactKind.Intent, Raise("accessibility.F_01", statement: "Another concern."));

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "accessibility.S_02");
    }

    [TestMethod]
    public async Task A_known_id_in_other_words_is_refused()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01"));

        Result<RecordRound.Response> refused = await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01", statement: "Reworded."));

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "different concern");
    }

    [TestMethod]
    public async Task A_round_cannot_answer_what_its_document_never_raised()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01"));
        workspace.WriteArtifact(ArtifactKind.Spec);

        Result<RecordRound.Response> refused = await Record(workspace, ArtifactKind.Spec, Raise("accessibility.S_01", "answered"));

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "never raised against spec.md");
    }

    [TestMethod]
    public async Task A_suggestion_cannot_be_turned_into_a_blocker_under_its_own_id()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await Record(workspace, ArtifactKind.Intent, Raise("accessibility.S_01"));

        Result<RecordRound.Response> refused = await Record(workspace, ArtifactKind.Intent, """
            { "findings": [ { "id": "accessibility.S_01", "severity": "blocking", "disposition": "open",
              "cites": { "id": "CON_06", "heading": "a plan's proof is adequate to the criterion it claims to prove" },
              "statement": "x", "gist": "y" } ] }
            """);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "takes an F_ id of its own");
    }

    [TestMethod]
    public async Task A_suggestion_raised_before_this_stays_in_its_document_ledger()
    {
        // MarketSense's cycles hold suggestions inside each document's ledger. A later round moving one
        // moves it there, where its history is, rather than splitting it across two files.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        CycleWorkspace.WriteRaw(
            workspace.Cycle.FindingsPath(ArtifactKind.Intent, 1),
            """{ "round": 1, "interrogated_at": "2026-10-01T10:00:00Z", "findings": [ { "id": "accessibility.F_01", "severity": "suggestion", "disposition": "open", "perspective": "accessibility", "statement": "old" } ] }""");

        Result<RecordRound.Response> answered = await Record(workspace, ArtifactKind.Intent, Raise("accessibility.F_01", "answered", "old"));

        Assert.IsTrue(answered.IsSuccess, Errors(answered));
        Assert.IsFalse(File.Exists(workspace.Cycle.SuggestionsLedgerPath));
        Assert.AreEqual(0, answered.Value.Standing.Count);
    }

    [TestMethod]
    public void Two_raised_in_a_row_against_one_document_is_an_edited_file()
    {
        string json = """
            { "suggestions": [ { "id": "S_01", "statement": "x", "history": [
              { "artifact": "intent", "round": 1, "event": "raised" },
              { "artifact": "intent", "round": 2, "event": "raised" } ] } ] }
            """;

        Result<SuggestionLedger> read = FindingsReader.ReadSuggestions(json, "suggestions.json");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "only sdlc round writes this file");
    }

    [TestMethod]
    public void Raised_against_two_documents_in_a_row_is_not()
    {
        string json = """
            { "suggestions": [ { "id": "S_01", "statement": "x", "history": [
              { "artifact": "intent", "round": 1, "event": "raised" },
              { "artifact": "plan", "round": 1, "event": "raised" } ] } ] }
            """;

        Assert.IsTrue(FindingsReader.ReadSuggestions(json, "suggestions.json").IsSuccess);
    }

    [TestMethod]
    [DataRow("Write", ".sdlc/cycles/x/findings/suggestions.json")]
    [DataRow("Bash", "echo {} > .sdlc/cycles/x/findings/suggestions.json")]
    public void An_agent_cannot_write_the_suggestions_ledger(string tool, string target)
    {
        string payload = tool == "Write"
            ? JsonSerializer.Serialize(new { tool_name = tool, tool_input = new { file_path = target } })
            : JsonSerializer.Serialize(new { tool_name = tool, tool_input = new { command = target } });

        HookVerdict verdict = HookPolicy.Decide(HookProtocol.ReadCall(payload, HookHost.Claude).Value);

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(verdict.Reason, "sdlc round");
    }

    private static string Raise(string id, string disposition = "open", string statement = Statement) =>
        JsonSerializer.Serialize(new
        {
            findings = new[]
            {
                new { id, severity = "suggestion", disposition, perspective = "accessibility", statement },
            },
        });

    private static async Task<SuggestionLedger> Ledger(CycleWorkspace workspace) =>
        (await FindingsReader.ReadSuggestions(workspace.Cycle, CancellationToken.None)).Value;

    private static string Errors<T>(Result<T> result) => string.Join("; ", result.Errors.Select(error => error.Message));

    private static async Task<Result<RecordRound.Response>> Record(CycleWorkspace workspace, ArtifactKind kind, string json)
    {
        string from = Path.Combine(workspace.Root, "readers." + Cycle.NameOf(kind) + ".json");
        await File.WriteAllTextAsync(from, json);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<TimeProvider>(new FakeClock(_now)));
        return await gate.Send<RecordRound.Command, RecordRound.Response>(
            new RecordRound.Command(workspace.Cycle.Id, kind, from));
    }
}
