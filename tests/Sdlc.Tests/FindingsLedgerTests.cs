using System.Text.Json.Nodes;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Approvals;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;

namespace Sdlc.Tests;

/// <summary>
/// One findings ledger per artifact (D-107): each finding once, each round an entry chained by hash, and
/// the approval anchoring the chain.
/// </summary>
[TestClass]
public sealed class FindingsLedgerTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private static readonly Citation _rule = new("CON_06", "a plan's proof is adequate to the criterion it claims to prove");

    [TestMethod]
    public void Restating_a_finding_where_it_stands_records_nothing()
    {
        FindingsLedger ledger = Build(
            Round(1, Open("F_01")),
            Round(2, Open("F_01"), Open("F_02")));

        Assert.AreEqual(2, ledger.Findings.Count);
        Assert.AreEqual(1, ledger.Findings[0].Transitions.Count, "F_01 was raised once and never moved.");
        CollectionAssert.AreEqual(
            new[] { "F_02" },
            ledger.Views()[1].Findings.Select(finding => finding.Id).ToArray(),
            "Round two raised F_02 and moved nothing else, so that is all it holds.");
    }

    [TestMethod]
    public void A_moved_disposition_is_one_transition()
    {
        FindingsLedger ledger = Build(
            Round(1, Open("F_01")),
            Round(2, Open("F_01") with { Disposition = Disposition.Answered }));

        CollectionAssert.AreEqual(
            new[] { Disposition.Open, Disposition.Answered },
            ledger.Findings[0].Transitions.Select(transition => transition.Disposition).ToArray());
        Assert.AreEqual(0, FindingsReader.StandingBlockers(ledger.Views()).Count);
    }

    [TestMethod]
    public void A_known_id_in_other_words_is_refused()
    {
        FindingsLedger ledger = Build(Round(1, Open("F_01")));

        Result<FindingsLedger> refused = ledger.Append(Round(2, Open("F_01") with { Statement = "a different objection." }));

        Assert.IsTrue(refused.IsFailed, "One id meaning two objections is what the id exists to prevent.");
        StringAssert.Contains(refused.Errors[0].Message, "F_01");
        StringAssert.Contains(refused.Errors[0].Message, "new id");
    }

    [TestMethod]
    public void A_reused_id_is_refused_with_the_next_free_one_under_its_prefix()
    {
        // A perspective reader numbers from F_01 every round, having no history (D-108).
        FindingsLedger ledger = Build(
            Round(1, Suggestion("maintainability.F_01"), Suggestion("maintainability.F_02"), Open("F_01")));

        Result<FindingsLedger> refused = ledger.Append(Round(2, Suggestion("maintainability.F_01") with { Statement = "a new objection." }));

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "the next free one is maintainability.F_03");
    }

    [TestMethod]
    public void Every_entry_is_chained_to_the_one_before()
    {
        FindingsLedger ledger = Build(Round(1, Open("F_01")), Round(2), Round(3, Open("F_02")));

        Assert.IsNull(ledger.Rounds[0].PreviousSha256);
        Assert.AreEqual(ledger.Rounds[0].EntrySha256, ledger.Rounds[1].PreviousSha256);
        Assert.AreEqual(ledger.Rounds[1].EntrySha256, ledger.Rounds[2].PreviousSha256);
        Assert.IsTrue(ledger.Verify("plan.json").IsSuccess);
    }

    [TestMethod]
    public void An_edited_finding_fails_the_chain_at_the_round_that_raised_it()
    {
        FindingsLedger ledger = Build(Round(1, Open("F_01")), Round(2, Open("F_02")));

        FindingsLedger edited = ledger with
        {
            Findings = [ledger.Findings[0] with { Statement = "nothing much." }, ledger.Findings[1]],
        };

        Result verified = edited.Verify("plan.json");

        Assert.IsTrue(verified.IsFailed);
        StringAssert.Contains(verified.Errors[0].Message, "round 1");
    }

    [TestMethod]
    public void A_ledger_read_back_from_its_file_verifies_unchanged()
    {
        FindingsLedger ledger = Build(Round(1, Open("F_01")), Round(2, Open("F_01") with { Disposition = Disposition.Withdrawn }));

        Result<FindingsLedger> read = FindingsReader.ReadLedger(FindingsReader.Write(ledger), "plan.json");

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(
            ledger.Rounds.Select(round => round.EntrySha256).ToArray(),
            read.Value.Rounds.Select(round => round.EntrySha256).ToArray());
    }

    [TestMethod]
    public void Twenty_six_rounds_restating_everything_store_each_finding_once()
    {
        // The shape that made this a decision: a session that copied every finding into every round. The
        // twenty-sixth round file held 798 findings, 764 of them copies.
        List<Finding> carried = [];
        var ledger = FindingsLedger.Empty("20261021_0118_04", "plan.md");

        for (int round = 1; round <= 26; round++)
        {
            carried.AddRange(Enumerable.Range(0, 30).Select(index => Suggestion($"F_{round:D2}_{index:D2}")));
            ledger = ledger.Append(Round(round, [.. carried])).Value;
        }

        Assert.AreEqual(780, ledger.Findings.Count);
        Assert.AreEqual(780, ledger.Findings.Sum(finding => finding.Transitions.Count), "Raised once each, and never restated.");
        Assert.AreEqual(30, ledger.Views()[^1].Findings.Count);
        Assert.AreEqual(780, FindingsReader.StandingSuggestions(ledger.Views()).Count);
    }

    [TestMethod]
    public async Task A_cycle_with_round_files_is_converted_by_its_next_round_and_keeps_its_hashes()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteBlockingRound(ArtifactKind.Intent, round: 1);
        workspace.WriteAnsweredRound(ArtifactKind.Intent, round: 2);

        IReadOnlyList<FindingsRound> before = await History(workspace, ArtifactKind.Intent);

        RecordRound.Response written = (await Record(workspace, ArtifactKind.Intent, """{ "findings": [] }""")).Value;

        Assert.AreEqual(3, written.Written.Round);
        Assert.AreEqual(2, written.Converted);
        Assert.IsFalse(File.Exists(workspace.Cycle.FindingsPath(ArtifactKind.Intent, 1)), "The round files go in the same write.");
        Assert.IsFalse(File.Exists(workspace.Cycle.FindingsPath(ArtifactKind.Intent, 2)));

        IReadOnlyList<FindingsRound> after = await History(workspace, ArtifactKind.Intent);

        CollectionAssert.AreEqual(
            before.Select(round => round.EntrySha256).ToArray(),
            after.Take(2).Select(round => round.EntrySha256).ToArray(),
            "An approval anchored on the rounds as read in memory still holds once they are written.");
    }

    [TestMethod]
    public async Task A_ledger_with_round_files_beside_it_is_refused()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await Record(workspace, ArtifactKind.Intent, """{ "findings": [] }""");
        workspace.WriteCleanRound(ArtifactKind.Intent, round: 2);

        Result<IReadOnlyList<FindingsRound>> read = await FindingsReader.ReadHistory(
            workspace.Cycle,
            ArtifactKind.Intent,
            CancellationToken.None);

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "round files beside it");
    }

    [TestMethod]
    public async Task A_ledger_edited_by_hand_cannot_be_read_and_the_gate_refuses()
    {
        using CycleWorkspace workspace = await Interrogated();
        workspace.WriteApproval();

        string path = workspace.Cycle.FindingsLedgerPath(ArtifactKind.Spec);
        JsonNode ledger = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        ledger["rounds"]![0]!["note"] = "nothing happened here.";
        await File.WriteAllTextAsync(path, ledger.ToJsonString());

        ApprovalGate.Response verdict = await Check(workspace);

        Assert.AreEqual(ApprovalGate.GateRefusal.UnreadableRound, verdict.Refusal, verdict.Explanation);
        StringAssert.Contains(verdict.Explanation, "does not hash to what it records");
    }

    [TestMethod]
    public async Task An_approval_anchored_on_the_rounds_holds_and_refuses_a_history_rewritten_behind_it()
    {
        using CycleWorkspace workspace = await Interrogated();
        workspace.WriteApproval(rounds: await Anchors(workspace));

        Assert.AreEqual(ApprovalGate.GateRefusal.None, (await Check(workspace)).Refusal);

        // Rewritten and rechained, so the ledger is consistent on its own. Only the anchor can tell.
        IReadOnlyList<FindingsRound> rounds = await History(workspace, ArtifactKind.Spec);
        FindingsLedger rewritten = FindingsLedger.FromRounds([rounds[0] with { Note = "a different argument." }]).Value;
        await File.WriteAllTextAsync(workspace.Cycle.FindingsLedgerPath(ArtifactKind.Spec), FindingsReader.Write(rewritten));

        ApprovalGate.Response verdict = await Check(workspace);

        Assert.AreEqual(ApprovalGate.GateRefusal.RewrittenRound, verdict.Refusal, verdict.Explanation);
        StringAssert.Contains(verdict.Explanation, "spec.md: round 1");
    }

    [TestMethod]
    public async Task A_round_after_the_anchor_leaves_the_approval_standing()
    {
        using CycleWorkspace workspace = await Interrogated();
        workspace.WriteApproval(rounds: await Anchors(workspace));

        await Record(workspace, ArtifactKind.Plan, """{ "findings": [] }""");

        Assert.AreEqual(ApprovalGate.GateRefusal.None, (await Check(workspace)).Refusal, "The anchor pins a prefix, not the file.");
    }

    [TestMethod]
    public async Task An_approval_written_before_anchors_is_judged_as_it_always_was()
    {
        using CycleWorkspace workspace = await Interrogated();
        workspace.WriteApproval();

        Assert.AreEqual(ApprovalGate.GateRefusal.None, (await Check(workspace)).Refusal);
    }

    [TestMethod]
    public async Task The_approval_draft_prints_each_ledger_round_and_hash()
    {
        using CycleWorkspace workspace = await Interrogated();
        IReadOnlyDictionary<ArtifactKind, RoundAnchor> anchors = await Anchors(workspace);

        using var gate = Gate.For(workspace.Root);
        ApprovalText.Response draft = (await gate.Send<ApprovalText.Query, ApprovalText.Response>(
            new ApprovalText.Query(workspace.Cycle.Id, "Peter Almanzar", GateDecision.Approved))).Value;

        StringAssert.Contains(draft.Entry, "    rounds:");
        StringAssert.Contains(draft.Entry, $"plan.md:    {{ round: 1, sha256: {anchors[ArtifactKind.Plan].Sha256} }}");

        // And what it prints is what the ledger reads back: the rename is the whole act.
        Result<ApprovalLedger> read = ApprovalLedger.Read(draft.FileText!, "approval.md");
        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(error => error.Message)));
        Assert.AreEqual(anchors[ArtifactKind.Spec], read.Value.Current!.Rounds!["spec.md"]);
    }

    [TestMethod]
    public void An_open_blocker_carried_silently_keeps_its_rule_from_going_quiet()
    {
        // F_02 stands open under the same rule throughout and is never mentioned again. Round two answers
        // F_01 alone, which leaves the rule open, so the new F_03 in round three is the reader moving on to
        // the next place, not the rule coming back.
        FindingsLedger ledger = Build(
            Round(1, Open("F_01"), Open("F_02")),
            Round(2, Open("F_01") with { Disposition = Disposition.Answered }),
            Round(3, Open("F_03")));

        Assert.AreEqual(0, Oscillation.Count(ledger.Views()).Rules.Count);
    }

    private static FindingsLedger Build(params FindingsRound[] rounds)
    {
        var ledger = FindingsLedger.Empty("20260929_1456_32", "plan.md");

        foreach (FindingsRound round in rounds)
        {
            Result<FindingsLedger> appended = ledger.Append(round);
            Assert.IsTrue(appended.IsSuccess, string.Join("; ", appended.Errors.Select(error => error.Message)));
            ledger = appended.Value;
        }

        return ledger;
    }

    private static FindingsRound Round(int number, params Finding[] findings) =>
        new("1.0.0", "20260929_1456_32", "plan.md", "3f9a2c7b", _now.AddMinutes(number), number, findings, SubjectSha256: "91c4de08");

    private static Finding Open(string id) =>
        new(id, Severity.Blocking, Disposition.Open, _rule, "something that must be answered.");

    private static Finding Suggestion(string id) =>
        new(id, Severity.Suggestion, Disposition.Open, null, "something worth doing.");

    private static async Task<CycleWorkspace> Interrogated()
    {
        var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            Result<RecordRound.Response> written = await Record(workspace, kind, """{ "findings": [] }""");
            Assert.IsTrue(written.IsSuccess, string.Join("; ", written.Errors.Select(error => error.Message)));
        }

        return workspace;
    }

    private static async Task<IReadOnlyDictionary<ArtifactKind, RoundAnchor>> Anchors(CycleWorkspace workspace)
    {
        Dictionary<ArtifactKind, RoundAnchor> anchors = [];

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            FindingsRound latest = (await History(workspace, kind))[^1];
            anchors[kind] = new RoundAnchor(latest.Round, latest.EntrySha256!);
        }

        return anchors;
    }

    private static async Task<IReadOnlyList<FindingsRound>> History(CycleWorkspace workspace, ArtifactKind kind)
    {
        Result<IReadOnlyList<FindingsRound>> read = await FindingsReader.ReadHistory(workspace.Cycle, kind, CancellationToken.None);
        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(error => error.Message)));
        return read.Value;
    }

    private static async Task<Result<RecordRound.Response>> Record(CycleWorkspace workspace, ArtifactKind kind, string json)
    {
        string from = Path.Combine(workspace.Root, "readers." + Cycle.NameOf(kind) + ".json");
        await File.WriteAllTextAsync(from, json);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<TimeProvider>(new FakeClock(_now)));
        return await gate.Send<RecordRound.Command, RecordRound.Response>(
            new RecordRound.Command(workspace.Cycle.Id, kind, from));
    }

    private static async Task<ApprovalGate.Response> Check(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ApprovalGate.Response> result = await gate
            .Send<ApprovalGate.Query, ApprovalGate.Response>(new ApprovalGate.Query(workspace.Cycle.Id));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }
}
