using System.Diagnostics.CodeAnalysis;

namespace Sdlc.Core.Cycles;

/// <summary>
/// <c>sandbox/&lt;id&gt;</c>: the branch a person plans several cycles on at once, named for the first (D-144).
/// </summary>
/// <remarks>
/// <para><strong>One worktree, several cycles.</strong> The sandbox opens with one cycle, its primary, whose id names
/// it; every cycle the work turns into — a slicing, an intent cut in two — opens beside it on the same branch, in the
/// same folder, rather than each in a worktree of its own.</para>
/// <para><strong>Worked on, never pull-requested.</strong> What leaves a sandbox is composed from it: <c>sdlc
/// admit</c> takes its approved cycles' folders onto an admission branch the pull-request check already knows, so
/// a cycle still being planned stays behind and nothing new has to be admitted.</para>
/// </remarks>
public static class SandboxBranch
{
    /// <summary>What every sandbox branch begins with.</summary>
    public const string Prefix = "sandbox/";

    /// <summary>The branch for a sandbox.</summary>
    /// <param name="primary">The cycle it opened with.</param>
    /// <returns>The branch name.</returns>
    public static string NameOf(CycleId primary) => Prefix + primary;

    /// <summary>The slug a sandbox's admission branch is named by when it carries several cycles.</summary>
    /// <param name="primary">The cycle it opened with.</param>
    /// <returns>A slug: <c>sandbox-</c> and the id, its underscores as hyphens.</returns>
    public static string SlugOf(CycleId primary) => "sandbox-" + primary.ToString().Replace('_', '-');

    /// <summary>Reads a sandbox's primary cycle out of a branch name, fully qualified or not.</summary>
    /// <param name="branch">The branch.</param>
    /// <param name="primary">The cycle it opened with, when this returns true.</param>
    /// <returns>True when the branch is <c>sandbox/&lt;id&gt;</c>.</returns>
    public static bool TryParse(string? branch, [NotNullWhen(true)] out CycleId? primary)
    {
        primary = null;
        string trimmed = branch?.Trim() ?? string.Empty;

        const string FullyQualified = "refs/heads/";
        if (trimmed.StartsWith(FullyQualified, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[FullyQualified.Length..];
        }

        return trimmed.StartsWith(Prefix, StringComparison.Ordinal)
            && CycleId.TryParse(trimmed[Prefix.Length..], out primary);
    }
}
