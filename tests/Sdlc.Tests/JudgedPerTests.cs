using FluentResults;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;

namespace Sdlc.Tests;

/// <summary>
/// A rule judged one item at a time (D-119): the field, the rules it marks, the rows the reader is asked
/// for, and the answer refused without them.
/// </summary>
[TestClass]
public sealed class JudgedPerTests
{
    private const string Rule = """
        ## LOC_01 — Every criterion names its owner

        **Applies to:** a spec.

        **Forbids:** a criterion naming nobody who would notice it failing.

        **Why:** an unowned criterion fails silently.

        **Judged:** {0}
        """;

    [TestMethod]
    [DataRow("per criterion.", JudgedPer.Criterion)]
    [DataRow("Per outcome", JudgedPer.Outcome)]
    public void The_field_is_read_after_why(string value, JudgedPer expected)
    {
        Constraint rule = ConstraintParser.Parse(Rule.Replace("{0}", value, StringComparison.Ordinal), "test").Value.Single();

        Assert.AreEqual(expected, rule.Judged);
        Assert.AreEqual("an unowned criterion fails silently.", rule.Why, "The field is not swallowed into Why.");
    }

    [TestMethod]
    public void A_value_outside_the_two_is_refused_rather_than_judged_whole()
    {
        Result<IReadOnlyList<Constraint>> parsed = ConstraintParser.Parse(Rule.Replace("{0}", "per section", StringComparison.Ordinal), "test");

        Assert.IsTrue(parsed.IsFailed);
        StringAssert.Contains(parsed.Errors[0].Message, "per criterion");
    }

    [TestMethod]
    public void The_library_judges_the_five_rules_that_kept_finding_one_instance_a_round_item_by_item()
    {
        var judged = ConstraintParser.Parse(File.ReadAllText(RepoPaths.Library), RepoPaths.Library).Value
            .Where(rule => rule.IsInForce)
            .ToDictionary(rule => rule.Id.Number, rule => rule.Judged);

        Assert.AreEqual(JudgedPer.Criterion, judged[3]);
        Assert.AreEqual(JudgedPer.Criterion, judged[21]);
        Assert.AreEqual(JudgedPer.Criterion, judged[22]);
        Assert.AreEqual(JudgedPer.Criterion, judged[23], "A criterion's cases are listed or not, one criterion at a time (D-132).");
        Assert.AreEqual(JudgedPer.Outcome, judged[16]);
        Assert.AreEqual(5, judged.Values.Count(value => value is not null), "A sixth is a decision, not an edit.");
    }

    [TestMethod]
    public async Task A_plan_rules_reader_is_asked_for_a_row_per_spec_criterion()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSpec(criteria: 3);
        workspace.WritePlan(proofs: 3);

        string prompt = await File.ReadAllTextAsync((await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Plan, Path.Combine(workspace.Root, "round"), "rules")).Value.Prompts[0].Path);

        StringAssert.Contains(prompt, "judged per acceptance criterion of the spec");
        StringAssert.Contains(prompt, "3. Something observable happens");
        StringAssert.Contains(prompt, "\"rows\": [");
    }

    [TestMethod]
    public async Task A_plan_answer_missing_a_row_or_leaving_an_unmet_row_unblocked_is_refused()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSpec(criteria: 2);
        workspace.WritePlan(proofs: 2);
        string directory = Path.Combine(workspace.Root, "round");
        Assert.IsTrue((await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Plan, directory)).IsSuccess);

        foreach (string reader in new[] { "security", "operability", "maintainability" })
        {
            await File.WriteAllTextAsync(ReaderAnswers.AnswerPath(directory, reader), """{ "findings": [] }""");
        }

        string rules = ReaderAnswers.AnswerPath(directory, "rules");

        await File.WriteAllTextAsync(rules, """{ "findings": [], "rows": [ { "rule": "CON_21", "item": 1, "met": true, "why": "x" } ] }""");
        Result<CollectRound.Response> missing = await Collect(workspace, directory);
        Assert.IsTrue(missing.IsFailed);
        StringAssert.Contains(missing.Errors[0].Message, "CON_21 item 2");

        await File.WriteAllTextAsync(rules, """{ "findings": [], "rows": [ { "rule": "CON_21", "item": 1, "met": true, "why": "x" }, { "rule": "CON_21", "item": 2, "met": false, "why": "a name, not an assertion" } ] }""");
        Result<CollectRound.Response> unblocked = await Collect(workspace, directory);
        Assert.IsTrue(unblocked.IsFailed);
        StringAssert.Contains(unblocked.Errors[0].Message, "raised no blocking finding");

        await File.WriteAllTextAsync(rules, """{ "findings": [], "rows": [ { "rule": "CON_21", "item": 1, "met": true, "why": "x" }, { "rule": "CON_21", "item": 2, "met": true, "why": "x" } ] }""");
        Result<CollectRound.Response> answered = await Collect(workspace, directory);
        Assert.IsTrue(answered.IsSuccess, string.Join("; ", answered.Errors.Select(error => error.Message)));
    }

    private static async Task<Result<CollectRound.Response>> Collect(CycleWorkspace workspace, string directory)
    {
        using var gate = Gate.For(workspace.Root);
        return await gate.Send<CollectRound.Command, CollectRound.Response>(
            new CollectRound.Command(workspace.Cycle.Id, ArtifactKind.Plan, directory));
    }
}
