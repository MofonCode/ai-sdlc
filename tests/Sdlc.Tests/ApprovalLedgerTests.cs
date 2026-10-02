using FluentResults;
using Sdlc.Core.Approvals;

namespace Sdlc.Tests;

[TestClass]
public sealed class ApprovalLedgerTests
{
    private const string OneApproval = """
        ---
        cycle: 20260929_1456_32
        framework_version: 1.0.0
        decisions:
          - decision: approved
            by: Peter Almanzar
            at: 2026-10-01T12:00:00Z
            hashes:
              intent.md: aaaa
              spec.md:   bbbb
              plan.md:   cccc
            findings_rounds:
              intent.md: 2
              spec.md:   3
              plan.md:   1
        ---
        """;

    [TestMethod]
    public void An_approval_reads_back_with_everything_it_recorded()
    {
        ApprovalLedger ledger = Read(OneApproval);

        GateEntry entry = ledger.Decisions.Single();
        Assert.AreEqual(GateDecision.Approved, entry.Decision);
        Assert.AreEqual("Peter Almanzar", entry.By);
        Assert.AreEqual(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero), entry.At);
        Assert.AreEqual("bbbb", entry.Hashes["spec.md"]);
        Assert.AreEqual(3, entry.FindingsRounds["spec.md"]);
        Assert.IsTrue(ledger.IsApproved);
    }

    [TestMethod]
    public void A_reopen_supersedes_an_approval_and_returns_the_cycle_to_unapproved()
    {
        const string reopened = """
            ---
            cycle: 20260929_1456_32
            decisions:
              - decision: approved
                by: Peter Almanzar
                at: 2026-10-01T12:00:00Z
              - decision: reopened
                by: Peter Almanzar
                at: 2026-10-02T09:30:00Z
                reason: the plan proves criterion 3 against a stub, so the proof reaches nothing
            ---
            """;

        ApprovalLedger ledger = Read(reopened);

        Assert.AreEqual(GateDecision.Reopened, ledger.Current!.Decision);
        Assert.IsFalse(
            ledger.IsApproved,
            "A reopen that left the cycle approved would do nothing. Turning IsApproved false is the "
            + "entire mechanism: the gate refuses the build again and the planning documents can be "
            + "rewritten.");
    }

    [TestMethod]
    public void A_reopen_with_no_reason_is_rejected_like_any_other_refusal()
    {
        const string silent = """
            ---
            cycle: 20260929_1456_32
            decisions:
              - decision: reopened
                by: Peter Almanzar
                at: 2026-10-02T09:30:00Z
            ---
            """;

        Result<ApprovalLedger> read = ApprovalLedger.Read(silent, "approval.md");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(
            read.Errors[0].Message,
            "why",
            "Withdrawing an approval is the one decision whose reason the author most needs, because "
            + "it arrives after the work was agreed.");
    }

    [TestMethod]
    public void An_accepted_findings_key_left_over_from_an_older_ledger_grants_nothing()
    {
        // The reader ignores unmatched properties, so an old file still parses. What matters is that
        // the key confers nothing: a blocking finding cannot be waived by any mechanism, and a ledger
        // written before that rule must not keep working as if it could.
        const string legacy = """
            ---
            cycle: 20260929_1456_32
            decisions:
              - decision: approved
                by: Peter Almanzar
                at: 2026-10-01T12:00:00Z
                accepted_findings: [plan.F_02]
            ---
            """;

        ApprovalLedger ledger = Read(legacy);

        Assert.IsTrue(ledger.IsApproved);
        Assert.AreEqual(
            1,
            ledger.Decisions.Count,
            "The entry still reads. Nothing on GateEntry records what it listed, so the gate cannot "
            + "consult it, which is what makes the rule absolute rather than grandfathered.");
    }

    [TestMethod]
    public void The_last_entry_is_the_one_that_stands_so_a_later_approval_supersedes_a_refusal()
    {
        const string both = """
            ---
            cycle: 20260929_1456_32
            decisions:
              - decision: changes_requested
                by: Peter Almanzar
                at: 2026-10-01T11:00:00Z
                reason: the spec has no criterion for delivery
              - decision: approved
                by: Peter Almanzar
                at: 2026-10-01T12:00:00Z
                hashes:
                  intent.md: aaaa
            ---
            """;

        ApprovalLedger ledger = Read(both);

        Assert.AreEqual(2, ledger.Decisions.Count, "The file is append-only, so both entries survive.");
        Assert.AreEqual(GateDecision.Approved, ledger.Current?.Decision);
        Assert.IsTrue(ledger.IsApproved);
    }

    [TestMethod]
    public void A_refusal_reads_back_with_its_reason()
    {
        const string denied = """
            ---
            cycle: 20260929_1456_32
            decisions:
              - decision: denied
                by: Peter Almanzar
                at: 2026-10-01T12:00:00Z
                reason: the approach needs the schema change that is in flight elsewhere
            ---
            """;

        ApprovalLedger ledger = Read(denied);

        Assert.AreEqual(GateDecision.Denied, ledger.Current?.Decision);
        StringAssert.Contains(ledger.Current!.Reason!, "schema change");
        Assert.IsFalse(ledger.IsApproved);
    }

    [TestMethod]
    public void A_refusal_with_no_reason_is_rejected_because_the_reason_is_the_whole_of_it()
    {
        const string silent = """
            ---
            cycle: 20260929_1456_32
            decisions:
              - decision: denied
                by: Peter Almanzar
                at: 2026-10-01T12:00:00Z
            ---
            """;

        Result<ApprovalLedger> result = ApprovalLedger.Read(silent, "test");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("why", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void An_entry_naming_nobody_is_rejected()
    {
        Result<ApprovalLedger> result = ApprovalLedger.Read(
            OneApproval.Replace("    by: Peter Almanzar\n", string.Empty, StringComparison.Ordinal),
            "test");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("names nobody", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void An_entry_with_no_timestamp_is_rejected_rather_than_taking_one_from_the_file()
    {
        // git does not store mtimes, so a clone sets every file's to the moment of checkout. Falling
        // back to it would make every approval read as granted just now on a teammate's machine.
        Result<ApprovalLedger> result = ApprovalLedger.Read(
            OneApproval.Replace("    at: 2026-10-01T12:00:00Z\n", string.Empty, StringComparison.Ordinal),
            "test");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("timestamp", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void A_decision_nobody_recognises_is_rejected_and_the_valid_ones_are_named()
    {
        Result<ApprovalLedger> result = ApprovalLedger.Read(
            OneApproval.Replace("decision: approved", "decision: looks_fine", StringComparison.Ordinal),
            "test");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("changes_requested", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void A_file_with_no_decisions_approves_nothing()
    {
        Result<ApprovalLedger> result = ApprovalLedger.Read(
            "---\ncycle: 20260929_1456_32\ndecisions:\n---\n",
            "test");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("approves nothing", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void A_file_with_no_frontmatter_is_rejected_with_the_fence_named()
    {
        Result<ApprovalLedger> result = ApprovalLedger.Read("# Just prose\n", "test");

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("---", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("changes_requested")]
    [DataRow("changesRequested")]
    [DataRow("CHANGES_REQUESTED")]
    public void A_decision_is_read_whichever_way_a_person_spelled_it(string spelling)
    {
        string text = $"""
            ---
            cycle: 20260929_1456_32
            decisions:
              - decision: {spelling}
                by: Peter Almanzar
                at: 2026-10-01T12:00:00Z
                reason: needs more thought
            ---
            """;

        Assert.AreEqual(GateDecision.ChangesRequested, Read(text).Current?.Decision);
    }

    private static ApprovalLedger Read(string text)
    {
        Result<ApprovalLedger> result = ApprovalLedger.Read(text, "test");

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
