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

## This is the whole of the learning loop today

Iteration 2 built a corpus with an importer and computed signals across cycles. Iteration 3 removed them
and recorded the cost plainly: nothing aggregates automatically. The data is in the findings rounds,
which is what this reads.

So if someone asks "what keeps going wrong across our cycles", the honest answer is that this listing is
what exists, and the aggregation is a deliberate deferral rather than an oversight. Three things would
have to be settled before it returns: the trap where an artifact's existence means done, so a mistaken
one silently loses work; a way to record which stage a problem was *caused* at, which is how the process
learns where it leaks; and the boundary between what a private solution contributes and what a public
corpus may hold.
