---
name: sdlc-router
description: Routes a change request to the right amount of SDLC ceremony (trivial / task / feature) and locates the artifact chain under .sdlc/. Use at the start of any non-trivial coding request, when the user says "spec this", "what tier is this", "where are we on <slug>", or when an /sdlc-core:intent, /sdlc-core:spec, or /sdlc-core:plan skill runs. Also use before writing code to check whether a plan.md exists for the current work.
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

Each arrow is a human gate. Artifacts are re-enterable: running `/sdlc-core:spec` on an existing
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
catch. Read `CHARTER.md` at the start of `/sdlc-core:intent` and `/sdlc-core:spec` when one exists.

If a change genuinely requires breaking a charter invariant, that is a charter amendment with
its own approval - not something an intent may do in passing.

(GitHub Spec Kit calls this same repo-scope artifact a *constitution*. Same idea, different
name: the durable rules a per-change spec is checked against.)

## Locating the chain

Artifacts live at `<artifact-root>/<slug>/`, committed on the feature branch, where `<slug>` is
kebab-case derived from the intent title. `CHARTER.md` sits at `<artifact-root>/CHARTER.md`,
outside any slug.

**The artifact root defaults to `.sdlc/` but is per-repo.** Find it before anything else:

```bash
ls .sdlc/CHARTER.md 2>/dev/null || ls */CHARTER.md 2>/dev/null
```

The charter's frontmatter declares its own root in `artifact_root`. A repo that keeps its
backlog visible at top level (`intent/`, `specs/`, `docs/sdlc/`) is making a deliberate choice —
respect it, and never create a second root alongside one that already exists.

To orient at the start of any SDLC command:

1. `ls .sdlc/` — existing slices.
2. If the user named a slice, use it. If exactly one slice has no `plan.md` yet, that is
   probably the active one — confirm, do not assume.
3. `git branch --show-current` — branch names often carry the slug.
4. No `.sdlc/` at all and tier is `feature`? Create the directory as part of `/sdlc-core:intent`.

## Gates

Never produce the next artifact without the user approving the current one. Approval is
explicit words from the user, not your own judgment that it looks fine. At each gate, state
what you are about to do and what it commits them to.

The one gate that is *not* negotiable: **do not write implementation code for a `feature`
tier change until a `plan.md` exists and the user has approved it.** The `sdlc-gate` hook
warns when this is violated; the discipline is yours.

### Validation before every commit

**Nothing is committed until the build is green and the tests pass — both the unit suite and
the integration suite — and the real output is pasted.** Not a summary of it, not an assurance
that it passed: the output.

This binds code changes and artifact commits alike, because an artifact that describes a
broken build is a lie the next stage will act on. It applies at every tier, `trivial`
included: a one-line change that breaks the build is still a broken build, and the cost of
checking is one command.

Three failure modes this is aimed at, all of which look like success:

- **Reporting a pass without running anything.** If no command was run, say so plainly rather
  than inferring the result from the size of the change.
- **Running the unit suite only.** Unit tests pass while the composed system is broken —
  that is most of what integration tests exist to catch, so "tests pass" means both.
- **Reading the exit code and not the output.** A suite that silently collected zero tests
  exits clean. Check what actually ran, not just that something did.

If the build or a test is already red before your change, say so before touching anything.
Inheriting a red build and then reporting your own change as verified attributes someone
else's breakage to you, and hides theirs.

The **commands** are the stack pack's business, not this skill's — read the committed pack for
the exact invocations. This skill states only that they must be run and seen to pass.

## Parallel build safety

This framework fans out multiple implementer agents. That is only safe when slices are
**provably file-disjoint**, so `plan.md` carries a mandatory file manifest per slice and the
build step asserts disjointness before fan-out. Overlapping manifests are a planning bug —
fix the plan, do not reconcile the conflict afterwards.

See `references/artifact-schemas.md` for the exact required shape of each artifact.
