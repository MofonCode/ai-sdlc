---
name: harvest-stack
description: Harvest the stack -- read what this release ships that the declared stack never mentions, and what the completed cycles built with, and propose the rows techstack.md should set, each with what in the record asks for it, for a person to mark. Then draft what they accepted and record the aspect answered. Proposes and never places. Use when the harvest playbook reaches the stack.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[--cycles <id>,<id>]"
allowed-tools: Bash(sdlc *), Read, Write, Glob
---

# Harvest the stack

```
sdlc harvest stack [--cycles <id>,<id>]
sdlc draft harvest <the proposal file>
sdlc harvest review stack --dir <scratch>/review
sdlc harvest record stack <the proposal file>
```

The declared stack is what a plan is judged against for its choice of tools (CON_15). A row that says `none`, or a
rule switched off, is an answer; **no row is a question nobody was asked**, and this aspect asks it.

## 1. Read the report

`sdlc harvest stack` lists each default and stack rule this release ships that `techstack.md` never mentions. Read the
completed cycles' plans for what they actually used (`sdlc stack` shows what is declared).

## 2. Answer every row

Each row or rule the report lists gets a `set` proposal — the choice the cycles made, or `none` where the solution
does not use it — or a line under `## Not proposed` saying why it is left unanswered for now. **There is no remove**:
a concern no longer used is set to `none`.

## 3. Write the proposal file

At the path the report names:

```markdown
# Harvest proposals: stack — <date>

## Stack

### [ ] set <concern> — <the choice, or none>
> <what in the record asks for it: the cycle and the plan line>

## Not proposed

- <the row> — <why it stays unanswered>

## Cycles

- <each cycle the report lists, copied as written>
```

## 4. Hand back, short

Tell the person: how many rows, one line each (the concern, the choice, and why), and that they mark `[x]` on what
they accept. Then stop.

## 5. Once they have marked it

```
sdlc draft harvest <the proposal file>
sdlc harvest review stack --dir <scratch>/review
```

Say each reviewer's verdict in a line; a gap goes back to step 2. `techstack.md` is a person's file: they rename the
draft into place, or delete it. Then:

```
sdlc harvest record stack <the proposal file>
```

Say what it recorded in one line, and what is next.
