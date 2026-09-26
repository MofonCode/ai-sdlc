---
name: verify
description: Assemble verify.md from generated evidence and decide the cycle's outcome. Use after build, including inside an unattended batch run.
argument-hint: "[cycle-id]"
allowed-tools: Bash(powershell.exe *) Bash(dotnet *) Bash(git *)
---

# Verify cycle $0

Assemble `.sdlc/cycles/$0/verify.md` and decide the outcome.

**Everything in a verify document is derived.** Traceability comes from test annotations,
adherence from the diff against the plan's declaration, gate results from the build and the
suppression scan. Nothing here is written by hand — a hand-maintained traceability table
beside the real tests drifts the moment anything is refactored, and nothing detects it
(INV-03).

So: **do not compute any of these numbers yourself, and do not estimate one you could not
obtain.** A number you invented is an unfounded claim (INV-07), and it is worse than a
missing one because it looks like evidence.

## Gather the evidence and get the outcome

One command does both. It runs the fact-gatherer against the spec and the plan, runs the
gates, reads the findings, and **decides the outcome in code**:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcVerifyOutcome.ps1 -CycleId $0
```

**Transcribe what it returns. Do not form a view about it.** That separation is the whole
reason this is a script: an agent asked both to gather the evidence and to rule on what it
means rounds in the direction of done — not from dishonesty, but because the work visibly
succeeded ten minutes ago. So the decision is made by something that did not do the work.

The rule it applies, in order, so you can explain the answer rather than merely repeat it:

| Condition | Outcome |
|---|---|
| The actual tier exceeds the approved tier | `halted-tier-escalated` |
| The build failed, or a test failed | `blocked` |
| A verifiable criterion has no annotated test | `blocked` |
| An annotation names a criterion the spec does not declare | `blocked` |
| A suppression has no written justification | `blocked` |
| Any finding is still `open` or `blocking` with no disposition | `blocked` |
| Spec or plan is missing entirely | `blocked` — absence is not a clean cycle |
| Otherwise | `passed` |

Tier escalation outranks a failing build deliberately. A failing build is an ordinary
blocked cycle someone fixes; an escalation means the work turned out more dangerous than it
was approved to be, and reporting that as an ordinary failure invites a fix-and-retry
instead of a person looking.

**If the script disagrees with you, the script is right.** If you believe it is wrong, say so
in your report and stop — do not write a different outcome into `verify.md`. A gap between a
derived artifact and its source is **corrected, never signed off**.

Every number in its output is derived: traceability from test annotations, adherence from
the diff against the plan's declaration, both tiers from one function applied to two inputs.
**Do not compute any of these numbers yourself**, and do not estimate one you could not
obtain. A number you invented is an unfounded claim (INV-07), and it is worse than a missing
one because it looks like evidence.

## Write it

Write `.sdlc/cycles/$0/verify.md`. The frontmatter must validate against
`schemas/verify.schema.json` — read it rather than trusting this list:

```yaml
---
cycle-id: $0
plan: $0
charter-version: <from the charter frontmatter>
outcome: <from the table above>
predicted-tier: <n>
actual-tier: <n>
suppression-count: <n>
criteria-total: <n>
criteria-verifiable: <n>
criteria-covered: <n>
undispositioned: <n>
---
```

In the body put the traceability table (criterion → the test proving it), the adherence
result, the gate results, and every finding with its disposition. Each of those is
transcribed from the evidence above, not composed.

## Interrogate the document you just wrote

A verify document is an artifact like any other, and the charter points invariants at it and
defines what a gap in one looks like — a criterion with no traceability row, a suppression with no
justification, a finding with no disposition. None of that was ever checked, because nothing
interrogated this stage's output.

```
/sdlc:interrogate .sdlc/cycles/$0/verify.md
```

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Add-SdlcFindings.ps1 -CycleId $0 -Path ".sdlc/cycles/$0/interrogation.json"
```

**A finding here is not a reason to soften the outcome.** If the interrogation shows the document
claims something the evidence does not support, the document is wrong and the fix is the document
— a gap between a derived artifact and its source is corrected, never signed off. If it shows the
cycle is worse than the outcome says, re-run the outcome script and transcribe the new answer.

Then re-run the outcome script one last time: its own rule counts undispositioned findings, so
findings filed after it ran are findings it did not see.

## Report

State the outcome and what drove it. Then stop:

> This cycle is now `awaiting-landing`. Landing is serialized and human-gated —
> `tools/Complete-SdlcCycle.ps1` rebases, re-verifies and squash-merges.

**Do not land it.** Do not merge, do not move any ref, and do not write a retro. The retro
is written at landing, and it is the completion certificate — writing one for a cycle that
has not landed would make a dead cycle read as done.
