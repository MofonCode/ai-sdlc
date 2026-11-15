# Running a cycle

One governed change, from why it exists to a pull request, in five steps — and what each one refuses if
you arrive at it early.

**This assumes the repository is adopted, and the drafts adoption left are placed and committed** —
`adopting-the-framework.md` and `declaring-the-stack.md` are the steps before, and `sdlc doctor` exits 0
when they are done. Nothing here is specific to what the change is.

**This is the happy path.** When an interrogation raises a blocking finding the loop cannot clear, or
remediation stops making progress, `handling-blockers.md` is the walk back. **If you have done this before**,
the cheat sheet, `../cheat-sheet.md`, is this same walk as one line per step.

| | Say | Or type |
|---|---|---|
| 1 | *add …*, *fix …* or *change …* — or just describe it | `/sdlc:intent` |
| 2 | *take it to plan* — and, reading the plan, *make it … instead* if you want it otherwise | `/sdlc:to-plan`, `/sdlc:steer` |
| 3 | — you rename the approval it drafted | — |
| 4 | *build it* | `/sdlc:build` |
| 5 | merge the pull request to accept it — or say what to change | `/sdlc:remediate` |

**You can say each step in a sentence**; the slash form is there if you prefer it, and on a host that loads
skills through Agent Plugins a sentence is the only form. Every hand-back ends by offering the next step, and
`sdlc status` names it whenever you lose your place. **You never need a cycle id** where one cycle is meant:
a skill given none asks `sdlc cycles --here`, and asks you which when the working copy carries several.

**The rules are read for you.** Every skill runs `sdlc constraints` before it writes or judges anything —
the global `CON_` set, the `PRO_` stack rules, then this solution's own `LOC_` — so a session never works from
recollection. Run it yourself to see them. The middle tier is the one your declared stack decides: the
framework ships five rules that have to name a framework, and a solution is held to exactly the ones its
stack file marks `true`; `declaring-the-stack.md` has why.

## 1. Begin an intent

Say what you want — something new, something broken, or something that should work differently — or
`/sdlc:intent`. **If you already know which, say so** (*it's a bug*, or `--type bug`, `add` or `change`) and the
interview asks that kind's questions from the start. If you don't, it works it out from your first answer and
checks with you in one line.

**Work bigger than one cycle is sliced first** (D-125). Before any cycle opens, the skill proposes the most
atomic slicing it can find: pieces that can each be built, verified and merged against the integration branch
alone. You accept it once, merge pieces back, or keep the work whole; nothing is sliced without you. A piece
that needs another's code is written as a **successor** — a file, not a cycle — and waits for that one to
merge. **Once an intent is written you can still cut it** (D-141): while none has a spec,
`/sdlc:intent --reslice <id>...` proposes a new slicing and applies it once you accept.

**Then it interviews you, one question at a time**, each with a few example answers, the next depending on
what you said. **_Not sure_ is always an answer.** A request shaped as a solution is turned back into a
problem: "add a cache" becomes "the dashboard takes fourteen seconds and people stop opening it". If you have
already decided something, say so — it is recorded under `Fixed decisions` with your name on it rather than
argued with. The skill reads what the solution already decided first, so it does not ask what is settled; if
you ask for something a recorded decision set aside, it says so, and asks whether to keep the decision or
depart from it.

**The last question is the perspectives' concerns** — keyboard use, who may see it, who hears when it fails.
Answer in your own words: *"anyone may use it, no sign-in; keyboard yes, and the result read out"*. A good
answer names a person and an outcome, or says plainly that it is not needed and why — *"make it secure"*
gives the spec nothing to test. It reads your answer back before writing it. What a perspective is, and
which ones run, is `../mechanisms/perspectives.md`.

**You do not open the cycle or switch branches yourself.** The skill allocates the id, opens a **sandbox** —
`../<repository>.sandboxes/<id>`, on `sandbox/<id>` — and says the id and the folder in one line (D-144). Every
cycle the work turns into opens beside the first, so one session sees all of them. Your own checkout is never
switched: the cycle's documents are written in the sandbox, so work on it from that folder.

The intent is then checked against the rules in a fresh process and revised until a round raises nothing
blocking, and **it hands back rather than going on** — deciding the intent is right is yours. Read it.

## 2. Take it to plan

Say *take it to plan*, or `/sdlc:to-plan`. It writes the spec, then the plan, each checked by its own readers
in fresh processes and revised until clean, and hands back **once** (D-158). Given several cycles, it takes
each in turn; one that needs you stops alone and the rest carry on. Interrupted part way, it picks a cycle up
where it stopped.

**The spec reads the intent and nothing else** — not the conversation, not the codebase. Every criterion has
to be able to fail, and its check has to be one this solution's testing layers can actually run. A criterion
that holds over several cases lists them beneath it under `Cases:`, and that list is all it covers from then
on: a case left off it is one no later stage will hold the build to (D-132).

**The plan must read the codebase**, because a file table invented from the spec alone is the fastest way to
a plan that cannot be built. It compares what the solution declares it is built from with what is there.
Where the shape rests on how a framework behaves and nobody has seen it happen, **it probes first**, and its
`## Assumptions proven` section quotes what the probe showed. Its `## Areas` section says which part of the
code each file belongs to (D-150). A plan that needs code another plan has not built yet is **parked** until
that one merges (D-145).

**It is the last document before you approve, so it is the one to read closely** — and the thing to read for
is proof adequacy. Ask of each check: *which case does it never run, and which outcome does it never read?*
If either has an answer, the proof is not adequate, and an inadequate proof is worse than none because it
produces a green verification instead of an obvious gap.

**At the hand-back it asks once about suggestions** — concerns a reader raised that the intent did not ask
for. It writes them to a file under `.sdlc/reports/`, grouped by theme with a suggested answer each, and you
answer in your own words — *"take A; decline B, it's only for me"*. Leaving them is a fine answer. **A
suggestion can only sharpen an outcome you already asked for**; one that adds an outcome is offered as a
successor instead, because every outcome you add is more the spec and plan must prove.

**Then it drafts the approval** and tells you where it is — step 3.

### Have it otherwise: steer

At any point before you approve, tell it what should be different, or `/sdlc:steer`:

```
/sdlc:steer "use the existing cache abstraction rather than a new one"
```

It edits the intent, spec and plan rather than regenerating them, records the correction with your name on
it, and checks again from the highest document it touched down. **Once a spec exists, steering changes
*how*, not *what*** — a correction that changes the problem is a different cycle, and the skill says so and
offers one. Before the spec, it may change the intent's outcomes too.

**After you have approved**, steering starts with a reopen: it drafts one, you place it, and it carries the
change through and hands you a new approval. `handling-blockers.md`, under *After approval*, has the rest.

## 3. Approve

One gate, covering all three documents, immediately before the build. **The skill drafted the approval at the
plan's hand-back** — `.sdlc/cycles/<id>/approval.approved.draft.md` — and printed the rename that places it,
once for bash and once for PowerShell, with absolute paths so it works from any directory, even when the draft
is in another worktree:

```pwsh
Move-Item C:/Repos/dice/.sdlc/cycles/20260929_1456_32/approval.approved.draft.md C:/Repos/dice/.sdlc/cycles/20260929_1456_32/approval.md
```

Above the rename it lists **any recorded decisions this cycle departs from**, each in the decision's own
words, and then **the decisions the plan says it makes** — the approach, the libraries, the hosting — so a
choice you did not ask for is in front of you before you approve rather than found after the build.

**The rename is the decision, and it is yours.** No tool places that file: the hook denies every agent write
to it by path and by shell command alike, there is no flag and no approve command for an exception to live
in, and that is precisely what makes a recorded decision mean something. The entry records a hash of each
of the three documents; **if any of them changes afterwards the gate refuses** until it is approved again.

**To send it back instead**, ask for that — `sdlc draft approval <id> --changes-requested`, `--deny` or
`--reopen` — and write the reason in the draft; the gate rejects a refusal that says nothing about why. It
refuses to draft at all while a blocking finding is open: **a blocking finding cannot be waived by any
mechanism**, and `handling-blockers.md` is how it is answered.

### Running without approvals

A person's approval before each build is on by default. To turn it off for this solution, run
`sdlc draft solution`, set `approval: none` in the draft, and rename it into place over `.sdlc/solution.md`.
Only a person can make that change: the hook refuses an agent's edit to the file. `sdlc settings` then shows
`approval  none  declared`.

**What still holds:** every planning document is interrogated, and a blocking finding still stops the build.
The documents built are the documents interrogated clean: one edited after its round is refused until it is
interrogated again. `verify-chain` still runs on every pull request.

**What you give up:** nobody signs the plan before code is written. The pull request the build opens is where a
person accepts it, and `verify-chain`, `sdlc status` and the sitrep all say no person approved it.

**To turn it back on,** set `approval: required` the same way. You can still approve a single cycle under
`none` with `sdlc draft approval <id>`, and the gate honours what you place in full.

## 4. Build it

Say *build it*, or `/sdlc:build`. **It goes all the way to the pull request** (D-178), and it is the first step
you are not expected to watch. It asks the gate first and stops if the gate refuses — `handling-refusals.md`
lists every reason with its remedy. Then it runs one command, `sdlc build-run <id>`, and the binary does the
rest:

1. **It records your approval and takes the plan out of the sandbox.** If the approval you renamed into place is
   not committed yet, it commits it — your act, recorded as yours. A sandbox is planned in and never merged, so the
   approved plan then moves onto a branch cut from the integration branch before anything is built. That is
   mechanical and local. A solution adopted before the integration branch existed has none, and its build starts
   from the sandbox.
2. **It builds in a process of its own**, in a worktree of its own. The build reads a great deal of code, and in
   your session every turn after it would pay for that; a builder that did not sit through the planning also
   works only from what was written down. It is the factory's own path for this one cycle, started by the binary
   with its tools named and permission checks left on, and it asks you nothing: saying *build it* was the
   decision. Your session stays where it was, and the skill reports when the process ends.
3. **It implements the plan, and only the plan.** Something worth fixing that the plan did not describe is left
   alone and noted in the commit, because creep is what reaches review dressed as approved work. On a new
   solution's first cycle, the skeleton the plan declared arrives first. It checks the suite actually ran rather
   than that the runner exited zero, and records the evidence — the diff, the commit it is measured from, the
   runner's own output, one normalised test summary.
4. **A fresh process verifies it** from the evidence alone: per criterion, which test did the plan name, did it
   run and pass, and **does that test actually establish the criterion?** A test that passed but does not reach
   its criterion is *not met*.
5. **It remediates while that is working.** On `blocked` it fixes the cause rather than the symptom, records
   fresh evidence and verifies again, going round while each pass reduces what is open. **When it stalls** — a
   pass undoes an earlier fix, or two reduce nothing — it may take one different approach, recorded as a
   deviation; it never changes what *done* means and never widens the cycle (D-179). If that stalls too, or three
   deviations bend the same approved text, the cycle **escalates**: it opens only a draft pull request, and
   `handling-blockers.md` is what to read.
6. **It opens the pull request** into the integration branch once the verification passes. With no remote it
   stops at a verified local branch and says so.

**Where the approved text cannot be followed, it records a deviation and keeps going** (D-156): a file the fix
has to touch that the plan did not declare, a test whose name has to change, a decision the work cannot keep.
**Where the approved text was silent and you would want to know what it chose** — a library, a pattern, a default
— it records a **choice** (D-180). Both go in `deviations.md`, and neither waits for an answer.

### The report

When the build ends, it hands back **the report** (D-181), which is also what `sdlc report <id>` prints:

- **every deviation** — which approved text it departs from, what was done instead, and why
- **every choice** the build made that nothing approved settled
- **every concern** its verification raised without blocking, the perspectives' included

The same report leads the pull request, so you read the same thing in both places. Its last line suggests
mining once the pull request has merged.

## 5. Review the pull request

**Merging is where you accept it** — the change, and every line of the report (D-181). A person merges it,
squashed, one pull request at a time, never the agent that wrote the change; the pull-request check refuses it
until the chain is complete. The body is the cycle rather than a summary somebody invented: the report first,
then the problem in the intent's own words, the criteria with the verification's verdict on each, the tier, the
risk section, and anything still `## Open`.

### Asking for a change

To have any line of the report otherwise, **say what instead**, in a session — *change deviation 2: use the
platform's retry policy instead*, *revert that choice*, *keep the decision this departs from* — or
`/sdlc:remediate`. It works in a worktree of its own on the pull request's branch — `sdlc branch <id> remediate`
prints it, so your checkout is left alone — records your words on the entry, changes the code and the tests,
verifies again in a fresh process, and pushes to the **same** pull request with the report refreshed. **It will not act on a "no" that does
not say what to do instead**: it asks until it knows, because a guess would be one more choice nobody approved.
It never edits the intent, spec or plan to do it — a change to what the cycle is for is a reopen, or another cycle.

### When the build escalates

Its pull request is a draft, and the report says what stopped it. **Say what to do, then *fix it*** —
`/sdlc:remediate` starts from your instruction, the same way it takes a change at the pull request, and goes round
until it passes or needs you again. When what stopped it is a criterion the code cannot meet, the instruction is a
reopen: no deviation changes what *done* means (D-179). `handling-blockers.md` has the rest.

**When the next pull request conflicts**, or goes red because an earlier merge broke it,
`/sdlc:resolve-conflict <id>` brings it up to date inside the cycle's declared files and verifies it again.

## Afterwards

**Mine what merged**: `/sdlc:mine`, then `sdlc mine record <the
proposal file>` once its drafts are placed — mined cycles become husks you may delete (D-152).
`mining-the-cycles.md` has the sitting step by step.

**Clear away what merged**: `sdlc tidy` prints the removal of every worktree and local branch whose work has
reached the integration branch — the plan branches, the build branches, the sandboxes and admissions — for you to
run. It deletes nothing itself, and a squash merge is why git cannot tell you this on its own (D-162).

**What was noticed and not acted on**: `sdlc suggestions`. Non-blocking is not trivial — two real ones from a
single trial cycle were "the suite did not actually run" and "this criterion's proof rests entirely on a
substituted source". **What keeps happening**: `sdlc propose constraints`, once a few cycles have run, reports
the rules cited in three or more distinct cycles. It proposes and never places; adding a rule to
`.sdlc/constraints.md` is a person's job.

**When merges make the next wave ready** — successors, and plans parked behind what merged — `/sdlc:next`
takes all of it: it starts each ready successor with a short interview, takes back each parked cycle, plans
them in one sandbox, and stops at the approvals (D-147). `sdlc backlog` lists what is waiting.

**With several cycles approved**, `/sdlc:batch-build --pr` builds, verifies and remediates them all, each on
its own branch, opens a pull request for each, and ends with the sitrep:

```pwsh
sdlc sitrep
```

It is the morning's page: what is ready to merge and in what order, which pull requests are expected to
conflict, what needs a person, and what is waiting and going stale. Everything unattended rests on the
remediation bound, so the first batch runs are worth watching.

## Where to go next

| | |
|---|---|
| this same walk with one solution in it, end to end | `../samples/rolling-a-die.md` |
| a blocking finding, a blocked verification, or a deviation | `handling-blockers.md` |
| what to do when something refuses | `handling-refusals.md` |
| this walk as one line per step | `../cheat-sheet.md` |
| every command on one screen | `../reference/commands.md` |
| what this solution is built from, and how to change it | `declaring-the-stack.md` |
| what each skill is and who starts it | `../../skills/README.md` |
