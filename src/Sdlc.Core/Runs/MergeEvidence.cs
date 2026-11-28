using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Series;

namespace Sdlc.Core.Runs;

/// <summary>What the last merge into a run's branch was checked by: the commit it made, and the suite run on it.</summary>
/// <param name="Head">The merge commit the suite ran on.</param>
/// <param name="SummaryPath">The normalised summary of that run.</param>
/// <param name="Summary">The summary itself.</param>
public sealed record MergeRun(string Head, string SummaryPath, TestSummary Summary);

/// <summary>
/// A run's merge evidence: after each merge into <c>wave/&lt;id&gt;</c> or <c>series/&lt;id&gt;</c>, the commit it
/// made and the whole suite run on it, kept in the run's own folder (D-234).
/// </summary>
/// <remarks>
/// <para><strong>Why a run keeps its own.</strong> Built one after another on one branch, a run's last cycle's run
/// contained every cycle before it, and the pull-request check read that. Built side by side and merged, no cycle's run
/// contains another's, so the run that counts is the one on the merged result.</para>
/// <para>Overwritten by each merge, because only the last can speak for the branch: an earlier one ran on less.</para>
/// </remarks>
public static class MergeEvidence
{
    /// <summary>The folder's name, inside the run's own folder.</summary>
    public const string DirectoryName = "merge";

    /// <summary>The run's merge evidence folder, relative to the root.</summary>
    /// <param name="kind">Which size of run.</param>
    /// <param name="id">The run's id.</param>
    /// <returns>The path, with forward slashes.</returns>
    public static string RelativeDirectory(RunKind kind, string id) =>
        (kind == RunKind.Series ? SeriesRecord.RelativeDirectory(id) : WaveRecord.RelativeDirectory(id)) + "/" + DirectoryName;

    /// <summary>The run's merge evidence folder in a working copy.</summary>
    /// <param name="solutionRoot">The working copy.</param>
    /// <param name="kind">Which size of run.</param>
    /// <param name="id">The run's id.</param>
    /// <returns>The full path.</returns>
    public static string DirectoryIn(string solutionRoot, RunKind kind, string id) =>
        Path.GetFullPath(Path.Combine(solutionRoot, RelativeDirectory(kind, id)));

    /// <summary>Reads the run's last merge evidence, when it has any.</summary>
    /// <param name="solutionRoot">The working copy.</param>
    /// <param name="kind">Which size of run.</param>
    /// <param name="id">The run's id.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The evidence, null when the run never merged, or why what is there cannot be read.</returns>
    public static async Task<Result<MergeRun?>> Read(string solutionRoot, RunKind kind, string id, CancellationToken cancellationToken)
    {
        string directory = DirectoryIn(solutionRoot, kind, id);
        string head = Path.Combine(directory, RecordEvidence.HeadFileName);
        string summary = Path.Combine(directory, RecordEvidence.SummaryFileName);

        if (!File.Exists(head) || !File.Exists(summary))
        {
            return Result.Ok<MergeRun?>(null);
        }

        Result<TestSummary> read = TestSummaryReader.Read(await File.ReadAllTextAsync(summary, cancellationToken).ConfigureAwait(false), summary);

        return read.IsFailed
            ? read.ToResult<MergeRun?>()
            : Result.Ok<MergeRun?>(new MergeRun((await File.ReadAllTextAsync(head, cancellationToken).ConfigureAwait(false)).Trim(), summary, read.Value));
    }
}
