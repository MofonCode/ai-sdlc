---
name: suggestions
description: List the non-blocking suggestions recorded across this solution's cycles. Use when a product owner or tech lead asks what has been noticed but not acted on.
argument-hint: "[cycle-id, optional]"
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Suggestions across cycles

A read-only view. It reads the verify documents under `.sdlc/cycles/*/verify.md` and lists their
`suggestion` findings. It writes nothing, stores nothing, and has nothing to keep in sync — which
is the whole reason it is a query rather than a queue.

With a cycle id, show that cycle only. Without one, show every cycle, newest first.

For each suggestion print: the cycle, the constraint it cites, its statement, and its locus. Group
by constraint when several cycles cite the same one — that repetition is the most useful thing this
view produces, because a constraint raised in six cycles is telling you something about the
codebase that no single cycle can.

## What this view deliberately does not do

It does not create intents. Suggestions are not promoted automatically: an unactioned intent queue
rots in a way that is hard to detect, and is worse than no queue.

If someone wants to act on what they see, say the next step is `/sdlc:intent` citing the verify
document and naming the suggestions to take up. That keeps the decision with a person and leaves no
backlog nobody chose.
