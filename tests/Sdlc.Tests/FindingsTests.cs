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
    public void A_finding_names_the_perspective_that_raised_it_and_only_a_real_one()
    {
        // Recurrence is counted by perspective name, so a misspelt one would be a perspective of its own
        // with a count of one, and the real one would read as recurring less than it did.
        Result<FindingsRound> named = FindingsReader.Read(
            """{ "round": 1, "findings": [{ "id": "security.F_01", "severity": "suggestion", "disposition": "open", "perspective": "security", "statement": "something." }] }""",
            "test");

        Assert.IsTrue(named.IsSuccess, string.Join("; ", named.Errors.Select(e => e.Message)));
        Assert.AreEqual("security", named.Value.Findings.Single().Perspective);

        Result<FindingsRound> misspelt = FindingsReader.Read(
            """{ "round": 1, "findings": [{ "id": "F_01", "severity": "suggestion", "disposition": "open", "perspective": "secruity", "statement": "something." }] }""",
            "spec.r1.json");

        Assert.IsTrue(misspelt.IsFailed);
        Assert.IsTrue(misspelt.Errors.Any(e => e.Message.Contains("'secruity'", StringComparison.Ordinal)
            && e.Message.Contains("maintainability", StringComparison.Ordinal)));
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

    [TestMethod]
    public void A_conflict_reads_back_naming_both_rules()
    {
        // The loop's third ending, which section 6 has promised since iteration 3 and nothing recorded.
        // Not a finding: a finding says this document is wrong, and this says no document is right.
        Result<FindingsRound> result = FindingsReader.Read(
            """
            {
              "round": 2,
              "conflicts": [
                {
                  "between": [
                    { "id": "CON_05", "heading": "a spec introduces no scope its intent did not ask for" },
                    { "id": "CON_16", "heading": "a spec covers every outcome its intent asks for, and all of each one" }
                  ],
                  "statement": "covering the visual half needs criteria the intent does not support; narrowing leaves the outcome half covered.",
                  "locus": "## Acceptance criteria"
                }
              ],
              "findings": []
            }
            """,
            "spec.r2.json");

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));

        Conflict conflict = result.Value.Conflicts!.Single();

        Assert.AreEqual(2, conflict.Between!.Count);
        Assert.AreEqual("CON_05 and CON_16", conflict.Ids());
        Assert.AreEqual("## Acceptance criteria", conflict.Locus);
        StringAssert.Contains(conflict.Statement!, "narrowing leaves the outcome half covered");
    }

    [TestMethod]
    public void A_round_that_names_no_conflicts_has_none_rather_than_null_trouble()
    {
        // Absent and empty mean the same thing for conflicts, unlike findings. A reader that found no
        // conflict is the normal case, so the key is omitted rather than written empty on every round.
        Result<FindingsRound> result = FindingsReader.Read(
            """{ "round": 1, "findings": [] }""",
            "spec.r1.json");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.Value.Conflicts);
        Assert.AreEqual(
            0,
            FindingsReader.StandingConflicts([result.Value]).Count,
            "The fold answers 'none' rather than making every caller handle a null.");
    }

    [TestMethod]
    [DataRow("""[{ "id": "CON_05", "heading": "x" }]""", DisplayName = "one rule")]
    [DataRow("[]", DisplayName = "no rules")]
    public void A_conflict_that_does_not_name_two_rules_is_refused(string between)
    {
        // One rule cannot conflict with itself, and a single-entry conflict is a finding wearing the
        // wrong shape -- which is exactly the confusion the distinction exists to prevent.
        Result<FindingsRound> result = FindingsReader.Read(
            $$"""{ "round": 1, "findings": [], "conflicts": [{ "between": {{between}}, "statement": "x." }] }""",
            "spec.r1.json");

        Assert.IsTrue(result.IsFailed);

        string message = string.Join("; ", result.Errors.Select(error => error.Message));

        StringAssert.Contains(message, "two rules or more");
        StringAssert.Contains(message, "spec.r1.json", "The error names the file.");
    }

    [TestMethod]
    public void A_conflict_whose_rule_carries_no_heading_is_refused()
    {
        // The same rule as a finding's citation, and for a sharper reason: a person reading this has to
        // weigh two rules against each other, which they cannot do from two numbers.
        Result<FindingsRound> result = FindingsReader.Read(
            """
            { "round": 1, "findings": [], "conflicts": [{ "between": [
                { "id": "CON_05", "heading": "a spec introduces no scope its intent did not ask for" },
                { "id": "CON_16" } ], "statement": "x." }] }
            """,
            "spec.r1.json");

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(string.Join("; ", result.Errors.Select(e => e.Message)), "CON_16");
    }

    [TestMethod]
    public void A_conflict_with_no_statement_is_refused()
    {
        // Unarguable otherwise. CON_10.2 refuses a gate reason that says nothing, and a conflict that
        // says nothing leaves a person the two ids and no account of why they cannot both hold.
        Result<FindingsRound> result = FindingsReader.Read(
            """
            { "round": 1, "findings": [], "conflicts": [{ "between": [
                { "id": "CON_05", "heading": "a spec introduces no scope its intent did not ask for" },
                { "id": "CON_16", "heading": "a spec covers every outcome, and all of each one" } ] }] }
            """,
            "spec.r1.json");

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(string.Join("; ", result.Errors.Select(e => e.Message)), "says nothing about why");
    }

    [TestMethod]
    public void A_conflict_may_name_more_than_two_rules()
    {
        // Three mutually unsatisfiable rules is conceivable and nobody has seen one. Permitting it costs
        // nothing in the model, where capping it at two would be a limit with no argument behind it.
        Result<FindingsRound> result = FindingsReader.Read(
            """
            { "round": 1, "findings": [], "conflicts": [{ "between": [
                { "id": "CON_05", "heading": "one" },
                { "id": "CON_16", "heading": "two" },
                { "id": "CON_12", "heading": "three" } ], "statement": "none of the three can all hold." }] }
            """,
            "spec.r1.json");

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.AreEqual("CON_05 and CON_16 and CON_12", result.Value.Conflicts!.Single().Ids());
    }

    [TestMethod]
    public void Only_the_latest_rounds_conflicts_stand()
    {
        // A conflict has no id and no disposition, so it is settled the way everything else here is --
        // by the next fresh reader's silence (D-49). One declared in round two and gone in round three is
        // one the refiner found an exit for. Accumulating them would report a resolved conflict forever.
        Conflict declared = new(
            [new Citation("CON_05", "a spec introduces no scope its intent did not ask for"), new Citation("CON_16", "a spec covers every outcome its intent asks for, and all of each one")],
            "no wording satisfies both.");

        IReadOnlyList<FindingsRound> rounds = [Conflicting(2, declared), Round(3)];

        Assert.AreEqual(
            0,
            FindingsReader.StandingConflicts(rounds).Count,
            "Round three said nothing about it, which is how a conflict is resolved.");

        Assert.AreEqual(
            1,
            FindingsReader.StandingConflicts([Round(1), Conflicting(2, declared)]).Count,
            "And it stands while the latest round is the one that declared it.");
    }

    /// <summary>A citation to hang a finding on, where the test is not about the citation.</summary>
    private static Citation Cited { get; } = new("CON_06", "a plan's proof is adequate to the criterion it claims to prove");

    private static FindingsRound Round(int number, params Finding[] findings) =>
        new(null, null, null, null, null, number, findings);

    private static FindingsRound Conflicting(int number, params Conflict[] conflicts) =>
        new(null, null, null, null, null, number, [], conflicts);
}
