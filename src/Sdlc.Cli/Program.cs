using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using FluentResults;
using Sdlc.Cli;
using Sdlc.Core.Approvals;
using Sdlc.Core.Areas;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Chain;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Diagnostics;
using Sdlc.Core.Drafts;
using Sdlc.Core.Evidence;
using Sdlc.Core.Factory;
using Sdlc.Core.Findings;
using Sdlc.Core.Hooks;
using Sdlc.Core.Init;
using Sdlc.Core.Harvest;
using Sdlc.Core.Decisions;
using Sdlc.Core.Deviations;
using Sdlc.Core.PullRequests;
using Sdlc.Core.Themes;
using Sdlc.Core.Proposals;
using Sdlc.Core.Reports;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

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

// Before anything reads the command, because the release that should read it may be another one: a solution
// pins the release its cycles run on, and this binary is only the one on the path (D-131).
if (await Handoff.Run(args).ConfigureAwait(false) is int handedOn)
{
    return handedOn;
}

// Before the parser too: everything after the verb is Claude Code's, and its options are not this binary's.
if (args is [ClaudeSession.Verb, .. var forClaude])
{
    return await ClaudeSession.Run(forClaude).ConfigureAwait(false);
}

var line = CommandLine.Parse(args);

if (line.Verb is "" or "help")
{
    await Console.Out.WriteAsync(Usage.Text).ConfigureAwait(false);
    return line.Verb.Length == 0 ? ExitCode.Usage : ExitCode.Ok;
}

// On any verb, and before the verb reads its arguments. 'sdlc evidence --help' answered "Which cycle?",
// which is a person asking how a command works being told they used it wrong.
if (line.HasOption("help"))
{
    await Console.Out.WriteAsync(Usage.Text).ConfigureAwait(false);
    return ExitCode.Ok;
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
        "evidence" => await EvidenceVerb(line).ConfigureAwait(false),
        "cycles" => await Workspace.Run(line, w => Cycles(w, line)).ConfigureAwait(false),
        "constraints" => await Workspace.Run(line, w => Constraints(w, line)).ConfigureAwait(false),
        "gate" => await WithCycle(line, "gate", Gate).ConfigureAwait(false),
        "verify-chain" => await VerifyChainCommand(line).ConfigureAwait(false),
        "admission" => await AdmissionCommand(line).ConfigureAwait(false),
        "tested" => await TestedCommand(line).ConfigureAwait(false),
        "promotion" => await PromotionCommand(line).ConfigureAwait(false),
        "branch-policy" => await Workspace.Run(line, w => BranchPolicyCommand(w, line)).ConfigureAwait(false),
        "branch" => await BranchCommand(line).ConfigureAwait(false),
        "sitrep" => await Workspace.Run(line, SitrepCommand).ConfigureAwait(false),
        "factory" => await Workspace.Run(line, FactoryCommand).ConfigureAwait(false),
        "lead" => await LeadCommand(line).ConfigureAwait(false),
        "successors" => await Workspace.Run(line, SuccessorsCommand).ConfigureAwait(false),
        "successor" => await SuccessorCommand(line).ConfigureAwait(false),
        "slices" => await SlicesCommand(line).ConfigureAwait(false),
        "admit" => await Workspace.Run(line, AdmitCommand).ConfigureAwait(false),
        "park" => await WithCycle(line, "park", ParkCommand).ConfigureAwait(false),
        "unpark" => await WithCycle(line, "unpark", UnparkCommand).ConfigureAwait(false),
        "backlog" => await Workspace.Run(line, BacklogCommand).ConfigureAwait(false),
        "discard" => await Workspace.Run(line, DiscardCommand).ConfigureAwait(false),
        "tidy" => await Workspace.Run(line, TidyCommand).ConfigureAwait(false),
        "upgrade" => await Workspace.Run(line, w => UpgradeCommand(w, line)).ConfigureAwait(false),
        "operate" => await Workspace.Run(line, w => OperateCommand(w, line)).ConfigureAwait(false),
        "guidance" => await Workspace.Run(line, w => GuidanceCommand(w, line)).ConfigureAwait(false),
        "perspective" => await PerspectiveCommand(line).ConfigureAwait(false),
        "perspectives" => await WithCycle(line, "perspectives", PerspectivesCommand).ConfigureAwait(false),
        "pr-ready" => await AskPrReady(line).ConfigureAwait(false),
        "draft" => await Draft(line).ConfigureAwait(false),
        "propose" => await Propose(line).ConfigureAwait(false),
        "harvest" => await HarvestCommand(line).ConfigureAwait(false),
        "mine" => await Renamed("mine", "harvest", "A harvest reads the completed cycles one aspect at a time; 'sdlc harvest' shows what each still needs.").ConfigureAwait(false),
        "status" => await WithCycle(line, "status", (w, id) => Status(w, line, id)).ConfigureAwait(false),
        "findings" => await WithCycle(line, "findings", (w, id) => Findings(w, line, id)).ConfigureAwait(false),
        "deviations" => await WithCycle(line, "deviations", DeviationsCommand).ConfigureAwait(false),
        "report" => await WithCycle(line, "report", ReportCommand).ConfigureAwait(false),
        "build" => await BuildVerb(line).ConfigureAwait(false),
        "round" => await RoundVerb(line).ConfigureAwait(false),
        "suggestions" => await Workspace.Run(line, w => Suggestions(w, line)).ConfigureAwait(false),
        "decisions" => await Workspace.Run(line, w => Decisions(w, line)).ConfigureAwait(false),
        "areas" => await Workspace.Run(line, AreasCommand).ConfigureAwait(false),
        "theme" => await Workspace.Run(line, ThemeCommand).ConfigureAwait(false),
        "stack" => await Workspace.Run(line, StackCommand).ConfigureAwait(false),
        "settings" => await Workspace.Run(line, SettingsCommand).ConfigureAwait(false),
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
    // The empty entry at the next free id, for a rule a person has in mind: no history needed, and no wording
    // passed in, because a tool may make room for a rule and may not shape one (D-191).
    if (line.HasOption("new"))
    {
        return await PrintDraft(await workspace
            .Send<ConstraintText.Query, DraftResult>(new ConstraintText.Query())
            .ConfigureAwait(false)).ConfigureAwait(false);
    }

    line.TryOption("for", out string? artifact);

    Result<EffectiveConstraints.Response> result = await workspace
        .Send<EffectiveConstraints.Query, EffectiveConstraints.Response>(new EffectiveConstraints.Query(artifact))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    // One rule in full, in force or superseded: what a stage handed only the headline asks for (D-151).
    if (line.Arguments.Count > 0 && string.Equals(line.Arguments[0], "show", StringComparison.OrdinalIgnoreCase))
    {
        Constraint? found = line.Arguments.Count < 2
            ? null
            : result.Value.Constraints.Concat(result.Value.Superseded)
                .FirstOrDefault(rule => string.Equals(rule.Id.ToString(), line.Arguments[1], StringComparison.OrdinalIgnoreCase));

        if (found is null)
        {
            await Console.Error.WriteLineAsync("Which rule? 'sdlc constraints show LOC_nn', one 'sdlc constraints --brief' lists.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        await Console.Out.WriteAsync(ConstraintRenderer.RenderOne(found)).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    Result<AreaScope> scope = await ScopeFor(workspace, line, artifact, rules: true).ConfigureAwait(false);

    if (scope.IsFailed)
    {
        return await Report(scope, ExitCode.Refused).ConfigureAwait(false);
    }

    ConstraintDetail detail = line.HasOption("brief") ? ConstraintDetail.Brief : ConstraintDetail.Full;

    await Console.Out.WriteAsync(ConstraintRenderer.Render(result.Value, detail, artifact, scope.Value))
        .ConfigureAwait(false);

    return ExitCode.Ok;
}

// The areas one stage of a cycle touches, for '--cycle <id> --for <stage>': entries outside them are rendered
// by their headline (D-151). Without --cycle nothing is narrowed. A rule is narrowed only from the plan on,
// because before it no file is declared and a rule applying to specs would never reach one in full.
static async Task<Result<AreaScope>> ScopeFor(Workspace workspace, CommandLine line, string? stage, bool rules)
{
    // The intent and the spec touch no area, so their scope needs no cycle: every headline, and in full only
    // what applies everywhere. A rule is not narrowed there at all.
    if (!rules && (string.Equals(stage, "intent", StringComparison.OrdinalIgnoreCase) || string.Equals(stage, "spec", StringComparison.OrdinalIgnoreCase)))
    {
        Result<IReadOnlyList<Area>> defined = await AreaScope.ReadDefined(workspace.SolutionRoot, CancellationToken.None).ConfigureAwait(false);

        return defined.IsFailed
            ? defined.ToResult<AreaScope>()
            : Result.Ok(AreaScope.For(defined.Value, [], AreaScope.CoveringNothing(defined.Value, workspace.SolutionRoot)));
    }

    if (!line.TryOption("cycle", out string? given))
    {
        return Result.Ok(AreaScope.Everything);
    }

    if (!CycleId.TryParse(given, out CycleId? id))
    {
        return Result.Fail<AreaScope>($"'{given}' is not a cycle id. 'sdlc cycles' lists them.");
    }

    ArtifactKind kind = string.Equals(stage, "diff", StringComparison.OrdinalIgnoreCase)
        ? ArtifactKind.Verify
        : Cycle.TryParseKind(stage, out ArtifactKind parsed)
            ? parsed
            : ArtifactKind.Intent;

    if (stage is null)
    {
        return Result.Fail<AreaScope>("--cycle says whose areas; --for says which stage: intent, spec, plan, diff or verify.");
    }

    if (rules && kind is ArtifactKind.Intent or ArtifactKind.Spec)
    {
        return Result.Ok(AreaScope.Everything);
    }

    return await AreaScope.Of(new Cycle(id.Value, workspace.SolutionRoot), kind, CancellationToken.None).ConfigureAwait(false);
}

// What each area covers today and which entries carry it, worked out on every read (D-150).
static async Task<int> AreasCommand(Workspace workspace)
{
    Result<ReadAreas.Response> result = await workspace
        .Send<ReadAreas.Query, ReadAreas.Response>(new ReadAreas.Query())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(AreasRenderer.Render(result.Value)).ConfigureAwait(false);
    return result.Value.Areas.Any(area => area.Files == 0) ? ExitCode.Refused : ExitCode.Ok;
}

// Human-invoked and never model-invoked: adopting a process is a decision about how a team works, so
// the hook denies an agent running this at all. What it does here is the mechanical half -- the
// interview, and the proposal of this solution's own constraints, belong to the skill wrapped around it,
// because they need language rather than a file listing.
static async Task<int> Init(Workspace workspace, CommandLine line)
{
    // Refused rather than ignored. Adoption writes the hook configuration and the pipeline, so a line
    // that ran while silently dropping part of what it said would leave somebody believing they had
    // asked for something they had not (D-81).
    IReadOnlyList<string> unknown = line.OptionsOtherThan("platform", "solution", "integration-branch");

    if (unknown.Count > 0)
    {
        await Console.Error.WriteLineAsync(
            $"'sdlc init' does not take {string.Join(", ", unknown.Select(name => "--" + name))}. "
            + "Its options are in 'sdlc help', under 'Options for init'.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    if (!TryPlatform(line, out Platform? platform, out string? complaint))
    {
        await Console.Error.WriteLineAsync(complaint).ConfigureAwait(false);
        return ExitCode.Usage;
    }

    string? integration = line.TryOption("integration-branch", out string? branch) ? branch.Trim() : null;

    Result<Initialise.Response> result = await workspace
        .Send<Initialise.Command, Initialise.Response>(new Initialise.Command(platform, integration))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    Initialise.Response report = result.Value;

    // Committed, then branched, then reported, so the report reads in the order things happened and
    // ends with what is left for a person. A failure in either is said on standard error after it.
    // Committed rather than left in the working tree: an untracked file is untracked when the first
    // cycle takes its diff, so the diff records the whole file as new and a plan saying it changes that
    // file can never match -- the first cycle would fail CON_25 for something adoption did.
    // Through the container, not `new GitRepository()`. Constructing the concrete type here meant
    // this one step could not be given a fake, so the only part of adoption that writes to git was
    // the only part no test could exercise -- while Workspace holds no closure precisely so that a
    // dependency can be substituted the way the application composes one.
    Result commit = report.Written.Count == 0
        ? Result.Ok()
        : workspace.Repository.Commit(
            workspace.SolutionRoot,
            report.Written,
            Templates.CommitMessage(report.Mode, report.Written));

    // The integration branch, created from what adoption just committed when it does not exist yet, and
    // switched to -- so the person's own commit of the files they place lands where approved plans wait,
    // and a cycle's plan branch starts from it rather than from whatever was checked out. After the
    // commit, so the default branch and the integration branch both hold the hooks. A branch that already
    // exists, here or on the remote, is left alone along with whatever is checked out: switching someone's
    // working copy is only this command's to do when it is the one making the branch (D-94).
    bool missing = commit.IsSuccess
        && !workspace.Repository.RefExists(workspace.SolutionRoot, report.IntegrationBranch)
        && !workspace.Repository.RefExists(workspace.SolutionRoot, "origin/" + report.IntegrationBranch);

    Result branched = missing
        ? workspace.Repository.SwitchToNewBranch(workspace.SolutionRoot, report.IntegrationBranch)
        : Result.Ok();

    await Console.Out.WriteAsync(InitRenderer.Render(report with
    {
        Branched = missing && branched.IsSuccess,
    })).ConfigureAwait(false);

    if (commit.IsFailed)
    {
        await Console.Error.WriteLineAsync(
            "The files are written, but git would not commit them: "
            + string.Join("; ", commit.Errors.Select(error => error.Message))
            + " Commit them by hand before the first cycle, or its diff will record them as new.")
            .ConfigureAwait(false);

        return ExitCode.Failed;
    }

    if (branched.IsFailed)
    {
        await Console.Error.WriteLineAsync(
            string.Create(CultureInfo.InvariantCulture, $"The integration branch {report.IntegrationBranch} could not be created: ")
            + string.Join("; ", branched.Errors.Select(error => error.Message))
            + string.Create(CultureInfo.InvariantCulture, $" Create it with 'git switch -c {report.IntegrationBranch}' before the first cycle; until then 'sdlc doctor' reports it."))
            .ConfigureAwait(false);

        return ExitCode.Failed;
    }

    return ExitCode.Ok;
}

// The solution skeleton, or the build settings an adopted solution's plan declares, and the gate is
// what permits either. Adoption writes neither: code arrives behind an approved plan like every line
// after it, and so do the files that make warnings errors -- so this asks the same question the build
// stage asks and refuses with the same reason.
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
        $"Opened {cycle.Directory}. Its plan's branch is {cycle.Id.BranchName(CycleBranchKind.Plan)}: the intent skill switches to it, and 'sdlc branch {cycle.Id} plan' prints the command when you are opening one by hand."))
        .ConfigureAwait(false);

    return ExitCode.Ok;
}

// Whether a cycle may open a pull request, without opening one. Pushing and opening it is the pr
// skill's, because it needs the platform's own tool and a sentence a person wrote; what the binary owns is
// the refusal, which has to be the same whether one cycle opens one or eight.
static async Task<int> AskPrReady(CommandLine line)
{
    return await WithCycle(line, "pr-ready", async (workspace, id) =>
        {
            Result<PullRequest.Response> result = await workspace
                .Send<PullRequest.Query, PullRequest.Response>(new PullRequest.Query(id))
                .ConfigureAwait(false);

            if (result.IsFailed)
            {
                return await Report(result, ExitCode.Failed).ConfigureAwait(false);
            }

            await Console.Out.WriteAsync(PullRequestRenderer.Render(result.Value)).ConfigureAwait(false);
            return result.Value.MayOpen ? ExitCode.Ok : ExitCode.Refused;
        }).ConfigureAwait(false);
}

// Every cycle is examined, and every one is listed -- ready or not. A list of only the eligible ones
// hides the surprise that matters most: a cycle from last week still sitting there verified, which the
// sweep picks up because every passed cycle is eligible.
static async Task<int> Sweep(Workspace workspace)
{
    List<PullRequest.Response> candidates = [];

    foreach (Cycle cycle in CycleIndex.All(workspace.SolutionRoot))
    {
        Result<PullRequest.Response> result = await workspace
            .Send<PullRequest.Query, PullRequest.Response>(new PullRequest.Query(cycle.Id))
            .ConfigureAwait(false);

        if (result.IsFailed)
        {
            return await Report(result, ExitCode.Failed).ConfigureAwait(false);
        }

        candidates.Add(result.Value);
    }

    await Console.Out.WriteAsync(PullRequestRenderer.RenderSweep(candidates)).ConfigureAwait(false);

    // Zero whether or not anything is ready. Nothing refused here; a sweep over a tidy solution with
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
// knew about 'claims' and not 'pr-ready --all' had no way to discover the second from the first.
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

    if (line.HasOption("pr-ready"))
    {
        return await Sweep(workspace).ConfigureAwait(false);
    }

    if (line.HasOption("here"))
    {
        // A skill given no cycle id asks which one it means (D-142). Zero only when exactly one is ready for
        // the step, so "use the id it printed" is the exit code and nothing has to parse prose.
        NextStep step = NextStep.Any;

        if (line.TryOption("for", out string? forStep) && !ResolveCycle.TryParseStep(forStep, out step))
        {
            await Console.Error.WriteLineAsync("--for takes to-plan, spec, plan, build, verify, remediate or pr.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        Result<ResolveCycle.Response> resolved = await workspace
            .Send<ResolveCycle.Query, ResolveCycle.Response>(new ResolveCycle.Query(step))
            .ConfigureAwait(false);

        if (resolved.IsFailed)
        {
            return await Report(resolved, ExitCode.Failed).ConfigureAwait(false);
        }

        await Console.Out.WriteAsync(ResolveRenderer.Render(resolved.Value)).ConfigureAwait(false);

        return resolved.Value.Only is not null ? ExitCode.Ok : ExitCode.Refused;
    }

    if (line.HasOption("queued"))
    {
        // One cycle named: a run started for it. Anything else after --queued is a mistake worth saying.
        CycleId? only = null;

        if (line.Arguments.Count > 0)
        {
            if (line.Arguments.Count > 1 || !CycleId.TryParse(line.Arguments[0], out only))
            {
                await Console.Error.WriteLineAsync("'sdlc cycles --queued [<id>]' takes at most one cycle id.").ConfigureAwait(false);
                return ExitCode.Usage;
            }
        }

        Result<ListQueuedCycles.Response> queue = await workspace
            .Send<ListQueuedCycles.Query, ListQueuedCycles.Response>(new ListQueuedCycles.Query(only))
            .ConfigureAwait(false);

        if (queue.IsFailed)
        {
            // A named cycle that is not queued is refused with why; with none named, a failure is the gate's.
            return await Report(queue, only is null ? ExitCode.Failed : ExitCode.Refused).ConfigureAwait(false);
        }

        await Console.Out.WriteAsync(QueuedRenderer.Render(queue.Value)).ConfigureAwait(false);

        // Zero on an empty queue, and with cycles held back. Both are answers, not problems.
        return ExitCode.Ok;
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
// 'evidence' records; 'evidence check' reads a verify against what was recorded (D-120). The sub-verb is
// read before the cycle id, because no cycle id is the word 'check'.
static async Task<int> EvidenceVerb(CommandLine line)
{
    if (line.Arguments.Count > 0 && string.Equals(line.Arguments[0], "check", StringComparison.OrdinalIgnoreCase))
    {
        CommandLine rest = line.WithoutFirstArgument();
        return await WithCycle(rest, "evidence check", (w, id) => EvidenceCheck(w, id)).ConfigureAwait(false);
    }

    return await WithCycle(line, "evidence", (w, id) => Evidence(w, line, id)).ConfigureAwait(false);
}

// Zero whatever it finds: it reports, and a non-zero exit would make a report read as a refusal (D-120).
static async Task<int> EvidenceCheck(Workspace workspace, CycleId id)
{
    Result<CheckEvidence.Response> result = await workspace
        .Send<CheckEvidence.Query, CheckEvidence.Response>(new CheckEvidence.Query(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(CheckEvidence.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

static async Task<int> Evidence(Workspace workspace, CommandLine line, CycleId id)
{
    line.TryOption("tests", out string? trx);
    line.TryOption("build-log", out string? log);
    line.TryOption("coverage", out string? coverage);

    Result<RecordEvidence.Response> result = await workspace
        .Send<RecordEvidence.Command, RecordEvidence.Response>(new RecordEvidence.Command(id, trx, log, coverage, line.HasOption("retire-pass")))
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

    if (SlicingBranch.TryParse(given, out string? slicing))
    {
        return await Workspace.Run(line, workspace => VerifySlicingCommand(workspace, line, slicing)).ConfigureAwait(false);
    }

    if (!CycleId.TryParse(given, out CycleId? id) && !CycleId.TryParseBranch(given, out id))
    {
        // A change that moves the release and nothing else is checked by reproducing it (D-133). Chosen by
        // what the diff carries, so the branch can be called anything and a name buys nothing.
        if (line.TryOption("base", out _))
        {
            return await Workspace.Run(line, workspace => VerifyUpgradeOrRefuse(workspace, line, given)).ConfigureAwait(false);
        }

        // Standard output, not error: this is a verdict a reader of a failed check has to read, and
        // the rest of the report goes there too.
        await Console.Out.WriteAsync(ChainRenderer.RenderUngoverned(given)).ConfigureAwait(false);
        return ExitCode.Refused;
    }

    return await Workspace.Run(line, workspace => VerifyChain(workspace, line, id.Value)).ConfigureAwait(false);
}

// A branch with no cycle behind it: an upgrade when everything it changes is the pin, a file adoption owns or
// the declared stack, and refused for having no chain otherwise -- exactly as before, one source file included.
static async Task<int> VerifyUpgradeOrRefuse(Workspace workspace, CommandLine line, string given)
{
    (IReadOnlyList<string>? changed, string? divergence) = await AgainstBase(workspace, line).ConfigureAwait(false);

    // A harvest's records, the ledger and closed cycles' deletions: no cycle, nothing built (D-152).
    if (changed is not null && VerifyHarvest.Carries(changed))
    {
        Result<VerifyUpgrade.Response> harvested = await workspace
            .Send<VerifyHarvest.Query, VerifyUpgrade.Response>(new VerifyHarvest.Query(changed))
            .ConfigureAwait(false);

        if (harvested.IsFailed)
        {
            return await Report(harvested, ExitCode.Failed).ConfigureAwait(false);
        }

        await Console.Out.WriteAsync(VerifyHarvest.Render(harvested.Value)).ConfigureAwait(false);
        return harvested.Value.Holds ? ExitCode.Ok : ExitCode.Refused;
    }

    if (changed is null || divergence is null || !VerifyUpgrade.Carries(changed))
    {
        await Console.Out.WriteAsync(ChainRenderer.RenderUngoverned(given)).ConfigureAwait(false);
        return ExitCode.Refused;
    }

    Result<VerifyUpgrade.Response> result = await workspace
        .Send<VerifyUpgrade.Query, VerifyUpgrade.Response>(new VerifyUpgrade.Query(changed, divergence))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(VerifyUpgrade.Render(result.Value)).ConfigureAwait(false);
    return result.Value.Holds ? ExitCode.Ok : ExitCode.Refused;
}

// The non-bypassable layer. A pipeline runs this behind branch policy, so its exit code is the thing
// standing between an unapproved chain and a merge -- which is why the whole report goes to standard
// output whether it holds or not: a reader looking at a failed check needs what passed as well.
//
// --base names the pull request's target branch, and with it the check reads what the pull request
// changes: one cycle's planning documents alone get the admission check (D-87). Without it, or when git
// cannot say what changed, the full chain runs -- a pipeline that passes nothing is as strict as before.
static async Task<int> VerifyChain(Workspace workspace, CommandLine line, CycleId id)
{
    (IReadOnlyList<string>? changed, string? divergence) = await AgainstBase(workspace, line).ConfigureAwait(false);

    Result<VerifyChain.Response> result = await workspace
        .Send<VerifyChain.Query, VerifyChain.Response>(new VerifyChain.Query(id, changed, divergence))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(ChainRenderer.Render(result.Value)).ConfigureAwait(false);
    return result.Value.Holds ? ExitCode.Ok : ExitCode.Refused;
}

// A slicing's branch, slices/<slug>/plan: every plan sliced together, each admitted as it would be alone,
// its successors sound, and no plan needing a file only a sibling creates (D-125). Still chosen by the diff:
// without --base, or when git cannot say what changed, there is no slicing to check and it is refused.
static async Task<int> VerifySlicingCommand(Workspace workspace, CommandLine line, string slug)
{
    IReadOnlyList<string>? changed = await ChangedAgainstBase(workspace, line).ConfigureAwait(false);

    if (changed is null)
    {
        await Console.Out.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"{SlicingBranch.NameOf(slug)} admits plans, and what it carries is read against --base. Without it there is nothing to admit, so the check does not hold."))
            .ConfigureAwait(false);
        return ExitCode.Refused;
    }

    Result<VerifySlicing.Response> result = await workspace
        .Send<VerifySlicing.Query, VerifySlicing.Response>(new VerifySlicing.Query(slug, changed))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(VerifySlicing.Render(result.Value)).ConfigureAwait(false);
    return result.Value.Holds ? ExitCode.Ok : ExitCode.Refused;
}

// What a pull request changes against the target branch --base names, or null when there is no --base
// or git cannot say. Null means the full chain, everywhere it is read.
static async Task<IReadOnlyList<string>?> ChangedAgainstBase(Workspace workspace, CommandLine line) =>
    (await AgainstBase(workspace, line).ConfigureAwait(false)).Changed;

// What a pull request changes against --base, and the commit its branch left the base at: the second is
// what lets the chain say when a rule moved, not only that it did (D-111).
static async Task<(IReadOnlyList<string>? Changed, string? Divergence)> AgainstBase(Workspace workspace, CommandLine line)
{
    if (!line.TryOption("base", out string? target))
    {
        return (null, null);
    }

    const string FullyQualified = "refs/heads/";
    string branch = target.StartsWith(FullyQualified, StringComparison.OrdinalIgnoreCase)
        ? target[FullyQualified.Length..]
        : target;

    string? divergence = workspace.Repository.MergeBase(workspace.SolutionRoot, "HEAD", branch);
    IReadOnlyList<string>? changed = divergence is not null
        ? workspace.Repository.CommittedChanges(workspace.SolutionRoot, divergence, "HEAD")
        : null;

    if (changed is null)
    {
        await Console.Error.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"git could not say what this pull request changes against {branch}, so the full chain is checked."))
            .ConfigureAwait(false);
    }

    return (changed, divergence);
}

// 'true' when a pull request carries one cycle's planning documents and nothing else, so a pipeline can
// skip the build and the tests and go straight to the admission check (D-87). 'false' for everything
// else -- an ungoverned branch, no --base, a diff git cannot read -- because false means the full
// pipeline, and the full pipeline is never the wrong answer, only the slower one. The word alone on
// standard output, so a pipeline step can capture it; the platform's way of setting a variable stays in
// the YAML, where each platform's knowledge already lives.
static async Task<int> AdmissionCommand(CommandLine line)
{
    if (line.Arguments.Count == 0)
    {
        await Console.Error.WriteLineAsync(
            "Which pull request? Give its branch, as in 'sdlc admission cycle/20260929_1456_32/plan --base development'.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    // The diff decides, never the branch (D-87): a pull request that changes nothing outside .sdlc/ has nothing to
    // build, whatever carries it -- a plan, a slicing, a harvest, a successor (D-193). The chain still judges it.
    return await Workspace.Run(line, async workspace =>
    {
        IReadOnlyList<string>? changed = await ChangedAgainstBase(workspace, line).ConfigureAwait(false);
        bool admission = changed is not null && Sdlc.Core.Chain.VerifyChain.RecordsOnly(changed);
        await Console.Out.WriteLineAsync(admission ? "true" : "false").ConfigureAwait(false);
        return ExitCode.Ok;
    }).ConfigureAwait(false);
}

// Whether a promotion's source branch already built and tested exactly what it merges (D-196). The pipeline pipes its
// platform's runs on that branch to standard input; 'true' lets it skip the tests, and anything unreadable is 'false'.
static async Task<int> TestedCommand(CommandLine line)
{
    if (line.Arguments.Count == 0)
    {
        await Console.Error.WriteLineAsync(
            "Tested on which branch? As in 'curl <the platform's runs on development> | sdlc tested development'.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    string runs = Console.IsInputRedirected ? await Console.In.ReadToEndAsync().ConfigureAwait(false) : string.Empty;

    return await Workspace.Run(line, async workspace =>
    {
        string source = line.Arguments[0].StartsWith("refs/heads/", StringComparison.Ordinal)
            ? line.Arguments[0]["refs/heads/".Length..]
            : line.Arguments[0];
        string? Ask(string arguments) =>
            workspace.Repository.Git(workspace.SolutionRoot, arguments) is { IsSuccess: true } answer ? answer.Value.Trim() : null;

        string? tip = Ask("rev-parse origin/" + source);
        bool tested = Promotion.AlreadyTested(
            Ask("rev-parse HEAD^{tree}"),
            tip is null ? null : Ask("rev-parse " + tip + "^{tree}"),
            tip,
            runs);

        await Console.Out.WriteLineAsync(tested ? "true" : "false").ConfigureAwait(false);
        return ExitCode.Ok;
    }).ConfigureAwait(false);
}

// Whether a pull request is a promotion, and whether it may come from where it does (D-136). 'true' on standard
// output when it is a promotion that may open, so the pipeline skips the chain; 'false' when it is not one, so
// the chain runs. A promotion from anywhere else exits 1 and says why on standard error, which fails the same
// required check the chain does.
static async Task<int> PromotionCommand(CommandLine line)
{
    if (line.Arguments.Count == 0 || !line.TryOption("base", out string? target) || string.IsNullOrWhiteSpace(target))
    {
        await Console.Error.WriteLineAsync(
            "Which pull request? Give its source and target, as in 'sdlc promotion development --base main'.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    return await Workspace.Run(line, async workspace =>
    {
        string source = line.Arguments[0];
        string integration = await SolutionDocument
            .IntegrationBranchOf(workspace.SolutionRoot, CancellationToken.None)
            .ConfigureAwait(false);

        PromotionVerdict verdict = Promotion.Judge(source, target, integration);
        await Console.Error.WriteLineAsync(Promotion.Explain(verdict, source, target, integration)).ConfigureAwait(false);

        if (verdict == PromotionVerdict.Refused)
        {
            return ExitCode.Refused;
        }

        await Console.Out.WriteLineAsync(verdict == PromotionVerdict.Allowed ? "true" : "false").ConfigureAwait(false);
        return ExitCode.Ok;
    }).ConfigureAwait(false);
}

// The branch policy adoption sets as its default, as the platform's own commands, for a person to run (D-136).
// Printed rather than applied: the binary never holds a token and changes nothing on the remote, and setting a
// policy needs an administrator.
static async Task<int> BranchPolicyCommand(Workspace workspace, CommandLine line)
{
    int approvals = 0;

    if (line.HasOption("approvals")
        && (!line.TryOption("approvals", out string? count)
            || !int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out approvals)))
    {
        await Console.Error.WriteLineAsync("--approvals takes a count, as in '--approvals 1'.").ConfigureAwait(false);
        return ExitCode.Usage;
    }

    if (line.HasOption("owner") && !line.TryOption("owner", out _))
    {
        await Console.Error.WriteLineAsync("--owner takes who must approve, as in '--owner @name' or '--owner @org/team,@other'.").ConfigureAwait(false);
        return ExitCode.Usage;
    }

    line.TryOption("owner", out string? owners);
    line.TryOption("self-approve", out string? team);

    BranchReviews reviews = new(
        approvals,
        [.. (owners ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
        line.HasOption("self-approve"),
        team);

    Result<SolutionDocument> solution = await SolutionDocument
        .ReadFileOrDraft(workspace.SolutionRoot, CancellationToken.None)
        .ConfigureAwait(false);

    if (solution.IsFailed)
    {
        return await Report(solution, ExitCode.Failed).ConfigureAwait(false);
    }

    // Whether the integration branch is the default one, a trunk, is the platform's answer, read the way the doctor
    // reads it. Unanswered, both policies are printed with a note saying which to skip.
    string? defaultBranch = solution.Value.Platform == Platform.Local
        ? null
        : await new CliBranchPolicies()
            .DefaultBranch(solution.Value.Platform, workspace.SolutionRoot, CancellationToken.None)
            .ConfigureAwait(false);

    Result<BranchPolicyScript> script = BranchPolicyScript.For(
        solution.Value.Platform,
        solution.Value.IntegrationBranch,
        workspace.SolutionRoot,
        reviews,
        defaultBranch);

    if (script.IsFailed)
    {
        return await Report(script, ExitCode.Refused).ConfigureAwait(false);
    }

    foreach ((string path, string text) in script.Value.Files)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(script.Value.Text).ConfigureAwait(false);
    return ExitCode.Ok;
}

// Which branch a kind of work on a cycle happens on, as the one git command that gets there. The command
// alone on standard output, so a skill can run exactly what was decided; why, on standard error.
static async Task<int> BranchCommand(CommandLine line)
{
    if (line.Arguments.Count < 2)
    {
        await Console.Error.WriteLineAsync(
            "Which cycle, and which kind of work? As in 'sdlc branch 20260929_1456_32 build' -- sandbox, plan, build, build-unattended or forward.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    // Remediation works where the build did, in a worktree on the branch its pull request is from, opened when
    // absent (D-185). It prints the path, which is where the session works.
    if (string.Equals(line.Arguments[1], "remediate", StringComparison.OrdinalIgnoreCase))
    {
        return await WithCycle(line, "branch", async (workspace, id) =>
        {
            Result<BuildRun.Place> place = await workspace
                .Send<BuildRun.Prepare, BuildRun.Place>(new BuildRun.Prepare(id, Remediate: true))
                .ConfigureAwait(false);

            if (place.IsFailed)
            {
                return await Report(place, ExitCode.Refused).ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync(place.Value.Path).ConfigureAwait(false);
            await Console.Error.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"{(place.Value.Opened ? "Opened" : "Already open")}: {place.Value.Branch}, where the build is. Work there; push from there and the same pull request takes it."))
                .ConfigureAwait(false);
            return ExitCode.Ok;
        }).ConfigureAwait(false);
    }

    // A sandbox is planning on a branch of its own kind, sandbox/<id>, in a worktree (D-144).
    bool sandbox = string.Equals(line.Arguments[1], "sandbox", StringComparison.OrdinalIgnoreCase);

    CycleBranchKind? kind = sandbox
        ? CycleBranchKind.Plan
        : CycleBranch.Named.Cast<CycleBranchKind?>().FirstOrDefault(k =>
            string.Equals(CycleBranch.NameOf(k!.Value), line.Arguments[1], StringComparison.OrdinalIgnoreCase));

    if (kind is null)
    {
        await Console.Error.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"'{line.Arguments[1]}' is not a kind of work. Ask for sandbox, plan, build, build-unattended or forward."))
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    return await WithCycle(line, "branch", async (workspace, id) =>
    {
        Result<StartBranch.Response> result = await workspace
            .Send<StartBranch.Query, StartBranch.Response>(new StartBranch.Query(id, kind.Value, line.HasOption("worktree"), sandbox))
            .ConfigureAwait(false);

        if (result.IsFailed)
        {
            return await Report(result, ExitCode.Refused).ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync(result.Value.Command).ConfigureAwait(false);
        await Console.Error.WriteLineAsync(result.Value.Why).ConfigureAwait(false);
        return ExitCode.Ok;
    }).ConfigureAwait(false);
}

// The factory's pipeline, for the platform solution.md records. Its own command rather than part of
// adoption, because a build agent is a choice a team makes after running cycles by hand (D-84).
static async Task<int> FactoryCommand(Workspace workspace)
{
    Result<WriteFactoryPipeline.Response> result = await workspace
        .Send<WriteFactoryPipeline.Command, WriteFactoryPipeline.Response>(new WriteFactoryPipeline.Command())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(WriteFactoryPipeline.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// A lead, fetched and fenced for 'intent --from' to interview from (D-89). 'show' is the one sub-verb today;
// listing a tracker's open leads is still being shaped.
static async Task<int> LeadCommand(CommandLine line)
{
    if (line.Arguments.Count < 2 || !string.Equals(line.Arguments[0], "show", StringComparison.OrdinalIgnoreCase))
    {
        await Console.Error.WriteLineAsync(
            "Show which lead? As in 'sdlc lead show azuredevops:1234' -- or github:56, jira:PROJ-123, suggestion:<cycle>/<finding>, successor:<slug>, or a path.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    if (!Sdlc.Core.Leads.LeadReference.TryParse(line.Arguments[1], out Sdlc.Core.Leads.LeadReference? reference, out string? problem))
    {
        await Console.Error.WriteLineAsync(problem).ConfigureAwait(false);
        return ExitCode.Usage;
    }

    return await Workspace.Run(line, async workspace =>
    {
        Result<Sdlc.Core.Leads.ReadLead.Response> result = await workspace
            .Send<Sdlc.Core.Leads.ReadLead.Query, Sdlc.Core.Leads.ReadLead.Response>(new Sdlc.Core.Leads.ReadLead.Query(reference))
            .ConfigureAwait(false);

        if (result.IsFailed)
        {
            // A successor that is not ready is a rule refusing, not a lead that could not be fetched (D-125).
            return await Report(result, reference.Source == Sdlc.Core.Leads.LeadSource.Successor ? ExitCode.Refused : ExitCode.Failed)
                .ConfigureAwait(false);
        }

        await Console.Out.WriteAsync(Sdlc.Core.Leads.ReadLead.Render(result.Value)).ConfigureAwait(false);
        return ExitCode.Ok;
    }).ConfigureAwait(false);
}

// The one git command that composes slices/<slug>/plan from the siblings' plan branches, so one pull request
// admits every plan sliced together (D-125). The command alone on standard output, as 'branch' prints it;
// the shared files, and why, on standard error.
// The plan-time claim check (D-145). Zero either way: parked or not is an answer, and the first line says which.
static async Task<int> ParkCommand(Workspace workspace, CycleId id)
{
    Result<ParkCycle.Response> result = await workspace
        .Send<ParkCycle.Command, ParkCycle.Response>(new ParkCycle.Command(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    ParkCycle.Response park = result.Value;

    if (park.AlreadyParked)
    {
        await Console.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{id} is parked already. 'sdlc backlog' says what it waits on.")).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    await Console.Out.WriteLineAsync(park.Written is { } written
        ? string.Create(CultureInfo.InvariantCulture, $"{id} is parked: {written.Why} Its spec and plan are kept; it waits on {string.Join(", ", written.WaitsOn)}, and 'sdlc unpark {id}' takes it back once each has merged.")
        : string.Create(CultureInfo.InvariantCulture, $"{id} needs nothing another unbuilt plan creates, so it can be built alone.")).ConfigureAwait(false);

    foreach (ComputeClaimGraph.Collision shared in park.Shared)
    {
        CycleId other = shared.First == id ? shared.Second : shared.First;
        await Console.Out.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"It names {string.Join(", ", shared.Files)}, which {other} names too: their pull requests may conflict, and the second to merge resolves it.")).ConfigureAwait(false);
    }

    return ExitCode.Ok;
}

// A parked cycle back on its way, once everything it waited on has merged with a pass (D-145).
static async Task<int> UnparkCommand(Workspace workspace, CycleId id)
{
    Result<UnparkCycle.Response> result = await workspace
        .Send<UnparkCycle.Command, UnparkCycle.Response>(new UnparkCycle.Command(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteLineAsync(string.Create(
        CultureInfo.InvariantCulture,
        $"{id} is unparked. Its plan was written before {string.Join(", ", result.Value.File.WaitsOn)} merged: read it again against what did -- '/sdlc:interrogate {id} spec', then plan -- before it is approved or built.")).ConfigureAwait(false);
    return ExitCode.Ok;
}

// What throwing a sandbox away would lose (D-146). It reports and deletes nothing; zero once nothing worth
// recording is left unrecorded, so the removal it prints is safe to offer.
static async Task<int> DiscardCommand(Workspace workspace)
{
    Result<DiscardSandbox.Response> result = await workspace
        .Send<DiscardSandbox.Query, DiscardSandbox.Response>(new DiscardSandbox.Query())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(DiscardRenderer.Render(result.Value)).ConfigureAwait(false);
    return result.Value.Remove is null ? ExitCode.Refused : ExitCode.Ok;
}

// The worktrees and branches whose work has reached the integration branch, and their removal (D-162).
// A view: it prints the commands and runs none.
static async Task<int> UpgradeCommand(Workspace workspace, CommandLine line)
{
    Result<UpgradeChecklist.Response> result = await workspace
        .Send<UpgradeChecklist.Query, UpgradeChecklist.Response>(new UpgradeChecklist.Query(line.Arguments.Count > 0 ? line.Arguments[0] : null, Release.Version))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(UpgradeRenderer.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

static async Task<int> TidyCommand(Workspace workspace)
{
    Result<TidyCycles.Response> result = await workspace
        .Send<TidyCycles.Query, TidyCycles.Response>(new TidyCycles.Query())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(TidyRenderer.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// Every successor and parked cycle, in the waves they come ready in (D-145). A view: it writes nothing.
static async Task<int> BacklogCommand(Workspace workspace)
{
    Result<ReadSuccessors.Response> result = await workspace
        .Send<ReadSuccessors.Query, ReadSuccessors.Response>(new ReadSuccessors.Query())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(BacklogRenderer.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// What a sandbox has approved, composed onto an admission branch the check already knows (D-144). The command
// alone on standard output, so the pr skill runs exactly what was decided; what stays behind, on standard error.
// Build it (D-178, D-184, D-187): the one stage name the binary runs rather than answers with the skill to use. With
// a cycle, that cycle; with none, every queued cycle in one process.
static async Task<int> BuildVerb(CommandLine line) =>
    line.Arguments.Count == 0
        ? await Workspace.Run(line, BuildEveryQueuedCommand).ConfigureAwait(false)
        : await WithCycle(line, "build", BuildCommand).ConfigureAwait(false);

// The plugin this release carries, which the build's process loads so it runs the release the solution pins.
static async Task<string?> BuildPlugin()
{
    string plugin = ClaudeSession.PluginDirectory;

    if (File.Exists(Path.Combine(plugin, ".claude-plugin", "plugin.json")))
    {
        return plugin;
    }

    await Console.Error.WriteLineAsync(
        $"sdlc {Sdlc.Core.Release.Version} carries no plugin at {plugin}, so the build's process cannot load this release's skills. Reinstall the tool from a package built by ci.yml.")
        .ConfigureAwait(false);
    return null;
}

// Every queued cycle, in one process started from this checkout (D-187). The playbook opens a worktree per cycle,
// as the factory's run does, and ends on the sitrep. A plan still only in a sandbox is not queued: name it, and
// that build admits it first.
static async Task<int> BuildEveryQueuedCommand(Workspace workspace)
{
    if (await BuildPlugin().ConfigureAwait(false) is not { } plugin)
    {
        return ExitCode.Usage;
    }

    await Console.Error.WriteLineAsync(string.Create(
        CultureInfo.InvariantCulture,
        $"Building every queued cycle from {workspace.SolutionRoot}, each in a worktree of its own. The build's process implements, verifies, fixes what it can and opens each pull request; it asks nothing, and this waits for it. A plan only in a sandbox is not queued: 'sdlc build <id>' admits it first."))
        .ConfigureAwait(false);

    Result<Sdlc.Core.AgentExit> exit = await workspace
        .Send<BuildRun.Launch, Sdlc.Core.AgentExit>(new BuildRun.Launch(null, workspace.SolutionRoot, plugin))
        .ConfigureAwait(false);

    if (exit.IsFailed)
    {
        return await Report(exit, ExitCode.Failed).ConfigureAwait(false);
    }

    if (exit.Value.NeverStarted)
    {
        await Console.Error.WriteLineAsync("The build's process cannot start: " + exit.Value.Problem).ConfigureAwait(false);
        return ExitCode.Failed;
    }

    if (!exit.Value.Succeeded)
    {
        await Console.Error.WriteLineAsync("The build's process ended badly: " + exit.Value.Problem + " 'sdlc cycles' says where each cycle stopped.").ConfigureAwait(false);
        return ExitCode.Failed;
    }

    return ExitCode.Ok;
}

// One cycle (D-178, D-184): the placed approval committed in its sandbox, the plan admitted, the build's worktree
// opened, its own process started there with its tools named, and the report read back.
static async Task<int> BuildCommand(Workspace workspace, CycleId id)
{
    if (await BuildPlugin().ConfigureAwait(false) is not { } plugin)
    {
        return ExitCode.Usage;
    }

    Result<BuildRun.Place> place = await workspace
        .Send<BuildRun.Prepare, BuildRun.Place>(new BuildRun.Prepare(id))
        .ConfigureAwait(false);

    if (place.IsSuccess && place.Value.AdmitFirst)
    {
        int admitted = await Workspace.RunAt(place.Value.Path, sandbox => AdmitForBuild(sandbox, id)).ConfigureAwait(false);

        if (admitted != ExitCode.Ok)
        {
            return admitted;
        }

        place = await workspace
            .Send<BuildRun.Prepare, BuildRun.Place>(new BuildRun.Prepare(id))
            .ConfigureAwait(false);

        if (place.IsSuccess && place.Value.AdmitFirst)
        {
            await Console.Error.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"The plan for {id} was admitted and the build still cannot find it outside the sandbox. Run 'sdlc branch {id} build-unattended' to see why."))
                .ConfigureAwait(false);
            return ExitCode.Refused;
        }
    }

    if (place.IsFailed)
    {
        return await Report(place, ExitCode.Refused).ConfigureAwait(false);
    }

    string worktree = place.Value.Path;

    await Console.Error.WriteLineAsync(string.Create(
        CultureInfo.InvariantCulture,
        $"Building {id} in {worktree}, on {place.Value.Branch}. Its own process implements, verifies, fixes what it can and opens the pull request; it asks nothing, and this waits for it."))
        .ConfigureAwait(false);

    Result<Sdlc.Core.AgentExit> exit = await workspace
        .Send<BuildRun.Launch, Sdlc.Core.AgentExit>(new BuildRun.Launch(id, worktree, plugin))
        .ConfigureAwait(false);

    if (exit.IsFailed)
    {
        return await Report(exit, ExitCode.Failed).ConfigureAwait(false);
    }

    if (exit.Value.NeverStarted)
    {
        await Console.Error.WriteLineAsync("The build's process cannot start: " + exit.Value.Problem).ConfigureAwait(false);
        return ExitCode.Failed;
    }

    if (!exit.Value.Succeeded)
    {
        await Console.Error.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"The build's process ended badly: {exit.Value.Problem} What it recorded is below.")).ConfigureAwait(false);
    }

    return await Workspace.RunAt(worktree, async tree =>
    {
        Result<BuildReport.Response> report = await tree
            .Send<BuildReport.Query, BuildReport.Response>(new BuildReport.Query(id))
            .ConfigureAwait(false);

        Result<ReadCycleStatus.Response> status = await tree
            .Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(new ReadCycleStatus.Query(id))
            .ConfigureAwait(false);

        string? outcome = status.IsSuccess ? status.Value.Outcome : null;

        // No verdict is no report: the process stopped before verification, and what it did is in its worktree.
        if (report.IsFailed || outcome is null)
        {
            await Console.Error.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"The build ended without a report: {(report.IsFailed ? string.Join("; ", report.Errors.Select(error => error.Message)) : "no verification was recorded")}. 'sdlc status {id}' in {worktree} says where it stopped."))
                .ConfigureAwait(false);
            return ExitCode.Failed;
        }

        await Console.Out.WriteAsync(BuildReport.Render(report.Value)).ConfigureAwait(false);

        await Console.Out.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"Outcome: {outcome}. 'sdlc status {id}' in {worktree} names the next step."))
            .ConfigureAwait(false);

        return exit.Value.Succeeded && string.Equals(outcome, "pass", StringComparison.OrdinalIgnoreCase)
            ? ExitCode.Ok
            : exit.Value.Succeeded ? ExitCode.Refused : ExitCode.Failed;
    }).ConfigureAwait(false);
}

// In the sandbox: the approval a person placed is committed -- their act, recorded; the binary writes no protected
// file -- and the plan is admitted onto its branch, by the git commands 'sdlc admit' prints (D-184).
static async Task<int> AdmitForBuild(Workspace sandbox, CycleId id)
{
    string root = sandbox.SolutionRoot;
    string folder = $"{Cycle.SdlcDirectoryName}/{Cycle.CyclesDirectoryName}/{id}/";
    string[] placed =
    [
        .. sandbox.Repository.UncommittedChanges(root)
            .Select(path => path.Replace('\\', '/'))
            .Where(path => path.StartsWith(folder, StringComparison.Ordinal)),
    ];

    if (placed.Length > 0)
    {
        Result committed = sandbox.Repository.Commit(root, placed, string.Create(CultureInfo.InvariantCulture, $"Record what was approved for {id}, for its build"));

        if (committed.IsFailed)
        {
            await Console.Error.WriteLineAsync(string.Join(Environment.NewLine, committed.Errors.Select(error => error.Message))).ConfigureAwait(false);
            return ExitCode.Failed;
        }
    }

    Result<ComposeAdmission.Response> admission = await sandbox
        .Send<ComposeAdmission.Query, ComposeAdmission.Response>(new ComposeAdmission.Query())
        .ConfigureAwait(false);

    if (admission.IsFailed)
    {
        return await Report(admission, ExitCode.Refused).ConfigureAwait(false);
    }

    if (!admission.Value.Admitted.Contains(id))
    {
        string why = admission.Value.Staying.FirstOrDefault(staying => staying.Id == id).Why ?? "the gate does not let it through";
        await Console.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{id} stays in the sandbox: {why}.")).ConfigureAwait(false);
        return ExitCode.Refused;
    }

    foreach (string step in admission.Value.Command.Split(" && ", StringSplitOptions.TrimEntries))
    {
        Result<string> ran = sandbox.Repository.Git(root, step.StartsWith("git ", StringComparison.Ordinal) ? step["git ".Length..] : step);

        if (ran.IsFailed)
        {
            return await Report(ran, ExitCode.Failed).ConfigureAwait(false);
        }
    }

    await Console.Error.WriteLineAsync(string.Create(
        CultureInfo.InvariantCulture,
        $"Admitted {string.Join(", ", admission.Value.Admitted)} onto {admission.Value.Branch}, in {admission.Value.Folder}. Nothing was pushed."))
        .ConfigureAwait(false);
    return ExitCode.Ok;
}

static async Task<int> AdmitCommand(Workspace workspace)
{
    Result<ComposeAdmission.Response> result = await workspace
        .Send<ComposeAdmission.Query, ComposeAdmission.Response>(new ComposeAdmission.Query())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    ComposeAdmission.Response admission = result.Value;

    await Console.Out.WriteLineAsync(admission.Command).ConfigureAwait(false);
    await Console.Error.WriteLineAsync(string.Create(
        CultureInfo.InvariantCulture,
        $"{admission.Branch} takes {string.Join(", ", admission.Admitted)}{(admission.Successors.Count == 0 ? string.Empty : " and " + admission.Successors.Count + " successor" + (admission.Successors.Count == 1 ? string.Empty : "s"))} onto the integration branch, in {admission.Folder}. {VerifySlicing.Shared(admission.Comparison).Detail} Check it with 'sdlc verify-chain {admission.Branch} --base <integration branch>' before it is pushed, and remove the folder once it merges."))
        .ConfigureAwait(false);

    foreach ((CycleId id, string why) in admission.Staying)
    {
        await Console.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Staying in the sandbox: {id} - {why}.")).ConfigureAwait(false);
    }

    return ExitCode.Ok;
}

static async Task<int> SlicesCommand(CommandLine line)
{
    List<CycleId> ids = [];

    foreach (string given in line.Arguments.Skip(1))
    {
        if (!CycleId.TryParse(given, out CycleId? id))
        {
            await Console.Error.WriteLineAsync($"'{given}' is not a cycle id.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        ids.Add(id.Value);
    }

    if (line.Arguments.Count < 3)
    {
        await Console.Error.WriteLineAsync(
            "Which slicing, and which cycles? As in 'sdlc slices export-totals 20261103_0900_00 20261103_0900_01'.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    return await Workspace.Run(line, async workspace =>
    {
        Result<ComposeSlicing.Response> result = await workspace
            .Send<ComposeSlicing.Query, ComposeSlicing.Response>(new ComposeSlicing.Query(line.Arguments[0], ids))
            .ConfigureAwait(false);

        if (result.IsFailed)
        {
            return await Report(result, ExitCode.Refused).ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync(result.Value.Command).ConfigureAwait(false);
        await Console.Error.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"{result.Value.Branch} composes {ids.Count} plans from their own branches. {VerifySlicing.Shared(result.Value.Comparison).Detail} Check it with 'sdlc verify-chain {result.Value.Branch} --base <integration branch>' before it is pushed."))
            .ConfigureAwait(false);
        return ExitCode.Ok;
    }).ConfigureAwait(false);
}

// Every successor a slicing wrote, and where each stands -- worked out from the record, never stored
// (D-125). A listing: it exits 0 whatever it found, and 'successor check' is the one that refuses.
// Where the build bent the approved documents, against what a person answered. Refuses while anything is wrong
// with them, so build and remediate can check what they recorded before they hand back.
static async Task<int> DeviationsCommand(Workspace workspace, CycleId id)
{
    Result<Sdlc.Core.Deviations.CycleDeviations.Response> result = await workspace
        .Send<Sdlc.Core.Deviations.CycleDeviations.Query, Sdlc.Core.Deviations.CycleDeviations.Response>(new Sdlc.Core.Deviations.CycleDeviations.Query(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(Sdlc.Core.Deviations.DeviationsRenderer.Render(result.Value)).ConfigureAwait(false);
    return result.Value.State.Problems.Count > 0 ? ExitCode.Refused : ExitCode.Ok;
}

// The report a build ends on and the pull request leads with (D-181). Exits 1 when a deviation is unsound, which
// the pull request check would refuse anyway.
static async Task<int> ReportCommand(Workspace workspace, CycleId id)
{
    Result<Sdlc.Core.Deviations.BuildReport.Response> result = await workspace
        .Send<Sdlc.Core.Deviations.BuildReport.Query, Sdlc.Core.Deviations.BuildReport.Response>(new Sdlc.Core.Deviations.BuildReport.Query(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(Sdlc.Core.Deviations.BuildReport.Render(result.Value)).ConfigureAwait(false);
    return result.Value.Deviations.Problems.Count > 0 ? ExitCode.Refused : ExitCode.Ok;
}

static async Task<int> SuccessorsCommand(Workspace workspace)
{
    Result<Sdlc.Core.Successors.ReadSuccessors.Response> result = await workspace
        .Send<Sdlc.Core.Successors.ReadSuccessors.Query, Sdlc.Core.Successors.ReadSuccessors.Response>(new Sdlc.Core.Successors.ReadSuccessors.Query())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(Sdlc.Core.Successors.SuccessorsRenderer.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// 'check' refuses a malformed file, a reference to nothing, and a loop; 'start <slug> <id>' refuses a
// successor that is not ready and deletes its file on the new cycle's plan branch (D-125).
static async Task<int> SuccessorCommand(CommandLine line)
{
    string action = line.Arguments.Count > 0 ? line.Arguments[0] : string.Empty;

    if (string.Equals(action, "check", StringComparison.OrdinalIgnoreCase))
    {
        return await Workspace.Run(line, async workspace =>
        {
            Result<Sdlc.Core.Successors.ReadSuccessors.Response> read = await workspace
                .Send<Sdlc.Core.Successors.ReadSuccessors.Query, Sdlc.Core.Successors.ReadSuccessors.Response>(new Sdlc.Core.Successors.ReadSuccessors.Query())
                .ConfigureAwait(false);

            if (read.IsFailed)
            {
                return await Report(read, ExitCode.Failed).ConfigureAwait(false);
            }

            string text = Sdlc.Core.Successors.SuccessorsRenderer.RenderCheck(read.Value);

            if (read.Value.Sound)
            {
                await Console.Out.WriteAsync(text).ConfigureAwait(false);
                return ExitCode.Ok;
            }

            await Console.Error.WriteAsync(text).ConfigureAwait(false);
            return ExitCode.Refused;
        }).ConfigureAwait(false);
    }

    if (!string.Equals(action, "start", StringComparison.OrdinalIgnoreCase)
        || line.Arguments.Count < 3
        || !CycleId.TryParse(line.Arguments[2], out CycleId? id))
    {
        await Console.Error.WriteLineAsync(
            "Check the successors, or start one? As in 'sdlc successor check', or 'sdlc successor start export-totals 20261103_0900_00' in the new cycle's folder.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    string slug = line.Arguments[1].StartsWith(Sdlc.Core.Successors.SuccessorReference.Prefix, StringComparison.Ordinal)
        ? line.Arguments[1][Sdlc.Core.Successors.SuccessorReference.Prefix.Length..]
        : line.Arguments[1];

    return await Workspace.Run(line, async workspace =>
    {
        Result<Sdlc.Core.Successors.StartSuccessor.Response> result = await workspace
            .Send<Sdlc.Core.Successors.StartSuccessor.Command, Sdlc.Core.Successors.StartSuccessor.Response>(new Sdlc.Core.Successors.StartSuccessor.Command(slug, id.Value))
            .ConfigureAwait(false);

        if (result.IsFailed)
        {
            return await Report(result, ExitCode.Refused).ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"successor:{slug} is cycle {id} now: {result.Value.Deleted} is deleted here, on its plan branch. Record lead: successor:{slug} in its intent -- that is what resolves every other reference to it."))
            .ConfigureAwait(false);
        return ExitCode.Ok;
    }).ConfigureAwait(false);
}

// Operate: production's errors, grouped, scrubbed, deduplicated and filed as leads (D-89). --since is
// hours, defaulting to a day; --dry-run files nothing. Zero whatever it found: a quiet night is an answer.
static async Task<int> OperateCommand(Workspace workspace, CommandLine line)
{
    int hours = 24;

    if (line.TryOption("since", out string? since)
        && (!int.TryParse(since.TrimEnd('h'), NumberStyles.Integer, CultureInfo.InvariantCulture, out hours) || hours < 1))
    {
        await Console.Error.WriteLineAsync("--since is a number of hours, as in --since 24h.").ConfigureAwait(false);
        return ExitCode.Usage;
    }

    Result<Sdlc.Core.Operate.RunOperate.Response> result = await workspace
        .Send<Sdlc.Core.Operate.RunOperate.Command, Sdlc.Core.Operate.RunOperate.Response>(
            new Sdlc.Core.Operate.RunOperate.Command(TimeSpan.FromHours(hours), line.HasOption("dry-run")))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(Sdlc.Core.Operate.RunOperate.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// One perspective for one stage (D-90): the full method and questions for a fresh reader, or with --cover the
// short list an author works from. A skill pastes what this prints and never restates it.
static async Task<int> PerspectiveCommand(CommandLine line)
{
    if (line.Arguments.Count < 2 || !string.Equals(line.Arguments[0], "show", StringComparison.OrdinalIgnoreCase)
        || !line.TryOption("for", out string? stage)
        || !Sdlc.Core.Perspectives.Perspective.Stages.Contains(stage.ToLowerInvariant()))
    {
        await Console.Error.WriteLineAsync(
            "Show which perspective, for which stage? As in 'sdlc perspective show security --for spec' -- intent, spec, plan, build or verify. Add --cover for an author's short list.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    Result<Sdlc.Core.Perspectives.Perspective> found = Sdlc.Core.Perspectives.PerspectiveLibrary.Find(line.Arguments[1]);

    if (found.IsFailed)
    {
        return await Report(found, ExitCode.Usage).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(Sdlc.Core.Perspectives.ActivePerspectives.RenderStage(found.Value, stage.ToLowerInvariant(), line.HasOption("cover")))
        .ConfigureAwait(false);
    return ExitCode.Ok;
}

// The guidance a solution plans with (D-195): the list, one entry in full, or the entries a cycle's plan switches on.
static async Task<int> GuidanceCommand(Workspace workspace, CommandLine line)
{
    Result<IReadOnlyList<Sdlc.Core.Guidance.GuidanceEntry>> all = await Sdlc.Core.Guidance.GuidanceLibrary
        .All(workspace.SolutionRoot, CancellationToken.None)
        .ConfigureAwait(false);

    if (all.IsFailed)
    {
        return await Report(all, ExitCode.Failed).ConfigureAwait(false);
    }

    if (line.Arguments.Count == 0)
    {
        await Console.Out.WriteAsync(Sdlc.Core.Guidance.GuidanceLibrary.RenderList(all.Value)).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    if (string.Equals(line.Arguments[0], "show", StringComparison.OrdinalIgnoreCase))
    {
        string? name = line.Arguments.Count > 1 ? line.Arguments[1] : null;

        if (all.Value.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)) is not { } found)
        {
            await Console.Error.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"Show which entry? As in 'sdlc guidance show pipeline'. There are: {string.Join(", ", all.Value.Select(entry => entry.Name))}."))
                .ConfigureAwait(false);
            return ExitCode.Usage;
        }

        await Console.Out.WriteLineAsync(found.Text).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    if (!CycleId.TryParse(line.Arguments[0], out CycleId? id))
    {
        await Console.Error.WriteLineAsync("Which cycle? As in 'sdlc guidance 20260929_1456_32', or 'sdlc guidance' for the list.").ConfigureAwait(false);
        return ExitCode.Usage;
    }

    string plan = new Cycle(id.Value, workspace.SolutionRoot).PathOf(ArtifactKind.Plan);

    if (!File.Exists(plan))
    {
        await Console.Error.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"{id} has no plan yet, so no declared files to match. 'sdlc guidance' lists every entry and the files that switch each on."))
            .ConfigureAwait(false);
        return ExitCode.Failed;
    }

    IReadOnlyList<string> declared = DeclaredFiles.Read(await File.ReadAllTextAsync(plan).ConfigureAwait(false));
    await Console.Out.WriteAsync(Sdlc.Core.Guidance.GuidanceLibrary.RenderFor(Sdlc.Core.Guidance.GuidanceLibrary.For(all.Value, declared)))
        .ConfigureAwait(false);
    return ExitCode.Ok;
}

static async Task<int> PerspectivesCommand(Workspace workspace, CycleId id)
{
    Result<Sdlc.Core.Perspectives.ActivePerspectives.Response> result = await workspace
        .Send<Sdlc.Core.Perspectives.ActivePerspectives.Query, Sdlc.Core.Perspectives.ActivePerspectives.Response>(
            new Sdlc.Core.Perspectives.ActivePerspectives.Query(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(Sdlc.Core.Perspectives.ActivePerspectives.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// The morning's page. Zero whatever it says: a page full of blocked cycles is an accurate page, and a
// pipeline publishing it as a run summary must not fail the run for reporting.
static async Task<int> SitrepCommand(Workspace workspace)
{
    Result<Sitrep.Response> result = await workspace
        .Send<Sitrep.Query, Sitrep.Response>(new Sitrep.Query())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Failed).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(SitrepRenderer.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// One verb drafts every file a person writes by hand. They follow one rule -- a tool never writes
// them, a tool prints what to put in them -- so they are one verb rather than four commands, and the
// hook rule that denies every agent write to all of them stays absolute rather than conditional.
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
        "solution" => await DraftSolution(rest).ConfigureAwait(false),
        "techstack" => await Workspace.Run(rest, w => DraftTechStack(w, rest)).ConfigureAwait(false),
        "harvest" => await Workspace.Run(rest, w => DraftHarvested(w, rest)).ConfigureAwait(false),
        "mining" => await Renamed("draft mining", "draft harvest <file>").ConfigureAwait(false),
        "convergence" => await Renamed("draft convergence", "draft harvest <file>").ConfigureAwait(false),
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

    // D-181 retired answering deviations here: the pull request leads with them and merging it accepts them.
    if (line.TryOption("accept", out _) || line.TryOption("decline", out _) || line.TryOption("note", out _))
    {
        await Console.Error.WriteLineAsync("Deviations are no longer answered here: the pull request leads with them, and merging it accepts them. To change one, tell the build what to do instead, and it goes round again into the same pull request.").ConfigureAwait(false);
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
//
// --standard, --approval and --coverage-floor are the keys a draft may move, and only when asked by name.
// Values rather than flags, because each has two ways to go and moving back is as entitled as moving on.
static async Task<int> DraftSolution(CommandLine line)
{
    SolutionStandard? standard = null;

    if (line.TryOption("standard", out string? named))
    {
        switch (named.Trim().ToUpperInvariant())
        {
            case "PROFILE":
                standard = SolutionStandard.Profile;
                break;
            case "EXISTING":
                standard = SolutionStandard.Existing;
                break;
            default:
                await Console.Error.WriteLineAsync(
                    $"'{named}' is not a standard. One of: profile, existing.").ConfigureAwait(false);
                return ExitCode.Usage;
        }
    }

    ApprovalRequirement? approval = null;

    if (line.TryOption("approval", out string? required))
    {
        if (!SolutionDocument.TryReadApproval(required, out ApprovalRequirement read))
        {
            await Console.Error.WriteLineAsync($"'{required}' is not an approval setting. One of: required, none.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        approval = read;
    }

    double? floor = null;

    if (line.TryOption("coverage-floor", out string? percent))
    {
        if (!SolutionDocument.TryReadFloor(percent, out double read))
        {
            await Console.Error.WriteLineAsync($"'{percent}' is not a coverage floor. A whole percentage from 0 to 100, such as 80%.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        floor = read;
    }

    return await Workspace.Run(line, async workspace => await PrintDraft(await workspace
        .Send<WriteSolutionDraft.Command, DraftResult>(new WriteSolutionDraft.Command(standard, approval, floor))
        .ConfigureAwait(false)).ConfigureAwait(false)).ConfigureAwait(false);
}

// --empty is a flag because its absence is the answer everybody already gave, and a stack that pins
// nothing is a decision somebody should have to type.
static async Task<int> DraftHarvested(Workspace workspace, CommandLine line)
{
    if (line.Arguments.Count == 0)
    {
        await Console.Error.WriteLineAsync(
            "Which proposal file? 'sdlc draft harvest <file>', the file a harvest skill wrote and you marked.")
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    return await PrintDraft(await workspace
        .Send<DraftHarvest.Command, DraftResult>(new DraftHarvest.Command(Path.GetFullPath(line.Arguments[0])))
        .ConfigureAwait(false)).ConfigureAwait(false);
}

// What the completed cycles can still give back, one aspect at a time (D-191). 'sdlc harvest' is the overview;
// 'harvest <aspect>' reads one aspect and names the proposal file beside the kept report, with the same
// timestamp; 'harvest record <aspect> <file>' records the aspect answered once its draft is placed; 'harvest close'
// closes the cycles every aspect has answered; '--history' reads the ledger a closed cycle leaves behind (D-152).
static async Task<int> HarvestCommand(CommandLine line)
{
    string? sub = line.Arguments.Count > 0 ? line.Arguments[0].ToLowerInvariant() : null;

    if (sub == "record")
    {
        CommandLine rest = line.WithoutFirstArgument();

        if (rest.Arguments.Count < 2 || !HarvestAspects.TryParse(rest.Arguments[0], out HarvestAspect aspect))
        {
            await Console.Error.WriteLineAsync($"Which aspect, and which proposal file? 'sdlc harvest record <aspect> <file>', the aspect one of {HarvestAspects.Listed}.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        return await Workspace.Run(rest, async workspace =>
        {
            Result<RecordHarvest.Response> recorded = await workspace
                .Send<RecordHarvest.Command, RecordHarvest.Response>(new RecordHarvest.Command(aspect, Path.GetFullPath(rest.Arguments[1])))
                .ConfigureAwait(false);

            if (recorded.IsFailed)
            {
                return await Report(recorded, ExitCode.Refused).ConfigureAwait(false);
            }

            await Console.Out.WriteAsync(HarvestRenderer.RenderRecorded(recorded.Value)).ConfigureAwait(false);
            return ExitCode.Ok;
        }).ConfigureAwait(false);
    }

    if (sub == "close")
    {
        return await Workspace.Run(line.WithoutFirstArgument(), async workspace =>
        {
            Result<CloseHarvest.Response> closed = await workspace
                .Send<CloseHarvest.Command, CloseHarvest.Response>(new CloseHarvest.Command())
                .ConfigureAwait(false);

            if (closed.IsFailed)
            {
                return await Report(closed, ExitCode.Refused).ConfigureAwait(false);
            }

            await Console.Out.WriteAsync(HarvestRenderer.RenderClosed(closed.Value)).ConfigureAwait(false);
            return ExitCode.Ok;
        }).ConfigureAwait(false);
    }

    // A harvest's readers, written and started by the binary as every reader is (D-183).
    if (sub is "read" or "review")
    {
        bool review = sub == "review";
        CommandLine rest = line.WithoutFirstArgument();
        HarvestAspect aspect = HarvestAspect.Constraints;

        if (review && (rest.Arguments.Count == 0 || !HarvestAspects.TryParse(rest.Arguments[0], out aspect)))
        {
            await Console.Error.WriteLineAsync($"Which aspect's sitting? 'sdlc harvest review <aspect> --dir <directory>', the aspect one of {HarvestAspects.Listed}.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        if (!review && rest.TryOption("aspect", out string? named) && !HarvestAspects.TryParse(named, out aspect))
        {
            await Console.Error.WriteLineAsync($"'{named}' is not an aspect. One of {HarvestAspects.Listed}.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        if (!rest.TryOption("dir", out string? directory))
        {
            await Console.Error.WriteLineAsync(review
                ? "Where should the review's readers write? 'sdlc harvest review <aspect> --dir <directory>', one outside the repository. --proposals <file> names the marked proposal file when it is not the newest."
                : "Where should the readers write? 'sdlc harvest read --dir <directory>', one outside the repository. --aspect names the sitting's aspect, constraints unless given; --cycles names its cycles.")
                .ConfigureAwait(false);
            return ExitCode.Usage;
        }

        (bool parsed, List<CycleId>? sitting) = await CyclesOption(rest).ConfigureAwait(false);

        if (!parsed)
        {
            return ExitCode.Usage;
        }

        rest.TryOption("proposals", out string? proposals);
        var timeout = TimeSpan.FromMinutes(30);

        return await Workspace.Run(rest, async workspace =>
        {
            Result<RunRound.Response> ran = review
                ? await workspace
                    .Send<HarvestReaders.Review, RunRound.Response>(new HarvestReaders.Review(
                        Path.GetFullPath(directory),
                        aspect,
                        proposals is null ? null : Path.GetFullPath(proposals),
                        timeout))
                    .ConfigureAwait(false)
                : await workspace
                    .Send<HarvestReaders.Read, RunRound.Response>(new HarvestReaders.Read(Path.GetFullPath(directory), aspect, sitting, timeout))
                    .ConfigureAwait(false);

            if (ran.IsFailed)
            {
                return await Report(ran, ExitCode.Refused).ConfigureAwait(false);
            }

            await Console.Out.WriteAsync(RunRound.Render(ran.Value)).ConfigureAwait(false);
            return ran.Value.AllAnswered ? ExitCode.Ok : ExitCode.Failed;
        }).ConfigureAwait(false);
    }

    if (line.HasOption("history"))
    {
        return await Workspace.Run(line, async workspace =>
        {
            Result<HarvestLedger> ledger = await HarvestLedger.Read(workspace.SolutionRoot, CancellationToken.None).ConfigureAwait(false);

            if (ledger.IsFailed)
            {
                return await Report(ledger, ExitCode.Refused).ConfigureAwait(false);
            }

            await Console.Out.WriteAsync(HarvestRenderer.RenderHistory(ledger.Value)).ConfigureAwait(false);
            return ExitCode.Ok;
        }).ConfigureAwait(false);
    }

    HarvestAspect? reading = null;

    if (sub is not null)
    {
        if (!HarvestAspects.TryParse(sub, out HarvestAspect parsedAspect))
        {
            await Console.Error.WriteLineAsync($"'{line.Arguments[0]}' is not something to harvest. One of {HarvestAspects.Listed}; or record, close, read, review, --history.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        reading = parsedAspect;
    }

    // A backlog is harvested a few cycles at a time, and the rest wait for a later sitting (D-166).
    (bool ok, List<CycleId>? cycles) = await CyclesOption(line).ConfigureAwait(false);

    return ok
        ? await Workspace.Run(line, workspace => Harvest(workspace, reading, cycles)).ConfigureAwait(false)
        : ExitCode.Usage;
}

// '--cycles <id>,<id>': the cycles a sitting reads. False when one is not an id, which has been said.
static async Task<(bool Parsed, List<CycleId>? Cycles)> CyclesOption(CommandLine line)
{
    if (!line.TryOption("cycles", out string? named))
    {
        return (true, null);
    }

    List<CycleId> parsed = [];

    foreach (string text in Ids(named))
    {
        if (!CycleId.TryParse(text, out CycleId? id))
        {
            await Console.Error.WriteLineAsync($"'{text}' is not a cycle id. 'sdlc cycles' lists them.").ConfigureAwait(false);
            return (false, null);
        }

        parsed.Add(id.Value);
    }

    return (true, parsed);
}

static async Task<int> Harvest(Workspace workspace, HarvestAspect? aspect, IReadOnlyList<CycleId>? cycles)
{
    // A cycle abandoned before any of it merged is read from its own branch (D-190).
    Result<GatherAbandoned.Response> gathered = await workspace
        .Send<GatherAbandoned.Command, GatherAbandoned.Response>(new GatherAbandoned.Command())
        .ConfigureAwait(false);

    if (gathered.IsFailed)
    {
        return await Report(gathered, ExitCode.Refused).ConfigureAwait(false);
    }

    Result<HarvestReport.Response> result = await workspace
        .Send<HarvestReport.Query, HarvestReport.Response>(new HarvestReport.Query(aspect, cycles))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    if (aspect is not { } reading)
    {
        await Console.Out.WriteAsync(HarvestReport.RenderOverview(result.Value)).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    DateTimeOffset now = TimeProvider.System.GetUtcNow();
    string kind = HarvestReaders.ReportKind(reading);
    string proposals = string.Create(
        CultureInfo.InvariantCulture,
        $"{Cycle.SdlcDirectoryName}/{ReportFile.DirectoryName}/{now:yyyyMMdd_HHmm_ss}-{kind}.proposed.md");

    string report = HarvestReport.Render(result.Value, proposals);
    await Console.Out.WriteAsync(report).ConfigureAwait(false);

    return result.Value.Sitting.Count == 0
        ? ExitCode.Ok
        : await Kept(workspace, kind, report).ConfigureAwait(false);
}

static async Task<int> DraftTechStack(Workspace workspace, CommandLine line) =>
    await PrintDraft(await workspace
        .Send<WriteTechStackDraft.Command, DraftResult>(
            new WriteTechStackDraft.Command(line.HasOption("empty")))
        .ConfigureAwait(false)).ConfigureAwait(false);

// 'propose' is retired (D-191): what it read from the cycles is a harvest's, and a theme's skeleton is 'sdlc theme'.
// Each subject is answered with where it went, the way a renamed command is.
static async Task<int> Propose(CommandLine line)
{
    string? what = line.Arguments.Count > 0 ? line.Arguments[0].ToLowerInvariant() : null;

    return what switch
    {
        "constraints" or "constraint" => await Renamed("propose constraints", "harvest constraints", "For a rule you have in mind, 'sdlc constraints --new' prints an empty entry at the next free id.").ConfigureAwait(false),
        "decisions" or "decision" => await Renamed("propose decisions", "harvest decisions").ConfigureAwait(false),
        "theme" => await Renamed("propose theme", "theme").ConfigureAwait(false),
        _ => await Renamed("propose", "harvest").ConfigureAwait(false),
    };
}

// A command renamed rather than removed is answered with the name it now has, the way a stage typed at the binary
// is answered with its skill: what somebody typed from memory still gets them there (D-148, D-191).
static async Task<int> Renamed(string was, string now, string? more = null)
{
    await Console.Error.WriteLineAsync($"'sdlc {was}' is now 'sdlc {now}'.{(more is null ? string.Empty : " " + more)}").ConfigureAwait(false);
    return ExitCode.Usage;
}

// What a stage reads so it does not re-decide something. Rendered on demand and never copied into an
// artifact, for the reason the constraint set is not: a copy is a second place the truth lives.
static async Task<int> Decisions(Workspace workspace, CommandLine line)
{
    bool show = line.Arguments.Count > 0 && string.Equals(line.Arguments[0], "show", StringComparison.OrdinalIgnoreCase);
    bool all = line.HasOption("all") || show;

    Result<ReadDecisions.Response> result = await workspace
        .Send<ReadDecisions.Query, ReadDecisions.Response>(new ReadDecisions.Query(all))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    // One entry in full, superseded or standing: what a reader handed only the headline asks for (D-149).
    if (show)
    {
        if (line.Arguments.Count < 2 || !DecisionId.TryParse(line.Arguments[1], out DecisionId? wanted))
        {
            await Console.Error.WriteLineAsync("Which decision? 'sdlc decisions show DEC_nn'.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        Decision? found = result.Value.Decisions.FirstOrDefault(decision => decision.Id == wanted.Value);

        if (found is null)
        {
            await Console.Error.WriteLineAsync(
                $"This solution records no {wanted}. 'sdlc decisions --brief' lists every entry it holds.").ConfigureAwait(false);
            return ExitCode.Refused;
        }

        await Console.Out.WriteAsync(DecisionsRenderer.RenderOne(found)).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    line.TryOption("for", out string? stage);
    Result<AreaScope> scope = await ScopeFor(workspace, line, stage, rules: false).ConfigureAwait(false);

    if (scope.IsFailed)
    {
        return await Report(scope, ExitCode.Refused).ConfigureAwait(false);
    }

    // One area's entries, for a person reading the record a part at a time.
    if (line.TryOption("area", out string? area))
    {
        result = Result.Ok(result.Value with { Decisions = [.. result.Value.Decisions.Where(decision => decision.InAreas.Contains(area!))] });
    }

    await Console.Out.WriteAsync(DecisionsRenderer.Render(
        result.Value,
        line.HasOption("brief") ? DecisionDetail.Brief : DecisionDetail.Full,
        scope.Value)).ConfigureAwait(false);

    return ExitCode.Ok;
}

// The theme as it stands, for a stage about to build a surface.
static async Task<int> ThemeCommand(Workspace workspace)
{
    Result<ReadTheme.Response> result = await workspace
        .Send<ReadTheme.Query, ReadTheme.Response>(new ReadTheme.Query())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(ThemeRenderer.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// What the solution declares it is built from, for a stage about to choose a shape -- with the one
// comparison here a file listing can decide: which build settings the stack takes against which are
// on disk. Zero whether or not there is a gap. A declared file that has not arrived is a plan somebody
// has yet to write, not a refusal.
static async Task<int> StackCommand(Workspace workspace)
{
    Result<ReadStackDeclaration.Response> result = await workspace
        .Send<ReadStackDeclaration.Query, ReadStackDeclaration.Response>(new ReadStackDeclaration.Query())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(StackRenderer.Render(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

// How this solution is set to run, every value with where it came from (D-138). One when solution.md cannot be
// read: its values are printed as unknown, and a view that exited zero over them would read as their defaults.
static async Task<int> SettingsCommand(Workspace workspace)
{
    Result<ReadSettings.Response> result = await workspace
        .Send<ReadSettings.Query, ReadSettings.Response>(new ReadSettings.Query())
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(SettingsRenderer.Render(result.Value)).ConfigureAwait(false);
    return result.Value.Complete ? ExitCode.Ok : ExitCode.Refused;
}

// Both reports keep a copy, because a person works from a file rather than from scrollback and these are
// long. Nothing reads one back, which is the whole difference between a report and a record -- so a
// failure to write one is said and never fatal: the answer was already printed.
static async Task<int> Kept(Workspace workspace, string kind, string body)
{
    try
    {
        string path = await ReportFile
            .Write(workspace.SolutionRoot, kind, body, TimeProvider.System.GetUtcNow(), CancellationToken.None)
            .ConfigureAwait(false);

        await Console.Out.WriteLineAsync("  kept at " + path).ConfigureAwait(false);
    }
    catch (IOException error)
    {
        await Console.Out.WriteLineAsync("  not kept: " + error.Message).ConfigureAwait(false);
    }
    catch (UnauthorizedAccessException error)
    {
        await Console.Out.WriteLineAsync("  not kept: " + error.Message).ConfigureAwait(false);
    }

    return ExitCode.Ok;
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
// Exits 1 when something is wrong, which is what `gate`, `pr-ready` and `verify-chain` do for the same
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

    // A merge that kept two branches' new suggestions under one id is mended here, not by hand (D-171).
    if (line.HasOption("renumber"))
    {
        if (id is not { } cycle)
        {
            await Console.Error.WriteLineAsync("Which cycle? 'sdlc suggestions <id> --renumber'.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        Result<RenumberSuggestions.Response> renumbered = await workspace
            .Send<RenumberSuggestions.Command, RenumberSuggestions.Response>(new RenumberSuggestions.Command(cycle))
            .ConfigureAwait(false);

        if (renumbered.IsFailed)
        {
            return await Report(renumbered, ExitCode.Refused).ConfigureAwait(false);
        }

        await Console.Out.WriteAsync(RenumberSuggestions.Render(renumbered.Value)).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    Result<Suggestions.Response> result = await workspace
        .Send<Suggestions.Query, Suggestions.Response>(new Suggestions.Query(id, line.HasOption("all")))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    // Zero whether or not anything was found. Nothing here refuses anything, and a non-zero exit would
    // make a tidy solution look like a failing one in a pipeline.
    string listing = SuggestionsRenderer.Render(result.Value);
    await Console.Out.WriteAsync(listing).ConfigureAwait(false);

    // Kept like the proposals report. This listing was short enough for a terminal when a solution held
    // one cycle; across fifty it is not, and the thing a person does with it is work through it.
    await Kept(workspace, "suggestions", listing).ConfigureAwait(false);

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
        .WriteAsync(FindingsRenderer.Render(
            result.Value,
            line.HasOption("brief"),
            line.HasOption("blocking"),
            line.TryOption("finding", out string? finding) ? finding : null))
        .ConfigureAwait(false);

    return ExitCode.Ok;
}

// 'round' records a round; 'round prompt' writes its readers' prompts (D-117). The sub-verb is read
// before the cycle id, because no cycle id is the word 'prompt'.
static async Task<int> RoundVerb(CommandLine line)
{
    if (line.Arguments.Count > 0 && string.Equals(line.Arguments[0], "prompt", StringComparison.OrdinalIgnoreCase))
    {
        CommandLine rest = line.WithoutFirstArgument();
        return await WithCycle(rest, "round prompt", (w, id) => RoundPrompt(w, rest, id)).ConfigureAwait(false);
    }

    if (line.Arguments.Count > 0 && string.Equals(line.Arguments[0], "collect", StringComparison.OrdinalIgnoreCase))
    {
        CommandLine rest = line.WithoutFirstArgument();
        return await WithCycle(rest, "round collect", (w, id) => RoundCollect(w, rest, id)).ConfigureAwait(false);
    }

    if (line.Arguments.Count > 0 && string.Equals(line.Arguments[0], "verdict", StringComparison.OrdinalIgnoreCase))
    {
        CommandLine rest = line.WithoutFirstArgument();
        return await WithCycle(rest, "round verdict", (w, id) => RoundVerdict(w, rest, id)).ConfigureAwait(false);
    }

    if (line.Arguments.Count > 0 && string.Equals(line.Arguments[0], "run", StringComparison.OrdinalIgnoreCase))
    {
        CommandLine rest = line.WithoutFirstArgument();
        return await WithCycle(rest, "round run", (w, id) => RoundRun(w, rest, id)).ConfigureAwait(false);
    }

    return await WithCycle(line, "round", (w, id) => Round(w, line, id)).ConfigureAwait(false);
}

// Starts every reader 'round prompt' wrote, the way the skills used to type it (D-183). The cycle and artifact are
// read for the shape of the command; what runs is decided by the prompts in the directory.
static async Task<int> RoundRun(Workspace workspace, CommandLine line, CycleId id)
{
    if (line.Arguments.Count < 2 || !Cycle.TryParseKind(line.Arguments[1], out _) || !line.TryOption("dir", out string? directory))
    {
        await Console.Error.WriteLineAsync(
            "Which round? 'sdlc round run <id> <intent|spec|plan|verify> --dir <directory>' starts every reader whose prompt 'sdlc round prompt' wrote there. --timeout <minutes> bounds each one; 30 when left off.")
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    int minutes = 30;

    if (line.TryOption("timeout", out string? limit)
        && (!int.TryParse(limit, CultureInfo.InvariantCulture, out minutes) || minutes < 1))
    {
        await Console.Error.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"'{limit}' is not a number of minutes. Give a whole number from 1."))
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    Result<RunRound.Response> result = await workspace
        .Send<RunRound.Command, RunRound.Response>(new RunRound.Command(
            Path.GetFullPath(directory),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(minutes)))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(RunRound.Render(result.Value)).ConfigureAwait(false);
    return result.Value.AllAnswered ? ExitCode.Ok : ExitCode.Failed;
}

static async Task<int> RoundPrompt(Workspace workspace, CommandLine line, CycleId id)
{
    if (line.Arguments.Count < 2 || !Cycle.TryParseKind(line.Arguments[1], out ArtifactKind artifact))
    {
        await Console.Error.WriteLineAsync(
            "Which artifact? 'sdlc round prompt <id> <intent|spec|plan|verify> --dir <directory>' writes every reader's prompt for a round; --reader <name> writes one, --list names them.")
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    bool list = line.HasOption("list");

    if (!line.TryOption("dir", out string? directory) && !list)
    {
        await Console.Error.WriteLineAsync(
            "Where should the prompts go? Give --dir <directory>: one outside the repository, kept for this round's answers.")
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    line.TryOption("reader", out string? reader);

    Result<ReaderPrompts.Response> result = await workspace
        .Send<ReaderPrompts.Command, ReaderPrompts.Response>(new ReaderPrompts.Command(
            id,
            artifact,
            directory is null ? string.Empty : Path.GetFullPath(directory),
            reader,
            list))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(FindingsRenderer.RenderPrompts(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

static async Task<int> RoundCollect(Workspace workspace, CommandLine line, CycleId id)
{
    if (line.Arguments.Count < 2 || !Cycle.TryParseKind(line.Arguments[1], out ArtifactKind artifact)
        || !line.TryOption("dir", out string? directory))
    {
        await Console.Error.WriteLineAsync(
            "Which round? 'sdlc round collect <id> <intent|spec|plan|verify> --dir <directory>' reads every reader's answer where 'sdlc round prompt' put the prompts. --host names the host the readers ran on.")
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    line.TryOption("host", out string? host);
    line.TryOption("answered", out string? answered);
    line.TryOption("withdrawn", out string? withdrawn);

    Result<CollectRound.Response> result = await workspace
        .Send<CollectRound.Command, CollectRound.Response>(new CollectRound.Command(
            id,
            artifact,
            Path.GetFullPath(directory),
            host,
            Ids(answered),
            Ids(withdrawn)))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(FindingsRenderer.RenderCollected(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

static string[] Ids(string? list) =>
    (list ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

static async Task<int> RoundVerdict(Workspace workspace, CommandLine line, CycleId id)
{
    if (!line.TryOption("from", out string? from))
    {
        await Console.Error.WriteLineAsync(
            "Where is the verdict? 'sdlc round verdict <id> --from <file>': the adjudicator's answer, as its process wrote it.")
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    Result<WriteVerdict.Response> result = await workspace
        .Send<WriteVerdict.Command, WriteVerdict.Response>(new WriteVerdict.Command(id, Path.GetFullPath(from)))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(FindingsRenderer.RenderVerdict(result.Value)).ConfigureAwait(false);
    return ExitCode.Ok;
}

static async Task<int> Round(Workspace workspace, CommandLine line, CycleId id)
{
    if (line.Arguments.Count < 2 || !Cycle.TryParseKind(line.Arguments[1], out ArtifactKind artifact))
    {
        await Console.Error.WriteLineAsync(
            "Which artifact? 'sdlc round <id> <intent|spec|plan|verify> --from <findings.json>' records a round; add --changed instead of --from to list the sections changed since the last one.")
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    if (line.HasOption("changed"))
    {
        Result<ChangedSections.Response> changed = await workspace
            .Send<ChangedSections.Query, ChangedSections.Response>(new ChangedSections.Query(id, artifact))
            .ConfigureAwait(false);

        if (changed.IsFailed)
        {
            return await Report(changed, ExitCode.Refused).ConfigureAwait(false);
        }

        await Console.Out.WriteAsync(FindingsRenderer.RenderChanged(changed.Value)).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    if (!line.TryOption("from", out string? from))
    {
        await Console.Error.WriteLineAsync(
            "Where are the findings? Give --from <file>: a JSON file with the readers' findings, and conflicts when there are any.")
            .ConfigureAwait(false);

        return ExitCode.Usage;
    }

    Result<RecordRound.Response> result = await workspace
        .Send<RecordRound.Command, RecordRound.Response>(new RecordRound.Command(id, artifact, Path.GetFullPath(from)))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    await Console.Out.WriteAsync(FindingsRenderer.RenderRecorded(result.Value)).ConfigureAwait(false);
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
    Result<ReadCycleStatus.ArtifactState> chosen =
        status.PanelFor(line.TryOption("stage", out string? named) ? named : null);

    if (chosen.IsFailed)
    {
        return await Report(chosen, ExitCode.Refused).ConfigureAwait(false);
    }

    ReadCycleStatus.ArtifactState state = chosen.Value;

    // A pass is only worth a pull request while the chain still holds. The panel said "verified clean"
    // while pr-ready refused, which is the panel confidently wrong about the one line people copy.
    if (string.Equals(status.Outcome, "pass", StringComparison.OrdinalIgnoreCase))
    {
        Result<VerifyChain.Response> chain = await workspace
            .Send<VerifyChain.Query, VerifyChain.Response>(new VerifyChain.Query(id))
            .ConfigureAwait(false);

        if (chain.IsSuccess)
        {
            status = status with { Chain = chain.Value };
        }

        // Merged already: the pull request it would offer is the one that merged it (D-162).
        status = status with { MergedOn = await MergedCycle.On(workspace.Repository, workspace.SolutionRoot, id, CancellationToken.None).ConfigureAwait(false) };
    }

    // On the cycle's build branch, say when the plan was amended under it (D-123): the panel would otherwise
    // walk a person through a verify the chain can never accept.
    if (await AmendedPlan.NewerOn(workspace.Repository, workspace.SolutionRoot, status.Cycle, CancellationToken.None).ConfigureAwait(false) is { } amended)
    {
        status = status with { AmendedOn = amended };
    }

    // The successors a person can act on -- ready to start, or stuck behind an abandoned cycle -- beside
    // the next command (D-125). What is still waiting is 'sdlc successors'.
    Result<Sdlc.Core.Successors.ReadSuccessors.Response> successors = await workspace
        .Send<Sdlc.Core.Successors.ReadSuccessors.Query, Sdlc.Core.Successors.ReadSuccessors.Response>(new Sdlc.Core.Successors.ReadSuccessors.Query())
        .ConfigureAwait(false);

    if (successors.IsSuccess)
    {
        status = status with { Successors = successors.Value };
    }

    // The recorded decisions this cycle departs from, beside the gate that approves them (D-139).
    IReadOnlyList<Decision> recorded = await Departures.Recorded(status.Cycle.SolutionRoot, CancellationToken.None).ConfigureAwait(false);
    status = status with
    {
        Departures = Departures.Named(await Departures.Of(status.Cycle, CancellationToken.None).ConfigureAwait(false), recorded),
    };

    // Where the build bent the approved documents, and the choices it made that they left open.
    Sdlc.Core.Deviations.DeviationsState deviations = await Sdlc.Core.Deviations.CycleDeviations.Read(status.Cycle, CancellationToken.None).ConfigureAwait(false);
    if (deviations.Current.Count > 0)
    {
        status = status with
        {
            Deviations =
            [
                .. deviations.Current.Select(deviation => Sdlc.Core.Deviations.DeviationsFile.Line(deviation.Deviation) + " - " + deviation.Standing switch
                {
                    Sdlc.Core.Deviations.DeviationStanding.Accepted => "accepted",
                    Sdlc.Core.Deviations.DeviationStanding.Declined => "changed at your request",
                    _ => "for the pull request",
                }),
            ],
        };
    }

    ArtifactKind kind = state.Kind;
    ArtifactOutline outline = await ArtifactOutlineReader
        .ReadFile(state.Path, CancellationToken.None)
        .ConfigureAwait(false);

    // The gate carries the schema evaluator with it, so an artifact is fully checked where it is
    // written rather than first in a pipeline. A shape problem found here costs a heading; the same one
    // found in CI costs a round trip.
    string? text = state.Exists
        ? await File.ReadAllTextAsync(state.Path, CancellationToken.None).ConfigureAwait(false)
        : null;
    ArtifactValidation? shape = text is null
        ? null
        : Departures.Checked(
            ArtifactSchema.Validate(kind, text, id, await NewerRules.Hold(status.Cycle, CancellationToken.None).ConfigureAwait(false), await NewerRules.Since(status.Cycle, NewerRules.Areas, CancellationToken.None).ConfigureAwait(false)),
            text,
            recorded);

    StagePanel panel = new(state, outline, shape, text is null ? null : ArtifactSize.Describe(kind, text));
    SupportedHost? host = SupportedHosts.Running(Environment.GetEnvironmentVariable);

    await Console.Out
        .WriteAsync(line.HasOption("brief")
            ? StatusRenderer.RenderBrief(status, panel, host)
            : StatusRenderer.RenderStage(status, panel, host))
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
            $"'{namedEvent}' is not a hook event. Pass --event pre-tool-use, --event turn-end or --event session-start, or leave it off for pre-tool-use.")
            .ConfigureAwait(false);
        return ExitCode.Usage;
    }

    // The plugin's copy of a hook steps aside where the solution's own configuration runs the same one, so each
    // check speaks once (D-172). It reads its payload regardless, so the host never sees a closed pipe.
    if (hookEvent != HookEvent.SessionStart
        && line.TryOption(PluginHooks.SourceOption, out string? source)
        && string.Equals(source, PluginHooks.PluginSource, StringComparison.Ordinal)
        && PluginHooks.AnsweredBySettings(
            Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR") is { Length: > 0 } project ? project : Directory.GetCurrentDirectory(),
            host.Value,
            hookEvent == HookEvent.TurnEnd))
    {
        _ = await Console.In.ReadToEndAsync().ConfigureAwait(false);
        await Console.Out.WriteAsync(hookEvent == HookEvent.TurnEnd
            ? HookProtocol.WriteTurnEndDecision(host.Value, HookDecision.Allow, null)
            : HookProtocol.WriteDecision(host.Value, HookDecision.Allow, null)).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    if (hookEvent == HookEvent.SessionStart)
    {
        // Context for the session, never a refusal. The host passes the plugin's directory on the command line,
        // and the handoff has already made this the release the directory pins (D-131).
        line.TryOption("plugin", out string? plugin);
        string? mismatch = PluginRelease.Mismatch(plugin, Release.Version);
        string? local = PluginRelease.LocalBuild(Release.Version);
        string[] context = [.. new[] { local, mismatch }.OfType<string>()];

        if (context.Length == 0)
        {
            await Console.Out.WriteAsync("{}").ConfigureAwait(false);
            return ExitCode.Ok;
        }

        System.Text.Json.Nodes.JsonObject output = new()
        {
            ["hookSpecificOutput"] = new System.Text.Json.Nodes.JsonObject
            {
                ["hookEventName"] = "SessionStart",
                ["additionalContext"] = string.Join(" ", context),
            },
        };

        // The person is told too, not only the agent: a dev build left switched on is theirs to notice (D-135).
        if (local is not null)
        {
            output["systemMessage"] = local;
        }

        await Console.Out.WriteAsync(output.ToJsonString()).ConfigureAwait(false);
        return ExitCode.Ok;
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

    // From the environment rather than the payload: the skill that starts a fresh reader marks it, and
    // the hook process inherits that from the host process it runs under.
    TurnEnd ending = turn.Value with
    {
        Reader = Environment.GetEnvironmentVariable(TurnEndGate.ReaderVariable) == "1",
    };

    // The exit code is discarded on purpose, for the reason the tool-call path gives: the decision
    // is in the payload, and a non-zero exit reads to the host as the hook itself having failed.
    _ = await Workspace.Run(line, async workspace =>
    {
        Result<HookVerdict> verdict = await workspace
            .Send<TurnEndGate.Query, HookVerdict>(new TurnEndGate.Query(ending))
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
