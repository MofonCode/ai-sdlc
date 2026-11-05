using System.Globalization;
using FluentResults;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>Where a cycle's remediation has got to.</summary>
/// <param name="Spent">How many passes have been made.</param>
/// <param name="OpenPerRound">
/// How many blockers stood after each verify round, in round order. The first entry is the verification
/// itself; each one after it is a pass. This is the progress measure, and it is read from the rounds
/// rather than kept anywhere, so it cannot disagree with the history it describes.
/// </param>
/// <param name="OpenBlockers">What still stands, after the latest pass.</param>
/// <param name="Loop">
/// What the verify rounds say about rules going backwards. A regression is a relapse on the verification:
/// a finding answered by one pass and raised again by the next.
/// </param>
public sealed record RemediationState(
    int Spent,
    IReadOnlyList<int> OpenPerRound,
    IReadOnlyList<Finding> OpenBlockers,
    OscillationState Loop)
{
    /// <summary>Whether anything still blocks.</summary>
    public bool Blocked => OpenBlockers.Count > 0;

    /// <summary>
    /// Whether a pass undid what an earlier one achieved.
    /// </summary>
    /// <remarks>
    /// A finding answered and then raised again is the code-side shape of oscillation, and it is the one
    /// signal that stops remediation on the spot rather than after waiting. A loop that is breaking what
    /// it already fixed is not going to finish, and every further pass is spend against a moving target.
    /// Relapses only, not conflict declarations: a declaration is a judgement about the rules, and this
    /// is a measurement of the code.
    /// </remarks>
    public bool Regressed => Loop.Rules.Any(rule => rule.Relapses > 0);

    /// <summary>
    /// Whether the last two passes both failed to reduce what is open.
    /// </summary>
    /// <remarks>
    /// Two rather than one, because a pass that fixes one thing and uncovers another of equal size has
    /// made real progress that the count cannot see. Two in a row is a loop that is not getting anywhere.
    /// </remarks>
    public bool Stalled => Passes().TakeLast(2) is { } last
        && last.Count() == 2
        && last.All(reduced => !reduced);

    /// <summary>
    /// Whether the cycle cannot be made to pass and should stop rather than open a pull request.
    /// </summary>
    /// <remarks>
    /// <para>This is the state the whole unattended half of the design rests on, and the state iteration 3
    /// never reached. A batch is only safe to run unwatched because a cycle that cannot be made to pass
    /// stops instead of opening one.</para>
    /// <para><strong>It is a predicate on progress now, not a count of passes.</strong> A hard three
    /// failed an honest cycle that needed a fourth pass and let a cycle making no progress at all spend
    /// all three. There is deliberately no ceiling behind the predicate (D-66), so termination rests on
    /// it entirely.</para>
    /// </remarks>
    public bool Escalated => Blocked && (Regressed || Stalled);

    /// <summary>Whether each pass reduced what was open, in order.</summary>
    /// <remarks>
    /// A pass is the step from one round to the next, so there is one fewer of these than there are
    /// rounds. The first round is the verification and reduces nothing.
    /// </remarks>
    private IEnumerable<bool> Passes()
    {
        for (int round = 1; round < OpenPerRound.Count; round++)
        {
            yield return OpenPerRound[round] < OpenPerRound[round - 1];
        }
    }
}

/// <summary>
/// Counts remediation passes from the verify document's findings rounds.
/// </summary>
/// <remarks>
/// <strong>A pass is a round.</strong> There is no counter file and no field in the verify document,
/// because a count kept beside the thing it counts is a count that can disagree with it -- and the one
/// that would be trusted is the cheap one to forge. Rounds accumulate and are never rewritten, so
/// counting them is counting what actually happened.
/// <para><strong>The bound is on progress, not on passes.</strong> It was three passes, counted from the
/// rounds. A blind count failed an honest cycle that needed a fourth and spent all three on a cycle
/// making no progress whatever, so what stops the loop now is a pass that undoes earlier work, or two
/// passes in a row that reduce nothing. There is no ceiling behind it (D-66): the predicate is the whole
/// of it, which trades a termination guarantee for a signal that matches what is actually wrong.
/// </para>
/// <para>
/// The paragraph this replaces said the interrogation loop needed no bound because it was "a person
/// deciding to keep reading". That was not true -- the stage skills run it themselves -- and
/// <see cref="Oscillation"/> is the guard it turned out to need.
/// </para>
/// </remarks>
public static class RemediationBudget
{
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

        IReadOnlyList<FindingsRound> rounds = [.. history.Value.OrderBy(round => round.Round)];

        // The first verification is not a remediation pass -- it is the verification. A pass is each
        // attempt to answer what one found, so the count is rounds after the first.
        int spent = Math.Max(0, rounds.Count - 1);

        return Result.Ok(new RemediationState(
            spent,
            FindingsReader.OpenBlockersPerRound(rounds),
            FindingsReader.StandingBlockers(rounds),
            Oscillation.Count(rounds)));
    }

    /// <summary>
    /// What to tell somebody about where remediation stands.
    /// </summary>
    /// <remarks>
    /// The one place this phrase is built. <see cref="PullRequests.PullRequest"/> assembled its own escalation
    /// sentence instead, down to its own pluralisation of "finding" -- two sentences saying the same
    /// thing, and the one a person read was not the one the suite checked.
    /// </remarks>
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
                    $"cleared after {Count(state.Spent, "pass", "passes")}");
        }

        if (!state.Escalated)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{Count(state.OpenBlockers.Count, "finding", "findings")} open, {Count(state.Spent, "pass", "passes")} spent and making progress");
        }

        // Which of the two stopped it, because they call for different things. A regression says the
        // last pass broke what an earlier one fixed, so somebody should look at what it did; a stall
        // says nothing is moving, so somebody should look at whether the finding is answerable at all.
        return state.Regressed
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"escalated: a pass undid earlier work, with {Count(state.OpenBlockers.Count, "finding", "findings")} still open after {Count(state.Spent, "pass", "passes")}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"escalated: the last two passes reduced nothing, with {Count(state.OpenBlockers.Count, "finding", "findings")} still open after {Count(state.Spent, "pass", "passes")}");
    }

    private static string Count(int number, string singular, string plural) => string.Create(
        CultureInfo.InvariantCulture,
        $"{number} {(number == 1 ? singular : plural)}");
}
