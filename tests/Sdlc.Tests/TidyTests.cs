using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Harvest;

namespace Sdlc.Tests;

/// <summary>
/// What the framework opened and the work has outgrown: done read from the integration branch, removal printed
/// and never run (D-162).
/// </summary>
[TestClass]
public sealed class TidyTests
{
    private const string Merged = "20260929_1456_32";
    private const string Open = "20260930_0900_00";
    private const string Dropped = "20261001_0900_00";
    private const string At = "origin/development";

    [TestMethod]
    public async Task Done_is_read_from_the_integration_branch_and_only_what_is_done_goes()
    {
        // One person removed twelve worktrees and fifteen branches by hand after squash merges.
        FakeRepository git = new() { Base = "base" };
        git.Refs.UnionWith([At, $"cycle/{Merged}/plan", $"cycle/{Merged}/build", $"cycle/{Open}/plan", $"cycle/{Open}/build", $"cycle/{Dropped}/plan", "slices/sandbox-x/plan", $"origin/cycle/{Open}/build"]);
        git.PathsAt.UnionWith([$"{At}:{Folder(Merged)}plan.md", $"{At}:{Folder(Open)}plan.md", $"{At}:{Folder(Dropped)}abandoned.md"]);
        git.Files[$"{At}:{Folder(Merged)}verify.md"] = "---\ncycle: " + Merged + "\noutcome: pass\n---\n\n# Done\n";
        git.Committed = [Folder(Merged) + "plan.md", Folder(Open) + "plan.md"];
        git.WorktreeList.Add(("/repo", "master"));
        git.WorktreeList.Add(($"/repo.cycles/{Merged}", $"cycle/{Merged}/build"));

        TidyCycles.Response review = await Review(git);

        CollectionAssert.AreEqual(
            new[] { $"cycle/{Merged}/build", $"cycle/{Merged}/plan", $"cycle/{Open}/plan", $"cycle/{Dropped}/plan", "slices/sandbox-x/plan" },
            review.Removable.Select(entry => entry.Branch).ToArray(),
            $"{Open}'s build has not passed there, and a remote branch is not touched.");
        Assert.AreEqual($"git worktree remove /repo.cycles/{Merged}", review.Commands[0]);
        StringAssert.StartsWith(review.Commands[1], "git branch -D ");
        StringAssert.Contains(TidyRenderer.Render(review), "Once the person confirms");
    }

    [TestMethod]
    public async Task A_cycle_abandoned_on_its_branch_goes_only_once_a_harvest_on_the_integration_branch_closed_it()
    {
        FakeRepository git = new() { Base = "base" };
        git.Refs.UnionWith([At, $"cycle/{Dropped}/plan"]);

        Assert.AreEqual(0, (await Review(git)).Removable.Count, "Not harvested yet: deleting the branch would lose the cycle.");

        HarvestedCycle husk = new(Dropped, "tip", DateTimeOffset.UnixEpoch, true, [], [], [], [], new Dictionary<string, int>(), 0, []);
        git.Files[$"{At}:.sdlc/{HarvestLedger.FileName}"] = HarvestLedger.Empty.Closing([husk]).Write();

        StringAssert.Contains((await Review(git)).Removable.Single().Why, "closed by a harvest");
    }

    [TestMethod]
    public async Task A_wave_or_series_is_open_on_the_sitrep_until_what_it_built_passed_there_and_then_tidy_removes_it()
    {
        string wave = "wave/" + Merged;
        string series = "series/" + Open;
        FakeRepository git = new() { Base = "base" };
        git.Refs.UnionWith([At, wave, series, "origin/" + series]);
        git.Files[$"{At}:{Folder(Merged)}verify.md"] = "---\ncycle: " + Merged + "\noutcome: pass\n---\n\n# Done\n";
        git.Files[$"{series}:.sdlc/series/{Open}/series.json"] = Sdlc.Core.Series.SeriesRecord.Start(Open, DateTimeOffset.UnixEpoch, 3, Sdlc.Core.Solutions.ApprovalRequirement.None)
            .WithWave(DateTimeOffset.UnixEpoch, [new Sdlc.Core.Series.SeriesItem(Sdlc.Core.Series.SeriesItemKind.Parked, Open, Open)])
            .Write();
        git.WorktreeList.Add(("/repo", "master"));
        git.WorktreeList.Add(($"/repo.series/{Open}", series));

        // Each carries a cycle that has not passed on the integration branch yet: both open, neither tidied.
        git.Committed = [Folder(Open) + "plan.md"];
        Assert.AreEqual(0, (await Review(git)).Removable.Count);

        Sdlc.Core.Factory.Sitrep.Response page = await Sitrep(git);
        CollectionAssert.AreEqual(new[] { wave, series }, page.Runs.Select(run => run.Branch).ToArray(), "Each once, the local branch read before the remote's, in id order.");
        Assert.AreEqual(1, page.Runs[1].Waves);
        Assert.AreEqual(3, page.Runs[1].Limit);
        string rendered = Sdlc.Core.Factory.SitrepRenderer.Render(page);
        StringAssert.Contains(rendered, "## Waves and series open");
        StringAssert.Contains(rendered, "1 of 3 waves taken");

        // What they built passed there: done, and the worktree and branches go the way a cycle's build does.
        git.Committed = [Folder(Merged) + "verify.md"];
        TidyCycles.Response review = await Review(git);

        CollectionAssert.AreEqual(new[] { series, wave }, review.Removable.Select(entry => entry.Branch).ToArray());
        CollectionAssert.Contains(review.Commands.ToArray(), $"git worktree remove /repo.series/{Open}");
        Assert.AreEqual(0, (await Sitrep(git)).Runs.Count);
    }

    [TestMethod]
    public async Task A_done_branch_checked_out_in_the_main_checkout_is_named_and_kept()
    {
        FakeRepository git = new() { Base = "base" };
        git.Refs.UnionWith([At, $"cycle/{Merged}/plan"]);
        git.PathsAt.Add($"{At}:{Folder(Merged)}plan.md");
        git.WorktreeList.Add(("/repo", $"cycle/{Merged}/plan"));

        TidyCycles.Response review = await Review(git);

        Assert.AreEqual(0, review.Removable.Count);
        StringAssert.Contains(review.Kept.Single().Why, "switch that checkout");
        Assert.AreEqual(0, review.Commands.Count);
    }

    [TestMethod]
    public async Task With_no_integration_branch_nothing_is_called_done()
    {
        Result<TidyCycles.Response> refused = await Send(new FakeRepository());

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "development");
    }

    [TestMethod]
    public void Git_lists_the_branches_by_prefix_and_the_worktrees_with_what_each_has_checked_out()
    {
        using var workspace = CycleWorkspace.Create(Merged);
        string worktree = workspace.Root + ".cycles-" + Merged;

        try
        {
            Git(workspace.Root, "init -b master");
            Git(workspace.Root, "-c user.name=t -c user.email=t@t -c commit.gpgsign=false commit --allow-empty -m start");
            Git(workspace.Root, $"branch cycle/{Merged}/plan");
            Git(workspace.Root, "branch cycle-not-one");
            Git(workspace.Root, $"worktree add \"{worktree}\" -b cycle/{Merged}/build");

            GitRepository git = new();

            CollectionAssert.AreEqual(new[] { $"cycle/{Merged}/build", $"cycle/{Merged}/plan" }, git.Branches(workspace.Root, "cycle/").ToArray());

            IReadOnlyList<(string Path, string? Branch)> worktrees = git.Worktrees(workspace.Root);
            Assert.AreEqual(2, worktrees.Count);
            Assert.AreEqual("master", worktrees[0].Branch, "The main checkout first.");
            Assert.AreEqual($"cycle/{Merged}/build", worktrees[1].Branch);
        }
        finally
        {
            Git(workspace.Root, $"worktree remove --force \"{worktree}\"");
        }
    }

    private static void Git(string root, string arguments)
    {
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            })!;

        process.WaitForExit();
    }

    private static string Folder(string id) => ".sdlc/cycles/" + id + "/";

    private static async Task<TidyCycles.Response> Review(FakeRepository git)
    {
        Result<TidyCycles.Response> result = await Send(git);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task<Sdlc.Core.Factory.Sitrep.Response> Sitrep(FakeRepository git)
    {
        using var workspace = CycleWorkspace.Create(Merged);
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<Sdlc.Core.Factory.Sitrep.Response> result = await gate.Send<Sdlc.Core.Factory.Sitrep.Query, Sdlc.Core.Factory.Sitrep.Response>(new Sdlc.Core.Factory.Sitrep.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task<Result<TidyCycles.Response>> Send(FakeRepository git)
    {
        using var workspace = CycleWorkspace.Create(Merged);
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        return await gate.Send<TidyCycles.Query, TidyCycles.Response>(new TidyCycles.Query());
    }
}
