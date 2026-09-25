---
name: repair
description: Work through blocking interrogation findings, re-checking after each repair and halting when progress stops. Use when an interrogation returned blocking findings with repairs.
argument-hint: [artifact path]
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
never fires and would loop forever. So track the count of unresolved blocking findings across
rounds. **Two consecutive rounds with no net reduction means stop.** Report
`halted-no-progress` with what remains.

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

## Escalate rather than guess

A finding is an escalation, not a repair, when:

- the repair would edit the charter or a stack pack
- the finding turns on what the **user** wanted, and both readings are coherent
- you have attempted it and it did not hold twice

For the second case, **ask the user**. Name both readings and what each costs downstream. An
ambiguity resolved silently by an agent is a decision nobody made — and this stage is where
that temptation is strongest, because asking feels like failing to repair.

## Report

State, per finding: repaired, escalated, or unresolved with the reason. Then the outcome —
`repaired`, `halted-no-progress`, or `escalated` — and the round count.

If anything was escalated, say what the user needs to decide, and stop rather than proceeding
to the next stage.
