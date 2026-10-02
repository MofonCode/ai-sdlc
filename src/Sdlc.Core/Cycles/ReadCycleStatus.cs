using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Findings;
using Sdlc.Core.Solutions;

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
/// <param name="UpstreamHash">
/// The hash of the artifact above as it stands now, or null when this artifact has no upstream.
/// </param>
/// <param name="JudgedAgainst">
/// The upstream hash the latest round recorded, or null when it recorded none or there is no round.
/// </param>
public sealed record ArtifactState(
    ArtifactKind Kind,
    string Path,
    bool Exists,
    string? Hash,
    int Rounds,
    int LatestRound,
    IReadOnlyList<int> History,
    IReadOnlyList<Finding> OpenBlockers,
    string? UpstreamHash = null,
    string? JudgedAgainst = null)
{
    /// <summary>
    /// Whether the latest round judged this artifact against an upstream that has since changed.
    /// </summary>
    /// <remarks>
    /// A round is clean <em>as of</em> the upstream it saw. The approval proves these three documents
    /// were approved, not that the spec was derived from this intent -- so an intent edited after a
    /// clean spec leaves a spec derived from an intent that no longer exists, with every approval hash
    /// matching on disk. Steering is the mechanism that creates exactly that.
    /// <para>
    /// A round that recorded no upstream at all is stale too. The honest reading of a missing hash is
    /// "this round cannot say what it judged", and treating that as fine would make the check skippable
    /// by omitting a field.
    /// </para>
    /// </remarks>
    public bool IsStale => UpstreamHash is not null
        && Rounds > 0
        && !string.Equals(UpstreamHash, JudgedAgainst, StringComparison.Ordinal);

    /// <summary>
    /// Whether this artifact lets the next stage run.
    /// </summary>
    /// <remarks>
    /// Interrogation is part of the chain rather than an optional extra, which is what keeps a single
    /// approval from resting on three documents nothing has argued with. So an artifact nobody has
    /// interrogated is not clear, however clean it looks -- and neither is one whose judgement was
    /// made against an upstream that has moved since.
    /// </remarks>
    public bool IsClear => Exists && Rounds > 0 && OpenBlockers.Count == 0 && !IsStale;
}


/// <summary>A cycle's state: its artifacts, its rounds, and what the gate makes of it.</summary>
/// <param name="Cycle">The cycle.</param>
/// <param name="Artifacts">Each planning document's state, in chain order.</param>
/// <param name="Gate">What the approval gate found.</param>
/// <param name="Verify">
/// The verify document's state, when one exists. Kept apart from <paramref name="Artifacts"/> rather than
/// added to it: that list is the three the approval covers, and the gate's blockers and hashes are computed
/// from it, so a fourth entry would quietly change what "approved" means.
/// </param>
/// <param name="Outcome">
/// What the verification concluded, when it has. Read here so the panel can say what to do after a build
/// rather than telling somebody to build again.
/// </param>
public sealed record CycleStatus(
    Cycle Cycle,
    IReadOnlyList<ArtifactState> Artifacts,
    GateVerdict Gate,
    ArtifactState? Verify = null,
    string? Outcome = null)
{
    /// <summary>The artifact the cycle is waiting on, or null when all three are clear.</summary>
    public ArtifactState? Blocking => Artifacts.FirstOrDefault(artifact => !artifact.IsClear);

    /// <summary>Artifacts whose latest round judged them against an upstream that has since changed.</summary>
    public IReadOnlyList<ArtifactState> Stale => [.. Artifacts.Where(artifact => artifact.IsStale)];

    /// <summary>Every open blocking finding across the three documents, prefixed by document.</summary>
    public IReadOnlyList<string> OpenBlockerIds =>
    [
        .. Artifacts.SelectMany(artifact => artifact.OpenBlockers.Select(finding =>
            string.Create(CultureInfo.InvariantCulture, $"{Cycle.NameOf(artifact.Kind)}.{finding.Id}"))),
    ];
}

/// <summary>Reads where a cycle has got to: every artifact, its rounds, and what to do next.</summary>
public static class ReadCycleStatus
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddReadCycleStatusRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Request, CycleStatus>, Handler>();
    }

    /// <summary>Asks where a cycle has got to.</summary>
    /// <param name="Id">The cycle to read.</param>
    public sealed record Request(CycleId Id) : IRequest<CycleStatus>;

    /// <summary>Reads a cycle's state from what is on disk.</summary>
    /// <param name="solution">The root of the solution being governed.</param>
    private sealed class Handler(SolutionRoot solution, Mediator mediator)
        : IRequestHandler<Request, CycleStatus>
    {
        /// <inheritdoc />
        public async Task<Result<CycleStatus>> Handle(
            Request request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);
            List<ArtifactState> artifacts = [];

            // Read in chain order, so each artifact's upstream is already in hand when its turn comes. The
            // upstream's hash is taken from what is on disk now, which is the whole point: the round says
            // what it judged, and this says what is there.
            foreach (ArtifactKind kind in Cycle.PlanningDocuments)
            {
                ArtifactKind? upstream = Cycle.UpstreamOf(kind);
                string? upstreamHash = upstream is null
                    ? null
                    : artifacts.FirstOrDefault(artifact => artifact.Kind == upstream.Value)?.Hash;

                Result<ArtifactState> state =
                    await Read(cycle, kind, upstreamHash, cancellationToken).ConfigureAwait(false);

                if (state.IsFailed)
                {
                    return state.ToResult<CycleStatus>();
                }

                artifacts.Add(state.Value);
            }

            Result<GateVerdict> gate = await mediator
                .Send<ApprovalGate.Request, GateVerdict>(
                    new ApprovalGate.Request(request.Id),
                    cancellationToken)
                .ConfigureAwait(false);

            if (gate.IsFailed)
            {
                return gate.ToResult<CycleStatus>();
            }

            Result<ArtifactState> verify = await Read(
                cycle,
                ArtifactKind.Verify,
                artifacts.Single(artifact => artifact.Kind == ArtifactKind.Plan).Hash,
                cancellationToken).ConfigureAwait(false);

            if (verify.IsFailed)
            {
                return verify.ToResult<CycleStatus>();
            }

            return Result.Ok(new CycleStatus(
                cycle,
                artifacts,
                gate.Value,
                verify.Value.Exists ? verify.Value : null,
                verify.Value.Exists
                    ? await Outcome(cycle, cancellationToken).ConfigureAwait(false)
                    : null));
        }

        /// <summary>
        /// What the verify document says its outcome was.
        /// </summary>
        /// <remarks>
        /// Read with a line scan rather than a YAML parser, because this is for a panel and an unreadable
        /// document should produce a panel that says nothing about the outcome rather than a status command that
        /// refuses to run. The chain check reads it properly, and that is where being strict belongs.
        /// </remarks>
        private static async Task<string?> Outcome(Cycle cycle, CancellationToken cancellationToken)
        {
            Result<FrontmatterDocument> split = await Frontmatter
                .ReadFile(cycle.PathOf(ArtifactKind.Verify), cancellationToken)
                .ConfigureAwait(false);

            if (split.IsFailed)
            {
                return null;
            }

            return split.Value.Yaml
                .ReplaceLineEndings()
                .Split(Environment.NewLine)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.StartsWith("outcome:", StringComparison.Ordinal))
                ?["outcome:".Length..]
                .Trim();
        }

        private static async Task<Result<ArtifactState>> Read(
            Cycle cycle,
            ArtifactKind kind,
            string? upstreamHash,
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

            // The latest round is the one that stands, so it is the one whose upstream matters. An earlier
            // round judged against an earlier upstream is history rather than a problem.
            FindingsRound? latest = rounds.Count == 0 ? null : rounds[^1];

            return Result.Ok(new ArtifactState(
                kind,
                path,
                exists,
                hash,
                rounds.Count,
                rounds.Count == 0 ? 0 : rounds.Max(round => round.Round),
                [.. rounds.Select(round => round.Findings.Count(finding => finding.IsOpenBlocker))],
                FindingsReader.StandingBlockers(rounds),
                upstreamHash,
                latest?.UpstreamSha256));
        }

    }
}
