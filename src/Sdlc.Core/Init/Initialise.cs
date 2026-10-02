using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Init;

/// <summary>Adopts the framework into a repository, in one of three modes.</summary>
public static class Initialise
{
    /// <summary>The agent instruction file adoption leans, and never overwrites.</summary>
    /// <remarks>
    /// On the holder rather than inside the handler: the name is a fact about what adoption writes,
    /// and the suite and the renderer both name it. The handler keeps nothing but its work.
    /// </remarks>
    public const string AgentsFileName = "AGENTS.md";

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddInitialiseRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>How adoption is running.</summary>
    public enum InitMode
    {
        /// <summary>A new solution. The framework's defaults are the solution's defaults.</summary>
        Scaffold,

        /// <summary>Code that existed first, with no framework directory yet.</summary>
        Onboard,

        /// <summary>A governed solution already. Only what the framework owns is refreshed.</summary>
        Update,
    }

    /// <summary>What happened to one file.</summary>
    public enum InitChange
    {
        /// <summary>It did not exist, and now does.</summary>
        Created,

        /// <summary>The framework owns it, and it was rewritten.</summary>
        Updated,

        /// <summary>It exists and is not the framework's to change.</summary>
        Kept,

        /// <summary>Nothing was written; the contents were printed for a person to place.</summary>
        Printed,
    }

    /// <summary>One file adoption touched, or deliberately did not.</summary>
    /// <param name="Path">Where it is, relative to the solution root.</param>
    /// <param name="Change">What happened to it.</param>
    /// <param name="Why">Why, when the answer is not obvious from the change.</param>
    public sealed record InitAction(string Path, InitChange Change, string? Why = null);

    /// <summary>What adoption did.</summary>
    /// <param name="Mode">Which mode ran.</param>
    /// <param name="Origin">What the solution's origin was decided to be.</param>
    /// <param name="Actions">Every file touched or deliberately left, in writing order.</param>
    /// <param name="Printed">File contents that were not written, keyed by the path they belong at.</param>
    public sealed record Response(
        InitMode Mode,
        SolutionOrigin Origin,
        IReadOnlyList<InitAction> Actions,
        IReadOnlyDictionary<string, string> Printed)
    {
        /// <summary>The paths that were written, for the commit to stage.</summary>
        public IReadOnlyList<string> Written =>
        [
            .. Actions
                .Where(action => action.Change is InitChange.Created or InitChange.Updated)
                .Select(action => action.Path),
        ];
    }

    /// <summary>Adopts the framework into a repository.</summary>
    /// <param name="Profile">The profile that will govern the solution.</param>
    /// <param name="Platform">Where the code lives, which decides the pipeline.</param>
    public sealed record Command(string Profile, Platform Platform) : IRequest<Response>;

    /// <summary>
    /// Writes the framework into a repository, in one of three modes.
    /// </summary>
    /// <remarks>
    /// <para><strong>It refuses outside a git repository.</strong> Without git, <c>build</c> cannot record
    /// the diff that <c>verify</c> derives from, and the gates key on a <c>cycle/&lt;id&gt;</c> branch that
    /// cannot exist -- so every hook would permit everything while the artifacts still looked governed. That
    /// is the exact failure this design is written against: a directory that appears enforced and is not.
    /// </para>
    /// <para><strong>Update needs no list of what it may touch.</strong> It updates what it owns, and what
    /// it owns is everything not protected. So the four files only a person writes are safe by the same rule
    /// the hook uses, rather than by a second list that could drift from it.
    /// </para>
    /// <para><strong>It never modifies a protected file, in any mode.</strong> It will create one that does
    /// not exist, because adoption is the moment those files come into being. The distinction matters because
    /// an agent can invoke a command even where it cannot write a file, and create-but-never-modify is the
    /// version of that which cannot destroy a person's work.
    /// </para>
    /// </remarks>
    /// <param name="solution">The repository to adopt into.</param>
    /// <param name="repository">How to ask git what it knows.</param>
    private sealed class Handler(SolutionRoot solution, IRepository repository)
        : IRequestHandler<Command, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.Profile))
            {
                return Result.Fail<Response>(
                    "Which profile? Give its package name, as in 'sdlc init --profile dotnet-default'.");
            }

            if (!repository.IsRepository(solution.Path))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{solution.Path} is not a git repository. Without git, a build cannot record the diff that verify derives from and the gates key on a branch that cannot exist -- so every hook would permit everything while the artifacts still looked governed. Run 'git init' first."));
            }

            string sdlc = Path.Combine(solution.Path, Cycle.SdlcDirectoryName);
            InitMode mode = Mode(sdlc);
            SolutionOrigin origin = await Origin(mode, cancellationToken).ConfigureAwait(false);

            List<InitAction> actions = [];
            Dictionary<string, string> printed = new(StringComparer.Ordinal);

            // Protected files are created and never modified, which is the one rule that makes an agent
            // invoking this command unable to destroy anything a person wrote.
            await Protected(
                Path.Combine(Cycle.SdlcDirectoryName, Cycle.SolutionFileName),
                Templates.Solution(request.Profile, origin),
                actions,
                cancellationToken).ConfigureAwait(false);

            // techstack.md is owned by a cycle whose plan declares it, not by the framework, so adoption
            // creates it once and then leaves it alone.
            await Once(
                Path.Combine(Cycle.SdlcDirectoryName, "techstack.md"),
                Templates.TechStack(origin),
                actions,
                cancellationToken).ConfigureAwait(false);

            // Framework-owned: rewritten every time, which is how a solution's scaffolding stops rotting
            // across versions.
            await Owned(
                Path.Combine(Cycle.SdlcDirectoryName, "README.md"),
                Templates.Readme(),
                actions,
                cancellationToken).ConfigureAwait(false);

            await Owned(
                Path.Combine(".claude", "settings.json"),
                Templates.HookConfig("claude"),
                actions,
                cancellationToken).ConfigureAwait(false);

            await Owned(
                Path.Combine(".github", "hooks", "sdlc.json"),
                Templates.HookConfig("copilot"),
                actions,
                cancellationToken).ConfigureAwait(false);

            await Owned(
                PipelinePath(request.Platform),
                Templates.Pipeline(request.Platform),
                actions,
                cancellationToken).ConfigureAwait(false);

            // Written under .sdlc rather than into .git/hooks, because .git is not tracked -- a hook written
            // there would exist on one machine and silently not on anybody else's, which is the shape of
            // enforcement this design is written against. The person installs it, and init says how.
            await Owned(
                Path.Combine(Cycle.SdlcDirectoryName, "hooks", "pre-push"),
                Templates.PrePushHook(),
                actions,
                cancellationToken).ConfigureAwait(false);

            await Agents(actions, printed, cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(mode, origin, actions, printed));
        }

        /// <summary>Where a platform's pull-request pipeline goes.</summary>
        /// <param name="platform">Which platform.</param>
        /// <returns>The path, relative to the solution root.</returns>
        public static string PipelinePath(Platform platform) => platform switch
        {
            Platform.GitHub => Path.Combine(".github", "workflows", "pull-request.yml"),
            Platform.AzureDevOps => "azure-pipelines.yml",
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };

        private InitMode Mode(string sdlc) => Directory.Exists(sdlc)
            ? InitMode.Update
            : repository.TrackedFiles(solution.Path).Any(IsCode)
                ? InitMode.Onboard
                : InitMode.Scaffold;

        /// <summary>
        /// Whether a tracked file counts as the repository already holding a solution.
        /// </summary>
        /// <remarks>
        /// A repository holding one empty commit, a README and a licence has no solution in it yet, so it is
        /// scaffolded rather than adopted. Getting this wrong in the adopted direction is the expensive one:
        /// an adopted solution does not take the profile's patterns, so a genuinely new solution misread as
        /// adopted silently loses every default it was meant to start from.
        /// </remarks>
        private static bool IsCode(string path)
        {
            string name = Path.GetFileName(path);

            string[] furniture =
            [
                "README.md", "README", "LICENSE", "LICENSE.md", "LICENCE", "LICENCE.md",
                ".gitignore", ".gitattributes", ".editorconfig", "CODE_OF_CONDUCT.md",
                "CONTRIBUTING.md", "SECURITY.md", "CHANGELOG.md", AgentsFileName, "CLAUDE.md",
            ];

            return !furniture.Contains(name, StringComparer.OrdinalIgnoreCase)
                && !path.Replace('\\', '/').StartsWith(Cycle.SdlcDirectoryName + "/", StringComparison.Ordinal);
        }

        /// <summary>
        /// What the solution's origin is.
        /// </summary>
        /// <remarks>
        /// Read from the existing document wherever there is one, and decided only when the file is being
        /// written for the first time. Recomputing it would turn a historical fact into something that moves:
        /// a scaffolded solution would read as adopted the moment it had code in it, which is every solution
        /// after its first cycle.
        /// <para>
        /// A repository with a framework directory but no solution document falls through to the fresh
        /// decision, which is right -- there is nothing to preserve, and the alternative is a tool that cannot
        /// repair the one file everything else resolves through.
        /// </para>
        /// </remarks>
        private async Task<SolutionOrigin> Origin(InitMode mode, CancellationToken cancellationToken)
        {
            if (mode == InitMode.Scaffold)
            {
                return SolutionOrigin.Scaffolded;
            }

            Result<SolutionDocument> existing = await SolutionDocument
                .ReadFile(solution.Path, cancellationToken)
                .ConfigureAwait(false);

            return existing.IsSuccess ? existing.Value.Origin : SolutionOrigin.Adopted;
        }

        private async Task Protected(
            string relative,
            string contents,
            List<InitAction> actions,
            CancellationToken cancellationToken)
        {
            string path = Path.Combine(solution.Path, relative);

            if (File.Exists(path))
            {
                actions.Add(new InitAction(
                    relative,
                    InitChange.Kept,
                    "only a person writes this, and it already exists"));
                return;
            }

            await Write(path, contents, cancellationToken).ConfigureAwait(false);
            actions.Add(new InitAction(relative, InitChange.Created));
        }

        private async Task Once(
            string relative,
            string contents,
            List<InitAction> actions,
            CancellationToken cancellationToken)
        {
            string path = Path.Combine(solution.Path, relative);

            if (File.Exists(path))
            {
                actions.Add(new InitAction(relative, InitChange.Kept, "a cycle owns this once it exists"));
                return;
            }

            await Write(path, contents, cancellationToken).ConfigureAwait(false);
            actions.Add(new InitAction(relative, InitChange.Created));
        }

        private async Task Owned(
            string relative,
            string contents,
            List<InitAction> actions,
            CancellationToken cancellationToken)
        {
            string path = Path.Combine(solution.Path, relative);
            bool existed = File.Exists(path);

            if (existed && string.Equals(
                await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
                contents,
                StringComparison.Ordinal))
            {
                actions.Add(new InitAction(relative, InitChange.Kept, "already current"));
                return;
            }

            await Write(path, contents, cancellationToken).ConfigureAwait(false);
            actions.Add(new InitAction(relative, existed ? InitChange.Updated : InitChange.Created));
        }

        /// <summary>
        /// Writes the agent instruction file, or prints a leaned version when one already exists.
        /// </summary>
        /// <remarks>
        /// Rewriting a repository's instruction file is the one destructive thing adoption could do, and the
        /// existing one usually holds things nothing else knows. So an existing file is printed rather than
        /// replaced -- the same draft pattern the protected files use, for the same reason.
        /// </remarks>
        private async Task Agents(
            List<InitAction> actions,
            Dictionary<string, string> printed,
            CancellationToken cancellationToken)
        {
            string path = Path.Combine(solution.Path, AgentsFileName);
            string contents = Templates.Agents(Path.GetFileName(solution.Path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)));

            if (File.Exists(path))
            {
                printed[AgentsFileName] = contents;
                actions.Add(new InitAction(
                    AgentsFileName,
                    InitChange.Printed,
                    "one already exists, and overwriting it would lose what only it knows"));
                return;
            }

            await Write(path, contents, cancellationToken).ConfigureAwait(false);
            actions.Add(new InitAction(AgentsFileName, InitChange.Created));
        }

        private static async Task Write(string path, string contents, CancellationToken cancellationToken)
        {
            // Directories are created here and nowhere speculatively: nothing makes a cycles/ directory that
            // holds no cycle, because an empty directory is a thing a reader has to work out the meaning of.
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, contents, cancellationToken).ConfigureAwait(false);
        }
    }
}
