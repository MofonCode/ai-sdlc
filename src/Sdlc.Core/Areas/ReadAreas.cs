using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Areas;

/// <summary>
/// What each of the solution's areas covers today, and which entries carry it. Reads; never writes (D-150).
/// </summary>
/// <remarks>
/// Worked out on every read and stored nowhere: which files an area covers changes whenever a file moves, and
/// which entries carry it whenever a person places a draft, so a stored list would be a second copy that
/// drifts the first time either happens -- the reason a suggestion's status is worked out too (D-115).
/// </remarks>
public static class ReadAreas
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddReadAreasRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>One area as it stands.</summary>
    /// <param name="Area">The area.</param>
    /// <param name="Files">How many files in the tree it covers; zero is the failure to fix.</param>
    /// <param name="Entries">The standing decisions and solution rules in force that carry it.</param>
    public sealed record Standing(Area Area, int Files, IReadOnlyList<string> Entries);

    /// <summary>An entry naming an area the file does not define.</summary>
    /// <param name="Entry">The entry's id.</param>
    /// <param name="Name">The name it gives.</param>
    public sealed record Unknown(string Entry, string Name);

    /// <summary>Asks what the areas cover.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>The areas, what they cover and carry, and what is wrong.</summary>
    /// <param name="Recorded">Whether the solution has an area file at all.</param>
    /// <param name="Areas">Each area, in the file's order.</param>
    /// <param name="Unknowns">Entries naming an area nobody defined. Shown in full everywhere until fixed.</param>
    /// <param name="Global">How many entries carry no area, and so reach every reader in full.</param>
    public sealed record Response(bool Recorded, IReadOnlyList<Standing> Areas, IReadOnlyList<Unknown> Unknowns, int Global);

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string path = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, AreaParser.FileName);
            Result<IReadOnlyList<Area>> defined = await AreaScope.ReadDefined(solution.Path, cancellationToken).ConfigureAwait(false);

            if (defined.IsFailed)
            {
                return defined.ToResult<Response>();
            }

            List<(string Id, IReadOnlyList<string> Areas)> entries = [];
            string decisions = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, DecisionParser.FileName);
            string rules = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, FileConstraintDocuments.SolutionConstraintsFileName);

            if (File.Exists(rules))
            {
                entries.AddRange((ConstraintParser.Parse(await File.ReadAllTextAsync(rules, cancellationToken).ConfigureAwait(false), rules).ValueOrDefault ?? [])
                    .Where(rule => rule.IsInForce)
                    .Select(rule => (rule.Id.ToString(), rule.InAreas)));
            }

            if (File.Exists(decisions))
            {
                entries.AddRange((DecisionParser.Parse(await File.ReadAllTextAsync(decisions, cancellationToken).ConfigureAwait(false), decisions).ValueOrDefault ?? [])
                    .Where(decision => decision.IsStanding)
                    .Select(decision => (decision.Id.ToString(), decision.InAreas)));
            }

            IReadOnlyList<string> tree = defined.Value.Count == 0 ? [] : AreaScope.Tree(solution.Path);
            HashSet<string> names = new(defined.Value.Select(area => area.Name), StringComparer.Ordinal);

            return Result.Ok(new Response(
                File.Exists(path),
                [.. defined.Value.Select(area => new Standing(
                    area,
                    tree.Count(area.Covers),
                    [.. entries.Where(entry => entry.Areas.Contains(area.Name)).Select(entry => entry.Id)]))],
                [.. entries.SelectMany(entry => entry.Areas
                    .Where(name => !names.Contains(name))
                    .Select(name => new Unknown(entry.Id, name)))],
                entries.Count(entry => entry.Areas.Count == 0)));
        }
    }
}
