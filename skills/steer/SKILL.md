---
name: steer
description: Carry a person's mid-cycle correction down the artifact chain, editing the intent, spec and plan rather than regenerating them. Use when someone wants to change how a cycle in progress is being done, or to take in or decline suggestions a reader raised in it.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id> [<finding id>...] [\"what to change\"]"
allowed-tools: Bash(sdlc *), Bash(git *), Read, Write, Edit, Glob, Grep
---

# Steer a cycle already underway

A comment arrives mid-cycle: *"use the existing cache abstraction rather than a new one"*. Doing that by
hand means editing the intent, re-interrogating it, editing the spec, re-interrogating that, and then
the plan. Nobody pays that, so the correction ends up in conversation where it is lost — and a path so
costly that it pushes information out of the system is worse than no path.

This is that path, made affordable. No constraint loosens.

## Refuse early

Requires an intent **and** a spec — or, when every part of the request is a finding id, an intent alone.

- **Only an intent, and a correction in words?** Stop. There is nothing downstream to carry the change
  into, so the right move is to refine the intent.
- **A finding id that is not an open suggestion in this cycle's latest rounds?** Stop on that id and
  name what it is. A blocking finding is `refine`'s to apply, not a person's to decline. A suggestion
  from another cycle starts a cycle of its own: `/sdlc:intent --from suggestion:<cycle>/<finding>`.
- **The cycle is approved?** Stop. A standing approval covers the exact bytes of three documents, so
  editing one breaks the gate rather than satisfying it. Say that the move is
  `sdlc draft approval <id> --reopen` first, which records why the approval was withdrawn.
- **Abandoned?** Stop. Abandonment is terminal.

## Steering changes how — and takes in what a reader raised

| | |
|---|---|
| **This** | which approach, which abstraction, which order, what to avoid; **and suggestions a reader raised in this cycle, named by id**, each taken in or declined |
| **Not this** | a different problem; a new outcome nobody raised; a changed definition of done that did not come from a finding |

A comment that changes the problem is a different intent. Say so and offer to open a new cycle. The
cost of getting this wrong is a cycle whose intent no longer describes the work, which is the thing the
whole chain exists to prevent.

**Why a raised suggestion may change what, and a comment may not** (D-97). The spec may only derive
from what the intent states (`CON_05`), so a concern a perspective found — keyboard use, where a key is
kept, what is logged on a failure — reaches a criterion only through the intent, and only because a
person chose it. A suggestion was raised by a fresh reader against this cycle's own documents: the
person is choosing from what the cycle was found to lack, not inventing a new piece of work midway. A
new outcome nobody raised has had none of that scrutiny, so it is a new cycle with an interview of its
own.

## What to do

**Take every finding id the person named in one run.** Each run re-interrogates the chain from the intent
down to where the cycle has got to, so three suggestions taken in one at a time cost three passes and
the same three taken together cost one. A correction in words may come in the same run.

**1. Interview, briefly.** One or two questions, not the intent interview again. What you need is
enough to record the decision in the person's own terms: what to do instead, and why they want it. For
finding ids, **one question covering all of them, in free text and never from a picker** (D-114): show
each suggestion's statement, grouped by theme, and ask what to take in, decline, or leave open. Leaving
it open is an answer — it stays in `sdlc suggestions` for a later cycle — and so is a group answered at
once. For each one taken in, ask how they would say it; for each one declined, why; then read back what
you will write before writing it. **When a stage's hand-back has just asked**, its answers and its
read-back are this interview: carry them, and ask only what they left unsaid.

**2. Record it as a fixed decision, with attribution.** In the intent's `Fixed decisions`:

```markdown
- Use the existing cache abstraction rather than a new one. Peter, 2026-10-02: a second
  abstraction would mean two eviction policies to reason about.
```

Attribution is what makes it a decision rather than a preference, and what stops a later phase
re-litigating it. `CON_01` routes decisions-already-taken here precisely so the intent stays a problem
statement.

**A suggestion taken in is a wanted outcome, in the person's words, and a declined one is a fixed
decision.** Both name the finding:

```markdown
## Wanted outcomes

- A trader can pick a stock and reach its chart by keyboard alone. Peter, 2026-10-22, taking in
  accessibility.S_01.

## Fixed decisions

- Not in this cycle: a text equivalent of the chart. Peter, 2026-10-22, declining accessibility.S_04:
  the first piece is for sighted watching; the table comes with the signals.
```

**Write each id straight after `taking in` or `declining`, exactly as the ledger holds it** — several
joined by commas or *and*, a decline's reason after a colon. `sdlc suggestions` reads that form and no
other to say which were accepted and which declined, and by which cycle (D-115); *"taking in the
keyboard one"* is a decision the listing cannot see.

**No finding's disposition is set here.** A disposition moves only on a fresh reader's silence (D-49):
the next round's reader finds the outcome stated, or the matter decided, and does not raise it again, so
the round records it `answered`. A reader that raises it anyway is saying the wording did not settle it,
which is worth hearing rather than overriding.

**3. Edit, do not regenerate.**

| Artifact | What usually changes |
|---|---|
| intent | `Fixed decisions` gains the entry, and `Wanted outcomes` each suggestion taken in. The problem does not change |
| spec | for a correction, usually nothing. If a criterion was written around the old approach, it was over-specified — fix that. For a suggestion taken in, a criterion covering all of the new outcome, and its *not asked for by the intent* line gone from `Out of scope` |
| plan | `Shape and departures`, and usually `Files`. Often `Proof` too, and always for a new criterion |

Regenerating would discard the rounds already spent and the wording already argued over. The findings
history belongs to these documents; a regenerated artifact has a history about a document that no
longer exists.

**4. Re-interrogate from the top, one document at a time.** Not optional, and checked: a round records
the hash of what it judged and of the artifact above it, and `sdlc round` refuses a spec round until the
intent is settled and a plan round until the spec is. So run `/sdlc:interrogate` on the highest document
you edited, refine until its round is clean, and only then move down — **never two documents' rounds at
once**. You may edit a lower document while an upper one is being judged; its round simply waits.

Each downstream round carries a `note` saying what moved above it and what this document did about it.
A spec left unchanged by a corrected intent says so there, with why — the next reader of the history
should meet a decision, not a gap.

**5. Report what changed, one line per artifact** — and nothing about whether the correction was a good
one. Then:

```
sdlc status <id>
```

Show it verbatim and stop.

## Why this is not a finding

A finding needs an artifact to attach to and a round to live in, and steering most often arrives before
a plan exists. It is also not an objection — nothing is wrong with the artifact. It is a person
deciding, which is what `Fixed decisions` is for. Taking in a suggestion is the same act from the other
side: a finding already exists, and the person decides what it becomes.
