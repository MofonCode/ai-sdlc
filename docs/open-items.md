<!--
freshness: exempt
reason: a working list — true as of its last edit, and expected to change with every session
-->

# Open items

What is unfinished, unproven, or undecided. Each item says what it is, why it matters, and what would
close it — so picking one up needs no archaeology.

**Last reviewed:** 2026-09-29, at `0.8.3`.

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

---

## Constraints cited here

Short descriptions, so nothing here sends you to look one up. `CONSTRAINTS.md` carries the full
statement, its `Forbids:` clause and the reasoning; `/sdlc:constraints` renders the effective set for
a solution, this list plus any `LOC_` of its own.

| | |
|---|---|
| `CON_03` | a criterion states an observable outcome and how it is checked |
| `CON_04` | a spec covers every outcome its intent asks for |
| `CON_06` | a plan's proof is adequate to the criterion it claims to prove |
| `LOC_01` | a local constraint in the brownfield sample, not a global rule |
