using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// The block adoption and <c>doctor</c> both end with: what is left to place, and how.
/// </summary>
/// <remarks>
/// Rendered from a state built here rather than read from disk, so these hold the block's own rules --
/// the count, the rename for each draft, the numbering, the optional draft riding along -- and the
/// adoption and doctor tests hold only that each prints it, from what is on disk. The steps carry
/// their own wording, so a change to how <see cref="Setup.Read"/> names a file is not a change here.
/// </remarks>
[TestClass]
public sealed class SetupTests
{
    [TestMethod]
    public void Every_draft_left_is_named_with_its_rename_and_the_commit()
    {
        string block = Render(
            Required("the paragraph", "solution.md", SetupProgress.Drafted),
            Required("the stack", "techstack.md", SetupProgress.Drafted));

        StringAssert.Contains(block, "Two things are yours to place before the first cycle: the paragraph, and the stack.");
        StringAssert.Contains(block, "an agent's write to any of these files is denied");
        StringAssert.Contains(block, "mv -f .sdlc/solution.draft.md .sdlc/solution.md");
        StringAssert.Contains(block, "mv -f .sdlc/techstack.draft.md .sdlc/techstack.md");
        StringAssert.Contains(block, "Move-Item -Force", "A person on Windows is told the rename in their own shell.");
        StringAssert.Contains(block, "3. Commit them");
    }

    [TestMethod]
    public void One_thing_left_is_counted_in_words_and_names_only_itself()
    {
        string block = Render(
            Required("the paragraph", "solution.md", SetupProgress.Placed),
            Required("the stack", "techstack.md", SetupProgress.Drafted));

        StringAssert.Contains(block, "One thing is yours to place before the first cycle: the stack.");
        Assert.IsFalse(block.Contains("solution", StringComparison.Ordinal), "A placed file is not a step left. " + block);
    }

    [TestMethod]
    public void Three_or_more_left_are_counted_in_figures_and_joined_with_a_final_and()
    {
        string block = Render(
            Required("one", "a.md", SetupProgress.Drafted),
            Required("two", "b.md", SetupProgress.Drafted),
            Required("three", "c.md", SetupProgress.Drafted));

        StringAssert.Contains(block, "3 things are yours to place before the first cycle: one, two, and three.");
    }

    [TestMethod]
    public void A_file_placed_with_the_template_still_in_it_is_named_at_its_path_and_needs_no_rename()
    {
        string block = Render(Required("the paragraph", "solution.md", SetupProgress.Unfilled));

        StringAssert.Contains(block, ".sdlc/solution.md");
        StringAssert.Contains(block, "still the template's text: write the paragraph");
        Assert.IsFalse(block.Contains("mv -f", StringComparison.Ordinal), "It is already in place. " + block);
        StringAssert.Contains(block, "2. Commit them", "With no rename, the commit is the second step.");
    }

    [TestMethod]
    public void A_file_with_no_draft_is_named_by_the_command_that_writes_one()
    {
        string block = Render(Required("the stack", "techstack.md", SetupProgress.Missing));

        StringAssert.Contains(block, "sdlc draft techstack");
        StringAssert.Contains(block, "writes .sdlc/techstack.draft.md to fill in");
        Assert.IsFalse(block.Contains("mv -f", StringComparison.Ordinal), "There is nothing to rename yet. " + block);
    }

    [TestMethod]
    public void The_optional_draft_rides_along_while_it_waits_and_is_never_counted()
    {
        // The rename a person is already doing for the others covers it, and a solution with no rules
        // of its own is sound, so it is not a thing left.
        string block = Render(
            Required("the stack", "techstack.md", SetupProgress.Drafted),
            Optional(SetupProgress.Drafted));

        StringAssert.Contains(block, "One thing is yours to place before the first cycle: the stack.");
        StringAssert.Contains(block, "mv -f .sdlc/constraints.draft.md .sdlc/constraints.md");
        StringAssert.Contains(block, "-- optional");
    }

    [TestMethod]
    public void Nothing_left_before_the_first_cycle_points_at_the_intent_and_not_at_the_optional_draft()
    {
        string block = Render(
            Required("the stack", "techstack.md", SetupProgress.Placed),
            Optional(SetupProgress.Drafted));

        StringAssert.Contains(block, "Nothing is left to place");
        StringAssert.Contains(block, "/sdlc:intent");
        Assert.IsFalse(block.Contains("constraints.draft.md", StringComparison.Ordinal), block);
    }

    [TestMethod]
    public void Nothing_left_once_a_cycle_exists_says_nothing()
    {
        // From the first cycle on the status panel names the next command for each cycle, and a block
        // here would be a second answer to that question.
        SetupState state = new([Required("the stack", "techstack.md", SetupProgress.Placed)], HasCycles: true);

        Assert.AreEqual(0, Setup.Render(state).Count);
    }

    /// <summary>
    /// Holds that a report printed the block for what it read, every line of it and in order.
    /// </summary>
    /// <remarks>
    /// For the adoption and doctor tests, which hold what was read from disk and that it was printed,
    /// and leave what the block says to the tests above.
    /// </remarks>
    internal static void AssertPrinted(string rendered, SetupState? state)
    {
        Assert.IsNotNull(state, "The report read no setup state.");

        IReadOnlyList<string> block = Setup.Render(state);
        Assert.AreNotEqual(0, block.Count, "There is nothing to print, so this proves nothing.");

        int from = 0;

        foreach (string line in block.Where(line => line.Length > 0))
        {
            int at = rendered.IndexOf(line, from, StringComparison.Ordinal);
            Assert.IsTrue(at >= 0, $"The report does not print \"{line.Trim()}\" where the block has it:{Environment.NewLine}{rendered}");
            from = at + line.Length;
        }
    }

    // Whitespace collapsed, because the sentence is wrapped to the terminal and where it breaks is not
    // what these hold.
    private static string Render(params SetupStep[] steps) =>
        System.Text.RegularExpressions.Regex.Replace(
            string.Join(" ", Setup.Render(new SetupState(steps, HasCycles: false))),
            @"\s+",
            " ");

    private static SetupStep Required(string thing, string fileName, SetupProgress progress) =>
        new(
            thing,
            fileName,
            Path.GetFileNameWithoutExtension(fileName) + ".draft.md",
            "sdlc draft " + Path.GetFileNameWithoutExtension(fileName),
            thing,
            progress);

    private static SetupStep Optional(SetupProgress progress) =>
        new(
            "its own constraints",
            "constraints.md",
            Setup.ConstraintsDraftFileName,
            "sdlc propose constraints",
            "a rule of its own",
            progress,
            Optional: true);
}
