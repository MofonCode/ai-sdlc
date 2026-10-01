using FluentResults;
using Sdlc.Core.Constraints;

namespace Sdlc.Tests;

[TestClass]
public sealed class ConstraintParserTests
{
    private const string OneGoodConstraint = """
        # Some library

        ## CON_01 — An intent states the problem, not the solution

        **Applies to:** an intent, at approval.

        **Forbids:** an intent whose body names an implementation and no observable problem.

        **Why:** the spec derives its criteria from the intent.
        """;

    [TestMethod]
    public void A_complete_constraint_parses_with_all_four_parts()
    {
        Result<IReadOnlyList<Constraint>> result = ConstraintParser.Parse(OneGoodConstraint, "test");

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Constraint only = result.Value.Single();
        Assert.AreEqual("CON_01", only.Id.ToString());
        Assert.AreEqual("An intent states the problem, not the solution", only.Statement);
        Assert.AreEqual("an intent, at approval.", only.AppliesTo);
        StringAssert.StartsWith(only.Forbids, "an intent whose body names");
        StringAssert.StartsWith(only.Why, "the spec derives its criteria");
        Assert.IsTrue(only.IsInForce);
    }

    [TestMethod]
    [DataRow("Applies to")]
    [DataRow("Forbids")]
    [DataRow("Why")]
    public void A_constraint_missing_a_clause_is_a_failure_naming_the_clause(string clause)
    {
        string[] lines = OneGoodConstraint.Split('\n');
        string without = string.Join(
            '\n',
            lines.Where(line => !line.StartsWith($"**{clause}:**", StringComparison.Ordinal)));

        Result<IReadOnlyList<Constraint>> result = ConstraintParser.Parse(without, "test");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(
            result.Errors.Any(error => error.Message.Contains(clause, StringComparison.Ordinal)),
            $"No error named the missing '{clause}' clause. Got: {string.Join("; ", result.Errors.Select(e => e.Message))}");
    }

    [TestMethod]
    public void A_failure_names_the_source_so_the_file_can_be_found()
    {
        string without = OneGoodConstraint.Replace("**Why:**", "**Note:**", StringComparison.Ordinal);

        Result<IReadOnlyList<Constraint>> result = ConstraintParser.Parse(without, "some/path.md");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("some/path.md", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Every_malformed_constraint_is_reported_rather_than_only_the_first()
    {
        const string two = """
            ## CON_01 — First

            **Applies to:** an intent.

            **Forbids:** something.

            ## CON_02 — Second

            **Applies to:** a spec.

            **Why:** a reason.
            """;

        Result<IReadOnlyList<Constraint>> result = ConstraintParser.Parse(two, "test");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("CON_01", StringComparison.Ordinal)));
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("CON_02", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void A_reused_id_is_a_failure_because_ids_are_assigned_once()
    {
        string twice = OneGoodConstraint + "\n\n" + OneGoodConstraint[OneGoodConstraint.IndexOf("## ", StringComparison.Ordinal)..];

        Result<IReadOnlyList<Constraint>> result = ConstraintParser.Parse(twice, "test");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("more than once", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void A_clause_wrapping_over_several_lines_is_joined_into_one()
    {
        const string wrapped = """
            ## CON_01 — A rule

            **Applies to:** an intent, at approval.

            **Forbids:** a first line
            a second line
            a third line.

            **Why:** a reason.
            """;

        Result<IReadOnlyList<Constraint>> result = ConstraintParser.Parse(wrapped, "test");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("a first line a second line a third line.", result.Value.Single().Forbids);
    }

    [TestMethod]
    public void A_superseded_constraint_parses_and_does_not_bind()
    {
        string superseded = OneGoodConstraint + "\n\n**Superseded-by:** CON_13\n";

        Result<IReadOnlyList<Constraint>> result = ConstraintParser.Parse(superseded, "test");

        Assert.IsTrue(result.IsSuccess);
        Constraint only = result.Value.Single();
        Assert.IsFalse(only.IsInForce);
        Assert.AreEqual("CON_13", only.SupersededBy?.ToString());
    }

    [TestMethod]
    public void A_document_with_no_constraints_parses_to_nothing_rather_than_failing()
    {
        Result<IReadOnlyList<Constraint>> result =
            ConstraintParser.Parse("# A document\n\nSome prose, no rules.\n", "test");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, result.Value.Count);
    }

    [TestMethod]
    public void A_hyphen_heading_parses_as_well_as_an_em_dash_so_an_author_need_not_hunt_for_one()
    {
        string hyphenated = OneGoodConstraint.Replace(
            "CON_01 — An intent",
            "CON_01 - An intent",
            StringComparison.Ordinal);

        Result<IReadOnlyList<Constraint>> result = ConstraintParser.Parse(hyphenated, "test");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("An intent states the problem, not the solution", result.Value.Single().Statement);
    }
}
