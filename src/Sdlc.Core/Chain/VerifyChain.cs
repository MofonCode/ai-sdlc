using System.Globalization;
using FluentResults;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;

namespace Sdlc.Core.Chain;

/// <summary>What one check in the chain concluded.</summary>
/// <param name="Name">What was checked, as a short label.</param>
/// <param name="Passed">Whether it held.</param>
/// <param name="Detail">What held, or what did not and where.</param>
public sealed record ChainCheck(string Name, bool Passed, string Detail);

/// <summary>Asks whether a cycle's artifact chain is complete, approved and hash-matched.</summary>
/// <param name="Id">The cycle to re-establish.</param>
public sealed record VerifyChainQuery(CycleId Id) : IRequest<ChainVerdict>;

/// <summary>What the chain check found.</summary>
/// <param name="Cycle">The cycle that was checked.</param>
/// <param name="Checks">Every check, in the order they ran.</param>
public sealed record ChainVerdict(Cycle Cycle, IReadOnlyList<ChainCheck> Checks)
{
    /// <summary>Whether every check held.</summary>
    public bool Holds => Checks.All(check => check.Passed);

    /// <summary>The checks that did not hold.</summary>
    public IReadOnlyList<ChainCheck> Failures => [.. Checks.Where(check => !check.Passed)];
}

/// <summary>
/// Re-establishes a cycle's chain from the repository rather than from any agent's word.
/// </summary>
/// <remarks>
/// This is the non-bypassable layer, and the only one of the three that is a guarantee. The advisory
/// layer is bypassable by the agent constantly and the local layer by any human with a shell; this runs
/// as a pull-request check behind branch policy, so nothing merges unless the chain is complete,
/// approved and hash-matched.
/// <para>
/// Every check lives here rather than in pipeline YAML, which is what keeps each platform's pipeline
/// about thirty lines wrapping one command. Two implementations of the guarantee would eventually
/// disagree, and the one a person reads would not be the one that blocks the merge.
/// </para>
/// <para>
/// It runs every check rather than stopping at the first failure. A pull request sent back once with
/// three problems costs less than three round trips, and the checks are independent.
/// </para>
/// </remarks>
/// <param name="solutionRoot">The root of the solution being checked.</param>
public sealed class VerifyChainHandler(string solutionRoot)
    : IRequestHandler<VerifyChainQuery, ChainVerdict>
{
    /// <summary>
    /// What this cannot establish, stated in its own output.
    /// </summary>
    /// <remarks>
    /// With a local, hand-pasted approval there is no way to prove from the repository that a second
    /// person approved: the entry names whoever typed it. A check that implied otherwise would be worse
    /// than one that admits it, because a team would stop looking for the thing it was not providing.
    /// </remarks>
    public const string Caveat =
        "This proves the approval entry exists and that the three documents still hash to what it "
        + "recorded. It cannot prove a second person approved: the entry names whoever wrote it. "
        + "Code-owner review on the pull request is what provides that, and nothing here replaces it.";

    /// <inheritdoc />
    public async Task<Result<ChainVerdict>> Handle(
        VerifyChainQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Cycle cycle = new(request.Id, solutionRoot);
        List<ChainCheck> checks = [];

        if (!cycle.Exists)
        {
            checks.Add(new ChainCheck(
                "the cycle",
                false,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"has no directory at {cycle.Directory}. There is no chain to check.")));

            return Result.Ok(new ChainVerdict(cycle, checks));
        }

        await Documents(cycle, checks, request.Id, cancellationToken).ConfigureAwait(false);
        await Gate(cycle, checks, cancellationToken).ConfigureAwait(false);
        await Proof(cycle, checks, cancellationToken).ConfigureAwait(false);
        await Proven(cycle, checks, cancellationToken).ConfigureAwait(false);
        await Outcome(cycle, checks, cancellationToken).ConfigureAwait(false);

        return Result.Ok(new ChainVerdict(cycle, checks));
    }

    /// <summary>Every artifact in the chain exists, and every one has a sound shape.</summary>
    private static async Task Documents(
        Cycle cycle,
        List<ChainCheck> checks,
        CycleId id,
        CancellationToken cancellationToken)
    {
        List<ArtifactKind> required = [.. Cycle.PlanningDocuments, ArtifactKind.Verify];
        List<string> missing = [];
        List<string> misshapen = [];

        foreach (ArtifactKind kind in required)
        {
            string path = cycle.PathOf(kind);

            if (!File.Exists(path))
            {
                missing.Add(Cycle.FileNameOf(kind));
                continue;
            }

            string text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            ArtifactValidation shape = ArtifactSchema.Validate(kind, text, id);

            misshapen.AddRange(shape.Violations.Select(violation => string.Create(
                CultureInfo.InvariantCulture,
                $"{Cycle.FileNameOf(kind)}: {violation.Where} {violation.Problem}")));
        }

        checks.Add(new ChainCheck(
            "the chain is complete",
            missing.Count == 0,
            missing.Count == 0
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"all {required.Count} artifacts exist")
                : "missing " + string.Join(", ", missing)));

        // Skipped when there is nothing to check. "No shape problems" on four missing documents reads
        // as a pass, and a reader scanning for green ticks would take it as one.
        if (missing.Count < required.Count)
        {
            checks.Add(new ChainCheck(
                "every artifact's shape is sound",
                misshapen.Count == 0,
                misshapen.Count == 0 ? "no shape problems" : string.Join(Environment.NewLine, misshapen)));
        }
    }

    /// <summary>
    /// The approval stands, the hashes match, and no round judged a document that has moved.
    /// </summary>
    /// <remarks>
    /// Delegated to the gate rather than reimplemented. The local gate and the pull-request check have
    /// to agree about what "approved" means, and the only way two implementations stay in agreement is
    /// by there being one.
    /// </remarks>
    private async Task Gate(Cycle cycle, List<ChainCheck> checks, CancellationToken cancellationToken)
    {
        Result<GateVerdict> read = await new ApprovalGateHandler(solutionRoot)
            .Handle(new ApprovalGateQuery(cycle.Id), cancellationToken)
            .ConfigureAwait(false);

        if (read.IsFailed)
        {
            checks.Add(new ChainCheck(
                "the approval",
                false,
                "could not be read. " + string.Join("; ", read.Errors.Select(e => e.Message))));
            return;
        }

        GateVerdict gate = read.Value;

        checks.Add(new ChainCheck(
            "the gate is satisfied",
            gate.MayBuild,
            gate.MayBuild
                ? gate.Explanation
                : string.Create(CultureInfo.InvariantCulture, $"{gate.Refusal}.")
                    + Environment.NewLine
                    + gate.Explanation));
    }

    /// <summary>
    /// Every spec criterion has a proof entry that cites it.
    /// </summary>
    /// <remarks>
    /// Matched by number rather than by prose, because the number is the one part of a criterion that
    /// can be compared without reading comprehension. Whether the proof is <em>adequate</em> to the
    /// criterion is `CON_06`, and that is judgement rather than a check -- which is exactly the split:
    /// this catches a criterion with no proof at all, and no schema could catch a proof that could pass
    /// while its criterion is unmet.
    /// </remarks>
    private static async Task Proof(
        Cycle cycle,
        List<ChainCheck> checks,
        CancellationToken cancellationToken)
    {
        string specPath = cycle.PathOf(ArtifactKind.Spec);
        string planPath = cycle.PathOf(ArtifactKind.Plan);

        if (!File.Exists(specPath) || !File.Exists(planPath))
        {
            // Already reported as a missing document. Saying it twice would read as two problems.
            return;
        }

        IReadOnlyDictionary<int, string> criteria = ArtifactSections.NumberedItems(
            await File.ReadAllTextAsync(specPath, cancellationToken).ConfigureAwait(false),
            "Acceptance criteria");

        IReadOnlyDictionary<int, string> proofs = ArtifactSections.NumberedItems(
            await File.ReadAllTextAsync(planPath, cancellationToken).ConfigureAwait(false),
            "Proof");

        if (criteria.Count == 0)
        {
            checks.Add(new ChainCheck(
                "every criterion has a proof",
                false,
                "the spec has no numbered acceptance criteria. A spec that asserts nothing cannot be met."));
            return;
        }

        List<int> unproven = [.. criteria.Keys.Where(number => !proofs.ContainsKey(number)).Order()];
        List<int> orphaned = [.. proofs.Keys.Where(number => !criteria.ContainsKey(number)).Order()];

        List<string> problems = [];

        if (unproven.Count > 0)
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"criteria with no proof: {string.Join(", ", unproven)}"));
        }

        // A proof citing a criterion that does not exist means one was deleted or renumbered after the
        // plan was written, so the proof that looks complete is proving something else.
        if (orphaned.Count > 0)
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"proofs citing a criterion the spec does not have: {string.Join(", ", orphaned)}"));
        }

        checks.Add(new ChainCheck(
            "every criterion has a proof",
            problems.Count == 0,
            problems.Count == 0
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"{criteria.Count} criteria, each with a proof entry")
                : string.Join(Environment.NewLine, problems)));
    }

    /// <summary>
    /// Every test a proof entry commits to actually ran and passed.
    /// </summary>
    /// <remarks>
    /// This is the check that blocks. A proof entry existing is bookkeeping; the criterion is met when the
    /// test it names passed, and that is read from the normalised summary rather than from anything the
    /// verify document claims. <c>D-15</c> moved the file comparison out of this layer precisely so that
    /// what blocks here is outcome-based.
    /// <para>
    /// A proof entry that names no test is reported rather than skipped. An entry saying "covered by the
    /// integration suite" commits to nothing a reader can check, which is the shape an inadequate proof
    /// takes when it is written in good faith.
    /// </para>
    /// </remarks>
    private static async Task Proven(
        Cycle cycle,
        List<ChainCheck> checks,
        CancellationToken cancellationToken)
    {
        string planPath = cycle.PathOf(ArtifactKind.Plan);

        if (!File.Exists(planPath))
        {
            return;
        }

        IReadOnlyDictionary<int, string> proofs = ArtifactSections.NumberedItems(
            await File.ReadAllTextAsync(planPath, cancellationToken).ConfigureAwait(false),
            "Proof");

        if (proofs.Count == 0)
        {
            return;
        }

        string summaryPath = Path.Combine(cycle.EvidenceDirectory, RecordEvidenceHandler.SummaryFileName);

        if (!File.Exists(summaryPath))
        {
            checks.Add(new ChainCheck(
                "every named test passed",
                false,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"there is no {RecordEvidenceHandler.SummaryFileName} in the evidence. A proof nothing ran is not a proof, and a chain that passed without one would mean 'approved' rather than 'met'.")));

            return;
        }

        Result<TestSummary> read = TestSummaryReader.Read(
            await File.ReadAllTextAsync(summaryPath, cancellationToken).ConfigureAwait(false),
            summaryPath);

        if (read.IsFailed)
        {
            checks.Add(new ChainCheck(
                "every named test passed",
                false,
                string.Join("; ", read.Errors.Select(error => error.Message))));

            return;
        }

        TestSummary summary = read.Value;
        List<string> problems = [];

        foreach ((int number, string entry) in proofs.OrderBy(proof => proof.Key))
        {
            IReadOnlyList<string> named = DeclaredFiles.TestsIn(entry);

            if (named.Count == 0)
            {
                problems.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"proof {number} names no test. Write the test's name in backticks, so there is something to check."));

                continue;
            }

            List<string> absent = [.. named.Where(name => !summary.DidPass(name))];

            if (absent.Count > 0)
            {
                problems.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"proof {number}: {string.Join(", ", absent)} did not run and pass."));
            }
        }

        checks.Add(new ChainCheck(
            "every named test passed",
            problems.Count == 0,
            problems.Count == 0
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"{proofs.Count} proofs, every named test passing out of {summary.Passed} that did")
                : string.Join(Environment.NewLine, problems)));
    }

    /// <summary>The verify document's outcome is a pass.</summary>
    private static async Task Outcome(
        Cycle cycle,
        List<ChainCheck> checks,
        CancellationToken cancellationToken)
    {
        string path = cycle.PathOf(ArtifactKind.Verify);

        if (!File.Exists(path))
        {
            return;
        }

        Result<FrontmatterDocument> split = await Frontmatter
            .ReadFile(path, cancellationToken)
            .ConfigureAwait(false);

        if (split.IsFailed)
        {
            // Already reported as a shape problem.
            return;
        }

        string? outcome = split.Value.Yaml
            .ReplaceLineEndings()
            .Split(Environment.NewLine)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("outcome:", StringComparison.Ordinal))
            ?["outcome:".Length..]
            .Trim();

        bool passed = string.Equals(outcome, "pass", StringComparison.OrdinalIgnoreCase);

        checks.Add(new ChainCheck(
            "the change was verified",
            passed,
            passed
                ? "verify records a pass"
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"verify records '{outcome ?? "nothing"}'. Only a pass merges; blocked goes back to remediate, and escalated means the cycle could not be made to pass.")));
    }
}
