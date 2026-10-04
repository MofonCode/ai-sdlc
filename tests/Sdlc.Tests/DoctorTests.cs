using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Diagnostics;
using Sdlc.Core.Git;
using Sdlc.Core.Hooks;
using Sdlc.Core.Init;

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

        // The honest category, and the reason `Sound` ignores it. Both of these are true of a correct
        // install, so counting either as a problem would make every correct install exit non-zero --
        // and counting either as sound would claim something nobody established.
        string[] elsewhere =
        [
            .. report.Checks
                .Where(check => check.Outcome == CheckOutcome.Elsewhere)
                .Select(check => check.Subject),
        ];

        CollectionAssert.AreEquivalent(
            new[] { "the pre-push hook", "the skills" },
            elsewhere,
            "A binary cannot ask a host what it loaded, and cannot read another clone's git config.");
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
                new Initialise.Command("dotnet-default", Platform.GitHub));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

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
