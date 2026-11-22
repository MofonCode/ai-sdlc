using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Harvest;

/// <summary>
/// Brings into the working tree each abandoned cycle whose abandonment never reached the integration branch, read from
/// the cycle's own branch, so a harvest reads it like any other (D-190).
/// </summary>
/// <remarks>
/// <para><strong>A cycle stopped before any of it merged lives only on its branch.</strong> Merging it first, only for
/// the close to delete it again, would be ceremony: what the harvest keeps from it reaches the integration branch through
/// the harvest's own pull request, and its folder never needs to. So its folder is restored from the branch, untracked,
/// and every aspect reads it where it reads the rest; the close deletes it from the working tree again.</para>
/// <para><strong>A rejected series the same way.</strong> A series' pull request closed without merging leaves every
/// cycle it ran on <c>series/&lt;id&gt;</c>; the one abandonment a person placed beside its record there brings in the
/// series' folder and each of those cycles' folders, and each reads as abandoned by the series.</para>
/// <para><strong>Nothing is fetched, checked out or staged.</strong> The branches are the ones this clone knows, its own
/// before the remote's. A cycle already closed, or already abandoned in the working tree, is left as it is.</para>
/// </remarks>
public static class GatherAbandoned
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddGatherAbandonedRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Brings in what is abandoned elsewhere.</summary>
    public sealed record Command : IRequest<Response>;

    /// <summary>One cycle brought in.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Branch">The branch its folder was read from.</param>
    public sealed record Gathered(CycleId Cycle, string Branch);

    /// <summary>What was brought in.</summary>
    /// <param name="Cycles">Each cycle brought in, with its branch; empty when there was none.</param>
    public sealed record Response(IReadOnlyList<Gathered> Cycles);

    private sealed class Handler(SolutionRoot solution, IRepository repository) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<HarvestLedger> ledger = await HarvestLedger.Read(solution.Path, cancellationToken).ConfigureAwait(false);

            if (ledger.IsFailed)
            {
                return ledger.ToResult<Response>();
            }

            string integration = await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken).ConfigureAwait(false);
            Series.SeriesAbandonment abandonments = new(repository, solution.Path, integration);
            List<Gathered> gathered = [];

            IEnumerable<CycleId> named = repository.Branches(solution.Path, CycleId.BranchPrefix)
                .Select(branch => branch.StartsWith("origin/", StringComparison.Ordinal) ? branch["origin/".Length..] : branch)
                .Select(branch => CycleId.TryParseBranch(branch, out CycleId? id) ? id : null)
                .Where(id => id is not null)
                .Select(id => id!.Value)
                .Distinct()
                .Order();

            foreach (CycleId id in named)
            {
                if (ledger.Value.Find(id.ToString()) is not null
                    || new Cycle(id, solution.Path).IsAbandoned
                    || Completion.AbandonedOn(repository, solution.Path, abandonments, id) is not { } branch)
                {
                    continue;
                }

                Result<string> restored = repository.Git(solution.Path, string.Create(
                    CultureInfo.InvariantCulture,
                    $"restore --source=\"{branch}\" --worktree -- \"{Cycle.SdlcDirectoryName}/{Cycle.CyclesDirectoryName}/{id}\""));

                if (restored.IsFailed)
                {
                    return restored.ToResult<Response>();
                }

                gathered.Add(new Gathered(id, branch));
            }

            Result series = Rejected(ledger.Value, abandonments, gathered);

            if (series.IsFailed)
            {
                return series;
            }

            return Result.Ok(new Response(gathered));
        }

        /// <summary>
        /// Every rejected series none of which merged: a <c>series/&lt;id&gt;</c> branch carrying the series'
        /// abandonment that the integration branch does not. Its folder and the folders of the cycles it ran are
        /// brought in from that branch, so each reads as abandoned by the series and a harvest reads it like any other.
        /// </summary>
        /// <remarks>
        /// The cycles are those the record names and those whose folders the branch changes against the integration
        /// branch: a successor whose cycle the record never resolved is still on the branch. <strong>Only the cycles
        /// the abandonment covers are brought in</strong> (D-208): a cycle whose folder is on the integration branch,
        /// or that was opened before the series started, came from there and stays as the integration branch holds
        /// it -- restoring the series' copy over it would overwrite it with work nobody accepted.
        /// </remarks>
        private Result Rejected(HarvestLedger ledger, Series.SeriesAbandonment abandonments, List<Gathered> gathered)
        {
            string root = solution.Path;
            IReadOnlyList<string> integrations = abandonments.Integrations;
            HashSet<string> seen = new(StringComparer.Ordinal);

            foreach ((string branch, string id) in abandonments.Branches)
            {
                if (!seen.Add(id))
                {
                    continue;
                }

                string folder = Series.SeriesRecord.RelativeDirectory(id);
                string abandoned = folder + "/" + Series.SeriesRecord.AbandonedFileName;

                if (!repository.PathExistsAt(root, branch, abandoned)
                    || integrations.Any(name => repository.PathExistsAt(root, name, abandoned))
                    || File.Exists(Series.SeriesRecord.AbandonedPathIn(root, id)))
                {
                    continue;
                }

                List<CycleId> cycles = [];
                Series.SeriesRecord? record = abandonments.Rejected(branch, id);

                if (record is not null)
                {
                    cycles.AddRange(record.Cycles);
                }

                if (integrations.Count > 0 && integrations[0] is { } target
                    && repository.MergeBase(root, branch, target) is { } divergence
                    && repository.CommittedChanges(root, divergence, branch) is { } changed)
                {
                    cycles.AddRange(Chain.VerifyChain.ChangedCycles(changed));
                }

                Result<string> restored = repository.Git(root, string.Create(CultureInfo.InvariantCulture, $"restore --source=\"{branch}\" --worktree -- \"{folder}\""));

                if (restored.IsFailed)
                {
                    return restored.ToResult();
                }

                foreach (CycleId cycle in cycles.Distinct().Order())
                {
                    string own = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, cycle.ToString());

                    if (ledger.Find(cycle.ToString()) is not null
                        || gathered.Any(entry => entry.Cycle == cycle)
                        || (record is not null && !record.Covers(cycle))
                        || abandonments.OnIntegration(cycle)
                        || !repository.PathExistsAt(root, branch, own + "/" + Cycle.FileNameOf(ArtifactKind.Intent)))
                    {
                        continue;
                    }

                    Result<string> brought = repository.Git(root, string.Create(CultureInfo.InvariantCulture, $"restore --source=\"{branch}\" --worktree -- \"{own}\""));

                    if (brought.IsFailed)
                    {
                        return brought.ToResult();
                    }

                    gathered.Add(new Gathered(cycle, branch));
                }
            }

            return Result.Ok();
        }
    }
}
