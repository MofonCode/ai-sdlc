using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Approvals;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Findings;
using Sdlc.Core.Hooks;

namespace Sdlc.Tests;

/// <summary>
/// 'sdlc round' (D-101): the binary owns a round's envelope and refuses one out of the chain's order.
/// </summary>
[TestClass]
public sealed class RecordRoundTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 24, 18, 0, 0, TimeSpan.Zero);

    private const string Clean = """{ "findings": [] }""";

    private const string Blocking = """
        { "findings": [ { "id": "F_01", "severity": "blocking", "disposition": "open",
          "cites": { "id": "CON_09", "heading": "No artifact restates another's content as its own authority" },
          "statement": "something that must be answered.", "gist": "The second criterion has no test." } ] }
        """;

    [TestMethod]
    [DataRow(null, DisplayName = "no gist")]
    [DataRow("", DisplayName = "an empty one")]
    [DataRow("two\nlines", DisplayName = "one over two lines")]
    public async Task A_blocker_raised_without_a_one_line_gist_is_refused_and_names_the_finding(string? gist)
    {
        // A panel showing an id and a rule told somebody at the gate that a rule was broken and not what
        // to look at. The reader that raised the blocker knows, so a round that raises one says it.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);

        string field = gist is null ? string.Empty : ", \"gist\": " + System.Text.Json.JsonSerializer.Serialize(gist);
        Result<RecordRound.Response> refused = await Record(
            workspace,
            ArtifactKind.Intent,
            Blocking.Replace(", \"gist\": \"The second criterion has no test.\"", field, StringComparison.Ordinal));

        Assert.IsTrue(refused.IsFailed, "A blocker with no usable gist was recorded.");
        StringAssert.Contains(refused.Errors[0].Message, "F_01");
        StringAssert.Contains(refused.Errors[0].Message, "gist");
    }

    [TestMethod]
    public async Task A_blocker_carried_forward_from_before_gists_needs_none()
    {
        // A cycle begun before gists holds blockers raised without one. Carrying such a finding forward is
        // not raising it, so the round is not refused over words nobody can now add.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);

        string without = Blocking.Replace(", \"gist\": \"The second criterion has no test.\"", string.Empty, StringComparison.Ordinal);
        CycleWorkspace.WriteRaw(workspace.Cycle.FindingsPath(ArtifactKind.Intent, 1), without
            .Replace("{ \"findings\"", "{ \"round\": 1, \"interrogated_at\": \"2026-10-01T10:00:00Z\", \"findings\"", StringComparison.Ordinal));

        Result<RecordRound.Response> carried = await Record(workspace, ArtifactKind.Intent, without);

        Assert.IsTrue(carried.IsSuccess, string.Join("; ", carried.Errors.Select(error => error.Message)));
    }

    [TestMethod]
    public async Task A_round_records_its_release_and_holds_its_cycle_to_the_suggestion_fence_while_an_older_one_does_not()
    {
        // D-131: the retired framework_version was the envelope's format and never moved, so nothing said a cycle's rules
        // had changed under it. D-128: the fence is for cycles that open under it -- an intent approved in the
        // old shape is not refused by a rule written after it.
        using var fresh = CycleWorkspace.Create();
        fresh.WriteArtifact(ArtifactKind.Intent);

        Assert.IsTrue(await NewerRules.Hold(fresh.Cycle, CancellationToken.None), "Not yet interrogated: the fence holds.");
        Assert.IsTrue((await Record(fresh, ArtifactKind.Intent, Clean)).IsSuccess);

        LedgerRound stamped = (await FindingsReader.ReadLedger(fresh.Cycle, ArtifactKind.Intent, CancellationToken.None)).Value.Ledger.Rounds[0];

        Assert.AreEqual(Sdlc.Core.Release.Version, stamped.Release);
        Assert.IsTrue(await NewerRules.Hold(fresh.Cycle, CancellationToken.None));

        using var older = CycleWorkspace.Create();
        older.WriteArtifact(ArtifactKind.Intent);
        CycleWorkspace.WriteRaw(older.Cycle.FindingsPath(ArtifactKind.Intent, 1), """{ "round": 1, "interrogated_at": "2026-10-01T10:00:00Z", "findings": [] }""");

        Assert.IsFalse(await NewerRules.Hold(older.Cycle, CancellationToken.None), "A round from before releases were recorded.");
    }

    [TestMethod]
    public async Task An_intent_round_is_numbered_stamped_and_bound_to_what_it_judged()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);

        RecordRound.Response written = (await Record(workspace, ArtifactKind.Intent, Clean)).Value;

        Assert.AreEqual(1, written.Written.Round);
        Assert.AreEqual(_now, written.Written.InterrogatedAt, "The binary's clock, not the session's.");
        Assert.AreEqual(workspace.HashOf(ArtifactKind.Intent), written.Written.SubjectSha256);
        Assert.IsNull(written.Written.UpstreamSha256, "An intent has no upstream.");
        Assert.IsTrue(File.Exists(workspace.Cycle.FindingsLedgerPath(ArtifactKind.Intent)), "One ledger per artifact (D-107).");
        Assert.IsFalse(File.Exists(workspace.Cycle.FindingsPath(ArtifactKind.Intent, 1)), "And no file per round.");
        Assert.IsNotNull(written.Written.EntrySha256, "The round is chained.");
    }

    [TestMethod]
    [DataRow("CON_06", null, DisplayName = "a superseded rule")]
    [DataRow("CON_21", null, DisplayName = "a plan's rule against an intent")]
    [DataRow("CON_99", null, DisplayName = "an id nobody issued")]
    [DataRow("CON_01", "security", DisplayName = "a rule the perspective does not list")]
    public async Task A_new_blocker_citing_a_rule_its_reader_was_not_handed_is_refused(string rule, string? perspective)
    {
        // round collect already makes such a blocker a suggestion; this is a draft edited after it (D-174, D-175).
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string id = perspective is null ? "F_01" : perspective + ".F_01";
        string under = perspective is null ? string.Empty : $", \"perspective\": \"{perspective}\"";

        Result<RecordRound.Response> refused = await Record(workspace, ArtifactKind.Intent, $$"""
            { "findings": [ { "id": "{{id}}", "severity": "blocking", "disposition": "open"{{under}},
              "cites": { "id": "{{rule}}", "heading": "whatever the reader wrote" },
              "statement": "x.", "gist": "y." } ] }
            """);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, id + " cites " + rule);
        StringAssert.Contains(refused.Errors[0].Message, "sdlc constraints --for intent");
    }

    [TestMethod]
    public async Task A_blocker_the_ledger_already_holds_is_never_judged_again()
    {
        // A round recorded under an earlier library stands as it was read: CON_06 was in force when it was raised.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string old = Blocking.Replace("CON_09", "CON_06", StringComparison.Ordinal);
        CycleWorkspace.WriteRaw(workspace.Cycle.FindingsPath(ArtifactKind.Intent, 1), old
            .Replace("{ \"findings\"", "{ \"round\": 1, \"interrogated_at\": \"2026-10-01T10:00:00Z\", \"findings\"", StringComparison.Ordinal));

        Result<RecordRound.Response> carried = await Record(workspace, ArtifactKind.Intent, old);

        Assert.IsTrue(carried.IsSuccess, string.Join("; ", carried.Errors.Select(error => error.Message)));
    }

    [TestMethod]
    public async Task A_new_finding_whose_rule_line_was_edited_is_refused_naming_the_real_line()
    {
        // round collect copies the rule's own line in, so this is a draft edited after it (D-177).
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);

        Result<RecordRound.Response> refused = await Record(workspace, ArtifactKind.Intent, Blocking.Replace(
            "No artifact restates another's content as its own authority", "Artifacts should not copy each other", StringComparison.Ordinal));

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "F_01 quotes CON_09 as \"Artifacts should not copy each other\"");
        StringAssert.Contains(refused.Errors[0].Message, "its line is \"No artifact restates another's content as its own authority\"");
    }

    [TestMethod]
    [DataRow("No artifact restates another’s content  as its own authority", DisplayName = "typographic quote and doubled space")]
    public async Task A_line_differing_only_in_whitespace_quotes_or_dashes_is_the_same_line(string quoted)
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);

        Result<RecordRound.Response> recorded = await Record(workspace, ArtifactKind.Intent, Blocking.Replace(
            "No artifact restates another's content as its own authority", quoted, StringComparison.Ordinal));

        Assert.IsTrue(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(error => error.Message)));
    }

    [TestMethod]
    public async Task A_finding_the_ledger_already_holds_keeps_the_line_it_was_judged_under()
    {
        // The frozen line is the point of D-63: a revision since is not a misquote, and a recorded round is not
        // judged again (D-177).
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        string old = Blocking.Replace("No artifact restates another's content as its own authority", "the wording of an earlier revision", StringComparison.Ordinal);
        CycleWorkspace.WriteRaw(workspace.Cycle.FindingsPath(ArtifactKind.Intent, 1), old
            .Replace("{ \"findings\"", "{ \"round\": 1, \"interrogated_at\": \"2026-10-01T10:00:00Z\", \"findings\"", StringComparison.Ordinal));

        Result<RecordRound.Response> carried = await Record(workspace, ArtifactKind.Intent, old);

        Assert.IsTrue(carried.IsSuccess, string.Join("; ", carried.Errors.Select(error => error.Message)));
    }

    [TestMethod]
    public async Task A_citation_of_an_earlier_revision_is_held_to_the_line_in_force_now()
    {
        // CON_12 is in force as CON_12.2. A reader is handed the current line, so that is what a new quote is held
        // to, whichever suffix the citation carries (D-177).
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteArtifact(ArtifactKind.Spec);
        await Record(workspace, ArtifactKind.Intent, Clean);
        const string current = "A criterion's check is one the solution's testing layers can run";

        string citing(string heading) => $$"""
            { "findings": [ { "id": "F_01", "severity": "blocking", "disposition": "open",
              "cites": { "id": "CON_12", "heading": {{JsonSerializer.Serialize(heading)}} },
              "statement": "x.", "gist": "y." } ] }
            """;

        Assert.IsTrue((await Record(workspace, ArtifactKind.Spec, citing(current + " as first written"))).IsFailed);
        Result<RecordRound.Response> recorded = await Record(workspace, ArtifactKind.Spec, citing(current));
        Assert.IsTrue(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(error => error.Message)));
    }

    [TestMethod]
    public async Task A_revision_of_a_rule_in_force_is_the_same_rule()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);

        Result<RecordRound.Response> recorded = await Record(workspace, ArtifactKind.Intent, Blocking.Replace("\"CON_09\"", "\"CON_09.7\"", StringComparison.Ordinal));

        Assert.IsTrue(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(error => error.Message)));
    }

    [TestMethod]
    public async Task The_next_round_takes_the_next_number_whatever_the_file_says()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await Record(workspace, ArtifactKind.Intent, Blocking);

        RecordRound.Response second = (await Record(
            workspace,
            ArtifactKind.Intent,
            """{ "round": 9, "interrogated_at": "2001-01-01T00:00:00Z", "findings": [] }""")).Value;

        Assert.AreEqual(2, second.Written.Round);
        Assert.AreEqual(_now, second.Written.InterrogatedAt);
    }

    [TestMethod]
    public async Task A_spec_round_waits_for_an_intent_round()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteArtifact(ArtifactKind.Spec);

        Result<RecordRound.Response> refused = await Record(workspace, ArtifactKind.Spec, Clean);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "intent.md has no interrogation round");
    }

    [TestMethod]
    public async Task A_spec_round_waits_while_the_intent_has_a_blocker_open()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteArtifact(ArtifactKind.Spec);
        await Record(workspace, ArtifactKind.Intent, Blocking);

        Result<RecordRound.Response> refused = await Record(workspace, ArtifactKind.Spec, Clean);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "open blocking finding: F_01");
    }

    [TestMethod]
    public async Task A_spec_round_is_refused_when_the_intent_moved_after_its_clean_round()
    {
        // The real case: the intent, the spec and the plan edited in one pass and judged at once.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteArtifact(ArtifactKind.Spec);
        await Record(workspace, ArtifactKind.Intent, Clean);
        workspace.WriteArtifact(ArtifactKind.Intent, "steered");

        Result<RecordRound.Response> refused = await Record(workspace, ArtifactKind.Spec, Clean);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "intent.md changed after its round 1");
    }

    [TestMethod]
    public async Task A_settled_chain_admits_the_next_round_judged_against_the_upstream_now()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        await Record(workspace, ArtifactKind.Intent, Clean);
        await Record(workspace, ArtifactKind.Spec, Clean);

        RecordRound.Response plan = (await Record(workspace, ArtifactKind.Plan, Clean)).Value;

        Assert.AreEqual(workspace.HashOf(ArtifactKind.Spec), plan.Written.UpstreamSha256);
        Assert.AreEqual(workspace.HashOf(ArtifactKind.Plan), plan.Written.SubjectSha256);
    }

    [TestMethod]
    public async Task A_plan_round_is_refused_when_the_intent_moved_under_a_settled_spec()
    {
        // The spec's own round is current about the spec, and judged an intent that is gone.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        await Record(workspace, ArtifactKind.Intent, Clean);
        await Record(workspace, ArtifactKind.Spec, Clean);
        workspace.WriteArtifact(ArtifactKind.Intent, "steered");

        Result<RecordRound.Response> refused = await Record(workspace, ArtifactKind.Plan, Clean);

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "intent.md changed after its round 1");
        StringAssert.Contains(refused.Errors[0].Message, "spec.md round 1 judged it against a intent.md that has since changed");
    }

    [TestMethod]
    public async Task A_document_edited_after_its_clean_round_is_stale_at_the_gate()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        await Record(workspace, ArtifactKind.Intent, Clean);
        await Record(workspace, ArtifactKind.Spec, Clean);
        await Record(workspace, ArtifactKind.Plan, Clean);
        workspace.WritePlan(proofs: 1);
        workspace.WriteApproval("approved");

        using var gate = Gate.For(workspace.Root);
        ApprovalGate.Response verdict = (await gate.Send<ApprovalGate.Query, ApprovalGate.Response>(
            new ApprovalGate.Query(workspace.Cycle.Id))).Value;

        Assert.IsFalse(verdict.MayBuild, "A plan changed after its clean round is a plan nobody judged.");
        StringAssert.Contains(verdict.Explanation, "plan.md: round 1 judged");
    }

    [TestMethod]
    public async Task A_verify_round_records_the_evidence_it_read()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        await Record(workspace, ArtifactKind.Intent, Clean);
        await Record(workspace, ArtifactKind.Spec, Clean);
        await Record(workspace, ArtifactKind.Plan, Clean);
        workspace.WriteVerify("pass");
        workspace.WriteTestSummary(proofs: 1);

        RecordRound.Response verify = (await Record(workspace, ArtifactKind.Verify, Clean)).Value;

        Assert.AreEqual(workspace.HashOf(ArtifactKind.Plan), verify.Written.UpstreamSha256);
        Assert.IsNotNull(verify.Written.EvidenceSha256);
        Assert.AreEqual(await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None), verify.Written.EvidenceSha256);
    }

    [TestMethod]
    public async Task The_changed_sections_are_the_ones_whose_text_moved()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteArtifact(ArtifactKind.Intent);
        await Record(workspace, ArtifactKind.Intent, Clean);

        string path = workspace.Cycle.PathOf(ArtifactKind.Intent);
        string text = await File.ReadAllTextAsync(path);
        int first = text.IndexOf("## ", StringComparison.Ordinal);
        string heading = text[(first + 3)..text.IndexOf('\n', first)];
        int body = text.IndexOf("content", first, StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, text[..body] + "moved" + text[(body + "content".Length)..]);

        using var gate = Gate.For(workspace.Root);
        ChangedSections.Response changed = (await gate.Send<ChangedSections.Query, ChangedSections.Response>(
            new ChangedSections.Query(workspace.Cycle.Id, ArtifactKind.Intent))).Value;

        CollectionAssert.AreEqual(new[] { heading }, changed.Changed.ToArray());
        Assert.AreEqual(1, changed.Since);
    }

    [TestMethod]
    [DataRow("Write", ".sdlc/cycles/20260929_1456_32/findings/plan.r3.json")]
    [DataRow("Bash", "cp /tmp/round.json .sdlc/cycles/x/findings/spec.r2.json")]
    [DataRow("Bash", "python -c pass > .sdlc/cycles/x/findings/intent.r1.json")]
    [DataRow("Write", ".sdlc/cycles/20260929_1456_32/findings/plan.json")]
    [DataRow("Write", ".sdlc\\cycles\\20260929_1456_32\\findings\\spec.json")]
    [DataRow("Bash", "cp /tmp/ledger.json .sdlc/cycles/x/findings/verify.json")]
    [DataRow("Bash", "jq . /tmp/x > .sdlc/cycles/x/findings/intent.json")]
    public void An_agent_cannot_write_a_round_itself(string tool, string target)
    {
        string payload = tool == "Write"
            ? JsonSerializer.Serialize(new { tool_name = tool, tool_input = new { file_path = target } })
            : JsonSerializer.Serialize(new { tool_name = tool, tool_input = new { command = target } });

        HookVerdict verdict = HookPolicy.Decide(HookProtocol.ReadCall(payload, HookHost.Claude).Value);

        Assert.AreEqual(HookDecision.Deny, verdict.Decision);
        StringAssert.Contains(verdict.Reason, "sdlc round");
    }

    [TestMethod]
    public void Reading_a_round_or_handing_one_to_the_binary_is_allowed()
    {
        foreach (string command in new[]
        {
            "cat .sdlc/cycles/x/findings/plan.r3.json",
            "cat .sdlc/cycles/x/findings/plan.json",
            "sdlc round 20260929_1456_32 plan --from /tmp/plan-round.json",

            // A ledger's name is only a ledger's inside a findings directory: a solution's own plan.json
            // is nothing the hook protects.
            "cp /tmp/x src/config/plan.json",
            "echo {} > plan.json",
        })
        {
            string payload = JsonSerializer.Serialize(new { tool_name = "Bash", tool_input = new { command } });
            Assert.AreEqual(HookDecision.Allow, HookPolicy.Decide(HookProtocol.ReadCall(payload, HookHost.Claude).Value).Decision, command);
        }
    }

    internal static async Task<Result<RecordRound.Response>> Record(CycleWorkspace workspace, ArtifactKind kind, string json)
    {
        string from = Path.Combine(workspace.Root, "readers." + Cycle.NameOf(kind) + ".json");
        await File.WriteAllTextAsync(from, json);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<TimeProvider>(new FakeClock(_now)));
        return await gate.Send<RecordRound.Command, RecordRound.Response>(
            new RecordRound.Command(workspace.Cycle.Id, kind, from));
    }
}
