# The guides

Six guides, written for somebody using the framework rather than changing it, each answering one task.
This file is the index: what each one covers, when you would read it, and what it leaves to another.

**They are in the order a person meets them.** Install once per machine, initialize and declare once per
repository, then run cycles — and keep the last two to hand for when a cycle stops. **If you want the
whole thing end to end with a real solution in it**, `../samples/rolling-a-die.md` is that walk.

| Guide | Covers | Read it |
|---|---|---|
| [`installing-the-plugin.md`](installing-the-plugin.md) | the plugin and the binary: installing, updating, removing, on each host, and the four things that catch people | once per machine, before anything else |
| [`initializing-the-codebase.md`](initializing-the-codebase.md) | adoption, the interview, placing `solution.md`, the first constraints, and the files your solution owns | once per repository |
| [`declaring-the-stack.md`](declaring-the-stack.md) | what a solution is built from: the declared stack, the stack rules it switches on, the build settings files it takes, and how to change any of them | right after adoption, and whenever a default or a rule is wrong for this solution |
| [`running-a-cycle.md`](running-a-cycle.md) | one cycle from the rules to a pull request, stage by stage — the happy path | once, front to back, then when you need a stage again |
| [`handling-blockers.md`](handling-blockers.md) | a blocking finding on a document, or a blocked verification: what each is and how it is answered | when a loop hands back, or a verification says `blocked` |
| [`handling-refusals.md`](handling-refusals.md) | every refusal, hook denial and turn sent back, with the remedy for each | when something stops |

The cheat sheets in `../cheat-sheets/README.md` are these guides as one line per thing, for when you
already know why.

---

## `installing-the-plugin.md`

**Installing, updating and removing both halves.** The plugin carries the skills and both hooks; the
`sdlc` binary is the gate those call. Without the plugin nothing asks for a cycle; without the binary
every skill's first command fails.

It opens with a table of what each half is and what missing it means, then covers the local-directory
source for changing the framework itself, each host's path, and what a correct install reports — a hook
count, a skill count, a version. **Those numbers are the verification step**, which is why a change to
what an install produces belongs in this guide and in its table at the top, not only in the prose.

What it does not cover: adopting a repository. Installing governs nothing, and adoption is per
repository — that is the next guide.

## `initializing-the-codebase.md`

**Putting a repository under the framework.** The init skill and the `sdlc init` command, which do
opposite halves of the job; what the command writes and commits; making the pipeline's check required;
the interview and where each answer goes; placing `solution.md`; and proposing the first constraints, or
none.

It closes with **what your solution owns**: the six files only a person writes, what each holds, when it
changes and which command drafts or proposes it — and the theme, which is the one solution file a cycle
may change.

What it does not cover: the declared stack beyond placing it, which is the next guide.

## `declaring-the-stack.md`

**What a solution is built from.** `techstack.md` and its three tables — defaults, stack rules, build
settings — what a row means and what no row means, where the first draft comes from, how to change it,
the options menu, what a profile is, and what `sdlc stack` and `sdlc doctor` say about it.

It is the one guide you come back to outside a cycle: read the part you need. It states no rule;
`sdlc constraints` renders those.

## `running-a-cycle.md`

**One cycle, every stage, end to end.** Reading the rules, opening the cycle and its branch, the three
planning documents, the gate, build, verify, the pull request, and what to look at afterwards.

It is the happy path on purpose. Where a stage can stop, it says so in a sentence and points at the next
two guides. It is also solution-agnostic — what to *answer* at each prompt is the sample's job.

## `handling-blockers.md`

**When the framework finds something wrong.** A blocking finding on an intent, spec or plan, and a
`blocked` verification on a build: where each comes from, what it stops, how refine and remediate answer
it, the loop and when it hands back, a stale round, steering, reopening an approval, escalation, and why
suggestions are not blockers.

What it does not cover: refusals in general. A blocker causes some refusals; it is not one.

## `handling-refusals.md`

**Every refusal the framework produces**, grouped by what refuses — `sdlc new`, the gate, scaffold,
evidence, `pr-ready`, the pre-tool hook and the turn-end hook — with what each means and the next
command. It opens with the exit codes, because a refusal and a failure are different on purpose, and
closes with the cases where nothing refuses and it should have.

`../cheat-sheets/refusals.md` is the same list as one line each.

## What these are not

They describe the framework from outside: what to type, what it will say, and what to do when it refuses.
**None of them states a rule.** The rules are rendered by `sdlc constraints`; the skills' surface is
[`../../skills/README.md`](../../skills/README.md); the design and every decision behind it is
[`../../CONCEPT.md`](../../CONCEPT.md).

Where a guide says something has not been exercised, that is load-bearing and is not to be tidied away.
