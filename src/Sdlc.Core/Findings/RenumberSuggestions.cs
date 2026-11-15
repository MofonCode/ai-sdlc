using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Findings;

/// <summary>
/// Gives a suggestion id a merge recorded twice the next free id, so the ledger reads again (D-171).
/// </summary>
/// <remarks>
/// <para><strong>The ledger is the binary's to write.</strong> The hook keeps an agent out of it, so the refusal
/// that found a duplicate asked a person to renumber JSON by hand, and the one time it happened an agent did it at
/// the merge. Which id moves and to what is decided by the file alone, so the binary does it.</para>
/// <para><strong>The id format does not change</strong> (D-162 rejected that): the first in the file keeps the id,
/// each later one takes the next free number under its prefix.</para>
/// </remarks>
public static class RenumberSuggestions
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddRenumberSuggestionsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Renumbers one cycle's duplicate suggestion ids.</summary>
    /// <param name="Id">The cycle.</param>
    public sealed record Command(CycleId Id) : IRequest<Response>;

    /// <summary>What moved.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Moved">Each suggestion that took a new id; empty when every id was recorded once.</param>
    public sealed record Response(CycleId Cycle, IReadOnlyList<RenumberedSuggestion> Moved);

    /// <summary>Renders what moved.</summary>
    /// <param name="response">What moved.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Moved.Count == 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Every suggestion id in {response.Cycle} is recorded once. Nothing moved.\n");
        }

        StringBuilder text = new();
        text.Append(CultureInfo.InvariantCulture, $"{response.Moved.Count} suggestion{(response.Moved.Count == 1 ? string.Empty : "s")} in {response.Cycle} shared an id with an earlier one and took the next free id:\n");

        foreach (RenumberedSuggestion moved in response.Moved)
        {
            text.Append(CultureInfo.InvariantCulture, $"  {moved.From} -> {moved.To}, {moved.Raised}\n");
        }

        text.Append("Commit the ledger with the merge that brought the two together.\n");
        return text.ToString();
    }

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);
            string path = cycle.SuggestionsLedgerPath;

            if (!File.Exists(path))
            {
                return Result.Ok(new Response(request.Id, []));
            }

            Result<(SuggestionLedger Ledger, IReadOnlyList<RenumberedSuggestion> Moved)> renumbered = FindingsReader.Renumber(
                await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
                path);

            if (renumbered.IsFailed)
            {
                return renumbered.ToResult<Response>();
            }

            if (renumbered.Value.Moved.Count > 0)
            {
                await File.WriteAllTextAsync(path, FindingsReader.Write(renumbered.Value.Ledger), cancellationToken).ConfigureAwait(false);
            }

            return Result.Ok(new Response(request.Id, renumbered.Value.Moved));
        }
    }
}
