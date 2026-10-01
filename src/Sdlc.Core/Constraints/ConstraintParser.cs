using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentResults;

namespace Sdlc.Core.Constraints;

/// <summary>
/// Reads a constraint document into constraints, and refuses one that is missing a clause.
/// </summary>
/// <remarks>
/// A constraint missing <c>Applies to</c>, <c>Forbids</c> or <c>Why</c> is invalid rather than
/// untidy: the first two decide whether it can fire at all, and the third is what a person has at
/// a gate instead of the conversation that produced the rule. So this returns a failure naming the
/// constraint and the clause, rather than a constraint with an empty field that reads as complete.
/// </remarks>
public static class ConstraintParser
{
    private static readonly Regex HeadingPattern = new(
        @"^##\s+(?<id>(?:CON|PRO|LOC)_[0-9]{2,4}(?:\.[0-9]{1,3})?)\s+[—-]\s+(?<statement>.+?)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly Regex ClausePattern = new(
        @"^\*\*(?<label>Applies to|Forbids|Why|Superseded-by):\*\*\s*(?<body>.*)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Parses every constraint in a document.
    /// </summary>
    /// <param name="markdown">The document's text.</param>
    /// <param name="source">
    /// What to call the document in an error message — a path, usually. Errors name it because a
    /// malformed constraint is useless to chase without knowing which file holds it.
    /// </param>
    /// <returns>
    /// The constraints in document order, or a failure listing every malformed one. All of them:
    /// reporting the first and stopping would mean a separate run per defect.
    /// </returns>
    public static Result<IReadOnlyList<Constraint>> Parse(string markdown, string source)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        List<Constraint> constraints = [];
        List<string> errors = [];
        List<ConstraintId> seen = [];

        foreach (Block block in Blocks(markdown))
        {
            if (!ConstraintId.TryParse(block.Id, out ConstraintId? id))
            {
                errors.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: '{block.Id}' is not a well-formed constraint id."));
                continue;
            }

            if (seen.Exists(other => other.Number == id.Value.Number && other.Tier == id.Value.Tier))
            {
                errors.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: {id} appears more than once. Ids are assigned once and never reused."));
                continue;
            }

            string? appliesTo = block.Clause("Applies to");
            string? forbids = block.Clause("Forbids");
            string? why = block.Clause("Why");

            foreach ((string label, string? value) in new[]
            {
                ("Applies to", appliesTo),
                ("Forbids", forbids),
                ("Why", why),
            })
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: {id} has no '{label}' clause. All four parts are required."));
                }
            }

            if (string.IsNullOrWhiteSpace(appliesTo)
                || string.IsNullOrWhiteSpace(forbids)
                || string.IsNullOrWhiteSpace(why))
            {
                continue;
            }

            ConstraintId? supersededBy = null;
            string? supersededText = block.Clause("Superseded-by");
            if (!string.IsNullOrWhiteSpace(supersededText))
            {
                if (!ConstraintId.TryParse(supersededText, out ConstraintId? replacement))
                {
                    errors.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: {id} names '{supersededText}' as its replacement, which is not a well-formed id."));
                    continue;
                }

                supersededBy = replacement;
            }

            seen.Add(id.Value);
            constraints.Add(new Constraint(
                id.Value,
                block.Statement,
                appliesTo,
                forbids,
                why,
                supersededBy));
        }

        return errors.Count > 0
            ? Result.Fail<IReadOnlyList<Constraint>>(errors.ConvertAll(message => new Error(message)))
            : Result.Ok<IReadOnlyList<Constraint>>(constraints);
    }

    private static IEnumerable<Block> Blocks(string markdown)
    {
        Block? current = null;

        foreach (string line in markdown.Split('\n'))
        {
            string text = line.TrimEnd('\r');
            Match heading = HeadingPattern.Match(text);

            if (heading.Success)
            {
                if (current is not null)
                {
                    yield return current;
                }

                current = new Block(
                    heading.Groups["id"].Value,
                    heading.Groups["statement"].Value.Trim());
                continue;
            }

            if (current is null)
            {
                continue;
            }

            Match clause = ClausePattern.Match(text);
            if (clause.Success)
            {
                current.Open(clause.Groups["label"].Value, clause.Groups["body"].Value);
            }
            else
            {
                current.Continue(text);
            }
        }

        if (current is not null)
        {
            yield return current;
        }
    }

    /// <summary>
    /// One constraint's heading and clauses, accumulated as lines arrive. A clause's body may wrap
    /// over several lines, so a line that is not a new clause continues the open one.
    /// </summary>
    private sealed class Block(string id, string statement)
    {
        private readonly Dictionary<string, StringBuilder> clauses = new(StringComparer.Ordinal);
        private string? open;

        public string Id { get; } = id;

        public string Statement { get; } = statement;

        public void Open(string label, string body)
        {
            open = label;
            clauses[label] = new StringBuilder(body.Trim());
        }

        public void Continue(string text)
        {
            if (open is null)
            {
                return;
            }

            if (text.Length == 0)
            {
                open = null;
                return;
            }

            StringBuilder builder = clauses[open];
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(text.Trim());
        }

        public string? Clause(string label) =>
            clauses.TryGetValue(label, out StringBuilder? builder) ? builder.ToString().Trim() : null;
    }
}
