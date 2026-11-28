---
name: wave
description: Run the next wave from the backlog -- start every successor that is ready, take back every parked cycle whose predecessor merged, plan them all in one sandbox, then build every cycle of the wave on one branch into one pull request. Under approval required it stops at the approvals, and a later run builds what was approved. A playbook: intent suggests each successor's intent, to-plan checks and plans, and sdlc build --run builds the wave's cycles a few at a time, each in a process of its own, merging each that passes. Use after merges land, when someone asks what is next, or to keep a piece of work moving to completion.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: ""
allowed-tools: Bash(sdlc *), Bash(git *), Read, Write, Edit, Glob, Grep
---

# Run the next wave

**A playbook** (D-186): `intent` suggests each ready successor's intent and asks what it inferred, `to-plan`
checks and plans every cycle the wave holds, its loop and its parking check included, and `sdlc build --run`
builds them, a few at a time and each in a process of its own, into **one pull request for the wave** (D-234).

Work that was sliced, or whose plans turned out to need each other, comes ready in waves: a successor once what
it succeeds has merged, a parked cycle once what it waits on has. This takes **every item ready now**, plans it
with the person, and builds it as one run.

It takes no id. It runs with the person: an intent is confirmed by the person it belongs to, and under
`approval: required` a plan is read before it is approved. **It never merges**: the wave's pull request is where
a person accepts it. A run that plans and builds wave after wave without asking is `series`, not this.

## 1. A wave already planned, or what is ready

**A wave planned and approved, but not built, goes first.** `git worktree list` names the open sandboxes,
`sandbox/<id>`; in each:

```
sdlc cycles --here --for build
```

Cycles listed there are a wave approved and waiting: go straight to §7 with that sandbox.

Otherwise, what is ready now:

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
of each is short. Do not stop at its hand-back, since the run hands back once; the intent is checked in §5, where
`to-plan` runs its loop.

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
leave with the rest of the wave.

## 5. Plan the new intents

Then run the **`to-plan`** playbook for this sandbox: every intent this wave opened checked, then its spec and its
plan, one cycle at a time, each with its loop and its `sdlc park` check — **a cycle this wave planned may itself be parked**,
behind one of its siblings, and that is the next wave forming.

## 6. Hand back the plans

One line per item: its id, its headline, and where it got to — `planned`, `read again` and unchanged, `read again`
and changed, `parked` with what it waits on, or stopped and why. Then `to-plan`'s hand-back — the panels and
one question about suggestions.

**Under `approval: required`, stop here, at the approvals.** Approving is the person's, one cycle at a time, in
the sandbox. Say in one line that once they have placed and committed the ones they agree with, `/sdlc:wave`
again builds them as one wave — or *build it* builds one alone, and `/sdlc:pr --plan` in the sandbox hands them to
the factory.

**Under `approval: none`**, the suggestions question is the last thing asked: once the person has answered it —
leaving them all is an answer — go on to §7.

## 7. Build the wave, into one pull request

**Only approved cycles are built** under `approval: required`, and their approvals must be committed in the
sandbox: the wave's branch starts from what the sandbox committed. If `git status` there shows one placed and not
committed, ask the person to commit it — it is their act — and never commit it for them.

Open the wave's branch, named by the sandbox's first cycle:

```
sdlc branch <sandbox id> wave
```

Run the one git command it prints: it opens `wave/<id>` from `sandbox/<id>` in a worktree beside the repository.
Then:

```
sdlc build --run --solution <the wave's worktree>
```

**One command, and the binary drives it** (D-187, D-234): it builds each cycle of the wave on a branch and in a
worktree of its own, taken from `wave/<id>`, at most two at once (`--parallel <n>` says otherwise) and never two whose
plans declare the same file — each implemented, verified and remediated as one cycle is, in a process of its own. Each
that passes is merged into `wave/<id>` with the whole suite run on the result; one that does not is taken out, its
branch pushed (D-235). Then it opens **one pull request for the wave**. Tell the person in one line that the build has
started and that you will report when it ends. **Never start the build yourself** in this session: if the
command refuses, show what it said.

## 8. Hand back once

Show what `sdlc build --run` printed verbatim — the run's report and the pull request's link. A cycle it took out
is named there with why and the branch its work is on. Then:

```
sdlc backlog
```

verbatim, so the person sees the waves still to come. **Close with one plain offer**: *review the pull request —
merging accepts the whole wave, report and all; to change anything in it, say what instead* (`/sdlc:remediate`).
Once it merges, `/sdlc:wave` takes the wave after. Then stop.

## What you may not do

- **Approve anything**, commit a person's approval, or write any file only a person writes.
- **Start a successor that is not ready**, or unpark a cycle `sdlc unpark` refuses.
- **Build an unapproved cycle** under `approval: required`, or build in this session: the build is `sdlc build
  --run`'s process.
- **Merge anything.** The wave ends at its pull request; accepting it is the person's.
