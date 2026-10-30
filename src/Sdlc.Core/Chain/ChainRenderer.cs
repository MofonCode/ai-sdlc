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
    /// Renders the refusal for a branch that carries no governed cycle.
    /// </summary>
    /// <remarks>
    /// <strong>A governance verdict, not a usage complaint, and the difference is what F-28 was
    /// about.</strong> The generated pipeline runs on every pull request and hands this command
    /// <c>github.head_ref</c>, so the argument is whatever branch somebody opened — a dependabot bump,
    /// a revert, a hotfix. Those used to be reported as a malformed argument, which on a bot's pull
    /// request reads as a misconfigured tool rather than as a decision, and the obvious response to a
    /// misconfigured tool is to delete the step that is complaining. That step is the only layer
    /// nobody can bypass.
    /// <para>
    /// So this says what actually happened and names both ways out, because there are two and the
    /// second one is a decision a team is allowed to make. What it must not do is read as a fault.
    /// </para>
    /// </remarks>
    /// <param name="reference">The branch or id the pipeline handed in.</param>
    /// <returns>The rendered refusal, ending in a newline.</returns>
    public static string RenderUngoverned(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        StringBuilder output = new();

        output.AppendLine();
        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}chain - {reference}"));
        output.AppendLine();
        output.AppendLine(Indent + "  [no]  this branch is a governed cycle");
        output.AppendLine(Indent + "        It is not a cycle branch - cycle/<id>/plan, build, build-unattended or");
        output.AppendLine(Indent + "        forward - so there is no intent, spec or plan");
        output.AppendLine(Indent + "        behind it and nothing has verified this change. A cycle id is a");
        output.AppendLine(Indent + "        date, a 24-hour time and seconds, as in 20260929_1456_32.");
        output.AppendLine();
        output.AppendLine(Indent + "The chain does not hold, because there is no chain.");
        output.AppendLine();
        output.AppendLine(Indent + "Two ways on, and they are not the same thing:");
        output.AppendLine();
        output.AppendLine(Indent + "  Open the change as a cycle. Every class of change runs the full chain,");
        output.AppendLine(Indent + "  including a dependency bump and a revert, and a small change makes a");
        output.AppendLine(Indent + "  short intent, spec and plan rather than none.");
        output.AppendLine();
        output.AppendLine(Indent + "  Or decide, as a team, that this class of branch is exempt - and say so");
        output.AppendLine(Indent + "  in branch policy by not requiring this check on it. That is a choice to");
        output.AppendLine(Indent + "  accept ungoverned changes of that kind, which is yours to make. It is");
        output.AppendLine(Indent + "  not the same as deleting this step, which accepts them everywhere and");
        output.AppendLine(Indent + "  leaves nothing saying so.");
        output.AppendLine();

        return output.ToString();
    }

    /// <summary>
    /// Renders a verdict.
    /// </summary>
    /// <param name="verdict">What the chain check found.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(VerifyChain.Response verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        StringBuilder output = new();

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}chain - {verdict.Cycle.Id}"));
        output.AppendLine();

        foreach (VerifyChain.ChainCheck check in verdict.Checks)
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

        if (verdict.Holds && verdict.Admission)
        {
            output.AppendLine(Indent + "The plan may be admitted. Nothing is built yet; its build gets the full chain.");
            output.AppendLine();

            foreach (string line in Wrap(VerifyChain.Caveat))
            {
                output.AppendLine(Indent + line);
            }

            return output.ToString();
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

        foreach (VerifyChain.ChainCheck failure in verdict.Failures)
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
