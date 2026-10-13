---
name: init
description: Adopt the framework into a repository -- run the scaffolding, interview the team about what matters in this solution, and propose its first constraints. Use when somebody asks to set up ai-sdlc here.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[--platform github|azure-devops|local]"
allowed-tools: Bash(sdlc *), Bash(git *), Read, Glob, Grep
---

# Adopt the framework

Adopting a process is a decision about how a team works. **A person runs this, and the hook denies you
running `sdlc init` yourself** — the command writes the hook configuration that constrains you, the
pipeline that gates the merge, and the file saying where this code came from.

So your part is the two halves a command cannot do: asking what matters here, and proposing the rules that
follow from the answers.

## 1. Check what you are adopting into

```
git rev-parse --is-inside-work-tree
git ls-files
```

**Not a repository?** Say that `sdlc init` will refuse, and why: without git, a build cannot record the
diff a verification derives from, and the gates key on a branch that cannot exist — so every hook would
permit everything while the artifacts still looked governed. Offer `git init`.

**Already governed?** `.sdlc/` exists, so this will be an update: the framework refreshes what it owns and
touches nothing a person writes. Say so, because "init" on a working solution sounds alarming and is not.

## 2. Ask for the command to be run

**Two things here share a name and do opposite halves of the job.** `/sdlc:init` is this skill — the
interview, and the constraints that follow from it — and it writes nothing. `sdlc init` is the binary that
does the scaffolding, and the hook denies it to you. **Say which one you are**, because somebody who has
just invoked the slash command reasonably believes adoption is now under way, and invoking it again only
loads these instructions a second time. That loop is three identical rounds before anybody notices.

Hand over the line with a leading `!`:

```
! sdlc init [--platform github|azure-devops|local]
```

On Claude Code the `!` prefix runs the rest of the line as the person's own command rather than as a tool
call of yours, so the gate — which fires on your tool calls — never sees it. The reason to prefer it goes
beyond getting past the gate: it runs in this session, so what the command prints lands in the
conversation and you can read the mode, the origin and the file list back without asking for a paste. A
bare line sends the person to another terminal and the output has to be copied in by hand. On a host with
no such prefix, say to run it in a terminal at the repository root and paste what it prints.

**Ask one thing before you hand over the line: where the code will live**, because the answer decides
whether a pipeline is written and there is no good default for somebody who has not said. If the
repository has no remote yet — `git remote -v` prints nothing — `local` is the honest answer: a pipeline
for a service nothing will ever send it to is a file that looks like a gate and is reachable by nothing.

**Do not ask what the solution is built from, and do not ask which conventions new work follows.**
Adoption decides neither. The stack is declared afterwards, in step 4, in a file a person places — there
is no profile to choose and `sdlc init` refuses `--profile` with a sentence saying so. And an existing
codebase keeps its own conventions unless somebody asks otherwise, which is the default the command
already takes.

**There is no option for holding an existing codebase to the framework's conventions, and you raise
the subject only when a person does.** It is two separate choices, each made after adoption, and
`sdlc init` answers the old `--adopt-standard` with a sentence saying so:

| A person wants | It is | What to say it costs, in one line |
|---|---|---|
| new work to follow the framework's patterns rather than the surrounding code | `sdlc draft solution --standard profile`, and a rename | every plan now argues when it matches the old code instead of the profile |
| the `.NET` build settings — `.editorconfig`, the three props files, `global.json` | rows in the declared stack, set to `true` in step 4; a cycle whose plan declares the files brings them | `Directory.Build.props` makes warnings errors, so a build can start failing on files nobody has touched |

**The rest answers a follow-up.** Neither migrates the code already there, which is a cycle like any
other work, and neither changes the origin. A build settings file is created once and never rewritten,
so narrowing a severity or scoping a rule to a directory is an ordinary edit to it, and one the
repository already had is kept.

**Say what local costs in one line, once:** no pull-request check governs a merge, so nothing
mechanical decides what reaches the default branch. **The rest answers a follow-up and is not
the line.** Everything else is unchanged: cycles are
planned, approved, built and verified exactly the same way. Only the guarantee is missing, and `sdlc
doctor` says so every time it runs. When a remote exists, re-running adoption with a platform writes the
pipeline, and the `pr` skill walks through creating the remote.

Give the person the line, `!` and all. When they have run it, read what it printed — it says which mode
ran, what the origin was decided to be, and every file it touched or deliberately left.

**It ends with one next step, and on a first adoption that step is yours to carry out with them.** It
says what a person still has to place before the first cycle and names a single command: `sdlc draft
solution` first, because what a solution is comes before what it is built from. That is question 1
below, so the command and this skill agree about what comes next. **Do not run `sdlc draft techstack`
ahead of it** — the report names it in the sentence and deliberately not as the step.

**The origin is the part worth stopping on**, because it is the one thing here with lasting consequences
and it never changes again:

| origin | What it means |
|---|---|
| `scaffolded` | this repository held no code, and never will have held any before the framework |
| `adopted` | the code existed first, and that is now a permanent fact about it |

**`standard` is the key beside it, and it is the one that decides anything.** The origin only says where
the code came from; the standard says which conventions new work follows, and unlike the origin a team is
entitled to change it.

| standard | What a plan does by default | Where it comes from |
|---|---|---|
| `profile` | takes the profile's patterns, and says why when it departs | every scaffolded solution, and an adopted one whose team moved it with `sdlc draft solution --standard profile` |
| `existing` | matches the surrounding code, and says why when it departs toward the profile | every adopted solution, until its team moves it |

Nothing is in violation on its first day either way. Constraints govern a change, never code that came
before adoption, so there is no baseline to record and nothing to forgive — `standard: profile` included,
which declares what new work follows and says nothing at all about the code already there.

## 3. Interview, then propose constraints

This is the half that needs you. Three or four questions, no more:

1. **What is this solution for, in a sentence a newcomer could read?**
2. **What has gone wrong here before that you would not want to happen again?**
3. **What is there a right way to do in this codebase that a newcomer would get wrong?**
4. **Is there anything that must never change — a contract, a data shape, a behaviour somebody depends on?**

Question two is the one that produces real constraints. Question three usually produces an analyzer rule
rather than a constraint, which is the right answer and worth saying out loud.

### On a repository that held no code, ask these instead

**Three of those four questions are about history, and a greenfield repository has none.** The honest
answers are *nothing*, *nothing* and *nothing* — so a brand-new solution gets zero solution constraints
by construction, and nothing anywhere records what it is meant to look like when it is finished. A
status page adopted this way, built a cycle later, came out as a numeral in a bare `<p>` with every gate
green: nobody had asked, so nobody had been told.

Keep question one. Replace two, three and four with the single question that has an answer before there
is a past:

1. **What is this solution for, in a sentence a newcomer could read?**
2. **What should every screen in this solution look and behave like?** A component library, a design
   somebody is working to, an existing product it has to sit beside — or plain and unstyled, which is a
   legitimate answer and a different one from nobody having asked.

**Two questions, and resist the third.** What *done to a good standard* means, and what a reviewer would
send back that no test caught, are already put to every change by the global tier: `CON_16` requires a
spec to cover every outcome its intent asks for and all of each one, `CON_17` requires it to say what
somebody is told when the action fails, and `CON_05` keeps out scope nobody asked for. Asking them at
adoption spends a person's attention on rules already in force, and invites an answer about the change
they have in mind rather than about the solution — which is the intent's question, one stage later,
where there is a change to ask it about.

### Never ask

- **Anything that belongs to a single change.** What one piece of work must achieve is the intent's
  question and the thresholds that prove it are the spec's. An adoption interview is only about what
  holds for every cycle.
- **Anything a rule in force already asks.** Run `sdlc constraints` before proposing one rather than
  only at step 5, because a question the global or profile tier already puts to every spec buys nothing
  here, and a `LOC_` that restates a tier above it is decoration that costs attention at every
  interrogation.
- **Anything whose answer you would be inferring.** The repository name, the directory path and the
  `AGENTS.md` title are not evidence of what the solution is — `sdlc init` writes that title from the
  folder name, so quoting it back is the question repeated, not an answer. Ask question one open-ended,
  in plain text, and offer no candidate answers to choose between: a subject you guessed is one a person
  has to correct instead of supply.

### Where each answer goes, which is not all the same place

**Most of these are not constraints, and offering them as one wastes the person's time at the admission
tests.** Sort before you propose:

| An answer about | Goes in | Because |
|---|---|---|
| what the solution is and who for | the paragraph under the three keys, which `sdlc draft solution` writes | it is there for people: no stage reads that prose, and it binds nothing |
| which component library, which packages, which project may reference what | an architecture test | test two below rejects it — an analyzer *can* decide it, so a constraint is the wrong tier |
| the names a visual treatment is expressed in | a theme, which `sdlc propose theme` drafts | roles survive a rebrand where values do not, and a stage can look a role up |
| whether a judgement was made and recorded — that a visual treatment was considered | a `LOC_` constraint | it needs reading comprehension, which is exactly what test two admits |

**Where an answer reaches none of those four, record it nowhere, and say so.** There is no fifth home,
and inventing one — prose a stage is told to read, a rule without the four parts — would put a rule
somewhere no revision, no id and no admission test reaches. The three tiers, the decision record and the
declared stack are the whole of where a rule may live; `sdlc constraints` renders the tiers and
`sdlc decisions` the record.

**`DEC_` is not available here, and that is structural.** `sdlc propose decisions` reads only approved
cycles, and a repository being adopted has none — so at adoption the live homes are a `LOC_`, an
architecture test and a theme. A choice worth recording as a decision is recorded a cycle later, from
the cycle that settled it.

`solution.md` is protected, so you cannot write it -- and you no longer have to hand over a block to
paste either. **Run `sdlc draft solution`.** It writes the whole file beside the real one, keeping the three
keys exactly as recorded, and prints the single rename that places it. Show what it prints verbatim.

**Do not let the file stay a placeholder, and do not oversell what it does.** No stage reads the
paragraph — the three keys above it are what record the origin, decide which conventions new work
follows and say whether a pull-request check governs a merge. It is still worth
writing, because it is the one place a person joining this repository learns what it is for, and
`Replace this with a paragraph a newcomer could read` tells them nothing. That is not hypothetical: a
block printed for pasting went unpasted through a whole cycle, which is why the drafter writes a file
now.

**Ask the person, do not read the codebase.** Reading an existing repository to decide what to forbid
would make adoption a judgement on code that predates the framework, and the first cycle would spend
itself on findings about work nobody is defending.

Then, for each answer that survives, propose a constraint:

```
sdlc propose constraints
```

A repository being adopted has no cycles, so this is the empty-handed mode: it prints the next free
`LOC_` id with its four parts empty, and the three admission tests. Fill the parts in from the answer
and show it. **All three tests must pass**, and test two rejects most candidates:

1. its `Forbids` clause names something that becomes illegal
2. **no analyzer, schema or hook could decide it**
3. it is incompatible with at least one plausible implementation

So "every public method has a doc comment" is an analyzer, not a constraint. "A new endpoint states which
caller depends on it" needs reading comprehension and is a constraint.

**A person places it.** Every agent write to `.sdlc/constraints.md` is denied, because a cycle judged by
the constraints it is changing is the same deadlock as the framework governing itself. Print, and let them
paste.

**Propose none if none survive.** An empty solution-constraint set is the correct starting point for most
solutions, and three constraints nobody believes in cost attention at every interrogation for the life of
the repository.

## 4. Declare the stack, and fill in what the scaffolding left blank

Two files adoption leaves for a person, and one it writes:

| File | What to offer |
|---|---|
| `.sdlc/solution.md` | written with a placeholder paragraph. Protected, so run `sdlc draft solution` with the paragraph from question 1 and show the rename |
| `.sdlc/techstack.md` | **not written at all.** Run `sdlc draft techstack`, cut the draft down to what is true here, and show the rename. No cycle opens until it is placed |
| `AGENTS.md` | written if absent, printed for merging if present |

**The declared stack is the step that cannot be skipped.** `sdlc new` refuses until the file exists.

```
sdlc draft techstack
```

The draft is the framework's default, whole: every row it would pin, under `## Rules` every stack
rule it ships, each marked `true`, and under `## Build settings` the five `.NET` build settings files.
**What ends up in the file is exactly what binds** — a row pins a choice, no row leaves that choice to
each plan, a rule applies only where its row says `true`, and a build settings file is this solution's
only where its row does. So the edit is subtraction:

| The solution | What to do to the draft |
|---|---|
| is built on the default stack | nothing. Rename it as it is |
| is .NET with different choices | change or delete the rows that are not true of it |
| is built on something else | delete the rows that do not apply, write in what it uses, and set to `false` any rule that names a framework it does not have |
| should pin nothing yet | `sdlc draft techstack --empty`, and rename that. Nothing is pinned, no stack rule applies, and every plan chooses freely |

**On a repository that already holds code, edit the draft from what you can read before you show the
rename.** This is the one place reading the codebase is right — the file declares what the solution
*is* rather than what it *should be* — and it matters here because the draft starts as a stack this
repository may not use at all. A default renamed unread onto a codebase that is not .NET declares
something false, and every plan after it is read against that.

**An empty stack is a legitimate answer, and say what it costs in one line**: nothing keeps the second
cycle from choosing a different framework from the first, because there is no declared default for it to
depart from. That is the trade for deciding the stack inside the work rather than before it, and rows
can be added later the same way.

**You cannot place it.** It is protected, like the other five, and nothing but a person's rename ever
writes it. Show what the command printed and let them rename. The reason is worth saying once if they
ask: it is the second rule source a plan is read against, and `CON_15` judges every plan against it, so
an agent able to edit it could satisfy that rule by adding the row it wanted instead of arguing for it —
or drop a stack rule by switching its row off.

**The `.NET` build settings are not written here, and the draft lists them.** `.editorconfig`, the
three props files and `global.json` are rows under `## Build settings`, and how they start depends on
the origin:

| origin | The rows start | Because |
|---|---|---|
| `scaffolded` | `true` | nothing predates them, and they arrive with the solution skeleton from `sdlc scaffold` in the first cycle |
| `adopted` | `false` | `Directory.Build.props` makes warnings errors, so on existing code a build can fail on files nobody has touched |

**On an adopted repository, leave them `false` unless the person asks**, and do not switch one on from
what you read in the codebase — that it is .NET does not mean its team wants warnings as errors. When
they do ask, a row set to `true` writes nothing: a later cycle whose plan declares the file brings it.
A solution on another stack deletes the rows and never gets the files.

## 5. Report

```
sdlc constraints
sdlc doctor
```

Show the first verbatim — that is the set now in force. From the second, show **the closing block
verbatim**: what a person still has to place before the first cycle, and the one command that is next.
It is read from disk, so it is right about a rename you did not see happen, and a list of your own
would be a second answer to the same question. Then **at most two lines of your own**: which mode ran,
and what the origin is.

**Nothing about what any of it means.** Adoption is the stage with a standing temptation to explain the
framework it has just installed, and `.sdlc/README.md` is the whole of that explanation — written, in the
repository, and better than a recap. A person who wants the reasoning opens it.

Then stop.
