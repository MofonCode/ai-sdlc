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
last commit   f10a659
verify        dotnet test   → expect 147 passed, 0 failed
plugin        claude plugin validate . --strict   → expect "Validation passed"
```

**Next task:** the chain is complete *and invocable*. The next move is a **decision**, not
a task: run `interrogate` on the charter — the first real workflow execution, and the
first evidence about the process rather than about missing parts — or continue with
items 11, 12, 14, 15.

If `dotnet test` does not report 147 passing, stop and diagnose before building anything:
the test suite is the only thing standing between "this looks right" and "this works".

---

## The first interrogation — charter v1, round 1

Run 2026-09-24 against `.sdlc/CHARTER.md`. **46 agents, 2.6M tokens, 13 minutes, no agent
errors.** Full output: `interrogations/charter-v1-round1.findings.json`.

    40 candidates from 4 roles  →  30 findings after merge  →  10 blocking, 20 suggested
    0 mechanical findings: the charter passes all five checks

**The anti-inflation guard held.** No blocking finding lacks a citation, no `unclassified`
finding blocks, and no location-cited finding is anything but `internal-contradiction`.
State was assigned in code from the citation, exactly as designed.

### What it found — the ten blocking findings

| ID | Category | Citation | Defect |
|---|---|---|---|
| `r2` | internal-contradiction | two locations | The `form` role that replaces `charter` when this document is interrogated states its one question as covering only `Forbids:`, while the charter mandates a non-empty `Applies-to:` under the same form check and the shipped role in workflows/interrogate.js asks about both clauses. |
| `r3` | internal-contradiction | two locations | INV-06's `Forbids:` clause names stack-specific tool syntax (NoWarn, pragma disable), which the form rule excludes from invariants, INV-04 delegates to the pack, and the Invariants preamble claims the invariants avoid. |
| `r7` | internal-contradiction | two locations | The Invariants preamble claims each of the seven invariants is the only blocking route its category has, while the Coverage table gives scope-drift, gate-bypass and unfounded-claim a second, invariant-free route through a failed check. |
| `r8` | internal-contradiction | two locations | The Coverage section asserts that every finding category can block, while its own table's last row says `unclassified` never blocks by construction. |
| `r9` | internal-contradiction | two locations | The State section's no-progress default sends an untriaged — therefore uncited — `open` finding to `blocking`, which the promotion rule and the state table's non-empty `cites` requirement both forbid. |
| `r10` | internal-contradiction | two locations | The `cites` rule admits exactly three kinds of value, none of them a `DEC` id, while the open-decision register, the disposition table and schemas/findings.schema.json all require citing one. |
| `r12` | internal-contradiction | two locations | The Applies-to coverage matrix disagrees with the clauses it restates: INV-07 is ticked under `code` though its clause reads "every artifact" (a scope the document distinguishes from code by writing "every artifact, and code" for INV-03 and INV-08), INV-08 has no row at all, and INV-06's declared `pack` scope has no column. |
| `r14` | internal-contradiction | two locations | The frontmatter records the restore from e36fc4b as dropping one resolved decision from the register, while the register accounts for every id from DEC-01 to DEC-08 with five of them (DEC-01 through DEC-05) recorded as resolved and retained as struck-through gaps, and e36fc4b carried all eight as open. |
| `r23` | unfounded-claim | invariant INV-07 | INV-03's scope exemption for the retro corpus asserts that `absorb-retro` enforces the no-overwrite condition, but no such skill exists in the tree — skills/ contains only README.md, which lists absorb-retro as unbuilt PLAN item 12. |
| `r39` | missing-case | invariant INV-03 | `Prevented-by:` converts an invariant's zero cites from a question into "working as designed" without requiring that the named template or check still exist or still reject an artifact that violates the invariant. |

Three are independently verified against the charter text: `r8` (lines 272 vs 286), `r10`
(line 353 admits three cite kinds, line 423 requires a fourth, and the schema implements
four), and `r12` — which is **the coverage-table drift already on this plan's outstanding
list, found without being told about it**. That is the strongest signal from the run.

### Two things the run revealed about the framework itself

1. **`cycle-id: null` — the one schema violation, and it is not a schema defect.** The
   charter routes a charter change through a `charter-review` intent, so a charter
   interrogation is meant to run inside a cycle; `cycle-id` is required and non-nullable
   and the schema was right to reject this. What the run exposed is that `interrogate.js`
   permits a standalone mode the artifact model has no place for. **Decision needed:** make
   the workflow require a cycle, or let the schema accept a null `cycle-id` for
   `target: charter`. Everything else in the document validates.

2. **The `locations` citation route is doing almost all the blocking work** — 8 of 10
   blocking findings cite two conflicting locations; only 2 cite an invariant. That route
   needs no invariant at all, so it is a far cheaper way to reach `blocking` than the guard
   contemplated. Not wrong on this artifact — the charter really does contradict itself in
   eight places — but worth watching, because on a spec or a plan it could become the
   inflation channel the guard was built to close.

### Cost, measured rather than estimated

46 agents against an estimate of ~15. The driver is triage: **one agent per candidate**, so
40 candidates cost 40 agents, and candidate count scales with artifact size. A 460-line
charter costs 2.6M tokens to interrogate once. Batching triage is the obvious lever if that
proves too expensive on larger artifacts.

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
   separate instances; the rule now lives in one place, `SourceText`, because it was
   about to be spelled out a third time and a rule that holds in two of three places
   still reads as enforced.

10. **A fixture that avoids the house style cannot find bugs the house style causes.**
   Every fact-gathering fixture was pure ASCII while every real artifact is full of
   em-dashes, so the encoding defect below was invisible to 146 tests. A fixture should
   look like the thing it stands in for, including the parts that are inconvenient.

9. **A reference inside a string is invisible to every tool.** `/sdlc:spec` in a
   generated prompt is seen by no compiler, analyzer or schema, which is how ten items
   shipped with nothing invocable. Anything naming a component by string needs a check
   that resolves it, and that check needs a guard asserting it found references at all —
   otherwise a broken scan passes as a clean repository.

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
| 13 | Packaging — ~~manifest~~, marketplace catalog, CI semver check | Manifest done (`45916c9`). What remains answers the one question outside our control: does a marketplace install authenticate against a private remote |
| 14 | `dotnet-library` pack | The first real client is `MofonCode.Core`, which is a library |
| 15 | `init` | Cannot precede 14 — it scaffolds *from* a pack |

---

## Small, cheap, still outstanding

| Task | Why |
|---|---|
| **Delete the charter's coverage table** | A live INV-03 violation, drifted three ways: INV-08 absent, no `pack` column though INV-06 needs one, and 5 columns against the schema's 8 artifact kinds. Derived data should be derived — the fix is deletion, not a check. **The interrogation found this independently as `r12`**, which is the best evidence yet that the process works |
| **Resolve the ten blocking charter findings** | They edit the charter, so INV-08 requires an approved `charter-review` intent. That intent is also what would give the findings a `cycle-id` and a legitimate home, retiring `interrogations/` |

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
- **Six of the seven workflows have never been executed.** `interrogate` has now run once,
  against the charter. Every workflow test is still structural — shape, forbidden
  constructs and enum drift — so for the other six, nothing has run an agent.
- **A locked worktree cannot be faked in a test**, so `in-progress` is the one lifecycle
  state asserted only by inspection of the derivation rather than end to end.
- **Interrogator roles are hardcoded in `interrogate.js`** rather than read from the
  charter — see Deferred.
- **`claude plugin details` does not report workflows.** Its inventory lists skills,
  agents, hooks, MCP and LSP servers and omits workflows entirely — not even a zero row —
  so it reads as though nothing loaded. The load is confirmed from the debug log instead:
  `Total plugin workflows loaded: 7`. Check with `--debug-file`, not `details`.
- **`master` is still at the initial commit.** Everything lives on `bootstrap`. Nothing
  has been pushed to either remote.
- **This repository has two remotes.** `origin` is Azure DevOps
  (`dev.azure.com/MofonCode/ai-sdlc`) and both `master` and `archive/v01` track it;
  `github` is `github.com/MofonCode/ai-sdlc`. So v01 already lives on Azure DevOps, and
  "move to GitHub" is a change of default remote rather than a first push.
- **Three actions pending:** confirm `github.com/MofonCode/ai-sdlc` is private before
  moving anything there; confirm the arriving teammates have GitHub org access; decide
  whether `MarketSense` stays on Azure DevOps.
- **A junction at `~/.claude/skills/sdlc` pointing at this repository** is what loads the
  plugin locally, so every session on this machine picks it up. Remove it with
  `rmdir` on that path. A marketplace install is item 13.

---

## Log

Newest first. The **corrections** column is the point: each came from running a component
against real input, not from reading the design.

| Commit | Item | Correction it produced |
|---|---|---|
| `f10a659` | — · first interrogation | **The workflow permits a mode the artifact model has no place for**: a standalone charter interrogation has no cycle, so it emits a `cycle-id` the schema requires and rejects. Also measured rather than guessed: 46 agents, because triage spends one agent per candidate. And the `locations` cite route carried 8 of 10 blocking findings, being much cheaper to earn than an invariant |
| `5766f9f` | — · encoding | **The fact gatherer emitted JSON that `JSON.parse` rejects**, for any artifact that is not pure ASCII — which is all of them. Reading a BOM-less file as ANSI turned an em-dash into three CP1252 characters, and the last, U+201D, best-fit on output to a plain `"` that terminated the string. `interrogate.js` responds to unparseable facts by continuing *without* mechanical findings, so the symptom was a clean-looking interrogation rather than an error. Found on the first attempt to run one |
| `45916c9` | 13a · plugin manifest | **Six of the eight "dangling" references were never broken** — a plugin workflow is namespaced `/<plugin>:<meta.name>`, so they resolved the moment the manifest existed. One was real (`/sdlc:intent`, item 11). One was never a reference at all: `/sdlc:init` sits in a comment as an example of a non-path, and I read grep's hit as a claim |
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
