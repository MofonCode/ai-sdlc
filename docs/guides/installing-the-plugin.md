# Installing the plugin and the binary

Installing, updating and removing both halves, on each host.

**Two things have to be present, and they fail differently.** The plugin carries the skills and the
hooks; the `sdlc` binary is what those skills and hooks call to decide anything. There is no
third thing to install: "the gate" elsewhere is one of the binary's checks, not a component. Without the plugin, nothing
asks for a cycle. Without the binary, the skills ask for something that is not there, which is the
louder of the two failures.

| | What it is | Missing means |
|---|---|---|
| the plugin | twenty-one skills, and on Claude Code three hooks, `SessionStart`, `PreToolUse` and `Stop` | no stages, and on Claude Code no deterministic layer |
| the `sdlc` binary | a .NET tool named `sdlc` | every skill's first command fails |

**One package, two plugin systems.** The root `plugin.json` makes it an
[Agent Plugins 1.0](https://agent-plugins.org/specification) plugin, which is what Copilot, VS Code,
Cursor, Codex and Kiro read; `.claude-plugin/plugin.json` is what Claude Code reads. Both find the
twenty-one skills in the same `skills/` directory, so there is no second copy to go stale. Hooks are
**not** part of that standard — it covers skills and MCP servers only — so on every host the hooks arrive
through `sdlc init` writing them into the repository, and on Claude Code additionally through the plugin.

**All three hosts are governed, and adoption writes every configuration regardless of which one you
use.** That is deliberate: a file is inert on a machine without that host, and a teammate who opens the
repository in a different agent is governed without having to adopt again.

| Host | Hook config adoption writes | Tool call | Turn end |
|---|---|---|---|
| Claude Code | `.claude/settings.json`, merged | `PreToolUse` | `Stop` |
| GitHub Copilot | `.github/hooks/sdlc.json`, owned | `preToolUse` | `agentStop` |
| Antigravity | `.agents/hooks.json`, the `sdlc` section | `PreToolUse` | **not governed** |

**Antigravity's turn end is deliberately left alone.** It has a `Stop` event, but its payload carries no
flag saying the agent is already continuing because a previous stop was refused — so a refusal there
would be an endless turn rather than a governed stop. Nothing is configured, and
`sdlc hook --event turn-end --host antigravity` refuses by name. The pull-request check still gates the
merge, and it depends on no host.

## Before either

| | |
|---|---|
| .NET SDK | 10.0.100 or later. `global.json` pins it with `rollForward: latestFeature` |
| git | the gates key on a branch, and a build records a diff |
| `gh` or `az` | only for `pr`, which uses the platform's own CLI to open the pull request. Not needed at all on a solution adopted with `--platform local`, which has no remote to push to |

## The binary

It is published to nuget.org as `MofonCode.Sdlc.Cli`. Install it once per machine, globally, so `sdlc` is
on the path wherever a skill or a hook calls it:

```pwsh
dotnet tool install --global MofonCode.Sdlc.Cli
```

**Until the first release reaches nuget.org**, or when you are changing the framework itself, build it
from a clone of this repository and install the package that build produces. One command per block. A package
packed on your machine is a local build — its version ends in `-local` — and no cycle it judges opens a pull
request; inside a solution that pins a published release, the pin runs instead. To run a cycle on a change you
are making, [Testing a local build](testing-a-local-build.md) is the better route.

```pwsh
cd C:\Repos\ai-sdlc
```

```pwsh
dotnet pack src\Sdlc.Cli\Sdlc.Cli.csproj -c Release -o src\Sdlc.Cli\bin\pack
```

```pwsh
dotnet tool install --global --add-source src\Sdlc.Cli\bin\pack MofonCode.Sdlc.Cli
```

Then, from anywhere — and **from anywhere matters**, so run this somewhere that is not a clone of this
repository:

```pwsh
sdlc constraints
```

It should print the nineteen global rules and end with `global  embedded in sdlc <version>`. **Use this
rather than `sdlc help` as the smoke test.** `help` and `version` are the only two commands that need
nothing from the installation, so they are the two that cannot tell you whether it worked.

The library is embedded in the binary, so there is nothing to install beside it. `SDLC_CONSTRAINTS` is
the escape hatch: point it at a `CONSTRAINTS.md` and that file binds instead, which is what you want when
running a build of the gate against a library you are editing. Pointed at a file that is not there, every
command refuses and says so rather than falling back — a person who named a rule set meant it.

**The package and the plugin carry the same number.**
`VersionPrefix` in `Directory.Build.props` is the one place the release version is set, and the suite
asserts it matches `plugin.json`, so the package version and the plugin version always tell you the same
thing. `sdlc version` prints both. If a reinstall has to pin one, `dotnet tool install --version`
takes it.

If `dotnet tool install` refuses because the tool is already present, the **Update** section below is the
pair to run.

### Which release runs in a solution

**The global binary is not the release a solution runs.** Adoption pins a release in the solution's
`.config/dotnet-tools.json`, and inside that solution the global `sdlc` hands every command on to the pinned
release: same arguments, same working directory, same input and output. The first time a pinned release is
needed on a machine it is restored from the feed, with what restore prints going to standard error. Nothing
else changes: every skill, hook and guide still says `sdlc` (D-131).

**A cycle keeps the release it opened on.** Its worktree's branch carries the manifest it was cut with, so
upgrading the machine, or bumping the integration branch, does not move a cycle in flight. It moves when a
person merges the integration branch into it, and the status panel says when a cycle's rounds were written by
more than one release.

`sdlc version` inside a solution reports the pinned release, because that is what answers. Outside one it
reports the global.

**The global binary has to be one that hands on.** A global `sdlc` from before the handoff runs every command
itself, whatever the solution pins. `sdlc version` in a solution that pins another release shows which you
have: the pinned number means it handed on.

### Start a session with the pinned release's skills

**In a governed solution, start Claude Code with `sdlc claude` rather than `claude`.** Every release of the
binary carries the plugin it was released with, and `sdlc claude` starts Claude Code with that copy loaded
over whatever plugin is installed. Inside a solution the pinned release does the starting, so a cycle's
skills, hooks and binary are one release, whatever the machine has installed. Every argument after `claude`
is Claude Code's: `sdlc claude --resume`, `sdlc claude -p "..."`.

Plain `claude` still works. It loads the plugin installed below, and when that is another release than the
binary the solution pins, the session is told so as it starts (D-131). The installed plugin is what a session
outside any solution uses, and the fallback when somebody forgets.

## The plugin

Two sources, and which one you want depends on whether you are *using* the framework or *changing* it.

| Source | For | Picks up |
|---|---|---|
| GitHub | using it — what a teammate runs | what has been pushed |
| a local directory | changing the framework itself | your working tree, uncommitted edits included |

### From GitHub

Works from anywhere; it clones the repository itself, so you need no copy.

```pwsh
claude plugin marketplace add MofonCode/ai-sdlc
```

```pwsh
claude plugin install sdlc@ai-sdlc
```

Use the `owner/repo` shorthand rather than the full URL. Both work and they register differently: the
shorthand becomes a **GitHub** source, which goes through GitHub authentication, while
`https://github.com/MofonCode/ai-sdlc` becomes a generic **Git** source — a plain clone falling back to
whatever the git credential manager holds, which on a fresh machine may be nothing at all.

### From a local directory

Use this when you are editing the framework. It reads your working tree, so a change is picked up by a
reinstall without pushing anything.

Run the first from wherever you cloned this repository. The `./` is required — a bare `.` is rejected.

```pwsh
cd C:\Repos\ai-sdlc
```

```pwsh
claude plugin marketplace add ./
```

```pwsh
claude plugin install sdlc@ai-sdlc
```

The two sources use the same marketplace name, so they cannot both be registered. Switching means
removing one first — see **Remove**.

### On Copilot

```pwsh
copilot plugin marketplace add MofonCode/ai-sdlc
```

```pwsh
copilot plugin install sdlc@ai-sdlc
```

`.claude-plugin/marketplace.json` serves both: Copilot looks for `marketplace.json` in `.github/plugin/`
and falls back to `.claude-plugin/`, so the one catalogue already there is the one it reads. In VS Code
the equivalent is `chat.plugins.enabled`, this repository added to `chat.plugins.marketplaces` in
`settings.json`, then `@agentPlugins` in the Extensions view.

**You still need `sdlc init` in each repository**, and more than on Claude Code: the hooks are not part
of the plugin standard, so adoption writing `.github/hooks/sdlc.json` is the only way they arrive.

### On Antigravity

Adoption writes an `sdlc` section into `.agents/hooks.json`, which is how the pre-tool hook reaches it.

The skills reach it through its own plugin mechanism rather than Agent Plugins: `agy plugin install`
with a git URL or a local path, and it also scans `.agents/skills/` in a workspace and
`~/.gemini/config/skills/` globally. A plugin there is a `plugin.json` plus an optional `skills/`
directory, which is the layout this repository already has — but that is **not** the same manifest as
the Agent Plugins one.

**The workspace configuration needs a trusted folder.** Antigravity loads `.agents/hooks.json` only in
a folder you have marked trusted, so on a fresh clone the local layer is off until you do. That is a
property of the host rather than of this framework, and it is one more reason the promise rests on the
pull-request check.

### On other hosts

**Nothing is configured for a host that is not in the table**, and `sdlc hook --host <name>` refuses a
name it does not know rather than guessing. Codex and Cursor both have a blocking pre-tool hook, so they
are adapters waiting to be written rather than hosts that cannot work.

## Check what you have

**In an adopted repository, one command does most of this walk:**

```pwsh
sdlc doctor
```

It checks the rules, git, adoption, whether the framework's files were written by this release of the
binary, whether `.gitattributes` normalises line endings, every host's hook
configuration and whether a pipeline here runs `sdlc verify-chain`, and it exits 1 if anything is wrong. Each problem names the command that fixes it.
In a repository that has not opened its first cycle it ends with what is still yours to place and the
one command that is next.

What it deliberately does **not** claim is the half a binary cannot see. Two things can come back marked
`elsewhere` with who to ask, rather than counted as a pass:

- **whether a host loaded the skills** — no binary can ask a host what it loaded, and this is the silent
  failure of the two, because an agent with no skills behaves exactly like an agent nobody asked.
- **whether branch policy requires the pipeline's check, when it cannot ask** — the policy lives on the
  platform, not in the repository. `doctor` asks through `az` or `gh`, signed in as you, and a policy that
  does not require the check is a problem. Signed out, or without permission to read the policy, it comes
  back `elsewhere` with the step to take. On Azure DevOps the policy is also what runs the pipeline, so
  without it nothing is checked at all. Not reported for a `local` solution, which has no pipeline.

So the rest of this section is still worth doing on a new machine.

```pwsh
sdlc version
```

```pwsh
claude plugin details sdlc@ai-sdlc
```

**The two should report the same version.** They are fetched separately, so they can drift — and a skill
calling a verb an older binary does not have is what that drift eventually produces. Expect
`Skills (21)` and `Hooks (3) SessionStart, PreToolUse, Stop`. **If `PreToolUse` or `Stop` is missing, treat
it as a failed install rather than a detail** — the deterministic layer is not loaded, nothing is denied,
and every cycle will still appear to work.

`SessionStart` checks that drift for you, and `sdlc claude` avoids it. As a session starts, the plugin hands the binary its own directory,
and when the skills are another release than the binary the directory runs, the agent is told so, and told
not to work around a command the binary does not have. It never refuses: a session may be the one about to
fix it. Inside a solution, the release it compares against is the one the solution pins.

The other two hooks govern two different moments. `PreToolUse` refuses a write to a file only a person writes.
`Stop` is asked before a turn ends, and sends the turn back when the cycle's recorded evidence shows a
failing suite or when code has moved since the build was recorded. It is the reason a warnings-as-errors
build and an architecture test are enforcement rather than something a reviewer finds afterwards.

**The tool call is governed on all three hosts and the turn end on two** — the table at the top of this
guide has the names. Off Claude Code they arrive through adoption rather than a plugin install, so there
is nothing to verify with `claude plugin details`: check instead that the host's configuration file
exists and that `sdlc version` answers.

```pwsh
claude plugin marketplace list
```

That says which source you are on, which is the first thing to check when a change you made is not
showing up.

## Update

A directory-marketplace install **caches by version**, and `claude plugin update` does nothing when the
version has not moved. So a refresh is a full reinstall, in this order:

```pwsh
claude plugin uninstall sdlc@ai-sdlc
```

```pwsh
claude plugin install sdlc@ai-sdlc
```

The two are a pair. Running only the first leaves you with no plugin.

On the GitHub source, pull the marketplace first so the clone is current, then do the pair above:

```pwsh
claude plugin marketplace update ai-sdlc
```

**The binary has the same trap, and `dotnet tool update` walks into it.** A tool is cached by version
too, and `VersionPrefix` only moves on a release — so when you are iterating locally, `update` prints
`Tool 'mofoncode.sdlc.cli' is already installed` and leaves the old binary on your PATH. It reads like
success. A stale `sdlc` then adopts repositories with the hook configuration of whatever build it is,
which is the quietest way to spend an hour on a defect that was fixed before you started.

So the binary is a reinstall too, in this order, after repacking:

```pwsh
dotnet pack src\Sdlc.Cli\Sdlc.Cli.csproj -c Release -o src\Sdlc.Cli\bin\pack
```

```pwsh
dotnet tool uninstall --global MofonCode.Sdlc.Cli
```

```pwsh
dotnet tool install --global --add-source src\Sdlc.Cli\bin\pack MofonCode.Sdlc.Cli
```

`sdlc version` reports the number, which does not move between local builds — so to tell whether a
reinstall took, look at the file's timestamp or run a command only the newer build has.

### Then each governed repository

**Reinstalling updates this machine and no repository on it.** Every governed solution still pins the
release it pinned, and has the hook configuration, the pipeline and the `.sdlc/README.md` that release wrote.
In each one, on the integration branch, a person moves the pin and then runs adoption again. Adoption picks
update mode by itself, refreshes only what the framework owns, and touches none of the seven files only a person
writes:

```pwsh
dotnet tool update --local MofonCode.Sdlc.Cli --version <release>
```

```pwsh
sdlc init
```

```pwsh
sdlc doctor
```

Commit what both change together. A cycle already open keeps its own pin until its branch takes that commit.

**An upgrade needs no cycle** (D-133). Do it on a branch of any name and open a pull request into the
integration branch. The pull-request check sees a change carrying only the pin, what adoption and
`sdlc factory` wrote and `techstack.md`, and checks it as an upgrade rather than refusing it for having no
chain: it runs as the release the branch pins and compares each framework file with what that release
writes. The blocks a release adds to `.sdlc/cycles/.gitignore`, which the next `sdlc new` would append,
may ride along too. A pipeline is compared step by step, so the steps a solution added beside the framework's ride
through unchanged; `sdlc factory` drops them, so put them back before committing. The
same pull request may answer the rows the release asks about in `techstack.md` — added rows only, each one
`sdlc stack` lists under *Not mentioned*. Anything else in it, one source file or one changed answer, and
it is refused as a change with no chain; `handling-refusals.md` has the four upgrade refusals.

**`doctor` says when this is due.** `.sdlc/README.md` carries the release that wrote it, and the
`framework files` check compares that with the binary:

| It finds | Outcome, and what to do |
|---|---|
| this release | sound |
| an older release, or no stamp at all — written before stamps were | a problem: a person runs adoption here |
| a newer release than the binary | a problem: this machine's binary is the stale half, so upgrade it rather than re-running adoption, which would roll the files back |

**What adoption cannot update is the declared stack**, because it is a person's file. A release that
added a default or a stack rule is named instead — in the adoption report, by `sdlc stack` and by
`doctor` — and `/sdlc:init` walks each one with you. `declaring-the-stack.md` covers it under When a
release adds a row or a rule.

## Remove

```pwsh
claude plugin uninstall sdlc@ai-sdlc
```

```pwsh
claude plugin marketplace remove ai-sdlc
```

```pwsh
dotnet tool uninstall --global MofonCode.Sdlc.Cli
```

Removing either leaves every `.sdlc/` directory untouched. The artifacts are a repository's own record,
not the plugin's, and they stay readable without it.

## Adopting the framework

Installing puts the framework on your machine. It governs nothing until a repository adopts it, which is
`sdlc init` — a person's command, denied to an agent, and covered in `adopting-the-framework.md`.

## Four things that catch people

**Hooks need workspace trust.** Project hooks do not run until the workspace is trusted. Until then the
gate is silent, and a cycle appears to work while nothing is denied.

**A session loads skills at startup.** Reinstall, then start a new session — an open one keeps what it
already had.

**The generated pipeline restores the release the branch pins.** Adoption writes a pull-request workflow
that runs `dotnet tool restore` before the build and makes the release `.config/dotnet-tools.json` names the
`sdlc` it runs, so a pull request is judged by the release its cycle ran on (D-37, D-131). The version is read
from the manifest, so a bump is one commit and no pipeline file goes stale. A solution adopted before the
manifest existed has none, and the step stops and says to run adoption. A restore that fails names the
usual cause: a `nuget.config` that clears the default sources and so hides nuget.org.

**Line endings differ between sources without meaning anything.** A directory install copies your working
tree; a GitHub install checks out through `.gitattributes`. Comparing the two byte for byte shows every
line of a shell script as changed when nothing has.
