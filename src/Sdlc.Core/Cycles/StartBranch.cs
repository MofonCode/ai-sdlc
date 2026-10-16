using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Cycles;

/// <summary>
/// Which branch a kind of work on a cycle happens on, and what it starts from.
/// </summary>
/// <remarks>
/// <para><strong>A file listing and a few refs decide it, so the binary does</strong> (§15.2). Three skills
/// need the answer — <c>intent</c> opens the plan branch, <c>build</c> and <c>batch-build</c> open a build
/// branch — and three skills working it out in prose would come out three ways.</para>
/// <para><strong>Where each kind starts</strong> (D-83, D-85):</para>
/// <list type="bullet">
/// <item><c>plan</c> — from the integration branch, or from what is checked out when there is none.</item>
/// <item><c>build</c> and <c>build-unattended</c> — from the integration branch once the plan has been admitted
/// to it; before that, from the plan's own branch, so a solution with no integration branch still works and
/// its one pull request carries the plan and the code. A cycle opened on the bare <c>cycle/&lt;id&gt;</c>
/// keeps building there.</item>
/// <item><c>forward</c> — from the integration branch, which it exists to reach.</item>
/// </list>
/// <para>A branch that already exists locally is switched to rather than created, so running the same
/// command twice is safe.</para>
/// </remarks>
public static class StartBranch
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddStartBranchRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks where a kind of work on a cycle starts.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Kind">The kind of work. Not the bare kind, which nothing new creates.</param>
    public sealed record Query(CycleId Id, CycleBranchKind Kind) : IRequest<Response>;

    /// <summary>Where the work starts.</summary>
    /// <param name="Branch">The branch the work happens on.</param>
    /// <param name="Command">The one git command that puts the working copy there.</param>
    /// <param name="Why">Why it starts where it does, for a person.</param>
    public sealed record Response(string Branch, string Command, string Why);

    private sealed class Handler(SolutionRoot solution, IRepository repository)
        : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.Kind == CycleBranchKind.Bare)
            {
                return Result.Fail<Response>(
                    "Nothing new starts on the bare cycle/<id> branch. Ask for plan, build, build-unattended or forward.");
            }

            string root = solution.Path;
            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);
            string branch = request.Id.BranchName(request.Kind);

            if (repository.RefExists(root, branch))
            {
                return Result.Ok(new Response(branch, "git switch " + branch, branch + " already exists, so the work carries on there."));
            }

            string? integrationRef = First(root, "origin/" + integration, integration);
            string plan = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, request.Id.ToString(), Cycle.FileNameOf(ArtifactKind.Plan));

            switch (request.Kind)
            {
                case CycleBranchKind.Plan:
                    return Result.Ok(integrationRef is null
                        ? new Response(branch, "git switch -c " + branch, string.Create(
                            CultureInfo.InvariantCulture,
                            $"There is no {integration} branch, so the plan starts from what is checked out."))
                        : Create(branch, integrationRef, $"The plan starts from {integrationRef}, the branch its pull request goes back into."));

                case CycleBranchKind.Forward:
                    return integrationRef is null
                        ? Result.Fail<Response>(string.Create(
                            CultureInfo.InvariantCulture,
                            $"There is no {integration} branch to carry a fix forward into."))
                        : Result.Ok(Create(branch, integrationRef, $"A forward carries a release's fix into {integrationRef}, so it starts there."));

                default:
                    if (await new Cycle(request.Id, root).ReleaseTarget(cancellationToken).ConfigureAwait(false) is { } release)
                    {
                        // D-88: a fix for a release is built from the release and back into it. The code starts
                        // there; the cycle's documents come from wherever the plan is.
                        string? releaseRef = First(root, "origin/" + release, release);
                        string? from = First(root, request.Id.BranchName(CycleBranchKind.Plan), "origin/" + request.Id.BranchName(CycleBranchKind.Plan), integrationRef ?? "HEAD");

                        if (releaseRef is null)
                        {
                            return Result.Fail<Response>(string.Create(
                                CultureInfo.InvariantCulture,
                                $"The intent names {release} as the release this fix is for, and there is no such branch. Fetch it, or correct the intent."));
                        }

                        string directory = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, request.Id.ToString());

                        return Result.Ok(new Response(
                            branch,
                            "git switch --no-track -c " + branch + " " + releaseRef + " && git checkout " + from + " -- " + directory,
                            $"The intent names {release}, so the build starts from it and takes the cycle's documents from {from}. Its pull request goes back into {release}, and 'sdlc branch {request.Id} forward' carries the fix into {integration}."));
                    }

                    if (integrationRef is not null && repository.PathExistsAt(root, integrationRef, plan))
                    {
                        return Result.Ok(Create(branch, integrationRef, $"The plan has been admitted to {integrationRef}, so the build starts there."));
                    }

                    string planBranch = request.Id.BranchName(CycleBranchKind.Plan);

                    if (First(root, planBranch, "origin/" + planBranch) is { } planRef)
                    {
                        return Result.Ok(Create(
                            branch,
                            planRef,
                            $"The plan has not reached {integration}, so the build starts from its own branch and its pull request will carry the plan and the code together."));
                    }

                    string bare = request.Id.BranchName();

                    if (First(root, bare, "origin/" + bare) is not null)
                    {
                        return Result.Ok(new Response(
                            bare,
                            "git switch " + bare,
                            "This cycle was opened on the bare branch before cycle branches had kinds, so it keeps building there."));
                    }

                    return Result.Fail<Response>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"The plan for {request.Id} is on no branch this can find: not on {integration}, not on {planBranch}. Merge its plan's pull request, or push its plan branch, then ask again."));
            }
        }

        private static Response Create(string branch, string from, string why) =>
            new(branch, "git switch --no-track -c " + branch + " " + from, why);

        private string? First(string root, params string[] refs) =>
            refs.FirstOrDefault(name => repository.RefExists(root, name));
    }
}
