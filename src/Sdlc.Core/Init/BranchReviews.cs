using FluentResults;

namespace Sdlc.Core.Init;

/// <summary>
/// Who must approve a pull request into either branch, as the branch policy sets it (D-136).
/// </summary>
/// <remarks>
/// <para><strong>A count, a named approver, or both.</strong> <see cref="Approvals"/> is how many approvals a pull
/// request needs. <see cref="Owners"/> is who must give one of them, whatever the count, so naming an owner needs
/// at least one approval. The default is neither, because nobody can approve their own pull request.</para>
/// <para><strong>Self-approval is not the same on both platforms.</strong> Azure DevOps can let the author's own
/// vote count. GitHub never counts an author's approval, so the nearest thing is a team that may bypass the rules
/// on a pull request, and only on one: a team member merges their own pull request without another approval, and
/// could merge it without the required check too, which no setting separates. <see cref="BypassTeam"/> names that
/// team, and GitHub refuses self-approval without it rather than pretending to grant it.</para>
/// </remarks>
/// <param name="Approvals">How many approvals a pull request needs.</param>
/// <param name="Owners">Who must approve, as users or teams: <c>@name</c> or <c>@org/team</c> on GitHub, a user or
/// group Azure DevOps resolves on Azure DevOps.</param>
/// <param name="SelfApprove">Whether a pull request's author may stand in for its approval.</param>
/// <param name="BypassTeam">On GitHub, the team whose members may merge their own pull requests.</param>
public sealed record BranchReviews(int Approvals, IReadOnlyList<string> Owners, bool SelfApprove, string? BypassTeam)
{
    /// <summary>The default: no approval, nobody named, nobody approving their own.</summary>
    public static readonly BranchReviews None = new(0, [], false, null);

    /// <summary>How many approvals a pull request needs once a named owner is counted: at least one when there is one.</summary>
    public int Required => Owners.Count > 0 ? Math.Max(Approvals, 1) : Approvals;

    /// <summary>The owners as GitHub writes them, each with one leading <c>@</c>.</summary>
    public IReadOnlyList<string> Handles => [.. Owners.Select(o => "@" + o.TrimStart('@'))];

    /// <summary>Whether this asks anything of a reviewer.</summary>
    public bool Any => Required > 0;

    /// <summary>Whether these can be set on a platform, and why not when they cannot.</summary>
    /// <param name="platform">Where the repository is hosted.</param>
    /// <returns>Success, or why not.</returns>
    public Result Check(Platform platform)
    {
        if (Approvals < 0)
        {
            return Result.Fail("--approvals is a count of approvals, so it cannot be below 0.");
        }

        if (SelfApprove && Required == 0)
        {
            return Result.Fail("--self-approve lets an author stand in for an approval, and none is required. Add --approvals or --owner.");
        }

        if (SelfApprove && platform == Platform.GitHub && string.IsNullOrWhiteSpace(BypassTeam))
        {
            return Result.Fail(
                "GitHub never counts an author's approval of their own pull request. The nearest thing is a team that may bypass " +
                "the rules on a pull request, so name it: --self-approve <team>, an organisation team holding only the people " +
                "who may merge their own. A member could also merge without the required check; no setting separates the two.");
        }

        return Result.Ok();
    }
}
