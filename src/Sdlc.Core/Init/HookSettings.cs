using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;

namespace Sdlc.Core.Init;

/// <summary>
/// Merges the framework's hook entries into a settings file the host defines and the team fills.
/// </summary>
/// <remarks>
/// <strong>The framework owns two hook entries in <c>.claude/settings.json</c>, not the file.</strong>
/// That file is the host's shared project settings -- permissions, environment, the team's own hooks --
/// and adoption used to write the whole of it from a template, in every mode and on every run, so a
/// <c>deny</c> rule or a hook of somebody's own was destroyed by the next upgrade and the destruction
/// was reported as "updated". Losing a hook is the failure the framework exists to prevent, arriving
/// through the framework. D-40 is the decision; <see cref="Initialise"/> is where it is applied.
/// <para>
/// The merge works on the inner hook objects rather than on the entries that group them, because a
/// team can legitimately put their own hook and this one under a single matcher. Every inner hook whose
/// command starts with <see cref="CommandPrefix"/> is removed and the framework's entries are appended
/// fresh, which makes the merge idempotent and makes an upgrade that changes a flag update the entry
/// rather than accumulate a second one. The one visible consequence is that a framework hook somebody
/// moved into their own matcher group moves back out into its own.
/// </para>
/// <para>
/// <strong>A file this cannot parse is not written at all.</strong> Relaxed parsing would accept a
/// settings file carrying comments or a trailing comma and then silently drop them on the way back
/// out, so the rule is strict-parse-or-print: the caller prints the entries for a person to place, and
/// the file is left byte for byte as it was. That is the pattern <c>AGENTS.md</c> already uses, for the
/// same reason.
/// </para>
/// <para>
/// <strong>Internal, unlike most of this assembly.</strong> Nearly everything here is public only so
/// the suite can reach it, which <c>PublicSurfaceTests</c> pins and calls out as a problem rather than
/// a design. This needs no such exemption: what matters is what adoption does to a settings file, so
/// every test goes through <see cref="Initialise"/> and the surface does not grow.
/// </para>
/// </remarks>
internal static class HookSettings
{
    /// <summary>
    /// What marks an inner hook entry as the framework's.
    /// </summary>
    /// <remarks>
    /// A prefix rather than the whole command, so a release that changes a flag replaces the entry it
    /// owns. Matching the full string would leave the old entry in place and append a second, and both
    /// would run.
    /// </remarks>
    public const string CommandPrefix = "sdlc hook";

    private static readonly JsonSerializerOptions _output = new()
    {
        WriteIndented = true,

        // The same relaxed escaping the hook's own output uses. A host settings file holds paths and
        // commands, and an apostrophe written as ' is legible to a parser and to nobody else.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Puts the framework's hook entries into an existing settings file, leaving everything else alone.
    /// </summary>
    /// <param name="existing">The file as it is now, or null when there is none.</param>
    /// <param name="framework">The framework's own settings template, which supplies the entries.</param>
    /// <returns>The merged file, or a failure saying why a merge could not be done safely.</returns>
    public static Result<string> Merge(string? existing, string framework)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(framework);

        if (string.IsNullOrWhiteSpace(existing))
        {
            return Result.Ok(framework);
        }

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(existing);
        }
        catch (JsonException error)
        {
            return Result.Fail<string>(string.Create(
                CultureInfo.InvariantCulture,
                $"it is not valid JSON, so the framework's hook entries cannot be merged into it without guessing. {error.Message}"));
        }

        if (parsed is not JsonObject current)
        {
            return Result.Fail<string>(
                "it holds something other than a JSON object, so there is nowhere for the framework's hook entries to go.");
        }

        if (JsonNode.Parse(framework) is not JsonObject ours || ours["hooks"] is not JsonObject oursByMoment)
        {
            throw new ArgumentException("The framework's own settings template has no hooks object.", nameof(framework));
        }

        if (current["hooks"] is not JsonObject theirsByMoment)
        {
            theirsByMoment = [];
            current["hooks"] = theirsByMoment;
        }

        foreach (KeyValuePair<string, JsonNode?> moment in oursByMoment)
        {
            if (moment.Value is not JsonArray ourEntries)
            {
                continue;
            }

            if (theirsByMoment[moment.Key] is not JsonArray entries)
            {
                entries = [];
                theirsByMoment[moment.Key] = entries;
            }

            Strip(entries);

            foreach (JsonNode? entry in ourEntries)
            {
                // Cloned because a node belongs to one parent, and the template is read again on the
                // next call.
                entries.Add(entry?.DeepClone());
            }
        }

        return Result.Ok(current.ToJsonString(_output) + Environment.NewLine);
    }

    /// <summary>
    /// Takes every framework hook out of one moment's entries, and drops an entry left holding none.
    /// </summary>
    /// <remarks>
    /// An entry is dropped only when this emptied it. A group the team wrote with no hooks in it is
    /// theirs, says something to whoever reads the file, and is none of adoption's business.
    /// </remarks>
    private static void Strip(JsonArray entries)
    {
        for (int index = entries.Count - 1; index >= 0; index--)
        {
            if (entries[index] is not JsonObject entry || entry["hooks"] is not JsonArray hooks)
            {
                continue;
            }

            bool removed = false;

            for (int hook = hooks.Count - 1; hook >= 0; hook--)
            {
                if (hooks[hook] is JsonObject candidate && IsFrameworks(candidate))
                {
                    hooks.RemoveAt(hook);
                    removed = true;
                }
            }

            if (removed && hooks.Count == 0)
            {
                entries.RemoveAt(index);
            }
        }
    }

    private static bool IsFrameworks(JsonObject hook) =>
        hook["command"] is JsonValue value
        && value.TryGetValue(out string? command)
        && command is not null
        && command.TrimStart().StartsWith(CommandPrefix, StringComparison.Ordinal);
}
