using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Sdlc.Core.Constraints;

/// <summary>The tier a constraint resolves in.</summary>
public enum ConstraintTier
{
    /// <summary>A <c>CON_nn</c> rule, from the framework's own library.</summary>
    Global,

    /// <summary>A <c>PRO_nn</c> rule, from the stack profile.</summary>
    Profile,

    /// <summary>A <c>LOC_nn</c> rule, from the solution being worked on.</summary>
    Solution,
}

/// <summary>
/// A constraint's identity: its tier, its number, and the revision of its wording.
/// </summary>
/// <remarks>
/// The number is the identity and the revision is not. Two revisions of <c>CON_01</c> forbid the
/// same thing in different words, so a finding citing either is a finding about the same rule;
/// a change to what is forbidden takes a new number instead, which is what keeps a historical
/// citation readable against the rule it was actually judged under.
/// </remarks>
public readonly record struct ConstraintId
{
    private static readonly Regex _pattern = new(
        @"^(?<tier>CON|PRO|LOC)_(?<number>[0-9]{2,4})(?:\.(?<revision>[0-9]{1,3}))?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private ConstraintId(ConstraintTier tier, int number, int revision)
    {
        Tier = tier;
        Number = number;
        Revision = revision;
    }

    /// <summary>
    /// A newly assigned id, at revision 1.
    /// </summary>
    /// <remarks>
    /// For allocating the next number in a tier. There is no overload taking a revision: a revision is
    /// what happens to wording that already exists, so it is read from a document rather than assigned.
    /// </remarks>
    /// <param name="tier">Which tier the id belongs to.</param>
    /// <param name="number">The number, which must never have been used in that tier before.</param>
    /// <returns>The id.</returns>
    public static ConstraintId Of(ConstraintTier tier, int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return new ConstraintId(tier, number, 1);
    }

    /// <summary>The tier this id resolves in.</summary>
    public ConstraintTier Tier { get; }

    /// <summary>The monotonic number within the tier. Assigned once, never reused.</summary>
    public int Number { get; }

    /// <summary>The wording revision. An id written without a suffix is revision 1.</summary>
    public int Revision { get; }

    /// <summary>The prefix for a tier, as it appears in an id.</summary>
    public static string PrefixOf(ConstraintTier tier) => tier switch
    {
        ConstraintTier.Global => "CON",
        ConstraintTier.Profile => "PRO",
        ConstraintTier.Solution => "LOC",
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown constraint tier."),
    };

    /// <summary>
    /// The tier's name as it is shown to a reader.
    /// </summary>
    /// <remarks>
    /// Spelled out rather than case-converted from the enum: lower-casing for display trips CA1308,
    /// and the rule has a point even here — a literal cannot be changed by a locale.
    /// </remarks>
    /// <param name="tier">The tier to name.</param>
    /// <returns>The tier's display name.</returns>
    public static string NameOf(ConstraintTier tier) => tier switch
    {
        ConstraintTier.Global => "global",
        ConstraintTier.Profile => "profile",
        ConstraintTier.Solution => "solution",
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown constraint tier."),
    };

    /// <summary>
    /// Parses an id such as <c>CON_01</c> or <c>CON_01.2</c>.
    /// </summary>
    /// <param name="text">The id to parse.</param>
    /// <param name="id">The parsed id, when this returns true.</param>
    /// <returns>True when <paramref name="text"/> is a well-formed id.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out ConstraintId? id)
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

        ConstraintTier tier = match.Groups["tier"].Value switch
        {
            "CON" => ConstraintTier.Global,
            "PRO" => ConstraintTier.Profile,
            _ => ConstraintTier.Solution,
        };

        int number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
        Group revisionGroup = match.Groups["revision"];
        int revision = revisionGroup.Success
            ? int.Parse(revisionGroup.Value, CultureInfo.InvariantCulture)
            : 1;

        id = new ConstraintId(tier, number, revision);
        return true;
    }

    /// <summary>
    /// Renders the id as it is written and cited. Revision 1 carries no suffix, so the common case
    /// reads as <c>CON_01</c> rather than <c>CON_01.1</c>.
    /// </summary>
    /// <returns>The id as text.</returns>
    public override string ToString()
    {
        string stem = string.Create(
            CultureInfo.InvariantCulture,
            $"{PrefixOf(Tier)}_{Number:D2}");

        return Revision == 1
            ? stem
            : string.Create(CultureInfo.InvariantCulture, $"{stem}.{Revision}");
    }
}
