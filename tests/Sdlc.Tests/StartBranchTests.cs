using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// Where each kind of work on a cycle starts (D-83, D-85).
/// </summary>
[TestClass]
public sealed class StartBranchTests
{
    private const string Id = "20260929_1456_32";
    private const string Plan = ".sdlc/cycles/" + Id + "/plan.md";

    [TestMethod]
    public async Task A_plan_starts_from_the_integration_branch()
    {
        FakeRepository git = new();
        git.Refs.Add("origin/development");

        StartBranch.Response start = await Ask(git, CycleBranchKind.Plan);

        Assert.AreEqual("cycle/" + Id + "/plan", start.Branch);
        Assert.AreEqual("git switch --no-track -c cycle/" + Id + "/plan origin/development", start.Command);
    }

    [TestMethod]
    public async Task With_no_integration_branch_a_plan_starts_from_what_is_checked_out()
    {
        StartBranch.Response start = await Ask(new FakeRepository(), CycleBranchKind.Plan);

        Assert.AreEqual("git switch -c cycle/" + Id + "/plan", start.Command);
    }

    [TestMethod]
    [DataRow(CycleBranchKind.Build)]
    [DataRow(CycleBranchKind.BuildUnattended)]
    public async Task A_build_starts_from_the_integration_branch_once_the_plan_is_admitted_to_it(CycleBranchKind kind)
    {
        FakeRepository git = new();
        git.Refs.Add("origin/development");
        git.Refs.Add("cycle/" + Id + "/plan");
        git.PathsAt.Add("origin/development:" + Plan);

        StartBranch.Response start = await Ask(git, kind);

        Assert.AreEqual(
            "git switch --no-track -c " + start.Branch + " origin/development",
            start.Command,
            "Admitted, so the plan branch is ignored even though it still exists.");
    }

    [TestMethod]
    public async Task Before_the_plan_is_admitted_a_build_starts_from_the_plan_branch()
    {
        // A solution with no integration branch still works: one pull request carries the plan and the code.
        FakeRepository git = new();
        git.Refs.Add("origin/cycle/" + Id + "/plan");

        StartBranch.Response start = await Ask(git, CycleBranchKind.Build);

        Assert.AreEqual("git switch --no-track -c cycle/" + Id + "/build origin/cycle/" + Id + "/plan", start.Command);
    }

    [TestMethod]
    public async Task An_older_cycle_on_the_bare_branch_keeps_building_there()
    {
        FakeRepository git = new();
        git.Refs.Add("cycle/" + Id);

        StartBranch.Response start = await Ask(git, CycleBranchKind.Build);

        Assert.AreEqual("git switch cycle/" + Id, start.Command);
    }

    [TestMethod]
    public async Task A_branch_that_exists_is_switched_to_rather_than_created()
    {
        FakeRepository git = new();
        git.Refs.Add("origin/development");
        git.Refs.Add("cycle/" + Id + "/build");

        StartBranch.Response start = await Ask(git, CycleBranchKind.Build);

        Assert.AreEqual("git switch cycle/" + Id + "/build", start.Command);
    }

    [TestMethod]
    public async Task A_build_whose_plan_is_nowhere_is_refused_with_where_it_looked()
    {
        Result<StartBranch.Response> result = await Send(new FakeRepository(), CycleBranchKind.Build);

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "development");
    }

    [TestMethod]
    public async Task A_build_starts_from_the_admission_branch_that_carries_its_plan()
    {
        // D-162. An admission of several cycles is named for the sandbox, so looking for this cycle's own
        // plan branch never found it.
        FakeRepository git = new();
        git.Refs.Add("origin/development");
        git.Refs.Add("slices/sandbox-20260929-1400-00/plan");
        git.PathsAt.Add("slices/sandbox-20260929-1400-00/plan:" + Plan);

        StartBranch.Response start = await Ask(git, CycleBranchKind.Build);

        Assert.AreEqual("git switch --no-track -c cycle/" + Id + "/build slices/sandbox-20260929-1400-00/plan", start.Command);
    }

    [TestMethod]
    public async Task A_plan_only_in_a_sandbox_names_admit_and_builds_from_it_only_with_nothing_to_admit_to()
    {
        FakeRepository git = new();
        git.Refs.Add("sandbox/20260929_1400_00");
        git.PathsAt.Add("sandbox/20260929_1400_00:" + Plan);

        StartBranch.Response start = await Ask(git, CycleBranchKind.Build);
        Assert.AreEqual("git switch --no-track -c cycle/" + Id + "/build sandbox/20260929_1400_00", start.Command, "Admit says so when there is no integration branch.");

        git.Refs.Add("origin/development");
        Result<StartBranch.Response> refused = await Send(git, CycleBranchKind.Build);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "sdlc admit");
    }

    [TestMethod]
    public async Task A_fix_for_a_release_builds_from_the_release_and_takes_its_documents_from_the_plan()
    {
        // D-88: the intent names the release; the code starts there and the cycle's documents come along.
        FakeRepository git = new();
        git.Refs.Add("origin/development");
        git.Refs.Add("origin/release/1.4");
        git.Refs.Add("cycle/" + Id + "/plan");

        Result<StartBranch.Response> result = await Send(git, CycleBranchKind.Build, release: "release/1.4");

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.AreEqual(
            "git switch --no-track -c cycle/" + Id + "/build origin/release/1.4 && git checkout cycle/" + Id + "/plan -- .sdlc/cycles/" + Id,
            result.Value.Command);
    }

    [TestMethod]
    public async Task A_forward_needs_an_integration_branch_to_reach()
    {
        Result<StartBranch.Response> result = await Send(new FakeRepository(), CycleBranchKind.Forward);

        Assert.IsTrue(result.IsFailed);
    }

    [TestMethod]
    public async Task A_plan_in_its_own_worktree_opens_beside_the_repository_on_a_new_branch()
    {
        // D-105: several cycles planned at once, one folder each, so nothing uncommitted rides between them.
        FakeRepository git = new();
        git.Refs.Add("origin/development");

        Result<StartBranch.Response> result = await Send(git, CycleBranchKind.Plan, worktree: true);

        StringAssert.StartsWith(result.Value.Command, "git worktree add -b cycle/" + Id + "/plan ../");
        StringAssert.Contains(result.Value.Command, ".cycles/" + Id + " origin/development");
        StringAssert.Contains(result.Value.Command, "mkdir -p");
    }

    [TestMethod]
    public async Task A_sandbox_opens_beside_the_repository_on_a_branch_of_its_own_kind()
    {
        // D-144: one worktree for every cycle the work turns into, named for the first.
        FakeRepository git = new();
        git.Refs.Add("origin/development");

        Result<StartBranch.Response> result = await Send(git, CycleBranchKind.Plan, sandbox: true);

        Assert.AreEqual("sandbox/" + Id, result.Value.Branch);
        StringAssert.StartsWith(result.Value.Command, "git worktree add -b sandbox/" + Id + " ../");
        StringAssert.Contains(result.Value.Command, ".sandboxes/" + Id + " origin/development");
        StringAssert.Contains(result.Value.Command, "mkdir -p");
    }

    [TestMethod]
    public async Task A_sandbox_switches_nothing_so_uncommitted_work_does_not_hold_it_shut()
    {
        Result<StartBranch.Response> result = await Send(new FakeRepository { Uncommitted = ["src/Half.cs"] }, CycleBranchKind.Plan, sandbox: true);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public async Task A_plan_switch_over_uncommitted_work_is_refused_naming_it_and_offering_a_sandbox()
    {
        // The switch would carry the changes into the cycle as work no plan declared (D-144, D-94).
        Result<StartBranch.Response> result = await Send(new FakeRepository { Uncommitted = [".sdlc/techstack.md", "src/Half.cs"] }, CycleBranchKind.Plan);

        Assert.IsTrue(result.IsFailed, "A plan branch was switched to over uncommitted work.");
        string message = result.Errors[0].Message;
        StringAssert.Contains(message, ".sdlc/techstack.md");
        StringAssert.Contains(message, "src/Half.cs");
        StringAssert.Contains(message, "Commit, stash or discard");
        StringAssert.Contains(message, "sdlc branch " + Id + " sandbox");
    }

    [TestMethod]
    public async Task The_frameworks_own_ignore_files_do_not_hold_a_plan_switch_shut()
    {
        // Opening a cycle writes the cycles directory's ignore, and a report its own; neither is anybody's work.
        Result<StartBranch.Response> result = await Send(
            new FakeRepository { Uncommitted = [".sdlc/cycles/.gitignore", ".sdlc/reports/.gitignore"] },
            CycleBranchKind.Plan);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public async Task A_worktree_is_only_for_planning()
    {
        Result<StartBranch.Response> result = await Send(new FakeRepository(), CycleBranchKind.Build, worktree: true);

        Assert.IsTrue(result.IsFailed);
    }

    private static async Task<StartBranch.Response> Ask(FakeRepository git, CycleBranchKind kind)
    {
        Result<StartBranch.Response> result = await Send(git, kind);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static async Task<Result<StartBranch.Response>> Send(FakeRepository git, CycleBranchKind kind, string? release = null, bool worktree = false, bool sandbox = false)
    {
        using var workspace = CycleWorkspace.Create(Id);

        if (release is not null)
        {
            CycleWorkspace.WriteRaw(
                workspace.Cycle.PathOf(ArtifactKind.Intent),
                "---\ncycle: " + Id + "\ntype: bug fix\nrelease: " + release + "\n---\n\n# A fix\n");
        }

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Assert.IsTrue(CycleId.TryParse(Id, out CycleId? id));

        return await gate.Send<StartBranch.Query, StartBranch.Response>(new StartBranch.Query(id.Value, kind, worktree, sandbox));
    }
}
