# Findings — ai-sdlc bootstrap

    Status:  LIVE. Updated as findings are resolved.
    Branch:  bootstrap
    Updated: 2026-09-25
    Verify:  dotnet test → 255 passed, 0 failed

## What this document is, and what it is not

Two halves, and they answer different questions:

- **[Resolved](#resolved)** — every defect found during the bootstrap and what fixed it. The
  record. Thirteen entries, and the point of keeping them is that **eleven of the thirteen
  were found by running something rather than by reading it.**
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

Eleven of thirteen surfaced only when something ran. That ratio is the strongest argument in
the project for the discipline of exercising each component against real input.

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

Two entries were **my own errors of claim rather than code**, and they are the ones worth
re-reading: F-01 (I reported the chain complete when nothing was invocable) and the audit that
found F-13 (I had claimed every decision was recorded when eleven were unbuilt).

---

## Open

**These live on the Azure Boards backlog now, not here.** Each work item carries the
story, the evidence, a proposed fix and acceptance criteria. Keeping a second copy in this
file would be exactly the INV-03 duplication the charter forbids — two representations of
one fact with nothing binding them — so what follows is an index and nothing more.

Board: **https://dev.azure.com/MofonCode/ai-sdlc/_workitems** — project `ai-sdlc`, area `ai-sdlc`, parent Feature **#66**.
Unscheduled deliberately: the process has no cadence (DEC-03 resolved as *a person,
manually, on no cadence*), so a story is pulled into an iteration when it is started.

| | # | Size | Story | Why |
|---|---|---|---|---|
| `US-01` | [67](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/67) | L | Run one cycle end to end | Nothing has gone intent to landed retro. The only large unknown left. |
| `US-02` | [68](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/68) | M | Make charter protection mechanical instead of prose | INV-08 is enforced by asking nicely. A PreToolUse hook would enforce it. |
| `US-03` | [69](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/69) | M | Land the bootstrap so a release can be cut | `master` is at the initial commit, so `release` cannot exist. **Needs a decision.** |
| `US-04` | [70](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/70) | M | Triage the twenty suggestions | The first interrogation's `suggested` pile is undispositioned. |
| `US-05` | [71](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/71) | S | Decide what the eight unfounded-claim suggestions mean | The strongest cluster. May be a missing invariant, may be eight small things. |
| `US-06` | [72](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/72) | M | Measure per-role yield | Four roles may be producing two and a half roles' worth of findings. |
| `US-07` | [73](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/73) | M | Cut interrogation cost | 46 agents and 2.6M tokens per artifact. Triage is one agent per candidate. |
| `US-08` | [74](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/74) | S | Remove DEC-08 from the register | Answered on 2026-09-25. Removing it is a charter intent, not an edit. |
| `US-09` | [75](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/75) | S | Give the one workflow a syntax check | Nothing proves `interrogate.js` is valid JavaScript. |
| `US-10` | [76](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/76) | L | Prove a skill obeys its rules | Skill tests assert the instruction is present, not that it was followed. |
| `US-11` | [77](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/77) | S | Cover the one unreachable lifecycle state | `in-progress` is asserted by inspection. `git worktree lock` makes it testable. |
| `US-12` | [78](https://dev.azure.com/MofonCode/ai-sdlc/_workitems/edit/78) | S | The two intents that bypassed the gate | Both predate `Submit-SdlcIntent.ps1`. **Recommend annotating, not retrofitting.** |

36 points across twelve stories. Three are quick wins at 1 point each (`US-08`, `US-09`, `US-11`) and two are decisions rather than tasks (`US-03`, `US-12`).

## How to work this list

**The board is where work is tracked. `.sdlc/intents/` is where it is authorised.** Those are
different jobs and neither replaces the other:

1. Pick a story from the board and set it **Active**.
2. Author an intent for it (`/sdlc:intent`) and put it behind its gate with
   `Submit-SdlcIntent.ps1`. Record the work item number in the intent body.
3. Run the cycle. The intent's state is **derived from git** — the board's `State` is a
   person's summary of it, and the two are allowed to lag.
4. When it lands, close the work item and move the row into **Resolved** above with its commit.

### The one thing to watch

A board beside the intents is a second list, and the charter forbids one without a check that
fails when the two disagree. There is no such check yet, which is a known and accepted gap
rather than an oversight:

- The **overlap window is small.** Before work starts there is no intent, so nothing is
  duplicated. After it lands, both are closed. Only an in-flight story is represented twice.
- The **binding is the work item number** recorded in the intent. That is what a check would
  compare, and adding one is cheap once there is more than one cycle to compare.

If the board and the derived state ever disagree in a way that matters, the derived state wins:
it is computed from git, and git is what actually happened.

### First three, and why

| | |
|---|---|
| `US-02` #68 | the right first cycle for `US-01` — small, real, and not self-referential |
| `US-08` #74 | a quick charter intent that exercises the charter-review path a second time |
| `US-03` #69 | a decision, and it blocks everything about distribution |
