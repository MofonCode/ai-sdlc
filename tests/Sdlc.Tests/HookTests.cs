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
        using var workspace = FixtureWorkspace.Materialize("greenfield");

        string target = Path.Combine(workspace.Path, ".sdlc", "cycles", CycleId, "findings", "intent.r1.json");

        Decision decision = Invoke(Tool("Assert-DerivedArtifact.ps1"), EditEvent(target, workspace.Path));

        Assert.IsFalse(decision.Denied, $"Interrogation must be able to write its findings: {decision.Reason}");
    }

    [TestMethod]
    public void OverwritingAnExistingFindingsRoundIsRefused()
    {
        using var workspace = FixtureWorkspace.Materialize("greenfield");

        string dir = Path.Combine(workspace.Path, ".sdlc", "cycles", CycleId, "findings");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, "intent.r1.json");
        File.WriteAllText(target, "{}");

        Decision decision = Invoke(Tool("Assert-DerivedArtifact.ps1"), EditEvent(target, workspace.Path));

        Assert.IsTrue(decision.Denied, "Rounds are kept: the sequence is the evidence a repair worked.");
        StringAssert.Contains(decision.Reason, "round", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void OverwritingExistingEvidenceIsRefused()
    {
        using var workspace = FixtureWorkspace.Materialize("greenfield");

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

    // ------------------------------------------------------ settled artifacts

    [TestMethod]
    [DataRow("intent")]
    [DataRow("spec")]
    [DataRow("plan")]
    public void FindingsAgainstASettledArtifactAreRefused(string kind)
    {
        // A person has passed the gate, so a finding has nothing left to act on it - and producing one
        // anyway is how the review loop restarts on work that was already finished.
        //
        // One gate word across all three artifacts. There used to be two - accepted_by on an intent and
        // a spec, approved_by on a plan - which meant every reader and every regex had to know which
        // artifact used which, for one act that is the same act.
        const string gate = "approved";
        using var workspace = FixtureWorkspace.Materialize("greenfield");

        string cycleDir = Path.Combine(workspace.Path, ".sdlc", "cycles", CycleId);
        Directory.CreateDirectory(Path.Combine(cycleDir, "findings"));

        string[] frontmatter =
        [
            "---",
            $"id: {CycleId}",
            $"{gate}_by: Peter Almanzar",
            $"{gate}_at: 2026-09-28T09:15:00Z",
            "---",
            "",
            $"# {kind}",
        ];
        File.WriteAllLines(Path.Combine(cycleDir, $"{kind}.md"), frontmatter);

        Decision decision = Invoke(
            Tool("Assert-DerivedArtifact.ps1"),
            EditEvent(Path.Combine(cycleDir, "findings", $"{kind}.r1.json"), workspace.Path));

        Assert.IsTrue(decision.Denied, $"A settled {kind} must not be interrogated again.");
        StringAssert.Contains(decision.Reason, gate, StringComparison.Ordinal);
        StringAssert.Contains(decision.Reason, "stale", StringComparison.Ordinal);
    }

    [TestMethod]
    public void FindingsAgainstAnUnsettledArtifactAreAllowed()
    {
        // The happy path: approval always comes after interrogation, so this guard must never fire
        // during a normal cycle.
        using var workspace = FixtureWorkspace.Materialize("greenfield");

        string cycleDir = Path.Combine(workspace.Path, ".sdlc", "cycles", CycleId);
        Directory.CreateDirectory(Path.Combine(cycleDir, "findings"));
        File.WriteAllLines(Path.Combine(cycleDir, "spec.md"), ["---", $"id: {CycleId}", "---", "", "# spec"]);

        Decision decision = Invoke(
            Tool("Assert-DerivedArtifact.ps1"),
            EditEvent(Path.Combine(cycleDir, "findings", "spec.r1.json"), workspace.Path));

        Assert.IsFalse(decision.Denied, $"Interrogation before approval is the normal flow: {decision.Reason}");
    }

    // ------------------------------------------------------------------ artifact shape

    private static object WriteEvent(string filePath, string content, string cwd) => new
    {
        hook_event_name = "PreToolUse",
        tool_name = "Write",
        cwd,
        tool_input = new { file_path = filePath, content },
    };

    [TestMethod]
    public void TheVerifyDocumentTheTrialProducedIsRefused()
    {
        // Exactly what the first trial run wrote: id for cycle, passes_spent outside remediation, and a
        // criteria key the schema does not permit. Nothing caught it, and the state machine it was
        // supposed to encode quietly stopped being enforced.
        string content = """
            ---
            id: 20260928_8d33
            spec: .sdlc/cycles/20260928_8d33/spec.md
            plan: .sdlc/cycles/20260928_8d33/plan.md
            framework_version: 0.1.0
            verified_at: 2026-09-28T04:54:38Z
            outcome: blocked
            passes_spent: 0
            evidence:
            criteria:
            findings:
            ---

            # Verify
            """;

        Decision decision = Invoke(
            Tool("Assert-ArtifactShape.ps1"),
            WriteEvent("C:/repo/.sdlc/cycles/20260928_8d33/verify.md", content, "C:/repo"));

        Assert.IsTrue(decision.Denied, "A verify document using the wrong key names must be refused.");
        StringAssert.Contains(decision.Reason, "cycle", StringComparison.Ordinal);
        StringAssert.Contains(decision.Reason, "criteria", StringComparison.Ordinal);
    }

    [TestMethod]
    public void TheSolutionProfileTheTrialProducedIsRefused()
    {
        string content = """
            ---
            stack: dotnet
            origin: adopted
            adopted_at: da302b15622cc6a16aa2e617878cf30662b78f49
            adopted_on: 2026-09-28
            ---

            # dice-trial
            """;

        Decision decision = Invoke(
            Tool("Assert-ArtifactShape.ps1"),
            WriteEvent("C:/repo/.sdlc/solution.md", content, "C:/repo"));

        Assert.IsTrue(decision.Denied, "adopted_on is not a permitted key.");
        StringAssert.Contains(decision.Reason, "adopted_on", StringComparison.Ordinal);
    }

    [TestMethod]
    public void AWellFormedVerifyDocumentIsAllowed()
    {
        string content = """
            ---
            framework_version: 0.1.0
            cycle: 20260928_8d33
            spec: .sdlc/cycles/20260928_8d33/spec.md
            plan: .sdlc/cycles/20260928_8d33/plan.md
            verified_at: 2026-09-28T04:54:38Z
            outcome: pass
            remediation:
              passes_spent: 0
            evidence:
              build:
                path: .sdlc/cycles/20260928_8d33/evidence/build.log
            findings: []
            ---

            # Verify
            """;

        Decision decision = Invoke(
            Tool("Assert-ArtifactShape.ps1"),
            WriteEvent("C:/repo/.sdlc/cycles/20260928_8d33/verify.md", content, "C:/repo"));

        Assert.IsFalse(decision.Denied, $"A conforming document must pass: {decision.Reason}");
    }

    [TestMethod]
    public void MalformedFindingsJsonIsRefused()
    {
        Decision decision = Invoke(
            Tool("Assert-ArtifactShape.ps1"),
            WriteEvent($"C:/repo/.sdlc/cycles/{CycleId}/findings/spec.r1.json", "{ not json", "C:/repo"));

        Assert.IsTrue(decision.Denied, "A findings document that is not JSON cannot be read by anything.");
    }

    [TestMethod]
    public void AnUngovernedFileIsNotChecked()
    {
        // This hook runs on every write. Anything outside the governed artifact set must pass straight
        // through, or it becomes a tax on ordinary work and gets switched off.
        Decision decision = Invoke(
            Tool("Assert-ArtifactShape.ps1"),
            WriteEvent("C:/repo/src/Thing.cs", "namespace X;", "C:/repo"));

        Assert.IsFalse(decision.Denied, $"Source files are not governed artifacts: {decision.Reason}");
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

    // --------------------------------------------- writes that arrive through the shell

    [TestMethod]
    public void AShellRedirectToAnUndeclaredFileIsRefused()
    {
        // The defect this closes: skills write files with `cat > path <<EOF`, which is the Bash tool, so
        // an Edit|Write matcher never saw it. All three write gates were being routed around by the
        // default path every skill takes.
        using var workspace = FixtureWorkspace.Materialize("greenfield");
        WritePlan(workspace, approved: true);

        Decision decision = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            BashEvent("cat > src/Greenfield.Core/Sneaky.cs <<EOF\nclass X {}\nEOF", workspace.Path));

        Assert.IsTrue(decision.Denied, "A shell redirect to an undeclared file must be refused.");
        StringAssert.Contains(decision.Reason, "Sneaky.cs", StringComparison.Ordinal);
    }

    [TestMethod]
    public void AShellRedirectToADeclaredFileIsAllowed()
    {
        using var workspace = FixtureWorkspace.Materialize("greenfield");
        WritePlan(workspace, approved: true);

        Decision decision = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            BashEvent("cat > src/Greenfield.Core/Totals.cs <<EOF\nx\nEOF", workspace.Path));

        Assert.IsFalse(decision.Denied, $"A declared file must be writable by any means: {decision.Reason}");
    }

    [TestMethod]
    [DataRow("dotnet test dice.slnx 2>&1 | tail -30", "a descriptor redirect names no file")]
    [DataRow("echo hello > /dev/null", "the null device is not a file")]
    [DataRow("git log --oneline | head -5", "no redirect at all")]
    [DataRow("dotnet build 2>&1", "stderr to stdout")]
    public void AShellCommandThatWritesNoFileIsIgnored(string command, string why)
    {
        // An over-refusing hook gets switched off, and this one now runs on every Bash call in a governed
        // repository. Ordinary commands must pass straight through.
        using var workspace = FixtureWorkspace.Materialize("greenfield");
        WritePlan(workspace, approved: true);

        Decision decision = Invoke(Tool("Assert-DeclaredFile.ps1"), BashEvent(command, workspace.Path));

        Assert.IsFalse(decision.Denied, $"{why}: {decision.Reason}");
    }

    [TestMethod]
    [DataRow(
        "git commit -m @'\nrecord artifacts\n\nCo-Authored-By: Someone <noreply@example.com>\n'@",
        "@",
        "an attribution line's closing angle bracket, inside a PowerShell here-string")]
    [DataRow(
        "cat > src/Greenfield.Core/Totals.cs <<'EOF'\n<LangVersion>latest</LangVersion>\nEOF",
        "latest",
        "markup inside a heredoc body")]
    [DataRow(
        "echo \"generic List<int> and a > sign\"",
        "sign",
        "an angle bracket inside a double-quoted string")]
    public void AnAngleBracketInsideALiteralIsNotARedirect(string command, string phantom, string why)
    {
        // Both of the first two refused during a trial run, and neither names a file. The parser scanned
        // the raw command string, so any `>` looked like a redirect: an attribution line ending
        // `<noreply@...>` yielded a file called "@", and a heredoc carrying `<LangVersion>latest</...>`
        // yielded one called "latest". That is every commit and every markup file a cycle writes - it
        // refused twelve times in one build, and the caller started routing around the gate.
        using var workspace = FixtureWorkspace.Materialize("greenfield");
        WritePlan(workspace, approved: true);

        Decision decision = Invoke(Tool("Assert-DeclaredFile.ps1"), BashEvent(command, workspace.Path));

        Assert.IsFalse(
            decision.Denied,
            $"Refused over {why}, reporting a file named \"{phantom}\": {decision.Reason}");
    }

    [TestMethod]
    [DataRow("", "an unfilled paste of the approval block")]
    [DataRow("   ", "a name of spaces")]
    [DataRow("\"  \"", "a quoted name of spaces, which passed the old check and the schema's minLength")]
    public void AnApprovalWithNoNameIsNotAnApproval(string name, string why)
    {
        // The interrogation report prints the block with approved_by empty, because typing the name is
        // the act of approving. So an unfilled paste has to stop the cycle rather than pass it - the
        // field exists to record that a specific person decided.
        using var workspace = FixtureWorkspace.Materialize("greenfield");
        workspace.Run("checkout", "--quiet", "-b", $"cycle/{CycleId}");

        string cycleDir = Path.Combine(workspace.Path, ".sdlc", "cycles", CycleId);
        Directory.CreateDirectory(cycleDir);

        File.WriteAllLines(Path.Combine(cycleDir, "plan.md"),
        [
            "---",
            $"id: {CycleId}",
            "tier: local",
            "approved_by:" + (string.IsNullOrEmpty(name) ? string.Empty : " " + name),
            "approved_at: 2026-09-29T01:05:00Z",
            "---",
            "",
            "# Plan",
            "",
            "## Files",
            "",
            "| File | Change |",
            "|---|---|",
            "| `src/Greenfield.Core/Totals.cs` | the declared work |",
        ]);

        Decision decision = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            EditEvent(Path.Combine(workspace.Path, "src", "Greenfield.Core", "Totals.cs"), workspace.Path));

        Assert.IsTrue(decision.Denied, $"{why} must not count as approved.");
        StringAssert.Contains(decision.Reason, "not approved", StringComparison.Ordinal);
    }

    [TestMethod]
    public void AWriteOutsideTheRepositoryIsNotThePlansBusiness()
    {
        // This refused during a trial run and blocked `gh pr create --body-file`, whose body was staged
        // in the session scratchpad - a path with no relationship to the plan. A plan declares what a
        // change does to its own repository and says nothing about anywhere else.
        using var workspace = FixtureWorkspace.Materialize("greenfield");
        WritePlan(workspace, approved: true);

        string outside = Path.Combine(Path.GetTempPath(), "sdlc-outside-the-repo", "pr-body.md");

        Decision viaTool = Invoke(
            Tool("Assert-DeclaredFile.ps1"), EditEvent(outside, workspace.Path));

        Assert.IsFalse(viaTool.Denied, $"A write outside the repository must pass: {viaTool.Reason}");

        Decision viaShell = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            BashEvent($"gh pr create --body-file {outside.Replace('\\', '/')}", workspace.Path));

        Assert.IsFalse(viaShell.Denied, $"Nor may the shell path refuse it: {viaShell.Reason}");
    }

    [TestMethod]
    public void AnUndeclaredWriteInsideTheRepositoryIsStillRefused()
    {
        // The counterweight to the two tests above. Narrowing the parser and scoping it to the
        // repository must not have turned the gate off: a plain redirect to an undeclared path in the
        // repository still has to fail, or the fix traded false refusals for no enforcement.
        using var workspace = FixtureWorkspace.Materialize("greenfield");
        WritePlan(workspace, approved: true);

        Decision relative = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            BashEvent("echo x > src/Greenfield.Core/Sneaky.cs", workspace.Path));

        Assert.IsTrue(relative.Denied, "A redirect to an undeclared path must still be refused.");

        Decision absolute = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            EditEvent(Path.Combine(workspace.Path, "src", "Greenfield.Core", "Sneaky.cs"), workspace.Path));

        Assert.IsTrue(absolute.Denied, "Nor may an absolute path inside the repository slip through.");
    }

    [TestMethod]
    public void TheDeclaredListComesFromTheFilesTableAlone()
    {
        // The first version scanned the whole plan for any backticked token holding a slash or a dot, so
        // it believed net10.0, text/html, GET / and Random.Shared.Next(1, 7) were declared files. A gate
        // matching against that is not a gate. This plan carries exactly those fragments in its prose.
        using var workspace = FixtureWorkspace.Materialize("greenfield");
        workspace.Run("checkout", "-q", "-b", $"cycle/{CycleId}");

        string cycleDir = Path.Combine(workspace.Path, ".sdlc", "cycles", CycleId);
        Directory.CreateDirectory(cycleDir);

        string[] plan =
        [
            "---",
            $"id: {CycleId}",
            $"spec: .sdlc/cycles/{CycleId}/spec.md",
            "tier: local",
            "approved_by: a person",
            "approved_at: 2026-09-28T12:00:00Z",
            "---",
            "",
            "# Plan",
            "",
            "## Files",
            "",
            "| File | Change |",
            "|---|---|",
            "| `src/Greenfield.Core/Totals.cs` | seed the accumulation |",
            "",
            "## Proof",
            "",
            "- Targets `net10.0`, serves `text/html`, maps `GET /`, calls `Random.Shared.Next(1, 7)`",
            "- Output lands in `bin/` and `obj/`, sources under `src/`",
        ];
        File.WriteAllLines(Path.Combine(cycleDir, "plan.md"), plan);

        // Declared: allowed.
        Decision declared = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            EditEvent(Path.Combine(workspace.Path, "src", "Greenfield.Core", "Totals.cs"), workspace.Path));

        Assert.IsFalse(declared.Denied, $"The one file in the table must be allowed: {declared.Reason}");

        // A fragment from the prose is not a declared file, so a path resembling one is still refused.
        Decision fragment = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            EditEvent(Path.Combine(workspace.Path, "src", "Other.cs"), workspace.Path));

        Assert.IsTrue(fragment.Denied,
            "A path not in the Files table must be refused, however many backticked fragments the " +
            "plan's prose contains.");
    }

    // ------------------------------------------------------------------ landing

    [TestMethod]
    public void LandingIsNotConsideredOffACycleBranch()
    {
        using var workspace = FixtureWorkspace.Materialize("greenfield");

        Decision decision = Invoke(Tool("Assert-Landable.ps1"), BashEvent("gh pr create --fill", workspace.Path));

        Assert.IsFalse(decision.Denied, "Off a cycle branch this hook has no cycle to judge.");
    }

    [TestMethod]
    public void LandingWithoutAVerifyDocumentIsRefused()
    {
        using var workspace = FixtureWorkspace.Materialize("greenfield");
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
        using var workspace = FixtureWorkspace.Materialize("greenfield");
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
        using var workspace = FixtureWorkspace.Materialize("greenfield");
        workspace.Run("checkout", "-q", "-b", $"cycle/{CycleId}");

        Decision decision = Invoke(Tool("Assert-Landable.ps1"), BashEvent("gh pr list --state open", workspace.Path));

        Assert.IsFalse(decision.Denied, "Only creating a pull request is gated.");
    }

    // ------------------------------------------------------------------ declared files

    [TestMethod]
    public void SourceEditsAreNotGatedOffACycleBranch()
    {
        using var workspace = FixtureWorkspace.Materialize("greenfield");

        Decision decision = Invoke(
            Tool("Assert-DeclaredFile.ps1"),
            EditEvent(Path.Combine(workspace.Path, "src", "Greenfield.Core", "Totals.cs"), workspace.Path));

        Assert.IsFalse(decision.Denied,
            "Ordinary work in a repository that happens to be governed must be unaffected.");
    }

    [TestMethod]
    public void SourceEditWithNoPlanIsRefused()
    {
        using var workspace = FixtureWorkspace.Materialize("greenfield");
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
        using var workspace = FixtureWorkspace.Materialize("greenfield");
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
        using var workspace = FixtureWorkspace.Materialize("greenfield");
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
        using var workspace = FixtureWorkspace.Materialize("greenfield");
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
