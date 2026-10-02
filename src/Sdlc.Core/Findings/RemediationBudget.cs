using System.Globalization;
using FluentResults;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>Where a cycle's remediation has got to.</summary>
/// <param name="Spent">How many passes have been made.</param>
/// <param name="Limit">How many are allowed.</param>
/// <param name="OpenBlockers">What still stands, after the latest pass.</param>
public sealed record RemediationState(int Spent, int Limit, IReadOnlyList<Finding> OpenBlockers)
{
    /// <summary>Whether anything still blocks.</summary>
    public bool Blocked => OpenBlockers.Count > 0;

    /// <summary>How many passes remain.</summary>
    public int Remaining => Math.Max(0, Limit - Spent);

    /// <summary>
    /// Whether the budget is spent and the cycle still does not pass.
    /// </summary>
    /// <remarks>
    /// This is the state the whole unattended half of the design rests on, and the state iteration 3 never
    /// reached. A batch is only safe to run unwatched because a cycle that cannot be made to pass stops
    /// instead of landing.
    /// </remarks>
    public bool Escalated => Blocked && Spent >= Limit;
}

/// <summary>
/// Counts remediation passes from the verify document's findings rounds.
/// </summary>
/// <remarks>
/// <strong>A pass is a round.</strong> There is no counter file and no field in the verify document,
/// because a count kept beside the thing it counts is a count that can disagree with it -- and the one
/// that would be trusted is the cheap one to forge. Rounds accumulate and are never rewritten, so
/// counting them is counting what actually happened.
/// <para>
/// The cap is on code and not on artifacts, deliberately. An unbounded fix-verify loop burns tokens
/// unwatched against a build that may never go green; an unbounded interrogation loop is a person
/// deciding to keep reading, which has been worth eleven rounds before now.
/// </para>
/// </remarks>
public static class RemediationBudget
{
    /// <summary>
    /// How many passes a cycle gets before it escalates.
    /// </summary>
    /// <remarks>
    /// Three, which is a judgement rather than a measurement: enough for a missed edge case, a wrong
    /// assertion, and one surprise, and few enough that a loop going nowhere stops while somebody could
    /// still care. It is a constant rather than configurable because the number a team would raise it to is
    /// the number at which the bound stops meaning anything.
    /// </remarks>
    public const int Limit = 3;

    /// <summary>
    /// Reads where a cycle's remediation stands.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The state, or a failure naming an unreadable round.</returns>
    public static async Task<Result<RemediationState>> Read(
        Cycle cycle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        Result<IReadOnlyList<FindingsRound>> history = await FindingsReader
            .ReadHistory(cycle, ArtifactKind.Verify, cancellationToken)
            .ConfigureAwait(false);

        if (history.IsFailed)
        {
            return history.ToResult<RemediationState>();
        }

        IReadOnlyList<FindingsRound> rounds = history.Value;

        // The first verification is not a remediation pass -- it is the verification. A pass is each
        // attempt to answer what one found, so the count is rounds after the first.
        int spent = Math.Max(0, rounds.Count - 1);

        return Result.Ok(new RemediationState(
            spent,
            Limit,
            FindingsReader.StandingBlockers(rounds)));
    }

    /// <summary>
    /// What to tell somebody about where remediation stands.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns>One line.</returns>
    public static string Describe(RemediationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.Blocked)
        {
            return state.Spent == 0
                ? "nothing to remediate"
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"cleared after {Passes(state.Spent)}");
        }

        return state.Escalated
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"escalated: {Passes(state.Spent)} spent and {state.OpenBlockers.Count} still open")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{state.OpenBlockers.Count} open, {Passes(state.Spent)} spent of {state.Limit}");
    }

    private static string Passes(int number) => number == 1
        ? "1 pass"
        : string.Create(CultureInfo.InvariantCulture, $"{number} passes");
}
