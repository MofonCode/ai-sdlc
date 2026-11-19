---
name: next
description: Pull the next wave from the backlog -- start every successor that is ready, take back every parked cycle whose predecessor merged, plan them all in one sandbox, and stop at the approvals. Use after merges land, when someone asks what is next, or to keep a piece of work moving to completion.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: ""
allowed-tools: Bash(sdlc *), Bash(git *), Read, Write, Edit, Glob, Grep
---

# Pull the next wave

**A playbook** (D-186): `intent` suggests each ready successor's intent and asks what it inferred, and `to-plan`
checks and plans every cycle the wave holds, its loop and its parking check included.

Work that was sliced, or whose plans turned out to need each other, comes ready in waves: a successor once what
it succeeds has merged, a parked cycle once what it waits on has. This takes **every item ready now** and carries
it as far as a person's approval, in one run, and says what the next wave waits on (D-147).

It takes no id. It runs locally, with the person: an intent is confirmed by the person it belongs to, and a plan
is read before it is approved. **The factory never runs this** — it builds what a person approved and nothing more
(D-143).

## 1. What is ready

```
git fetch origin
sdlc backlog
```

`Ready now` is the wave. **Nothing ready?** Show the backlog verbatim — it says what each item waits on — then
`sdlc cycles --queued`, whose lines name what merging each queued cycle starts, and stop: the next wave is a merge
away, and merging is the person's.

**Say what you will take, one line per item, and go.** The person asked for the next wave; the read-back is so
they can see which. If they name only some, take those.

## 2. Open one sandbox for the wave

Every item in this wave is planned together, in one sandbox (D-144): the first successor opens it as the `intent`
skill's §1 says, and every other opens inside it.

## 3. Successors: suggest each from where the slicing left it

For each ready successor, run **`intent`** with `--from successor:<slug>`: it fetches the outcome and
why the slicing left, starts the successor in this sandbox, suggests its intent against the code that merged, and asks
the person only what it inferred — the why above all, since the slicing wrote it before that code existed. Most
of each is short. Do not stop at its hand-back, since the run hands back once, in §6; the
intent is checked in §5, where `to-plan` runs its loop.

## 4. Parked cycles: take each back and read it again

For each ready parked cycle:

```
sdlc unpark <id>
```

Its plan was written before the code it needed existed, so **read its spec and its plan again against what
merged** — `to-plan`'s loop on its spec, then on its plan, one document at a time. A round that changes nothing leaves any approval it had standing. One that changes a document needs that
document's changes approved again: an approved cycle is reopened first, `sdlc draft approval <id> --reopen`, as
the `steer` skill says.

**Where it lives decides where you work.** A parked cycle not yet admitted is in the sandbox it was planned in:
work there. One already admitted is on the integration branch: unpark it in this sandbox, and its changed files
leave by the same admission as the rest.

## 5. Plan the new intents

Then run the **`to-plan`** playbook for this sandbox: every intent this wave opened checked, then its spec and its
plan, one cycle at a time, each with its loop and its `sdlc park` check — **a cycle this wave planned may itself be parked**,
behind one of its siblings, and that is the next wave forming.

## 6. Hand back once

One line per item: its id, its headline, and where it got to — `planned`, `read again` and unchanged, `read again`
and changed, `parked` with what it waits on, or stopped and why. Then `to-plan`'s hand-back — the panels and
one question about suggestions — and then:

```
sdlc backlog
```

verbatim, so the person sees the waves still to come.

**Then stop, at the approvals.** Approving is the person's, one cycle at a time; then *build it* carries each to
its pull request, taking its plan out of this sandbox itself (D-178) — or `/sdlc:pr --plan` here hands what they
approved to the factory. Once those merge, this skill takes the wave after. Say so in one line.

## What you may not do

- **Approve anything**, or write any file only a person writes.
- **Start a successor that is not ready**, or unpark a cycle `sdlc unpark` refuses.
- **Merge, or start a build.** The wave ends at the approvals; what comes after is the person's.
