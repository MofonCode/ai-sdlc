using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Factory;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

namespace Sdlc.Core.Series;

/// <summary>
/// Takes a series' next wave: what is ready against the series branch, up to the series' wave size, recorded in its
/// record, each item with what starts it.
/// </summary>
/// <remarks>
/// <para><strong>Ready is judged against the series branch</strong>, where a predecessor is ready once its passing
/// verify is committed there: under <c>approval: none</c>, every ready successor and every ready parked cycle; under
/// <c>approval: required</c>, every approved cycle nothing has built that no earlier wave took, and every approved
/// parked cycle whose predecessors are ready there, to be taken back with <c>sdlc unpark</c> and built.</para>
/// <para><strong>A wave takes no more than the series' wave size</strong> (D-228): parked or approved cycles first,
/// then successors, and what is past the size stays ready for a later wave. A wave of every ready successor was a
/// backlog of twenty-five planned at once, a session's limit spent in minutes with nothing built.</para>
/// <para><strong>It records and never commits.</strong> The series playbook commits the record with what the wave did,
/// so a wave that started nothing leaves no commit behind.</para>
/// <para><strong>A wave left open is finished before the next is taken</strong> (D-230). The series records each wave
/// built with <see cref="Done"/>; a series that stopped partway through one -- a usage limit, a process killed -- is
/// handed that wave back when it runs again, each item with what is left of it, rather than a new wave that would count
/// the stopped one against the limit and never take its items again. An open wave whose every item is built or stopped
/// is recorded done as the next is taken, so a series that stopped between building and recording loses nothing.</para>
/// </remarks>
public static class NextWave
{
    /// <summary>What an item no cycle has been opened for says it is.</summary>
    public const string NotStarted = "not started";

    /// <summary>What a parked item ready to be taken back, and not yet taken back, says it is.</summary>
    public const string ParkedReady = "parked, ready";

    /// <summary>Registers this operation's handlers.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddNextWaveRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddTransient<IRequestHandler<Command, Response>, Handler>()
            .AddTransient<IRequestHandler<Done, Recorded>, DoneHandler>();
    }

    /// <summary>Asks for the next wave of the series checked out here.</summary>
    public sealed record Command : IRequest<Response>;

    /// <summary>The wave taken, or that there was none to take.</summary>
    /// <param name="Record">The record, as written.</param>
    /// <param name="Branch">The series' branch.</param>
    /// <param name="Wave">The wave taken, or null when nothing was ready and the series is finished early.</param>
    /// <param name="Left">How many more were ready than the wave size let it take, for a later wave.</param>
    /// <param name="Open">
    /// When <paramref name="Wave"/> is one the series stopped partway through, where each of its items stands, in order;
    /// null for a wave just taken.
    /// </param>
    public sealed record Response(SeriesRecord Record, string Branch, SeriesWave? Wave, int Left = 0, IReadOnlyList<string>? Open = null);

    /// <summary>Records the last wave of the series checked out here built, so the next is taken rather than it again.</summary>
    public sealed record Done : IRequest<Recorded>;

    /// <summary>The wave recorded built.</summary>
    /// <param name="Record">The record, as written.</param>
    /// <param name="Wave">The wave.</param>
    /// <param name="Already">Whether it had been recorded built before.</param>
    public sealed record Recorded(SeriesRecord Record, SeriesWave Wave, bool Already);

    /// <summary>Whether an item in this state still has work in its wave: not started, not taken back, or not built.</summary>
    /// <param name="state">Where the item stands, as <see cref="SeriesStatus.StateOf"/> or this class says it.</param>
    /// <returns>True while the wave cannot be done without it.</returns>
    public static bool IsOpen(string state) =>
        state is NotStarted or ParkedReady or SeriesStatus.BeingPlanned or SeriesStatus.Planned;

    /// <summary>What starts one item of a wave.</summary>
    /// <param name="item">The item.</param>
    /// <param name="branch">The series' branch.</param>
    /// <param name="approval">
    /// The setting the series runs under. Under <c>approval: required</c> a parked item is an approved cycle, built once
    /// it is taken back rather than planned again.
    /// </param>
    /// <returns>The command or invocation.</returns>
    public static string StartOf(SeriesItem item, string branch, ApprovalRequirement approval = ApprovalRequirement.None)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Kind switch
        {
            SeriesItemKind.Successor => "/sdlc:intent --from successor:" + item.Ref + " --unattended",
            SeriesItemKind.Parked when approval == ApprovalRequirement.Required => "sdlc unpark " + item.Ref + " --onto " + branch + ", then build " + item.Ref,
            SeriesItemKind.Parked => "sdlc unpark " + item.Ref + " --onto " + branch,
            _ => "build " + item.Ref,
        };
    }

    /// <summary>The wave, one line per item with what starts it.</summary>
    /// <param name="taken">What was taken.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response taken)
    {
        ArgumentNullException.ThrowIfNull(taken);

        StringBuilder text = new();

        if (taken.Wave is not { } wave)
        {
            string why = taken.Record.Scope switch
            {
                null => "Nothing is ready against " + taken.Branch,
                { Count: 0 } => "Its list is empty",
                _ when ListTaken(taken.Record) => "It has taken everything on its list",
                _ => "Nothing left on its list is ready against " + taken.Branch,
            };

            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{why}, so series {taken.Record.Id} is finished early, after {taken.Record.Waves.Count} of its {taken.Record.Limit} waves. No wave was taken. Open its pull request."));
            return text.ToString();
        }

        text.AppendLine(taken.Open is null
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Wave {wave.Number} of {taken.Record.Limit}, on {taken.Branch}: {wave.Items.Count} item{(wave.Items.Count == 1 ? string.Empty : "s")}, recorded in {SeriesRecord.RelativeDirectory(taken.Record.Id)}/{SeriesRecord.FileName}.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Wave {wave.Number} of {taken.Record.Limit}, on {taken.Branch}, is not finished: the series stopped partway through it. Finish it before the next wave is taken."));

        for (int index = 0; index < wave.Items.Count; index++)
        {
            SeriesItem item = wave.Items[index];
            string what = item.Kind switch
            {
                SeriesItemKind.Successor => "successor:" + item.Ref,
                SeriesItemKind.Parked when taken.Record.Approval == ApprovalRequirement.Required => item.Ref + " (approved, parked)",
                SeriesItemKind.Parked => item.Ref + " (parked)",
                _ => item.Ref + " (approved)",
            };

            string next = taken.Open is { } open
                ? LeftOf(item, open[index], taken.Branch, taken.Record.Approval)
                : StartOf(item, taken.Branch, taken.Record.Approval);

            text.AppendLine("  " + what + " -- " + next);
        }

        if (taken.Left > 0)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{taken.Left} more {(taken.Left == 1 ? "is" : "are")} ready, past this series' wave size of {taken.Record.WaveSize}: {(taken.Left == 1 ? "it waits" : "they wait")} for a later wave."));
        }

        text.AppendLine("Plan and build these one after another, never in parallel: one item finished before the next starts.");
        text.AppendLine("When the wave is built, 'sdlc series wave --done' records it; the next wave is taken after that.");

        return text.ToString();
    }

    /// <summary>The wave recorded built, as a person reads it.</summary>
    /// <param name="recorded">What was recorded.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Recorded recorded)
    {
        ArgumentNullException.ThrowIfNull(recorded);

        string next = recorded.Record.Finished ? "Every wave is taken: open the series' pull request." : "'sdlc series wave' takes the next.";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Wave {recorded.Wave.Number} of series {recorded.Record.Id} {(recorded.Already ? "was already recorded built" : "is recorded built")}, at {recorded.Wave.Done:yyyy-MM-dd HH:mm} UTC. {next}\n");
    }

    // What is left of one item of a wave the series stopped partway through.
    private static string LeftOf(SeriesItem item, string state, string branch, ApprovalRequirement approval)
    {
        string cycle = item.Kind == SeriesItemKind.Successor && item.Cycle is { } id ? "cycle " + id + ", " : string.Empty;

        return state switch
        {
            NotStarted or ParkedReady => StartOf(item, branch, approval),
            SeriesStatus.BeingPlanned => cycle + state + ": to-plan takes it on from where it stopped",
            SeriesStatus.Planned => cycle + state + ": the wave's build builds it",
            _ => cycle + state + ": nothing left to do",
        };
    }

    // What a series' list names, and every cycle a listed item became; null for a series with no list, which takes
    // whatever is ready.
    private static HashSet<string>? Listed(SeriesRecord record)
    {
        if (record.Scope is null)
        {
            return null;
        }

        HashSet<string> listed = new(record.Scope.Select(item => SeriesRecord.KeyOf(item.Kind, item.Ref)), StringComparer.Ordinal);

        foreach (SeriesItem item in record.Waves.SelectMany(wave => wave.Items))
        {
            if (listed.Contains(SeriesRecord.KeyOf(item.Kind, item.Ref)) && item.Cycle is { } cycle)
            {
                listed.Add(cycle);
            }
        }

        return listed;
    }

    // Whether everything on a series' list has been taken.
    private static bool ListTaken(SeriesRecord record) =>
        record.Scope is { } scope
        && scope.All(item => record.Waves.SelectMany(wave => wave.Items).Any(taken => taken.Kind == item.Kind && taken.Ref == item.Ref));

    // Where each item of the last wave stands, while that wave is not recorded built and one of its items still has
    // work; null when the next wave can be taken.
    private static async Task<IReadOnlyList<string>?> OpenStates(SeriesRecord record, string root, string branch, Mediator mediator, CancellationToken cancellationToken)
    {
        if (record.Waves.Count == 0 || record.Waves[^1].Done is not null)
        {
            return null;
        }

        IReadOnlyList<SeriesItem> items = record.Waves[^1].Items;
        HashSet<CycleId> ready = [];

        if (items.Any(item => item.Kind == SeriesItemKind.Parked))
        {
            Result<ReadSuccessors.Response> read = await mediator
                .Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query(branch), cancellationToken)
                .ConfigureAwait(false);

            if (read.IsSuccess)
            {
                ready.UnionWith(read.Value.Waiting.Where(parked => parked.State == SuccessorState.Ready).Select(parked => parked.Cycle));
            }
        }

        List<string> states = [];

        foreach (SeriesItem item in items)
        {
            if (item.Cycle is not { } named || !CycleId.TryParse(named, out CycleId? id))
            {
                states.Add(NotStarted);
                continue;
            }

            string state = SeriesStatus.StateOf(new Cycle(id.Value, root));

            // A parked item not yet taken back is the wave's to take back while it is ready against the series branch.
            // Any other parked cycle -- one its planning parked -- has stopped in this wave, and a later wave takes it
            // back (D-231).
            states.Add(item.Kind == SeriesItemKind.Parked && state == SeriesStatus.ParkedState && ready.Contains(id.Value) ? ParkedReady : state);
        }

        return states.Any(IsOpen) ? states : null;
    }

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository, TimeProvider clock)
        : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            Result<string> here = SeriesScope.SeriesHere(repository, root);

            if (here.IsFailed)
            {
                return here.ToResult<Response>();
            }

            string id = here.Value;
            string branch = RunBranch.NameOf(RunKind.Series, id);
            Result<SeriesRecord> read = await SeriesRecord.Read(root, id, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            SeriesRecord record = read.Value.Resolved(await ReadSuccessors.Starts(root, cancellationToken).ConfigureAwait(false));

            // A wave the series stopped partway through is handed back before the limit is asked, so the last wave is
            // finished too (D-230).
            if (await OpenStates(record, root, branch, mediator, cancellationToken).ConfigureAwait(false) is { } open)
            {
                await WriteIfChanged(root, id, read.Value, record, cancellationToken).ConfigureAwait(false);
                return Result.Ok(new Response(record, branch, record.Waves[^1], Open: open));
            }

            // An open wave with nothing left in it is done, and recorded so.
            record = record.WithWaveDone(clock.GetUtcNow());

            if (record.Finished)
            {
                await WriteIfChanged(root, id, read.Value, record, cancellationToken).ConfigureAwait(false);
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Series {id} is finished: it has taken all {record.Limit} of its waves. Open its pull request; a new series takes what is ready next."));
            }

            // A series whose list was never frozen has it frozen as its first wave is taken (D-231).
            if (record.Scope is null && record.Waves.Count == 0)
            {
                Result<SeriesScope.Response> frozen = await SeriesScope.FreezeIn(record, root, mediator, cancellationToken).ConfigureAwait(false);

                if (frozen.IsFailed)
                {
                    return frozen.ToResult<Response>();
                }

                record = frozen.Value.Record;
            }

            Result<IReadOnlyList<SeriesScope.Candidate>> candidates = await SeriesScope
                .Candidates(record.Approval, branch, mediator, cancellationToken)
                .ConfigureAwait(false);

            if (candidates.IsFailed)
            {
                return candidates.ToResult<Response>();
            }

            HashSet<string> taken = new(record.Waves.SelectMany(wave => wave.Items).SelectMany(item => new[] { item.Ref, item.Cycle }).OfType<string>(), StringComparer.Ordinal);
            HashSet<string>? listed = Listed(record);

            // Ready, on the list, and not taken -- or taken and parked again by its planning, which a later wave takes
            // back once what it waits on is ready, so the series never loses it.
            List<SeriesItem> items =
            [
                .. candidates.Value
                    .Where(candidate => candidate.Ready
                        && (listed is null || listed.Contains(candidate.Key))
                        && (!taken.Contains(candidate.Ref) || candidate.Kind == SeriesItemKind.Parked))
                    .Select(candidate => new SeriesItem(candidate.Kind, candidate.Ref, candidate.Kind == SeriesItemKind.Successor ? null : candidate.Ref)),
            ];

            // What is ready past the wave size waits for the next wave, still ready then (D-228).
            int left = Math.Max(0, items.Count - record.WaveSize);
            SeriesWave? wave = null;

            if (items.Count > 0)
            {
                record = record.WithWave(clock.GetUtcNow(), [.. items.Take(record.WaveSize)]);
                wave = record.Waves[^1];
            }

            // The resolved items are written back even when nothing was ready: the next reader should not resolve again.
            await WriteIfChanged(root, id, read.Value, record, cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(record, branch, wave, left));
        }

        private static async Task WriteIfChanged(string root, string id, SeriesRecord read, SeriesRecord record, CancellationToken cancellationToken)
        {
            if (!string.Equals(record.Write(), read.Write(), StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(SeriesRecord.PathIn(root, id), record.Write(), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class DoneHandler(SolutionRoot solution, IRepository repository, TimeProvider clock) : IRequestHandler<Done, Recorded>
    {
        public async Task<Result<Recorded>> Handle(Done request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            Result<string> here = SeriesScope.SeriesHere(repository, root);

            if (here.IsFailed)
            {
                return here.ToResult<Recorded>();
            }

            Result<SeriesRecord> read = await SeriesRecord.Read(root, here.Value, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Recorded>();
            }

            if (read.Value.Waves.Count == 0)
            {
                return Result.Fail<Recorded>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Series {here.Value} has taken no wave yet, so there is none to record built. 'sdlc series wave' takes the first."));
            }

            bool already = read.Value.Waves[^1].Done is not null;
            SeriesRecord record = read.Value.WithWaveDone(clock.GetUtcNow());

            if (!already)
            {
                await File.WriteAllTextAsync(SeriesRecord.PathIn(root, here.Value), record.Write(), cancellationToken).ConfigureAwait(false);
            }

            return Result.Ok(new Recorded(record, record.Waves[^1], already));
        }
    }
}
