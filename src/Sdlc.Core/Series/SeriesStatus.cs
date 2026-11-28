using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

namespace Sdlc.Core.Series;

/// <summary>
/// Where a series stands: its record, each wave's items and the state of the cycle each became, and the forecast --
/// the backlog judged against the series branch.
/// </summary>
/// <remarks>
/// The forecast is a forecast: planning can find two siblings need each other and park one a wave later, so what the
/// waves took is shown beside it rather than replaced by it.
/// </remarks>
public static class SeriesStatus
{
    /// <summary>What a cycle with an intent and no plan yet says it is.</summary>
    public const string BeingPlanned = "being planned";

    /// <summary>What a cycle with a plan and no verification yet says it is.</summary>
    public const string Planned = "planned";

    /// <summary>What a cycle waiting, with its spec and plan kept, on code that has not merged says it is.</summary>
    public const string ParkedState = "parked";

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddSeriesStatusRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks where a series stands.</summary>
    /// <param name="Id">The series, or null for the one checked out here.</param>
    public sealed record Query(string? Id = null) : IRequest<Response>;

    /// <summary>One item of a wave and where its cycle stands.</summary>
    /// <param name="Wave">The wave's number.</param>
    /// <param name="Item">The item.</param>
    /// <param name="State">Where its cycle stands, in a word or two.</param>
    /// <param name="Steps">Which of its steps its cycle's own files show done, and the next, as <see cref="StepsOf"/> says.</param>
    public sealed record ItemState(int Wave, SeriesItem Item, string State, string Steps = "");

    /// <summary>Where the series stands.</summary>
    /// <param name="Record">Its record, its successors resolved.</param>
    /// <param name="Items">Every item of every wave, in order.</param>
    /// <param name="Forecast">The backlog, judged against the series branch when it exists here.</param>
    /// <param name="Onto">The branch the forecast was judged against, or null for the integration branch.</param>
    public sealed record Response(SeriesRecord Record, IReadOnlyList<ItemState> Items, ReadSuccessors.Response Forecast, string? Onto);

    /// <summary>Where one cycle of a run stands, in a word or two: abandoned, built and its outcome, or how far planned.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The state.</returns>
    public static string StateOf(Cycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        if (!cycle.Exists)
        {
            return "not here";
        }

        if (cycle.IsAbandoned)
        {
            return "abandoned";
        }

        string verify = cycle.PathOf(ArtifactKind.Verify);

        if (File.Exists(verify)
            && Frontmatter.Read(File.ReadAllText(verify), verify) is { IsSuccess: true } split
            && Frontmatter.Key(split.Value.Yaml, "outcome") is { } outcome)
        {
            return string.Equals(outcome.Trim(), "pass", StringComparison.OrdinalIgnoreCase) ? "built: pass" : outcome.Trim();
        }

        if (File.Exists(Parked.PathOf(cycle)))
        {
            return ParkedState;
        }

        return File.Exists(cycle.PathOf(ArtifactKind.Plan)) ? Planned : BeingPlanned;
    }

    /// <summary>
    /// Which of a cycle's steps its own files show done -- its intent, spec and plan written, its build verified -- and
    /// the step next, worked out on every read and never stored, so it cannot disagree with them (D-231).
    /// </summary>
    /// <param name="cycle">The cycle, or null for an item no cycle has been opened for.</param>
    /// <returns>The steps, as in <c>intent, spec written; plan next</c>; empty for an abandoned cycle or one not here.</returns>
    public static string StepsOf(Cycle? cycle)
    {
        if (cycle is null)
        {
            return "nothing written; intent next";
        }

        if (!cycle.Exists || cycle.IsAbandoned)
        {
            return string.Empty;
        }

        (string Name, ArtifactKind Kind)[] planning = [("intent", ArtifactKind.Intent), ("spec", ArtifactKind.Spec), ("plan", ArtifactKind.Plan)];
        string[] written = [.. planning.Where(step => File.Exists(cycle.PathOf(step.Kind))).Select(step => step.Name)];
        string done = written.Length == 0 ? "nothing written" : string.Join(", ", written) + " written";

        if (File.Exists(cycle.PathOf(ArtifactKind.Verify)))
        {
            return done + ", build verified";
        }

        if (File.Exists(Parked.PathOf(cycle)))
        {
            return done + "; waits on what it needs, then a later wave takes it back";
        }

        string? missing = planning.Select(step => step.Name).FirstOrDefault(name => !written.Contains(name, StringComparer.Ordinal));
        return done + "; " + (missing ?? "build") + " next";
    }

    /// <summary>The series as a person reads it.</summary>
    /// <param name="status">Where it stands.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response status)
    {
        ArgumentNullException.ThrowIfNull(status);

        SeriesRecord record = status.Record;
        StringBuilder text = new();

        IReadOnlyList<SeriesItem> taken = [.. record.Waves.SelectMany(wave => wave.Items)];
        string listed = record.Scope is { } list
            ? string.Create(CultureInfo.InvariantCulture, $"; {list.Count(item => taken.Any(entry => entry.Kind == item.Kind && entry.Ref == item.Ref))} of the {list.Count} item{(list.Count == 1 ? string.Empty : "s")} on its list taken")
            : string.Empty;

        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Series {record.Id}, started {record.Started:yyyy-MM-dd HH:mm} UTC under approval: {SolutionDocument.NameOf(record.Approval)}: {record.Waves.Count} of {record.Limit} wave{(record.Limit == 1 ? string.Empty : "s")} taken{listed}{(record.Finished ? ", finished" : string.Empty)}."));

        if (record.Scope is { Count: > 0 } scope)
        {
            text.AppendLine();
            text.AppendLine("Its list, in the waves forecast when it started:");

            foreach (SeriesScopeItem item in scope)
            {
                SeriesWave? took = record.Waves.FirstOrDefault(wave => wave.Items.Any(entry => entry.Kind == item.Kind && entry.Ref == item.Ref));
                string where = took is null ? "not taken yet" : string.Create(CultureInfo.InvariantCulture, $"taken in wave {took.Number}");
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  wave {item.Wave}: {SeriesScope.Named(item.Kind, item.Ref, record.Approval)} -- {where}"));
            }
        }

        foreach (SeriesWave wave in record.Waves)
        {
            text.AppendLine();
            string done = wave.Done is { } at ? string.Create(CultureInfo.InvariantCulture, $", built {at:yyyy-MM-dd HH:mm} UTC") : ", not recorded built";
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Wave {wave.Number}, taken {wave.Started:yyyy-MM-dd HH:mm} UTC{done}:"));

            foreach (ItemState item in status.Items.Where(entry => entry.Wave == wave.Number))
            {
                string cycle = item.Item.Cycle is { } id && item.Item.Kind == SeriesItemKind.Successor ? ", cycle " + id : string.Empty;
                string steps = item.Steps.Length > 0 ? " (" + item.Steps + ")" : string.Empty;
                text.AppendLine("  " + SeriesScope.Named(item.Item.Kind, item.Item.Ref) + cycle + " -- " + item.State + steps);
            }
        }

        if (AddFix.Lines(record) is { Count: > 0 } fixes)
        {
            text.AppendLine();
            text.AppendLine("Added by the series -- a fault that stopped a wave's evidence, fixed first in a cycle of its own (D-232):");

            foreach (string line in fixes)
            {
                text.AppendLine("  " + line);
            }
        }

        text.AppendLine();
        text.AppendLine("The forecast -- what comes ready next, and after which merges:");
        text.AppendLine();
        text.Append(BacklogRenderer.Render(status.Forecast, status.Onto));

        return text.ToString();
    }

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            string? on = repository.IsRepository(root) ? repository.CurrentBranch(root) : null;
            string? id = request.Id?.Trim();

            if (id is null)
            {
                if (!RunBranch.TryParse(on, out RunKind kind, out string? here) || kind != RunKind.Series)
                {
                    return Result.Fail<Response>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"Which series? This is {on ?? "no branch"}, not a series' branch. Name it -- 'sdlc series status <id>' -- or run it in the series' worktree."));
                }

                id = here;
            }

            if (!CycleId.TryParse(id, out _))
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"'{id}' is not a series id: one is shaped as a cycle id, as in 20261121_0930_15."));
            }

            Result<SeriesRecord> read = await SeriesRecord.Read(root, id, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            SeriesRecord record = read.Value.Resolved(await ReadSuccessors.Starts(root, cancellationToken).ConfigureAwait(false));

            if (!string.Equals(record.Write(), read.Value.Write(), StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(SeriesRecord.PathIn(root, id), record.Write(), cancellationToken).ConfigureAwait(false);
            }

            List<ItemState> items = [];

            foreach (SeriesWave wave in record.Waves)
            {
                foreach (SeriesItem item in wave.Items)
                {
                    Cycle? opened = item.Cycle is { } cycle && CycleId.TryParse(cycle, out CycleId? parsed) ? new Cycle(parsed.Value, root) : null;
                    string state = opened is null ? NextWave.NotStarted : AddFix.StateOf(record, item.Cycle!, StateOf(opened), root, repository);
                    items.Add(new ItemState(wave.Number, item, state, StepsOf(opened)));
                }
            }

            string branch = RunBranch.NameOf(RunKind.Series, id);
            string? onto = repository.IsRepository(root) && (repository.RefExists(root, branch) || repository.RefExists(root, "origin/" + branch)) ? branch : null;

            Result<ReadSuccessors.Response> forecast = await mediator
                .Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query(onto), cancellationToken)
                .ConfigureAwait(false);

            return forecast.IsFailed
                ? forecast.ToResult<Response>()
                : Result.Ok(new Response(record, items, forecast.Value, onto));
        }
    }
}
