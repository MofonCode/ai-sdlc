using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.PullRequests;

/// <summary>Says whether a cycle may open a pull request, without opening one.</summary>
public static class PullRequest
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddPullRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Why a cycle may not open a pull request.</summary>
    public enum PullRequestRefusal
    {
        /// <summary>Nothing refuses it.</summary>
        None,

        /// <summary>There is no cycle, or no verify document.</summary>
        NotVerified,

        /// <summary>The verification did not pass.</summary>
        NotPassed,

        /// <summary>Remediation ran out of passes with blockers still open.</summary>
        Escalated,

        /// <summary>The chain does not hold, so the pull-request check would refuse it anyway.</summary>
        ChainBroken,
    }

    /// <summary>Whether a cycle may open a pull request, and why not.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Refusal">Why not, or <see cref="PullRequestRefusal.None"/>.</param>
    /// <param name="Explanation">What to tell the person.</param>
    /// <param name="Chain">What the chain check found, when it was reached.</param>
    public sealed record Response(
        Cycle Cycle,
        PullRequestRefusal Refusal,
        string Explanation,
        VerifyChain.Response? Chain)
    {
        /// <summary>Whether the cycle may open a pull request.</summary>
        public bool MayOpen => Refusal == PullRequestRefusal.None;
    }

    /// <summary>Asks whether a cycle may open a pull request, without opening one.</summary>
    /// <param name="Id">The cycle to check.</param>
    public sealed record Query(CycleId Id) : IRequest<Response>;

    /// <summary>
    /// Decides whether a cycle may open a pull request.
    /// </summary>
    /// <remarks>
    /// Opening a pull request is the only thing that touches the remote, which is why this is a question of its own rather
    /// than part of the build. Three things it checks beyond the chain:
    /// <para>
    /// <strong>The verdict is a pass.</strong> Not "no failures recorded" -- an actual pass. The verdict is the
    /// only thing standing between unattended building and unreviewed work reaching a pull request.
    /// </para>
    /// <para>
    /// <strong>An escalated cycle never opens one.</strong> A cycle that could not be made to pass within its
    /// remediation budget stops, and that stopping is what makes an unattended batch safe to run at all.
    /// </para>
    /// <para>
    /// <strong>The chain is re-checked rather than trusted.</strong> A verdict recorded an hour ago says
    /// nothing about what is on disk now, and the pull-request check would refuse it anyway -- so refusing here
    /// costs a person nothing and saves a round trip through CI.
    /// </para>
    /// </remarks>
    /// <param name="solution">The solution the pull request is opened from.</param>
    private sealed class Handler(SolutionRoot solution, Mediator mediator) : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);
            string verify = cycle.PathOf(ArtifactKind.Verify);

            if (!cycle.Exists || !File.Exists(verify))
            {
                return Verdict(cycle, PullRequestRefusal.NotVerified, string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has not been verified. There is nothing to open a pull request for until a verification says what the build did."));
            }

            Result<RemediationState> budget = await RemediationBudget
                .Read(cycle, cancellationToken)
                .ConfigureAwait(false);

            if (budget.IsFailed)
            {
                return budget.ToResult<Response>();
            }

            if (budget.Value.Escalated)
            {
                // The phrase comes from RemediationBudget.Describe, which exists to build exactly
                // this and was built here a second time instead -- including its own pluralisation,
                // which is what a duplicated sentence always grows.
                return Verdict(cycle, PullRequestRefusal.Escalated, string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} is {RemediationBudget.Describe(budget.Value)}. A cycle that has stopped making progress stops rather than opening a pull request, and that stopping is what makes an unattended batch safe to run."));
            }

            Result<VerifyChain.Response> chain = await mediator
                .Send<VerifyChain.Query, VerifyChain.Response>(
                    new VerifyChain.Query(request.Id),
                    cancellationToken)
                .ConfigureAwait(false);

            if (chain.IsFailed)
            {
                return chain.ToResult<Response>();
            }

            // The outcome is read through the chain rather than parsed again here, so the local refusal and the
            // merge check cannot disagree about what a pass is.
            VerifyChain.ChainCheck? verified = chain.Value.Checks
                .FirstOrDefault(check => check.Name.Contains("verified", StringComparison.Ordinal));

            if (verified is { Passed: false })
            {
                return Verdict(
                    cycle,
                    PullRequestRefusal.NotPassed,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Cycle {cycle.Id} did not pass verification. {verified.Detail}"),
                    chain.Value);
            }

            if (!chain.Value.Holds)
            {
                return Verdict(
                    cycle,
                    PullRequestRefusal.ChainBroken,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Cycle {cycle.Id} verified, and its chain does not hold. The pull-request check would refuse it, so refusing here saves a round trip:{Environment.NewLine}{string.Join(Environment.NewLine, chain.Value.Failures.Select(failure => "  - " + failure.Name))}"),
                    chain.Value);
            }

            return Verdict(
                cycle,
                PullRequestRefusal.None,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} verified and its chain holds."),
                chain.Value);
        }

        private static Result<Response> Verdict(
            Cycle cycle,
            PullRequestRefusal refusal,
            string explanation,
            VerifyChain.Response? chain = null) =>
            Result.Ok(new Response(cycle, refusal, explanation, chain));
    }
}
