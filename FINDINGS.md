# Findings — ai-sdlc bootstrap

    Status:  LIVE. Updated as findings are resolved.
    Branch:  bootstrap
    Updated: 2026-09-25
    Verify:  dotnet test → 255 passed, 0 failed

## What this document is, and what it is not

Two halves, and they answer different questions:

- **[Resolved](#resolved)** — every defect found during the bootstrap and what fixed it. The
  record. Eighteen entries, and the point of keeping them is that **sixteen of the eighteen
  were found by running something rather than by reading it.** One, `F-18`, is a gap in the
  process itself and is still open.
- **[Open](#open)** — an index into the Azure Boards backlog, where the twelve remaining
  stories live with their evidence, proposed fix and acceptance criteria.

**Three places, three jobs, and they do not overlap:**

| | Holds | Authority for |
|---|---|---|
| this file | the resolved record, and an index to the board | **what was found and fixed** |
| the ADO board | the twelve open stories, in full | **what is tracked and prioritised** |
| `.sdlc/intents/` | an intent per story once work starts | **what is authorised**, and the only route |

The open stories are *not* duplicated here. A second copy of a story would be the INV-03
duplication the charter forbids, so the index below carries a title and a link and nothing more.

Sizes are **S** (under an hour), **M** (a sitting), **L** (its own cycle, plausibly several).

---

## Resolved

Sixteen of eighteen surfaced only when something ran. That ratio is the strongest argument in
the project for exercising each component against real input — and F-14 through F-17 all
arrived **after** I had declared the concept complete, which is why that claim is now
withdrawn.

| # | Finding | How it surfaced | Fix |
|---|---|---|---|
| F-01 | **Nothing was invocable.** Ten items built, tested and committed with no plugin manifest, so no workflow became a command and no skill loaded. The dispatcher's prompt named eight `/sdlc:…` commands that resolved to nothing | a question from Peter | `45916c9` — manifest, plus a test resolving every `/sdlc:` reference against what ships |
| F-02 | **The fact gatherer emitted JSON that `JSON.parse` rejects**, for any artifact that is not pure ASCII — which is all of them. 5.1 read a BOM-less file as ANSI, and U+201D best-fit on output to a `"` that terminated the string. `interrogate.js` responds by continuing *without* mechanical findings, so the symptom was a clean-looking interrogation | first attempt to interrogate | `5766f9f` — `-Encoding UTF8` on six reads, explicit non-BOM UTF-8 output on four scripts |
| F-03 | **The charter contradicted itself in ten blocking places**, eight of them between two of its own passages. Reviewed by a human twice beforehand | the first interrogation | `8346a43` — charter v2, via charter intent `20260924-c4a1` |
| F-04 | **Nine of fourteen checks could not block.** Unmapped ids fell through to `unclassified`, so gate bypasses and scope drift were silently advisory while the charter claimed those routes worked | the first interrogation (finding `r20`) | `855498c` — all fourteen classified, plus a binding check in both directions |
| F-05 | **`2>&1` on a native command makes a successful `git` call throw** under `Stop`. Fourteen call sites, **eight in the landing script, which had never run** — the first landing would have died on its first git command | writing the intent gate | `5fbcc49` — `SdlcGit.ps1` redirects stderr to a file; `2>&1` banned in shipped code |
| F-06 | **The intent gate had the defect it exists to prevent.** It branched from HEAD while the PR targets master, so submitting from the 45-commit bootstrap branch would have proposed all 45 | trying to use it | `c1a9731` — branches from master explicitly |
| F-07 | **The pack reader matched only column-zero keys**, so every version under the indented `packages:` block read as empty and restore failed with NU1604 — on a solution the scaffolder had just reported as created | building the scaffolder's output | `2a2652a` — one level of nesting, plus a key-coverage binding check |
| F-08 | **The scaffolded test project could not compile a test.** It inherited `GenerateDocumentationFile` and `CA1515`, both of which fight MSTest's requirement that test classes be public | building the scaffolder's output | `2a2652a` — documentation off for tests, CA1515 suppressed with its justification |
| F-09 | **Landing skipped the review half of deploy.** It squash-merged straight onto master — the same shape of hole the intent gate had, at the other end of the cycle | reading the concept against the code | `1a68ee5` — landing opens a pull request and stops |
| F-10 | **The retro skill and landing deadlocked.** Landing refuses to open a PR without a retro; the retro refused to write one before the merge | making F-09's change | `1a68ee5` — the retro is written on the cycle branch and rides in with the squash |
| F-11 | **The charter role parser reported twenty-five roles**, sweeping in the coverage table, the taxonomy, the state table and the disposition table. The charter is full of tables that look alike and only position distinguishes them | running it | `a690f29` — scoped to the roles section |
| F-12 | **The `Workflow` tool is stripped from every subagent and teammate**, so any stage a subagent might drive cannot be a workflow. Invalidated "each stage is its own dynamic workflow" | verifying a claim before building on it | `c7021f0` — ten skills, one workflow |
| F-13 | **Two remotes, and the concept contradicted itself** on which was the home: a later "GitHub only, no mirror" decision reversed the Azure DevOps move, but the ADO distribution text was never cleaned up | auditing the concept | Peter settled it — GitHub only, ADO dropped |

| F-14 | **The release script could not cut a first release.** It demanded a strictly greater version than the manifest, unconditionally — so releasing the 0.1.0 the manifest already declared was refused and the project could never reach v0.1.0. It also contradicted the CI gate written for the same rule | trying to cut v0.1.0 | `4c37944` — first release may match the manifest; only `v*` tags mark a previous one |
| F-15 | **The release gate was hardcoded**, so a fixture with no .NET project could only report `refused-tests-fail` and every check after the gate was unreachable by a test. Also against INV-04: tool names belong to the pack | the same attempt | `421919a` — a `-TestCommand` parameter, matching `Get-SdlcVerifyOutcome` |
| F-16 | **Six git writes discarded their result.** A CI runner has no git identity, so `git commit` failed there and `Submit-SdlcIntent` reported `branched` with an empty branch — a person would believe the gate had closed on nothing | CI, which had been red for four pushes and which I had never looked at | `f1e3849` — named failures on every write |
| F-17 | **All three `ShouldProcess` scripts could not run non-interactively.** `ConfirmImpact = 'High'` prompts, and `powershell.exe -File` has no host that can ask. Interactively it throws a null-reference; from a test host it HANGS, waiting for a prompt that never arrives. Eight unattended cycles would have hung silently | cutting the release for real | `421919a` — the automatic prompt removed; `-WhatIf` kept |
| F-19 | **The only workflow in the framework was syntactically invalid for four commits.** Two errors, both from editing through layers that ate one level of escaping: a regex body that became a single backslash, and a `
` in a single-quoted string that became a real newline. `interrogate.js` would have thrown at launch the whole time. Every structural test passed, because they read the file as text | doing `US-16`, and only because node turned out to be installed | `d2c6c38` — `WorkflowSyntaxTests` parses each workflow the way the runtime loads it |
| F-20 | **A behavioural test of a function nothing calls.** I added the citation validation, then a test that executed `citationResolves` directly and proved it correct. Deleting the *call* — leaving the function sitting there, correct and unused — passed all fifteen structural tests **and** that new test. Proving a guard works is not proving anything consults it | mutation-testing `US-16` before committing it | `d2c6c38` — a second test executes the real assignment loop and reads the states it produces |
| F-21 | **The board and the backlog file drifted, and nothing could fail.** Ten stories existed only in `FINDINGS.md`; `US-03` was released and `US-04`/`US-05` dissolved while all three sat in `New`. Two representations of one list with no check between them — INV-03, in the repository that declares INV-03. Nothing caught it because closing a work item is not part of any ceremony: `verify` reads the build, `land` reads the PR, `retro` reads the findings, and nothing reads the board | being asked whether stories were being updated as they completed | `tools/Compare-SdlcBacklog.ps1` — reports every disagreement and exits non-zero; the comparison is fixture-tested, and three mutations of it fail |
| F-18 | **The process has no route for a defect found while doing something else.** F-14 through F-17 were all found mid-task and all blocked progress. Filing an intent for each would mean five gates; fixing them inline bypasses the process. I chose inline, twice, on shipped code — while running the gate for an unrelated intent | doing the above, and being asked about it | **unresolved.** `maintain` files intents and never fixes; the repair loop only works inside a cycle. Neither covers this |

Two entries were **my own errors of claim rather than code**, and they are the ones worth
re-reading: F-01 (I reported the chain complete when nothing was invocable) and the audit that
found F-13 (I had claimed every decision was recorded when eleven were unbuilt).

---

## Open — one backlog

**Everything in one place, deduped.** Two sources fed this: the fifteen stories on the
Azure Board and the twenty `suggested` findings from the first interrogation. They
overlapped, several suggestions were already fixed by later work, and two stories
dissolved once their content was resolved.

| | |
|---|---|
| in | 15 board stories + 20 suggestions + 2 loose findings = **37** |
| already fixed | 4 suggestions |
| dissolved | 2 stories |
| merged as duplicates | 1 pair (`US-13` and `r1`) |
| **open** | **22** |
| closed since | 2 — `US-16` and `US-09`, both by `d2c6c38` |
| **left** | **20** |

Sizes: **S** under an hour, **M** a sitting, **L** its own cycle.

### New — promoted from the suggestions, not previously tracked anywhere

| | Board | Size | Kind | Item | From |
|---|---|---|---|---|---|
| **`US-17`** | [83](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/83) | M | mechanism | Three invariants declare charter scope that no role ever loads | `r18` |
| **`US-18`** | [84](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/84) | M | mechanism | INV-06 claims a suppression-count comparison that nothing performs | `r27` |
| **`US-19`** | [85](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/85) | S | mechanism | INV-04's exemption has nowhere to be recorded, so it cannot be used | `r6` |
| **`US-20`** | [86](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/86) | M | mechanism | The taxonomy binding points at the schema, not at the charter that owns it | `r22` |
| **`US-21`** | [87](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/87) | M | charter | The charter says seven things it does not define or honour | `r4, r11, r15, r17, r29, r31, r36` |
| **`US-22`** | [88](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/88) | S | charter | Three quantitative claims about v01 have no verifying source | `r28` |
| **`US-23`** | [89](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/89) | M | charter | Disposition has two lifecycle gaps once time passes | `r37, r40` |
| **`US-24`** | [90](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/90) | M | process | There is no route for a defect found while doing something else | `F-18` |
| **`US-25`** | [91](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/91) | S | process | A completed charter-review intent reads as queued forever | `observed 2026-09-25` |

### Carried — already on the board

| | # | Size | Kind | Item |
|---|---|---|---|---|
| **`US-01`** | [67](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/67) | L | blocked | Run one cycle end to end |
| **`US-02`** | [68](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/68) | M | build | Make charter protection mechanical instead of prose |
| **`US-06`** | [72](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/72) | M | measure | Measure per-role yield before trusting the role set |
| **`US-07`** | [73](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/73) | M | cost | Cut interrogation cost |
| **`US-08`** | [74](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/74) | S | charter | Remove DEC-08 from the register |
| **`US-10`** | [76](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/76) | L | test | Prove a skill obeys its rules, not merely that it states them |
| **`US-11`** | [77](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/77) | S | test | Cover the one lifecycle state a test cannot reach |
| **`US-12`** | [78](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/78) | S | decide | The two intents that bypassed the gate |
| **`US-13`** | [79](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/79) | M | mechanism | Make the charter's form rule mechanical |
| **`US-14`** | [80](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/80) | S | decide | Confirm teammates have GitHub org access |
| **`US-15`** | [81](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/81) | S | decide | Decide whether MarketSense code stays on Azure DevOps |

### Closed — done, dissolved, or fixed by later work

| | Outcome | Why |
|---|---|---|
| `US-03` [69](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/69) | Landed and released | `bootstrap` squash-merged, v0.1.0 tagged, marketplace re-pointed at `#release`, release-gate CI green on its first run. |
| `US-04` [70](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/70) | Dissolved | Its content was “triage the twenty suggestions”. This document is that triage, so the story has no remaining work. |
| `US-05` [71](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/71) | Dissolved | Its content was “decide what the eight unfounded-claim suggestions mean”. Answered: four were true and are now fixed (`r20` `r24` `r25` `r26`), and four survive as `US-18`, `US-20`, `US-21` and `US-22`. |
| `r20` | Fixed `855498c` | Nine of fourteen checks could not block. All fourteen are classified now, with a binding check in both directions. |
| `r24` | Fixed `2a2652a` | “No init skill ships and no pack exists”. Both ship. |
| `r25` | Fixed `7fe1c9b` | “Neither the corpus nor the query exists”. `Get-SdlcSignals.ps1` is the query. |
| `r26` | Fixed `855498c` | “Nothing writes `suggestions/<cycle-id>.md`”. `Format-SdlcFindings` writes it and landing commits it. |

### What each kind means, and why it is the useful axis

| Kind | |
|---|---|
| `mechanism` | the code claims something it does not do |
| `charter` | the charter says something it does not define or honour |
| `process` | the process has no route for something that keeps happening |
| `test` | a guarantee weaker than it looks |
| `build` | described and not built |
| `measure` | we are guessing where we could measure |
| `cost` | it works and costs too much |
| `decide` | needs a decision, not work |
| `blocked` | blocked by agreement |

**`mechanism` is the group that matters.** Five items where the code or the charter claims a check, a scope or a comparison that does not exist. Every one of them makes the framework look stronger than it is. The worst of them, `US-16`, is now closed — it was a hole in the anti-inflation guard, which is what everything else rests on. `US-17` inherits the position: three invariants claim scope over the charter, and interrogating the charter loads none of them.

### The detail

#### `US-17` · M · **Three invariants declare charter scope that no role ever loads** · [#83](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/83) · from `r18`

`Applies-to:` is defined as what tells the `charter` role which invariants to load — and that role is **removed** when the artifact under interrogation is the charter. So INV-03, INV-07 and INV-08 claim scope over the charter, and interrogating the charter loads none of them.

#### `US-18` · M · **INV-06 claims a suppression-count comparison that nothing performs** · [#84](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/84) · from `r27`

Its evidence says an increase in the suppression count “is itself a finding”. `Get-SdlcFacts` emits a per-run count and checks only justification; nothing retains or compares a previous cycle's. Needs cross-cycle state, which nothing in the design has.

#### `US-19` · S · **INV-04's exemption has nowhere to be recorded, so it cannot be used** · [#85](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/85) · from `r6`

The clause reads “unless this charter records the deviation” and the charter defines no section, field or format in which to record one. A rule with an unusable escape hatch is a rule with no escape hatch.

#### `US-20` · M · **The taxonomy binding points at the schema, not at the charter that owns it** · [#86](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/86) · from `r22`

The charter says nothing but the findings schema may restate the category, stage and state values. `interrogate.js` restates them, and the only binding check ties those copies to the **schema enum** rather than to the charter. So the charter could drift from both and nothing would fail.

#### `US-21` · M · **The charter says seven things it does not define or honour** · [#87](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/87) · from `r4, r11, r15, r17, r29, r31, r36`

One amendment, seven defects, grouped because splitting one document edit seven ways is overhead without benefit:<br>**r4** the vocabulary `Applies-to:` draws from is never declared as a closed set (`code` is not a stage name; `every artifact` is undefined).<br>**r11** the frontmatter says `pack: none` while INV-04, INV-06 and INV-08 all define themselves by reference to a pack.<br>**r15** the amendment table says a `DEC` register change bumps the version; a register change was made on 2026-09-24 and the version did not move.<br>**r17** the resolved-decision row counts the zero-cite query as a sixth trigger beside five; the zero-cite paragraph counts it as the fifth of five.<br>**r29** the `charter-review` row lists an `Inherits:` line the charter does not have.<br>**r31** the frontmatter dates the charter from 2026-09-22 and git history has no commit on that date.<br>**r36** commencement is defined as an event and nothing records that it happened.

#### `US-22` · S · **Three quantitative claims about v01 have no verifying source** · [#88](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/88) · from `r28`

“Roughly 70% of findings mechanical”, “INV-03 bitten four times” and “a round cut from 30 tool calls to 13” appear in the charter and the concept as settled fact. The repository holds v01's intents, plans and specs but no findings corpus or retro, so none is checkable. They are load-bearing — several decisions rest on them — which is exactly why they need a source or a hedge.

#### `US-23` · M · **Disposition has two lifecycle gaps once time passes** · [#89](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/89) · from `r37, r40`

**r37** suggestions are required to be dispositioned “later, when the suggestions file is triaged”, and no trigger, cadence or owner for that triage is defined anywhere — which is why twenty sat unread for a day.<br>**r40** nothing states what happens to a finding dispositioned `deferred-to-dec` when the deferred decision is later resolved. Live now: DEC-01 through DEC-05 have been resolved.

#### `US-24` · M · **There is no route for a defect found while doing something else** · [#90](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/90) · from `F-18`

Four defects on 2026-09-25 were found mid-task and all blocked progress. Filing an intent for each means four gates; fixing inline bypasses the process. I chose inline, twice, on shipped code — while running the gate for an unrelated intent. `maintain` files intents and never fixes; the repair loop only works inside a cycle. Neither covers this, and until it does anyone will drift the same way.

#### `US-25` · S · **A completed charter-review intent reads as queued forever** · [#91](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/91) · from `observed 2026-09-25`

The lifecycle derivation reads `done` from a retro, and a charter intent has no cycle to produce one — so `20260924-c4a1` and `20260925-1a7e` will read as `queued` permanently. Harmless today because `dispatchable` needs an approved plan, but the lifecycle model simply does not describe charter intents.

#### `US-01` · L · **Run one cycle end to end** · [#67](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/67)

**Blocked by agreement**: no cycles until the concept is complete.

#### `US-02` · M · **Make charter protection mechanical instead of prose** · [#68](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/68)

Its design record is the abandoned intent `20260925-963e` — fail closed in every case, four protected paths. Build it as bootstrap work.

#### `US-06` · M · **Measure per-role yield before trusting the role set** · [#72](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/72)

Four roles may produce two and a half roles' worth of findings at four roles' cost.

#### `US-07` · M · **Cut interrogation cost** · [#73](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/73)

46 agents, 2.6M tokens, 13 minutes per artifact. Triage spends one agent per candidate.

#### `US-08` · S · **Remove DEC-08 from the register** · [#74](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/74)

Answered 2026-09-25. Removing it is a charter amendment, not an edit.

#### `US-10` · L · **Prove a skill obeys its rules, not merely that it states them** · [#76](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/76)

Skill tests assert the instruction is present, not that it was followed.

#### `US-11` · S · **Cover the one lifecycle state a test cannot reach** · [#77](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/77)

`git worktree lock` needs no agent, so `in-progress` is testable after all.

#### `US-12` · S · **The two intents that bypassed the gate** · [#78](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/78)

Recommend annotating, not retrofitting a paper trail for approvals made in conversation.

#### `US-13` · M · **Make the charter's form rule mechanical** · [#79](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/79)

**Merged with `r1`**, which found the same thing independently: the rule has no citation route, so nothing can cite it even when it is violated.

#### `US-14` · S · **Confirm teammates have GitHub org access** · [#80](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/80)

**Time-sensitive** — they arrive around 2026-09-29. Now installable from `#release`.

#### `US-15` · S · **Decide whether MarketSense code stays on Azure DevOps** · [#81](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/81)

Boards is settled for both projects; code hosting is not.

---

## How to work this list

**This document is the backlog.** One list, because three lists was the problem: stories on a
board, suggestions in a findings file, and loose observations in commit messages meant `US-16`
— a hole in the central mechanism — was invisible to anyone looking at the work queue.

| | Holds | Authority for |
|---|---|---|
| this file | the resolved record **and the whole open backlog** | **what is true and what is left** |
| the ADO board | **every item in this file**, one work item each | tracking, prioritising, and what a teammate sees |
| `.sdlc/intents/` | an intent per item once work starts | **what is authorised**, and the only route |

**The board now matches this file**, asked for on 2026-09-25 and not before. Until then it was
behind by ten items and carried four stories marked `New` that were finished — `US-03` released,
`US-04` and `US-05` dissolved, `US-09` closed. `US-16` through `US-25` are now #82 through #91,
each with its proposed fix on the work item.

**What let it drift:** closing a board item is not part of any ceremony. `verify` checks the
build, `land` checks the PR, `retro` reads the findings — nothing reads the board, so nothing
failed while it was stale.

**Now checkable:** `tools/Compare-SdlcBacklog.ps1` compares the two and exits non-zero on any
disagreement, in either direction, including a story missing from the board entirely. It cannot
be a CI test — the comparison needs `az` and a credentialled board connection, and CI has
neither — so the gathering runs on a developer machine while the judgement underneath it is
fixture-tested. Three mutations of the comparison fail the suite.

**Still open, and it is a decision not a task:** which ceremony runs it. Putting *run the parity
check* in a skill's prose is `US-10`'s weakness exactly — asserting the instruction is present
rather than that it was obeyed. A promise is what just failed here, so the honest options are to
have `land` refuse without a clean parity check, or to accept that this one runs by hand. Adding them
is one call away; leaving the board as the subset is also defensible while it is only Peter
working. What is not defensible is the state before this document existed, where nothing held
the whole list.

### And no cycles

Every item here is bootstrap work until the concept is complete. That is the agreement, and
`US-01` stays blocked by it deliberately — not deferred, blocked.

### Next three, and why

`US-16` was the first and is done — `d2c6c38`. What it cost is worth carrying forward: two
tests, because the obvious one passed under mutation, and a workflow that had been invalid
JavaScript for four commits found only by accident.

| | |
|---|---|
| `US-24` | moved up. Doing `US-16` produced two more mid-task defects, F-19 and F-20, and I fixed both inline again. Until there is a route for this, every item on the list below costs the same argument |
| `US-21` | seven charter defects, one amendment, and it clears the largest single group |
| `US-14` | the only item with a date on it — teammates arrive around 2026-09-29 |
