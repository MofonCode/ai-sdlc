using FluentResults;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Microsoft.Extensions.DependencyInjection;

namespace Sdlc.Tests;

/// <summary>
/// The plans sliced together are compared before admission, and admitted by one pull request (D-125).
/// </summary>
[TestClass]
public sealed class SlicingTests
{
    private const string A = "20261103_0900_00";
    private const string B = "20261103_0900_01";

    [TestMethod]
    public void A_row_whose_change_reads_new_creates_its_file_and_one_that_changes_a_file_does_not()
    {
        string plan = """
            ## Files

            | File | Change |
            |---|---|
            | `src/Totals/TotalsService.cs` | new: sums the month |
            | `src/Export/ExportPage.razor` | adds the totals line, using `TotalsService.cs` |
            | `tests/Totals/TotalsTests.cs` | **New** |

            - `src/Totals/Totals.css` — new, the totals' styles
            - `src/Export/Export.css` — one rule renamed
            """;

        CollectionAssert.AreEqual(
            new[] { "src/Totals/Totals.css", "src/Totals/TotalsService.cs", "tests/Totals/TotalsTests.cs" },
            DeclaredFiles.Created(plan).ToArray());
    }

    [TestMethod]
    public void Independent_siblings_are_atomic_and_share_nothing()
    {
        ComputeClaimGraph.SlicingComparison compared = ComputeClaimGraph.CompareSlicing(
            [Plan(A, ["src/Totals.cs"], ["src/Totals.cs"]), Plan(B, ["src/Chart.cs"], ["src/Chart.cs"])],
            _ => false);

        Assert.IsTrue(compared.Atomic);
        Assert.AreEqual(0, compared.Shared.Count);
    }

    [TestMethod]
    public void A_file_two_plans_both_change_is_reported_and_refuses_nothing()
    {
        ComputeClaimGraph.SlicingComparison compared = ComputeClaimGraph.CompareSlicing(
            [Plan(A, ["src/Export.razor", "src/Totals.cs"], ["src/Totals.cs"]), Plan(B, ["src/Export.razor"], [])],
            path => path == "src/Export.razor");

        Assert.IsTrue(compared.Atomic);
        CollectionAssert.AreEqual(new[] { "src/Export.razor" }, compared.Shared.Single().Files.ToArray());
        Assert.IsTrue(VerifySlicing.Shared(compared).Passed);
    }

    [TestMethod]
    public void A_plan_needing_a_file_only_a_sibling_creates_depends_on_it_and_is_refused()
    {
        ComputeClaimGraph.SlicingComparison compared = ComputeClaimGraph.CompareSlicing(
            [Plan(A, ["src/Totals.cs"], ["src/Totals.cs"]), Plan(B, ["src/Totals.cs", "src/Chart.cs"], ["src/Chart.cs"])],
            _ => false);

        ComputeClaimGraph.Dependency dependency = compared.Dependencies.Single();
        Assert.AreEqual(B, dependency.Dependent.ToString());
        Assert.AreEqual(A, dependency.On.ToString());
        CollectionAssert.AreEqual(new[] { "src/Totals.cs" }, dependency.Files.ToArray());
        Assert.AreEqual(0, compared.Shared.Count, "A dependency is not reported a second time as a shared file.");

        VerifyChain.ChainCheck check = VerifySlicing.Atomic(compared);
        Assert.IsFalse(check.Passed);
        StringAssert.Contains(check.Detail, "make it a successor");
    }

    [TestMethod]
    public void A_file_already_on_the_base_is_no_dependency_whoever_marks_it_new()
    {
        ComputeClaimGraph.SlicingComparison compared = ComputeClaimGraph.CompareSlicing(
            [Plan(A, ["src/Totals.cs"], ["src/Totals.cs"]), Plan(B, ["src/Totals.cs"], [])],
            _ => true);

        Assert.IsTrue(compared.Atomic);
    }

    [TestMethod]
    public void Several_cycles_planning_and_successors_are_planning_and_one_source_file_is_not()
    {
        string[] planning =
        [
            ".sdlc/cycles/" + B + "/plan.md",
            ".sdlc/cycles/" + A + "/intent.md",
            ".sdlc/cycles/" + A + "/findings/plan.json",
            ".sdlc/successors/export-chart.md",
        ];

        CollectionAssert.AreEqual(new[] { A, B }, VerifyChain.PlanningCycles(planning)!.Select(id => id.ToString()).ToArray());
        Assert.IsNull(VerifyChain.PlanningCycles([.. planning, "src/Totals.cs"]));
        Assert.IsNull(VerifyChain.PlanningCycles([.. planning, ".sdlc/cycles/" + A + "/verify.md"]));
        Assert.IsNull(VerifyChain.PlanningCycles([".sdlc/successors/export-chart.md"]), "Successors alone admit no cycle.");
    }

    [TestMethod]
    public void A_cycles_own_plan_may_carry_successors_but_not_alone()
    {
        Assert.IsTrue(CycleId.TryParse(A, out CycleId? id));

        Assert.IsTrue(VerifyChain.AdmitsAsPlan(id.Value, [".sdlc/cycles/" + A + "/plan.md", ".sdlc/successors/chart.md"]));
        Assert.IsFalse(VerifyChain.AdmitsAsPlan(id.Value, [".sdlc/successors/chart.md"]));
    }

    [TestMethod]
    public void Only_slices_slug_plan_is_a_slicing_branch()
    {
        Assert.IsTrue(SlicingBranch.TryParse("refs/heads/slices/export-totals/plan", out string? slug));
        Assert.AreEqual("export-totals", slug);
        Assert.AreEqual("slices/export-totals/plan", SlicingBranch.NameOf("export-totals"));

        Assert.IsFalse(SlicingBranch.TryParse("slices/export-totals", out _));
        Assert.IsFalse(SlicingBranch.TryParse("slices/Export/plan", out _));
        Assert.IsFalse(SlicingBranch.TryParse("cycle/" + A + "/plan", out _));
    }

    [TestMethod]
    public async Task A_slicing_carrying_code_is_refused_before_any_cycle_is_checked()
    {
        using var workspace = CycleWorkspace.Create(A);

        VerifySlicing.Response verdict = await Check(workspace, [".sdlc/cycles/" + A + "/plan.md", "src/Totals.cs"]);

        Assert.IsFalse(verdict.Holds);
        Assert.AreEqual(0, verdict.Cycles.Count);
        StringAssert.Contains(VerifySlicing.Render(verdict), "none of its plans is admitted");
    }

    [TestMethod]
    public async Task An_approved_plan_and_the_successors_it_wrote_are_admitted_together()
    {
        using CycleWorkspace workspace = Approved();
        SuccessorTests.Write(workspace, "export-chart", workspace.Cycle.Id.ToString());
        string root = ".sdlc/cycles/" + workspace.Cycle.Id + "/";

        VerifySlicing.Response verdict = await Check(
            workspace,
            [root + "intent.md", root + "spec.md", root + "plan.md", root + "approval.md", ".sdlc/successors/export-chart.md"]);

        Assert.IsTrue(verdict.Holds, VerifySlicing.Render(verdict));
        Assert.IsTrue(verdict.Cycles.Single().Admission);
        Assert.IsTrue(verdict.Checks.Single(c => c.Name.Contains("successors", StringComparison.Ordinal) && c.Name.Contains("sound", StringComparison.Ordinal)).Passed);
    }

    [TestMethod]
    public async Task A_successor_naming_nothing_refuses_the_slicing()
    {
        using CycleWorkspace workspace = Approved();
        SuccessorTests.Write(workspace, "export-chart", "successor:nowhere");
        string root = ".sdlc/cycles/" + workspace.Cycle.Id + "/";

        VerifySlicing.Response verdict = await Check(workspace, [root + "plan.md", ".sdlc/successors/export-chart.md"]);

        Assert.IsFalse(verdict.Holds);
        StringAssert.Contains(VerifySlicing.Render(verdict), "names what does not exist: successor:nowhere");
    }

    [TestMethod]
    public async Task Composing_a_slicing_merges_each_plan_branch_onto_the_integration_branch()
    {
        using var workspace = CycleWorkspace.Create(A);
        FakeRepository git = Slicing(createdByA: "src/Totals.cs", namedByB: "src/Chart.cs");

        Result<ComposeSlicing.Response> composed = await Compose(workspace, git, A, B);

        Assert.IsTrue(composed.IsSuccess, string.Join("; ", composed.Errors.Select(e => e.Message)));
        Assert.AreEqual("slices/export/plan", composed.Value.Branch);
        Assert.AreEqual(
            "git switch --no-track -c slices/export/plan origin/development && git merge --no-ff --no-edit cycle/" + A + "/plan origin/cycle/" + B + "/plan",
            composed.Value.Command);
    }

    [TestMethod]
    public async Task Composing_refuses_a_plan_that_needs_what_only_a_sibling_creates()
    {
        using var workspace = CycleWorkspace.Create(A);
        FakeRepository git = Slicing(createdByA: "src/Totals.cs", namedByB: "src/Totals.cs");

        Result<ComposeSlicing.Response> composed = await Compose(workspace, git, A, B);

        Assert.IsTrue(composed.IsFailed);
        StringAssert.Contains(composed.Errors[0].Message, B + " needs src/Totals.cs, which only " + A + " creates");
    }

    [TestMethod]
    public async Task Composing_refuses_a_sibling_with_no_plan_branch_and_a_slicing_of_one()
    {
        using var workspace = CycleWorkspace.Create(A);
        FakeRepository git = Slicing(createdByA: "src/Totals.cs", namedByB: "src/Chart.cs");
        git.Refs.Remove("origin/cycle/" + B + "/plan");

        StringAssert.Contains((await Compose(workspace, git, A, B)).Errors[0].Message, B + " has no cycle/" + B + "/plan branch");
        StringAssert.Contains((await Compose(workspace, git, A)).Errors[0].Message, "two cycles or more");
    }

    private static FakeRepository Slicing(string createdByA, string namedByB)
    {
        FakeRepository git = new();
        git.Refs.Add("origin/development");
        git.Refs.Add("cycle/" + A + "/plan");
        git.Refs.Add("origin/cycle/" + B + "/plan");
        git.Files["cycle/" + A + "/plan:.sdlc/cycles/" + A + "/plan.md"] = "## Files\n\n| File | Change |\n|---|---|\n| `" + createdByA + "` | new |\n";
        git.Files["cycle/" + A + "/plan:.sdlc/cycles/" + A + "/approval.md"] = "ledger";
        git.Files["origin/cycle/" + B + "/plan:.sdlc/cycles/" + B + "/plan.md"] = "## Files\n\n| File | Change |\n|---|---|\n| `" + namedByB + "` | adds a chart |\n";
        git.Files["origin/cycle/" + B + "/plan:.sdlc/cycles/" + B + "/approval.md"] = "ledger";
        return git;
    }

    private static async Task<Result<ComposeSlicing.Response>> Compose(CycleWorkspace workspace, FakeRepository git, params string[] ids)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        return await gate.Send<ComposeSlicing.Query, ComposeSlicing.Response>(new ComposeSlicing.Query(
            "export",
            [.. ids.Select(id => CycleId.TryParse(id, out CycleId? parsed) ? parsed.Value : default)]));
    }

    private static ComputeClaimGraph.SlicePlan Plan(string id, string[] declared, string[] created)
    {
        Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed));
        return new ComputeClaimGraph.SlicePlan(parsed.Value, declared, created);
    }

    private static CycleWorkspace Approved()
    {
        var workspace = CycleWorkspace.Create(A);
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteSpec(criteria: 3);
        workspace.WritePlan(proofs: 3);

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        return workspace;
    }

    private static async Task<VerifySlicing.Response> Check(CycleWorkspace workspace, IReadOnlyList<string> changed)
    {
        using var gate = Gate.For(workspace.Root);

        Result<VerifySlicing.Response> result = await gate
            .Send<VerifySlicing.Query, VerifySlicing.Response>(new VerifySlicing.Query("export", changed));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
