using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;

namespace Sdlc.Tests;

/// <summary>
/// 'sdlc round collect' and 'sdlc round verdict' (D-117), and the usage a recorded round leaves (D-121).
/// </summary>
[TestClass]
public sealed class CollectRoundTests
{
    private const string Blocker = """
        { "findings": [ { "id": "F_01", "severity": "blocking",
          "cites": { "id": "CON_01", "heading": "An intent states the problem, not the solution" },
          "gist": "The problem names the fix.", "statement": "It prescribes a button.", "locus": "Problem" } ] }
        """;

    private const string Silent = """{ "findings": [] }""";

    [TestMethod]
    public async Task A_new_blocker_is_numbered_past_the_ledger_and_a_perspectives_suggestion_is_put_under_its_name()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteBlockingRound(ArtifactKind.Intent, id: "F_01", cites: "CON_02", heading: "An intent's unknowns are recorded as open questions, not answered by guess");
        string directory = await Answers(workspace, rules: Blocker, security: """{ "findings": [ { "id": "S_09", "severity": "suggestion", "statement": "Say who may roll." } ] }""");
        await Answer(directory, "reconcile", """{ "restatements": [], "pairs": [] }""");

        CollectRound.Response drafted = (await Collect(workspace, directory)).Value;

        CollectionAssert.AreEquivalent(new[] { "F_02", "security.S_01" }, drafted.Raised.Select(finding => finding.Id).ToArray());
        Assert.AreEqual("security", drafted.Raised.Single(finding => finding.Id == "security.S_01").Perspective);
        Assert.AreEqual("F_01", drafted.NotRaised.Single().Id, "The standing blocker no reader raised is left for the session (D-49).");
        Assert.AreEqual(5, drafted.Usage.Count, "Four readers and the reconciler.");
    }

    [TestMethod]
    public async Task A_restatement_is_carried_under_the_standing_id_in_the_ledgers_words()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteBlockingRound(ArtifactKind.Intent, id: "F_01", cites: "CON_01", heading: "An intent states the problem, not the solution");
        string directory = await Answers(workspace, rules: Blocker);
        await Answer(directory, "reconcile", """{ "restatements": [ { "finding": "F_01", "standing": "F_01" } ], "pairs": [] }""");

        CollectRound.Response drafted = (await Collect(workspace, directory)).Value;

        Finding carried = drafted.Raised.Single();
        Assert.AreEqual("F_01", carried.Id);
        Assert.AreEqual("something that must be answered.", carried.Statement, "The ledger's words, not the reader's.");
        Assert.AreEqual(0, drafted.NotRaised.Count);
    }

    [TestMethod]
    public async Task Two_findings_resting_on_no_rule_that_cannot_both_be_answered_become_suggestions_naming_each_other()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string directory = await Answers(
            workspace,
            security: """{ "findings": [ { "id": "S_01", "severity": "suggestion", "statement": "Log every roll." } ] }""",
            operability: """{ "findings": [ { "id": "S_01", "severity": "suggestion", "statement": "Log nothing." } ] }""");
        await Answer(directory, "reconcile", """{ "restatements": [], "pairs": [ { "between": [ "security.S_01", "operability.S_01" ], "why": "one logs, one does not" } ] }""");

        CollectRound.Response drafted = (await Collect(workspace, directory)).Value;

        StringAssert.Contains(drafted.Raised.Single(f => f.Perspective == "security").Statement, "Contradicts `operability.S_01`.");
        StringAssert.Contains(drafted.Raised.Single(f => f.Perspective == "operability").Statement, "Contradicts `security.S_01`.");
    }

    [TestMethod]
    public async Task A_round_with_findings_and_no_reconciler_is_refused()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string directory = await Answers(workspace, rules: Blocker);

        Result<CollectRound.Response> refused = await Collect(workspace, directory);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "reconcile");
    }

    [TestMethod]
    public async Task A_blocker_without_a_gist_sends_its_reader_back()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string directory = await Answers(workspace, rules: Blocker.Replace("\"gist\": \"The problem names the fix.\", ", string.Empty, StringComparison.Ordinal));

        Result<CollectRound.Response> refused = await Collect(workspace, directory);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "Start it again");
    }

    [TestMethod]
    public async Task Usage_reaches_the_cycle_when_the_round_is_recorded_and_not_before()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string directory = await Answers(workspace);
        CollectRound.Response drafted = (await Collect(workspace, directory)).Value;

        Assert.IsFalse(File.Exists(UsageLedger.PathOf(workspace.Cycle)), "A drafted round that is never recorded leaves no usage.");

        using (var gate = Gate.For(workspace.Root, services => services.AddSingleton<TimeProvider>(new FakeClock(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero)))))
        {
            Result<RecordRound.Response> recorded = await gate.Send<RecordRound.Command, RecordRound.Response>(
                new RecordRound.Command(workspace.Cycle.Id, ArtifactKind.Intent, drafted.Path));
            Assert.IsTrue(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(error => error.Message)));
        }

        string usage = await File.ReadAllTextAsync(UsageLedger.PathOf(workspace.Cycle));
        using var document = JsonDocument.Parse(usage);
        JsonElement round = document.RootElement.GetProperty("rounds")[0];

        Assert.AreEqual("intent", round.GetProperty("artifact").GetString());
        Assert.AreEqual("claude", round.GetProperty("host").GetString(), "The host as SupportedHosts spells it.");
        Assert.AreEqual(4, round.GetProperty("readers").GetArrayLength());
        Assert.AreEqual(7035, round.GetProperty("readers")[0].GetProperty("cache_write").GetInt64());
        Assert.AreEqual(JsonValueKind.Null, round.GetProperty("readers")[0].GetProperty("effort").ValueKind, "Unreported is null, never 0.");
        Assert.IsFalse(usage.Contains("cost", StringComparison.OrdinalIgnoreCase), "No dollar figure.");
    }

    [TestMethod]
    public async Task The_verdict_is_written_byte_for_byte_and_prose_is_refused()
    {
        using var workspace = CycleWorkspace.Create();
        string verdict = "---\nframework_version: 1.0.0\ncycle: 20260929_1456_32\noutcome: pass\n---\n\n# Rolls\n\n## Criteria\n\n1. Met. `Rolls` passed.\n";
        string from = Path.Combine(workspace.Root, "adjudicate.json");

        await File.WriteAllTextAsync(from, ReaderPromptsTests.Envelope("Here is the verdict:\n\n" + verdict));
        Assert.IsTrue((await Verdict(workspace, from)).IsFailed, "Something before the frontmatter is the session's to start again, not repair.");

        await File.WriteAllTextAsync(from, ReaderPromptsTests.Envelope(verdict));
        WriteVerdict.Response written = (await Verdict(workspace, from)).Value;

        Assert.AreEqual("pass", written.Outcome);
        Assert.AreEqual(verdict, await File.ReadAllTextAsync(workspace.Cycle.PathOf(ArtifactKind.Verify)));
    }

    [TestMethod]
    public async Task A_fence_around_the_whole_verdict_is_taken_off_and_one_inside_it_is_kept()
    {
        // D-161. A fenced verdict was refused, and the rerun cost the largest prompt the framework sends.
        using var workspace = CycleWorkspace.Create();
        string verdict = "---\ncycle: 20260929_1456_32\noutcome: blocked\n---\n\n# Rolls\n\n## Criteria\n\n1. Not met.\n\n```\nAssert.AreEqual(1, roll);\n```\n";
        string from = Path.Combine(workspace.Root, "adjudicate.json");

        await File.WriteAllTextAsync(from, ReaderPromptsTests.Envelope("```markdown\n" + verdict + "```\n"));
        WriteVerdict.Response written = (await Verdict(workspace, from)).Value;

        Assert.AreEqual("blocked", written.Outcome);
        Assert.AreEqual(verdict, await File.ReadAllTextAsync(workspace.Cycle.PathOf(ArtifactKind.Verify)));

        await File.WriteAllTextAsync(from, ReaderPromptsTests.Envelope("Here it is:\n```markdown\n" + verdict + "```\n"));
        Assert.IsTrue((await Verdict(workspace, from)).IsFailed, "Prose beside the fence is still prose.");
    }

    private static async Task<string> Answers(
        CycleWorkspace workspace,
        string rules = Silent,
        string security = Silent,
        string operability = Silent,
        string maintainability = Silent)
    {
        string directory = Path.Combine(workspace.Root, "round");
        Assert.IsTrue((await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Intent, directory)).IsSuccess);

        await Answer(directory, "rules", rules);
        await Answer(directory, "security", security);
        await Answer(directory, "operability", operability);
        await Answer(directory, "maintainability", maintainability);

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

    private static async Task<Result<WriteVerdict.Response>> Verdict(CycleWorkspace workspace, string from)
    {
        using var gate = Gate.For(workspace.Root);
        return await gate.Send<WriteVerdict.Command, WriteVerdict.Response>(new WriteVerdict.Command(workspace.Cycle.Id, from));
    }
}
