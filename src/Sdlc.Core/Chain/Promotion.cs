namespace Sdlc.Core.Chain;

/// <summary>What a pull request is, by the branch it targets, and whether it may come from where it does.</summary>
public enum PromotionVerdict
{
    /// <summary>It targets the integration branch or a release branch, so the chain is what judges it.</summary>
    NotAPromotion,

    /// <summary>It promotes the integration branch or a release branch into another branch: main, usually.</summary>
    Allowed,

    /// <summary>It targets another branch from somewhere that is neither, so it is refused.</summary>
    Refused,
}

/// <summary>
/// Which branches may open a pull request into the default branch (D-136).
/// </summary>
/// <remarks>
/// <para><strong>Decided by what a pull request targets, not by naming the default branch.</strong> A pipeline on
/// Azure DevOps cannot read which branch is the default, and the default may be <c>main</c> or <c>master</c>.
/// Every cycle's pull request targets the integration branch, and a fix for a release targets its release
/// branch (D-88), so a pull request targeting anything else is a promotion.</para>
/// <para><strong>Only the integration branch and a release branch promote.</strong> Work reaches main through
/// the chain on the integration branch, or as a release cut from it and fixed by cycles of its own. A branch
/// that is neither would put on main code no chain ever judged.</para>
/// </remarks>
public static class Promotion
{
    /// <summary>The prefix a release branch carries (D-88).</summary>
    public const string ReleasePrefix = "release/";

    /// <summary>What a pull request is.</summary>
    /// <param name="source">The branch it comes from, bare or as <c>refs/heads/...</c>.</param>
    /// <param name="target">The branch it targets, bare or as <c>refs/heads/...</c>.</param>
    /// <param name="integration">The solution's integration branch.</param>
    /// <returns>The verdict.</returns>
    public static PromotionVerdict Judge(string source, string target, string integration)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(integration);

        string from = Bare(source);
        string into = Bare(target);

        if (Same(into, integration) || IsRelease(into))
        {
            return PromotionVerdict.NotAPromotion;
        }

        return Same(from, integration) || IsRelease(from) ? PromotionVerdict.Allowed : PromotionVerdict.Refused;
    }

    /// <summary>What to tell the person about a verdict.</summary>
    /// <param name="verdict">The verdict.</param>
    /// <param name="source">The branch the pull request comes from.</param>
    /// <param name="target">The branch it targets.</param>
    /// <param name="integration">The solution's integration branch.</param>
    /// <returns>One sentence.</returns>
    public static string Explain(PromotionVerdict verdict, string source, string target, string integration)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        string from = Bare(source);
        string into = Bare(target);

        return verdict switch
        {
            PromotionVerdict.NotAPromotion =>
                $"{from} into {into} is not a promotion: a pull request into {integration} or a release branch is judged by the chain.",
            PromotionVerdict.Allowed =>
                $"{from} into {into} is a promotion, and {from} may open one.",
            _ =>
                $"{from} may not open a pull request into {into}. Only {integration} and a {ReleasePrefix}* branch promote: anything else would put on {into} code no chain judged. Open the pull request into {integration} as a cycle (D-136).",
        };
    }

    private static bool IsRelease(string branch) =>
        branch.StartsWith(ReleasePrefix, StringComparison.Ordinal) && branch.Length > ReleasePrefix.Length;

    private static bool Same(string branch, string other) =>
        string.Equals(branch, Bare(other), StringComparison.Ordinal);

    private static string Bare(string branch)
    {
        string trimmed = branch.Trim();
        return trimmed.StartsWith("refs/heads/", StringComparison.Ordinal) ? trimmed["refs/heads/".Length..] : trimmed;
    }
}
