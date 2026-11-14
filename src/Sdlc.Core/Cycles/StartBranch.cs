using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
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
    /// <param name="Worktree">Whether the cycle gets a worktree of its own rather than switching this checkout.</param>
    /// <param name="Sandbox">
    /// Whether the cycle opens a sandbox: a worktree on <c>sandbox/&lt;id&gt;</c>, where every cycle the work turns
    /// into is planned beside it (D-144). Planning only.
    /// </param>
    public sealed record Query(CycleId Id, CycleBranchKind Kind, bool Worktree = false, bool Sandbox = false) : IRequest<Response>;

    /// <summary>
    /// Where a sandbox's working copy goes: beside the repository, in a directory named for its first cycle.
    /// </summary>
    /// <param name="solutionRoot">The repository's root.</param>
    /// <param name="primary">The cycle it opens with.</param>
    /// <returns>The path, forward-slashed, relative to the repository's root.</returns>
    public static string SandboxPath(string solutionRoot, CycleId primary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);

        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(solutionRoot)));
        return "../" + name + ".sandboxes/" + primary;
    }

    /// <summary>
    /// Where a cycle's own working copy goes: beside the repository, in a directory named for it.
    /// </summary>
    /// <remarks>
    /// D-105. A person planning two cycles at once had one working copy, and the plan stage switches it, so
    /// the second intent took the first cycle's documents out of sight and carried any half-made edit across.
    /// A worktree per cycle is one folder per cycle on one repository: each its own branch and its own
    /// session, and nothing uncommitted rides from one to the other. Beside rather than inside, so no build
    /// or glob in the repository ever walks into another cycle's copy.
    /// </remarks>
    /// <param name="solutionRoot">The repository's root.</param>
    /// <param name="id">The cycle.</param>
    /// <returns>The path, forward-slashed, relative to the repository's root.</returns>
    public static string WorktreePath(string solutionRoot, CycleId id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);

        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(solutionRoot)));
        return "../" + name + ".cycles/" + id;
    }

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
            string? integrationRef = First(root, "origin/" + integration, integration);

            // D-123: a plan reopened after admission is amended on a plan branch. One left over from an earlier
            // round of planning lacks the approval that now stands, and carrying on there would amend a plan
            // nobody reads any more.
            if (request.Kind == CycleBranchKind.Plan
                && integrationRef is not null
                && repository.RefExists(root, branch)
                && AmendedPlan.Predates(
                    repository.ReadAt(root, branch, AmendedPlan.LedgerPath(request.Id)),
                    repository.ReadAt(root, integrationRef, AmendedPlan.LedgerPath(request.Id))))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{branch} is left over from earlier planning: {integrationRef} holds an approval it does not. Delete it with 'git branch -D {branch}' and ask again, so the amendment starts from {integrationRef}."));
            }

            if (request.Sandbox)
            {
                if (request.Kind != CycleBranchKind.Plan)
                {
                    return Result.Fail<Response>("A sandbox is for planning. A cycle builds on a branch of its own: 'sdlc branch <id> build'.");
                }

                string sandbox = SandboxBranch.NameOf(request.Id);
                string place = SandboxPath(root, request.Id);
                string folder = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, request.Id.ToString());

                return Result.Ok(repository.RefExists(root, sandbox)
                    ? new Response(sandbox, "git worktree add " + place + " " + sandbox, string.Create(CultureInfo.InvariantCulture, $"{sandbox} already exists, so its worktree opens on it at {place}."))
                    : new Response(
                        sandbox,
                        "git worktree add -b " + sandbox + " " + place + (integrationRef is null ? string.Empty : " " + integrationRef) + " && mkdir -p " + place + "/" + folder,
                        string.Create(CultureInfo.InvariantCulture, $"This work gets a sandbox at {place}, on {sandbox} from {integrationRef ?? "what is checked out"}. Every cycle it turns into is planned there, beside this one; this checkout stays as it is.")));
            }

            if (request.Worktree)
            {
                if (request.Kind != CycleBranchKind.Plan)
                {
                    return Result.Fail<Response>("A worktree is for planning a cycle beside others. The build, its remediation and its forward run in the cycle's worktree once it has one: run 'sdlc branch <id> build' there.");
                }

                string path = WorktreePath(root, request.Id);
                string directory = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, request.Id.ToString());

                return Result.Ok(repository.RefExists(root, branch)
                    ? new Response(branch, "git worktree add " + path + " " + branch, string.Create(CultureInfo.InvariantCulture, $"{branch} already exists, so its worktree opens on it at {path}."))
                    : new Response(
                        branch,
                        "git worktree add -b " + branch + " " + path + (integrationRef is null ? string.Empty : " " + integrationRef) + " && mkdir -p " + path + "/" + directory,
                        string.Create(CultureInfo.InvariantCulture, $"This cycle gets its own working copy at {path}, on {branch} from {integrationRef ?? "what is checked out"}. Work on it there, in a session opened there; this checkout stays as it is.")));
            }

            // A switch carries uncommitted changes with it, so a plan branch switched to over them would take them
            // into the cycle as work no plan declared. Refused here, where the carrying happens: 'sdlc new' did
            // it before sandboxes, which switch nothing (D-144). What to do with them is the person's.
            if (request.Kind == CycleBranchKind.Plan
                && repository.IsRepository(root)
                && repository.UncommittedChanges(root).Where(path => !IsBookkeeping(path)).ToArray() is { Length: > 0 } changed)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Nothing was switched: {changed.Length} {(changed.Length == 1 ? "file has" : "files have")} uncommitted changes, and {branch} would carry {(changed.Length == 1 ? "it" : "them")} into the cycle as work no plan declared -- {string.Join(", ", changed.Take(8))}{(changed.Length > 8 ? ", and more" : string.Empty)}. Commit, stash or discard {(changed.Length == 1 ? "it" : "them")} -- or plan in a sandbox, which switches nothing: 'sdlc branch {request.Id} sandbox'."));
            }

            if (repository.RefExists(root, branch))
            {
                return Result.Ok(new Response(branch, "git switch " + branch, branch + " already exists, so the work carries on there."));
            }

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

                    // An admission of several cycles goes on one slicing branch, named for the sandbox rather
                    // than for this cycle, so it is found by what it carries (D-162).
                    if (repository.Branches(root, SlicingBranch.Prefix).FirstOrDefault(name => repository.PathExistsAt(root, name, plan)) is { } admission)
                    {
                        return Result.Ok(Create(
                            branch,
                            admission,
                            $"The plan is on {admission}, an admission not yet merged into {integration}, so the build starts there and its pull request carries the plan and the code together."));
                    }

                    string bare = request.Id.BranchName();

                    if (First(root, bare, "origin/" + bare) is not null)
                    {
                        return Result.Ok(new Response(
                            bare,
                            "git switch " + bare,
                            "This cycle was opened on the bare branch before cycle branches had kinds, so it keeps building there."));
                    }

                    // A sandbox is planned in and never merged, so its plans leave by 'sdlc admit' (D-144) -- unless
                    // there is no integration branch to admit them to, which is when admit says to build from it.
                    if (repository.Branches(root, SandboxBranch.Prefix).FirstOrDefault(name => repository.PathExistsAt(root, name, plan)) is { } sandbox)
                    {
                        return integrationRef is null
                            ? Result.Ok(Create(branch, sandbox, $"There is no {integration} branch to admit the plan to, so the build starts from {sandbox}, where it was planned."))
                            : Result.Fail<Response>(string.Create(
                                CultureInfo.InvariantCulture,
                                $"The plan for {request.Id} is only in {sandbox}, which is planned in and never merged. Run 'sdlc admit' in that sandbox to put its approved plans on an admission branch, then ask again: the build starts from that branch, or from {integration} once it merges."));
                    }

                    return Result.Fail<Response>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"The plan for {request.Id} is on no branch this can find: not on {integration}, not on {planBranch}, not on an admission branch or in a sandbox. Merge its plan's pull request, or push its plan branch, then ask again."));
            }
        }

        /// <summary>
        /// Whether an uncommitted path is an ignore file the framework writes into its own directory.
        /// </summary>
        /// <remarks>
        /// <c>sdlc new</c> writes the cycles directory's on the first cycle and a report writes its own, so counting
        /// them would have the plan branch refused over a file opening the cycle created. They hold nobody's work,
        /// which is the only thing the refusal exists to protect.
        /// </remarks>
        private static bool IsBookkeeping(string path)
        {
            string normalised = path.Replace('\\', '/');

            return normalised.StartsWith(Cycle.SdlcDirectoryName + "/", StringComparison.Ordinal)
                && normalised.EndsWith("/.gitignore", StringComparison.Ordinal);
        }

        private static Response Create(string branch, string from, string why) =>
            new(branch, "git switch --no-track -c " + branch + " " + from, why);

        private string? First(string root, params string[] refs) =>
            refs.FirstOrDefault(name => repository.RefExists(root, name));
    }
}
