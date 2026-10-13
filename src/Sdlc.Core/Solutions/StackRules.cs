using System.Globalization;
using System.Text.RegularExpressions;

namespace Sdlc.Core.Solutions;

/// <summary>
/// Reads which stack rules a declared stack switches on.
/// </summary>
/// <remarks>
/// <para><strong>What is listed is what binds.</strong> The rules that may name a language, framework
/// or package used to follow from a profile name recorded in <c>solution.md</c>, so a person reading
/// their own stack file could not see what that name brought with it. They are rows here instead: an
/// id, whether it is on, and what it holds a plan to. A row that says <c>false</c> is a rule this
/// solution was offered and declined; a rule with no row does not apply. An empty file has no rows and
/// so no rules, which is the same answer it gives about everything else (D-76).</para>
///
/// <para><strong>Three cells, where a declared default has two.</strong> <see cref="StackTable"/>
/// recognises a two-cell row and nothing else, so the two tables live in one file without either
/// reader counting the other's rows — a rule is not a default a plan can depart from, and a default is
/// not a rule.</para>
///
/// <para><strong>Read, never written</strong>, for the reason the rest of the file is: an agent able
/// to flip a row could drop the rule its own plan is about to be judged against.</para>
/// </remarks>
public static class StackRules
{
    /// <summary>The heading the rules sit under in a declared stack.</summary>
    public const string Heading = "## Rules";

    private static readonly Regex _rowPattern = new(
        @"^\s*\|\s*`?(?<id>PRO_[0-9]{2,4})`?\s*\|\s*(?<on>[^|]*?)\s*\|\s*(?<title>[^|]*?)\s*\|\s*$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>One rule a declared stack names.</summary>
    /// <param name="Id">The rule's id, without a revision.</param>
    /// <param name="On">Whether the solution is judged against it.</param>
    /// <param name="Title">What the row says the rule holds a plan to. For a reader; nothing binds on it.</param>
    public sealed record Row(string Id, bool On, string Title);

    /// <summary>
    /// Every rule row in a declared stack.
    /// </summary>
    /// <param name="markdown">The declared stack's text.</param>
    /// <returns>The rows in document order. Empty when the file names no rule at all.</returns>
    public static IReadOnlyList<Row> Rows(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        List<Row> rows = [];

        foreach (string line in markdown.Split('\n'))
        {
            Match match = _rowPattern.Match(line.TrimEnd('\r'));

            if (match.Success)
            {
                rows.Add(new Row(
                    match.Groups["id"].Value,
                    IsOn(match.Groups["on"].Value),
                    match.Groups["title"].Value));
            }
        }

        return rows;
    }

    /// <summary>
    /// The rules table as a declared stack carries it.
    /// </summary>
    /// <param name="rules">Each rule's id and what it holds a plan to.</param>
    /// <param name="on">Whether the rows are written switched on.</param>
    /// <returns>The header, the divider and one row per rule.</returns>
    public static IEnumerable<string> Table(IEnumerable<(string Id, string Title)> rules, bool on)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return
        [
            "| Rule | On | What it holds a plan to |",
            "|---|---|---|",
            .. rules.Select(rule => string.Create(
                CultureInfo.InvariantCulture,
                $"| `{rule.Id}` | {(on ? "true" : "false")} | {rule.Title} |")),
        ];
    }

    /// <summary>
    /// Whether a cell switches its rule on.
    /// </summary>
    /// <remarks>
    /// Anything that is not plainly yes is no. A person owns this file, so a cell will sooner or later
    /// say something this did not expect, and reading an unrecognised word as on would bind a solution
    /// to a rule on the strength of a typo.
    /// </remarks>
    private static bool IsOn(string cell) => cell.Trim().ToUpperInvariant() is "TRUE" or "YES" or "ON";
}
