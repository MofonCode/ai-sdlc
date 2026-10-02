---
name: remediate
description: Apply a blocked verification's findings to source and tests. Edits code, never a planning document, and gets at most three passes. Use after verify recorded a blocked outcome.
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

**The guard is a hook rule, not a request.** A write to a planning document from here is denied at the
tool call, and that is deliberate: if a failing test could cause an agent to adjust the spec, the
criteria become whatever the code happens to do. The whole chain exists to stop that one inversion.

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

Say, per finding: what was wrong, what you changed, and whether you are confident it is the cause. Then:

```
sdlc status <id>
```

Show it verbatim and stop. The person decides whether to verify again.

**Do not verify your own remediation.** You have just spent effort on this code; a verification by the
session that wrote the fix is the investment bias the whole separation exists to remove. Verify runs as
its own stage, reading only the evidence.
