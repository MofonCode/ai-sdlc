using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Successors;

namespace Sdlc.Tests;

/// <summary>
/// The plan-time claim check, parking, and the backlog: a plan that needs another's unmerged code waits with its
/// spec and plan kept, and comes back once that code has merged (D-145).
/// </summary>
[TestClass]
public sealed class ParkingTests
{
    private const string Creator = "20261103_0900_00";
    private const string Needer = "20261103_0900_01";
    private const string Integration = "origin/development";

    [TestMethod]
    public async Task A_plan_needing_a_file_only_another_unbuilt_plan_creates_is_parked_with_why()
    {
        using CycleWorkspace workspace = Two();

        ParkCycle.Response park = (await Park(workspace, Needer)).Value;

        Assert.IsNotNull(park.Written, "A plan that cannot be built alone was not parked.");
        Assert.AreEqual(Creator, park.Written.WaitsOn.Single().ToString());
        StringAssert.Contains(park.Written.Why, "src/Totals.cs");
        Assert.IsTrue(File.Exists(Parked.PathOf(Cycle(workspace, Needer))));
        StringAssert.Contains(await File.ReadAllTextAsync(Cycle(workspace, Needer).PathOf(ArtifactKind.Plan)), "src/Totals.cs", "The plan is kept.");
    }

    [TestMethod]
    public async Task A_plan_that_needs_nothing_unmerged_is_not_parked_and_a_shared_file_is_only_said()
    {
        using CycleWorkspace workspace = Two(neederAlsoNames: "src/Shared.cs", creatorAlsoNames: "src/Shared.cs");

        ParkCycle.Response park = (await Park(workspace, Creator)).Value;

        Assert.IsNull(park.Written);
        Assert.IsFalse(File.Exists(Parked.PathOf(Cycle(workspace, Creator))));
        Assert.IsTrue(park.Shared.Any(s => s.Files.Contains("src/Shared.cs")), "A file two plans name is reported.");
    }

    [TestMethod]
    public async Task A_parked_cycle_waits_while_what_it_needs_is_open_and_unparks_once_that_merged_with_a_pass()
    {
        using CycleWorkspace workspace = Two();
        await Park(workspace, Needer);
        FakeRepository git = new();
        git.Refs.Add(Integration);

        Result<UnparkCycle.Response> early = await Unpark(workspace, git, Needer);
        string backlog = BacklogRenderer.Render(await Read(workspace, git));

        git.Files[Integration + ":.sdlc/cycles/" + Creator + "/verify.md"] = "---\noutcome: pass\n---\n";
        git.PathsAt.Add(Integration + ":.sdlc/cycles/" + Creator + "/intent.md");
        Result<UnparkCycle.Response> late = await Unpark(workspace, git, Needer);

        Assert.IsTrue(early.IsFailed);
        StringAssert.Contains(early.Errors[0].Message, "not merged yet");
        StringAssert.Contains(backlog, Needer + " (parked)");
        StringAssert.Contains(backlog, "Wave 2");
        Assert.IsTrue(late.IsSuccess, string.Join("; ", late.Errors.Select(e => e.Message)));
        Assert.IsFalse(File.Exists(Parked.PathOf(Cycle(workspace, Needer))));
    }

    [TestMethod]
    public void A_parked_file_reads_back_what_was_written_and_refuses_one_that_waits_on_nothing()
    {
        Assert.IsTrue(CycleId.TryParse(Creator, out CycleId? on));
        Parked written = new([new SuccessorReference(on, null)], "abc123", "It needs src/Totals.cs.");

        Result<Parked> read = Parked.Parse(written.Text());
        Result<Parked> empty = Parked.Parse("---\nbase: abc\n---\n\nwhy\n");

        Assert.IsTrue(read.IsSuccess);
        Assert.AreEqual(Creator, read.Value.WaitsOn.Single().ToString());
        Assert.AreEqual("abc123", read.Value.Base);
        Assert.IsTrue(empty.IsFailed);
    }

    [TestMethod]
    public void A_parked_file_travels_with_its_plan_through_admission()
    {
        Assert.IsTrue(CycleId.TryParse(Needer, out CycleId? id));

        Assert.IsTrue(VerifyChain.AdmitsAsPlan(id.Value, [".sdlc/cycles/" + Needer + "/plan.md", ".sdlc/cycles/" + Needer + "/parked.md"]));
    }

    [TestMethod]
    public void An_empty_backlog_says_so()
    {
        StringAssert.Contains(BacklogRenderer.Render(new ReadSuccessors.Response([], [], [], null)), "The backlog is empty");
    }

    /// <summary>Two unbuilt cycles: the first creates a file, the second needs it.</summary>
    private static CycleWorkspace Two(string? neederAlsoNames = null, string? creatorAlsoNames = null)
    {
        var workspace = CycleWorkspace.Create(Creator);
        workspace.WriteArtifact(ArtifactKind.Intent);
        Write(workspace, Creator, ("src/Totals.cs", "new"), creatorAlsoNames is null ? null : (creatorAlsoNames, "changes it"));
        Write(workspace, Needer, ("src/Totals.cs", "calls it"), neederAlsoNames is null ? null : (neederAlsoNames, "changes it"));
        File.WriteAllText(Cycle(workspace, Needer).PathOf(ArtifactKind.Intent), "---\ncycle: " + Needer + "\n---\n\n# Totals on the page\n");
        return workspace;
    }

    private static void Write(CycleWorkspace workspace, string id, (string File, string Change) first, (string File, string Change)? second)
    {
        Cycle cycle = Cycle(workspace, id);
        Directory.CreateDirectory(cycle.Directory);

        string rows = "| `" + first.File + "` | " + first.Change + " |\n" + (second is { } more ? "| `" + more.File + "` | " + more.Change + " |\n" : string.Empty);
        File.WriteAllText(cycle.PathOf(ArtifactKind.Plan), "## Files\n\n| File | Change |\n|---|---|\n" + rows);
    }

    private static Cycle Cycle(CycleWorkspace workspace, string id)
    {
        Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed));
        return new Cycle(parsed.Value, workspace.Root);
    }

    private static async Task<Result<ParkCycle.Response>> Park(CycleWorkspace workspace, string id)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(new FakeRepository()));

        return await gate.Send<ParkCycle.Command, ParkCycle.Response>(new ParkCycle.Command(Cycle(workspace, id).Id));
    }

    private static async Task<Result<UnparkCycle.Response>> Unpark(CycleWorkspace workspace, FakeRepository git, string id)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        return await gate.Send<UnparkCycle.Command, UnparkCycle.Response>(new UnparkCycle.Command(Cycle(workspace, id).Id));
    }

    private static async Task<ReadSuccessors.Response> Read(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        return (await gate.Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query())).Value;
    }
}
