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
    public sealed record ItemState(int Wave, SeriesItem Item, string State);

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

        return File.Exists(cycle.PathOf(ArtifactKind.Plan)) ? Planned : BeingPlanned;
    }

    /// <summary>The series as a person reads it.</summary>
    /// <param name="status">Where it stands.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response status)
    {
        ArgumentNullException.ThrowIfNull(status);

        SeriesRecord record = status.Record;
        StringBuilder text = new();

        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Series {record.Id}, started {record.Started:yyyy-MM-dd HH:mm} UTC under approval: {SolutionDocument.NameOf(record.Approval)}: {record.Waves.Count} of {record.Limit} wave{(record.Limit == 1 ? string.Empty : "s")} taken{(record.Finished ? ", finished" : string.Empty)}."));

        foreach (SeriesWave wave in record.Waves)
        {
            text.AppendLine();
            string done = wave.Done is { } at ? string.Create(CultureInfo.InvariantCulture, $", built {at:yyyy-MM-dd HH:mm} UTC") : ", not recorded built";
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Wave {wave.Number}, taken {wave.Started:yyyy-MM-dd HH:mm} UTC{done}:"));

            foreach (ItemState item in status.Items.Where(entry => entry.Wave == wave.Number))
            {
                string what = item.Item.Kind switch
                {
                    SeriesItemKind.Successor => "successor:" + item.Item.Ref,
                    SeriesItemKind.Parked => item.Item.Ref + " (parked)",
                    _ => item.Item.Ref + " (approved)",
                };

                string cycle = item.Item.Cycle is { } id && item.Item.Kind == SeriesItemKind.Successor ? ", cycle " + id : string.Empty;
                text.AppendLine("  " + what + cycle + " -- " + item.State);
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
                    items.Add(new ItemState(
                        wave.Number,
                        item,
                        item.Cycle is { } cycle && CycleId.TryParse(cycle, out CycleId? parsed)
                            ? StateOf(new Cycle(parsed.Value, root))
                            : "not started"));
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
