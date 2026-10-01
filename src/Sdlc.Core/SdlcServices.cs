using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Constraints;

namespace Sdlc.Core;

/// <summary>Registers the gate's requests and their handlers.</summary>
public static class SdlcServices
{
    /// <summary>
    /// Adds the mediator and every handler the gate dispatches to.
    /// </summary>
    /// <param name="services">The collection to add to.</param>
    /// <param name="libraryPath">The framework's own <c>CONSTRAINTS.md</c>.</param>
    /// <param name="solutionRoot">The root of the solution being worked on.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddSdlc(
        this IServiceCollection services,
        string libraryPath,
        string solutionRoot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);

        services.AddSingleton<Mediator>();

        services.AddSingleton<IConstraintDocuments>(
            _ => new FileConstraintDocuments(libraryPath, solutionRoot));

        services.AddTransient<
            IRequestHandler<EffectiveConstraintsQuery, ConstraintSet>,
            EffectiveConstraintsHandler>();

        return services;
    }
}
