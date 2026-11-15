using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Deviations;

/// <summary>
/// The report a build ends on: where it bent the approved documents, what it chose that they left open, what the
/// person asked to change, and what the verification noticed without blocking (D-181).
/// </summary>
/// <remarks>
/// <para><strong>The pull request is where a person accepts</strong>, so this is what they read before they do:
/// the build's hand-back and the head of the pull request's body. The binary renders it so every host prints the
/// same thing, and an agent never summarises it -- a summary is where a deviation quietly goes missing.</para>
/// <para><strong>Concerns are the verification's open suggestions</strong>, every reader's, perspectives
/// included: what was noticed and did not block. They are not this cycle's work, and each can start the next.</para>
/// </remarks>
public static class BuildReport
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddBuildReportRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks for a cycle's report.</summary>
    /// <param name="Id">The cycle.</param>
    public sealed record Query(CycleId Id) : IRequest<Response>;

    /// <summary>What the report says.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Deviations">Its deviations and choices, and where each stands.</param>
    /// <param name="Concerns">The verification's open non-blocking findings, every reader's.</param>
    public sealed record Response(Cycle Cycle, DeviationsState Deviations, IReadOnlyList<Finding> Concerns)
    {
        /// <summary>Whether there is anything to tell the person beyond a passing verification.</summary>
        public bool Empty => Deviations.Current.Count == 0 && Concerns.Count == 0;
    }

    /// <summary>The report as markdown: fit for the head of a pull request's body and for a terminal.</summary>
    /// <param name="response">What was read.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        DeviationsState deviations = response.Deviations;
        StringBuilder text = new();

        text.Append("## Report\n\n");

        if (response.Empty)
        {
            text.Append("Nothing to report: the build followed the approved documents, made no choice they left open, and the verification raised no concern.\n");
            return text.ToString();
        }

        if (deviations.InForce.Count > 0)
        {
            text.Append("**Deviations** — where the build could not follow the approved text, and what it did instead\n\n");
            foreach (DeviationState state in deviations.InForce)
            {
                Deviation deviation = state.Deviation;
                text.Append(CultureInfo.InvariantCulture, $"- **{deviation.Id}** — {deviation.Gist} ({Deviation.NameOf(deviation.Kind)}). ");
                text.Append(CultureInfo.InvariantCulture, $"Departs from {deviation.Document}{(deviation.Section.Length > 0 ? ", " + deviation.Section : string.Empty)}. ");
                text.Append(CultureInfo.InvariantCulture, $"Instead: {deviation.Instead} Why: {deviation.Why}\n");
            }

            text.Append('\n');
        }

        if (deviations.Choices.Count > 0)
        {
            text.Append("**Choices** — what the build decided that the approved text left open\n\n");
            foreach (DeviationState state in deviations.Choices)
            {
                Deviation deviation = state.Deviation;
                text.Append(CultureInfo.InvariantCulture, $"- **{deviation.Id}** — {deviation.Gist}. Chose {deviation.Instead} instead of {deviation.Alternative}. Why: {deviation.Why}\n");
            }

            text.Append('\n');
        }

        IReadOnlyList<DeviationState> changed = [.. deviations.Declined.Where(state => state.Standing != DeviationStanding.History)];
        if (changed.Count > 0)
        {
            text.Append("**Changed at your request**\n\n");
            foreach (DeviationState state in changed)
            {
                text.Append(CultureInfo.InvariantCulture, $"- **{state.Deviation.Id}** — {state.Deviation.Gist}: {state.Note}\n");
            }

            text.Append('\n');
        }

        if (response.Concerns.Count > 0)
        {
            text.Append("**Concerns** — what the verification noticed and did not block on\n\n");
            foreach (Finding concern in response.Concerns)
            {
                string reader = concern.Perspective is { } perspective ? "[" + perspective + "] " : string.Empty;
                text.Append(CultureInfo.InvariantCulture, $"- {reader}{concern.Headline} ({concern.Id})\n");
            }

            text.Append('\n');
        }

        if (deviations.InForce.Count + deviations.Choices.Count > 0)
        {
            text.Append("Merging the pull request accepts the deviations and choices above. To change one, say what to do instead: the build goes round again and updates this pull request.\n");
        }

        return text.ToString();
    }

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);

            if (!Directory.Exists(cycle.Directory))
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"No cycle {request.Id} in this solution."));
            }

            DeviationsState deviations = await CycleDeviations.Read(cycle, cancellationToken).ConfigureAwait(false);

            Result<IReadOnlyList<FindingsRound>> rounds = await FindingsReader
                .ReadHistory(cycle, ArtifactKind.Verify, cancellationToken)
                .ConfigureAwait(false);

            if (rounds.IsFailed)
            {
                return rounds.ToResult<Response>();
            }

            return Result.Ok(new Response(cycle, deviations, FindingsReader.StandingSuggestions(rounds.Value)));
        }
    }
}
