using System.Globalization;
using System.Text;
using Sdlc.Core.Areas;

namespace Sdlc.Core.Constraints;

/// <summary>How much of each constraint to render.</summary>
public enum ConstraintDetail
{
    /// <summary>Id and statement only — enough to see what is in force.</summary>
    Brief,

    /// <summary>Every clause. What a session needs in order to judge against the set.</summary>
    Full,
}

/// <summary>
/// Renders a constraint set as text.
/// </summary>
/// <remarks>
/// The rendering is part of the contract rather than presentation, because a skill shows this output
/// verbatim and a session judges against it. So it lives here with the model and is asserted by the
/// suite, rather than being formatted at each call site where three hosts would drift apart.
/// The frame this draws - headings, labels, indentation - is ASCII, so it renders in a pipeline log
/// and in a console that is not UTF-8. A constraint's own text is passed through verbatim, em dashes
/// included: a session judges against what it reads here and a finding cites it, so normalising the
/// content would leave citations quoting text that differs from the library.
/// </remarks>
public static class ConstraintRenderer
{
    private const int Width = 92;
    private const string Indent = "    ";
    private const string ClauseIndent = "               ";

    /// <summary>
    /// Renders the set.
    /// </summary>
    /// <param name="set">The set to render.</param>
    /// <param name="detail">How much of each constraint to include.</param>
    /// <param name="artifact">The artifact the set was narrowed to, when it was.</param>
    /// <param name="scope">
    /// The areas the stage touches: one of this solution's own rules outside them is rendered by its headline
    /// alone (D-151). Null renders every rule at <paramref name="detail"/>; a framework or stack rule always is.
    /// </param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(EffectiveConstraints.Response set, ConstraintDetail detail, string? artifact = null, AreaScope? scope = null)
    {
        ArgumentNullException.ThrowIfNull(set);
        scope ??= AreaScope.Everything;
        int headlinesOnly = 0;

        StringBuilder output = new();

        string heading = artifact is null
            ? string.Create(CultureInfo.InvariantCulture, $"Constraints in force: {set.Constraints.Count}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Constraints in force for {artifact}: {set.Constraints.Count}");

        output.AppendLine(heading);

        if (set.Constraints.Count == 0)
        {
            output.AppendLine();
            output.AppendLine("Nothing binds this artifact. Every blocking finding cites a constraint, so");
            output.AppendLine("there is nothing here for an interrogation to raise.");
            return output.ToString();
        }

        foreach (Constraint constraint in set.Constraints)
        {
            output.AppendLine();
            string areas = constraint.InAreas.Count == 0
                ? string.Empty
                : " [" + string.Join(", ", constraint.InAreas) + "]";

            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{constraint.Id,-10} {constraint.Statement}{areas}"));

            if (detail == ConstraintDetail.Brief)
            {
                continue;
            }

            if (constraint.Id.Tier == ConstraintTier.Solution && !scope.InFull(constraint.InAreas))
            {
                headlinesOnly++;
                continue;
            }

            Clause(output, "applies to", constraint.AppliesTo);
            Clause(output, "forbids", constraint.Forbids);
            Clause(output, "why", constraint.Why);

            if (constraint.Judged is { } judged)
            {
                Clause(output, "judged", judged == JudgedPer.Criterion ? "per criterion" : "per outcome");
            }
        }

        if (headlinesOnly > 0)
        {
            output.AppendLine();
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{headlinesOnly} of this solution's rules are shown by their headline alone: they apply to areas this"));
            output.AppendLine("stage does not touch. 'sdlc constraints show LOC_nn' prints one in full.");
        }

        output.AppendLine();
        output.AppendLine("Read from");
        foreach (ConstraintDocument source in set.Sources)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{ConstraintId.NameOf(source.Tier),-10} {source.Source}"));
        }

        if (set.Superseded.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("Superseded, kept for the record");
            foreach (Constraint constraint in set.Superseded)
            {
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Indent}{constraint.Id,-10} replaced by {constraint.SupersededBy}"));
            }
        }

        return output.ToString();
    }

    /// <summary>Renders one rule in full: what a stage handed only its headline asks for (D-151).</summary>
    /// <param name="constraint">The rule, in force or superseded.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string RenderOne(Constraint constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);

        StringBuilder output = new();
        string areas = constraint.InAreas.Count == 0 ? string.Empty : " [" + string.Join(", ", constraint.InAreas) + "]";
        string marker = constraint.IsInForce ? string.Empty : "  (superseded by " + constraint.SupersededBy + ")";

        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{constraint.Id} {constraint.Statement}{areas}{marker}"));
        Clause(output, "applies to", constraint.AppliesTo);
        Clause(output, "forbids", constraint.Forbids);
        Clause(output, "why", constraint.Why);
        return output.ToString();
    }

    private static void Clause(StringBuilder output, string label, string body)
    {
        bool first = true;
        foreach (string line in Wrap(body, Width - ClauseIndent.Length))
        {
            if (first)
            {
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Indent}{label,-11}{line}"));
                first = false;
            }
            else
            {
                output.AppendLine(ClauseIndent + line);
            }
        }
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        StringBuilder line = new();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }
}
