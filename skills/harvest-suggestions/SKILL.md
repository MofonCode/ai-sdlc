---
name: harvest-suggestions
description: Harvest the suggestions -- answer every open suggestion of the completed cycles one of four ways, taken further as a new intent's successor, declined with the reason a person would give, covered by the rule, decision, successor or file that already handles it, or ignored and tallied by concern, and list the gaps the framework should hear about. Then draft the successors and record the aspect answered. Proposes and never places. Use when the harvest playbook reaches the suggestions, or when somebody asks to turn what readers noticed into new work.
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
sdlc harvest summary <the proposal file>
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
| **covered** | a rule, a decision, a successor or a file already handles it | a `## Covered` line naming what covers it, checked and kept in the sitting |
| **ignored** | nothing to do and nothing to decide: a one-off, a nicety, a reader's taste | an `## Ignored` line — a tally of the concern and the cycles |

**Decline is not a polite ignore.** Propose one only where you can write the reason the person would give. **A
decline is not a decision either**: it stays in the sitting and never reaches `decisions.md`.

**Covered is not a decline.** Where a rule in force, a standing decision, a successor or a file in this repository
already handles the concern, nobody decided against it: name that thing. `sdlc harvest record` checks every covered
line names something real, and refuses the sitting when one does not.

**An ignored suggestion is counted by its concern, not its id.** Where it is one of the report's *ignored before*
concerns, write the gist **exactly as written there**, so the tally gains this cycle. **A concern at the bar —
ignored in three cycles — is not ignored again**: make it a successor, decline it, or cover it. A covered line's
`Concern:` names the concern it closes, worded exactly as the tally has it, and takes it out of the tally.

**Themes are for the person's reading.** Under `## Declined`, `## Covered` and `## Ignored`, group the lines under
`### <theme>` headings — *Settled by a recorded decision*, *Already handled by a rule* — so the hand-back is a
handful of groups rather than a line per suggestion. A theme changes nothing recorded.

Each successor the report lists under *Gaps in the framework* goes under `## For the framework` (D-173), as a note
written for somebody who has never seen this solution (D-204): its heading names the part of the framework — a rule's
id, `stage:verify`, `command:evidence`, `evidence` — and what fell short, rewording its `framework:` line wherever
that names the solution's own product, files or people; a `Kind:` line says `cost`, `missing` or `defect`; its slug
goes beneath under `>`, which stays here. `sdlc harvest record` refuses a note with no surface or no `Kind:`.

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

### <theme>

- <cycle>/<suggestion id>[, <cycle>/<suggestion id>]: <why it will not be done>

## Covered

### <theme>

- <cycle>/<suggestion id>[, <cycle>/<suggestion id>]: <a rule id, a decision id, successor:<slug>, or a path> — <how it covers it>
  Concern: <the ignored or watched concern it closes, worded as the report has it — only when there is one>

## Ignored

### <theme>

- <the concern, in one sentence, worded as the report's ignored-before line when it is one> — <cycle>, <cycle>
  Why: <what it prevents, and what it promotes — for a new concern; a known one keeps its reason unless this is sharper>

## For the framework

### <surface> — <what fell short, worded for somebody who has never seen this solution>
Kind: <cost | missing | defect>
> <slug>: <its framework: line, as written>

## Cycles

- <each cycle the report lists, copied as written>
```

The text is copied in because the cycle it came from is about to be deleted.

## 4. Hand back, short

```
sdlc harvest summary <the proposal file>
```

Show its output: the successors to mark, each answer counted by theme, and every rule, decision and successor the
file names, with what it says. Add at most two sentences of your own; never one line per suggestion. They mark `[x]`
on the successors they want and may move any line between sections. Then stop.

## 5. Once they have marked it

```
sdlc draft harvest <the proposal file>
sdlc harvest review suggestions --dir <scratch>/review
sdlc harvest record suggestions <the proposal file>
```

The draft writes each accepted successor to `.sdlc/successors/<slug>.md`, ready once its cycle has merged; `/sdlc:wave`
or `/sdlc:series` picks it up. The reviewer naming a suggestion with no answer, or a concern at the bar ignored again, sends that one
back to step 2 before recording, as does a covered line `record` refuses. Say what was recorded in one line, and what
is next.
