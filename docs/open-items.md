<!--
freshness: exempt
reason: a working list — true as of its last edit, and expected to change with every session
-->

# Open items

What is unfinished, unproven, or undecided. Each item says what it is, why it matters, and what would
close it — so picking one up needs no archaeology.

**Last reviewed:** 2026-09-30, at `0.8.3`.

Three categories, and the distinction matters more than it looks. **Needs a person** cannot be closed by
an agent at all. **Unconfirmed** is work that is done and unverified — the riskiest kind, because it
looks finished. **Never exercised** is honest ignorance.

---

## Needs a person

### 1. `ANTHROPIC_API_KEY` is not set in the repository secrets

The nightly `skills` job in `.github/workflows/ci.yml` invokes the model to assert that an
interrogation reaches the right verdict. Without the secret it emits a GitHub warning and stops
cleanly — which means **the job that asserts skill behaviour currently tests nothing**, while
appearing to run.

*Closes when:* the secret exists and one scheduled run has been observed.

### 2. CI has never been observed green

Every command CI runs has been verified locally — `dotnet test`, `dotnet format --verify-no-changes`,
both `claude plugin validate` calls. **No GitHub Actions run has ever been looked at.** The formatting
gate and the `skills` job are both new and unproven *in CI*, where the environment differs: a fresh
clone, no NuGet cache, no installed plugin.

*Closes when:* a run on `master` is green and someone has read the log.

### 3. `dice-trial` PR #1 is open and unmerged

Trial 3 carried a change from an empty directory to a pull request on branch
`cycle/20260929_4e1a`. Landing it would exercise the last step nothing has: a code owner merging.

### 4. The repository is private

Deferred deliberately. It affects one thing: `docs/guides/install.md` documents installing from
GitHub, and that path is untestable by anyone without credentials. The directory-marketplace path
works either way, and CI installs from the checkout, so nothing is blocked.

---

## Unconfirmed — done, and not verified

### 5. The CON_06 flake fix has not been confirmed

`plan.good.md` drew a blocking CON_06 finding in roughly half of twelve runs. The cause was traced to
the spec it is judged against: criterion 2 claimed a total for *every* non-empty sequence and named a
check over *one*, so an implementation correct only at the length it was tested at passes the check and
fails the criterion. That is CON_06's clause exactly.

Fixed across all four spec samples, by reading CON_03 rather than by running the model. **Whether the
flake is gone is not knowable from the structural suite.**

*Closes when:* the nightly job reports, or
`SDLC_HEADLESS=1 dotnet test --filter InterrogatingAGoldenArtifact` passes twice — roughly ten minutes
and nine model calls per run.

*If it still flakes:* that is judgement variance rather than a defect in the artifacts, and the answer
belongs in the expectation — a field for constraints permitted but not required — rather than in more
edits to the sample. Do not chase a judgement by editing an artifact until the model agrees; that tunes
a sample to one model on one day.

### 6. The first-cycle guide was last walked on `0.5.0`

We are at `0.8.3`. Four releases since changed things steps 4 to 13 touch: the declared-file gate
stopped refusing writes it had no business refusing, the intent interview stopped asking about the
stack, the approval block began printing an empty name, and the testing default gained the two settings
`dotnet test` needs. **Steps 4 to 13 are current by inspection, not by evidence.**

*Closes when:* a cycle is walked on the current version. A trial 4 would also exercise the Results and
Mediator stack defaults, which no real cycle has seen.

---

## Never exercised

### 7. `remediate`, `run` and `suggestions` have never run

Written, schema-governed, and untouched by any real cycle or any test that invokes them.

### 8. Nothing has reached `escalated`, so landing has never refused a cycle

The bounded-remediation state machine — three passes, then a person owns it — exists entirely on paper.
`Assert-Landable.ps1` has never refused anything, because no cycle has ever been unlandable.

**This is the behaviour that makes unattended work safe to trust**, and `run` depends on it. Deferred
deliberately, and worth naming as the largest untested claim in the framework.

*Closes when:* a cycle is driven to `escalated` on purpose — the cheapest route is a deliberately
inadequate proof, which should block, then remediate, then exhaust the budget.

### 9. brownfield's second property is untested

The sample carries `LOC_01` and an `adopted_at` sha to stand in for a repository that predates the
framework. What is now proven is that a solution's own constraints reach the effective set. What is not
is the other half, and it is the one the constraint is written for:

> Existing code that does this predates adoption and is not a finding; a change that adds another one is.

*Closes when:* a cycle in brownfield adds a tenant-id read and verify cites `LOC_01`, while the
pre-existing one in `OrderHandler.cs` draws nothing. Needs a diff, so it shares a prerequisite with
item 8 — one trial could close both.

---

## Undecided

### 10. Does an abandoned cycle keep its directory?

Concept open question 4, and now the narrow half of a question otherwise settled: landed cycles keep
their artifacts, decided at `0.8.3`. An abandoned cycle never landed, so its directory records only a
decision to stop. Keeping it accumulates dead cycles; removing it loses the reason it died.

### 11. `Sdlc.Validate` is not wired into any skill

The complete schema evaluator runs as a repository and CI tool. Inside a consumer solution, `verify`
still relies on the shallow key-name hook, because reaching the evaluator there would mean shipping a
built binary in the plugin. So a live cycle's `verify.md` is never fully validated in the solution that
produced it.

### 12. Nothing is designed for a non-Claude agent

The result-based checks — `Assert-BuildReady.ps1`, `Set-CycleBase.ps1`, `Compare-DiffToPlan.ps1` — read
git and would work as CI gates whichever agent did the work. The three action-based hooks are Claude
Code only, as are skills, load-time injection and `${CLAUDE_PLUGIN_ROOT}`. A coherent split exists —
git-based gates as the authority, hooks as a fast local layer — and is neither designed nor written.

### 13. Should an intent that is too large be split, and by whom?

**Raised by Peter.** The proposal: have the intent skill detect an oversized intent and split it into
smaller ones.

**Being too large is a real problem, and the mechanism is specific.** A cycle lands whole or not at all.
There is no partial landing, no way to ship four met outcomes while a fifth is unmet — so **cycle size
decides how much work one unmet criterion holds hostage.** A large intent also defeats the gate that
matters: the plan gate is the last point before anything becomes irreversible, and a plan touching sixty
files across five features cannot be meaningfully read, so approval degrades into signing. And the
remediation budget is per cycle, so three passes on an enormous change are three enormous passes.

**Three objections to the skill doing the splitting.**

*Detection is a constraint's job, not a skill's.* "This intent is too large" is a rule about an artifact
that needs reading comprehension to apply, which is the definition of a constraint here. The machinery
already exists: write it as `CON_nn`, and every interrogation raises it as a blocking finding with
splitting as the remedy. A skill that decides this instead puts a judgement somewhere nothing can audit,
and outside the one place findings are recorded.

*Splitting is a scoping decision, so it belongs to a person.* Which outcomes go in which cycle, and in
what order, has consequences — what ships first, what can be abandoned, what a reviewer sees together.
That is the same objection as auto-approval: the machine may propose, and a person decides at the gate.

*Splitting needs a concept the framework does not have.* If an intent splits into three and the second
depends on code the first creates, the second's plan declares files that do not exist yet. Nothing
expresses "this cycle depends on that one" — `.sdlc/cycles/` is flat, ids are timestamps with no
ordering guarantee, and no gate checks a predecessor landed. **Auto-splitting without dependency
ordering produces cycles that cannot be built in isolation**, which is worse than one large cycle that
at least builds.

**What would be better.** Detection and remedy are different problems, and the proposal conflates them.

1. **Have the interview ask, rather than the skill decide.** The intent skill already interviews. When
   the answers describe outcomes that share no cause and no beneficiary, ask whether this is one piece
   of work or several, and if several, say so and stop — the person starts separate cycles. Costs
   nothing architecturally, keeps the decision where the framework puts every other scoping decision,
   and fits what the skill already does.
2. **Add the constraint as the backstop**, for when the interview did not catch it. Not "how many
   outcomes" — an intent legitimately has several, and CON_04 requires the spec to cover all of them.
   The test is whether **one reviewable change could prove them all.**
3. **Consider catching it at the plan as well**, where size stops being speculative. At intent it is a
   guess; a plan declares its files, so a threshold is measurable there. The cost is late feedback, and
   the concept is explicit that killing a bad idea costs a paragraph at intent and a week after it is
   built — so the plan is a last line of defence, not the first.

**The deeper question this exposes:** size hurts because landing is all or nothing. Partial landing
would address the root cause and would also weaken *nothing advances on assertion*, which is the
property the framework is built on. Probably not worth it — but it is the reason splitting is a
workaround rather than a fix, and that is worth knowing before building either.

*Closes when:* a decision is recorded in `docs/concept.md`, and if a constraint is the answer, it is
written with a sample that trips it.

### 14. A skill that suggests how to divide an intent

**Raised by Peter,** as the constructive half of item 13. Splitting stays a person's decision; a skill
proposes how.

It would be a third read-only view, alongside `constraints` and `suggestions` — it decides nothing and
**writes nothing**. The moment it writes a split proposal it needs a schema, a gate, and an answer for
what happens when the proposal is ignored.

Three things to get right:

- **Run it before the intent is approved.** Splitting after a spec exists throws the spec away.
- **It will propose an order the framework cannot express.** Nothing records that one cycle depends on
  another: `.sdlc/cycles/` is flat, ids are timestamps with no ordering guarantee, and no gate checks a
  predecessor landed. The advice is still useful; claiming it is enforced would be the "looks like
  protection and is not" failure.
- **Naming.** Both existing views are plural nouns of what they show. `slices` fits; `divide` and `split`
  read as activities, which phases here deliberately avoid.

It pairs with the constraint in item 13: **the constraint answers whether an intent is too large, the
advisor answers how to cut it.** Different questions, no overlap.

Splitting also leaves the original intent's directory behind, which makes item 10 load-bearing rather
than theoretical.

### 15. Auto-interrogation with bounded self-healing

**Raised by Peter.** One command writes an artifact, interrogates it, works its own blocking findings and
interrogates again, up to a bound — reporting once at the end. Approval stays manual throughout.

**This does not need a skill to invoke another skill.** The constraint set already reaches `intent`,
`spec` and `plan` by load-time injection, so a skill can judge itself against the same rules in process.
The chaining prohibition is about invoking `/sdlc:interrogate`, not about doing the work.

**The bound is a state, not a stopping point.** Remediation already has this shape — three passes, and
exhaustion is the named outcome `escalated`. A loop needs the same, because three endings must be
distinguishable: converged clean, still blocking after the budget, and oscillating. Oscillation is not
hypothetical: uncapped observations once ran four rounds on a spec that was sound after two.

**Healing may fix how something is said. It may never supply what was never said.** This is the hard
limit and it falls hardest on intent, which is written *by interviewing a person*. Given "as a board game
player I would like to roll a single dice" there is no problem, no who, no done-looks-like and no
must-not-change — and a loop that heals rather than asks will **invent** them, which is CON_02's exact
failure mode, with every later phase then treating the invention as approved.

| Finding | Auto-healable |
|---|---|
| states a solution rather than a problem (CON_01) | yes — rephrase from the same information |
| a section missing or malformed | yes |
| asserts an unmeasured figure (CON_02) | **no** — demote it to **Open**, or stop and ask |

Demotion to **Open** is the legitimate third move: not deleted, not verified, recorded as unknown, which
is what CON_02 asks for. Spec and plan carry no such limit — both derive from an approved upstream
artifact, so the information exists and healing is only rewriting.

**Report what was fixed, not just that it converged.** The rounds are the record of what was challenged.
If r1's findings are resolved silently, a reader sees a clean r2 and never learns what was wrong with
their own spec, and that feedback is part of what the gate is for.

### 16. Independent interrogation: a fresh context, and a quorum for contested findings

**Raised by Peter.** Two separate wins, and conflating them hides the better one.

**A subagent buys independence.** It never sees the interview, the drafts or the author's reasoning —
only the artifact, the constraints and whatever it is judged against. That removes **investment bias**.
It does **not** remove model bias: same model, same priors, so it would flip the same coin on a contested
constraint.

Worth knowing what the status quo actually is. `run` drives each step as its own `claude -p` session, so
the unattended path already has a clean context per step. **The interactive path does not** — all three
trials ran `/sdlc:intent` then `/sdlc:interrogate` in one session, so the model that wrote the artifact
judged it with its own reasoning still in context. Typing the second command never created independence;
it created friction. This work makes the interactive path honest about a property the unattended path
already has.

**A team buys something different and more valuable: it turns a coin flip into a visible disagreement.**
`plan.good.md` draws a blocking CON_06 finding in about half of runs, and today that returns as a single
verdict. Three independent interrogators return 2–1 or 1–2, and **the split is the information**: a
finding raised by 3 of 3 is not the claim a finding raised by 1 of 3 is, and nothing can currently tell
them apart. That is why item 5 took twelve runs to characterise by hand.

This supersedes an earlier idea of a `may_cite_blocking` field. **Record a finding as contested rather
than as permitted** — contested is true, permitted is a tolerance. It needs a field on a finding: how
many interrogators raised it, out of how many.

On what to do with disagreement: *any is blocking* over-refuses, *majority* collapses the signal back
into a verdict, *unanimous* lets one lenient judge clear anything. **Record the split and let the gate see
it** — a contested judgement is exactly what a person should weigh, and a single verdict destroys it.

**Cost, and proportionality.** Three artifacts, two rounds, three agents is about eighteen model calls a
cycle for interrogation alone. Match judges to the cost of being wrong: **most for the plan**, which is
the last gate before anything becomes irreversible; one for intent and spec, which are cheap to redo.

**A subagent must invoke the real skill, not a paraphrase of it.** Global constraints arrive by load-time
injection from the plugin root; a bare agent handed a hand-written prompt is a second path to the same
rules, which is the two-sources failure CON_09 exists for.

### 17. Orchestrated multi-cycle build: partition, parallel worktrees, one integration branch

**Raised by Peter.** Several approved plans, built together, landing as a single pull request.

The primitive is right and already available: **two plans conflict exactly when their declared file sets
intersect**, and `Get-SdlcDeclaredFile` in `tools/SdlcPlan.ps1` already parses a plan's `## Files` table
into that set. Nothing new to invent, and nothing to approve twice.

**The algorithm.**

1. Collect eligible cycles — approved plan, no blocking findings.
2. Read each `## Files` table.
3. Build a conflict graph, an edge wherever declared sets intersect.
4. Connected components of size greater than one are **serial groups**; isolated nodes are
   **parallel-safe**.
5. Create the orchestrated feature branch off the default branch.
6. **Serial groups first**, one cycle at a time, each in a worktree branched off the *current feature
   tip* — build, verify, squash-merge on `pass`.
7. After each serial merge, flag any cycle not yet run whose declared set intersects what just changed.
8. **Parallel-safe set concurrently**, each in a worktree off the feature tip at that moment — build,
   verify, squash-merge.
9. **Integration check on the feature branch**: build and a full test run.
10. One pull request, carrying every cycle's intent, plan and verify document.

Branching worktrees off the feature tip rather than the default branch means later cycles build on
earlier ones. Their pinned bases differ, which is correct — each is pinned in its own worktree at its
own build time.

**This belongs to `run`, not `build`.** Keep `build` single-cycle. `run` is already the orchestrator —
eligibility, a worktree per cycle, one `claude -p` session per step — and it inherits the mechanism that
makes any of this possible.

**Verify runs in the worktree, never on the merged commit.** A cycle pins `base.sha` and
`Compare-DiffToPlan` diffs base to working tree. A squash merge produces a new sha and drops the cycle's
commits from the feature branch's history, so on that branch the base is unreachable and the comparison
is meaningless. The feature branch is integration only.

**Why not one shared working tree.** It disables three mechanisms at once: the gates key on
`^cycle/<id>$`, so a branch carrying several cycles matches nothing and every write gate goes silent
during the phase they exist for; `Compare-DiffToPlan` reads unstaged and untracked files too, so each
cycle sees its siblings' work as `UNDECLARED`; and two agents running `dotnet build` share `obj/` and
`bin/`. It also inverts a DECIDED entry — one worktree per cycle, so parallel cycles cannot interfere.

**Two gaps this exposes.**

*Nothing checks the combined result.* Each cycle is green in its own worktree; together is unknown. And
**file overlap is a conservative proxy that misses semantic coupling**: cycle A declares `Totals.cs`,
cycle B declares `Invoicing.cs` which calls `Totals.Sum`, the sets do not intersect, the partitioner runs
them in parallel, and A renaming a method breaks B. Step 9 is what catches it, and it is not verify —
verify adjudicates one cycle against one spec, while this asks whether the integration stands.

*Nothing detects a stale plan.* Serialising does not resolve a conflict, it sequences it: the second plan
was approved against a codebase where the first had not happened. CON_07 judges a change against its
plan; no constraint judges a plan against the tree it will be built into. Building anyway defers the
problem to verify, and auto-rebasing the plan is the machine editing an approved artifact, which is the
approval gate gone. **Refusing and reporting is the stop that keeps the gate meaningful** — "B overlaps A
on `Foo.cs`, A has landed, B needs re-approval".

*Also true today, independent of orchestration:* two approved plans can declare the same file and neither
knows. The `## Files` table is checked per plan; nothing cross-checks plans in flight. A cross-plan
overlap check is small, independently useful, and the same read step 3 needs.

---

## Constraints cited here

Short descriptions, so nothing here sends you to look one up. `CONSTRAINTS.md` carries the full
statement, its `Forbids:` clause and the reasoning; `/sdlc:constraints` renders the effective set for
a solution, this list plus any `LOC_` of its own.

| | |
|---|---|
| `CON_01` | an intent states the problem, not the solution |
| `CON_02` | an intent's unknowns stay open questions |
| `CON_03` | a criterion states an observable outcome and how it is checked |
| `CON_04` | a spec covers every outcome its intent asks for |
| `CON_06` | a plan's proof is adequate to the criterion it claims to prove |
| `CON_07` | a change does only what its plan declared |
| `CON_09` | no artifact restates another's content as its own authority |
| `LOC_01` | a local constraint in the brownfield sample, not a global rule |
