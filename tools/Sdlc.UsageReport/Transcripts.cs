using System.Globalization;
using System.Text.Json;

namespace Sdlc.UsageReport;

/// <summary>What one transcript file cost, and who it was.</summary>
/// <param name="Folder">The project folder the host filed it under.</param>
/// <param name="Session">The file's name: the session, or a subagent's id.</param>
/// <param name="Role">Who it was: a reader by kind, a subagent, or a session a person drove.</param>
/// <param name="Start">The first timestamp in it.</param>
/// <param name="Calls">Model calls, each counted once however many lines repeat it.</param>
/// <param name="Input">Input tokens neither read from nor written to a cache.</param>
/// <param name="CacheWrite">Input tokens written to a cache.</param>
/// <param name="CacheRead">Input tokens read from a cache.</param>
/// <param name="Output">Output tokens.</param>
/// <param name="MaxContext">The largest context one call carried.</param>
/// <param name="Head">The first prompt's opening, on one line.</param>
internal sealed record SessionUsage(
    string Folder,
    string Session,
    string Role,
    DateTimeOffset? Start,
    int Calls,
    long Input,
    long CacheWrite,
    long CacheRead,
    long Output,
    long MaxContext,
    string Head)
{
    /// <summary>
    /// Tokens weighted to one scale: input ×1, cache write ×1.25, cache read ×0.1, output ×5 — the scale
    /// the reviews that found where the tokens went used, so their numbers and this one compare.
    /// </summary>
    public double Weighted => Input + (1.25 * CacheWrite) + (0.1 * CacheRead) + (5.0 * Output);
}

/// <summary>Reads Claude Code's transcripts: one JSON object per line, one file per session or subagent.</summary>
/// <remarks>
/// Host-specific, which is why it is a tool and not the binary (D-46, D-121). Read-only, and tolerant: a
/// line it cannot parse is skipped, because a transcript is a log the host owns and changes.
/// </remarks>
internal static class Transcripts
{
    /// <summary>Every transcript under the projects directory whose folder name contains a filter.</summary>
    /// <param name="root">The host's projects directory.</param>
    /// <param name="filter">A substring of the folder name, or null for every folder.</param>
    /// <returns>Each file's usage.</returns>
    public static IEnumerable<SessionUsage> Read(string root, string? filter)
    {
        foreach (string folder in Directory.EnumerateDirectories(root)
            .Where(folder => filter is null || Path.GetFileName(folder).Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.jsonl", SearchOption.AllDirectories))
            {
                if (ReadOne(folder, file) is { Calls: > 0 } usage)
                {
                    yield return usage;
                }
            }
        }
    }

    private static SessionUsage? ReadOne(string folder, string file)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        long input = 0, write = 0, read = 0, output = 0, context = 0;
        int calls = 0;
        DateTimeOffset? start = null;
        string? entry = null;
        string? prompt = null;

        foreach (string line in File.ReadLines(file))
        {
            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                JsonElement row = document.RootElement;

                if (row.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (start is null
                    && Text(row, "timestamp") is { } stamp
                    && DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset at))
                {
                    start = at;
                }

                entry ??= Text(row, "entrypoint");
                prompt ??= Prompt(row);

                if (!row.TryGetProperty("message", out JsonElement message)
                    || message.ValueKind != JsonValueKind.Object
                    || !message.TryGetProperty("usage", out JsonElement usage)
                    || usage.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                // A call is written once per content block, so the same message id recurs; count it once.
                if (Text(message, "id") is { } id && !seen.Add(id))
                {
                    continue;
                }

                calls++;
                long i = Whole(usage, "input_tokens"), w = Whole(usage, "cache_creation_input_tokens"), r = Whole(usage, "cache_read_input_tokens");
                input += i;
                write += w;
                read += r;
                output += Whole(usage, "output_tokens");
                context = Math.Max(context, i + w + r);
            }
        }

        string head = (prompt ?? string.Empty).ReplaceLineEndings(" ");

        return new SessionUsage(
            Path.GetFileName(folder),
            Path.GetFileNameWithoutExtension(file),
            RoleOf(prompt ?? string.Empty, entry, file),
            start,
            calls,
            input,
            write,
            read,
            output,
            context,
            head.Length > 100 ? head[..100] : head);
    }

    /// <summary>
    /// Who a transcript was, from its first prompt: the binary's reader prompts say which reader they are
    /// for (D-117), and older ones open with the judging prompt the skills used to carry.
    /// </summary>
    private static string RoleOf(string prompt, string? entry, string file)
    {
        if (prompt.Contains("You are reconciling one interrogation round", StringComparison.Ordinal))
        {
            return "reader: reconcile";
        }

        if (prompt.Contains("You are adjudicating one build", StringComparison.Ordinal))
        {
            return "reader: adjudicate";
        }

        if (prompt.Contains("You are reading one document the way one perspective reads", StringComparison.Ordinal))
        {
            return "reader: perspective";
        }

        if (prompt.Contains("You are judging one document against a fixed set of rules", StringComparison.Ordinal))
        {
            return "reader: rules";
        }

        // The openings sessions wrote by hand before the binary wrote the prompts, so the history classifies too.
        if (prompt.StartsWith("You are reading one document", StringComparison.Ordinal))
        {
            return "reader: perspective";
        }

        if (prompt.StartsWith("You are reconciling", StringComparison.Ordinal)
            || prompt.StartsWith("You read one document, the findings", StringComparison.Ordinal))
        {
            return "reader: reconcile";
        }

        if (prompt.StartsWith("You are verifying one build", StringComparison.Ordinal))
        {
            return "reader: adjudicate";
        }

        if (prompt.StartsWith("Group these findings", StringComparison.Ordinal)
            || prompt.StartsWith("Here is everything the record raised", StringComparison.Ordinal))
        {
            return "reader: propose";
        }

        if (file.Contains(Path.DirectorySeparatorChar + "subagents" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return "subagent";
        }

        return string.Equals(entry, "sdk-cli", StringComparison.Ordinal) ? "reader: other" : "session";
    }

    private static string? Prompt(JsonElement row)
    {
        string? type = Text(row, "type");

        if (type == "queue-operation")
        {
            return Text(row, "content");
        }

        if (type != "user"
            || !row.TryGetProperty("message", out JsonElement message)
            || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("content", out JsonElement content))
        {
            return null;
        }

        return content.ValueKind == JsonValueKind.String
            ? content.GetString()
            : content.ValueKind == JsonValueKind.Array
                ? content.EnumerateArray()
                    .Where(part => part.ValueKind == JsonValueKind.Object)
                    .Select(part => Text(part, "text"))
                    .FirstOrDefault(text => text is not null)
                : null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long Whole(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number)
            ? number
            : 0;
}
