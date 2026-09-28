using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sdlc.Tests;

/// <summary>
/// Hooks are the only enforcement a model cannot reason around, which makes an unverified hook the
/// most dangerous thing in the repository: it looks like protection and is not. Each script is fed
/// a synthetic event and its decision asserted — including the cases it must stay out of, because a
/// hook that denies too much gets disabled and then protects nothing at all.
/// </summary>
[TestClass]
public sealed class HookTests
{
    private const string CycleId = "20260927_ab12";

    private static string Tool(string name) => RepoPaths.At("tools", name);

    private sealed record Decision(bool Denied, string Reason);

    private static Decision Invoke(string script, object payload)
    {
        ProcessStartInfo info = new("powershell.exe")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in new[]
        {
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
        })
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info) ?? throw new InvalidOperationException("Could not start powershell.exe.");

        process.StandardInput.Write(JsonSerializer.Serialize(payload));
        process.StandardInput.Close();

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.AreEqual(0, process.ExitCode,
            $"{Path.GetFileName(script)} exited {process.ExitCode}. A hook signals refusal in its " +
            $"output, not by failing. stderr: {stderr.Trim()}");

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return new Decision(false, string.Empty);
        }

        JsonNode node = JsonNode.Parse(stdout)
            ?? throw new InvalidOperationException($"Unparseable hook output: {stdout}");

        JsonNode? output = node["hookSpecificOutput"];
        string decision = output?["permissionDecision"]?.GetValue<string>() ?? string.Empty;

        return new Decision(
            decision == "deny",
            output?["permissionDecisionReason"]?.GetValue<string>() ?? string.Empty);
    }

    private static object EditEvent(string filePath, string cwd, string toolName = "Write") => new
    {
        hook_event_name = "PreToolUse",
        tool_name = toolName,
        cwd,
        tool_input = new { file_path = filePath },
    };

    private static object BashEvent(string command, string cwd) => new
    {
        hook_event_name = "PreToolUse",
        tool_name = "Bash",
        cwd,
        tool_input = new { command },
    };

    // ------------------------------------------------------------------ derived artifacts

    [TestMethod]
    public void TargetedEditOfAVerifyDocumentIsRefused()
    {
        Decision decision = Invoke(
            Tool("Assert-DerivedArtifact.ps1"),
            EditEvent($"C:/repo/.sdlc/cycles/{CycleId}/verify.md", "C:/repo", toolName: "Edit"));

        Assert.IsTrue(decision.Denied, "Changing an outcome by hand must be refused.");
        Assert.IsTrue(decision.Reason.Length > 40, "A refusal must say what to do instead.");
    }

    [TestMethod]
    public void RederivingAVerifyDocumentIsAllowed()
    {
        // Re-deriving replaces the file whole, which is indistinguishable from a hand rewrite at the
        // call - so this layer allows it and CI validates the result. A hook that blocked it would
        // stop /sdlc:verify from doing its job.
        Decision decision = Invoke(
            Tool("Assert-DerivedArtifact.ps1"),
            EditEvent($"C:/repo/.sdlc/cycles/{CycleId}/verify.md", "C:/repo", toolName: "Write"));

        Assert.IsFalse(decision.Denied, $"Verify must be able to write its own document: {decision.Reason}");
    }

    [TestMethod]
    public void WritingANewFindingsRoundIsAllowed()
    {
        // The first version of this hook denied every write under findings/ and blocked
        // /sdlc:interrogate from writing its own output. Found by running the flow, not by reading it.
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");

        string target = Path.Combine(workspace.Path, ".sdlc", "intents", "findings", $"{CycleId}.r1.json");

        Decision decision = Invoke(Tool("Assert-DerivedArtifact.ps1"), EditEvent(target, workspace.Path));

        Assert.IsFalse(decision.Denied, $"Interrogation must be able to write its findings: {decision.Reason}");
    }

    [TestMethod]
    public void OverwritingAnExistingFindingsRoundIsRefused()
    {
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");

        string dir = Path.Combine(workspace.Path, ".sdlc", "intents", "findings");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, $"{CycleId}.r1.json");
        File.WriteAllText(target, "{}");

        Decision decision = Invoke(Tool("Assert-DerivedArtifact.ps1"), EditEvent(target, workspace.Path));

        Assert.IsTrue(decision.Denied, "Rounds are kept: the sequence is the evidence a repair worked.");
        StringAssert.Contains(decision.Reason, "round", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void OverwritingExistingEvidenceIsRefused()
    {
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");

        string dir = Path.Combine(workspace.Path, ".sdlc", "cycles", CycleId, "evidence");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, "tests.trx");
        File.WriteAllText(target, "<TestRun />");

        Decision decision = Invoke(Tool("Assert-DerivedArtifact.ps1"), EditEvent(target, workspace.Path));

        Assert.IsTrue(decision.Denied, "Replacing evidence makes every claim that cited it unfalsifiable.");
    }

    [TestMethod]
    [DataRow("src/Api/OrderController.cs")]
    [DataRow("plan.md")]
    public void EditingAnAuthoredArtifactIsAllowed(string relative)
    {
        // A plan is written by hand. A hook that blocked authored artifacts would make the process
        // unusable, and an over-refusing hook gets switched off - after which it protects nothing.
        Decision decision = Invoke(
            Tool("Assert-DerivedArtifact.ps1"),
            EditEvent($"C:/repo/{relative}", "C:/repo"));

        Assert.IsFalse(decision.Denied, $"{relative} is authored, not derived: {decision.Reason}");
    }

    // ------------------------------------------------- a governed directory without git

    [TestMethod]
    [DataRow("Assert-DeclaredFile.ps1")]
    [DataRow("Assert-Landable.ps1")]
    public void AGovernedDirectoryWithoutGitIsRefused(string script)
    {
        // The gates key on a cycle/<id> branch, and build's only evidence of what changed is a diff.
        // Without git a governed directory enforces nothing while still producing artifacts that look
        // governed, so the hooks fail closed here rather than permitting everything.
        string bare = Path.Combine(Path.GetTempPath(), "ai-sdlc-nogit", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(bare, ".sdlc"));

        try
        {
            Decision decision = script == "Assert-Landable.ps1"
                ? Invoke(Tool(script), BashEvent("gh pr create --fill", bare))
                : Invoke(Tool(script), EditEvent(Path.Combine(bare, "src", "Thing.cs"), bare));

            Assert.IsTrue(decision.Denied, $"{script} permitted everything in a governed directory with no git.");
            StringAssert.Contains(decision.Reason, "git init", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(bare, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("Assert-DeclaredFile.ps1")]
    [DataRow("Assert-Landable.ps1")]
    public void AnUngovernedDirectoryWithoutGitIsLeftAlone(string script)
    {
        // These hooks run on every edit everywhere. Refusing in an ordinary folder would get them
        // switched off, after which they protect nothing at all.
        string bare = Path.Combine(Path.GetTempPath(), "ai-sdlc-nogit", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(bare);

        try
        {
            Decision decision = script == "Assert-Landable.ps1"
                ? Invoke(Tool(script), BashEvent("gh pr create --fill", bare))
                : Invoke(Tool(script), EditEvent(Path.Combine(bare, "notes.txt"), bare));

            Assert.IsFalse(decision.Denied, $"{script} interfered with a directory it does not govern: {decision.Reason}");
        }
        finally
        {
            Directory.Delete(bare, recursive: true);
        }
    }

    // ------------------------------------------------------------------ landing

    [TestMethod]
    public void LandingIsNotConsideredOffACycleBranch()
    {
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");

        Decision decision = Invoke(Tool("Assert-Landable.ps1"), BashEvent("gh pr create --fill", workspace.Path));

        Assert.IsFalse(decision.Denied, "Off a cycle branch this hook has no cycle to judge.");
    }

    [TestMethod]
    public void LandingWithoutAVerifyDocumentIsRefused()
    {
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        workspace.Run("checkout", "-q", "-b", $"cycle/{CycleId}");

        Decision decision = Invoke(Tool("Assert-Landable.ps1"), BashEvent("gh pr create --fill", workspace.Path));

        Assert.IsTrue(decision.Denied, "An unverified cycle has no claim to land.");
        StringAssert.Contains(decision.Reason, "verify", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    [DataRow("blocked", 1, false)]
    [DataRow("escalated", 3, false)]
    [DataRow("pass", 0, true)]
    public void LandingFollowsTheVerifiedOutcome(string outcome, int passesSpent, bool shouldBeAllowed)
    {
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        workspace.Run("checkout", "-q", "-b", $"cycle/{CycleId}");

        string cycleDir = Path.Combine(workspace.Path, ".sdlc", "cycles", CycleId);
        Directory.CreateDirectory(cycleDir);
        File.WriteAllText(Path.Combine(cycleDir, "verify.md"),
            $"""
            ---
            outcome: {outcome}
            remediation:
              passes_spent: {passesSpent}
            ---

            # Verify - cycle {CycleId}
            """);

        Decision decision = Invoke(Tool("Assert-Landable.ps1"), BashEvent("gh pr create --fill", workspace.Path));

        Assert.AreEqual(shouldBeAllowed, !decision.Denied,
            $"outcome {outcome} with {passesSpent} passes spent: {decision.Reason}");

        if (outcome == "escalated")
        {
            // A reviewer must be able to tell work that resisted three attempts from work nobody
            // tried to fix, so the refusal carries the count.
            StringAssert.Contains(decision.Reason, "3", StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void ReadingPullRequestsIsNeverRefused()
    {
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        workspace.Run("checkout", "-q", "-b", $"cycle/{CycleId}");

        Decision decision = Invoke(Tool("Assert-Landable.ps1"), BashEvent("gh pr list --state open", workspace.Path));

        Assert.IsFalse(decision.Denied, "Only creating a pull request is gated.");
    }

    // ------------------------------------------------------------------ declared files

    [TestMethod]
    public void SourceEditsAreNotGatedOffACycleBranch()
    {
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");

        Decision decision = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            EditEvent(Path.Combine(workspace.Path, "src", "Greenfield.Core", "Totals.cs"), workspace.Path));

        Assert.IsFalse(decision.Denied,
            "Ordinary work in a repository that happens to be governed must be unaffected.");
    }

    [TestMethod]
    public void SourceEditWithNoPlanIsRefused()
    {
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        workspace.Run("checkout", "-q", "-b", $"cycle/{CycleId}");

        Decision decision = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            EditEvent(Path.Combine(workspace.Path, "src", "Greenfield.Core", "Totals.cs"), workspace.Path));

        Assert.IsTrue(decision.Denied, "A cycle branch with no plan may not touch source.");
        StringAssert.Contains(decision.Reason, "plan", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void SourceEditWithAnUnapprovedPlanIsRefused()
    {
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        WritePlan(workspace, approved: false);

        Decision decision = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            EditEvent(Path.Combine(workspace.Path, "src", "Greenfield.Core", "Totals.cs"), workspace.Path));

        Assert.IsTrue(decision.Denied,
            "The plan gate is the last review before anything becomes irreversible.");
        StringAssert.Contains(decision.Reason, "approved", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void UndeclaredFileIsRefusedEvenWithAnApprovedPlan()
    {
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        WritePlan(workspace, approved: true);

        Decision decision = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            EditEvent(Path.Combine(workspace.Path, "src", "Greenfield.Core", "Undeclared.cs"), workspace.Path));

        Assert.IsTrue(decision.Denied, "A file outside the declared list carries an approval it never had.");
        StringAssert.Contains(decision.Reason, "Undeclared.cs", StringComparison.Ordinal);
    }

    [TestMethod]
    public void DeclaredFileIsAllowedWithAnApprovedPlan()
    {
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        WritePlan(workspace, approved: true);

        Decision decision = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            EditEvent(Path.Combine(workspace.Path, "src", "Greenfield.Core", "Totals.cs"), workspace.Path));

        Assert.IsFalse(decision.Denied, $"A declared file under an approved plan must build: {decision.Reason}");
    }

    private static void WritePlan(FixtureWorkspace workspace, bool approved)
    {
        workspace.Run("checkout", "-q", "-b", $"cycle/{CycleId}");

        string cycleDir = Path.Combine(workspace.Path, ".sdlc", "cycles", CycleId);
        Directory.CreateDirectory(cycleDir);

        string approval = approved
            ? "approved_by: a person\napproved_at: 2026-09-27T12:00:00Z\n"
            : string.Empty;

        File.WriteAllText(Path.Combine(cycleDir, "plan.md"),
            $"""
            ---
            id: {CycleId}
            spec: .sdlc/cycles/{CycleId}/spec.md
            tier: local
            {approval}---

            # Plan - sum totals without overflowing

            ## Files

            | File | Change |
            |---|---|
            | `src/Greenfield.Core/Totals.cs` | use a checked accumulator |
            """);
    }
}
