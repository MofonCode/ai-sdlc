using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Diagnostics;
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
    public void A_refusal_names_every_profile_that_exists()
    {
        // A refusal that only says no sends somebody looking for the flag that makes it work, which is
        // the confusion the whole draft pattern exists to avoid.
        string message = StackProfiles.Unknown("pytorch");

        StringAssert.Contains(message, "pytorch");

        foreach (StackProfile profile in StackProfiles.All)
        {
            StringAssert.Contains(message, profile.Name);
        }
    }

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
    public void The_dotnet_profile_ships_a_skeleton_and_the_tensorflow_one_says_it_does_not()
    {
        // The honesty rule. The scaffold is twenty-four .NET templates, so a profile declaring another
        // language either ships its own or says it has none -- and saying so is what stops a solution
        // getting a layout its own stack file contradicts.
        Assert.IsTrue(StackProfiles.Find("dotnet-default")!.Scaffolds);
        Assert.IsFalse(StackProfiles.Find("ml-tensorflow")!.Scaffolds);
    }

    [TestMethod]
    public void A_profile_with_no_skeleton_says_so_in_the_file_a_plan_reads()
    {
        // Where it matters. The stack file is the first thing a plan is handed about the solution, so the
        // absence belongs there rather than only in a command's refusal.
        string stack = Templates.TechStack(
            StackProfiles.Find("ml-tensorflow")!,
            SolutionOrigin.Scaffolded);

        StringAssert.Contains(stack, "declares no solution skeleton");
        StringAssert.Contains(
            stack,
            "architecture suite is missing",
            "And names the material gap, because it is the only mechanical enforcement of shape there is.");
    }

    [TestMethod]
    public void A_scaffolded_stack_file_is_one_flat_table_of_the_profiles_rows()
    {
        StackProfile profile = StackProfiles.Find("ml-tensorflow")!;
        string stack = Templates.TechStack(profile, SolutionOrigin.Scaffolded);

        StringAssert.Contains(stack, "| Concern | Choice |");

        foreach (StackDefault row in profile.Defaults)
        {
            StringAssert.Contains(
                stack,
                $"| {row.Concern} | {row.Choice} |",
                "Every declared default reaches the file verbatim.");
        }

        Assert.IsFalse(
            stack.Contains("C#, .NET", StringComparison.Ordinal),
            "And nothing from the other profile. One profile governs a solution.");
    }

    [TestMethod]
    public void An_adopted_stack_file_declares_nothing_and_still_carries_the_profiles_reasoning()
    {
        // Reading an existing repository to guess what it is built from would write a guess down as a
        // declaration, and the first plan to depart from a wrong guess would be judged against it. The
        // notes still come, because a solution that has not taken the defaults still has to know what it
        // would be departing from.
        string stack = Templates.TechStack(
            StackProfiles.Find("dotnet-default")!,
            SolutionOrigin.Adopted);

        StringAssert.Contains(stack, "nothing is declared here yet");
        Assert.IsFalse(stack.Contains("| ORM | EF Core |", StringComparison.Ordinal));
        StringAssert.Contains(stack, "## Notes worth keeping");
    }

    [TestMethod]
    public async Task Doctor_reports_a_recorded_profile_that_answers_to_nothing()
    {
        // Adoption and 'draft profile' both refuse an unknown name, so reaching this state means a
        // solution adopted before profiles resolved to anything, or one hand-edited since. It reads fine
        // and every gate still works, which is what makes it worth reporting: the failure is quiet. No
        // profile supplies the defaults, so CON_15 has nothing to bite on and a plan departs from nothing.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution("dotnet-defualt", SolutionOrigin.Adopted);

        using var gate = Gate.For(workspace.Root);

        Result<Diagnose.Response> result = await gate
            .Send<Diagnose.Query, Diagnose.Response>(new Diagnose.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));

        Check adoption = result.Value.Checks.First(check => check.Subject == "adoption");

        Assert.AreEqual(CheckOutcome.Problem, adoption.Outcome, adoption.Detail);
        StringAssert.Contains(adoption.Detail, "dotnet-defualt");
        StringAssert.Contains(adoption.Remedy!, "draft profile", "And names the act that fixes it.");
    }

    [TestMethod]
    public void The_help_text_names_every_profile_that_exists()
    {
        // A list in help is a claim somebody will act on, and the first wrong one makes the rest not worth
        // reading. The usage text is a const so it cannot compose this itself, which is exactly why the
        // suite holds the two together.
        foreach (StackProfile profile in StackProfiles.All)
        {
            StringAssert.Contains(
                Sdlc.Cli.Usage.Text,
                profile.Name,
                $"'sdlc help' does not mention {profile.Name}, so nobody would know to ask for it.");
        }
    }

    [TestMethod]
    public void Every_profile_explains_itself_in_one_line_and_carries_notes()
    {
        foreach (StackProfile profile in StackProfiles.All)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(profile.Summary),
                $"{profile.Name} has no summary, so a listing of profiles would show a bare name.");

            Assert.IsTrue(
                profile.Notes.Count > 0,
                $"{profile.Name} carries no notes. The table holds choices and the notes hold the "
                + "reasoning a plan needs, which no row can.");
        }
    }
}
