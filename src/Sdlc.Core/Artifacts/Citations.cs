using System.Globalization;
using System.Text.RegularExpressions;
using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Findings;

namespace Sdlc.Core.Artifacts;

/// <summary>
/// What a document cites by number -- a recorded decision, a criterion of its spec -- held to what exists (D-223).
/// </summary>
/// <remarks>
/// <para><strong>A number either points at something or it does not, and that is a lookup.</strong> Readers were
/// the only check: across one governed solution's cycles they raised intents and plans citing decisions the record
/// did not hold, an Out of scope entry crediting a criterion for what another did after a renumbering, and criteria
/// out of order, each as a suggestion somebody then had to answer. A reader spending a round on a dangling number is
/// a reader not reading the document.</para>
/// <para><strong>A decision is cited once it is placed, on the branch the cycle is planned from.</strong> The record
/// readers are handed is the one in the cycle's own checkout. A decision a harvest proposed and nobody placed, or one
/// a person placed in another checkout and has not committed to the integration branch, is not in it, so the
/// citation points at nothing for every reader -- and a placed number can still move before it is committed. A
/// superseded decision is accepted, as a departure from one is (D-139): a harvest may supersede it after the cycle
/// cited it.</para>
/// <para><strong>A criterion is cited by its number, and the spec says which numbers there are.</strong> A
/// reference to another cycle's criterion names that cycle (its id, <c>successor:</c>, or <i>another cycle</i> in words) and is
/// left alone. A spec's criteria run in ascending order, so a renumbering that left one behind is seen.</para>
/// <para>Held only by cycles opened on the release that brought it, so a document approved before is never refused
/// for it (<see cref="NewerRules.Citations"/>).</para>
/// </remarks>
public static partial class Citations
{
    // A decision id as the record writes it, standing alone.
    [GeneratedRegex(@"(?<![A-Za-z0-9_])DEC_(?<number>[0-9]{2,4})(?![A-Za-z0-9_])", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DecisionMention();

    // "criterion 4", "criteria 2, 3 and 7", "criteria 2-4", "criterion #3", "criteria 1, 2a and 2b".
    [GeneratedRegex(@"(?<![A-Za-z])criteri(?:on|a)\s+#?(?<list>[0-9]{1,3}[a-z]?(?:(?:\s*,\s*(?:and\s+|or\s+)?|\s+and\s+|\s+or\s+|\s*&\s*|\s*(?:–|—|-|to)\s*)#?[0-9]{1,3}[a-z]?)*)(?![0-9])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CriterionMention();

    [GeneratedRegex(@"(?<number>[0-9]{1,3})[a-z]?(?:\s*(?<range>–|—|-|to)\s*#?(?<end>[0-9]{1,3})[a-z]?)?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CriterionNumber();

    // What marks a reference as another cycle's: its id, a successor, or another cycle named in words.
    [GeneratedRegex(@"[0-9]{8}_[0-9]{4}_[0-9]{2}|successor:|\b(?:another|other|earlier|previous|later|sibling|parent)\s+cycle", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Elsewhere();

    [GeneratedRegex(@"^\s*(?:[-*]\s+)?Departs from\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DepartureLine();

    [GeneratedRegex(@"^(?<number>[0-9]+)\.\s", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CriterionItem();

    /// <summary>How far before a criterion reference a mark of another cycle is looked for.</summary>
    private const int ElsewhereWindow = 80;

    /// <summary>The longest range a reference is read as, so a typo cannot name a thousand criteria.</summary>
    private const int LongestRange = 30;

    /// <summary>
    /// The decision ids a document cites that the record does not hold, one violation per id.
    /// </summary>
    /// <remarks>
    /// A departure line is the departure check's (<see cref="Departures.Check"/>), so it is not reported twice. A
    /// fenced block is left alone: it quotes rather than cites.
    /// </remarks>
    /// <param name="kind">Which document.</param>
    /// <param name="markdown">Its text.</param>
    /// <param name="recorded">Every decision the record holds, superseded ones included; empty when there is no record.</param>
    /// <returns>The violations; empty for a verify, which cites no decision of its own, or when every id is held.</returns>
    public static IReadOnlyList<SchemaViolation> Decisions(ArtifactKind kind, string markdown, IReadOnlyList<Decision> recorded)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(recorded);

        if (kind == ArtifactKind.Verify)
        {
            return [];
        }

        HashSet<int> known = [.. recorded.Select(decision => decision.Id.Number)];
        HashSet<int> reported = [];
        List<SchemaViolation> violations = [];
        string holds = recorded.Count == 0
            ? "this solution's decisions.md holds none"
            : string.Create(CultureInfo.InvariantCulture, $"decisions.md here holds DEC_{recorded.Min(decision => decision.Id.Number):00} to DEC_{recorded.Max(decision => decision.Id.Number):00}");

        foreach ((string section, string line) in Lines(markdown))
        {
            if (DepartureLine().IsMatch(line))
            {
                continue;
            }

            foreach (Match match in DecisionMention().Matches(line))
            {
                int number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);

                if (known.Contains(number) || !reported.Add(number))
                {
                    continue;
                }

                violations.Add(new SchemaViolation(
                    Where(section),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"cites {match.Value}, and {holds}. A decision is cited once it is placed and committed on the branch this cycle is planned from: one a harvest proposed, or one placed in another checkout and not yet on the integration branch, is not in the record this cycle's readers are handed, and its number can still move. Place it and bring it onto this branch, or cite what is recorded -- 'sdlc decisions' lists it.")));
            }
        }

        return violations;
    }

    /// <summary>
    /// The criterion numbers a document cites that its spec does not have, and on a spec, criteria out of order.
    /// </summary>
    /// <param name="kind">Which document. An intent has no criteria to cite.</param>
    /// <param name="markdown">Its text.</param>
    /// <param name="spec">The cycle's spec, or null when it has none yet; a spec is read from <paramref name="markdown"/>.</param>
    /// <returns>The violations; empty when the spec states no numbered criteria, which other checks already report.</returns>
    public static IReadOnlyList<SchemaViolation> Criteria(ArtifactKind kind, string markdown, string? spec)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        if (kind == ArtifactKind.Intent)
        {
            return [];
        }

        string source = kind == ArtifactKind.Spec ? markdown : spec ?? string.Empty;
        HashSet<int> criteria = [.. ArtifactSections.NumberedItems(source, "Acceptance criteria").Keys];

        if (criteria.Count == 0)
        {
            return [];
        }

        List<SchemaViolation> violations = [];

        if (kind == ArtifactKind.Spec)
        {
            violations.AddRange(OutOfOrder(markdown));
        }

        // The numbered entries that answer criteria one by one: a plan's proof, a verify's adjudication.
        string? answering = kind switch
        {
            ArtifactKind.Plan => "Proof",
            ArtifactKind.Verify => "Criteria",
            _ => null,
        };

        HashSet<int> reported = [];
        string has = Has(criteria);

        if (answering is not null)
        {
            foreach (int number in ArtifactSections.NumberedItems(markdown, answering).Keys.Where(number => !criteria.Contains(number)).Order())
            {
                reported.Add(number);
                violations.Add(new SchemaViolation(
                    "## " + answering,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"has an entry for criterion {number}, and the spec {has}. A spec renumbered under it leaves the entry answering a criterion that is not there: give it the criterion's number now.")));
            }
        }

        foreach ((string section, string line) in Lines(markdown))
        {
            foreach (Match match in CriterionMention().Matches(line))
            {
                if (Elsewhere().IsMatch(line[Math.Max(0, match.Index - ElsewhereWindow)..match.Index]))
                {
                    continue;
                }

                foreach (int number in Numbers(match.Groups["list"].Value).Where(number => !criteria.Contains(number) && reported.Add(number)))
                {
                    violations.Add(new SchemaViolation(
                        Where(section),
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"cites criterion {number}, and the spec {has}. A reference left behind by a renumbering credits a criterion for what another does: give it the number the spec has now. A criterion of another cycle names that cycle beside it.")));
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Everything a cycle's document cites that does not exist, when the cycle is held to it; empty otherwise.
    /// </summary>
    /// <remarks>
    /// A record that does not parse is not judged here: <c>sdlc decisions</c> reports its shape, and refusing a
    /// document over it would send the author to the wrong file.
    /// </remarks>
    /// <param name="cycle">The cycle.</param>
    /// <param name="kind">Which document.</param>
    /// <param name="markdown">Its text.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The violations.</returns>
    public static async Task<IReadOnlyList<SchemaViolation>> Of(Cycle cycle, ArtifactKind kind, string markdown, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(markdown);

        if (!await NewerRules.Since(cycle, NewerRules.Citations, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        List<SchemaViolation> violations = [];

        if (await Record(cycle.SolutionRoot, cancellationToken).ConfigureAwait(false) is { } recorded)
        {
            violations.AddRange(Decisions(kind, markdown, recorded));
        }

        string specPath = cycle.PathOf(ArtifactKind.Spec);
        string? spec = kind != ArtifactKind.Spec && File.Exists(specPath)
            ? await File.ReadAllTextAsync(specPath, cancellationToken).ConfigureAwait(false)
            : null;

        violations.AddRange(Criteria(kind, markdown, spec));

        return violations;
    }

    /// <summary>A document's shape, with what it cites checked when its cycle is held to it.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="shape">What the schema made of it.</param>
    /// <param name="markdown">Its text.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The shape, with a violation added for each dangling citation.</returns>
    public static async Task<ArtifactValidation> Checked(Cycle cycle, ArtifactValidation shape, string markdown, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shape);

        IReadOnlyList<SchemaViolation> bad = await Of(cycle, shape.Kind, markdown, cancellationToken).ConfigureAwait(false);

        return bad.Count == 0 ? shape : shape with { Violations = [.. shape.Violations, .. bad] };
    }

    /// <summary>
    /// The refusal a round gives a document citing what does not exist, before its readers are paid for and again
    /// when it is recorded; null when it cites nothing missing.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="kind">Which document.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The refusal, or null.</returns>
    internal static async Task<string?> Refusal(Cycle cycle, ArtifactKind kind, CancellationToken cancellationToken)
    {
        string path = cycle.PathOf(kind);

        if (!File.Exists(path))
        {
            return null;
        }

        string markdown = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SchemaViolation> bad = await Of(cycle, kind, markdown, cancellationToken).ConfigureAwait(false);

        if (bad.Count == 0)
        {
            return null;
        }

        string name = Cycle.FileNameOf(kind);
        string repair = kind == ArtifactKind.Verify
            ? "The adjudicator wrote it, so run it again: a reader would spend the round on a number, not the verdict."
            : string.Create(CultureInfo.InvariantCulture, $"Correct {name} and ask again: a reader would spend the round on a number, not the document.");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{name} cites what does not exist, so no reader is started:{Environment.NewLine}{string.Join(Environment.NewLine, bad.Select(violation => "  " + violation.Where + " " + violation.Problem))}{Environment.NewLine}{repair}");
    }

    /// <summary>The criteria a spec numbers out of ascending order.</summary>
    private static IEnumerable<SchemaViolation> OutOfOrder(string markdown)
    {
        if (!ArtifactSections.Read(markdown).TryGetValue("Acceptance criteria", out IReadOnlyList<string>? lines))
        {
            yield break;
        }

        int last = 0;

        foreach (string line in lines)
        {
            Match item = CriterionItem().Match(line);

            if (!item.Success)
            {
                continue;
            }

            int number = int.Parse(item.Groups["number"].Value, CultureInfo.InvariantCulture);

            if (number <= last)
            {
                yield return new SchemaViolation(
                    "## Acceptance criteria",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"numbers criterion {number} after criterion {last}. Criteria run in ascending order, each number once, so a reference to one finds one criterion: renumber them, and every reference to them."));
            }

            last = Math.Max(last, number);
        }
    }

    /// <summary>The numbers one reference names, a range read out.</summary>
    private static IEnumerable<int> Numbers(string list)
    {
        foreach (Match match in CriterionNumber().Matches(list))
        {
            int first = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);

            if (!match.Groups["end"].Success)
            {
                yield return first;
                continue;
            }

            int end = int.Parse(match.Groups["end"].Value, CultureInfo.InvariantCulture);

            if (end < first || end - first > LongestRange)
            {
                yield return first;
                yield return end;
                continue;
            }

            for (int number = first; number <= end; number++)
            {
                yield return number;
            }
        }
    }

    /// <summary>The spec's criteria as a reader of the refusal needs them.</summary>
    private static string Has(HashSet<int> criteria)
    {
        List<int> sorted = [.. criteria.Order()];
        bool contiguous = sorted[^1] - sorted[0] + 1 == sorted.Count;

        return contiguous
            ? string.Create(CultureInfo.InvariantCulture, $"has criteria {sorted[0]} to {sorted[^1]}")
            : "has criteria " + string.Join(", ", sorted);
    }

    private static string Where(string section) => section.Length == 0 ? "(before the first section)" : "## " + section;

    /// <summary>Each line of a document with the section it is in, outside the frontmatter and fenced blocks.</summary>
    private static IEnumerable<(string Section, string Line)> Lines(string markdown)
    {
        string section = string.Empty;
        bool frontmatter = false;
        bool seenFence = false;
        bool fenced = false;

        foreach (string raw in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            string trimmed = raw.Trim();

            if (trimmed == "---" && !fenced)
            {
                if (!seenFence)
                {
                    seenFence = true;
                    frontmatter = true;
                    continue;
                }

                if (frontmatter)
                {
                    frontmatter = false;
                    continue;
                }
            }

            if (frontmatter)
            {
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }

            if (fenced)
            {
                continue;
            }

            if (raw.StartsWith("## ", StringComparison.Ordinal))
            {
                section = trimmed[3..].Trim();
                continue;
            }

            yield return (section, raw);
        }
    }

    /// <summary>Every decision the record holds; empty when there is none, null when it does not parse.</summary>
    private static async Task<IReadOnlyList<Decision>?> Record(string solutionRoot, CancellationToken cancellationToken)
    {
        string path = Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, DecisionParser.FileName);

        if (!File.Exists(path))
        {
            return [];
        }

        Result<IReadOnlyList<Decision>> read = DecisionParser.Parse(
            await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
            path);

        return read.IsSuccess ? read.Value : null;
    }
}
