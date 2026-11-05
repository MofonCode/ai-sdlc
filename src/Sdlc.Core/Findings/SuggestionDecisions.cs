using System.Globalization;
using System.Text.RegularExpressions;
using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Leads;

namespace Sdlc.Core.Findings;

/// <summary>Where a suggestion has got to, in one word (D-115).</summary>
public enum SuggestionStatus
{
    /// <summary>Nobody decided, and a document still raises it.</summary>
    Open,

    /// <summary>A person took it in: an intent's wanted outcome, or a cycle begun from it.</summary>
    Accepted,

    /// <summary>A person decided against it, in an intent or in an abandonment.</summary>
    Declined,

    /// <summary>Nobody decided, and no document still raises it.</summary>
    Answered,

    /// <summary>Nobody decided, and every document that raised it withdrew it.</summary>
    Withdrawn,
}

/// <summary>A person's decision about one suggestion, and the cycle that recorded it.</summary>
/// <param name="Status">Accepted or declined.</param>
/// <param name="DecidedIn">The cycle whose intent, lead or abandonment says so.</param>
public sealed record SuggestionDecision(SuggestionStatus Status, CycleId DecidedIn);

/// <summary>
/// Reads what people decided about suggestions from where they wrote it, on every read (D-115).
/// </summary>
/// <remarks>
/// <para><strong>Never stored.</strong> The decision's wording is in an intent or an <c>abandoned.md</c>,
/// so a status kept beside it would be a second copy that drifts the first time one changes without the
/// other. Read here, an abandoned cycle's acceptances lapse with no step anybody has to remember.</para>
/// <para><strong>The fixed form is <c>taking in &lt;id&gt;</c> and <c>declining &lt;id&gt;</c>.</strong> A bare
/// id is the deciding cycle's own suggestion, which is all <c>steer</c> takes; <c>&lt;cycle&gt;/&lt;id&gt;</c>
/// names another cycle's, which is how an abandonment declines what its lead accepted. Several ids may
/// follow one phrase, joined by commas or <em>and</em>.</para>
/// </remarks>
public static partial class SuggestionDecisions
{
    /// <summary>The key a decision is filed under: the source cycle and the suggestion's id.</summary>
    /// <param name="cycle">The cycle that raised the suggestion.</param>
    /// <param name="id">The suggestion's id.</param>
    /// <returns>The key.</returns>
    public static string Key(CycleId cycle, string id) => cycle.ToString() + "/" + id;

    /// <summary>
    /// Every decision the solution's cycles record, the latest cycle's winning where two decide one.
    /// </summary>
    /// <param name="cycles">The solution's cycles.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The decisions, by <see cref="Key"/>.</returns>
    public static async Task<IReadOnlyDictionary<string, SuggestionDecision>> Read(
        IReadOnlyList<Cycle> cycles,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycles);

        Dictionary<string, SuggestionDecision> decisions = new(StringComparer.Ordinal);

        foreach (Cycle cycle in cycles.OrderBy(cycle => cycle.Id.ToString(), StringComparer.Ordinal))
        {
            if (cycle.IsAbandoned)
            {
                // An abandoned cycle accepts nothing: its intent's lines and its lead stop counting, so
                // what it took in reads open again. Its abandonment may still decline, one line each with
                // its reason; a line kept with none declines nothing.
                string abandoned = await File.ReadAllTextAsync(cycle.AbandonedPath, cancellationToken).ConfigureAwait(false);

                foreach (string id in DeclinedIn(abandoned, withReason: true))
                {
                    decisions[Qualified(cycle.Id, id)] = new SuggestionDecision(SuggestionStatus.Declined, cycle.Id);
                }

                continue;
            }

            string path = cycle.PathOf(ArtifactKind.Intent);

            if (!File.Exists(path))
            {
                continue;
            }

            string intent = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            foreach (string id in Accepted(intent, cycle.Id))
            {
                decisions[Qualified(cycle.Id, id)] = new SuggestionDecision(SuggestionStatus.Accepted, cycle.Id);
            }

            foreach (string id in IdsAfter(Section(ArtifactSections.Read(intent), "Fixed decisions"), "declining"))
            {
                decisions[Qualified(cycle.Id, id)] = new SuggestionDecision(SuggestionStatus.Declined, cycle.Id);
            }
        }

        return decisions;
    }

    /// <summary>
    /// What one intent took in, as an abandonment would name each: its own suggestions by bare id, and the
    /// one it was begun from as <c>&lt;cycle&gt;/&lt;id&gt;</c>.
    /// </summary>
    /// <param name="intent">The intent's text.</param>
    /// <param name="cycle">The intent's cycle.</param>
    /// <returns>The references, in the order the intent gives them, the lead first.</returns>
    public static IReadOnlyList<string> Accepted(string intent, CycleId cycle)
    {
        ArgumentNullException.ThrowIfNull(intent);

        List<string> accepted = [];

        if (LeadOf(intent) is { } lead)
        {
            accepted.Add(lead);
        }

        accepted.AddRange(IdsAfter(Section(ArtifactSections.Read(intent), "Wanted outcomes"), "taking in"));

        return [.. accepted.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The ids that follow each occurrence of a phrase: <c>declining accessibility.S_04</c>,
    /// <c>taking in operability.S_01, maintainability.S_04 and security.S_05</c>.
    /// </summary>
    /// <param name="text">Where to look.</param>
    /// <param name="phrase">The phrase, matched in any case.</param>
    /// <returns>Each id named, in order.</returns>
    public static IReadOnlyList<string> IdsAfter(string text, string phrase)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(phrase);

        string flat = string.Join(' ', text.ReplaceLineEndings().Split(Environment.NewLine).Select(line => line.Trim()));
        List<string> ids = [];
        int at = 0;

        while ((at = flat.IndexOf(phrase, at, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            at += phrase.Length;

            foreach (string token in flat[at..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string bare = token.TrimEnd(',', ';', ':', '.', ')');

                if (IdPattern().IsMatch(bare))
                {
                    ids.Add(bare);

                    // A colon or a full stop ends the list: what follows is the reason or the next sentence.
                    if (token.EndsWith(':') || token.EndsWith('.') || token.EndsWith(';'))
                    {
                        break;
                    }

                    continue;
                }

                // Words that sit between the phrase and its ids: "the plan's", "and".
                if (bare is "the" or "and" || bare.EndsWith("'s", StringComparison.Ordinal))
                {
                    continue;
                }

                break;
            }
        }

        return ids;
    }

    /// <summary>
    /// The <c>declining &lt;id&gt;: &lt;reason&gt;</c> lines of an abandonment, one suggestion each.
    /// </summary>
    /// <param name="body">The abandonment's text.</param>
    /// <param name="withReason">True for the lines that give a reason, false for the ones that give none.</param>
    /// <returns>The ids, in order.</returns>
    public static IReadOnlyList<string> DeclinedIn(string body, bool withReason)
    {
        ArgumentNullException.ThrowIfNull(body);

        return
        [
            .. body.ReplaceLineEndings().Split(Environment.NewLine)
                .Select(line => DeclineLine().Match(line))
                .Where(match => match.Success && (match.Groups["reason"].Value.Trim().Length > 0) == withReason)
                .Select(match => match.Groups["id"].Value),
        ];
    }

    /// <summary>Whether a line is one of an abandonment's <c>declining &lt;id&gt;:</c> lines.</summary>
    /// <param name="line">The line.</param>
    /// <returns>True when it is.</returns>
    public static bool IsDeclineLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return DeclineLine().IsMatch(line);
    }

    private static string Section(IReadOnlyDictionary<string, IReadOnlyList<string>> sections, string heading) =>
        sections.TryGetValue(heading, out IReadOnlyList<string>? lines) ? string.Join('\n', lines) : string.Empty;

    private static string Qualified(CycleId deciding, string id)
    {
        int slash = id.IndexOf('/', StringComparison.Ordinal);

        return slash >= 0 && CycleId.TryParse(id[..slash], out CycleId? other)
            ? Key(other.Value, id[(slash + 1)..])
            : Key(deciding, id);
    }

    private static string? LeadOf(string intent)
    {
        Result<FrontmatterDocument> split = Frontmatter.Read(intent, "intent.md");

        if (split.IsFailed
            || Frontmatter.Key(split.Value.Yaml, "lead") is not { } text
            || !LeadReference.TryParse(text, out LeadReference? lead, out _)
            || lead.Source != LeadSource.Suggestion)
        {
            return null;
        }

        string[] parts = lead.Value.Split('/');

        return CycleId.TryParse(parts[0], out CycleId? source)
            ? Key(source.Value, parts[^1])
            : null;
    }

    [GeneratedRegex(@"^([0-9]{8}_[0-9]{4}_[0-9]{2}/)?([a-z][a-z-]*\.)?[FS]_[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^\s*(-\s*)?declining\s+(?<id>\S+?):(?<reason>.*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex DeclineLine();
}
