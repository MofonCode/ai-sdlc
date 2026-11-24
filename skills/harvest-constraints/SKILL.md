---
name: harvest-constraints
description: Harvest the rules -- read what the completed cycles' findings argued, which rules they kept citing and which findings rested on no rule, and propose the constraints this solution is missing, each with its evidence, for a person to mark. Then draft what they accepted and record the aspect answered. Proposes and never places. Use when the harvest playbook reaches the rules, or when somebody asks what keeps going wrong across cycles.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[--cycles <id>,<id>]"
allowed-tools: Bash(sdlc *), Bash(git show *), Read, Write, Glob
---

# Harvest the rules

```
sdlc harvest constraints [--cycles <id>,<id>]
sdlc harvest read --dir <scratch>/harvest --aspect constraints
sdlc draft harvest <the proposal file>
sdlc harvest review constraints --dir <scratch>/review
sdlc harvest record constraints <the proposal file>
```

**A constraint is earned backward from the findings.** The same rule blocking three separate cycles says a rule is
missing; three findings inside one cycle is one document going round the loop. Only completed cycles count (D-190).

**A tool may propose a rule when it brings the evidence, and may not shape a rule somebody just typed.** Every
proposal arrives with the cycles and finding ids behind it, so a person refuses one on the record rather than on
taste. A rule a person already has in mind is `sdlc constraints --new`, not this.

## 1. Read the report, then the rounds

`sdlc harvest constraints` prints the rules cited across cycles, the rules **argued** longest inside one, findings
that rested on no rule grouped by who raised them, perspectives at the bar, abandoned cycles' reasons, deviations by
the rule behind them, each cycle's rounds and every criterion that failed more than one verdict, rules over their
word budget, and the concerns watched below the bar. It ends with the path of the proposal file.

**The report is what to read, not what to propose.** A history runs to hundreds of findings, so the grouping is
done by fresh processes the binary starts — **you start no process yourself**:

```
sdlc harvest read --dir <scratch>/harvest --aspect constraints
```

Each reader groups one document's findings by the problem underneath and returns the groups. Read their answers,
then read yourself each criterion that failed more than one verdict (`git show <commit>:<path>`).

**If it stops before every reader answered**, `run.log` in that directory says which reader failed and why, and
`<reader>.err` holds its own words (D-214). Tell the person what they say, then run it again: only the readers with
no answer yet are started.

## 2. Answer every item

Every rule cited across cycles, argued rule, group whose findings stood three rounds or more, perspective at the
bar, rule behind deviations in more than one cycle, criterion that failed more than one verdict, and rule over
budget gets **one** answer:

- a proposal, by the heading it will have in the file
- *already recorded*, naming the entry — `LOC_02`
- *a note for the framework* — `cost`: a framework rule, reader or step costing more here than it caught, which no
  local rule can change; `missing`: a rule any solution would need, not this one alone; `defect`: something the
  framework did wrong
- *no proposal, because* — a real reason: a one-off, a mistake the rule caught correctly, something an analyzer
  decides

**A rule nothing about this solution shapes is the framework's too.** When a proposal's `Forbids` would read the same
in any solution — a check that cannot fail, a test pinned to wording nobody fixed — propose it here if the person
wants it now, and write it under `## For the framework` as `missing` as well.

**A note is written for somebody who has never seen this solution** (D-204). Its heading names the part of the
framework — a rule's id, `perspective:security`, `stage:verify`, `command:evidence`, `evidence` — and what fell
short, with no product, provider, file or person of the solution's in it. The cycles, finding ids and rounds go
beneath it under `>`, which stays here: `sdlc harvest framework` prints everything above that line and nothing
below it. `sdlc harvest record` refuses a note with no surface or no `Kind:`.

**An argued rule is usually settled by a decision, not a rule.** Answer here only whether the rule itself needs
changing; `harvest-decisions` reads what the plan finally did about it.

**The three tests, on every proposal:**

1. its `Forbids` names something that becomes illegal
2. no analyzer, schema or hook could decide it
3. it is incompatible with at least one plausible implementation

Test two disqualifies most candidates: a rule about code belongs in an analyzer or an architecture test, which fails
a build where a constraint cannot. Say which, as a better home rather than a refusal. **"Nothing admissible" is a
good answer.**

A concern seen in fewer than three cycles goes under `## Watch`, worded the same way each time, so its count
survives the folders it came from. **Under a new concern, write a `Why:` line**: what it prevents and what it
promotes, in a line or two, from the findings that raised it. It is what a person weighs when the concern reaches
the bar, after the folders are gone (D-194). Under a known one, leave it out to keep the reason held, or write a
sharper one.

## 3. Write the proposal file

At the path the report names:

```markdown
# Harvest proposals: constraints — <date>

Mark each proposal you accept by changing [ ] to [x], and edit its wording as you want it.

## Rules

### [ ] add — <the rule's one line>
<its three clauses -- which artifact and when, what becomes illegal, what goes wrong without it -- each a bold
label and a colon, exactly as constraints.md writes them>
> <the cycles and finding ids behind it, each with one line of what it objected to>

### [ ] supersede LOC_02 — <the replacing rule's one line>
<its three clauses>
> <why the old one is out of force>

### [ ] reword LOC_01 — <the same rule, better words>
<its three clauses: the same Forbids, or it is a supersede>

## For the framework

### <surface> — <what fell short, worded for somebody who has never seen this solution>
Kind: <cost | missing | defect>
Cycles: <cycle>, <cycle>
Measure: <the numbers: cycles, findings, rounds, tokens>
> <the cycles, finding ids and rounds behind it -- kept here, never sent>

## Not proposed

- <the item> — <already recorded as LOC_04 | no proposal, because ...>

## Watch

- <a concern below the bar, in one sentence> — <cycle>, <cycle>
  Why: <what it prevents, and what it promotes>

## Cycles

- <each cycle the report lists, copied as written>
```

**There is no remove**: a rule out of force is superseded and keeps its place. Label nothing with an id you invent;
the drafter numbers what is accepted. A title takes at most 15 words and a clause at most 60 (D-149).

## 4. Hand back, short

Run `sdlc harvest summary <the proposal file>` and show it: each proposal, and every rule it names with what it says.
Add a line on why, if one is needed, how many items were answered without a proposal, and that they mark `[x]` on what
they accept. Then stop.

## 5. Once they have marked it

```
sdlc draft harvest <the proposal file>
sdlc harvest review constraints --dir <scratch>/review
```

The draft is a copy of `constraints.md` with only the accepted lines applied. The binary starts two fresh reviewers:
one asks whether what was approved still serves its evidence and `solution.md`, one names any item left without an
answer. **A gap goes back to step 2 before the person renames anything.** Say each reviewer's verdict in a line.

The rename is the person's: every agent write to `constraints.md` is denied. Once they have renamed the draft into
place, or deleted it:

```
sdlc harvest record constraints <the proposal file>
```

Say what it recorded in one line, and what is next.
