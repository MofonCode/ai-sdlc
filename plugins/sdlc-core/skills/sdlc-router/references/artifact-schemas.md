# Artifact schemas

All artifacts are markdown with YAML frontmatter, committed at `.sdlc/<slug>/`.
Frontmatter is the machine-readable part; the body is for humans and for the next agent.

Status values: `draft` | `approved` | `superseded`.
Nothing downstream may be produced from a `draft`.

---

## CHARTER.md — repo scope, long-lived

One per repository, at `.sdlc/CHARTER.md`. Not part of the per-change chain: it is the thing
every `intent.md` is checked **against**. Edited rarely and deliberately; amending it is its
own approval, never a side effect of shipping a feature.

```yaml
---
solution: MarketSense
status: approved
version: 1
updated: 2026-09-16
owner: peter
---
```

Required sections:

- **What this is** — the system in three sentences, written for someone who has never seen it.
- **Who it is for** — the actual users and the job they hire it for.
- **Architectural invariants** — the properties that must hold across every change, stated so a
  reviewer could catch a violation. These are the load-bearing section. For a market-data or
  trading system they typically include things like: no computation reads data unavailable at
  its own timestamp; stored market data is append-only; point-in-time values are never
  overwritten by restatements; every signal is reproducible from persisted inputs alone.
- **Permanent non-goals** — what this system will never be. An intent landing here is blocked,
  not negotiated.
- **Technology commitments** — the stack decisions already made and not up for relitigation per
  feature, each with a one-line reason.
- **Amendment log** — what changed, when, why. An invariant that was removed matters more than
  one that was added.

Gate: the solution owner approves. `intent-interrogator` reads this file and treats a conflict
with an invariant or a permanent non-goal as a **blocking** finding.

---

## intent.md — Stage: Plan

Captures the problem *before* anyone has decided on a solution. Written by
`intent-interrogator`. A good intent.md contains no implementation.

```yaml
---
slug: order-book-snapshots
title: Persist order book snapshots for replay
status: draft
tier: feature
created: 2026-09-16
owner: peter
---
```

Required sections:

- **Problem** — what is wrong today, stated observably. Not "we need X" but "when Y happens, Z."
- **Affected users and systems** — who feels it, which components are touched.
- **Proposed outcome** — the observable end state. Still not a design.
- **Success criteria** — how we will know it worked. Measurable or falsifiable, or it is not a criterion.
- **Constraints** — deadlines, compliance, compute, compatibility, team.
- **Non-goals** — explicitly out of scope. The most valuable section; it is what stops scope creep at build time.
- **Open questions** — with an owner named for each.

Gate: the requester approves before `/spec` runs.

---

## spec.md — Stage: Design

Requirements and design. Written by `spec-writer`, reading an **approved** intent.md.

```yaml
---
slug: order-book-snapshots
status: draft
intent: ./intent.md
tier: feature
---
```

Required sections:

- **Requirements** — numbered `R1, R2, ...` so plan.md and tests can cite them. Each is testable.
- **Design** — the chosen approach, and the alternatives rejected with the reason.
- **Data and contracts** — schemas, message shapes, API surface, migration implications. Call out anything persisted or public.
- **Failure modes** — what breaks, what the system does about it.
- **Flagged concerns** — security, compliance, cost, privacy. Each flag names the policy owner who must resolve it. Engineering does not proceed past an unresolved flag.
- **Test strategy** — how each requirement gets verified.
- **Out of scope** — carried forward from intent Non-goals, plus anything discovered during design.

Gate: requester approves; flagged concerns resolved with their named owners.

---

## plan.md — Stage: Build

The implementation plan. This is the artifact the build fans out from, so its manifest is
load-bearing, not documentation.

```yaml
---
slug: order-book-snapshots
status: draft
spec: ./spec.md
tier: feature
slices:
  - id: storage
    files: ["src/MarketSense.Storage/SnapshotWriter.cs", "src/MarketSense.Storage/ISnapshotStore.cs"]
    requirements: [R1, R2]
    depends_on: []
  - id: api
    files: ["src/MarketSense.Api/SnapshotController.cs"]
    requirements: [R4]
    depends_on: [storage]
---
```

**The `slices` block is mandatory and its `files` lists must be pairwise disjoint.**
Parallel implementers are dispatched one per slice; two agents holding the same file is a
merge conflict by construction. If two slices genuinely need the same file, they are one
slice — merge them, or extract the shared part into a third slice that both `depends_on`.

`depends_on` serialises what cannot be parallel. Slices with no unmet dependency run
concurrently; the rest wait.

Required sections:

- **Work order** — the slices in dependency order, with what each one does.
- **Files** — every file created or modified, matching the frontmatter manifest exactly.
- **Tests** — what proves each requirement. Name the test files; they belong to a slice's manifest too.
- **Risks** — what could go wrong during implementation, and the early-warning sign for each.
- **Rollback** — how to undo this if it ships badly.

Gate: engineer approves the plan before any code is written.

---

## Disjointness check

Before fanning out, verify:

```bash
python - <<'PY'
import sys, itertools, yaml, pathlib
fm = pathlib.Path(".sdlc/<slug>/plan.md").read_text().split("---")[1]
slices = yaml.safe_load(fm)["slices"]
bad = [(a["id"], b["id"], sorted(set(a["files"]) & set(b["files"])))
       for a, b in itertools.combinations(slices, 2) if set(a["files"]) & set(b["files"])]
if bad:
    for x, y, f in bad: print(f"OVERLAP {x} <-> {y}: {f}")
    sys.exit(1)
print(f"{len(slices)} slices, manifests disjoint")
PY
```

A failure here is a **planning** bug. Fix plan.md and re-approve; do not start the build and
sort it out in the merge.
