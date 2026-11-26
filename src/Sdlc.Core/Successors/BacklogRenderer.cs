using System.Globalization;
using System.Text;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Successors;

/// <summary>
/// The backlog: every successor and every parked cycle, in the waves they come ready in (D-145).
/// </summary>
/// <remarks>
/// <para><strong>A view, never a file.</strong> Worked out from the successor files and the parked cycles on every
/// read, so nothing stored can go stale and nothing shared is edited from two sandboxes at once.</para>
/// <para><strong>A wave is how many merges stand between an item and its start.</strong> Ready now is the first;
/// an item waiting only on cycles already open is the next; one waiting on that is the one after. Stuck and unresolved
/// items are listed apart, since no merge makes them ready, and so are held ones, which a person starts (D-220).</para>
/// </remarks>
public static class BacklogRenderer
{
    private const string Indent = "  ";

    /// <summary>Renders the backlog.</summary>
    /// <param name="read">Where every successor and parked cycle stands.</param>
    /// <param name="onto">The branch readiness was judged against, when one was named rather than the integration branch.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(ReadSuccessors.Response read, string? onto = null)
    {
        ArgumentNullException.ThrowIfNull(read);

        List<Item> items =
        [
            .. read.Successors.Select(s => new Item(
                "successor:" + s.Successor.Slug,
                s.Successor.Headline,
                s.State,
                s.Waits,
                "/sdlc:intent --from successor:" + s.Successor.Slug,
                FirstLine(s.Successor.Outcome),
                s.Successor.Held)),
            .. read.Waiting.Select(p => new Item(
                p.Cycle + " (parked)",
                p.Headline,
                p.State,
                p.Waits,
                "sdlc unpark " + p.Cycle + ", then read its spec and plan again",
                null,
                null)),
        ];

        StringBuilder output = new();

        if (onto is not null)
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Judged against {onto}: a predecessor is merged once its passing verify is committed there."));
            output.AppendLine();
        }

        if (items.Count == 0)
        {
            output.AppendLine("The backlog is empty: no successor waits, and no cycle is parked.");
            return output.ToString();
        }

        Dictionary<string, int> depth = new(StringComparer.Ordinal);
        var byName = items.ToDictionary(Key, StringComparer.Ordinal);

        foreach (IGrouping<int, Item> wave in items
            .Where(i => i.State is SuccessorState.Ready or SuccessorState.Waiting)
            .GroupBy(i => Depth(i, byName, depth, []))
            .OrderBy(g => g.Key))
        {
            output.AppendLine(wave.Key == 0
                ? "Ready now:"
                : string.Create(CultureInfo.InvariantCulture, $"Wave {wave.Key + 1}, after {wave.Key} more merge{(wave.Key == 1 ? string.Empty : "s")}:"));

            foreach (Item item in wave)
            {
                Line(output, item);
                output.AppendLine(Indent + Indent + "  " + (item.State == SuccessorState.Ready ? "start it: " + item.Start : "waits on: " + string.Join(", ", item.Waits.Select(ReadSuccessors.Describe))));
            }

            output.AppendLine();
        }

        Item[] blocked = [.. items.Where(i => i.State is SuccessorState.Stuck or SuccessorState.Unresolved)];

        if (blocked.Length > 0)
        {
            output.AppendLine("No merge makes these ready:");

            foreach (Item item in blocked)
            {
                Line(output, item);
                output.AppendLine(Indent + Indent + "  " + (item.State == SuccessorState.Stuck ? "stuck: " : "unresolved: ") + string.Join(", ", item.Waits.Select(ReadSuccessors.Describe)));
            }

            output.AppendLine();
        }

        Item[] held = [.. items.Where(i => i.State == SuccessorState.Held)];

        if (held.Length > 0)
        {
            output.AppendLine("On hold -- no wave takes these; a person starting one lifts its hold:");

            foreach (Item item in held)
            {
                Line(output, item);
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Indent}{Indent}  held since {item.Held:yyyy-MM-dd}; start it: {item.Start}"));
            }

            output.AppendLine();
        }

        return output.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void Line(StringBuilder output, Item item)
    {
        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}{item.Name}  {item.Headline ?? "(no headline)"}"));

        if (item.Outcome is not null)
        {
            output.AppendLine(Indent + Indent + "  builds: " + item.Outcome);
        }
    }

    /// <summary>What a successor's outcome says first: its first sentence, or its first line when it has no full stop.</summary>
    private static string? FirstLine(string? outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome))
        {
            return null;
        }

        string line = outcome.Trim().ReplaceLineEndings("\n").Split('\n', 2)[0].Trim().TrimStart('-', '*', ' ');
        int stop = line.IndexOf(". ", StringComparison.Ordinal);

        return stop > 0 ? line[..(stop + 1)] : line;
    }

    private static string Key(Item item) => item.Name.Replace(" (parked)", string.Empty, StringComparison.Ordinal);

    /// <summary>Merges between an item and its start: none when ready, else one more than what it waits on.</summary>
    private static int Depth(Item item, Dictionary<string, Item> byName, Dictionary<string, int> known, HashSet<string> visiting)
    {
        string key = Key(item);

        if (known.TryGetValue(key, out int done))
        {
            return done;
        }

        if (item.State == SuccessorState.Ready || !visiting.Add(key))
        {
            return known[key] = 0;
        }

        int deepest = 0;

        foreach (ReadSuccessors.Wait wait in item.Waits.Where(w => w.State != WaitState.Merged))
        {
            string waited = wait.Cycle?.ToString() ?? wait.Reference.ToString();

            deepest = Math.Max(deepest, byName.TryGetValue(waited, out Item? behind) || byName.TryGetValue(wait.Reference.ToString(), out behind)
                ? Depth(behind, byName, known, visiting)
                : 0);
        }

        return known[key] = deepest + 1;
    }

    private sealed record Item(string Name, string? Headline, SuccessorState State, IReadOnlyList<ReadSuccessors.Wait> Waits, string Start, string? Outcome, DateOnly? Held);
}
