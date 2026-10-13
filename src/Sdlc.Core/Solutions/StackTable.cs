using System.Text.RegularExpressions;

namespace Sdlc.Core.Solutions;

/// <summary>
/// Reads the rows out of a declared stack.
/// </summary>
/// <remarks>
/// <para><strong>Read, never written.</strong> The declared stack is a person's file: adoption creates
/// it from the profile's template and the only thing that changes it afterwards is a person renaming a
/// draft over it (D-67). So this parses and nothing here emits — a writer would be the seam an agent
/// could satisfy <c>CON_15</c> through, by adding the row it wanted instead of arguing for it.</para>
///
/// <para><strong>Tolerant on purpose.</strong> A person owns the file, so it will have rows this did
/// not expect: extra columns, prose between tables, a concern worded differently from any profile's.
/// What it recognises is a two-cell pipe row, and what it ignores is everything else including the
/// header and the divider — because the alternative is a reader that reports a hand-edited file as
/// broken, which would teach somebody to stop reading what the gate says.</para>
/// </remarks>
public static class StackTable
{
    private static readonly Regex _rowPattern = new(
        @"^\s*\|\s*(?<concern>[^|]*?)\s*\|\s*(?<choice>[^|]*?)\s*\|\s*$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>One declared row: what is being decided, and what this solution chose.</summary>
    /// <param name="Concern">The left-hand cell.</param>
    /// <param name="Choice">The right-hand cell, which is empty where nothing is declared yet.</param>
    public sealed record Row(string Concern, string Choice);

    /// <summary>
    /// Every declared row in a document.
    /// </summary>
    /// <param name="markdown">The declared stack's text.</param>
    /// <returns>The rows in document order, header and divider excluded.</returns>
    public static IReadOnlyList<Row> Rows(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        List<Row> rows = [];

        foreach (string line in markdown.Split('\n'))
        {
            Match match = _rowPattern.Match(line.TrimEnd('\r'));

            if (!match.Success)
            {
                continue;
            }

            string concern = match.Groups["concern"].Value;

            // The header names the columns and the divider is dashes and colons. Neither is a row, and
            // keying on the content rather than on position means prose between two tables cannot
            // shift which lines get skipped.
            if (string.Equals(concern, "Concern", StringComparison.OrdinalIgnoreCase)
                || concern.Length == 0
                || concern.All(character => character is '-' or ':' or ' '))
            {
                continue;
            }

            rows.Add(new Row(concern, match.Groups["choice"].Value));
        }

        return rows;
    }
}
