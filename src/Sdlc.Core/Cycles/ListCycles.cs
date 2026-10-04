using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Cycles;

/// <summary>One cycle, as a list of them shows it.</summary>
/// <param name="Cycle">Which cycle.</param>
/// <param name="Headline">The intent's own heading, which is how a person recognises the work.</param>
/// <param name="Reached">The furthest artifact that exists.</param>
/// <param name="Rounds">How many rounds the furthest artifact has had.</param>
/// <param name="Blockers">Open blocking findings across every artifact.</param>
/// <param name="Suggestions">Open non-blocking findings across every artifact.</param>
/// <param name="Gate">Why the build may not run, or none.</param>
/// <param name="Outcome">What the verification said, when there is one.</param>
/// <param name="Abandoned">Whether the cycle was stopped, which is terminal.</param>
public sealed record CycleSummary(
    Cycle Cycle,
    string? Headline,
    ArtifactKind Reached,
    int Rounds,
    int Blockers,
    int Suggestions,
    ApprovalGate.GateRefusal Gate,
    string? Outcome,
    bool Abandoned)
{
    /// <summary>Whether a person has approved this cycle's three planning documents.</summary>
    public bool Approved => Gate == ApprovalGate.GateRefusal.None;

    /// <summary>Whether a verification passed.</summary>
    public bool Verified => string.Equals(Outcome, "pass", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Every cycle this solution holds, newest first.
/// </summary>
/// <remarks>
/// <para><strong>Newest first, always, and not configurable.</strong> A list somebody scans to find the
/// work they were doing is a list whose top is the most recent thing. An option for the other order
/// would be a second way to read one answer, and the one nobody chose would still have to be tested.</para>
/// <para><strong>Every cycle is listed, and the filters narrow rather than tidy.</strong> A default that
/// hid abandoned or stalled cycles would hide the surprise that matters most -- the cycle from last week
/// still sitting at spec -- which is the same reason the landable sweep lists cycles it will not land.</para>
/// </remarks>
public static class ListCycles
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddListCyclesRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Which cycles to list.</summary>
    /// <param name="Stage">Only cycles whose furthest artifact is this one, or null for all.</param>
    /// <param name="Approved">True for only approved cycles, false for only unapproved, null for all.</param>
    /// <param name="Verified">True for only cycles whose verification passed, null for all.</param>
    public sealed record Query(
        ArtifactKind? Stage = null,
        bool? Approved = null,
        bool? Verified = null) : IRequest<Response>;

    /// <summary>The cycles, newest first.</summary>
    /// <param name="Cycles">What matched, in reverse chronological order.</param>
    /// <param name="Examined">How many cycles the solution holds, before filtering.</param>
    public sealed record Response(IReadOnlyList<CycleSummary> Cycles, int Examined);

    /// <summary>Reads each cycle's state through the one operation that knows it.</summary>
    /// <param name="solution">The root of the solution being governed.</param>
    /// <param name="mediator">How each cycle's status is read, rather than re-deriving it here.</param>
    private sealed class Handler(SolutionRoot solution, Mediator mediator)
        : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            IReadOnlyList<Cycle> all = CycleIndex.All(solution.Path);
            List<CycleSummary> summaries = [];

            foreach (Cycle cycle in all)
            {
                // Through the status operation rather than reading the files again. Two readers of
                // "where has this cycle got to" would eventually disagree, and the one a person saw in
                // a list would not be the one that refused the build.
                Result<ReadCycleStatus.Response> read = await mediator
                    .Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(
                        new ReadCycleStatus.Query(cycle.Id),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (read.IsFailed)
                {
                    return read.ToResult<Response>();
                }

                summaries.Add(await Summarise(read.Value, cancellationToken).ConfigureAwait(false));
            }

            IEnumerable<CycleSummary> matched = summaries
                .OrderByDescending(summary => summary.Cycle.Id.ToString(), StringComparer.Ordinal);

            if (request.Stage is { } stage)
            {
                matched = matched.Where(summary => summary.Reached == stage);
            }

            if (request.Approved is { } approved)
            {
                matched = matched.Where(summary => summary.Approved == approved);
            }

            if (request.Verified is { } verified)
            {
                matched = matched.Where(summary => summary.Verified == verified);
            }

            return Result.Ok(new Response([.. matched], all.Count));
        }

        private static async Task<CycleSummary> Summarise(
            ReadCycleStatus.Response status,
            CancellationToken cancellationToken)
        {
            IEnumerable<ReadCycleStatus.ArtifactState> every = status.Verify is { } verify
                ? [.. status.Artifacts, verify]
                : status.Artifacts;

            ReadCycleStatus.ArtifactState reached = status.Artifacts
                .Where(artifact => artifact.Exists)
                .OrderBy(artifact => artifact.Kind)
                .LastOrDefault() ?? status.Artifacts[0];

            ArtifactOutline outline = await ArtifactOutlineReader
                .ReadFile(status.Cycle.PathOf(ArtifactKind.Intent), cancellationToken)
                .ConfigureAwait(false);

            return new CycleSummary(
                status.Cycle,
                outline.Headline,
                reached.Kind,
                reached.Rounds,
                every.Sum(artifact => artifact.OpenBlockers.Count),
                every.Sum(artifact => artifact.OpenSuggestions.Count),
                status.Gate.Refusal,
                status.Outcome,
                status.Cycle.IsAbandoned);
        }
    }
}
