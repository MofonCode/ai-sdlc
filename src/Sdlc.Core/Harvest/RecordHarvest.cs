using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Harvest;

/// <summary>
/// Records one aspect's sitting once the person has placed its draft: which completed cycles have now answered that
/// aspect (D-191). Closes nothing.
/// </summary>
/// <remarks>
/// <para><strong>A sitting answers one aspect.</strong> Each of the five is read and answered by its own skill, which
/// can be run alone, so recording one is never closing a cycle: <c>sdlc harvest close</c> closes a cycle once every
/// aspect has been answered for it.</para>
/// <para><strong>Recorded once what it decided is in the files.</strong> A draft still beside its file is a decision
/// the person has not made, and recording over it would mark an aspect answered that was not.</para>
/// </remarks>
public static class RecordHarvest
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddRecordHarvestRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Records one sitting.</summary>
    /// <param name="Aspect">The aspect it answered.</param>
    /// <param name="From">The proposal file the person marked.</param>
    public sealed record Command(HarvestAspect Aspect, string From) : IRequest<Response>;

    /// <summary>What was recorded, and what is left.</summary>
    /// <param name="Aspect">The aspect answered.</param>
    /// <param name="Recorded">The cycles that have now answered it.</param>
    /// <param name="Watched">How many concerns the ledger now watches.</param>
    /// <param name="Ignored">The concerns the ledger has seen ignored, and how many cycles each was ignored in.</param>
    /// <param name="Open">Every completed cycle not closed yet, with the aspects it still needs.</param>
    public sealed record Response(
        HarvestAspect Aspect,
        IReadOnlyList<CycleId> Recorded,
        int Watched,
        IReadOnlyList<Watched> Ignored,
        IReadOnlyList<HarvestReport.OpenCycle> Open);

    /// <summary>
    /// Reads the proposal file's <c>## Watch</c>, <c>## Ignored</c> and <c>## Declined</c> sections: <c>-
    /// &lt;concern&gt; — &lt;cycle&gt;, &lt;cycle&gt;</c> for the first two and <c>- &lt;suggestion id&gt;:
    /// &lt;reason&gt;</c> for the last.
    /// </summary>
    /// <param name="markdown">The proposal file.</param>
    /// <returns>The concerns watched, the suggestions declined, every accepted heading, and the concerns ignored.</returns>
    public static (IReadOnlyList<Watched> Watch, IReadOnlyList<HarvestDecline> Declined, IReadOnlyList<string> Accepted, IReadOnlyList<Watched> Ignored) Read(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        IReadOnlyDictionary<string, IReadOnlyList<string>> sections = ArtifactSections.Read(markdown);
        List<HarvestDecline> declined = [];

        foreach (string line in Bullets(sections, "Declined"))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);

            if (colon > 0 && line[(colon + 1)..].Trim().Length > 0)
            {
                declined.Add(new HarvestDecline(line[..colon].Trim().Trim('`'), line[(colon + 1)..].Trim()));
            }
        }

        List<string> accepted = [.. markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Where(line => line.StartsWith("### [x]", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[7..].Trim())];

        return (Concerns(sections, "Watch"), declined, accepted, Concerns(sections, "Ignored"));
    }

    /// <summary>
    /// Reads the proposal file's <c>## Cycles</c> section: the cycles the sitting read, one <c>- &lt;id&gt;</c>
    /// each (D-166).
    /// </summary>
    /// <param name="markdown">The proposal file.</param>
    /// <returns>The cycles named, or null when the file has no such section, which records every eligible one.</returns>
    public static IReadOnlyList<CycleId>? Sitting(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        IReadOnlyDictionary<string, IReadOnlyList<string>> sections = ArtifactSections.Read(markdown);

        return sections.ContainsKey("Cycles")
            ? [.. Bullets(sections, "Cycles")
                .Select(line => CycleId.TryParse(line.Trim('`'), out CycleId? id) ? id : null)
                .Where(id => id is not null)
                .Select(id => id!.Value)]
            : null;
    }

    // A concern and the cycles it was seen in: "- <gist> — <cycle>, <cycle>".
    private static List<Watched> Concerns(IReadOnlyDictionary<string, IReadOnlyList<string>> sections, string name)
    {
        List<Watched> concerns = [];

        foreach (string line in Bullets(sections, name))
        {
            int dash = line.LastIndexOf(" — ", StringComparison.Ordinal);
            dash = dash < 0 ? line.LastIndexOf(" -- ", StringComparison.Ordinal) : dash;

            if (dash > 0)
            {
                concerns.Add(new Watched(
                    line[..dash].Trim(),
                    [.. line[(dash + 3)..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(id => id.Trim('-', ' '))]));
            }
        }

        return concerns;
    }

    private static IEnumerable<string> Bullets(IReadOnlyDictionary<string, IReadOnlyList<string>> sections, string name) =>
        sections.TryGetValue(name, out IReadOnlyList<string>? lines)
            ? lines.Select(line => line.Trim()).Where(line => line.StartsWith("- ", StringComparison.Ordinal)).Select(line => line[2..].Trim())
            : [];

    private sealed class Handler(SolutionRoot solution, Mediator mediator, TimeProvider clock) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!File.Exists(request.From))
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"There is no proposal file at {request.From}. '/sdlc:harvest-{HarvestAspects.NameOf(request.Aspect)}' writes one, and the person marks it."));
            }

            if (HarvestAspects.DraftOf(request.Aspect) is { } draft && File.Exists(Path.Combine(solution.Path, Cycle.SdlcDirectoryName, draft)))
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"{draft} is still beside the real file. Rename it into place, or delete it if you decided against it, then record the sitting."));
            }

            Result<HarvestLedger> ledger = await HarvestLedger.Read(solution.Path, cancellationToken).ConfigureAwait(false);

            if (ledger.IsFailed)
            {
                return ledger.ToResult<Response>();
            }

            // The cycles the proposal file names, or every completed one still needing this aspect. Each must still
            // need it: a cycle already answered, closed, or not completed is refused by the report, as it is there.
            string proposal = await File.ReadAllTextAsync(request.From, cancellationToken).ConfigureAwait(false);

            Result<HarvestReport.Response> report = await mediator
                .Send<HarvestReport.Query, HarvestReport.Response>(new HarvestReport.Query(request.Aspect, Sitting(proposal)), cancellationToken)
                .ConfigureAwait(false);

            if (report.IsFailed)
            {
                return report.ToResult<Response>();
            }

            if (report.Value.Sitting.Count == 0)
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"No completed cycle still needs {HarvestAspects.NameOf(request.Aspect)} answered, so there is nothing to record."));
            }

            (IReadOnlyList<Watched> watch, IReadOnlyList<HarvestDecline> declined, IReadOnlyList<string> accepted, IReadOnlyList<Watched> ignored) = Read(proposal);

            HarvestLedger written = ledger.Value.With(
                new HarvestSitting(clock.GetUtcNow(), request.Aspect, [.. report.Value.Sitting.Select(cycle => cycle.ToString())], accepted, declined),
                watch,
                ignored);

            await File.WriteAllTextAsync(HarvestLedger.PathIn(solution.Path), written.Write(), cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(
                request.Aspect,
                report.Value.Sitting,
                written.Watch.Count,
                written.Ignored ?? [],
                [.. report.Value.Open.Select(open => open with { Missing = written.Missing(open.Cycle.ToString()) })]));
        }
    }
}
