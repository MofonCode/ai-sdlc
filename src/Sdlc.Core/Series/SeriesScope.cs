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
/// A series' list: everything it may take, worked out from the backlog against the series branch and frozen when it
/// starts, in the waves it is forecast to take them in (D-231).
/// </summary>
/// <remarks>
/// <para><strong>The list is the scope; the waves are the record.</strong> Each wave still takes what is ready when it
/// starts, but only from the list, so a successor added once the series started never slips in, and the person sees
/// what the series will take on before anything is spent. The forecast wave beside each item is a forecast: planning
/// can park a cycle, or a verification block one, and the waves taken say what happened.</para>
/// <para><strong>It holds only what the series can reach</strong>: an item ready now, or one waiting only on items
/// the list holds before it. One waiting on work the series does not carry -- a cycle open elsewhere -- can never be
/// ready on the series branch, so it is left out and counted. So is what the waves times the wave size cannot hold.</para>
/// <para><strong>Frozen once.</strong> <c>sdlc series start</c> freezes it in the series' worktree; a series started
/// before the list existed, or whose list was never frozen, has it frozen as its first wave is taken, and one that has
/// already taken a wave without one takes whatever is ready, as it did.</para>
/// </remarks>
public static class SeriesScope
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddSeriesScopeRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Freezes the list of the series checked out here, and commits it.</summary>
    public sealed record Command : IRequest<Response>;

    /// <summary>The list, as frozen.</summary>
    /// <param name="Record">The record, as written.</param>
    /// <param name="Left">How many could be reached but are past what the waves times the wave size can hold.</param>
    /// <param name="Outside">How many wait on work the series does not carry.</param>
    /// <param name="Already">Whether the list had been frozen before, and was left as it was.</param>
    public sealed record Response(SeriesRecord Record, int Left, int Outside, bool Already = false);

    /// <summary>The list as a person reads it, in the waves it is forecast to take them in.</summary>
    /// <param name="frozen">The list.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response frozen)
    {
        ArgumentNullException.ThrowIfNull(frozen);

        SeriesRecord record = frozen.Record;
        IReadOnlyList<SeriesScopeItem> scope = record.Scope ?? [];
        StringBuilder text = new();

        if (scope.Count == 0)
        {
            text.AppendLine("Its list is empty: nothing is ready, or waiting only on what it would build, so it finishes at its first wave.");
        }
        else
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Its list: {scope.Count} item{(scope.Count == 1 ? string.Empty : "s")}, the most it takes on. Each wave takes from it what is ready then, so the waves below are a forecast:"));

            foreach (SeriesScopeItem item in scope)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  wave {item.Wave}: {Named(item.Kind, item.Ref, record.Approval)}"));
            }
        }

        if (frozen.Left > 0)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{frozen.Left} more {(frozen.Left == 1 ? "is" : "are")} past what {record.Limit} wave{(record.Limit == 1 ? string.Empty : "s")} of {record.WaveSize} can take: a later series takes {(frozen.Left == 1 ? "it" : "them")}."));
        }

        if (frozen.Outside > 0)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{frozen.Outside} wait{(frozen.Outside == 1 ? "s" : string.Empty)} on work this series does not carry, so {(frozen.Outside == 1 ? "it is" : "they are")} left out: 'sdlc backlog' says on what."));
        }

        return text.ToString();
    }

    /// <summary>What a person reads an item on a list or in a wave as.</summary>
    /// <param name="kind">What starts it.</param>
    /// <param name="reference">A successor's slug, or a cycle id.</param>
    /// <param name="approval">The setting the series runs under.</param>
    /// <returns>The name.</returns>
    public static string Named(SeriesItemKind kind, string reference, ApprovalRequirement approval = ApprovalRequirement.None) => kind switch
    {
        SeriesItemKind.Successor => "successor:" + reference,
        SeriesItemKind.Parked when approval == ApprovalRequirement.Required => reference + " (approved, parked)",
        SeriesItemKind.Parked => reference + " (parked)",
        _ => reference + " (approved)",
    };

    /// <summary>
    /// Everything the series could take, ready or waiting, against <paramref name="onto"/>: under <c>approval: none</c>
    /// every parked cycle and successor; under <c>approval: required</c> every approved cycle nothing has built and
    /// every approved parked cycle. Nearest first, parked and approved cycles before successors at the same distance.
    /// </summary>
    internal static async Task<Result<IReadOnlyList<Candidate>>> Candidates(ApprovalRequirement approval, string onto, Mediator mediator, CancellationToken cancellationToken)
    {
        Result<ReadSuccessors.Response> read = await mediator
            .Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query(onto), cancellationToken)
            .ConfigureAwait(false);

        if (read.IsFailed)
        {
            return read.ToResult<IReadOnlyList<Candidate>>();
        }

        IReadOnlyDictionary<string, int> depth = BacklogRenderer.Forecast(read.Value);
        List<Candidate> candidates = [];
        IEnumerable<ReadSuccessors.ParkedStanding> parked = read.Value.Waiting.Where(standing => standing.State is SuccessorState.Ready or SuccessorState.Waiting);

        if (approval == ApprovalRequirement.None)
        {
            candidates.AddRange(parked.Select(standing => Of(SeriesItemKind.Parked, standing.Cycle.ToString(), standing.State, standing.Waits, depth)));
            candidates.AddRange(read.Value.Successors
                .Where(standing => standing.State is SuccessorState.Ready or SuccessorState.Waiting)
                .Select(standing => Of(SeriesItemKind.Successor, standing.Successor.Slug, standing.State, standing.Waits, depth)));
        }
        else
        {
            Result<ListQueuedCycles.Response> queued = await mediator
                .Send<ListQueuedCycles.Query, ListQueuedCycles.Response>(new ListQueuedCycles.Query(Onto: onto), cancellationToken)
                .ConfigureAwait(false);

            if (queued.IsFailed)
            {
                return queued.ToResult<IReadOnlyList<Candidate>>();
            }

            candidates.AddRange(queued.Value.Cycles.Select(cycle => new Candidate(SeriesItemKind.Approved, cycle.Id.ToString(), true, [], 0)));

            // An approved cycle that was parked is never in the queue: it waits on code that had not merged. Once what
            // it waits on is ready against the series branch it is taken back and built, as approved.
            Result<ComputeClaimGraph.Response> graph = await mediator
                .Send<ComputeClaimGraph.Query, ComputeClaimGraph.Response>(new ComputeClaimGraph.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (graph.IsFailed)
            {
                return graph.ToResult<IReadOnlyList<Candidate>>();
            }

            HashSet<CycleId> approved = [.. graph.Value.Claims.Select(claim => claim.Cycle)];
            candidates.AddRange(parked
                .Where(standing => approved.Contains(standing.Cycle))
                .Select(standing => Of(SeriesItemKind.Parked, standing.Cycle.ToString(), standing.State, standing.Waits, depth)));
        }

        return Result.Ok<IReadOnlyList<Candidate>>([.. candidates.OrderBy(candidate => candidate.Depth).ThenBy(candidate => candidate.Kind == SeriesItemKind.Successor ? 1 : 0)]);
    }

    /// <summary>
    /// The list: what the series can reach, in order, each in the first wave its predecessors allow that has room;
    /// with how many it could reach past what its waves can hold, and how many wait on work it does not carry.
    /// </summary>
    internal static Planned Plan(IReadOnlyList<Candidate> candidates, int limit, int waveSize)
    {
        // Reachable: ready now, or waiting only on what is reachable.
        HashSet<string> reachable = new(StringComparer.Ordinal);

        for (bool grew = true; grew;)
        {
            grew = false;

            foreach (Candidate candidate in candidates.Where(candidate => !reachable.Contains(candidate.Key) && candidate.WaitsOn.All(reachable.Contains)))
            {
                grew |= reachable.Add(candidate.Key);
            }
        }

        Dictionary<string, int> wave = new(StringComparer.Ordinal);
        int[] filled = new int[limit + 2];
        List<SeriesScopeItem> items = [];

        foreach (Candidate candidate in candidates.Where(candidate => reachable.Contains(candidate.Key)))
        {
            // One whose predecessor did not fit does not fit either.
            if (!candidate.WaitsOn.All(wave.ContainsKey))
            {
                continue;
            }

            int earliest = 1 + candidate.WaitsOn.Select(waited => wave[waited]).DefaultIfEmpty(0).Max();
            int placed = Enumerable.Range(earliest, Math.Max(0, limit - earliest + 1)).FirstOrDefault(number => filled[number] < waveSize);

            if (placed == 0)
            {
                continue;
            }

            filled[placed]++;
            wave[candidate.Key] = placed;
            items.Add(new SeriesScopeItem(candidate.Kind, candidate.Ref, placed));
        }

        return new Planned(items, reachable.Count - items.Count, candidates.Count - reachable.Count);
    }

    /// <summary>The series checked out here, or why this is not a series' branch.</summary>
    internal static Result<string> SeriesHere(IRepository repository, string root)
    {
        string? on = repository.IsRepository(root) ? repository.CurrentBranch(root) : null;

        return RunBranch.TryParse(on, out RunKind kind, out string? id) && kind == RunKind.Series
            ? Result.Ok(id)
            : Result.Fail<string>(string.Create(
                CultureInfo.InvariantCulture,
                $"A series' wave is taken on its own branch, series/<id>, and this is {on ?? "no branch"}. Run it in the series' worktree, or name it with --solution; 'sdlc series start' opens one."));
    }

    private static Candidate Of(SeriesItemKind kind, string reference, SuccessorState state, IReadOnlyList<ReadSuccessors.Wait> waits, IReadOnlyDictionary<string, int> depth)
    {
        string key = SeriesRecord.KeyOf(kind, reference);

        return new Candidate(
            kind,
            reference,
            state == SuccessorState.Ready,
            [.. waits.Where(wait => wait.State != WaitState.Merged).Select(wait => wait.Cycle?.ToString() ?? wait.Reference.ToString())],
            depth.TryGetValue(key, out int merges) ? merges : 0);
    }

    /// <summary>One thing a series could take.</summary>
    /// <param name="Kind">What starts it.</param>
    /// <param name="Ref">A successor's slug, or a cycle id.</param>
    /// <param name="Ready">Whether it is ready against the series branch now.</param>
    /// <param name="WaitsOn">What it waits on that has not passed there, named as the backlog names it.</param>
    /// <param name="Depth">How many merges stand between it and its start.</param>
    internal sealed record Candidate(SeriesItemKind Kind, string Ref, bool Ready, IReadOnlyList<string> WaitsOn, int Depth)
    {
        /// <summary>What names it, as the backlog does.</summary>
        public string Key => SeriesRecord.KeyOf(Kind, Ref);
    }

    /// <summary>A list worked out, and what it left out.</summary>
    /// <param name="Items">The list.</param>
    /// <param name="Left">Reachable, but past what the waves can hold.</param>
    /// <param name="Outside">Waiting on work the series does not carry.</param>
    internal sealed record Planned(IReadOnlyList<SeriesScopeItem> Items, int Left, int Outside);

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            Result<string> here = SeriesHere(repository, root);

            if (here.IsFailed)
            {
                return here.ToResult<Response>();
            }

            string id = here.Value;
            Result<SeriesRecord> read = await SeriesRecord.Read(root, id, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            if (read.Value.Scope is not null)
            {
                return Result.Ok(new Response(read.Value, 0, 0, Already: true));
            }

            if (read.Value.Waves.Count > 0)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Series {id} has taken {read.Value.Waves.Count} wave{(read.Value.Waves.Count == 1 ? string.Empty : "s")} without a list, so it goes on taking whatever is ready: a list is frozen only before the first wave."));
            }

            Result<Response> frozen = await FreezeIn(read.Value, root, mediator, cancellationToken).ConfigureAwait(false);

            if (frozen.IsFailed)
            {
                return frozen;
            }

            Result committed = repository.Commit(
                root,
                [SeriesRecord.RelativeDirectory(id) + "/" + SeriesRecord.FileName],
                string.Create(CultureInfo.InvariantCulture, $"Freeze series {id}'s list of {frozen.Value.Record.Scope!.Count}"));

            return committed.IsFailed ? committed : frozen;
        }
    }

    /// <summary>Works out a series' list against its branch, and writes the record with it. Commits nothing.</summary>
    internal static async Task<Result<Response>> FreezeIn(SeriesRecord record, string root, Mediator mediator, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<Candidate>> candidates = await Candidates(record.Approval, RunBranch.NameOf(RunKind.Series, record.Id), mediator, cancellationToken).ConfigureAwait(false);

        if (candidates.IsFailed)
        {
            return candidates.ToResult<Response>();
        }

        Planned planned = Plan(candidates.Value, record.Limit, record.WaveSize);
        SeriesRecord frozen = record.WithScope(planned.Items);
        await File.WriteAllTextAsync(SeriesRecord.PathIn(root, record.Id), frozen.Write(), cancellationToken).ConfigureAwait(false);

        return Result.Ok(new Response(frozen, planned.Left, planned.Outside));
    }
}
