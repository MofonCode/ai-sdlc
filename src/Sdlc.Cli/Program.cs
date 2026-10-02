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
using Sdlc.Core.Drafts;
using Sdlc.Core.Findings;
using Sdlc.Core.Hooks;
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

try
{
    return line.Verb switch
    {
        "constraints" => await Workspace.Run(line, w => Constraints(w, line)).ConfigureAwait(false),
        "gate" => await WithCycle(line, "gate", Gate).ConfigureAwait(false),
        "verify-chain" => await WithCycle(line, "verify-chain", VerifyChain).ConfigureAwait(false),
        "draft" => await Draft(line).ConfigureAwait(false),
        "status" => await WithCycle(line, "status", (w, id) => Status(w, line, id)).ConfigureAwait(false),
        "suggestions" => await Workspace.Run(line, w => Suggestions(w, line)).ConfigureAwait(false),
        "hook" => await Hook(line).ConfigureAwait(false),
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

    Result<ConstraintSet> result = await workspace
        .Send<EffectiveConstraintsQuery, ConstraintSet>(new EffectiveConstraintsQuery(artifact))
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

static async Task<int> Gate(Workspace workspace, CycleId id)
{
    Result<GateVerdict> result = await workspace
        .Send<ApprovalGateQuery, GateVerdict>(new ApprovalGateQuery(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    GateVerdict verdict = result.Value;

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

// The non-bypassable layer. A pipeline runs this behind branch policy, so its exit code is the thing
// standing between an unapproved chain and a merge -- which is why the whole report goes to standard
// output whether it holds or not: a reader looking at a failed check needs what passed as well.
static async Task<int> VerifyChain(Workspace workspace, CycleId id)
{
    Result<ChainVerdict> result = await workspace
        .Send<VerifyChainQuery, ChainVerdict>(new VerifyChainQuery(id))
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
        : Git.UserName(workspace.SolutionRoot) ?? "<your name>";

    if (!TryDecision(line, out GateDecision decision, out string? clash))
    {
        await Console.Error.WriteLineAsync(clash).ConfigureAwait(false);
        return ExitCode.Usage;
    }

    Result<ApprovalTextResult> result = await workspace
        .Send<ApprovalTextQuery, ApprovalTextResult>(new ApprovalTextQuery(id, by, decision))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    ApprovalTextResult text = result.Value;

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
    string by = Git.UserName(workspace.SolutionRoot) ?? "<your name>";

    return await PrintDraft(await workspace
        .Send<AbandonTextQuery, DraftResult>(new AbandonTextQuery(id, by))
        .ConfigureAwait(false)).ConfigureAwait(false);
}

static async Task<int> DraftConstraint(Workspace workspace) =>
    await PrintDraft(await workspace
        .Send<ConstraintTextQuery, DraftResult>(new ConstraintTextQuery())
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
        .Send<ProfileTextQuery, DraftResult>(new ProfileTextQuery(line.Arguments[0]))
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

    Result<IReadOnlyList<Suggestion>> result = await workspace
        .Send<SuggestionsQuery, IReadOnlyList<Suggestion>>(new SuggestionsQuery(id))
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

static async Task<int> Status(Workspace workspace, CommandLine line, CycleId id)
{
    Result<CycleStatus> result = await workspace
        .Send<CycleStatusQuery, CycleStatus>(new CycleStatusQuery(id))
        .ConfigureAwait(false);

    if (result.IsFailed)
    {
        return await Report(result, ExitCode.Refused).ConfigureAwait(false);
    }

    CycleStatus status = result.Value;

    // --stage names the artifact whose panel to show. Without it, the panel is for whichever artifact
    // the cycle is waiting on, which is what a person wants after any stage finishes.
    ArtifactKind kind = line.TryOption("stage", out string? named) && Cycle.TryParseKind(named, out ArtifactKind asked)
        ? asked
        : status.Blocking?.Kind ?? ArtifactKind.Plan;

    ArtifactState state = status.Artifacts.First(artifact => artifact.Kind == kind);
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

    await Console.Out
        .WriteAsync(StatusRenderer.RenderStage(status, new StagePanel(state, outline, shape)))
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
        await Console.Error.WriteLineAsync(
            "Which host? Pass --host claude or --host copilot.").ConfigureAwait(false);
        return ExitCode.Usage;
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

    HookVerdict verdict = HookPolicyHandler.Decide(call.Value);

    await Console.Out.WriteAsync(
        HookProtocol.WriteDecision(host.Value, verdict.Decision, verdict.Reason)).ConfigureAwait(false);

    // Zero either way: the decision is in the payload, and a non-zero exit would read to the host as
    // the hook itself having failed.
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

    if (!CycleId.TryParse(line.Arguments[0], out CycleId? parsed))
    {
        complaint = string.Create(
            CultureInfo.InvariantCulture,
            $"'{line.Arguments[0]}' is not a cycle id. They are written as date, 24-hour time and seconds, as in 20260929_1456_32.");
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
