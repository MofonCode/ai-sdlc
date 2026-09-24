using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sdlc.Tests;

/// <summary>
/// Checks the interrogation workflow script for the defects the runtime only reports at
/// launch, and — the one that matters — holds its hardcoded enums against the schema
/// that owns them.
///
/// The script restates the eleven categories, three states and six stages because a
/// workflow script cannot read a file. That is a second list, so INV-03 requires the
/// check binding the two together to ship in the same change. This is that check.
/// </summary>
[TestClass]
public sealed class WorkflowScriptTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "workflows", "interrogate.js");

    private static string SchemaPath =>
        Path.Combine(AppContext.BaseDirectory, "schemas", "findings.schema.json");

    private static string Script => File.ReadAllText(ScriptPath);

    private static string[] SchemaEnum(string defName)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(SchemaPath));
        return [.. doc.RootElement
            .GetProperty("$defs").GetProperty(defName).GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!)];
    }

    private static string[] ScriptArray(string constName)
    {
        Match block = Regex.Match(
            Script,
            @"const\s+" + Regex.Escape(constName) + @"\s*=\s*\[(?<body>[^\]]*)\]",
            RegexOptions.Singleline);

        Assert.IsTrue(block.Success, $"could not find `const {constName} = [...]` in the script");

        return [.. Regex.Matches(block.Groups["body"].Value, @"'([^']+)'")
            .Select(m => m.Groups[1].Value)];
    }

    [TestMethod]
    public void TheScriptShips()
    {
        Assert.IsTrue(File.Exists(ScriptPath), $"not found: {ScriptPath}");
    }

    [TestMethod]
    public void MetaIsFirstAndCarriesTheRequiredFields()
    {
        string script = Script;

        Assert.IsTrue(
            script.TrimStart().StartsWith("export const meta", StringComparison.Ordinal),
            "meta must be the first statement or the workflow is dropped from autocomplete");

        Assert.IsTrue(script.Contains("name:", StringComparison.Ordinal), "meta needs a name");
        Assert.IsTrue(script.Contains("description:", StringComparison.Ordinal), "meta needs a description");
    }

    [TestMethod]
    public void MetaIsAPureLiteral()
    {
        // A variable, call or spread inside meta drops the workflow from autocomplete
        // silently, which is the worst failure mode available: it just never appears.
        int start = Script.IndexOf("export const meta", StringComparison.Ordinal);
        int end = Script.IndexOf("\n}", start, StringComparison.Ordinal);
        string meta = Script[start..end];

        Assert.IsFalse(meta.Contains("${", StringComparison.Ordinal), "no template interpolation in meta");
        Assert.IsFalse(meta.Contains("...", StringComparison.Ordinal), "no spread in meta");
        Assert.IsFalse(Regex.IsMatch(meta, @"[A-Za-z_$][\w$]*\("), "no function calls in meta");
    }

    [TestMethod]
    public void EveryPhaseCallHasAMatchingMetaEntry()
    {
        string script = Script;

        string[] called = [.. Regex.Matches(script, @"phase\('([^']+)'\)")
            .Select(m => m.Groups[1].Value).Distinct()];

        string[] declared = [.. Regex.Matches(script, @"\{\s*title:\s*'([^']+)'")
            .Select(m => m.Groups[1].Value).Distinct()];

        Assert.IsTrue(called.Length > 0, "no phase() calls found");

        foreach (string phase in called)
        {
            CollectionAssert.Contains(declared, phase,
                $"phase('{phase}') has no meta.phases entry — titles are matched exactly");
        }
    }

    [TestMethod]
    public void NoConstructsTheRuntimeForbids()
    {
        string script = Script;

        // These break resume, so the runtime makes them throw.
        Assert.IsFalse(script.Contains("Date.now()", StringComparison.Ordinal),
            "Date.now() throws inside a workflow: it would break resume");
        Assert.IsFalse(script.Contains("Math.random()", StringComparison.Ordinal),
            "Math.random() throws inside a workflow: it would break resume");
        Assert.IsFalse(Regex.IsMatch(script, @"new Date\(\s*\)"),
            "argless new Date() throws inside a workflow");

        // A script containing import() fails before the run starts.
        Assert.IsFalse(Regex.IsMatch(script, @"\bimport\s*\("),
            "no module loading inside a workflow script");

        // Plain JavaScript, not TypeScript.
        Assert.IsFalse(Regex.IsMatch(script, @"\b(interface|enum)\s+[A-Z]"),
            "workflow scripts are plain JavaScript");
    }

    // --- the INV-03 binding checks -----------------------------------------

    [TestMethod]
    public void ScriptCategoriesMatchTheSchema()
    {
        CollectionAssert.AreEquivalent(
            SchemaEnum("category"),
            ScriptArray("CATEGORIES"),
            "the script's categories have drifted from schemas/findings.schema.json, which owns them");
    }

    [TestMethod]
    public void ScriptStagesMatchTheSchema()
    {
        CollectionAssert.AreEquivalent(
            SchemaEnum("stage"),
            ScriptArray("STAGES"),
            "the script's stages have drifted from the schema that owns them");
    }

    [TestMethod]
    public void ScriptStatesMatchTheSchema()
    {
        // The states are inline in the merge schema rather than a named const, so they
        // are read from there.
        Match inline = Regex.Match(Script, @"state:\s*\{\s*enum:\s*\[(?<body>[^\]]*)\]");
        Assert.IsTrue(inline.Success, "could not find the state enum in the script");

        string[] scriptStates = [.. Regex.Matches(inline.Groups["body"].Value, @"'([^']+)'")
            .Select(m => m.Groups[1].Value)];

        CollectionAssert.AreEquivalent(SchemaEnum("state"), scriptStates,
            "the script's states have drifted from the schema that owns them");
    }

    [TestMethod]
    public void StateIsAssignedInCodeAndNotByAnAgent()
    {
        // The anti-inflation guard only holds if an agent cannot set state. The merge
        // prompt must say so explicitly, because a merge agent handed a list of findings
        // will otherwise helpfully "correct" them.
        Assert.IsTrue(
            Script.Contains("Do NOT change any `state`", StringComparison.Ordinal),
            "the merge prompt must forbid changing state, or the guard is advisory");
    }

    [TestMethod]
    public void TheCharterSubstitutesTheFormRole()
    {
        // Interrogating the charter has no authority above it, so the charter role is
        // replaced by form, which judges shape rather than content. Without this the
        // charter is the one artifact nothing can check.
        Assert.IsTrue(Script.Contains("FORM_ROLE", StringComparison.Ordinal));
        Assert.IsTrue(Script.Contains("isCharter", StringComparison.Ordinal));
    }
}
