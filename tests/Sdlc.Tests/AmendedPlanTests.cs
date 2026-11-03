using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Factory;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// A plan reopened after admission is amended on its plan branch, and a build made before it says so (D-123).
/// </summary>
[TestClass]
public sealed class AmendedPlanTests
{
    private const string Integration = "origin/development";

    [TestMethod]
    public void A_branch_whose_ledger_lacks_the_standing_approval_predates_it()
    {
        using CycleWorkspace workspace = Approved();
        string standing = Ledger(workspace);

        Assert.IsTrue(AmendedPlan.Predates(Earlier(standing), standing));
    }

    [TestMethod]
    public void A_branch_holding_the_standing_approval_does_not_predate_it()
    {
        // The build branch that merged the integration branch carries the amendment, and so does one that never
        // needed it: the same ledger on both sides.
        using CycleWorkspace workspace = Approved();
        string standing = Ledger(workspace);

        Assert.IsFalse(AmendedPlan.Predates(standing, standing));
    }

    [TestMethod]
    public void A_standing_decision_that_is_not_an_approval_amends_nothing()
    {
        // A reopen merged on its own is not an amended plan; the queue already passes the cycle over.
        using CycleWorkspace workspace = Approved();
        string standing = Ledger(workspace);

        Assert.IsFalse(AmendedPlan.Predates(Earlier(standing), standing.Replace("decision: approved", "decision: reopened", StringComparison.Ordinal)));
        Assert.IsFalse(AmendedPlan.Predates(null, standing));
        Assert.IsFalse(AmendedPlan.Predates("not a ledger", standing));
    }

    [TestMethod]
    public async Task The_queue_names_a_build_made_before_the_amendment_with_both_choices()
    {
        using CycleWorkspace workspace = Approved();
        string branch = "cycle/" + workspace.Cycle.Id + "/build-unattended";
        FakeRepository git = new() { Remote = [branch] };
        git.Files["origin/" + branch + ":" + AmendedPlan.LedgerPath(workspace.Cycle.Id)] = Earlier(Ledger(workspace));

        ListQueuedCycles.Response queue = await Send<ListQueuedCycles.Query, ListQueuedCycles.Response>(workspace, git, new ListQueuedCycles.Query());

        string reason = queue.Held.Single().Reason;
        StringAssert.Contains(reason, "before its plan was amended");
        StringAssert.Contains(reason, "never resumes");
        StringAssert.Contains(reason, "merge development into " + branch);
        StringAssert.Contains(reason, "delete " + branch);
    }

    [TestMethod]
    public async Task The_queue_keeps_its_usual_reason_for_a_build_that_holds_the_standing_approval()
    {
        using CycleWorkspace workspace = Approved();
        string branch = "cycle/" + workspace.Cycle.Id + "/build";
        FakeRepository git = new() { Remote = [branch] };
        git.Files["origin/" + branch + ":" + AmendedPlan.LedgerPath(workspace.Cycle.Id)] = Ledger(workspace);

        ListQueuedCycles.Response queue = await Send<ListQueuedCycles.Query, ListQueuedCycles.Response>(workspace, git, new ListQueuedCycles.Query());

        StringAssert.Contains(queue.Held.Single().Reason, "by hand");
    }

    [TestMethod]
    public async Task The_sitrep_never_calls_a_build_made_before_the_amendment_ready_whatever_its_verify_says()
    {
        using CycleWorkspace workspace = Approved();
        string branch = "cycle/" + workspace.Cycle.Id + "/build-unattended";
        string at = "origin/" + branch + ":.sdlc/cycles/" + workspace.Cycle.Id + "/";
        FakeRepository git = new() { Remote = [branch] };
        git.Files[at + "verify.md"] = "---\noutcome: pass\n---\n";
        git.Files[at + "approval.md"] = Earlier(Ledger(workspace));

        Sitrep.Response page = await Send<Sitrep.Query, Sitrep.Response>(workspace, git, new Sitrep.Query());

        Assert.AreEqual(0, page.Ready.Count);
        Assert.IsTrue(page.NeedsPerson.Single().Amended);
        StringAssert.Contains(SitrepRenderer.Render(page), "built before its plan was amended");
    }

    [TestMethod]
    public async Task A_reopen_drafted_on_the_build_branch_after_admission_is_refused_and_names_the_route()
    {
        using CycleWorkspace workspace = Approved();
        FakeRepository git = Admitted(workspace, "cycle/" + workspace.Cycle.Id + "/build");

        ApprovalText.Response text = await Send<ApprovalText.Query, ApprovalText.Response>(
            workspace,
            git,
            new ApprovalText.Query(workspace.Cycle.Id, "Peter Almanzar", GateDecision.Reopened));

        Assert.IsFalse(text.MayApprove);
        StringAssert.Contains(text.Instructions, "sdlc branch " + workspace.Cycle.Id + " plan");
        StringAssert.Contains(text.Instructions, "pr --plan");
    }

    [TestMethod]
    [DataRow("plan")]
    [DataRow(null)]
    public async Task A_reopen_off_the_build_branch_is_drafted_as_before(string? kind)
    {
        using CycleWorkspace workspace = Approved();
        FakeRepository git = Admitted(workspace, kind is null ? "master" : "cycle/" + workspace.Cycle.Id + "/" + kind);

        ApprovalText.Response text = await Send<ApprovalText.Query, ApprovalText.Response>(
            workspace,
            git,
            new ApprovalText.Query(workspace.Cycle.Id, "Peter Almanzar", GateDecision.Reopened));

        Assert.IsTrue(text.MayApprove, text.Instructions);
    }

    [TestMethod]
    public async Task A_reopen_on_a_build_branch_whose_plan_never_reached_the_integration_branch_is_drafted_as_before()
    {
        // A solution with no integration branch builds from the plan's own branch, and the reopen stays there.
        using CycleWorkspace workspace = Approved();
        FakeRepository git = new() { Branch = "cycle/" + workspace.Cycle.Id + "/build" };

        ApprovalText.Response text = await Send<ApprovalText.Query, ApprovalText.Response>(
            workspace,
            git,
            new ApprovalText.Query(workspace.Cycle.Id, "Peter Almanzar", GateDecision.Reopened));

        Assert.IsTrue(text.MayApprove, text.Instructions);
    }

    [TestMethod]
    public async Task A_plan_branch_left_over_from_earlier_planning_is_refused_rather_than_carried_on()
    {
        using CycleWorkspace workspace = Approved();
        string branch = "cycle/" + workspace.Cycle.Id + "/plan";
        string ledger = AmendedPlan.LedgerPath(workspace.Cycle.Id);
        FakeRepository git = new();
        git.Refs.Add(branch);
        git.Refs.Add(Integration);
        git.Files[branch + ":" + ledger] = Earlier(Ledger(workspace));
        git.Files[Integration + ":" + ledger] = Ledger(workspace);

        Result<StartBranch.Response> result = await Start(workspace, git);

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "git branch -D " + branch);
    }

    [TestMethod]
    public async Task A_plan_branch_holding_the_standing_approval_is_carried_on_as_before()
    {
        using CycleWorkspace workspace = Approved();
        string branch = "cycle/" + workspace.Cycle.Id + "/plan";
        string ledger = AmendedPlan.LedgerPath(workspace.Cycle.Id);
        FakeRepository git = new();
        git.Refs.Add(branch);
        git.Refs.Add(Integration);
        git.Files[branch + ":" + ledger] = Ledger(workspace);
        git.Files[Integration + ":" + ledger] = Ledger(workspace);

        Result<StartBranch.Response> result = await Start(workspace, git);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("git switch " + branch, result.Value.Command);
    }

    [TestMethod]
    public async Task Status_on_the_build_branch_says_the_plan_was_amended_under_it()
    {
        using CycleWorkspace workspace = Approved();
        string standing = Ledger(workspace);
        CycleWorkspace.WriteRaw(workspace.Cycle.ApprovalPath, Earlier(standing));
        FakeRepository git = new() { Branch = "cycle/" + workspace.Cycle.Id + "/build" };
        git.Refs.Add(Integration);
        git.Files[Integration + ":" + AmendedPlan.LedgerPath(workspace.Cycle.Id)] = standing;

        string? amended = await AmendedPlan.NewerOn(git, workspace.Root, workspace.Cycle, CancellationToken.None);

        Assert.AreEqual(Integration, amended);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<ReadCycleStatus.Response> read = await gate.Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(new ReadCycleStatus.Query(workspace.Cycle.Id));
        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));
        ReadCycleStatus.Response status = read.Value with { AmendedOn = amended };
        ReadCycleStatus.ArtifactState plan = status.Artifacts.Single(a => a.Kind == ArtifactKind.Plan);

        string panel = StatusRenderer.RenderBrief(status, new StagePanel(plan, ArtifactOutlineReader.Read(await File.ReadAllTextAsync(plan.Path)), null));

        StringAssert.Contains(panel, "amended on " + Integration);
        StringAssert.Contains(panel, "delete this branch");
    }

    [TestMethod]
    [DataRow("master")]
    [DataRow("plan")]
    public async Task Status_off_the_build_branch_says_nothing_about_an_amendment(string where)
    {
        using CycleWorkspace workspace = Approved();
        string standing = Ledger(workspace);
        CycleWorkspace.WriteRaw(workspace.Cycle.ApprovalPath, Earlier(standing));
        FakeRepository git = new() { Branch = where == "master" ? "master" : "cycle/" + workspace.Cycle.Id + "/" + where };
        git.Refs.Add(Integration);
        git.Files[Integration + ":" + AmendedPlan.LedgerPath(workspace.Cycle.Id)] = standing;

        Assert.IsNull(await AmendedPlan.NewerOn(git, workspace.Root, workspace.Cycle, CancellationToken.None));
    }

    private static CycleWorkspace Approved()
    {
        var workspace = CycleWorkspace.Create();
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

    private static string Ledger(CycleWorkspace workspace) => File.ReadAllText(workspace.Cycle.ApprovalPath);

    /// <summary>The same ledger as it stood before the amendment: an approval taken a day earlier.</summary>
    private static string Earlier(string ledger) =>
        ledger.Replace("at: 2026-10-01T", "at: 2026-09-30T", StringComparison.Ordinal);

    private static FakeRepository Admitted(CycleWorkspace workspace, string branch)
    {
        FakeRepository git = new() { Branch = branch };
        git.Refs.Add(Integration);
        git.PathsAt.Add(Integration + ":.sdlc/cycles/" + workspace.Cycle.Id + "/plan.md");
        return git;
    }

    private static async Task<Result<StartBranch.Response>> Start(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        return await gate.Send<StartBranch.Query, StartBranch.Response>(new StartBranch.Query(workspace.Cycle.Id, CycleBranchKind.Plan));
    }

    private static async Task<TResponse> Send<TQuery, TResponse>(CycleWorkspace workspace, FakeRepository git, TQuery query)
        where TQuery : MofonCode.Core.IRequest<TResponse>
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        Result<TResponse> result = await gate.Send<TQuery, TResponse>(query);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
