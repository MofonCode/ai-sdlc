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

**Done.** Landed as D-66; the reasoning is in `CONCEPT.md` and no longer here.

---

## Decisions taken

Peter, 2026-10-05. Each is written into the phase it governs; they are collected here so the reasoning
and the ruling are not in two places that can drift.

| # | Decision | Where it lives |
|---|---|---|
| 1 | Warn at 2 points, halt at 3 | done, D-65 |
| 2 | `steer` clears a halt and the loop re-evaluates. No mechanical guard against an agent steering itself; the approval gate downstream is what it rests on | done, D-65 |
| 3 | **No ceiling** on the remediation loop. `Limit = 3` goes and the progress rule is the whole of it | done, D-66 |
| 4 | A halt stops the loop and reports. It does not refuse the gate, and `ApprovalGate` is untouched | done, D-65 |
| 5 | `between` holds two **or more** | done, D-64 |

Decision 3 is the one that trades a guarantee for a better signal, and phase 3 says so in those words
rather than leaving it to be rediscovered.

## Order

All three are done: phase 1, then phase 2 because a conflict declaration is one of the counter's two
inputs, then phase 3, which reused the counter rather than inventing a second one.

**This file has nothing left in it but the record of what was decided.** Every phase's reasoning is in
`CONCEPT.md` as D-64, D-65 and D-66, which is where somebody will look for it. Delete the file and its
row in `AGENTS.md`'s map once the two loose ends below have homes.

## 5b — may a plan depart from an architecture rule?

**Decided, and `CON_18` is placed.** A plan may change a rule the cycle is judged by, and must say in
`Shape and departures` what satisfying that rule unchanged would have cost.

**Why this was easier than the review thought.** The review asked whether an architecture test should get
a departure door like `techstack.md` has, and could argue it both ways. D-67 moved the ground: the stack
file can now be argued with but not edited, while an architecture test can be edited but not argued with
— which is backwards, since the suite is the only mechanical enforcement of shape the framework has. So
the door was already open. `DeclaredFiles.Governs` says it outright: *"A cycle is allowed to change these,
and that is exactly why they are reported."* The question was never whether to open one, but whether
passing through it should require an argument. It now does, and the suite still runs — no mechanical
enforcement was traded away.

The counts, §7.5's table and the plan skill's `Shape and departures` guidance moved with it.

**What was rejected:** flagging when a declared shape is heavy relative to its intent. No bright line, so
it fires inconsistently — and the dice case's real miss was an intent bound, *"keep it very simple"*, that
no stage reads. That is a separate gap and still open.

---

## What this plan does not do

- **It does not read an intent's bounds.** The dice cycle's *"keep it very simple — nothing fancy"* was a
  fixed decision no stage acts on, which is what let a nine-project shape pass for one button. `CON_18`
  asks a plan to argue about rules it changes; nothing asks whether a shape is proportionate to what was
  wanted.
- **It does not add the heading drift check.** Verifying a stored citation heading against the live
  library has no home on the write path, because no command writes a round — the skill does. `sdlc
  doctor` is where it belongs.
- **It adds no round cap to anything.** Both guards are blind to how long a healthy loop runs.
