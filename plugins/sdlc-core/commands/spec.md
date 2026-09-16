---
description: Turn an approved intent.md into an approved spec.md — requirements, chosen design, rejected alternatives, flagged concerns
argument-hint: Slug of the slice to spec, or blank to infer from .sdlc/
---

# /spec — Stage 2, Design

Produce a `spec.md` an engineer could plan from. Schema in `references/artifact-schemas.md`
(the `sdlc-router` skill).

## 1. Check the gate behind you

Read `.sdlc/<slug>/intent.md`.

- **No intent.md** — if the tier is `feature`, run `/intent` first; say so and stop. If the
  user insists there is no intent because this is a `task`, offer `/plan` instead.
- **`status: draft`** — the intent is not approved. Stop and ask for approval. Producing a
  spec from an unapproved intent means designing a solution to a problem nobody agreed on.
- **`status: approved`** — proceed.

Revising an existing spec? Read it first and amend. Append new requirements with fresh numbers
and mark superseded ones — **never renumber**, because plan.md and tests cite R-numbers.

## 2. Draft

Dispatch the **spec-writer** agent with the slug and the approved intent. It reads the
codebase, chooses a design, and writes `.sdlc/<slug>/spec.md` as `status: draft`.

Then read the spec yourself. You are accountable for what you present, not the subagent.

## 3. Attack it

Dispatch **intent-interrogator** against the draft spec. Relay the blocking findings in your
own words — its report is not shown to the user.

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

## 5. Gate

Explicit approval, and every flagged concern resolved by its named owner. On approval set
`status: approved` and commit:

```
git add .sdlc/<slug>/spec.md && git commit -m "spec(<slug>): <one-line design>"
```

Then tell them `/plan` is next. Do not chain into it.
