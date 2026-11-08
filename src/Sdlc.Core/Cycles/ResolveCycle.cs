using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Cycles;

/// <summary>What a skill run without a cycle id is about to do, and so which cycles it could mean.</summary>
public enum NextStep
{
    /// <summary>Anything: every cycle this working copy carries.</summary>
    Any,

    /// <summary>Writing a spec: the intent is clear and there is no spec.</summary>
    Spec,

    /// <summary>Writing a plan: the spec is clear and there is no plan.</summary>
    Plan,

    /// <summary>Building: the gate lets it through and nothing is built yet.</summary>
    Build,

    /// <summary>Verifying: a build recorded its evidence and nothing has judged it.</summary>
    Verify,

    /// <summary>Remediating: the verify is blocked.</summary>
    Remediate,
}

/// <summary>
/// Which cycle a skill means when it was given no id: the ones this working copy carries that are ready for what
/// it is about to do (D-142).
/// </summary>
/// <remarks>
/// <para><strong>One resolver, so no skill guesses on its own.</strong> Every stage skill takes one cycle id at
/// most, and without one it asks this. Exactly one candidate is used; several are listed and the person chooses;
/// none says so. It never picks among several, because the cycle a skill writes into is not a guess.</para>
/// <para><strong>What a working copy carries.</strong> On a cycle's own branch, <c>cycle/&lt;id&gt;/...</c>, that
/// cycle. Anywhere else, every cycle whose folder is here and not on the integration branch: what this branch
/// added. With no integration branch to compare with, every cycle here. Abandoned cycles are never candidates.</para>
/// </remarks>
public static class ResolveCycle
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddResolveCycleRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Reads a step's name as a skill or a person spells it.</summary>
    /// <param name="text">The name.</param>
    /// <param name="step">The step, when this returns true.</param>
    /// <returns>True when it names one.</returns>
    public static bool TryParseStep(string? text, out NextStep step) =>
        Enum.TryParse(text, ignoreCase: true, out step) && Enum.IsDefined(step) && !int.TryParse(text, out _);

    /// <summary>One cycle this working copy carries.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Headline">What its intent is about, when it says.</param>
    public sealed record Candidate(CycleId Id, string? Headline);

    /// <summary>Asks which cycles a step could mean.</summary>
    /// <param name="Step">What the caller is about to do.</param>
    public sealed record Query(NextStep Step = NextStep.Any) : IRequest<Response>;

    /// <summary>The cycles a step could mean.</summary>
    /// <param name="Step">What was asked.</param>
    /// <param name="Branch">The branch this working copy is on, when it is on one.</param>
    /// <param name="Carried">Every cycle this working copy carries, oldest first.</param>
    /// <param name="Ready">Those ready for the step, oldest first.</param>
    public sealed record Response(NextStep Step, string? Branch, IReadOnlyList<Candidate> Carried, IReadOnlyList<Candidate> Ready)
    {
        /// <summary>The one cycle to use, when exactly one is ready.</summary>
        public CycleId? Only => Ready.Count == 1 ? Ready[0].Id : null;
    }

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository)
        : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            bool repo = repository.IsRepository(root);
            string? branch = repo ? repository.CurrentBranch(root) : null;

            IEnumerable<Cycle> carried = CycleIndex.All(root).Where(cycle => !cycle.IsAbandoned);

            if (CycleBranch.TryParse(branch, out CycleBranch? own))
            {
                carried = carried.Where(cycle => cycle.Id == own.Id);
            }
            else if (repo)
            {
                string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);
                string? at = new[] { "origin/" + integration, integration }.FirstOrDefault(name => repository.RefExists(root, name));

                if (at is not null)
                {
                    carried = carried.Where(cycle => !repository.PathExistsAt(root, at, Relative(cycle)));
                }
            }

            List<Candidate> all = [];
            List<Candidate> ready = [];

            foreach (Cycle cycle in carried.OrderBy(cycle => cycle.Id.ToString(), StringComparer.Ordinal))
            {
                Result<ReadCycleStatus.Response> status = await mediator
                    .Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(new ReadCycleStatus.Query(cycle.Id), cancellationToken)
                    .ConfigureAwait(false);

                if (status.IsFailed)
                {
                    return status.ToResult<Response>();
                }

                ArtifactOutline outline = await ArtifactOutlineReader
                    .ReadFile(cycle.PathOf(ArtifactKind.Intent), cancellationToken)
                    .ConfigureAwait(false);
                Candidate candidate = new(cycle.Id, outline.Headline);

                all.Add(candidate);

                if (IsReady(status.Value, request.Step))
                {
                    ready.Add(candidate);
                }
            }

            return Result.Ok(new Response(request.Step, branch, all, ready));
        }

        private static string Relative(Cycle cycle) =>
            string.Create(CultureInfo.InvariantCulture, $"{Cycle.SdlcDirectoryName}/{Cycle.CyclesDirectoryName}/{cycle.Id}/{Cycle.FileNameOf(ArtifactKind.Intent)}");

        private static bool IsReady(ReadCycleStatus.Response status, NextStep step)
        {
            ReadCycleStatus.ArtifactState Of(ArtifactKind kind) => status.Artifacts.First(artifact => artifact.Kind == kind);

            return step switch
            {
                NextStep.Spec => Of(ArtifactKind.Intent).IsClear && !Of(ArtifactKind.Spec).Exists,
                NextStep.Plan => Of(ArtifactKind.Spec).IsClear && !Of(ArtifactKind.Plan).Exists,
                NextStep.Build => status.Gate.Refusal == Approvals.ApprovalGate.GateRefusal.None && !status.Built,
                NextStep.Verify => status.Built && status.Outcome is null,
                NextStep.Remediate => string.Equals(status.Outcome, "blocked", StringComparison.OrdinalIgnoreCase),
                _ => true,
            };
        }
    }
}
