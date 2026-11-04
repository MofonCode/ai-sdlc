namespace Sdlc.Core.Findings;

/// <summary>
/// Where the latest round's open blockers came from, and what became of every one raised before it.
/// </summary>
/// <remarks>
/// <para>The panel's history says how many blockers each round left open, which cannot tell two very
/// different loops apart: one fixing the same objections over and over, and one where every round finds
/// something nobody had raised and every earlier objection was answered. The second is a document too
/// wide to prove in one pass, and the person who sees it should be resizing the cycle rather than
/// running another refine. Nothing else on the panel says which loop this is.</para>
/// <para>A finding is counted by its id, which it keeps across rounds. One that was answered or withdrawn
/// and then stood open again is counted as having come back, whatever the rule it cites; the loop block
/// counts the same history by rule and draws its own conclusion.</para>
/// </remarks>
/// <param name="Round">The latest round's number.</param>
/// <param name="Raised">Open blockers the latest round raised for the first time.</param>
/// <param name="Carried">Open blockers raised in an earlier round and never settled since.</param>
/// <param name="Returned">Open blockers that had been answered or withdrawn, and are open again.</param>
/// <param name="Settled">Blockers raised at any point that are now answered or withdrawn.</param>
public sealed record Turnover(int Round, int Raised, int Carried, int Returned, int Settled)
{
    /// <summary>
    /// Counts the turnover of one artifact's blockers.
    /// </summary>
    /// <param name="rounds">Every round recorded for the artifact.</param>
    /// <returns>The counts, or null before a second round, when everything open is new by definition.</returns>
    public static Turnover? Of(IReadOnlyList<FindingsRound> rounds)
    {
        ArgumentNullException.ThrowIfNull(rounds);

        if (rounds.Count < 2)
        {
            return null;
        }

        List<FindingsRound> ordered = [.. rounds.OrderBy(round => round.Round)];
        int latest = ordered[^1].Round;

        Dictionary<string, int> firstRaised = new(StringComparer.Ordinal);
        HashSet<string> settledOnce = new(StringComparer.Ordinal);

        foreach (FindingsRound round in ordered[..^1])
        {
            foreach (Finding finding in round.Findings)
            {
                firstRaised.TryAdd(finding.Id, round.Round);

                if (finding.Disposition != Disposition.Open)
                {
                    settledOnce.Add(finding.Id);
                }
            }
        }

        int raised = 0;
        int carried = 0;
        int returned = 0;
        int settled = 0;

        foreach (Finding finding in FindingsReader.Standing(ordered))
        {
            if (finding.Severity != Severity.Blocking)
            {
                continue;
            }

            if (finding.Disposition != Disposition.Open)
            {
                settled++;
            }
            else if (!firstRaised.ContainsKey(finding.Id))
            {
                raised++;
            }
            else if (settledOnce.Contains(finding.Id))
            {
                returned++;
            }
            else
            {
                carried++;
            }
        }

        return new Turnover(latest, raised, carried, returned, settled);
    }
}
