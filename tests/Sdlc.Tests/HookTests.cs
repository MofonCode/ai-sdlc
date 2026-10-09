using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
    [DataRow("codex")]
    [DataRow("cursor")]
    [DataRow("gemini")]
    public void A_host_that_is_not_supported_yet_is_not_recognised(string? spelling)
    {
        // Codex and Cursor both have a blocking pre-tool hook, so they are adapters waiting to be
        // written rather than hosts that cannot work. Claiming one before its payload
        // shapes and its refusal shape are held by a test would be the "looks supported and is not"
        // failure.
        //
        // Gemini CLI had a row for two commits. Google retired the product on 18 June 2026 and
        // replaced it with Antigravity CLI, so the row configured a hook for a program that no
        // longer serves requests -- which is the same defect as claiming an unexercised host,
        // arrived at from the other direction. D-50.
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
    public void A_copilot_cli_payload_reads_too_despite_naming_the_arguments_differently()
    {
        // Copilot documents two payload shapes and only one was read. The test above feeds the
        // VS Code-compatible one, which writes `tool_input`; this is the CLI's own camelCase shape,
        // which writes `toolArgs`. The reader looked for `tool_input` and `toolInput` and found
        // neither, so there was no path and no command, every call classified as harmless, and the
        // hook allowed an agent to write the five files only a person writes -- on the host the
        // README said was supported, with the conformance test green.
        const string payload = """
            {
              "sessionId": "x",
              "timestamp": 1704614400000,
              "cwd": "/solution",
              "toolName": "str_replace_editor",
              "toolArgs": { "path": ".sdlc/cycles/20260929_1456_32/approval.md" }
            }
            """;

        ToolCall call = Read(payload, HookHost.Copilot);

        Assert.AreEqual(ToolKind.FileWrite, call.Kind);
        Assert.AreEqual(HookDecision.Deny, HookPolicy.Decide(call).Decision);
    }

    [TestMethod]
    public void A_copilot_cli_shell_payload_reads_its_command_out_of_the_camel_case_arguments()
    {
        ToolCall call = Read(
            """{ "toolName": "bash", "toolArgs": { "command": "echo x > .sdlc/constraints.md" } }""",
            HookHost.Copilot);

        Assert.AreEqual(ToolKind.Shell, call.Kind);
        Assert.AreEqual(HookDecision.Deny, HookPolicy.Decide(call).Decision);
    }

    [TestMethod]
    public void An_antigravity_payload_reads_despite_nesting_the_call_inside_an_object()
    {
        // Antigravity's PreToolUse payload, from Google's hooks reference. Every host before it put
        // the tool's name and its arguments at the top level under some spelling; this one nests both
        // under `toolCall`, which is why the reader gained a place to look rather than a host's own
        // parser. `write_to_file` is its tool for creating a file.
        const string payload = """
            {
              "conversationId": "abc123",
              "workspacePaths": ["C:/solution"],
              "transcriptPath": "/tmp/transcript.jsonl",
              "artifactDirectoryPath": "/tmp/artifacts",
              "modelName": "gemini-3-pro",
              "stepIdx": 4,
              "toolCall": {
                "name": "write_to_file",
                "args": { "file_path": "C:/solution/.sdlc/cycles/20260929_1456_32/approval.md" }
              }
            }
            """;

        ToolCall call = Read(payload, HookHost.Antigravity);

        Assert.AreEqual("write_to_file", call.ToolName);
        Assert.AreEqual(ToolKind.FileWrite, call.Kind);
        Assert.AreEqual(HookDecision.Deny, HookPolicy.Decide(call).Decision);
    }

    [TestMethod]
    public void An_antigravity_shell_payload_is_judged_on_the_command_it_carries()
    {
        // `run_command` is its shell tool, and `replace_file_content` its editor. Classified by what
        // the call carries, so a shell writing a protected file is refused whatever it is called.
        ToolCall call = Read(
            """{ "toolCall": { "name": "run_command", "args": { "command": "echo x > .sdlc/constraints.md" } } }""",
            HookHost.Antigravity);

        Assert.AreEqual(ToolKind.Shell, call.Kind);
        Assert.AreEqual(HookDecision.Deny, HookPolicy.Decide(call).Decision);
    }

    [TestMethod]
    public void An_antigravity_denial_is_a_decision_rather_than_a_permission()
    {
        // The third refusal shape across three hosts, which is why the write methods are typed
        // switches rather than fields on a row. Antigravity does not frame a tool call as a
        // permission at all.
        JsonNode written = Write(HookHost.Antigravity, HookDecision.Deny, "because.");

        Assert.AreEqual("deny", written["decision"]!.GetValue<string>());
        Assert.AreEqual("because.", written["reason"]!.GetValue<string>());
        Assert.IsNull(written["permissionDecision"], "That is Copilot's shape, not this one.");
        Assert.IsNull(written["hookSpecificOutput"], "That is Claude Code's shape, not this one.");
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
        Assert.AreEqual(HookDecision.Allow, HookPolicy.Decide(call).Decision);
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
    public void Every_refusal_names_the_command_that_is_the_way_through()
    {
        // A refusal has to name the way through, or protecting the file just blocks the legitimate act.
        // This replaced three tests that each named one file and checked one fragment of its reason:
        // between them they covered two of the six protected files, and the two the record gained last
        // -- the declared stack and the decision record -- had no test on their reason at all. Asserting
        // the set is what makes a seventh covered the day it is added, which is the argument
        // Every_protected_file_is_denied_through_both_rules already makes about the denial itself.
        //
        // Deliberately absolute: every protected file, no exemption list. The one file this was arguable
        // for is the abandonment record, because abandoning is the one protected act an agent itself
        // wants -- and withholding the command there made abandoning ON THE RECORD harder without making
        // abandoning harder. ProtectedPaths carries that reasoning. An exemption here would be the
        // conditional rule D-09 says is where a bypass lives, and it would hide the next gap.
        //
        // The command is read out of the reason rather than compared against a table of expected
        // commands, because a table here would be a second list of the drafters -- and the one that
        // mattered would be whichever the author had not read (D-67).
        foreach (string name in ProtectedPaths.Names)
        {
            ToolCall call = Read(
                JsonSerializer.Serialize(new
                {
                    tool_name = "Write",
                    tool_input = new { file_path = ".sdlc/cycles/20260929_1456_32/" + name },
                }),
                HookHost.Claude);

            string reason = HookPolicy.Decide(call).Reason;

            Match named = Regex.Match(reason, @"Run '(?<command>sdlc [^']+)'");

            Assert.IsTrue(
                named.Success,
                $"The refusal about {name} names no command. A person meets this text as an error and "
                + "has to be told what to run instead, or the protection reads as a dead end:\n\n"
                + reason);

            // And the command it names has to be one the tool offers. A refusal pointing at a verb that
            // was renamed sends somebody looking for the flag that makes it work, which is the confusion
            // Usage.Drafters exists to avoid -- so the offer and the refusal are held together here.
            string command = named.Groups["command"].Value["sdlc ".Length..];

            StringAssert.Contains(
                Sdlc.Cli.Usage.Text,
                command,
                $"The refusal about {name} says to run '{command}', which the usage text does not offer.");
        }
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
                HookPolicy.Decide(write).Decision,
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
                HookPolicy.Decide(shell).Decision,
                $"A shell write to {name} was allowed.");
        }
    }

    [TestMethod]
    public void Every_refusal_says_it_is_a_rule_before_it_says_anything_else()
    {
        // A refusal here is the design working, and every host renders this text as an error -- red,
        // prefixed `Error:`. So a rule doing its job arrived looking like a crash, a person went
        // hunting for the bug, and the thing that was working sent them back twice. Asserted over
        // both rules and every protected file, because the next one added inherits the problem.
        foreach (string name in ProtectedPaths.Names)
        {
            ToolCall write = Read(
                JsonSerializer.Serialize(new
                {
                    tool_name = "Write",
                    tool_input = new { file_path = ".sdlc/cycles/20260929_1456_32/" + name },
                }),
                HookHost.Claude);

            StringAssert.StartsWith(
                HookPolicy.Decide(write).Reason,
                ProtectedPaths.Preface,
                $"The refusal about {name} does not say it is a rule.");
        }

        ToolCall shell = Read(
            """{ "tool_name": "Bash", "tool_input": { "command": "sdlc init" } }""",
            HookHost.Claude);

        StringAssert.StartsWith(HookPolicy.Decide(shell).Reason, ProtectedPaths.Preface);
    }

    [TestMethod]
    public void The_adoption_refusal_says_where_the_options_actually_are()
    {
        // The rule matches the verb rather than parsing the line, so `sdlc init --help` is refused
        // too -- which cost a real session two rounds: an agent tried to read the flags so it could
        // hand a person an accurate command, met a refusal that looked like a fault, and the person
        // went looking for a broken install. The flags are in `sdlc help` and the refusal now says
        // so. Narrowing the rule to permit `--help` was the other option and is worse: "allow when
        // --help appears" lets `sdlc init --help && sdlc init` through, and a conditional in a hook
        // rule is where a bypass lives.
        ToolCall call = Read(
            """{ "tool_name": "Bash", "tool_input": { "command": "sdlc init --help" } }""",
            HookHost.Claude);

        HookVerdict verdict = HookPolicy.Decide(call);

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(verdict.Reason, "sdlc help");
        StringAssert.Contains(
            verdict.Reason,
            "Options for init",
            "Naming the command without naming the section leaves a reader scanning the whole of it.");

        StringAssert.Contains(
            Sdlc.Cli.Usage.Text,
            "Options for init",
            "The refusal points at a heading in the usage text, so the heading has to be there.");
    }

    [TestMethod]
    public void A_file_with_a_protected_name_outside_the_framework_directory_is_allowed()
    {
        // Somebody else's approval.md is not this framework's business.
        ToolCall call = Read(
            """{ "tool_name": "Write", "tool_input": { "file_path": "docs/approval.md" } }""",
            HookHost.Claude);

        Assert.AreEqual(HookDecision.Allow, HookPolicy.Decide(call).Decision);
    }

    [TestMethod]
    [DataRow("echo approved >> .sdlc/cycles/20260929_1456_32/approval.md")]
    [DataRow("cd .sdlc/cycles/x; Set-Content approval.md \"approved\"")]
    [DataRow("cp /tmp/forged.md .sdlc/cycles/x/approval.md")]
    [DataRow("sed -i s/denied/approved/ .sdlc/cycles/x/approval.md")]
    [DataRow("Out-File -FilePath approval.md")]
    [DataRow("rm .sdlc/constraints.md")]
    [DataRow("sed -i s/dotnet-default/mofoncode/ .sdlc/solution.md")]

    // The rename that places a decision, in every spelling somebody would reach for. These passed:
    // the write indicators were "move " and "copy " with trailing spaces, which match neither
    // Move-Item nor Copy-Item, so on Windows the whole shell rule was one hyphen away from nothing.
    [DataRow("mv approval.approved.draft.md approval.md")]
    [DataRow("Move-Item -Force approval.approved.draft.md approval.md")]
    [DataRow("Rename-Item approval.approved.draft.md approval.md")]
    [DataRow("Copy-Item approval.approved.draft.md .sdlc/cycles/x/approval.md")]
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
            HookPolicy.Decide(call).Decision,
            $"'{command}' would write a file only a person writes.");
    }

    [TestMethod]
    [DataRow("Bash")]
    [DataRow("PowerShell")]
    public void The_shell_rule_covers_every_shell_the_host_offers(string tool)
    {
        // PowerShell is a separate tool from Bash and the host matches on the tool's name, so a
        // matcher naming only Bash exempted it -- on the one platform where PowerShell is the shell.
        // A rule that silently stops covering part of the surface is worse than no rule.
        ToolCall call = Read(
            JsonSerializer.Serialize(new
            {
                tool_name = tool,
                tool_input = new { command = "Move-Item x .sdlc/cycles/x/approval.md" },
            }),
            HookHost.Claude);

        Assert.AreEqual(
            HookDecision.Deny,
            HookPolicy.Decide(call).Decision,
            $"The shell rule does not reach the {tool} tool.");

        StringAssert.Contains(
            SupportedHosts.Of(HookHost.Claude).ToolMatcher,
            tool,
            $"The policy denies a {tool} call, and the matcher adoption writes has to send it one.");
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

        Assert.AreEqual(HookDecision.Allow, HookPolicy.Decide(call).Decision, command);
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
    [DynamicData(nameof(Hosts))]
    public void An_allow_says_nothing_rather_than_bypassing_the_hosts_own_gate(HookHost host)
    {
        // Do not "fix" this back to an explicit allow. On these surfaces
        // `permissionDecision: "allow"` is not silence -- Claude Code documents it as bypassing the
        // permission system -- so writing one whenever the policy has no opinion would stop the host
        // asking a person about every Edit, Write and Bash the hook matches, and would override the
        // team's own allow and deny rules for them. D-39 records why, and why `force_ask` is not the
        // answer either.
        //
        // Driven off the row, because one host cannot be silent: Antigravity requires a decision on
        // every call, so the honest translation of "nothing to say" is its neutral word rather than
        // an empty object. The principle is the same on all three -- hand the question back to the
        // person's own permission flow -- and only the spelling differs. D-51.
        SupportedHost described = SupportedHosts.Of(host);
        JsonNode written = Write(host, HookDecision.Allow, null);

        if (described.NeutralDecision is not { } neutral)
        {
            Assert.AreEqual(0, written.AsObject().Count, "There is nothing to say, so nothing is said.");
            return;
        }

        Assert.AreEqual(
            neutral,
            written["decision"]!.GetValue<string>(),
            described.Name + " requires a decision on every call, and this is not the neutral one.");

        Assert.AreNotEqual(
            "allow",
            written["decision"]!.GetValue<string>(),
            "An explicit allow bypasses the host's own permission system rather than deferring to it.");

        Assert.IsNull(written["reason"], "There is no reason, because nothing was refused.");
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

    [TestMethod]
    [DataRow("pre-tool-use", HookEvent.PreToolUse)]
    [DataRow("pretooluse", HookEvent.PreToolUse)]
    [DataRow("turn-end", HookEvent.TurnEnd)]
    [DataRow("TURN-END", HookEvent.TurnEnd)]
    [DataRow("stop", HookEvent.TurnEnd)]
    public void A_known_event_is_recognised_however_it_is_spelled(string spelling, HookEvent expected)
    {
        // "stop" is accepted because that is what Claude Code calls it, so a hook config copied from
        // the host's own documentation works without translation.
        Assert.IsTrue(HookProtocol.TryParseEvent(spelling, out HookEvent? parsed));
        Assert.AreEqual(expected, parsed);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void An_absent_event_is_the_tool_call(string? spelling)
    {
        // Every hook config written before the second event existed means this one. A default that
        // refused would turn an upgrade of the binary into a hook that stops running, and a hook
        // that stops running permits everything.
        Assert.IsTrue(HookProtocol.TryParseEvent(spelling, out HookEvent? parsed));
        Assert.AreEqual(HookEvent.PreToolUse, parsed);
    }

    [TestMethod]
    [DataRow("postToolUse")]
    [DataRow("sessionStart")]
    public void An_event_that_is_not_judged_is_not_recognised(string spelling)
    {
        Assert.IsFalse(HookProtocol.TryParseEvent(spelling, out HookEvent? parsed));
        Assert.IsNull(parsed);
    }

    [TestMethod]
    public void A_claude_stop_payload_reads_as_a_turn_end()
    {
        const string payload = """
            {
              "session_id": "abc123",
              "transcript_path": "/tmp/transcript.jsonl",
              "cwd": "C:/solution",
              "hook_event_name": "Stop",
              "stop_hook_active": false
            }
            """;

        Result<TurnEnd> result = HookProtocol.ReadTurnEnd(payload);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.AreEqual("C:/solution", result.Value.WorkingDirectory);
        Assert.IsFalse(result.Value.AlreadyContinuing);
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("\"true\"")]
    public void A_host_already_continuing_says_so_whichever_way_it_writes_the_flag(string written)
    {
        // Both spellings, because the cost of being wrong is asymmetric: reading a true flag as
        // false refuses a turn the host has already been sent back once, which is an endless turn.
        Result<TurnEnd> result = HookProtocol.ReadTurnEnd(
            $$"""{ "hook_event_name": "Stop", "stop_hook_active": {{written}} }""");

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.IsTrue(result.Value.AlreadyContinuing);
    }

    [TestMethod]
    public void A_turn_end_payload_that_cannot_be_read_fails_rather_than_reading_as_a_clean_turn()
    {
        // The command line turns this into an allow and says so on standard error. What must not
        // happen is a malformed payload reading as a turn with nothing to say, because then a broken
        // host and a green build are the same answer.
        Assert.IsTrue(HookProtocol.ReadTurnEnd("not json").IsFailed);
        Assert.IsTrue(HookProtocol.ReadTurnEnd("[]").IsFailed);
        Assert.IsTrue(HookProtocol.ReadTurnEnd("   ").IsFailed);
    }

    [TestMethod]
    public void A_turn_sent_back_is_a_decision_rather_than_a_permission()
    {
        // Deliberately a different shape from the tool-call decision. A tool call is permitted or
        // refused; a turn is sent back or let go. Writing one in the other's shape is the bug this
        // asserts against, and the host's answer to it is silence.
        JsonNode node = Parse(HookProtocol.WriteTurnEndDecision(
            HookHost.Claude,
            HookDecision.Deny,
            "the suite is red"));

        Assert.AreEqual("block", node["decision"]!.GetValue<string>());
        Assert.AreEqual("the suite is red", node["reason"]!.GetValue<string>());
        Assert.IsNull(node["permissionDecision"], "That is the tool-call shape, not this one.");
        Assert.IsNull(node["hookSpecificOutput"], "That is the tool-call shape, not this one.");
    }

    [TestMethod]
    public void A_turn_let_go_says_nothing_at_all()
    {
        JsonNode node = Parse(HookProtocol.WriteTurnEndDecision(HookHost.Claude, HookDecision.Allow, null));

        Assert.AreEqual(0, node.AsObject().Count, "There is nothing to say, so nothing is said.");
    }

    [TestMethod]
    [DynamicData(nameof(Hosts))]
    public void Every_host_is_described_completely(HookHost named)
    {
        // The table is the support claim, so an incomplete row is a host that looks supported and is
        // not. Driven off the enum rather than the table, because a member with no row at all is the
        // case worth catching and enumerating the table would skip it.
        SupportedHost host = SupportedHosts.Of(named);

        Assert.IsFalse(string.IsNullOrWhiteSpace(host.Name), "A host with no name cannot be refused by one.");
        Assert.IsTrue(host.Spellings.Count > 0, host.Name + " answers to no name on the command line.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(host.PreToolEvent), host.Name + " names no pre-tool event.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(host.TimeoutKey), host.Name + " names no timeout key.");

        foreach (string spelling in host.Spellings)
        {
            Assert.IsFalse(
                spelling.Any(char.IsUpper),
                "Spellings are matched case-insensitively, so an upper-case one in the table is a "
                + $"reader wondering whether case matters: '{spelling}'.");

            Assert.IsTrue(
                HookProtocol.TryParseHost(spelling, out HookHost? parsed) && parsed == named,
                $"'{spelling}' is in {host.Name}'s row and the command line does not accept it.");
        }

        // Relative and forward-slashed, which is how every document writes it and what lets a reader
        // check the path against the host's own documentation without translating separators.
        Assert.IsFalse(string.IsNullOrWhiteSpace(host.ConfigPath), host.Name + " has nowhere to write its config.");
        Assert.IsFalse(
            host.ConfigPath.Contains('\\', StringComparison.Ordinal),
            host.Name + "'s config path carries a backslash, and Initialise splits on '/'.");
        Assert.IsFalse(
            Path.IsPathRooted(host.ConfigPath),
            host.Name + "'s config path is absolute, so adoption would write outside the solution.");
    }

    [TestMethod]
    public void No_two_hosts_answer_to_the_same_name()
    {
        // One spelling resolving to two hosts would make the adapter's answer depend on table order,
        // which is a defect with no symptom until a decision is written in the wrong shape.
        List<string> spellings = [.. SupportedHosts.All.SelectMany(host => host.Spellings)];

        CollectionAssert.AreEquivalent(
            spellings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            spellings,
            "Two hosts claim one spelling.");
    }

    [TestMethod]
    [DynamicData(nameof(Hosts))]
    public void Every_host_refuses_a_write_to_a_protected_file_in_its_own_payload_shape(HookHost named)
    {
        // §12.1's promise, per row: each host is fed a payload in the shape it documents and has to
        // deny. A host added to the table without this passing is a host claimed and not exercised.
        ToolCall call = Read(
            $$"""{ "tool_name": "Write", "tool_input": { "file_path": "{{ApprovalPath}}" } }""",
            named);

        Assert.AreEqual(
            HookDecision.Deny,
            HookPolicy.Decide(call).Decision,
            SupportedHosts.Of(named).Name + " allowed a write to an approval ledger.");
    }

    [TestMethod]
    [DynamicData(nameof(Hosts))]
    public void Every_host_has_a_refusal_shape_for_both_moments(HookHost named)
    {
        // The two write methods are switches on the host, so a row added without a branch in each
        // throws rather than compiles -- and this is where that is found. A hook that throws is a
        // hook that does not run, which permits everything while appearing to protect.
        SupportedHost host = SupportedHosts.Of(named);

        JsonNode call = Write(named, HookDecision.Deny, "because.");
        Assert.AreNotEqual(0, call.AsObject().Count, host.Name + " has no tool-call refusal shape.");

        if (!host.GovernsTurnEnd)
        {
            // A host with no turn end still has a shape written for it rather than a throw, so the
            // day one is wired is a test failure about a word and not a crash in a hook. Antigravity
            // is the first row to exercise this: it has a Stop event and no documented flag saying
            // the agent is already continuing, so the moment is left ungoverned. D-51.
            JsonNode unused = Parse(HookProtocol.WriteTurnEndDecision(named, HookDecision.Deny, "why"));

            Assert.AreNotEqual(
                0,
                unused.AsObject().Count,
                host.Name + " governs no turn end, and the shape for the day it does is missing.");

            return;
        }

        JsonNode turn = Parse(HookProtocol.WriteTurnEndDecision(named, HookDecision.Deny, "the suite is red"));

        // The word each host reads, taken from that host's own documentation and written out here
        // rather than read off the implementation -- an assertion that asked the code what it wrote
        // would agree with any answer. Both current hosts spell it `block`; that is not a given, and
        // a host added that spells it otherwise fails here until its word is captured.
        string expected = named switch
        {
            HookHost.Claude => "block",
            HookHost.Copilot => "block",
            _ => throw new AssertFailedException(
                $"{host.Name} governs the turn end and no captured fact here says how it spells a "
                + "refusal. Add the host's own word, from its documentation."),
        };

        Assert.AreEqual(
            expected,
            turn["decision"]!.GetValue<string>(),
            host.Name + " governs the turn end and does not spell the refusal in a shape it reads.");
        Assert.AreEqual("the suite is red", turn["reason"]!.GetValue<string>());
    }

    /// <summary>
    /// Every host the enum declares, so the conformance assertions run per host.
    /// </summary>
    /// <remarks>
    /// The enum rather than <c>SupportedHosts.All</c>, deliberately: a member with no row is a host
    /// half-added, and enumerating the table would be the one arrangement that cannot see it.
    /// </remarks>
    public static IEnumerable<object[]> Hosts =>
        Enum.GetValues<HookHost>().Select(host => new object[] { host });

    private static ToolCall Read(string payload, HookHost host)
    {
        Result<ToolCall> result = HookProtocol.ReadCall(payload, host);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static JsonNode Parse(string json)
    {
        var node = JsonNode.Parse(json);

        Assert.IsNotNull(node, "The decision must be valid JSON: " + json);
        return node;
    }

    private static JsonNode Write(HookHost host, HookDecision decision, string? reason)
    {
        string json = HookProtocol.WriteDecision(host, decision, reason);
        var node = JsonNode.Parse(json);

        Assert.IsNotNull(node, "The decision must be valid JSON: " + json);
        return node;
    }
}
