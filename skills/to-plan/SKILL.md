---
name: to-plan
description: Take an intent to a plan -- one cycle, or every one this branch carries -- writing each cycle's spec, then its plan, each with its own interrogation loop, picking up wherever a cycle stopped, and hand back once with every panel, one question about suggestions, and the approvals drafted. Use when a person is happy with their intent and says take it to plan.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[<cycle id>]"
allowed-tools: Bash(sdlc *), Bash(git *), Read, Write, Edit, Glob, Grep
---

# Take the intent to plan

A person has written their intent, read it, and is happy. This takes it from there to a plan a person can
approve — the spec, then the plan, each with the loop its stage runs — and hands back **once**, at the end,
instead of once per stage (D-142, D-158). It is the one step between an intent and an approval: a person
never needs to know that a spec is written, checked and refined on the way.

**It stops at the plan.** Approving is a person's, one cycle at a time. It drafts the approval, because
drafting is not deciding; it never places one.

## 1. Find the work

**Given a cycle id, that cycle is the work.** Given none:

```
sdlc cycles --here --for to-plan
```

It lists the cycles this branch carries that are ready to be taken to plan — an intent, a plan not yet clean,
and no standing approval — wherever each one stopped. **Exit 1 is not a failure here**: it means not exactly
one, and when several are listed the person asked for all of them, so take every one. Take the ids from the
indented lines. A cycle not listed is not ready — already planned, approved, or abandoned — and
`sdlc cycles --here` lists everything carried, so name each one you leave out, with `sdlc status <id>` for why.

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

## 2. Pick up where it stopped, then spec, then plan, one cycle at a time, oldest first

```
sdlc status <id>
```

**Start from the first document that is not clean** — a cycle stopped mid-round is taken up there, not
started again:

| Where it stopped | What to do |
|---|---|
| the intent has no clean round | run the intent's loop — the `intent` skill's loop section — until it is clean, then go on |
| no spec | follow the `spec` skill for that id |
| a spec with no clean round | run the spec's loop — the `spec` skill's loop section — until it is clean |
| no plan | follow the `plan` skill for that id |
| a plan with no clean round | run the plan's loop — the `plan` skill's loop section — until it is clean |

Follow each stage's skill exactly as it is written — the constraints, the decisions, the perspectives, the
loop — with three differences:

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

## 3. Hand back once

**One line per cycle**: its id, its headline, and where it got to — `planned`, `parked` with what it waits on
(the `plan` skill's `sdlc park` decides it), or stopped and why, in the person's terms, never as a count. Then
`sdlc backlog` verbatim when anything was parked, and, for each planned cycle:

```
sdlc status <id> --stage plan --brief
```

verbatim, one after another.

**Then the suggestions, once, for every cycle together.** Follow the hand-back in the `plan` skill — grouped by
theme, one free-text question, the read-back by finding id — with every planned cycle's open suggestions
together, grouped by cycle and then by theme. Whatever the person takes in goes through `/sdlc:steer <id>
<finding id>...`, one run per cycle, which you run for them once they confirm.

**Then draft the approvals.** For each planned cycle, `sdlc draft approval <id>` — drafting is not placing,
so a person is handed the file rather than told to make it. Then one paragraph, filled in:

> The plans are clean. I've drafted each approval — `<path>` for `<id>`, one line each — read each, and
> place the ones you agree with by the rename it printed. If anything should be done differently, tell me
> and I'll carry it through the documents first. I can't place an approval myself — every agent write to
> one is denied — and that is what makes it mean something. The build runs best in a new session: say
> *build it* there (or `/sdlc:build`).

When the solution declares `approval: none` (D-137), draft nothing: say the plans are clean, that the plan's
pull request is the first time a person reads them, and that the build runs in a new session.

**Then stop.** This run stayed in one session, which grew with every cycle; the build is the step worth a fresh
one, and the paragraph above is the one place that says so (D-158).

## What you may not do

- **Place an approval**, or write any file only a person writes. Drafting one is not placing it.
- **Start a build.** The plan is the last artifact before a person approves.
- **Take a cycle another branch carries**, or one `sdlc cycles --here` did not list as ready.
- **Answer a question meant for the person** to keep the batch moving. A stopped cycle is a result.
