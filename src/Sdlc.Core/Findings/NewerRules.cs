using FluentResults;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>
/// Whether a cycle is held to the shape rules added once rounds began recording their release.
/// </summary>
/// <remarks>
/// <para>Two rules about how a document is written arrived after documents were written the old way: a
/// suggestion taken in only beneath the outcome it sharpens (D-128), and a plan that says which platform
/// assumptions it proved (D-129). Applied to a cycle in flight, either would refuse a document its own
/// approval covers, and the only repair is a reopen.</para>
/// <para>So they hold a cycle whose intent was first interrogated by a release that records itself on a round
/// (D-131) -- every release since these rules -- and a cycle not interrogated at all. A cycle opened before
/// keeps the shape it was approved in.</para>
/// </remarks>
public static class NewerRules
{
    /// <summary>Says whether the newer shape rules hold a cycle.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>True when its intent has no round, or its first round names the release that wrote it.</returns>
    public static async Task<bool> Hold(Cycle cycle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        Result<LedgerOnDisk> ledger = await FindingsReader
            .ReadLedger(cycle, ArtifactKind.Intent, cancellationToken)
            .ConfigureAwait(false);

        return ledger.IsFailed
            || ledger.Value.Ledger.Rounds.Count == 0
            || ledger.Value.Ledger.Rounds[0].Release is not null;
    }

    /// <summary>The release that brought areas: a plan of a cycle opened on it or later names its areas (D-150).</summary>
    public static Version Areas { get; } = new(0, 39, 0);

    /// <summary>
    /// Says whether a cycle was opened on a given release or a later one: its intent has no round yet, or its
    /// first round names a release at least that.
    /// </summary>
    /// <remarks>
    /// A rule about how a document is written holds the cycles opened since it, never one in flight, for the
    /// reason <see cref="Hold"/> gives. A pre-release suffix is read as the release it precedes.
    /// </remarks>
    /// <param name="cycle">The cycle.</param>
    /// <param name="release">The release the rule arrived in.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>True when the cycle is held to it.</returns>
    public static async Task<bool> Since(Cycle cycle, Version release, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(release);

        Result<LedgerOnDisk> ledger = await FindingsReader
            .ReadLedger(cycle, ArtifactKind.Intent, cancellationToken)
            .ConfigureAwait(false);

        if (ledger.IsFailed || ledger.Value.Ledger.Rounds.Count == 0)
        {
            return true;
        }

        string? first = ledger.Value.Ledger.Rounds[0].Release;
        string numeric = first?.Split('-', '+')[0] ?? string.Empty;

        return Version.TryParse(numeric, out Version? opened) && opened >= release;
    }
}
