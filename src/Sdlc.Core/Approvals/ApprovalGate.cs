using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Approvals;

/// <summary>Says whether a cycle's build may run, and refuses with the reason when it may not.</summary>
public static class ApprovalGate
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApprovalGateRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

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

        /// <summary>
        /// There is an approval ledger and it cannot be read.
        /// </summary>
        /// <remarks>
        /// <strong>A malformed ledger is a governed stop, not the gate breaking.</strong> This was a
        /// failed <c>Result</c>, which propagated out of <see cref="ReadCycleStatus"/> -- so a person who
        /// mis-pasted an entry lost <c>sdlc status</c> and <c>sdlc draft approval</c> in the same stroke,
        /// and the only tool that would have handed them the correct text refused to run until they fixed
        /// the YAML by hand. The file a person writes is the file most likely to be malformed, so the
        /// recovery path has to survive it.
        /// <para>
        /// It never reads as approved: a refusal that is not <see cref="GateRefusal.None"/> is a refusal,
        /// and the same reasoning as <see cref="GateRefusal.UnreadableRound"/> applies -- "could not tell"
        /// must never read as "clean".
        /// </para>
        /// </remarks>
        UnreadableLedger,

        /// <summary>The standing decision refuses the cycle.</summary>
        Refused,

        /// <summary>
        /// A findings round exists and cannot be read.
        /// </summary>
        /// <remarks>
        /// <strong>"Could not tell" must never read as "clean".</strong> This skipped the artifact
        /// instead, so overwriting one round file with <c>{}</c> made every blocker for that document
        /// disappear from the gate and from <c>verify-chain</c> -- a one-command waiver for the thing
        /// D-27 says cannot be waived by any mechanism.
        /// </remarks>
        UnreadableRound,

        /// <summary>
        /// A planning document has no findings round at all.
        /// </summary>
        /// <remarks>
        /// §5.4's clean-round requirement, which <c>ReadCycleStatus.ArtifactState.IsClear</c> has
        /// always applied and this did not -- so deleting the <c>findings/</c> directory outright left
        /// the gate, <c>verify-chain</c> and <c>pr-ready</c> all passing, with no round having ever
        /// existed. The advisory reader was stricter than the guarantee; <c>ApprovalText</c> refuses
        /// this too, and it is advice a person can decline.
        /// </remarks>
        Uninterrogated,

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

    /// <summary>What the gate found.</summary>
    /// <param name="Cycle">The cycle that was checked.</param>
    /// <param name="Refusal">Why it may not be built, or <see cref="GateRefusal.None"/>.</param>
    /// <param name="Explanation">
    /// What to tell the person, naming the document at fault. Never just "hash mismatch": a refusal that
    /// does not say which of the three changed leaves nothing to act on.
    /// </param>
    /// <param name="Ledger">The ledger, when there was one to read.</param>
    /// <param name="Hashes">Each planning document, as approved and as it is now.</param>
    public sealed record Response(
        Cycle Cycle,
        GateRefusal Refusal,
        string Explanation,
        ApprovalLedger? Ledger,
        IReadOnlyList<DocumentHash> Hashes)
    {
        /// <summary>Whether the build may run.</summary>
        public bool MayBuild => Refusal == GateRefusal.None;
    }

    /// <summary>Asks whether a cycle's build may run.</summary>
    /// <param name="Id">The cycle to check.</param>
    public sealed record Query(CycleId Id) : IRequest<Response>;

    /// <summary>
    /// Decides whether a cycle's build may run.
    /// </summary>
    /// <remarks>
    /// This is the local gate: it defends against an agent that forgets, not one that lies. Approval is a
    /// hand-pasted entry and the gate recomputes every hash, so approve-then-edit is caught — which is the
    /// bypass that happens by accident rather than on purpose.
    /// </remarks>
    /// <param name="solution">The root of the solution being governed.</param>
    private sealed class Handler(SolutionRoot solution)
        : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);

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
                    $"Cycle {cycle.Id} is not approved: there is no {Path.GetFileName(cycle.ApprovalPath)}. Run 'sdlc draft approval {cycle.Id}' and rename the draft it writes."));
            }

            string ledgerText = await File.ReadAllTextAsync(cycle.ApprovalPath, cancellationToken)
                .ConfigureAwait(false);

            Result<ApprovalLedger> read = ApprovalLedger.Read(ledgerText, cycle.ApprovalPath);
            if (read.IsFailed)
            {
                // Refused rather than failed, and the reason is the recovery: the drafter reads this
                // verdict, so failing here would take the one command that can replace the broken file
                // down with it.
                return Verdict(cycle, GateRefusal.UnreadableLedger, string.Join(
                    Environment.NewLine,
                    [
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"Cycle {cycle.Id} is not approved: {Path.GetFileName(cycle.ApprovalPath)} cannot be read."),
                        string.Empty,
                        .. read.Errors.Select(error => "    " + error.Message),
                        string.Empty,
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"Run 'sdlc draft approval {cycle.Id}' for a replacement file to put in its place."),
                    ]));
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

            // `IsApproved` rather than the comparison written out again. The ledger already names
            // this concept and documents why the last entry is the one that counts -- a later
            // approval supersedes a refusal and a later reopen supersedes an approval -- and the
            // comparison here was a second definition of the same rule, kept correct by coincidence.
            //
            // The one thing the old spelling bought was flow analysis: `current is null || ...` left
            // the compiler able to prove `current` is not null below. `approved` is that proof made
            // explicit instead, and it cannot drift from the property because it reads it.
            if (!ledger.IsApproved)
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

            // Read once, here, and refused on before anything is inferred from them. Both of the
            // holes F-04 found were the same mistake in two places: an unreadable round was skipped
            // and an absent one was never looked for, so "could not tell" and "nobody asked" both
            // came out as "no blockers".
            Result<IReadOnlyDictionary<ArtifactKind, IReadOnlyList<FindingsRound>>> interrogations =
                await Interrogations(cycle, cancellationToken).ConfigureAwait(false);

            if (interrogations.IsFailed)
            {
                return Verdict(
                    cycle,
                    GateRefusal.UnreadableRound,
                    Unreadable(cycle, interrogations.Errors),
                    ledger,
                    hashes);
            }

            List<ArtifactKind> uninterrogated =
                [.. Cycle.PlanningDocuments.Where(kind => interrogations.Value[kind].Count == 0)];

            if (uninterrogated.Count > 0)
            {
                return Verdict(
                    cycle,
                    GateRefusal.Uninterrogated,
                    Unchallenged(cycle, uninterrogated),
                    ledger,
                    hashes);
            }

            List<string> blockers = OpenBlockers(interrogations.Value);

            if (blockers.Count > 0)
            {
                return Verdict(cycle, GateRefusal.OpenBlocker, Blocked(cycle, blockers), ledger, hashes);
            }

            IReadOnlyList<string> stale = await Stale(cycle, interrogations.Value, cancellationToken)
                .ConfigureAwait(false);

            if (stale.Count > 0)
            {
                return Verdict(cycle, GateRefusal.StaleRound, Superseded(cycle, stale), ledger, hashes);
            }

            // IsApproved is false whenever there is no standing entry, so reaching here means there
            // is one.
            GateEntry standing = ledger.Current!;

            return Verdict(
                cycle,
                GateRefusal.None,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} was approved by {standing.By} at {standing.At:u}, and all three documents match what was approved."),
                ledger,
                hashes);
        }

        /// <summary>
        /// Finds every blocking finding still open across the three planning documents.
        /// </summary>
        /// <remarks>
        /// A pure function over histories the caller has already read and already refused on. It used
        /// to read them itself and skip any it could not read, which is the defect: inferring "no
        /// blockers" from "could not tell" is how a check comes to pass for the wrong reason, and the
        /// comment that stood here claimed an unreadable round was "reported where it is read" --
        /// true of <c>status</c> and <c>suggestions</c>, and not of the two layers that decide.
        /// </remarks>
        private static List<string> OpenBlockers(
            IReadOnlyDictionary<ArtifactKind, IReadOnlyList<FindingsRound>> interrogations)
        {
            List<string> blockers = [];

            foreach (ArtifactKind kind in Cycle.PlanningDocuments)
            {
                foreach (Finding blocker in FindingsReader.StandingBlockers(interrogations[kind]))
                {
                    string qualified = string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Cycle.NameOf(kind)}.{blocker.Id}");

                    // The rule's own line between the id and the objection. This list is what a gate
                    // refusal hands back, so it is read by whoever has to decide what to do about the
                    // blocker -- and CON_10.2 requires their reason to say what has to change, which
                    // is not a judgement anybody can make from an id alone.
                    string rule = blocker.Cites?.Heading is { Length: > 0 } heading
                        ? heading + "  "
                        : string.Empty;

                    blockers.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"  {qualified}  {blocker.Cites?.Id}  {rule}{blocker.Statement}"));
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
            IReadOnlyDictionary<ArtifactKind, IReadOnlyList<FindingsRound>> interrogations,
            CancellationToken cancellationToken)
        {
            List<string> stale = [];
            Dictionary<ArtifactKind, string> hashes = [];

            foreach (ArtifactKind kind in Cycle.PlanningDocuments)
            {
                hashes[kind] = await ContentHash
                    .OfFile(cycle.PathOf(kind), cancellationToken)
                    .ConfigureAwait(false);

                // The round's own subject (D-101): a document edited after its clean round was caught by
                // nothing but the person approving the bytes. A round written before rounds recorded
                // their subject has nothing to compare, and is judged on its upstream alone as before.
                FindingsRound latest = interrogations[kind][^1];
                if (latest.SubjectSha256 is { } subject && !string.Equals(subject, hashes[kind], StringComparison.Ordinal))
                {
                    stale.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"  {Cycle.FileNameOf(kind)}: round {latest.Round} judged {ContentHash.Abbreviate(subject)}, and the document is now {ContentHash.Abbreviate(hashes[kind])}"));
                }

                if (Cycle.UpstreamOf(kind) is not { } upstream)
                {
                    continue;
                }

                // Never empty and never unreadable by the time this runs: the caller refuses on both.
                string? judged = interrogations[kind][^1].UpstreamSha256;
                string now = hashes[upstream];

                if (string.Equals(judged, now, StringComparison.Ordinal))
                {
                    continue;
                }

                stale.Add(judged is null
                    ? string.Create(
                        CultureInfo.InvariantCulture,
                        $"  {Cycle.FileNameOf(kind)}: round {interrogations[kind][^1].Round} did not record which {Cycle.FileNameOf(upstream)} it judged against")
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"  {Cycle.FileNameOf(kind)}: round {interrogations[kind][^1].Round} judged {Cycle.FileNameOf(upstream)} {ContentHash.Abbreviate(judged)}, which is now {ContentHash.Abbreviate(now)}"));
            }

            return stale;
        }

        /// <summary>
        /// Reads every planning document's findings history, failing if any one of them cannot be read.
        /// </summary>
        /// <remarks>
        /// One read for the whole verdict, so the blockers check and the staleness check cannot
        /// disagree about what a round said, and so a refusal happens before anything is inferred.
        /// </remarks>
        private static async Task<Result<IReadOnlyDictionary<ArtifactKind, IReadOnlyList<FindingsRound>>>> Interrogations(
            Cycle cycle,
            CancellationToken cancellationToken)
        {
            Dictionary<ArtifactKind, IReadOnlyList<FindingsRound>> histories = [];
            List<IError> errors = [];

            foreach (ArtifactKind kind in Cycle.PlanningDocuments)
            {
                Result<IReadOnlyList<FindingsRound>> history =
                    await FindingsReader.ReadHistory(cycle, kind, cancellationToken).ConfigureAwait(false);

                if (history.IsFailed)
                {
                    errors.AddRange(history.Errors);
                    continue;
                }

                histories[kind] = history.Value;
            }

            return errors.Count > 0
                ? Result.Fail<IReadOnlyDictionary<ArtifactKind, IReadOnlyList<FindingsRound>>>(errors)
                : Result.Ok<IReadOnlyDictionary<ArtifactKind, IReadOnlyList<FindingsRound>>>(histories);
        }

        private static string Unreadable(Cycle cycle, IReadOnlyList<IError> errors) =>
            string.Join(
                Environment.NewLine,
                [
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Cycle {cycle.Id} has a findings round that cannot be read, so the gate cannot say whether a blocking finding is open:"),
                    string.Empty,
                    .. errors.Select(error => "  " + error.Message),
                    string.Empty,
                    "This is a refusal rather than a shrug on purpose. A round that cannot be read used to be",
                    "skipped, which made every blocker for that document disappear -- and a blocking finding",
                    "cannot be waived by any mechanism, least of all by a damaged file. Repair the round, or",
                    "delete it and interrogate the artifact again.",
                ]);

        private static string Unchallenged(Cycle cycle, List<ArtifactKind> pending) =>
            string.Join(
                Environment.NewLine,
                [
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Cycle {cycle.Id} is approved, and {pending.Count} document{(pending.Count == 1 ? " has" : "s have")} never been interrogated:"),
                    string.Empty,
                    .. pending.Select(kind => "  " + Cycle.FileNameOf(kind)),
                    string.Empty,
                    "An approval rests on three documents something has argued with. A document with no round",
                    "at all has not been judged clean -- nothing has judged it. Run 'sdlc interrogate' on each",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"of those, then approve again with 'sdlc draft approval {cycle.Id}'."),
                ]);

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

        private static string Blocked(Cycle cycle, List<string> blockers) =>
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

        private static Result<Response> Verdict(
            Cycle cycle,
            GateRefusal refusal,
            string explanation,
            ApprovalLedger? ledger = null,
            IReadOnlyList<DocumentHash>? hashes = null) =>
            Result.Ok(new Response(cycle, refusal, explanation, ledger, hashes ?? []));
    }
}
