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

### The counter

One counter, per constraint, per artifact. Two sources, weighted the same because both are
corroborated evidence that one rule's ground is stuck:

| Event | Points against that constraint |
|---|---|
| a relapse — `open → answered → open` | 1 |
| a reader declares a conflict naming it | 1 |

| Points | What happens |
|---|---|
| 1 | **nothing.** An incomplete fix is ordinary and common |
| 2 | **warn.** The panel says so. The loop continues |
| 3 | **halt.** A person is called |

**No round cap, anywhere.** A converging interrogation scores zero points however long it runs, so the
eleven-round case is untouched. Three points is roughly six rounds of genuine ping-pong — deliberate
room for a loop that oscillates *on its way to* converging, which is the normal shape of a refiner
finding a boundary: narrow, overshoot, broaden, overshoot, then find the `Out of scope` phrasing that
satisfies both. Halting at two would kill that one round before it succeeds.

### The warning has to change what `refine` is told

**This is the part that makes the extra room worth paying for.** More laps at the same approach is just
more spend. The refiner is currently handed findings and no history, so it cannot know it is refighting
old ground. At the warning stage it gets told, in the skill's own voice:

> This is the second time `CON_05` has come back after being answered, and the fix that answered it
> raised `CON_16`. Before editing the same sentence again, consider whether the two rules share an
> exit: a remainder named in `Out of scope`, or an item that genuinely belongs under `## Open`.

So the loop gets another attempt **and better information for it**. Without this, phase 2 is a bigger
bill for the same outcome.

### Work

| File | Change |
|---|---|
| `src/Sdlc.Core/Findings/Oscillation.cs` | new, shaped like `RemediationBudget`: a state record and a static reader that counts from the rounds. **No counter file** — the same reason that one gives, that a count kept beside the thing it counts can disagree with it, and the cheap one to forge is the one that gets trusted |
| same | `Warn = 2`, `Halt = 3` as constants. Constants and not configuration, for `RemediationBudget`'s stated reason: the number a team would raise it to is the number at which the bound stops meaning anything |
| `src/Sdlc.Core/Cycles/StatusRenderer.cs` | the warn and halt states on the panel, naming the constraint and the rounds involved |
| `skills/intent`, `skills/spec`, `skills/plan` | the loop stops on a halt instead of on the agent's own judgement that nothing further can be settled |
| `skills/refine/SKILL.md` | the `Capped: no` row becomes the counter; the warning paragraph above |
| `CONCEPT.md` | §6's three endings become true. A decision entry |

### A halt stops the loop and reports. It does not refuse the gate

No new `GateRefusal` value, and `ApprovalGate` is untouched. The reasoning is mechanical rather than a
matter of taste: a halt almost always stands beside an open blocking finding -- that is what a relapse
requires -- and `GateRefusal.OpenBlocker` already refuses the build. A gate refusal would be redundant
in the common case and wrong in the only case where it would bite, which is the counter reaching three
and then `refine` fixing it and the next reader going silent. That is a loop which converged, and
refusing it at the gate would punish the success.

**A halt is evidence that automation got stuck, not evidence the artifact is wrong.** The thing that
decides whether the artifact may advance is the blockers, and they already do.

### What the halt message must not claim

The counter cannot tell a genuine conflict from a refiner making the same mistake repeatedly. Both need
a person, so the distinction does not change what happens — but the message must say what it knows and
not more: *this ground has been refought twice, these are the rules involved, these are the rounds.* Not
*these rules conflict*.

### Resolution

A halt is cleared by a person, and the exits already exist:

| The actual problem | The exit |
|---|---|
| scope or approach needs to change | `steer` — it edits the intent, spec and plan together and records the correction as a fixed decision with attribution |
| the remainder cannot be covered | name it in `Out of scope` (`CON_16`) or under `## Open` (`CON_14`) |
| the rules genuinely cannot both hold | a constraint change — a `LOC_` addition or a library revision. Outside a cycle |
| the cycle should not continue | `sdlc draft abandon <id>` |

**`steer` clears the halt, and the loop re-evaluates.** That is the recorded decision. Steering already
re-interrogates everything downstream of what it touched, and a round records the hash of the artifact
above it -- so editing the intent makes the spec's latest round stale and the loop runs again on its own.
The counter resets with it: the ground that was being refought has moved.

**`steer` does not reach every conflict**, and the halt message should not imply it does. It changes how
rather than what, so it reaches a scope or approach change and not a case where no document satisfies
both rules.

**What stops an agent clearing its own halt.** Nothing mechanical, and there is nowhere to put it: the
hook denies writes to the four protected files, and `steer` is a skill rather than a file, so there is no
path to deny. What the reset actually rests on is downstream — steering records the correction as a fixed
decision **with attribution**, in the intent, and a person reads all three planning documents at the
approval gate before any code exists. A fabricated steer is a fabricated attribution sitting on the page
where somebody is about to approve it. That is the same layer of protection every advisory rule in the
framework has, and it is worth stating out loud rather than leaving as an assumption.

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
| 1 | Warn at 2 points, halt at 3 | phase 2, the counter |
| 2 | `steer` clears a halt and the loop re-evaluates. No mechanical guard against an agent steering itself; the approval gate downstream is what it rests on | phase 2, resolution |
| 3 | **No ceiling** on the remediation loop. `Limit = 3` goes and the progress rule is the whole of it | phase 3 |
| 4 | A halt stops the loop and reports. It does not refuse the gate, and `ApprovalGate` is untouched | phase 2 |
| 5 | `between` holds two **or more** | done, D-64 |

Decision 3 is the one that trades a guarantee for a better signal, and phase 3 says so in those words
rather than leaving it to be rediscovered.

## Order, and why it is not negotiable

Phase 1 before phase 2: a conflict declaration is one of the counter's two inputs, so the counter cannot
read it until it exists. Phase 3 is independent of both and could go first or last.

The phase-2 counter is *almost* independent of phase 1 — relapses alone are computable from what rounds
already record. If phase 1 slips, a relapse-only counter still works and is worth having.

## What this plan does not do

- **It does not resolve finding 5b** from the deleted review: whether a plan may depart from an
  architecture rule the way it departs from the declared stack. That question is now recorded nowhere
  but git (`d572eb2`) and still wants a home.
- **It does not add the heading drift check.** Verifying a stored citation heading against the live
  library has no home on the write path, because no command writes a round — the skill does. `sdlc
  doctor` is where it belongs.
- **It adds no round cap to anything.** Both guards are blind to how long a healthy loop runs.
