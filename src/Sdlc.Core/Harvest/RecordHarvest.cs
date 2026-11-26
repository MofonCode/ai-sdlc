using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Reports;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

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
    /// <summary>
    /// What follows a cycle's id, <c>&lt;cycle&gt;/*</c>, in a covered line that answers every open suggestion of an
    /// abandoned cycle by the successor taking its work up (D-225).
    /// </summary>
    public const string WholeCycle = "/*";

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
    /// <param name="FrameworkNotes">How many notes for the framework the sitting added to its file (D-204).</param>
    public sealed record Response(
        HarvestAspect Aspect,
        IReadOnlyList<CycleId> Recorded,
        int Watched,
        IReadOnlyList<Watched> Ignored,
        IReadOnlyList<HarvestReport.OpenCycle> Open,
        int FrameworkNotes = 0);

    /// <summary>What a proposal file answered, as <see cref="Read"/> finds it.</summary>
    /// <param name="Watch">The concerns watched.</param>
    /// <param name="Declined">The suggestions declined, one per id, each with why.</param>
    /// <param name="Accepted">Every heading marked <c>[x]</c>, or <c>[h]</c> for a successor accepted and held (D-220).</param>
    /// <param name="Ignored">The concerns ignored.</param>
    /// <param name="Covered">The <c>## Covered</c> lines, as written: checked before anything is recorded.</param>
    public sealed record Answers(
        IReadOnlyList<Watched> Watch,
        IReadOnlyList<HarvestDecline> Declined,
        IReadOnlyList<string> Accepted,
        IReadOnlyList<Watched> Ignored,
        IReadOnlyList<Cover> Covered);

    /// <summary>
    /// One <c>## Covered</c> line: <c>- &lt;suggestion id&gt;[, &lt;suggestion id&gt;…]: &lt;ref&gt; — &lt;how it
    /// covers it&gt;</c>, optionally followed by an indented <c>Concern: &lt;gist&gt;</c> line naming the ignored or
    /// watched concern it closes.
    /// </summary>
    /// <param name="Line">The line as written, without its bullet.</param>
    /// <param name="Ids">The suggestions it answers; empty when the line does not read.</param>
    /// <param name="By">
    /// What covers them, as written without backticks: a rule id, a decision id, <c>successor:&lt;slug&gt;</c>, or a
    /// path under the solution. Empty when the line does not read.
    /// </param>
    /// <param name="Reason">How it covers them; empty when the line does not read.</param>
    /// <param name="Concern">The concern it closes, worded as the tally words it, or null.</param>
    /// <param name="Theme">The <c>### &lt;theme&gt;</c> it stands under, or null.</param>
    public sealed record Cover(string Line, IReadOnlyList<string> Ids, string By, string Reason, string? Concern = null, string? Theme = null)
    {
        /// <summary>Whether the line reads: ids, what covers them, and how.</summary>
        public bool Reads => Ids.Count > 0 && By.Length > 0 && Reason.Length > 0;
    }

    /// <summary>
    /// Reads the proposal file's <c>## Watch</c>, <c>## Ignored</c>, <c>## Declined</c> and <c>## Covered</c>
    /// sections: <c>- &lt;concern&gt; — &lt;cycle&gt;, &lt;cycle&gt;</c> for the first two, each optionally followed by
    /// an indented <c>Why: &lt;what it prevents and promotes&gt;</c> line (D-194); <c>- &lt;suggestion id&gt;[,
    /// &lt;suggestion id&gt;…]: &lt;reason&gt;</c> for Declined; and <see cref="Cover"/> lines for Covered. A
    /// <c>### &lt;theme&gt;</c> line inside any of them groups the lines for the person's reading and changes nothing
    /// recorded.
    /// </summary>
    /// <param name="markdown">The proposal file.</param>
    /// <returns>What the file answered.</returns>
    public static Answers Read(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        IReadOnlyDictionary<string, IReadOnlyList<string>> sections = ArtifactSections.Read(markdown);
        List<HarvestDecline> declined = [];

        foreach (string line in Bullets(sections, "Declined"))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);

            if (colon > 0 && line[(colon + 1)..].Trim().Length > 0)
            {
                string reason = line[(colon + 1)..].Trim();
                declined.AddRange(Ids(line[..colon]).Select(id => new HarvestDecline(id, reason)));
            }
        }

        List<string> accepted = [.. markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Where(line => line.StartsWith("### [x]", StringComparison.OrdinalIgnoreCase) || line.StartsWith("### [h]", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[7..].Trim())];

        return new Answers(Concerns(sections, "Watch"), declined, accepted, Concerns(sections, "Ignored"), Covers(sections));
    }

    /// <summary>
    /// A section's lines with the <c>### &lt;theme&gt;</c> each stands under: a theme groups lines for a person's
    /// reading, and a line before any theme stands under none.
    /// </summary>
    /// <param name="sections">The file's sections.</param>
    /// <param name="name">The section.</param>
    /// <returns>Each line that is not a theme heading, with its theme or null.</returns>
    internal static IEnumerable<(string? Theme, string Line)> Themed(IReadOnlyDictionary<string, IReadOnlyList<string>> sections, string name)
    {
        string? theme = null;

        foreach (string line in sections.TryGetValue(name, out IReadOnlyList<string>? lines) ? lines : [])
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                theme = trimmed[4..].Trim();
                theme = theme.Length == 0 ? null : theme;
                continue;
            }

            yield return (theme, line);
        }
    }

    /// <summary>The suggestion ids written before a line's colon, comma-separated, without backticks.</summary>
    /// <param name="written">The text before the colon.</param>
    /// <returns>The ids.</returns>
    internal static List<string> Ids(string written) =>
        [.. written.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(id => id.Trim('`').Trim()).Where(id => id.Length > 0)];

    // "- <id>, <id>: <ref> — <how>", and the "Concern: <gist>" line under it.
    private static List<Cover> Covers(IReadOnlyDictionary<string, IReadOnlyList<string>> sections)
    {
        List<Cover> covers = [];

        foreach ((string? theme, string raw) in Themed(sections, "Covered"))
        {
            string trimmed = raw.Trim();

            if (trimmed.StartsWith("Concern:", StringComparison.OrdinalIgnoreCase) && covers.Count > 0 && trimmed[8..].Trim().Length > 0)
            {
                covers[^1] = covers[^1] with { Concern = trimmed[8..].Trim() };
                continue;
            }

            if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            string line = trimmed[2..].Trim();
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            string rest = colon > 0 ? line[(colon + 1)..].Trim() : string.Empty;
            int dash = rest.IndexOf(" — ", StringComparison.Ordinal);
            int width = 3;

            if (dash < 0)
            {
                dash = rest.IndexOf(" -- ", StringComparison.Ordinal);
                width = 4;
            }

            covers.Add(colon > 0 && dash > 0
                ? new Cover(line, Ids(line[..colon]), rest[..dash].Trim().Trim('`').Trim(), rest[(dash + width)..].Trim(), Theme: theme)
                : new Cover(line, [], string.Empty, string.Empty, Theme: theme));
        }

        return covers;
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

    // A concern and the cycles it was seen in: "- <gist> — <cycle>, <cycle>", and the "Why: ..." line under it.
    private static List<Watched> Concerns(IReadOnlyDictionary<string, IReadOnlyList<string>> sections, string name)
    {
        List<Watched> concerns = [];

        foreach (string raw in sections.TryGetValue(name, out IReadOnlyList<string>? lines) ? lines : [])
        {
            string trimmed = raw.Trim();

            if (trimmed.StartsWith("Why:", StringComparison.OrdinalIgnoreCase) && concerns.Count > 0 && trimmed[4..].Trim().Length > 0)
            {
                concerns[^1] = concerns[^1] with { Why = trimmed[4..].Trim() };
                continue;
            }

            if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            string line = trimmed[2..].Trim();
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

            (IReadOnlyList<Watched> watch, IReadOnlyList<HarvestDecline> declined, IReadOnlyList<string> accepted, IReadOnlyList<Watched> ignored, IReadOnlyList<Cover> covered) = Read(proposal);

            // A covered answer names what handles the suggestion, and that thing must be real: checked before anything
            // is written, so one line naming nothing refuses the whole sitting rather than half of it.
            HashSet<string> concerns = new(
                ledger.Value.Watch.Concat(ledger.Value.Ignored ?? []).Concat(watch).Concat(ignored).Select(concern => concern.Gist),
                StringComparer.Ordinal);
            HashSet<string> abandoned = new(
                report.Value.Open.Where(open => open.Abandoned && report.Value.Sitting.Contains(open.Cycle)).Select(open => open.Cycle.ToString()),
                StringComparer.Ordinal);
            Result unresolved = await Resolved(covered, concerns, abandoned, cancellationToken).ConfigureAwait(false);

            if (unresolved.IsFailed)
            {
                return unresolved.ToResult<Response>();
            }

            DateTimeOffset now = clock.GetUtcNow();
            List<string> sitting = [.. report.Value.Sitting.Select(cycle => cycle.ToString())];

            // The framework's share, kept apart from the solution's: the report it was written from is gitignored and
            // the cycles behind it go at close, so without this it is lost (D-204). Read before anything is written,
            // so a note that does not read refuses the whole sitting rather than half of it.
            Result<IReadOnlyList<FrameworkNote>> notes = FrameworkHarvest.ReadNotes(
                proposal,
                request.Aspect,
                now,
                sitting,
                report.Value.Record.ToDictionary(record => record.Cycle.ToString(), record => record.Releases ?? [], StringComparer.Ordinal));

            if (notes.IsFailed)
            {
                return notes.ToResult<Response>();
            }

            Result<FrameworkHarvest> framework = await FrameworkHarvest.Read(solution.Path, cancellationToken).ConfigureAwait(false);

            if (framework.IsFailed)
            {
                return framework.ToResult<Response>();
            }

            List<HarvestCover> covers = [.. covered.SelectMany(cover => cover.Ids.Select(id => new HarvestCover(id, cover.By, cover.Reason)))];
            HarvestLedger written = ledger.Value.With(
                new HarvestSitting(now, request.Aspect, sitting, accepted, declined, covers.Count > 0 ? covers : null),
                watch,
                ignored,
                [.. covered.Select(cover => cover.Concern).OfType<string>()]);

            await File.WriteAllTextAsync(HarvestLedger.PathIn(solution.Path), written.Write(), cancellationToken).ConfigureAwait(false);

            if (notes.Value.Count > 0)
            {
                await File.WriteAllTextAsync(FrameworkHarvest.PathIn(solution.Path), framework.Value.Adding(notes.Value).Write(), cancellationToken).ConfigureAwait(false);
            }

            return Result.Ok(new Response(
                request.Aspect,
                report.Value.Sitting,
                written.Watch.Count,
                written.Ignored ?? [],
                [.. report.Value.Open.Select(open => open with { Missing = written.Missing(open.Cycle.ToString()) })],
                notes.Value.Count));
        }

        // Every covered line reads, and what it names is real: a rule in force, a standing decision, a successor
        // with a file or a cycle started from it, or a file or directory under the solution.
        // A covered line's Concern: must name a concern the ledger or this sitting tallies, word for word, or it would
        // close nothing and the concern would go on counting.
        // A whole cycle's suggestions, '<cycle>/*', are answered at once only for an abandoned cycle of this sitting, and
        // only by the successor that takes its work up: that successor replans the work, so each suggestion against the
        // abandoned approach is either carried by it or moot (D-225).
        private async Task<Result> Resolved(IReadOnlyList<Cover> covered, HashSet<string> concerns, HashSet<string> abandoned, CancellationToken cancellationToken)
        {
            if (covered.Count == 0)
            {
                return Result.Ok();
            }

            List<string> ids = [.. covered.Where(cover => cover.Reads && IdTexts.IsId(cover.By)).Select(cover => cover.By).Distinct(StringComparer.Ordinal)];
            Dictionary<string, IdTexts.Named> named = new(StringComparer.Ordinal);

            if (ids.Count > 0)
            {
                Result<IdTexts.Response> resolved = await mediator
                    .Send<IdTexts.Query, IdTexts.Response>(new IdTexts.Query(ids), cancellationToken)
                    .ConfigureAwait(false);

                if (resolved.IsFailed)
                {
                    return resolved.ToResult();
                }

                foreach (IdTexts.Named id in resolved.Value.Named)
                {
                    named[id.Id] = id;
                }
            }

            List<string> refused = [];

            foreach (Cover cover in covered)
            {
                if (!cover.Reads)
                {
                    refused.Add("'- " + cover.Line + "' does not read: write '- <suggestion id>: <rule, decision, successor:<slug> or path> — <how it covers it>'.");
                }
                else if (named.TryGetValue(cover.By, out IdTexts.Named? id))
                {
                    if (!id.Standing)
                    {
                        refused.Add("'- " + cover.Line + "': " + Unresolved(cover.By, id.Text is not null) + ".");
                    }
                }
                else if (!IsPathHere(cover.By))
                {
                    refused.Add("'- " + cover.Line + "': " + cover.By + " is not a rule, a decision, successor:<slug>, or a file or directory in this solution.");
                }

                foreach (string whole in cover.Ids.Where(id => id.EndsWith(WholeCycle, StringComparison.Ordinal)))
                {
                    string cycle = whole[..^WholeCycle.Length];

                    if (!abandoned.Contains(cycle))
                    {
                        refused.Add("'- " + cover.Line + "': " + whole + " answers every suggestion of a cycle at once, and " + cycle
                            + " is not an abandoned cycle of this sitting. Answer a passed cycle's suggestions one by one.");
                    }
                    else if (!cover.By.StartsWith(SuccessorReference.Prefix, StringComparison.Ordinal))
                    {
                        refused.Add("'- " + cover.Line + "': " + whole + " is answered whole only by the successor that takes the abandoned work up: write successor:<slug>.");
                    }
                }

                if (cover.Concern is { } gist && !concerns.Contains(gist))
                {
                    refused.Add("'Concern: " + gist + "' matches no concern ignored or watched: its wording must match the report's 'ignored before' or watch line exactly.");
                }
            }

            return refused.Count > 0
                ? Result.Fail("Under '## Covered', nothing was recorded: " + string.Join(" ", refused)
                    + " Name what really covers each, or decline it with why if nothing does, then record the sitting again.")
                : Result.Ok();
        }

        private static string Unresolved(string by, bool superseded) =>
            ConstraintId.TryParse(by, out _)
                ? by + (superseded ? " is superseded, so it covers nothing now" : " is not a rule in force here") + " ('sdlc constraints' lists them)"
                : DecisionId.TryParse(by, out _)
                    ? by + (superseded ? " is superseded, so it covers nothing now" : " is not a decision recorded here") + " ('sdlc decisions' lists them)"
                    : by + " names no successor: there is no " + Successor.RelativePath(by[SuccessorReference.Prefix.Length..]) + " and no cycle started from it";

        private bool IsPathHere(string written)
        {
            string root = Path.GetFullPath(solution.Path);
            string full = Path.GetFullPath(Path.Combine(root, written.Replace('/', Path.DirectorySeparatorChar)));
            string inside = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

            return full.StartsWith(inside, StringComparison.OrdinalIgnoreCase) && (File.Exists(full) || Directory.Exists(full));
        }
    }
}
