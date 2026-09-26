using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// The INV-08 gate: an edit to the charter or a pack outside a charter-review branch is refused.
/// </summary>
/// <remarks>
/// <b>INV-08 was the only invariant enforced by asking nicely.</b> Every other rule is enforced
/// by being citable — a finding points at it, earns a citation, and the cycle halts. INV-08
/// forbids amending the charter, the stack pack or the pack rules outside an approved charter
/// intent, and nothing checked it. The rule was repeated in three skills as prose, and prose is
/// what an agent is asked to follow rather than what it is prevented from breaking.
/// <para>
/// <b>These tests assert the REFUSALS.</b> That is the design record's instruction and it is the
/// right one: a protection test that only proves the happy path proves nothing. Every row of the
/// fail-closed table below gets its own test, because each is a different way for the gate to
/// stop working silently.
/// </para>
/// <para>
/// <b>The gate must never answer <c>allow</c>.</b> A PreToolUse hook returning
/// <c>permissionDecision: "allow"</c> auto-approves the call and skips the checks that would
/// otherwise apply — so granting blanket approval to every Write in the repository, as a side
/// effect of protecting one file, would open a far larger hole than the one being closed. When
/// it has no objection the gate prints nothing. <see cref="ItNeverGrantsBlanketApproval"/> is
/// the test that holds that line.
/// </para>
/// </remarks>
[TestClass]
public sealed class CharterGateTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Assert-SdlcCharterGate.ps1");

    private static string? _repo;

    [ClassInitialize]
    public static void CreateFixtureRepo(TestContext context)
    {
        _repo = Path.Combine(Path.GetTempPath(), "sdlc-gate-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "intents"));
        Directory.CreateDirectory(Path.Combine(_repo, "packs", "dotnet-library"));
        Directory.CreateDirectory(Path.Combine(_repo, "src"));

        File.WriteAllText(Path.Combine(_repo, ".sdlc", "CHARTER.md"), "# Charter\n");
        File.WriteAllText(Path.Combine(_repo, "packs", "dotnet-library", "pack.yaml"), "name: fixture\n");
        File.WriteAllText(Path.Combine(_repo, "packs", "dotnet-library", "pack.rules.yaml"), "rules: []\n");
        File.WriteAllText(Path.Combine(_repo, "packs", "dotnet-library", "CHARTER.md"), "# Template\n");
        File.WriteAllText(Path.Combine(_repo, "src", "Program.cs"), "// ordinary code\n");

        static string Intent(string id, string source) =>
            $"---\nid: {id}\nsource: {source}\ncharter-version: 1\n---\n\n# Intent {id}\n";

        string intents = Path.Combine(_repo, ".sdlc", "intents");
        File.WriteAllText(Path.Combine(intents, "20260925-c001.md"), Intent("20260925-c001", "charter-review"));
        File.WriteAllText(Path.Combine(intents, "20260925-p001.md"), Intent("20260925-p001", "pack-review"));
        File.WriteAllText(Path.Combine(intents, "20260925-a001.md"), Intent("20260925-a001", "authored"));

        // An intent with no `source:` at all, which must deny rather than be read as permissive.
        File.WriteAllText(
            Path.Combine(intents, "20260925-n001.md"),
            "---\nid: 20260925-n001\ncharter-version: 1\n---\n\n# No source\n");

        Git("init --initial-branch=master");
        Git("config user.email t@example.invalid");
        Git("config user.name Test");
        Git("add -A");
        Git("-c user.email=t@example.invalid -c user.name=Test commit -m fixture");

        Git("branch intent-20260925-c001");
        Git("branch intent-20260925-p001");
        Git("branch cycle-20260925-a001");
        Git("branch intent-20260925-n001");
        Git("branch intent-20260925-missing");
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

    private static void Git(string args)
    {
        using Process p = Process.Start(new ProcessStartInfo("git", args)
        {
            WorkingDirectory = _repo!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;

        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        Assert.AreEqual(0, p.ExitCode, $"git {args} failed: {stderr}");
    }

    private static void Checkout(string branch) => Git("checkout --quiet " + branch);

    /// <summary>Runs the gate with a raw payload and returns whatever it printed.</summary>
    private static string RunGateRaw(string payloadJson)
    {
        string payloadFile = Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(payloadFile, payloadJson, new UTF8Encoding(false));

        try
        {
            using Process p = Process.Start(new ProcessStartInfo("powershell.exe")
            {
                ArgumentList =
                {
                    "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                    "-File", ScriptPath, "-PayloadPath", payloadFile,
                },
                WorkingDirectory = _repo!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            })!;

            string stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            // Exit 0 always. A non-zero exit from a hook means something else to Claude Code,
            // and a gate that crashes rather than denying is the failure this is built against.
            Assert.AreEqual(0, p.ExitCode, "the gate must always exit 0 and express itself in JSON");
            return stdout.Trim();
        }
        finally
        {
            File.Delete(payloadFile);
        }
    }

    /// <summary>The fixture repo path in the form a hook payload carries it.</summary>
    private static string RepoCwd => _repo!.Replace('\\', '/');

    private static string Payload(string toolName, string pathField, string path) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = toolName,
            ["cwd"] = RepoCwd,
            ["tool_input"] = new Dictionary<string, object> { [pathField] = path },
        });

    /// <summary>The refusal reason, or null when the gate had no objection.</summary>
    private static string? DenialReason(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        using JsonDocument doc = JsonDocument.Parse(output);
        JsonElement specific = doc.RootElement.GetProperty("hookSpecificOutput");

        Assert.AreEqual("PreToolUse", specific.GetProperty("hookEventName").GetString());
        Assert.AreEqual("deny", specific.GetProperty("permissionDecision").GetString(),
            "the only decision this gate may express is deny");

        return specific.GetProperty("permissionDecisionReason").GetString();
    }

    private static string AssertDenied(string output)
    {
        string? reason = DenialReason(output);
        Assert.IsNotNull(reason, "expected a refusal, got silence");
        Assert.IsTrue(reason!.Contains("INV-08", StringComparison.Ordinal),
            "a refusal must name the rule it is enforcing: " + reason);
        return reason;
    }

    private static void AssertNoObjection(string output) =>
        Assert.AreEqual(string.Empty, output,
            "the gate must stay silent when it has no objection, never answer allow");

    // --- the protected paths ------------------------------------------------

    [TestMethod]
    [DataRow(".sdlc/CHARTER.md")]
    [DataRow("packs/dotnet-library/pack.yaml")]
    [DataRow("packs/dotnet-library/pack.rules.yaml")]
    [DataRow("packs/dotnet-library/CHARTER.md")]
    [TestProperty("AC", "AC-20260926-46a0-9")]
    public void EachProtectedPathIsRefusedOnMaster(string path)
    {
        // The fourth is a judgement worth recording. INV-08's wording is "this document, the
        // stack pack, or the pack rules", and a pack's charter TEMPLATE governs nothing until a
        // solution copies it — so a narrow reading would leave it out. It is in scope because
        // every solution created from that pack inherits it: an ungated edit changes the rules
        // every future solution is judged by.
        Checkout("master");
        string reason = AssertDenied(RunGateRaw(Payload("Edit", "file_path", path)));
        StringAssert.Contains(reason, path, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AnOrdinarySourceFileIsNotThisGatesBusiness()
    {
        // The gate has an opinion about four paths. Everything else must pass without comment,
        // or it becomes a permission prompt on every edit in the repository and gets disabled
        // within a day — which is the INV-06 erosion failure this framework is built around.
        Checkout("master");
        AssertNoObjection(RunGateRaw(Payload("Write", "file_path", "src/Program.cs")));
    }

    [TestMethod]
    public void AnAbsolutePathToAProtectedFileIsStillRefused()
    {
        // Tools pass absolute paths in practice. Matching only the relative form would leave the
        // gate trivially avoidable without anyone intending to avoid it.
        Checkout("master");
        string absolute = Path.Combine(_repo!, ".sdlc", "CHARTER.md");
        AssertDenied(RunGateRaw(Payload("Edit", "file_path", absolute)));
    }

    [TestMethod]
    public void AFileOutsideTheProjectIsNotThisGatesBusiness()
    {
        Checkout("master");
        AssertNoObjection(RunGateRaw(Payload("Write", "file_path", "C:/Windows/Temp/unrelated.md")));
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-9")]
    public void ANotebookPathIsCheckedToo()
    {
        // NotebookEdit carries `notebook_path`, not `file_path`. A gate that reads one field name
        // and assumes it covers every writing tool has a hole shaped exactly like the next tool.
        Checkout("master");
        AssertDenied(RunGateRaw(Payload("NotebookEdit", "notebook_path", ".sdlc/CHARTER.md")));
    }

    [TestMethod]
    public void AToolThatDoesNotWriteFilesIsIgnored()
    {
        Checkout("master");
        AssertNoObjection(RunGateRaw(Payload("Read", "file_path", ".sdlc/CHARTER.md")));
    }

    // --- the branch has to earn it -----------------------------------------

    [TestMethod]
    public void ACharterReviewBranchMayAmendTheCharter()
    {
        // The one permitted case, and the reason the gate is a gate rather than a ban.
        Checkout("intent-20260925-c001");
        AssertNoObjection(RunGateRaw(Payload("Edit", "file_path", ".sdlc/CHARTER.md")));
    }

    [TestMethod]
    public void APackReviewBranchMayAmendAPack()
    {
        Checkout("intent-20260925-p001");
        AssertNoObjection(RunGateRaw(Payload("Edit", "file_path", "packs/dotnet-library/pack.rules.yaml")));
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-26")]
    public void AnOrdinaryCycleBranchMayNot()
    {
        // The case the invariant exists for: a cycle that edits its way around an invariant
        // leaves no finding behind. The branch is well-formed and its intent is real — it is the
        // SOURCE that decides, not the branch shape.
        Checkout("cycle-20260925-a001");
        string reason = AssertDenied(RunGateRaw(Payload("Edit", "file_path", ".sdlc/CHARTER.md")));
        StringAssert.Contains(reason, "authored", StringComparison.Ordinal);
    }

    // --- fail closed, every row of the table -------------------------------

    [TestMethod]
    public void ADetachedHeadIsRefused()
    {
        // Fail closed: no branch means no intent, and an edit no intent governs is exactly what
        // INV-08 forbids.
        Git("checkout --quiet --detach master");
        try
        {
            string reason = AssertDenied(RunGateRaw(Payload("Edit", "file_path", ".sdlc/CHARTER.md")));
            StringAssert.Contains(reason, "detached", StringComparison.Ordinal);
        }
        finally
        {
            Checkout("master");
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-24")]
    public void AMissingIntentFileIsRefused()
    {
        // The branch looks right and there is nothing behind it. Reading a well-formed branch
        // name as authorisation would make the gate bypassable by `git checkout -b intent-x`.
        Checkout("intent-20260925-missing");
        string reason = AssertDenied(RunGateRaw(Payload("Edit", "file_path", ".sdlc/CHARTER.md")));
        StringAssert.Contains(reason, "no intent file", StringComparison.Ordinal);
    }

    [TestMethod]
    public void AnIntentWithNoDeclaredSourceIsRefused()
    {
        // An absent field is not permission. Treating a missing `source:` as acceptable would
        // make the weakest possible intent the most powerful one.
        Checkout("intent-20260925-n001");
        string reason = AssertDenied(RunGateRaw(Payload("Edit", "file_path", ".sdlc/CHARTER.md")));
        StringAssert.Contains(reason, "no source", StringComparison.Ordinal);
    }

    [TestMethod]
    public void AnEmptyPayloadIsRefused()
    {
        Checkout("intent-20260925-c001");
        string reason = AssertDenied(RunGateRaw(string.Empty));
        StringAssert.Contains(reason, "empty", StringComparison.Ordinal);
    }

    [TestMethod]
    public void AnUnparseablePayloadIsRefused()
    {
        // The gate cannot identify the target, so it cannot show the edit is safe. Note this
        // runs on a PERMITTED branch: the refusal must come from the broken input, not from
        // where it happens to be standing.
        Checkout("intent-20260925-c001");
        string reason = AssertDenied(RunGateRaw("{ this is not json"));
        StringAssert.Contains(reason, "the gate itself failed", StringComparison.Ordinal);
    }

    [TestMethod]
    public void APayloadWithNoWorkingDirectoryIsRefused()
    {
        Checkout("intent-20260925-c001");
        string payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = "Edit",
            ["tool_input"] = new Dictionary<string, object> { ["file_path"] = ".sdlc/CHARTER.md" },
        });

        AssertDenied(RunGateRaw(payload));
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-9")]
    public void AWritingToolWithNoRecognisedPathIsRefused()
    {
        // A writing tool whose shape this gate does not know. It cannot check the edit, so it
        // does not pretend to — the alternative is a gate that silently stops covering new
        // tools as they are added.
        Checkout("master");
        string payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = "Write",
            ["cwd"] = RepoCwd,
            ["tool_input"] = new Dictionary<string, object> { ["contents"] = "no path at all" },
        });

        string reason = AssertDenied(RunGateRaw(payload));
        StringAssert.Contains(reason, "no file path", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ABatchEditIsCheckedEntryByEntry()
    {
        // One innocuous entry and one protected one. Checking only the first, or only the
        // top-level path, would let a batch carry a charter edit through unexamined.
        Checkout("master");
        string payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = "MultiEdit",
            ["cwd"] = RepoCwd,
            ["tool_input"] = new Dictionary<string, object>
            {
                ["edits"] = new[]
                {
                    new Dictionary<string, object> { ["file_path"] = "src/Program.cs" },
                    new Dictionary<string, object> { ["file_path"] = ".sdlc/CHARTER.md" },
                },
            },
        });

        // ASSERT THE REASON, NOT JUST THE REFUSAL. Mutation Z6 — stop reading the batch's own
        // list — passed all twenty-one tests, because with no path recognised the gate falls to
        // its "no file path this gate recognises" refusal. It still denied, for entirely the
        // wrong reason, and a test that only checks for denial cannot tell the difference. On a
        // permitted branch that mutation would have let the charter edit straight through.
        string reason = AssertDenied(RunGateRaw(payload));
        StringAssert.Contains(reason, ".sdlc/CHARTER.md", StringComparison.Ordinal,
            "the refusal must name the protected path it found inside the batch");
    }

    [TestMethod]
    public void ABatchEditOnAPermittedBranchIsAllowedThroughOnItsOwnMerits()
    {
        // The half that makes the assertion above load-bearing: on a charter-review branch the
        // same batch must pass. Without this, "deny everything with an edits list" would satisfy
        // the test above and still be wrong.
        Checkout("intent-20260925-c001");
        string payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = "MultiEdit",
            ["cwd"] = RepoCwd,
            ["tool_input"] = new Dictionary<string, object>
            {
                ["edits"] = new[]
                {
                    new Dictionary<string, object> { ["file_path"] = "src/Program.cs" },
                    new Dictionary<string, object> { ["file_path"] = ".sdlc/CHARTER.md" },
                },
            },
        });

        AssertNoObjection(RunGateRaw(payload));
    }

    // --- the shell route ----------------------------------------------------
    //
    // The hole this cycle closes. The gate once watched four file-writing tools, so a shell
    // command walked past it - the rule was enforced against the tools that announce a path and
    // not against the one that hides it in a string.
    //
    // Every case here feeds the gate a payload directly, which is how this whole file already
    // works and why it can test a decision the harness may never deliver for PowerShell.

    private static string ShellPayload(string toolName, string command) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = toolName,
            ["cwd"] = RepoCwd,
            ["tool_input"] = new Dictionary<string, object> { ["command"] = command },
        });

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-1")]
    [DataRow("Bash")]
    [DataRow("PowerShell")]
    public void AShellWriteToTheCharterIsRefusedWithoutALicence(string tool)
    {
        Checkout("master");
        string reason = AssertDenied(RunGateRaw(ShellPayload(tool, "echo x > .sdlc/CHARTER.md")));

        StringAssert.Contains(reason, ".sdlc/CHARTER.md", StringComparison.Ordinal,
            "a refusal has to name the path, or nobody can act on it");
        StringAssert.Contains(reason, "charter-review", StringComparison.Ordinal,
            "a refusal has to say what would permit the write");
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-2")]
    public void AShellWriteToTheCharterIsPermittedOnACharterReviewBranch()
    {
        Checkout("intent-20260925-c001");
        AssertNoObjection(RunGateRaw(ShellPayload("Bash", "echo x > .sdlc/CHARTER.md")));
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-3")]
    [DataRow("packs/dotnet-library/pack.yaml")]
    [DataRow("packs/dotnet-library/pack.rules.yaml")]
    [DataRow("packs/dotnet-library/CHARTER.md")]
    public void AShellWriteToAPackFileIsPermittedOnAPackReviewBranch(string path)
    {
        Checkout("intent-20260925-p001");
        AssertNoObjection(RunGateRaw(ShellPayload("Bash", $"echo x > {path}")));
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-4")]
    public void ALicenceIsBoundToItsPathAndDoesNotCrossOver()
    {
        // Before the shared module the gate held ONE flat list of permitted sources, so either
        // source licensed every protected path. A pack-review intent has no business rewriting a
        // solution's charter, and a charter-review intent has none rewriting the rules every
        // future solution inherits.
        Checkout("intent-20260925-p001");
        string charterReason = AssertDenied(RunGateRaw(Payload("Edit", "file_path", ".sdlc/CHARTER.md")));
        StringAssert.Contains(charterReason, "charter-review", StringComparison.Ordinal);

        Checkout("intent-20260925-c001");
        string packReason = AssertDenied(RunGateRaw(Payload("Edit", "file_path", "packs/dotnet-library/pack.yaml")));
        StringAssert.Contains(packReason, "pack-review", StringComparison.Ordinal);
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-5")]
    [DataRow("cat .sdlc/CHARTER.md")]
    [DataRow("grep -n \"INV-08\" .sdlc/CHARTER.md")]
    [DataRow("grep -n \"INV\" .sdlc/CHARTER.md > notes.txt")]
    public void AShellCommandThatOnlyReadsAProtectedPathIsPermitted(string command)
    {
        // Reading is not changing. The third case matters most: a redirection is present, but its
        // TARGET is not the protected path, and an earlier version of the classifier denied it.
        Checkout("master");
        AssertNoObjection(RunGateRaw(ShellPayload("Bash", command)));
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-6")]
    [DataRow("echo hello > src/Program.cs")]
    [DataRow("rm -rf build")]
    [DataRow("dotnet test")]
    [DataRow("git commit -m \"a message mentioning the charter\"")]
    public void AShellCommandNamingNoProtectedPathIsPermitted(string command)
    {
        // This gate has an opinion about four files. A command touching none of them is not its
        // business, whatever else it does - otherwise widening the matcher would turn the gate
        // into a block on the shell.
        Checkout("master");
        AssertNoObjection(RunGateRaw(ShellPayload("Bash", command)));
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-7")]
    [DataRow("X=.sdlc/CHARTER.md; echo hi > $X")]
    [DataRow("echo \"unbalanced > .sdlc/CHARTER.md")]
    [DataRow("eval \"cat .sdlc/CHARTER.md > /dev/null\"")]
    public void AShellCommandTheGateCannotEvaluateIsRefused(string command)
    {
        // COULD-NOT-EVALUATE IS NOT NO-WRITE-FOUND. Evaluating a command and finding no write is a
        // determination, and permitting it is the accepted leak. Failing to evaluate it at all is
        // no determination, so it denies - the same reason a thrown gate denies.
        Checkout("master");
        string reason = AssertDenied(RunGateRaw(ShellPayload("Bash", command)));
        StringAssert.Contains(reason, "could not evaluate", StringComparison.Ordinal,
            "the refusal has to say it could not evaluate, not imply a write was found: " + reason);
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-7")]
    public void AnUnreadableCommandIsPermittedOnTheBranchThatHoldsTheLicence()
    {
        // Fail closed means denying when permission cannot be ESTABLISHED. On a charter-review
        // branch it has been, for this path - so refusing a command the gate cannot parse there is
        // friction with no safety behind it, since the same author could make the same edit with
        // Edit and be permitted. The deny stays for every branch with no licence.
        Checkout("intent-20260925-c001");
        AssertNoObjection(RunGateRaw(ShellPayload("Bash", "sed -i \"s/$OLD/new/\" .sdlc/CHARTER.md")));
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-22")]
    [DataRow("rm .sdlc/CHARTER.md")]
    [DataRow("mv .sdlc/CHARTER.md .sdlc/CHARTER.old.md")]
    [DataRow("Remove-Item .sdlc/CHARTER.md")]
    [DataRow("truncate -s 0 .sdlc/CHARTER.md")]
    public void DestroyingAProtectedPathIsRefusedLikeAnyOtherWrite(string command)
    {
        // DESTRUCTION IS A WRITE SHAPE, NOT THE ABSENCE OF ONE. Removing, renaming, moving or
        // truncating a protected path is the largest change available to it, and the first version
        // of the classifier recognised only writes that produce content - so a command deleting
        // the charter would have been permitted on an unlicensed branch.
        Checkout("master");
        string reason = AssertDenied(RunGateRaw(ShellPayload("Bash", command)));
        StringAssert.Contains(reason, ".sdlc/CHARTER.md", StringComparison.Ordinal);
    }

    // --- creation is not modification ---------------------------------------

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-8")]
    public void CreatingAProtectedPathThatDoesNotExistYetNeedsNoLicence()
    {
        // You cannot loosen a rule that has not been written. Creating a new pack is an ordinary
        // cycle, and `init` writing a first charter into an ungoverned repository is the same
        // shape.
        Checkout("master");
        AssertNoObjection(RunGateRaw(Payload("Write", "file_path", "packs/brand-new/pack.yaml")));
        AssertNoObjection(RunGateRaw(ShellPayload("Bash", "echo 'name: new' > packs/brand-new/pack.yaml")));
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-8")]
    public void AFirstCharterInARepositoryWithNoCommitIsPermitted()
    {
        // The case that decided the mechanic: existence is tested ON DISK rather than against a
        // git baseline, because this repository has no commit to compare against - which is
        // exactly where `init` runs. An earlier version tested the baseline and denied it.
        string fresh = Path.Combine(Path.GetTempPath(), "sdlc-fresh-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(fresh, ".sdlc"));

        try
        {
            RunGitIn(fresh, "init --initial-branch=master");

            string payload = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["hook_event_name"] = "PreToolUse",
                ["tool_name"] = "Write",
                ["cwd"] = fresh.Replace('\\', '/'),
                ["tool_input"] = new Dictionary<string, object> { ["file_path"] = ".sdlc/CHARTER.md" },
            });

            AssertNoObjection(RunGateInRaw(fresh, payload));
        }
        finally
        {
            TryDelete(fresh);
        }
    }

    // --- the shared licence answer ------------------------------------------

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-14")]
    public void BothMechanismsResolveTheLicenceThroughOneSharedFunction()
    {
        // A PREVENTION AND A DETECTION THAT DISAGREE ARE WORSE THAN EITHER ALONE: the gate looks
        // stricter than it is and the check reports paths nobody guards. Asserted on the source
        // because the alternative - comparing two behaviours across every case - is the drift
        // this is meant to prevent, discovered late.
        string gate = File.ReadAllText(ScriptPath);
        string facts = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "tools", "Get-SdlcFacts.ps1"));

        foreach ((string name, string code) in new[] { ("the gate", gate), ("the fact-gatherer", facts) })
        {
            StringAssert.Contains(code, "SdlcProtectedPaths.ps1", StringComparison.Ordinal,
                $"{name} must resolve the licence through the shared module");
            StringAssert.Contains(code, "Test-SdlcWriteLicensed", StringComparison.Ordinal,
                $"{name} must ask the shared function, not its own copy of the rule");
        }

        Assert.IsFalse(gate.Contains("$permittedSources", StringComparison.Ordinal),
            "the gate's own flat list of permitted sources is what path-binding replaced");
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-23")]
    public void WhenTheSharedModuleCannotBeLoadedTheGateDenies()
    {
        // The reason the module is dot-sourced INSIDE the gate's try/catch. A load failure above
        // it is an error the script never sees, which Claude Code reads as no opinion - a
        // fail-open in the one component that must fail closed.
        string sandbox = Path.Combine(Path.GetTempPath(), "sdlc-noload-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(sandbox);

        try
        {
            string gateCopy = Path.Combine(sandbox, "Assert-SdlcCharterGate.ps1");
            File.Copy(ScriptPath, gateCopy);

            // Everything the gate needs EXCEPT the licence module, which is the condition under
            // test. A sandbox missing every helper would prove only that a broken install denies.
            string moduleName = "SdlcProtectedPaths.ps1";
            foreach (string helper in Directory.EnumerateFiles(
                Path.Combine(AppContext.BaseDirectory, "tools"), "Sdlc*.ps1"))
            {
                if (Path.GetFileName(helper).Equals(moduleName, StringComparison.Ordinal))
                {
                    continue;
                }

                File.Copy(helper, Path.Combine(sandbox, Path.GetFileName(helper)), overwrite: true);
            }

            string payloadFile = Path.Combine(sandbox, "payload.json");
            File.WriteAllText(payloadFile, Payload("Edit", "file_path", ".sdlc/CHARTER.md"), new UTF8Encoding(false));

            using Process p = Process.Start(new ProcessStartInfo("powershell.exe")
            {
                ArgumentList =
                {
                    "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                    "-File", gateCopy, "-PayloadPath", payloadFile,
                },
                WorkingDirectory = _repo!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            })!;

            string stdout = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();

            Assert.AreEqual(0, p.ExitCode, "even a broken gate must exit 0 and express itself in JSON");
            AssertDenied(stdout);
        }
        finally
        {
            TryDelete(sandbox);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-46a0-25")]
    public void AnIntentWithUnreadableFrontmatterIsRefused()
    {
        // An intent file with no frontmatter at all declares nothing, and nothing is not a
        // licence.
        string intents = Path.Combine(_repo!, ".sdlc", "intents");
        File.WriteAllText(Path.Combine(intents, "20260925-b001.md"), "# no frontmatter here\n");
        Git("branch --force intent-20260925-b001 master");
        Checkout("intent-20260925-b001");

        string reason = AssertDenied(RunGateRaw(Payload("Edit", "file_path", ".sdlc/CHARTER.md")));
        StringAssert.Contains(reason, "frontmatter", StringComparison.Ordinal);
    }

    private static void RunGitIn(string root, string args)
    {
        using Process p = Process.Start(new ProcessStartInfo("git", args)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;

        p.StandardError.ReadToEnd();
        p.WaitForExit();
    }

    private static string RunGateInRaw(string root, string payloadJson)
    {
        string payloadFile = Path.Combine(root, "payload.json");
        File.WriteAllText(payloadFile, payloadJson, new UTF8Encoding(false));

        using Process p = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            ArgumentList =
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", ScriptPath, "-PayloadPath", payloadFile,
            },
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        Assert.AreEqual(0, p.ExitCode, "the gate must always exit 0 and express itself in JSON");
        return stdout.Trim();
    }

    private static void TryDelete(string dir)
    {
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

    [TestMethod]
    public void ItNeverGrantsBlanketApproval()
    {
        // THE LINE THAT MUST NOT MOVE. Answering `allow` would auto-approve the tool call and
        // skip every check that would otherwise apply — so a gate protecting four files would
        // be handing out permission for every Write in the repository. Silence is the only
        // correct non-objection.
        Checkout("intent-20260925-c001");

        foreach (string path in new[] { "src/Program.cs", ".sdlc/CHARTER.md" })
        {
            string output = RunGateRaw(Payload("Write", "file_path", path));
            Assert.IsFalse(output.Contains("\"allow\"", StringComparison.Ordinal),
                $"the gate answered allow for {path}, which auto-approves and skips other checks");
        }
    }
}
