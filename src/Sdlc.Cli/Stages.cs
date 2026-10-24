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
    private static readonly Dictionary<string, string> _skills = new(StringComparer.OrdinalIgnoreCase)
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
        ["pr"] = "pushes, opens the pull request, and links the work item",
        ["batch-build"] = "completes every approved cycle, concurrently where the claims allow",
        ["resolve-conflict"] = "resolves a collision on a file no cycle can claim exclusively",
    };

    /// <summary>Whether a verb names a stage skill.</summary>
    /// <param name="verb">The verb a person typed.</param>
    /// <returns>True when it is a skill rather than an unknown command.</returns>
    public static bool IsSkill(string verb) => _skills.ContainsKey(verb);

    /// <summary>Every verb this class will explain as a skill.</summary>
    /// <remarks>
    /// <para><strong>Nothing in the binary calls this, and it stays by D-48's first exception:</strong> the
    /// dictionary above is the single statement of which stages are skills rather than commands, and the
    /// suite is what holds it to the skills actually shipped. Every verb here is a verb the status panel
    /// can print as the next command, so one that no skill answers sends a person somewhere this tool does
    /// not go — the failure the remarks on this class are written against.</para>
    ///
    /// <para>Asserting it needs the whole set rather than one lookup. <see cref="IsSkill"/> can only
    /// confirm a verb somebody already named, so a test built on it has to carry its own list of verbs to
    /// ask about — and that list was the defect: it named six of the twelve, and three of those were only
    /// reached through a string match on the renderer's source that a rename would have silently
    /// broken.</para>
    /// </remarks>
    public static IReadOnlyCollection<string> Skills => _skills.Keys;

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
                $"'{verb}' is a skill, not a command. It {_skills[verb]}, which needs language rather than",
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
