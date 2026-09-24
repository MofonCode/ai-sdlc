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
last commit   24329cf
verify        dotnet test   → expect 139 passed, 0 failed
```

**Next task:** the chain is complete. Items 1–10 are done, which is the definition of
*enough* below — so the next move is a **decision**, not a task: either run a real cycle
end to end, or continue with items 11–15.

If `dotnet test` does not report 139 passing, stop and diagnose before building anything:
the test suite is the only thing standing between "this looks right" and "this works".

---

## The goal, and when to stop building

> **Enough = a complete chain from a hand-written intent to a landed retro.**

**Reached 2026-09-24.** Items 1–10 are done, so running a real intent now produces
evidence about the **process** rather than about missing parts.

Items 11–15 are conveniences and distribution. None is needed to prove the chain works.

---

## Discipline — agreed, and load-bearing

1. **The whole first draft is disposable.** Written to be rewritten. Do not polish;
   polishing untested design is expensive and teaches nothing.

2. **Bootstrapping is exempt from the process.** Items 1–10 were built without running
   cycles, deliberately. That exemption **has now expired**: the chain exists, so work
   from here goes through it.

3. **Exercise each component against real input as it lands.** This is *not* the same as
   running the system, and it is where the evidence comes from. It produced eight design
   corrections across items 1–10 — see the log.

4. ~~Do not run a cycle before item 10.~~ **Satisfied.**

5. **No health-checking layer yet.** It would be justified by reasoning rather than
   evidence. Revisit after a real cycle has landed.

6. **One commit per item**, squashed, so each is reviewable alone and `git revert` of one
   item is one operation.

7. **Prefer false negatives in any mechanical check.** A check that reports healthy
   artifacts as broken gets switched off, which is the INV-06 erosion failure one layer
   up. This rule earned itself four times over — see the log.

8. **A construct check reads comment-stripped code.** Grepping source text for a
   forbidden construct also matches the comment explaining that it is forbidden. Four
   separate instances; it is now written into both test helpers.

---

## Tasks

### Done — the chain

| # | Item | Commit | Proven by |
|---|---|---|---|
| 1 | Artifact model — layout, schemas, test project | `f171fb0` | mutation-tested: neutering rule 1 fails only rule 1's test |
| 2 | Fact-gathering — `Get-SdlcFacts.ps1` | `8327aea` | drives the real `.ps1` in a throwaway git repo |
| 3 | Interrogator — `interrogate.js` | `28fe481` | enum-drift check mutation-tested |
| 4 | Repair loop — `repair.js` | `edbc126` | no-progress termination, escalation, introduced-findings |
| 5 | Spec stage — `spec.js` + schema | `e599106` | criteria parsed with verifiable flags; duplicate ids caught |
| 6 | Plan stage — `plan.js` + schema + tier | `192cae6` | tier 1 and tier 3 both asserted against real fixtures |
| 7 | Build stage — `build.js` | `f27c3af` | tier escalation halts; adherence computed from the diff |
| 8 | Verify stage — `verify.js` + schema | `4eb34fa` | traceability generated from annotations; outcome decided in code |
| 9 | Retro emission — `retro.js` + schema | `5f5b56e` | refuses to certify a cycle that did not pass |
| 10 | Dispatch and landing — three `.ps1` | `24329cf` | seven states asserted against a real git fixture |

### Remaining — conveniences and distribution

| # | Item | Note |
|---|---|---|
| 11 | `intent` skill — the interview | A convenience, not a dependency: an intent was hand-written successfully |
| 12 | Retro absorption into ai-sdlc | Needs retros to exist |
| 13 | Packaging — manifest, marketplace, CI semver check | Answers the one question outside our control: does a marketplace install authenticate against a private remote |
| 14 | `dotnet-library` pack | The first real client is `MofonCode.Core`, which is a library |
| 15 | `init` | Cannot precede 14 — it scaffolds *from* a pack |

---

## Small, cheap, still outstanding

| Task | Why |
|---|---|
| **Delete the charter's coverage table** | A live INV-03 violation, drifted three ways: INV-08 absent, no `pack` column though INV-06 needs one, and 5 columns against the schema's 8 artifact kinds. Derived data should be derived — the fix is deletion, not a check |

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
| Interrogator roles as charter data | when a retro has enough evidence to drop a lens |

---

## Known gaps, stated rather than buried

- **Nothing proves the workflow scripts are syntactically valid JavaScript.** Node was
  deliberately dropped as a dependency and adding a JS engine to lint six files is not
  worth it, so a syntax error surfaces at first launch rather than at build. A real cost
  of the Node decision, not a defect in the tests. The PowerShell side has no equivalent
  gap — those are parsed by the real 5.1 parser in the suite.
- **No workflow has ever been executed.** Every workflow test is structural. The scripts
  are checked for shape, forbidden constructs and enum drift; none has run an agent.
- **A locked worktree cannot be faked in a test**, so `in-progress` is the one lifecycle
  state asserted only by inspection of the derivation rather than end to end.
- **Interrogator roles are hardcoded in `interrogate.js`** rather than read from the
  charter — see Deferred.
- **`master` is still at the initial commit.** Everything lives on `bootstrap`. Nothing
  has been pushed.
- **Three actions pending:** confirm `github.com/MofonCode/ai-sdlc` is private before
  moving anything there; confirm the arriving teammates have GitHub org access; decide
  whether `MarketSense` stays on Azure DevOps.

---

## Log

Newest first. The **corrections** column is the point: each came from running a component
against real input, not from reading the design.

| Commit | Item | Correction it produced |
|---|---|---|
| `24329cf` | 10 · dispatch and landing | **No script had a UTF-8 BOM**, and 5.1 reads a BOM-less file as ANSI — one em-dash inside a string decoded to a smart quote and terminated it early. Also: `-AsHashtable` matched its own "avoid this" comment |
| `5f5b56e` | 9 · retro | `Date.now()` in a comment explaining its absence tripped the check forbidding it |
| `4eb34fa` | 8 · verify | The suppression-justification heuristic reported **this repository's own justified suppression** as unjustified: it looked for a comment *start*, and the justification is a 12-line XML comment. Also: a literal ellipsis read as a spread |
| `f27c3af` | 7 · build | — |
| `192cae6` | 6 · plan | **A PowerShell function returning a single-element collection unwraps it**, so a one-item result arrived as a bare object and `.Count` threw. Five extractors had carried this since item 2 and 70 tests never saw it |
| `e599106` | 5 · spec | — |
| `edbc126` | 4 · repair | — |
| `28fe481` | 3 · interrogator | Mechanical findings need no agent at all: a failed check *is* the citation |
| `8327aea` | 2 · fact-gathering | Checks narrowed to `.sdlc/` after the design record produced 11 false positives, every one correct as written. Path heuristic tightened. **Struck-through ids read as declared**, so a finding citing a removed decision would have passed |
| `f171fb0` | 1 · artifact model | — |
| `edcff2c` | — | Seven contradictions resolved; charter restored; its register was stale in **5 of 8** entries |
| `2074e93` | — | Every decision folded into the design record; Appendix A carries the first charter |
| `ef99ec8` | — | Froze the concept as `concept.raw.md` |
