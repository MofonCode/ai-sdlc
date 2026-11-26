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
/// </remarks>
public static class NextWave
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddNextWaveRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Asks for the next wave of the series checked out here.</summary>
    public sealed record Command : IRequest<Response>;

    /// <summary>The wave taken, or that there was none to take.</summary>
    /// <param name="Record">The record, as written.</param>
    /// <param name="Branch">The series' branch.</param>
    /// <param name="Wave">The wave taken, or null when nothing was ready and the series is finished early.</param>
    /// <param name="Left">How many more were ready than the wave size let it take, for a later wave.</param>
    public sealed record Response(SeriesRecord Record, string Branch, SeriesWave? Wave, int Left = 0);

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
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Nothing is ready against {taken.Branch}, so series {taken.Record.Id} is finished early, after {taken.Record.Waves.Count} of its {taken.Record.Limit} waves. Nothing was recorded. Open its pull request."));
            return text.ToString();
        }

        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Wave {wave.Number} of {taken.Record.Limit}, on {taken.Branch}: {wave.Items.Count} item{(wave.Items.Count == 1 ? string.Empty : "s")}, recorded in {SeriesRecord.RelativeDirectory(taken.Record.Id)}/{SeriesRecord.FileName}."));

        foreach (SeriesItem item in wave.Items)
        {
            string what = item.Kind switch
            {
                SeriesItemKind.Successor => "successor:" + item.Ref,
                SeriesItemKind.Parked when taken.Record.Approval == ApprovalRequirement.Required => item.Ref + " (approved, parked)",
                SeriesItemKind.Parked => item.Ref + " (parked)",
                _ => item.Ref + " (approved)",
            };

            text.AppendLine("  " + what + " -- " + StartOf(item, taken.Branch, taken.Record.Approval));
        }

        if (taken.Left > 0)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{taken.Left} more {(taken.Left == 1 ? "is" : "are")} ready, past this series' wave size of {taken.Record.WaveSize}: {(taken.Left == 1 ? "it waits" : "they wait")} for a later wave."));
        }

        text.AppendLine("Plan and build these one after another, never in parallel: one item finished before the next starts.");

        return text.ToString();
    }

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository, TimeProvider clock)
        : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            string? on = repository.IsRepository(root) ? repository.CurrentBranch(root) : null;

            if (!RunBranch.TryParse(on, out RunKind kind, out string? id) || kind != RunKind.Series)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"A series' wave is taken on its own branch, series/<id>, and this is {on ?? "no branch"}. Run it in the series' worktree, or name it with --solution; 'sdlc series start' opens one."));
            }

            string branch = RunBranch.NameOf(RunKind.Series, id);
            Result<SeriesRecord> read = await SeriesRecord.Read(root, id, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            if (read.Value.Finished)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Series {id} is finished: it has taken all {read.Value.Limit} of its waves. Open its pull request; a new series takes what is ready next."));
            }

            SeriesRecord record = read.Value.Resolved(await ReadSuccessors.Starts(root, cancellationToken).ConfigureAwait(false));
            HashSet<string> taken = new(record.Waves.SelectMany(wave => wave.Items).SelectMany(item => new[] { item.Ref, item.Cycle }).OfType<string>(), StringComparer.Ordinal);
            List<SeriesItem> items = [];

            if (record.Approval == ApprovalRequirement.None)
            {
                Result<ReadSuccessors.Response> ready = await mediator
                    .Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query(branch), cancellationToken)
                    .ConfigureAwait(false);

                if (ready.IsFailed)
                {
                    return ready.ToResult<Response>();
                }

                // A parked cycle first: it was planned once already, so it is the nearer to done.
                items.AddRange(ready.Value.Waiting
                    .Where(parked => parked.State == SuccessorState.Ready && !taken.Contains(parked.Cycle.ToString()))
                    .Select(parked => new SeriesItem(SeriesItemKind.Parked, parked.Cycle.ToString(), parked.Cycle.ToString())));

                items.AddRange(ready.Value.Successors
                    .Where(standing => standing.State == SuccessorState.Ready && !taken.Contains(standing.Successor.Slug))
                    .Select(standing => new SeriesItem(SeriesItemKind.Successor, standing.Successor.Slug, null)));
            }
            else
            {
                Result<ListQueuedCycles.Response> queued = await mediator
                    .Send<ListQueuedCycles.Query, ListQueuedCycles.Response>(new ListQueuedCycles.Query(Onto: branch), cancellationToken)
                    .ConfigureAwait(false);

                if (queued.IsFailed)
                {
                    return queued.ToResult<Response>();
                }

                items.AddRange(queued.Value.Cycles
                    .Where(cycle => !taken.Contains(cycle.Id.ToString()))
                    .Select(cycle => new SeriesItem(SeriesItemKind.Approved, cycle.Id.ToString(), cycle.Id.ToString())));

                // An approved cycle that was parked is never in the queue: it waits on code that had not merged. Once
                // what it waits on is ready against the series branch it is taken back and built, as approved; a
                // person read its plan, so nothing is planned again.
                Result<ReadSuccessors.Response> ready = await mediator
                    .Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query(branch), cancellationToken)
                    .ConfigureAwait(false);

                if (ready.IsFailed)
                {
                    return ready.ToResult<Response>();
                }

                Result<ComputeClaimGraph.Response> graph = await mediator
                    .Send<ComputeClaimGraph.Query, ComputeClaimGraph.Response>(new ComputeClaimGraph.Query(), cancellationToken)
                    .ConfigureAwait(false);

                if (graph.IsFailed)
                {
                    return graph.ToResult<Response>();
                }

                HashSet<CycleId> approved = [.. graph.Value.Claims.Select(claim => claim.Cycle)];

                items.AddRange(ready.Value.Waiting
                    .Where(parked => parked.State == SuccessorState.Ready && approved.Contains(parked.Cycle) && !taken.Contains(parked.Cycle.ToString()))
                    .Select(parked => new SeriesItem(SeriesItemKind.Parked, parked.Cycle.ToString(), parked.Cycle.ToString())));
            }

            // What is ready past the wave size waits for the next wave, still ready then (D-228).
            int left = Math.Max(0, items.Count - record.WaveSize);
            SeriesWave? wave = null;

            if (items.Count > 0)
            {
                record = record.WithWave(clock.GetUtcNow(), [.. items.Take(record.WaveSize)]);
                wave = record.Waves[^1];
            }

            // The resolved items are written back even when nothing was ready: the next reader should not resolve again.
            if (wave is not null || !string.Equals(record.Write(), read.Value.Write(), StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(SeriesRecord.PathIn(root, id), record.Write(), cancellationToken).ConfigureAwait(false);
            }

            return Result.Ok(new Response(record, branch, wave, left));
        }
    }
}
