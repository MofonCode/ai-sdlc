---
name: constraints
description: Render the effective constraint set for this solution — global constraints merged with the solution's own. Use when someone asks what rules apply here, or before writing a local constraint.
argument-hint: "[constraint id, optional]"
allowed-tools: Bash(powershell.exe *)
---

# The effective constraint set

Render what an interrogation in this repository actually checks against. Nothing is stored merged —
the effective set is derived, the same way a verify document is.

Two sources:

| Source | Owns | Series |
|---|---|---|
| `${CLAUDE_PLUGIN_ROOT}/constraints.md` | global constraints | `CON-nn` |
| `.sdlc/constraints.md` | this solution's own | `LOC-nn` |

With an argument, print that one constraint in full — statement, `Applies to`, `Forbids`, and why.
Without one, print a table: id, statement, and what it applies to. Say where each came from, since
`CON-` and `LOC-` are resolved in different files and a reader needs to know which they can change.

Record the framework version from `${CLAUDE_PLUGIN_ROOT}/.claude-plugin/plugin.json`. Global
constraints live in the library, so a plugin upgrade changes these rules with no commit here.

## If someone wants to add a local constraint

Say the three tests before they write it. All must pass:

1. Its `Forbids:` clause names something that becomes illegal
2. No hook, schema or analyzer could decide it — it needs reading comprehension
3. It is incompatible with at least one plausible implementation

And the narrower test for a local one: it names a cross-cutting hazard that **looks fine locally in
a diff**. A tenant id read from a request body looks like ordinary parameter binding; a column drop
looks like cleanup. That is the class a reviewer misses and an analyzer cannot judge, because the
violation is about context rather than syntax.

If an analyzer could catch it, it belongs to the analyzer. Most candidates do.

IDs are a monotonic sequence: assigned once, never reused, never backfilled. The next `LOC-` is one
higher than the highest that has ever existed, including any that were removed.
