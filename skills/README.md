# Skills

Skills go here, one directory each, holding a `SKILL.md`. The plugin manifest does not
name this directory because Claude Code always scans `skills/` at the plugin root —
declaring it would only add a second place for the path to drift.

## Why there are so few of them

Most of this framework is **workflows**, not skills, and the split is not stylistic.

A workflow cannot ask a question mid-run. That is a hard property of the runtime, and it
is the reason the stages are workflows: *the gate boundary is the workflow boundary*. When
`spec` finishes, the run ends, and a person deciding whether to continue is the only thing
that can start the next one. Written as a skill, every stage would be free to carry itself
past a gate on its own judgment, and the gates would be a convention rather than a
mechanism. Nothing would enforce them.

So the rule:

| What it is | What it must be |
|---|---|
| A stage that produces a governed artifact | a **workflow** — it ends at its gate |
| An interaction that needs the person in the loop | a **skill** — it cannot be a workflow |

Only three things fall on the skill side, because only three of them genuinely need a
conversation:

| Skill | Why it cannot be a workflow | Status |
|---|---|---|
| `intent` | It is an interview. The elicitation *is* the work, and a workflow cannot ask | PLAN item 11 |
| `absorb-retro` | It proposes charter amendments, which a person must approve one at a time | PLAN item 12 |
| `init` | It scaffolds from a pack, and which pack is a question | PLAN item 15 |

## Namespacing

The plugin is named `sdlc`, so every component is addressed as `/sdlc:<name>`. A workflow
is named by its `meta.name` rather than its filename; a skill by its directory name.

That name is load-bearing: renaming the plugin silently breaks every `/sdlc:…` reference
in the shipped tooling, because those references are strings inside prompts and no
compiler sees them. `PluginManifestTests` is what sees them — it asserts the plugin name,
and that every `/sdlc:…` reference in shipped tooling resolves to a workflow or a skill
that actually ships.
