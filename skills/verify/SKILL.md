---
name: verify
description: Adjudicate a build against its spec, from the recorded evidence alone. Use after a build has recorded its evidence. Never edits code.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
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
| `diff.patch` | the change itself. The cycle's own artifacts are **not** in it — their absence is by design, not a missing commit |
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

**Every criterion the spec states gets an entry under `Criteria`, numbered to match.** A criterion whose
proof could not be reached is **not met** — it does not move to `## Open` as a limit of the proof. That
region is for what this document cannot settle, and whether a criterion was met is the one thing a verify
is for. `sdlc verify-chain` checks this too, so a verify that leaves one out is refused at the merge
however the outcome reads. A genuine limit still belongs under `## Open`: the template below shows one,
and the difference is that it bounds a criterion that was met rather than standing in for one that
was not.

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
| `blocked` | at least one criterion not met, and remediation is still reducing what is open |
| `escalated` | not yours to choose — it is what a remediation loop that stopped progressing makes of a blocked cycle |

**`escalated` is computed, not declared.** There is no pass ceiling: remediation continues while each
pass strictly reduces the open blocking findings, and stops when one undoes earlier work or two in a row
reduce nothing (D-66). The passes are the verify findings rounds after the first. Write `blocked` and let
the progress decide. A cycle you marked escalated yourself would be one somebody has to work out the
reasoning of.

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
sdlc pr-ready <id>
```

Show both verbatim. Then **at most two lines of your own**, and only the outcome in your own words.
Then stop.

**Nothing that is not already in `verify.md`.** You have just written the document that is the
adjudication; a hand-back that re-argues it produces a second, shorter verdict with no hash and no
round, and a reader who disagrees with one cannot tell which one the chain carries.

- **Passed?** The person decides whether to open a pull request.
- **Blocked?** The person decides whether to remediate. Do not start remediating; you
  have just finished judging, and the separation is the point.
