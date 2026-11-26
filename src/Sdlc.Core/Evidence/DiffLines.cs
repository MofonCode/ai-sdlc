using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sdlc.Core.Evidence;

/// <summary>
/// A diff's lines by the number a verdict cites them by: the numbered rendering every verify reader is handed, and
/// the citations whose quote is on other lines than the ones they name (D-222).
/// </summary>
/// <remarks>
/// <para><strong>A verdict cites a diff line as <c>+41</c>, and nothing in a unified diff says which line is
/// 41.</strong> The adjudicator counted down from the hunk header, and slipped: in one governed solution's 23 cycles
/// the readers' verify findings were, again and again, a line reference off by one beside a quote that was right.
/// Each cost a reader's finding and the session a disposition, for a number a lookup decides.</para>
/// <para><strong>So the readers are handed the numbers, and the slips that remain are listed.</strong> The patch on
/// disk is unchanged; what a verify round reads carries each hunk line's number in a margin, the new file's for an
/// added or unchanged line and the old file's for a removed one, so a citation is copied rather than counted. A
/// citation whose quote is in the diff on other lines than it names is a slip, reported by <c>sdlc evidence
/// check</c> and handed to the rewrite that corrects it, never refused: which file a sentence means is read from
/// prose, and a refusal resting on that reading would cost a rerun of the largest prompt the framework sends.</para>
/// </remarks>
public static partial class DiffLines
{
    /// <summary>One citation whose quote is on other lines than it names.</summary>
    /// <param name="Criterion">The criterion entry it is in, or null when it is outside one.</param>
    /// <param name="Cited">The lines as the verdict cites them, sign included: <c>+38</c>, <c>+57–60</c>.</param>
    /// <param name="Quote">The quote, as the verdict gives it.</param>
    /// <param name="Found">Where the quote is: the file and the lines, sign included.</param>
    public sealed record Slip(int? Criterion, string Cited, string Quote, string Found)
    {
        /// <summary>The slip as one line a person reads.</summary>
        /// <returns>The line.</returns>
        public override string ToString() => string.Create(
            CultureInfo.InvariantCulture,
            $"{(Criterion is { } number ? "criterion " + number.ToString(CultureInfo.InvariantCulture) + ": " : string.Empty)}{Cited} quotes `{Quote}`, which is at {Found}");
    }

    // One hunk line, by the side it is numbered on.
    private sealed record Line(string File, char Side, int Number, string Text);

    /// <summary>The patch with each hunk line's number in a margin, the rest as it stands.</summary>
    /// <param name="patch">The patch.</param>
    /// <returns>The numbered rendering.</returns>
    public static string Numbered(string patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        StringBuilder text = new();

        foreach ((string raw, char sign, int number) in Walk(patch))
        {
            if (sign == '\0')
            {
                text.Append(raw).Append('\n');
                continue;
            }

            text.Append(Margin(sign, number)).Append(raw.AsSpan(1)).Append('\n');
        }

        return text.ToString().TrimEnd('\n') + "\n";
    }

    // Every line of the patch, and for a hunk line its sign and its number: the new file's for an added or unchanged
    // line, the old file's for a removed one. Every other line has the sign '\0'. A hunk ends when its header's
    // counts are spent, so a removed line reading "--- " is still a line and not a file's header.
    private static IEnumerable<(string Raw, char Sign, int Number)> Walk(string patch)
    {
        int oldNext = 0;
        int newNext = 0;
        int oldLeft = 0;
        int newLeft = 0;

        foreach (string raw in patch.ReplaceLineEndings("\n").Split('\n'))
        {
            bool inHunk = oldLeft > 0 || newLeft > 0;

            if (inHunk && raw.Length > 0 && raw[0] is '+' or '-' or ' ')
            {
                switch (raw[0])
                {
                    case '+':
                        newLeft--;
                        yield return (raw, '+', newNext++);
                        break;
                    case '-':
                        oldLeft--;
                        yield return (raw, '-', oldNext++);
                        break;
                    default:
                        oldLeft--;
                        newLeft--;
                        oldNext++;
                        yield return (raw, ' ', newNext++);
                        break;
                }

                continue;
            }

            if (!inHunk && HunkHeader().Match(raw) is { Success: true } header)
            {
                oldNext = Number(header, "old");
                newNext = Number(header, "new");
                oldLeft = header.Groups["oldCount"].Success ? Number(header, "oldCount") : 1;
                newLeft = header.Groups["newCount"].Success ? Number(header, "newCount") : 1;
            }

            yield return (raw, '\0', 0);
        }

        static int Number(Match match, string group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>What a reader is told the margin means, said once above the numbered patch.</summary>
    public const string Legend =
        "Each hunk line is numbered in the margin, before the `|`: `+41` is line 41 of the new file, added; `-12` is line 12 "
        + "of the old file, removed; ` 29` is line 29 of the new file, unchanged. Cite a line by its margin, and quote what "
        + "follows the `|`.";

    /// <summary>Every citation in a verify document whose quote is in the diff, on other lines than it names.</summary>
    /// <remarks>
    /// A citation is a sign and a line or a range -- <c>+41</c>, <c>+57–60</c>, <c>-12</c> -- followed on its line by a
    /// quote in backticks, or by a colon and a fenced block whose first line is the quote. It is read against the
    /// last file of the diff its entry named in backticks, or every file when the entry has named none. A quote
    /// elided with <c>...</c> is found by its longest piece, and a piece under six characters decides nothing. A quote
    /// found nowhere in the diff is not a slip: that a verdict quotes what is not there is <c>CON_22</c>'s, and
    /// <see cref="CheckEvidence"/> already lists it.
    /// </remarks>
    /// <param name="verify">The verify document.</param>
    /// <param name="patch">The diff it cites.</param>
    /// <returns>Each slip, in the order the document gives them.</returns>
    public static IReadOnlyList<Slip> Slips(string verify, string patch)
    {
        ArgumentNullException.ThrowIfNull(verify);
        ArgumentNullException.ThrowIfNull(patch);

        List<Line> lines = Parse(patch);

        if (lines.Count == 0)
        {
            return [];
        }

        List<string> files = [.. lines.Select(line => line.File).Distinct(StringComparer.Ordinal)];
        List<Slip> slips = [];
        string[] body = verify.ReplaceLineEndings("\n").Split('\n');
        int? criterion = null;
        string? file = null;
        bool inFrontmatter = body.Length > 0 && body[0] == "---";

        for (int index = inFrontmatter ? 1 : 0; index < body.Length; index++)
        {
            string text = body[index];

            if (inFrontmatter)
            {
                inFrontmatter = text != "---";
                continue;
            }

            if (text.StartsWith("## ", StringComparison.Ordinal))
            {
                criterion = null;
                file = null;
                continue;
            }

            if (EntryStart().Match(text) is { Success: true } entry)
            {
                criterion = int.Parse(entry.Groups["number"].Value, CultureInfo.InvariantCulture);
                file = null;
            }

            // A file named on this line, before its citations, is the one they read against.
            string? named = null;

            foreach (Match span in Backticked().Matches(text))
            {
                if (FileOf(span.Groups["text"].Value, files) is { } path)
                {
                    named = path;
                }
            }

            string bare = Backticked().Replace(text, match => new string(' ', match.Length));

            foreach (Match cite in Citation().Matches(bare))
            {
                string? quote = QuoteAfter(text, cite.Index + cite.Length, body, index);

                if (quote is null || Piece(quote) is not { } piece)
                {
                    continue;
                }

                char side = cite.Groups["sign"].Value[0];
                int from = int.Parse(cite.Groups["from"].Value, CultureInfo.InvariantCulture);
                int to = cite.Groups["to"].Success ? int.Parse(cite.Groups["to"].Value, CultureInfo.InvariantCulture) : from;

                if (to < from)
                {
                    continue;
                }

                string? scope = FileBefore(text, cite.Index, files) ?? file ?? named;
                List<Line> candidates = [.. lines.Where(line => line.Side == side && (scope is null || line.File == scope))];

                if (Holds(candidates, from, to, piece))
                {
                    continue;
                }

                List<Line> found = [.. lines.Where(line => line.Side == side && Flat(line.Text).Contains(piece, StringComparison.Ordinal))];

                if (found.Count == 0)
                {
                    continue;
                }

                Line first = found.OrderBy(line => line.File == scope ? 0 : 1).ThenBy(line => Math.Abs(line.Number - from)).First();
                slips.Add(new Slip(
                    criterion,
                    cite.Value.Trim(),
                    quote,
                    string.Create(CultureInfo.InvariantCulture, $"{side}{first.Number} in {first.File}")));
            }

            file = named ?? file;
        }

        return slips;
    }

    private static string Margin(char sign, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"{(sign == ' ' ? " " : sign.ToString())}{number}").PadRight(7) + "| ";

    private static List<Line> Parse(string patch)
    {
        List<Line> lines = [];
        string? oldFile = null;
        string? newFile = null;

        foreach ((string raw, char sign, int number) in Walk(patch))
        {
            switch (sign)
            {
                case '\0' when raw.StartsWith("--- ", StringComparison.Ordinal):
                    oldFile = PathOf(raw[4..]);
                    break;
                case '\0' when raw.StartsWith("+++ ", StringComparison.Ordinal):
                    newFile = PathOf(raw[4..]);
                    break;
                case '+' when newFile is not null:
                    lines.Add(new Line(newFile, '+', number, raw[1..]));
                    break;
                case '-' when (newFile ?? oldFile) is { } file:
                    lines.Add(new Line(file, '-', number, raw[1..]));
                    break;
                case ' ' when newFile is not null:
                    // An unchanged line is in both files at once; a verdict citing it with either sign means it.
                    lines.Add(new Line(newFile, '+', number, raw[1..]));
                    lines.Add(new Line(newFile, '-', number, raw[1..]));
                    break;
                default:
                    break;
            }
        }

        return lines;
    }

    private static string? PathOf(string header)
    {
        string path = header.Trim();

        if (path == "/dev/null")
        {
            return null;
        }

        return path.StartsWith("a/", StringComparison.Ordinal) || path.StartsWith("b/", StringComparison.Ordinal) ? path[2..] : path;
    }

    private static string? FileOf(string span, List<string> files)
    {
        string name = span.Trim().Replace('\\', '/');

        return files.Find(path => path == name)
            ?? (name.Contains('.', StringComparison.Ordinal) ? files.Find(path => path.EndsWith("/" + name, StringComparison.Ordinal)) : null);
    }

    // The last file named in backticks on the line before the citation.
    private static string? FileBefore(string text, int position, List<string> files)
    {
        string? last = null;

        foreach (Match span in Backticked().Matches(text[..Math.Min(position, text.Length)]))
        {
            last = FileOf(span.Groups["text"].Value, files) ?? last;
        }

        return last;
    }

    private static string? QuoteAfter(string text, int position, string[] body, int index)
    {
        string rest = text[position..];
        Match next = Backticked().Match(rest);

        // A citation the next citation on the line comes before is answered by that one's quote, not this one's.
        Match following = Citation().Match(Backticked().Replace(rest, match => new string(' ', match.Length)));

        if (next.Success && (!following.Success || next.Index < following.Index))
        {
            return next.Groups["text"].Value.Trim();
        }

        if (!rest.TrimEnd().EndsWith(':'))
        {
            return null;
        }

        for (int line = index + 1; line < body.Length; line++)
        {
            string candidate = body[line].Trim();

            if (candidate.Length == 0)
            {
                continue;
            }

            if (!candidate.StartsWith("```", StringComparison.Ordinal))
            {
                return null;
            }

            for (int inside = line + 1; inside < body.Length; inside++)
            {
                string code = body[inside].Trim();

                if (code.StartsWith("```", StringComparison.Ordinal))
                {
                    return null;
                }

                if (code.Length > 0)
                {
                    return code;
                }
            }

            return null;
        }

        return null;
    }

    private static string? Piece(string quote)
    {
        string? longest = quote
            .Split(["...", "…"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Flat)
            .OrderByDescending(piece => piece.Length)
            .FirstOrDefault();

        return longest is { Length: >= 6 } ? longest : null;
    }

    private static bool Holds(List<Line> candidates, int from, int to, string piece)
    {
        foreach (IGrouping<string, Line> file in candidates.GroupBy(line => line.File))
        {
            string range = string.Join(" ", file.Where(line => line.Number >= from && line.Number <= to).OrderBy(line => line.Number).Select(line => Flat(line.Text)));

            if (range.Contains(piece, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Flat(string text) => Space().Replace(text, " ").Trim();

    [GeneratedRegex(@"^@@ -(?<old>[0-9]+)(?:,(?<oldCount>[0-9]+))? \+(?<new>[0-9]+)(?:,(?<newCount>[0-9]+))? @@", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex HunkHeader();

    [GeneratedRegex(@"`(?<text>[^`\n]+)`", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Backticked();

    // A sign and a line or a range, standing alone: not part of a word, a number, a hunk header's "-28,14", or "C++".
    [GeneratedRegex(@"(?<![\w+\-.,/])(?<sign>[+\-])(?<from>[0-9]{1,6})(?:\s*[–-]\s*(?<to>[0-9]{1,6}))?(?![\w.,]?[0-9])(?![\w])", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Citation();

    [GeneratedRegex(@"^\s*(?<number>[0-9]{1,3})[.)]\s+\S", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex EntryStart();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Space();
}
