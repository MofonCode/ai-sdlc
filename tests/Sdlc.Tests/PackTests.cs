using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Sdlc.Tests;

/// <summary>
/// The dotnet-library stack pack: its facts, its rules, and its one code verb.
/// </summary>
/// <remarks>
/// The expensive test here earns its cost. <c>TheScaffoldedSolutionBuildsAndRunsATest</c>
/// runs the real scaffolder into a temporary directory and then builds and tests what it
/// produced, because a scaffolder is worth exactly what its output compiles to and nothing
/// less will establish that. It found two defects the first time it ran:
/// <list type="bullet">
/// <item>the pack reader matched only column-zero keys, so every version under the indented
/// <c>packages:</c> block read as empty and restore failed with NU1604 — on a solution the
/// scaffolder had just reported as successfully created;</item>
/// <item>the test project inherited <c>GenerateDocumentationFile</c> and <c>CA1515</c> from
/// the shared props, both of which fight MSTest's requirement that test classes be public,
/// so no test could compile at all.</item>
/// </list>
/// Neither was visible from reading the script.
/// </remarks>
[TestClass]
public sealed class PackTests
{
    private static string PackDir =>
        Path.Combine(AppContext.BaseDirectory, "packs", "dotnet-library");

    private static string PackYaml => File.ReadAllText(Path.Combine(PackDir, "pack.yaml"));

    private static string RulesYaml => File.ReadAllText(Path.Combine(PackDir, "pack.rules.yaml"));

    private static string ScaffoldPath => Path.Combine(PackDir, "scaffold.ps1");

    /// <summary>
    /// Structural fragments only the taxonomy itself contains. Category NAMES are excluded
    /// deliberately: an invariant's <c>Unlocks:</c> line names one, and treating that as a
    /// restatement reported a correct charter as broken.
    /// </summary>
    private static readonly string[] TaxonomyTells =
    [
        "| Category | Definition |",
        "Fits none of the above",
        "Tie-break:",
        "Axis 1",
    ];

    // --- the pack ships what a pack must ship ------------------------------

    [TestMethod]
    public void ThePackShipsItsFactsItsRulesAndItsCharter()
    {
        // A pack is three things and the split is load-bearing: facts change on someone
        // else's schedule and need only a reason, rules govern existing solutions and need
        // corpus-replay evidence, and the charter is what `init` copies.
        foreach (string file in new[] { "pack.yaml", "pack.rules.yaml", "CHARTER.md", "scaffold.ps1" })
        {
            Assert.IsTrue(File.Exists(Path.Combine(PackDir, file)),
                $"the pack is missing {file}");
        }
    }

    [TestMethod]
    public void ThePackCharterIsATemplateAndSaysSo()
    {
        // A pack charter is in force nowhere. If it read as a live charter someone would
        // eventually interrogate a solution against it, or worse, edit it as though it
        // governed something.
        string charter = File.ReadAllText(Path.Combine(PackDir, "CHARTER.md"));

        Assert.IsTrue(charter.Contains("This is a pack charter: a template", StringComparison.Ordinal));
        Assert.IsTrue(charter.Contains("TEMPLATE", StringComparison.Ordinal),
            "the fields init must replace have to be visibly unfilled");
    }

    [TestMethod]
    public void ThePackCharterDoesNotRestateTheEngineOwnedTaxonomy()
    {
        // The taxonomy, the state model and the disposition set belong to
        // schemas/findings.schema.json. A pack charter that copied them would be a second
        // list with nothing binding it — and there is no inheritance to keep them in step.
        string charter = File.ReadAllText(Path.Combine(PackDir, "CHARTER.md"));

        Assert.IsTrue(charter.Contains("engine-owned and are not restated", StringComparison.Ordinal),
            "the pack charter must say where the taxonomy lives rather than repeat it");

        // WHAT TO LOOK FOR, precisely. Counting category mentions was the first attempt and
        // it was a false positive: every invariant legitimately names the category it
        // `Unlocks:`, so a correct charter trips a mention count. The tell is the taxonomy's
        // STRUCTURE — a definition table, or the `unclassified` row that only the taxonomy
        // has — not the vocabulary, which invariants are entitled to use.
        //
        // A precise check beats a heuristic that flags correct artifacts, because the
        // heuristic is the one that gets switched off.
        foreach (string tell in TaxonomyTells)
        {
            Assert.IsFalse(charter.Contains(tell, StringComparison.Ordinal),
                $"the pack charter restates the taxonomy: found \"{tell}\", which belongs to " +
                "schemas/findings.schema.json");
        }
    }

    [TestMethod]
    public void ThePackCharterCarriesTheStackPackLineAsCharterContent()
    {
        // Bumping that line changes every rule governing the solution at once, so it is a
        // charter-review intent rather than routine maintenance. It has to live in the
        // charter for that to be true.
        string charter = File.ReadAllText(Path.Combine(PackDir, "CHARTER.md"));

        Assert.IsTrue(charter.Contains("Stack pack: dotnet-library@v1", StringComparison.Ordinal));
        Assert.IsTrue(charter.Contains("charter content", StringComparison.Ordinal),
            "the line must be identified as charter content, or it reads as metadata");
    }

    // --- the rules obey the same form rule as invariants -------------------

    [TestMethod]
    public void EveryPackRuleHasAUniqueIdAndForbidsSomething()
    {
        // The same acceptance test the charter applies to itself: a rule compatible with
        // every possible implementation is not a rule. Pack rules are citable through
        // INV-04, so a decorative one is a citation route to nothing.
        MatchCollection ids = Regex.Matches(RulesYaml, @"(?m)^\s*-\s+id:\s*(?<id>PR-\d{2})\s*$");
        Assert.IsTrue(ids.Count > 0, "the pack declares no rules");

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (Match m in ids)
        {
            Assert.IsTrue(seen.Add(m.Groups["id"].Value),
                $"{m.Groups["id"].Value} is declared twice; ids are assigned once and never reused");
        }

        // One `forbids:` and one `evidence:` per rule, or a rule is missing its half.
        int forbids = Regex.Count(RulesYaml, @"(?m)^\s+forbids:\s*");
        int evidence = Regex.Count(RulesYaml, @"(?m)^\s+evidence:\s*");

        Assert.AreEqual(ids.Count, forbids, "every rule needs a non-empty forbids");
        Assert.AreEqual(ids.Count, evidence, "every rule needs its evidence");
    }

    [TestMethod]
    public void PackRulesUnlockOnlyCategoriesTheSchemaDeclares()
    {
        // A rule unlocking a category the engine does not have is a citation route into
        // nothing, and it would only be discovered when a finding tried to use it.
        string[] categories = [.. System.Text.Json.JsonDocument
            .Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schemas", "findings.schema.json")))
            .RootElement.GetProperty("$defs").GetProperty("category").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!)];

        foreach (Match m in Regex.Matches(RulesYaml, @"(?m)^\s+unlocks:\s*(?<c>[a-z-]+)\s*$"))
        {
            CollectionAssert.Contains(categories, m.Groups["c"].Value,
                $"pack rule unlocks '{m.Groups["c"].Value}', which is not a category the schema declares");
        }
    }

    [TestMethod]
    public void ThePackKeepsToolNamesOutOfTheCharterByHoldingThemItself()
    {
        // This is the division of labour INV-04 exists to create. The spellings of a
        // suppression are stack-specific, so naming them in an invariant breaks the charter's
        // own exclusion of tool names — which is exactly the defect charter v2 fixed.
        Assert.IsTrue(RulesYaml.Contains("NoWarn", StringComparison.Ordinal),
            "the pack must carry the stack's suppression spellings");

        string charter = File.ReadAllText(Path.Combine(PackDir, "CHARTER.md"));
        Assert.IsFalse(charter.Contains("#pragma warning disable", StringComparison.Ordinal),
            "a pack charter must not name tool syntax; that is what the pack rules are for");
    }

    [TestMethod]
    public void TheLanguagePreferenceIsPerContextRatherThanGlobal()
    {
        // A single global ordering is wrong because the right answer differs by job, and this
        // is the Microsoft-first preference that must live in the pack rather than the
        // charter. JavaScript appears only where a runtime mandates it.
        Assert.IsTrue(PackYaml.Contains("language-preference:", StringComparison.Ordinal));
        Assert.IsTrue(PackYaml.Contains("workflow-orchestration:", StringComparison.Ordinal),
            "the one context where JavaScript is mandated must be named as such");
        Assert.IsTrue(PackYaml.Contains("mandate rather than a", StringComparison.Ordinal),
            "and it must say it is a mandate, not a preference");
    }

    [TestMethod]
    public void ThePackDeclaresTheGateCommandsSoTheCharterDoesNot()
    {
        // Get-SdlcVerifyOutcome takes these as parameters and merely defaults to them, which
        // is what lets a different pack bring a different toolchain.
        Assert.IsTrue(PackYaml.Contains("gates:", StringComparison.Ordinal));
        Assert.IsTrue(PackYaml.Contains("dotnet build", StringComparison.Ordinal));
        Assert.IsTrue(PackYaml.Contains("dotnet test", StringComparison.Ordinal));
    }

    // --- the binding check that would have caught the version bug ---------

    [TestMethod]
    public void EveryPackKeyTheScaffolderRequiresIsDeclared()
    {
        // THE BINDING CHECK. The scaffolder reads pack.yaml by key, so a key it requires and
        // the pack does not declare is a second list with nothing binding it. That is how the
        // version bug happened: the reader could not see the nested keys, the lookups
        // returned empty, and the scaffolder reported success while producing a solution that
        // would not restore.
        string scaffold = File.ReadAllText(ScaffoldPath);

        // Both the explicit requirement list and every lookup in the body.
        HashSet<string> required = new(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(scaffold, @"\$pack\['(?<k>[a-z0-9.-]+)'\]"))
        {
            required.Add(m.Groups["k"].Value);
        }

        Assert.IsTrue(required.Count > 0, "the scaffolder reads nothing from the pack");

        foreach (string key in required)
        {
            string[] parts = key.Split('.');
            string pattern = parts.Length == 1
                ? @"(?m)^" + Regex.Escape(parts[0]) + @":\s*\S"
                : @"(?m)^" + Regex.Escape(parts[0]) + @":[\s\S]*?^\s+" + Regex.Escape(parts[1]) + @":\s*\S";

            Assert.IsTrue(Regex.IsMatch(PackYaml, pattern),
                $"the scaffolder reads '{key}' but pack.yaml does not declare it");
        }
    }

    [TestMethod]
    public void TheScaffolderReadsNestedPackKeys()
    {
        // The regression. An earlier reader matched only column zero, so the indented
        // `packages:` block was invisible and every version came back empty.
        Assert.IsTrue(File.ReadAllText(ScaffoldPath)
            .Contains("AN EARLIER VERSION MATCHED ONLY COLUMN ZERO", StringComparison.Ordinal),
            "the reason the reader handles nesting must be recorded where someone would simplify it");
    }

    // --- the expensive one -------------------------------------------------

    [TestMethod]
    public void TheScaffoldedSolutionBuildsAndRunsATest()
    {
        // A scaffolder is worth what its output compiles to. Nothing short of building it
        // establishes that, and this test found both defects above on its first run.
        string root = Path.Combine(Path.GetTempPath(), "sdlc-scaffold-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);

        try
        {
            Run("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{ScaffoldPath}\" " +
                $"-SolutionName Fixture.Widgets -Root \"{root}\"",
                root);

            Directory.CreateDirectory(Path.Combine(root, "src", "Fixture.Widgets"));
            File.WriteAllText(Path.Combine(root, "src", "Fixture.Widgets", "Greeter.cs"), """
                namespace Fixture.Widgets;

                /// <summary>Documented, because the pack rules require it.</summary>
                public static class Greeter
                {
                    /// <summary>Returns a greeting.</summary>
                    /// <param name="name">Who to greet.</param>
                    /// <returns>The greeting.</returns>
                    public static string Greet(string name) => $"hello {name}";
                }
                """);

            File.WriteAllText(Path.Combine(root, "tests", "Fixture.Widgets.Tests", "GreeterTests.cs"), """
                namespace Fixture.Widgets.Tests;

                [TestClass]
                public class GreeterTests
                {
                    [TestMethod]
                    [TestProperty("AC", "AC-20260925-aa01-1")]
                    public void ItGreets() => Assert.AreEqual("hello world", Greeter.Greet("world"));
                }
                """);

            string output = Run("dotnet", "test", root, allowFailure: true);

            Assert.IsTrue(output.Contains("Passed!", StringComparison.Ordinal),
                "the scaffolded solution must build and run a test with an AC annotation:\n" + output);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [TestMethod]
    public void ScaffoldingTwiceSkipsRatherThanOverwrites()
    {
        // `init` is allowed to be re-run, so a scaffolder that clobbers is one keystroke from
        // destroying work.
        string root = Path.Combine(Path.GetTempPath(), "sdlc-idem-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);

        try
        {
            Run("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{ScaffoldPath}\" -SolutionName Fixture.Widgets -Root \"{root}\"",
                root);

            string marker = Path.Combine(root, "global.json");
            File.WriteAllText(marker, "{ \"edited\": true }");

            string second = Run("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{ScaffoldPath}\" -SolutionName Fixture.Widgets -Root \"{root}\"",
                root);

            Assert.IsTrue(second.Contains("global.json", StringComparison.Ordinal),
                "the second run must report what it skipped");
            Assert.AreEqual("{ \"edited\": true }", File.ReadAllText(marker),
                "an existing file must be left exactly as it was found");
        }
        finally
        {
            TryDelete(root);
        }
    }

    [TestMethod]
    public void TheScaffolderDoesNotWriteTheCharter()
    {
        // Provenance is `init`'s to stamp. A scaffolder that also wrote the charter could
        // claim a scaffolded origin for an inferred one, and `Origin:` is what tells every
        // later interrogation how much to trust the document.
        string root = Path.Combine(Path.GetTempPath(), "sdlc-nochart-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);

        try
        {
            string output = Run("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{ScaffoldPath}\" -SolutionName Fixture.Widgets -Root \"{root}\"",
                root);

            Assert.IsFalse(File.Exists(Path.Combine(root, ".sdlc", "CHARTER.md")),
                "the scaffolder must not write the charter");
            Assert.IsTrue(output.Contains("init copies CHARTER.md", StringComparison.Ordinal),
                "and it must say whose job it is, so the gap does not read as an omission");
        }
        finally
        {
            TryDelete(root);
        }
    }

    // --- plumbing ----------------------------------------------------------

    private static string Run(string exe, string args, string cwd, bool allowFailure = false)
    {
        using Process p = Process.Start(new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (!allowFailure)
        {
            Assert.AreEqual(0, p.ExitCode, $"{exe} {args} failed: {stderr}");
        }

        return stdout + stderr;
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                }

                Directory.Delete(dir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
