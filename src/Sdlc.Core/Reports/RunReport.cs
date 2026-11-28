using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Deviations;
using Sdlc.Core.Git;
using Sdlc.Core.Series;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

namespace Sdlc.Core.Reports;

/// <summary>
/// The one report a run of several cycles ends on -- a wave's or a series' -- and the head of its one pull request's
/// body, after the agent's short summary.
/// </summary>
/// <remarks>
/// <para><strong>One report at the level of what ran, never one per cycle.</strong> Each cycle gets a short entry: what
/// it is, how it ended, and the facts its own report would carry -- deviations, choices, what was changed at the
/// person's request, concerns -- rendered by the same code, so a fact is said one way wherever it is said (D-181).</para>
/// <para><strong>What a run adds is what stopped.</strong> A blocked verification stops only the cycles that depend on
/// it, so each cycle that did not pass is named with what it held back: the successors that succeed it and the parked
/// cycles that wait on it, followed through each other.</para>
/// <para><strong>A series asked nobody</strong>, so every intent line marked <c>(inferred)</c> is listed as not
/// confirmed by anyone. And every rule, decision and successor the report names is shown with its text.</para>
/// </remarks>
public static class RunReport
{
    /// <summary>The name a kept copy is filed under in <c>.sdlc/reports/</c>.</summary>
    public const string Kind = "run-report";

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddRunReportRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks for a run's report.</summary>
    /// <param name="Run">Which size of run.</param>
    /// <param name="Id">The run's id.</param>
    /// <param name="Changed">What the run's branch changes against the integration branch, whose cycle folders are the run's cycles.</param>
    public sealed record Query(RunKind Run, string Id, IReadOnlyList<string> Changed) : IRequest<Response>;

    /// <summary>One cycle the run carries.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Headline">What its intent is about, when it says.</param>
    /// <param name="Outcome">pass, blocked, escalated, abandoned, or planned, not built.</param>
    /// <param name="Lead">Its intent's <c>lead:</c>, when it has one.</param>
    /// <param name="Report">Its own report's facts, when it could be read.</param>
    /// <param name="Inferred">Every line of its intent marked <c>(inferred)</c>.</param>
    public sealed record Entry(CycleId Id, string? Headline, string Outcome, string? Lead, BuildReport.Response? Report, IReadOnlyList<string> Inferred);

    /// <summary>A cycle that did not pass, and what it held back.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Outcome">How it ended.</param>
    /// <param name="HeldBack">The successors and parked cycles that depend on it, directly or through each other.</param>
    public sealed record Stopped(CycleId Cycle, string Outcome, IReadOnlyList<string> HeldBack);

    /// <summary>The run's report.</summary>
    /// <param name="Run">Which size of run.</param>
    /// <param name="Id">The run's id.</param>
    /// <param name="Record">The series' record, when the run is a series and it could be read.</param>
    /// <param name="Cycles">Every cycle the run carries, oldest first.</param>
    /// <param name="Stops">Every cycle that did not pass, with what it held back.</param>
    /// <param name="Named">Every rule, decision and successor the report names, with its text.</param>
    public sealed record Response(
        RunKind Run,
        string Id,
        SeriesRecord? Record,
        IReadOnlyList<Entry> Cycles,
        IReadOnlyList<Stopped> Stops,
        IReadOnlyList<IdTexts.Named> Named)
    {
        /// <summary>Whether any cycle's deviations have something wrong with them, which its pull request check refuses.</summary>
        public bool Unsound => Cycles.Any(cycle => cycle.Report is { } report && report.Deviations.Problems.Count > 0);
    }

    /// <summary>The report as markdown, ending with what each id it names says.</summary>
    /// <param name="report">What was read.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response report)
    {
        ArgumentNullException.ThrowIfNull(report);

        string body = Body(report);
        string named = IdTexts.Render(report.Named);

        return named.Length == 0 ? body : body + "\n" + named;
    }

    /// <summary>The report without its <c>Named:</c> block, which is what the ids are found in.</summary>
    private static string Body(Response report)
    {
        StringBuilder text = new();
        string branch = RunBranch.NameOf(report.Run, report.Id);

        text.Append(CultureInfo.InvariantCulture, $"## {(report.Run == RunKind.Series ? "Series" : "Wave")} {report.Id}\n\n");

        IEnumerable<string> counts = report.Cycles
            .GroupBy(cycle => cycle.Outcome)
            .OrderBy(group => group.Key == "pass" ? 0 : 1)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Count() + " " + (group.Key == "pass" ? "passed" : group.Key));

        text.Append(CultureInfo.InvariantCulture, $"{report.Cycles.Count} cycle{(report.Cycles.Count == 1 ? string.Empty : "s")} on {branch}: {(report.Cycles.Count == 0 ? "none" : string.Join(", ", counts))}.");

        if (report.Record is { } record)
        {
            text.Append(CultureInfo.InvariantCulture, $" {record.Waves.Count} of {record.Limit} wave{(record.Limit == 1 ? string.Empty : "s")} taken, under approval: {SolutionDocument.NameOf(record.Approval)}.");
        }

        text.Append("\n\n");

        HashSet<CycleId> shown = [];

        if (report.Record is { } series)
        {
            foreach (SeriesWave wave in series.Waves)
            {
                text.Append(CultureInfo.InvariantCulture, $"### Wave {wave.Number}\n\n");
                text.Append("Forecast: " + string.Join(", ", wave.Items.Select(Named)) + ".\n\n");

                List<string> never = [];

                foreach (SeriesItem item in wave.Items)
                {
                    Entry? ran = item.Cycle is { } id ? report.Cycles.FirstOrDefault(cycle => cycle.Id.ToString() == id) : null;

                    if (ran is null)
                    {
                        never.Add(Named(item));
                        continue;
                    }

                    if (shown.Add(ran.Id))
                    {
                        Cycle(text, ran, report.Run);
                    }
                }

                if (never.Count > 0)
                {
                    text.Append("Did not run: " + string.Join(", ", never) + ".\n\n");
                }
            }

            if (AddFix.Lines(series) is { Count: > 0 } fixes)
            {
                text.Append("### Added by the series\n\n");
                text.Append("A test that passed in one recorded run and failed in another stopped a wave's evidence, so the series fixed it first, in a cycle of its own, and held what it stopped until the fix passed (D-232).\n\n");

                foreach (string line in fixes)
                {
                    text.Append("- " + line + "\n");
                }

                text.Append('\n');
            }

            if (report.Cycles.Any(cycle => !shown.Contains(cycle.Id)))
            {
                text.Append("### Not in a recorded wave\n\n");
            }
        }

        foreach (Entry cycle in report.Cycles.Where(cycle => !shown.Contains(cycle.Id)))
        {
            Cycle(text, cycle, report.Run);
        }

        text.Append("### What stopped\n\n");

        if (report.Stops.Count == 0)
        {
            text.Append("Nothing: every cycle passed.\n");
        }
        else
        {
            foreach (Stopped stop in report.Stops)
            {
                text.Append(CultureInfo.InvariantCulture, $"- {stop.Cycle} ({stop.Outcome})");
                text.Append(stop.HeldBack.Count == 0 ? ": nothing waits on it.\n" : ": held back " + string.Join(", ", stop.HeldBack) + ".\n");
            }
        }

        if (report.Cycles.Any(cycle => cycle.Outcome is "pass" && cycle.Report is { } built && built.Deviations.InForce.Count + built.Deviations.Choices.Count > 0))
        {
            text.Append("\nMerging the pull request accepts every deviation and choice above. To change one, name its cycle and say what to do instead.\n");
        }

        return text.ToString();
    }

    private static void Cycle(StringBuilder text, Entry cycle, RunKind run)
    {
        text.Append(CultureInfo.InvariantCulture, $"#### {cycle.Id}{(cycle.Headline is { } headline ? " — " + headline : string.Empty)}\n\n");
        text.Append(CultureInfo.InvariantCulture, $"Outcome: {cycle.Outcome}.{(cycle.Lead is { } lead ? " From " + lead + "." : string.Empty)}\n\n");

        string sections = cycle.Report is { } report ? BuildReport.Sections(report) : string.Empty;
        text.Append(sections.Length > 0 ? sections : "Nothing to report.\n\n");

        if (run == RunKind.Series && cycle.Inferred.Count > 0)
        {
            text.Append("**Not confirmed by anyone** — what its intent inferred, and nobody was asked\n\n");

            foreach (string line in cycle.Inferred)
            {
                text.Append("- " + line + "\n");
            }

            text.Append('\n');
        }
    }

    private static string Named(SeriesItem item) => item.Kind switch
    {
        SeriesItemKind.Successor => "successor:" + item.Ref,
        SeriesItemKind.Parked => item.Ref + " (parked)",
        _ => item.Ref + " (approved)",
    };

    /// <summary>The run's outcome for a cycle: its verify's, abandoned, or planned and not built.</summary>
    private static string OutcomeOf(Cycle cycle) => SeriesStatus.StateOf(cycle) switch
    {
        "built: pass" => "pass",
        "planned" or "being planned" or "not here" => "planned, not built",
        string other => other,
    };

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            SeriesRecord? record = null;

            if (request.Run == RunKind.Series && File.Exists(SeriesRecord.PathIn(root, request.Id)))
            {
                Result<SeriesRecord> read = await SeriesRecord.Read(root, request.Id, cancellationToken).ConfigureAwait(false);

                if (read.IsFailed)
                {
                    return read.ToResult<Response>();
                }

                record = read.Value.Resolved(await ReadSuccessors.Starts(root, cancellationToken).ConfigureAwait(false));
            }

            List<Entry> entries = [];

            foreach (CycleId id in VerifyChain.ChangedCycles(request.Changed))
            {
                Cycle cycle = new(id, root);
                Result<BuildReport.Response> built = await mediator
                    .Send<BuildReport.Query, BuildReport.Response>(new BuildReport.Query(id), cancellationToken)
                    .ConfigureAwait(false);

                string intent = cycle.PathOf(ArtifactKind.Intent);
                string? text = File.Exists(intent) ? await File.ReadAllTextAsync(intent, cancellationToken).ConfigureAwait(false) : null;

                entries.Add(new Entry(
                    id,
                    text is null ? null : ArtifactOutlineReader.Read(text).Headline,
                    OutcomeOf(cycle),
                    text is null ? null : SeriesRecord.LeadOf(text, intent),
                    built.IsSuccess ? built.Value : null,
                    text is null ? [] : Inferred(text)));
            }

            List<Stopped> stops = [];

            if (entries.Any(entry => entry.Outcome != "pass"))
            {
                ReadSuccessors.Response backlog = await Backlog(RunBranch.NameOf(request.Run, request.Id), cancellationToken).ConfigureAwait(false);

                stops.AddRange(entries
                    .Where(entry => entry.Outcome != "pass")
                    .Select(entry => new Stopped(entry.Id, entry.Outcome, HeldBack(backlog, entry.Id))));
            }

            Response report = new(request.Run, request.Id, record, entries, stops, []);

            Result<IdTexts.Response> named = await mediator
                .Send<IdTexts.Query, IdTexts.Response>(new IdTexts.Query(IdTexts.Find(Body(report))), cancellationToken)
                .ConfigureAwait(false);

            return named.IsFailed
                ? named.ToResult<Response>()
                : Result.Ok(report with { Named = named.Value.Named });
        }

        // Judged against the run's branch when this clone has it, else the integration branch: what depends on a cycle
        // is the same either way, and only where it stands differs.
        private async Task<ReadSuccessors.Response> Backlog(string branch, CancellationToken cancellationToken)
        {
            string? onto = repository.IsRepository(solution.Path) && (repository.RefExists(solution.Path, branch) || repository.RefExists(solution.Path, "origin/" + branch))
                ? branch
                : null;

            Result<ReadSuccessors.Response> read = await mediator
                .Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query(onto), cancellationToken)
                .ConfigureAwait(false);

            return read.ValueOrDefault ?? new ReadSuccessors.Response([], [], [], null);
        }

        /// <summary>Every successor and parked cycle that waits on a cycle, directly or through each other.</summary>
        private static List<string> HeldBack(ReadSuccessors.Response backlog, CycleId stopped)
        {
            HashSet<string> reached = new(StringComparer.Ordinal) { stopped.ToString() };
            List<string> held = [];
            bool grew = true;

            while (grew)
            {
                grew = false;

                foreach (ReadSuccessors.Standing standing in backlog.Successors)
                {
                    string name = SuccessorReference.Prefix + standing.Successor.Slug;

                    if (!held.Contains(name, StringComparer.Ordinal) && standing.Waits.Any(wait => reached.Contains(wait.Cycle?.ToString() ?? wait.Reference.ToString())))
                    {
                        held.Add(name);
                        reached.Add(name);
                        grew = true;
                    }
                }

                foreach (ReadSuccessors.ParkedStanding parked in backlog.Waiting)
                {
                    string name = parked.Cycle + " (parked)";

                    if (!held.Contains(name, StringComparer.Ordinal) && parked.Waits.Any(wait => reached.Contains(wait.Cycle?.ToString() ?? wait.Reference.ToString())))
                    {
                        held.Add(name);
                        reached.Add(parked.Cycle.ToString());
                        grew = true;
                    }
                }
            }

            return held;
        }

        /// <summary>Every line of an intent's body marked <c>(inferred)</c>: what was suggested and nobody confirmed.</summary>
        private static List<string> Inferred(string intent)
        {
            string body = Frontmatter.Read(intent, "intent.md") is { IsSuccess: true } split ? split.Value.Body : intent;

            return
            [
                .. body.ReplaceLineEndings("\n").Split('\n')
                    .Where(line => line.Contains("(inferred)", StringComparison.OrdinalIgnoreCase))
                    .Select(line => line.Trim().TrimStart('-', '*', ' ').Trim())
                    .Where(line => line.Length > 0),
            ];
        }
    }
}
