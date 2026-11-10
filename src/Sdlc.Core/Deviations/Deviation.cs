using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;

namespace Sdlc.Core.Deviations;

/// <summary>What a deviation bends, decided by the approved text it quotes.</summary>
public enum DeviationKind
{
    /// <summary>The plan's files, shape or steps.</summary>
    Plan,

    /// <summary>The planned proof for a criterion, or a test it names.</summary>
    Proof,

    /// <summary>A spec criterion, narrowed. What it drops becomes a successor.</summary>
    Criterion,

    /// <summary>A recorded decision, departed from for this cycle.</summary>
    Decision,
}

/// <summary>
/// One place a build or a remediation bent the approved documents, recorded so a person can sign it.
/// </summary>
/// <remarks>
/// <para><strong>A deviation doesn't excuse a blocker. It replaces the approved text the blocker was measured
/// against</strong>, and a person signs the replacement, so nothing is waived (D-27). It is defined by what it
/// quotes: nothing quoted means nothing deviated from, which is what keeps a choice the plan left open off the
/// list.</para>
/// </remarks>
/// <param name="Id">Its id, <c>DEV_nn</c>, unique in the cycle and never reused.</param>
/// <param name="Gist">Its heading after the id: one line a person reads first.</param>
/// <param name="Kind">What it bends.</param>
/// <param name="Document">The approved document it quotes, as a file name.</param>
/// <param name="Section">The section of that document, as the deviation names it.</param>
/// <param name="Quote">The approved text it departs from, word for word.</param>
/// <param name="Blocker">What stopped the build following the approved text, with its evidence or the finding it answers.</param>
/// <param name="Instead">What was done instead.</param>
/// <param name="Why">Why that and not something inside the approved text.</param>
/// <param name="Files">The files it adds to what the plan declared, when it adds any.</param>
/// <param name="Successor">For a criterion deviation, the successor the dropped part becomes: its slug, then what it is.</param>
/// <param name="Replaces">The earlier deviation, declined, that this one is a second attempt at.</param>
/// <param name="Sha256">The hash of its text, which a person's answer is held to.</param>
public sealed record Deviation(
    string Id,
    string Gist,
    DeviationKind Kind,
    string Document,
    string Section,
    string Quote,
    string Blocker,
    string Instead,
    string Why,
    IReadOnlyList<string> Files,
    string? Successor,
    string? Replaces,
    string Sha256)
{
    /// <summary>The successor's slug, the first word of <see cref="Successor"/>, when there is one.</summary>
    public string? SuccessorSlug => Successor is { } text && text.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries) is [{ } slug, ..]
        ? slug.TrimEnd(':', ',', '.')
        : null;

    /// <summary>The kind as a person reads it.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Its lower-case name, as the file writes it.</returns>
    public static string NameOf(DeviationKind kind) => kind switch
    {
        DeviationKind.Plan => "plan",
        DeviationKind.Proof => "proof",
        DeviationKind.Criterion => "criterion",
        DeviationKind.Decision => "decision",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The one document a kind may quote.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Its file name.</returns>
    public static string DocumentOf(DeviationKind kind) => kind switch
    {
        DeviationKind.Plan or DeviationKind.Proof => Cycle.FileNameOf(ArtifactKind.Plan),
        DeviationKind.Criterion => Cycle.FileNameOf(ArtifactKind.Spec),
        DeviationKind.Decision => DecisionParser.FileName,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

/// <summary>
/// Reads a cycle's <c>deviations.md</c>: one <c>## DEV_nn — gist</c> section per deviation, append-only.
/// </summary>
/// <remarks>
/// <para><strong>Written by build and remediate, never by verify</strong>, and not a protected file: a person's
/// signature is the deviations entry in <c>approval.md</c>, which holds each deviation's hash, so an entry
/// edited after it was answered is unanswered again.</para>
/// <para>Every problem in the file is reported at once, as the other parsers here do, so one pass fixes all
/// of them.</para>
/// </remarks>
public static partial class DeviationsFile
{
    /// <summary>The file's name in the cycle directory.</summary>
    public const string FileName = "deviations.md";

    private static readonly string[] _fields = ["Kind", "Departs from", "Blocker", "Deviation", "Why", "Files", "Successor", "Replaces"];

    [GeneratedRegex(@"^##\s+(?<id>DEV_[0-9]{2,4})\s*(?:—|–|-|:)\s*(?<gist>.+?)\s*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Heading();

    [GeneratedRegex(@"^##\s+DEV", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Attempt();

    [GeneratedRegex(@"`(?<path>[^`]+)`", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Backticked();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Space();

    [GeneratedRegex(@"\b(?<id>(?:CON|LOC|PRO|DEC)_[0-9]{2,4}(?:\.[0-9]+)?)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RuleId();

    /// <summary>
    /// The rules a deviation's blocker names: a constraint at any tier, or a decision. Empty when the build hit
    /// the obstacle before any reader raised it and it cites none.
    /// </summary>
    /// <param name="deviation">The deviation.</param>
    /// <returns>Each id once, in the order written.</returns>
    public static IReadOnlyList<string> RulesOf(Deviation deviation)
    {
        ArgumentNullException.ThrowIfNull(deviation);

        return [.. RuleId().Matches(deviation.Blocker).Select(found => found.Groups["id"].Value).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The path of a cycle's deviations.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The path.</returns>
    public static string PathOf(Cycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        return Path.Combine(cycle.Directory, FileName);
    }

    /// <summary>Text with every run of white space made one space, so a quote is matched by its words.</summary>
    /// <param name="text">The text.</param>
    /// <returns>It, normalised and trimmed.</returns>
    public static string Normalise(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Space().Replace(text, " ").Trim();
    }

    /// <summary>Reads the deviations a file records.</summary>
    /// <param name="text">The file's contents.</param>
    /// <param name="source">What to call it in an error message.</param>
    /// <returns>Every deviation in the order written, or a failure listing every problem.</returns>
    public static Result<IReadOnlyList<Deviation>> Parse(string text, string source)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        List<Deviation> found = [];
        List<IError> errors = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach ((string heading, List<string> body) in Sections(text))
        {
            Match match = Heading().Match(heading);

            if (!match.Success)
            {
                errors.Add(new Error(string.Create(CultureInfo.InvariantCulture, $"{source}: '{heading}' is not a deviation heading. Write it as '## DEV_nn — what was done, in one line'.")));
                continue;
            }

            string id = match.Groups["id"].Value;
            string position = source + ": " + id;

            if (!seen.Add(id))
            {
                errors.Add(new Error($"{position} is used twice. An id is never reused; a second attempt is a new deviation with 'Replaces: {id}'."));
                continue;
            }

            Result<Deviation> read = Read(id, match.Groups["gist"].Value, heading, body, position);

            if (read.IsFailed)
            {
                errors.AddRange(read.Errors);
                continue;
            }

            found.Add(read.Value);
        }

        return errors.Count > 0 ? Result.Fail<IReadOnlyList<Deviation>>(errors) : Result.Ok<IReadOnlyList<Deviation>>(found);
    }

    private static Result<Deviation> Read(string id, string gist, string heading, List<string> body, string position)
    {
        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);
        List<string> quote = [];
        List<string> problems = [];
        string? field = null;

        foreach (string line in body)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith('>'))
            {
                quote.Add(trimmed[1..].Trim());
                field = null;
                continue;
            }

            if (_fields.FirstOrDefault(name => trimmed.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase)) is { } name)
            {
                field = name;
                fields[name] = trimmed[(name.Length + 1)..].Trim();
                continue;
            }

            if (trimmed.Length == 0)
            {
                field = null;
                continue;
            }

            if (field is not null)
            {
                fields[field] = (fields[field] + " " + trimmed).Trim();
            }
        }

        string Field(string name) => fields.TryGetValue(name, out string? value) ? value : string.Empty;

        DeviationKind kind = default;
        if (Field("Kind") is { Length: > 0 } written)
        {
            if (!TryKind(written, out kind))
            {
                problems.Add($"has kind '{written}'. Expected plan, proof, criterion or decision.");
            }
        }
        else
        {
            problems.Add("has no 'Kind:' line.");
        }

        string document = string.Empty;
        string section = string.Empty;
        if (Field("Departs from") is { Length: > 0 } departs)
        {
            string[] parts = departs.Split(',', 2, StringSplitOptions.TrimEntries);
            document = parts[0].Trim('`');
            section = parts.Length > 1 ? parts[1] : string.Empty;

            if (problems.Count == 0 && !string.Equals(document, Deviation.DocumentOf(kind), StringComparison.Ordinal))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"is a {Deviation.NameOf(kind)} deviation quoting {document}. A {Deviation.NameOf(kind)} deviation quotes {Deviation.DocumentOf(kind)}; the intent is never deviated from."));
            }
        }
        else
        {
            problems.Add("has no 'Departs from:' line naming the document and its section.");
        }

        if (quote.Count == 0)
        {
            problems.Add("quotes nothing. A deviation quotes the approved text it departs from on '> ' lines; a choice the plan left open is not a deviation.");
        }

        foreach (string required in (string[])["Blocker", "Deviation", "Why"])
        {
            if (Field(required).Length == 0)
            {
                problems.Add($"has no '{required}:' line.");
            }
        }

        string? successor = Field("Successor") is { Length: > 0 } next ? next : null;
        if (problems.Count == 0 && kind == DeviationKind.Criterion && successor is null)
        {
            problems.Add("narrows a criterion and names no successor. Add 'Successor: <slug> — what the dropped part makes possible'.");
        }

        string? replaces = Field("Replaces") is { Length: > 0 } earlier ? earlier.Trim() : null;

        if (problems.Count > 0)
        {
            return Result.Fail<Deviation>(problems.Select(problem => (IError)new Error(position + " " + problem)));
        }

        IReadOnlyList<string> files = [.. Backticked().Matches(Field("Files")).Select(found => found.Groups["path"].Value.Trim())];
        string sha = ContentHash.Of(string.Join('\n', [heading, .. body]).Trim());

        return Result.Ok(new Deviation(
            id,
            gist,
            kind,
            document,
            section,
            string.Join('\n', quote),
            Field("Blocker"),
            Field("Deviation"),
            Field("Why"),
            files,
            successor,
            replaces,
            sha));
    }

    private static bool TryKind(string text, out DeviationKind kind)
    {
        foreach (DeviationKind candidate in Enum.GetValues<DeviationKind>())
        {
            if (string.Equals(text.Trim(), Deviation.NameOf(candidate), StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    // Each '## ' section that opens like a deviation, with the lines beneath it. Text before the first one --
    // a title, a sentence about the file -- is the file's own and is not read.
    private static IEnumerable<(string Heading, List<string> Body)> Sections(string text)
    {
        string? heading = null;
        List<string> body = [];

        foreach (string raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = raw.TrimEnd();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (heading is not null)
                {
                    yield return (heading, body);
                }

                heading = Attempt().IsMatch(line) || heading is not null ? line : null;
                body = [];
                continue;
            }

            if (heading is not null)
            {
                body.Add(line);
            }
        }

        if (heading is not null)
        {
            yield return (heading, body);
        }
    }

    /// <summary>A deviation's text as the readers and the draft show it, for one line.</summary>
    /// <param name="deviation">The deviation.</param>
    /// <returns>Its id, gist and kind.</returns>
    public static string Line(Deviation deviation)
    {
        ArgumentNullException.ThrowIfNull(deviation);

        return string.Create(CultureInfo.InvariantCulture, $"{deviation.Id} — {deviation.Gist} ({Deviation.NameOf(deviation.Kind)})");
    }

    /// <summary>Whether a document holds a quote, word for word once white space is normalised.</summary>
    /// <param name="document">The document's text.</param>
    /// <param name="quote">The quote.</param>
    /// <returns>True when it does.</returns>
    public static bool Holds(string document, string quote)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(quote);

        StringBuilder flat = new();
        foreach (string line in quote.Split('\n'))
        {
            flat.Append(line).Append(' ');
        }

        return Normalise(document).Contains(Normalise(flat.ToString()), StringComparison.Ordinal);
    }
}
