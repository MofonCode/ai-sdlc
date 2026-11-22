using Sdlc.Core.Cycles;
using Sdlc.Core.Git;

namespace Sdlc.Core.Evidence;

/// <summary>
/// The patch a verify reads, rebuilt from the two commits the evidence records (D-124).
/// </summary>
/// <remarks>
/// <para><strong><c>diff.patch</c> is a working file, not a record.</strong> Git holds the change, so the cycle
/// commits <c>base.sha</c> and <c>head.sha</c> and the patch is rebuilt from them wherever it is needed: by the
/// evidence hash a verify round records and <c>verify-chain</c> compares, and on a checkout that never had the
/// file, so a reader there reads the patch the build did.</para>
/// <para><strong>A cycle that took in a merge of the integration branch</strong> also records <c>merged.sha</c>, and
/// its patch is measured from the merge of the two (D-206); <see cref="From"/> is the one place that says so.</para>
/// <para><strong>A cycle with no <c>head.sha</c> recorded its evidence before this</strong>, committed its patch,
/// and is read from the file as before. Nothing about it is rewritten.</para>
/// </remarks>
public static class EvidenceDiff
{
    /// <summary>
    /// The patch rebuilt from <c>base.sha</c> and <c>head.sha</c>, or null when either is missing or git cannot
    /// reach one of the commits.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="repository">Git.</param>
    /// <param name="root">The repository's root.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The patch, or null.</returns>
    public static async Task<string?> Rebuild(Cycle cycle, IRepository repository, string root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(repository);

        if (await From(cycle, repository, root, cancellationToken).ConfigureAwait(false) is not { } from
            || await Commit(cycle, RecordEvidence.HeadFileName, cancellationToken).ConfigureAwait(false) is not { } until)
        {
            return null;
        }

        return repository.DiffBetween(root, from, until, [Cycle.SdlcDirectoryName]);
    }

    /// <summary>
    /// Every path the recorded change touches, <c>.sdlc/</c> included, measured as the patch is; null when the
    /// evidence names no base or head, or git cannot reach them.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="repository">Git.</param>
    /// <param name="root">The repository's root.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The paths, relative to the root, or null.</returns>
    public static async Task<IReadOnlyList<string>?> ChangedFiles(Cycle cycle, IRepository repository, string root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(repository);

        return await From(cycle, repository, root, cancellationToken).ConfigureAwait(false) is { } from
            && await Commit(cycle, RecordEvidence.HeadFileName, cancellationToken).ConfigureAwait(false) is { } until
                ? repository.CommittedChanges(root, from, until)
                : null;
    }

    /// <summary>
    /// What the patch is measured from: <c>base.sha</c>, or, when the evidence also records <c>merged.sha</c>, the
    /// tree a merge of the two would produce; null when there is no base, or the merge cannot be rebuilt cleanly.
    /// </summary>
    /// <remarks>
    /// A run that merged the integration branch after the cycle before it finished records both commits rather than
    /// the merged tree, because a tree nothing references is never pushed and is pruned in time: two commits rebuild
    /// the same tree on any clone that holds them (D-206).
    /// </remarks>
    /// <param name="cycle">The cycle.</param>
    /// <param name="repository">Git.</param>
    /// <param name="root">The repository's root.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A commit or a tree, or null.</returns>
    internal static async Task<string?> From(Cycle cycle, IRepository repository, string root, CancellationToken cancellationToken)
    {
        if (await Commit(cycle, RecordEvidence.BaseFileName, cancellationToken).ConfigureAwait(false) is not { } from)
        {
            return null;
        }

        if (await Commit(cycle, RecordEvidence.MergedFileName, cancellationToken).ConfigureAwait(false) is not { } merged)
        {
            return from;
        }

        return repository.MergeTree(root, from, merged) is { Conflicts.Count: 0 } tree ? tree.Tree : null;
    }

    /// <summary>Whether the cycle's evidence names the commit its build was recorded at.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>True when <c>head.sha</c> exists.</returns>
    public static bool IsRebuilt(Cycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        return File.Exists(Path.Combine(cycle.EvidenceDirectory, RecordEvidence.HeadFileName));
    }

    /// <summary>
    /// Writes <c>diff.patch</c> from the two commits when this checkout does not have it.
    /// </summary>
    /// <remarks>
    /// The file is ignored by git, so a fresh clone of a build branch has <c>tests.json</c> and no patch. Every
    /// reader of the patch -- the verify's readers, the evidence check, the turn-end hook -- asks for it here
    /// first, so none of them reads a missing file as a change nobody recorded.
    /// </remarks>
    /// <param name="cycle">The cycle.</param>
    /// <param name="repository">Git.</param>
    /// <param name="root">The repository's root.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is in place, or when there was nothing to rebuild it from.</returns>
    public static async Task Restore(Cycle cycle, IRepository repository, string root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        string path = Path.Combine(cycle.EvidenceDirectory, RecordEvidence.DiffFileName);

        if (File.Exists(path) || !IsRebuilt(cycle))
        {
            return;
        }

        if (await Rebuild(cycle, repository, root, cancellationToken).ConfigureAwait(false) is { } patch)
        {
            await File.WriteAllTextAsync(path, patch, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task<string?> Commit(Cycle cycle, string name, CancellationToken cancellationToken)
    {
        string path = Path.Combine(cycle.EvidenceDirectory, name);

        return File.Exists(path)
            && (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim() is { Length: > 0 } commit
            ? commit
            : null;
    }
}
