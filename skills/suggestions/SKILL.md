---
name: suggestions
description: List the open non-blocking findings a solution has accumulated. Use when someone asks what has been noticed but not acted on, or is looking for small work worth picking up.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[cycle id]"
allowed-tools: Bash(sdlc *)
---

# List the open suggestions

```
sdlc suggestions [<cycle id>]
```

No id lists every cycle, which is usually the question being asked.

Show what it prints verbatim.

A cycle contributes `Noticed:` lines only from its own branch, measured from where it diverged. A cycle
that has not been built yet has no commits to read, and a merged cycle whose branch is gone contributes
nothing — neither is an error, and neither is worth remarking on.

## Non-blocking is not trivial

This is the thing to get right when reporting these. A suggestion did not block an artifact, which says
nothing about whether it matters. Two real ones from a single trial cycle:

- `dotnet test` did not actually run the suite, because the runner was not configured
- a criterion's proof rested entirely on a substituted source

Both were substantive. Neither cited a rule that forbade the current state, which is the only thing that
made them suggestions rather than blockers.

## No ranking, and no ages

The listing does not sort by age and does not say how long anything has been open, because a
suggestion's age says nothing about its worth and a tool that sorted by age would quietly argue that the
oldest matters most. Do not add that when you report.

If the person asks which to pick up, read the statements and say what you think in **at most two
lines**, under the literal heading `My reading:` so it is not mistaken for something the command
printed. **Unasked, add nothing** — the listing is the answer.

## Two stores, and the listing reads both

A suggestion was raised by a fresh reader judging a document, and it has a round and an id. A
**`Noticed:`** line was written by a build that saw something it was not allowed to fix — fixing it
would have been creep inside an approved file — and it lives in that build's commit message, which is
where whoever reviews the pull request will meet it.

They are listed apart, under their own headings, because only one of them has a round and an id.
Mixing them would make a build's observation look like a finding nobody dispositioned.

Reading only the rounds was a real hole: the dice-roller cycle's build wrote three `Noticed:` lines and
the command that answers *what have we noticed and not acted on* listed none of them.

## What this is not: the cross-cycle question

This answers *what is outstanding* — what was noticed and nobody acted on. **It aggregates nothing, on
purpose.** If someone asks "what keeps going wrong across our cycles", that is a different question with
its own command:

```
sdlc propose constraints
```

It reads every cycle's findings — including the blockers that were answered, which this listing never
shows — and reports a rule cited in three or more distinct cycles, with the cycles and finding ids behind
each, as a candidate for one of this solution's own constraints (`CONCEPT.md` D-69). The two read the
same rounds for opposite halves of them, so send the question to the one that answers it rather than
reading a pattern into this listing yourself.

**What neither answers is which stage a problem was *caused* at** — how the process would learn where it
leaks. Nothing records that today, and saying so is better than guessing at it from the listing.
