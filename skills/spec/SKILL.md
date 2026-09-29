---
name: spec
description: Turn an approved intent into testable acceptance criteria. Use after an intent exists and a person has accepted it, and before any planning.
argument-hint: "[cycle-id]"
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Write the spec for cycle `$0`

Turn `.sdlc/cycles/$0/intent.md` into `.sdlc/cycles/$0/spec.md`.

A spec says **what done means** for this cycle, in criteria a test could check. It answers to the
intent in both directions, and those two failures are different constraints because they are fixed
differently.

## Refuse if the intent is not ready

Read the intent first. Stop and say so if:

- it does not exist, or names a problem you cannot find
- its own interrogation left blocking findings unresolved
- it carries no `approved_by` and `approved_at` in its frontmatter. That pair is how a person records
  approval, and they write it — never you

Do not write a spec against an intent nobody approved. The spec inherits the intent's authority,
and an unapproved intent has none to give.

## Every criterion must be able to fail

CON_03. Words like *performs well*, *works correctly*, *handles large files*, *is secure* name no
observable outcome and no check. A criterion that cannot fail is satisfied by construction: build
implements it, verify passes it, and nobody ever learns whether it was met.

Each criterion states **the observable outcome** and **how it is checked**. A threshold needs a
number and the conditions it holds under. If the intent left the number open, you cannot invent
it — say which criterion is blocked on which open question and stop.

## Cover the intent, and nothing more

- **CON_04** — every outcome the intent asks for has a criterion. A gap here is invisible
  downstream: build implements the spec, verify checks the spec, and the omitted outcome is never
  mentioned again. Read the intent's **Must not change** section especially; it is the one most
  often dropped
- **CON_05** — no criterion that no statement in the intent supports. Scope added here was never
  approved by whoever approved the intent, and it arrives carrying an approved artifact's
  authority

Honour **Fixed decisions** as given. They are a person's choice, not yours to revisit.

## Write it

Sections:

Frontmatter carries `id` and `intent`, and `intent` is the **repository-relative path** of the
intent on disk — `.sdlc/cycles/<id>/intent.md`, never a bare filename. Every later phase binds the spec to
its intent through that field, and an interrogation that cannot resolve it cannot judge coverage.

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
an interface: never renumber a criterion once the spec is approved.

## Then stop

**Do not invoke another skill.** A skill cannot reliably call another one, and a chain that only
sometimes fires is worse than none: it produces artifacts nobody challenged while appearing to have
challenged them. Report what you produced and name the next command for whoever is driving — a person
in an interactive session, or the batch runner in an unattended one.

Say: the spec is at `.sdlc/cycles/$0/spec.md`, the next step is
`/sdlc:interrogate .sdlc/cycles/$0/spec.md`, and after that a person approves it by adding
`approved_by` and `approved_at` to its frontmatter.

---

# Reference

The framework version, the current UTC time, and the global constraints, injected at load time. They
are here so that what you write is written to them. **Do not try to read them from disk** — a session
working in a governed solution is sandboxed to that solution and the attempt is denied, which wastes a
turn and looks like a failure to anyone watching.

Interrogation is still the gate. Writing to the constraints does not mean judging your own work
against them.

!`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "${CLAUDE_PLUGIN_ROOT}/tools/Get-Constraints.ps1"`
