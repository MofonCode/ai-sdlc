# Installing the plugin and the binary

Installing, updating and removing both halves, on each host.

**Two things have to be present, and they fail differently.** The plugin carries the skills and both
hooks; the `sdlc` binary is the gate those skills and hooks call. Without the plugin, nothing
asks for a cycle. Without the binary, the skills ask for something that is not there, which is the
louder of the two failures.

| | What it is | Missing means |
|---|---|---|
| the plugin | eighteen skills, and on Claude Code two hooks, `PreToolUse` and `Stop` | no stages, and on Claude Code no deterministic layer |
| the `sdlc` binary | a .NET tool named `sdlc` | every skill's first command fails |

**One package, two plugin systems.** The root `plugin.json` makes it an
[Agent Plugins 1.0](https://agent-plugins.org/specification) plugin, which is what Copilot, VS Code,
Cursor, Codex and Kiro read; `.claude-plugin/plugin.json` is what Claude Code reads. Both find the
eighteen skills in the same `skills/` directory, so there is no second copy to go stale. Hooks are
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

**There is no published feed yet**, so the binary is built from a clone of this repository and installed
from the package that build produces. One command per block.

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

It should print the seventeen global rules and end with `global  embedded in sdlc <version>`. **Use this
rather than `sdlc help` as the smoke test.** `help` and `version` are the only two commands that need
nothing from the installation, so they are the two that cannot tell you whether it worked.

The library is embedded in the binary, so there is nothing to install beside it. `SDLC_CONSTRAINTS` is
the escape hatch: point it at a `CONSTRAINTS.md` and that file binds instead, which is what you want when
running a build of the gate against a library you are editing. Pointed at a file that is not there, every
command refuses and says so rather than falling back — a person who named a rule set meant it.

**The package and the plugin carry the same number, and the artifact format does not.**
`VersionPrefix` in `Directory.Build.props` is the one place the release version is set, and the suite
asserts it matches `plugin.json` — before it existed the package took the SDK default of 1.0.0 while the
plugin said 0.9.0, so neither number told anybody what they had. The third version, stamped into every
artifact's `framework_version`, tracks the file envelope rather than the release and is meant to stay put
across releases that change no format. `sdlc version` prints all three and says which is which. If a
reinstall has to pin one, `dotnet tool install --version` takes it.

**Verified:** the pack step, and the install-and-run from outside the tree — `.github/workflows/ci.yml`
packs the tool, installs it to a temporary tool path and runs `sdlc constraints` from a directory that is
not this repository, on every pull request. That step exists because everything else runs the binary from
its build output, which sits inside this tree and so always finds a `CONSTRAINTS.md` by walking up to the
root; the packed artifact went untested for a release, and shipped no library at all. **Not verified:**
`--global` specifically, on a clean machine, which differs from the CI path only in where the tool lands. If `dotnet tool install` refuses because the tool is already
present, the **Update** section below is the pair to run.

## The plugin

Two sources, and which one you want depends on whether you are *using* the framework or *changing* it.

| Source | For | Picks up |
|---|---|---|
| GitHub | using it — what a teammate runs | what has been pushed |
| a local directory | changing the framework itself | your working tree, uncommitted edits included |

### From GitHub

Works from anywhere; it clones the repository itself, so you need no copy. **The repository is private**,
so authenticate first.

```pwsh
gh auth login
```

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

**Untried — nobody has run these and watched the skills load.** They are the documented commands and the
package conforms to the standard they read, which is not the same claim. If you are the first, say what
happened.

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

**Untried.** The adapter is conformance-tested against Google's own hooks reference — the nested
`toolCall` payload, the decision-on-every-call contract, and the `sdlc` section adoption writes into
`.agents/hooks.json` — but no Antigravity session has had a tool call refused by it.

The skills reach it through its own plugin mechanism rather than Agent Plugins: `agy plugin install`
with a git URL or a local path, and it also scans `.agents/skills/` in a workspace and
`~/.gemini/config/skills/` globally. A plugin there is a `plugin.json` plus an optional `skills/`
directory, which is the layout this repository already has — but that is **not** the same manifest as
the Agent Plugins one, and nobody has run `agy plugin validate` against it.

**The workspace configuration needs a trusted folder.** Antigravity loads `.agents/hooks.json` only in
a folder you have marked trusted, so on a fresh clone the local layer is off until you do. That is a
property of the host rather than of this framework, and it is one more reason the promise rests on the
pull-request check.

### On other hosts

**Nothing is configured for a host that is not in the table**, and `sdlc hook --host <name>` refuses a
name it does not know rather than guessing. Codex and Cursor both have a blocking pre-tool hook, so they
are adapters waiting to be written rather than hosts that cannot work.

Gemini CLI had a row for two releases and no longer does: Google retired the product on 18 June 2026 in
favour of Antigravity CLI, so the configuration described a hook for a program that no longer answers.
`CONCEPT.md` D-50 has what that cost and what Antigravity would take.

## Check what you have

**In an adopted repository, one command does most of this walk:**

```pwsh
sdlc doctor
```

It checks the rules, git, adoption, whether `.gitattributes` normalises line endings, every host's hook
configuration and whether a pipeline here runs `sdlc verify-chain`, and it exits 1 if anything is wrong. Each problem names the command that fixes it.
In a repository that has not opened its first cycle it ends with what is still yours to place and the
one command that is next.

What it deliberately does **not** claim is the half a binary cannot see. Two things come back marked
`elsewhere` with who to ask, rather than counted as a pass:

- **whether a host loaded the skills** — no binary can ask a host what it loaded, and this is the silent
  failure of the two, because an agent with no skills behaves exactly like an agent nobody asked.
- **whether branch policy requires the pipeline's check** — the policy lives on the platform, not in the
  repository. On Azure DevOps it is also what runs the pipeline, so without it nothing is checked at
  all. Not reported for a `local` solution, which has no pipeline.

So the rest of this section is still worth doing on a new machine.

```pwsh
sdlc version
```

```pwsh
claude plugin details sdlc@ai-sdlc
```

**The two should report the same version.** They are fetched separately, so they can drift — and a skill
calling a verb an older binary does not have is what that drift eventually produces. Expect
`Skills (18)` and `Hooks (2) PreToolUse, Stop`. **If the hook count is below two, treat it as a
failed install rather than a detail** — the deterministic layer is not loaded, nothing is denied, and
every cycle will still appear to work.

The two hooks govern two different moments. `PreToolUse` refuses a write to a file only a person writes.
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

**The generated pipeline installs the gate, and that install does not resolve yet.** `init` writes a
pull-request workflow that runs `dotnet tool install --global MofonCode.Sdlc.Cli --version <release>`
before the build, pinned so an unpinned governance tool cannot change what a repository is judged by
between two runs. **The package is not on a public feed**, so until it is — or until a feed holding it
is named in the solution's `nuget.config` — that step fails, and the pull request stops there. It fails
loudly, early, and in the step whose comment says why, which is the whole reason the step exists rather
than being left out (D-37).

**Line endings differ between sources without meaning anything.** A directory install copies your working
tree; a GitHub install checks out through `.gitattributes`. Comparing the two byte for byte shows every
line of a shell script as changed when nothing has.
