using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Approvals;

/// <summary>
/// Writes a drafted gate decision beside the ledger, for a person to rename onto it.
/// </summary>
/// <remarks>
/// <para><strong>The binary writes the draft, never a skill.</strong> An agent allowed to write the
/// draft would be authoring approval text with its own hands, and the hook would need a rule saying
/// which file name an agent may write near the ledger — the first conditional hook rule, which D-09
/// says is where a bypass would live. Here the rule set does not move: every agent write to the ledger
/// is denied, and this is a command a person's shell ran.</para>
/// <para><strong>There is deliberately no command that puts it in place.</strong> The rename is the
/// act, and an <c>sdlc approve</c> would be exactly the exception D-09 refuses to create. Nothing in
/// this file knows how to reach <see cref="Cycle.ApprovalPath"/>.</para>
/// </remarks>
public static class WriteApprovalDraft
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddWriteApprovalDraftRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, ApprovalText.Response>, Handler>();
    }

    /// <summary>Asks for a decision to be drafted to disk.</summary>
    /// <param name="Id">The cycle the decision is about.</param>
    /// <param name="By">Who is deciding, from the git identity.</param>
    /// <param name="Decision">What is being recorded.</param>
    public sealed record Command(CycleId Id, string By, GateDecision Decision = GateDecision.Approved)
        : IRequest<ApprovalText.Response>;

    /// <summary>
    /// Composes the decision and writes it where a rename can reach it.
    /// </summary>
    /// <param name="mediator">How the text is composed. This adds a write and decides nothing.</param>
    private sealed class Handler(Mediator mediator) : IRequestHandler<Command, ApprovalText.Response>
    {
        /// <inheritdoc />
        public async Task<Result<ApprovalText.Response>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<ApprovalText.Response> composed = await mediator
                .Send<ApprovalText.Query, ApprovalText.Response>(
                    new ApprovalText.Query(request.Id, request.By, request.Decision),
                    cancellationToken)
                .ConfigureAwait(false);

            if (composed.IsFailed)
            {
                return composed;
            }

            ApprovalText.Response text = composed.Value;

            // A refusal produces no file. Writing an empty draft would leave something to rename for a
            // cycle the gate would refuse anyway, and a draft on disk reads as a decision waiting.
            if (!text.MayApprove || text.FileText is null || text.DraftPath is null)
            {
                return composed;
            }

            await File.WriteAllTextAsync(text.DraftPath, text.FileText, cancellationToken)
                .ConfigureAwait(false);

            return composed;
        }
    }
}
