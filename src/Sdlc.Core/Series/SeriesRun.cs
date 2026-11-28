using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Series;

/// <summary>Which step of a series a process is started for.</summary>
public enum SeriesStep
{
    /// <summary>Plan the open wave: each successor's intent, then every cycle's spec and plan with its loop.</summary>
    Plan,

    /// <summary>Answer the cycles a wave's build left stopped: add the fix a test that comes and goes needs, or nothing.</summary>
    Stops,
}

/// <summary>
/// What <c>sdlc series run</c> starts for each step that needs language: the series playbook unattended, in the series'
/// worktree, to plan a wave or answer what its build left stopped, with its tools named as a build's are. Each cycle's
/// build, and the pull request at the end, are processes of their own with a build's tools.
/// </summary>
/// <remarks>
/// <para><strong>One process per step, never one for the whole run</strong> (D-234). The binary drives the waves:
/// it takes each, starts a process to plan it, builds its cycles a few at a time in processes of their own, merges them,
/// and records the wave. A process that drove it all would sit in one shell call for as long as the series takes.</para>
/// <para><strong>The same terms as a build's process</strong> (D-183, D-202): headless, asking nothing, never with the
/// host's permission checks switched off, never marked as a reader, and allowed each skill it runs, because a skill it
/// is not allowed is refused with nobody to ask.</para>
/// <para><strong>Its skills are planning's</strong>: the intent, and the loop <c>to-plan</c> runs. Building is never this
/// process's, so the build's skills are not on its list.</para>
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

    /// <summary>What one of the series' processes is asked to do.</summary>
    /// <param name="id">The series.</param>
    /// <param name="step">Which step.</param>
    /// <param name="stops">For <see cref="SeriesStep.Stops"/>, the file naming each stopped cycle and what its build said.</param>
    /// <returns>The prompt: the series playbook unattended at that step.</returns>
    public static string PromptFor(string id, SeriesStep step = SeriesStep.Plan, string? stops = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return step switch
        {
            SeriesStep.Stops => "/sdlc:series " + id.Trim() + " --unattended --step stops --stops \"" + stops + "\"",
            _ => "/sdlc:series " + id.Trim() + " --unattended --step plan",
        };
    }

    /// <summary>The arguments the series' own process starts with, after the word <c>claude</c>.</summary>
    /// <param name="id">The series.</param>
    /// <param name="pluginDirectory">This release's skills.</param>
    /// <param name="buildTools">The commands the solution's <c>build_tools</c> names, or none.</param>
    /// <param name="step">Which step.</param>
    /// <param name="stops">For <see cref="SeriesStep.Stops"/>, the file naming the stopped cycles.</param>
    /// <returns>The arguments, one each; the named tools last, because the option takes every word after it.</returns>
    public static IReadOnlyList<string> ArgumentsFor(string id, string pluginDirectory, IReadOnlyList<string>? buildTools = null, SeriesStep step = SeriesStep.Plan, string? stops = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        return
        [
            "--plugin-dir",
            pluginDirectory,
            "-p",
            PromptFor(id, step, stops),
            .. BuildRun.OutputFormat,
            "--allowedTools",
            .. AllowedTools.Concat((buildTools ?? []).SelectMany(BuildRun.RulesFor)).Distinct(StringComparer.Ordinal),
        ];
    }

    /// <summary>Starts one of the series' processes in its worktree, and waits for it.</summary>
    /// <param name="Id">The series.</param>
    /// <param name="Worktree">The series' worktree's full path.</param>
    /// <param name="PluginDirectory">This release's skills.</param>
    /// <param name="Step">Which step.</param>
    /// <param name="Stops">For <see cref="SeriesStep.Stops"/>, the file naming the stopped cycles.</param>
    public sealed record Launch(string Id, string Worktree, string PluginDirectory, SeriesStep Step = SeriesStep.Plan, string? Stops = null) : IRequest<AgentExit>;

    private sealed class Handler(IAgentProcess agents) : IRequestHandler<Launch, AgentExit>
    {
        public Task<Result<AgentExit>> Handle(Launch request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            return BuildRun.Start(agents, request.Worktree, tools => ArgumentsFor(request.Id, request.PluginDirectory, tools, request.Step, request.Stops), cancellationToken);
        }
    }
}
