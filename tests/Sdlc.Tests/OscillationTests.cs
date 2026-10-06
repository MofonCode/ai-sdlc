using Sdlc.Core.Findings;

namespace Sdlc.Tests;

/// <summary>
/// What the loop guard counts, and what it deliberately does not.
/// </summary>
/// <remarks>
/// The distinction every test here turns on: a rule cited three times for three different mistakes is
/// a sloppy author and the loop is converging, while a rule cited, answered, and cited again is the
/// loop going backwards. Counting citations would conflate them, and conflating them would halt the
/// honest case and permit the pathological one.
/// </remarks>
[TestClass]
public sealed class OscillationTests
{
    [TestMethod]
    public void A_rule_cited_many_times_without_ever_being_answered_scores_nothing()
    {
        // The eleven-round interrogation. Each round finds something new about the same rule, nothing
        // comes back, and the loop is working -- so there is nothing to report however long it runs.
        OscillationState state = Oscillation.Count(
        [
            Round(1, Blocker("F_01", "CON_05", Disposition.Open)),
            Round(2, Blocker("F_02", "CON_05", Disposition.Open)),
            Round(3, Blocker("F_03", "CON_05", Disposition.Open)),
        ]);

        Assert.AreEqual(0, state.Rules.Count);
        Assert.IsFalse(state.Halted);
        Assert.IsNull(Oscillation.Describe(state), "Nothing to say about a loop that is converging.");
    }

    [TestMethod]
    public void A_rule_answered_and_then_cited_again_is_one_relapse()
    {
        OscillationState state = Oscillation.Count(
        [
            Round(1, Blocker("F_01", "CON_05", Disposition.Open)),
            Round(2, Blocker("F_01", "CON_05", Disposition.Answered)),
            Round(3, Blocker("F_02", "CON_05", Disposition.Open)),
        ]);

        StuckRule rule = state.Rules.Single();

        Assert.AreEqual(1, rule.Points);
        Assert.IsFalse(rule.Warned, "One relapse is an incomplete fix, and those are ordinary.");
        Assert.AreEqual(3, rule.Rounds.Single());
    }

    [TestMethod]
    public void A_relapse_is_counted_even_when_the_finding_id_is_new()
    {
        // The signal is the cited rule, not the finding id. Id reuse across rounds is the skill's
        // judgement and therefore soft; the citation is structured data since D-63. A fresh reader that
        // raises the same objection under a new id must still register.
        OscillationState state = Oscillation.Count(
        [
            Round(1, Blocker("F_01", "CON_05", Disposition.Open)),
            Round(2, Blocker("F_01", "CON_05", Disposition.Answered)),
            Round(3, Blocker("F_99", "CON_05", Disposition.Open)),
        ]);

        Assert.AreEqual(1, state.Rules.Single().Points);
    }

    [TestMethod]
    public void A_revision_of_the_same_rule_counts_as_the_same_rule()
    {
        // Section 7.4: a citation resolves to the number and the suffix is provenance. A library
        // revision mid-cycle must not reset a count, which keying on the written id would do.
        OscillationState state = Oscillation.Count(
        [
            Round(1, Blocker("F_01", "CON_07", Disposition.Open)),
            Round(2, Blocker("F_01", "CON_07", Disposition.Answered)),
            Round(3, Blocker("F_02", "CON_07.2", Disposition.Open)),
        ]);

        Assert.AreEqual(1, state.Rules.Count);
        Assert.AreEqual(1, state.Rules[0].Points);
    }

    [TestMethod]
    public void Two_relapses_warn_and_three_halt()
    {
        OscillationState warned = Oscillation.Count(
        [
            Round(1, Blocker("F_01", "CON_05", Disposition.Open)),
            Round(2, Blocker("F_01", "CON_05", Disposition.Answered)),
            Round(3, Blocker("F_02", "CON_05", Disposition.Open)),
            Round(4, Blocker("F_02", "CON_05", Disposition.Answered)),
            Round(5, Blocker("F_03", "CON_05", Disposition.Open)),
        ]);

        Assert.AreEqual(2, warned.Rules.Single().Points);
        Assert.IsTrue(warned.Rules[0].Warned);
        Assert.IsFalse(warned.Halted, "Two is room to converge, not a halt.");
        StringAssert.Contains(Oscillation.Describe(warned)!, "Look for an exit");

        OscillationState halted = Oscillation.Count(
        [
            .. new[]
            {
                Round(1, Blocker("F_01", "CON_05", Disposition.Open)),
                Round(2, Blocker("F_01", "CON_05", Disposition.Answered)),
                Round(3, Blocker("F_02", "CON_05", Disposition.Open)),
                Round(4, Blocker("F_02", "CON_05", Disposition.Answered)),
                Round(5, Blocker("F_03", "CON_05", Disposition.Open)),
                Round(6, Blocker("F_03", "CON_05", Disposition.Answered)),
                Round(7, Blocker("F_04", "CON_05", Disposition.Open)),
            },
        ]);

        Assert.AreEqual(3, halted.Rules.Single().Points);
        Assert.IsTrue(halted.Halted);
        StringAssert.Contains(Oscillation.Describe(halted)!, "A person decides what gives.");
    }

    [TestMethod]
    public void One_answered_transition_does_not_score_again_on_every_later_round()
    {
        // Disarming matters. Without it, a rule answered once and then open in rounds three, four and
        // five would halt on one transition -- a loop that is simply slow to finish one fix reported as
        // a loop going in circles.
        OscillationState state = Oscillation.Count(
        [
            Round(1, Blocker("F_01", "CON_05", Disposition.Open)),
            Round(2, Blocker("F_01", "CON_05", Disposition.Answered)),
            Round(3, Blocker("F_02", "CON_05", Disposition.Open)),
            Round(4, Blocker("F_02", "CON_05", Disposition.Open)),
            Round(5, Blocker("F_02", "CON_05", Disposition.Open)),
        ]);

        Assert.AreEqual(1, state.Rules.Single().Points);
        Assert.IsFalse(state.Halted);
    }

    [TestMethod]
    public void Two_different_rules_relapsing_once_each_do_not_aggregate_into_a_halt()
    {
        // Why this counts per rule and not per artifact. These are two unrelated incomplete fixes, and
        // aggregating them would stop a loop that is converging on both.
        OscillationState state = Oscillation.Count(
        [
            Round(1, Blocker("F_01", "CON_05", Disposition.Open), Blocker("F_02", "CON_12", Disposition.Open)),
            Round(2, Blocker("F_01", "CON_05", Disposition.Answered), Blocker("F_02", "CON_12", Disposition.Answered)),
            Round(3, Blocker("F_03", "CON_05", Disposition.Open), Blocker("F_04", "CON_12", Disposition.Open)),
        ]);

        Assert.AreEqual(2, state.Rules.Count);
        Assert.IsTrue(state.Rules.All(rule => rule.Points == 1));
        Assert.IsFalse(state.Halted);
        Assert.AreEqual(0, state.Warnings.Count);
    }

    [TestMethod]
    public void A_declared_conflict_scores_against_every_rule_it_names()
    {
        // A conflict between two rules is evidence about both of them, not about one. And one
        // declaration only warns -- it is an assertion until a second, independent reader agrees.
        OscillationState state = Oscillation.Count([Conflicting(1, "CON_05", "CON_16")]);

        Assert.AreEqual(2, state.Rules.Count);
        Assert.IsTrue(state.Rules.All(rule => rule.Points == 1));
        Assert.IsFalse(state.Halted, "One declaration is one reader's claim.");
    }

    [TestMethod]
    public void Three_readers_declaring_the_same_conflict_halt_the_loop()
    {
        // The corroboration argument. A reader is never shown the prior rounds, so these are three
        // uninvested processes agreeing with no knowledge of each other -- which is why a declaration is
        // allowed to count at all, and why one of them is not enough.
        OscillationState state = Oscillation.Count(
        [
            Conflicting(1, "CON_05", "CON_16"),
            Conflicting(2, "CON_05", "CON_16"),
            Conflicting(3, "CON_05", "CON_16"),
        ]);

        Assert.IsTrue(state.Halted);
        Assert.AreEqual(3, state.Rules[0].Points);
    }

    [TestMethod]
    public void A_relapse_and_a_declaration_add_up()
    {
        // They are weighted the same because both are corroborated rather than asserted. A loop that
        // refought the ground once and then had a reader name the conflict has two pieces of evidence,
        // not one of each kind to be counted separately.
        OscillationState state = Oscillation.Count(
        [
            Round(1, Blocker("F_01", "CON_05", Disposition.Open)),
            Round(2, Blocker("F_01", "CON_05", Disposition.Answered)),
            Round(3, Blocker("F_02", "CON_05", Disposition.Open)),
            Conflicting(4, "CON_05", "CON_16"),
        ]);

        StuckRule stuck = state.Rules.First(rule => rule.Rule.Id == "CON_05");

        Assert.AreEqual(2, stuck.Points);
        Assert.IsTrue(stuck.Warned);
    }

    [TestMethod]
    public void A_warning_names_what_the_rule_is_trading_with()
    {
        // The whole value of the extra room. The refiner is handed findings and no history, so it cannot
        // know it is refighting old ground -- and more laps at the same approach is just more spend. The
        // pair is what it needs in order to look for a wording that satisfies both.
        OscillationState state = Oscillation.Count(
        [
            Round(1, Blocker("F_01", "CON_05", Disposition.Open)),
            Round(2, Blocker("F_01", "CON_05", Disposition.Answered), Blocker("F_02", "CON_16", Disposition.Open)),
            Round(3, Blocker("F_03", "CON_05", Disposition.Open)),
            Round(4, Blocker("F_03", "CON_05", Disposition.Answered), Blocker("F_04", "CON_16", Disposition.Open)),
            Round(5, Blocker("F_05", "CON_05", Disposition.Open)),
        ]);

        StuckRule stuck = state.Rules.First(rule => rule.Rule.Id == "CON_05");

        Assert.IsTrue(stuck.Warned);
        Assert.IsTrue(
            stuck.Against.Any(citation => citation.Id == "CON_16"),
            "The rule it is caught against, which is what a refiner needs to find an exit.");
        Assert.IsFalse(
            stuck.Against.Any(citation => citation.Id == "CON_05"),
            "And not itself.");

        StringAssert.Contains(Oscillation.Describe(state)!, "against CON_16");
    }

    [TestMethod]
    public void A_suggestion_that_comes_back_scores_nothing()
    {
        // Only blockers stop a loop, so only blockers can keep one going round. A suggestion nobody is
        // obliged to act on reappearing is not the loop failing to converge.
        OscillationState state = Oscillation.Count(
        [
            new FindingsRound(null, null, null, null, null, 1,
                [new Finding("F_01", Severity.Suggestion, Disposition.Open, Cite("CON_05"), "x.")]),
            new FindingsRound(null, null, null, null, null, 2,
                [new Finding("F_01", Severity.Suggestion, Disposition.Answered, Cite("CON_05"), "x.")]),
            new FindingsRound(null, null, null, null, null, 3,
                [new Finding("F_02", Severity.Suggestion, Disposition.Open, Cite("CON_05"), "x.")]),
        ]);

        Assert.AreEqual(0, state.Rules.Count);
    }

    [TestMethod]
    public void Rounds_are_walked_in_round_order_rather_than_the_order_given()
    {
        // A relapse is only a relapse when the open citation comes after the answered one. Reading the
        // directory listing's order would make the sequence a coin toss.
        OscillationState ordered = Oscillation.Count(
        [
            Round(3, Blocker("F_02", "CON_05", Disposition.Open)),
            Round(1, Blocker("F_01", "CON_05", Disposition.Open)),
            Round(2, Blocker("F_01", "CON_05", Disposition.Answered)),
        ]);

        Assert.AreEqual(1, ordered.Rules.Single().Points);
    }

    private static Citation Cite(string id) => new(
        id,
        "the rule " + id + " states, in one line");

    private static Finding Blocker(string id, string cites, Disposition disposition) =>
        new(id, Severity.Blocking, disposition, Cite(cites), "something that must be answered.");

    private static FindingsRound Round(int number, params Finding[] findings) =>
        new(null, null, null, null, null, number, findings);

    private static FindingsRound Conflicting(int number, params string[] between) =>
        new(
            null,
            null,
            null,
            null,
            null,
            number,
            [],
            [new Conflict([.. between.Select(Cite)], "no wording satisfies both.")]);
}
