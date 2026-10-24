# Handling blockers

What to do when the framework finds something wrong with a cycle: a **blocking finding** on an intent,
spec or plan, or a **`blocked` verification** on a build.

**A blocker is not a refusal, and the difference is worth keeping straight.** A blocker is a judgement:
a fresh reader looked at a document or at the evidence and found it breaks a rule. A refusal is a
command or hook declining to go on — and an open blocker is one of the things that causes refusals, at
the next stage, at the approval draft and at the gate. `handling-refusals.md` covers every refusal; this
guide covers the blockers behind some of them, and how they are answered.

**Nothing here can be waived.** There is no field for accepting a blocker, no flag, and no recorded
override. It is answered, or the cycle does not advance — and that is the design working, because a
blocker that could be waived would be waived exactly when it mattered.

## Where blockers come from

| Loop | Judges | Blocker | Answered by |
|---|---|---|---|
| interrogate, then refine | one planning document — intent, spec or plan — against the rules in force | a blocking finding | `/sdlc:refine`, which edits that document |
| verify, then remediate | the build, from its recorded evidence, against the spec | a `blocked` verification | `/sdlc:remediate`, which edits source and tests |

**The two editors have opposite guards, and the asymmetry is the whole design.** Refine edits a
planning document and never code. Remediate edits code and never a planning document — if a failing test
could make an agent adjust the spec, the criteria would become whatever the code happens to do.

**Neither loop counts rounds.** Both stop on going backwards rather than on going slowly, and both hand
back to you when they do.

---

## A blocking finding on a document

### What it is

`/sdlc:interrogate <id> <artifact>` reads one document in a fresh process — a reader that never saw it
being written, so it has nothing to defend — and writes a findings round to
`findings/<artifact>.rN.json`. Each finding has an id kept across rounds, the one rule it cites, what is
wrong, where, and the remedy.

| Severity | Means |
|---|---|
| `blocking` | the document breaks the rule it cites. It stops the document advancing |
| `suggestion` | something worth knowing that no rule forbids. It stops nothing — see the end of this guide |

**The stage skills run this loop for you.** `/sdlc:intent`, `/sdlc:spec` and `/sdlc:plan` each write
their document, interrogate it, refine it, and repeat until a round raises nothing blocking, then hand
back. You meet a blocker directly when a loop hands back early, when you edit a document yourself, or
when a refusal elsewhere names one.

### What it stops

While a blocking finding is open:

- **the next stage refuses to start** — `/sdlc:spec` will not derive from an intent without a clean round
- **`sdlc draft approval` refuses to print** an approval
- **the gate refuses the build**, as "a blocking finding is open"

### Answering it

```
/sdlc:refine <id> <artifact>
```

Refine applies the latest round's findings to that one document and never judges its own work. **Every
finding leaves by exactly one of three exits**, and there is no fourth:

| Exit | What happens | When |
|---|---|---|
| **healed** | the document changes so the finding no longer holds | the finding is right and the fix is clear |
| **assumed** | a working answer is recorded in the document's decisions region, marked unconfirmed | an answer is needed to go on, and the refiner can make a reasonable one |
| **asked** | the question is recorded in `## Open`, and the skill stops and asks you | only you can answer it |

**When it asks, answer in the conversation.** Your answer becomes part of the document, and the next
round reads it.

**What refine may not do is answer a finding by making the document vaguer.** "Fourteen seconds"
becoming "slow" removes the problem from the page while leaving the belief in it. It may disagree with a
finding — not every finding is right — and it reports every id it was given, saying whether it changed
the document or disagreed and left it alone.

**Refine answers the mistake, not only the line.** It reads the rest of the document for the same
defect under the same rule and fixes every instance in one pass, and a reader names every place a
repeated defect occurs in one finding. Without both, a mistake made in five places costs five rounds.

**Only the next interrogation clears a blocker.** Refine's opinion of its own corrections counts for
nothing; a fresh round records an earlier finding as `answered`, or `withdrawn` where the objection was
dropped. One it raises again stays open.

```
/sdlc:interrogate <id> <artifact>
```

```pwsh
sdlc status <id> --brief
```

### The loop

Rounds accumulate and are never rewritten. **How many rounds is deliberately not a question the tool
will answer.** On one real intent, round seven found the headline change was unreachable and round
eleven found that no success criterion failed without it; a tool that suggested stopping at round four
would have been confidently wrong.

**What the tool watches for is going backwards.** The status panel's `Loop` line appears when a rule has
been answered and raised again, and names it. *Answered* means the rule went quiet — a round raised
nothing open against it — or that one finding was answered and the same finding came back. A round that
answers one finding and raises a different one under the same rule is not going backwards: that is the
reader moving on to the next place the same mistake was made, and it scores nothing. When one rule has been answered and raised again three
times, the loop stops and hands back to you. That usually means one of three things:

- **the finding is right and the fix keeps undoing another one.** Two rules no version of the document
  can satisfy are reported as a conflict rather than resolved by picking a side — decide which gives way,
  and say so
- **the refiner keeps guessing at something only you know.** Answer it directly
- **the document is answering the wrong question.** That is a correction, below

### A stale round

A round records the hash of the document above it as the reader saw it. Edit an intent after its spec
had a clean round and the spec's round is now **stale**: it judged a spec derived from an intent that no
longer exists, with every approval hash still matching. The gate refuses it.

**Re-interrogate downstream** — the spec, and the plan after it if the spec changed.

### When the finding is right and the intent is wrong

Do not edit the documents by hand, and do not refine around a problem you have changed your mind about.

```
/sdlc:steer <id> "what to change"
```

Steer carries the correction down the chain — editing the intent, spec and plan rather than regenerating
them — records it with your name on it, and re-interrogates everything downstream of what it touched,
which is what keeps rounds from going stale. **Steering changes *how*, not *what*.** A correction that
changes the problem is a different cycle, and the skill says so and offers one. The exception is a
suggestion a reader raised: name its id — `/sdlc:steer <id> accessibility.F_01 security.F_02` — to take
it into the intent with your name on it, or to decline it. Name every one you want in the same run.

### After approval

Refine refuses once a cycle is approved: the documents are hash-locked, so editing one would break the
gate rather than satisfy it. A blocker found after approval means withdrawing the approval first:

```pwsh
sdlc draft approval <id> --reopen
```

It leaves the reason line empty for you, and the rename records it. Then the loop runs as before, and the
documents are approved again. **A reopen after the build leaves its evidence and its verdict behind**:
once the cycle is approved again, `sdlc verify-chain` refuses a verify round older than that approval, so
delete `verify.md`, build, record the evidence and verify the documents as they are now approved. Reopen as
many times as it takes — the ledger keeps every entry and the last one stands.

---

## A blocked verification

### What it is

`/sdlc:verify <id>` reads the recorded evidence — `tests.json`, `diff.patch`, `base.sha` — and never
runs the tests or reads the source. Per criterion it asks which test the plan named, whether the summary
says it ran and passed, and **whether that test actually establishes the criterion.** A test that passed
but does not reach its criterion is *not met*.

It writes `pass` or `blocked`, and a findings round of its own, `findings/verify.rN.json`, whose blocking
findings are what remediation works from. **`pr-ready` refuses anything but `pass`.**

### Answering it

```
/sdlc:remediate <id>
```

Remediate reads the latest verify round and edits source and tests to fix the **cause**, never a
planning document. It does not verify its own work — a verification by the session that wrote the fix is
the bias the separation exists to remove — so after each pass it builds, tests, records the evidence
again and commits it, and a fresh verification reads the new evidence.

**It runs while each pass reduces what is open, and stops when it is not getting anywhere** (D-66):

| It stops when | Because |
|---|---|
| a pass undoes what an earlier one answered | a loop undoing its own work is not going to finish |
| two passes in a row reduce nothing | nothing is moving |

There is no pass ceiling. Nothing stops pass nine if pass nine is still making progress.

### When the criterion is wrong

**Remediate will not adjust the spec to match the code.** If the honest conclusion is that a criterion
or a planned proof is wrong, it says so and stops. The recovery is yours: reopen the approval with a
recorded reason, correct the document — by refine, or steer if the intent moved — and approve it again.

```pwsh
sdlc draft approval <id> --reopen
```

### Escalated

**A remediation loop that stops making progress sets the verify outcome to `escalated`.** It is computed,
never declared, and `pr` refuses it — a batch run with `--pr` opens a draft pull request for it instead,
which the check will not let merge (D-85). An escalated cycle is waiting for a person: read what the passes
actually did, then decide whether the plan needs reopening, the work needs a different approach, or the
cycle should stop.

```pwsh
sdlc draft abandon <id>
```

Abandoning is terminal and recorded with a reason, and the rename is yours, like every decision.

---

## Suggestions are not blockers

```pwsh
sdlc suggestions
```

A suggestion is a finding that cited no rule the document breaks, and a `Noticed:` line is something a
build saw and was not allowed to fix. **Non-blocking is not trivial** — two real ones from a single
trial cycle were "the suite did not actually run" and "this criterion's proof rests entirely on a
substituted source". Neither stops anything, so nothing makes you read them; this command is where they
collect.

What keeps going wrong across cycles — answered blockers included — is `sdlc propose constraints`, which
reports a rule cited in three or more distinct cycles as a candidate for one of this solution's own.

**A perspective's suggestions are how its rules get written** — `propose constraints` counts them by
perspective, and How a perspective earns rules in `../mechanisms/perspectives.md` says how to read that count.

## Where to go next

| | |
|---|---|
| every refusal, and the remedy for each | `handling-refusals.md` |
| the stages these loops sit inside | `running-a-cycle.md` |
| each skill named here, in depth | `../../skills/README.md` |
| one line per refusal | `../reference/refusals.md` |
