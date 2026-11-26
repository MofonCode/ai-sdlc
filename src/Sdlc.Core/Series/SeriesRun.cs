using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Series;

/// <summary>
/// What <c>sdlc series run</c> starts: the series' own process, in its worktree, running the series playbook
/// unattended, with its tools named as a build's are.
/// </summary>
/// <remarks>
/// <para><strong>The same terms as a build's process</strong> (D-183, D-202): headless, asking nothing, never with the
/// host's permission checks switched off, never marked as a reader, and allowed each skill it runs, because a skill it
/// is not allowed is refused with nobody to ask.</para>
/// <para><strong>A series plans as well as builds</strong>, so its skills are the wave's -- the intent, and the loop
/// <c>to-plan</c> runs -- beside the build's.</para>
/// </remarks>
public static class SeriesRun
{
    /// <summary>The skills the series playbook runs, itself or through the playbooks it runs, each of which its process must be allowed.</summary>
    public static IReadOnlyList<string> Skills { get; } =
    [
        "intent",
        "to-plan",
        "spec",
        "plan",
        "interrogate",
        "refine",
        "steer",
        "build",
        .. BuildRun.Skills,
    ];

    /// <summary>The tools every series' own process may use: its skills, then what every build's process may use beside its own skills.</summary>
    public static IReadOnlyList<string> AllowedTools { get; } =
    [
        .. Skills.Select(skill => "Skill(sdlc:" + skill + ")"),
        .. BuildRun.AllowedTools.Where(tool => !tool.StartsWith("Skill(", StringComparison.Ordinal)),
    ];

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddSeriesRunRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Launch, AgentExit>, Handler>();
    }

    /// <summary>What the series' own process is asked to do.</summary>
    /// <param name="id">The series.</param>
    /// <returns>The prompt: the series playbook, unattended.</returns>
    public static string PromptFor(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return "/sdlc:series " + id.Trim() + " --unattended";
    }

    /// <summary>The arguments the series' own process starts with, after the word <c>claude</c>.</summary>
    /// <param name="id">The series.</param>
    /// <param name="pluginDirectory">This release's skills.</param>
    /// <param name="buildTools">The commands the solution's <c>build_tools</c> names, or none.</param>
    /// <returns>The arguments, one each; the named tools last, because the option takes every word after it.</returns>
    public static IReadOnlyList<string> ArgumentsFor(string id, string pluginDirectory, IReadOnlyList<string>? buildTools = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        return
        [
            "--plugin-dir",
            pluginDirectory,
            "-p",
            PromptFor(id),
            .. BuildRun.OutputFormat,
            "--allowedTools",
            .. AllowedTools.Concat((buildTools ?? []).SelectMany(BuildRun.RulesFor)).Distinct(StringComparer.Ordinal),
        ];
    }

    /// <summary>Starts the series' own process in its worktree, and waits for it.</summary>
    /// <param name="Id">The series.</param>
    /// <param name="Worktree">The series' worktree's full path.</param>
    /// <param name="PluginDirectory">This release's skills.</param>
    public sealed record Launch(string Id, string Worktree, string PluginDirectory) : IRequest<AgentExit>;

    private sealed class Handler(IAgentProcess agents) : IRequestHandler<Launch, AgentExit>
    {
        public Task<Result<AgentExit>> Handle(Launch request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            return BuildRun.Start(agents, request.Worktree, tools => ArgumentsFor(request.Id, request.PluginDirectory, tools), cancellationToken);
        }
    }
}
