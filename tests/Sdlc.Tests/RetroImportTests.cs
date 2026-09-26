using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// Absorbing retros into the corpus, and the refusal the charter's exemption rests on.
/// </summary>
/// <remarks>
/// INV-03 forbids a second representation of a fact without, in the same change, the check that
/// fails when the two disagree. The retro corpus is a copy of every solution's retros sitting
/// beside the originals, and its exemption is narrow and conditional: a copy of an IMMUTABLE
/// artifact is not a second list, because divergence is impossible — <c>provided the mechanism that
/// makes the copy refuses to overwrite with different content</c>.
/// <para>
/// No such mechanism existed, and the charter says so about itself: "None is built yet, so the
/// retro corpus does not currently qualify." The skill described the refusal in prose, which is
/// something a model is asked to follow rather than something it is prevented from breaking. So
/// the test that matters here is not that a copy lands — it is that a DIFFERING copy does not.
/// </para>
/// </remarks>
[TestClass]
public sealed class RetroImportTests
{
    private static string ToolDir => Path.Combine(AppContext.BaseDirectory, "tools");

    private static string? _corpusRepo;
    private static string? _solution;

    [ClassInitialize]
    public static void CreateFixtures(TestContext context)
    {
        // Two repositories, because that is the real shape: a retro is written in the solution that
        // landed it and copied into ai-sdlc, and the fields exist precisely because git metadata
        // does not survive that copy.
        _corpusRepo = Path.Combine(Path.GetTempPath(), "sdlc-corpus-" + Guid.NewGuid().ToString("N")[..8]);
        _solution = Path.Combine(Path.GetTempPath(), "sdlc-solution-" + Guid.NewGuid().ToString("N")[..8]);

        Directory.CreateDirectory(_corpusRepo);
        Directory.CreateDirectory(Path.Combine(_solution, ".sdlc", "cycles", "20260925-ee01"));

        Run("git", "init --initial-branch=master", _corpusRepo);
        Run("git", "init --initial-branch=master", _solution);
    }

    [ClassCleanup]
    public static void RemoveFixtures()
    {
        foreach (string? dir in new[] { _corpusRepo, _solution })
        {
            if (dir is null || !Directory.Exists(dir))
            {
                continue;
            }

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
    }

    [TestInitialize]
    public void ClearCorpus()
    {
        string corpus = Path.Combine(_corpusRepo!, "retros");
        if (Directory.Exists(corpus))
        {
            Directory.Delete(corpus, recursive: true);
        }
    }

    private static string Run(string exe, string args, string workingDirectory)
    {
        using Process p = Process.Start(new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return stdout;
    }

    private static void WriteRetro(string cycleId, string body, string solutionName = "MofonCode.Core")
    {
        string dir = Path.Combine(_solution!, ".sdlc", "cycles", cycleId);
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "retro.md"),
            $"""
            ---
            cycle-id: {cycleId}
            solution: {solutionName}
            landed-at: 2026-09-25
            charter-version: 3
            ---

            # Retro — {cycleId}

            {body}
            """);
    }

    private sealed record Report(string Outcome, int Imported, int Unchanged, int Refused, string[] Details, int ExitCode);

    private static Report Import(string? path = null)
    {
        string script = Path.Combine(ToolDir, "Import-SdlcRetro.ps1");

        using Process p = Process.Start(new ProcessStartInfo("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" " +
            $"-Path \"{path ?? _solution}\" -RepoRoot \"{_corpusRepo}\"")
        {
            WorkingDirectory = _corpusRepo!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();

        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout), "the import script printed nothing");

        using JsonDocument doc = JsonDocument.Parse(stdout);
        JsonElement root = doc.RootElement;

        return new Report(
            root.GetProperty("outcome").GetString()!,
            root.GetProperty("imported").GetInt32(),
            root.GetProperty("unchanged").GetInt32(),
            root.GetProperty("refused").GetInt32(),
            [.. root.GetProperty("entries").EnumerateArray()
                .Select(e => e.GetProperty("detail").GetString() ?? string.Empty)],
            p.ExitCode);
    }

    private static string CorpusEntry(string cycleId, string solutionName = "MofonCode.Core") =>
        Path.Combine(_corpusRepo!, "retros", solutionName, cycleId + ".md");

    [TestMethod]
    public void ARetroIsFiledUnderItsSolutionAndCycleId()
    {
        WriteRetro("20260925-ee01", "Nothing notable.");

        Report r = Import();

        Assert.AreEqual("imported", r.Outcome, string.Join(" | ", r.Details));
        Assert.AreEqual(1, r.Imported);
        Assert.AreEqual(0, r.ExitCode);

        // Partitioned by solution, because aggregation groups rather than pools: "where does our
        // process leak" may have a different answer for a library than for a competition.
        Assert.IsTrue(File.Exists(CorpusEntry("20260925-ee01")));
    }

    [TestMethod]
    public void ImportingTwiceChangesNothing()
    {
        WriteRetro("20260925-ee01", "Nothing notable.");
        Assert.AreEqual("imported", Import().Outcome);

        Report again = Import();

        // Idempotent by id, with no ledger: a "retros processed" list would be a second list beside
        // a directory that is derivable by listing it.
        Assert.AreEqual(1, again.Unchanged);
        Assert.AreEqual(0, again.Imported);
        Assert.AreEqual(0, again.ExitCode, "an unchanged corpus is a successful run");
    }

    [TestMethod]
    public void ARETROTHATCHANGEDAFTERLANDINGISREFUSED()
    {
        // THE ONE THAT MATTERS. Without this refusal the corpus is a plain second list and INV-03's
        // exemption does not hold, so the charter would be wrong about its own compliance.
        WriteRetro("20260925-ee01", "As landed.");
        Assert.AreEqual("imported", Import().Outcome);

        string before = File.ReadAllText(CorpusEntry("20260925-ee01"));

        WriteRetro("20260925-ee01", "Edited after landing, which must never win.");
        Report r = Import();

        Assert.AreEqual("refused", r.Outcome);
        Assert.AreEqual(1, r.Refused);
        Assert.AreNotEqual(0, r.ExitCode, "a check that only prints gets skimmed");
        Assert.AreEqual(before, File.ReadAllText(CorpusEntry("20260925-ee01")),
            "the corpus entry must be untouched");

        Assert.IsTrue(r.Details.Any(d => d.Contains("DIFFERENT content", StringComparison.Ordinal)),
            "the refusal must say what it found: " + string.Join(" | ", r.Details));
    }

    [TestMethod]
    public void ARetroMissingAFieldThatCannotSurviveTheCopyIsRefused()
    {
        // `landed-at` is not derivable once the retro is here: this repository's git date is the
        // ABSORPTION date, and the landing date lives in a history it does not have. So a retro
        // without it is refused at the door rather than filed and puzzled over later.
        string dir = Path.Combine(_solution!, ".sdlc", "cycles", "20260925-ee02");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "retro.md"),
            """
            ---
            cycle-id: 20260925-ee02
            solution: MofonCode.Core
            charter-version: 3
            ---

            # A retro with no landing date
            """);

        try
        {
            Report r = Import(dir);

            Assert.AreEqual("refused", r.Outcome);
            Assert.AreNotEqual(0, r.ExitCode);
            Assert.IsFalse(File.Exists(CorpusEntry("20260925-ee02")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void PointingAtASolutionFindsEveryRetroUnderIt()
    {
        // The directory form is a loop over the single-file case — one piece of logic, no
        // reconciliation, nothing stateful.
        WriteRetro("20260925-ee01", "One.");
        WriteRetro("20260925-ee03", "Two.");

        try
        {
            Report r = Import();

            Assert.AreEqual(2, r.Imported, string.Join(" | ", r.Details));
            Assert.IsTrue(File.Exists(CorpusEntry("20260925-ee01")));
            Assert.IsTrue(File.Exists(CorpusEntry("20260925-ee03")));
        }
        finally
        {
            Directory.Delete(Path.Combine(_solution!, ".sdlc", "cycles", "20260925-ee03"), recursive: true);
        }
    }

    [TestMethod]
    public void NothingToImportIsNotAFailure()
    {
        string empty = Path.Combine(_corpusRepo!, "empty");
        Directory.CreateDirectory(empty);

        Report r = Import(empty);

        Assert.AreEqual("nothing-to-import", r.Outcome);
        Assert.AreEqual(0, r.ExitCode, "a solution with no landed cycles has no retros");
    }
}
