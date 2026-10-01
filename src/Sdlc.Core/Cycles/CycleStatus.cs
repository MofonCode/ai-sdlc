using System.Globalization;
using FluentResults;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Findings;

namespace Sdlc.Core.Cycles;

/// <summary>Where one artifact in a cycle has got to.</summary>
/// <param name="Kind">Which artifact.</param>
/// <param name="Path">Where it is, whether or not it exists.</param>
/// <param name="Exists">Whether it has been written.</param>
/// <param name="Hash">Its canonical hash, when it exists.</param>
/// <param name="Rounds">How many interrogation rounds it has had.</param>
/// <param name="LatestRound">The highest round number, or zero when it has never been interrogated.</param>
/// <param name="History">How many open blockers each round left, in round order, for the panel.</param>
/// <param name="OpenBlockers">
/// Findings that still stop it advancing: blocking, and open in the latest round that mentions them.
/// </param>
public sealed record ArtifactState(
    ArtifactKind Kind,
    string Path,
    bool Exists,
    string? Hash,
    int Rounds,
    int LatestRound,
    IReadOnlyList<int> History,
    IReadOnlyList<Finding> OpenBlockers)
{
    /// <summary>
    /// Whether this artifact lets the next stage run.
    /// </summary>
    /// <remarks>
    /// Interrogation is part of the chain rather than an optional extra, which is what keeps a single
    /// approval from resting on three documents nothing has argued with. So an artifact nobody has
    /// interrogated is not clear, however clean it looks.
    /// </remarks>
    public bool IsClear => Exists && Rounds > 0 && OpenBlockers.Count == 0;
}

/// <summary>Asks where a cycle has got to.</summary>
/// <param name="Id">The cycle to read.</param>
public sealed record CycleStatusQuery(CycleId Id) : IRequest<CycleStatus>;

/// <summary>A cycle's state: its artifacts, its rounds, and what the gate makes of it.</summary>
/// <param name="Cycle">The cycle.</param>
/// <param name="Artifacts">Each planning document's state, in chain order.</param>
/// <param name="Gate">What the approval gate found.</param>
public sealed record CycleStatus(
    Cycle Cycle,
    IReadOnlyList<ArtifactState> Artifacts,
    GateVerdict Gate)
{
    /// <summary>The artifact the cycle is waiting on, or null when all three are clear.</summary>
    public ArtifactState? Blocking => Artifacts.FirstOrDefault(artifact => !artifact.IsClear);

    /// <summary>Every open blocking finding across the three documents, prefixed by document.</summary>
    public IReadOnlyList<string> OpenBlockerIds =>
    [
        .. Artifacts.SelectMany(artifact => artifact.OpenBlockers.Select(finding =>
            string.Create(CultureInfo.InvariantCulture, $"{Cycle.NameOf(artifact.Kind)}.{finding.Id}"))),
    ];
}

/// <summary>Reads a cycle's state from what is on disk.</summary>
/// <param name="solutionRoot">The root of the solution being governed.</param>
public sealed class CycleStatusHandler(string solutionRoot)
    : IRequestHandler<CycleStatusQuery, CycleStatus>
{
    /// <inheritdoc />
    public async Task<Result<CycleStatus>> Handle(
        CycleStatusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Cycle cycle = new(request.Id, solutionRoot);
        List<ArtifactState> artifacts = [];

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            Result<ArtifactState> state = await Read(cycle, kind, cancellationToken).ConfigureAwait(false);
            if (state.IsFailed)
            {
                return state.ToResult<CycleStatus>();
            }

            artifacts.Add(state.Value);
        }

        Result<GateVerdict> gate = await new ApprovalGateHandler(solutionRoot)
            .Handle(new ApprovalGateQuery(request.Id), cancellationToken)
            .ConfigureAwait(false);

        return gate.IsFailed
            ? gate.ToResult<CycleStatus>()
            : Result.Ok(new CycleStatus(cycle, artifacts, gate.Value));
    }

    private static async Task<Result<ArtifactState>> Read(
        Cycle cycle,
        ArtifactKind kind,
        CancellationToken cancellationToken)
    {
        string path = cycle.PathOf(kind);
        bool exists = File.Exists(path);

        string? hash = exists
            ? await ContentHash.OfFile(path, cancellationToken).ConfigureAwait(false)
            : null;

        Result<IReadOnlyList<FindingsRound>> history =
            await FindingsReader.ReadHistory(cycle, kind, cancellationToken).ConfigureAwait(false);

        if (history.IsFailed)
        {
            return history.ToResult<ArtifactState>();
        }

        IReadOnlyList<FindingsRound> rounds = history.Value;

        return Result.Ok(new ArtifactState(
            kind,
            path,
            exists,
            hash,
            rounds.Count,
            rounds.Count == 0 ? 0 : rounds.Max(round => round.Round),
            [.. rounds.Select(round => round.Findings.Count(finding => finding.IsOpenBlocker))],
            FindingsReader.StandingBlockers(rounds)));
    }

}
