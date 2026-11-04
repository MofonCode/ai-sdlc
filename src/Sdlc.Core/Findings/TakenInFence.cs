using FluentResults;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>
/// Whether a cycle's intent is held to taking a suggestion in only beneath the outcome it sharpens (D-128).
/// </summary>
/// <remarks>
/// A rule about how an intent is written, added after intents were written to the old shape. Applied to a
/// cycle in flight it would refuse an intent its own approval covers, and the only repair is a reopen. So it
/// holds a cycle whose intent was first interrogated by a release that records itself on a round (D-131) --
/// every release since the fence -- and a cycle not yet interrogated at all. A cycle opened before keeps the
/// shape it was approved in.
/// </remarks>
public static class TakenInFence
{
    /// <summary>Says whether the fence holds a cycle.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>True when its intent has no round, or its first round names the release that wrote it.</returns>
    public static async Task<bool> Holds(Cycle cycle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        Result<LedgerOnDisk> ledger = await FindingsReader
            .ReadLedger(cycle, ArtifactKind.Intent, cancellationToken)
            .ConfigureAwait(false);

        return ledger.IsFailed
            || ledger.Value.Ledger.Rounds.Count == 0
            || ledger.Value.Ledger.Rounds[0].Release is not null;
    }
}
