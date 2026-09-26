using System.Diagnostics;
using System.Text;

namespace Sdlc.Tests;

/// <summary>
/// The findings renderer, which produces both markdown outputs the design calls for.
/// </summary>
/// <remarks>
/// One renderer, two audiences, deliberately: the committed suggestions file and the
/// regenerable full view. Two templates over one schema would be the INV-03 duplication
/// this framework exists to catch, so the interesting tests here are the ones proving the
/// two outputs really are the same renderer with a filter.
/// <para>
/// The JSON stays authoritative. Nothing in this framework parses the markdown, which is
/// why these tests check what a person needs — that no finding is silently dropped and no
/// summary is silently truncated — rather than an exact layout.
/// </para>
/// </remarks>
[TestClass]
public sealed class FindingsRenderTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Format-SdlcFindings.ps1");

    private static string? _repo;

    [ClassInitialize]
    public static void CreateFixtureRepo(TestContext context)
    {
        _repo = Path.Combine(Path.GetTempPath(), "sdlc-render-" + Guid.NewGuid().ToString("N")[..8]);
        // ONE DOCUMENT PER INTERROGATION, under the cycle's findings directory. The findings
        // schema carries a single target and round per document, which is what one interrogation
        // is; a cycle accumulates one per stage and one per repair round.
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "cycles", "20260925-aa01", "findings"));

        // A pipe inside a summary is the case worth fixturing. Markdown would read it as a
        // cell boundary and everything after it would land in the wrong column or vanish,
        // which loses the half of the sentence that says what is actually wrong.
        File.WriteAllText(
            Path.Combine(_repo, ".sdlc", "cycles", "20260925-aa01", "findings", "spec.r1.json"),
            """
            {
              "cycle-id": "20260925-aa01",
              "target": "spec",
              "charter-version": 2,
              "round": 1,
              "findings": [
                { "id": "m1", "category": "gate-bypass", "state": "blocking", "stage": "build",
                  "detection": "check", "role": "facts",
                  "summary": "Check `suppressions-justified` failed.",
                  "cites": [{ "kind": "check", "id": "suppressions-justified" }],
                  "repair": "Write a justification next to the suppression." },
                { "id": "r1", "category": "ambiguity", "state": "suggested", "stage": "spec",
                  "detection": "reasoning", "role": "coherence",
                  "summary": "Reads two ways: a | b, and the second half must survive." },
                { "id": "r2", "category": "missing-case", "state": "suggested", "stage": "spec",
                  "detection": "reasoning", "role": "gaps",
                  "summary": "No failure mode for the timeout path." }
              ]
            }
            """);

        Git("init --initial-branch=master");
        Git("add -A");
        Git("-c user.email=t@example.invalid -c user.name=Test commit -m fixture");
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

    private static string Render(string extra = "")
    {
        return Run(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" " +
            $"-CycleId 20260925-aa01 {extra}");
    }

    [TestMethod]
    public void TheFullViewRendersEveryFinding()
    {
        string md = Render();

        foreach (string id in new[] { "m1", "r1", "r2" })
        {
            Assert.IsTrue(md.Contains("`" + id + "`", StringComparison.Ordinal),
                $"finding {id} is missing from the rendered view");
        }
    }

    [TestMethod]
    public void TheSuggestionsFileCarriesOnlySuggestedFindings()
    {
        // The whole point of the filter: a blocking finding was resolved before landing, so
        // it belongs in verify.md and the retro, not in the intake pile.
        string md = Render("-State suggested");

        Assert.IsTrue(md.Contains("`r1`", StringComparison.Ordinal));
        Assert.IsTrue(md.Contains("`r2`", StringComparison.Ordinal));
        Assert.IsFalse(md.Contains("`m1`", StringComparison.Ordinal),
            "a blocking finding is not intake; it was resolved before the cycle landed");
    }

    [TestMethod]
    public void APipeInASummaryDoesNotTruncateTheRow()
    {
        // Unescaped, markdown reads the pipe as a cell boundary and the rest of the sentence
        // lands in the wrong column or disappears — losing the half that says what is wrong.
        string md = Render();

        Assert.IsTrue(md.Contains(@"a \| b", StringComparison.Ordinal),
            "the pipe must be escaped rather than stripped: it is part of what the agent wrote");
        Assert.IsTrue(md.Contains("the second half must survive", StringComparison.Ordinal),
            "the text after the pipe must still be present");
    }

    [TestMethod]
    public void BlockingFindingsSortAboveSuggestedOnes()
    {
        string md = Render();

        int blocking = md.IndexOf("`m1`", StringComparison.Ordinal);
        int suggested = md.IndexOf("`r1`", StringComparison.Ordinal);

        Assert.IsTrue(blocking > 0 && suggested > 0);
        Assert.IsTrue(blocking < suggested,
            "what halts the cycle belongs at the top; a reader should not have to search for it");
    }

    [TestMethod]
    public void TheRenderSaysTheJsonIsAuthoritative()
    {
        // Findings exist in two representations. Without a stated authority that is an INV-03
        // duplication, and the statement has to be in the generated file itself — that is the
        // copy someone will find and be tempted to edit.
        string md = Render();

        Assert.IsTrue(md.Contains("GENERATED", StringComparison.Ordinal));
        Assert.IsTrue(md.Contains("JSON is authoritative", StringComparison.Ordinal));
        Assert.IsTrue(md.Contains("Edits here are lost", StringComparison.Ordinal),
            "the file must say what happens to an edit, not merely that it was generated");
    }

    [TestMethod]
    public void RepairsAreRenderedOutsideTheTable()
    {
        // A repair is a paragraph. Inside a cell it is unreadable; dropped, the most
        // actionable part of a finding is lost.
        string md = Render();

        Assert.IsTrue(md.Contains("## Suggested repairs", StringComparison.Ordinal));
        Assert.IsTrue(md.Contains("Write a justification next to the suppression.", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AnEmptyResultIsStatedRatherThanBlank()
    {
        // An interrogation that finds nothing is a normal and useful result. A blank file
        // reads as a broken render.
        string md = Render("-State open");

        Assert.IsTrue(md.Contains("No `open` findings", StringComparison.Ordinal),
            "an empty set must say so explicitly");
        Assert.IsTrue(md.Contains("normal and useful", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WritingToAFileCreatesMissingDirectories()
    {
        // The suggestions directory does not exist until the first cycle lands, and landing
        // is not the moment to fail on a missing folder.
        Render("-State suggested -OutFile \".sdlc/suggestions/20260925-aa01.md\"");

        string written = Path.Combine(_repo!, ".sdlc", "suggestions", "20260925-aa01.md");
        Assert.IsTrue(File.Exists(written), "the renderer must create the directory it writes into");

        string text = File.ReadAllText(written);
        Assert.IsTrue(text.Contains("Suggestions from cycle 20260925-aa01", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TheFileIsWrittenWithoutAByteOrderMark()
    {
        // Committed markdown. A BOM renders as a stray character in some viewers, and unlike
        // the .ps1 files this is never executed by PowerShell 5.1, so nothing needs one.
        Render("-State suggested -OutFile \".sdlc/suggestions/bom-check.md\"");

        byte[] head = new byte[3];
        using FileStream fs = File.OpenRead(Path.Combine(_repo!, ".sdlc", "suggestions", "bom-check.md"));
        Assert.AreEqual(3, fs.Read(head, 0, 3));

        bool hasBom = head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
        Assert.IsFalse(hasBom, "rendered markdown must not carry a BOM");
    }

    [TestMethod]
    public void LandingWritesTheSuggestionsFileBeforeTheSquash()
    {
        // Order is the point. Written on the cycle branch before it is pushed for review, the
        // file rides in with the squash exactly as the retro does; written after, it would land
        // on master as a separate commit outside the cycle it belongs to.
        string landing = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "tools", "Complete-SdlcCycle.ps1"));

        int suggestions = landing.IndexOf("unresolved suggestions become intake", StringComparison.Ordinal);
        int push = landing.IndexOf("Add-Step 'push'", StringComparison.Ordinal);
        int reverify = landing.IndexOf("reverify-failed", StringComparison.Ordinal);

        Assert.IsTrue(suggestions > 0, "landing must write the suggestions file");

        // Before the PUSH, not before a local merge: squashing now happens when a person
        // merges the pull request, so the branch has to carry the file by the time it is
        // pushed for review.
        Assert.IsTrue(suggestions < push,
            "the file must be committed to the branch before the branch is pushed for review");
        Assert.IsTrue(reverify < suggestions,
            "and after re-verification: a cycle that fails re-verify is not landing, so its " +
            "suggestions are not yet intake");
    }
}
