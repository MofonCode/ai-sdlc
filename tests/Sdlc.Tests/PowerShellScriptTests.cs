using System.Diagnostics;
using System.Text;

namespace Sdlc.Tests;

/// <summary>
/// Checks every shipped PowerShell script for the failures Windows PowerShell 5.1
/// produces and nothing else catches.
///
/// The BOM check is the important one, and it exists because of a real break. The
/// charter commits the framework's shipped scripts to Windows PowerShell 5.1, and 5.1
/// reads a file WITHOUT a byte-order mark as ANSI rather than UTF-8. An em-dash then
/// decodes under CP1252 as three characters, the last of which is a smart closing
/// quote — and PowerShell accepts that as a string terminator. So a single em-dash
/// inside a double-quoted string silently ends it early and the rest of the line
/// becomes bare tokens.
///
/// House style here uses em-dashes liberally. Without this check, any script that
/// puts one inside a string is broken on the runtime it is committed to, and the only
/// symptom is a parse error nobody sees until the script runs.
/// </summary>
[TestClass]
public sealed class PowerShellScriptTests
{
    private static string ToolDir => Path.Combine(AppContext.BaseDirectory, "tools");

    public static IEnumerable<object[]> AllScripts() =>
        Directory.EnumerateFiles(ToolDir, "*.ps1")
            .Select(p => new object[] { Path.GetFileName(p) });

    private static string PathOf(string name) => Path.Combine(ToolDir, name);

    /// <summary>Stages a person drives, which an unattended run must never reach.</summary>
    private static readonly string[] SupervisedStages =
        ["/sdlc:spec", "/sdlc:plan", "/sdlc:intent", "/sdlc:interrogate"];

    /// <summary>
    /// The script with comments stripped, for checks that must read code rather than prose.
    /// See <see cref="SourceText"/> for why every construct check goes through this.
    /// </summary>
    private static string CodeOf(string name) =>
        SourceText.StripPowerShellComments(File.ReadAllText(PathOf(name)));

    [TestMethod]
    public void ScriptsShip()
    {
        Assert.IsTrue(AllScripts().Any(), "no PowerShell scripts found in the output");
    }

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void HasAUtf8ByteOrderMark(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        byte[] head = new byte[3];
        using (FileStream fs = File.OpenRead(PathOf(name)))
        {
            Assert.AreEqual(3, fs.Read(head, 0, 3), $"{name} is too short to be a script");
        }

        bool hasBom = head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;

        Assert.IsTrue(hasBom,
            $"{name} has no UTF-8 BOM. Windows PowerShell 5.1 reads a BOM-less file as ANSI, " +
            "so an em-dash inside a string decodes to a smart closing quote and terminates it early.");
    }

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void ParsesUnderWindowsPowerShell(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // Parsed by the runtime that will run it, not by a lookalike. Encoding bugs of the
        // kind above only appear in the real parser reading the real bytes.
        string script =
            "$errs = $null; " +
            "$null = [System.Management.Automation.Language.Parser]::ParseFile(" +
            $"'{PathOf(name)}', [ref]$null, [ref]$errs); " +
            "if ($errs) { $errs | ForEach-Object { $_.Message } } else { 'OK' }";

        using Process p = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", script },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd().Trim();
        p.WaitForExit();

        Assert.AreEqual("OK", stdout, $"{name} does not parse: {stdout}");
    }

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void DeclaresItsMinimumVersionAndUsesStrictMode(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string text = File.ReadAllText(PathOf(name));

        Assert.IsTrue(text.Contains("#Requires -Version 5.1", StringComparison.Ordinal),
            $"{name} must declare the version it is committed to running on");
        Assert.IsTrue(text.Contains("Set-StrictMode", StringComparison.Ordinal),
            $"{name} must use strict mode — it is what caught the single-element unwrap bug");
    }

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void NeverRedirectsNativeStderrIntoTheSuccessStream(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // THE BUG THIS PREVENTS FIRES ON SUCCESS, which is why nothing caught it for days.
        //
        // In Windows PowerShell, `2>&1` on a native command wraps each stderr line in an
        // ErrorRecord and sets $? to false EVEN AT EXIT CODE 0. Under the
        // $ErrorActionPreference = 'Stop' every script here sets, that terminates. And git
        // writes ordinary progress to stderr: `git checkout -b x` says "Switched to a new
        // branch" there when it works.
        //
        // Fourteen calls were written that way, eight in the landing script, which had never
        // been run. The first landing would have died at its first git command with an error
        // describing a successful operation.
        //
        // SdlcGit.ps1 is the sanctioned path: it redirects stderr to a FILE, which creates no
        // ErrorRecord, leaving the exit code as the only signal of failure.
        string code = CodeOf(name);

        Assert.IsFalse(code.Contains("2>&1", StringComparison.Ordinal),
            $"{name}: redirect native stderr to a file, or use Invoke-SdlcGit / " +
            "Invoke-SdlcNative from SdlcGit.ps1. `2>&1` makes a successful git call throw.");
    }

    [TestMethod]
    public void ScriptsThatCallGitUseTheHelper()
    {
        // A script calling git directly is not wrong in itself — a read-only call whose
        // output is consumed is fine, and Get-SdlcFacts does that. What must not happen is a
        // script branching on $LASTEXITCODE after a call whose stderr it redirected, because
        // that is the pattern above wearing a different hat. So: any script that mutates
        // state with git dot-sources the helper.
        foreach (string name in new[] { "Complete-SdlcCycle.ps1", "Submit-SdlcIntent.ps1" })
        {
            string code = CodeOf(name);

            Assert.IsTrue(code.Contains("SdlcGit.ps1", StringComparison.Ordinal),
                $"{name} mutates repository state with git and must dot-source the helper");
            Assert.IsTrue(code.Contains("Invoke-SdlcGit", StringComparison.Ordinal),
                $"{name} must call git through Invoke-SdlcGit");
        }
    }

    [TestMethod]
    [DynamicData(nameof(AllScripts))]
    public void AvoidsConstructsWindowsPowerShellLacks(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string code = CodeOf(name);

        // 5.1 has no null-coalescing, no ternary and no pipeline chain operators. Each
        // would be a runtime failure on the committed runtime rather than a warning.
        Assert.IsFalse(code.Contains("??", StringComparison.Ordinal),
            $"{name}: no null-coalescing in 5.1");
        Assert.IsFalse(code.Contains("&&", StringComparison.Ordinal),
            $"{name}: no pipeline chain operators in 5.1");
        Assert.IsFalse(code.Contains("-AsHashtable", StringComparison.Ordinal),
            $"{name}: ConvertFrom-Json has no -AsHashtable in 5.1");
    }

    [TestMethod]
    public void TheStateDerivationCoversEveryStateItClaims()
    {
        // Eight states, each documented in the script's own help. If one is described and
        // never assigned, the derivation is incomplete and something sits in no state at
        // all — which is how a finished cycle came to read as queued in an earlier draft.
        string text = File.ReadAllText(PathOf("Get-SdlcCycleState.ps1"));

        foreach (string state in new[]
                 {
                     "queued", "planned", "in-progress", "dead", "awaiting-landing",
                     "blocked", "done", "abandoned",
                 })
        {
            Assert.IsTrue(text.Contains($"'{state}'", StringComparison.Ordinal),
                $"the derivation documents '{state}' but never assigns it");
        }
    }

    [TestMethod]
    public void TheRunnerBuildsAndVerifiesAndNothingElse()
    {
        // The batch builds; it does not think. Intent, spec and plan are supervised because
        // they need judgment and may need to ask the user something. If the dispatcher's
        // prompt grows an authoring stage back, unattended runs start producing artifacts
        // nobody reviewed — which is the failure this split exists to prevent.
        // STRIPPED, per SourceText. The script carries a comment explaining why
        // '/sdlc:intent' is deliberately absent, and reading raw text matched that comment
        // as if it were the reference — the fifth time this suite has made that mistake.
        string code = CodeOf("Invoke-SdlcRun.ps1");

        Assert.IsTrue(code.Contains("/sdlc:build", StringComparison.Ordinal));
        Assert.IsTrue(code.Contains("/sdlc:verify", StringComparison.Ordinal));

        foreach (string stage in SupervisedStages)
        {
            Assert.IsFalse(code.Contains(stage, StringComparison.Ordinal),
                $"the unattended prompt must not reach {stage}: that stage is supervised");
        }
    }

    [TestMethod]
    public void TheRunnerHasNoConcurrencyCap()
    {
        // A cap is the engine guessing at a resource question it cannot know. The control
        // is the confirmation, and what it must show is the worktree count — that is what
        // predicts the thing most likely to bite.
        string text = File.ReadAllText(PathOf("Invoke-SdlcRun.ps1"));

        Assert.IsTrue(text.Contains("NO CONCURRENCY CAP", StringComparison.Ordinal),
            "the absence of a cap is a decision and must be stated where someone would add one");
        Assert.IsTrue(text.Contains("worktree(s) will be created", StringComparison.Ordinal),
            "the confirmation must state the worktree count, not only the intent list");
        Assert.IsTrue(text.Contains("SupportsShouldProcess", StringComparison.Ordinal),
            "-WhatIf must show the plan without acting on it");
    }

    [TestMethod]
    public void LandingReVerifiesAfterRebaseAndRefusesToMergeIfItFails()
    {
        // Worktrees make file collision impossible but not SEMANTIC collision: cycle A
        // renames a method, cycle B adds a caller elsewhere, git merges both cleanly and
        // the build breaks. Only the post-rebase run catches that, which is why it is
        // mandatory and why landings are serialized.
        string text = File.ReadAllText(PathOf("Complete-SdlcCycle.ps1"));

        Assert.IsTrue(text.Contains("reverify-failed", StringComparison.Ordinal),
            "a failed post-rebase verification must be a named outcome, not a warning");
        // The squash now happens when a person merges the pull request, so what the script
        // owes is the INSTRUCTION. One cycle becomes one commit either way, which is what keeps
        // git revert of a cycle a single operation - and a PR merged without squashing breaks
        // that quietly.
        Assert.IsTrue(text.Contains("SQUASH-MERGE THIS", StringComparison.Ordinal),
            "the pull request must say how to merge, or one cycle stops being one commit");
        Assert.IsFalse(text.Contains("Invoke-SdlcGit merge", StringComparison.Ordinal),
            "landing must not merge locally: deploy means PR plus review, and a local merge " +
            "skips the review half exactly as writing an intent to master skipped the intent gate");
        Assert.IsTrue(text.Contains("awaiting-landing", StringComparison.Ordinal),
            "only a verified cycle can be landed");
    }
}
