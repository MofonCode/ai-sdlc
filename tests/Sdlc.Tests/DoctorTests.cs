using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Diagnostics;
using Sdlc.Core.Git;
using Sdlc.Core.Hooks;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// What <c>sdlc doctor</c> says about an installation.
/// </summary>
/// <remarks>
/// The thing worth testing here is not that it finds a missing file. It is that it never reports an
/// install as sound on the strength of something it did not check -- a command that said everything was
/// fine because the questions it could not answer were counted as answers would be the exact failure
/// this framework opens by naming.
/// </remarks>
[TestClass]
public sealed class DoctorTests
{
    [TestMethod]
    public async Task A_repository_that_was_never_adopted_says_so_and_stops_there()
    {
        // One fact rather than six. Every check below adoption reads what adoption wrote, so listing
        // each of them as missing would bury the only thing a person can act on.
        using var solution = Scratch.Create();

        Diagnose.Response report = await Diagnosis(solution);

        Assert.IsFalse(report.Sound);

        Check adoption = report.Checks.Single(check => check.Subject == "adoption");

        Assert.AreEqual(CheckOutcome.Problem, adoption.Outcome);
        StringAssert.Contains(adoption.Detail, "not governed");
        StringAssert.Contains(adoption.Remedy!, "a person runs");

        Assert.IsFalse(
            report.Checks.Any(check => check.Subject == "the guarantee"),
            "There is nothing to say about a pipeline in a repository that is not governed at all.");
    }

    [TestMethod]
    public async Task A_directory_that_is_not_a_repository_fails_on_git_before_anything_else()
    {
        // The same refusal adoption gives, as a check: without git a build cannot record the diff a
        // verification derives from, and the gates key on a branch that cannot exist.
        using var solution = Scratch.Create();

        Diagnose.Response report = await Diagnosis(solution, new FakeRepository { Repository = false });

        Check git = report.Checks.Single(check => check.Subject == "git");

        Assert.AreEqual(CheckOutcome.Problem, git.Outcome);
        StringAssert.Contains(git.Remedy!, "git init");
    }

    [TestMethod]
    public async Task An_adopted_repository_is_sound_and_still_says_what_it_cannot_see()
    {
        using var solution = Scratch.Create();
        await Adopt(solution);

        Diagnose.Response report = await Diagnosis(solution);

        Assert.IsTrue(
            report.Sound,
            "Adoption's own output should satisfy the doctor: "
            + string.Join("; ", report.Checks
                .Where(check => check.Outcome == CheckOutcome.Problem)
                .Select(check => check.Subject + " - " + check.Detail)));

        // The honest category, and the reason `Sound` ignores it. This is true of a correct install, so
        // counting it as a problem would make every correct install exit non-zero -- and counting it as
        // sound would claim something nobody established.
        string[] elsewhere =
        [
            .. report.Checks
                .Where(check => check.Outcome == CheckOutcome.Elsewhere)
                .Select(check => check.Subject),
        ];

        CollectionAssert.AreEquivalent(
            new[] { "branch policy", "the skills" },
            elsewhere,
            "A binary cannot ask a host what it loaded, or a platform it is not signed in to what its branch policy requires.");
    }

    [TestMethod]
    public async Task A_solution_whose_line_endings_are_not_normalised_is_a_problem()
    {
        // A solution adopted before adoption wrote the rule, or one whose own file leaves it out. The cost
        // is a review where a commit from another platform shows a whole file changed.
        using var solution = Scratch.Create();
        await Adopt(solution);
        await File.WriteAllTextAsync(Path.Combine(solution.Root, LineEndings.FileName), "*.png binary\n");

        Diagnose.Response report = await Diagnosis(solution);
        Check check = report.Checks.Single(candidate => candidate.Subject == "line endings");

        Assert.AreEqual(CheckOutcome.Problem, check.Outcome);
        StringAssert.Contains(check.Remedy!, LineEndings.Rule);

        File.Delete(Path.Combine(solution.Root, LineEndings.FileName));

        check = (await Diagnosis(solution)).Checks.Single(candidate => candidate.Subject == "line endings");

        Assert.AreEqual(CheckOutcome.Problem, check.Outcome);
        StringAssert.Contains(check.Detail, "there is no");
    }

    [TestMethod]
    public async Task On_azure_devops_the_branch_policy_is_named_as_what_runs_the_check()
    {
        // Azure Repos ignores a YAML pull-request trigger, so the pipeline runs only from a build
        // validation policy. Without one there is no optional check, there is no check -- and a remedy
        // that read the same as GitHub's would let somebody believe the pipeline was running.
        using var solution = Scratch.Create();
        await Adopt(solution, Platform.AzureDevOps);

        Diagnose.Response report = await Diagnosis(solution);
        Check policy = report.Checks.Single(check => check.Subject == "branch policy");

        Assert.AreEqual(CheckOutcome.Elsewhere, policy.Outcome);
        StringAssert.Contains(policy.Remedy!, "Build validation");
        StringAssert.Contains(policy.Remedy!, "nothing runs");
    }

    [TestMethod]
    [DynamicData(nameof(Hosts))]
    public async Task Every_host_in_the_table_is_checked_by_name(HookHost named)
    {
        // Driven off the enum, so a host added to the table is checked here without anybody
        // remembering -- and one added without adoption writing its config fails rather than being
        // quietly unreported.
        SupportedHost host = SupportedHosts.Of(named);

        using var solution = Scratch.Create();
        await Adopt(solution);

        Diagnose.Response report = await Diagnosis(solution);
        Check check = report.Checks.Single(candidate => candidate.Subject == host.Name);

        Assert.AreEqual(CheckOutcome.Sound, check.Outcome, check.Detail);
        StringAssert.Contains(check.Detail, host.ConfigPath);
    }

    [TestMethod]
    public async Task A_hook_config_that_exists_and_calls_nothing_is_a_problem()
    {
        // **The dangerous direction.** A file that parses and names no gate is what an upgrade leaves
        // when a merge went wrong, and it is indistinguishable from a working install by a listing.
        // A hook that is not loaded denies nothing and every cycle still appears to work, so the
        // check reads the file rather than testing for it.
        SupportedHost claude = SupportedHosts.Of(HookHost.Claude);

        using var solution = Scratch.Create();
        await Adopt(solution);

        string path = Path.Combine(solution.Root, Path.Combine([.. claude.ConfigPath.Split('/')]));
        await File.WriteAllTextAsync(path, """{ "hooks": { "PreToolUse": [] } }""");

        Diagnose.Response report = await Diagnosis(solution);
        Check check = report.Checks.Single(candidate => candidate.Subject == claude.Name);

        Assert.AreEqual(CheckOutcome.Problem, check.Outcome);
        StringAssert.Contains(check.Detail, "does not call the gate");
        Assert.IsFalse(report.Sound);
    }

    [TestMethod]
    public async Task A_solution_with_no_pipeline_running_the_check_is_told_the_guarantee_is_absent()
    {
        // The one non-bypassable layer. A governed directory with no pipeline running it is the
        // shape of a repository that looks enforced and is not.
        using var solution = Scratch.Create();
        await Adopt(solution);

        foreach (string workflow in Directory.EnumerateFiles(
            Path.Combine(solution.Root, ".github", "workflows")))
        {
            File.Delete(workflow);
        }

        Diagnose.Response report = await Diagnosis(solution);
        Check check = report.Checks.Single(candidate => candidate.Subject == "the guarantee");

        Assert.AreEqual(CheckOutcome.Problem, check.Outcome);
        StringAssert.Contains(check.Detail, "verify-chain");
        StringAssert.Contains(check.Remedy!, "branch policy");
    }

    [TestMethod]
    public async Task The_panel_names_every_check_and_says_which_exit_code_follows()
    {
        // A person reads this when something is wrong, so the exit code is stated rather than left to
        // be inferred from the markers.
        using var solution = Scratch.Create();

        string panel = DiagnosisRenderer.Render(await Diagnosis(solution));

        StringAssert.Contains(panel, "sdlc doctor");
        StringAssert.Contains(panel, "problem");
        StringAssert.Contains(panel, "Exits 1");
        Assert.IsFalse(panel.Contains('→'), "ASCII only -- this is read in a pipeline log.");

        string sound = DiagnosisRenderer.Render(new Diagnose.Response(
        [
            new Check("binary", CheckOutcome.Sound, "sdlc 0.0.0"),
            new Check("the skills", CheckOutcome.Elsewhere, "a host loads these"),
        ]));

        StringAssert.Contains(sound, "no problems");
        StringAssert.Contains(
            sound,
            "elsewhere",
            "The marker has to appear, or the one honest category reads as a pass.");
    }

    [TestMethod]
    public async Task The_panel_ends_with_the_same_drafts_and_renames_adoption_does()
    {
        // Each step ends in a rename, which runs nothing, so what adoption printed is stale the moment
        // the first draft is placed. This is the command that answers "what now" (D-94).
        using var solution = Scratch.Create();

        Assert.IsNull(
            (await Diagnosis(solution)).Setup,
            "A repository nobody adopted has one thing to do, and the adoption check already says it.");

        await Drafted(solution);

        // What the block says is SetupTests'; this holds that doctor reads it afresh and prints it.
        Diagnose.Response both = await Diagnosis(solution);
        string rendered = DiagnosisRenderer.Render(both);

        Assert.AreEqual(2, both.Setup!.Left.Count);
        SetupTests.AssertPrinted(rendered, both.Setup);
        StringAssert.Contains(rendered, "Exits 1", "Neither file is in place, which is the fault; the block is only the order.");

        string sdlc = Path.Combine(solution.Root, Cycle.SdlcDirectoryName);
        File.Move(Path.Combine(sdlc, WriteTechStackDraft.DraftFileName), Path.Combine(sdlc, Cycle.TechStackFileName));

        Diagnose.Response one = await Diagnosis(solution);

        CollectionAssert.AreEqual(
            new[] { Cycle.SolutionFileName },
            one.Setup!.Left.Select(step => step.FileName).ToArray(),
            "A rename runs nothing, so doctor has to see it on disk.");
        SetupTests.AssertPrinted(DiagnosisRenderer.Render(one), one.Setup);
    }

    [TestMethod]
    public async Task A_solution_document_placed_with_the_template_still_in_it_is_a_problem()
    {
        // The failure this replaced: the drafter's prompt was not the placeholder the check knew, so an
        // unfilled draft renamed into place read as written and doctor said nothing was left (D-94).
        using var solution = Scratch.Create();
        await Drafted(solution);

        string sdlc = Path.Combine(solution.Root, Cycle.SdlcDirectoryName);
        File.Move(Path.Combine(sdlc, WriteSolutionDraft.DraftFileName), Path.Combine(sdlc, Cycle.SolutionFileName));
        File.Move(Path.Combine(sdlc, WriteTechStackDraft.DraftFileName), Path.Combine(sdlc, Cycle.TechStackFileName));

        Diagnose.Response report = await Diagnosis(solution);
        Check adoption = report.Checks.Single(check => check.Subject == "adoption");

        Assert.AreEqual(CheckOutcome.Problem, adoption.Outcome);
        StringAssert.Contains(adoption.Detail, "template's text");
        Assert.IsFalse(report.Sound);

        string panel = DiagnosisRenderer.Render(report);
        StringAssert.Contains(panel, ".sdlc/solution.md");
        StringAssert.Contains(panel, "still the template's text");
        Assert.IsFalse(panel.Contains("Nothing is left to place", StringComparison.Ordinal), panel);
    }

    [TestMethod]
    public async Task A_solution_with_no_constraints_of_its_own_is_sound_with_or_without_the_draft()
    {
        // Optional: nothing stands between a solution with no rules of its own and its first cycle.
        using var solution = Scratch.Create();
        await Adopt(solution);

        string draft = Path.Combine(solution.Root, Cycle.SdlcDirectoryName, Setup.ConstraintsDraftFileName);
        Assert.IsTrue(File.Exists(draft), "Adoption drafts it even though it is optional.");

        Diagnose.Response waiting = await Diagnosis(solution);
        Assert.IsTrue(waiting.Sound, string.Join("; ", waiting.Checks.Where(c => c.Outcome == CheckOutcome.Problem).Select(c => c.Detail)));
        StringAssert.Contains(DiagnosisRenderer.Render(waiting), "Nothing is left to place");

        File.Delete(draft);

        Assert.IsTrue((await Diagnosis(solution)).Sound, "Nor does deleting the draft make it a fault.");
    }

    [TestMethod]
    public async Task A_missing_integration_branch_is_a_problem_naming_the_command_that_makes_it()
    {
        using var solution = Scratch.Create();
        await Adopt(solution);

        Diagnose.Response report = await Diagnosis(solution, new FakeRepository());
        Check branch = report.Checks.Single(check => check.Subject == "integration branch");

        Assert.AreEqual(CheckOutcome.Problem, branch.Outcome);
        StringAssert.Contains(branch.Remedy!, "git switch -c development");

        Diagnose.Response remote = await Diagnosis(solution, new FakeRepository { Refs = { "origin/development" } });

        Assert.AreEqual(
            CheckOutcome.Sound,
            remote.Checks.Single(check => check.Subject == "integration branch").Outcome,
            "A clone that has the branch only on the remote has it.");
    }

    [TestMethod]
    public async Task An_adopted_solution_reports_what_its_declared_stack_declares()
    {
        // The second rule source a plan is read against, and until now nothing looked at it. A gate
        // works perfectly over a stack file that says nothing, which is what makes this quiet.
        using var solution = Scratch.Create();
        await Adopt(solution);

        Diagnose.Response report = await Diagnosis(solution);
        Check stack = report.Checks.Single(check => check.Subject == "declared stack");

        Assert.AreEqual(CheckOutcome.Sound, stack.Outcome, stack.Detail);
        StringAssert.Contains(stack.Detail, "rows declared");
    }

    [TestMethod]
    public async Task A_row_that_declares_nothing_is_reported_with_the_concern_it_left_open()
    {
        // An adopted solution's stack file opens with four empty rows and an instruction to fill them
        // in. Nothing ever checked whether anybody did, so CON_15 could be judging every plan against
        // four blanks -- and a plan reaching for anything at all departs from no declared default.
        using var solution = Scratch.Create();
        await Adopt(solution);

        await File.WriteAllTextAsync(
            Path.Combine(solution.Root, Cycle.SdlcDirectoryName, Cycle.TechStackFileName),
            "| Concern | Choice |\n|---|---|\n| api | ASP.NET Core |\n| cache | |\n",
            TestContext.CancellationToken);

        Diagnose.Response report = await Diagnosis(solution);
        Check stack = report.Checks.Single(check => check.Subject == "declared stack");

        Assert.AreEqual(CheckOutcome.Problem, stack.Outcome);
        StringAssert.Contains(stack.Detail, "cache", "Named, or a person has to hunt for the blank row.");
        StringAssert.Contains(stack.Remedy!, "draft techstack");
    }

    [TestMethod]
    public async Task A_declared_stack_carrying_the_options_menu_is_reported()
    {
        // The hazard the two-file design exists to avoid, reached the one way left open: a person
        // pasting the menu into the real file. A declared stack listing answers the solution has not
        // chosen is one a plan can reach into and truthfully not be departing from anything.
        using var solution = Scratch.Create();
        await Adopt(solution);

        string path = Path.Combine(solution.Root, Cycle.SdlcDirectoryName, Cycle.TechStackFileName);

        await File.WriteAllTextAsync(
            path,
            await File.ReadAllTextAsync(path, TestContext.CancellationToken)
                + "\n" + StackOptions.Marker + "\n# Stack options\n",
            TestContext.CancellationToken);

        Diagnose.Response report = await Diagnosis(solution);
        Check stack = report.Checks.Single(check => check.Subject == "declared stack");

        Assert.AreEqual(CheckOutcome.Problem, stack.Outcome);
        StringAssert.Contains(stack.Detail, "options menu");
        StringAssert.Contains(stack.Remedy!, "reports");
    }

    [TestMethod]
    public async Task A_declared_stack_that_is_not_there_says_what_writes_the_draft()
    {
        // Adoption writes none, so this is the state every solution is in between adopting and a person
        // renaming the first draft -- and the state a deleted file leaves. No cycle opens in it.
        using var solution = Scratch.Create();
        await Adopt(solution);

        File.Delete(Path.Combine(solution.Root, Cycle.SdlcDirectoryName, Cycle.TechStackFileName));

        Diagnose.Response report = await Diagnosis(solution);
        Check stack = report.Checks.Single(check => check.Subject == "declared stack");

        Assert.AreEqual(CheckOutcome.Problem, stack.Outcome);
        StringAssert.Contains(stack.Detail, "no cycle opens");
        StringAssert.Contains(stack.Remedy!, "draft techstack");
        Assert.IsFalse(
            stack.Remedy!.Contains("sdlc init", StringComparison.Ordinal),
            "Adoption does not write this file, so sending somebody back to it fixes nothing.");
    }

    [TestMethod]
    public async Task A_declared_stack_with_its_draft_waiting_says_to_fill_it_in_and_rename_it()
    {
        using var solution = Scratch.Create();
        await Drafted(solution);

        Check stack = (await Diagnosis(solution)).Checks.Single(check => check.Subject == "declared stack");

        Assert.AreEqual(CheckOutcome.Problem, stack.Outcome);
        StringAssert.Contains(stack.Remedy!, WriteTechStackDraft.DraftFileName);
        StringAssert.Contains(stack.Remedy!, "rename");
    }

    [TestMethod]
    public async Task An_empty_declared_stack_is_a_decision_and_not_a_fault()
    {
        // A person placed a file with nothing in it: nothing pinned, no stack rule on, every plan
        // choosing freely. This was a problem while adoption wrote the file itself, because then empty
        // meant nobody had got round to it. Now the rename is the statement.
        using var solution = Scratch.Create();
        await Adopt(solution);
        await Declare(solution, string.Empty);

        Diagnose.Response report = await Diagnosis(solution);
        Check stack = report.Checks.Single(check => check.Subject == "declared stack");

        Assert.AreEqual(CheckOutcome.Sound, stack.Outcome, stack.Detail);
        StringAssert.Contains(stack.Detail, "pins nothing");
        StringAssert.Contains(stack.Detail, "chooses freely");
    }

    [TestMethod]
    public async Task The_default_stack_reports_its_rows_and_every_rule_it_switches_on()
    {
        using var solution = Scratch.Create();
        await Adopt(solution);

        Diagnose.Response report = await Diagnosis(solution);
        Check stack = report.Checks.Single(check => check.Subject == "declared stack");

        Assert.AreEqual(CheckOutcome.Sound, stack.Outcome, stack.Detail);
        StringAssert.Contains(stack.Detail, "5 of 5 listed stack rules on");
        Assert.IsFalse(
            stack.Detail.Contains("also ships", StringComparison.Ordinal),
            "Every shipped rule is listed, so there is nothing unlisted to mention.");
    }

    [TestMethod]
    public async Task A_shipped_rule_the_stack_does_not_list_is_information_rather_than_fault()
    {
        // What is listed is what binds, so a release adding a rule changes nothing for a solution
        // already adopted until somebody adds the row. Worth saying, so the rule is declined rather
        // than merely unseen; not worth failing, because not being held to it is a solution's to choose.
        using var solution = Scratch.Create();
        await Adopt(solution);
        await Declare(solution, "| api | ASP.NET Core |\n| `PRO_01` | true | browser |\n| `PRO_02` | false | kernel |\n");

        Diagnose.Response report = await Diagnosis(solution);
        Check stack = report.Checks.Single(check => check.Subject == "declared stack");

        Assert.AreEqual(CheckOutcome.Sound, stack.Outcome, stack.Detail);
        StringAssert.Contains(stack.Detail, "1 of 2 listed stack rules on");
        StringAssert.Contains(stack.Detail, "PRO_03", "The one it does not list, by name.");
    }

    [TestMethod]
    public async Task A_rule_row_naming_an_id_nothing_ships_is_reported()
    {
        // The quiet one: a row that reads as a solution being held to a rule and holds it to nothing.
        using var solution = Scratch.Create();
        await Adopt(solution);
        await Declare(solution, "| api | ASP.NET Core |\n| `PRO_91` | true | a rule nobody wrote |\n");

        Diagnose.Response report = await Diagnosis(solution);
        Check stack = report.Checks.Single(check => check.Subject == "declared stack");

        Assert.AreEqual(CheckOutcome.Problem, stack.Outcome);
        StringAssert.Contains(stack.Detail, "PRO_91");
        StringAssert.Contains(stack.Remedy!, "draft techstack");
    }

    [TestMethod]
    public async Task A_stack_that_lists_no_rule_is_told_it_is_held_to_none_whatever_the_old_line_says()
    {
        // An old profile line no longer selects anything (D-81), so a solution still carrying one is
        // told plainly that it is held to none of the rules this release ships.
        using var solution = Scratch.Create();
        await Adopt(solution);
        await Declare(solution, "| Concern | Choice |\n|---|---|\n| ORM | EF Core |\n");

        await File.WriteAllTextAsync(
            Sdlc.Core.Solutions.SolutionDocument.PathIn(solution.Root),
            "---\nprofile: dotnet-default\norigin: adopted\n---\n",
            TestContext.CancellationToken);

        Diagnose.Response report = await Diagnosis(solution);
        Check stack = report.Checks.Single(check => check.Subject == "declared stack");

        Assert.AreEqual(CheckOutcome.Sound, stack.Outcome, stack.Detail);
        StringAssert.Contains(stack.Detail, "does not mention");
        StringAssert.Contains(stack.Detail, "PRO_01");
        Assert.IsFalse(stack.Detail.Contains("dotnet-default", StringComparison.Ordinal), "The old line is not read.");
    }

    [TestMethod]
    public async Task Framework_files_this_release_wrote_are_sound()
    {
        using var solution = Scratch.Create();
        await Adopt(solution);

        Check stamp = (await Diagnosis(solution)).Checks.Single(check => check.Subject == "framework files");

        Assert.AreEqual(CheckOutcome.Sound, stamp.Outcome, stamp.Detail);
        StringAssert.Contains(stamp.Detail, Sdlc.Core.Release.Version);
    }

    [TestMethod]
    public async Task Framework_files_an_older_release_wrote_ask_a_person_to_run_init()
    {
        // The upgrade that reached the machine and not the repository: hooks and a pipeline a release
        // behind the binary judging the cycles, and nothing else that could tell.
        using var solution = Scratch.Create();
        await Adopt(solution);
        await Restamp(solution, FrameworkStamp.Line("0.0.1"));

        Check stamp = (await Diagnosis(solution)).Checks.Single(check => check.Subject == "framework files");

        Assert.AreEqual(CheckOutcome.Problem, stamp.Outcome);
        StringAssert.Contains(stamp.Detail, "0.0.1");
        StringAssert.Contains(stamp.Remedy!, "a person runs 'sdlc init'");
    }

    [TestMethod]
    public async Task Framework_files_from_before_stamps_are_an_older_release()
    {
        using var solution = Scratch.Create();
        await Adopt(solution);
        await Restamp(solution, string.Empty);

        Check stamp = (await Diagnosis(solution)).Checks.Single(check => check.Subject == "framework files");

        Assert.AreEqual(CheckOutcome.Problem, stamp.Outcome);
        StringAssert.Contains(stamp.Detail, "no release stamp");
        StringAssert.Contains(stamp.Remedy!, "sdlc init");
    }

    [TestMethod]
    public async Task Framework_files_a_newer_release_wrote_ask_for_the_binary_rather_than_init()
    {
        // Re-running init with the older binary would roll the hooks back; the remedy is the other half.
        using var solution = Scratch.Create();
        await Adopt(solution);
        await Restamp(solution, FrameworkStamp.Line("999.0.0"));

        Check stamp = (await Diagnosis(solution)).Checks.Single(check => check.Subject == "framework files");

        Assert.AreEqual(CheckOutcome.Problem, stamp.Outcome);
        StringAssert.Contains(stamp.Remedy!, "upgrade the binary");
    }

    [TestMethod]
    public async Task A_default_the_stack_never_mentions_is_named_and_one_answered_none_is_not()
    {
        using var solution = Scratch.Create();
        await Adopt(solution);

        string stack = string.Join(
            '\n',
            Templates.TechStack().ReplaceLineEndings("\n").Split('\n')
                .Where(line => !line.StartsWith("| accessibility testing |", StringComparison.Ordinal)))
            .Replace("| ORM | EF Core |", "| ORM | none |", StringComparison.Ordinal);

        await Declare(solution, stack);

        Check check = (await Diagnosis(solution)).Checks.Single(c => c.Subject == "declared stack");

        Assert.AreEqual(CheckOutcome.Sound, check.Outcome, "What was never asked is information, not a fault.");
        StringAssert.Contains(check.Detail, "does not mention accessibility testing");
        Assert.IsFalse(check.Detail.Contains("ORM", StringComparison.Ordinal), "A row that says none is an answer.");
    }

    /// <summary>The test context, for the cancellation token the analysers require to be passed on.</summary>
    public TestContext TestContext { get; set; } = null!;

    private static async Task Restamp(Scratch solution, string line)
    {
        string path = Path.Combine(solution.Root, Cycle.SdlcDirectoryName, "README.md");
        string text = await File.ReadAllTextAsync(path);

        await File.WriteAllTextAsync(path, text.Replace(FrameworkStamp.Line(Sdlc.Core.Release.Version), line, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_platform_that_requires_the_check_makes_the_policy_sound()
    {
        using var solution = Scratch.Create();
        await Adopt(solution);

        Diagnose.Response report = await Diagnosis(
            solution,
            platform: new PolicyAnswer(PolicyFinding.Required, "a merge into development requires the 'verify' status check"));
        Check policy = report.Checks.Single(check => check.Subject == "branch policy");

        Assert.AreEqual(CheckOutcome.Sound, policy.Outcome);
        StringAssert.Contains(policy.Detail, "requires the 'verify' status check");
        Assert.IsTrue(report.Sound);
    }

    [TestMethod]
    public async Task A_platform_that_answered_and_requires_nothing_is_a_problem()
    {
        // The case nothing in the repository can show: a policy switched off, or never set. The pipeline
        // file is still there and still runs the chain, so every other check reads sound.
        using var solution = Scratch.Create();
        await Adopt(solution);

        Diagnose.Response report = await Diagnosis(
            solution,
            platform: new PolicyAnswer(PolicyFinding.NotRequired, "development requires no status check"));
        Check policy = report.Checks.Single(check => check.Subject == "branch policy");

        Assert.AreEqual(CheckOutcome.Problem, policy.Outcome);
        Assert.IsFalse(report.Sound, "The guarantee is not in force, which is what the exit code is for.");
        StringAssert.Contains(policy.Remedy!, "branch protection rule or ruleset");
    }

    [TestMethod]
    public async Task A_platform_that_could_not_be_asked_is_a_warning_that_says_how_to_let_it_ask()
    {
        // Not signed in is the person running the doctor, not a broken install, so it never fails the exit
        // code -- and it never reads as sound either, because nobody established it.
        using var solution = Scratch.Create();
        await Adopt(solution, Platform.AzureDevOps);

        Diagnose.Response report = await Diagnosis(
            solution,
            platform: new PolicyAnswer(PolicyFinding.Unasked, "'az repos show' failed: Please run 'az login' to setup account."));
        Check policy = report.Checks.Single(check => check.Subject == "branch policy");

        Assert.AreEqual(CheckOutcome.Elsewhere, policy.Outcome);
        StringAssert.Contains(policy.Detail, "could not be asked");
        StringAssert.Contains(policy.Detail, "az login");
        StringAssert.Contains(policy.Remedy!, "Sign in with 'az login'");
        Assert.IsTrue(report.Sound);
    }

    [TestMethod]
    public void An_azure_policy_list_yields_only_enabled_blocking_build_validations()
    {
        const string listed = """
            [
              { "isEnabled": true,  "isBlocking": true,  "type": { "displayName": "Build" }, "settings": { "buildDefinitionId": 12 } },
              { "isEnabled": true,  "isBlocking": false, "type": { "displayName": "Build" }, "settings": { "buildDefinitionId": 13 } },
              { "isEnabled": false, "isBlocking": true,  "type": { "displayName": "Build" }, "settings": { "buildDefinitionId": 14 } },
              { "isEnabled": true,  "isBlocking": true,  "type": { "displayName": "Minimum number of reviewers" }, "settings": { "minimumApproverCount": 1 } }
            ]
            """;

        CollectionAssert.AreEqual(new[] { 12 }, CliBranchPolicies.BlockingBuildPolicies(listed).ToArray());
    }

    [TestMethod]
    public void A_github_branch_names_its_required_checks_from_protection_and_from_rulesets()
    {
        const string branch = """
            { "name": "development", "protected": true,
              "protection": { "enabled": true, "required_status_checks": { "enforcement_level": "everyone", "contexts": ["build"], "checks": [ { "context": "verify", "app_id": null } ] } } }
            """;
        const string off = """
            { "name": "development", "protected": true,
              "protection": { "enabled": true, "required_status_checks": { "enforcement_level": "off", "contexts": ["verify"] } } }
            """;
        const string rules = """
            [ { "type": "pull_request" },
              { "type": "required_status_checks", "parameters": { "required_status_checks": [ { "context": "verify" } ] } } ]
            """;

        CollectionAssert.AreEquivalent(new[] { "build", "verify" }, CliBranchPolicies.ProtectedContexts(branch).ToArray());
        Assert.AreEqual(0, CliBranchPolicies.ProtectedContexts(off).Count, "Protection switched off requires nothing.");
        Assert.AreEqual(0, CliBranchPolicies.ProtectedContexts("""{ "name": "development", "protected": false }""").Count);
        CollectionAssert.AreEqual(new[] { "verify" }, CliBranchPolicies.RulesetContexts(rules).ToArray());
    }

    [TestMethod]
    [DataRow("https://dev.azure.com/org/Project/_git/MarketSense", "MarketSense")]
    [DataRow("https://org@dev.azure.com/org/Project/_git/Market%20Sense", "Market Sense")]
    [DataRow("https://org.visualstudio.com/Project/_git/MarketSense/", "MarketSense")]
    [DataRow("git@ssh.dev.azure.com:v3/org/Project/MarketSense", "MarketSense")]
    [DataRow("https://github.com/MofonCode/ai-sdlc.git", null)]
    public void An_azure_repos_remote_names_its_repository(string remote, string? expected) =>
        Assert.AreEqual(expected, CliBranchPolicies.RepositoryName(remote));

    /// <summary>Every host the enum declares, so the per-host check runs per host.</summary>
    public static IEnumerable<object[]> Hosts =>
        Enum.GetValues<HookHost>().Select(host => new object[] { host });

    private static async Task<Diagnose.Response> Diagnosis(Scratch solution, FakeRepository? git = null, PolicyAnswer? platform = null)
    {
        // Adoption creates the integration branch after its commit, so a repository it finished with has
        // one. A test about a missing branch passes a repository without it.
        using var gate = Gate.For(
            solution.Root,
            services =>
            {
                services.AddSingleton<IRepository>(git ?? new FakeRepository { Refs = { SolutionDocument.DefaultIntegrationBranch } });

                if (platform is not null)
                {
                    services.AddSingleton<IBranchPolicies>(new AnsweredBranchPolicies(platform));
                }
            });

        Result<Diagnose.Response> result = await gate
            .Send<Diagnose.Query, Diagnose.Response>(new Diagnose.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static async Task Adopt(Scratch solution, Platform platform = Platform.GitHub)
    {
        await Drafted(solution, platform);

        // Adoption places nothing a person writes; it drafts them. Every check here is about a solution
        // somebody finished adopting, so the paragraph is written and both drafts are placed the way a
        // rename would place them -- the stack as the default, the drafts gone.
        string sdlc = Path.Combine(solution.Root, Cycle.SdlcDirectoryName);
        string draft = Path.Combine(sdlc, WriteSolutionDraft.DraftFileName);

        await File.WriteAllTextAsync(Path.Combine(sdlc, Cycle.SolutionFileName), Written(await File.ReadAllTextAsync(draft)));
        File.Delete(draft);
        File.Delete(Path.Combine(sdlc, WriteTechStackDraft.DraftFileName));

        await Declare(solution, Templates.TechStack());
    }

    /// <summary>Adopts and stops there: the drafts waiting, nothing a person writes in place.</summary>
    private static async Task Drafted(Scratch solution, Platform platform = Platform.GitHub)
    {
        using var gate = Gate.For(
            solution.Root,
            services => services.AddSingleton<IRepository>(new FakeRepository()));

        Result<Initialise.Response> result = await gate
            .Send<Initialise.Command, Initialise.Response>(
                new Initialise.Command(platform));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    /// <summary>A solution document with its prompt replaced by a paragraph, as a person would leave it.</summary>
    private static string Written(string draft)
    {
        int from = draft.IndexOf(WriteSolutionDraft.Placeholder, StringComparison.Ordinal);
        int to = draft.IndexOf("-->", from, StringComparison.Ordinal) + "-->".Length;

        return draft[..from] + "A die, rolled over HTTP, for anyone settling an argument." + draft[to..];
    }

    private static Task Declare(Scratch solution, string stack) => File.WriteAllTextAsync(
        Path.Combine(solution.Root, Cycle.SdlcDirectoryName, Cycle.TechStackFileName),
        stack);

    /// <summary>A scratch directory to adopt into and then inspect.</summary>
    private sealed class Scratch : IDisposable
    {
        private Scratch(string root) => Root = root;

        public string Root { get; }

        public static Scratch Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "sdlc-doctor-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new Scratch(root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
                // A scratch directory a virus scanner still has open. Nothing here depends on it going.
            }
        }
    }

    private sealed class AnsweredBranchPolicies(PolicyAnswer answer) : IBranchPolicies
    {
        public Task<PolicyAnswer> Ask(Platform platform, string branch, string pipelineFile, string workingDirectory, CancellationToken cancellationToken) =>
            Task.FromResult(answer);
    }
}
