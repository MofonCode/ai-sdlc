---
name: spec
description: Turn an accepted intent into testable acceptance criteria. Use after an intent exists and a person has accepted it, and before any planning.
argument-hint: "[cycle-id]"
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Write the spec for cycle `$0`

Turn `.sdlc/intents/$0.md` into `.sdlc/cycles/$0/spec.md`.

A spec says **what done means** for this cycle, in criteria a test could check. It answers to the
intent in both directions, and those two failures are different constraints because they are fixed
differently.

## Refuse if the intent is not ready

Read the intent first. Stop and say so if:

- it does not exist, or names a problem you cannot find
- its own interrogation left blocking findings unresolved
- a person has not accepted it

Do not write a spec against an intent nobody approved. The spec inherits the intent's authority,
and an unapproved intent has none to give.

## Every criterion must be able to fail

CON-03. Words like *performs well*, *works correctly*, *handles large files*, *is secure* name no
observable outcome and no check. A criterion that cannot fail is satisfied by construction: build
implements it, verify passes it, and nobody ever learns whether it was met.

Each criterion states **the observable outcome** and **how it is checked**. A threshold needs a
number and the conditions it holds under. If the intent left the number open, you cannot invent
it — say which criterion is blocked on which open question and stop.

## Cover the intent, and nothing more

- **CON-04** — every outcome the intent asks for has a criterion. A gap here is invisible
  downstream: build implements the spec, verify checks the spec, and the omitted outcome is never
  mentioned again. Read the intent's **Must not change** section especially; it is the one most
  often dropped
- **CON-05** — no criterion that no statement in the intent supports. Scope added here was never
  approved by whoever accepted the intent, and it arrives carrying an accepted artifact's
  authority

Honour **Fixed decisions** as given. They are a person's choice, not yours to revisit.

## Write it

Sections:

```markdown
# Spec - <what done means, in a line>

## Acceptance criteria
1. <observable outcome, and how it is checked>

## Out of scope
<what a reader might reasonably expect and will not get, so the plan does not add it>

## Blocked on            (omit if none)
<criterion that cannot be written until an open question is answered>
```

Number the criteria. The plan and the verify document both cite them by number, so the numbers are
an interface: never renumber a criterion once the spec is accepted.

## Then stop

Run `/sdlc:interrogate .sdlc/cycles/$0/spec.md` and report. A person accepts the spec, not you.
