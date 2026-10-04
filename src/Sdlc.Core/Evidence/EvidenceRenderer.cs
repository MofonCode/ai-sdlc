using System.Globalization;
using System.Text;

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

        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}Changed   {Count(report.Changed.Count, "file")}"));

        Comparison(output, report);

        output.AppendLine();
        output.AppendLine(Indent + "Verify reads tests.json and the diff, and nothing else. A claim it cannot");
        output.AppendLine(Indent + "trace to one of these files is a claim it must not make.");

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
            return;
        }

        foreach (string path in comparison.Undeclared)
        {
            output.AppendLine(Indent + "    undeclared  " + path);
        }

        foreach (string path in comparison.Undelivered)
        {
            output.AppendLine(Indent + "    undelivered " + path);
        }

        output.AppendLine();
        output.AppendLine(Indent + "          These are suggestions, not a refusal. The comparison cannot see inside");
        output.AppendLine(Indent + "          a file, which is why CON_07.2 exists -- so it was never what caught scope");
        output.AppendLine(Indent + "          creep. What blocks is outcome-based: the build fails, tests fail, or a");
        output.AppendLine(Indent + "          criterion's proof is not met.");
    }

    private static string Count(int number, string noun) => number == 1
        ? "1 " + noun
        : string.Create(CultureInfo.InvariantCulture, $"{number} {noun}s");
}
