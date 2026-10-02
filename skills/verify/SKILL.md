---
name: verify
description: Adjudicate a build against its spec, from the recorded evidence alone. Use after a build has recorded its evidence. Never edits code.
argument-hint: "<cycle id>"
allowed-tools: Bash(sdlc *), Read, Write, Glob, Grep
---

# Verify a build against its spec

Verify is the only artifact whose entire value is being **derived**. A claim that reaches beyond its
evidence turns an auditor into an author, and nothing downstream can tell which one wrote it.

Run `sdlc constraints --for verify` first.

## Read the evidence, and nothing else

In `.sdlc/cycles/<id>/evidence/`:

| File | What it is |
|---|---|
| `tests.json` | the normalised test summary: name and outcome per test |
| `diff.patch` | the change itself |
| `base.sha` | the commit the diff is measured from |
| the runner's own output | kept alongside, for when the summary is not enough |
| `build.log` | what the build said |

**Read `tests.json` rather than running the tests.** If you run them yourself you are producing new
evidence rather than adjudicating the build's, and the next reader cannot tell which run your verdict
describes. If the evidence is missing, say so and stop — that is a build that did not finish, not a
verification waiting to happen.

**Do not read the source to decide whether a criterion is met.** The diff tells you what changed. The
tests tell you whether it works. Reading the implementation and concluding it looks correct is the
single most tempting way to write a claim with no evidence behind it.

## Adjudicate criterion by criterion

Read `spec.md` for the criteria and `plan.md` for the proof committed to each. Then, for each
criterion in turn:

1. Which test did the plan name for it?
2. Does `tests.json` say that test ran and passed?
3. Does that test, as the diff shows it, actually establish the criterion?

Question three is where judgement lives, and where you may disagree with the plan. A test that ran and
passed but does not reach its criterion is **not met** — say so, cite the test and the diff lines, and
let the finding stand. That is `CON_06` caught late rather than never, and it is worth more here than a
pass that nobody can rely on.

Question two is also checked mechanically by `sdlc verify-chain`, so a verdict of `pass` on a criterion
whose named test did not pass will be refused at the merge. Agreeing with the evidence costs you
nothing; disagreeing with it costs the cycle a round trip.

## Cite everything

`CON_08` forbids a statement with no cited build output, test result or diff hunk — and equally forbids
one citing evidence that does not establish what the statement claims.

| Not a claim | A claim |
|---|---|
| criterion 2 is met | Met. `Two_requests_differ` passed; `diff.patch` shows the draw moved into a scoped service |
| the change looks correct | — nothing. That is not derivable from evidence |
| tests pass | Met. 14 passed, 0 failed, 0 skipped |

**Where the evidence does not reach, say so in `## Open`.** A proof that only covers 100 rows says
nothing about 50,000, and recording that limit is honest; claiming the criterion met is not.

## Choose the outcome

| outcome | When |
|---|---|
| `pass` | every criterion met, each on cited evidence |
| `blocked` | at least one criterion not met, and remediation has passes left |
| `escalated` | not yours to choose — it is what the budget running out makes of a blocked cycle |

**`escalated` is computed, not declared.** Three remediation passes are allowed; the passes are the
verify findings rounds after the first. Write `blocked` and let the count decide. A cycle you marked
escalated yourself would be one somebody has to work out the arithmetic of.

## Write it

`.sdlc/cycles/<id>/verify.md`:

```markdown
---
framework_version: 1.0.0
cycle: <id>
outcome: pass
---

# <what the change did, in one line>

## Criteria

1. Met. `Rolls_within_one_and_six` passed over 10,000 draws; `diff.patch` adds the
   service at `src/Dice/RollService.cs`.
2. Not met. `Two_requests_differ` failed: both draws returned 4. The service is
   registered as a singleton, so one instance holds one sequence.

## Open

- Nothing about behaviour beyond 10,000 draws; the test does not reach further.
```

## Then run the loop, and stop

Interrogate the verify document like any other artifact — `CON_08` applies to it, and the rounds it
accumulates are also what counts remediation passes. Refine it if a round finds a claim reaching beyond
its evidence.

```
sdlc status <id>
sdlc landable <id>
```

Show both verbatim. Then stop.

- **Passed?** The person decides whether to land.
- **Blocked?** The person decides whether to remediate. Do not start remediating; you
  have just finished judging, and the separation is the point.
