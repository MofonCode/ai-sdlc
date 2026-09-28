---
name: land
description: Open the pull request for a verified cycle. Use when verify reported pass.
argument-hint: "[cycle-id] [target branch, optional]"
allowed-tools: Bash(powershell.exe *) Bash(git *) Bash(gh *)
disable-model-invocation: true
---

# Land cycle `$0`

Open a pull request for cycle **$0** into `$1`, or the default branch if none was named.

This skill is human-invoked only. Landing is outward-facing and the one step that puts work in
front of other people, so it happens because someone asked, never because a model judged the
moment right.

## Refuse unless verify says pass

Read `.sdlc/cycles/$0/verify.md`.

- **`pass`** — proceed
- **`blocked`** — refuse. Say how many remediation passes remain and that `/sdlc:remediate $0` is
  the next step
- **`escalated`** — refuse, and say three things: that verification is still failing, that
  remediation was attempted and spent all three passes, and that a person owns the cycle. The
  distinction matters to whoever reads this: work that resisted three attempts is not the same as
  work nobody tried to fix
- **missing** — refuse. An unverified cycle has no claim to land

A hook refuses this too. If you find yourself reasoning about whether an exception is justified,
that is the hook's answer, not a judgement call.

## What the pull request carries

The body is assembled from the artifacts, not written fresh — CON_09 forbids restating what
another artifact owns. Link each and summarise in one line:

- the intent — the problem, in its own words
- the spec — how many criteria, all met
- the plan — the files and the tier
- the verify document — the outcome, and the evidence behind it
- any waived finding, with who waived it and why. A waiver must never be invisible in the pull
  request that carries it

Then, plainly, the suggestions verify recorded. They do not block, and this is the last moment a
person sees them before the cycle is history — after landing they stay findable only through
`/sdlc:suggestions`.

## What you do not do

- **You do not merge.** A code owner approves and merges, and it is never the agent that wrote the
  change. Say who is waiting on it
- **You do not amend the artifacts** to make the pull request read better. They are the record

## Then stop

Report the pull request URL and who it is waiting on.
