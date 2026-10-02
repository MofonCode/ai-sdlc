using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using FluentResults;
using Sdlc.Core;

namespace Sdlc.Cli;

/// <summary>
/// The container and the paths one invocation needs.
/// </summary>
/// <remarks>
/// Every command needs the same three things — where the solution is, where the library is, and a
/// mediator to dispatch through — so they are resolved in one place. A command that built its own
/// would be free to resolve the solution root differently, and two commands disagreeing about which
/// solution they are governing is a defect with no obvious symptom.
/// <para>
/// Opened through <see cref="Run"/> rather than returned from a factory, so the container's lifetime
/// belongs to the call rather than to whoever remembers to dispose it.
/// </para>
/// </remarks>
internal sealed class Workspace
{
    private readonly ServiceProvider _provider;

    private Workspace(ServiceProvider provider, string solutionRoot)
    {
        _provider = provider;
        SolutionRoot = solutionRoot;
    }

    /// <summary>The root of the solution being governed.</summary>
    public string SolutionRoot { get; }

    /// <summary>
    /// Opens a workspace, runs <paramref name="body"/> in it, and closes it.
    /// </summary>
    /// <param name="line">The command line, read for <c>--solution</c>.</param>
    /// <param name="body">What to do with the workspace.</param>
    /// <returns>What <paramref name="body"/> returned, or a failing exit code when the library is missing.</returns>
    public static async Task<int> Run(CommandLine line, Func<Workspace, Task<int>> body)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(body);

        string solutionRoot = line.TryOption("solution", out string? root)
            ? Path.GetFullPath(root)
            : Directory.GetCurrentDirectory();

        if (!Locate.Library(out string? libraryPath, out string? why))
        {
            await Console.Error.WriteLineAsync(why).ConfigureAwait(false);
            return ExitCode.Failed;
        }

        ServiceProvider _provider = new ServiceCollection()
            .AddSdlc(libraryPath, solutionRoot)
            .BuildServiceProvider();

        await using (_provider.ConfigureAwait(false))
        {
            return await body(new Workspace(_provider, solutionRoot)).ConfigureAwait(false);
        }
    }

    /// <summary>Dispatches a request through the mediator.</summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">What it produces.</typeparam>
    /// <param name="request">The request.</param>
    /// <returns>The handler's result.</returns>
    public Task<Result<TResponse>> Send<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse> =>
        _provider.GetRequiredService<Mediator>().Send<TRequest, TResponse>(request);
}
