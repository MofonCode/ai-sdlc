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

    /// <summary>
    /// The host defined the file and the framework owns one named top-level section of it.
    /// </summary>
    /// <remarks>
    /// Antigravity's shape, and the easiest of the three to own honestly: its <c>hooks.json</c> is a map
    /// of named hook groups, so the framework's group is one key and every other key is somebody else's.
    /// Replacing that one key is both the update and the uninstall, with no need to match entries by the
    /// command they run.
    /// </remarks>
    Section,
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
/// fields nothing reads -- which is the thing this change is paired with removing. How a person invokes
/// a stage did get a consumer, the status panel's Next line, and arrived with it (D-100).
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
/// <param name="Ownership">Whether the framework owns that file, one section of it, or only its entries.</param>
/// <param name="EventsKey">
/// The single top-level key the event map sits under. <c>hooks</c> on the two hosts that have one of
/// those; on Antigravity the file is a map of named hook groups instead, so the key is the framework's
/// group name and owning it is owning that key.
/// </param>
/// <param name="ConfigVersion">The schema version the file declares, when the host asks for one.</param>
/// <param name="EntryShape">How one entry is spelled inside it.</param>
/// <param name="ToolMatcher">
/// Which tools the pre-tool entry matches, when the host filters by name. Null when it does not, and
/// then every tool call is judged -- which is the safer of the two directions.
/// </param>
/// <param name="NeutralDecision">
/// What to write when the policy has nothing to say, or null where the honest answer is nothing at all.
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
/// <para>
/// <strong>The unit is not derivable from the key, and a host that proves it needs a field here.</strong>
/// Both hosts below read seconds, so there is nothing to carry. Gemini CLI, briefly in this table before
/// the product turned out to have been retired, called it <c>timeout</c> exactly as Claude Code does and
/// read it as milliseconds -- one shared number would have given it a ten-millisecond budget, which is a
/// hook killed before it answers on every tool call. If a host like that is added, the unit becomes a
/// field again rather than an assumption.
/// </para>
/// </param>
/// <param name="StageInvocation">
/// How a person invokes a stage by name, as a format whose <c>{0}</c> is the stage and its arguments --
/// or null where a stage is asked for in a sentence, because the host chooses a skill by matching the
/// request against its description and has no name to type.
/// </param>
/// <param name="ShellMarker">
/// The environment variable the host sets in every shell it runs, which is how the binary knows it was
/// called from inside that host. Null where none is documented: a guessed marker is a panel that names
/// one host's invocation to a person using another.
/// </param>
public sealed record SupportedHost(
    HookHost Host,
    string Name,
    IReadOnlyList<string> Spellings,
    string ConfigPath,
    ConfigOwnership Ownership,
    string EventsKey,
    int? ConfigVersion,
    HookEntryShape EntryShape,
    string? ToolMatcher,
    string? NeutralDecision,
    string PreToolEvent,
    string? TurnEndEvent,
    string TimeoutKey,
    string? StageInvocation,
    string? ShellMarker)
{
    /// <summary>What generated configuration passes to <c>--host</c>.</summary>
    public string Spelling => Spellings[0];

    /// <summary>Whether this host can be asked about a turn that is ending.</summary>
    public bool GovernsTurnEnd => TurnEndEvent is not null;
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
    /// Seconds, because every host in the table reads seconds. It was briefly a duration converted per
    /// host, for one that did not -- see <see cref="SupportedHost.TimeoutKey"/>.
    /// </para>
    /// </remarks>
    public const int TimeoutSeconds = 10;

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
            EventsKey: "hooks",
            ConfigVersion: null,
            HookEntryShape.Grouped,

            // PowerShell is a separate tool from Bash and this matcher is a regular expression over the
            // tool's name, so leaving it out exempted every PowerShell call from the shell rule -- on
            // the one platform where PowerShell is the shell somebody actually has. The path rules held;
            // the rule that catches a write no path names did not.
            "Edit|Write|MultiEdit|NotebookEdit|Bash|PowerShell",

            // Nothing, on an allow. D-39.
            NeutralDecision: null,
            "PreToolUse",
            "Stop",
            "timeout",
            StageInvocation: "/sdlc:{0}",

            // Set to 1 in every shell Claude Code starts, interactive or not, so a skill's own call to
            // `sdlc status` and the factory's headless run both see it. A person's own terminal does not.
            ShellMarker: "CLAUDECODE"),

        new(
            HookHost.Copilot,
            "GitHub Copilot",
            ["copilot", "github-copilot"],

            // Framework-named, so owning the whole file is what the name already says. Copilot reads
            // every json in that directory, so a team's own configuration is a sibling file rather
            // than a sibling key.
            ".github/hooks/sdlc.json",
            ConfigOwnership.Framework,
            EventsKey: "hooks",
            ConfigVersion: 1,
            HookEntryShape.Direct,

            // No matcher. Copilot's pre-tool event fires for every tool and filters nowhere, so every
            // call reaches the policy -- which is the direction that cannot quietly stop applying.
            ToolMatcher: null,
            NeutralDecision: null,
            "preToolUse",
            "agentStop",
            "timeoutSec",

            // Asked for in prose: Copilot matches the request against each skill's description.
            StageInvocation: null,
            ShellMarker: null),

        new(
            HookHost.Antigravity,
            "Antigravity",
            ["antigravity", "agy"],

            // The host's own path, and a map of named hook groups rather than one hooks object. So the
            // framework owns the group it named and every sibling key is somebody else's -- which is
            // the cleanest of the three ownerships, and the only one where an uninstall is a key
            // being removed.
            ".agents/hooks.json",
            ConfigOwnership.Section,
            EventsKey: "sdlc",
            ConfigVersion: null,

            // The same nesting Claude Code uses, for the pre-tool event. Antigravity's other events
            // take a flat list instead, and nothing here configures one of those.
            HookEntryShape.Grouped,

            // Every tool, which its documentation spells "*" -- so this is the one host where match
            // everything is a documented value rather than an inference. It costs a process per tool
            // call, including reads, and buys the property that no tool can be renamed out of the
            // policy's reach. `ToolMatcher: null` is not available: the matcher is part of the
            // documented handler shape and omitting it is not documented as valid.
            "*",

            // **The one host that cannot be silent, and the reason D-39 needed a second paragraph.**
            // `decision` is mandatory on every pre-tool call, so writing nothing is not an available
            // answer -- and an empty object is reported to deny, which would refuse every tool call an
            // agent makes. Of the five values, `ask` is the only one that hands the question back:
            // Google documents it as prompting while respecting the person's Always Allow settings,
            // which is what silence does on the other two hosts. `allow` is the bypass D-39 refuses
            // and `force_ask` is the same override in the restrictive direction. See D-51.
            NeutralDecision: "ask",
            "PreToolUse",

            // **No turn end here, deliberately, and this is the first row to say so.** Antigravity has
            // a `Stop` event and sending a turn back is `decision: "continue"` -- but its payload
            // carries no flag saying the agent is already continuing because a previous stop was
            // refused. `executionNum` increments per attempt and its own documentation says it does
            // not signal prior continuations. Without that, a refusal is an endless turn rather than a
            // governed stop, so the moment is left ungoverned and `sdlc hook --event turn-end` refuses
            // it by name. The build is still gated at the pull request, which depends on no host.
            TurnEndEvent: null,
            "timeout",

            // Asked for in prose, as on Copilot.
            StageInvocation: null,
            ShellMarker: null),
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

    /// <summary>
    /// The host whose shell this process is running in, when one says so.
    /// </summary>
    /// <remarks>
    /// Read from the environment rather than passed as a flag, because the call that needs it is the one
    /// every stage skill makes at the end of a lap: a flag is one more argument eighteen skills have to
    /// remember, on three hosts, and the one that forgets prints the wrong form without anything failing.
    /// Null is the common case and a safe one -- the panel then names a stage the host-neutral way, which
    /// the binary answers by naming the invocation.
    /// </remarks>
    /// <param name="environment">Reads one environment variable; null or empty when it is not set.</param>
    /// <returns>The host, or null when no host's marker is set.</returns>
    public static SupportedHost? Running(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return Array.Find(
            _all,
            host => host.ShellMarker is { } marker && !string.IsNullOrEmpty(environment(marker)));
    }
}
