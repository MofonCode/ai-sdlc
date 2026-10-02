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
    private static readonly Regex NumberedItem = new(
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

                // A repeated heading accumulates rather than replacing. Two sections of one name is a
                // schema violation reported elsewhere; silently keeping only the second would hide half
                // the content from a check that is meant to be exhaustive.
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
            Match match = NumberedItem.Match(line);
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

        return items;
    }
}
