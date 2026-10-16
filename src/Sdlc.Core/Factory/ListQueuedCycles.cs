using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Factory;

/// <summary>
/// What the factory would build now, and what it would pass over and why.
/// </summary>
/// <remarks>
/// <para><strong>The queue is a listing, not a state anybody maintains</strong> (D-83, D-85). A cycle is
/// queued when it is approved for a build, has no verify and is not abandoned — which is the claim graph's
/// own answer, so "buildable" means one thing — <em>and</em> no <c>cycle/&lt;id&gt;/...</c> branch for it is
/// on the remote. A surviving plan branch means the plan may still be moving; a build branch means a person
/// or a factory run has taken it. Deleting the branch puts the cycle back.</para>
/// <para><strong>Every cycle passed over is named with its reason.</strong> A forgotten plan branch would
/// otherwise starve its cycle silently, night after night, which is the failure the sitrep exists to make
/// visible.</para>
/// </remarks>
public static class ListQueuedCycles
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddListQueuedCyclesRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>A cycle the factory would build.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Headline">What its intent is about, when it says.</param>
    public sealed record Queued(CycleId Id, string? Headline);

    /// <summary>A cycle the factory would pass over, and why.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Headline">What its intent is about, when it says.</param>
    /// <param name="Reason">Why it is held back, as a person would want to read it.</param>
    public sealed record HeldBack(CycleId Id, string? Headline, string Reason);

    /// <summary>Asks for the queue.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>The queue.</summary>
    /// <param name="Cycles">What would be built, oldest first.</param>
    /// <param name="Held">What would be passed over, oldest first.</param>
    /// <param name="RemoteRead">
    /// Whether the remote's branches could be read. False on a solution with no remote, or when git could
    /// not reach it — and then nothing is held back by a branch, which the renderer says.
    /// </param>
    public sealed record Response(IReadOnlyList<Queued> Cycles, IReadOnlyList<HeldBack> Held, bool RemoteRead);

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository)
        : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<ComputeClaimGraph.Response> graph = await mediator
                .Send<ComputeClaimGraph.Query, ComputeClaimGraph.Response>(new ComputeClaimGraph.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (graph.IsFailed)
            {
                return graph.ToResult<Response>();
            }

            IReadOnlyList<string>? remote = repository.RemoteBranches(solution.Path, CycleId.BranchPrefix);
            ILookup<CycleId, CycleBranch> branches = (remote ?? [])
                .Select(name => CycleBranch.TryParse(name, out CycleBranch? read) ? read : null)
                .OfType<CycleBranch>()
                .ToLookup(branch => branch.Id);

            List<Queued> queued = [];
            List<HeldBack> held = [];

            foreach (ComputeClaimGraph.Claim claim in graph.Value.Claims.OrderBy(c => c.Cycle.ToString(), StringComparer.Ordinal))
            {
                string? headline = await Headline(claim.Cycle, cancellationToken).ConfigureAwait(false);
                CycleBranch[] taken = [.. branches[claim.Cycle]];

                if (await new Cycle(claim.Cycle, solution.Path).ReleaseTarget(cancellationToken).ConfigureAwait(false) is { } release)
                {
                    held.Add(new HeldBack(
                        claim.Cycle,
                        headline,
                        string.Create(CultureInfo.InvariantCulture, $"its intent names {release}: a fix for a release is built by hand from that branch, not by a batch")));
                    continue;
                }

                if (taken.Length == 0)
                {
                    queued.Add(new Queued(claim.Cycle, headline));
                    continue;
                }

                held.Add(new HeldBack(claim.Cycle, headline, Why(taken)));
            }

            // An approval that no longer matches is a plan somebody edited after it was approved. The gate
            // refuses it, so it is not in the graph's claims; it is named here because nothing else will
            // say why a cycle a person approved is not being built.
            foreach (ComputeClaimGraph.Skipped skipped in graph.Value.Skipped
                .Where(s => s.Why == "not approved for a build")
                .OrderBy(s => s.Cycle.ToString(), StringComparer.Ordinal))
            {
                Cycle cycle = new(skipped.Cycle, solution.Path);

                if (File.Exists(cycle.ApprovalPath))
                {
                    held.Add(new HeldBack(
                        skipped.Cycle,
                        await Headline(skipped.Cycle, cancellationToken).ConfigureAwait(false),
                        "it has an approval and the gate refuses it - run 'sdlc gate " + skipped.Cycle + "' for why"));
                }
            }

            return Result.Ok(new Response(queued, held, remote is not null));
        }

        private static string Why(IReadOnlyList<CycleBranch> taken)
        {
            CycleBranch first = taken
                .OrderBy(branch => branch.IsBuild ? 0 : 1)
                .First();

            string what = first.Kind switch
            {
                CycleBranchKind.Plan => "the plan may still be moving; its pull request deletes the branch when it merges",
                CycleBranchKind.Build => "a person is building it by hand",
                CycleBranchKind.BuildUnattended => "a factory run has taken it; its pull request is waiting",
                CycleBranchKind.Forward => "a fix for a release is being carried forward",
                _ => "an older cycle branch is still there",
            };

            return string.Create(CultureInfo.InvariantCulture, $"{first} is on the remote: {what}");
        }

        private async Task<string?> Headline(CycleId id, CancellationToken cancellationToken)
        {
            Cycle cycle = new(id, solution.Path);
            ArtifactOutline outline = await ArtifactOutlineReader
                .ReadFile(cycle.PathOf(ArtifactKind.Intent), cancellationToken)
                .ConfigureAwait(false);
            return outline.Headline;
        }
    }
}
