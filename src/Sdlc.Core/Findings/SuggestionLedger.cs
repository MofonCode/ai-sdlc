using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FluentResults;
using Sdlc.Core.Cycles;
using CycleFile = Sdlc.Core.Cycles.Cycle;

namespace Sdlc.Core.Findings;

/// <summary>What happened to a suggestion against one document in one round (D-115).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SuggestionEventKind>))]
public enum SuggestionEventKind
{
    /// <summary>A reader raised it, or raised it again after it had been answered.</summary>
    [JsonStringEnumMemberName("raised")]
    Raised,

    /// <summary>A later reader found the document had changed to address it.</summary>
    [JsonStringEnumMemberName("answered")]
    Answered,

    /// <summary>A later round retracted it, or raised it again as a blocker.</summary>
    [JsonStringEnumMemberName("withdrawn")]
    Withdrawn,
}

/// <summary>One line of a suggestion's history: one document, one round, one event (D-115).</summary>
/// <param name="Artifact">The document's name: <c>intent</c>, <c>spec</c>, <c>plan</c> or <c>verify</c>.</param>
/// <param name="Round">That document's round.</param>
/// <param name="Event">What happened.</param>
/// <param name="By">Who recorded it, when the round said.</param>
/// <param name="At">When, when the round said.</param>
public sealed record SuggestionEvent(
    string Artifact,
    int Round,
    SuggestionEventKind Event,
    string? By = null,
    DateTimeOffset? At = null);

/// <summary>One concern a cycle's readers raised, stored once whichever documents raised it (D-115).</summary>
/// <remarks>
/// Everything but <paramref name="History"/> is fixed when it is first raised, as a ledger finding's is.
/// </remarks>
/// <param name="Id">The id it keeps for the life of the cycle, ending <c>S_nn</c>.</param>
/// <param name="Cites">The rule it rests on, when it rests on one.</param>
/// <param name="Statement">What is wrong.</param>
/// <param name="Locus">Where, in the document that first raised it.</param>
/// <param name="Remedy">What would fix it.</param>
/// <param name="Perspective">The perspective whose reader raised it, if one did.</param>
/// <param name="History">Every event, in the order the rounds ran.</param>
/// <param name="Gist">The one sentence a person reads first, when the reader gave one.</param>
public sealed record LedgerSuggestion(
    string Id,
    Citation? Cites,
    string Statement,
    string? Locus,
    string? Remedy,
    string? Perspective,
    IReadOnlyList<SuggestionEvent> History,
    string? Gist = null)
{
    /// <summary>The latest event against one document, or null when it never raised this.</summary>
    /// <param name="kind">The document.</param>
    /// <returns>The event.</returns>
    public SuggestionEvent? LatestOn(ArtifactKind kind) =>
        History.LastOrDefault(line => string.Equals(line.Artifact, Cycle.NameOf(kind), StringComparison.Ordinal));

    /// <summary>Whether any document's latest event still raises it.</summary>
    public bool IsOpen => Documents.Any(kind => LatestOn(kind)?.Event == SuggestionEventKind.Raised);

    /// <summary>Whether every document that raised it has since withdrawn it.</summary>
    public bool IsWithdrawn => Documents.All(kind => LatestOn(kind)?.Event == SuggestionEventKind.Withdrawn);

    /// <summary>The documents that raised it, in the order they first did.</summary>
    public IReadOnlyList<ArtifactKind> Documents =>
    [
        .. History
            .Select(line => Cycle.TryParseKind(line.Artifact, out ArtifactKind kind) ? kind : (ArtifactKind?)null)
            .OfType<ArtifactKind>()
            .Distinct(),
    ];

    /// <summary>The suggestion as a round's finding, at what one event made it.</summary>
    /// <param name="line">The event.</param>
    /// <returns>The finding.</returns>
    public Finding AsFinding(SuggestionEvent line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return new Finding(
            Id,
            Severity.Suggestion,
            SuggestionLedger.DispositionOf(line.Event),
            Cites,
            Statement,
            Locus,
            Remedy,
            line.By,
            line.At,
            Perspective,
            Gist);
    }
}

/// <summary>
/// Every suggestion one cycle's readers raised, against any of its documents, each concern once (D-115).
/// </summary>
/// <remarks>
/// <para><strong>It replaced a suggestion per document per round.</strong> The readers raise the same
/// concern against the intent, the spec and the plan, and an id that belonged to one document could not be
/// carried to the next, so one cycle came to 1,489 open suggestions that were mostly a handful of concerns
/// raised again under fresh ids. Here the id is the cycle's, and the reconciler carries it across documents.</para>
/// <para><strong>It is not chained, and need not be.</strong> A suggestion gates nothing, so an approval has
/// nothing here to anchor; what a person chose from these is in the intent, whose hash it pins. The hook is
/// what keeps an agent from writing it.</para>
/// <para><strong>The consumers read it through the rounds.</strong> <see cref="FindingsReader.ReadHistory"/>
/// folds each event into the round of the document it names, so the gate, the panel and the loop guards
/// read one shape whichever file a finding lives in.</para>
/// </remarks>
/// <param name="Cycle">The cycle's id.</param>
/// <param name="Suggestions">Each concern once, in the order first raised.</param>
public sealed partial record SuggestionLedger(string? Cycle, IReadOnlyList<LedgerSuggestion> Suggestions)
{
    /// <summary>The file's name in a cycle's <c>findings</c> directory.</summary>
    public const string FileName = "suggestions.json";

    /// <summary>A cycle with no suggestions yet.</summary>
    /// <param name="cycle">The cycle's id.</param>
    /// <returns>The empty ledger.</returns>
    public static SuggestionLedger Empty(string? cycle) => new(cycle, []);

    /// <summary>Whether an id is in the form a suggestion's takes: anything, then <c>S_</c> and a number.</summary>
    /// <param name="id">The id.</param>
    /// <returns>True when it is.</returns>
    public static bool IsSuggestionId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return SuggestionIdPattern().IsMatch(id);
    }

    /// <summary>The disposition a round view shows for an event.</summary>
    /// <param name="kind">The event.</param>
    /// <returns>The disposition.</returns>
    public static Disposition DispositionOf(SuggestionEventKind kind) => kind switch
    {
        SuggestionEventKind.Raised => Disposition.Open,
        SuggestionEventKind.Answered => Disposition.Answered,
        _ => Disposition.Withdrawn,
    };

    /// <summary>One suggestion by id, or null.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The suggestion.</returns>
    public LedgerSuggestion? Find(string id) =>
        Suggestions.FirstOrDefault(suggestion => string.Equals(suggestion.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// What one round of one document said about suggestions, as findings of that round's view.
    /// </summary>
    /// <param name="kind">The document.</param>
    /// <param name="round">Its round.</param>
    /// <returns>Each suggestion the round raised or moved, at what it made it.</returns>
    public IReadOnlyList<Finding> ViewOf(ArtifactKind kind, int round) =>
    [
        .. Suggestions.SelectMany(suggestion => suggestion.History
            .Where(line => line.Round == round
                && string.Equals(line.Artifact, CycleFile.NameOf(kind), StringComparison.Ordinal))
            .Select(suggestion.AsFinding)),
    ];

    /// <summary>
    /// Checks every history: a document it names, a round from one, and events that alternate per document.
    /// </summary>
    /// <param name="source">What to call the file in an error message.</param>
    /// <returns>Success, or a failure naming the first suggestion that does not hold.</returns>
    public Result Verify(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        HashSet<string> ids = new(StringComparer.Ordinal);

        foreach (LedgerSuggestion suggestion in Suggestions)
        {
            if (!ids.Add(suggestion.Id))
            {
                return Fail(source, $"{suggestion.Id} is recorded twice. One id is one concern.");
            }

            if (suggestion.History.Count == 0)
            {
                return Fail(source, $"{suggestion.Id} has no history, so nothing raised it.");
            }

            Dictionary<string, SuggestionEventKind> latest = new(StringComparer.Ordinal);

            foreach (SuggestionEvent line in suggestion.History)
            {
                if (!CycleFile.TryParseKind(line.Artifact, out _) || line.Round < 1)
                {
                    return Fail(source, $"{suggestion.Id} names round {line.Round} of '{line.Artifact}', which is not a document's round.");
                }

                bool known = latest.TryGetValue(line.Artifact, out SuggestionEventKind before);

                // Within one document the events alternate: raised, then answered or withdrawn, then
                // raised. Anything else is a line no round writes, so the file was edited.
                bool alternates = known
                    ? (before == SuggestionEventKind.Raised) != (line.Event == SuggestionEventKind.Raised)
                    : line.Event == SuggestionEventKind.Raised;

                if (!alternates)
                {
                    return Fail(source, $"{suggestion.Id} goes from {(known ? Name(before) : "nothing")} to {Name(line.Event)} on {line.Artifact} in round {line.Round}. Against one document a suggestion is raised, then answered or withdrawn, then raised again; only sdlc round writes this file.");
                }

                latest[line.Artifact] = line.Event;
            }
        }

        return Result.Ok();
    }

    /// <summary>
    /// Records what one round of one document said about the cycle's suggestions (D-115).
    /// </summary>
    /// <remarks>
    /// A new id is a new concern, and must be free across the cycle and end <c>S_nn</c>. A known id is the
    /// same concern restated, in the same words; it records an event only when it moves where the suggestion
    /// stands on this document, so a reader restating it where it stands records nothing.
    /// </remarks>
    /// <param name="kind">The document the round judged.</param>
    /// <param name="round">The round's number.</param>
    /// <param name="given">The round's suggestions bound for this file.</param>
    /// <param name="taken">Every id the cycle's document ledgers already hold.</param>
    /// <returns>The ledger with the round recorded, or a failure naming what was refused.</returns>
    public Result<SuggestionLedger> Record(
        ArtifactKind kind,
        int round,
        IReadOnlyList<Finding> given,
        IReadOnlySet<string> taken)
    {
        ArgumentNullException.ThrowIfNull(given);
        ArgumentNullException.ThrowIfNull(taken);

        string artifact = CycleFile.NameOf(kind);
        List<LedgerSuggestion> suggestions = [.. Suggestions];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (Finding finding in given)
        {
            if (!seen.Add(finding.Id))
            {
                return Refuse($"{artifact} round {round} names {finding.Id} twice. One id is one concern.");
            }

            SuggestionEvent line = new(artifact, round, EventOf(finding.Disposition), finding.DispositionBy, finding.DispositionAt);
            int at = suggestions.FindIndex(suggestion => string.Equals(suggestion.Id, finding.Id, StringComparison.Ordinal));

            if (at < 0)
            {
                if (taken.Contains(finding.Id) || !IsSuggestionId(finding.Id))
                {
                    return Refuse($"{finding.Id} cannot be a new suggestion: {(taken.Contains(finding.Id) ? "a document's ledger already holds that id" : "a suggestion's id ends S_ and a number")}. A suggestion raised now takes a cycle-wide id; the next free one is {NextId(suggestions, finding.Id)}.");
                }

                if (line.Event != SuggestionEventKind.Raised)
                {
                    return Refuse($"{finding.Id} is new, and says it is {Name(line.Event)}. A suggestion is raised before anything can answer or withdraw it.");
                }

                suggestions.Add(new LedgerSuggestion(
                    finding.Id,
                    finding.Cites,
                    finding.Statement,
                    finding.Locus,
                    finding.Remedy,
                    finding.Perspective,
                    [line],
                    finding.Gist));
                continue;
            }

            LedgerSuggestion known = suggestions[at];

            if (!SameConcern(known, finding))
            {
                return Refuse($"{finding.Id} is already recorded as a different concern -- its statement or citation differs from what it was raised with. Carry a suggestion's id with its words unchanged, and give a new concern a new id: the next free one is {NextId(suggestions, finding.Id)}.");
            }

            SuggestionEventKind? before = known.LatestOn(kind)?.Event;

            if (before is null && line.Event != SuggestionEventKind.Raised)
            {
                return Refuse($"{finding.Id} was never raised against {artifact}.md, and round {round} says it is {Name(line.Event)} there. A round answers or withdraws only what was raised against the document it judged.");
            }

            // Where it already stands on this document, a restatement moves nothing, so it records
            // nothing. Answered and withdrawn are both not raised, so one does not follow the other.
            bool moves = before is null || (before == SuggestionEventKind.Raised) != (line.Event == SuggestionEventKind.Raised);

            if (moves)
            {
                suggestions[at] = known with { History = [.. known.History, line] };
            }
        }

        return Result.Ok(this with { Suggestions = suggestions });
    }

    /// <summary>The ledger as the file holds it.</summary>
    /// <param name="options">The serializer's options.</param>
    /// <returns>Its JSON.</returns>
    internal string Write(JsonSerializerOptions options) => JsonSerializer.Serialize(this, options) + "\n";

    /// <summary>The next free suggestion id under the same prefix as one given.</summary>
    private static string NextId(IEnumerable<LedgerSuggestion> suggestions, string id)
    {
        int dot = id.LastIndexOf('.');
        string prefix = dot < 0 ? string.Empty : id[..(dot + 1)];

        int highest = suggestions
            .Select(suggestion => suggestion.Id)
            .Where(known => known.StartsWith(prefix + "S_", StringComparison.Ordinal))
            .Select(known => int.TryParse(known[(prefix.Length + 2)..], NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();

        return string.Create(CultureInfo.InvariantCulture, $"{prefix}S_{highest + 1:D2}");
    }

    private static bool SameConcern(LedgerSuggestion known, Finding finding) =>
        string.Equals(known.Statement, finding.Statement, StringComparison.Ordinal)
        && string.Equals(known.Cites?.Id, finding.Cites?.Id, StringComparison.Ordinal)
        && string.Equals(known.Cites?.Heading, finding.Cites?.Heading, StringComparison.Ordinal);

    private static SuggestionEventKind EventOf(Disposition disposition) => disposition switch
    {
        Disposition.Open => SuggestionEventKind.Raised,
        Disposition.Answered => SuggestionEventKind.Answered,
        _ => SuggestionEventKind.Withdrawn,
    };

    private static string Name(SuggestionEventKind kind) => kind switch
    {
        SuggestionEventKind.Raised => "raised",
        SuggestionEventKind.Answered => "answered",
        _ => "withdrawn",
    };

    private static Result Fail(string source, string message) =>
        Result.Fail(string.Create(CultureInfo.InvariantCulture, $"{source}: {message}"));

    private static Result<SuggestionLedger> Refuse(string message) =>
        Result.Fail<SuggestionLedger>(string.Create(CultureInfo.InvariantCulture, $"{FileName}: {message}"));

    [GeneratedRegex(@"(^|\.)S_[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SuggestionIdPattern();
}
