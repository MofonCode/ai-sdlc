using System.Globalization;
using System.Text;

namespace Sdlc.Core.Factory;

/// <summary>Renders the sitrep as Markdown, for a terminal and for a pipeline run's summary.</summary>
/// <remarks>
/// Ordered by what a person does first: what to merge and in what order, what needs them, what did not run
/// and why, what is waiting and going stale, and the integration branch. Every line ends in the next thing to
/// do, because the page is read at the start of a morning and acted on line by line.
/// </remarks>
public static class SitrepRenderer
{
    /// <summary>Renders the page.</summary>
    /// <param name="page">The sitrep.</param>
    /// <returns>Markdown, ending in a newline.</returns>
    public static string Render(Sitrep.Response page)
    {
        ArgumentNullException.ThrowIfNull(page);

        StringBuilder output = new();
        int built = page.Ready.Count + page.NeedsPerson.Count;

        output.AppendLine("# Sitrep");
        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{built} built: {page.Ready.Count} ready, {page.NeedsPerson.Count} needing a person | {page.Held.Count} held back | {page.Queued.Count} waiting for a build"));
        output.AppendLine();

        output.AppendLine("## Ready to merge, in this order");
        output.AppendLine();

        if (page.Ready.Count == 0)
        {
            output.AppendLine("Nothing.");
        }

        int position = 1;

        foreach (Sitrep.Built cycle in page.Ready)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{position++}. `{cycle.Id}` {cycle.Headline ?? "(no headline)"} - `{cycle.Branch}`{Files(cycle.Files)}"));

            foreach (Sitrep.Overlap overlap in page.Overlaps.Where(o => o.First == cycle.Id))
            {
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"   - also changed by `{overlap.Second}` ({string.Join(", ", overlap.Files)}) - expect it to conflict after this merges; resolve it with `resolve-conflict`"));
            }
        }

        output.AppendLine();
        output.AppendLine("## Needs a person");
        output.AppendLine();

        if (page.NeedsPerson.Count == 0)
        {
            output.AppendLine("Nothing.");
        }

        foreach (Sitrep.Built cycle in page.NeedsPerson)
        {
            string next = cycle.Outcome?.ToUpperInvariant() switch
            {
                "BLOCKED" => "blocked -> `remediate` on its branch, then mark the draft ready",
                "ESCALATED" => "escalated: the loop stopped reducing -> `steer`, a reopen, or abandon",
                null => "no verify to read -> fetch the remote, or build it again by deleting the branch",
                _ => string.Create(CultureInfo.InvariantCulture, $"outcome '{cycle.Outcome}' -> read its verify"),
            };

            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"- `{cycle.Id}` {cycle.Headline ?? "(no headline)"} - `{cycle.Branch}` - {next}"));
        }

        output.AppendLine();
        output.AppendLine("## Held back, not built");
        output.AppendLine();

        if (page.Held.Count == 0)
        {
            output.AppendLine("Nothing.");
        }

        foreach (ListQueuedCycles.HeldBack cycle in page.Held)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"- `{cycle.Id}` {cycle.Headline ?? "(no headline)"} - {cycle.Reason}"));
        }

        output.AppendLine();
        output.AppendLine("## Waiting for a build");
        output.AppendLine();

        if (page.Queued.Count == 0)
        {
            output.AppendLine("Nothing.");
        }

        foreach (Sitrep.Waiting cycle in page.Queued)
        {
            string stale = cycle.Changed.Count == 0
                ? string.Empty
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $" - **going stale**: {string.Join(", ", cycle.Changed)} changed on the integration branch since approval; read the plan again before it builds");

            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"- `{cycle.Id}` {cycle.Headline ?? "(no headline)"} - waiting {Days(cycle.Days)}{stale}"));
        }

        output.AppendLine();
        output.AppendLine("## " + page.IntegrationBranch);
        output.AppendLine();
        output.AppendLine(page.IntegrationHead is null
            ? string.Create(CultureInfo.InvariantCulture, $"There is no `{page.IntegrationBranch}` branch yet.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Head `{page.IntegrationHead}`. Whether its build is green is the platform's to say; a stable point to promote is a head whose build is green."));

        if (!page.RemoteRead)
        {
            output.AppendLine();
            output.AppendLine("The remote's branches could not be read, so nothing built is listed and nothing is held back by a branch.");
        }

        return output.ToString();
    }

    private static string Files(IReadOnlyList<string> files) => files.Count switch
    {
        0 => string.Empty,
        <= 3 => " - " + string.Join(", ", files),
        _ => string.Create(CultureInfo.InvariantCulture, $" - {string.Join(", ", files.Take(3))} and {files.Count - 3} more"),
    };

    private static string Days(int days) => days switch
    {
        0 => "under a day",
        1 => "1 day",
        _ => string.Create(CultureInfo.InvariantCulture, $"{days} days"),
    };
}
