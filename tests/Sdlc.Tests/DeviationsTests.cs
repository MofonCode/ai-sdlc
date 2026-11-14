using System.Globalization;
using FluentResults;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Deviations;
using Sdlc.Core.Findings;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// Deviations: where a build or a remediation bent the approved documents, and a person's answer to each.
/// </summary>
[TestClass]
public sealed class DeviationsTests
{
    // Every fixture document's sections hold the word "content", so it is approved text a deviation can quote.
    private const string Quoted = "content";

    [TestMethod]
    public void A_deviation_is_read_with_everything_it_says()
    {
        Result<IReadOnlyList<Deviation>> read = DeviationsFile.Parse(
            Entry("DEV_01", files: "Files: `src/StreamSocket.cs`"),
            DeviationsFile.FileName);

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(error => error.Message)));
        Deviation deviation = read.Value.Single();
        Assert.AreEqual("DEV_01", deviation.Id);
        Assert.AreEqual("The socket file is changed to fix a race", deviation.Gist);
        Assert.AreEqual(DeviationKind.Plan, deviation.Kind);
        Assert.AreEqual("plan.md", deviation.Document);
        Assert.AreEqual("Files", deviation.Section);
        Assert.AreEqual(Quoted, deviation.Quote);
        Assert.AreEqual("src/StreamSocket.cs", deviation.Files.Single());
        StringAssert.Contains(deviation.Blocker, "ObjectDisposedException");
        Assert.AreEqual(64, deviation.Sha256.Length);
    }

    [TestMethod]
    public void A_deviation_missing_what_makes_it_one_is_refused_with_every_problem_at_once()
    {
        Result<IReadOnlyList<Deviation>> read = DeviationsFile.Parse(
            "## DEV_01 — Something changed\n\nKind: plan\nDeparts from: plan.md, Files\n",
            DeviationsFile.FileName);

        Assert.IsTrue(read.IsFailed);
        string all = string.Join("\n", read.Errors.Select(error => error.Message));
        StringAssert.Contains(all, "quotes nothing");
        StringAssert.Contains(all, "'Blocker:'");
        StringAssert.Contains(all, "'Deviation:'");
        StringAssert.Contains(all, "'Why:'");
    }

    [TestMethod]
    [DataRow("criterion", "plan.md")]
    [DataRow("plan", "spec.md")]
    [DataRow("plan", "intent.md")]
    public void A_kind_quotes_only_its_own_document(string kind, string document)
    {
        Result<IReadOnlyList<Deviation>> read = DeviationsFile.Parse(
            Entry("DEV_01", kind: kind, document: document, successor: "Successor: later — the rest"),
            DeviationsFile.FileName);

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "the intent is never deviated from");
    }

    [TestMethod]
    public void A_narrowed_criterion_names_the_successor_the_dropped_part_becomes()
    {
        Result<IReadOnlyList<Deviation>> read = DeviationsFile.Parse(
            Entry("DEV_01", kind: "criterion", document: "spec.md"),
            DeviationsFile.FileName);

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "names no successor");
    }

    [TestMethod]
    public void An_id_is_never_used_twice()
    {
        Result<IReadOnlyList<Deviation>> read = DeviationsFile.Parse(
            Entry("DEV_01") + "\n" + Entry("DEV_01"),
            DeviationsFile.FileName);

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "Replaces: DEV_01");
    }

    [TestMethod]
    public void The_rules_behind_a_deviation_are_the_ids_its_blocker_names()
    {
        Deviation deviation = DeviationsFile.Parse(
            Entry("DEV_01", blocker: "diff.F_02 under CON_07.2, and DEC_04 set this aside"),
            DeviationsFile.FileName).Value.Single();

        CollectionAssert.AreEqual(new[] { "CON_07.2", "DEC_04" }, DeviationsFile.RulesOf(deviation).ToArray());
    }

    [TestMethod]
    public void A_deviations_entry_sits_beside_the_approval_and_never_stands_in_its_place()
    {
        Result<ApprovalLedger> read = ApprovalLedger.Read(
            """
            ---
            cycle: 20260929_1456_32
            decisions:
              - decision: approved
                by: Peter Almanzar
                at: 2026-10-01T12:00:00Z
              - decision: deviations
                by: Peter Almanzar
                at: 2026-10-02T12:00:00Z
                deviations:
                  - id: DEV_01
                    gist: "Changed: a colon"
                    kind: plan
                    sha256: abc
                    accepted: true
                    note: ""
                  - id: DEV_02
                    sha256: def
                    accepted: false
                    note: "Fix the fixture instead."
            ---
            """,
            "approval.md");

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(error => error.Message)));
        Assert.IsTrue(read.Value.IsApproved, "A deviations entry answers for the approval; it does not withdraw it.");
        Assert.AreEqual(GateDecision.Approved, read.Value.Current!.Decision);
        Assert.AreEqual(0, read.Value.StandingIndex);

        IReadOnlyList<DeviationAnswer> answers = read.Value.Decisions[1].Answers!;
        Assert.IsTrue(answers[0].Accepted);
        Assert.IsTrue(answers[1].Declined);
        Assert.AreEqual("Fix the fixture instead.", answers[1].Note);
    }

    [TestMethod]
    public void A_deviations_entry_answers_with_each_id_and_hash()
    {
        Result<ApprovalLedger> read = ApprovalLedger.Read(
            """
            ---
            cycle: 20260929_1456_32
            decisions:
              - decision: deviations
                by: Peter Almanzar
                at: 2026-10-02T12:00:00Z
                deviations:
                  - id: DEV_01
                    accepted: true
            ---
            """,
            "approval.md");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "without its id and hash");
    }

    [TestMethod]
    public async Task A_deviation_nobody_answered_is_pending_and_an_accepted_one_holds_until_its_text_moves()
    {
        using CycleWorkspace workspace = Approved();
        WriteDeviations(workspace, Entry("DEV_01"));

        DeviationsState state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);
        Assert.AreEqual(DeviationStanding.Pending, state.States.Single().Standing);
        Assert.AreEqual(0, state.Problems.Count, string.Join("; ", state.Problems));

        Answer(workspace, ("DEV_01", state.States[0].Deviation.Sha256, true, null));
        state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);
        Assert.AreEqual(DeviationStanding.Accepted, state.States.Single().Standing);

        WriteDeviations(workspace, Entry("DEV_01", instead: "Something else entirely."));
        state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);
        Assert.AreEqual(DeviationStanding.Pending, state.States.Single().Standing);
        Assert.IsTrue(state.States.Single().Changed);
    }

    [TestMethod]
    public async Task False_with_a_note_declines_and_false_with_none_is_not_reviewed()
    {
        using CycleWorkspace workspace = Approved();
        WriteDeviations(workspace, Entry("DEV_01") + "\n" + Entry("DEV_02"));
        DeviationsState state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);

        Answer(
            workspace,
            ("DEV_01", state.States[0].Deviation.Sha256, false, "Fix the fixture instead."),
            ("DEV_02", state.States[1].Deviation.Sha256, false, null));

        state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);
        Assert.AreEqual(DeviationStanding.Declined, state.States[0].Standing);
        Assert.AreEqual("Fix the fixture instead.", state.States[0].Note);
        Assert.AreEqual(DeviationStanding.Pending, state.States[1].Standing);
    }

    [TestMethod]
    public async Task Under_approval_none_every_deviation_is_accepted_and_the_pull_request_is_the_review()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteSolution(SolutionOrigin.Adopted, approval: "none");
        WriteDeviations(workspace, Entry("DEV_01"));

        DeviationsState state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);

        Assert.IsTrue(state.AcceptedBySetting);
        Assert.AreEqual(DeviationStanding.Accepted, state.States.Single().Standing);
        Assert.AreEqual(0, state.Pending.Count);
    }

    [TestMethod]
    public async Task A_deviation_answered_before_a_reopen_is_history_and_is_not_checked_again()
    {
        using CycleWorkspace workspace = Approved();
        WriteDeviations(workspace, Entry("DEV_01", quote: "text a reopen took out"));
        string sha = (await CycleDeviations.Read(workspace.Cycle, CancellationToken.None)).States[0].Deviation.Sha256;

        Answer(workspace, ("DEV_01", sha, false, "Rethink the plan."));
        Append(workspace, "  - decision: reopened\n    by: Peter Almanzar\n    at: 2026-10-03T12:00:00Z\n    reason: The plan is wrong.\n");
        Append(workspace, "  - decision: approved\n    by: Peter Almanzar\n    at: 2026-10-04T12:00:00Z\n");

        DeviationsState state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);

        Assert.AreEqual(DeviationStanding.History, state.States.Single().Standing);
        Assert.AreEqual(0, state.Current.Count);
        Assert.AreEqual(0, state.Problems.Count, "History quotes a plan that has since been amended, and is not held to it.");
    }

    [TestMethod]
    public async Task A_quote_the_approved_text_does_not_hold_is_a_problem()
    {
        using CycleWorkspace workspace = Approved();
        WriteDeviations(workspace, Entry("DEV_01", quote: "words nobody approved"));

        DeviationsState state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);

        StringAssert.Contains(state.Problems.Single(), "does not hold");
    }

    [TestMethod]
    public async Task A_file_another_open_cycle_claims_is_a_conflict_and_not_a_deviation()
    {
        using CycleWorkspace workspace = Approved();
        Assert.IsTrue(CycleId.TryParse("20260930_0900_00", out CycleId? id));
        Cycle other = new(id.Value, workspace.Root);
        CycleWorkspace.WriteRaw(other.PathOf(ArtifactKind.Plan), "# plan\n\n## Files\n\n| File | Change |\n|---|---|\n| `src/StreamSocket.cs` | new |\n");
        WriteDeviations(workspace, Entry("DEV_01", files: "Files: `src/StreamSocket.cs`"));

        DeviationsState state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);

        StringAssert.Contains(state.Problems.Single(), "cycle 20260930_0900_00 has claimed");
    }

    [TestMethod]
    public async Task An_accepted_narrowing_needs_its_successor_written()
    {
        using CycleWorkspace workspace = Approved();
        WriteDeviations(workspace, Entry("DEV_01", kind: "criterion", document: "spec.md", successor: "Successor: server-host — the rest of criterion 7"));
        string sha = (await CycleDeviations.Read(workspace.Cycle, CancellationToken.None)).States[0].Deviation.Sha256;

        Answer(workspace, ("DEV_01", sha, true, null));
        DeviationsState state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);
        StringAssert.Contains(state.Problems.Single(), ".sdlc/successors/server-host.md");

        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", "successors", "server-host.md"), "x");
        state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);
        Assert.AreEqual(0, state.Problems.Count);
    }

    [TestMethod]
    public async Task A_third_deviation_bending_the_same_text_escalates_a_verification_that_still_blocks()
    {
        using CycleWorkspace workspace = Approved();
        WriteDeviations(workspace, Entry("DEV_01") + "\n" + Entry("DEV_02"));
        workspace.WriteBlockingRound(ArtifactKind.Verify, round: 1, id: "F_01", cites: "CON_07.2");

        Result<RemediationState> two = await RemediationBudget.Read(workspace.Cycle, CancellationToken.None);
        Assert.IsFalse(two.Value.Circling, "Two is coincidence.");

        WriteDeviations(workspace, Entry("DEV_01") + "\n" + Entry("DEV_02") + "\n" + Entry("DEV_03"));
        Result<RemediationState> three = await RemediationBudget.Read(workspace.Cycle, CancellationToken.None);

        Assert.IsTrue(three.Value.Circling);
        Assert.IsTrue(three.Value.Escalated);
        StringAssert.Contains(RemediationBudget.Describe(three.Value), "DEV_01, DEV_02, DEV_03 bend the same text in plan.md");
    }

    [TestMethod]
    public async Task An_approved_cycle_with_unanswered_deviations_drafts_an_answer_to_each_with_the_same_command()
    {
        using CycleWorkspace workspace = Approved();
        WriteDeviations(workspace, Entry("DEV_01") + "\n" + Entry("DEV_02"));

        ApprovalText.Response text = await Draft(workspace);

        Assert.IsTrue(text.MayApprove, text.Instructions);
        Assert.AreEqual(GateDecision.Deviations, text.Decision);
        StringAssert.Contains(text.Entry, "decision: deviations");
        StringAssert.Contains(text.Entry, "- id: DEV_02");
        StringAssert.Contains(text.Entry, "accepted: false");
        StringAssert.Contains(text.Entry, "note: \"\"");
        StringAssert.EndsWith(text.DraftPath, "approval.deviations.draft.md");
        StringAssert.Contains(text.Instructions, "set 'accepted: true' to accept it");

        // The draft, renamed as it stands, is a ledger the gate still approves and every deviation unanswered.
        await File.WriteAllTextAsync(workspace.Cycle.ApprovalPath, text.FileText);
        Result<ApprovalLedger> placed = ApprovalLedger.Read(text.FileText!, "approval.md");
        Assert.IsTrue(placed.IsSuccess, string.Join("; ", placed.Errors.Select(error => error.Message)));
        Assert.IsTrue(placed.Value.IsApproved);
        Assert.AreEqual(2, (await CycleDeviations.Read(workspace.Cycle, CancellationToken.None)).Pending.Count);
    }

    [TestMethod]
    public async Task Answers_given_on_the_command_line_are_drafted_and_the_ledger_takes_them()
    {
        // D-162. The person said which to accept, and the agent's hand edit of the draft is what went wrong.
        using CycleWorkspace workspace = Approved();
        WriteDeviations(workspace, Entry("DEV_01") + "\n" + Entry("DEV_02") + "\n" + Entry("DEV_03"));

        Assert.IsTrue((await Answering(workspace, new(["DEV_01"], ["DEV_01"], "no"))).IsFailed, "One deviation, one answer.");
        StringAssert.Contains((await Answering(workspace, new(["DEV_09"], [], null))).Errors[0].Message, "DEV_01, DEV_02, DEV_03");
        StringAssert.Contains((await Answering(workspace, new([], ["DEV_02"], " "))).Errors[0].Message, "--note");

        ApprovalText.Response text = (await Answering(workspace, new(["DEV_01"], ["DEV_02"], "Keep the retry: \"why\" matters."))).Value;

        await File.WriteAllTextAsync(workspace.Cycle.ApprovalPath, text.FileText);
        Assert.IsTrue(ApprovalLedger.Read(text.FileText!, "approval.md").IsSuccess);
        DeviationsState state = await CycleDeviations.Read(workspace.Cycle, CancellationToken.None);
        Assert.AreEqual("DEV_03", state.Pending.Single().Deviation.Id, "Accepted and declined are answered; the unnamed one is not.");
    }

    [TestMethod]
    public async Task A_reopen_waits_until_every_deviation_is_answered()
    {
        using CycleWorkspace workspace = Approved();
        WriteDeviations(workspace, Entry("DEV_01"));

        ApprovalText.Response text = await Draft(workspace, GateDecision.Reopened);

        Assert.IsFalse(text.MayApprove);
        StringAssert.Contains(text.Instructions, "cannot be reopened yet: 1 deviation is unanswered");
    }

    [TestMethod]
    public async Task The_gate_still_lets_the_build_run_once_deviations_are_answered()
    {
        using CycleWorkspace workspace = Approved();
        WriteDeviations(workspace, Entry("DEV_01"));
        string sha = (await CycleDeviations.Read(workspace.Cycle, CancellationToken.None)).States[0].Deviation.Sha256;
        Answer(workspace, ("DEV_01", sha, true, null));

        using var gate = Gate.For(workspace.Root);
        Result<ApprovalGate.Response> verdict = await gate.Send<ApprovalGate.Query, ApprovalGate.Response>(new ApprovalGate.Query(workspace.Cycle.Id));

        Assert.IsTrue(verdict.IsSuccess);
        Assert.IsTrue(verdict.Value.MayBuild, verdict.Value.Explanation);
    }

    [TestMethod]
    public async Task Every_verify_reader_is_handed_the_deviations_with_where_each_stands()
    {
        using CycleWorkspace workspace = Approved();
        workspace.WriteArtifact(ArtifactKind.Verify);
        WriteDeviations(workspace, Entry("DEV_01"));
        string directory = Path.Combine(workspace.Root, "prompts");

        Result<ReaderPrompts.Response> written = await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Verify, directory);

        Assert.IsTrue(written.IsSuccess, string.Join("; ", written.Errors.Select(error => error.Message)));

        foreach (string prompt in Directory.GetFiles(directory, "*.prompt.md").Where(path => !path.EndsWith("reconcile.prompt.md", StringComparison.Ordinal)))
        {
            string text = await File.ReadAllTextAsync(prompt);
            StringAssert.Contains(text, "DEV_01 — The socket file is changed to fix a race (plan) -- in force, awaiting the person's answer", Path.GetFileName(prompt));
            StringAssert.Contains(text, "<file name=\"deviations.md\">", Path.GetFileName(prompt));
        }
    }

    [TestMethod]
    public async Task A_cycle_that_followed_its_plan_tells_its_readers_so()
    {
        using CycleWorkspace workspace = Approved();
        workspace.WriteArtifact(ArtifactKind.Verify);

        string shared = await ReaderPrompts.Shared(workspace.Cycle, ArtifactKind.Verify, withDocument: true, CancellationToken.None);

        StringAssert.Contains(shared, "None. The build recorded no deviation");
    }

    private static string Entry(
        string id,
        string kind = "plan",
        string document = "plan.md",
        string quote = Quoted,
        string blocker = "Credential_never_reaches_a_trader fails 2 runs in 3 with ObjectDisposedException",
        string instead = "StreamSocket.cs is also changed.",
        string? files = null,
        string? successor = null) => string.Create(
        CultureInfo.InvariantCulture,
        $"""
        ## {id} — The socket file is changed to fix a race

        Kind: {kind}
        Departs from: {document}, Files
        > {quote}
        Blocker: {blocker}
        Deviation: {instead}
        Why: the race is in StreamSocket.
        {files ?? string.Empty}
        {successor ?? string.Empty}

        """);

    private static CycleWorkspace Approved()
    {
        var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        return workspace;
    }

    private static void WriteDeviations(CycleWorkspace workspace, string text) =>
        CycleWorkspace.WriteRaw(DeviationsFile.PathOf(workspace.Cycle), "# Deviations\n\n" + text);

    private static void Answer(CycleWorkspace workspace, params (string Id, string Sha, bool Accepted, string? Note)[] answers) => Append(
        workspace,
        "  - decision: deviations\n    by: Peter Almanzar\n    at: 2026-10-02T12:00:00Z\n    deviations:\n"
        + string.Concat(answers.Select(answer => string.Create(
            CultureInfo.InvariantCulture,
            $"      - id: {answer.Id}\n        sha256: {answer.Sha}\n        accepted: {(answer.Accepted ? "true" : "false")}\n        note: \"{answer.Note}\"\n"))));

    // Appends an entry to the ledger, before its closing fence.
    private static void Append(CycleWorkspace workspace, string entry)
    {
        string text = File.ReadAllText(workspace.Cycle.ApprovalPath).TrimEnd();
        File.WriteAllText(workspace.Cycle.ApprovalPath, text[..^3] + entry + "---\n");
    }

    private static async Task<ApprovalText.Response> Draft(CycleWorkspace workspace, GateDecision decision = GateDecision.Approved)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ApprovalText.Response> result = await gate.Send<ApprovalText.Query, ApprovalText.Response>(
            new ApprovalText.Query(workspace.Cycle.Id, "Peter Almanzar", decision));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task<Result<ApprovalText.Response>> Answering(CycleWorkspace workspace, ApprovalText.DeviationAnswers answers)
    {
        using var gate = Gate.For(workspace.Root);
        return await gate.Send<ApprovalText.Query, ApprovalText.Response>(
            new ApprovalText.Query(workspace.Cycle.Id, "Peter Almanzar", GateDecision.Approved, answers));
    }
}
