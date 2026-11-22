using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;

namespace Sdlc.Core.Harvest;

/// <summary>
/// Whether a cycle is completed, which is the only kind a harvest reads (D-190).
/// </summary>
/// <remarks>
/// <para><strong>Completed is passed and merged, or abandoned.</strong> Since D-181 the pull request is where a person
/// accepts a build, so a cycle whose verify passed and whose pull request was closed, or sent back, was never accepted,
/// and its choices wait until it merges. An abandonment needs no merge: the person ended the cycle by writing it, and a
/// cycle stopped before any of it merged lives only on its own branch, where a harvest reads it (D-190).</para>
/// </remarks>
public static class Completion
{
    /// <summary>
    /// Whether a cycle is finished: its verify passed, or a person abandoned it.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>True when it is.</returns>
    public static async Task<bool> IsFinished(Cycle cycle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        if (cycle.IsAbandoned)
        {
            return true;
        }

        string verify = cycle.PathOf(ArtifactKind.Verify);

        if (!File.Exists(verify))
        {
            return false;
        }

        Result<FrontmatterDocument> split = Frontmatter.Read(await File.ReadAllTextAsync(verify, cancellationToken).ConfigureAwait(false), verify);

        return split.IsSuccess
            && string.Equals(Frontmatter.Key(split.Value.Yaml, "outcome"), "pass", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The last commit on the integration branch that touched a cycle's folder, or null when it has not merged.
    /// </summary>
    /// <remarks>
    /// The remote's branch is asked first, then the local one: a cycle merged on the platform and not yet pulled
    /// is still merged, and one merged locally and not yet pushed is too.
    /// </remarks>
    /// <param name="repository">The repository.</param>
    /// <param name="root">The solution's root.</param>
    /// <param name="integration">The integration branch's name.</param>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The commit, or null.</returns>
    public static string? MergeOf(IRepository repository, string root, string integration, CycleId cycle)
    {
        ArgumentNullException.ThrowIfNull(repository);

        string folder = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, cycle.ToString());

        foreach (string reference in new[] { "origin/" + integration, integration }.Where(name => repository.RefExists(root, name)))
        {
            IReadOnlyList<(string Commit, DateTimeOffset When)> commits = repository.CommitsTouching(root, reference, folder);

            if (commits.Count > 0)
            {
                return commits[^1].Commit;
            }
        }

        return null;
    }

    /// <summary>
    /// The branch an abandoned cycle is read from when its abandonment never reached the integration branch, or null
    /// when it did, or when no branch of the cycle carries one.
    /// </summary>
    /// <remarks>
    /// A local branch is asked before the remote's: it is the person's own, and holds at least what they pushed.
    /// </remarks>
    /// <param name="repository">The repository.</param>
    /// <param name="root">The solution's root.</param>
    /// <param name="integration">The integration branch's name.</param>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The branch, or null.</returns>
    public static string? AbandonedOn(IRepository repository, string root, string integration, CycleId cycle)
    {
        ArgumentNullException.ThrowIfNull(repository);

        return AbandonedOn(repository, root, new Series.SeriesAbandonment(repository, root, integration), cycle);
    }

    /// <summary>
    /// <see cref="AbandonedOn(IRepository, string, string, CycleId)"/> with what the series abandon read once, for a
    /// caller asking it of many cycles.
    /// </summary>
    /// <param name="repository">The repository.</param>
    /// <param name="root">The solution's root.</param>
    /// <param name="series">The series' abandonments, read once for the request.</param>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The branch, or null.</returns>
    internal static string? AbandonedOn(IRepository repository, string root, Series.SeriesAbandonment series, CycleId cycle)
    {
        string abandoned = AbandonedPath(cycle);

        if (series.Integrations.Any(name => repository.PathExistsAt(root, name, abandoned)) || series.AbandonedOnIntegration(cycle))
        {
            return null;
        }

        // A rejected series is abandoned in one step, on its own branch, and never merges: the series branch whose
        // abandonment covers the cycle, when no branch of the cycle's own carries one.
        return repository.Branches(root, CycleId.BranchPrefix + cycle + "/")
            .OrderBy(name => name.StartsWith("origin/", StringComparison.Ordinal))
            .ThenBy(name => name, StringComparer.Ordinal)
            .FirstOrDefault(name => repository.PathExistsAt(root, name, abandoned))
            ?? series.BranchFor(cycle);
    }

    /// <summary>A cycle's abandonment record, relative to the root with forward slashes.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The path.</returns>
    internal static string AbandonedPath(CycleId cycle) =>
        string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, cycle.ToString(), "abandoned.md");
}
