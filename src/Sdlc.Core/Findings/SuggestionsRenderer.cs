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
    public static string Render(IReadOnlyList<Suggestion> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);

        StringBuilder output = new();

        if (suggestions.Count == 0)
        {
            output.AppendLine(Indent + "No open suggestions.");
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
                string cites = suggestion.Finding.Cites is { } constraint
                    ? "  " + constraint
                    : string.Empty;

                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Indent}    {Cycle.NameOf(suggestion.Artifact),-8}r{suggestion.Round}  {suggestion.Finding.Id}{cites}"));

                output.AppendLine(Indent + "        " + suggestion.Finding.Statement);
            }
        }

        return output.ToString();
    }

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
