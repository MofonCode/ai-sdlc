using System.Globalization;
using System.Text;

namespace Sdlc.Core.Cycles;

/// <summary>Renders which cycle a skill run without an id means, for the skill and the person behind it.</summary>
/// <remarks>
/// When exactly one is ready the first line is the id alone and nothing follows, so a skill reads it without
/// parsing prose. Otherwise every candidate is listed, id first, with what the step needs, so the person can be
/// asked which one.
/// </remarks>
public static class ResolveRenderer
{
    private const string Indent = "  ";

    /// <summary>Renders the answer.</summary>
    /// <param name="resolved">What was resolved.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(ResolveCycle.Response resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);

        StringBuilder output = new();
        string step = NameOf(resolved.Step);
        string where = resolved.Branch is null ? "this working copy" : resolved.Branch;

        if (resolved.Only is { } only)
        {
            output.AppendLine(only.ToString());
            return output.ToString();
        }

        if (resolved.Ready.Count > 1)
        {
            output.AppendLine(resolved.Step == NextStep.Any
                ? string.Create(CultureInfo.InvariantCulture, $"{where} carries {resolved.Ready.Count} cycles. Ask which; never pick one:")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{resolved.Ready.Count} cycles {where} carries are ready for {step}. Ask which; never pick one:"));
            Lines(output, resolved.Ready);
            return output.ToString();
        }

        output.AppendLine(resolved.Step == NextStep.Any
            ? string.Create(CultureInfo.InvariantCulture, $"{where} carries no cycle. Name one; 'sdlc cycles' lists them all.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"No cycle {where} carries is ready for {step}. {Needs(resolved.Step)} Name the cycle, or run 'sdlc status <id>' to see where one stands."));

        if (resolved.Carried.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("It carries:");
            Lines(output, resolved.Carried);
        }

        return output.ToString();
    }

    private static string NameOf(NextStep step) => step switch
    {
        NextStep.Spec => "spec",
        NextStep.Plan => "plan",
        NextStep.Build => "build",
        NextStep.Verify => "verify",
        NextStep.Remediate => "remediate",
        NextStep.ToPlan => "to-plan",
        NextStep.Pr => "a pull request",
        _ => "anything",
    };

    private static string Needs(NextStep step) => step switch
    {
        NextStep.Spec => "A spec needs an intent whose latest round is clean, and no spec yet.",
        NextStep.Plan => "A plan needs a spec whose latest round is clean, and no plan yet.",
        NextStep.Build => "A build needs the gate to let it through, and nothing built yet.",
        NextStep.Verify => "A verify needs a build's recorded evidence, and no verify yet.",
        NextStep.ToPlan => "Taking one to plan needs an intent, a plan not yet clean, and no standing approval.",
        NextStep.Pr => "A pull request needs a verify that passed.",
        _ => "Remediation needs a blocked verify.",
    };

    private static void Lines(StringBuilder output, IReadOnlyList<ResolveCycle.Candidate> candidates)
    {
        foreach (ResolveCycle.Candidate candidate in candidates)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{candidate.Id}  {candidate.Headline ?? "(no headline)"}"));
        }
    }
}
