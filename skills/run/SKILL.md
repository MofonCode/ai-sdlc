---
name: run
description: Batch-build approved plans unattended, one worktree per cycle. Use when several cycles have approved plans and the user wants them built without supervision.
argument-hint: "[cycle-id ...] or --all"
disable-model-invocation: true
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Batch-build approved plans

Launch the cycles named in **$ARGUMENTS** — or every eligible one, if the user said `--all`
— each in its own worktree, unattended.

## What this covers, and what it deliberately does not

**The batch builds. It does not think.**

Intent, spec and plan are supervised: a person drives them, one cycle at a time, and
approves each plan. That is where the judgment lives, and it is also where a stage may need
to ask a question only the user can answer. An unsupervised spec is perfectly safe and
probably wrong.

This stage is the other half. A plan a person approved is a contract — a declared file list
and an approved tier — so executing it is mechanical and checkable. That is what can be
batched, and the only thing that can.

Per cycle, a run does exactly this: **build, then verify. Nothing else.** It does not author
artifacts, it does not interrogate, and it does not land anything.

## Check eligibility first, and name what you skip

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcCycleState.ps1
```

Only cycles whose derived state is **`planned`** may run — an approved plan, no build yet.
Everything else is skipped, and you must **name each skipped cycle and why**. A batch that
silently drops work reads as having covered everything.

| State | Why it is not eligible |
|---|---|
| `queued` | no spec or plan yet — this needs the supervised phase |
| `in-progress` | a worktree is locked; something is already building it |
| `dead` | a branch with no lock and no `verify.md` — relaunch or discard it first |
| `awaiting-landing` | already verified; it needs landing, not building |
| `blocked` | waiting on a charter intent that has not merged |
| `done` · `abandoned` | finished |

A plan without `approved:` in its frontmatter is **not** `planned`, however complete it
looks. That field is the gate, and the gate is a person.

## State the cost before launching

Dispatch through the script, which owns the confirmation:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Invoke-SdlcRun.ps1 -Intent <id> [<id> ...]
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Invoke-SdlcRun.ps1 -All
```

Pass `-WhatIf` first if the user has not seen the list, and show them what it prints.

**There is no concurrency cap, by decision.** A cap is the engine guessing at a resource
question it cannot know: this machine, what else is running, how much disk is free. The
control is the confirmation, and what it states is the **worktree count**, because each
worktree is a fresh checkout with its own restore and build. Eight of those is real disk and
real minutes, and that is the number worth seeing before saying yes.

Do not invent a cap of your own, and do not batch in waves to be helpful. If the user asks
for eight, launch eight.

## After launching

Tell the user how to watch, and what failure looks like:

> Watch with `tools/Get-SdlcCycleState.ps1`. A run that dies leaves an unlocked worktree
> with no `verify.md`, which reads as `dead` rather than as still working — relaunch or
> discard it.

Then stop. **Landing is not part of this.** Each cycle finishes at `awaiting-landing`, and
landing is serialized and human-gated: `tools/Complete-SdlcCycle.ps1` rebases, re-verifies
and squash-merges one cycle at a time.

That serialization is not caution for its own sake. Worktrees make file collision
structurally impossible but not **semantic** collision: cycle A renames a method, cycle B
adds a caller elsewhere, git merges both cleanly and the build breaks. Only the post-rebase
verification catches it.
