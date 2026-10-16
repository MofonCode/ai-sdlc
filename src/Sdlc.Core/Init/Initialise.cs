using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Hooks;
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
        /// <remarks>
        /// Named <c>Greenfield</c> rather than <c>Scaffold</c> because <c>sdlc scaffold</c> is the
        /// command that writes the projects and this mode writes none — D-35 is the rule that they are
        /// different things. One word meaning both "adoption found no code" and "write the solution
        /// skeleton" put the collision at exactly the place somebody asks which one <c>init</c> does.
        /// <para>
        /// Not <c>New</c> either: <c>sdlc new</c> allocates a cycle, so that spelling trades this
        /// collision for a second one, in the document where both are listed.
        /// </para>
        /// <para>
        /// <see cref="Solutions.SolutionOrigin.Scaffolded"/> keeps its name, and correctly: it records
        /// that a repository's code came from the skeleton, which is what the command did.
        /// </para>
        /// </remarks>
        Greenfield,

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
    /// <param name="Standard">What the solution document says, or will say, about the standard.</param>
    /// <param name="Actions">Every file touched or deliberately left, in writing order.</param>
    /// <param name="Printed">File contents that were not written, keyed by the path they belong at.</param>
    /// <param name="PlatformPending">
    /// Whether <c>--platform</c> asked for a platform the recorded document does not say. True on
    /// exactly one path: the option against a <c>solution.md</c> that already exists. Protected files
    /// are created and never modified, so the pipeline is written and the keys are printed for a person
    /// to place -- and until they do, the pipeline is on disk and nothing knows it governs the merge.
    /// It was once invisible: the platform did not enter the calculation at all, so re-running init
    /// wrote the pipeline and printed nothing, and the recorded platform stayed <c>local</c> forever.
    /// </param>
    /// <param name="Platform">
    /// What the solution document says, or will say, about where the code lives. The counterpart to
    /// <see cref="Standard"/>, and here for the same reason: a reader told a key is pending has to be
    /// told which value is still standing, and a renderer that assumed one would be wrong in every
    /// direction but the common one.
    /// </param>
    /// <param name="Setup">
    /// What a person still has to place before the first cycle, read after everything above was
    /// written. Adoption writes the solution document with a placeholder and no declared stack at all
    /// (D-76), so on a first run both are left and the report names the first (D-79). Null renders no
    /// next step.
    /// </param>
    public sealed record Response(
        InitMode Mode,
        SolutionOrigin Origin,
        SolutionStandard Standard,
        IReadOnlyList<InitAction> Actions,
        IReadOnlyDictionary<string, string> Printed,
        bool PlatformPending = false,
        Platform Platform = Platform.GitHub,
        SetupState? Setup = null)
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
    /// <remarks>
    /// It takes nothing about the stack. Which technologies a solution is built from, and which stack
    /// rules it is held to, are declared afterwards in a file a person places -- so adoption has no
    /// profile to be told and cannot record one that later disagrees with that file (D-76). That
    /// includes the .NET build settings: an option here used to write them into an existing codebase,
    /// which was adoption deciding a stack question before the stack was declared, whatever the stack
    /// went on to say. They are rows in the declared stack now, and a cycle brings them (D-77).
    /// </remarks>
    /// <param name="Platform">
    /// Where the code lives, which decides the pipeline. <strong>Null means the caller did not say</strong>,
    /// which is not the same as saying <c>github</c>: on a first adoption the two coincide, but on a
    /// repository already governed they are opposites. Absent has to mean "keep what is recorded", or
    /// re-running init to pick up a template would propose migrating a deliberately local solution onto
    /// a platform nobody asked for.
    /// </param>
    public sealed record Command(Platform? Platform = null) : IRequest<Response>;

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
    /// it owns is everything not protected. So the six files only a person writes are safe by the same rule
    /// the hook uses, rather than by a second list that could drift from it.
    /// </para>
    /// <para><strong>It never modifies a protected file, in any mode.</strong> It creates exactly one,
    /// <c>solution.md</c>, when it does not exist, because adoption is the moment that file comes into
    /// being and its keys are facts adoption is the one to observe. The distinction matters because an
    /// agent can invoke a command even where it cannot write a file, and create-but-never-modify is the
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

            if (!repository.IsRepository(solution.Path))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{solution.Path} is not a git repository. Without git, a build cannot record the diff that verify derives from and the gates key on a branch that cannot exist -- so every hook would permit everything while the artifacts still looked governed. Run 'git init' first."));
            }

            string sdlc = Path.Combine(solution.Path, Cycle.SdlcDirectoryName);
            InitMode mode = Mode(sdlc);
            SolutionDocument? recorded = await Recorded(mode, cancellationToken).ConfigureAwait(false);
            SolutionOrigin origin = recorded?.Origin ?? Decided(mode);

            // Recorded, or what the origin implies. Adoption never changes it on a document that
            // exists: which conventions new work follows is a person's decision, made by renaming a
            // draft -- 'sdlc draft solution --standard' -- like every other change to that file (D-77).
            SolutionStandard standard = recorded?.Standard ?? SolutionDocument.DefaultFor(origin);

            // What this run asks for, and what the file will actually say. The two differ on exactly one
            // path -- the option against a solution.md that already exists -- because the document is
            // protected and adoption creates it without ever rewriting it. There the pipeline is
            // written and the keys are printed for a person to place, which is the same arrangement
            // every other protected file has. It used to print nothing: re-running init with a new
            // platform wrote the pipeline and left the recorded platform untouched, which left doctor
            // reporting "no pull-request check governs a merge" after the check existed and the pr
            // skill refusing a cycle that had somewhere to go.
            Platform wantedPlatform = request.Platform ?? recorded?.Platform ?? Platform.GitHub;
            Platform platform = recorded?.Platform ?? wantedPlatform;

            bool platformPending = platform != wantedPlatform;

            List<InitAction> actions = [];
            Dictionary<string, string> printed = new(StringComparer.Ordinal);

            string document = Path.Combine(Cycle.SdlcDirectoryName, Cycle.SolutionFileName);

            // Protected files are created and never modified, which is the one rule that makes an agent
            // invoking this command unable to destroy anything a person wrote.
            await Protected(
                document,
                Templates.Solution(origin, standard, wantedPlatform),
                actions,
                cancellationToken).ConfigureAwait(false);

            if (platformPending)
            {
                // The whole key block, not the one line that changes. A person pasting a partial block
                // loses whichever key was left out of it, and the one that silently reclassifies a
                // solution is the platform.
                printed[document] = string.Join(
                    Environment.NewLine,
                    SolutionDocument.Keys(origin, standard, wantedPlatform))
                    + Environment.NewLine;
            }

            // The declared stack is not written here, and that is the change D-76 makes. It used to be
            // the one protected file a tool created with its contents already decided -- from a profile
            // named on this command line, before anybody had said what the solution was built from. A
            // person places it now, from 'sdlc draft techstack', and the report's next step says so
            // when it is the one left: no cycle opens until it is there.

            // Framework-owned: rewritten every time, which is how a solution's scaffolding stops rotting
            // across versions.
            await Owned(
                Path.Combine(Cycle.SdlcDirectoryName, "README.md"),
                Templates.Readme(),
                actions,
                cancellationToken).ConfigureAwait(false);

            // Every supported host's hook configuration, from the table that describes them rather
            // than a block each. Two blocks is where this started, which meant adding a host was an
            // edit here that nothing asked for and nothing missed -- and the two differed in a way
            // that mattered: one file the framework owns outright and one it owns entries inside.
            // That is the host's row saying so now, not a choice made twice by hand. D-40, D-46.
            foreach (SupportedHost host in SupportedHosts.All)
            {
                string relative = Path.Combine([.. host.ConfigPath.Split('/')]);
                string contents = Templates.HookConfig(host.Host);

                switch (host.Ownership)
                {
                    case ConfigOwnership.Shared:
                        // The host's own shared project settings -- permissions, environment, the
                        // team's own hooks. Everything that is not the framework's two entries is
                        // read, kept and written back.
                        await Merged(relative, contents, actions, printed, cancellationToken)
                            .ConfigureAwait(false);
                        break;

                    case ConfigOwnership.Section:
                        // A file of named hook groups. The framework owns the one it named and every
                        // sibling key is somebody else's, so the merge replaces a key rather than
                        // matching entries by the command they run.
                        await Merged(
                            relative,
                            contents,
                            actions,
                            printed,
                            cancellationToken,
                            host.EventsKey).ConfigureAwait(false);
                        break;

                    default:
                        await Owned(relative, contents, actions, cancellationToken).ConfigureAwait(false);
                        break;
                }
            }

            // The pipeline is the one non-bypassable layer, so adoption has to write it -- but only a
            // framework-named file is the framework's to rewrite. `sdlc-verify.yml` is owned.
            // `azure-pipelines.yml` is the name Azure DevOps looks for, so an existing one is this
            // repository's own CI: it is printed for a person to place rather than replaced, and init
            // says the guarantee is not installed until they do. D-40.
            // Local writes none, and that is the choice rather than an omission. A pipeline for a service
            // this repository has no remote on would be a file that looks like a gate and is reachable by
            // nothing -- which is the exact failure D-32 names about an unrun pipeline. What replaces it
            // is the record in solution.md, so doctor can say the guarantee is not installed instead of
            // reporting a broken install.
            if (wantedPlatform == Platform.Local)
            {
                // Nothing to write. The report names what is not installed, which is said out loud in
                // InitRenderer rather than left for somebody to notice.
            }
            else if (wantedPlatform == Platform.GitHub)
            {
                await Owned(
                    PipelinePath(wantedPlatform),
                    Templates.Pipeline(wantedPlatform, recorded?.IntegrationBranch ?? SolutionDocument.DefaultIntegrationBranch),
                    actions,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await Introduced(
                    PipelinePath(wantedPlatform),
                    Templates.Pipeline(wantedPlatform, recorded?.IntegrationBranch ?? SolutionDocument.DefaultIntegrationBranch),
                    "one already exists, and it is this repository's own CI rather than the framework's to replace. Nothing verifies a chain until this step runs in a pipeline",
                    actions,
                    printed,
                    cancellationToken).ConfigureAwait(false);
            }

            // No build settings, in any mode. They are .NET -- warnings as errors in a props file, a
            // global.json pinning an SDK -- and adoption does not know what a solution is built from,
            // so writing them here would be deciding the stack on the way past. The declared stack
            // lists the ones a solution takes, and 'sdlc scaffold' writes them inside a cycle whose
            // approved plan declares them (D-77).

            // No git hook either. A pre-push hook was written here and never installed -- installing is
            // core.hooksPath, which is per clone and untracked -- so it looked like enforcement and ran
            // nowhere, and where it did run it refused a push of work in progress. What may merge is the
            // pull-request check's to decide, and the pr skill asks 'sdlc pr-ready' before it pushes (D-78).

            await Agents(actions, printed, cancellationToken).ConfigureAwait(false);
            await Attributes(mode, actions, printed, cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(
                mode,
                origin,
                standard,
                actions,
                printed,
                platformPending,
                platform,
                await Setup.Read(solution.Path, cancellationToken).ConfigureAwait(false)));
        }

        /// <summary>Where a platform's pull-request pipeline goes.</summary>
        /// <remarks>
        /// The GitHub path is framework-named on purpose. It was <c>pull-request.yml</c>, which is a
        /// name a repository plausibly already uses, and adoption rewrote whatever was there -- so a
        /// team's own pull-request workflow was replaced and committed, announced as "updated". A
        /// framework-named file makes owning it honest, and takes the collision away rather than
        /// handling it. Azure DevOps has no equivalent escape, because <c>azure-pipelines.yml</c> is
        /// the path that platform looks for, so that one is printed on a collision instead. D-40.
        /// </remarks>
        /// <param name="platform">Which platform.</param>
        /// <returns>The path, relative to the solution root.</returns>
        public static string PipelinePath(Platform platform) => platform switch
        {
            Platform.GitHub => Path.Combine(".github", "workflows", "sdlc-verify.yml"),
            Platform.AzureDevOps => "azure-pipelines.yml",

            // Local has no pipeline and so has no path. Asking for one is a caller that has not read the
            // platform, and returning a plausible path would have it write a file nothing runs.
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };

        private InitMode Mode(string sdlc) => Directory.Exists(sdlc)
            ? InitMode.Update
            : repository.TrackedFiles(solution.Path).Any(IsCode)
                ? InitMode.Onboard
                : InitMode.Greenfield;

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
        /// The solution document as it stands, when there is a readable one.
        /// </summary>
        /// <remarks>
        /// The whole document rather than the one key this used to read, because two keys now come from it
        /// and a third and fourth are needed to print a replacement block. Reading it twice would be two
        /// chances to disagree with itself.
        /// <para>
        /// Null for a repository with a framework directory and no readable document, which falls through
        /// to the fresh decision -- there is nothing to preserve, and the alternative is a tool that cannot
        /// repair the one file everything else resolves through.
        /// </para>
        /// </remarks>
        private async Task<SolutionDocument?> Recorded(InitMode mode, CancellationToken cancellationToken)
        {
            if (mode == InitMode.Greenfield)
            {
                return null;
            }

            Result<SolutionDocument> existing = await SolutionDocument
                .ReadFile(solution.Path, cancellationToken)
                .ConfigureAwait(false);

            return existing.IsSuccess ? existing.Value : null;
        }

        /// <summary>
        /// What the origin is when nothing has recorded one yet.
        /// </summary>
        /// <remarks>
        /// Only ever consulted for a document that does not exist. A recorded origin always wins, because
        /// recomputing it would turn a historical fact into something that moves: a scaffolded solution
        /// would read as adopted the moment it had code in it, which is every solution after its first
        /// cycle.
        /// </remarks>
        private static SolutionOrigin Decided(InitMode mode) => mode == InitMode.Greenfield
            ? SolutionOrigin.Scaffolded
            : SolutionOrigin.Adopted;

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
        /// Puts the framework's hook entries into a settings file the team also owns.
        /// </summary>
        /// <remarks>
        /// <see cref="Owned"/> would be wrong here, and was: the framework owns two hook entries in
        /// <c>.claude/settings.json</c>, and the host and the team own the rest of it. Where a merge
        /// cannot be done without guessing -- the file is not valid JSON -- nothing is written and the
        /// entries are printed, which leaves a file somebody hand-edited exactly as they left it.
        /// </remarks>
        private async Task Merged(
            string relative,
            string contents,
            List<InitAction> actions,
            Dictionary<string, string> printed,
            CancellationToken cancellationToken,
            string? section = null)
        {
            string path = Path.Combine(solution.Path, relative);

            if (!File.Exists(path))
            {
                await Write(path, contents, cancellationToken).ConfigureAwait(false);
                actions.Add(new InitAction(relative, InitChange.Created));
                return;
            }

            string existing = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            // Two merges, because a host's file is shared in one of two ways: the framework owns
            // entries inside one hooks object, or it owns one named top-level section beside the
            // team's own. A file this cannot parse is written to in neither case.
            Result<string> merged = section is null
                ? HookSettings.Merge(existing, contents)
                : HookSettings.MergeSection(existing, contents, section);

            if (merged.IsFailed)
            {
                printed[relative] = contents;
                actions.Add(new InitAction(
                    relative,
                    InitChange.Printed,
                    string.Join(" ", merged.Errors.Select(error => error.Message))));
                return;
            }

            if (string.Equals(existing, merged.Value, StringComparison.Ordinal))
            {
                actions.Add(new InitAction(relative, InitChange.Kept, "already current"));
                return;
            }

            await Write(path, merged.Value, cancellationToken).ConfigureAwait(false);
            actions.Add(new InitAction(
                relative,
                InitChange.Updated,
                "the framework's hook entries only -- everything else in the file was kept"));
        }

        /// <summary>
        /// Writes a file the framework introduces but does not own, and prints it when one is there.
        /// </summary>
        /// <remarks>
        /// For a file at a path the platform chose rather than the framework. Replacing it would
        /// destroy a repository's own, and keeping quiet would leave a person thinking the guarantee
        /// was installed, so it is printed and the reason is said out loud.
        /// </remarks>
        private async Task Introduced(
            string relative,
            string contents,
            string why,
            List<InitAction> actions,
            Dictionary<string, string> printed,
            CancellationToken cancellationToken)
        {
            string path = Path.Combine(solution.Path, relative);

            if (File.Exists(path))
            {
                printed[relative] = contents;
                actions.Add(new InitAction(relative, InitChange.Printed, why));
                return;
            }

            await Write(path, contents, cancellationToken).ConfigureAwait(false);
            actions.Add(new InitAction(relative, InitChange.Created));
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

        /// <summary>
        /// Writes the line-endings rule, or prints it when the repository's own file lacks it.
        /// </summary>
        /// <remarks>
        /// The file is the repository's, so an existing one is never rewritten -- it is kept when it
        /// already normalises, and the one line is printed when it does not. Where there was code before
        /// this, files committed with CRLF show as changed until they are renormalised, and the report
        /// says so rather than leaving a person to find a status full of files nobody touched.
        /// </remarks>
        private async Task Attributes(
            InitMode mode,
            List<InitAction> actions,
            Dictionary<string, string> printed,
            CancellationToken cancellationToken)
        {
            string path = Path.Combine(solution.Path, LineEndings.FileName);

            if (File.Exists(path))
            {
                string existing = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

                if (LineEndings.Normalises(existing))
                {
                    actions.Add(new InitAction(LineEndings.FileName, InitChange.Kept, "already normalises line endings"));
                    return;
                }

                printed[LineEndings.FileName] = LineEndings.Rule + Environment.NewLine;
                actions.Add(new InitAction(
                    LineEndings.FileName,
                    InitChange.Printed,
                    "it is this repository's own and does not normalise line endings, so a commit from another platform can show a whole file changed. Add the line, then 'git add --renormalize .' and commit"));
                return;
            }

            await Write(path, LineEndings.Contents(), cancellationToken).ConfigureAwait(false);
            actions.Add(mode == InitMode.Greenfield
                ? new InitAction(LineEndings.FileName, InitChange.Created)
                : new InitAction(
                    LineEndings.FileName,
                    InitChange.Created,
                    "files already committed with CRLF endings show as changed until 'git add --renormalize .' is committed"));
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
