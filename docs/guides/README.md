# The guides

Six documents, written for somebody using the framework rather than changing it. This file is the index:
what each one covers, when you would read it, and what it deliberately leaves to another.

**They are in the order a person meets them.** Install once per machine, walk one cycle once, then keep
the command sheet open and the refusal index to hand. Nothing here needs reading front to back twice.

| Guide | Covers | Read it |
|---|---|---|
| [`install.md`](install.md) | the plugin, the binary, updating, removing, and the four things that catch people | once per machine, before anything else |
| [`first-cycle.md`](first-cycle.md) | one cycle from an empty directory to a pull request, stage by stage | once, front to back |
| [`dice-worked-example.md`](dice-worked-example.md) | that same cycle with one small solution in it: every command, and the answer to give at every prompt | alongside your first cycle, if you want something that runs at the end |
| [`cycle-commands.md`](cycle-commands.md) | the short path from an unadopted repository to a pull request, then every command for one cycle on one screen | while working |
| [`when-it-blocks.md`](when-it-blocks.md) | every refusal the framework produces, what each means, and the remedy | when something stops |
| [`the-stack.md`](the-stack.md) | what a solution is built from: the declared stack, the stack rules it switches on, the build settings files it takes, and how to change any of them | when declaring the stack, when a default or a rule is wrong for this solution, or when taking the .NET build settings into an existing codebase |

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

**One cycle, every stage, end to end.** From an empty directory to an open pull request, and what each
stage refuses if you arrive at it early.

This is the guide to read when you want to know what using the framework actually feels like: where you
are asked something, where you wait, where you approve, and which of the three planning documents is
being argued with at any moment. It assumes both halves are installed and says so.

What it does not cover: why the flow is shaped this way. That is `../../CONCEPT.md`, and the reasoning
is a different question from the walkthrough. It is also solution-agnostic on purpose — what to *answer*
at each prompt is the worked example's job.

## `dice-worked-example.md`

**The same walk with one solution in it, command by command.** A six-sided die rolled over HTTP, from a
new directory to something that answers a request, with the answer to give at every interview and the
four criteria the work comes to.

It exists because a solution-agnostic walkthrough tells you where you will be asked something and not
what to say, and the gap between those is where a first cycle stalls. **Every command is there to be
typed**, including the ones a skill runs for you, because what those record is what the later stages
read.

**The solution is local** — no remote, no pipeline, no pull request — so the walk stays on one machine,
and the guide says what that costs: the pre-push hook is then the only mechanical layer. Giving it a
remote afterwards is its last section.

What it does not cover: the blocked paths. It is the happy path deliberately — a refine after a blocking
finding and a remediate after a blocked verification get a second example rather than a bigger dice
program. It also states no rule; it reads them like everything else.

## `cycle-commands.md`

**Every command for one cycle, in order, on one screen.** The reference you keep open rather than read.

**It opens with the short path** — fourteen numbered lines from an unadopted repository to an open pull
request, including the four setup steps that are easiest to skip and cost the most. That is the one to
hand somebody, or to scan when you know the flow and want the next command rather than the reason for it.

Each entry below it says whether the binary or an agent runs it, because that split is the one people get
wrong: **a command is deterministic and the binary owns it; a skill needs language and an agent runs it.**
The binary never writes a planning document and a skill never decides a gate.

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

## `the-stack.md`

**What a solution is built from, and how to change it.** The declared stack and who owns it, its three
tables — the choices it pins, the stack rules it switches on and the build settings files it takes —
where the first draft comes from now that adoption writes none, what an empty one means, the draft and
the menu that go beside it, what a profile still is, how a build settings file reaches a repository
through a cycle, what `sdlc stack` shows, and what `sdlc doctor` says about all of it.

It is reference rather than a walkthrough — the one guide written to be read in parts. It exists because
the declared stack is the second rule source every plan is read against and was the only one with no
document of its own: what the shipped profiles declare was readable in the framework's source and
nowhere else.

What it does not cover: the rules themselves. `sdlc constraints` renders those, and this guide states
none — including the one about departing from a declared default, which it names and leaves to the
command. The reasoning behind a declared stack that lists its own rules is `../../CONCEPT.md` D-76,
and behind one that lists its build settings D-77, both cited rather than repeated.

---

## What these are not

They describe the framework from outside: what to type, what it will say, and what to do when it refuses.
**None of them states a rule.** The rules are rendered by `sdlc constraints`; the skills' surface is
[`../../skills/README.md`](../../skills/README.md); the design and every decision behind it is
[`../../CONCEPT.md`](../../CONCEPT.md).

Where a guide says something has not been exercised, that is load-bearing and is not to be tidied away.
