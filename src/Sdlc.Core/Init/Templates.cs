using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Hooks;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Init;

/// <summary>Where the code lives, which decides which pipeline is generated.</summary>
/// <remarks>
/// A platform is not a host. A host is the agent program -- Claude Code, Copilot -- and a platform is
/// GitHub or Azure DevOps. The two were called the same thing in an earlier draft and the confusion
/// reached the pipeline section, so the vocabulary is kept apart deliberately.
/// </remarks>
public enum Platform
{
    /// <summary>GitHub, with Actions and a pull request.</summary>
    GitHub,

    /// <summary>Azure DevOps, with Pipelines and a pull request.</summary>
    AzureDevOps,

    /// <summary>
    /// Nowhere yet. No remote, no pipeline, and the whole cycle runs on this machine.
    /// </summary>
    /// <remarks>
    /// <para><strong>This is the one platform where the guarantee is not installed, and it is recorded so
    /// that fact can be stated rather than inferred.</strong> §13's pull-request check is the only
    /// non-bypassable layer, and nothing on one machine stands in for it (D-78). A solution
    /// with no pipeline is therefore governed by habit, and the difference between choosing that and
    /// having a broken GitHub install is a difference <c>doctor</c> has to be able to see (D-53).</para>
    /// <para>It exists because the alternative was worse: somebody trying the framework on a repository
    /// with no remote had to pick a platform they were not using, which wrote a pipeline for a service
    /// nothing would ever send it to -- and then <c>pr</c> reached for a tool with nowhere to push.</para>
    /// </remarks>
    Local,
}

/// <summary>
/// What <c>init</c> writes.
/// </summary>
/// <remarks>
/// String constants rather than files on disk. The gate ships as one executable that a pre-tool hook
/// invokes on every tool call, so it has to work published single-file with nothing beside it -- and a
/// template read from a path next to the binary is a template that is missing in exactly that case.
/// <para>
/// Nothing here names a package version. Versions are resolved at adoption and written into the files
/// that own them, so a template cannot go stale in a way a reader would not notice.
/// </para>
/// </remarks>
public static class Templates
{
    /// <summary>
    /// How a hook configuration is written out.
    /// </summary>
    /// <remarks>
    /// Indented, because a person reads this file and edits the half beside it. The same relaxed
    /// escaping <see cref="HookSettings"/> and the hook's own output use, and for the same reason: a
    /// settings file holds paths and commands, and an apostrophe written as <c>&#x5c;u0027</c> is
    /// legible to a parser and to nobody else.
    /// </remarks>
    private static readonly JsonSerializerOptions _hookConfig = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The kernel version a scaffolded solution is pinned to.
    /// </summary>
    /// <remarks>
    /// Here rather than inline so the suite can assert the reference slice and the pin agree. The slice
    /// uses <c>ResultEnvelope</c>, <c>ToUserMessage</c> and <c>ToLogString</c>, which exist from
    /// <c>0.2.0</c> -- so a scaffold pinned below that produces a solution that does not compile, and
    /// one pinned above a published version produces one that does not restore.
    /// </remarks>
    public const string KernelVersion = "0.2.0";

    /// <summary>
    /// The platforms a pipeline is written for.
    /// </summary>
    /// <remarks>
    /// Named once so that adding a platform cannot leave the suite checking a shorter list than the one
    /// adoption writes. The tests that assert every pipeline runs <c>verify-chain</c> used to enumerate
    /// the enum, so adding <see cref="Platform.Local"/> -- which has no pipeline by design -- made them
    /// throw rather than skip it; the alternative, each test excluding Local for itself, is three places
    /// that have to agree about which platforms are hosted.
    /// </remarks>
    public static IReadOnlyList<Platform> Hosted { get; } = [Platform.GitHub, Platform.AzureDevOps];

    /// <summary>The solution document, which carries the three keys and nothing else.</summary>
    /// <param name="origin">Where the code came from.</param>
    /// <param name="standard">Which conventions new work follows.</param>
    /// <param name="platform">Where it is pushed, which decides whether a pipeline was written.</param>
    /// <returns>The file's contents.</returns>
    public static string Solution(
        SolutionOrigin origin,
        SolutionStandard standard,
        Platform platform) => string.Join(
        Environment.NewLine,
        [
            "---",
            .. SolutionDocument.Keys(origin, standard, platform),
            "---",
            string.Empty,
            "# What this solution is",
            string.Empty,
            "Replace this with a paragraph a newcomer could read: what the solution does, and who for.",
            "Nothing reads this prose -- the three keys above are what record where this code came",
            "from, decide which conventions new work follows, and say whether a pull-request check",
            "governs a merge -- so it is here for people.",
            string.Empty,
        ]);

    /// <summary>
    /// The first draft of a declared stack: the default profile's rows, and every shipped rule on.
    /// </summary>
    /// <remarks>
    /// <para><strong>A draft, never the file.</strong> Adoption used to write this straight into place,
    /// which made it the one moment a tool wrote a protected file -- and forced an adopted repository
    /// to start with empty rows, because a guess written down as a declaration is worse than nothing.
    /// As a draft that argument goes away: a person reads it, deletes what is not true of their
    /// solution, and renames it, so every solution can start from the same full table (D-76).</para>
    /// <para><strong>Everything the default brings is on the page.</strong> The rules that name a
    /// framework are rows here rather than a consequence of a name recorded somewhere else, so what
    /// somebody reads is exactly what a plan will be judged against, and switching one off is an edit
    /// to the line that says it is on.</para>
    /// <para><strong>One flat table of defaults, always.</strong> The profile composes its rows from a
    /// shared platform set plus its own, and that composition happens here rather than in the solution
    /// -- what lands on disk is a table a person owns and edits, never a derived file (D-68).</para>
    /// <para><strong>The build settings start on where nothing predates them, and off where code
    /// does.</strong> <c>Directory.Build.props</c> makes warnings errors, so on a codebase that existed
    /// first it can fail a build over files nobody touched. That has to be something a team switched
    /// on, never something it got by renaming a draft unread (D-77).</para>
    /// </remarks>
    /// <param name="origin">
    /// Where the solution's code came from, which decides how the build-settings rows start.
    /// </param>
    /// <returns>The draft's contents.</returns>
    public static string TechStack(SolutionOrigin origin = SolutionOrigin.Scaffolded)
    {
        StackProfile profile = StackProfiles.Default;

        return string.Join(
        Environment.NewLine,
        [
            "# What this solution is built from",
            string.Empty,
            "A plan may depart from anything in the first table, in either direction -- reaching for",
            "something it does not declare, or leaving something it declares unapplied in work that",
            "calls for it. Saying nothing about departing is what is forbidden, which is `CON_15` -- so",
            "those rows are a declaration of defaults rather than a set of rules. To make a default",
            "bind, add a `LOC_` constraint or an architecture test.",
            string.Empty,
            "**Delete what is not true of this solution.** A row pins a choice and no row leaves it to",
            "each plan, so a file with nothing in it is a legitimate answer: nothing is pinned, no stack",
            "rule applies, and every plan chooses freely.",
            string.Empty,
            .. Declared(profile),
            StackRules.Heading,
            string.Empty,
            "The rules that may name a language, framework or package, one row each. `true` holds every",
            "plan to the rule; `false`, or no row at all, does not. `sdlc constraints` prints each one",
            "in full, with what it forbids and why.",
            string.Empty,
            .. StackRules.Table(
                ShippedProfileLibrary.Rules().Select(rule => (ShippedProfileLibrary.RowId(rule), rule.Statement)),
                on: true),
            string.Empty,
            BuildSettings.Heading,
            string.Empty,
            "The files that decide how .NET code is built here, one row each. `true` declares that this",
            "solution takes the file; `false`, or no row at all, that it does not. A row writes nothing:",
            "a cycle whose approved plan declares the file brings it, with `sdlc scaffold`, and",
            "`sdlc stack` shows which are declared and not there yet. Each is created once and is yours",
            "to amend afterwards. The solution skeleton arrives with all five whatever these rows say,",
            "because it does not compile without them.",
            string.Empty,
            .. BuildSettings.Table(on: origin == SolutionOrigin.Scaffolded),
            string.Empty,
            .. profile.Notes,
        ]);
    }

    /// <summary>The profile's defaults as the table a solution reads.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The heading, the table, and a blank line after it.</returns>
    private static IEnumerable<string> Declared(StackProfile profile) =>
    [
        "## Defaults",
        string.Empty,
        "| Concern | Choice |",
        "|---|---|",
        .. profile.Defaults.Select(row => string.Create(
            CultureInfo.InvariantCulture,
            $"| {row.Concern} | {row.Choice} |")),
        string.Empty,
    ];

    /// <summary>The framework's own README inside a governed solution.</summary>
    /// <returns>The file's contents.</returns>
    public static string Readme() => string.Join(
        Environment.NewLine,
        [
            "# .sdlc",
            string.Empty,
            "This directory is how a change gets from a request to a merged pull request. It is written",
            "by `sdlc`, and regenerated when the framework is updated -- so do not edit this file.",
            string.Empty,
            "## What a cycle is",
            string.Empty,
            "One governed change: an id, a directory under `cycles/`, and a `cycle/<id>` branch. The id is",
            "the moment the cycle opened, to the second, so cycles sort chronologically.",
            string.Empty,
            "## The chain",
            string.Empty,
            "```",
            "intent -> spec -> plan -> build -> verify -> pr",
            "                    ^        |",
            "                    +- remediate (at most 3) -+",
            "```",
            string.Empty,
            "Intent, spec and plan are all required, always. Each is written, challenged against the",
            "constraints, and revised until a round raises nothing blocking. A stage will not run until",
            "the artifact before it has such a round.",
            string.Empty,
            "## What the files are",
            string.Empty,
            "| File | What it holds | Who writes it |",
            "|---|---|---|",
            "| `solution.md` | where this code came from, which conventions new work follows, and where it is pushed | a person, by hand |",
            "| `techstack.md` | what the solution is built from, which stack rules it is held to, and which build settings files it takes | a person, by hand |",
            "| `constraints.md` | this solution's own rules | a person, by hand |",
            "| `decisions.md` | the choices this solution has settled, so a cycle reads the argument rather than having it again | a person, by hand |",
            "| `cycles/<id>/intent.md` | why the change exists | the intent stage |",
            "| `cycles/<id>/spec.md` | what done means | the spec stage |",
            "| `cycles/<id>/plan.md` | how, and the proof per criterion | the plan stage |",
            "| `cycles/<id>/verify.md` | whether the change did the spec justice | the verify stage |",
            "| `cycles/<id>/approval.md` | the gate decisions, append-only | a person, by hand |",
            "| `cycles/<id>/abandoned.md` | why a cycle stopped | a person, by hand |",
            "| `cycles/<id>/findings/` | what each interrogation found | the interrogate stage |",
            "| `cycles/<id>/evidence/` | the diff, the logs, the test results | the build stage |",
            string.Empty,
            "Six of those are written only by a person. No tool writes them; a tool prints what belongs",
            "in them and the person places it. The pre-tool hook denies every agent write to all six,",
            "with no exception -- which is what lets the rule be stated in one sentence.",
            string.Empty,
            "`techstack.md` is placed by a person and never written by anything. Adoption does not",
            "create it: `sdlc draft techstack` writes a first draft from the framework's default, you",
            "delete what is not true here, and the rename places it. **No cycle opens until it is",
            "there.** It is the second rule source a plan is read against, and `CON_15` judges every plan",
            "against it -- so an agent able to edit it could satisfy that rule by adding the row it wanted",
            "instead of arguing for it. A plan that reaches for something undeclared departs and says what",
            "the declared default would have cost, which is an argument rather than an edit here. Changing",
            "a default is its own act: `sdlc draft techstack`, then a rename.",
            string.Empty,
            "**It holds three tables.** The first pins choices: a row is a default every plan is read",
            "against, and no row leaves that choice to each plan. The second lists the stack rules --",
            "the ones that may name a framework -- with `true` or `false` beside each, and only the ones",
            "marked `true` bind. The third lists the .NET build settings files the same way: `true`",
            "declares that this solution takes the file. **An empty file is a legitimate answer**:",
            "nothing is pinned, no stack rule applies, and every plan chooses freely.",
            string.Empty,
            "**A build settings row writes nothing.** `Directory.Build.props` makes warnings errors, so",
            "the file arriving is the moment a build can fail on code nobody touched. It arrives inside a",
            "cycle whose approved plan declares it -- `sdlc scaffold <id>` writes the build settings a",
            "plan names and leaves alone any already there -- so that moment is one change a person",
            "approved. `sdlc stack` shows which files the stack takes and which are not on disk yet.",
            string.Empty,
            "**`sdlc draft techstack` writes two files.** `techstack.draft.md` is what you rename: a byte copy of",
            "the declared stack, or the default when there is none yet, or nothing at all with `--empty`.",
            "The other is the menu, written as a report under `reports/` with its path printed -- every",
            "concern the shipped profiles know about with what each declares for it, the alternatives the",
            "framework has argued, every stack rule it ships, each build settings file with what it does",
            "and what it costs, and an empty table for a solution starting over. It is written fresh every run, read back by nothing, and **never renamed into place**:",
            "a declared stack listing answers this solution has not chosen is one a plan can reach into",
            "and truthfully not be departing from anything. `sdlc doctor` reports a declared stack with",
            "the menu inside it, one with a row left half-filled, and one that is not there.",
            string.Empty,
            "## What an artifact looks like",
            string.Empty,
            "Every artifact is an envelope carrying a body. The envelope is the same two keys and one",
            "section on all of them; the body differs, because a different thing reads each one.",
            string.Empty,
            "```markdown",
            "---",
            "framework_version: " + Cycle.FrameworkVersion,
            "cycle: 20260929_1456_32",
            "---",
            string.Empty,
            "# what this artifact is about, in one line",
            string.Empty,
            "## <the body sections for this kind>",
            string.Empty,
            "## Open",
            "```",
            string.Empty,
            "| Artifact | Its body | Its own frontmatter key |",
            "|---|---|---|",
            "| intent | Problem, Wanted outcomes, Fixed decisions, Must not change | `type` |",
            "| spec | Acceptance criteria, Out of scope | |",
            "| plan | Shape and departures, Files, Proof, Risk | `tier` |",
            "| verify | Criteria | `outcome` |",
            string.Empty,
            "Three things the schema will refuse, so they are worth knowing before you write one by hand:",
            string.Empty,
            "**`## Open` is required even when empty.** Without the section there is no way to tell",
            "\"nothing is open\" from \"the author forgot\", and those need different responses. What goes in",
            "it is something in scope that this artifact cannot settle, with the reason it cannot -- not",
            "work nobody has started, not something wrong, and not something chosen out of scope.",
            string.Empty,
            "**A key or section nothing declared is a violation, not a tolerated extra.** A section exists",
            "because something reads it, and decoration costs attention at every interrogation. If you",
            "need to carry a ticket number on every artifact, that is a change to the framework rather",
            "than a field to add locally.",
            string.Empty,
            "**`cycle` has to match the directory the file sits in.** The duplication is the point: a file",
            "copied out of one cycle into another keeps the id it was written under, and nothing else",
            "would notice.",
            string.Empty,
            "`sdlc status <id> --stage <artifact>` shows what the schema makes of an artifact, and says",
            "nothing at all when the shape is sound.",
            string.Empty,
            "## Commands",
            string.Empty,
            "```",
            "sdlc doctor                           is this install sound, and what is left to place",
            "sdlc new                              open a cycle",
            "sdlc scaffold <id>                    write the solution skeleton, or the build settings a",
            "                                      plan declares, once that plan is approved",
            "sdlc stack                            what the stack declares, and what is not on disk yet",
            "sdlc constraints [--for <artifact>]   the rules in force",
            "sdlc status <id> [--stage <name>]     where a cycle has got to",
            "sdlc gate <id>                        may this cycle's build run?",
            "sdlc draft approval <id>              the gate entry, for a person to paste",
            "sdlc draft abandon <id>               the record that stops a cycle",
            "sdlc draft solution                   the solution document, beside itself",
            "sdlc draft techstack                  the declared stack beside itself, and the menu",
            "sdlc evidence <id>                    record what a build produced",
            "sdlc cycles                           every cycle here, newest first",
            "sdlc cycles --claims                  which cycles may be built together",
            "sdlc suggestions [<id>]               the open non-blocking findings",
            "sdlc propose constraints              what the history says the rules are missing",
            "sdlc decisions [--brief|--all]        the choices this solution has settled",
            "sdlc propose decisions                what an approved cycle settled, to record",
            "sdlc theme                          the roles its surfaces use, and their values",
            "sdlc propose theme                  the roles a theme declares, or the empty ones",
            "sdlc pr-ready <id>                    may a cycle open a pull request?",
            "sdlc cycles --pr-ready                every cycle, saying which may open one",
            "sdlc verify-chain <id>                the whole chain, as the pipeline checks it",
            "```",
            string.Empty,
            "## What is enforced, and by what",
            string.Empty,
            "Three layers, and only the last is a guarantee.",
            string.Empty,
            "| Layer | How | Who can get past it |",
            "|---|---|---|",
            "| advisory | skill and instruction text | the agent, constantly |",
            "| local | the `sdlc` binary, a pre-tool hook, a turn-end hook | anyone with a shell |",
            "| non-bypassable | `sdlc verify-chain` as a pull-request check | nobody without repo admin |",
            string.Empty,
            "The promise is the third layer: nothing merges unless the chain is complete, approved and",
            "hash-matched. The local layer is there to make that cheap to satisfy, not to be the proof.",
            string.Empty,
            "Two hooks, for two moments. The pre-tool hook refuses a write to one of the six files only a",
            "person writes.",
            "The turn-end hook is asked before an agent finishes, and sends the turn back when this cycle's",
            "recorded evidence shows a failing suite, or when the working tree has moved since the build was",
            "recorded. That second one is what makes a warnings-as-errors build and an architecture test",
            "enforcement rather than something a reviewer finds afterwards -- nothing can report done over a",
            "red suite. It never runs a build itself: it reads what `sdlc evidence` wrote.",
            string.Empty,
            "Nothing here checks a push. Push a cycle branch as often as you like: what may merge is the",
            "pull-request check's to decide, and on a solution with no remote nothing mechanical decides",
            "it at all -- `sdlc doctor` says so on every run.",
            string.Empty,
            "The stages -- intent, spec, plan, build, verify, pr -- are skills rather than commands: they",
            "need language, so an agent runs them and this binary checks what they produced. Typing one,",
            "such as `sdlc spec <id>`, says so and names the invocation.",
            string.Empty,
        ]);

    /// <summary>
    /// The lean agent instruction file.
    /// </summary>
    /// <remarks>
    /// Under 200 lines, and it never contains constraints. A copy of the rules here would be a second
    /// place they live, and the copy is always the one that goes stale -- so this points at the command
    /// that renders them instead. It also carries nothing that a cycle could make untrue, which is why
    /// there is no status, no current work and no list of cycles in it.
    /// </remarks>
    /// <param name="solutionName">What to call the solution.</param>
    /// <returns>The file's contents.</returns>
    public static string Agents(string solutionName) => string.Join(
        Environment.NewLine,
        [
            "# " + solutionName,
            string.Empty,
            "Replace this paragraph with a short summary of what this solution does.",
            string.Empty,
            "## Where things are",
            string.Empty,
            "| What | Where |",
            "|---|---|",
            "| what this is built from | `.sdlc/techstack.md` |",
            "| how the lifecycle works | `.sdlc/README.md` |",
            "| the rules in force | `sdlc constraints`, which renders all three tiers |",
            "| this solution's own rules, once there are any | `.sdlc/constraints.md` |",
            string.Empty,
            "Add the repository's own map here: which project is which, and where to start reading.",
            string.Empty,
            "## Building and testing",
            string.Empty,
            "Replace these with the real commands.",
            string.Empty,
            "```",
            "dotnet build",
            "dotnet test",
            "```",
            string.Empty,
            "## Running it locally",
            string.Empty,
            "Replace this with how to start the application and what to open.",
            string.Empty,
            "**If this solution grows an Aspire AppHost, it reads two variables.** `batch-build` exports",
            "`SDLC_RUN_MODE` (`local` or `batch`) and `SDLC_RUN_ID` (unique per cycle) so that several",
            "cycles can build at once without colliding. In `batch`, give container names, volume names",
            "and database names the run id -- not just the ports. Two app hosts calling",
            "`WithDataVolume()` share one volume, which means two writers on one database, and that",
            "fails strangely rather than loudly. Group them too: Docker Desktop draws an app host's",
            "services as one collapsible row when its containers carry `com.docker.compose.project` --",
            "but only if `com.docker.compose.config-hash` is set as well, to any stable value, because",
            "the project label on its own does not group. Give that group name the run id for the same",
            "reason the container names get it, and set `com.docker.compose.service` so the rows read as",
            "resource names rather than container names.",
            string.Empty,
            "## Governed changes",
            string.Empty,
            "This solution's lifecycle is governed. A change moves through intent, spec and plan, each",
            "challenged against a fixed set of rules, and a person approves all three before any code is",
            "written. `.sdlc/README.md` is the whole of it.",
            string.Empty,
            "**Do not restate the rules here.** Run `sdlc constraints` to see what is in force. A copy in",
            "this file would be a second place they live, and the copy is the one that goes stale.",
            string.Empty,
            "**Six files are a person's to write**: `approval.md`, `abandoned.md`, `constraints.md`,",
            "`decisions.md`, `solution.md` and `techstack.md`. Every agent write to them is denied. Run the",
            "matching `sdlc draft` command -- or `sdlc propose` for `constraints.md` and `decisions.md` --",
            "and give the person what it prints.",
            string.Empty,
        ]);

    /// <summary>
    /// The hook configuration for one host, built from that host's row.
    /// </summary>
    /// <remarks>
    /// Every host gets the same binary with a different <c>--host</c>, so the adapter normalises the
    /// vocabulary in one place and no host's quirks reach the policy. What differs -- the event keys,
    /// whether entries group under a matcher, whether the file declares a version, what the timeout
    /// is called -- is <see cref="SupportedHosts"/>, and this assembles it.
    /// <para>
    /// <strong>Assembled as JSON rather than written out as text, and that is a correctness
    /// property.</strong> Each configuration used to be a hand-written list of lines per host, where
    /// a stray comma is a file the host rejects and a hook that never runs permits everything while
    /// the directory still looks governed. There was a test for exactly that, because the risk was
    /// real. Built from objects, a malformed file is no longer something a test has to catch.
    /// </para>
    /// <para>
    /// <strong>A host with no turn-end event gets no turn-end block</strong> -- not an empty one, and
    /// not a plausible-looking one. That case is <see cref="SupportedHost.TurnEndEvent"/> being null,
    /// and it is the one field here that must never be guessed: a block for an event that cannot fire
    /// is precisely the directory that appears enforced and is not. All three hosts currently have the
    /// event. Copilot's is <c>agentStop</c>, which the framework spent a release claiming did not
    /// exist -- see D-46.
    /// </para>
    /// </remarks>
    /// <param name="host">Which host's configuration to write.</param>
    /// <returns>The file's contents.</returns>
    public static string HookConfig(HookHost host)
    {
        SupportedHost described = SupportedHosts.Of(host);

        JsonObject events = [];

        events[described.PreToolEvent] = Entries(described, described.ToolMatcher, string.Empty);

        if (described.TurnEndEvent is { } turnEnd)
        {
            // No matcher on the turn end even where the host supports one: a turn is not a tool, so
            // there is nothing to filter by name.
            events[turnEnd] = Entries(described, null, " --event turn-end");
        }

        JsonObject file = [];

        if (described.ConfigVersion is { } version)
        {
            file["version"] = version;
        }

        // One top-level key on every host so far, and only its name differs: `hooks` where the file is
        // the framework's or the host's own hook object, and the framework's group name where the file
        // is a map of named groups.
        file[described.EventsKey] = events;

        return file.ToJsonString(_hookConfig) + Environment.NewLine;
    }

    /// <summary>
    /// One event's entries, in the shape the host spells them.
    /// </summary>
    /// <param name="described">The host's row.</param>
    /// <param name="matcher">Which tools to match, or null for every one.</param>
    /// <param name="arguments">What to append to the command, for an event that needs an option.</param>
    /// <returns>The array the event key holds.</returns>
    private static JsonArray Entries(SupportedHost described, string? matcher, string arguments)
    {
        string command = "sdlc hook --host " + described.Spelling + arguments;

        if (described.EntryShape == HookEntryShape.Direct)
        {
            // The entry is the command, written for both shells. Copilot takes a PowerShell command
            // as well as a bash one, and a Windows-only machine with no bash would otherwise have a
            // hook that silently never runs.
            return
            [
                new JsonObject
                {
                    ["type"] = "command",
                    ["bash"] = command,
                    ["powershell"] = command,
                    [described.TimeoutKey] = SupportedHosts.TimeoutSeconds,
                },
            ];
        }

        JsonObject entry = [];

        if (matcher is not null)
        {
            entry["matcher"] = matcher;
        }

        entry["hooks"] = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "command",
                ["command"] = command,
                [described.TimeoutKey] = SupportedHosts.TimeoutSeconds,
            },
        };

        return [entry];
    }

    /// <summary>
    /// The pull-request pipeline for one platform.
    /// </summary>
    /// <remarks>
    /// About thirty lines wrapping one command, which is the whole point of keeping every check inside
    /// <c>sdlc verify-chain</c>: two implementations of the guarantee would eventually disagree, and the
    /// one a person reads would not be the one that blocks the merge.
    /// <para>
    /// Coverage is collected and displayed and never thresholded. A threshold gets met by testing what is
    /// easy to test.
    /// </para>
    /// <para>
    /// The Azure DevOps template carries a note saying it has never been run against a real server, and
    /// that note is deliberately in the generated file rather than only in the framework's own documents --
    /// a pipeline nobody has run looks exactly like a gate.
    /// </para>
    /// <para>
    /// <strong>It has no trigger of its own, and says so.</strong> It carried a <c>pr:</c> block for a
    /// while, which Azure Repos ignores: that keyword is for GitHub and Bitbucket, and on Azure Repos a
    /// pipeline runs on a pull request only when a build validation branch policy names it. With
    /// <c>trigger: none</c> beside it, the file ran on nothing and read as a gate. The step a person has
    /// to take is now in the file, where the person reading it is.
    /// </para>
    /// <para>
    /// <strong>It installs the gate before it calls it.</strong> The first version did not, and assumed
    /// <c>sdlc</c> was on the runner's path -- so the one line the whole guarantee rests on would have
    /// reported command-not-found, from the step a reader trusts most. The install is pinned, because an
    /// unpinned governance tool can change what a repository is judged by without anybody choosing it,
    /// and it runs before the build so a missing gate fails in seconds rather than after a full suite.
    /// </para>
    /// <para>
    /// The package is not on a feed yet, and that step says so in the generated file on the same terms as
    /// the Azure DevOps note. A step that cannot resolve is a loud failure before the check it feeds,
    /// which is the honest shape; writing no step at all was the quiet one.
    /// </para>
    /// </remarks>
    /// <param name="platform">Which platform.</param>
    /// <returns>The file's contents.</returns>
    public static string Pipeline(Platform platform) => platform switch
    {
        Platform.GitHub => string.Join(
            Environment.NewLine,
            [
                "name: pull request",
                string.Empty,
                "on:",
                "  pull_request:",
                string.Empty,
                "jobs:",
                "  verify:",
                "    runs-on: ubuntu-latest",
                "    steps:",
                "      - uses: actions/checkout@v4",
                "        with:",
                "          fetch-depth: 0",
                string.Empty,
                "      - uses: actions/setup-dotnet@v4",
                "        with:",
                "          global-json-file: global.json",
                string.Empty,
                "      # The binary the last step runs. Pinned, because an unpinned governance tool can",
                "      # change what this repository is judged by without anybody choosing it. First, so",
                "      # a missing gate fails in seconds rather than after a full build and test run.",
                "      #",
                "      # NOTE: MofonCode.Sdlc.Cli is not on a public feed yet, so this step does not",
                "      # resolve until it is, or until a feed holding it is configured in nuget.config.",
                "      # It fails here, loudly, rather than at 'sdlc verify-chain' -- which would report",
                "      # command-not-found from the one line the whole guarantee rests on.",
                "      - name: install the gate",
                $"        run: dotnet tool install --global MofonCode.Sdlc.Cli --version {Release.Version}",
                string.Empty,
                "      - run: dotnet build --configuration Release",
                string.Empty,
                "      - run: dotnet test --configuration Release --coverage --coverage-output-format cobertura",
                string.Empty,
                "      # Displayed, never thresholded. A threshold gets met by testing what is easy to test.",
                "      - uses: irongut/CodeCoverageSummary@v1.3.0",
                "        if: always()",
                "        with:",
                "          filename: '**/*.cobertura.xml'",
                string.Empty,
                "      # The guarantee. Every check lives in the binary, so this stays one line.",
                "      #",
                "      # This runs on every pull request, including ones that are not governed cycles --",
                "      # a dependabot bump, a revert, a hotfix branch. Those fail, and the failure is the",
                "      # answer rather than a fault: every class of change runs the full chain, so a change",
                "      # with no intent, spec and plan has not been verified by anything.",
                "      #",
                "      # If you want that class of branch to merge anyway, say so in branch policy by not",
                "      # requiring this check on it. That is a decision to accept ungoverned changes of that",
                "      # kind, recorded where somebody can read it. DELETING THIS STEP is not the same",
                "      # decision: it accepts them everywhere and leaves nothing saying so.",
                "      - name: verify the artifact chain",
                "        run: sdlc verify-chain \"${CYCLE_ID}\"",
                "        env:",
                "          CYCLE_ID: ${{ github.head_ref }}",
                string.Empty,
            ]),

        Platform.AzureDevOps => string.Join(
            Environment.NewLine,
            [
                "# NOTE: this pipeline has never run against a real Azure DevOps server. The GitHub one",
                "# has; this one is the same six steps written from the documentation. If the first run",
                "# fails on syntax or on a task version, that is why -- the one line that matters is the",
                "# last, and 'sdlc verify-chain' is what has to execute for the gate to mean anything.",
                "#",
                "# Said here rather than only in the framework's own notes, because a pipeline nobody has",
                "# run looks exactly like a gate, and a check that appears to enforce and does not is",
                "# worse than one that never pretended.",
                string.Empty,
                "# This runs on pull requests, and only when a branch policy starts it. Azure Repos ignores a",
                "# 'pr:' trigger written here -- that keyword is for GitHub and Bitbucket -- so until a person",
                "# adds the policy, NOTHING HERE RUNS AT ALL and every pull request merges unchecked:",
                "#",
                "#   Repos > Branches > the default branch > Branch policies > Build validation: add this",
                "#   pipeline, make it Required, and let it expire immediately when the branch is updated.",
                "trigger: none",
                string.Empty,
                "pool:",
                "  vmImage: ubuntu-latest",
                string.Empty,
                "steps:",
                "  - task: UseDotNet@2",
                "    inputs:",
                "      useGlobalJson: true",
                string.Empty,
                "  # The binary the last step runs. Pinned, because an unpinned governance tool can change",
                "  # what this repository is judged by without anybody choosing it. First, so a missing gate",
                "  # fails in seconds rather than after a full build and test run.",
                "  #",
                "  # NOTE: MofonCode.Sdlc.Cli is not on a public feed yet, so this step does not resolve",
                "  # until it is, or until a feed holding it is configured in nuget.config.",
                $"  - script: dotnet tool install --global MofonCode.Sdlc.Cli --version {Release.Version}",
                "    displayName: install the gate",
                string.Empty,
                "  - script: dotnet build --configuration Release",
                "    displayName: build",
                string.Empty,
                "  - script: dotnet test --configuration Release --coverage --coverage-output-format cobertura",
                "    displayName: test",
                string.Empty,
                "  # Azure DevOps has a native coverage tab, so publishing is all that is needed.",
                "  - task: PublishCodeCoverageResults@2",
                "    condition: always()",
                "    inputs:",
                "      summaryFileLocation: '**/*.cobertura.xml'",
                string.Empty,
                "  # The guarantee. Every check lives in the binary, so this stays one line.",
                "  - script: sdlc verify-chain \"$(System.PullRequest.SourceBranch)\"",
                "    displayName: verify the artifact chain",
                string.Empty,
            ]),

        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };

    /// <summary>The commit message for what adoption created.</summary>
    /// <param name="mode">Which mode ran.</param>
    /// <param name="paths">What it wrote.</param>
    /// <returns>The message.</returns>
    public static string CommitMessage(Initialise.InitMode mode, IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        string headline = mode switch
        {
            Initialise.InitMode.Greenfield => "Adopt ai-sdlc into a new repository",
            Initialise.InitMode.Onboard => "Adopt ai-sdlc into this repository",
            Initialise.InitMode.Update => "Update the ai-sdlc scaffolding",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        // Committed by init rather than left for the author, because an untracked AGENTS.md is untracked
        // when the first cycle takes its diff -- so the diff records the whole file as new, and a plan
        // saying it *changes* that file can never match. The first cycle would fail CON_07.2 for something
        // adoption did.
        return string.Join(
            Environment.NewLine,
            [
                headline,
                string.Empty,
                "Written by 'sdlc init'. Committed rather than left in the working tree: an untracked",
                "file is untracked when the first cycle takes its diff, so the diff would record the",
                "whole file as new and a plan saying it changes that file could never match.",
                string.Empty,
                .. paths.Select(path => "- " + path.Replace('\\', '/')),
                string.Empty,
            ]);
    }

    /// <summary>
    /// The code-style rules a scaffolded solution starts from.
    /// </summary>
    /// <remarks>
    /// <para>Deliberately shorter than this repository's own. A template a consumer has to read should
    /// carry the rules that bind and not every preference that merely nudges, because a two-hundred-line
    /// file nobody reads is a file nobody can be held to.</para>
    /// <para><strong>The naming section decides nothing at build time, and says so in the file.</strong>
    /// Naming surfaces as <c>IDE1006</c>, which <c>EnforceCodeStyleInBuild</c> does not cover. The
    /// architecture test project scaffolded beside it is what makes those rules real, so the comment
    /// points at it rather than letting a reader assume the build is checking.</para>
    /// </remarks>
    /// <returns>The file's contents.</returns>
    public static string EditorConfig() => string.Join(
        Environment.NewLine,
        [
            "# Written by 'sdlc' with the default .NET skeleton. Yours to amend: a cycle that",
            "# changes it is a cycle like any other, and the diff is the record.",
            "#",
            "# WHAT ENFORCES WHAT, because this is the thing most easily assumed wrong:",
            "#   formatting and style  -> the build, via EnforceCodeStyleInBuild + warnings as errors",
            "#   naming                -> NOTHING here. Naming is IDE1006, which EnforceCodeStyleInBuild",
            "#                            does not cover, so these rules guide an editor and decide no",
            "#                            build. The architecture test project is what enforces them.",
            string.Empty,
            "root = true",
            string.Empty,
            "[*]",
            "indent_style = space",
            "indent_size = 4",
            "charset = utf-8",
            "trim_trailing_whitespace = true",
            "insert_final_newline = true",
            string.Empty,
            "[*.{cs,csx}]",
            string.Empty,
            "# One namespace per file, declared without a block.",
            "csharp_style_namespace_declarations = file_scoped:warning",
            string.Empty,
            "# Explicit types, except where the right-hand side already names the type.",
            "csharp_style_var_for_built_in_types = false:warning",
            "csharp_style_var_when_type_is_apparent = true:warning",
            "csharp_style_var_elsewhere = false:warning",
            string.Empty,
            "# Collection expressions and target-typed new.",
            "dotnet_style_prefer_collection_expression = true:warning",
            "dotnet_style_prefer_simplified_object_creation = true:warning",
            string.Empty,
            "# Formatting is a build concern. IDE0055 defaults to silent, so without this line",
            "# EnforceCodeStyleInBuild enforces no formatting at all - a trailing space then survives",
            "# every green build and every CI gate.",
            "dotnet_diagnostic.IDE0055.severity = error",
            string.Empty,
            "# Sealing by default. CA1852 reaches internal types only; the architecture test covers the",
            "# public ones, because no analyzer does.",
            "dotnet_diagnostic.CA1852.severity = error",
            string.Empty,
            "# NAMING. Guides an editor; enforced by the architecture test project, not by the build.",
            "# The shape of it is that accessibility is visible in the name: an underscore means state on",
            "# this instance, camelCase means private to the type, PascalCase means reachable outside it.",
            string.Empty,
            "# Private fields, instance and static: _camelCase",
            "dotnet_naming_rule.private_fields_are_underscore_camel.severity = warning",
            "dotnet_naming_rule.private_fields_are_underscore_camel.symbols = private_fields",
            "dotnet_naming_rule.private_fields_are_underscore_camel.style = underscore_camel",
            "dotnet_naming_symbols.private_fields.applicable_kinds = field",
            "dotnet_naming_symbols.private_fields.applicable_accessibilities = private, private_protected",
            "dotnet_naming_style.underscore_camel.required_prefix = _",
            "dotnet_naming_style.underscore_camel.capitalization = camel_case",
            string.Empty,
            "# Constants: PascalCase, no prefix. A constant is a value rather than state.",
            "dotnet_naming_rule.constants_are_pascal.severity = warning",
            "dotnet_naming_rule.constants_are_pascal.symbols = constants",
            "dotnet_naming_rule.constants_are_pascal.style = pascal",
            "dotnet_naming_symbols.constants.applicable_kinds = field",
            "dotnet_naming_symbols.constants.required_modifiers = const",
            "dotnet_naming_style.pascal.capitalization = pascal_case",
            string.Empty,
            "# Private methods: camelCase",
            "dotnet_naming_rule.private_methods_are_camel.severity = warning",
            "dotnet_naming_rule.private_methods_are_camel.symbols = private_methods",
            "dotnet_naming_rule.private_methods_are_camel.style = camel",
            "dotnet_naming_symbols.private_methods.applicable_kinds = method",
            "dotnet_naming_symbols.private_methods.applicable_accessibilities = private, private_protected",
            "dotnet_naming_style.camel.capitalization = camel_case",
            string.Empty,
            "# A private NESTED TYPE is not covered, and stays PascalCase. camelCase is not available to",
            "# it: CS8981 reserves an all-lower-case type name for the language, and it fires on one word",
            "# and not on two - so 'handler' is refused where 'ledgerShape' is accepted, and a rule that",
            "# covers some type names and not others is not a rule.",
            string.Empty,
            "# Everything reachable from outside its type: PascalCase",
            "dotnet_naming_rule.public_members_are_pascal.severity = warning",
            "dotnet_naming_rule.public_members_are_pascal.symbols = public_members",
            "dotnet_naming_rule.public_members_are_pascal.style = pascal",
            "dotnet_naming_symbols.public_members.applicable_kinds = method, property, event, class, struct, enum, delegate, namespace",
            "dotnet_naming_symbols.public_members.applicable_accessibilities = public, internal, protected, protected_internal",
            string.Empty,
            "# Interfaces: IPascalCase. Type parameters: TPascalCase.",
            "dotnet_naming_rule.interfaces_are_prefixed.severity = warning",
            "dotnet_naming_rule.interfaces_are_prefixed.symbols = interfaces",
            "dotnet_naming_rule.interfaces_are_prefixed.style = interface_style",
            "dotnet_naming_symbols.interfaces.applicable_kinds = interface",
            "dotnet_naming_style.interface_style.required_prefix = I",
            "dotnet_naming_style.interface_style.capitalization = pascal_case",
            string.Empty,
            "dotnet_naming_rule.type_parameters_are_prefixed.severity = warning",
            "dotnet_naming_rule.type_parameters_are_prefixed.symbols = type_parameters",
            "dotnet_naming_rule.type_parameters_are_prefixed.style = type_parameter_style",
            "dotnet_naming_symbols.type_parameters.applicable_kinds = type_parameter",
            "dotnet_naming_style.type_parameter_style.required_prefix = T",
            "dotnet_naming_style.type_parameter_style.capitalization = pascal_case",
            string.Empty,
            "# Locals and parameters: camelCase",
            "dotnet_naming_rule.locals_are_camel.severity = suggestion",
            "dotnet_naming_rule.locals_are_camel.symbols = locals",
            "dotnet_naming_rule.locals_are_camel.style = camel",
            "dotnet_naming_symbols.locals.applicable_kinds = parameter, local",
            string.Empty,
            "[*.{xml,json,yaml,yml,csproj,props,targets,slnx}]",
            "indent_size = 2",
            string.Empty,
        ]);

    /// <summary>
    /// The build settings a scaffolded solution starts from.
    /// </summary>
    /// <remarks>
    /// <c>latest-recommended</c> rather than the <c>latest-all</c> this repository runs on itself,
    /// because that is the level <c>MofonCode.Core</c> validated for application work.
    /// <c>WarningsNotAsErrors</c> is empty on purpose, so an exception is one visible line in this file
    /// rather than a pragma in source -- and a pragma in source is what the architecture test refuses.
    /// </remarks>
    /// <returns>The file's contents.</returns>
    public static string BuildProps() => string.Join(
        Environment.NewLine,
        [
            "<Project>",
            string.Empty,
            "  <!-- Written by 'sdlc' with the default .NET skeleton. Yours to amend. -->",
            string.Empty,
            "  <PropertyGroup>",
            "    <TargetFramework>net10.0</TargetFramework>",
            "    <LangVersion>latest</LangVersion>",
            "    <Nullable>enable</Nullable>",
            "    <ImplicitUsings>enable</ImplicitUsings>",
            string.Empty,
            "    <!-- A build carries no warnings. WarningsNotAsErrors is deliberately empty, so an",
            "         exception is one visible line here with its reason rather than a pragma buried in",
            "         source - and a pragma in source is what the architecture tests refuse outright. -->",
            "    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
            "    <WarningsNotAsErrors></WarningsNotAsErrors>",
            "    <AnalysisLevel>latest-recommended</AnalysisLevel>",
            "    <AnalysisModeSecurity>All</AnalysisModeSecurity>",
            "    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>",
            string.Empty,
            "    <!-- A vulnerable dependency fails the build. 'moderate' rather than the SDK's 'low':",
            "         low-severity advisories on transitive packages are noise a team learns to ignore,",
            "         and a gate people ignore is worse than no gate. -->",
            "    <NuGetAudit>true</NuGetAudit>",
            "    <NuGetAuditMode>all</NuGetAuditMode>",
            "    <NuGetAuditLevel>moderate</NuGetAuditLevel>",
            string.Empty,
            "    <!-- MSTest on Microsoft.Testing.Platform needs this AND the 'test' runner section in",
            "         global.json. Without both, 'dotnet test' falls back to VSTest, the platform refuses",
            "         that path, and the command exits zero having run nothing - which reads as a clean",
            "         run. It lives here rather than in a test project so a project added later cannot",
            "         quietly run a different runner. Do not set TestingPlatformDotnetTestSupport: that is",
            "         the .NET 9 mechanism, and setting it instead is what produced exactly that silent",
            "         zero-test pass in a trial cycle. -->",
            "    <EnableMSTestRunner>true</EnableMSTestRunner>",
            "  </PropertyGroup>",
            string.Empty,
            "  <!-- A list of APIs this solution does not use, with the reason beside each. It exists",
            "       because the alternative to a mechanism is a convention: a rule nobody can enforce is",
            "       one somebody reaches for at the moment it is inconvenient. RS0030 is a warning, and",
            "       warnings are errors here, so the build refuses the call rather than a reviewer",
            "       catching it. Wired once here rather than per project, so a project added later is",
            "       covered without anybody remembering. -->",
            "  <ItemGroup>",
            "    <PackageReference Include=\"Microsoft.CodeAnalysis.BannedApiAnalyzers\" PrivateAssets=\"all\" />",
            "    <!-- Conditional, because the ban list arrives with the skeleton, not with adoption.",
            "       The condition is load-bearing and is not laziness. Adoption writes this file and the",
            "       skeleton writes the ban list, so between 'sdlc init' and 'sdlc scaffold' there is a",
            "       window in which an unconditional item points at a file that is not there, and any",
            "       project added in that window fails to compile with CS2001 rather than merely going",
            "       unconstrained. D-35 puts the ban list with the surface it governs and D-36 says a ban",
            "       ships with that surface, so the file arrives later by design.",
            "",
            "       What the condition does NOT cost: enforcement going quietly missing if somebody",
            "       deletes the ban list. The skeleton's own test reads BannedSymbols.txt directly, so a",
            "       deleted one is a red build rather than a silent gap. -->",
            "    <AdditionalFiles Include=\"$(MSBuildThisFileDirectory)BannedSymbols.txt\"",
            "                     Condition=\"Exists('$(MSBuildThisFileDirectory)BannedSymbols.txt')\" />",
            "  </ItemGroup>",
            string.Empty,
            "</Project>",
            string.Empty,
        ]);

    /// <summary>
    /// Central package management, with the versions this release of the gate builds against.
    /// </summary>
    /// <remarks>
    /// The concept asks for versions resolved at adoption rather than guessed in a document. Resolving
    /// needs a feed and a network, which adoption deliberately does not require (D-33), so these are
    /// the versions the gate itself builds against at this release: a known-good set rather than a
    /// guess, and a failed restore is the check. A cycle that moves one records it in the diff.
    /// </remarks>
    /// <returns>The file's contents.</returns>
    public static string PackageProps() => string.Join(
        Environment.NewLine,
        [
            "<Project>",
            string.Empty,
            "  <!-- Written by 'sdlc' with the default .NET skeleton. A new package reference is a",
            "       deliberate act: it is added here with its version, and the diff records it. -->",
            string.Empty,
            "  <PropertyGroup>",
            "    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>",
            "    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>",
            "  </PropertyGroup>",
            string.Empty,
            "  <ItemGroup>",
            "    <!-- The versions this release of the gate builds against, rather than resolved from a",
            "         feed: adoption needs no network, so a known-good set beats a guess and a failed",
            "         restore is the check. -->",
            string.Empty,
            "    <!-- The kernel the operation shape is written against: a mediator, Result handlers,",
            "         tracing, and the one shape allowed across an error boundary. MIT, on nuget.org.",
            "         Pinned rather than floated because it is pre-1.0 and every consumer inherits its",
            "         breaking changes. -->",
            "    <PackageVersion Include=\"MofonCode.Core\" Version=\"" + KernelVersion + "\" />",
            string.Empty,
            string.Empty,
            "    <!-- Validation, run as a pipeline behaviour. The kernel takes no dependency on it, so",
            "         the choice is this solution's and the version is pinned here like any other. -->",
            "    <PackageVersion Include=\"FluentValidation\" Version=\"12.1.1\" />",
            string.Empty,
            "    <!-- Reads BannedSymbols.txt and fails the build on a call this solution has ruled out.",
            "         PrivateAssets=all in Directory.Build.props, because an analyzer is a build-time",
            "         tool and never a dependency of what is shipped. -->",
            "    <PackageVersion Include=\"Microsoft.CodeAnalysis.BannedApiAnalyzers\" Version=\"4.14.0\" />",
            string.Empty,
            string.Empty,
            "    <!-- The api's own description, generated from the routes so it cannot disagree with",
            "         them, and the page that renders it. Two packages rather than one on purpose: the",
            "         generator is the declared default and the viewer is swappable, so changing the",
            "         page never changes the document a client generator reads. -->",
            "    <PackageVersion Include=\"Microsoft.AspNetCore.OpenApi\" Version=\"10.0.12\" />",
            "    <PackageVersion Include=\"Swashbuckle.AspNetCore.SwaggerUI\" Version=\"10.2.3\" />",
            string.Empty,
            "    <!-- Observability. Registration and three instrumentations, plus the exporter, which",
            "         only activates where something says where to send it, so a solution with no",
            "         collector carries the registration and exports nothing. -->",
            "    <PackageVersion Include=\"OpenTelemetry.Extensions.Hosting\" Version=\"1.15.3\" />",
            "    <PackageVersion Include=\"OpenTelemetry.Instrumentation.AspNetCore\" Version=\"1.15.2\" />",
            "    <PackageVersion Include=\"OpenTelemetry.Instrumentation.Http\" Version=\"1.15.1\" />",
            "    <PackageVersion Include=\"OpenTelemetry.Instrumentation.Runtime\" Version=\"1.15.1\" />",
            "    <PackageVersion Include=\"OpenTelemetry.Exporter.OpenTelemetryProtocol\" Version=\"1.15.3\" />",
            string.Empty,
            "    <PackageVersion Include=\"MSTest\" Version=\"4.4.1\" />",
            "    <PackageVersion Include=\"Microsoft.Extensions.DependencyInjection\" Version=\"10.0.12\" />",
            "  </ItemGroup>",
            string.Empty,
            "</Project>",
            string.Empty,
        ]);

    /// <summary>
    /// The handful of analyzer rules a test project needs relaxed.
    /// </summary>
    /// <remarks>
    /// <para><strong>Found by running it.</strong> With the analyzer policy and no relaxations, a
    /// scaffolded solution cannot write its first test: <c>CA1707</c> refuses
    /// <c>Rolls_within_one_and_six</c> and <c>CA1305</c> refuses a bare <c>ToString()</c> inside an
    /// assertion. A profile that shipped the policy without this would make every consumer rediscover
    /// the same five exceptions.</para>
    /// <para>Each one argues against something a suite does on purpose, and each says so, because the
    /// policy's own rule is that an exception is a visible line with its reason. The import matters as
    /// much: a <c>Directory.Build.props</c> in a subdirectory shadows the one above it, so without it a
    /// test project silently loses the target framework, the analyzer level and warnings-as-errors all
    /// at once.</para>
    /// </remarks>
    /// <returns>The file's contents.</returns>
    public static string TestBuildProps() => string.Join(
        Environment.NewLine,
        [
            "<Project>",
            string.Empty,
            "  <!-- Written by 'sdlc' with the default .NET skeleton. Yours to amend. -->",
            string.Empty,
            "  <!-- Inherit rather than replace: a Directory.Build.props in a subdirectory shadows the",
            "       one above it, so without this import a test project loses the target framework, the",
            "       analyzer level and warnings-as-errors all at once. -->",
            "  <Import Project=\"$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))\" />",
            string.Empty,
            "  <PropertyGroup>",
            "    <IsPackable>false</IsPackable>",
            string.Empty,
            "    <!-- Warnings stay errors here. These six are relaxed for test code only, each because",
            "         it argues against something a suite does on purpose.",
            string.Empty,
            "         CA1707 - underscores in member names. A test name is a sentence stating what must",
            "                  be true, and the failure output is that sentence.",
            "         CA1515 - types could be internal. MSTest discovers public test classes.",
            "         CA1307 - StringComparison on a comparison. Correct in production string handling",
            "                  and noise inside an assertion, where a failing test is read rather than",
            "                  relied on by a caller.",
            "         CA1305 - IFormatProvider on a ToString. Same reason: an assertion message is read",
            "                  by whoever broke the test, not formatted for a user.",
            "         CA2007 - ConfigureAwait on an await. There is no synchronization context to",
            "                  capture in a test host, so the call conveys nothing.",
            "         CA1861 - a constant array argument hoisted to a static readonly field. The rule",
            "                  targets a hot path called repeatedly; a test method runs once per case,",
            "                  and the array is the expectation, which belongs beside the assertion",
            "                  that reads it rather than at the top of the file away from it. -->",
            "    <NoWarn>$(NoWarn);CA1707;CA1515;CA1307;CA1305;CA2007;CA1861</NoWarn>",
            "  </PropertyGroup>",
            string.Empty,
            "</Project>",
            string.Empty,
        ]);

    /// <summary>
    /// The pinned SDK and the test runner.
    /// </summary>
    /// <remarks>
    /// The <c>test</c> section is half of a pair: without it and <c>EnableMSTestRunner</c> in
    /// <c>Directory.Build.props</c>, <c>dotnet test</c> exits zero having run nothing, which reads as a
    /// clean run. A trial cycle lost its whole verification to exactly that.
    /// </remarks>
    /// <returns>The file's contents.</returns>
    public static string GlobalJson() => string.Join(
        Environment.NewLine,
        [
            "{",
            "  \"sdk\": {",
            "    \"version\": \"10.0.100\",",
            "    \"rollForward\": \"latestFeature\"",
            "  },",
            "  \"test\": {",
            "    \"runner\": \"Microsoft.Testing.Platform\"",
            "  }",
            "}",
            string.Empty,
        ]);
}
