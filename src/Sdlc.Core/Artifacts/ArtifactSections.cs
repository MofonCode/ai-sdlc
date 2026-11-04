using System.Globalization;
using System.Text.RegularExpressions;

namespace Sdlc.Core.Artifacts;

/// <summary>
/// Reads a document's second-level sections and their contents.
/// </summary>
/// <remarks>
/// The outline reader answers "which sections are there" for the panel. This answers "what is in one",
/// which is what the mechanical checks need: a criterion list, a proof list, a declared file set.
/// <para>
/// It reads markdown rather than asking an artifact to carry its content as data. That is deliberate:
/// an artifact is written for a person to read and argue with at a gate, and a format a person would
/// not choose to read is a format they stop reading.
/// </para>
/// </remarks>
public static class ArtifactSections
{
    // A numbered item, as a criterion list is written: "1." or "1)" at the start of a line, with the
    // number captured so a proof can be matched to it by number rather than by prose.
    private static readonly Regex _numberedItem = new(
        @"^\s*(?<number>[0-9]{1,3})[.)]\s+(?<text>\S.*)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Splits a document into its second-level sections.
    /// </summary>
    /// <param name="markdown">The document.</param>
    /// <returns>Each section's heading mapped to its lines, in document order.</returns>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Read(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        Dictionary<string, List<string>> sections = new(StringComparer.Ordinal);
        List<string>? current = null;
        bool inFrontmatter = false;
        bool seenFence = false;

        foreach (string raw in markdown.ReplaceLineEndings().Split(Environment.NewLine))
        {
            string line = raw.TrimEnd();

            if (line.Trim() == "---")
            {
                if (!seenFence)
                {
                    seenFence = true;
                    inFrontmatter = true;
                    continue;
                }

                if (inFrontmatter)
                {
                    inFrontmatter = false;
                    continue;
                }
            }

            if (inFrontmatter)
            {
                continue;
            }

            if (line.TrimStart().StartsWith("## ", StringComparison.Ordinal))
            {
                string heading = line.Trim()[3..].Trim();

                // A repeated heading accumulates rather than replacing, which is the safer of the two
                // behaviours given a duplicate exists: silently keeping only the second would hide half
                // the content from a check that is meant to be exhaustive.
                //
                // Two sections of one name is a schema violation, and for a while this comment said it
                // was "reported elsewhere" when nothing reported it -- so a second "## Acceptance
                // criteria" validated cleanly while its first criterion was discarded downstream by
                // NumberedItems. ArtifactSchema.CheckSections reports it now, which is what makes
                // accumulating safe rather than merely quiet.
                if (!sections.TryGetValue(heading, out current))
                {
                    current = [];
                    sections[heading] = current;
                }

                continue;
            }

            // A deeper heading closes nothing: the body is flat, so a "### " line is content.
            current?.Add(line);
        }

        return sections.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The numbered items in one section, keyed by their number.
    /// </summary>
    /// <remarks>
    /// Numbered rather than counted, because a proof cites a criterion by its number and a criterion
    /// deleted from the middle must not silently renumber the ones after it.
    /// </remarks>
    /// <param name="markdown">The document.</param>
    /// <param name="section">Which section to read.</param>
    /// <returns>Each item's number mapped to its text. Empty when the section is absent.</returns>
    public static IReadOnlyDictionary<int, string> NumberedItems(string markdown, string section)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        Dictionary<int, string> items = [];

        if (!Read(markdown).TryGetValue(section, out IReadOnlyList<string>? lines))
        {
            return items;
        }

        foreach (string line in lines)
        {
            Match match = _numberedItem.Match(line);
            if (!match.Success)
            {
                continue;
            }

            int number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);

            // The first mention wins, so a continuation line that happens to start with a number does
            // not overwrite the item it belongs to.
            if (!items.ContainsKey(number))
            {
                items[number] = match.Groups["text"].Value.Trim();
            }
        }

        // A proof written as a table (D-130): one row per case, its first cell the criterion's number. A
        // criterion's rows become one entry that opens with every test the rows name, so the chain reads
        // them as it reads a numbered entry and requires every one to have passed. A number the list already
        // gave keeps the list's entry.
        foreach (IGrouping<int, TableRow> criterion in TableRows(markdown, section).GroupBy(row => row.Number))
        {
            if (items.ContainsKey(criterion.Key))
            {
                continue;
            }

            string tests = string.Join(", ", criterion.Select(row => row.Test).Where(test => test.Length > 0));
            string rest = string.Join("; ", criterion.Select(row => string.Join(": ", row.Others.Where(cell => cell.Length > 0))));

            items[criterion.Key] = (tests + " " + rest).Trim();
        }

        return items;
    }

    /// <summary>
    /// Reads the rows of a table in a section whose first cell is a number.
    /// </summary>
    /// <remarks>
    /// The header names the columns, and the one headed <c>Test</c> is where a row's test names are. A cell
    /// is split on <c>|</c>, so a cell may not hold one.
    /// </remarks>
    /// <param name="markdown">The document.</param>
    /// <param name="section">Which section to read.</param>
    /// <returns>Each numbered row, in document order. Empty when the section holds no such table.</returns>
    public static IReadOnlyList<TableRow> TableRows(string markdown, string section)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        List<TableRow> rows = [];

        if (!Read(markdown).TryGetValue(section, out IReadOnlyList<string>? lines))
        {
            return rows;
        }

        int test = -1;

        foreach (string line in lines.Select(line => line.Trim()).Where(line => line.StartsWith('|')))
        {
            string[] cells = [.. line.Trim('|').Split('|').Select(cell => cell.Trim())];

            if (cells.All(cell => cell.Length == 0 || cell.All(c => c is '-' or ':')))
            {
                continue;
            }

            if (!int.TryParse(cells[0], NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            {
                test = Array.FindIndex(cells, cell => string.Equals(cell, "Test", StringComparison.OrdinalIgnoreCase));
                continue;
            }

            rows.Add(new TableRow(
                number,
                test > 0 && test < cells.Length ? cells[test] : string.Empty,
                [.. cells.Skip(1).Where((_, index) => index + 1 != test)]));
        }

        return rows;
    }
}

/// <summary>One numbered row of a table in an artifact's section.</summary>
/// <param name="Number">The criterion it belongs to.</param>
/// <param name="Test">What its <c>Test</c> column holds, or empty when there is none.</param>
/// <param name="Others">Its other cells after the number, in order.</param>
public sealed record TableRow(int Number, string Test, IReadOnlyList<string> Others);

