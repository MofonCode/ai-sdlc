using System.Globalization;
using Sdlc.Core.Constraints;

namespace Sdlc.Core.Decisions;

/// <summary>
/// How many words a recorded entry's headline and each of its clauses may take (D-149).
/// </summary>
/// <remarks>
/// <para><strong>The headline is the entry's own title.</strong> A reader is handed every entry's headline
/// and only some entries' bodies, so a title that runs to thirty words is a body nobody asked for. There is
/// no new field: the title is kept short.</para>
/// <para><strong>A budget is held where an entry is written, never where one is read.</strong> Mining's
/// drafter refuses a new entry over it, so the person shortens the wording before placing it; an entry
/// already on the page is noted and proposed for rewording, never refused, because the file is a person's
/// and refusing what they placed would refuse the record itself.</para>
/// <para>Counting words is deterministic, so it is the binary's. Whether the shorter wording still says what
/// was decided is a reader's, which is why the drafter names the overrun and leaves the rewrite to the
/// person.</para>
/// </remarks>
internal static class EntryBudget
{
    /// <summary>The most words a title may take: what was decided, not why.</summary>
    public const int Headline = 15;

    /// <summary>The most words each clause may take.</summary>
    public const int Clause = 60;

    /// <summary>One part of an entry over its budget.</summary>
    /// <param name="Part">The part, as the entry labels it: <c>title</c>, <c>decided</c>, <c>forbids</c>.</param>
    /// <param name="Words">How many words it takes.</param>
    /// <param name="Budget">How many it may.</param>
    public sealed record Overrun(string Part, int Words, int Budget);

    /// <summary>Counts words as a person would: runs of text between whitespace.</summary>
    /// <param name="text">The text.</param>
    /// <returns>How many words it holds.</returns>
    public static int Words(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>The parts of a decision over budget. <c>From</c> is provenance and is not counted.</summary>
    /// <param name="decision">The decision.</param>
    /// <returns>Each part over budget, in the order the entry writes them; empty when it fits.</returns>
    public static IReadOnlyList<Overrun> Of(Decision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return Over(
            ("title", decision.Statement, Headline),
            ("decided", decision.Decided, Clause),
            ("because", decision.Because, Clause),
            ("instead of", decision.InsteadOf, Clause));
    }

    /// <summary>The parts of a rule over budget.</summary>
    /// <param name="constraint">The rule.</param>
    /// <returns>Each part over budget, in the order the entry writes them; empty when it fits.</returns>
    public static IReadOnlyList<Overrun> Of(Constraint constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);

        return Over(
            ("title", constraint.Statement, Headline),
            ("applies to", constraint.AppliesTo, Clause),
            ("forbids", constraint.Forbids, Clause),
            ("why", constraint.Why, Clause));
    }

    /// <summary>Overruns in one line: <c>title 31 of 15, decided 95 of 60</c>.</summary>
    /// <param name="overruns">The overruns.</param>
    /// <returns>The line.</returns>
    public static string Describe(IEnumerable<Overrun> overruns) =>
        string.Join(", ", overruns.Select(overrun => string.Create(
            CultureInfo.InvariantCulture,
            $"{overrun.Part} {overrun.Words} of {overrun.Budget}")));

    private static List<Overrun> Over(params (string Part, string Text, int Budget)[] parts) =>
        [.. parts
            .Select(part => new Overrun(part.Part, Words(part.Text), part.Budget))
            .Where(overrun => overrun.Words > overrun.Budget)];
}
