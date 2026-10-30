---
name: batch-build
description: Complete every queued cycle -- build and verify each on its own branch, independently, and report the sitrep. With --pr, open a pull request for each; with --unattended, run as the factory does, asking nothing. Use when several cycles are approved and waiting.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[--pr] [--unattended]"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Bash(gh *), Bash(az *), Read, Write, Edit, Glob, Grep
---

# Complete the queued cycles

It collects the cycles waiting for a build, builds and verifies each on a branch of its own, and reports.
Run by a person, it works from this working copy. Run by the factory, it works from a fresh checkout of the
integration branch, on a schedule, asking nothing (`CONCEPT.md` §10.5, D-84).

## Read this first

**Everything here rests on the remediation bound.** A cycle that cannot be made to pass stops rather
than reaching a ready pull request, and that stopping is the only reason this is safe to run without
watching. The bound is a progress rule rather than a pass count: remediation continues while each pass
reduces what is open, and stops when a pass undoes earlier work or two in a row reduce nothing. **There is
no ceiling behind it (D-66)**, so a loop that keeps showing small improvements can run for a long time —
which is why the factory's pipeline job has a timeout, and why the first few runs are worth watching.

**It builds; it never merges.** A person reviews and merges each pull request, one at a time (D-84, D-86).

## 1. Find the work

```
git fetch origin
sdlc cycles --queued
sdlc cycles --claims
```

The first lists what to build — approved, no verify, not abandoned, and no `cycle/<id>/...` branch on the
remote — and every cycle held back, with why. **Build exactly the queued ones.** A held-back cycle is
somebody else's or still moving; the sitrep at the end names it, and a person decides.

The second marks the cycles whose plans stand up a backing service, which is what caps concurrency
(step 3). **It no longer schedules**: every cycle builds from the same base, and two that touch the same
file meet at merge, where a person is (D-86).

**Do not work either list out yourself.** The binary reads the same refs and the same plans the same way
every time; a list derived by reading files would come out slightly differently each run.

## Keep a task list, if your host has one

If your host gives you a task list, open one now that the work is known. If it does not,
carry on without one — nothing below depends on it.

**One line per cycle.** Every cycle this run will build, by id — and nothing else. No line per stage or per
remediation pass: a batch of nine is nine lines, and a person glancing in is scanning for the one that has
not moved.

**A tick means `passed`, and nothing else does.** Mark a cycle in progress when its build starts and
leave it there through verification and remediation, because the line that stays in progress is the
one worth noticing — remediation has no ceiling. Tick it only when its outcome is `passed`. For
`blocked`, `escalated` and did not finish, **leave it unticked and write the outcome onto the line**.

**The build stage keeps no list of its own inside a batch.** Its skill says so: the batch's line for a
cycle is the whole of what that cycle shows.

**The list is a view, never the record.** The outcomes are in each cycle's verify document, on its branch.

## 2. One worktree and one branch per cycle

Every queued cycle builds **independently**, in its own git worktree, on its own branch:

```
git worktree add --detach ../wt-<id>
sdlc branch <id> build-unattended
```

Run the one command `sdlc branch` prints, inside that worktree. It creates
`cycle/<id>/build-unattended` from the integration branch the plan was admitted to — or from the plan's own
branch on a solution with none. **Never build on the integration branch itself, and never build two cycles
in one worktree**: a verify could pass on another cycle's code.

Independent means no cycle waits for another and none is rebased onto another. A pull request rejected in
the morning costs the rest nothing.

**EF Core migrations are the one thing to watch.** Two cycles that each add a migration snapshot the same
parent model; each builds green alone, and the second to merge is regenerated, never merged
(`resolve-conflict` says how). `sdlc cycles --claims` flags a plan that mentions one.

## 3. Cap concurrency by what a plan needs

`sdlc cycles --claims` marks the cycles whose plans stand up a backing service. Those are the ones to cap.

| | Concurrency |
|---|---|
| service-free plans | run wide |
| plans standing up SQL Server, Redis, a Service Bus emulator | run under a low cap, two or three |

And export two variables into each cycle's worktree, before its build runs:

```sh
SDLC_RUN_MODE=test
SDLC_RUN_ID=<eight hex characters, different for every cycle in the run>
```

**Per cycle, not per run.** The collision is between two app hosts running at the same time, so one id
shared across the batch would not separate them. **You are setting them, not using them**: whether anything
reads them is the solution's AppHost's business, and `CONCEPT.md` §10.2 is the contract. Export them anyway.

**Named volumes are a worse collision than ports.** Two concurrent app hosts calling `WithDataVolume()`
share one volume, and that fails strangely rather than loudly. If this solution has no AppHost, nothing reads
the variables, and the cap above is what keeps concurrent runs apart.

## 4. Build and verify each

Per cycle, in its worktree: the build stage, then the verify stage, remediating while it makes progress.
**Verify as well as build** — a batch that cannot say which cycles succeeded cannot tell a ready pull
request from a draft.

| Outcome | What it is |
|---|---|
| `passed` | built, verified, its verify outcome `pass` |
| `blocked` | a criterion unmet, and remediation still reducing what is open when the run ended |
| `escalated` | remediation stopped: a pass undid earlier work, or two in a row reduced nothing. A person picks this up |
| did not finish | the build or the verify could not run to an outcome — the toolchain failed, the run was stopped |

**A sibling's outcome changes nothing about another cycle.** Each is reported, and each gets its own pull
request.

**Commit everything the cycle wrote on its branch**, verify included, before anything is pushed. The
branch is the record of the attempt.

## 5. With `--pr`: a pull request for every cycle

**Ask once, before the run** — not at the end — because the question is about volume and the person should
answer it before waiting rather than after. With `--unattended`, ask nothing: the schedule that started the
run was that decision, taken once by a person where it can be read (D-84).

As each cycle finishes:

| Outcome | Pull request |
|---|---|
| `passed` | `pr <id>`, ready for review |
| `blocked`, `escalated` | `pr <id> --draft`: the attempted code, the verify and the evidence, marked as not ready. Its check goes red, which is accurate |
| did not finish | none. Report it; nothing was verified, so there is nothing to review. Its branch, if pushed, holds it back until a person deletes the branch |

**Every cycle that reached an outcome gets a pull request** (D-85). A blocked cycle with none would be
invisible in the morning and built again the next night at full cost; its draft is where a person picks it
up. A failure opening one does not stop the others.

Everything in the `pr` skill applies: it asks `sdlc pr-ready` first, pushes the cycle's own branch, and
targets the integration branch.

**Never resolve a conflict between two cycles here.** They built independently and meet at merge.

## 6. Report, and stop

**One line per cycle**: its id, its outcome, its branch, its pull request's link if it has one, and for
anything not `passed`, what stopped it. Then:

```
sdlc sitrep
```

Show it verbatim and stop. Unattended, it is also the run's summary: the pipeline publishes it, so the
morning starts from that page.

**Plain `batch-build` never pushes.** Without `--pr` the branches stay local, and the person decides.

## What you may not do

- **Merge anything**, or push to the integration branch. Every change reaches it by a pull request a person
  merges.
- **Write a file only a person writes.** A cycle that cannot be made to pass is `blocked` or `escalated`;
  abandoning it is a person's decision, drafted by `sdlc draft abandon` and placed by them.
- **Build a held-back cycle**, or delete somebody's branch to free one.
