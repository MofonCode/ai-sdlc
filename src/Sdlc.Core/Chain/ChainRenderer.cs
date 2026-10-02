using System.Globalization;
using System.Text;

namespace Sdlc.Core.Chain;

/// <summary>
/// Renders the chain check, for a pipeline log and for a person reading one.
/// </summary>
/// <remarks>
/// The audience is somebody looking at a failed pull-request check, usually in a web view, usually
/// without the repository in front of them. So every line says what was checked and what it found, the
/// failures are repeated at the end where the log is truncated from the top, and the glyphs are ASCII
/// because a pipeline log is not reliably UTF-8.
/// </remarks>
public static class ChainRenderer
{
    private const string Indent = "  ";

    /// <summary>
    /// Renders a verdict.
    /// </summary>
    /// <param name="verdict">What the chain check found.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ChainVerdict verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        StringBuilder output = new();

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}chain - {verdict.Cycle.Id}"));
        output.AppendLine();

        foreach (ChainCheck check in verdict.Checks)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}[{(check.Passed ? "ok" : "no")}]  {check.Name}"));

            foreach (string line in check.Detail.ReplaceLineEndings().Split(Environment.NewLine))
            {
                output.AppendLine(Indent + "      " + line);
            }

            output.AppendLine();
        }

        if (verdict.Holds)
        {
            output.AppendLine(Indent + "The chain holds.");
            output.AppendLine();

            // Stated on a pass rather than only on a failure. A team reads this line when the check is
            // green, which is the moment they are deciding how much it proved.
            foreach (string line in Wrap(VerifyChain.Caveat))
            {
                output.AppendLine(Indent + line);
            }

            return output.ToString();
        }

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}The chain does not hold. {Count(verdict.Failures.Count)}:"));

        output.AppendLine();

        foreach (ChainCheck failure in verdict.Failures)
        {
            output.AppendLine(Indent + "  - " + failure.Name);
        }

        return output.ToString();
    }

    private static string Count(int failures) => failures == 1
        ? "1 check failed"
        : string.Create(CultureInfo.InvariantCulture, $"{failures} checks failed");

    private static List<string> Wrap(string text)
    {
        const int width = 88;

        List<string> lines = [];
        StringBuilder line = new();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }

        return lines;
    }
}
