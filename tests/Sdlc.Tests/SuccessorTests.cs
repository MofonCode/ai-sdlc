using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Successors;

namespace Sdlc.Tests;

/// <summary>
/// A slice that waits on another's code is a file, and where it stands is worked out from the record (D-125).
/// </summary>
[TestClass]
public sealed class SuccessorTests
{
    private const string Integration = "origin/development";
    private const string First = "20261103_0900_00";
    private const string Second = "20261103_0900_01";
    private const string Third = "20261103_0900_02";

    [TestMethod]
    public void A_well_formed_successor_reads_its_waits_and_its_substance()
    {
        Result<Successor> read = Successor.Parse("export-totals", File(First + ", successor:export-csv", "sliced_from: github:56\nmirror: github:57\n"));

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));
        Assert.AreEqual(2, read.Value.Succeeds.Count);
        Assert.AreEqual(First, read.Value.Succeeds[0].ToString());
        Assert.AreEqual("successor:export-csv", read.Value.Succeeds[1].ToString());
        Assert.AreEqual("github:56", read.Value.SlicedFrom);
        Assert.AreEqual("github:57", read.Value.Mirror);
        StringAssert.Contains(read.Value.Outcome, "totals");
    }

    [TestMethod]
    public void A_malformed_successor_is_refused_with_every_problem_at_once()
    {
        string text = "---\nsucceeds: tomorrow\nmirror: notes.md\nafter: x\n---\n\n# Totals\n\n## Outcome\n\nTotals.\n";

        Result<Successor> read = Successor.Parse("Export_Totals", text);

        Assert.IsTrue(read.IsFailed);
        string problems = string.Join("\n", read.Errors.Select(e => e.Message));
        StringAssert.Contains(problems, "is not a slug");
        StringAssert.Contains(problems, "'tomorrow' in succeeds:");
        StringAssert.Contains(problems, "is not a work item");
        StringAssert.Contains(problems, "'after' is not a key");
        StringAssert.Contains(problems, "'## Why' is missing");
    }

    [TestMethod]
    public void A_successor_that_waits_on_nothing_is_refused()
    {
        Result<Successor> read = Successor.Parse("totals", File(string.Empty));

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "succeeds: is missing or empty");
    }

    [TestMethod]
    public async Task A_successor_waits_while_its_predecessor_is_open_and_is_ready_once_it_merges_with_a_pass()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Write(workspace, "totals", First);
        FakeRepository git = Git();

        Assert.AreEqual(SuccessorState.Waiting, (await Read(workspace, git)).Find("totals")!.State);

        Merge(git, First);

        ReadSuccessors.Response after = await Read(workspace, git);
        Assert.AreEqual(SuccessorState.Ready, after.Find("totals")!.State);
        Assert.AreEqual(WaitState.Merged, after.Find("totals")!.Waits.Single().State);
        Assert.IsTrue(after.Sound);
    }

    [TestMethod]
    public async Task A_plan_admitted_and_not_built_is_not_merged()
    {
        using CycleWorkspace workspace = Solution();
        Write(workspace, "totals", First);
        FakeRepository git = Git();
        git.PathsAt.Add(Integration + ":" + Folder(First) + "intent.md");

        ReadSuccessors.Standing totals = (await Read(workspace, git)).Find("totals")!;

        Assert.AreEqual(SuccessorState.Waiting, totals.State);
        Assert.AreEqual(WaitState.Open, totals.Waits.Single().State);
    }

    [TestMethod]
    public async Task A_successor_behind_an_abandoned_cycle_is_stuck_and_so_is_one_behind_it()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        await System.IO.File.WriteAllTextAsync(new Cycle(Parse(First), workspace.Root).AbandonedPath, "stopped");
        Write(workspace, "totals", First);
        Write(workspace, "chart", "successor:totals");

        ReadSuccessors.Response read = await Read(workspace, Git());

        Assert.AreEqual(SuccessorState.Stuck, read.Find("totals")!.State);
        Assert.AreEqual(SuccessorState.Stuck, read.Find("chart")!.State);
    }

    [TestMethod]
    public async Task A_successor_naming_nothing_that_exists_is_unresolved_and_the_set_is_unsound()
    {
        using CycleWorkspace workspace = Solution();
        Write(workspace, "totals", Third + ", successor:nowhere");

        ReadSuccessors.Response read = await Read(workspace, Git());

        Assert.AreEqual(SuccessorState.Unresolved, read.Find("totals")!.State);
        Assert.IsTrue(read.Find("totals")!.Waits.All(w => w.State == WaitState.Missing));
        Assert.IsFalse(read.Sound);
    }

    [TestMethod]
    public async Task A_cycle_known_only_by_its_plan_branch_resolves_and_is_open()
    {
        // A sibling opened in its own worktree: its folder is not in this checkout, its branch is.
        using CycleWorkspace workspace = Solution();
        Write(workspace, "totals", Second);
        FakeRepository git = Git();
        git.Refs.Add("cycle/" + Second + "/plan");

        Assert.AreEqual(WaitState.Open, (await Read(workspace, git)).Find("totals")!.Waits.Single().State);
    }

    [TestMethod]
    public async Task A_fan_in_is_ready_only_when_every_predecessor_has_merged()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Opened(workspace, Second);
        Write(workspace, "report", First + ", " + Second);
        FakeRepository git = Git();

        Merge(git, First);
        Assert.AreEqual(SuccessorState.Waiting, (await Read(workspace, git)).Find("report")!.State);

        Merge(git, Second);
        Assert.AreEqual(SuccessorState.Ready, (await Read(workspace, git)).Find("report")!.State);
    }

    [TestMethod]
    public async Task A_three_deep_chain_resolves_one_step_at_a_time_without_editing_what_named_a_slug()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Write(workspace, "totals", First);
        Write(workspace, "chart", "successor:totals");
        Write(workspace, "share", "successor:chart");
        FakeRepository git = Git();

        ReadSuccessors.Response start = await Read(workspace, git);
        Assert.AreEqual(SuccessorState.Waiting, start.Find("totals")!.State);
        Assert.AreEqual(WaitState.NotStarted, start.Find("chart")!.Waits.Single().State);

        // The first merges: totals comes ready, and is started as a cycle whose intent names it.
        Merge(git, First);
        Assert.AreEqual(SuccessorState.Ready, (await Read(workspace, git)).Find("totals")!.State);
        Start(workspace, Second, "totals");

        ReadSuccessors.Response started = await Read(workspace, git);
        Assert.IsNull(started.Find("totals"));
        Assert.AreEqual(Parse(Second), started.Find("chart")!.Waits.Single().Cycle);
        Assert.AreEqual(SuccessorState.Waiting, started.Find("chart")!.State);

        // It merges: chart comes ready, and share still waits on chart, a file.
        Merge(git, Second);
        ReadSuccessors.Response merged = await Read(workspace, git);
        Assert.AreEqual(SuccessorState.Ready, merged.Find("chart")!.State);
        Assert.AreEqual(SuccessorState.Waiting, merged.Find("share")!.State);

        Start(workspace, Third, "chart");
        Merge(git, Third);
        Assert.AreEqual(SuccessorState.Ready, (await Read(workspace, git)).Find("share")!.State);
    }

    [TestMethod]
    public async Task A_loop_is_named_once_and_makes_the_set_unsound()
    {
        using CycleWorkspace workspace = Solution();
        Write(workspace, "alpha", "successor:beta");
        Write(workspace, "beta", "successor:gamma");
        Write(workspace, "gamma", "successor:alpha");
        Write(workspace, "self", "successor:self");

        ReadSuccessors.Response read = await Read(workspace, Git());

        Assert.AreEqual(2, read.Loops.Count);
        CollectionAssert.AreEqual(new[] { "alpha", "beta", "gamma", "alpha" }, read.Loops[0].ToArray());
        CollectionAssert.AreEqual(new[] { "self", "self" }, read.Loops[1].ToArray());
        Assert.IsFalse(read.Sound);
        Assert.IsTrue(read.Successors.All(s => s.State == SuccessorState.Waiting));
    }

    [TestMethod]
    public async Task A_malformed_file_is_reported_and_not_read_as_a_successor()
    {
        using CycleWorkspace workspace = Solution();
        Directory.CreateDirectory(Successor.DirectoryOf(workspace.Root));
        await System.IO.File.WriteAllTextAsync(Path.Combine(Successor.DirectoryOf(workspace.Root), "broken.md"), "no frontmatter");

        ReadSuccessors.Response read = await Read(workspace, Git());

        Assert.AreEqual("broken", read.Malformed.Single().Slug);
        Assert.AreEqual(0, read.Successors.Count);
        Assert.IsFalse(read.Sound);
    }

    [TestMethod]
    public void Only_a_file_under_the_successors_folder_named_by_a_slug_is_a_successor_path()
    {
        Assert.IsTrue(Successor.IsSuccessorPath(".sdlc/successors/export-totals.md"));
        Assert.IsTrue(Successor.IsSuccessorPath(".sdlc\\successors\\totals.md"));
        Assert.IsFalse(Successor.IsSuccessorPath(".sdlc/successors/Totals.md"));
        Assert.IsFalse(Successor.IsSuccessorPath(".sdlc/successors/nested/totals.md"));
        Assert.IsFalse(Successor.IsSuccessorPath("src/successors/totals.md"));
    }

    internal static string File(string succeeds, string extra = "") =>
        "---\n"
        + (succeeds.Length == 0 ? string.Empty : "succeeds: " + succeeds + "\n")
        + extra
        + "---\n\n# Export totals\n\n## Outcome\n\nFinance sees the month's totals beside the export.\n\n## Why\n\nThey add it up by hand today.\n";

    internal static void Write(CycleWorkspace workspace, string slug, string succeeds)
    {
        string directory = Successor.DirectoryOf(workspace.Root);
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(Path.Combine(directory, slug + ".md"), File(succeeds));
    }

    private static CycleWorkspace Solution() => CycleWorkspace.Create("20261101_0800_00");

    private static void Opened(CycleWorkspace workspace, string id) =>
        Directory.CreateDirectory(new Cycle(Parse(id), workspace.Root).Directory);

    /// <summary>Starts a successor as a cycle: its intent names it, and its file is gone.</summary>
    private static void Start(CycleWorkspace workspace, string id, string slug)
    {
        Cycle cycle = new(Parse(id), workspace.Root);
        Directory.CreateDirectory(cycle.Directory);
        System.IO.File.WriteAllText(
            cycle.PathOf(ArtifactKind.Intent),
            "---\nframework_version: 1.0.0\ncycle: " + id + "\ntype: feature addition\nlead: successor:" + slug + "\n---\n\n# x\n");
        System.IO.File.Delete(Path.Combine(Successor.DirectoryOf(workspace.Root), slug + ".md"));
    }

    private static void Merge(FakeRepository git, string id)
    {
        git.PathsAt.Add(Integration + ":" + Folder(id) + "intent.md");
        git.Files[Integration + ":" + Folder(id) + "verify.md"] = "---\nframework_version: 1.0.0\ncycle: " + id + "\noutcome: pass\n---\n\n# v\n";
    }

    private static string Folder(string id) => ".sdlc/cycles/" + id + "/";

    private static CycleId Parse(string id)
    {
        Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed));
        return parsed.Value;
    }

    private static FakeRepository Git()
    {
        FakeRepository git = new();
        git.Refs.Add(Integration);
        return git;
    }

    private static async Task<ReadSuccessors.Response> Read(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        Result<ReadSuccessors.Response> result = await gate.Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
