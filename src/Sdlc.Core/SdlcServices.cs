using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;

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

        // The three cycle handlers take the solution root rather than resolving it themselves, so a
        // test can point them at a scratch directory without an environment variable deciding it.
        services.AddTransient<IRequestHandler<ApprovalGateQuery, GateVerdict>>(
            _ => new ApprovalGateHandler(solutionRoot));

        services.AddTransient<IRequestHandler<CycleStatusQuery, CycleStatus>>(
            _ => new CycleStatusHandler(solutionRoot));

        services.AddTransient<IRequestHandler<ApprovalTextQuery, ApprovalTextResult>>(
            _ => new ApprovalTextHandler(solutionRoot));

        return services;
    }
}
