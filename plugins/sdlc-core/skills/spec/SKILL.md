---
name: spec
description: Turn an approved intent.md into an approved spec.md - EARS-form testable requirements, the chosen design with rejected alternatives, data and contract changes, and flagged concerns with named owners. Stage 2 (Design) of the AI-Native SDLC artifact chain.
argument-hint: Slug of the slice to spec, or blank to infer from .sdlc/
disable-model-invocation: true
---

# /sdlc-core:spec — Stage 2, Design

Produce a `spec.md` an engineer could plan from. Schema in `references/artifact-schemas.md`
(the `sdlc-router` skill).

**Resolve the artifact root first.** It defaults to `.sdlc/` but is per-repo, declared in the charter's `artifact_root` (see `sdlc-router`). Everything below writes `<root>/`; never hardcode `.sdlc/`.

## 1. Check the gate behind you

Read `<root>/<slug>/intent.md`.

- **No intent.md** — if the tier is `feature`, run `/sdlc-core:intent` first; say so and stop. If the
  user insists there is no intent because this is a `task`, offer `/sdlc-core:plan` instead.
- **`status: draft`** — the intent is not approved. Stop and ask for approval. Producing a
  spec from an unapproved intent means designing a solution to a problem nobody agreed on.
- **`status: approved`** — proceed.

Revising an existing spec? Read it first and amend. Append new requirements with fresh numbers
and mark superseded ones — **never renumber**, because plan.md and tests cite R-numbers.

## 2. Draft

Dispatch the **spec-writer** agent with the slug and the approved intent. It reads the
codebase, chooses a design, and writes `<root>/<slug>/spec.md` as `status: draft`.

Then read the spec yourself. You are accountable for what you present, not the subagent.

## 3. Attack it

Dispatch **intent-interrogator** against the draft spec. Relay the blocking findings in your
own words — its report is not shown to the user.

**Pass it the artifact path and nothing else.** No summary of the conversation, no note about
which parts you think are solid, no account of why a choice was made. Its whole value is that
it was not in the room; every line of context you add hands back the reasoning it exists to be
free of, and a reader who has heard the argument has already been argued with.

## 4. Present for review, honestly

Show the user, in this order:

1. **The design chosen**, one paragraph.
2. **What was rejected and why** — this is what makes the spec reviewable. A reviewer needs to
   see that their preferred approach was considered.
3. **Anything persisted or public** that this commits to. Call it out even if the user did not
   ask; these are the expensive-to-reverse decisions.
4. **Flagged concerns with their owners.** Engineering does not proceed past an unresolved
   flag — name who has to resolve each one.
5. **Where the spec is weakest.** Say it plainly. Directing review effort at the soft parts is
   worth more than a confident summary.

Requirements go in the file, not the chat. Summarise the count and the shape; the user reads
the artifact.

Keep every number and add what it says beside it. A requirement number, invariant label or
concern id on its own tells the reviewer nothing they can decide with, and stripping it out
loses the handle they need to look it up. See `references/plain-language.md`
(the `sdlc-router` skill).

## 5. Gate

Explicit approval, and every flagged concern resolved by its named owner. On approval set
`status: approved` and commit:

```
git add <root>/<slug>/spec.md && git commit -m "spec(<slug>): <one-line design>"
```

Then tell them `/sdlc-core:plan` is next, **and to run it in a fresh session** (`/clear`).
The spec is self-contained by design; carrying the whole design conversation into planning
just fills context with reasoning the artifact already captures. Do not chain into it.
