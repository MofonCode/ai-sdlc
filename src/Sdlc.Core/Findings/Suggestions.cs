using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Findings;

/// <summary>One non-blocking finding, with the cycle and artifact it was raised against.</summary>
/// <param name="Cycle">The cycle it came from.</param>
/// <param name="Artifact">Which artifact it was raised against.</param>
/// <param name="Round">The round that raised it.</param>
/// <param name="Finding">The finding itself.</param>
public sealed record Suggestion(CycleId Cycle, ArtifactKind Artifact, int Round, Finding Finding);

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
    public sealed record Query(CycleId? Id = null) : IRequest<Response>;

    /// <summary>What was found.</summary>
    /// <remarks>
    /// A record around the list rather than the list itself, so this operation's response is its own
    /// shape like every other one's. It also leaves somewhere for a count or a cap to go later
    /// without changing what every caller takes.
    /// </remarks>
    /// <param name="Suggestions">Every open suggestion, newest round first within each cycle.</param>
    public sealed record Response(IReadOnlyList<Suggestion> Suggestions);

    /// <summary>
    /// Collects the findings nobody had to act on.
    /// </summary>
    /// <remarks>
    /// A non-blocking finding is non-blocking rather than trivial. The trial's two were substantive -- that
    /// <c>dotnet test</c> did not actually run the suite, and that a criterion's proof rested entirely on a
    /// substituted source -- and both would have been lost without somewhere to read them back from.
    /// <para>
    /// This is the whole of the cross-cycle learning loop that exists today. Iteration 2 built a corpus with
    /// an importer and computed signals; iteration 3 removed them and recorded the cost as "nothing
    /// aggregates automatically". The data to aggregate is in the rounds, which is what this reads -- a
    /// listing rather than an analysis, and deliberately so until there is a question worth answering.
    /// </para>
    /// </remarks>
    /// <param name="solution">The root of the solution being read.</param>
    private sealed class Handler(SolutionRoot solution)
        : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            IReadOnlyList<Cycle> cycles = request.Id is { } id
                ? [new Cycle(id, solution.Path)]
                : CycleIndex.All(solution.Path);

            List<Suggestion> suggestions = [];
            List<IError> errors = [];

            foreach (Cycle cycle in cycles)
            {
                foreach (ArtifactKind kind in Enum.GetValues<ArtifactKind>())
                {
                    Result<IReadOnlyList<FindingsRound>> history =
                        await FindingsReader.ReadHistory(cycle, kind, cancellationToken).ConfigureAwait(false);

                    if (history.IsFailed)
                    {
                        errors.AddRange(history.Errors);
                        continue;
                    }

                    suggestions.AddRange(Standing(cycle, kind, history.Value));
                }
            }

            return errors.Count > 0
                ? Result.Fail<Response>(errors)
                : Result.Ok(new Response(suggestions));
        }

        /// <summary>
        /// The suggestions still open, folded the same way standing blockers are.
        /// </summary>
        /// <remarks>
        /// A finding keeps its id across rounds, so the latest round that mentions one says where it got to.
        /// Reading every round flat would list one suggestion as many, and a list that over-counts is a list
        /// people stop reading.
        /// </remarks>
        private static IEnumerable<Suggestion> Standing(
            Cycle cycle,
            ArtifactKind kind,
            IReadOnlyList<FindingsRound> rounds)
        {
            Dictionary<string, (int Round, Finding Finding)> standing = new(StringComparer.Ordinal);

            foreach (FindingsRound round in rounds.OrderBy(round => round.Round))
            {
                foreach (Finding finding in round.Findings.Where(f => f.Severity == Severity.Suggestion))
                {
                    standing[finding.Id] = (round.Round, finding);
                }
            }

            return standing
                .Where(entry => entry.Value.Finding.Disposition == Disposition.Open)
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new Suggestion(cycle.Id, kind, entry.Value.Round, entry.Value.Finding));
        }
    }
}
