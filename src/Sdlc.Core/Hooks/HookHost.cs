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
}

/// <summary>
/// Which moment the host is asking about.
/// </summary>
/// <remarks>
/// Named for what the moment is rather than for what either host calls it. Claude Code spells the
/// second one <c>Stop</c>, which describes the host's own state machine rather than the thing being
/// judged, and letting that name inland would put one host's vocabulary in the policy -- which is
/// the whole of what this file exists to prevent.
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
    /// How each host may be named on the command line, so a hook config written by hand is accepted
    /// whichever way its author spelled it.
    /// </summary>
    private static readonly (string Spelling, HookHost Host)[] _spellings =
    [
        ("claude", HookHost.Claude),
        ("claude-code", HookHost.Claude),
        ("copilot", HookHost.Copilot),
        ("github-copilot", HookHost.Copilot),
    ];

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
        ("stop", HookEvent.TurnEnd),
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

    /// <summary>Reads a host name as it is written on the command line.</summary>
    /// <param name="text">The host name.</param>
    /// <param name="host">The host, when this returns true.</param>
    /// <returns>True when the name is one of the supported hosts.</returns>
    public static bool TryParseHost(string? text, [NotNullWhen(true)] out HookHost? host)
    {
        string name = text?.Trim() ?? string.Empty;

        host = null;
        foreach ((string spelling, HookHost candidate) in _spellings)
        {
            if (string.Equals(name, spelling, StringComparison.OrdinalIgnoreCase))
            {
                host = candidate;
                break;
            }
        }

        return host is not null;
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

        string toolName = Text(payload, "tool_name") ?? Text(payload, "toolName") ?? string.Empty;
        string? workingDirectory = Text(payload, "cwd") ?? Text(payload, "workingDirectory");

        JsonObject? input = payload["tool_input"] as JsonObject ?? payload["toolInput"] as JsonObject;

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
    /// <param name="host">The host that asked.</param>
    /// <param name="decision">What was decided.</param>
    /// <param name="reason">Why, when it was a refusal.</param>
    /// <returns>The JSON to write to standard output.</returns>
    public static string WriteDecision(HookHost host, HookDecision decision, string? reason)
    {
        string spelled = decision == HookDecision.Deny ? "deny" : "allow";

        JsonObject payload = host switch
        {
            // Claude Code nests the decision under the event it belongs to.
            HookHost.Claude => new JsonObject
            {
                ["hookSpecificOutput"] = new JsonObject
                {
                    ["hookEventName"] = "PreToolUse",
                    ["permissionDecision"] = spelled,
                    ["permissionDecisionReason"] = reason ?? string.Empty,
                },
            },

            // Copilot reads the same two fields at the top level.
            HookHost.Copilot => new JsonObject
            {
                ["permissionDecision"] = spelled,
                ["permissionDecisionReason"] = reason ?? string.Empty,
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

            // Reached only if a Copilot turn-end event appears and the command line stops refusing
            // it. Written in the same shape rather than left to throw, so the day that happens is a
            // test failure about a field name and not a crash in a hook.
            HookHost.Copilot => new JsonObject
            {
                ["decision"] = "block",
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
