---
name: remediate
description: Work the blocking findings in a verify document, bounded at three passes. Use when verify reported blocked.
argument-hint: "[cycle-id]"
allowed-tools: Bash(powershell.exe *) Bash(git *) Bash(dotnet *)
---

# Remediate cycle `$0`

Read `.sdlc/cycles/$0/verify.md` and work its **open blocking** findings.

## Refuse unless there is budget

Read `remediation.passes_spent` from the verify frontmatter.

- `outcome: pass` — nothing to do. Say so and stop
- `outcome: escalated`, or `passes_spent` already 3 — **stop.** The budget is spent and a person
  owns this cycle. Say what remains and who it is waiting on. Do not try a fourth time, and do not
  edit `verify.md` to make room
- `outcome: blocked` — proceed

## Work only the blocking findings

Suggestions are not yours. They exist for a person to read, and acting on them here would add
scope nobody approved and reset nothing in your favour.

For each open blocking finding, apply its `remedy`. If the remedy is wrong or impossible, that is
worth more than a silent workaround — stop and say which finding, and why. The four ways out of a
stuck cycle all belong to a person: fix by hand, amend the spec and re-approve it, waive the
finding with a reason, or abandon the cycle.

The build's rules still hold while you work: no file the plan did not declare, no change inside a
declared file the plan did not describe, no test weakened to pass, no warning suppressed to clear
a build.

## Verification is the next step, and it is not yours

When the pass is done, **name `/sdlc:verify $0` as the next command** and stop. Do not invoke it: a
skill cannot reliably call another one. Do not report the findings as fixed either — nothing but a
fresh verification can tell whether a fix worked, and a remediation that certifies itself is the
same failure as an auditor producing its own evidence.

Verify increments `passes_spent`. Never write that number yourself.

## Then stop

Report which findings you worked, what you changed, and how many passes had been spent before this
one. Say that the outcome is unknown until `/sdlc:verify $0` runs, and that if it comes back
`escalated` the budget is spent and a person owns the cycle.
