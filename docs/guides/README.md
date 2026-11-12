# The guides

Ten guides, each answering one task. Nine are for somebody using the framework; the last is for somebody
changing it.
This file is the index: what each one covers, when you would read it, and what it leaves to another.

**They are in the order a person meets them.** Install once per machine, initialize and declare once per
repository, then run cycles, mine them once they finish — and keep the two `handling-` guides to hand for when a cycle stops. **If you want the
whole thing end to end with a real solution in it**, `../samples/rolling-a-die.md` is that walk.

| Guide | Covers | Read it |
|---|---|---|
| [`installing-the-plugin.md`](installing-the-plugin.md) | the plugin and the binary: installing, updating, removing, on each host, and the four things that catch people | once per machine, before anything else |
| [`adopting-the-framework.md`](adopting-the-framework.md) | adoption, the interview, filling in, placing and committing the drafts it leaves, the first constraints, and the files your solution owns | once per repository |
| [`declaring-the-stack.md`](declaring-the-stack.md) | what a solution is built from: the declared stack, the stack rules it switches on, the build settings files it takes, and how to change any of them | right after adoption, and whenever a default or a rule is wrong for this solution |
| [`running-a-cycle.md`](running-a-cycle.md) | one cycle in seven steps, from an intent to a pull request, each one said in a sentence — the happy path | once, front to back, then when you need a stage again |
| [`setting-up-the-factory.md`](setting-up-the-factory.md) | the factory from nothing to a watched first run: the agent's credential, the tools on the agent, the integration branch, the pipeline and its permissions, and what to do when a step fails | once per solution, when you are ready to let a pipeline build |
| [`running-the-factory.md`](running-the-factory.md) | many cycles built overnight: the integration branch, admitting plans, the factory's pipeline, and the morning's sitrep, merges and conflicts | every day the factory runs |
| [`mining-the-cycles.md`](mining-the-cycles.md) | after a cycle finishes: reading what the cycles settled, deciding the rules, decisions, stack rows, areas and successors the solution keeps, answering every open suggestion — taken further, declined or ignored — and deleting the finished cycles' folders | once a verify passes, on the cycle's branch or after the merge, or after several cycles in one sitting |
| [`handling-blockers.md`](handling-blockers.md) | a blocking finding on a document, or a blocked verification: what each is and how it is answered | when a loop hands back, or a verification says `blocked` |
| [`handling-refusals.md`](handling-refusals.md) | every refusal, hook denial and turn sent back, with the remedy for each | when something stops |
| [`testing-a-local-build.md`](testing-a-local-build.md) | a cycle run end to end on a build of the framework nobody published: switching it on, what stops it at the pull request, and shipping the change for real | when you are changing ai-sdlc itself and want to watch a cycle go through the change |

`../cheat-sheet.md` is a cycle as one line per step, and `../reference/README.md` is every command,
skill and refusal as one line each, for when you already know why.

---

## `installing-the-plugin.md`

**Installing, updating and removing both halves.** The plugin carries the skills and the hooks; the
`sdlc` binary is what those call, and a solution pins which release of it runs there. Without the plugin nothing asks for a cycle; without the binary
every skill's first command fails.

It opens with a table of what each half is and what missing it means, then covers the local-directory
source for changing the framework itself, each host's path, and what a correct install reports — a hook
count, a skill count, a version. **Those numbers are the verification step**, which is why a change to
what an install produces belongs in this guide and in its table at the top, not only in the prose.

What it does not cover: adopting the framework. Installing governs nothing, and adoption is per
repository — that is the next guide.

## `adopting-the-framework.md`

**Putting a repository under the framework.** The init skill and the `sdlc init` command, which do
opposite halves of the job; what the command writes, commits and drafts, and the integration branch it
creates; the interview and where each answer goes; filling in the three drafts, renaming them into place
and committing them; making the pipeline's check required; and opening the first cycle.

It closes with **what your solution owns**: the seven files only a person writes, what each holds, when it
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

**One cycle, seven steps, end to end.** Begin an intent, take it to plan, steer the plan, approve, build
it — which verifies — fix it, and open the pull request, then what to look at afterwards. Each step is
something a person can say; the slash form is beside it.

It is the happy path on purpose. Where a stage can stop, it says so in a sentence and points at the next
two guides. It is also solution-agnostic — what to *answer* at each prompt is the sample's job.

## `setting-up-the-factory.md`

**The factory, set up once, step by step.** Choosing how the agent signs in — an API key or a subscription
token, and what each costs — getting `sdlc` and Claude Code onto the agent, or using your own agent that already has them, pushing and protecting the
integration branch adoption created, writing and committing the pipeline before the branch is protected, the pipeline's
secret, permissions and lock on Azure DevOps or its secrets on GitHub, and a first run watched against a
throwaway cycle, with what each step looks like when it passes. It ends with a table of failures, each
with its cause and its step.

What it does not cover: the daily routine once it runs, which is `running-the-factory.md`.

## `running-the-factory.md`

**Many cycles, built overnight.** The integration branch, admitting a plan to it with `pr --plan`, what
the factory's pipeline does, and the morning: reading the sitrep, merging one pull request at a time, resolving a conflict with
`resolve-conflict`, picking up a draft. It closes with a fix found on a release.

What it does not cover: setting it up, which is `setting-up-the-factory.md`; the stages themselves, which are
`running-a-cycle.md`; or promotion to main, which is the team's process and not the framework's.

## `mining-the-cycles.md`

**What a solution keeps from its finished cycles.** When to mine and when not to, a sitting step by step — the
report, the proposal file, the drafts, the two reviews, the renames, the record and the husks' deletion — what
each section of the proposal file holds and what places it, and how a suggestion is answered: taken further as a
successor, declined with a reason, or ignored and tallied by concern until it reaches the bar. It closes with what
refuses.

What it does not cover: the skill's every input and output, which is `../../skills/README.md`; or a document's
rounds settling inside a cycle, which is `handling-blockers.md` — the word *converge* now means only that.

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

`../reference/refusals.md` is the same list as one line each.

## `testing-a-local-build.md`

**A cycle run end to end on a change to the framework that nobody has published.** Building the change into a
folder of its own, or taking the package a pull request built; switching it on with `SDLC_DEV_BUILD` for one
terminal; starting the session; why `/sdlc:pr` refuses the cycle; abandoning it; and shipping the change as a
preview and running the work again on it. It closes with pinning a local pack in a sandbox solution, and why
never in a real one.

What it does not cover: installing a published release, which is `installing-the-plugin.md`.

## What these are not

They describe the framework from outside: what to type, what it will say, and what to do when it refuses.
**None of them explains a mechanism for its own sake** — a page that says how something works and asks
nothing of you is a mechanism page, in [`../mechanisms/README.md`](../mechanisms/README.md). **None of them states a rule.** The rules are rendered by `sdlc constraints`; the skills' surface is
[`../../skills/README.md`](../../skills/README.md); the design and every decision behind it is
[`../../CONCEPT.md`](../../CONCEPT.md).

**None of them reports where the project stands.** What has and has not been exercised, and what changed
in which release, belong to the Where it stands section of [`../../README.md`](../../README.md) and the
decisions in `CONCEPT.md`. A guide says how a thing works and what to do, as true on the day it is read
as on the day it was written.

## See also

- [The reference](../reference/README.md) — every command, skill and refusal, one line each
- [The samples](../samples/README.md) — one solution, every command
- [The mechanisms](../mechanisms/README.md) — how one works and why, one page each
- [The components](../README.md) — every concept the framework names, defined once
- [The skills reference](../../skills/README.md)
