# The guides

Four documents, written for somebody using the framework rather than changing it. This file is the index:
what each one covers, when you would read it, and what it deliberately leaves to another.

**They are in the order a person meets them.** Install once per machine, walk one cycle once, then keep
the command sheet open and the refusal index to hand. Nothing here needs reading front to back twice.

| Guide | Covers | Read it |
|---|---|---|
| [`install.md`](install.md) | the plugin, the binary, updating, removing, and the four things that catch people | once per machine, before anything else |
| [`first-cycle.md`](first-cycle.md) | one cycle from an empty directory to a pull request, in fourteen steps | once, front to back |
| [`cycle-commands.md`](cycle-commands.md) | every command for one cycle, in order, on one screen | while working |
| [`when-it-blocks.md`](when-it-blocks.md) | every refusal the framework produces, what each means, and the remedy | when something stops |

---

## `install.md`

**Installing, updating and removing.** Two halves have to be present and they fail differently: the
plugin carries the skills and both hooks, the `sdlc` binary is the gate those call. Without the plugin
nothing asks for a cycle; without the binary every skill's first command fails.

It opens with a table of what each half is and what missing it means, then covers the local-directory
source for changing the framework itself, the Copilot path, and what a correct install reports — a hook
count, a skill count, a version. **Those numbers are the verification step**, which is why a change to
what an install produces belongs in this guide and in its table at the top, not only in the prose.

What it does not cover: adopting a repository. Installing governs nothing, and adoption is per
repository — that is `first-cycle.md`'s first step.

## `first-cycle.md`

**One cycle, every stage, end to end.** Fourteen steps from an empty directory to an open pull request,
and what each stage refuses if you arrive at it early.

This is the guide to read when you want to know what using the framework actually feels like: where you
are asked something, where you wait, where you approve, and which of the three planning documents is
being argued with at any moment. It assumes both halves are installed and says so.

What it does not cover: why the flow is shaped this way. That is `../../CONCEPT.md`, and the reasoning
is a different question from the walkthrough.

## `cycle-commands.md`

**Every command for one cycle, in order, on one screen.** The reference you keep open rather than read.

Each entry says whether the binary or an agent runs it, because that split is the one people get wrong:
**a command is deterministic and the binary owns it; a skill needs language and an agent runs it.** The
binary never writes a planning document and a skill never decides a gate.

What it does not cover: what each skill is, who starts it, and what it refuses. That is
[`../../skills/README.md`](../../skills/README.md), and this one is the order they go in.

## `when-it-blocks.md`

**Every refusal, what it means, and what to do next.** Indexed so that a message can be looked up rather
than puzzled over.

It leads with the distinction the whole document rests on: **a refusal is the gate working, a failure is
the gate broken**, and those are different exit codes on purpose so that a hook never reports its own bug
as a governed stop. It also states the holes — what the hook denies today and what it does not — because
a directory that appears enforced and is not is worse than one that never pretended.

What it does not cover: the rules themselves. `sdlc constraints` renders those, all tiers resolved, and a
copy here would be a second place they live.

---

## What these are not

They describe the framework from outside: what to type, what it will say, and what to do when it refuses.
**None of them states a rule.** The rules are rendered by `sdlc constraints`; the skills' surface is
[`../../skills/README.md`](../../skills/README.md); the design and every decision behind it is
[`../../CONCEPT.md`](../../CONCEPT.md).

Where a guide says something has not been exercised, that is load-bearing and is not to be tidied away.
