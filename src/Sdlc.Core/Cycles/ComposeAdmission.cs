using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

namespace Sdlc.Core.Cycles;

/// <summary>
/// The one git command that takes a sandbox's approved cycles onto an admission branch (D-144).
/// </summary>
/// <remarks>
/// <para><strong>Composed onto a branch the check already knows.</strong> One approved cycle goes on
/// <c>cycle/&lt;id&gt;/plan</c>; several on <c>slices/&lt;slug&gt;/plan</c>, as a slicing's do (D-125). The pull-request
/// check then admits it as it admits those, so a sandbox adds no new kind of admission.</para>
/// <para><strong>Only what is approved leaves.</strong> A cycle the gate does not let through — still being
/// planned, refused, reopened — stays in the sandbox and goes by a later admission. The composed branch takes each
/// approved cycle's folder from the sandbox's last commit, with every successor the sandbox wrote, onto the
/// integration branch as it stands, in a worktree of its own beside the sandbox so the sandbox stays where it
/// is.</para>
/// <para><strong>It refuses before anything is composed</strong>: outside a sandbox, with no integration branch,
/// with an approved cycle's folder holding uncommitted changes, and with a plan that needs a file only another
/// plan in the sandbox creates — which is a successor, not a sibling.</para>
/// </remarks>
public static class ComposeAdmission
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddComposeAdmissionRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks how to admit what this sandbox has approved.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>How to admit it.</summary>
    /// <param name="Branch">The admission branch it composes.</param>
    /// <param name="Folder">Where that branch is checked out, beside the sandbox.</param>
    /// <param name="Command">The one git command that composes it.</param>
    /// <param name="Admitted">The approved cycles it carries, oldest first.</param>
    /// <param name="Staying">The cycles the sandbox carries that stay behind, each with why.</param>
    /// <param name="Successors">The successor files it carries.</param>
    /// <param name="Comparison">What comparing the plans found: the shared files to say.</param>
    /// <param name="Records">The abandoned cycles it carries as records of what was stopped (D-146).</param>
    public sealed record Response(
        string Branch,
        string Folder,
        string Command,
        IReadOnlyList<CycleId> Admitted,
        IReadOnlyList<(CycleId Id, string Why)> Staying,
        IReadOnlyList<string> Successors,
        ComputeClaimGraph.SlicingComparison Comparison,
        IReadOnlyList<CycleId> Records);

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            string? current = repository.IsRepository(root) ? repository.CurrentBranch(root) : null;

            if (!SandboxBranch.TryParse(current, out CycleId? primary))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"This is {current ?? "no branch"}, not a sandbox. Run it in the sandbox whose cycles you approved; a cycle planned on its own plan branch goes by '/sdlc:pr <id> --plan'."));
            }

            string sandbox = current!;
            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);
            string? integrationRef = new[] { "origin/" + integration, integration }.FirstOrDefault(name => repository.RefExists(root, name));

            if (integrationRef is null)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no {integration} branch, so there is nothing to admit the sandbox's plans to. Each cycle builds from the sandbox: 'sdlc branch <id> build'."));
            }

            Result<ResolveCycle.Response> carried = await mediator
                .Send<ResolveCycle.Query, ResolveCycle.Response>(new ResolveCycle.Query(NextStep.Any), cancellationToken)
                .ConfigureAwait(false);

            if (carried.IsFailed)
            {
                return carried.ToResult<Response>();
            }

            List<CycleId> admitted = [];
            List<(CycleId, string)> staying = [];
            List<ComputeClaimGraph.SlicePlan> plans = [];
            List<string> problems = [];
            IReadOnlyList<string> uncommitted = repository.UncommittedChanges(root);

            foreach (ResolveCycle.Candidate candidate in carried.Value.Carried)
            {
                Result<ReadCycleStatus.Response> status = await mediator
                    .Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(new ReadCycleStatus.Query(candidate.Id), cancellationToken)
                    .ConfigureAwait(false);

                if (status.IsFailed)
                {
                    return status.ToResult<Response>();
                }

                if (status.Value.Gate.Refusal != ApprovalGate.GateRefusal.None)
                {
                    staying.Add((candidate.Id, "the gate does not let it through yet - 'sdlc gate " + candidate.Id + "' says why"));
                    continue;
                }

                string folder = Folder(candidate.Id);

                if (uncommitted.Any(path => path.Replace('\\', '/').StartsWith(folder, StringComparison.Ordinal)))
                {
                    problems.Add(string.Create(CultureInfo.InvariantCulture, $"{candidate.Id} has uncommitted changes in its folder; commit them on {sandbox} first, since what is admitted is what the sandbox committed."));
                    continue;
                }

                if (repository.ReadAt(root, sandbox, folder + Cycle.FileNameOf(ArtifactKind.Plan)) is not { } plan)
                {
                    problems.Add(string.Create(CultureInfo.InvariantCulture, $"{sandbox} holds no committed plan for {candidate.Id}; commit it there first."));
                    continue;
                }

                admitted.Add(candidate.Id);
                plans.Add(ComputeClaimGraph.SlicePlan.Read(candidate.Id, plan));
            }

            // D-146: a cycle stopped here goes too, as the record of what was argued and stopped. Abandoned cycles are
            // never carried as work, so they are found apart: here, not yet on the integration branch.
            List<CycleId> records = [];

            foreach (Cycle stopped in CycleIndex.All(root).Where(c => c.IsAbandoned && !repository.PathExistsAt(root, integrationRef, Folder(c.Id) + "abandoned.md")))
            {
                string folder = Folder(stopped.Id);

                if (uncommitted.Any(path => path.Replace('\\', '/').StartsWith(folder, StringComparison.Ordinal)))
                {
                    problems.Add(string.Create(CultureInfo.InvariantCulture, $"{stopped.Id} was abandoned and its folder has uncommitted changes; commit the abandonment on {sandbox} first."));
                    continue;
                }

                records.Add(stopped.Id);
            }

            if (problems.Count > 0)
            {
                return Result.Fail<Response>("Nothing was composed: " + string.Join(" ", problems));
            }

            if (admitted.Count == 0 && records.Count == 0)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Nothing in {sandbox} is approved yet, so there is nothing to admit. {string.Join(" ", staying.Select(s => s.Item1 + ": " + s.Item2 + "."))}"));
            }

            ComputeClaimGraph.SlicingComparison comparison = ComputeClaimGraph.CompareSlicing(
                plans,
                path => repository.PathExistsAt(root, integrationRef, path));

            if (!comparison.Atomic)
            {
                return Result.Fail<Response>("Nothing was composed: " + Chain.VerifySlicing.Atomic(comparison).Detail);
            }

            CycleId[] leaving = [.. admitted.Concat(records).Order()];
            string branch = leaving.Length == 1
                ? leaving[0].BranchName(CycleBranchKind.Plan)
                : SlicingBranch.NameOf(SandboxBranch.SlugOf(primary.Value));

            if (repository.RefExists(root, branch))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{branch} already exists: an earlier admission from this sandbox, not yet merged or deleted. Merge its pull request, or delete it with 'git branch -D {branch}', and ask again."));
            }

            List<string> successors = [];

            if (repository.MergeBase(root, sandbox, integrationRef) is { } fork
                && repository.CommittedChanges(root, fork, sandbox) is { } changed)
            {
                string directory = Cycle.SdlcDirectoryName + "/" + Successor.DirectoryName + "/";

                successors.AddRange(changed
                    .Select(path => path.Replace('\\', '/'))
                    .Where(path => path.StartsWith(directory, StringComparison.Ordinal) && repository.PathExistsAt(root, sandbox, path))
                    .Order(StringComparer.Ordinal));
            }

            // Beside the sandbox, so composing never moves the sandbox off its branch.
            string place = "../" + primary.Value + ".admit";
            IEnumerable<string> paths = leaving.Select(id => Folder(id).TrimEnd('/')).Concat(successors);

            string command = string.Create(
                CultureInfo.InvariantCulture,
                $"git worktree add -b {branch} {place} {integrationRef} && git -C {place} checkout {sandbox} -- {string.Join(' ', paths)} && git -C {place} commit -m \"Admit {string.Join(", ", leaving)} from {sandbox}\"");

            return Result.Ok(new Response(branch, place, command, admitted, staying, successors, comparison, records));
        }

        private static string Folder(CycleId id) =>
            string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, id.ToString()) + "/";
    }
}
