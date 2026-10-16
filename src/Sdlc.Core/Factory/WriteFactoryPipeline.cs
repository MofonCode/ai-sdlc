using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Factory;

/// <summary>
/// Writes the factory's pipeline for the platform the solution records (D-84, D-91).
/// </summary>
/// <remarks>
/// <para><strong>Its own command, not part of adoption.</strong> A factory is a choice a team makes after it
/// has run cycles by hand, and the pull-request check every solution needs should not arrive bundled with a
/// nightly agent somebody did not ask for. When the factory moves to a plugin of its own, this is the command
/// it brings.</para>
/// <para><strong>The file is framework-named, so it is the framework's to rewrite</strong>, as
/// <c>sdlc-verify.yml</c> is (D-40): running the command again writes the current template over it.</para>
/// </remarks>
public static class WriteFactoryPipeline
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddWriteFactoryPipelineRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Asks for the factory's pipeline to be written.</summary>
    public sealed record Command : IRequest<Response>;

    /// <summary>What was written.</summary>
    /// <param name="Path">The file, relative to the root.</param>
    /// <param name="Rewritten">Whether a file was already there and has been replaced.</param>
    /// <param name="Platform">The platform it was written for.</param>
    public sealed record Response(string Path, bool Rewritten, Platform Platform);

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<SolutionDocument> read = await SolutionDocument.ReadFile(solution.Path, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            SolutionDocument document = read.Value;

            if (document.Platform == Platform.Local)
            {
                return Result.Fail<Response>(
                    "This solution is local: there is no platform to run a factory on, and no pull request for it to open. Give it a remote and re-run adoption with --platform first.");
            }

            string relative = FactoryPipeline.PathOf(document.Platform);
            string path = System.IO.Path.Combine(solution.Path, relative);
            bool existed = File.Exists(path);

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? solution.Path);
            await File.WriteAllTextAsync(
                path,
                FactoryPipeline.Text(document.Platform, document.IntegrationBranch, Release.Version),
                cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(relative.Replace('\\', '/'), existed, document.Platform));
        }
    }

    /// <summary>Renders what was written and what a person still has to do.</summary>
    /// <param name="written">What was written.</param>
    /// <returns>The report, ending in a newline.</returns>
    public static string Render(Response written)
    {
        ArgumentNullException.ThrowIfNull(written);

        string setup = written.Platform == Platform.AzureDevOps
            ? "Create a pipeline from it, add one secret variable for the agent -- ANTHROPIC_API_KEY, or CLAUDE_CODE_OAUTH_TOKEN from 'claude setup-token' -- let the build service contribute, create branches and contribute to pull requests, and add an Exclusive lock check to the sdlc-factory environment."
            : "Add one secret for the agent -- ANTHROPIC_API_KEY, or CLAUDE_CODE_OAUTH_TOKEN from 'claude setup-token' -- and SDLC_FACTORY_TOKEN, a token that may push and open pull requests, not GITHUB_TOKEN, whose pull requests start no workflow.";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            {(written.Rewritten ? "Rewrote" : "Wrote")} {written.Path}: the factory, nightly, one run at a time, six hours at most.
            {setup}
            It has never run. Commit it, run it once by hand against one throwaway approved cycle, and watch it.
            docs/guides/setting-up-the-factory.md walks every step.

            """);
    }
}
