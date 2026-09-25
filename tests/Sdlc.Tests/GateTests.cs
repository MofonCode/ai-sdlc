using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// The two gates that were conventions until they were scripts.
/// </summary>
/// <remarks>
/// The intent gate is the one that had actually been bypassed. Both intents authored on
/// 2026-09-24 were written straight to master, which removes the pull request and therefore
/// removes the gate, and nothing noticed. So the interesting assertions here are the
/// REFUSALS: submitting from a cycle branch, and submitting something already submitted.
/// <para>
/// Charter-intent dedup matters only under concurrency, which is exactly when nobody is
/// watching. Eight cycles hitting one bad invariant file eight identical requests without
/// it, and the case worth its own test is the <c>abandoned</c> one: a cycle blocked on a
/// question already refused must surface the refusal rather than re-ask it.
/// </para>
/// </remarks>
[TestClass]
public sealed class GateTests
{
    private static string SubmitScript =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Submit-SdlcIntent.ps1");

    private static string FindScript =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Find-SdlcCharterIntent.ps1");

    private static string? _repo;

    [ClassInitialize]
    public static void CreateFixtureRepo(TestContext context)
    {
        _repo = Path.Combine(Path.GetTempPath(), "sdlc-gate-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "intents", "abandoned"));

        // An ordinary intent, ready to submit.
        Intent("20260925-aa01", "authored", "An ordinary intent");

        // Submitted twice, to prove the second attempt refuses.
        Intent("20260925-aa02", "authored", "Submitted once already");

        // Refused from a cycle branch.
        Intent("20260925-aa03", "authored", "Submitted from the wrong branch");

        // A PENDING charter intent targeting INV-02: a cycle hitting INV-02 links to it.
        Intent("20260925-bb01", "charter-review", "INV-02 is too broad", targets: "[INV-02]");

        // An ABANDONED charter intent targeting INV-05, with the reason that was given.
        // This is the case that earns the script.
        Intent("20260925-bb02", "charter-review", "Drop the file declaration",
            targets: "[INV-05]",
            reason: "rejected: the declaration is what makes scope drift detectable",
            abandoned: true);

        // Three refusals against INV-07 — a clarification signal, not a deletion one.
        for (int i = 3; i <= 5; i++)
        {
            Intent($"20260925-bb0{i}", "charter-review", $"Narrow INV-07, attempt {i}",
                targets: "[INV-07]", reason: $"rejected: attempt {i} restated the same case",
                abandoned: true);
        }

        Git("init --initial-branch=master");
        Git("add -A");
        Git("-c user.email=t@example.invalid -c user.name=Test commit -m fixture");
    }

    private static void Intent(
        string id, string source, string title,
        string? targets = null, string? reason = null, bool abandoned = false)
    {
        List<string> fm =
        [
            "---",
            $"id: {id}",
            $"source: {source}",
            "charter-version: 2",
        ];

        if (targets is not null)
        {
            fm.Add($"targets: {targets}");
        }

        if (reason is not null)
        {
            fm.Add($"reason: {reason}");
        }

        fm.Add("---");
        fm.Add(string.Empty);
        fm.Add($"# {title}");
        fm.Add(string.Empty);

        string dir = abandoned
            ? Path.Combine(_repo!, ".sdlc", "intents", "abandoned")
            : Path.Combine(_repo!, ".sdlc", "intents");

        File.WriteAllText(Path.Combine(dir, id + ".md"), string.Join("\n", fm));
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

    /// <summary>
    /// The scripts print human lines before their JSON, so the object is parsed from the
    /// first line that opens one rather than from the whole stream.
    /// </summary>
    private static JsonElement JsonOf(string stdout)
    {
        int start = stdout.IndexOf('{', StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "no JSON in:\n" + stdout);
        return JsonDocument.Parse(stdout[start..]).RootElement.Clone();
    }

    private static JsonElement Submit(string id, string extra = "") =>
        JsonOf(Run("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{SubmitScript}\" -CycleId {id} {extra}"));

    private static JsonElement Find(string target) =>
        JsonOf(Run("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{FindScript}\" -Target {target}"));

    // --- the intent gate ---------------------------------------------------

    [TestMethod]
    public void SubmittingCreatesTheIntentBranchAndCommitsOnlyTheIntent()
    {
        JsonElement r = Submit("20260925-aa01", "-NoPullRequest");

        Assert.AreEqual("branched", r.GetProperty("outcome").GetString());
        Assert.AreEqual("intent-20260925-aa01", r.GetProperty("branch").GetString());

        string branches = Run("git", "for-each-ref --format=%(refname:short) refs/heads");
        Assert.IsTrue(branches.Contains("intent-20260925-aa01", StringComparison.Ordinal));

        Run("git", "checkout master");
    }

    [TestMethod]
    public void TheBranchPrefixIsIntentNotCycle()
    {
        // The lifecycle derivation keys on `cycle-<id>` specifically. If the intent branch
        // shared that prefix, a lingering one would read as work in progress and the
        // dispatcher would refuse to start the very cycle it is waiting for.
        JsonElement r = Submit("20260925-aa02", "-NoPullRequest");

        string branch = r.GetProperty("branch").GetString()!;
        Assert.IsTrue(branch.StartsWith("intent-", StringComparison.Ordinal));
        Assert.IsFalse(branch.StartsWith("cycle-", StringComparison.Ordinal));

        Run("git", "checkout master");
    }

    [TestMethod]
    public void SubmittingTwiceRefusesRatherThanBranchingAgain()
    {
        Submit("20260925-aa02", "-NoPullRequest");
        Run("git", "checkout master");

        JsonElement again = Submit("20260925-aa02", "-NoPullRequest");
        Assert.AreEqual("already-submitted", again.GetProperty("outcome").GetString());
    }

    [TestMethod]
    public void SubmittingFromACycleBranchIsRefused()
    {
        // THE REFUSAL THAT MATTERS. An intent commit on top of a cycle's work means approving
        // the intent approves that work too, which is the mixed-diff failure the two-branch
        // design exists to prevent.
        Run("git", "checkout -b cycle-20260925-ffff");
        try
        {
            JsonElement r = Submit("20260925-aa03", "-NoPullRequest");
            Assert.AreEqual("refused-on-cycle-branch", r.GetProperty("outcome").GetString());
        }
        finally
        {
            Run("git", "checkout master");
        }
    }

    [TestMethod]
    public void ANonexistentIntentIsRefusedBeforeAnyBranchIsMade()
    {
        // Ordering: the branch must not exist if the submission cannot succeed, or a retry
        // hits `already-submitted` for an intent that was never submitted.
        using Process p = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{SubmitScript}\" -CycleId 20260925-dead",
            WorkingDirectory = _repo!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();

        Assert.AreNotEqual(0, p.ExitCode, "a missing intent must fail rather than branch");

        string branches = Run("git", "for-each-ref --format=%(refname:short) refs/heads");
        Assert.IsFalse(branches.Contains("intent-20260925-dead", StringComparison.Ordinal),
            "no branch may be left behind by a refused submission");
    }

    // --- charter-intent dedup ----------------------------------------------

    [TestMethod]
    public void APendingCharterIntentIsLinkedRatherThanDuplicated()
    {
        // Without this, eight concurrent cycles hitting one invariant file eight identical
        // requests and a person answers the same question eight times.
        JsonElement r = Find("INV-02");

        Assert.AreEqual("link-and-block", r.GetProperty("verdict").GetString());
        Assert.AreEqual(1, r.GetProperty("pending-count").GetInt32());
    }

    [TestMethod]
    public void AnAbandonedCharterIntentBlocksAndSurfacesItsReason()
    {
        // THE USEFUL CASE. Blocked on an already-answered question is a different signal from
        // blocked on an open one: the WORK needs rethinking, not the charter. And the reason
        // already given is shown rather than re-derived.
        JsonElement r = Find("INV-05");

        Assert.AreEqual("blocked-already-answered", r.GetProperty("verdict").GetString());
        Assert.IsTrue(r.GetProperty("advice").GetString()!
            .Contains("what makes scope drift detectable", StringComparison.Ordinal),
            "the recorded reason must be surfaced, not merely counted");
    }

    [TestMethod]
    public void NothingTargetingTheRuleMeansFileOne()
    {
        JsonElement r = Find("INV-01");

        Assert.AreEqual("file-one", r.GetProperty("verdict").GetString());
        Assert.AreEqual(0, r.GetProperty("pending-count").GetInt32());
        Assert.AreEqual(0, r.GetProperty("abandoned-count").GetInt32());
    }

    [TestMethod]
    public void ThreeRefusalsRaiseAClarificationSignal()
    {
        // Repeated rejections say the rule is UNCLEAR rather than wrong: agents keep bumping
        // into it and a person keeps saying no. It is a charter amendment signal, and the
        // action it implies is clarifying the wording, never deleting the rule.
        JsonElement r = Find("INV-07");

        Assert.AreEqual(3, r.GetProperty("abandoned-count").GetInt32());
        Assert.IsTrue(r.GetProperty("clarification-signal").GetBoolean());
    }

    [TestMethod]
    public void OnlyReviewIntentsCountAsCharterIntents()
    {
        // An ordinary intent that happens to mention an invariant is not a request to change
        // it. Counting one would block a cycle on work that was never asking for anything.
        JsonElement r = Find("INV-01");

        Assert.AreEqual("file-one", r.GetProperty("verdict").GetString(),
            "three authored intents exist in this fixture and none should register");
    }
}
