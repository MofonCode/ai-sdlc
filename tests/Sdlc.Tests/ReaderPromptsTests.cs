using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;

namespace Sdlc.Tests;

/// <summary>
/// 'sdlc round prompt' (D-117): the binary writes every reader's prompt, the shared block first and the
/// same for each, and each reader's own part last.
/// </summary>
[TestClass]
public sealed class ReaderPromptsTests
{
    private const string Ask = "# What you are asked";

    [TestMethod]
    public async Task Every_blind_reader_shares_one_prefix_and_only_the_rules_reader_is_given_the_rules()
    {
        // A shared prefix is what a host can cache and a replay can compare. The rules stay in the rules
        // reader's part, because a perspective is not handed the constraint set and would judge differently.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent, "## Problem\n\nThe die is hard to read.");
        string directory = Path.Combine(workspace.Root, "round");

        ReaderPrompts.Response written = (await Prompt(workspace, ArtifactKind.Intent, directory)).Value;

        CollectionAssert.AreEqual(
            new[] { "rules", "security", "operability", "maintainability" },
            written.Prompts.Select(prompt => prompt.Reader).ToArray(),
            "The rules reader, then the three perspectives that are always on.");

        string[] texts = [.. written.Prompts.Select(prompt => File.ReadAllText(prompt.Path))];
        string prefix = texts[0][..texts[0].IndexOf(Ask, StringComparison.Ordinal)];

        foreach (string text in texts)
        {
            Assert.IsTrue(text.StartsWith(prefix, StringComparison.Ordinal), "Every reader's prompt opens with the same bytes.");
        }

        StringAssert.Contains(prefix, "The die is hard to read.");
        StringAssert.Contains(prefix, "None -- this is the first artifact.");
        StringAssert.Contains(texts[0], "CON_01");
        Assert.IsFalse(texts[1].Contains("## CON_01", StringComparison.Ordinal), "A perspective is not handed the rule set.");
        StringAssert.Contains(texts[1], "security.F_01");
    }

    [TestMethod]
    public async Task A_plan_round_carries_the_stack_file_and_says_so_when_there_is_none()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Spec);
        workspace.WriteArtifact(ArtifactKind.Plan);
        string directory = Path.Combine(workspace.Root, "round");

        string absent = await File.ReadAllTextAsync((await Prompt(workspace, ArtifactKind.Plan, directory, "rules")).Value.Prompts[0].Path);
        StringAssert.Contains(absent, "This solution has no techstack.md: it declares nothing.");

        workspace.WriteTechStack("| Concern | Default |\n|---|---|\n| Testing | MSTest |\n");
        string present = await File.ReadAllTextAsync((await Prompt(workspace, ArtifactKind.Plan, directory, "rules")).Value.Prompts[0].Path);
        StringAssert.Contains(present, "| Testing | MSTest |");
    }

    [TestMethod]
    public async Task A_verify_round_carries_the_criteria_and_the_evidence_and_the_build_log_only_where_there_is_one()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSpec(criteria: 2);
        workspace.WritePlan(proofs: 2);
        workspace.WriteTestSummary(proofs: 2);
        workspace.WriteVerify("pass");
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Cycle.EvidenceDirectory, "diff.patch"), "+Assert.AreEqual(1, roll);\n");
        string directory = Path.Combine(workspace.Root, "round");

        string first = await File.ReadAllTextAsync((await Prompt(workspace, ArtifactKind.Verify, directory, "rules")).Value.Prompts[0].Path);

        StringAssert.Contains(first, "<file name=\"spec.md\">");
        StringAssert.Contains(first, "<file name=\"evidence/tests.json\">");
        StringAssert.Contains(first, "+Assert.AreEqual(1, roll);");
        StringAssert.Contains(first, "Find each quote in the diff");
        Assert.IsFalse(first.Contains("evidence/build.log", StringComparison.Ordinal), "No build log, no block claiming one.");

        CycleWorkspace.WriteRaw(Path.Combine(workspace.Cycle.EvidenceDirectory, "build.log"), "Build succeeded.\n");
        string second = await File.ReadAllTextAsync((await Prompt(workspace, ArtifactKind.Verify, directory, "rules")).Value.Prompts[0].Path);
        StringAssert.Contains(second, "Build succeeded.");
    }

    [TestMethod]
    public async Task A_proof_reader_and_the_adjudicator_block_on_what_a_test_never_reaches_and_not_on_what_they_imagine()
    {
        // D-127. "What would let it pass while unmet" had no bound: a fresh reader always found one more
        // defect a test would miss, and one plan spent 44 rounds that way. Both prompts now ask for the case
        // a test never runs and the outcome it never reads, and send the imagined defect to a suggestion.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSpec(criteria: 1);
        workspace.WritePlan(proofs: 1);
        workspace.WriteTestSummary(proofs: 1);
        workspace.WriteVerify("pass");
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Cycle.EvidenceDirectory, "diff.patch"), "+Assert.AreEqual(1, roll);\n");
        string directory = Path.Combine(workspace.Root, "round");

        string rules = await File.ReadAllTextAsync((await Prompt(workspace, ArtifactKind.Verify, directory, "rules")).Value.Prompts[0].Path);
        string adjudicate = await File.ReadAllTextAsync((await Prompt(workspace, ArtifactKind.Verify, directory, "adjudicate")).Value.Prompts[0].Path);

        foreach (string prompt in new[] { rules, adjudicate })
        {
            StringAssert.Contains(prompt, "never read");
            Assert.IsFalse(prompt.Contains("pass while the criterion is unmet", StringComparison.Ordinal), "The unbounded question is gone.");
        }

        StringAssert.Contains(rules, "A defect you can only\nimagine the test missing is a suggestion.");
        StringAssert.Contains(rules, "CON_22");
    }

    [TestMethod]
    public async Task The_adjudicator_is_composed_for_a_verify_alone_and_is_not_handed_a_verdict_to_judge()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSpec(criteria: 1);
        workspace.WritePlan(proofs: 1);
        string directory = Path.Combine(workspace.Root, "round");

        Assert.IsTrue((await Prompt(workspace, ArtifactKind.Plan, directory, "adjudicate")).IsFailed);

        string prompt = await File.ReadAllTextAsync((await Prompt(workspace, ArtifactKind.Verify, directory, "adjudicate")).Value.Prompts[0].Path);

        StringAssert.Contains(prompt, "Write `verify.md` for cycle `20260929_1456_32`");
        StringAssert.Contains(prompt, "## Adjudicate criterion by criterion");
        Assert.IsFalse(prompt.Contains("The document to judge:", StringComparison.Ordinal), "The adjudicator writes the verdict; it is not judging one.");
    }

    [TestMethod]
    public async Task Asking_for_the_list_writes_nothing()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string directory = Path.Combine(workspace.Root, "round");

        ReaderPrompts.Response listed = (await Prompt(workspace, ArtifactKind.Intent, directory, list: true)).Value;

        Assert.AreEqual(4, listed.Readers.Count);
        Assert.AreEqual(0, listed.Prompts.Count);
        Assert.IsFalse(Directory.Exists(directory));
    }

    [TestMethod]
    public async Task The_reconciler_reads_every_answer_with_each_perspectives_ids_under_its_name()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string directory = Path.Combine(workspace.Root, "round");
        await Prompt(workspace, ArtifactKind.Intent, directory);

        await File.WriteAllTextAsync(ReaderAnswers.AnswerPath(directory, "rules"), Envelope("""{ "findings": [] }"""));
        await File.WriteAllTextAsync(ReaderAnswers.AnswerPath(directory, "security"), Envelope(
            """{ "findings": [ { "id": "F_01", "severity": "suggestion", "statement": "Say who may roll." } ] }"""));
        await File.WriteAllTextAsync(ReaderAnswers.AnswerPath(directory, "operability"), """{ "findings": [] }""");
        await File.WriteAllTextAsync(ReaderAnswers.AnswerPath(directory, "maintainability"), "```json\n{ \"findings\": [] }\n```");

        Result<ReaderPrompts.Response> reconcile = await Prompt(workspace, ArtifactKind.Intent, directory, "reconcile");

        Assert.IsTrue(reconcile.IsSuccess, string.Join("; ", reconcile.Errors.Select(error => error.Message)));
        string prompt = await File.ReadAllTextAsync(reconcile.Value.Prompts[0].Path);
        StringAssert.Contains(prompt, "\"id\": \"security.F_01\"");
        StringAssert.Contains(prompt, "Say who may roll.");
        Assert.IsTrue(File.Exists(ReaderAnswers.AnswerPath(directory, "rules")), "Composing the reconciler clears nothing.");
    }

    [TestMethod]
    public void A_hosts_result_object_gives_the_answer_and_its_usage_and_plain_text_gives_nulls()
    {
        ReaderAnswer wrapped = ReaderAnswers.Parse("rules", Envelope("""{ "findings": [] }"""), 1200, "rules.json").Value;

        Assert.AreEqual("""{ "findings": [] }""", wrapped.Text);
        Assert.AreEqual("claude-opus-5-5", wrapped.Usage.Model);
        Assert.AreEqual(9L, wrapped.Usage.Input);
        Assert.AreEqual(7035L, wrapped.Usage.CacheWrite);
        Assert.AreEqual(4388L, wrapped.Usage.CacheRead);
        Assert.AreEqual(352L, wrapped.Usage.Output);
        Assert.AreEqual(1, wrapped.Usage.Turns);
        Assert.AreEqual(4.2, wrapped.Usage.Seconds);
        Assert.AreEqual(1200L, wrapped.Usage.PromptBytes);
        Assert.IsNull(wrapped.Usage.Effort, "The host does not say, so it is not 0 or a guess.");

        ReaderAnswer plain = ReaderAnswers.Parse("rules", "{ \"findings\": [] }", null, "rules.json").Value;
        Assert.IsNull(plain.Usage.Input, "A host that reports nothing is null, never 0 (D-121).");
    }

    [TestMethod]
    public void A_reader_process_that_failed_is_refused_rather_than_read_as_an_answer()
    {
        string failed = """{ "type": "result", "subtype": "error_max_turns", "is_error": true, "result": "" }""";

        Result<ReaderAnswer> read = ReaderAnswers.Parse("rules", failed, null, "rules.json");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "error_max_turns");
    }

    internal static string Envelope(string result) => System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["type"] = "result",
        ["subtype"] = "success",
        ["is_error"] = false,
        ["num_turns"] = 1,
        ["duration_ms"] = 4221,
        ["result"] = result,
        ["usage"] = new Dictionary<string, long>
        {
            ["input_tokens"] = 9,
            ["cache_creation_input_tokens"] = 7035,
            ["cache_read_input_tokens"] = 4388,
            ["output_tokens"] = 352,
        },
        ["modelUsage"] = new Dictionary<string, object>
        {
            ["claude-opus-5-5"] = new Dictionary<string, long> { ["outputTokens"] = 352 },
        },
    });

    internal static async Task<Result<ReaderPrompts.Response>> Prompt(
        CycleWorkspace workspace,
        ArtifactKind kind,
        string directory,
        string? reader = null,
        bool list = false)
    {
        using var gate = Gate.For(workspace.Root);
        return await gate.Send<ReaderPrompts.Command, ReaderPrompts.Response>(
            new ReaderPrompts.Command(workspace.Cycle.Id, kind, directory, reader, list));
    }
}
