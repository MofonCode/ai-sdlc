using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core;
using Sdlc.Core.Diagnostics;
using Sdlc.Core.Init;

namespace Sdlc.Tests;

/// <summary>
/// The gate as the application composes it, pointed at a scratch solution.
/// </summary>
/// <remarks>
/// <para><strong>Tests dispatch through this rather than constructing a handler.</strong>
/// Constructing one proves the handler works and proves nothing about whether it is wired; going
/// through <see cref="SdlcServices.AddSdlc"/> and the mediator proves both. The failure that costs
/// is the second kind — a handler nothing can reach is a handler that throws on the first request,
/// and it compiles perfectly.</para>
///
/// <para>It is also what the operation shape requires. A handler lives <c>private</c> inside its
/// operation's holder, so there is nothing here to construct even if a test wanted to.</para>
///
/// <para><strong>Substitutions are registered before <c>AddSdlc</c>, deliberately.</strong> The
/// container registers the solution root, the clock and git with <c>TryAdd</c>, so whatever is
/// already there wins — which means a test says what it is substituting and nothing else, and says
/// it in the order a host would.</para>
/// </remarks>
internal sealed class Gate : IDisposable
{
    private readonly ServiceProvider provider;

    private Gate(ServiceProvider provider) => this.provider = provider;

    /// <summary>Builds the gate over a scratch solution.</summary>
    /// <param name="solutionRoot">The solution to govern.</param>
    /// <param name="substitute">
    /// What to register ahead of the real thing — a fake git, a fixed clock. Runs before
    /// <c>AddSdlc</c>, whose <c>TryAdd</c> registrations then defer to it.
    /// </param>
    /// <returns>The gate, which owns the container until it is disposed.</returns>
    public static Gate For(string solutionRoot, Action<IServiceCollection>? substitute = null)
    {
        ServiceCollection services = new();

        // Never the platform's CLI: the suite needs no network and no sign-in. Registered first, so a test
        // that substitutes its own answer wins, the way the last registration of a service always does.
        services.AddSingleton<IBranchPolicies>(new UnaskedBranchPolicies());
        substitute?.Invoke(services);

        return new Gate(services
            .AddSdlc(RepoPaths.Library, solutionRoot)
            .BuildServiceProvider());
    }

    /// <summary>Dispatches a request that produces a response.</summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">What it produces.</typeparam>
    /// <param name="request">The request.</param>
    /// <returns>The handler's result, through the behaviour pipeline.</returns>
    public Task<Result<TResponse>> Send<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse> =>
        provider.GetRequiredService<Mediator>().Send<TRequest, TResponse>(request);

    /// <summary>Resolves anything the container holds, for the few tests that need the thing itself.</summary>
    /// <typeparam name="T">What to resolve.</typeparam>
    /// <returns>The instance.</returns>
    public T Resolve<T>()
        where T : notnull => provider.GetRequiredService<T>();

    /// <inheritdoc />
    public void Dispose() => provider.Dispose();
}

/// <summary>The platform as it is to a person who has not signed in, which is what every test gets by default.</summary>
internal sealed class UnaskedBranchPolicies : IBranchPolicies
{
    public Task<PolicyAnswer> Ask(Platform platform, string branch, string pipelineFile, string workingDirectory, CancellationToken cancellationToken) =>
        Task.FromResult(new PolicyAnswer(PolicyFinding.Unasked, "'gh' is not signed in"));
}
