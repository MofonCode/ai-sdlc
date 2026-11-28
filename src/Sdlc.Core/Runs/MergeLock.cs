namespace Sdlc.Core.Runs;

/// <summary>
/// One merge into a run's branch at a time, across every process on the machine that shares the repository (D-234).
/// </summary>
/// <remarks>
/// A file in the repository's common directory, held open with no sharing for as long as the merge and its suite take.
/// The operating system releases it when the process ends, however it ends, so a killed merge never leaves the lock
/// taken. The file itself is left in place: deleting it on release would let a waiting process lock a file that is
/// already gone while a third creates and locks a new one.
/// </remarks>
internal static class MergeLock
{
    /// <summary>The lock file's name, in the repository's common directory.</summary>
    public const string FileName = "sdlc-merge.lock";

    /// <summary>Waits for the lock and takes it.</summary>
    /// <param name="commonDirectory">The repository's common directory, shared by every worktree.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The lock, released when disposed.</returns>
    public static async Task<IDisposable> Take(string commonDirectory, CancellationToken cancellationToken)
    {
        string path = Path.Combine(commonDirectory, FileName);

        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
