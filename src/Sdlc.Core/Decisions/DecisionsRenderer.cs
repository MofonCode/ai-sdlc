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

    /// <summary>
    /// Id, statement and <c>Instead of</c>: what a reader judges a contradiction against (D-72), at about a
    /// third of the size, so a long record still fits beside the document it judges (D-139).
    /// </summary>
    Judged,
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

            if (detail == DecisionDetail.Judged)
            {
                Clause(output, "instead of", decision.InsteadOf);
                continue;
            }

            Clause(output, "decided", decision.Decided);
            Clause(output, "because", decision.Because);
            Clause(output, "instead of", decision.InsteadOf);
            Clause(output, "from", decision.From);
        }

        // A reader is told how to judge the record in its own instructions; this paragraph is for a writer.
        if (detail == DecisionDetail.Judged)
        {
            return output.ToString();
        }

        output.AppendLine();
        output.AppendLine("A cycle may depart from any of these, and CON_24 asks it to say so where the");
        output.AppendLine("departure arises: an outcome in the intent, under Fixed decisions; an approach in");
        output.AppendLine("the plan, under Shape and departures. Write it as one line, '- Departs from DEC_nn");
        output.AppendLine("(the decision in brief): what this cycle does instead', with who and why. Departing");
        output.AppendLine("is legal; departing in silence is not. The spec, the plan and the code are each read");
        output.AppendLine("against this record, and rely on a departure declared above them.");

        // Information, never a refusal: the file is a person's, and mining proposes the rewording (D-149).
        List<string> overBudget = [.. response.Decisions
            .Where(decision => decision.IsStanding)
            .Select(decision => (decision.Id, Overruns: EntryBudget.Of(decision)))
            .Where(entry => entry.Overruns.Count > 0)
            .Select(entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Id} ({EntryBudget.Describe(entry.Overruns)})"))];

        if (overBudget.Count > 0)
        {
            output.AppendLine();
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Over the word budget -- a {EntryBudget.Headline}-word title, {EntryBudget.Clause} words a clause -- so a reader"));
            output.AppendLine("handed only the headline gets less than it should: " + string.Join("; ", overBudget) + ".");
            output.AppendLine("'/sdlc:mine' proposes rewording them; nothing is refused.");
        }

        if (response.Superseded > 0 && detail == DecisionDetail.Brief)
        {
            output.AppendLine();
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{response.Superseded} reversed and kept on the page. 'sdlc decisions --all' shows them."));
        }

        return output.ToString();
    }

    /// <summary>
    /// Renders one entry in full: what a reader handed only its headline asks for (D-149).
    /// </summary>
    /// <param name="decision">The entry, standing or superseded.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string RenderOne(Decision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        StringBuilder output = new();
        string marker = decision.IsStanding
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"  (superseded by {decision.SupersededBy})");

        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{decision.Id} {decision.Statement}{marker}"));
        Clause(output, "decided", decision.Decided);
        Clause(output, "because", decision.Because);
        Clause(output, "instead of", decision.InsteadOf);
        Clause(output, "from", decision.From);

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
