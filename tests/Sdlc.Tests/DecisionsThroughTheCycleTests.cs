using System.Globalization;
using FluentResults;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Findings;
using Sdlc.Core.Proposals;

namespace Sdlc.Tests;

/// <summary>
/// D-139: a solution's decisions are standing criteria every stage reads and every reader is handed, a
/// departure is declared once where it arises and reported to the person, and the verify round's diff
/// reader holds the change itself to the plan and the decisions.
/// </summary>
[TestClass]
public sealed class DecisionsThroughTheCycleTests
{
    private const string Delayed = "market data is the 15-minute-delayed feed";

    [TestMethod]
    public void A_departure_is_read_from_the_section_that_declares_it_with_its_continuation()
    {
        string intent = """
            ## Fixed decisions

            - Keep the page on one origin. Peter, 2026-11-01: one cookie scope.
            - Departs from DEC_08 (market data is the delayed feed): the real-time feed for the
              watchlist. Peter, 2026-11-07: the trial account now includes it.

            ## Must not change
            """;

        Departure departure = Departures.In(ArtifactKind.Intent, intent).Single();

        Assert.AreEqual(8, departure.Decision.Number);
        Assert.AreEqual(ArtifactKind.Intent, departure.Where);
        StringAssert.Contains(departure.Text, "the trial account now includes it", "A wrapped line is one departure.");
    }

    [TestMethod]
    public void A_spec_declares_no_departure_of_its_own()
    {
        // The intent is where an outcome is settled with a person; a spec criterion rests on that.
        Assert.AreEqual(0, Departures.In(ArtifactKind.Spec, "## Fixed decisions\n\n- Departs from DEC_08 (x): y.\n").Count);
    }

    [TestMethod]
    public void A_departure_naming_a_decision_the_record_does_not_hold_is_a_shape_problem()
    {
        string plan = "## Shape and departures\n\nDecisions this plan makes:\n\n- Departs from DEC_09 (x): y.\n- Departs from the delayed feed: real time.\n";
        IReadOnlyList<Decision> recorded = [Recorded(8)];

        IReadOnlyList<SchemaViolation> found = Departures.Check(ArtifactKind.Plan, plan, recorded);

        Assert.AreEqual(2, found.Count, string.Join("; ", found.Select(v => v.Problem)));
        StringAssert.Contains(found[0].Problem, "DEC_09");
        StringAssert.Contains(found[1].Problem, "names no decision");
        Assert.AreEqual(0, Departures.Check(ArtifactKind.Plan, plan.Replace("DEC_09", "DEC_08", StringComparison.Ordinal).Replace("- Departs from the delayed feed: real time.\n", string.Empty, StringComparison.Ordinal), recorded).Count);
    }

    [TestMethod]
    public void A_departure_from_a_decision_since_superseded_still_stands()
    {
        // Mining may supersede the very decision a cycle departed from, on the cycle's own branch (D-111).
        IReadOnlyList<Decision> recorded = [Recorded(8) with { SupersededBy = DecisionId.Of(9) }, Recorded(9)];

        Assert.AreEqual(0, Departures.Check(ArtifactKind.Intent, "## Fixed decisions\n\n- Departs from DEC_08 (x): y.\n", recorded).Count);
    }

    [TestMethod]
    public void The_report_names_the_decision_in_its_own_words_and_what_the_cycle_does_instead()
    {
        IReadOnlyList<string> lines = Departures.Report(
            [new Departure(DecisionId.Of(8), "(the delayed feed): the real-time feed. Peter, 2026-11-07: included now.", ArtifactKind.Intent)],
            [Recorded(8)]);

        Assert.AreEqual("This cycle departs from 1 recorded decision:", lines[0]);
        Assert.AreEqual("  - " + Delayed + " (DEC_08)", lines[1], "The decision's words first, the id trailing.");
        Assert.AreEqual("    instead: the real-time feed. Peter, 2026-11-07: included now.", lines[2]);
        Assert.AreEqual("    declared in: intent.md", lines[3]);
        Assert.AreEqual(0, Departures.Report([], [Recorded(8)]).Count, "Nothing to say, nothing said.");
    }

    [TestMethod]
    public async Task Every_reader_of_every_stage_is_handed_the_standing_decisions_in_short_form()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        Record(workspace, Entry(7, "charts are drawn by Lightweight Charts", supersededBy: 8), Entry(8, Delayed));
        string directory = Path.Combine(workspace.Root, "round");

        foreach (ArtifactKind kind in (ArtifactKind[])[ArtifactKind.Intent, ArtifactKind.Spec, ArtifactKind.Plan])
        {
            string prompt = await File.ReadAllTextAsync((await ReaderPromptsTests.Prompt(workspace, kind, directory, "rules")).Value.Prompts[0].Path);

            StringAssert.Contains(prompt, "DEC_08", kind + " readers are handed the record.");
            StringAssert.Contains(prompt, Delayed);
            StringAssert.Contains(prompt, "instead of", "The clause a contradiction is judged against.");
            Assert.IsFalse(prompt.Contains("charts are drawn by", StringComparison.Ordinal), "A superseded decision binds nothing.");
            Assert.IsFalse(prompt.Contains("the handler writes", StringComparison.Ordinal), "Short form: no Decided clause.");
        }
    }

    [TestMethod]
    public async Task A_solution_with_no_record_is_said_to_have_settled_nothing()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent, "## Problem\n\nThe die is hard to read.");

        string prompt = await File.ReadAllTextAsync((await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Intent, Path.Combine(workspace.Root, "round"), "rules")).Value.Prompts[0].Path);

        StringAssert.Contains(prompt, "This solution has no decisions.md: it has settled nothing.");
    }

    [TestMethod]
    public async Task A_verify_round_runs_the_diff_reader_with_the_change_and_not_the_criteria()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteTestSummary(proofs: 1);
        workspace.WriteVerify("pass");
        Record(workspace, Entry(8, Delayed));
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Cycle.EvidenceDirectory, "diff.patch"), "+var feed = RealTime();\n");
        string directory = Path.Combine(workspace.Root, "round");

        ReaderPrompts.Response listed = (await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Verify, directory, list: true)).Value;
        CollectionAssert.AreEqual(new[] { "rules", "diff" }, listed.Readers.Take(2).ToArray(), "The diff reader runs beside the rules reader.");

        ReaderPrompts.Response planRound = (await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Plan, directory, list: true)).Value;
        Assert.IsFalse(planRound.Readers.Contains("diff"), "A document has no diff to read.");

        string prompt = await File.ReadAllTextAsync((await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Verify, directory, "diff")).Value.Prompts[0].Path);

        StringAssert.Contains(prompt, "+var feed = RealTime();");
        StringAssert.Contains(prompt, "<file name=\"plan.md\">");
        StringAssert.Contains(prompt, "<file name=\"intent.md\">", "A departure may be declared in the intent.");
        StringAssert.Contains(prompt, Delayed);
        StringAssert.Contains(prompt, "CON_07.2");
        StringAssert.Contains(prompt, "CON_24");
        StringAssert.Contains(prompt, "never by the file list", "Scope by purpose: the file table is a guess.");
        Assert.IsFalse(prompt.Contains("<file name=\"spec.md\">", StringComparison.Ordinal), "The criteria are the adjudicator's question.");
        Assert.IsFalse(prompt.Contains("evidence/tests.json", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task The_diff_readers_findings_are_its_own_and_go_to_the_code()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteTestSummary(proofs: 1);
        workspace.WriteVerify("pass");
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Cycle.EvidenceDirectory, "diff.patch"), "+var feed = RealTime();\n");
        string directory = Path.Combine(workspace.Root, "round");
        ReaderPrompts.Response written = (await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Verify, directory)).Value;

        const string Blocker = """{ "findings": [ { "id": "F_01", "severity": "blocking", "cites": { "id": "CON_24", "heading": "A departure from a recorded decision is declared where it arises, and later stages rely on it" }, "gist": "The feed is real-time, where the solution decided on the delayed one.", "statement": "s", "locus": "diff.patch +1" } ] }""";

        foreach (string reader in written.Readers)
        {
            await File.WriteAllTextAsync(
                ReaderAnswers.AnswerPath(directory, reader),
                ReaderPromptsTests.Envelope(reader == "diff" ? Blocker : """{ "findings": [] }"""));
        }

        await File.WriteAllTextAsync(ReaderAnswers.AnswerPath(directory, ReaderAnswers.Reconcile), ReaderPromptsTests.Envelope("""{ "restatements": [], "pairs": [] }"""));

        using var gate = Gate.For(workspace.Root);
        Result<CollectRound.Response> collected = await gate.Send<CollectRound.Command, CollectRound.Response>(
            new CollectRound.Command(workspace.Cycle.Id, ArtifactKind.Verify, directory, "claude"));

        Assert.IsTrue(collected.IsSuccess, string.Join("; ", collected.Errors.Select(error => error.Message)));
        Finding finding = collected.Value.Raised.Single();

        StringAssert.StartsWith(finding.Id, "diff.", "Prefixed, so it cannot collide with the rules reader's ids.");
        Assert.IsNull(finding.Perspective, "Not a perspective: propose constraints counts by perspective.");
        Assert.IsTrue(ReaderAnswers.IsAboutTheChange(finding));
    }

    [TestMethod]
    public async Task The_rule_binds_the_three_documents_and_the_change_and_not_the_verdict()
    {
        using var workspace = CycleWorkspace.Create();
        using var gate = Gate.For(workspace.Root);

        foreach ((string artifact, bool binds) in (ValueTuple<string, bool>[])[("intent", true), ("spec", true), ("plan", true), ("diff", true), ("verify", false)])
        {
            Result<EffectiveConstraints.Response> set = await gate.Send<EffectiveConstraints.Query, EffectiveConstraints.Response>(new EffectiveConstraints.Query(artifact));

            Assert.AreEqual(binds, set.Value.Constraints.Any(rule => rule.Id.ToString() == "CON_24"), artifact);
            Assert.IsFalse(set.Value.Constraints.Any(rule => rule.Id.ToString() == "CON_19"), "Superseded, and binds nowhere.");
        }

        Result<EffectiveConstraints.Response> diff = await gate.Send<EffectiveConstraints.Query, EffectiveConstraints.Response>(new EffectiveConstraints.Query("diff"));
        StringAssert.Contains(string.Join(" ", diff.Value.Constraints.Select(rule => rule.Id.ToString())), "CON_07.2", "The rule that reached no reader until now.");
    }

    [TestMethod]
    public async Task The_approval_draft_lists_departures_first_and_apart_from_the_other_choices()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        // The helper writes this body under every section; only Shape and departures declares anything.
        workspace.WriteArtifact(ArtifactKind.Plan, """
            Decisions this plan makes:

            - Host the api under one AppHost.
            - Departs from DEC_08 (the delayed feed): the real-time feed.
            """);
        Record(workspace, Entry(8, Delayed));

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        using var gate = Gate.For(workspace.Root);
        Result<ApprovalText.Response> printed = await gate.Send<ApprovalText.Query, ApprovalText.Response>(
            new ApprovalText.Query(workspace.Cycle.Id, "Peter Almanzar"));

        string text = printed.Value.Instructions;
        int departing = text.IndexOf("This cycle departs from 1 recorded decision:", StringComparison.Ordinal);
        int choices = text.IndexOf("This approves the 1 decision the plan says it makes", StringComparison.Ordinal);

        Assert.IsTrue(departing >= 0 && choices > departing, text);
        StringAssert.Contains(text, Delayed + " (DEC_08)");
        Assert.IsFalse(text.Contains("Departs from DEC_08", StringComparison.Ordinal), "Listed among the departures, and not again among the choices.");
    }

    [TestMethod]
    public async Task An_approved_departure_is_offered_as_a_supersede_of_the_decision_it_left()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteArtifact(ArtifactKind.Intent, "- Departs from DEC_08 (the delayed feed): real time. Peter, 2026-11-07: included now.");
        Record(workspace, Entry(8, Delayed));

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        using var gate = Gate.For(workspace.Root);
        ApprovalText.Response printed = (await gate.Send<ApprovalText.Query, ApprovalText.Response>(new ApprovalText.Query(workspace.Cycle.Id, "Peter Almanzar"))).Value;
        await File.WriteAllTextAsync(
            workspace.Cycle.ApprovalPath,
            string.Create(CultureInfo.InvariantCulture, $"---\ncycle: {workspace.Cycle.Id}\ndecisions:\n{printed.Entry}---\n"));

        ProposeDecisions.Response proposed = (await gate.Send<ProposeDecisions.Query, ProposeDecisions.Response>(new ProposeDecisions.Query())).Value;

        Candidate departure = proposed.Candidates.Single(candidate => candidate.Source == DecisionSource.Departure);
        StringAssert.StartsWith(departure.Passage, "Departs from DEC_08");
        StringAssert.Contains(ProposalsRenderer.Render(proposed), "Superseded-by");
    }

    private static Decision Recorded(int number) =>
        new(DecisionId.Of(number), Delayed, "the delayed feed", "it costs nothing", "the real-time feed, which costs a subscription", "by hand");

    private static void Record(CycleWorkspace workspace, params string[] entries) =>
        CycleWorkspace.WriteRaw(
            Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, DecisionParser.FileName),
            "# This solution's decisions\n" + string.Join("\n", entries));

    private static string Entry(int number, string statement, int? supersededBy = null) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"\n## DEC_{number:D2} — {statement}\n\n**Decided:** the handler writes it\n\n**Because:** it is cheap\n\n**Instead of:** the real-time feed, which costs a subscription\n\n**From:** by hand\n\n{(supersededBy is { } by ? string.Create(CultureInfo.InvariantCulture, $"**Superseded-by:** DEC_{by:D2}\n") : string.Empty)}");
}
