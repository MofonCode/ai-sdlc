using System.Globalization;
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

/// <summary>
/// The rule a finding rests on: its id, and the one line that states it.
/// </summary>
/// <remarks>
/// <para><strong>One object rather than two fields side by side.</strong> §7.1 calls a constraint's id
/// and its one-line statement together the constraint's <em>name</em>, so this is the pair the design
/// already describes rather than a second arrangement of it. Two sibling fields can be written apart
/// and drift; one object cannot be half-filled without the schema noticing.</para>
///
/// <para><strong>The heading is frozen here rather than resolved when something renders it.</strong> A
/// round is already a historical record pinned to the hash of the artifact above it, and this is the
/// wording the reader actually judged against -- which is what somebody reading a finished cycle months
/// later needs, not whatever the library says by then. It also keeps every reader of a finding free of
/// the constraint library: resolving an id at render time would make <c>sdlc status</c> and the gate
/// fail whenever the library does not parse, which is a new way for two commands that do not read it
/// today to break.</para>
///
/// <para><strong>Why this is not the copy CON_09 forbids.</strong> That rule forbids an artifact
/// reproducing a constraint <em>instead of</em> citing it, and the cost it names is two live copies
/// diverging with nothing to say which binds. Neither applies here: this sits beside the citation
/// rather than in place of it, and it is evidence of what was read rather than a second authority.
/// What keeps the copy from becoming a paraphrase is that the reader is told to copy the line verbatim,
/// and a round carrying a citation without one is rejected.</para>
/// </remarks>
/// <param name="Id">The constraint's id, carrying the revision when its author read one.</param>
/// <param name="Heading">
/// The rule in one line, copied verbatim from the set the round was judged against. Never a summary:
/// a reader who rewords the rule has produced a second version of it, which is the one thing a frozen
/// copy must not do.
/// </param>
public sealed record Citation(string? Id, string? Heading)
{
    /// <summary>
    /// The labelled line a renderer shows beneath a finding's id, or null when nothing was cited.
    /// </summary>
    /// <remarks>
    /// Here rather than at each call site, for the reason <c>ConstraintRenderer</c> gives for living
    /// beside its own model: five outputs show this line -- the status panel, the findings history, the
    /// suggestions listing, a gate refusal and an approval draft -- and five formats would be five
    /// answers to one question. The label is eight characters wide, which is what aligns it with the
    /// <c>remedy</c> label the findings history already prints beneath a statement.
    /// </remarks>
    /// <returns>The line, or null when there is no heading to show.</returns>
    public string? Line() => string.IsNullOrWhiteSpace(Heading)
        ? null
        : string.Create(CultureInfo.InvariantCulture, $"rule    {Heading}");
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
/// The constraint it rests on, as an id and the rule's own line. Required on a blocking finding, which
/// is what makes a blocker arguable rather than an opinion; optional on a suggestion, which may just be
/// an improvement nothing forbids. One citation and not a list: a finding is one objection resting on
/// one rule, which is what keeps the refiner's three dispositions meaning something, since a finding
/// answered against two of its three rules would be neither open nor answered. Two rules whose remedies
/// exclude each other is a conflict, which section 6 makes an ending of the loop rather than a finding.
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
    Citation? Cites,
    string Statement,
    string? Locus = null,
    string? Remedy = null,
    string? DispositionBy = null,
    DateTimeOffset? DispositionAt = null)
{
    /// <summary>Whether this finding stops the artifact advancing as things stand.</summary>
    public bool IsOpenBlocker => Severity == Severity.Blocking && Disposition == Disposition.Open;
}
