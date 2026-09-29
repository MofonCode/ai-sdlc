---
name: plan
description: Turn an approved spec into a plan that declares the files it will touch, its blast radius, and how each criterion will be proven. Use after a spec is approved and before any code is written.
argument-hint: "[cycle-id]"
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Write the plan for cycle `$0`

Turn `.sdlc/cycles/$0/spec.md` into `.sdlc/cycles/$0/plan.md`.

**This is the last artifact before anything becomes irreversible, and the only one a person
approves before code exists.** Everything after it is machine-decided until landing. Write it as
the thing someone reads to decide whether to let the change happen.

## Refuse if the spec is not approved

Same rule as the spec's: no plan against a spec with unresolved blocking findings, and none against a
spec whose frontmatter carries no `approved_by` and `approved_at`. That pair is how a person records
approval, and they write it — never you.

## Choose shapes from the defaults, and say so when you depart

The stack defaults are injected at the end of this skill: which project shape serves which concern, when
a solution gets an Aspire AppHost, and which shared-kernel type covers which seam.

They are defaults, not rules. A solution may depart from any of them, and the departure is legal — but
**silence about it is not.** When this plan introduces a project, name the shape it uses; when it departs
from the default, say why in one sentence. That sentence is what a reviewer checks, and its absence is
what makes a departure look like an oversight.

Two places plans go wrong against these:

- **Standing up orchestration nothing needs.** An AppHost belongs where there is more than one process,
  or one process plus a backing service. A lone executable with no dependencies does not get one, and a
  plan that builds one anyway has spent the cycle on scaffolding
- **Naming a library instead of a seam.** "Use the shared kernel" chooses nothing. Name the type — the
  mediator interface, the result extension, the trace scope — so the proof can cite something real

## Declare every file

List every file you will create, change or delete, each with what happens to it. A hook compares
the eventual diff against this list and refuses a file you did not declare, so an incomplete list
is not a small problem — it stops the build.

Read the repository before writing the list. A plan that guesses at file paths is a plan that
fails its own hook.

## Name a proof for every criterion, and make it adequate

CON_06. For each numbered criterion in the spec, name the check that establishes it — a test by
name, a measurement, an observation with a procedure.

The test is **could this check pass while the criterion is unmet?** If yes, the proof is
inadequate and worse than none, because it produces a green verification rather than an obvious
gap. A unit test asserting a query is issued once does not prove a two-second budget. "Manual
check" proves nothing that survives the session. "Covered by criterion 1" proves nothing at all.

Where you genuinely cannot prove a criterion, say so under **Cannot prove** rather than inventing
a check. That is an honest plan; the other kind is a trap.

## Declare the blast radius

One tier, in frontmatter:

| Tier | When |
|---|---|
| `local` | nothing outside this repository depends on what changes |
| `contract` | a published API, event shape or schema that something else consumes |
| `data` | a migration, or anything reverting the code does not undo |

Choose by reversibility, not by size. A one-line change that drops a column is `data`. Say in one
sentence why the tier is what it is — that sentence is what a reviewer checks.

## Write it

Frontmatter carries `id`, `spec` and `tier`, and `spec` is the **repository-relative path** of the
spec on disk — `.sdlc/cycles/<id>/spec.md`, never a bare filename.

```markdown
# Plan - <the spec's subject>

## Files
| File | Change |

## Order
<only when something must happen before something else, and why>

## Proof
- Criterion 1 - <the check, named>

## Cannot prove       (omit if empty)

## Risk
<what could go wrong that the proofs would not catch, and what reduces it>
```

## Then stop

**Do not invoke another skill.** A skill cannot reliably call another one, and a chain that only
sometimes fires is worse than none: it produces artifacts nobody challenged while appearing to have
challenged them. Report what you produced and name the next command for whoever is driving — a person
in an interactive session, or the batch runner in an unattended one.

Say: the plan is at `.sdlc/cycles/$0/plan.md`, the next step is
`/sdlc:interrogate .sdlc/cycles/$0/plan.md`, and after that a person approves it by adding
`approved_by` and `approved_at` to its frontmatter.

**Do not begin building.** A hook refuses source edits until that approval exists, so an attempt
wastes the turn.

---

# Reference

The framework version, the current UTC time, the global constraints, and the stack defaults, injected at
load time. They
are here so that what you write is written to them. **Do not try to read them from disk** — a session
working in a governed solution is sandboxed to that solution and the attempt is denied, which wastes a
turn and looks like a failure to anyone watching.

Interrogation is still the gate. Writing to the constraints does not mean judging your own work
against them.

!`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "${CLAUDE_PLUGIN_ROOT}/tools/Get-Constraints.ps1"`

---

!`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "${CLAUDE_PLUGIN_ROOT}/tools/Get-StackDefaults.ps1"`
