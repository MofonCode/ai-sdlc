using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;

namespace Sdlc.Core.Hooks;

/// <summary>The agent hosts the gate speaks to.</summary>
public enum HookHost
{
    /// <summary>Claude Code. <c>PreToolUse</c>, decision under <c>hookSpecificOutput</c>.</summary>
    Claude,

    /// <summary>GitHub Copilot. <c>preToolUse</c>, decision at the top level.</summary>
    Copilot,

    /// <summary>
    /// Antigravity. <c>PreToolUse</c>, a nested payload, and a decision on every call including the
    /// ones the policy has no opinion about.
    /// </summary>
    Antigravity,

    // Every other fact about a host -- where its configuration goes, who owns that file, what it
    // calls each event, whether it has a turn end at all -- is a row in SupportedHosts rather than a
    // switch here. A member added without a row throws from SupportedHosts.Of, which the suite holds.
}

/// <summary>
/// Which moment the host is asking about.
/// </summary>
/// <remarks>
/// Named for what the moment is rather than for what any host calls it. Claude Code spells the
/// second one <c>Stop</c> and Copilot <c>agentStop</c>, both of which describe that host's own state
/// machine rather than the thing being judged, and letting either name inland would put one host's
/// vocabulary in the policy -- which is the whole of what this file exists to prevent. Each host's
/// spelling is a field on its row in <see cref="SupportedHosts"/>, which is the only place they
/// belong.
/// </remarks>
public enum HookEvent
{
    /// <summary>An agent is about to use a tool. The policy may refuse it.</summary>
    PreToolUse,

    /// <summary>An agent is about to finish a turn. The gate may send it back.</summary>
    TurnEnd,
}

/// <summary>What the policy decided about a tool call.</summary>
public enum HookDecision
{
    /// <summary>Nothing to say. The host carries on as it would have.</summary>
    Allow,

    /// <summary>Refused. The reason is shown to the agent and the person.</summary>
    Deny,
}

/// <summary>
/// Reads a host's hook payload and writes its decision, in that host's own shape.
/// </summary>
/// <remarks>
/// Every host can deny a tool call before it runs, so the enforcement primitive is portable and what
/// differs is vocabulary: the event's name, the tool's name, where the file path sits in the payload,
/// and how a refusal is spelled. All of that lives here, so the policy is written once.
/// <para>
/// The payload readers are deliberately tolerant. These surfaces are young and a key may move; a
/// reader that threw on an unexpected shape would turn a renamed field into a hook that fails to
/// start, which permits everything while appearing to protect. So a shape this cannot understand
/// allows the call and says so on standard error, where it is visible rather than silent.
/// </para>
/// </remarks>
public static class HookProtocol
{
    /// <summary>
    /// How a decision is written.
    /// </summary>
    /// <remarks>
    /// Relaxed escaping, deliberately. The default escapes apostrophes and angle brackets for safety in
    /// HTML, which turns a refusal reason into `a person's act` on the wire. The host parses this
    /// as JSON and never embeds it in a page, so the escaping buys nothing here and costs legibility in
    /// the one place a refusal is read raw: a log, when someone is working out why they were stopped.
    /// </remarks>
    private static readonly JsonSerializerOptions _output = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// How each event may be named on the command line.
    /// </summary>
    /// <remarks>
    /// Each host's own spelling is accepted alongside the framework's, so a hook config copied from a
    /// host's documentation works without translation.
    /// </remarks>
    private static readonly (string Spelling, HookEvent Event)[] _events =
    [
        ("pre-tool-use", HookEvent.PreToolUse),
        ("pretooluse", HookEvent.PreToolUse),
        ("pre-tool", HookEvent.PreToolUse),
        ("turn-end", HookEvent.TurnEnd),
        ("turnend", HookEvent.TurnEnd),

        // Each host's own name for the moment: `Stop` on Claude Code, `agentStop` on Copilot. Both
        // are accepted for the reason the remark above gives -- a config copied from a host's
        // documentation should work without translation.
        ("stop", HookEvent.TurnEnd),
        ("agentstop", HookEvent.TurnEnd),
        ("agent-stop", HookEvent.TurnEnd),
    ];

    /// <summary>
    /// Reads an event name as it is written on the command line.
    /// </summary>
    /// <remarks>
    /// An absent name is <see cref="HookEvent.PreToolUse"/>, which is what every hook config written
    /// before the second event existed means. A default that refused instead would turn an upgrade of
    /// the binary into a hook that stops running, and a hook that stops running permits everything.
    /// </remarks>
    /// <param name="text">The event name, or null when the option was not given.</param>
    /// <param name="hookEvent">The event, when this returns true.</param>
    /// <returns>True when the name is absent or is one of the supported events.</returns>
    public static bool TryParseEvent(string? text, [NotNullWhen(true)] out HookEvent? hookEvent)
    {
        string name = text?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            hookEvent = HookEvent.PreToolUse;
            return true;
        }

        hookEvent = null;
        foreach ((string spelling, HookEvent candidate) in _events)
        {
            if (string.Equals(name, spelling, StringComparison.OrdinalIgnoreCase))
            {
                hookEvent = candidate;
                break;
            }
        }

        return hookEvent is not null;
    }

    /// <summary>
    /// Reads a host name as it is written on the command line.
    /// </summary>
    /// <remarks>
    /// The spellings come from <see cref="SupportedHosts"/> rather than from a list here, so what the
    /// command line accepts and what adoption configures cannot disagree. They did not have to: the
    /// two lists were separate, and a host could be spellable without being installable or the
    /// reverse, with nothing saying which.
    /// </remarks>
    /// <param name="text">The host name.</param>
    /// <param name="host">The host, when this returns true.</param>
    /// <returns>True when the name is one of the supported hosts.</returns>
    public static bool TryParseHost(string? text, [NotNullWhen(true)] out HookHost? host)
    {
        string name = text?.Trim() ?? string.Empty;

        host = null;

        if (name.Length == 0)
        {
            return false;
        }

        foreach (SupportedHost candidate in SupportedHosts.All)
        {
            foreach (string spelling in candidate.Spellings)
            {
                if (string.Equals(name, spelling, StringComparison.OrdinalIgnoreCase))
                {
                    host = candidate.Host;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a tool call out of a host's payload.
    /// </summary>
    /// <param name="json">The payload the host wrote to standard input.</param>
    /// <param name="host">Which host wrote it.</param>
    /// <returns>The normalised call, or a failure describing what could not be read.</returns>
    public static Result<ToolCall> ReadCall(string json, HookHost host)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Fail<ToolCall>("The hook received no payload on standard input.");
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException error)
        {
            return Result.Fail<ToolCall>(
                string.Create(CultureInfo.InvariantCulture, $"The hook payload is not valid JSON. {error.Message}"));
        }

        if (root is not JsonObject payload)
        {
            return Result.Fail<ToolCall>("The hook payload is not a JSON object.");
        }

        // Antigravity nests both under `toolCall`, where every other host puts them at the top level
        // under some spelling. Read as another place to look rather than as a host's own reader, for
        // the reason the whole method is tolerant: a host that moves a key must not fall out of the
        // rules, and a reader that only understood the asking host's shape would be one rename away
        // from permitting everything.
        var call = payload["toolCall"] as JsonObject;

        string toolName = Text(payload, "tool_name")
            ?? Text(payload, "toolName")
            ?? (call is null ? null : Text(call, "name"))
            ?? string.Empty;

        string? workingDirectory = Text(payload, "cwd") ?? Text(payload, "workingDirectory");

        // Three spellings, and the third is not cosmetic. Copilot documents two payload shapes -- a
        // VS Code-compatible one that writes `tool_input`, and the CLI's own camelCase one that
        // writes `toolArgs` -- and only the first was read. So on Copilot CLI this found no path and
        // no command, every call classified as harmless, and the hook allowed writes to the files
        // only a person writes while the directory still looked governed. The conformance test
        // passed because the captured payload was the VS Code shape, which is why there is now one
        // of each.
        JsonObject? input = payload["tool_input"] as JsonObject
            ?? payload["toolInput"] as JsonObject
            ?? payload["toolArgs"] as JsonObject
            ?? (call?["args"] as JsonObject);

        List<string> paths = [];
        string? command = null;

        if (input is not null)
        {
            // Hosts name the path differently and some calls carry several. Collecting from every
            // known key is what keeps one host's rename from silently disabling the rule.
            foreach (string key in (string[])["file_path", "filePath", "path", "notebook_path", "file"])
            {
                if (Text(input, key) is { Length: > 0 } value)
                {
                    paths.Add(value);
                }
            }

            if (input["edits"] is JsonArray edits)
            {
                foreach (JsonNode? edit in edits)
                {
                    if (edit is JsonObject entry && Text(entry, "file_path") is { Length: > 0 } nested)
                    {
                        paths.Add(nested);
                    }
                }
            }

            command = Text(input, "command") ?? Text(input, "cmd") ?? Text(input, "script");
        }

        return Result.Ok(new ToolCall(toolName, Classify(toolName, paths, command), paths, command, workingDirectory));
    }

    /// <summary>
    /// Reads a turn ending out of a host's payload.
    /// </summary>
    /// <remarks>
    /// Tolerant for the same reason <see cref="ReadCall"/> is, and the stakes are the same: a key
    /// that moves must not turn into a hook that fails to start.
    /// <para>
    /// The flag matters more than the working directory. Claude Code sets it when the agent is
    /// already continuing because a previous turn end was refused, and an unconditional second
    /// refusal there is an endless turn rather than a governed stop.
    /// </para>
    /// </remarks>
    /// <param name="json">The payload the host wrote to standard input.</param>
    /// <returns>The normalised turn ending, or a failure describing what could not be read.</returns>
    public static Result<TurnEnd> ReadTurnEnd(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Fail<TurnEnd>("The hook received no payload on standard input.");
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException error)
        {
            return Result.Fail<TurnEnd>(
                string.Create(CultureInfo.InvariantCulture, $"The hook payload is not valid JSON. {error.Message}"));
        }

        if (root is not JsonObject payload)
        {
            return Result.Fail<TurnEnd>("The hook payload is not a JSON object.");
        }

        return Result.Ok(new TurnEnd(
            Text(payload, "cwd") ?? Text(payload, "workingDirectory"),
            Flag(payload, "stop_hook_active") || Flag(payload, "stopHookActive")));
    }

    /// <summary>
    /// Writes a decision in the host's own shape.
    /// </summary>
    /// <remarks>
    /// <strong>A refusal is written; an allow says nothing at all.</strong> On these surfaces an
    /// explicit <c>permissionDecision: "allow"</c> is not silence -- Claude Code documents it as
    /// bypassing the permission system, so writing one on every call the policy has no opinion about
    /// would stop the host asking a person about the very tools the hook matches
    /// (<c>Edit|Write|MultiEdit|NotebookEdit|Bash|PowerShell</c>) and would override whatever allow and deny
    /// rules the team wrote for them. A governance tool removing the host's own gate as a side
    /// effect is the inverse of what it is for, so the policy having nothing to say is written as
    /// nothing. See D-39.
    /// <para>
    /// <c>ask</c> is the available third value and is also wrong: it would force a prompt on calls a
    /// person's own settings auto-approve, which overrides the host in the restrictive direction
    /// rather than the permissive one. Still an override.
    /// </para>
    /// <para>
    /// Silence for every host, including the two whose output spec was not verifiable when this was
    /// decided. The failure directions are asymmetric: a host that ignores an empty object falls back
    /// to asking a person, and a host that honours an explicit allow the way Claude Code does gets
    /// the same bypass shipped to it.
    /// </para>
    /// </remarks>
    /// <param name="host">The host that asked.</param>
    /// <param name="decision">What was decided.</param>
    /// <param name="reason">Why, when it was a refusal.</param>
    /// <returns>The JSON to write to standard output.</returns>
    public static string WriteDecision(HookHost host, HookDecision decision, string? reason)
    {
        if (decision != HookDecision.Deny)
        {
            // Silence where the host permits it, and the host's own neutral word where it does not.
            // Antigravity requires a decision on every call, so there is nothing to be silent with --
            // see D-51 for why that word is `ask` and not `allow`.
            return SupportedHosts.Of(host).NeutralDecision is { } neutral
                ? new JsonObject { ["decision"] = neutral }.ToJsonString(_output)
                : new JsonObject().ToJsonString(_output);
        }

        JsonObject payload = host switch
        {
            // Claude Code nests the decision under the event it belongs to, and names the event it
            // is answering -- taken from the host's row rather than written out here, so the name in
            // the decision and the key in the generated configuration are the same string.
            HookHost.Claude => new JsonObject
            {
                ["hookSpecificOutput"] = new JsonObject
                {
                    ["hookEventName"] = SupportedHosts.Of(host).PreToolEvent,
                    ["permissionDecision"] = "deny",
                    ["permissionDecisionReason"] = reason ?? string.Empty,
                },
            },

            // Copilot reads the same two fields at the top level.
            HookHost.Copilot => new JsonObject
            {
                ["permissionDecision"] = "deny",
                ["permissionDecisionReason"] = reason ?? string.Empty,
            },

            // Antigravity does not frame a tool call as a permission at all: the same `decision` and
            // `reason` it uses for every other answer, with the refusal spelled `deny`. A third shape
            // from a third host, which is why these stay typed switches rather than fields on a row.
            HookHost.Antigravity => new JsonObject
            {
                ["decision"] = "deny",
                ["reason"] = reason ?? string.Empty,
            },

            _ => throw new ArgumentOutOfRangeException(nameof(host), host, "Unknown host."),
        };

        return payload.ToJsonString(_output);
    }

    /// <summary>
    /// Writes a turn-end decision in the host's own shape.
    /// </summary>
    /// <remarks>
    /// <strong>A different shape from <see cref="WriteDecision"/>, which is why the adapter exists.</strong>
    /// A tool call is permitted or refused, so its answer is a permission; a turn is sent back or let
    /// go, so its answer is a decision at the top level with the reason beside it. Claude Code spells
    /// the refusal <c>block</c>.
    /// <para>
    /// An allow writes an empty object rather than an explicit permission. There is nothing to say,
    /// and a host that gains a field for saying it will not break on silence.
    /// </para>
    /// </remarks>
    /// <param name="host">The host that asked.</param>
    /// <param name="decision">What was decided.</param>
    /// <param name="reason">Why, when the turn was sent back.</param>
    /// <returns>The JSON to write to standard output.</returns>
    public static string WriteTurnEndDecision(HookHost host, HookDecision decision, string? reason)
    {
        if (decision != HookDecision.Deny)
        {
            return new JsonObject().ToJsonString(_output);
        }

        JsonObject payload = host switch
        {
            HookHost.Claude => new JsonObject
            {
                ["decision"] = "block",
                ["reason"] = reason ?? string.Empty,
            },

            // Copilot's `agentStop` reads the same two fields, and spells the refusal the same way.
            // This branch was written before the event was wired, against the day it would be, and
            // the day came: the host has had the event all along and the framework claimed it did
            // not. See D-46.
            HookHost.Copilot => new JsonObject
            {
                ["decision"] = "block",
                ["reason"] = reason ?? string.Empty,
            },

            // Antigravity has a Stop event and this is not reached, because its row governs no turn
            // end: the payload carries no flag saying the agent is already continuing, so a refusal
            // would be an endless turn. The shape is written anyway rather than left to throw, and
            // it is the inverted one -- there, `continue` is what sends a turn back and anything
            // else lets it go, so the word a refusal needs is the opposite of the other two hosts'.
            // The day a loop guard is documented, this is already right. D-51.
            HookHost.Antigravity => new JsonObject
            {
                ["decision"] = "continue",
                ["reason"] = reason ?? string.Empty,
            },

            _ => throw new ArgumentOutOfRangeException(nameof(host), host, "Unknown host."),
        };

        return payload.ToJsonString(_output);
    }

    private static ToolKind Classify(string toolName, List<string> paths, string? command)
    {
        // Classified by what the call carries rather than only by its name, so a tool this does not
        // recognise is still judged on whether it is about to write a file or run a shell.
        if (!string.IsNullOrWhiteSpace(command))
        {
            return ToolKind.Shell;
        }

        string[] shellNames = ["bash", "powershell", "shell", "run_shell_command", "exec_command", "terminal"];
        if (Array.Exists(shellNames, name => toolName.Contains(name, StringComparison.OrdinalIgnoreCase)))
        {
            return ToolKind.Shell;
        }

        string[] writeNames = ["edit", "write", "create", "replace", "notebookedit", "multiedit", "apply_patch", "str_replace"];
        if (paths.Count > 0
            && Array.Exists(writeNames, name => toolName.Replace("_", string.Empty, StringComparison.Ordinal)
                .Contains(name.Replace("_", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)))
        {
            return ToolKind.FileWrite;
        }

        return ToolKind.Harmless;
    }

    private static string? Text(JsonObject node, string key) =>
        node.TryGetPropertyValue(key, out JsonNode? value) && value is JsonValue
            ? value.GetValue<object>()?.ToString()
            : null;

    /// <summary>
    /// Reads a boolean that a host may have written as a boolean or as a string.
    /// </summary>
    /// <remarks>
    /// Both spellings are accepted because the cost of being wrong is asymmetric. Reading a true flag
    /// as false makes the gate refuse a turn the host has already been sent back once, which is an
    /// endless turn; reading a false one as true makes it say nothing, once.
    /// </remarks>
    private static bool Flag(JsonObject node, string key)
    {
        if (!node.TryGetPropertyValue(key, out JsonNode? value) || value is not JsonValue found)
        {
            return false;
        }

        return found.TryGetValue(out bool flag)
            ? flag
            : bool.TryParse(found.GetValue<object>()?.ToString(), out bool parsed) && parsed;
    }
}
