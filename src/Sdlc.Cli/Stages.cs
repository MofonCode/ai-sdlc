namespace Sdlc.Cli;

/// <summary>
/// The stages that are skills rather than commands, and what to say when somebody types one.
/// </summary>
/// <remarks>
/// The status panel ends with the literal next command, because a panel that reports a state and leaves a
/// person to work out the command has stopped one step short. For a stage that needs language -- writing
/// an intent, judging a spec against the rules -- that command names a skill, and the binary has no way to
/// do the work.
/// <para>
/// So the binary answers anyway. <strong>A tool must never send somebody somewhere it does not go:</strong>
/// the panel printing <c>sdlc spec &lt;id&gt;</c> and the binary replying "Unknown command" would make the
/// most-read output in the plugin wrong, and the person would conclude the install is broken.
/// </para>
/// </remarks>
internal static class Stages
{
    /// <summary>What each stage skill is for, by the verb a person would type.</summary>
    private static readonly Dictionary<string, string> Skills = new(StringComparer.OrdinalIgnoreCase)
    {
        ["intent"] = "interviews a person into the problem the change exists to solve",
        ["spec"] = "derives testable acceptance criteria from the intent",
        ["plan"] = "decides how, and what will prove each criterion",
        ["interrogate"] = "judges one artifact against the rules, in a fresh process",
        ["refine"] = "applies an artifact's findings to that artifact",
        ["steer"] = "carries a mid-cycle correction down the chain",
        ["build"] = "implements the plan and records what happened as evidence",
        ["verify"] = "adjudicates the build against the spec, from the evidence alone",
        ["remediate"] = "applies a blocked verification's findings to source and tests",
        ["land"] = "pushes, opens the pull request, and links the work item",
        ["batch-build"] = "completes every approved cycle, concurrently where the claims allow",
        ["resolve-conflict"] = "resolves a collision on a file no cycle can claim exclusively",
    };

    /// <summary>Whether a verb names a stage skill.</summary>
    /// <param name="verb">The verb a person typed.</param>
    /// <returns>True when it is a skill rather than an unknown command.</returns>
    public static bool IsSkill(string verb) => Skills.ContainsKey(verb);

    /// <summary>
    /// Explains that a stage is a skill, and how to invoke it.
    /// </summary>
    /// <param name="verb">Which stage.</param>
    /// <param name="arguments">What the person passed after it, so the suggestion is copy-pasteable.</param>
    /// <returns>What to print.</returns>
    public static string Explain(string verb, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verb);
        ArgumentNullException.ThrowIfNull(arguments);

        string tail = arguments.Count > 0 ? " " + string.Join(' ', arguments) : string.Empty;

        return string.Join(
            Environment.NewLine,
            [
                $"'{verb}' is a skill, not a command. It {Skills[verb]}, which needs language rather than",
                "a file listing -- so your agent runs it and this binary checks what it produced.",
                string.Empty,
                "Ask your agent for it. In Claude Code:",
                string.Empty,
                $"    /sdlc:{verb}{tail}",
                string.Empty,
                "What this binary does on its own is in 'sdlc help': the rules in force, where a cycle has",
                "got to, whether its build may run, and the entries a person pastes by hand.",
            ]) + Environment.NewLine;
    }
}
