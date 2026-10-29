using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Findings;

/// <summary>One non-blocking finding, with the cycle and artifact it was raised against.</summary>
/// <param name="Cycle">The cycle it came from.</param>
/// <param name="Artifact">The first document that raised it.</param>
/// <param name="Round">The latest round that said anything about it.</param>
/// <param name="Finding">The finding itself, as that round left it.</param>
/// <param name="RaisedOn">
/// Every document that raised it, in the order they first did. One document for a suggestion recorded before
/// the cycle's suggestions ledger (D-115), which belonged to the document it was raised against.
/// </param>
/// <param name="Status">Where it has got to, worked out from the record on this read (D-115).</param>
/// <param name="DecidedIn">The cycle whose intent, lead or abandonment decided it, when a person did.</param>
public sealed record Suggestion(
    CycleId Cycle,
    ArtifactKind Artifact,
    int Round,
    Finding Finding,
    IReadOnlyList<ArtifactKind>? RaisedOn = null,
    SuggestionStatus Status = SuggestionStatus.Open,
    CycleId? DecidedIn = null)
{
    /// <summary>The documents that raised it, never empty.</summary>
    public IReadOnlyList<ArtifactKind> Documents => RaisedOn is { Count: > 0 } raised ? raised : [Artifact];
}

/// <summary>Collects the open non-blocking findings, for one cycle or across every cycle.</summary>
public static class Suggestions
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddSuggestionsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks for the non-blocking findings a solution has accumulated.</summary>
    /// <param name="Id">One cycle, or null for every cycle in the solution.</param>
    /// <param name="All">Every suggestion whatever its status, rather than the open ones (D-115).</param>
    public sealed record Query(CycleId? Id = null, bool All = false) : IRequest<Response>;

    /// <summary>What was found.</summary>
    /// <remarks>
    /// A record around the list rather than the list itself, so this operation's response is its own
    /// shape like every other one's. It also leaves somewhere for a count or a cap to go later
    /// without changing what every caller takes.
    /// </remarks>
    /// <param name="Suggestions">Every open suggestion, or every suggestion when <paramref name="All"/> asked.</param>
    /// <param name="Noticed">
    /// What the builds wrote down and left alone. A different store from the rounds and listed apart from
    /// them: a suggestion was raised by a reader judging a document, and one of these was raised by the
    /// stage that was forbidden from fixing it.
    /// </param>
    /// <param name="All">Whether every status was asked for, rather than the open ones.</param>
    public sealed record Response(
        IReadOnlyList<Suggestion> Suggestions,
        IReadOnlyList<Noticed> Noticed,
        bool All = false);

    /// <summary>
    /// Collects the findings nobody had to act on.
    /// </summary>
    /// <remarks>
    /// A non-blocking finding is non-blocking rather than trivial. The trial's two were substantive -- that
    /// <c>dotnet test</c> did not actually run the suite, and that a criterion's proof rested entirely on a
    /// substituted source -- and both would have been lost without somewhere to read them back from.
    /// <para>
    /// A listing rather than an analysis, and deliberately so: it answers what is outstanding. What keeps
    /// recurring across cycles is <c>propose constraints</c>, which reads the same rounds for the other
    /// half of them -- the answered blockers included -- and reports a rule cited in three distinct
    /// cycles (D-69). Neither records which stage a problem was caused at; that half is still open.
    /// </para>
    /// </remarks>
    /// <param name="solution">The root of the solution being read.</param>
    /// <param name="repository">Git, for the <c>Noticed:</c> lines the builds left in their commits.</param>
    private sealed class Handler(SolutionRoot solution, IRepository repository)
        : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            IReadOnlyList<Cycle> every = CycleIndex.All(solution.Path);
            IReadOnlyList<Cycle> cycles = request.Id is { } id
                ? [new Cycle(id, solution.Path)]
                : every;

            List<Suggestion> suggestions = [];
            List<Noticed> noticed = [];
            List<IError> errors = [];
            string integration = await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken)
                .ConfigureAwait(false);

            // Read from every cycle whichever one is listed: a later cycle's lead accepts an earlier
            // cycle's suggestion, and its abandonment can decline one.
            IReadOnlyDictionary<string, SuggestionDecision> decisions =
                await SuggestionDecisions.Read(every, cancellationToken).ConfigureAwait(false);

            foreach (Cycle cycle in cycles)
            {
                foreach (ArtifactKind kind in Enum.GetValues<ArtifactKind>())
                {
                    // The document's own ledger alone: the suggestions it still holds are the ones
                    // raised before the cycle's suggestions ledger, and are listed per document.
                    Result<LedgerOnDisk> ledger =
                        await FindingsReader.ReadLedger(cycle, kind, cancellationToken).ConfigureAwait(false);

                    if (ledger.IsFailed)
                    {
                        errors.AddRange(ledger.Errors);
                        continue;
                    }

                    suggestions.AddRange(Recorded(cycle, kind, ledger.Value.Ledger.Views(), decisions));
                }

                Result<SuggestionLedger> raised =
                    await FindingsReader.ReadSuggestions(cycle, cancellationToken).ConfigureAwait(false);

                if (raised.IsFailed)
                {
                    errors.AddRange(raised.Errors);
                }
                else
                {
                    suggestions.AddRange(raised.Value.Suggestions.Select(suggestion => Listed(cycle, suggestion, decisions)));
                }

                noticed.AddRange(Findings.Noticed.OfCycle(repository, solution.Path, cycle.Id, integration));
            }

            return errors.Count > 0
                ? Result.Fail<Response>(errors)
                : Result.Ok(new Response(
                    [.. suggestions.Where(suggestion => request.All || suggestion.Status == SuggestionStatus.Open)],
                    noticed,
                    request.All));
        }

        /// <summary>
        /// The suggestions a document's own ledger holds, folded the same way standing blockers are.
        /// </summary>
        /// <remarks>
        /// A finding keeps its id across rounds, so the latest round that mentions one says where it got to.
        /// Reading every round flat would list one suggestion as many, and a list that over-counts is a list
        /// people stop reading.
        /// </remarks>
        private static IEnumerable<Suggestion> Recorded(
            Cycle cycle,
            ArtifactKind kind,
            IReadOnlyList<FindingsRound> rounds,
            IReadOnlyDictionary<string, SuggestionDecision> decisions)
        {
            // The shared fold, not a copy of it. This had its own, and it differed in a way that
            // mattered: it folded only over findings a round called a suggestion, so an objection
            // raised as a suggestion and re-raised as blocking kept the earlier mention as standing --
            // listed here as an open suggestion while the gate refused on it as an open blocker. One
            // fold means a panel's count and a refusal cannot describe the same finding differently.
            IReadOnlyList<Finding> standing = FindingsReader.Standing(rounds);

            return standing
                .Where(finding => finding.Severity != Severity.Blocking)
                .Select(finding =>
                {
                    SuggestionStatus read = finding.Disposition switch
                    {
                        Disposition.Open => SuggestionStatus.Open,
                        Disposition.Answered => SuggestionStatus.Answered,
                        _ => SuggestionStatus.Withdrawn,
                    };

                    return Decided(
                        new Suggestion(cycle.Id, kind, LatestMention(rounds, finding.Id), finding, Status: read),
                        decisions);
                });
        }

        /// <summary>One concern from the cycle's suggestions ledger, at its latest event.</summary>
        private static Suggestion Listed(
            Cycle cycle,
            LedgerSuggestion suggestion,
            IReadOnlyDictionary<string, SuggestionDecision> decisions)
        {
            SuggestionEvent latest = suggestion.History[^1];
            IReadOnlyList<ArtifactKind> documents = suggestion.Documents;
            SuggestionStatus read = suggestion.IsOpen
                ? SuggestionStatus.Open
                : suggestion.IsWithdrawn ? SuggestionStatus.Withdrawn : SuggestionStatus.Answered;

            return Decided(
                new Suggestion(cycle.Id, documents[0], latest.Round, suggestion.AsFinding(latest), documents, read),
                decisions);
        }

        /// <summary>A person's decision wins over where the readers left it (D-115).</summary>
        private static Suggestion Decided(Suggestion suggestion, IReadOnlyDictionary<string, SuggestionDecision> decisions) =>
            decisions.TryGetValue(SuggestionDecisions.Key(suggestion.Cycle, suggestion.Finding.Id), out SuggestionDecision? decision)
                ? suggestion with { Status = decision.Status, DecidedIn = decision.DecidedIn }
                : suggestion;

        /// <summary>Which round last said anything about a finding, which is the round that stands.</summary>
        private static int LatestMention(IReadOnlyList<FindingsRound> rounds, string id) => rounds
            .Where(round => round.Findings.Any(finding => string.Equals(finding.Id, id, StringComparison.Ordinal)))
            .Max(round => round.Round);
    }
}
