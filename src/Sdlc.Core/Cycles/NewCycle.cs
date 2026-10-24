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
    /// <param name="repository">How to ask git whether anything is uncommitted.</param>
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
        /// </remarks>
        private static readonly string[] _ignore =
        [
            "# A cycle's .opened marker claims its id: creating it is what makes two cycles opened in",
            "# the same second collide. That is local state the filesystem decides, not history -- and",
            "# committing it would put a file no plan declared into a build's diff.",
            ".opened",
            "!.gitignore",
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

            // Nor over work nobody has committed. The intent skill switches to the cycle's plan branch
            // next, and a switch carries uncommitted changes with it -- so a placed stack, a half-written
            // change, anything, would ride into the cycle and reach its diff as work no plan declared.
            // Refused here rather than asked about in the skill, because a file listing decides it, and
            // so that opening a cycle by hand gets the same answer. What to do with the changes -- commit,
            // stash, discard -- is the person's, so this names them and decides nothing.
            if (repository.IsRepository(solution.Path)
                && repository.UncommittedChanges(solution.Path).Where(path => !IsBookkeeping(path)).ToArray() is { Length: > 0 } changed)
            {
                return Task.FromResult(Result.Fail<Cycle>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Nothing was opened: {changed.Length} {(changed.Length == 1 ? "file has" : "files have")} uncommitted changes, and a cycle's branch would carry {(changed.Length == 1 ? "it" : "them")} into the cycle as work no plan declared -- {string.Join(", ", changed.Take(8))}{(changed.Length > 8 ? ", and more" : string.Empty)}. Commit, stash or discard {(changed.Length == 1 ? "it" : "them")}, then open the cycle again.")));
            }

            var id = CycleId.Allocate(time);
            Cycle cycle = new(id, solution.Path);

            try
            {
                Directory.CreateDirectory(cycle.Directory);

                // Written before the claim, so the first cycle a solution opens is already covered. If
                // it is absent the solution predates this and gets it now, which is why it is checked
                // rather than written once at adoption.
                string ignore = Path.Combine(
                    Path.GetDirectoryName(cycle.Directory)!,
                    ".gitignore");

                if (!File.Exists(ignore))
                {
                    File.WriteAllLines(ignore, _ignore);
                }

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
        /// Whether an uncommitted path is an ignore file the framework writes into its own directory.
        /// </summary>
        /// <remarks>
        /// This command writes the cycles directory's on the first cycle and a report writes its own, so
        /// counting them would have the second cycle refused over a file the first one created. They hold
        /// nobody's work, which is the only thing the refusal exists to protect.
        /// </remarks>
        private static bool IsBookkeeping(string path)
        {
            string normalised = path.Replace('\\', '/');

            return normalised.StartsWith(Cycle.SdlcDirectoryName + "/", StringComparison.Ordinal)
                && normalised.EndsWith("/.gitignore", StringComparison.Ordinal);
        }
    }
}
