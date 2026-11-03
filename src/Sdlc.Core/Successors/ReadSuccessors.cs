using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Successors;

/// <summary>Where a successor stands, worked out from the record (D-125).</summary>
public enum SuccessorState
{
    /// <summary>Something it succeeds has not merged with a passing verify yet.</summary>
    Waiting,

    /// <summary>Everything it succeeds is on the integration branch with a passing verify.</summary>
    Ready,

    /// <summary>It waits on an abandoned cycle, or on a successor that does, so it never comes ready alone.</summary>
    Stuck,

    /// <summary>It names a cycle or a slug that is nowhere.</summary>
    Unresolved,
}

/// <summary>Where one thing a successor waits for stands.</summary>
public enum WaitState
{
    /// <summary>A cycle on the integration branch with a passing verify.</summary>
    Merged,

    /// <summary>A cycle that exists and has not merged with a passing verify.</summary>
    Open,

    /// <summary>A cycle that holds an abandonment.</summary>
    Abandoned,

    /// <summary>A successor that has not become a cycle yet.</summary>
    NotStarted,

    /// <summary>Nothing by that id or slug exists.</summary>
    Missing,
}

/// <summary>
/// Reads every successor a solution holds, and works out where each stands (D-125).
/// </summary>
/// <remarks>
/// <para><strong>Worked out on every read, never stored</strong> — D-115's reasoning. A readiness written down
/// when a predecessor merged would be a second copy of what the integration branch already says, and the step
/// that writes it is one somebody forgets.</para>
/// <para><strong>How a reference resolves.</strong> A cycle id names that cycle: one whose folder is here, whose
/// intent is on the integration branch, or whose plan branch exists. A <c>successor:&lt;slug&gt;</c> names the cycle
/// whose intent records <c>lead: successor:&lt;slug&gt;</c> once one does, and the waiting file until then — so nobody
/// edits the files that named it when it starts.</para>
/// <para><strong>Merged means on the integration branch with a passing verify</strong>, read at
/// <c>origin/&lt;integration&gt;</c>, else the local branch, else — a solution with no integration branch — the
/// working copy. A plan admitted and not built is not merged: its code does not exist yet.</para>
/// </remarks>
public static class ReadSuccessors
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddReadSuccessorsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks where every successor stands.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>One thing a successor waits for, and where it stands.</summary>
    /// <param name="Reference">As <c>succeeds:</c> wrote it.</param>
    /// <param name="Cycle">The cycle it resolves to, when it resolves to one.</param>
    /// <param name="State">Where that stands.</param>
    public sealed record Wait(SuccessorReference Reference, CycleId? Cycle, WaitState State);

    /// <summary>A successor, and where it stands.</summary>
    /// <param name="Successor">The successor.</param>
    /// <param name="State">Where it stands.</param>
    /// <param name="Waits">Each thing it waits for, in the order written.</param>
    public sealed record Standing(Successor Successor, SuccessorState State, IReadOnlyList<Wait> Waits);

    /// <summary>A successor's file that could not be read.</summary>
    /// <param name="Slug">The slug its file is named for.</param>
    /// <param name="Problems">Everything wrong with it.</param>
    public sealed record Malformed(string Slug, IReadOnlyList<string> Problems);

    /// <summary>Every successor, and what is wrong with the set.</summary>
    /// <param name="Successors">Each successor that could be read, by slug.</param>
    /// <param name="Malformed">Each file that could not be.</param>
    /// <param name="Loops">Each loop through <c>succeeds:</c>, as the slugs around it, the first repeated last.</param>
    /// <param name="ReadAt">Where merged was read: a ref, or null for the working copy.</param>
    public sealed record Response(
        IReadOnlyList<Standing> Successors,
        IReadOnlyList<Malformed> Malformed,
        IReadOnlyList<IReadOnlyList<string>> Loops,
        string? ReadAt)
    {
        /// <summary>Whether every file is well formed, every reference resolves, and nothing loops.</summary>
        public bool Sound =>
            Malformed.Count == 0 && Loops.Count == 0 && Successors.All(s => s.State != SuccessorState.Unresolved);

        /// <summary>The successor by that slug, or null.</summary>
        /// <param name="slug">The slug.</param>
        /// <returns>It, or null.</returns>
        public Standing? Find(string slug) =>
            Successors.FirstOrDefault(s => string.Equals(s.Successor.Slug, slug, StringComparison.Ordinal));
    }

    /// <summary>
    /// Why a successor cannot be started now, or null when it is ready.
    /// </summary>
    /// <remarks>
    /// One sentence for every place that starts one — <c>sdlc lead show</c> and <c>sdlc successor start</c> —
    /// so the two cannot disagree about what ready means.
    /// </remarks>
    /// <param name="read">Where every successor stands.</param>
    /// <param name="slug">The one asked about.</param>
    /// <returns>The refusal, or null.</returns>
    public static string? WhyNotReady(Response read, string slug)
    {
        ArgumentNullException.ThrowIfNull(read);

        if (read.Malformed.FirstOrDefault(m => string.Equals(m.Slug, slug, StringComparison.Ordinal)) is { } broken)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"successor:{slug} cannot be read: {string.Join(" ", broken.Problems)} 'sdlc successor check' lists every problem.");
        }

        if (read.Find(slug) is not { } standing)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"There is no successor:{slug} in {Successor.RelativeDirectory}/ here. 'sdlc successors' lists the ones there are; one already started is a cycle whose intent records lead: successor:{slug}.");
        }

        if (read.Loops.FirstOrDefault(loop => loop.Contains(slug, StringComparer.Ordinal)) is { } loop)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"successor:{slug} waits in a loop -- {string.Join(" -> ", loop)} -- so none of them can ever come ready. Correct one file's succeeds: so the loop opens.");
        }

        string waits = string.Join(", ", standing.Waits.Select(Describe));

        return standing.State switch
        {
            SuccessorState.Ready => null,
            SuccessorState.Waiting => string.Create(
                CultureInfo.InvariantCulture,
                $"successor:{slug} is waiting: {waits}. It starts once each has merged into the integration branch with a passing verify, so its cycle can be built alone."),
            SuccessorState.Stuck => string.Create(
                CultureInfo.InvariantCulture,
                $"successor:{slug} is stuck: {waits}. Something it succeeds was abandoned, so it never comes ready on its own. Correct its succeeds: to name what replaces it, or delete the file."),
            _ => string.Create(
                CultureInfo.InvariantCulture,
                $"successor:{slug} names something that does not exist: {waits}. Correct its succeeds:."),
        };
    }

    /// <summary>One wait, for a person: what it names, what that resolved to, and where it stands.</summary>
    /// <param name="wait">The wait.</param>
    /// <returns>The phrase.</returns>
    public static string Describe(Wait wait)
    {
        ArgumentNullException.ThrowIfNull(wait);

        string named = wait.Reference.Slug is not null && wait.Cycle is { } cycle
            ? wait.Reference + " (cycle " + cycle + ")"
            : wait.Reference.ToString();

        return named + " " + wait.State switch
        {
            WaitState.Merged => "merged",
            WaitState.Open => "not merged yet",
            WaitState.Abandoned => "abandoned",
            WaitState.NotStarted => "not started yet",
            _ => "does not exist",
        };
    }

    private sealed class Handler(SolutionRoot solution, IRepository repository) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            string directory = Successor.DirectoryOf(root);
            Dictionary<string, Successor> read = new(StringComparer.Ordinal);
            List<Malformed> malformed = [];

            if (Directory.Exists(directory))
            {
                foreach (string file in Directory.EnumerateFiles(directory, "*.md").Order(StringComparer.Ordinal))
                {
                    string slug = Path.GetFileNameWithoutExtension(file);

                    // The folder's own index, when somebody gave it one, is not a successor.
                    if (string.Equals(slug, "README", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Result<Successor> parsed = Successor.Parse(
                        slug,
                        await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false));

                    if (parsed.IsFailed)
                    {
                        malformed.Add(new Malformed(slug, [.. parsed.Errors.Select(error => error.Message)]));
                        continue;
                    }

                    read[slug] = parsed.Value;
                }
            }

            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);
            bool repo = repository.IsRepository(root);
            string? at = repo
                ? new[] { "origin/" + integration, integration }.FirstOrDefault(name => repository.RefExists(root, name))
                : null;

            Dictionary<string, CycleId> started = await Started(root, cancellationToken).ConfigureAwait(false);
            Records records = new(root, at, repo ? repository : null);

            List<IReadOnlyList<string>> loops = Loops(read, started);
            HashSet<string> looped = [.. loops.SelectMany(loop => loop)];
            Dictionary<string, Standing> standing = new(StringComparer.Ordinal);

            foreach (string slug in read.Keys.Order(StringComparer.Ordinal))
            {
                await Work(slug, read, started, records, looped, standing, cancellationToken).ConfigureAwait(false);
            }

            return Result.Ok(new Response(
                [.. standing.Values.OrderBy(s => s.Successor.Slug, StringComparer.Ordinal)],
                malformed,
                loops,
                at));
        }

        /// <summary>Every slug an intent here records as its lead: the successors that have become cycles.</summary>
        private static async Task<Dictionary<string, CycleId>> Started(string root, CancellationToken cancellationToken)
        {
            Dictionary<string, CycleId> started = new(StringComparer.Ordinal);

            foreach (Cycle cycle in CycleIndex.All(root))
            {
                string intent = cycle.PathOf(ArtifactKind.Intent);

                if (!File.Exists(intent))
                {
                    continue;
                }

                Result<FrontmatterDocument> split = Frontmatter.Read(
                    await File.ReadAllTextAsync(intent, cancellationToken).ConfigureAwait(false),
                    intent);

                if (split.IsSuccess
                    && Frontmatter.Key(split.Value.Yaml, "lead") is { } lead
                    && lead.StartsWith(SuccessorReference.Prefix, StringComparison.Ordinal))
                {
                    // An abandoned start does not resolve the slug: the piece it stood for was not done. The
                    // earliest live cycle wins, which is the one that started it.
                    if (!cycle.IsAbandoned)
                    {
                        started.TryAdd(lead[SuccessorReference.Prefix.Length..].Trim(), cycle.Id);
                    }
                }
            }

            return started;
        }

        private static async Task<SuccessorState> Work(
            string slug,
            Dictionary<string, Successor> read,
            Dictionary<string, CycleId> started,
            Records records,
            HashSet<string> looped,
            Dictionary<string, Standing> standing,
            CancellationToken cancellationToken)
        {
            if (standing.TryGetValue(slug, out Standing? known))
            {
                return known.State;
            }

            Successor successor = read[slug];
            List<Wait> waits = [];
            bool stuckBehind = false;

            foreach (SuccessorReference reference in successor.Succeeds)
            {
                CycleId? cycle = reference.Cycle
                    ?? (started.TryGetValue(reference.Slug!, out CycleId resolved) ? resolved : null);

                if (cycle is { } id)
                {
                    waits.Add(new Wait(reference, id, await records.StateOf(id, cancellationToken).ConfigureAwait(false)));
                    continue;
                }

                if (!read.ContainsKey(reference.Slug!))
                {
                    waits.Add(new Wait(reference, null, WaitState.Missing));
                    continue;
                }

                waits.Add(new Wait(reference, null, WaitState.NotStarted));

                // A successor behind one that can never come ready cannot either. A loop is refused on its
                // own, so it is not followed here.
                if (!looped.Contains(reference.Slug!)
                    && await Work(reference.Slug!, read, started, records, looped, standing, cancellationToken).ConfigureAwait(false) == SuccessorState.Stuck)
                {
                    stuckBehind = true;
                }
            }

            SuccessorState state =
                waits.Any(w => w.State == WaitState.Missing) ? SuccessorState.Unresolved
                : stuckBehind || waits.Any(w => w.State == WaitState.Abandoned) ? SuccessorState.Stuck
                : looped.Contains(slug) || waits.Any(w => w.State != WaitState.Merged) ? SuccessorState.Waiting
                : SuccessorState.Ready;

            standing[slug] = new Standing(successor, state, waits);
            return state;
        }

        /// <summary>
        /// Every loop through <c>succeeds:</c> among the successors not started yet.
        /// </summary>
        /// <remarks>
        /// A started successor is a cycle, and a cycle waits on nothing, so only waiting files can close a loop.
        /// Each loop is reported once, from its least slug, so the same loop does not read as three.
        /// </remarks>
        private static List<IReadOnlyList<string>> Loops(Dictionary<string, Successor> read, Dictionary<string, CycleId> started)
        {
            var edges = read.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Succeeds
                    .Where(r => r.Slug is not null && !started.ContainsKey(r.Slug) && read.ContainsKey(r.Slug))
                    .Select(r => r.Slug!)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

            List<IReadOnlyList<string>> loops = [];
            HashSet<string> seen = new(StringComparer.Ordinal);

            foreach (string start in edges.Keys.Order(StringComparer.Ordinal))
            {
                Find(start, [start]);
            }

            return loops;

            void Find(string at, List<string> path)
            {
                foreach (string next in edges[at])
                {
                    if (next == path[0])
                    {
                        string key = string.Join(' ', path.Order(StringComparer.Ordinal));

                        // From its least member only, so each loop is named once.
                        if (path.Min(StringComparer.Ordinal) == path[0] && seen.Add(key))
                        {
                            loops.Add([.. path, path[0]]);
                        }

                        continue;
                    }

                    if (!path.Contains(next, StringComparer.Ordinal))
                    {
                        Find(next, [.. path, next]);
                    }
                }
            }
        }
    }

    /// <summary>Where a cycle stands, read from the working copy and the integration branch.</summary>
    private sealed class Records(string root, string? at, IRepository? repository)
    {
        private readonly Dictionary<CycleId, WaitState> _known = [];

        public async Task<WaitState> StateOf(CycleId id, CancellationToken cancellationToken)
        {
            if (_known.TryGetValue(id, out WaitState known))
            {
                return known;
            }

            Cycle cycle = new(id, root);
            string folder = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, id.ToString()) + "/";
            string verifyPath = folder + Cycle.FileNameOf(ArtifactKind.Verify);
            string intentPath = folder + Cycle.FileNameOf(ArtifactKind.Intent);
            string plan = id.BranchName(CycleBranchKind.Plan);

            bool onIntegration = at is not null && repository!.PathExistsAt(root, at, intentPath);
            bool exists = cycle.Exists
                || onIntegration
                || (repository is not null && (repository.RefExists(root, plan) || repository.RefExists(root, "origin/" + plan)));

            WaitState state;

            if (!exists)
            {
                state = WaitState.Missing;
            }
            else if (cycle.IsAbandoned || (at is not null && repository!.PathExistsAt(root, at, folder + "abandoned.md")))
            {
                state = WaitState.Abandoned;
            }
            else
            {
                string? verify = at is not null
                    ? repository!.ReadAt(root, at, verifyPath)
                    : File.Exists(cycle.PathOf(ArtifactKind.Verify))
                        ? await File.ReadAllTextAsync(cycle.PathOf(ArtifactKind.Verify), cancellationToken).ConfigureAwait(false)
                        : null;

                state = verify is not null && Passed(verify) ? WaitState.Merged : WaitState.Open;
            }

            _known[id] = state;
            return state;
        }

        private static bool Passed(string verify)
        {
            Result<FrontmatterDocument> split = Frontmatter.Read(verify, "verify.md");

            return split.IsSuccess
                && string.Equals(Frontmatter.Key(split.Value.Yaml, "outcome"), "pass", StringComparison.OrdinalIgnoreCase);
        }
    }
}
