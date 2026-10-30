using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// What a profile declares, and what it honestly says it does not have.
/// </summary>
/// <remarks>
/// The property every test here protects: one profile governs a solution and reaches it as one flat table
/// a person owns. Composition happens in the framework, where two profiles sharing a platform choice share
/// one set of rows; it never reaches a solution, because a derived stack file would make the thing every
/// plan is judged against something nobody wrote.
/// </remarks>
[TestClass]
public sealed class StackProfileTests
{
    [TestMethod]
    public void The_default_profile_is_the_one_a_solution_gets_without_asking()
    {
        Assert.AreEqual(
            "dotnet-default",
            StackProfiles.All[0].Name,
            "The default is listed first, because a listing is read top down -- and its name is the one "
            + "the command line falls back to, so the two cannot be allowed to drift apart.");

        Assert.IsNotNull(
            StackProfiles.Find(StackProfiles.DefaultName),
            "The fallback name resolves to a profile that exists.");
    }

    [TestMethod]
    [DataRow("dotnet-default")]
    [DataRow("ml-tensorflow")]
    [DataRow("DOTNET-DEFAULT")]
    public void A_profile_resolves_by_name_whatever_the_case(string name) =>
        Assert.IsNotNull(StackProfiles.Find(name));

    [TestMethod]
    [DataRow("tensorflow")]
    [DataRow("ml_tensorflow")]
    [DataRow("")]
    [DataRow(null)]
    public void A_name_nothing_answers_to_resolves_to_nothing(string? name) =>
        Assert.IsNull(
            StackProfiles.Find(name),
            "Including a near miss. 'ml_tensorflow' is not 'ml-tensorflow', and silently accepting it "
            + "would hand a solution a stack it never chose.");

    [TestMethod]
    public void Every_profile_shares_the_platform_rows_and_declares_them_once()
    {
        // The only composition there is, and the reason it exists: where data lives and how
        // infrastructure is described are decisions about the platform rather than the language, so two
        // profiles edited separately must not come to disagree about Terraform.
        foreach (StackProfile profile in StackProfiles.All)
        {
            foreach (string concern in (string[])["cloud", "infrastructure as code", "relational data"])
            {
                StackDefault? row = profile.Defaults.FirstOrDefault(d => d.Concern == concern);

                Assert.IsNotNull(row, $"{profile.Name} declares nothing for '{concern}'.");
                Assert.AreNotEqual(string.Empty, row.Choice);
            }
        }

        Assert.AreEqual(
            StackProfiles.All[0].Defaults.First(d => d.Concern == "cloud").Choice,
            StackProfiles.All[1].Defaults.First(d => d.Concern == "cloud").Choice,
            "Shared rows are one set of rows, not two that happen to agree today.");
    }

    [TestMethod]
    public void Every_profile_declares_a_concern_only_once()
    {
        // The dotnet profile composes the platform rows into the middle of its own, so a row repeated by
        // accident would reach a solution as a table declaring two answers to one question.
        foreach (StackProfile profile in StackProfiles.All)
        {
            string[] duplicated =
            [
                .. profile.Defaults
                    .GroupBy(row => row.Concern, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key),
            ];

            Assert.AreEqual(
                0,
                duplicated.Length,
                $"{profile.Name} declares twice: {string.Join(", ", duplicated)}");
        }
    }

    [TestMethod]
    public void A_first_stack_draft_is_one_flat_table_of_the_default_profiles_rows()
    {
        StackProfile profile = StackProfiles.Default;
        string stack = Templates.TechStack();

        StringAssert.Contains(stack, "| Concern | Choice |");

        foreach (StackDefault row in profile.Defaults)
        {
            StringAssert.Contains(
                stack,
                $"| {row.Concern} | {row.Choice} |",
                "Every declared default reaches the draft verbatim.");
        }

        Assert.IsFalse(
            stack.Contains("TensorFlow", StringComparison.Ordinal),
            "And nothing from the other profile. Its rows are in the menu, for somebody to copy across.");

        StringAssert.Contains(stack, "## Notes worth keeping");
    }

    [TestMethod]
    public void A_first_stack_draft_lists_every_shipped_stack_rule_switched_on()
    {
        // What a person reads is what binds. The rules used to follow from a profile name recorded in
        // another file, so nothing in the declared stack said a plan would be held to them -- now each
        // is a row, and declining one is an edit to the line that says it is on.
        IReadOnlyList<StackRules.Row> rules = StackRules.Rows(Templates.TechStack());

        CollectionAssert.AreEqual(
            (string[])["PRO_01", "PRO_02", "PRO_03", "PRO_04", "PRO_05"],
            rules.Select(rule => rule.Id).ToArray());

        foreach (StackRules.Row rule in rules)
        {
            Assert.IsTrue(rule.On, $"{rule.Id} is listed off in a draft of the default.");
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(rule.Title),
                $"{rule.Id} has no description beside it, so the row says a rule is on and not which.");
        }
    }

    [TestMethod]
    public void A_rule_row_is_never_counted_as_a_declared_default()
    {
        // Two tables in one file, read by two readers. A rule a plan could "depart from" by arguing
        // would be a rule that binds nobody, and a default counted as a rule would switch on nothing.
        string stack = Templates.TechStack();

        Assert.AreEqual(
            StackProfiles.Default.Defaults.Count,
            StackTable.Rows(stack).Count,
            "The rules table has three cells and a declared default has two, which is what keeps them apart.");
    }

    [TestMethod]
    [DataRow("true", true)]
    [DataRow("TRUE", true)]
    [DataRow("yes", true)]
    [DataRow("on", true)]
    [DataRow("false", false)]
    [DataRow("", false)]
    [DataRow("maybe", false)]
    [DataRow("ture", false)]
    public void A_cell_that_is_not_plainly_yes_leaves_its_rule_off(string cell, bool expected)
    {
        // A person owns this file, so a cell will sooner or later say something nobody expected. Reading
        // an unrecognised word as on would bind a solution to a rule on the strength of a typo.
        IReadOnlyList<StackRules.Row> rules = StackRules.Rows($"| `PRO_02` | {cell} | names the seam |\n");

        Assert.AreEqual(1, rules.Count);
        Assert.AreEqual("PRO_02", rules[0].Id, "With or without the backticks a rendered table shows.");
        Assert.AreEqual(expected, rules[0].On);
    }

    [TestMethod]
    public void The_command_line_no_longer_offers_a_profile_to_choose()
    {
        // The option recorded a profile at adoption, before anybody had said what the solution was built
        // from, and a refresh consulted it rather than the one recorded. Nothing records a profile now,
        // so help that still listed the option would be describing a choice nobody is given.
        Assert.IsFalse(
            Sdlc.Cli.Usage.Text.Contains("--profile", StringComparison.Ordinal),
            "'sdlc help' still documents --profile, which 'sdlc init' answers with a refusal.");

        Assert.IsFalse(
            Sdlc.Cli.Usage.Text.Contains("draft profile", StringComparison.Ordinal),
            "'sdlc help' still documents 'draft profile', which is gone.");
    }

    [TestMethod]
    public void Every_profile_explains_itself_in_one_line_and_the_default_carries_notes()
    {
        foreach (StackProfile profile in StackProfiles.All)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(profile.Summary),
                $"{profile.Name} has no summary, so a listing of profiles would show a bare name.");
        }

        Assert.IsTrue(
            StackProfiles.Default.Notes.Count > 0,
            "The default carries no notes. The table holds choices and the notes hold the reasoning a "
            + "plan needs, which no row can -- and the default is the one a draft is written from.");
    }
}
