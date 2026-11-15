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
                    || Completion.AbandonedOn(repository, solution.Path, integration, id) is not { } branch)
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

            return Result.Ok(new Response(gathered));
        }
    }
}
