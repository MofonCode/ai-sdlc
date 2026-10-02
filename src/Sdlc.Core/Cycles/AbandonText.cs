using System.Globalization;
using FluentResults;
using MofonCode.Core;
using Sdlc.Core.Drafts;

namespace Sdlc.Core.Cycles;

/// <summary>Asks for the record a person would place to stop a cycle.</summary>
/// <param name="Id">The cycle to stop.</param>
/// <param name="By">
/// Who is stopping it. Supplied by the caller from the git identity rather than invented here, for the
/// same reason an approval names a person: a tool that filled this in would be taking the decision.
/// </param>
public sealed record AbandonTextQuery(CycleId Id, string By) : IRequest<DraftResult>;

/// <summary>
/// Prints an abandonment record. Writes nothing.
/// </summary>
/// <remarks>
/// The reason is left empty, because it is the only thing this file is for and nobody but the person
/// stopping the cycle can write it. An agent stuck on a blocking finding has an obvious escape in
/// abandoning and starting clean, and a hand-written reason is what keeps the objections that killed
/// the cycle on the record rather than in a conversation nobody can find.
/// </remarks>
/// <param name="solutionRoot">The root of the solution being governed.</param>
public sealed class AbandonTextHandler(string solutionRoot)
    : IRequestHandler<AbandonTextQuery, DraftResult>
{
    /// <inheritdoc />
    public Task<Result<DraftResult>> Handle(AbandonTextQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Cycle cycle = new(request.Id, solutionRoot);

        if (!cycle.Exists)
        {
            return Task.FromResult(Result.Ok(DraftResult.Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {cycle.Id} has no directory at {cycle.Directory}. There is nothing to abandon."))));
        }

        if (cycle.IsAbandoned)
        {
            return Task.FromResult(Result.Ok(DraftResult.Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {cycle.Id} was already abandoned. {cycle.AbandonedPath} says why, and abandonment is terminal rather than a state to re-enter."))));
        }

        string entry = string.Join(
            Environment.NewLine,
            [
                "---",
                string.Create(CultureInfo.InvariantCulture, $"cycle: {cycle.Id}"),
                string.Create(CultureInfo.InvariantCulture, $"framework_version: {Cycle.FrameworkVersion}"),
                string.Create(CultureInfo.InvariantCulture, $"by: {request.By}"),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"at: {DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"),
                "---",
                string.Empty,
                "# Why this cycle stopped",
                string.Empty,
            ]) + Environment.NewLine;

        string instructions = string.Join(
            Environment.NewLine,
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Create {cycle.AbandonedPath} with the text above, then write the reason under the heading."),
                string.Empty,
                "The reason is the only thing this cycle produced, and the file is rejected without one.",
                "Say what was wrong with the intent, not that the cycle was abandoned - a repository",
                "holding the intents that were tried and dropped, with the objections that killed them,",
                "is readable history. A record saying only 'not needed' has thrown that away.",
                string.Empty,
                "Abandonment is terminal. This cycle will never build, even if it also holds an approval,",
                "and there is no command to revive it. Open a new cycle instead.",
                string.Empty,
                "Nothing here writes that file. The hook denies every agent write to it.",
            ]);

        return Task.FromResult(Result.Ok(DraftResult.Drafted(entry, instructions)));
    }
}
