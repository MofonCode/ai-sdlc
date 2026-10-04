using System.Globalization;
using System.Text;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Landing;

/// <summary>
/// Renders what landing would do, and what it is waiting for.
/// </summary>
/// <remarks>
/// A pull request is reversible but outward-facing: it notifies people, it appears in a queue, and
/// withdrawing it is a thing somebody has to notice happened. So the sweep prints the whole list before it
/// pushes anything, and the list says what each cycle is -- a count would be a number a person cannot
/// check, and "land 8 cycles?" is a question nobody can answer honestly.
/// </remarks>
public static class LandRenderer
{
    private const string Indent = "  ";

    /// <summary>Renders one cycle's landability.</summary>
    /// <param name="landable">What was found.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(Land.Response landable)
    {
        ArgumentNullException.ThrowIfNull(landable);

        StringBuilder output = new();

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}land - {landable.Cycle.Id}"));

        output.AppendLine();

        foreach (string line in landable.Explanation.ReplaceLineEndings().Split(Environment.NewLine))
        {
            output.AppendLine(Indent + line);
        }

        return output.ToString();
    }

    /// <summary>
    /// Renders what a sweep would land, for the one confirmation it asks.
    /// </summary>
    /// <remarks>
    /// Every cycle appears, landable or not. A list of only the eligible ones hides the surprise that
    /// matters most -- a cycle from last week that is still sitting there verified, which the sweep will
    /// pick up because every passed cycle is eligible.
    /// </remarks>
    /// <param name="candidates">Every cycle the sweep looked at.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string RenderSweep(IReadOnlyList<Land.Response> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        StringBuilder output = new();
        List<Land.Response> landable = [.. candidates.Where(candidate => candidate.MayLand)];

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}cycles --landable - {Count(landable.Count)} of {candidates.Count} examined"));

        output.AppendLine();

        foreach (Land.Response candidate in candidates)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{(candidate.MayLand ? "land" : "skip"),-6}{candidate.Cycle.Id}  {Reason(candidate)}"));
        }

        output.AppendLine();

        if (landable.Count == 0)
        {
            output.AppendLine(Indent + "Nothing to land.");
            return output.ToString();
        }

        output.AppendLine(Indent + "Each of these opens a pull request, which notifies people. The approval");
        output.AppendLine(Indent + "already said the plan was agreed, so this is about how many rather than");
        output.AppendLine(Indent + "whether -- but every cycle that ever passed is eligible, including one");
        output.AppendLine(Indent + "from last week, which is why the whole list is above.");

        return output.ToString();
    }

    private static string Reason(Land.Response candidate) => candidate.Refusal switch
    {
        Land.LandRefusal.None => "verified, chain holds",
        Land.LandRefusal.NotVerified => "not verified yet",
        Land.LandRefusal.NotPassed => "verification did not pass",
        Land.LandRefusal.Escalated => "escalated: could not be made to pass within its budget",
        Land.LandRefusal.ChainBroken => "verified, but the chain does not hold",
        _ => "unknown",
    };

    private static string Count(int number) => number == 1
        ? "1 to land"
        : string.Create(CultureInfo.InvariantCulture, $"{number} to land");
}
