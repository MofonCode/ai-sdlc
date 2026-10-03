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

## 1. Find the work and read the claim graph

```
sdlc landable --all
sdlc claims
```

The first lists every cycle and its state; the ones to build are those whose gate is satisfied and which
have no verification yet. The second computes the claim graph and prints it: what each cycle claims, which
groups may run together, and every collision with the files it is about.

**Do not work the graph out yourself.** It is a set intersection over a file table, which the binary does
the same way every time; a graph you derived by reading plans would come out slightly differently each run,
and what it decides is what runs concurrently.

## 2. Run the groups

- Each group from `sdlc claims` runs in **parallel**, each cycle in its own git worktree.
- Cycles in different groups collide, so they run **in cycle-id order**, each rebased on the previous result.

The order is taken rather than declared. Two cycles that genuinely depend on each other with nobody having
said so means the second's build fails, a person learns, and they rerun. That is accepted: there is no
dependency declaration, because the skill that would have produced one was removed with `split`.

**Schedule around collisions rather than resolving them.** A merge you did not have to do is worth more
than a merge you did well.

**EF Core migrations serialise, always.** Two branches that each add a migration snapshot the same parent
model, and the merged chain breaks in a way that compiles and then fails at deploy — which is the worst
shape a failure can take. `sdlc claims` flags a plan that mentions one.

## 3. Cap concurrency by what a plan needs

`sdlc claims` marks the cycles whose plans stand up a backing service. Those are the ones to cap.

| | Concurrency |
|---|---|
| service-free plans | run wide |
| plans standing up SQL Server, Redis, a Service Bus emulator | run under a low cap, two or three |

And export two variables into each cycle's worktree, before its build runs:

```sh
SDLC_RUN_MODE=batch
SDLC_RUN_ID=<eight hex characters, different for every cycle in the run>
```

**Per cycle, not per run.** The collision below is between two app hosts running at the same time, so
one id shared across the batch would not separate them. A fresh id per cycle invocation does.

**You are setting them, not using them.** Whether anything reads them is the solution's business: an
AppHost is conditional, so a solution has one only if it needs more than one process, and most do not.
Export them anyway — a variable that is always present is one an AppHost can rely on, and the cost of
setting it where nothing reads it is nothing.

**What a solution's AppHost does with them**, which is the contract rather than your job:

| | `local` | `batch` |
|---|---|---|
| host ports | stable | dynamic |
| container names | stable | `{app}-{SDLC_RUN_ID}` |
| volumes | persistent, named | ephemeral, or named per run |
| databases | persistent | named per run |
| dashboard, OTLP endpoint | on | off |

**Named volumes are a worse collision than ports.** Two concurrent app hosts calling `WithDataVolume()`
share one volume — the same database files, two writers — and that fails strangely rather than loudly.
The run id belongs on container names, volume names and database names, not only on ports.

**If this solution has no AppHost, nothing reads these and the table constrains nothing** — which is
the state every solution starts in. The cap above is then the only thing keeping concurrent runs apart,
and it works because service-free plans have nothing to collide on. §10.2 records the gap.

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
Terraform near state. Those go to the resolve-conflict stage, which is scoped to exactly that enumerated
list rather than to whatever happens to conflict.
