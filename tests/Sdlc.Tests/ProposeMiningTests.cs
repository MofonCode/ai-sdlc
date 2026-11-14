using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Proposals;

namespace Sdlc.Tests;

/// <summary>
/// The mining report's reading of how each cycle went (D-112): what the findings alone never held.
/// </summary>
/// <remarks>
/// MarketSense's second cycle is the case. Its findings showed a verify that argued about wording; what
/// they did not show was criterion 15 failing six verdicts in a row, or the five reopens in which a person
/// had already decided something and written down what they refused. Both were in the repository -- one
/// in git's history of <c>verify.md</c>, one in the approval ledger -- and the report read neither.
/// </remarks>
[TestClass]
public sealed class ProposeMiningTests
{
    [TestMethod]
    public async Task Every_verdict_the_verify_reached_is_read_from_git_and_a_repeated_failure_is_named()
    {
        // Only the last verdict is on disk. Each earlier one was a commit of verify.md, and a criterion
        // that fails verdict after verdict is an argument about what it asks -- a decision, or a reopen.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        string path = ".sdlc/cycles/" + workspace.Cycle.Id + "/verify.md";
        FakeRepository git = new();
        git.History["HEAD:" + path] =
        [
            ("c1", DateTimeOffset.UnixEpoch),
            ("c2", DateTimeOffset.UnixEpoch.AddDays(1)),
            ("c3", DateTimeOffset.UnixEpoch.AddDays(2)),
        ];
        git.Files["c1:" + path] = Verdict("blocked", "1. Met. Shown.", "2. Not met. The proof does not reach.");
        git.Files["c2:" + path] = Verdict("blocked", "1. Met. Shown.", "2. Not met. Still not.");
        git.Files["c3:" + path] = Verdict("pass", "1. Met. Shown.", "2. Met. Shown now.");

        string report = await Render(workspace, git);

        StringAssert.Contains(report, "verdicts: blocked on 2 -> blocked on 2 -> pass");
        StringAssert.Contains(report, "criterion 2 not met in 2 verdicts");
    }

    [TestMethod]
    public async Task A_reopen_is_shown_with_the_reason_the_person_wrote()
    {
        // A reopen is a person deciding something with its alternative already named -- "skip the
        // review records instead of narrowing the matcher" -- which is a decision ready to record.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteApproval("reopened", reason: "Skip the review records rather than narrow the matcher.");

        string report = await Render(workspace, new FakeRepository());

        StringAssert.Contains(report, "1 reopen,");
        StringAssert.Contains(report, "Skip the review records rather than narrow the matcher.");
    }

    [TestMethod]
    public async Task Deviations_are_counted_per_cycle_and_tallied_by_the_rule_behind_them()
    {
        // How far each plan had to bend, and which rules keep making it bend: the two things mining is asked
        // to say about deviations.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteApproval();
        CycleWorkspace.WriteRaw(Sdlc.Core.Deviations.DeviationsFile.PathOf(workspace.Cycle), """
            ## DEV_01 — An undeclared file is changed

            Kind: plan
            Departs from: plan.md, Files
            > content
            Blocker: diff.F_01 under CON_07.2
            Deviation: another file is changed.
            Why: the fault is there.

            ## DEV_02 — The proof waits for the page

            Kind: proof
            Departs from: plan.md, Proof
            > content
            Blocker: CON_07.2 again, the wait was undescribed
            Deviation: the probe waits until the page is live.
            Why: it read too early.

            """);

        string report = await Render(workspace, new FakeRepository());

        StringAssert.Contains(report, "deviated 2x (1 plan, 1 proof): 0 accepted, 0 declined, 2 unanswered", report);
        StringAssert.Contains(report, "Deviations   2 across 1 cycles, by the rule behind them");
        StringAssert.Contains(report, "CON_07.2: 2 in 1 cycle, 0 declined");
    }

    [TestMethod]
    public async Task An_argued_rule_says_how_long_its_longest_finding_stood_and_whose_rule_it_is()
    {
        // Forty findings answered at once and one argued for nineteen rounds read the same by count.
        // And a rule of the framework's own is not one this solution can mend, so it says where the
        // rest goes.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 1, id: "F_01", cites: "CON_03");
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 2, id: "F_01", cites: "CON_03");
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 3, id: "F_01", cites: "CON_03");

        string report = await Render(workspace, new FakeRepository());

        StringAssert.Contains(report, "one standing 3 rounds", report);
        StringAssert.Contains(report, "a note for ai-sdlc");
    }

    [TestMethod]
    public async Task An_entry_past_its_word_budget_is_listed_for_rewording()
    {
        // D-149. Mining is where a long entry already on the page gets shortened: proposed, never refused.
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            Path.Combine(workspace.Root, ".sdlc", "decisions.md"),
            "# Decisions\n\n## DEC_01 — " + string.Join(' ', Enumerable.Repeat("long", 20)) + "\n\n**Decided:** one.\n\n**Because:** two.\n\n**Instead of:** three.\n\n**From:** here.\n");

        string report = await Render(workspace, new FakeRepository());

        StringAssert.Contains(report, "Over the word budget");
        StringAssert.Contains(report, "DEC_01 (title 20 of 15)");
    }

    [TestMethod]
    public async Task The_rounds_each_document_took_are_counted()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent, round: 1);
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 1);
        workspace.WriteCleanRound(ArtifactKind.Spec, round: 2);

        string report = await Render(workspace, new FakeRepository());

        StringAssert.Contains(report, workspace.Cycle.Id + ": intent 1, spec 2;");
    }

    [TestMethod]
    public async Task What_each_cycle_and_document_cost_is_shown_beside_its_rounds()
    {
        // D-121: the rounds say what a cycle took; the usage says what that came to.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent, round: 1);
        CycleWorkspace.WriteRaw(Sdlc.Core.Findings.UsageLedger.PathOf(workspace.Cycle), """
            { "rounds": [ { "artifact": "intent", "round": 1, "recorded_at": "2026-11-03T09:00:00Z", "host": null,
              "readers": [ { "role": "rules", "model": null, "effort": null, "prompt_bytes": null, "input": 50000, "cache_write": null, "cache_read": null, "output": null, "turns": null, "seconds": null } ] } ] }
            """);

        string report = await Render(workspace, new FakeRepository());

        StringAssert.Contains(report, "spent 1 round, 50k weighted tokens: rules 50k");
        StringAssert.Contains(report, "intent 1 round, 50k weighted tokens");
    }

    private static string Verdict(string outcome, params string[] criteria) =>
        "---\ncycle: c\noutcome: " + outcome + "\n---\n\n# A verify\n\n## Criteria\n\n"
        + string.Join("\n", criteria) + "\n";

    private static async Task<string> Render(CycleWorkspace workspace, FakeRepository git)
    {
        workspace.WriteTechStack(string.Empty);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        Result<ProposeMining.Response> result = await gate
            .Send<ProposeMining.Query, ProposeMining.Response>(new ProposeMining.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return ProposeMining.Render(result.Value, "proposals.md");
    }
}
