---
description: Turn an approved spec.md into an approved plan.md with a file-disjoint slice manifest ready for parallel implementation
argument-hint: Slug of the slice to plan, or a direct description for a task-tier change
---

# /plan — Stage 3, Build (planning half)

Produce the `plan.md` the build fans out from. Schema in `references/artifact-schemas.md`
(the `sdlc-router` skill).

This artifact is **load-bearing, not documentation.** Parallel implementers are dispatched one
per slice straight from its frontmatter manifest.

## 1. Establish the input

- **Slug given, spec.md approved** — plan from it.
- **spec.md is `draft`** — stop. Ask for approval first.
- **No spec and tier is `task`** — legitimate. Plan directly from the description in
  `$ARGUMENTS`, still writing `.sdlc/<slug>/plan.md`. Requirements come from the conversation
  instead of R-numbers; say so in the file.
- **No spec and tier is `feature`** — run `/spec` first.

## 2. Read the code before planning it

Dispatch **code-explorer** (from the `feature-dev` plugin) if the area is unfamiliar. A plan
that names files which do not exist, or misses the file that actually has to change, wastes
the entire build.

## 3. Cut the slices — the part that matters

Decompose into slices that can be built **independently and in parallel**. For each: an id,
the exact files it creates or modifies, the requirements it satisfies, and what it depends on.

**Every file belongs to exactly one slice.** Two implementers holding the same file is a merge
conflict by construction, not bad luck. If two slices need the same file, they are one slice —
merge them, or extract the shared part into a third slice both `depends_on`.

Test files are files. They go in a slice's manifest too.

Use `depends_on` for what genuinely cannot be concurrent — an interface another slice
implements against, a migration another slice writes through. Everything with no unmet
dependency runs at the same time, so keep the dependency graph shallow: a chain of five slices
is a sequential build wearing a parallel costume.

Good slice boundaries usually follow: layer (storage / domain / api), or independent vertical
features, or a shared-contract slice followed by N implementers. Bad ones cut across a single
class or a single file.

## 4. Write it and prove the manifest

Write `.sdlc/<slug>/plan.md`, `status: draft`.

Then **run the disjointness check** in `references/artifact-schemas.md` and paste the real
output. Do not assert the manifests are disjoint — show it. An overlap is a planning bug: fix
the plan and re-check, never start the build intending to reconcile in the merge.

## 5. Present

- The slices, in dependency order, one line each.
- **What runs in parallel and what is serialised**, explicitly. This is the whole reason the
  manifest exists — the user should see the shape of the fan-out before approving it.
- Risks, each with its early-warning sign.
- The rollback path.
- Requirement coverage: any R-number in the spec that no slice claims is a gap. Say so.

## 6. Gate — the one that is not negotiable

Get explicit approval before any code is written. On approval, set `status: approved` and
commit:

```
git add .sdlc/<slug>/plan.md && git commit -m "plan(<slug>): <n> slices"
```

Then offer the build. Parallel implementation runs through a Workflow script — see
`workflows/parallel-build.js` — which requires the user to opt in per run. A single-writer
build in one worktree is always the safe fallback, and is the better choice when the
dependency graph turned out deep or the slices are small.
