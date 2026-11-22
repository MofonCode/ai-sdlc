using System.Diagnostics.CodeAnalysis;

namespace Sdlc.Core.Cycles;

/// <summary>Which size of run a run branch carries.</summary>
public enum RunKind
{
    /// <summary>Every cycle of one wave, built one after another: <c>wave/&lt;id&gt;</c>.</summary>
    Wave,

    /// <summary>Wave after wave, built one after another: <c>series/&lt;id&gt;</c>.</summary>
    Series,
}

/// <summary>
/// <c>wave/&lt;id&gt;</c> and <c>series/&lt;id&gt;</c>: the one branch a run builds several cycles into, and the one
/// pull request it opens.
/// </summary>
/// <remarks>
/// <para><strong>Several cycles, each still its own.</strong> Every cycle a run builds keeps its folder, rounds,
/// verification and commits; the branch is a grouping, never a merged record. So the pull-request check reads each
/// cycle whose folder the branch changes as it would read it alone, and adds what only a run can break: a later
/// cycle failing a test an earlier one named.</para>
/// <para><strong>A build branch for every cycle it carries.</strong> Where something asks whether the working copy is
/// a cycle's build branch, a run branch answers yes for each cycle in it.</para>
/// <para><strong>Its id has a cycle id's shape</strong> — the moment the run started, to the second — so runs sort
/// by when they started, and allocating one needs no coordination.</para>
/// </remarks>
public static class RunBranch
{
    /// <summary>What every wave's branch begins with.</summary>
    public const string WavePrefix = "wave/";

    /// <summary>What every series' branch begins with.</summary>
    public const string SeriesPrefix = "series/";

    /// <summary>The branch for a run.</summary>
    /// <param name="kind">Which size of run.</param>
    /// <param name="id">The run's id, shaped as a cycle id.</param>
    /// <returns>The branch name.</returns>
    public static string NameOf(RunKind kind, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!CycleId.TryParse(id, out _))
        {
            throw new ArgumentException("A run's id is shaped as a cycle id, as in 20261121_0930_15.", nameof(id));
        }

        return (kind == RunKind.Wave ? WavePrefix : SeriesPrefix) + id.Trim();
    }

    /// <summary>Where a run's worktree goes, beside the repository: <c>../&lt;repo&gt;.waves/&lt;id&gt;</c> or <c>../&lt;repo&gt;.series/&lt;id&gt;</c>.</summary>
    /// <param name="solutionRoot">The repository's root.</param>
    /// <param name="kind">Which size of run.</param>
    /// <param name="id">The run's id.</param>
    /// <returns>The path, forward-slashed, relative to the repository's root.</returns>
    public static string WorktreePath(string solutionRoot, RunKind kind, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(solutionRoot)));
        return "../" + name + (kind == RunKind.Wave ? ".waves/" : ".series/") + id.Trim();
    }

    /// <summary>
    /// The git command that opens a wave's worktree on <c>wave/&lt;id&gt;</c> from the sandbox it was planned in,
    /// <c>sandbox/&lt;id&gt;</c>, or on the wave's branch when it already exists. The wave takes the sandbox's id.
    /// </summary>
    /// <param name="solutionRoot">The repository's root.</param>
    /// <param name="sandbox">The sandbox's first cycle, which names it and the wave.</param>
    /// <param name="exists">Whether <c>wave/&lt;id&gt;</c> already exists.</param>
    /// <returns>The command.</returns>
    public static string OpenWave(string solutionRoot, CycleId sandbox, bool exists)
    {
        string id = sandbox.ToString();
        string branch = NameOf(RunKind.Wave, id);
        string place = WorktreePath(solutionRoot, RunKind.Wave, id);

        return exists
            ? "git worktree add " + place + " " + branch
            : "git worktree add --no-track -b " + branch + " " + place + " " + SandboxBranch.NameOf(sandbox);
    }

    /// <summary>An id for a run starting now.</summary>
    /// <param name="time">The clock to read.</param>
    /// <returns>The id, shaped as a cycle id.</returns>
    public static string Allocate(TimeProvider time) => CycleId.Allocate(time).ToString();

    /// <summary>Reads a run out of a branch name, fully qualified or not.</summary>
    /// <param name="branch">The branch.</param>
    /// <param name="kind">Which size of run, when this returns true.</param>
    /// <param name="id">The run's id, when this returns true.</param>
    /// <returns>True when the branch is <c>wave/&lt;id&gt;</c> or <c>series/&lt;id&gt;</c>.</returns>
    public static bool TryParse(string? branch, out RunKind kind, [NotNullWhen(true)] out string? id)
    {
        kind = RunKind.Wave;
        id = null;
        string trimmed = branch?.Trim() ?? string.Empty;

        const string FullyQualified = "refs/heads/";
        if (trimmed.StartsWith(FullyQualified, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[FullyQualified.Length..];
        }

        string rest;

        if (trimmed.StartsWith(WavePrefix, StringComparison.Ordinal))
        {
            rest = trimmed[WavePrefix.Length..];
        }
        else if (trimmed.StartsWith(SeriesPrefix, StringComparison.Ordinal))
        {
            kind = RunKind.Series;
            rest = trimmed[SeriesPrefix.Length..];
        }
        else
        {
            return false;
        }

        if (!CycleId.TryParse(rest, out CycleId? parsed) || !string.Equals(parsed.Value.ToString(), rest, StringComparison.Ordinal))
        {
            kind = RunKind.Wave;
            return false;
        }

        id = rest;
        return true;
    }

    /// <summary>
    /// The branch work here builds onto when nobody named one: the run's branch checked out here, or null for the
    /// integration branch.
    /// </summary>
    /// <remarks>
    /// A wave-two successor's predecessor passed on the run's branch and has not merged anywhere else yet, so on that
    /// branch readiness is judged against it.
    /// </remarks>
    /// <param name="repository">The repository.</param>
    /// <param name="root">The working copy's root.</param>
    /// <param name="named">The branch a person named with <c>--onto</c>, which wins.</param>
    /// <returns>The branch, or null.</returns>
    internal static string? OntoHere(Git.IRepository repository, string root, string? named)
    {
        if (!string.IsNullOrWhiteSpace(named))
        {
            return named.Trim();
        }

        string? on = repository.IsRepository(root) ? repository.CurrentBranch(root) : null;
        return IsRun(on) ? on!.Trim() : null;
    }

    /// <summary>Whether a branch is a run's branch.</summary>
    /// <param name="branch">The branch.</param>
    /// <returns>True when it is <c>wave/&lt;id&gt;</c> or <c>series/&lt;id&gt;</c>.</returns>
    public static bool IsRun(string? branch) => TryParse(branch, out _, out _);

    /// <summary>
    /// Whether a branch is where a cycle is built: its own <c>cycle/&lt;id&gt;/build</c> (or the unattended one), or a
    /// run's branch, which is a build branch for every cycle it carries.
    /// </summary>
    /// <param name="branch">The branch, as git names what is checked out.</param>
    /// <param name="id">The cycle.</param>
    /// <returns>True when the cycle is built there.</returns>
    public static bool BuildsOn(string? branch, CycleId id) =>
        IsRun(branch) || (CycleBranch.TryParse(branch, out CycleBranch? own) && own.Id == id && own.IsBuild);
}
