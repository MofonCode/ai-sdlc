---
name: remediate
description: Apply a blocked verification's findings to source and tests. Edits code, never a planning document, and gets at most three passes. Use after verify recorded a blocked outcome.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id>"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Read, Write, Edit, Glob, Grep
---

# Remediate a blocked verification

A verification found a criterion unmet. Your job is to make the code meet it.

## What you may and may not touch

| | |
|---|---|
| **May edit** | source, tests |
| **May never edit** | `intent.md`, `spec.md`, `plan.md`, `approval.md` |
| **Capped** | three passes, then the cycle escalates |

**`approval.md` is denied at the tool call. The other three are yours to keep.** The hook sees a tool
call and not which skill made it, so it cannot tell your edit to `spec.md` from the same edit made by
`refine`, where it is the whole job.

What the rule is for: if a failing test could cause an agent to adjust the spec, the criteria become
whatever the code happens to do, and the whole chain exists to stop that one inversion. What catches you
if you break it anyway is slower and certain — this cycle is approved, so its three documents are
hash-locked, and editing one makes `sdlc gate` refuse the build and `sdlc verify-chain` refuse the
merge, both naming which document moved. **You cannot edit your way past a criterion; you can only turn
a cycle that would have landed into one that cannot.**

So when the honest conclusion is "the criterion is wrong", say it and stop. Do not make the code satisfy
a criterion you believe is mistaken, and do not edit the criterion. The move is
`sdlc draft approval <id> --reopen`, which withdraws the approval with a recorded reason so the planning
documents can be rewritten and re-approved. That is the recovery path, and it leaves a record.

## Know which pass you are on

```
sdlc landable <id>
```

It says how many passes are spent and how many remain. A pass is a verify findings round after the
first, so this is counted from what happened rather than from a counter anybody could edit.

**Three, then the cycle stops.** Not "three then try harder". A cycle that could not be made to pass
within its budget escalates, landing refuses it, and a person picks it up. That stopping is the only
reason an unattended batch is safe to run at all, so do not work around it — and if you are on the third
pass and not confident, say that rather than spending it.

## Work the findings

Read `findings/verify.r<n>.json` for the latest round. Each blocking finding names what is wrong, where,
and what would fix it.

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
dotnet test
sdlc evidence <id> --tests <results> --build-log <log>
```

Commit on the cycle's branch. The next verification reads the new evidence, so evidence left in your
working tree is evidence it cannot see.

## Report and hand back

**One line per finding**, carrying three things: what was wrong, what you changed, and whether you are
confident that was the cause. A finding you are not confident about says so in its line — that is the
most useful thing in the report, and it is the first thing a paragraph hides. Then:

```
sdlc status <id>
```

Show it verbatim and stop. The person decides whether to verify again.

**Do not verify your own remediation.** You have just spent effort on this code; a verification by the
session that wrote the fix is the investment bias the whole separation exists to remove. Verify runs as
its own stage, reading only the evidence.
