using System.Diagnostics;
using System.Text;

namespace Sdlc.Tests;

/// <summary>
/// Parses every shipped workflow with a real JavaScript engine.
/// </summary>
/// <remarks>
/// This was a recorded gap for days — "nothing proves the workflow scripts are syntactically
/// valid JavaScript" — justified by Node having been dropped as a dependency. The gap then did
/// exactly what a gap does: <c>interrogate.js</c> shipped <b>syntactically invalid for four
/// commits</b>, with two separate errors, and the only workflow in the framework would have
/// thrown at launch.
/// <para>
/// Both errors came from the same cause. The file was edited through layers that ate one level
/// of escaping, so <c>/[backslash][backslash]/g</c> became <c>/[backslash]/g</c> — an
/// unterminated regex — and a <c>[backslash]n</c> inside a single-quoted string became a real
/// newline. Neither is visible on a skim, and every structural test passed throughout because
/// they read the file as text.
/// </para>
/// <para>
/// <b>How it parses matters.</b> Checking as a bare module reports the top-level
/// <c>return</c> as illegal, which is wrong: the runtime wraps the script body in an async
/// function, where a top-level return and top-level await are both legal. So the check mirrors
/// the runtime — <c>meta</c> stays a declaration and the body is wrapped — otherwise it would
/// fail every correct workflow and be switched off within a day.
/// </para>
/// </remarks>
[TestClass]
public sealed class WorkflowSyntaxTests
{
    private static string WorkflowDir => Path.Combine(AppContext.BaseDirectory, "workflows");

    public static IEnumerable<object[]> AllScripts() =>
        Directory.EnumerateFiles(WorkflowDir, "*.js")
            .Select(p => new object[] { Path.GetFileName(p) });

    private static bool NodeIsAvailable()
    {
        try
        {
            using Process p = Process.Start(new ProcessStartInfo("node", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            })!;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void ParsesAsTheRuntimeLoadsIt(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // Node ships on the GitHub Windows runner and on this machine. If it is ever absent the
        // honest thing is to say the check did not run, rather than to pass silently — a
        // skipped check that reports success is how this gap survived in the first place.
        Assert.IsTrue(NodeIsAvailable(),
            "node is not on PATH, so workflow syntax was NOT checked. Install node or remove " +
            "this test deliberately; do not let it pass by default.");

        string source = File.ReadAllText(Path.Combine(WorkflowDir, name));

        // Mirror the runtime: `meta` is a module export, and the body executes inside an async
        // function. Wrapping is what makes a top-level return and top-level await legal.
        string wrapped =
            "async function __syntaxCheck() {" + Environment.NewLine
            + ReplaceFirst(source, "export const meta", "const meta") + Environment.NewLine
            + "}" + Environment.NewLine;

        string temp = Path.Combine(Path.GetTempPath(), "wf-" + Guid.NewGuid().ToString("N")[..8] + ".mjs");
        File.WriteAllText(temp, wrapped);

        try
        {
            using Process p = Process.Start(new ProcessStartInfo("node")
            {
                ArgumentList = { "--check", temp },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            })!;

            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            Assert.AreEqual(0, p.ExitCode,
                $"{name} is not valid JavaScript, so it would throw at launch:{Environment.NewLine}{stderr}");
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void ContainsNoUnterminatedRegexFromLostEscaping(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // The specific shape that shipped, checked directly as well as by the parser. A regex
        // whose body is a single backslash is always an escaping accident: it can never be
        // what anyone meant, and the parser error it produces points at the wrong column.
        string source = File.ReadAllText(Path.Combine(WorkflowDir, name));
        string singleBackslashRegex = "/" + (char)92 + "/g";

        Assert.IsFalse(source.Contains(singleBackslashRegex, StringComparison.Ordinal),
            $"{name} contains an unterminated regex — one level of escaping was lost. " +
            "Use String.fromCharCode(92) for a literal backslash; it cannot be eaten.");
    }

    [TestMethod]
    public void AnUnresolvableCitationCannotGrantBlocking()
    {
        // THE ANTI-INFLATION GUARD, EXECUTED rather than grepped for. Until node turned out to
        // be available, every workflow assertion in this suite read the file as text — so
        // deleting the validation from the condition that uses it passed all fifteen structural
        // tests. A guard nothing exercises is a guard nobody can rely on.
        //
        // A check citation is stricter than the others by design: an invariant or DEC only has
        // to EXIST, a check has to have actually FAILED in this run. `dec-ids-resolve` below is
        // the case that matters — a real check id that did not fail, which is the most
        // plausible-looking false citation available.
        Assert.IsTrue(NodeIsAvailable(), "node is not on PATH, so the guard was NOT exercised");

        string source = File.ReadAllText(Path.Combine(WorkflowDir, "interrogate.js"));
        string fn = ExtractBlock(source, "function citationResolves");

        string harness = string.Join(Environment.NewLine,
            "const declaredInvariants = ['INV-01', 'LOC-01']",
            "const declaredDecisions = ['DEC-06']",
            "const declaredPackRules = ['PR-01']",
            "const failedChecks = ['paths-resolve']",
            fn,
            "const cases = {",
            "  invariantDeclared:      citationResolves('invariant', 'INV-01'),",
            "  invariantUndeclared:    citationResolves('invariant', 'INV-99'),",
            "  localInvariant:         citationResolves('invariant', 'LOC-01'),",
            "  packRuleAsAnInvariant:  citationResolves('invariant', 'PR-01'),",
            "  decisionDeclared:       citationResolves('deferred', 'DEC-06'),",
            "  decisionUndeclared:     citationResolves('deferred', 'DEC-99'),",
            "  checkThatFailed:        citationResolves('check', 'paths-resolve'),",
            "  checkThatDidNotFail:    citationResolves('check', 'dec-ids-resolve'),",
            "  checkThatDoesNotExist:  citationResolves('check', 'invented-check'),",
            "  unknownKind:            citationResolves('vibes', 'INV-01'),",
            "}",
            "console.log(JSON.stringify(cases))");

        string result = RunNode(harness);

        // Expected, spelled out so a wrong answer names itself.
        var expected = new (string Key, bool Value)[]
        {
            ("invariantDeclared", true),
            ("invariantUndeclared", false),
            ("localInvariant", true),
            // A PACK RULE IS NOT AN INVARIANT, and accepting one here produced a document nothing
            // could file. `cites` takes four kinds and the findings schema pins the invariant one to
            // `^(INV|LOC)-[0-9]{2}$`, so `{kind:'invariant', id:'PR-01'}` reached `blocking` and was
            // then rejected by the schema — taking every other finding in the run with it, because a
            // document is filed whole or not at all.
            //
            // The charter says what the right shape is, and it is why `cites` stays at three kinds:
            // one invariant makes every pack rule blockable, so a finding cites the invariant that
            // DELEGATES and names the rule it broke in its summary.
            ("packRuleAsAnInvariant", false),
            ("decisionDeclared", true),
            ("decisionUndeclared", false),
            ("checkThatFailed", true),
            ("checkThatDidNotFail", false),
            ("checkThatDoesNotExist", false),
            ("unknownKind", false),
        };

        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(result);
        foreach ((string key, bool value) in expected)
        {
            Assert.AreEqual(value, doc.RootElement.GetProperty(key).GetBoolean(),
                $"citationResolves is wrong for '{key}'. Full result: {result}");
        }
    }

    [TestMethod]
    public void StateIsAssignedFromCitationsThatResolve()
    {
        // THE ANTI-INFLATION GUARD, END TO END. The test above proves `citationResolves` returns
        // the right answers; it does NOT prove anything consults it. Deleting the call from the
        // condition — leaving the function sitting there, correct and unused — passed both the
        // fifteen structural tests AND the new unit test. So this executes the real assignment
        // loop and reads the states it produces.
        //
        // That is the whole claim of the guard: an agent can argue for importance but cannot
        // grant it. Every row below is an agent insisting on `blocking`.
        Assert.IsTrue(NodeIsAvailable(), "node is not on PATH, so the guard was NOT exercised");

        string source = File.ReadAllText(Path.Combine(WorkflowDir, "interrogate.js"));

        string harness = string.Join(Environment.NewLine,
            "const ARTIFACT = 'a.md'",
            // The run prefix that makes a finding id unique within a CYCLE rather than within
            // one interrogation. A cycle interrogates its intent, spec, plan and verification,
            // and every one of those runs used to emit `r1`.
            "const RUN = 'spec-r1'",
            "const declaredInvariants = ['INV-01', 'LOC-01']",
            "const declaredDecisions = ['DEC-06']",
            "const declaredPackRules = ['PR-01']",
            "const failedChecks = ['paths-resolve']",
            ExtractBlock(source, "function citationResolves"),
            "const rejectedCitations = []",
            "const assembled = []",
            "let n = 0",
            // Each row is a triage verdict that says `blocking`, as loudly as the schema allows.
            "const triaged = [",
            "  { candidate: { id: 'a', category: 'internal-contradiction', anchor: 'x', secondAnchor: 'y' },",
            "    verdict: { cited: true, citeKind: 'invariant', citeId: 'INV-01' } },",
            "  { candidate: { id: 'b', category: 'internal-contradiction', anchor: 'x', secondAnchor: 'y' },",
            "    verdict: { cited: true, citeKind: 'invariant', citeId: 'INV-99' } },",
            "  { candidate: { id: 'c', category: 'internal-contradiction' },",
            "    verdict: { cited: true, citeKind: 'check', citeId: 'dec-ids-resolve' } },",
            "  { candidate: { id: 'd', category: 'internal-contradiction' },",
            "    verdict: { cited: true, citeKind: 'check', citeId: 'paths-resolve' } },",
            "  { candidate: { id: 'e', category: 'unclassified' },",
            "    verdict: { cited: true, citeKind: 'invariant', citeId: 'INV-01' } },",
            "  { candidate: { id: 'f', category: 'vague-language', anchor: 'x', secondAnchor: 'y' },",
            "    verdict: { cited: true, citeKind: 'locations' } },",
            "  { candidate: { id: 'g', category: 'internal-contradiction', anchor: 'x', secondAnchor: 'y' },",
            "    verdict: { cited: true, citeKind: 'locations' } },",
            "  { candidate: { id: 'h', category: 'internal-contradiction' },",
            "    verdict: { cited: false } },",
            "]",
            ExtractBlock(source, "for (const row of triaged)"),
            "console.log(JSON.stringify({",
            "  states: assembled.map(f => f.state),",
            "  ids: assembled.map(f => f.id),",
            "  rejected: rejectedCitations.length,",
            "}))");

        string result = RunNode(harness);

        // In fixture order. The reason each row lands where it does is the point of the test.
        var expected = new (string Why, string State)[]
        {
            ("a declared invariant, cited by a classified finding", "blocking"),
            ("INV-99 does not exist, so the citation grants nothing", "suggested"),
            ("dec-ids-resolve is a real check that did not fail", "suggested"),
            ("paths-resolve is a check that did fail", "blocking"),
            ("RULE 2: unclassified can never block, however well cited", "suggested"),
            ("RULE 4: a location pair is self-evident only for a contradiction", "suggested"),
            ("a location pair on a contradiction is self-evident", "blocking"),
            ("no citation offered at all", "suggested"),
        };

        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(result);
        System.Text.Json.JsonElement states = doc.RootElement.GetProperty("states");

        Assert.AreEqual(expected.Length, states.GetArrayLength(),
            $"the loop produced the wrong number of findings. Full result: {result}");

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.AreEqual(expected[i].State, states[i].GetString(),
                $"row {i}: {expected[i].Why}. Full result: {result}");
        }

        // The two dropped citations are recorded rather than silently swallowed, so a run can
        // show what an agent tried to claim.
        Assert.AreEqual(2, doc.RootElement.GetProperty("rejected").GetInt32(),
            "unresolvable citations must be reported, not discarded quietly");

        // EVERY ID CARRIES ITS RUN. Ids have to be unique within a cycle, not within one
        // interrogation, because a disposition is recorded against an id and a cycle files a
        // document per stage and per repair round. Unprefixed, `reason1` would name a different
        // finding in each of them and dispositioning one would be ambiguous.
        foreach (System.Text.Json.JsonElement id in doc.RootElement.GetProperty("ids").EnumerateArray())
        {
            StringAssert.StartsWith(id.GetString(), "spec-r1-", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Lifts a brace-balanced block out of a workflow, so it can be executed against fixtures
    /// without the rest of the script's runtime hooks.
    /// </summary>
    private static string ExtractBlock(string source, string marker)
    {
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"'{marker}' is not in the workflow — the code under test is gone");

        int depth = 0;
        bool opened = false;
        for (int i = start; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
                opened = true;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (opened && depth == 0)
                {
                    return source[start..(i + 1)];
                }
            }
        }

        Assert.Fail($"'{marker}' has unbalanced braces");
        return string.Empty;
    }

    private static string RunNode(string script)
    {
        string temp = Path.Combine(Path.GetTempPath(), "wf-run-" + Guid.NewGuid().ToString("N")[..8] + ".mjs");
        File.WriteAllText(temp, script);

        try
        {
            using Process p = Process.Start(new ProcessStartInfo("node")
            {
                ArgumentList = { temp },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            })!;

            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            Assert.AreEqual(0, p.ExitCode, "node failed: " + stderr);
            return stdout.Trim();
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string ReplaceFirst(string text, string search, string replace)
    {
        int i = text.IndexOf(search, StringComparison.Ordinal);
        return i < 0 ? text : text[..i] + replace + text[(i + search.Length)..];
    }
}
