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
