using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
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
    private sealed class Handler(SolutionRoot solution, TimeProvider time)
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

        /// <inheritdoc />
        public Task<Result<Cycle>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var id = CycleId.Allocate(time);
            Cycle cycle = new(id, solution.Path);

            try
            {
                Directory.CreateDirectory(cycle.Directory);

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
    }
}
