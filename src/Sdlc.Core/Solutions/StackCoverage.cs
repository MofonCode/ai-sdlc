using Sdlc.Core.Constraints;

namespace Sdlc.Core.Solutions;

/// <summary>
/// What this release ships that a declared stack does not mention.
/// </summary>
/// <remarks>
/// <para><strong>Unmentioned is not the same as declined, and that difference is the whole of this.</strong>
/// A rule row says <c>false</c> to decline, and a default row says <c>none</c> (D-93). So a concern or a rule
/// with no row at all is one this solution was never asked about -- most often because a release added it
/// after the stack was placed -- and saying so is how an upgrade reaches a person's file without anything
/// writing it. Before D-93 a default was declined by deleting its row, which made "declined" and "never
/// seen" the same absence, and any report of what was new would have offered every row a team had ever cut.
/// </para>
///
/// <para><strong>Against the default profile's rows, and only those.</strong> It is the one profile a draft
/// is written from (D-76); every other profile's rows are in the options menu to copy across, and a .NET
/// solution asked about experiment tracking would learn to stop reading this.</para>
///
/// <para><strong>An empty stack is told nothing.</strong> A file with no rows and no rules is a person
/// declaring that nothing is pinned, which is an answer about every concern at once.</para>
///
/// <para><strong>It reports and never writes.</strong> The declared stack is a person's file, and a tool
/// adding a row would be making the judgement this exists to put in front of somebody.</para>
/// </remarks>
public static class StackCoverage
{
    /// <summary>One shipped stack rule a declared stack has no row for.</summary>
    /// <param name="Id">Its id, without a revision.</param>
    /// <param name="Statement">What it holds a plan to.</param>
    public sealed record Rule(string Id, string Statement);

    /// <summary>What a declared stack does not mention.</summary>
    /// <param name="Defaults">The default profile's rows whose concern has no row in the stack.</param>
    /// <param name="Rules">The shipped stack rules with no row in the stack.</param>
    public sealed record Unmentioned(IReadOnlyList<StackDefault> Defaults, IReadOnlyList<Rule> Rules)
    {
        /// <summary>Nothing unmentioned.</summary>
        public static Unmentioned Nothing { get; } = new([], []);

        /// <summary>Whether the stack mentions everything this release ships.</summary>
        public bool IsEmpty => Defaults.Count == 0 && Rules.Count == 0;

        /// <summary>The concerns and rule ids, in one list, for a sentence.</summary>
        public IReadOnlyList<string> Names => [.. Defaults.Select(row => row.Concern), .. Rules.Select(rule => rule.Id)];
    }

    /// <summary>
    /// Compares a declared stack against what this release ships.
    /// </summary>
    /// <param name="markdown">The declared stack's text.</param>
    /// <returns>What it does not mention, or nothing for an empty stack.</returns>
    public static Unmentioned Of(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        IReadOnlyList<StackTable.Row> rows = StackTable.Rows(markdown);
        IReadOnlyList<StackRules.Row> rules = StackRules.Rows(markdown);

        if (rows.Count == 0 && rules.Count == 0)
        {
            return Unmentioned.Nothing;
        }

        HashSet<string> concerns = new(rows.Select(row => row.Concern.Trim()), StringComparer.OrdinalIgnoreCase);
        HashSet<string> listed = new(rules.Select(rule => rule.Id), StringComparer.OrdinalIgnoreCase);

        return new Unmentioned(
            [.. StackProfiles.Default.Defaults.Where(row => !concerns.Contains(row.Concern))],
            [.. ShippedProfileLibrary.Rules()
                .Select(rule => new Rule(ShippedProfileLibrary.RowId(rule), rule.Statement))
                .Where(rule => !listed.Contains(rule.Id))]);
    }

    /// <summary>
    /// The lines that list what is unmentioned and say how to answer each.
    /// </summary>
    /// <remarks>
    /// Shared by <c>sdlc stack</c> and the techstack draft's instructions, so the two cannot come to
    /// describe the same gap differently.
    /// </remarks>
    /// <param name="unmentioned">What the stack does not mention.</param>
    /// <param name="indent">What each listed item starts with.</param>
    /// <returns>The lines, or none when there is nothing unmentioned.</returns>
    public static IReadOnlyList<string> Describe(Unmentioned unmentioned, string indent)
    {
        ArgumentNullException.ThrowIfNull(unmentioned);

        if (unmentioned.IsEmpty)
        {
            return [];
        }

        int width = unmentioned.Defaults.Count == 0 ? 0 : unmentioned.Defaults.Max(row => row.Concern.Length) + 2;

        return
        [
            "Not mentioned: this release ships these and the stack has no row for them, so it was never",
            "asked. Answer each in a draft: a default takes the profile's choice, another, or 'none';",
            "a rule takes true or false. A row that says no is how the next release knows not to ask.",
            string.Empty,
            .. unmentioned.Defaults.Select(row => indent + row.Concern.PadRight(width) + row.Choice),
            .. unmentioned.Rules.Select(rule => indent + rule.Id.PadRight(Math.Max(width, 10)) + rule.Statement),
        ];
    }
}
