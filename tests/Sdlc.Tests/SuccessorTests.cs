using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Factory;
using Sdlc.Core.Git;
using Sdlc.Core.Leads;
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

    [TestMethod]
    public void A_successor_is_a_lead_named_by_its_slug_and_a_malformed_slug_is_refused()
    {
        Assert.IsTrue(LeadReference.TryParse("successor:export-totals", out LeadReference? lead, out _));
        Assert.AreEqual(LeadSource.Successor, lead.Source);
        Assert.AreEqual("export-totals", lead.Value);
        Assert.AreEqual("successor:export-totals", lead.ToString());

        Assert.IsFalse(LeadReference.TryParse("successor:Export Totals", out _, out string? problem));
        StringAssert.Contains(problem, "a successor's slug");
    }

    [TestMethod]
    public async Task A_ready_successor_is_shown_as_a_lead_and_one_still_waiting_is_refused()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Write(workspace, "totals", First);
        FakeRepository git = Git();

        Result<ReadLead.Response> waiting = await Lead(workspace, git, "totals");
        Assert.IsTrue(waiting.IsFailed);
        StringAssert.Contains(waiting.Errors[0].Message, "is waiting: " + First + " not merged yet");

        Merge(git, First);
        Result<ReadLead.Response> ready = await Lead(workspace, git, "totals");

        Assert.IsTrue(ready.IsSuccess, string.Join("; ", ready.Errors.Select(e => e.Message)));
        Assert.AreEqual("Export totals", ready.Value.Title);
        StringAssert.Contains(ready.Value.Description, "Finance sees the month's totals");
        StringAssert.Contains(ready.Value.Kind, "which has merged");
    }

    [TestMethod]
    public async Task Starting_a_ready_successor_deletes_its_file_and_a_waiting_one_is_left_alone()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Opened(workspace, Second);
        Write(workspace, "totals", First);
        string file = Path.Combine(Successor.DirectoryOf(workspace.Root), "totals.md");
        FakeRepository git = Git();

        Result<StartSuccessor.Response> refused = await Start(workspace, git, "totals", Second);
        Assert.IsTrue(refused.IsFailed);
        Assert.IsTrue(System.IO.File.Exists(file));

        Merge(git, First);
        Result<StartSuccessor.Response> started = await Start(workspace, git, "totals", Second);

        Assert.IsTrue(started.IsSuccess, string.Join("; ", started.Errors.Select(e => e.Message)));
        Assert.AreEqual(".sdlc/successors/totals.md", started.Value.Deleted);
        Assert.IsFalse(System.IO.File.Exists(file));
    }

    [TestMethod]
    public async Task Starting_a_successor_as_a_cycle_that_is_not_here_is_refused()
    {
        using CycleWorkspace workspace = Solution();
        Write(workspace, "totals", First);

        Result<StartSuccessor.Response> refused = await Start(workspace, Git(), "totals", Third);

        StringAssert.Contains(refused.Errors[0].Message, "There is no cycle " + Third);
    }

    [TestMethod]
    public async Task A_loop_is_the_reason_given_for_a_successor_in_it()
    {
        using CycleWorkspace workspace = Solution();
        Write(workspace, "alpha", "successor:beta");
        Write(workspace, "beta", "successor:alpha");

        Result<ReadLead.Response> refused = await Lead(workspace, Git(), "beta");

        StringAssert.Contains(refused.Errors[0].Message, "waits in a loop -- alpha -> beta -> alpha");
    }

    [TestMethod]
    public async Task The_listing_puts_what_is_ready_first_with_the_command_that_starts_it()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Write(workspace, "chart", "successor:totals");
        Write(workspace, "totals", First);
        FakeRepository git = Git();
        Merge(git, First);

        string text = SuccessorsRenderer.Render(await Read(workspace, git));

        Assert.IsTrue(text.IndexOf("  ready ", StringComparison.Ordinal) < text.IndexOf("  waiting ", StringComparison.Ordinal), text);
        StringAssert.Contains(text, "start it: /sdlc:intent --from successor:totals");
        StringAssert.Contains(text, "succeeds successor:totals not started yet");
    }

    [TestMethod]
    public async Task The_status_panel_names_a_ready_and_a_stuck_successor_beside_the_next_command_and_not_a_waiting_one()
    {
        using CycleWorkspace workspace = Solution();
        workspace.WriteArtifact(ArtifactKind.Intent);
        Opened(workspace, First);
        Opened(workspace, Second);
        await System.IO.File.WriteAllTextAsync(new Cycle(Parse(Second), workspace.Root).AbandonedPath, "stopped");
        Write(workspace, "totals", First);
        Write(workspace, "chart", Second);
        Write(workspace, "share", Third + ", successor:totals");
        FakeRepository git = Git();
        Merge(git, First);
        git.Refs.Add("cycle/" + Third + "/plan");

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<ReadCycleStatus.Response> status = await gate.Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(new ReadCycleStatus.Query(workspace.Cycle.Id));
        Assert.IsTrue(status.IsSuccess, string.Join("; ", status.Errors.Select(e => e.Message)));
        ReadCycleStatus.ArtifactState intent = status.Value.Artifacts[0];

        string panel = StatusRenderer.RenderBrief(
            status.Value with { Successors = await Read(workspace, git) },
            new StagePanel(intent, await ArtifactOutlineReader.ReadFile(intent.Path, CancellationToken.None)),
            null);

        StringAssert.Contains(panel, "successor:totals is ready");
        StringAssert.Contains(panel, "intent --from successor:totals");
        StringAssert.Contains(panel, "successor:chart is stuck");
        Assert.IsFalse(panel.Contains("successor:share", StringComparison.Ordinal), panel);
        Assert.IsTrue(panel.IndexOf("Successor", StringComparison.Ordinal) < panel.IndexOf("Next", StringComparison.Ordinal), panel);

        // D-161: past two, a panel names only what its own cycle brought, and counts the rest.
        Write(workspace, "export", First);
        Write(workspace, "import-feeds-from-the-exchange", workspace.Cycle.Id.ToString());
        Merge(git, workspace.Cycle.Id.ToString());

        string crowded = StatusRenderer.RenderBrief(
            status.Value with { Successors = await Read(workspace, git) },
            new StagePanel(intent, await ArtifactOutlineReader.ReadFile(intent.Path, CancellationToken.None)),
            null);

        StringAssert.Contains(crowded, "successor:import-feeds-from-the-exchange is ready ");
        Assert.IsFalse(crowded.Contains("successor:totals", StringComparison.Ordinal), crowded);
        StringAssert.Contains(crowded, "and 3 more are ready or stuck");
    }

    [TestMethod]
    public async Task The_sitrep_names_what_came_ready_for_a_person_to_start()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Write(workspace, "totals", First);
        Write(workspace, "chart", "successor:totals");
        FakeRepository git = Git();
        Merge(git, First);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<Sitrep.Response> page = await gate.Send<Sitrep.Query, Sitrep.Response>(new Sitrep.Query());
        Assert.IsTrue(page.IsSuccess, string.Join("; ", page.Errors.Select(e => e.Message)));

        Assert.AreEqual("totals", page.Value.Successors.Single().Successor.Slug);
        string text = SitrepRenderer.Render(page.Value);
        StringAssert.Contains(text, "## Backlog");
        StringAssert.Contains(text, "**Ready now**");
        StringAssert.Contains(text, "Start it with `/sdlc:intent --from successor:totals`");

        // What still waits is on the page too, with what it waits on, and so is what the page cannot see.
        StringAssert.Contains(text, "**Still waiting**");
        StringAssert.Contains(text, "`successor:chart`");
        StringAssert.Contains(text, "Not on this page: cycles still being planned in a sandbox");
    }

    [TestMethod]
    public async Task The_sitrep_says_so_when_the_backlog_is_empty_rather_than_leaving_the_section_out()
    {
        using CycleWorkspace workspace = Solution();

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(Git()));
        Result<Sitrep.Response> page = await gate.Send<Sitrep.Query, Sitrep.Response>(new Sitrep.Query());
        Assert.IsTrue(page.IsSuccess, string.Join("; ", page.Errors.Select(e => e.Message)));

        string text = SitrepRenderer.Render(page.Value);

        StringAssert.Contains(text, "## Backlog");
        StringAssert.Contains(text, "Nothing waits here");
        StringAssert.Contains(text, "Not on this page");
    }

    [TestMethod]
    public async Task The_sitrep_names_a_parked_cycle_and_what_it_waits_on()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Opened(workspace, Second);
        Assert.IsTrue(CycleId.TryParse(Second, out CycleId? second));
        await System.IO.File.WriteAllTextAsync(
            Parked.PathOf(new Cycle(second.Value, workspace.Root)),
            "---\nwaits_on: " + First + "\n---\n\nIt needs src/Totals.cs, which only " + First + " creates.\n");

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(Git()));
        Result<Sitrep.Response> page = await gate.Send<Sitrep.Query, Sitrep.Response>(new Sitrep.Query());
        Assert.IsTrue(page.IsSuccess, string.Join("; ", page.Errors.Select(e => e.Message)));

        string text = SitrepRenderer.Render(page.Value);

        StringAssert.Contains(text, "`" + Second + "`");
        StringAssert.Contains(text, "parked, waits on " + First);
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
            "---\ncycle: " + id + "\ntype: feature addition\nlead: successor:" + slug + "\n---\n\n# x\n");
        System.IO.File.Delete(Path.Combine(Successor.DirectoryOf(workspace.Root), slug + ".md"));
    }

    private static void Merge(FakeRepository git, string id)
    {
        git.PathsAt.Add(Integration + ":" + Folder(id) + "intent.md");
        git.Files[Integration + ":" + Folder(id) + "verify.md"] = "---\ncycle: " + id + "\noutcome: pass\n---\n\n# v\n";
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

    private static async Task<Result<ReadLead.Response>> Lead(CycleWorkspace workspace, FakeRepository git, string slug)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        return await gate.Send<ReadLead.Query, ReadLead.Response>(new ReadLead.Query(new LeadReference(LeadSource.Successor, slug)));
    }

    private static async Task<Result<StartSuccessor.Response>> Start(CycleWorkspace workspace, FakeRepository git, string slug, string id)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        return await gate.Send<StartSuccessor.Command, StartSuccessor.Response>(new StartSuccessor.Command(slug, Parse(id)));
    }

    private static async Task<ReadSuccessors.Response> Read(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        Result<ReadSuccessors.Response> result = await gate.Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
