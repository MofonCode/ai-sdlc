---
name: init
description: Adopt the framework into a repository by scaffolding .sdlc/ and a lean AGENTS.md. Use when a repository has no .sdlc/ directory and someone wants to start running cycles there.
argument-hint: "[stack, optional — defaults to dotnet]"
allowed-tools: Bash(powershell.exe *) Bash(git *)
disable-model-invocation: true
---

# Adopt the framework here

Scaffold the framework into the repository you are in. Human-invoked only: adopting a process is a
decision about how a team works, not something a model concludes mid-task.

## Refuse if it is already adopted

If `.sdlc/` exists, stop and say so. Re-scaffolding would overwrite a solution's own declarations and
its local constraints, which are the two things here that are nobody else's to write.

## Refuse if this is not a git repository

Run `git rev-parse --git-dir`. If it fails, stop and say that the framework needs a repository, then
name the two commands that make one:

```
git init
git commit --allow-empty -m "before adoption"
```

Without git, `build` cannot record the diff that is the only evidence verify derives its claims from,
and the gates key on a `cycle/<id>` branch that cannot exist — so every hook permits everything while
the artifacts still look governed. A directory that appears enforced and is not is worse than one that
never pretended.

## Adoption is free

Read nothing to judge it. Constraints govern each change as it is made, never code that predates them,
so an existing repository is not in violation on its first day and there is nothing to baseline. If you
find yourself wanting to record existing problems, that is analyzer and refactoring work with its own
tooling, and it is not this framework's business.

## What to create

```
.sdlc/solution.md      declarations: stack and origin
.sdlc/README.md        terminology, flow and commands, for whoever works here
```

**Create no empty directories.** A cycle's directory is made by the skill that writes the first
artifact into it. An empty directory needs a placeholder file to survive git, and nothing ever removes
that placeholder once real files arrive.

### `.sdlc/solution.md`

Frontmatter carries exactly three keys, and no others — the schema rejects anything extra, so an extra
key makes the file invalid rather than merely untidy:

- `stack` — `$0` if given, otherwise `dotnet`. It decides which analyzers and plan defaults apply
- `origin` — `scaffolded` or `adopted`, decided by the rule below
- `adopted_at` — the current HEAD sha, **only** when `origin` is `adopted`

Do not add a date field. There is no `adopted_on`; the sha identifies the moment, and the commit it
names carries its own date.

```markdown
---
stack: dotnet
origin: scaffolded
---

# Solution profile — <repository name>

Scaffolded by `init`. Code written here is governed from the first cycle onward.
```

### Deciding `origin`

The distinction is about **code, not commits.** Run `git ls-files` and look at what is tracked.

- **`scaffolded`** — no source files. A repository holding only an empty commit, a README, or a
  licence is scaffolded: there is nothing that predates governance. Omit `adopted_at`
- **`adopted`** — source files exist that were written before the framework arrived. Record
  `adopted_at` as the current HEAD, which marks the boundary

A repository can have commits and still be `scaffolded`. Getting this wrong writes a false claim into
the one file every later cycle reads to understand where it is.

### `.sdlc/README.md`

For whoever works in this solution. Cover, in this order: the six phases and what each produces, the
three human gates and who holds them, the vocabulary — cycle, intent, spec, criterion, plan,
constraint, finding, waiver, outcome — the directory layout, and the commands in the order they are
run.

**Do not copy the constraints into it.** They live in the library and are never copied into a
solution: a stale local copy and the live set will disagree, and nothing will say which one binds.
Point at `/sdlc:constraints`, which renders the effective set — global plus this solution's own — on
demand.

## The instruction file

If the repository has no `AGENTS.md`, write a lean one: what the solution is, how to build and test it,
its repo map, and an index of any detail docs. Keep it under 200 lines — it loads into context every
session, and a longer file both costs more and is followed less.

Two things it must not contain:

- **Constraints.** Global ones live in the library, local ones in `.sdlc/CONSTRAINTS.md`. A third copy
  here would be a source that disagrees with both
- **Anything that goes stale the moment a cycle runs.** Do not write that the repository is empty, that
  there is no build yet, or a note telling a future reader to delete a paragraph once something lands.
  An instruction that depends on someone remembering to remove it will outlive the truth it described,
  and it is loaded into every session in the meantime. Describe what is true in a way that stays true,
  or leave the section out until there is something to say

If an `AGENTS.md` already exists, leave it alone and say what you would have added.

## Commit what you created

Adoption is not a change to be reviewed — it is the boundary every later cycle is measured from. So
commit it, on the current branch, before any cycle begins:

```
git add .sdlc AGENTS.md
git commit -m "adopt ai-sdlc"
```

This matters more than it looks. An uncommitted `AGENTS.md` is untracked at the point a cycle takes its
diff, so the diff records the whole file as new, and a plan that says it *changes* that file can never
match what the diff shows. The first cycle then fails CON_07 for something adoption did rather than
anything the cycle chose. Committing here is what keeps that from happening to every solution's first
cycle.

## Then report

List what you created and that it is committed, then say the first step is `/sdlc:intent`.
