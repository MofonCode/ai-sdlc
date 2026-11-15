---
name: remediate
description: Fix what a blocked verification found, or change what a person asks of a built cycle -- apply unmet criteria, findings or the person's words to source and tests, verify again in a fresh process, and go round while each pass reduces what is open, deviating within limits before it escalates. Edits code, never a planning document. Use after verify recorded a blocked outcome, or when a person reviewing the pull request says what to change.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[<cycle id>]"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Bash(gh *), Bash(az *), Read, Write, Edit, Glob, Grep
---

# Remediate a blocked verification, or a change a person asks for

Three ways in.

- **A verification found a criterion unmet** — the outcome is `blocked` — and your job is to make the code meet
  it: usually inside the build's own process, where `batch-build` runs you with nobody watching.
- **The build escalated**, and the person has said what to do: *say what to do, then fix it* is what the panel
  offered them. Their instruction is your starting point, and the loop below runs from there.
- **The cycle passed and its pull request is open, and a person reviewing it says what to change** — *change
  deviation 2: use X instead*, *revert that choice* — and your job is to make the code say that, verified, on
  the same pull request (D-181).

## Which cycle

**Given no cycle id, ask the binary which one is meant** (D-142):

```
sdlc cycles --here --for remediate
```

It finds a cycle that is `blocked`, `escalated`, or passed with its pull request open, and exits 0 printing the
one this working copy carries. Otherwise it lists the candidates, or says none is ready and why: ask the person
which, in one question, and **never pick one yourself** — the cycle a skill acts on is not a guess.

## Where you work

**Inside the build's own process**, you are already on the cycle's build branch, in the worktree the binary
started you in. Stay there.

**In a person's session**, the cycle's branch is checked out in the build's worktree, not in their checkout, so
ask the binary where to work:

```
sdlc branch <id> remediate
```

It prints the path of a worktree on the branch the pull request is from, creating it if absent. Work there for
every step below — edits, build, evidence, verify and push — and leave the person's own checkout alone.

## What you may and may not touch

| | |
|---|---|
| **May edit** | source, tests, and `deviations.md` where the approved text cannot be followed |
| **May never edit** | `intent.md`, `spec.md`, `plan.md`, `approval.md` |
| **Capped** | not on passes. When a pass undoes earlier work or two in a row reduce nothing, it may take one different approach, recorded as a deviation; it escalates when that stalls or regresses too, or when a third deviation bends the same approved text |

**`approval.md` is denied at the tool call. The other three are yours to keep.** The hook sees a tool
call and not which skill made it, so it cannot tell your edit to `spec.md` from the same edit made by
`refine`, where it is the whole job.

What the rule is for: if a failing test could cause an agent to adjust the spec, the criteria become
whatever the code happens to do, and the whole chain exists to stop that one inversion. What catches you
if you break it anyway is slower and certain — this cycle is approved, so its three documents are
hash-locked, and editing one makes `sdlc gate` refuse the build and `sdlc verify-chain` refuse the
merge, both naming which document moved. **You cannot edit your way past a criterion; you can only turn
a cycle that would have opened a pull request into one that cannot.**

So when the honest conclusion is "the plan's approved text cannot be followed", **record a deviation** rather
than editing the document or stopping (D-156). Every deviation reaches the person in the report, and merging the
pull request accepts it. **A criterion is never deviated from** (D-179): do not make the code satisfy a
criterion you believe is mistaken, and do not narrow it. A criterion the code cannot be made to meet escalates,
and a person reopens the plan — `sdlc deviations` and the pull-request check both refuse a deviation that
narrows one. **When what could not be followed is a gap in the framework itself** — the verify was never handed
a file, the evidence drops one — say so in the escalation, and the person files it as a successor marked
`framework:`, which mining hands to the framework rather than leaving in the solution's backlog (D-173).

A deviation is a section appended to `.sdlc/cycles/<id>/deviations.md`:

```markdown
## DEV_02 — The token refresh moves into a handler the plan did not name

Kind: plan
Departs from: plan.md, Shape and departures
> The client refreshes the token before each call.
Blocker: verify.F_04 under CON_22 — a call already in flight when the token expires still fails
Deviation: a delegating handler refreshes on a 401 and retries the call once.
Why: refreshing before each call cannot reach a call already in flight.
Files: `src/Quotes/TokenRefreshHandler.cs`
```

The quote decides the kind: `plan` and `proof` quote `plan.md`, `decision` quotes `decisions.md`, and nothing
quotes `spec.md`. The blocker is the finding it answers, by id and gist, or the
evidence. Never the intent, never wider, never a file another cycle claims — those stop you, and a person
decides. Check it with `sdlc deviations <id>`, and commit it with the code.

**A reopen is the person's**, when the deviations show the plan is wrong in substance. Once the plan is on the
integration branch it happens on the plan branch, not this one (D-123).

## When a person asks for a change

**At the pull request, a person accepts by merging — or says, in words, what they want instead** (D-181): *change
deviation 2: use X instead*, *revert that choice*, *keep the decision this departs from*. That is your
instruction, and you are in their session.

0. **Work in the worktree `sdlc branch <id> remediate` printed**, on the branch the pull request is from.
1. **Never act on a "no" that does not say what to do instead.** *Not like this* leaves you guessing, and a guess
   is a new choice nobody approved. Ask, in one question, what they want in its place, and ask again until you
   know.
2. **Record it on the entry**, beneath the deviation or choice it changes, in the person's words:

   ```markdown
   Changed: "use the platform's retry policy instead" — at the person's request
   ```

   If doing what they asked still has to bend the approved text, that is a new deviation with
   `Replaces: DEV_nn`; a choice they reverted needs nothing more than the line.
3. **Change the code and the tests to match**, then build, record the evidence with `--retire-pass` — the cycle
   passed, and only a person's request retires a pass — and verify as below, going round while it is working. Everything in *What you may and may not touch* still holds: a person's words never let you
   edit the intent, the spec or the plan, and never widen the cycle.
4. **Push to the same pull request** once the verification passes — `git push` on the branch it was opened from —
   and refresh its body's report from `sdlc report <id>`: write what it prints to a file and replace the body's
   report with it — `gh pr edit <number> --body-file <file>` on GitHub, `az repos pr update --id <number>
   --description` on Azure DevOps. Hand back that report, and the same offer as the build's: merging accepts it.

## Know whether the loop is still getting anywhere

```
sdlc pr-ready <id>
```

A pass is a verify findings round after the first, counted from what happened rather than from a counter
anybody could edit. What matters is not how many there have been but whether they are working:

| What it prints | What it means |
|---|---|
| *N findings open, N passes spent and making progress* | each pass has reduced what is open. Keep going — **there is no pass limit to run out of** |
| *… and the passes stopped converging: the next may take a different approach, recorded as a deviation, and must reduce what is open or the cycle escalates* | a pass undid earlier work, or two reduced nothing. Take one different approach (below), and the next pass has to reduce what is open |
| *escalated: the passes stopped converging again after a different approach, …* | the different approach did not work either. The cycle has stopped |
| *escalated: DEV_nn, DEV_nn, DEV_nn bend the same text in …* | a third attempt at the same approved text. The cycle has stopped |

**There used to be a hard three and there is deliberately no ceiling now (D-66).** A blind three failed
an honest cycle that needed a fourth pass, and let a cycle making no progress at all spend all three. So
the bound is on progress instead — but read what that costs you, because it lands on you rather than on
a counter: **nothing will stop you at pass nine.** If the passes are getting smaller and you can say what
the next one will fix, that is the rule working. If you are repeating yourself, say so and stop rather
than finding one more thing to adjust.

## When it stalls, deviate once before you escalate

**A stall is not yet an escalation** (D-179). When a pass undoes earlier work, or two passes reduce nothing, the
approach the plan described may be what is failing. Take a different one — recorded as a deviation, quoting the
approved text it departs from and resting on the finding or the failing test — and go round again. Three limits,
and none bends:

1. **Never change what *done* means.** Not the intent, not the spec, not a criterion, and never a test weakened
   so it passes. If the criteria are what is wrong, that is a person's call, and you escalate.
2. **Never widen the cycle.** No outcome or criterion added, no case reached that the approved text did not.
3. **Never go on without end.** If the passes after your deviation stall or regress too, or a third deviation
   would bend the same approved text, the cycle escalates: stop, and let the report say what is open.

## Work the findings

Two sources, and the job is both of them:

- **The criteria `verify.md` marks Not met.** These are why the outcome is `blocked`, and they are
  usually **not** findings: a reader raises a finding when a verdict overclaims, and a Not met it agrees
  with raises nothing. Read the Criteria section of `.sdlc/cycles/<id>/verify.md` for every entry that
  is not Met.
- **The open blockers**, with `sdlc findings <id> --stage verify`, which prints each finding where it
  stands after the latest round. Each blocking finding names what is wrong, where, and what would fix it.

Working only the findings is the easy mistake, because the command lists them and nothing lists the
criteria. A pass that answers every finding and leaves a Not met criterion standing has fixed nothing the
outcome was about.

**A finding whose id starts `diff.` is about the change, not the verdict**, and it is yours even when the
outcome reads `pass` (D-139). Two kinds:

- **`CON_25` — the change does something the plan did not describe.** Take it back out. If it is worth
  keeping, it is a `Noticed:` line in the commit message for a later cycle. **If the approved text cannot
  be followed without it**, keep it and record a `plan` deviation answering the finding.
- **`CON_27` — the change takes what a recorded decision set aside, and nothing declared the departure.**
  The finding quotes the decision in its own words. Make the code keep the decision. **If the work cannot
  be done without departing**, record a `decision` deviation quoting it; the report shows it, and the merge
  accepts it.

Fix the **cause**, not the symptom. The failure modes to avoid, in order of how tempting they are:

| Tempting | Why it is wrong |
|---|---|
| loosen the assertion | the criterion still is not met; now nothing says so |
| add a sleep or a retry | a flaky proof gets rerun until green, which teaches a team to ignore red |
| substitute the thing under test | the test then proves the substitution |
| skip the test | a skipped test is not evidence of anything, and the summary records it as skipped |
| widen the test to a case that passes | the criterion was about the case that does not |

The last three are all caught: the chain checks that each named test **ran and passed**, and skipped
counts as neither.

## Then build, test, and record again

```
dotnet build
dotnet test --coverage --coverage-output-format cobertura --results-directory TestResults
git commit -am "<what the pass fixed>"
sdlc evidence <id> --tests TestResults --build-log <log> --coverage TestResults
```

Commit the fix before recording: `sdlc evidence` refuses uncommitted code, because the evidence names the
commit it was recorded at (D-124). Recording retires the blocked `verify.md` and says so (D-161): do not
delete it yourself first. **On a cycle that passed**, add `--retire-pass`: a pass is retired only because a person
asked for a change, and without the flag `sdlc evidence` refuses it. Then commit the evidence and that removal on the cycle's branch. The next verification
reads the new evidence, so evidence left in your working tree is evidence it cannot see.

## Then verify again, and go round while it is working

**You run the loop; you never write the verdict** (D-159). After each pass, follow the `verify` skill for this
cycle: it starts the adjudicator in a fresh process that never saw your fix, hands it only the evidence, and
records what it returns. You start that process and read what it decided. You do not adjudicate, you do not
edit `verify.md`, and you do not argue a Not met back to Met — that is the investment bias the separation
exists to remove, and it stays removed because the judge is a separate process, not because a person typed
the next command.

Then read where the loop stands:

```
sdlc pr-ready <id>
```

| The outcome | What you do |
|---|---|
| `pass` | stop, and hand back |
| `escalated` — it stalled or regressed again after a deviation, or three deviations bend the same text | stop, and hand back |
| `blocked`, and *making progress* | work the next pass |

**There is no check-in on a count.** The build runs in a process of its own with nobody to ask (D-178), and the
progress rule, the one deviation it allows and the third-deviation stop are what bound it.

## Report and hand back

**Name every finding, deviation, criterion and rule by what it says, with its id trailing at most.** The
person has not read the round: *"the die has no label (S_04)"*, never *"S_04"* alone. A list of ids is
how one person ended up asking "what is dev 1 - 5?".

**One line per pass**, and in it **one line per unmet criterion and per finding** it worked, carrying three
things: what was wrong, what you changed — or the deviation you recorded — and whether you are confident that
was the cause. A finding you are not confident about says so in its line — that is the most useful thing in
the report, and it is the first thing a paragraph hides. Then:

```
sdlc status <id>
```

Show it verbatim and stop. **Close with one plain offer** that fits where it ended: on `pass`, hand back
`sdlc report <id>` and say *review the pull request — merging accepts it* (inside a batch, the batch opens it);
on `escalated`, what the passes did is the hand-back, and the person decides what happens next — *say what to
do, then fix it* starts this skill again from their instruction.
