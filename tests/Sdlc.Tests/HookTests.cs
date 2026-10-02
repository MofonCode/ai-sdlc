using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;
using Sdlc.Core.Hooks;

namespace Sdlc.Tests;

/// <summary>
/// The hook, exercised against each host's payload shape.
/// </summary>
/// <remarks>
/// The payloads here are the shapes the hosts document. They are a conformance suite rather than unit
/// tests of a parser: when a host renames a field, this is what fails, and failing here is the cheap
/// way to find out rather than discovering that a rule quietly stopped applying.
/// </remarks>
[TestClass]
public sealed class HookTests
{
    private const string ApprovalPath = "C:/solution/.sdlc/cycles/20260929_1456_32/approval.md";

    [TestMethod]
    [DataRow("claude")]
    [DataRow("claude-code")]
    [DataRow("Claude")]
    [DataRow("copilot")]
    [DataRow("github-copilot")]
    [DataRow("COPILOT")]
    public void A_known_host_is_recognised_however_it_is_spelled(string spelling)
    {
        Assert.IsTrue(HookProtocol.TryParseHost(spelling, out HookHost? host));
        Assert.IsNotNull(host);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    [DataRow("gemini")]
    [DataRow("codex")]
    public void A_host_that_is_not_supported_yet_is_not_recognised(string? spelling)
    {
        // Gemini, Codex and Antigravity all have a blocking pre-tool hook, so they are adapters waiting
        // to be written rather than hosts that cannot work. Claiming them before they are tested would
        // be the "looks supported and is not" failure.
        Assert.IsFalse(HookProtocol.TryParseHost(spelling, out HookHost? host));
        Assert.IsNull(host);
    }

    [TestMethod]
    public void A_claude_edit_payload_reads_as_a_file_write_with_its_path()
    {
        const string payload = """
            {
              "session_id": "abc123",
              "transcript_path": "/tmp/transcript.jsonl",
              "cwd": "C:/solution",
              "hook_event_name": "PreToolUse",
              "tool_name": "Edit",
              "tool_input": {
                "file_path": "C:/solution/src/Program.cs",
                "old_string": "a",
                "new_string": "b"
              }
            }
            """;

        ToolCall call = Read(payload, HookHost.Claude);

        Assert.AreEqual("Edit", call.ToolName);
        Assert.AreEqual(ToolKind.FileWrite, call.Kind);
        CollectionAssert.AreEqual(new[] { "C:/solution/src/Program.cs" }, call.Paths.ToArray());
        Assert.AreEqual("C:/solution", call.WorkingDirectory);
    }

    [TestMethod]
    public void A_copilot_payload_reads_the_same_way_despite_naming_the_path_differently()
    {
        const string payload = """
            {
              "hook_event_name": "preToolUse",
              "session_id": "x",
              "timestamp": "2026-10-01T00:00:00Z",
              "cwd": "/solution",
              "tool_name": "edit",
              "tool_input": { "path": "src/Program.cs" }
            }
            """;

        ToolCall call = Read(payload, HookHost.Copilot);

        Assert.AreEqual(ToolKind.FileWrite, call.Kind);
        CollectionAssert.AreEqual(new[] { "src/Program.cs" }, call.Paths.ToArray());
    }

    [TestMethod]
    public void A_shell_payload_reads_as_a_shell_call_with_its_command()
    {
        ToolCall call = Read(
            """{ "tool_name": "Bash", "tool_input": { "command": "dotnet build" } }""",
            HookHost.Claude);

        Assert.AreEqual(ToolKind.Shell, call.Kind);
        Assert.AreEqual("dotnet build", call.Command);
    }

    [TestMethod]
    public void A_tool_the_reader_does_not_recognise_is_still_classified_by_what_it_carries()
    {
        // A host that adds a tool, or renames one, must not silently fall out of the rules.
        ToolCall withCommand = Read(
            """{ "tool_name": "some_future_tool", "tool_input": { "command": "rm -rf ." } }""",
            HookHost.Claude);

        Assert.AreEqual(ToolKind.Shell, withCommand.Kind);
    }

    [TestMethod]
    public void A_read_only_tool_is_harmless()
    {
        ToolCall call = Read(
            """{ "tool_name": "Read", "tool_input": { "file_path": "src/Program.cs" } }""",
            HookHost.Claude);

        Assert.AreEqual(ToolKind.Harmless, call.Kind);
        Assert.AreEqual(HookDecision.Allow, HookPolicyHandler.Decide(call).Decision);
    }

    [TestMethod]
    public void Several_paths_in_one_call_are_all_collected()
    {
        ToolCall call = Read(
            """
            {
              "tool_name": "MultiEdit",
              "tool_input": {
                "file_path": "a.cs",
                "edits": [ { "file_path": "b.cs" }, { "file_path": "c.cs" } ]
              }
            }
            """,
            HookHost.Claude);

        CollectionAssert.AreEquivalent(new[] { "a.cs", "b.cs", "c.cs" }, call.Paths.ToArray());
    }

    [TestMethod]
    public void Writing_an_approval_ledger_is_denied_and_the_refusal_says_what_to_do_instead()
    {
        ToolCall call = Read(
            $$"""{ "tool_name": "Write", "tool_input": { "file_path": "{{ApprovalPath}}" } }""",
            HookHost.Claude);

        HookVerdict verdict = HookPolicyHandler.Decide(call);

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(verdict.Reason, "no agent may write it");
        StringAssert.Contains(verdict.Reason, "sdlc draft approval");
    }

    [TestMethod]
    public void Writing_an_abandonment_record_is_denied()
    {
        ToolCall call = Read(
            """{ "tool_name": "Write", "tool_input": { "file_path": ".sdlc/cycles/20260929_1456_32/abandoned.md" } }""",
            HookHost.Claude);

        Assert.AreEqual(HookDecision.Deny, HookPolicyHandler.Decide(call).Decision);
    }

    [TestMethod]
    public void Writing_the_solution_constraints_is_denied()
    {
        ToolCall call = Read(
            """{ "tool_name": "Edit", "tool_input": { "file_path": ".sdlc/constraints.md" } }""",
            HookHost.Claude);

        HookVerdict verdict = HookPolicyHandler.Decide(call);

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(verdict.Reason, "deadlock");
    }

    [TestMethod]
    public void Writing_the_solution_file_is_denied()
    {
        ToolCall call = Read(
            """{ "tool_name": "Edit", "tool_input": { "file_path": ".sdlc/solution.md" } }""",
            HookHost.Claude);

        HookVerdict verdict = HookPolicyHandler.Decide(call);

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(
            verdict.Reason,
            "draft profile",
            "A refusal has to name the way through, or protecting the file just blocks a migration.");
    }

    [TestMethod]
    public void Every_protected_file_is_denied_through_both_rules()
    {
        // The two rules have disagreed before: the shell rule passed a bare file name into the path
        // rule, which requires an .sdlc segment, so every shell command was allowed while the file rule
        // worked. Asserting the set rather than four files is what makes a fifth one covered the day it
        // is added.
        foreach (string name in ProtectedPaths.Names)
        {
            ToolCall write = Read(
                JsonSerializer.Serialize(new
                {
                    tool_name = "Write",
                    tool_input = new { file_path = ".sdlc/cycles/20260929_1456_32/" + name },
                }),
                HookHost.Claude);

            Assert.AreEqual(
                HookDecision.Deny,
                HookPolicyHandler.Decide(write).Decision,
                $"A tool write to {name} was allowed.");

            ToolCall shell = Read(
                JsonSerializer.Serialize(new
                {
                    tool_name = "Bash",
                    tool_input = new { command = "echo x > " + name },
                }),
                HookHost.Claude);

            Assert.AreEqual(
                HookDecision.Deny,
                HookPolicyHandler.Decide(shell).Decision,
                $"A shell write to {name} was allowed.");
        }
    }

    [TestMethod]
    public void A_file_with_a_protected_name_outside_the_framework_directory_is_allowed()
    {
        // Somebody else's approval.md is not this framework's business.
        ToolCall call = Read(
            """{ "tool_name": "Write", "tool_input": { "file_path": "docs/approval.md" } }""",
            HookHost.Claude);

        Assert.AreEqual(HookDecision.Allow, HookPolicyHandler.Decide(call).Decision);
    }

    [TestMethod]
    [DataRow("echo approved >> .sdlc/cycles/20260929_1456_32/approval.md")]
    [DataRow("cd .sdlc/cycles/x; Set-Content approval.md \"approved\"")]
    [DataRow("cp /tmp/forged.md .sdlc/cycles/x/approval.md")]
    [DataRow("sed -i s/denied/approved/ .sdlc/cycles/x/approval.md")]
    [DataRow("Out-File -FilePath approval.md")]
    [DataRow("rm .sdlc/constraints.md")]
    [DataRow("sed -i s/dotnet-default/mofoncode/ .sdlc/solution.md")]
    public void A_shell_command_that_would_write_a_protected_file_is_denied(string command)
    {
        // The file rules alone would leave the obvious way round them open: a shell can write a file
        // without any tool naming its path.
        ToolCall call = Read(
            JsonSerializer.Serialize(new
            {
                tool_name = "Bash",
                tool_input = new { command },
            }),
            HookHost.Claude);

        Assert.AreEqual(
            HookDecision.Deny,
            HookPolicyHandler.Decide(call).Decision,
            $"'{command}' would write a file only a person writes.");
    }

    [TestMethod]
    [DataRow("cat .sdlc/cycles/20260929_1456_32/approval.md")]
    [DataRow("git diff .sdlc/constraints.md")]
    [DataRow("cat .sdlc/solution.md")]
    [DataRow("grep approved .sdlc/cycles/x/approval.md")]
    public void A_shell_command_that_only_reads_a_protected_file_is_allowed(string command)
    {
        // Refusing a read would make the gate obstructive without making it safer.
        ToolCall call = Read(
            JsonSerializer.Serialize(new { tool_name = "Bash", tool_input = new { command } }),
            HookHost.Claude);

        Assert.AreEqual(HookDecision.Allow, HookPolicyHandler.Decide(call).Decision, command);
    }

    [TestMethod]
    public void A_claude_denial_is_written_where_claude_reads_it()
    {
        JsonNode written = Write(HookHost.Claude, HookDecision.Deny, "because.");

        JsonNode nested = written["hookSpecificOutput"]!;
        Assert.AreEqual("PreToolUse", nested["hookEventName"]!.GetValue<string>());
        Assert.AreEqual("deny", nested["permissionDecision"]!.GetValue<string>());
        Assert.AreEqual("because.", nested["permissionDecisionReason"]!.GetValue<string>());
    }

    [TestMethod]
    public void A_copilot_denial_is_written_at_the_top_level_where_copilot_reads_it()
    {
        JsonNode written = Write(HookHost.Copilot, HookDecision.Deny, "because.");

        Assert.AreEqual("deny", written["permissionDecision"]!.GetValue<string>());
        Assert.AreEqual("because.", written["permissionDecisionReason"]!.GetValue<string>());
        Assert.IsNull(written["hookSpecificOutput"]);
    }

    [TestMethod]
    [DataRow(HookHost.Claude)]
    [DataRow(HookHost.Copilot)]
    public void An_allow_is_written_in_the_same_shape_as_a_denial(HookHost host)
    {
        JsonNode written = Write(host, HookDecision.Allow, null);

        JsonNode scope = host == HookHost.Claude ? written["hookSpecificOutput"]! : written;
        Assert.AreEqual("allow", scope["permissionDecision"]!.GetValue<string>());
    }

    [TestMethod]
    public void A_refusal_reason_is_written_readably_rather_than_escaped_for_html()
    {
        // The host parses this as JSON and never puts it in a page, and a refusal is read raw in a log
        // by someone working out why they were stopped.
        string json = HookProtocol.WriteDecision(HookHost.Claude, HookDecision.Deny, "a person's act <id>");

        StringAssert.Contains(json, "a person's act <id>");
        Assert.IsFalse(json.Contains("\\u0027", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not json")]
    [DataRow("[1, 2, 3]")]
    public void A_payload_that_cannot_be_read_is_a_failure_rather_than_an_exception(string payload)
    {
        // The caller turns this into an allow and says so on standard error. A hook that threw would
        // fail to start, which permits everything while appearing to protect.
        Result<ToolCall> result = HookProtocol.ReadCall(payload, HookHost.Claude);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Count > 0);
    }

    [TestMethod]
    public void A_payload_with_no_tool_input_reads_as_harmless_rather_than_failing()
    {
        ToolCall call = Read("""{ "tool_name": "SomethingElse" }""", HookHost.Claude);

        Assert.AreEqual(ToolKind.Harmless, call.Kind);
        Assert.AreEqual(0, call.Paths.Count);
    }

    private static ToolCall Read(string payload, HookHost host)
    {
        Result<ToolCall> result = HookProtocol.ReadCall(payload, host);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static JsonNode Write(HookHost host, HookDecision decision, string? reason)
    {
        string json = HookProtocol.WriteDecision(host, decision, reason);
        var node = JsonNode.Parse(json);

        Assert.IsNotNull(node, "The decision must be valid JSON: " + json);
        return node;
    }
}
