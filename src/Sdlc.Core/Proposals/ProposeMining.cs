using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Areas;
using Sdlc.Core.Constraints;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Deviations;
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
public static class ProposeMining
{
    /// <summary>Statements quoted per group, at most.</summary>
    public const int Sample = 5;

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddProposeMiningRequestHandler(this IServiceCollection services)
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
    /// <param name="Usage">What its readers cost, when its rounds recorded it (D-121).</param>
    /// <param name="Deviations">Every deviation its build recorded, with where each stands and the rules behind it.</param>
    public sealed record CycleRecord(
        CycleId Cycle,
        IReadOnlyDictionary<ArtifactKind, int> Rounds,
        IReadOnlyList<Reopen> Reopens,
        IReadOnlyList<Verdict> Verdicts,
        UsageLedger? Usage = null,
        IReadOnlyList<DeviationRecord>? Deviations = null);

    /// <summary>One deviation a cycle recorded, as mining tallies it.</summary>
    /// <param name="Id">Its id.</param>
    /// <param name="Kind">What it bent.</param>
    /// <param name="Standing">Where it stands: accepted, declined, unanswered, or taken in by a reopen.</param>
    /// <param name="Rules">The rules its blocker named; empty when it named none.</param>
    public sealed record DeviationRecord(string Id, DeviationKind Kind, DeviationStanding Standing, IReadOnlyList<string> Rules);

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
    /// <param name="OverBudget">
    /// The standing decisions and this solution's rules in force whose title or a clause runs past its word
    /// budget, each with its overrun: candidates for a <c>reword</c> or a superseding entry (D-149).
    /// </param>
    /// <param name="Areas">
    /// What the cycles say about the areas: the names plans proposed, how often each area would have matched,
    /// and changed files no area covers (D-150).
    /// </param>
    /// <param name="Mined">
    /// The mining ledger: the cycles already mined, and the concerns watched below the bar, whose counts carry
    /// across sittings because the folders they came from are gone (D-152).
    /// </param>
    /// <param name="Suggestions">
    /// The open suggestions of the finished cycles not mined yet: each is taken further, declined with its reason,
    /// or ignored, and an ignored one is tallied by concern in the ledger, since nothing else keeps it (D-153).
    /// </param>
    /// <param name="Sitting">The finished cycles not mined yet that this sitting reads and records.</param>
    /// <param name="Limited">Whether the sitting was limited to cycles named on the command line (D-166).</param>
    public sealed record Response(
        ProposeConstraints.Response Rules,
        IReadOnlyList<Group> Groups,
        ProposeDecisions.Response Decisions,
        StackCoverage.Unmentioned Stack,
        IReadOnlyList<CycleRecord>? Record = null,
        IReadOnlyList<string>? OverBudget = null,
        AreaHistory.Reading? Areas = null,
        Mining.MinedLedger? Mined = null,
        IReadOnlyList<Suggestion>? Suggestions = null,
        IReadOnlyList<CycleId>? Sitting = null,
        bool Limited = false);

    /// <summary>Asks for the report.</summary>
    /// <param name="Cycles">
    /// The finished cycles not mined yet that this sitting reads, or null for all of them. A backlog is mined a
    /// few cycles at a time, and the rest wait for a later sitting.
    /// </param>
    public sealed record Query(IReadOnlyList<CycleId>? Cycles = null) : IRequest<Response>;

    /// <summary>Renders the report.</summary>
    /// <param name="response">The report.</param>
    /// <param name="proposals">Where the skill writes the proposal file a person marks.</param>
    /// <returns>The text.</returns>
    public static string Render(Response response, string proposals)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();
        text.AppendLine("Mining: what the record says the rules, decisions and stack are missing");
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
        DeviatedBy(text, response.Record ?? []);

        text.AppendLine();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Decisions   next free {response.Decisions.NextId}, {response.Decisions.Standing.Count} recorded, {response.Decisions.Candidates.Count} passages from approved cycles"));

        foreach (IGrouping<CycleId, Candidate> cycle in response.Decisions.Candidates.GroupBy(candidate => candidate.Cycle))
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {cycle.Key}: {cycle.Count()} passages -- 'sdlc propose decisions' prints them in full"));
        }

        if (response.OverBudget is { Count: > 0 } overBudget)
        {
            text.AppendLine();
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Over the word budget   {overBudget.Count} entries past a {EntryBudget.Headline}-word title or {EntryBudget.Clause} words a clause -- reword a rule, supersede a decision"));

            foreach (string entry in overBudget)
            {
                text.AppendLine("  " + entry);
            }
        }

        Areas(text, response.Areas);
        Suggested(text, response.Suggestions ?? [], response.Mined?.Ignored ?? []);

        // What earlier sittings left: the cycles already mined, and the concerns watched below the bar, which
        // a cycle mined now may carry over it (D-152).
        if (response.Mined is { } mined && (mined.Cycles.Count > 0 || mined.Watch.Count > 0))
        {
            text.AppendLine();
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Mined before   {mined.Cycles.Count} cycles in .sdlc/{Mining.MinedLedger.FileName}, {mined.Watch.Count} concerns watched"));

            foreach (Mining.Watched watched in mined.Watch)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  watched: {watched.Gist} -- {watched.Cycles.Count} of {ProposeConstraints.Bar} cycles ({string.Join(", ", watched.Cycles)})"));
            }

            foreach (Mining.MinedCycle husk in mined.Cycles.Where(husk => response.Record?.Any(record => record.Cycle.ToString() == husk.Cycle) == true))
            {
                text.AppendLine("  " + husk.Cycle + " is mined and still here: a husk, waiting for its folder to be deleted");
            }
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

        if (response.Limited)
        {
            text.AppendLine();
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"This sitting   {string.Join(", ", response.Sitting ?? [])} -- the cycles' findings, record and suggestions above are theirs alone; the rule counts are the whole record's. Finished cycles not named wait for a later sitting (D-166)."));
        }

        text.AppendLine();
        text.AppendLine("The proposals go in " + proposals + ", one heading each, for a person to mark:");
        text.AppendLine();
        text.AppendLine("  ## Rules        ### [ ] add — <statement>   ### [ ] supersede LOC_02 — <statement>   ### [ ] reword LOC_01 — <statement>");
        text.AppendLine("  ## Decisions    ### [ ] add — <statement>   ### [ ] supersede DEC_02 — <statement>");
        text.AppendLine("  ## Stack        ### [ ] set <concern> — <choice, or none>");
        text.AppendLine("  ## Areas        ### [ ] add <name> — <what it is>   ### [ ] rename <old> <new> — <why>   ### [ ] merge <from> into <to> — <why>");
        text.AppendLine("                  ### [ ] remove <name> — <why>   ### [ ] files <name> — <globs>   ### [ ] tag DEC_04 — <area, area, or none>");
        text.AppendLine("  ## Successors   ### [ ] add <slug> — <what it makes possible>, with **Succeeds:**, **Outcome:** and **Why it matters:** beneath");
        text.AppendLine("  ## Watch        - <a concern below the bar> — <cycle>, <cycle>");
        text.AppendLine("  ## Declined     - <suggestion id>: <why>");
        text.AppendLine("  ## Ignored      - <the concern, worded as it was ignored before> — <cycle>, <cycle>");
        text.AppendLine("  ## Cycles       copied as written: 'mine record' records the cycles listed here and no others");

        foreach (CycleId cycle in response.Sitting ?? [])
        {
            text.AppendLine("                  - " + cycle);
        }

        text.AppendLine();
        text.AppendLine("  Under each heading, the entry's clauses as they would be pasted, and '> ' lines for the evidence.");
        text.AppendLine("  There is no remove: supersede a rule or decision, and set a stack concern to none.");
        text.AppendLine("  Then 'sdlc draft mining " + proposals + "' writes the drafts from what was marked [x],");
        text.AppendLine("  and once they are renamed into place, 'sdlc mine record " + proposals + "' records the sitting and names the husks.");

        return text.ToString();
    }

    /// <summary>
    /// The open suggestions of the cycles being mined, and the concerns earlier sittings ignored (D-153).
    /// </summary>
    /// <remarks>
    /// Each suggestion is answered one of three ways. Taken further, it becomes a successor and lives on as a file;
    /// declined, its reason is kept in the sitting; ignored, nothing about it survives its folder but the tally.
    /// The earlier ignored concerns are listed in their own words so an ignored suggestion is worded the same way
    /// and counted with them, and one at the bar is not ignored again.
    /// </remarks>
    private static void Suggested(StringBuilder text, IReadOnlyList<Suggestion> suggestions, IReadOnlyList<Mining.Watched> ignored)
    {
        if (suggestions.Count == 0 && ignored.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Suggestions   {suggestions.Count} open in {suggestions.Select(suggestion => suggestion.Cycle).Distinct().Count()} finished cycles -- take each further, decline it with why, or ignore it"));

        foreach (Suggestion suggestion in suggestions
            .OrderBy(suggestion => suggestion.Cycle.ToString(), StringComparer.Ordinal)
            .ThenBy(suggestion => suggestion.Finding.Id, StringComparer.Ordinal))
        {
            string raiser = suggestion.Finding.Perspective is { } perspective ? " [" + perspective + "]" : string.Empty;
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {suggestion.Cycle}/{suggestion.Finding.Id}{raiser} on the {string.Join(", ", suggestion.Documents.Select(Cycle.NameOf))}: {suggestion.Finding.Headline}"));
        }

        foreach (Mining.Watched concern in ignored.OrderByDescending(concern => concern.Cycles.Count))
        {
            string bar = concern.Cycles.Count >= ProposeConstraints.Bar
                ? " -- at the bar: propose it, make it a successor, or decline it, and do not ignore it again"
                : string.Empty;
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  ignored before, {concern.Cycles.Count} of {ProposeConstraints.Bar} cycles: {concern.Gist} ({string.Join(", ", concern.Cycles)}){bar}"));
        }
    }

    /// <summary>
    /// What the cycles say about the areas (D-150): names plans proposed, at the three-cycle bar or below it;
    /// how often each defined area would have matched a plan; and changed files no area covers.
    /// </summary>
    private static void Areas(StringBuilder text, AreaHistory.Reading? areas)
    {
        if (areas is null || (areas.Candidates.Count == 0 && areas.Defined.Count == 0))
        {
            return;
        }

        text.AppendLine();
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Areas   {areas.Defined.Count} defined, {areas.Candidates.Count} names proposed, {areas.Plans} plans replayed"));

        foreach (AreaHistory.Candidate candidate in areas.Candidates)
        {
            string bar = candidate.Cycles.Count >= ProposeConstraints.Bar ? "at the bar" : "below the bar";
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  proposed: {candidate.Name} in {candidate.Cycles.Count} cycle{(candidate.Cycles.Count == 1 ? string.Empty : "s")} ({bar}) -- would match {candidate.Plans} of {areas.Plans} plans -- {string.Join(", ", candidate.Files)}"));
        }

        foreach (AreaHistory.Replay replay in areas.Defined)
        {
            string note = areas.Plans > 0 && replay.Plans == areas.Plans ? " -- every plan touches it, so it hides nothing" : replay.Plans == 0 ? " -- no plan touched it" : string.Empty;
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  defined: {replay.Name} matched {replay.Plans} of {areas.Plans} plans{note}"));
        }

        foreach (string file in areas.Unmatched.Take(20))
        {
            text.AppendLine("  changed, in no area: " + file);
        }

        if (areas.Unmatched.Count > 20)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  ...and {areas.Unmatched.Count - 20} more changed files in no area"));
        }
    }

    /// <summary>
    /// Which rules keep causing deviations, across every cycle read: the rule each deviation's blocker named,
    /// most first. A rule behind deviations in several cycles is one the plans keep failing to anticipate.
    /// </summary>
    private static void DeviatedBy(StringBuilder text, IReadOnlyList<CycleRecord> record)
    {
        List<(CycleId Cycle, DeviationRecord Deviation)> all = [.. record.SelectMany(cycle => (cycle.Deviations ?? []).Select(deviation => (cycle.Cycle, deviation)))];

        if (all.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Deviations   {all.Count} across {all.Select(entry => entry.Cycle).Distinct().Count()} cycles, by the rule behind them"));

        foreach (IGrouping<string, (CycleId Cycle, DeviationRecord Deviation)> rule in all
            .SelectMany(entry => entry.Deviation.Rules.Count == 0 ? ["no rule named"] : entry.Deviation.Rules, (entry, rule) => (Rule: rule, entry))
            .GroupBy(pair => pair.Rule, pair => pair.entry, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {rule.Key}: {rule.Count()} in {rule.Select(entry => entry.Cycle).Distinct().Count()} cycle{(rule.Select(entry => entry.Cycle).Distinct().Count() == 1 ? string.Empty : "s")}, {rule.Count(entry => entry.Deviation.Standing == DeviationStanding.Declined)} declined"));
        }
    }

    /// <summary>
    /// How each cycle went: rounds per document, reopens with their reasons, every deviation, and every verdict.
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

            // What it cost beside what it took, per cycle and per document (D-121).
            if (cycle.Usage?.Summarise(null) is { } spent)
            {
                text.AppendLine("    spent " + spent.Describe());

                foreach (ArtifactKind kind in cycle.Rounds.Keys.Order())
                {
                    if (cycle.Usage.Summarise(Cycle.NameOf(kind)) is { } document)
                    {
                        text.AppendLine("      " + Cycle.NameOf(kind) + " " + document.Describe());
                    }
                }
            }

            // How far the build had to bend the approved documents: a plan that needed many deviations was a
            // weak plan, and a spec that needed criterion deviations overreached.
            if (cycle.Deviations is { Count: > 0 } deviated)
            {
                text.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"    deviated {deviated.Count}x ({string.Join(", ", deviated.GroupBy(deviation => deviation.Kind).OrderBy(group => group.Key).Select(group => string.Create(CultureInfo.InvariantCulture, $"{group.Count()} {Deviation.NameOf(group.Key)}")))}): {deviated.Count(deviation => deviation.Standing is DeviationStanding.Accepted)} accepted, {deviated.Count(deviation => deviation.Standing is DeviationStanding.Declined)} declined, {deviated.Count(deviation => deviation.Standing is DeviationStanding.Pending)} unanswered, {deviated.Count(deviation => deviation.Standing is DeviationStanding.History)} taken in by a reopen"));
            }

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

            Mining.MinedLedger? mined = (await Mining.MinedLedger.Read(solution.Path, cancellationToken).ConfigureAwait(false)).ValueOrDefault;

            // Only a finished cycle's suggestions: one in flight still answers its own through steer, and one already
            // mined had its suggestions answered at that sitting.
            List<CycleId> finished = [];

            foreach (Cycle cycle in CycleIndex.All(solution.Path))
            {
                if (mined?.Find(cycle.Id.ToString()) is null
                    && await Mining.RecordMining.IsFinished(cycle, cancellationToken).ConfigureAwait(false))
                {
                    finished.Add(cycle.Id);
                }
            }

            // A sitting limited to some cycles reads only theirs, and the rest wait for a later one (D-166). The rule
            // counts stay over the whole record: they are counts, and a rule cited in three cycles is cited in three
            // however many of them this sitting reads.
            bool limited = request.Cycles is not null;
            List<string> refused = [.. (request.Cycles ?? []).Where(id => !finished.Contains(id)).Select(id => Refusal(id, mined))];

            if (refused.Count > 0)
            {
                return Result.Fail<Response>("A sitting reads finished cycles not mined yet. " + string.Join(" ", refused));
            }

            HashSet<CycleId> sitting = [.. request.Cycles ?? finished];

            List<Group> groups =
            [
                .. rules.Value.Uncited
                    .Where(finding => !limited || sitting.Contains(finding.Cycle))
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

            foreach (Cycle cycle in CycleIndex.All(solution.Path).Where(cycle => !limited || sitting.Contains(cycle.Id)))
            {
                record.Add(await Recorded(cycle, cancellationToken).ConfigureAwait(false));
            }

            Result<IReadOnlyList<Area>> defined = await AreaScope.ReadDefined(solution.Path, cancellationToken).ConfigureAwait(false);

            if (defined.IsFailed)
            {
                return defined.ToResult<Response>();
            }

            Result<Suggestions.Response> open = await mediator
                .Send<Suggestions.Query, Suggestions.Response>(new Suggestions.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (open.IsFailed)
            {
                return open.ToResult<Response>();
            }

            return Result.Ok(new Response(
                rules.Value,
                groups,
                decisions.Value,
                unmentioned,
                record,
                await OverBudget(decisions.Value.Standing, cancellationToken).ConfigureAwait(false),
                await AreaHistory.Read(solution.Path, defined.Value, cancellationToken).ConfigureAwait(false),
                mined,
                [.. open.Value.Suggestions.Where(suggestion => sitting.Contains(suggestion.Cycle))],
                [.. finished.Where(sitting.Contains)],
                limited));
        }

        private static string Refusal(CycleId id, Mining.MinedLedger? mined) =>
            mined?.Find(id.ToString()) is not null
                ? string.Create(CultureInfo.InvariantCulture, $"{id} was mined already; 'sdlc mine --history' lists when.")
                : string.Create(CultureInfo.InvariantCulture, $"{id} is not a finished cycle here: its verify has not passed and nobody abandoned it, or there is no such cycle. 'sdlc cycles' lists them.");

        /// <summary>
        /// The entries a reader handed only the headline would get too much of: never refused, because the
        /// files are a person's, and proposed for rewording here (D-149).
        /// </summary>
        private async Task<IReadOnlyList<string>> OverBudget(IReadOnlyList<Decision> standing, CancellationToken cancellationToken)
        {
            List<string> over = [];
            string path = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, FileConstraintDocuments.SolutionConstraintsFileName);

            if (File.Exists(path))
            {
                IReadOnlyList<Constraint> local = ConstraintParser
                    .Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), path)
                    .ValueOrDefault ?? [];

                over.AddRange(local
                    .Where(rule => rule.IsInForce)
                    .Select(rule => (Id: rule.Id.ToString(), Overruns: EntryBudget.Of(rule)))
                    .Where(entry => entry.Overruns.Count > 0)
                    .Select(entry => entry.Id + " (" + EntryBudget.Describe(entry.Overruns) + ")"));
            }

            over.AddRange(standing
                .Select(decision => (Id: decision.Id.ToString(), Overruns: EntryBudget.Of(decision)))
                .Where(entry => entry.Overruns.Count > 0)
                .Select(entry => entry.Id + " (" + EntryBudget.Describe(entry.Overruns) + ")"));

            return over;
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

            DeviationsState deviations = await CycleDeviations.Read(cycle, cancellationToken).ConfigureAwait(false);

            return new CycleRecord(
                cycle.Id,
                rounds,
                reopens,
                Verdicts(cycle),
                (await UsageLedger.Read(cycle, cancellationToken).ConfigureAwait(false)).ValueOrDefault,
                [.. deviations.States.Select(state => new DeviationRecord(
                    state.Deviation.Id,
                    state.Deviation.Kind,
                    state.Standing,
                    DeviationsFile.RulesOf(state.Deviation)))]);
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
