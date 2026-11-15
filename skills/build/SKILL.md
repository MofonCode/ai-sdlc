---
name: build
description: Carry approved cycles to their pull requests -- the one named, or every approved cycle when none is -- and report every deviation, choice and concern. In a person's session it asks the gate and runs sdlc build, which starts this playbook again in a process of its own; there, with --unattended, it runs implement, verify, remediate while it converges, and pr for each cycle. A playbook: it names the skills it runs and does no stage work itself. Use when a person says build it, and as the factory's run.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[<cycle id>] [--unattended]"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Bash(gh *), Bash(az *), Read, Write, Edit, Glob, Grep
---

# Build it: approved cycles, through to their pull requests

**A playbook** (D-186): it orchestrates and does no stage work itself. Saying *build it* carries a cycle all the
way (D-178, D-187) — the plan taken out of its sandbox, the code written and tested, the evidence recorded, a
verification by a process that never saw the build, remediation while it converges, and the pull request
opened — then one report of everything a person should know before they merge.

| Run as | Where | Does |
|---|---|---|
| `/sdlc:build [<id>]` | a person's session | asks the gate, runs `sdlc build [<id>]`, hands back what it prints — *In a person's session* |
| `/sdlc:build [<id>] --unattended` | the process `sdlc build` starts, and the factory | for each cycle: `implement`, `verify`, `remediate` while it converges, `pr`, then the report — *In its own process* |

**Given an id, that one cycle; given none, every approved cycle ready to build.**

---

# In a person's session

## Which cycle

**Given no cycle id, ask the binary what is ready** (D-142):

```
sdlc cycles --here --for build
```

One cycle printed is the cycle. Several listed means the person asked for all of them: say which, in one line
each, and go on with no id. None ready says why: show it and stop.

## The gate decides, not you

```
sdlc gate <cycle id>
```

For each cycle you are about to build. If it refuses, stop and show what it said — the refusal names what is
missing, and the next step belongs to the person. Do not work around it and do not ask them to approve so you
can continue.

It refuses in twelve ways, each with its own next step: no cycle, abandoned, a missing document, nobody
approved, an approval ledger it cannot read, a refusal standing, a findings round it cannot read, a document
never interrogated, a document changed since it was approved, a blocking finding still open, a round that judged
a document which has moved since, or a findings round the approval anchored that has been rewritten. In a
solution that declares `approval: none` the gate asks for no approval, and every other refusal stands. **A
blocking finding cannot be waived.**

## Keep a task list, if your host has one

If your host gives you a task list, open one once the gate has said the build may run, with these items, and
mark each done as it finishes. If it does not, carry on without one.

1. run the build
2. hand back its report

**This stage only.** What the build's own process does runs where your list cannot see it, and its outcome is
what `sdlc report` and `sdlc status <id>` print, not a tick.
**The list is a view, never the record.** What is done is what is on disk and what the binary reports.

## Run the build

```
sdlc build [<id>]
```

**One command, and the binary does the rest** (D-184, D-187). From the person's checkout it commits the
approval they placed in the sandbox if they have not — their act, recorded as theirs — admits the plan out of
the sandbox when it is only there, prepares the build's own worktree on `cycle/<id>/build-unattended`, and starts
this playbook again **in a process of its own** in that worktree, with `--unattended`: its tools named, and
**never with permission checks switched off**. Then it prints the report and the pull request's link — or the
escalation and what is open.

**Why a process of its own.** The build reads a great deal of code, and in a session that carried the planning
conversation every turn after it would pay for that again; and a builder that did not sit through the planning
works from what was written down — the intent, the decisions, the constraints and the plan — which is the
separation every reader already has.

- **It asks nothing.** Saying *build it* was the decision. With no remote, it stops at a verified local branch
  and says so.
- **It takes as long as it takes.** Run it where your host lets a long command finish, and tell the person in one
  line that the build has started and that you will report when it ends.
- **Never start the build yourself**, in this session or by launching a process: if `sdlc build` refuses, show
  what it said.

## Hand back the report

Show what `sdlc build` printed verbatim — the report, and the pull request's link — then:

```
sdlc status <id> --brief
```

**The report is the hand-back** (D-181): every deviation the build recorded, every choice it made that nothing
approved settled, and every concern its verification raised without blocking — each by what it says. It leads
the pull request too.

**Close with one plain offer**: *review the pull request — merging accepts it, report and all; to change anything
in it, say what instead* (`/sdlc:remediate`). Once it merges, `/sdlc:mine` takes what the cycle settled into the
solution's record, as the report's last line says. **If the build escalated**, its pull request is a draft and
the report says what stopped it: *say what to do, then fix it* (`/sdlc:remediate`). Name no new session.

---

# In its own process: `--unattended`

Run by `sdlc build` for a person, or by the factory in a run a person started (`CONCEPT.md` §10.5, D-84, D-143).
**Ask nothing**: starting the run was the decision. What the build could not settle from what was written down
goes into `deviations.md`, where the report finds it.

**Everything here rests on the remediation bound.** A cycle that cannot be made to pass stops rather than
reaching a ready pull request, and that stopping is the only reason this is safe to run without watching. The
bound is on progress, not passes: remediation goes on while each pass reduces what is open, takes one different
approach when it stops converging, and escalates when that stalls too or a third deviation bends the same
approved text (D-66, D-179). **There is no ceiling behind it**, which is why the factory's pipeline job has a
timeout.

**It builds; it never merges.** A person reviews and merges each pull request (D-84, D-86).

## 1. Find the work

```
sdlc cycles --queued [<id>]
sdlc cycles --claims
```

**Fetch first only when `git remote` names one** (`git fetch origin`); with no remote the queue reads this
machine's branches. **Given a cycle id, pass it**: the queue is then that cycle alone — admitted on the
integration branch or, for a person's build, only on this machine — and a cycle that is not queued is refused
with why. Report it and stop; never build the rest instead.

Unnamed, the queue is every cycle approved, with no verify, not abandoned, and no `cycle/<id>/...` branch on the
remote, and every cycle held back, with why. **Build exactly the queued ones, in the order listed**: the cycle
the most successors wait on comes first. `--claims` marks the plans that stand up a backing service, which caps
concurrency, and the files two plans share, which meet at merge, where a person is (D-86).

**Do not work either list out yourself.** The binary reads the same refs and plans the same way every time.

## Keep a task list, if your host has one

**One line per cycle**, by id, and nothing else. **A tick means `passed`, and nothing else does**: a cycle stays
in progress through verification and remediation, and one that ends `blocked`, `escalated` or did not finish
stays unticked with the outcome written on the line. The skills this playbook runs keep no list of their own
here. **The list is a view, never the record**: the outcomes are in each cycle's verify document, on its branch.

## 2. One worktree and one branch per cycle

**Started by `sdlc build` for one cycle, this is already done** (D-184): the binary put you in the build's
worktree on `cycle/<id>/build-unattended`. Stay where you are, and go to step 4.

Otherwise every queued cycle builds **independently**, in its own git worktree, on its own branch:

```
git worktree add --detach ../wt-<id>
sdlc branch <id> build-unattended
```

Run the one command `sdlc branch` prints, inside that worktree. **Never build on the integration branch itself,
and never build two cycles in one worktree**: a verify could pass on another cycle's code. No cycle waits for
another and none is rebased onto another. **EF Core migrations are the one thing to watch**: two cycles that each
add one snapshot the same parent model, and the second to merge is regenerated, never merged (`resolve-conflict`
says how).

## 3. Cap concurrency by what a plan needs

| | Concurrency |
|---|---|
| service-free plans | run wide |
| plans standing up SQL Server, Redis, a Service Bus emulator | a low cap, two or three |

Export, into each cycle's worktree before its build runs, `SDLC_RUN_MODE=test` and `SDLC_RUN_ID=<eight hex
characters, different for every cycle>`. **Per cycle, not per run**: the collision is between two app hosts
running at once. Whether anything reads them is the solution's AppHost's business (`CONCEPT.md` §10.2).

## 4. For each cycle: implement, verify, remediate, pull request

In the cycle's worktree, each skill as it is written:

1. **`implement`**, handed the approved plan. It writes the code and tests, records deviations and choices, and
   records the evidence. If the build or tests are red after it, the cycle did not finish.
2. **`verify`**: the adjudicator in a fresh process that never saw the build, from the evidence alone.
3. **`remediate`**, when the outcome is `blocked`: it hands `implement` the findings and verifies again, while
   each pass converges, within D-179's limits — ending `pass` or `escalated`.
4. **`pr`**, once the cycle has an outcome (below).

| Outcome | What it is |
|---|---|
| `passed` | built, verified, its verify outcome `pass` |
| `blocked` | a criterion unmet, and remediation still converging when the run ended |
| `escalated` | remediation stopped: it stalled or regressed again after a deviation, or a third deviation bent the same text. A person picks this up |
| did not finish | the build or the verify could not run to an outcome — the toolchain failed, a cause `implement` handed back, the run was stopped |

**A sibling's outcome changes nothing about another cycle.** Commit everything a cycle wrote on its branch,
verify included, before anything is pushed: the branch is the record of the attempt.

## 5. A pull request for every cycle that reached an outcome

**With a remote, every build ends at a pull request** (D-85, D-187), opened by the `pr` skill, which asks
nothing here — starting the build was the decision:

| Outcome | Pull request |
|---|---|
| `passed` | ready for review, its body led by `sdlc report <id>` |
| `blocked`, `escalated` | `--draft`: the attempted code, the verify and the evidence, marked not ready. Its check goes red, which is accurate |
| did not finish | none. Nothing was verified, so there is nothing to review |

A blocked cycle with no pull request would be invisible in the morning and built again at full cost; its draft is
where a person picks it up. A failure opening one does not stop the others. **With no remote**, push nothing: the
verified branch stays local, and the report says so. **Never resolve a conflict between two cycles here.**

## 6. Report, and stop

**For one cycle**, print `sdlc report <id>` verbatim and its pull request's link — that is what `sdlc build`
hands back to the person.

**For several, one line per cycle**: its id, its outcome, its branch, its pull request's link. For anything not
`passed`, say what stopped it beneath that line, never as a count: each criterion `verify.md` marks Not met by
its number and the first sentence of its reason, and each open blocker by its id and gist. Then:

```
sdlc sitrep
```

Show it verbatim and stop. In the factory the pipeline publishes it, so the morning starts from that page.

## What you may not do

- **Stage work of your own.** Code, tests and evidence are `implement`'s; the verdict is `verify`'s; a pull
  request is `pr`'s.
- **Merge anything**, or push to the integration branch.
- **Write a file only a person writes.** Abandoning a cycle is a person's decision, drafted by `sdlc draft
  abandon`.
- **Build a held-back cycle**, or delete somebody's branch to free one.
