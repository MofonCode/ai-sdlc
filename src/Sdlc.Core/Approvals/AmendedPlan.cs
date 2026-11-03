using Sdlc.Core.Cycles;
using Sdlc.Core.Git;

namespace Sdlc.Core.Approvals;

/// <summary>
/// Whether a branch's copy of a cycle's approval predates the one standing on the integration branch (D-123).
/// </summary>
/// <remarks>
/// <para><strong>A plan reopened after admission is amended on its plan branch</strong>, and its pull request
/// carries the new approval to the integration branch. A build made before that still carries the ledger it
/// started from, so the question every reader asks is one comparison: does this branch's ledger hold the
/// approval that now stands? A ledger is append-only, so a branch that merged the amendment holds it, and one
/// that did not, does not.</para>
/// <para><strong>The entry is matched by what it records, not by its bytes.</strong> The decision, who took it
/// and when — the same three a person would read to tell two approvals apart. A ledger that cannot be read
/// answers no: an unreadable file is the gate's to refuse, and this only adds a reason beside it.</para>
/// </remarks>
public static class AmendedPlan
{
    /// <summary>
    /// Where a cycle's ledger sits in the repository, forward-slashed, for reading it at a reference.
    /// </summary>
    /// <param name="id">The cycle.</param>
    /// <returns>The path from the repository's root.</returns>
    public static string LedgerPath(CycleId id) =>
        string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, id.ToString(), "approval.md");

    /// <summary>
    /// The reference the integration branch is read at: the remote's copy when there is one, else the local branch.
    /// </summary>
    /// <param name="repository">Git.</param>
    /// <param name="root">The repository's root.</param>
    /// <param name="integration">The integration branch's name.</param>
    /// <returns>The reference, or null when neither exists.</returns>
    public static string? IntegrationRef(IRepository repository, string root, string integration)
    {
        ArgumentNullException.ThrowIfNull(repository);

        return new[] { "origin/" + integration, integration }.FirstOrDefault(reference => repository.RefExists(root, reference));
    }

    /// <summary>
    /// Whether a branch's ledger lacks the approval standing in another.
    /// </summary>
    /// <param name="branch">The ledger on the branch being asked about, or null when it has none.</param>
    /// <param name="standing">The ledger on the integration branch, or null when it has none.</param>
    /// <returns>
    /// True when the standing ledger's last decision is an approval and the branch's ledger does not hold it.
    /// </returns>
    public static bool Predates(string? branch, string? standing)
    {
        if (branch is null || standing is null)
        {
            return false;
        }

        if (ApprovalLedger.Read(standing, "approval.md") is not { IsSuccess: true } read
            || ApprovalLedger.Read(branch, "approval.md") is not { IsSuccess: true } held
            || !read.Value.IsApproved
            || read.Value.Current is not { } current)
        {
            return false;
        }

        return !held.Value.Decisions.Any(entry =>
            entry.Decision == current.Decision
            && entry.At == current.At
            && string.Equals(entry.By, current.By, StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether the working copy is this cycle's build branch and the integration branch holds an approval it lacks.
    /// </summary>
    /// <param name="repository">Git.</param>
    /// <param name="root">The repository's root.</param>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The integration branch's reference when it does, else null.</returns>
    public static async Task<string?> NewerOn(IRepository repository, string root, Cycle cycle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(cycle);

        if (!CycleBranch.TryParse(repository.CurrentBranch(root), out CycleBranch? branch)
            || branch.Id != cycle.Id
            || !branch.IsBuild
            || !File.Exists(cycle.ApprovalPath))
        {
            return null;
        }

        string integration = await Solutions.SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);

        if (IntegrationRef(repository, root, integration) is not { } reference)
        {
            return null;
        }

        string local = await File.ReadAllTextAsync(cycle.ApprovalPath, cancellationToken).ConfigureAwait(false);

        return Predates(local, repository.ReadAt(root, reference, LedgerPath(cycle.Id))) ? reference : null;
    }

    /// <summary>The two moves a person has with a build made before the amendment, for a reason line.</summary>
    /// <param name="branch">The build branch.</param>
    /// <param name="integration">The integration branch.</param>
    /// <returns>The sentence, with no full stop.</returns>
    public static string Choices(string branch, string integration) =>
        "merge " + integration + " into " + branch + " to continue by hand, or delete " + branch
        + " for the factory to build the amended plan";
}
