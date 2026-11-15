using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;

namespace Sdlc.Core.Harvest;

/// <summary>
/// Whether a cycle is completed, which is the only kind a harvest reads (D-190).
/// </summary>
/// <remarks>
/// <para><strong>Completed is finished and merged.</strong> Finished: its verify passed, or a person abandoned it.
/// Merged: its folder is on the integration branch. Since D-181 the pull request is where a person accepts a build,
/// so a cycle whose verify passed and whose pull request was closed, or sent back, was never accepted, and its
/// choices wait until it merges. An abandoned cycle's folder reaches the integration branch the same way, which is
/// also what lets a closed cycle's folder be deleted through a pull request.</para>
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
}
