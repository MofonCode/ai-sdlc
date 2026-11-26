using System.Globalization;
using System.Text;
using FluentResults;
using Sdlc.Core.Chain;

namespace Sdlc.Core.Git;

/// <summary>Where a branch about to open a pull request stands against the target as the remote has it.</summary>
public enum Freshness
{
    /// <summary>The branch holds the target's head: nothing the target took is missing from it.</summary>
    UpToDate,

    /// <summary>The target has commits the branch does not.</summary>
    Behind,

    /// <summary>The target could not be fetched, so nobody knows; never read as up to date.</summary>
    Unchecked,
}

/// <summary>What <see cref="TargetFreshness.Check"/> found.</summary>
/// <param name="Standing">Up to date, behind, or not known.</param>
/// <param name="Branch">The branch checked out, or <c>HEAD</c> when none is.</param>
/// <param name="Target">The branch the pull request goes into, bare.</param>
/// <param name="TargetCommit">The target's head as the remote has it, or null when it could not be fetched.</param>
/// <param name="Behind">How many commits the target has that the branch does not; null when git could not count them.</param>
/// <param name="RecordsOnly">
/// Whether everything the branch changes is under <c>.sdlc/</c>, so bringing the target in rebuilds nothing; false
/// for a branch carrying built code, whose verification judged a base without the target's newer commits.
/// </param>
/// <param name="Conflicts">The paths a merge of the target would stop on; none when it would be clean, or was not tried.</param>
/// <param name="Why">What git said when the fetch failed.</param>
public sealed record FreshnessCheck(
    Freshness Standing,
    string Branch,
    string Target,
    string? TargetCommit,
    int? Behind,
    bool RecordsOnly,
    IReadOnlyList<string> Conflicts,
    string? Why);

/// <summary>
/// Whether a branch has taken the latest of the branch its pull request goes into, before it is opened (D-227).
/// </summary>
/// <remarks>
/// <para><strong>Fetched, never assumed.</strong> A local copy of the target is as old as the last fetch, and a
/// branch judged against it would read as current while the remote moved on. The fetch names its refspec, so a
/// clone that follows one branch alone still learns the target's head.</para>
/// <para><strong>A fetch that fails refuses.</strong> Offline, or with no remote, nothing was compared; passing
/// would be the guess the check exists to stop.</para>
/// <para><strong>Ancestry, not equality.</strong> The branch is up to date when the target's head is in its history;
/// it may hold any number of commits of its own.</para>
/// </remarks>
public static class TargetFreshness
{
    private const string Heads = "refs/heads/";
    private const string Origin = "origin/";

    /// <summary>Fetches the target and compares what is checked out with it.</summary>
    /// <param name="repository">Git.</param>
    /// <param name="root">The working copy to ask from.</param>
    /// <param name="target">The branch the pull request goes into: bare, <c>refs/heads/...</c> or <c>origin/...</c>.</param>
    /// <returns>Where the branch stands.</returns>
    public static FreshnessCheck Check(IRepository repository, string root, string target)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        string bare = Bare(target);
        string tracking = "refs/remotes/origin/" + bare;
        string branch = repository.CurrentBranch(root) ?? "HEAD";

        Result<string> fetched = repository.Git(root, $"fetch --quiet origin \"+{Heads}{bare}:{tracking}\"");

        if (fetched.IsFailed)
        {
            string why = string.Join(" ", fetched.Errors.Select(error => error.Message)).Trim();
            return new FreshnessCheck(Freshness.Unchecked, branch, bare, null, null, false, [], why.Length > 0 ? why : "git gave no reason");
        }

        if (repository.CommitOf(root, tracking) is not { } head)
        {
            return new FreshnessCheck(Freshness.Unchecked, branch, bare, null, null, false, [], $"origin has no branch {bare}");
        }

        if (repository.IsAncestor(root, head, "HEAD"))
        {
            return new FreshnessCheck(Freshness.UpToDate, branch, bare, head, 0, false, [], null);
        }

        int? behind = repository.Git(root, $"rev-list --count \"HEAD..{tracking}\"") is { IsSuccess: true } counted
            && int.TryParse(counted.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int count)
                ? count
                : null;

        string? divergence = repository.Git(root, $"merge-base HEAD \"{tracking}\"") is { IsSuccess: true } found
            ? found.Value.Trim()
            : null;
        IReadOnlyList<string>? changed = divergence is { Length: > 0 } ? repository.CommittedChanges(root, divergence, "HEAD") : null;
        bool recordsOnly = changed is not null && (changed.Count == 0 || VerifyChain.RecordsOnly(changed));

        IReadOnlyList<string> conflicts = repository.MergeTree(root, "HEAD", head) is { } merged ? merged.Conflicts : [];

        return new FreshnessCheck(Freshness.Behind, branch, bare, head, behind, recordsOnly, conflicts, null);
    }

    /// <summary>What to tell the person: one paragraph, and the commands that bring the branch up to date.</summary>
    /// <param name="check">What <see cref="Check"/> found.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(FreshnessCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);

        var text = new StringBuilder();

        switch (check.Standing)
        {
            case Freshness.UpToDate:
                text.Append(CultureInfo.InvariantCulture, $"{check.Branch} has everything {check.Target} has on origin, at {Short(check.TargetCommit)}: a pull request from it is checked against the {check.Target} it was built on.\n");
                return text.ToString();

            case Freshness.Unchecked:
                text.Append(CultureInfo.InvariantCulture, $"{check.Branch} may not open a pull request yet: {check.Target} could not be fetched from origin, so nobody knows whether the branch is behind it (D-227). git said: {check.Why}\n\n");
                text.Append(CultureInfo.InvariantCulture, $"Once the remote answers, fetch it and ask again:\n\n    git fetch origin {check.Target}\n    sdlc up-to-date\n");
                return text.ToString();

            default:
                string commits = check.Behind is { } count
                    ? string.Create(CultureInfo.InvariantCulture, $"{count} commit{(count == 1 ? string.Empty : "s")}")
                    : "commits";
                text.Append(CultureInfo.InvariantCulture, $"{check.Branch} may not open a pull request yet: it is {commits} behind {check.Target} on origin, so it would be reviewed and checked without what {check.Target} took since (D-227).\n\n");

                if (check.RecordsOnly)
                {
                    text.Append(CultureInfo.InvariantCulture, $"It changes only the record under .sdlc/, so nothing is built again. Bring {check.Target} in, then run the pull request's checks again:\n\n    git merge origin/{check.Target}\n    sdlc up-to-date\n\n");
                    text.Append(CultureInfo.InvariantCulture, $"Merge, never rebase: the branch may already be pushed, and the pull request is squashed when it lands, so a merge rewrites nothing.\n");
                }
                else
                {
                    text.Append(CultureInfo.InvariantCulture, $"It carries built code, so bringing {check.Target} in changes the code its verification judged, and that verification goes with it. The cycle is brought up to date, built and verified again in a fresh process: /sdlc:remediate <id>, whose first pass merges {check.Target} in (resolve-conflict). On a run's branch, remediate the run's last cycle: its fresh run is the one the run's check reads.\n");
                }

                text.Append('\n');
                text.Append(check.Conflicts.Count == 0
                    ? "The merge is clean.\n"
                    : string.Create(CultureInfo.InvariantCulture, $"The merge stops on: {string.Join(", ", check.Conflicts)}. Settle each inside the files the cycle's plan declares; one outside them is a change of plan, not a merge.\n"));
                return text.ToString();
        }
    }

    private static string Bare(string target)
    {
        string bare = target.Trim();

        if (bare.StartsWith(Heads, StringComparison.Ordinal))
        {
            bare = bare[Heads.Length..];
        }

        return bare.StartsWith(Origin, StringComparison.Ordinal) ? bare[Origin.Length..] : bare;
    }

    private static string Short(string? commit) =>
        commit is { Length: > 7 } ? commit[..7] : commit ?? "an unknown commit";
}
