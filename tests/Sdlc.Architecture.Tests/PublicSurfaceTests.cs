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
        "Sdlc.Core.Approvals.AmendedPlan",
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

        // Where a findings ledger stood when a decision was taken (D-107). Public because GateEntry carries it.
        "Sdlc.Core.Approvals.RoundAnchor",

        // A person's answer to one deviation, per entry. Public because GateEntry carries it.
        "Sdlc.Core.Approvals.DeviationAnswer",

        // The command 'sdlc draft approval' dispatches. Public because Sdlc.Cli sends it, like every
        // other operation's request; it returns ApprovalText.Response rather than a shape of its own,
        // because composing the decision and putting it on disk produce the same answer.
        "Sdlc.Core.Approvals.WriteApprovalDraft",
        "Sdlc.Core.Approvals.WriteApprovalDraft+Command",
        "Sdlc.Core.Areas.Area",
        "Sdlc.Core.Areas.AreaHistory",
        "Sdlc.Core.Areas.AreaHistory+Candidate",
        "Sdlc.Core.Areas.AreaHistory+Reading",
        "Sdlc.Core.Areas.AreaHistory+Replay",
        "Sdlc.Core.Areas.AreaParser",
        "Sdlc.Core.Areas.AreaScope",
        "Sdlc.Core.Areas.AreasRenderer",
        "Sdlc.Core.Areas.ReadAreas",
        "Sdlc.Core.Areas.ReadAreas+Query",
        "Sdlc.Core.Areas.ReadAreas+Response",
        "Sdlc.Core.Areas.ReadAreas+Standing",
        "Sdlc.Core.Areas.ReadAreas+Unknown",
        // 'sdlc findings': the operation, the per-artifact shape it returns, and its renderer.
        "Sdlc.Core.Findings.ArtifactFindings",
        "Sdlc.Core.Findings.FindingsRenderer",
        "Sdlc.Core.Findings.ReadFindings",
        "Sdlc.Core.Findings.ReadFindings+Query",
        "Sdlc.Core.Findings.ReadFindings+Response",
        "Sdlc.Core.Proposals.DraftMining",
        "Sdlc.Core.Proposals.DraftMining+Command",
        "Sdlc.Core.Proposals.DraftMining+Operation",
        "Sdlc.Core.Proposals.DraftMining+Proposal",
        "Sdlc.Core.Proposals.DraftMining+Record",
        "Sdlc.Core.Proposals.ProposeMining",
        "Sdlc.Core.Proposals.ProposeMining+Group",
        "Sdlc.Core.Proposals.ProposeMining+Query",
        "Sdlc.Core.Proposals.ProposeMining+Response",
        // How each cycle went, which the report renders (D-112). Public because Response carries them.
        "Sdlc.Core.Proposals.ProposeMining+CycleRecord",
        "Sdlc.Core.Proposals.ProposeMining+Reopen",
        "Sdlc.Core.Proposals.ProposeMining+Verdict",
        "Sdlc.Core.Proposals.ProposeMining+DeviationRecord",
        "Sdlc.Core.Evidence.CheckEvidence",
        "Sdlc.Core.Evidence.CheckEvidence+Query",
        "Sdlc.Core.Evidence.CheckEvidence+Response",
        "Sdlc.Core.Evidence.Coverage",
        "Sdlc.Core.Evidence.EvidenceDiff",
        "Sdlc.Core.Evidence.CoverageSummary",
        "Sdlc.Core.Findings.RecordRound",
        "Sdlc.Core.Findings.RecordRound+Command",
        "Sdlc.Core.Findings.RecordRound+Response",
        "Sdlc.Core.Findings.ReaderPrompts",
        "Sdlc.Core.Findings.ReaderPrompts+Command",
        "Sdlc.Core.Findings.ReaderPrompts+Response",
        "Sdlc.Core.Findings.ReaderPrompts+Written",
        "Sdlc.Core.Findings.ReaderAnswers",
        "Sdlc.Core.Findings.ReaderAnswer",
        "Sdlc.Core.Findings.ReaderUsage",
        "Sdlc.Core.Findings.ReaderRow",
        "Sdlc.Core.Findings.CollectRound",
        "Sdlc.Core.Findings.CollectRound+Command",
        "Sdlc.Core.Findings.CollectRound+Response",
        "Sdlc.Core.Findings.WriteVerdict",
        "Sdlc.Core.Findings.WriteVerdict+Command",
        "Sdlc.Core.Findings.WriteVerdict+Response",
        "Sdlc.Core.Findings.UsageLedger",
        "Sdlc.Core.Findings.UsageRound",
        "Sdlc.Core.Findings.UsageSummary",
        "Sdlc.Core.Findings.ChangedSections",
        "Sdlc.Core.Findings.ChangedSections+Query",
        "Sdlc.Core.Findings.ChangedSections+Response",

        // 'sdlc cycles': the listing, the per-cycle shape, and its renderer.
        "Sdlc.Core.Cycles.CycleSummary",
        "Sdlc.Core.Cycles.CyclesRenderer",
        "Sdlc.Core.Cycles.ListCycles",
        "Sdlc.Core.Cycles.ListCycles+Query",
        "Sdlc.Core.Cycles.ListCycles+Response",

        "Sdlc.Core.Artifacts.ArtifactOutline",
        "Sdlc.Core.Artifacts.ArtifactOutlineReader",
        "Sdlc.Core.Artifacts.ArtifactSchema",
        "Sdlc.Core.Artifacts.ArtifactSections",
        "Sdlc.Core.Artifacts.ArtifactSize",
        "Sdlc.Core.Artifacts.ArtifactValidation",
        "Sdlc.Core.Artifacts.ContentHash",
        "Sdlc.Core.Artifacts.Frontmatter",
        "Sdlc.Core.Artifacts.FrontmatterDocument",
        "Sdlc.Core.Artifacts.SchemaViolation",
        "Sdlc.Core.Artifacts.TableRow",
        "Sdlc.Core.Chain.ChainRenderer",
        "Sdlc.Core.Chain.VerifyChain",
        // D-136: which branches may promote into the default branch, judged by 'sdlc promotion'.
        "Sdlc.Core.Chain.Promotion",
        "Sdlc.Core.Chain.PromotionVerdict",
        "Sdlc.Core.Chain.VerifyChain+ChainCheck",
        "Sdlc.Core.Chain.VerifyChain+Query",
        "Sdlc.Core.Chain.VerifyChain+Response",
        "Sdlc.Core.Chain.VerifySlicing",
        "Sdlc.Core.Chain.VerifySlicing+Query",
        "Sdlc.Core.Chain.VerifySlicing+Response",
        "Sdlc.Core.Chain.VerifyUpgrade",
        "Sdlc.Core.Chain.VerifyUpgrade+Query",
        "Sdlc.Core.Chain.VerifyUpgrade+Response",
        "Sdlc.Core.Chain.VerifyMining",
        "Sdlc.Core.Chain.VerifyMining+Query",
        "Sdlc.Core.Constraints.Constraint",
        "Sdlc.Core.Constraints.JudgedPer",
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

        // `sdlc doctor`. A response a renderer reads, so the record and its outcome enum are public
        // for the same reason every other operation's are.
        "Sdlc.Core.Diagnostics.Check",
        "Sdlc.Core.Diagnostics.CliBranchPolicies",
        "Sdlc.Core.Diagnostics.CheckOutcome",
        "Sdlc.Core.Diagnostics.Diagnose",
        "Sdlc.Core.Diagnostics.Diagnose+Query",
        "Sdlc.Core.Diagnostics.Diagnose+Response",
        "Sdlc.Core.Diagnostics.DiagnosisRenderer",
        "Sdlc.Core.Diagnostics.IBranchPolicies",
        // D-136: the default branch and how each branch merges, read back by the doctor.
        "Sdlc.Core.Diagnostics.MergeAnswer",
        "Sdlc.Core.Diagnostics.PolicyAnswer",
        "Sdlc.Core.Diagnostics.PolicyFinding",
        "Sdlc.Core.Cycles.AbandonRecord",
        "Sdlc.Core.Cycles.AbandonText",
        "Sdlc.Core.Cycles.AbandonText+Query",
        "Sdlc.Core.Cycles.ArtifactKind",
        "Sdlc.Core.Cycles.Cycle",
        "Sdlc.Core.Cycles.CycleId",

        // A cycle branch read back with its kind, D-85: plan, build, build-unattended, forward. Public
        // because the factory's skip rule and the sitrep read the remote's branches through it.
        "Sdlc.Core.Cycles.CycleBranch",

        // Parking and the backlog: a plan needing another's unmerged code waits with its spec and plan kept (D-145).
        "Sdlc.Core.Successors.BacklogRenderer",
        "Sdlc.Core.Successors.ParkCycle",
        "Sdlc.Core.Successors.ParkCycle+Command",
        "Sdlc.Core.Successors.ParkCycle+Response",
        "Sdlc.Core.Successors.Parked",
        "Sdlc.Core.Successors.ReadSuccessors+ParkedStanding",
        "Sdlc.Core.Successors.UnparkCycle",
        "Sdlc.Core.Successors.UnparkCycle+Command",
        "Sdlc.Core.Successors.UnparkCycle+Response",

        // Throwing a sandbox away: what can go, and what is worth recording first (D-146).
        "Sdlc.Core.Cycles.DiscardRenderer",
        "Sdlc.Core.Cycles.DiscardSandbox",
        "Sdlc.Core.Cycles.DiscardSandbox+Entry",
        "Sdlc.Core.Cycles.DiscardSandbox+Query",
        "Sdlc.Core.Cycles.DiscardSandbox+Response",
        "Sdlc.Core.Cycles.DiscardVerdict",

        // The sandbox: several cycles planned on one branch, and its approved ones composed for admission (D-144).
        "Sdlc.Core.Cycles.ComposeAdmission",
        "Sdlc.Core.Cycles.ComposeAdmission+Query",
        "Sdlc.Core.Cycles.ComposeAdmission+Response",
        "Sdlc.Core.Cycles.SandboxBranch",

        // 'sdlc cycles --here': which cycle a skill given no id means, one used and several asked about (D-142).
        "Sdlc.Core.Cycles.NextStep",
        "Sdlc.Core.Cycles.ResolveCycle",
        "Sdlc.Core.Cycles.ResolveCycle+Candidate",
        "Sdlc.Core.Cycles.ResolveCycle+Query",
        "Sdlc.Core.Cycles.ResolveCycle+Response",
        "Sdlc.Core.Cycles.ResolveRenderer",

        // 'sdlc cycles --queued': what the factory would build and what it would pass over (D-85).
        "Sdlc.Core.Factory.QueuedRenderer",
        "Sdlc.Core.Factory.ListQueuedCycles",
        "Sdlc.Core.Factory.ListQueuedCycles+HeldBack",
        "Sdlc.Core.Factory.ListQueuedCycles+Query",
        "Sdlc.Core.Factory.ListQueuedCycles+Queued",
        "Sdlc.Core.Factory.ListQueuedCycles+Response",

        // 'sdlc sitrep': the morning's page (D-84, D-86).
        "Sdlc.Core.Factory.Sitrep",
        "Sdlc.Core.Factory.Sitrep+Built",
        "Sdlc.Core.Factory.Sitrep+Overlap",
        "Sdlc.Core.Factory.Sitrep+Query",
        "Sdlc.Core.Factory.Sitrep+Response",
        "Sdlc.Core.Factory.Sitrep+Waiting",
        "Sdlc.Core.Factory.SitrepRenderer",

        // 'sdlc factory': the factory's pipeline, for the recorded platform (D-84).
        "Sdlc.Core.Factory.FactoryPipeline",
        "Sdlc.Core.Factory.WriteFactoryPipeline",
        "Sdlc.Core.Factory.WriteFactoryPipeline+Command",
        "Sdlc.Core.Factory.WriteFactoryPipeline+Response",

        // 'sdlc lead show': a lead fetched and made one shape for 'intent --from' (D-89). The tracker is an
        // interface for the reason IRepository is one: the suite must not need a network or a CLI.
        "Sdlc.Core.Leads.CliTracker",
        "Sdlc.Core.Leads.ITracker",
        "Sdlc.Core.Leads.LeadReference",
        "Sdlc.Core.Leads.LeadSource",
        "Sdlc.Core.Leads.ReadLead",
        "Sdlc.Core.Leads.ReadLead+Query",
        "Sdlc.Core.Leads.ReadLead+Response",
        "Sdlc.Core.Leads.TrackerItem",

        // Mining's ledger, the record of a sitting, and the husks it names (D-152).
        "Sdlc.Core.Mining.MinedArea",
        "Sdlc.Core.Mining.MinedCycle",
        "Sdlc.Core.Mining.MinedDecline",
        "Sdlc.Core.Mining.MinedLedger",
        "Sdlc.Core.Mining.MiningRenderer",
        "Sdlc.Core.Mining.MiningRun",
        "Sdlc.Core.Mining.RecordMining",
        "Sdlc.Core.Mining.RecordMining+Command",
        "Sdlc.Core.Mining.RecordMining+Response",
        "Sdlc.Core.Mining.Watched",

        // 'sdlc operate': production's errors filed as leads (D-89). The store and the filer are
        // interfaces so the suite needs neither a network nor a CLI.
        "Sdlc.Core.Operate.AppInsightsTelemetry",
        "Sdlc.Core.Operate.BugToFile",
        "Sdlc.Core.Operate.CliWorkFiler",
        "Sdlc.Core.Operate.FiledSignature",
        "Sdlc.Core.Operate.ITelemetry",
        "Sdlc.Core.Operate.IWorkFiler",
        "Sdlc.Core.Operate.RunOperate",
        "Sdlc.Core.Operate.RunOperate+Command",
        "Sdlc.Core.Operate.RunOperate+Lead",
        "Sdlc.Core.Operate.RunOperate+Response",
        "Sdlc.Core.Operate.Scrub",
        "Sdlc.Core.Operate.TelemetryError",

        // 'sdlc perspective show' and 'sdlc perspectives': the method documents, and which a cycle gets (D-90).
        "Sdlc.Core.Perspectives.ActivePerspectives",
        "Sdlc.Core.Perspectives.ActivePerspectives+Active",
        "Sdlc.Core.Perspectives.ActivePerspectives+Query",
        "Sdlc.Core.Perspectives.ActivePerspectives+Response",
        "Sdlc.Core.Perspectives.Perspective",
        "Sdlc.Core.Perspectives.PerspectiveLibrary",
        "Sdlc.Core.Cycles.CycleBranchKind",

        // 'sdlc branch <id> <kind>': which branch a kind of work starts on and from what.
        "Sdlc.Core.Cycles.StartBranch",
        "Sdlc.Core.Cycles.StartBranch+Query",
        "Sdlc.Core.Cycles.StartBranch+Response",
        "Sdlc.Core.Cycles.CycleIndex",
        "Sdlc.Core.Cycles.SlicingBranch",
        "Sdlc.Core.Cycles.ComposeSlicing",
        "Sdlc.Core.Cycles.ComposeSlicing+Query",
        "Sdlc.Core.Cycles.ComposeSlicing+Response",
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
        "Sdlc.Core.Evidence.ComputeClaimGraph+Dependency",
        "Sdlc.Core.Evidence.ComputeClaimGraph+SlicePlan",
        "Sdlc.Core.Evidence.ComputeClaimGraph+SlicingComparison",
        "Sdlc.Core.Evidence.ComputeClaimGraph+Query",
        "Sdlc.Core.Evidence.ComputeClaimGraph+Response",

        // Added with F-13: the claim graph now reports the cycles it left out and why, because a
        // silently shorter schedule is its own confusion. Part of a Response a renderer reads, so it
        // is public for the same reason Claim and Collision are.
        "Sdlc.Core.Evidence.ComputeClaimGraph+Skipped",
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
        "Sdlc.Core.Findings.Citation",
        "Sdlc.Core.Findings.Conflict",
        "Sdlc.Core.Findings.Disposition",
        "Sdlc.Core.Findings.Finding",
        "Sdlc.Core.Findings.FindingsReader",
        "Sdlc.Core.Findings.NewerRules",
        "Sdlc.Core.Findings.Turnover",

        // One artifact's findings as the file holds them (D-107): each finding once with its moves, each
        // round an entry chained by hash. Public because FindingsReader reads and writes one, and because
        // the merge and the chain are what the suite has to hold -- that a restatement adds nothing, a
        // reworded id is refused, and an edited entry fails to verify.
        "Sdlc.Core.Findings.FindingsLedger",
        "Sdlc.Core.Findings.LedgerFinding",
        "Sdlc.Core.Findings.LedgerOnDisk",
        "Sdlc.Core.Findings.LedgerRound",
        "Sdlc.Core.Findings.Transition",
        // A cycle's suggestions, each concern once with a flat history of events per document (D-115).
        // Public for the same reasons as the ledger: FindingsReader reads and writes one, and the suite
        // holds that events alternate per document, a restatement records nothing, and a new id is free.
        "Sdlc.Core.Findings.SuggestionLedger",
        "Sdlc.Core.Findings.LedgerSuggestion",
        "Sdlc.Core.Findings.SuggestionEvent",
        "Sdlc.Core.Findings.SuggestionEventKind",
        // A suggestion's status, worked out from intents, leads and abandonments on every read and never
        // stored (D-115). Public because the listing carries it and the suite holds how each is read.
        "Sdlc.Core.Findings.SuggestionDecision",
        "Sdlc.Core.Findings.SuggestionDecisions",
        "Sdlc.Core.Findings.SuggestionStatus",

        // What a build saw, left alone and wrote into its commit. Public because it is part of the
        // suggestions Response a renderer reads, and because the parser that reads one out of a commit
        // message is the thing under test.
        "Sdlc.Core.Findings.Noticed",
        "Sdlc.Core.Findings.Oscillation",
        "Sdlc.Core.Findings.OscillationState",
        "Sdlc.Core.Findings.StuckRule",
        // What came back under a stuck rule, so the panel can say one objection from several. Public
        // because StuckRule carries it.
        "Sdlc.Core.Findings.Returned",
        "Sdlc.Core.Findings.FindingsRound",
        "Sdlc.Core.Findings.RemediationBudget",
        "Sdlc.Core.Findings.RemediationState",
        "Sdlc.Core.Findings.Severity",
        "Sdlc.Core.Findings.Suggestion",
        "Sdlc.Core.Findings.Suggestions",
        "Sdlc.Core.Findings.Suggestions+Query",
        "Sdlc.Core.Findings.Suggestions+Response",
        "Sdlc.Core.Findings.SuggestionsRenderer",

        // 'sdlc propose constraints': the operation, the three shapes it hands back, and its renderer.
        // Public for the reason the suggestions listing is -- Sdlc.Cli dispatches the request and renders
        // the response -- and the three records are part of that response rather than types of their own
        // standing. The renderer is here and not in Constraints because this reads the findings as well
        // as the rules, and a namespace that depended on both would put a cycle between the two.
        "Sdlc.Core.Proposals.PerspectiveTally",
        "Sdlc.Core.Proposals.ProposalsRenderer",
        "Sdlc.Core.Proposals.ProposeConstraints",
        "Sdlc.Core.Proposals.ProposeConstraints+Query",
        "Sdlc.Core.Proposals.ProposeConstraints+Response",
        "Sdlc.Core.Proposals.Recurrence",
        "Sdlc.Core.Proposals.Stopped",
        "Sdlc.Core.Proposals.Uncited",

        // Writes the copy of a report a person works from. Public because Sdlc.Cli calls it after
        // printing, and deliberately not an operation through the mediator: nothing reads a report back,
        // so there is no state here for a handler to own.
        "Sdlc.Core.Reports.ReportFile",

        // 'sdlc propose decisions': the operation, its response, and the two shapes that response
        // carries. DecisionSource is public because Candidate exposes it.
        "Sdlc.Core.Proposals.Candidate",
        "Sdlc.Core.Proposals.DecisionSource",
        "Sdlc.Core.Proposals.ProposeDecisions",
        "Sdlc.Core.Proposals.ProposeDecisions+Query",
        "Sdlc.Core.Proposals.ProposeDecisions+Response",

        // 'sdlc decisions': the record a stage reads so it does not re-decide something, its model, its
        // parser and its renderer. The parser is public for the same reason ConstraintParser is -- the
        // proposer and the reader both parse the one document, and a second reader would be a second
        // answer to what the file says.
        "Sdlc.Core.Decisions.Decision",
        "Sdlc.Core.Decisions.DecisionDetail",
        "Sdlc.Core.Decisions.DecisionId",
        "Sdlc.Core.Decisions.DecisionParser",
        "Sdlc.Core.Decisions.DecisionsRenderer",

        // The departures a cycle declares from that record (D-139): status and the shape check in the
        // CLI read them, so the one parser answers what a departure line says everywhere it is shown.
        "Sdlc.Core.Decisions.Departure",
        "Sdlc.Core.Decisions.Departures",
        "Sdlc.Core.Decisions.ReadDecisions",
        "Sdlc.Core.Decisions.ReadDecisions+Query",
        "Sdlc.Core.Decisions.ReadDecisions+Response",

        // 'sdlc deviations': where the build bent the approved documents, against what a person answered.
        // The operation, the shapes its response exposes, the file's parser and the renderer.
        "Sdlc.Core.Deviations.CycleDeviations",
        "Sdlc.Core.Deviations.CycleDeviations+Query",
        "Sdlc.Core.Deviations.CycleDeviations+Response",
        "Sdlc.Core.Deviations.Deviation",
        "Sdlc.Core.Deviations.DeviationCircle",
        "Sdlc.Core.Deviations.DeviationKind",
        "Sdlc.Core.Deviations.DeviationStanding",
        "Sdlc.Core.Deviations.DeviationState",
        "Sdlc.Core.Deviations.DeviationsFile",
        "Sdlc.Core.Deviations.DeviationsRenderer",
        "Sdlc.Core.Deviations.DeviationsState",

        // 'sdlc theme' and 'sdlc propose theme': the register, its one slot shape, the operation
        // that reads it and the renderer that both shows and proposes one. Theme is public rather
        // than internal because ReadTheme.Response exposes it and Sdlc.Cli renders it.
        "Sdlc.Core.Themes.Theme",
        "Sdlc.Core.Themes.ThemeRenderer",
        "Sdlc.Core.Themes.ReadTheme",
        "Sdlc.Core.Themes.ReadTheme+Query",
        "Sdlc.Core.Themes.ReadTheme+Response",
        "Sdlc.Core.Themes.Slot",
        "Sdlc.Core.Git.GitRepository",
        "Sdlc.Core.Git.IRepository",
        // The four added with D-46. Each host's facts are one row in a table rather than a switch
        // arm in each of four files, and the row is public because `Templates` and `Initialise`
        // read it and the conformance suite enumerates it. ConfigOwnership and HookEntryShape are
        // public for the reason the note above gives: a property of a public record cannot be less
        // accessible than the record.
        "Sdlc.Core.Hooks.ConfigOwnership",
        "Sdlc.Core.Hooks.HookDecision",
        "Sdlc.Core.Hooks.HookEntryShape",
        "Sdlc.Core.Hooks.HookEvent",
        "Sdlc.Core.Hooks.HookHost",
        "Sdlc.Core.Hooks.HookPolicy",
        "Sdlc.Core.Hooks.HookProtocol",
        "Sdlc.Core.Hooks.HookVerdict",
        "Sdlc.Core.Hooks.ProtectedFile",
        "Sdlc.Core.Hooks.PluginRelease",
        "Sdlc.Core.Hooks.ProtectedPaths",
        "Sdlc.Core.Hooks.SupportedHost",
        "Sdlc.Core.Hooks.SupportedHosts",
        "Sdlc.Core.Hooks.ToolCall",
        "Sdlc.Core.Hooks.ToolKind",
        "Sdlc.Core.Hooks.TurnEnd",
        "Sdlc.Core.Hooks.TurnEndGate",
        "Sdlc.Core.Hooks.TurnEndGate+Query",
        // D-136: the branch policy adoption sets as its default, printed by 'sdlc branch-policy'.
        "Sdlc.Core.Init.BranchPolicyScript",
        // D-136: who must approve a pull request, the options 'sdlc branch-policy' takes.
        "Sdlc.Core.Init.BranchReviews",
        "Sdlc.Core.Init.FrameworkStamp",
        "Sdlc.Core.Init.InitRenderer",
        "Sdlc.Core.Init.LineEndings",
        "Sdlc.Core.Init.Initialise",
        "Sdlc.Core.Init.Initialise+Command",
        "Sdlc.Core.Init.Initialise+InitAction",
        "Sdlc.Core.Init.Initialise+InitChange",
        "Sdlc.Core.Init.Initialise+InitMode",
        "Sdlc.Core.Init.Initialise+Response",
        "Sdlc.Core.Init.Platform",
        "Sdlc.Core.Init.Scaffold",
        "Sdlc.Core.Init.ScaffoldSolution",
        "Sdlc.Core.Init.ScaffoldSolution+Command",
        "Sdlc.Core.Init.ScaffoldSolution+Response",
        "Sdlc.Core.Init.Templates",
        "Sdlc.Core.PullRequests.PullRequest",
        "Sdlc.Core.PullRequests.PullRequest+PullRequestRefusal",
        "Sdlc.Core.PullRequests.PullRequest+Query",
        "Sdlc.Core.PullRequests.PullRequest+Response",
        "Sdlc.Core.PullRequests.PullRequestRenderer",
        // Public because two assemblies need the stamped release version and they would otherwise
        // read it twice: 'sdlc version' in the CLI, and the pipeline template here that pins the
        // binary a pull request restores.
        "Sdlc.Core.PinnedRelease",
        "Sdlc.Core.Release",
        "Sdlc.Core.SdlcServices",
        // The five .NET build settings files, as one list: what each is, what it costs, and which of
        // them a declared stack takes. Public because the scaffold harness writes the same files the
        // binary does and must not keep its own copy of the list, and because the suite asserts what
        // a hand-edited table reads as. Setting and Row are public because the list and the reader
        // expose them. D-77.
        "Sdlc.Core.Solutions.BuildSettings",
        "Sdlc.Core.Solutions.BuildSettings+Row",
        "Sdlc.Core.Solutions.BuildSettings+Setting",

        // 'sdlc stack': the operation, the per-file state it returns, and its renderer. Public for
        // the reason every other operation's request and response are -- Sdlc.Cli dispatches one and
        // renders the other.
        "Sdlc.Core.Solutions.ReadStackDeclaration",
        "Sdlc.Core.Solutions.ReadStackDeclaration+Declaration",
        "Sdlc.Core.Solutions.ReadStackDeclaration+Query",
        "Sdlc.Core.Solutions.ReadStackDeclaration+Response",
        "Sdlc.Core.Solutions.ReadStackDeclaration+SettingState",
        "Sdlc.Core.Solutions.StackRenderer",

        // 'sdlc settings': how a solution is set to run, each value with its source. Public for the
        // reason every operation's request and response are; the setting's group and source are what the
        // renderer prints from. D-138.
        "Sdlc.Core.Solutions.ReadSettings",
        "Sdlc.Core.Solutions.ReadSettings+Query",
        "Sdlc.Core.Solutions.ReadSettings+Response",
        "Sdlc.Core.Solutions.ReadSettings+Setting",
        "Sdlc.Core.Solutions.ReadSettings+SettingGroup",
        "Sdlc.Core.Solutions.ReadSettings+SettingSource",
        "Sdlc.Core.Solutions.SettingsRenderer",

        // Whether a person approves each cycle before it is built: a key of the solution document, read
        // by the gate, the chain and the sitrep. D-137.
        "Sdlc.Core.Solutions.ApprovalRequirement",

        // What a person still has to place before the first cycle, and the block naming the one
        // step that is next. Public because adoption's response and doctor's both carry the state,
        // and the two renderers print the same block from it. D-79.
        "Sdlc.Core.Solutions.Setup",
        "Sdlc.Core.Solutions.SetupProgress",
        "Sdlc.Core.Solutions.SetupState",
        "Sdlc.Core.Solutions.SetupStep",
        "Sdlc.Core.Solutions.SolutionDocument",
        "Sdlc.Core.Solutions.SolutionOrigin",
        "Sdlc.Core.Solutions.SolutionRoot",
        "Sdlc.Core.Solutions.SolutionStandard",

        // The drafter that writes the solution document beside itself for a person to rename. Public
        // for the same reason the approval one is: the CLI dispatches it, and its two constants are
        // what a test asserts the draft against.
        // The profiles the framework ships, and the shape of one. Public because Templates composes a
        // stack file from a profile and the suite asserts what each one declares.
        "Sdlc.Core.Solutions.StackCoverage",
        "Sdlc.Core.Solutions.StackCoverage+Rule",
        "Sdlc.Core.Solutions.StackCoverage+Unmentioned",
        "Sdlc.Core.Solutions.StackDefault",
        "Sdlc.Core.Solutions.StackProfile",
        "Sdlc.Core.Solutions.StackProfiles",

        // The menu written beside the draft, and the reader that tells doctor what a declared stack
        // says. Public because the drafter renders one and the suite asserts both -- that the menu
        // offers every concern the profiles declare and recommends none of them, and that a table a
        // person hand-edited still reads.
        "Sdlc.Core.Solutions.StackOptions",

        // The reader for the other table in a declared stack: which stack rules it switches on.
        // Public for the reason StackTable is -- the suite asserts that a rule row is never counted
        // as a declared default, and that a cell which is not plainly yes leaves its rule off.
        "Sdlc.Core.Solutions.StackRules",
        "Sdlc.Core.Solutions.StackRules+Row",
        "Sdlc.Core.Solutions.StackTable",
        "Sdlc.Core.Solutions.StackTable+Row",
        "Sdlc.Core.Solutions.WriteSolutionDraft",
        "Sdlc.Core.Solutions.WriteSolutionDraft+Command",
        "Sdlc.Core.Solutions.WriteTechStackDraft",
        "Sdlc.Core.Solutions.WriteTechStackDraft+Command",

        // The pieces of a slicing that wait on another's code (D-125): the file's shape, and where each
        // stands, which the binary lists, checks and starts from.
        "Sdlc.Core.Successors.ReadSuccessors",
        "Sdlc.Core.Successors.ReadSuccessors+Malformed",
        "Sdlc.Core.Successors.ReadSuccessors+Query",
        "Sdlc.Core.Successors.ReadSuccessors+Response",
        "Sdlc.Core.Successors.ReadSuccessors+Standing",
        "Sdlc.Core.Successors.ReadSuccessors+Wait",
        "Sdlc.Core.Successors.StartSuccessor",
        "Sdlc.Core.Successors.StartSuccessor+Command",
        "Sdlc.Core.Successors.StartSuccessor+Response",
        "Sdlc.Core.Successors.Successor",
        "Sdlc.Core.Successors.SuccessorReference",
        "Sdlc.Core.Successors.SuccessorState",
        "Sdlc.Core.Successors.SuccessorsRenderer",
        "Sdlc.Core.Successors.WaitState",
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
