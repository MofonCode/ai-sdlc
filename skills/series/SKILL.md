---
name: series
description: Run a series -- wave after wave of ready successors and parked cycles, planned and built one after another into one series branch, ending in one pull request, until its wave limit or an empty backlog. In a person's session it starts the series and runs sdlc series run, which starts this playbook again in a process of its own; there, with --unattended, it takes each wave, runs intent and to-plan for what needs planning and build for the wave, and pr once at the end. Under approval required it plans nothing and builds approved cycles in wave order. A playbook. Use when a person wants a sliced piece of work carried through several waves without stopping at each, and as the factory's run.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[<id>] [--waves <n>] [--wave-size <n>] [--unattended]"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Bash(gh *), Bash(az *), Read, Write, Edit, Glob, Grep
---

# Run a series

**A playbook** (D-186): it orchestrates and does no stage work itself. A series is **wave after wave**, each
built into one branch, `series/<id>`, and ends in **one pull request** for all of it. `intent` suggests each
successor's intent, `to-plan` checks and plans each cycle, `build` builds each wave on the series' branch, and
`pr` opens the one pull request at the end.

| Run as | Where | Does |
|---|---|---|
| `/sdlc:series --waves <n> [--wave-size <n>]` | a person's session | starts the series, runs `sdlc series run`, hands back what it printed — *In a person's session* |
| `/sdlc:series <id> --unattended` | the process `sdlc series run` starts, and the factory | wave after wave: take it, plan what needs planning, build it, record it; then the pull request — *In its own process* |

**What a series may do follows the approval setting** (D-137):

| Setting | A series |
|---|---|
| `approval: none` | plans and builds, wave after wave, asking nobody |
| `approval: required` | plans nothing: builds only cycles a person already approved, in wave order, taking an approved parked cycle back once what it waits on is ready on the series branch |

A person approves each plan before it is built where approval is required, so a run that planned past that point
would break what the setting is for. Where nobody reads plans before code anyway, planning ahead removes nothing a
person relied on.

---

# In a person's session

## Start it

**The person states how many waves it may run**: a series stops at that number or when nothing is ready,
whichever comes first, and an open-ended run is spend nobody chose. Given no `--waves`, ask for the number, once.
**A wave takes at most `--wave-size` items**, 3 when the person names none (D-228): what is ready past it waits for
the next wave, so the waves times the wave size is the most the series takes on. Pass `--wave-size` only when the
person gave one.

```
sdlc series start --waves <n> [--wave-size <n>]
```

It opens `series/<id>` from the integration branch, in a worktree beside the repository, with the series' record
committed there, and prints the worktree's path alone. Show what it said on standard error — what the series will
do under this solution's approval setting. **A refusal is final**: show it and stop.

## Run it

```
sdlc series run --solution <the path it printed>
```

**One command, and a process of its own**: it starts this playbook again in the series' worktree, with
`--unattended`, its tools named and **never with permission checks switched off**. It asks nothing, and it takes as
long as it takes: run it where your host lets a long command finish, and tell the person in one line that the
series has started and that you will report when it ends. **Never run the series yourself**, in this session: if
the command refuses, show what it said.

## Hand back

Show what `sdlc series run` printed verbatim, then:

```
sdlc series status <id>
```

verbatim — each wave, each cycle in it and where it stands — and the pull request's link. **Close with one plain
offer**: *review the pull request — merging accepts the whole series, report and all; to change anything in it,
say what instead* (`/sdlc:remediate`), naming the cycle. **Rejecting it is one step**: `sdlc draft abandon --series
<id>` prints the record a person places to stop every cycle that exists only in the series, with their reason; an
approved cycle already on the integration branch stays as it stands, to be built again. Then stop.

---

# In its own process: `--unattended`

Run by `sdlc series run` for a person, or by the factory in a run a person started. You are in the series'
worktree, on `series/<id>`. **Ask nothing**: starting the series was the decision. What a run could not settle
from what was written down goes where the report finds it — an intent's `(inferred)` marks, a build's
`deviations.md`, a stopped cycle's reason.

**It builds; it never merges, never approves, and never writes a file only a person writes.**

**One item at a time, never in parallel** (D-228). Every skill this playbook runs is run here, in this process, one
after another: each successor's intent, each cycle's spec and plan with its loop, each build. **Never hand items to
subagents or background tasks to run at once.** Run together, each one rebuilds the same context, the whole wave
spends at once, and a usage limit reached mid-wave leaves every item half done instead of the finished ones
finished and the rest untouched.

## Nobody approves a tool call here

`sdlc series run` starts this process with its tools named, as a build's are: the skills this playbook reaches,
`sdlc`, `git`, `dotnet`, `gh`, `az`, the file tools, and each command the solution's `build_tools` names (D-202).
Anything else is refused, and **a refusal is final**. Write every command so its check can read it: one plain
command per shell call, no variable or brace expansion, files through the file tools, bash rather than PowerShell
on Windows.

## 1. Take the next wave

```
sdlc series wave
```

It records the next wave in the series' record — at most the series' wave size, parked cycles first — and prints one
line per item with what starts it. **When it says
the series is finished** — its limit reached, or nothing ready against the series branch — go to §5.

## 2. Each item, as the line says

| The item | What to do |
|---|---|
| a successor | run **`intent`** with `--from successor:<slug> --unattended`: it opens the cycle on this branch and suggests its intent, asking nobody; what it inferred stays marked `(inferred)` for the report |
| a parked cycle | `sdlc unpark <id> --onto series/<id>`; its spec and plan are read again by `to-plan`'s loop, below |
| an approved cycle, under `approval: required` | nothing to plan: it is built in §3 |
| an approved, parked cycle, under `approval: required` — the line says `then build` | `sdlc unpark <id> --onto series/<id>`; its spec and plan are read again by `to-plan`'s loop, below, because its plan was written before the code it waited on existed. Read unchanged, its approval stands and it is built in §3. Commit the unparking with the wave |

Then run **`to-plan`** with `--unattended` for this branch: every intent this wave opened checked, then its spec and
its plan, and every unparked cycle's spec and plan read again, each with its loop. **A cycle whose loop cannot
settle stops there**, left planned or less with its reason in the hand-back; the others go on. Under `approval:
required` it reads again only the unparked cycles: **one whose spec or plan the loop changed has lost its approval,
and stops** — the gate refuses it until a person reopens and approves it — with that named in the hand-back.

Commit what the wave planned, on this branch: `git add .sdlc/cycles`, then `git commit -m "Series <id>: wave <n>
planned"`.

## 3. Build the wave

Run **`build`** with `--run --no-pr --unattended`: it builds every planned, unbuilt cycle of this wave one after
another on this branch — implemented, verified and remediated as one cycle is, each one's evidence recorded on top
of the one before. **A blocked verification stops only that cycle and the cycles that depend on it**: `build`
leaves it as it is and names what it held back, and its siblings carry on. The series carries on with what is ready.

## 4. Record it, and go again

Commit the series' record, `.sdlc/series/<id>/series.json`, and anything the wave left uncommitted:
`git add .sdlc`, then `git commit -m "Series <id>: wave <n> built"`. Then back to §1.

## 5. One pull request for the series

When `sdlc series wave` says the series is finished, run **`pr`** with `--run`: one pull request from `series/<id>`
into the integration branch, ready or draft as `sdlc pr-ready` says, its body a short summary followed by `sdlc
report --run`. **With no remote**, push nothing: the branch stays local, and the hand-back says so.

**When `pr` hands back that the series branch is behind the integration branch** (D-227) — somebody merged while it
ran — the last cycle's verification judged code without that. **A clean merge**: run **`remediate`** for the
series' last built cycle, with the pull request's conflict way in, which merges the integration branch in, builds,
records fresh evidence and verifies again — its fresh run is the one the run's check reads — then **`pr`** with
`--run` again. **A merge that stops on a conflict**: open nothing, and the hand-back names the files it stops on
and `/sdlc:remediate <id>` for a person; the factory never resolves one (D-86).

## 6. Report, and stop

Print `sdlc series status` verbatim and the pull request's link. That is what `sdlc series run` hands back to the
person.

## What you may not do

- **Stage work of your own.** An intent is `intent`'s; a spec, a plan and their loops are `to-plan`'s; code,
  verdicts and remediation are `build`'s, but for bringing the branch up to date before its pull request; the pull
  request is `pr`'s.
- **Plan anything under `approval: required`**, or build a cycle nobody approved there.
- **Merge anything**, approve anything, or write any file only a person writes.
- **Ask anybody anything.** A cycle that needs a person stops, with why, and the series goes on without it.
