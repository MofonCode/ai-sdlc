using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Proposals;
using Sdlc.Core.Reports;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Mining;

/// <summary>
/// Mining's two sets of fresh readers, written and started by the binary: one per finished cycle and document with
/// findings, and the two that review what a person approved (D-183).
/// </summary>
/// <remarks>
/// <para><strong>The mine skill started these itself</strong>, with the reader's launch line typed per reader. Moved
/// here for the reason every reader moved: a headless or careful host has to approve each such line, and the binary
/// starts a process the same way on every host. Each reader is handed only what its prompt holds, answers in prose,
/// and its answer is written beside its prompt, where the skill reads it.</para>
/// <para><strong>What is in each prompt is the record and nothing else.</strong> A reader of one document's findings
/// is handed that document's findings as <c>sdlc findings</c> prints them; the reviewers are handed the files the
/// skill named, and no conversation.</para>
/// </remarks>
public static class MineReaders
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

    /// <summary>What the second reviewer, handed the report and the proposals, is asked.</summary>
    public const string CompletenessQuestion =
        "Here is everything the record raised, and what was made of it. Name each argued rule, reopen, criterion that "
        + "failed more than one verdict, and perspective at the bar that has **no answer** in the proposal file -- "
        + "neither a proposal nor a line under `## Not proposed` or `## For the framework` -- and each open suggestion "
        + "that is neither a successor, a `## Declined` line nor counted on an `## Ignored` line. Name each concern the "
        + "report marks at the bar that is ignored again, and each `## Ignored` gist that rewords one the report already "
        + "lists. Then name each `no proposal, because` whose reason the report contradicts. Answer **complete**, or list "
        + "the gaps. Do not propose anything yourself.";

    /// <summary>Registers this operation's handlers.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddMineReadersRequestHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddTransient<IRequestHandler<Read, RunRound.Response>, ReadHandler>()
            .AddTransient<IRequestHandler<Review, RunRound.Response>, ReviewHandler>();
    }

    /// <summary>Starts one reader per finished cycle and document with findings, as the sitting reads them.</summary>
    /// <param name="Directory">Where the prompts and answers go: outside the repository.</param>
    /// <param name="Cycles">The cycles the sitting reads, or null for every finished one not mined yet.</param>
    /// <param name="Timeout">How long any one reader may take.</param>
    public sealed record Read(string Directory, IReadOnlyList<CycleId>? Cycles, TimeSpan Timeout) : IRequest<RunRound.Response>;

    /// <summary>Starts the two readers that review what a person approved from a sitting.</summary>
    /// <param name="Directory">Where the prompts and answers go: outside the repository.</param>
    /// <param name="Proposals">The marked proposal file, or null for the newest one the sitting wrote.</param>
    /// <param name="Timeout">How long either reader may take.</param>
    public sealed record Review(string Directory, string? Proposals, TimeSpan Timeout) : IRequest<RunRound.Response>;

    private sealed class ReadHandler(SolutionRoot solution, Mediator mediator) : IRequestHandler<Read, RunRound.Response>
    {
        public async Task<Result<RunRound.Response>> Handle(Read request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<ProposeMining.Response> sitting = await mediator
                .Send<ProposeMining.Query, ProposeMining.Response>(new ProposeMining.Query(request.Cycles), cancellationToken)
                .ConfigureAwait(false);

            if (sitting.IsFailed)
            {
                return sitting.ToResult<RunRound.Response>();
            }

            Directory.CreateDirectory(request.Directory);
            int written = 0;

            foreach (CycleId id in sitting.Value.Sitting ?? [])
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
                return Result.Fail<RunRound.Response>("No finished cycle in this sitting has a document with findings, so there is nothing for a reader to group.");
            }

            return await mediator
                .Send<RunRound.Command, RunRound.Response>(new RunRound.Command(request.Directory, TimeSpan.Zero, request.Timeout, Text: true), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private sealed class ReviewHandler(SolutionRoot solution, Mediator mediator) : IRequestHandler<Review, RunRound.Response>
    {
        public async Task<Result<RunRound.Response>> Handle(Review request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string reports = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, ReportFile.DirectoryName);
            string? proposals = request.Proposals ?? Newest(reports, "*-mining.proposed.md");
            string? report = Newest(reports, "*-mining.md");

            if (proposals is null || !File.Exists(proposals))
            {
                return Result.Fail<RunRound.Response>("There is no proposal file to review. Run the sitting first, or name the file with --proposals <file>.");
            }

            if (report is null)
            {
                return Result.Fail<RunRound.Response>("There is no mining report to review against. 'sdlc mine' keeps one in .sdlc/reports; run it again.");
            }

            string sdlc = Path.Combine(solution.Path, Cycle.SdlcDirectoryName);
            StringBuilder alignment = new(AlignmentQuestion + "\n\n");

            Section(alignment, "The proposal file, as the person marked it", await File.ReadAllTextAsync(proposals, cancellationToken).ConfigureAwait(false));

            foreach (string draft in Directory.Exists(sdlc) ? [.. Directory.GetFiles(sdlc, "*.draft.md").Order(StringComparer.Ordinal)] : Array.Empty<string>())
            {
                Section(alignment, Path.GetFileName(draft), await File.ReadAllTextAsync(draft, cancellationToken).ConfigureAwait(false));
            }

            string document = Path.Combine(sdlc, "solution.md");

            if (File.Exists(document))
            {
                Section(alignment, "solution.md", await File.ReadAllTextAsync(document, cancellationToken).ConfigureAwait(false));
            }

            StringBuilder completeness = new(CompletenessQuestion + "\n\n");
            Section(completeness, "The report", await File.ReadAllTextAsync(report, cancellationToken).ConfigureAwait(false));
            Section(completeness, "The proposal file", await File.ReadAllTextAsync(proposals, cancellationToken).ConfigureAwait(false));

            Directory.CreateDirectory(request.Directory);
            await File.WriteAllTextAsync(Path.Combine(request.Directory, "alignment" + RunRound.PromptSuffix), alignment.ToString(), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(request.Directory, "completeness" + RunRound.PromptSuffix), completeness.ToString(), cancellationToken).ConfigureAwait(false);

            return await mediator
                .Send<RunRound.Command, RunRound.Response>(new RunRound.Command(request.Directory, TimeSpan.Zero, request.Timeout, Text: true), cancellationToken)
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
