using System.Diagnostics.CodeAnalysis;
using Sdlc.Core.Successors;

namespace Sdlc.Core.Cycles;

/// <summary>
/// <c>slices/&lt;slug&gt;/plan</c>: the one branch that admits every plan sliced together (D-125).
/// </summary>
/// <remarks>
/// <para><strong>Composed, never worked on.</strong> The binary builds it from the siblings' plan branches, which
/// touch disjoint cycle folders, so composing them cannot conflict; merging its pull request deletes it.</para>
/// <para><strong>Its name chooses nothing.</strong> The pull-request check reads what it carries (D-87): the name
/// only tells the check that this is a slicing's branch rather than a change with no cycle behind it.</para>
/// </remarks>
public static class SlicingBranch
{
    /// <summary>What every slicing branch begins with.</summary>
    public const string Prefix = "slices/";

    private const string Suffix = "/plan";

    /// <summary>The branch for a slicing.</summary>
    /// <param name="slug">The slicing's slug.</param>
    /// <returns>The branch name.</returns>
    public static string NameOf(string slug) => Prefix + slug + Suffix;

    /// <summary>Reads a slicing's slug out of a branch name, fully qualified or not.</summary>
    /// <param name="branch">The branch.</param>
    /// <param name="slug">The slug, when this returns true.</param>
    /// <returns>True when the branch is <c>slices/&lt;slug&gt;/plan</c>.</returns>
    public static bool TryParse(string? branch, [NotNullWhen(true)] out string? slug)
    {
        slug = null;
        string trimmed = branch?.Trim() ?? string.Empty;

        const string FullyQualified = "refs/heads/";
        if (trimmed.StartsWith(FullyQualified, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[FullyQualified.Length..];
        }

        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal) || !trimmed.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return false;
        }

        string inner = trimmed[Prefix.Length..^Suffix.Length];

        if (!Successor.IsSlug(inner))
        {
            return false;
        }

        slug = inner;
        return true;
    }
}
