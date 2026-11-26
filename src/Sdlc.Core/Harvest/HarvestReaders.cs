using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Reports;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Harvest;

/// <summary>
/// A harvest's two sets of fresh readers, written and started by the binary: one per completed cycle and document
/// with findings, and the two that review what a person approved from one aspect (D-183).
/// </summary>
/// <remarks>
/// <para><strong>The skill started these itself</strong>, with the reader's launch line typed per reader. Moved
/// here for the reason every reader moved: a headless or careful host has to approve each such line, and the binary
/// starts a process the same way on every host. Each reader is handed only what its prompt holds, answers in prose,
/// and its answer is written beside its prompt, where the skill reads it.</para>
/// <para><strong>What is in each prompt is the record and nothing else.</strong> A reader of one document's findings
/// is handed that document's findings as <c>sdlc findings</c> prints them; the reviewers are handed the files the
/// aspect's sitting wrote, and no conversation.</para>
/// </remarks>
public static class HarvestReaders
{
    /// <summary>What each reader of one document's findings is asked.</summary>
    public const string ReadQuestion =
        "Group these findings by the problem underneath, not by their wording -- one plan raised the same missing log "
        + "level twenty times in twenty wordings. For each group: the finding ids, one sentence naming the problem, how "
        + "many rounds its longest finding stood, and the remedy the readers agreed on, if they did. Return the groups "
        + "and nothing else.";

    /// <summary>What the first reviewer, handed the marked proposals, the drafts and the solution document, is asked.</summary>
    public const string AlignmentQuestion =
        "For each proposal: was it accepted as proposed, edited, or declined? For each edit and each decline, does what "
        + "was approved still serve what the evidence lines describe? And does the approved set, taken together, move the "
        + "solution toward what solution.md says it is, or away from it? Answer **in alignment**, or **deviates**, with "
        + "each place it deviates and why. Do not argue for the proposals.";

    /// <summary>Registers this operation's handlers.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddHarvestReadersRequestHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddTransient<IRequestHandler<Read, RunRound.Response>, ReadHandler>()
            .AddTransient<IRequestHandler<Review, RunRound.Response>, ReviewHandler>();
    }

    /// <summary>
    /// What the second reviewer, handed one aspect's report and its proposal file, is asked: whether every item the
    /// report raised has an answer.
    /// </summary>
    /// <param name="aspect">The aspect reviewed.</param>
    /// <returns>The question.</returns>
    public static string CompletenessQuestion(HarvestAspect aspect) =>
        "Here is everything the record raised for " + HarvestAspects.NameOf(aspect) + ", and what was made of it. Name each "
        + Items(aspect) + " that has **no answer** in the proposal file -- neither a proposal nor a line under "
        + "`## Not proposed` or `## For the framework`. "
        + (aspect == HarvestAspect.Suggestions
            ? "Name each concern the report marks at the bar that is ignored again, each `## Ignored` gist that rewords one the report already lists, "
                + "and each `## Covered` line whose rule, decision, successor or file does not in fact handle the suggestion. "
            : string.Empty)
        + "Then name each `no proposal, because` whose reason the report contradicts, and each `already recorded as` whose "
        + "entry does not in fact say it. Answer **complete**, or list the "
        + "gaps. Do not propose anything yourself.";

    private static string Items(HarvestAspect aspect) => aspect switch
    {
        HarvestAspect.Constraints => "rule cited across cycles, argued rule, group of findings that rested on no rule, perspective at the bar, rule behind deviations in more than one cycle, criterion that failed more than one verdict, and rule over its word budget",
        HarvestAspect.Decisions => "passage stated in a completed cycle, reopen, change made at a person's request, rule argued inside a cycle, and decision over its word budget",
        HarvestAspect.Suggestions => "open suggestion (a successor, a `## Declined` line, a `## Covered` line, or counted on an `## Ignored` line answers it; a `## Covered` line naming `<cycle>/*` answers every open suggestion of that abandoned cycle) and gap in the framework",
        HarvestAspect.Stack => "row or rule the declared stack never mentions",
        HarvestAspect.Areas => "area name proposed at the bar, defined area every plan or no plan touched, and changed file in no area",
        _ => "item",
    };

    /// <summary>Starts one reader per completed cycle and document with findings, as an aspect's sitting reads them.</summary>
    /// <param name="Directory">Where the prompts and answers go: outside the repository.</param>
    /// <param name="Aspect">The aspect whose sitting is read.</param>
    /// <param name="Cycles">The cycles the sitting reads, or null for every completed one still needing the aspect.</param>
    /// <param name="Timeout">How long any one reader may take.</param>
    /// <param name="Readers">How the readers run on this machine, or null for the default.</param>
    public sealed record Read(string Directory, HarvestAspect Aspect, IReadOnlyList<CycleId>? Cycles, TimeSpan Timeout, ReaderLimits? Readers = null) : IRequest<RunRound.Response>;

    /// <summary>Starts the two readers that review what a person approved from one aspect's sitting.</summary>
    /// <param name="Directory">Where the prompts and answers go: outside the repository.</param>
    /// <param name="Aspect">The aspect reviewed.</param>
    /// <param name="Proposals">The marked proposal file, or null for the newest one the aspect's sitting wrote.</param>
    /// <param name="Timeout">How long either reader may take.</param>
    /// <param name="Readers">How the readers run on this machine, or null for the default.</param>
    public sealed record Review(string Directory, HarvestAspect Aspect, string? Proposals, TimeSpan Timeout, ReaderLimits? Readers = null) : IRequest<RunRound.Response>;

    /// <summary>The name a sitting's report is kept under, in the reports directory.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <returns>The kind, as <c>sdlc</c> keeps it.</returns>
    public static string ReportKind(HarvestAspect aspect) => "harvest-" + HarvestAspects.NameOf(aspect);

    private sealed class ReadHandler(SolutionRoot solution, Mediator mediator) : IRequestHandler<Read, RunRound.Response>
    {
        public async Task<Result<RunRound.Response>> Handle(Read request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<HarvestReport.Response> sitting = await mediator
                .Send<HarvestReport.Query, HarvestReport.Response>(new HarvestReport.Query(request.Aspect, request.Cycles), cancellationToken)
                .ConfigureAwait(false);

            if (sitting.IsFailed)
            {
                return sitting.ToResult<RunRound.Response>();
            }

            Directory.CreateDirectory(request.Directory);
            int written = 0;

            foreach (CycleId id in sitting.Value.Sitting)
            {
                if (!new Cycle(id, solution.Path).Exists)
                {
                    continue;
                }

                Result<ReadFindings.Response> findings = await mediator
                    .Send<ReadFindings.Query, ReadFindings.Response>(new ReadFindings.Query(id), cancellationToken)
                    .ConfigureAwait(false);

                if (findings.IsFailed)
                {
                    return findings.ToResult<RunRound.Response>();
                }

                foreach (ArtifactFindings artifact in findings.Value.Artifacts.Where(a => a.Rounds.Any(round => round.Findings.Count > 0)))
                {
                    string rendered = FindingsRenderer.Render(findings.Value with { Artifacts = [artifact] });
                    string name = string.Create(CultureInfo.InvariantCulture, $"{id}-{Cycle.NameOf(artifact.Artifact)}");

                    await File.WriteAllTextAsync(
                        Path.Combine(request.Directory, name + RunRound.PromptSuffix),
                        ReadQuestion + "\n\n" + rendered,
                        cancellationToken).ConfigureAwait(false);
                    written++;
                }
            }

            if (written == 0)
            {
                return Result.Fail<RunRound.Response>("No completed cycle in this sitting has a document with findings, so there is nothing for a reader to group.");
            }

            return await mediator
                .Send<RunRound.Command, RunRound.Response>(new RunRound.Command(request.Directory, TimeSpan.Zero, request.Timeout, Text: true, request.Readers), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private sealed class ReviewHandler(SolutionRoot solution, Mediator mediator) : IRequestHandler<Review, RunRound.Response>
    {
        public async Task<Result<RunRound.Response>> Handle(Review request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string kind = ReportKind(request.Aspect);
            string reports = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, ReportFile.DirectoryName);
            string? proposals = request.Proposals ?? Newest(reports, "*-" + kind + ".proposed.md");
            string? report = Newest(reports, "*-" + kind + ".md");

            if (proposals is null || !File.Exists(proposals))
            {
                return Result.Fail<RunRound.Response>("There is no proposal file to review. Run the sitting first, or name the file with --proposals <file>.");
            }

            if (report is null)
            {
                return Result.Fail<RunRound.Response>(string.Create(CultureInfo.InvariantCulture, $"There is no {HarvestAspects.NameOf(request.Aspect)} report to review against. 'sdlc harvest {HarvestAspects.NameOf(request.Aspect)}' keeps one in .sdlc/reports; run it again."));
            }

            string sdlc = Path.Combine(solution.Path, Cycle.SdlcDirectoryName);
            StringBuilder alignment = new(AlignmentQuestion + "\n\n");

            Section(alignment, "The proposal file, as the person marked it", await File.ReadAllTextAsync(proposals, cancellationToken).ConfigureAwait(false));

            if (HarvestAspects.DraftOf(request.Aspect) is { } draft && File.Exists(Path.Combine(sdlc, draft)))
            {
                Section(alignment, draft, await File.ReadAllTextAsync(Path.Combine(sdlc, draft), cancellationToken).ConfigureAwait(false));
            }

            string document = Path.Combine(sdlc, "solution.md");

            if (File.Exists(document))
            {
                Section(alignment, "solution.md", await File.ReadAllTextAsync(document, cancellationToken).ConfigureAwait(false));
            }

            StringBuilder completeness = new(CompletenessQuestion(request.Aspect) + "\n\n");
            Section(completeness, "The report", await File.ReadAllTextAsync(report, cancellationToken).ConfigureAwait(false));
            Section(completeness, "The proposal file", await File.ReadAllTextAsync(proposals, cancellationToken).ConfigureAwait(false));

            Directory.CreateDirectory(request.Directory);
            await File.WriteAllTextAsync(Path.Combine(request.Directory, "alignment" + RunRound.PromptSuffix), alignment.ToString(), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(request.Directory, "completeness" + RunRound.PromptSuffix), completeness.ToString(), cancellationToken).ConfigureAwait(false);

            return await mediator
                .Send<RunRound.Command, RunRound.Response>(new RunRound.Command(request.Directory, TimeSpan.Zero, request.Timeout, Text: true, request.Readers), cancellationToken)
                .ConfigureAwait(false);
        }

        private static string? Newest(string directory, string pattern) =>
            Directory.Exists(directory)
                ? Directory.GetFiles(directory, pattern).Order(StringComparer.Ordinal).LastOrDefault()
                : null;

        private static void Section(StringBuilder text, string heading, string body) =>
            text.Append(CultureInfo.InvariantCulture, $"## {heading}\n\n{body.TrimEnd()}\n\n");
    }
}
