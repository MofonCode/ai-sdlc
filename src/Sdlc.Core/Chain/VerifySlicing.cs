using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Chain;

/// <summary>
/// The pull-request check on <c>slices/&lt;slug&gt;/plan</c>: every plan sliced together, admitted at once (D-125).
/// </summary>
/// <remarks>
/// <para><strong>Still chosen by the diff</strong> (D-87). Every path must be one cycle's planning paperwork or a
/// successor's file; one path that is neither and the slicing is refused, so its branch cannot carry code past the
/// check any more than a plan's can.</para>
/// <para><strong>Each cycle gets the admission check it would have alone</strong>, and the pull request is refused
/// if any one fails. Then the two things only a slicing has: its successors are sound, and no plan needs a file only
/// a sibling's plan creates. Files two plans both name are reported and refuse nothing.</para>
/// </remarks>
public static class VerifySlicing
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddVerifySlicingRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks whether a slicing's pull request may be admitted.</summary>
    /// <param name="Slug">The slicing.</param>
    /// <param name="Changed">What the pull request changes, relative to the root.</param>
    public sealed record Query(string Slug, IReadOnlyList<string> Changed) : IRequest<Response>;

    /// <summary>What the check found.</summary>
    /// <param name="Slug">The slicing.</param>
    /// <param name="Cycles">Each cycle's admission check, in id order.</param>
    /// <param name="Checks">What only a slicing is checked for.</param>
    /// <param name="Comparison">What comparing the plans found, when they could be compared.</param>
    public sealed record Response(
        string Slug,
        IReadOnlyList<VerifyChain.Response> Cycles,
        IReadOnlyList<VerifyChain.ChainCheck> Checks,
        ComputeClaimGraph.SlicingComparison? Comparison)
    {
        /// <summary>Whether every cycle and every slicing check holds.</summary>
        public bool Holds => Cycles.All(c => c.Holds) && Checks.All(c => c.Passed);
    }

    private sealed class Handler(SolutionRoot solution, Mediator mediator) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            List<VerifyChain.ChainCheck> checks = [];

            if (VerifyChain.PlanningCycles(request.Changed) is not { } ids)
            {
                checks.Add(new VerifyChain.ChainCheck(
                    "this pull request carries only planning documents and successors",
                    false,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{request.Changed.Count} changed, and not all of them are a cycle's intent, spec, plan, findings or approval, or a file under .sdlc/successors/. A slicing admits plans; each cycle's code comes back by a pull request of its own.")));

                return Result.Ok(new Response(request.Slug, [], checks, null));
            }

            checks.Add(new VerifyChain.ChainCheck(
                "this pull request carries only planning documents and successors",
                true,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{ids.Count} cycle{(ids.Count == 1 ? string.Empty : "s")} sliced together, each checked for admission below.")));

            List<VerifyChain.Response> cycles = [];
            List<ComputeClaimGraph.SlicePlan> plans = [];

            foreach (CycleId id in ids)
            {
                string folder = Cycle.SdlcDirectoryName + "/" + Cycle.CyclesDirectoryName + "/" + id + "/";
                List<string> own = [.. request.Changed.Where(path => path.Replace('\\', '/').TrimStart('/').StartsWith(folder, StringComparison.Ordinal))];

                Result<VerifyChain.Response> chain = await mediator
                    .Send<VerifyChain.Query, VerifyChain.Response>(new VerifyChain.Query(id, own), cancellationToken)
                    .ConfigureAwait(false);

                if (chain.IsFailed)
                {
                    return chain.ToResult<Response>();
                }

                cycles.Add(chain.Value);

                string plan = new Cycle(id, solution.Path).PathOf(ArtifactKind.Plan);

                if (File.Exists(plan))
                {
                    plans.Add(ComputeClaimGraph.SlicePlan.Read(id, await File.ReadAllTextAsync(plan, cancellationToken).ConfigureAwait(false)));
                }
            }

            checks.Add(await VerifyChain.SuccessorsSound(mediator, cancellationToken).ConfigureAwait(false));

            // The branch carries planning and nothing else, so what is on disk here is the base's code: a
            // file present now was present before the slicing.
            ComputeClaimGraph.SlicingComparison comparison = ComputeClaimGraph.CompareSlicing(
                plans,
                path => File.Exists(Path.Combine(solution.Path, path.Replace('/', Path.DirectorySeparatorChar))));

            checks.Add(Atomic(comparison));
            checks.Add(Shared(comparison));

            return Result.Ok(new Response(request.Slug, cycles, checks, comparison));
        }
    }

    /// <summary>The refusal a dependency between sibling plans earns: that piece is a successor.</summary>
    /// <param name="comparison">What comparing the plans found.</param>
    /// <returns>The check.</returns>
    public static VerifyChain.ChainCheck Atomic(ComputeClaimGraph.SlicingComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        const string Name = "each plan sliced together can be built alone";

        if (comparison.Atomic)
        {
            return new VerifyChain.ChainCheck(Name, true, "no plan needs a file only a sibling's plan creates.");
        }

        StringBuilder detail = new();

        foreach (ComputeClaimGraph.Dependency dependency in comparison.Dependencies)
        {
            detail.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"{dependency.Dependent} needs {string.Join(", ", dependency.Files)}, which only {dependency.On} creates. "));
        }

        detail.Append("A piece that needs a sibling's unmerged code is not atomic: make it a successor of that sibling, or merge the two pieces.");
        return new VerifyChain.ChainCheck(Name, false, detail.ToString());
    }

    /// <summary>Files two plans both name: reported, never refused.</summary>
    /// <param name="comparison">What comparing the plans found.</param>
    /// <returns>The check, which always passes.</returns>
    public static VerifyChain.ChainCheck Shared(ComputeClaimGraph.SlicingComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        const string Name = "files two plans both name";

        return comparison.Shared.Count == 0
            ? new VerifyChain.ChainCheck(Name, true, "none.")
            : new VerifyChain.ChainCheck(Name, true, string.Join(
                " ",
                comparison.Shared.Select(c => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{c.First} and {c.Second} both name {string.Join(", ", c.Files)}; their pull requests may conflict, and the second to merge resolves it."))));
    }

    /// <summary>Renders the check for a pipeline log: each cycle's admission, then the slicing's own checks.</summary>
    /// <param name="verdict">What it found.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        StringBuilder output = new();
        output.AppendLine();
        output.AppendLine("  slicing - " + SlicingBranch.NameOf(verdict.Slug));
        output.AppendLine();

        foreach (VerifyChain.ChainCheck check in verdict.Checks)
        {
            output.AppendLine((check.Passed ? "    [ok]  " : "    [no]  ") + check.Name);
            output.AppendLine("          " + check.Detail);
        }

        foreach (VerifyChain.Response cycle in verdict.Cycles)
        {
            output.Append(ChainRenderer.Render(cycle));
        }

        output.AppendLine();
        output.AppendLine(verdict.Holds
            ? "  The slicing holds: every plan in it may be admitted."
            : "  The slicing does not hold, so none of its plans is admitted.");

        return output.ToString();
    }
}
