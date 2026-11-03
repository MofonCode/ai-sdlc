using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Evidence;

/// <summary>
/// Reads a verify document against the recorded evidence, and reports what it cites that the evidence
/// does not hold (D-120).
/// </summary>
/// <remarks>
/// <para>Twenty-eight of one cycle's verify blockers cited <c>CON_08</c>, and the cheapest were a quoted
/// assertion that was not in the diff, each found by a reader paid to read the whole of it. A string
/// match finds those first and for nothing.</para>
/// <para><strong>It reports, and never refuses.</strong> A backticked name is a test, a type, a file, a log
/// line or a setting, and a match cannot tell which a sentence meant. A refusal on that guess would block
/// a correct verdict, and the only way past it would be wording a verdict to satisfy a grep. Whether a claim
/// rests on its evidence stays the reader's judgement.</para>
/// </remarks>
public static class CheckEvidence
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddCheckEvidenceRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>What the verify cites that the evidence does not hold.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Cited">How many distinct names the verify cites in backticks.</param>
    /// <param name="Unfound">Each one that appears in neither the test summary nor the diff.</param>
    /// <param name="NotPassed">Each test the verify says passed that the summary does not record as passed.</param>
    public sealed record Response(Cycle Cycle, int Cited, IReadOnlyList<string> Unfound, IReadOnlyList<string> NotPassed);

    /// <summary>Checks one cycle's verify document.</summary>
    /// <param name="Id">The cycle.</param>
    public sealed record Query(CycleId Id) : IRequest<Response>;

    // A backticked span on one line.
    private static readonly Regex _cited = new(
        @"`(?<name>[^`\r\n]+)`",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    // A backticked name followed by "passed": a claim the test ran and passed.
    private static readonly Regex _claimedPassed = new(
        @"`(?<name>[A-Za-z_][\w.]*)`\s+passed\b",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly Regex _space = new(@"\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Names the evidence itself, which a verdict cites by name and no diff contains.</summary>
    private static readonly HashSet<string> _evidenceNames = new(StringComparer.Ordinal)
    {
        RecordEvidence.SummaryFileName,
        RecordEvidence.DiffFileName,
        RecordEvidence.BaseFileName,
        Coverage.SummaryFileName,
        "build.log",
        "pass",
        "blocked",
        "escalated",
    };

    /// <summary>Checks a verify document's text against the evidence, with nothing read from disk.</summary>
    /// <param name="verify">The verify document.</param>
    /// <param name="summary">The test summary, or null when none was recorded.</param>
    /// <param name="diff">The diff, empty when none was recorded.</param>
    /// <returns>How many names it cites, those found nowhere, and the tests claimed passed that did not.</returns>
    public static (int Cited, IReadOnlyList<string> Unfound, IReadOnlyList<string> NotPassed) Check(string verify, TestSummary? summary, string diff)
    {
        ArgumentNullException.ThrowIfNull(verify);
        ArgumentNullException.ThrowIfNull(diff);

        // Whitespace collapsed on both sides: a quote reflowed across a line is still the same quote.
        string flatDiff = _space.Replace(diff, " ");
        List<string> names = [.. summary?.Tests.Select(test => test.Name) ?? []];
        string body = Body(verify);

        List<string> cited = [.. _cited.Matches(body)
            .Select(match => match.Groups["name"].Value.Trim())
            .Where(name => name.Length > 0 && !_evidenceNames.Contains(name))
            .Distinct(StringComparer.Ordinal)];

        // An elided quote -- "Assert.AreEqual(1, refused.Count, ...)" -- is found when every piece of it is.
        List<string> unfound = [.. cited.Where(name =>
            !names.Exists(test => test.Contains(name, StringComparison.Ordinal))
            && !name.Split(["...", "…"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .All(piece => flatDiff.Contains(_space.Replace(piece, " "), StringComparison.Ordinal)))];

        List<string> notPassed = [.. _claimedPassed.Matches(body)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Where(name => summary is null || !summary.DidPass(name))];

        return (cited.Count, unfound, notPassed);
    }

    // The frontmatter is the envelope, not a claim.
    private static string Body(string verify)
    {
        string text = verify.ReplaceLineEndings("\n");

        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return text;
        }

        int close = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        return close < 0 ? text : text[(close + 4)..];
    }

    /// <summary>What <c>sdlc evidence check</c> prints.</summary>
    /// <param name="response">The check.</param>
    /// <returns>The report.</returns>
    public static string Render(Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"verify.md cites {response.Cited} name{(response.Cited == 1 ? string.Empty : "s")} in backticks. This reports; it refuses nothing, and the reader still judges CON_08."));

        if (response.Unfound.Count == 0 && response.NotPassed.Count == 0)
        {
            text.AppendLine("Every one is in tests.json or diff.patch, and every test it says passed is recorded as passed.");
            return text.ToString();
        }

        if (response.Unfound.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("In none of the recorded evidence -- tests.json, diff.patch, coverage.json, and build.log where this checkout has one:");

            foreach (string name in response.Unfound)
            {
                text.AppendLine("  " + name);
            }
        }

        if (response.NotPassed.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Said to have passed, and not recorded as passed in tests.json:");

            foreach (string name in response.NotPassed)
            {
                text.AppendLine("  " + name);
            }
        }

        return text.ToString();
    }

    private sealed class Handler(SolutionRoot solution, IRepository repository) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);
            string verify = cycle.PathOf(ArtifactKind.Verify);

            if (!File.Exists(verify))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has no verify.md to check."));
            }

            await EvidenceDiff.Restore(cycle, repository, solution.Path, cancellationToken).ConfigureAwait(false);

            string summaryPath = Path.Combine(cycle.EvidenceDirectory, RecordEvidence.SummaryFileName);
            string diffPath = Path.Combine(cycle.EvidenceDirectory, RecordEvidence.DiffFileName);

            TestSummary? summary = File.Exists(summaryPath)
                ? TestSummaryReader.Read(await File.ReadAllTextAsync(summaryPath, cancellationToken).ConfigureAwait(false), summaryPath).ValueOrDefault
                : null;

            string diff = File.Exists(diffPath) ? await File.ReadAllTextAsync(diffPath, cancellationToken).ConfigureAwait(false) : string.Empty;

            // The coverage summary and the build log are evidence too, which a verdict may cite for what only
            // they hold. The log is not committed, so in a pipeline it is simply not searched.
            foreach (string other in new[] { Coverage.SummaryFileName, "build.log" })
            {
                string path = Path.Combine(cycle.EvidenceDirectory, other);

                if (File.Exists(path))
                {
                    diff += "\n" + await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                }
            }

            (int cited, IReadOnlyList<string> unfound, IReadOnlyList<string> notPassed) = Check(
                await File.ReadAllTextAsync(verify, cancellationToken).ConfigureAwait(false),
                summary,
                diff);

            return Result.Ok(new Response(cycle, cited, unfound, notPassed));
        }
    }
}
