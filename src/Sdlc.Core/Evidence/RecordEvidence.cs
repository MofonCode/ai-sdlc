using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
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
    public sealed record Response(
        Cycle Cycle,
        IReadOnlyList<string> Written,
        TestSummary? Summary,
        IReadOnlyList<string> Changed,
        FileComparison Comparison,
        IReadOnlyList<string> TestSources);

    /// <summary>The normalised test summary every later reader uses.</summary>
    public const string SummaryFileName = "tests.json";

    /// <summary>The commit the change is measured from.</summary>
    public const string BaseFileName = "base.sha";

    /// <summary>The change itself.</summary>
    public const string DiffFileName = "diff.patch";

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
    public sealed record Command(CycleId Id, string? Trx = null, string? BuildLog = null)
        : IRequest<Response>;

    /// <summary>
    /// Writes a build's evidence, and compares what changed against what the plan declared.
    /// </summary>
    /// <remarks>
    /// <c>verify</c> reads only what this writes. That is the point of recording it: a verdict derived from a
    /// session's recollection of the build is a verdict nobody can check, and <c>CON_08</c> forbids a claim
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
            string verdict = cycle.PathOf(ArtifactKind.Verify);

            if (File.Exists(verdict))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} already has a verdict at {verdict}, and it adjudicates the run recorded now. Recording a new run would leave every claim in it citing evidence that is gone. Delete {Cycle.FileNameOf(ArtifactKind.Verify)} and verify again once this run is recorded -- the findings rounds under findings/ are kept either way, so the history of what was challenged survives."));
            }

            Directory.CreateDirectory(cycle.EvidenceDirectory);
            List<string> written = [];

            // HEAD rather than a branch named after the cycle: a build runs on cycle/<id>/build or
            // cycle/<id>/build-unattended, or on the bare cycle/<id> of a cycle opened before those, and what
            // is checked out is the build being recorded. D-85.
            string integration = await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken)
                .ConfigureAwait(false);
            string? baseCommit = repository.MergeBase(solution.Path, "HEAD", integration);

            if (baseCommit is null)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"git could not say where this branch diverged from {integration} or the default branch. The diff is measured from that commit, so without it there is nothing to record."));
            }

            await Write(cycle, BaseFileName, baseCommit + Environment.NewLine, written, cancellationToken)
                .ConfigureAwait(false);

            // The cycle's own paperwork is left out, for the reason DeclaredFiles.IsFrameworkOwned gives:
            // this patch is what a verify reads and nothing else, and it used to open with the plan the
            // verify is meant to be checked against. Keeping the two readers on one answer is the point.
            string diff = repository.Diff(solution.Path, baseCommit, [Cycle.SdlcDirectoryName])
                ?? string.Empty;

            await Write(cycle, DiffFileName, diff, written, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<string> changed = repository.ChangedFiles(solution.Path, baseCommit);

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
                    $"{substantive} file(s) outside {Cycle.SdlcDirectoryName}/ changed since {baseCommit[..Math.Min(12, baseCommit.Length)]} and the recorded patch is empty, which cannot both be true. Nothing was recorded, because a verify reads the patch and nothing else -- an empty one would leave a claim with nothing to cite and a panel saying everything is in order. Check that git can see the change: 'git status --short'."));
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
                    "build.log",
                    await File.ReadAllTextAsync(log, cancellationToken).ConfigureAwait(false),
                    written,
                    cancellationToken).ConfigureAwait(false);
            }

            FileComparison comparison = DeclaredFiles.Compare(
                DeclaredFiles.Read(await File.ReadAllTextAsync(plan, cancellationToken).ConfigureAwait(false)),
                changed);

            return Result.Ok(new Response(cycle, written, summary, changed, comparison, sources));
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
                await Write(
                    cycle,
                    sources.Count == 1 ? TrxFileName : TrxFileNameAt(index + 1),
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
                Cycle.FrameworkVersion,
                cycle.Id.ToString(),
                time.GetUtcNow(),
                tests);

            await Write(
                cycle,
                SummaryFileName,
                TestSummaryReader.Write(summary),
                written,
                cancellationToken).ConfigureAwait(false);

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
