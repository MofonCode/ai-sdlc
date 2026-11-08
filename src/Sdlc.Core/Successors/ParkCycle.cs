using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Successors;

/// <summary>
/// The plan-time claim check: whether a cycle's plan needs a file only another unbuilt plan creates, and if so, parks
/// it (D-145).
/// </summary>
/// <remarks>
/// <para><strong>Against every plan not yet built</strong> — the cycles beside it in a sandbox, and those admitted and
/// waiting for a build — rather than only a slicing's siblings at admission. A dependency found at the plan is cheaper
/// than one found at the pull request, and cheaper still than one found by a build that cannot compile.</para>
/// <para><strong>Two answers, kept apart.</strong> Files two plans both name are a conflict a person meets at merge,
/// and are reported (D-86). A file only another plan creates is a dependency: the cycle cannot be built against the
/// integration branch as it stands, so it is parked — its spec and plan kept, its id kept, waiting on the cycle that
/// creates the file. A slicing that cut it wrong costs a parked cycle, not a thrown-away plan.</para>
/// </remarks>
public static class ParkCycle
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddParkCycleRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Checks a cycle's plan against every unbuilt plan, and parks it when it needs one of them.</summary>
    /// <param name="Id">The cycle.</param>
    public sealed record Command(CycleId Id) : IRequest<Response>;

    /// <summary>What the check found.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Written">The parked file written, or null when the cycle needs nothing unmerged.</param>
    /// <param name="AlreadyParked">Whether it was parked before this ran, in which case nothing was written.</param>
    /// <param name="Dependencies">Each plan whose files this one needs.</param>
    /// <param name="Shared">Each plan naming files this one names too: a conflict at merge, reported.</param>
    public sealed record Response(
        CycleId Id,
        Parked? Written,
        bool AlreadyParked,
        IReadOnlyList<ComputeClaimGraph.Dependency> Dependencies,
        IReadOnlyList<ComputeClaimGraph.Collision> Shared);

    private sealed class Handler(SolutionRoot solution, IRepository repository) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            Cycle cycle = new(request.Id, root);
            string own = cycle.PathOf(ArtifactKind.Plan);

            if (!File.Exists(own))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{request.Id} has no plan, so there is nothing to compare. The check runs once a plan is written."));
            }

            if (File.Exists(Parked.PathOf(cycle)))
            {
                return Result.Ok(new Response(request.Id, null, true, [], []));
            }

            List<ComputeClaimGraph.SlicePlan> plans =
            [
                ComputeClaimGraph.SlicePlan.Read(request.Id, await File.ReadAllTextAsync(own, cancellationToken).ConfigureAwait(false)),
            ];

            foreach (Cycle peer in CycleIndex.All(root).Where(c => c.Id != request.Id && !c.IsAbandoned))
            {
                string plan = peer.PathOf(ArtifactKind.Plan);

                if (File.Exists(plan) && !await Verified(peer, cancellationToken).ConfigureAwait(false))
                {
                    plans.Add(ComputeClaimGraph.SlicePlan.Read(peer.Id, await File.ReadAllTextAsync(plan, cancellationToken).ConfigureAwait(false)));
                }
            }

            // Nothing unbuilt has code here yet, so a file present now was present before any of them.
            ComputeClaimGraph.SlicingComparison comparison = ComputeClaimGraph.CompareSlicing(
                plans,
                path => File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))));

            ComputeClaimGraph.Dependency[] needs = [.. comparison.Dependencies.Where(d => d.Dependent == request.Id)];
            ComputeClaimGraph.Collision[] shared = [.. comparison.Shared.Where(c => c.First == request.Id || c.Second == request.Id)];

            if (needs.Length == 0)
            {
                return Result.Ok(new Response(request.Id, null, false, [], shared));
            }

            Parked parked = new(
                [.. needs.Select(d => d.On).Distinct().Order().Select(on => new SuccessorReference(on, null))],
                repository.IsRepository(root) ? repository.CommitOf(root, "HEAD") : null,
                string.Join(
                    " ",
                    needs.Select(d => string.Create(
                        CultureInfo.InvariantCulture,
                        $"{d.Dependent} needs {string.Join(", ", d.Files)}, which only {d.On} creates."))));

            await File.WriteAllTextAsync(Parked.PathOf(cycle), parked.Text(), cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(request.Id, parked, false, needs, shared));
        }

        private static async Task<bool> Verified(Cycle cycle, CancellationToken cancellationToken)
        {
            string verify = cycle.PathOf(ArtifactKind.Verify);

            if (!File.Exists(verify))
            {
                return false;
            }

            Result<FrontmatterDocument> split = Frontmatter.Read(
                await File.ReadAllTextAsync(verify, cancellationToken).ConfigureAwait(false),
                verify);

            return split.IsSuccess
                && string.Equals(Frontmatter.Key(split.Value.Yaml, "outcome"), "pass", StringComparison.OrdinalIgnoreCase);
        }
    }
}
