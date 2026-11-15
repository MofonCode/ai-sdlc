using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Areas;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Harvest;

/// <summary>
/// Closes every completed cycle whose five aspects have all been answered: records what later harvests need in the
/// ledger, and commits the removal of their folders on a branch of its own, for a pull request a person merges
/// (D-152, D-192).
/// </summary>
/// <remarks>
/// <para><strong>The check is the binary's, because a ledger and a file listing decide it.</strong> Every aspect
/// answered for the cycle, and no draft still beside its file. A cycle with an aspect unanswered stays, and the refusal
/// names what it still needs, so nothing a cycle could still give is lost with its folder.</para>
/// <para><strong>Nothing reaches the integration branch unreviewed.</strong> The removal, the ledger and the records
/// the person placed are committed on <c>harvest/&lt;date&gt;</c>; pushing it and opening the pull request is the
/// close skill's, the way a cycle's pull request is the pr skill's. The check admits it as a harvest when it carries
/// only those files (D-152).</para>
/// </remarks>
public static class CloseHarvest
{
    /// <summary>The prefix of the branch a close commits to.</summary>
    public const string BranchPrefix = "harvest/";

    /// <summary>The drafts a harvest writes, which must be placed or thrown away before anything closes.</summary>
    public static IReadOnlyList<string> Drafts { get; } = [.. HarvestAspects.All.Select(HarvestAspects.DraftOf).OfType<string>()];

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddCloseHarvestRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Closes what may close.</summary>
    public sealed record Command : IRequest<Response>;

    /// <summary>What closed, and where it was committed.</summary>
    /// <param name="Closed">The cycles closed, each now a husk whose folder the branch removes.</param>
    /// <param name="Branch">The branch the removal was committed on.</param>
    /// <param name="IntegrationBranch">The branch its pull request goes to.</param>
    /// <param name="Waiting">Completed cycles that stay open, with the aspects each still needs.</param>
    public sealed record Response(
        IReadOnlyList<HarvestedCycle> Closed,
        string Branch,
        string IntegrationBranch,
        IReadOnlyList<HarvestReport.OpenCycle> Waiting);

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository, TimeProvider clock) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string sdlc = Path.Combine(solution.Path, Cycle.SdlcDirectoryName);
            List<string> pending = [.. Drafts.Where(draft => File.Exists(Path.Combine(sdlc, draft)))];

            if (pending.Count > 0)
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"{string.Join(", ", pending)} {(pending.Count == 1 ? "is" : "are")} still beside the real file{(pending.Count == 1 ? string.Empty : "s")}. Rename each into place, or delete the ones you decided against, then close."));
            }

            Result<HarvestReport.Response> report = await mediator
                .Send<HarvestReport.Query, HarvestReport.Response>(new HarvestReport.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (report.IsFailed)
            {
                return report.ToResult<Response>();
            }

            List<HarvestReport.OpenCycle> closing = [.. report.Value.Open.Where(open => open.Missing.Count == 0)];
            List<HarvestReport.OpenCycle> waiting = [.. report.Value.Open.Where(open => open.Missing.Count > 0)];

            if (closing.Count == 0)
            {
                return Result.Fail<Response>(report.Value.Open.Count == 0
                    ? "No completed cycle is open, so nothing can close."
                    : "Nothing can close yet: " + string.Join("; ", waiting.Select(open => string.Create(
                        CultureInfo.InvariantCulture,
                        $"{open.Cycle} still needs {string.Join(", ", open.Missing.Select(HarvestAspects.NameOf))}"))) + ". 'sdlc harvest <aspect>' reads each.");
            }

            string integration = report.Value.IntegrationBranch;

            // The records a person placed are in this working tree, so the branch starts from the integration branch
            // they were placed on; from anywhere else the pull request would carry other work with it.
            if (!string.Equals(repository.CurrentBranch(solution.Path), integration, StringComparison.Ordinal))
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"Close from {integration}, where the cycles merged and the drafts were placed; this checkout is on {repository.CurrentBranch(solution.Path) ?? "no branch"}."));
            }

            DateTimeOffset now = clock.GetUtcNow();
            string branch = BranchPrefix + now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            Result switched = repository.SwitchToNewBranch(solution.Path, branch);

            if (switched.IsFailed)
            {
                return switched;
            }

            List<HarvestedCycle> closed = [];

            foreach (HarvestReport.OpenCycle open in closing)
            {
                closed.Add(await Husk(new Cycle(open.Cycle, solution.Path), integration, now, report.Value, cancellationToken).ConfigureAwait(false));
            }

            HarvestLedger written = report.Value.Ledger.Closing(closed);
            await File.WriteAllTextAsync(HarvestLedger.PathIn(solution.Path), written.Write(), cancellationToken).ConfigureAwait(false);

            foreach (HarvestedCycle husk in closed)
            {
                Result<string> removed = repository.Git(solution.Path, string.Create(
                    CultureInfo.InvariantCulture,
                    $"rm -r -q -- \"{Cycle.SdlcDirectoryName}/{Cycle.CyclesDirectoryName}/{husk.Cycle}\""));

                if (removed.IsFailed)
                {
                    return removed.ToResult<Response>();
                }
            }

            // Only what a harvest may change: the ledger, the records a person placed, and the successors written.
            List<string> paths =
            [
                .. repository.UncommittedChanges(solution.Path)
                    .Select(path => path.Replace('\\', '/'))
                    .Where(path => VerifyHarvest.IsRecord(path) || Successors.Successor.IsSuccessorPath(path)),
            ];

            string ledgerPath = Cycle.SdlcDirectoryName + "/" + HarvestLedger.FileName;

            if (!paths.Contains(ledgerPath, StringComparer.Ordinal))
            {
                paths.Add(ledgerPath);
            }

            Result committed = repository.Commit(solution.Path, paths, string.Create(
                CultureInfo.InvariantCulture,
                $"Close {closed.Count} harvested cycle{(closed.Count == 1 ? string.Empty : "s")}: {string.Join(", ", closed.Select(husk => husk.Cycle))}\n\nEvery aspect was answered for each, and each folder is in git at the merge the ledger names.\n"));

            return committed.IsFailed
                ? committed
                : Result.Ok(new Response(closed, branch, integration, waiting));
        }

        private async Task<HarvestedCycle> Husk(Cycle cycle, string integration, DateTimeOffset now, HarvestReport.Response report, CancellationToken cancellationToken)
        {
            string planPath = cycle.PathOf(ArtifactKind.Plan);
            string plan = File.Exists(planPath) ? await File.ReadAllTextAsync(planPath, cancellationToken).ConfigureAwait(false) : string.Empty;
            string patchPath = Path.Combine(cycle.EvidenceDirectory, "diff.patch");
            IReadOnlyList<string> changed = File.Exists(patchPath)
                ? AreaScope.FilesIn(await File.ReadAllTextAsync(patchPath, cancellationToken).ConfigureAwait(false))
                : [];

            HarvestReport.CycleRecord? how = report.Record.FirstOrDefault(record => record.Cycle == cycle.Id);

            return new HarvestedCycle(
                cycle.Id.ToString(),
                Completion.MergeOf(repository, solution.Path, integration, cycle.Id),
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
                    .Select(line => new HarvestedArea(line.Name!, line.Files))],
                how?.Rounds.ToDictionary(round => Cycle.NameOf(round.Key), round => round.Value, StringComparer.Ordinal) ?? [],
                how?.Reopens.Count ?? 0,
                [.. how?.Verdicts.Select(verdict => verdict.Outcome ?? "unreadable") ?? []]);
        }
    }
}
