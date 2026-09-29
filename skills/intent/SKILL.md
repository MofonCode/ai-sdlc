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

Most requests arrive already shaped as a solution — "add a cache", "make the export async". CON_01
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

**Ask them in the tense the work is actually in.** The wording below is for something that exists and
misbehaves. For a new solution nothing is broken yet, and asking how they would know it was *fixed*
reads as though you have not understood the request — which is the impression it made the first time
this skill met one. Establish the type first, then use the matching column.

| | Something exists and misbehaves | Nothing exists yet |
|---|---|---|
| 1 | What happens today that shouldn't? | What can nobody do today that they need to? |
| 2 | Who notices, and how often? | Who needs this, and how often would they use it? |
| 3 | How would you know it was fixed? | What would you be able to see that you cannot see today? |
| 4 | What must not change? | What is out of bounds — what must this not touch or become? |

Question 3 is the highest-value answer you will get in either column: it is the seed the spec grows
from. Do not let them answer in thresholds; that is the spec's job.

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

CON_02 forbids stating as settled fact a figure, cause or behaviour nobody verified. Every later
phase treats an approved intent as settled, so a guessed number becomes a criterion, then a test,
then a green verification of something nobody established.

Record unknowns under **Open**. An intent with three open questions is sound. An intent with
three invented answers is a liability.

## Write it

Write `.sdlc/cycles/<id>/intent.md`, where `<id>` is `YYYYMMDD_xxxx` — the date from the injected
`Now (UTC)` value below, and four random hex characters. Create the directory; everything else this
cycle produces will sit beside it.

Frontmatter first, and it is not optional — every later phase routes on it:

- `id` — the same `YYYYMMDD_xxxx`
- `type` — one of `solution`, `feature`, `bug`, `suggestion`
- `stack` — only when this intent departs from the solution's declared stack
- `source` — only for `type: suggestion`: the verify document it came from

Then the sections, in order:

```markdown
---
id: 20260927_7b3c
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

Say: the intent is at `.sdlc/cycles/<id>/intent.md`, the next step is
`/sdlc:interrogate .sdlc/cycles/<id>/intent.md`, and after that a person approves it by adding
`approved_by` and `approved_at` to its frontmatter. Do not write that pair yourself — it is the gate, not a
formality.

If the interview left many open questions because nobody was there to answer them, say so plainly.
An intent with seven open questions is honest, and it is also not ready for a spec — the person
reading this needs to know which of the two situations they are in.

---

# Reference

The framework version and the current UTC time, injected at load time. You cannot read a clock, and a
guessed date puts the cycle id in the wrong day.

!`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "${CLAUDE_PLUGIN_ROOT}/tools/Get-Constraints.ps1"`
