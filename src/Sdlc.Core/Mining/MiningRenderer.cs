using System.Globalization;
using System.Text;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Mining;

/// <summary>Renders a recorded sitting, and the ledger's history.</summary>
public static class MiningRenderer
{
    /// <summary>What was recorded, and the removal a person runs.</summary>
    /// <param name="response">The sitting.</param>
    /// <returns>The text.</returns>
    public static string RenderRecorded(RecordMining.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Recorded {response.Recorded.Count} mined cycle{(response.Recorded.Count == 1 ? string.Empty : "s")} in .sdlc/{MinedLedger.FileName}; {response.Watched} concern{(response.Watched == 1 ? string.Empty : "s")} watched."));

        foreach (MinedCycle cycle in response.Recorded)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {cycle.Cycle}  husk -- merged at {cycle.Merge?[..Math.Min(12, cycle.Merge.Length)]}{(cycle.Abandoned ? ", abandoned" : string.Empty)}"));
        }

        foreach (CycleId cycle in response.Unmerged)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {cycle}  finished, not on {response.IntegrationBranch} yet -- mined again at the next sitting after it merges"));
        }

        if (response.Unfinished.Count > 0)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {response.Unfinished.Count} cycle{(response.Unfinished.Count == 1 ? string.Empty : "s")} still in flight, left alone"));
        }

        // A concern ignored in enough cycles is a pattern, and the next sitting has to answer it (D-153).
        foreach (Watched ignored in (response.Ignored ?? []).Where(ignored => ignored.Cycles.Count >= Proposals.ProposeConstraints.Bar))
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  ignored in {ignored.Cycles.Count} cycles, at the bar: {ignored.Gist} -- the next sitting proposes it, makes it a successor, or declines it"));
        }

        if (response.Recorded.Count == 0)
        {
            return text.ToString();
        }

        text.AppendLine();
        text.AppendLine("Each husk's folder may go: everything in it is in git at the commit named, and what later mining");
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"needs is in the ledger. Delete them on a branch of their own and open a pull request into {response.IntegrationBranch};"));
        text.AppendLine("the check admits it as mining when it carries only the records, the ledger and these deletions:");
        text.AppendLine();
        text.AppendLine("    git switch -c mining/" + response.Recorded[0].Mined.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        foreach (MinedCycle cycle in response.Recorded)
        {
            text.AppendLine("    git rm -r -q " + Cycle.SdlcDirectoryName + "/" + Cycle.CyclesDirectoryName + "/" + cycle.Cycle);
        }

        text.AppendLine("    git add " + Cycle.SdlcDirectoryName + "/" + MinedLedger.FileName);
        text.AppendLine("    git commit -F <a message in a file>");
        return text.ToString();
    }

    /// <summary>The ledger, for a person: each mined cycle, each sitting, and what is watched.</summary>
    /// <param name="ledger">The ledger.</param>
    /// <returns>The text.</returns>
    public static string RenderHistory(MinedLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        StringBuilder text = new();

        if (ledger.Cycles.Count == 0 && ledger.Runs.Count == 0)
        {
            text.AppendLine("Nothing mined yet. '/sdlc:mine' reads the cycles; 'sdlc mine record <file>' records a sitting.");
            return text.ToString();
        }

        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Mined: {ledger.Cycles.Count} cycles in {ledger.Runs.Count} sittings."));

        foreach (MinedCycle cycle in ledger.Cycles)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {cycle.Cycle}  {(cycle.Abandoned ? "abandoned" : "passed")}, merged at {cycle.Merge}, rounds {string.Join(", ", cycle.Rounds.Select(round => round.Key + " " + round.Value))}, {cycle.Reopens} reopen{(cycle.Reopens == 1 ? string.Empty : "s")}"));
        }

        foreach (MiningRun run in ledger.Runs)
        {
            text.AppendLine();
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {run.Mined:yyyy-MM-dd}: {run.Cycles.Count} cycles, {run.Accepted.Count} accepted, {run.Declined.Count} suggestions declined"));

            foreach (string accepted in run.Accepted)
            {
                text.AppendLine("    accepted " + accepted);
            }

            foreach (MinedDecline decline in run.Declined)
            {
                text.AppendLine("    declined " + decline.Id + ": " + decline.Reason);
            }
        }

        if (ledger.Watch.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Watched, below the bar of three cycles:");

            foreach (Watched watched in ledger.Watch)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {watched.Cycles.Count} of 3 -- {watched.Gist} ({string.Join(", ", watched.Cycles)})"));
            }
        }

        if (ledger.Ignored is { Count: > 0 } ignored)
        {
            text.AppendLine();
            text.AppendLine("Suggestions ignored, by concern:");

            foreach (Watched concern in ignored)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {concern.Cycles.Count} of {Proposals.ProposeConstraints.Bar} -- {concern.Gist} ({string.Join(", ", concern.Cycles)})"));
            }
        }

        return text.ToString();
    }
}
