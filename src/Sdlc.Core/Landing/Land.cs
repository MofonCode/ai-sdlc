using System.Globalization;
using FluentResults;
using MofonCode.Core;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Landing;

/// <summary>Why a cycle may not be landed.</summary>
public enum LandRefusal
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

/// <summary>Asks whether a cycle may be landed, without landing it.</summary>
/// <param name="Id">The cycle to check.</param>
public sealed record LandableQuery(CycleId Id) : IRequest<Landable>;

/// <summary>Whether a cycle may be landed, and why not.</summary>
/// <param name="Cycle">The cycle.</param>
/// <param name="Refusal">Why not, or <see cref="LandRefusal.None"/>.</param>
/// <param name="Explanation">What to tell the person.</param>
/// <param name="Chain">What the chain check found, when it was reached.</param>
public sealed record Landable(
    Cycle Cycle,
    LandRefusal Refusal,
    string Explanation,
    ChainVerdict? Chain)
{
    /// <summary>Whether the cycle may be landed.</summary>
    public bool MayLand => Refusal == LandRefusal.None;
}

/// <summary>
/// Decides whether a cycle may be landed.
/// </summary>
/// <remarks>
/// Landing is the only thing that touches the remote, which is why this is a question of its own rather
/// than part of the build. Three things it checks beyond the chain:
/// <para>
/// <strong>The verdict is a pass.</strong> Not "no failures recorded" -- an actual pass. The verdict is the
/// only thing standing between unattended building and unreviewed work reaching a pull request.
/// </para>
/// <para>
/// <strong>An escalated cycle never lands.</strong> A cycle that could not be made to pass within its
/// remediation budget stops, and that stopping is what makes an unattended batch safe to run at all.
/// </para>
/// <para>
/// <strong>The chain is re-checked rather than trusted.</strong> A verdict recorded an hour ago says
/// nothing about what is on disk now, and the pull-request check would refuse it anyway -- so refusing here
/// costs a person nothing and saves a round trip through CI.
/// </para>
/// </remarks>
/// <param name="solution">The solution being landed from.</param>
public sealed class LandableHandler(SolutionRoot solution) : IRequestHandler<LandableQuery, Landable>
{
    /// <inheritdoc />
    public async Task<Result<Landable>> Handle(LandableQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Cycle cycle = new(request.Id, solution.Path);
        string verify = cycle.PathOf(ArtifactKind.Verify);

        if (!cycle.Exists || !File.Exists(verify))
        {
            return Verdict(cycle, LandRefusal.NotVerified, string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {cycle.Id} has not been verified. There is nothing to land until a verification says what the build did."));
        }

        Result<RemediationState> budget = await RemediationBudget
            .Read(cycle, cancellationToken)
            .ConfigureAwait(false);

        if (budget.IsFailed)
        {
            return budget.ToResult<Landable>();
        }

        if (budget.Value.Escalated)
        {
            return Verdict(cycle, LandRefusal.Escalated, string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {cycle.Id} is escalated: {budget.Value.Spent} remediation passes spent and {budget.Value.OpenBlockers.Count} finding{(budget.Value.OpenBlockers.Count == 1 ? string.Empty : "s")} still open. A cycle that could not be made to pass within its budget stops rather than landing, and that stopping is what makes an unattended batch safe to run."));
        }

        Result<ChainVerdict> chain = await new VerifyChainHandler(solution)
            .Handle(new VerifyChainQuery(request.Id), cancellationToken)
            .ConfigureAwait(false);

        if (chain.IsFailed)
        {
            return chain.ToResult<Landable>();
        }

        // The outcome is read through the chain rather than parsed again here, so the local refusal and the
        // merge check cannot disagree about what a pass is.
        ChainCheck? verified = chain.Value.Checks
            .FirstOrDefault(check => check.Name.Contains("verified", StringComparison.Ordinal));

        if (verified is { Passed: false })
        {
            return Verdict(
                cycle,
                LandRefusal.NotPassed,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} did not pass verification. {verified.Detail}"),
                chain.Value);
        }

        if (!chain.Value.Holds)
        {
            return Verdict(
                cycle,
                LandRefusal.ChainBroken,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} verified, and its chain does not hold. The pull-request check would refuse it, so refusing here saves a round trip:{Environment.NewLine}{string.Join(Environment.NewLine, chain.Value.Failures.Select(failure => "  - " + failure.Name))}"),
                chain.Value);
        }

        return Verdict(
            cycle,
            LandRefusal.None,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Cycle {cycle.Id} verified and its chain holds."),
            chain.Value);
    }

    private static Result<Landable> Verdict(
        Cycle cycle,
        LandRefusal refusal,
        string explanation,
        ChainVerdict? chain = null) =>
        Result.Ok(new Landable(cycle, refusal, explanation, chain));
}
