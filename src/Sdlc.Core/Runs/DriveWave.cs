using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Series;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Runs;

/// <summary>
/// Builds a wave that stands alone, <c>wave/&lt;id&gt;</c>, from the binary: every cycle it carries ready to build,
/// a few at a time, each merged as it passes; a cycle that does not pass taken out; then one pull request (D-234,
/// D-235, D-236).
/// </summary>
/// <remarks>
/// The same build a series' wave gets, without the planning: a wave's cycles were planned and approved in their slicing
/// before the wave was opened. What it took out is in its own record, <c>.sdlc/waves/&lt;id&gt;/wave.json</c>, beside
/// how many of its cycles build at once.
/// </remarks>
public static class DriveWave
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddDriveWaveRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Builds the wave checked out here.</summary>
    /// <param name="Id">The wave's id.</param>
    /// <param name="PluginDirectory">This release's skills.</param>
    /// <param name="Parallel">How many of its cycles build at once, or null to keep what its record says.</param>
    /// <param name="OpenPullRequest">False to build and merge and stop there, opening nothing.</param>
    /// <param name="Progress">Told one line at each step.</param>
    public sealed record Command(string Id, string PluginDirectory, int? Parallel = null, bool OpenPullRequest = true, IProgress<string>? Progress = null)
        : IRequest<Response>;

    /// <summary>How the wave ended.</summary>
    /// <param name="Record">Its record, as written.</param>
    /// <param name="Built">Each cycle's end.</param>
    /// <param name="Stopped">Why it stopped before its pull request, or null.</param>
    /// <param name="Said">What the pull request's process handed back, when it ran.</param>
    public sealed record Response(WaveRecord Record, IReadOnlyList<WaveBuild.Built> Built, string? Stopped, string? Said);

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            string branch = RunBranch.NameOf(RunKind.Wave, request.Id);

            if (!string.Equals(repository.CurrentBranch(root), branch, StringComparison.Ordinal))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Wave {request.Id} builds in its worktree, on {branch}, and {root} is on {repository.CurrentBranch(root) ?? "no branch"}."));
            }

            Result<WaveRecord> read = await WaveRecord.Read(root, request.Id, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            WaveRecord record = request.Parallel is { } parallel ? read.Value with { Parallel = Math.Max(1, parallel) } : read.Value;
            await record.Write(root, cancellationToken).ConfigureAwait(false);

            Result<ResolveCycle.Response> ready = await mediator
                .Send<ResolveCycle.Query, ResolveCycle.Response>(new ResolveCycle.Query(NextStep.Build), cancellationToken)
                .ConfigureAwait(false);

            if (ready.IsFailed)
            {
                return ready.ToResult<Response>();
            }

            HashSet<string> leaving = new(record.TakenOut.Select(taken => taken.Cycle), StringComparer.Ordinal);
            List<CycleId> cycles = [.. ready.Value.Ready.Select(candidate => candidate.Id).Where(id => !leaving.Contains(id.ToString())).Order()];
            List<WaveBuild.Built> ends = [];

            if (cycles.Count > 0)
            {
                Result<WaveBuild.Response> built = await mediator
                    .Send<WaveBuild.Command, WaveBuild.Response>(
                        new WaveBuild.Command(RunKind.Wave, request.Id, cycles, record.Parallel, request.PluginDirectory, request.Progress),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (built.IsFailed)
                {
                    return built.ToResult<Response>();
                }

                ends.AddRange(built.Value.Cycles);
                record = record with
                {
                    TakenOut =
                    [
                        .. record.TakenOut,
                        .. built.Value.Stopped.Select(cycle => new TakenOut(cycle.Id.ToString(), 1, cycle.Detail, cycle.Branch, cycle.Pushed)),
                    ],
                };
            }

            await record.Write(root, cancellationToken).ConfigureAwait(false);
            await RunGit.Commit(root, string.Create(CultureInfo.InvariantCulture, $"Wave {request.Id}: built"), cancellationToken, Cycle.SdlcDirectoryName).ConfigureAwait(false);
            await RunGit.Push(root, branch, cancellationToken).ConfigureAwait(false);

            if (!request.OpenPullRequest)
            {
                return Result.Ok(new Response(record, ends, null, null));
            }

            if (await CatchUp(root, request.Id, cancellationToken).ConfigureAwait(false) is { } behind)
            {
                return Result.Ok(new Response(record, ends, behind, null));
            }

            request.Progress?.Report("Opening the wave's pull request.");
            Result<AgentExit> opened = await mediator
                .Send<RunPullRequest.Launch, AgentExit>(new RunPullRequest.Launch(root, request.PluginDirectory), cancellationToken)
                .ConfigureAwait(false);

            if (opened.IsFailed)
            {
                return opened.ToResult<Response>();
            }

            return Result.Ok(new Response(
                record,
                ends,
                opened.Value.Succeeded ? null : "The pull request's process ended without finishing: " + (opened.Value.Problem ?? "it exited " + opened.Value.ExitCode.ToString(CultureInfo.InvariantCulture)),
                opened.Value.Said));
        }

        private async Task<string?> CatchUp(string root, string id, CancellationToken cancellationToken)
        {
            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);

            if (TargetFreshness.Check(repository, root, integration).Standing != Freshness.Behind)
            {
                return null;
            }

            Result<MergeIntoRun.Response> merged = await mediator
                .Send<MergeIntoRun.Command, MergeIntoRun.Response>(
                    new MergeIntoRun.Command(RunKind.Wave, id, "origin/" + integration, "the integration branch " + integration),
                    cancellationToken)
                .ConfigureAwait(false);

            if (merged.IsFailed)
            {
                return string.Join("; ", merged.Errors.Select(error => error.Message));
            }

            return merged.Value.Outcome == MergeIntoRun.Outcome.Merged
                ? null
                : string.Create(CultureInfo.InvariantCulture, $"Wave {id} fell behind {integration}, and taking it in did not hold: {merged.Value.Detail} No pull request was opened; '/sdlc:remediate' settles it with a person.");
        }
    }
}

/// <summary>Starts the process that opens a run's one pull request: <c>pr --run</c>, unattended, with a build's tools.</summary>
public static class RunPullRequest
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddRunPullRequestRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Launch, AgentExit>, Handler>();
    }

    /// <summary>The prompt the process is started with.</summary>
    public const string Prompt = "/sdlc:pr --run";

    /// <summary>Starts it in the run's worktree, and waits for it.</summary>
    /// <param name="Worktree">The run's worktree.</param>
    /// <param name="PluginDirectory">This release's skills.</param>
    public sealed record Launch(string Worktree, string PluginDirectory) : IRequest<AgentExit>;

    private sealed class Handler(IAgentProcess agents) : IRequestHandler<Launch, AgentExit>
    {
        public Task<Result<AgentExit>> Handle(Launch request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            return BuildRun.Start(
                agents,
                request.Worktree,
                tools => ["--plugin-dir", request.PluginDirectory, "-p", Prompt, .. BuildRun.OutputFormat, "--allowedTools", .. BuildRun.AllowedToolsWith(tools)],
                cancellationToken);
        }
    }
}
