using System.Globalization;
using System.Text.RegularExpressions;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Findings;

/// <summary>
/// Writes <c>verify.md</c> from the adjudicator's answer, byte for byte (D-117, D-109).
/// </summary>
/// <remarks>
/// The session recorded the verdict by copying it out of the reader's output, and a session that copies
/// can also repair — which is this session writing the verdict after all. An answer that is not a verify
/// document is refused rather than tidied, so the only way past it is a new adjudication.
/// </remarks>
public static partial class WriteVerdict
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddWriteVerdictRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>The verdict written.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Path">Where it was written.</param>
    /// <param name="Outcome">The outcome it declares.</param>
    public sealed record Response(Cycle Cycle, string Path, string Outcome);

    /// <summary>Writes the verdict an adjudicator returned.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="From">The adjudicator's answer: the host's result object, or its plain text.</param>
    public sealed record Command(CycleId Id, string From) : IRequest<Response>;

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);

            if (!File.Exists(request.From))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no adjudicator's answer at {request.From}."));
            }

            Result<ReaderAnswer> answer = ReaderAnswers.Parse(
                ReaderAnswers.Adjudicate,
                await File.ReadAllTextAsync(request.From, cancellationToken).ConfigureAwait(false),
                null,
                request.From);

            if (answer.IsFailed)
            {
                return answer.ToResult<Response>();
            }

            string text = Unfenced(answer.Value.Text);
            const string Again = " Start a new adjudication; do not repair this one, because the repair is this session writing the verdict.";

            if (!text.StartsWith("---", StringComparison.Ordinal))
            {
                return Result.Fail<Response>("The adjudicator's answer does not open with a verify document's frontmatter: it has something before it, or is not a verify document." + Again);
            }

            Result<FrontmatterDocument> split = Frontmatter.Read(text, "verify.md");

            if (split.IsFailed || Frontmatter.Key(split.Value.Yaml, "outcome") is not { Length: > 0 } outcome)
            {
                return Result.Fail<Response>("The adjudicator's answer has no `outcome` in its frontmatter." + Again);
            }

            IReadOnlyDictionary<int, string> criteria = ArtifactSections.NumberedItems(text, "Criteria");

            if (criteria.Count == 0)
            {
                return Result.Fail<Response>("The adjudicator's answer adjudicates no criterion under `## Criteria`." + Again);
            }

            if (Unreasoned(criteria) is { Count: > 0 } unreasoned)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The adjudicator's answer marks {Criteria(unreasoned)} Not met without saying why. Each opens `Not met (contradicted).` when the evidence shows the criterion failing, or `Not met (unproven).` when it does not show it either way (D-222).{Again}"));
            }

            if (Unbounded(text, criteria) is { Count: > 0 } unbounded)
            {
                return Result.Fail<Response>(
                    "The adjudicator's answer has entries under `## Open` that bound no criterion it met: "
                    + string.Join("; ", unbounded)
                    + ". An open entry names the met criterion whose proof it bounds -- `Criterion 2: nothing beyond 10,000 draws` -- and anything else is settled in a criterion's entry or left out (D-222)."
                    + Again);
            }

            string path = cycle.PathOf(ArtifactKind.Verify);
            await File.WriteAllTextAsync(path, text, cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(cycle, path, outcome));
        }
    }

    /// <summary>The criteria an answer marks Not met without saying whether the evidence contradicts or only fails to prove them.</summary>
    /// <remarks>
    /// <para><strong>Unproven is still Not met, and still blocks</strong>: whether a criterion was met is the one thing a
    /// verify is for, and a proof that does not reach decides nothing. What the two words said alike was the cause. A
    /// criterion about credentials marked Not met on a coverage gap read as a leak, and remediate cannot tell code to
    /// fix from a proof to strengthen. So each says which, in words the binary reads (D-222).</para>
    /// </remarks>
    /// <param name="criteria">Each criterion entry's first line, by number.</param>
    /// <returns>The numbers, in order.</returns>
    private static List<int> Unreasoned(IReadOnlyDictionary<int, string> criteria) =>
        [.. criteria
            .Where(entry => NotMet().IsMatch(entry.Value) && !Reason().IsMatch(entry.Value))
            .Select(entry => entry.Key)
            .Order()];

    /// <summary>The entries under <c>## Open</c> that bound no criterion the answer met.</summary>
    /// <remarks>
    /// <para><strong>An open entry bounds a criterion that was met</strong>: a limit of its proof, recorded honestly.
    /// That is the whole of what the section is for in a verify (D-03), and nothing else read it: one verdict kept
    /// three entries the evidence could settle or no criterion asked, and a reader stood on them for nine rounds.</para>
    /// <para>Whether an entry the evidence could settle is a reader's judgement. Which criterion it bounds is not: an
    /// entry naming none bounds nothing, one naming a criterion the verdict does not adjudicate bounds nothing, and one
    /// naming a criterion it marks Not met is a reason that belongs in that criterion's entry. A section that says
    /// nothing is open, or holds no list, is left as it is.</para>
    /// </remarks>
    /// <param name="text">The answer.</param>
    /// <param name="criteria">Each criterion entry's first line, by number.</param>
    /// <returns>What is wrong with each such entry, one line each.</returns>
    private static List<string> Unbounded(string text, IReadOnlyDictionary<int, string> criteria)
    {
        List<string> problems = [];

        if (!ArtifactSections.Read(text).TryGetValue(ArtifactSchema.OpenSection, out IReadOnlyList<string>? lines))
        {
            return problems;
        }

        foreach (string line in lines)
        {
            Match item = OpenItem().Match(line);

            if (!item.Success)
            {
                continue;
            }

            string entry = item.Groups["text"].Value.Trim();

            if (Nothing().IsMatch(entry))
            {
                continue;
            }

            List<int> named = [.. Named(entry)];
            string quoted = entry.Length > 60 ? entry[..60] + "..." : entry;

            if (named.Count == 0)
            {
                problems.Add("'" + quoted + "' names no criterion");
                continue;
            }

            foreach (int number in named)
            {
                if (!criteria.TryGetValue(number, out string? adjudicated))
                {
                    problems.Add(string.Create(CultureInfo.InvariantCulture, $"'{quoted}' names criterion {number}, which it does not adjudicate"));
                }
                else if (NotMet().IsMatch(adjudicated))
                {
                    problems.Add(string.Create(CultureInfo.InvariantCulture, $"'{quoted}' bounds criterion {number}, which it marks Not met: why belongs in that entry"));
                }
            }
        }

        return problems;
    }

    // Every criterion number an open entry names: "criterion 2", "criteria 3 and 5", "criteria 3–5".
    private static IEnumerable<int> Named(string entry)
    {
        foreach (Match list in CriterionList().Matches(entry))
        {
            foreach (Match part in Part().Matches(list.Groups["list"].Value))
            {
                int from = int.Parse(part.Groups["from"].Value, CultureInfo.InvariantCulture);
                int to = part.Groups["to"].Success ? int.Parse(part.Groups["to"].Value, CultureInfo.InvariantCulture) : from;

                for (int number = from; number <= Math.Min(to, from + 200); number++)
                {
                    yield return number;
                }
            }
        }
    }

    private static string Criteria(List<int> numbers) =>
        (numbers.Count == 1 ? "criterion " : "criteria ") + string.Join(", ", numbers);

    [GeneratedRegex(@"^\W*not\s+met\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex NotMet();

    [GeneratedRegex(@"^\W*not\s+met\W*\((contradicted|unproven)\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex Reason();

    [GeneratedRegex(@"^(?:[-*+]|[0-9]{1,3}[.)])\s+(?<text>\S.*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex OpenItem();

    [GeneratedRegex(@"^\W*(none|nothing)\W*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex Nothing();

    [GeneratedRegex(@"\bcriteri(?:on|a)\s+(?<list>[0-9]+(?:\s*(?:,|&|–|-|\bto\b|\band\b|\bor\b)\s*[0-9]+)*)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex CriterionList();

    [GeneratedRegex(@"(?<from>[0-9]+)(?:\s*(?:–|-|\bto\b)\s*(?<to>[0-9]+))?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Part();

    /// <summary>The answer with one fence around the whole of it taken off, and otherwise as it came.</summary>
    /// <remarks>
    /// An adjudicator that wraps its document in a fence has written the document, and refusing it cost a rerun
    /// of the largest prompt the framework sends (D-161). Only a fence opening the answer and closing it is
    /// taken: one inside the document is the document's own, and anything around a fence is still refused,
    /// because prose beside a verdict is not a verdict.
    /// </remarks>
    /// <param name="text">The adjudicator's answer.</param>
    /// <returns>The document inside the fence, or the answer unchanged.</returns>
    internal static string Unfenced(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string trimmed = text.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal) || !trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        int open = trimmed.IndexOf('\n', StringComparison.Ordinal);
        int close = trimmed.LastIndexOf("```", StringComparison.Ordinal);

        return open < 0 || close <= open ? text : trimmed[(open + 1)..close].TrimEnd() + "\n";
    }
}
