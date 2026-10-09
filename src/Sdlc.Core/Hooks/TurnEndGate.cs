using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Hooks;

/// <summary>Says whether an agent may finish its turn, by reading what the build recorded.</summary>
public static class TurnEndGate
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddTurnEndGateRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, HookVerdict>, Handler>();
    }

    /// <summary>Asks whether an agent may finish its turn.</summary>
    /// <param name="Turn">The turn that is ending.</param>
    public sealed record Query(TurnEnd Turn) : IRequest<HookVerdict>;

    /// <summary>
    /// Decides whether a turn may end, by reading what the build recorded.
    /// </summary>
    /// <remarks>
    /// <para><strong>Why this exists.</strong> Every rule the compiler can hold -- an analyzer, a
    /// severity, an architecture test -- is only as strong as something that runs the build before the
    /// turn ends. Without that, a red build is discovered by the next person to type <c>dotnet test</c>,
    /// and the layer that was meant to shape what an agent writes becomes a layer that reports on it
    /// afterwards. This is what makes those rules bind: an agent cannot report done over a failing
    /// suite, so it fixes its own output first.</para>
    ///
    /// <para><strong>It does not run a build.</strong> That would put a stack choice inside the gate,
    /// which <see cref="TestSummaryReader"/> states the rule against: the gate must not know about test
    /// platforms. So it reads the normalised evidence the build stage already writes, which every stack
    /// produces in the same shape.</para>
    ///
    /// <para><strong>Most of what it does is allow.</strong> A governance hook that met a person
    /// finishing a thought would be worse than none, so it says nothing off a cycle branch, nothing on a
    /// cycle that has no directory or was abandoned, nothing before a plan exists, nothing when only the
    /// framework's own files moved, and nothing when the host is already continuing because it was sent
    /// back once. Three rules refuse: a recorded suite with a failure, code written with no evidence
    /// recorded at all, and code that has moved since the evidence was recorded.</para>
    ///
    /// <para><strong>The holes, stated rather than hidden.</strong> An agent can satisfy this by running
    /// <c>sdlc evidence</c> against a stale results file; and once a patch is recorded, a file created
    /// and never staged afterwards does not move the comparison, because <c>git diff</c> cannot see an
    /// untracked file and so the recorded patch never held it either. Both are the local layer behaving
    /// exactly as designed -- bypassable by anyone with a shell -- and <c>verify-chain</c> in the
    /// pipeline is the guarantee, because it re-reads the evidence and checks that every test the plan
    /// named actually passed. Closing either hole here would mean running the build, and that is the
    /// trade this refuses.</para>
    /// </remarks>
    /// <param name="solution">The solution being governed.</param>
    /// <param name="repository">How to ask git what the tree looks like.</param>
    private sealed class Handler(SolutionRoot solution, IRepository repository)
        : IRequestHandler<Query, HookVerdict>
    {
        /// <inheritdoc />
        public async Task<Result<HookVerdict>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // The loop guard comes first, before any question that could answer deny. The host sets this
            // when it is already continuing because a previous turn end was refused, and refusing again
            // is a turn with no end rather than a governed stop.
            if (request.Turn.AlreadyContinuing)
            {
                return Result.Ok(HookVerdict.Allowed);
            }

            if (!CycleId.TryParseBranch(repository.CurrentBranch(solution.Path), out CycleId? id))
            {
                // Not on a cycle. A spike, a document, a configuration change -- none of it is governed
                // work, and a governance hook that fired there would be obstructive for no gain.
                return Result.Ok(HookVerdict.Allowed);
            }

            Cycle cycle = new(id.Value, solution.Path);

            if (!cycle.Exists || cycle.IsAbandoned)
            {
                // The branch exists and the cycle does not, or it was stopped deliberately. Whether that
                // is a problem is 'sdlc gate' and 'sdlc pr-ready' business; this is not the place to
                // answer it, and answering it here would mean two rules about the same thing.
                return Result.Ok(HookVerdict.Allowed);
            }

            if (!File.Exists(cycle.PathOf(ArtifactKind.Plan)))
            {
                // The planning stages legitimately end turns with nothing built. Interrogation runs
                // uncapped rounds, and a hook demanding evidence from a cycle that has not reached a plan
                // would make the artifact loop impossible to run.
                return Result.Ok(HookVerdict.Allowed);
            }

            string evidence = cycle.EvidenceDirectory;
            string summaryPath = Path.Combine(evidence, RecordEvidence.SummaryFileName);
            string diffPath = Path.Combine(evidence, RecordEvidence.DiffFileName);
            string basePath = Path.Combine(evidence, RecordEvidence.BaseFileName);

            if (File.Exists(summaryPath))
            {
                string json = await File.ReadAllTextAsync(summaryPath, cancellationToken).ConfigureAwait(false);
                Result<TestSummary> read = TestSummaryReader.Read(json, summaryPath);

                // An unreadable summary is not a red suite. Saying so would turn a malformed file into a
                // claim about the tests, and the honest answer is that nothing is known -- which the
                // rules below will reach by a different route if code has moved.
                if (read.IsSuccess && read.Value.Failed > 0)
                {
                    return Result.Ok(Deny(
                        id.Value,
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{read.Value.Failed} of {read.Value.Tests.Count} tests failed in the evidence recorded for cycle {id.Value}.")
                        + " A failing build is a build to fix, and adjusting a test to make it pass is the"
                        + " inversion the whole design exists to prevent. Fix the code, run the suite, then"
                        + " record what it did."));
                }
            }

            string? baseCommit = await ReadBase(basePath, cancellationToken).ConfigureAwait(false)
                ?? repository.MergeBase(solution.Path, id.Value.BranchName());

            if (baseCommit is null)
            {
                // git cannot say where the branch diverged, so there is nothing to compare against. The
                // build stage reports this properly when it runs; refusing a turn over it would be a
                // refusal nobody can act on from here.
                return Result.Ok(HookVerdict.Allowed);
            }

            if (!File.Exists(diffPath))
            {
                // The changed-file list rather than the patch, because a patch does not see an untracked
                // file and writing new files is most of what building a feature is. The repository
                // interface already draws this distinction for the build stage's own comparison, and
                // asking the other question here would miss the case this rule is best at catching.
                if (!repository.ChangedFiles(solution.Path, baseCommit).Any(IsCode))
                {
                    return Result.Ok(HookVerdict.Allowed);
                }

                return Result.Ok(Deny(
                    id.Value,
                    $"Cycle {id.Value} has changes in the working tree and no recorded evidence."
                    + " Verify reads only what the build records, so a change nothing recorded is a change"
                    + " nothing can judge."));
            }

            // Asked with the same exclusion the recorder uses, so the two patches are answers to one
            // question. CodeOnly still runs over both: it is what keeps a patch recorded before the
            // recorder took the exclusion comparable with one taken after.
            string working = repository.Diff(solution.Path, baseCommit, [Cycle.SdlcDirectoryName])
                ?? string.Empty;

            string recorded = await File.ReadAllTextAsync(diffPath, cancellationToken).ConfigureAwait(false);

            if (!string.Equals(CodeOnly(recorded), CodeOnly(working), StringComparison.Ordinal))
            {
                return Result.Ok(Deny(
                    id.Value,
                    $"The working tree has moved since the evidence for cycle {id.Value} was recorded, so"
                    + " the recorded diff describes something other than what is on disk. Run the build and"
                    + " the suite again over what is there now."));
            }

            return Result.Ok(HookVerdict.Allowed);
        }

        /// <summary>
        /// Phrases a refusal, with the command that answers it.
        /// </summary>
        /// <remarks>
        /// Every refusal names the next step. The framework holds its own gate to that -- a refusal that
        /// does not say what has to change is what <c>CON_10.2</c> forbids -- and a hook is read in worse
        /// conditions than a gate verdict, because it arrives in the middle of somebody else's work.
        /// </remarks>
        private static HookVerdict Deny(CycleId id, string why) => new(
            HookDecision.Deny,
            why
            + Environment.NewLine
            + Environment.NewLine
            + string.Create(
                CultureInfo.InvariantCulture,
                $"Run the build and the tests, then 'sdlc evidence {id} --tests <results file> --build-log <log>'.")
            + Environment.NewLine
            + string.Create(CultureInfo.InvariantCulture, $"'sdlc status {id}' says where the cycle stands."));

        private static async Task<string?> ReadBase(string path, CancellationToken cancellationToken)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            string text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            string trimmed = text.Trim();

            return trimmed.Length == 0 ? null : trimmed;
        }

        /// <summary>
        /// Whether a changed path is code rather than the framework's own paperwork.
        /// </summary>
        /// <remarks>
        /// A cycle's artifacts live inside the repository and are untracked until somebody commits them,
        /// so writing an intent makes the change list non-empty. Without this the gate demanded build
        /// evidence from a planning turn, which was the first thing a real repository showed.
        /// </remarks>
        private static bool IsCode(string path) =>
            !path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Contains(
                Cycle.SdlcDirectoryName,
                StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A patch with the framework's own files taken out, and line endings flattened.
        /// </summary>
        /// <remarks>
        /// <para>Two reasons, and both are false-refusal reasons. The recorded diff went through a file
        /// write and git's output did not, so on Windows two identical patches differ by line ending
        /// alone. And the verify stage writes <c>verify.md</c> after the evidence was recorded, which
        /// moves the raw patch and would refuse a turn in the ordinary flow of every cycle.</para>
        /// <para>So both questions this handler asks are about code, asked the same way. A rule that
        /// fires during normal work is a rule somebody switches off, and then the case it was for goes
        /// uncaught with it.</para>
        /// </remarks>
        private static string CodeOnly(string patch)
        {
            StringBuilder kept = new();
            bool keeping = true;

            foreach (string line in patch.ReplaceLineEndings("\n").Split('\n'))
            {
                if (line.StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    keeping = !line.Contains(
                        " a/" + Cycle.SdlcDirectoryName + "/",
                        StringComparison.OrdinalIgnoreCase);
                }

                if (keeping)
                {
                    kept.Append(line).Append('\n');
                }
            }

            return kept.ToString().TrimEnd('\n');
        }
    }
}
