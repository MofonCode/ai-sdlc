using Sdlc.Core.Evidence;

namespace Sdlc.Tests;

/// <summary>
/// 'sdlc evidence check' (D-120): what a verify cites that the evidence does not hold, reported and never
/// refused.
/// </summary>
[TestClass]
public sealed class CheckEvidenceTests
{
    private static readonly TestSummary _summary = new(
        "1.0.0",
        "20260929_1456_32",
        null,
        [
            new RecordedTest("Dice.Tests.Rolls_within_one_and_six", TestOutcome.Passed),
            new RecordedTest("Dice.Tests.Two_requests_differ", TestOutcome.Failed),
        ]);

    private const string Diff = """
        +++ b/tests/DiceTests.cs
        +    Assert.IsTrue(roll is >= 1 and <= 6);
        """;

    [TestMethod]
    public void A_quote_in_the_diff_and_a_test_in_the_summary_are_found()
    {
        string verify = "---\noutcome: pass\n---\n\n## Criteria\n\n1. Met. `Rolls_within_one_and_six` passed; `diff.patch` `tests/DiceTests.cs`: `Assert.IsTrue(roll is >= 1 and <= 6);`\n";

        (int cited, IReadOnlyList<string> unfound, IReadOnlyList<string> notPassed) = CheckEvidence.Check(verify, _summary, Diff);

        Assert.AreEqual(3, cited, "The evidence's own file names are not names to look for.");
        Assert.AreEqual(0, unfound.Count, string.Join(", ", unfound));
        Assert.AreEqual(0, notPassed.Count);
    }

    [TestMethod]
    public void A_quote_the_diff_does_not_hold_and_a_failed_test_said_to_pass_are_both_reported()
    {
        string verify = "## Criteria\n\n1. Met. `Two_requests_differ` passed; `Assert.AreNotEqual(first, second)`.\n";

        (_, IReadOnlyList<string> unfound, IReadOnlyList<string> notPassed) = CheckEvidence.Check(verify, _summary, Diff);

        CollectionAssert.AreEqual(new[] { "Assert.AreNotEqual(first, second)" }, unfound.ToArray());
        CollectionAssert.AreEqual(new[] { "Two_requests_differ" }, notPassed.ToArray());
    }

    [TestMethod]
    public void A_quote_reflowed_across_lines_is_still_found()
    {
        string verify = "1. Met. `Assert.IsTrue(roll is >= 1\n   and <= 6);`\n".Replace("\n   and", " and", StringComparison.Ordinal);

        Assert.AreEqual(0, CheckEvidence.Check(verify, _summary, Diff.Replace("and <= 6", "and\n+        <= 6", StringComparison.Ordinal).Replace("+        ", string.Empty, StringComparison.Ordinal)).Unfound.Count);
    }

    [TestMethod]
    public async Task The_platform_and_the_commits_the_evidence_records_are_found()
    {
        // D-161. Every verdict on one solution listed its platform and its commits as cited from nowhere, while
        // the report said it had searched every file they were in.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteTestSummary(proofs: 1);
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Cycle.EvidenceDirectory, "base.sha"), "0123456789abcdef0123456789abcdef01234567\n");
        CycleWorkspace.WriteRaw(
            workspace.Cycle.PathOf(Sdlc.Core.Cycles.ArtifactKind.Verify),
            "---\noutcome: pass\n---\n\n## Criteria\n\n1. Met. Measured from `0123456789abcdef0123456789abcdef01234567` against the `coverage_floor`, on `Thing_does_the_thing_1`.\n");

        using var gate = Gate.For(workspace.Root);
        FluentResults.Result<CheckEvidence.Response> checkedOut = await gate.Send<CheckEvidence.Query, CheckEvidence.Response>(new CheckEvidence.Query(workspace.Cycle.Id));

        Assert.IsTrue(checkedOut.IsSuccess, string.Join("; ", checkedOut.Errors.Select(error => error.Message)));
        Assert.AreEqual(0, checkedOut.Value.Unfound.Count, string.Join(", ", checkedOut.Value.Unfound));
    }

    [TestMethod]
    public void The_report_says_it_refuses_nothing()
    {
        string rendered = CheckEvidence.Render(new CheckEvidence.Response(null!, 2, ["x"], []));

        StringAssert.Contains(rendered, "refuses nothing");
        StringAssert.Contains(rendered, "In none of the recorded evidence");
    }
}
