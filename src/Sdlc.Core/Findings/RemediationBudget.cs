using System.Globalization;
using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Deviations;

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
/// <param name="Circles">Approved text the cycle's deviations bent three times or more.</param>
/// <param name="RelapsesPerRound">
/// How many relapses the verify rounds had counted by each round, in round order, so a pass that undid earlier
/// work can be placed in the sequence rather than only known to have happened. Null reads every relapse as the
/// latest pass's.
/// </param>
public sealed record RemediationState(
    int Spent,
    IReadOnlyList<int> OpenPerRound,
    IReadOnlyList<Finding> OpenBlockers,
    OscillationState Loop,
    IReadOnlyList<DeviationCircle>? Circles = null,
    IReadOnlyList<int>? RelapsesPerRound = null)
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
    /// How many times the passes stopped converging: a pass that undid earlier work, or two in a row that reduced
    /// nothing. After the first, the very next pass has to reduce what is open; one that does not is the second.
    /// </summary>
    public int Stops => Walk().Stops;

    /// <summary>
    /// Whether remediation has stopped converging once and the pass that answers it is still to come: it may take a
    /// different approach than the approved text described, recorded as a deviation, before it escalates (D-179).
    /// </summary>
    public bool MayDeviate => Blocked && !Circling && Walk() is { Stops: 1, Pending: true };

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
    /// <para><strong>The first stop is not the end (D-179).</strong> When the passes stop converging the build may
    /// take one different approach, recorded as a deviation, and the next pass must then reduce what is open. It
    /// escalates on the second stop, or when a third deviation bends the same approved text.</para>
    /// </remarks>
    public bool Escalated => Blocked && (Stops >= 2 || Circling);

    /// <summary>
    /// Whether three deviations or more bend the same approved text and the verification still blocks.
    /// </summary>
    /// <remarks>
    /// D-65's halt carried to deviations: a third attempt to bend the same text is the loop going round, not
    /// converging. It is not a cap on how many deviations a cycle may record.
    /// </remarks>
    public bool Circling => Circles is { Count: > 0 };

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

    /// <summary>Whether each pass undid earlier work, in order: the relapse count rose over it.</summary>
    private bool RegressedAt(int pass)
    {
        if (RelapsesPerRound is not { } relapses || relapses.Count != OpenPerRound.Count)
        {
            // Without the sequence every relapse is the latest pass's, which is where a fresh one shows.
            return pass == OpenPerRound.Count - 1 && Regressed;
        }

        return relapses[pass] > relapses[pass - 1];
    }

    // The passes in order, counting the times they stopped converging. After the first stop the next pass is the
    // one a deviation answers, and it has to reduce what is open; Pending says it has not been made yet.
    private (int Stops, bool Pending) Walk()
    {
        int stops = 0;
        int flat = 0;
        bool answering = false;
        List<bool> reduced = [.. Passes()];

        for (int index = 0; index < reduced.Count; index++)
        {
            int pass = index + 1;
            bool regressed = RegressedAt(pass);

            if (answering)
            {
                answering = false;
                flat = 0;

                if (regressed || !reduced[index])
                {
                    stops++;
                }

                continue;
            }

            bool stopped = regressed || (!reduced[index] && ++flat >= 2);

            if (reduced[index] && !regressed)
            {
                flat = 0;
            }

            if (stopped)
            {
                stops++;
                flat = 0;
                answering = stops == 1;
            }
        }

        return (stops, answering);
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

        // The relapses counted by each round, so a pass that undid earlier work is placed where it happened.
        List<int> relapses = [.. Enumerable.Range(1, rounds.Count).Select(count => Oscillation.Count([.. rounds.Take(count)]).Rules.Sum(rule => rule.Relapses))];

        return Result.Ok(new RemediationState(
            spent,
            FindingsReader.OpenBlockersPerRound(rounds),
            FindingsReader.StandingBlockers(rounds),
            Oscillation.Count(rounds),
            (await CycleDeviations.Read(cycle, cancellationToken).ConfigureAwait(false)).Circles,
            relapses));
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

        if (state.MayDeviate)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{Count(state.OpenBlockers.Count, "finding", "findings")} open after {Count(state.Spent, "pass", "passes")}, and the passes stopped converging: the next may take a different approach, recorded as a deviation, and must reduce what is open or the cycle escalates");
        }

        if (!state.Escalated)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{Count(state.OpenBlockers.Count, "finding", "findings")} open, {Count(state.Spent, "pass", "passes")} spent and making progress");
        }

        // Which stopped it, because they call for different things. Three deviations at one place say the
        // approved text there is wrong; a second stop says a different approach did not help either, so somebody
        // should look at whether the finding is answerable at all.
        if (state.Circling)
        {
            DeviationCircle circle = state.Circles![0];
            return string.Create(
                CultureInfo.InvariantCulture,
                $"escalated: {string.Join(", ", circle.Ids)} bend the same text in {circle.Document}, with {Count(state.OpenBlockers.Count, "finding", "findings")} still open after {Count(state.Spent, "pass", "passes")}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"escalated: the passes stopped converging again after a different approach, with {Count(state.OpenBlockers.Count, "finding", "findings")} still open after {Count(state.Spent, "pass", "passes")}");
    }

    private static string Count(int number, string singular, string plural) => string.Create(
        CultureInfo.InvariantCulture,
        $"{number} {(number == 1 ? singular : plural)}");
}
