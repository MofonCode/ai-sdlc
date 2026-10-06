using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using FluentResults;
using Sdlc.Cli;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Chain;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Diagnostics;
using Sdlc.Core.Drafts;
using Sdlc.Core.Evidence;
using Sdlc.Core.Findings;
using Sdlc.Core.Hooks;
using Sdlc.Core.Init;
using Sdlc.Core.Landing;
using Sdlc.Core.Solutions;

// Constraint and artifact text is passed through verbatim, so the stream has to carry what the files
// hold. On Windows the default is the console codepage, which turns an em dash into a question mark -
// and a session judging against mangled text would cite mangled text. Setting this throws on a handle
// that cannot take it, and a failure here is cosmetic rather than a reason not to run.
try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // A redirected or closed handle. The gate still works; the dashes may not look right.
}

var line = CommandLine.Parse(args);

if (line.Verb is "" or "help")
{
    await Console.Out.WriteAsync(Usage.Text).ConfigureAwait(false);
    return line.Verb.Length == 0 ? ExitCode.Usage : ExitCode.Ok;
}

// Answered before any workspace is opened, because the question somebody asks this is "what have I
// got installed" -- and a version that refused outside a governed solution would be unanswerable at
// exactly the moment they are checking whether the install worked.
if (line.Verb is "version")
{
    await Console.Out.WriteAsync(Release.Text).ConfigureAwait(false);
    return ExitCode.Ok;
}

try
{
    return line.Verb switch
    {
        "init" => await Workspace.Run(line, w => Init(w, line)).ConfigureAwait(false),
        "new" => await Workspace.Run(line, NewCycle).ConfigureAwait(false),
        "scaffold" => await WithCycle(line, "scaffold", ScaffoldSolution).ConfigureAwait(false),
        "evidence" => await WithCycle(line, "evidence", (w, id) => Evidence(w, line, id)).ConfigureAwait(false),
        "cycles" => await Workspace.Run(line, w => Cycles(w, line)).ConfigureAwait(false),
        "constraints" => await Workspace.Run(line, w => Constraints(w, line)).ConfigureAwait(false),
        "gate" => await WithCycle(line, "gate", Gate).ConfigureAwait(false),
        "verify-chain" => await VerifyChainCommand(line).ConfigureAwait(false),
        "landable" => await AskLandable(line).ConfigureAwait(false),
        "draft" => await Draft(line).ConfigureAwait(false),
        "status" => await WithCycle(line, "status", (w, id) => Status(w, line, id)).ConfigureAwait(false),
        "findings" => await WithCycle(line, "findings", (w, id) => Findings(w, line, id)).ConfigureAwait(false),
        "suggestions" => await Workspace.Run(line, w => Suggestions(w, line)).ConfigureAwait(false),
        "doctor" => await Workspace.Run(line, Doctor).ConfigureAwait(false),
        "hook" => await Hook(line).ConfigureAwait(false),
        _ when Stages.IsSkill(line.Verb) => await Skill(line).ConfigureAwait(false),
        _ => await Unknown(line.Verb).ConfigureAwait(false),
    };
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException)
{
    // The gate could not read what it needed. That is the gate failing rather than a rule refusing,
    // and the exit code says which, so a hook does not report a broken tool as a governed stop.
    await Console.Error.WriteLineAsync(error.Message).ConfigureAwait(false);
    return ExitCode.Failed;
}

static async Task<int> Constraints(Workspace workspace, CommandLine line)
{
    line.TryOption("for", out string? artifact);

    Result<EffectiveConstraints.Response> result = await workspace
        .Send<EffectiveConstraints.Query, EffectiveConstraints.Response>(new EffectiveConstraints.Query(artifact))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    ConstraintDetail detail = line.HasOption("brief") ? ConstraintDetail.Brief : ConstraintDetail.Full;

    await Console.Out.WriteAsync(ConstraintRenderer.Render(result.Value, detail, artifact))
        .ConfigureAwait(false);

    return ExitCode.Ok;
}

// Human-invoked and never model-invoked: adopting a process is a decision about how a team works, so
// the hook denies an agent running this at all. What it does here is the mechanical half -- the
// interview, and the proposal of this solution's own constraints, belong to the skill wrapped around it,
// because they need language rather than a file listing.
static async Task<int> Init(Workspace workspace, CommandLine line)
{
    string profile = line.TryOption("profile", out string? named) ? named : "dotnet-default";

    if (!TryPlatform(line, out Platform? platform, out string? complaint))
    {
        await Console.Error.WriteLineAsync(complaint).ConfigureAwait(false);
        return ExitCode.Usage;
    }

    // A flag, not a value: there is no --adopt-standard=false to misread, and the absence of the option
    // is the answer every existing solution already gave.
    Result<Initialise.Response> result = await workspace
        .Send<Initialise.Command, Initialise.Response>(
            new Initialise.Command(profile, platform, line.HasOption("adopt-standard")))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    Initialise.Response report = result.Value;
    await Console.Out.WriteAsync(InitRenderer.Render(report)).ConfigureAwait(false);

    if (report.Written.Count == 0)
    {
        return ExitCode.Ok;
    }

    // Committed rather than left in the working tree. An untracked file is untracked when the first
    // cycle takes its diff, so the diff records the whole file as new and a plan saying it changes that
    // file can never match -- the first cycle would fail CON_07.2 for something adoption did.
    // Through the container, not `new GitRepository()`. Constructing the concrete type here meant
    // this one step could not be given a fake, so the only part of adoption that writes to git was
    // the only part no test could exercise -- while Workspace holds no closure precisely so that a
    // dependency can be substituted the way the application composes one.
    Result commit = workspace.Repository.Commit(
        workspace.SolutionRoot,
        report.Written,
        Templates.CommitMessage(report.Mode, report.Written));

    if (commit.IsFailed)
    {
        await Console.Error.WriteLineAsync(
            "The files are written, but git would not commit them: "
            + string.Join("; ", commit.Errors.Select(error => error.Message))
            + " Commit them by hand before the first cycle, or its diff will record them as new.")
            .ConfigureAwait(false);

        return ExitCode.Failed;
    }

    return ExitCode.Ok;
}

// The solution skeleton, and the gate is what permits it. Adoption writes the files that constrain
// how code is written; the code itself arrives behind an approved plan like every line after it -- so
// this asks the same question the build stage asks and refuses with the same reason.
static async Task<int> ScaffoldSolution(Workspace workspace, CycleId id)
{
    Result<Sdlc.Core.Init.ScaffoldSolution.Response> result = await workspace
        .Send<Sdlc.Core.Init.ScaffoldSolution.Command, Sdlc.Core.Init.ScaffoldSolution.Response>(
            new Sdlc.Core.Init.ScaffoldSolution.Command(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(InitRenderer.RenderScaffold(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// Null when the option is absent, which is the whole point: on a repository already governed, "keep what
// is recorded" and "github" are different answers, and returning the default for both is what let a
// re-run write a pipeline while the recorded platform stayed local with nothing printed.
static bool TryPlatform(
    CommandLine line,
    out Platform? platform,
    [NotNullWhen(false)] out string? complaint)
{
    platform = null;
    complaint = null;

    if (!line.TryOption("platform", out string? named))
    {
        return true;
    }

    switch (named.Trim().Replace("-", string.Empty, StringComparison.Ordinal))
    {
        case string github when github.Equals("github", StringComparison.OrdinalIgnoreCase):
            platform = Platform.GitHub;
            return true;
        case string ado when ado.Equals("azuredevops", StringComparison.OrdinalIgnoreCase)
            || ado.Equals("ado", StringComparison.OrdinalIgnoreCase):
            platform = Platform.AzureDevOps;
            return true;

        // 'none' and 'offline' are accepted spellings of the same answer. Somebody adopting into a
        // repository with no remote reaches for whichever word they have in mind, and refusing two of
        // three would send them to read the options for a choice they had already made.
        case string local when local.Equals("local", StringComparison.OrdinalIgnoreCase)
            || local.Equals("none", StringComparison.OrdinalIgnoreCase)
            || local.Equals("offline", StringComparison.OrdinalIgnoreCase):
            platform = Platform.Local;
            return true;
        default:
            complaint = $"'{named}' is not a platform. One of: github, azure-devops, local.";
            return false;
    }
}

// Allocates an id and creates the directory. Creating it is the collision check: at allocation there is
// no directory to be already there, so a look-then-create would leave the race it was written to close.
static async Task<int> NewCycle(Workspace workspace)
{
    Result<Cycle> result = await workspace
        .Send<NewCycle.Command, Cycle>(new NewCycle.Command())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    Cycle cycle = result.Value;

    // The id alone on standard output, so a skill can capture it, with everything a person needs on
    // standard error beside it.
    await Console.Out.WriteLineAsync(cycle.Id.ToString()).ConfigureAwait(false);

    await Console.Error.WriteLineAsync(string.Create(
        CultureInfo.InvariantCulture,
        $"Opened {cycle.Directory}. Its branch is {cycle.Id.BranchName()}; create it with 'git switch -c {cycle.Id.BranchName()}'."))
        .ConfigureAwait(false);

    return ExitCode.Ok;
}

// Whether a cycle may be landed, without landing it. Pushing and opening the pull request is the land
// skill's, because it needs the platform's own tool and a sentence a person wrote; what the binary owns is
// the refusal, which has to be the same whether one cycle is landed or eight.
static async Task<int> AskLandable(CommandLine line)
{
    // --all moved to 'sdlc cycles --landable', where every other "what is in this solution" question
    // lives. Answered rather than refused: it was documented, people copied it, and a flag that stopped
    // working with no explanation is how a working install reads as a broken one.
    if (line.HasOption("all"))
    {
        await Console.Error.WriteLineAsync(
            "'landable --all' is now 'sdlc cycles --landable', beside the rest of what a solution holds. "
            + "'sdlc landable <id>' is unchanged.")
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    return await WithCycle(line, "landable", async (workspace, id) =>
        {
            Result<Land.Response> result = await workspace
                .Send<Land.Query, Land.Response>(new Land.Query(id))
                .ConfigureAwait(false);

            if (result.IsFailed)
            {
                return await Report(result, ExitCode.Failed).ConfigureAwait(false);
            }

            await Console.Out.WriteAsync(LandRenderer.Render(result.Value)).ConfigureAwait(false);
            return result.Value.MayLand ? ExitCode.Ok : ExitCode.Refused;
        }).ConfigureAwait(false);
}

// Every cycle is examined, and every one is listed -- landable or not. A list of only the eligible ones
// hides the surprise that matters most: a cycle from last week still sitting there verified, which the
// sweep picks up because every passed cycle is eligible.
static async Task<int> Sweep(Workspace workspace)
{
    List<Land.Response> candidates = [];

    foreach (Cycle cycle in CycleIndex.All(workspace.SolutionRoot))
    {
        Result<Land.Response> result = await workspace
            .Send<Land.Query, Land.Response>(new Land.Query(cycle.Id))
            .ConfigureAwait(false);

        if (result.IsFailed)
        {
            return await Report(result, ExitCode.Failed).ConfigureAwait(false);
        }

        candidates.Add(result.Value);
    }

    await Console.Out.WriteAsync(LandRenderer.RenderSweep(candidates)).ConfigureAwait(false);

    // Zero whether or not anything is landable. Nothing refused here; a sweep over a tidy solution with
    // nothing ready is a correct answer rather than a failure.
    return ExitCode.Ok;
}

// A stage that needs language is a skill, and the binary says so rather than reporting an unknown
// command. The status panel ends with the literal next command, so this is where somebody arrives after
// copying one -- and a tool that sent them nowhere would make its most-read output look wrong.
static async Task<int> Skill(CommandLine line)
{
    await Console.Error.WriteAsync(Stages.Explain(line.Verb, line.Arguments)).ConfigureAwait(false);
    return ExitCode.Usage;
}

// Which cycles may be built together. Deterministic -- a set intersection over a file table -- so the
// binary owns it. A batch skill working the graph out by reading plans would get it slightly differently
// each run, and what it decides is what runs concurrently.
// Every cycle a solution holds, newest first -- and the two questions that used to be their own verbs.
// One verb, because all three answer "what is in this solution and what can I do with it"; a person who
// knew about 'claims' and not 'landable --all' had no way to discover the second from the first.
//
// The two folded modes keep their own renderers rather than collapsing into the list. A claim graph is a
// grouping with collisions and a sweep is a list with a warning about notifying people; flattening either
// into a chronological table would lose exactly what it is read for.
static async Task<int> Cycles(Workspace workspace, CommandLine line)
{
    if (line.HasOption("claims"))
    {
        Result<ComputeClaimGraph.Response> graph = await workspace
            .Send<ComputeClaimGraph.Query, ComputeClaimGraph.Response>(new ComputeClaimGraph.Query())
            .ConfigureAwait(false);

        if (graph.IsFailed)
        {
            return await Report(graph, ExitCode.Failed).ConfigureAwait(false);
        }

        await Console.Out.WriteAsync(ClaimGraphRenderer.Render(graph.Value)).ConfigureAwait(false);

        // Zero whether or not anything collides. A collision is a schedule, not a problem.
        return ExitCode.Ok;
    }

    if (line.HasOption("landable"))
    {
        return await Sweep(workspace).ConfigureAwait(false);
    }

    ArtifactKind? stage = line.TryOption("stage", out string? named)
        && Cycle.TryParseKind(named, out ArtifactKind asked)
            ? asked
            : null;

    if (named is not null && stage is null)
    {
        await Console.Error.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"There is no artifact called '{named}'. Ask for intent, spec, plan or verify."))
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    Result<ListCycles.Response> result = await workspace
        .Send<ListCycles.Query, ListCycles.Response>(new ListCycles.Query(
            stage,
            line.HasOption("approved") ? true : null,
            line.HasOption("verified") ? true : null))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(CyclesRenderer.Render(result.Value)).ConfigureAwait(false);

    // Zero on an empty list. A solution with nothing matching a filter is a correct answer.
    return ExitCode.Ok;
}

// What a build leaves behind, as files rather than prose. Verify reads only this, because a verdict
// derived from a session's recollection is a verdict nobody can check.
static async Task<int> Evidence(Workspace workspace, CommandLine line, CycleId id)
{
    line.TryOption("tests", out string? trx);
    line.TryOption("build-log", out string? log);

    Result<RecordEvidence.Response> result = await workspace
        .Send<RecordEvidence.Command, RecordEvidence.Response>(new RecordEvidence.Command(id, trx, log))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(EvidenceRenderer.Render(result.Value)).ConfigureAwait(false);

    // Zero even when the comparison disagrees and even when tests failed. Recording what happened
    // succeeded; what to make of it is verify's, and a non-zero exit here would make the build step look
    // like the thing that refused.
    return ExitCode.Ok;
}

static async Task<int> Gate(Workspace workspace, CycleId id)
{
    Result<ApprovalGate.Response> result = await workspace
        .Send<ApprovalGate.Query, ApprovalGate.Response>(new ApprovalGate.Query(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    ApprovalGate.Response verdict = result.Value;

    // A refusal goes to standard error and a pass to standard output, so a hook that forwards only one
    // stream still shows the person why they were stopped.
    if (verdict.MayBuild)
    {
        await Console.Out.WriteLineAsync(verdict.Explanation).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    await Console.Error.WriteLineAsync(verdict.Explanation).ConfigureAwait(false);
    return ExitCode.Refused;
}

// Not WithCycle, and that is the whole of F-28.
//
// Every other cycle command is typed by a person, so an argument that is not a cycle id is a typo and
// ExitCode.Usage is the honest answer. This one is handed `github.head_ref` by the generated pipeline
// on every pull request, so the argument is whatever branch somebody opened -- and
// `dependabot/nuget/Serilog-4.1.0` is not a typo, it is a change with no governed cycle behind it.
//
// Reported as Usage, that read as "the command line could not be understood": a complaint about
// argument syntax on a bot's pull request, which looks exactly like a misconfigured tool. The obvious
// fix is to delete the step, and the step is the only layer nobody can bypass. So the worst outcome of
// the wrong exit code was not the red check; it was the step being removed to stop it.
//
// It is Refused instead -- "a rule refused what was asked, the gate working as intended" -- because
// that is what happened. §3.1 requires every class of change to run the full chain, and a change
// arriving without one has not. Exiting zero was the alternative and would have made the branch name
// the bypass: anything merges if you do not call it cycle/<id>. D-45.
static async Task<int> VerifyChainCommand(CommandLine line)
{
    if (line.Arguments.Count == 0)
    {
        await Console.Error.WriteLineAsync(
            "Which cycle? Give its id or its branch, as in 'sdlc verify-chain 20260929_1456_32'.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    string given = line.Arguments[0];

    if (!CycleId.TryParse(given, out CycleId? id) && !CycleId.TryParseBranch(given, out id))
    {
        // Standard output, not error: this is a verdict a reader of a failed check has to read, and
        // the rest of the report goes there too.
        await Console.Out.WriteAsync(ChainRenderer.RenderUngoverned(given)).ConfigureAwait(false);
        return ExitCode.Refused;
    }

    return await Workspace.Run(line, workspace => VerifyChain(workspace, id.Value)).ConfigureAwait(false);
}

// The non-bypassable layer. A pipeline runs this behind branch policy, so its exit code is the thing
// standing between an unapproved chain and a merge -- which is why the whole report goes to standard
// output whether it holds or not: a reader looking at a failed check needs what passed as well.
static async Task<int> VerifyChain(Workspace workspace, CycleId id)
{
    Result<VerifyChain.Response> result = await workspace
        .Send<VerifyChain.Query, VerifyChain.Response>(new VerifyChain.Query(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(ChainRenderer.Render(result.Value)).ConfigureAwait(false);
    return result.Value.Holds ? ExitCode.Ok : ExitCode.Refused;
}

// One verb drafts every file a person writes by hand. They follow one rule -- a tool never writes
// them, a tool prints what to put in them -- so they are one verb rather than four commands, and the
// hook rule that denies every agent write to all four stays absolute rather than conditional.
static async Task<int> Draft(CommandLine line)
{
    if (line.Arguments.Count == 0)
    {
        await Console.Error.WriteLineAsync("Draft what? " + Usage.Drafters).ConfigureAwait(false);
        return ExitCode.Usage;
    }

    // The sub-verb is consumed here, so the cycle id is the first argument the rest of the tool sees.
    CommandLine rest = line.WithoutFirstArgument();

    return line.Arguments[0].ToLowerInvariant() switch
    {
        "approval" => await WithCycle(rest, "draft approval", (w, id) => DraftApproval(w, line, id))
            .ConfigureAwait(false),
        "abandon" => await WithCycle(rest, "draft abandon", (w, id) => DraftAbandon(w, id))
            .ConfigureAwait(false),
        "constraint" => await Workspace.Run(rest, DraftConstraint).ConfigureAwait(false),
        "profile" => await Workspace.Run(rest, w => DraftProfile(w, rest)).ConfigureAwait(false),
        "solution" => await Workspace.Run(rest, DraftSolution).ConfigureAwait(false),
        "techstack" => await Workspace.Run(rest, DraftTechStack).ConfigureAwait(false),
        _ => await UnknownDraft(line.Arguments[0]).ConfigureAwait(false),
    };
}

// Only what exists is offered. A message that listed a drafter this cannot run would send someone
// looking for a flag to make it work, which is the failure mode the hook rules are written against.
static async Task<int> UnknownDraft(string what)
{
    await Console.Error.WriteLineAsync(
        $"There is nothing called '{what}' to draft. {Usage.Drafters}").ConfigureAwait(false);
    return ExitCode.Usage;
}

static async Task<int> DraftApproval(Workspace workspace, CommandLine line, CycleId id)
{
    string by = line.TryOption("by", out string? named)
        ? named
        : workspace.Repository.UserName(workspace.SolutionRoot) ?? "<your name>";

    if (!TryDecision(line, out GateDecision decision, out string? clash))
    {
        await Console.Error.WriteLineAsync(clash).ConfigureAwait(false);
        return ExitCode.Usage;
    }

    Result<ApprovalText.Response> result = await workspace
        .Send<WriteApprovalDraft.Command, ApprovalText.Response>(
            new WriteApprovalDraft.Command(id, by, decision))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    ApprovalText.Response text = result.Value;

    if (!text.MayApprove)
    {
        await Console.Error.WriteLineAsync(text.Instructions).ConfigureAwait(false);
        return ExitCode.Refused;
    }

    await Console.Out.WriteAsync(ApprovalRenderer.Render(text)).ConfigureAwait(false);
    return ExitCode.Ok;
}

static async Task<int> DraftAbandon(Workspace workspace, CycleId id)
{
    string by = workspace.Repository.UserName(workspace.SolutionRoot) ?? "<your name>";

    return await PrintDraft(await workspace
        .Send<AbandonText.Query, DraftResult>(new AbandonText.Query(id, by))
        .ConfigureAwait(false)).ConfigureAwait(false);
}

// Writes rather than prints, for the reason the approval drafter does: a block to paste is a block
// that does not get pasted, and a real adoption left the file on its shipped placeholder because
// of exactly that.
static async Task<int> DraftSolution(Workspace workspace) =>
    await PrintDraft(await workspace
        .Send<WriteSolutionDraft.Command, DraftResult>(new WriteSolutionDraft.Command())
        .ConfigureAwait(false)).ConfigureAwait(false);

static async Task<int> DraftTechStack(Workspace workspace) =>
    await PrintDraft(await workspace
        .Send<WriteTechStackDraft.Command, DraftResult>(new WriteTechStackDraft.Command())
        .ConfigureAwait(false)).ConfigureAwait(false);

static async Task<int> DraftConstraint(Workspace workspace) =>
    await PrintDraft(await workspace
        .Send<ConstraintText.Query, DraftResult>(new ConstraintText.Query())
        .ConfigureAwait(false)).ConfigureAwait(false);

static async Task<int> DraftProfile(Workspace workspace, CommandLine line)
{
    if (line.Arguments.Count == 0)
    {
        await Console.Error.WriteLineAsync(
            "Which profile? Give its package name, as in 'sdlc draft profile dotnet-default'.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    return await PrintDraft(await workspace
        .Send<ProfileText.Query, DraftResult>(new ProfileText.Query(line.Arguments[0]))
        .ConfigureAwait(false)).ConfigureAwait(false);
}

// A drafter that produced nothing is a refusal rather than a failure, so its explanation goes to
// standard error and the exit code says a rule stopped it rather than the tool breaking.
static async Task<int> PrintDraft(Result<DraftResult> result)
{
    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    DraftResult draft = result.Value;

    if (!draft.MayDraft)
    {
        await Console.Error.WriteLineAsync(draft.Instructions).ConfigureAwait(false);
        return ExitCode.Refused;
    }

    await Console.Out.WriteAsync(DraftRenderer.Render(draft)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// Takes an optional cycle rather than requiring one, because the question people ask is usually "what
// has this solution accumulated" rather than "what did that cycle leave".
// Reports on the install and never repairs it. Every remedy it prints is a command a person runs --
// adoption especially, which the pre-tool hook denies to an agent, so a doctor that offered to fix
// things would be a second path into the files adoption writes.
//
// Exits 1 when something is wrong, which is what `gate`, `landable` and `verify-chain` do for the same
// kind of question: a script asking "is this sound" wants an exit code and not a parse.
static async Task<int> Doctor(Workspace workspace)
{
    Result<Diagnose.Response> result = await workspace
        .Send<Diagnose.Query, Diagnose.Response>(new Diagnose.Query())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(DiagnosisRenderer.Render(result.Value)).ConfigureAwait(false);

    return result.Value.Sound ? ExitCode.Ok : ExitCode.Refused;
}

static async Task<int> Suggestions(Workspace workspace, CommandLine line)
{
    CycleId? id = null;

    if (line.Arguments.Count > 0)
    {
        if (!CycleId.TryParse(line.Arguments[0], out CycleId? parsed))
        {
            await Console.Error.WriteLineAsync(
                $"'{line.Arguments[0]}' is not a cycle id. Give one, or none for every cycle.")
                .ConfigureAwait(false);
            return ExitCode.Usage;
        }

        id = parsed.Value;
    }

    Result<Suggestions.Response> result = await workspace
        .Send<Suggestions.Query, Suggestions.Response>(new Suggestions.Query(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    // Zero whether or not anything was found. Nothing here refuses anything, and a non-zero exit would
    // make a tidy solution look like a failing one in a pipeline.
    await Console.Out.WriteAsync(SuggestionsRenderer.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// What the interrogations said about one cycle, per artifact. Deterministic, so the binary owns it: an
// agent spending a turn to print rounds it could have piped is cost with nothing bought.
static async Task<int> Findings(Workspace workspace, CommandLine line, CycleId id)
{
    ArtifactKind? stage = line.TryOption("stage", out string? named)
        && Cycle.TryParseKind(named, out ArtifactKind asked)
            ? asked
            : null;

    if (named is not null && stage is null)
    {
        await Console.Error.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"There is no artifact called '{named}'. Ask for intent, spec, plan or verify."))
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    int? round = null;

    if (line.TryOption("round", out string? asRound))
    {
        if (!int.TryParse(asRound, CultureInfo.InvariantCulture, out int parsed) || parsed < 1)
        {
            await Console.Error.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"'{asRound}' is not a round number. Rounds count from 1."))
                .ConfigureAwait(false);

            return ExitCode.Usage;
        }

        round = parsed;
    }

    Result<ReadFindings.Response> result = await workspace
        .Send<ReadFindings.Query, ReadFindings.Response>(new ReadFindings.Query(id, stage, round))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out
        .WriteAsync(FindingsRenderer.Render(result.Value, line.HasOption("brief")))
        .ConfigureAwait(false);

    return ExitCode.Ok;
}

static async Task<int> Status(Workspace workspace, CommandLine line, CycleId id)
{
    Result<ReadCycleStatus.Response> result = await workspace
        .Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(new ReadCycleStatus.Query(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    ReadCycleStatus.Response status = result.Value;

    // --stage names the artifact whose panel to show. Without it, the panel is for whichever artifact
    // the cycle is waiting on, which is what a person wants after any stage finishes.
    ArtifactKind kind = line.TryOption("stage", out string? named) && Cycle.TryParseKind(named, out ArtifactKind asked)
        ? asked
        : status.Blocking?.Kind ?? ArtifactKind.Plan;

    ReadCycleStatus.ArtifactState state = status.Artifacts.First(artifact => artifact.Kind == kind);
    ArtifactOutline outline = await ArtifactOutlineReader
        .ReadFile(state.Path, CancellationToken.None)
        .ConfigureAwait(false);

    // The gate carries the schema evaluator with it, so an artifact is fully checked where it is
    // written rather than first in a pipeline. A shape problem found here costs a heading; the same one
    // found in CI costs a round trip.
    ArtifactValidation? shape = state.Exists
        ? ArtifactSchema.Validate(
            kind,
            await File.ReadAllTextAsync(state.Path, CancellationToken.None).ConfigureAwait(false),
            id)
        : null;

    StagePanel panel = new(state, outline, shape);

    await Console.Out
        .WriteAsync(line.HasOption("brief")
            ? StatusRenderer.RenderBrief(status, panel)
            : StatusRenderer.RenderStage(status, panel))
        .ConfigureAwait(false);

    return ExitCode.Ok;
}

// Named flags rather than a --decision value, because the three refusals are different acts and a
// person reaching for one knows which. Passing two is a contradiction rather than a last-one-wins.
static bool TryDecision(
    CommandLine line,
    out GateDecision decision,
    [NotNullWhen(false)] out string? clash)
{
    (string Flag, GateDecision Decision)[] flags =
    [
        ("changes-requested", GateDecision.ChangesRequested),
        ("deny", GateDecision.Denied),
        ("reopen", GateDecision.Reopened),
    ];

    List<string> given = [.. flags.Where(flag => line.HasOption(flag.Flag)).Select(flag => "--" + flag.Flag)];

    if (given.Count > 1)
    {
        decision = default;
        clash = $"{string.Join(" and ", given)} ask for different decisions. Pass one.";
        return false;
    }

    decision = flags.FirstOrDefault(flag => line.HasOption(flag.Flag)).Decision;
    clash = null;
    return true;
}

static async Task<int> WithCycle(CommandLine line, string verb, Func<Workspace, CycleId, Task<int>> body)
{
    if (!TryCycle(line, verb, out CycleId id, out string? complaint))
    {
        await Console.Error.WriteLineAsync(complaint).ConfigureAwait(false);
        return ExitCode.Usage;
    }

    return await Workspace.Run(line, workspace => body(workspace, id)).ConfigureAwait(false);
}

static async Task<int> Hook(CommandLine line)
{
    if (!line.TryOption("host", out string? named) || !HookProtocol.TryParseHost(named, out HookHost? host))
    {
        // The hosts come from the table, so this message cannot name one the adapter does not speak
        // to, and cannot omit one it does.
        await Console.Error.WriteLineAsync(
            "Which host? One of: " + string.Join(", ", SupportedHosts.Spellings()) + ".")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    line.TryOption("event", out string? namedEvent);

    if (!HookProtocol.TryParseEvent(namedEvent, out HookEvent? hookEvent))
    {
        await Console.Error.WriteLineAsync(
            $"'{namedEvent}' is not a hook event. Pass --event pre-tool-use or --event turn-end, or leave it off for pre-tool-use.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    if (hookEvent == HookEvent.TurnEnd)
    {
        // Said out loud rather than allowed silently. A host with no turn-end event has nothing for
        // this to listen to, and a configuration that appeared to install one would be the failure
        // this design is most concerned with: a check that looks like enforcement and is not.
        //
        // Read from the host's row rather than naming a host here. It named Copilot, on the belief
        // that its hook surface had no such event -- which was wrong, and the refusal was the last
        // thing standing between the framework and a governed turn end on that host. D-46.
        SupportedHost described = SupportedHosts.Of(host.Value);

        if (!described.GovernsTurnEnd)
        {
            string[] governed =
            [
                .. SupportedHosts.All
                    .Where(candidate => candidate.GovernsTurnEnd)
                    .Select(candidate => "--host " + candidate.Spelling),
            ];

            // The alternative is offered only when there is one. "Use , or leave it ungoverned" is
            // the shape a message takes when it is assembled from a list nobody checked was filled.
            string instead = governed.Length > 0
                ? $" Use {string.Join(" or ", governed)}, or leave"
                : " Leave";

            // States that nothing is wired, not why. The reasons differ -- a host may have no such
            // event at all, or have one the framework declines to govern because its payload carries
            // no way to tell a first stop from a turn already being sent back. Asserting the first
            // where the second is true would be the inaccuracy this project refuses; the row's own
            // remarks carry the reason.
            await Console.Error.WriteLineAsync(
                $"No turn-end event is wired for {described.Name}, so there is nothing for this to listen to.{instead} the turn end ungoverned on that host rather than configuring a check that cannot run.")
                .ConfigureAwait(false);
            return ExitCode.Usage;
        }

        return await TurnEnd(line, host.Value).ConfigureAwait(false);
    }

    string payload = await Console.In.ReadToEndAsync().ConfigureAwait(false);
    Result<ToolCall> call = HookProtocol.ReadCall(payload, host.Value);

    // A payload this cannot read allows the call and says so on standard error. These surfaces are
    // young, and a hook that failed closed on a renamed field would stop a person working for a reason
    // they cannot act on; one that failed silently would permit everything while appearing to protect.
    // Visible and permissive is the only honest third option.
    if (call.IsFailed)
    {
        foreach (IError error in call.Errors)
        {
            await Console.Error.WriteLineAsync("sdlc hook: " + error.Message).ConfigureAwait(false);
        }

        await Console.Out.WriteAsync(
            HookProtocol.WriteDecision(host.Value, HookDecision.Allow, null)).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    HookVerdict verdict = HookPolicy.Decide(call.Value);

    await Console.Out.WriteAsync(
        HookProtocol.WriteDecision(host.Value, verdict.Decision, verdict.Reason)).ConfigureAwait(false);

    // Zero either way: the decision is in the payload, and a non-zero exit would read to the host as
    // the hook itself having failed.
    return ExitCode.Ok;
}

// The turn end. Separate from the tool-call path because it answers a different question in a
// different shape, and because it needs the solution -- git and the recorded evidence -- where the
// tool-call policy needs only the call.
//
// Every failure here allows the turn and says why on standard error, which is the same choice the
// tool-call path makes for an unreadable payload and is made for the same reason: a hook that fails
// closed stops a person working for something they cannot act on, and one that fails silently
// permits everything while appearing to protect. Visible and permissive is the honest third option.
static async Task<int> TurnEnd(CommandLine line, HookHost host)
{
    string payload = await Console.In.ReadToEndAsync().ConfigureAwait(false);
    Result<TurnEnd> turn = HookProtocol.ReadTurnEnd(payload);

    if (turn.IsFailed)
    {
        foreach (IError error in turn.Errors)
        {
            await Console.Error.WriteLineAsync("sdlc hook: " + error.Message).ConfigureAwait(false);
        }

        await Console.Out.WriteAsync(
            HookProtocol.WriteTurnEndDecision(host, HookDecision.Allow, null)).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    bool answered = false;

    // The exit code is discarded on purpose, for the reason the tool-call path gives: the decision
    // is in the payload, and a non-zero exit reads to the host as the hook itself having failed.
    _ = await Workspace.Run(line, async workspace =>
    {
        Result<HookVerdict> verdict = await workspace
            .Send<TurnEndGate.Query, HookVerdict>(new TurnEndGate.Query(turn.Value))
            .ConfigureAwait(false);

        if (verdict.IsFailed)
        {
            foreach (IError error in verdict.Errors)
            {
                await Console.Error.WriteLineAsync("sdlc hook: " + error.Message).ConfigureAwait(false);
            }

            return ExitCode.Ok;
        }

        await Console.Out.WriteAsync(
            HookProtocol.WriteTurnEndDecision(host, verdict.Value.Decision, verdict.Value.Reason))
            .ConfigureAwait(false);

        answered = true;
        return ExitCode.Ok;
    }).ConfigureAwait(false);

    // The workspace refuses to open when the constraint library is not beside the binary, and says
    // so on standard error before the body runs. The turn-end gate does not read that library -- it
    // reads git and the recorded evidence -- so a missing library is a reason to say nothing rather
    // than a reason to stop a turn.
    if (!answered)
    {
        await Console.Out.WriteAsync(
            HookProtocol.WriteTurnEndDecision(host, HookDecision.Allow, null)).ConfigureAwait(false);
    }

    return ExitCode.Ok;
}

static bool TryCycle(
    CommandLine line,
    string verb,
    out CycleId id,
    [NotNullWhen(false)] out string? complaint)
{
    id = default;

    if (line.Arguments.Count == 0)
    {
        complaint = string.Create(
            CultureInfo.InvariantCulture,
            $"Which cycle? Give its id, as in 'sdlc {verb} 20260929_1456_32'.");
        return false;
    }

    // A branch name is accepted as well as a bare id, because the generated pipelines hand one
    // straight in -- GitHub's head_ref is 'cycle/<id>' and Azure DevOps reports
    // 'refs/heads/cycle/<id>'. Both were refused as malformed ids, which meant the pull-request
    // check, the one non-bypassable layer and the whole promise made to a team, could not run as
    // shipped. Found by typing the command the pipeline runs.
    if (!CycleId.TryParse(line.Arguments[0], out CycleId? parsed)
        && !CycleId.TryParseBranch(line.Arguments[0], out parsed))
    {
        complaint = string.Create(
            CultureInfo.InvariantCulture,
            $"'{line.Arguments[0]}' is not a cycle id. They are written as date, 24-hour time and seconds, as in 20260929_1456_32 -- or as the branch that carries one, 'cycle/20260929_1456_32'.");
        return false;
    }

    id = parsed.Value;
    complaint = null;
    return true;
}

static async Task<int> Report<T>(Result<T> result, int code)
{
    foreach (IError error in result.Errors)
    {
        await Console.Error.WriteLineAsync(error.Message).ConfigureAwait(false);
    }

    return code;
}

static async Task<int> Unknown(string verb)
{
    await Console.Error.WriteLineAsync($"Unknown command '{verb}'.").ConfigureAwait(false);
    await Console.Error.WriteAsync(Usage.Text).ConfigureAwait(false);
    return ExitCode.Usage;
}
