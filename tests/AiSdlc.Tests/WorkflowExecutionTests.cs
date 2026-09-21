using System.Text.Json;

namespace AiSdlc.Tests;

/// <summary>
/// The shipped workflow body, executed rather than parsed.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WorkflowTests"/> proves a workflow body parses inside the wrapper the runtime
/// puts around it, and says in its own source that it deliberately never runs what it builds.
/// That gap shipped a real defect: the parallel build's dependency-wave loop removed dispatched
/// slices by testing slice objects against a string id, a comparison that is always false, so
/// the loop could not end and nothing below it had ever executed - not the approval gate, not
/// one implementer, not verification. It parsed perfectly every time.
/// </para>
/// <para>
/// This class drives the real file at <c>plugins/sdlc-core/workflows/parallel-build.js</c>
/// through <c>scripts/run-workflow.mjs</c>, which wraps the body the same way and runs it with
/// the five globals the runtime binds, against doubles supplied per scenario. What is asserted
/// is control flow: that it finishes, which agents it dispatched, and crucially which ones it
/// dispatched <em>together</em>.
/// </para>
/// <para>
/// What this does not prove, stated here because a green run invites the stronger reading: the
/// gate, the dispatch and the verification stage have run against doubles, never against
/// agents. This is evidence the body's control flow is right. It is not evidence that fan-out
/// works, and a first live fan-out still matters.
/// </para>
/// </remarks>
[TestClass]
public sealed class WorkflowExecutionTests
{
    private static string Harness => Path.Combine(Repo.Root, "scripts", "run-workflow.mjs");

    private static string ParseValidator => Path.Combine(Repo.Root, "scripts", "check-workflow.mjs");

    private static string ParallelBuild => Path.Combine(
        Repo.Root, "plugins", "sdlc-core", "workflows", "parallel-build.js");

    private const string SliceA =
        """{ "id": "a", "files": ["src/A.cs"], "requirements": ["R1"], "depends_on": [] }""";

    private const string SliceBAfterA =
        """{ "id": "b", "files": ["src/B.cs"], "requirements": ["R2"], "depends_on": ["a"] }""";

    private const string SliceBBesideA =
        """{ "id": "b", "files": ["src/B.cs"], "requirements": ["R2"], "depends_on": [] }""";

    [TestMethod]
    public void TheBuild_TerminatesOnASinglePlanSlice()
    {
        // The cheapest reproduction of the defect: the loop fails to terminate on a plan with
        // nothing to order, because the bug is in how a dispatched slice is removed and not in
        // the dependency arithmetic.
        Execution run = Execute(
            slices: SliceA,
            responses: Gate("a") + "," + Build("a", "src/A.cs") + "," + Verify(ship: true));

        Assert.IsTrue(run.Ok, run.Error);
        Assert.AreEqual("verified", run.Status);
        AssertDispatchedSomething(run);
    }

    [TestMethod]
    public void TheBuild_TerminatesWhenOneSliceDependsOnAnother()
    {
        Execution run = Execute(
            slices: SliceA + "," + SliceBAfterA,
            responses: Gate("a", "b") + "," + Build("a", "src/A.cs") + "," +
                       Build("b", "src/B.cs") + "," + Verify(ship: true));

        Assert.IsTrue(run.Ok, run.Error);
        Assert.AreEqual("verified", run.Status);
        CollectionAssert.AreEqual(
            new[] { "Gate", "Build", "Build", "Verify" },
            run.Phases,
            "phases entered: " + string.Join(", ", run.Phases));
    }

    [TestMethod]
    public void ADependentSlice_IsNotDispatchedUntilTheWaveBeforeItIsDone()
    {
        Execution run = Execute(
            slices: SliceA + "," + SliceBAfterA,
            responses: Gate("a", "b") + "," + Build("a", "src/A.cs") + "," +
                       Build("b", "src/B.cs") + "," + Verify(ship: true));

        Assert.IsTrue(run.Ok, run.Error);

        // Groups, not a flat list. A flat list reads identically for a body that fans out and
        // one that runs the same work in sequence, so it cannot prove what this asserts.
        AssertGroups(run, ["gate:demo"], ["build:a"], ["build:b"], ["verify:demo"]);
    }

    [TestMethod]
    public void IndependentSlices_AreDispatchedInOneWave()
    {
        Execution run = Execute(
            slices: SliceA + "," + SliceBBesideA,
            responses: Gate("a", "b") + "," + Build("a", "src/A.cs") + "," +
                       Build("b", "src/B.cs") + "," + Verify(ship: true));

        Assert.IsTrue(run.Ok, run.Error);

        // The whole point of the workflow. If these two arrive in separate groups it is a
        // serial build wearing a parallel build's name.
        AssertGroups(run, ["gate:demo"], ["build:a", "build:b"], ["verify:demo"]);
    }

    [TestMethod]
    public void ADraftPlan_IsRefusedAndNoImplementerIsDispatched()
    {
        Execution run = Execute(
            slices: SliceA,
            responses: """ "gate:demo": { "status": "draft", "sliceIds": ["a"], "manifestMatches": true } """);

        Assert.IsTrue(run.Ok, run.Error);
        Assert.AreEqual("blocked", run.Status);
        AssertNoImplementerDispatched(run);
    }

    [TestMethod]
    public void APayloadThatDoesNotMatchThePlan_IsRefusedAndNoImplementerIsDispatched()
    {
        // The gate reads the file rather than trusting the payload precisely so that a stale
        // args block cannot build something nobody approved.
        Execution run = Execute(
            slices: SliceA,
            responses: """ "gate:demo": { "status": "approved", "sliceIds": ["a", "gone"], "manifestMatches": false } """);

        Assert.IsTrue(run.Ok, run.Error);
        Assert.AreEqual("blocked", run.Status);
        AssertNoImplementerDispatched(run);
    }

    [TestMethod]
    public void ARedBuild_HaltsBeforeTheNextWaveIsDispatched()
    {
        // Later waves consume what earlier ones built, so a green build is a precondition and
        // not a preference. `b` must never be dispatched.
        Execution run = Execute(
            slices: SliceA + "," + SliceBAfterA,
            responses: Gate("a", "b") + "," + Build("a", "src/A.cs", passed: false));

        Assert.IsTrue(run.Ok, run.Error);
        Assert.AreEqual("halted", run.Status);
        Assert.IsFalse(
            run.Labels.Contains("build:b"),
            "wave 2 was dispatched after wave 1 failed: " + string.Join(", ", run.Labels));
        Assert.IsFalse(
            run.Labels.Contains("verify:demo"),
            "verification ran over a halted build: " + string.Join(", ", run.Labels));
    }

    [TestMethod]
    public void ADoNotShipVerdict_ReturnsBlockedRatherThanVerified()
    {
        Execution run = Execute(
            slices: SliceA,
            responses: Gate("a") + "," + Build("a", "src/A.cs") + "," + Verify(ship: false));

        Assert.IsTrue(run.Ok, run.Error);
        Assert.AreEqual("blocked", run.Status);
    }

    [TestMethod]
    public void TheHarness_ReportsABodyThatDoesNotTerminate()
    {
        // The bound is the load-bearing part of the harness and is proved against a body built
        // to spin, so it stays proved after the shipped body is fixed. Without this, the day
        // the defect is repaired is the day nothing checks the check.
        string spinner = Path.Combine(
            Path.GetTempPath(), "wf-spin-" + Guid.NewGuid().ToString("N") + ".js");

        File.WriteAllText(
            spinner,
            """
            export const meta = {
              name: 'never-ends',
              description: 'A body whose loop cannot terminate, so the bound has something to catch',
              phases: [{ title: 'Spin' }],
            }

            phase('Spin')
            let n = 0
            while (true) {
              n = n + 1
            }
            return n
            """);

        try
        {
            Execution run = ExecuteScenario(spinner, ScenarioJson(SliceA, Gate("a"), budgetMs: 1500));

            Assert.IsFalse(run.Ok, "a body that cannot terminate was reported as completing");
            StringAssert.Contains(
                run.Error ?? string.Empty,
                "did not terminate",
                "the failure has to say what happened, or it reads as an unexplained timeout");
        }
        finally
        {
            File.Delete(spinner);
        }
    }

    [TestMethod]
    public void TheHarness_RefusesAnAgentTheScenarioDoesNotAnswer()
    {
        // A double that answers anything lets a body dispatch something nobody expected and
        // still pass, which is the shape of failure this whole class exists to catch.
        Execution run = Execute(slices: SliceA, responses: Gate("a"));

        Assert.IsFalse(run.Ok, "an unanswered agent label was silently defaulted");
        StringAssert.Contains(run.Error ?? string.Empty, "build:a");
    }

    [TestMethod]
    public void TheHarnessAndTheParseValidator_BindTheSameGlobals()
    {
        // Two validators reading one format. If they disagree about what a body may call, a
        // body can parse under one and be impossible under the other, and nothing says so.
        string[] parsed = QuotedNames(
            Between(File.ReadAllText(ParseValidator), "new Function(", ")"));

        string[] executed = QuotedNames(
            Between(File.ReadAllText(Harness), "const BOUND = [", "]"));

        Assert.IsTrue(executed.Length > 0, "no bound globals found in the harness - asserted nothing");
        Assert.IsTrue(parsed.Length > 0, "no bound globals found in the parse validator - asserted nothing");

        CollectionAssert.AreEqual(
            parsed,
            executed,
            "the parse validator and the execution harness bind different globals:" +
            Environment.NewLine + "  parses:   " + string.Join(", ", parsed) +
            Environment.NewLine + "  executes: " + string.Join(", ", executed));
    }

    // --- running a scenario -----------------------------------------------------------------

    private static Execution Execute(string slices, string responses) =>
        ExecuteScenario(ParallelBuild, ScenarioJson(slices, responses));

    private static Execution ExecuteScenario(string workflow, string scenarioJson)
    {
        string scenario = Path.Combine(
            Path.GetTempPath(), "wf-scenario-" + Guid.NewGuid().ToString("N") + ".json");

        File.WriteAllText(scenario, scenarioJson);

        try
        {
            Repo.RunResult result = Repo.Run("node", [Harness, workflow, scenario]);

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(result.StdOut),
                "the harness printed nothing." + Environment.NewLine + result.Combined);

            using JsonDocument document = JsonDocument.Parse(result.StdOut);
            JsonElement root = document.RootElement;

            return new Execution(
                Ok: root.GetProperty("ok").GetBoolean(),
                Error: root.TryGetProperty("error", out JsonElement error) ? error.GetString() : null,
                Phases: Strings(root, "phases"),
                Groups: Groups(root),
                Status: Status(root));
        }
        finally
        {
            File.Delete(scenario);
        }
    }

    private static string ScenarioJson(string slices, string responses, int budgetMs = 5000) =>
        $$"""
        {
          "args": { "slug": "demo", "artifactRoot": ".sdlc", "slices": [ {{slices}} ] },
          "agentResponses": { {{responses}} },
          "limits": { "ms": {{budgetMs}} }
        }
        """;

    private static string Gate(params string[] sliceIds) =>
        $$"""
        "gate:demo": { "status": "approved", "sliceIds": [{{string.Join(", ", sliceIds.Select(Quote))}}], "manifestMatches": true }
        """;

    private static string Build(string sliceId, string file, bool passed = true) =>
        $$"""
        "build:{{sliceId}}": { "sliceId": {{Quote(sliceId)}}, "filesTouched": [{{Quote(file)}}], "buildPassed": {{(passed ? "true" : "false")}}, "notes": "" }
        """;

    private static string Verify(bool ship) =>
        $$"""
        "verify:demo": { "ship": {{(ship ? "true" : "false")}}, "blocking": {{(ship ? "[]" : """["a blocking finding"]""")}} }
        """;

    private static string Quote(string value) => "\"" + value + "\"";

    // --- reading what came back ---------------------------------------------------------------

    private sealed record Execution(
        bool Ok,
        string? Error,
        string[] Phases,
        string[][] Groups,
        string? Status)
    {
        /// <summary>Every dispatched label, flattened - for asking whether one ever happened.</summary>
        public string[] Labels => Groups.SelectMany(g => g).ToArray();
    }

    private static string[] Strings(JsonElement root, string property) =>
        root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray()
            : [];

    private static string[][] Groups(JsonElement root) =>
        root.TryGetProperty("dispatched", out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Select(g => g.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray())
                .ToArray()
            : [];

    private static string? Status(JsonElement root) =>
        root.TryGetProperty("result", out JsonElement result)
        && result.ValueKind == JsonValueKind.Object
        && result.TryGetProperty("status", out JsonElement status)
            ? status.GetString()
            : null;

    // --- assertions -----------------------------------------------------------------------

    private static void AssertGroups(Execution run, params string[][] expected)
    {
        string actual = Render(run.Groups);
        string wanted = Render(expected);

        Assert.AreEqual(
            wanted,
            actual,
            "dispatch groups differ." + Environment.NewLine +
            "  expected: " + wanted + Environment.NewLine +
            "  actual:   " + actual);
    }

    private static void AssertNoImplementerDispatched(Execution run) =>
        Assert.IsFalse(
            run.Labels.Any(l => l.StartsWith("build:", StringComparison.Ordinal)),
            "the gate refused and an implementer was dispatched anyway: " +
            string.Join(", ", run.Labels));

    private static void AssertDispatchedSomething(Execution run) =>
        Assert.IsTrue(
            run.Labels.Length > 0,
            "the body reported no dispatches at all - this assertion compared nothing");

    private static string Render(IEnumerable<string[]> groups) =>
        string.Join(" ", groups.Select(g => "[" + string.Join(", ", g) + "]"));

    // --- reading a literal list out of a script ---------------------------------------------

    private static string Between(string text, string open, string close)
    {
        int start = text.IndexOf(open, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "could not find \"" + open + "\"");

        int from = start + open.Length;
        int end = text.IndexOf(close, from, StringComparison.Ordinal);
        Assert.IsTrue(end > from, "could not find \"" + close + "\" after \"" + open + "\"");

        return text[from..end];
    }

    private static string[] QuotedNames(string fragment) =>
        System.Text.RegularExpressions.Regex
            .Matches(fragment, "['\"]([A-Za-z_][A-Za-z0-9_]*)['\"]")
            .Select(m => m.Groups[1].Value)
            .ToArray();
}
