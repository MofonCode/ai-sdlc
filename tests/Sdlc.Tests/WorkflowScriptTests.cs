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

    private static string SchemaPath =>
        Path.Combine(AppContext.BaseDirectory, "schemas", "findings.schema.json");

    public static IEnumerable<object[]> AllScripts() =>
        Directory.EnumerateFiles(WorkflowDir, "*.js")
            .Select(p => new object[] { Path.GetFileName(p) });

    private static string Read(string name) => File.ReadAllText(Path.Combine(WorkflowDir, name));

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
        Assert.IsFalse(meta.Contains("...", StringComparison.Ordinal), $"{name}: no spread in meta");
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
        string script = Read(name);

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
    public void RepairTerminatesOnNoProgressRatherThanOnClean()
    {
        string script = Read("repair.js");

        Assert.IsTrue(script.Contains("stalls", StringComparison.Ordinal),
            "repair must track consecutive rounds without progress");
        Assert.IsTrue(script.Contains("STALL_LIMIT", StringComparison.Ordinal),
            "repair must have a stall limit — 'until clean' never fires, because the finding rate does not decay");
        Assert.IsTrue(script.Contains("halted-no-progress", StringComparison.Ordinal),
            "an unconverged repair must report a named outcome rather than passing quietly");
    }

    [TestMethod]
    public void RepairLooksForWhatTheRepairIntroduced()
    {
        // This is the whole point. A recheck that only confirms the fix is blind to the
        // measured failure: roughly three in four repairs create a new blocking defect.
        string script = Read("repair.js");

        Assert.IsTrue(script.Contains("introduced", StringComparison.Ordinal),
            "the recheck must ask what the repair introduced, not only whether it worked");
        Assert.IsTrue(script.Contains("INTRODUCE", StringComparison.Ordinal),
            "the recheck prompt must make the second job explicit");
    }

    [TestMethod]
    public void RepairEscalatesFindingsItCannotHeal()
    {
        // A blocking finding with no repair cannot self-heal — a deferred-decision
        // blocker never can, since no artifact edit resolves "nobody has decided yet".
        // Carrying it round after round would spin the loop.
        string script = Read("repair.js");

        Assert.IsTrue(script.Contains("harvestEscalations", StringComparison.Ordinal));
        Assert.IsTrue(script.Contains("escalated", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SpecRequiresEveryCriterionToBeMarkedVerifiableOrNot()
    {
        // Without the distinction you either annotate the unannotatable, or quietly pass
        // untested criteria as tested.
        string script = Read("spec.js");

        Assert.IsTrue(script.Contains("verifiable", StringComparison.Ordinal));
        Assert.IsTrue(script.Contains("AC-", StringComparison.Ordinal),
            "criterion ids must be prefixed so they can be matched to test annotations");
        Assert.IsTrue(script.Contains("never renumbered", StringComparison.Ordinal),
            "the never-renumber rule must be stated: those ids end up in test annotations");
    }

    [TestMethod]
    public void PlanComputesTheTierMechanicallyRatherThanJudgingIt()
    {
        string script = Read("plan.js");

        Assert.IsTrue(script.Contains("Get-SdlcFacts.ps1", StringComparison.Ordinal),
            "the tier comes from the gatherer, not from an agent's opinion");
        Assert.IsTrue(script.Contains("Do not compute or adjust the tier yourself", StringComparison.Ordinal),
            "the prompt must forbid the agent computing it — the point of a function is that it cannot be argued with");
        Assert.IsTrue(script.Contains("predicted-tier", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildRefusesToRunUnattendedAboveTierOne()
    {
        string script = Read("build.js");

        Assert.IsTrue(script.Contains("UNATTENDED", StringComparison.Ordinal));
        Assert.IsTrue(script.Contains("tier 1 only", StringComparison.Ordinal),
            "unattended builds are permitted at tier 1 only");
    }

    [TestMethod]
    public void BuildHaltsOnTierEscalationRatherThanReportingIt()
    {
        // The work turned out more dangerous than it was approved to be, and the
        // approval does not stretch to cover it.
        string script = Read("build.js");

        Assert.IsTrue(script.Contains("halted-tier-escalated", StringComparison.Ordinal),
            "an escalation must produce a named halt, not a warning in a log");
        Assert.IsTrue(script.Contains("tierEscalated", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EveryStageNamesItsSuccessor()
    {
        // The chain is self-documenting only if each stage says what comes next; a stage
        // that returns without one leaves the caller guessing.
        foreach (string name in new[] { "spec.js", "plan.js", "build.js" })
        {
            Assert.IsTrue(Read(name).Contains("next:", StringComparison.Ordinal),
                $"{name} must name the next stage in its result");
        }
    }

    [TestMethod]
    public void RepairDropsARepairThatDidNotWork()
    {
        // Without this the loop can retry the same failed change forever, and the
        // no-progress detector would be the only thing stopping it.
        Assert.IsTrue(
            Read("repair.js").Contains("repair: undefined", StringComparison.Ordinal),
            "a repair shown not to resolve its finding must not be retried verbatim");
    }
}
