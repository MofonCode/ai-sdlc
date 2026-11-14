using Sdlc.Core.Artifacts;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Cycles;

/// <summary>
/// Whether a cycle's work has reached the integration branch: its verification passes there (D-162).
/// </summary>
/// <remarks>
/// The definition successors already wait on, read where they read it. Git's own merge state cannot say, because a
/// squash merge leaves every cycle branch unmerged as far as git can tell.
/// </remarks>
public static class MergedCycle
{
    /// <summary>The integration branch's reference when the cycle's verification passes there, else null.</summary>
    /// <param name="repository">Git.</param>
    /// <param name="root">The solution's root.</param>
    /// <param name="id">The cycle.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The reference, such as <c>origin/development</c>, or null.</returns>
    public static async Task<string?> On(IRepository repository, string root, CycleId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);

        if (!repository.IsRepository(root))
        {
            return null;
        }

        string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);
        string verify = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, id.ToString(), Cycle.FileNameOf(ArtifactKind.Verify));

        return new[] { "origin/" + integration, integration }
            .Where(name => repository.RefExists(root, name))
            .FirstOrDefault(name => Passed(repository.ReadAt(root, name, verify)));
    }

    /// <summary>Whether a verify document's outcome is a pass.</summary>
    /// <param name="verify">Its text, or null when there is none.</param>
    /// <returns>True for a pass.</returns>
    public static bool Passed(string? verify) =>
        verify is not null
        && Frontmatter.Read(verify, Cycle.FileNameOf(ArtifactKind.Verify)) is { IsSuccess: true } split
        && string.Equals(Frontmatter.Key(split.Value.Yaml, "outcome"), "pass", StringComparison.OrdinalIgnoreCase);
}
