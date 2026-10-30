using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Constraints;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Proposals;

/// <summary>
/// Everything the record says the solution's rules, decisions and stack are missing, in one ranked
/// report (D-104).
/// </summary>
/// <remarks>
/// <para><strong>The two reports it replaces were unreadable for opposite reasons.</strong> MarketSense's
/// constraints report was 431 KB: 754 findings that rested on no rule, listed raw. Its decisions report was
/// 36 passages copied verbatim. Clustering and wording were done by hand, by a person and an agent re-reading
/// both. This counts and groups -- what a binary can decide -- and leaves the wording to the skill, which
/// writes the proposal file a person marks.</para>
/// <para>Findings that rested on no rule are grouped by who raised them and at which stage, ranked by
/// distinct cycles, with the shortest few statements each as a sample -- a pointer for the skill, which
/// reads the rounds for the rest.</para>
/// </remarks>
public static class ProposeConvergence
{
    /// <summary>Statements quoted per group, at most.</summary>
    public const int Sample = 5;

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddProposeConvergenceRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Findings that rested on no rule, raised by one reader at one stage.</summary>
    /// <param name="Raiser">The perspective, or <c>rules</c> for the round's own reader.</param>
    /// <param name="Artifact">The stage.</param>
    /// <param name="Cycles">The distinct cycles it was raised in.</param>
    /// <param name="Raised">How many findings.</param>
    /// <param name="Statements">A sample, shortest first.</param>
    public sealed record Group(string Raiser, ArtifactKind Artifact, IReadOnlyList<CycleId> Cycles, int Raised, IReadOnlyList<string> Statements);

    /// <summary>A reopen: a person taking an approval back, with the reason they gave.</summary>
    /// <param name="At">When.</param>
    /// <param name="By">Who.</param>
    /// <param name="Reason">What they wrote, which is usually a decision with its alternative already named.</param>
    public sealed record Reopen(DateTimeOffset At, string By, string? Reason);

    /// <summary>One verdict a cycle's verify reached, read back from the commit that wrote it.</summary>
    /// <param name="Commit">The commit.</param>
    /// <param name="At">When.</param>
    /// <param name="Outcome">What it said: pass, blocked, escalated, or null when it would not parse.</param>
    /// <param name="NotMet">The criteria it found not met.</param>
    public sealed record Verdict(string Commit, DateTimeOffset At, string? Outcome, IReadOnlyList<int> NotMet);

    /// <summary>How one cycle went: rounds per document, reopens, and every verdict.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Rounds">How many rounds each document took.</param>
    /// <param name="Reopens">Every reopen, oldest first.</param>
    /// <param name="Verdicts">Every verdict its verify reached, oldest first.</param>
    public sealed record CycleRecord(
        CycleId Cycle,
        IReadOnlyDictionary<ArtifactKind, int> Rounds,
        IReadOnlyList<Reopen> Reopens,
        IReadOnlyList<Verdict> Verdicts);

    /// <summary>The report.</summary>
    /// <param name="Rules">What the findings say about the rules.</param>
    /// <param name="Groups">The findings that rested on no rule, grouped and ranked.</param>
    /// <param name="Decisions">What approved cycles stated.</param>
    /// <param name="Stack">What the declared stack does not mention.</param>
    /// <param name="Record">
    /// How each cycle went (D-112). The findings say what was raised; this says what it cost -- rounds,
    /// reopens, verdicts -- and two of the three, the reopen reasons and every verdict but the last, are
    /// read nowhere else.
    /// </param>
    public sealed record Response(
        ProposeConstraints.Response Rules,
        IReadOnlyList<Group> Groups,
        ProposeDecisions.Response Decisions,
        StackCoverage.Unmentioned Stack,
        IReadOnlyList<CycleRecord>? Record = null);

    /// <summary>Asks for the report.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>Renders the report.</summary>
    /// <param name="response">The report.</param>
    /// <param name="proposals">Where the skill writes the proposal file a person marks.</param>
    /// <returns>The text.</returns>
    public static string Render(Response response, string proposals)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();
        text.AppendLine("Convergence: what the record says the rules, decisions and stack are missing");
        text.AppendLine();
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Rules   next free {response.Rules.NextId}, {response.Rules.InForce} in force, {response.Rules.Cycles} cycles read"));

        foreach (Recurrence recurrence in response.Rules.Recurrences)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {recurrence.Cited} cited in {recurrence.Cycles.Count} cycles -- {recurrence.Heading}"));
        }

        if (response.Rules.Recurrences.Count == 0)
        {
            text.AppendLine("  no rule cited in three or more cycles");
        }

        // Below the bar for a rule, and often exactly where a decision is missing: a rule argued over
        // dozens of findings inside one cycle is an argument the cycle settled and nothing recorded, so the
        // next cycle has it again. In MarketSense's first cycle the proof rule drew 36 plan blockers and the
        // failure-record rule a dozen, and both ended in choices that belonged in the decision record.
        foreach (Recurrence argued in (response.Rules.Cited ?? [])
            .Where(recurrence => recurrence.Cycles.Count < ProposeConstraints.Bar)
            .OrderByDescending(recurrence => recurrence.Findings.Count)
            .Take(Sample))
        {
            // A rule of the framework's own is not one this solution can mend: a local rule cannot
            // replace it, and a decision settles only this solution's side. So it says where the rest
            // goes, rather than leaving a framework rule's cost with nowhere to be written down.
            string settles = argued.Cited.Tier == ConstraintTier.Global
                ? "A decision settles this solution's side; the framework's rule is the framework's, so if the cost is the rule's, a note for ai-sdlc"
                : "A decision that settles the argument, more often than a rule";

            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  argued: {argued.Cited} in {argued.Findings.Count} findings over {argued.Cycles.Count} cycle{(argued.Cycles.Count == 1 ? string.Empty : "s")}, one standing {argued.Longest} round{(argued.Longest == 1 ? string.Empty : "s")} -- {argued.Heading}. {settles}"));
        }

        text.AppendLine();
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Rested on no rule   {response.Groups.Sum(group => group.Raised)} findings in {response.Groups.Count} groups, most cycles first"));

        foreach (Group group in response.Groups)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {group.Raiser} on the {Cycle.NameOf(group.Artifact)}: {group.Raised} findings, {group.Cycles.Count} cycle{(group.Cycles.Count == 1 ? string.Empty : "s")}"));

            foreach (string statement in group.Statements)
            {
                text.AppendLine("    - " + statement);
            }
        }

        foreach (Stopped stopped in response.Rules.Stopped)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  stopped {stopped.Cycle}: {stopped.Reason ?? stopped.Unreadable}"));
        }

        // Which readers raise what a later reader takes back. About the reader, not the solution: no
        // proposal rests on these, and a perspective high here is one whose method wants a look.
        if (response.Rules.Withdrawn is { Count: > 0 } withdrawn)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  withdrawn: {string.Join(", ", withdrawn.GroupBy(finding => finding.Perspective ?? "rules").OrderByDescending(group => group.Count()).Select(group => string.Create(CultureInfo.InvariantCulture, $"{group.Key} {group.Count()}")))} -- raised, then taken back by a later reader"));
        }

        Record(text, response.Record ?? []);

        text.AppendLine();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Decisions   next free {response.Decisions.NextId}, {response.Decisions.Standing.Count} recorded, {response.Decisions.Candidates.Count} passages from approved cycles"));

        foreach (IGrouping<CycleId, Candidate> cycle in response.Decisions.Candidates.GroupBy(candidate => candidate.Cycle))
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {cycle.Key}: {cycle.Count()} passages -- 'sdlc propose decisions' prints them in full"));
        }

        text.AppendLine();
        text.AppendLine("Stack");

        foreach (StackDefault missing in response.Stack.Defaults)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  not mentioned: {missing.Concern} -- the profile says {missing.Choice}"));
        }

        foreach (StackCoverage.Rule rule in response.Stack.Rules)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  not mentioned: {rule.Id} -- {rule.Statement}"));
        }

        if (response.Stack.Defaults.Count == 0 && response.Stack.Rules.Count == 0)
        {
            text.AppendLine("  every row and rule this release ships is answered");
        }

        text.AppendLine();
        text.AppendLine("The proposals go in " + proposals + ", one heading each, for a person to mark:");
        text.AppendLine();
        text.AppendLine("  ## Rules        ### [ ] add — <statement>   ### [ ] supersede LOC_02 — <statement>   ### [ ] reword LOC_01 — <statement>");
        text.AppendLine("  ## Decisions    ### [ ] add — <statement>   ### [ ] supersede DEC_02 — <statement>");
        text.AppendLine("  ## Stack        ### [ ] set <concern> — <choice, or none>");
        text.AppendLine();
        text.AppendLine("  Under each heading, the entry's clauses as they would be pasted, and '> ' lines for the evidence.");
        text.AppendLine("  There is no remove: supersede a rule or decision, and set a stack concern to none.");
        text.AppendLine("  Then 'sdlc draft convergence " + proposals + "' writes the drafts from what was marked [x].");

        return text.ToString();
    }

    /// <summary>
    /// How each cycle went: rounds per document, reopens with their reasons, and every verdict.
    /// </summary>
    /// <remarks>
    /// The reopens are where a person already decided something and wrote down what they refused, and
    /// the verdicts are where a criterion kept failing -- both are decisions the findings never hold.
    /// </remarks>
    private static void Record(StringBuilder text, IReadOnlyList<CycleRecord> record)
    {
        if (record.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine("Cycles   rounds per document, reopens, and every verdict the verify reached");

        foreach (CycleRecord cycle in record)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {cycle.Cycle}: {(cycle.Rounds.Count == 0 ? "no rounds" : string.Join(", ", cycle.Rounds.OrderBy(entry => entry.Key).Select(entry => string.Create(CultureInfo.InvariantCulture, $"{Cycle.NameOf(entry.Key)} {entry.Value}"))))}; {cycle.Reopens.Count} reopen{(cycle.Reopens.Count == 1 ? string.Empty : "s")}, {cycle.Verdicts.Count} verdict{(cycle.Verdicts.Count == 1 ? string.Empty : "s")}"));

            foreach (Reopen reopen in cycle.Reopens)
            {
                text.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"    reopened {reopen.At:yyyy-MM-dd} by {reopen.By}: {reopen.Reason?.ReplaceLineEndings(" ") ?? "(no reason)"}"));
            }

            if (cycle.Verdicts.Count > 0)
            {
                text.AppendLine("    verdicts: " + string.Join(" -> ", cycle.Verdicts.Select(verdict => verdict.NotMet.Count == 0
                    ? verdict.Outcome ?? "?"
                    : string.Create(CultureInfo.InvariantCulture, $"{verdict.Outcome ?? "?"} on {string.Join(",", verdict.NotMet)}"))));

                // A criterion that failed more than one verdict is an argument about what it asks, and
                // where a decision -- or a reopen of the spec -- usually sits.
                foreach (IGrouping<int, int> repeated in cycle.Verdicts
                    .SelectMany(verdict => verdict.NotMet)
                    .GroupBy(number => number)
                    .Where(group => group.Count() > 1)
                    .OrderByDescending(group => group.Count()))
                {
                    text.AppendLine(string.Create(
                        CultureInfo.InvariantCulture,
                        $"    criterion {repeated.Key} not met in {repeated.Count()} verdicts"));
                }
            }
        }
    }

    private sealed class Handler(Mediator mediator, SolutionRoot solution, IRepository repository)
        : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<ProposeConstraints.Response> rules = await mediator
                .Send<ProposeConstraints.Query, ProposeConstraints.Response>(new ProposeConstraints.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (rules.IsFailed)
            {
                return rules.ToResult<Response>();
            }

            Result<ProposeDecisions.Response> decisions = await mediator
                .Send<ProposeDecisions.Query, ProposeDecisions.Response>(new ProposeDecisions.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (decisions.IsFailed)
            {
                return decisions.ToResult<Response>();
            }

            string stack = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, Cycle.TechStackFileName);
            StackCoverage.Unmentioned unmentioned = File.Exists(stack)
                ? StackCoverage.Of(await File.ReadAllTextAsync(stack, cancellationToken).ConfigureAwait(false))
                : StackCoverage.Unmentioned.Nothing;

            List<Group> groups =
            [
                .. rules.Value.Uncited
                    .GroupBy(finding => (Raiser: finding.Perspective ?? "rules", finding.Artifact))
                    .Select(group =>
                    {
                        List<CycleId> cycles = [.. group.Select(finding => finding.Cycle).Distinct()];
                        return new Group(
                            group.Key.Raiser,
                            group.Key.Artifact,
                            cycles,
                            group.Count(),
                            [
                                .. group
                                    .GroupBy(finding => finding.Statement, StringComparer.Ordinal)
                                    .Select(same => same.MaxBy(finding => finding.Argued)!)
                                    .OrderByDescending(finding => finding.Argued)
                                    .ThenBy(finding => finding.Statement.Length)
                                    .Take(Sample)
                                    .Select(finding => string.Create(
                                        CultureInfo.InvariantCulture,
                                        $"[{finding.Cycle} {finding.Id}, {finding.Argued} round{(finding.Argued == 1 ? string.Empty : "s")}] {finding.Statement.ReplaceLineEndings(" ")}")),
                            ]);
                    })
                    .OrderByDescending(group => group.Cycles.Count)
                    .ThenByDescending(group => group.Raised),
            ];

            List<CycleRecord> record = [];

            foreach (Cycle cycle in CycleIndex.All(solution.Path))
            {
                record.Add(await Recorded(cycle, cancellationToken).ConfigureAwait(false));
            }

            return Result.Ok(new Response(rules.Value, groups, decisions.Value, unmentioned, record));
        }

        /// <summary>Reads how one cycle went, from its ledgers, its approval and its verify's history.</summary>
        private async Task<CycleRecord> Recorded(Cycle cycle, CancellationToken cancellationToken)
        {
            Dictionary<ArtifactKind, int> rounds = [];

            foreach (ArtifactKind kind in Enum.GetValues<ArtifactKind>())
            {
                Result<IReadOnlyList<FindingsRound>> history = await FindingsReader
                    .ReadHistory(cycle, kind, cancellationToken)
                    .ConfigureAwait(false);

                if (history.IsSuccess && history.Value.Count > 0)
                {
                    rounds[kind] = history.Value.Count;
                }
            }

            List<Reopen> reopens = [];

            if (File.Exists(cycle.ApprovalPath)
                && ApprovalLedger.Read(
                    await File.ReadAllTextAsync(cycle.ApprovalPath, cancellationToken).ConfigureAwait(false),
                    cycle.ApprovalPath) is { IsSuccess: true } ledger)
            {
                reopens.AddRange(ledger.Value.Decisions
                    .Where(entry => entry.Decision == GateDecision.Reopened)
                    .Select(entry => new Reopen(entry.At, entry.By, entry.Reason)));
            }

            return new CycleRecord(cycle.Id, rounds, reopens, Verdicts(cycle));
        }

        /// <summary>
        /// Every verdict the cycle's verify reached, from whichever branch holds the most of them.
        /// </summary>
        /// <remarks>
        /// Only the last verdict is on disk; each earlier one was a commit of <c>verify.md</c>. A cycle
        /// merged by squash keeps one commit on the integration branch and its history on its own
        /// branch, so both are asked, and the longer answer is the history.
        /// </remarks>
        private List<Verdict> Verdicts(Cycle cycle)
        {
            string path = Cycle.SdlcDirectoryName + "/" + Cycle.CyclesDirectoryName + "/" + cycle.Id + "/"
                + Cycle.FileNameOf(ArtifactKind.Verify);

            string[] references =
            [
                "HEAD",
                "cycle/" + cycle.Id + "/build",
                "origin/cycle/" + cycle.Id + "/build",
                "cycle/" + cycle.Id,
                "origin/cycle/" + cycle.Id,
            ];

            IReadOnlyList<(string Commit, DateTimeOffset When)> longest = references
                .Where(reference => reference == "HEAD" || repository.RefExists(solution.Path, reference))
                .Select(reference => repository.CommitsTouching(solution.Path, reference, path))
                .OrderByDescending(commits => commits.Count)
                .FirstOrDefault() ?? [];

            List<Verdict> verdicts = [];

            foreach ((string commit, DateTimeOffset when) in longest)
            {
                // A commit that deleted the verdict, to record a new run's evidence, has nothing to read.
                if (repository.ReadAt(solution.Path, commit, path) is not { } text)
                {
                    continue;
                }

                string? outcome = Frontmatter.Read(text, path) is { IsSuccess: true } split
                    ? Frontmatter.Key(split.Value.Yaml, "outcome")
                    : null;

                List<int> notMet =
                [
                    .. ArtifactSections.NumberedItems(text, "Criteria")
                        .Where(item => item.Value.TrimStart().StartsWith("Not met", StringComparison.OrdinalIgnoreCase))
                        .Select(item => item.Key)
                        .Order(),
                ];

                verdicts.Add(new Verdict(commit, when, outcome, notMet));
            }

            return verdicts;
        }
    }
}
