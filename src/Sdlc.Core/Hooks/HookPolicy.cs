using FluentResults;
using MofonCode.Core;

namespace Sdlc.Core.Hooks;

/// <summary>Asks the policy what to do about a tool call.</summary>
/// <param name="Call">What the agent is about to do.</param>
public sealed record HookPolicyQuery(ToolCall Call) : IRequest<HookVerdict>;

/// <summary>What the policy decided, and why.</summary>
/// <param name="Decision">Allow or deny.</param>
/// <param name="Reason">Why it was refused. Empty on an allow.</param>
/// <param name="Path">The path that caused a refusal, when one did.</param>
public sealed record HookVerdict(HookDecision Decision, string Reason, string? Path)
{
    /// <summary>Nothing to say about this call.</summary>
    public static HookVerdict Allowed { get; } = new(HookDecision.Allow, string.Empty, null);
}

/// <summary>
/// Decides whether a tool call may proceed.
/// </summary>
/// <remarks>
/// This is the fast local layer, not the guarantee. It catches an agent that forgets - which is the
/// common case and the one worth catching early - while the pipeline check is what nothing can get
/// past. So its rules are the ones that are absolute and cheap: a handful of files only a person
/// writes, in file writes and in shell commands alike.
/// </remarks>
public sealed class HookPolicyHandler : IRequestHandler<HookPolicyQuery, HookVerdict>
{
    /// <inheritdoc />
    public Task<Result<HookVerdict>> Handle(HookPolicyQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(Result.Ok(Decide(request.Call)));
    }

    /// <summary>
    /// Applies the policy.
    /// </summary>
    /// <param name="call">What the agent is about to do.</param>
    /// <returns>The verdict.</returns>
    public static HookVerdict Decide(ToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);

        switch (call.Kind)
        {
            case ToolKind.FileWrite:
                foreach (string path in call.Paths)
                {
                    if (ProtectedPaths.IsProtected(path, out ProtectedFile? file))
                    {
                        return new HookVerdict(
                            HookDecision.Deny,
                            ProtectedPaths.Refusal(path, file!),
                            path);
                    }
                }

                return HookVerdict.Allowed;

            case ToolKind.Shell:
                // A shell can write a file without any tool naming its path, so the file rules alone
                // would be a front door with the back door open.
                if (ProtectedPaths.CommandTouchesProtected(call.Command, out ProtectedFile? touched))
                {
                    return new HookVerdict(
                        HookDecision.Deny,
                        ProtectedPaths.CommandRefusal(touched!),
                        null);
                }

                return HookVerdict.Allowed;

            case ToolKind.Harmless:
            default:
                return HookVerdict.Allowed;
        }
    }
}
