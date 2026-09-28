---
name: constraints
description: Show the global constraints every solution is judged against, merged with the solution's own. Use when someone asks what rules apply here, or before writing a local constraint.
argument-hint: "[constraint id, optional]"
allowed-tools: Bash(powershell.exe *)
---

# The effective constraint set

The global constraints are injected at the end of this skill, at load time. They are **not** yours to
read from disk: a session working in a governed solution is sandboxed to that solution and cannot
reach the plugin root, so anything depending on a file read there would fail - and would fail hardest
in an unattended run, where there is nobody to approve a directory.

The effective set is those plus the solution's own `.sdlc/constraints.md`, which is inside the
solution and always readable. Nothing is stored merged: like a verify document, the effective set is
derived.

| Source | Owns | Series |
|---|---|---|
| the library, injected below | global constraints | `CON_nn` |
| `.sdlc/constraints.md` | this solution's own | `LOC_nn` |

A citation says where to resolve it. `CON_` binds every solution; `LOC_` binds only this one. A reader
needs the distinction to know which they may change.

With an argument, print that one constraint in full. Without one, print a table of id, statement and
what each applies to, saying which source each came from, and report the framework version - every
findings and verify document records it, because global constraints travel with the library and an
upgrade changes these rules with no commit in the solution.

## Adding a local constraint

Say the three tests below before one is written, and the narrower test for a local one: it names a
cross-cutting hazard that **looks fine locally in a diff**. A tenant id read from a request body looks
like ordinary parameter binding; a column drop looks like cleanup. That is the class a reviewer misses
and an analyzer cannot judge, because the violation is about context rather than syntax. If an
analyzer could catch it, it belongs to the analyzer - most candidates do.

`LOC_` ids are a monotonic sequence of their own: the next is higher than any that has ever existed in
this solution, including any since removed.

---

!`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "${CLAUDE_PLUGIN_ROOT}/tools/Get-Constraints.ps1"`
