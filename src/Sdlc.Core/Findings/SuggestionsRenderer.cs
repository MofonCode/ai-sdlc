using System.Globalization;
using System.Text;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>
/// Renders the accumulated suggestions, grouped by cycle.
/// </summary>
/// <remarks>
/// Grouped rather than flat, because a reader scanning this is deciding what to pick up next and the
/// cycle is how they recognise work. No count of how long one has been open and no ranking: a
/// suggestion's age says nothing about its worth, and a tool that sorted by age would quietly argue
/// that the oldest matters most.
/// </remarks>
public static class SuggestionsRenderer
{
    private const string Indent = "  ";

    /// <summary>
    /// Renders a list of suggestions.
    /// </summary>
    /// <param name="suggestions">What was found, in any order.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(Suggestions.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        IReadOnlyList<Suggestion> suggestions = response.Suggestions;

        StringBuilder output = new();

        if (suggestions.Count == 0 && response.Noticed.Count == 0)
        {
            output.AppendLine(Indent + "No open suggestions.");
            return output.ToString();
        }

        if (suggestions.Count == 0)
        {
            output.AppendLine(Indent + "No open suggestions.");
            Noticed(output, response);
            return output.ToString();
        }

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}{Count(suggestions.Count)}, across {Cycles(suggestions)}"));

        foreach (IGrouping<CycleId, Suggestion> group in suggestions
            .GroupBy(suggestion => suggestion.Cycle)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal))
        {
            output.AppendLine();
            output.AppendLine(Indent + group.Key.ToString());

            foreach (Suggestion suggestion in group
                .OrderBy(s => s.Artifact)
                .ThenBy(s => s.Finding.Id, StringComparer.Ordinal))
            {
                string cites = suggestion.Finding.Cites?.Id is { } id
                    ? "  " + id
                    : string.Empty;

                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Indent}    {Cycle.NameOf(suggestion.Artifact),-8}r{suggestion.Round}  {suggestion.Finding.Id}{cites}"));

                // Most suggestions cite nothing, so this line is usually absent -- which is what makes
                // it worth printing when it is there. A suggestion that does rest on a rule is a
                // different kind of thing from one that is simply an improvement, and the id alone does
                // not say which rule somebody would be choosing not to act on.
                if (suggestion.Finding.Cites?.Line() is { } rule)
                {
                    output.AppendLine(Indent + "        " + rule);
                }

                output.AppendLine(Indent + "        " + suggestion.Finding.Statement);
            }
        }

        Noticed(output, response);

        return output.ToString();
    }

    /// <summary>
    /// Writes what the builds noticed and left alone, under its own heading.
    /// </summary>
    /// <remarks>
    /// Apart from the suggestions rather than mixed in, because the two were raised by different readers
    /// and only one of them has a round and an id. Mixing them would make a build's observation look like
    /// a finding nobody dispositioned.
    /// </remarks>
    private static void Noticed(StringBuilder output, Suggestions.Response response)
    {
        if (response.Noticed.Count == 0)
        {
            return;
        }

        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}{Noticed(response.Noticed.Count)}, from the builds' commits"));

        foreach (IGrouping<CycleId, Noticed> group in response.Noticed
            .GroupBy(noticed => noticed.Cycle)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal))
        {
            output.AppendLine();
            output.AppendLine(Indent + group.Key.ToString());

            foreach (Noticed noticed in group)
            {
                output.AppendLine(Indent + "        " + noticed.Statement);
            }
        }
    }

    private static string Noticed(int number) => number == 1
        ? "1 thing noticed"
        : string.Create(CultureInfo.InvariantCulture, $"{number} things noticed");

    private static string Count(int number) => number == 1
        ? "1 open suggestion"
        : string.Create(CultureInfo.InvariantCulture, $"{number} open suggestions");

    private static string Cycles(IReadOnlyList<Suggestion> suggestions)
    {
        int cycles = suggestions.Select(suggestion => suggestion.Cycle).Distinct().Count();

        return cycles == 1
            ? "1 cycle"
            : string.Create(CultureInfo.InvariantCulture, $"{cycles} cycles");
    }
}
