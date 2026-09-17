# ai-sdlc

A personal Claude Code plugin marketplace for AI-assisted software development, implementing
[Anthropic's AI-Native SDLC Playbook](https://claude.com/blog/the-ai-native-sdlc-playbook).

The one idea everything rests on: **every stage commits a version-controlled artifact the next
stage reads.** The artifact chain is the state machine — not the order the skills were invoked.

```
CHARTER.md                            repo scope, long-lived, every intent is checked against it
   │
   ├── intent.md  →  spec.md  →  plan.md  →  code + tests  →  verified diff
        Plan          Design       Build         Build            Test
```

Each arrow is a human gate. Cross a gate in a **fresh session** — the artifacts are written to
be self-contained precisely so the prior conversation can be dropped.

## Install

```
/plugin marketplace add C:\Repos\ai-sdlc
/plugin install sdlc-core@ai-sdlc
```

Installed once, available in every project. It stays silent in repos that have no `.sdlc/`
directory.

## Plugins

### `sdlc-core`

Plugin skills are namespaced, so the short form works only when nothing else claims it.

| Skill | Stage | Produces |
|---|---|---|
| `/sdlc-core:init` | 0 — once per repo | `.sdlc/CHARTER.md` |
| `/sdlc-core:intent` | Plan | `.sdlc/<slug>/intent.md` |
| `/sdlc-core:spec` | Design | `.sdlc/<slug>/spec.md` |
| `/sdlc-core:plan` | Build | `.sdlc/<slug>/plan.md` with a file-disjoint slice manifest |
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
carries a **pairwise-disjoint per-slice file manifest** — proven at plan time, re-proven before
fan-out. This is Anthropic's own rule for agent teams (*"break the work so each teammate owns a
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

## The gate hook

`hooks/sdlc_gate.py` warns when source files are edited with no approved `plan.md`. Advisory by
default — a hard block on every edit gets the plugin uninstalled. Set `SDLC_GATE=block` to make
it refuse instead.

## Evals

```
claude plugin eval . --no-publish --threshold 0.8
```

**Requires Claude Code 2.1.269 or later** — the command exists as a stub in 2.1.268 and refuses
with `plugin eval is currently in early access`. The suite below has been written to the
documented format and frontmatter-checked, but **has not yet been executed**, so the graders are
uncalibrated until a first run.

Three cases under `evals/`, each run three times with the plugin and three times without, so
the report shows what the plugin actually contributes (`Δ`) rather than just that it ran:

- `trivial-change-skips-ceremony` — the escape hatch holds; a typo doesn't get a spec
- `feature-request-tiers-up` — feature work isn't coded on sight
- `overlapping-manifest-is-a-planning-bug` — a shared file is caught before fan-out

In CI: `claude plugin eval . --trust-plugin --json results.json --threshold 0.8 --model
claude-sonnet-5 --no-publish`.

## Where the tech stack is specified

Three layers, read by `spec-writer` in this order — first answer wins:

| Layer | Scope | Holds |
|---|---|---|
| `.sdlc/CHARTER.md` → **Technology commitments** | one repo | language, framework, test runner, persistence — **binding** |
| `CLAUDE.md` | one repo | conventions, commands, gotchas for the agent |
| the codebase | one repo | patterns inferred from existing code |

Set it **once per repo** at `/sdlc-core:init`, not once per feature. A spec that wants a
commitment changed raises a flagged concern; changing one is a charter amendment.

For conventions spanning several projects, put them in their own skill and name it from the
charter (`conventions: the stack-dotnet skill`) rather than restating them per repo.

On a greenfield repo `spec-writer` will **ask** rather than invent a stack — the whole project
would otherwise inherit a guess.

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
`claude --plugin-dir C:\Reposi-sdlc\plugins\sdlc-core` loads the working tree directly and
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
- Stage 4-6: `/verify`, `/ship`, and the Maintain loop back to `intent.md`.
