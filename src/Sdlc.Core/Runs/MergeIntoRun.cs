using System.Globalization;
using System.Runtime.InteropServices;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Runs;

/// <summary>
/// Merges one branch into the run's branch checked out here -- a cycle's build, or the integration branch a run fell
/// behind -- runs the whole suite on the result, and keeps the merge only when it is green (D-234).
/// </summary>
/// <remarks>
/// <para><strong>The binary does every merge into a run, one at a time.</strong> A merge holds the repository's merge
/// lock from before it starts until its evidence is committed, so two cycles finishing together, or a person's session
/// beside a series, never merge into one branch at once.</para>
/// <para><strong>Each merge is tested where it lands.</strong> Two cycles can each pass on their own and fail together;
/// a cycle built beside another never ran the other's code. So the suite runs once on the merged commit, and a merge it
/// does not pass is undone, leaving the branch where it was.</para>
/// <para><strong>A conflict is never resolved here.</strong> The merge is abandoned and the files named; which side
/// wins is a person's to settle, or the cycle's to rebuild on the run's latest.</para>
/// </remarks>
public static class MergeIntoRun
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddMergeIntoRunRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>How a merge ended.</summary>
    public enum Outcome
    {
        /// <summary>Merged, the suite green on the result, and its evidence committed.</summary>
        Merged,

        /// <summary>The two sides changed the same lines; the merge was abandoned.</summary>
        Conflict,

        /// <summary>Merged cleanly, but the result did not build or a test failed on it; the merge was undone.</summary>
        Red,
    }

    /// <summary>Merges a branch into the run checked out here.</summary>
    /// <param name="Kind">Which size of run.</param>
    /// <param name="Id">The run's id.</param>
    /// <param name="Source">What to merge: a branch, or a remote's branch.</param>
    /// <param name="Label">What it is, for the commit message and the report, as in <c>cycle 20261121_1000_00</c>.</param>
    public sealed record Command(RunKind Kind, string Id, string Source, string Label) : IRequest<Response>;

    /// <summary>How the merge ended.</summary>
    /// <param name="Outcome">Merged, a conflict, or red.</param>
    /// <param name="Detail">What happened, in a sentence a report can carry.</param>
    /// <param name="Head">The merge commit the suite ran on, when it merged.</param>
    /// <param name="Files">The files a conflict stopped on, or the tests a red suite failed.</param>
    public sealed record Response(Outcome Outcome, string Detail, string? Head, IReadOnlyList<string> Files);

    private sealed class Handler(SolutionRoot solution, IRepository repository, ISuiteRunner suite, TimeProvider clock)
        : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            string branch = RunBranch.NameOf(request.Kind, request.Id);

            if (!string.Equals(repository.CurrentBranch(root), branch, StringComparison.Ordinal))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"A merge into {branch} runs in its worktree, and {root} is on {repository.CurrentBranch(root) ?? "no branch"}."));
            }

            if (repository.UncommittedChanges(root) is { Count: > 0 } dirty)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{branch} has changes nobody committed ({string.Join(", ", dirty.Take(5))}), and a merge that failed would undo them with itself. Commit them first."));
            }

            if (repository.CommonDirectory(root) is not { } common)
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"{root} is not in a git repository."));
            }

            using IDisposable held = await MergeLock.Take(common, cancellationToken).ConfigureAwait(false);

            if (repository.CommitOf(root, "HEAD") is not { } before)
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"{branch} has no commit to merge into."));
            }

            string message = string.Create(CultureInfo.InvariantCulture, $"Merge {request.Label} into {branch}");
            (int merged, string said) = await LongProcess
                .Git(root, cancellationToken, "merge", "--no-ff", "--no-edit", "-m", message, request.Source)
                .ConfigureAwait(false);

            if (merged != 0)
            {
                (_, string unmerged) = await LongProcess.Git(root, cancellationToken, "diff", "--name-only", "--diff-filter=U").ConfigureAwait(false);
                await LongProcess.Git(root, cancellationToken, "merge", "--abort").ConfigureAwait(false);
                List<string> files = [.. Lines(unmerged)];

                return Result.Ok(new Response(
                    Outcome.Conflict,
                    files.Count > 0
                        ? string.Create(CultureInfo.InvariantCulture, $"{request.Label} conflicts with {branch} in {string.Join(", ", files)}.")
                        : string.Create(CultureInfo.InvariantCulture, $"{request.Label} did not merge into {branch}: {Last(said)}"),
                    null,
                    files));
            }

            string head = repository.CommitOf(root, "HEAD") ?? before;
            string results = Path.Combine(Path.GetTempPath(), "sdlc-merge-" + Guid.NewGuid().ToString("N"));

            try
            {
                SuiteRun run = await suite.Run(root, results, cancellationToken).ConfigureAwait(false);
                Result<List<RecordedTest>> tests = await Read(run, cancellationToken).ConfigureAwait(false);
                List<string> failed = tests.IsSuccess ? [.. tests.Value.Where(test => test.Outcome == TestOutcome.Failed).Select(test => test.Name)] : [];

                if (!run.Built || tests.IsFailed || failed.Count > 0)
                {
                    await LongProcess.Git(root, cancellationToken, "reset", "--hard", before).ConfigureAwait(false);

                    string why = !run.Built
                        ? "the result did not build"
                        : tests.IsFailed
                            ? string.Join("; ", tests.Errors.Select(error => error.Message))
                            : string.Create(CultureInfo.InvariantCulture, $"{Count(failed.Count, "test")} failed on the result: {string.Join(", ", failed.Take(8))}{(failed.Count > 8 ? " and more" : string.Empty)}");

                    return Result.Ok(new Response(
                        Outcome.Red,
                        string.Create(CultureInfo.InvariantCulture, $"{request.Label} merged into {branch} cleanly, but {why}, so the merge was undone."),
                        null,
                        failed));
                }

                Result kept = await Keep(root, request, head, tests.Value, run.Results, cancellationToken).ConfigureAwait(false);

                if (kept.IsFailed)
                {
                    return kept.ToResult<Response>();
                }

                return Result.Ok(new Response(
                    Outcome.Merged,
                    string.Create(CultureInfo.InvariantCulture, $"{request.Label} merged into {branch} at {head[..Math.Min(12, head.Length)]}, {Count(tests.Value.Count, "test")} run on the result, none failing."),
                    head,
                    []));
            }
            finally
            {
                if (Directory.Exists(results))
                {
                    Directory.Delete(results, recursive: true);
                }
            }
        }

        /// <summary>Every test the run's results files report; no results, or no tests, is a failure rather than a pass.</summary>
        private static async Task<Result<List<RecordedTest>>> Read(SuiteRun run, CancellationToken cancellationToken)
        {
            if (!run.Built)
            {
                return Result.Ok<List<RecordedTest>>([]);
            }

            List<RecordedTest> tests = [];

            foreach (string file in run.Results)
            {
                Result<IReadOnlyList<RecordedTest>> read = TestSummaryReader.ReadTrx(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false), file);

                if (read.IsFailed)
                {
                    return read.ToResult<List<RecordedTest>>();
                }

                tests.AddRange(read.Value);
            }

            return tests.Count == 0
                ? Result.Fail<List<RecordedTest>>("no test ran on the result, which is a failure rather than a clean run")
                : Result.Ok(tests);
        }

        /// <summary>Writes the merge's evidence into the run's folder and commits it on top of the merge.</summary>
        private async Task<Result> Keep(
            string root,
            Command request,
            string head,
            List<RecordedTest> tests,
            IReadOnlyList<string> results,
            CancellationToken cancellationToken)
        {
            string directory = MergeEvidence.DirectoryIn(root, request.Kind, request.Id);

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            Directory.CreateDirectory(directory);

            TestSummary summary = new(
                null,
                clock.GetUtcNow(),
                tests,
                string.Create(CultureInfo.InvariantCulture, $"{RuntimeInformation.OSDescription.Trim()} {RuntimeInformation.OSArchitecture}"));

            await File.WriteAllTextAsync(Path.Combine(directory, RecordEvidence.HeadFileName), head + "\n", cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, RecordEvidence.SummaryFileName), TestSummaryReader.Write(summary), cancellationToken).ConfigureAwait(false);

            for (int index = 0; index < results.Count; index++)
            {
                File.Copy(results[index], Path.Combine(directory, results.Count == 1 ? RecordEvidence.TrxFileName : RecordEvidence.TrxFileNameAt(index + 1)));
            }

            return repository.Commit(
                root,
                [MergeEvidence.RelativeDirectory(request.Kind, request.Id)],
                string.Create(CultureInfo.InvariantCulture, $"Merge evidence: {request.Label}, suite green at {head[..Math.Min(12, head.Length)]}"));
        }

        private static string[] Lines(string text) =>
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static string Last(string text) => Lines(text).LastOrDefault() ?? "git said nothing";

        private static string Count(int count, string noun) =>
            string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");
    }
}
