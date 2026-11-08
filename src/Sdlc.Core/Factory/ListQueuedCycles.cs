using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

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
/// <para><strong>A build made before its plan was amended says so</strong> (D-123). Its ledger lacks the
/// approval standing here, so its verify can never pass the chain, and the factory never resumes it: the reason
/// names both of the person's moves rather than calling it somebody's build in progress.</para>
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
    /// <param name="Unlocks">
    /// The successors waiting on it, directly or behind another successor that is, by slug: what merging it
    /// starts moving, and why it is built first.
    /// </param>
    public sealed record Queued(CycleId Id, string? Headline, IReadOnlyList<string> Unlocks);

    /// <summary>A cycle the factory would pass over, and why.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Headline">What its intent is about, when it says.</param>
    /// <param name="Reason">Why it is held back, as a person would want to read it.</param>
    public sealed record HeldBack(CycleId Id, string? Headline, string Reason);

    /// <summary>Asks for the queue.</summary>
    /// <param name="Only">
    /// The one cycle a run was asked to build. The queue is then that cycle alone, and one that is not queued is
    /// refused with why, so a run started for it stops before it builds anything.
    /// </param>
    public sealed record Query(CycleId? Only = null) : IRequest<Response>;

    /// <summary>The queue.</summary>
    /// <param name="Cycles">
    /// What would be built, in the order to build it: the cycle the most successors wait on first, so the time a
    /// run has goes where it starts the most moving, then oldest first.
    /// </param>
    /// <param name="Held">What would be passed over, oldest first.</param>
    /// <param name="RemoteRead">
    /// Whether the remote's branches could be read. False on a solution with no remote, or when git could
    /// not reach it — and then nothing is held back by a branch, which the renderer says.
    /// </param>
    public sealed record Response(IReadOnlyList<Queued> Cycles, IReadOnlyList<HeldBack> Held, bool RemoteRead);

    /// <summary>
    /// Every successor waiting on a cycle, directly or behind another successor that is, by slug.
    /// </summary>
    /// <param name="successors">Where every successor stands.</param>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The slugs, in the order they were reached.</returns>
    public static IReadOnlyList<string> Waiting(ReadSuccessors.Response successors, CycleId cycle)
    {
        ArgumentNullException.ThrowIfNull(successors);

        // What a successor names resolves to a cycle once one started from it; until then it is the slug.
        HashSet<string> reached = new(StringComparer.Ordinal) { cycle.ToString() };
        List<string> waiting = [];
        bool grew = true;

        while (grew)
        {
            grew = false;

            foreach (ReadSuccessors.Standing standing in successors.Successors)
            {
                string slug = standing.Successor.Slug;

                if (!waiting.Contains(slug, StringComparer.Ordinal)
                    && standing.Waits.Any(wait => reached.Contains(wait.Cycle?.ToString() ?? wait.Reference.ToString())))
                {
                    waiting.Add(slug);
                    reached.Add(SuccessorReference.Prefix + slug);
                    grew = true;
                }
            }
        }

        return waiting;
    }

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

            Result<ReadSuccessors.Response> successors = await mediator
                .Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (successors.IsFailed)
            {
                return successors.ToResult<Response>();
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
                    queued.Add(new Queued(claim.Cycle, headline, Waiting(successors.Value, claim.Cycle)));
                    continue;
                }

                held.Add(new HeldBack(claim.Cycle, headline, await Amended(claim.Cycle, taken, cancellationToken).ConfigureAwait(false) ?? Why(taken)));
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

            // Most waiting first: a run that stops on its time limit has then built what starts the most
            // moving. The claims were read oldest first, and the sort is stable, so age breaks a tie.
            Queued[] ordered = [.. queued.OrderByDescending(cycle => cycle.Unlocks.Count)];

            if (request.Only is not { } only)
            {
                return Result.Ok(new Response(ordered, held, remote is not null));
            }

            if (ordered.FirstOrDefault(cycle => cycle.Id == only) is { } asked)
            {
                return Result.Ok(new Response([asked], [], remote is not null));
            }

            string why = held.FirstOrDefault(cycle => cycle.Id == only)?.Reason
                ?? (Directory.Exists(new Cycle(only, solution.Path).Directory)
                    ? "it is not approved for a build, or it already has a verify - 'sdlc status " + only + "' says where it stands"
                    : "there is no such cycle here");

            return Result.Fail(string.Create(
                CultureInfo.InvariantCulture,
                $"Nothing was built: {only} is not queued, because {why}."));
        }

        /// <summary>The reason for a build branch older than the approval standing here, or null when none is.</summary>
        private async Task<string?> Amended(CycleId id, IReadOnlyList<CycleBranch> taken, CancellationToken cancellationToken)
        {
            Cycle cycle = new(id, solution.Path);

            if (!File.Exists(cycle.ApprovalPath))
            {
                return null;
            }

            string standing = await File.ReadAllTextAsync(cycle.ApprovalPath, cancellationToken).ConfigureAwait(false);
            string integration = await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken).ConfigureAwait(false);

            foreach (CycleBranch branch in taken.Where(b => b.IsBuild))
            {
                if (AmendedPlan.Predates(repository.ReadAt(solution.Path, "origin/" + branch, AmendedPlan.LedgerPath(id)), standing))
                {
                    return string.Create(
                        CultureInfo.InvariantCulture,
                        $"{branch} was built before its plan was amended, and the factory never resumes it: {AmendedPlan.Choices(branch.ToString(), integration)}");
                }
            }

            return null;
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
