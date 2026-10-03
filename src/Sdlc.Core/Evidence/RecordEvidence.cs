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
        FileComparison Comparison);

    /// <summary>The normalised test summary every later reader uses.</summary>
    public const string SummaryFileName = "tests.json";

    /// <summary>The commit the change is measured from.</summary>
    public const string BaseFileName = "base.sha";

    /// <summary>The change itself.</summary>
    public const string DiffFileName = "diff.patch";

    /// <summary>The runner's own results file, kept beside the normalised summary under a fixed name.</summary>
    public const string TrxFileName = "tests.trx";

    /// <summary>Records what a build produced, as files rather than prose.</summary>
    /// <param name="Id">The cycle the build belongs to.</param>
    /// <param name="Trx">A runner's own test results to normalise, or null when there are none yet.</param>
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

            Directory.CreateDirectory(cycle.EvidenceDirectory);
            List<string> written = [];

            string? baseCommit = repository.MergeBase(solution.Path, cycle.Id.BranchName());

            if (baseCommit is null)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"git could not say where {cycle.Id.BranchName()} diverged from the default branch. The diff is measured from that commit, so without it there is nothing to record."));
            }

            await Write(cycle, BaseFileName, baseCommit + Environment.NewLine, written, cancellationToken)
                .ConfigureAwait(false);

            string diff = repository.Diff(solution.Path, baseCommit) ?? string.Empty;
            await Write(cycle, DiffFileName, diff, written, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<string> changed = repository.ChangedFiles(solution.Path, baseCommit);

            // The two answers have to agree, and they used to not: `git diff` cannot see an untracked
            // file while ChangedFiles asks for them explicitly, so a cycle that created files recorded
            // an empty patch beside a full list of changes -- and said "every declared file changed,
            // and nothing else did" on the way past. Diff now stages untracked paths with
            // --intent-to-add, so this cannot happen; if it does, it is a defect in the evidence
            // itself and §4.5 has verify reading this patch "and nothing else", so it refuses rather
            // than recording something a verify would cite and a reviewer would trust. F-05.
            if (changed.Count > 0 && string.IsNullOrWhiteSpace(diff))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{changed.Count} file(s) changed since {baseCommit[..Math.Min(12, baseCommit.Length)]} and the recorded patch is empty, which cannot both be true. Nothing was recorded, because a verify reads the patch and nothing else -- an empty one would leave a claim with nothing to cite and a panel saying everything is in order. Check that git can see the change: 'git status --short'."));
            }

            TestSummary? summary = null;

            if (request.Trx is { } trx)
            {
                Result<TestSummary> normalised =
                    await Normalise(cycle, trx, written, cancellationToken).ConfigureAwait(false);

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

            return Result.Ok(new Response(cycle, written, summary, changed, comparison));
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
            string trx,
            List<string> written,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(trx))
            {
                return Result.Fail<TestSummary>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{trx}: no such file. If the run produced no results file, the run did not happen -- which is not the same as a run with nothing to report."));
            }

            string xml = await File.ReadAllTextAsync(trx, cancellationToken).ConfigureAwait(false);
            Result<IReadOnlyList<RecordedTest>> read = TestSummaryReader.ReadTrx(xml, trx);

            if (read.IsFailed)
            {
                return read.ToResult<TestSummary>();
            }

            // Under a fixed name rather than the runner's own, which is usually a GUID. §16 documents
            // the evidence directory as holding a `tests.trx`, and a cycle's evidence directory is
            // read by people and cited by a verify -- a predictable name is worth more than a record
            // of what the runner happened to call it, which the original path already says. F-38.
            await Write(cycle, TrxFileName, xml, written, cancellationToken).ConfigureAwait(false);

            TestSummary summary = new(
                Cycle.FrameworkVersion,
                cycle.Id.ToString(),
                time.GetUtcNow(),
                read.Value);

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
