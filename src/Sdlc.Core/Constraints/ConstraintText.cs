using System.Globalization;
using FluentResults;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Drafts;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Constraints;

/// <summary>Asks for the skeleton of a new solution constraint.</summary>
/// <remarks>
/// Takes no statement. A constraint's wording is the thing being decided, and a tool that accepted one
/// on the command line would be proposing a rule rather than making room for one.
/// </remarks>
public sealed record ConstraintTextQuery : IRequest<DraftResult>;

/// <summary>
/// Prints the next free solution constraint id, with its four required parts empty. Writes nothing.
/// </summary>
/// <remarks>
/// A cycle judged by the constraints it is changing is the same deadlock as the framework governing
/// itself, so <c>constraints.md</c> is edited by a person, by hand. What a tool can do without taking
/// the decision is the bookkeeping: read what is already there, and say which id is free.
/// </remarks>
/// <param name="solution">The root of the solution being governed.</param>
public sealed class ConstraintTextHandler(SolutionRoot solution)
    : IRequestHandler<ConstraintTextQuery, DraftResult>
{
    // Shown only when the file does not exist yet, so a first constraint lands in a document that
    // explains itself rather than one holding a bare heading.
    private static readonly string[] Header =
    [
        string.Empty,
        "    # This solution's own constraints",
        string.Empty,
        "    Rules that bind this solution and no other. Resolved after the global and profile tiers,",
        "    so these are the last word.",
        string.Empty,
        "    ---",
    ];

    /// <inheritdoc />
    public async Task<Result<DraftResult>> Handle(
        ConstraintTextQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string path = Path.Combine(
            solution.Path,
            Cycle.SdlcDirectoryName,
            FileConstraintDocuments.SolutionConstraintsFileName);

        bool exists = File.Exists(path);
        IReadOnlyList<Constraint> existing = [];

        if (exists)
        {
            string markdown = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            Result<IReadOnlyList<Constraint>> read = ConstraintParser.Parse(markdown, path);

            // A malformed file is reported rather than worked around. Allocating an id from a document
            // this cannot read would risk handing back one that is already taken, and a reused id is the
            // one mistake the monotonic rule exists to make impossible.
            if (read.IsFailed)
            {
                return read.ToResult<DraftResult>();
            }

            existing = read.Value;
        }

        // Ids are a monotonic sequence: assigned once, never reused, never backfilled. So this takes one
        // past the highest ever used rather than the first gap -- a gap is the record of a constraint
        // that was removed, and filling it would make a commit citing the old id read as the new rule.
        int next = existing.Count == 0 ? 1 : existing.Max(constraint => constraint.Id.Number) + 1;
        var id = ConstraintId.Of(ConstraintTier.Solution, next);

        string entry = string.Join(
            Environment.NewLine,
            [
                string.Create(CultureInfo.InvariantCulture, $"## {id} — <the rule, in one line>"),
                string.Empty,
                "**Applies to:** <which artifact, and at what moment>",
                string.Empty,
                "**Forbids:** <what becomes illegal, named concretely enough that an example could be written>",
                string.Empty,
                "**Why:** <what goes wrong if the rule is absent - the cost it avoids, never a restatement>",
            ]) + Environment.NewLine;

        string instructions = string.Join(
            Environment.NewLine,
            [
                exists
                    ? string.Create(CultureInfo.InvariantCulture, $"Open {path} and add the block above at the end.")
                    : string.Create(CultureInfo.InvariantCulture, $"Create {path} with the header below, then the block above."),
                .. exists ? [] : Header,
                string.Empty,
                existing.Count == 0
                    ? string.Create(CultureInfo.InvariantCulture, $"{id} is the first solution constraint.")
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"{id} is the next free id. {existing.Count} already exist, the highest being {existing.Max(constraint => constraint.Id.Number):00}."),
                "Ids are assigned once and never reused, so this is one past the highest ever used rather",
                "than the first gap. A gap is the record of a constraint that was removed, and filling it",
                "would make an old finding's citation read as the new rule.",
                string.Empty,
                "Three tests, all of which have to pass, or the rule is decoration that costs attention at",
                "every interrogation:",
                string.Empty,
                "    1. its Forbids clause names something that becomes illegal",
                "    2. no analyzer, schema or hook could decide it",
                "    3. it is incompatible with at least one plausible implementation",
                string.Empty,
                "Test two is the one that catches most candidates. A rule about code belongs in an",
                "analyzer, and where an existing analyzer already decides it, use that analyzer rather",
                "than writing a rule. A mechanical rule about an artifact belongs in a schema or a hook.",
                string.Empty,
                "Nothing here writes that file. The hook denies every agent write to it, because a cycle",
                "judged by the constraints it is changing is the same deadlock as the framework governing",
                "itself.",
            ]);

        return Result.Ok(DraftResult.Drafted(entry, instructions));
    }
}
