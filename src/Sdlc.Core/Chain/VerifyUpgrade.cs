using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Factory;
using Sdlc.Core.Git;
using Sdlc.Core.Hooks;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Chain;

/// <summary>
/// The pull-request check on a change that moves a solution to another release and does nothing else (D-133).
/// </summary>
/// <remarks>
/// <para><strong>Chosen by the diff, never the branch name</strong> (D-45, D-87). Every changed path must be the
/// pin, a file adoption owns, or the declared stack; one path that is anything else and this is not an upgrade, so
/// the pull request is refused for having no chain, as before.</para>
/// <para><strong>Reproduced, not trusted.</strong> The pipeline restores the release the branch pins and runs this
/// as that release, so each framework file is compared with what this release's adoption writes from the base's
/// copy. A team permission slipped into the host settings, another tool bumped in the manifest: each is a file that
/// differs from what adoption writes, and each is refused. A pipeline is compared step by step instead, because a
/// team's own steps live beside the framework's: see <see cref="Shaped"/>.</para>
/// <para><strong>The declared stack may only be answered.</strong> Its lines are kept, in order, and each line
/// added answers a row this release ships and the base's stack never mentions (D-93). So nothing already in force
/// moves: a <c>true</c> or a default binds what was unbound, and a <c>false</c> or <c>none</c> records a no to
/// something that never bound.</para>
/// </remarks>
public static class VerifyUpgrade
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddVerifyUpgradeRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    private const string TechStackPath = Cycle.SdlcDirectoryName + "/" + Cycle.TechStackFileName;
    private const string ReadmePath = Cycle.SdlcDirectoryName + "/README.md";
    private const string IgnorePath = Cycle.SdlcDirectoryName + "/.gitignore";
    private static readonly string _gitHubPipeline = Initialise.PipelinePath(Platform.GitHub).Replace('\\', '/');
    private static readonly string _azurePipeline = Initialise.PipelinePath(Platform.AzureDevOps).Replace('\\', '/');
    private static readonly string _gitHubFactory = FactoryPipeline.PathOf(Platform.GitHub).Replace('\\', '/');
    private static readonly string _azureFactory = FactoryPipeline.PathOf(Platform.AzureDevOps).Replace('\\', '/');
    private static readonly string[] _pipelines = [_gitHubPipeline, _azurePipeline, _gitHubFactory, _azureFactory];

    /// <summary>Every path an upgrade may change, relative to the root.</summary>
    /// <remarks>
    /// What adoption owns in update mode, the factory's pipeline <c>sdlc factory</c> writes, the cycles directory's ignore
    /// file that opening a cycle tops up, the pin and the declared
    /// stack. Not the agent instruction file or the line-endings rule: adoption writes those only where there are none,
    /// which a governed solution already has.
    /// </remarks>
    public static IReadOnlyList<string> Paths { get; } =
    [
        PinnedRelease.ManifestPath,
        ReadmePath,
        IgnorePath,
        NewCycle.IgnorePath,
        .. SupportedHosts.All.Select(host => host.ConfigPath),
        .. _pipelines,
        TechStackPath,
    ];

    /// <summary>Asks whether a pull request is an upgrade and nothing else.</summary>
    /// <param name="Changed">What the pull request changes, relative to the root.</param>
    /// <param name="Divergence">The commit its branch left the target at: the base each file is compared from.</param>
    public sealed record Query(IReadOnlyList<string> Changed, string Divergence) : IRequest<Response>;

    /// <summary>What the check found.</summary>
    /// <param name="From">The release the base pins, or null when it pins none.</param>
    /// <param name="To">The release the branch pins, or null when it pins none.</param>
    /// <param name="Checks">Every check, in the order they ran.</param>
    public sealed record Response(string? From, string? To, IReadOnlyList<VerifyChain.ChainCheck> Checks)
    {
        /// <summary>Whether every check held.</summary>
        public bool Holds => Checks.All(check => check.Passed);
    }

    /// <summary>Whether a pull request's changes are only paths an upgrade may change.</summary>
    /// <param name="changed">The changed paths, relative to the root, with either separator.</param>
    /// <returns>True when there is at least one, and every one is.</returns>
    public static bool Carries(IReadOnlyList<string> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);

        return changed.Count > 0 && changed.All(path => Paths.Contains(Normalised(path), StringComparer.Ordinal));
    }

    private sealed class Handler(SolutionRoot solution, IRepository repository) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            List<VerifyChain.ChainCheck> checks = [];

            string? from = PinnedRelease.PinnedIn(Base(request, PinnedRelease.ManifestPath));
            string? to = PinnedRelease.PinnedIn(await Head(PinnedRelease.ManifestPath, cancellationToken).ConfigureAwait(false));

            checks.Add(Runs(from, to));

            if (!Carries(request.Changed))
            {
                checks.Add(new VerifyChain.ChainCheck(
                    "this pull request carries only an upgrade",
                    false,
                    "something it changes is not the pin, a file adoption owns, or the declared stack."));

                return Result.Ok(new Response(from, to, checks));
            }

            SolutionDocument? document = null;
            string? unreadable = null;

            foreach (string path in request.Changed.Select(Normalised).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                if (path == TechStackPath)
                {
                    checks.Add(Answered(
                        Base(request, path),
                        await Head(path, cancellationToken).ConfigureAwait(false)));
                    continue;
                }

                bool pipeline = _pipelines.Contains(path, StringComparer.Ordinal);

                if (pipeline && document is null && unreadable is null)
                {
                    Result<SolutionDocument> read = await SolutionDocument.ReadFile(solution.Path, cancellationToken).ConfigureAwait(false);
                    document = read.IsSuccess ? read.Value : null;
                    unreadable = read.IsSuccess ? null : string.Join(" ", read.Errors.Select(error => error.Message));
                }

                string? existing = Base(request, path);
                Result<string> expected = Expected(path, existing, document, unreadable);
                string? head = await Head(path, cancellationToken).ConfigureAwait(false);

                checks.Add(pipeline ? Shaped(path, expected, existing, head) : Written(path, expected, head));
            }

            return Result.Ok(new Response(from, to, checks));
        }

        private string? Base(Query request, string path) => repository.ReadAt(solution.Path, request.Divergence, path);

        private async Task<string?> Head(string path, CancellationToken cancellationToken)
        {
            string full = Path.Combine(solution.Path, Path.Combine([.. path.Split('/')]));

            return File.Exists(full)
                ? await File.ReadAllTextAsync(full, cancellationToken).ConfigureAwait(false)
                : null;
        }
    }

    /// <summary>
    /// Whether this check is running as the release the branch pins, which is what makes its comparison mean anything.
    /// </summary>
    /// <param name="from">The release the base pins.</param>
    /// <param name="to">The release the branch pins.</param>
    /// <returns>The check.</returns>
    public static VerifyChain.ChainCheck Runs(string? from, string? to)
    {
        const string Name = "this check runs as the release the branch pins";

        if (to is null)
        {
            return new VerifyChain.ChainCheck(
                Name,
                false,
                $"{PinnedRelease.ManifestPath} pins no release on this branch. Run 'dotnet tool update --local MofonCode.Sdlc.Cli --version <release>', then 'sdlc init', and commit both.");
        }

        if (!string.Equals(to, Release.Version, StringComparison.OrdinalIgnoreCase))
        {
            return new VerifyChain.ChainCheck(
                Name,
                false,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the branch pins {to} and this is sdlc {Release.Version}, so what it compares against is another release's. The pipeline restores the pin before this step; run it that way."));
        }

        string moved = from is null
            ? string.Create(CultureInfo.InvariantCulture, $"the base pinned nothing, and the branch pins {to}.")
            : string.Equals(from, to, StringComparison.OrdinalIgnoreCase)
                ? string.Create(CultureInfo.InvariantCulture, $"both pin {to}: a refresh rather than a move.")
                : FrameworkStamp.Compare(from, to) > 0
                    ? string.Create(CultureInfo.InvariantCulture, $"from {from} back to {to}: a rollback.")
                    : string.Create(CultureInfo.InvariantCulture, $"from {from} to {to}.");

        return new VerifyChain.ChainCheck(Name, true, moved);
    }

    /// <summary>What this release's adoption writes at a path, given the base's copy.</summary>
    /// <param name="path">The path, normalised.</param>
    /// <param name="existing">The base's copy, or null when it had none.</param>
    /// <param name="document">The branch's solution document, when a pipeline is asked about and it reads.</param>
    /// <param name="unreadable">Why the solution document did not read, when it did not.</param>
    /// <returns>The text, or why there is none to compare with.</returns>
    public static Result<string> Expected(string path, string? existing, SolutionDocument? document, string? unreadable)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path == PinnedRelease.ManifestPath)
        {
            return PinnedRelease.Pin(existing, Release.Version);
        }

        if (path == ReadmePath)
        {
            return Templates.Readme();
        }

        if (path == IgnorePath)
        {
            return Templates.SdlcIgnore();
        }

        if (path == NewCycle.IgnorePath)
        {
            return NewCycle.IgnoreFile(existing);
        }

        if (_pipelines.Contains(path, StringComparer.Ordinal))
        {
            Platform platform = path == _gitHubPipeline || path == _gitHubFactory ? Platform.GitHub : Platform.AzureDevOps;
            bool factory = path == _gitHubFactory || path == _azureFactory;

            if (document is null)
            {
                return Result.Fail(unreadable ?? "the solution document does not read, so which pipeline it takes is unknown.");
            }

            if (document.Platform != platform)
            {
                return Result.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"the solution document records '{document.Platform}', and the framework writes no pipeline here for it."));
            }

            return factory
                ? FactoryPipeline.Text(platform, document.IntegrationBranch)
                : Templates.Pipeline(platform, document.IntegrationBranch);
        }

        SupportedHost? host = SupportedHosts.All.FirstOrDefault(candidate => candidate.ConfigPath == path);

        if (host is null)
        {
            return Result.Fail("adoption does not write this path.");
        }

        string contents = Templates.HookConfig(host.Host);

        return existing is null
            ? contents
            : host.Ownership switch
            {
                ConfigOwnership.Shared => HookSettings.Merge(existing, contents),
                ConfigOwnership.Section => HookSettings.MergeSection(existing, contents, host.EventsKey),
                _ => contents,
            };
    }

    /// <summary>Whether a framework file is exactly what this release's adoption writes.</summary>
    /// <param name="path">The path.</param>
    /// <param name="expected">What adoption writes there, or why there is nothing to compare with.</param>
    /// <param name="head">The branch's copy, or null when the branch deleted it.</param>
    /// <returns>The check.</returns>
    public static VerifyChain.ChainCheck Written(string path, Result<string> expected, string? head)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(expected);

        string name = path + " is what adoption writes";

        if (expected.IsFailed)
        {
            return new VerifyChain.ChainCheck(name, false, string.Join(" ", expected.Errors.Select(error => error.Message)));
        }

        if (head is null)
        {
            return new VerifyChain.ChainCheck(name, false, "the branch deletes it, and adoption deletes nothing.");
        }

        return Lines(head) == Lines(expected.Value)
            ? new VerifyChain.ChainCheck(name, true, string.Create(CultureInfo.InvariantCulture, $"byte for byte, line endings aside, what sdlc {Release.Version} writes from the base's copy."))
            : new VerifyChain.ChainCheck(name, false, "it differs from what adoption writes from the base's copy. A change of a team's own belongs in a cycle; one made by an older release is fixed by running 'sdlc init' again as this one.");
    }

    /// <summary>
    /// Whether a pipeline carries every step this release writes, and beside them only steps of the team's own that the
    /// base already had.
    /// </summary>
    /// <remarks>
    /// <para><strong>A pipeline is a team's file as well as the framework's.</strong> Adoption writes the CI pipeline on
    /// Azure DevOps once and never again, <c>sdlc factory</c> rewrites the factory's and a person puts back what it
    /// dropped, and a solution adds steps of its own to both - a browser install, a feed sign-in, its own pool. So the
    /// file is compared as steps rather than as text: each step this release writes is there exactly and in its order,
    /// and every other step is one the base had, unchanged. A step added, a step of the team's changed, a step of the
    /// framework's missing or edited, and it is refused.</para>
    /// <para><strong>Outside the steps it is the base's or this release's</strong>, whole: the triggers, the pool,
    /// the job. Comments and blank lines are not compared, because neither runs.</para>
    /// <para><strong>What it lets through</strong>: a step of the team's own dropped in the upgrade. That weakens the
    /// team's own pipeline, not the framework's steps, and it is visible in the diff.</para>
    /// </remarks>
    /// <param name="path">The path.</param>
    /// <param name="template">What this release writes there, or why there is nothing to compare with.</param>
    /// <param name="existing">The base's copy, or null when it had none.</param>
    /// <param name="head">The branch's copy, or null when the branch deleted it.</param>
    /// <returns>The check.</returns>
    public static VerifyChain.ChainCheck Shaped(string path, Result<string> template, string? existing, string? head)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(template);

        string name = path + " has this release's steps, and only the team's own beside them";

        if (template.IsFailed)
        {
            return new VerifyChain.ChainCheck(name, false, string.Join(" ", template.Errors.Select(error => error.Message)));
        }

        if (head is null)
        {
            return new VerifyChain.ChainCheck(name, false, "the branch deletes it, and an upgrade deletes nothing.");
        }

        var want = Shape.Of(template.Value);
        Shape had = existing is null ? Shape.Empty : Shape.Of(existing);
        var has = Shape.Of(head);

        if (!has.Frame.SequenceEqual(want.Frame, StringComparer.Ordinal)
            && !(existing is not null && has.Frame.SequenceEqual(had.Frame, StringComparer.Ordinal)))
        {
            return new VerifyChain.ChainCheck(
                name,
                false,
                "outside its steps - the triggers, the pool, the job - it is neither what this release writes nor what the base had. A change of a team's own belongs in a cycle.");
        }

        int at = 0;
        List<string> own = [];

        foreach (string step in has.Steps)
        {
            if (at < want.Steps.Count && string.Equals(step, want.Steps[at], StringComparison.Ordinal))
            {
                at++;
            }
            else
            {
                own.Add(step);
            }
        }

        if (at < want.Steps.Count)
        {
            return new VerifyChain.ChainCheck(
                name,
                false,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the step '{Shape.Opening(want.Steps[at])}' that sdlc {Release.Version} writes is missing, changed or out of order. Put it back as the release writes it: 'sdlc init' prints it, and 'sdlc factory' writes the factory's."));
        }

        HashSet<string> carried = new(had.Steps, StringComparer.Ordinal);
        string? added = own.FirstOrDefault(step => !carried.Contains(step));

        if (added is not null)
        {
            return new VerifyChain.ChainCheck(
                name,
                false,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the step '{Shape.Opening(added)}' is neither this release's nor one the base had unchanged. A step of a team's own is added or changed by a cycle."));
        }

        return new VerifyChain.ChainCheck(
            name,
            true,
            own.Count == 0
                ? string.Create(CultureInfo.InvariantCulture, $"the steps sdlc {Release.Version} writes, and no others.")
                : string.Create(CultureInfo.InvariantCulture, $"the steps sdlc {Release.Version} writes, with {own.Count} of the team's own carried over unchanged."));
    }

    /// <summary>A pipeline as what lies outside its step lists and each step in them, comments and blank lines aside.</summary>
    /// <param name="Frame">Every line outside a step list, the <c>steps:</c> key included, in order.</param>
    /// <param name="Steps">Each step, its lines joined and prefixed with which step list it is in.</param>
    private sealed record Shape(IReadOnlyList<string> Frame, IReadOnlyList<string> Steps)
    {
        public static Shape Empty { get; } = new([], []);

        public static Shape Of(string text)
        {
            List<string> frame = [];
            List<string> steps = [];
            StringBuilder? step = null;
            int list = -1;
            int key = -1;
            int? item = null;

            void Close()
            {
                if (step is not null)
                {
                    steps.Add(step.ToString());
                    step = null;
                }
            }

            foreach (string raw in text.ReplaceLineEndings("\n").Split('\n'))
            {
                string line = raw.TrimEnd();
                string trimmed = line.TrimStart();

                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }

                int indent = line.Length - trimmed.Length;
                bool dash = trimmed.StartsWith("- ", StringComparison.Ordinal);

                if (key >= 0 && (indent < key || (indent == key && !dash)))
                {
                    Close();
                    key = -1;
                    item = null;
                }

                if (key >= 0)
                {
                    item ??= dash ? indent : null;

                    if (dash && indent == item)
                    {
                        Close();
                        step = new StringBuilder(list.ToString(CultureInfo.InvariantCulture));
                    }

                    if (step is not null)
                    {
                        step.Append('\n').Append(line);
                        continue;
                    }
                }

                frame.Add(line);

                if (trimmed == "steps:")
                {
                    key = indent;
                    list++;
                }
            }

            Close();
            return new Shape(frame, steps);
        }

        /// <summary>A step's first line, to name it by.</summary>
        public static string Opening(string step) => step.Split('\n')[1].Trim();
    }

    /// <summary>Whether the declared stack only gained answers to rows this release asks about.</summary>
    /// <param name="existing">The base's stack, or null when there was none.</param>
    /// <param name="head">The branch's stack, or null when it deleted it.</param>
    /// <returns>The check.</returns>
    public static VerifyChain.ChainCheck Answered(string? existing, string? head)
    {
        string name = Cycle.TechStackFileName + " only answers what this release asks";

        if (existing is null || head is null)
        {
            return new VerifyChain.ChainCheck(
                name,
                false,
                existing is null
                    ? "the base has no declared stack, so there is nothing for an upgrade to answer. Placing one is a person's first declaration, and it is not an upgrade."
                    : "the branch deletes the declared stack.");
        }

        List<string> kept = [.. existing.Split('\n').Select(line => line.TrimEnd('\r'))];
        List<string> added = [];
        int at = 0;

        foreach (string line in head.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (at < kept.Count && string.Equals(line, kept[at], StringComparison.Ordinal))
            {
                at++;
            }
            else
            {
                added.Add(line);
            }
        }

        if (at < kept.Count)
        {
            return new VerifyChain.ChainCheck(
                name,
                false,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a line the stack had is changed or gone: '{kept[at].Trim()}'. An answer already given is changed by a cycle, because plans were judged against it."));
        }

        StackCoverage.Unmentioned asked = StackCoverage.Of(existing);
        HashSet<string> concerns = new(asked.Defaults.Select(row => row.Concern), StringComparer.OrdinalIgnoreCase);
        HashSet<string> rules = new(asked.Rules.Select(rule => rule.Id), StringComparer.OrdinalIgnoreCase);
        string[] furniture = [StackRules.Heading, .. StackRules.Table([], on: true)];
        List<string> answers = [];

        foreach (string line in added)
        {
            if (line.Trim().Length == 0 || furniture.Contains(line.Trim(), StringComparer.Ordinal))
            {
                continue;
            }

            if (StackRules.Rows(line) is [StackRules.Row rule] && rules.Remove(rule.Id))
            {
                answers.Add(rule.Id);
                continue;
            }

            if (StackTable.Rows(line) is [StackTable.Row row] && concerns.Remove(row.Concern.Trim()))
            {
                answers.Add(row.Concern.Trim());
                continue;
            }

            return new VerifyChain.ChainCheck(
                name,
                false,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{line.Trim()}' answers nothing this release asks: each line added must be a row the base's stack never mentioned, answered once. Any other change to the stack belongs in a cycle."));
        }

        return new VerifyChain.ChainCheck(
            name,
            true,
            answers.Count == 0
                ? "every line it had is kept, and nothing was added."
                : "every line it had is kept, and it answers " + string.Join(", ", answers) + ".");
    }

    /// <summary>Renders the check for a pipeline log.</summary>
    /// <param name="verdict">What it found.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        StringBuilder output = new();
        output.AppendLine();
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  upgrade - sdlc {verdict.From ?? "(none)"} to {verdict.To ?? "(none)"}"));
        output.AppendLine();

        foreach (VerifyChain.ChainCheck check in verdict.Checks)
        {
            output.AppendLine((check.Passed ? "    [ok]  " : "    [no]  ") + check.Name);
            output.AppendLine("          " + check.Detail);
        }

        output.AppendLine();
        output.AppendLine(verdict.Holds
            ? "  The upgrade holds: it changes nothing but what this release's adoption writes, and answers to what it asks."
            : "  The upgrade does not hold. Moving a release needs no cycle, and anything else in the same pull request does.");

        return output.ToString();
    }

    private static string Normalised(string path) => path.Replace('\\', '/').TrimStart('/');

    private static string Lines(string text) => text.ReplaceLineEndings("\n");
}
