using System.Globalization;
using System.Text;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Evidence;

/// <summary>
/// Renders what a build recorded.
/// </summary>
/// <remarks>
/// Two audiences at once: a person seeing whether the build went anywhere, and the verify stage being told
/// what it may read. So it names the files rather than summarising their contents, and it is explicit that
/// the comparison below is a signal — because a reader who takes it for a gate will either panic about an
/// undeclared file or stop trusting the line that reports one.
/// </remarks>
public static class EvidenceRenderer
{
    private const string Indent = "  ";

    /// <summary>
    /// Renders an evidence report.
    /// </summary>
    /// <param name="report">What was recorded.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(RecordEvidence.Response report)
    {
        ArgumentNullException.ThrowIfNull(report);

        StringBuilder output = new();

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}evidence - {report.Cycle.Id}"));

        output.AppendLine();

        foreach (string file in report.Written)
        {
            output.AppendLine(Indent + "    " + file);
        }

        output.AppendLine();

        if (report.Summary is { } summary)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}Tests     {summary.Passed} passed, {summary.Failed} failed, {summary.Skipped} not run"));

            // Said every time, including for one. A summary built from one results file while the run
            // produced two is how a verification adjudicates against half a suite and reports nothing
            // unusual -- so the count that would have shown it is on the page whether or not it is news.
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}          {Count(summary.Tests.Count, "result")} from {Count(report.TestSources.Count, "results file")}"));

            foreach (string source in report.TestSources)
            {
                output.AppendLine(Indent + "    " + source);
            }

            output.AppendLine();
            output.AppendLine(Indent + "          Every test project a run covers writes its own results file. If a project");
            output.AppendLine(Indent + "          is missing from that list, its tests are missing from the summary a");
            output.AppendLine(Indent + "          verification reads -- point --tests at the directory holding them all.");

            foreach (RecordedTest failure in summary.Tests.Where(test => test.Outcome == TestOutcome.Failed))
            {
                output.AppendLine(Indent + "    " + failure.Name);

                if (failure.Detail is { } detail)
                {
                    output.AppendLine(Indent + "        " + detail.ReplaceLineEndings(" "));
                }
            }
        }
        else
        {
            output.AppendLine(Indent + "Tests     none recorded yet");
        }

        if (report.Coverage is { } coverage)
        {
            output.AppendLine();
            output.AppendLine(coverage.ChangedRate is { } changed
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Indent}Coverage  changed lines {changed:P1} ({coverage.ChangedCovered} of {coverage.ChangedCoverable}), floor {CoverageSummary.ChangedFloor:P0}{(coverage.ChangedMeetsFloor ? string.Empty : " -- below it")}")
                : string.Create(CultureInfo.InvariantCulture, $"{Indent}Coverage  the change added no coverable line"));
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}          whole codebase {coverage.Rate:P1}, {Codebase(coverage)}"));
        }

        if (report.RetiredVerdict)
        {
            output.AppendLine();
            output.AppendLine(Indent + "Verdict   the blocked verify.md was removed: it judged the run this one replaces.");
            output.AppendLine(Indent + "          Commit its removal with this evidence, then verify again.");
        }

        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}Changed   {Count(report.Changed.Count, "file")}"));

        Comparison(output, report);

        output.AppendLine();
        output.AppendLine(Indent + "Verify reads tests.json, the diff and the coverage, and nothing else. A claim");
        output.AppendLine(Indent + "it cannot trace to one of these files is a claim it must not make.");

        return output.ToString();
    }

    /// <summary>
    /// Writes the declared-against-changed comparison, saying plainly that it decides nothing.
    /// </summary>
    /// <remarks>
    /// Stated every time, including when the two sets agree. A reader who met this line only on a
    /// disagreement would have no way to know whether it was about to stop them.
    /// </remarks>
    private static void Comparison(StringBuilder output, RecordEvidence.Response report)
    {
        FileComparison comparison = report.Comparison;

        if (comparison.Agrees)
        {
            output.AppendLine(Indent + "          every declared file changed, and nothing else did");

            if (comparison.Governing.Count == 0)
            {
                return;
            }
        }

        foreach (string path in comparison.Undeclared)
        {
            output.AppendLine(Indent + "    undeclared  " + path);
        }

        foreach (string path in comparison.Undelivered)
        {
            output.AppendLine(Indent + "    undelivered " + path);
        }

        // Listed after the two directions and whether or not they disagreed, because a governing file is
        // most often declared and in order -- which is the case worth a second look rather than the one
        // worth skipping.
        foreach (string path in comparison.Governing)
        {
            output.AppendLine(Indent + "    governing   " + path);
        }

        output.AppendLine();

        if (comparison.Governing.Count > 0)
        {
            output.AppendLine(Indent + "          A governing file holds a rule this cycle is judged by -- an");
            output.AppendLine(Indent + "          architecture test, a build property, the banned list, a hook, the");
            output.AppendLine(Indent + "          pipeline, the declared stack, this solution's constraints. Changing");
            output.AppendLine(Indent + "          one is a change to the rules rather than to the work, and a plan");
            output.AppendLine(Indent + "          declaring it makes it ordinary rather than unremarkable.");
            output.AppendLine();
        }
        output.AppendLine(Indent + "          These are suggestions, not a refusal. The comparison cannot see inside");
        output.AppendLine(Indent + "          a file, which is why CON_07.2 exists -- so it was never what caught scope");
        output.AppendLine(Indent + "          creep. What blocks is outcome-based: the build fails, tests fail, or a");
        output.AppendLine(Indent + "          criterion's proof is not met.");
    }

    /// <summary>The whole codebase against its baseline and the floor, in the terms the merge judges it (D-157).</summary>
    private static string Codebase(CoverageSummary coverage) => coverage switch
    {
        { Baseline: null } => "the first measurement, which sets the baseline",
        { HeldTheBaseline: true } => string.Create(CultureInfo.InvariantCulture, $"last recorded {coverage.Baseline:P1} -- held, and carried forward"),
        { AtTheFloor: true } => string.Create(
            CultureInfo.InvariantCulture,
            $"last recorded {coverage.Baseline:P1} -- lower, but at or above the floor of {SolutionDocument.FloorName(coverage.Floor ?? 0)}, so it holds and becomes the baseline"),
        _ => string.Create(
            CultureInfo.InvariantCulture,
            $"last recorded {coverage.Baseline:P1} -- fell, under the floor ({(coverage.Floor is { } floor ? SolutionDocument.FloorName(floor) : "none declared")}), so the merge refuses it and the baseline stays"),
    };

    private static string Count(int number, string noun) => number == 1

        ? "1 " + noun
        : string.Create(CultureInfo.InvariantCulture, $"{number} {noun}s");
}
