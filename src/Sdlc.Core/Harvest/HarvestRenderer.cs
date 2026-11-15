using System.Globalization;
using System.Text;
using Sdlc.Core.Proposals;

namespace Sdlc.Core.Harvest;

/// <summary>Renders a recorded sitting, a close, and the ledger's history.</summary>
public static class HarvestRenderer
{
    /// <summary>What one aspect's sitting recorded, and what is left.</summary>
    /// <param name="response">The sitting.</param>
    /// <returns>The text.</returns>
    public static string RenderRecorded(RecordHarvest.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Recorded {HarvestAspects.NameOf(response.Aspect)} for {response.Recorded.Count} cycle{(response.Recorded.Count == 1 ? string.Empty : "s")}: {string.Join(", ", response.Recorded)}."));

        // A concern ignored in enough cycles is a pattern, and the next sitting has to answer it (D-153).
        foreach (Watched ignored in response.Ignored.Where(ignored => ignored.Cycles.Count >= ProposeConstraints.Bar))
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  ignored in {ignored.Cycles.Count} cycles, at the bar: {ignored.Gist} -- the next sitting makes it a successor or declines it"));
        }

        Remaining(text, response.Open);
        return text.ToString();
    }

    /// <summary>What closed, and the push and pull request that are left.</summary>
    /// <param name="response">The close.</param>
    /// <returns>The text.</returns>
    public static string RenderClosed(CloseHarvest.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Closed {response.Closed.Count} cycle{(response.Closed.Count == 1 ? string.Empty : "s")} on {response.Branch}: {string.Join(", ", response.Closed.Select(cycle => cycle.Cycle))}."));
        text.AppendLine("Their folders are removed in that commit, with the ledger and the records you placed. Each folder is still");
        text.AppendLine("in git at the merge the ledger names.");

        foreach (HarvestReport.OpenCycle open in response.Waiting)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  stays open: {open.Cycle} still needs {string.Join(", ", open.Missing.Select(HarvestAspects.NameOf))}"));
        }

        text.AppendLine();
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Next: push {response.Branch} and open its pull request into {response.IntegrationBranch}:"));
        text.AppendLine();
        text.AppendLine("    git push -u origin " + response.Branch);
        text.AppendLine();
        text.AppendLine("The check admits it as a harvest: it carries only the records, the ledger, successors and these removals.");
        return text.ToString();
    }

    /// <summary>The ledger, for a person: each closed cycle, each sitting, and what is watched.</summary>
    /// <param name="ledger">The ledger.</param>
    /// <returns>The text.</returns>
    public static string RenderHistory(HarvestLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        StringBuilder text = new();

        if (ledger.Cycles.Count == 0 && ledger.Sittings.Count == 0)
        {
            text.AppendLine("Nothing harvested yet. '/sdlc:harvest' reads the completed cycles one aspect at a time.");
            return text.ToString();
        }

        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Harvested: {ledger.Cycles.Count} cycles closed, {ledger.Sittings.Count} sittings."));

        foreach (HarvestedCycle cycle in ledger.Cycles)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {cycle.Cycle}  {(cycle.Abandoned ? "abandoned" : "passed")}, merged at {cycle.Merge}, closed {cycle.Closed:yyyy-MM-dd}, rounds {string.Join(", ", cycle.Rounds.Select(round => round.Key + " " + round.Value))}, {cycle.Reopens} reopen{(cycle.Reopens == 1 ? string.Empty : "s")}"));
        }

        foreach (HarvestSitting sitting in ledger.Sittings)
        {
            text.AppendLine();
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {sitting.Recorded:yyyy-MM-dd} {HarvestAspects.NameOf(sitting.Aspect)}: {sitting.Cycles.Count} cycles, {sitting.Accepted.Count} accepted, {sitting.Declined.Count} suggestions declined"));

            foreach (string accepted in sitting.Accepted)
            {
                text.AppendLine("    accepted " + accepted);
            }

            foreach (HarvestDecline decline in sitting.Declined)
            {
                text.AppendLine("    declined " + decline.Id + ": " + decline.Reason);
            }
        }

        if (ledger.Watch.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Watched, below the bar of {ProposeConstraints.Bar} cycles:"));

            foreach (Watched watched in ledger.Watch)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {watched.Cycles.Count} of {ProposeConstraints.Bar} -- {watched.Gist} ({string.Join(", ", watched.Cycles)})"));
            }
        }

        if (ledger.Ignored is { Count: > 0 } ignored)
        {
            text.AppendLine();
            text.AppendLine("Suggestions ignored, by concern:");

            foreach (Watched concern in ignored)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {concern.Cycles.Count} of {ProposeConstraints.Bar} -- {concern.Gist} ({string.Join(", ", concern.Cycles)})"));
            }
        }

        return text.ToString();
    }

    private static void Remaining(StringBuilder text, IReadOnlyList<HarvestReport.OpenCycle> open)
    {
        List<HarvestReport.OpenCycle> ready = [.. open.Where(cycle => cycle.Missing.Count == 0)];

        foreach (HarvestReport.OpenCycle cycle in open.Where(cycle => cycle.Missing.Count > 0))
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {cycle.Cycle} still needs {string.Join(", ", cycle.Missing.Select(HarvestAspects.NameOf))}"));
        }

        if (ready.Count > 0)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  ready to close: {string.Join(", ", ready.Select(cycle => cycle.Cycle))} -- 'sdlc harvest close'"));
        }
    }
}
