using System.Globalization;
using System.Text;
using FluentResults;
using MofonCode.Core;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Approvals;

/// <summary>Asks for the approval entry a person would paste to approve a cycle.</summary>
/// <param name="Id">The cycle to approve.</param>
/// <param name="By">
/// Who is approving. Supplied by the caller from the git identity rather than invented here: the
/// entry names a person, and a tool that filled that in for itself would be writing the approval.
/// </param>
public sealed record ApprovalTextQuery(CycleId Id, string By) : IRequest<ApprovalTextResult>;

/// <summary>The text to paste, or what has to happen before there is any.</summary>
/// <param name="Status">The cycle's state, so a caller can report it without reading again.</param>
/// <param name="Entry">The entry to paste, when the cycle may be approved.</param>
/// <param name="Instructions">What to do with it, or what is in the way.</param>
/// <param name="MayApprove">Whether an entry was produced.</param>
public sealed record ApprovalTextResult(
    CycleStatus Status,
    string? Entry,
    string Instructions,
    bool MayApprove);

/// <summary>
/// Prints an approval entry. Writes nothing.
/// </summary>
/// <remarks>
/// A human performs the act of approval and no skill can perform it for them: this produces text, the
/// person creates or opens <c>approval.md</c>, pastes, and saves. That is what keeps the hook rule
/// absolute rather than conditional — every agent write to that file is denied, with no approve
/// command to carve out an exception for.
/// <para>
/// It refuses while any blocking finding is open and unlisted. What it prints instead names them, so
/// accepting a blocker is a visible act rather than a silence.
/// </para>
/// </remarks>
/// <param name="solutionRoot">The root of the solution being governed.</param>
public sealed class ApprovalTextHandler(string solutionRoot)
    : IRequestHandler<ApprovalTextQuery, ApprovalTextResult>
{
    /// <inheritdoc />
    public async Task<Result<ApprovalTextResult>> Handle(
        ApprovalTextQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<CycleStatus> read = await new CycleStatusHandler(solutionRoot)
            .Handle(new CycleStatusQuery(request.Id), cancellationToken)
            .ConfigureAwait(false);

        if (read.IsFailed)
        {
            return read.ToResult<ApprovalTextResult>();
        }

        CycleStatus status = read.Value;

        if (status.Cycle.IsAbandoned)
        {
            return Refuse(status, string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {status.Cycle.Id} was abandoned, and abandonment is terminal. Open a new cycle instead."));
        }

        List<ArtifactState> missing = [.. status.Artifacts.Where(artifact => !artifact.Exists)];
        if (missing.Count > 0)
        {
            return Refuse(status, Missing(status, missing));
        }

        List<ArtifactState> uninterrogated = [.. status.Artifacts.Where(artifact => artifact.Rounds == 0)];
        if (uninterrogated.Count > 0)
        {
            return Refuse(status, Uninterrogated(status, uninterrogated));
        }

        // The approval lists every open blocker as accepted, so a person pasting it is accepting them
        // by name. Refusing outright would make a blocker unacceptable, and the gate's whole design is
        // that a person may override the machine as long as the override is recorded.
        string entry = Entry(status, request.By);

        return Result.Ok(new ApprovalTextResult(
            status,
            entry,
            Instructions(status),
            MayApprove: true));
    }

    private static Result<ApprovalTextResult> Refuse(CycleStatus status, string why) =>
        Result.Ok(new ApprovalTextResult(status, Entry: null, why, MayApprove: false));

    private static string Missing(CycleStatus status, List<ArtifactState> missing)
    {
        IEnumerable<string> names = missing.Select(artifact => Cycle.FileNameOf(artifact.Kind));

        string joined = string.Join(", ", names);
        string verb = missing.Count == 1 ? "has" : "have";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Cycle {status.Cycle.Id} cannot be approved yet: {joined} {verb} not been written. The approval covers all three planning documents as one act, so all three have to exist first.");
    }

    private static string Uninterrogated(CycleStatus status, List<ArtifactState> pending)
    {
        string joined = string.Join(", ", pending.Select(artifact => Cycle.NameOf(artifact.Kind)));
        string verb = pending.Count == 1 ? "has" : "have";

        List<string> lines =
        [
            string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {status.Cycle.Id} cannot be approved yet: {joined} {verb} never been interrogated."),
            string.Empty,
            "Interrogation is part of the chain rather than an optional extra. Approving three documents",
            "that nothing has argued with is the single gate resting on nothing.",
            string.Empty,
            .. pending.Select(artifact => string.Create(
                CultureInfo.InvariantCulture,
                $"    sdlc interrogate {status.Cycle.Id} {Cycle.NameOf(artifact.Kind)}")),
        ];

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Builds the entry, with every open blocker pre-listed as accepted.
    /// </summary>
    /// <remarks>
    /// Pre-listing them is what makes accepting one visible: a person reading what they are about to
    /// paste sees the objections they are agreeing to carry, by id, and can remove the line and go and
    /// fix one instead.
    /// </remarks>
    private static string Entry(CycleStatus status, string by)
    {
        StringBuilder entry = new();
        IReadOnlyList<string> accepted = status.OpenBlockerIds;

        entry.AppendLine("  - decision: approved");
        entry.AppendLine(CultureInfo.InvariantCulture, $"    by: {by}");
        entry.AppendLine(CultureInfo.InvariantCulture, $"    at: {DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        entry.AppendLine("    hashes:");

        // The key carries its colon before the padding, so the columns line up and the result is
        // still YAML. Padding a bare name and relying on whitespace would produce a block that reads
        // correctly and parses as nothing.
        foreach (ArtifactState artifact in status.Artifacts)
        {
            string key = Cycle.FileNameOf(artifact.Kind) + ":";
            entry.AppendLine(CultureInfo.InvariantCulture, $"      {key,-11} {artifact.Hash}");
        }

        entry.AppendLine("    findings_rounds:");
        foreach (ArtifactState artifact in status.Artifacts)
        {
            string key = Cycle.FileNameOf(artifact.Kind) + ":";
            entry.AppendLine(CultureInfo.InvariantCulture, $"      {key,-11} {artifact.LatestRound}");
        }

        entry.Append("    accepted_findings: [");
        entry.Append(string.Join(", ", accepted));
        entry.AppendLine("]");

        return entry.ToString();
    }

    private static string Instructions(CycleStatus status)
    {
        bool exists = File.Exists(status.Cycle.ApprovalPath);
        IReadOnlyList<string> accepted = status.OpenBlockerIds;

        List<string> lines =
        [
            exists
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"Open {status.Cycle.ApprovalPath} and paste the entry above at the end of the decisions list.")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"Create {status.Cycle.ApprovalPath} with the header below, then the entry above."),
        ];

        if (!exists)
        {
            lines.AddRange(
                string.Empty,
                "    ---",
                string.Create(CultureInfo.InvariantCulture, $"    cycle: {status.Cycle.Id}"),
                "    decisions:",
                "      <the entry above>",
                "    ---");
        }

        if (accepted.Count > 0)
        {
            lines.AddRange(
                string.Empty,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The entry accepts {accepted.Count} open blocking {(accepted.Count == 1 ? "finding" : "findings")}: {string.Join(", ", accepted)}."),
                "Pasting it is agreeing to carry them. To fix one instead, remove it from",
                "accepted_findings and refine the artifact first - the gate will refuse an",
                "approval that leaves a blocker neither answered nor accepted.");
        }

        lines.AddRange(
            string.Empty,
            "Nothing here writes that file. The hook denies every agent write to it, which is what",
            "makes a pasted approval a person's act rather than a tool's.");

        return string.Join(Environment.NewLine, lines);
    }
}
