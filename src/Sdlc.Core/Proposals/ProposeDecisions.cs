using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Proposals;

/// <summary>Where a candidate decision was taken from.</summary>
public enum DecisionSource
{
    /// <summary>
    /// An intent's <c>Fixed decisions</c> — a choice a person had already taken when the cycle opened.
    /// </summary>
    /// <remarks>
    /// A person's own words, so one statement is enough (D-189). An entry marked unconfirmed is left out: it is
    /// a figure or a belief nobody measured, an assumption rather than a choice.
    /// </remarks>
    Intent,

    /// <summary>A plan's <c>Shape and departures</c> — which approach, and what it departed from.</summary>
    /// <remarks>
    /// An agent's prose, so it qualifies only when the same choice appears in two or more cycles (D-189): a
    /// departure argued once is a local choice, the same one argued across cycles is a default that has actually
    /// moved, and only a reader can tell whether two passages are one choice.
    /// </remarks>
    Plan,

    /// <summary>
    /// A departure a completed cycle declared from a recorded decision (D-139).
    /// </summary>
    /// <remarks>
    /// The one candidate whose proposal is already known: a supersede of the decision it names. Left
    /// standing, the record says one thing and the code another, and the next cycle is judged by the decision
    /// this one set aside.
    /// </remarks>
    Departure,

    /// <summary>Why a person abandoned the cycle, from its <c>abandoned.md</c>.</summary>
    /// <remarks>
    /// The only passage an abandoned cycle gives (D-189). The reason is a person's decision, usually naming what
    /// they refused; the plan's approach is what was abandoned, so it is never offered as a choice to keep.
    /// </remarks>
    Abandoned,
}

/// <summary>
/// One passage from a completed cycle that may hold a decision worth recording.
/// </summary>
/// <remarks>
/// A passage rather than a parsed decision. Whether prose records a real choice, what its alternatives
/// were, and whether two passages in different cycles are the same decision are judgements about
/// language — so this hands over the text with its provenance and keeps its opinions to itself.
/// </remarks>
/// <param name="Cycle">The cycle it came from.</param>
/// <param name="Source">Which section.</param>
/// <param name="Passage">The text, as the artifact holds it.</param>
public sealed record Candidate(CycleId Cycle, DecisionSource Source, string Passage);

/// <summary>
/// Reads the choices a solution's completed cycles settled, for a person to record. Writes nothing.
/// </summary>
/// <remarks>
/// <para><strong>Captured forward, because a decision cannot be counted from findings.</strong> A constraint is
/// earned backward from findings — the same rule blocking three cycles says a rule is missing. A decision never
/// collides with a rule at all: a colour, a layout convention, a code-style choice produces no finding,
/// so no amount of history review surfaces it. What can be read instead is the place a choice was
/// *stated* — an intent's fixed decisions and a plan's shape — and that is what this walks.</para>
///
/// <para><strong>Only completed cycles, and that is the whole provenance story</strong> (D-190). A cycle is
/// completed when its pull request merged after a passing verify — a person accepted it — or a person abandoned
/// it. Reading a cycle still in flight would make the record a place where an agent's draft prose became doctrine
/// that later cycles align to. The caller names the completed cycles; this reads those and no others.</para>
///
/// <para><strong>It proposes and never places.</strong> <c>decisions.md</c> is a person's file and the
/// hook denies every agent write to it, for the reason the rule file is protected: a stage reads the
/// record for alignment, so an agent that could write it could invent doctrine and then align to
/// it.</para>
/// </remarks>
public static class ProposeDecisions
{
    /// <summary>The word that marks a fixed decision as a belief nobody measured.</summary>
    public const string Unconfirmed = "unconfirmed";

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddProposeDecisionsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks what the named completed cycles settled.</summary>
    /// <param name="Completed">The completed cycles to read; every other cycle is skipped.</param>
    public sealed record Query(IReadOnlyCollection<CycleId> Completed) : IRequest<Response>;

    /// <summary>
    /// What the completed record holds.
    /// </summary>
    /// <param name="NextId">
    /// The next free decision id, one past the highest ever used. Stated once: a refused proposal must
    /// leave no trace in the sequence, because a gap is the record of an entry that was removed.
    /// </param>
    /// <param name="Standing">
    /// What is already recorded, so a proposal is not a duplicate of something settled. Handed over
    /// rather than matched against: whether a passage says the same thing as an existing entry is a
    /// judgement about prose.
    /// </param>
    /// <param name="Candidates">The passages, oldest cycle first.</param>
    /// <param name="Skipped">
    /// How many cycles were not read for not being completed, or for not being this sitting's. Reported so a thin
    /// result says why it is thin rather than reading as a solution that decided nothing.
    /// </param>
    /// <param name="Cycles">How many cycles the solution holds.</param>
    public sealed record Response(
        DecisionId NextId,
        IReadOnlyList<Decision> Standing,
        IReadOnlyList<Candidate> Candidates,
        int Skipped,
        int Cycles)
    {
        /// <summary>Whether there is anything to read.</summary>
        public bool HasMaterial => Candidates.Count > 0;
    }

    /// <summary>
    /// Walks the completed cycles and collects what they stated.
    /// </summary>
    /// <param name="solution">The root of the solution being read.</param>
    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<IReadOnlyList<Decision>> recorded =
                await Recorded(cancellationToken).ConfigureAwait(false);

            // A malformed record is reported rather than worked around, for the reason the rule file's
            // reader gives: allocating an id from a document this cannot read risks handing back one
            // already taken, and a reused id is the one mistake the monotonic rule exists to prevent.
            if (recorded.IsFailed)
            {
                return recorded.ToResult<Response>();
            }

            IReadOnlyList<Cycle> cycles = CycleIndex.All(solution.Path);

            List<Candidate> candidates = [];
            int skipped = 0;

            foreach (Cycle cycle in cycles)
            {
                if (!request.Completed.Contains(cycle.Id))
                {
                    skipped++;
                    continue;
                }

                // An abandoned cycle gives its reason and nothing else: its approach is what was refused.
                if (cycle.IsAbandoned)
                {
                    Result<AbandonRecord> abandon = AbandonRecord.Read(
                        await File.ReadAllTextAsync(cycle.AbandonedPath, cancellationToken).ConfigureAwait(false),
                        cycle.AbandonedPath);

                    if (abandon.IsSuccess && !string.IsNullOrWhiteSpace(abandon.Value.Reason))
                    {
                        candidates.Add(new Candidate(cycle.Id, DecisionSource.Abandoned, abandon.Value.Reason.ReplaceLineEndings(" ").Trim()));
                    }

                    continue;
                }

                await Collect(cycle, ArtifactKind.Intent, "Fixed decisions", DecisionSource.Intent, candidates, cancellationToken)
                    .ConfigureAwait(false);

                await Collect(cycle, ArtifactKind.Plan, "Shape and departures", DecisionSource.Plan, candidates, cancellationToken)
                    .ConfigureAwait(false);

                // A departure from a decision still standing: the record no longer says what the solution does.
                foreach (Departure departure in await Departures.Of(cycle, cancellationToken).ConfigureAwait(false))
                {
                    if (recorded.Value.Any(decision => decision.Id == departure.Decision && decision.IsStanding))
                    {
                        candidates.Add(new Candidate(cycle.Id, DecisionSource.Departure, $"{Departures.Opening}{departure.Decision} {departure.Text}"));
                    }
                }
            }

            return Result.Ok(new Response(
                DecisionParser.NextId(recorded.Value),
                [.. recorded.Value.Where(decision => decision.IsStanding)],
                candidates,
                skipped,
                cycles.Count));
        }

        /// <summary>
        /// Takes one section out of one artifact, as paragraphs.
        /// </summary>
        /// <remarks>
        /// Paragraphs rather than lines, because a decision is a sentence or two and a line-per-candidate
        /// listing would split one choice across several entries. A missing artifact or an empty section
        /// is silence rather than an error: a bug-fix cycle may genuinely have fixed nothing in advance.
        /// </remarks>
        private static async Task Collect(
            Cycle cycle,
            ArtifactKind kind,
            string section,
            DecisionSource source,
            List<Candidate> candidates,
            CancellationToken cancellationToken)
        {
            string path = cycle.PathOf(kind);

            if (!File.Exists(path))
            {
                return;
            }

            string markdown = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            if (!ArtifactSections.Read(markdown).TryGetValue(section, out IReadOnlyList<string>? lines))
            {
                return;
            }

            foreach (string paragraph in Paragraphs(lines))
            {
                // A belief nobody measured is an assumption, not a choice (D-189).
                if (source == DecisionSource.Intent && paragraph.Contains(Unconfirmed, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                candidates.Add(new Candidate(cycle.Id, source, paragraph));
            }
        }

        /// <summary>
        /// Groups a section's lines into paragraphs, dropping the blank runs between them; each list item starts
        /// one of its own, because an intent writes one fixed decision per bullet.
        /// </summary>
        private static IEnumerable<string> Paragraphs(IReadOnlyList<string> lines)
        {
            List<string> current = [];

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (current.Count > 0 && (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal)))
                {
                    yield return string.Join(' ', current);
                    current = [];
                }

                if (trimmed.Length == 0)
                {
                    if (current.Count > 0)
                    {
                        yield return string.Join(' ', current);
                        current = [];
                    }

                    continue;
                }

                current.Add(trimmed);
            }

            if (current.Count > 0)
            {
                yield return string.Join(' ', current);
            }
        }

        /// <summary>What the decision record already holds, superseded entries included.</summary>
        private async Task<Result<IReadOnlyList<Decision>>> Recorded(CancellationToken cancellationToken)
        {
            string path = Path.Combine(
                solution.Path,
                Cycle.SdlcDirectoryName,
                DecisionParser.FileName);

            if (!File.Exists(path))
            {
                return Result.Ok<IReadOnlyList<Decision>>([]);
            }

            string markdown = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            return DecisionParser.Parse(markdown, path);
        }
    }
}
