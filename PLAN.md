# Plan — building ai-sdlc v02

    Status:  LIVE. Updated as each task completes.
    Branch:  bootstrap
    Updated: 2026-09-24

## What this document is authoritative for

Three documents, three jobs. They do not overlap, and where they appear to, this list
decides:

| Document | Authority for | Mutable? |
|---|---|---|
| **`PLAN.md`** (this) | **what remains, and what is done** | yes — updated per task |
| `concept.raw.md` | **why** — every decision and the reasoning behind it | no, frozen |
| `.sdlc/CHARTER.md` | **what the work must obey** | only by approved charter intent |

`concept.raw.md` is frozen, so it cannot carry a corrected task list — its split says
eleven items and the real number is fifteen (it omitted the four middle stage skills).
**This document's list wins.** The concept stays the authority for reasoning.

---

## Recovering after a session break

Read this section first. It is the whole handoff.

```
branch        bootstrap
last commit   7af6d90
verify        dotnet test   → expect 42 passed, 0 failed
```

**Next task:** item 4 — the repair loop. Interrogation already emits findings and there
is nowhere for them to go, which makes it the most immediate hole.

If `dotnet test` does not report 42 passing, stop and diagnose before building anything:
the test suite is the only thing standing between "this looks right" and "this works".

---

## The goal, and when to stop building

> **Enough = a complete chain from a hand-written intent to a landed retro.**

That is items 1–10. At that point, running a real intent produces evidence about the
**process** rather than about missing parts — which is the only reason to run one.

Items 11–15 are conveniences and distribution. None is needed to prove the chain works.

---

## Discipline — agreed, and load-bearing

1. **The whole first draft is disposable.** Written to be rewritten. Do not polish;
   polishing untested design is expensive and teaches nothing.

2. **Bootstrapping is exempt from the process.** No self-hosting system's first version
   is built with itself. Do not hand-run cycles to build the thing that automates
   cycles — measured cost: one hand-run interrogation of one intent produced four
   blocking findings whose repairs introduced three more, with no tooling involved.

3. **Exercise each component against real input as it lands.** This is *not* the same as
   running the system, and it is where the evidence comes from. Items 1–3 produced three
   design corrections this way:
   - running the gatherer on the design record narrowed checks to `.sdlc/` (11 false
     positives otherwise)
   - running it on the charter produced the strikethrough convention, after removed
     decisions read as declared
   - running it at all fixed the path heuristic, after commands and artifact-kind names
     resolved as paths

4. **Do not run a cycle before item 10.** Findings against an incomplete system are about
   absence, not design. The charter demands verify gates, traceability and dispositions
   that have no implementation yet, so every measurement would fail for the same
   uninteresting reason.

5. **No health-checking layer yet.** It would be justified by reasoning rather than
   evidence — building the observer before the observed has run once. Revisit after a real
   cycle has landed.

6. **One commit per item**, squashed, so each is reviewable alone and `git revert` of one
   item is one operation.

7. **Prefer false negatives in any mechanical check.** A check that reports healthy
   artifacts as broken gets switched off, which is the INV-06 erosion failure one layer up.

---

## Tasks

### Done

| # | Item | Commit | Proven by |
|---|---|---|---|
| 1 | Artifact model — layout, schemas, test project | `f171fb0` | 24 tests; mutation-tested (neutering rule 1 fails only rule 1's test) |
| 2 | Fact-gathering — `tools/Get-SdlcFacts.ps1` | `8327aea` | 8 tests driving the real `.ps1` in a throwaway git repo |
| 3 | Interrogator — `workflows/interrogate.js` | `28fe481` | 10 tests; enum-drift check mutation-tested |

### Remaining — the chain (items 4–10)

| # | Item | Done when |
|---|---|---|
| 4 | **Repair loop** | A blocking finding can be repaired, the repair *diff* re-interrogated against the finding it claims to fix, and the loop terminates on **no-progress** rather than on "clean" |
| 5 | **`spec` skill** | Produces `spec.md` with acceptance criteria as `AC-<cycle-id>-<n>`, each marked verifiable or not. Needs a spec schema |
| 6 | **`plan` skill** | Produces `plan.md` that **enumerates the files it will touch** (INV-05), which is also what the blast-radius tier reads |
| 7 | **`build` skill** | Follows the plan. Halts if the actual tier exceeds the predicted tier |
| 8 | **`verify` skill** | Produces `verify.md`: traceability, plan adherence, tool gates, dispositions. Suppression count tracked, an increase is a finding |
| 9 | **Retro emission** | Produces `retro.md` with its five frontmatter fields, carrying findings and dispositions for the corpus |
| 10 | **Dispatch and landing** | `/sdlc:run` with the confirmation; worktree per cycle; rebase → re-verify → merge, serialized |

### Remaining — conveniences and distribution (items 11–15)

| # | Item | Note |
|---|---|---|
| 11 | `intent` skill — the interview | A convenience, not a dependency: an intent was hand-written successfully |
| 12 | Retro absorption into ai-sdlc | Needs retros to exist |
| 13 | Packaging — manifest, marketplace, CI semver check | Answers the one question outside our control: does a marketplace install authenticate against a private remote |
| 14 | `dotnet-library` pack | The first real client is `MofonCode.Core`, which is a library |
| 15 | `init` | Cannot precede 14 — it scaffolds *from* a pack |

---

## Small, cheap, do alongside

| Task | Why now |
|---|---|
| **Delete the charter's coverage table** | A live INV-03 violation, already drifted three ways: INV-08 absent, no `pack` column though INV-06 needs one, and 5 columns against the schema's 8 artifact kinds. Derived data should be derived — the fix is deletion, not a check |

---

## Deferred, with triggers recorded

| Deferred | Trigger |
|---|---|
| Health-checking layer | after a real cycle has landed |
| Corpus health query | when there are retros to aggregate |
| Repo-scope interrogation | when skills exist, so "a `/sdlc:` command with no skill" is checkable |
| A living solution-level spec | when someone asks what the solution does and reading the code is too slow |
| Artifact directory archiving | when `intents/` passes ~50 files |
| Charter structural checks (`Forbids:` present, `Applies-to` valid) | with the health layer — the `form` rule is prose today |

---

## Known gaps, stated rather than buried

- **Nothing proves `workflows/interrogate.js` is syntactically valid JavaScript.** Node was
  deliberately dropped as a dependency and adding a JS engine to lint one file is not
  worth it, so a syntax error surfaces at first launch rather than at build. A real cost
  of the Node decision, not a defect in the tests.
- **Interrogator roles are hardcoded in the workflow script.** They belong in the charter
  as data, so a retro can eventually say "the gaps role produced two findings, both
  rejected" and drop a lens on evidence. Reading them out of the charter needs a parser
  the gatherer does not have.
- **Three checks on the charter's own structure are prose, not code** — see Deferred.
- **`master` is still at the initial commit.** Everything lives on `bootstrap`, 13 commits
  ahead. Nothing has been pushed.
- **Three actions pending, unblocked by any of the above:** confirm
  `github.com/MofonCode/ai-sdlc` is private before moving anything there; confirm the
  arriving teammates have GitHub org access; decide whether `MarketSense` stays on Azure
  DevOps.

---

## Log

Newest first. One line per completed task, with the commit that carries it.

| Date | Commit | What |
|---|---|---|
| 2026-09-24 | `28fe481` | Item 3 — interrogator. Mechanical findings need no agent; roles cannot set state |
| 2026-09-24 | `8327aea` | Item 2 — fact-gathering. Three design corrections from running it |
| 2026-09-24 | `f171fb0` | Item 1 — artifact model. Five schema rules enforced, mutation-verified |
| 2026-09-24 | `edcff2c` | Resolved seven contradictions; restored the charter; register was stale in 5 of 8 entries |
| 2026-09-24 | `2074e93` | Folded every decision into the design record; Appendix A carries the first charter |
| 2026-09-23 | `ef99ec8` | Froze the concept as `concept.raw.md` |
