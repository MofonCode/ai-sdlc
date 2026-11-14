using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// 'sdlc upgrade': everything that moves to take a release, in order, with what each is on now, and nothing run
/// (D-170).
/// </summary>
[TestClass]
public sealed class UpgradeChecklistTests
{
    private const string Open = "20260930_0900_00";
    private const string Merged = "20260929_1456_32";
    private const string At = "origin/development";

    [TestMethod]
    public async Task A_machine_behind_the_target_moves_first_and_nothing_else_is_listed()
    {
        // The release being moved to knows what it changed; an older one would describe the wrong upgrade.
        UpgradeChecklist.Response upgrade = await Ask(new FakeRepository(), pinned: "0.44.0", target: "0.46.0", machine: "0.45.0");

        string printed = UpgradeRenderer.Render(upgrade);

        Assert.IsTrue(upgrade.MachineFirst);
        StringAssert.Contains(printed, "dotnet tool update --global MofonCode.Sdlc.Cli --version 0.46.0");
        StringAssert.Contains(printed, "sdlc upgrade 0.46.0");
        Assert.IsFalse(printed.Contains("--local", StringComparison.Ordinal), "The rest is the new release's to say.");
    }

    [TestMethod]
    public async Task The_solution_moves_on_a_branch_by_pull_request_and_its_factory_and_open_cycles_are_named()
    {
        FakeRepository git = new();
        git.Refs.UnionWith([At, $"cycle/{Open}/build", $"cycle/{Merged}/build"]);
        git.Files[$"cycle/{Open}/build:.config/dotnet-tools.json"] = PinnedRelease.Pin(null, "0.44.0").Value;
        git.Files[$"cycle/{Merged}/build:.config/dotnet-tools.json"] = PinnedRelease.Pin(null, "0.44.0").Value;
        git.Files[$"{At}:.sdlc/cycles/{Merged}/verify.md"] = "---\ncycle: " + Merged + "\noutcome: pass\n---\n\n# Done\n";

        UpgradeChecklist.Response upgrade = await Ask(git, pinned: "0.45.0", target: null, machine: "0.46.0", factory: true);
        string printed = UpgradeRenderer.Render(upgrade);

        Assert.AreEqual("0.46.0", upgrade.Target, "Without a release, the one this machine runs.");
        StringAssert.Contains(printed, "Moving to sdlc 0.46.0 from 0.45.0");
        StringAssert.Contains(printed, "git switch -c upgrade/sdlc-0.46.0 development");
        StringAssert.Contains(printed, "dotnet tool update --local MofonCode.Sdlc.Cli --version 0.46.0");
        StringAssert.Contains(printed, "sdlc factory");
        StringAssert.Contains(printed, "build its agent image from 0.46.0");
        CollectionAssert.AreEqual(new[] { $"cycle/{Open}/build" }, upgrade.Cycles.Select(cycle => cycle.Branch).ToArray(), "A cycle whose work is on development is tidy's.");
        StringAssert.Contains(printed, $"cycle/{Open}/build  pins 0.44.0");
    }

    [TestMethod]
    public async Task A_solution_already_on_the_target_has_nothing_to_move_and_no_factory_step()
    {
        UpgradeChecklist.Response upgrade = await Ask(new FakeRepository(), pinned: "0.46.0", target: null, machine: "0.46.0");
        string printed = UpgradeRenderer.Render(upgrade);

        StringAssert.Contains(printed, "This solution already pins 0.46.0. Nothing to do.");
        StringAssert.Contains(printed, "No open cycle pins an older release.");
        Assert.IsFalse(printed.Contains("factory", StringComparison.Ordinal), "No factory, no factory step.");
    }

    [TestMethod]
    public async Task A_local_build_or_a_word_that_is_no_release_is_refused()
    {
        Result<UpgradeChecklist.Response> local = await Send(new FakeRepository(), pinned: "0.45.0", target: null, machine: "0.46.0-local");
        Result<UpgradeChecklist.Response> nonsense = await Send(new FakeRepository(), pinned: "0.45.0", target: "latest", machine: "0.46.0");

        StringAssert.Contains(local.Errors.Single().Message, "local build");
        StringAssert.Contains(nonsense.Errors.Single().Message, "'latest' is not a release");
    }

    [TestMethod]
    [DataRow("0.45.0", "0.46.0", true)]
    [DataRow("0.46.0-preview", "0.46.0", true)]
    [DataRow("0.46.0", "0.46.0", false)]
    [DataRow("0.46.0", "0.45.9", false)]
    [DataRow("0.9.0", "0.10.0", true)]
    public void Releases_compare_by_number_and_a_preview_comes_before_its_release(string release, string than, bool older) =>
        Assert.AreEqual(older, UpgradeChecklist.IsOlder(release, than));

    private static async Task<UpgradeChecklist.Response> Ask(FakeRepository git, string pinned, string? target, string machine, bool factory = false)
    {
        Result<UpgradeChecklist.Response> result = await Send(git, pinned, target, machine, factory);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task<Result<UpgradeChecklist.Response>> Send(FakeRepository git, string pinned, string? target, string machine, bool factory = false)
    {
        using var workspace = CycleWorkspace.Create(Merged);
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".config", "dotnet-tools.json"), PinnedRelease.Pin(null, pinned).Value);

        if (factory)
        {
            CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".github", "workflows", "sdlc-factory.yml"), "name: factory\n");
        }

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        return await gate.Send<UpgradeChecklist.Query, UpgradeChecklist.Response>(new UpgradeChecklist.Query(target, machine));
    }
}
