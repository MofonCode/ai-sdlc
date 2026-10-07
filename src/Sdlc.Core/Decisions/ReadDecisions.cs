using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Decisions;

/// <summary>
/// Renders the choices this solution has already settled. Reads; never writes.
/// </summary>
/// <remarks>
/// <para><strong>This is the half the record exists for.</strong> A constraint stops a cycle doing
/// something; a decision stops a cycle re-deciding something. Cycle forty cannot see that cycle seven
/// chose an outbox over two-phase commit and why, because the reasoning is in one plan's
/// <c>Shape and departures</c> and nothing reads across cycles. So this is what a stage runs to find out
/// what was already argued — the same job <c>sdlc constraints</c> does for the rules.</para>
///
/// <para><strong>Rendered on demand and never copied into an artifact.</strong> Same reason the
/// constraint set is: a copy is a second place the truth lives, and <c>CON_09</c> forbids an artifact
/// restating another document's content as its own authority.</para>
///
/// <para><strong>It binds nothing.</strong> A plan may depart from any of these. What it may not do is
/// depart without knowing it had — and that is what reading this prevents, rather than any rule.
/// <c>CON_15</c> does not reach these entries: its <c>Forbids</c> clause names the declared stack
/// specifically.</para>
/// </remarks>
public static class ReadDecisions
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddReadDecisionsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>
    /// Asks what this solution has already decided.
    /// </summary>
    /// <param name="IncludeSuperseded">
    /// Whether to carry the reversed entries too. False for a stage, which wants what stands; true for
    /// somebody reading history, who needs the entry a cycle cited even after it was replaced.
    /// </param>
    public sealed record Query(bool IncludeSuperseded = false) : IRequest<Response>;

    /// <summary>
    /// What the record holds.
    /// </summary>
    /// <param name="Decisions">
    /// The entries, in document order. Standing only unless the query asked for the rest.
    /// </param>
    /// <param name="Superseded">How many entries are on the page and no longer current.</param>
    /// <param name="Recorded">Whether the solution has a decision document at all.</param>
    public sealed record Response(
        IReadOnlyList<Decision> Decisions,
        int Superseded,
        bool Recorded);

    /// <summary>
    /// Reads the document, or reports that there is none.
    /// </summary>
    /// <remarks>
    /// An absent file is not a failure. Most solutions will not have one for a while, and a command that
    /// refused would make "nothing settled yet" indistinguishable from a broken install.
    /// </remarks>
    /// <param name="solution">The root of the solution being read.</param>
    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string path = Path.Combine(
                solution.Path,
                Cycle.SdlcDirectoryName,
                DecisionParser.FileName);

            if (!File.Exists(path))
            {
                return Result.Ok(new Response([], 0, Recorded: false));
            }

            string markdown = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            Result<IReadOnlyList<Decision>> read = DecisionParser.Parse(markdown, path);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            IReadOnlyList<Decision> all = read.Value;

            return Result.Ok(new Response(
                request.IncludeSuperseded ? all : [.. all.Where(decision => decision.IsStanding)],
                all.Count(decision => !decision.IsStanding),
                Recorded: true));
        }
    }
}
