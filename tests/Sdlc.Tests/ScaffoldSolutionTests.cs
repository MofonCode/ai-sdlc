using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// The skeleton, and what has to be true before it may be written.
/// </summary>
/// <remarks>
/// <para>D-35 split this from adoption, and the split is only worth anything if the gate here is real.
/// Adoption writing the solution made the first code in a governed repository the one thing in it with
/// no intent, no spec, no plan and no approval behind it — so these tests are about the refusals rather
/// than the files.</para>
///
/// <para>Each refusal is checked for the reason it carries, not just that it failed. A refusal that does
/// not say which of the four things is missing sends somebody looking for a flag that does not exist.</para>
/// </remarks>
[TestClass]
public sealed class ScaffoldSolutionTests
{
    [TestMethod]
    public async Task An_adopted_solution_is_refused_because_it_already_has_a_shape()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted);
        Approve(workspace);

        Result<ScaffoldSolution.Response> result = await Run(workspace);

        Assert.IsTrue(
            result.IsFailed,
            "An approved plan is not a licence to lay a second layout over a codebase that predates the "
            + "framework. Origin is what decides that, and it decides it here too.");

        StringAssert.Contains(Explain(result), "adopted");
    }

    [TestMethod]
    public async Task An_unapproved_cycle_is_refused_in_the_gates_own_words()
    {
        // The reason has to be the gate's, not a second opinion written here. Two sources for the same
        // refusal drift, and then the two commands disagree about whether a cycle may proceed.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Scaffolded);
        workspace.WriteAllArtifacts();

        Result<ScaffoldSolution.Response> result = await Run(workspace);

        Assert.IsTrue(result.IsFailed, "Nothing has been to the gate, so there is no plan declaring these files.");

        string message = Explain(result);
        StringAssert.Contains(message, "is not approved");
        StringAssert.Contains(message, "draft approval");
    }

    [TestMethod]
    public async Task A_cycle_missing_a_planning_document_is_refused_and_the_document_is_named()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Scaffolded);
        workspace.WriteArtifact(ArtifactKind.Intent);

        Result<ScaffoldSolution.Response> result = await Run(workspace);

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(Explain(result), "spec.md");
    }

    [TestMethod]
    public async Task An_approved_cycle_gets_the_skeleton_and_the_build_settings_it_compiles_under()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Scaffolded);
        Approve(workspace);

        Result<ScaffoldSolution.Response> result = await Run(workspace);

        Assert.IsTrue(result.IsSuccess, Explain(result));

        ScaffoldSolution.Response written = result.Value;
        Assert.AreEqual(0, written.Kept.Count, "Nothing was there to keep.");
        CollectionAssert.AreEquivalent(
            (string[])
            [
                .. Initialise.StackFiles().Select(file => file.Path),
                .. Scaffold.Files(written.Name).Select(file => file.Key),
            ],
            written.Written.ToArray(),
            "What it reports is the skeleton and the build settings it compiles under, so a reviewer "
            + "reading the report is reading the change.");

        foreach (string relative in written.Written)
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(workspace.Root, relative)),
                relative + " was reported as written, so it has to be on disk.");
        }
    }

    [TestMethod]
    public async Task A_second_run_keeps_what_is_there_rather_than_overwriting_it()
    {
        // Overwriting is the one thing a scaffold must not do. By the time this runs twice the first
        // files have been edited -- that is what a reference slice is for -- so a rewrite would silently
        // throw away the work the slice was copied into.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Scaffolded);
        Approve(workspace);

        Result<ScaffoldSolution.Response> first = await Run(workspace);
        Assert.IsTrue(first.IsSuccess, Explain(first));

        string edited = Path.Combine(workspace.Root, first.Value.Written[0]);
        await File.WriteAllTextAsync(edited, "// what somebody did with it\n");

        Result<ScaffoldSolution.Response> second = await Run(workspace);

        Assert.IsTrue(second.IsSuccess, Explain(second));
        Assert.AreEqual(0, second.Value.Written.Count);
        CollectionAssert.AreEquivalent(first.Value.Written.ToArray(), second.Value.Kept.ToArray());
        Assert.AreEqual("// what somebody did with it\n", await File.ReadAllTextAsync(edited));
    }

    [TestMethod]
    public async Task The_build_settings_arrive_with_the_skeleton_and_one_already_there_is_kept()
    {
        // Adoption wrote these when it was told a profile. It is told nothing about the stack now, and
        // they do nothing until there is a project for them to apply to -- so they arrive here, behind
        // the same approved plan as the code they constrain. Created once: a repository that already
        // has its own is not overruled by a skeleton.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Scaffolded);
        Approve(workspace);

        string own = Path.Combine(workspace.Root, ".editorconfig");
        await File.WriteAllTextAsync(own, "# ours\n");

        Result<ScaffoldSolution.Response> result = await Run(workspace);

        Assert.IsTrue(result.IsSuccess, Explain(result));

        foreach ((string path, _) in Initialise.StackFiles().Where(file => file.Path != ".editorconfig"))
        {
            CollectionAssert.Contains(result.Value.Written.ToArray(), path);
            Assert.IsTrue(File.Exists(Path.Combine(workspace.Root, path)), path + " is not on disk.");
        }

        CollectionAssert.Contains(result.Value.Kept.ToArray(), ".editorconfig");
        Assert.AreEqual("# ours\n", await File.ReadAllTextAsync(own));
    }

    [TestMethod]
    public async Task Nothing_about_the_declared_stack_decides_whether_the_skeleton_is_written()
    {
        // A profile name used to: one that shipped no skeleton was refused. No solution records a
        // profile now, and the thing standing in its place is the approval -- a plan declaring these
        // files says the solution is .NET, and a person approved it. Where the declared stack pins
        // something else, that plan had to argue the departure to be approved at all.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Scaffolded);
        workspace.WriteTechStack(string.Empty);
        Approve(workspace);

        Result<ScaffoldSolution.Response> result = await Run(workspace);

        Assert.IsTrue(result.IsSuccess, Explain(result));
    }

    private static async Task<Result<ScaffoldSolution.Response>> Run(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        return await gate.Send<ScaffoldSolution.Command, ScaffoldSolution.Response>(
            new ScaffoldSolution.Command(workspace.Cycle.Id));
    }

    private static string Explain(Result<ScaffoldSolution.Response> result) =>
        string.Join("; ", result.Errors.Select(error => error.Message));

    /// <summary>Takes a cycle all the way to a standing approval the gate accepts.</summary>
    private static void Approve(CycleWorkspace workspace)
    {
        workspace.WriteAllArtifacts();

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
    }
}
