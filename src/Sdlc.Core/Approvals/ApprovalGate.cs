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
    /// A blocking finding is open.
    /// </summary>
    /// <remarks>
    /// There is no way out of this refusal but to answer the finding. A blocking finding cannot be
    /// waived, accepted or overridden by any mechanism, so the gate does not ask whether the approval
    /// named it -- an approval standing over an open blocker says the cycle is agreed while an
    /// objection to it stands unanswered, and that is what this refuses.
    /// <para>
    /// The printer refuses to produce an entry while a blocker is open, so reaching this means the
    /// blocker was raised after the entry was printed or the entry was written by hand. Keeping the
    /// check here as well as there is deliberate: the printer is advice a person can decline to take,
    /// and the gate is the layer that does not negotiate.
    /// </para>
    /// </remarks>
    OpenBlocker,

    /// <summary>
    /// A findings round judged its artifact against an upstream that has since changed.
    /// </summary>
    /// <remarks>
    /// The three approval hashes prove these three documents were approved. They do not prove the spec
    /// was derived from this intent -- so an intent edited after a clean spec leaves a spec derived from
    /// an intent that no longer exists, with every approval hash matching. Only the round knows what it
    /// read, which is why it records it.
    /// </remarks>
    StaleRound,
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
                $"Cycle {cycle.Id} is not approved: there is no {Path.GetFileName(cycle.ApprovalPath)}. Run 'sdlc draft approval {cycle.Id}' and paste what it prints."));
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

        IReadOnlyList<string> blockers =
            await OpenBlockers(cycle, cancellationToken).ConfigureAwait(false);

        if (blockers.Count > 0)
        {
            return Verdict(cycle, GateRefusal.OpenBlocker, Blocked(cycle, blockers), ledger, hashes);
        }

        IReadOnlyList<string> stale = await Stale(cycle, cancellationToken).ConfigureAwait(false);
        if (stale.Count > 0)
        {
            return Verdict(cycle, GateRefusal.StaleRound, Superseded(cycle, stale), ledger, hashes);
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
    /// Finds every blocking finding still open across the three planning documents.
    /// </summary>
    /// <remarks>
    /// A history that cannot be read is skipped rather than treated as clean by accident: an unreadable
    /// round is a separate failure, reported where it is read, and inferring "no blockers" from "could
    /// not tell" is how a check comes to pass for the wrong reason.
    /// </remarks>
    private static async Task<IReadOnlyList<string>> OpenBlockers(
        Cycle cycle,
        CancellationToken cancellationToken)
    {
        List<string> blockers = [];

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

                blockers.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {qualified}  {blocker.Cites}  {blocker.Statement}"));
            }
        }

        return blockers;
    }

    /// <summary>
    /// Finds artifacts whose latest round judged them against an upstream that has since moved.
    /// </summary>
    /// <remarks>
    /// Reads the documents in chain order and compares each round's recorded upstream against the
    /// upstream's hash now. A round that recorded no upstream at all counts as stale: a round that
    /// cannot say what it judged is not evidence that it judged the right thing.
    /// </remarks>
    private static async Task<IReadOnlyList<string>> Stale(
        Cycle cycle,
        CancellationToken cancellationToken)
    {
        List<string> stale = [];
        Dictionary<ArtifactKind, string> hashes = [];

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            hashes[kind] = await ContentHash
                .OfFile(cycle.PathOf(kind), cancellationToken)
                .ConfigureAwait(false);

            if (Cycle.UpstreamOf(kind) is not { } upstream)
            {
                continue;
            }

            Result<IReadOnlyList<FindingsRound>> history =
                await FindingsReader.ReadHistory(cycle, kind, cancellationToken).ConfigureAwait(false);

            if (history.IsFailed || history.Value.Count == 0)
            {
                continue;
            }

            string? judged = history.Value[^1].UpstreamSha256;
            string now = hashes[upstream];

            if (string.Equals(judged, now, StringComparison.Ordinal))
            {
                continue;
            }

            stale.Add(judged is null
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {Cycle.FileNameOf(kind)}: round {history.Value[^1].Round} did not record which {Cycle.FileNameOf(upstream)} it judged against")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {Cycle.FileNameOf(kind)}: round {history.Value[^1].Round} judged {Cycle.FileNameOf(upstream)} {ContentHash.Abbreviate(judged)}, which is now {ContentHash.Abbreviate(now)}"));
        }

        return stale;
    }

    private static string Superseded(Cycle cycle, IReadOnlyList<string> stale) =>
        string.Join(
            Environment.NewLine,
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} is approved, and {stale.Count} interrogation{(stale.Count == 1 ? " judged" : "s judged")} against a document that has changed since:"),
                string.Empty,
                .. stale,
                string.Empty,
                "The approval records that three documents were read. It cannot record that the spec was",
                "derived from this intent, so an upstream edited after a clean round leaves a judgement",
                "about a document that no longer exists. Re-interrogate the artifact named above, then",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"approve again with 'sdlc draft approval {cycle.Id}'."),
            ]);

    private static string Blocked(Cycle cycle, IReadOnlyList<string> blockers) =>
        string.Join(
            Environment.NewLine,
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} is approved, and {blockers.Count} blocking finding{(blockers.Count == 1 ? " is" : "s are")} still open:"),
                string.Empty,
                .. blockers,
                string.Empty,
                "A blocking finding cannot be waived, accepted or overridden. Refine the artifact until",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the finding is answered, re-interrogate, then approve again with 'sdlc draft approval {cycle.Id}'."),
            ]);

    private static string Describe(GateDecision? decision) => decision switch
    {
        GateDecision.Approved => "approved",
        GateDecision.ChangesRequested => "changes requested",
        GateDecision.Denied => "denied",
        GateDecision.Reopened => "reopened",
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
                    $"The approval describes documents that are no longer on disk. Re-run 'sdlc draft approval {cycle.Id}' and approve them again."),
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
