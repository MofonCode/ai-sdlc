using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// What a release ships that a declared stack does not mention (D-93).
/// </summary>
/// <remarks>
/// The property worth holding is the difference between declined and never asked. A report that could
/// not tell them apart would offer every row a team had ever cut, at every release, and be ignored.
/// </remarks>
[TestClass]
public sealed class StackCoverageTests
{
    [TestMethod]
    public void The_default_draft_mentions_everything_this_release_ships()
    {
        // The draft is where a stack starts, so a solution that renamed it untouched has been asked
        // about everything. If this fails, a row or rule was added to one and not the other.
        StackCoverage.Unmentioned unmentioned = StackCoverage.Of(Templates.TechStack());

        Assert.IsTrue(unmentioned.IsEmpty, string.Join(", ", unmentioned.Names));
    }

    [TestMethod]
    public void A_default_with_no_row_is_named_with_the_profiles_choice()
    {
        string stack = Without(Templates.TechStack(), "| accessibility testing |");

        StackCoverage.Unmentioned unmentioned = StackCoverage.Of(stack);

        StackDefault row = unmentioned.Defaults.Single();
        Assert.AreEqual("accessibility testing", row.Concern);
        StringAssert.Contains(row.Choice, "axe-core");
        Assert.AreEqual(0, unmentioned.Rules.Count);
    }

    [TestMethod]
    public void A_row_that_says_none_is_an_answer_and_is_not_offered_again()
    {
        string stack = Templates.TechStack().Replace(
            "| ORM | EF Core |",
            "| ORM | none |",
            StringComparison.Ordinal);

        Assert.IsTrue(StackCoverage.Of(stack).IsEmpty);
    }

    [TestMethod]
    public void A_concern_is_matched_whatever_its_case()
    {
        string stack = Templates.TechStack().Replace("| ORM |", "| orm |", StringComparison.Ordinal);

        Assert.IsTrue(StackCoverage.Of(stack).IsEmpty);
    }

    [TestMethod]
    public void A_rule_with_no_row_is_named_and_a_rule_set_false_is_not()
    {
        string declined = Templates.TechStack().Replace("| `PRO_02` | true |", "| `PRO_02` | false |", StringComparison.Ordinal);
        string unseen = Without(declined, "| `PRO_03` |");

        StackCoverage.Unmentioned unmentioned = StackCoverage.Of(unseen);

        CollectionAssert.AreEqual(new[] { "PRO_03" }, unmentioned.Rules.Select(rule => rule.Id).ToArray());
        Assert.AreEqual(0, unmentioned.Defaults.Count);
    }

    [TestMethod]
    public void An_empty_stack_is_an_answer_about_every_concern_and_is_told_nothing()
    {
        Assert.IsTrue(StackCoverage.Of(string.Empty).IsEmpty);
        Assert.IsTrue(StackCoverage.Of("# What this solution is built from\n\nNothing is pinned.\n").IsEmpty);
    }

    [TestMethod]
    public void The_description_says_how_to_answer_and_lists_each_item()
    {
        string stack = Without(Without(Templates.TechStack(), "| accessibility testing |"), "| `PRO_01` |");

        string text = string.Join('\n', StackCoverage.Describe(StackCoverage.Of(stack), "    "));

        StringAssert.Contains(text, "'none'");
        StringAssert.Contains(text, "accessibility testing");
        StringAssert.Contains(text, "PRO_01");
        Assert.AreEqual(0, StackCoverage.Describe(StackCoverage.Unmentioned.Nothing, "    ").Count);
    }

    private static string Without(string text, string lineStart) => string.Join(
        '\n',
        text.ReplaceLineEndings("\n").Split('\n').Where(line => !line.StartsWith(lineStart, StringComparison.Ordinal)));
}
