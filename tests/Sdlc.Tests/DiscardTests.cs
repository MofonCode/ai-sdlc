using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// Before a sandbox is thrown away: the empty go, and what holds a choice or a finding is recorded first (D-146).
/// </summary>
[TestClass]
public sealed class DiscardTests
{
    private const string Id = "20260929_1456_32";
    private const string Sandbox = "sandbox/" + Id;

    [TestMethod]
    public async Task An_intent_no_round_judged_is_empty_and_the_removal_is_offered()
    {
        using var workspace = CycleWorkspace.Create(Id);
        workspace.WriteArtifact(ArtifactKind.Intent);

        DiscardSandbox.Response review = (await Review(workspace)).Value;

        Assert.AreEqual(DiscardVerdict.Empty, review.Cycles.Single().Verdict);
        Assert.IsNotNull(review.Remove);
        StringAssert.Contains(review.Remove, "git branch -D " + Sandbox);
        StringAssert.Contains(DiscardRenderer.Render(review), "Once the person confirms");
    }

    [TestMethod]
    public async Task A_fixed_decision_is_worth_recording_and_holds_the_removal_back()
    {
        using var workspace = CycleWorkspace.Create(Id);
        workspace.WriteArtifact(ArtifactKind.Intent, "## Fixed decisions\n\n- Use the cache we have. Peter, 2026-11-08: one eviction policy.\n");
        workspace.WriteCleanRound(ArtifactKind.Intent);

        DiscardSandbox.Response review = (await Review(workspace)).Value;

        Assert.AreEqual(DiscardVerdict.WorthRecording, review.Cycles.Single().Verdict);
        StringAssert.Contains(review.Cycles.Single().Why, "1 fixed decision");
        StringAssert.Contains(review.Cycles.Single().Why, "sdlc draft abandon " + Id);
        Assert.IsNull(review.Remove, "A sandbox holding a choice was offered for deletion.");
    }

    [TestMethod]
    public async Task One_clean_round_and_nothing_argued_can_go()
    {
        using var workspace = CycleWorkspace.Create(Id);
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteCleanRound(ArtifactKind.Intent);

        DiscardSandbox.Response review = (await Review(workspace)).Value;

        Assert.AreEqual(DiscardVerdict.Deletable, review.Cycles.Single().Verdict);
        Assert.IsNotNull(review.Remove);
    }

    [TestMethod]
    public async Task An_abandonment_not_yet_admitted_holds_the_removal_back_until_it_leaves()
    {
        using var workspace = CycleWorkspace.Create(Id);
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.Abandon();

        DiscardSandbox.Response review = (await Review(workspace)).Value;

        Assert.AreEqual(DiscardVerdict.Recorded, review.Cycles.Single().Verdict);
        Assert.IsNull(review.Remove);
    }

    [TestMethod]
    public async Task Outside_a_sandbox_it_refuses()
    {
        using var workspace = CycleWorkspace.Create(Id);

        Result<DiscardSandbox.Response> result = await Review(workspace, new FakeRepository { Branch = "development" });

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "not a sandbox");
    }

    private static async Task<Result<DiscardSandbox.Response>> Review(CycleWorkspace workspace, FakeRepository? git = null)
    {
        using var gate = Gate.For(
            workspace.Root,
            services => services.AddSingleton<IRepository>(git ?? new FakeRepository { Branch = Sandbox }));

        return await gate.Send<DiscardSandbox.Query, DiscardSandbox.Response>(new DiscardSandbox.Query());
    }
}
