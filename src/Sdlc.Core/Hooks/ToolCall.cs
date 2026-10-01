namespace Sdlc.Core.Hooks;

/// <summary>What an agent is about to do, in terms the policy can reason about.</summary>
/// <param name="ToolName">The tool's name as the host called it.</param>
/// <param name="Kind">What that tool does, normalised across hosts.</param>
/// <param name="Paths">Every file path the call names, absolute or relative as given.</param>
/// <param name="Command">The shell command, when the call runs one.</param>
/// <param name="WorkingDirectory">Where the host says it is working.</param>
public sealed record ToolCall(
    string ToolName,
    ToolKind Kind,
    IReadOnlyList<string> Paths,
    string? Command,
    string? WorkingDirectory);

/// <summary>
/// What a tool does, independent of what a host calls it.
/// </summary>
/// <remarks>
/// This is the whole of what the adapters have to agree on. Hosts name the same three capabilities
/// differently — <c>Edit</c>/<c>Write</c> against <c>edit</c>/<c>create</c> against
/// <c>write_file</c>/<c>replace</c>, and <c>Bash</c> against <c>bash</c> against
/// <c>run_shell_command</c> — so the policy is written against these and each host maps onto them.
/// </remarks>
public enum ToolKind
{
    /// <summary>Something the policy has no opinion about: reading, searching, listing.</summary>
    Harmless,

    /// <summary>Writes or edits a file at a known path.</summary>
    FileWrite,

    /// <summary>Runs a shell command, which may do anything including write a file.</summary>
    Shell,
}
