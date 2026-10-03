# ai-sdlc

**Version 0.9.0** · iteration 4 · .NET 10 · MIT

A host-agnostic plugin that governs a team's software lifecycle through gated artifacts.

A change does not travel from a request to a diff. It moves through phases, each producing a file the
next phase reads, and each file is challenged against a fixed set of rules before it advances. **Nothing
advances on assertion.**

Two things it exists to make possible:

- **A person approves a plan before code exists**, and the approval is recorded against the exact bytes
  that were approved.
- **Approved plans build unattended**, one or many at a time.

## The cycle

```
intent -> spec -> plan -> build -> verify -> land
                            ^         |
                            +- remediate (at most 3) -+
```

Intent, spec and plan are all required, always. Each is written, challenged in a **fresh process** against
the rules in force, and revised until a round raises nothing blocking. A stage will not run until the
artifact before it has such a round.

One gate: a person approves all three planning documents together, immediately before the build, and the
approval records a hash of each. If any of the three changes afterwards, the gate refuses until it is
approved again.

## What is actually enforced

Three layers, and only the last is a guarantee.

| Layer | Mechanism | Who can get past it |
|---|---|---|
| advisory | skill and instruction text | the agent, constantly |
| local | the `sdlc` binary, a pre-tool hook, a turn-end hook, git hooks | anyone with a shell |
| **non-bypassable** | `sdlc verify-chain` as a pull-request check | nobody without repo admin |

The promise made to a team is the third layer: **nothing merges unless the artifact chain is complete,
approved and hash-matched.** The local layer exists to make that cheap to satisfy, not to be the proof.

A rule of its own: **a directory that appears enforced and is not is worse than one that never pretended.**
Iteration 3 shipped a nightly job that asserted interrogation verdicts, needed an API key nobody had set,
and warned and exited cleanly for weeks. That is the failure mode this design is written against.

## Four files only a person writes

`approval.md`, `abandoned.md`, `constraints.md`, `solution.md`.

No tool writes them. A tool prints what belongs in them — `sdlc draft approval`, `draft abandon`,
`draft constraint`, `draft profile` — and a person pastes it. The pre-tool hook denies every agent write
to all four, by file path and by shell command alike, with no exception. There is no approve command for
an exception to be carved out of, which is why the rule states in one sentence and has nowhere for a
bypass to live.

**A blocking finding cannot be waived by any mechanism.** Not at the gate, not with a flag, not with a
recorded override. It is answered, or the cycle does not advance.

## Host-agnostic, not stack-agnostic

Every candidate host can deny a tool call before it runs, so the enforcement primitive is portable. What
differs is vocabulary, and one adapter normalises it: `sdlc hook --host <name>` reads the host's payload,
asks the one policy engine, and writes the host's own decision shape.

**v1 ships Claude Code, GitHub Copilot and Gemini CLI.** The rules themselves name no language, framework
or package, so they travel to any stack — but the tool is a .NET executable, so the toolchain does not.

**Both moments are governed on all three.** A second moment is worth governing — an agent must not report
done over a failing build, which is what makes an analyzer or an architecture test an enforcement layer
rather than one a reviewer consults afterwards. The names are the only difference:

| | Tool call | Turn end | Config | Refusal |
|---|---|---|---|---|
| Claude Code | `PreToolUse` | `Stop` | `.claude/settings.json`, merged | `permissionDecision` nested |
| GitHub Copilot | `preToolUse` | `agentStop` | `.github/hooks/sdlc.json`, owned | `permissionDecision` at top level |
| Gemini CLI | `BeforeTool` | `AfterAgent` | `.gemini/settings.json`, merged | `decision: "deny"` |

**What a host supports is a row in a table, not a judgement.** `SupportedHosts` holds one row per host —
its spellings, where its hook configuration goes, who owns that file, what it calls each event, whether it
has a turn end at all — and the suite asserts every row is complete and that each host denies a write to a
protected file when fed a payload in its own shape. That exists because the previous arrangement spread
those facts over four files and got two of them wrong without anything noticing: the turn end above was
claimed to be Claude-only for a release, and the Copilot pre-tool hook read one of that host's two
documented payload shapes and silently allowed everything on the other. A host with genuinely no turn-end
event is still refused by name rather than given a check that cannot run. `CONCEPT.md` D-46 has the
reasoning.

Gemini CLI was the first host added through that table, and it earned its keep immediately: Gemini spells
the timeout key `timeout` exactly as Claude Code does and reads it as **milliseconds**, so one shared
number would have given it a ten-millisecond budget — a hook killed before it can answer, on every tool
call, with the configuration looking correct. The unit is a field on the row, and the suite asserts the
number each host is written.

## Install

Two halves have to be present, and they fail differently. The **plugin** carries the seventeen skills and
the two hooks; the **`sdlc` binary** is the gate those skills call. Without the plugin nothing asks for
a cycle. Without the binary every skill's first command fails.

**There is no published feed yet**, so the binary is built from a clone of this repository.

```pwsh
dotnet pack src\Sdlc.Cli\Sdlc.Cli.csproj -c Release -o src\Sdlc.Cli\bin\pack
dotnet tool install --global --add-source src\Sdlc.Cli\bin\pack MofonCode.Sdlc.Cli
```

The plugin comes from GitHub. **The repository is private**, so authenticate first.

```pwsh
gh auth login
claude plugin marketplace add MofonCode/ai-sdlc
claude plugin install sdlc@ai-sdlc
```

Then ask both halves what they are:

```pwsh
sdlc version
claude plugin details sdlc@ai-sdlc
```

Expect the same version from each, with `Skills (17)` and `Hooks (2) PreToolUse, Stop`. **A hook count
below two is a failed install rather than a detail** — the deterministic layer is not loaded, nothing is
denied, and every cycle will still appear to work. Start a new session afterwards, because skills load at
startup.

`docs/guides/install.md` has the rest: the local-directory source for changing the framework itself,
updating, removing, and the four things that catch people.

## Getting started

Installing puts the framework on your machine. It governs nothing until a repository adopts it.

```
sdlc init              # adopt the framework into this repository. A person runs this
sdlc constraints       # the rules now in force
sdlc new               # open a cycle
```

Then ask your agent for the stages: `/sdlc:intent`, `/sdlc:spec`, `/sdlc:plan`. `sdlc help` lists what the
binary does on its own, and `.sdlc/README.md` — written into the solution by `init` — is the whole flow in
one page.

## Documentation

| | |
|---|---|
| [`docs/guides/install.md`](docs/guides/install.md) | installing the plugin and the binary, updating, and removing |
| [`docs/guides/first-cycle.md`](docs/guides/first-cycle.md) | a cycle from an empty directory to a pull request, in fourteen steps |
| [`docs/guides/cycle-commands.md`](docs/guides/cycle-commands.md) | every command for one cycle, in order, on one screen |
| [`docs/guides/when-it-blocks.md`](docs/guides/when-it-blocks.md) | the index to every refusal, and the remedy for each |
| [`docs/skills.md`](docs/skills.md) | the seventeen skills: what each is, how it is invoked, when it runs, and who starts it |
| [`src/README.md`](src/README.md) | what `Sdlc.Core` and `Sdlc.Cli` are, how one invocation flows, and where a new thing goes |

Two things to read alongside them: [`reference/iteration-3-cycle/`](reference/iteration-3-cycle/) is a real
cycle run in anger, with its interrogation rounds and its evidence — the best worked example the project
has. And `.sdlc/README.md`, written into every governed solution by `sdlc init`, is the flow, the files and
the commands in one page that travels with the repository.

## Repository layout

| | |
|---|---|
| `CONCEPT.md` | the design, and every decision behind it with what each one rejected |
| `CONSTRAINTS.md` | the fourteen global constraints, each with what it forbids and why |
| `CONCEPT.raw.md` | the original longhand draft, preserved unedited |
| `AGENTS.md` | how to work in this repository: what it is, where things are, what the suite enforces |
| `docs/` | the guides: the skills, installing, a first cycle, the commands, and every refusal |
| `src/Sdlc.Core/` | the gate: constraints, cycles, approvals, findings, hooks, evidence, the chain |
| `src/Sdlc.Cli/` | the one binary every skill on every host calls |
| `skills/` | the seventeen skills, read by both plugin systems from this one directory |
| `plugin.json` | the portable Agent Plugins manifest, which every client but Claude Code reads |
| `hooks/`, `.claude-plugin/` | Claude Code's own plugin: its manifest, its marketplace catalogue, its hooks |
| `tests/` | the suite |
| `tools/` | build-time tools that are not the product — the scaffold harness CI runs |
| `.github/workflows/ci.yml` | this repository's own CI, which deliberately does not govern itself (D-42) |

## Where it stands

Version 0.9.0, iteration 4. The planning half is complete and exercised end to end: adoption, a cycle, the three
documents, interrogation rounds, a pasted approval, recorded evidence, a verification, the chain, and the
landing decision.

What is honest to say about the rest:

- **Bounded remediation is implemented and tested, and has been exercised in fixtures far more than in
  anger.** Everything unattended rests on it, so the first batch runs are worth watching.
- **`land` and `batch-build` are skills with no command behind the push.** The refusal is in the binary and
  tested; the push itself uses the platform's own CLI.
- **The Copilot install has never been run, though nothing is guessed any more.** The package is a
  conformant [Agent Plugins 1.0](https://agent-plugins.org/specification) plugin, so the seventeen skills
  are discoverable by every client implementing that standard — Copilot included — out of the same
  `skills/` directory Claude Code reads. What is asserted: the manifest matches the published schema, and
  every skill satisfies the Agent Skills specification. What is **not** established: nobody has run
  `copilot plugin marketplace add` against this repository and watched the skills load. Treat the Copilot
  path as untried in the same way the Azure DevOps pipeline is.
- **The Gemini adapter has never been run against Gemini CLI.** Its row, both payload shapes and both
  refusal shapes are held by conformance tests built from Google's own hooks reference, and `sdlc init`
  writes `.gemini/settings.json` — but no session on that host has had a tool call refused. One part is a
  known guess: the `BeforeTool` matcher names `write_file|replace|run_shell_command`, so a write tool
  Gemini adds under a fourth name would not reach the policy. The pipeline check still catches it, which
  is the layer that does not depend on any host.
- **Off Claude Code a skill is chosen, not named.** There is no `/sdlc:spec` on Copilot or Gemini — those
  clients match a request against a skill's `description` and ask before activating. So the stages have to
  be asked for in prose, and the two Claude-specific frontmatter fields (`argument-hint`, and
  `allowed-tools` in Claude's own syntax) do nothing there, which costs a permission prompt per command
  rather than correctness.
- **The Azure DevOps pipeline has never run against a real server, by decision.** It is one file and one
  command, the failure is loud, and nothing else depends on it — so it ships with a note saying so in the
  generated file itself, where the first person to adopt on Azure DevOps will read it. `CONCEPT.md` D-32
  has the reasoning and what it rejected.

## Not goals

Enforcing a coverage percentage. Making the local approval unforgeable — it cannot be, and the pull request
is where accountability lives. Governing ai-sdlc with ai-sdlc. Resolving merge conflicts as the primary
strategy. Tooling for splitting work. A device end-to-end layer. `CONCEPT.md` §18 has the full list with
reasons.

## License

MIT. See [LICENSE](LICENSE).
