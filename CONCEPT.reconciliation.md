# CONCEPT.reconciliation.md

Working through the findings in [`CONCEPT.contradictions.md`](CONCEPT.contradictions.md),
reconciled against what is actually implemented on
`development/iteration-4-foundation` as of 2026-10-01 (five commits, 202 tests).

Each item carries what the code does now, what the concept says that conflicts with it, the
text of any constraint it turns on, and the options for closing it. **Decision** is filled in
as we go; nothing is changed until it is.

## Status

Two pending additions to `CONSTRAINTS.md`, both from decisions below: **`CON_13`** (a plan says
why it departs from the declared stack, from N7) and **`CON_14`** (an open item is one nobody can
resolve yet, from S2). Neither is written yet; `ShippedLibraryTests` asserts a gapless id
sequence, so they land together or the count assertion is updated with them.

| | Finding | Severity | Code affected | Status |
|---|---|---|---|---|
| S1 | interrogation is both a fresh process and in-process | blocked building | no — unbuilt | **settled** |
| S2 | accepting a blocking finding before the gate is impossible | blocked building | latent | **settled** |
| S4 | one mechanical check duplicates `CON_04` | structural | no — unbuilt | **settled** |
| S6 | `solution.md` cannot be changed by the one thing allowed to change it | structural | **yes — unguarded** | **settled (D-09)** |
| N7 | nothing makes an unargued stack departure illegal | minor | no | **settled - CON_13** |
| N3 | no collision detector exists at allocation | minor | **yes — false claim in code** | **settled (D-10)** |
| N8 | no skill prints a refusal | minor | yes | **settled (D-11)** |
| S11 | `verify-chain` checks half of what makes a plan binding | structural | no — unbuilt | **settled (D-14)** |
| N1 | citation format for revision 1 is given two ways | minor | yes - permissive | **correction** |
| — | `SolutionFileName` reads as `solution.md` | minor | yes | **settled (D-12)** |
| S3 | the single gate is justified by something `CON_09` forbids | prose | no | **correction** |
| S5 | the gate binary and PowerShell 5.1 are mutually exclusive | structural | settled in code | **correction** |
| S7 | `batch-build` never pushes, and lands partial batches | structural | no - unbuilt | **settled (D-22/23/24)** |
| S8 | `after` lives in an artifact that does not exist when it is written | structural | no - unbuilt | **dissolved (D-25)** |
| S9 | the self-healing loop's exits have nowhere to land in a plan | structural | no — unbuilt | **settled (D-07, D-08)** |
| S10 | the evidence contract is stack-bound | structural | no — unbuilt | **settled (D-13)** |
| T12 | `adopted_at` is dropped three times and alive twice | stale prose | no | **correction** |
| T13 | `MofonCode.Core` both ships analyzers and ships none | stale prose | no | **correction** |
| T14 | `CON_10` binds a thing the design deleted | stale prose | **yes - removes code** | **settled (D-27)** |
| N2 | "three files in `.sdlc/`" against a layout showing four | minor | no | **settled (D-28)** |
| N4 | "host" means two things | minor | no | **correction** |
| N5 | three delivery paths, no stated precedence | minor | settled in code | **settled (D-26)** |
| N6 | the panel is not ASCII-safe | minor | code already correct | **correction** |
| N9 | the self-healing loop owns no skill | minor | no | **settled (D-29)** |
| G1 | no recovery path when the plan is wrong | structural | no - unbuilt | **settled (D-19)** |
| G2 | steering has nowhere to go between stages | structural | no - unbuilt | **settled (D-16/17/18)** |
| G3 | nothing proves a downstream artifact derived from the current upstream | structural | no - unbuilt | **settled (D-21)** |
| — | artifact sections audited against a consumer test | structural | no — unbuilt | **settled (D-06)** |
| — | envelope audited; metadata bag rejected; `type` and `tier` to the body | structural | no — unbuilt | **settled (D-07a)** |

---

## S1 — interrogation is both a fresh process and in-process

**Settled.**

**Decision.** A new process for interrogations. Generalised into a principle worth stating in
the concept:

> **Side research and evaluation run in a new process. The main window keeps only what it needs
> to decide.**

Both triggers earn it for the same reason from two directions: research returns an answer and
should not leave its search behind, and an evaluation must not have seen the thing it judges
being made. One sentence covers interrogation, the fresh readers, and any "go find out X" step.

**Consequence.** The healing loop cannot judge in-process. It spawns a fresh reader per round
like anything else, which makes the loop *orchestration* — write, send out for judgement, apply,
send out again — rather than a thing that judges. That also removes the naming overlap that
created the contradiction.

---

## S2 — accepting a blocking finding before the gate is impossible

**Settled.** The deadlock turned out to be in the concept's prose rather than in the constraint
set: the compliant exit already existed, and the trial had used it.

### What we are supposed to do

- §4.4: a stage will not run until the artifact before it has an interrogation round with **no
  open blocking findings**. A blocker may be cleared by refining the artifact, or — where a
  person accepts it — by naming it in the approval entry. Not by ignoring it.
- §4.1: a cycle has **exactly one gate**, taken immediately before `build`.

### What conflicts

The approval entry does not exist during the planning stages, so a blocking finding that
refinement cannot close has no legal exit, and the next stage never runs.

### The constraints involved

> **CON_02 — An intent's unknowns are recorded as open questions, not answered by guess.**
> *Applies to:* an intent, at approval.
> *Forbids:* an intent stating as settled fact a figure, cause or behaviour that nobody has
> measured or verified.
> *Why:* every later phase treats an approved intent as settled. A guessed figure becomes a
> criterion, then a test, then a green verification of something nobody established.

> **CON_04 — A spec covers every outcome its intent asks for.**
> *Applies to:* a spec, at approval.
> *Forbids:* a spec whose criteria satisfy some of the intent's stated outcomes while leaving
> others with no criterion at all.
> *Why:* a gap here is invisible downstream. Build implements the spec and verify checks the
> spec, so an omitted outcome is never mentioned again and the cycle lands green having done
> part of the job.

### Where the code stands

`ArtifactState.IsClear` requires zero open blockers, and `CycleStatus.Blocking` drives the panel
off it. Nothing refuses to run the next stage **only because `spec` and `plan` are not
implemented**. The deadlock goes live the moment they are. Meanwhile `cycle-approval` pre-lists
open blockers as accepted, so the approval entry is currently the only acceptance mechanism in
the system.

### Options

- **(A) Advance on a round existing, not on it being clean.** The blocker travels forward and is
  accepted by name at the one gate. No new mechanism. Cost: a plan can be built on a spec with a
  known hole, visible but not agreed until the gate.
- **(B) A pre-gate acceptance record.** A second place to accept a finding. Cost: two
  mechanisms, two places to look, and it is a second gate under another name.
- **(C) The artifact carries the record.** An open region — the intent's `Open`, the spec's
  `Blocked on` — holds what cannot be resolved yet, with its context. The next interrogation
  judges whether that record is honest. This is what the trial cycle actually did.
- **(D) Only spec and plan require clean; intent does not.** Arbitrary, and the intent is where
  the hardest blockers live.

### Decision

**Constraints bind the artifact's body. The open region holds what cannot be resolved yet,
with its context. Moving an item there is not a waiver — it is the compliant move.**

Read `CON_02`'s forbids clause literally: *an intent stating as settled fact a figure nobody has
measured*. An item sitting in `Open` as a question is not being stated as settled fact, so the
constraint is **satisfied** rather than waived. `CON_02` asks for exactly this; it is in the name.
And `CON_04` forbids leaving an intent's *stated outcomes* without criteria — an open question is
not a stated outcome, it is an unresolved question about whether there is one, so `CON_04` never
fires on it.

**So there was never a deadlock in the constraint set.** The deadlock was in §4.4's prose, which
assumed a blocking finding needs a waiver when the compliant exit existed all along. The trial
found that exit without being told: the spec's `Blocked on` entry is it, and the cycle was
approved and shipped.

This **saves** §4.4 rather than weakening it. Clean-to-advance stays, because an unresolvable
item moves to the open region and that *is* the fix, so the round goes clean. No pre-gate
acceptance, no second gate, no waiver machinery. Options A, B and D are all rejected; the
decision is C, generalised.

**What it needs: a guard on the open region.** If moving an item there always satisfies the
constraint, an agent can empty the body into it and be trivially compliant — a body with nothing
in it breaks no rule. That is the only new rule the model requires:

> **CON_14 — An open item is one nobody can resolve yet, not one nobody has done.**
> *Applies to:* an intent, a spec or a plan, at approval.
> *Forbids:* an open item recording something the artifact's author could have established — a
> question answerable by reading the code, a decision the interviewee was never asked, or a
> criterion that is merely hard to write.
> *Why:* moving a claim out of the body into the open region is how an artifact becomes compliant
> honestly. It is also how one becomes compliant falsely, because a body with nothing in it
> breaks no rule. Whether each open item is genuinely unresolvable at this stage is the only
> control on that, and it needs a reader.

**On the gate at the plan stage — half of it, and the half that falls out for free.**

*No blocking findings at plan:* yes, and it needs no new rule once the open region works as
above.

*No open items at plan:* **no.** The trial is the evidence against it. Its plan carried
`Cannot prove` entries citing intent Open 2 and Open 3, was approved, built, and shipped a
working solution. A hard rule would have blocked a cycle that was fine, and false positives on a
gate are what make a team route around it.

**The true version is about dependency, not absence:** nothing the change *depends on* is
unresolved. The existing constraints already produce that, with one addition — **the spec must
dispose of every intent open item** into one of three places:

1. a criterion — resolved, in scope
2. `Out of scope`, with a reason — resolved, excluded
3. `Blocked on`, citing the intent question — deferred, explicitly

With every item disposed, anything still open at plan time is *by construction* out of scope or
explicitly deferred: anything in scope got a criterion, and `CON_06` and `CON_12` require every
criterion to have a proof that can actually run. The trial spec already does this — six
`Out of scope` entries with reasons, two `Blocked on` entries citing intent Opens by number.

**Assembled:**

| Piece | What it is | New |
|---|---|---|
| constraints bind the body; the open region holds unresolvables with their context | how the constraints already read | no |
| `CON_14` guards the open region against being a dumping ground | one new constraint | **yes** |
| the spec disposes of every intent open item into criterion, out of scope, or blocked on | mechanical check | replaces S4's |
| clean round to advance | §4.4, unchanged and now safe | no |
| one gate before build | §4.1, unchanged | no |

**Follows from this:** S9 (what the open regions are called on each artifact) stops being a
loop detail and becomes load-bearing structure. It is the next item.

---

## S4 — one mechanical check duplicates `CON_04`

**Settled, with S2.**

**Now.** Not implemented. §4.4 lists it as the first of three checks that "need no reading
comprehension at all": *every intent statement maps to at least one spec criterion.*

**Conflicts with.** `CON_04` (text above) is in the library only because it passes admission
test two: *no analyzer, schema or hook could decide it*. A mechanical check that decides
intent-to-spec coverage would mean `CON_04` fails its own admission test.

**Options.**

- **(a) Drop the mechanical check, keep `CON_04`.** Deciding what counts as a statement, and
  what counts as a criterion answering it, is reading comprehension.
- **(b) Replace it with a check that does not overlap.** Mechanical: *every criterion carries an
  "Answers to" line citing the intent.* Presence is decidable by a parser; `CON_04` keeps
  adequacy. The trial spec already writes those lines.
- **(c) Drop `CON_04` and make it a hook.** Rejected — coverage cannot be decided mechanically,
  which is why it is a constraint.

**Recommended.** (b). Note the third check in §4.4 — criterion to a named test — is sound as-is:
existence is mechanical where `CON_06`'s adequacy is not.

**Decision.** The intent-to-spec coverage check is **dropped**; `CON_04` keeps it, because
deciding what counts as a statement and what counts as answering it is reading comprehension.

It is **replaced by the disposal check from S2**: every item in the intent's open region is
mentioned somewhere in the spec — as a criterion, under `Out of scope`, or under `Blocked on`.
That is presence, which a parser decides, and it does not overlap `CON_04`'s judgement about
whether what was written actually covers the outcome.

So the three structural checks in §4.4 become:

1. every intent open item is disposed of by the spec *(new, replaces the coverage check)*
2. every spec criterion maps to at least one file and one named test in the plan *(unchanged)*
3. every changed file is declared in the plan, and every declared file changed *(unchanged — see
   S11 for the second half)*

---

## S6 — `solution.md` cannot be changed by the one thing allowed to change it

**Open. Code affected.**

**Now.** `ProtectedPaths` guards `approval.md`, `abandoned.md` and the solution's
`constraints.md`. **`solution.md` is not guarded at all** — an agent can write it today.

**Conflicts with.** §7's mutability table gives `solution.md` "the hook denies agent writes
after creation", unconditionally. §7.3 then says `profile` is the one key that can legitimately
change, "through a cycle that declares `solution.md`". A cycle is run by an agent, whose write
the hook denies — the same carve-out regress that the unlock flag was rejected for in §7.1:
*an agent that can write files can flip the flag and then edit the file, and protecting the flag
needs a flag.*

**Options.**

- **(a) Protect the file; change `profile` by hand.** Consistent with `constraints.md`. A profile
  migration is rare and deliberate, so a person edits one line. No carve-out, no new concept.
- **(b) Protect per-key.** The hook sees a write, not a diff, so this needs the gate to compare
  before and after. Possible, new machinery.
- **(c) Leave it unprotected; let the gate catch drift.** Cheapest. `origin` becomes
  agent-rewritable, which is the historical fact the file exists to protect.

**Recommended.** (a).

**Decision.** _To be recorded._

---

## N7 — nothing makes an unargued stack departure illegal

**Open.**

**Now.** Nothing enforces it.

**Conflicts with.** §7.2: *"A plan may depart from any default. Silence about departing is what
is illegal"* — in a section that opens by saying `techstack.md` is not enforced and "nothing here
binds by itself", with no constraint among the twelve covering it.

**Way forward.** The requirement splits in two, which is also what connects it to the
maintenance gap:

- the **mechanical half** is drift — a package in the diff that `techstack.md` does not mention.
  A check decides that.
- the **judgement half** is whether the plan argued the departure. No hook decides that, so it
  is admissible as a constraint:

> **CON_13 — A plan that departs from the solution's declared defaults says why.**
> *Applies to:* a plan, at approval.
> *Forbids:* a plan naming a dependency, framework or pattern the solution's declared stack does
> not, without a sentence giving the reason.
> *Why:* a default exists so a reader knows what to expect. A departure argued in the artifact a
> person approves is a decision; a silent one is a habit nobody chose and nobody can find later.

Stack-neutral, forbids something concrete, and incompatible with a plausible plan that simply
adds a package — so it passes all three admission tests.

**Decision.** _To be recorded._

---

## N3 — no collision detector exists at allocation

**Open. Code affected.**

**Now.** `CycleId.Allocate` checks nothing. Directories are created later by whatever writes the
first artifact into them.

**Conflicts with.** §15 claims *"two cycles opened in the same second would collide; the
directory already existing is what catches it."* That claim is also written into `CycleId`'s own
doc comment, and it is false: at allocation there is no directory yet.

**Options.**

- **(a) The intent skill creates the directory atomically and fails if it exists.** Makes the
  claim true. One check, before anything is written.
- **(b) Allocate, then retry with the next second on collision.** Hides the collision; two cycles
  then differ by a second nobody chose.
- **(c) Drop the claim.** Honest, and leaves a real collision to surface as a confusing merge.

**Recommended.** (a), in the same slice as the intent skill.

**Decision.** _To be recorded._

---

## N8 — no skill prints a refusal

**Open. Code affected.**

**Now.** `cycle-approval` prints an approval entry only.

**Conflicts with.** The ledger supports `changes_requested` and `denied`, and §4.3's "a revisit
is an edit" assumes a printer exists. One half of the finding the code already answers: the
ledger does **not** require hashes on a refusal, so a denial can be hand-written.

**Options.**

- **(a) `cycle-approval --deny "reason"` prints a refusal entry.** Same print-don't-write
  pattern, one flag.
- **(b) Leave refusals hand-written.** Four fields; a person can type them. Cost: one decision
  has a printer and the other does not.

**Recommended.** (a).

**Decision.** _To be recorded._

---

## S11 — `verify-chain` checks half of what makes a plan binding

**Open.**

**Now.** Not implemented.

**Conflicts with.** §3.2 and §4.4 both state the comparison in two directions — every changed
file is declared, **and every declared file changed** — reported as `UNDECLARED` and
`UNDELIVERED`. §12's `verify-chain` list has only the first, so the undelivered half lives only
in the layer §2.1 marks as bypassable by a human with a shell.

**Options.** Implement both halves, or state deliberately that undelivered is a local-only
warning.

**Recommended.** Implement both; it is one more comparison over data already loaded.

**Decision.** _To be recorded._

---

## N1 — citation format for revision 1 is given two ways

**Open. Code affected, permissively.**

**Now.** `ConstraintId.TryParse` accepts `CON_01` and `CON_01.1`; `ToString()` emits revision 1
bare.

**Conflicts with.** §6.4 and `CONSTRAINTS.md` both say a bare id is revision 1, while both
worked examples cite `CON_04.1`.

**Options.** Declare bare canonical and fix the examples — which is what the code already does —
or declare explicit-revision canonical and change `ToString()`.

**Recommended.** Bare canonical. Less churn, and it reads better in a finding.

**Decision.** _To be recorded._

---

## Naming — `SolutionFileName` reads as `solution.md`

**Open. Code affected.**

`FileConstraintDocuments.SolutionFileName` is `"constraints.md"`. It means "the solution's
constraints file" and reads as `solution.md`. Rename to `SolutionConstraintsFileName` before
anything depends on it.

**Decision.** _To be recorded._

---

## S3 — the single gate is justified by something `CON_09` forbids

**Open. Prose rather than structural.**

**Now.** No code depends on either reading.

**Conflicts with.** §4.1 justifies the single gate because *"a person reading a plan is reading
the spec restated inside it."*

> **CON_09 — No artifact restates another's content as its own authority.**
> *Applies to:* every artifact.
> *Forbids:* an artifact reproducing a constraint, a criterion or an outcome that another
> artifact owns, instead of citing it.
> *Why:* two copies diverge, and nothing says which is binding. Citation keeps exactly one owner
> per fact, which is the only arrangement that stays true without maintenance.

**Assessment.** A plan citing a criterion to say how it will be proven *is* citation, and
`CON_09` forbids reproduction *as its own authority*. So the library is not violated; §4.1's
sentence is loose.

**Options.** Reword to "a person reading a plan is reading it against the spec it cites." No
design change.

**Decision.** _To be recorded._

---

## S5 — the gate binary and PowerShell 5.1 are mutually exclusive

**Open. Settled in code.**

**Now.** A .NET binary. No PowerShell anywhere in the implementation.

**Conflicts with.** §2.2 (one executable, carrying the schema evaluator, with iteration 3's
missing `Test-Json` named as the defect it fixes) against §11.3 (*"shipped tooling stays Windows
PowerShell 5.1"*, with the `Test-Json` consequence intact). §3.2 also says the diff-to-plan
comparison "already exists as `Compare-DiffToPlan.ps1`" — true of v03, not of this.

**One refinement to the finding.** It asks how a .NET binary meets the "must not fail to start"
bar. A .NET solution scaffolded by a .NET profile has the SDK by definition, so the bar is met
*for the stack the profile targets*. The honest statement the concept does not make:
**host-agnostic is not stack-agnostic.** The rules travel anywhere; the tool needs .NET.

**Options.** Delete §11.3's PowerShell paragraph and add that sentence, or keep PowerShell for a
thin shim and accept two implementations.

**Recommended.** The first.

**Decision.** _To be recorded._

---

## S7 — `batch-build` never pushes, and lands partial batches

**Open.**

**Now.** Not implemented.

**Conflicts with.** §9: *"Partial batches land. Successes are landed rather than discarded when a
sibling fails"*, against *"`land` is what touches the remote: it pushes, opens the pull
request… batch-build never pushes."*

**Options.** One word per meaning: `batch-build` *completes* a cycle locally; `land` pushes. Then
"partial batches complete" is unambiguous and nothing claims batch-build pushes.

**Decision.** _To be recorded._

---

## S8 — `after` lives in an artifact that does not exist when it is written

**Open.**

**Now.** Not implemented.

**Conflicts with.** §3.1.1 says splitting is cheapest early — after a spec exists, splitting
discards it — so the normal case is a split at intent stage. But the ordering a split produces is
recorded as `after: [...]` *in the plan's frontmatter*, and `batch-build` reads it from there.

**Options.** Move `after` to the cycle index, which exists for `batch-build` anyway; or to the
intent's frontmatter, with the plan inheriting it.

**Recommended.** The index. Ordering is a property of the cycle rather than of one artifact.

**Decision.** _To be recorded._

---

## S9 — the self-healing loop's exits have nowhere to land in a plan

**Open.**

**Now.** Not implemented.

**Conflicts with.** §5.0's **assumed** exit writes to an `Assumptions` section and **asked** to
an `Open` section, for all three artifacts. The plan's defined contents (§3.2) have neither; the
spec's equivalent is `blocked-on`; and §5.0's own trial narrative uses `Blocked on`.

**Options.** Define both sections on all three artifact templates under one spelling; or map per
artifact — intent `Open`, spec `Blocked on`, plan `Cannot prove` — and say so.

**Note.** This interacts with S2: if an artifact's open region is where unresolvable items live,
these sections stop being a loop detail and become load-bearing structure.

**Decision.** _To be recorded._

---

## S10 — the evidence contract is stack-bound

**Open.**

**Now.** Not implemented.

**Conflicts with.** §6.2 claims stack neutrality lets the library travel untouched, while
evidence is fixed as `diff.patch`, `build.log`, `tests.trx`, `base.sha` — and `.trx` is an
MSTest/VSTest format. `CON_08` and `CON_12` both derive from it.

> **CON_08 — A verify claim rests on evidence that exists and supports it.**
> *Forbids:* a statement in a verify document with no cited build output, test result or diff
> hunk — or one citing evidence that does not establish what the statement claims.

**The library is clean.** The shipped `CON_12` says "the recorded test results and diff", not
`tests.trx`. The concept's quoted copy names `tests.trx`, and `ShippedLibraryTests` would fail on
it, since its stack-word list includes MSTest.

**Options.** Declare the evidence *roles* neutral — a test result, a build log, a diff, a base
commit — and let the profile name the formats; or accept that evidence is profile-bound and say
so.

**Recommended.** The first, which keeps §1's "swappable" honest. Deferred in practice, since both
profiles are .NET.

**Decision.** _To be recorded._

---

## T12 — `adopted_at` is dropped three times and alive twice

**Open. No code impact** — `solution.md` is not implemented.

Dropped with evidence in §7.3 twice; still alive in §15's layout (`profile, origin, adopted_at`)
and in §16's sequencing note, which says a trial "closes the brownfield half of `adopted_at`" —
of a field that no longer exists.

**Options.** Delete both survivals.

**Decision.** _To be recorded._

---

## T13 — `MofonCode.Core` both ships analyzers and ships none

**Open. No code impact.**

§10.2 says the kernel ships "analyzers that keep a vertical slice in shape", then corrects itself
forty lines later from reading the package. Open question 1 then contradicts the correction by
putting new analyzers back in `MofonCode.Core`.

**Already decided in conversation:** custom analyzers are developed in the profile that declares
the convention.

**Options.** Fix the opening sentence and open question 1 to match the correction.

**Decision.** _To be recorded._

---

## T14 — `CON_10` binds a thing the design deleted

**Open. The library is already right.**

§6.5's table still reads "`CON_10` | a waiver's reason says why the finding is acceptable | a
waiver", and flags it as needing rewording. The shipped `CONSTRAINTS.md` is already reworded:

> **CON_10 — An accepted finding's reason says why the finding is acceptable.**
> *Applies to:* an approval decision that lists accepted findings.

So the concept is behind its own library. The `waived` disposition survives deliberately, now
meaning "accepted at the gate", and is documented that way in `Finding.cs`.

**Options.** Update §6.5's table to the shipped wording.

**Decision.** _To be recorded._

---

## N2, N4, N5, N6, N9 — smaller

| | Now | Conflicts with | Options |
|---|---|---|---|
| **N2** | layout shows four files in `.sdlc/` | §7 says "three files", and `README.md` has no stated mutability | add `README.md` to the mutability table, or say why it is outside it |
| **N4** | code uses `HookHost` for the agent host only | §11 defines "host" as the agent host, §12 reuses it for the CI platform | a different word for CI — "platform" or "forge" |
| **N5** | all-CLI | MCP is "the portability lever", `sdlc constraints` is "how the set reaches a session", and fresh readers use the host's `-p`; no precedence stated | state the CLI as the mechanism and MCP as a later surface over the same commands |
| **N6** | renderer is ASCII, asserted by a test | §13's example panel uses `·`, and the ledger example uses `…` | fix the examples; the code is already right |
| **N9** | — | §14 lists seventeen skills, none of which is the loop's command | name it, or say the loop is a mode of the stage skills rather than a skill |

**Decision.** _To be recorded._

---

# Decisions

Destined for `CONCEPT.md`'s appendix. Recorded here in final form as each item settles, so the
rewrite is assembly rather than authorship.

Ids are a monotonic sequence of their own, separate from `CON_nn`: a constraint judges an
artifact, a decision explains the framework. Assigned once, never reused. A reversed decision is
**superseded** by a new id rather than edited, so a commit message citing `D-02` still means what
it meant.

Each entry carries what it governs, the decision, the cost it avoids, and what was rejected. The
`Rejected` field is half the value: in six months the question is not "why this" but "why not
that", and this is what answers it without relitigating evidence already gathered.

## D-01 — Side research and evaluation run in a new process

**From:** S1. **Governs:** interrogation, the healing loop.

**Decision.** The main window keeps only what it needs to decide. Interrogation, fresh readers,
and any "go and find out X" step run in their own process.

**Why.** Research returns an answer and should not leave its search behind. An evaluation must
not have seen the thing it judges being made — which is the investment bias independence exists
to remove.

**Rejected.** Subagents for interrogation: better ergonomics on one host, two code paths for the
most important skill. In-process self-judgement inside the healing loop: removes the only thing
independence buys, and re-merges the two acts that judging-and-editing-are-separate exists to
split.

**Consequence.** The healing loop is orchestration — write, send out for judgement, apply, send
out again — rather than a thing that judges.

## D-02 — Constraints bind an artifact's body; its open region is the compliant exit

**From:** S2. **Governs:** the advance rule, the single gate, artifact structure.

**Decision.** A constraint judges what an artifact asserts. An item that cannot be resolved yet
moves out of the body and into the artifact's open region, with its context. That move is not a
waiver and not an acceptance — it is how the artifact becomes compliant.

**Why.** Read `CON_02` literally: it forbids *an intent stating as settled fact* a figure nobody
measured. An item recorded as an open question is not being stated as settled fact, so the
constraint is satisfied rather than overridden. `CON_02` asks for exactly this. Equally `CON_04`
forbids leaving an intent's *stated outcomes* uncovered, and an open question is not a stated
outcome — it is an unresolved question about whether there is one.

So the deadlock the contradictions document found was in the concept's prose, not in the
constraint set: the compliant exit existed all along, and the trial cycle used it without being
told to. Its spec recorded "a criterion for demonstrable impartiality — intent Open 3" under
`Blocked on`, and the cycle was approved and shipped.

**Consequence, and the reason this matters.** Clean-to-advance **stays**. An unresolvable item
moves to the open region, that *is* the fix, and the round goes clean. The rule is saved rather
than weakened.

**Rejected.** Advancing on a round merely existing: unnecessary once the compliant exit is
recognised, and it would let a plan be built on a spec with an unacknowledged hole. A pre-gate
acceptance record: two acceptance mechanisms, two places to look, and a second gate under
another name. Requiring a clean round only of the spec and plan: arbitrary, and the intent is
where the hardest items live.

## D-03 — An open item must be one nobody can resolve yet

**From:** S2. **Governs:** `CONSTRAINTS.md`, as `CON_14`.

**Decision.** Add `CON_14`:

> **CON_14 — An open item is one nobody can resolve yet, not one nobody has done.**
> *Applies to:* an intent, a spec or a plan, at approval.
> *Forbids:* an open item recording something the artifact's author could have established — a
> question answerable by reading the code, a decision the interviewee was never asked, or a
> criterion that is merely hard to write.
> *Why:* moving a claim out of the body into the open region is how an artifact becomes compliant
> honestly. It is also how one becomes compliant falsely, because a body with nothing in it
> breaks no rule. Whether each open item is genuinely unresolvable at this stage is the only
> control on that, and it needs a reader.

**Why.** `D-02` makes the open region a legitimate exit, which makes it an exploitable one. An
agent can empty the body into it and satisfy every constraint by having asserted nothing. This is
the only control on that, and no hook can decide it.

**Rejected.** Capping the number of open items: an artifact legitimately has several, and a cap
is a number nobody chose. Leaving the region ungoverned and relying on the gate: a person reading
a body with nothing in it has nothing to object to.

## D-04 — The plan gate is about dependency, not the absence of open items

**From:** S2. **Governs:** the gate, the spec's obligations.

**Decision.** The gate before build requires no *blocking findings*. It does **not** require that
no open items remain. What it requires instead is that nothing the change depends on is
unresolved — and that falls out of the spec disposing of every intent open item (`D-05`) plus
`CON_06` and `CON_12` requiring every criterion to have a proof that can run.

**Why.** A hard no-open-items rule would have blocked the trial cycle, whose plan carried
`Cannot prove` entries citing intent Open 2 and Open 3, was approved, built, and shipped a
working solution. Those questions did not affect what was built. False positives on a gate are
what make a team route around it.

With every intent item disposed, anything still open at plan time is by construction out of
scope or explicitly deferred: anything in scope got a criterion, and every criterion got a proof.

**Rejected.** "No blockers *or* open items at plan": blocks cycles that are fine, for a property
the existing constraints already deliver.

## D-05 — The spec disposes of every intent open item

**From:** S4, bridging S2. **Governs:** the structural checks.

**Decision.** Every item in the intent's open region is accounted for by the spec, in exactly one
of three places:

1. a criterion — resolved, in scope
2. `Out of scope`, with a reason — resolved, excluded
3. `Blocked on`, citing the intent item — deferred, explicitly

This **replaces** the structural check "every intent statement maps to at least one spec
criterion", which is dropped.

**Why.** The dropped check cannot exist: `CON_04` is in the library only because no analyzer,
schema or hook can decide intent-to-spec coverage, and a mechanical check that decided it would
fail `CON_04`'s own admission test. Disposal is a different question — *presence*, which a parser
decides — and it does not overlap `CON_04`'s judgement about whether what was written actually
covers the outcome. It is also what makes `D-04` true.

The trial spec already does this: six `Out of scope` entries with reasons, two `Blocked on`
entries citing intent Opens by number.

**Rejected.** Keeping the coverage check and removing `CON_04` from the library: coverage needs
reading comprehension, which is why it was a constraint. Keeping both: they are the same
judgement, and `CON_11` exists to stop the library holding a rule that buys nothing.

**The three structural checks are therefore:**

1. every intent open item is disposed of by the spec *(new)*
2. every spec criterion maps to at least one file and one named test in the plan *(unchanged)*
3. every changed file is declared in the plan, and every declared file changed *(unchanged — see
   S11 for the second half)*

## D-06 — An artifact section earns its place by having a consumer

**From:** the section inventory, taken while working S9. **Governs:** artifact structure.

**Decision.** A section exists because something reads it: a constraint, a mechanical check, a
downstream skill, or a person at the gate. A section with no consumer of any kind is decoration,
and decoration costs attention at every interrogation.

This is the same admission test the constraint library applies to itself, pointed at structure
instead of rules — and `CON_11` is the precedent: *a rule that forbids nothing changes no plan and
no diff. It costs attention at every interrogation and buys nothing.*

**Applied.** Four changes, from auditing every section against that test:

| Section | Verdict | Change |
|---|---|---|
| intent `Who` | no consumer of any kind | **dropped.** `Problem` now owns it — a problem is someone's problem, and the sentence belongs in the sentence that states it |
| plan `Order` | no mechanical consumer; `build` follows it and nothing checks it | **folded into `Files`** |
| plan `Risk` | no machine consumer; the person at the gate is a real one | **kept** |
| intent `Done looks like` | consumed by `CON_04`, but the name invites being mistaken for a spec concern | **renamed `Wanted outcomes`** |

**Why, per change.**

*`Who` dropped.* Nothing read it. The implicit value — that an intent with no "who" is often a
solution looking for a problem — was never checked by anything, so it was a heading that made an
artifact look thorough without making it truer. **The cost to watch:** `Problem` has to carry who
has it, or the intent gets vaguer rather than leaner.

*`Order` folded.* It is a property of the files rather than a separate subject. **The cost to
watch:** the trial's `Order` held reasoning, not a list — *"central package management has to exist
before the first restore, or artefacts land in the diff verify reads"* — and that paragraph
prevents a build failure. A bare column in a table has nowhere to put it, so `Files` needs a place
for ordering reasoning, as a lead-in or a note per row. Folding that loses the reasoning would be
a worse outcome than leaving the section alone.

*`Risk` kept.* The trial's entry named the antiforgery-cookie problem and said *"criterion 4's
test is the detector"*, which is the most useful thing in that plan for a person deciding whether
to approve. "Unused" has to mean *nothing reads it and dropping it loses nothing*; this fails the
second half.

*`Done looks like` renamed.* It is `CON_04`'s left-hand side: *"a spec covers every outcome its
intent asks for"* is vacuous if the intent states no outcomes. So it cannot move to the spec
without killing the constraint that catches a dropped must-not-change item landing green. But the
name reads as a definition of done, which is a spec concept, and that is what invited the question.
`CON_04` refers to "stated outcomes" generically rather than naming the section, so the rename
needs no library change.

**Rejected.** Dropping `Risk` as unused: it has a consumer, just not a mechanical one. Giving
`Risk` a constraint — *a named risk names its detector* — which would be admissible and
stack-neutral; deferred because the section is young and a constraint written before the practice
settles tends to describe the first example rather than the rule. Moving `Done looks like` to the
spec: kills `CON_04`.

**The resulting shape.**

| artifact | body | open region |
|---|---|---|
| intent | `Problem`, `Wanted outcomes`, `Fixed decisions`, `Must not change` | `Open` |
| spec | `Acceptance criteria`, `Out of scope` | `Blocked on` |
| plan | `Shape and departures`, `Files`, `Proof`, `Risk` | `Cannot prove` |

**And the separation this makes explicit.** Findings are not artifact sections. Blockers,
suggestions and advice live in `findings/<artifact>.r<round>.json` with their own schema, produced
by a different process, *about* the artifact and never *in* it. `CON_09` requires that: an artifact
carrying its own blockers would be a second record of every finding, and the gate would read one
while the person read the other.

## D-07 — An artifact is an envelope carrying a body

**From:** S9, after naming the pattern. **Governs:** artifact structure, the schemas, the gate's
reader.

**Decision.** Every artifact has the same two-part shape.

| Part | Holds | Same across artifacts | Governed by |
|---|---|---|---|
| **envelope** | frontmatter — id, upstream reference, framework version — and the `Open` region | yes, identically | the envelope schema; `CON_14` governs `Open` |
| **body** | the artifact-specific sections | no, deliberately | a body schema per kind; the constraints bind this |

**The test for where a field goes.** Same on every artifact, with the same meaning and the same
consumer, goes in the envelope. Differs per artifact because a different consumer reads it, goes
in the body. Applied each time rather than assumed — see the risk below.

**Why.** It is a name for the split `D-02` and `D-06` already arrived at, and it makes the
governance rule one sentence: *constraints bind the body; the envelope carries what the artifact
does not assert.* It also puts the seam in the code where it belongs — an envelope reader is
artifact-agnostic, so per-artifact knowledge is isolated to one place instead of being repeated
wherever a document is parsed.

**And it removes duplication the schemas would otherwise carry.** Iteration 3 shipped four
schemas — intent, spec, plan, verify — each restating the common frontmatter keys. That is `CON_09`
applied to schemas: four copies of one shape, diverging the first time one gains a field the others
do not. The envelope pattern makes it one envelope schema plus one body schema per kind, so the
common part has a single owner.

**The reader is a component of the gate, not a skill.** Splitting a document on headings,
extracting a region and validating a shape is deterministic, and §2.2 settled that the gate is a
program rather than a prompt. So `sdlc artifact <id> <kind> --body | --open` hands a skill what it
needs, and no skill parses a document. A secondary benefit: the gate can report a heading that
belongs to neither the body nor the envelope, which catches an artifact that invented or
misspelled a section.

**Rejected.** An artifact reader as a skill: deterministic work in a prompt can be wrong in ways
nothing catches. A single monolithic schema per artifact: what iteration 3 had, and what put four
copies of the envelope in the repository.

**The risk to watch.** Naming a pattern invites stretching it. The failure mode is an envelope that
accumulates fields which are uniform only because somebody forced them, so every artifact carries
something three of them do not need. The test above is the guard; the pattern's gravity is not.

## D-08 — The open region is `## Open`, on every artifact

**From:** S9. **Governs:** artifact structure, the interrogation skill.

**Decision.** One name on all three artifacts. `Blocked on` and `Cannot prove` are retired into it.
The rule a skill gets is one sentence with no mapping: **everything except `## Open` is judged
against the constraints and against the artifact before it.**

**Why.** A three-name mapping is free for the gate — three entries in tested code — and not free
for the interrogation skill, which is a model reading a prompt. It will usually apply a mapping
correctly. When it does not, the failure goes one of two ways: it raises a finding about something
correctly recorded as open, which is annoying and self-correcting; or **it fails to judge something
in the body**, having mistaken that section for the open region. The second is silent, and it is
the exact failure the design exists to prevent — an artifact that passed because nothing looked at
it. An ambiguity whose failure mode is "the body went unjudged" is not a papercut.

**And the three names bought less than they appeared to.** The information is in the entries, not
the headings. The trial's *"Anything about reachability from another machine. Blocked on intent Open
2"* says it is blocked and names what by — and `D-05` requires that citation, so the heading is
redundant with it. *"That the production roller is impartial. Criterion 2 and criterion 5 are proven
through a substituted source…"* says it is an evidence limit. The headings labelled entries that
already declared their own kind.

This also resolves S9 rather than papering over it: §5.0 said the loop's exits write to `Open`, and
that becomes true on all three artifacts. The concept was right and the artifacts were inconsistent
with each other.

**Rejected.** Keeping the three names with a mapping in the skill's prompt — the silent-false-negative
risk above. Keeping them with the gate extracting the region, which removes the risk but leaves
three words for one role and a mapping to maintain for no remaining benefit. Adding `Assumptions`
and `Open` to all three as §5.0 implies — eight sections on a plan, and it separates an assumption
from the decisions it sits beside, which is where a reviewer looks.

**The cost, and where it is paid.** `## Cannot prove` is a better *prompt for an author* than
`## Open`: someone writing a plan under a heading called `Open` may not think to record a limit of
the proof there. The artifact template carries that instead — `## Open`, with a line saying what
belongs: questions nobody has answered, criteria that cannot be written yet, and limits of the
proof.

**Assumptions.** An assumption is not an open item — it is a provisional answer rather than the
absence of one — so it does not go in `Open`. It goes in the artifact's decisions region marked
unconfirmed: `Fixed decisions` on an intent, `Shape and departures` on a plan. A spec has neither
and needs neither: healing a spec is rewriting from an approved intent, so the information already
exists, and inventing a criterion would breach `CON_05`.

## D-07a — Correction to D-07, from auditing the trial's frontmatter

**D-07 named the envelope as "frontmatter — id, upstream reference, framework version — and the
`Open` region". That describes no artifact that exists.** The audit:

| Field | intent | spec | plan | verify | Verdict |
|---|---|---|---|---|---|
| cycle id | `id` | `id` | `id` | **`cycle`** | universal in meaning, spelled two ways |
| `framework_version` | — | — | — | ✓ | universal in meaning, on one of four |
| upstream reference | — | `intent:` | `spec:` | `spec:`+`plan:` | **not universal** |
| `type` | ✓ | — | — | — | intent only |
| `tier` | — | — | ✓ | — | plan only |
| `outcome`, `evidence`, `remediation`, `findings` | — | — | — | ✓ | verify only |

The common part was a cycle id under two names. I stated D-07's own test — same on every artifact
with the same meaning and consumer — and then violated it in the same decision, by generalising
from three files without opening the fourth. The test needs applying rather than assuming, which is
the risk D-07 records, arriving at birth rather than by accretion.

**The envelope, corrected.** Two fields and one section:

```
framework_version: 1.0.0
cycle: 20260929_1456_32        checked against the path

## Open
```

- **`framework_version` — kept.** Universal, with a real consumer: a format migration must know
  what it is reading. Per file rather than per cycle, because a cycle written over days can span a
  version bump.
- **`cycle` — kept, and checked against the path.** The path already carries it, so it is a second
  copy of what the path owns. Kept because an artifact does get read out of context — pasted into a
  review, attached to a work item — and because a duplication with a check on it is a different
  thing from one without. Spelled `cycle`, not `id`: `id` invites "the id of what?", and `verify.md`
  already chose the better word.
- **upstream reference — dropped.** It fails twice: not universal, since the intent has none; and
  derivable, since the chain order is fixed and the gate finds the artifact before any other by
  kind. Nothing reads it that could not derive it. The trial wrote a path, which is a third copy of
  what the layout already determines.
- **`## Open` — required even when empty**, or the gate cannot tell "nothing is open" from "the
  author forgot the section".

**`type` and `tier` go in the body, and that is the finding that killed the metadata bag.**

A `metadata: { }` bag in the envelope was considered, to keep the body purely evaluated content.
Rejected, because **both candidates for it turned out to be claims rather than bookkeeping** — and a
claim in the envelope escapes the constraints, since interrogation judges the body.

- `type` is the change classification. An intent classified as a bug fix with no reproduction is
  wrong, which is judgeable.
- `tier` is the plan's blast radius — `local`, `contract`, `data`, *"chosen by reversibility, not by
  size. A one-line change that drops a column is `data`."* v03 required a sentence saying why the
  tier is what it is, *"and that sentence is what a reviewer checks"*. Judgeable by construction.

Three further reasons against a bag, independent of its contents: an open bag defeats the schema,
and v03 set `additionalProperties: false` deliberately — *"the schema rejects anything extra, so an
extra key makes the file invalid rather than merely untidy"*; a bag institutionalises exactly what
`D-06` just removed, by giving fields with no consumer somewhere to live unexamined; and "custom"
names no owner, which is the one question `D-06` asks.

**The real need underneath it, recorded and deferred.** A team will want to stamp a ticket number or
a cost centre on every artifact, and with nowhere to put it they will use the body, where
interrogation will object. The shape that serves that without the costs is a **declared extension**:
the solution declares which extra keys its artifacts carry, and the envelope schema validates
against the declaration — so the field has an owner, the schema still validates, an undeclared key
is still an error, and adding one means declaring it. Same pattern as `LOC_nn`. **Deferred:** no
solution needs it, and a mechanism with no user is how a bag gets built badly under time pressure
later.

**The body is flat.** A nested `body.<evaluated content>` was considered and rejected: once `type`
and `tier` are in the body, the body is *entirely* evaluated content, so a slot inside it for "the
evaluated part" is indirection around nothing. `D-06` already established that every body section
has a consumer, so there are no leftovers to separate.

**Candidate constraint, noted not written.** `tier` is worth recovering because blast radius chosen
by reversibility is the best single signal for how carefully a person should read a plan. v03's
"say in one sentence why" was already this rule in prose:

> **CON_15 — A plan's blast radius matches what it changes.** *Forbids:* a plan claiming the lowest
> tier for a change something outside the repository depends on, or that reverting the code does not
> undo. *Why:* the tier tells a reader how carefully to read, and an understated one buys a cursory
> review for the change that most needed a careful one.

Held until `tier` is in use again, so the constraint describes the rule rather than its first
example.

## D-08a — `Open` is kept as the name

**Explored and kept.** The region holds *something in scope that this artifact deliberately does not
assert, with the reason it cannot* — not work nobody has done (a later cycle), not something wrong
(a finding), not something chosen out (`Out of scope`).

**Rejected, with reasons.** `Open questions`: two of the three entry kinds are not questions — a
criterion that cannot be written and a proof that cannot reach — so it would misname two artifacts
and reintroduce what `D-08` removed. `Unresolved`: implies it could have been resolved and was not,
which points at "nobody has done it" rather than "nobody can yet". `Deferred`: implies a later, and
the plan's evidence limits have none. `Limits`: collides with `Must not change` and with
"constraints". `Cannot settle` and `Unsettled`: both resist backlog content better — `Cannot settle`
forces the author to assert impossibility, which is the trick that made v03's `Cannot prove` a good
name, and which would have the name and `CON_14` pointing the same way instead of the constraint
spending itself undoing the heading.

**Kept anyway** because it is short, already written in the trial, the concept and `D-08`, and a
short heading is the one people type. The cost is accepted: `Open` carries a backlog connotation
from software vernacular, so `CON_14` does the work the name does not — *"an open item is one nobody
can resolve yet, not one nobody has done"* — and the artifact template states what belongs there.

## D-09 — `solution.md` is protected, and a profile change is drafted for a person to place

**From:** S6. **Governs:** the hook's protected set, §7's mutability table, the gate's command
surface.

**Decision.** The hook denies every agent write to `solution.md`, unconditionally — the same
treatment as `approval.md`, `abandoned.md` and `constraints.md`. A profile migration is a one-line
edit a person makes, and the gate **drafts** the line for them to place.

**Why.** The file holds two keys with opposite natures after `D-07a`: `origin` is a historical fact
that must never change, and `profile` is a choice that can legitimately be revisited. Protecting
both and hand-editing the rare case costs one manual line per profile migration. Every alternative
either exposes `origin` — and an agent that rewrites it from `adopted` to `scaffolded` changes how
every later intent reads the repository — or introduces the framework's **first conditional hook
rule**, which is where a bypass would live.

What that buys, and it is worth more than it looks: **every hook rule stays absolute.** Four files,
no exceptions, no modes, no diffs to inspect. The whole rule set states in one sentence, and there
is nowhere for an exception to hide.

**Rejected.** Per-key protection: the hook receives a path and sometimes the new content, never a
before-and-after, so it would need the gate to read the current file and compare — real machinery,
and the first conditional in an otherwise absolute set. Leaving it unprotected and catching drift
at the gate: makes `origin` agent-rewritable, which is the one thing the file exists to hold.
Splitting into two files: a third file in `.sdlc/` for two keys, and N2 already records that the
file count does not match the layout.

### The pattern this completes, and the naming it needs

Four protected files now follow one rule: **a tool never writes them; a tool drafts what to put in
them and a person places it.**

| File | What is drafted |
|---|---|
| `approval.md` | the gate decision entry — three hashes, timestamp, accepted findings |
| `abandoned.md` | why the cycle stopped (N8 — no printer exists yet) |
| `constraints.md` | a proposed `LOC_nn`, from `init`'s interview |
| `solution.md` | the `profile` line, on migration |

**That is one verb, not four commands.** The surface is already drifting — `cycle-approval` fits no
pattern, and three more ad-hoc names would make four shapes for one idea. Proposed:

```
sdlc draft approval <id>
sdlc draft abandon <id> --reason "..."
sdlc draft profile <name>
sdlc draft constraint ...
```

`draft` is the right word: it says the output is not final, which is exactly the contract — the
person reads it, decides, and places it. It also renames `cycle-approval` to `draft approval`
before four commands exist with four conventions.

**Code consequences, for when we next touch code.** Add `solution.md` to `ProtectedPaths.Names`
(one line plus a test). Rename `cycle-approval` to `draft approval`. Add `draft abandon` (N8) and
`draft profile`.

## D-10 — The intent skill creates the cycle directory, and fails if it exists

**From:** N3. **Governs:** cycle allocation, §15's collision claim, §8.6.

**Today.** `CycleId.Allocate` builds an id from the clock and never touches the disk. The directory
appears later, written by whatever puts the first artifact in it. Nothing checks for a collision.

**The conflict.** §15 and `CycleId`'s own doc comment claim *"the directory already existing is what
catches it"*, while §8.6 and §15 say *"directories appear when the skill that writes the first file
into them runs; nothing is pre-created."* At allocation there is no directory, so the stated safety
net does not exist and a collision surfaces much later as two branches with one name.

**Decision.** `intent` creates the cycle directory as its first act and fails if it is already
there. Creating it **is** the check, and the filesystem makes that atomic.

**Why.** It makes the claim true rather than deleting it, and it closes the race that a
check-then-write would leave open.

**Rejected.** Deleting the claim and accepting a later surface: a collision then appears as a
confusing merge instead of a clear refusal. Checking the disk at allocation without creating
anything: leaves a gap between the check and the write, which is the race the atomic create avoids.

**Amendment this needs.** §8.6's *"nothing is pre-created"* stays, with its scope made explicit: it
forbids making directories *speculatively*, ahead of the step that owns them. A directory created by
the step that writes into it is not pre-creation.

## D-11 — A refusal is drafted too

**From:** N8. **Governs:** the `draft` surface.

**Today.** The approval printer drafts an approval only. A person sending a cycle back has no draft
to work from, and would hand-write the YAML.

**The conflict.** §4.2 gives the ledger three decisions — `approved`, `changes_requested`, `denied` —
with a worked `changes_requested` example, and §4.3's *"a revisit is an edit"* assumes a first entry
exists to revisit. But nothing drafts a refusal, which is the *easier* of the two since a refusal
carries no hashes.

**Decision.** `draft approval <id> --deny "reason"` and `--changes-requested "reason"` print a
refusal entry.

**Why.** Rejecting is what a person does when they are least inclined to look up a format. Giving
help to the easy case and withholding it from the hard one is the asymmetry people resent.

**Rejected.** Leaving refusals hand-written: four fields is writable but the asymmetry is the
problem, not the length. Dropping `denied` and keeping two states: loses the difference between
"fix this and come back" and "no, not this way", which is real at a gate.

## D-12 — `SolutionFileName` becomes `SolutionConstraintsFileName`

**From:** the naming wart. **Governs:** `src/Sdlc.Core/Constraints/ConstraintDocuments.cs`.

**Today.** `public const string SolutionFileName = "constraints.md";` — it means *the file name of
the solution's own constraints* and reads as *the solution file's name*, which anyone would take to
be `solution.md`.

**The conflict.** Code against itself: the name says one file, the value is another. It matters more
after `D-09`, because `solution.md` is now protected and handled in the same code path — so the next
person adding it to the protected set picks the wrong constant out of autocomplete and the hook
guards the wrong file.

**Decision.** `SolutionConstraintsFileName`.

**Why.** It matches the tier's own name: the enum is `ConstraintTier.Solution` and the renderer
prints "solution".

**Rejected.** `LocalConstraintsFileName`: reads well, but introduces a second word for a tier
already called "solution". A comment instead of a rename: a comment does not reach someone picking
a constant out of autocomplete.

## D-13 — Evidence is normalised at the boundary, not read in its native format

**From:** S10. **Governs:** `build`, `verify`, §3.3, §6.2, `CON_08`, `CON_12`.

**Today.** Unimplemented, but the concept fixes evidence as `diff.patch`, `build.log`, `tests.trx`,
`base.sha`, and the trial produced exactly those. `tests.trx` is the VSTest/MSTest result format.

**The conflict.** §6.2 says no constraint may name a language, framework or package, and that
neutrality *"is what lets the whole library survive the move to other hosts untouched"*; §1 says the
stack opinion is swappable. But `CON_08` and `CON_12` take their meaning from an evidence set whose
test result comes from one test platform — so the opinion is not swappable at the exact point the
verdict depends on.

Supporting evidence: the **shipped** `CON_12` is already neutral — *"verify derives its verdict from
the recorded test results and diff"* — while the **concept's quoted copy** of it names `tests.trx`.
`CONSTRAINTS.md` is clean and `CONCEPT.md` is not, and `ShippedLibraryTests` would fail on the
concept's version.

**Decision.** `build` writes **both** what the toolchain produced and one framework-defined summary.
`verify` reads only the summary. The profile supplies the converter, and it runs in the build step,
which is stack-specific by definition — it is the thing that runs `dotnet test`.

The summary is minimal: **test name and outcome.** That is all `verify` needs, since it maps
criterion to test name to result. Anything more is speculation.

**Why.** **The gate must not know about test platforms.** Reading evidence in its native format
would put a `.trx` parser inside the gate binary, then a JUnit parser beside it, and the thing that
owns the *process* constraints starts owning *file formats* — which is not what §2.2 claims for it.
Normalising at the boundary puts the stack-specific part where the stack already is.

Keeping the original alongside the summary matters for `CON_08`: both exist, so a claim can cite
either, and a person can still read what the toolchain actually wrote.

**Rejected.** Roles with a per-profile reader in the gate: stack-specific code inside the
stack-agnostic binary. Accepting that evidence is profile-bound and narrowing §6.2's claim: honest,
but it reduces "swappable" to "swappable within .NET". Deferring because both profiles are .NET:
leaves the concept holding a claim it cannot support, which is the class of defect being removed.

## D-14 — `verify-chain` checks the comparison in both directions

**Superseded by D-15.** Kept verbatim: a decision is superseded, never edited.

**From:** S11. **Governs:** §12, `verify-chain`.

**Today.** Unimplemented. The comparison itself existed in v03 as `Compare-DiffToPlan.ps1` and
reported both directions; the trial's verify printed *"MATCH - every changed file is declared, and
every declared file changed."*

**The conflict.** §3.2 and §4.4 both state it two ways — *"every changed file must be declared and
every declared file must have changed, reported as `UNDECLARED` and `UNDELIVERED`. That check is
what turns the plan from a description into a commitment."* §12's `verify-chain` list has only the
first. Since §2.1 marks the local layer *"bypassable by a human with a shell"*, the undelivered half
would be enforced only where it can be bypassed.

**Decision.** Both directions, both failing.

**Why.** The failure it catches is concrete: a plan declares five files, build writes three, and
verify passes because every changed file was declared — so two files were promised and silently not
delivered. That is a cycle landing green having done part of the job, which is what `CON_04`
prevents one artifact earlier. And it costs nothing: the check already loads the plan's file table
and the diff.

**Rejected.** Undelivered as a warning: a warning in a pipeline is a thing nobody reads, and it
makes "I forgot to deliver" indistinguishable from "I over-declared". The legitimate over-declaring
case is not an exception to handle — it is a plan that was wrong about its own blast radius, and
saying so is the point; the honest route is a corrected plan, which the hash check already forces.
Keeping it local-only and amending §3.2: gives up the strongest mechanism in the design to avoid
writing one comparison.

## D-15 — The declared-versus-changed comparison is a signal, not a gate. Supersedes D-14.

**From:** G1a. **Governs:** §12 `verify-chain`, §3.2, `verify`'s findings.

**Today.** Unimplemented. D-14 decided both directions fail; this reverses that with better
reasoning, and takes a new id rather than editing D-14 because a commit citing D-14 must still mean
what it meant.

**What blocks instead.** Build fails, tests fail, or the proof is not met — *every criterion maps to
a named test and that test passed.* Outcome-based rather than process-based: it asks whether the
change did what the spec required, not which files it happened to touch.

**Decision.** Both directions are recorded as suggestions, with wording that distinguishes them:

| | Meaning | What the note says |
|---|---|---|
| **UNDELIVERED** | the plan declared a file nothing changed | the plan's blast radius was wider than the change |
| **UNDECLARED** | a file changed that the plan never declared | this reached the diff without the hook seeing it — check why |

**Why — and this is the correction.** `CON_07` already says the file set is not its job: *"the file
set is mechanical and a hook already enforces it. Creep **inside** an approved file passes that hook
and reaches review dressed as approved work."* The constraint exists **because** the file-set check
cannot see inside a file. So the verify-time comparison was never load-bearing for scope creep — it
is a cross-check on a prevention that already happened.

And the counterexample I had argued with dissolves. *The plan said modify `AuthService.cs` to add a
permission check, build did not touch it, every test passes because no test covers the check.* Trace
it: if a criterion required that check, the plan's `Proof` mapped it to a test. If that test passes
without the file changing, the proof was inadequate — which is `CON_06`, caught at the **plan gate,
before code existed.** The undelivered file is a symptom; the inadequate proof is the defect.

The comparison's real uses are the ones it is now kept for: estimating blast radius, and best-effort
detection of file collisions for `batch-build`'s claim graph.

**Rejected.** UNDECLARED blocking while UNDELIVERED does not, on the grounds that a guard failed:
defensible, and rejected because a noisy blocker gets routed around, and the three ways an
undeclared file can appear — a human hand-edit, a checked-in generated file, an uninstalled hook —
all show in the pull request diff.

**What this gives up, recorded rather than waved past.** The claim *"no file reached this change
without a reviewer"* moves layers. The trial asserted it on the strength of a blocking check; now the
hook asserts it and verify confirms. §2.1 marks the hook as *"bypassable by a human with a shell"*
while `verify-chain` is the non-bypassable layer — so a person who hand-edits an undeclared file can
land it with a note rather than a refusal. Acceptable and consistent: a human acting deliberately in
their own repository, with the diff in the pull request, is the case the code-owner review exists
for.

**And a second cost.** If plans routinely over-declare with nothing pushing back, claim sets drift
wide, and wide claims over-serialise cycles in `batch-build` that did not need ordering. A
performance cost rather than a correctness one, and self-correcting — someone notices builds running
one at a time. Under-declaration stays prevented by the hook.

**The insight underneath, which belongs in the concept.** The plan holds two different kinds of
claim, and treating them alike is what made a guess feel like a commitment:

| Section | Kind | Judged by |
|---|---|---|
| `Shape and departures` | a **decision** — which approach | `CON_06`, `CON_13` |
| `Files` | an **estimate** — which files, how large | nothing; it is a signal |

You do not block on a wrong estimate. You block on a wrong decision.

## D-16 — There is no `design` skill. The stages are the human's decision points.

**From:** exploring a combined intent-spec-plan skill. **Governs:** the skill inventory.

**Decision.** No skill runs intent, spec and plan as one invocation.

**Why.** The separate commands are not code organisation — they are where a person decides to
proceed. Even a combined skill that interviewed at every stage would remove the moment where you
stop, read the intent, and *choose* to go on to the spec. Human-in-the-loop is not only "was a
person asked"; it is "did a person decide to continue", and a chained command answers the first and
skips the second.

**Rejected.** A chained orchestrator that interrogates between stages — which would preserve the
constraints, since every round still runs in a fresh process and every clean round is still
required. Rejected on the decision points, not on the constraints.

**Noted as the cost that made it tempting:** a late idea otherwise costs a chain re-run driven by
hand, stage by stage. D-17 addresses that cost directly instead.

## D-17 — `steer` re-runs the chain from an existing intent and spec

**From:** the steering question. **Governs:** the skill inventory, the approval ledger.

**Decision.** `steer <id> "comment"` interviews, records the decision in the intent's
`Fixed decisions` with attribution, then carries the change down the chain — updating and
re-interrogating the spec, and the plan when one exists.

**It requires an intent and a spec.** It refuses when only an intent exists: one document to edit
and one command to re-interrogate needs no orchestrator, and a skill that refuses when it is not
needed beats one that does trivial work.

**Why.** The correct path by hand — change the intent, re-interrogate, update the spec,
re-interrogate — is expensive enough that people will not pay it, so the steering ends up in
conversation where it is lost. **A path so costly that it pushes information out of the system is
worse than no path.** `steer` keeps the correct path and makes it affordable, which is the right
kind of flexibility: **compliance gets cheaper and no constraint loosens.** Every round still runs
in a fresh process (D-01), every clean round is still required (D-02), and the gate is still one
manual paste.

**Five things it must do.**

1. **Report what changed.** A one-line comment can cascade into a dropped criterion. Without
   "this changed the intent's fixed decisions, which rewrote criterion 3 and dropped criterion 6",
   it is a black box that eats a spec.
2. **Edit, never regenerate.** Regenerating a spec from the new intent discards hand-written
   refinement, earlier rounds' work, and accepted findings. Edit, and let the interrogation catch
   what went stale.
3. **Refuse on an approved cycle** until a reopen is recorded — D-19.
4. **Steering changes how, not what.** A comment that changes the problem is a different intent,
   not a steer. CON_01 would fire anyway if the problem were replaced by an implementation, but the
   skill should not need the constraint to tell it.
5. **State the cost up front.** Three interrogations minimum, each a fresh process, several minutes
   apiece on the evidence of the eleven-round intent.

**Rejected.** A `steering.md` input file, which D-17 makes unnecessary: with `steer` interviewing
and the result landing in `Fixed decisions`, no timing gap is left to cover and no input file's
status needs deciding. An `advice` finding alone: it needs an artifact to attach to, and the
scenario that started this is steering that arrives before a plan exists. A proposed implementation
in the spec: a spec's criteria must derive from the intent, so an implementation proposal appearing
there derives from nothing, which is what CON_05 forbids.

## D-18 — A stage skill refuses when its artifact exists, and names what you meant

**From:** the re-run question. **Governs:** `intent`, `spec`, `plan`.

**Decision.** Running a stage skill when its artifact already exists **refuses**, naming the two
commands you probably wanted: `interrogate <id> <artifact>` to judge it, or `steer <id> "..."` to
change it.

**Why.** The safety matters — typing `intent <id>` must never silently overwrite an intent. And a
flag that re-interrogates would be a second way to do what `interrogate` already does: two owners
for one behaviour, and the kind of pair where the one people type is the one that drifts. A refusal
that teaches the right command adds nothing and removes a mode that could destroy work. It is also
the shape `init` already uses — "If `.sdlc/` exists, stop and say so."

**Found while drafting D-20's table:** `spec` and `plan` need this too, not only `intent`. Nobody
had stated it, and without it re-running either silently overwrites.

## D-19 — `reopen` is a fourth decision, and it is how a wrong plan is recovered

**From:** G1's unanswered half, reached again through the approval-tracking concern.

**Decision.** The ledger takes a fourth decision value, `reopened`, drafted and pasted like any
other. It supersedes the approval, so the cycle is pre-approval again: `refine` and `steer` work,
the documents are rewritten, re-interrogated, and re-approved with a new entry. The ledger then
reads as the whole history — approved, reopened, approved — each entry with its own three hashes.

**Why.** Without it a wrong plan has no legal move. `remediate` may not edit a planning document
(§2.4's corollary: a red test must not rewrite the spec), `refine` refuses once the cycle is
approved, and the plan is hash-locked. So `remediate` could only keep trying to make wrong code
satisfy a wrong plan until it escalates.

It also gives the design the symmetry it wants: **code failures are capped** at three passes,
because an unbounded fix-verify loop burns tokens with nobody watching; **artifact failures are
not**, which is the eleven-round lesson.

**Rejected.** Abandoning and starting a new cycle: throws away an intent and spec that are probably
fine, and a wrong plan does not mean a wrong problem. Letting `remediate` edit the plan when verify
is blocked: the inversion §2.4 exists to prevent, where the plan becomes whatever the code can do.
Hand-editing the plan and relying on the hash mismatch to demote the cycle: the mechanism works but
leaves no record of why it was reopened, and `refine` still refuses so no help is available for the
edit.

**A stuck cycle therefore has three distinct moves:** reopen the plan, abandon the cycle, or
escalate for a person to choose between them.

## D-20 — Each skill's preconditions are stated in one table

**From:** the "how do we know each skill checks the right artifacts" concern.

**Decision.** The concept carries one table rather than scattering preconditions through skill
prose. `CycleStatus` already computes most of what it reads.

| Skill | Requires | Refuses when | Writes |
|---|---|---|---|
| `init` | a git repository, no `.sdlc/` | `.sdlc/` exists; not a repository | the scaffolding |
| `intent` | nothing | an intent exists (D-18) | `intent.md`, creating the cycle directory (D-10) |
| `spec` | a clean intent | intent missing or not clean; a spec exists | `spec.md` |
| `plan` | a clean spec | spec missing or not clean; a plan exists | `plan.md` |
| `interrogate <id> <artifact>` | the artifact exists | it does not | a findings round |
| `refine <id> <artifact>` | a round with open blockers | no blockers; cycle approved | that artifact only |
| `steer <id> "..."` | an intent **and** a spec | only an intent; approved without a reopen | intent, spec, plan — whichever exist |
| `draft approval` | three documents, each with a round | any missing or uninterrogated | nothing; it prints |
| `build` | the gate satisfied | the gate refuses | code, evidence |
| `verify` | evidence exists | no evidence | `verify.md` |
| `remediate` | verify blocked | not blocked; three passes spent | code and tests only |
| `land` | verify passed | not passed; escalated | push, pull request |

**Why.** Its absence is what lets a skill quietly check the wrong precondition. Drafting it surfaced
two rules nobody had written: `spec` and `plan` refuse on an existing artifact (D-18), and `refine`
refuses on an approved cycle — decided in passing and never recorded beside the others.

## D-21 — A findings round records the upstream hash it judged against

**From:** the chain-hash idea. **Governs:** the findings schema, D-02's clean-round rule.

**The gap.** The approval proves *these three documents, as they are now, were approved.* It does
not prove *the spec was derived from this intent.* So: intent v1; spec written from it, clean;
intent edited to v2 and re-interrogated clean; plan written, clean; approve — every hash matches on
disk, the gate passes, and **the spec was derived from an intent that no longer exists.** Nothing
requires re-interrogating a downstream artifact when its upstream changes, because D-02 gates a
stage before it runs rather than after.

And it is not hypothetical: **D-17 is the mechanism that creates it.**

**Decision.** A findings round records `upstream_sha256` — the hash of the artifact above it, as the
round saw it. A round is clean **as of** the upstream it read, so changing the upstream makes the
round stale, the stage gate fails, and re-interrogation is required.

**Why here rather than in the artifact's frontmatter**, which was the original proposal:

- **It cannot lie.** An `intent_sha256` in the spec's frontmatter would be written by the spec
  skill, and a person hand-editing the spec will not update it — so it could be stale *and wrong*.
  A findings round is written by the interrogation, which has just read both documents.
- **It describes the thing that goes stale** — the judgement, not the document.
- **It makes D-02 compute staleness for nothing.** The rule already requires a clean round; the hash
  makes "clean" mean "clean against the upstream it saw", and the gate already compares hashes.
- **It keeps the envelope uniform.** A frontmatter field on two of three artifacts is exactly the
  non-universality that dropped the upstream path in D-07a.

**Compatible with D-07a rather than reversing it.** That dropped the upstream *path*, because the
fixed chain order makes it derivable. A hash is not derivable — computing it needs the old content.
Different field, different reasoning.

## D-22 — `batch-build` completes cycles; `land` pushes. One word per meaning.

**From:** S7. **Governs:** §9, the skill inventory.

**Today.** Unimplemented.

**The conflict.** §9 said *"Partial batches land. Successes are landed rather than discarded when a
sibling fails"* and, thirty lines later, *"`land` is what touches the remote: it pushes, opens the
pull request... batch-build never pushes."* If landing is part of a batch run, the batch pushes. If
it is not, "partial batches land" describes something `batch-build` does not do and nothing says who
lands the successes.

The root cause is one word carrying two meanings: `land` as *the skill that pushes and opens a pull
request*, and "land" as *a cycle reaching completion*.

**Decision.** One word per meaning. **`batch-build` completes** cycles — it builds **and verifies**
each one. **`land` pushes.** So the partial-batch rule reads: *a sibling's failure does not discard
the cycles that completed; each can be landed individually.*

**The sub-question it settles: `batch-build` runs verify.** "Successes" implies success is
determined, and if a batch only built, "success" would mean "it compiled", which is not a verdict.
The verdict comes from `verify`, so a batch that cannot say which cycles succeeded cannot land the
ones that did.

**Four per-cycle outcomes, all already defined:** `passed`, `blocked`, `escalated`, and **not
attempted** — a cycle downstream of a failure in the claim graph.

**Rejected.** `batch-build` pushing as well: contradicts the stated division — *"batch build is a
local skill... the pullrequest will handle the remote push and pull requesting"* — and it makes an
unattended run reach the remote, which is the one boundary worth keeping deliberate. v03 had the
precedent in `run --all`, which would *"carry every cycle with an approved plan through to a pull
request unattended"*. Building without verifying: leaves "success" undefined inside a batch, so the
partial-batch rule has nothing to key on.

## D-23 — `land --all` sweeps every completed cycle, with one confirmation

**From:** S7's sub-point. **Governs:** `land`.

**Decision.** `land --all` lands every cycle whose verdict is `passed`. Five guards.

**1. It only lands `passed`.** Not `blocked`, not `escalated`, not unverified. This is the check v03
called `Assert-Landable` — and which, per v03's own open items, *"has never refused anything, because
no cycle has ever been unlandable."* It is the behaviour that makes unattended work safe to trust,
and it remains the framework's largest untested claim.

**2. It re-checks the gate per cycle, not just the verdict.** A cycle approved, built and verified
whose spec was then edited must not land. The approval's three hashes are recomputed; a mismatch
refuses that cycle and the others carry on.

**3. It reports what it would land and asks once.** Not per cycle — that defeats the point of a
sweep — but once, before the first push. Pushing a branch and opening a pull request is reversible
but **outward-facing**: other people see it, and eight pull requests arriving together is a
notification and review burden somebody did not choose. The confirmation is the same shape as the
approval — a person authorises the step that leaves the machine.

It also covers a surprise the sweep creates: *every* `passed` cycle is eligible, including one from
last week that was never landed. Printing the list is what keeps that from being a shock.

**4. A push failure on one cycle does not stop the others.** Network, authentication, or a protected
branch rule says nothing about the next cycle. It reports per cycle and carries on; stopping would
leave a person re-running the sweep over cycles that already pushed.

**5. It lands in `after` order, and a dependent cycle's pull request names what it depends on.**
Landing order is not merge order — a code owner merges, and the framework does not control that. But
a reviewer looking at a pull request built against a master that does not yet contain its
predecessor needs to be told, and the pull request body is where that belongs.

**Why a sweep at all.** A batch of eight cycles that completes and then needs eight manual `land`
invocations is a batch whose value evaporates at the last step.

**Rejected.** Per-cycle confirmation: defeats the sweep. No confirmation: an unattended run reaching
other people's notifications is the one place the local/remote boundary earns a deliberate pause.
Landing `blocked` cycles with a warning: the verdict is the only thing standing between unattended
building and unreviewed work reaching a pull request.

## D-24 — `batch-build --land` composes the two; plain `batch-build` stops at completion

**From:** asked directly — should `batch-build` also land? **Governs:** `batch-build`, `land`.

**Decision.** Yes, behind a flag, and as **composition rather than a new responsibility**:
`batch-build --land` invokes `land` per completed cycle. `batch-build` alone still never pushes, so
D-22's division holds and the pushing code has one home.

**The confirmation moves to the front.** `land --all` on its own asks when the list of passed cycles
is already known; `batch-build --land` asks before the run, when it is not. Both are one
confirmation; they differ in what a person can see when they give it.

**Why authorising a set you cannot see yet is acceptable here.** Landing is not a new decision. The
approval already said *this plan is agreed, build it* — per cycle, by hand, against three hashes.
A cycle that then verifies clean did what its spec required. And **a pull request requests review
rather than commits anything**: the code owner merging is the decision, and that is unchanged. So
`--land` is authorising the gate's own verdict to reach a reviewer, not authorising work.

**What the up-front confirmation is actually for**, then, is volume rather than permission: eight
pull requests arriving together is a burden on reviewers who did not choose it, and a person should
know that is what they started.

**Why a flag rather than the default.** The composed form touches the remote and the plain form does
not, so the quieter one is the default. Someone running `batch-build` to see where eight cycles
stand should not discover they have opened eight pull requests.

**What could go wrong, weighed rather than assumed.** A cycle that passes verify but is poor work
costs a reviewer's time and a closed pull request — recoverable. Eight pull requests at once is a
review burden, which the confirmation surfaces. A push to the wrong place is caught by `land`'s own
checks. None is severe, and the framework's whole claim is that an approved plan which verifies
clean is trustworthy work. If that claim is wrong, the problem is upstream of landing.

**Rejected.** Landing by default: the quieter behaviour should be what an unqualified command does.
A confirmation per cycle inside the composed run: defeats the point, since the run is unattended by
design. No confirmation at all: defensible on the authorisation argument above, and rejected because
volume is a real cost somebody should have chosen.

## D-25 — There is no `split` skill, and no `after` ordering. S8 dissolves.

**From:** S8, which asked where `after` lives. **Governs:** the skill inventory, §3.1.1, §9,
D-23.

**Today.** Unimplemented. `after` was introduced in §3.1.1 as a plan frontmatter key, read by
`batch-build`.

**The conflict S8 found.** §3.1.1 says splitting is cheapest early — *"splitting late throws work
away"* — so the normal case is a split at intent stage. But the ordering it produces was recorded
*"in the plan's frontmatter"*, and a split before a plan exists has nowhere to put it.

**Decision.** Remove both. Splitting work into deliverable chunks is the responsibility of whoever
is prompting: they write smaller intents and run each through the cycle. No tooling accommodates
splitting, at least not yet.

**Why.** v03 analysed this and reached the same conclusion, and its objections were the right ones:

> *Splitting is a scoping decision, so it belongs to a person. Which outcomes go in which cycle, and
> in what order, has consequences — what ships first, what can be abandoned, what a reviewer sees
> together. That is the same objection as auto-approval: the machine may propose, and a person
> decides at the gate.*

> *Splitting needs a concept the framework does not have... **Auto-splitting without dependency
> ordering produces cycles that cannot be built in isolation**, which is worse than one large cycle
> that at least builds.*

`after` was introduced specifically to supply that missing concept — **machinery built to enable a
feature whose value had never been established.** That is D-06's test pointed at a skill rather than
a section, and `split` fails it: no demonstrated consumer, and a dependency mechanism existing only
to serve it.

**What goes with it.** The `split` skill; `after`; S8 entirely, since the contradiction was about
where `after` lives; D-23's fifth guard, which landed in `after` order and annotated a dependent
pull request; and §9's first step, which read it.

**What survives, with its cause narrowed.** The `not attempted` outcome. Claim collisions arise
whenever two independently-authored cycles touch the same files, not only from splits — so
`batch-build` still partitions and serialises. It picks an order (cycle id, therefore chronological)
rather than being told one. Two cycles that genuinely depend on each other with nobody having said
so means the second's build fails, and a person learns and reruns. Acceptable under *not tooling for
this yet*.

**The risk this leaves open, recorded rather than solved.** v03's size argument still holds and now
has nothing covering it:

> *A cycle lands whole or not at all... so cycle size decides how much work one unmet criterion
> holds hostage. A large intent also defeats the gate that matters: the plan gate is the last point
> before anything becomes irreversible, and a plan touching sixty files across five features cannot
> be meaningfully read, so **approval degrades into signing**.*

With no constraint on intent size and no split skill, nothing pushes back on a sixty-file plan. That
is a legitimate choice — scoping is human judgement throughout this design — but it is an uncovered
risk rather than a solved problem. Partial mitigation exists: `tier` signposts reversibility, and the
candidate `CON_15` would make that claim checkable, so the dangerous kind of large is flagged even
where raw size is not.

**What could return without any of these costs.** v03's open item 14 proposed a **read-only
advisor** that proposes how to cut an intent and *writes nothing*. It has none of the dependency
problem, because it creates no cycles and therefore cannot create cycles that will not build. If
size becomes painful, that is the shape to revisit — not `split`.

## D-26 — The command line is the mechanism. MCP is a later surface over the same commands.

**From:** N5. **Governs:** §11.2, §6.3, §13.

**Today.** All command line. No MCP server exists.

**The conflict.** §11.2 says *"an MCP server exposes every cycle stage, the gate, and interrogation as
tools... **this is how the agent does the work**"*, while §6.3 says running `sdlc constraints` is the
first step of every skill and per-host delivery is *"an optimisation layered on top, **never the
mechanism**"*. Two things each claiming to be the primary path, so a skill author cannot tell whether
to invoke a command or call a tool — and with both, two surfaces must be kept in agreement, which is
`CON_09` at the API level.

**Decision.** The command line is the mechanism. If an MCP server ships it is a surface over the same
commands — one implementation, two ways in — and §11.2 moves from *how the agent does the work* to a
stated later direction.

**Two reasons, and the second only surfaced while writing the finding up.**

**A pipeline and a git hook cannot speak MCP.** It is an agent-host protocol, and §2.2 requires *"the
same binary runs in the pre-tool hook, in git hooks, and in the pipeline."* So the command line has
to exist regardless; MCP can only ever be additional.

**MCP cannot deliver `D-01`'s independence for interrogation.** An MCP tool runs in its own process,
but **the model calling it is the same session** — the one holding the authoring context. So an
interrogation tool has two possible shapes and both fail:

- *the tool judges* — but a tool is code, and code cannot decide whether a criterion answers an
  intent. It would have to make its own model call, which means the server needs credentials and a
  bill. That is a service, not a wrapper.
- *the tool returns the materials and the calling model judges* — which is the session that just
  wrote the artifact grading its own homework, exactly the investment bias `D-01` removes.

**A tool call hands data to the reader you already have. A new process creates a new reader.**

**This is specific to interrogation.** Everything else the gate does is deterministic — `constraints`
assembles a set, `gate` compares hashes, `status` computes state — and a tool call is a fine way to
hand a computed answer to the calling model. So MCP would serve most of the surface and fail the one
skill whose entire value is that a *different* reader does the reading. Which makes §11.2's claim
wrong rather than merely unranked: the honest version is *how the agent does the deterministic
parts*, and at that point it is a convenience over commands that must exist anyway.

**Rejected.** MCP as the mechanism with the command line for pipelines only: two surfaces from day
one, the agent-facing one unexercisable by CI in the same way, and interrogation still cannot go
through it. Both with no precedence: the current state, and the contradiction. Dropping MCP from the
concept: loses a real future benefit — typed tool definitions make a model less likely to mis-invoke
a command, and all five hosts support it — for a sentence's worth of cost.

## D-27 — A blocking finding cannot be waived, by any mechanism. Hard rule.

**From:** T14. **Governs:** the approval ledger, the gate, the findings model, `CON_10`.

**Today, in code.** A blocker *can* be accepted at the gate: `GateEntry.AcceptedFindings`,
`cycle-approval` pre-listing every open blocker as accepted, `GateRefusal.UnacceptedBlocker`, and
`Disposition.Waived`.

**Decision.** A blocking finding is fixed or the cycle does not advance. There is no waiver, no
acceptance, and no override — at the gate or anywhere else. Non-blocking findings are recorded and
accumulate; blocking findings stop everything.

**Why — and `D-02` had already made the waiver vestigial.** Acceptance existed because I believed
some blockers had no legal exit. `D-02` established that an unresolvable item moves to the artifact's
open region and **that move is the compliant exit**. Once every blocker has one, no legitimate case
for waiving one remains.

**And the exit does not launder bad work**, which is the thing worth checking. If a plan's proof
cannot reach a criterion, the plan records that under `Open`, so `CON_06` is not breached — it named
no inadequate check. But the criterion is then unproven, and **verify still blocks**, because `D-15`
requires every criterion to map to a named test that passed. The exit moves *where* a problem is
caught, never *whether*.

Some blockers have no open-region exit and must simply be fixed — `CON_01`, an intent naming a
solution with no observable problem. You cannot move "the problem" to `Open`; you have to write one.
Correct, and no waiver there would be legitimate either.

**`CON_10` rebinds rather than retiring.** Its forbids clause — *"a reason that restates the finding,
cites schedule pressure alone, or says only that the finding is understood"* — applies exactly to a
**gate refusal's reason**, which the ledger already requires on `changes_requested`, `denied` and
`reopened`. Same rule, new subject, no gap in the id sequence.

**A note on vocabulary.** Non-blocking findings are *non-blocking*, not *trivial*. The trial's two
were substantive — that `dotnet test` did not actually run the suite, and that criterion 5's proof
rested entirely on a substituted source. `sdlc suggestions` exists because accumulated non-trivial
ones matter.

**Code this removes.** The first reconciliation decision that deletes working code:
`GateRefusal.UnacceptedBlocker`; `ApprovalGateHandler`'s `Unaccepted` and `Unanswered`;
`GateEntry.AcceptedFindings`; `Disposition.Waived`; and the approval printer's pre-listing, which
simplifies to *refuses while any blocker is open*. Roughly five tests change.

## D-28 — `init` has three modes, and the protected set defines what update may touch

**From:** N2. **Governs:** `init`, §7's mutability table, §15's layout.

**Decision.**

| Mode | When | What it does |
|---|---|---|
| **scaffold** | a new solution | writes everything, `origin: scaffolded` |
| **onboard** | existing code, no `.sdlc/` | writes everything, `origin: adopted` |
| **update** | `.sdlc/` already exists | refreshes only what the framework owns |

**The rule that makes update safe needs no list.** `constraints.md`, `solution.md`, `approval.md` and
`abandoned.md` are hook-protected, so update cannot touch them: **init updates what it owns, and what
it owns is everything not protected.** So update refreshes hook configs, pipelines, and
`.sdlc/README.md`.

`AGENTS.md` follows the draft pattern — update **prints** the changes rather than applying them,
because rewriting a repository's instruction file is the one destructive thing adoption could do.

**This answers N2's original question.** §7 said "three files in `.sdlc/`" against a layout showing
four, with `README.md`'s mutability unstated. It is **framework-owned**, regenerated by update,
unprotected — which names the owner rather than only the permission.

**And it closes part of the maintenance gap** raised much earlier: when the framework version moves,
something now updates a solution's scaffolding. The judgement half — proposing changes to
constraints and the tech stack — remains open.

**Rejected.** `init` refusing when `.sdlc/` exists, which is what v03 did and what `D-20`'s table
recorded: correct while nothing needed refreshing, and it leaves a solution's scaffolding to rot
across framework versions with nothing to fix it.

## D-29 — The interrogate-refine loop is a mode of each stage skill, not a skill of its own

**From:** N9. **Governs:** §5.0, §14.

**Findings from reading the document**, rather than from recollection:

- §5.0 says *"**One command** writes an artifact, interrogates it, works its own blocking findings,
  and interrogates again"* — a command it never names.
- §14's table says `intent` *"interviews the user; classifies the change; writes `intent.md`"*. No
  loop. Same for `spec` and `plan`. `interrogate` and `refine` are separate rows.

So the document describes a command absent from the inventory while listing stage skills that do not
loop. Three accounts, no owner.

**Decision.** Each stage skill runs the loop: `intent`, `spec` and `plan` write, interrogate, refine
and interrogate again until the round is clean or nothing further can be settled, then report. No
eighteenth skill, and no second way to invoke the same loop.

**Why.** It is where the behaviour already belongs — `D-02` requires a clean round to advance, so the
stage that must reach one is the stage that should run the loop. And `D-16` is satisfied: the loop
stops on convergence and **hands back rather than claiming to be done**, so the decision to continue
interrogating stays the person's, which `D-02` explicitly preserves.

**A second fix in the same paragraph.** §5.0 still says *"a skill can judge itself against the same
rules **in process**"* — which `D-01` reversed. That sentence is the S1 contradiction surviving in the
text, and it belongs in the same edit.

## Corrections to apply in the rewrite

Text that disagrees with itself or with the code. No decision in any of them.

| | Change |
|---|---|
| **S3** | §4.1: *"reading the spec restated inside it"* → *"reading it against the spec it cites"*. The plan names a criterion in order to say how it will be proven, which is citation; `CON_09` forbids reproducing something *as its own authority*, which this is not. The design was right and the sentence overclaimed |
| **S5** | Delete §11.3's PowerShell paragraph — the binary won and there is no PowerShell in the implementation. Fix §3.2's claim that the diff-to-plan comparison *"already exists as `Compare-DiffToPlan.ps1`"*, true of v03 and not of this. Add the sentence the concept never makes: **host-agnostic is not stack-agnostic** — the rules travel anywhere, the tool needs .NET |
| **T12** | Delete `adopted_at` from §15's layout and from §16's sequencing note. Dropped with evidence in §7.3 and surviving in two places |
| **T13** | Delete §10.2's opening claim that the kernel ships *"analyzers that keep a vertical slice in shape"* — verified false: no analyzer project, no diagnostic ids, no source generator. Fix §18's open question 1, which puts new analyzers back in `MofonCode.Core` against the settled decision that they live in the profile declaring the convention |
| **N1** | Declare the bare form canonical — `CON_01` is revision 1 — and fix the two worked examples citing `CON_04.1`. The code already behaves this way |
| **N4** | **"platform"** for the CI side — GitHub, Azure DevOps. **"host"** stays the agent program, which is what `HookHost` already means in code |
| **N6** | §13's example panel uses `·` (U+00B7) and §4.2's ledger example uses `…` (U+2026). Neither is ASCII, and §13's own rule four requires ASCII-safe glyphs so the panel renders in a pipeline log and a non-UTF-8 console. The renderer obeys the rule and a test asserts it; the examples break it. Replace with `-` and `...` |
