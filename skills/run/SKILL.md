---
name: run
description: Build approved plans unattended, one worktree per cycle. Use when several cycles have approved plans and someone wants them carried to pull requests without supervision.
argument-hint: "[cycle-id ...] or --all"
allowed-tools: Bash(powershell.exe *) Bash(git *) Bash(dotnet *)
disable-model-invocation: true
---

# Build approved plans unattended

Carry the cycles named in **$ARGUMENTS** — or every eligible one, if `--all` — from an approved plan
to a pull request, without supervision.

## Eligibility is an approved plan, and nothing earlier

A cycle is eligible only if `.sdlc/cycles/<id>/plan.md` exists, its interrogation left no blocking
findings, and **a person has approved it**.

Never start a batch from an accepted intent or an accepted spec. The plan gate is the one place a
person sees what will change before it changes; beginning earlier means the machine writes the
spec, writes the plan and executes it with nobody between the idea and the diff. That removes the
gate the whole process rests on.

Report what you skipped and why. A cycle silently omitted from a batch is worse than one refused
out loud.

## One worktree per cycle

Each cycle gets its own git worktree on its own branch, so parallel cycles cannot interfere and a
failed one leaves no debris in the main tree. Remove the worktree when the cycle finishes, whatever
the outcome.

## The loop

For each cycle: `build` → `verify` → while `blocked`, `remediate` → `verify` → then `land` on
`pass`.

Both endings are legitimate:

- **`pass`** — land it, and record the pull request
- **`escalated`** — stop that cycle and leave it for a person. Three remediation passes were spent;
  a fourth is not yours to take

A batch that escalates two of nine cycles has done its job. Do not treat escalation as failure to
be worked around, and never lower a bar to finish a batch.

## Nothing asks a person mid-batch

Hooks may refuse during a build; a hook that asks belongs at the landing boundary. If you find
yourself wanting to ask a question, that cycle is not eligible for unattended work — stop it, say
why, and continue with the rest.

## Then report

A table: cycle, outcome, and either the pull request or what a person now has to decide. Then the
totals. Escalations get named individually, not summarised as a count — each one is a person's
next task.
