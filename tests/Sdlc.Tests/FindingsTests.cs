using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;

namespace Sdlc.Tests;

[TestClass]
public sealed class FindingsTests
{
    [TestMethod]
    public void A_round_reads_back_with_its_finding_in_full()
    {
        const string json = """
            {
              "framework_version": "1.0.0",
              "cycle": "20260929_1456_32",
              "artifact": ".sdlc/cycles/20260929_1456_32/spec.md",
              "interrogated_at": "2026-09-29T01:08:39Z",
              "round": 1,
              "findings": [
                {
                  "id": "F_01",
                  "severity": "blocking",
                  "disposition": "open",
                  "cites": "CON_04.1",
                  "statement": "the decided delivery has no criterion.",
                  "locus": "## Acceptance criteria",
                  "remedy": "add a criterion for the delivery itself."
                }
              ]
            }
            """;

        Result<FindingsRound> result = FindingsReader.Read(json, "test");

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.AreEqual(1, result.Value.Round);
        Assert.AreEqual("1.0.0", result.Value.FrameworkVersion);

        Finding finding = result.Value.Findings.Single();
        Assert.AreEqual("F_01", finding.Id);
        Assert.AreEqual(Severity.Blocking, finding.Severity);
        Assert.AreEqual(Disposition.Open, finding.Disposition);
        Assert.AreEqual("CON_04.1", finding.Cites);
        Assert.IsTrue(finding.IsOpenBlocker);
        Assert.IsFalse(result.Value.IsClean);
    }

    [TestMethod]
    public void An_empty_findings_array_is_a_clean_round()
    {
        Result<FindingsRound> result = FindingsReader.Read(
            """{ "round": 2, "findings": [] }""",
            "test");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.Value.IsClean);
        Assert.AreEqual(0, result.Value.Findings.Count);
    }

    [TestMethod]
    public void A_round_with_no_findings_array_is_rejected_rather_than_read_as_clean()
    {
        // A file that forgot to say and a file that says nothing is wrong are different things, and
        // treating them alike would let a malformed round read as nothing to answer.
        Result<FindingsRound> result = FindingsReader.Read("""{ "round": 2 }""", "test");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("findings array", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Invalid_json_is_reported_with_the_source_named()
    {
        Result<FindingsRound> result = FindingsReader.Read("{ not json", "spec.r1.json");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("spec.r1.json", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("suggestion", Severity.Suggestion)]
    [DataRow("advice", Severity.Advice)]
    [DataRow("blocking", Severity.Blocking)]
    public void Each_severity_reads_back(string text, Severity expected)
    {
        Result<FindingsRound> result = FindingsReader.Read(
            $$"""{ "round": 1, "findings": [{ "id": "F_01", "severity": "{{text}}", "disposition": "open", "statement": "something." }] }""",
            "test");

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.AreEqual(expected, result.Value.Findings.Single().Severity);
    }

    [TestMethod]
    public void Advice_cites_nothing_and_does_not_block()
    {
        Result<FindingsRound> result = FindingsReader.Read(
            """{ "round": 1, "findings": [{ "id": "F_09", "severity": "advice", "disposition": "open", "statement": "this would read better as a table." }] }""",
            "test");

        Assert.IsTrue(result.IsSuccess);
        Finding finding = result.Value.Findings.Single();
        Assert.IsNull(finding.Cites);
        Assert.IsFalse(finding.IsOpenBlocker);
        Assert.IsTrue(result.Value.IsClean);
    }

    [TestMethod]
    public async Task A_history_is_read_in_round_order_whatever_order_the_files_come_back_in()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteCleanRound(ArtifactKind.Spec, round: 10);
        workspace.WriteCleanRound(ArtifactKind.Spec, round: 2);
        workspace.WriteCleanRound(ArtifactKind.Spec, round: 1);

        Result<IReadOnlyList<FindingsRound>> history =
            await FindingsReader.ReadHistory(workspace.Cycle, ArtifactKind.Spec, CancellationToken.None);

        Assert.IsTrue(history.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { 1, 2, 10 },
            history.Value.Select(round => round.Round).ToArray(),
            "Ordering by file name would put r10 before r2.");
    }

    [TestMethod]
    public async Task One_artifact_history_does_not_pick_up_another_artifact_rounds()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteCleanRound(ArtifactKind.Spec);
        workspace.WriteCleanRound(ArtifactKind.Plan);
        workspace.WriteCleanRound(ArtifactKind.Plan, round: 2);

        Result<IReadOnlyList<FindingsRound>> history =
            await FindingsReader.ReadHistory(workspace.Cycle, ArtifactKind.Spec, CancellationToken.None);

        Assert.IsTrue(history.IsSuccess);
        Assert.AreEqual(1, history.Value.Count);
    }

    [TestMethod]
    public async Task A_cycle_with_no_findings_directory_has_an_empty_history_rather_than_an_error()
    {
        using var workspace = CycleWorkspace.Create();

        Result<IReadOnlyList<FindingsRound>> history =
            await FindingsReader.ReadHistory(workspace.Cycle, ArtifactKind.Intent, CancellationToken.None);

        Assert.IsTrue(history.IsSuccess);
        Assert.AreEqual(0, history.Value.Count);
    }

    [TestMethod]
    public void A_blocker_answered_in_a_later_round_is_no_longer_standing()
    {
        FindingsRound first = Round(1, new Finding("F_01", Severity.Blocking, Disposition.Open, "CON_06", "wrong."));
        FindingsRound second = Round(2, new Finding("F_01", Severity.Blocking, Disposition.Answered, "CON_06", "wrong."));

        Assert.AreEqual(0, FindingsReader.StandingBlockers([first, second]).Count);
    }

    [TestMethod]
    public void A_blocker_carried_unchanged_into_a_later_round_is_counted_once()
    {
        Finding carried = new("F_01", Severity.Blocking, Disposition.Open, "CON_06", "still wrong.");

        IReadOnlyList<Finding> standing = FindingsReader.StandingBlockers([Round(1, carried), Round(2, carried)]);

        Assert.AreEqual(
            1,
            standing.Count,
            "Reading the rounds as a flat list would count one objection twice.");
    }

    [TestMethod]
    public void A_blocker_only_the_first_round_mentioned_is_still_standing()
    {
        // A later round that says nothing about a finding has not answered it. Reading only the latest
        // round would lose it.
        FindingsRound first = Round(1, new Finding("F_01", Severity.Blocking, Disposition.Open, "CON_06", "wrong."));
        FindingsRound second = Round(2);

        Assert.AreEqual(1, FindingsReader.StandingBlockers([first, second]).Count);
    }

    [TestMethod]
    public void A_waived_blocker_is_not_standing()
    {
        FindingsRound round = Round(
            1,
            new Finding("F_01", Severity.Blocking, Disposition.Waived, "CON_06", "accepted at the gate."));

        Assert.AreEqual(0, FindingsReader.StandingBlockers([round]).Count);
    }

    private static FindingsRound Round(int number, params Finding[] findings) =>
        new(null, null, null, null, number, findings);
}
