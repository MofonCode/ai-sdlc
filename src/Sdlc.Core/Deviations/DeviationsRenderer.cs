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
            text.Append("None. The build followed the approved documents.\n");
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
            text.Append("    ").Append(Standing(deviation, state.AcceptedBySetting)).Append('\n');
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

        if (state.Pending.Count > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $"\n{state.Pending.Count} unanswered. 'sdlc draft approval {response.Cycle.Id}' drafts the answer for a person to mark and place.\n");
        }

        return text.ToString();
    }

    private static string Standing(DeviationState deviation, bool bySetting) => deviation.Standing switch
    {
        DeviationStanding.Accepted when bySetting => "accepted by 'approval: none'",
        DeviationStanding.Accepted => "accepted",
        DeviationStanding.Declined => "declined: " + deviation.Note,
        DeviationStanding.History => "answered before the approval that stands; the reopen took it in",
        _ when deviation.Changed => "unanswered: changed since it was answered",
        _ => "unanswered",
    };
}
