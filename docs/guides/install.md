# Installing, updating and removing

**Two things have to be present, and they fail differently.** The plugin carries the skills and both
hooks; the `sdlc` binary is the gate those skills and hooks call. Without the plugin, nothing
asks for a cycle. Without the binary, the skills ask for something that is not there — and the pre-push
hook says so and lets the push through, which is the louder of the two failures.

| | What it is | Missing means |
|---|---|---|
| the plugin | seventeen skills and two hooks, `PreToolUse` and `Stop` | no stages, and no deterministic layer |
| the `sdlc` binary | a .NET tool named `sdlc` | every skill's first command fails |

## Before either

| | |
|---|---|
| .NET SDK | 10.0.100 or later. `global.json` pins it with `rollForward: latestFeature` |
| git | the gates key on a branch, and a build records a diff |
| `gh` or `az` | only for `land`, which uses the platform's own CLI to open the pull request |

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

Then, from anywhere:

```pwsh
sdlc help
```

**The package and the plugin carry the same number, and the artifact format does not.**
`VersionPrefix` in `Directory.Build.props` is the one place the release version is set, and the suite
asserts it matches `plugin.json` — before it existed the package took the SDK default of 1.0.0 while the
plugin said 0.9.0, so neither number told anybody what they had. The third version, stamped into every
artifact's `framework_version`, tracks the file envelope rather than the release and is meant to stay put
across releases that change no format. `sdlc version` prints all three and says which is which. If a
reinstall has to pin one, `dotnet tool install --version` takes it.

**Verified:** the pack step, on this repository, at this commit. **Not verified:** the global install,
which has not been run on a clean machine. If `dotnet tool install` refuses because the tool is already
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

## Check what you have

```pwsh
sdlc version
```

```pwsh
claude plugin details sdlc@ai-sdlc
```

**The two should report the same version.** They are fetched separately, so they can drift — and a skill
calling a verb an older binary does not have is what that drift eventually produces. Expect
`Skills (17)` and `Hooks (2) PreToolUse, Stop`. **If the hook count is below two, treat it as a
failed install rather than a detail** — the deterministic layer is not loaded, nothing is denied, and
every cycle will still appear to work.

The two hooks govern two different moments. `PreToolUse` refuses a write to a file only a person writes.
`Stop` is asked before a turn ends, and sends the turn back when the cycle's recorded evidence shows a
failing suite or when code has moved since the build was recorded. It is the reason a warnings-as-errors
build and an architecture test are enforcement rather than something a reviewer finds afterwards.

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

The binary updates the same way — repack, then:

```pwsh
dotnet tool update --global --add-source src\Sdlc.Cli\bin\pack MofonCode.Sdlc.Cli
```

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

## Adopting a repository

Installing puts the framework on your machine. It governs nothing until a repository adopts it, which is
`sdlc init` — a person's command, denied to an agent, and covered in `first-cycle.md`.

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
