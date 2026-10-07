---
name: propose
description: Read what a solution's whole history says about the rules it is missing, and propose entries a person may adopt. Proposes and never places. Use when somebody asks what keeps going wrong across cycles, wants to add one of this solution's own constraints, or is looking for rules worth having.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "constraints"
allowed-tools: Bash(sdlc *)
---

# Propose what the record is pushing toward

```
sdlc propose constraints
```

**Two verbs, split by who made the decision.** `draft` is for a decision somebody already took: it
hands back a finished file and the rename or the paste that places it. `propose` is for one they have
not. Identical grammar on purpose — a subject after the verb — so the only thing to remember is which
side of the decision you are on.

**A tool may propose a rule when it brings the evidence, and may not shape a rule somebody just
typed.** That is the whole licence for this skill, and it is why there is no way to pass wording in.
Every proposal arrives with the cycles and the finding ids behind it, so a person can refuse one on
the record rather than on taste.

Run the command and show what it prints **verbatim**. Then do the reading it cannot: the sections
below. Everything you add goes under its own heading so nothing you wrote is mistaken for something
the command found.

## What the command gives you, and what it deliberately does not

It counts, groups and allocates. It forms no opinion.

| It reports | Which means |
|---|---|
| rules cited in three or more **distinct cycles** | two is coincidence, three is a pattern |
| rules cited in two cycles, as a count only | named so the report is honest, withheld so the bar means something |
| findings that rested on **no rule** | a reader saw something real that nothing forbade |
| cycles that **stopped**, with the hand-written reason | the best evidence in the repository, and the kind nothing can tally |
| the next free `LOC_` id, what is in force, what is superseded | the bookkeeping |
| what a person **already refused** | so you do not ask twice |

Distinct cycles, not findings. Three findings against one rule inside a single cycle is one artifact
going round the loop; the same rule blocking three separate cycles is a solution walking into the same
wall.

## Your four sections

**Add.** A new rule, complete and paste-ready, with its evidence above it. Label candidates `P1`,
`P2`, `P3` — never `LOC_07`, `LOC_08`, `LOC_09`. State the next free id once and let the person number
what they accept. A proposal nobody took must leave no trace in the sequence: a gap is the record of a
constraint that was *removed*, and one left by a refused proposal is indistinguishable from a rule
somebody retired.

**Reword.** Same `Forbids`, better words — a revision, `LOC_03` becoming `LOC_03.2`, replacing the old
text on the page. Say out loud that the person decides whether it is really a revision: if what is
forbidden changed at all, it is a new rule at a new id and the old entry stays. That judgement cannot
be made mechanically, and getting it wrong as a revision loses the old rule for good, because a
revision leaves no predecessor behind.

**Supersede.** Never a deletion. Print the `Superseded-by` line to add, and nothing to cut. A rule out
of force keeps its place on the page so a finding that cited it stays readable, and whatever you print
is what gets pasted — which makes your text the place this is really enforced.

**Belongs below a constraint.** Expect this to be your biggest section. Most of what a history review
turns up is mechanical — a naming convention, a layering rule, a banned API — and those belong in an
analyzer, an architecture test or `BannedSymbols.txt`. Say which, and say it as a better home rather
than a refusal: all three fail a build, which a constraint cannot.

## The three tests, on every candidate

1. its `Forbids` clause names something that becomes illegal
2. no analyzer, schema or hook could decide it
3. it is incompatible with at least one plausible implementation

**Test two disqualifies most candidates**, and that is the test to apply hardest against yourself. A
rule about code belongs in an analyzer, and where an existing analyzer already decides it, name that
analyzer rather than writing a rule.

**A recurrence is not automatically a missing rule.** It is as likely to mean the opposite — that an
existing rule keeps catching the same real mistake and is doing its job. Which it is depends on
whether the solution could have been built so the mistake was impossible. Say which you think it is,
and why.

**"Nothing admissible this time" is a good answer.** The library exists to be read at every
interrogation, and a rule that fires inconsistently costs more than it buys. A run that proposes
nothing is not a run that failed.

## You cannot place any of it

Every agent write to `constraints.md` is denied at the tool call, by file path and by shell command
alike. Do not offer to place a proposal, do not offer a shell command that would, and do not read the
refusal as a configuration problem. A cycle judged by the constraints it is changing is the same
deadlock as the framework governing itself.

What a person does with an accepted proposal is paste it into `.sdlc/constraints.md` by hand. Say so,
and stop.

## Where a refusal goes

Under a `## Considered and rejected` heading at the foot of `constraints.md`, one line each, written by
hand. This command reads that section and stays quiet about what is in it.

Tell the person this exists when you propose something you expect them to refuse. Without it the same
two candidates come back on every run forever, which is how a report teaches people to skim it.

## When the record holds no pattern

The command prints an empty entry at the next free id with the three admission tests beside it — the
shape, when it cannot propose the rule. That is the ordinary state of a young solution and of a
well-behaved one, and it is also the path for a person adding a rule by hand on day one, before any
cycle exists.

Show it and add nothing. There is no evidence to read, so there is no reading to do.

## The report is kept, and nothing reads it

The command writes its answer to `.sdlc/reports/`, because a person works through one of these rather
than reading it once. That directory ignores itself and **nothing reads a report back, ever** — a file
whose existence meant "history reviewed up to here" would be state a tool trusts, and a mistaken one
would silently lose the learning.

So never cite a report as current. If one is on disk and you want what it says, re-run the command.

## This is not `suggestions`

`sdlc suggestions` lists what a reader already raised and nobody acted on — open, non-blocking,
per-item, no opinion. This reads the whole history including **closed blockers**, which is where the
strongest evidence lives and which that command deliberately never shows. If somebody wants to know
what is outstanding, send them there instead.
