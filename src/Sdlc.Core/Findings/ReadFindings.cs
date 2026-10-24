using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Findings;

/// <summary>One artifact's interrogation history, as it stands.</summary>
/// <param name="Artifact">Which document was judged.</param>
/// <param name="Rounds">Every round recorded for it, in round order.</param>
/// <param name="Standing">Each finding once, at the disposition the latest round that mentions it gave it.</param>
/// <param name="Conflicts">
/// The rules the latest round found cannot both be satisfied. The latest round's and no others, for the
/// reason <see cref="FindingsReader.StandingConflicts"/> gives.
/// </param>
public sealed record ArtifactFindings(
    ArtifactKind Artifact,
    IReadOnlyList<FindingsRound> Rounds,
    IReadOnlyList<Finding> Standing,
    IReadOnlyList<Conflict> Conflicts);

/// <summary>
/// Reads what the interrogations of a cycle said.
/// </summary>
/// <remarks>
/// <para><strong>Deterministic, so it is a command rather than a skill.</strong> Reading rounds off disk
/// and folding them needs no language; an agent spending a turn to print JSON it could have piped is
/// cost with nothing bought. The split is the same one the whole framework runs on.</para>
/// <para>It reports every finding and not only the open ones, which is what separates it from
/// <see cref="Suggestions"/>: that answers "what has this solution noticed and not acted on", across
/// cycles, and this answers "what was said about this document, and where did each objection get to".
/// An answered blocker is the most interesting line in a finished cycle and the other command cannot
/// show it.</para>
/// </remarks>
public static class ReadFindings
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddReadFindingsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>What the interrogations said.</summary>
    /// <param name="Cycle">The cycle read.</param>
    /// <param name="Artifacts">One entry per artifact asked about, in chain order.</param>
    public sealed record Response(Cycle Cycle, IReadOnlyList<ArtifactFindings> Artifacts);

    /// <summary>Asks what the interrogations of a cycle said.</summary>
    /// <param name="Id">The cycle to read.</param>
    /// <param name="Artifact">One document, or null for every one that has a round.</param>
    /// <param name="Round">
    /// One round, or null for every round. A round nobody recorded is an empty history rather than a
    /// failure: asking about round three of a document interrogated twice is a question with an answer.
    /// </param>
    public sealed record Query(CycleId Id, ArtifactKind? Artifact = null, int? Round = null)
        : IRequest<Response>;

    /// <summary>Reads the rounds and folds them.</summary>
    /// <param name="solution">The root of the solution being governed.</param>
    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);

            if (!cycle.Exists)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has no directory at {cycle.Directory}."));
            }

            IReadOnlyList<ArtifactKind> asked = request.Artifact is { } one
                ? [one]
                : [.. Cycle.PlanningDocuments, ArtifactKind.Verify];

            List<ArtifactFindings> artifacts = [];

            foreach (ArtifactKind kind in asked)
            {
                Result<IReadOnlyList<FindingsRound>> history = await FindingsReader
                    .ReadHistory(cycle, kind, cancellationToken)
                    .ConfigureAwait(false);

                if (history.IsFailed)
                {
                    return history.ToResult<Response>();
                }

                // Filtered after the fold, never before it. A finding keeps its id across rounds, so
                // "where does F_01 stand" is a question about every round -- answering it from round one
                // alone would report an answered blocker as open, which is the one mistake this output
                // must not make.
                IReadOnlyList<Finding> standing = FindingsReader.Standing(history.Value);

                IReadOnlyList<FindingsRound> rounds = request.Round is { } wanted
                    ? [.. history.Value.Where(round => round.Round == wanted)]
                    : history.Value;

                artifacts.Add(new ArtifactFindings(
                    kind,
                    rounds,
                    standing,
                    FindingsReader.StandingConflicts(history.Value)));
            }

            return Result.Ok(new Response(cycle, artifacts));
        }
    }
}
