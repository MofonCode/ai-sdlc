using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Diagnostics;
using Sdlc.Core.Git;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// The .NET build settings as rows in a declared stack: what a row reads as, what the menu says about
/// each file, and what the comparison against disk reports.
/// </summary>
/// <remarks>
/// <para>D-77 moved these off an option on adoption and into the one file that says what a solution is
/// built from. What makes that honest is the same thing that makes the rule rows honest: what is listed
/// is what the solution takes, and a cell that is not plainly yes is no.</para>
///
/// <para><strong>Nothing here writes a file on the strength of a row</strong>, and several of these
/// tests are about exactly that. The comparison names a gap and closes none; the diagnosis reports a
/// declared file that has not arrived as information rather than fault. The file lands inside a cycle
/// whose approved plan declares it, which <see cref="ScaffoldSolutionTests"/> holds.</para>
/// </remarks>
[TestClass]
public sealed class BuildSettingsTests
{
    [TestMethod]
    public void Every_shipped_file_says_what_it_does_and_what_it_costs()
    {
        // A draft that starts five rows at false on an existing codebase is five names and no way to
        // choose between them unless each says what it is. The summary has to fit a table cell; the
        // detail is where the cost to a codebase that predates the file is said.
        Assert.AreEqual(5, BuildSettings.All.Count);

        foreach (BuildSettings.Setting setting in BuildSettings.All)
        {
            Assert.IsTrue(setting.Summary.Length > 20, setting.Path + " has no summary worth a row.");
            Assert.IsFalse(
                setting.Summary.Contains('|', StringComparison.Ordinal),
                setting.Path + "'s summary would break the table it is written into.");

            StringAssert.Contains(
                setting.Detail,
                "On a codebase that predates it",
                setting.Path + " does not say what switching it on costs an existing codebase.");

            Assert.IsTrue(setting.Contents.Length > 0);
            Assert.AreSame(setting, BuildSettings.Find(setting.OnDisk), "Either slash finds the file.");
        }
    }

    [TestMethod]
    public void The_menu_describes_every_build_settings_file_and_offers_none_of_them_switched_on()
    {
        string menu = StackOptions.Render();

        StringAssert.Contains(menu, "## Build settings this framework ships");

        foreach (BuildSettings.Setting setting in BuildSettings.All)
        {
            StringAssert.Contains(menu, "### `" + setting.Path + "`");
            StringAssert.Contains(menu, setting.Summary);
            StringAssert.Contains(menu, setting.Detail);
        }

        // Read the way a declared stack is read. The menu's section carries a different heading on
        // purpose, so nothing in it counts as a row a solution has taken.
        Assert.AreEqual(
            0,
            BuildSettings.Rows(menu).Count,
            "The menu is a report. Pasted whole into a declared stack it must switch nothing on.");

        Assert.IsFalse(
            menu.Contains("| true |", StringComparison.Ordinal),
            "Listing one is not choosing it.");
    }

    [TestMethod]
    public void A_build_settings_row_is_read_from_its_own_section_and_nowhere_else()
    {
        // A rule row has an id to key on; a file name has nothing. So the section is what makes a
        // three-cell row one of these, and a rule or a default must never be counted as a file.
        string stack = Templates.TechStack(SolutionOrigin.Scaffolded);

        IReadOnlyList<BuildSettings.Row> rows = BuildSettings.Rows(stack);

        Assert.AreEqual(BuildSettings.All.Count, rows.Count);
        Assert.IsFalse(rows.Any(row => row.File.StartsWith("PRO_", StringComparison.Ordinal)));

        Assert.IsFalse(
            StackTable.Rows(stack).Any(row => BuildSettings.Find(row.Concern) is not null),
            "And the other direction: a file is not a declared default a plan can depart from.");

        Assert.AreEqual(
            0,
            StackRules.Rows(stack).Count(rule => BuildSettings.Find(rule.Id) is not null));
    }

    [TestMethod]
    public void A_cell_that_is_not_plainly_yes_leaves_its_file_undeclared()
    {
        // A person owns this file, so a cell will sooner or later say something nobody expected. The
        // file it would switch on makes warnings errors, so an unrecognised word has to read as off.
        const string Stack = """
            ## Build settings

            Some prose a person wrote above their table.

            | File | On | What it does |
            |---|---|---|
            | `.editorconfig` | yes | ours |
            | Directory.Build.props | maybe | not sure yet |
            | `tests\Directory.Build.props` | TRUE | relaxations |
            | `global.json` | false | no |
            | `Directory.Bild.props` | true | a typo |

            ## Notes

            | `Directory.Packages.props` | true | under another heading, so not a row |
            """;

        IReadOnlyList<BuildSettings.Row> rows = BuildSettings.Rows(Stack);

        CollectionAssert.AreEqual(
            (string[])
            [
                ".editorconfig",
                "Directory.Build.props",
                "tests/Directory.Build.props",
                "global.json",
                "Directory.Bild.props",
            ],
            rows.Select(row => row.File).ToArray());

        CollectionAssert.AreEqual(
            (bool[])[true, false, true, false, true],
            rows.Select(row => row.On).ToArray());

        Assert.IsNull(
            BuildSettings.Find(rows[^1].File),
            "A misspelt name is returned for the diagnosis to name, and takes nothing.");
    }

    [TestMethod]
    public async Task The_comparison_names_what_the_stack_takes_and_is_not_on_disk()
    {
        // The one question here a file listing can decide, which is why the binary answers it rather
        // than a stage working it out by reading. Present means a file of that name exists and nothing
        // more: each is a person's to amend, so there is no framework's version to compare against.
        using var solution = CycleWorkspace.Create();
        solution.WriteSolution(SolutionOrigin.Adopted);

        solution.WriteTechStack(
            """
            ## Build settings

            | File | On | What it does |
            |---|---|---|
            | `.editorconfig` | false | ours already |
            | `Directory.Build.props` | true | the policy |
            | `Directory.Packages.props` | true | versions |
            """);

        await File.WriteAllTextAsync(Path.Combine(solution.Root, ".editorconfig"), "root = true\n");
        await File.WriteAllTextAsync(Path.Combine(solution.Root, "Directory.Packages.props"), "<Project />\n");

        ReadStackDeclaration.Response stack = await Stack(solution);

        Assert.IsTrue(stack.Placed);

        CollectionAssert.AreEqual(
            (string[])["Directory.Build.props"],
            stack.Gap.Select(setting => setting.Path).ToArray(),
            "Declared and absent. The one that is declared and present is not a gap, and neither is "
            + "one the stack declined or says nothing about.");

        ReadStackDeclaration.SettingState State(string path) =>
            stack.Settings.Single(state => state.Setting.Path == path);

        Assert.AreEqual(ReadStackDeclaration.Declaration.Declined, State(".editorconfig").Declaration);
        Assert.IsTrue(State(".editorconfig").Present);
        Assert.AreEqual(ReadStackDeclaration.Declaration.NoRow, State("global.json").Declaration);

        string rendered = StackRenderer.Render(stack);

        StringAssert.Contains(rendered, "declared, MISSING");
        StringAssert.Contains(rendered, "Gap: 1 declared and not here -- Directory.Build.props.");
        StringAssert.Contains(
            rendered,
            "A row writes nothing",
            "The obvious reading of a missing declared file is that something should now write it.");

        Assert.IsFalse(
            File.Exists(Path.Combine(solution.Root, "Directory.Build.props")),
            "Reading the stack closes no gap.");
    }

    [TestMethod]
    public async Task A_solution_with_no_declared_stack_is_told_so_rather_than_shown_empty_tables()
    {
        using var solution = CycleWorkspace.Create();
        solution.WriteSolution(SolutionOrigin.Adopted);

        ReadStackDeclaration.Response stack = await Stack(solution);

        Assert.IsFalse(stack.Placed);
        StringAssert.Contains(StackRenderer.Render(stack), "sdlc draft techstack");
    }

    [TestMethod]
    public async Task The_diagnosis_reports_a_declared_file_that_has_not_arrived_without_calling_it_a_fault()
    {
        // The interval between switching a row on and the cycle that brings the file is the design
        // working. Failing the diagnosis for it would fail every solution that had just decided to
        // take one, which is the moment somebody is most likely to run this.
        using var solution = CycleWorkspace.Create();
        solution.WriteSolution(SolutionOrigin.Adopted);
        solution.WriteTechStack(Templates.TechStack(SolutionOrigin.Scaffolded));

        Check check = await BuildSettingsCheck(solution);

        Assert.AreEqual(CheckOutcome.Sound, check.Outcome);
        StringAssert.Contains(check.Detail, "5 of 5");
        StringAssert.Contains(check.Detail, "Not here yet");
        StringAssert.Contains(check.Detail, "sdlc stack");
    }

    [TestMethod]
    public async Task The_diagnosis_names_a_row_for_a_file_nothing_ships()
    {
        // For the reason an unknown rule id is a fault: the row reads as a solution having taken
        // something and takes nothing. A typo, in a file a person edits by hand.
        using var solution = CycleWorkspace.Create();
        solution.WriteSolution(SolutionOrigin.Adopted);

        solution.WriteTechStack(
            """
            ## Build settings

            | File | On | What it does |
            |---|---|---|
            | `Directory.Bild.props` | true | a typo |
            """);

        Check check = await BuildSettingsCheck(solution);

        Assert.AreEqual(CheckOutcome.Problem, check.Outcome);
        StringAssert.Contains(check.Detail, "Directory.Bild.props");
    }

    [TestMethod]
    public async Task The_diagnosis_says_nothing_about_build_settings_a_stack_does_not_mention()
    {
        // An empty stack, or one on another language, has no rows here and nothing to be told. A line
        // reading "0 of 5" on a Python solution is the kind nobody reads twice.
        using var solution = CycleWorkspace.Create();
        solution.WriteSolution(SolutionOrigin.Adopted);
        solution.WriteTechStack(string.Empty);

        using var gate = Gate.For(
            solution.Root,
            services => services.AddSingleton<IRepository>(new FakeRepository()));

        Result<Diagnose.Response> report = await gate
            .Send<Diagnose.Query, Diagnose.Response>(new Diagnose.Query());

        Assert.IsTrue(report.IsSuccess);
        Assert.IsFalse(report.Value.Checks.Any(check => check.Subject == "build settings"));
    }

    private static async Task<Check> BuildSettingsCheck(CycleWorkspace solution)
    {
        using var gate = Gate.For(
            solution.Root,
            services => services.AddSingleton<IRepository>(new FakeRepository()));

        Result<Diagnose.Response> report = await gate
            .Send<Diagnose.Query, Diagnose.Response>(new Diagnose.Query());

        Assert.IsTrue(report.IsSuccess, string.Join("; ", report.Errors.Select(error => error.Message)));
        return report.Value.Checks.Single(check => check.Subject == "build settings");
    }

    private static async Task<ReadStackDeclaration.Response> Stack(CycleWorkspace solution)
    {
        using var gate = Gate.For(solution.Root);

        Result<ReadStackDeclaration.Response> result = await gate
            .Send<ReadStackDeclaration.Query, ReadStackDeclaration.Response>(new ReadStackDeclaration.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }
}
