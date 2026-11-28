using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Runs;

namespace Sdlc.Tests;

/// <summary>
/// A run's branch, <c>wave/&lt;id&gt;</c> or <c>series/&lt;id&gt;</c>: several cycles built one after another, in one
/// pull request, each checked as it would be alone, and every test their plans named passing at the end.
/// </summary>
[TestClass]
public sealed class VerifyRunTests
{
    private const string First = "20261121_0900_00";
    private const string Second = "20261121_0900_01";
    private const string Stopped = "20261121_0900_02";
    private const string FirstHead = "1111111111111111111111111111111111111111";
    private const string SecondHead = "2222222222222222222222222222222222222222";
    private const string RunBase = "0000000000000000000000000000000000000000";
    private const string MergeHead = "3333333333333333333333333333333333333333";
    private const string NamedTests = "every named test passed at the end of the run";
    private const string Accounted = "every change outside .sdlc/ belongs to a verified cycle";
    private const string Run = "20261121_0930_15";

    [TestMethod]
    [DataRow("wave/20261121_0930_15", RunKind.Wave)]
    [DataRow("series/20261121_0930_15", RunKind.Series)]
    [DataRow("refs/heads/series/20261121_0930_15", RunKind.Series)]
    public void A_run_branch_reads_back_its_kind_and_id(string branch, RunKind expected)
    {
        Assert.IsTrue(RunBranch.TryParse(branch, out RunKind kind, out string? id));
        Assert.AreEqual(expected, kind);
        Assert.AreEqual(Run, id);
        ArgumentNullException.ThrowIfNull(branch);
        Assert.AreEqual(branch.Replace("refs/heads/", string.Empty, StringComparison.Ordinal), RunBranch.NameOf(kind, id));
    }

    [TestMethod]
    [DataRow("series/tomorrow")]
    [DataRow("series/20261332_2599_99")]
    [DataRow("waves/20261121_0930_15")]
    [DataRow("cycle/20261121_0930_15/build")]
    [DataRow("series/20261121_0930_15/extra")]
    [DataRow(null)]
    public void Anything_else_is_not_a_run_branch(string? branch)
    {
        Assert.IsFalse(RunBranch.TryParse(branch, out _, out _));
    }

    [TestMethod]
    public void A_run_branch_is_a_build_branch_for_any_cycle_and_a_cycle_branch_only_for_its_own_build()
    {
        Assert.IsTrue(CycleId.TryParse(First, out CycleId? first));
        Assert.IsTrue(CycleId.TryParse(Second, out CycleId? second));

        Assert.IsTrue(RunBranch.BuildsOn("series/" + Run, first.Value));
        Assert.IsTrue(RunBranch.BuildsOn("cycle/" + First + "/build", first.Value));
        Assert.IsFalse(RunBranch.BuildsOn("cycle/" + First + "/plan", first.Value));
        Assert.IsFalse(RunBranch.BuildsOn("cycle/" + Second + "/build", first.Value));
        Assert.IsTrue(RunBranch.BuildsOn("cycle/" + Second + "/build", second.Value));
    }

    [TestMethod]
    public async Task Every_cycle_holding_and_every_named_test_passing_at_the_end_holds_the_run()
    {
        using CycleWorkspace workspace = Built();
        using CycleWorkspace second = After(workspace);

        VerifyRun.Response verdict = await Check(workspace, Changed(First, Second));

        Assert.IsTrue(verdict.Holds, Describe(verdict));
        Assert.AreEqual(2, verdict.Cycles.Count);
        StringAssert.Contains(verdict.Checks.Single(c => c.Name == NamedTests).Detail, "the last cycle's run, " + Second);
        Assert.IsTrue(verdict.Checks.Single(c => c.Name == Accounted).Passed);
        StringAssert.Contains(VerifyRun.Render(verdict), "The series holds");
    }

    [TestMethod]
    public async Task Cycles_built_side_by_side_are_held_by_the_run_on_their_merged_result()
    {
        // Each cycle built from the run's base on a branch of its own, so neither's head holds the other's: only the
        // suite run on the merge commit speaks for both (D-234).
        using CycleWorkspace workspace = Built();
        using CycleWorkspace second = Beside(workspace);
        WriteMerge(workspace, MergeHead, proofs: 3);

        VerifyRun.Response verdict = await Check(workspace, Changed(First, Second), SideBySide);

        Assert.IsTrue(verdict.Holds, Describe(verdict));
        StringAssert.Contains(verdict.Checks.Single(c => c.Name == NamedTests).Detail, "the run's last merge, " + MergeHead[..12]);
    }

    [TestMethod]
    public async Task Cycles_built_side_by_side_with_no_merge_evidence_are_not_in_one_line()
    {
        using CycleWorkspace workspace = Built();
        using CycleWorkspace second = Beside(workspace);

        VerifyRun.Response verdict = await Check(workspace, Changed(First, Second), SideBySide);

        VerifyChain.ChainCheck atTheEnd = verdict.Checks.Single(c => c.Name == NamedTests);
        Assert.IsFalse(atTheEnd.Passed);
        StringAssert.Contains(atTheEnd.Detail, "not in one line");
    }

    [TestMethod]
    public async Task Merge_evidence_that_does_not_hold_every_cycle_speaks_for_none_of_them()
    {
        // A merge recorded before the second cycle merged ran on less than the pull request carries.
        using CycleWorkspace workspace = Built();
        using CycleWorkspace second = Beside(workspace);
        WriteMerge(workspace, FirstHead, proofs: 3);

        VerifyRun.Response verdict = await Check(workspace, Changed(First, Second), SideBySide);

        Assert.IsFalse(verdict.Checks.Single(c => c.Name == NamedTests).Passed, Describe(verdict));
    }

    [TestMethod]
    public async Task A_test_failing_in_the_merged_run_fails_the_run_though_each_cycle_passed_alone()
    {
        using CycleWorkspace workspace = Built();
        using CycleWorkspace second = Beside(workspace);
        WriteMerge(workspace, MergeHead, proofs: 3, failing: 2);

        VerifyRun.Response verdict = await Check(workspace, Changed(First, Second), SideBySide);

        Assert.IsTrue(verdict.Cycles.All(c => c.Holds), Describe(verdict));
        VerifyChain.ChainCheck atTheEnd = verdict.Checks.Single(c => c.Name == NamedTests);
        Assert.IsFalse(atTheEnd.Passed);
        StringAssert.Contains(atTheEnd.Detail, "in the run's last merge");
        StringAssert.Contains(atTheEnd.Detail, "Thing_does_the_thing_2 (cycle " + Second + ")");
    }

    [TestMethod]
    public async Task One_cycle_whose_chain_fails_fails_the_run()
    {
        using CycleWorkspace workspace = Built();
        using CycleWorkspace second = After(workspace);
        second.WriteVerify(outcome: "blocked");

        VerifyRun.Response verdict = await Check(workspace, Changed(First, Second));

        Assert.IsFalse(verdict.Holds);
        Assert.IsTrue(verdict.Cycles.Single(c => c.Cycle.Id.ToString() == First).Holds, "the first cycle is not to blame");
        Assert.IsFalse(verdict.Cycles.Single(c => c.Cycle.Id.ToString() == Second).Holds);
        StringAssert.Contains(VerifyRun.Render(verdict), "does not hold");
    }

    [TestMethod]
    public async Task A_test_an_earlier_cycle_named_that_fails_in_the_last_cycles_run_fails_the_run_and_names_both()
    {
        using CycleWorkspace workspace = Built();
        using CycleWorkspace second = After(workspace);

        // The second cycle's run, the last, recorded a test both plans name failing; the first cycle's own run had
        // it passing, and nothing would verify the first again.
        second.WriteTestSummary(proofs: 3, failing: 2);

        VerifyRun.Response verdict = await Check(workspace, Changed(First, Second));

        VerifyChain.ChainCheck atTheEnd = verdict.Checks.Single(c => c.Name == NamedTests);
        Assert.IsFalse(atTheEnd.Passed);
        StringAssert.Contains(atTheEnd.Detail, "Thing_does_the_thing_2 (cycle " + First + ")");
        StringAssert.Contains(atTheEnd.Detail, "Thing_does_the_thing_2 (cycle " + Second + ")");
        StringAssert.Contains(atTheEnd.Detail, "last cycle's run, " + Second);
    }

    [TestMethod]
    public async Task A_test_only_the_earlier_cycle_named_must_still_pass_in_the_last_cycles_run()
    {
        using CycleWorkspace workspace = Built(firstProofs: 4);
        using CycleWorkspace second = After(workspace);

        // The first cycle's own run passed all four; the last run, the second's, ran only three of them.
        VerifyRun.Response verdict = await Check(workspace, Changed(First, Second));

        Assert.IsTrue(verdict.Cycles.All(c => c.Holds), Describe(verdict));
        VerifyChain.ChainCheck atTheEnd = verdict.Checks.Single(c => c.Name == NamedTests);
        Assert.IsFalse(atTheEnd.Passed);
        StringAssert.Contains(atTheEnd.Detail, "Thing_does_the_thing_4 (cycle " + First + ")");
        Assert.IsFalse(atTheEnd.Detail.Contains("(cycle " + Second + ")", StringComparison.Ordinal), atTheEnd.Detail);
    }

    [TestMethod]
    public async Task An_abandoned_cycle_in_the_run_is_admitted_as_a_record()
    {
        using CycleWorkspace workspace = Built();
        using CycleWorkspace second = After(workspace);
        using CycleWorkspace stopped = workspace.Beside(Stopped);
        stopped.WriteArtifact(ArtifactKind.Intent);
        stopped.Abandon();

        VerifyRun.Response verdict = await Check(
            workspace,
            [
                .. Changed(First, Second),
                ".sdlc/cycles/" + Stopped + "/intent.md",
                ".sdlc/cycles/" + Stopped + "/abandoned.md",
            ]);

        Assert.IsTrue(verdict.Holds, Describe(verdict));
        VerifyChain.Response record = verdict.Cycles.Single(c => c.Cycle.Id.ToString() == Stopped);
        Assert.IsTrue(record.Admission);
        Assert.IsTrue(record.Checks.Any(c => c.Name == "this pull request carries an abandoned cycle's record"), Describe(verdict));
    }

    [TestMethod]
    public async Task Code_no_built_cycles_evidence_records_fails_the_run_and_names_the_file()
    {
        // A cycle committed and never recorded, or an abandoned one whose code stayed: every cycle passes, and the
        // pull request still carries code nobody verified.
        using CycleWorkspace workspace = Built();
        using CycleWorkspace second = After(workspace);

        VerifyRun.Response verdict = await Check(workspace, [.. Changed(First, Second), "src/Stray.cs"]);

        Assert.IsTrue(verdict.Cycles.All(c => c.Holds), Describe(verdict));
        VerifyChain.ChainCheck accounted = verdict.Checks.Single(c => c.Name == Accounted);
        Assert.IsFalse(accounted.Passed);
        Assert.IsFalse(verdict.Holds);
        StringAssert.Contains(accounted.Detail, "src/Stray.cs");
        Assert.IsFalse(accounted.Detail.Contains("src/Thing.cs", StringComparison.Ordinal), accounted.Detail);
        StringAssert.Contains(accounted.Detail, "record that cycle's evidence");
        StringAssert.Contains(accounted.Detail, "revert that code");
    }

    [TestMethod]
    public async Task Code_changed_after_the_last_cycles_head_fails_the_run_unless_a_merge_of_the_integration_branch_brought_it()
    {
        using CycleWorkspace workspace = Built();
        using CycleWorkspace second = After(workspace);

        VerifyRun.Response late = await Check(workspace, Changed(First, Second), git => git.CommittedBetween[(SecondHead, "HEAD")] = ["src/Thing.cs"]);

        VerifyChain.ChainCheck refused = late.Checks.Single(c => c.Name == Accounted);
        Assert.IsFalse(refused.Passed);
        StringAssert.Contains(refused.Detail, "src/Thing.cs changed after the last cycle's recorded head");

        // The same change, brought by merging the integration branch after the last cycle: the merged tree is where the
        // run is measured from, and nothing is left.
        VerifyRun.Response merged = await Check(
            workspace,
            Changed(First, Second),
            git =>
            {
                git.CommittedBetween[(SecondHead, "HEAD")] = ["src/Thing.cs"];
                git.Merges[(SecondHead, "9999999999999999999999999999999999999999")] = ("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", []);
                git.CommittedBetween[("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", "HEAD")] = [];
            },
            divergence: "9999999999999999999999999999999999999999");

        Assert.IsTrue(merged.Holds, Describe(merged));
    }

    [TestMethod]
    public async Task Code_in_a_run_whose_only_cycles_are_paperwork_fails_the_run()
    {
        using var workspace = CycleWorkspace.Create(Stopped);
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.Abandon();

        VerifyRun.Response verdict = await Check(
            workspace,
            ["src/Thing.cs", ".sdlc/cycles/" + Stopped + "/intent.md", ".sdlc/cycles/" + Stopped + "/abandoned.md"]);

        Assert.IsTrue(verdict.Cycles.Single().Holds, Describe(verdict));
        VerifyChain.ChainCheck accounted = verdict.Checks.Single(c => c.Name == Accounted);
        Assert.IsFalse(accounted.Passed);
        StringAssert.Contains(accounted.Detail, "no cycle in this run was built");
        StringAssert.Contains(accounted.Detail, "src/Thing.cs");
    }

    [TestMethod]
    public async Task A_run_that_changes_no_cycle_is_refused()
    {
        using CycleWorkspace workspace = Built();
        using CycleWorkspace second = After(workspace);

        VerifyRun.Response verdict = await Check(workspace, ["src/Thing.cs"]);

        Assert.IsFalse(verdict.Holds);
        Assert.AreEqual(0, verdict.Cycles.Count);
    }

    [TestMethod]
    public void Every_cycle_whose_folder_a_pull_request_changes_is_found_whatever_else_it_changes()
    {
        IReadOnlyList<CycleId> found = VerifyChain.ChangedCycles(
        [
            "src/Thing.cs",
            ".sdlc/cycles/" + Second + "/verify.md",
            ".sdlc\\cycles\\" + First + "\\evidence\\tests.json",
            ".sdlc/cycles/not-an-id/plan.md",
            ".sdlc/successors/totals.md",
        ]);

        CollectionAssert.AreEqual(new[] { First, Second }, found.Select(id => id.ToString()).ToArray());
    }

    /// <summary>The first sound cycle; <see cref="After"/> builds the second on top of it.</summary>
    /// <remarks>Each workspace is disposed by the test; the first to go removes the shared directory.</remarks>
    private static CycleWorkspace Built(int firstProofs = 3)
    {
        var workspace = CycleWorkspace.Create(First);
        Sound(workspace, firstProofs, RunBase, FirstHead);
        return workspace;
    }

    /// <summary>The second sound cycle, its recorded head descending from the first's.</summary>
    private static CycleWorkspace After(CycleWorkspace workspace)
    {
        CycleWorkspace second = workspace.Beside(Second);
        Sound(second, 3, FirstHead, SecondHead);
        return second;
    }

    /// <summary>The second sound cycle, built beside the first from the run's base rather than on top of it.</summary>
    private static CycleWorkspace Beside(CycleWorkspace workspace)
    {
        CycleWorkspace second = workspace.Beside(Second);
        Sound(second, 3, RunBase, SecondHead);
        return second;
    }

    /// <summary>Both heads merged into one commit, and nothing changed since.</summary>
    private static void SideBySide(FakeRepository git)
    {
        git.Ancestors.Remove((FirstHead, SecondHead));
        git.Ancestors.Add((FirstHead, MergeHead));
        git.Ancestors.Add((SecondHead, MergeHead));
        git.CommittedBetween[(RunBase, SecondHead)] = ["src/Thing.cs"];
        git.CommittedBetween[(MergeHead, "HEAD")] = [];
    }

    /// <summary>The run's merge evidence: the merge commit, and the suite run on it.</summary>
    private static void WriteMerge(CycleWorkspace workspace, string head, int proofs, params int[] failing)
    {
        string directory = MergeEvidence.DirectoryIn(workspace.Root, RunKind.Series, Run);
        Directory.CreateDirectory(directory);
        CycleWorkspace.WriteRaw(Path.Combine(directory, RecordEvidence.HeadFileName), head + "\n");
        CycleWorkspace.WriteRaw(
            Path.Combine(directory, RecordEvidence.SummaryFileName),
            TestSummaryReader.Write(new TestSummary(
                null,
                DateTimeOffset.UnixEpoch,
                [.. Enumerable.Range(1, proofs).Select(number => new RecordedTest(
                    "Thing.Tests.Thing_does_the_thing_" + number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    failing.Contains(number) ? TestOutcome.Failed : TestOutcome.Passed))])));
    }

    private static void Sound(CycleWorkspace workspace, int proofs, string from, string head)
    {
        workspace.WriteArtifact(ArtifactKind.Intent);
        workspace.WriteSpec(criteria: proofs);
        workspace.WritePlan(proofs: proofs);
        workspace.WriteVerify(outcome: "pass");
        workspace.WriteTestSummary(proofs: proofs);
        workspace.WriteCoverage();

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Cycle.EvidenceDirectory, RecordEvidence.BaseFileName), from + "\n");
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Cycle.EvidenceDirectory, RecordEvidence.HeadFileName), head + "\n");
    }

    private static string[] Changed(params string[] ids) =>
        ["src/Thing.cs", .. ids.Select(id => ".sdlc/cycles/" + id + "/verify.md")];

    /// <summary>Checks the run; each cycle's recorded change is src/Thing.cs, and nothing moved after the last head.</summary>
    private static async Task<VerifyRun.Response> Check(
        CycleWorkspace workspace,
        IReadOnlyList<string> changed,
        Action<FakeRepository>? arrange = null,
        string? divergence = null)
    {
        var git = new FakeRepository();
        git.Ancestors.Add((FirstHead, SecondHead));
        git.CommittedBetween[(RunBase, FirstHead)] = ["src/Thing.cs", ".sdlc/cycles/" + First + "/evidence/tests.json"];
        git.CommittedBetween[(FirstHead, SecondHead)] = ["src/Thing.cs"];
        git.CommittedBetween[(SecondHead, "HEAD")] = [];
        arrange?.Invoke(git);

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        Result<VerifyRun.Response> result = await gate
            .Send<VerifyRun.Query, VerifyRun.Response>(new VerifyRun.Query(RunKind.Series, Run, changed, divergence));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static string Describe(VerifyRun.Response verdict) =>
        string.Join(
            "; ",
            verdict.Checks.Select(c => (c.Passed ? "ok " : "NO ") + c.Name + ": " + c.Detail)
                .Concat(verdict.Cycles.SelectMany(cycle => cycle.Checks.Where(c => !c.Passed).Select(c => cycle.Cycle.Id + " NO " + c.Name + ": " + c.Detail))));
}
