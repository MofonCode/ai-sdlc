---
name: sdlc-router
description: Routes a change request to the right amount of SDLC ceremony (trivial / task / feature) and locates the artifact chain under .sdlc/. Use at the start of any non-trivial coding request, when the user says "spec this", "what tier is this", "where are we on <slug>", or when an /intent, /spec, or /plan command runs. Also use before writing code to check whether a plan.md exists for the current work.
user-invocable: false
---

# SDLC router

Decides **how much process a change deserves** and finds the artifact chain. The entire
framework rests on one idea from the AI-Native SDLC Playbook: *every stage commits a
version-controlled artifact the next stage reads.* The chain is the state machine — not the
order the commands were typed.

```
intent.md  ->  spec.md  ->  plan.md  ->  code + tests  ->  verified diff
  Plan          Design       Build         Build            Test
```

Each arrow is a human gate. Artifacts are re-enterable: running `/spec` on an existing
spec.md revises it, it does not start over.

## Tiers — the escape hatch

Ceremony that costs more than it saves gets abandoned, and then nothing is used. Pick a tier
first, say which one out loud, and skip everything below it.

| Tier | Looks like | Required artifacts |
|---|---|---|
| **trivial** | typo, rename, comment, config value, one-line fix, formatting | none — just do it |
| **task** | bounded change in known code; behavior already agreed; < ~3 files | `plan.md` only |
| **feature** | new capability, new module, schema/contract change, anything user-visible, anything hard to reverse | `intent.md` -> `spec.md` -> `plan.md` |

Ambiguous between two tiers? Ask the user which, naming both and the cost difference. Never
silently upgrade someone into a full chain — that is how the framework dies.

Escalate a tier mid-flight when any of these appear, and say why:
- the change touches a data model, a persisted format, or a public contract
- more than one reasonable design exists and they differ in cost
- the blast radius reaches code the user did not mention

## Two scopes: charter and intent

There is **one `.sdlc/CHARTER.md` per repo** and **one `intent.md` per change**. They are not
the same kind of thing and the difference matters.

| | `CHARTER.md` | `<slug>/intent.md` |
|---|---|---|
| Scope | the whole solution | one change |
| Lifetime | years; edited rarely and deliberately | one branch |
| Answers | what this system is, who for, its architectural invariants and permanent non-goals | what is wrong today, what outcome, what success looks like |

The charter is not decoration - **every new intent is checked against it.** An intent that
violates a charter invariant, or lands inside a permanent non-goal, is a blocking finding at
Stage 1, which is where "this feature quietly turns us into a different product" is cheap to
catch. Read `CHARTER.md` at the start of `/intent` and `/spec` when one exists.

If a change genuinely requires breaking a charter invariant, that is a charter amendment with
its own approval - not something an intent may do in passing.

## Locating the chain

Artifacts live at `.sdlc/<slug>/` in the repo, committed on the feature branch, where
`<slug>` is kebab-case derived from the intent title (e.g. `.sdlc/order-book-snapshots/`).
`CHARTER.md` sits at `.sdlc/CHARTER.md`, outside any slug.

To orient at the start of any SDLC command:

1. `ls .sdlc/` — existing slices.
2. If the user named a slice, use it. If exactly one slice has no `plan.md` yet, that is
   probably the active one — confirm, do not assume.
3. `git branch --show-current` — branch names often carry the slug.
4. No `.sdlc/` at all and tier is `feature`? Create the directory as part of `/intent`.

## Gates

Never produce the next artifact without the user approving the current one. Approval is
explicit words from the user, not your own judgment that it looks fine. At each gate, state
what you are about to do and what it commits them to.

The one gate that is *not* negotiable: **do not write implementation code for a `feature`
tier change until a `plan.md` exists and the user has approved it.** The `sdlc-gate` hook
warns when this is violated; the discipline is yours.

## Parallel build safety

This framework fans out multiple implementer agents. That is only safe when slices are
**provably file-disjoint**, so `plan.md` carries a mandatory file manifest per slice and the
build step asserts disjointness before fan-out. Overlapping manifests are a planning bug —
fix the plan, do not reconcile the conflict afterwards.

See `references/artifact-schemas.md` for the exact required shape of each artifact.
