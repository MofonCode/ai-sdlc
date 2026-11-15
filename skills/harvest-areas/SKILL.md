---
name: harvest-areas
description: Harvest the areas -- read the area names the completed cycles' plans proposed, how often each defined area matched a plan, and the changed files no area covers, and propose the areas to add, rename, merge, remove, re-glob or tag, for a person to mark. Then draft what they accepted and record the aspect answered. Proposes and never places. Use when the harvest playbook reaches the areas.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[--cycles <id>,<id>]"
allowed-tools: Bash(sdlc *), Read, Write, Glob
---

# Harvest the areas

```
sdlc harvest areas [--cycles <id>,<id>]
sdlc draft harvest <the proposal file>
sdlc harvest review areas --dir <scratch>/review
sdlc harvest record areas <the proposal file>
```

An area is a name for a set of files. It decides which rules and decisions reach each reader in full, so an area
that covers everything hides nothing and one that covers nothing reaches no one (D-150).

## 1. Read the report

`sdlc harvest areas` lists each name plans proposed and in how many cycles, how many plans each defined area would
have matched, and the changed files no area covers.

## 2. Answer every item

**An area is proposed at the bar a rule meets**: one cycle is an instance, two a coincidence, three a pattern — so
`add` an area only at three. Names that differ over the same files are one area: reconcile them, using the word the
team would use. Then:

- an area every plan touches hides nothing: `merge` or `remove` it
- an area no plan touched in many cycles is a candidate for `remove`
- changed files in no area are where a new area, or `files` for an area whose files moved, belongs
- a decision or rule only one area's work reaches gets a `tag`; one about the business stays global

Every `tag` names an area that exists or is added in the same file — the drafter refuses one that does not. An area
may be removed, unlike a rule or a decision: nothing cites an area's name, and its entries become global.

## 3. Write the proposal file

At the path the report names:

```markdown
# Harvest proposals: areas — <date>

## Areas

### [ ] add <name> — <what the area is, in one line>
**Files:** <glob>, <glob>
> <the cycles whose plans proposed it, and how many plans the replay says it matches>

### [ ] rename <old> <new> — <why the new word is the team's>
### [ ] merge <from> into <to> — <why they always match together>
### [ ] remove <name> — <why it never narrows anything>
### [ ] files <name> — <the new globs, after the files moved>
### [ ] tag DEC_04 — <area, area -- or none>

## Not proposed

- <the item> — <why>

## Cycles

- <each cycle the report lists, copied as written>
```

## 4. Hand back, short

Tell the person: how many proposals, one line each, and that they mark `[x]` on what they accept. Then stop.

## 5. Once they have marked it

```
sdlc draft harvest <the proposal file>
sdlc harvest review areas --dir <scratch>/review
```

The draft carries a renamed or removed area onto every entry naming it. Say each reviewer's verdict in a line; a gap
goes back to step 2. `areas.md` is a person's file: they rename the draft into place, or delete it. Then:

```
sdlc harvest record areas <the proposal file>
```

Say what it recorded in one line, and what is next.
