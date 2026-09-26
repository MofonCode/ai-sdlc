using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// The fact gatherer against a repository that has no charter and no commits.
/// </summary>
/// <remarks>
/// THIS IS A FIRST-USE STATE, NOT A BROKEN ONE. The precondition rule is explicit that an
/// ungoverned repository is an ordinary case with a defined answer — "no charter here;
/// <c>/sdlc:init</c> creates one" — and a freshly initialised solution can have its first artifact
/// written before anything is committed.
/// <para>
/// The gatherer died on both. <c>git rev-parse HEAD</c> fails with no commits, and taking
/// <c>.Trim()</c> of what it did not return threw "You cannot call a method on a null-valued
/// expression". With no charter, a variable assigned only inside <c>if ($charterPresent)</c> threw
/// "The variable '$charterFm' cannot be retrieved" under strict mode. Both name an internal detail
/// rather than the missing thing the gatherer actually found, on what may be a person's first ever
/// use of this framework.
/// </para>
/// <para>
/// Every other fixture in this suite commits and writes a charter, which is exactly why neither was
/// visible: a fixture that avoids the awkward state cannot find the bugs that state causes.
/// </para>
/// </remarks>
[TestClass]
public sealed class UngovernedRepositoryTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Get-SdlcFacts.ps1");

    private static string? _repo;

    [ClassInitialize]
    public static void CreateBareRepo(TestContext context)
    {
        _repo = Path.Combine(Path.GetTempPath(), "sdlc-ungoverned-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "intents"));

        // Initialised and NOTHING ELSE. No commit, no charter — the state `init` leaves behind, and
        // the state a person is in when they type their first command.
        Run("git", "init --initial-branch=master", _repo);

        File.WriteAllText(Path.Combine(_repo, ".sdlc", "intents", "20260926-ab01.md"),
            """
            ---
            id: 20260926-ab01
            source: authored
            charter-version: 1
            ---

            # The first intent anyone writes — before a charter, before a commit
            """);
    }

    [ClassCleanup]
    public static void RemoveBareRepo()
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

    private static JsonDocument Gather()
    {
        string stdout = Run(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" " +
            "-Artifact \".sdlc/intents/20260926-ab01.md\"",
            _repo!);

        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout),
            "the gatherer printed nothing for an ungoverned repository");

        return JsonDocument.Parse(stdout);
    }

    [TestMethod]
    public void ItStillEmitsValidJson()
    {
        // The whole contract. `interrogate` responds to unparseable facts by continuing WITHOUT
        // mechanical findings, so a gatherer that dies here produces a clean-looking interrogation
        // rather than an error — which is how the encoding defect stayed invisible for days.
        using JsonDocument facts = Gather();

        Assert.AreEqual(JsonValueKind.Object, facts.RootElement.ValueKind);
    }

    [TestMethod]
    public void TheMissingCommitIsReportedAsNullRatherThanCrashing()
    {
        using JsonDocument facts = Gather();

        Assert.AreEqual(JsonValueKind.Null,
            facts.RootElement.GetProperty("generated-at-sha").ValueKind,
            "there is no commit to pin, and saying so is the honest answer");
    }

    [TestMethod]
    public void TheMissingCharterIsAFAILEDCHECKRatherThanACrash()
    {
        // The absence of a charter is the SIGNAL that a repository is not governed. It has a named
        // check and a defined consequence, so it must arrive as a finding rather than as a stack
        // trace: a stage refuses, names what is missing, and offers the command that produces it.
        using JsonDocument facts = Gather();

        List<string> failed = [.. facts.RootElement.GetProperty("failed-checks").EnumerateArray()
            .Select(e => e.GetString()!)];

        Assert.IsTrue(failed.Contains("charter-present"),
            "an ungoverned repository must fail the charter check: " + string.Join(", ", failed));

        Assert.IsFalse(facts.RootElement.GetProperty("charter").GetProperty("present").GetBoolean());
    }

    [TestMethod]
    public void TheArtifactIsStillIdentifiedAndStillChecked()
    {
        // Losing the charter loses the invariants, not the artifact. Two of the three citation
        // routes survive without one — a failed mechanical check and a location pair — so an
        // ungoverned repository still catches unresolvable references and false claims about
        // the tree.
        using JsonDocument facts = Gather();
        JsonElement artifact = facts.RootElement.GetProperty("artifact");

        Assert.AreEqual("intent", artifact.GetProperty("kind").GetString());
        Assert.AreEqual("20260926-ab01", artifact.GetProperty("cycle-id").GetString());
        Assert.IsTrue(artifact.GetProperty("governed").GetBoolean());
        Assert.IsTrue(facts.RootElement.GetProperty("checks").GetArrayLength() > 1);
    }
}
