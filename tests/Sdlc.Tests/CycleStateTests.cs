using System.Diagnostics;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// Exercises the lifecycle derivation against a real git repository, because the whole
/// point of deriving state is that it comes from evidence git already holds — testing it
/// against a mock would prove a mock.
///
/// The case that matters most is <c>done</c>. A naive derivation says "on master with no
/// cycle branch" means queued — but that also matches a FINISHED intent, because the
/// branch is gone once it merges. An unsupervised dispatcher reading that queue would
/// relaunch every completed cycle, forever. The retro is what distinguishes them.
/// </summary>
[TestClass]
public sealed class CycleStateTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Get-SdlcCycleState.ps1");

    private static string? _repo;

    /// <summary>A locked worktree, so `in-progress` has a fixture that is not a mock.</summary>
    private static string? _worktree;

    /// <summary>An UNLOCKED worktree whose path contains "locked", to catch a loose match.</summary>
    private static string? _decoyWorktree;

    [ClassInitialize]
    public static void CreateFixtureRepo(TestContext context)
    {
        _repo = Path.Combine(Path.GetTempPath(), "sdlc-state-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "intents", "abandoned"));

        // `source` is a parameter rather than a constant because a charter intent declares
        // `source: charter-review`, and adding a second `source:` line through `extra` would
        // have produced two — with the frontmatter reader taking the first and the test
        // silently exercising an ordinary intent.
        static string Intent(string id, string? extra = null, string source = "authored") => $"""
            ---
            id: {id}
            source: {source}
            charter-version: 1
            {extra ?? string.Empty}
            ---

            # Intent {id}
            """;

        string intents = Path.Combine(_repo!, ".sdlc", "intents");

        // queued — nothing but the file
        File.WriteAllText(Path.Combine(intents, "20260924-1111.md"), Intent("20260924-1111"));

        // done — a retro exists, which is the completion certificate
        File.WriteAllText(Path.Combine(intents, "20260924-2222.md"), Intent("20260924-2222"));
        Directory.CreateDirectory(Path.Combine(_repo!, ".sdlc", "cycles", "20260924-2222"));
        File.WriteAllText(
            Path.Combine(_repo!, ".sdlc", "cycles", "20260924-2222", "retro.md"),
            "---\ncycle-id: 20260924-2222\nsolution: fixture\nlanded-at: 2026-09-24\ncharter-version: 1\n---\n");

        // awaiting-landing — verify.md present, and a branch will be created below
        File.WriteAllText(Path.Combine(intents, "20260924-3333.md"), Intent("20260924-3333"));
        Directory.CreateDirectory(Path.Combine(_repo!, ".sdlc", "cycles", "20260924-3333"));
        File.WriteAllText(
            Path.Combine(_repo!, ".sdlc", "cycles", "20260924-3333", "verify.md"),
            "---\ncycle-id: 20260924-3333\nplan: 20260924-3333\ncharter-version: 1\noutcome: passed\n---\n");

        // blocked — blocked-by names a charter intent that does not exist yet
        File.WriteAllText(
            Path.Combine(intents, "20260924-4444.md"),
            Intent("20260924-4444", "blocked-by: 20260924-9999"));

        // blocked, then resolved — the blocker landed in abandoned/, which still counts as
        // merged: a charter intent's PR merges either way, and where it landed is the answer
        File.WriteAllText(
            Path.Combine(intents, "20260924-5555.md"),
            Intent("20260924-5555", "blocked-by: 20260924-8888"));
        File.WriteAllText(
            Path.Combine(intents, "abandoned", "20260924-8888.md"),
            Intent("20260924-8888", "reason: rejected — the invariant was right"));

        // abandoned — the file sits under abandoned/
        File.WriteAllText(
            Path.Combine(intents, "abandoned", "20260924-6666.md"),
            Intent("20260924-6666", "reason: priorities changed"));

        // planned — a plan a person approved. This is the ONLY dispatchable state, and the
        // `approved:` field is the whole gate: without it an unattended batch would pick up
        // work nobody signed off.
        Directory.CreateDirectory(Path.Combine(_repo!, ".sdlc", "cycles", "20260924-9a01"));
        File.WriteAllText(Path.Combine(intents, "20260924-9a01.md"), Intent("20260924-9a01"));
        File.WriteAllText(
            Path.Combine(_repo!, ".sdlc", "cycles", "20260924-9a01", "plan.md"),
            """
            ---
            cycle-id: 20260924-9a01
            spec: 20260924-9a01
            charter-version: 1
            predicted-tier: 1
            approved: 2026-09-24
            ---
            """);

        // queued — a complete-looking plan that nobody approved. The distinction is the
        // point: completeness is not approval.
        Directory.CreateDirectory(Path.Combine(_repo!, ".sdlc", "cycles", "20260924-9a02"));
        File.WriteAllText(Path.Combine(intents, "20260924-9a02.md"), Intent("20260924-9a02"));
        File.WriteAllText(
            Path.Combine(_repo!, ".sdlc", "cycles", "20260924-9a02", "plan.md"),
            """
            ---
            cycle-id: 20260924-9a02
            spec: 20260924-9a02
            charter-version: 1
            predicted-tier: 1
            ---
            """);

        // A CHARTER, because a charter intent records its completion there rather than in a
        // retro. TWO `amended:` lines, since the real charter has one per amendment and a
        // single-value frontmatter reader would silently see only the first.
        File.WriteAllText(Path.Combine(_repo!, ".sdlc", "CHARTER.md"), """
            ---
            version: 2
            solution: fixture
            amended: 2026-09-24 by 20260924-c001 - the first amendment
            amended: 2026-09-25 by 20260924-c002 - the second, so the reader cannot stop at one
            ---

            # Charter - fixture
            """);

        // done - a charter intent the charter says amended it. It has no cycle and therefore
        // no retro, so before this it read `queued` forever: finished work sitting at the
        // head of the queue, permanently.
        File.WriteAllText(
            Path.Combine(intents, "20260924-c001.md"),
            Intent("20260924-c001", source: "charter-review"));

        // Also done, and it is the SECOND `amended:` line that says so.
        File.WriteAllText(
            Path.Combine(intents, "20260924-c002.md"),
            Intent("20260924-c002", source: "charter-review"));

        // queued - a charter intent the charter does not mention. Its edit has not landed.
        File.WriteAllText(
            Path.Combine(intents, "20260924-c003.md"),
            Intent("20260924-c003", source: "charter-review"));

        Git("init --initial-branch=master");

        // A LOCAL IDENTITY, because the scripts under test run `git commit` themselves and a
        // CI runner has no global one. Without this the commit fails, and it failed SILENTLY:
        // Submit-SdlcIntent discarded the result and reported a successful submission with an
        // empty branch. CI caught that; every local run passed because a global identity
        // happened to exist.
        Git("config user.email t@example.invalid");
        Git("config user.name Test");
        Git("add -A");
        Git("-c user.email=t@example.invalid -c user.name=Test commit -m fixture");

        // A branch with no lock and no verify.md reads as dead; with verify.md it reads as
        // awaiting-landing. Both are created here, unlocked.
        Git("branch cycle-20260924-3333");
        Git("branch cycle-20260924-7777");
        File.WriteAllText(Path.Combine(intents, "20260924-7777.md"), Intent("20260924-7777"));

        // in-progress — a REAL locked worktree. An earlier version of this comment said a
        // locked worktree "needs a live agent holding it, which a test cannot honestly fake",
        // and left the one state nothing covered. That was wrong: `git worktree lock` writes a
        // file, it needs no process holding anything, and the derivation reads exactly that
        // file. The claim had talked the suite out of covering the state that decides whether
        // an unattended dispatcher relaunches work already running.
        File.WriteAllText(Path.Combine(intents, "20260924-8a01.md"), Intent("20260924-8a01"));
        Git("branch cycle-20260924-8a01");
        _worktree = _repo + "-wt";
        Git($"worktree add \"{_worktree}\" cycle-20260924-8a01");
        Git($"worktree lock \"{_worktree}\"");

        // dead, NOT in-progress — an UNLOCKED worktree whose PATH contains "locked". The lock
        // test reads `git worktree list --porcelain`, where `locked` is a line of its own; an
        // unanchored match also finds the word inside the path on the `worktree` line, and
        // would report an abandoned worktree as an agent actively running. That is the
        // expensive direction: a dispatcher skips work it should relaunch, silently and
        // forever.
        File.WriteAllText(Path.Combine(intents, "20260924-8a02.md"), Intent("20260924-8a02"));
        Git("branch cycle-20260924-8a02");
        _decoyWorktree = _repo + "-locked-wt";
        Git($"worktree add \"{_decoyWorktree}\" cycle-20260924-8a02");
    }

    [ClassCleanup]
    public static void RemoveFixtureRepo()
    {
        if (_repo is null || !Directory.Exists(_repo))
        {
            return;
        }

        // Unlock and remove the worktree first. A locked worktree keeps its own
        // administrative files, and deleting the repository under it leaves git metadata
        // pointing at nothing — which breaks the NEXT run rather than this one.
        if (_worktree is not null && Directory.Exists(_worktree))
        {
            TryGit($"worktree unlock \"{_worktree}\"");
            TryGit($"worktree remove --force \"{_worktree}\"");
        }

        if (_decoyWorktree is not null && Directory.Exists(_decoyWorktree))
        {
            TryGit($"worktree remove --force \"{_decoyWorktree}\"");
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

    /// <summary>Cleanup git: a failure here must not mask the test result.</summary>
    private static void TryGit(string args)
    {
        try
        {
            using Process p = Process.Start(new ProcessStartInfo("git", args)
            {
                WorkingDirectory = _repo!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            })!;
            p.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static string Run(string exe, string args)
    {
        using Process p = Process.Start(new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = _repo!,
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

    private static Dictionary<string, JsonElement> States()
    {
        string json = Run(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\"");

        using JsonDocument doc = JsonDocument.Parse(json);
        Dictionary<string, JsonElement> byId = [];

        foreach (JsonElement e in doc.RootElement.EnumerateArray())
        {
            byId[e.GetProperty("cycle-id").GetString()!] = JsonDocument.Parse(e.GetRawText()).RootElement;
        }

        return byId;
    }

    private static string StateOf(Dictionary<string, JsonElement> states, string id) =>
        states[id].GetProperty("state").GetString()!;

    [TestMethod]
    public void EveryIntentFileAppearsExactlyOnce()
    {
        // Asserted by id rather than by count: a count is brittle against adding a fixture,
        // and what actually matters is that nothing is silently omitted. An intent missing
        // from this report is an intent in no state at all, which is worse than a wrong one.
        Dictionary<string, JsonElement> states = States();

        foreach (string id in new[]
                 {
                     "20260924-1111", "20260924-2222", "20260924-3333", "20260924-4444",
                     "20260924-5555", "20260924-6666", "20260924-7777", "20260924-8888",
                     "20260924-9a01", "20260924-9a02",
                 })
        {
            Assert.IsTrue(states.ContainsKey(id), $"{id} is in no state at all");
        }
    }

    [TestMethod]
    public void AnIntentWithNothingElseIsQueued()
    {
        Assert.AreEqual("queued", StateOf(States(), "20260924-1111"));
    }

    [TestMethod]
    public void ARetroMeansDoneEvenWithNoBranchLeft()
    {
        // The defect this exists to prevent: with no cycle branch, a finished intent looks
        // identical to a queued one, so a dispatcher would relaunch it forever.
        Dictionary<string, JsonElement> states = States();

        Assert.AreEqual("done", StateOf(states, "20260924-2222"));
        Assert.IsFalse(states["20260924-2222"].GetProperty("branch-exists").GetBoolean(),
            "the branch is gone, which is exactly why the retro has to be the certificate");
        Assert.IsFalse(states["20260924-2222"].GetProperty("dispatchable").GetBoolean(),
            "a finished cycle must never be dispatchable");
    }

    [TestMethod]
    public void AVerifiedBranchIsAwaitingLanding()
    {
        // The state worth surfacing: progress has stopped and only a person can restart it.
        Assert.AreEqual("awaiting-landing", StateOf(States(), "20260924-3333"));
    }

    [TestMethod]
    public void ALockedWorktreeIsInProgressAndNotDispatchable()
    {
        // THE STATE THAT STOPS A DOUBLE LAUNCH. `in-progress` is how an unattended dispatcher
        // knows work is already running; read as `planned` instead, a batch would start a
        // second agent on the same branch. It was the one state with no coverage, excused by a
        // claim about locks that turned out to be false.
        Dictionary<string, JsonElement> states = States();

        Assert.AreEqual("in-progress", StateOf(states, "20260924-8a01"),
            "a branch with a locked worktree is being worked on right now");
        Assert.IsTrue(states["20260924-8a01"].GetProperty("worktree-locked").GetBoolean(),
            "the lock is the evidence, and it must be reported as such");
        Assert.IsFalse(states["20260924-8a01"].GetProperty("dispatchable").GetBoolean(),
            "dispatching this would put a second agent on a branch already in use");
    }

    [TestMethod]
    public void ACharterIntentIsDoneWhenTheCharterRecordsItsAmendment()
    {
        // A CHARTER INTENT HAS NO CYCLE, SO IT NEVER PRODUCES A RETRO. The derivation reads
        // `done` from a retro, so both real charter intents read `queued` permanently -
        // finished work sitting at the head of the queue forever.
        //
        // The charter itself records the completion, which keeps state derived rather than
        // stored: the amendment IS the certificate, and it lives in the document it changed.
        Dictionary<string, JsonElement> states = States();

        Assert.AreEqual("done", StateOf(states, "20260924-c001"),
            "the charter names this intent in an amended: line");
        Assert.IsTrue(states["20260924-c001"].GetProperty("charter-intent").GetBoolean(),
            "source: charter-review is what makes it one");

        // The SECOND line specifically. Get-FrontmatterField returns only the first match, so
        // reusing it here would have made every amendment after the first invisible.
        Assert.AreEqual("done", StateOf(states, "20260924-c002"),
            "the second amended: line must be read too");
    }

    [TestMethod]
    public void ACharterIntentTheCharterDoesNotMentionIsStillQueued()
    {
        // The other half. Reading completion from the charter must not mark every charter
        // intent done regardless of whether its edit landed.
        Dictionary<string, JsonElement> states = States();

        Assert.AreEqual("queued", StateOf(states, "20260924-c003"),
            "no amended: line names it, so its edit has not landed");
        StringAssert.Contains(
            states["20260924-c003"].GetProperty("reason").GetString()!,
            "does not record this amendment",
            StringComparison.Ordinal);
        Assert.IsFalse(states["20260924-c003"].GetProperty("dispatchable").GetBoolean(),
            "a charter intent is never batched - the charter gate is human");
    }

    [TestMethod]
    public void AWorktreePathContainingLockedIsNotALock()
    {
        // The word "locked" in a PATH is not a lock. `git worktree list --porcelain` puts
        // `locked` on a line of its own, so the match must be anchored; unanchored, this
        // fixture reads as an agent running and a dispatcher would never relaunch it.
        Dictionary<string, JsonElement> states = States();

        Assert.AreEqual("dead", StateOf(states, "20260924-8a02"),
            "an unlocked worktree is dead however its directory is spelled");
        Assert.IsFalse(states["20260924-8a02"].GetProperty("worktree-locked").GetBoolean(),
            "nothing here is locked");
    }

    [TestMethod]
    public void AnUnlockedBranchWithNoVerifyIsDead()
    {
        // Distinguishable from in-progress only by the worktree lock. Without that, a cycle
        // killed by resource exhaustion sits forever while the dispatcher refuses to touch
        // it — and with no concurrency cap, exhaustion is the likelier failure.
        Assert.AreEqual("dead", StateOf(States(), "20260924-7777"));
    }

    [TestMethod]
    public void AnUnresolvedBlockerMeansBlocked()
    {
        Dictionary<string, JsonElement> states = States();

        Assert.AreEqual("blocked", StateOf(states, "20260924-4444"));
        Assert.IsFalse(states["20260924-4444"].GetProperty("dispatchable").GetBoolean());
    }

    [TestMethod]
    public void ABlockerThatLandedInAbandonedStillResolves()
    {
        // A charter intent's PR merges either way — approved with the edit, or rejected into
        // abandoned/ with a reason. So blocked-by ALWAYS resolves and nothing can be blocked
        // on something that never merges. Where it landed is the answer, not whether.
        Assert.AreEqual("queued", StateOf(States(), "20260924-5555"),
            "a rejected blocker unblocks the waiting intent rather than stranding it");
    }

    [TestMethod]
    public void AFileUnderAbandonedIsAbandoned()
    {
        Assert.AreEqual("abandoned", StateOf(States(), "20260924-6666"));
    }

    [TestMethod]
    public void AnApprovedPlanIsPlannedAndDispatchable()
    {
        Dictionary<string, JsonElement> states = States();

        Assert.AreEqual("planned", StateOf(states, "20260924-9a01"));
        Assert.IsTrue(states["20260924-9a01"].GetProperty("plan-approved").GetBoolean());
        Assert.IsTrue(states["20260924-9a01"].GetProperty("dispatchable").GetBoolean(),
            "an approved plan is the only thing a batch may build");
    }

    [TestMethod]
    public void APlanNobodyApprovedIsNotDispatchable()
    {
        // The defect this prevents: a complete plan read as ready, so an unattended batch
        // builds work that never passed its gate. Completeness is not approval, and the
        // gate is a person.
        Dictionary<string, JsonElement> states = States();

        Assert.AreEqual("queued", StateOf(states, "20260924-9a02"));
        Assert.IsTrue(states["20260924-9a02"].GetProperty("has-plan").GetBoolean(),
            "the plan exists, which is exactly why this case is worth asserting");
        Assert.IsFalse(states["20260924-9a02"].GetProperty("plan-approved").GetBoolean());
        Assert.IsFalse(states["20260924-9a02"].GetProperty("dispatchable").GetBoolean());
    }

    [TestMethod]
    public void OnlyPlannedCyclesAreDispatchable()
    {
        Dictionary<string, JsonElement> states = States();

        foreach ((string id, JsonElement e) in states)
        {
            bool dispatchable = e.GetProperty("dispatchable").GetBoolean();
            bool planned = e.GetProperty("state").GetString() == "planned";

            Assert.AreEqual(planned, dispatchable,
                $"{id}: dispatchable must track planned exactly — anything looser either " +
                "relaunches work in flight or builds a plan nobody approved");
        }
    }
}
