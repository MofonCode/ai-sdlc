using System.Globalization;
using System.Text;
using Sdlc.Core.Approvals;

namespace Sdlc.Core.Cycles;

/// <summary>
/// Renders every cycle a solution holds, newest first.
/// </summary>
/// <remarks>
/// Two lines per cycle rather than one: the state has to be scannable in a column, and the intent's
/// heading is the only part that says what the work <em>is</em> -- truncating it into a column would
/// make the one human-readable field the one nobody can read.
/// </remarks>
public static class CyclesRenderer
{
    private const string Indent = "  ";

    /// <summary>
    /// Renders a cycle listing.
    /// </summary>
    /// <param name="response">What matched.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ListCycles.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder output = new();

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}cycles - {Count(response.Cycles.Count, "shown")} of {response.Examined} examined"));

        if (response.Cycles.Count == 0)
        {
            output.AppendLine();
            output.AppendLine(response.Examined == 0
                ? Indent + "This solution holds no cycles. 'sdlc new' opens one."
                : Indent + "No cycle matches. Without a filter, every cycle is listed.");

            return output.ToString();
        }

        foreach (CycleSummary summary in response.Cycles)
        {
            output.AppendLine();

            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{summary.Cycle.Id}  {Cycle.NameOf(summary.Reached),-7} {Rounds(summary),-9} blk {summary.Blockers}  sug {summary.Suggestions}  {State(summary)}"));

            output.AppendLine(Indent + "    " + (summary.Headline ?? "(no intent heading yet)"));
        }

        return output.ToString();
    }

    /// <summary>
    /// Where the cycle stands, in the one phrase that decides what happens to it next.
    /// </summary>
    /// <remarks>
    /// Abandonment first, because it is terminal and outranks everything else a cycle could also be --
    /// an abandoned cycle holding an approval will never build, and reporting it as approved would be
    /// true and useless.
    /// </remarks>
    private static string State(CycleSummary summary)
    {
        if (summary.Abandoned)
        {
            return "abandoned";
        }

        if (summary.Outcome is { } outcome)
        {
            return string.Create(CultureInfo.InvariantCulture, $"verify {outcome}");
        }

        return summary.Gate switch
        {
            ApprovalGate.GateRefusal.None => "approved",
            ApprovalGate.GateRefusal.NotApproved => "not approved",
            ApprovalGate.GateRefusal.UnreadableLedger => "ledger unreadable",
            ApprovalGate.GateRefusal.OpenBlocker => "blocked",
            ApprovalGate.GateRefusal.HashMismatch => "stale hash",
            ApprovalGate.GateRefusal.StaleRound => "stale round",
            ApprovalGate.GateRefusal.Uninterrogated => "not interrogated",
            ApprovalGate.GateRefusal.MissingDocument => "incomplete",
            _ => "refused",
        };
    }

    private static string Rounds(CycleSummary summary) => summary.Rounds == 0
        ? "r0"
        : string.Create(CultureInfo.InvariantCulture, $"r{summary.Rounds}");

    private static string Count(int number, string suffix) => number == 1
        ? string.Create(CultureInfo.InvariantCulture, $"1 cycle {suffix}")
        : string.Create(CultureInfo.InvariantCulture, $"{number} cycles {suffix}");
}
