using System.Globalization;
using System.Text.RegularExpressions;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Proposals;

/// <summary>
/// One rule that findings have cited in several cycles.
/// </summary>
/// <remarks>
/// Counted by <em>distinct cycles</em> rather than by findings. Three findings against one rule inside a
/// single cycle is one artifact going round the loop; the same rule blocking three separate cycles is a
/// solution that keeps walking into the same wall, and only the second says a rule is missing.
/// </remarks>
/// <param name="Cited">The rule the findings cited.</param>
/// <param name="Heading">
/// The rule's own line, as the latest finding to cite it recorded. Frozen in the round rather than
/// resolved from the library (D-63), so this is the wording the reader actually judged against.
/// </param>
/// <param name="Cycles">Which cycles, oldest first.</param>
/// <param name="Findings">The finding ids, so a proposal can be chased back to what raised it.</param>
public sealed record Recurrence(
    ConstraintId Cited,
    string? Heading,
    IReadOnlyList<CycleId> Cycles,
    IReadOnlyList<string> Findings);

/// <summary>
/// A finding that rested on no rule at all.
/// </summary>
/// <remarks>
/// The richest material here and the part no count can reach. A blocker cites exactly one constraint,
/// so every one of these is a suggestion — somebody noticed something real that nothing forbade, which
/// is the definition of a rule that does not exist yet. Grouping them is a judgement about prose and
/// belongs to the skill; this hands over the statements and keeps its opinions to itself.
/// </remarks>
/// <param name="Cycle">The cycle it was raised in.</param>
/// <param name="Artifact">Which artifact it was raised against.</param>
/// <param name="Id">The finding's id within that cycle and artifact.</param>
/// <param name="Statement">What the reader said was wrong.</param>
/// <param name="Perspective">The perspective whose reader raised it, or null for the round's own reader.</param>
public sealed record Uncited(CycleId Cycle, ArtifactKind Artifact, string Id, string Statement, string? Perspective = null);

/// <summary>
/// How often one perspective raised something no rule forbade, counted by distinct cycle.
/// </summary>
/// <remarks>
/// <strong>A count of where to look, not a claim that the items are one problem.</strong> A perspective
/// has no rules until its suggestions earn them (D-90), so this is how a person sees which perspective
/// keeps finding things: a perspective at <see cref="ProposeConstraints.Bar"/> or more cycles is where a
/// rule is most likely missing. Whether its statements are the same objection worded differently is
/// reading, and the skill does it.
/// </remarks>
/// <param name="Perspective">The perspective.</param>
/// <param name="Cycles">The cycles it raised something in, oldest first.</param>
/// <param name="Raised">How many findings and noticed lines it accounts for.</param>
public sealed record PerspectiveTally(string Perspective, IReadOnlyList<CycleId> Cycles, int Raised);

/// <summary>
/// A cycle that stopped, and the reason somebody wrote by hand.
/// </summary>
/// <remarks>
/// §16.1 keeps a stopped cycle's directory so the objection that killed it stays on the record, which
/// makes this the highest-grade evidence in a repository: a person's own words about something that went
/// wrong badly enough to end the work. It carries no citation and no structure, so it feeds the judged
/// layer and never the count — which is why the renderer puts it under its own heading rather than
/// mixing it with arithmetic.
/// </remarks>
/// <param name="Cycle">The cycle that stopped.</param>
/// <param name="Reason">The reason, or null when the record is there and could not be read.</param>
/// <param name="Unreadable">The path of a record that would not parse, so somebody can go and look.</param>
public sealed record Stopped(CycleId Cycle, string? Reason, string? Unreadable = null);

/// <summary>
/// Reads what a solution's whole history says about the rules it is missing. Writes nothing.
/// </summary>
/// <remarks>
/// <para><strong>Mined backward, because a constraint is earned rather than chosen.</strong> The corpus
/// is every findings round on the default branch — blocking and suggestion alike, closed included,
/// which is the deliberate difference from <c>sdlc suggestions</c>. That command lists what is
/// <em>open</em> and non-blocking, and a closed blocker that recurred across three cycles is the
/// strongest evidence a rule is missing, so the one command can never see the other's best material.
/// </para>
///
/// <para><strong>Facts here, reading comprehension in the skill.</strong> This counts, groups and
/// allocates; it proposes no wording. Which recurrences are really the same underlying problem, which
/// candidates an analyzer could decide instead, and what a rule should say are judgements, and a binary
/// that made them would be the tool proposing the rule — which is the thing <c>constraints.md</c> is
/// hand-written to prevent. What makes an evidenced proposal legitimate at all is that it arrives with
/// its evidence: a tool may propose a rule when it brings the record, and may not shape a rule somebody
/// just typed.</para>
///
/// <para><strong>Nothing is remembered between runs.</strong> There is no marker saying which cycles
/// have been absorbed, because such a marker is the completion-certificate trap of §19 item 3 — a mistaken
/// one silently loses the learning and looks exactly like a correct one. Like <c>CycleIndex</c>, this
/// re-derives from the directories every time.</para>
/// </remarks>
public static class ProposeConstraints
{
    /// <summary>
    /// How many distinct cycles make a pattern.
    /// </summary>
    /// <remarks>
    /// Two is coincidence, three is a pattern. There is deliberately no second threshold on how much
    /// history a solution must have before this will report: three blocked cycles out of a solution's
    /// first four <em>is</em> a pattern, and a floor would suppress the most useful report this can
    /// produce at exactly the moment the rules are cheapest to add.
    /// </remarks>
    public const int Bar = 3;

    /// <summary>
    /// The heading under which a person records a proposal they have refused.
    /// </summary>
    /// <remarks>
    /// Without somewhere for a refusal to live, the same two candidates come back on every run forever
    /// and the report trains people to skim it. The section is hand-written in <c>constraints.md</c>,
    /// like everything else in that file, and the constraint parser ignores it — it recognises only
    /// <c>## LOC_nn — statement</c> headings, so a prose section beside them costs nothing.
    /// </remarks>
    public const string RejectedHeading = "Considered and rejected";

    // Built from the heading above rather than spelled again. The two were written out separately once,
    // which is one place too many: renaming the constant would have left the reader looking for the old
    // words and reporting that nobody had refused anything, which is indistinguishable from the truth.
    private static readonly Regex _rejectedSection = new(
        @"^##\s+"
            + Regex.Escape(RejectedHeading).Replace(@"\ ", @"\s+", StringComparison.Ordinal)
                .Replace(" ", @"\s+", StringComparison.Ordinal)
            + @"\s*$(?<body>.*?)(?=^##\s|\z)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline
            | RegexOptions.Singleline,
        TimeSpan.FromSeconds(1));

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddProposeConstraintsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>
    /// Asks what the solution's history says about its rules.
    /// </summary>
    /// <remarks>
    /// Takes no arguments. Narrowing it to one cycle would answer a question nobody has — recurrence
    /// across cycles is the entire signal, and a single cycle is the sample size that produces
    /// overfitted rules.
    /// </remarks>
    public sealed record Query : IRequest<Response>;

    /// <summary>
    /// What the record holds, as counts and ids.
    /// </summary>
    /// <param name="NextId">
    /// The next free solution constraint id, one past the highest ever used. A refused proposal must
    /// leave no trace in the sequence, which is why the renderer labels candidates and never numbers
    /// them: a gap is the record of a constraint that was removed (§7.4), and a gap left by a proposal
    /// nobody accepted would read as exactly that.
    /// </param>
    /// <param name="InForce">How many of this solution's own rules still bind.</param>
    /// <param name="Superseded">
    /// The ones kept on the page and out of force. Listed so a report can say what is already retired
    /// rather than proposing to retire it twice.
    /// </param>
    /// <param name="Rejected">What a person has already refused, verbatim, one entry per line.</param>
    /// <param name="Recurrences">Rules cited in <see cref="Bar"/> or more distinct cycles.</param>
    /// <param name="BelowBar">
    /// Rules cited in more than one cycle but fewer than <see cref="Bar"/>. Reported as a count rather
    /// than as candidates, because a coincidence presented beside a pattern is read as a weaker pattern.
    /// </param>
    /// <param name="Uncited">Findings that rested on no rule, which is where a missing rule shows.</param>
    /// <param name="Stopped">Cycles that ended, with the reasons somebody wrote.</param>
    /// <param name="Cycles">How many cycles the corpus held, so a thin report says why it is thin.</param>
    /// <param name="Noticed">
    /// What the builds wrote down and left alone, from their commits. Suggestions too, in the other store:
    /// a perspective asking for something a plan did not describe is a tagged <c>Noticed:</c> line.
    /// </param>
    /// <param name="Perspectives">
    /// Each perspective that raised something, counted by distinct cycle across both stores, most first.
    /// </param>
    public sealed record Response(
        ConstraintId NextId,
        int InForce,
        IReadOnlyList<ConstraintId> Superseded,
        IReadOnlyList<string> Rejected,
        IReadOnlyList<Recurrence> Recurrences,
        IReadOnlyList<Recurrence> BelowBar,
        IReadOnlyList<Uncited> Uncited,
        IReadOnlyList<Stopped> Stopped,
        int Cycles,
        IReadOnlyList<Noticed> Noticed,
        IReadOnlyList<PerspectiveTally> Perspectives)
    {
        /// <summary>
        /// Whether the history yielded anything to argue about.
        /// </summary>
        /// <remarks>
        /// What separates this operation's two modes. With material, the command reports it and a person
        /// decides; with none, the command prints an empty entry at the next free id with the three
        /// admission tests beside it — proposing the shape when it cannot propose the rule. A young
        /// solution and a well-behaved one both land in the second mode, which is why a person adding a
        /// rule by hand on day one is served by the same command rather than by a second one.
        /// <para>
        /// <see cref="BelowBar"/> is deliberately not material. A coincidence is not evidence, and a
        /// command that printed candidates from two cycles would be teaching the bar is negotiable.
        /// </para>
        /// </remarks>
        public bool HasMaterial => Recurrences.Count > 0 || Uncited.Count > 0 || Stopped.Count > 0 || Noticed.Count > 0;
    }

    /// <summary>
    /// Walks every cycle and folds what the readers said into counts.
    /// </summary>
    /// <param name="solution">The root of the solution being read.</param>
    /// <param name="repository">Git, for the <c>Noticed:</c> lines the builds left in their commits.</param>
    private sealed class Handler(SolutionRoot solution, IRepository repository) : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<RuleSet> rules = await Rules(cancellationToken).ConfigureAwait(false);

            // A malformed constraints document is reported rather than worked around, for the reason the
            // old drafter gave: allocating an id from a file this cannot read risks handing back one
            // already taken, and a reused id is the single mistake the monotonic rule exists to make
            // impossible.
            if (rules.IsFailed)
            {
                return rules.ToResult<Response>();
            }

            IReadOnlyList<Cycle> cycles = CycleIndex.All(solution.Path);

            List<Cited> cited = [];
            List<Uncited> uncited = [];
            List<Stopped> stopped = [];
            List<Noticed> noticed = [];
            List<IError> errors = [];
            string integration = await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken)
                .ConfigureAwait(false);

            foreach (Cycle cycle in cycles)
            {
                foreach (ArtifactKind kind in Enum.GetValues<ArtifactKind>())
                {
                    Result<IReadOnlyList<FindingsRound>> history = await FindingsReader
                        .ReadHistory(cycle, kind, cancellationToken)
                        .ConfigureAwait(false);

                    if (history.IsFailed)
                    {
                        errors.AddRange(history.Errors);
                        continue;
                    }

                    Sort(cycle, kind, history.Value, cited, uncited);
                }

                noticed.AddRange(Noticed.OfCycle(repository, solution.Path, cycle.Id, integration));

                if (cycle.IsAbandoned)
                {
                    stopped.Add(await Reason(cycle, cancellationToken).ConfigureAwait(false));
                }
            }

            if (errors.Count > 0)
            {
                return Result.Fail<Response>(errors);
            }

            List<Recurrence> grouped = [.. Group(cited)];

            return Result.Ok(new Response(
                rules.Value.NextId,
                rules.Value.InForce,
                rules.Value.Superseded,
                rules.Value.Rejected,
                [.. grouped.Where(recurrence => recurrence.Cycles.Count >= Bar)],
                [.. grouped.Where(recurrence => recurrence.Cycles.Count is > 1 and < Bar)],
                uncited,
                stopped,
                cycles.Count,
                noticed,
                [.. Tally(uncited, noticed)]));
        }

        /// <summary>Counts each perspective's suggestions by distinct cycle, across both stores.</summary>
        private static IEnumerable<PerspectiveTally> Tally(List<Uncited> uncited, List<Noticed> noticed) => uncited
            .Where(finding => finding.Perspective is not null)
            .Select(finding => (Perspective: finding.Perspective!, finding.Cycle))
            .Concat(noticed
                .Where(line => line.Perspective is not null)
                .Select(line => (Perspective: line.Perspective!, line.Cycle)))
            .GroupBy(entry => entry.Perspective, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PerspectiveTally(
                group.Key,
                [.. group.Select(entry => entry.Cycle).Distinct().OrderBy(id => id.ToString(), StringComparer.Ordinal)],
                group.Count()))
            .OrderByDescending(tally => tally.Cycles.Count)
            .ThenBy(tally => tally.Perspective, StringComparer.Ordinal);

        /// <summary>
        /// Every mention of every finding, flattened into the two piles that matter.
        /// </summary>
        /// <remarks>
        /// <strong>Every round, not the standing fold.</strong> <c>sdlc suggestions</c> folds a history
        /// down to what is still open, which is right for a list of work to pick up and wrong here: a
        /// blocker raised in round one and answered in round two is a problem this solution had, and
        /// answering it is what makes it evidence rather than an outstanding task. Folding first would
        /// throw away every recurrence that was dealt with properly — which is most of them.
        /// <para>
        /// A finding keeps its id across rounds, so one objection is counted once per artifact by id,
        /// taking the last round that mentioned it. Counting mentions would make a stubborn artifact
        /// look like a solution-wide pattern.
        /// </para>
        /// </remarks>
        private static void Sort(
            Cycle cycle,
            ArtifactKind kind,
            IReadOnlyList<FindingsRound> rounds,
            List<Cited> cited,
            List<Uncited> uncited)
        {
            IEnumerable<Finding> once = rounds
                .SelectMany(round => round.Findings.Select(finding => (round.Round, finding)))
                .GroupBy(mention => mention.finding.Id, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(mention => mention.Round).First().finding);

            foreach (Finding finding in once)
            {
                if (finding.Cites?.Id is { } raw && ConstraintId.TryParse(raw, out ConstraintId? id))
                {
                    cited.Add(new Cited(id.Value, finding.Cites.Heading, cycle.Id, finding.Id));
                    continue;
                }

                // Withdrawn means a later reader retracted the objection, so there was nothing wrong
                // after all. Counting it would propose a rule against a problem the record already
                // says did not exist.
                if (finding.Disposition != Disposition.Withdrawn)
                {
                    uncited.Add(new Uncited(cycle.Id, kind, finding.Id, finding.Statement, finding.Perspective));
                }
            }
        }

        /// <summary>Folds the citations into one entry per rule, counted by distinct cycle.</summary>
        private static IEnumerable<Recurrence> Group(List<Cited> cited) => cited
            .GroupBy(entry => (entry.Id.Tier, entry.Id.Number))
            .Select(group => new Recurrence(
                group.First().Id,
                group.Select(entry => entry.Heading).LastOrDefault(heading => !string.IsNullOrWhiteSpace(heading)),
                [.. group.Select(entry => entry.Cycle).Distinct().OrderBy(id => id.ToString(), StringComparer.Ordinal)],
                [.. group.Select(entry => entry.Finding).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]))
            .OrderByDescending(recurrence => recurrence.Cycles.Count)
            .ThenBy(recurrence => recurrence.Cited.ToString(), StringComparer.Ordinal);

        /// <summary>
        /// Why one cycle stopped.
        /// </summary>
        /// <remarks>
        /// A record that will not parse is named rather than thrown away, and it does not fail the
        /// report. This is prose a person wrote by hand under no schema's eye, and refusing to say
        /// anything about a solution's history because one such file lacks a timestamp would lose the
        /// other nine reasons to protect a field nothing here reads.
        /// </remarks>
        private static async Task<Stopped> Reason(Cycle cycle, CancellationToken cancellationToken)
        {
            string text = await File
                .ReadAllTextAsync(cycle.AbandonedPath, cancellationToken)
                .ConfigureAwait(false);

            Result<AbandonRecord> read = AbandonRecord.Read(text, cycle.AbandonedPath);

            return read.IsSuccess
                ? new Stopped(cycle.Id, read.Value.Reason)
                : new Stopped(cycle.Id, Reason: null, cycle.AbandonedPath);
        }

        /// <summary>This solution's own rules, and what a person has already refused.</summary>
        private async Task<Result<RuleSet>> Rules(CancellationToken cancellationToken)
        {
            string path = Path.Combine(
                solution.Path,
                Cycle.SdlcDirectoryName,
                FileConstraintDocuments.SolutionConstraintsFileName);

            if (!File.Exists(path))
            {
                return Result.Ok(new RuleSet(
                    ConstraintId.Of(ConstraintTier.Solution, 1),
                    InForce: 0,
                    [],
                    []));
            }

            string markdown = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            Result<IReadOnlyList<Constraint>> read = ConstraintParser.Parse(markdown, path);

            if (read.IsFailed)
            {
                return read.ToResult<RuleSet>();
            }

            IReadOnlyList<Constraint> existing = read.Value;

            // One past the highest ever used, never the first gap. A gap is the record of a constraint
            // that was removed, and filling it would make an old finding's citation read as the new rule.
            int next = existing.Count == 0
                ? 1
                : existing.Max(constraint => constraint.Id.Number) + 1;

            return Result.Ok(new RuleSet(
                ConstraintId.Of(ConstraintTier.Solution, next),
                existing.Count(constraint => constraint.IsInForce),
                [.. existing.Where(constraint => !constraint.IsInForce).Select(constraint => constraint.Id)],
                Rejected(markdown)));
        }

        /// <summary>
        /// The refusals a person has recorded, one per line.
        /// </summary>
        /// <remarks>
        /// Read as lines rather than parsed into a shape. A refusal is a sentence somebody wrote to stop
        /// a proposal coming back, and a schema over it would turn the cheapest thing in the file into
        /// another form to fill in.
        /// </remarks>
        private static IReadOnlyList<string> Rejected(string markdown)
        {
            Match section = _rejectedSection.Match(markdown);

            if (!section.Success)
            {
                return [];
            }

            return
            [
                .. section.Groups["body"].Value
                    .ReplaceLineEndings()
                    .Split(Environment.NewLine)
                    .Select(line => line.TrimStart('-', '*', ' ', '\t').Trim())
                    .Where(line => line.Length > 0),
            ];
        }

        /// <summary>One finding's citation, before the citations are folded by rule.</summary>
        private sealed record Cited(ConstraintId Id, string? Heading, CycleId Cycle, string Finding);

        /// <summary>What <c>constraints.md</c> holds today.</summary>
        private sealed record RuleSet(
            ConstraintId NextId,
            int InForce,
            IReadOnlyList<ConstraintId> Superseded,
            IReadOnlyList<string> Rejected);
    }
}
