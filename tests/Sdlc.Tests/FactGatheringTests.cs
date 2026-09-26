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

    /// <summary>Files N blocking findings as one round's document for the plan fixture.</summary>
    private static void FileRound(string cycleId, int round, int blocking)
    {
        string dir = Path.Combine(_repo!, ".sdlc", "cycles", cycleId, "findings");
        Directory.CreateDirectory(dir);

        IEnumerable<string> items = Enumerable.Range(1, blocking).Select(i =>
            $$"""
            { "id": "plan-r{{round}}-reason{{i}}", "category": "ambiguity", "state": "blocking",
              "stage": "plan", "detection": "reasoning", "summary": "s",
              "cites": [{ "kind": "invariant", "id": "INV-02" }] }
            """);

        File.WriteAllText(Path.Combine(dir, $"plan.r{round}.json"),
            $$"""
            { "cycle-id": "{{cycleId}}", "target": "plan", "charter-version": 3, "round": {{round}},
              "findings": [{{string.Join(",", items)}}] }
            """);
    }

    private static JsonElement ProgressFor(string cycleId)
    {
        using JsonDocument facts = Gather($".sdlc/cycles/{cycleId}/plan.md");
        return facts.RootElement.GetProperty("interrogation").Clone();
    }

    private static string Verdict(JsonElement progress) =>
        progress.GetProperty("progress").GetString()!;

    [TestMethod]
    public void TheROUNDNUMBERCOMESFROMWHATIsAlreadyFiled()
    {
        // NOBODY INCREMENTED THE ROUND. The workflow took one as an input that defaulted to 1, so
        // every interrogation was round 1, every document overwrote the last, and the no-progress
        // rule — the only thing that makes the repair loop terminate — had no state to measure.
        const string cycle = "20260924-cccc";
        string dir = Path.Combine(_repo!, ".sdlc", "cycles", cycle, "findings");

        try
        {
            Assert.AreEqual(1, ProgressFor(cycle).GetProperty("next-round").GetInt32(),
                "with nothing filed the next round is the first");

            FileRound(cycle, 1, 5);
            Assert.AreEqual(2, ProgressFor(cycle).GetProperty("next-round").GetInt32());

            FileRound(cycle, 2, 4);
            Assert.AreEqual(3, ProgressFor(cycle).GetProperty("next-round").GetInt32());
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [TestMethod]
    public void TwoConsecutiveRoundsWithNoReductionHaltTheLoop()
    {
        // The rule, and every state on the way to it. Measured rather than assumed: round 2 found
        // three of five blocking items inside round 1's repairs, round 3 four of five inside
        // round 2's, replicated here at three of four. So "until clean" never fires, and a fixed
        // round cap ships known-broken artifacts wearing a clean label.
        const string cycle = "20260924-cccc";
        string dir = Path.Combine(_repo!, ".sdlc", "cycles", cycle, "findings");

        try
        {
            FileRound(cycle, 1, 5);
            Assert.AreEqual("first-round", Verdict(ProgressFor(cycle)),
                "one round has nothing to compare against");

            FileRound(cycle, 2, 4);
            Assert.AreEqual("reducing", Verdict(ProgressFor(cycle)));

            FileRound(cycle, 3, 4);
            Assert.AreEqual("stalled-once", Verdict(ProgressFor(cycle)),
                "one stalled round is not yet the rule firing");

            // Worse, not merely equal: repairs INTRODUCING findings is the measured behaviour, and
            // it must count as no reduction rather than as something other than a stall.
            FileRound(cycle, 4, 6);
            JsonElement halted = ProgressFor(cycle);
            Assert.AreEqual("halted-no-progress", Verdict(halted));
            StringAssert.Contains(halted.GetProperty("detail").GetString(), "r4=6",
                StringComparison.Ordinal);

            FileRound(cycle, 5, 0);
            Assert.AreEqual("converged", Verdict(ProgressFor(cycle)),
                "a round that finds nothing blocking is the loop finishing, not stalling");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [TestMethod]
    public void AStallThatRecoversIsNotAHalt()
    {
        // CONSECUTIVE is the word doing the work. A loop that stalled, reduced, then stalled again
        // has not stalled twice in a row, and halting it would stop a repair that was working.
        const string cycle = "20260924-cccc";
        string dir = Path.Combine(_repo!, ".sdlc", "cycles", cycle, "findings");

        try
        {
            FileRound(cycle, 1, 5);
            FileRound(cycle, 2, 5);
            FileRound(cycle, 3, 3);
            FileRound(cycle, 4, 3);

            Assert.AreEqual("stalled-once", Verdict(ProgressFor(cycle)),
                "the stall at r2 is not consecutive with the one at r4");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [TestMethod]
    public void TheCycleIdComesFromThePathSoNothingElseHasToDeriveIt()
    {
        // One answer to "which cycle is this". The interrogation workflow derived it in JavaScript
        // while this derived it in PowerShell: two implementations of one path convention, which
        // disagree the first time a convention moves.
        using JsonDocument intent = Gather(".sdlc/intents/20260924-aaaa.md");
        Assert.AreEqual("20260924-aaaa",
            intent.RootElement.GetProperty("artifact").GetProperty("cycle-id").GetString());

        using JsonDocument plan = Gather(".sdlc/cycles/20260924-cccc/plan.md");
        Assert.AreEqual("20260924-cccc",
            plan.RootElement.GetProperty("artifact").GetProperty("cycle-id").GetString());

        // A charter belongs to the charter-review intent proposing to change it, so it has to be
        // told rather than guessed.
        using JsonDocument charter = Gather(".sdlc/CHARTER.md");
        Assert.AreEqual(JsonValueKind.Null,
            charter.RootElement.GetProperty("artifact").GetProperty("cycle-id").ValueKind);
    }

    private static string DetailOf(JsonDocument facts, string checkId) =>
        facts.RootElement.GetProperty("checks").EnumerateArray()
            .First(c => c.GetProperty("id").GetString() == checkId)
            .GetProperty("detail").GetString() ?? string.Empty;

    [TestMethod]
    public void AnArtifactWhoseFrontmatterBreaksItsSchemaFailsACheck()
    {
        // THE SIX SCHEMAS WERE EXERCISED ONLY BY FIXTURES. Nothing validated a real artifact at
        // the moment one was written, so a spec with no charter version or a plan with a
        // malformed cycle id passed every gate there was.
        //
        // This fixture intent declares `id` and `source` and no `charter-version`, which the
        // intent schema requires — a retro stamped against no charter version is uninterpretable,
        // which is exactly why the field is required.
        using JsonDocument facts = Gather(".sdlc/intents/20260924-aaaa.md");

        Assert.IsTrue(FailedChecks(facts).Contains("frontmatter-valid"),
            "an intent missing a required frontmatter field must fail schema validation");

        StringAssert.Contains(DetailOf(facts, "frontmatter-valid"), "charter-version",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void AConformingArtifactPassesSchemaValidation()
    {
        // The other direction, and the one that decides whether the check survives: a check that
        // reports healthy artifacts as broken gets switched off, which is the erosion failure
        // one layer up.
        using JsonDocument facts = Gather(".sdlc/cycles/20260924-bbbb/spec.md");

        Assert.IsFalse(FailedChecks(facts).Contains("frontmatter-valid"),
            "this spec carries every field its schema requires");
    }

    [TestMethod]
    public void AKindWithNoSchemaPassesAndSaysWhy()
    {
        // No charter.schema.json ships — a charter's shape is governed by the form rule instead.
        // That has to read as "nothing governs this" rather than as either a pass or a failure,
        // because a caller told nothing will eventually call it approval.
        using JsonDocument facts = Gather(".sdlc/CHARTER.md");

        Assert.IsFalse(FailedChecks(facts).Contains("frontmatter-valid"),
            "an unvalidatable kind is not a failing artifact");

        StringAssert.Contains(DetailOf(facts, "frontmatter-valid"), "no schema",
            StringComparison.Ordinal);
    }

    // --- INV-08's detection half --------------------------------------------
    //
    // Each case builds its own throwaway repository rather than sharing the class fixture, because
    // every one of them needs a different branch, a different intent source, or a deliberately
    // broken git.

    /// <summary>
    /// A repository with a charter, one intent of the given source, and a spec under that cycle.
    /// Committed on master unless <paramref name="commit"/> says otherwise.
    /// </summary>
    private static string MakeProtectedRepo(string intentId, string source, bool commit = true)
    {
        string root = Path.Combine(Path.GetTempPath(), "sdlc-pp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, ".sdlc", "intents"));
        Directory.CreateDirectory(Path.Combine(root, ".sdlc", "cycles", intentId));

        File.WriteAllText(Path.Combine(root, ".sdlc", "CHARTER.md"),
            "---\nversion: 1\nsolution: fixture\n---\n\n# Charter\n");

        File.WriteAllText(Path.Combine(root, ".sdlc", "intents", intentId + ".md"),
            $"---\nid: {intentId}\nsource: {source}\ncharter-version: 1\n---\n\n# Intent\n");

        File.WriteAllText(Path.Combine(root, ".sdlc", "cycles", intentId, "spec.md"),
            $"---\ncycle-id: {intentId}\nintent: {intentId}\ncharter-version: 1\n---\n\n# Spec\n");

        GitIn(root, "init --initial-branch=master");
        if (commit)
        {
            GitIn(root, "add -A");
            GitIn(root, "-c user.email=t@example.invalid -c user.name=Test commit -m fixture");
        }

        return root;
    }

    private static void GitIn(string root, string args)
    {
        using Process p = Process.Start(new ProcessStartInfo("git", args)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;

        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
    }

    private static JsonDocument GatherIn(string root, string artifactRelativePath)
    {
        string json = Run(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" -Artifact \"{artifactRelativePath}\"",
            root);

        return JsonDocument.Parse(json);
    }

    private static void Discard(string dir)
    {
        try
        {
            foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }

            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-10")]
    public void AnUnlicensedProtectedChangeFailsTheCheck()
    {
        // The whole point of the detection half. The gate prevents a write it can SEE; this reports
        // one that already happened - a person in an editor, a tool the matcher does not cover, a
        // hook switched off.
        string root = MakeProtectedRepo("20260926-a001", "authored");

        try
        {
            GitIn(root, "checkout -b cycle-20260926-a001");
            File.AppendAllText(Path.Combine(root, ".sdlc", "CHARTER.md"), "\nan unlicensed amendment\n");

            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-a001/spec.md");

            Assert.IsTrue(FailedChecks(facts).Contains("protected-paths-licensed"),
                "a charter changed on a branch whose intent is `authored` is exactly INV-08's case");

            JsonElement pp = facts.RootElement.GetProperty("protected-paths");
            Assert.AreEqual(1, pp.GetProperty("unlicensed").GetArrayLength());
            Assert.AreEqual(".sdlc/CHARTER.md",
                pp.GetProperty("unlicensed")[0].GetProperty("path").GetString());
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-10")]
    public void ALicensedProtectedChangeDoesNotFailTheCheck()
    {
        // The other direction, and the one that decides whether the check is usable: a charter
        // amendment on its own charter-review branch is the PERMITTED case, and a check that fired
        // on it would make every amendment unlandable.
        string root = MakeProtectedRepo("20260926-c002", "charter-review");

        try
        {
            GitIn(root, "checkout -b intent-20260926-c002");
            File.AppendAllText(Path.Combine(root, ".sdlc", "CHARTER.md"), "\na licensed amendment\n");

            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-c002/spec.md");

            Assert.IsFalse(FailedChecks(facts).Contains("protected-paths-licensed"),
                "an amendment on its own charter-review branch is what INV-08 permits");
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-11")]
    public void TheCheckSkipsWithAReasonWhenNoBaselineResolves()
    {
        // A repository with no commit, which is where `init` runs. Nothing to compare against, so
        // no comparison was owed - and reporting a failure would make an ungoverned repository look
        // like a violated one.
        string root = MakeProtectedRepo("20260926-a003", "authored", commit: false);

        try
        {
            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-a003/spec.md");

            Assert.IsFalse(FailedChecks(facts).Contains("protected-paths-licensed"),
                "a skip is not a failure");

            JsonElement pp = facts.RootElement.GetProperty("protected-paths");
            Assert.IsTrue(pp.GetProperty("skipped").GetBoolean());
            Assert.IsFalse(string.IsNullOrWhiteSpace(pp.GetProperty("reason").GetString()),
                "a skip without a reason is indistinguishable from a pass to whoever reads it");
            StringAssert.Contains(DetailOf(facts, "protected-paths-licensed"), "skipped",
                StringComparison.Ordinal);
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-12")]
    public void TheCheckFailsWhenItCannotRunAndSaysSoDistinctlyFromSkipping()
    {
        // COULD-NOT-RUN IS NOT A SKIP. A baseline resolved, so a comparison WAS owed - and a git
        // that cannot make it must not read as a clean charter. Provoked by breaking the index,
        // which leaves `rev-parse` working and `diff` failing, so the two answers are genuinely
        // reached by different conditions rather than by a flag a test set.
        string root = MakeProtectedRepo("20260926-a004", "authored");

        try
        {
            string index = Path.Combine(root, ".git", "index");
            File.Delete(index);
            Directory.CreateDirectory(index);

            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-a004/spec.md");

            Assert.IsTrue(FailedChecks(facts).Contains("protected-paths-licensed"),
                "a comparison that was owed and could not be made is a failure");

            JsonElement pp = facts.RootElement.GetProperty("protected-paths");
            Assert.IsTrue(pp.GetProperty("could-not-run").GetBoolean());
            Assert.IsFalse(pp.GetProperty("skipped").GetBoolean(),
                "could-not-run and skipped are different answers and must not collapse");
            StringAssert.Contains(DetailOf(facts, "protected-paths-licensed"), "could not run",
                StringComparison.Ordinal);
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-13")]
    public void AProtectedPathCommittedDirectlyToMasterIsReportedAgainstARemoteBaseline()
    {
        // The case a local-only baseline cannot see. On master the branch carries no intent at all,
        // so a charter commit there is unlicensed by construction - but only a remote-tracking
        // baseline reveals it, because comparing master against itself finds nothing.
        string root = MakeProtectedRepo("20260926-a005", "authored");
        string remote = root + "-remote";

        try
        {
            GitIn(root, $"init --bare \"{remote}\"");
            GitIn(root, $"remote add origin \"{remote}\"");
            GitIn(root, "push -u origin master");

            File.AppendAllText(Path.Combine(root, ".sdlc", "CHARTER.md"), "\nstraight to master\n");
            GitIn(root, "add -A");
            GitIn(root, "-c user.email=t@example.invalid -c user.name=Test commit -m sneaky");

            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-a005/spec.md");

            JsonElement pp = facts.RootElement.GetProperty("protected-paths");
            Assert.AreEqual("remote-tracking", pp.GetProperty("baseline-kind").GetString(),
                "the baseline actually used decides what the result means, so it is reported");
            Assert.IsTrue(FailedChecks(facts).Contains("protected-paths-licensed"),
                "a charter committed directly to master carries no intent and no licence");
        }
        finally
        {
            Discard(root);
            Discard(remote);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-27")]
    public void AnAmendmentAlreadyInTheBaselineDoesNotFailTheCheck()
    {
        // Once an amendment is merged and pushed the baseline CONTAINS it, so the difference
        // disappears and the check goes quiet. Without this the first landed amendment would leave
        // every later cycle permanently blocked.
        string root = MakeProtectedRepo("20260926-c006", "charter-review");
        string remote = root + "-remote";

        try
        {
            File.AppendAllText(Path.Combine(root, ".sdlc", "CHARTER.md"), "\na landed amendment\n");
            GitIn(root, "add -A");
            GitIn(root, "-c user.email=t@example.invalid -c user.name=Test commit -m amend");

            GitIn(root, $"init --bare \"{remote}\"");
            GitIn(root, $"remote add origin \"{remote}\"");
            GitIn(root, "push -u origin master");

            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-c006/spec.md");

            Assert.IsFalse(FailedChecks(facts).Contains("protected-paths-licensed"),
                "an amendment present in the baseline is not a difference from it");
        }
        finally
        {
            Discard(root);
            Discard(remote);
        }
    }

    [TestMethod]
    public void ARepositoryWithNoProtectedPathsPassesRatherThanSkipping()
    {
        // There is nothing to compare and nothing to violate. Skipping would imply a check was owed
        // and not made, which is a different claim about the same repository.
        string root = MakeProtectedRepo("20260926-a007", "authored");

        try
        {
            File.Delete(Path.Combine(root, ".sdlc", "CHARTER.md"));
            GitIn(root, "add -A");
            GitIn(root, "-c user.email=t@example.invalid -c user.name=Test commit -m ungoverned");

            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-a007/spec.md");

            JsonElement pp = facts.RootElement.GetProperty("protected-paths");
            Assert.IsFalse(pp.GetProperty("skipped").GetBoolean(),
                "no protected paths is an answer, not an absence of one");
            Assert.IsFalse(FailedChecks(facts).Contains("protected-paths-licensed"));
        }
        finally
        {
            Discard(root);
        }
    }

    // --- the two scanners, in both directions -------------------------------
    //
    // BOTH DIRECTIONS FOR EACH, because a scanner that reports nothing passes the false-positive
    // half and is useless. These two checks blocked verification for every cycle in this
    // repository, and the fix that silences them must not silence them everywhere.

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-17")]
    [TestProperty("AC", "AC-20260926-46a0-18")]
    public void TheSuppressionScanIgnoresASpellingInsideAStringAndStillReportsARealOne()
    {
        string root = MakeProtectedRepo("20260926-a008", "authored");

        try
        {
            // The false positive's exact shape: an assertion ABOUT suppressions, which therefore
            // names one inside a quoted string on a code line with no comment beside it.
            File.WriteAllText(Path.Combine(root, "Asserts.cs"), """
                public static class Asserts
                {
                    public static bool Check(string text) =>
                        text.Contains("#pragma warning disable", System.StringComparison.Ordinal)
                        || text.Contains("<NoWarn>", System.StringComparison.Ordinal);
                }
                """);

            // And a genuine one, in code, with nothing justifying it.
            File.WriteAllText(Path.Combine(root, "Real.cs"), """
                public static class Real
                {
                #pragma warning disable CS1591
                    public static int Value => 1;
                }
                """);

            GitIn(root, "add -A");
            GitIn(root, "-c user.email=t@example.invalid -c user.name=Test commit -m scanners");

            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-a008/spec.md");

            string[] unjustified = [.. facts.RootElement
                .GetProperty("traceability").GetProperty("unjustified-suppressions")
                .EnumerateArray().Select(e => e.GetString()!)];

            Assert.IsFalse(unjustified.Any(u => u.StartsWith("Asserts.cs", StringComparison.Ordinal)),
                "a suppression spelling inside a quoted string is an assertion about suppressions, "
                + "not a suppression: " + string.Join(", ", unjustified));

            Assert.IsTrue(unjustified.Any(u => u.StartsWith("Real.cs", StringComparison.Ordinal)),
                "a scanner that reports nothing is useless: " + string.Join(", ", unjustified));
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-19")]
    [TestProperty("AC", "AC-20260926-46a0-20")]
    public void TheAnnotationScanIgnoresAnAnnotationInARawStringAndStillReadsARealOne()
    {
        string root = MakeProtectedRepo("20260926-a009", "authored");

        try
        {
            // A real annotation, and one that is the SOURCE TEXT of a generated fixture project.
            // Read literally the second is traceability this repository never declared.
            string quote = "\"\"\"";
            File.WriteAllText(Path.Combine(root, "Tests.cs"),
                "public sealed class Tests\n"
                + "{\n"
                + "    [TestProperty(\"AC\", \"AC-20260926-a009-1\")]\n"
                + "    public void Real() { }\n"
                + "\n"
                + "    public string Fixture => " + quote + "\n"
                + "        [TestProperty(\"AC\", \"AC-generated-fixture-42\")]\n"
                + "        public void Inside() { }\n"
                + "        " + quote + ";\n"
                + "}\n");

            GitIn(root, "add -A");
            GitIn(root, "-c user.email=t@example.invalid -c user.name=Test commit -m annotations");

            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-a009/spec.md");

            string[] annotated = [.. facts.RootElement.GetProperty("test-annotations")
                .EnumerateArray().Select(e => e.GetProperty("criterion").GetString()!)];

            Assert.IsFalse(annotated.Contains("AC-generated-fixture-42"),
                "an annotation inside a raw string literal is fixture source, not traceability: "
                + string.Join(", ", annotated));

            Assert.IsTrue(annotated.Contains("AC-20260926-a009-1"),
                "the real annotation must survive the fix: " + string.Join(", ", annotated));
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-11")]
    public void TheBaselineDegradesToLocalMasterWhenThereIsNoRemote()
    {
        // The spec declares three baselines and the criteria named only the first. Which comparison
        // was actually made changes what the result MEANS, so each fallback has to be reachable and
        // reported - an untested fallback is a silent change of subject.
        string root = MakeProtectedRepo("20260926-a010", "authored");

        try
        {
            GitIn(root, "checkout -b cycle-20260926-a010");

            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-a010/spec.md");
            JsonElement pp = facts.RootElement.GetProperty("protected-paths");

            Assert.AreEqual("local-master", pp.GetProperty("baseline-kind").GetString(),
                "with no remote and HEAD off master, local master is the merge target");
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-11")]
    public void TheBaselineDegradesToHeadOnMasterWithNoRemote()
    {
        // The last fallback. Against HEAD the check sees only an UNCOMMITTED change, which is a
        // weaker claim than the other two make - hence reporting the kind rather than only the
        // result.
        string root = MakeProtectedRepo("20260926-a011", "authored");

        try
        {
            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-a011/spec.md");
            JsonElement pp = facts.RootElement.GetProperty("protected-paths");

            Assert.AreEqual("head", pp.GetProperty("baseline-kind").GetString(),
                "on master with no remote there is nothing to compare against but HEAD");
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-8")]
    public void TheDetectionHalfDoesNotReportAProtectedPathThatIsMerelyBeingCreated()
    {
        // Creation needs no licence in BOTH mechanisms, and only the gate half had a criterion for
        // it. The two must agree: a gate that permits a first charter while the check reports it as
        // an unlicensed change would block the very cycle the gate just allowed.
        string root = MakeProtectedRepo("20260926-a012", "authored");

        try
        {
            // A pack file that exists on disk and in no commit - created, not amended.
            Directory.CreateDirectory(Path.Combine(root, "packs", "fresh"));
            File.WriteAllText(Path.Combine(root, "packs", "fresh", "pack.yaml"), "name: fresh\n");

            GitIn(root, "checkout -b cycle-20260926-a012");

            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-a012/spec.md");

            Assert.IsFalse(FailedChecks(facts).Contains("protected-paths-licensed"),
                "a protected path being created is not a protected path being loosened");
        }
        finally
        {
            Discard(root);
        }
    }

    // --- an annotation is judged against its own cycle's spec ---------------
    //
    // `orphan-annotations` compared every annotation in the suite against ONE spec's criteria. That
    // was correct while the suite held one cycle's annotations and blocked every verification from
    // the moment a second cycle annotated a test. It did exactly that, which is how it was found.

    /// <summary>
    /// A repository with two cycles, each declaring one criterion, and a test file annotated with
    /// whichever ids the caller asks for.
    /// </summary>
    private static string MakeTwoCycleRepo(string[] annotatedIds, bool writeSecondSpec = true)
    {
        string root = Path.Combine(Path.GetTempPath(), "sdlc-orph-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, ".sdlc", "intents"));

        static string Spec(string id) => $"""
            ---
            cycle-id: {id}
            intent: {id}
            charter-version: 1
            ---

            # Spec {id}

            | ID | Verifiable | Criterion |
            |---|---|---|
            | AC-{id}-1 | yes | the one criterion this cycle declares |
            """;

        foreach (string id in new[] { "20260926-d001", "20260926-d002" })
        {
            File.WriteAllText(Path.Combine(root, ".sdlc", "intents", id + ".md"),
                $"---\nid: {id}\nsource: authored\ncharter-version: 1\n---\n\n# Intent\n");
        }

        Directory.CreateDirectory(Path.Combine(root, ".sdlc", "cycles", "20260926-d001"));
        File.WriteAllText(Path.Combine(root, ".sdlc", "cycles", "20260926-d001", "spec.md"),
            Spec("20260926-d001"));

        Directory.CreateDirectory(Path.Combine(root, ".sdlc", "cycles", "20260926-d002"));
        if (writeSecondSpec)
        {
            File.WriteAllText(Path.Combine(root, ".sdlc", "cycles", "20260926-d002", "spec.md"),
                Spec("20260926-d002"));
        }
        else
        {
            // A cycle directory with no spec: the annotation names criteria of a document that does
            // not exist, which is the renumbering case the check exists to catch.
            File.WriteAllText(Path.Combine(root, ".sdlc", "cycles", "20260926-d002", "notes.md"), "# no spec\n");
        }

        string annotations = string.Join("\n", annotatedIds.Select(id =>
            $"    [TestProperty(\"AC\", \"{id}\")]\n    public void T_{id.Replace('-', '_')}() {{ }}\n"));

        File.WriteAllText(Path.Combine(root, "Annotated.cs"),
            "public sealed class Annotated\n{\n" + annotations + "}\n");

        GitIn(root, "init --initial-branch=master");
        GitIn(root, "add -A");
        GitIn(root, "-c user.email=t@example.invalid -c user.name=Test commit -m fixture");

        return root;
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-26a1-14")]
    public void AnAnnotationNamingAnotherCyclesCriterionIsNotAnOrphan()
    {
        // The blocker. Gathering for d001 must not report d002's annotation as an orphan just because
        // d001's spec does not declare it - d002's spec does, and the id says so.
        string root = MakeTwoCycleRepo(["AC-20260926-d001-1", "AC-20260926-d002-1"]);

        try
        {
            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-d001/spec.md");

            string[] orphans = [.. facts.RootElement.GetProperty("traceability")
                .GetProperty("orphan-annotations").EnumerateArray().Select(e => e.GetString()!)];

            Assert.AreEqual(0, orphans.Length,
                "each annotation resolves against its own cycle's spec: " + string.Join(", ", orphans));

            Assert.IsFalse(FailedChecks(facts).Contains("annotations-resolve"),
                "a second cycle's annotations must not block the first cycle's verification");
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-26a1-15")]
    public void AnAnnotationNamingACycleWithNoSpecIsStillAnOrphan()
    {
        // THE CHECK'S PURPOSE, PRESERVED. Loosening it so that nothing is ever an orphan would pass
        // the blocker test above and remove the only thing that catches a renumbering.
        string root = MakeTwoCycleRepo(["AC-20260926-d001-1", "AC-20260926-d002-1"], writeSecondSpec: false);

        try
        {
            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-d001/spec.md");

            string[] orphans = [.. facts.RootElement.GetProperty("traceability")
                .GetProperty("orphan-annotations").EnumerateArray().Select(e => e.GetString()!)];

            CollectionAssert.Contains(orphans, "AC-20260926-d002-1",
                "an id naming a cycle with no spec resolves to nothing and is an orphan");
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-26a1-15")]
    public void AnAnnotationNamingAnUndeclaredCriterionOfItsOwnCycleIsStillAnOrphan()
    {
        // The other half of the same purpose: the cycle is right and the number is wrong.
        string root = MakeTwoCycleRepo(["AC-20260926-d001-1", "AC-20260926-d001-99"]);

        try
        {
            using JsonDocument facts = GatherIn(root, ".sdlc/cycles/20260926-d001/spec.md");

            string[] orphans = [.. facts.RootElement.GetProperty("traceability")
                .GetProperty("orphan-annotations").EnumerateArray().Select(e => e.GetString()!)];

            CollectionAssert.Contains(orphans, "AC-20260926-d001-99",
                "a criterion its own spec does not declare is the typo case and must stay caught");
        }
        finally
        {
            Discard(root);
        }
    }
}
