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
    private static readonly EvaluationOptions Options = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true,
    };

    private static readonly string[] ExpectedSeverities = ["blocking", "suggestion"];
    private static readonly string[] ExpectedOutcomes = ["pass", "blocked", "escalated"];

    private static readonly Dictionary<string, JsonSchema> Loaded = new(StringComparer.Ordinal);

    [ClassInitialize]
    public static void LoadSchemas(TestContext _)
    {
        // FromFile registers each schema in the global registry under its own $id, which is how
        // verify.schema.json's relative $ref to findings.schema.json resolves. It also means
        // loading the same file twice throws, so every schema is loaded exactly once here.
        foreach (string path in Directory.EnumerateFiles(RepoPaths.Schemas, "*.schema.json"))
        {
            Loaded[Path.GetFileName(path)] = JsonSchema.FromFile(path);
        }
    }

    [TestMethod]
    [DataRow("findings.schema.json", "findings.valid.json", true)]
    [DataRow("findings.schema.json", "findings.invalid-severity.json", false)]
    [DataRow("findings.schema.json", "findings.invalid-blocking-no-remedy.json", false)]
    [DataRow("verify.schema.json", "verify.valid-pass.json", true)]
    [DataRow("verify.schema.json", "verify.valid-escalated.json", true)]
    [DataRow("verify.schema.json", "verify.valid-pass-with-waiver.json", true)]
    [DataRow("verify.schema.json", "verify.invalid-pass-with-open-blocking.json", false)]
    [DataRow("verify.schema.json", "verify.invalid-escalated-budget-left.json", false)]
    [DataRow("verify.schema.json", "verify.invalid-waived-without-document.json", false)]
    public void FixtureMatchesItsSchema(string schemaName, string fixtureName, bool expectedValid)
    {
        JsonSchema schema = Loaded[schemaName];
        using JsonDocument instance = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Fixtures, "schemas", fixtureName)));

        EvaluationResults results = schema.Evaluate(instance.RootElement, Options);

        Assert.AreEqual(
            expectedValid,
            results.IsValid,
            $"{fixtureName} against {schemaName}: {Describe(results)}");
    }

    [TestMethod]
    public void EverySchemaFixtureIsCovered()
    {
        // A fixture nobody evaluates is worse than no fixture: it looks like coverage.
        string[] onDisk = [.. Directory
            .EnumerateFiles(Path.Combine(RepoPaths.Fixtures, "schemas"), "*.json")
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
            "Every fixture under tests/fixtures/schemas must appear in exactly one DataRow. " +
            $"On disk: {string.Join(", ", onDisk)}. Covered: {string.Join(", ", covered)}.");
    }

    [TestMethod]
    public void SeverityEnumIsTheSingleDefinition()
    {
        // The concept makes the findings schema the sole owner of the severity taxonomy. If a
        // second definition appears anywhere, consumers can disagree about what blocks.
        using JsonDocument findings = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Schemas, "findings.schema.json")));

        JsonElement severity = findings.RootElement
            .GetProperty("$defs").GetProperty("severity").GetProperty("enum");

        string[] values = [.. severity.EnumerateArray().Select(v => v.GetString()!)];

        CollectionAssert.AreEqual(ExpectedSeverities, values);
    }

    [TestMethod]
    public void OutcomeEnumMatchesTheStateMachine()
    {
        using JsonDocument verify = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Schemas, "verify.schema.json")));

        JsonElement outcome = verify.RootElement
            .GetProperty("properties").GetProperty("outcome").GetProperty("enum");

        string[] values = [.. outcome.EnumerateArray().Select(v => v.GetString()!)];

        CollectionAssert.AreEqual(ExpectedOutcomes, values);
    }

    private static string Describe(EvaluationResults results)
    {
        if (results.IsValid)
        {
            return "valid";
        }

        IEnumerable<string> messages = (results.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .Take(6);

        return string.Join(" | ", messages);
    }
}
