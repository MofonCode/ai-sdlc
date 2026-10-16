using System.Diagnostics.CodeAnalysis;

namespace Sdlc.Core.Cycles;

/// <summary>Which kind of work a cycle branch carries.</summary>
/// <remarks>
/// One branch per kind, all under <c>cycle/&lt;id&gt;/</c>, so the three ways a cycle moves can run
/// side by side, the names are a trace of how it went, and everything about one cycle sits in one place.
/// D-85.
/// </remarks>
public enum CycleBranchKind
{
    /// <summary>The bare <c>cycle/&lt;id&gt;</c> an older cycle ran on. Read, never created.</summary>
    Bare,

    /// <summary><c>plan</c>: the planning documents, pull-requested into the integration branch.</summary>
    Plan,

    /// <summary><c>build</c>: a build a person ran by hand.</summary>
    Build,

    /// <summary><c>build-unattended</c>: a build the factory ran in a batch.</summary>
    BuildUnattended,

    /// <summary><c>forward</c>: a release-targeted fix carried into the integration branch.</summary>
    Forward,
}

/// <summary>
/// A cycle branch, read: the cycle it belongs to and the kind of work it carries.
/// </summary>
/// <param name="Id">The cycle.</param>
/// <param name="Kind">The kind of work.</param>
public sealed record CycleBranch(CycleId Id, CycleBranchKind Kind)
{
    /// <summary>The kinds a branch may name, as they are written after the id.</summary>
    public static IReadOnlyList<CycleBranchKind> Named { get; } =
        [CycleBranchKind.Plan, CycleBranchKind.Build, CycleBranchKind.BuildUnattended, CycleBranchKind.Forward];

    /// <summary>Whether this branch carries a build — by hand or unattended.</summary>
    public bool IsBuild => Kind is CycleBranchKind.Build or CycleBranchKind.BuildUnattended;

    /// <summary>The branch's name.</summary>
    /// <returns>The name, as <c>cycle/&lt;id&gt;</c> or <c>cycle/&lt;id&gt;/&lt;kind&gt;</c>.</returns>
    public override string ToString() => Kind == CycleBranchKind.Bare ? Id.BranchName() : Id.BranchName(Kind);

    /// <summary>How a kind is written after the id.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Its name. The bare kind has none.</returns>
    public static string NameOf(CycleBranchKind kind) => kind switch
    {
        CycleBranchKind.Plan => "plan",
        CycleBranchKind.Build => "build",
        CycleBranchKind.BuildUnattended => "build-unattended",
        CycleBranchKind.Forward => "forward",
        CycleBranchKind.Bare => throw new ArgumentOutOfRangeException(
            nameof(kind),
            "The bare branch has no kind after the id."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// Reads a branch name as a cycle branch.
    /// </summary>
    /// <remarks>
    /// <strong>A kind nobody recognises is not a cycle branch.</strong> <c>cycle/&lt;id&gt;/biuld</c> read as
    /// a build would govern a branch by a typo; read as nothing, the pull-request check says there is no
    /// chain and the person sees the name they meant.
    /// </remarks>
    /// <param name="branch">
    /// A branch name as a host or a platform spells it: bare, or a fully qualified <c>refs/heads/</c>
    /// reference. Azure DevOps reports the second and GitHub the first.
    /// </param>
    /// <param name="read">The branch, when this returns true.</param>
    /// <returns>True when the branch is a cycle branch naming a well-formed id and a known kind.</returns>
    public static bool TryParse(string? branch, [NotNullWhen(true)] out CycleBranch? read)
    {
        read = null;
        string trimmed = branch?.Trim() ?? string.Empty;

        const string FullyQualified = "refs/heads/";
        if (trimmed.StartsWith(FullyQualified, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[FullyQualified.Length..];
        }

        if (!trimmed.StartsWith(CycleId.BranchPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rest = trimmed[CycleId.BranchPrefix.Length..];
        int slash = rest.IndexOf('/', StringComparison.Ordinal);
        string idText = slash < 0 ? rest : rest[..slash];

        if (!CycleId.TryParse(idText, out CycleId? id))
        {
            return false;
        }

        if (slash < 0)
        {
            read = new CycleBranch(id.Value, CycleBranchKind.Bare);
            return true;
        }

        string kindText = rest[(slash + 1)..];

        foreach (CycleBranchKind kind in Named)
        {
            if (kindText.Equals(NameOf(kind), StringComparison.OrdinalIgnoreCase))
            {
                read = new CycleBranch(id.Value, kind);
                return true;
            }
        }

        return false;
    }
}
