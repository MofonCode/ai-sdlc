using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Cycles;

/// <summary>What throwing a sandbox away would lose, cycle by cycle.</summary>
public enum DiscardVerdict
{
    /// <summary>Nothing was written or argued: no intent, or one no round judged and no decision fixed.</summary>
    Empty,

    /// <summary>Written and judged, and nothing in it a later cycle would learn from.</summary>
    Deletable,

    /// <summary>It holds a choice or a finding worth keeping: record it as abandoned rather than delete it.</summary>
    WorthRecording,

    /// <summary>A person already recorded it as abandoned; it leaves by <c>sdlc admit</c>.</summary>
    Recorded,
}

/// <summary>
/// Before a sandbox is thrown away: which of its cycles can simply go, and which hold something worth recording
/// (D-146).
/// </summary>
/// <remarks>
/// <para><strong>The binary makes the first cut.</strong> Empty is a file listing: no intent, or an intent no round
/// judged with no decision fixed. A cycle that fixed a decision — a choice somebody made, a suggestion declined — or
/// whose readers raised anything is worth recording: <c>propose</c> and <c>decisions</c> read the record, and a cycle
/// deleted is one they never see. What is left was written and judged and argued nothing, and can go.</para>
/// <para><strong>It reports and never deletes.</strong> Removing a worktree and a branch is a person's to confirm, and
/// an abandonment is a person's file. It prints the removal only once nothing worth recording is left unrecorded.</para>
/// </remarks>
public static class DiscardSandbox
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddDiscardSandboxRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks what throwing this sandbox away would lose.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>One cycle, and what discarding it would lose.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Headline">What its intent is about, when it says.</param>
    /// <param name="Verdict">What to do with it.</param>
    /// <param name="Why">What it holds, for a person.</param>
    public sealed record Entry(CycleId Id, string? Headline, DiscardVerdict Verdict, string Why);

    /// <summary>The review.</summary>
    /// <param name="Sandbox">The sandbox's branch.</param>
    /// <param name="Folder">The sandbox's folder.</param>
    /// <param name="Cycles">Every cycle it carries, oldest first.</param>
    /// <param name="Remove">The command that removes it, run from the person's own checkout, once nothing worth recording is unrecorded; else null.</param>
    public sealed record Response(string Sandbox, string Folder, IReadOnlyList<Entry> Cycles, string? Remove);

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            string? current = repository.IsRepository(root) ? repository.CurrentBranch(root) : null;

            if (!SandboxBranch.TryParse(current, out _))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"This is {current ?? "no branch"}, not a sandbox. Run it in the sandbox you mean to throw away."));
            }

            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);
            string? at = new[] { "origin/" + integration, integration }.FirstOrDefault(name => repository.RefExists(root, name));

            List<Entry> entries = [];

            foreach (Cycle cycle in CycleIndex.All(root).OrderBy(c => c.Id.ToString(), StringComparer.Ordinal))
            {
                string folder = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, cycle.Id.ToString()) + "/";

                // Only what this sandbox added: a cycle already on the integration branch is not this sandbox's to lose.
                if (at is not null && (repository.PathExistsAt(root, at, folder + Cycle.FileNameOf(ArtifactKind.Intent))
                    || repository.PathExistsAt(root, at, folder + "abandoned.md")))
                {
                    continue;
                }

                ArtifactOutline outline = await ArtifactOutlineReader
                    .ReadFile(cycle.PathOf(ArtifactKind.Intent), cancellationToken)
                    .ConfigureAwait(false);

                if (cycle.IsAbandoned)
                {
                    entries.Add(new Entry(cycle.Id, outline.Headline, DiscardVerdict.Recorded, "recorded as abandoned; 'sdlc admit' takes the record to the integration branch"));
                    continue;
                }

                if (!File.Exists(cycle.PathOf(ArtifactKind.Intent)))
                {
                    entries.Add(new Entry(cycle.Id, outline.Headline, DiscardVerdict.Empty, "no intent was written"));
                    continue;
                }

                Result<ReadCycleStatus.Response> status = await mediator
                    .Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(new ReadCycleStatus.Query(cycle.Id), cancellationToken)
                    .ConfigureAwait(false);

                if (status.IsFailed)
                {
                    return status.ToResult<Response>();
                }

                int decisions = await FixedDecisions(cycle, cancellationToken).ConfigureAwait(false);
                int rounds = status.Value.Artifacts.Sum(a => a.Rounds);
                int raised = status.Value.Artifacts.Sum(a => a.OpenBlockers.Count + a.OpenSuggestions.Count);
                bool argued = status.Value.Artifacts.Any(a => a.Rounds > 1);

                List<string> holds = [];

                if (decisions > 0)
                {
                    holds.Add(Count(decisions, "fixed decision"));
                }

                if (raised > 0)
                {
                    holds.Add(Count(raised, "open finding"));
                }

                if (argued)
                {
                    holds.Add("rounds that answered what a reader raised");
                }

                entries.Add(holds.Count > 0
                    ? new Entry(cycle.Id, outline.Headline, DiscardVerdict.WorthRecording, "holds " + string.Join(", ", holds) + string.Create(CultureInfo.InvariantCulture, $" - record it: 'sdlc draft abandon {cycle.Id}', placed by a person, then 'sdlc admit'"))
                    : rounds == 0
                        ? new Entry(cycle.Id, outline.Headline, DiscardVerdict.Empty, "an intent no round judged, and no decision fixed")
                        : new Entry(cycle.Id, outline.Headline, DiscardVerdict.Deletable, Count(rounds, "clean round") + " and nothing argued"));
            }

            string folderPath = Path.GetFullPath(root).Replace('\\', '/');
            bool clear = entries.All(e => e.Verdict is DiscardVerdict.Empty or DiscardVerdict.Deletable);

            return Result.Ok(new Response(
                current!,
                folderPath,
                entries,
                clear ? string.Create(CultureInfo.InvariantCulture, $"git worktree remove --force {folderPath} && git branch -D {current}") : null));
        }

        private static string Count(int count, string what) =>
            string.Create(CultureInfo.InvariantCulture, $"{count} {what}{(count == 1 ? string.Empty : "s")}");

        /// <summary>The entries under the intent's <c>## Fixed decisions</c>: each a choice somebody made.</summary>
        private static async Task<int> FixedDecisions(Cycle cycle, CancellationToken cancellationToken)
        {
            string[] lines = (await File.ReadAllTextAsync(cycle.PathOf(ArtifactKind.Intent), cancellationToken).ConfigureAwait(false))
                .ReplaceLineEndings("\n")
                .Split('\n');

            return lines
                .SkipWhile(line => !line.TrimEnd().Equals("## Fixed decisions", StringComparison.Ordinal))
                .Skip(1)
                .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
                .Count(line => line.StartsWith("- ", StringComparison.Ordinal));
        }
    }
}
