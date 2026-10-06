# Plan — conflicts, oscillation, and the loop guards

A working document, not a design authority. When each phase lands, its reasoning goes to `CONCEPT.md`
as a decision entry and this file loses that section. When the file is empty, delete it.

## What is wrong today

Three separate things, found while looking at why a bare `CON_03` reaches a reader with no words
beside it.

**§6 promises three endings and the code has one.** `CONCEPT.md:531` says a loop "stops on convergence
or on conflict, not on a count. … Three endings have to be distinguishable: converged, open,
conflicting." `grep -rn "Conflict" src --include=*.cs`, outside the merge-collision skill, returns
nothing. A conflict today lands as prose inside one finding's `statement`, where it is
indistinguishable from any other objection to the status panel, the gate and the suggestions listing.
Nothing counts it, nothing stops on it, and nothing tells a person it happened.

**The interrogation loop is uncapped on a premise that is no longer true.**
`src/Sdlc.Core/Findings/RemediationBudget.cs` explains why the cap is on code and not on artifacts:

> an unbounded interrogation loop is a person deciding to keep reading, which has been worth eleven
> rounds before now.

No person is deciding. `skills/spec/SKILL.md:150` reads *"Interrogate, refine, interrogate again until
a round is clean or nothing further can be settled"*, and `skills/refine/SKILL.md:20` says
`**Capped** | no. The loop is uncapped on purpose`. The agent runs the loop, and *"nothing further can
be settled"* is a judgement the same agent makes about its own work — which is the inversion the
interrogate/refine split exists to prevent.

**The remediation loop is capped on a blunt count.** Three passes, read from the verify rounds. A hard
three fails an honest cycle that needed four, and lets a cycle that is making no progress at all spend
all three.

## Definitions, settled

These are the whole plan. The mechanism below is downstream of them.

| | What it is | How it is known |
|---|---|---|
| **finding** | the artifact breaks a rule | a reader raises it. The ordinary, healthy output of the loop |
| **conflict** | two rules cannot both be satisfied by *any* version of this artifact | a reader declares it. **One declaration is not settled** — see below |
| **relapse** | one constraint going `open → answered → open` | computed from the round files |
| **oscillation** | points accumulating against a **single constraint**, from relapses and conflict declarations together | the counter in phase 2 |

**A finding is an objection to the artifact. A conflict is an objection to the rule set as it lands on
this situation.** With a finding, a correct artifact exists and this is not it. With a conflict, no
correct artifact exists: every edit satisfying one rule breaks the other.

A real pair, from the live library. The intent says *"somebody can see a die on the screen showing a
number between 1 and 6."* Write criteria covering the visual half and nothing in the intent supports
them, so `CON_05` fires. Narrow the criterion to the number and that is half the outcome, so `CON_16`
fires. The spec is not wrong; there is no right spec available, and the resolution is outside it.

**True conflicts are rare, and that is by design.** `CON_16` offers `Out of scope` as an exit precisely
so this pair usually does not deadlock, and `CON_14` gives `## Open` to an unsettleable question. A
genuine conflict is one where no exit applies. That rarity is what justifies calling a person rather
than automating a resolution.

**Why one conflict declaration is not enough.** A declared conflict is an assertion, and nothing in
this framework advances on assertion. It is also an escape hatch: an agent stuck on a blocker it cannot
answer has an obvious out in calling it a conflict — the same hazard the abandonment record is written
to prevent. But a reader is handed only the constraints, the artifact and the artifact above it, and
**never the prior rounds**. So two readers declaring the same conflict are two uninvested processes
reaching one conclusion with no knowledge of each other. That is corroboration, and it is the same
property D-49 already rests `answered` on.

**Why oscillation is a sequence and not a count.** `CON_05` cited in rounds 1, 3 and 5 for three
different mistakes is a sloppy author, converging fine. `CON_05` cited, *answered*, and cited again is
the loop refighting ground it already won. Same tally, different situation. The signal is the
transition, not the total.

**Why relapse is trustworthy.** D-49: *"the reader's silence is the evidence, and the reader is never
asked for a disposition."* So `answered` is not self-reported — it is a fresh reader declining to raise
something. A relapse is therefore two independent readers, one silent and one not, with nobody able to
fabricate either half.

**Why it keys on the constraint and not the finding id.** Finding-id reuse across rounds is the skill's
judgement, so it is soft. The cited constraint is not, and since `f1e59ba` it is a structured `cites.id`
rather than a string somebody formatted. A brand-new finding id citing `CON_05` again still registers.

**Why points are counted per constraint and not per artifact.** `CON_05` coming back three times is the
same ground refought. `CON_05` once and `CON_12` once is two unrelated incomplete fixes, and
aggregating them would halt a loop that is converging. Counting per constraint also makes
constraint-*pair* bookkeeping unnecessary: nothing can relapse repeatedly on one rule without something
else pushing back each time, so the pair is implied.

---

## Phase 1 — a conflict becomes a record

**Done.** Landed as D-64; the reasoning is in `CONCEPT.md` and no longer here.

---

## Phase 2 — the counter, and the room to converge

**Done.** Landed as D-65; the reasoning is in `CONCEPT.md` and no longer here.

---

## Phase 3 — the remediation loop

Still genuinely unsettled, and the only part of this plan I would not write without an answer.

### The asymmetry that matters

| Loop | Can it land anything? | Worst case |
|---|---|---|
| interrogate → refine | **no.** It edits planning documents, and a person approves afterwards | wasted tokens before a human gate |
| verify → remediate | **yes.** It runs after approval, inside `batch-build`, possibly unattended, and feeds `land` | unbounded spend with no human between it and the remote |

`RemediationBudget` is explicit that the cap is what makes the unattended half safe: *"A batch is only
safe to run unwatched because a cycle that cannot be made to pass stops instead of landing."* So
removing it outright costs that property, and that is a decision to record rather than a side effect.

### Progress rather than passes, and no ceiling

| | Rule |
|---|---|
| continue while | each pass **strictly reduces** the open findings |
| stop after | two consecutive passes with no reduction |
| stop immediately on | a **regression** — a criterion that passed last pass failing now. The code-side oscillation signature |
| hard ceiling | **none.** `Limit = 3` goes and nothing replaces it |

This lets an honest six-pass remediation through and stops a loop going nowhere *faster* than three blind
passes did. A progress rule is a predicate rather than a number somebody will want to raise, so it does
not reopen the configurability argument that kept `Limit` a constant.

**What no ceiling costs, recorded because it is a real cost and not a detail.** `RemediationBudget`'s own
remarks say the cap is what makes the unattended half safe: *"A batch is only safe to run unwatched
because a cycle that cannot be made to pass stops instead of landing."* With no ceiling, termination rests
entirely on the progress rule. A loop that manufactures one trivial improvement per pass — closing a
finding while opening another, forever — satisfies "strictly reduces" on alternate passes and never
terminates. The progress rule is a better signal than a blind count and it is not a *guarantee*, which the
count was.

This was decided knowing that. The judgement is that a blind three was failing honest cycles often enough
to cost more than the unbounded tail risk, and that the regression rule plus the no-reduction rule catch
the realistic bad loops. If an unattended batch is ever seen running away, the remedy is a ceiling put
back deliberately rather than this rule tightened.

### Work

`RemediationState` gains the per-pass open sets; `Escalated` becomes the predicate above;
`Land.LandRefusal.Escalated`'s reason text changes; a decision entry, because this supersedes the stated
reasoning behind `Limit = 3`.

---

## Decisions taken

Peter, 2026-10-05. Each is written into the phase it governs; they are collected here so the reasoning
and the ruling are not in two places that can drift.

| # | Decision | Where it lives |
|---|---|---|
| 1 | Warn at 2 points, halt at 3 | done, D-65 |
| 2 | `steer` clears a halt and the loop re-evaluates. No mechanical guard against an agent steering itself; the approval gate downstream is what it rests on | done, D-65 |
| 3 | **No ceiling** on the remediation loop. `Limit = 3` goes and the progress rule is the whole of it | phase 3 |
| 4 | A halt stops the loop and reports. It does not refuse the gate, and `ApprovalGate` is untouched | done, D-65 |
| 5 | `between` holds two **or more** | done, D-64 |

Decision 3 is the one that trades a guarantee for a better signal, and phase 3 says so in those words
rather than leaving it to be rediscovered.

## Order

Phases 1 and 2 are done, in that order, because a conflict declaration is one of the counter's two
inputs. Phase 3 is independent of both and is what remains.

## What this plan does not do

- **It does not resolve finding 5b** from the deleted review: whether a plan may depart from an
  architecture rule the way it departs from the declared stack. That question is now recorded nowhere
  but git (`d572eb2`) and still wants a home.
- **It does not add the heading drift check.** Verifying a stored citation heading against the live
  library has no home on the write path, because no command writes a round — the skill does. `sdlc
  doctor` is where it belongs.
- **It adds no round cap to anything.** Both guards are blind to how long a healthy loop runs.
