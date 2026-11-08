using System.Globalization;
using System.Text;
using Sdlc.Core.Approvals;
using Sdlc.Core.Solutions;

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

        if (page.Approval == ApprovalRequirement.None)
        {
            output.AppendLine("No person approves a plan here: solution.md declares 'approval: none'. Whoever reviews a pull request below is the first person to read its plan.");
            output.AppendLine();
        }

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
            string next = cycle.Amended
                ? string.Create(CultureInfo.InvariantCulture, $"built before its plan was amended, so its verify cannot pass -> {AmendedPlan.Choices(cycle.Branch, page.IntegrationBranch)}")
                : Next(cycle);

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

        return Tail(page, output);
    }

    private static string Next(Sitrep.Built cycle) => cycle.Outcome?.ToUpperInvariant() switch
    {
        "BLOCKED" => "blocked -> `remediate` on its branch, then mark the draft ready",
        "ESCALATED" => "escalated: the loop stopped reducing -> `steer`, a reopen, or abandon",
        null => "no verify to read -> fetch the remote, or build it again by deleting the branch",
        _ => string.Create(CultureInfo.InvariantCulture, $"outcome '{cycle.Outcome}' -> read its verify"),
    };

    private static string Tail(Sitrep.Response page, StringBuilder output)
    {
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

        Backlog(output, page);

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

    /// <summary>
    /// The backlog: every successor and parked cycle this checkout holds — ready, still waiting, or stuck — and what
    /// the page cannot see (D-125, D-145).
    /// </summary>
    /// <remarks>
    /// Always shown, empty or not, so an absent section never reads as an empty backlog. It ends by naming what is
    /// not on the page: cycles still being planned in a sandbox are on no branch this reads until they are admitted.
    /// </remarks>
    private static void Backlog(StringBuilder output, Sitrep.Response page)
    {
        output.AppendLine();
        output.AppendLine("## Backlog");
        output.AppendLine();

        if (page.Backlog is not { } backlog)
        {
            output.AppendLine("The successors and parked cycles could not be read; `sdlc backlog` says why.");
            return;
        }

        List<(string Line, Successors.SuccessorState State)> items =
        [
            .. backlog.Successors.Select(s => (Successor(s), s.State)),
            .. backlog.Waiting.Select(p => (Parked(p), p.State)),
        ];

        if (items.Count == 0)
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Nothing waits here: no successor, and no parked cycle on {page.IntegrationBranch}."));
        }

        Group(output, "**Ready now** — what the merges made ready. `/sdlc:next` takes all of it; the factory starts none.", items, Successors.SuccessorState.Ready);
        Group(output, "**Still waiting**", items, Successors.SuccessorState.Waiting);
        Group(output, "**No merge makes these ready**", items, Successors.SuccessorState.Stuck, Successors.SuccessorState.Unresolved);

        output.AppendLine();
        output.AppendLine(
            "Not on this page: cycles still being planned in a sandbox, and anything sliced or parked there, are on no branch "
            + "this reads until they are admitted — `sdlc backlog` in the sandbox shows them. The waves in full: `sdlc backlog`.");
    }

    private static void Group(StringBuilder output, string heading, List<(string Line, Successors.SuccessorState State)> items, params Successors.SuccessorState[] states)
    {
        string[] lines = [.. items.Where(i => states.Contains(i.State)).Select(i => i.Line)];

        if (lines.Length == 0)
        {
            return;
        }

        output.AppendLine(heading);
        output.AppendLine();

        foreach (string line in lines)
        {
            output.AppendLine(line);
        }

        output.AppendLine();
    }

    private static string Successor(Successors.ReadSuccessors.Standing successor) => successor.State switch
    {
        Successors.SuccessorState.Ready => string.Create(
            CultureInfo.InvariantCulture,
            $"- `successor:{successor.Successor.Slug}` {successor.Successor.Headline ?? string.Empty} - **ready**: everything it succeeds has merged. Start it with `/sdlc:intent --from successor:{successor.Successor.Slug}`"),
        Successors.SuccessorState.Waiting => string.Create(
            CultureInfo.InvariantCulture,
            $"- `successor:{successor.Successor.Slug}` {successor.Successor.Headline ?? string.Empty} - waits on {string.Join(", ", successor.Waits.Select(Successors.ReadSuccessors.Describe))}"),
        Successors.SuccessorState.Stuck => string.Create(
            CultureInfo.InvariantCulture,
            $"- `successor:{successor.Successor.Slug}` {successor.Successor.Headline ?? string.Empty} - **stuck**: {string.Join(", ", successor.Waits.Select(Successors.ReadSuccessors.Describe))}. Correct its `succeeds:`, or delete it"),
        _ => string.Create(
            CultureInfo.InvariantCulture,
            $"- `successor:{successor.Successor.Slug}` {successor.Successor.Headline ?? string.Empty} - **unresolved**: {string.Join(", ", successor.Waits.Select(Successors.ReadSuccessors.Describe))}. Correct its `succeeds:`"),
    };

    private static string Parked(Successors.ReadSuccessors.ParkedStanding parked) => parked.State switch
    {
        Successors.SuccessorState.Ready => string.Create(
            CultureInfo.InvariantCulture,
            $"- `{parked.Cycle}` {parked.Headline ?? string.Empty} - **parked, ready**: what it waited on has merged. `sdlc unpark {parked.Cycle}`, then read its spec and plan again"),
        Successors.SuccessorState.Waiting => string.Create(
            CultureInfo.InvariantCulture,
            $"- `{parked.Cycle}` {parked.Headline ?? string.Empty} - parked, waits on {string.Join(", ", parked.Waits.Select(Successors.ReadSuccessors.Describe))}"),
        _ => string.Create(
            CultureInfo.InvariantCulture,
            $"- `{parked.Cycle}` {parked.Headline ?? string.Empty} - parked, **{(parked.State == Successors.SuccessorState.Stuck ? "stuck" : "unresolved")}**: {string.Join(", ", parked.Waits.Select(Successors.ReadSuccessors.Describe))}. Correct its `waits_on:`"),
    };

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
