---
name: harvest
description: Harvest the completed cycles -- for every cycle whose pull request merged after a passing verify, or that a person abandoned, take back what it settled one aspect at a time (rules, decisions, suggestions, stack, areas), each answered with the person before the next, then close the cycles every aspect has answered. A playbook: the five harvest skills read and propose, close-cycle opens the pull request that removes the closed cycles. Proposes and never places. Use once a cycle's pull request has merged, or when somebody asks to bring the records up to what the cycles settled or to clear out old cycles.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[--cycles <id>,<id>]"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(gh *), Bash(az *), Read, Write, Glob
---

# Harvest the completed cycles

**A playbook** (D-186): it runs the skills that read and propose, one at a time, and writes nothing itself.

| Step | Skill |
|---|---|
| the rules the findings argued | `harvest-constraints` |
| the choices the cycles settled | `harvest-decisions` |
| each open suggestion: taken further, declined, covered or ignored | `harvest-suggestions` |
| what the declared stack never mentions | `harvest-stack` |
| the areas the plans named | `harvest-areas` |
| close the cycles every aspect has answered | `close-cycle` |

**Only completed cycles are read** (D-190): a cycle whose pull request merged after a passing verify — a person
accepted it — or one a person abandoned, merged or not: one stopped before any of it merged is brought in from its own
branch by `sdlc harvest`, untracked. A cycle still in flight, or one that passed verify and has not merged, is
read by nothing here, and the overview says it is waiting. What a harvest places binds the **next** cycle, never one
in flight.

## 1. See what is open

```
sdlc harvest
```

It lists each completed cycle not closed yet and the aspects it still needs. Tell the person in two or three lines:
how many cycles, what is left, and which aspect comes first. **Run only the aspects still listed**; one already
answered for every cycle is skipped.

**A long backlog is harvested a few cycles at a time** (D-166). When the person names cycles, or there are more than
one sitting can read with care, pass `--cycles <id>,<id>` to each step and say which cycles this sitting reads.

## 2. One aspect at a time

Run each aspect still listed, in this order, **one at a time**:

1. **`harvest-constraints`**
2. **`harvest-decisions`**
3. **`harvest-suggestions`**
4. **`harvest-stack`**
5. **`harvest-areas`**

Each writes its proposal file, hands back a short report, and waits for the person to mark it. When they have, the
same skill drafts, reviews and records it. **Only then start the next.** Never hand the person two aspects at
once: one aspect's report is what a person can sort through, and five together is not.

**Keep every hand-back short**: each skill shows `sdlc harvest summary` of its proposal file — the proposals, numbered so a person can answer by number, the
answers counted by theme, and each id with what it says — and the one thing needed from the person. The reports are kept under `.sdlc/reports/` for anyone who wants the detail.

A person may stop after any aspect. Nothing is lost: the ledger keeps which aspects each cycle has answered, and the
next `sdlc harvest` picks up where this stopped.

## 3. Close

Once every aspect is answered for a cycle, run **`close-cycle`**. It checks that nothing is left, commits the
closed cycles' removal on a branch of its own, and opens the pull request a person merges.

## Hand back

One short message: what was placed, what was declined, covered or ignored, which cycles closed and the pull request's link,
and which cycles stay open with what they still need. When a sitting kept notes for the framework, say how many, and
that `sdlc harvest framework` prints what of them could be handed to the framework's maintainers — the person's
choice, never sent for them (D-204).
