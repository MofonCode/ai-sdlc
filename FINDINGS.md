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
- **[Open](#open)** — what remains, written as user stories with a proposed fix each. The
  backlog.

**This is a triage list, not a second backlog.** `.sdlc/intents/` is this framework's intake
and the only place work is authorised from. A story below becomes real by being authored as an
intent and put through its gate — the same pattern the concept describes for promoting a
suggestion. If this file ever starts being worked from directly it has become the second list
INV-03 forbids, and the fix is to promote the stories and delete the rows.

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

### US-01 · Run one cycle end to end · **L**

> As the person who has to trust this process, I want one intent taken all the way from
> authoring to a landed retro, so that "it is built" becomes "it works".

**Evidence.** Nothing has gone intent → spec → plan → build → verify → land → retro. No spec,
plan, `verify.md` or retro has ever been produced by its skill, and `Complete-SdlcCycle.ps1`
has never landed anything. Eleven of twelve skills have never run. This is the only large
unknown left, and every other story is easier to judge once it is answered.

**Proposed fix.** Pick something small, real and self-contained — US-02 below is the obvious
candidate — and drive it through supervised, one stage at a time, recording where each stage
misbehaves. Do **not** batch it, and do not pick anything that modifies the stages themselves:
the first real run must not aim at a moving target.

---

### US-02 · Make charter protection mechanical instead of prose · **M**

> As someone relying on INV-08, I want an edit to `.sdlc/CHARTER.md` outside a charter-review
> cycle to be *blocked*, so that the rule protecting every other rule is not the only one
> enforced by asking nicely.

**Evidence.** I claimed early on that the gate boundary was runtime-enforced and later had to
concede it is not. What actually enforces the gates is the artifact model plus the landing
script; "do not edit the charter" is prose in INV-08 and in three skills. Every other invariant
is enforced by being citable — the rule with the most to lose is the exception.

**Proposed fix.** A `PreToolUse` hook, shipped in the plugin as the `hooks` component. It
rejects an `Edit`/`Write` whose path is `.sdlc/CHARTER.md`, `pack.yaml` or `pack.rules.yaml`
unless the current cycle's intent carries `source: charter-review` or `pack-review`. Tested the
way the other gates are: the refusals are the feature.

This is also the natural first cycle for US-01 — small, real, and it closes a hole I
introduced by overclaiming.

---

### US-03 · Land the bootstrap so a release can be cut · **M**

> As a teammate arriving next week, I want to install `sdlc@ai-sdlc` pinned to `release`, so
> that I am not tracking someone's feature branch.

**Evidence.** `master` is still at the initial commit. Forty-seven commits live on `bootstrap`,
so `Move-SdlcRelease.ps1` cannot run (it refuses off master, correctly) and the marketplace is
pinned to `#bootstrap`. `release` does not exist.

**Proposed fix.** Merge `bootstrap` to `master` — Peter's call, since it is the whole
bootstrap in one go and squashing it would destroy a commit log that is itself the record of
findings F-01 through F-13. Then `Move-SdlcRelease.ps1 -Version 0.1.0`, and re-point the
marketplace at `#release`. The release-gate workflow then has something to guard.

**Open question for Peter:** merge commit or fast-forward? A squash would collapse the
findings record, which argues against it.

---

### US-04 · Triage the twenty suggestions from the first interrogation · **M**

> As the person maintaining the charter, I want the first interrogation's suggestions read and
> dispositioned, so that the pile is evidence rather than a backlog nobody opens.

**Evidence.** `.sdlc/suggestions/20260924-c4a1.md` holds twenty findings that did not halt the
cycle:

| Category | Count |
|---|---|
| `unfounded-claim` | 8 |
| `internal-contradiction` | 4 |
| `missing-case` | 4 |
| `unresolvable-reference` | 2 |
| `ambiguity` | 1 |
| `untestable` | 1 |

Several are already stale — `r24` and `r25` said `init` and the signals query did not exist,
and both now do. Reading them is also how US-05 gets its answer.

**Proposed fix.** Work the file top to bottom. Each suggestion gets one of: promoted to an
intent with `source: suggestion`, `rejected` with a reason, or `accepted-wont-fix` with a
reason. **Do not merge the last two** — they feed opposite amendment signals. Stale ones are
`fixed`, naming the commit.

---

### US-05 · Decide what the eight unfounded-claim suggestions mean · **S**

> As the person deciding whether the charter is missing a rule, I want the largest suggestion
> cluster read as a cluster, so that a missing invariant is noticed rather than averaged away.

**Evidence.** `Get-SdlcSignals.ps1` reports `unfounded-claim` as the strongest cluster at
eight, which is the charter's own signal for *a missing invariant — candidate for addition*.
But INV-07 already unlocks that category and was cited once, so the reading is not obvious: it
may mean unverifiable claims are common and mostly minor, or that INV-07 is too narrow to catch
them.

**Proposed fix.** Read the eight together, not one at a time. If they share a shape INV-07
cannot reach, that is a charter intent. If they are eight unrelated small things, record that as
the answer and expect the cluster to persist harmlessly. **One cycle is not a trend** — the
charter's floor is ten — so the honest outcome may be "note it and wait".

---

### US-06 · Measure per-role yield before trusting the role set · **M**

> As someone paying 2.6M tokens per interrogation, I want to know which roles actually produce
> findings on which artifact kinds, so that dropping one is evidence rather than a guess.

**Evidence.** Recorded in intent `20260925-1a7e` and not yet acted on. On an intent, `facts`
has almost nothing to check — an intent makes few claims about the tree — and `charter` can only
load INV-01 and INV-02, the only invariants whose `Applies-to` reaches an intent. So four roles
may be producing two and a half roles' worth of findings at four roles' cost.

Measured once: `form` 5, `coherence` 10, `facts` 10, `gaps` 5 — against the charter, where all
four had something to read.

**Proposed fix.** Record role and artifact kind per finding — both already in the schema — and
extend `Get-SdlcSignals.ps1` with a yield-by-role-and-kind table. Drop nothing on one cycle's
evidence. **Do not delete a role to save tokens**: US-07 is the cheaper lever.

---

### US-07 · Cut the interrogation's cost, which is dominated by triage · **M**

> As someone who has to wait for an interrogation before a gate, I want it to cost minutes
> rather than a quarter of an hour, so that supervising a cycle is not four hours of sitting
> through them.

**Evidence.** Measured: **46 agents, 2.6M tokens, 13 minutes** for one 470-line document. The
driver is triage — **one agent per candidate**, so 40 candidates cost 40 agents — and candidate
count scales with artifact size. A supervised cycle interrogates twice (spec and plan), so
about 26 minutes of waiting per cycle and roughly 4 hours to prepare ten.

**Proposed fix.** Batch triage: one agent per *category* rather than per candidate, since
triage asks the same question of each and the citation rules are per-category anyway. That
alone should take 46 agents to roughly 12. Measure against the 2.6M baseline rather than
assuming.

Second lever, cheaper still: skip triage entirely for candidates whose category has no
invariant route at all — they can only ever be `suggested`, so the triage agent's answer is
knowable without asking.

---

### US-08 · Remove DEC-08 from the register, now that it is answered · **S**

> As a reader of the charter, I want the open-decision register to contain only decisions that
> are actually open, so that interrogation does not dismiss a finding against a settled question.

**Evidence.** DEC-08 reads *"One real install proving the private-marketplace credential
path"*. It was proven on 2026-09-25: a private GitHub marketplace cloned and installed on
ambient git credentials, no PAT. The register still lists it, and a `DEC` citation exists
precisely so interrogation can dismiss findings against an open question — which this no longer
is.

**Proposed fix.** A charter-review intent, `targets: [DEC-08]`, striking the row through as
`~~DEC-08~~` with its resolution recorded — the same treatment DEC-01 through DEC-05 received.
**Not an edit:** the register is charter content and INV-08 admits no other route.

---

### US-09 · Give the one remaining workflow a syntax check · **S**

> As someone launching `/sdlc:interrogate`, I want a syntax error in it to fail at build rather
> than mid-run, so that a typo does not surface only after the facts agent has already spent
> tokens.

**Evidence.** A long-standing recorded gap. Node was deliberately dropped as a dependency, so
nothing proves `interrogate.js` is valid JavaScript. It is checked for shape, forbidden
constructs and enum drift — all by regex over its text. The PowerShell side has no equivalent
gap: those are parsed by the real 5.1 parser in the suite.

The cost has fallen: there is now **one** `.js` file rather than seven.

**Proposed fix.** Parse it in the test suite with a managed JavaScript engine —
`Microsoft.ClearScript.V8` or `Jint`, either being a .NET package and so pack-compliant. Parse
only; do not execute. If neither is acceptable, record the gap as permanent and stop listing it,
because a gap that is never going to close is noise.

---

### US-10 · Prove a skill obeys its rules, not merely that it states them · **L**

> As someone relying on twelve skills, I want evidence that a skill's load-bearing rule is
> followed, so that a test suite that goes green does not just mean the instructions are still
> present.

**Evidence.** Stated plainly in `SkillTests`: these checks are weaker than the workflow tests
they replaced. When a stage was a workflow, "the outcome is decided in code" was enforced by a
branch a test could assert. As a skill it is prose a model is asked to follow, so a test can
show the instruction is there and nothing more.

The mitigation already applied is the right one and is not complete: rules that **must** be
mechanical live in `tools/` — the blast-radius tier, the lifecycle derivation, the landing
gate, the verification outcome.

**Proposed fix.** Two directions, and the first is cheaper than it looks.

1. **Move more rules into scripts.** Each one moved converts a prose assertion into a real one.
   Candidates: the spec's criterion-id uniqueness and `Verifiable` flag shape, and the plan's
   declared-files table — both already parsed by the gatherer, so the check is a comparison
   rather than new parsing.
2. **`claude plugin eval`.** It exists and runs eval cases against a plugin. That is the only
   mechanism available that tests a skill by *behaviour*. Not before US-01: an eval suite
   written against skills nobody has run would encode guesses.

---

### US-11 · Cover the one lifecycle state a test cannot reach · **S**

> As someone trusting the dispatcher not to relaunch live work, I want `in-progress` covered by
> something, so that the state distinguishing a running cycle from a dead one is not the only
> one asserted by inspection.

**Evidence.** Seven of eight lifecycle states are asserted against a real git fixture.
`in-progress` requires a **locked worktree**, which needs a live agent holding it — a test
cannot honestly fake that. It is currently verified by reading the derivation.

This matters more than its size suggests: `dead` versus `in-progress` is what stops a
dispatcher either relaunching work in flight or leaving a resource-exhausted cycle stalled
forever. With no concurrency cap, exhaustion is the likelier failure.

**Proposed fix.** `git worktree lock` can be invoked directly — no agent required. A test can
create a worktree, lock it, assert `in-progress`, unlock, and assert `dead`. That is a real end
-to-end check of both states and it removes the one inspection-only claim in the suite.

---

### US-12 · Put the two existing intents behind the gate they bypassed · **S**

> As someone auditing how this repository got here, I want every intent to have gone through
> the intent gate, so that the first two are not permanent exceptions to the rule they
> established.

**Evidence.** `20260924-c4a1` and `20260925-1a7e` were both written straight to the working
branch, because `Submit-SdlcIntent.ps1` did not exist yet — which is finding F-01's sibling and
what motivated building the gate. Neither has an `intent-<id>` branch or a pull request.

**Proposed fix.** Honestly, the cheapest correct answer may be **to do nothing and record it
here**. Retrofitting branches and PRs for two merged intents manufactures a paper trail for
approvals that happened in conversation instead, which is worse than an annotated exception.

The alternative, if the audit trail matters more: add a line to each intent stating it predates
the gate, and name the commit that introduced the gate. That is honest and costs nothing.

**Recommend:** annotate, do not retrofit. Flagging it for Peter because it is a judgment about
what the record is *for*.

---

## How to work this list

1. Author an intent for the story (`/sdlc:intent`), and put it behind its gate with
   `Submit-SdlcIntent.ps1`.
2. Mark the row here as promoted, naming the intent id.
3. When it lands, move it to **Resolved** with the commit.

The stories worth doing first, and why: **US-02** is the right first cycle for **US-01**
because it is small, real and not self-referential. **US-08** is a fifteen-minute charter intent
that also exercises the charter-review path for the second time. **US-03** is Peter's decision
and blocks everything about distribution.
