---
name: build
description: Implement an approved plan, then check the work against what the plan declared. Use after a plan is approved, including inside an unattended batch run.
argument-hint: "[cycle-id]"
allowed-tools: Bash(powershell.exe *) Bash(dotnet *) Bash(git *)
---

# Build an approved plan

Implement the plan for cycle **$0** and then check what you actually did against what it
declared.

This is the first stage that touches code, which makes it the first stage where anything is
irreversible. Everything below follows from that.

## Refuse before you start, if you must

Read `.sdlc/cycles/$0/plan.md`.

- **No plan, or a plan declaring no files** → stop. A plan with no declaration cannot be
  built against, because there is nothing to check the result for drift.
- **No `approved:` in the plan frontmatter** → stop. An unapproved plan has not passed its
  gate, and the gate is a person.

Refusing is a success. Report the reason and exit.

**There is no separate tier refusal here, and that is deliberate.** An earlier version stopped
whenever the predicted tier was above 1 and the run was unattended — while the dispatcher launched
those cycles anyway, because the tier was seen and signed off when the plan was approved. Both
rules shipped, so every approved tier-2 plan created a fresh worktree, refused at its first step,
and left that worktree unlocked with no `verify.md`: a state the derivation reads as **dead**,
meaning relaunch or discard. Two gates disagreeing produced a third outcome neither intended.

Approving the plan **is** the sign-off the tier asks for: the tier was computed from the declared
files and printed in front of whoever approved it, and the launch confirmation states it again
before any worktree is created. What still halts is the thing approval cannot cover — the work
turning out more dangerous than the plan predicted, which is checked below against the real diff.

## Gather the contract

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact ".sdlc/cycles/$0/plan.md" -Charter ".sdlc/CHARTER.md"
```

Take `declared-files` and `blast-radius` from that output. **Do not compute the tier
yourself** — it comes from the gatherer, which is the same function that produced the
predicted one, so the two are comparable.

Read the spec at `.sdlc/cycles/$0/spec.md` and note every **verifiable** acceptance
criterion id. Each one needs a test, and this stage is what owes them.

## Implement

Work through the declared files. **Touch the declared files and nothing else.**

A file you change that the plan did not declare becomes a `scope-drift` finding attributed
to this stage. If you discover the plan was wrong — a file it did not anticipate genuinely
must change — **stop and say so** rather than quietly widening the change. That is a plan
defect, and the honest move is to surface it, not to absorb it.

Where the work is large and the declared files are independent, delegate a file to a
subagent each. Where they are coupled, do it yourself; parallel edits to coupled files cost
more to reconcile than they save.

Two charter rules bind you while writing code:

- **Annotate tests with the criterion they prove** — `[TestProperty("AC", "<id>")]`. An
  unannotated test proves nothing traceable, and verification blocks on a criterion with no
  test.
- **No warning, no failed analyzer, no unjustified suppression.** If you genuinely need a
  suppression, write the justification next to it. A suppression without one is a finding,
  and the count is tracked in every `verify.md` (INV-06).

## Check adherence — the part that is not optional

Run the gatherer against the plan again. It now sees the working-tree diff and computes the
**actual** tier from it:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact ".sdlc/cycles/$0/plan.md" -Charter ".sdlc/CHARTER.md"
```

Read `plan-adherence` and `actual-blast-radius`, then run `dotnet build` and `dotnet test`.

Report four things: the actual tier and whether it escalated, files changed but not
declared, files declared but never touched, and whether the build and tests pass.

**A tier escalation halts. It does not report.** The work turned out more dangerous than it
was approved to be, and the approval does not stretch to cover it. Say what the predicted
and actual tiers were and stop.

A build that does not compile has implemented nothing, whatever the file count says.

## Report

State the outcome plainly — `built`, `halted-tier-escalated`, or `failed` — with the tiers,
any undeclared files, and the build and test results. Then:

> The next step is `/sdlc:verify $0`.

Do not write `verify.md` here. Do not merge, move any ref, or write a retro. Landing is a
separate, serialized, human-gated step.
