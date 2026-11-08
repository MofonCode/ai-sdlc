using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Cycles;

/// <summary>Opens a cycle: allocates its id and creates the directory that proves the id is unused.</summary>
public static class NewCycle
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddNewCycleRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Cycle>, Handler>();
    }

    /// <summary>Where the cycles directory's ignore file lives, relative to the root.</summary>
    public const string IgnorePath = Cycle.SdlcDirectoryName + "/" + Cycle.CyclesDirectoryName + "/.gitignore";

    /// <summary>
    /// The cycles directory's ignore file as the next cycle to open leaves it: written whole where there is none,
    /// and otherwise with each block an older release did not write appended, every line already there kept.
    /// </summary>
    /// <remarks>
    /// An upgrade's pull request may carry it (D-133), so the check computes it from the base's copy the same way.
    /// </remarks>
    /// <param name="existing">The file as it stands, or null when there is none.</param>
    /// <returns>The file's text.</returns>
    public static string IgnoreFile(string? existing)
    {
        if (existing is null)
        {
            return string.Concat(Handler.IgnoreBlocks
                .SelectMany(block => block.Append(string.Empty))
                .SkipLast(1)
                .Select(line => line + Environment.NewLine));
        }

        HashSet<string> present = [.. existing.ReplaceLineEndings("\n").Split('\n').Select(line => line.Trim())];

        // A block counts as present when every pattern in it is; its comment is not compared, so a
        // person who reworded one is not handed a second copy.
        IEnumerable<string> missing = Handler.IgnoreBlocks
            .Where(block => !block.Where(line => !line.StartsWith('#')).All(present.Contains))
            .SelectMany(block => block.Prepend(string.Empty));

        return existing + string.Concat(missing.Select(line => line + Environment.NewLine));
    }

    /// <summary>Opens a cycle: allocates its id and creates its directory.</summary>
    /// <remarks>
    /// A command rather than a query, and the only one in the gate that creates anything a cycle depends on.
    /// It exists because the intent stage needs the directory to exist before it can write into it, and
    /// because creating it is the collision check.
    /// </remarks>
    public sealed record Command : IRequest<Cycle>;

    /// <summary>
    /// Allocates a cycle id and creates its directory, failing if one is already there.
    /// </summary>
    /// <remarks>
    /// <para><strong>Claiming the cycle is the check, and a directory cannot do it.</strong> Two cycles
    /// opened in the same second would collide, and nothing earlier can catch that: at allocation there is
    /// no directory to be already there. This used to read <c>Directory.Exists</c> and then
    /// <c>Directory.CreateDirectory</c> directly beneath a remark saying a look-then-create would leave
    /// the race open -- and it could not have been done the stated way regardless, because
    /// <c>CreateDirectory</c> succeeds on a directory that is already there, so creation alone can never
    /// be a collision check. D-10 is a recorded decision with a rejection list, and the code did the
    /// rejected thing while asserting otherwise.
    /// </para>
    /// <para>
    /// So the claim is a marker file opened with <see cref="FileMode.CreateNew"/>, which throws when the
    /// file exists. That is one atomic operation the filesystem decides, which is what the decision says.
    /// The directory is created first and harmlessly: an existing one is not the collision, the claim is.
    /// </para>
    /// <para>
    /// The id is purely temporal, so ids sort chronologically and say when the cycle opened. Allocation needs
    /// no coordination and works offline, which a sequence number would not.
    /// </para>
    /// </remarks>
    /// <param name="solution">The solution to open the cycle in.</param>
    /// <param name="time">Where the current moment comes from, so a test can fix it.</param>
    /// <param name="repository">Where every worktree of the repository claims an id.</param>
    private sealed class Handler(SolutionRoot solution, TimeProvider time, IRepository repository)
        : IRequestHandler<Command, Cycle>
    {
        /// <summary>
        /// The file whose creation claims the cycle.
        /// </summary>
        /// <remarks>
        /// Dot-prefixed and named for what it is, so somebody finding one in a cycle directory can tell
        /// it is bookkeeping rather than an artifact. Nothing ever reads its contents; it is empty, and
        /// its existence is the whole of the information.
        /// </remarks>
        private const string ClaimFileName = ".opened";

        /// <summary>
        /// What the cycles directory's own ignore file says.
        /// </summary>
        /// <remarks>
        /// <para><strong>The claim is local state, not history.</strong> Its whole job is to make two
        /// <c>sdlc new</c> invocations in the same second collide in one working copy, which the
        /// filesystem decides and git has no part in. Committing it would put a file no plan declared
        /// into the first cycle's diff, which the evidence comparison reports as <c>UNDECLARED</c> on
        /// every cycle forever.</para>
        ///
        /// <para>One file in the cycles directory rather than one per cycle, because a pattern with no
        /// slash matches at any depth below the ignore file — so every cycle is covered by the one
        /// written when the first cycle is opened. The negation keeps the ignore itself tracked, so a
        /// clone arrives already ignoring claims rather than ignoring them only here. Same shape and
        /// same reasoning as <c>ReportFile</c>, which D-69 settled: the directory ignores itself, and an
        /// existing solution gets it the first time something writes there rather than needing an
        /// adoption re-run.</para>
        ///
        /// <para><strong>The runner's own output is not history either</strong> (D-92). Nothing reads a
        /// <c>.trx</c> or <c>build.log</c> once <c>tests.json</c> holds the run, and the pipeline publishes
        /// its own. Kept on disk for the cycle's verify, which may cite them while it runs.</para>
        ///
        /// <para>In blocks, so a solution whose file predates a block gets that block appended, with its
        /// comment, the next time a cycle opens. A file somebody edited keeps every line they wrote.</para>
        /// </remarks>
        internal static string[][] IgnoreBlocks { get; } =
        [
            [
                "# A cycle's .opened marker claims its id: creating it is what makes two cycles opened in",
                "# the same second collide. That is local state the filesystem decides, not history -- and",
                "# committing it would put a file no plan declared into a build's diff.",
                ".opened",
                "!.gitignore",
            ],
            [
                "# The runner's own output, kept beside tests.json for the cycle's verify and never committed:",
                "# nothing reads it once the summary holds the run, and the pipeline publishes its own (D-92).",
                "/*/evidence/*.trx",
                "/*/evidence/build.log",
            ],
            [
                "# The change itself, as the verify reads it. Never committed: git holds the change, and the",
                "# chain rebuilds the patch from base.sha and head.sha (D-124).",
                "/*/evidence/diff.patch",
            ],
        ];

        /// <inheritdoc />
        public Task<Result<Cycle>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // No cycle opens until a person has said what the solution is built from. Adoption does
            // not write the declared stack any more, so without this a first cycle could reach its plan
            // with nothing for CON_15 to read it against and no stack rule in force -- every gate
            // green, and the one argument the stack file exists to force never made. An empty file
            // passes: that is a person declaring that nothing is pinned, which is a decision, where a
            // missing file is nobody having made one (D-76).
            string stack = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, Cycle.TechStackFileName);

            if (!File.Exists(stack))
            {
                return Task.FromResult(Result.Fail<Cycle>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no {stack}, so nothing says what this solution is built from and a plan would be judged against nothing. Fill in the draft beside it -- adoption wrote techstack.draft.md, and 'sdlc draft techstack' writes it again if it is gone, or with --empty to pin nothing -- write 'none' in each row not true here, and rename it into place. Only a person places that file.")));
            }

            // Uncommitted work is not refused here any more (D-144). It was, because the plan branch was
            // switched to next and a switch carries changes with it; a sandbox is a worktree and switches
            // nothing, and the one command that does switch -- 'sdlc branch <id> plan' without a worktree --
            // refuses it instead, where the carrying would happen.
            var id = CycleId.Allocate(time);
            Cycle cycle = new(id, solution.Path);

            // Claimed first where every worktree of the repository looks, so two sandboxes opening a cycle in
            // the same second collide there rather than each succeeding in a folder of its own (D-144).
            if (repository.IsRepository(solution.Path) && repository.CommonDirectory(solution.Path) is { } common)
            {
                string shared = Path.Combine(common, "sdlc", "claims", id.ToString());

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(shared)!);

                    using (new FileStream(shared, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                    }
                }
                catch (IOException) when (File.Exists(shared))
                {
                    return Task.FromResult(Result.Fail<Cycle>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"Cycle {id} was already opened in another worktree of this repository. Two cycles opened in the same second collide, because an id is the moment it opened to the second. Wait a second and try again.")));
                }
            }

            try
            {
                Directory.CreateDirectory(cycle.Directory);

                // Written before the claim, so the first cycle a solution opens is already covered. If
                // it is absent the solution predates this and gets it now, which is why it is checked
                // rather than written once at adoption.
                string ignore = Path.Combine(
                    Path.GetDirectoryName(cycle.Directory)!,
                    ".gitignore");

                Ignore(ignore);

                // CreateNew is the whole mechanism: it throws if the file is there, and the filesystem
                // decides that, so two processes in the same second cannot both win. Closed at once --
                // nothing reads the marker, its existence is the claim.
                using (new FileStream(
                    Path.Combine(cycle.Directory, ClaimFileName),
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                }
            }
            catch (IOException) when (File.Exists(Path.Combine(cycle.Directory, ClaimFileName)))
            {
                return Task.FromResult(Result.Fail<Cycle>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {id} already exists at {cycle.Directory}. Two cycles opened in the same second collide, because an id is the moment it opened to the second. Wait a second and try again.")));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return Task.FromResult(Result.Fail<Cycle>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Could not create {cycle.Directory}. {error.Message}")));
            }

            return Task.FromResult(Result.Ok(cycle));
        }

        /// <summary>
        /// Writes the cycles directory's ignore file, or appends each block an older one lacks.
        /// </summary>
        /// <param name="path">The ignore file.</param>
        private static void Ignore(string path)
        {
            string? existing = File.Exists(path) ? File.ReadAllText(path) : null;
            string written = IgnoreFile(existing);

            if (!string.Equals(existing, written, StringComparison.Ordinal))
            {
                File.WriteAllText(path, written);
            }
        }
    }
}
