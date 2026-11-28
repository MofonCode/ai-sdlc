using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Runs;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Chain;

/// <summary>
/// The pull-request check on a run's branch, <c>wave/&lt;id&gt;</c> or <c>series/&lt;id&gt;</c>: several cycles in one
/// pull request, built one after another on the branch or side by side and merged into it.
/// </summary>
/// <remarks>
/// <para><strong>Each cycle is checked as it would be alone.</strong> Every cycle whose folder the pull request
/// changes gets the whole chain with everything the pull request changes, so the checks that read the diff — when
/// the records it carries were committed — read all of it. A cycle that carries only its paperwork is read from
/// its own folder: an abandoned one is admitted as the record of what was stopped, and a plan nobody built yet gets
/// the admission check a slicing would give it.</para>
/// <para><strong>Then the one thing only a run can break.</strong> A later cycle can fail a test an earlier one
/// named, and nothing verifies the earlier one again. So every test named in every built cycle's plan must have
/// passed in the run that holds them all: the run's last merge evidence when its merge commit holds every cycle's
/// recorded head (D-234), and otherwise the last cycle's run — the cycle whose recorded head every other built cycle's
/// descends into.</para>
/// <para><strong>And every change outside <c>.sdlc/</c> belongs to a verified cycle.</strong> A cycle carrying only its
/// paperwork is read from its own folder, so without this a run could carry code no cycle's evidence records — a cycle
/// committed and never recorded, or an abandoned one whose code was never taken out — and every cycle would still pass.
/// Every path the pull request changes outside <c>.sdlc/</c> must be in some built cycle's recorded change, and nothing
/// there may change after the last cycle's head, or the last merge, except what merging the integration branch brought.</para>
/// <para><strong>Still chosen by the diff</strong> (D-87): the branch's name says only that this is a run, so a
/// pull request that changes no cycle's folder is refused rather than waved through.</para>
/// </remarks>
public static class VerifyRun
{
    private const string NamedTestsCheck = "every named test passed at the end of the run";

    private const string AccountedCheck = "every change outside .sdlc/ belongs to a verified cycle";

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddVerifyRunRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks whether a run's pull request may merge.</summary>
    /// <param name="Kind">Which size of run.</param>
    /// <param name="Id">The run's id.</param>
    /// <param name="Changed">What the pull request changes, relative to the root.</param>
    /// <param name="Divergence">The commit the run's branch left its target at, when known.</param>
    public sealed record Query(RunKind Kind, string Id, IReadOnlyList<string> Changed, string? Divergence = null)
        : IRequest<Response>;

    /// <summary>What the check found.</summary>
    /// <param name="Kind">Which size of run.</param>
    /// <param name="Id">The run's id.</param>
    /// <param name="Cycles">Each cycle's chain, in id order.</param>
    /// <param name="Checks">What only a run is checked for.</param>
    public sealed record Response(
        RunKind Kind,
        string Id,
        IReadOnlyList<VerifyChain.Response> Cycles,
        IReadOnlyList<VerifyChain.ChainCheck> Checks)
    {
        /// <summary>The run's branch.</summary>
        public string Branch => RunBranch.NameOf(Kind, Id);

        /// <summary>Whether every cycle and every run check holds.</summary>
        public bool Holds => Cycles.All(c => c.Holds) && Checks.All(c => c.Passed);
    }

    /// <summary>Renders the check for a pipeline log: the run's own checks, then each cycle's chain.</summary>
    /// <param name="verdict">What it found.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        string noun = verdict.Kind == RunKind.Wave ? "wave" : "series";
        StringBuilder output = new();
        output.AppendLine();
        output.AppendLine("  " + noun + " - " + verdict.Branch);
        output.AppendLine();

        foreach (VerifyChain.ChainCheck check in verdict.Checks)
        {
            output.AppendLine((check.Passed ? "    [ok]  " : "    [no]  ") + check.Name);

            foreach (string line in check.Detail.ReplaceLineEndings("\n").Split('\n'))
            {
                output.AppendLine("          " + line);
            }
        }

        foreach (VerifyChain.Response cycle in verdict.Cycles)
        {
            output.Append(ChainRenderer.Render(cycle));
        }

        output.AppendLine();
        output.AppendLine(verdict.Holds
            ? string.Create(CultureInfo.InvariantCulture, $"  The {noun} holds: every cycle in it may merge.")
            : string.Create(CultureInfo.InvariantCulture, $"  The {noun} does not hold, so none of its cycles merges."));

        return output.ToString();
    }

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            List<VerifyChain.ChainCheck> checks = [];
            IReadOnlyList<CycleId> ids = VerifyChain.ChangedCycles(request.Changed);

            if (ids.Count == 0)
            {
                checks.Add(new VerifyChain.ChainCheck(
                    "this pull request carries a cycle",
                    false,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{request.Changed.Count} changed, and none of it is under .sdlc/cycles/. A run's pull request carries the cycles it built; with none there is no chain to check.")));

                return Result.Ok(new Response(request.Kind, request.Id, [], checks));
            }

            List<VerifyChain.Response> cycles = [];
            List<Cycle> built = [];

            foreach (CycleId id in ids)
            {
                string folder = Cycle.SdlcDirectoryName + "/" + Cycle.CyclesDirectoryName + "/" + id + "/";
                List<string> own = [.. request.Changed.Where(path => path.Replace('\\', '/').TrimStart('/').StartsWith(folder, StringComparison.Ordinal))];

                // Paperwork alone -- an abandonment, or a plan the run never built -- is read from the cycle's own
                // folder, as it would be alone. Anything built gets the whole pull request.
                bool paperwork = VerifyChain.AdmitsAsPlan(id, own);

                Result<VerifyChain.Response> chain = await mediator
                    .Send<VerifyChain.Query, VerifyChain.Response>(
                        new VerifyChain.Query(id, paperwork ? own : request.Changed, request.Divergence),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (chain.IsFailed)
                {
                    return chain.ToResult<Response>();
                }

                cycles.Add(chain.Value);

                if (!chain.Value.Admission && !chain.Value.Cycle.IsAbandoned)
                {
                    built.Add(chain.Value.Cycle);
                }
            }

            MergeRun? merge = await LastMerge(request.Kind, request.Id, built, cancellationToken).ConfigureAwait(false);
            checks.Add(await NamedTestsAtTheEnd(built, merge, cancellationToken).ConfigureAwait(false));
            checks.Add(await Accounted(request.Changed, built, request.Divergence, merge, cancellationToken).ConfigureAwait(false));

            return Result.Ok(new Response(request.Kind, request.Id, cycles, checks));
        }

        /// <summary>
        /// The run's last merge evidence, when its merge commit holds every built cycle's recorded head (D-234): the run on
        /// the merged result, which speaks for every cycle merged into it. Null for a run built one after another on its
        /// branch, whose last cycle's run is the one that counts, and for evidence a later change left behind.
        /// </summary>
        private async Task<MergeRun?> LastMerge(RunKind kind, string id, List<Cycle> built, CancellationToken cancellationToken)
        {
            if (built.Count == 0 || (await MergeEvidence.Read(solution.Path, kind, id, cancellationToken).ConfigureAwait(false)).ValueOrDefault is not { } merge)
            {
                return null;
            }

            foreach (Cycle cycle in built)
            {
                if (await EvidenceDiff.Commit(cycle, RecordEvidence.HeadFileName, cancellationToken).ConfigureAwait(false) is not { } head
                    || !repository.IsAncestor(solution.Path, head, merge.Head))
                {
                    return null;
                }
            }

            return merge;
        }

        /// <summary>
        /// Every path the pull request changes outside <c>.sdlc/</c> is in a built cycle's recorded change, and none
        /// changed after the last cycle's head but what merging the integration branch brought.
        /// </summary>
        private async Task<VerifyChain.ChainCheck> Accounted(
            IReadOnlyList<string> changed,
            List<Cycle> built,
            string? divergence,
            MergeRun? merge,
            CancellationToken cancellationToken)
        {
            string prefix = Cycle.SdlcDirectoryName + "/";
            List<string> outside =
            [
                .. changed
                    .Select(Normalised)
                    .Where(path => !path.StartsWith(prefix, StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ];

            if (outside.Count == 0)
            {
                return new VerifyChain.ChainCheck(AccountedCheck, true, "nothing outside .sdlc/ changed.");
            }

            if (built.Count == 0)
            {
                return new VerifyChain.ChainCheck(
                    AccountedCheck,
                    false,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Files(outside)} changed, and no cycle in this run was built, so no cycle's evidence accounts for {(outside.Count == 1 ? "it" : "them")}. Record the evidence of the cycle that built {(outside.Count == 1 ? "it" : "them")}, or revert the code of a cycle that was left unbuilt or abandoned."));
            }

            HashSet<string> covered = new(StringComparer.Ordinal);
            List<string> unrecorded = [];
            List<string> heads = [];

            foreach (Cycle cycle in built)
            {
                IReadOnlyList<string>? recorded = await EvidenceDiff.ChangedFiles(cycle, repository, solution.Path, cancellationToken).ConfigureAwait(false);

                if (recorded is null || await EvidenceDiff.Commit(cycle, RecordEvidence.HeadFileName, cancellationToken).ConfigureAwait(false) is not { } head)
                {
                    unrecorded.Add(cycle.Id.ToString());
                    continue;
                }

                covered.UnionWith(recorded.Select(Normalised));
                heads.Add(head);
            }

            List<string> problems = [];
            List<string> loose = [.. outside.Where(path => !covered.Contains(path))];

            if (unrecorded.Count > 0)
            {
                problems.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"git cannot rebuild the recorded change of {string.Join(", ", unrecorded)} from {(unrecorded.Count == 1 ? "its" : "their")} {RecordEvidence.BaseFileName} and {RecordEvidence.HeadFileName}, so what {(unrecorded.Count == 1 ? "it" : "they")} built cannot be told. Record {(unrecorded.Count == 1 ? "its" : "their")} evidence again on this branch."));
            }

            if (loose.Count > 0)
            {
                problems.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Files(loose)} changed, and no built cycle's recorded change includes {(loose.Count == 1 ? "it" : "them")}. If a cycle built {(loose.Count == 1 ? "it" : "them")}, record that cycle's evidence; if {(loose.Count == 1 ? "it belongs" : "they belong")} to a cycle left unbuilt or abandoned, revert that code."));
            }

            // After the last cycle's head -- or the last merge, which holds every head -- nothing may move but what a merge of
            // the integration branch brought.
            if ((merge?.Head ?? heads.FirstOrDefault(candidate => heads.All(other => repository.IsAncestor(solution.Path, other, candidate)))) is { } last)
            {
                string from = last;

                if (divergence is not null && !repository.IsAncestor(solution.Path, divergence, last))
                {
                    from = repository.MergeTree(solution.Path, last, divergence) is { } merged ? merged.Tree : last;
                }

                List<string> late =
                [
                    .. (repository.CommittedChanges(solution.Path, from, "HEAD") ?? [])
                        .Select(Normalised)
                        .Where(path => outside.Contains(path, StringComparer.Ordinal))
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal),
                ];

                if (late.Count > 0)
                {
                    problems.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Files(late)} changed after the last cycle's recorded head, {last[..Math.Min(12, last.Length)]}, and not by merging the integration branch, so no cycle's evidence holds that change. Record the last cycle's evidence again on top of it, or revert it."));
                }
            }

            return problems.Count == 0
                ? new VerifyChain.ChainCheck(
                    AccountedCheck,
                    true,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{outside.Count} file{(outside.Count == 1 ? string.Empty : "s")} outside .sdlc/ changed, every one in the recorded change of a built cycle."))
                : new VerifyChain.ChainCheck(AccountedCheck, false, string.Join(Environment.NewLine, problems));
        }

        private static string Normalised(string path) => path.Replace('\\', '/').TrimStart('/');

        /// <summary>A list of paths for a refusal: the first few, and how many more.</summary>
        private static string Files(List<string> paths) =>
            paths.Count <= 8
                ? string.Join(", ", paths)
                : string.Create(CultureInfo.InvariantCulture, $"{string.Join(", ", paths.Take(8))} and {paths.Count - 8} more");

        /// <summary>Every test every built cycle's plan names passed in the last cycle's run.</summary>
        private async Task<VerifyChain.ChainCheck> NamedTestsAtTheEnd(List<Cycle> built, MergeRun? merge, CancellationToken cancellationToken)
        {
            if (built.Count == 0)
            {
                return new VerifyChain.ChainCheck(NamedTestsCheck, true, "no cycle in this run was built, so no test is named.");
            }

            if (merge is not null)
            {
                return await Named(built, merge.Summary, "the run's last merge, " + merge.Head[..Math.Min(12, merge.Head.Length)], cancellationToken).ConfigureAwait(false);
            }

            List<(Cycle Cycle, string Head)> heads = [];

            foreach (Cycle cycle in built)
            {
                if (await EvidenceDiff.Commit(cycle, RecordEvidence.HeadFileName, cancellationToken).ConfigureAwait(false) is not { } head)
                {
                    return new VerifyChain.ChainCheck(
                        NamedTestsCheck,
                        false,
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"cycle {cycle.Id} records no {RecordEvidence.HeadFileName}, so which cycle's run came last cannot be told. Record its evidence again."));
                }

                heads.Add((cycle, head));
            }

            if (heads.FirstOrDefault(candidate => heads.All(other => repository.IsAncestor(solution.Path, other.Head, candidate.Head))) is not { Cycle: { } last })
            {
                return new VerifyChain.ChainCheck(
                    NamedTestsCheck,
                    false,
                    "the cycles' recorded heads are not in one line, so no one cycle's run is the run's last. A run builds its cycles one after another on its branch; record each cycle's evidence on it.");
            }

            string summaryPath = Path.Combine(last.EvidenceDirectory, RecordEvidence.SummaryFileName);

            if (!File.Exists(summaryPath))
            {
                return new VerifyChain.ChainCheck(
                    NamedTestsCheck,
                    false,
                    string.Create(CultureInfo.InvariantCulture, $"the last cycle, {last.Id}, has no {RecordEvidence.SummaryFileName}, so nothing says the run's tests passed at its end."));
            }

            Result<TestSummary> read = TestSummaryReader.Read(
                await File.ReadAllTextAsync(summaryPath, cancellationToken).ConfigureAwait(false),
                summaryPath);

            if (read.IsFailed)
            {
                return new VerifyChain.ChainCheck(NamedTestsCheck, false, string.Join("; ", read.Errors.Select(error => error.Message)));
            }

            return await Named(built, read.Value, "the last cycle's run, " + last.Id, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Every test every built cycle's plan names passed in one run.</summary>
        private static async Task<VerifyChain.ChainCheck> Named(List<Cycle> built, TestSummary run, string which, CancellationToken cancellationToken)
        {
            List<string> failed = [];
            int named = 0;

            foreach (Cycle cycle in built)
            {
                string planPath = cycle.PathOf(ArtifactKind.Plan);

                if (!File.Exists(planPath))
                {
                    continue;
                }

                IReadOnlyDictionary<int, string> proofs = ArtifactSections.NumberedItems(
                    await File.ReadAllTextAsync(planPath, cancellationToken).ConfigureAwait(false),
                    "Proof");

                foreach (string test in proofs.OrderBy(p => p.Key).SelectMany(p => DeclaredFiles.TestsIn(p.Value)).Distinct(StringComparer.Ordinal))
                {
                    named++;

                    if (!run.DidPass(test))
                    {
                        failed.Add(string.Create(CultureInfo.InvariantCulture, $"{test} (cycle {cycle.Id})"));
                    }
                }
            }

            return failed.Count == 0
                ? new VerifyChain.ChainCheck(
                    NamedTestsCheck,
                    true,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{named} test{(named == 1 ? string.Empty : "s")} named across {built.Count} cycle{(built.Count == 1 ? string.Empty : "s")}, every one passing in {which}."))
                : new VerifyChain.ChainCheck(
                    NamedTestsCheck,
                    false,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"in {which}, these did not run and pass: {string.Join(", ", failed)}. A later cycle broke what an earlier one proved; fix it on the run's branch and record the last cycle's evidence again."));
        }
    }
}
