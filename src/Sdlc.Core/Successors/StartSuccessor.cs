using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Successors;

/// <summary>
/// Starts a ready successor as a cycle: refuses one that is not ready, and deletes its file (D-125).
/// </summary>
/// <remarks>
/// <para><strong>Deterministic, so the binary does it</strong> (§15.2). Whether a successor is ready is a reading
/// of the record, and deleting one file is not a judgement; a skill doing either in prose would do it three ways.</para>
/// <para><strong>Run in the new cycle's working copy, on its plan branch.</strong> The deletion then travels with that
/// cycle's plan pull request, and the intent that records <c>lead: successor:&lt;slug&gt;</c> beside it is what resolves
/// every other reference to the slug. Nobody edits the files that named it.</para>
/// </remarks>
public static class StartSuccessor
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddStartSuccessorRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Starts a successor as the cycle given.</summary>
    /// <param name="Slug">The successor.</param>
    /// <param name="Cycle">The cycle opened for it, here.</param>
    public sealed record Command(string Slug, CycleId Cycle) : IRequest<Response>;

    /// <summary>What was started.</summary>
    /// <param name="Successor">The successor, as its file held it.</param>
    /// <param name="Cycle">The cycle it now is.</param>
    /// <param name="Deleted">The file deleted, relative to the root.</param>
    public sealed record Response(Successor Successor, CycleId Cycle, string Deleted);

    private sealed class Handler(SolutionRoot solution, Mediator mediator) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Cycle, solution.Path);

            if (!cycle.Exists)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no cycle {request.Cycle} in this working copy. Open it first -- 'sdlc new', then 'sdlc branch <id> plan --worktree' -- and run this in its folder, so the file is deleted on its plan branch."));
            }

            Result<ReadSuccessors.Response> read = await mediator
                .Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            if (ReadSuccessors.WhyNotReady(read.Value, request.Slug) is { } refusal)
            {
                return Result.Fail<Response>(refusal);
            }

            string relative = Successor.RelativePath(request.Slug);
            File.Delete(Path.Combine(solution.Path, relative.Replace('/', Path.DirectorySeparatorChar)));

            return Result.Ok(new Response(read.Value.Find(request.Slug)!.Successor, request.Cycle, relative));
        }
    }
}
