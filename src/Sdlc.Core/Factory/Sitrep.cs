using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Factory;

/// <summary>
/// The morning's page: what is ready to merge and in what order, what needs a person, what was held back,
/// what is waiting and going stale (D-84, D-85, D-86).
/// </summary>
/// <remarks>
/// <para><strong>Every line is a listing, so the binary writes it</strong> (§15.2): the cycles and their
/// verify outcomes, the remote's branches, the claim graph, and git's history. A skill may present it; the
/// factory publishes it as the run's summary.</para>
/// <para><strong>A built cycle is read from its own branch.</strong> Until its pull request merges, its verify
/// lives on <c>cycle/&lt;id&gt;/build</c> or <c>build-unattended</c>, not on the integration branch, so the
/// outcome is read at <c>origin/&lt;branch&gt;</c>. That needs the remote fetched; a branch that is listed and
/// not fetched is reported as such rather than guessed at.</para>
/// <para><strong>What it cannot say, it does not.</strong> Pull-request numbers, review state and whether the
/// integration branch's own build is green are the platform's, and reading them needs its API. Until then the
/// page names branches and the integration branch's head, and says that the build status is read elsewhere.</para>
/// </remarks>
public static class Sitrep
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddSitrepRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>A cycle that has been built and is waiting on its pull request.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Headline">What its intent is about.</param>
    /// <param name="Branch">The branch its build is on.</param>
    /// <param name="Outcome">The verify outcome read at that branch, or null when there is none to read.</param>
    /// <param name="Files">The files its plan declares.</param>
    public sealed record Built(CycleId Id, string? Headline, string Branch, string? Outcome, IReadOnlyList<string> Files);

    /// <summary>Two built cycles whose plans declare the same files, so the second to merge will likely conflict.</summary>
    /// <param name="First">The cycle to merge first.</param>
    /// <param name="Second">The cycle expected to conflict after it.</param>
    /// <param name="Files">The files both declare.</param>
    public sealed record Overlap(CycleId First, CycleId Second, IReadOnlyList<string> Files);

    /// <summary>A queued cycle whose declared files moved on the integration branch after it was approved.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Headline">What its intent is about.</param>
    /// <param name="Days">How long it has waited since its approval reached the integration branch.</param>
    /// <param name="Changed">The declared files that changed since.</param>
    public sealed record Waiting(CycleId Id, string? Headline, int Days, IReadOnlyList<string> Changed);

    /// <summary>Asks for the page.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>The page.</summary>
    /// <param name="Ready">Built and passed, in the order to merge them.</param>
    /// <param name="NeedsPerson">Built and blocked, escalated, or with no verify to read.</param>
    /// <param name="Overlaps">Pairs of built cycles expected to conflict, in merge order.</param>
    /// <param name="Held">Held back for a reason other than being built.</param>
    /// <param name="Queued">Waiting for a build, each with its age and whether it is going stale.</param>
    /// <param name="IntegrationBranch">The integration branch's name.</param>
    /// <param name="IntegrationHead">Its head commit, abbreviated, or null when it does not exist.</param>
    /// <param name="RemoteRead">Whether the remote's branches could be listed.</param>
    public sealed record Response(
        IReadOnlyList<Built> Ready,
        IReadOnlyList<Built> NeedsPerson,
        IReadOnlyList<Overlap> Overlaps,
        IReadOnlyList<ListQueuedCycles.HeldBack> Held,
        IReadOnlyList<Waiting> Queued,
        string IntegrationBranch,
        string? IntegrationHead,
        bool RemoteRead);

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository, TimeProvider time)
        : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);
            string? integrationRef = new[] { "origin/" + integration, integration }.FirstOrDefault(r => repository.RefExists(root, r));

            Result<ListQueuedCycles.Response> queue = await mediator
                .Send<ListQueuedCycles.Query, ListQueuedCycles.Response>(new ListQueuedCycles.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (queue.IsFailed)
            {
                return queue.ToResult<Response>();
            }

            IReadOnlyList<string>? remote = repository.RemoteBranches(root, CycleId.BranchPrefix);
            List<Built> built = [];

            foreach (CycleBranch branch in (remote ?? [])
                .Select(name => CycleBranch.TryParse(name, out CycleBranch? read) ? read : null)
                .OfType<CycleBranch>()
                .Where(b => b.IsBuild)
                .OrderBy(b => b.Id.ToString(), StringComparer.Ordinal))
            {
                built.Add(Read(root, branch));
            }

            List<Built> ready = [.. built.Where(b => string.Equals(b.Outcome, "pass", StringComparison.OrdinalIgnoreCase))];
            List<Built> needs = [.. built.Except(ready)];
            List<Overlap> overlaps = Overlaps([.. ready, .. needs], out List<CycleId> order);
            ready = [.. ready.OrderBy(b => order.IndexOf(b.Id))];

            List<Waiting> waiting = [];

            foreach (ListQueuedCycles.Queued cycle in queue.Value.Cycles)
            {
                waiting.Add(await Age(root, integrationRef, cycle, cancellationToken).ConfigureAwait(false));
            }

            return Result.Ok(new Response(
                ready,
                needs,
                overlaps,
                [.. queue.Value.Held.Where(h => !built.Any(b => b.Id == h.Id))],
                waiting,
                integration,
                integrationRef is null ? null : repository.ShortCommit(root, integrationRef),
                remote is not null));
        }

        private Built Read(string root, CycleBranch branch)
        {
            Cycle cycle = new(branch.Id, root);
            string at = "origin/" + branch;
            string directory = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, branch.Id.ToString());

            string? intent = repository.ReadAt(root, at, directory + "/" + Cycle.FileNameOf(ArtifactKind.Intent));
            string? plan = repository.ReadAt(root, at, directory + "/" + Cycle.FileNameOf(ArtifactKind.Plan));
            string? verify = repository.ReadAt(root, at, directory + "/" + Cycle.FileNameOf(ArtifactKind.Verify));

            return new Built(
                cycle.Id,
                intent is null ? null : ArtifactOutlineReader.Read(intent).Headline,
                branch.ToString(),
                verify is null ? null : Outcome(verify),
                plan is null ? [] : DeclaredFiles.Read(plan));
        }

        private static string? Outcome(string verify)
        {
            Result<FrontmatterDocument> split = Frontmatter.Read(verify, "verify.md");
            return split.IsSuccess ? Frontmatter.Key(split.Value.Yaml, "outcome") : null;
        }

        /// <summary>
        /// Which built cycles will likely conflict, and the order that keeps the conflicts fewest.
        /// </summary>
        /// <remarks>
        /// The claim graph's own comparison — declared files intersected — read as advice rather than a
        /// schedule (D-86). The more contained cycle goes first: fewest overlaps, then fewest files, so the
        /// cycle most likely to need resolving meets the smallest change already merged.
        /// </remarks>
        private static List<Overlap> Overlaps(List<Built> built, out List<CycleId> order)
        {
            List<(Built A, Built B, List<string> Files)> pairs = [];

            for (int first = 0; first < built.Count; first++)
            {
                for (int second = first + 1; second < built.Count; second++)
                {
                    List<string> shared = [.. built[first].Files
                        .Intersect(built[second].Files, StringComparer.OrdinalIgnoreCase)
                        .Order(StringComparer.Ordinal)];

                    if (shared.Count > 0)
                    {
                        pairs.Add((built[first], built[second], shared));
                    }
                }
            }

            order = [.. built
                .OrderBy(b => pairs.Count(p => p.A.Id == b.Id || p.B.Id == b.Id))
                .ThenBy(b => b.Files.Count)
                .ThenBy(b => b.Id.ToString(), StringComparer.Ordinal)
                .Select(b => b.Id)];

            List<CycleId> sequence = order;

            return [.. pairs
                .Select(p => sequence.IndexOf(p.A.Id) <= sequence.IndexOf(p.B.Id)
                    ? new Overlap(p.A.Id, p.B.Id, p.Files)
                    : new Overlap(p.B.Id, p.A.Id, p.Files))
                .OrderBy(o => sequence.IndexOf(o.First))];
        }

        /// <summary>
        /// How long a queued cycle has waited, and which of its declared files moved since its approval.
        /// </summary>
        /// <remarks>
        /// No threshold: a cycle is going stale when a file its plan declares changed on the integration branch
        /// after the approval reached it, which is the precise signal; the age is shown beside it, and nothing
        /// warns on age alone.
        /// </remarks>
        private async Task<Waiting> Age(string root, string? integrationRef, ListQueuedCycles.Queued cycle, CancellationToken cancellationToken)
        {
            Cycle located = new(cycle.Id, root);
            string approval = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, cycle.Id.ToString(), "approval.md");

            if (integrationRef is null || repository.LastCommitTouching(root, integrationRef, approval) is not { } admitted)
            {
                return new Waiting(cycle.Id, cycle.Headline, 0, []);
            }

            IReadOnlyList<string> declared = File.Exists(located.PathOf(ArtifactKind.Plan))
                ? DeclaredFiles.Read(await File.ReadAllTextAsync(located.PathOf(ArtifactKind.Plan), cancellationToken).ConfigureAwait(false))
                : [];

            IReadOnlyList<string> moved = repository.CommittedChanges(root, admitted.Commit, integrationRef) ?? [];

            int days = Math.Max(0, (int)(time.GetUtcNow() - admitted.When).TotalDays);

            return new Waiting(
                cycle.Id,
                cycle.Headline,
                days,
                [.. declared.Intersect(moved, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)]);
        }
    }
}
