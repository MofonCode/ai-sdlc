using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Drafts;
using Sdlc.Core.Reports;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// Asserts the menu written beside a stack draft: what it offers, and what it must never become.
/// </summary>
/// <remarks>
/// <para>The menu exists because a declared stack is hard to edit from nothing — an adopted solution's
/// opens with four empty rows and an instruction to fill them in from what the repository uses, and
/// what the shipped profiles declare was readable only in the framework's own source.</para>
/// <para><strong>The shape it must keep is the separation.</strong> It is a second file, written fresh
/// on every run as a report, never renamed into place. Inside the draft it would reach the declared
/// stack through the rename, and a declared stack listing answers the solution has not chosen is one a
/// plan can reach into and truthfully not be departing from anything — which is the deadlock protecting
/// that file closed (D-67). Being a report is the other half: the menu outlives the rename, so a copy
/// anywhere unignored would reach a later cycle's diff as a file no plan declared (D-75). Three
/// assertions here are about those two things and not about the content.</para>
/// </remarks>
[TestClass]
public sealed class StackOptionsTests
{
    [TestMethod]
    public void The_menu_offers_every_concern_every_shipped_profile_declares()
    {
        // Derived rather than authored, which is what stops it disagreeing with the tables it came
        // from. A second catalogue of choices would drift the first time a profile gained a row.
        string menu = StackOptions.Render();

        foreach (StackProfile profile in StackProfiles.All)
        {
            foreach (StackDefault row in profile.Defaults)
            {
                StringAssert.Contains(
                    menu,
                    row.Concern,
                    $"'{profile.Name}' declares a concern the menu does not offer: {row.Concern}.");
                StringAssert.Contains(
                    menu,
                    row.Choice,
                    $"'{profile.Name}' declares an answer the menu does not carry: {row.Choice}.");
            }
        }
    }

    [TestMethod]
    public void Where_two_profiles_disagree_both_answers_are_on_the_page_with_who_declares_each()
    {
        // The case the whole menu is for. One profile per solution and no stacking (D-68), so a
        // solution wanting a row another profile declares copies it in -- and until now, finding out
        // what that row said meant reading the framework's source.
        string menu = StackOptions.Render();

        // The menu's row rather than the empty table's, which carries the same concern with no answer.
        string machineLearning = menu
            .Split('\n')
            .Single(line => line.StartsWith("| machine learning |", StringComparison.Ordinal)
                && !line.TrimEnd().EndsWith("| |", StringComparison.Ordinal));

        StringAssert.Contains(machineLearning, "ML.NET");
        StringAssert.Contains(machineLearning, "TensorFlow");
        StringAssert.Contains(machineLearning, "dotnet-default");
        StringAssert.Contains(
            machineLearning,
            "ml-tensorflow",
            "Each answer names the profile declaring it. An answer with no provenance is one a reader "
            + "has to take on trust.");
    }

    [TestMethod]
    public void A_row_every_profile_declares_says_so_rather_than_crediting_one_of_them()
    {
        // The platform rows -- where data lives, how infrastructure is described -- come from one
        // shared set, so that two profiles cannot come to disagree about Terraform by being edited
        // separately. The first version of this credited whichever profile mentioned a choice first,
        // which read as the others having no answer for it: the exact opposite of what those rows mean.
        string menu = StackOptions.Render();

        string[] shared = [.. StackProfiles.All
            .Select(profile => profile.Defaults)
            .Aggregate((first, second) => [.. first.Intersect(second)])
            .Select(row => row.Concern)];

        Assert.IsTrue(shared.Length > 0, "No row is shared, so this would pass by finding nothing.");

        foreach (string concern in shared)
        {
            string row = menu
                .Split('\n')
                .Single(line => line.StartsWith("| " + concern + " |", StringComparison.Ordinal)
                    && !line.TrimEnd().EndsWith("| |", StringComparison.Ordinal));

            StringAssert.Contains(
                row,
                "every profile",
                $"'{concern}' is declared by all of them, and saying so is what tells a reader the row "
                + "is a decision about the platform rather than about the language.");
        }
    }

    [TestMethod]
    public void The_menu_says_it_is_never_renamed_into_place_and_carries_the_marker_that_proves_it()
    {
        // Both halves matter. The sentence is for the person; the marker is what `sdlc doctor` keys on
        // when somebody pastes the menu into the real file anyway.
        string menu = StackOptions.Render();

        StringAssert.Contains(menu, StackOptions.Marker);
        StringAssert.Contains(menu, "never renamed into place");
        StringAssert.StartsWith(
            menu,
            StackOptions.Marker,
            "First line, so a file that was truncated mid-paste still carries it.");
    }

    [TestMethod]
    public void The_menu_carries_the_alternatives_the_framework_argued_and_what_each_turns_on()
    {
        // The one authored part, and the reason it is allowed: these are answers the concept document
        // weighs and no profile declares, so deriving them from the tables would lose them. Each
        // carries its reasoning -- a candidate with only a name in a menu reads as an endorsement.
        string menu = StackOptions.Render();

        StringAssert.Contains(menu, "Microsoft.FluentUI.AspNetCore.Components");
        StringAssert.Contains(menu, "breadth of the component set");
        StringAssert.Contains(menu, "MediatR", "Declared against rather than left unmentioned.");
        StringAssert.Contains(menu, "Appium");
    }

    [TestMethod]
    public void The_empty_table_holds_the_default_profiles_concerns_with_the_answers_taken_out()
    {
        // For a solution starting over rather than amending one row. It is here and not in the draft
        // deliberately: a draft of an existing file is a byte copy, and a blank form in its place would
        // drop every row nobody retyped from the one file every plan is judged against. The default's
        // concerns, because no solution records a profile for it to be anybody else's.
        string menu = StackOptions.Render();

        string[] lines = menu.Split('\n');

        foreach (StackDefault row in StackProfiles.Default.Defaults)
        {
            Assert.IsTrue(
                lines.Any(line => line.Trim() == "| " + row.Concern + " | |"),
                $"The empty table has no row for '{row.Concern}'.");
        }
    }

    [TestMethod]
    public void The_menu_lists_every_stack_rule_this_release_ships_and_switches_none_of_them_on()
    {
        // Where somebody whose stack is not the default finds the rows to copy in. Off, because the
        // menu recommends nothing: a rule shown as on is one a person pastes without deciding.
        IReadOnlyList<StackRules.Row> rules = StackRules.Rows(StackOptions.Render());

        CollectionAssert.AreEqual(
            StackRules.Rows(Sdlc.Core.Init.Templates.TechStack()).Select(rule => rule.Id).ToArray(),
            rules.Select(rule => rule.Id).ToArray(),
            "The same rules a default draft lists, so the two cannot come to disagree.");

        Assert.IsFalse(rules.Any(rule => rule.On), "Listing a rule is not choosing it.");
    }

    [TestMethod]
    public async Task Drafting_the_stack_writes_the_menu_beside_the_draft_and_leaves_the_draft_a_copy()
    {
        // The two files together, which is the design: the draft is what gets renamed and stays a byte
        // copy, and the menu is what a person reads while editing it.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted);

        string path = Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, Cycle.TechStackFileName);
        const string Declared = "# What this solution is built from\n\n| Concern | Choice |\n|---|---|\n| api | ASP.NET Core |\n";

        await File.WriteAllTextAsync(path, Declared, TestContext.CancellationToken);

        using var gate = Gate.For(workspace.Root);

        Result<DraftResult> result = await gate.Send<WriteTechStackDraft.Command, DraftResult>(
            new WriteTechStackDraft.Command());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));

        string draftPath = Path.Combine(
            workspace.Root,
            Cycle.SdlcDirectoryName,
            WriteTechStackDraft.DraftFileName);

        string reports = Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, ReportFile.DirectoryName);

        string optionsPath = Directory
            .EnumerateFiles(reports, "*-" + WriteTechStackDraft.OptionsReportKind + ".md")
            .Single();

        Assert.AreEqual(
            Declared,
            await File.ReadAllTextAsync(draftPath, TestContext.CancellationToken),
            "Still a byte copy. The menu went somewhere else, which is the whole point of two files.");

        string menu = await File.ReadAllTextAsync(optionsPath, TestContext.CancellationToken);

        StringAssert.Contains(menu, StackOptions.Marker);
        StringAssert.Contains(menu, "machine learning");
        StringAssert.Contains(
            result.Value.Instructions,
            optionsPath,
            "And the instructions name the path, or nobody finds it.");
    }

    [TestMethod]
    public async Task The_menu_is_a_report_so_a_copy_left_behind_never_reaches_a_later_cycles_diff()
    {
        // The defect this closes, and it is not hypothetical. The menu outlives the rename -- nothing
        // deletes it -- so the first version of this left a file in `.sdlc/` indefinitely. `evidence`
        // stages untracked paths with --intent-to-add before diffing and excludes ignored ones, so an
        // unignored copy would reach every later cycle's diff as a file no plan declared, which is what
        // CON_07.2 fires on. ReportFile already carries that whole argument and the directory that
        // ignores itself; the menu matches its definition exactly.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted);

        string path = Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, Cycle.TechStackFileName);
        await File.WriteAllTextAsync(path, "| Concern | Choice |\n|---|---|\n| api | ASP.NET Core |\n", TestContext.CancellationToken);

        using var gate = Gate.For(workspace.Root);

        Result<DraftResult> result = await gate.Send<WriteTechStackDraft.Command, DraftResult>(
            new WriteTechStackDraft.Command());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));

        string reports = Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, ReportFile.DirectoryName);
        string ignore = Path.Combine(reports, ".gitignore");

        Assert.IsTrue(File.Exists(ignore), "The directory has to arrive ignoring itself.");
        StringAssert.Contains(await File.ReadAllTextAsync(ignore, TestContext.CancellationToken), "*");

        Assert.IsFalse(
            File.Exists(Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, "techstack.options.md")),
            "Nothing is left in .sdlc/ itself, which is where this first wrote it.");
    }

    /// <summary>The test context, for the cancellation token the analysers require to be passed on.</summary>
    public TestContext TestContext { get; set; } = null!;
}
