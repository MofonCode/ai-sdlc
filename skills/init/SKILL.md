---
name: init
description: Adopt the framework into a repository -- run the scaffolding, interview the team about what matters in this solution, and propose its first constraints. Use when somebody asks to set up ai-sdlc here.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[--platform github|azure-devops|local] [--adopt-standard]"
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
! sdlc init [--profile <name>] [--platform github|azure-devops|local] [--adopt-standard]
```

On Claude Code the `!` prefix runs the rest of the line as the person's own command rather than as a tool
call of yours, so the gate — which fires on your tool calls — never sees it. The reason to prefer it goes
beyond getting past the gate: it runs in this session, so what the command prints lands in the
conversation and you can read the mode, the origin and the file list back without asking for a paste. A
bare line sends the person to another terminal and the output has to be copied in by hand. On a host with
no such prefix, say to run it in a terminal at the repository root and paste what it prints.

**Ask where the code will live before you hand over the line**, because the answer decides whether a
pipeline is written and there is no good default for somebody who has not said. If the repository has no
remote yet — `git remote -v` prints nothing — `local` is the honest answer: a pipeline for a service
nothing will ever send it to is a file that looks like a gate and is reachable by nothing.

**In a repository that already holds code, ask one more question before you hand the line over:
should new work follow the profile's conventions, or the code already here?** It is the only question in
this skill whose answer writes files, and the answer is not guessable — a team six months into a rewrite
answers differently from a team maintaining something stable.

| Answer | Flag | What it does |
|---|---|---|
| the code already here | none | the default. Nothing extra is written |
| the profile's conventions | `--adopt-standard` | writes `.editorconfig`, both props files and `global.json`, and records `standard: profile` |

**Say what the flag costs before they choose it, in one line**, because the cost lands on the next build
rather than on this command: `Directory.Build.props` makes warnings errors, so a build can start failing
on files nobody has touched. **The rest of this paragraph is the answer if they ask, not part of the
line.** That is the opt-in working, not a fault — and the remedy is to narrow a severity or
scope a rule to a directory, because each of those files is created once and never rewritten, so an
amendment survives every later run. Nothing is overwritten either: an `.editorconfig` the repository
already had is kept.

**Two things the flag does not do**, one line each because somebody will assume otherwise.
It does not migrate the code already there — that is a cycle, with an intent and an approval, like any
other work. And it does not change the origin, which stays `adopted` for the life of the repository.

**On a repository that is already governed, it writes the files and prints the key.** `solution.md` is
protected, so adoption creates it and never rewrites it. The report prints the four keys for a person to
place, and says that until they do, the recorded standard is what every plan is held to. Show that block
verbatim and let them paste it — the same rule as every other protected file.

**Say what local costs in one line, once:** no pull-request check governs a merge, so the pre-push hook
is the only mechanical layer and `--no-verify` walks past it. **The rest answers a follow-up and is not
the line.** Everything else is unchanged: cycles are
planned, approved, built and verified exactly the same way. Only the guarantee is missing, and `sdlc
doctor` says so every time it runs. When a remote exists, re-running adoption with a platform writes the
pipeline, and the `land` skill walks through creating the remote.

Give the person the line, `!` and all. When they have run it, read what it printed — it says which mode
ran, what the origin was decided to be, and every file it touched or deliberately left.

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
| `profile` | takes the profile's patterns, and says why when it departs | every scaffolded solution, and an adopted one that asked |
| `existing` | matches the surrounding code, and says why when it departs toward the profile | an adopted solution, unless it asked |

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
dice-roller adopted this way, built a cycle later, came out as a numeral in a bare `<p>` with every gate
green: nobody had asked, so nobody had been told.

Keep question one. Replace two, three and four with questions that have answers before there is a past:

1. **What is this solution for, in a sentence a newcomer could read?**
2. **What should every screen in this solution look and behave like?** A component library, a design
   somebody is working to, an existing product it has to sit beside — or plain and unstyled, which is a
   legitimate answer and a different one from nobody having asked.
3. **What does "done to a good standard" mean here?** The thing a reviewer would send back that no test
   would have caught.
4. **What will somebody judge this on that no test can check?** This is the one that earns its place
   later, because `CON_12` keeps it out of a spec and the person is then the only reader it has.

### Where each answer goes, which is not all the same place

**Most of these are not constraints, and offering them as one wastes the person's time at the admission
tests.** Sort before you propose:

| An answer about | Goes in | Because |
|---|---|---|
| what the solution is and the standard expected of it | `solution.md`, the paragraph under the four keys | every plan reads it, and it binds nothing by itself |
| which component library, which packages, which project may reference what | an architecture test | test two below rejects it — an analyzer *can* decide it, so a constraint is the wrong tier |
| whether a judgement was made and recorded — that a visual treatment was considered, that an unhappy path was specified | a `LOC_` constraint | it needs reading comprehension, which is exactly what test two admits |

`solution.md` is protected, so print the paragraph and let them place it. **Do not let it stay a
placeholder.** It is the only file in the solution that says what good looks like, and a plan that reads
`Replace this with a paragraph a newcomer could read` has been told nothing.

**Ask the person, do not read the codebase.** Reading an existing repository to decide what to forbid
would make adoption a judgement on code that predates the framework, and the first cycle would spend
itself on findings about work nobody is defending.

Then, for each answer that survives, propose a constraint:

```
sdlc draft constraint
```

It prints the next free `LOC_` id with its four parts empty, and the three admission tests. Fill the parts
in from the answer and show it. **All three tests must pass**, and test two rejects most candidates:

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

## 4. Fill in what the scaffolding left blank

Three files are written with placeholders, and they are placeholders on purpose:

| File | What to offer |
|---|---|
| `.sdlc/solution.md` | the paragraph from question 1. It is protected — print, do not write |
| `.sdlc/techstack.md` | on an adopted solution, the table is empty. Offer to fill it from what you can read |
| the stack files | written only where the standard is the profile's. Each is a person's to amend, and a cycle that changes one is a cycle like any other |
| `AGENTS.md` | written if absent, printed for merging if present |

`techstack.md` is the one place reading the codebase is right — it declares what the solution *is* rather
than what it *should be*, so being wrong there is a correctable fact rather than a rule applied to
innocent code.

## 5. Report

```
sdlc constraints
```

Show it verbatim — that is the set now in force. Then **at most five lines of your own**, one line each
and only these: which mode ran, what the origin is, which files are placeholders, and what a person has
to place before the first cycle.

**Nothing about what any of it means.** Adoption is the stage with a standing temptation to explain the
framework it has just installed, and `.sdlc/README.md` is the whole of that explanation — written, in the
repository, and better than a recap. A person who wants the reasoning opens it.

Then stop.
