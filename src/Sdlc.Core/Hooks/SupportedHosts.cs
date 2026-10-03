namespace Sdlc.Core.Hooks;

/// <summary>Who owns the file a host's hook configuration lives in.</summary>
/// <remarks>
/// D-40's two cases, named. A file the framework chose the name of is the framework's to rewrite; a
/// file the host defined and the team fills is one the framework owns entries inside. Getting this
/// wrong in the owning direction destroys a person's work and reports it as "updated", which is how
/// the distinction came to be written down.
/// </remarks>
public enum ConfigOwnership
{
    /// <summary>The framework named the file, so it owns all of it and rewrites it on update.</summary>
    Framework,

    /// <summary>The host defined the file and the team fills it. The framework owns its entries only.</summary>
    Shared,
}

/// <summary>
/// What unit a host's timeout is expressed in.
/// </summary>
/// <remarks>
/// <strong>Two hosts spell the key the same way and mean different things by the number.</strong>
/// Claude Code's <c>timeout</c> is seconds; Gemini CLI's <c>timeout</c> is milliseconds, defaulting
/// to 60000. So one shared number written under one shared key would have given Gemini a ten
/// millisecond budget -- a hook killed before it can answer, on every tool call, which permits
/// everything while the configuration looks right. The key name alone was not enough to describe a
/// host, and this is the field that says so.
/// </remarks>
public enum TimeoutUnit
{
    /// <summary>The number is seconds.</summary>
    Seconds,

    /// <summary>The number is milliseconds.</summary>
    Milliseconds,
}

/// <summary>
/// How a host spells one hook entry inside its configuration.
/// </summary>
/// <remarks>
/// Two shapes, each named for the host documentation it comes from, and each covering both of the
/// things that differ: how entries nest, and how the command is spelled.
/// <para>
/// A third host reuses whichever of these matches rather than adding a field. The alternative --
/// modelling nesting and command spelling as two orthogonal fields -- buys a combination no host
/// has asked for and costs a reader two questions where one will do.
/// </para>
/// </remarks>
public enum HookEntryShape
{
    /// <summary>
    /// Commands grouped under a matcher: the event holds entries, and an entry holds a
    /// <c>hooks</c> array of <c>{ "type": "command", "command": ... }</c>.
    /// </summary>
    Grouped,

    /// <summary>
    /// The entry is the command, written for both shells: <c>{ "type": "command", "bash": ...,
    /// "powershell": ... }</c>. Copilot takes both, and a Windows machine with no bash would
    /// otherwise have a hook that silently never runs.
    /// </summary>
    Direct,
}

/// <summary>
/// Everything the framework needs to know about one agent host.
/// </summary>
/// <remarks>
/// <strong>One row rather than one switch per concern.</strong> These facts used to live in four
/// places -- the spellings in <see cref="HookProtocol"/>, the configuration's content in
/// <c>Templates.HookConfig</c>, its path and its ownership hardcoded twice in <c>Initialise</c>, and
/// the turn end's availability in a refusal in the command line -- so adding a host meant finding
/// four unrelated edits, and a host half-added looked exactly like a host supported. D-46.
/// <para>
/// <strong>What is deliberately not here.</strong> The payload key names, because
/// <see cref="HookProtocol.ReadCall"/> collects from every key any host is known to use rather than
/// from one host's set: a host that renames a field must not fall out of the rules, and narrowing the
/// reader to a row would trade that tolerance for tidiness. And the two decision shapes, because
/// those are JSON structure rather than data -- they stay as typed switches in
/// <see cref="HookProtocol"/>, which is what makes a row added without a write branch a compile-time
/// hole a test closes rather than a silent allow.
/// </para>
/// <para>
/// <strong>And what is not here yet.</strong> Where a host reads skills from, and what its
/// non-interactive invocation is for the fresh reader <c>interrogate</c> needs. Both are real facts
/// about a host and neither has a consumer in the binary today, so adding them now would be adding
/// fields nothing reads -- which is the thing this change is paired with removing.
/// </para>
/// </remarks>
/// <param name="Host">Which host this describes.</param>
/// <param name="Name">What to call it to a person, in a refusal or a listing.</param>
/// <param name="Spellings">
/// Every name it answers to on the command line, the canonical one first. The first is what generated
/// configuration passes to <c>--host</c>; the rest exist so a hook config written by hand, or copied
/// from the host's own documentation, is accepted however its author spelled it.
/// </param>
/// <param name="ConfigPath">
/// Where its hook configuration goes, relative to the solution root, with forward slashes. Written
/// the way every document writes it, so a reader can check it against the host's documentation
/// without translating separators.
/// </param>
/// <param name="Ownership">Whether the framework owns that file or only its entries.</param>
/// <param name="ConfigVersion">The schema version the file declares, when the host asks for one.</param>
/// <param name="EntryShape">How one entry is spelled inside it.</param>
/// <param name="ToolMatcher">
/// Which tools the pre-tool entry matches, when the host filters by name. Null when it does not, and
/// then every tool call is judged -- which is the safer of the two directions.
/// </param>
/// <param name="PreToolEvent">The configuration key for the moment before a tool runs.</param>
/// <param name="TurnEndEvent">
/// The configuration key for the moment a turn ends, or null when the host has no such event. Null is
/// a real answer and the one thing here that must never be guessed: a plausible-looking block for an
/// event that cannot fire is a check that looks like enforcement and is not.
/// </param>
/// <param name="TimeoutKey">
/// What the host calls the per-hook timeout. Writing the wrong one is an unbounded hook on the path
/// of every edit.
/// </param>
/// <param name="TimeoutUnit">
/// What that number means to this host. Not derivable from the key: two hosts call it
/// <c>timeout</c> and only one of them means seconds.
/// </param>
public sealed record SupportedHost(
    HookHost Host,
    string Name,
    IReadOnlyList<string> Spellings,
    string ConfigPath,
    ConfigOwnership Ownership,
    int? ConfigVersion,
    HookEntryShape EntryShape,
    string? ToolMatcher,
    string PreToolEvent,
    string? TurnEndEvent,
    string TimeoutKey,
    TimeoutUnit TimeoutUnit)
{
    /// <summary>What generated configuration passes to <c>--host</c>.</summary>
    public string Spelling => Spellings[0];

    /// <summary>Whether this host can be asked about a turn that is ending.</summary>
    public bool GovernsTurnEnd => TurnEndEvent is not null;

    /// <summary>
    /// The timeout to write, in whatever unit this host reads.
    /// </summary>
    /// <remarks>
    /// One budget, converted per host, rather than a number per row. A number per row is a number
    /// somebody changes in one place.
    /// </remarks>
    public int Timeout => TimeoutUnit == TimeoutUnit.Milliseconds
        ? (int)SupportedHosts.Patience.TotalMilliseconds
        : (int)SupportedHosts.Patience.TotalSeconds;
}

/// <summary>
/// The hosts this framework supports, and the whole of what differs between them.
/// </summary>
/// <remarks>
/// <strong>The table is the support claim.</strong> "Do we support host X" used to be a judgement
/// somebody made by reading four files; it is now a row, and the suite asserts every row is complete
/// and that each one denies a write to a protected file when fed a payload in its own shape. So a
/// host that is listed is a host that was exercised, and a host that is not listed is refused by
/// name.
/// <para>
/// Adding one is a row here, a branch in each of <see cref="HookProtocol"/>'s two write methods, and
/// a captured payload in the conformance test. Nothing else, and if a change reaches
/// <see cref="HookPolicy"/> the abstraction has leaked.
/// </para>
/// </remarks>
public static class SupportedHosts
{
    /// <summary>
    /// How long a hook may take before the host gives up on it.
    /// </summary>
    /// <remarks>
    /// Ten seconds, which is what the plugin's own configuration has always used, so the generated
    /// configuration and the shipped one agree rather than drifting. The hook runs on every tool call
    /// an agent makes, so this is on the critical path of every edit -- and a hook with no timeout at
    /// all is one that can hang the session it was meant to protect.
    /// <para>
    /// A duration rather than a number, because the number differs by host even where the key does
    /// not. <see cref="SupportedHost.Timeout"/> converts it.
    /// </para>
    /// </remarks>
    public static TimeSpan Patience { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Every host, in the order adoption writes their configuration.
    /// </summary>
    /// <remarks>
    /// Claude Code first, because that is the order the actions were reported in before this table
    /// existed and the order is what a person reads down after running <c>init</c>.
    /// </remarks>
    private static readonly SupportedHost[] _all =
    [
        new(
            HookHost.Claude,
            "Claude Code",
            ["claude", "claude-code"],
            ".claude/settings.json",

            // The host's shared project settings -- permissions, environment, the team's own hooks.
            // Adoption writes two entries into it and reads, keeps and writes back everything else.
            ConfigOwnership.Shared,
            ConfigVersion: null,
            HookEntryShape.Grouped,
            "Edit|Write|MultiEdit|NotebookEdit|Bash",
            "PreToolUse",
            "Stop",
            "timeout",
            TimeoutUnit.Seconds),

        new(
            HookHost.Copilot,
            "GitHub Copilot",
            ["copilot", "github-copilot"],

            // Framework-named, so owning the whole file is what the name already says. Copilot reads
            // every json in that directory, so a team's own configuration is a sibling file rather
            // than a sibling key.
            ".github/hooks/sdlc.json",
            ConfigOwnership.Framework,
            ConfigVersion: 1,
            HookEntryShape.Direct,

            // No matcher. Copilot's pre-tool event fires for every tool and filters nowhere, so every
            // call reaches the policy -- which is the direction that cannot quietly stop applying.
            ToolMatcher: null,
            "preToolUse",
            "agentStop",
            "timeoutSec",
            TimeoutUnit.Seconds),

        new(
            HookHost.Gemini,
            "Gemini CLI",
            ["gemini", "gemini-cli"],

            // Merged into, not owned. Gemini CLI reads this file for permissions, model settings and
            // the team's own hooks, and merges project settings over user settings -- so the
            // framework owns its entries and nothing else, the way it does on Claude Code.
            ".gemini/settings.json",
            ConfigOwnership.Shared,
            ConfigVersion: null,

            // The same nesting Claude Code uses: a matcher, then a hooks array of commands.
            HookEntryShape.Grouped,

            // The one guessed thing in this row, and it is named rather than hidden. Gemini matches
            // tool events by regular expression against the tool's name, and these three are the
            // write-and-shell tools its own documentation names. A tool it adds under a fourth name
            // will not reach the policy -- which the pipeline check still catches, because that is
            // the layer nothing bypasses. A wildcard would be safer in principle and is worse in
            // practice: the documented spellings for "match everything" are "*" and "", neither of
            // which is a valid regular expression, so a matcher that failed to compile could take
            // the whole entry with it.
            "write_file|replace|run_shell_command",
            "BeforeTool",
            "AfterAgent",

            // Spelled like Claude Code's and measured like nothing else: milliseconds, defaulting to
            // 60000. See TimeoutUnit.
            "timeout",
            TimeoutUnit.Milliseconds),
    ];

    /// <summary>Every host the framework supports.</summary>
    public static IReadOnlyList<SupportedHost> All => _all;

    /// <summary>
    /// What the framework knows about one host.
    /// </summary>
    /// <param name="host">Which host.</param>
    /// <returns>Its row.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The enum has a member with no row. That is a host half-added, which is the state this table
    /// exists to make impossible to ship.
    /// </exception>
    public static SupportedHost Of(HookHost host) =>
        Array.Find(_all, candidate => candidate.Host == host)
        ?? throw new ArgumentOutOfRangeException(
            nameof(host),
            host,
            "No host description. A HookHost member without a row in SupportedHosts is a host that "
            + "appears supported and is not.");

    /// <summary>
    /// Every spelling every host answers to, for a message about one that is not recognised.
    /// </summary>
    /// <remarks>
    /// Only what exists is offered, which is the same rule <c>Usage.Drafters</c> follows: naming a
    /// host the adapter cannot speak to would send somebody looking for the flag that makes it work.
    /// </remarks>
    /// <returns>The canonical spellings, in table order.</returns>
    public static IReadOnlyList<string> Spellings() => [.. _all.Select(host => host.Spelling)];
}
