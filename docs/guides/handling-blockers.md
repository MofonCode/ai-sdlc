# Handling blockers

What to do when the framework finds something wrong with a cycle: a **blocking finding** on an intent,
spec or plan, or a **`blocked` verification** on a build.

**A blocker is not a refusal, and the difference is worth keeping straight.** A blocker is a judgement:
a fresh reader looked at a document or at the evidence and found it breaks a rule. A refusal is a
command or hook declining to go on — and an open blocker is one of the things that causes refusals, at
the next stage, at the approval draft and at the gate. `handling-refusals.md` covers every refusal; this
guide covers the blockers behind some of them, and how they are answered.

**Reading one.** The status panel, the approval draft and the gate refusal show each open blocker as the
rule it cites, a **gist** — one plain sentence from the reader that raised it, saying what in the
document set it off — a **`Where:`** line, the place in the document it was found (the first place, and
how many more), and a **`Fix:`** line, the reader's remedy. The status panel lists blockers under the rule
they cite, with the rule's words once, and ends with the command that explains them:
`sdlc findings <id> --stage <artifact> --blocking` prints each one's full statement, which can run to a long
paragraph, and every place it was found; `--finding <fid>` prints one. The brief panel a stage ends on
names each blocker by its gist. A blocker raised before gists existed shows its statement in the gist's
place.

**Reading the loop.** From the second round the panel adds a **Turnover** line: how many of the open
blockers this round raised for the first time, how many it carried over, how many came back after being
answered, and how many raised earlier are settled. *3 new, 0 carried over, 0 came back; 12 settled*
is a reader finding something nobody had raised each round with every earlier objection answered —
a document too wide to prove in one pass, which another refine will not narrow. Blockers that come back
are the loop the **Loop** line warns about.

**Most of this runs without you.** Taking an intent to plan runs each document's interrogate-and-refine loop
itself, and fixing a blocked build verifies again after every pass. This guide is what those loops are doing,
and what to do when one stops and asks you — which is the only time you need to.

**Nothing here can be waived.** There is no field for accepting a blocker, no flag, and no recorded
override. It is answered, or the cycle does not advance — and that is the design working, because a
blocker that could be waived would be waived exactly when it mattered.

## Where blockers come from

| Loop | Judges | Blocker | Answered by |
|---|---|---|---|
| interrogate, then refine — run by each stage, and by `to-plan` | one planning document — intent, spec or plan — against the rules in force | a blocking finding | `refine`, which edits that document |
| verify, then remediate — verify run by a green build, then by remediate after each pass | the build, from its recorded evidence, against the spec | a `blocked` verification | `remediate`, which edits source and tests — say *fix it* |

**The two editors have opposite guards, and the asymmetry is the whole design.** Refine edits a
planning document and never code. Remediate edits code and never a planning document — if a failing test
could make an agent adjust the spec, the criteria would become whatever the code happens to do.

**Neither loop counts rounds.** Both stop on going backwards rather than on going slowly, and both hand
back to you when they do.

---

## A blocking finding on a document

### What it is

`/sdlc:interrogate <id> <artifact>` reads one document in a fresh process — a reader that never saw it
being written, so it has nothing to defend — and adds a findings round to the artifact's ledger,
`findings/<artifact>.json`. Each finding has an id kept across rounds, the one rule it cites, what is
wrong, where, and the remedy. `sdlc findings <id>` prints each one where it stands.

| Severity | Means |
|---|---|
| `blocking` | the document breaks the rule it cites. It stops the document advancing |
| `suggestion` | something worth knowing that no rule forbids. It stops nothing — see the end of this guide |

**A blocker is something wrong; an improvement is a suggestion.** A criterion that holds over several cases
lists them under `Cases:`, and from then on the listed cases are what counts: a listed case a proof never
runs, or a verify never shows, blocks; a case nobody listed is a suggestion, whatever rule a reader cites
it under. Which cases belong on the list is decided once, at the spec, against the intent's own words, and
you see the list when you approve (D-132).

**The stage skills run this loop for you.** `/sdlc:intent`, and `/sdlc:to-plan` for the spec and the plan,
each write their document, interrogate it, refine it, and repeat until a round raises nothing blocking, then hand
back. You meet a blocker directly when a loop hands back early, when you edit a document yourself, or
when a refusal elsewhere names one.

### What it stops

While a blocking finding is open:

- **the next stage refuses to start** — `/sdlc:spec` will not derive from an intent without a clean round
- **`sdlc draft approval` refuses to print** an approval
- **the gate refuses the build**, as "a blocking finding is open"

### Answering it

**The stage answers it**: each stage, and `to-plan` across them, runs refine and interrogates again until a
round is clean. What you meet is the moment it stops and asks. To run a step by hand —
`/sdlc:refine <id> <artifact>`, then `/sdlc:interrogate <id> <artifact>` — is the same loop.

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

Each round is added to the ledger and chained to the ones before; once approved, none is rewritten. **How many rounds is deliberately not a question the tool
will answer.** On one real intent, round seven found the headline change was unreachable and round
eleven found that no success criterion failed without it; a tool that suggested stopping at round four
would have been confidently wrong.

**What it does show is what the rounds have cost.** Once rounds record their readers' usage, the panel's
`Spent` line gives the rounds so far, the weighted tokens they took and the split between the rules
reader, the perspectives, the reconciler and the adjudicator. It is a number to decide with, not a
warning, and nothing stops on it (D-121).

**What the tool watches for is going backwards.** The status panel's `Loop` line appears when a rule has
been answered and raised again, and names it. *Answered* means the rule went quiet — a round raised
nothing open against it — or that one finding was answered and the same finding came back. A round that
answers one finding and raises a different one under the same rule is not going backwards: that is the
reader moving on to the next place the same mistake was made, and it scores nothing. When one rule has been answered and raised again three
times, the loop stops and hands back to you. Under the rule, the panel lists **what came back** each time,
by round and finding, and says whether it was the same finding every time. Several different findings
under one rule is a document with several faults of one kind, not a loop: read them before deciding what
gives. Otherwise it usually means one of three things:

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
suggestion a reader raised: name its id — `/sdlc:steer <id> accessibility.S_01 security.S_02` — to take
it into the intent with your name on it, or to decline it. Name every one you want in the same run.

### After approval

Refine refuses once a cycle is approved: the documents are hash-locked, so editing one would break the
gate rather than satisfy it. **A build that finds the approved text cannot be followed records a deviation
instead** — [below](#when-the-approved-text-cannot-be-followed) — and keeps going. A plan you find wrong in
substance means withdrawing the approval first:

```pwsh
sdlc draft approval <id> --reopen
```

It leaves the reason line empty for you, and the rename records it. Then the loop runs as before, and the
documents are approved again. **A reopen after the build leaves its evidence and its verdict behind**:
once the cycle is approved again, `sdlc verify-chain` refuses a verify round older than that approval, so
delete `verify.md`, build, record the evidence and verify the documents as they are now approved. Reopen as
many times as it takes — the ledger keeps every entry and the last one stands.

**Once the plan is on the integration branch, reopen it on its plan branch, not on the build** (D-123). The
team and the factory read the plan there, so the amendment goes back by a pull request of its own:

```pwsh
sdlc branch <id> plan
sdlc draft approval <id> --reopen
```

Correct the documents, approve again, and `/sdlc:pr <id> --plan` opens it as an amended plan, leading with
your reason. The drafter refuses a reopen on the build branch and names this route. **The build you
already made is yours to keep or drop:** merge the integration branch into it to continue by hand, or
delete it and the factory builds the amended plan on its next run. Until you choose, the queue, the
sitrep and `sdlc status` on that branch say it was built before the plan was amended.

---

## A blocked verification

### What it is

Verify — which a green build runs, and remediation runs again after each pass — reads the recorded
evidence — `tests.json`, `diff.patch`, `base.sha` — and never
runs the tests or reads the source. Per criterion it asks which test the plan named, whether the summary
says it ran and passed, **whether that test actually establishes the criterion**, and which case it never
runs or which outcome it never reads. A test that passed but does not reach its criterion is *not met*. Each
Met quotes the assertion it rests on from `diff.patch`, so the interrogation that follows checks the quote
against the diff rather than trusting a test's name.

The verdict is written by a **fresh process** that never saw the build: the session running the skill
assembles the spec, the plan and the evidence, starts the adjudicator, and records what it returns. When
the verify document's own interrogation raises a blocker, a new adjudicator answers it — the session does
not edit the verdict.

It writes `pass` or `blocked`, and a findings round of its own in `findings/verify.json`. **`pr-ready`
refuses anything but `pass`.**

**Do not refine a verify document.** Its blockers are answered by a fresh adjudication when the outcome
is `pass`, and by remediation when it is `blocked`; `sdlc status` names whichever applies.

**The change itself is read too.** The verify round's diff reader is handed the intent, the plan, the
solution's decisions and `diff.patch`, and raises a finding — its id starts `diff.` — when the change does
something it was not given leave to:

| It cites | It means | You |
|---|---|---|
| `CON_07.2` | the change does something the plan did not describe: a drive-by refactor, an unrelated fix. Judged by what each change is for, never by which files it touched | take it back out; keep it as a `Noticed:` line for a later cycle |
| `CON_24` | the change takes what a recorded decision set aside, and neither the intent nor the plan declares that departure. The finding names the decision in its own words and quotes the lines | make the code keep the decision, or — when the work genuinely needs to depart — remediate records a `decision` deviation quoting it, for you to answer (D-156) |

**These go to `/sdlc:remediate` whatever the outcome reads**, because no adjudication changes the code;
`sdlc status` names it.

### Answering it

Say *fix it*, or `/sdlc:remediate`. Remediate works from two things: the criteria `verify.md` marks Not met, and the open blockers in the
latest verify round. They are not the same list. A Not met that the verdict's readers agree with raises
no finding, so a pass that worked only the findings could leave the criterion that blocked the cycle
untouched. It edits source and tests to fix the **cause**, never a planning document. After each pass it builds,
tests, records the evidence again and commits it, then **runs verify again** — whose adjudicator is a fresh
process that never saw the fix, so the session that wrote it never grades it (D-159).

**It runs while each pass reduces what is open, and stops when it is not getting anywhere** (D-66):

| It stops when | Because |
|---|---|
| a pass undoes what an earlier one answered | a loop undoing its own work is not going to finish |
| two passes in a row reduce nothing | nothing is moving |
| a third deviation bends the same approved text, and the verification still blocks | the loop is going round, not converging |

There is no pass ceiling. Nothing stops pass nine if pass nine is still making progress. **Every fourth pass
it checks in** — what closed, what is open — and asks whether to keep going, in a new session. That is not a
ceiling: it is you back in front of the one loop the progress rule cannot catch, which closes one finding
while opening another.

### When the approved text cannot be followed

**Remediate will not adjust the spec to match the code, and neither stage stops to ask you.** When the
approved text cannot be followed — a file the fix has to touch that the plan did not declare, a test whose
name has to change, a criterion no finite proof meets, a decision the work cannot keep — build or remediate
does the work and records a **deviation** in `deviations.md` (D-156). Each one quotes the approved text it
departs from, word for word, names the blocker with its evidence, and says what was done instead. A
criterion may only be narrowed, and the part dropped becomes a successor.

**A deviation doesn't excuse a blocker. It replaces the approved text the blocker was measured against,
and you sign the replacement.** Every verify reader reads them and judges against the documents as
deviated. A change that departs from the plan with no deviation describing it is blocked as an undeclared
one, so the build can bend the plan but not silently.

```pwsh
sdlc deviations <id>
```

lists each one with where it stands. **Before the pull request, answer every one**, with the same drafter
that approves:

```pwsh
sdlc draft approval <id>
```

On an approved cycle with unanswered deviations it drafts a `deviations` entry, each one listed with
`accepted: false` and `note: ""`:

| You leave | Means |
|---|---|
| `accepted: true` | accepted: it is part of what the cycle promises |
| `accepted: false`, a note written | declined: remediate redoes that part, with your note as its instruction |
| `accepted: false`, the note empty | not reviewed. The pull request check refuses until you answer it |

Accept some and decline others in one entry; each is held to the hash of its text, so one edited after you
answered it is unanswered again. A declined one has to be verified again before the pull request. **Under
`approval: none`** every deviation is accepted, and the pull request, whose body leads with them, is your
review.

**When the deviations show the plan is wrong in substance, reopen** — but answer every deviation first;
the drafter refuses a reopen until you have. The accepted ones are written into the amended plan, and the
declined ones, with your notes, are the reopen's reason.

```pwsh
sdlc draft approval <id> --reopen
```

A change that needs the intent changed, a wider scope, or a file another cycle claims is never a
deviation. The stage stops and hands back, and the choice is a successor or a reopen.

### Escalated

**A remediation loop that stops making progress sets the verify outcome to `escalated`.** It is computed,
never declared, and `pr` refuses it — a batch run with `--pr` opens a draft pull request for it instead,
which the check will not let merge (D-85). Its body leads with the criteria marked Not met and each open
blocker's rule, gist, place and fix. An escalated cycle is waiting for a person: read what the passes
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
collect, each concern once however many of a cycle's documents raised it.

**To see what became of one**, `sdlc suggestions --all` lists every suggestion with one word — `open`,
`accepted`, `declined`, `answered`, `withdrawn` — and the cycle that decided it. Nobody sets that word: it
is read from what a person wrote in an intent through `/sdlc:steer`, from a cycle begun with `--from
suggestion:<cycle>/<id>`, and from an abandonment's `declining` lines (D-115).

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
