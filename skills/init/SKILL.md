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

This is not bureaucracy. Without git, `build` cannot record the diff that is the only evidence verify
derives its claims from, and the gates key on a `cycle/<id>` branch that cannot exist — so every hook
permits everything while the artifacts still look governed. A directory that appears enforced and is
not is worse than one that never pretended.

A repository with no commits is also not enough: `origin: adopted` records the sha that marks what
predates governance, so ask for that empty commit rather than guessing.

## Adoption is free

Read nothing to judge it. Constraints govern each change as it is made, never code that predates
them, so an existing repository is not in violation on its first day and there is nothing to
baseline. If you find yourself wanting to record existing problems, that is analyzer and
refactoring work with its own tooling, and it is not this framework's business.

## What to create

```
.sdlc/solution.md      declarations: stack and origin
.sdlc/README.md        terminology, flow and commands for whoever works here
```

**Create no empty directories.** `.sdlc/cycles/<id>/` is created by the skill that writes the first
artifact into it. An empty directory needs a placeholder file to survive git, and nothing ever removes
that placeholder once real files arrive.

`solution.md` is markdown with YAML frontmatter carrying:

- `stack` — `$0` if given, otherwise `dotnet`. It decides which analyzers and plan defaults apply
- `origin` — `scaffolded` for an empty repository, `adopted` for one with history. Look, do not ask
- `adopted_at` — the current HEAD sha, when `origin` is `adopted`. It marks what predates governance

**No `.sdlc/constraints.md`.** A solution starts with no local constraints and earns its first one
when a real hazard justifies it. An empty file with ID discipline attached is ceremony.

## The instruction file

If the repository has no `AGENTS.md`, write a lean one: what the solution is, how to build and test
it, its repo map, and an index of any detail docs. Keep it under 200 lines — it loads into context
every session, and a longer file both costs more and is followed less.

It carries **no constraints**. Global ones live in the library and local ones in
`.sdlc/constraints.md`; a third copy here would be a source that disagrees with both.

If an `AGENTS.md` already exists, leave it alone and say what you would have added.

## Then report

List what you created, and say the first step is `/sdlc:intent`.
