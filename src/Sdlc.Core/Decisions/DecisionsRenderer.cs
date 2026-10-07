using System.Globalization;
using System.Text;

namespace Sdlc.Core.Decisions;

/// <summary>How much of each decision to render.</summary>
public enum DecisionDetail
{
    /// <summary>Id and statement only — enough to see what has been settled.</summary>
    Brief,

    /// <summary>Every clause. What a stage needs in order not to re-decide something.</summary>
    Full,
}

/// <summary>
/// Renders the decision record as text.
/// </summary>
/// <remarks>
/// The rendering is part of the contract rather than presentation, for the reason the constraint
/// renderer gives: a skill shows this output verbatim and a stage reads it, so formatting it at each
/// call site would let three hosts drift apart. The frame is ASCII so it renders in a pipeline log and
/// in a console that is not UTF-8; an entry's own text passes through verbatim, because a plan citing
/// one quotes it.
/// </remarks>
public static class DecisionsRenderer
{
    private const int Width = 92;
    private const string Indent = "    ";
    private const string ClauseIndent = "                ";

    /// <summary>
    /// Renders the record.
    /// </summary>
    /// <param name="response">What the record holds.</param>
    /// <param name="detail">How much of each entry to include.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ReadDecisions.Response response, DecisionDetail detail)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder output = new();

        // An absent document and an empty one are different states and read differently. Most
        // solutions have neither for a while, and saying "nothing is recorded" where a stage expected
        // a list is what stops somebody hunting for a broken install.
        if (!response.Recorded)
        {
            output.AppendLine("No decisions recorded.");
            output.AppendLine();
            output.AppendLine("This solution has no decision record yet. 'sdlc propose decisions' reads the");
            output.AppendLine("approved cycles and proposes entries for a person to place.");
            return output.ToString();
        }

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Decisions standing: {response.Decisions.Count(decision => decision.IsStanding)}"));

        if (response.Decisions.Count == 0)
        {
            output.AppendLine();
            output.AppendLine("The record exists and holds nothing. Nothing has been settled here that a");
            output.AppendLine("later cycle would otherwise argue again.");
            return output.ToString();
        }

        foreach (Decision decision in response.Decisions)
        {
            output.AppendLine();

            string marker = decision.IsStanding
                ? string.Empty
                : string.Create(CultureInfo.InvariantCulture, $"  (superseded by {decision.SupersededBy})");

            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{decision.Id,-9} {decision.Statement}{marker}"));

            if (detail == DecisionDetail.Brief)
            {
                continue;
            }

            Clause(output, "decided", decision.Decided);
            Clause(output, "because", decision.Because);
            Clause(output, "instead of", decision.InsteadOf);
            Clause(output, "from", decision.From);
        }

        output.AppendLine();
        output.AppendLine("A plan may depart from any of these. Nothing here forbids anything -- what the");
        output.AppendLine("record buys is that a departure is taken knowingly rather than by forgetting the");
        output.AppendLine("argument was already had. Where a decision needs to bind, that half is a");
        output.AppendLine("constraint or an architecture test.");

        if (response.Superseded > 0 && detail == DecisionDetail.Brief)
        {
            output.AppendLine();
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{response.Superseded} reversed and kept on the page. 'sdlc decisions --all' shows them."));
        }

        return output.ToString();
    }

    private static void Clause(StringBuilder output, string label, string body)
    {
        bool first = true;

        foreach (string line in Wrap(body, Width - ClauseIndent.Length))
        {
            if (first)
            {
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Indent}{label,-12}{line}"));
                first = false;
            }
            else
            {
                output.AppendLine(ClauseIndent + line);
            }
        }
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        StringBuilder line = new();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
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
            yield return line.ToString();
        }
    }
}
