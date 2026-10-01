using System.Globalization;
using FluentResults;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;

namespace Sdlc.Core.Approvals;

/// <summary>Why a cycle may not be built.</summary>
public enum GateRefusal
{
    /// <summary>Nothing refuses it.</summary>
    None,

    /// <summary>The cycle has no directory. Nothing has been written yet.</summary>
    NoCycle,

    /// <summary>The cycle was abandoned, which is terminal.</summary>
    Abandoned,

    /// <summary>One of the three planning documents is missing.</summary>
    MissingDocument,

    /// <summary>There is no approval ledger. Nobody has been to the gate.</summary>
    NotApproved,

    /// <summary>The standing decision refuses the cycle.</summary>
    Refused,

    /// <summary>A document has changed since it was approved.</summary>
    HashMismatch,

    /// <summary>
    /// A blocking finding is open and the approval does not accept it.
    /// </summary>
    /// <remarks>
    /// A person may override the machine; the override has to be recorded. An approval that leaves a
    /// blocker neither answered nor named in <c>accepted_findings</c> has done neither, so it says the
    /// cycle is agreed while an objection to it stands unanswered.
    /// </remarks>
    UnacceptedBlocker,
}

/// <summary>One document's hash as approved against its hash now.</summary>
/// <param name="Document">The file name.</param>
/// <param name="Approved">The hash the ledger recorded, or null when it recorded none.</param>
/// <param name="Current">The hash of what is on disk, or null when the file is missing.</param>
public sealed record DocumentHash(string Document, string? Approved, string? Current)
{
    /// <summary>Whether what is on disk is what was approved.</summary>
    public bool Matches => Approved is not null && Current is not null
        && string.Equals(Approved, Current, StringComparison.Ordinal);
}

/// <summary>Asks whether a cycle's build may run.</summary>
/// <param name="Id">The cycle to check.</param>
public sealed record ApprovalGateQuery(CycleId Id) : IRequest<GateVerdict>;

/// <summary>What the gate found.</summary>
/// <param name="Cycle">The cycle that was checked.</param>
/// <param name="Refusal">Why it may not be built, or <see cref="GateRefusal.None"/>.</param>
/// <param name="Explanation">
/// What to tell the person, naming the document at fault. Never just "hash mismatch": a refusal that
/// does not say which of the three changed leaves nothing to act on.
/// </param>
/// <param name="Ledger">The ledger, when there was one to read.</param>
/// <param name="Hashes">Each planning document, as approved and as it is now.</param>
public sealed record GateVerdict(
    Cycle Cycle,
    GateRefusal Refusal,
    string Explanation,
    ApprovalLedger? Ledger,
    IReadOnlyList<DocumentHash> Hashes)
{
    /// <summary>Whether the build may run.</summary>
    public bool MayBuild => Refusal == GateRefusal.None;
}

/// <summary>
/// Decides whether a cycle's build may run.
/// </summary>
/// <remarks>
/// This is the local gate: it defends against an agent that forgets, not one that lies. Approval is a
/// hand-pasted entry and the gate recomputes every hash, so approve-then-edit is caught — which is the
/// bypass that happens by accident rather than on purpose.
/// </remarks>
/// <param name="solutionRoot">The root of the solution being governed.</param>
public sealed class ApprovalGateHandler(string solutionRoot)
    : IRequestHandler<ApprovalGateQuery, GateVerdict>
{
    /// <inheritdoc />
    public async Task<Result<GateVerdict>> Handle(
        ApprovalGateQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Cycle cycle = new(request.Id, solutionRoot);

        if (!cycle.Exists)
        {
            return Verdict(cycle, GateRefusal.NoCycle, string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {cycle.Id} has no directory at {cycle.Directory}. Start it with an intent."));
        }

        if (cycle.IsAbandoned)
        {
            return Verdict(cycle, GateRefusal.Abandoned, string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {cycle.Id} was abandoned, and abandonment is terminal. Open a new cycle rather than reviving this one; {cycle.AbandonedPath} says why it stopped."));
        }

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            if (!File.Exists(cycle.PathOf(kind)))
            {
                return Verdict(cycle, GateRefusal.MissingDocument, string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has no {Cycle.FileNameOf(kind)}. The approval covers all three planning documents, so all three have to exist."));
            }
        }

        if (!File.Exists(cycle.ApprovalPath))
        {
            return Verdict(cycle, GateRefusal.NotApproved, string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {cycle.Id} is not approved: there is no {Path.GetFileName(cycle.ApprovalPath)}. Run 'sdlc cycle-approval {cycle.Id}' and paste what it prints."));
        }

        string ledgerText = await File.ReadAllTextAsync(cycle.ApprovalPath, cancellationToken)
            .ConfigureAwait(false);

        Result<ApprovalLedger> read = ApprovalLedger.Read(ledgerText, cycle.ApprovalPath);
        if (read.IsFailed)
        {
            return read.ToResult<GateVerdict>();
        }

        ApprovalLedger ledger = read.Value;
        GateEntry? current = ledger.Current;

        List<DocumentHash> hashes = [];
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            string name = Cycle.FileNameOf(kind);
            string? approved = current is not null && current.Hashes.TryGetValue(name, out string? recorded)
                ? recorded
                : null;

            string actual = await ContentHash
                .OfFile(cycle.PathOf(kind), cancellationToken)
                .ConfigureAwait(false);

            hashes.Add(new DocumentHash(name, approved, actual));
        }

        if (current is null || current.Decision != GateDecision.Approved)
        {
            string detail = current?.Reason is { } reason
                ? string.Create(CultureInfo.InvariantCulture, $" The reason given: {reason}")
                : string.Empty;

            return Verdict(
                cycle,
                GateRefusal.Refused,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} is not approved: the standing decision is {Describe(current?.Decision)}.{detail}"),
                ledger,
                hashes);
        }

        List<DocumentHash> changed = hashes.FindAll(hash => !hash.Matches);
        if (changed.Count > 0)
        {
            return Verdict(cycle, GateRefusal.HashMismatch, Mismatch(cycle, changed), ledger, hashes);
        }

        IReadOnlyList<string> unaccepted =
            await Unaccepted(cycle, current, cancellationToken).ConfigureAwait(false);

        if (unaccepted.Count > 0)
        {
            return Verdict(cycle, GateRefusal.UnacceptedBlocker, Unanswered(cycle, unaccepted), ledger, hashes);
        }

        return Verdict(
            cycle,
            GateRefusal.None,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {cycle.Id} was approved by {current.By} at {current.At:u}, and all three documents match what was approved."),
            ledger,
            hashes);
    }

    /// <summary>
    /// Finds blocking findings that are open and that the approval does not name.
    /// </summary>
    /// <remarks>
    /// The approval text pre-lists every open blocker, so a person pasting it accepts them by name and
    /// this finds nothing. It fires when a blocker was raised after the text was printed, or when
    /// someone removed a line from accepted_findings intending to fix it and approved anyway.
    /// </remarks>
    private static async Task<IReadOnlyList<string>> Unaccepted(
        Cycle cycle,
        GateEntry approval,
        CancellationToken cancellationToken)
    {
        List<string> unaccepted = [];

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            Result<IReadOnlyList<FindingsRound>> history =
                await FindingsReader.ReadHistory(cycle, kind, cancellationToken).ConfigureAwait(false);

            if (history.IsFailed)
            {
                continue;
            }

            foreach (Finding blocker in FindingsReader.StandingBlockers(history.Value))
            {
                string qualified = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Cycle.NameOf(kind)}.{blocker.Id}");

                bool accepted = approval.AcceptedFindings.Any(listed =>
                    string.Equals(listed.Trim(), qualified, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(listed.Trim(), blocker.Id, StringComparison.OrdinalIgnoreCase));

                if (!accepted)
                {
                    unaccepted.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"  {qualified}  {blocker.Cites}  {blocker.Statement}"));
                }
            }
        }

        return unaccepted;
    }

    private static string Unanswered(Cycle cycle, IReadOnlyList<string> unaccepted) =>
        string.Join(
            Environment.NewLine,
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} is approved, and {unaccepted.Count} blocking finding{(unaccepted.Count == 1 ? " is" : "s are")} open without being accepted:"),
                string.Empty,
                .. unaccepted,
                string.Empty,
                "Either refine the artifact so the finding is answered, or accept it by name in the",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"approval's accepted_findings. Re-run 'sdlc cycle-approval {cycle.Id}' to get an entry that lists them."),
            ]);

    private static string Describe(GateDecision? decision) => decision switch
    {
        GateDecision.Approved => "approved",
        GateDecision.ChangesRequested => "changes requested",
        GateDecision.Denied => "denied",
        _ => "nothing at all",
    };

    /// <summary>
    /// Says which document changed, and shows enough of each hash to be compared by eye.
    /// </summary>
    /// <remarks>
    /// A pasted hash cannot be checked by a reader, so a refusal that says only "hash mismatch" is
    /// unactionable. Naming the file and showing twelve characters of each is what turns it into one
    /// obvious next step.
    /// </remarks>
    private static string Mismatch(Cycle cycle, IReadOnlyList<DocumentHash> changed)
    {
        IEnumerable<string> lines = changed.Select(hash => hash.Approved is null
            ? string.Create(CultureInfo.InvariantCulture, $"  {hash.Document}: the approval records no hash for it")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"  {hash.Document}: approved {ContentHash.Abbreviate(hash.Approved)}, now {ContentHash.Abbreviate(hash.Current ?? "missing")}"));

        return string.Join(
            Environment.NewLine,
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has changed since it was approved:"),
                .. lines,
                string.Empty,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The approval describes documents that are no longer on disk. Re-run 'sdlc cycle-approval {cycle.Id}' and approve them again."),
            ]);
    }

    private static Result<GateVerdict> Verdict(
        Cycle cycle,
        GateRefusal refusal,
        string explanation,
        ApprovalLedger? ledger = null,
        IReadOnlyList<DocumentHash>? hashes = null) =>
        Result.Ok(new GateVerdict(cycle, refusal, explanation, ledger, hashes ?? []));
}
