---
name: to-plan
description: Take every intent this branch carries to a plan -- write each cycle's spec, then its plan, each with its own interrogation loop -- and hand back once with every panel and one question about suggestions. Use when a person has read their intents and asks to take them, or all of them, to plan.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: ""
allowed-tools: Bash(sdlc *), Bash(git *), Read, Write, Edit, Glob, Grep
---

# Take the intents to plan

A person has written their intents, read them, steered or re-cut what they wanted, and is happy. This takes
**every cycle this branch carries** from there to a plan a person can approve — each cycle's spec, then its
plan, each with the loop its stage runs — and hands back **once**, at the end, instead of once per stage per
cycle (D-142).

It takes no cycle id. A batch is every ready cycle; one cycle is `/sdlc:spec <id>` and `/sdlc:plan <id>`.

**It stops at the plan.** Approving is a person's, one cycle at a time, and nothing here drafts, places or
assumes an approval.

## 1. Find the work

```
sdlc cycles --here --for spec
sdlc cycles --here --for plan
```

Each lists the cycles this branch carries that are ready for that step — a spec needs a clean intent and no
spec, a plan a clean spec and no plan. **Exit 1 is not a failure here**: it means not exactly one, and you want
the list. Take the ids from the indented lines. A cycle in neither list is not ready — still at its intent's
loop, already planned, or blocked — and `sdlc cycles --here` lists everything carried, so name each one you
leave out, with `sdlc status <id>` for why.

**Say what you will take, in one line per cycle, and go.** The person asked for this; the read-back is so they
can see which, not a question. If they named only some, take those.

## Keep a task list, if your host has one

If your host gives you a task list, open one now that the work is known. If it does not,
carry on without one — nothing below depends on it.

**One line per cycle.** By id and headline, and nothing else: no line per stage or per round, so a batch of
five is five lines and a person glancing in sees the one that has not moved.

**A tick means planned, and nothing else does.** Mark a cycle in progress when its spec starts, and tick it only
when its plan's latest round is clean. A cycle that stopped stays unticked, with why written on the line.

**The list is a view, never the record.** Where a cycle got to is its documents and its rounds, which
`sdlc status <id>` reads.

## 2. Spec, then plan, one cycle at a time, oldest first

For each cycle, **follow the `spec` skill for that id, and then the `plan` skill**, exactly as they are written —
the constraints, the decisions, the perspectives, the loop — with three differences:

- **Do not stop at a stage's hand-back.** Where `spec` or `plan` would show the panel and stop, carry on: the
  person asked for the whole run, and the hand-back comes once, in §3.
- **Do not ask about suggestions at each hand-back.** Leave every open suggestion open; §3 asks once, for every
  cycle together. A suggestion taken in at the spec would have cost one round less than one taken in after the
  plan, and that is the price of asking once — say so in §3, in a line.
- **A cycle that cannot go on stops alone.** A loop that will not converge, a reader reporting a conflict, a
  decision the person must keep or depart from, a question only they can answer: stop **that cycle** where it is,
  note what it needs, and go on to the next. Never answer for the person to keep a batch moving.

Take the cycles **one at a time**, never two stages at once: each stage's rounds are judged against the document
above it, and `sdlc round` refuses one out of order.

**This runs in your session**, which grows with every cycle. When it ends, say in a line that the next step —
reading and approving the plans — is worth a fresh session.

## 3. Hand back once

**One line per cycle**: its id, its headline, and where it got to — `planned`, `parked` with what it waits on
(the `plan` skill's `sdlc park` decides it), or stopped and why, in the person's terms, never as a count. Then
`sdlc backlog` verbatim when anything was parked, and, for each planned cycle:

```
sdlc status <id> --stage plan --brief
```

verbatim, one after another.

**Then the suggestions, once, for every cycle together.** Follow the hand-back in the `plan` skill — the file
under `.sdlc/reports/`, grouped by theme, one free-text question, the read-back by finding id — with every
planned cycle's open suggestions in the one file, grouped by cycle and then by theme. Whatever the person takes
in goes through `/sdlc:steer <id> <finding id>...`, one run per cycle.

**Then stop.** Approval is next, and it is the person's: `sdlc draft approval <id>`, read, and renamed into
place, for each cycle they approve — or `approval: none` lets the gate pass on clean rounds alone (D-137).

## What you may not do

- **Approve anything**, or write any file only a person writes.
- **Start a build.** The plan is the last artifact before a person approves.
- **Take a cycle another branch carries**, or one `sdlc cycles --here` did not list as ready.
- **Answer a question meant for the person** to keep the batch moving. A stopped cycle is a result.
