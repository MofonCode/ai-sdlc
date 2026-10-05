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
                  "cites": { "id": "CON_04.1", "heading": "a spec covers every outcome its intent asks for" },
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
        Assert.AreEqual("CON_04.1", finding.Cites?.Id);
        Assert.AreEqual(
            "a spec covers every outcome its intent asks for",
            finding.Cites?.Heading,
            "The citation carries the rule's own line, which is what makes the finding readable "
            + "without resolving the id against a library that has since moved.");
        Assert.IsTrue(finding.IsOpenBlocker);

        // Through StandingBlockers, which is what the gate and the status panel both read. The round
        // used to answer this itself with an IsClean property, and nothing but this assertion ever
        // asked it -- so the suite was exercising a second definition of "open" that no caller used.
        Assert.AreEqual(1, FindingsReader.StandingBlockers([result.Value]).Count);
    }

    [TestMethod]
    public void An_empty_findings_array_is_a_clean_round()
    {
        Result<FindingsRound> result = FindingsReader.Read(
            """{ "round": 2, "findings": [] }""",
            "test");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, FindingsReader.StandingBlockers([result.Value]).Count);
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
    [DataRow("blocking", Severity.Blocking)]
    public void Each_severity_reads_back(string text, Severity expected)
    {
        // The citation is here because a blocking finding now requires one -- see
        // A_blocking_finding_with_no_citation_is_refused. A suggestion may carry one or not.
        Result<FindingsRound> result = FindingsReader.Read(
            $$"""{ "round": 1, "findings": [{ "id": "F_01", "severity": "{{text}}", "disposition": "open", "cites": { "id": "CON_04", "heading": "a spec covers every outcome its intent asks for" }, "statement": "something." }] }""",
            "test");

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.AreEqual(expected, result.Value.Findings.Single().Severity);
    }

    [TestMethod]
    public void A_blocking_finding_with_no_citation_is_refused()
    {
        // §7 says every blocking finding cites exactly one constraint and D-30 says the citation is
        // required on a blocker and optional on a suggestion. Nothing enforced it, so an uncited
        // blocker was accepted and the gate rendered the constraint column as whitespace -- leaving a
        // person at the gate unable to judge whether the finding was cheap or expensive, which is the
        // property §7.1 says the citation buys. Mechanically decidable, so §2.3 puts it here. F-11.
        Result<FindingsRound> result = FindingsReader.Read(
            """{ "round": 1, "findings": [{ "id": "F_09", "severity": "blocking", "disposition": "open", "statement": "no citation at all." }] }""",
            "spec.r1.json");

        Assert.IsTrue(result.IsFailed);

        string message = string.Join("; ", result.Errors.Select(error => error.Message));

        StringAssert.Contains(message, "F_09", "The error names the finding.");
        StringAssert.Contains(message, "spec.r1.json", "And the file.");
    }

    [TestMethod]
    [DataRow("CON_99", DisplayName = "a constraint number that does not exist still parses as an id")]
    [DataRow("CON_4", DisplayName = "unpadded")]
    [DataRow("con_04", DisplayName = "lower case")]
    [DataRow("CON-04", DisplayName = "wrong separator")]
    [DataRow("the one about tests", DisplayName = "prose")]
    [DataRow("   ", DisplayName = "whitespace")]
    public void A_blocking_citation_that_is_not_a_constraint_id_is_refused(string cites)
    {
        // Shape only. Whether the id names a constraint that is in force needs the constraint
        // documents, which this reader does not have and should not acquire.
        Result<FindingsRound> result = FindingsReader.Read(
            $$"""{ "round": 1, "findings": [{ "id": "F_02", "severity": "blocking", "disposition": "open", "cites": { "id": "{{cites}}", "heading": "a rule, stated in one line" }, "statement": "x." }] }""",
            "spec.r1.json");

        // CON_99 is a well-formed id for a constraint nobody wrote, so it passes the shape check and
        // is somebody else's problem. Everything else here is not an id at all.
        if (string.Equals(cites, "CON_99", StringComparison.Ordinal))
        {
            Assert.IsTrue(result.IsSuccess, "Well-formed, even if nothing answers to it.");
            return;
        }

        Assert.IsTrue(result.IsFailed, cites);
        StringAssert.Contains(string.Join("; ", result.Errors.Select(error => error.Message)), "F_02");
    }

    [TestMethod]
    public void A_suggestion_may_cite_nothing_and_still_does_not_block()
    {
        // What the third severity was for. A citation is required only on a blocker, so an uncited
        // non-blocking finding is a suggestion and needs no tier of its own.
        Result<FindingsRound> result = FindingsReader.Read(
            """{ "round": 1, "findings": [{ "id": "F_09", "severity": "suggestion", "disposition": "open", "statement": "this would read better as a table." }] }""",
            "test");

        Assert.IsTrue(result.IsSuccess);
        Finding finding = result.Value.Findings.Single();
        Assert.IsNull(finding.Cites);
        Assert.IsFalse(finding.IsOpenBlocker);
        Assert.AreEqual(
            0,
            FindingsReader.StandingBlockers([result.Value]).Count,
            "A suggestion is noticed and does not block.");
    }

    [TestMethod]
    public void There_are_two_severities_and_a_third_value_is_rejected()
    {
        // Fail loudly rather than defaulting. A severity the model does not have must not read as
        // 'suggestion' by accident, because the one that matters would read as suggestion too.
        Result<FindingsRound> result = FindingsReader.Read(
            """{ "round": 1, "findings": [{ "id": "F_01", "severity": "advice", "disposition": "open", "statement": "x." }] }""",
            "test");

        Assert.IsTrue(result.IsFailed);
        Assert.AreEqual(2, Enum.GetValues<Severity>().Length);
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
        FindingsRound first = Round(1, new Finding("F_01", Severity.Blocking, Disposition.Open, Cited, "wrong."));
        FindingsRound second = Round(2, new Finding("F_01", Severity.Blocking, Disposition.Answered, Cited, "wrong."));

        Assert.AreEqual(0, FindingsReader.StandingBlockers([first, second]).Count);
    }

    [TestMethod]
    public void A_blocker_carried_unchanged_into_a_later_round_is_counted_once()
    {
        Finding carried = new("F_01", Severity.Blocking, Disposition.Open, Cited, "still wrong.");

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
        FindingsRound first = Round(1, new Finding("F_01", Severity.Blocking, Disposition.Open, Cited, "wrong."));
        FindingsRound second = Round(2);

        Assert.AreEqual(1, FindingsReader.StandingBlockers([first, second]).Count);
    }

    [TestMethod]
    public void A_round_claiming_a_waived_disposition_is_rejected_rather_than_read_as_something_else()
    {
        // There is no waived disposition: a blocking finding is answered, withdrawn by a later round, or
        // open. A round carrying the old value has to fail loudly -- reading it as open would be right
        // by accident, and silently defaulting it would let a pre-rule round grant what it used to.
        const string json = """
            {
              "cycle": "20260929_1456_32",
              "artifact": "plan.md",
              "round": 1,
              "findings": [
                {
                  "id": "F_01",
                  "severity": "blocking",
                  "disposition": "waived",
                  "cites": { "id": "CON_06", "heading": "a plan's proof is adequate to the criterion it claims to prove" },
                  "statement": "the proof rests on a stub."
                }
              ]
            }
            """;

        Result<FindingsRound> read = FindingsReader.Read(json, "plan.r1.json");

        Assert.IsTrue(
            read.IsFailed,
            "A disposition the model does not have must not parse. Accepting it would make the hard "
            + "rule depend on which values a reader happens to recognise.");
    }

    [TestMethod]
    [DataRow("blocking", DisplayName = "on a blocker")]
    [DataRow("suggestion", DisplayName = "on a suggestion")]
    public void A_citation_with_no_heading_is_refused(string severity)
    {
        // The id alone is an index entry: it says a rule was breached without saying which, so reading
        // the finding means resolving the id somewhere else -- which is the thing nobody does and the
        // reason the heading is frozen into the round beside it. Checked at both severities because a
        // suggestion that cites is making the same claim on a rule and is read in the same listing.
        Result<FindingsRound> result = FindingsReader.Read(
            $$"""{ "round": 1, "findings": [{ "id": "F_04", "severity": "{{severity}}", "disposition": "open", "cites": { "id": "CON_06" }, "statement": "x." }] }""",
            "plan.r1.json");

        Assert.IsTrue(result.IsFailed, "A citation carries the rule's own line beside its id.");

        string message = string.Join("; ", result.Errors.Select(error => error.Message));

        StringAssert.Contains(message, "F_04", "The error names the finding.");
        StringAssert.Contains(message, "CON_06", "And the id it cited.");
        StringAssert.Contains(message, "plan.r1.json", "And the file.");
    }

    [TestMethod]
    public void A_citation_renders_as_the_rules_own_line_under_a_label()
    {
        // One format, defined beside the model, because five outputs show this line. The label is what
        // tells a reader which sentence is the rule and which is the objection -- two unlabelled
        // sentences in a row is the confusion this whole change exists to remove.
        Citation citation = new("CON_06", "a plan's proof is adequate to the criterion it claims to prove");

        Assert.AreEqual("rule    a plan's proof is adequate to the criterion it claims to prove", citation.Line());
        Assert.IsNull(
            new Citation("CON_06", null).Line(),
            "Nothing to show rather than a label with nothing after it.");
    }

    /// <summary>A citation to hang a finding on, where the test is not about the citation.</summary>
    private static Citation Cited { get; } = new("CON_06", "a plan's proof is adequate to the criterion it claims to prove");

    private static FindingsRound Round(int number, params Finding[] findings) =>
        new(null, null, null, null, null, number, findings);
}
