# ai-sdlc

[![release](https://img.shields.io/nuget/v/MofonCode.Sdlc.Cli?label=release)](https://www.nuget.org/packages/MofonCode.Sdlc.Cli)
[![preview](https://img.shields.io/nuget/vpre/MofonCode.Sdlc.Cli?label=preview&color=orange)](https://www.nuget.org/packages/MofonCode.Sdlc.Cli#versions-body-tab)
[![downloads](https://img.shields.io/nuget/dt/MofonCode.Sdlc.Cli)](https://www.nuget.org/packages/MofonCode.Sdlc.Cli)
[![ci](https://img.shields.io/github/actions/workflow/status/MofonCode/ai-sdlc/ci.yml?branch=main&label=ci)](https://github.com/MofonCode/ai-sdlc/actions/workflows/ci.yml)
[![license](https://img.shields.io/github/license/MofonCode/ai-sdlc)](LICENSE)

**Version 0.47.0** · .NET 10 · [changelog](CHANGELOG.md)

**ai-sdlc holds an AI coding agent, and the people directing it, to what was written down.**

Every change is planned before any code exists, challenged by a reader that never saw it written, approved by a
person against the exact text, and proved from recorded evidence. A pull-request check refuses anything missing a
link. The solution's rules and decisions live in the repository beside its code and carry from one cycle to the
next, so every stage of every change is read against everything settled before it. Whatever contradicts them is
brought into the open, for the person and the model alike, rather than slipping through.

> **Is this for me?** If what you want is an agent that moves faster, no. If you have ever read an
> agent's pull request and been unable to tell what it was asked for, what it decided by itself, and
> what proves it works — yes.

## Why choose it

An agent can take a sentence to a merged diff in minutes, and nothing along the way records what the change was
for, what *done* meant, what the agent decided by itself, or who agreed to any of it. The code may well be good.
**The trouble is that nobody can tell**, and the next session cannot either. These are what ai-sdlc does about
it, most important first.

### 1. Decisions that outlive the conversation

A choice settled in one conversation is gone by the next; a different thread, or a different model, argues it
again from nothing, or quietly takes the alternative somebody already weighed and rejected. **`decisions.md`
keeps those choices in the repository**: what the solution chose, and what it chose *instead of*, written by a
person and versioned with the code. Every stage of every cycle works from it. The intent interview reads it
before asking anything, so it does not re-ask what is settled, and every reader is handed it — the intent's, the
spec's, the plan's, and the one that reads the finished code at verify.

### 2. Rules the agent cannot bend

A rule written into a prompt is forgotten next session and bent when it is inconvenient. Here **a constraint is
a record, not an instruction**: an id, four clauses, and always one saying what it *forbids*. Three tiers — the
framework's global rules, the stack's, and the solution's own in `constraints.md` — are resolved and rendered
fresh into every reading, never copied into a document where they go stale, and every blocking finding cites one by id.
Like the decisions, they live in the repository and bind every cycle until a person changes them.

### 3. Accountability runs both ways

When something contradicts a decision or a rule, it is brought up — to whoever is doing the contradicting.

- **The model.** A reader questions anything that contradicts a recorded decision. A cycle may depart from one,
  but it declares the departure where it arises, with a reason, and the person is shown it at the approval, in
  `sdlc status` and in the pull request. A build that cannot follow the approved plan records a **deviation**
  and a person answers each one before the pull request. And the agent cannot edit what it is judged by: seven
  files — the approval, the rules, the decisions and the rest — are written only by a person, and every agent
  write to them is denied at the tool call.
- **The person.** Ask for something a decision set aside and the interview says so, in a sentence, and asks
  whether to keep the decision or depart from it. The approval is yours, recorded against a hash of the exact
  text, and void the moment that text changes. A team may run without approvals, and then every report says
  that no person approved.
- **Neither** can waive a blocking finding. It is answered, or the cycle does not advance.

### 4. Nothing grades its own work

The agent that wrote a document agrees with itself. **Every planning document is read by a fresh process that
never saw it being written** — no memory of writing it, no tools, nothing to defend — and judging and editing are
separate acts: the reader never edits, and the editor never judges. The verification runs the same way, in a
process that never saw the build.

### 5. A definition of done, fixed before the code, and proof that traces back to it

**Why it exists.** An agent reviewing an agent can always find one more thing. With no fixed line for *done*,
review becomes a loop: each round imagines a new case, the build chases it, and the next round imagines another.
The change either never finishes or finishes when somebody gives up, and neither says whether it was good
enough. ai-sdlc settles *good enough for this cycle* up front, in writing, and holds everything after it to that
line.

**That line is what separates a blocker from a suggestion.** A finding blocks only when it breaks a criterion, a
case the spec listed, or a rule it can cite by id. Anything else is a **suggestion** — kept, never lost, free to
start the next cycle, and unable to hold this one hostage. A case nobody listed stays a suggestion at every later
stage, so the target does not move once the work is underway.

**How the line is drawn, and how it is proved:**

- **The intent** says what outcome the change exists for.
- **The spec** turns each outcome into criteria that can fail. A spec that leaves out an outcome, or part of one,
  is blocked, and so is one that adds scope nobody asked for. A criterion ranging over cases lists them, and that
  list *is* the definition of done.
- **The plan** names the test that proves each criterion. A reader blocks a proof that would pass without ever
  exercising what the criterion claims — not one that misses a case somebody merely imagined. Where the plan rests
  on how a platform behaves, a throwaway probe makes that behaviour happen first, and the plan quotes what it
  showed.
- **The build** writes those tests and records the evidence: the diff, a normalised test summary and coverage.
  The turn cannot end on a red build or on missing evidence.
- **The verification**, in a process that never saw the build, goes criterion by criterion: did the named test
  run, did it pass, and does it actually establish the criterion? When it blocks, remediation goes round only
  while each pass reduces what is still unmet; a pass that undoes an earlier fix, or two that reduce nothing,
  hand the cycle to a person.

So every test that decides the verdict traces back through a criterion to a line in the intent, the loop has an
end you can name in advance, and what did not make this cycle is waiting for the next one rather than forgotten.

### 6. Enforcement that does not depend on the agent's goodwill

An instruction the agent reads is advisory, and an agent will talk its way past it. **Everything a program can
decide is decided by one** — the `sdlc` binary, called from the agent's hooks and from CI — and the guarantee is a
pull-request check that refuses any change whose chain is not complete, approved and hash-matched. It depends on
no agent host, so it governs the merge whichever agent wrote the code. [What stops an agent talking its way past
it](#what-stops-an-agent-talking-its-way-past-it) has the three layers.

### 7. A record that learns from itself

The same mistake recurring across cycles should become a rule, and a choice a cycle settled should become a
decision. **`/sdlc:mine` and `/sdlc:propose` read the record for both** — the rule the findings keep reaching for,
the choices approved cycles settled, the suggestions nobody has answered — and argue each as an entry a person
may adopt. They propose and never place, so the rules and decisions grow from the solution's own history and
stay a person's.

### 8. An audit trail you can read months later

The pull-request body is the cycle itself: the problem in the intent's words, each criterion with its verdict,
and anything still open. Each document's rounds of findings are one ledger chained by hash, and the approval
records a hash of what it approved, so **the argument behind a merge cannot be quietly rewritten** afterwards.
*Why does this code exist, and who agreed to it?* is answered from the repository, not from anybody's memory.

**And it says plainly what it has not proved.** A thing that appears enforced and is not is worse than one that
never pretended, so [Where it stands](#where-it-stands) lists which capabilities have met real use and which only
the test suite.

## How it works

**A person walks seven steps, and can say each in a sentence.** Every hand-back offers the next one in words;
the slash form is there if you prefer it.

| | Step | Say | Or type |
|---|---|---|---|
| 1 | **Begin an intent** | *add …*, *fix …* or *change …* — or just describe it | `/sdlc:intent` |
| 2 | **Take it to plan** | *take it to plan* | `/sdlc:to-plan` |
| 3 | **Steer the plan** | *in that plan, make it … instead* | `/sdlc:steer` |
| 4 | **Approve** | — you rename the approval it drafted, then *admit it* | `/sdlc:pr --plan` |
| 5 | **Build it** | *build it*, in a new session | `/sdlc:build` |
| 6 | **Fix it** | *fix it*, when verification blocks | `/sdlc:remediate` |
| 7 | **Open the pull request** | *open the pull request* | `/sdlc:pr` |

**Step 4 has two halves.** The rename is the approval. Admitting it takes the approved plan out of the sandbox it
was planned in and onto the integration branch, by a pull request of its own — `/sdlc:pr --plan` in the sandbox,
or `sdlc admit` in a solution with no remote. It approves nothing a second time, and the build refuses a plan
that has not left the sandbox.

Underneath, each step produces a file the next one reads:

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

**The three planning documents are always required**, and a stage will not run until the document before it
has had a reading that raised nothing blocking. **There is exactly one gate**: a person approves all three
documents together, immediately before the build. A team may declare that no approval is required —
`approval: none` in `solution.md`, which only a person writes; every other check stays
([running a cycle](docs/guides/running-a-cycle.md), under *Running without approvals*).

**Two loops, one on each side of that gate, and neither is open-ended.** `/sdlc:steer` carries a correction
down the chain before you approve, editing the intent, spec and plan rather than regenerating them; it changes
**how, not what**, and a correction that changes the problem is a different cycle. `/sdlc:remediate` fixes code
and tests against a verification that blocked, and runs **while each pass reduces what is open** — the bound is
on progress, not on passes (D-66), which is the only reason an unattended run is safe to start.

## Features

The whole catalogue, each feature with the problem it exists for. [Where it stands](#where-it-stands) says which have met real
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
| **Findings** | Review comments are vague, cite nothing, and quietly get dismissed | Every blocking finding cites one rule by id, and every finding says where, what and the remedy. **A blocking one cannot be waived by any mechanism** — it is answered, or the cycle does not advance. Each artifact's rounds are one ledger chained by hash, so the argument behind an approval cannot be rewritten |
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
| **Protected files** | An agent edits its own approval, or the rules it is judged by | Seven files — the approval, an abandonment, the solution's rules, its decisions, its areas, its stack and what it is — are written only by a person. The pre-tool hook denies every agent write to them, by path and by shell command alike |
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

## What stops an agent talking its way past it

Three layers, and only the last is a guarantee:

| Layer | What it is | Who can get past it |
|---|---|---|
| advisory | the instructions the agent reads | the agent, constantly |
| local | the `sdlc` binary, a pre-tool hook, a turn-end hook | anyone with a shell |
| **non-bypassable** | `sdlc verify-chain` as a pull-request check | nobody without repository admin |

The promise made to a team is the third one. The local layer exists to make it cheap to satisfy, not to
be the proof.

**And seven files only a person ever writes:** `approval.md` the gate decision, `abandoned.md` why a cycle
stopped, `constraints.md` the solution's own rules, `decisions.md` the choices it has settled, `areas.md`
the parts of its code that decide which of those reach a reader in full, `solution.md` what it is, and
`techstack.md` what it is built from. A tool prints what belongs in them
and a person puts it there. Every agent write to all seven is denied at the tool call, by file path and by
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

To install a build of your own instead, [the install guide](docs/guides/installing-the-plugin.md#the-binary)
has the commands.

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
`global  embedded in sdlc 0.47.0`. **Use this rather than `sdlc help` as the smoke test** — `help` and
`version` are the only commands that need nothing from the installation, so they are the two that cannot
tell you whether it worked. Then `claude plugin details sdlc@ai-sdlc` should say `Skills (21)` and
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

The skill opens the cycle in a **sandbox** — a folder of its own beside your clone, on `sandbox/<id>` — and
never switches your own checkout; carry on in a session opened there. It interviews you into the intent and
waits for your answers, because the intent is written from them. Then *take it to plan* — `/sdlc:to-plan` —
writes the spec and the plan, and you approve all three. **The approved plan then leaves the sandbox by
admission** — `/sdlc:pr --plan`, or `sdlc admit` in a solution with no remote — and the build, in a new session,
runs and verifies itself.

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
| [`docs/guides/running-the-factory.md`](docs/guides/running-the-factory.md) | many cycles built unattended, whenever you start the factory: the integration branch, the factory, and its sitrep |
| [`docs/guides/mining-the-cycles.md`](docs/guides/mining-the-cycles.md) | after a cycle finishes: what the solution keeps from it, every open suggestion answered, and the finished cycles' folders cleared |
| [`docs/guides/handling-blockers.md`](docs/guides/handling-blockers.md) | a blocking finding or a blocked verification, and how each is answered |
| [`docs/guides/handling-refusals.md`](docs/guides/handling-refusals.md) | every refusal, and the remedy for each |
| [`docs/guides/testing-a-local-build.md`](docs/guides/testing-a-local-build.md) | a cycle run end to end on a change to the framework nobody has published, and why it never reaches a pull request |
| [`docs/mechanisms/README.md`](docs/mechanisms/README.md) | **the mechanisms** — how one works and why, one page each |
| [`docs/mechanisms/protected-files.md`](docs/mechanisms/protected-files.md) | the files only a person changes, and how the hook guards them |
| [`docs/mechanisms/perspectives.md`](docs/mechanisms/perspectives.md) | what examines a change beyond the rules, when each perspective steers and judges, and how to read what it found |
| [`docs/cheat-sheet.md`](docs/cheat-sheet.md) | **the cheat sheet** — one cycle, one line per step |
| [`docs/reference/README.md`](docs/reference/README.md) | **the reference** — every command, skill, refusal and piece of data, one line each |
| [`docs/reference/commands.md`](docs/reference/commands.md) | every command, on one screen |
| [`docs/reference/skills.md`](docs/reference/skills.md) | the twenty-one skills, one line each |
| [`docs/reference/refusals.md`](docs/reference/refusals.md) | every refusal, one line each |
| [`docs/reference/data-views.md`](docs/reference/data-views.md) | **data views** — every piece of data, where it lives, and the command that shows it |
| [`docs/samples/rolling-a-die.md`](docs/samples/rolling-a-die.md) | **the whole walk with one solution in it** — a d6 rolled from a page, every command, in a repository with no remote |
| [`skills/README.md`](skills/README.md) | the twenty-one skills as a reference: syntax, parameters, output, refusals and remarks for each |
| `sdlc constraints` | the rules in force, all tiers resolved. Run it rather than reading a copy of them |

One more worth knowing about. `.sdlc/README.md`, written into your repository by `sdlc init`, is the whole flow on one page that
travels with your code.

**Understanding or changing it:**

| | |
|---|---|
| [`CONCEPT.md`](CONCEPT.md) | the design, and every decision behind it with what each one rejected |
| [`CHANGELOG.md`](CHANGELOG.md) | what each release changed, and what moving to it asks of a solution |
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

**The planning half is complete and exercised end to end on real cycles**: adoption, a cycle, the three
documents, interrogation rounds, a placed approval, recorded evidence, a verification, the chain, and the
pull-request decision. Everything else is below, one row per capability, with how far it has been proved.
**Real use** means a governed solution's own cycles, pipeline or factory; **suite** means the test suite and
fixtures, and nothing live. What each release changed is in the [changelog](CHANGELOG.md).

| Capability | Has met | Not yet |
|---|---|---|
| **Bounded remediation** | the suite, far more than real use | enough real escalations to trust an unattended batch without watching the first ones |
| **The factory** | real use: one unattended run that built a cycle and opened its pull request | a morning spent on a sitrep of more than one cycle |
| **The Azure DevOps pipeline** | real use: one server, checking every pull request on the factory's agent image. It runs only once a person adds a build validation branch policy, which the file, the adoption guide and `sdlc doctor` all say | the branch-policy commands `sdlc branch-policy` prints for Azure DevOps |
| **Round cost** | real use: one plan round's blind readers, from about $0.79 to $0.33 | a verify round measured; which readers run on which model (`CONCEPT.md` §19) |
| **The reader prompts** | prompts generated against a real cycle, and the launch line measured on Windows PowerShell and Git Bash | a reader judging from them in a live cycle; whether answering row by row brings a plan's rounds down |
| **The findings ledger** | a copy of one real cycle: 17 MB of round files to 1.9 MB of ledger, with the same findings standing | a live cycle |
| **Planning in a sandbox** | real use: one sandbox carried two approved plans to a pull request, composed by hand after `sdlc admit` crashed, fixed in 0.39.3 | `sdlc admit` composing one for real |
| **The coverage floor** | one real solution's numbers | a cycle refused or passed by it |
| **The decisions through every stage, and departures** | the suite | a cycle judged this way: how often the change's reader blocks, and whether it adds rounds |
| **Deviations** | the suite | a recorded deviation: how often a build bends where it used to stop |
| **The seven-step cycle** | the suite | a cycle run this way, and whether one session holds the whole planning chain |
| **Steering before the spec, and reslicing** | the suite | either done for real |
| **Slicing and successors** | the suite | a slicing accepted, composed and merged; a successor started the morning it came ready. Readiness is read from the working copy and the integration branch, so a successor started in another worktree reads `ready` until its plan merges |
| **Parking, the backlog and `/sdlc:next`** | the suite | a parked plan meeting the code it waited for; a piece of work carried wave by wave |
| **Mining, areas and husks** | the suite, and one solution's history for reading a cycle's record | an area, a husk or a mined ledger in any solution; mining on a cycle's branch before its pull request |
| **Amending a plan after admission** | the suite | an amended plan through a real pull request |
| **The rebuilt patch** | a real git repository in the suite | a pull-request check rebuilding one |
| **The upgrade check** | a refused real upgrade, replayed and passing in a copy of the repository | a real upgrade pull request passing it |
| **Dependency moves without a cycle** | the suite, and a real security fix checked locally | one merged that way |
| **A test run on a local build** | the suite | a whole test run through `SDLC_DEV_BUILD` |
| **Running without approvals** | the suite | a real cycle. Under it nothing local anchors a findings ledger; git history and the pull request are what remain (`CONCEPT.md` D-137) |
| **Declaring the stack after adoption** | its author | a real adoption walked through it in order, by somebody new |
| **Taking the build settings into an existing codebase** | the suite: `sdlc scaffold` writes what the plan names, and `sdlc stack` reports the gap | the cycle that switches warnings-as-errors on over real code and fixes what it broke |
| **The stack rules** | — five `PRO_` rules, .NET only, a solution held to the ones its declared stack marks `true` | judging a real plan. Three came from convictions rather than findings; `PRO_04` and `PRO_05` came from trial and real plans and have not yet refused one |
| **Perspectives** | security, operability, maintainability and accessibility, written as methods that steer every stage; operability's reader may block on `CON_20` | proof that a perspective's reader catches what it should; UI/UX and the four others are not written |
| **Leads and Operate** | fakes only: `sdlc lead show` through `az` and `gh`, `sdlc operate` reading Application Insights | a real tracker or telemetry store. Jira is not read |
| **The task lists** | the suite holds the wording (`CONCEPT.md` D-80) | watched on any host |
| **Pushing** | the refusal, in the binary and tested | a command behind the push: `pr` and `batch-build` use the platform's own CLI |

**Three gaps are the hosts' and the design's rather than a test's.**

- **The Copilot install has never been run**, though nothing is guessed any more: the package conforms to
  [Agent Plugins 1.0](https://agent-plugins.org/specification) and the suite asserts that it does, so the
  skills should be discoverable — but nobody has watched them load.
- **The Antigravity adapter has never been run either**, and two of its gaps are the host's: its turn end
  is not governed at all, and its workspace configuration loads only in a folder marked trusted, so on a
  fresh clone the local layer is off until somebody marks it.
- **A local solution has no mechanical check over a merge, and nothing checks a push anywhere.** The pull-request
  check is the guarantee (`CONCEPT.md` D-78).

## Not goals

A whole-codebase coverage minimum that refuses a cycle for coverage it did not lower — a change is held to
70% of its own lines, and the codebase to its baseline only under the solution's floor.
 Making the local approval unforgeable — it cannot be, and the pull
request is where accountability lives. Governing ai-sdlc with ai-sdlc. Slicing work unattended. A device end-to-end layer. `CONCEPT.md` §18 has the full
list with reasons.

## License

MIT. See [LICENSE](LICENSE).
