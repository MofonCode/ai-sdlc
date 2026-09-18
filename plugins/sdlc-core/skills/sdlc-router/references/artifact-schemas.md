# Artifact schemas

All artifacts are markdown with YAML frontmatter, committed at `<artifact_root>/<slug>/`.
The root defaults to `.sdlc/` and is declared in the charter's `artifact_root`; resolve it
once per command as `sdlc-router` describes, and never hardcode it.
Frontmatter is the machine-readable part; the body is for humans and for the next agent.

Status values: `draft` | `approved` | `superseded`.
Nothing downstream may be produced from a `draft`.

---

## CHARTER.md — repo scope, long-lived

One per repository, at `<artifact_root>/CHARTER.md`. Not part of the per-change chain: it is the thing
every `intent.md` is checked **against**. Edited rarely and deliberately; amending it is its
own approval, never a side effect of shipping a feature.

```yaml
---
solution: Orders Platform
artifact_root: .sdlc/     # where this repo keeps the chain; every skill reads this
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
  reviewer could catch a violation. These are the load-bearing section. They are always
  domain-specific; the useful ones read like: no computation reads data unavailable at its own
  timestamp; stored events are append-only; a persisted value is never overwritten by a later
  correction; every derived result is reproducible from persisted inputs alone; no tenant can
  observe another tenant's rows.
- **Permanent non-goals** — what this system will never be. An intent landing here is blocked,
  not negotiated.
- **Technology commitments** — the stack decisions already made and not up for relitigation per
  feature, each with a one-line reason. `spec-writer` reads this section **before** the codebase
  and treats it as binding, so it is how you stop every spec re-deciding the same things.
  Record language and version, framework, test runner, persistence, and anything else a spec
  would otherwise have to guess. Where conventions span projects, name the skill that carries
  them (for example `conventions: the stack-<name> skill`) rather than restating them here.
  A spec that needs a commitment changed raises it as a flagged concern; changing one is a
  charter amendment.
- **Amendment log** — what changed, when, why. An invariant that was removed matters more than
  one that was added.

Gate: the solution owner approves. `intent-interrogator` reads this file and treats a conflict
with an invariant or a permanent non-goal as a **blocking** finding.

---

## intent.md — Stage: Plan

Captures the problem *before* anyone has decided on a solution. Written by the `/sdlc-core:intent`
skill and then attacked by `intent-interrogator`, which reports weaknesses and never writes. A good intent.md contains no implementation.

```yaml
---
slug: audit-log-retention
title: Retain a queryable audit log
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

Gate: the requester approves before `/sdlc-core:spec` runs.

---

## spec.md — Stage: Design

Requirements and design. Written by `spec-writer`, reading an **approved** intent.md.

```yaml
---
slug: audit-log-retention
status: draft
intent: ./intent.md
tier: feature
---
```

Required sections:

- **Requirements** — numbered `R1, R2, ...` so plan.md and tests can cite them. Each is testable,
  and each is written in **EARS** form (see below).
- **Design** — the chosen approach, and the alternatives rejected with the reason.
- **Data and contracts** — schemas, message shapes, API surface, migration implications. Call out anything persisted or public.
- **Failure modes** — what breaks, what the system does about it.
- **Flagged concerns** — security, compliance, cost, privacy. Each flag names the policy owner who must resolve it. Engineering does not proceed past an unresolved flag.
- **Test strategy** — how each requirement gets verified.
- **Out of scope** — carried forward from intent Non-goals, plus anything discovered during design.
- **End-to-end verification** — the single check that proves the whole feature works, start to
  finish. A spec without one has no definition of done.

A spec is *self-contained* when it names the files and interfaces involved, states what is out
of scope, and ends with that end-to-end verification step. Aim for a spec a fresh session could
implement from with no other context, because that is exactly what will happen.

### EARS requirement syntax

Write every requirement in one of these five forms. They force the trigger, condition, or state
to be explicit, which is what makes a requirement testable and what kills the "the system should
handle errors gracefully" class of non-requirement.

| Pattern | Template | Use for |
|---|---|---|
| Ubiquitous | THE `<system>` SHALL `<response>` | always-true properties |
| Event-driven | WHEN `<trigger>`, THE `<system>` SHALL `<response>` | responses to an event |
| State-driven | WHILE `<state>`, THE `<system>` SHALL `<response>` | behavior during a state |
| Unwanted behavior | IF `<trigger>`, THEN THE `<system>` SHALL `<response>` | errors and failure handling |
| Optional feature | WHERE `<feature is included>`, THE `<system>` SHALL `<response>` | conditional capabilities |

```
R1. WHEN a record is modified, THE audit writer SHALL persist the prior value with the
    timestamp of the modification.
R2. IF an entry arrives with a sequence number not exactly one greater than the last persisted
    sequence number, THEN THE audit writer SHALL reject it and raise a gap event.
R3. WHILE a retention purge is running, THE audit store SHALL reject writes.
```

Each maps to a test with no translation step. That is the whole point.

Gate: requester approves; flagged concerns resolved with their named owners.

---

## plan.md — Stage: Build

The implementation plan. This is the artifact the build fans out from, so its manifest is
load-bearing, not documentation.

```yaml
---
slug: audit-log-retention
status: draft
spec: ./spec.md
tier: feature
slices:
  - id: storage
    files: ["src/Storage/AuditWriter.cs", "src/Storage/IAuditStore.cs", "tests/Storage/AuditWriterTests.cs"]
    requirements: [R1, R2]
    depends_on: []
  - id: api
    files: ["src/Api/AuditEndpoint.cs", "tests/Api/AuditEndpointTests.cs"]
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

## verification.md — Stage: Test

The record of whether the change actually does what the spec said. Written by the
`/sdlc-core:verify` skill from the `verifier` agent's fresh-context report.

This artifact is what makes verification a stage rather than a chat message. A retrospective
reads it, the next eval case is written from it, and six weeks later it is the only thing that
answers "was that requirement ever actually tested".

```yaml
---
slug: audit-log-retention
status: draft            # approved once the verdict is acted on
spec: ./spec.md
plan: ./plan.md
verdict: ship            # ship | do-not-ship
verified: 2026-09-17
requirements_total: 7
requirements_tested: 6
---
```

Required sections:

- **Verdict** — SHIP or DO NOT SHIP, first line, with the one-sentence reason.
- **Blocking findings** — each with a concrete failure scenario: specific inputs or state
  leading to a specific wrong outcome. A finding with no scenario belongs under *Should fix*.
- **Requirement coverage** — a row per R-number: implemented, tested, note. A requirement that
  is implemented but untested is the gap that survives to production; say so per row rather
  than in a total.
- **Manifest drift** — `git diff --name-only` against the plan's `files` lists. Files changed
  that no slice claimed are unplanned scope; files claimed but untouched mean a slice did not
  land.
- **Build and test output** — the real output, including failures. Not a summary of it. A
  verification that reports success when tests failed is worse than no verification, because
  it converts an unknown into a false known.
- **Not checked** — what the verifier could not reach, and why. Silence here reads as coverage.

Gate: **DO NOT SHIP stops the change.** Fix the findings and re-verify with a fresh dispatch,
never by asking the same agent to reconsider. A verdict that can be talked down is not a gate.

---

## Disjointness check

Before fanning out, verify:

```bash
# Substitute the artifact root and slug; the root is resolved per sdlc-router.
python - <<'CHECK'
import itertools, pathlib, re, sys

PLAN = pathlib.Path(".sdlc/<slug>/plan.md")          # <- substitute root and slug
parts = PLAN.read_text(encoding="utf-8").split("---")
if len(parts) < 3:
    sys.exit("plan.md has no frontmatter - the slice manifest is mandatory")

try:
    import yaml
    slices = yaml.safe_load(parts[1])["slices"]
except ImportError:
    # PyYAML is not guaranteed to be installed and this check must still run. Same
    # fallback the gate hook uses, deliberately, so the two parsers never disagree.
    slices, cur = [], None
    for line in parts[1].splitlines():
        if re.match(r"\s*-\s+id:", line):
            cur = {"id": line.split("id:", 1)[1].strip(), "files": []}
            slices.append(cur)
        elif "files:" in line and cur is not None:
            cur["files"] = re.findall(r'''["']([^"']+)["']''', line)

bad = [(a["id"], b["id"], sorted(set(a["files"]) & set(b["files"])))
       for a, b in itertools.combinations(slices, 2) if set(a["files"]) & set(b["files"])]
if bad:
    for x, y, overlap in bad:
        print(f"OVERLAP {x} <-> {y}: {overlap}")
    sys.exit(1)

empty = [s["id"] for s in slices if not s["files"]]
if empty:
    sys.exit(f"empty manifest: {empty} - a slice that owns no files is not a slice")

print(f"{len(slices)} slices, {sum(len(s['files']) for s in slices)} files, manifests disjoint")
CHECK
```

A failure here is a **planning** bug. Fix plan.md and re-approve; do not start the build and
sort it out in the merge.
