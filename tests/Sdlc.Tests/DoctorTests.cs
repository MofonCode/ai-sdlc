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
            new[] { "the skills" },
            elsewhere,
            "A binary cannot ask a host what it loaded.");
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
            "Adoption does not write this file any more, so sending somebody back to it fixes nothing.");
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
        StringAssert.Contains(stack.Detail, "3 of 3 listed stack rules on");
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
    public async Task A_stack_from_before_rules_were_rows_is_told_where_its_rules_still_come_from()
    {
        // It lists none and is still held to the ones its old profile line names. Saying "0 of 0" and
        // stopping would read as having none, which is the opposite of what the gate does with it.
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
        StringAssert.Contains(stack.Detail, "dotnet-default");
        StringAssert.Contains(stack.Detail, "draft techstack", "And how they become visible in this file.");
    }

    /// <summary>The test context, for the cancellation token the analysers require to be passed on.</summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Every host the enum declares, so the per-host check runs per host.</summary>
    public static IEnumerable<object[]> Hosts =>
        Enum.GetValues<HookHost>().Select(host => new object[] { host });

    private static async Task<Diagnose.Response> Diagnosis(Scratch solution, FakeRepository? git = null)
    {
        using var gate = Gate.For(
            solution.Root,
            services => services.AddSingleton<IRepository>(git ?? new FakeRepository()));

        Result<Diagnose.Response> result = await gate
            .Send<Diagnose.Query, Diagnose.Response>(new Diagnose.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static async Task Adopt(Scratch solution)
    {
        using var gate = Gate.For(
            solution.Root,
            services => services.AddSingleton<IRepository>(new FakeRepository()));

        Result<Initialise.Response> result = await gate
            .Send<Initialise.Command, Initialise.Response>(
                new Initialise.Command(Platform.GitHub));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));

        // Adoption writes no declared stack; a person places one. Every check here is about a solution
        // somebody finished adopting, so the default is placed the way a rename would place it.
        await Declare(solution, Templates.TechStack());
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
}
