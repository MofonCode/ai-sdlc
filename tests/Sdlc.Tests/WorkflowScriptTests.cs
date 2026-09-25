using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sdlc.Tests;

/// <summary>
/// Structural checks every shipped workflow script must pass, plus the per-script
/// guarantees that carry real weight.
///
/// The enum checks are the important ones. A workflow script cannot read a file, so
/// every script restates the categories and stages the findings schema owns — a second
/// list, which means INV-03 requires the check binding them together to ship in the same
/// change. These are those checks.
/// </summary>
[TestClass]
public sealed class WorkflowScriptTests
{
    private static string WorkflowDir => Path.Combine(AppContext.BaseDirectory, "workflows");

    /// <summary>The one script that clears both clauses of the workflow rule.</summary>
    private static readonly string[] OnlyWorkflow = ["interrogate.js"];

    private static string SchemaPath =>
        Path.Combine(AppContext.BaseDirectory, "schemas", "findings.schema.json");

    public static IEnumerable<object[]> AllScripts() =>
        Directory.EnumerateFiles(WorkflowDir, "*.js")
            .Select(p => new object[] { Path.GetFileName(p) });

    private static string Read(string name) => File.ReadAllText(Path.Combine(WorkflowDir, name));

    /// <summary>
    /// The script with comments removed, for checks that must look at code rather than
    /// prose. Without this, a comment explaining why <c>Date.now()</c> is unavailable
    /// trips the check forbidding <c>Date.now()</c>. See <see cref="SourceText"/>.
    /// </summary>
    private static string ReadCode(string name) =>
        SourceText.StripJavaScriptComments(Read(name));

    private static string[] SchemaEnum(string defName)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(SchemaPath));
        return [.. doc.RootElement
            .GetProperty("$defs").GetProperty(defName).GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!)];
    }

    private static string[]? ScriptArray(string script, string constName)
    {
        Match block = Regex.Match(
            script,
            @"const\s+" + Regex.Escape(constName) + @"\s*=\s*\[(?<body>[^\]]*)\]",
            RegexOptions.Singleline);

        if (!block.Success)
        {
            return null;
        }

        return [.. Regex.Matches(block.Groups["body"].Value, @"'([^']+)'")
            .Select(m => m.Groups[1].Value)];
    }

    // --- structural, every script -------------------------------------------

    [TestMethod]
    public void WorkflowsShip()
    {
        Assert.IsTrue(AllScripts().Any(), "no workflow scripts found in the output");
    }

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void MetaIsFirstAndCarriesTheRequiredFields(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string script = Read(name);

        Assert.IsTrue(
            script.TrimStart().StartsWith("export const meta", StringComparison.Ordinal),
            $"{name}: meta must be the first statement or the workflow is dropped from autocomplete");
        Assert.IsTrue(script.Contains("name:", StringComparison.Ordinal), $"{name}: meta needs a name");
        Assert.IsTrue(script.Contains("description:", StringComparison.Ordinal), $"{name}: meta needs a description");
    }

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void MetaIsAPureLiteral(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string script = Read(name);

        // A variable, call or spread inside meta drops the workflow from autocomplete
        // silently — the worst failure available, because it simply never appears.
        int start = script.IndexOf("export const meta", StringComparison.Ordinal);
        int end = script.IndexOf("\n}", start, StringComparison.Ordinal);
        string meta = script[start..end];

        Assert.IsFalse(meta.Contains("${", StringComparison.Ordinal), $"{name}: no template interpolation in meta");

        // A spread is `...` followed by an identifier or an opening brace. A literal
        // ellipsis in prose — "Pass { cycle: ..., findings: [...] }" — is not one, and
        // matching on the three dots alone reported a perfectly valid meta as broken.
        Assert.IsFalse(Regex.IsMatch(meta, @"\.\.\.\s*[A-Za-z_${]"), $"{name}: no spread in meta");

        Assert.IsFalse(Regex.IsMatch(meta, @"[A-Za-z_$][\w$]*\("), $"{name}: no function calls in meta");
    }

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void EveryPhaseCallHasAMatchingMetaEntry(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string script = Read(name);

        string[] called = [.. Regex.Matches(script, @"phase\('([^']+)'\)")
            .Select(m => m.Groups[1].Value).Distinct()];
        string[] declared = [.. Regex.Matches(script, @"\{\s*title:\s*'([^']+)'")
            .Select(m => m.Groups[1].Value).Distinct()];

        foreach (string phase in called)
        {
            CollectionAssert.Contains(declared, phase,
                $"{name}: phase('{phase}') has no meta.phases entry — titles are matched exactly");
        }
    }

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void NoConstructsTheRuntimeForbids(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string script = ReadCode(name);

        // These would break resume, so the runtime makes them throw.
        Assert.IsFalse(script.Contains("Date.now()", StringComparison.Ordinal), $"{name}: Date.now() throws");
        Assert.IsFalse(script.Contains("Math.random()", StringComparison.Ordinal), $"{name}: Math.random() throws");
        Assert.IsFalse(Regex.IsMatch(script, @"new Date\(\s*\)"), $"{name}: argless new Date() throws");

        // A script containing import() fails before the run starts.
        Assert.IsFalse(Regex.IsMatch(script, @"\bimport\s*\("), $"{name}: no module loading");

        // Plain JavaScript, not TypeScript.
        Assert.IsFalse(Regex.IsMatch(script, @"\b(interface|enum)\s+[A-Z]"), $"{name}: plain JavaScript only");
    }

    // --- the INV-03 binding checks -----------------------------------------

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void RestatedEnumsMatchTheSchemaThatOwnsThem(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string script = Read(name);

        string[]? categories = ScriptArray(script, "CATEGORIES");
        if (categories is not null)
        {
            CollectionAssert.AreEquivalent(SchemaEnum("category"), categories,
                $"{name}: categories have drifted from schemas/findings.schema.json, which owns them");
        }

        string[]? stages = ScriptArray(script, "STAGES");
        if (stages is not null)
        {
            CollectionAssert.AreEquivalent(SchemaEnum("stage"), stages,
                $"{name}: stages have drifted from the schema that owns them");
        }
    }

    [TestMethod]
    public void InterrogateStatesMatchTheSchema()
    {
        Match inline = Regex.Match(Read("interrogate.js"), @"state:\s*\{\s*enum:\s*\[(?<body>[^\]]*)\]");
        Assert.IsTrue(inline.Success, "could not find the state enum in interrogate.js");

        string[] states = [.. Regex.Matches(inline.Groups["body"].Value, @"'([^']+)'")
            .Select(m => m.Groups[1].Value)];

        CollectionAssert.AreEquivalent(SchemaEnum("state"), states,
            "interrogate.js states have drifted from the schema that owns them");
    }

    // --- per-script guarantees that carry weight ---------------------------

    [TestMethod]
    public void InterrogateForbidsAgentsChangingState()
    {
        // The anti-inflation guard only holds if an agent cannot set state. A merge agent
        // handed a list of findings will otherwise helpfully "correct" them.
        Assert.IsTrue(
            Read("interrogate.js").Contains("Do NOT change any `state`", StringComparison.Ordinal),
            "the merge prompt must forbid changing state, or the guard is advisory");
    }

    [TestMethod]
    public void InterrogateSubstitutesTheFormRoleForTheCharter()
    {
        string script = Read("interrogate.js");
        Assert.IsTrue(script.Contains("FORM_ROLE", StringComparison.Ordinal));
        Assert.IsTrue(script.Contains("isCharter", StringComparison.Ordinal));
    }

    [TestMethod]
    public void InterrogateIsTheOnlyWorkflowThatShips()
    {
        // Every other stage became a skill. A workflow is correct only when several agents
        // must reach the same artifact without seeing each other's conclusions AND the stage
        // can never need to ask the user anything. Interrogation is the only stage where both
        // hold: four orthogonal roles whose independence IS the method, and it reports rather
        // than asks. Anything else appearing here should be justified against those two
        // clauses before it is added.
        string[] scripts = [.. AllScripts().Select(o => (string)o[0])];

        CollectionAssert.AreEquivalent(OnlyWorkflow, scripts,
            "a new workflow must clear both clauses of the rule; a stage that could ever need " +
            "to ask a question cannot be one, because a workflow cannot ask");
    }
}
