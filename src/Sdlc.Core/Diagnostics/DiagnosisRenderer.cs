using System.Globalization;
using System.Text;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Diagnostics;

/// <summary>
/// Renders what <see cref="Diagnose"/> found.
/// </summary>
/// <remarks>
/// A static pure function from a response to a string, like every other renderer here, and ASCII for
/// the reason <c>StatusRenderer</c> gives: this is read in a pipeline log and in a console that is not
/// UTF-8, which is exactly where somebody is when an install has gone wrong.
/// <para>
/// The remedies are indented under their checks rather than put in a second list, because a person
/// reading this is looking for one line and then the command that answers it.
/// </para>
/// </remarks>
public static class DiagnosisRenderer
{
    private const int SubjectWidth = 20;

    /// <summary>
    /// Renders the panel.
    /// </summary>
    /// <param name="response">What was found.</param>
    /// <returns>The text to print.</returns>
    public static string Render(Diagnose.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var output = new StringBuilder();
        int problems = response.Checks.Count(check => check.Outcome == CheckOutcome.Problem);

        output.Append("  sdlc doctor");
        output.AppendLine(Headline(problems).PadLeft(66 - "  sdlc doctor".Length));
        output.AppendLine();

        foreach (Check check in response.Checks)
        {
            output.Append("  ");
            output.Append(Marker(check.Outcome).PadRight(11));
            output.Append(check.Subject.PadRight(SubjectWidth));
            output.AppendLine(check.Detail);

            if (check.Remedy is { Length: > 0 } remedy)
            {
                output.Append(new string(' ', 13 + SubjectWidth));
                output.Append("-> ");
                output.AppendLine(remedy);
            }
        }

        output.AppendLine();

        // The exit code is the thing a script reads, so the panel says which it will be rather than
        // leaving a reader to infer it from the markers.
        output.AppendLine(response.Sound
            ? "  Nothing here is wrong. What is marked 'elsewhere' is somebody else's to confirm."
            : "  Exits 1. Every problem above names what fixes it.");

        // The same block adoption ends with, from the same list. A missing declared stack is a
        // problem above as well -- that is the fault; this is the order to do things in.
        if (response.Setup is { } setup
            && Setup.Render(setup, "Run 'sdlc doctor' again once it is placed, for the step after it.") is { Count: > 0 } next)
        {
            output.AppendLine();

            foreach (string line in next)
            {
                output.AppendLine(line);
            }
        }

        return output.ToString();
    }

    private static string Headline(int problems) => problems switch
    {
        0 => "no problems",
        1 => "1 problem",
        _ => string.Create(CultureInfo.InvariantCulture, $"{problems} problems"),
    };

    private static string Marker(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.Sound => "ok",
        CheckOutcome.Problem => "problem",
        CheckOutcome.Elsewhere => "elsewhere",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown outcome."),
    };
}
