using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// Which cycle a skill run without an id means: the ones this working copy carries that are ready for its step,
/// one used and several asked about, never picked (D-142).
/// </summary>
[TestClass]
public sealed class ResolveCycleTests
{
    private const string Second = "20261103_0900_01";

    [TestMethod]
    public async Task One_cycle_ready_for_the_step_is_the_one_used_and_printed_alone()
    {
        using CycleWorkspace workspace = ClearIntent();

        ResolveCycle.Response resolved = await Resolve(workspace, new FakeRepository { Branch = "sandbox/x" }, NextStep.Spec);

        Assert.AreEqual(workspace.Cycle.Id, resolved.Only);
        Assert.AreEqual(workspace.Cycle.Id + Environment.NewLine, ResolveRenderer.Render(resolved));
    }

    [TestMethod]
    public async Task A_cycle_not_ready_for_the_step_is_carried_and_not_offered()
    {
        using CycleWorkspace workspace = ClearIntent();

        ResolveCycle.Response resolved = await Resolve(workspace, new FakeRepository { Branch = "sandbox/x" }, NextStep.Plan);

        Assert.IsNull(resolved.Only);
        Assert.AreEqual(1, resolved.Carried.Count);
        StringAssert.Contains(ResolveRenderer.Render(resolved), "No cycle sandbox/x carries is ready for plan");
    }

    [TestMethod]
    public async Task Several_ready_are_listed_to_ask_about_and_none_is_picked()
    {
        using CycleWorkspace workspace = ClearIntent();
        Second_cycle_with_a_clear_intent(workspace);

        ResolveCycle.Response resolved = await Resolve(workspace, new FakeRepository { Branch = "sandbox/x" }, NextStep.Spec);

        Assert.IsNull(resolved.Only);
        Assert.AreEqual(2, resolved.Ready.Count);
        StringAssert.Contains(ResolveRenderer.Render(resolved), "Ask which; never pick one");
    }

    [TestMethod]
    public async Task On_a_cycle_branch_only_that_cycle_is_carried()
    {
        using CycleWorkspace workspace = ClearIntent();
        Second_cycle_with_a_clear_intent(workspace);

        ResolveCycle.Response resolved = await Resolve(
            workspace,
            new FakeRepository { Branch = "cycle/" + Second + "/plan" },
            NextStep.Spec);

        Assert.AreEqual(Second, resolved.Only.ToString());
    }

    [TestMethod]
    public async Task A_cycle_already_on_the_integration_branch_is_not_carried_by_this_one()
    {
        // What a branch carries is what it added: a sandbox holds the cycles it opened, not every cycle merged.
        using CycleWorkspace workspace = ClearIntent();
        Second_cycle_with_a_clear_intent(workspace);
        FakeRepository git = new() { Branch = "sandbox/x" };
        git.Refs.Add("origin/development");
        git.PathsAt.Add("origin/development:.sdlc/cycles/" + workspace.Cycle.Id + "/intent.md");

        ResolveCycle.Response resolved = await Resolve(workspace, git, NextStep.Spec);

        Assert.AreEqual(Second, resolved.Only.ToString());
        Assert.AreEqual(1, resolved.Carried.Count);
    }

    [TestMethod]
    public async Task An_abandoned_cycle_is_never_a_candidate()
    {
        using CycleWorkspace workspace = ClearIntent();
        workspace.Abandon();

        ResolveCycle.Response resolved = await Resolve(workspace, new FakeRepository { Branch = "sandbox/x" }, NextStep.Any);

        Assert.AreEqual(0, resolved.Carried.Count);
    }

    [TestMethod]
    public async Task A_cycle_stopped_mid_loop_is_ready_to_be_taken_to_plan_and_one_whose_plan_is_clean_is_not()
    {
        // to-plan takes a cycle up wherever its planning stopped, so a spec with no clean round is ready for it,
        // where the spec step, which writes a spec, is not (D-158).
        using CycleWorkspace workspace = ClearIntent();
        workspace.WriteArtifact(ArtifactKind.Spec);
        FakeRepository git = new() { Branch = "sandbox/x" };

        Assert.AreEqual(workspace.Cycle.Id, (await Resolve(workspace, git, NextStep.ToPlan)).Only);
        Assert.IsNull((await Resolve(workspace, git, NextStep.Spec)).Only);

        workspace.WriteCleanRound(ArtifactKind.Spec);
        workspace.WriteArtifact(ArtifactKind.Plan);
        workspace.WriteCleanRound(ArtifactKind.Plan);

        Assert.IsNull((await Resolve(workspace, git, NextStep.ToPlan)).Only);
    }

    [TestMethod]
    [DataRow("spec", NextStep.Spec)]
    [DataRow("Remediate", NextStep.Remediate)]
    [DataRow("to-plan", NextStep.ToPlan)]
    [DataRow("pr", NextStep.Pr)]
    public void A_step_is_read_by_its_name(string text, NextStep expected)
    {
        Assert.IsTrue(ResolveCycle.TryParseStep(text, out NextStep step));
        Assert.AreEqual(expected, step);
    }

    [TestMethod]
    [DataRow("intent")]
    [DataRow("2")]
    [DataRow(null)]
    public void Anything_else_is_not_a_step(string? text) =>
        Assert.IsFalse(ResolveCycle.TryParseStep(text, out _));

    private static CycleWorkspace ClearIntent()
    {
        var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteCleanRound(ArtifactKind.Intent);
        return workspace;
    }

    private static void Second_cycle_with_a_clear_intent(CycleWorkspace workspace)
    {
        Assert.IsTrue(CycleId.TryParse(Second, out CycleId? id));
        Cycle cycle = new(id.Value, workspace.Root);

        File.Copy(workspace.Cycle.PathOf(ArtifactKind.Intent), Path.Combine(Directory.CreateDirectory(cycle.Directory).FullName, "intent.md"));
        Directory.CreateDirectory(cycle.FindingsDirectory);

        foreach (string round in Directory.GetFiles(workspace.Cycle.FindingsDirectory))
        {
            File.Copy(round, Path.Combine(cycle.FindingsDirectory, Path.GetFileName(round)));
        }
    }

    private static async Task<ResolveCycle.Response> Resolve(CycleWorkspace workspace, FakeRepository git, NextStep step)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        Result<ResolveCycle.Response> result = await gate.Send<ResolveCycle.Query, ResolveCycle.Response>(new ResolveCycle.Query(step));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
