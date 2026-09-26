using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// Drives the shipped PowerShell as an external process rather than reimplementing it
/// in C#. A test that reimplements the logic proves the reimplementation, not the thing
/// that ships — and what ships is Windows PowerShell 5.1, so that is what runs here.
/// </summary>
[TestClass]
public sealed class FactGatheringTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Get-SdlcFacts.ps1");

    private static string? _repo;

    /// <summary>
    /// A throwaway git repository with a minimal charter and two artifacts: one governed
    /// and deliberately broken, one prose. Real repo, real git, real resolution.
    /// </summary>
    [ClassInitialize]
    public static void CreateFixtureRepo(TestContext context)
    {
        _repo = Path.Combine(Path.GetTempPath(), "sdlc-facts-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "intents"));

        File.WriteAllText(Path.Combine(_repo, ".sdlc", "CHARTER.md"), """
            ---
            version: 3
            solution: fixture
            ---

            # Charter — fixture

            ### INV-01 · Declared for the fixture

                Forbids:  something specific.

            ### INV-02 · Also declared

                Forbids:  something else.

            ## Open-decision register

            | ID | Deliberately open |
            |---|---|
            | DEC-01 | still open |
            | ~~DEC-02~~ | removed, and the gap is the record |
            """);

        // THE FORM RULE, which the charter asserts about itself twice. The fixture charter
        // above carries `Forbids:` and no `Applies-to:`, so it FAILS the rule - true of it,
        // and useful. These two make the pass and the both-ways-broken cases explicit.
        //
        // Artifact kind comes from the leaf filename, so a charter in its own directory is
        // still a charter.
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "well-formed"));
        File.WriteAllText(Path.Combine(_repo, ".sdlc", "well-formed", "CHARTER.md"), """
            ---
            version: 1
            solution: fixture
            ---

            # Charter - well formed

            ### INV-01 - Carries both clauses

                Applies-to: intent, spec
                Forbids:  something an implementation could actually do.
            """);

        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "malformed"));
        File.WriteAllText(Path.Combine(_repo, ".sdlc", "malformed", "CHARTER.md"), """
            ---
            version: 1
            solution: fixture
            ---

            # Charter - broken both ways

            ### INV-01 - No Forbids at all

                Applies-to: intent
                Evidence: a clause that is not the one the rule requires.

            ### INV-02 - An EMPTY Applies-to, which the charter names explicitly

                Applies-to:
                Forbids:  something specific.
            """);

        // Governed, and broken in three distinct ways so each check can be named.
        File.WriteAllText(Path.Combine(_repo, ".sdlc", "intents", "20260924-aaaa.md"), """
            ---
            id: 20260924-aaaa
            source: authored
            ---

            # A deliberately broken intent

            Cites `INV-02`, which exists, and `INV-09`, which does not.
            Defers to `DEC-01`, which exists, and `DEC-07`, which does not.
            Points at `src/does/not/exist.cs`.
            Mentions `verify.md`, which is an artifact kind rather than a path,
            and the `/sdlc:init` command, which is not a path either.
            """);

        File.WriteAllText(Path.Combine(_repo, "notes.md"), """
            # Prose, not governed

            Describes `intents/` and `retros/` as future directories, and cites `LOC-01`
            as an illustration. None of these exist, and none of it is wrong.
            """);

        // A spec whose criteria table parses, including a duplicated id — duplication is
        // not cosmetic, because two criteria sharing an id make traceability report one
        // as covered when the other is not.
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "cycles", "20260924-bbbb"));
        File.WriteAllText(Path.Combine(_repo, ".sdlc", "cycles", "20260924-bbbb", "spec.md"), """
            ---
            cycle-id: 20260924-bbbb
            intent: 20260924-bbbb
            charter-version: 3
            ---

            # Spec

            ## Acceptance criteria

            | ID | Verifiable | Criterion |
            |---|---|---|
            | AC-20260924-bbbb-1 | yes | Provable by a test. |
            | AC-20260924-bbbb-2 | no | Should feel responsive. |
            | AC-20260924-bbbb-2 | yes | A duplicated id. |
            """);

        // A plan declaring one file that exists and one that does not, which is the
        // inconsistency INV-05's declaration exists to make detectable.
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "cycles", "20260924-cccc"));
        File.WriteAllText(Path.Combine(_repo, ".sdlc", "cycles", "20260924-cccc", "plan.md"), """
            ---
            cycle-id: 20260924-cccc
            spec: 20260924-cccc
            charter-version: 3
            ---

            # Plan

            ## Files

            | Path | Change |
            |---|---|
            | notes.md | modify |
            | src/absent.cs | modify |
            """);

        // A plan touching migrations and auth — tier 3, because neither can be unwound
        // by reverting the commit.
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "cycles", "20260924-dddd"));
        File.WriteAllText(Path.Combine(_repo, ".sdlc", "cycles", "20260924-dddd", "plan.md"), """
            ---
            cycle-id: 20260924-dddd
            spec: 20260924-dddd
            charter-version: 3
            ---

            # Plan

            ## Files

            | Path | Change |
            |---|---|
            | src/Migrations/0001_Init.cs | new |
            | src/Auth/TokenService.cs | new |
            """);

        // A plan touching one leaf file — tier 1, nothing flagged.
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "cycles", "20260924-eeee"));
        File.WriteAllText(Path.Combine(_repo, ".sdlc", "cycles", "20260924-eeee", "plan.md"), """
            ---
            cycle-id: 20260924-eeee
            spec: 20260924-eeee
            charter-version: 3
            ---

            # Plan

            ## Files

            | Path | Change |
            |---|---|
            | src/Leaf.cs | new |
            """);

        // NOT PURE ASCII, deliberately. Every other fixture in this suite is, and that is
        // precisely why the suite could not see the encoding defect: the real charter's
        // frontmatter contains em-dashes, the gatherer emitted structurally invalid JSON for
        // it, and all 146 tests passed anyway.
        File.WriteAllText(Path.Combine(_repo, ".sdlc", "intents", "20260924-ffff.md"), """
            ---
            id: 20260924-ffff
            source: authored
            origin: hand-written — an em-dash in a frontmatter value
            ---

            # An intent whose prose uses the house style

            House style uses em-dashes — liberally — and curly quotes
            like “this”, so an artifact without them is the exception.
            """);

        Git("init --initial-branch=main");
        Git("add -A");
        Git("-c user.email=t@example.invalid -c user.name=Test commit -m fixture");
    }

    [ClassCleanup]
    public static void RemoveFixtureRepo()
    {
        if (_repo is not null && Directory.Exists(_repo))
        {
            try
            {
                // .git holds read-only objects on Windows.
                foreach (string f in Directory.EnumerateFiles(_repo, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                }

                Directory.Delete(_repo, recursive: true);
            }
            catch (IOException)
            {
                // A leaked temp directory is not worth failing a suite over.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void Git(string args) => Run("git", args, _repo!);

    private static string Run(string exe, string args, string workingDirectory)
    {
        using Process p = Process.Start(new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,

            // Read as UTF-8 explicitly. Without this the harness decodes the child's output
            // using the test host's default codepage, so a script that correctly emits UTF-8
            // would still arrive mangled and the check would be measuring the harness.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        Assert.AreEqual(0, p.ExitCode, $"{exe} {args} failed: {stderr}");
        return stdout;
    }

    private static JsonDocument Gather(string artifactRelativePath)
    {
        string json = Run(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" -Artifact \"{artifactRelativePath}\"",
            _repo!);

        return JsonDocument.Parse(json);
    }

    private static string[] FailedChecks(JsonDocument facts) =>
        [.. facts.RootElement.GetProperty("failed-checks").EnumerateArray()
            .Select(e => e.GetString()!)];

    [TestMethod]
    public void AWellFormedCharterPassesTheFormRule()
    {
        // The charter says twice that a missing or empty clause "fails the form check", and
        // there was no form check. The rule governing the shape of every invariant could be
        // violated with nothing to cite — not a missing feature, a claim the document makes
        // and did not keep.
        using JsonDocument facts = Gather(".sdlc/well-formed/CHARTER.md");

        Assert.IsFalse(FailedChecks(facts).Contains("invariant-form"),
            "an invariant carrying both clauses satisfies the rule");
    }

    [TestMethod]
    public void AMissingForbidsAndAnEmptyAppliesToBothFailTheFormRule()
    {
        // Both halves, because the charter states them as separate sentences and an empty
        // `Applies-to:` is called out by name: it "governs nothing and fails the form check
        // for the same reason an empty `Forbids:` does".
        //
        // The EMPTY case is the one a looser parser misses. `Applies-to:` with nothing after
        // it still matches a search for the label, so the check has to require something
        // after the colon rather than merely the clause being present.
        using JsonDocument facts = Gather(".sdlc/malformed/CHARTER.md");

        Assert.IsTrue(FailedChecks(facts).Contains("invariant-form"),
            "one invariant has no Forbids: and another has an empty Applies-to:");

        string detail = facts.RootElement.GetProperty("checks").EnumerateArray()
            .First(c => c.GetProperty("id").GetString() == "invariant-form")
            .GetProperty("detail").GetString()!;

        StringAssert.Contains(detail, "INV-01", StringComparison.Ordinal);
        StringAssert.Contains(detail, "Forbids:", StringComparison.Ordinal);
        StringAssert.Contains(detail, "INV-02", StringComparison.Ordinal);
        StringAssert.Contains(detail, "Applies-to:", StringComparison.Ordinal);
    }

    [TestMethod]
    public void TheFormRuleRunsOnlyForACharter()
    {
        // An intent has no invariants, and reporting a form failure against one would be a
        // check firing on an artifact it says nothing about — the noise that gets a check
        // switched off, which is the INV-06 erosion failure one layer up.
        using JsonDocument facts = Gather(".sdlc/intents/20260924-aaaa.md");

        bool present = facts.RootElement.GetProperty("checks").EnumerateArray()
            .Any(c => c.GetProperty("id").GetString() == "invariant-form");

        Assert.IsFalse(present, "the form rule is about a charter's shape and nothing else");
    }

    [TestMethod]
    public void TheFormRuleReportsEachInvariantSoARoleCanReadIt()
    {
        // The `form` role asks the same question in prose. Emitting the per-invariant answer
        // means the role reads a fact instead of re-deriving it — and INV-03 wants the two
        // bound rather than merely similar.
        using JsonDocument facts = Gather(".sdlc/malformed/CHARTER.md");

        JsonElement rows = facts.RootElement.GetProperty("charter").GetProperty("invariant-form");
        Assert.AreEqual(2, rows.GetArrayLength());

        JsonElement first = rows[0];
        Assert.AreEqual("INV-01", first.GetProperty("id").GetString());
        Assert.IsFalse(first.GetProperty("has-forbids").GetBoolean());
        Assert.IsTrue(first.GetProperty("has-applies-to").GetBoolean());

        JsonElement second = rows[1];
        Assert.AreEqual("INV-02", second.GetProperty("id").GetString());
        Assert.IsTrue(second.GetProperty("has-forbids").GetBoolean());
        Assert.IsFalse(second.GetProperty("has-applies-to").GetBoolean(),
            "an Applies-to: label with nothing after it governs nothing");
    }

    [TestMethod]
    public void TheScriptShips()
    {
        Assert.IsTrue(File.Exists(ScriptPath), $"not found: {ScriptPath}");
    }

    [TestMethod]
    public void ItEmitsParseableJsonAndPinsTheCommit()
    {
        using JsonDocument facts = Gather(".sdlc/CHARTER.md");

        string sha = facts.RootElement.GetProperty("generated-at-sha").GetString()!;
        Assert.IsFalse(string.IsNullOrWhiteSpace(sha), "facts must pin the commit they were gathered at");
    }

    [TestMethod]
    public void ItEmitsValidJsonForAnArtifactThatIsNotPureAscii()
    {
        // THE REGRESSION. Windows PowerShell 5.1 reads a BOM-less file as ANSI and encodes
        // stdout using the console codepage, and both halves broke at once:
        //
        //   reading — an em-dash's UTF-8 bytes decoded under CP1252 to three characters
        //   writing — the last of those, U+201D, best-fit to a plain double quote, which
        //             TERMINATED the JSON string and made the whole document unparseable
        //
        // The caller is a workflow that runs JSON.parse on this, and its failure path is to
        // continue WITHOUT mechanical findings. So the symptom was never an error: it was an
        // interrogation quietly losing the mechanical findings that are most of its yield.
        using JsonDocument facts = Gather(".sdlc/intents/20260924-ffff.md");

        string origin = facts.RootElement
            .GetProperty("artifact").GetProperty("frontmatter")
            .GetProperty("origin").GetString()!;

        Assert.AreEqual("hand-written — an em-dash in a frontmatter value", origin,
            "the em-dash must survive the round trip through the gatherer intact");
        Assert.IsFalse(origin.Contains('�', StringComparison.Ordinal),
            "a replacement character means the file was decoded as the wrong codepage");
        Assert.IsFalse(origin.Contains('"', StringComparison.Ordinal),
            "a double quote here is the best-fit substitution that broke the JSON");
    }

    [TestMethod]
    public void ItReadsCharterVersionFromFrontmatter()
    {
        using JsonDocument facts = Gather(".sdlc/CHARTER.md");

        Assert.AreEqual(3, facts.RootElement.GetProperty("charter").GetProperty("version").GetInt32());
    }

    [TestMethod]
    public void AStruckThroughIdIsARecordOfAbsenceNotADeclaration()
    {
        using JsonDocument facts = Gather(".sdlc/CHARTER.md");

        string[] declared = [.. facts.RootElement.GetProperty("charter").GetProperty("dec-ids")
            .EnumerateArray().Select(e => e.GetString()!)];

        CollectionAssert.Contains(declared, "DEC-01");
        CollectionAssert.DoesNotContain(declared, "DEC-02",
            "a struck-through register row records a removed decision and must not read as declared");
    }

    [TestMethod]
    public void UnresolvedIdsAndPathsFailTheirChecks()
    {
        using JsonDocument facts = Gather(".sdlc/intents/20260924-aaaa.md");
        string[] failed = FailedChecks(facts);

        CollectionAssert.Contains(failed, "invariant-ids-resolve", "INV-09 is not declared");
        CollectionAssert.Contains(failed, "dec-ids-resolve", "DEC-07 is not in the register");
        CollectionAssert.Contains(failed, "paths-resolve", "src/does/not/exist.cs is absent");
    }

    [TestMethod]
    public void ArtifactKindNamesAndCommandsAreNotTreatedAsPaths()
    {
        using JsonDocument facts = Gather(".sdlc/intents/20260924-aaaa.md");

        string[] paths = [.. facts.RootElement.GetProperty("references").GetProperty("paths")
            .EnumerateArray().Select(e => e.GetProperty("path").GetString()!)];

        CollectionAssert.DoesNotContain(paths, "verify.md",
            "an artifact kind name is not a path reference");
        CollectionAssert.DoesNotContain(paths, "/sdlc:init",
            "a command is not a path reference");
        CollectionAssert.Contains(paths, "src/does/not/exist.cs");
    }

    [TestMethod]
    public void ProseGetsFactsButNoVerdicts()
    {
        using JsonDocument facts = Gather("notes.md");

        Assert.IsFalse(facts.RootElement.GetProperty("artifact").GetProperty("governed").GetBoolean());
        Assert.AreEqual(0, facts.RootElement.GetProperty("checks").GetArrayLength(),
            "a design document describes directories that correctly do not exist; verdicts there are noise");
        Assert.IsTrue(
            facts.RootElement.GetProperty("references").GetProperty("paths").GetArrayLength() > 0,
            "facts are still reported — only the verdicts are withheld");
    }

    [TestMethod]
    public void ArtifactKindIsInferredFromThePath()
    {
        using JsonDocument charter = Gather(".sdlc/CHARTER.md");
        using JsonDocument spec = Gather(".sdlc/cycles/20260924-bbbb/spec.md");
        using JsonDocument plan = Gather(".sdlc/cycles/20260924-cccc/plan.md");
        using JsonDocument intent = Gather(".sdlc/intents/20260924-aaaa.md");

        Assert.AreEqual("charter", charter.RootElement.GetProperty("artifact").GetProperty("kind").GetString());
        Assert.AreEqual("spec", spec.RootElement.GetProperty("artifact").GetProperty("kind").GetString());
        Assert.AreEqual("plan", plan.RootElement.GetProperty("artifact").GetProperty("kind").GetString());
        Assert.AreEqual("intent", intent.RootElement.GetProperty("artifact").GetProperty("kind").GetString());
    }

    [TestMethod]
    public void AcceptanceCriteriaAreParsedWithTheirVerifiableFlag()
    {
        using JsonDocument facts = Gather(".sdlc/cycles/20260924-bbbb/spec.md");

        JsonElement criteria = facts.RootElement.GetProperty("acceptance-criteria");
        Assert.AreEqual(3, criteria.GetArrayLength());

        JsonElement first = criteria[0];
        Assert.AreEqual("AC-20260924-bbbb-1", first.GetProperty("id").GetString());
        Assert.IsTrue(first.GetProperty("verifiable").GetBoolean());
        Assert.IsFalse(criteria[1].GetProperty("verifiable").GetBoolean(),
            "an unverifiable criterion is a correct answer, not a failure");
    }

    [TestMethod]
    public void ADuplicatedCriterionIdFailsItsCheck()
    {
        using JsonDocument facts = Gather(".sdlc/cycles/20260924-bbbb/spec.md");

        CollectionAssert.Contains(FailedChecks(facts), "acceptance-criteria-unique",
            "two criteria sharing an id make traceability report one as covered when it is not");
    }

    [TestMethod]
    public void APlanDeclaringAnAbsentFileFailsItsCheck()
    {
        using JsonDocument facts = Gather(".sdlc/cycles/20260924-cccc/plan.md");

        JsonElement declared = facts.RootElement.GetProperty("declared-files");
        Assert.AreEqual(2, declared.GetArrayLength());

        CollectionAssert.Contains(FailedChecks(facts), "plan-files-consistent",
            "a plan claiming to modify a file that does not exist was written against a different tree");
        CollectionAssert.DoesNotContain(FailedChecks(facts), "plan-declares-files",
            "the plan does declare files, so that check should pass");
    }

    [TestMethod]
    public void IrreversibleChangesReachTierThree()
    {
        using JsonDocument facts = Gather(".sdlc/cycles/20260924-dddd/plan.md");
        JsonElement radius = facts.RootElement.GetProperty("blast-radius");

        Assert.AreEqual(3, radius.GetProperty("tier").GetInt32(),
            "migrations and auth cannot be unwound by reverting the commit");

        string[] flags = [.. radius.GetProperty("flags").EnumerateArray()
            .Select(e => e.GetProperty("flag").GetString()!)];

        CollectionAssert.Contains(flags, "migrations", "git revert does not un-migrate");
        CollectionAssert.Contains(flags, "auth");
    }

    [TestMethod]
    public void ALeafChangeStaysAtTierOne()
    {
        using JsonDocument facts = Gather(".sdlc/cycles/20260924-eeee/plan.md");
        JsonElement radius = facts.RootElement.GetProperty("blast-radius");

        Assert.AreEqual(1, radius.GetProperty("tier").GetInt32(),
            "one leaf file with no flags is the case unattended runs are permitted for");
        Assert.AreEqual(0, radius.GetProperty("flags").GetArrayLength());
    }

    [TestMethod]
    public void OnlyPlansGetABlastRadius()
    {
        // The tier is a function of declared files, and nothing but a plan declares any.
        using JsonDocument spec = Gather(".sdlc/cycles/20260924-bbbb/spec.md");

        Assert.AreEqual(JsonValueKind.Null, spec.RootElement.GetProperty("blast-radius").ValueKind,
            "a spec declares no files, so it has no blast radius to compute");
    }

    [TestMethod]
    public void AGovernedArtifactIsChecked()
    {
        using JsonDocument facts = Gather(".sdlc/CHARTER.md");

        Assert.IsTrue(facts.RootElement.GetProperty("artifact").GetProperty("governed").GetBoolean());
        Assert.IsTrue(facts.RootElement.GetProperty("checks").GetArrayLength() > 0);
    }
}
