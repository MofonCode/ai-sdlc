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
    public async Task A_forward_needs_an_integration_branch_to_reach()
    {
        Result<StartBranch.Response> result = await Send(new FakeRepository(), CycleBranchKind.Forward);

        Assert.IsTrue(result.IsFailed);
    }

    private static async Task<StartBranch.Response> Ask(FakeRepository git, CycleBranchKind kind)
    {
        Result<StartBranch.Response> result = await Send(git, kind);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static async Task<Result<StartBranch.Response>> Send(FakeRepository git, CycleBranchKind kind)
    {
        using var workspace = CycleWorkspace.Create(Id);
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Assert.IsTrue(CycleId.TryParse(Id, out CycleId? id));

        return await gate.Send<StartBranch.Query, StartBranch.Response>(new StartBranch.Query(id.Value, kind));
    }
}
