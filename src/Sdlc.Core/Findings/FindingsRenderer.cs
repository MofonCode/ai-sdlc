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
    /// <param name="blocking">
    /// True for the open blockers alone: what stops the cycle, without the answered history and the
    /// suggestions that make the whole listing of a long cycle run to hundreds of entries.
    /// </param>
    /// <param name="finding">One finding's id, or null for every one.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ReadFindings.Response response, bool brief = false, bool blocking = false, string? finding = null)
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
                    .Where(one => !blocking || OpenBlocker(one))
                    .Where(one => finding is null || string.Equals(one.Id, finding, StringComparison.Ordinal))
                    .OrderBy(finding => Rank(finding))
                    .ThenBy(finding => finding.Id, StringComparer.Ordinal),
            ];

            // Conflicts count as something raised. A round with no findings and a declared conflict is
            // the loop's third ending, not a quiet round -- and reporting "nothing raised" above it was
            // worse than saying nothing, because the one line that mattered was the one being denied.
            // A conflict is not a finding, so asking for one finding is not asking for it; asking for what
            // blocks still is, because a declared conflict stops the loop as surely as a blocker does.
            IReadOnlyList<Conflict> conflicts = finding is null ? artifact.Conflicts : [];

            if (shown.Count == 0 && conflicts.Count == 0)
            {
                output.AppendLine(Indent + (finding is not null
                    ? "    no finding " + finding
                    : blocking ? "    no open blockers" : "    nothing raised"));
                continue;
            }

            foreach (Finding one in shown)
            {
                Entry(output, one, brief);
            }

            // After the findings, because a conflict is rarer and reads as the conclusion of the
            // history rather than one more item in it. Only the latest round's, for the reason
            // StandingConflicts gives: a conflict nobody re-declared was resolved.
            foreach (Conflict conflict in conflicts)
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

        // The gist first when the reader gave one, then the statement in full: this listing is where the
        // whole objection is read, where the panels show only the gist.
        if (finding.Gist is { } gist)
        {
            output.AppendLine(Indent + "        gist    " + gist.ReplaceLineEndings(" "));
        }

        // Every place, where the panels show the first: this is the listing somebody follows into the
        // document, and a place left out is one they find by rereading it.
        if (!string.IsNullOrWhiteSpace(finding.Locus))
        {
            output.AppendLine(Indent + "        where   " + finding.Locus.ReplaceLineEndings(" "));
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

    /// <summary>
    /// What <c>sdlc round collect</c> prints: what the draft carries, and what is left for the session.
    /// </summary>
    /// <remarks>
    /// The findings no reader raised again are the session's to dispose of (D-49), so they come last and
    /// are the one thing the listing asks anybody to do.
    /// </remarks>
    /// <param name="response">The draft.</param>
    /// <returns>The listing.</returns>
    public static string RenderCollected(CollectRound.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();
        int blocking = response.Raised.Count(finding => finding.Severity == Severity.Blocking && finding.Disposition == Disposition.Open);

        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Drafted {response.Path}: {response.Raised.Count} finding{(response.Raised.Count == 1 ? string.Empty : "s")}, {blocking} blocking; {response.Conflicts.Count} conflict{(response.Conflicts.Count == 1 ? string.Empty : "s")}."));

        foreach (Finding finding in response.Raised)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {finding.Id,-28} {(finding.Severity == Severity.Blocking ? "blocking" : "suggestion"),-10} {Shorten(finding.Headline)}"));
        }

        foreach ((string reader, string standing) in response.Restated)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {reader} restates {standing}, carried under that id."));
        }

        foreach (string id in response.Demoted)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {id} was raised as blocking on a rule its perspective does not list, so it is a suggestion."));
        }

        if (response.NotRaised.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("No reader raised these again. Add each to the draft as answered or withdrawn, as the refiner's report says,");
            text.AppendLine("or leave it out and it stays open (D-49):");

            foreach (Finding finding in response.NotRaised)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {finding.Id,-28} {Shorten(finding.Headline)}"));
            }
        }

        text.AppendLine();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Then: sdlc round {response.Cycle.Id} {Cycle.NameOf(response.Artifact)} --from {response.Path}"));

        return text.ToString();
    }

    /// <summary>What <c>sdlc round verdict</c> prints.</summary>
    /// <param name="response">The verdict written.</param>
    /// <returns>One line.</returns>
    public static string RenderVerdict(WriteVerdict.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return string.Create(CultureInfo.InvariantCulture, $"Wrote {response.Path}, outcome {response.Outcome}, as the adjudicator returned it.{Environment.NewLine}");
    }

    private static string Shorten(string text) => text.Length <= 100 ? text : text[..97] + "...";

    /// <summary>
    /// What <c>sdlc round prompt</c> prints: the readers a round needs, and each prompt written.
    /// </summary>
    /// <remarks>
    /// Each answer's path beside its prompt, because that is the one convention the launch line and
    /// <c>round collect</c> share (D-117). Nothing about how to start a reader: that is a host's spelling,
    /// and it lives in the skills (D-118).
    /// </remarks>
    /// <param name="response">What was written.</param>
    /// <returns>The listing.</returns>
    public static string RenderPrompts(ReaderPrompts.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"A {Cycle.NameOf(response.Artifact)} round of {response.Cycle.Id} runs {response.Readers.Count} reader{(response.Readers.Count == 1 ? string.Empty : "s")} blind, then {ReaderAnswers.Reconcile}:"));

        foreach (string reader in response.Readers)
        {
            text.AppendLine("  " + reader);
        }

        foreach (string missing in response.Unwritten)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {missing} -- active, and not written yet, so it has nothing to run. A gap, not a pass."));
        }

        if (response.Prompts.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Written:");

            foreach (ReaderPrompts.Written prompt in response.Prompts)
            {
                text.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {prompt.Path}  ({prompt.Bytes:N0} bytes) -> answer to {ReaderAnswers.AnswerPath(response.Directory, prompt.Reader)}"));
            }
        }

        return text.ToString();
    }
}
