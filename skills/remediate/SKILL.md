---
name: remediate
description: Apply a blocked verification's unmet criteria and findings to source and tests. Edits code, never a planning document, and runs while each pass reduces what is open. Use after verify recorded a blocked outcome.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[<cycle id>]"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Read, Write, Edit, Glob, Grep
---

# Remediate a blocked verification

A verification found a criterion unmet. Your job is to make the code meet it.

## Which cycle

**Given no cycle id, ask the binary which one is meant** (D-142):

```
sdlc cycles --here --for remediate
```

It exits 0 printing the one cycle ready for this step, and that is the cycle. Otherwise it lists the
candidates, or says none is ready and why: ask the person which, in one question, and **never pick one
yourself** — the cycle a skill acts on is not a guess.

## What you may and may not touch

| | |
|---|---|
| **May edit** | source, tests, and `deviations.md` where the approved text cannot be followed |
| **May never edit** | `intent.md`, `spec.md`, `plan.md`, `approval.md` |
| **Capped** | not on passes. It stops when a pass undoes earlier work, two in a row reduce nothing, or three deviations bend the same approved text |

**`approval.md` is denied at the tool call. The other three are yours to keep.** The hook sees a tool
call and not which skill made it, so it cannot tell your edit to `spec.md` from the same edit made by
`refine`, where it is the whole job.

What the rule is for: if a failing test could cause an agent to adjust the spec, the criteria become
whatever the code happens to do, and the whole chain exists to stop that one inversion. What catches you
if you break it anyway is slower and certain — this cycle is approved, so its three documents are
hash-locked, and editing one makes `sdlc gate` refuse the build and `sdlc verify-chain` refuse the
merge, both naming which document moved. **You cannot edit your way past a criterion; you can only turn
a cycle that would have opened a pull request into one that cannot.**

So when the honest conclusion is "the approved text cannot be followed", **record a deviation** rather
than editing the document or stopping (D-156). Do not make the code satisfy a criterion you believe is
mistaken, and do not edit the criterion: a `criterion` deviation narrows it, quoting it word for word, and
names the successor the dropped part becomes. A person answers every deviation before the merge.

A deviation is a section appended to `.sdlc/cycles/<id>/deviations.md`:

```markdown
## DEV_02 — Criterion 7 narrowed to the server host

Kind: criterion
Departs from: spec.md, Acceptance criteria
> 7. The same implementation serves both hosts.
Blocker: verify.F_04 under CON_08 — the framework gives each host its own implementation, so no test can show one
Deviation: criterion 7 holds for the server host; the browser host's half becomes a successor.
Why: no finite proof reaches the criterion as written.
Successor: browser-host-parity — the browser host serves the same implementation
```

The quote decides the kind: `plan` and `proof` quote `plan.md`, `criterion` quotes `spec.md` and only
narrows, `decision` quotes `decisions.md`. The blocker is the finding it answers, by id and gist, or the
evidence. Never the intent, never wider, never a file another cycle claims — those stop you, and a person
decides. Check it with `sdlc deviations <id>`, and commit it with the code.

**A deviation a person declined is redone.** Its note is your instruction. If the redo still has to bend
the approved text, record a new deviation with `Replaces: DEV_nn`; do not edit the declined one.

**A reopen is the person's**, when the deviations show the plan is wrong in substance. It is refused until
every deviation is answered, and once the plan is on the integration branch it happens on the plan branch,
not this one (D-123).

## Know whether the loop is still getting anywhere

```
sdlc pr-ready <id>
```

A pass is a verify findings round after the first, counted from what happened rather than from a counter
anybody could edit. What matters is not how many there have been but whether they are working:

| What it says | What it means |
|---|---|
| *making progress* | each pass has reduced what is open. Keep going — **there is no pass limit to run out of** |
| *a pass undid earlier work* | a finding an earlier pass answered has been raised again, and the cycle has stopped |
| *the last two passes reduced nothing* | nothing is moving, and the cycle has stopped |
| *DEV_nn, DEV_nn, DEV_nn bend the same text* | a third attempt at the same approved text, and the verification still blocks: the loop is going round, and the cycle has stopped |

**There used to be a hard three and there is deliberately no ceiling now (D-66).** A blind three failed
an honest cycle that needed a fourth pass, and let a cycle making no progress at all spend all three. So
the bound is on progress instead — but read what that costs you, because it lands on you rather than on
a counter: **nothing will stop you at pass nine.** If the passes are getting smaller and you can say what
the next one will fix, that is the rule working. If you are repeating yourself, say so and stop rather
than finding one more thing to adjust.

**The one that should stop you cold is a regression.** If a pass broke something an earlier pass fixed,
do not reach for a fix: a loop that is undoing its own work is not going to finish, and what a person
needs to see is what the pass actually did. Escalation refuses the pull request and somebody picks it up,
and that stopping is what makes an unattended batch safe to run at all.

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

- **`CON_07.2` — the change does something the plan did not describe.** Take it back out. If it is worth
  keeping, it is a `Noticed:` line in the commit message for a later cycle. **If the approved text cannot
  be followed without it**, keep it and record a `plan` deviation answering the finding.
- **`CON_24` — the change takes what a recorded decision set aside, and nothing declared the departure.**
  The finding quotes the decision in its own words. Make the code keep the decision. **If the work cannot
  be done without departing**, record a `decision` deviation quoting it, for the person to answer.

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
commit it was recorded at (D-124). Then commit the evidence on the cycle's branch. The next verification
reads the new evidence, so evidence left in your working tree is evidence it cannot see.

## Report and hand back

**One line per unmet criterion and per finding**, carrying three things: what was wrong, what you
changed — or the deviation you recorded — and whether you are confident that was the cause. A finding you are not confident about says so in its line — that is the
most useful thing in the report, and it is the first thing a paragraph hides. Then:

```
sdlc status <id>
```

Show it verbatim and stop. The person decides whether to verify again.
**Say that it runs in a new session** — in the line of your own, not a paragraph. A session carried past its stage rebuilds its whole context after every pause, and that, not the judging, was most of what one solution's cycles cost (D-122).

**Do not verify your own remediation.** You have just spent effort on this code; a verification by the
session that wrote the fix is the investment bias the whole separation exists to remove. Verify runs as
its own stage, reading only the evidence.
