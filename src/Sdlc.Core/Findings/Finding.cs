using System.Text.Json.Serialization;

namespace Sdlc.Core.Findings;

/// <summary>How much weight a finding carries.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Severity>))]
public enum Severity
{
    /// <summary>Stops the artifact advancing. Cites exactly one constraint.</summary>
    [JsonStringEnumMemberName("blocking")]
    Blocking,

    /// <summary>Noticed, does not block. Cites a constraint.</summary>
    [JsonStringEnumMemberName("suggestion")]
    Suggestion,

    /// <summary>
    /// An improvement that violates nothing and cites nothing.
    /// </summary>
    /// <remarks>
    /// New in iteration 4. Every finding previously had to cite a constraint, which left nowhere for
    /// "this would be better as X" to go.
    /// </remarks>
    [JsonStringEnumMemberName("advice")]
    Advice,
}

/// <summary>Where a finding has got to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Disposition>))]
public enum Disposition
{
    /// <summary>Outstanding.</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>The artifact changed to address it.</summary>
    [JsonStringEnumMemberName("answered")]
    Answered,

    /// <summary>Accepted at the gate, and named in the approval entry.</summary>
    [JsonStringEnumMemberName("waived")]
    Waived,

    /// <summary>Retracted by a later round.</summary>
    [JsonStringEnumMemberName("withdrawn")]
    Withdrawn,
}

/// <summary>One objection an interrogation raised.</summary>
/// <param name="Id">
/// The finding's id, unique for the artifact over the cycle's life. A finding carried into a later
/// round keeps its id; without that, an id in round two is either a new objection or an old one and
/// only prose distinguishes them.
/// </param>
/// <param name="Severity">How much weight it carries.</param>
/// <param name="Disposition">Where it has got to.</param>
/// <param name="Cites">The constraint it rests on, with its revision. Null only for advice.</param>
/// <param name="Statement">What is wrong, in full.</param>
/// <param name="Locus">Where in the artifact.</param>
/// <param name="Remedy">What would fix it.</param>
/// <param name="DispositionBy">Who moved it off open, when someone did.</param>
/// <param name="DispositionAt">When it was moved off open.</param>
public sealed record Finding(
    string Id,
    Severity Severity,
    Disposition Disposition,
    string? Cites,
    string Statement,
    string? Locus = null,
    string? Remedy = null,
    string? DispositionBy = null,
    DateTimeOffset? DispositionAt = null)
{
    /// <summary>Whether this finding stops the artifact advancing as things stand.</summary>
    public bool IsOpenBlocker => Severity == Severity.Blocking && Disposition == Disposition.Open;
}
