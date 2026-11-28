using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Runs;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

namespace Sdlc.Core.Series;

/// <summary>
/// Runs a series to its end from the binary: wave after wave, a process to plan each, its cycles built a few at a time
/// and merged into the series' branch, a cycle that cannot be saved taken out, and one pull request at the end (D-234,
/// D-235, D-236).
/// </summary>
/// <remarks>
/// <para><strong>Each process does one step and ends.</strong> Planning a wave is the series playbook's, unattended;
/// each cycle's build is the build playbook's for that cycle; answering what stopped is the series playbook's again;
/// the pull request is <c>pr</c>'s. Between them the binary takes the wave, merges, commits and pushes, so a process
/// killed by a usage limit loses its own step and nothing else, and running this again picks up where it stopped.</para>
/// <para><strong>A stopped cycle is answered once, then taken out.</strong> Under <c>approval: none</c>, the cycles a
/// build left stopped are handed to the series playbook, which adds the fix a test that comes and goes needs (D-232) or
/// nothing. What the fix holds waits for it; everything else that stopped is taken out with its reason and its pushed
/// branch, and the series carries on (D-235).</para>
/// </remarks>
public static class DriveSeries
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddDriveSeriesRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Runs the series checked out here to its end.</summary>
    /// <param name="Id">The series.</param>
    /// <param name="PluginDirectory">This release's skills, which every process it starts loads.</param>
    /// <param name="Progress">Told one line at each step, for the person waiting.</param>
    public sealed record Command(string Id, string PluginDirectory, IProgress<string>? Progress = null) : IRequest<Response>;

    /// <summary>How the run ended.</summary>
    /// <param name="Record">The series' record, as last written.</param>
    /// <param name="Stopped">Why it stopped before its pull request, or null when it opened it.</param>
    /// <param name="Said">What the pull request's process handed back, when it ran.</param>
    public sealed record Response(SeriesRecord Record, string? Stopped, string? Said);

    /// <summary>Where each of a wave's items stands, once the series' record is read.</summary>
    /// <param name="record">The series' record, its items resolved.</param>
    /// <param name="root">The series' worktree.</param>
    /// <param name="repository">The repository.</param>
    /// <returns>Each item of the last wave, with its cycle when one is open and its state.</returns>
    internal static List<(SeriesItem Item, CycleId? Cycle, string State)> States(SeriesRecord record, string root, IRepository repository)
    {
        List<(SeriesItem, CycleId?, string)> states = [];

        foreach (SeriesItem item in record.Waves.Count == 0 ? [] : record.Waves[^1].Items)
        {
            states.Add(item.Cycle is { } named && CycleId.TryParse(named, out CycleId? id)
                ? (item, id, AddFix.StateOf(record, named, SeriesStatus.StateOf(new Cycle(id.Value, root)), root, repository))
                : (item, null, NextWave.NotStarted));
        }

        return states;
    }

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository) : IRequestHandler<Command, Response>
    {
        // A wave is planned at most twice, once as taken and once more if a fix was added; this bounds the loop well past that.
        private const int MostPasses = 400;

        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            string branch = RunBranch.NameOf(RunKind.Series, request.Id);

            for (int pass = 0; pass < MostPasses; pass++)
            {
                Result<NextWave.Response> taken = await mediator
                    .Send<NextWave.Command, NextWave.Response>(new NextWave.Command(), cancellationToken)
                    .ConfigureAwait(false);

                if (taken.IsFailed)
                {
                    if ((await Read(root, request.Id, cancellationToken).ConfigureAwait(false)).ValueOrDefault is { Finished: true })
                    {
                        break;
                    }

                    return taken.ToResult<Response>();
                }

                if (taken.Value.Wave is not { } wave)
                {
                    break;
                }

                await RunGit.Commit(root, Said($"Series {request.Id}: wave {wave.Number} taken"), cancellationToken, Cycle.SdlcDirectoryName).ConfigureAwait(false);
                request.Progress?.Report(Said($"Wave {wave.Number}: {wave.Items.Count} item{(wave.Items.Count == 1 ? string.Empty : "s")}."));

                Result<bool> waved = await Wave(request, root, wave.Number, cancellationToken).ConfigureAwait(false);

                if (waved.IsFailed)
                {
                    SeriesRecord? record = (await Read(root, request.Id, cancellationToken).ConfigureAwait(false)).ValueOrDefault;
                    return record is null
                        ? waved.ToResult<Response>()
                        : Result.Ok(new Response(record, string.Join("; ", waved.Errors.Select(error => error.Message)), null));
                }

                // A fix was added: the wave is handed back with it first, planned and built before what it holds.
                if (!waved.Value)
                {
                    continue;
                }

                Result<NextWave.Recorded> done = await mediator
                    .Send<NextWave.Done, NextWave.Recorded>(new NextWave.Done(), cancellationToken)
                    .ConfigureAwait(false);

                if (done.IsFailed)
                {
                    return Result.Ok(new Response(done.ValueOrDefault?.Record ?? taken.Value.Record, string.Join("; ", done.Errors.Select(error => error.Message)), null));
                }

                await RunGit.Commit(root, Said($"Series {request.Id}: wave {wave.Number} built"), cancellationToken, Cycle.SdlcDirectoryName).ConfigureAwait(false);
                await RunGit.Push(root, branch, cancellationToken).ConfigureAwait(false);
                request.Progress?.Report(Said($"Wave {wave.Number} recorded built, and pushed when there is a remote."));
            }

            Result<SeriesRecord> final = await Read(root, request.Id, cancellationToken).ConfigureAwait(false);

            if (final.IsFailed)
            {
                return final.ToResult<Response>();
            }

            if (await CatchUp(root, request.Id, cancellationToken).ConfigureAwait(false) is { } behind)
            {
                return Result.Ok(new Response(final.Value, behind, null));
            }

            request.Progress?.Report("Opening the series' pull request.");
            Result<AgentExit> opened = await mediator
                .Send<RunPullRequest.Launch, AgentExit>(new RunPullRequest.Launch(root, request.PluginDirectory), cancellationToken)
                .ConfigureAwait(false);

            if (opened.IsFailed)
            {
                return opened.ToResult<Response>();
            }

            return Result.Ok(new Response(
                final.Value,
                opened.Value.Succeeded ? null : "The pull request's process ended without finishing: " + (opened.Value.Problem ?? "it exited " + opened.Value.ExitCode.ToString(CultureInfo.InvariantCulture)),
                opened.Value.Said));
        }

        /// <summary>
        /// Plans and builds the open wave. True when it is ready to record built; false when a fix was added and the wave
        /// goes round again; a failure when a process stopped and the run has to be continued later.
        /// </summary>
        private async Task<Result<bool>> Wave(Command request, string root, int number, CancellationToken cancellationToken)
        {
            SeriesRecord record = (await Read(root, request.Id, cancellationToken).ConfigureAwait(false)).Value;

            if (States(record, root, repository).Any(state => NeedsPlanning(state.Item, state.State)))
            {
                request.Progress?.Report(Said($"Planning wave {number}."));
                Result planned = await Step(request, root, SeriesStep.Plan, null, cancellationToken).ConfigureAwait(false);

                if (planned.IsFailed)
                {
                    return planned.ToResult<bool>();
                }

                await RunGit.Commit(root, Said($"Series {request.Id}: wave {number} planned"), cancellationToken, Cycle.SdlcDirectoryName).ConfigureAwait(false);
            }

            HashSet<CycleId> tried = [];

            while (true)
            {
                record = (await Read(root, request.Id, cancellationToken).ConfigureAwait(false)).Value;
                List<CycleId> buildable =
                [
                    .. States(record, root, repository)
                        .Where(state => state.Cycle is { } id && !tried.Contains(id) && state.State is SeriesStatus.Planned or AddFix.BuildAgain)
                        .Select(state => state.Cycle!.Value),
                ];

                if (buildable.Count == 0)
                {
                    return Result.Ok(true);
                }

                tried.UnionWith(buildable);
                SeriesWave wave = record.Waves[^1];
                Result<WaveBuild.Response> built = await mediator
                    .Send<WaveBuild.Command, WaveBuild.Response>(
                        new WaveBuild.Command(RunKind.Series, request.Id, buildable, wave.Parallel ?? record.Parallel, request.PluginDirectory, request.Progress),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (built.IsFailed)
                {
                    return built.ToResult<bool>();
                }

                if (built.Value.Stopped is not { Count: > 0 } stopped)
                {
                    continue;
                }

                int fixes = record.Fixes?.Count ?? 0;

                // Answered once a wave, and only where nobody approves plans: a fix is work the series adds itself (D-232).
                if (record.Approval == ApprovalRequirement.None && record.Fixes?.Any(fix => fix.Wave == number) != true)
                {
                    string file = Path.Combine(Path.GetTempPath(), "sdlc-stops-" + Guid.NewGuid().ToString("N") + ".md");

                    try
                    {
                        await File.WriteAllTextAsync(file, Stops(stopped), cancellationToken).ConfigureAwait(false);
                        Result answered = await Step(request, root, SeriesStep.Stops, file, cancellationToken).ConfigureAwait(false);

                        if (answered.IsFailed)
                        {
                            return answered.ToResult<bool>();
                        }
                    }
                    finally
                    {
                        File.Delete(file);
                    }

                    record = (await Read(root, request.Id, cancellationToken).ConfigureAwait(false)).Value;
                }

                IReadOnlyList<string> held = (record.Fixes?.Count ?? 0) > fixes ? record.Fixes![^1].Holds : [];
                List<TakenOut> leaving =
                [
                    .. stopped
                        .Where(cycle => !held.Contains(cycle.Id.ToString(), StringComparer.Ordinal))
                        .Select(cycle => new TakenOut(cycle.Id.ToString(), number, cycle.Detail, cycle.Branch, cycle.Pushed)),
                ];

                if (leaving.Count > 0)
                {
                    await File.WriteAllTextAsync(SeriesRecord.PathIn(root, request.Id), record.WithTakenOut(leaving).Write(), cancellationToken).ConfigureAwait(false);
                    request.Progress?.Report(Said($"Wave {number} took out {string.Join(", ", leaving.Select(taken => taken.Cycle))}."));
                }

                await RunGit.Commit(root, Said($"Series {request.Id}: wave {number} answers what stopped"), cancellationToken, Cycle.SdlcDirectoryName).ConfigureAwait(false);

                if (held.Count > 0)
                {
                    return Result.Ok(false);
                }
            }
        }

        /// <summary>Starts one of the series' processes and reads how it ended.</summary>
        private async Task<Result> Step(Command request, string root, SeriesStep step, string? stops, CancellationToken cancellationToken)
        {
            Result<AgentExit> exit = await mediator
                .Send<SeriesRun.Launch, AgentExit>(new SeriesRun.Launch(request.Id, root, request.PluginDirectory, step, stops), cancellationToken)
                .ConfigureAwait(false);

            if (exit.IsFailed)
            {
                return exit.ToResult();
            }

            return exit.Value.Succeeded
                ? Result.Ok()
                : Result.Fail(Said($"The series' {(step == SeriesStep.Plan ? "planning" : "answer to what stopped")} ended without finishing ({exit.Value.Problem ?? "it exited " + exit.Value.ExitCode.ToString(CultureInfo.InvariantCulture)}). '/sdlc:series {request.Id}' continues it from there."));
        }

        /// <summary>
        /// Merges the integration branch in when the series fell behind it, tested as any merge is (D-227, D-234). Null
        /// when the branch is current or now is; otherwise why the pull request waits for a person.
        /// </summary>
        private async Task<string?> CatchUp(string root, string id, CancellationToken cancellationToken)
        {
            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);
            FreshnessCheck check = TargetFreshness.Check(repository, root, integration);

            if (check.Standing != Freshness.Behind)
            {
                return null;
            }

            Result<MergeIntoRun.Response> merged = await mediator
                .Send<MergeIntoRun.Command, MergeIntoRun.Response>(
                    new MergeIntoRun.Command(RunKind.Series, id, "origin/" + integration, "the integration branch " + integration),
                    cancellationToken)
                .ConfigureAwait(false);

            if (merged.IsFailed)
            {
                return string.Join("; ", merged.Errors.Select(error => error.Message));
            }

            return merged.Value.Outcome == MergeIntoRun.Outcome.Merged
                ? null
                : Said($"Series {id} fell behind {integration}, and taking it in did not hold: {merged.Value.Detail} No pull request was opened; '/sdlc:remediate' settles it with a person.");
        }

        private static bool NeedsPlanning(SeriesItem item, string state) =>
            state is NextWave.NotStarted or SeriesStatus.BeingPlanned
            || (item.Kind == SeriesItemKind.Parked && state == SeriesStatus.ParkedState);

        private static string Stops(IReadOnlyList<WaveBuild.Built> stopped)
        {
            StringBuilder text = new();
            text.AppendLine("# What the wave's build left stopped");
            text.AppendLine();

            foreach (WaveBuild.Built cycle in stopped)
            {
                text.AppendLine("## Cycle " + cycle.Id);
                text.AppendLine();
                text.AppendLine(cycle.Detail);
                text.AppendLine();

                if (cycle.Said is { Length: > 0 } said)
                {
                    text.AppendLine("What its build handed back:");
                    text.AppendLine();
                    text.AppendLine(said.Trim());
                    text.AppendLine();
                }
            }

            return text.ToString();
        }

        private static async Task<Result<SeriesRecord>> Read(string root, string id, CancellationToken cancellationToken)
        {
            Result<SeriesRecord> read = await SeriesRecord.Read(root, id, cancellationToken).ConfigureAwait(false);

            return read.IsFailed
                ? read
                : Result.Ok(read.Value.Resolved(await ReadSuccessors.Starts(root, cancellationToken).ConfigureAwait(false)));
        }

        private static string Said(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
    }
}
