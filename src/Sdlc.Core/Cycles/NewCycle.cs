using System.Globalization;
using FluentResults;
using MofonCode.Core;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Cycles;

/// <summary>Opens a cycle: allocates its id and creates its directory.</summary>
/// <remarks>
/// A command rather than a query, and the only one in the gate that creates anything a cycle depends on.
/// It exists because the intent stage needs the directory to exist before it can write into it, and
/// because creating it is the collision check.
/// </remarks>
public sealed record NewCycleCommand : IRequest<Cycle>;

/// <summary>
/// Allocates a cycle id and creates its directory, failing if one is already there.
/// </summary>
/// <remarks>
/// <para><strong>Creating the directory is the check.</strong> Two cycles opened in the same second
/// would collide, and nothing earlier can catch that: at allocation there is no directory to be already
/// there. A look-then-create would leave the race it was written to close, so this creates and lets the
/// filesystem decide -- which it does atomically.
/// </para>
/// <para>
/// The id is purely temporal, so ids sort chronologically and say when the cycle opened. Allocation needs
/// no coordination and works offline, which a sequence number would not.
/// </para>
/// </remarks>
/// <param name="solution">The solution to open the cycle in.</param>
/// <param name="time">Where the current moment comes from, so a test can fix it.</param>
public sealed class NewCycleHandler(SolutionRoot solution, TimeProvider time)
    : IRequestHandler<NewCycleCommand, Cycle>
{
    /// <inheritdoc />
    public Task<Result<Cycle>> Handle(NewCycleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = CycleId.Allocate(time);
        Cycle cycle = new(id, solution.Path);

        if (Directory.Exists(cycle.Directory))
        {
            return Task.FromResult(Result.Fail<Cycle>(string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {id} already exists at {cycle.Directory}. Two cycles opened in the same second collide, because an id is the moment it opened to the second. Wait a second and try again.")));
        }

        try
        {
            Directory.CreateDirectory(cycle.Directory);
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
