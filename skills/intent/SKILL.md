---
name: intent
description: Interview a person into a governed intent, the first artifact of a cycle. Use when someone wants to start new work, reports a problem, or asks to turn an idea into something the process can carry.
argument-hint: "[a sentence describing the work, optional]"
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Author an intent

An intent says **why** work exists. It is the only artifact written by interview, and your job is
to **elicit, not draft**. The person knows what they want; they do not yet know what is
underspecified about it. Finding that out is the work.

## First, convert

Most requests arrive already shaped as a solution — "add a cache", "make the export async". CON-01
forbids an intent that is an implementation instruction with no statement of what is wrong. So the
first move is not a question, it is a conversion:

> What happens today that shouldn't?

If the answer names another solution, ask once more what its absence causes. "We have no cache" is
a missing thing. "The dashboard takes fourteen seconds and people stop opening it" is an intent.

**Do not discard their solution.** A decision already taken is legitimate — record it under
**Fixed decisions** with who decided it and when. That keeps the intent a problem statement while
letting the spec honour the decision, and six months later it distinguishes a requirement from an
assumption.

## The questions

Four always, in roughly this order. Stop asking when an answer would only be a guess.

1. **What happens today that shouldn't?** — the problem
2. **Who notices, and how often?** — affected people and systems, frequency, severity
3. **How would you know it was fixed?** — the observable difference. This is the seed the spec
   grows from, so it is the highest-value answer you will get. Do not let them answer in
   thresholds; that is the spec's job
4. **What must not change?** — compatibility, deadlines, data shapes, anything out of bounds. The
   most-skipped question and the one whose absence causes the most rework

Then, conditionally:

5. **Is there a decision already made that I should treat as fixed?**
6. **Is anything here not our default stack?** — only for a new solution, or when the request
   implies it. Record it, because `solution.md` is what later cycles read

## Never ask

- Anything the repository can answer — where code lives, what the current behaviour is. An
  interview spends a person's attention; do not spend it on what you can read
- How to build it. That is the plan
- Criteria in testable form. That is the spec. The boundary: an intent says "the export finishes
  without the user waiting"; a spec says "returns a job id within 200ms"

## Unknowns stay unknown

CON-02 forbids stating as settled fact a figure, cause or behaviour nobody verified. Every later
phase treats an accepted intent as settled, so a guessed number becomes a criterion, then a test,
then a green verification of something nobody established.

Record unknowns under **Open**. An intent with three open questions is sound. An intent with
three invented answers is a liability.

## Write it

Write `.sdlc/intents/<id>.md` where `<id>` is `YYYYMMDD-xxxx` — the date from the injected `Now (UTC)`
value below, and four random hex characters.

Frontmatter first, and it is not optional — every later phase routes on it:

- `id` — the same `YYYYMMDD-xxxx`
- `type` — one of `solution`, `feature`, `bug`, `suggestion`
- `stack` — only when this intent departs from the solution's declared stack
- `source` — only for `type: suggestion`: the verify document it came from

Then the sections, in order:

```markdown
---
id: 20260927-7b3c
type: bug
---

# <one line naming the problem, not the solution>

## Problem
## Who
## Done looks like
## Fixed decisions      (omit if none)
## Must not change      (omit if none)
## Open                 (omit only if genuinely nothing is unknown)
```

## Then stop

**Do not invoke another skill.** A skill cannot reliably call another one, and a chain that only
sometimes fires is worse than none: it produces artifacts nobody challenged while appearing to have
challenged them. Report what you produced and name the next command for whoever is driving — a person
in an interactive session, or the batch runner in an unattended one.

Say: the intent is at `.sdlc/intents/<id>.md`, the next step is
`/sdlc:interrogate .sdlc/intents/<id>.md`, and after that a person accepts it by adding `accepted_by`
and `accepted_at` to its frontmatter. Do not write that pair yourself — it is the gate, not a
formality.

If the interview left many open questions because nobody was there to answer them, say so plainly.
An intent with seven open questions is honest, and it is also not ready for a spec — the person
reading this needs to know which of the two situations they are in.

---

# Reference

The framework version and the current UTC time, injected at load time. You cannot read a clock, and a
guessed date puts the cycle id in the wrong day.

!`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "${CLAUDE_PLUGIN_ROOT}/tools/Get-Constraints.ps1"`
