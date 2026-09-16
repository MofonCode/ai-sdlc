# ai-sdlc

A personal Claude Code plugin marketplace for AI-assisted software development, implementing
[Anthropic's AI-Native SDLC Playbook](https://claude.com/blog/the-ai-native-sdlc-playbook).

The one idea everything rests on: **every stage commits a version-controlled artifact the next
stage reads.** The artifact chain is the state machine — not the order the commands were typed.

```
CHARTER.md                            repo scope, long-lived, every intent is checked against it
   │
   ├── intent.md  →  spec.md  →  plan.md  →  code + tests  →  verified diff
        Plan          Design       Build         Build            Test
```

Each arrow is a human gate.

## Install

```
/plugin marketplace add C:\Repos\ai-sdlc
/plugin install sdlc-core@ai-sdlc
```

Installed once, available in every project. It stays quiet in repos that have no `.sdlc/`
directory.

## Plugins

### `sdlc-core`

| Command | Stage | Produces |
|---|---|---|
| `/intent` | Plan | `.sdlc/<slug>/intent.md` |
| `/spec` | Design | `.sdlc/<slug>/spec.md` |
| `/plan` | Build | `.sdlc/<slug>/plan.md` with a file-disjoint slice manifest |

| Agent | Role |
|---|---|
| `intent-interrogator` | Fresh-context adversary. Attacks a draft intent or spec for unfalsifiable criteria, smuggled solutions, missing non-goals, assumed invariants, charter conflicts. |
| `spec-writer` | Approved intent → draft spec: numbered testable requirements, chosen design, rejected alternatives, flagged concerns with named owners. |
| `verifier` | Fresh-context verification of a finished change: real build/test output, requirement coverage, silent failures, time-series contamination. |

Plus the `sdlc-router` skill (tiering + artifact location), a `PreToolUse` gate hook, and a
parallel-build workflow script.

## Tiers — the escape hatch

Ceremony that costs more than it saves gets abandoned, and then nothing gets used. Every
command tiers the request first and skips everything below it.

| Tier | Looks like | Required |
|---|---|---|
| **trivial** | typo, rename, config value, one-liner | nothing |
| **task** | bounded change, behavior already agreed, few files | `plan.md` |
| **feature** | new capability, schema/contract change, hard to reverse | full chain |

## Parallel implementation

Multiple implementer agents build concurrently, one per slice. That is only safe because
`plan.md` carries a **mandatory per-slice file manifest whose lists are pairwise disjoint** —
`/plan` proves disjointness before offering the build, and `workflows/parallel-build.js`
re-proves it before fanning out. Two agents holding the same file is a merge conflict by
construction, so an overlap is treated as a planning bug: fix the plan, never reconcile in the
merge.

Slices are dispatched in dependency waves. A red build halts the run rather than feeding a
broken dependency to the next wave.

## The gate hook

`hooks/sdlc_gate.py` warns when source files are edited with no approved `plan.md`. Advisory
by default — a hard block on every edit is infuriating and gets the plugin uninstalled. Set
`SDLC_GATE=block` to make it refuse instead.

## Reused rather than rebuilt

From the official marketplace: `feature-dev` (code-explorer, code-architect),
`pr-review-toolkit` (six review agents), `skill-creator` (skill authoring **and its eval
harness**), `claude-md-management`.

## Not yet built

- `sdlc-kaggle` — sibling plugin. Competitions need an experiment ledger, a validation
  architect that owns the CV scheme and hunts leakage, baseline-first discipline, and
  submission-budget accounting — not `intent → spec → plan`, because the objective is already
  given by the metric.
- `sdlc-init` — `/init-solution`, `/init-competition`, and `/charter` scaffolding.
- `evals/` — 20-50 real tasks gating every change to skills, agents, and hooks. The playbook
  makes this a gate, and without it there is no way to know whether skill #14 made things worse.
- Stage 4-6 commands: `/verify`, `/ship`, and the Maintain loop back to `intent.md`.
