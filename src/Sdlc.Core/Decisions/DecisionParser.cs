using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentResults;

namespace Sdlc.Core.Decisions;

/// <summary>
/// Reads a decision document into decisions, and refuses one that is missing a part.
/// </summary>
/// <remarks>
/// Deliberately the same shape as the constraint parser, because a reader meets both and a second
/// arrangement of one idea is a second thing to learn. A decision missing <c>Instead of</c> is invalid
/// rather than untidy: without it the entry records something nobody chose, which is the one kind of
/// entry this record exists to keep out. The other three are what a later cycle reads instead of
/// re-deciding.
/// </remarks>
public static class DecisionParser
{
    /// <summary>The file a solution's decisions live in, inside the framework's directory.</summary>
    public const string FileName = "decisions.md";

    private static readonly Regex _headingPattern = new(
        @"^##\s+(?<id>DEC_[0-9]{2,4})\s+[—-]\s+(?<statement>.+?)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly Regex _clausePattern = new(
        @"^\*\*(?<label>Decided|Because|Instead of|From|Superseded-by):\*\*\s*(?<body>.*)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Parses every decision in a document.
    /// </summary>
    /// <param name="markdown">The document's text.</param>
    /// <param name="source">What to call the document in an error message — a path, usually.</param>
    /// <returns>
    /// The decisions in document order, or a failure listing every malformed one. All of them, because
    /// reporting the first and stopping would mean a separate run per defect.
    /// </returns>
    public static Result<IReadOnlyList<Decision>> Parse(string markdown, string source)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        List<Decision> decisions = [];
        List<string> errors = [];
        List<DecisionId> seen = [];

        foreach (Block block in Blocks(markdown))
        {
            if (!DecisionId.TryParse(block.Id, out DecisionId? id))
            {
                errors.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: '{block.Id}' is not a well-formed decision id."));
                continue;
            }

            if (seen.Exists(other => other.Number == id.Value.Number))
            {
                errors.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: {id} appears more than once. Ids are assigned once and never reused."));
                continue;
            }

            string? decided = block.Clause("Decided");
            string? because = block.Clause("Because");
            string? insteadOf = block.Clause("Instead of");
            string? from = block.Clause("From");

            foreach ((string label, string? value) in new[]
            {
                ("Decided", decided),
                ("Because", because),
                ("Instead of", insteadOf),
                ("From", from),
            })
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: {id} has no '{label}' clause. All four parts are required."));
                }
            }

            if (string.IsNullOrWhiteSpace(decided)
                || string.IsNullOrWhiteSpace(because)
                || string.IsNullOrWhiteSpace(insteadOf)
                || string.IsNullOrWhiteSpace(from))
            {
                continue;
            }

            DecisionId? supersededBy = null;
            string? supersededText = block.Clause("Superseded-by");

            if (!string.IsNullOrWhiteSpace(supersededText))
            {
                if (!DecisionId.TryParse(supersededText, out DecisionId? replacement))
                {
                    errors.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: {id} names '{supersededText}' as its replacement, which is not a well-formed id."));
                    continue;
                }

                supersededBy = replacement;
            }

            seen.Add(id.Value);
            decisions.Add(new Decision(
                id.Value,
                block.Statement,
                decided,
                because,
                insteadOf,
                from,
                supersededBy));
        }

        return errors.Count > 0
            ? Result.Fail<IReadOnlyList<Decision>>(errors.ConvertAll(message => new Error(message)))
            : Result.Ok<IReadOnlyList<Decision>>(decisions);
    }

    /// <summary>
    /// The next free id: one past the highest ever used, never the first gap.
    /// </summary>
    /// <remarks>
    /// Ids are a monotonic sequence, assigned once and never reused. A gap is the record of a decision
    /// that was removed, and filling it would make a cycle citing the old id read as the new entry.
    /// </remarks>
    /// <param name="decisions">What the document already holds, superseded entries included.</param>
    /// <returns>The id a new decision takes.</returns>
    public static DecisionId NextId(IReadOnlyList<Decision> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        return DecisionId.Of(decisions.Count == 0
            ? 1
            : decisions.Max(decision => decision.Id.Number) + 1);
    }

    private static IEnumerable<Block> Blocks(string markdown)
    {
        Block? current = null;

        foreach (string line in markdown.Split('\n'))
        {
            string text = line.TrimEnd('\r');
            Match heading = _headingPattern.Match(text);

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

            Match clause = _clausePattern.Match(text);

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
    /// One decision's heading and clauses, accumulated as lines arrive. A clause's body may wrap over
    /// several lines, so a line that is not a new clause continues the open one.
    /// </summary>
    private sealed class Block(string id, string statement)
    {
        private readonly Dictionary<string, StringBuilder> _clauses = new(StringComparer.Ordinal);
        private string? _open;

        public string Id { get; } = id;

        public string Statement { get; } = statement;

        public void Open(string label, string body)
        {
            _open = label;
            _clauses[label] = new StringBuilder(body.Trim());
        }

        public void Continue(string text)
        {
            if (_open is null || text.Trim().Length == 0)
            {
                _open = null;
                return;
            }

            _clauses[_open].Append(' ').Append(text.Trim());
        }

        public string? Clause(string label) =>
            _clauses.TryGetValue(label, out StringBuilder? body) && body.Length > 0
                ? body.ToString()
                : null;
    }
}
