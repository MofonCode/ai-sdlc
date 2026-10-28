using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Proposals;

namespace Sdlc.Tests;

/// <summary>
/// The convergence report's reading of how each cycle went (D-112): what the findings alone never held.
/// </summary>
/// <remarks>
/// MarketSense's second cycle is the case. Its findings showed a verify that argued about wording; what
/// they did not show was criterion 15 failing six verdicts in a row, or the five reopens in which a person
/// had already decided something and written down what they refused. Both were in the repository -- one
/// in git's history of <c>verify.md</c>, one in the approval ledger -- and the report read neither.
/// </remarks>
[TestClass]
public sealed class ProposeConvergenceTests
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

    private static string Verdict(string outcome, params string[] criteria) =>
        "---\nframework_version: 1.0.0\ncycle: c\noutcome: " + outcome + "\n---\n\n# A verify\n\n## Criteria\n\n"
        + string.Join("\n", criteria) + "\n";

    private static async Task<string> Render(CycleWorkspace workspace, FakeRepository git)
    {
        workspace.WriteTechStack(string.Empty);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        Result<ProposeConvergence.Response> result = await gate
            .Send<ProposeConvergence.Query, ProposeConvergence.Response>(new ProposeConvergence.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return ProposeConvergence.Render(result.Value, "proposals.md");
    }
}
