using System.Globalization;
using System.Text.RegularExpressions;
using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Decisions;

/// <summary>
/// One declared departure from a recorded decision: which decision, and what this cycle does instead.
/// </summary>
/// <param name="Decision">The decision departed from.</param>
/// <param name="Text">
/// The rest of the line after the decision's id, as the document wrote it: the decision in brief, what this
/// cycle does instead, and who decided it and why.
/// </param>
/// <param name="Where">The document that declared it.</param>
public sealed record Departure(DecisionId Decision, string Text, ArtifactKind Where);

/// <summary>
/// Reads the departures a cycle declares, so the binary can report them and the readers can rely on them.
/// </summary>
/// <remarks>
/// <para><strong>A departure is declared once, where it arises, and every later stage relies on it</strong>
/// (D-139). An outcome a decision set aside is declared in the intent, under <c>Fixed decisions</c>, with
/// the person's name on it; an approach is declared in the plan, under <c>Shape and departures</c>. A spec
/// declares none: a criterion requiring something a decision set aside rests on the intent's.</para>
/// <para><strong>One line with a fixed opening</strong> — <c>- Departs from DEC_08 (...): ...</c> — because
/// the person is told about every departure at the gate, in the status panel and in the pull request, and
/// a model summarising prose each time would word it differently each time. Parsed here, it reads the same
/// everywhere.</para>
/// </remarks>
public static partial class Departures
{
    /// <summary>How a departure line opens, before the decision's id.</summary>
    public const string Opening = "Departs from ";

    [GeneratedRegex(@"^Departs from (?<id>DEC_[0-9]{2,4})\b\s*(?<rest>.*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Line();

    [GeneratedRegex(@"^Departs from\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Attempt();

    /// <summary>The section of a document a departure is declared in, or null for one that declares none.</summary>
    /// <param name="kind">The document.</param>
    /// <returns>The section's heading.</returns>
    public static string? SectionOf(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Intent => "Fixed decisions",
        ArtifactKind.Plan => "Shape and departures",
        _ => null,
    };

    /// <summary>The departures one document declares, in the order it declares them.</summary>
    /// <param name="kind">Which document.</param>
    /// <param name="markdown">Its text.</param>
    /// <returns>The departures; empty when it declares none, or is a kind that declares none.</returns>
    public static IReadOnlyList<Departure> In(ArtifactKind kind, string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        List<Departure> found = [];

        foreach ((string text, _) in Items(kind, markdown))
        {
            Match match = Line().Match(text);

            if (match.Success && DecisionId.TryParse(match.Groups["id"].Value, out DecisionId? id))
            {
                found.Add(new Departure(id.Value, match.Groups["rest"].Value.Trim(), kind));
            }
        }

        return found;
    }

    /// <summary>
    /// What is wrong with the departures a document declares: a line that opens like one and names no
    /// decision, or names one the record does not hold.
    /// </summary>
    /// <remarks>
    /// A superseded decision is accepted. Mining may supersede the very decision a cycle departed from,
    /// on the cycle's own branch after its verify (D-111), and the departure stays true of the cycle.
    /// </remarks>
    /// <param name="kind">Which document.</param>
    /// <param name="markdown">Its text.</param>
    /// <param name="recorded">Every decision the record holds, superseded ones included.</param>
    /// <returns>One violation per bad line; empty when there are none.</returns>
    public static IReadOnlyList<SchemaViolation> Check(ArtifactKind kind, string markdown, IReadOnlyList<Decision> recorded)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(recorded);

        List<SchemaViolation> violations = [];
        HashSet<int> known = [.. recorded.Select(decision => decision.Id.Number)];

        foreach ((string text, string section) in Items(kind, markdown))
        {
            if (!Attempt().IsMatch(text))
            {
                continue;
            }

            Match match = Line().Match(text);

            if (!match.Success || !DecisionId.TryParse(match.Groups["id"].Value, out DecisionId? id))
            {
                violations.Add(new SchemaViolation(
                    "## " + section,
                    string.Create(CultureInfo.InvariantCulture, $"has a departure that names no decision: '{Brief(text)}'. Write it as 'Departs from DEC_nn (...): ...' with the id from 'sdlc decisions'.")));
                continue;
            }

            if (!known.Contains(id.Value.Number))
            {
                violations.Add(new SchemaViolation(
                    "## " + section,
                    string.Create(CultureInfo.InvariantCulture, $"departs from {id.Value}, which decisions.md does not hold. 'sdlc decisions' lists what is recorded.")));
            }
        }

        return violations;
    }

    /// <summary>
    /// Every decision a solution's record holds, superseded ones included; empty when there is no record or
    /// it does not parse.
    /// </summary>
    /// <remarks>
    /// Empty rather than failed on a record that does not parse: the record's own shape is reported by
    /// <c>sdlc decisions</c>, and a departure check is not the place to refuse a cycle for it.
    /// </remarks>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The decisions.</returns>
    public static async Task<IReadOnlyList<Decision>> Recorded(string solutionRoot, CancellationToken cancellationToken)
    {
        string path = Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, DecisionParser.FileName);

        if (!File.Exists(path))
        {
            return [];
        }

        Result<IReadOnlyList<Decision>> read = DecisionParser.Parse(
            await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
            path);

        return read.IsSuccess ? read.Value : [];
    }

    /// <summary>A document's shape, with its departures checked against the record.</summary>
    /// <param name="shape">What the schema made of it.</param>
    /// <param name="markdown">Its text.</param>
    /// <param name="recorded">Every decision the record holds.</param>
    /// <returns>The shape, with a violation added for each bad departure.</returns>
    public static ArtifactValidation Checked(ArtifactValidation shape, string markdown, IReadOnlyList<Decision> recorded)
    {
        ArgumentNullException.ThrowIfNull(shape);

        IReadOnlyList<SchemaViolation> bad = Check(shape.Kind, markdown, recorded);

        return bad.Count == 0 ? shape : shape with { Violations = [.. shape.Violations, .. bad] };
    }

    /// <summary>
    /// The departures a cycle declares, intent first, then plan. Documents that do not exist yet declare
    /// nothing.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The departures.</returns>
    public static async Task<IReadOnlyList<Departure>> Of(Cycle cycle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        List<Departure> all = [];

        foreach (ArtifactKind kind in (ArtifactKind[])[ArtifactKind.Intent, ArtifactKind.Plan])
        {
            string path = cycle.PathOf(kind);

            if (File.Exists(path))
            {
                all.AddRange(In(kind, await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)));
            }
        }

        return all;
    }

    /// <summary>
    /// The departures as a person reads them: a count, then one line each, the decision's own words first.
    /// </summary>
    /// <param name="departures">The departures.</param>
    /// <param name="recorded">The record, for each decision's statement; superseded ones included.</param>
    /// <returns>The lines, without a trailing blank; empty when there are no departures.</returns>
    public static IReadOnlyList<string> Report(IReadOnlyList<Departure> departures, IReadOnlyList<Decision> recorded)
    {
        ArgumentNullException.ThrowIfNull(departures);
        ArgumentNullException.ThrowIfNull(recorded);

        if (departures.Count == 0)
        {
            return [];
        }

        List<string> lines =
        [
            string.Create(CultureInfo.InvariantCulture, $"This cycle departs from {departures.Count} recorded decision{(departures.Count == 1 ? string.Empty : "s")}:"),
        ];

        foreach (Departure departure in departures)
        {
            string statement = recorded.FirstOrDefault(decision => decision.Id == departure.Decision)?.Statement ?? "not in decisions.md";
            string instead = Instead(departure.Text);

            lines.Add(string.Create(CultureInfo.InvariantCulture, $"  - {statement} ({departure.Decision})"));
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"    instead: {instead}"));
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"    declared in: {Cycle.FileNameOf(departure.Where)}"));
        }

        return lines;
    }

    /// <summary>Each departure as one short line: the decision's own words, its id after.</summary>
    /// <param name="departures">The departures.</param>
    /// <param name="recorded">The record, for each decision's statement; superseded ones included.</param>
    /// <returns>One line per departure.</returns>
    public static IReadOnlyList<string> Named(IReadOnlyList<Departure> departures, IReadOnlyList<Decision> recorded)
    {
        ArgumentNullException.ThrowIfNull(departures);
        ArgumentNullException.ThrowIfNull(recorded);

        return [.. departures.Select(departure => string.Create(
            CultureInfo.InvariantCulture,
            $"{recorded.FirstOrDefault(decision => decision.Id == departure.Decision)?.Statement ?? "not in decisions.md"} ({departure.Decision})"))];
    }

    // What the cycle does instead, without the "(the decision in brief):" the line opens with -- the
    // report already states the decision in the record's own words.
    private static string Instead(string text)
    {
        string rest = text.Trim();

        if (rest.StartsWith('('))
        {
            int close = rest.IndexOf(')', StringComparison.Ordinal);

            if (close > 0)
            {
                rest = rest[(close + 1)..].TrimStart();
            }
        }

        return rest.TrimStart(':', ' ', '-', '—').Trim();
    }

    private static string Brief(string text) => text.Length <= 80 ? text : text[..77] + "...";

    // Each list item of the declaring section, its continuation lines joined on.
    private static IEnumerable<(string Text, string Section)> Items(ArtifactKind kind, string markdown)
    {
        if (SectionOf(kind) is not { } section
            || !ArtifactSections.Read(markdown).TryGetValue(section, out IReadOnlyList<string>? lines))
        {
            yield break;
        }

        string? current = null;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    yield return (current, section);
                }

                current = trimmed[2..].Trim();
            }
            else if (current is not null && trimmed.Length > 0 && line.StartsWith("  ", StringComparison.Ordinal))
            {
                current += " " + trimmed;
            }
            else if (current is not null)
            {
                yield return (current, section);
                current = null;
            }
        }

        if (current is not null)
        {
            yield return (current, section);
        }
    }
}
