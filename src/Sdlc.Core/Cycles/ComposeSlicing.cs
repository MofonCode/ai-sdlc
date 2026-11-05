using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Cycles;

/// <summary>
/// The one git command that composes <c>slices/&lt;slug&gt;/plan</c> from the siblings' plan branches (D-125).
/// </summary>
/// <remarks>
/// <para><strong>A few refs and a set comparison decide it, so the binary does</strong> (§15.2). The plans are read
/// from their own branches, because each sibling was planned in a worktree of its own and this checkout holds none
/// of them.</para>
/// <para><strong>It refuses before anything is composed</strong>: a sibling with no plan branch, a plan branch with
/// no plan or no approval ledger, and a plan that needs a file only a sibling's plan creates. Files two plans both
/// name are said and refuse nothing. Whether each approval stands is <c>sdlc gate</c>'s, in each cycle's folder,
/// and the pull-request check asks it again of the composed branch.</para>
/// <para><strong>Composing cannot conflict</strong>: each plan branch touches its own cycle folder, and the
/// successors only the first one carries.</para>
/// </remarks>
public static class ComposeSlicing
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddComposeSlicingRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks how to compose a slicing's branch.</summary>
    /// <param name="Slug">The slicing.</param>
    /// <param name="Cycles">The cycles sliced together.</param>
    public sealed record Query(string Slug, IReadOnlyList<CycleId> Cycles) : IRequest<Response>;

    /// <summary>How to compose it.</summary>
    /// <param name="Branch">The branch it composes.</param>
    /// <param name="Command">The one git command that composes it.</param>
    /// <param name="Comparison">What comparing the plans found: the shared files to say.</param>
    public sealed record Response(string Branch, string Command, ComputeClaimGraph.SlicingComparison Comparison);

    private sealed class Handler(SolutionRoot solution, IRepository repository) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!Successors.Successor.IsSlug(request.Slug))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{request.Slug}' is not a slug: lower-case letters and digits, joined by single hyphens."));
            }

            if (request.Cycles.Distinct().Count() < 2)
            {
                return Result.Fail<Response>("A slicing admits two cycles or more. One cycle's plan goes by its own plan branch: 'sdlc branch <id> plan'.");
            }

            string root = solution.Path;
            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);
            string? integrationRef = First(root, "origin/" + integration, integration);

            if (integrationRef is null)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no {integration} branch, so there is nothing to admit a slicing to. Each cycle's plan goes with its build."));
            }

            List<string> branches = [];
            List<ComputeClaimGraph.SlicePlan> plans = [];
            List<string> problems = [];

            foreach (CycleId id in request.Cycles.Distinct().OrderBy(id => id.ToString(), StringComparer.Ordinal))
            {
                string plan = id.BranchName(CycleBranchKind.Plan);
                string folder = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, id.ToString()) + "/";

                if (First(root, plan, "origin/" + plan) is not { } at)
                {
                    problems.Add(string.Create(CultureInfo.InvariantCulture, $"{id} has no {plan} branch here."));
                    continue;
                }

                string? text = repository.ReadAt(root, at, folder + Cycle.FileNameOf(ArtifactKind.Plan));

                if (text is null || repository.ReadAt(root, at, folder + "approval.md") is null)
                {
                    problems.Add(string.Create(CultureInfo.InvariantCulture, $"{at} holds no {(text is null ? "plan" : "approval ledger")} for {id}: commit it there, approved, first."));
                    continue;
                }

                branches.Add(at);
                plans.Add(ComputeClaimGraph.SlicePlan.Read(id, text));
            }

            if (problems.Count > 0)
            {
                return Result.Fail<Response>("Nothing was composed: " + string.Join(" ", problems));
            }

            ComputeClaimGraph.SlicingComparison comparison = ComputeClaimGraph.CompareSlicing(
                plans,
                path => repository.PathExistsAt(root, integrationRef, path));

            if (!comparison.Atomic)
            {
                return Result.Fail<Response>("Nothing was composed: " + Chain.VerifySlicing.Atomic(comparison).Detail);
            }

            string branch = SlicingBranch.NameOf(request.Slug);

            return Result.Ok(new Response(
                branch,
                "git switch --no-track -c " + branch + " " + integrationRef + " && git merge --no-ff --no-edit " + string.Join(' ', branches),
                comparison));
        }

        private string? First(string root, params string[] refs) =>
            refs.FirstOrDefault(name => repository.RefExists(root, name));
    }
}
