using System.Diagnostics;
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
    public void AGovernedArtifactIsChecked()
    {
        using JsonDocument facts = Gather(".sdlc/CHARTER.md");

        Assert.IsTrue(facts.RootElement.GetProperty("artifact").GetProperty("governed").GetBoolean());
        Assert.IsTrue(facts.RootElement.GetProperty("checks").GetArrayLength() > 0);
    }
}
