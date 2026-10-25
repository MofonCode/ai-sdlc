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
          "cites": { "id": "CON_06", "heading": "a plan's proof is adequate to the criterion it claims to prove" },
          "statement": "something that must be answered." } ] }
        """;

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
        // The MarketSense case: the intent, the spec and the plan edited in one pass and judged at once.
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

    private static async Task<Result<RecordRound.Response>> Record(CycleWorkspace workspace, ArtifactKind kind, string json)
    {
        string from = Path.Combine(workspace.Root, "readers." + Cycle.NameOf(kind) + ".json");
        await File.WriteAllTextAsync(from, json);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<TimeProvider>(new FakeClock(_now)));
        return await gate.Send<RecordRound.Command, RecordRound.Response>(
            new RecordRound.Command(workspace.Cycle.Id, kind, from));
    }
}
