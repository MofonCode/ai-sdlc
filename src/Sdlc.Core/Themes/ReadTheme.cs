using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Themes;

/// <summary>
/// Reads the theme a solution's surfaces use. Reads; never writes.
/// </summary>
/// <remarks>
/// What a stage runs before it builds a page or a report, so a colour or a typeface comes from the one
/// place they live rather than from whatever the last file used. The same job <c>sdlc constraints</c>
/// does for the rules and <c>sdlc decisions</c> for the settled choices — rendered on demand, never
/// copied into an artifact.
/// <para>
/// A solution with no front-end surface has no theme and that is not a failure. §2.1's rule applies:
/// a check that claims to verify a UI solution has one would have to decide what counts as a UI, which
/// nothing here can do — so absence is reported and never judged.
/// </para>
/// </remarks>
public static class ReadTheme
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddReadThemeRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks for this solution's theme.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>
    /// What the theme holds.
    /// </summary>
    /// <param name="Theme">The theme, or null when the solution has none.</param>
    /// <param name="Path">Where it lives, whether or not it is there yet.</param>
    public sealed record Response(Theme? Theme, string Path)
    {
        /// <summary>Whether the solution has a theme at all.</summary>
        public bool Recorded => Theme is not null;
    }

    /// <param name="solution">The root of the solution being read.</param>
    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string path = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, Theme.FileName);

            if (!File.Exists(path))
            {
                return Result.Ok(new Response(null, path));
            }

            string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            Result<Theme> read = Theme.Read(json, path);

            return read.IsFailed
                ? read.ToResult<Response>()
                : Result.Ok(new Response(read.Value, path));
        }
    }
}
