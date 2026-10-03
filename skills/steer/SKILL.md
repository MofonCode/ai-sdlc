---
name: steer
description: Carry a person's mid-cycle correction down the artifact chain, editing the intent, spec and plan rather than regenerating them. Use when someone wants to change how a cycle in progress is being done.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id> \"what to change\""
allowed-tools: Bash(sdlc *), Bash(git *), Read, Write, Edit, Glob, Grep
---

# Steer a cycle already underway

A comment arrives mid-cycle: *"use the existing cache abstraction rather than a new one"*. Doing that by
hand means editing the intent, re-interrogating it, editing the spec, re-interrogating that, and then
the plan. Nobody pays that, so the correction ends up in conversation where it is lost — and a path so
costly that it pushes information out of the system is worse than no path.

This is that path, made affordable. No constraint loosens.

## Refuse early

Requires an intent **and** a spec.

- **Only an intent?** Stop. There is nothing downstream to carry the change into, so the right move is
  to refine the intent.
- **The cycle is approved?** Stop. A standing approval covers the exact bytes of three documents, so
  editing one breaks the gate rather than satisfying it. Say that the move is
  `sdlc draft approval <id> --reopen` first, which records why the approval was withdrawn.
- **Abandoned?** Stop. Abandonment is terminal.

## Steering changes how, not what

| | |
|---|---|
| **This** | which approach, which abstraction, which order, what to avoid |
| **Not this** | a different problem, a new outcome, a changed definition of done |

A comment that changes the problem is a different intent. Say so and offer to open a new cycle. The
cost of getting this wrong is a cycle whose intent no longer describes the work, which is the thing the
whole chain exists to prevent.

## What to do

**1. Interview, briefly.** One or two questions, not the intent interview again. What you need is
enough to record the decision in the person's own terms: what to do instead, and why they want it.

**2. Record it as a fixed decision, with attribution.** In the intent's `Fixed decisions`:

```markdown
- Use the existing cache abstraction rather than a new one. Peter, 2026-10-02: a second
  abstraction would mean two eviction policies to reason about.
```

Attribution is what makes it a decision rather than a preference, and what stops a later phase
re-litigating it. `CON_01` routes decisions-already-taken here precisely so the intent stays a problem
statement.

**3. Edit, do not regenerate.**

| Artifact | What usually changes |
|---|---|
| intent | `Fixed decisions` gains the entry. The problem does not change |
| spec | usually nothing. If a criterion was written around the old approach, it was over-specified — fix that |
| plan | `Shape and departures`, and usually `Files`. Often `Proof` too |

Regenerating would discard the rounds already spent and the wording already argued over. The findings
history belongs to these documents; a regenerated artifact has a history about a document that no
longer exists.

**4. Re-interrogate everything downstream of what you edited.** Not optional, and checked: a findings
round records the hash of the artifact above it, so editing the intent makes the spec's latest round
stale, which fails the stage gate and the chain check. Re-interrogate what you touched and everything
below it.

**5. Report what changed, per artifact,** then:

```
sdlc status <id>
```

Show it verbatim and stop.

## Why this is not a finding

A finding needs an artifact to attach to and a round to live in, and steering most often arrives before
a plan exists. It is also not an objection — nothing is wrong with the artifact. It is a person
deciding, which is what `Fixed decisions` is for.
