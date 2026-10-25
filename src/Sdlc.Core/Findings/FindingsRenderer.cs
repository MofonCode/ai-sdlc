using System.Globalization;
using System.Text;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>
/// Renders one cycle's interrogation history, per artifact.
/// </summary>
/// <remarks>
/// <para>Grouped by artifact and then by finding rather than by round, because the question somebody has
/// is "what is still wrong with the spec", not "what happened in round two". A round-first layout makes
/// a reader fold the history themselves, which is the fold they get wrong: an objection raised in round
/// one and answered in round two appears twice and reads as two.</para>
/// <para>The statement is shown in full and the remedy with it. A findings list that elided either would
/// be an index to a file somebody then opens, and the point of the command is not opening the file.</para>
/// <para>The cited rule's own line is shown under the id for the same reason, and it is the reason the
/// round carries the line at all. A bare `CON_16` is an index entry: it tells a reader that something
/// was breached and not what, so the only way to read the finding is to go and resolve the id, which
/// nobody does. Brief mode is the exception, because it is explicitly the list of ids for somebody
/// deciding what to open.</para>
/// </remarks>
public static class FindingsRenderer
{
    private const string Indent = "  ";

    /// <summary>
    /// Renders the findings for one cycle.
    /// </summary>
    /// <param name="response">What was read.</param>
    /// <param name="brief">
    /// True for one line per finding. The counts and ids, for somebody deciding what to open rather than
    /// reading the objections.
    /// </param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ReadFindings.Response response, bool brief = false)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder output = new();

        int blockers = response.Artifacts.Sum(artifact => artifact.Standing.Count(OpenBlocker));
        int suggestions = response.Artifacts.Sum(artifact => artifact.Standing.Count(OpenSuggestion));

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}{response.Cycle.Id}   blockers {blockers}   suggestions {suggestions}"));

        bool anything = false;

        foreach (ArtifactFindings artifact in response.Artifacts)
        {
            if (artifact.Rounds.Count == 0)
            {
                continue;
            }

            anything = true;
            output.AppendLine();

            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{Cycle.NameOf(artifact.Artifact)}   {Rounds(artifact)}"));

            // Only findings the asked-for rounds mentioned, but each at the disposition the whole
            // history gave it. Asking about one round is asking what that round said, not asking to be
            // told an answered objection is open.
            HashSet<string> mentioned =
            [
                .. artifact.Rounds.SelectMany(round => round.Findings).Select(finding => finding.Id),
            ];

            IReadOnlyList<Finding> shown =
            [
                .. artifact.Standing
                    .Where(finding => mentioned.Contains(finding.Id))
                    .OrderBy(finding => Rank(finding))
                    .ThenBy(finding => finding.Id, StringComparer.Ordinal),
            ];

            // Conflicts count as something raised. A round with no findings and a declared conflict is
            // the loop's third ending, not a quiet round -- and reporting "nothing raised" above it was
            // worse than saying nothing, because the one line that mattered was the one being denied.
            if (shown.Count == 0 && artifact.Conflicts.Count == 0)
            {
                output.AppendLine(Indent + "    nothing raised");
                continue;
            }

            foreach (Finding finding in shown)
            {
                Entry(output, finding, brief);
            }

            // After the findings, because a conflict is rarer and reads as the conclusion of the
            // history rather than one more item in it. Only the latest round's, for the reason
            // StandingConflicts gives: a conflict nobody re-declared was resolved.
            foreach (Conflict conflict in artifact.Conflicts)
            {
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Indent}    conflict  {conflict.Ids()}"));

                if (brief)
                {
                    continue;
                }

                foreach (Citation citation in conflict.Between ?? [])
                {
                    if (citation.Line() is { } rule)
                    {
                        output.AppendLine(Indent + "        " + rule);
                    }
                }

                output.AppendLine(Indent + "        " + conflict.Statement?.ReplaceLineEndings(" "));
            }
        }

        if (!anything)
        {
            output.AppendLine();
            output.AppendLine(Indent + "Nothing has been interrogated yet. A document nothing has argued");
            output.AppendLine(Indent + "with is not clean, it is unexamined.");
        }

        return output.ToString();
    }

    private static void Entry(StringBuilder output, Finding finding, bool brief)
    {
        string cites = finding.Cites?.Id is { } id ? "  " + id : string.Empty;

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}    {finding.Id}  {Describe(finding),-22}{cites}"));

        if (brief)
        {
            return;
        }

        if (finding.Cites?.Line() is { } rule)
        {
            output.AppendLine(Indent + "        " + rule);
        }

        output.AppendLine(Indent + "        " + finding.Statement.ReplaceLineEndings(" "));

        if (finding.Remedy is { } remedy && finding.Disposition == Disposition.Open)
        {
            output.AppendLine(Indent + "        remedy  " + remedy.ReplaceLineEndings(" "));
        }
    }

    /// <summary>
    /// How a finding's state reads.
    /// </summary>
    /// <remarks>
    /// The severity and the disposition together, because neither alone says whether anything is owed.
    /// An open blocker stops the cycle, an answered one is the record of an argument that was had, and a
    /// suggestion is work nobody is obliged to do -- three different things a single word would conflate.
    /// </remarks>
    private static string Describe(Finding finding) => string.Create(
        CultureInfo.InvariantCulture,
        $"{Word(finding.Severity)} {Word(finding.Disposition)}");

    private static string Word(Severity severity) => severity == Severity.Blocking
        ? "blocking"
        : "suggestion";

    /// <summary>
    /// Each disposition spelled out, with no fall-through.
    /// </summary>
    /// <remarks>
    /// Written out rather than lower-casing the enum's name: a value added later would then render
    /// itself, and a word nobody chose is a word nobody checked. The compiler does not catch a missing
    /// arm here, so this is the one place to look when a disposition is added.
    /// </remarks>
    private static string Word(Disposition disposition) => disposition switch
    {
        Disposition.Open => "open",
        Disposition.Answered => "answered",
        Disposition.Withdrawn => "withdrawn",
        _ => "unknown",
    };

    /// <summary>Open blockers first, then open suggestions, then everything settled.</summary>
    private static int Rank(Finding finding) => finding switch
    {
        { Severity: Severity.Blocking, Disposition: Disposition.Open } => 0,
        { Disposition: Disposition.Open } => 1,
        _ => 2,
    };

    private static bool OpenBlocker(Finding finding) =>
        finding is { Severity: Severity.Blocking, Disposition: Disposition.Open };

    private static bool OpenSuggestion(Finding finding) =>
        finding is { Severity: not Severity.Blocking, Disposition: Disposition.Open };

    private static string Rounds(ArtifactFindings artifact) => artifact.Rounds.Count == 1
        ? string.Create(CultureInfo.InvariantCulture, $"round {artifact.Rounds[0].Round}")
        : string.Create(
            CultureInfo.InvariantCulture,
            $"rounds {string.Join(", ", artifact.Rounds.Select(round => round.Round))}");

    /// <summary>
    /// What <c>sdlc round</c> says after writing one.
    /// </summary>
    /// <param name="response">The round as written.</param>
    /// <returns>Where it went, what it stamped, and how many findings it holds.</returns>
    public static string RenderRecorded(RecordRound.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        FindingsRound round = response.Written;
        StringBuilder text = new();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Recorded {Cycle.NameOf(response.Artifact)} round {round.Round} at {response.Path}"));
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  judged      {ContentHash.Abbreviate(round.SubjectSha256 ?? string.Empty)}"));

        if (round.UpstreamSha256 is { } upstream)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  against     {ContentHash.Abbreviate(upstream)}"));
        }

        if (round.EvidenceSha256 is { } evidence)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  evidence    {ContentHash.Abbreviate(evidence)}"));
        }

        // What this round added to the ledger, then what stands: a round holds only what it raised or
        // moved (D-107), so its own counts are not what is open.
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  this round  {round.Findings.Count(finding => finding.Disposition == Disposition.Open)} opened, {round.Findings.Count(finding => finding.Disposition != Disposition.Open)} settled"));
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  standing    {response.Standing.Count(OpenBlocker)} blocking open, {response.Standing.Count(OpenSuggestion)} suggestions open"));

        if (response.Converted > 0)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  converted   {response.Converted} round file{(response.Converted == 1 ? string.Empty : "s")} into the ledger, and removed them"));
        }

        return text.ToString();
    }

    /// <summary>
    /// What <c>sdlc round --changed</c> prints: one heading per line, for the perspective readers' prompt.
    /// </summary>
    /// <param name="response">The sections that moved.</param>
    /// <returns>The headings, or a line saying there is no earlier round.</returns>
    public static string RenderChanged(ChangedSections.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();
        text.AppendLine(response.Since is { } since
            ? string.Create(CultureInfo.InvariantCulture, $"Sections of {Cycle.FileNameOf(response.Artifact)} changed since round {since}:")
            : string.Create(CultureInfo.InvariantCulture, $"{Cycle.FileNameOf(response.Artifact)} has no round yet, so every section is new:"));

        foreach (string heading in response.Changed)
        {
            text.AppendLine("  " + heading);
        }

        if (response.Changed.Count == 0)
        {
            text.AppendLine("  none");
        }

        return text.ToString();
    }
}
