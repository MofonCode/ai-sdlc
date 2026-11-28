---
name: to-plan
description: Take an intent to a plan -- one cycle, or every one this branch carries -- checking the intent, writing each cycle's spec and then its plan, running the interrogate and refine loop on each until it is clean, picking up wherever a cycle stopped, carrying a person's correction or chosen suggestions through steer first when they give one, and hand back once with every panel, one question about suggestions, and the approvals drafted. A playbook: spec and plan write the documents, steer writes a correction into them, interrogate judges, refine edits. With --unattended, in a series' own process, it asks nobody: a cycle it cannot settle stops, with its reason. Use when a person is happy with their intent and says take it to plan, or says what should change in a cycle being planned.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[<cycle id>] [<finding id>...] [\"what to change\"] | --unattended"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Read, Write, Edit, Glob, Grep
---

# Take the intent to plan

**A playbook** (D-186): it orchestrates the planning half and writes nothing itself. A person has written their
intent, read it, and is happy. This checks it, writes the spec, then the plan, runs **the loop** on each — the
one place the interrogate and refine loop lives (D-188) — and hands back **once**, at the end (D-142, D-158). A
person never needs to know that a document is written, checked and refined on the way.

| Step | Skill |
|---|---|
| write the spec | `spec` |
| write the plan | `plan` |
| judge a document | `interrogate` |
| apply its findings | `refine` |
| write a person's correction, or a suggestion they chose, into the documents | `steer` |

**It stops at the plan.** Approving is a person's, one cycle at a time. It drafts the approval, because drafting
is not deciding; it never places one.

**With `--unattended`** — run by the `series` playbook in its own process, under `approval: none` — it takes every
cycle this branch carries the same way and **asks nobody**: §6 says what changes.

## 1. Find the work

**Given a cycle id, that cycle is the work.** Given none:

```
sdlc cycles --here --for to-plan
```

It lists the cycles this branch carries that are ready to be taken to plan — an intent, a plan not yet clean,
and no standing approval — wherever each one stopped. **Exit 1 is not a failure here**: it means not exactly
one, and when several are listed the person asked for all of them, so take every one. A cycle not listed is not
ready — already planned, approved, or abandoned — and `sdlc cycles --here` lists everything carried, so name each
one you leave out, with `sdlc status <id>` for why.

**Say what you will take, in one line per cycle, and go** — one cycle at a time, its loops clean or stopped
before the next starts, never several handed to subagents at once (D-228).

**Given a correction or finding ids** — the person said what should change, at a hand-back or on their own —
run **`steer`** with their words and the ids first, then pick the cycle up as §2 says: the documents it edited
are no longer clean, so the loop starts at the highest of them and works down. Steering is a sentence, never a
step of its own (D-182, D-188).

## Keep a task list, if your host has one

If your host gives you a task list, open one now that the work is known. If it does not, carry on without one.

**One line per cycle.** By id and headline, and nothing else: no line per stage or per round.
**A tick means planned, and nothing else does.** A cycle that stopped stays unticked, with why written on the line. The skills
this playbook runs keep no list of their own here. **The list is a view, never the record.**

## 2. Pick up where it stopped, one cycle at a time, oldest first

```
sdlc status <id>
```

**Start from the first document that is not clean** — a cycle stopped mid-round is taken up there, not started
again:

| Where it stopped | What to do |
|---|---|
| the intent still marks a line `(inferred)` — its panel names it under `Shape` | **a person starting this playbook said *go*** (D-211): run **`steer`** with *go — every line still marked `(inferred)` is adopted as written*, then **the loop** on the intent. Keep each line it adopted, word for word, for the hand-back. A person who wants to answer them first does that with the `intent` skill before starting this. **Unattended, go on** without steering: the marks are left for the run's report |
| the intent is not clean — no round yet, a blocker open, or a round that judged an older version | **the loop** on the intent |
| no spec | **`spec`** writes it, then **the loop** on it |
| a spec that is not clean | **the loop** on the spec |
| no plan | **`plan`** writes it, then **the loop** on it, then the parking check |
| a plan that is not clean | **the loop** on the plan, then the parking check |

Take the cycles **one at a time**, never two stages at once: each stage's rounds are judged against the
document above it, and `sdlc round` refuses one out of order. **A cycle that cannot go on stops alone** — a loop
stuck, a reader reporting a conflict, a decision the person must keep or depart from, a question only they can
answer: stop **that cycle** where it is, note what it needs, and go on to the next. Never answer for the person.

## 3. The loop

For one document — the intent, the spec or the plan:

1. **`interrogate`** it: a round of fresh readers that never saw it written.
2. Read the panel: `sdlc status <id> --stage <document> --brief`.
3. **Clean** — no blocker open, **and no `Shape` problem on the panel** — and the document is done. Otherwise
   **`refine`** applies the round's findings and every `Shape` line to it, and back to 1. A shape problem is what the
   pull-request check refuses, so a plan that leaves this loop with one is a cycle built only to be refused (D-233).

**Commit after every step** (D-233): each document written, each round, each refine — `git add .sdlc/cycles/<id>`,
then `git commit -m "<id>: <what the step did>"`. A run that stops loses the step it was on, never the ones before.

**Stop on convergence, on conflict, or on a halt — never on a count.** A clean round is convergence. Two
constraints whose remedies exclude each other is a conflict: report it as one rather than picking a side. A halt
is the panel's `Loop` line saying one rule has come back three times. **There is no round limit, and the `Loop`
line is not one**: eleven rounds has been worth it before now. What is counted is a rule answered and then
raised again — going backwards, not going slowly. **"Nothing further can be settled" is not this session's call**:
the thing being judged is not the judge.

| The `Loop` line says | What to do |
|---|---|
| nothing | keep going. There is no cap to run out of |
| a rule *has come back* | **tell `refine` to look for an exit, not another edit of the same words.** The line names what the rule is caught against: can the remainder be named in `Out of scope`, or does the question belong under `## Open`? |
| *stuck on* a rule | **stop this cycle.** Say which rule, what it is against, and that the exits are a correction in words, which this playbook carries through `steer`, a scope change, a rule change, or abandoning the cycle |

**A halt is not a verdict on the document.** The open blockers still decide whether anything advances. Do not
call a halt a conflict unless a reader declared one.

**Per document, where a finding usually points**: on the intent, an assumption goes to `Fixed decisions` marked
unconfirmed and a question nobody can settle goes to `## Open`; on the spec, which has no decisions region, an
assumption is almost always a sign the intent is the thing to fix; on the plan, a finding about proof usually
wants a probe or a test that reaches the criterion, not more words.

## 4. After the plan's loop: what it needs from plans not yet built

```
sdlc park <id>
```

It compares this plan's files with every plan not yet built — the cycles beside it in the sandbox, and those
admitted and waiting (D-145). **A file only another plan creates parks this cycle**: it writes `parked.md`, keeps
the spec and plan, and the cycle waits until that one merges; it may still be approved, and builds once `sdlc
unpark <id>` takes it back and reads its spec and plan again. **A file two plans both name is not a dependency**:
say which, since their pull requests may conflict, and carry on.

## 5. Hand back once

**Name every finding, deviation, criterion and rule by what it says, with its id trailing at most.** The person
has not read the rounds: *"the die has no label (S_04)"*, never *"S_04"* alone.

**One line per cycle**: its id, its headline, and where it got to — `planned`, `parked` with what it waits on, or
stopped and why, in the person's terms, never as a count. Then `sdlc backlog` verbatim when anything was parked,
and, for each planned cycle, `sdlc status <id> --stage plan --brief` verbatim, one after another.

**Then the readings it adopted**, when a cycle's intent carried `(inferred)` marks: under *Adopted as written,
because you took it to plan*, each line word for word. A person who reads one as wrong says so, and `steer` carries
the correction down like any other.

### The suggestions, once, for every cycle together

The rounds raise more than a person can weigh one line at a time, so you read them all and the person reads the
themes (D-114). Leaving them is an answer, and the commonest one — skip all of this when nothing is open.

1. **List them.** `sdlc suggestions <id>` for each planned cycle — what it prints is the list, and its count is
   the count the file must hold.
2. **Write `.sdlc/reports/<date>-to-plan-suggestions.md`, grouped by cycle and then by theme:**

   ```markdown
   # Open suggestions — 0007

   Answer in chat, in your own words: name a theme or a finding, or say what you want.
   Anything you don't mention stays open in `sdlc suggestions` for a later cycle.

   ## A. Errors say too much and record too little
   The page shows the provider's raw error, and a failed fetch is logged nowhere. This sharpens
   outcome 1, "a reader sees today's rate": it says what failing looks like. Taking it in now is one
   message and one log line; after the build, tests will already assert the current text.
   Suggest: take in beneath outcome 1 — the feed this cycle adds is the thing that fails.
   - security.S_02 — the provider's raw error is shown to the user
   - operability.S_01 — a failed fetch is recorded nowhere
   ```

   A theme is a heading, a short summary of what its findings mean **for this change**, whether it **sharpens**
   an outcome the intent already wants — name which — or **adds or widens** one, what taking it in costs now
   against later, and a suggested answer with its reason. **Only a sharpening may be taken in** (D-128): one that
   adds an outcome, or carries one to more states, devices, failure paths or audiences, is suggested as a
   successor. **The themes only group**: every open suggestion appears once — none merged, none left out — and
   two that contradict share a theme, which says so.
3. **Ask one question, in free text, never from a picker**: name the file, give each theme in one line, and ask
   what to take in, decline, or leave. Say in a line that one taken in now costs another round of the documents
   below it.
4. **Map the answer to finding ids, and read it back before acting:**

   > **Take in** A, beneath outcome 1 — `security.S_02`, `operability.S_01` — as *"errors say what failed,
   > never the provider's message, and every failure is logged"*
   > **Decline** B — `accessibility.S_01` — *"a personal page, only I use it"*
   > **Successor** C — `operability.S_03` — widens outcome 1, so it waits as `successor:all-currencies`
   > **Leave** the rest. Go?

   A theme named whole is every id beneath it. A decline needs a reason — ask once if the answer gave none. **Words
   with no finding behind them are not taken in here**: a change to *how* is a correction, carried the same way, and
   something no reader raised is a cycle of its own (D-97).
5. **Once they confirm**, run **`steer`** with the finding ids, one run per cycle, carrying the person's words,
   then pick each cycle up again as §2 says and hand back once more when its loops are clean. **Never take one in
   on their behalf.**

### Then draft the approvals

For each planned cycle, `sdlc draft approval <id>` — drafting is not placing, so a person is handed the file
rather than told to make it. **If the drafter refuses**, show what it printed: it names what is still open. Then
one paragraph, filled in:

> The plans are clean. I've drafted each approval — `<path>` for `<id>`, one line each — read each, and
> place the ones you agree with by the rename it printed. If anything should be done differently, tell me
> and I'll carry it through the documents first. I can't place an approval myself — every agent write to
> one is denied — and that is what makes it mean something. Once they are placed, say *build it* (or
> `/sdlc:build`): each implements its plan, verifies it, fixes what it can and opens the pull request,
> in a process of its own, then reports every deviation, every choice it made that the plan left open,
> and every concern. Merging a pull request is where you accept it.

When the solution declares `approval: none` (D-137), draft nothing: say the plans are clean, and that *build it*
carries each to a pull request whose report and merge are where a person accepts it. To hand them to the factory
instead, `/sdlc:pr --plan` admits them to the integration branch by one pull request.

**Then stop.** Name no new session and no admission (D-178, D-182): the build takes each plan out of this sandbox
itself and runs in a process of its own.

## 6. Unattended: `--unattended`

Run by the `series` playbook, in its own process, for the cycles a wave opened or unparked. Everything above holds,
except that **nobody is there to answer**:

- **A cycle that cannot go on stops, and the run goes on.** A loop stuck on a rule, a reader reporting a conflict, a
  decision to keep or depart from, a question only a person can answer: leave the cycle where it is — planned as far
  as it got — and write its reason in the hand-back. Never answer for the person, and never pick a side of a
  conflict to keep the run moving. **A shape problem only a person can answer** — a departure from the declared stack
  or a default that no recorded decision covers, when only a person writes `decisions.md` — stops the cycle too, with
  the decision it needs written out in the hand-back, so placing it is one step.
- **An intent's `(inferred)` marks are left**, as the series' `intent` wrote them: the run's report names each one
  as confirmed by nobody.
- **No suggestions question.** Every open suggestion stays open, in `sdlc suggestions`, for a person to answer
  later; take none in and decline none.
- **No approvals drafted**: under `approval: none` there are none to draft.
- **Hand back one line per cycle**, its id, its headline, and `planned`, `parked` with what it waits on, or stopped
  and why — and nothing else. The playbook that ran this goes on.

## What you may not do

- **Write a document yourself.** `spec` and `plan` write them; `refine` edits them; `interrogate` judges them.
- **Place an approval**, or write any file only a person writes. Drafting one is not placing it.
- **Start a build.** The plan is the last artifact before a person approves.
- **Take a cycle another branch carries**, or one `sdlc cycles --here` did not list as ready.
- **Answer a question meant for the person** to keep the run moving. A stopped cycle is a result. An intent's
  `(inferred)` marks are not such a question once the person started this playbook: that was their *go* (D-211).
