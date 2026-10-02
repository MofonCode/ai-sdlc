using System.Text.Json.Serialization;

namespace Sdlc.Core.Findings;

/// <summary>How much weight a finding carries.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Severity>))]
public enum Severity
{
    /// <summary>Stops the artifact advancing. Cites exactly one constraint.</summary>
    [JsonStringEnumMemberName("blocking")]
    Blocking,

    /// <summary>
    /// Noticed, does not block.
    /// </summary>
    /// <remarks>
    /// Non-blocking is not trivial. The trial's two suggestions were substantive -- that
    /// <c>dotnet test</c> did not actually run the suite, and that a criterion's proof rested entirely
    /// on a substituted source -- which is why they accumulate and are listed rather than discarded.
    /// <para>
    /// There are two severities and not three. A third, for an improvement that violates nothing and
    /// cites nothing, had no behaviour of its own: a citation is required only on a blocking finding,
    /// so an uncited non-blocking finding is already this. Its other purpose was a home for human
    /// steering, which belongs in the intent's fixed decisions instead.
    /// </para>
    /// </remarks>
    [JsonStringEnumMemberName("suggestion")]
    Suggestion,
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
/// <param name="Cites">
/// The constraint it rests on, with its revision. Required on a blocking finding, which is what makes
/// a blocker arguable rather than an opinion; optional on a suggestion, which may just be an
/// improvement nothing forbids.
/// </param>
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
