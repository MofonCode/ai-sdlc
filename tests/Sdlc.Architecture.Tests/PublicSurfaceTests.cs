using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sdlc.Core;

namespace Sdlc.Architecture.Tests;

/// <summary>
/// The types <c>Sdlc.Core</c> exposes, listed, so adding one is a decision.
/// </summary>
/// <remarks>
/// <para><strong>Types rather than members, and that is the deliberate difference from the same rule in
/// <c>MofonCode.Core</c>.</strong> That package's surface is its product, so a removed member there is
/// a breaking change somebody inherits and the list goes down to member level. This library ships
/// inside one tool: what matters is that a new type becoming public is noticed, not that a record
/// gained a property. Member-level here would churn on every change and teach people to update the
/// list without reading it.</para>
///
/// <para><strong>What this is not.</strong> It does not establish that everything public needs to be.
/// Thirty-five of these are not named anywhere in <c>Sdlc.Cli</c>, and some of those are public only
/// because something public exposes them — a return type cannot be less accessible than the method
/// returning it. Deciding which are genuinely needed wants the metadata tables of <c>sdlc.dll</c>
/// read for what it actually references, and then an internalising pass with
/// <c>InternalsVisibleTo</c> for the suite. That is its own change; this is the rule that stops the
/// surface growing further while nobody is looking.</para>
/// </remarks>
[TestClass]
public sealed class PublicSurfaceTests
{
    /// <summary>
    /// Every type this library exposes.
    /// </summary>
    /// <remarks>
    /// Add a line in the same change that makes a type public, and say in the commit why the surface
    /// grew. Removing one is ordinary here — this library is not a package — but it still has to be
    /// deliberate, because <c>Sdlc.Cli</c> or the suite may be using it.
    /// </remarks>
    private static readonly string[] _approved =
    [
        "Sdlc.Core.Approvals.ApprovalGate",
        "Sdlc.Core.Approvals.ApprovalGate+DocumentHash",
        "Sdlc.Core.Approvals.ApprovalGate+GateRefusal",
        "Sdlc.Core.Approvals.ApprovalGate+Query",
        "Sdlc.Core.Approvals.ApprovalGate+Response",
        "Sdlc.Core.Approvals.ApprovalLedger",
        "Sdlc.Core.Approvals.ApprovalRenderer",
        "Sdlc.Core.Approvals.ApprovalText",
        "Sdlc.Core.Approvals.ApprovalText+Query",
        "Sdlc.Core.Approvals.ApprovalText+Response",
        "Sdlc.Core.Approvals.GateDecision",
        "Sdlc.Core.Approvals.GateEntry",
        "Sdlc.Core.Artifacts.ArtifactOutline",
        "Sdlc.Core.Artifacts.ArtifactOutlineReader",
        "Sdlc.Core.Artifacts.ArtifactSchema",
        "Sdlc.Core.Artifacts.ArtifactSections",
        "Sdlc.Core.Artifacts.ArtifactValidation",
        "Sdlc.Core.Artifacts.ContentHash",
        "Sdlc.Core.Artifacts.Frontmatter",
        "Sdlc.Core.Artifacts.FrontmatterDocument",
        "Sdlc.Core.Artifacts.SchemaViolation",
        "Sdlc.Core.Chain.ChainRenderer",
        "Sdlc.Core.Chain.VerifyChain",
        "Sdlc.Core.Chain.VerifyChain+ChainCheck",
        "Sdlc.Core.Chain.VerifyChain+Query",
        "Sdlc.Core.Chain.VerifyChain+Response",
        "Sdlc.Core.Constraints.Constraint",
        "Sdlc.Core.Constraints.ConstraintDetail",
        "Sdlc.Core.Constraints.ConstraintDocument",
        "Sdlc.Core.Constraints.ConstraintId",
        "Sdlc.Core.Constraints.ConstraintParser",
        "Sdlc.Core.Constraints.ConstraintRenderer",
        "Sdlc.Core.Constraints.ConstraintText",
        "Sdlc.Core.Constraints.ConstraintText+Query",
        "Sdlc.Core.Constraints.ConstraintTier",
        "Sdlc.Core.Constraints.EffectiveConstraints",
        "Sdlc.Core.Constraints.EffectiveConstraints+Query",
        "Sdlc.Core.Constraints.EffectiveConstraints+Response",
        "Sdlc.Core.Constraints.FileConstraintDocuments",
        "Sdlc.Core.Constraints.IConstraintDocuments",
        "Sdlc.Core.Cycles.AbandonRecord",
        "Sdlc.Core.Cycles.AbandonText",
        "Sdlc.Core.Cycles.AbandonText+Query",
        "Sdlc.Core.Cycles.ArtifactKind",
        "Sdlc.Core.Cycles.Cycle",
        "Sdlc.Core.Cycles.CycleId",
        "Sdlc.Core.Cycles.CycleIndex",
        "Sdlc.Core.Cycles.NewCycle",
        "Sdlc.Core.Cycles.NewCycle+Command",
        "Sdlc.Core.Cycles.ReadCycleStatus",
        "Sdlc.Core.Cycles.ReadCycleStatus+ArtifactState",
        "Sdlc.Core.Cycles.ReadCycleStatus+Query",
        "Sdlc.Core.Cycles.ReadCycleStatus+Response",
        "Sdlc.Core.Cycles.StagePanel",
        "Sdlc.Core.Cycles.StatusRenderer",
        "Sdlc.Core.Drafts.DraftRenderer",
        "Sdlc.Core.Drafts.DraftResult",
        "Sdlc.Core.Evidence.ClaimGraphRenderer",
        "Sdlc.Core.Evidence.ComputeClaimGraph",
        "Sdlc.Core.Evidence.ComputeClaimGraph+Claim",
        "Sdlc.Core.Evidence.ComputeClaimGraph+Collision",
        "Sdlc.Core.Evidence.ComputeClaimGraph+Query",
        "Sdlc.Core.Evidence.ComputeClaimGraph+Response",
        "Sdlc.Core.Evidence.DeclaredFiles",
        "Sdlc.Core.Evidence.EvidenceRenderer",
        "Sdlc.Core.Evidence.FileComparison",
        "Sdlc.Core.Evidence.RecordEvidence",
        "Sdlc.Core.Evidence.RecordEvidence+Command",
        "Sdlc.Core.Evidence.RecordEvidence+Response",
        "Sdlc.Core.Evidence.RecordedTest",
        "Sdlc.Core.Evidence.TestOutcome",
        "Sdlc.Core.Evidence.TestSummary",
        "Sdlc.Core.Evidence.TestSummaryReader",
        "Sdlc.Core.Findings.Disposition",
        "Sdlc.Core.Findings.Finding",
        "Sdlc.Core.Findings.FindingsReader",
        "Sdlc.Core.Findings.FindingsRound",
        "Sdlc.Core.Findings.RemediationBudget",
        "Sdlc.Core.Findings.RemediationState",
        "Sdlc.Core.Findings.Severity",
        "Sdlc.Core.Findings.Suggestion",
        "Sdlc.Core.Findings.Suggestions",
        "Sdlc.Core.Findings.Suggestions+Query",
        "Sdlc.Core.Findings.Suggestions+Response",
        "Sdlc.Core.Findings.SuggestionsRenderer",
        "Sdlc.Core.Git.GitRepository",
        "Sdlc.Core.Git.IRepository",
        "Sdlc.Core.Hooks.HookDecision",
        "Sdlc.Core.Hooks.HookEvent",
        "Sdlc.Core.Hooks.HookHost",
        "Sdlc.Core.Hooks.HookPolicy",
        "Sdlc.Core.Hooks.HookProtocol",
        "Sdlc.Core.Hooks.HookVerdict",
        "Sdlc.Core.Hooks.ProtectedFile",
        "Sdlc.Core.Hooks.ProtectedPaths",
        "Sdlc.Core.Hooks.ToolCall",
        "Sdlc.Core.Hooks.ToolKind",
        "Sdlc.Core.Hooks.TurnEnd",
        "Sdlc.Core.Hooks.TurnEndGate",
        "Sdlc.Core.Hooks.TurnEndGate+Query",
        "Sdlc.Core.Init.InitRenderer",
        "Sdlc.Core.Init.Initialise",
        "Sdlc.Core.Init.Initialise+Command",
        "Sdlc.Core.Init.Initialise+InitAction",
        "Sdlc.Core.Init.Initialise+InitChange",
        "Sdlc.Core.Init.Initialise+InitMode",
        "Sdlc.Core.Init.Initialise+Response",
        "Sdlc.Core.Init.Platform",
        "Sdlc.Core.Init.Scaffold",
        "Sdlc.Core.Init.Templates",
        "Sdlc.Core.Landing.Land",
        "Sdlc.Core.Landing.Land+LandRefusal",
        "Sdlc.Core.Landing.Land+Query",
        "Sdlc.Core.Landing.Land+Response",
        "Sdlc.Core.Landing.LandRenderer",
        "Sdlc.Core.SdlcServices",
        "Sdlc.Core.Solutions.ProfileText",
        "Sdlc.Core.Solutions.ProfileText+Query",
        "Sdlc.Core.Solutions.SolutionDocument",
        "Sdlc.Core.Solutions.SolutionOrigin",
        "Sdlc.Core.Solutions.SolutionRoot",
    ];

    [TestMethod]
    public void The_public_surface_of_the_core_is_exactly_what_was_approved()
    {
        string[] actual = Surface();

        Assert.IsTrue(
            actual.Length > 0,
            "Nothing was enumerated, so this rule would pass however the library looked. The likely "
            + "cause is an assembly that did not load.");

        string[] added = [.. actual.Except(_approved, StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        string[] removed = [.. _approved.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal)];

        Assert.IsTrue(added.Length == 0 && removed.Length == 0, Describe(added, removed, actual));
    }

    private static string[] Surface() =>
        [.. typeof(SdlcServices).Assembly
            .GetExportedTypes()
            .Where(type => type.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .Select(type => type.FullName ?? type.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private static string Describe(string[] added, string[] removed, string[] actual)
    {
        List<string> parts = [];

        if (added.Length > 0)
        {
            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{added.Length} type(s) became public and are not approved:\n  ")
                + string.Join("\n  ", added)
                + "\n\nIf the type is only needed inside this library, make it internal. If the suite "
                + "needs it, InternalsVisibleTo is the answer rather than public. If it is genuinely "
                + "part of what Sdlc.Cli uses, add it below and say why in the commit.");
        }

        if (removed.Length > 0)
        {
            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{removed.Length} approved type(s) no longer exist:\n  ")
                + string.Join("\n  ", removed)
                + "\n\nDelete the lines so the list keeps describing reality. A list that permits what "
                + "is not there is a wish rather than a record.");
        }

        parts.Add("The surface as it stands, for the approved list:\n\n"
            + string.Join("\n", actual.Select(line => "        \"" + line + "\",")));

        return string.Join("\n\n", parts);
    }
}
