---
name: plan
description: Turn an accepted spec into a plan that declares the files it will touch, its blast radius, and how each criterion will be proven. Use after a spec is accepted and before any code is written.
argument-hint: "[cycle-id]"
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Write the plan for cycle `$0`

Turn `.sdlc/cycles/$0/spec.md` into `.sdlc/cycles/$0/plan.md`.

**This is the last artifact before anything becomes irreversible, and the only one a person
approves before code exists.** Everything after it is machine-decided until landing. Write it as
the thing someone reads to decide whether to let the change happen.

## Refuse if the spec is not accepted

Same rule as the spec's: no plan against an unaccepted spec, and none against a spec with
unresolved blocking findings.

## Declare every file

List every file you will create, change or delete, each with what happens to it. A hook compares
the eventual diff against this list and refuses a file you did not declare, so an incomplete list
is not a small problem — it stops the build.

Read the repository before writing the list. A plan that guesses at file paths is a plan that
fails its own hook.

## Name a proof for every criterion, and make it adequate

CON-06. For each numbered criterion in the spec, name the check that establishes it — a test by
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

Run `/sdlc:interrogate .sdlc/cycles/$0/plan.md` and report. **Do not begin building.** A person
approves this plan first, and saying so is the last thing you do.
