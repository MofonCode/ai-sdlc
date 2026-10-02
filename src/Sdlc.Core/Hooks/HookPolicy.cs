namespace Sdlc.Core.Hooks;

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
/// <para>
/// A static function rather than a request and a handler, deliberately, and this is the one place in
/// the gate where that is the right call. The pre-tool hook runs on every tool call an agent makes,
/// so its start-up cost is on the critical path of every edit - which is why
/// <c>Sdlc.Cli.csproj</c> turns on tiered PGO and invariant globalization - and building a container
/// to reach a pure function over a <see cref="ToolCall"/> would spend that budget on indirection.
/// It is also not an application operation by the framework's own test: one expression, called from
/// one place, that cannot fail.
/// </para>
/// <para>
/// It was briefly both. A <c>HookPolicyQuery</c> existed, was never registered, and so could never
/// be dispatched - the command line called the static method all along. The architecture suite's
/// composition rule is what found it, on its first run.
/// </para>
/// </remarks>
public static class HookPolicy
{
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
