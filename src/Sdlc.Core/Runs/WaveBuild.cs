using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Runs;

/// <summary>
/// Builds a wave's cycles, a few at a time, each on a branch and in a worktree of its own taken from the run's latest,
/// and merges each into the run's branch as it passes (D-234, D-236).
/// </summary>
/// <remarks>
/// <para><strong>The binary drives it, not an agent.</strong> Each cycle's build is its own process, the build playbook
/// for that one cycle, opening no pull request; this waits for each, merges what passed under the merge lock, and starts
/// the next ready cycle in the slot it left. An agent driving it would sit in one shell call for as long as the wave
/// takes, which no host allows.</para>
/// <para><strong>Cycles that touch the same file never build at once</strong> (D-236). The later in the wave's order
/// starts only once the earlier has merged or stopped, so it is taken from code that already holds the earlier's change
/// rather than meeting it as a conflict at the end.</para>
/// <para><strong>A cycle that does not pass is left where it is.</strong> Its branch and worktree stay, pushed when the
/// repository has a remote, and the caller decides whether a fix is added or the cycle is taken out.</para>
/// </remarks>
public static class WaveBuild
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddWaveBuildRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Builds a wave's cycles into the run checked out here.</summary>
    /// <param name="Kind">Which size of run.</param>
    /// <param name="Id">The run's id.</param>
    /// <param name="Cycles">The cycles to build, in the wave's order: an earlier one sharing a file goes first.</param>
    /// <param name="Parallel">How many build at once.</param>
    /// <param name="PluginDirectory">This release's skills, which each build's process loads.</param>
    /// <param name="Progress">Told one line as each cycle starts, merges or stops, for the person waiting.</param>
    public sealed record Command(
        RunKind Kind,
        string Id,
        IReadOnlyList<CycleId> Cycles,
        int Parallel,
        string PluginDirectory,
        IProgress<string>? Progress = null) : IRequest<Response>;

    /// <summary>Where one cycle ended.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Merged">Whether it is in the run's branch now.</param>
    /// <param name="Branch">The branch it was built on.</param>
    /// <param name="Detail">What happened, in a sentence a report can carry.</param>
    /// <param name="Said">What its build's process handed back, when it ran one.</param>
    /// <param name="Pushed">Whether a cycle left unmerged has its branch on the remote.</param>
    public sealed record Built(CycleId Id, bool Merged, string Branch, string Detail, string? Said, bool Pushed = false);

    /// <summary>Every cycle's end, in the order they ended.</summary>
    /// <param name="Cycles">Each cycle.</param>
    public sealed record Response(IReadOnlyList<Built> Cycles)
    {
        /// <summary>The cycles that did not reach the run's branch.</summary>
        public IReadOnlyList<Built> Stopped => [.. Cycles.Where(cycle => !cycle.Merged)];
    }

    /// <summary>Which earlier cycles in the order each one waits on, because their plans declare a file it does too.</summary>
    /// <param name="order">The cycles, in the wave's order.</param>
    /// <param name="declared">Each cycle's declared files.</param>
    /// <returns>For each cycle, the earlier ones it waits on.</returns>
    public static IReadOnlyDictionary<CycleId, IReadOnlyList<CycleId>> WaitsOn(
        IReadOnlyList<CycleId> order,
        IReadOnlyDictionary<CycleId, IReadOnlyList<string>> declared)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(declared);

        Dictionary<CycleId, IReadOnlyList<CycleId>> waits = [];

        for (int index = 0; index < order.Count; index++)
        {
            IReadOnlyList<string> mine = declared.GetValueOrDefault(order[index]) ?? [];
            waits[order[index]] =
            [
                .. order.Take(index).Where(earlier => (declared.GetValueOrDefault(earlier) ?? []).Intersect(mine, StringComparer.OrdinalIgnoreCase).Any()),
            ];
        }

        return waits;
    }

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            string runBranch = RunBranch.NameOf(request.Kind, request.Id);
            Dictionary<CycleId, IReadOnlyList<string>> declared = [];

            foreach (CycleId id in request.Cycles)
            {
                string plan = new Cycle(id, root).PathOf(ArtifactKind.Plan);
                declared[id] = File.Exists(plan)
                    ? [.. DeclaredFiles.Read(await File.ReadAllTextAsync(plan, cancellationToken).ConfigureAwait(false)).Where(path => !DeclaredFiles.IsFrameworkOwned(path))]
                    : [];
            }

            IReadOnlyDictionary<CycleId, IReadOnlyList<CycleId>> waits = WaitsOn(request.Cycles, declared);
            List<CycleId> pending = [.. request.Cycles];
            HashSet<CycleId> ended = [];
            Dictionary<Task<Result<(CycleId Id, string Branch, string? Said, bool Passed)>>, CycleId> running = [];
            List<Built> results = [];
            int slots = Math.Max(1, request.Parallel);

            while (pending.Count > 0 || running.Count > 0)
            {
                foreach (CycleId next in pending.Where(id => waits[id].All(ended.Contains)).ToList())
                {
                    if (running.Count >= slots)
                    {
                        break;
                    }

                    pending.Remove(next);
                    request.Progress?.Report(string.Create(CultureInfo.InvariantCulture, $"Building cycle {next}."));
                    running[BuildOne(root, runBranch, next, request.PluginDirectory, cancellationToken)] = next;
                }

                if (running.Count == 0)
                {
                    // Only an earlier cycle is ever waited on, so this is unreachable; guarded rather than looped.
                    break;
                }

                Task<Result<(CycleId Id, string Branch, string? Said, bool Passed)>> done = await Task.WhenAny(running.Keys).ConfigureAwait(false);
                CycleId finished = running[done];
                running.Remove(done);
                ended.Add(finished);

                Result<(CycleId Id, string Branch, string? Said, bool Passed)> built = await done.ConfigureAwait(false);
                Built result = built.IsFailed
                    ? new Built(finished, false, finished.BranchName(CycleBranchKind.BuildUnattended), string.Join("; ", built.Errors.Select(error => error.Message)), null)
                    : await Settle(root, request, built.Value, cancellationToken).ConfigureAwait(false);

                results.Add(result);
                request.Progress?.Report(result.Detail);
            }

            return Result.Ok(new Response(results));
        }

        /// <summary>Merges a cycle that passed, or leaves one that did not on its pushed branch.</summary>
        private async Task<Built> Settle(
            string root,
            Command request,
            (CycleId Id, string Branch, string? Said, bool Passed) built,
            CancellationToken cancellationToken)
        {
            if (!built.Passed)
            {
                bool pushed = await RunGit.Push(root, built.Branch, cancellationToken).ConfigureAwait(false);
                return new Built(
                    built.Id,
                    false,
                    built.Branch,
                    string.Create(CultureInfo.InvariantCulture, $"Cycle {built.Id} did not pass its verification, so it was not merged; its work is on {built.Branch}{(pushed ? ", pushed" : string.Empty)}."),
                    built.Said,
                    pushed);
            }

            Result<MergeIntoRun.Response> merged = await mediator
                .Send<MergeIntoRun.Command, MergeIntoRun.Response>(
                    new MergeIntoRun.Command(request.Kind, request.Id, built.Branch, "cycle " + built.Id),
                    cancellationToken)
                .ConfigureAwait(false);

            if (merged.IsSuccess && merged.Value.Outcome == MergeIntoRun.Outcome.Merged)
            {
                Remove(root, built.Id);
                return new Built(built.Id, true, built.Branch, merged.Value.Detail, built.Said);
            }

            bool kept = await RunGit.Push(root, built.Branch, cancellationToken).ConfigureAwait(false);
            string why = merged.IsSuccess ? merged.Value.Detail : string.Join("; ", merged.Errors.Select(error => error.Message));

            return new Built(
                built.Id,
                false,
                built.Branch,
                string.Create(CultureInfo.InvariantCulture, $"{why} Its work is on {built.Branch}{(kept ? ", pushed" : string.Empty)}."),
                built.Said,
                kept);
        }

        /// <summary>Opens a cycle's worktree from the run's latest, runs its build, and reads whether it passed.</summary>
        private async Task<Result<(CycleId Id, string Branch, string? Said, bool Passed)>> BuildOne(
            string root,
            string runBranch,
            CycleId id,
            string plugin,
            CancellationToken cancellationToken)
        {
            // Yielded first, so the caller starts every slot before any build's setup runs.
            await Task.Yield();

            string branch = id.BranchName(CycleBranchKind.BuildUnattended);
            string verify = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, id.ToString(), Cycle.FileNameOf(ArtifactKind.Verify));

            // A cycle that passed on its branch before the run stopped is merged as it stands, never built again.
            if (repository.RefExists(root, branch) && Passed(root, branch, verify))
            {
                return Result.Ok<(CycleId, string, string?, bool)>((id, branch, null, true));
            }

            Result<string> place = await Open(root, runBranch, branch, id, cancellationToken).ConfigureAwait(false);

            if (place.IsFailed)
            {
                return place.ToResult<(CycleId, string, string?, bool)>();
            }

            Result<AgentExit> exit = await mediator
                .Send<BuildRun.Launch, AgentExit>(new BuildRun.Launch(id, place.Value, plugin, InRun: true), cancellationToken)
                .ConfigureAwait(false);

            if (exit.IsFailed)
            {
                return exit.ToResult<(CycleId, string, string?, bool)>();
            }

            // A command the build was refused is named with how to grant it, so a refusal never reads as a failing test (D-224).
            string? said = exit.Value.Refused.Count == 0
                ? exit.Value.Said ?? exit.Value.Problem
                : string.Join("\n\n", new[] { exit.Value.Said ?? exit.Value.Problem, BuildRun.RenderRefused(exit.Value.Refused) }.OfType<string>());
            return Result.Ok<(CycleId, string, string?, bool)>((id, branch, said, Passed(root, branch, verify)));
        }

        /// <summary>The cycle's worktree, opened from the run's latest, or found and brought up to it.</summary>
        private async Task<Result<string>> Open(string root, string runBranch, string branch, CycleId id, CancellationToken cancellationToken)
        {
            string place = Path.GetFullPath(Path.Combine(root, BuildRun.WorktreePath(root, id)));

            if (repository.Worktrees(root).FirstOrDefault(tree => string.Equals(tree.Branch, branch, StringComparison.Ordinal)) is { Path: { } found })
            {
                place = found;
            }
            else
            {
                (int opened, string said) = repository.RefExists(root, branch)
                    ? await LongProcess.Git(root, cancellationToken, "worktree", "add", place, branch).ConfigureAwait(false)
                    : await LongProcess.Git(root, cancellationToken, "worktree", "add", "--no-track", "-b", branch, place, runBranch).ConfigureAwait(false);

                if (opened != 0)
                {
                    return Result.Fail<string>(string.Create(CultureInfo.InvariantCulture, $"Cycle {id}'s worktree could not be opened at {place}: {said.Trim()}"));
                }
            }

            // A branch built before -- a cycle held by a fix, or a build the run stopped partway through -- takes the
            // run's latest first, so what it builds on includes everything merged since.
            (int caught, string caughtSaid) = await LongProcess.Git(place, cancellationToken, "merge", "--no-edit", runBranch).ConfigureAwait(false);

            if (caught != 0)
            {
                await LongProcess.Git(place, cancellationToken, "merge", "--abort").ConfigureAwait(false);
                return Result.Fail<string>(string.Create(CultureInfo.InvariantCulture, $"Cycle {id}'s branch cannot take {runBranch}'s latest: {caughtSaid.Trim()}"));
            }

            return Result.Ok(place);
        }

        private bool Passed(string root, string branch, string verify) =>
            repository.ReadAt(root, branch, verify) is { } text
            && Frontmatter.Read(text, verify) is { IsSuccess: true } split
            && string.Equals(Frontmatter.Key(split.Value.Yaml, "outcome")?.Trim(), "pass", StringComparison.OrdinalIgnoreCase);

        /// <summary>Takes a merged cycle's worktree away; its branch stays, merged.</summary>
        private void Remove(string root, CycleId id)
        {
            string branch = id.BranchName(CycleBranchKind.BuildUnattended);

            if (repository.Worktrees(root).FirstOrDefault(tree => string.Equals(tree.Branch, branch, StringComparison.Ordinal)) is { Path: { } place })
            {
                repository.Git(root, "worktree remove --force \"" + place + "\"");
            }
        }
    }
}
