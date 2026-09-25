# Skills

The library. One directory each, holding a `SKILL.md`, invoked as `/sdlc:<directory-name>`.

The plugin manifest does not name this directory because Claude Code always scans `skills/`
at the plugin root — declaring it would only add a second place for the path to drift.

## The rule: skill or workflow

Both contribute `/sdlc:<name>`, so the question is never "which is nicer" but which one the
stage can actually be. A stage is a **workflow** only when **both** of these hold:

1. **It needs several agents that must not see each other's conclusions.** Not "it would be
   faster in parallel" — the *independence* has to be the method.
2. **It can never need to ask the user anything.** A workflow cannot ask, at all, ever. One
   question it cannot answer itself and the workflow form is wrong.

Everything else is a skill. And where a rule must be mechanical rather than instructed, it
goes into a script under `tools/` — not into a workflow, because a script can read files and
be unit-tested and a workflow script can do neither.

By that rule exactly one stage is a workflow:

| | Independent agents? | Can need to ask? | |
|---|---|---|---|
| `interrogate` | **yes** — four orthogonal roles, plus per-finding triage | no, it reports | **workflow** |
| `spec` · `plan` | no | **yes** — an ambiguity only the user can settle | skill |
| `repair` | no | **yes** — an escalation *is* a question | skill |
| `build` | not by design | **yes** — a plan defect must surface | skill |
| `verify` · `retro` | no | no, but there is no fan-out either | skill |
| `intent` | no | **yes** — it is an interview | skill |
| `run` · `init` · `absorb-retro` | no | yes | skill |

`verify` and `retro` are the interesting pair: they *could* be workflows, since neither needs
to ask. They are skills because clause 1 fails — a workflow that spawns one agent to do
everything is a wrapper with no fan-out inside it, and it pays for that with no filesystem
access, no testability, and a JavaScript file where a script would do.

## Reachability, which is the other half of the rule

| Context | Can launch a workflow | Can invoke a skill |
|---|---|---|
| Interactive session | yes | yes |
| `claude -p` (what the batch launches) | yes | yes |
| Subagent | **no** — the tool is removed | yes |
| Teammate | **no** | yes |

So a skill runs everywhere and a workflow runs only from a main session. Anything a subagent
or a teammate might need to drive cannot be a workflow, whatever else is true of it.

## Supervised, and batched

- **Supervised, one cycle at a time:** `intent` → `spec` → `plan`. A person drives these and
  approves the plan, because this is where the judgment is and where a stage may need to ask
  something only they can answer. An unsupervised spec is perfectly safe and probably wrong.
- **Batched, unattended:** `build` → `verify`, one worktree per cycle, via `run`. An approved
  plan is a contract with a declared file list and a signed-off tier, so executing it is
  mechanical and checkable. That is the only part worth batching.

The gate between them is the `approved:` field in a plan's frontmatter. Without it the
lifecycle derivation reports the cycle as `queued` rather than `planned`, and no batch will
touch it. `plan` is forbidden from writing that field itself.

## Naming

The plugin is named `sdlc`, so a skill is addressed by its directory name and a workflow by
its `meta.name`. That name is load-bearing: renaming the plugin silently breaks every
`/sdlc:…` reference in the shipped tooling, because those references are strings inside
prompts and no compiler sees them. `PluginManifestTests` is what sees them.
