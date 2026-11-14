using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Factory;
using Sdlc.Core.Git;
using Sdlc.Core.Init;

namespace Sdlc.Core.Solutions;

/// <summary>
/// Everything that moves when a solution takes another release, in the order it moves, with the release each
/// piece is on now (D-170).
/// </summary>
/// <remarks>
/// <para><strong>Six places, moved by hand, four times in four days.</strong> The machine's tool, the plugin, the
/// solution's pin and what adoption writes, the factory's images, and the cycles already open each have a page
/// that says how, and none says which come first or which apply here. A person asked "what do we need to do".
/// The facts are all on disk or in git, so the answer is the binary's (§15.2).</para>
/// <para><strong>It runs nothing</strong>, as <c>tidy</c> does: an upgrade lands by pull request (D-133), and
/// which commands to run is a person's call.</para>
/// <para><strong>The machine's release answers, never the pinned one.</strong> The command is not handed on (D-131):
/// asked inside a solution, the pinned release would describe an upgrade to itself.</para>
/// </remarks>
public static class UpgradeChecklist
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddUpgradeChecklistRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks what moving to a release involves here.</summary>
    /// <param name="Target">The release to move to; null for the one this machine runs.</param>
    /// <param name="Machine">The release this machine runs.</param>
    public sealed record Query(string? Target, string Machine) : IRequest<Response>;

    /// <summary>A cycle whose branch still pins an older release.</summary>
    /// <param name="Branch">The branch.</param>
    /// <param name="Pinned">The release it pins.</param>
    public sealed record Behind(string Branch, string Pinned);

    /// <summary>What moves, and from where.</summary>
    /// <param name="Target">The release to move to.</param>
    /// <param name="Machine">The release this machine runs.</param>
    /// <param name="Pinned">The release the solution pins on this branch, or null when it pins none.</param>
    /// <param name="Integration">The branch the upgrade lands on.</param>
    /// <param name="Factory">The factory pipeline this solution carries, or null when it has no factory.</param>
    /// <param name="Cycles">The open cycle branches pinning an older release than the target.</param>
    public sealed record Response(
        string Target,
        string Machine,
        string? Pinned,
        string Integration,
        string? Factory,
        IReadOnlyList<Behind> Cycles)
    {
        /// <summary>Whether this machine runs an older release than the target, so it moves first.</summary>
        public bool MachineFirst => IsOlder(Machine, Target);

        /// <summary>Whether the solution's pin is older than the target.</summary>
        public bool PinMoves => Pinned is null || IsOlder(Pinned, Target);
    }

    /// <summary>Whether one release is older than another: by major, minor and patch, then a prerelease before its release.</summary>
    /// <param name="release">The release asked about.</param>
    /// <param name="than">The one it is compared with.</param>
    /// <returns>True when <paramref name="release"/> is the older.</returns>
    public static bool IsOlder(string release, string than)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(than);

        if (!Version.TryParse(PinnedRelease.Core(release), out Version? left) || !Version.TryParse(PinnedRelease.Core(than), out Version? right))
        {
            return false;
        }

        return left < right || (left == right && release.Contains('-', StringComparison.Ordinal) && !than.Contains('-', StringComparison.Ordinal));
    }

    private sealed class Handler(SolutionRoot solution, IRepository repository) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string target = request.Target ?? request.Machine;

            // A local build is never pinned or published (D-135), so it is no release to move a solution to.
            if (PinnedRelease.IsLocal(target))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"This machine runs a local build ({target}), which no solution pins. Name the release to move to: 'sdlc upgrade <release>'."));
            }

            if (!Version.TryParse(PinnedRelease.Core(target), out _))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{target}' is not a release. Name one such as 0.46.0, as nuget.org lists MofonCode.Sdlc.Cli."));
            }

            string root = solution.Path;
            string? pinned = PinnedRelease.PinnedIn(File.Exists(Path.Combine(root, PinnedRelease.ManifestPath))
                ? await File.ReadAllTextAsync(Path.Combine(root, PinnedRelease.ManifestPath), cancellationToken).ConfigureAwait(false)
                : null);
            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);

            string? factory = new[] { Platform.GitHub, Platform.AzureDevOps }
                .Select(FactoryPipeline.PathOf)
                .FirstOrDefault(path => File.Exists(Path.Combine(root, path)))
                ?.Replace('\\', '/');

            return Result.Ok(new Response(target, request.Machine, pinned, integration, factory, Cycles(root, integration, target)));
        }

        // A cycle keeps the release its branch pins until that branch takes the upgrade (D-131), so each open one
        // pinning an older release is named. A branch whose work has reached the integration branch is tidy's.
        private List<Behind> Cycles(string root, string integration, string target)
        {
            if (!repository.IsRepository(root)
                || new[] { "origin/" + integration, integration }.FirstOrDefault(name => repository.RefExists(root, name)) is not { } at)
            {
                return [];
            }

            List<Behind> behind = [];

            foreach (string branch in repository.Branches(root, CycleId.BranchPrefix)
                .Where(name => !name.StartsWith("origin/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal))
            {
                if (TidyCycles.Done(repository, root, at, branch) is not null)
                {
                    continue;
                }

                if (PinnedRelease.PinnedIn(repository.ReadAt(root, branch, PinnedRelease.ManifestPath)) is { } pin && IsOlder(pin, target))
                {
                    behind.Add(new Behind(branch, pin));
                }
            }

            return behind;
        }
    }
}
