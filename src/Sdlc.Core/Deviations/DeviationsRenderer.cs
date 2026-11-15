using System.Globalization;
using System.Text;

namespace Sdlc.Core.Deviations;

/// <summary>Renders a cycle's deviations for <c>sdlc deviations</c>: each one, where it stands, and what is wrong.</summary>
public static class DeviationsRenderer
{
    /// <summary>The deviations, as a person or the stage that wrote them reads them.</summary>
    /// <param name="response">What was read.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(CycleDeviations.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        DeviationsState state = response.State;
        StringBuilder text = new();

        text.Append(CultureInfo.InvariantCulture, $"deviations - {response.Cycle.Id}\n\n");

        if (!state.Recorded)
        {
            text.Append("None. The build followed the approved documents and recorded no choice they left open.\n");
            return text.ToString();
        }

        if (state.States.Count == 0)
        {
            text.Append(CultureInfo.InvariantCulture, $"{DeviationsFile.FileName} cannot be read:\n");
            foreach (string problem in state.Problems)
            {
                text.Append("  ").Append(problem).Append('\n');
            }

            return text.ToString();
        }

        foreach (DeviationState deviation in state.States)
        {
            text.Append("  ").Append(DeviationsFile.Line(deviation.Deviation)).Append('\n');
            text.Append("    ").Append(Standing(deviation)).Append('\n');
        }

        if (state.Problems.Count > 0)
        {
            text.Append("\nWhat is wrong, which the pull request check refuses until it is fixed:\n");
            foreach (string problem in state.Problems)
            {
                text.Append("  ").Append(problem).Append('\n');
            }
        }

        foreach (DeviationCircle circle in state.Circles)
        {
            text.Append(CultureInfo.InvariantCulture, $"\n{string.Join(", ", circle.Ids)} bend the same text in {circle.Document}. A third attempt at one place is the loop going round: if the verification still blocks, the cycle escalates to a person.\n");
        }

        if (state.InForce.Count + state.Choices.Count > 0)
        {
            text.Append("\nThe pull request leads with these, and merging it accepts them. To change one, say what to do instead and the build goes round again into the same pull request.\n");
        }

        return text.ToString();
    }

    private static string Standing(DeviationState deviation) => deviation.Standing switch
    {
        DeviationStanding.Recorded when !deviation.Deviation.Bends => "a choice the approved text left open, instead of " + deviation.Deviation.Alternative,
        DeviationStanding.Recorded => "recorded; accepted by merging the pull request",
        DeviationStanding.Accepted => "accepted",
        DeviationStanding.Declined => "changed at the person's request: " + deviation.Note,
        DeviationStanding.History => "recorded before the approval that stands; the reopen took it in",
        _ => throw new ArgumentOutOfRangeException(nameof(deviation)),
    };
}
