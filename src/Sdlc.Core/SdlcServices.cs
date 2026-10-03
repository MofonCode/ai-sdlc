using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Chain;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Drafts;
using Sdlc.Core.Evidence;
using Sdlc.Core.Findings;
using Sdlc.Core.Git;
using Sdlc.Core.Hooks;
using Sdlc.Core.Init;
using Sdlc.Core.Landing;
using Sdlc.Core.Solutions;

namespace Sdlc.Core;

/// <summary>Registers the gate's requests and their handlers.</summary>
public static class SdlcServices
{
    /// <summary>
    /// Adds the mediator and every handler the gate dispatches to.
    /// </summary>
    /// <remarks>
    /// <para><strong>Every handler is registered by type, and nothing is captured in a closure.</strong>
    /// That is a deliberate property rather than a style, and it took a <see cref="SolutionRoot"/>
    /// to get: while the root was a bare <c>string</c> parameter, fourteen of these had to be
    /// factories closing over it, and two of those also closed over <see cref="TimeProvider"/> --
    /// which meant no caller could substitute a clock through the container at all.</para>
    ///
    /// <para>What by-type registration buys: a handler can be <c>private</c> inside its operation's
    /// holder, because the registration names the type from inside that holder, where it is the only
    /// thing that can see it. A factory written out here could not.</para>
    ///
    /// <para>It also means a test substitutes a dependency the way the application composes one --
    /// register the fake and dispatch -- so the test proves the wiring as well as the handler.
    /// Constructing a handler directly proves only the second.</para>
    /// </remarks>
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

        // The two things every handler resolves rather than receives. TryAdd on both, so a host or a
        // test that registered its own -- a fixed clock, a scratch directory -- keeps it.
        services.TryAddSingleton(new SolutionRoot(solutionRoot));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRepository, GitRepository>();

        // Still a factory, and the one that should be: the library is a property of the framework's
        // installation rather than of the solution, so it is not a value a handler resolves. TryAdd
        // like the rest, so a caller holding documents of its own -- a test with a stub, a host
        // reading them from somewhere else -- keeps them and this never touches the disk.
        services.TryAddSingleton<IConstraintDocuments>(
            _ => new FileConstraintDocuments(libraryPath, solutionRoot));

        services.AddEffectiveConstraintsRequestHandler();

        services.AddApprovalGateRequestHandler();

        services.AddReadCycleStatusRequestHandler();

        services.AddApprovalTextRequestHandler();

        // The turn-end gate. Unlike the pre-tool policy, which is a pure function over a tool call
        // and stays static for the sake of the hook's start-up cost, this one reads git and the
        // recorded evidence and decides -- so it is a real operation and goes through the mediator.
        services.AddTurnEndGateRequestHandler();

        // The drafters. Each prints what belongs in a file only a person writes, and none of them opens
        // that file for writing -- which is what lets every hook rule stay absolute.
        services.AddAbandonTextRequestHandler();

        services.AddConstraintTextRequestHandler();

        // Self-registering, inside the operation's own holder. That is not a style choice: the
        // handler is private in there, so nothing written out here could name it.
        services.AddProfileTextRequestHandler();

        services.AddVerifyChainRequestHandler();

        services.AddSuggestionsRequestHandler();

        services.AddInitialiseRequestHandler();
        services.AddScaffoldSolutionRequestHandler();

        services.AddNewCycleRequestHandler();

        services.AddComputeClaimGraphRequestHandler();

        services.AddLandRequestHandler();

        services.AddRecordEvidenceRequestHandler();

        return services;
    }
}
