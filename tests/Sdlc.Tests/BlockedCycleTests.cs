using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// Blocking a cycle on a charter intent, and discarding the work built under the old rules.
/// </summary>
/// <remarks>
/// A CYCLE NEVER AMENDS THE CHARTER GOVERNING IT — it files a request and stops, for the same
/// reason a process cannot grant itself a permission. The four steps that follow were described in
/// the design and implemented in none of them: record the link, discard the worktree, write no
/// retro, and let the block self-clear when the charter intent merges.
/// <para>
/// The link is the load-bearing half. Blockedness is DERIVED from whether the <c>blocked-by</c>
/// target has merged, so nothing writes a status field and there is no unblock step — but nothing
/// wrote the link either, which meant the derivation had an input no code produced. A blocked cycle
/// read as <c>dead</c>, and <c>dead</c> means "relaunch or discard".
/// </para>
/// </remarks>
[TestClass]
public sealed class BlockedCycleTests
{
    private const string Cycle = "20260926-bb01";
    private const string CharterIntent = "20260926-cc02";

    private static string ToolDir => Path.Combine(AppContext.BaseDirectory, "tools");

    private static string? _repo;
    private static string? _worktree;

    [TestInitialize]
    public void CreateFixtureRepo()
    {
        // REBUILT PER TEST, because every test here deletes a branch or a worktree. Sharing one
        // fixture would make each test depend on which ran first, and the outcome of a destructive
        // operation is precisely what is being asserted.
        _repo = Path.Combine(Path.GetTempPath(), "sdlc-block-" + Guid.NewGuid().ToString("N")[..8]);
        _worktree = _repo + "-wt";

        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "intents", "abandoned"));

        File.WriteAllText(Path.Combine(_repo, ".sdlc", "CHARTER.md"),
            """
            ---
            version: 3
            solution: fixture
            ---

            # Charter — fixture

            ### INV-02 · One reading per statement

                Applies-to: intent, spec, plan
                Forbids:  a statement that admits two readings.
            """);

        WriteIntent(Cycle, "authored");

        Run("git", "init --initial-branch=master");
        Run("git", "-c user.email=t@example.invalid -c user.name=Test add -A");
        Run("git", "-c user.email=t@example.invalid -c user.name=Test commit -m fixture");

        // THE CHARTER INTENT IS PENDING, WHICH MEANS IT IS NOT ON MASTER. Blockedness is derived
        // from whether the blocker has merged, and the derivation reads merged as "its file is on
        // master" — so at the moment a cycle blocks, the intent it waits on has a branch and an open
        // pull request and nothing else. A fixture that put the file on master would be testing the
        // already-resolved case, where a block cannot hold for a moment.
        Run("git", $"branch intent-{CharterIntent}");

        // The cycle's work: a branch with a spec and a plan on it, and a worktree checked out —
        // exactly what has to be discarded, because it was built under rules that are changing.
        Run("git", $"branch cycle-{Cycle}");
        Run("git", $"worktree add \"{_worktree}\" cycle-{Cycle}");
    }

    [TestCleanup]
    public void RemoveFixtureRepo()
    {
        foreach (string? dir in new[] { _worktree, _repo })
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

    private static void WriteIntent(string id, string source, bool abandoned = false)
    {
        string dir = Path.Combine(_repo!, ".sdlc", "intents");
        if (abandoned)
        {
            dir = Path.Combine(dir, "abandoned");
        }

        File.WriteAllText(Path.Combine(dir, id + ".md"),
            $"""
            ---
            id: {id}
            source: {source}
            charter-version: 3
            ---

            # Intent {id}
            """);
    }

    private static string Run(string exe, string args, string? workingDirectory = null)
    {
        using Process p = Process.Start(new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = workingDirectory ?? _repo!,
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

    private sealed record Report(
        string Outcome, string? BlockerState, bool WorktreeRemoved, bool BranchDeleted, string[] Errors);

    private static Report Block(string cycle = Cycle, string blocker = CharterIntent, string extra = "")
    {
        string script = Path.Combine(ToolDir, "Block-SdlcCycle.ps1");
        string output = Run("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" " +
            $"-CycleId {cycle} -BlockedBy {blocker} -RepoRoot \"{_repo}\" {extra}");

        // Under -WhatIf, PowerShell prints its own preview line first and `-File` merges host output
        // into stdout. The JSON is always last, so read from the first brace.
        int start = output.IndexOf('{', StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "the block script printed no JSON: " + output);

        using JsonDocument doc = JsonDocument.Parse(output[start..]);
        JsonElement root = doc.RootElement;

        return new Report(
            root.GetProperty("outcome").GetString()!,
            root.GetProperty("blocker-state").GetString(),
            root.GetProperty("worktree-removed").GetBoolean(),
            root.GetProperty("branch-deleted").GetBoolean(),
            [.. root.GetProperty("errors").EnumerateArray().Select(e => e.GetString() ?? string.Empty)]);
    }

    private static string DerivedState(string cycle = Cycle)
    {
        string script = Path.Combine(ToolDir, "Get-SdlcCycleState.ps1");
        string json = Run("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -CycleId {cycle} -RepoRoot \"{_repo}\"");

        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement[0].GetProperty("state").GetString()!;
    }

    [TestMethod]
    public void TheLinkIsRecordedAndTheDerivationReadsItAsBlocked()
    {
        // The whole point of writing the link: `blocked` outranks every branch-based state, so a
        // cycle waiting on a charter decision cannot be mistaken for one that died.
        Assert.AreNotEqual("blocked", DerivedState(), "the fixture must not start blocked");

        Report r = Block();

        Assert.AreEqual("blocked", r.Outcome, string.Join(" | ", r.Errors));
        StringAssert.Contains(
            File.ReadAllText(Path.Combine(_repo!, ".sdlc", "intents", Cycle + ".md")),
            $"blocked-by: {CharterIntent}", StringComparison.Ordinal);

        Assert.AreEqual("blocked", DerivedState());
    }

    [TestMethod]
    public void TheWorkIsDiscardedSoTheCycleReRunsFromSpec()
    {
        // The spec and the plan live on that branch, so deleting it IS "re-run from spec". Resuming
        // work built under rules that are about to change would be worse than starting again.
        Report r = Block();

        Assert.IsTrue(r.WorktreeRemoved, "the worktree must be discarded");
        Assert.IsTrue(r.BranchDeleted, "the branch must be discarded");

        string heads = Run("git", "for-each-ref --format=%(refname:short) refs/heads");
        Assert.IsFalse(heads.Contains($"cycle-{Cycle}", StringComparison.Ordinal),
            "the cycle branch is still there: " + heads);
    }

    [TestMethod]
    public void ABlockerThatDoesNotExistIsRefused()
    {
        // `blocked-by` pointing at nothing NEVER RESOLVES, and blockedness is derived from whether
        // the target has merged — so an invented id blocks the cycle permanently, which is the one
        // outcome this state was designed to avoid.
        Report r = Block(blocker: "20260926-ffff");

        Assert.AreEqual("refused-no-blocker", r.Outcome);

        string heads = Run("git", "for-each-ref --format=%(refname:short) refs/heads");
        Assert.IsTrue(heads.Contains($"cycle-{Cycle}", StringComparison.Ordinal),
            "a refused block must not have discarded anything");
    }

    [TestMethod]
    public void APendingBlockerIsReportedAsPending()
    {
        // The ordinary case, and the one an earlier version of this script refused: the charter
        // intent has a branch and an open pull request, and no file on master at all.
        Report r = Block();

        Assert.AreEqual("blocked", r.Outcome, string.Join(" | ", r.Errors));
        Assert.AreEqual("pending", r.BlockerState,
            "a blocker with a branch and no landed file is pending, and the block self-clears on merge");
    }

    [TestMethod]
    public void ABlockerThatWasAlreadyREFUSEDIsRecordedAsSuch()
    {
        // A charter intent's PR merges EITHER WAY — approved with the charter edit, or rejected into
        // intents/abandoned/ with a reason. A cycle blocked on a question already answered is a
        // different signal from one blocked on an open question: it says the WORK needs rethinking,
        // not the charter, and a person has to act now rather than the cycle waiting.
        WriteIntent(CharterIntent, "charter-review", abandoned: true);

        Report r = Block();

        Assert.AreEqual("blocked", r.Outcome, string.Join(" | ", r.Errors));
        Assert.AreEqual("already-refused", r.BlockerState);
    }

    [TestMethod]
    public void ACycleWithARetroIsRefused()
    {
        // A BLOCKED CYCLE WRITES NO RETRO. The retro is the completion certificate, so one here
        // would leave the derivation reading `done` while the branch is deleted underneath it —
        // work that never landed, reading as finished.
        string cycleDir = Path.Combine(_repo!, ".sdlc", "cycles", Cycle);
        Directory.CreateDirectory(cycleDir);
        File.WriteAllText(Path.Combine(cycleDir, "retro.md"), "---\ncycle-id: " + Cycle + "\n---\n");

        Report r = Block();

        Assert.AreEqual("refused-retro-exists", r.Outcome);
    }

    [TestMethod]
    public void ACycleCannotBeBlockedOnItself()
    {
        // That is the self-amendment the rule exists to forbid, wearing a different hat.
        Report r = Block(blocker: Cycle);

        Assert.AreEqual("refused-self-block", r.Outcome);
    }

    [TestMethod]
    public void WhatIfChangesNothing()
    {
        Report r = Block(extra: "-WhatIf");

        Assert.AreEqual("whatif", r.Outcome);
        Assert.IsFalse(
            File.ReadAllText(Path.Combine(_repo!, ".sdlc", "intents", Cycle + ".md"))
                .Contains("blocked-by", StringComparison.Ordinal),
            "a preview must not write the link");

        string heads = Run("git", "for-each-ref --format=%(refname:short) refs/heads");
        Assert.IsTrue(heads.Contains($"cycle-{Cycle}", StringComparison.Ordinal),
            "a preview must not delete the branch");
    }

    [TestMethod]
    public void ALOCKEDWorktreeIsNotDiscarded()
    {
        // A lock means an agent is still running in it. Killing someone else's agent is not this
        // script's business, and the link is still worth recording.
        Run("git", $"worktree lock \"{_worktree}\"");

        try
        {
            Report r = Block();

            Assert.AreEqual("refused-worktree-locked", r.Outcome);

            string heads = Run("git", "for-each-ref --format=%(refname:short) refs/heads");
            Assert.IsTrue(heads.Contains($"cycle-{Cycle}", StringComparison.Ordinal),
                "nothing may be discarded while an agent holds the lock");
        }
        finally
        {
            Run("git", $"worktree unlock \"{_worktree}\"");
        }
    }
}
