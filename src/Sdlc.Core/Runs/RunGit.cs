namespace Sdlc.Core.Runs;

/// <summary>The commits and pushes a run's driver makes between the processes it starts.</summary>
internal static class RunGit
{
    /// <summary>Commits whatever changed under the paths, when anything did.</summary>
    /// <param name="root">The working copy.</param>
    /// <param name="message">The commit message.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <param name="paths">What to stage.</param>
    /// <returns>True when a commit was made.</returns>
    public static async Task<bool> Commit(string root, string message, CancellationToken cancellationToken, params string[] paths)
    {
        await LongProcess.Git(root, cancellationToken, ["add", "--", .. paths]).ConfigureAwait(false);
        (int unchanged, _) = await LongProcess.Git(root, cancellationToken, "diff", "--cached", "--quiet").ConfigureAwait(false);

        if (unchanged == 0)
        {
            return false;
        }

        (int committed, _) = await LongProcess.Git(root, cancellationToken, "commit", "-m", message).ConfigureAwait(false);
        return committed == 0;
    }

    /// <summary>Pushes a branch to <c>origin</c>, when the repository has it.</summary>
    /// <param name="root">The working copy.</param>
    /// <param name="branch">The branch.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>True when it reached the remote; false with no remote, or when the push failed.</returns>
    public static async Task<bool> Push(string root, string branch, CancellationToken cancellationToken)
    {
        (int listed, string names) = await LongProcess.Git(root, cancellationToken, "remote").ConfigureAwait(false);

        if (listed != 0 || !names.Split('\n', StringSplitOptions.TrimEntries).Contains("origin", StringComparer.Ordinal))
        {
            return false;
        }

        (int pushed, _) = await LongProcess.Git(root, cancellationToken, "push", "-u", "origin", branch).ConfigureAwait(false);
        return pushed == 0;
    }
}
