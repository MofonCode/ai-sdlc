using System.Globalization;
using System.Text;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.PullRequests;

/// <summary>
/// Renders what opening a pull request would do, and what it is waiting for.
/// </summary>
/// <remarks>
/// A pull request is reversible but outward-facing: it notifies people, it appears in a queue, and
/// withdrawing it is a thing somebody has to notice happened. So the sweep prints the whole list before it
/// pushes anything, and the list says what each cycle is -- a count would be a number a person cannot
/// check, and "open 8 pull requests?" is a question nobody can answer honestly.
/// </remarks>
public static class PullRequestRenderer
{
    private const string Indent = "  ";

    /// <summary>Renders whether one cycle may open a pull request.</summary>
    /// <param name="ready">What was found.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(PullRequest.Response ready)
    {
        ArgumentNullException.ThrowIfNull(ready);

        StringBuilder output = new();

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}pr - {ready.Cycle.Id}"));

        output.AppendLine();

        foreach (string line in ready.Explanation.ReplaceLineEndings().Split(Environment.NewLine))
        {
            output.AppendLine(Indent + line);
        }

        return output.ToString();
    }

    /// <summary>
    /// Renders what a sweep would open, for the one confirmation it asks.
    /// </summary>
    /// <remarks>
    /// Every cycle appears, ready or not. A list of only the eligible ones hides the surprise that
    /// matters most -- a cycle from last week that is still sitting there verified, which the sweep will
    /// pick up because every passed cycle is eligible.
    /// </remarks>
    /// <param name="candidates">Every cycle the sweep looked at.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string RenderSweep(IReadOnlyList<PullRequest.Response> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        StringBuilder output = new();
        List<PullRequest.Response> ready = [.. candidates.Where(candidate => candidate.MayOpen)];

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}cycles --pr-ready - {Count(ready.Count)} of {candidates.Count} examined"));

        output.AppendLine();

        foreach (PullRequest.Response candidate in candidates)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{(candidate.MayOpen ? "pr" : "skip"),-6}{candidate.Cycle.Id}  {Reason(candidate)}"));
        }

        output.AppendLine();

        if (ready.Count == 0)
        {
            output.AppendLine(Indent + "Nothing ready for a pull request.");
            return output.ToString();
        }

        output.AppendLine(Indent + "Each of these opens a pull request, which notifies people. The approval");
        output.AppendLine(Indent + "already said the plan was agreed, so this is about how many rather than");
        output.AppendLine(Indent + "whether -- but every cycle that ever passed is eligible, including one");
        output.AppendLine(Indent + "from last week, which is why the whole list is above.");

        return output.ToString();
    }

    private static string Reason(PullRequest.Response candidate) => candidate.Refusal switch
    {
        PullRequest.PullRequestRefusal.None => "verified, chain holds",
        PullRequest.PullRequestRefusal.NotVerified => "not verified yet",
        PullRequest.PullRequestRefusal.NotPassed => "verification did not pass",
        PullRequest.PullRequestRefusal.Escalated => "escalated: could not be made to pass within its budget",
        PullRequest.PullRequestRefusal.ChainBroken => "verified, but the chain does not hold",
        _ => "unknown",
    };

    private static string Count(int number) => number == 1
        ? "1 ready"
        : string.Create(CultureInfo.InvariantCulture, $"{number} ready");
}
