# ai-sdlc

A personal Claude Code plugin marketplace for AI-assisted software development, implementing
[Anthropic's AI-Native SDLC Playbook](https://claude.com/blog/the-ai-native-sdlc-playbook).

Two layers, deliberately separate:

- **`sdlc-core`** — the engine. Stack-agnostic: it knows about intent, specs, plans, gates and
  verification, and nothing about any language, framework or company.
- **`stack-*` packs** — one opinionated answer each to "what do we build this with". `init`
  discovers whichever are installed and proposes one. Swap the pack, keep the engine.

`stack-dotnet` is installed, so **.NET is the default in practice** — `init` proposes it without
asking. The separation is not about weakening that default. It is about keeping *stack*, which
belongs in a pack and is the same across all of this owner's projects, apart from *domain*,
which belongs in each repo's own `CHARTER.md` and never in the library.

The one idea everything rests on: **every stage commits a version-controlled artifact the next
stage reads.** The artifact chain is the state machine — not the order the skills were invoked.

```
CHARTER.md                            repo scope, long-lived, every intent is checked against it
   │
   ├── intent.md  →  spec.md  →  plan.md  →  code + tests  →  verification.md
        Plan          Design       Build         Build            Test
```

The artifact root defaults to `.sdlc/` and is per-repo: the charter declares its own location
in `artifact_root`, and every skill, agent and the gate hook resolve it rather than assuming.

Each arrow is a human gate. Cross a gate in a **fresh session** — the artifacts are written to
be self-contained precisely so the prior conversation can be dropped.

## Install

```
/plugin marketplace add C:\Repos\ai-sdlc
/plugin install sdlc-core@ai-sdlc
/plugin install stack-dotnet@ai-sdlc
```

Installed once, available in every project. It stays silent in repos that have no `.sdlc/`
directory.

## Plugins

### `sdlc-core`

Plugin skills are namespaced, so the short form works only when nothing else claims it.

| Skill | Stage | Produces |
|---|---|---|
| `/sdlc-core:init` | 0 — once per repo | `<root>/CHARTER.md` |
| `/sdlc-core:intent` | Plan | `<root>/<slug>/intent.md` |
| `/sdlc-core:spec` | Design | `<root>/<slug>/spec.md` |
| `/sdlc-core:plan` | Build | `<root>/<slug>/plan.md` with a file-disjoint slice manifest |
| `/sdlc-core:verify` | Test | `<root>/<slug>/verification.md` — verdict, requirement coverage, real build output |
| `sdlc-router` | — | model-invoked; tiering rules and artifact location |

The chain skills are `disable-model-invocation: true` — they write files and commit, so
they are triggered by you, never reached for on Claude's own initiative.

| Agent | Role |
|---|---|
| `intent-interrogator` | Fresh-context adversary. Attacks a draft intent or spec for unfalsifiable criteria, smuggled solutions, missing non-goals, assumed invariants, charter conflicts. |
| `spec-writer` | Approved intent → draft spec: EARS-form requirements, chosen design, rejected alternatives, flagged concerns with named owners. |
| `verifier` | Fresh-context verification: real build/test output, requirement coverage, silent failures, time-series contamination. |

These double as **agent-team teammate roles** — a subagent definition can be named when
spawning a teammate, so the same file serves both.

## Tiers — the escape hatch

Ceremony that costs more than it saves gets abandoned, and then nothing gets used. Every skill
tiers the request first and skips everything below it. This mirrors Anthropic's own plan-mode
guidance: *"If you could describe the diff in one sentence, skip the plan."*

| Tier | Looks like | Required |
|---|---|---|
| **trivial** | typo, rename, config value, one-liner | nothing |
| **task** | bounded change, behavior already agreed, few files | `plan.md` |
| **feature** | new capability, schema/contract change, hard to reverse | full chain |

## Requirements are written in EARS

[Easy Approach to Requirements Syntax](https://alistairmavin.com/ears/) — five templates that
force the trigger, state, or condition to be explicit:

| Pattern | Template |
|---|---|
| Ubiquitous | THE `<system>` SHALL `<response>` |
| Event-driven | WHEN `<trigger>`, THE `<system>` SHALL `<response>` |
| State-driven | WHILE `<state>`, THE `<system>` SHALL `<response>` |
| Unwanted behavior | IF `<trigger>`, THEN THE `<system>` SHALL `<response>` |
| Optional feature | WHERE `<feature>`, THE `<system>` SHALL `<response>` |

Each maps to a test with no translation step. If a requirement won't fit one of the five, it
isn't understood yet.

## Parallel implementation

Slices are built concurrently, one implementer per slice. That is only safe because `plan.md`
carries a **pairwise-disjoint per-slice file manifest** — proven at plan time by
`scripts/Assert-SliceManifest.ps1`, which also rejects empty manifests, unknown `depends_on`
and dependency cycles, and re-proven in JS before fan-out. This is Anthropic's own rule for agent teams (*"break the work so each teammate owns a
different set of files"*) enforced as a data structure instead of a hope.

Three ways to run a plan, in descending order of what the fan-out buys you:

1. **Agent teams** — `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1`, one teammate per slice. Start
   with 3-5; tokens scale linearly with teammates and coordination worse than linearly.
2. **`workflows/parallel-build.js`** — deterministic dependency waves, re-proves disjointness,
   halts on a red build rather than feeding a broken dependency forward.
3. **Single writer in one worktree** — always safe, and genuinely *better* when the dependency
   graph is deep, the slices are small, or they touch adjacent code.

Anthropic's caveat is worth repeating: parallel *implementation* carries coordination costs
that parallel *research and review* does not.

**Disjoint source files are not disjoint build output.** Implementers fanned out into one
working tree still share `obj/`, `bin/`, and every other intermediate directory, so a
solution-wide build in two slices at once races itself and fails with a file lock that looks
like a code error. Scope build and test commands to the slice's own project, or give each
implementer its own worktree. The manifest guarantees nothing here — it is about source.

## The gate hook

`hooks/sdlc_gate.py` warns on two distinct states, because they are not the same problem:

- **no approved plan** — nothing has been agreed yet.
- **unplanned scope** — a plan is approved but no slice claims the file being edited. That is
  the verifier's manifest check moved from after the build to before it, which is where it is
  cheap.

Advisory by default — a hard block on every edit gets the plugin uninstalled. Set
`SDLC_GATE=block` to make it refuse instead. It resolves the artifact root from the charter,
is silent in repos with no chain, and exits 0 on anything it did not expect: a hook that runs
on every edit must never be the reason a session breaks. Behaviour pinned in
`scripts/Test-SdlcGate.ps1`.

## Evals

```
claude plugin eval . --no-publish --threshold 0.8
```

**Requires Claude Code 2.1.269 or later, plus early-access enablement on the account.** Retested
on 2.1.268 (2026-09-18): `--help` returns full usage, which is misleading — the command is
registered as a stub and running it still refuses with `plugin eval is currently in early
access`. `winget` reports no newer version available, so this is not a bump away.

The suite below is written to the documented format and frontmatter-checked, but **has never
been executed**, so its graders are uncalibrated. Until it runs, the "process improves itself"
claim has no number attached, and the agent-team topology question has no instrument to settle
it. Both are recorded as blocked in the charter rather than as pending work — the blocker is
external and no amount of effort here moves it.

Three cases under `evals/`, each run three times with the plugin and three times without, so
the report shows what the plugin actually contributes (`Δ`) rather than just that it ran:

- `trivial-change-skips-ceremony` — the escape hatch holds; a typo doesn't get a spec
- `feature-request-tiers-up` — feature work isn't coded on sight
- `overlapping-manifest-is-a-planning-bug` — a shared file is caught before fan-out

In CI: `claude plugin eval . --trust-plugin --json results.json --threshold 0.8 --model
claude-sonnet-5 --no-publish`.

## Checks

```
dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj
```

One command, 49 tests, run in CI on `windows-latest` (`.github/workflows/checks.yml`). MSTest,
the same framework every solution this framework builds uses. The tests drive the **real
shipped artefacts** as external processes — the hook, the manifest check, the workflow
validator — rather than reimplementing their logic in C#, because a reimplementation tests the
reimplementation and the two drift silently.

**Every check is mutation-tested.** The fault is injected and the named check must fail; a
check nobody has seen fail is a check nobody should trust. Twelve faults, twelve caught.

Windows only: the hook and the manifest check both invoke `powershell.exe`.

These exist because **every failure they catch is silent at runtime.** A skill whose
frontmatter is not the first line of the file is simply never loaded; a workflow whose
`phase()` titles disagree with its `meta` renders a blank phase; a gate that stops matching
reads as approval. None of it raises an error — it just quietly stops working, which is the
expensive kind of broken and exactly what bit this repo before.

### Which runtime, and why

| | Runtime | Ships? | Reason |
|---|---|---|---|
| `hooks/sdlc_gate.ps1`, `scripts/Assert-SliceManifest.ps1` | Windows PowerShell 5.1 | **yes** | Runs on every edit and at every fan-out. Present on every Windows machine with no install step, which beats 100ms: measured at 149ms against 67 for node, 51 for python. |
| `workflows/parallel-build.js` | Node | **yes** | Not a choice — workflow scripts *are* JavaScript. That is the format the runtime evaluates. |
| `scripts/check-workflow.mjs` | Node, ~80 lines | no | The only remaining JS tooling. A workflow body must be evaluated the way the runtime evaluates it, which needs a JS parser. Nothing else does. |
| `tests/AiSdlc.Tests` | MSTest / net10.0 | no | Everything else. One framework across everything this owner builds. |

Preference order for anything new: C# → PowerShell → Node → bash/python, each with the standing
exception that a runtime already justified above keeps its job. **There is no Python here**, and
a test fails if any returns.

A 220-line Node structure checker used to sit here. Four of its five check groups were plain
file and string work that needed no JavaScript at all — they are now MSTest, and the ~20 lines
that genuinely need a JS parser are all that remain. Every fault the old checker caught was
re-injected against the ported version before it was deleted.

## Where the tech stack is specified

`sdlc-core` ships **no stack defaults**. A **stack pack** is a skill named `stack-<name>` that
describes one opinionated stack and carries its scaffold files. `init` lists the installed
packs, proposes one (an existing stack in the repo always wins), and writes the files that pack
names. With no pack installed it asks for the stack directly and writes nothing it has no
template for.

`spec-writer` then reads, first answer wins:

| Layer | Scope | Holds |
|---|---|---|
| `.sdlc/CHARTER.md` → **Technology commitments** | one repo | the profile name and overrides — **binding** |
| `CLAUDE.md` | one repo | conventions, commands, gotchas for the agent |
| the codebase | one repo | patterns inferred from existing code |

Set it **once per repo** at `/sdlc-core:init`, not once per feature. A spec that wants a
commitment changed raises a flagged concern; changing one is a charter amendment. On a
greenfield repo `spec-writer` will **ask** rather than invent — the project would otherwise
inherit a guess.

**Code style is never asked about and never stored in the charter.** The stack pack's
`.editorconfig` and formatter enforce it: deterministic, checkable in CI, no dependence on an
agent remembering anything. The charter holds *invariants* — things that make a change wrong
regardless of intent. Brace placement is not one.

### `stack-dotnet`

net10.0 · Aspire (AppHost + ServiceDefaults) · SQL Server with a persisted data volume · MSTest
· coverlet/Cobertura · `EnforceCodeStyleInBuild` + `TreatWarningsAsErrors`. A `dotnet-ml`
variant adds ML.NET, LightGbm, AutoML and `Microsoft.Data.Analysis`.

Scaffolds `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`
and `coverage.runsettings`, never overwriting an existing one. `Directory.Build.props` carries a
`{{SOLUTION}}` token that must be substituted with the repo's name.

### Writing your own pack

Copy `plugins/stack-dotnet`, rename the skill `stack-<yours>`, and replace `assets/`. The
skill's body states which files it writes, from which asset, and any tokens to substitute —
`init` follows that and has no file list of its own.

## Updating the library

The plugin is cached **per version** at `~/.claude/plugins/cache/ai-sdlc/sdlc-core/<version>/`.
Source edits do *not* propagate on their own. To ship a change to every project:

```
# 1. bump version in plugins/sdlc-core/.claude-plugin/plugin.json
# 2.
claude plugin update sdlc-core@ai-sdlc
# 3. restart, or /reload-plugins
```

Skipping the bump means the update is a no-op. While iterating on the plugin itself,
`claude --plugin-dir C:\Repos\ai-sdlc\plugins\sdlc-core` loads the working tree directly and
skips the cache entirely.

## Reused rather than rebuilt

From the official marketplace: `feature-dev` (code-explorer, code-architect),
`pr-review-toolkit` (six review agents), `skill-creator`, `claude-md-management`.

## Prior art

- [Anthropic AI-Native SDLC Playbook](https://claude.com/blog/the-ai-native-sdlc-playbook) — the artifact chain and stage gates
- [GitHub Spec Kit](https://github.com/github/spec-kit) — calls the repo-scope artifact a *constitution*; same idea as `CHARTER.md`
- [Superpowers](https://github.com/obra/superpowers) — brainstorming, worktree isolation, TDD loops
- [BMAD-METHOD](https://github.com/bmad-code-org/BMAD-METHOD) — role-based agents and phased workflows

## Not yet built

- `sdlc-kaggle` — sibling plugin. Competitions need an experiment ledger, a validation architect
  that owns the CV scheme and hunts leakage, baseline-first discipline, and submission-budget
  accounting — not `intent → spec → plan`, because the objective is already given by the metric.
- `/init-competition` — the Kaggle equivalent of `/sdlc-core:init`.
- Stage 5-6: `/ship`, and the Maintain loop back to `intent.md`.
