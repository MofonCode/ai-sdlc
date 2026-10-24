using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
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

    /// <summary>The report.</summary>
    /// <param name="Rules">What the findings say about the rules.</param>
    /// <param name="Groups">The findings that rested on no rule, grouped and ranked.</param>
    /// <param name="Decisions">What approved cycles stated.</param>
    /// <param name="Stack">What the declared stack does not mention.</param>
    public sealed record Response(
        ProposeConstraints.Response Rules,
        IReadOnlyList<Group> Groups,
        ProposeDecisions.Response Decisions,
        StackCoverage.Unmentioned Stack);

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
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  argued: {argued.Cited} in {argued.Findings.Count} findings over {argued.Cycles.Count} cycle{(argued.Cycles.Count == 1 ? string.Empty : "s")} -- {argued.Heading}. A decision that settles the argument, more often than a rule"));
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

    private sealed class Handler(Mediator mediator, SolutionRoot solution) : IRequestHandler<Query, Response>
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
                            [.. group.Select(finding => finding.Statement).Distinct(StringComparer.Ordinal).OrderBy(statement => statement.Length).Take(Sample)]);
                    })
                    .OrderByDescending(group => group.Cycles.Count)
                    .ThenByDescending(group => group.Raised),
            ];

            return Result.Ok(new Response(rules.Value, groups, decisions.Value, unmentioned));
        }
    }
}
