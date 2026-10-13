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
    /// <param name="StandardPending">
    /// Whether <c>--adopt-standard</c> asked for a standard the recorded document does not say. True on
    /// exactly one path: the flag against a <c>solution.md</c> that already exists. Protected files are
    /// created and never modified, so the stack files are written and the key is printed for a person to
    /// place -- and until they do, <see cref="Standard"/> is what every plan is held to.
    /// </param>
    /// <param name="PlatformPending">
    /// Whether <c>--platform</c> asked for a platform the recorded document does not say. The same one
    /// path as <see cref="StandardPending"/>, and kept separate from it because the consequence differs:
    /// a pending standard means plans are held to the surrounding code, a pending platform means the
    /// pipeline is on disk and nothing knows it governs the merge. Both were once invisible -- the
    /// platform did not enter the calculation at all, so re-running init wrote the pipeline and printed
    /// nothing, and the recorded platform stayed <c>local</c> forever.
    /// </param>
    /// <param name="Platform">
    /// What the solution document says, or will say, about where the code lives. The counterpart to
    /// <see cref="Standard"/>, and here for the same reason: a reader told a key is pending has to be
    /// told which value is still standing, and a renderer that assumed one would be wrong in every
    /// direction but the common one.
    /// </param>
    /// <param name="StackDeclared">
    /// Whether a declared stack is in place. Adoption never writes one -- a person places it, from a
    /// draft -- so on a first run this is false and the report says what to run next, because no cycle
    /// opens until it is there (D-76).
    /// </param>
    public sealed record Response(
        InitMode Mode,
        SolutionOrigin Origin,
        SolutionStandard Standard,
        IReadOnlyList<InitAction> Actions,
        IReadOnlyDictionary<string, string> Printed,
        bool StandardPending = false,
        bool PlatformPending = false,
        Platform Platform = Platform.GitHub,
        bool StackDeclared = true)
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
    /// profile to be told and cannot record one that later disagrees with that file (D-76).
    /// </remarks>
    /// <param name="Platform">
    /// Where the code lives, which decides the pipeline. <strong>Null means the caller did not say</strong>,
    /// which is not the same as saying <c>github</c>: on a first adoption the two coincide, but on a
    /// repository already governed they are opposites. Absent has to mean "keep what is recorded", or
    /// re-running init to pick up a template would propose migrating a deliberately local solution onto
    /// a platform nobody asked for.
    /// </param>
    /// <param name="AdoptStandard">
    /// Whether an existing codebase is opting in to the profile's conventions for its new work. It
    /// writes the stack files now, which nothing else at adoption does, and records
    /// <c>standard: profile</c>.
    /// </param>
    public sealed record Command(Platform? Platform = null, bool AdoptStandard = false)
        : IRequest<Response>;

    /// <summary>
    /// The files that decide how .NET code is written here.
    /// </summary>
    /// <remarks>
    /// <strong>Exposed because something else needs the same list, and a second copy of it is how
    /// the harness comes to build a solution nobody will ever get.</strong>
    /// <c>tools/Sdlc.ScaffoldHarness</c> writes these before the skeleton so that what it compiles
    /// is what adoption produces -- and it listed them by hand, with nothing asserting the two
    /// agreed. A sixth file added here would have left CI green and the real thing broken, which is
    /// the failure the harness exists to prevent, one level up. F-22.
    /// <para>
    /// <strong>Two things write them, and adoption on its own is neither.</strong> They are .NET build
    /// settings, so they belong to a stack nobody has declared yet when adoption runs. A new solution
    /// gets them from <c>sdlc scaffold</c>, with the skeleton they exist to constrain and behind the
    /// same approved plan (D-76). An adopted repository gets them only by asking:
    /// <c>--adopt-standard</c> is that asking, and it is a person's decision rather than a default,
    /// because these files apply a convention to code that never agreed to one --
    /// <c>Directory.Build.props</c> makes warnings errors, so the next build can fail on files nobody
    /// has touched. D-56.
    /// </para>
    /// <para>
    /// Each is created once and never rewritten, update included -- they are a person's to amend, and a
    /// cycle that changes one is a cycle like any other. Which is also what keeps the opt-in from
    /// overwriting anything: an <c>.editorconfig</c> the repository already had is kept.
    /// </para>
    /// </remarks>
    /// <returns>Each file's path relative to the solution root, and its contents.</returns>
    public static IReadOnlyList<(string Path, string Contents)> StackFiles() =>
    [
        (".editorconfig", Templates.EditorConfig()),
        ("Directory.Build.props", Templates.BuildProps()),
        ("Directory.Packages.props", Templates.PackageProps()),
        ("global.json", Templates.GlobalJson()),
        (System.IO.Path.Combine("tests", "Directory.Build.props"), Templates.TestBuildProps()),
    ];


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

            // What this run asks for, and what the file will actually say. The two differ on exactly one
            // path -- the flag against a solution.md that already exists -- because the document is
            // protected and adoption creates it without ever rewriting it. There, the stack files are
            // written and the key is printed for a person to place, which is the same arrangement every
            // other protected file has rather than a special case for this one. D-56.
            SolutionStandard wanted = request.AdoptStandard
                ? SolutionStandard.Profile
                : recorded?.Standard ?? SolutionDocument.DefaultFor(origin);

            SolutionStandard standard = recorded?.Standard ?? wanted;

            // The platform the same way, and it used to not be. `pending` was computed from the standard
            // alone, so re-running init with a new platform wrote the pipeline and left the recorded
            // platform untouched with nothing printed -- which contradicts what --platform's own help
            // says, and left doctor reporting "no pull-request check governs a merge" after the check
            // existed and the pr skill refusing a cycle that had somewhere to go. A document that has
            // gone stale against the repository, with nothing to notice: the same defect as a verify
            // document outliving its evidence.
            Platform wantedPlatform = request.Platform ?? recorded?.Platform ?? Platform.GitHub;
            Platform platform = recorded?.Platform ?? wantedPlatform;

            bool standardPending = standard != wanted;
            bool platformPending = platform != wantedPlatform;
            bool pending = standardPending || platformPending;

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

            if (pending)
            {
                // The whole key block, not the one line that changes. A person pasting a partial block
                // loses whichever key was left out of it, and the one that silently reclassifies a
                // solution is the platform.
                printed[document] = string.Join(
                    Environment.NewLine,
                    SolutionDocument.Keys(origin, wanted, wantedPlatform))
                    + Environment.NewLine;
            }

            // The declared stack is not written here, and that is the change D-76 makes. It used to be
            // the one protected file a tool created with its contents already decided -- from a profile
            // named on this command line, before anybody had said what the solution was built from. A
            // person places it now, from 'sdlc draft techstack', and this only reports whether they
            // have: no cycle opens until it is there.
            bool stackDeclared = File.Exists(Path.Combine(
                solution.Path,
                Cycle.SdlcDirectoryName,
                Cycle.TechStackFileName));

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
                    Templates.Pipeline(wantedPlatform),
                    actions,
                    cancellationToken).ConfigureAwait(false);

                Superseded(Path.Combine(".github", "workflows", "pull-request.yml"), actions);
            }
            else
            {
                await Introduced(
                    PipelinePath(wantedPlatform),
                    Templates.Pipeline(wantedPlatform),
                    "one already exists, and it is this repository's own CI rather than the framework's to replace. Nothing verifies a chain until this step runs in a pipeline",
                    actions,
                    printed,
                    cancellationToken).ConfigureAwait(false);
            }

            // The stack files, and only when this run asks for them with --adopt-standard. They are .NET
            // -- warnings as errors in a props file, a global.json pinning an SDK -- and adoption no
            // longer knows what a solution is built from, so writing them unasked would be deciding the
            // stack on the way past. A new solution gets them from 'sdlc scaffold', with the skeleton
            // they constrain; an adopted one keeps its own conventions unless its team asks here.
            //
            // Keyed on what this run asks for rather than on what the document records, so the flag does
            // something on an update -- which is where a migration is actually decided, months after
            // adoption. The cost is the one state the renderer has to say out loud: the files are here
            // and the recorded key still says otherwise until a person places it. It also means a plain
            // refresh writes none of them, whatever the document records: this used to consult the
            // profile named on the command line rather than the one recorded, so re-running adoption
            // could write .NET settings into a solution declared on another stack.
            //
            // This is not a baseline. Nothing is marked forgiven and no existing violation is recorded --
            // the files declare what new work follows, and the warnings they raise on old code are the
            // cost of the opt-in, carried by whoever opted in.
            //
            // Written by the binary rather than restored from a package -- D-33.
            //
            // Created once and never rewritten. These are a person's to amend, and a cycle that changes
            // one is a cycle like any other.
            if (request.AdoptStandard)
            {
                foreach ((string path, string contents) in StackFiles())
                {
                    await Once(path, contents, actions, cancellationToken).ConfigureAwait(false);
                }
            }

            // Written under .sdlc rather than into .git/hooks, because .git is not tracked -- a hook written
            // there would exist on one machine and silently not on anybody else's, which is the shape of
            // enforcement this design is written against. The person installs it, and init says how.
            await Owned(
                Path.Combine(Cycle.SdlcDirectoryName, "hooks", "pre-push"),
                Templates.PrePushHook(),
                actions,
                cancellationToken).ConfigureAwait(false);

            await Agents(actions, printed, cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(
                mode,
                origin,
                standard,
                actions,
                printed,
                standardPending,
                platformPending,
                platform,
                stackDeclared));
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
        /// Names a pipeline an earlier adoption wrote at the name the framework used to claim.
        /// </summary>
        /// <remarks>
        /// The GitHub workflow moved to <c>sdlc-verify.yml</c> (D-40), which leaves the old file behind
        /// still running the same check on every pull request. Deleting somebody's workflow is not a
        /// thing adoption should do, and saying nothing would leave two pipelines doing one job with
        /// no explanation of why. So it is named, and a person removes it.
        /// <para>
        /// Only when the file holds the gate's own command. A <c>pull-request.yml</c> that is a team's
        /// own workflow is none of adoption's business, and the whole point of the rename is that the
        /// framework stops having an opinion about that name.
        /// </para>
        /// </remarks>
        private void Superseded(string relative, List<InitAction> actions)
        {
            string path = Path.Combine(solution.Path, relative);

            if (!File.Exists(path)
                || !File.ReadAllText(path).Contains("sdlc verify-chain", StringComparison.Ordinal))
            {
                return;
            }

            actions.Add(new InitAction(
                relative,
                InitChange.Kept,
                "an earlier adoption wrote this, and the framework's pipeline is now sdlc-verify.yml. "
                + "Nothing here deletes a workflow -- remove it yourself, or two pipelines run the same check"));
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

        private static async Task Write(string path, string contents, CancellationToken cancellationToken)
        {
            // Directories are created here and nowhere speculatively: nothing makes a cycles/ directory that
            // holds no cycle, because an empty directory is a thing a reader has to work out the meaning of.
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, contents, cancellationToken).ConfigureAwait(false);
        }
    }
}
