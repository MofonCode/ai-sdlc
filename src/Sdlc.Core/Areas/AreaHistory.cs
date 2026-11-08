using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;

namespace Sdlc.Core.Areas;

/// <summary>
/// What the cycles say about the solution's areas, for mining: the names plans proposed, how often each area
/// would have matched, and changed files no area covers (D-150).
/// </summary>
/// <remarks>
/// <para><strong>Counts, never names.</strong> Whether two proposals over overlapping files are one area is a
/// judgement, so this groups by the name written and leaves the reconciling to the mining skill. What it does
/// decide is a listing: which cycles proposed what, and which files each glob covers.</para>
/// <para><strong>The replay</strong> re-matches each cycle's declared files against an area, so a proposal can
/// be judged on whether it would ever narrow anything: an area every plan touches hides nothing.</para>
/// </remarks>
public static class AreaHistory
{
    /// <summary>A name plans proposed, and where.</summary>
    /// <param name="Name">The name written.</param>
    /// <param name="Cycles">The distinct cycles whose plans proposed it.</param>
    /// <param name="Files">Every file or glob those plans listed under it.</param>
    /// <param name="Plans">How many cycles' declared files its globs cover, out of <see cref="Reading.Plans"/>.</param>
    public sealed record Candidate(string Name, IReadOnlyList<CycleId> Cycles, IReadOnlyList<string> Files, int Plans);

    /// <summary>How often a defined area would have matched.</summary>
    /// <param name="Name">The area.</param>
    /// <param name="Plans">How many cycles' declared files it covers.</param>
    public sealed record Replay(string Name, int Plans);

    /// <summary>The reading.</summary>
    /// <param name="Plans">How many cycles have a plan to replay.</param>
    /// <param name="Candidates">Names plans proposed, most cycles first.</param>
    /// <param name="Defined">Each defined area's replay.</param>
    /// <param name="Unmatched">Files the cycles changed that no defined area covers.</param>
    public sealed record Reading(int Plans, IReadOnlyList<Candidate> Candidates, IReadOnlyList<Replay> Defined, IReadOnlyList<string> Unmatched);

    /// <summary>Reads every cycle in the tree.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="defined">The areas the solution defines.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The reading.</returns>
    public static async Task<Reading> Read(string solutionRoot, IReadOnlyList<Area> defined, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(defined);

        List<(CycleId Cycle, IReadOnlyList<string> Declared, IReadOnlyList<PlanArea> Lines, IReadOnlyList<string> Changed)> cycles = [];

        foreach (Cycle cycle in CycleIndex.All(solutionRoot))
        {
            string planPath = cycle.PathOf(ArtifactKind.Plan);

            if (!File.Exists(planPath))
            {
                continue;
            }

            string plan = await File.ReadAllTextAsync(planPath, cancellationToken).ConfigureAwait(false);
            string patchPath = Path.Combine(cycle.EvidenceDirectory, "diff.patch");
            IReadOnlyList<string> changed = File.Exists(patchPath)
                ? AreaScope.FilesIn(await File.ReadAllTextAsync(patchPath, cancellationToken).ConfigureAwait(false))
                : [];

            cycles.Add((cycle.Id, DeclaredFiles.Read(plan), PlanAreas.Read(plan).ValueOrDefault ?? [], changed));
        }

        return Of(cycles.ConvertAll(cycle => (cycle.Cycle, cycle.Declared, cycle.Lines, cycle.Changed)), defined);
    }

    internal static Reading Of(
        IReadOnlyList<(CycleId Cycle, IReadOnlyList<string> Declared, IReadOnlyList<PlanArea> Lines, IReadOnlyList<string> Changed)> cycles,
        IReadOnlyList<Area> defined)
    {
        List<Candidate> candidates =
        [
            .. cycles
                .SelectMany(cycle => cycle.Lines
                    .Where(line => line.Kind == PlanAreaKind.Proposed)
                    .Select(line => (cycle.Cycle, line.Name!, line.Files)))
                .GroupBy(proposal => proposal.Item2, StringComparer.Ordinal)
                .Select(group =>
                {
                    List<string> files = [.. group.SelectMany(proposal => proposal.Files).Distinct(StringComparer.OrdinalIgnoreCase)];
                    Area probe = new(group.Key, files, string.Empty);

                    return new Candidate(
                        group.Key,
                        [.. group.Select(proposal => proposal.Cycle).Distinct()],
                        files,
                        cycles.Count(cycle => cycle.Declared.Any(probe.Covers)));
                })
                .OrderByDescending(candidate => candidate.Cycles.Count)
                .ThenBy(candidate => candidate.Name, StringComparer.Ordinal),
        ];

        List<string> unmatched = defined.Count == 0
            ? []
            : [.. cycles
                .SelectMany(cycle => cycle.Changed)
                .Where(file => !file.StartsWith(Cycle.SdlcDirectoryName + "/", StringComparison.Ordinal))
                .Where(file => !defined.Any(area => area.Covers(file)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.Ordinal)];

        return new Reading(
            cycles.Count,
            candidates,
            [.. defined.Select(area => new Replay(area.Name, cycles.Count(cycle => cycle.Declared.Any(area.Covers))))],
            unmatched);
    }
}
