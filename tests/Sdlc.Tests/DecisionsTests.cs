using System.Globalization;
using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Hooks;
using Sdlc.Core.Proposals;

namespace Sdlc.Tests;

/// <summary>
/// The choices a solution has settled, and what an approved cycle stated.
/// </summary>
/// <remarks>
/// Two failures to guard against. An entry that no person approved would make the record a place where
/// an agent's draft prose becomes doctrine later cycles align to, which is the authorship problem this
/// shape exists to answer -- so most of the proposer's tests are about what does <em>not</em>
/// contribute. And a reversed entry losing its place on the page would make a cycle that cited it
/// unreadable, which is the irreversible half.
/// </remarks>
[TestClass]
public sealed class DecisionsTests
{
    [TestMethod]
    [DataRow("DEC_01", 1)]
    [DataRow("DEC_07", 7)]
    [DataRow("DEC_123", 123)]
    public void An_id_reads_back_to_its_number(string text, int number)
    {
        Assert.IsTrue(DecisionId.TryParse(text, out DecisionId? id));
        Assert.AreEqual(number, id.Value.Number);
        Assert.AreEqual(text, id.Value.ToString());
    }

    [TestMethod]
    [DataRow("DEC_1")]
    [DataRow("DEC_01.2")]
    [DataRow("LOC_01")]
    [DataRow("CON_01")]
    [DataRow("DEC_00")]
    [DataRow("")]
    [DataRow(null)]
    public void What_is_not_a_decision_id_is_refused(string? text)
    {
        // No revision suffix, deliberately. A constraint carries one because its wording can improve
        // without changing what it forbids; a decision forbids nothing, so a rewritten reason is a
        // different reason and takes a new id. And a constraint id is not a decision id: a citation that
        // could be either would be one nobody could resolve.
        Assert.IsFalse(DecisionId.TryParse(text, out _));
    }

    [TestMethod]
    public void A_decision_carries_four_parts_and_a_missing_one_is_named()
    {
        Result<IReadOnlyList<Decision>> read = DecisionParser.Parse(
            string.Join(
                Environment.NewLine,
                "## DEC_01 — publishing goes through an outbox",
                string.Empty,
                "**Decided:** the handler writes to an outbox and a dispatcher publishes from it",
                string.Empty,
                "**Because:** the broker cannot join our transaction",
                string.Empty,
                "**From:** 20260901_0900_01"),
            "decisions");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(string.Join("; ", read.Errors.Select(e => e.Message)), "Instead of");
    }

    [TestMethod]
    public void Instead_of_is_what_separates_a_decision_from_a_fact()
    {
        // The load-bearing part, and the one a reader is most likely to leave out. An entry whose
        // alternatives were never real records something nobody chose, and reading it costs attention at
        // every cycle for nothing.
        Decision only = Parsed(Entry("DEC_01")).Single();

        Assert.AreNotEqual(0, only.InsteadOf.Length);
        Assert.IsTrue(only.IsStanding);
    }

    [TestMethod]
    public void An_id_that_appears_twice_is_refused()
    {
        Result<IReadOnlyList<Decision>> read = DecisionParser.Parse(
            Entry("DEC_01") + Environment.NewLine + Entry("DEC_01"),
            "decisions");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(
            string.Join("; ", read.Errors.Select(e => e.Message)),
            "never reused");
    }

    [TestMethod]
    public void A_reversed_decision_keeps_its_place_and_stops_standing()
    {
        // Same discipline section 20's own preamble states for the framework's decisions: a reversed one
        // is superseded by a new id rather than edited, so a cycle citing it still means what it meant.
        IReadOnlyList<Decision> all = Parsed(
            Entry("DEC_01", supersededBy: "DEC_02") + Environment.NewLine + Entry("DEC_02"));

        Assert.AreEqual(2, all.Count, "The reversed entry stays on the page.");
        Assert.IsFalse(all[0].IsStanding);
        Assert.IsTrue(all[1].IsStanding);
    }

    [TestMethod]
    public void The_next_id_is_one_past_the_highest_and_never_fills_a_gap()
    {
        IReadOnlyList<Decision> all = Parsed(
            Entry("DEC_01") + Environment.NewLine + Entry("DEC_04"));

        Assert.AreEqual("DEC_05", DecisionParser.NextId(all).ToString());
    }

    [TestMethod]
    public async Task A_solution_with_no_record_is_reported_rather_than_refused()
    {
        // Most solutions have none for a while. A command that refused would make "nothing settled yet"
        // indistinguishable from a broken install.
        using var workspace = CycleWorkspace.Create();

        ReadDecisions.Response response = await ReadRecord(workspace);

        Assert.IsFalse(response.Recorded);
        StringAssert.Contains(
            DecisionsRenderer.Render(response, DecisionDetail.Full),
            "No decisions recorded.");
    }

    [TestMethod]
    public async Task A_reversed_entry_is_withheld_unless_it_is_asked_for()
    {
        using var workspace = CycleWorkspace.Create();
        Record(workspace, Entry("DEC_01", supersededBy: "DEC_02"), Entry("DEC_02"));

        ReadDecisions.Response standing = await ReadRecord(workspace);
        ReadDecisions.Response everything = await ReadRecord(workspace, all: true);

        Assert.AreEqual(1, standing.Decisions.Count, "A stage wants what stands.");
        Assert.AreEqual(1, standing.Superseded, "And is told how many it is not seeing.");
        Assert.AreEqual(2, everything.Decisions.Count, "History needs the entry a cycle cited.");
    }

    [TestMethod]
    public async Task The_record_says_departing_is_legal_and_silence_is_not()
    {
        // The distinction CON_19 rests on, and the one a reader will get wrong in the other direction: a
        // decision that cannot be revisited is a cage, and the first case it does not fit would see the
        // record ignored rather than amended. What the rule catches is nobody noticing they departed.
        using var workspace = CycleWorkspace.Create();
        Record(workspace, Entry("DEC_01"));

        string rendered = DecisionsRenderer.Render(await ReadRecord(workspace), DecisionDetail.Full);

        StringAssert.Contains(rendered, "may depart");
        StringAssert.Contains(rendered, "CON_19");
        StringAssert.Contains(rendered, "departing in silence is not");
    }

    [TestMethod]
    public async Task Only_an_approved_cycle_states_anything()
    {
        // The whole provenance story. An approval records a hash of the three planning documents against
        // a person's name, so a passage from an approved cycle is one a person signed off on those exact
        // bytes; reading an unapproved cycle would put unsigned prose in a record whose only value is
        // that a person signed it.
        using var workspace = CycleWorkspace.Create();
        Stated(workspace);

        ProposeDecisions.Response before = await Propose(workspace);
        Assert.AreEqual(0, before.Candidates.Count);
        Assert.AreEqual(1, before.Unapproved, "And it says why it found nothing.");

        Approve(workspace);

        ProposeDecisions.Response after = await Propose(workspace);
        Assert.AreEqual(2, after.Candidates.Count, "The intent's and the plan's, one each.");
        Assert.AreEqual(0, after.Unapproved);
    }

    [TestMethod]
    public async Task An_approval_that_was_withdrawn_states_nothing()
    {
        // The ledger is append-only and the last entry stands, so a cycle approved and then reopened is
        // not approved -- and a passage taken from one would be prose a person explicitly sent back.
        using var workspace = CycleWorkspace.Create();
        Stated(workspace);
        Approve(workspace, reopened: true);

        Assert.AreEqual(0, (await Propose(workspace)).Candidates.Count);
    }

    [TestMethod]
    public async Task A_stopped_cycle_states_nothing_even_when_it_was_approved()
    {
        // A decision lifted out of work somebody abandoned is the opposite of a settled choice.
        using var workspace = CycleWorkspace.Create();
        Stated(workspace);
        Approve(workspace);
        CycleWorkspace.WriteRaw(workspace.Cycle.AbandonedPath, "stopped, and why.");

        Assert.AreEqual(0, (await Propose(workspace)).Candidates.Count);
    }

    [TestMethod]
    public async Task Each_section_is_handed_over_whole_rather_than_line_by_line()
    {
        // A decision is a sentence or two. A line-per-candidate listing would split one choice across
        // several entries and read as several choices.
        using var workspace = CycleWorkspace.Create();
        Stated(workspace);
        Approve(workspace);

        ProposeDecisions.Response response = await Propose(workspace);

        Candidate plan = response.Candidates.Single(c => c.Source == DecisionSource.Plan);
        StringAssert.Contains(plan.Passage, "outbox");
        StringAssert.Contains(plan.Passage, "cannot join", "The whole paragraph, not its first line.");
    }

    [TestMethod]
    public async Task What_is_already_recorded_is_handed_over_so_a_proposal_is_not_a_duplicate()
    {
        using var workspace = CycleWorkspace.Create();
        Record(workspace, Entry("DEC_01"));
        Stated(workspace);
        Approve(workspace);

        ProposeDecisions.Response response = await Propose(workspace);

        Assert.AreEqual("DEC_01", response.Standing.Single().Id.ToString());
        Assert.AreEqual("DEC_02", response.NextId.ToString());
        StringAssert.Contains(ProposalsRenderer.Render(response), "DEC_01");
    }

    [TestMethod]
    public async Task The_admission_tests_are_printed_whenever_there_is_material()
    {
        using var workspace = CycleWorkspace.Create();
        Stated(workspace);
        Approve(workspace);

        string rendered = ProposalsRenderer.Render(await Propose(workspace));

        StringAssert.Contains(rendered, "the alternatives were real");
        StringAssert.Contains(rendered, "it forbids nothing");
    }

    [TestMethod]
    [DataRow("C:/solution/.sdlc/decisions.md")]
    [DataRow(".sdlc/decisions.md")]
    [DataRow(".sdlc\\DECISIONS.MD")]
    public void The_record_is_a_file_only_a_person_writes(string path)
    {
        // A stage reads it for alignment, so an agent that could write it could invent doctrine and then
        // align to it. That is the one failure the record exists to avoid, which is why it is the sixth
        // protected file rather than an ordinary one.
        Assert.IsTrue(ProtectedPaths.IsProtected(path, out ProtectedFile? file));
        Assert.IsNotNull(file);
        StringAssert.Contains(file.Reason, "propose decisions");
    }

    [TestMethod]
    public void A_shell_command_naming_the_record_is_refused_too()
    {
        // A shell can write a file without any tool naming its path, so the path rule alone would leave
        // the obvious way round it open.
        Assert.IsTrue(
            ProtectedPaths.CommandTouchesProtected("echo settled >> .sdlc/decisions.md", out _));
    }

    /// <summary>Dispatches the reader the way the application composes it.</summary>
    private static async Task<ReadDecisions.Response> ReadRecord(
        CycleWorkspace workspace,
        bool all = false)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ReadDecisions.Response> result = await gate
            .Send<ReadDecisions.Query, ReadDecisions.Response>(new ReadDecisions.Query(all));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    /// <summary>Dispatches the proposer.</summary>
    private static async Task<ProposeDecisions.Response> Propose(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ProposeDecisions.Response> result = await gate
            .Send<ProposeDecisions.Query, ProposeDecisions.Response>(new ProposeDecisions.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static IReadOnlyList<Decision> Parsed(string markdown)
    {
        Result<IReadOnlyList<Decision>> read = DecisionParser.Parse(markdown, "decisions");

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));
        return read.Value;
    }

    /// <summary>Writes the solution's decision record.</summary>
    private static void Record(CycleWorkspace workspace, params string[] entries) =>
        CycleWorkspace.WriteRaw(
            Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, DecisionParser.FileName),
            "# This solution's decisions"
                + Environment.NewLine
                + string.Join(Environment.NewLine, entries));

    private static string Entry(string id, string? supersededBy = null)
    {
        List<string> lines =
        [
            string.Empty,
            string.Create(CultureInfo.InvariantCulture, $"## {id} — publishing goes through an outbox"),
            string.Empty,
            "**Decided:** the handler writes to an outbox and a dispatcher publishes from it",
            string.Empty,
            "**Because:** the broker cannot join our transaction, so a direct publish can succeed",
            "after the write has rolled back",
            string.Empty,
            "**Instead of:** a direct publish from the handler, which costs the atomicity; or",
            "two-phase commit, which the broker does not support",
            string.Empty,
            "**From:** 20260901_0900_01",
            string.Empty,
        ];

        if (supersededBy is not null)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"**Superseded-by:** {supersededBy}"));
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Writes an intent and a plan that each state something, and the spec between them.</summary>
    /// <remarks>
    /// The spec carries nothing these tests read. It is here because the approval writer records the real
    /// hash of all three planning documents, and a ledger is what decides whether a passage is offered at
    /// all — so a cycle missing one could not be approved in the first place.
    /// </remarks>
    private static void Stated(CycleWorkspace workspace)
    {
        workspace.WriteSpec(criteria: 1);

        CycleWorkspace.WriteRaw(
            workspace.Cycle.PathOf(ArtifactKind.Intent),
            string.Join(
                Environment.NewLine,
                "---",
                "framework_version: " + Cycle.FrameworkVersion,
                "cycle: " + workspace.Cycle.Id,
                "type: feature addition",
                "---",
                string.Empty,
                "# intent",
                string.Empty,
                "## Problem",
                string.Empty,
                "Orders publish twice when the broker retries.",
                string.Empty,
                "## Fixed decisions",
                string.Empty,
                "Keep it very simple. We are not introducing a new broker for this.",
                string.Empty,
                "## Open"));

        CycleWorkspace.WriteRaw(
            workspace.Cycle.PathOf(ArtifactKind.Plan),
            string.Join(
                Environment.NewLine,
                "---",
                "framework_version: " + Cycle.FrameworkVersion,
                "cycle: " + workspace.Cycle.Id,
                "tier: contract",
                "---",
                string.Empty,
                "# plan",
                string.Empty,
                "## Shape and departures",
                string.Empty,
                "The handler writes to an outbox in the same transaction.",
                "The declared default is a direct publish, which the broker cannot join.",
                string.Empty,
                "## Open"));
    }

    /// <summary>
    /// Records a gate decision.
    /// </summary>
    /// <remarks>
    /// The single-decision case goes through the workspace's own writer, so these tests read the ledger
    /// shape the rest of the suite writes rather than a second idea of it. The withdrawn case needs two
    /// entries, which that writer does not do, and the point being tested is that the <em>last</em> one
    /// stands -- so it is composed here with both.
    /// </remarks>
    private static void Approve(CycleWorkspace workspace, bool reopened = false)
    {
        if (!reopened)
        {
            workspace.WriteApproval();
            return;
        }

        List<string> lines =
        [
            "---",
            "cycle: " + workspace.Cycle.Id,
            "framework_version: " + Cycle.FrameworkVersion,
            "decisions:",
            "  - decision: approved",
            "    by: Somebody",
            "    at: 2026-09-01T10:00:00Z",
            "    hashes:",
        ];

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            lines.Add("      " + Cycle.FileNameOf(kind) + ": " + workspace.HashOf(kind));
        }

        lines.AddRange(
            "  - decision: reopened",
            "    by: Somebody",
            "    at: 2026-09-02T10:00:00Z",
            "    reason: the plan needs rewriting",
            "    hashes:");

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            lines.Add("      " + Cycle.FileNameOf(kind) + ": " + workspace.HashOf(kind));
        }

        lines.Add("---");

        CycleWorkspace.WriteRaw(
            workspace.Cycle.ApprovalPath,
            string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }
}
