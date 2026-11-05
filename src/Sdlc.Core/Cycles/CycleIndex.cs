namespace Sdlc.Core.Cycles;

/// <summary>
/// Finds the cycles a solution holds.
/// </summary>
/// <remarks>
/// Reads the directory rather than a manifest. A manifest is a second place the truth lives, and the
/// one that goes stale is always the one a tool trusts -- a cycle created by hand, or one on a branch
/// that was merged, would be invisible. The directory is what the gate reads and so it is what this
/// reads.
/// <para>
/// A persisted index exists later for a different reason: so a batch run can be resumed and read, not
/// so work can be found. Finding work stays a directory listing.
/// </para>
/// </remarks>
public static class CycleIndex
{
    /// <summary>
    /// Every cycle in a solution, oldest first.
    /// </summary>
    /// <remarks>
    /// Chronological because cycle ids are purely temporal, so sorting the names sorts the cycles. That
    /// is also the order a batch serialises overlapping plans in, which means one ordering rather than
    /// two that could disagree.
    /// </remarks>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <returns>The cycles found, which may be none.</returns>
    public static IReadOnlyList<Cycle> All(string solutionRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);

        string cycles = Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName);

        if (!Directory.Exists(cycles))
        {
            return [];
        }

        List<Cycle> found = [];

        foreach (string directory in Directory.EnumerateDirectories(cycles).Order(StringComparer.Ordinal))
        {
            // A directory whose name is not a cycle id is somebody else's, and skipping it quietly is
            // right: this is a listing, and a listing that fails on an unrelated folder is a listing
            // nothing can rely on.
            if (CycleId.TryParse(Path.GetFileName(directory), out CycleId? id))
            {
                found.Add(new Cycle(id.Value, solutionRoot));
            }
        }

        return found;
    }
}
