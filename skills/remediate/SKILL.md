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

## Re-verify, always

When the pass is done, run `/sdlc:verify $0`. Nothing else can tell whether the fix worked, and a
remediation that reports its own success is the same failure as an auditor producing its own
evidence.

Verify increments `passes_spent`. Never write that number yourself.

## Then stop

Report which findings you worked, what you changed, and what verify said afterwards. If the new
outcome is `escalated`, say plainly that the budget is spent and what a person now has to decide.
