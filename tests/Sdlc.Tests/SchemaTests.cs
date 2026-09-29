using System.Text.Json;
using Json.Schema;

namespace Sdlc.Tests;

/// <summary>
/// The schemas carry the framework's vocabularies and its verify state machine, so they are
/// tested with real fixtures in both directions. A schema proven only by documents that should
/// pass has not been proven: the rules that matter are the ones that reject.
/// </summary>
[TestClass]
public sealed class SchemaTests
{
    private static readonly string[] _expectedSeverities = ["blocking", "suggestion"];
    private static readonly string[] _expectedOutcomes = ["pass", "blocked", "escalated"];

    [TestMethod]
    [DataRow("findings.schema.json", "findings.valid.json", true)]
    [DataRow("findings.schema.json", "findings.invalid-severity.json", false)]
    [DataRow("findings.schema.json", "findings.invalid-blocking-no-remedy.json", false)]
    [DataRow("findings.schema.json", "findings.valid-suggestion-without-cites.json", true)]
    [DataRow("findings.schema.json", "findings.invalid-blocking-no-cites.json", false)]
    [DataRow("verify.schema.json", "verify.valid-pass.json", true)]
    [DataRow("verify.schema.json", "verify.valid-escalated.json", true)]
    [DataRow("verify.schema.json", "verify.valid-pass-with-waiver.json", true)]
    [DataRow("verify.schema.json", "verify.invalid-pass-with-open-blocking.json", false)]
    [DataRow("verify.schema.json", "verify.invalid-escalated-budget-left.json", false)]
    [DataRow("verify.schema.json", "verify.invalid-waived-without-document.json", false)]
    [DataRow("intent.schema.json", "intent.valid.json", true)]
    [DataRow("intent.schema.json", "intent.invalid-approved-without-timestamp.json", false)]
    [DataRow("intent.schema.json", "intent.invalid-unknown-key.json", false)]
    [DataRow("spec.schema.json", "spec.valid.json", true)]
    [DataRow("spec.schema.json", "spec.invalid-approved-without-timestamp.json", false)]
    [DataRow("plan.schema.json", "plan.valid.json", true)]
    [DataRow("plan.schema.json", "plan.invalid-tier.json", false)]
    [DataRow("solution.schema.json", "solution.valid.json", true)]
    [DataRow("solution.schema.json", "solution.invalid-baseline.json", false)]
    [DataRow("solution.schema.json", "solution.invalid-origin.json", false)]
    [DataRow("solution.schema.json", "solution.invalid-adopted-without-sha.json", false)]
    [DataRow("expectation.schema.json", "expectation.valid.json", true)]
    [DataRow("expectation.schema.json", "expectation.invalid-missing-assertions.json", false)]
    [DataRow("expectation.schema.json", "expectation.invalid-kind.json", false)]
    public void FixtureMatchesItsSchema(string schemaName, string fixtureName, bool expectedValid)
    {
        using var instance = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.SchemaSamples, fixtureName)));

        EvaluationResults results = Schemas.Get(schemaName).Evaluate(instance.RootElement, Schemas.Options);

        Assert.AreEqual(
            expectedValid,
            results.IsValid,
            $"{fixtureName} against {schemaName}: {Schemas.Describe(results)}");
    }

    [TestMethod]
    public void EverySchemaFixtureIsCovered()
    {
        // A fixture nobody evaluates is worse than no fixture: it looks like coverage.
        string[] onDisk = [.. Directory
            .EnumerateFiles(RepoPaths.SchemaSamples, "*.json")
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.Ordinal)];

        string[] covered = [.. typeof(SchemaTests)
            .GetMethod(nameof(FixtureMatchesItsSchema))!
            .GetCustomAttributes(typeof(DataRowAttribute), inherit: false)
            .Cast<DataRowAttribute>()
            .Select(r => (string)r.Data[1]!)
            .OrderBy(n => n, StringComparer.Ordinal)];

        CollectionAssert.AreEqual(
            onDisk,
            covered,
            "Every document under tests/schemas must appear in exactly one DataRow. " +
            $"On disk: {string.Join(", ", onDisk)}. Covered: {string.Join(", ", covered)}.");
    }

    [TestMethod]
    public void SeverityEnumIsTheSingleDefinition()
    {
        // The concept makes the findings schema the sole owner of the severity taxonomy. If a
        // second definition appears anywhere, consumers can disagree about what blocks.
        using var findings = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Schemas, "findings.schema.json")));

        JsonElement severity = findings.RootElement
            .GetProperty("$defs").GetProperty("severity").GetProperty("enum");

        string[] values = [.. severity.EnumerateArray().Select(v => v.GetString()!)];

        CollectionAssert.AreEqual(_expectedSeverities, values);
    }

    [TestMethod]
    public void OutcomeEnumMatchesTheStateMachine()
    {
        using var verify = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Schemas, "verify.schema.json")));

        JsonElement outcome = verify.RootElement
            .GetProperty("properties").GetProperty("outcome").GetProperty("enum");

        string[] values = [.. outcome.EnumerateArray().Select(v => v.GetString()!)];

        CollectionAssert.AreEqual(_expectedOutcomes, values);
    }

    [TestMethod]
    public void OneWordNamesTheHumanGateOnEveryArtifact()
    {
        // An intent, a spec and a plan each wait on the same act: a person puts their name and the time
        // on it. It used to be spelled two ways - accepted_by on an intent and a spec, approved_by on a
        // plan - so every reader, every skill and every regex had to remember which artifact used which,
        // and the hook that refuses findings against a settled artifact carried two code paths to match
        // one decision. The word is "approved", everywhere.
        //
        // "Acceptance" is not free to reuse either: a spec's criteria are acceptance criteria, so a spec
        // that was also "accepted" made the word mean both the criteria and the sign-off.
        List<string> problems = [];

        foreach (string artifact in new[] { "intent", "spec", "plan" })
        {
            using var schema = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(RepoPaths.Schemas, $"{artifact}.schema.json")));

            JsonElement properties = schema.RootElement.GetProperty("properties");

            foreach (string field in new[] { "approved_by", "approved_at" })
            {
                if (!properties.TryGetProperty(field, out _))
                {
                    problems.Add($"{artifact}.schema.json does not define {field}");
                }
            }

            foreach (string field in new[] { "accepted_by", "accepted_at" })
            {
                if (properties.TryGetProperty(field, out _))
                {
                    problems.Add($"{artifact}.schema.json still defines {field}");
                }
            }
        }

        // The scripts and skills have to speak it too: a schema nobody reads the same way is not a
        // vocabulary.
        foreach (string file in Directory.EnumerateFiles(RepoPaths.At("tools"), "*.ps1")
            .Concat(Directory.EnumerateFiles(RepoPaths.Skills, "SKILL.md", SearchOption.AllDirectories)))
        {
            string text = File.ReadAllText(file);

            if (text.Contains("accepted_by", StringComparison.Ordinal)
                || text.Contains("accepted_at", StringComparison.Ordinal))
            {
                problems.Add($"{Path.GetFileName(file)} names the retired accepted_* field");
            }
        }

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }
}
