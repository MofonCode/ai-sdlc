using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// What leaves a sandbox: its approved cycles, composed onto an admission branch the check already knows (D-144).
/// </summary>
[TestClass]
public sealed class AdmissionTests
{
    private const string Id = "20260929_1456_32";
    private const string Sandbox = "sandbox/" + Id;

    [TestMethod]
    public async Task One_approved_cycle_goes_on_its_own_plan_branch_beside_the_sandbox()
    {
        using CycleWorkspace workspace = Approved();
        FakeRepository git = Git(workspace);

        ComposeAdmission.Response admission = (await Compose(workspace, git)).Value;

        Assert.AreEqual("cycle/" + Id + "/plan", admission.Branch);
        CollectionAssert.AreEqual(new[] { workspace.Cycle.Id }, admission.Admitted.ToArray());
        StringAssert.StartsWith(admission.Command, "git worktree add -b cycle/" + Id + "/plan ../" + Id + ".admit origin/development");
        StringAssert.Contains(admission.Command, "checkout " + Sandbox + " -- .sdlc/cycles/" + Id);
        StringAssert.Contains(admission.Command, "commit -m \"Admit " + Id + " from " + Sandbox + "\"");
    }

    [TestMethod]
    public async Task The_successors_the_sandbox_wrote_go_with_it()
    {
        using CycleWorkspace workspace = Approved();
        FakeRepository git = Git(workspace);
        git.Committed = [".sdlc/successors/totals.md", ".sdlc/cycles/" + Id + "/plan.md"];
        git.PathsAt.Add(Sandbox + ":.sdlc/successors/totals.md");

        ComposeAdmission.Response admission = (await Compose(workspace, git)).Value;

        CollectionAssert.AreEqual(new[] { ".sdlc/successors/totals.md" }, admission.Successors.ToArray());
        StringAssert.Contains(admission.Command, ".sdlc/successors/totals.md");
    }

    [TestMethod]
    public async Task A_cycle_the_gate_does_not_let_through_stays_and_with_none_approved_nothing_is_composed()
    {
        using var workspace = CycleWorkspace.Create(Id);
        workspace.WriteArtifact(ArtifactKind.Intent);

        Result<ComposeAdmission.Response> result = await Compose(workspace, Git(workspace));

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "Nothing in " + Sandbox + " is approved yet");
    }

    [TestMethod]
    public async Task Outside_a_sandbox_it_refuses_and_names_the_plan_route()
    {
        using CycleWorkspace workspace = Approved();
        FakeRepository git = new() { Branch = "cycle/" + Id + "/plan" };
        git.Refs.Add("origin/development");

        Result<ComposeAdmission.Response> result = await Compose(workspace, git);

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "not a sandbox");
    }

    [TestMethod]
    public async Task Uncommitted_changes_in_an_approved_cycle_refuse_because_what_leaves_is_what_was_committed()
    {
        using CycleWorkspace workspace = Approved();
        FakeRepository git = Git(workspace, uncommitted: [".sdlc/cycles/" + Id + "/plan.md"]);

        Result<ComposeAdmission.Response> result = await Compose(workspace, git);

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "uncommitted changes");
    }

    [TestMethod]
    public async Task An_admission_branch_already_there_is_named_rather_than_written_over()
    {
        using CycleWorkspace workspace = Approved();
        FakeRepository git = Git(workspace);
        git.Refs.Add("cycle/" + Id + "/plan");

        Result<ComposeAdmission.Response> result = await Compose(workspace, git);

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "already exists");
    }

    private static CycleWorkspace Approved()
    {
        var workspace = CycleWorkspace.Create(Id);

        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteSpec(criteria: 2);
        workspace.WritePlan(proofs: 2);

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        return workspace;
    }

    private static FakeRepository Git(CycleWorkspace workspace, IReadOnlyList<string>? uncommitted = null)
    {
        FakeRepository git = new() { Branch = Sandbox, Uncommitted = uncommitted ?? [] };
        git.Refs.Add("origin/development");
        string plan = workspace.Cycle.PathOf(ArtifactKind.Plan);

        if (File.Exists(plan))
        {
            git.Files[Sandbox + ":.sdlc/cycles/" + Id + "/plan.md"] = File.ReadAllText(plan);
        }

        return git;
    }

    private static async Task<Result<ComposeAdmission.Response>> Compose(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        return await gate.Send<ComposeAdmission.Query, ComposeAdmission.Response>(new ComposeAdmission.Query());
    }
}
