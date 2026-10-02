using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Approvals;


/// <summary>The text to paste, or what has to happen before there is any.</summary>
/// <param name="Status">The cycle's state, so a caller can report it without reading again.</param>
/// <param name="Entry">The entry to paste, when there is one to produce.</param>
/// <param name="Instructions">What to do with it, or what is in the way.</param>
/// <param name="MayApprove">Whether an entry was produced.</param>
/// <param name="Decision">Which decision the entry records.</param>
public sealed record ApprovalTextResult(
    CycleStatus Status,
    string? Entry,
    string Instructions,
    bool MayApprove,
    GateDecision Decision = GateDecision.Approved);

/// <summary>Prints the gate entry a person pastes into the approval ledger. Writes nothing.</summary>
public static class ApprovalText
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApprovalTextRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Request, ApprovalTextResult>, Handler>();
    }

    /// <summary>Asks for the ledger entry a person would paste to record a gate decision.</summary>
    /// <param name="Id">The cycle the decision is about.</param>
    /// <param name="By">
    /// Who is deciding. Supplied by the caller from the git identity rather than invented here: the entry
    /// names a person, and a tool that filled that in for itself would be writing the decision.
    /// </param>
    /// <param name="Decision">
    /// What is being recorded. A refusal is drafted on the same terms as an approval: rejecting is what a
    /// person does when they are least inclined to look a format up, and giving help to the easy case
    /// while withholding it from the hard one is the asymmetry people resent.
    /// </param>
    public sealed record Request(CycleId Id, string By, GateDecision Decision = GateDecision.Approved)
        : IRequest<ApprovalTextResult>;

    /// <summary>
    /// Prints an approval entry. Writes nothing.
    /// </summary>
    /// <remarks>
    /// A human performs the act of approval and no skill can perform it for them: this produces text, the
    /// person creates or opens <c>approval.md</c>, pastes, and saves. That is what keeps the hook rule
    /// absolute rather than conditional — every agent write to that file is denied, with no approve
    /// command to carve out an exception for.
    /// <para>
    /// It refuses while any blocking finding is open, and names them. There is no entry it could print
    /// instead: a blocking finding cannot be waived, accepted or overridden, so an approval standing over
    /// one would assert something untrue.
    /// </para>
    /// </remarks>
    /// <param name="mediator">
    /// How the cycle's status is read. It takes no solution root of its own: the only thing it
    /// needed one for was reaching the status handler, and that is now a dispatch.
    /// </param>
    private sealed class Handler(Mediator mediator)
        : IRequestHandler<Request, ApprovalTextResult>
    {
        /// <inheritdoc />
        public async Task<Result<ApprovalTextResult>> Handle(
            Request request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<CycleStatus> read = await mediator
                .Send<ReadCycleStatus.Request, CycleStatus>(
                    new ReadCycleStatus.Request(request.Id),
                    cancellationToken)
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
                    $"Cycle {status.Cycle.Id} was abandoned, and abandonment is terminal. Open a new cycle instead."), request.Decision);
            }

            // Every decision records three hashes, so all three documents have to exist whichever way the
            // gate goes. There is nothing to refuse until there is something to refuse.
            List<ArtifactState> missing = [.. status.Artifacts.Where(artifact => !artifact.Exists)];
            if (missing.Count > 0)
            {
                return Refuse(status, Missing(status, missing), request.Decision);
            }

            List<ArtifactState> uninterrogated = [.. status.Artifacts.Where(artifact => artifact.Rounds == 0)];
            if (request.Decision == GateDecision.Approved && uninterrogated.Count > 0)
            {
                return Refuse(status, Uninterrogated(status, uninterrogated), request.Decision);
            }

            // A judgement made against a document that has moved since is not a judgement of what is on
            // disk now. Like a blocker, this stops an approval and not a refusal: the staleness is often
            // exactly what the person is sending the cycle back about.
            if (request.Decision == GateDecision.Approved && status.Stale.Count > 0)
            {
                return Refuse(status, Superseded(status), request.Decision);
            }

            // An open blocker stops an approval and nothing else. Refusing a blocked cycle is the correct
            // move, so a drafter that would not help with it would push the person into writing YAML by hand
            // at exactly the moment the format is least welcome.
            if (request.Decision == GateDecision.Approved && status.OpenBlockerIds.Count > 0)
            {
                return Refuse(status, Blocked(status), request.Decision);
            }

            string entry = Entry(status, request.By, request.Decision);

            return Result.Ok(new ApprovalTextResult(
                status,
                entry,
                Instructions(status, request.Decision),
                MayApprove: true,
                request.Decision));
        }

        private static Result<ApprovalTextResult> Refuse(
            CycleStatus status,
            string why,
            GateDecision decision = GateDecision.Approved) =>
            Result.Ok(new ApprovalTextResult(status, Entry: null, why, MayApprove: false, decision));

        private static string Missing(CycleStatus status, List<ArtifactState> missing)
        {
            IEnumerable<string> names = missing.Select(artifact => Cycle.FileNameOf(artifact.Kind));

            string joined = string.Join(", ", names);
            string verb = missing.Count == 1 ? "has" : "have";

            return string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {status.Cycle.Id} cannot be approved yet: {joined} {verb} not been written. The approval covers all three planning documents as one act, so all three have to exist first.");
        }

        /// <summary>
        /// Says which artifacts were judged against a document that has changed since.
        /// </summary>
        /// <remarks>
        /// The three hashes cannot catch this. They record that three documents were read, not that the
        /// spec was derived from this intent -- so an upstream edited after a clean round leaves every
        /// approval hash matching and a judgement about a document that no longer exists.
        /// </remarks>
        private static string Superseded(CycleStatus status)
        {
            List<string> lines =
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {status.Cycle.Id} cannot be approved: {status.Stale.Count} interrogation{(status.Stale.Count == 1 ? " judged" : "s judged")} against a document that has changed since."),
                string.Empty,
                .. status.Stale.Select(artifact => string.Create(
                    CultureInfo.InvariantCulture,
                    $"    {Cycle.FileNameOf(artifact.Kind)} was judged against {Cycle.FileNameOf(Cycle.UpstreamOf(artifact.Kind)!.Value)} as it stood at round {artifact.LatestRound}")),
                string.Empty,
                "Interrogate each one again, so the round says it judged what is there now:",
                string.Empty,
                .. status.Stale.Select(artifact => string.Create(
                    CultureInfo.InvariantCulture,
                    $"    sdlc interrogate {status.Cycle.Id} {Cycle.NameOf(artifact.Kind)}")),
            ];

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Says which blockers stand in the way, and that there is no way past them but to answer them.
        /// </summary>
        /// <remarks>
        /// This is where a person arrives wanting to approve, so it is the place the hard rule has to be
        /// stated rather than merely enforced. A refusal that said only "blocked" would read as a tool
        /// being awkward and send someone looking for the flag that turns it off.
        /// </remarks>
        private static string Blocked(CycleStatus status)
        {
            IReadOnlyList<string> ids = status.OpenBlockerIds;
            string verb = ids.Count == 1 ? "finding is" : "findings are";

            List<string> lines =
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {status.Cycle.Id} cannot be approved: {ids.Count} blocking {verb} open."),
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"    {string.Join(", ", ids)}"),
                string.Empty,
                "A blocking finding cannot be waived, accepted or overridden, so there is no entry to",
                "print. Answer each one, re-interrogate, and come back:",
                string.Empty,
                .. status.Artifacts
                    .Where(artifact => artifact.OpenBlockers.Count > 0)
                    .Select(artifact => string.Create(
                        CultureInfo.InvariantCulture,
                        $"    sdlc refine {status.Cycle.Id} {Cycle.NameOf(artifact.Kind)}")),
                string.Empty,
                "If a finding cannot be answered at all, the artifact is wrong rather than incomplete.",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Abandon the cycle with 'sdlc draft abandon {status.Cycle.Id}' and open a new one."),
            ];

            return string.Join(Environment.NewLine, lines);
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
        /// Builds the entry a person pastes.
        /// </summary>
        /// <remarks>
        /// Reached only once every blocker is answered, so there is nothing to carry forward and nothing to
        /// warn about. The entry records who, when, the three hashes, and the round each hash was taken at.
        /// </remarks>
        private static string Entry(CycleStatus status, string by, GateDecision decision)
        {
            StringBuilder entry = new();

            entry.AppendLine(CultureInfo.InvariantCulture, $"  - decision: {ApprovalLedger.NameOf(decision)}");
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

            // The key is printed with nothing after it on purpose. A refusal needs a reason and nobody can
            // write it but the person refusing, so a placeholder would be worse than an empty value: the
            // ledger rejects an empty one and would accept "see above" forever.
            if (decision != GateDecision.Approved)
            {
                entry.AppendLine("    reason:");
            }

            return entry.ToString();
        }

        private static string Instructions(CycleStatus status, GateDecision decision)
        {
            bool exists = File.Exists(status.Cycle.ApprovalPath);

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
                    string.Create(CultureInfo.InvariantCulture, $"    framework_version: {Cycle.FrameworkVersion}"),
                    "    decisions:",
                    "      <the entry above>",
                    "    ---");
            }

            if (decision != GateDecision.Approved)
            {
                lines.AddRange(
                    string.Empty,
                    "Write the reason on the empty reason line. The gate rejects an entry that refuses a",
                    "cycle and says nothing about why, so an unfilled one fails rather than passing quietly.",
                    "Say what has to change, not that the finding was noted - the reason is what the author",
                    "has to work from.");
            }

            lines.AddRange(
                string.Empty,
                "Nothing here writes that file. The hook denies every agent write to it, which is what",
                "makes a pasted decision a person's act rather than a tool's.");

            return string.Join(Environment.NewLine, lines);
        }
    }
}
