---
name: retro
description: Write the retro for a landed cycle, the completion certificate that carries evidence back to ai-sdlc. Use only at landing, after a cycle has verified and merged.
argument-hint: [cycle-id]
allowed-tools: Bash(git *) Bash(powershell.exe *)
---

# Write the retro for cycle $0

Write `.sdlc/cycles/$0/retro.md`.

## Refuse unless the cycle actually landed

Read `.sdlc/cycles/$0/verify.md` first.

- **No `verify.md`** → refuse.
- **`outcome` is not `passed`** → refuse.
- **Not yet merged** → refuse.

Say which condition failed and stop.

This refusal is the single most load-bearing thing here. **The retro is the completion
certificate**: the lifecycle derivation reads "a retro exists" as `done`. Write one for a cycle
that did not land and a dead cycle reads as finished, a dispatcher stops relaunching it, and
the work is silently lost. That is worse than no retro at all.

## What goes in, and why each field exists

Every field is here because **it cannot survive the copy any other way**. A retro travels to
the ai-sdlc corpus alone — no spec, no plan, no verify document follows it — so anything the
amendment signals need must be *in* it.

The frontmatter must validate against `schemas/retro.schema.json`; read it rather than trusting
this list:

```yaml
---
cycle-id: $0
solution: <this solution's name, from the charter frontmatter>
landed-at: <the date it merged>
charter-version: <the version in force when the cycle ran>
---
```

- **`solution`** — the corpus aggregates across solutions; a retro that does not say where it
  came from cannot be attributed.
- **`charter-version`** — amendments never rewrite history. A retro stamped `charter v1` stays
  interpretable against v1 forever, and a signal computed across mixed versions without this
  is meaningless.
- **`caused-by` / `caused-at-stage`**, when this cycle fixed something an earlier one produced.
  Upstream attribution is the high-value signal: it is how the process learns which *stage*
  leaks, not merely that defects happen.

In the body carry **every finding with its category, attributed stage, detection and
disposition**. That is what the signals are computed from. Do not summarise the findings into
prose and drop the fields — a paraphrase is not aggregatable.

## Do not compute the signals here

Report what happened. **Do not conclude what the charter should become.**

Amendment is by counts across the whole corpus, not by one cycle's impression, and a signal is
a prompt to investigate rather than a verdict. One cycle cannot see a trend, and a retro that
editorialises puts a conclusion into the evidence it is supposed to be.

`/sdlc:absorb-retro` computes signals, across many retros, and even then only *proposes*.

## Note honestly what the process cost

Where a stage was slow, wasteful, or produced findings that were all rejected, say so with
numbers. A role whose findings are consistently rejected is a **noisy role**, and that is a
signal about the process rather than about the work — but only if the retro records it.

## When you are done

Confirm the path. Then state that the cycle is `done`, and that its evidence reaches ai-sdlc
through `/sdlc:absorb-retro`.
