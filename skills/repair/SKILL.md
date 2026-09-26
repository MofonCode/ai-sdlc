---
name: repair
description: Work through blocking interrogation findings, re-checking after each repair and halting when progress stops. Use when an interrogation returned blocking findings with repairs.
argument-hint: "[artifact path]"
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Repair the blocking findings on $0

Work through the `blocking` findings for **$0**, repairing what can be repaired and escalating
what cannot.

## Two rules that make this terminate

**1. Re-check after each repair, and look for what the repair INTRODUCED.**

This is not paranoia, it is measurement. In v01, three of five blocking findings in a repair
round were *introduced by the repairs of the previous round*. Replicated on a second codebase:
three of four. A repair is an edit, and an edit is exactly the thing interrogation exists to
check.

So after each repair, ask two questions, not one:

- Does the original finding still hold?
- **What did this change introduce?**

**2. Halt on no progress. Never "repair until clean".**

The finding rate does not decay — that was measured, not assumed, which is why "until clean"
never fires and would loop forever.

**Do not count the rounds yourself.** Ask:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact $0
```

Read `interrogation`. It reports the rounds already filed for this artifact, the number the next
one is, and one of five verdicts:

| `progress` | What it means |
|---|---|
| `first-round` | nothing to compare against yet |
| `reducing` | blocking findings are still falling round over round |
| `stalled-once` | one round with no net reduction. Another and the loop is done |
| `halted-no-progress` | **two consecutive rounds with no net reduction. Stop and escalate** |
| `converged` | the last round found nothing blocking |

This is a measurement, not a judgment, which is exactly why it is not yours to make. A rule an
agent is asked to remember across rounds is a rule it can talk itself past — and this is the one
rule that makes the loop terminate at all. It also means `round` is no longer something to pass:
the next number comes from what is already filed, so round 2 no longer overwrites round 1.

Halting is a success. A loop that runs forever burning tokens is not.

## What you may not do

- **Do not change a finding's `state`.** State was assigned in code from the citation — that
  is the anti-inflation guard, and re-deciding it here defeats the point of assigning it by
  rule. You resolve findings; you do not re-rank them.
- **Do not repair by deleting the claim.** Making a spec vaguer so an ambiguity finding stops
  matching is not a repair, it is erasure. The same goes for removing a criterion rather than
  making it testable.
- **Do not touch the charter.** A finding whose repair edits `.sdlc/CHARTER.md` is not
  repairable here at all. It requires an approved `charter-review` intent under INV-08, and no
  cycle may amend the charter governing it. File it and stop — but check first, below.

## Before filing a charter intent, check whether the question was already asked

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Find-SdlcCharterIntent.ps1 -Target INV-nn
```

Three answers, and each means something different:

| Verdict | Do this |
|---|---|
| `link-and-block` | A request is already pending. **Link to it and block.** Do not file a second. |
| `blocked-already-answered` | It was **refused**, and the script shows the reason. Block and surface that reason. |
| `file-one` | Nothing targets this rule. File one, recording the rule in `targets`. |

The middle row is the one that matters. A cycle blocked on a question already answered is a
different signal from one blocked on an open question: it says **the work needs rethinking,
not the charter**. Show the reason already given rather than making someone re-derive it. A
genuinely different case may still file a new intent, but it has to state what differs.

Without this check, a batch of eight cycles hitting one bad invariant files eight identical
requests and a person answers the same question eight times.

Record the rule in the intent's `targets` field, or the next cycle cannot find your request
and files a duplicate.

## Then block the cycle, which is a mechanism and not a note

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Block-SdlcCycle.ps1 -CycleId <cycle> -BlockedBy <charter intent id>
```

Pass `-WhatIf` first if you want to see what it will discard. It does three things, and each is
load-bearing:

- **Writes `blocked-by:`** into the cycle's intent. That link is the derivation's only input for
  the blocked state — without it the cycle reads as `dead`, and `dead` means *relaunch or discard*,
  so a dispatcher would start the work again into the same wall.
- **Discards the worktree and the cycle branch.** The spec and the plan live on that branch, so
  deleting it *is* re-running from spec — which is the point, because the work was built under rules
  that are about to change. A locked worktree is refused: a lock means an agent is still running
  there, and stopping someone else's agent is not this script's business.
- **Refuses if a retro exists.** A blocked cycle writes none, because a retro is the completion
  certificate and one here would make work that never landed read as done.

**It self-clears.** The moment the charter intent merges — approved into `intents/` or refused into
`intents/abandoned/` — the link resolves and the cycle is picked up again. There is no unblock step
and nothing to remember. The script reports `blocker-state`, and `already-refused` is worth reading
out loud: the decision has been made and this work needs re-scoping by a person, not another round.

Then commit the intent edit to master. The dispatcher reads the link from there, and it is a
semantic link rather than work, so it carries no diff of its own.

## Escalate rather than guess

A finding is an escalation, not a repair, when:

- the repair would edit the charter or a stack pack
- the finding turns on what the **user** wanted, and both readings are coherent
- you have attempted it and it did not hold twice

For the second case, **ask the user**. Name both readings and what each costs downstream. An
ambiguity resolved silently by an agent is a decision nobody made — and this stage is where
that temptation is strongest, because asking feels like failing to repair.

## Record a disposition for every finding you touch

A repair that is not recorded did not happen as far as anything downstream can tell. Verification
refuses to pass a cycle carrying an open or blocking finding with no disposition, and the retro
carries the dispositions into the corpus, which is what every charter amendment signal is computed
from.

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Set-SdlcDisposition.ps1 -CycleId <cycle> -Finding <id> -Disposition fixed
```

`rejected` and `accepted-wont-fix` each require `-Reason`, and the schema refuses them without
one. Use them for what they mean and never interchangeably: **`rejected` says the finding was
wrong**, which is evidence about the role that raised it; **`accepted-wont-fix` says the finding
was right and we are knowingly not acting**, which is evidence the invariant it cited may be too
broad. Dismissing a bogus finding as `accepted-wont-fix` narrows a perfectly sound invariant on
your behalf.

Note what this script will not let you do: **change a finding's state.** That was assigned in code
from the citation, and re-deciding it here is the inflation the guard exists to prevent.

## Report

State, per finding: repaired, escalated, or unresolved with the reason. Then the outcome —
`repaired`, `halted-no-progress`, or `escalated` — and the round count.

If anything was escalated, say what the user needs to decide, and stop rather than proceeding
to the next stage.
