using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Drafts;
using Sdlc.Core.Findings;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Cycles;

/// <summary>Prints the record a person places to stop a cycle. Writes nothing.</summary>
public static class AbandonText
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddAbandonTextRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddTransient<IRequestHandler<Query, DraftResult>, Handler>()
            .AddTransient<IRequestHandler<SeriesQuery, DraftResult>, SeriesHandler>();
    }

    /// <summary>Asks for the record a person would place to stop a whole series, every cycle it ran with it.</summary>
    /// <param name="Id">The series to stop.</param>
    /// <param name="By">Who is stopping it, from the git identity, as for a cycle.</param>
    public sealed record SeriesQuery(string Id, string By) : IRequest<DraftResult>;

    /// <summary>Asks for the record a person would place to stop a cycle.</summary>
    /// <param name="Id">The cycle to stop.</param>
    /// <param name="By">
    /// Who is stopping it. Supplied by the caller from the git identity rather than invented here, for the
    /// same reason an approval names a person: a tool that filled this in would be taking the decision.
    /// </param>
    public sealed record Query(CycleId Id, string By) : IRequest<DraftResult>;

    /// <summary>
    /// Prints an abandonment record. Writes nothing.
    /// </summary>
    /// <remarks>
    /// The reason is left empty, because it is the only thing this file is for and nobody but the person
    /// stopping the cycle can write it. An agent stuck on a blocking finding has an obvious escape in
    /// abandoning and starting clean, and a hand-written reason is what keeps the objections that killed
    /// the cycle on the record rather than in a conversation nobody can find.
    /// </remarks>
    /// <param name="solution">The root of the solution being governed.</param>
    /// <param name="clock">
    /// What stamps the record. Injected rather than read from a static, which is what lets a test assert
    /// the stamped line instead of asserting around it — and it is the rule this framework bans the
    /// alternative to in every solution it scaffolds.
    /// </param>
    private sealed class Handler(SolutionRoot solution, TimeProvider clock)
        : IRequestHandler<Query, DraftResult>
    {
        /// <inheritdoc />
        public async Task<Result<DraftResult>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);

            if (!cycle.Exists)
            {
                return Result.Ok(DraftResult.Refused(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has no directory at {cycle.Directory}. There is nothing to abandon.")));
            }

            if (cycle.IsAbandoned)
            {
                return Result.Ok(DraftResult.Refused(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} was already abandoned. {cycle.AbandonmentPath} says why, and abandonment is terminal rather than a state to re-enter.")));
            }

            string entry = string.Join(
                Environment.NewLine,
                [
                    "---",
                    string.Create(CultureInfo.InvariantCulture, $"cycle: {cycle.Id}"),
                    string.Create(CultureInfo.InvariantCulture, $"by: {request.By}"),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"at: {clock.GetUtcNow():yyyy-MM-ddTHH:mm:ssZ}"),
                    "---",
                    string.Empty,
                    "# Why this cycle stopped",
                    string.Empty,
                ]) + Environment.NewLine;

            string instructions = string.Join(
                Environment.NewLine,
                [
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Create {cycle.AbandonedPath} with the text above, then write the reason under the heading."),
                    string.Empty,
                    "The reason is the only thing this cycle produced, and the file is rejected without one.",
                    "Say what was wrong with the intent, not that the cycle was abandoned - a repository",
                    "holding the intents that were tried and dropped, with the objections that killed them,",
                    "is readable history. A record saying only 'not needed' has thrown that away.",
                    string.Empty,
                    "Abandonment is terminal. This cycle will never build, even if it also holds an approval,",
                    "and there is no command to revive it. Open a new cycle instead.",
                    string.Empty,
                    "Nothing here writes that file. The hook denies every agent write to it.",
                ]);

            string path = cycle.PathOf(ArtifactKind.Intent);
            IReadOnlyList<string> accepted = File.Exists(path)
                ? SuggestionDecisions.Accepted(
                    await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
                    cycle.Id)
                : [];

            return Result.Ok(accepted.Count == 0
                ? DraftResult.Drafted(entry, instructions)
                : DraftResult.Drafted(entry + Accepted(accepted), instructions + AcceptedNote()));
        }

        /// <summary>
        /// The suggestions this cycle took in, each as a decline waiting for its reason (D-115).
        /// </summary>
        /// <remarks>
        /// Abandoning the cycle returns them to the pool by itself, because an abandoned cycle accepts
        /// nothing. What the person may want instead is to say none of them is wanted, and this is the one
        /// moment they are deciding; a line they keep is that decision, a line they delete is the default.
        /// </remarks>
        private static string Accepted(IReadOnlyList<string> accepted) => string.Join(
            Environment.NewLine,
            [
                string.Empty,
                "## Suggestions this cycle accepted",
                string.Empty,
                .. accepted.Select(id => "- declining " + id + ":"),
            ]) + Environment.NewLine;

        private static string AcceptedNote() => string.Join(
            Environment.NewLine,
            [
                string.Empty,
                string.Empty,
                "The suggestions this cycle accepted return to the pool once the file is placed: an abandoned",
                "cycle accepts nothing. Delete a line to leave its suggestion open for a later cycle, or",
                "write after its colon why it is not wanted at all, which declines it. A line kept with",
                "no reason declines nothing.",
            ]);
    }

    /// <summary>
    /// Prints a series' abandonment: one record beside the series' own, covering every cycle that exists only in the
    /// series, so a rejected series is stopped in one step rather than leaving each cycle neither merged nor abandoned
    /// (D-190, D-208).
    /// </summary>
    /// <remarks>
    /// The cycles it covers are listed in its frontmatter for the person to read; what decides coverage is the series'
    /// record, read whenever abandonment is asked about, so a successor the series started later is covered too. A
    /// cycle the series took from the integration branch is not listed and not covered: it stays there as it stands,
    /// to be built again, and the instructions name it so the person knows.
    /// </remarks>
    private sealed class SeriesHandler(SolutionRoot solution, TimeProvider clock, IRepository repository) : IRequestHandler<SeriesQuery, DraftResult>
    {
        public async Task<Result<DraftResult>> Handle(SeriesQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string id = request.Id.Trim();
            Result<Series.SeriesRecord> read = await Series.SeriesRecord.Read(solution.Path, id, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return Result.Ok(DraftResult.Refused(string.Join(" ", read.Errors.Select(error => error.Message))));
            }

            string place = Series.SeriesRecord.AbandonedPathIn(solution.Path, id);

            if (File.Exists(place))
            {
                return Result.Ok(DraftResult.Refused(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Series {id} was already abandoned. {place} says why, and abandonment is terminal rather than a state to re-enter.")));
            }

            Series.SeriesRecord record = read.Value.Resolved(await Successors.ReadSuccessors.Starts(solution.Path, cancellationToken).ConfigureAwait(false));
            Series.SeriesAbandonment abandonments = new(
                repository,
                solution.Path,
                await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken).ConfigureAwait(false));
            bool git = repository.IsRepository(solution.Path);
            IReadOnlyList<CycleId> covers = [.. record.Cycles.Where(cycle => record.Covers(cycle) && !(git && abandonments.OnIntegration(cycle)))];
            IReadOnlyList<CycleId> stays = [.. record.Cycles.Except(covers)];

            string entry = string.Join(
                Environment.NewLine,
                [
                    "---",
                    string.Create(CultureInfo.InvariantCulture, $"series: {id}"),
                    string.Create(CultureInfo.InvariantCulture, $"by: {request.By}"),
                    string.Create(CultureInfo.InvariantCulture, $"at: {clock.GetUtcNow():yyyy-MM-ddTHH:mm:ssZ}"),
                    covers.Count == 0 ? "covers: []" : "covers:",
                    .. covers.Select(cycle => "  - " + cycle),
                    "---",
                    string.Empty,
                    "# Why this series stopped",
                    string.Empty,
                ]) + Environment.NewLine;

            string instructions = string.Join(
                Environment.NewLine,
                [
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Create {place} with the text above, on {RunBranch.NameOf(RunKind.Series, id)}, then write the reason under the heading and commit it there."),
                    string.Empty,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"It stops every cycle that exists only in the series -- {(covers.Count == 0 ? "none yet" : string.Join(", ", covers))} -- and any successor it starts since,"),
                    "with one reason, so a harvest still reads what was rejected: from this branch, once the series' pull request",
                    "is closed without merging.",
                    .. stays.Count == 0
                        ? Array.Empty<string>()
                        : [
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"{string.Join(", ", stays)} came from the integration branch and {(stays.Count == 1 ? "is" : "are")} not stopped: {(stays.Count == 1 ? "it stays" : "they stay")} there as {(stays.Count == 1 ? "it stands" : "they stand")}, to be built again."),
                        ],
                    string.Empty,
                    "Say what was wrong with what the series built, not that it was rejected. The file is refused without a reason.",
                    "Abandonment is terminal: none of these cycles will build again. Open new cycles instead.",
                    string.Empty,
                    "Nothing here writes that file. The hook denies every agent write to it.",
                ]);

            return Result.Ok(DraftResult.Drafted(entry, instructions));
        }
    }
}
