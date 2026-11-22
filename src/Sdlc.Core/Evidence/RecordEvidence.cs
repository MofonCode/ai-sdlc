using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Evidence;

/// <summary>Records what a build produced - the diff, its base commit, and a normalised test summary.</summary>
public static class RecordEvidence
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddRecordEvidenceRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>What was recorded, and what the comparison noticed.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Written">Each evidence file written, relative to the evidence directory.</param>
    /// <param name="Summary">The normalised test summary, when there was one to write.</param>
    /// <param name="Changed">The paths the diff touched.</param>
    /// <param name="Comparison">Declared against changed, which is a signal rather than a gate.</param>
    /// <param name="TestSources">The results files read.</param>
    /// <param name="Coverage">What the run's coverage said, when a report was given (D-103).</param>
    /// <param name="RetiredVerdict">
    /// Whether a blocked <c>verify.md</c> about the run this one replaces was removed, so the next verify starts
    /// from this run (D-161).
    /// </param>
    /// <param name="Timings">How long the run took and where the time went, when its results files say (D-169).</param>
    public sealed record Response(
        Cycle Cycle,
        IReadOnlyList<string> Written,
        TestSummary? Summary,
        IReadOnlyList<string> Changed,
        FileComparison Comparison,
        IReadOnlyList<string> TestSources,
        CoverageSummary? Coverage = null,
        bool RetiredVerdict = false,
        RunTimings? Timings = null);

    /// <summary>The normalised test summary every later reader uses.</summary>
    public const string SummaryFileName = "tests.json";

    /// <summary>The commit the change is measured from.</summary>
    public const string BaseFileName = "base.sha";

    /// <summary>The commit the build was recorded at, which the patch is measured to (D-124).</summary>
    public const string HeadFileName = "head.sha";

    /// <summary>
    /// The integration branch's commit a run merged in after the base, when it did: the patch is then measured from
    /// the merge of the two, so what the merge brought is not counted as the cycle's (D-206).
    /// </summary>
    public const string MergedFileName = "merged.sha";

    /// <summary>The change itself.</summary>
    public const string DiffFileName = "diff.patch";

    /// <summary>The build's own log, when one was given.</summary>
    public const string BuildLogFileName = "build.log";

    /// <summary>The runner's own results file, kept beside the normalised summary under a fixed name.</summary>
    public const string TrxFileName = "tests.trx";

    /// <summary>
    /// What a runner's results file is called when a run produced more than one.
    /// </summary>
    /// <param name="index">Which source, from one.</param>
    /// <returns>The name to keep it under.</returns>
    public static string TrxFileNameAt(int index) => string.Create(
        CultureInfo.InvariantCulture,
        $"tests.{index}.trx");

    /// <summary>Records what a build produced, as files rather than prose.</summary>
    /// <param name="Id">The cycle the build belongs to.</param>
    /// <param name="Trx">
    /// Where the runner's own test results are: one results file, or a directory holding every results
    /// file a run produced. Null when there are none yet.
    /// </param>
    /// <param name="BuildLog">A build log to keep alongside, or null.</param>
    /// <param name="Coverage">
    /// A cobertura report, or a directory holding every one a run wrote. Null when there is none.
    /// </param>
    /// <param name="RetirePass">
    /// Whether a <c>pass</c> verdict is retired by this run as a blocked one is: true only when a person asked for
    /// a change to a cycle that passed, which is what remediate is doing then (D-185).
    /// </param>
    public sealed record Command(CycleId Id, string? Trx = null, string? BuildLog = null, string? Coverage = null, bool RetirePass = false)
        : IRequest<Response>;

    /// <summary>
    /// Writes a build's evidence, and compares what changed against what the plan declared.
    /// </summary>
    /// <remarks>
    /// <c>verify</c> reads only what this writes. That is the point of recording it: a verdict derived from a
    /// session's recollection of the build is a verdict nobody can check, and <c>CON_22</c> forbids a claim
    /// with no cited evidence.
    /// <para>
    /// Both the runner's own output and the normalised summary are kept. The summary is what every later
    /// reader uses, so the gate never learns what a <c>.trx</c> is; the original is kept so a verify claim can
    /// cite either.
    /// </para>
    /// </remarks>
    /// <param name="solution">The solution being built.</param>
    /// <param name="repository">How to ask git what changed.</param>
    /// <param name="time">Where the current moment comes from.</param>
    private sealed class Handler(
        SolutionRoot solution,
        IRepository repository,
        TimeProvider time) : IRequestHandler<Command, Response>
    {
        /// <summary>
        /// Where the previous cycle built on this branch ended, when one was: the latest head another cycle's evidence
        /// records that this commit descends from and the branch's divergence does not; else null.
        /// </summary>
        /// <remarks>
        /// A run builds several cycles one after another into one branch, so measuring each from where the branch
        /// left the integration branch would credit, and blame, a cycle for every one built before it. A cycle that
        /// already merged recorded a head the divergence descends from, so it is never taken; on a branch carrying
        /// one cycle there is none, and the divergence stands as before. <strong>A cycle built after this one is never
        /// taken either</strong>: remediating an earlier cycle of a run lands on top of the later ones, and measuring
        /// from their head would leave the earlier cycle's own change out of its evidence. Its head descends from this
        /// cycle's previous head, which is how it is told apart.
        /// </remarks>
        private async Task<string?> BuiltOnto(Cycle cycle, string head, string divergence, string? previousHead, CancellationToken cancellationToken)
        {
            List<(CycleId Id, string Head)> before = [];

            foreach (Cycle other in CycleIndex.All(solution.Path).Where(c => c.Id != cycle.Id))
            {
                if (await EvidenceDiff.Commit(other, HeadFileName, cancellationToken).ConfigureAwait(false) is { } recorded
                    && repository.IsAncestor(solution.Path, recorded, head)
                    && !repository.IsAncestor(solution.Path, recorded, divergence)
                    && (previousHead is null || !repository.IsAncestor(solution.Path, previousHead, recorded)))
                {
                    before.Add((other.Id, recorded));
                }
            }

            // The latest is the one every other descends into. On a branch built in a line there always is one; were
            // there not, the newest cycle among those nothing else descends from.
            return before
                .Where(candidate => before.All(o => repository.IsAncestor(solution.Path, o.Head, candidate.Head)))
                .Select(candidate => candidate.Head)
                .FirstOrDefault()
                ?? before
                    .Where(candidate => !before.Any(o => o.Head != candidate.Head && repository.IsAncestor(solution.Path, candidate.Head, o.Head)))
                    .OrderByDescending(candidate => candidate.Id)
                    .Select(candidate => candidate.Head)
                    .FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Result<Response>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);

            if (!cycle.Exists)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has no directory at {cycle.Directory}."));
            }

            string plan = cycle.PathOf(ArtifactKind.Plan);

            if (!File.Exists(plan))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has no plan. There is nothing to compare a change against."));
            }

            // A verdict is an adjudication of one recorded run, and replacing the run underneath it leaves
            // a document whose every citation is about evidence that is no longer there. A trial cycle did
            // exactly this: verify passed, a test was renamed, the evidence was re-recorded, and the
            // verify document was left saying 26 tests where tests.json then held 24 and citing two
            // per-engine results that no longer existed. Nothing downstream could tell -- the chain check
            // reads the plan's named tests against tests.json and both of those had moved together.
            //
            // Refused rather than warned, for the same reason the approval gate refuses an interrogation
            // round judged against a document that has changed since: the stale artifact is the one a
            // reader trusts, because it looks finished.
            //
            // A blocked verdict is the exception, and is retired here rather than refused (D-161). Recording a
            // new run under one is what remediation is: the verdict asked for the fix this run measures, and
            // the next verify replaces it. Refusing it taught every remediation the deletion by refusal, after
            // a run that had already taken most of an hour.
            string verdict = cycle.PathOf(ArtifactKind.Verify);
            bool retire = false;

            if (File.Exists(verdict))
            {
                Result<FrontmatterDocument> split = await Frontmatter.ReadFile(verdict, cancellationToken).ConfigureAwait(false);
                string? outcome = split.IsSuccess ? Frontmatter.Key(split.Value.Yaml, "outcome") : null;

                // A pass is retired the same way when a person asked for a change after it (D-185): the change is
                // what this run measures, and a verdict over the run before it would cite evidence that is gone.
                // Only on request, so a stray re-recording still cannot quietly unseat a verdict.
                bool asked = request.RetirePass && string.Equals(outcome, "pass", StringComparison.OrdinalIgnoreCase);

                if (!asked && !string.Equals(outcome, "blocked", StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Fail<Response>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"Cycle {cycle.Id} already has a verdict at {verdict}, and it adjudicates the run recorded now. Its outcome is '{outcome ?? "unreadable"}', and only a blocked verdict is retired by recording a new run -- or a pass, with --retire-pass, when a person asked for a change. Recording one would leave every claim in it citing evidence that is gone. Delete {Cycle.FileNameOf(ArtifactKind.Verify)} and verify again once this run is recorded -- the findings rounds under findings/ are kept either way, so the history of what was challenged survives."));
                }

                retire = true;
            }

            // D-124: the patch is measured to a commit, so the chain can rebuild it from git rather than
            // trust a stored copy. Code nobody committed has no commit to name, and a verify of it would be a
            // verify of something no clone can see. The cycle's own paperwork is exempt -- this command writes
            // there -- and so is the run's own output, which is what is being recorded.
            IReadOnlyList<string> uncommitted =
            [
                .. repository.UncommittedChanges(solution.Path)
                    .Select(path => path.Replace('\\', '/'))
                    .Where(path => !path.StartsWith(Cycle.SdlcDirectoryName + "/", StringComparison.Ordinal))
                    .Where(path => !IsRunOutput(path, request)),
            ];

            if (uncommitted.Count > 0)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{uncommitted.Count} file(s) outside {Cycle.SdlcDirectoryName}/ are not committed: {string.Join(", ", uncommitted.Take(5))}{(uncommitted.Count > 5 ? ", ..." : string.Empty)}. The evidence names the commit the build was recorded at, so commit the change first, then record it."));
            }

            string? head = repository.CommitOf(solution.Path, "HEAD");

            if (head is null)
            {
                return Result.Fail<Response>("git could not say which commit is checked out. The evidence names it, so without it there is nothing to record.");
            }

            Directory.CreateDirectory(cycle.EvidenceDirectory);
            List<string> written = [];

            // HEAD rather than a branch named after the cycle: a build runs on cycle/<id>/build or
            // cycle/<id>/build-unattended, or on the bare cycle/<id> of a cycle opened before those, and what
            // is checked out is the build being recorded. D-85.
            string integration = await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken)
                .ConfigureAwait(false);
            string? divergence = repository.MergeBase(solution.Path, "HEAD", integration);

            if (divergence is null)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"git could not say where this branch diverged from {integration} or the default branch. The diff is measured from that commit, so without it there is nothing to record."));
            }

            // A cycle recorded before keeps the base it was measured from while that is still behind what is checked
            // out: a remediation records the same cycle again, and by then later cycles of its run may sit between
            // its base and the new head (D-206).
            string? previousBase = await EvidenceDiff.Commit(cycle, BaseFileName, cancellationToken).ConfigureAwait(false);
            string? previousHead = await EvidenceDiff.Commit(cycle, HeadFileName, cancellationToken).ConfigureAwait(false);

            string baseCommit = previousBase is not null && repository.IsAncestor(solution.Path, previousBase, head)
                ? previousBase
                : await BuiltOnto(cycle, head, divergence, previousHead, cancellationToken).ConfigureAwait(false) ?? divergence;

            // A run that merged the integration branch after the base would otherwise hand this cycle everything the
            // merge brought. Measured instead from the base with that merge taken in, rebuilt from the two commits
            // (D-206); EvidenceDiff.From is where every reader of the patch does the same.
            string from = baseCommit;
            string? merged = null;

            if (!repository.IsAncestor(solution.Path, divergence, baseCommit))
            {
                if (repository.MergeTree(solution.Path, baseCommit, divergence) is not { } tree)
                {
                    return Result.Fail<Response>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"This branch took in {integration} after {Short(baseCommit)}, where cycle {cycle.Id} is measured from, and git could not work out that merge on its own to leave what it brought out of the cycle's change. That needs git 2.38 or later ('git --version'). Nothing was recorded."));
                }

                if (tree.Conflicts.Count > 0)
                {
                    return Result.Fail<Response>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"This branch took in {integration} after {Short(baseCommit)}, where cycle {cycle.Id} is measured from, and that merge overlaps the work before it in {string.Join(", ", tree.Conflicts.Take(5))}{(tree.Conflicts.Count > 5 ? ", ..." : string.Empty)}. The cycle's own change cannot be told apart from what the merge brought, so nothing was recorded. Record the evidence once the overlap is resolved in a commit of its own, or build the cycle again on top of the merge."));
                }

                from = tree.Tree;
                merged = divergence;
            }

            await Write(cycle, BaseFileName, baseCommit + Environment.NewLine, written, cancellationToken)
                .ConfigureAwait(false);
            await Write(cycle, HeadFileName, head + Environment.NewLine, written, cancellationToken)
                .ConfigureAwait(false);

            string mergedPath = Path.Combine(cycle.EvidenceDirectory, MergedFileName);

            if (merged is not null)
            {
                await Write(cycle, MergedFileName, merged + Environment.NewLine, written, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (File.Exists(mergedPath))
            {
                File.Delete(mergedPath);
            }

            // The cycle's own paperwork is left out, for the reason DeclaredFiles.IsFrameworkOwned gives:
            // this patch is what a verify reads and nothing else, and it used to open with the plan the
            // verify is meant to be checked against. Keeping the two readers on one answer is the point.
            //
            // Between the two commits rather than to the working tree (D-124): the one patch the chain can
            // rebuild on a clean checkout, and the same one the uncommitted check above makes it equal to.
            string diff = repository.DiffBetween(solution.Path, from, head, [Cycle.SdlcDirectoryName])
                ?? string.Empty;

            await Write(cycle, DiffFileName, diff, written, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<string> changed = repository.ChangedFiles(solution.Path, from);

            // The two answers have to agree, and they used to not: `git diff` cannot see an untracked
            // file while ChangedFiles asks for them explicitly, so a cycle that created files recorded
            // an empty patch beside a full list of changes -- and said "every declared file changed,
            // and nothing else did" on the way past. Diff now stages untracked paths with
            // --intent-to-add, so this cannot happen; if it does, it is a defect in the evidence
            // itself and §4.5 has verify reading this patch "and nothing else", so it refuses rather
            // than recording something a verify would cite and a reviewer would trust. F-05.
            //
            // Counted over the files the patch is allowed to carry. The patch excludes the cycle's own
            // paperwork, so a run whose only changes were under .sdlc/ would otherwise trip this on a
            // disagreement that is the design rather than a defect.
            int substantive = changed.Count(path => !DeclaredFiles.IsFrameworkOwned(path));

            if (substantive > 0 && string.IsNullOrWhiteSpace(diff))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{substantive} file(s) outside {Cycle.SdlcDirectoryName}/ changed since {Short(baseCommit)} and the recorded patch is empty, which cannot both be true. Nothing was recorded, because a verify reads the patch and nothing else -- an empty one would leave a claim with nothing to cite and a panel saying everything is in order. Check that git can see the change: 'git status --short'."));
            }

            TestSummary? summary = null;
            IReadOnlyList<string> sources = [];

            if (request.Trx is { } trx)
            {
                Result<IReadOnlyList<string>> found = Sources(trx);

                if (found.IsFailed)
                {
                    return found.ToResult<Response>();
                }

                sources = found.Value;

                Result<TestSummary> normalised =
                    await Normalise(cycle, sources, written, cancellationToken).ConfigureAwait(false);

                if (normalised.IsFailed)
                {
                    return normalised.ToResult<Response>();
                }

                summary = normalised.Value;
            }

            if (request.BuildLog is { } log && File.Exists(log))
            {
                await Write(
                    cycle,
                    BuildLogFileName,
                    await File.ReadAllTextAsync(log, cancellationToken).ConfigureAwait(false),
                    written,
                    cancellationToken).ConfigureAwait(false);
            }

            CoverageSummary? coverage = null;

            if (request.Coverage is { } reports)
            {
                Result<CoverageSummary> measured = await Measure(cycle, reports, baseCommit, diff, written, cancellationToken)
                    .ConfigureAwait(false);

                if (measured.IsFailed)
                {
                    return measured.ToResult<Response>();
                }

                coverage = measured.Value;
            }

            FileComparison comparison = DeclaredFiles.Compare(
                DeclaredFiles.Read(await File.ReadAllTextAsync(plan, cancellationToken).ConfigureAwait(false)),
                changed);

            // Last, so a recording refused on the way leaves the verdict where it was.
            if (retire)
            {
                File.Delete(verdict);
            }

            RunTimings? timings = written.Contains(RunTimings.FileName)
                ? RunTimings.Read(await File.ReadAllTextAsync(Path.Combine(cycle.EvidenceDirectory, RunTimings.FileName), cancellationToken).ConfigureAwait(false))
                : null;

            return Result.Ok(new Response(cycle, written, summary, changed, comparison, sources, coverage, retire, timings));
        }

        private static string Short(string commit) => commit[..Math.Min(12, commit.Length)];

        /// <summary>Whether an uncommitted path is the run's own output, named on the command line.</summary>
        private bool IsRunOutput(string path, Command request)
        {
            foreach (string? given in new[] { request.Trx, request.BuildLog, request.Coverage })
            {
                if (given is null)
                {
                    continue;
                }

                string relative = Path.GetRelativePath(solution.Path, Path.GetFullPath(given, solution.Path))
                    .Replace('\\', '/')
                    .TrimEnd('/');

                if (path.Equals(relative, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(relative + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads the run's coverage, measures the change and the whole codebase, and carries the baseline.
        /// </summary>
        /// <remarks>
        /// <para>The baseline is read <strong>at the base commit</strong>, never from the working tree: a cycle
        /// could otherwise lower the bar it is about to be measured against, and the file lives under
        /// <c>.sdlc/</c>, which the recorded patch leaves out. The new rate is written back whenever the
        /// codebase holds its coverage (D-157): under the solution's floor that means it held the baseline, so
        /// the bar there only rises; at or above the floor the baseline follows the rate, so it stays the last
        /// rate a merged cycle recorded rather than the highest one ever reached.</para>
        /// <para>Two cycles built at once both move the file, and the second to merge conflicts on it -- which
        /// is right: its rate was measured without the first one's code. <c>resolve-conflict</c> records
        /// the evidence again.</para>
        /// </remarks>
        private async Task<Result<CoverageSummary>> Measure(
            Cycle cycle,
            string reports,
            string baseCommit,
            string diff,
            List<string> written,
            CancellationToken cancellationToken)
        {
            List<string> found = File.Exists(reports)
                ? [reports]
                : Directory.Exists(reports)
                    ? [.. Directory.EnumerateFiles(reports, "*.cobertura.xml", SearchOption.AllDirectories).Order(StringComparer.Ordinal)]
                    : [];

            if (found.Count == 0)
            {
                return Result.Fail<CoverageSummary>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{reports}: no cobertura report there. Run the tests with '--coverage --coverage-output-format cobertura' and point --coverage at the file, or at the directory holding every *.cobertura.xml the run wrote."));
            }

            List<(string Text, string Source)> texts = [];

            foreach (string report in found)
            {
                texts.Add((await File.ReadAllTextAsync(report, cancellationToken).ConfigureAwait(false), report));
            }

            Result<IReadOnlyDictionary<string, IReadOnlyDictionary<int, int>>> lines = Evidence.Coverage.Read(texts, solution.Path);

            if (lines.IsFailed)
            {
                return lines.ToResult<CoverageSummary>();
            }

            string baselinePath = Path.Combine(Cycle.SdlcDirectoryName, Evidence.Coverage.BaselineFileName).Replace('\\', '/');
            double? baseline = Evidence.Coverage.ReadBaseline(repository.ReadAt(solution.Path, baseCommit, baselinePath));

            CoverageSummary measured = Evidence.Coverage.Summarise(lines.Value, diff, baseline) with
            {
                Floor = await SolutionDocument.CoverageFloorOf(solution.Path, cancellationToken).ConfigureAwait(false),
            };

            await Write(cycle, Evidence.Coverage.SummaryFileName, Evidence.Coverage.Write(measured), written, cancellationToken)
                .ConfigureAwait(false);

            if (measured.HoldsItsCoverage)

            {
                await File.WriteAllTextAsync(
                    Path.Combine(solution.Path, Cycle.SdlcDirectoryName, Evidence.Coverage.BaselineFileName),
                    Evidence.Coverage.WriteBaseline(measured.Rate),
                    cancellationToken).ConfigureAwait(false);
            }

            return Result.Ok(measured);
        }

        /// <summary>
        /// Finds every results file a run produced.
        /// </summary>
        /// <remarks>
        /// <strong>A run with two test projects writes two results files, and this took one.</strong> The
        /// summary silently held the first project's results while the build log recorded the whole run,
        /// so a verification adjudicated against part of a suite and had no way to know: a cycle recorded
        /// 14 of 26 results, and every claim it made was true of the 14. The architecture tests were the
        /// half that went missing, which is the half that holds the rules.
        /// <para>
        /// A directory is therefore accepted and searched, because the number of results files is a fact
        /// about the solution rather than something a caller should have to count. Sorted, so the order
        /// recorded is the order on disk rather than the order the file system happened to enumerate.
        /// </para>
        /// </remarks>
        /// <param name="path">A results file, or a directory holding them.</param>
        /// <returns>Every results file to read, or a failure saying why there are none.</returns>
        private static Result<IReadOnlyList<string>> Sources(string path)
        {
            if (File.Exists(path))
            {
                return Result.Ok<IReadOnlyList<string>>([path]);
            }

            if (!Directory.Exists(path))
            {
                return Result.Fail<IReadOnlyList<string>>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{path}: no such file or directory. If the run produced no results file, the run did not happen -- which is not the same as a run with nothing to report."));
            }

            List<string> found = [.. Directory
                .EnumerateFiles(path, "*.trx", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.Ordinal)];

            return found.Count == 0
                ? Result.Fail<IReadOnlyList<string>>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{path}: holds no .trx file. If the run produced no results file, the run did not happen -- which is not the same as a run with nothing to report."))
                : Result.Ok<IReadOnlyList<string>>(found);
        }

        /// <summary>
        /// Reads the runner's output, writes it through, and writes the normalised summary beside it.
        /// </summary>
        /// <remarks>
        /// The only place in the framework that knows what a runner's format looks like. Everything downstream
        /// reads the summary, so adding a stack means adding a reader rather than changing what the gate does.
        /// </remarks>
        private async Task<Result<TestSummary>> Normalise(
            Cycle cycle,
            IReadOnlyList<string> sources,
            List<string> written,
            CancellationToken cancellationToken)
        {
            List<RecordedTest> tests = [];
            List<(string Xml, string File)> kept = [];

            for (int index = 0; index < sources.Count; index++)
            {
                string trx = sources[index];
                string xml = await File.ReadAllTextAsync(trx, cancellationToken).ConfigureAwait(false);
                Result<IReadOnlyList<RecordedTest>> read = TestSummaryReader.ReadTrx(xml, trx);

                if (read.IsFailed)
                {
                    return read.ToResult<TestSummary>();
                }

                tests.AddRange(read.Value);

                // Under a fixed name rather than the runner's own, which is usually a GUID. §16 documents
                // the evidence directory as holding a `tests.trx`, and a cycle's evidence directory is
                // read by people and cited by a verify -- a predictable name is worth more than a record
                // of what the runner happened to call it, which the original path already says. F-38.
                // A run with several results files numbers them, because one of them cannot be the one.
                string keptAs = sources.Count == 1 ? TrxFileName : TrxFileNameAt(index + 1);
                kept.Add((xml, keptAs));

                await Write(
                    cycle,
                    keptAs,
                    xml,
                    written,
                    cancellationToken).ConfigureAwait(false);
            }

            // Two projects reporting the same test name make the chain check ambiguous: it matches a
            // plan's named test on the end of what the runner reported, so a failing one and a passing
            // one of the same name would let the passing one answer for both.
            List<string> ambiguous = [.. tests
                .GroupBy(test => test.Name, StringComparer.Ordinal)
                .Where(group => group.Select(test => test.Outcome).Distinct().Count() > 1)
                .Select(group => group.Key)
                .OrderBy(name => name, StringComparer.Ordinal)];

            if (ambiguous.Count > 0)
            {
                return Result.Fail<TestSummary>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The run reported {string.Join(", ", ambiguous)} with more than one outcome, across {sources.Count} results files. The chain check matches a plan's named test on the end of what the runner reported, so a passing result would answer for a failing one of the same name. Rename one of them, or run the projects separately."));
            }

            TestSummary summary = new(
                cycle.Id.ToString(),
                time.GetUtcNow(),
                tests,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim()} {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}"));

            await Write(
                cycle,
                SummaryFileName,
                TestSummaryReader.Write(summary),
                written,
                cancellationToken).ConfigureAwait(false);

            // Where the run's time went, for whoever asks next (D-169). Beside the summary rather than in it, so
            // no reader is handed a duration to judge; a run whose files carry no times leaves none.
            string timingsPath = Path.Combine(cycle.EvidenceDirectory, RunTimings.FileName);

            if (RunTimings.FromTrx(kept) is { } timings)
            {
                await Write(cycle, RunTimings.FileName, RunTimings.Write(timings), written, cancellationToken).ConfigureAwait(false);
            }
            else if (File.Exists(timingsPath))
            {
                File.Delete(timingsPath);
            }

            return Result.Ok(summary);
        }

        private static async Task Write(
            Cycle cycle,
            string name,
            string contents,
            List<string> written,
            CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(
                Path.Combine(cycle.EvidenceDirectory, name),
                contents,
                cancellationToken).ConfigureAwait(false);

            written.Add(name);
        }
    }
}
