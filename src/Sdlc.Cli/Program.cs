using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using FluentResults;
using Sdlc.Cli;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Hooks;

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
        "gate" => await WithCycle(line, Gate).ConfigureAwait(false),
        "cycle-approval" => await WithCycle(line, (w, id) => CycleApproval(w, line, id)).ConfigureAwait(false),
        "status" => await WithCycle(line, (w, id) => Status(w, line, id)).ConfigureAwait(false),
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

static async Task<int> CycleApproval(Workspace workspace, CommandLine line, CycleId id)
{
    string by = line.TryOption("by", out string? named)
        ? named
        : Git.UserName(workspace.SolutionRoot) ?? "<your name>";

    Result<ApprovalTextResult> result = await workspace
        .Send<ApprovalTextQuery, ApprovalTextResult>(new ApprovalTextQuery(id, by))
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

    await Console.Out.WriteAsync(StatusRenderer.RenderStage(status, new StagePanel(state, outline)))
        .ConfigureAwait(false);

    return ExitCode.Ok;
}

static async Task<int> WithCycle(CommandLine line, Func<Workspace, CycleId, Task<int>> body)
{
    if (!TryCycle(line, out CycleId id, out string? complaint))
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

static bool TryCycle(CommandLine line, out CycleId id, [NotNullWhen(false)] out string? complaint)
{
    id = default;

    if (line.Arguments.Count == 0)
    {
        complaint = string.Create(
            CultureInfo.InvariantCulture,
            $"Which cycle? Give its id, as in 'sdlc {line.Verb} 20260929_1456_32'.");
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
