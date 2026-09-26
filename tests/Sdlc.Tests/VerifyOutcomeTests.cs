using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// The verification decision, exercised against a real repository.
/// </summary>
/// <remarks>
/// This is the test the old <c>workflows/verify.js</c> could not have. The decision lived in
/// a JavaScript branch and there is no JS runner here, so it was only ever checked by
/// grepping its source for the string <c>"decided in code"</c> — a check that would have
/// passed with the logic inverted.
/// <para>
/// The case that matters most is <c>PassesOnlyWhenEverythingIsClean</c>, because the
/// dangerous failure is not a wrong <c>blocked</c>. It is a <c>passed</c> reached by nothing
/// having contradicted it: a cycle with no spec, no criteria and no findings looks
/// indistinguishable from a clean one unless absence is treated as a blocker.
/// </para>
/// </remarks>
[TestClass]
public sealed class VerifyOutcomeTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Get-SdlcVerifyOutcome.ps1");

    private static string? _repo;

    /// <summary>
    /// Gate commands that succeed and fail without invoking a real toolchain. Passing the
    /// commands in means the production path always runs — there is no test-only bypass in
    /// the shipped script, which is the kind of switch that later gets left on.
    /// </summary>
    private const string GatePasses = "cmd /c exit 0";
    private const string GateFails = "cmd /c exit 1";

    [ClassInitialize]
    public static void CreateFixtureRepo(TestContext context)
    {
        _repo = Path.Combine(Path.GetTempPath(), "sdlc-outcome-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc"));

        File.WriteAllText(Path.Combine(_repo, ".sdlc", "CHARTER.md"), """
            ---
            version: 1
            solution: fixture
            ---

            # Charter — fixture

            ### INV-01 · Declared

                Forbids:  something specific.
            """);

        // Ids must be valid cycle ids — yyyymmdd plus four HEX characters. An earlier draft
        // of this fixture used 'cln1' and 'unc1' as mnemonics; l, n and u are not hex, so the
        // criteria parser correctly declined to read criteria keyed to a malformed id and
        // every coverage assertion failed for a reason that had nothing to do with coverage.
        //
        // A clean cycle: one verifiable criterion, and a test annotated with it.
        WriteCycle("20260924-aa01", verifiableCovered: true);

        // The same, except the annotated test is missing, so the criterion is unproven.
        WriteCycle("20260924-bb02", verifiableCovered: false);

        Directory.CreateDirectory(Path.Combine(_repo!, "src"));
        File.WriteAllText(Path.Combine(_repo!, "src", "Leaf.cs"), "// a leaf file, tier 1\n");

        Git("init --initial-branch=master");
        Git("add -A");
        Git("-c user.email=t@example.invalid -c user.name=Test commit -m fixture");
    }

    private static void WriteCycle(string id, bool verifiableCovered)
    {
        string dir = Path.Combine(_repo!, ".sdlc", "cycles", id);
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "spec.md"), $"""
            ---
            cycle-id: {id}
            intent: {id}
            charter-version: 1
            ---

            # Spec

            ## Acceptance criteria

            | ID | Verifiable | Criterion |
            |---|---|---|
            | AC-{id}-1 | yes | Provable by a test. |
            | AC-{id}-2 | no | A person judges this one. |
            """);

        File.WriteAllText(Path.Combine(dir, "plan.md"), $"""
            ---
            cycle-id: {id}
            spec: {id}
            charter-version: 1
            predicted-tier: 1
            approved: 2026-09-24
            ---

            # Plan

            ## Files

            | Path | Change |
            |---|---|
            | src/Leaf.cs | modify |
            """);

        if (verifiableCovered)
        {
            Directory.CreateDirectory(Path.Combine(_repo!, "tests"));
            File.WriteAllText(
                Path.Combine(_repo!, "tests", $"{id}Tests.cs"),
                $$"""
                 public class Generated_{{id.Replace("-", "_", StringComparison.Ordinal)}}
                 {
                     [TestProperty("AC", "AC-{{id}}-1")]
                     public void ItWorks() { }
                 }
                 """);
        }
    }

    [ClassCleanup]
    public static void RemoveFixtureRepo()
    {
        if (_repo is null || !Directory.Exists(_repo))
        {
            return;
        }

        try
        {
            foreach (string f in Directory.EnumerateFiles(_repo, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }

            Directory.Delete(_repo, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void Git(string args) => Run("git", args);

    private static string Run(string exe, string args)
    {
        using Process p = Process.Start(new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = _repo!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        Assert.AreEqual(0, p.ExitCode, $"{exe} {args} failed: {stderr}");
        return stdout;
    }

    private static JsonElement Decide(string cycleId, string build = GatePasses, string test = GatePasses)
    {
        string json = Run(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" " +
            $"-CycleId {cycleId} -BuildCommand \"{build}\" -TestCommand \"{test}\"");

        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static string OutcomeOf(JsonElement d) => d.GetProperty("outcome").GetString()!;

    [TestMethod]
    public void PassesOnlyWhenEverythingIsClean()
    {
        JsonElement d = Decide("20260924-aa01");

        Assert.AreEqual("passed", OutcomeOf(d), d.GetProperty("reason").GetString());
        Assert.AreEqual(0, d.GetProperty("blockers").GetArrayLength());
        Assert.AreEqual(0, d.GetProperty("undispositioned").GetInt32());
    }

    [TestMethod]
    public void AFailedBuildBlocks()
    {
        JsonElement d = Decide("20260924-aa01", build: GateFails);

        Assert.AreEqual("blocked", OutcomeOf(d));
        Assert.IsFalse(d.GetProperty("build-passed").GetBoolean());
    }

    [TestMethod]
    public void AFailedTestBlocks()
    {
        JsonElement d = Decide("20260924-aa01", test: GateFails);

        Assert.AreEqual("blocked", OutcomeOf(d));
        Assert.IsFalse(d.GetProperty("tests-passed").GetBoolean());
    }

    [TestMethod]
    public void AVerifiableCriterionWithNoTestBlocks()
    {
        // The gate that makes `Verifiable: yes` a commitment rather than a label.
        JsonElement d = Decide("20260924-bb02");

        Assert.AreEqual("blocked", OutcomeOf(d));
        Assert.IsTrue(d.GetProperty("uncovered").GetArrayLength() > 0,
            "the unproven criterion must be named, not just counted");
    }

    [TestMethod]
    public void AnUnverifiableCriterionIsNotExpectedToBeCovered()
    {
        // A criterion a person judges is legitimate. If it counted as uncovered, every spec
        // would be pushed toward a dishonest `yes`, which is the `untestable` finding.
        JsonElement d = Decide("20260924-aa01");

        Assert.AreEqual(2, d.GetProperty("criteria-total").GetInt32());
        Assert.AreEqual(1, d.GetProperty("criteria-verifiable").GetInt32());
        Assert.AreEqual(1, d.GetProperty("criteria-covered").GetInt32());
    }

    [TestMethod]
    public void AnUndispositionedFindingBlocks()
    {
        // Every finding, from adversary or tool, at any severity, is dispositioned.
        // Filed as one document per interrogation, which is the layout every consumer now reads:
        // a cycle interrogates several artifacts over several rounds, and the schema carries one
        // target and round per document.
        string dir = Path.Combine(_repo!, ".sdlc", "cycles", "20260924-aa01", "findings");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "spec.r1.json");
        File.WriteAllText(path, """
            {
              "cycle-id": "20260924-aa01",
              "target": "spec",
              "charter-version": 1,
              "findings": [
                { "id": "r1", "category": "ambiguity", "state": "blocking", "stage": "spec",
                  "detection": "reasoning", "summary": "Two readings.",
                  "cites": [{ "kind": "invariant", "id": "INV-01" }] }
              ]
            }
            """);

        try
        {
            JsonElement d = Decide("20260924-aa01");

            Assert.AreEqual("blocked", OutcomeOf(d));
            Assert.AreEqual(1, d.GetProperty("undispositioned").GetInt32());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ADispositionedFindingDoesNotBlock()
    {
        // Filed as one document per interrogation, which is the layout every consumer now reads:
        // a cycle interrogates several artifacts over several rounds, and the schema carries one
        // target and round per document.
        string dir = Path.Combine(_repo!, ".sdlc", "cycles", "20260924-aa01", "findings");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "spec.r1.json");
        File.WriteAllText(path, """
            {
              "cycle-id": "20260924-aa01",
              "target": "spec",
              "charter-version": 1,
              "findings": [
                { "id": "r1", "category": "ambiguity", "state": "blocking", "stage": "spec",
                  "detection": "reasoning", "summary": "Two readings.",
                  "cites": [{ "kind": "invariant", "id": "INV-01" }],
                  "disposition": "fixed" }
              ]
            }
            """);

        try
        {
            JsonElement d = Decide("20260924-aa01");

            Assert.AreEqual("passed", OutcomeOf(d), d.GetProperty("reason").GetString());
            Assert.AreEqual(0, d.GetProperty("undispositioned").GetInt32());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void MissingEvidenceBlocksRatherThanPasses()
    {
        // THE IMPORTANT ONE. A cycle with no spec and no plan has nothing that could
        // contradict a pass, so a decision built as "block when something is wrong" reports
        // it as clean. Absence of evidence is not evidence of a clean cycle.
        JsonElement d = Decide("20260924-cc03");

        Assert.AreEqual("blocked", OutcomeOf(d));
        Assert.IsTrue(d.GetProperty("reason").GetString()!.Contains("no evidence", StringComparison.Ordinal),
            "the reason must say the evidence is missing, not invent a different blocker");
    }

    [TestMethod]
    public void TierEscalationOutranksAFailingBuild()
    {
        // Order matters. A failing build is a normal blocked cycle someone fixes; a tier
        // escalation means the work became more dangerous than it was approved to be, and
        // reporting that as an ordinary failure invites fix-and-retry instead of a person
        // looking. Asserted on the script's own rule ordering, because producing a real
        // tier-3 diff in a fixture would require migrations or auth paths on disk.
        string text = File.ReadAllText(ScriptPath);

        int escalation = text.IndexOf("halted-tier-escalated", StringComparison.Ordinal);
        int buildFailed = text.IndexOf("the build failed", StringComparison.Ordinal);

        Assert.IsTrue(escalation > 0 && buildFailed > 0);
        Assert.IsTrue(escalation < buildFailed,
            "the tier check must be evaluated before the gate checks");
    }

    [TestMethod]
    public void TheGateCommandsAreParametersSoAPackCanOverrideThem()
    {
        // INV-04 delegates the stack to the pack, and tool names stay out of the charter.
        //
        // THE PARAMETER DEFAULT WAS NOT ENOUGH, and this test used to assert exactly that: it
        // required the literal `= 'dotnet build'` in the param block, which is a default a caller can
        // override and NOTHING ELSE. pack.yaml declared its gates and nothing read them, so the
        // delegation the charter depends on stopped one step short of the script that runs the gate.
        //
        // Resolution order now, and the middle step is the one that was missing: a caller's value,
        // then the declared pack's, then the .NET default for a solution consuming none.
        string text = File.ReadAllText(ScriptPath);

        Assert.IsTrue(text.Contains("Get-SdlcPackGate", StringComparison.Ordinal),
            "the pack declares the gates, so this must ask it rather than assuming a toolchain");
        Assert.IsTrue(text.Contains("[string] $BuildCommand,", StringComparison.Ordinal),
            "a parameter with a hardcoded default cannot tell a caller's choice from an assumption");
        Assert.IsTrue(text.Contains("if (-not $BuildCommand) { $BuildCommand = 'dotnet build' }", StringComparison.Ordinal),
            "solution zero consumes no pack, so a last-resort default still has to exist");
        Assert.IsTrue(text.Contains("INV-04", StringComparison.Ordinal),
            "the reason the default is only a default must be stated where someone would hardcode it");
    }

    // --- INV-08's detection half gates, rather than only reporting ----------

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-28")]
    public void BlocksWhenAProtectedPathChangedWithNoLicence()
    {
        // THE CORRECTION THE SPEC'S SECOND VERSION RESTS ON. This script consumes no checks at all -
        // it reads specific fact fields - so a detection that emitted only a named check would have
        // gated nothing at all. The check makes the finding citable; this makes it binding.
        string charter = Path.Combine(_repo!, ".sdlc", "CHARTER.md");
        string original = File.ReadAllText(charter);

        try
        {
            // On master, which carries no intent and therefore no licence for anything.
            File.WriteAllText(charter, original + "\nan amendment nothing authorised\n");

            JsonElement d = Decide("20260924-aa01");

            Assert.AreEqual("blocked", OutcomeOf(d),
                "an unlicensed charter change has to stop a landing, or INV-08 is advisory again");

            string[] blockers = [.. d.GetProperty("blockers").EnumerateArray()
                .Select(e => e.GetString()!)];

            Assert.IsTrue(blockers.Any(b => b.Contains("INV-08", StringComparison.Ordinal)),
                "the blocker has to name the rule: " + string.Join(" | ", blockers));

            Assert.AreEqual(1, d.GetProperty("unlicensed-protected-paths").GetArrayLength(),
                "the outcome reports what it blocked on, not only that it blocked");
        }
        finally
        {
            File.WriteAllText(charter, original);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-21")]
    public void ThisRepositoryReportsNoUnjustifiedSuppressionsAndNoOrphanAnnotations()
    {
        // The criterion that says the fix actually unblocked THIS repository rather than only
        // working on a fixture. Both checks reported false positives here and the verification
        // outcome blocks on both, so no cycle in this tree could pass verification - including any
        // cycle that tried to fix something else.
        //
        // Run against the real checkout, found by walking up from the test output, because the
        // false positives live in this repository's own sources and a temporary fixture cannot
        // reproduce them. A missing checkout FAILS rather than skips: a claim about this repository
        // that quietly went unverified is the shape of defect this whole criterion is about.
        string? root = FindCheckout(AppContext.BaseDirectory);
        Assert.IsNotNull(root, "no git checkout found above the test output, so the claim is unverified");

        string[] specs = [.. Directory
            .EnumerateFiles(Path.Combine(root!, ".sdlc", "cycles"), "spec.md", SearchOption.AllDirectories)
            .OrderBy(s => s, StringComparer.Ordinal)];

        Assert.IsTrue(specs.Length > 0, "no spec in this repository to gather facts for");

        // ORPHANS ARE COUNTED PER CYCLE, AGAINST THAT CYCLE'S OWN SPEC, and the narrowing is
        // deliberate rather than convenient. `orphan-annotations` compares every annotation in the
        // suite against ONE spec's criteria, so once a second cycle annotates a test, each cycle's
        // annotations read as orphans of the other's spec. That is a real defect in the check and it
        // is not this cycle's to fix - but it means the only stable form of this claim is the one
        // that matters: no annotation names a criterion ITS OWN spec fails to declare.
        List<string> offenders = [];
        List<string> unjustifiedAnywhere = [];

        foreach (string spec in specs)
        {
            string cycleId = Path.GetFileName(Path.GetDirectoryName(spec)!);
            using JsonDocument facts = GatherFor(root!, spec);
            JsonElement t = facts.RootElement.GetProperty("traceability");

            offenders.AddRange(t.GetProperty("orphan-annotations")
                .EnumerateArray()
                .Select(e => e.GetString()!)
                .Where(id => id.StartsWith("AC-" + cycleId + "-", StringComparison.Ordinal)));

            // Spec-independent, so reading it from the first gather is enough - but read from every
            // one anyway, because a scan that depended on which artifact asked would be its own bug.
            unjustifiedAnywhere.AddRange(t.GetProperty("unjustified-suppressions")
                .EnumerateArray().Select(e => e.GetString()!));
        }

        Assert.AreEqual(0, unjustifiedAnywhere.Distinct().Count(),
            "unjustified suppressions in this repository: "
            + string.Join(", ", unjustifiedAnywhere.Distinct()));

        Assert.AreEqual(0, offenders.Count,
            "annotations naming a criterion their own spec does not declare: "
            + string.Join(", ", offenders));
    }

    private static JsonDocument GatherFor(string root, string specPath)
    {
        string factsScript = Path.Combine(root, "tools", "Get-SdlcFacts.ps1");
        string relative = Path.GetRelativePath(root, specPath).Replace('\\', '/');

        using Process p = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            ArgumentList =
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", factsScript, "-Artifact", relative,
            },
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string json = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        Assert.AreEqual(0, p.ExitCode, $"the fact-gatherer failed on {relative}: {stderr}");

        return JsonDocument.Parse(json);
    }

    private static string? FindCheckout(string start)
    {
        string? dir = start;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, ".git"))
                && Directory.Exists(Path.Combine(dir, ".sdlc")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}
