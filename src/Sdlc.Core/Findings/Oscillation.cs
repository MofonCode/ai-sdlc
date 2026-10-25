using System.Globalization;
using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>One rule the interrogation loop keeps coming back to, and how it is known.</summary>
/// <param name="Rule">
/// The rule, as the round that last named it wrote it. Carries the heading, so a reader of a warning
/// meets the rule rather than a number -- the same reason D-63 put it on a citation.
/// </param>
/// <param name="Relapses">
/// How many times this rule's objection was answered and then raised again. Kept apart from
/// <paramref name="Declarations"/> because the two are different facts even though they weigh the same:
/// a relapse is the loop measurably going backwards, which is what a remediation loop needs in order to
/// recognise a regression, and a declaration is two readers' judgement.
/// </param>
/// <param name="Declarations">How many readers named this rule in a conflict.</param>
/// <param name="Rounds">Which rounds contributed a point, in order.</param>
/// <param name="Against">
/// The rules this one is trading with: the others raised or named in the same rounds that gave it
/// points. What the loop is caught between, which is what a refiner needs in order to look for an exit
/// rather than editing the same sentence again.
/// </param>
public sealed record StuckRule(
    Citation Rule,
    int Relapses,
    int Declarations,
    IReadOnlyList<int> Rounds,
    IReadOnlyList<Citation> Against)
{
    /// <summary>
    /// How much corroborated evidence there is that this rule's ground is stuck.
    /// </summary>
    /// <remarks>
    /// A relapse and a declaration weigh the same, because both are two independent readers agreeing
    /// rather than one reader asserting. Derived rather than stored, so the two cannot come to disagree
    /// with their own total.
    /// </remarks>
    public int Points => Relapses + Declarations;

    /// <summary>Whether this rule has enough points to be worth telling the refiner about.</summary>
    public bool Warned => Points >= Oscillation.Warn;

    /// <summary>Whether this rule has enough points that the loop should stop and ask for a person.</summary>
    public bool Halted => Points >= Oscillation.Halt;
}

/// <summary>Where one artifact's interrogation loop stands: converging, warned, or stuck.</summary>
/// <param name="Rules">Every rule with at least one point, most points first.</param>
public sealed record OscillationState(IReadOnlyList<StuckRule> Rules)
{
    /// <summary>Whether the loop should stop and hand back to a person.</summary>
    public bool Halted => Rules.Any(rule => rule.Halted);

    /// <summary>The rules worth telling the refiner about, most points first.</summary>
    public IReadOnlyList<StuckRule> Warnings => [.. Rules.Where(rule => rule.Warned)];
}

/// <summary>
/// Counts the evidence that an interrogation loop is refighting ground it already won.
/// </summary>
/// <remarks>
/// <para><strong>There is no round cap, and this is not one.</strong> A converging interrogation scores
/// zero however long it runs, and eleven rounds has been worth it before now. What is counted is
/// <em>oscillation</em>: a rule whose objection was answered and came back. A cap on rounds would kill
/// the long honest reading and permit the short pathological one, which is the wrong way round.</para>
///
/// <para><strong>A relapse is a sequence, not a tally.</strong> One rule cited in rounds one, three and
/// five for three different mistakes is a sloppy author and the loop is converging fine. One rule cited,
/// <em>answered</em>, and cited again is the loop going backwards. Counting citations would conflate
/// them, so what is counted is the transition `open -> answered -> open`.</para>
///
/// <para><strong>The transition is the rule's or the finding's, never one finding's answer and another's
/// objection.</strong> A rule is answered when a round leaves nothing open under it; a finding is
/// answered when its own id is. A round names only what it raised or moved (D-107), so "leaves" is read
/// from what stands after the round, and an open blocker carried silently keeps its rule open. A round that answers one finding and raises a different one under the same
/// rule is the reader having moved on to the next place the same mistake was made, which is the sloppy
/// author again -- found on a plan whose browser proofs each waited on a response rather than a render, and
/// halted after five rounds that each fixed one proof and found the next.</para>
///
/// <para><strong>Both signals are corroborated rather than asserted, which is what makes them
/// usable.</strong> `answered` is not self-reported: D-49 derives it from a fresh reader declining to
/// raise something, so a relapse is two independent readers, one silent and one not, with nobody able to
/// fabricate either half. A conflict declaration is the same shape -- a reader is never shown the prior
/// rounds, so two readers declaring one conflict are two uninvested processes agreeing with no knowledge
/// of each other. Neither is a number anybody in the loop can move directly.</para>
///
/// <para><strong>Counted per rule, not per artifact.</strong> One rule coming back three times is the
/// same ground refought. Two different rules relapsing once each is two unrelated incomplete fixes, and
/// aggregating them would halt a loop that is converging. Per-rule counting also makes constraint-pair
/// bookkeeping unnecessary: nothing relapses repeatedly on one rule without something else pushing back
/// each time, so the pair is implied and recorded in <see cref="StuckRule.Against"/>.</para>
///
/// <para><strong>No counter file.</strong> The same reason <see cref="RemediationBudget"/> gives: a count
/// kept beside the thing it counts can disagree with it, and the cheap one to forge is the one that gets
/// trusted. The rounds are a chained ledger whose approved prefix is anchored (D-107), so counting them
/// is counting what happened.</para>
///
/// <para><strong>A revision is the same rule.</strong> Points are keyed on tier and number, so `CON_07`
/// and `CON_07.2` are one rule -- which is what §7.4 says a citation resolves to. Keying on the written
/// id would let a library revision silently reset the count mid-cycle.</para>
/// </remarks>
public static class Oscillation
{
    /// <summary>
    /// Points at which the refiner is told it is refighting old ground.
    /// </summary>
    /// <remarks>
    /// Two, because one relapse is an incomplete fix and those are ordinary. The warning is not a
    /// formality: it is what changes what the refiner is told, and extra rounds at the same approach are
    /// just more spend.
    /// </remarks>
    public const int Warn = 2;

    /// <summary>
    /// Points at which the loop stops and a person is asked.
    /// </summary>
    /// <remarks>
    /// Three, which is roughly six rounds of genuine ping-pong. Deliberate room for a loop that
    /// oscillates <em>on its way to</em> converging, which is the normal shape of a refiner finding a
    /// boundary: narrow, overshoot, broaden, overshoot, then find the wording that satisfies both.
    /// Halting at two would kill that one round before it succeeds.
    /// <para>
    /// Constants rather than configuration, for the reason the remediation loop's own bound was a
    /// constant: the number a team would raise it to is the number at which the bound stops meaning
    /// anything.
    /// </para>
    /// </remarks>
    public const int Halt = 3;

    /// <summary>
    /// Reads where one artifact's loop stands.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="kind">The artifact whose rounds to count.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The state, or a failure naming an unreadable round.</returns>
    public static async Task<Result<OscillationState>> Read(
        Cycle cycle,
        ArtifactKind kind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        Result<IReadOnlyList<FindingsRound>> history = await FindingsReader
            .ReadHistory(cycle, kind, cancellationToken)
            .ConfigureAwait(false);

        return history.IsFailed
            ? history.ToResult<OscillationState>()
            : Result.Ok(Count(history.Value));
    }

    /// <summary>
    /// Counts the points in a history.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Read"/> so the counting can be tested on a history built in memory,
    /// without a cycle on disk. The rule it must not get wrong is the ordering: a relapse is only a
    /// relapse when the open citation comes <em>after</em> an answered one, so the rounds are walked in
    /// round order rather than in whatever order the directory listed them.
    /// </remarks>
    /// <param name="rounds">Every round recorded for one artifact.</param>
    /// <returns>The state.</returns>
    public static OscillationState Count(IReadOnlyList<FindingsRound> rounds)
    {
        ArgumentNullException.ThrowIfNull(rounds);

        Dictionary<string, Tally> tallies = new(StringComparer.Ordinal);
        Dictionary<string, Finding> standing = new(StringComparer.Ordinal);

        foreach (FindingsRound round in rounds.OrderBy(round => round.Round))
        {
            foreach (Finding finding in round.Findings)
            {
                standing[finding.Id] = finding;
            }

            // Every rule this round had something to say about, so a rule that scores can name what it
            // is trading with. Conflicts count here too: a conflict names the rules it is between, and
            // each of them is part of what the other is caught against.
            List<Citation> raised =
            [
                .. round.Findings
                    .Where(finding => finding.Severity == Severity.Blocking && finding.Cites is not null)
                    .Select(finding => finding.Cites!),
                .. (round.Conflicts ?? []).SelectMany(conflict => conflict.Between ?? []),
            ];

            // Grouped by rule, because whether a rule was answered is a fact about the whole round: one
            // finding answered beside another still open under the same rule leaves the rule open.
            IEnumerable<IGrouping<string, Finding>> byRule = round.Findings
                .Where(finding => finding.Severity == Severity.Blocking)
                .Select(finding => (Finding: finding, Key: Key(finding.Cites)))
                .Where(pair => pair.Key is not null)
                .GroupBy(pair => pair.Key!, pair => pair.Finding, StringComparer.Ordinal);

            foreach (IGrouping<string, Finding> rule in byRule)
            {
                Tally tally = At(tallies, rule.Key, rule.Last().Cites!);

                List<Finding> open = [.. rule.Where(finding => finding.Disposition == Disposition.Open)];
                List<Finding> answered = [.. rule.Where(finding => finding.Disposition == Disposition.Answered)];

                // Back: the rule went quiet and is raised again, or a finding answered by id is raised
                // again by id. Once per rule per round however many findings came back.
                if (open.Count > 0
                    && (tally.Answered || open.Exists(finding => tally.AnsweredIds.Contains(finding.Id))))
                {
                    tally.Relapses++;
                    tally.Rounds.Add(round.Round);
                    tally.Note(raised, rule.Key);

                    // Disarmed, so the next answered-then-open pair scores again rather than every
                    // later round scoring off one old transition.
                    tally.Answered = false;
                    tally.AnsweredIds.Clear();
                }

                if (answered.Count == 0)
                {
                    continue;
                }

                // Armed. The rules raised in *this* round are what the fix cost, so they are what a
                // later relapse is caught against.
                tally.Note(raised, rule.Key);
                tally.AnsweredIds.UnionWith(answered.Select(finding => finding.Id));

                // Quiet is what the round left standing, not what it mentioned (D-107). A round names
                // only what it raised or moved, so a blocker still open under this rule and simply not
                // mentioned keeps the rule open -- reading mentions would arm the counter on it.
                if (!standing.Values.Any(finding => finding.IsOpenBlocker
                    && string.Equals(Key(finding.Cites), rule.Key, StringComparison.Ordinal)))
                {
                    tally.Answered = true;
                }
            }

            // A declaration is worth what a relapse is worth: both are two readers agreeing. Every rule
            // the conflict names is stuck, so each of them scores -- a conflict between two rules is
            // evidence about both of them and not about one.
            foreach (Conflict conflict in round.Conflicts ?? [])
            {
                foreach (Citation citation in conflict.Between ?? [])
                {
                    if (Key(citation) is not { } key)
                    {
                        continue;
                    }

                    Tally tally = At(tallies, key, citation);
                    tally.Declarations++;
                    tally.Rounds.Add(round.Round);
                    tally.Note(raised, key);
                }
            }
        }

        return new OscillationState(
        [
            .. tallies.Values
                .Where(tally => tally.Points > 0)
                .OrderByDescending(tally => tally.Points)
                .ThenBy(tally => tally.Rule.Id, StringComparer.Ordinal)
                .Select(tally => new StuckRule(
                    tally.Rule,
                    tally.Relapses,
                    tally.Declarations,
                    tally.Rounds,
                    tally.Against)),
        ]);
    }

    /// <summary>
    /// What to tell somebody about where the loop stands, or null while it is converging.
    /// </summary>
    /// <remarks>
    /// <para>The one place this phrasing is built, for the reason <see cref="RemediationBudget.Describe"/>
    /// gives: two sentences saying the same thing means the one a person reads is not the one the suite
    /// checks.</para>
    /// <para><strong>It says what it knows and no more.</strong> The counter cannot tell a genuine
    /// conflict from a refiner making the same mistake repeatedly -- both need a person, so the
    /// distinction changes nothing about what happens, but claiming the former would be asserting
    /// something nobody established.</para>
    /// </remarks>
    /// <param name="state">The state.</param>
    /// <returns>One line, or null when nothing has scored.</returns>
    public static string? Describe(OscillationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Warnings.Count == 0)
        {
            return null;
        }

        StuckRule worst = state.Warnings[0];
        string rounds = string.Join(", ", worst.Rounds.Select(round => string.Create(
            CultureInfo.InvariantCulture,
            $"r{round}")));

        string against = worst.Against.Count == 0
            ? string.Empty
            : string.Create(
                CultureInfo.InvariantCulture,
                $", against {string.Join(" and ", worst.Against.Select(citation => citation.Id))}");

        return worst.Halted
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"stuck on {worst.Rule.Id} -- {worst.Points} times over {rounds}{against}. A person decides what gives.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{worst.Rule.Id} has come back {worst.Points} times, over {rounds}{against}. Look for an exit rather than editing the same words again.");
    }

    /// <summary>
    /// The tier and number a citation resolves to, or null when it resolves to nothing.
    /// </summary>
    /// <remarks>
    /// Without the revision, because §7.4 says a citation resolves to the number and the suffix is
    /// provenance. `CON_07` and `CON_07.2` forbid the same thing, so a library revision mid-cycle must
    /// not reset a count.
    /// </remarks>
    private static string? Key(Citation? citation) =>
        ConstraintId.TryParse(citation?.Id, out ConstraintId? id)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{ConstraintId.PrefixOf(id.Value.Tier)}_{id.Value.Number}")
            : null;

    private static Tally At(Dictionary<string, Tally> tallies, string key, Citation citation)
    {
        if (tallies.TryGetValue(key, out Tally? found))
        {
            // The latest wording wins, so a warning quotes the rule as the round that last cited it read
            // it rather than as the first round did.
            found.Rule = citation;
            return found;
        }

        Tally created = new(citation);
        tallies[key] = created;
        return created;
    }

    /// <summary>One rule's running count, while a history is being walked.</summary>
    private sealed class Tally(Citation rule)
    {
        public Citation Rule { get; set; } = rule;

        public int Relapses { get; set; }

        public int Declarations { get; set; }

        public int Points => Relapses + Declarations;

        /// <summary>Whether a round has raised nothing open against this rule since it last scored.</summary>
        public bool Answered { get; set; }

        /// <summary>The findings answered by id since this rule last scored.</summary>
        public HashSet<string> AnsweredIds { get; } = new(StringComparer.Ordinal);

        public List<int> Rounds { get; } = [];

        public List<Citation> Against { get; } = [];

        /// <summary>Records the other rules in play, without repeating one or naming this rule itself.</summary>
        /// <param name="raised">Every rule the round had something to say about.</param>
        /// <param name="self">This rule's key, so it is not listed as trading with itself.</param>
        public void Note(IEnumerable<Citation> raised, string self)
        {
            foreach (Citation citation in raised)
            {
                if (Key(citation) is not { } key
                    || string.Equals(key, self, StringComparison.Ordinal)
                    || Against.Exists(already => string.Equals(already.Id, citation.Id, StringComparison.Ordinal)))
                {
                    continue;
                }

                Against.Add(citation);
            }
        }
    }
}
