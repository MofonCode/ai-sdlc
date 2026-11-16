using System.Text.Json;

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

    /// <summary>
    /// Whether the branch a promotion comes from already built and tested exactly what the promotion merges (D-196).
    /// </summary>
    /// <remarks>
    /// <para><strong>Two things, both decided here.</strong> The merge's tree is the source's tip tree, so the default
    /// branch holds no change of its own; and the platform's runs on the source branch hold a green one of that tip.
    /// Then the tests would find nothing new, and a pipeline may skip them. It still builds, so the release is built
    /// with its own version: a package compiled as a preview cannot be renamed into a release.</para>
    /// <para><strong>The binary reads the runs and never fetches them.</strong> It holds no token, so the pipeline asks
    /// its platform and hands the answer over: Azure DevOps' builds list (<c>value[].sourceVersion</c>,
    /// <c>result</c>) or GitHub's workflow runs (<c>workflow_runs[].head_sha</c>, <c>conclusion</c>). Anything it
    /// cannot read is no, and a no runs everything.</para>
    /// </remarks>
    /// <param name="mergeTree">The tree of the commit the promotion's pull request builds.</param>
    /// <param name="sourceTree">The tree of the source branch's tip.</param>
    /// <param name="sourceCommit">The source branch's tip.</param>
    /// <param name="runs">The platform's runs on the source branch, as its API returns them.</param>
    /// <returns>True only when the trees match and a green run of that tip is listed.</returns>
    public static bool AlreadyTested(string? mergeTree, string? sourceTree, string? sourceCommit, string? runs)
    {
        if (string.IsNullOrWhiteSpace(mergeTree) || string.IsNullOrWhiteSpace(sourceCommit) || string.IsNullOrWhiteSpace(runs)
            || !string.Equals(mergeTree.Trim(), sourceTree?.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(runs);

            return Green(document.RootElement, "value", "sourceVersion", "result", "succeeded", sourceCommit.Trim())
                || Green(document.RootElement, "workflow_runs", "head_sha", "conclusion", "success", sourceCommit.Trim());
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool Green(JsonElement root, string list, string commit, string outcome, string passed, string tip) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(list, out JsonElement runs)
        && runs.ValueKind == JsonValueKind.Array
        && runs.EnumerateArray().Any(run =>
            run.ValueKind == JsonValueKind.Object
            && run.TryGetProperty(commit, out JsonElement sha) && sha.ValueKind == JsonValueKind.String
            && string.Equals(sha.GetString(), tip, StringComparison.OrdinalIgnoreCase)
            && run.TryGetProperty(outcome, out JsonElement result) && result.ValueKind == JsonValueKind.String
            && string.Equals(result.GetString(), passed, StringComparison.OrdinalIgnoreCase));

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
