---
name: harvest-decisions
description: Harvest the decisions -- read what the completed cycles settled (a person's fixed decisions, abandonment and reopen reasons, declined deviations, the plan approaches that recur, departures from a standing decision) and propose the decisions this solution should record, each a choice future cycles will meet again, for a person to mark. Then draft what they accepted and record the aspect answered. Proposes and never places. Use when the harvest playbook reaches the decisions, or when somebody wants to record a decision a cycle settled.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[--cycles <id>,<id>]"
allowed-tools: Bash(sdlc *), Bash(git show *), Read, Write, Glob
---

# Harvest the decisions

```
sdlc harvest decisions [--cycles <id>,<id>]
sdlc draft harvest <the proposal file>
sdlc harvest review decisions --dir <scratch>/review
sdlc harvest record decisions <the proposal file>
```

**What a decision is** (D-189): a choice that had a real alternative, that future cycles will meet again, whose
reason is not in the code, and that steers without forbidding — departing from it is declared, not blocked.

**A decision is captured forward**, because a choice that never collided with a rule produced no finding to count:
a colour, a layout convention, a code-style choice is nowhere in the findings. What can be read is where a choice
was *stated*, in a completed cycle — merged after a passing verify, or abandoned.

## 1. Read the report

`sdlc harvest decisions` prints the passages, by cycle and source, the reopens and the changes made at a person's
request, the rules argued inside a cycle, the standing decisions, and those over their word budget.

**Who wrote a passage decides how much it takes:**

| Source | Qualifies |
|---|---|
| an intent's fixed decision, an abandonment's reason, a reopen's reason, a declined deviation's note | **once** — a person's own words |
| a plan's shape and departures | only when **the same choice is in two or more cycles** — an agent's prose, and one cycle is a local choice |
| a departure from a standing decision | **once**, as a supersede of the decision it names |
| a rule argued inside a cycle | what the plan **finally did** about it, read in the plan |
| a fixed decision marked unconfirmed | **never** — an assumption; the binary has already left it out |

From an abandoned cycle only its reason is offered: its approach is what was refused.

**A plan's shape comes whole**, one passage per plan with its bullets kept (D-225): compare the approaches across
cycles and propose the choice two or more of them share, never one bullet at a time. A fixed decision that only
bounds its own piece of work — *not in this piece*, *later* — is no choice future cycles meet: answer it *no
proposal, because it bounds one cycle*.

## 2. Answer every item

For each passage, reopen, declined deviation and argued rule, decide whether it holds a real choice. **The four
tests**, all of which must pass:

1. the alternatives were real — somebody could have chosen otherwise
2. future cycles will meet it again — it reaches past the cycle that made it
3. the reason is not recoverable from the code
4. it forbids nothing

Test four sends work elsewhere: where a choice forbids something, that half is a rule (`harvest-constraints`) or an
architecture test — two artifacts of one choice, the decision saying why. Say so under `## Not proposed`.

Every item gets a proposal, *already recorded* naming the entry, *a note for the framework*, or *no proposal,
because* with a real reason — *a local choice, said in one cycle only* is one.

**What the framework already holds is already recorded** (D-219): a global or stack rule (`CON_22`, `PRO_05`), a
guidance entry (`guidance:pipeline`), or a setting the solution has chosen (`solution.md: branch-policy`). Each binds
every cycle without a decision, so restating one here adds no force and leaves a second copy to drift. Name it as
the entry. Only what the solution chose beyond it is a decision: a value the framework left open, or a narrower
form of what it says.

**A note for the framework is written for somebody who has never seen this solution** (D-204): a choice every cycle
had to make again because the framework left it open, or a step that cost more than it gave. Its heading names the
part of the framework — a rule's id, `stage:plan`, `document:intent`, `command:draft` — and what fell short, with
no product, provider, file or person of the solution's in it; a `Kind:` line says `cost`, `missing` or `defect`.
The evidence goes beneath under `>`, which stays here. **`Cycles:` names the cycles whose record shows it, by id**
— `all` only when every cycle of the sitting does (D-225). `sdlc harvest record` refuses a note with no surface, no
`Kind:`, or no `Cycles:` line of cycle ids.

**`Instead of` is the load-bearing clause**: an entry whose alternatives were never real records something nobody
chose. **`Because` cannot be recovered later**: if the passage does not say why, ask the person rather than invent
one. **A declined suggestion is a decline, not a decision** — it belongs to `harvest-suggestions`.

## 3. Write the proposal file

At the path the report names:

```markdown
# Harvest proposals: decisions — <date>

Mark each proposal you accept by changing [ ] to [x], and edit its wording as you want it.

## Decisions

### [ ] add — <the decision's one line>
**Decided:** <what>

**Because:** <why>

**Instead of:** <what was weighed and refused>

**From:** <the cycle, and where it was settled>

### [ ] supersede DEC_02 — <the reversal's one line>
<its four clauses>

## For the framework

### <surface> — <what fell short, worded for somebody who has never seen this solution>
Kind: <cost | missing | defect>
Cycles: <cycle>, <cycle> | all
Measure: <the numbers: cycles, findings, rounds, tokens>
> <the cycles, finding ids and rounds behind it -- kept here, never sent>

## Not proposed

- <the item> — <already recorded as DEC_04, CON_22, PRO_05, guidance:pipeline or solution.md: branch-policy | no proposal, because ...>

## Cycles

- <each cycle the report lists, copied as written>
```

**A reversed decision takes a new id and the old one stays on the page**, so a cycle that cited it is still
readable. A title takes at most 15 words and a clause at most 60 (D-149); an entry over budget is superseded with
shorter wording, never reworded in place.

## 4. Hand back, short

Run `sdlc harvest summary <the proposal file>` and show it: each proposal, and every decision it names with what it
says. Add whose words each came from, how many items were answered without one, and that they mark `[x]` on what they
accept. Keep its numbers: a person may answer by number instead (*accept all but 11*), and you mark the file as they said. Then stop.

## 5. Once they have marked it

```
sdlc draft harvest <the proposal file>
sdlc harvest review decisions --dir <scratch>/review
```

The binary starts two fresh reviewers; **a gap one names goes back to step 2** before the person renames anything.
Say each verdict in a line. `decisions.md` is a person's file and every agent write to it is denied: a stage reads
it for alignment, so an agent that could write it could invent doctrine and then align to it. Once they have renamed
the draft into place, or deleted it:

```
sdlc harvest record decisions <the proposal file>
```

Say what it recorded in one line, and what is next.
