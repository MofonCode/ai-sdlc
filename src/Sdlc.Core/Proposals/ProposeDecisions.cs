using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Proposals;

/// <summary>Which section a candidate decision was taken from.</summary>
public enum DecisionSource
{
    /// <summary>
    /// An intent's <c>Fixed decisions</c> — a choice a person had already taken when the cycle opened.
    /// </summary>
    /// <remarks>
    /// The strongest material here, and §19 item 6 is that nothing reads it: a fixed decision like
    /// <em>"keep it very simple — nothing fancy"</em> is recorded with attribution, survives every gate,
    /// and no stage acts on it. These are a person's own words, which is why they need no further
    /// provenance than the approval.
    /// </remarks>
    Intent,

    /// <summary>A plan's <c>Shape and departures</c> — which approach, and what it departed from.</summary>
    /// <remarks>
    /// §4.4 calls this the plan's <em>decision</em>, as against the <c>Files</c> estimate that nothing
    /// judges. A departure argued here once is a local choice; the same departure argued across cycles is
    /// a default that has actually moved, and only a reader can tell which.
    /// </remarks>
    Plan,
}

/// <summary>
/// One passage from an approved cycle that may hold a decision worth recording.
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
/// Reads the choices a solution's approved cycles settled, for a person to record. Writes nothing.
/// </summary>
/// <remarks>
/// <para><strong>Captured forward, because a decision cannot be mined.</strong> A constraint is earned
/// backward from findings — the same rule blocking three cycles says a rule is missing. A decision never
/// collides with a rule at all: a colour, a layout convention, a code-style choice produces no finding,
/// so no amount of history review surfaces it. What can be read instead is the place a choice was
/// *stated* — an intent's fixed decisions and a plan's shape — and that is what this walks.</para>
///
/// <para><strong>Only approved cycles, and that is the whole provenance story.</strong> An approval
/// records a hash of the intent, the spec and the plan against a person's name, so a passage from an
/// approved cycle is one a person signed off on those exact bytes. Reading unapproved cycles would make
/// the record a place where an agent's draft prose became doctrine that later cycles align to — which is
/// the authorship problem this shape exists to answer.</para>
///
/// <para><strong>It proposes and never places.</strong> <c>decisions.md</c> is a person's file and the
/// hook denies every agent write to it, for the reason the rule file is protected: a stage reads the
/// record for alignment, so an agent that could write it could invent doctrine and then align to
/// it.</para>
/// </remarks>
public static class ProposeDecisions
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddProposeDecisionsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks what this solution's approved cycles settled.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>
    /// What the approved record holds.
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
    /// <param name="Unapproved">
    /// How many cycles were skipped for having no standing approval. Reported so a thin result says why
    /// it is thin rather than reading as a solution that decided nothing.
    /// </param>
    /// <param name="Cycles">How many cycles the solution holds.</param>
    public sealed record Response(
        DecisionId NextId,
        IReadOnlyList<Decision> Standing,
        IReadOnlyList<Candidate> Candidates,
        int Unapproved,
        int Cycles)
    {
        /// <summary>Whether there is anything to read.</summary>
        public bool HasMaterial => Candidates.Count > 0;
    }

    /// <summary>
    /// Walks the approved cycles and collects what they stated.
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
            int unapproved = 0;

            foreach (Cycle cycle in cycles)
            {
                if (!await IsApproved(cycle, cancellationToken).ConfigureAwait(false))
                {
                    unapproved++;
                    continue;
                }

                await Collect(cycle, ArtifactKind.Intent, "Fixed decisions", DecisionSource.Intent, candidates, cancellationToken)
                    .ConfigureAwait(false);

                await Collect(cycle, ArtifactKind.Plan, "Shape and departures", DecisionSource.Plan, candidates, cancellationToken)
                    .ConfigureAwait(false);
            }

            return Result.Ok(new Response(
                DecisionParser.NextId(recorded.Value),
                [.. recorded.Value.Where(decision => decision.IsStanding)],
                candidates,
                unapproved,
                cycles.Count));
        }

        /// <summary>
        /// Whether a person has approved this cycle, and it has not been withdrawn or stopped.
        /// </summary>
        /// <remarks>
        /// <para>Read from the ledger rather than from the file's existence. The ledger is append-only and
        /// the last entry stands, so a cycle approved and then reopened is not approved — and a passage
        /// taken from one would be prose a person had explicitly sent back.</para>
        /// <para>A stopped cycle contributes nothing either. §16.1 keeps its directory because the
        /// objection that killed it is readable history, and a decision lifted out of work somebody
        /// abandoned is the opposite of a settled choice.</para>
        /// <para>An unreadable ledger reads as unapproved. It is the conservative direction: the cost is a
        /// passage not offered, where trusting it would put unsigned prose in a record whose whole value
        /// is that a person signed it.</para>
        /// </remarks>
        private static async Task<bool> IsApproved(Cycle cycle, CancellationToken cancellationToken)
        {
            if (cycle.IsAbandoned || !File.Exists(cycle.ApprovalPath))
            {
                return false;
            }

            string text = await File
                .ReadAllTextAsync(cycle.ApprovalPath, cancellationToken)
                .ConfigureAwait(false);

            Result<ApprovalLedger> ledger = ApprovalLedger.Read(text, cycle.ApprovalPath);

            return ledger.IsSuccess && ledger.Value.IsApproved;
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
                candidates.Add(new Candidate(cycle.Id, source, paragraph));
            }
        }

        /// <summary>Groups a section's lines into paragraphs, dropping the blank runs between them.</summary>
        private static IEnumerable<string> Paragraphs(IReadOnlyList<string> lines)
        {
            List<string> current = [];

            foreach (string line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    if (current.Count > 0)
                    {
                        yield return string.Join(' ', current);
                        current = [];
                    }

                    continue;
                }

                current.Add(line.Trim());
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
