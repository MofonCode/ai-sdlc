---
name: harvest-suggestions
description: Harvest the suggestions -- answer every open suggestion of the completed cycles one of three ways, taken further as a new intent's successor, declined with the reason a person would give, or ignored and tallied by concern, and list the gaps the framework should hear about. Then draft the successors and record the aspect answered. Proposes and never places. Use when the harvest playbook reaches the suggestions, or when somebody asks to turn what readers noticed into new work.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[--cycles <id>,<id>]"
allowed-tools: Bash(sdlc *), Read, Write, Glob
---

# Harvest the suggestions

```
sdlc harvest suggestions [--cycles <id>,<id>]
sdlc draft harvest <the proposal file>
sdlc harvest review suggestions --dir <scratch>/review
sdlc harvest record suggestions <the proposal file>
```

A suggestion is a non-blocking finding a reader raised. A cycle in flight answers its own through steer; once the
cycle is completed, each one still open is answered here, before the cycle's folder goes.

## 1. Read the report

`sdlc harvest suggestions` lists each open suggestion as `<cycle>/<id>`, the concerns earlier sittings ignored with
the cycles each was ignored in, and every successor marked `framework:`.

## 2. Answer every suggestion

**Propose the answer it earns, not the easiest one** (D-153):

| Answer | When | Where it lives afterwards |
|---|---|---|
| **taken further** | it is worth doing | a successor under `## Successors`, its text copied in — then the intent it becomes |
| **declined** | somebody decided against it, and will be asked again | a `## Declined` line with the reason, kept in the sitting |
| **ignored** | nothing to do and nothing to decide: a one-off, a nicety, a reader's taste | an `## Ignored` line — a tally of the concern and the cycles |

**Decline is not a polite ignore.** Propose one only where you can write the reason the person would give. **A
decline is not a decision either**: it stays in the sitting and never reaches `decisions.md`.

**An ignored suggestion is counted by its concern, not its id.** Where it is one of the report's *ignored before*
concerns, write the gist **exactly as written there**, so the tally gains this cycle. **A concern at the bar —
ignored in three cycles — is not ignored again**: make it a successor or decline it.

Each successor the report lists under *Gaps in the framework* goes under `## For the framework`, by its slug and its
`framework:` line (D-173).

## 3. Write the proposal file

At the path the report names:

```markdown
# Harvest proposals: suggestions — <date>

## Successors

### [ ] add <slug> — <what it makes possible>
**Succeeds:** <the cycle it came from>

**Outcome:** <what it should make possible>

**Why it matters:** <the suggestion's id, its gist and its remedy, copied>

## Declined

- <cycle>/<suggestion id>: <why it will not be done>

## Ignored

- <the concern, in one sentence, worded as the report's ignored-before line when it is one> — <cycle>, <cycle>

## For the framework

### <slug> — <its framework: line>

## Cycles

- <each cycle the report lists, copied as written>
```

The text is copied in because the cycle it came from is about to be deleted.

## 4. Hand back, short

Tell the person: how many suggestions, and how many of each answer, with one line per successor and per decline.
They mark `[x]` on the successors they want and may move any line between sections. Then stop.

## 5. Once they have marked it

```
sdlc draft harvest <the proposal file>
sdlc harvest review suggestions --dir <scratch>/review
sdlc harvest record suggestions <the proposal file>
```

The draft writes each accepted successor to `.sdlc/successors/<slug>.md`, ready once its cycle has merged; `/sdlc:next`
picks it up. The reviewer naming a suggestion with no answer, or a concern at the bar ignored again, sends that one
back to step 2 before recording. Say what was recorded in one line, and what is next.
