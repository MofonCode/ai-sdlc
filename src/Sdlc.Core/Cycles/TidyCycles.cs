using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Cycles;

/// <summary>
/// The worktrees and branches the framework opened whose work has reached the integration branch, and the
/// commands that remove them (D-162).
/// </summary>
/// <remarks>
/// <para><strong>Done is read from the integration branch, never from git's merge state.</strong> A squash merge
/// leaves every branch unmerged as far as git can tell, which is why one person removed twelve worktrees and fifteen
/// branches by hand. A plan branch is done once its plan is on the integration branch; any other cycle branch once
/// the verification there passes; a slicing or a sandbox once every cycle it changed is; and every branch of a cycle
/// once its abandonment is there.</para>
/// <para><strong>It reports and never deletes</strong>, as <c>discard</c> does: a worktree is removed without
/// <c>--force</c>, so git refuses one holding uncommitted work, and a branch is deleted with <c>-D</c>, because
/// <c>-d</c> asks git the question a squash merge makes it answer wrongly. Remote branches are left alone.</para>
/// </remarks>
public static class TidyCycles
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddTidyCyclesRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks what can be tidied away.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>One branch, and why it goes or stays.</summary>
    /// <param name="Branch">The local branch.</param>
    /// <param name="Worktree">The worktree it is checked out in, when it is in one of its own.</param>
    /// <param name="Why">For a person.</param>
    public sealed record Entry(string Branch, string? Worktree, string Why);

    /// <summary>What can go, what stays, and the commands that remove what can.</summary>
    /// <param name="Integration">The branch done is read from.</param>
    /// <param name="Removable">The branches whose work is done.</param>
    /// <param name="Kept">The branches the framework opened that stay, each with why.</param>
    /// <param name="Commands">The worktree removals, then one branch deletion; empty when nothing can go.</param>
    public sealed record Response(string Integration, IReadOnlyList<Entry> Removable, IReadOnlyList<Entry> Kept, IReadOnlyList<string> Commands);

    private static readonly string[] _families = [CycleId.BranchPrefix, SlicingBranch.Prefix, SandboxBranch.Prefix];

    private sealed class Handler(SolutionRoot solution, IRepository repository) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;

            if (!repository.IsRepository(root))
            {
                return Result.Fail<Response>("This is not a git repository, so there is nothing to tidy.");
            }

            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);

            if (new[] { "origin/" + integration, integration }.FirstOrDefault(name => repository.RefExists(root, name)) is not { } at)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no {integration} branch, so nothing can be said to have reached it. Tidy reads done from there."));
            }

            IReadOnlyList<(string Path, string? Branch)> worktrees = repository.Worktrees(root);
            string here = Path.GetFullPath(root).TrimEnd('/', '\\');
            Dictionary<string, string> checkedOut = new(StringComparer.Ordinal);

            foreach ((string path, string? branch) in worktrees)
            {
                if (branch is not null)
                {
                    checkedOut.TryAdd(branch, path);
                }
            }

            string? main = worktrees.Count > 0 ? worktrees[0].Path : null;
            List<Entry> removable = [];
            List<Entry> kept = [];

            foreach (string branch in _families
                .SelectMany(prefix => repository.Branches(root, prefix))
                .Where(name => !name.StartsWith("origin/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal))
            {
                if (Done(root, at, branch) is not { } why)
                {
                    continue;
                }

                checkedOut.TryGetValue(branch, out string? worktree);
                bool own = worktree is not null && !Same(worktree, main);

                if (worktree is not null && (Same(worktree, here) || !own))
                {
                    kept.Add(new Entry(branch, worktree, why + string.Create(CultureInfo.InvariantCulture, $", but it is checked out in {worktree}: switch that checkout to another branch first.")));
                    continue;
                }

                removable.Add(new Entry(branch, own ? worktree : null, why));
            }

            List<string> commands =
            [
                .. removable.Where(entry => entry.Worktree is not null).Select(entry => "git worktree remove " + Quoted(entry.Worktree!)),
            ];

            if (removable.Count > 0)
            {
                commands.Add("git branch -D " + string.Join(' ', removable.Select(entry => entry.Branch)));
            }

            return Result.Ok(new Response(integration, removable, kept, commands));
        }

        /// <summary>Why a branch's work is done, or null while it is not or this cannot tell.</summary>
        private string? Done(string root, string at, string branch)
        {
            if (CycleId.TryParseBranch(branch, out CycleId? parsed))
            {
                CycleId id = parsed.Value;
                string folder = Folder(id);

                if (repository.PathExistsAt(root, at, folder + "abandoned.md"))
                {
                    return string.Create(CultureInfo.InvariantCulture, $"{id} is recorded abandoned on {at}");
                }

                if (branch.EndsWith("/" + CycleBranch.NameOf(CycleBranchKind.Plan), StringComparison.Ordinal))
                {
                    return repository.PathExistsAt(root, at, folder + Cycle.FileNameOf(ArtifactKind.Plan))
                        ? string.Create(CultureInfo.InvariantCulture, $"{id}'s plan is on {at}")
                        : null;
                }

                return MergedCycle.Passed(repository.ReadAt(root, at, folder + Cycle.FileNameOf(ArtifactKind.Verify)))
                    ? string.Create(CultureInfo.InvariantCulture, $"{id} passed verification on {at}")
                    : null;
            }

            // A slicing or a sandbox carries several cycles: done once each it changed has reached the integration
            // branch. What it changed is measured from where it left, so later work there is not counted as its.
            if (repository.MergeBase(root, branch, at) is not { } from
                || repository.CommittedChanges(root, from, branch) is not { } changed)
            {
                return null;
            }

            List<CycleId> carried = [.. CyclesIn(changed)];

            return carried.Count > 0
                && carried.All(cycle => repository.PathExistsAt(root, at, Folder(cycle) + Cycle.FileNameOf(ArtifactKind.Plan))
                    || repository.PathExistsAt(root, at, Folder(cycle) + "abandoned.md"))
                ? string.Create(CultureInfo.InvariantCulture, $"every cycle it carries is on {at}: {string.Join(", ", carried)}")
                : null;
        }

        private static IEnumerable<CycleId> CyclesIn(IReadOnlyList<string> paths)
        {
            string prefix = Cycle.SdlcDirectoryName + "/" + Cycle.CyclesDirectoryName + "/";

            return paths
                .Select(path => path.Replace('\\', '/'))
                .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
                .Select(path => path[prefix.Length..].Split('/')[0])
                .Distinct(StringComparer.Ordinal)
                .Select(name => CycleId.TryParse(name, out CycleId? id) ? id : null)
                .Where(id => id is not null)
                .Select(id => id!.Value);
        }

        private static string Folder(CycleId id) =>
            string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, id.ToString()) + "/";

        private static bool Same(string path, string? other) =>
            other is not null
            && string.Equals(
                Path.GetFullPath(path).TrimEnd('/', '\\'),
                Path.GetFullPath(other).TrimEnd('/', '\\'),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        private static string Quoted(string path)
        {
            string forward = path.Replace('\\', '/');
            return forward.Contains(' ', StringComparison.Ordinal) ? "\"" + forward + "\"" : forward;
        }
    }
}
