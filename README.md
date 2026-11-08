# ai-sdlc

[![release](https://img.shields.io/nuget/v/MofonCode.Sdlc.Cli?label=release)](https://www.nuget.org/packages/MofonCode.Sdlc.Cli)
[![preview](https://img.shields.io/nuget/vpre/MofonCode.Sdlc.Cli?label=preview&color=orange)](https://www.nuget.org/packages/MofonCode.Sdlc.Cli#versions-body-tab)
[![downloads](https://img.shields.io/nuget/dt/MofonCode.Sdlc.Cli)](https://www.nuget.org/packages/MofonCode.Sdlc.Cli)
[![ci](https://img.shields.io/github/actions/workflow/status/MofonCode/ai-sdlc/ci.yml?branch=main&label=ci)](https://github.com/MofonCode/ai-sdlc/actions/workflows/ci.yml)
[![license](https://img.shields.io/github/license/MofonCode/ai-sdlc)](LICENSE)

**Version 0.38.0** · iteration 4 · .NET 10

**A plugin that makes an AI coding agent work the way a careful team does** — write down what the change
is for, agree what *done* means, plan it, have a person approve it, and only then write code. **And it
keeps a ledger of what was decided**, so the next session, and the next model, builds on those decisions
rather than quietly undoing them.

Most agent workflows go from a sentence straight to a diff. This one does not. A change moves through six
stages, each producing a file the next stage reads, and **each file is challenged by a fresh reader
against a fixed set of rules before it advances.** Nothing advances because something said it was fine.

> **Is this for me?** If what you want is an agent that moves faster, no. If you have ever read an
> agent's pull request and been unable to tell what it was asked for, what it decided by itself, and
> what proves it works — yes.

## Why use it

An agent can take a sentence to a merged diff in minutes, and nothing along the way records what the change
was for, what *done* meant, what the agent decided by itself, or who agreed to any of it. The code may well
be good. **The trouble is that nobody can tell**, and a reviewer reading the diff afterwards is
reconstructing intent from its result.

ai-sdlc makes the agent write those things down *before* the code, has each one challenged by a reader with
no stake in it, has a person sign off on them, and refuses to merge anything that lacks them. **Everything
that can be decided by a program is decided by one** — the `sdlc` binary, called from the agent's hooks and
from CI — because an instruction the agent reads is advisory, and the agent will talk its way past it.

**The decisions are what AI development loses first.** A choice settled in one conversation is gone by the
next session; a different thread, or a different model, argues it again from nothing, or simply takes the
alternative somebody already weighed and rejected, and nothing in the diff says so. ai-sdlc keeps
those choices in a ledger rather than in anybody's memory: `decisions.md`, the solution's standing record of
what it chose and what it chose *instead of*, which a person keeps. **Every stage works from it.** The
intent interview reads it before asking anything, so it does not re-ask what is settled, and when you ask
for something a decision set aside it tells you then, in a sentence, and asks whether to keep the decision
or depart from it. The spec, the plan and the build read it too, and **a fresh reader holds each of them,
and the code itself, against it**: anything that contradicts a decision is questioned, not let through. A
cycle may depart from a decision, but it declares the departure once, where it arises, with a name and a
reason on it — and you are told, at the approval, beside the gate and in the pull request. Once the cycle
is approved, `/sdlc:propose` offers each departure as an update to the record, and the choices the cycle
settled as new entries, so the ledger keeps saying what the solution does. What was decided three sessions
ago, in another thread, by another model, is still what the code does — or the change says plainly why
not.

## Features

Each one names the problem it exists for. [Where it stands](#where-it-stands) says which have met real
cycles and which only fixtures.

### Planning the change

| Feature | The problem | What it does |
|---|---|---|
| **Intent interview** | The agent starts coding from one sentence, and the assumptions it filled in never surface | `/sdlc:intent` interviews a person into *why* the change exists. Choices already made are recorded with the person's name on them, and anything nobody can answer yet goes under `## Open` rather than being guessed |
| **Slicing** | A large piece of work becomes one sprawling pull request nobody can review | Before any cycle opens, `intent` proposes cutting the work into pieces that can each be built and merged on their own. A piece that needs another's code waits as a *successor* and reports itself ready when that code merges |
| **Spec with cases** | *Done* is vague, so edge cases get argued at review — after the code exists | Every criterion can fail, and lists the cases it covers. The list closes the criterion: a listed case left unmet blocks, and a case nobody listed can only be a suggestion later |
| **Plan with proofs** | Tests pass and prove nothing about what was asked | The plan names the test that proves each criterion and says what files it will touch. Where it rests on how a platform behaves, a throwaway *probe* makes that behaviour happen first, and the plan quotes what it showed |
| **Steer** | Correcting direction halfway means regenerating the documents and losing what was settled | `/sdlc:steer` carries a correction down the chain, editing the intent, spec and plan rather than rewriting them, and has everything downstream of the edit read again |

### Challenging it

| Feature | The problem | What it does |
|---|---|---|
| **Interrogator** | The agent that wrote a document reviews it, and agrees with itself | `/sdlc:interrogate` has a fresh process — no memory of writing it, no tools — read the document against the rules in force. It judges and never edits; `/sdlc:refine` edits and never judges. The loop runs until a round raises nothing blocking, and a rule answered then raised again is reported as going backwards |
| **Findings** | Review comments are vague, cite nothing, and quietly get dismissed | Every finding cites one rule by id and says where, what and the remedy. **A blocking one cannot be waived by any mechanism** — it is answered, or the cycle does not advance. Each artifact's rounds are one ledger chained by hash, so the argument behind an approval cannot be rewritten |
| **Suggestions** | Good observations that block nothing are lost the moment the session ends | Non-blocking findings go to a ledger, each concern once. `/sdlc:suggestions` lists what is still open across the solution, what became of each, and any of them can start the next cycle |
| **Perspectives** | Security, operability, maintainability and accessibility are considered only if somebody happens to think of them | Each perspective steers the author with a short list at every stage and judges in a reader of its own. Only a finding citing a rule it lists blocks; the rest arrive as suggestions under its name |

### Remembering what was settled

| Feature | The problem | What it does |
|---|---|---|
| **Constraints** | A rule written into a prompt is forgotten next session, and the agent bends it when it is inconvenient | Rules have ids and four clauses, including what each one **forbids**. Three tiers — global, the stack's, and the solution's own — are resolved and rendered fresh into every reading, never copied into a document where they go stale |
| **Decisions charter** | Each new session re-argues choices made weeks ago, unless somebody remembers to prompt it | `decisions.md` records what the solution chose and what it chose *instead of*. Every stage reads it without anybody asking, and every reader is handed it — the intent's, the spec's, the plan's, and the one that reads the code at verify. A cycle may depart from a decision, but not silently: it declares the departure where it arises, and the person is shown it at the approval, in `sdlc status` and in the pull request |
| **Stack definition** | The agent picks frameworks and packages ad hoc, and every cycle drifts a little | `techstack.md` declares what the solution is built from, which stack rules hold it, and which build settings it takes. What is in the file is exactly what binds, and a plan is read against it |
| **Propose** | The same mistake recurs across cycles, and nobody turns it into a rule | `/sdlc:propose` reads the findings for the rule a solution keeps reaching for, and approved cycles for the choices they settled, and argues each as an entry a person may adopt |

### Approving and building it

| Feature | The problem | What it does |
|---|---|---|
| **Hash-bound gate** | Approval is a "looks good" in chat, and the plan changes afterwards | A person approves all three planning documents together, and the approval records a hash of each. Edit one afterwards and the gate refuses until it is approved again. On by default; a team may declare it off, and `sdlc settings` shows which it is |
| **Protected files** | An agent edits its own approval, or the rules it is judged by | Six files — the approval, an abandonment, the solution's rules, its decisions, its stack and what it is — are written only by a person. The pre-tool hook denies every agent write to them, by path and by shell command alike |
| **Recorded evidence** | "The tests pass" is a claim, and the turn ends on a red build | The build records the diff, a normalised test summary and coverage. The turn-end hook sends the turn back while the suite has a failure or the evidence is missing or stale |
| **Verify** | A reviewer re-reads the code and trusts the green tick | A fresh process that did not see the build judges it from the recorded evidence alone, criterion by criterion: did the named test run, pass, and actually establish the criterion? Beside it, another reads the change itself for anything the plan did not describe or a recorded decision set aside |
| **Bounded remediation** | A fix loop either thrashes forever or gives up after an arbitrary count | `/sdlc:remediate` runs while each pass reduces what is open. When a pass undoes an earlier answer, or two reduce nothing, the cycle escalates to a person and never opens a pull request |

### Merging and running

| Feature | The problem | What it does |
|---|---|---|
| **Chain check** | The process is skipped and the change merged anyway | `sdlc verify-chain` runs as a required pull-request check and refuses a change whose chain is not complete, approved and hash-matched, or whose new lines are under 70% covered |
| **The pull request from the cycle** | A pull request description is a summary somebody invented | The body is the cycle itself: the problem in the intent's words, each criterion with its verdict, the risk tier, and anything still open |
| **Batch build and the factory** | Approved work sits waiting for somebody to supervise each build | `/sdlc:batch-build` builds every approved cycle on its own branch; the factory runs it unattended whenever you start it and **never merges**. The sitrep it ends on says what is ready, in what order, and what needs a person |
| **Leads** | Work arrives in a tracker and is retyped into a prompt | `/sdlc:intent --from` starts from an Azure DevOps or GitHub work item, a suggestion or a ready successor. Operate reads production's errors and files them as leads |
| **Init** | Bringing governance into a repository is a week of setup nobody finishes | `/sdlc:init` interviews the team about what matters here and proposes its first rules. Adoption writes the hooks for every supported agent and the pipeline, drafts the files only a person writes, and creates the integration branch |
| **Pinned release** | Upgrading the tool halfway through a change moves the rules under it, and moving to a new release costs a whole cycle about nothing | A solution pins its release, and a cycle runs on the one it opened on — the binary, its skills and its hooks together. Moving the pin needs no cycle: the pull-request check writes the framework's files again as the new release and merges the upgrade only if they match |
| **More than one agent** | Governance written for one agent does not survive a teammate using another | One hook adapter speaks Claude Code, GitHub Copilot and Antigravity, and the merge check depends on none of them |

## What it costs

Honestly: **more time and more tokens per change**, by design. Every change runs the whole chain, small
ones included, and each planning document is read until a reading raises nothing blocking. The tool is a
.NET executable, the stack rules it ships are .NET only, and Claude Code is the only host it has been run
on in earnest. If you want an agent that moves faster, this is the wrong tool. If you want to be able to
trust and audit what an agent merged, that cost is the point.

## How it works

```
+----- steer -----+
v                 |
intent -> spec -> plan -> build -> verify -> pr
                            ^         |
                            +- remediate (while it progresses) -+
```

| Stage | Produces | The question it answers |
|---|---|---|
| **intent** | `intent.md` | why does this change exist? |
| **spec** | `spec.md` | what does done mean, in criteria that can fail? |
| **plan** | `plan.md` | how will it be met, and what will prove each criterion? |
| **build** | code, and recorded evidence | the plan implemented, with the diff and the test output saved |
| **verify** | `verify.md` | does that evidence actually show each criterion met? |
| **pr** | a pull request | a person reviews it and merges |

**The three planning documents are always required.** Each is written, then read by a **fresh process
that never saw it being made** — so it has nothing to defend — and revised until a reading raises nothing
blocking. A stage will not run until the document before it has had such a reading.

**There is exactly one gate.** A person approves all three documents together, immediately before the
build, and the approval records a hash of each. Change any of them afterwards and the gate refuses until
they are approved again. **A team may declare that no approval is required** — `approval: none` in
`solution.md`, which only a person writes. The gate then keeps every other check, a blocking finding still
stops the build, and every report says no person approved; the default is `required`
([running a cycle](docs/guides/running-a-cycle.md), under *Running without approvals*).

**Two loops, one on each side of that gate.** Neither is open-ended:

- **`/sdlc:steer`** carries a correction you make mid-cycle back down the chain — *use the existing cache
  abstraction rather than a new one* — editing the intent, spec and plan rather than regenerating them,
  and re-reading everything downstream of what it touched. It changes **how, not what**: a correction that
  changes the problem is a different cycle, and it will tell you so. It refuses once you have approved,
  because the plan is hash-locked from that moment.
- **`/sdlc:remediate`** fixes code and tests against a verification that failed, and runs **while each
  pass reduces what is open**. It stops when a pass undoes what an earlier one answered, or when two in
  a row reduce nothing — then the cycle escalates rather than thrashing, which is the only reason an
  unattended run is safe to start. **The bound is on progress, not on passes** (D-66): a blind count of
  three failed an honest cycle that needed a fourth and spent all three on one that was going nowhere.

## What stops an agent talking its way past it

Three layers, and only the last is a guarantee:

| Layer | What it is | Who can get past it |
|---|---|---|
| advisory | the instructions the agent reads | the agent, constantly |
| local | the `sdlc` binary, a pre-tool hook, a turn-end hook | anyone with a shell |
| **non-bypassable** | `sdlc verify-chain` as a pull-request check | nobody without repository admin |

The promise made to a team is the third one. The local layer exists to make it cheap to satisfy, not to
be the proof.

**And six files only a person ever writes:** `approval.md` the gate decision, `abandoned.md` why a cycle
stopped, `constraints.md` the solution's own rules, `decisions.md` the choices it has settled,
`solution.md` what it is, and `techstack.md` what it is built from. A tool prints what belongs in them
and a person puts it there. Every agent write to all six is denied at the tool call, by file path and by
shell command alike, with no exception and no flag to carve one out of.

**A blocking objection cannot be waived by any mechanism.** It is answered, or the cycle does not
advance.

One rule the project holds itself to: **a thing that appears enforced and is not is worse than one that
never pretended.** That is why [Where it stands](#where-it-stands) says plainly what has not been
exercised.

## Getting started

Two halves have to be present and they fail differently. The **plugin** carries the skills and the hooks;
the **`sdlc` binary** is the gate they call. Without the plugin nothing asks for a cycle; without the
binary every skill's first command fails.

### Prerequisites

- the .NET 10 SDK, to pack and install the binary
- git
- an agent host: Claude Code is shown below, and [Will it work with my agent?](#will-it-work-with-my-agent)
  lists the others

**1. Install the binary.** From nuget.org, once per machine. A governed solution pins its own release, and
this `sdlc` hands each command on to it:

```pwsh
dotnet tool install --global MofonCode.Sdlc.Cli
```

Until the first release reaches nuget.org, pack it from a clone instead —
[the install guide](docs/guides/installing-the-plugin.md#the-binary) has the three commands.

**2. Install the plugin.** Claude Code shown:

```pwsh
claude plugin marketplace add MofonCode/ai-sdlc
claude plugin install sdlc@ai-sdlc
```

**3. Check it worked.** From a directory that is *not* a clone of this repository:

```pwsh
sdlc constraints
```

It should print the nineteen global constraints in force and say it read them from
`global  embedded in sdlc 0.38.0`. **Use this rather than `sdlc help` as the smoke test** — `help` and
`version` are the only commands that need nothing from the installation, so they are the two that cannot
tell you whether it worked. Then `claude plugin details sdlc@ai-sdlc` should say `Skills (18)` and
`Hooks (3)`: **a hook count below three is a failed install**, not a detail, because the deterministic layer
is simply not loaded and every cycle will still appear to work.

**4. Adopt a repository.** Installing governs nothing — the framework reaches a repository when a person
adopts it there, one repository at a time. Start a new agent session first, because skills load at
startup. Once a repository is adopted, start sessions in it with `sdlc claude`: it loads the skills of the
release the repository pins rather than the ones installed.

```
/sdlc:init
```

**Start with the skill.** It asks where the code will live, then hands you the adoption command to run
yourself — a person runs that and an agent cannot, because adoption is a decision about how a team works
and it writes the hook configuration that constrains the agent. The order matters: the file recording
that answer is protected once it is placed, so answering afterwards costs a draft and a rename.

**The command commits what it writes, creates the integration branch, `development`, and switches to
it.** It also drafts the three files only a person writes, beside where each goes, and places none of
them — each says at its top that an agent cannot change it.

**Then fill in the drafts, rename them, and commit them.** Adoption's report ends with exactly that,
and `sdlc doctor` prints the same block whenever you ask:

| Draft | You write |
|---|---|
| `.sdlc/solution.draft.md` | a paragraph saying what the solution is and who it is for, in place of the comment |
| `.sdlc/techstack.draft.md` | the framework's default stack cut down to what is true here — `none` in a row that is not. What is in it is exactly what binds, and no cycle opens until it is placed |
| `.sdlc/constraints.draft.md` | this solution's own rules, if the interview found any. Optional |

```sh
mv -f .sdlc/solution.draft.md .sdlc/solution.md
mv -f .sdlc/techstack.draft.md .sdlc/techstack.md
mv -f .sdlc/constraints.draft.md .sdlc/constraints.md
git add .sdlc && git commit -m "Place the solution and its stack"
```

In PowerShell, `Move-Item -Force` in place of `mv -f`. **A cycle does not open over uncommitted work**,
which is why the commit is a step and not a tidy-up.

**5. Start a cycle.** In your agent, not the shell:

```
/sdlc:intent
```

The skill opens the cycle and switches to its branch itself, then interviews you into the intent —
and waits for your answers, because the intent is written from them. `/sdlc:spec` and `/sdlc:plan`
follow, you approve all three, and then the build runs.

**Want the whole walkthrough, or the variations?**
[`docs/guides/running-a-cycle.md`](docs/guides/running-a-cycle.md) is one cycle from the rules to an
open pull request, and
[`docs/samples/rolling-a-die.md`](docs/samples/rolling-a-die.md) is the whole walk with a six-sided die
in it — every command, no remote required — so there is something to type at every prompt and something
that runs at the end. [`docs/guides/installing-the-plugin.md`](docs/guides/installing-the-plugin.md) has
every install path — Copilot, updating, removing, and the four things that catch people. And if
something refuses, [`docs/guides/handling-refusals.md`](docs/guides/handling-refusals.md) has every
refusal with its remedy.

## Where to read more

**Using it:**

| | |
|---|---|
| [`docs/README.md`](docs/README.md) | **the components** — every concept the framework names, defined once: what it is, what uses it, where it lives |
| [`docs/guides/README.md`](docs/guides/README.md) | **the guides, indexed** — what each covers and when to read it |
| [`docs/guides/installing-the-plugin.md`](docs/guides/installing-the-plugin.md) | installing the plugin and the binary, updating, and removing |
| [`docs/guides/adopting-the-framework.md`](docs/guides/adopting-the-framework.md) | adopting the framework, placing and committing the drafts it leaves, and the files your solution owns |
| [`docs/guides/declaring-the-stack.md`](docs/guides/declaring-the-stack.md) | what a solution is built from — the declared stack, the stack rules it switches on, the build settings files it takes, and how to change any of them |
| [`docs/guides/running-a-cycle.md`](docs/guides/running-a-cycle.md) | one cycle end to end, stage by stage |
| [`docs/guides/setting-up-the-factory.md`](docs/guides/setting-up-the-factory.md) | the factory set up once, step by step, to a watched first run |
| [`docs/guides/running-the-factory.md`](docs/guides/running-the-factory.md) | many cycles built overnight: the integration branch, the factory, and the morning |
| [`docs/guides/handling-blockers.md`](docs/guides/handling-blockers.md) | a blocking finding or a blocked verification, and how each is answered |
| [`docs/guides/handling-refusals.md`](docs/guides/handling-refusals.md) | every refusal, and the remedy for each |
| [`docs/guides/testing-a-local-build.md`](docs/guides/testing-a-local-build.md) | a cycle run end to end on a change to the framework nobody has published, and why it never reaches a pull request |
| [`docs/mechanisms/README.md`](docs/mechanisms/README.md) | **the mechanisms** — how one works and why, one page each |
| [`docs/mechanisms/protected-files.md`](docs/mechanisms/protected-files.md) | the files only a person changes, and how the hook guards them |
| [`docs/mechanisms/perspectives.md`](docs/mechanisms/perspectives.md) | what examines a change beyond the rules, when each perspective steers and judges, and how to read what it found |
| [`docs/cheat-sheet.md`](docs/cheat-sheet.md) | **the cheat sheet** — one cycle, one line per step |
| [`docs/reference/README.md`](docs/reference/README.md) | **the reference** — every command, skill and refusal, one line each |
| [`docs/reference/commands.md`](docs/reference/commands.md) | every command, on one screen |
| [`docs/reference/skills.md`](docs/reference/skills.md) | the nineteen skills, one line each |
| [`docs/reference/refusals.md`](docs/reference/refusals.md) | every refusal, one line each |
| [`docs/samples/rolling-a-die.md`](docs/samples/rolling-a-die.md) | **the whole walk with one solution in it** — a d6 rolled from a page, every command, in a repository with no remote |
| [`skills/README.md`](skills/README.md) | the nineteen skills as a reference: syntax, parameters, output, refusals and remarks for each |
| `sdlc constraints` | the rules in force, all tiers resolved. Run it rather than reading a copy of them |

One more worth knowing about. `.sdlc/README.md`, written into your repository by `sdlc init`, is the whole flow on one page that
travels with your code.

**Understanding or changing it:**

| | |
|---|---|
| [`CONCEPT.md`](CONCEPT.md) | the design, and every decision behind it with what each one rejected |
| [`CONSTRAINTS.md`](CONSTRAINTS.md) | the nineteen global constraints, each with what it forbids and why |
| [`AGENTS.md`](AGENTS.md) | how to work in this repository, written for an agent — and the repository map |
| [`src/README.md`](src/README.md) | what `Sdlc.Core` and `Sdlc.Cli` are, and where a new thing goes |
| [`tools/README.md`](tools/README.md) | the tools that check what the test suite cannot, and the one that measures what sessions cost |

## Will it work with my agent?

Every candidate host can deny a tool call before it runs, so the enforcement primitive is portable. One
adapter normalises the rest: `sdlc hook --host <name>` reads the host's payload, asks the one policy
engine, and answers in that host's own shape.

| Host | Stages | Local enforcement | Exercised |
|---|---|---|---|
| **Claude Code** | `/sdlc:<stage>` | tool calls **and** turn end | **yes** — the host this is developed on |
| GitHub Copilot | asked for in prose | tool calls and turn end | no |
| Antigravity | asked for in prose | tool calls only — **no turn end** | no |

**Off Claude Code a stage is chosen rather than named.** There is no `/sdlc:spec` on the others: they
match your request against each skill's description and ask before activating one, so a stage has to be
asked for in a sentence — with the cycle id in it, because nothing else carries it.

**The merge gate depends on no host.** Whichever agent you use, the pull-request check is what actually
governs the merge, so an unexercised host costs you the local layer rather than the guarantee.

The rules name no language, framework or package, so they travel to any stack. **The tool is a .NET
executable, so the toolchain does not.** `CONCEPT.md` D-46 has how a host is added and D-51 why
Antigravity's turn end is deliberately left ungoverned.

## Where it stands

**The planning half is complete and exercised end to end**: adoption, a cycle, the three documents,
interrogation rounds, a placed approval, recorded evidence, a verification, the chain, and the
pull-request decision.

What is honest to say about the rest:

- **Bounded remediation is implemented and tested, but exercised in fixtures far more than in anger.**
  Everything unattended rests on it, so the first batch runs are worth watching.
- **The stack rules are .NET only and have never judged a real plan.** The framework ships five
  `PRO_` rules, and a solution is held to the ones its declared stack marks `true`. None is written for
  another stack — nothing has been built on one, so a rule there would be a guess with the authority of
  a constraint. The first three were written from convictions this framework has paid for rather than
  from findings a cycle raised, which is the weaker provenance and worth saying. `PRO_04`, an Aspire
  AppHost once a solution runs more than one process, came from a trial cycle that argued its way out
  of one; nothing has yet watched it refuse a plan. **It reaches no solution adopted before 0.28.0**
  until a person adds its row to that solution's `techstack.md`. `PRO_05`, which asks a plan adding
  script to a Blazor page what Blazor already does, came from a real plan whose page script a person
  replaced with the framework's own mechanisms; nothing has yet watched it refuse one either. **It
  reaches no solution adopted before 0.35.0** until its row is added the same way.
- **The upgrade check has not yet passed a real pull request.** A pull request that moves the pin merges
  without a cycle when every framework file is what the pinned release writes, a pipeline compared step by
  step. MarketSense's move to 0.36.4 was refused by 0.36.4's word-for-word pipeline comparison and merged on
  a policy override; 0.36.5's step comparison passes that same change replayed in a copy of the repository,
  and refuses an edited step of its own. Its next upgrade is the first real one.
- **Dependency moves need no cycle, and none has merged that way yet** (0.36.6). A pull request that only moves
  versions in `Directory.Packages.props`, `global.json` or the tool manifest is checked by comparing everything
  else; a package added or removed is still a cycle's. Held by the suite and by MarketSense's ASP.NET Core
  security fix, checked locally and not yet merged.
- **The decisions run through the whole cycle, and nothing real has run that way yet** (0.37.0). Every reader
  of an intent, a spec, a plan and a verify is handed the decision record, a departure is declared where it
  arises and reported at the gate, and a new reader at verify holds the change itself to the plan and the
  decisions. The suite holds each piece. No cycle has been judged this way, so nobody yet knows how often the
  change's reader blocks, or whether its narrow trigger keeps it from adding rounds. A solution receives it when
  it moves its pin, and it is a minor because a round may now block what it passed before: the
  rule against changes outside the plan's purpose reached no reader until this.
- **An intent can be changed and cut after it is written, and nobody has done either for real yet** (0.38.0).
  Before the spec, `/sdlc:steer` takes a correction in words that may add, drop or reword an outcome, and
  `/sdlc:intent --reslice` splits an intent or joins several. Both are skill text; the binary already refused
  the rounds that would let a changed intent leave a stale spec behind. A skill acting on one cycle takes its
  id as optional, and `sdlc cycles --here` names the cycle or lists the ones to ask about.
- **Planning in a sandbox has only met the suite** (0.38.0). The intent skill opens one worktree on
  `sandbox/<id>` for every cycle a piece of work turns into, and `sdlc admit` composes what is approved onto an
  admission branch the check already knows. The commands it prints are held as strings: nobody has yet run a
  sandbox through to an admitted pull request, and `sdlc new` no longer refuses uncommitted work, which the plan
  switch refuses instead.
- **A plan needing another's unbuilt code is parked, and nothing has been parked for real** (0.38.0). `sdlc
  park` runs at the plan and keeps the spec and plan; `sdlc backlog` shows the parked and the successors in the
  waves they come ready in; `sdlc unpark` takes one back once its predecessor merged. The re-reading on unpark is
  where a plan written before its code existed meets that code, and nobody has watched one yet.
- **The factory is started by a person and has not been started that way yet** (0.38.0). Its pipeline has no
  schedule and no Operate step, takes one optional cycle to build alone, and builds the cycle the most
  successors wait on first. A solution with a factory pipeline from an earlier release keeps its schedule
  until somebody runs `sdlc factory` again and commits it; `sdlc doctor` names it a problem until then.
  `sdlc operate` is a command a person runs.
- **Declaring the stack after adoption has not been walked by anybody but its author.** Adoption no
  longer writes the declared stack or the .NET build settings: a person places the first from a draft,
  and the second arrive with the skeleton. The suite holds each step; no real adoption has gone through
  them in order yet.
- **Taking the build settings into an existing codebase has never been done for real.** They are rows
  in the declared stack now, switched off on a repository that held code, and a cycle whose plan
  declares one brings it. The suite holds that `sdlc scaffold` writes exactly what the plan names and
  that `sdlc stack` reports the gap. Nobody has yet run the cycle that switches warnings-as-errors on
  over real code and fixed what it broke, which is the whole of what the route is for.
- **`standard: profile` is named after something no solution records any more.** The key still means
  what it meant — new work follows the framework's conventions rather than the surrounding code — and
  the value was left alone because every existing `solution.md` is protected and already says it.
- **Running without approvals has not met a real cycle** (0.36.10). `approval: none` in `solution.md` lets a
  cycle build once its rounds are clean, and the suite holds that a blocker, an uninterrogated document and a
  document edited after its round still refuse. No solution has run that way yet. Under it, nothing local
  anchors a findings ledger: an agent could rewrite one, and git history and the pull request are what remain
  (`CONCEPT.md` D-137).
- **A local solution has no mechanical check over a merge, and nothing checks a push anywhere.** The
  pre-push hook is gone (`CONCEPT.md` D-78): it was written and never installed, and where it ran it
  refused a push of work in progress. A solution adopted before 0.18.0 still has the file, and a clone
  that set `core.hooksPath` still runs it until somebody deletes the file or unsets the key.
- **A solution adopted before 0.16.0 that never listed its stack rules is held to none from 0.20.0.**
  Its old `profile:` line in `solution.md` is no longer read (`CONCEPT.md` D-81). `sdlc doctor` says
  so; `sdlc draft techstack` and a rename put the rules back.
- **Converging on a cycle's branch has not been walked.** The report's new reading of a cycle's record
  was checked against one solution's history, and `verify-chain` reports the order rather than refusing
  it (D-113); no cycle has yet converged before its pull request. **A cycle opened before 0.32.0 keeps its committed `.trx`
  and `build.log` tracked**: the ignore patterns stop new ones only, and `git rm --cached` removes the old.
- **Adoption drafting all three files and creating the integration branch has been read by its author
  and by nobody else.** The suite holds the drafts, the renames the report names, the commit, the branch
  and the refusal over uncommitted work; no adoption has been walked through it by somebody new.
- **The task lists have not been watched on any host.** `init`, `intent`, `spec`, `plan` and `build`
  each ask the agent to keep a list of the stage's steps where its host has one, and `batch-build` one
  line per cycle (`CONCEPT.md` D-80). The suite holds the wording; whether each host's agent opens
  one, stops a stage's at the hand-back, and leaves an escalated cycle unticked is something only a
  session shows.
- **`pr` and `batch-build` have no command behind the push.** The refusal is in the binary and tested;
  the push itself uses the platform's own CLI.
- **The Copilot install has never been run**, though nothing is guessed any more: the package conforms to
  [Agent Plugins 1.0](https://agent-plugins.org/specification) and the suite asserts that it does, so the
  skills should be discoverable — but nobody has watched them load.
- **The Antigravity adapter has never been run either**, and two of its gaps are the host's: its turn end
  is not governed at all, and its workspace configuration loads only in a folder marked trusted, so on a
  fresh clone the local layer is off until somebody marks it.
- **The Azure DevOps pipeline runs on one real server: MarketSense's.** It checks every pull request
  there, on the factory's agent image. It runs only once a person adds a build validation branch policy —
  Azure Repos ignores a YAML pull-request trigger — and the file, the adoption guide and `sdlc doctor` all
  say so.
- **The binary is not on a feed.** Installing means cloning and packing — the single thing that would
  most improve a teammate's first hour. CI installs the packed tool to a temporary path on every pull
  request; `--global` on a clean machine has not been tried.
- **The factory has run against one solution, MarketSense**, on the agent image in `factory/`
  under a local VM. Its first run built one cycle unattended and opened its pull request, and found
  0.28.1's fix: the verify went uninterrogated because the `claude` the agent starts had no credential.
  Nobody has yet spent a morning on a sitrep of more than one cycle (`CONCEPT.md` §10.4, §10.5).
- **Slicing has not met a real piece of work.** `intent` proposes how to cut work into atomic cycles
  before any opens, and a piece that needs another's code waits as a successor under
  `.sdlc/successors/` (`CONCEPT.md` D-125). The suite holds the successor's readiness, the sibling
  comparison and a slicing's admission check; no slicing has yet been accepted, composed into
  `slices/<slug>/plan` and merged, and nobody has started a successor the morning it came ready. Readiness
  is read from the working copy and the integration branch, so a successor already started in another
  worktree reads `ready` until that cycle's plan merges.
- **The rebuilt patch has not met a real cycle.** `diff.patch` is no longer committed: `sdlc evidence`
  refuses uncommitted code, records `head.sha`, and the chain rebuilds the patch from the two commits
  (`CONCEPT.md` D-124). A real git repository in the suite holds that the rebuilt patch is the recorded
  one; no pull-request check has rebuilt one yet. **A solution's cycles directory gets the ignore line
  the next time a cycle opens**, and a cycle that committed its patch before keeps it.
- **Amending a plan after admission has not met a real cycle.** A reopen once the plan is on the
  integration branch now goes back by a plan pull request of its own, and a build made before it is
  named by the queue, the sitrep and `sdlc status` until a person keeps or deletes it (`CONCEPT.md`
  D-123). The suite holds the comparison and both refusals; no amended plan has gone through a real
  pull request yet, and nothing records which reopens were made the old way, on a build branch.
- **Leads and Operate have only met fakes.** `sdlc lead show` reads work items through `az` and `gh`, and
  `sdlc operate` reads Application Insights through `az` and files through the same CLIs; none of that has
  touched a real tracker or a real telemetry store. Jira is not read yet.
- **0.29.0 changes what a solution has to do, and none of it has met a real cycle yet.** It came from
  reviewing MarketSense's first cycle. A round is written by `sdlc round`, which refuses one out of the
  chain's order, and the hook denies writing one any other way (D-101). `verify-chain` refuses a change
  whose added lines are under 70% covered or that lowers the codebase's coverage, so a build records
  `--coverage` (D-103). The run mode `batch` is now `test`: an AppHost reading `SDLC_RUN_MODE=batch` has
  to read `test`, and the architecture suite a solution was scaffolded with before 0.29.0 does not carry
  the launch-profile test (D-102). `/sdlc:propose convergence` and `sdlc draft convergence` are new
  (D-104), and `/sdlc:intent` plans each cycle in a worktree of its own (D-105). The .NET profile's new
  *fake data and seeding* row reaches no existing stack until a person answers it. 0.30.0 adds host tests
  to the skeleton, which a solution scaffolded earlier does not have, and `doctor` asks the platform
  whether branch policy requires the check (D-106), which has been run against one Azure DevOps
  repository and no GitHub one.
- **The findings ledger has been run on a copy of one real cycle and on nothing live.** From 0.31.0 an
  artifact's rounds are one file, `findings/<artifact>.json`: each finding stored once, each round chained
  by hash, and an approval anchoring the round each ledger had reached (D-107). A cycle still holding round
  files is converted by its next round, with nothing to run. On a copy of MarketSense's first cycle that
  took the planning documents from 17 MB of round files to 1.9 MB of ledger, with the same findings
  standing.
- **One perspective has a rule, and nothing proves a perspective's reader catches what it should.**
  Security, operability and maintainability are written as methods and steer every stage, and
  accessibility does the same for any change touching a user interface. Operability's reader may block on
  `CON_20`, earned on two trial cycles; everything else they find is a suggestion (`CONCEPT.md` §6.5).
  UI/UX and the four others are not written.
- **The readers' prompts are the binary's now, and no live cycle has run through them yet.** `sdlc round
  prompt` writes every reader's prompt and `sdlc round collect` drafts the round from their answers
  (D-117); each reader starts with no tools, no MCP servers and no session file (D-118). Each prompt was
  generated against a real MarketSense cycle and the launch line measured on Windows PowerShell and Git
  Bash, but no reader has judged from them in a cycle. `CON_03`, `CON_21`, `CON_22`, `CON_23` and `CON_16` are
  answered a row per criterion or outcome (D-119), which is meant to bring a plan's rounds down and has
  not been measured doing so. `findings/usage.json` records what each round's readers cost (D-121), and
  `tools/Sdlc.UsageReport` reads whole sessions; which readers run on which rounds, and on which model,
  waits for what they record (`CONCEPT.md` §19).
- **The convergence changes are unreleased and have not run a cycle.** The proof rules block what a
  test never reaches rather than what a reader can imagine (`CON_21` and `CON_22` supersede `CON_06` and
  `CON_08`, D-127); a suggestion may only sharpen an outcome (D-128); a plan probes the platform
  behaviour it rests on (D-129), proves in a table, and a verify is rewritten in place (D-130); each
  round records the release that wrote it (D-131); a criterion lists the cases it covers, so a case nobody
  listed is a suggestion at every later stage (`CON_23`, D-132). They were drawn from one MarketSense cycle of 88
  rounds and six reopens, and the claim that they shorten a cycle is untested. D-128's, D-129's and D-132's shape
  rules hold only cycles opened under them.
- **A cycle runs on the release it opened on.** A solution pins the
  binary in `.config/dotnet-tools.json`, and the global `sdlc` hands every command on to that release. The
  release carries its own skills and hooks, and `sdlc claude` starts a session with them; plain `claude`
  loads the installed plugin, and a session-start hook says when that is another release (D-131).
  `.github/workflows/ci.yml` publishes to nuget.org: a preview from each merge into development that ships, one per patch from
  `0.36.1-preview` on, and a release from each merge to main once a reviewer approves it (D-135).
- **A cycle can run on a change to the framework nobody has published, and stops at the pull request**
  (0.36.7). `SDLC_DEV_BUILD` hands every `sdlc` in a terminal to a local build, which calls itself
  `-local`; a cycle any round of which it wrote is a test run, and `pr-ready` and the pull-request check refuse
  it (D-135). Nobody has run a whole test run through it yet.
- **Branch policy has a default, printed rather than applied, on both platforms** (0.36.8). Pull requests only
  into the integration and default branches, nobody bypassing, the check required on both, squash into one and
  merge commits into the other, and only the integration branch or a `release/*` branch promoting, which the
  pipeline's `sdlc promotion` decides. `sdlc branch-policy` prints the commands and `doctor` reads them back
  (D-136); `--approvals`, `--owner` and `--self-approve` add reviewers, a required approver, and an author
  standing in for one. This repository runs under the GitHub half. **The Azure DevOps commands have never been run**, like the
  rest of that platform's path.

## Not goals

A whole-codebase coverage floor — a change is held to 70% of its own lines and the codebase to its
baseline instead. Making the local approval unforgeable — it cannot be, and the pull
request is where accountability lives. Governing ai-sdlc with ai-sdlc. Slicing work unattended. A device end-to-end layer. `CONCEPT.md` §18 has the full
list with reasons.

## License

MIT. See [LICENSE](LICENSE).
