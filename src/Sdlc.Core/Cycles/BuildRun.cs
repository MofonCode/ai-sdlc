using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    /// <param name="buildTools">The commands <c>build_tools</c> names, each a bare command name or one command line.</param>
    /// <returns>The list, with each extra command allowed as <see cref="RulesFor"/> says and none twice.</returns>
    public static IReadOnlyList<string> AllowedToolsWith(IReadOnlyList<string> buildTools)
    {
        ArgumentNullException.ThrowIfNull(buildTools);

        return [.. AllowedTools.Concat(buildTools.SelectMany(RulesFor)).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The host's allow rules for one <c>build_tools</c> entry.</summary>
    /// <remarks>
    /// A bare name is the whole command, <c>Bash(pwsh *)</c>, as it always was (D-202). A command line is that line
    /// and the line with more words after it, and nothing else (D-224): <c>docker build</c> allows
    /// <c>docker build .</c> and never <c>docker run</c>, which granting the runtime whole used to allow.
    /// </remarks>
    /// <param name="entry">The entry as the solution document read it.</param>
    /// <returns>The rules, one or two.</returns>
    public static IReadOnlyList<string> RulesFor(string entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry);

        return entry.Contains(' ', StringComparison.Ordinal)
            ? ["Bash(" + entry + ")", "Bash(" + entry + " *)"]
            : ["Bash(" + entry + " *)"];
    }

    /// <summary>How a build's or a series' process answers: one JSON object, which <see cref="Start"/> reads.</summary>
    /// <remarks>
    /// Its hand-back is the text the plain format printed, and beside it every tool use the host refused (D-224). A
    /// headless process prints nothing until it ends in either format, so nothing a person watched is lost.
    /// </remarks>
    public static IReadOnlyList<string> OutputFormat { get; } = ["--output-format", "json"];

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
    /// <param name="inRun">
    /// True for one cycle of a wave or a series, built on a branch of its own from the run's and opening no pull request:
    /// the binary merges it into the run when it passes, and the run opens one pull request for all of it (D-234).
    /// </param>
    /// <returns>The prompt: the build playbook, unattended, for this cycle or every queued one (D-187).</returns>
    public static string PromptFor(CycleId? id, bool inRun = false) =>
        id is { } cycle
            ? string.Create(CultureInfo.InvariantCulture, $"/sdlc:build {cycle} --unattended{(inRun ? " --no-pr" : string.Empty)}")
            : "/sdlc:build --unattended";

    /// <summary>The arguments the build's own process starts with, after the word <c>claude</c>.</summary>
    /// <param name="id">The cycle, or none for every queued cycle.</param>
    /// <param name="pluginDirectory">This release's skills, so the process runs the release the solution pins.</param>
    /// <param name="buildTools">The commands the solution's <c>build_tools</c> names, or none.</param>
    /// <param name="inRun">True for one cycle of a run, which opens no pull request of its own.</param>
    /// <returns>The arguments, one each; the named tools last, because the option takes every word after it.</returns>
    public static IReadOnlyList<string> ArgumentsFor(CycleId? id, string pluginDirectory, IReadOnlyList<string>? buildTools = null, bool inRun = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        return ["--plugin-dir", pluginDirectory, "-p", PromptFor(id, inRun), .. OutputFormat, "--allowedTools", .. AllowedToolsWith(buildTools ?? [])];
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
    /// <param name="InRun">True for one cycle of a wave or a series, which opens no pull request of its own (D-234).</param>
    public sealed record Launch(CycleId? Id, string Worktree, string PluginDirectory, bool InRun = false) : IRequest<AgentExit>;

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

        string answer = Path.Combine(Path.GetTempPath(), "sdlc-build-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            AgentExit exit = await agents.Run(
                new AgentLaunch(
                    worktree,
                    arguments(solution.Value.ExtraBuildTools),
                    Input: null,
                    Output: answer,
                    Reader: false,
                    Timeout: null),
                cancellationToken).ConfigureAwait(false);

            if (!File.Exists(answer))
            {
                return Result.Ok(exit);
            }

            (string? said, IReadOnlyList<string> refused) = ReadAnswer(await File.ReadAllTextAsync(answer, cancellationToken).ConfigureAwait(false));
            return Result.Ok(exit with { Said = said, Refused = refused });
        }
        finally
        {
            File.Delete(answer);
        }
    }

    /// <summary>Reads what a build's process answered in the JSON format: its hand-back, and every tool use refused.</summary>
    /// <remarks>
    /// The host records each refusal as <c>permission_denials</c>, with the tool and what it was asked to run. An
    /// answer that is not that object -- an older host, a crash part way -- is handed back as it was written, refusing
    /// nothing, because what it said is still the person's to read.
    /// </remarks>
    /// <param name="text">What the process wrote to its standard output.</param>
    /// <returns>Its hand-back, or null for none; and each refusal as <c>tool: command</c>.</returns>
    public static (string? Said, IReadOnlyList<string> Refused) ReadAnswer(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        JsonNode? root;

        try
        {
            root = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return (text.Trim().Length == 0 ? null : text.Trim(), []);
        }

        if (root is not JsonObject answer)
        {
            return (text.Trim().Length == 0 ? null : text.Trim(), []);
        }

        string? said = answer["result"] is JsonValue result && result.TryGetValue(out string? value) && value.Trim().Length > 0 ? value.Trim() : null;
        List<string> refused = [];

        if (answer["permission_denials"] is JsonArray denials)
        {
            foreach (JsonNode? denial in denials)
            {
                string tool = denial?["tool_name"] is JsonValue name && name.TryGetValue(out string? named) ? named : "a tool";
                JsonNode? input = denial?["tool_input"];
                string asked = input?["command"] is JsonValue command && command.TryGetValue(out string? line)
                    ? line
                    : input?.ToJsonString() ?? string.Empty;

                string entry = asked.Length == 0 ? tool : tool + ": " + asked.ReplaceLineEndings(" ").Trim();

                if (!refused.Contains(entry, StringComparer.Ordinal))
                {
                    refused.Add(entry);
                }
            }
        }

        return (said, refused);
    }

    /// <summary>Says which commands a build was refused and how to grant one, for a person reading the build's end.</summary>
    /// <param name="refused">Each refusal, as <see cref="ReadAnswer"/> read it.</param>
    /// <returns>The paragraph, or empty when nothing was refused.</returns>
    public static string RenderRefused(IReadOnlyList<string> refused)
    {
        ArgumentNullException.ThrowIfNull(refused);

        if (refused.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder text = new();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"The build's process was refused {refused.Count} {(refused.Count == 1 ? "command" : "commands")} its tools do not allow. It stopped at that step; no test failed for it:"));

        foreach (string entry in refused)
        {
            text.AppendLine("  " + entry);
        }

        string? first = refused
            .Where(entry => entry.StartsWith("Bash: ", StringComparison.Ordinal))
            .Select(entry => entry["Bash: ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .FirstOrDefault(word => word is not null && SolutionDocument.IsCommandName(word));

        text.AppendLine(first is null
            ? "A tool other than a shell command is one no build is given: the build playbook runs commands through bash."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"If the build needs it, name it in solution.md's build_tools -- '{first}' grants the command whole, '{first} <words>' only that command line -- commit it, and build again."));

        return text.ToString();
    }

    /// <summary>The commands a build's process may run beyond every build's own, and what this machine adds that it was never granted.</summary>
    /// <param name="Granted">What <c>build_tools</c> names, as written there.</param>
    /// <param name="MachineOnly">
    /// The shell commands this machine's own host settings allow that neither every build's tools nor <c>build_tools</c>
    /// cover. A build here may run them; an unattended build anywhere else is refused them.
    /// </param>
    public sealed record Tools(IReadOnlyList<string> Granted, IReadOnlyList<string> MachineOnly)
    {
        /// <summary>Says both, for a person, in one paragraph.</summary>
        /// <returns>The text, ending in a line break.</returns>
        public string Render()
        {
            StringBuilder text = new();
            text.AppendLine(Granted.Count == 0
                ? "Build tools: every build's own, and nothing from solution.md's build_tools."
                : "Build tools: every build's own, and from solution.md's build_tools: " + string.Join(", ", Granted) + ".");

            if (MachineOnly.Count > 0)
            {
                text.AppendLine(
                    "This machine's own host settings also allow " + string.Join(", ", MachineOnly)
                    + ", which build_tools does not grant. A build here may use them and pass; the factory, or any other machine, refuses them. If the build needs one, name it in build_tools.");
            }

            return text.ToString();
        }
    }

    /// <summary>Works out the tools a build in a worktree runs with: the grant, and what this machine adds to it.</summary>
    /// <remarks>
    /// <para><strong>The process is <c>claude</c></strong>, started as <see cref="CliAgentProcess"/> starts it, so the
    /// settings it reads beside its arguments are that host's: the user's own, and the worktree's local file. Both are
    /// this machine's alone. The repository's committed settings travel with it, so they are not counted.</para>
    /// <para>A file that cannot be read adds nothing: this reports, and a report that refused for a person's settings
    /// file would stop a build over something the build does not need.</para>
    /// </remarks>
    /// <param name="worktree">Where the build works.</param>
    /// <param name="buildTools">What <c>build_tools</c> names.</param>
    /// <param name="home">The user's home directory, or null for this process's own.</param>
    /// <returns>The tools.</returns>
    public static Tools ToolsIn(string worktree, IReadOnlyList<string> buildTools, string? home = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktree);
        ArgumentNullException.ThrowIfNull(buildTools);

        string user = home ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        HashSet<string> granted = new(AllowedToolsWith(buildTools), StringComparer.Ordinal);
        List<string> machine = [];

        foreach (string file in new[] { Path.Combine(user, ".claude", "settings.json"), Path.Combine(worktree, ".claude", "settings.local.json") })
        {
            foreach (string rule in AllowRules(file))
            {
                if (rule.StartsWith("Bash(", StringComparison.Ordinal) && !granted.Contains(rule) && !machine.Contains(rule, StringComparer.Ordinal))
                {
                    machine.Add(rule);
                }
            }
        }

        return new Tools(buildTools, machine);
    }

    private static IEnumerable<string> AllowRules(string file)
    {
        if (!File.Exists(file))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(file))?["permissions"]?["allow"] is JsonArray allow
                ? [.. allow.Select(rule => rule is JsonValue value && value.TryGetValue(out string? text) ? text : null).OfType<string>()]
                : [];
        }
        catch (Exception unreadable) when (unreadable is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private sealed class LaunchHandler(IAgentProcess agents) : IRequestHandler<Launch, AgentExit>
    {
        public Task<Result<AgentExit>> Handle(Launch request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            return Start(agents, request.Worktree, tools => ArgumentsFor(request.Id, request.PluginDirectory, tools, request.InRun), cancellationToken);
        }
    }
}
