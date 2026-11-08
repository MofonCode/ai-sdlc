using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Successors;

/// <summary>
/// Takes a parked cycle off the backlog once everything it waited on has merged with a passing verify (D-145).
/// </summary>
/// <remarks>
/// <para><strong>Ready means what a successor's ready means</strong>, worked out by the same reader, so the backlog
/// and this command cannot disagree.</para>
/// <para><strong>Unparking is not approving.</strong> The plan was written before the code it needed existed; it is
/// read again against what merged, by interrogating the spec and the plan, and only a round that changes nothing
/// leaves a standing approval matching. The command says so and does none of it.</para>
/// </remarks>
public static class UnparkCycle
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddUnparkCycleRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Unparks a cycle.</summary>
    /// <param name="Id">The cycle.</param>
    public sealed record Command(CycleId Id) : IRequest<Response>;

    /// <summary>What was unparked.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="File">The parked file it had, now gone.</param>
    public sealed record Response(CycleId Id, Parked File);

    private sealed class Handler(SolutionRoot solution, Mediator mediator) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);

            if (!File.Exists(Parked.PathOf(cycle)))
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"{request.Id} is not parked."));
            }

            Result<ReadSuccessors.Response> backlog = await mediator
                .Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (backlog.IsFailed)
            {
                return backlog.ToResult<Response>();
            }

            if (backlog.Value.Waiting.FirstOrDefault(p => p.Cycle == request.Id) is not { } standing)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{request.Id}'s {Parked.FileName} cannot be read: 'sdlc successor check' says why."));
            }

            if (standing.State != SuccessorState.Ready)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{request.Id} is {standing.State.ToString().ToUpperInvariant()[0]}{standing.State.ToString()[1..]}: {string.Join(", ", standing.Waits.Select(ReadSuccessors.Describe))}. It unparks once each has merged into the integration branch with a passing verify."));
            }

            File.Delete(Parked.PathOf(cycle));
            return Result.Ok(new Response(request.Id, standing.File));
        }
    }
}
