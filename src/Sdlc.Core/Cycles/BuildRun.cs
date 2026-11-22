using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Cycles;

/// <summary>
/// What <c>sdlc build</c> and <c>sdlc branch &lt;id&gt; remediate</c> decide: where a cycle's build works, and how
/// its own process is started there (D-183, D-184, D-185, D-187).
/// </summary>
/// <remarks>
/// <para><strong>Build it is one command a skill runs, not a shell script it types.</strong> The binary prepares the
/// build's worktree from the branch the plan is on, starts the build's own process in it with its tools named, and
/// waits. The process is headless and asks nothing; it is never started with the host's permission checks switched
/// off, and it is never marked as a reader, because that mark would let a red build end its turn.</para>
/// <para><strong>The worktree is beside the repository, one per cycle</strong>, so a person's checkout stays where it
/// is and remediation later works in the same place the build did. An existing one on the cycle's build branch is
/// reused rather than refused.</para>
/// </remarks>
public static class BuildRun
{
    /// <summary>The skills the build playbook runs, itself or through <c>remediate</c>, each of which its process must be allowed.</summary>
    /// <remarks>
    /// The process is headless, so a skill it is not allowed is refused with nobody to ask, and the build ends at its
    /// first step with no report -- which is what every build did while this list was missing (D-202). The suite holds
    /// it to the skills table, so a skill added to either playbook cannot be left off.
    /// </remarks>
    public static IReadOnlyList<string> Skills { get; } = ["implement", "verify", "remediate", "pr", "resolve-conflict"];

    /// <summary>The tools every build's own process may use.</summary>
    /// <remarks>
    /// The skills the playbook runs; <c>sdlc</c> for every judgement and every process the chain starts; <c>git</c> for
    /// the branch; <c>dotnet</c> for the solution's build and tests; <c>gh</c> and <c>az</c> to open the pull request;
    /// and the edit tools. A command a solution's own build needs beyond these -- an evidence script's shell, a
    /// container -- is the person's to name in <c>build_tools</c>, never a list this release guesses (D-202).
    /// </remarks>
    public static IReadOnlyList<string> AllowedTools { get; } =
    [
        .. Skills.Select(skill => "Skill(sdlc:" + skill + ")"),
        "Bash(sdlc *)",
        "Bash(git *)",
        "Bash(dotnet *)",
        "Bash(gh *)",
        "Bash(az *)",
        "Read",
        "Write",
        "Edit",
        "Glob",
        "Grep",
    ];

    /// <summary>The tools a build's own process may use: every build's, then the solution's own commands.</summary>
    /// <param name="buildTools">The commands <c>build_tools</c> names, each a bare command name.</param>
    /// <returns>The list, with each extra command allowed as <c>Bash(&lt;name&gt; *)</c> and none twice.</returns>
    public static IReadOnlyList<string> AllowedToolsWith(IReadOnlyList<string> buildTools)
    {
        ArgumentNullException.ThrowIfNull(buildTools);

        return [.. AllowedTools.Concat(buildTools.Select(tool => "Bash(" + tool + " *)")).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Registers this operation's handlers.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddBuildRunRequestHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddTransient<IRequestHandler<Prepare, Place>, PrepareHandler>()
            .AddTransient<IRequestHandler<Launch, AgentExit>, LaunchHandler>();
    }

    /// <summary>Where a cycle's build works, beside the repository.</summary>
    /// <param name="solutionRoot">The repository's root.</param>
    /// <param name="id">The cycle.</param>
    /// <returns>The path, forward-slashed, relative to the repository's root.</returns>
    public static string WorktreePath(string solutionRoot, CycleId id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);

        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(solutionRoot)));
        return "../" + name + ".builds/" + id;
    }

    /// <summary>What the build's own process is asked to do.</summary>
    /// <param name="id">The cycle, or none for every queued cycle.</param>
    /// <param name="run">
    /// True to build every cycle a run's branch carries, one after another, into that branch: <c>wave/&lt;id&gt;</c> or
    /// <c>series/&lt;id&gt;</c>, checked out where the process starts.
    /// </param>
    /// <returns>The prompt: the build playbook, unattended, for this cycle, every queued one, or the run's (D-187).</returns>
    public static string PromptFor(CycleId? id, bool run = false) =>
        run ? "/sdlc:build --run --unattended"
        : id is { } cycle
            ? string.Create(CultureInfo.InvariantCulture, $"/sdlc:build {cycle} --unattended")
            : "/sdlc:build --unattended";

    /// <summary>The arguments the build's own process starts with, after the word <c>claude</c>.</summary>
    /// <param name="id">The cycle, or none for every queued cycle.</param>
    /// <param name="pluginDirectory">This release's skills, so the process runs the release the solution pins.</param>
    /// <param name="buildTools">The commands the solution's <c>build_tools</c> names, or none.</param>
    /// <param name="run">True to build the run checked out where the process starts.</param>
    /// <returns>The arguments, one each; the named tools last, because the option takes every word after it.</returns>
    public static IReadOnlyList<string> ArgumentsFor(CycleId? id, string pluginDirectory, IReadOnlyList<string>? buildTools = null, bool run = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        return ["--plugin-dir", pluginDirectory, "-p", PromptFor(id, run), "--allowedTools", .. AllowedToolsWith(buildTools ?? [])];
    }

    /// <summary>
    /// The git commands that open a worktree where <c>sdlc branch</c> would have switched the checkout.
    /// </summary>
    /// <param name="start">Where <c>sdlc branch &lt;id&gt; build-unattended</c> says the build starts.</param>
    /// <param name="place">The worktree's path.</param>
    /// <returns>Each command's arguments after the word git, or a failure for a start this cannot open.</returns>
    public static Result<IReadOnlyList<string>> WorktreeCommands(StartBranch.Response start, string place)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentException.ThrowIfNullOrWhiteSpace(place);

        string quoted = "\"" + place + "\"";
        string[] parts = start.Command.Split(" && ", StringSplitOptions.TrimEntries);
        List<string> commands = [];

        const string Create = "git switch --no-track -c ";
        const string Existing = "git switch ";
        const string Checkout = "git checkout ";

        if (parts[0].StartsWith(Create, StringComparison.Ordinal)
            && parts[0][Create.Length..].Split(' ', 2) is [string branch, string from])
        {
            commands.Add("worktree add --no-track -b " + branch + " " + quoted + " " + from);
        }
        else if (parts[0].StartsWith(Existing, StringComparison.Ordinal)
            && parts[0][Existing.Length..] is { } named
            && !named.Contains(' ', StringComparison.Ordinal))
        {
            commands.Add("worktree add " + quoted + " " + named);
        }
        else
        {
            return Result.Fail<IReadOnlyList<string>>(string.Create(
                CultureInfo.InvariantCulture,
                $"The build starts with '{start.Command}', which this cannot open as a worktree. Run 'sdlc branch <id> build-unattended' and build there."));
        }

        foreach (string part in parts.Skip(1))
        {
            if (!part.StartsWith(Checkout, StringComparison.Ordinal))
            {
                return Result.Fail<IReadOnlyList<string>>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The build starts with '{start.Command}', which this cannot open as a worktree. Run 'sdlc branch <id> build-unattended' and build there."));
            }

            commands.Add("-C " + quoted + " checkout " + part[Checkout.Length..]);
        }

        return Result.Ok<IReadOnlyList<string>>(commands);
    }

    /// <summary>Asks for the worktree a cycle's build, or its remediation, works in, opening it when absent.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Remediate">
    /// True for remediation, which works where the build did: on the branch its pull request is from, never on a new
    /// one. False for a build, which starts where <c>sdlc branch &lt;id&gt; build-unattended</c> says.
    /// </param>
    public sealed record Prepare(CycleId Id, bool Remediate = false) : IRequest<Place>;

    /// <summary>Where the work is, or where the plan has to be admitted from first.</summary>
    /// <param name="Branch">The branch the worktree is on, or the sandbox's when admission comes first.</param>
    /// <param name="Path">The worktree's full path, or the sandbox's when admission comes first.</param>
    /// <param name="Opened">Whether this opened the worktree rather than finding it.</param>
    /// <param name="AdmitFirst">
    /// True when the plan is only in a sandbox and there is an integration branch: the plan is admitted from the
    /// sandbox at <paramref name="Path"/>, then the build is prepared again (D-184).
    /// </param>
    public sealed record Place(string Branch, string Path, bool Opened, bool AdmitFirst = false);

    /// <summary>Starts the build's own process in its worktree, and waits for it.</summary>
    /// <param name="Id">The cycle, or none for every queued cycle, each of which the playbook builds in a worktree of its own.</param>
    /// <param name="Worktree">The worktree's full path, or the repository's root when no cycle is named.</param>
    /// <param name="PluginDirectory">This release's skills.</param>
    /// <param name="Run">True to build every cycle the run checked out in <paramref name="Worktree"/> carries, into its branch.</param>
    public sealed record Launch(CycleId? Id, string Worktree, string PluginDirectory, bool Run = false) : IRequest<AgentExit>;

    private sealed class PrepareHandler(SolutionRoot solution, Mediator mediator, IRepository repository)
        : IRequestHandler<Prepare, Place>
    {
        public async Task<Result<Place>> Handle(Prepare request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            string[] builds = [request.Id.BranchName(CycleBranchKind.BuildUnattended), request.Id.BranchName(CycleBranchKind.Build)];
            IReadOnlyList<(string Path, string? Branch)> worktrees = repository.Worktrees(root);

            if (worktrees.FirstOrDefault(tree => tree.Branch is { } on && builds.Contains(on, StringComparer.Ordinal)) is { Branch: { } found } existing)
            {
                return Result.Ok(new Place(found, existing.Path, Opened: false));
            }

            string place = Path.GetFullPath(Path.Combine(root, WorktreePath(root, request.Id)));
            string quoted = "\"" + place + "\"";

            if (request.Remediate)
            {
                // Remediation goes back to the branch the pull request is from: local first, else the remote's, which a
                // build on another machine pushed.
                if (builds.FirstOrDefault(branch => repository.RefExists(root, branch)) is { } local)
                {
                    return Open(root, local, place, "worktree add " + quoted + " " + local);
                }

                if (builds.FirstOrDefault(branch => repository.RefExists(root, "origin/" + branch)) is { } remote)
                {
                    return Open(root, remote, place, "worktree add -b " + remote + " " + quoted + " origin/" + remote);
                }

                return Result.Fail<Place>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {request.Id} has no build branch here or on the remote, so there is nothing to change. Build it first."));
            }

            Result<StartBranch.Response> start = await mediator
                .Send<StartBranch.Query, StartBranch.Response>(new StartBranch.Query(request.Id, CycleBranchKind.BuildUnattended), cancellationToken)
                .ConfigureAwait(false);

            if (start.IsFailed)
            {
                return await AdmitFirst(root, request.Id, worktrees, cancellationToken).ConfigureAwait(false) is { } sandbox
                    ? Result.Ok(sandbox)
                    : start.ToResult<Place>();
            }

            Result<IReadOnlyList<string>> commands = WorktreeCommands(start.Value, place);

            if (commands.IsFailed)
            {
                return commands.ToResult<Place>();
            }

            foreach (string command in commands.Value)
            {
                Result<string> ran = repository.Git(root, command);

                if (ran.IsFailed)
                {
                    return ran.ToResult<Place>();
                }
            }

            return Result.Ok(new Place(start.Value.Branch, place, Opened: true));
        }

        private Result<Place> Open(string root, string branch, string place, string command)
        {
            Result<string> ran = repository.Git(root, command);

            return ran.IsFailed ? ran.ToResult<Place>() : Result.Ok(new Place(branch, place, Opened: true));
        }

        /// <summary>The sandbox the plan has to be admitted from, when that is why the build cannot start.</summary>
        private async Task<Place?> AdmitFirst(
            string root,
            CycleId id,
            IReadOnlyList<(string Path, string? Branch)> worktrees,
            CancellationToken cancellationToken)
        {
            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);

            if (!repository.RefExists(root, integration) && !repository.RefExists(root, "origin/" + integration))
            {
                return null;
            }

            string plan = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, id.ToString(), Cycle.FileNameOf(ArtifactKind.Plan));

            return worktrees.FirstOrDefault(tree => tree.Branch is { } on
                    && on.StartsWith(SandboxBranch.Prefix, StringComparison.Ordinal)
                    && File.Exists(Path.Combine(tree.Path, plan))) is { Branch: { } sandbox } found
                ? new Place(sandbox, found.Path, Opened: false, AdmitFirst: true)
                : null;
        }
    }

    /// <summary>
    /// Starts an agent's own process in a worktree, with arguments that follow from the solution's own commands, and
    /// waits for it.
    /// </summary>
    /// <remarks>
    /// The worktree's own solution document, because that is the commit the process works from. One that cannot be
    /// read stops the launch: a process started without the commands the person named would be refused at the step
    /// that needs them, with nobody there to ask. Its output is passed straight through, so the person sees the
    /// process's own hand-back as it ends.
    /// </remarks>
    /// <param name="agents">What starts the process.</param>
    /// <param name="worktree">Where it starts.</param>
    /// <param name="arguments">Its arguments, given the commands <c>build_tools</c> names.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>How it ended, or why it could not start.</returns>
    internal static async Task<Result<AgentExit>> Start(
        IAgentProcess agents,
        string worktree,
        Func<IReadOnlyList<string>, IReadOnlyList<string>> arguments,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(worktree))
        {
            return Result.Fail<AgentExit>(string.Create(CultureInfo.InvariantCulture, $"There is no worktree at {worktree}."));
        }

        Result<SolutionDocument> solution = await SolutionDocument.ReadFile(worktree, cancellationToken).ConfigureAwait(false);

        if (solution.IsFailed)
        {
            return solution.ToResult<AgentExit>();
        }

        return Result.Ok(await agents.Run(
            new AgentLaunch(
                worktree,
                arguments(solution.Value.ExtraBuildTools),
                Input: null,
                Output: null,
                Reader: false,
                Timeout: null),
            cancellationToken).ConfigureAwait(false));
    }

    private sealed class LaunchHandler(IAgentProcess agents) : IRequestHandler<Launch, AgentExit>
    {
        public Task<Result<AgentExit>> Handle(Launch request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            return Start(agents, request.Worktree, tools => ArgumentsFor(request.Id, request.PluginDirectory, tools, request.Run), cancellationToken);
        }
    }
}
