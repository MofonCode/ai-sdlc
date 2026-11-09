using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Areas;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Proposals;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Mining;

/// <summary>
/// Records a mining sitting once the person has placed its drafts, and says which mined cycles are husks
/// whose folders may now be deleted (D-152).
/// </summary>
/// <remarks>
/// <para><strong>It deletes nothing.</strong> It prints the removal for the person to run, as <c>sdlc
/// discard</c> does (D-146): a husk is deleted by a person, on a branch of its own, through a pull request the
/// check admits as mining.</para>
/// <para><strong>A cycle is a husk once it is finished, merged, and recorded.</strong> Finished: its verify
/// passed, or a person abandoned it. Merged: its folder is on the integration branch, which is where a pull
/// request with no governed cycle would otherwise be refused (D-45). A cycle mined on its own branch before its
/// pull request is reported and left for the next sitting, because its folder cannot go before it merges.</para>
/// <para><strong>What later mining needs is kept, not trusted to git alone</strong>: the merge commit, the
/// files the area replay reads, the rules a three-cycle count reads, the areas the plan proposed, and the
/// concerns the person chose to watch.</para>
/// </remarks>
public static class RecordMining
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddRecordMiningRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Records one sitting.</summary>
    /// <param name="From">The proposal file the person marked.</param>
    public sealed record Command(string From) : IRequest<Response>;

    /// <summary>What was recorded, and what is left.</summary>
    /// <param name="Recorded">The cycles recorded as mined: each now a husk.</param>
    /// <param name="Unmerged">Finished cycles whose folders are not on the integration branch yet.</param>
    /// <param name="Unfinished">Cycles still in flight, which mining leaves alone.</param>
    /// <param name="Watched">How many concerns the ledger now watches.</param>
    /// <param name="IntegrationBranch">The branch the husks' pull request goes to.</param>
    /// <param name="Ignored">The concerns the ledger has seen ignored, and how many cycles each was ignored in.</param>
    public sealed record Response(
        IReadOnlyList<MinedCycle> Recorded,
        IReadOnlyList<CycleId> Unmerged,
        IReadOnlyList<CycleId> Unfinished,
        int Watched,
        string IntegrationBranch,
        IReadOnlyList<Watched>? Ignored = null);

    /// <summary>The drafts mining writes, which must be placed or thrown away before a sitting is recorded.</summary>
    public static IReadOnlyList<string> Drafts { get; } =
    [
        "constraints.draft.md",
        "decisions.draft.md",
        "techstack.draft.md",
        "areas.draft.md",
    ];

    /// <summary>
    /// Reads the proposal file's <c>## Watch</c>, <c>## Ignored</c> and <c>## Declined</c> sections: <c>-
    /// &lt;concern&gt; — &lt;cycle&gt;, &lt;cycle&gt;</c> for the first two and <c>- &lt;suggestion id&gt;:
    /// &lt;reason&gt;</c> for the last.
    /// </summary>
    /// <param name="markdown">The proposal file.</param>
    /// <returns>The concerns watched, the suggestions declined, every accepted heading, and the concerns ignored.</returns>
    public static (IReadOnlyList<Watched> Watch, IReadOnlyList<MinedDecline> Declined, IReadOnlyList<string> Accepted, IReadOnlyList<Watched> Ignored) Read(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        IReadOnlyDictionary<string, IReadOnlyList<string>> sections = ArtifactSections.Read(markdown);
        List<MinedDecline> declined = [];

        foreach (string line in Bullets(sections, "Declined"))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);

            if (colon > 0 && line[(colon + 1)..].Trim().Length > 0)
            {
                declined.Add(new MinedDecline(line[..colon].Trim().Trim('`'), line[(colon + 1)..].Trim()));
            }
        }

        List<string> accepted = [.. markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Where(line => line.StartsWith("### [x]", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[7..].Trim())];

        return (Concerns(sections, "Watch"), declined, accepted, Concerns(sections, "Ignored"));
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

    /// <summary>
    /// Whether a cycle is finished, which is when mining may read it as settled: its verify passed, or a person
    /// abandoned it.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>True when it is.</returns>
    public static async Task<bool> IsFinished(Cycle cycle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        if (cycle.IsAbandoned)
        {
            return true;
        }

        string verify = cycle.PathOf(ArtifactKind.Verify);

        if (!File.Exists(verify))
        {
            return false;
        }

        Result<FrontmatterDocument> split = Frontmatter.Read(await File.ReadAllTextAsync(verify, cancellationToken).ConfigureAwait(false), verify);

        return split.IsSuccess
            && string.Equals(Frontmatter.Key(split.Value.Yaml, "outcome"), "pass", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> Bullets(IReadOnlyDictionary<string, IReadOnlyList<string>> sections, string name) =>
        sections.TryGetValue(name, out IReadOnlyList<string>? lines)
            ? lines.Select(line => line.Trim()).Where(line => line.StartsWith("- ", StringComparison.Ordinal)).Select(line => line[2..].Trim())
            : [];

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository, TimeProvider clock) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!File.Exists(request.From))
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"There is no proposal file at {request.From}. '/sdlc:mine' writes one, and the person marks it."));
            }

            string sdlc = Path.Combine(solution.Path, Cycle.SdlcDirectoryName);
            List<string> pending = [.. Drafts.Where(draft => File.Exists(Path.Combine(sdlc, draft)))];

            // A sitting is recorded once what it decided is in the files: a draft still beside its file is a
            // decision the person has not made, and recording over it would mark cycles mined that were not.
            if (pending.Count > 0)
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"{string.Join(", ", pending)} {(pending.Count == 1 ? "is" : "are")} still beside the real file{(pending.Count == 1 ? string.Empty : "s")}. Rename each into place, or delete the ones you decided against, then record the sitting."));
            }

            Result<MinedLedger> ledger = await MinedLedger.Read(solution.Path, cancellationToken).ConfigureAwait(false);

            if (ledger.IsFailed)
            {
                return ledger.ToResult<Response>();
            }

            Result<ProposeMining.Response> report = await mediator
                .Send<ProposeMining.Query, ProposeMining.Response>(new ProposeMining.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (report.IsFailed)
            {
                return report.ToResult<Response>();
            }

            string integration = await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken).ConfigureAwait(false);
            string? at = new[] { "origin/" + integration, integration }.FirstOrDefault(name => repository.RefExists(solution.Path, name));
            DateTimeOffset now = clock.GetUtcNow();

            List<MinedCycle> recorded = [];
            List<CycleId> unmerged = [];
            List<CycleId> unfinished = [];

            foreach (Cycle cycle in CycleIndex.All(solution.Path))
            {
                if (ledger.Value.Find(cycle.Id.ToString()) is not null)
                {
                    continue;
                }

                if (!await IsFinished(cycle, cancellationToken).ConfigureAwait(false))
                {
                    unfinished.Add(cycle.Id);
                    continue;
                }

                string folder = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, cycle.Id.ToString());
                IReadOnlyList<(string Commit, DateTimeOffset When)> commits = at is null ? [] : repository.CommitsTouching(solution.Path, at, folder);

                if (commits.Count == 0)
                {
                    unmerged.Add(cycle.Id);
                    continue;
                }

                recorded.Add(await Mined(cycle, commits[^1].Commit, now, report.Value, cancellationToken).ConfigureAwait(false));
            }

            (IReadOnlyList<Watched> watch, IReadOnlyList<MinedDecline> declined, IReadOnlyList<string> accepted, IReadOnlyList<Watched> ignored) =
                Read(await File.ReadAllTextAsync(request.From, cancellationToken).ConfigureAwait(false));

            MinedLedger written = ledger.Value.With(
                recorded,
                new MiningRun(now, [.. recorded.Select(cycle => cycle.Cycle)], accepted, declined),
                watch,
                ignored);

            await File.WriteAllTextAsync(MinedLedger.PathIn(solution.Path), written.Write(), cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(recorded, unmerged, unfinished, written.Watch.Count, integration, written.Ignored));
        }

        private static async Task<MinedCycle> Mined(Cycle cycle, string merge, DateTimeOffset now, ProposeMining.Response report, CancellationToken cancellationToken)
        {
            string planPath = cycle.PathOf(ArtifactKind.Plan);
            string plan = File.Exists(planPath) ? await File.ReadAllTextAsync(planPath, cancellationToken).ConfigureAwait(false) : string.Empty;
            string patchPath = Path.Combine(cycle.EvidenceDirectory, "diff.patch");
            IReadOnlyList<string> changed = File.Exists(patchPath)
                ? AreaScope.FilesIn(await File.ReadAllTextAsync(patchPath, cancellationToken).ConfigureAwait(false))
                : [];

            ProposeMining.CycleRecord? how = report.Record?.FirstOrDefault(record => record.Cycle == cycle.Id);

            return new MinedCycle(
                cycle.Id.ToString(),
                merge,
                now,
                cycle.IsAbandoned,
                DeclaredFiles.Read(plan),
                changed,
                [.. (report.Rules.Cited ?? [])
                    .Where(recurrence => recurrence.Cycles.Contains(cycle.Id))
                    .Select(recurrence => recurrence.Cited.ToString())
                    .Distinct(StringComparer.Ordinal)],
                [.. (PlanAreas.Read(plan).ValueOrDefault ?? [])
                    .Where(line => line.Kind == PlanAreaKind.Proposed)
                    .Select(line => new MinedArea(line.Name!, line.Files))],
                how?.Rounds.ToDictionary(round => Cycle.NameOf(round.Key), round => round.Value, StringComparer.Ordinal) ?? [],
                how?.Reopens.Count ?? 0,
                [.. how?.Verdicts.Select(verdict => verdict.Outcome ?? "unreadable") ?? []]);
        }
    }
}
