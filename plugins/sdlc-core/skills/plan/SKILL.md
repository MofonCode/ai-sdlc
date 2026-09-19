---
name: plan
description: Turn an approved spec.md into an approved plan.md carrying a pairwise-disjoint per-slice file manifest, ready for parallel implementation. Stage 3 (Build planning) of the AI-Native SDLC artifact chain.
argument-hint: Slug of the slice to plan, or a direct description for a task-tier change
disable-model-invocation: true
---

# /sdlc-core:plan — Stage 3, Build (planning half)

Produce the `plan.md` the build fans out from. Schema in `references/artifact-schemas.md`
(the `sdlc-router` skill).

This artifact is **load-bearing, not documentation.** Parallel implementers are dispatched one
per slice straight from its frontmatter manifest.

**Resolve the artifact root first.** It defaults to `.sdlc/` but is per-repo, declared in the charter's `artifact_root` (see `sdlc-router`). Everything below writes `<root>/`; never hardcode `.sdlc/`.

## 1. Establish the input

- **Slug given, spec.md approved** — plan from it.
- **spec.md is `draft`** — stop. Ask for approval first.
- **No spec and tier is `task`** — legitimate. Plan directly from the description in
  `$ARGUMENTS`, still writing `<root>/<slug>/plan.md`. Requirements come from the conversation
  instead of R-numbers; say so in the file.
- **No spec and tier is `feature`** — run `/sdlc-core:spec` first.

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

Write `<root>/<slug>/plan.md`, `status: draft`.

Then **run the manifest check and paste the real output.** Do not assert the manifests are
disjoint — show it:

```
powershell -NoProfile -ExecutionPolicy Bypass -File "${CLAUDE_PLUGIN_ROOT}/scripts/Assert-SliceManifest.ps1" -Plan <root>/<slug>/plan.md
```

It rejects an overlap, an empty manifest, an unknown `depends_on`, and a dependency cycle.
On success it prints the wave plan, which is the thing being approved: the user is agreeing
to a shape of fan-out, not to a claim that one is safe. An overlap is a planning bug — fix
the plan and re-check, never start the build intending to reconcile in the merge.

## 5. Present

- The slices, in dependency order, one line each.
- **What runs in parallel and what is serialised**, explicitly. This is the whole reason the
  manifest exists — the user should see the shape of the fan-out before approving it.
- Risks, each with its early-warning sign.
- The rollback path.
- Requirement coverage: any R-number in the spec that no slice claims is a gap. Say so.

Write all five in plain language, for someone who has never used this process: what each slice
builds, with each requirement number kept and glossed rather than listed bare. See
`references/plain-language.md` (the `sdlc-router`
skill).

## 6. Gate — the one that is not negotiable

Get explicit approval before any code is written. On approval, set `status: approved` and
commit:

```
git add <root>/<slug>/plan.md && git commit -m "plan(<slug>): <n> slices"
```

Then offer the build, **in a fresh session** (`/clear`) — the plan is self-contained and the
planning conversation is dead weight during implementation.

Three ways to run it, in descending order of how much the fan-out buys you:

1. **Agent teams** (`CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1`) — one teammate per slice, each
   owning its manifest. Anthropic's own guidance for teams is *"break the work so each teammate
   owns a different set of files"*, which is exactly what the manifest guarantees. Start with
   3-5 teammates; more scales tokens linearly and coordination worse than linearly.
2. **Workflow script** — `workflows/parallel-build.js`, deterministic waves, re-proves
   disjointness, halts on a red build. Requires the user to opt in per run.
3. **Single writer in one worktree** — always safe, and the *better* choice when the dependency
   graph turned out deep, the slices are small, or the slices touch adjacent code. Say so plainly
   when that is the case rather than fanning out for its own sake.

Anthropic's stated caveat applies and should be repeated to the user when relevant: parallel
implementation carries coordination costs that research and review fan-out does not. Sequential
work, same-file edits, and deep dependency chains are faster with one session.
