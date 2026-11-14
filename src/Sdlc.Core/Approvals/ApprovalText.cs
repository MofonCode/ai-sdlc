using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Deviations;
using Sdlc.Core.Findings;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Approvals;

/// <summary>Prints the gate entry a person pastes into the approval ledger. Writes nothing.</summary>
public static class ApprovalText
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApprovalTextRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>The file to put in place, or what has to happen before there is one.</summary>
    /// <param name="Status">The cycle's state, so a caller can report it without reading again.</param>
    /// <param name="Entry">The entry alone, when there is one to produce.</param>
    /// <param name="FileText">
    /// The complete ledger file this decision would produce, ready to stand on its own.
    /// </param>
    /// <param name="DraftPath">Where that file is written for a person to rename.</param>
    /// <param name="CarriesForward">
    /// How many entries already in the ledger the file carries ahead of the new one. Non-zero means the
    /// rename replaces a ledger whose history is reproduced verbatim inside the draft.
    /// </param>
    /// <param name="Instructions">What to do with it, or what is in the way.</param>
    /// <param name="MayApprove">Whether an entry was produced.</param>
    /// <param name="Decision">Which decision the entry records.</param>
    public sealed record Response(
        ReadCycleStatus.Response Status,
        string? Entry,
        string? FileText,
        string? DraftPath,
        int CarriesForward,
        string Instructions,
        bool MayApprove,
        GateDecision Decision = GateDecision.Approved);

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
    /// <param name="Answers">Deviations to draft already answered, or null to draft every one unanswered.</param>
    public sealed record Query(CycleId Id, string By, GateDecision Decision = GateDecision.Approved, DeviationAnswers? Answers = null)
        : IRequest<Response>;

    /// <summary>
    /// The deviations a draft carries already answered (D-162): the person said which, and copying that into
    /// the draft by hand is what an agent kept getting wrong. The rename still records it, and only a person
    /// makes that.
    /// </summary>
    /// <param name="Accept">The deviations drafted with <c>accepted: true</c>.</param>
    /// <param name="Decline">The deviations drafted declined, each with the note.</param>
    /// <param name="Note">Why the declined ones are declined.</param>
    public sealed record DeviationAnswers(IReadOnlyList<string> Accept, IReadOnlyList<string> Decline, string? Note);

    /// <summary>The label a plan's list of the choices it makes opens with.</summary>
    public const string DecisionsLabel = "Decisions this plan makes:";

    /// <summary>
    /// The choices a plan lists at the head of <c>Shape and departures</c>, one line each.
    /// </summary>
    /// <remarks>
    /// Both of MarketSense's reopens were a decision the person found in the plan only after approving
    /// it -- Aspire left out, the chart tables drawn by hand -- because the plan was a hundred kilobytes
    /// and the choice was a clause in it. The list is what the approval draft puts in front of them, so
    /// the choices are read before the rename rather than discovered after it.
    /// </remarks>
    /// <param name="plan">The plan.</param>
    /// <returns>The listed choices, or an empty list when the plan lists none.</returns>
    public static IReadOnlyList<string> PlanDecisions(string plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!ArtifactSections.Read(plan).TryGetValue("Shape and departures", out IReadOnlyList<string>? lines))
        {
            return [];
        }

        List<string> decisions = [];
        bool inList = false;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (!inList)
            {
                inList = string.Equals(trimmed, DecisionsLabel, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                decisions.Add(trimmed[2..].Trim());
            }
            else if (trimmed.Length > 0 && decisions.Count > 0 && line.StartsWith("  ", StringComparison.Ordinal))
            {
                decisions[^1] += " " + trimmed;
            }
            else if (trimmed.Length > 0 || decisions.Count > 0)
            {
                break;
            }
        }

        return decisions;
    }

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
    /// <param name="clock">
    /// What stamps the entry. Injected rather than read from a static, which is what lets a test assert
    /// the stamped line instead of asserting around it — and it is the rule this framework bans the
    /// alternative to in every solution it scaffolds.
    /// </param>
    private sealed class Handler(Mediator mediator, TimeProvider clock, SolutionRoot solution, IRepository repository)
        : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<ReadCycleStatus.Response> read = await mediator
                .Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(
                    new ReadCycleStatus.Query(request.Id),
                    cancellationToken)
                .ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            ReadCycleStatus.Response status = read.Value;

            if (status.Cycle.IsAbandoned)
            {
                return Refuse(status, string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {status.Cycle.Id} was abandoned, and abandonment is terminal. Open a new cycle instead."), request.Decision);
            }

            DeviationsState deviations = await CycleDeviations.Read(status.Cycle, cancellationToken).ConfigureAwait(false);

            // A reopen folds the accepted deviations into the amended plan and gives the declined ones as its
            // reason, so every one has to be answered first: a deviation nobody answered would be neither.
            if (request.Decision == GateDecision.Reopened && deviations.Pending.Count > 0)
            {
                return Refuse(status, Unanswered(status, deviations), request.Decision);
            }

            // An approved cycle whose build recorded deviations: what the person approves now is each of them,
            // one by one, beside the approval that stands. Same command, no flag of its own.
            if (request.Decision == GateDecision.Approved
                && status.Gate.Ledger?.IsApproved == true
                && (deviations.Pending.Count > 0 || (deviations.Recorded && deviations.States.Count == 0 && deviations.Problems.Count > 0)))
            {
                return await Deviations(status, deviations, request.By, request.Answers, cancellationToken).ConfigureAwait(false);
            }

            if (request.Answers is { } answers && (answers.Accept.Count > 0 || answers.Decline.Count > 0))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {status.Cycle.Id} has no deviation waiting for an answer, so there is nothing to accept or decline."));
            }

            // D-123: once the plan is on the integration branch, a reopen is an amendment to what the team and
            // the factory read, and it travels by a plan pull request of its own. Drafted on the build branch,
            // it would reach the integration branch only with the code, and the factory would keep reading the
            // plan the person just withdrew.
            if (request.Decision == GateDecision.Reopened
                && await AdmittedUnderBuild(status.Cycle.Id, cancellationToken).ConfigureAwait(false) is ({ } admitted, { } build))
            {
                return Refuse(status, string.Create(
                    CultureInfo.InvariantCulture,
                    $"The plan for {status.Cycle.Id} is on {admitted}, so it is reopened there and not on this build branch. Run 'sdlc branch {status.Cycle.Id} plan', reopen, correct and approve it again on that branch, and open its pull request with 'pr --plan'. Then this build is yours to keep or drop: {AmendedPlan.Choices(build, admitted)}."), request.Decision);
            }

            // Every decision records three hashes, so all three documents have to exist whichever way the
            // gate goes. There is nothing to refuse until there is something to refuse.
            List<ReadCycleStatus.ArtifactState> missing = [.. status.Artifacts.Where(artifact => !artifact.Exists)];
            if (missing.Count > 0)
            {
                return Refuse(status, Missing(status, missing), request.Decision);
            }

            List<ReadCycleStatus.ArtifactState> uninterrogated = [.. status.Artifacts.Where(artifact => artifact.Rounds == 0)];
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

            string entry = Entry(status, request.By, request.Decision, clock.GetUtcNow());

            Result<LedgerFile> file = await Compose(status, entry, cancellationToken)
                .ConfigureAwait(false);

            if (file.IsFailed)
            {
                return file.ToResult<Response>();
            }

            string draftPath = status.Cycle.ApprovalDraftPath(ApprovalLedger.NameOf(request.Decision));

            IReadOnlyList<string>? decisions = null;
            IReadOnlyList<string> departing = [];

            // Departures first and apart from the plan's other choices (D-139): approving one sets a recorded
            // decision aside for this cycle, and that is the line a person must not miss among the rest.
            if (request.Decision == GateDecision.Approved)
            {
                IReadOnlyList<Departure> departures = await Departures.Of(status.Cycle, cancellationToken).ConfigureAwait(false);
                departing = Departures.Report(departures, await Departures.Recorded(status.Cycle.SolutionRoot, cancellationToken).ConfigureAwait(false));
                decisions = [.. PlanDecisions(await File.ReadAllTextAsync(status.Cycle.PathOf(ArtifactKind.Plan), cancellationToken).ConfigureAwait(false))
                    .Where(made => !made.StartsWith(Departures.Opening, StringComparison.Ordinal))];
            }

            return Result.Ok(new Response(
                status,
                entry,
                file.Value.Text,
                draftPath,
                file.Value.CarriesForward,
                Instructions(status, request.Decision, draftPath, file.Value, decisions, departing),
                MayApprove: true,
                request.Decision));
        }


        /// <summary>
        /// Drafts the person's answer to every deviation nobody has answered: each one listed, accepted false
        /// and an empty note, for the person to set to true or to decline by writing why.
        /// </summary>
        private async Task<Result<Response>> Deviations(
            ReadCycleStatus.Response status,
            DeviationsState deviations,
            string by,
            DeviationAnswers? answers,
            CancellationToken cancellationToken)
        {
            if (deviations.States.Count == 0)
            {
                return Refuse(status, string.Join(Environment.NewLine,
                [
                    string.Create(CultureInfo.InvariantCulture, $"Cycle {status.Cycle.Id}'s {DeviationsFile.FileName} cannot be read, so there is nothing to answer yet:"),
                    string.Empty,
                    .. deviations.Problems.Select(problem => "    " + problem),
                    string.Empty,
                    "The build or remediation that wrote it corrects it; then draft again.",
                ]), GateDecision.Deviations);
            }

            DeviationAnswers given = answers ?? new DeviationAnswers([], [], null);
            HashSet<string> pending = [.. deviations.Pending.Select(state => state.Deviation.Id)];

            if (given.Accept.Intersect(given.Decline, StringComparer.Ordinal).FirstOrDefault() is { } both)
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"{both} is named both accepted and declined. Name it once."));
            }

            if (given.Accept.Concat(given.Decline).FirstOrDefault(id => !pending.Contains(id)) is { } unknown)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{unknown} is not a deviation waiting for an answer. Those waiting: {string.Join(", ", pending.Order(StringComparer.Ordinal))}."));
            }

            if (given.Decline.Count > 0 && string.IsNullOrWhiteSpace(given.Note))
            {
                return Result.Fail<Response>("A declined deviation is redone, and the note is what the build redoes it from. Say why with --note.");
            }

            string entry = DeviationsEntry(by, clock.GetUtcNow(), deviations.Pending, given);

            Result<LedgerFile> file = await Compose(status, entry, cancellationToken).ConfigureAwait(false);

            if (file.IsFailed)
            {
                return file.ToResult<Response>();
            }

            string draftPath = status.Cycle.ApprovalDraftPath(ApprovalLedger.NameOf(GateDecision.Deviations));

            List<string> review =
            [
                string.Create(CultureInfo.InvariantCulture, $"The build bent the approved documents in {deviations.Pending.Count} place{(deviations.Pending.Count == 1 ? string.Empty : "s")}. Read each one, in {DeviationsFile.FileName}:"),
                string.Empty,
            ];

            foreach (DeviationState state in deviations.Pending)
            {
                Deviation deviation = state.Deviation;
                review.Add("  " + DeviationsFile.Line(deviation) + (state.Changed ? "  - changed since you answered it" : string.Empty));
                review.Add("    departs from: " + deviation.Document + (deviation.Section.Length > 0 ? ", " + deviation.Section : string.Empty));
                review.Add("    blocker:      " + deviation.Blocker);
                review.Add("    instead:      " + deviation.Instead);
                review.Add(string.Empty);
            }

            if (deviations.Problems.Count > 0)
            {
                review.Add("What is wrong with them, which the pull request check refuses until it is fixed:");
                review.AddRange(deviations.Problems.Select(problem => "    " + problem));
                review.Add(string.Empty);
            }

            review.AddRange(
            [
                "For each one in the draft: set 'accepted: true' to accept it, or leave it false and write",
                "why in its note to decline it. False with no note is not reviewed, and the pull request",
                "check refuses until it is answered. A declined deviation is redone; to rethink the plan",
                string.Create(CultureInfo.InvariantCulture, $"instead, decline it and then reopen with 'sdlc draft approval {status.Cycle.Id} --reopen'."),
                string.Empty,
            ]);

            string instructions = string.Join(Environment.NewLine, review)
                + Environment.NewLine
                + Instructions(status, GateDecision.Deviations, draftPath, file.Value, null, []);

            return Result.Ok(new Response(
                status,
                entry,
                file.Value.Text,
                draftPath,
                file.Value.CarriesForward,
                instructions,
                MayApprove: true,
                GateDecision.Deviations));
        }

        /// <summary>The deviations entry: who, when, and each deviation with its hash, unanswered.</summary>
        private static string DeviationsEntry(string by, DateTimeOffset at, IReadOnlyList<DeviationState> pending, DeviationAnswers answers)
        {
            StringBuilder entry = new();

            entry.AppendLine(CultureInfo.InvariantCulture, $"  - decision: {ApprovalLedger.NameOf(GateDecision.Deviations)}");
            entry.AppendLine(CultureInfo.InvariantCulture, $"    by: {by}");
            entry.AppendLine(CultureInfo.InvariantCulture, $"    at: {at:yyyy-MM-ddTHH:mm:ssZ}");
            entry.AppendLine("    deviations:");

            // The note is printed with its quotes already in place, so a colon the person types cannot turn the
            // line into something else. False is the default, so anything nobody looked at blocks.
            foreach (DeviationState state in pending)
            {
                Deviation deviation = state.Deviation;
                entry.AppendLine(CultureInfo.InvariantCulture, $"      - id: {deviation.Id}");
                entry.AppendLine(CultureInfo.InvariantCulture, $"        gist: {Quoted(deviation.Gist)}");
                entry.AppendLine(CultureInfo.InvariantCulture, $"        kind: {Deviation.NameOf(deviation.Kind)}");
                entry.AppendLine(CultureInfo.InvariantCulture, $"        sha256: {deviation.Sha256}");
                bool declined = answers.Decline.Contains(deviation.Id, StringComparer.Ordinal);
                entry.AppendLine(answers.Accept.Contains(deviation.Id, StringComparer.Ordinal) ? "        accepted: true" : "        accepted: false");
                entry.AppendLine("        note: " + (declined ? Quoted(answers.Note!.Trim()) : "\"\""));
            }

            return entry.ToString();
        }

        private static string Quoted(string text) =>
            "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

        /// <summary>Says which deviations a reopen is waiting on.</summary>
        private static string Unanswered(ReadCycleStatus.Response status, DeviationsState deviations) => string.Join(
            Environment.NewLine,
            [
                string.Create(CultureInfo.InvariantCulture, $"Cycle {status.Cycle.Id} cannot be reopened yet: {deviations.Pending.Count} deviation{(deviations.Pending.Count == 1 ? " is" : "s are")} unanswered."),
                string.Empty,
                .. deviations.Pending.Select(state => "    " + DeviationsFile.Line(state.Deviation)),
                string.Empty,
                "A reopen writes the accepted ones into the amended plan and gives the declined ones as its",
                "reason, so answer each one first:",
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"    sdlc draft approval {status.Cycle.Id}"),
            ]);

        /// <summary>A complete ledger file, and how much history it reproduces.</summary>
        /// <param name="Text">The file's whole contents.</param>
        /// <param name="CarriesForward">How many existing entries it carries ahead of the new one.</param>
        /// <param name="Replacing">Whether a ledger that cannot be read is being replaced.</param>
        private sealed record LedgerFile(string Text, int CarriesForward, bool Replacing);

        /// <summary>
        /// Builds the whole file rather than a fragment to splice.
        /// </summary>
        /// <remarks>
        /// <strong>One block at one indentation, because splicing two is where this broke.</strong> The
        /// printer used to emit the entry at its real indentation and the header at a four-space display
        /// indent, with a <c>&lt;the entry above&gt;</c> placeholder between them — so placing a first
        /// approval meant dedenting one block, not the other, and substituting by hand. A header at
        /// indent two above a <c>decisions:</c> at indent nought is valid-looking and parses as nothing,
        /// which is exactly what arrived.
        /// <para>
        /// An existing ledger is carried forward <strong>verbatim</strong>, fence to fence, rather than
        /// re-emitted from the parsed entries. A refusal's reason is prose a person wrote, and
        /// round-tripping it through a YAML emitter is how a quoted colon or a line break becomes a
        /// different document. It also means the rename cannot lose history: the replacement already
        /// contains every entry the file it replaces had, which the old "paste at the end" instruction
        /// relied on the person to get right.
        /// </para>
        /// </remarks>
        private static async Task<Result<LedgerFile>> Compose(
            ReadCycleStatus.Response status,
            string entry,
            CancellationToken cancellationToken)
        {
            string path = status.Cycle.ApprovalPath;

            if (!File.Exists(path))
            {
                return Result.Ok(new LedgerFile(Fresh(status, entry), CarriesForward: 0, Replacing: false));
            }

            string existing = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            Result<FrontmatterDocument> split = Frontmatter.Read(existing, path);
            Result<ApprovalLedger> ledger = ApprovalLedger.Read(existing, path);

            // A ledger that will not parse is a failed paste rather than history. Carrying its bytes
            // forward would append a valid entry to an invalid document and produce a second invalid one,
            // so the draft starts clean and says so.
            if (split.IsFailed || ledger.IsFailed)
            {
                return Result.Ok(new LedgerFile(Fresh(status, entry), CarriesForward: 0, Replacing: true));
            }

            string body = string.IsNullOrWhiteSpace(split.Value.Body)
                ? string.Empty
                : split.Value.Body.TrimEnd() + Environment.NewLine;

            string text = string.Join(
                Environment.NewLine,
                [
                    "---",
                    .. split.Value.Yaml.TrimEnd().Split('\n'),
                    .. entry.TrimEnd().Split(Environment.NewLine),
                    "---",
                ]) + Environment.NewLine + body;

            return Result.Ok(new LedgerFile(
                text,
                ledger.Value.Decisions.Count,
                Replacing: false));
        }

        /// <summary>The whole file, for a cycle whose ledger does not yet stand.</summary>
        private static string Fresh(ReadCycleStatus.Response status, string entry) => string.Join(
            Environment.NewLine,
            [
                "---",
                string.Create(CultureInfo.InvariantCulture, $"cycle: {status.Cycle.Id}"),
                "decisions:",
                .. entry.TrimEnd().Split(Environment.NewLine),
                "---",
            ]) + Environment.NewLine;

        /// <summary>
        /// The integration branch's reference, when this working copy is the cycle's build branch and its plan
        /// is already on the integration branch, with the build branch's name; else nulls.
        /// </summary>
        private async Task<(string? Admitted, string? Build)> AdmittedUnderBuild(CycleId id, CancellationToken cancellationToken)
        {
            if (!CycleBranch.TryParse(repository.CurrentBranch(solution.Path), out CycleBranch? branch)
                || branch.Id != id
                || !branch.IsBuild)
            {
                return (null, null);
            }

            string integration = await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken).ConfigureAwait(false);
            string plan = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, id.ToString(), Cycle.FileNameOf(ArtifactKind.Plan));

            return AmendedPlan.IntegrationRef(repository, solution.Path, integration) is { } reference
                && repository.PathExistsAt(solution.Path, reference, plan)
                ? (reference, branch.ToString())
                : (null, null);
        }

        private static Result<Response> Refuse(
            ReadCycleStatus.Response status,
            string why,
            GateDecision decision = GateDecision.Approved) =>
            Result.Ok(new Response(
                status,
                Entry: null,
                FileText: null,
                DraftPath: null,
                CarriesForward: 0,
                why,
                MayApprove: false,
                decision));

        private static string Missing(ReadCycleStatus.Response status, List<ReadCycleStatus.ArtifactState> missing)
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
        private static string Superseded(ReadCycleStatus.Response status)
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
        private static string Blocked(ReadCycleStatus.Response status)
        {
            IReadOnlyList<string> ids = status.OpenBlockerIds;
            string verb = ids.Count == 1 ? "finding is" : "findings are";

            List<string> lines =
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {status.Cycle.Id} cannot be approved: {ids.Count} blocking {verb} open."),
                string.Empty,
                .. status.Artifacts.SelectMany(artifact => artifact.OpenBlockers.SelectMany(finding => BlockerLines.Of(
                    string.Create(CultureInfo.InvariantCulture, $"{Cycle.NameOf(artifact.Kind)}.{finding.Id}"),
                    finding,
                    "    "))),
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

        private static string Uninterrogated(ReadCycleStatus.Response status, List<ReadCycleStatus.ArtifactState> pending)
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
        /// warn about. The entry records who, when, the three hashes, and the round each ledger had reached.
        /// </remarks>
        private static string Entry(
            ReadCycleStatus.Response status,
            string by,
            GateDecision decision,
            DateTimeOffset at)
        {
            StringBuilder entry = new();

            entry.AppendLine(CultureInfo.InvariantCulture, $"  - decision: {ApprovalLedger.NameOf(decision)}");
            entry.AppendLine(CultureInfo.InvariantCulture, $"    by: {by}");
            entry.AppendLine(CultureInfo.InvariantCulture, $"    at: {at:yyyy-MM-ddTHH:mm:ssZ}");
            entry.AppendLine("    hashes:");

            // The key carries its colon before the padding, so the columns line up and the result is
            // still YAML. Padding a bare name and relying on whitespace would produce a block that reads
            // correctly and parses as nothing.
            foreach (ReadCycleStatus.ArtifactState artifact in status.Artifacts)
            {
                string key = Cycle.FileNameOf(artifact.Kind) + ":";
                entry.AppendLine(CultureInfo.InvariantCulture, $"      {key,-11} {artifact.Hash}");
            }

            // The argument as well as the documents (D-107): the round each ledger had reached, and its
            // hash, so rewriting the history behind this decision means changing this file too.
            if (status.Artifacts.All(artifact => artifact.LatestEntry is not null))
            {
                entry.AppendLine("    rounds:");

                foreach (ReadCycleStatus.ArtifactState artifact in status.Artifacts)
                {
                    string key = Cycle.FileNameOf(artifact.Kind) + ":";
                    entry.AppendLine(CultureInfo.InvariantCulture, $"      {key,-11} {{ round: {artifact.LatestRound}, sha256: {artifact.LatestEntry} }}");
                }
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

        /// <summary>
        /// Says how to put the decision in place.
        /// </summary>
        /// <remarks>
        /// One act, and it is a rename. There is no clipboard step left to get wrong, and no second
        /// indentation convention on the page — the draft file beside the ledger <em>is</em> the ledger,
        /// one rename away.
        /// <para>The rename is written from the repository root, which is where a person types it and where
        /// every other command here runs. It named the bare files, and cycle-02's person typed it at the
        /// root and got <c>cannot stat</c>.</para>
        /// </remarks>
        private static string Instructions(
            ReadCycleStatus.Response status,
            GateDecision decision,
            string draftPath,
            LedgerFile file,
            IReadOnlyList<string>? decisions,
            IReadOnlyList<string> departing)
        {
            string draft = Absolute(draftPath);
            string target = Absolute(status.Cycle.ApprovalPath);
            string ledger = Path.GetFileName(status.Cycle.ApprovalPath);
            bool overwriting = file.CarriesForward > 0 || file.Replacing;

            List<string> lines = [];

            // D-137: drafting one is allowed and placing one is honoured, so the person is told what it
            // changes rather than refused -- a refusal or a reopen is how a team under `none` stops a cycle.
            if (status.Gate.Declared)
            {
                lines.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"This solution does not require an approval ('approval: none' in {Cycle.SolutionFileName}). One placed here is honoured in full: once it is in place the gate reads it, and anything but an approval stops the build."));
                lines.Add(string.Empty);
            }

            if (departing.Count > 0)
            {
                lines.AddRange(departing);
                lines.Add(string.Empty);
            }

            if (decisions is not null)
            {
                lines.Add(decisions.Count == 0
                    ? "The plan lists no decisions it makes. Read its 'Shape and departures' before you rename: that is where a choice you did not ask for would be."
                    : string.Create(CultureInfo.InvariantCulture, $"This approves the {decisions.Count} decision{(decisions.Count == 1 ? string.Empty : "s")} the plan says it makes. Read them before you rename:"));

                lines.AddRange(decisions.Select(made => "  - " + made));
                lines.Add(string.Empty);
            }

            lines.AddRange(
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Written to {draft}. Rename it, from any directory, to record the decision. In bash or zsh:"),
                string.Empty,
                overwriting
                    ? string.Create(CultureInfo.InvariantCulture, $"    mv -f {draft} {target}")
                    : string.Create(CultureInfo.InvariantCulture, $"    mv {draft} {target}"),
                string.Empty,
                "In PowerShell:",
                string.Empty,
                overwriting
                    ? string.Create(CultureInfo.InvariantCulture, $"    Move-Item -Force {draft} {target}")
                    : string.Create(CultureInfo.InvariantCulture, $"    Move-Item {draft} {target}"),
            ]);

            if (file.CarriesForward > 0)
            {
                lines.AddRange(
                    string.Empty,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"That replaces a ledger holding {file.CarriesForward} decision{(file.CarriesForward == 1 ? string.Empty : "s")}, and loses none of them:"),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"the draft reproduces every one of them above the new entry, fence to fence, exactly as"),
                    "they stand. The ledger is append-only, so check they are all there before you rename.");
            }

            if (file.Replacing)
            {
                lines.AddRange(
                    string.Empty,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The {ledger} already there cannot be read, so nothing in it was carried forward. That"),
                    "is what a mis-placed entry looks like rather than a decision somebody took, and this",
                    "draft stands on its own. Read it before you rename, and if it did hold a decision worth",
                    "keeping, recover that from git rather than from the broken file.");
            }

            if (decision is not GateDecision.Approved and not GateDecision.Deviations)
            {
                lines.AddRange(
                    string.Empty,
                    "Write the reason on the empty reason line before you rename it. The gate rejects an entry",
                    "that refuses a cycle and says nothing about why, so an unfilled one fails rather than",
                    "passing quietly. Say what has to change, not that the finding was noted - the reason is",
                    "what the author has to work from.");
            }

            lines.AddRange(
                string.Empty,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The rename is the decision, and it is yours. The hook denies every agent write to {ledger}"),
                "and every agent command that would move anything onto it, with no exception and nothing to",
                "configure. A draft nobody renamed records nothing.");

            return string.Join(Environment.NewLine, lines);
        }

        // Absolute, because the draft is often in another worktree than the one the person's shell is in, and
        // a path from the root ran from the wrong root (D-162). Forward slashes, because both shells the rename
        // is written for accept them; quoted when a space would split it.
        private static string Absolute(string path)
        {
            string full = Path.GetFullPath(path).Replace('\\', '/');
            return full.Contains(' ', StringComparison.Ordinal) ? "\"" + full + "\"" : full;
        }
    }
}
