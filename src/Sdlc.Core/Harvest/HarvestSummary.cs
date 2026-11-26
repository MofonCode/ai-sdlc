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
    public sealed record Theme(string Name, int Count, IReadOnlyList<string> Refs);

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

            List<(string Theme, int Count, string? Ref)> counted = [];

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
                    counted.Add((theme ?? NoTheme, 1, null));
                }
                else if (colon > 0)
                {
                    string? by = kind == "Covered" ? By(line[(colon + 1)..]) : null;
                    counted.Add((theme ?? NoTheme, RecordHarvest.Ids(line[..colon]).Count, by));
                }
            }

            answers.Add(new Answer(
                kind,
                counted.Sum(entry => entry.Count),
                [.. counted.GroupBy(entry => entry.Theme, StringComparer.Ordinal).Select(group => new Theme(
                    group.Key,
                    group.Sum(entry => entry.Count),
                    [.. group.Select(entry => entry.Ref).OfType<string>().Where(by => by.Length > 0).Distinct(StringComparer.Ordinal)]))]));
        }

        return new Response(proposals, answers, []);
    }

    /// <summary>The summary, for a person: brief, plain, one line per group.</summary>
    /// <param name="response">The summary.</param>
    /// <returns>The text.</returns>
    public static string Render(Response response)
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

        foreach (Answer answer in response.Answers)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{answer.Kind}: {answer.Total} {(answer.Kind == "Ignored" ? "concern" : "suggestion")}{(answer.Total == 1 ? string.Empty : "s")}"));

            foreach (Theme theme in answer.Themes)
            {
                string refs = theme.Refs.Count > 0 ? " (by " + string.Join(", ", theme.Refs) + ")" : string.Empty;
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {theme.Name} — {theme.Count}{refs}"));
            }
        }

        if (text.Length == 0)
        {
            text.AppendLine("The proposal file proposes nothing and answers nothing yet.");
        }

        text.Append(IdTexts.Render(response.Named));
        return text.ToString();
    }

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
