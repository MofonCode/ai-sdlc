using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Evidence;
using Sdlc.Core.Findings;
using Sdlc.Core.Perspectives;
using Sdlc.Core.Proposals;

namespace Sdlc.Core.Solutions;

/// <summary>
/// How a solution is set to run, every value with where it came from and how it is changed (D-138).
/// </summary>
/// <remarks>
/// <para><strong>One view, never one file.</strong> The values stay where they are, because protection is per
/// path (D-09) and each file says one kind of thing. What was missing was a place to see them together, and
/// in particular a place where a default reads as a default rather than as nothing.</para>
/// <para><strong>Read-only, and it never restates what another command owns.</strong> The rules are
/// <c>sdlc constraints</c>', the stack is <c>sdlc stack</c>'s and the decisions are <c>sdlc decisions</c>'; the
/// view names each and points at it, because a second rendering is a second thing to keep in step, and the
/// decisions alone run to hundreds of entries.</para>
/// </remarks>
public static class ReadSettings
{
    /// <summary>The variable that points the binary at another constraint library.</summary>
    /// <remarks>Held here, and read by the command-line host's locator through this name.</remarks>
    public const string LibraryVariable = "SDLC_CONSTRAINTS";

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddReadSettingsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Which part of the view a setting sits in.</summary>
    public enum SettingGroup
    {
        /// <summary>A key in <c>solution.md</c>: how this solution chooses to run.</summary>
        Solution,

        /// <summary>A thing that binds the solution, owned and shown by another command.</summary>
        Elsewhere,

        /// <summary>A value this machine's environment sets, which the repository does not state.</summary>
        Machine,

        /// <summary>A value this release fixes, with the decision that keeps it fixed.</summary>
        Fixed,
    }

    /// <summary>Where a setting's value came from.</summary>
    public enum SettingSource
    {
        /// <summary>Written in the file.</summary>
        Declared,

        /// <summary>Absent from the file, and read as its default.</summary>
        Default,

        /// <summary>Not set anywhere, and nothing reads it as anything.</summary>
        NotDeclared,

        /// <summary>Set in this machine's environment.</summary>
        Environment,

        /// <summary>Fixed by the release.</summary>
        Fixed,

        /// <summary>Shown by another command.</summary>
        Elsewhere,

        /// <summary>The file it lives in could not be read, so its value is unknown.</summary>
        Unreadable,
    }

    /// <summary>One value in force.</summary>
    /// <param name="Group">Which part of the view it sits in.</param>
    /// <param name="Name">The key, variable or value's name.</param>
    /// <param name="Value">What it is, as the person would write it.</param>
    /// <param name="Source">Where it came from.</param>
    /// <param name="Change">How a person changes it, or the decision that keeps it fixed.</param>
    public sealed record Setting(SettingGroup Group, string Name, string Value, SettingSource Source, string Change);

    /// <summary>The view.</summary>
    /// <param name="Settings">Every value, in the order the view prints them.</param>
    /// <param name="Release">The release pinned for this solution, or null when it pins none.</param>
    /// <param name="Problems">Why <c>solution.md</c> could not be read, empty when it could.</param>
    public sealed record Response(IReadOnlyList<Setting> Settings, string? Release, IReadOnlyList<string> Problems)
    {
        /// <summary>
        /// Whether every value could be resolved. False exits 1: a key that could not be read is never shown
        /// as its default, because "could not tell" must not read as the answer.
        /// </summary>
        public bool Complete => Problems.Count == 0;
    }

    /// <summary>Asks how this solution is set to run.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>Reads the solution document, the environment and the release's fixed values.</summary>
    /// <param name="solution">The root of the solution being read.</param>
    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            List<Setting> settings = [];
            List<string> problems = [];

            await Solution(settings, problems, cancellationToken).ConfigureAwait(false);

            settings.Add(new Setting(SettingGroup.Elsewhere, "rules", "every tier, resolved", SettingSource.Elsewhere, "sdlc constraints"));
            settings.Add(new Setting(SettingGroup.Elsewhere, "stack", "defaults, stack rules and build settings", SettingSource.Elsewhere, "sdlc stack"));
            settings.Add(new Setting(SettingGroup.Elsewhere, "decisions", "its own view", SettingSource.Elsewhere, "sdlc decisions"));

            // SDLC_CONSTRAINTS is always listed, set or not: it changes which rules bind, and nothing in the
            // repository says so. The development build is listed only when it is set.
            string? library = Environment.GetEnvironmentVariable(LibraryVariable);
            settings.Add(new Setting(
                SettingGroup.Machine,
                LibraryVariable,
                string.IsNullOrWhiteSpace(library) ? "(unset)" : library,
                string.IsNullOrWhiteSpace(library) ? SettingSource.NotDeclared : SettingSource.Environment,
                string.IsNullOrWhiteSpace(library)
                    ? "the library this release carries binds"
                    : "replaces the library this release carries, on this machine only"));

            if (PinnedRelease.DevBuild() is { } devBuild)
            {
                settings.Add(new Setting(
                    SettingGroup.Machine,
                    PinnedRelease.DevBuildVariable,
                    devBuild,
                    SettingSource.Environment,
                    "a local build: no cycle it judges opens a pull request (D-135)"));
            }

            settings.Add(Fixed("changed-line coverage floor", CoverageSummary.ChangedFloor.ToString("P0", CultureInfo.InvariantCulture), "D-103"));
            settings.Add(Fixed("interrogation loop", string.Create(CultureInfo.InvariantCulture, $"warns at {Oscillation.Warn} relapses, stops at {Oscillation.Halt}"), "D-65"));
            settings.Add(Fixed("remediation loop", "stops when a pass reduces nothing", "D-66"));
            settings.Add(Fixed("a constraint is proposed after", string.Create(CultureInfo.InvariantCulture, $"{ProposeConstraints.Bar} cycles cite it"), "D-69"));
            settings.Add(Fixed("always-on perspectives", string.Join(", ", Perspective.AlwaysOn), "D-90"));

            return Result.Ok(new Response(settings, PinnedRelease.Find(solution.Path), problems));
        }

        private static Setting Fixed(string name, string value, string decision) =>
            new(SettingGroup.Fixed, name, value, SettingSource.Fixed, decision);

        /// <summary>The keys of <c>solution.md</c>, each declared or defaulted, or every one unreadable.</summary>
        private async Task Solution(List<Setting> settings, List<string> problems, CancellationToken cancellationToken)
        {
            string path = SolutionDocument.PathIn(solution.Path);
            Result<SolutionDocument> read = await SolutionDocument.ReadFile(solution.Path, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                problems.AddRange(read.Errors.Select(error => error.Message));

                foreach (string key in _keys)
                {
                    settings.Add(new Setting(SettingGroup.Solution, key, "?", SettingSource.Unreadable, "sdlc doctor"));
                }

                return;
            }

            SolutionDocument document = read.Value;
            string yaml = Frontmatter.Read(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), path) is { IsSuccess: true } split
                ? split.Value.Yaml
                : string.Empty;

            SettingSource Source(string key) =>
                string.IsNullOrWhiteSpace(Frontmatter.Key(yaml, key)) ? SettingSource.Default : SettingSource.Declared;

            settings.Add(new Setting(SettingGroup.Solution, "approval", SolutionDocument.NameOf(document.Approval), Source("approval"), "sdlc draft solution"));
            settings.Add(new Setting(SettingGroup.Solution, "origin", SolutionDocument.NameOf(document.Origin), SettingSource.Declared, "fixed once recorded"));
            settings.Add(new Setting(SettingGroup.Solution, "standard", SolutionDocument.NameOf(document.Standard), Source("standard"), "sdlc draft solution --standard <existing|profile>"));
            settings.Add(new Setting(SettingGroup.Solution, "platform", SolutionDocument.NameOf(document.Platform), Source("platform"), "sdlc draft solution"));
            settings.Add(new Setting(SettingGroup.Solution, "integration_branch", document.IntegrationBranch, Source("integration_branch"), "sdlc draft solution"));
            settings.Add(new Setting(
                SettingGroup.Solution,
                "telemetry",
                document.Telemetry ?? "(none)",
                document.Telemetry is null ? SettingSource.NotDeclared : SettingSource.Declared,
                "sdlc draft solution"));
        }

        private static readonly string[] _keys = ["approval", "origin", "standard", "platform", "integration_branch", "telemetry"];
    }
}
