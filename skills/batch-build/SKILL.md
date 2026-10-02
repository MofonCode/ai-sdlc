---
name: batch-build
description: Complete every approved cycle in this working copy -- build and verify each, concurrently where their declared files do not collide. Use when several cycles are approved and waiting.
argument-hint: "[--land]"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Read, Write, Edit, Glob, Grep
---

# Complete the approved cycles

A local skill, executed rather than subscribed to. It collects the approved cycles in **this** working
copy, builds and verifies each, and reports. There is no shared queue and no daemon.

## Read this first

**Everything here rests on the remediation bound.** A cycle that cannot be made to pass stops at three
passes and escalates, and that stopping is the only reason this is safe to run without watching it. The
bound is implemented and tested, but it has been exercised in fixtures far more than in anger — so the
first few batch runs are worth watching, and a cycle that escalates is worth reading rather than
re-running.

## 1. Find the work and build the claim graph

```
sdlc landable --all
```

That lists every cycle and its state. The ones to build are the cycles whose gate is satisfied and which
have no verification yet.

For each, read the plan's `Files` section. Those are the files the cycle **claims**. Two cycles claiming
the same file collide, and the claim graph is how that is known **before** anything is built rather than
discovered in a merge.

## 2. Partition, then serialise what overlaps

- Cycles whose claims do not intersect run in **parallel**, each in its own git worktree.
- Cycles whose claims do intersect run **in cycle-id order**, which is chronological, each rebased on
  the previous result.

The order is taken rather than declared. Two cycles that genuinely depend on each other with nobody
having said so means the second's build fails, a person learns, and they rerun. That is accepted: there
is no dependency declaration, because the skill that would have produced one was removed with `split`.

**Schedule around collisions rather than resolving them.** A merge you did not have to do is worth more
than a merge you did well.

**EF Core migrations serialise, always.** Two branches that each add a migration snapshot the same parent
model, and the merged chain breaks in a way that compiles and then fails at deploy — which is the worst
shape a failure can take.

## 3. Cap concurrency by what a plan needs

Read each plan: does it touch the app host, or declare a dependency on a backing service?

| | Concurrency |
|---|---|
| service-free plans | run wide |
| plans standing up SQL Server, Redis, a Service Bus emulator | run under a low cap, two or three |

And set the run mode so concurrent runs do not collide on more than ports:

| | local dev | batch |
|---|---|---|
| host ports | stable | dynamic |
| container names | stable | `{app}-{shortGuid}` |
| volumes | persistent, named | ephemeral, or named per run |
| databases | persistent | named per run |
| dashboard, OTLP endpoint | on | off |

**Named volumes are a worse collision than ports.** Two concurrent app hosts calling `WithDataVolume()`
share one volume — the same database files, two writers — and that fails strangely rather than loudly.
The run id belongs on container names, volume names and database names, not only on ports.

## 4. Build and verify each

Per cycle, in its worktree: the build stage, then the verify stage. **Verify as well as build** — a batch
that cannot say which cycles succeeded cannot land the ones that did, so "completed" means verified.

Four outcomes:

| Outcome | What it is |
|---|---|
| `passed` | built, verified, ready to land |
| `blocked` | a criterion unmet, with remediation passes left |
| `escalated` | the budget ran out. A person picks this up |
| not attempted | downstream of a failure in the claim graph |

**A sibling's failure does not discard the cycles that completed.** Each can be landed individually, and
reporting them as a batch failure would throw away work that is fine.

## 5. Report, and stop

Per cycle: its id, its outcome, and for anything not `passed`, what stopped it. Then:

```
sdlc landable --all
```

Show it verbatim and stop.

**Plain `batch-build` never pushes.** Landing is a separate decision and the person makes it.

## With `--land`

Compose the two: land each completed cycle as it finishes. **Ask once, before the run** — not at the end
— because the question is about volume and the person should answer it before waiting rather than after.

Everything in the land skill still applies: `sdlc landable` before each push, a failure on one does not
stop the others, and each pull request reported individually with its link.

## When a file cannot be claimed exclusively

Some files are touched by almost every cycle and cannot be partitioned on: project files,
`Directory.Packages.props`, dependency-injection registration, `appsettings`, EF Core migrations,
Terraform near state. Those go to `sdlc resolve-conflict`, which is scoped to exactly that enumerated
list rather than to whatever happens to conflict.
