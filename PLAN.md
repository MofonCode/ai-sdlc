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
| `FINDINGS.md` | **what was found and fixed, and the whole open backlog** — one list | yes — it is the working list |
| Azure Boards, project `ai-sdlc` | the twelve carried stories, for tracking those twelve | yes, but a subset — ten items exist only in `FINDINGS.md` |
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
last commit   a690f29
verify        dotnet test   → expect 255 passed, 0 failed
plugin        claude plugin validate . --strict                    → marketplace, passes
              claude plugin validate .claude-plugin/plugin.json    → plugin, passes
installed     sdlc@ai-sdlc, from the private GitHub marketplace
```

**Read `FINDINGS.md` first.** It carries the resolved findings, and indexes the open stories
on the Azure Boards backlog (project `ai-sdlc`, Feature #66) with a proposed fix on each.

**Next task: keep building, as bootstrap work.** No cycles. The concept is not complete, and
every attempt to declare it so has been followed within hours by finding something missing.

The nearest piece is `US-02` #68, the charter-protection hook. Its intent — `20260925-963e` —
is already merged and reads as a design record: the decisions in it (fail closed in every
case, four protected paths) were made deliberately and still hold. Build the hook against it
directly.

If `dotnet test` does not report 255 passing, stop and diagnose before building anything:
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

> **Enough = every decision in `concept.raw.md` is built.**

**NOT reached.** Declared reached on 2026-09-25 and that was wrong — the third time I have
claimed completeness in this project and been wrong about it:

| Claimed | Actually |
|---|---|
| "the chain is complete" | nothing was invocable |
| "every decision is recorded" | eleven components were unbuilt |
| "the concept is built" | `US-13` was missing entirely, and four more defects surfaced within hours |

The A–E audit closed eleven gaps and that work stands. What does not stand is the inference
that closing a list I wrote means the concept is done. Each time, the list was complete and
reality was not.

**So: NO CYCLES until the concept is complete.** Not "until the audit is empty" — until running
one stops finding parts that were missing.

An earlier version of this section said "a complete chain from a hand-written intent to a
landed retro", declared it reached on 2026-09-24, and called the rest conveniences. All three
were wrong: the chain was not invocable, and eleven decisions were unbuilt — including the
**intent gate**, which is a gate rather than a convenience.

---

## The shape: ten skills, one workflow, four scripts

Settled 2026-09-24 after the migration. **A stage is a workflow only when BOTH clauses
hold**, and only interrogation clears them:

1. it needs several agents that must not see each other's conclusions — the *independence*
   is the method, not merely parallelism; and
2. it can never need to ask the user anything, because a workflow cannot ask, at all.

```
skills/      intent spec plan repair build verify retro run init absorb-retro
workflows/   interrogate.js
tools/       Get-SdlcFacts · Get-SdlcCycleState · Get-SdlcVerifyOutcome · Complete-SdlcCycle
```

Two facts forced it, and both were verified rather than assumed:

- **The `Workflow` tool is removed from every subagent**, "even when listed in the `tools`
  field", and teammates cannot launch one either. So anything a subagent or teammate might
  drive cannot be a workflow. Skills run in every context; workflows only from a main session.
- **spec, plan, repair and build can all need to ask.** An `ambiguity` finding says a
  statement admits two readings leading to materially different work; when the two differ in
  what the *user* wanted, no reasoning settles it. The charter already escalates such a
  finding to a person, and an escalation **is** a question.

`verify` and `retro` could have stayed workflows — neither needs to ask — but clause 1 fails,
and a workflow with no fan-out inside it is a wrapper that pays with no filesystem access and
no testability.

**Where a rule must be mechanical rather than instructed, it goes in `tools/`.** That is why
the blast-radius tier, the lifecycle derivation, the landing gate and now the verification
outcome are PowerShell: a script can read files and be unit-tested, and a workflow script can
do neither.

### Supervised, then batched

| Phase | Stages | Who |
|---|---|---|
| Supervised, one cycle at a time | `intent` → `spec` → `plan` | a person drives, and approves the plan |
| Batched, unattended | `build` → `verify` | `run`, one worktree per cycle |

The old dispatcher had this backwards: it ran spec and plan unattended and halted before
build, reasoning that nothing irreversible happens before the plan. True, but it answers the
wrong question — safety was never the constraint on spec and plan, **quality** is. An
unsupervised spec is perfectly safe and probably wrong.

The gate between the phases is `approved:` in a plan's frontmatter, and the lifecycle
derivation enforces it: `dispatchable` tracks `planned`, never `queued`. The `plan` skill is
forbidden from writing that field.

---

## What the concept still asks for

Audited 2026-09-25 against all 93 `DECIDED` entries in `concept.raw.md`. Eleven components
were missing; **all eleven are now built.** Kept as the record of what each piece is for.

| | Built | What it is |
|---|---|---|
| **A1** | `855498c` | `Format-SdlcFindings.ps1` — one renderer, two audiences |
| **A2** | `855498c` | the same renderer, unfiltered, uncommitted |
| **B1** | `5fbcc49` | `Submit-SdlcIntent.ps1` — the intent gate |
| **B2** | `5fbcc49` | `Find-SdlcCharterIntent.ps1` — dedup, and the already-refused case |
| **C1-C3** | `2a2652a` | `packs/dotnet-library/` — facts, rules, charter, scaffolder |
| **D1** | `c1a9731` | `Move-SdlcRelease.ps1` + `marketplace.json` |
| **D2** | `c1a9731` | `.github/workflows/release-gate.yml` |
| **E1** | `7fe1c9b` | `skills/grill/` |
| **E2** | `7fe1c9b` | `skills/maintain/` |
| **E3** | `1a68ee5` | landing deploys through a pull request |
| **E4** | `7fe1c9b` | `Get-SdlcSignals.ps1` |
| **E5** | `a690f29` | charter v3 — roles are data, `gaps` learns the artifact |

**DEC-08 is answered.** A marketplace install authenticates against a private GitHub repo
using ambient git credentials — no PAT, no token in the repo. Proven by installing
`sdlc@ai-sdlc` from it. Removing the entry from the register is a charter change and needs
its own intent.

What follows is the original audit, unchanged.

### A — finish what is half-built

| # | Missing | Concept | Note |
|---|---|---|---|
| A1 | `suggestions/<cycle-id>.md` written at landing | §3 | One file per cycle, never a shared backlog — a file every cycle appends to conflicts the way `plugin.json` does. 20 suggested findings already have nowhere to live |
| A2 | Findings markdown view, rendered from the JSON | §3 | The JSON is authoritative; the markdown is regenerable and uncommitted, the same pattern as `facts.md`. `*.findings.md` is already gitignored for it |

### B — the gates, which are currently conventions

| # | Missing | Concept | Note |
|---|---|---|---|
| B1 | **Intent branch + PR gate**: `intent-<id>` → PR → merge, *then* `cycle-<id>` | §7 | *"The intent gate has to close before work starts or it is not a gate."* Both of today's intents were written straight to master, so this gate does not exist and two artifacts went through the hole |
| B2 | Charter-intent dedup on the targeted invariant | §7 | Pending → link and wait; **abandoned → block and surface its `reason:`**; nothing → file one. Without it a batch of eight files eight identical requests |

### C — the stack pack seam

| # | Missing | Concept | Note |
|---|---|---|---|
| C1 | `pack.yaml`, `pack.rules.yaml`, one real pack | §6 | Build the seam, not the machinery for the seam: one manifest the engine reads, and no registry or resolver until a second pack forces it |
| C2 | `scaffold` as pack **code** | §6 | The one verb that is code rather than data, because `dotnet new` is execution |
| C3 | The Microsoft-first ordering, per context | §9 | It is supposed to live in the pack, **not** the charter — tool names stay out of invariants |

Unblocks `init`, which cannot be exercised without a pack, and `maintain`'s staleness half.

### D — distribution

| # | Missing | Concept | Note |
|---|---|---|---|
| D1 | `release` as a single moving branch, plus a version tag at each move | §7 | No commits of its own; teammates subscribe once and you decide when they see something new |
| D2 | CI: **fail a `release` move that does not bump the version** | §7 | Never in-cycle: two concurrent cycles bumping `plugin.json` collide every time |

Answers DEC-08, and depends on the hosting contradiction below being settled.

### E — the remainder

| # | Missing | Concept | Note |
|---|---|---|---|
| E1 | `grill` — agent-team design debate, deliberately outside the cycle | §3 | Teammates challenging each other directly, which orthogonal roles do not need. **An original ask that was never built and never flagged** |
| E2 | `maintain` — files intents, never fixes | §1 | Fuelled by CVE and dependency drift and by pack staleness; telemetry is the reserved path and is unfuelled because nothing is deployed |
| E3 | `deploy` — PR + review | §1 | Landing exists; PR creation and review do not. *Promotion* is a separate pack-owned sub-step that may be nothing |
| E4 | Amendment signal query, read-only and on demand | §2 | `absorb-retro` describes it; nothing computes it |
| E5 | Interrogator roles as charter data | §3 | Intent `20260925-1a7e` drafted, not built |

### Recorded, not built — where we diverged from the frozen concept

| Concept says | We chose | Why |
|---|---|---|
| *"each stage is its own dynamic workflow"* (§1) | ten skills, one workflow | `Workflow` is stripped from every subagent and teammate, and most stages can need to ask the user something. See **The shape** above |
| *"the private ADO git repo IS the distribution"* (§10) | **unsettled** | The same section says *"GitHub only. One home, no mirror"* and explicitly reverses the ADO move. The later decision should win and the earlier text was never cleaned up, which is why this repo has two remotes. **Peter's call**, and D1 depends on it |

---

## Discipline — agreed, and load-bearing

1. **The whole first draft is disposable.** Written to be rewritten. Do not polish;
   polishing untested design is expensive and teaches nothing.

2. **Bootstrapping is exempt from the process until the concept is complete.** This line
   has now been wrong twice, in the same direction, and both times I declared the exemption
   over on the strength of a list I had written myself.

   **The rule, and it is Peter's not mine:** no cycles until the concept is complete. A few
   interrogations are worth running — they found real defects — but the intent-to-retro
   ceremony waits.

   **What going early actually cost, on 2026-09-25.** I ran the gate properly for one intent
   and bypassed it entirely for the work around it: two commits, four shipped scripts, no
   intent and no plan. Insisting on the ceremony in one place while walking past it in
   another is worse than either, because it makes the gate look observed when it is not.

   The half-run cycle `20260925-963e` is the evidence. Its intent is merged and its work
   never started.

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
| ~~11~~ | ~~`intent` skill~~ | **Done.** Shipped with the migration, along with nine others |
| 12 | Retro absorption into ai-sdlc | `absorb-retro` ships, but has never run: no retro exists yet |
| 13 | Packaging — ~~manifest~~, marketplace catalog, CI semver check | Manifest done (`45916c9`). What remains answers the one question outside our control: does a marketplace install authenticate against a private remote |
| 14 | `dotnet-library` pack | The first real client is `MofonCode.Core`, which is a library |
| ~~15~~ | ~~`init`~~ | **Ships**, but cannot be exercised before 14: it scaffolds *from* a pack |

---

## Small, cheap, still outstanding

Nothing. Both rows that lived here are done:

| Was | Outcome |
|---|---|
| ~~Delete the charter's coverage table~~ | **Done** in charter v2 (`8346a43`). The drifted `Applies-to` matrix was deleted rather than repaired, because derived data in a hand-maintained document with no generator should not exist. Verified: zero references remain |
| ~~Resolve the ten blocking charter findings~~ | **Done** in charter v2, via charter intent `20260924-c4a1` |

Everything outstanding now has a work item. See `FINDINGS.md` for the index.

---

## Deferred, with triggers recorded

| Deferred | Trigger |
|---|---|
| Health-checking layer | after a real cycle has landed |
| ~~Corpus health query~~ | **Done** — `tools/Get-SdlcSignals.ps1` is it (`7fe1c9b`) |
| Repo-scope interrogation | **Trigger fired** — skills exist, and the specific case is covered: `PluginManifestTests` resolves every `/sdlc:` reference. The broader idea (interrogate the whole repo, not one artifact) is still deferred and has no trigger |
| A living solution-level spec | when someone asks what the solution does and reading the code is too slow |
| Artifact directory archiving | when `intents/` passes ~50 files |
| ~~Charter structural checks~~ | **Promoted** to `US-13` #79. It was never a health-layer concern: the charter claims a check that does not exist, which is an unfounded claim rather than a missing convenience |
| ~~Interrogator roles as charter data~~ | **Done** — charter v3 (`a690f29`). The trigger never fired; direct observation of the drift was enough |

---

## Known gaps, stated rather than buried

- **Nothing proves the workflow scripts are syntactically valid JavaScript.** Node was
  deliberately dropped as a dependency and adding a JS engine to lint six files is not
  worth it, so a syntax error surfaces at first launch rather than at build. A real cost
  of the Node decision, not a defect in the tests. The PowerShell side has no equivalent
  gap — those are parsed by the real 5.1 parser in the suite.
- ~~**No `/sdlc:*` command has ever been invoked by name.**~~ A fresh session resolves all
  twelve skills and the workflow by name. The three with real side effects — `run`, `init`,
  `absorb-retro` — are correctly hidden from the model and visible only to a person.
- **Eleven of the twelve skills have never run.** They are markdown; the tests assert each
  still states its load-bearing rule, which is weaker than the workflow tests they replaced:
  a test can show the instruction is present, not that it was obeyed.
- **No cycle has gone end to end.** No spec, plan, `verify.md` or retro has ever been
  produced by its skill, and `Complete-SdlcCycle.ps1` has never landed anything. This is now
  the only large unknown left.
- **`bootstrap` has never merged to master.** `master` is still at the initial commit, so
  `release` cannot be cut and the marketplace is pinned to `#bootstrap` rather than
  `#release`.
- ~~**A locked worktree cannot be faked in a test.**~~ It can: `git worktree lock` needs no
  agent. `in-progress` is still the one state asserted only by inspection, but that is now a
  task rather than a limitation — `US-11` #77.
- ~~**Interrogator roles are hardcoded in `interrogate.js`.**~~ Charter v3 made them charter
  data, and the workflow keeps no copy (`a690f29`).
- **`claude plugin details` does not report workflows.** Its inventory lists skills,
  agents, hooks, MCP and LSP servers and omits workflows entirely — not even a zero row —
  so it reads as though nothing loaded. The load is confirmed from the debug log instead:
  `Total plugin workflows loaded: 7`. Check with `--debug-file`, not `details`.
- **`master` is still at the initial commit.** Everything lives on `bootstrap`, which is
  pushed. Tracked as `US-03` #69.
- ~~**This repository has two remotes.**~~ One now: `origin` is
  `github.com/MofonCode/ai-sdlc`, confirmed private. The Azure DevOps remote was removed on
  2026-09-25, losing nothing — GitHub already held `master` and `archive/v01` at identical SHAs.
  **ADO Boards is still in use for work tracking**, which is a different thing from hosting.
- **Of the three long-pending actions, one is answered and two are now tracked:**
  `github.com/MofonCode/ai-sdlc` is **confirmed private** (verified via `gh`); teammate org
  access is `US-14` #80 and is time-sensitive; MarketSense's code hosting is `US-15` #81.
- ~~**A junction at `~/.claude/skills/sdlc`**~~ is gone. The plugin is installed properly as
  `sdlc@ai-sdlc` from the private GitHub marketplace, which supersedes it — Claude Code said so
  itself when both were present.

---

## Log

Newest first. The **corrections** column is the point: each came from running a component
against real input, not from reading the design.

| Commit | Item | Correction it produced |
|---|---|---|
| `a690f29` | E5 · roles as data | **My charter parser reported twenty-five roles**, sweeping in the coverage table, the taxonomy, the state table and the disposition table. The charter is full of tables that look alike and only position distinguishes them |
| `1a68ee5` | E3 · deploy | **The retro skill and landing deadlocked.** Landing refuses to open its PR without a retro; the retro skill refused to write one before the merge. The concept settles it: the retro is written on the cycle branch and rides in with the squash |
| `c1a9731` | D · release | **The intent gate had the defect it exists to prevent.** It branched from HEAD while the PR targets master, so submitting from the 45-commit bootstrap branch would have proposed all 45 |
| `2a2652a` | C · pack | **Two defects found by building the scaffolder's output**, neither visible from reading it: a reader that matched only column-zero keys, so every version was empty and restore failed; and a test project that inherited settings under which NO TEST COULD COMPILE |
| `5fbcc49` | B · gates | **`2>&1` on a native command makes a successful git call throw** under `Stop`. Fourteen calls, eight in the landing script, which had never been run |
| `855498c` | A · suggestions | **Nine of fourteen checks could not block.** Unmapped ids fell through to `unclassified`, so gate bypasses and scope drift were silently advisory. Found by an interrogation, not by this suite |
| `79aee85` | — · outcome script | **My fixture was wrong and the parser was right.** Mnemonic cycle ids `cln1`/`unc1` are not valid: an id is eight digits plus four *hex* characters, so the criteria parser declined to read criteria keyed to a malformed id and every coverage assertion failed for an unrelated reason |
| `c7021f0` | — · skills migration | **`Workflow` is stripped from every subagent**, so any stage a subagent or teammate might drive cannot be a workflow. And my own new test read raw text and matched the comment explaining an absence — the fifth instance of discipline 8 in this suite, committed by its author |
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
