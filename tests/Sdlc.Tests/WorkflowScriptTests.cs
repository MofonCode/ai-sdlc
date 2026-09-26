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
        // The substitution survives charter v3; only its source moved. `charter` has nothing
        // above it to check against when the artifact IS the charter, and `form` judges each
        // invariant's SHAPE instead — which is what makes the charter interrogable without an
        // infinite regress. Both roles are now declared by the charter, so this asserts the
        // SELECTION rather than a hardcoded role object.
        string code = ReadCode("interrogate.js");

        Assert.IsTrue(code.Contains("isCharter", StringComparison.Ordinal));
        Assert.IsTrue(Regex.IsMatch(code, @"'form',\s*'coherence'"),
            "interrogating the charter must select form in place of charter");
        Assert.IsTrue(Regex.IsMatch(code, @"'charter',\s*'coherence'"),
            "and anything else must select charter");
    }

    [TestMethod]
    public void TheCharterDeclaresEveryRoleTheSchemaAllows()
    {
        // Role names now live in the charter (which OWNS them), in the findings schema (which
        // validates them), and in interrogate.js's merge schema (which restates the schema's
        // enum). Three places, so INV-03 wants the binding — and this is the half that was
        // missing: the charter and the schema had nothing tying them together.
        string charter = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                ".sdlc", "CHARTER.md"));

        foreach (string role in SchemaEnum("role"))
        {
            Assert.IsTrue(charter.Contains("| `" + role + "` |", StringComparison.Ordinal),
                $"the schema allows role '{role}' but the charter's role tables do not declare it");
        }
    }

    [TestMethod]
    public void InterrogateKeepsNoCopyOfTheRoles()
    {
        // The whole point of charter v3. A fallback copy would be the second list again, and
        // interrogating with roles that might not match the charter is worse than declining to
        // interrogate — so there is no fallback and the workflow throws instead.
        string code = ReadCode("interrogate.js");

        Assert.IsTrue(code.Contains("rolesFromFacts", StringComparison.Ordinal),
            "roles must be derived from the attached facts");
        Assert.IsTrue(code.Contains("charter declares no interrogator roles", StringComparison.Ordinal),
            "missing roles must throw rather than fall back to a copy");

        // The old hardcoded shapes, by name. Their absence is the assertion.
        Assert.IsFalse(Regex.IsMatch(code, @"const\s+ROLES\s*="),
            "a hardcoded ROLES array is the second list this change removed");
        Assert.IsFalse(Regex.IsMatch(code, @"const\s+FORM_ROLE\s*="),
            "the form substitution is the charter's to declare, not this file's");
    }

    [TestMethod]
    public void TheROUNDAndTheCYCLEIDComeFromTheFactsAndNotFromASecondDerivation()
    {
        // NOBODY INCREMENTED THE ROUND. It was an input defaulting to 1, so every interrogation was
        // round 1, every filed document overwrote the last, and the no-progress rule — the only
        // thing that makes the repair loop terminate — had no history to measure. The gatherer
        // counts what is filed and names the next number.
        //
        // The cycle id had the opposite problem: it was derived HERE in JavaScript and also in the
        // gatherer in PowerShell, which is two implementations of one path convention. They agree
        // until a convention moves.
        string code = ReadCode("interrogate.js");

        Assert.IsTrue(code.Contains("PROGRESS['next-round']", StringComparison.Ordinal),
            "the round must come from what is already filed, not from a default");
        Assert.IsTrue(code.Contains("parsedFacts.artifact['cycle-id']", StringComparison.Ordinal),
            "the cycle id must come from the facts, which is where the path convention lives");

        // The removed second derivation, by name. Its absence is the assertion.
        Assert.IsFalse(Regex.IsMatch(code, @"function\s+cycleIdFromPath"),
            "a local path parser is the second implementation this change removed");

        // And the stalled loop has to be said out loud, because the alternative is discovering it
        // after paying for four role agents and one triage agent per candidate.
        Assert.IsTrue(code.Contains("halted-no-progress", StringComparison.Ordinal),
            "a stalled repair loop must be reported at the top of the run");
    }

    [TestMethod]
    public void APackRuleIsNamedRatherThanCited()
    {
        // `cites` takes four kinds and a pack rule is none of them: the findings schema pins the
        // invariant kind to `^(INV|LOC)-[0-9]{2}$`. Accepting `PR-01` there reached `blocking` and
        // was then rejected by the schema — and a document is filed whole or not at all, so one such
        // citation discarded every other finding in the run.
        //
        // One invariant makes every pack rule blockable: the finding cites the invariant that
        // DELEGATES and names the rule. For that the role needs the rule's wording, which is why the
        // pack's rules travel in the facts.
        string code = ReadCode("interrogate.js");

        Assert.IsFalse(code.Contains("declaredPackRules.indexOf(id) !== -1", StringComparison.Ordinal),
            "a pack rule id must not resolve as an invariant citation; the schema rejects it");
        Assert.IsTrue(code.Contains("packRuleText", StringComparison.Ordinal),
            "the pack's rules must reach the role that checks against the charter");
        Assert.IsTrue(code.Contains("NAMING the rule", StringComparison.Ordinal),
            "the role has to be told the shape: cite the delegating invariant, name the rule");
    }

    [TestMethod]
    public void TheGapsRoleIsGivenTheCharterRowForTheArtifactKind()
    {
        // `gaps` is the one role whose evidence varies by artifact, and leaving that unstated
        // is what produces its declared failure: inventing requirements nobody asked for.
        string code = ReadCode("interrogate.js");

        Assert.IsTrue(code.Contains("gaps-by-artifact", StringComparison.Ordinal));
        Assert.IsTrue(code.Contains("gapsGuidance", StringComparison.Ordinal));
        Assert.IsTrue(code.Contains("not exhaustive", StringComparison.Ordinal),
            "the row must be offered as entitlement rather than as a closed list, or the role " +
            "stops reasoning past it");
    }

    [TestMethod]
    public void EveryCheckTheGathererEmitsIsClassified()
    {
        // THE BINDING CHECK, and it exists because the omission it prevents was real. An
        // unmapped check id falls through to `unclassified`, which can never block by
        // construction — so a failed check would be reported as advisory while the charter's
        // Coverage table claims that category blocks through exactly this route.
        //
        // Five of fourteen were mapped for two days. The nine missing included
        // `suppressions-justified` and `plan-adherence`, which is to say gate bypasses and
        // scope drift were silently non-blocking. An interrogation of the charter found it;
        // nothing in this suite would have.
        Dictionary<string, string> mapped = InterrogateCheckCategories();
        string[] emitted = GathererCheckIds();

        List<string> unmapped = [.. emitted.Where(id => !mapped.ContainsKey(id))];
        Assert.AreEqual(0, unmapped.Count,
            "these checks fall through to `unclassified` and can never block: " +
            string.Join(", ", unmapped));

        // The reverse direction matters too, though less: a mapped id the gatherer never
        // emits is dead weight that reads as coverage.
        List<string> phantom = [.. mapped.Keys.Where(id => !emitted.Contains(id))];
        Assert.AreEqual(0, phantom.Count,
            "these are classified but never emitted, so the map overstates its coverage: " +
            string.Join(", ", phantom));
    }

    [TestMethod]
    public void NoCheckIsClassifiedAsUnclassified()
    {
        // Mapping a check to `unclassified` would satisfy the binding check above while
        // reintroducing exactly the defect it exists to stop.
        foreach ((string id, string category) in InterrogateCheckCategories())
        {
            Assert.AreNotEqual("unclassified", category,
                $"check '{id}' is classified as unclassified, which can never block");
        }
    }

    private static Dictionary<string, string> InterrogateCheckCategories()
    {
        // `[^}]*` rather than `.*?` up to a newline-brace: the object body contains no
        // closing brace once comments are stripped, and a pattern with no newline in it
        // cannot lose one. The previous form had a LITERAL newline in a verbatim string,
        // which git normalised to CRLF while the .js file kept LF - so it matched nothing.
        Match block = Regex.Match(
            ReadCode("interrogate.js"),
            @"const\s+CHECK_CATEGORY\s*=\s*\{(?<body>[^}]*)\}",
            RegexOptions.Singleline);

        Assert.IsTrue(block.Success, "could not find CHECK_CATEGORY in interrogate.js");

        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(block.Groups["body"].Value, @"'(?<id>[a-z-]+)'\s*:\s*'(?<cat>[a-z-]+)'"))
        {
            map[m.Groups["id"].Value] = m.Groups["cat"].Value;
        }

        return map;
    }

    private static string[] GathererCheckIds()
    {
        // Read from the gatherer's source, which is the thing that decides what exists.
        // Stripped, so a commented-out check does not count as emitted.
        string code = SourceText.StripPowerShellComments(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "tools", "Get-SdlcFacts.ps1")));

        return [.. Regex.Matches(code, @"Add-Check\s+'(?<id>[a-z-]+)'")
            .Select(m => m.Groups["id"].Value).Distinct(StringComparer.Ordinal)];
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
