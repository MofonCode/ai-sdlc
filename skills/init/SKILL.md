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

```
sdlc init [--profile <name>] [--platform github|azure-devops|local]
```

**Ask where the code will live before you hand over the line**, because the answer decides whether a
pipeline is written and there is no good default for somebody who has not said. If the repository has no
remote yet — `git remote -v` prints nothing — `local` is the honest answer: a pipeline for a service
nothing will ever send it to is a file that looks like a gate and is reachable by nothing.

**Say what local costs, once, plainly.** No pull-request check governs a merge, so the pre-push hook is
the only mechanical layer and `--no-verify` walks past it. Everything else is unchanged: cycles are
planned, approved, built and verified exactly the same way. Only the guarantee is missing, and `sdlc
doctor` says so every time it runs. When a remote exists, re-running adoption with a platform writes the
pipeline, and the `land` skill walks through creating the remote.

Give the person the line. When they have run it, read what it printed — it says which mode ran, what the
origin was decided to be, and every file it touched or deliberately left.

**The origin is the part worth stopping on**, because it is the one thing here with lasting consequences
and it never changes again:

| origin | What it means |
|---|---|
| `scaffolded` | the profile's patterns are this solution's patterns. A plan departing from them has to say why |
| `adopted` | where the solution has an existing standard, that standard wins |

Nothing is in violation on its first day either way. Constraints govern a change, never code that came
before adoption, so there is no baseline to record and nothing to forgive.

## 3. Interview, then propose constraints

This is the half that needs you. Three or four questions, no more:

1. **What is this solution for, in a sentence a newcomer could read?**
2. **What has gone wrong here before that you would not want to happen again?**
3. **What is there a right way to do in this codebase that a newcomer would get wrong?**
4. **Is there anything that must never change — a contract, a data shape, a behaviour somebody depends on?**

Question two is the one that produces real constraints. Question three usually produces an analyzer rule
rather than a constraint, which is the right answer and worth saying out loud.

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
| `AGENTS.md` | written if absent, printed for merging if present |

`techstack.md` is the one place reading the codebase is right — it declares what the solution *is* rather
than what it *should be*, so being wrong there is a correctable fact rather than a rule applied to
innocent code.

## 5. Report

Say what ran, what the origin is and what it means, what was proposed for the person to place, and what is
still a placeholder. Then:

```
sdlc constraints
```

Show it verbatim — that is the set now in force — and stop.
