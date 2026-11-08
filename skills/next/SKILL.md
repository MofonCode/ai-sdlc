---
name: next
description: Pull the next wave from the backlog -- start every successor that is ready, take back every parked cycle whose predecessor merged, plan them all in one sandbox, and stop at the approvals. Use after merges land, when someone asks what is next, or to keep a piece of work moving to completion.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: ""
allowed-tools: Bash(sdlc *), Bash(git *), Read, Write, Edit, Glob, Grep
---

# Pull the next wave

Work that was sliced, or whose plans turned out to need each other, comes ready in waves: a successor once what
it succeeds has merged, a parked cycle once what it waits on has. This takes **every item ready now** and carries
it as far as a person's approval, in one run, and says what the next wave waits on (D-147).

It takes no id. It runs locally, with the person: an intent is an interview, and a plan is read before it is
approved. **The factory never runs this** — it builds what a person approved and nothing more (D-143).

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

## 3. Successors: interview each from where the slicing left it

For each ready successor, **follow the `intent` skill with `--from successor:<slug>`**: it fetches the outcome and
why the slicing left, starts the successor in this sandbox, and interviews the person against the code that
merged. Most of each interview is short. Run each intent's loop as the skill says; do not stop at its hand-back,
since the run hands back once, in §6.

## 4. Parked cycles: take each back and read it again

For each ready parked cycle:

```
sdlc unpark <id>
```

Its plan was written before the code it needed existed, so **read its spec and its plan again against what
merged** — `/sdlc:interrogate <id> spec`, refine until clean, then the same for its plan, one document at a
time. A round that changes nothing leaves any approval it had standing. One that changes a document needs that
document's changes approved again: an approved cycle is reopened first, `sdlc draft approval <id> --reopen`, as
the `steer` skill says.

**Where it lives decides where you work.** A parked cycle not yet admitted is in the sandbox it was planned in:
work there. One already admitted is on the integration branch: unpark it in this sandbox, and its changed files
leave by the same admission as the rest.

## 5. Plan the new intents

Then follow the `batch-plan` skill for this sandbox: spec then plan for every intent this wave opened, one cycle
at a time, each with its loop and its `sdlc park` check — **a cycle this wave planned may itself be parked**,
behind one of its siblings, and that is the next wave forming.

## 6. Hand back once

One line per item: its id, its headline, and where it got to — `planned`, `read again` and unchanged, `read again`
and changed, `parked` with what it waits on, or stopped and why. Then `batch-plan`'s hand-back — the panels and
one question about suggestions — and then:

```
sdlc backlog
```

verbatim, so the person sees the waves still to come.

**Then stop, at the approvals.** Approving is the person's, one cycle at a time; then `/sdlc:pr --plan` in the
sandbox admits what they approved, and the factory, or `/sdlc:batch-build`, builds it. Once those merge, this
skill takes the wave after. Say so in one line, and that the approvals are worth a fresh session.

## What you may not do

- **Approve anything**, or write any file only a person writes.
- **Start a successor that is not ready**, or unpark a cycle `sdlc unpark` refuses.
- **Merge, or start a build.** The wave ends at the approvals; what comes after is the person's.
