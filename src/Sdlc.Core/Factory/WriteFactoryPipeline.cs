using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Chain;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Factory;

/// <summary>
/// Writes the factory's pipeline for the platform the solution records (D-84, D-91).
/// </summary>
/// <remarks>
/// <para><strong>Its own command, not part of adoption.</strong> A factory is a choice a team makes after it
/// has run cycles by hand, and the pull-request check every solution needs should not arrive bundled with a
/// build agent somebody did not ask for. When the factory moves to a plugin of its own, this is the command
/// it brings.</para>
/// <para><strong>The file is framework-named, so it is the framework's to rewrite</strong>, as
/// <c>sdlc-verify.yml</c> is (D-40): running the command again writes the current template over it.</para>
/// <para><strong>Kept, when asked and it still holds</strong> (D-201). A team's factory carries its own pool and steps
/// beside the framework's, and a rewrite drops them. With <c>--keep</c>, as the upgrade pipeline runs it, a file that
/// already carries every step this release writes, by the upgrade check's own measure, is left as it is; one that
/// does not is rewritten, and what of the team's that loses is named.</para>
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
    /// <param name="Keep">Leave a file that already carries every step this release writes as it is.</param>
    public sealed record Command(bool Keep = false) : IRequest<Response>;

    /// <summary>What was written.</summary>
    /// <param name="Path">The file, relative to the root.</param>
    /// <param name="Rewritten">Whether a file was already there and has been replaced.</param>
    /// <param name="Platform">The platform it was written for.</param>
    /// <param name="Kept">Whether the file was left as it is, because it already carries this release's steps.</param>
    /// <param name="Lost">Each pool of the team's own the rewrite replaced; empty when there was none.</param>
    public sealed record Response(string Path, bool Rewritten, Platform Platform, bool Kept = false, IReadOnlyList<string>? Lost = null);

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
            string slashed = relative.Replace('\\', '/');
            string template = FactoryPipeline.Text(document.Platform, document.IntegrationBranch);
            string? existing = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : null;

            // The file as its own base: every step that is not the release's is the team's, carried unchanged, so it
            // passes exactly when no step this release writes is missing, changed or out of order.
            if (request.Keep && existing is not null && VerifyUpgrade.Shaped(slashed, template, existing, existing).Passed)
            {
                return Result.Ok(new Response(slashed, false, document.Platform, Kept: true));
            }

            IReadOnlyList<string> lost = existing is null ? [] : VerifyUpgrade.OwnPools(template, existing);

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? solution.Path);
            await File.WriteAllTextAsync(path, template, cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(slashed, existing is not null, document.Platform, Lost: lost));
        }
    }

    /// <summary>Renders what was written and what a person still has to do.</summary>
    /// <param name="written">What was written.</param>
    /// <returns>The report, ending in a newline.</returns>
    public static string Render(Response written)
    {
        ArgumentNullException.ThrowIfNull(written);

        if (written.Kept)
        {
            return $"Kept {written.Path} as it is: it already carries every step this release writes, beside the team's own.\n";
        }

        string lost = written.Lost is { Count: > 0 } pools
            ? "It replaced the team's own pool (" + string.Join(", ", pools) + "): put it back, and any step of the team's own, from 'git diff'.\n"
            : string.Empty;

        string setup = written.Platform == Platform.AzureDevOps
            ? "Create a pipeline from it, add one secret variable for the agent -- ANTHROPIC_API_KEY, or CLAUDE_CODE_OAUTH_TOKEN from 'claude setup-token' -- let the build service contribute, create branches and contribute to pull requests, and add an Exclusive lock check to the sdlc-factory environment."
            : "Add one secret for the agent -- ANTHROPIC_API_KEY, or CLAUDE_CODE_OAUTH_TOKEN from 'claude setup-token' -- and SDLC_FACTORY_TOKEN, a token that may push and open pull requests, not GITHUB_TOKEN, whose pull requests start no workflow.";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            {(written.Rewritten ? "Rewrote" : "Wrote")} {written.Path}: the factory, run when a person starts it, one run at a time, six hours at most.
            {setup}
            {lost}It has never run. Commit it, run it once by hand against one throwaway approved cycle, and watch it.
            docs/guides/setting-up-the-factory.md walks every step.

            """);
    }
}
