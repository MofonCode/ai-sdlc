using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;

namespace Sdlc.Core.Constraints;

/// <summary>Resolves the constraint set in force, across every tier, for one artifact or for all.</summary>
public static class EffectiveConstraints
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddEffectiveConstraintsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>The constraints in force, in resolution order, with what was read to find them.</summary>
    /// <param name="Constraints">The constraints that bind, global first, then profile, then solution.</param>
    /// <param name="Sources">The documents the set was assembled from.</param>
    /// <param name="Superseded">
    /// Constraints found but no longer binding. Kept so a reader can see that a rule existed and was
    /// replaced, rather than finding a gap in the numbering and nothing else.
    /// </param>
    public sealed record Response(
        IReadOnlyList<Constraint> Constraints,
        IReadOnlyList<ConstraintDocument> Sources,
        IReadOnlyList<Constraint> Superseded);

    /// <summary>
    /// Asks for the constraint set in force, optionally narrowed to one artifact.
    /// </summary>
    /// <remarks>
    /// This is how the rules reach a session on any host. Every skill that judges or writes an artifact
    /// runs it first, so there is one source and one format rather than a per-host copy of the rules —
    /// per-host injection is an optimisation layered over this, never the mechanism.
    /// </remarks>
    /// <param name="Artifact">
    /// The artifact to narrow to — <c>intent</c>, <c>spec</c>, <c>plan</c>, <c>diff</c>, <c>verify</c>.
    /// Null returns everything in force.
    /// </param>
    public sealed record Query(string? Artifact = null) : IRequest<Response>;

    /// <summary>Assembles the effective set from the tier documents.</summary>
    /// <param name="documents">The tier documents to read.</param>
    private sealed class Handler(IConstraintDocuments documents)
        : IRequestHandler<Query, Response>
    {
        /// <summary>
        /// Applies when a constraint's <c>Applies to</c> clause names every artifact rather than one.
        /// </summary>
        private const string EveryArtifact = "every artifact";

        /// <inheritdoc />
        public async Task<Result<Response>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            IReadOnlyList<ConstraintDocument> sources =
                await documents.Enumerate(cancellationToken).ConfigureAwait(false);

            List<Constraint> inForce = [];
            List<Constraint> superseded = [];
            List<IError> errors = [];

            foreach (ConstraintDocument document in sources)
            {
                Result<IReadOnlyList<Constraint>> parsed =
                    ConstraintParser.Parse(document.Text, document.Source);

                if (parsed.IsFailed)
                {
                    errors.AddRange(parsed.Errors);
                    continue;
                }

                foreach (Constraint constraint in parsed.Value)
                {
                    if (constraint.IsInForce)
                    {
                        inForce.Add(constraint);
                    }
                    else
                    {
                        superseded.Add(constraint);
                    }
                }
            }

            if (errors.Count > 0)
            {
                return Result.Fail<Response>(errors);
            }

            List<Constraint> narrowed = request.Artifact is { } artifact
                ? inForce.FindAll(constraint => Applies(constraint, artifact))
                : inForce;

            return Result.Ok(new Response(narrowed, sources, superseded));
        }

        /// <summary>
        /// Whether a constraint binds the named artifact, read from its <c>Applies to</c> clause.
        /// </summary>
        /// <remarks>
        /// A substring match on the clause, plus anything that says it binds every artifact. The clause
        /// is prose written for a person — "an intent, at approval" — so this reads what is already
        /// there rather than requiring every constraint to carry a second machine-readable copy of it,
        /// which two places would then have to keep in agreement.
        /// </remarks>
        private static bool Applies(Constraint constraint, string artifact) =>
            constraint.AppliesTo.Contains(artifact, StringComparison.OrdinalIgnoreCase)
            || constraint.AppliesTo.Contains(EveryArtifact, StringComparison.OrdinalIgnoreCase);
    }
}
