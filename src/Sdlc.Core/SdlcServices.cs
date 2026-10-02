using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Drafts;
using Sdlc.Core.Solutions;

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

        // The drafters. Each prints what belongs in a file only a person writes, and none of them opens
        // that file for writing -- which is what lets every hook rule stay absolute.
        services.AddTransient<IRequestHandler<AbandonTextQuery, DraftResult>>(
            _ => new AbandonTextHandler(solutionRoot));

        services.AddTransient<IRequestHandler<ConstraintTextQuery, DraftResult>>(
            _ => new ConstraintTextHandler(solutionRoot));

        services.AddTransient<IRequestHandler<ProfileTextQuery, DraftResult>>(
            _ => new ProfileTextHandler(solutionRoot));

        return services;
    }
}
