using Sdlc.Core.Constraints;

namespace Sdlc.Tests;

[TestClass]
public sealed class ConstraintRendererTests
{
    private static EffectiveConstraints.Response Set(params Constraint[] constraints) =>
        new(
            constraints,
            [new ConstraintDocument(ConstraintTier.Global, "CONSTRAINTS.md", string.Empty)],
            []);

    private static Constraint Rule(string id, string statement, string appliesTo = "an intent, at approval.")
    {
        Assert.IsTrue(ConstraintId.TryParse(id, out ConstraintId? parsed));
        return new Constraint(
            parsed.Value,
            statement,
            appliesTo,
            "something specific that becomes illegal.",
            "what goes wrong when the rule is absent.");
    }

    [TestMethod]
    public void The_full_rendering_carries_every_clause_because_a_session_judges_against_them()
    {
        string output = ConstraintRenderer.Render(
            Set(Rule("CON_01", "An intent states the problem")),
            ConstraintDetail.Full);

        StringAssert.Contains(output, "CON_01");
        StringAssert.Contains(output, "An intent states the problem");
        StringAssert.Contains(output, "applies to");
        StringAssert.Contains(output, "forbids");
        StringAssert.Contains(output, "why");
    }

    [TestMethod]
    public void The_brief_rendering_drops_the_clauses_and_keeps_the_statements()
    {
        string output = ConstraintRenderer.Render(
            Set(Rule("CON_01", "An intent states the problem")),
            ConstraintDetail.Brief);

        StringAssert.Contains(output, "An intent states the problem");
        Assert.IsFalse(output.Contains("forbids", StringComparison.Ordinal));
    }

    [TestMethod]
    public void The_heading_counts_what_is_in_force()
    {
        string output = ConstraintRenderer.Render(
            Set(Rule("CON_01", "First"), Rule("CON_02", "Second")),
            ConstraintDetail.Brief);

        StringAssert.Contains(output, "Constraints in force: 2");
    }

    [TestMethod]
    public void The_heading_names_the_artifact_when_the_set_was_narrowed()
    {
        string output = ConstraintRenderer.Render(
            Set(Rule("CON_01", "First")),
            ConstraintDetail.Brief,
            "intent");

        StringAssert.Contains(output, "Constraints in force for intent: 1");
    }

    [TestMethod]
    public void An_empty_set_says_nothing_binds_rather_than_printing_a_bare_zero()
    {
        string output = ConstraintRenderer.Render(Set(), ConstraintDetail.Full, "retrospective");

        StringAssert.Contains(output, "Nothing binds this artifact");
    }

    [TestMethod]
    public void The_rendering_names_what_it_read_so_a_reader_can_check_the_set_is_complete()
    {
        string output = ConstraintRenderer.Render(
            Set(Rule("CON_01", "First")),
            ConstraintDetail.Full);

        StringAssert.Contains(output, "Read from");
        StringAssert.Contains(output, "global");
        StringAssert.Contains(output, "CONSTRAINTS.md");
    }

    [TestMethod]
    public void A_superseded_constraint_is_listed_so_a_numbering_gap_has_an_explanation()
    {
        Assert.IsTrue(ConstraintId.TryParse("CON_21", out ConstraintId? replacement));
        Constraint retired = Rule("CON_20", "A retired rule") with { SupersededBy = replacement };

        EffectiveConstraints.Response set = new(
            [Rule("CON_01", "First")],
            [new ConstraintDocument(ConstraintTier.Global, "CONSTRAINTS.md", string.Empty)],
            [retired]);

        string output = ConstraintRenderer.Render(set, ConstraintDetail.Brief);

        StringAssert.Contains(output, "Superseded");
        StringAssert.Contains(output, "CON_20");
        StringAssert.Contains(output, "CON_21");
    }

    [TestMethod]
    public void A_long_clause_wraps_and_every_continuation_lines_up_under_the_first()
    {
        Constraint verbose = new(
            Rule("CON_01", "A rule").Id,
            "A rule",
            "an intent, at approval.",
            string.Join(' ', Enumerable.Repeat("word", 60)),
            "a reason.");

        string output = ConstraintRenderer.Render(Set(verbose), ConstraintDetail.Full);

        string[] lines = output.Split(Environment.NewLine);
        int forbidsAt = Array.FindIndex(lines, line => line.Contains("forbids", StringComparison.Ordinal));

        Assert.IsTrue(forbidsAt >= 0, "The forbids clause was not rendered.");

        int firstTextColumn = lines[forbidsAt].IndexOf("word", StringComparison.Ordinal);
        Assert.IsTrue(firstTextColumn > 0);

        string continuation = lines[forbidsAt + 1];
        Assert.IsTrue(
            continuation.TrimStart().StartsWith("word", StringComparison.Ordinal),
            "The clause should have wrapped onto a second line.");
        Assert.AreEqual(
            firstTextColumn,
            continuation.IndexOf("word", StringComparison.Ordinal),
            "A continuation that does not line up under the first line reads as a separate field.");
    }

    [TestMethod]
    public void No_line_the_renderer_draws_exceeds_the_width_it_wraps_to()
    {
        Constraint verbose = new(
            Rule("CON_01", "A rule").Id,
            "A rule",
            "an intent, at approval.",
            string.Join(' ', Enumerable.Repeat("word", 80)),
            string.Join(' ', Enumerable.Repeat("reason", 40)));

        string output = ConstraintRenderer.Render(Set(verbose), ConstraintDetail.Full);

        foreach (string candidate in output.Split(Environment.NewLine))
        {
            Assert.IsTrue(
                candidate.Length <= 92,
                $"A {candidate.Length}-character line would wrap in a narrow console: '{candidate}'");
        }
    }
}
