using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Harvest;
using Sdlc.Core.Leads;
using Sdlc.Core.Successors;

namespace Sdlc.Tests;

/// <summary>
/// A successor a person wants, and not now, is held: nothing unattended starts it, and starting it lifts the hold (D-220).
/// </summary>
[TestClass]
public sealed class HeldSuccessorTests
{
    private const string Integration = "origin/development";
    private const string First = "20261103_0900_00";
    private const string Second = "20261103_0900_01";

    [TestMethod]
    public void A_held_successor_reads_the_day_it_was_held_and_a_held_that_is_not_a_day_is_refused()
    {
        Result<Successor> held = Successor.Parse("totals", SuccessorTests.File(First, "held: 2026-11-26\n"));

        Assert.IsTrue(held.IsSuccess, string.Join("; ", held.Errors.Select(e => e.Message)));
        Assert.AreEqual(new DateOnly(2026, 11, 26), held.Value.Held);
        Assert.IsNull(Successor.Parse("totals", SuccessorTests.File(First)).Value.Held, "No held: line, no hold.");

        Result<Successor> wrong = Successor.Parse("totals", SuccessorTests.File(First, "held: later\n"));
        Assert.IsTrue(wrong.IsFailed);
        StringAssert.Contains(wrong.Errors[0].Message, "held: 'later' is not a day");
    }

    [TestMethod]
    public async Task A_held_successor_is_held_whether_its_predecessor_merged_or_not_and_never_ready()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Held(workspace, "totals", First);
        FakeRepository git = Git();

        Assert.AreEqual(SuccessorState.Held, (await Read(workspace, git)).Find("totals")!.State);

        Merge(git, First);
        ReadSuccessors.Response after = await Read(workspace, git);

        Assert.AreEqual(SuccessorState.Held, after.Find("totals")!.State);
        Assert.IsTrue(after.Sound, "A hold is not a problem with the set.");
    }

    [TestMethod]
    public async Task A_held_successor_naming_nothing_is_still_unresolved_because_that_needs_correcting_either_way()
    {
        using CycleWorkspace workspace = Solution();
        Held(workspace, "totals", "successor:nowhere");

        ReadSuccessors.Response read = await Read(workspace, Git());

        Assert.AreEqual(SuccessorState.Unresolved, read.Find("totals")!.State);
        Assert.IsFalse(read.Sound);
    }

    [TestMethod]
    public async Task A_person_starting_a_held_successor_lifts_the_hold_once_what_it_waits_on_merged()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Opened(workspace, Second);
        Held(workspace, "totals", First);
        FakeRepository git = Git();

        Result<StartSuccessor.Response> early = await Start(workspace, git, "totals", Second);
        Assert.IsTrue(early.IsFailed);
        StringAssert.Contains(early.Errors[0].Message, "is on hold, and is waiting too");

        Merge(git, First);
        Result<ReadLead.Response> lead = await Lead(workspace, git, "totals");
        Assert.IsTrue(lead.IsSuccess, string.Join("; ", lead.Errors.Select(e => e.Message)));

        Result<StartSuccessor.Response> started = await Start(workspace, git, "totals", Second);
        Assert.IsTrue(started.IsSuccess, string.Join("; ", started.Errors.Select(e => e.Message)));
        Assert.IsFalse(File.Exists(Path.Combine(Successor.DirectoryOf(workspace.Root), "totals.md")), "Its file goes, and the hold with it.");
    }

    [TestMethod]
    public async Task The_listing_the_backlog_and_the_sitrep_show_a_held_successor_apart_with_the_day_it_was_held()
    {
        using CycleWorkspace workspace = Solution();
        Opened(workspace, First);
        Held(workspace, "totals", First);
        FakeRepository git = Git();
        Merge(git, First);
        ReadSuccessors.Response read = await Read(workspace, git);

        string listing = SuccessorsRenderer.Render(read);
        StringAssert.Contains(listing, "  held ");
        StringAssert.Contains(listing, "on hold since 2026-11-26");
        Assert.IsFalse(listing.Contains("  ready ", StringComparison.Ordinal), listing);

        string backlog = BacklogRenderer.Render(read);
        StringAssert.Contains(backlog, "On hold -- no wave takes these");
        StringAssert.Contains(backlog, "held since 2026-11-26; start it: /sdlc:intent --from successor:totals");
        Assert.IsFalse(backlog.Contains("Ready now:", StringComparison.Ordinal), backlog);
    }

    [TestMethod]
    public void A_successor_marked_h_is_drafted_held_and_only_a_successor_can_be_held()
    {
        IReadOnlyList<DraftHarvest.Proposal> read = DraftHarvest.Parse(
            "## Successors\n\n### [h] add pause-chart — pause the live chart\n**Succeeds:** " + First
            + "\n\n**Outcome:** a trader pauses the chart.\n\n**Why it matters:** WCAG 2.2.2.\n").Value;

        DraftHarvest.Proposal proposal = read.Single();
        Assert.IsTrue(proposal.Accepted, "Held is accepted: it is drafted.");
        Assert.IsTrue(proposal.Held);

        string text = DraftHarvest.SuccessorFile(proposal, new DateOnly(2026, 11, 26)).Value;
        StringAssert.Contains(text, "held: 2026-11-26");
        Assert.AreEqual(new DateOnly(2026, 11, 26), Successor.Parse("pause-chart", text).Value.Held);

        Result<IReadOnlyList<DraftHarvest.Proposal>> rule = DraftHarvest.Parse("## Rules\n\n### [h] add — a rule\n");
        Assert.IsTrue(rule.IsFailed);
        StringAssert.Contains(rule.Errors[0].Message, "only a successor can be held");
    }

    [TestMethod]
    public void A_held_mark_is_recorded_as_accepted_and_the_summary_shows_it()
    {
        const string File = "## Successors\n\n### [h] add pause-chart — pause\n\n### [x] add sign-in — sign in\n\n### [ ] add export — export\n";

        CollectionAssert.AreEqual(new[] { "add pause-chart — pause", "add sign-in — sign in" }, RecordHarvest.Read(File).Accepted.ToArray());

        HarvestSummary.Response summary = HarvestSummary.Read(File);
        Assert.AreEqual(3, summary.Proposals.Count);
        Assert.IsTrue(summary.Proposals[0].Held);
        StringAssert.Contains(HarvestSummary.Render(summary), "  1. [h] add pause-chart — pause");
        StringAssert.Contains(HarvestSummary.Render(summary), "Proposals: 3, 2 marked");
    }

    private static void Held(CycleWorkspace workspace, string slug, string succeeds)
    {
        string directory = Successor.DirectoryOf(workspace.Root);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, slug + ".md"), SuccessorTests.File(succeeds, "held: 2026-11-26\n"));
    }

    private static CycleWorkspace Solution() => CycleWorkspace.Create("20261101_0800_00");

    private static void Opened(CycleWorkspace workspace, string id) =>
        Directory.CreateDirectory(new Cycle(Parse(id), workspace.Root).Directory);

    private static void Merge(FakeRepository git, string id)
    {
        git.PathsAt.Add(Integration + ":.sdlc/cycles/" + id + "/intent.md");
        git.Files[Integration + ":.sdlc/cycles/" + id + "/verify.md"] = "---\ncycle: " + id + "\noutcome: pass\n---\n\n# v\n";
    }

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
