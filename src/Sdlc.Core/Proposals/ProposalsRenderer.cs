using System.Globalization;
using System.Text;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Decisions;

namespace Sdlc.Core.Proposals;

/// <summary>
/// Renders what the record holds, in the four groups a person reads it in.
/// </summary>
/// <remarks>
/// <para><strong>Evidence first, and never a verdict.</strong> Each group says what happened and where,
/// so a candidate can be refused on the record rather than on taste. The one thing this never prints is
/// a rule: the wording is the decision, and a renderer that filled it in would make the weakest part of
/// the report the part that reads as finished.</para>
///
/// <para><strong>Candidates are labelled, never numbered.</strong> The next free id is stated once, at
/// the top. Numbering three candidates <c>LOC_07</c> to <c>LOC_09</c> and having a person take only the
/// middle one would leave a gap, and §7.4 makes a gap the record of a constraint that was
/// <em>removed</em> — so a proposal nobody accepted would be indistinguishable from a rule somebody
/// retired.</para>
///
/// <para><strong>Removal is not offered.</strong> Nowhere here prints an entry to delete. A rule out of
/// force keeps its place on the page with a <c>Superseded-by</c> clause, because a finding that cited it
/// stays readable — and whatever this prints is what somebody pastes, which makes the printed text the
/// place that rule is really enforced.</para>
/// </remarks>
public static class ProposalsRenderer
{
    private const string Indent = "  ";
    private const string Field = Indent + "    ";
    private const string Body = Indent + "        ";

    /// <summary>
    /// Renders the report.
    /// </summary>
    /// <param name="response">What the history held.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ProposeConstraints.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder output = new();

        Header(output, response);
        Recurrences(output, response);
        BelowBar(output, response);
        Perspectives(output, response);
        Uncited(output, response);
        Noticed(output, response);
        Stopped(output, response);
        Rejected(output, response);
        Closing(output, response);

        return output.ToString();
    }

    private static void Header(StringBuilder output, ProposeConstraints.Response response)
    {
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}{Cycles(response.Cycles)} read. {response.NextId} is the next free id."));

        string rules = response.InForce == 1 ? "1 rule in force" : string.Create(
            CultureInfo.InvariantCulture,
            $"{response.InForce} rules in force");

        output.AppendLine(response.Superseded.Count == 0
            ? Indent + rules + "."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{rules}, and {response.Superseded.Count} superseded: {Ids(response.Superseded)}."));
    }

    /// <summary>
    /// The rules that blocked or bothered enough separate cycles to be a pattern.
    /// </summary>
    /// <remarks>
    /// A recurrence is not itself a missing rule — it is as likely to mean the opposite, that an existing
    /// rule keeps catching the same real mistake and is doing its job. Which of the two it is depends on
    /// whether the solution could have been built so the mistake was impossible, and that is the
    /// judgement the skill makes from these statements.
    /// </remarks>
    private static void Recurrences(StringBuilder output, ProposeConstraints.Response response)
    {
        if (response.Recurrences.Count == 0)
        {
            return;
        }

        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}Recurred in {ProposeConstraints.Bar} or more cycles"));

        foreach (Recurrence recurrence in response.Recurrences)
        {
            output.AppendLine();
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Field}{recurrence.Cited}  {Cycles(recurrence.Cycles.Count)}"));

            if (!string.IsNullOrWhiteSpace(recurrence.Heading))
            {
                output.AppendLine(Body + "rule    " + recurrence.Heading);
            }

            output.AppendLine(Body + "cycles  " + string.Join(", ", recurrence.Cycles));
            output.AppendLine(Body + "found   " + string.Join(", ", recurrence.Findings));
        }
    }

    /// <summary>
    /// What recurred twice, as a count and nothing more.
    /// </summary>
    /// <remarks>
    /// Named so the report is honest about what it looked at, and withheld as candidates so the bar means
    /// something. Two is coincidence; printing a coincidence beside a pattern is how a bar gets argued
    /// down one report at a time.
    /// </remarks>
    private static void BelowBar(StringBuilder output, ProposeConstraints.Response response)
    {
        if (response.BelowBar.Count == 0)
        {
            return;
        }

        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}{Rules(response.BelowBar.Count)} recurred in two cycles, which is coincidence rather"));
        output.AppendLine(Indent + "than a pattern. Not listed as candidates, on purpose.");
    }

    /// <summary>
    /// The findings that rested on no rule, which is where a missing one shows.
    /// </summary>
    /// <remarks>
    /// Grouped by cycle, because the cycle is how a person recognises work, and left as statements
    /// because grouping them by what they are really about is the judgement this hands over. Two of
    /// these worded differently are one candidate; recognising that is reading, not counting.
    /// </remarks>
    private static void Uncited(StringBuilder output, ProposeConstraints.Response response)
    {
        if (response.Uncited.Count == 0)
        {
            return;
        }

        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}Rested on no rule — {response.Uncited.Count}"));

        foreach (IGrouping<CycleId, Uncited> group in response.Uncited
            .GroupBy(finding => finding.Cycle)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal))
        {
            output.AppendLine();
            output.AppendLine(Field + group.Key.ToString());

            foreach (Uncited finding in group
                .OrderBy(f => f.Artifact)
                .ThenBy(f => f.Id, StringComparer.Ordinal))
            {
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Body}{Cycle.NameOf(finding.Artifact),-8}{finding.Id}{Tag(finding.Perspective)}"));
                output.AppendLine(Body + "    " + finding.Statement);
            }
        }
    }

    /// <summary>
    /// Which perspectives keep raising things no rule forbids, counted by distinct cycle.
    /// </summary>
    /// <remarks>
    /// Before the statements, so a person reads where to look first. A perspective at the bar is marked,
    /// and that is all: whether its statements are one objection is the skill's reading, not this count.
    /// </remarks>
    private static void Perspectives(StringBuilder output, ProposeConstraints.Response response)
    {
        if (response.Perspectives.Count == 0)
        {
            return;
        }

        output.AppendLine();
        output.AppendLine(Indent + "Raised by a perspective, with no rule behind it");
        output.AppendLine();

        foreach (PerspectiveTally tally in response.Perspectives)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Field}{tally.Perspective,-16}{Cycles(tally.Cycles.Count)}, {tally.Raised} raised{(tally.Cycles.Count >= ProposeConstraints.Bar ? "  -- at the bar" : string.Empty)}"));
        }
    }

    /// <summary>
    /// What the builds wrote down and left alone, from their commits.
    /// </summary>
    private static void Noticed(StringBuilder output, ProposeConstraints.Response response)
    {
        if (response.Noticed.Count == 0)
        {
            return;
        }

        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}Noticed in a build and left alone — {response.Noticed.Count}"));

        foreach (IGrouping<CycleId, Noticed> group in response.Noticed
            .GroupBy(line => line.Cycle)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal))
        {
            output.AppendLine();
            output.AppendLine(Field + group.Key.ToString());

            foreach (Noticed line in group)
            {
                output.AppendLine(Body + (line.Perspective is { } perspective ? "[" + perspective + "] " : string.Empty) + line.Statement);
            }
        }
    }

    private static string Tag(string? perspective) => perspective is null ? string.Empty : "  [" + perspective + "]";

    /// <summary>
    /// The cycles somebody stopped, under their own heading.
    /// </summary>
    /// <remarks>
    /// Apart from the counted groups rather than mixed into them, so judgement is not mistaken for
    /// arithmetic. These reasons carry no citation and no structure — they are a person's own words about
    /// work that died — which makes them the best evidence here and the kind nothing can tally.
    /// </remarks>
    private static void Stopped(StringBuilder output, ProposeConstraints.Response response)
    {
        if (response.Stopped.Count == 0)
        {
            return;
        }

        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}Cycles that stopped — {response.Stopped.Count}"));

        foreach (Stopped stopped in response.Stopped
            .OrderBy(entry => entry.Cycle.ToString(), StringComparer.Ordinal))
        {
            output.AppendLine();
            output.AppendLine(Field + stopped.Cycle.ToString());

            if (stopped.Unreadable is { } path)
            {
                output.AppendLine(Body + "the record would not parse: " + path);
                continue;
            }

            foreach (string paragraph in (stopped.Reason ?? string.Empty)
                .ReplaceLineEndings()
                .Split(Environment.NewLine))
            {
                output.AppendLine(Body + paragraph);
            }
        }
    }

    /// <summary>
    /// What a person has already refused, so the report does not ask twice.
    /// </summary>
    /// <remarks>
    /// Echoed rather than silently applied. A refusal that quietly suppressed a candidate would make the
    /// report's silence ambiguous — nothing found, or something found and hidden — and the whole point of
    /// writing refusals down is that the next reader can see the argument was already had.
    /// </remarks>
    private static void Rejected(StringBuilder output, ProposeConstraints.Response response)
    {
        if (response.Rejected.Count == 0)
        {
            return;
        }

        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}Already refused — {response.Rejected.Count}"));
        output.AppendLine();

        foreach (string line in response.Rejected)
        {
            output.AppendLine(Field + line);
        }
    }

    /// <summary>
    /// The three admission tests, printed whatever the report found.
    /// </summary>
    /// <remarks>
    /// Always, and not only in the empty-handed mode. These are the quality bar, and a report that
    /// listed evidence without them would be at its most persuasive exactly where it is least
    /// authoritative — the second test is the one that disqualifies most candidates, and it disqualifies
    /// them in favour of something better rather than refusing the problem.
    /// </remarks>
    private static void Closing(StringBuilder output, ProposeConstraints.Response response)
    {
        output.AppendLine();

        if (!response.HasMaterial)
        {
            output.AppendLine(Indent + "The history holds no pattern: nothing recurred across cycles, no finding");
            output.AppendLine(Indent + "rested on no rule, and no cycle stopped. There is nothing here to argue from.");
            return;
        }

        output.AppendLine(Indent + "None of this is a rule yet. Three tests, all of which have to pass, or the");
        output.AppendLine(Indent + "rule is decoration that costs attention at every interrogation:");
        output.AppendLine();
        output.AppendLine(Field + "1. its Forbids clause names something that becomes illegal");
        output.AppendLine(Field + "2. no analyzer, schema or hook could decide it");
        output.AppendLine(Field + "3. it is incompatible with at least one plausible implementation");
        output.AppendLine();
        output.AppendLine(Indent + "Test two is the one that catches most candidates, and catching one is not a");
        output.AppendLine(Indent + "refusal: a rule about code belongs in an analyzer or an architecture test, and a");
        output.AppendLine(Indent + "mechanical rule about an artifact belongs in a schema or a hook. Both of those");
        output.AppendLine(Indent + "fail a build, which a constraint cannot.");
        output.AppendLine();
        output.AppendLine(Indent + "Nothing here writes constraints.md. The hook denies every agent write to it,");
        output.AppendLine(Indent + "because a cycle judged by the constraints it is changing is the same deadlock as");
        output.AppendLine(Indent + "the framework governing itself.");
    }

    /// <summary>
    /// Renders what the approved cycles stated, for a person to turn into entries.
    /// </summary>
    /// <remarks>
    /// A second method on this renderer rather than a renderer of its own, because both answer the same
    /// question in the same shape — here is the record, here is what you might adopt, nothing is adopted
    /// yet. What differs is only which record was read.
    /// <para>
    /// As with the rules, the four parts are printed empty. The wording is the decision, and a renderer
    /// that filled in <c>Because</c> from a plan's prose would be writing the half that cannot be
    /// recovered later — the reason a passage exists is rarely in the passage.
    /// </para>
    /// </remarks>
    /// <param name="response">What the approved cycles held.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ProposeDecisions.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder output = new();

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}{Cycles(response.Cycles)} held, {Approved(response)}. {response.NextId} is the next free id."));

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}{Standing(response.Standing.Count)} already recorded."));

        if (response.Standing.Count > 0)
        {
            output.AppendLine();

            foreach (Decision decision in response.Standing)
            {
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Field}{decision.Id,-9} {decision.Statement}"));
            }
        }

        Candidates(output, response);
        DecisionClosing(output, response);

        return output.ToString();
    }

    /// <summary>
    /// The passages, grouped by the cycle that stated them.
    /// </summary>
    /// <remarks>
    /// Labelled by cycle and section rather than numbered, for the reason a rule candidate is: the next
    /// free id is stated once, and a person numbers what they accept. The passage is printed as the
    /// artifact holds it, never summarised — a paraphrase is a second version of what somebody approved.
    /// </remarks>
    private static void Candidates(StringBuilder output, ProposeDecisions.Response response)
    {
        if (response.Candidates.Count == 0)
        {
            return;
        }

        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}Stated in an approved cycle — {response.Candidates.Count}"));

        foreach (IGrouping<CycleId, Candidate> group in response.Candidates
            .GroupBy(candidate => candidate.Cycle)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal))
        {
            output.AppendLine();
            output.AppendLine(Field + group.Key.ToString());

            foreach (Candidate candidate in group)
            {
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Body}{Section(candidate.Source)}"));
                output.AppendLine(Body + "    " + candidate.Passage);
            }
        }
    }

    /// <summary>
    /// The three admission tests for a decision, which invert the first of a constraint's.
    /// </summary>
    /// <remarks>
    /// Printed whatever was found, like the rules' tests. The third is the one that routes work away from
    /// this record and into something that can fail a build, which is the direction the layering in §2.3
    /// always runs.
    /// </remarks>
    private static void DecisionClosing(StringBuilder output, ProposeDecisions.Response response)
    {
        output.AppendLine();

        if (!response.HasMaterial)
        {
            output.AppendLine(Indent + "No approved cycle has stated anything. A decision is captured at the moment");
            output.AppendLine(Indent + "a choice is made, so there is nothing to recover here -- record one by hand as");
            output.AppendLine(Indent + "the choice is taken, rather than waiting for a cycle to have said it.");
            return;
        }

        output.AppendLine(Indent + "None of this is a decision yet. Three tests, all of which have to pass:");
        output.AppendLine();
        output.AppendLine(Field + "1. the alternatives were real -- somebody could have chosen otherwise");
        output.AppendLine(Field + "2. the reason is not recoverable from the code");
        output.AppendLine(Field + "3. it forbids nothing");
        output.AppendLine();
        output.AppendLine(Indent + "Test one is what separates a decision from a fact: an entry whose alternatives");
        output.AppendLine(Indent + "were never real records something nobody chose. Test three is the one that sends");
        output.AppendLine(Indent + "work elsewhere -- where a choice does forbid something, that half is a constraint");
        output.AppendLine(Indent + "or an architecture test, and the two are separate artifacts of one choice: the");
        output.AppendLine(Indent + "decision says why, the rule forbids the alternative.");
        output.AppendLine();
        output.AppendLine(Indent + "Nothing here writes the record. A stage reads it for alignment, so an agent that");
        output.AppendLine(Indent + "could write it could invent doctrine and then align to it.");
    }

    private static string Section(DecisionSource source) => source switch
    {
        DecisionSource.Intent => "intent  fixed decisions",
        DecisionSource.Plan => "plan    shape and departures",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown source."),
    };

    private static string Approved(ProposeDecisions.Response response)
    {
        int approved = response.Cycles - response.Unapproved;

        return response.Unapproved == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{approved} approved")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{approved} approved and {response.Unapproved} not read for having no standing approval");
    }

    private static string Standing(int number) => number == 1
        ? "1 decision"
        : string.Create(CultureInfo.InvariantCulture, $"{number} decisions");

    private static string Ids(IReadOnlyList<ConstraintId> ids) =>
        string.Join(", ", ids.Select(id => id.ToString()));

    private static string Cycles(int number) => number == 1
        ? "1 cycle"
        : string.Create(CultureInfo.InvariantCulture, $"{number} cycles");

    private static string Rules(int number) => number == 1
        ? "1 rule"
        : string.Create(CultureInfo.InvariantCulture, $"{number} rules");
}
