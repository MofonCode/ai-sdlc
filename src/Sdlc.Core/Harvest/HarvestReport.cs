using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Areas;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Deviations;
using Sdlc.Core.Findings;
using Sdlc.Core.Git;
using Sdlc.Core.Proposals;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Harvest;

/// <summary>
/// What the completed cycles can still give back, read one aspect at a time (D-104, D-191).
/// </summary>
/// <remarks>
/// <para><strong>One aspect per report, because the whole was too much to sort through.</strong> One solution's
/// constraints report was 431 KB: 754 findings that rested on no rule, listed raw. Its decisions report was 36
/// passages copied verbatim. This counts and groups -- what a binary can decide -- one aspect at a time, and leaves
/// the wording to the aspect's skill, which writes the proposal file a person marks. With no aspect it prints the
/// overview: which completed cycles are open, which aspects each still needs, and which may close.</para>
/// <para><strong>Completed cycles only</strong> (D-190): merged after a passing verify, or abandoned. A cycle still in
/// flight, or one whose pull request has not merged, is read by no aspect, and its findings count toward no rule.</para>
/// </remarks>
public static class HarvestReport
{
    /// <summary>Statements quoted per group, at most.</summary>
    public const int Sample = 5;

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddHarvestReportRequestHandler(this IServiceCollection services)
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

    /// <summary>One deviation a cycle recorded, as a harvest tallies it.</summary>
    /// <param name="Id">Its id.</param>
    /// <param name="Kind">What it bent.</param>
    /// <param name="Standing">Where it stands: recorded, accepted before D-181, changed at the person's request, or taken in by a reopen.</param>
    /// <param name="Rules">The rules its blocker named; empty when it named none.</param>
    public sealed record DeviationRecord(string Id, DeviationKind Kind, DeviationStanding Standing, IReadOnlyList<string> Rules);

    /// <summary>A completed cycle not closed yet, and the aspects it still needs answered.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Abandoned">Whether it was abandoned rather than passed.</param>
    /// <param name="Missing">The aspects no sitting has answered for it, in the order the playbook runs them.</param>
    public sealed record OpenCycle(CycleId Cycle, bool Abandoned, IReadOnlyList<HarvestAspect> Missing);

    /// <summary>The report.</summary>
    /// <param name="Aspect">The aspect it reads, or null for the overview.</param>
    /// <param name="Rules">What the completed cycles' findings say about the rules.</param>
    /// <param name="Groups">The findings that rested on no rule, grouped and ranked.</param>
    /// <param name="Decisions">What the sitting's completed cycles stated.</param>
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
    /// <param name="Ledger">
    /// The harvest ledger: the cycles already closed, the aspects each open cycle has had answered, and the
    /// concerns watched below the bar, whose counts carry across sittings because the folders they came from go (D-152).
    /// </param>
    /// <param name="Suggestions">
    /// The open suggestions of the sitting's cycles: each is taken further, declined with its reason, or ignored,
    /// and an ignored one is tallied by concern in the ledger, since nothing else keeps it (D-153).
    /// </param>
    /// <param name="Sitting">The completed cycles this report reads: for an aspect, those that have not answered it yet.</param>
    /// <param name="Limited">Whether the sitting was limited to cycles named on the command line (D-166).</param>
    /// <param name="FrameworkGaps">
    /// Every successor marked <c>framework:</c>, for a person to take to the framework rather than build here (D-173).
    /// </param>
    /// <param name="Open">Every completed cycle not closed yet, with the aspects it still needs.</param>
    /// <param name="Unmerged">Finished cycles whose folders are not on the integration branch yet: they wait.</param>
    /// <param name="IntegrationBranch">The integration branch.</param>
    public sealed record Response(
        HarvestAspect? Aspect,
        ProposeConstraints.Response Rules,
        IReadOnlyList<Group> Groups,
        ProposeDecisions.Response Decisions,
        StackCoverage.Unmentioned Stack,
        IReadOnlyList<CycleRecord> Record,
        IReadOnlyList<string> OverBudget,
        AreaHistory.Reading Areas,
        HarvestLedger Ledger,
        IReadOnlyList<Suggestion> Suggestions,
        IReadOnlyList<CycleId> Sitting,
        bool Limited,
        IReadOnlyList<Successors.Successor> FrameworkGaps,
        IReadOnlyList<OpenCycle> Open,
        IReadOnlyList<CycleId> Unmerged,
        string IntegrationBranch);

    /// <summary>Asks for the report.</summary>
    /// <param name="Aspect">The aspect to read, or null for the overview.</param>
    /// <param name="Cycles">
    /// The completed cycles this sitting reads, or null for all of them that still need the aspect. A backlog is
    /// harvested a few cycles at a time, and the rest wait for a later sitting.
    /// </param>
    public sealed record Query(HarvestAspect? Aspect = null, IReadOnlyList<CycleId>? Cycles = null) : IRequest<Response>;

    /// <summary>
    /// Renders the overview: the completed cycles still open, what each needs, and the next step.
    /// </summary>
    /// <param name="response">The report, read with no aspect.</param>
    /// <returns>The text.</returns>
    public static string RenderOverview(Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();

        if (response.Open.Count == 0)
        {
            text.AppendLine("Nothing to harvest: no completed cycle is open.");
            Waiting(text, response);
            text.AppendLine("A cycle is completed once its pull request merges after a passing verify, or a person abandons it.");
            return text.ToString();
        }

        List<OpenCycle> ready = [.. response.Open.Where(open => open.Missing.Count == 0)];
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Harvest: {Count(response.Open.Count, "completed cycle")} open, {ready.Count} ready to close."));
        text.AppendLine();

        foreach (OpenCycle open in response.Open)
        {
            string state = open.Missing.Count == 0
                ? "every aspect answered -- ready to close"
                : "to answer: " + string.Join(", ", open.Missing.Select(HarvestAspects.NameOf));
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {open.Cycle}{(open.Abandoned ? " (abandoned)" : string.Empty)}  {state}"));
        }

        Waiting(text, response);
        text.AppendLine();

        List<HarvestAspect> pending = [.. HarvestAspects.All.Where(aspect => response.Open.Any(open => open.Missing.Contains(aspect)))];

        text.AppendLine(pending.Count > 0
            ? "Next: 'sdlc harvest " + HarvestAspects.NameOf(pending[0]) + "'. Then each aspect still listed, then 'sdlc harvest close'."
            : "Next: 'sdlc harvest close' -- it commits the closed cycles' removal on a branch of its own.");

        return text.ToString();
    }

    /// <summary>Renders one aspect's report, ending in the shape of the proposal file a person marks.</summary>
    /// <param name="response">The report, read for one aspect.</param>
    /// <param name="proposals">Where the skill writes the proposal file.</param>
    /// <returns>The text.</returns>
    public static string Render(Response response, string proposals)
    {
        ArgumentNullException.ThrowIfNull(response);

        HarvestAspect aspect = response.Aspect ?? throw new ArgumentException("An aspect's report needs its aspect.", nameof(response));
        StringBuilder text = new();
        string name = HarvestAspects.NameOf(aspect);

        if (response.Sitting.Count == 0)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Harvest {name}: every completed cycle has answered it. Nothing to propose."));
            Waiting(text, response);
            return text.ToString();
        }

        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Harvest {name}: {Count(response.Sitting.Count, "completed cycle")} -- {string.Join(", ", response.Sitting)}"));

        switch (aspect)
        {
            case HarvestAspect.Constraints:
                Constraints(text, response);
                break;
            case HarvestAspect.Decisions:
                Decisions(text, response);
                break;
            case HarvestAspect.Suggestions:
                Suggested(text, response.Suggestions, response.Ledger.Ignored ?? []);
                Gaps(text, response.FrameworkGaps);
                break;
            case HarvestAspect.Stack:
                Stack(text, response.Stack);
                break;
            case HarvestAspect.Areas:
                Areas(text, response.Areas);
                break;
        }

        if (response.Limited)
        {
            text.AppendLine();
            text.AppendLine("This sitting reads the cycles named above alone; the rule counts are the whole completed record's. Completed cycles not named wait for a later sitting (D-166).");
        }

        Shape(text, aspect, proposals, response.Sitting);
        return text.ToString();
    }

    private static void Waiting(StringBuilder text, Response response)
    {
        foreach (CycleId cycle in response.Unmerged)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  waiting: {cycle} is finished but not merged into {response.IntegrationBranch} yet"));
        }
    }

    private static string Count(int number, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{number} {noun}{(number == 1 ? string.Empty : "s")}");

    /// <summary>
    /// What the findings say about the rules: rules cited across cycles, rules argued inside one, findings that
    /// rested on no rule, deviations by rule, criteria that kept failing, and the concerns watched below the bar.
    /// </summary>
    private static void Constraints(StringBuilder text, Response response)
    {
        ProposeConstraints.Response rules = response.Rules;

        text.AppendLine();
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Rules   next free {rules.NextId}, {rules.InForce} in force"));

        foreach (Recurrence recurrence in rules.Recurrences)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {recurrence.Cited} cited in {recurrence.Cycles.Count} cycles -- {recurrence.Heading}"));
        }

        if (rules.Recurrences.Count == 0)
        {
            text.AppendLine("  no rule cited in three or more cycles");
        }

        // Below the bar for a rule, and often exactly where a decision is missing: a rule argued over dozens of
        // findings inside one cycle is an argument the cycle settled and nothing recorded. The decisions skill reads
        // what the plan finally did about each; this one answers whether the rule itself needs changing.
        foreach (Recurrence argued in Argued(response))
        {
            string settles = argued.Cited.Tier == ConstraintTier.Global
                ? "the framework's rule -- if the cost is the rule's, a note for the framework"
                : "a decision settles it more often than a rule";

            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  argued: {argued.Cited} in {argued.Findings.Count} findings over {Count(argued.Cycles.Count, "cycle")}, one standing {argued.Longest} round{(argued.Longest == 1 ? string.Empty : "s")} -- {argued.Heading}; {settles}"));
        }

        text.AppendLine();
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Rested on no rule   {response.Groups.Sum(group => group.Raised)} findings in {response.Groups.Count} groups, most cycles first"));

        foreach (Group group in response.Groups)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {group.Raiser} on the {Cycle.NameOf(group.Artifact)}: {group.Raised} findings, {Count(group.Cycles.Count, "cycle")}"));

            foreach (string statement in group.Statements)
            {
                text.AppendLine("    - " + statement);
            }
        }

        foreach (PerspectiveTally tally in rules.Perspectives.Where(tally => tally.Cycles.Count >= ProposeConstraints.Bar))
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  perspective at the bar: {tally.Perspective} in {tally.Cycles.Count} cycles, {tally.Raised} raised"));
        }

        foreach (Stopped stopped in rules.Stopped.Where(stopped => response.Sitting.Contains(stopped.Cycle)))
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  abandoned {stopped.Cycle}: {stopped.Reason ?? stopped.Unreadable}"));
        }

        // Which readers raise what a later reader takes back: about the reader, not the solution.
        if (rules.Withdrawn is { Count: > 0 } withdrawn)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  withdrawn: {string.Join(", ", withdrawn.GroupBy(finding => finding.Perspective ?? "rules").OrderByDescending(group => group.Count()).Select(group => string.Create(CultureInfo.InvariantCulture, $"{group.Key} {group.Count()}")))} -- raised, then taken back by a later reader"));
        }

        DeviatedBy(text, response.Record);
        Failing(text, response.Record);
        Budget(text, response.OverBudget, "LOC_", "reword it");

        if (response.Ledger.Watch.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Watched before, below the bar");

            foreach (Watched watched in response.Ledger.Watch)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {watched.Cycles.Count} of {ProposeConstraints.Bar} cycles: {watched.Gist} ({string.Join(", ", watched.Cycles)})"));
            }
        }
    }

    /// <summary>
    /// What the sitting's cycles stated, the reopens and declined deviations that are a person's decisions already,
    /// the rules each argued, and the standing decisions over their word budget.
    /// </summary>
    private static void Decisions(StringBuilder text, Response response)
    {
        text.AppendLine();
        text.Append(ProposalsRenderer.Render(response.Decisions));

        List<(CycleId Cycle, Reopen Reopen)> reopens = [.. response.Record.SelectMany(cycle => cycle.Reopens.Select(reopen => (cycle.Cycle, reopen)))];
        List<(CycleId Cycle, DeviationRecord Deviation)> declined =
        [
            .. response.Record.SelectMany(cycle => (cycle.Deviations ?? [])
                .Where(deviation => deviation.Standing == DeviationStanding.Declined)
                .Select(deviation => (cycle.Cycle, deviation))),
        ];

        if (reopens.Count > 0 || declined.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("A person's words already -- each qualifies once");

            foreach ((CycleId cycle, Reopen reopen) in reopens)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {cycle} reopened {reopen.At:yyyy-MM-dd} by {reopen.By}: {reopen.Reason?.ReplaceLineEndings(" ") ?? "(no reason)"}"));
            }

            foreach ((CycleId cycle, DeviationRecord deviation) in declined)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {cycle} {deviation.Id} changed at the person's request -- 'sdlc report {cycle}' has their note"));
            }
        }

        List<Recurrence> argued = [.. Argued(response)];

        if (argued.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Argued inside a cycle -- what the plan finally did is the decision to record");

            foreach (Recurrence rule in argued)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {rule.Cited} in {rule.Findings.Count} findings ({string.Join(", ", rule.Cycles)}) -- {rule.Heading}"));
            }
        }

        Budget(text, response.OverBudget, "DEC_", "supersede it with shorter wording");
    }

    private static IEnumerable<Recurrence> Argued(Response response) =>
        (response.Rules.Cited ?? [])
            .Where(recurrence => recurrence.Cycles.Count < ProposeConstraints.Bar && recurrence.Cycles.Any(response.Sitting.Contains))
            .OrderByDescending(recurrence => recurrence.Findings.Count)
            .Take(Sample);

    private static void Budget(StringBuilder text, IReadOnlyList<string> overBudget, string prefix, string remedy)
    {
        List<string> entries = [.. overBudget.Where(entry => entry.StartsWith(prefix, StringComparison.Ordinal))];

        if (entries.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Over the word budget   past a {EntryBudget.Headline}-word title or {EntryBudget.Clause} words a clause -- {remedy}"));

        foreach (string entry in entries)
        {
            text.AppendLine("  " + entry);
        }
    }

    private static void Stack(StringBuilder text, StackCoverage.Unmentioned stack)
    {
        text.AppendLine();
        text.AppendLine("Stack   what this release ships that techstack.md never mentions");

        foreach (StackDefault missing in stack.Defaults)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  not mentioned: {missing.Concern} -- the profile says {missing.Choice}"));
        }

        foreach (StackCoverage.Rule rule in stack.Rules)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  not mentioned: {rule.Id} -- {rule.Statement}"));
        }

        if (stack.Defaults.Count == 0 && stack.Rules.Count == 0)
        {
            text.AppendLine("  every row and rule this release ships is answered");
        }
    }

    private static void Gaps(StringBuilder text, IReadOnlyList<Successors.Successor> gaps)
    {
        if (gaps.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine("Gaps in the framework -- successors marked framework:, for a person to file with the framework rather than build here (D-173)");

        foreach (Successors.Successor gap in gaps)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {gap.Slug} -- {gap.Framework}"));
        }
    }

    /// <summary>The sections of the proposal file this aspect writes, and the steps after it.</summary>
    private static void Shape(StringBuilder text, HarvestAspect aspect, string proposals, IReadOnlyList<CycleId> sitting)
    {
        text.AppendLine();
        text.AppendLine("The proposals go in " + proposals + ", one heading each, for a person to mark:");
        text.AppendLine();

        switch (aspect)
        {
            case HarvestAspect.Constraints:
                text.AppendLine("  ## Rules        ### [ ] add — <statement>   ### [ ] supersede LOC_02 — <statement>   ### [ ] reword LOC_01 — <statement>");
                text.AppendLine("  ## Watch        - <a concern below the bar> — <cycle>, <cycle>");
                break;
            case HarvestAspect.Decisions:
                text.AppendLine("  ## Decisions    ### [ ] add — <statement>   ### [ ] supersede DEC_02 — <statement>");
                break;
            case HarvestAspect.Suggestions:
                text.AppendLine("  ## Successors   ### [ ] add <slug> — <what it makes possible>, with **Succeeds:**, **Outcome:** and **Why it matters:** beneath");
                text.AppendLine("  ## Declined     - <cycle>/<suggestion id>: <why>");
                text.AppendLine("  ## Ignored      - <the concern, worded as it was ignored before> — <cycle>, <cycle>");
                break;
            case HarvestAspect.Stack:
                text.AppendLine("  ## Stack        ### [ ] set <concern> — <choice, or none>");
                break;
            case HarvestAspect.Areas:
                text.AppendLine("  ## Areas        ### [ ] add <name> — <what it is>   ### [ ] rename <old> <new> — <why>   ### [ ] merge <from> into <to> — <why>");
                text.AppendLine("                  ### [ ] remove <name> — <why>   ### [ ] files <name> — <globs>   ### [ ] tag DEC_04 — <area, area, or none>");
                break;
        }

        text.AppendLine("  ## For the framework   ### <the framework rule> — <what it cost here>");
        text.AppendLine("  ## Not proposed - <the item> — <already recorded as ... | no proposal, because ...>");
        text.AppendLine("  ## Cycles       copied as written: the answers are recorded for these cycles and no others");

        foreach (CycleId cycle in sitting)
        {
            text.AppendLine("                  - " + cycle);
        }

        text.AppendLine();
        text.AppendLine("  Every item above gets an answer: a proposal, a line under Not proposed, or a note for the framework.");
        text.AppendLine("  Then 'sdlc draft harvest " + proposals + "' writes the drafts from what was marked [x], and once they");
        text.AppendLine("  are placed, 'sdlc harvest record " + HarvestAspects.NameOf(aspect) + " " + proposals + "' records the answers.");
    }

    /// <summary>
    /// The open suggestions of the sitting's cycles, and the concerns earlier sittings ignored (D-153).
    /// </summary>
    /// <remarks>
    /// Each suggestion is answered one of three ways. Taken further, it becomes a successor and lives on as a file;
    /// declined, its reason is kept in the sitting; ignored, nothing about it survives its folder but the tally.
    /// The earlier ignored concerns are listed in their own words so an ignored suggestion is worded the same way
    /// and counted with them, and one at the bar is not ignored again.
    /// </remarks>
    private static void Suggested(StringBuilder text, IReadOnlyList<Suggestion> suggestions, IReadOnlyList<Watched> ignored)
    {
        text.AppendLine();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Suggestions   {suggestions.Count} open -- take each further, decline it with why, or ignore it"));

        foreach (Suggestion suggestion in suggestions
            .OrderBy(suggestion => suggestion.Cycle.ToString(), StringComparer.Ordinal)
            .ThenBy(suggestion => suggestion.Finding.Id, StringComparer.Ordinal))
        {
            string raiser = suggestion.Finding.Perspective is { } perspective ? " [" + perspective + "]" : string.Empty;
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {suggestion.Cycle}/{suggestion.Finding.Id}{raiser} on the {string.Join(", ", suggestion.Documents.Select(Cycle.NameOf))}: {suggestion.Finding.Headline}"));
        }

        foreach (Watched concern in ignored.OrderByDescending(concern => concern.Cycles.Count))
        {
            string bar = concern.Cycles.Count >= ProposeConstraints.Bar
                ? " -- at the bar: make it a successor or decline it, and do not ignore it again"
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
    private static void Areas(StringBuilder text, AreaHistory.Reading areas)
    {
        text.AppendLine();
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Areas   {areas.Defined.Count} defined, {areas.Candidates.Count} names proposed, {areas.Plans} plans replayed"));

        foreach (AreaHistory.Candidate candidate in areas.Candidates)
        {
            string bar = candidate.Cycles.Count >= ProposeConstraints.Bar ? "at the bar" : "below the bar";
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  proposed: {candidate.Name} in {Count(candidate.Cycles.Count, "cycle")} ({bar}) -- would match {candidate.Plans} of {areas.Plans} plans -- {string.Join(", ", candidate.Files)}"));
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
    /// Which rules keep causing deviations, across the sitting's cycles: the rule each deviation's blocker named,
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
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Deviations   {all.Count} across {Count(all.Select(entry => entry.Cycle).Distinct().Count(), "cycle")}, by the rule behind them"));

        foreach (IGrouping<string, (CycleId Cycle, DeviationRecord Deviation)> rule in all
            .SelectMany(entry => entry.Deviation.Rules.Count == 0 ? ["no rule named"] : entry.Deviation.Rules, (entry, rule) => (Rule: rule, entry))
            .GroupBy(pair => pair.Rule, pair => pair.entry, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            int cycles = rule.Select(entry => entry.Cycle).Distinct().Count();
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {rule.Key}: {rule.Count()} in {Count(cycles, "cycle")}, {rule.Count(entry => entry.Deviation.Standing == DeviationStanding.Declined)} declined"));
        }
    }

    /// <summary>
    /// Each cycle's rounds, and every criterion that failed more than one verdict: an argument about what the
    /// criterion asks.
    /// </summary>
    private static void Failing(StringBuilder text, IReadOnlyList<CycleRecord> record)
    {
        if (record.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine("Cycles   rounds per document, and criteria that failed more than one verdict");

        foreach (CycleRecord cycle in record)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {cycle.Cycle}: {(cycle.Rounds.Count == 0 ? "no rounds" : string.Join(", ", cycle.Rounds.OrderBy(entry => entry.Key).Select(entry => string.Create(CultureInfo.InvariantCulture, $"{Cycle.NameOf(entry.Key)} {entry.Value}"))))}; {Count(cycle.Verdicts.Count, "verdict")}"));

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

            // How far the build had to bend the approved documents: a plan that needed many deviations was a weak plan.
            if (cycle.Deviations is { Count: > 0 } deviated)
            {
                text.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"    deviated {deviated.Count}x ({string.Join(", ", deviated.GroupBy(deviation => deviation.Kind).OrderBy(group => group.Key).Select(group => string.Create(CultureInfo.InvariantCulture, $"{group.Count()} {Deviation.NameOf(group.Key)}")))}): {deviated.Count(deviation => deviation.Standing is DeviationStanding.Recorded or DeviationStanding.Accepted)} kept, {deviated.Count(deviation => deviation.Standing is DeviationStanding.Declined)} changed at the person's request, {deviated.Count(deviation => deviation.Standing is DeviationStanding.History)} taken in by a reopen"));
            }

            if (cycle.Verdicts.Count > 0)
            {
                text.AppendLine("    verdicts: " + string.Join(" -> ", cycle.Verdicts.Select(verdict => verdict.NotMet.Count == 0
                    ? verdict.Outcome ?? "?"
                    : string.Create(CultureInfo.InvariantCulture, $"{verdict.Outcome ?? "?"} on {string.Join(",", verdict.NotMet)}"))));
            }

            foreach (IGrouping<int, int> repeated in cycle.Verdicts
                .SelectMany(verdict => verdict.NotMet)
                .GroupBy(number => number)
                .Where(group => group.Count() > 1)
                .OrderByDescending(group => group.Count()))
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"    criterion {repeated.Key} not met in {repeated.Count()} verdicts"));
            }
        }
    }

    private sealed class Handler(Mediator mediator, SolutionRoot solution, IRepository repository)
        : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<HarvestLedger> read = await HarvestLedger.Read(solution.Path, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            HarvestLedger ledger = read.Value;
            string integration = await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken).ConfigureAwait(false);

            List<OpenCycle> open = [];
            List<CycleId> unmerged = [];

            foreach (Cycle cycle in CycleIndex.All(solution.Path))
            {
                if (ledger.Find(cycle.Id.ToString()) is not null
                    || !await Completion.IsFinished(cycle, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                if (Completion.MergeOf(repository, solution.Path, integration, cycle.Id) is null)
                {
                    unmerged.Add(cycle.Id);
                    continue;
                }

                open.Add(new OpenCycle(cycle.Id, cycle.IsAbandoned, ledger.Missing(cycle.Id.ToString())));
            }

            List<CycleId> completed = [.. open.Select(cycle => cycle.Cycle)];
            List<CycleId> eligible = request.Aspect is { } aspect
                ? [.. open.Where(cycle => cycle.Missing.Contains(aspect)).Select(cycle => cycle.Cycle)]
                : completed;

            // A sitting limited to some cycles reads only theirs, and the rest wait for a later one (D-166). The rule
            // counts stay over the whole completed record: a rule cited in three cycles is cited in three however
            // many of them this sitting reads.
            bool limited = request.Cycles is not null;
            List<string> refused = [.. (request.Cycles ?? []).Where(id => !eligible.Contains(id)).Select(id => Refusal(id, ledger, completed, unmerged, request.Aspect))];

            if (refused.Count > 0)
            {
                return Result.Fail<Response>(string.Join(" ", refused));
            }

            List<CycleId> sitting = [.. request.Cycles ?? eligible];

            Result<ProposeConstraints.Response> rules = await mediator
                .Send<ProposeConstraints.Query, ProposeConstraints.Response>(new ProposeConstraints.Query(completed), cancellationToken)
                .ConfigureAwait(false);

            if (rules.IsFailed)
            {
                return rules.ToResult<Response>();
            }

            Result<ProposeDecisions.Response> decisions = await mediator
                .Send<ProposeDecisions.Query, ProposeDecisions.Response>(new ProposeDecisions.Query(sitting), cancellationToken)
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
                    .Where(finding => sitting.Contains(finding.Cycle))
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

            foreach (Cycle cycle in CycleIndex.All(solution.Path).Where(cycle => sitting.Contains(cycle.Id)))
            {
                record.Add(await Recorded(cycle, cancellationToken).ConfigureAwait(false));
            }

            Result<IReadOnlyList<Area>> defined = await AreaScope.ReadDefined(solution.Path, cancellationToken).ConfigureAwait(false);

            if (defined.IsFailed)
            {
                return defined.ToResult<Response>();
            }

            Result<Suggestions.Response> suggestions = await mediator
                .Send<Suggestions.Query, Suggestions.Response>(new Suggestions.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (suggestions.IsFailed)
            {
                return suggestions.ToResult<Response>();
            }

            return Result.Ok(new Response(
                request.Aspect,
                rules.Value,
                groups,
                decisions.Value,
                unmentioned,
                record,
                await OverBudget(decisions.Value.Standing, cancellationToken).ConfigureAwait(false),
                await AreaHistory.Read(solution.Path, defined.Value, completed, cancellationToken).ConfigureAwait(false),
                ledger,
                [.. suggestions.Value.Suggestions.Where(suggestion => sitting.Contains(suggestion.Cycle))],
                sitting,
                limited,
                await FrameworkGaps(cancellationToken).ConfigureAwait(false),
                open,
                unmerged,
                integration));
        }

        // Read whatever else is wrong with the successors: a malformed one is the successors command's to report.
        private async Task<IReadOnlyList<Successors.Successor>> FrameworkGaps(CancellationToken cancellationToken)
        {
            Result<Successors.ReadSuccessors.Response> read = await mediator
                .Send<Successors.ReadSuccessors.Query, Successors.ReadSuccessors.Response>(new Successors.ReadSuccessors.Query(), cancellationToken)
                .ConfigureAwait(false);

            return read.IsFailed
                ? []
                : [.. read.Value.Successors.Select(standing => standing.Successor).Where(successor => successor.Framework is not null).OrderBy(successor => successor.Slug, StringComparer.Ordinal)];
        }

        private static string Refusal(CycleId id, HarvestLedger ledger, IReadOnlyList<CycleId> completed, IReadOnlyList<CycleId> unmerged, HarvestAspect? aspect) =>
            ledger.Find(id.ToString()) is not null
                ? string.Create(CultureInfo.InvariantCulture, $"{id} is closed already; 'sdlc harvest --history' lists when.")
                : unmerged.Contains(id)
                    ? string.Create(CultureInfo.InvariantCulture, $"{id} is finished but its pull request has not merged, so nobody has accepted it yet.")
                    : completed.Contains(id) && aspect is { } answered
                        ? string.Create(CultureInfo.InvariantCulture, $"{id} has answered {HarvestAspects.NameOf(answered)} already.")
                        : string.Create(CultureInfo.InvariantCulture, $"{id} is not a completed cycle here: its verify has not passed and nobody abandoned it, or there is no such cycle. 'sdlc cycles' lists them.");

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
                    cycle.ApprovalPath) is { IsSuccess: true } approvals)
            {
                reopens.AddRange(approvals.Value.Decisions
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
