using System.Text.Json;
using Json.Schema;

namespace Sdlc.Tests;

/// <summary>
/// Proves the shipped schemas accept what they should and — the half that actually
/// matters — reject what they should. A schema nothing validates against is a
/// convention, and a convention nothing enforces is not a guard.
/// </summary>
[TestClass]
public sealed class SchemaTests
{
    private static readonly EvaluationOptions Options = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true,
    };

    private static string SchemaDir => Path.Combine(AppContext.BaseDirectory, "schemas");

    private static string ExampleDir => Path.Combine(AppContext.BaseDirectory, "examples");

    // Each schema declares an $id, and JsonSchema.Net registers that id globally, so
    // loading the same file twice throws. Load once, reuse.
    private static readonly Dictionary<string, JsonSchema> Cache = [];

    private static JsonSchema Load(string name)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(name, out JsonSchema? cached))
            {
                cached = JsonSchema.FromFile(Path.Combine(SchemaDir, name));
                Cache[name] = cached;
            }

            return cached;
        }
    }

    private static EvaluationResults Evaluate(string schemaName, string exampleName)
    {
        JsonSchema schema = Load(schemaName);
        using JsonDocument instance = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(ExampleDir, exampleName)));
        return schema.Evaluate(instance.RootElement, Options);
    }

    private static string SchemaFor(string exampleName) =>
        exampleName.StartsWith("findings", StringComparison.Ordinal)
            ? "findings.schema.json"
            : "intent.schema.json";

    public static IEnumerable<object[]> ValidExamples() =>
        Directory.EnumerateFiles(ExampleDir, "*.valid*.json")
            .Select(p => new object[] { Path.GetFileName(p) });

    public static IEnumerable<object[]> InvalidExamples() =>
        Directory.EnumerateFiles(ExampleDir, "*.invalid-*.json")
            .Select(p => new object[] { Path.GetFileName(p) });

    [TestMethod]
    public void BothSchemasLoad()
    {
        Assert.IsNotNull(Load("findings.schema.json"));
        Assert.IsNotNull(Load("intent.schema.json"));
    }

    [TestMethod]
    public void ExamplesExistForBothDirections()
    {
        // Guards the failure mode where the suite passes because it validates nothing.
        Assert.IsTrue(ValidExamples().Any(), "no valid examples found");
        Assert.IsTrue(InvalidExamples().Count() >= 5, "fewer invalid examples than enforced rules");
    }

    [TestMethod]
    [DynamicData(nameof(ValidExamples))]
    public void ValidExampleIsAccepted(string example)
    {
        ArgumentNullException.ThrowIfNull(example);
        EvaluationResults results = Evaluate(SchemaFor(example), example);
        Assert.IsTrue(results.IsValid, $"{example} should validate but did not");
    }

    [TestMethod]
    [DynamicData(nameof(InvalidExamples))]
    public void InvalidExampleIsRejected(string example)
    {
        ArgumentNullException.ThrowIfNull(example);
        EvaluationResults results = Evaluate(SchemaFor(example), example);
        Assert.IsFalse(results.IsValid, $"{example} should be rejected but validated");
    }

    // The five rules the findings schema exists to enforce, named individually so a
    // regression says which guarantee broke rather than which file changed.

    [TestMethod]
    public void RuleOneBlockingRequiresACitation()
    {
        Assert.IsFalse(
            Evaluate("findings.schema.json", "findings.invalid-01-uncited-blocking.json").IsValid,
            "an uncited blocking finding must be unemittable — this is the anti-inflation guard");
    }

    [TestMethod]
    public void RuleTwoUnclassifiedCanNeverBlock()
    {
        Assert.IsFalse(
            Evaluate("findings.schema.json", "findings.invalid-02-unclassified-blocking.json").IsValid,
            "unclassified must never halt a cycle, even when cited");
    }

    [TestMethod]
    public void RuleThreeMechanicalFindingsSkipTriage()
    {
        Assert.IsFalse(
            Evaluate("findings.schema.json", "findings.invalid-03-open-from-check.json").IsValid,
            "a check-detected finding is born blocking and never sits at open");
    }

    [TestMethod]
    public void RuleFourLocationPairsOnlyCiteContradictions()
    {
        Assert.IsFalse(
            Evaluate("findings.schema.json", "findings.invalid-04-locations-wrong-category.json").IsValid,
            "a location pair is self-evident only for internal-contradiction");
    }

    [TestMethod]
    public void RuleFiveDismissalsMustSayWhy()
    {
        Assert.IsFalse(
            Evaluate("findings.schema.json", "findings.invalid-05-unexplained-dismissal.json").IsValid,
            "rejected and accepted-wont-fix feed opposite amendment signals, so both need a reason");
    }

    [TestMethod]
    public void AContradictionNeedsBothHalves()
    {
        Assert.IsFalse(
            Evaluate("findings.schema.json", "findings.invalid-06-single-location.json").IsValid,
            "one location proves no contradiction");
    }

    [TestMethod]
    public void CycleIdShapeIsEnforced()
    {
        Assert.IsFalse(
            Evaluate("findings.schema.json", "findings.invalid-07-bad-cycle-id.json").IsValid,
            "a cycle id is yyyymmdd-hhhh");
        Assert.IsFalse(
            Evaluate("intent.schema.json", "intent.invalid-01-bad-id.json").IsValid,
            "an intent id is yyyymmdd-hhhh");
    }

    [TestMethod]
    public void CausedAtStageRequiresACause()
    {
        Assert.IsFalse(
            Evaluate("intent.schema.json", "intent.invalid-02-stage-without-cause.json").IsValid,
            "naming a stage without naming the cycle names the stage of nothing");
    }

    [TestMethod]
    public void UnknownCauseIsPermitted()
    {
        Assert.IsTrue(
            Evaluate("intent.schema.json", "intent.valid-caused-by-unknown.json").IsValid,
            "attribution must never be forced: a guess pollutes the only unbiased signal the retro loop has");
    }
}
