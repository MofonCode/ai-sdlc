using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Sdlc.Core.Decisions;

/// <summary>
/// A recorded decision's identity.
/// </summary>
/// <remarks>
/// <para>A sequence of its own, <c>DEC_nn</c>, separate from every constraint tier. A constraint judges
/// an artifact and a decision explains a choice, so an id that could be either would be a citation
/// nobody could resolve.</para>
///
/// <para><strong>No revision suffix, which is the deliberate difference from a constraint id.</strong>
/// A constraint carries one because its wording can be improved without changing what it forbids, and a
/// finding judged under the old words was judged under the same rule. A decision forbids nothing, so
/// there is no equivalent invariant to hold the two wordings together — a rewritten reason *is* a
/// different reason. Either the entry stands as written or it is superseded by a new id.</para>
/// </remarks>
public readonly record struct DecisionId
{
    private static readonly Regex _pattern = new(
        @"^DEC_(?<number>[0-9]{2,4})$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private DecisionId(int number) => Number = number;

    /// <summary>The number, which is the identity.</summary>
    public int Number { get; }

    /// <summary>
    /// A newly assigned id.
    /// </summary>
    /// <param name="number">The number, which must never have been used before.</param>
    /// <returns>The id.</returns>
    public static DecisionId Of(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return new DecisionId(number);
    }

    /// <summary>Reads an id from its written form.</summary>
    /// <param name="text">The id as written, such as <c>DEC_03</c>.</param>
    /// <param name="id">The id, when this returns true.</param>
    /// <returns>True when the text is a well-formed decision id.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out DecisionId? id)
    {
        id = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Match match = _pattern.Match(text.Trim());

        if (!match.Success)
        {
            return false;
        }

        int number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);

        if (number < 1)
        {
            return false;
        }

        id = new DecisionId(number);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"DEC_{Number:00}");
}

/// <summary>
/// One choice this solution has made, with the alternatives that were real and the reason.
/// </summary>
/// <remarks>
/// <para><strong>Four parts, and <c>Instead of</c> is the load-bearing one.</strong> It is what
/// separates a decision from a fact: an entry whose alternatives were never real records something
/// nobody chose, and reading it costs attention at every cycle for nothing. The same job the
/// <c>Forbids</c> clause does for a constraint — the part that makes the entry arguable rather than
/// decoration.</para>
///
/// <para><strong>A decision forbids nothing.</strong> Where a choice does forbid something, that half is
/// a constraint or an architecture test, and the two are separate artifacts of one choice: the decision
/// says why, the rule forbids the alternative. Expect roughly one <c>LOC_</c> for every five or six of
/// these.</para>
///
/// <para><strong>It binds nothing today, and the gap is deliberate.</strong> <c>CON_15</c> makes a plan
/// say why it departs from a declared default, but its <c>Forbids</c> clause names the declared stack
/// specifically — so a plan departing from one of these breaks no rule. What the record buys is that a
/// cycle can read what was already settled instead of re-litigating it. Making it bind would take a new
/// global constraint rather than a reworded one, because what is forbidden would change (§7.4), and
/// that is a person's to write.</para>
/// </remarks>
/// <param name="Id">The decision's identity.</param>
/// <param name="Statement">The choice in one line, as the heading states it.</param>
/// <param name="Decided">What was chosen, concretely enough to act on.</param>
/// <param name="Because">
/// Why — the reason that is not recoverable from the code. Never a restatement of
/// <paramref name="Decided"/>.
/// </param>
/// <param name="InsteadOf">The alternatives that were real, and what each would have cost.</param>
/// <param name="From">
/// Where it was settled — a cycle id, or how else it was taken. Provenance is what makes the record
/// something nobody asserted on its own: an entry traced to an approved cycle carries a person's
/// signature through the ledger hash.
/// </param>
/// <param name="SupersededBy">
/// The decision that replaced this one, when it was reversed. A superseded decision stays on the page so
/// a cycle that cited it remains readable, the same discipline §20's own preamble states for the
/// framework's decisions.
/// </param>
public sealed record Decision(
    DecisionId Id,
    string Statement,
    string Decided,
    string Because,
    string InsteadOf,
    string From,
    DecisionId? SupersededBy = null)
{
    /// <summary>
    /// Whether this decision still stands. A superseded one is kept for the record and is not current.
    /// </summary>
    public bool IsStanding => SupersededBy is null;
}
