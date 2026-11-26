using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Reports;

namespace Sdlc.Core.Harvest;

/// <summary>
/// What one harvest proposal file asks of a person, in a few lines: the proposals they are approving, each answer's
/// count by theme, and every id the file names with what it says. Reads; writes nothing.
/// </summary>
/// <remarks>
/// <para><strong>The grouping is the agent's; the counts and the texts are the binary's.</strong> The sitting chooses
/// the <c>### &lt;theme&gt;</c> each line stands under, so a person reads a handful of groups rather than a line per
/// suggestion; this counts what each group holds and resolves each id, so a group cannot claim more than it holds and
/// nobody retypes a rule.</para>
/// <para><strong>Each proposal is numbered, in the file's order</strong>, so a person can answer one by its number --
/// <em>accept all but 11</em> -- rather than retyping its heading. The number is the summary's, never written into
/// the file: the headings keep the shape drafting and recording read, and a rerun renumbers after an edit.</para>
/// </remarks>
public static class HarvestSummary
{
    /// <summary>What a line under no theme is counted as.</summary>
    public const string NoTheme = "(no theme)";

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddHarvestSummaryRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks for one proposal file's summary.</summary>
    /// <param name="From">The proposal file.</param>
    public sealed record Query(string From) : IRequest<Response>;

    /// <summary>The summary.</summary>
    /// <param name="Proposals">Every proposal heading, in order, with whether it is marked.</param>
    /// <param name="Answers">Declined, Covered and Ignored, each that the file holds, in that order.</param>
    /// <param name="Named">Every rule, decision and successor the file names, with what it says.</param>
    public sealed record Response(IReadOnlyList<Proposal> Proposals, IReadOnlyList<Answer> Answers, IReadOnlyList<IdTexts.Named> Named);

    /// <summary>One proposal heading.</summary>
    /// <param name="Heading">The heading after its mark.</param>
    /// <param name="Marked">Whether the person marked it <c>[x]</c> or <c>[h]</c>.</param>
    /// <param name="Held">Whether the mark is <c>[h]</c>: a successor accepted and held (D-220).</param>
    public sealed record Proposal(string Heading, bool Marked, bool Held = false);

    /// <summary>One kind of answer and its themes.</summary>
    /// <param name="Kind">The section: <c>Declined</c>, <c>Covered</c> or <c>Ignored</c>.</param>
    /// <param name="Total">Suggestion ids for Declined and Covered; concerns for Ignored.</param>
    /// <param name="Themes">Each theme in the order it first appears.</param>
    public sealed record Answer(string Kind, int Total, IReadOnlyList<Theme> Themes);

    /// <summary>One theme's count.</summary>
    /// <param name="Name">The theme, or <see cref="NoTheme"/>.</param>
    /// <param name="Count">What it holds: suggestion ids, or concerns.</param>
    /// <param name="Refs">For Covered, what its lines rely on, each once; empty otherwise.</param>
    /// <param name="Whole">
    /// For Covered, the abandoned cycles whose every suggestion it answers at once, <c>&lt;cycle&gt;/*</c>, counted
    /// apart from <paramref name="Count"/> because the file does not say how many suggestions each holds (D-225).
    /// </param>
    public sealed record Theme(string Name, int Count, IReadOnlyList<string> Refs, int Whole = 0);

    /// <summary>
    /// How many themes of one answer a summary shows before it folds the rest into one line: past this a person is
    /// reading a list again, which is what the themes were for (D-225).
    /// </summary>
    public const int Shown = 8;

    /// <summary>Reads a proposal file without resolving its ids.</summary>
    /// <param name="markdown">The proposal file.</param>
    /// <returns>Its proposals and answers; <see cref="Response.Named"/> empty.</returns>
    public static Response Read(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        List<Proposal> proposals = [];

        foreach (string raw in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            string line = raw.Trim();

            if (line.StartsWith("### [ ]", StringComparison.Ordinal)
                || line.StartsWith("### [x]", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("### [h]", StringComparison.OrdinalIgnoreCase))
            {
                proposals.Add(new Proposal(line[7..].Trim(), line[5] is 'x' or 'X' or 'h' or 'H', line[5] is 'h' or 'H'));
            }
        }

        IReadOnlyDictionary<string, IReadOnlyList<string>> sections = ArtifactSections.Read(markdown);
        List<Answer> answers = [];

        foreach (string kind in (string[])["Declined", "Covered", "Ignored"])
        {
            if (!sections.ContainsKey(kind))
            {
                continue;
            }

            List<(string Theme, int Count, string? Ref, int Whole)> counted = [];

            foreach ((string? theme, string raw) in RecordHarvest.Themed(sections, kind))
            {
                string line = raw.Trim();

                if (!line.StartsWith("- ", StringComparison.Ordinal))
                {
                    continue;
                }

                line = line[2..].Trim();
                int colon = line.IndexOf(':', StringComparison.Ordinal);

                if (kind == "Ignored")
                {
                    counted.Add((theme ?? NoTheme, 1, null, 0));
                }
                else if (colon > 0)
                {
                    string? by = kind == "Covered" ? By(line[(colon + 1)..]) : null;
                    List<string> ids = RecordHarvest.Ids(line[..colon]);
                    int whole = ids.Count(id => id.EndsWith(RecordHarvest.WholeCycle, StringComparison.Ordinal));
                    counted.Add((theme ?? NoTheme, ids.Count - whole, by, whole));
                }
            }

            answers.Add(new Answer(
                kind,
                counted.Sum(entry => entry.Count),
                [.. counted.GroupBy(entry => entry.Theme, StringComparer.Ordinal).Select(group => new Theme(
                    group.Key,
                    group.Sum(entry => entry.Count),
                    [.. group.Select(entry => entry.Ref).OfType<string>().Where(by => by.Length > 0).Distinct(StringComparer.Ordinal)],
                    group.Sum(entry => entry.Whole)))]));
        }

        return new Response(proposals, answers, []);
    }

    /// <summary>
    /// The summary, for a person: brief, plain, one line per group, and no more than <see cref="Shown"/> groups to an
    /// answer unless <paramref name="all"/> asks for every one.
    /// </summary>
    /// <param name="response">The summary.</param>
    /// <param name="all">Whether to show every theme rather than fold the smallest.</param>
    /// <returns>The text.</returns>
    public static string Render(Response response, bool all = false)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();

        if (response.Proposals.Count > 0)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Proposals: {response.Proposals.Count}, {response.Proposals.Count(proposal => proposal.Marked)} marked -- what you approve is each [x], and each [h] a successor you want and hold"));

            for (int index = 0; index < response.Proposals.Count; index++)
            {
                Proposal proposal = response.Proposals[index];
                text.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {index + 1}. {(proposal.Held ? "[h]" : proposal.Marked ? "[x]" : "[ ]")} {proposal.Heading}"));
            }
        }

        bool folded = false;

        foreach (Answer answer in response.Answers)
        {
            int whole = answer.Themes.Sum(theme => theme.Whole);
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{answer.Kind}: {answer.Total} {(answer.Kind == "Ignored" ? "concern" : "suggestion")}{(answer.Total == 1 ? string.Empty : "s")}{Whole(whole)}"));

            // Past the limit the largest themes are shown and the rest counted on one line: a sitting of thousands of
            // suggestions came back as hundreds of themes, which is a list again (D-225).
            IReadOnlyList<Theme> themes = all || answer.Themes.Count <= Shown
                ? answer.Themes
                : [.. answer.Themes.OrderByDescending(theme => theme.Count + theme.Whole).Take(Shown)];

            foreach (Theme theme in themes)
            {
                string refs = theme.Refs.Count > 0 ? " (by " + string.Join(", ", theme.Refs.Take(all ? int.MaxValue : Shown)) + (!all && theme.Refs.Count > Shown ? ", ..." : string.Empty) + ")" : string.Empty;
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {theme.Name} — {theme.Count}{Whole(theme.Whole)}{refs}"));
            }

            if (themes.Count < answer.Themes.Count)
            {
                List<Theme> rest = [.. answer.Themes.Except(themes)];
                folded = true;
                text.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  ...and {rest.Count} smaller themes holding {rest.Sum(theme => theme.Count)}{Whole(rest.Sum(theme => theme.Whole))}"));
            }
        }

        if (folded)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"More than {Shown} themes to an answer is a list again: 'sdlc harvest summary <file> --all' shows every one."));
        }

        if (text.Length == 0)
        {
            text.AppendLine("The proposal file proposes nothing and answers nothing yet.");
        }

        text.Append(IdTexts.Render(response.Named));
        return text.ToString();
    }

    private static string Whole(int cycles) => cycles switch
    {
        0 => string.Empty,
        1 => ", and every suggestion of 1 abandoned cycle",
        _ => string.Create(CultureInfo.InvariantCulture, $", and every suggestion of {cycles} abandoned cycles"),
    };

    // What a covered line relies on: the text between its colon and its dash.
    private static string By(string rest)
    {
        int dash = rest.IndexOf(" — ", StringComparison.Ordinal);
        dash = dash < 0 ? rest.IndexOf(" -- ", StringComparison.Ordinal) : dash;

        return dash > 0 ? rest[..dash].Trim().Trim('`').Trim() : string.Empty;
    }

    private sealed class Handler(Mediator mediator) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!File.Exists(request.From))
            {
                return Result.Fail<Response>("There is no proposal file at " + request.From + ". A harvest aspect's sitting writes one.");
            }

            string markdown = await File.ReadAllTextAsync(request.From, cancellationToken).ConfigureAwait(false);
            Response read = Read(markdown);
            IReadOnlyList<string> ids = IdTexts.Find(markdown);

            if (ids.Count == 0)
            {
                return Result.Ok(read);
            }

            Result<IdTexts.Response> named = await mediator
                .Send<IdTexts.Query, IdTexts.Response>(new IdTexts.Query(ids), cancellationToken)
                .ConfigureAwait(false);

            return named.IsFailed ? named.ToResult<Response>() : Result.Ok(read with { Named = named.Value.Named });
        }
    }
}
