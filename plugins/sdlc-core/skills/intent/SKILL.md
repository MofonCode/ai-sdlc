---
name: intent
description: Capture an idea as an approved intent.md - tier-route it (trivial/task/feature), interrogate the gaps with the user, attack the draft in fresh context, then commit the artifact. Stage 1 (Plan) of the AI-Native SDLC artifact chain.
argument-hint: A rough description of the idea, or an existing slug to revise
disable-model-invocation: true
---

# /sdlc-core:intent — Stage 1, Plan

Turn a rough idea into a committed `intent.md` that the Design stage can read. Use the
`sdlc-router` skill for tiering rules and artifact locations, and `references/artifact-schemas.md`
for the required shape.

This command runs in the main loop because **it has to ask you questions.** That is most of
its value.

**Resolve the artifact root first.** It defaults to `.sdlc/` but is per-repo, declared in the charter's `artifact_root` (see `sdlc-router`). Everything below writes `<root>/`; never hardcode `.sdlc/`.

## 1. Tier first

Before anything else, classify: `trivial`, `task`, or `feature`. State the tier and why, in
one line.

- **trivial** — stop. Say "this is trivial, no intent needed" and just do the work.
- **task** — stop. Say so, and offer `/sdlc-core:plan` instead. An intent here is pure overhead.
- **feature** — continue.

If it is genuinely between two tiers, ask which, naming the cost difference. Do not silently
upgrade someone into a full chain.

## 2. Orient

Read `<root>/CHARTER.md` if it exists — the solution's invariants and permanent non-goals. You
are looking for whether this idea conflicts with one. If it does, raise it now: that is a
charter amendment conversation, not an intent.

Check `<root>/` for existing slices. If `$ARGUMENTS` names one, you are **revising** — read the
existing intent.md and work from it rather than starting over. Otherwise derive a kebab-case
slug from the idea and confirm it with the user.

## 3. Interrogate

Ask about what is actually missing. Use AskUserQuestion when the answers are choices; ask in
prose when they are open. Do not run through a fixed checklist out loud — pull only on the
threads this particular idea leaves loose.

The things an intent cannot ship without:

- **The observable symptom.** Not "we need X" — what happens today that shouldn't, or doesn't
  happen that should? If the user answers with a solution, ask what problem it solves.
- **Who feels it.** Named users or systems. Grep the codebase for callers rather than asking
  the user to enumerate them.
- **Success criteria you could write a test against.** Push back on anything unfalsifiable.
  "Faster" gets one question: faster than what, measured how, by how much?
- **Non-goals.** Ask directly what this is explicitly *not*. This is the highest-value answer
  in the whole conversation and users rarely volunteer it.
- **Constraints.** Deadline, compliance, compatibility, cost, team.
- **Reversibility.** Does this commit us to a persisted format or public contract?

Batch related questions rather than interrogating one at a time. Stop when the remaining
unknowns are genuinely things the Design stage should decide — an intent is allowed to have
open questions, as long as each names an owner.

## 4. Write it

Write `<root>/<slug>/intent.md` per the schema, `status: draft`. Create the root if needed.

## 5. Attack it

Dispatch the **intent-interrogator** agent against the draft. It arrives with no memory of
your conversation, so it catches what you have already been talked into.

**Pass it the artifact path and nothing else.** No summary of the conversation, no note about
which parts you think are solid, no account of why a choice was made. Its whole value is that
it was not in the room; every line of context you add hands back the reasoning it exists to be
free of, and a reader who has heard the argument has already been argued with.

Relay its findings — the agent's report is not shown to the user, so summarise the blocking
ones in your own words. Fix what is clearly right to fix, and put the rest to the user.

## 6. Gate

Ask for explicit approval. Say what approving commits them to: that the Design stage will
choose a solution to *this* problem, and requirements will be written against *these* success
criteria.

On approval, set `status: approved` and commit the file:

```
git add <root>/<slug>/intent.md && git commit -m "intent(<slug>): <title>"
```

Then tell them `/sdlc-core:spec` is next. Do not run it automatically — the gate is the point.
