---
name: series
description: Run a series -- wave after wave of ready successors and parked cycles, each wave planned, its cycles built a few at a time and merged into one series branch, ending in one pull request, until its wave limit or an empty backlog. In a person's session it starts the series, or continues one that stopped, and runs sdlc series run, which drives the waves from the binary and starts this playbook in a process of its own for each step that needs language; there, with --unattended --step plan, it runs intent and to-plan for what the wave needs planned, and with --step stops it adds the fix a test that comes and goes needs. Under approval required it plans nothing and the binary builds approved cycles in wave order. A playbook. Use when a person wants a sliced piece of work carried through several waves without stopping at each, and as the factory's run.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[<id>] [--waves <n>] [--wave-size <n>] [--parallel <n>] [--unattended --step plan|stops [--stops <file>]]"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Bash(gh *), Bash(az *), Read, Write, Edit, Glob, Grep
---

# Run a series

**A playbook** (D-186): it orchestrates and does no stage work itself. A series is **wave after wave** into one
branch, `series/<id>`, and ends in **one pull request** for all of it. `intent` suggests each successor's intent and
`to-plan` checks and plans each cycle. **The binary does the rest** (D-234): `sdlc series run` builds each wave's
cycles a few at a time, each in a process of its own, merges each that passes with the suite run on the result,
takes out a cycle that cannot be saved (D-235), and starts the process that opens the pull request.

| Run as | Where | Does |
|---|---|---|
| `/sdlc:series --waves <n> [--wave-size <n>] [--parallel <n>]` | a person's session | starts the series, runs `sdlc series run`, hands back what it printed — *In a person's session* |
| `/sdlc:series <id>` | a person's session | continues a series that stopped: runs `sdlc series run` in its worktree again — *Continue one that stopped* |
| `/sdlc:series <id> --unattended --step plan` | a process `sdlc series run` starts | plans the open wave — *In its own process* |
| `/sdlc:series <id> --unattended --step stops --stops <file>` | a process `sdlc series run` starts | answers what the wave's build left stopped — *In its own process* |

**What a series may do follows the approval setting** (D-137):

| Setting | A series |
|---|---|
| `approval: none` | plans and builds, wave after wave, asking nobody |
| `approval: required` | plans nothing: builds only cycles a person already approved, in wave order, taking an approved parked cycle back once what it waits on is ready on the series branch, and never adds a fix |

A person approves each plan before it is built where approval is required, so a run that planned past that point
would break what the setting is for. Where nobody reads plans before code anyway, planning ahead removes nothing a
person relied on.

---

# In a person's session

## Start it

**The person states how many waves it may run**: a series stops at that number or when nothing is ready,
whichever comes first, and an open-ended run is spend nobody chose. Given no `--waves`, ask for the number, once.
**A wave takes at most `--wave-size` items**, 3 when the person names none (D-228): what is ready past it waits for
the next wave, so the waves times the wave size is the most the series takes on. **A wave builds at most
`--parallel` of its cycles at once**, 2 when the person names none (D-236): each cycle is built in a process of its
own, so two at once reach a usage limit about twice as fast. Pass either only when the person gave one.

```
sdlc series start --waves <n> [--wave-size <n>] [--parallel <n>]
```

It opens `series/<id>` from the integration branch, in a worktree beside the repository, with the series' record
committed there, and prints the worktree's path alone. Show what it said on standard error verbatim — what the
series will do under this solution's approval setting, and **its list** (D-231): everything it may take, in the waves
it is forecast to take them in, with what it left out and why. The list is the series' scope; each wave takes from it
what is ready then. **A refusal is final**: show it and stop.

## Run it

```
sdlc series run --solution <the path it printed>
```

**One command, and it drives the series itself**: wave after wave, it starts a process for each step — this playbook
to plan a wave, the build playbook for each cycle, `pr` at the end — each with its tools named and **never with
permission checks switched off**. It asks nothing, and it takes as
long as it takes: run it where your host lets a long command finish, and tell the person in one line that the
series has started and that you will report when it ends. **Never run the series yourself**, in this session: if
the command refuses, show what it said.

## Continue one that stopped

**Given a series' `<id>` and no `--unattended`**, the person is continuing a series that stopped — a usage limit, a
closed session, a process killed. Start nothing new: find its worktree, the one `git worktree list` shows on
`series/<id>`, and run it again as above, `sdlc series run --solution <that worktree>`. Its process picks up where
the series stopped, the wave it was in first (D-230). With no worktree on that branch, say so and stop: the series
was cleaned up, or never started here.

## Hand back

Show what `sdlc series run` printed verbatim, then:

```
sdlc series status <id>
```

verbatim — each wave, each cycle in it and where it stands, and what it took out with the branch its work is on —
and the pull request's link. **Close with one plain
offer**: *review the pull request — merging accepts the whole series, report and all; to change anything in it,
say what instead* (`/sdlc:remediate`), naming the cycle. **Rejecting it is one step**: `sdlc draft abandon --series
<id>` prints the record a person places to stop every cycle that exists only in the series, with their reason; an
approved cycle already on the integration branch stays as it stands, to be built again. Then stop.

---

# In its own process: `--unattended --step <step>`

`sdlc series run` drives the series from the binary (D-234): it takes each wave, starts this playbook in a process of
its own to **plan** it, builds the wave's cycles a few at a time — each in a branch and a worktree of its own, each
merged into `series/<id>` when it passes, with the whole suite run on the result — and records the wave built and
pushes it. When a build leaves cycles stopped it starts this playbook again to **answer** them. The pull request at the
end is a process of its own. **You do one step, then stop.**

You are in the series' worktree, on `series/<id>`. **Ask nothing**: starting the series was the decision. What a run
could not settle from what was written down goes where the report finds it — an intent's `(inferred)` marks, a
stopped cycle's reason.

**You never build, merge, approve, or write a file only a person writes.** Building and merging are the binary's.

**One item at a time, never in parallel** (D-228). Every skill this playbook runs is run here, one after another.
**Never hand items to subagents or background tasks to run at once**: parallel builds are the binary's, which runs
each in a process of its own and merges them one at a time; planning stays one item at a time.

## Nobody approves a tool call here

`sdlc series run` starts this process with its tools named, as a build's are: the skills this playbook reaches,
`sdlc`, `git`, `dotnet`, `gh`, `az`, the file tools, and each command the solution's `build_tools` names (D-202).
Anything else is refused, and **a refusal is final**. Write every command so its check can read it: one plain
command per shell call, no variable or brace expansion, files through the file tools, bash rather than PowerShell
on Windows.

## `--step plan`: plan the open wave

```
sdlc series wave
```

The binary has already taken the wave; this hands it back, one line per item with what starts it, or what is left of
it when the series stopped partway through (D-230). Do what each line says:

| The item | What to do |
|---|---|
| a successor | run **`intent`** with `--from successor:<slug> --unattended`: it opens the cycle on this branch and suggests its intent, asking nobody; what it inferred stays marked `(inferred)` for the report |
| a parked cycle | `sdlc unpark <id> --onto series/<id>`; its spec and plan are read again by `to-plan`'s loop, below |
| an approved cycle, under `approval: required` | nothing to plan: the binary builds it |
| an approved, parked cycle, under `approval: required` — the line says `then build` | `sdlc unpark <id> --onto series/<id>`; its spec and plan are read again by `to-plan`'s loop, below, because its plan was written before the code it waited on existed. Read unchanged, its approval stands and the binary builds it |
| *the wave's build builds it*, or *nothing left to do* | nothing |

Then run **`to-plan`** with `--unattended` for this branch: every intent this wave opened checked, then its spec and
its plan, and every unparked cycle's spec and plan read again, each with its loop. **A cycle whose loop cannot settle
stops there**, left planned or less with its reason in the hand-back; the others go on. Under `approval: required` it
reads again only the unparked cycles: **one whose spec or plan the loop changed has lost its approval, and stops** —
the gate refuses it until a person reopens and approves it — with that named in the hand-back.

`to-plan` commits after every step; commit whatever is left, `git add .sdlc`, then `git commit -m "Series <id>: wave
<n> planned"`. Hand back one line per item: planned, or stopped and why. **Then stop**: the binary builds the wave.

## `--step stops --stops <file>`: answer what the wave's build left stopped

The file names each cycle the wave's build left unmerged, what happened to it, and what its build handed back. Read
it. **One stop is the series' to answer itself, under `approval: none`** (D-232): a cycle that stopped on a test that
fails only sometimes — its build handed back not finished, naming the test and the failing run's results file,
because its change did not cause it (D-216). For that, and only that, add the fix:

```
sdlc series fix --test <name> --results <file> --holds <id>[,<id>...]
```

`--holds` names every cycle in the file that stopped on that test, the one whose run failed first. It writes the fix
as a successor, first in the wave and on the list, and holds those cycles until it passes; the binary then hands the
wave back to be planned with the fix first, and builds the fix before what it holds. Commit what it wrote: `git add
.sdlc`, then `git commit -m "Series <id>: wave <n> adds a fix"`. **When it refuses** — a second fault in one wave,
the same test failing again after its fix, a test no recorded run shows passing — say so in the hand-back.

**Every other stop is left as it is.** Do not remediate, rebuild or merge anything: the binary takes each cycle you
did not hold out of the series, with its reason and its pushed branch, and the series goes on without it (D-235).
Hand back one line per cycle: held by the fix, or left to be taken out. **Then stop.**

## What you may not do

- **Stage work of your own.** An intent is `intent`'s; a spec, a plan and their loops are `to-plan`'s.
- **Build, verify, remediate, merge or open a pull request.** Those are the binary's and the processes it starts.
- **Plan anything under `approval: required`** beyond reading an unparked cycle again.
- **Approve anything**, or write any file only a person writes.
- **Ask anybody anything.** A cycle that needs a person stops, with why, and the series goes on without it.
- **Add any work but a fix `sdlc series fix` accepts.** A successor a cycle proposes mid-run waits for a later series
  (D-231); a test the series reruns until it passes is the green run D-216 forbids.
