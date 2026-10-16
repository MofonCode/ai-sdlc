# ai-sdlc

**Version 0.23.0** · iteration 4 · .NET 10 · MIT

**A plugin that makes an AI coding agent work the way a careful team does** — write down what the change
is for, agree what *done* means, plan it, have a person approve it, and only then write code.

Most agent workflows go from a sentence straight to a diff. This one does not. A change moves through six
stages, each producing a file the next stage reads, and **each file is challenged by a fresh reader
against a fixed set of rules before it advances.** Nothing advances because something said it was fine.

What that gives you:

- **A person approves the plan before any code exists** — and the approval is recorded against the exact
  bytes approved, so editing the plan afterwards invalidates it until it is approved again.
- **Approved work then builds unattended**, one cycle or many, and *stops* rather than thrashing when a
  cycle cannot be made to pass.
- **Nothing merges unless the whole chain is there** — complete, approved and hash-matched. That check
  runs in CI, not in the agent that wrote the code.

> **Is this for me?** If what you want is an agent that moves faster, no. If you have ever read an
> agent's pull request and been unable to tell what it was asked for, what it decided by itself, and
> what proves it works — yes.

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
they are approved again.

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
- the GitHub CLI, `gh`, signed in with access to this repository — it is private
- an agent host: Claude Code is shown below, and [Will it work with my agent?](#will-it-work-with-my-agent)
  lists the others

**1. Install the binary.** There is no published feed yet, so it is packed from a clone:

```pwsh
git clone https://github.com/MofonCode/ai-sdlc
cd ai-sdlc
dotnet pack src\Sdlc.Cli\Sdlc.Cli.csproj -c Release -o src\Sdlc.Cli\bin\pack
dotnet tool install --global --add-source src\Sdlc.Cli\bin\pack MofonCode.Sdlc.Cli
```

**2. Install the plugin.** Claude Code shown; the repository is private, hence `gh auth login`:

```pwsh
gh auth login
claude plugin marketplace add MofonCode/ai-sdlc
claude plugin install sdlc@ai-sdlc
```

**3. Check it worked.** From a directory that is *not* a clone of this repository:

```pwsh
sdlc constraints
```

It should print the seventeen global constraints in force and say it read them from
`global  embedded in sdlc 0.23.0`. **Use this rather than `sdlc help` as the smoke test** — `help` and
`version` are the only commands that need nothing from the installation, so they are the two that cannot
tell you whether it worked. Then `claude plugin details sdlc@ai-sdlc` should say `Skills (18)` and
`Hooks (2)`: **a hook count below two is a failed install**, not a detail, because the deterministic layer
is simply not loaded and every cycle will still appear to work.

**4. Adopt a repository.** Installing governs nothing — the framework reaches a repository when a person
adopts it there, one repository at a time. Start a new agent session first, because skills load at
startup.

```
/sdlc:init
```

**Start with the skill.** It asks where the code will live, then hands you the adoption command to run
yourself — a person runs that and an agent cannot, because adoption is a decision about how a team works
and it writes the hook configuration that constrains the agent. The order matters: the file recording
that answer is protected the moment it exists, so answering afterwards costs a draft and a rename.

**Then place the two things adoption leaves to you, one at a time.** Its report ends with a single next
step rather than a list of commands, and `sdlc doctor` names the one after it. First a paragraph saying
what the solution is: `sdlc draft solution` writes the file beside itself, and you rename it into place.

**Then declare the stack.** Adoption decides nothing about what the solution is built from. `sdlc draft
techstack` writes a draft of the framework's default — every row it would pin, every stack rule it
ships switched on, and the five .NET build settings files, switched on only where the repository held
no code — and you cut it down and rename it into place. What is in that file is exactly what
binds; an empty one pins nothing; and no cycle opens until it is there.

**5. Start a cycle.** In your agent, not the shell:

```
/sdlc:intent  The dashboard takes fourteen seconds and people have stopped opening it
```

That interviews you into the intent. `/sdlc:spec` and `/sdlc:plan` follow, you approve all three, and
then the build runs.

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
| [`docs/guides/adopting-the-framework.md`](docs/guides/adopting-the-framework.md) | adopting the framework, placing `solution.md`, and the files your solution owns |
| [`docs/guides/declaring-the-stack.md`](docs/guides/declaring-the-stack.md) | what a solution is built from — the declared stack, the stack rules it switches on, the build settings files it takes, and how to change any of them |
| [`docs/guides/running-a-cycle.md`](docs/guides/running-a-cycle.md) | one cycle end to end, stage by stage |
| [`docs/guides/setting-up-the-factory.md`](docs/guides/setting-up-the-factory.md) | the factory set up once, step by step, to a watched first run |
| [`docs/guides/running-the-factory.md`](docs/guides/running-the-factory.md) | many cycles built overnight: the integration branch, the factory, and the morning |
| [`docs/guides/handling-blockers.md`](docs/guides/handling-blockers.md) | a blocking finding or a blocked verification, and how each is answered |
| [`docs/guides/handling-refusals.md`](docs/guides/handling-refusals.md) | every refusal, and the remedy for each |
| [`docs/cheat-sheets/README.md`](docs/cheat-sheets/README.md) | **the cheat sheets** — one line per thing |
| [`docs/cheat-sheets/commands.md`](docs/cheat-sheets/commands.md) | every command for one cycle, on one screen |
| [`docs/cheat-sheets/skills.md`](docs/cheat-sheets/skills.md) | the eighteen skills, one line each |
| [`docs/cheat-sheets/refusals.md`](docs/cheat-sheets/refusals.md) | every refusal, one line each |
| [`docs/samples/rolling-a-die.md`](docs/samples/rolling-a-die.md) | **the whole walk with one solution in it** — a d6 rolled from a page, every command, in a repository with no remote |
| [`skills/README.md`](skills/README.md) | the eighteen skills as a reference: syntax, parameters, output, refusals and remarks for each |
| `sdlc constraints` | the rules in force, all tiers resolved. Run it rather than reading a copy of them |

One more worth knowing about. `.sdlc/README.md`, written into your repository by `sdlc init`, is the whole flow on one page that
travels with your code.

**Understanding or changing it:**

| | |
|---|---|
| [`CONCEPT.md`](CONCEPT.md) | the design, and every decision behind it with what each one rejected |
| [`CONSTRAINTS.md`](CONSTRAINTS.md) | the seventeen global constraints, each with what it forbids and why |
| [`AGENTS.md`](AGENTS.md) | how to work in this repository, written for an agent — and the repository map |
| [`src/README.md`](src/README.md) | what `Sdlc.Core` and `Sdlc.Cli` are, and where a new thing goes |
| [`tools/README.md`](tools/README.md) | the two tools that check what the test suite cannot |

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
- **The stack rules are .NET only and have never judged a real plan.** The framework ships three
  `PRO_` rules, and a solution is held to the ones its declared stack marks `true`. None is written for
  another stack — nothing has been built on one, so a rule there would be a guess with the authority of
  a constraint. The three were written from convictions this framework has paid for rather than from
  findings a cycle raised, which is the weaker provenance and worth saying.
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
- **A local solution has no mechanical check over a merge, and nothing checks a push anywhere.** The
  pre-push hook is gone (`CONCEPT.md` D-78): it was written and never installed, and where it ran it
  refused a push of work in progress. A solution adopted before 0.18.0 still has the file, and a clone
  that set `core.hooksPath` still runs it until somebody deletes the file or unsets the key.
- **A solution adopted before 0.16.0 that never listed its stack rules is held to none from 0.20.0.**
  Its old `profile:` line in `solution.md` is no longer read (`CONCEPT.md` D-81). `sdlc doctor` says
  so; `sdlc draft techstack` and a rename put the rules back.
- **Adoption ending on one next step has been read by its author and by nobody else.** The suite holds
  the order and the wording at each step; no adoption has been walked through it by somebody new.
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
- **The Azure DevOps pipeline has never run against a real server, by decision.** It ships with a note
  saying so in the generated file, where the first person to adopt there will read it (`CONCEPT.md`
  D-32). It runs only once a person adds a build validation branch policy — Azure Repos ignores a YAML
  pull-request trigger — and the file, the adoption guide and `sdlc doctor` all say so.
- **The binary is not on a feed.** Installing means cloning and packing — the single thing that would
  most improve a teammate's first hour.
- **The integration branch and the factory are built and have never run for real.** The queue, the cycle
  branches, the admission check, the sitrep, `batch-build --pr --unattended` and `sdlc factory` are held by
  the suite and tried against scratch repositories; no factory pipeline has run on a server, and nobody has
  spent a morning on a real sitrep (`CONCEPT.md` §10.4, §10.5).
- **Leads and Operate have only met fakes.** `sdlc lead show` reads work items through `az` and `gh`, and
  `sdlc operate` reads Application Insights through `az` and files through the same CLIs; none of that has
  touched a real tracker or a real telemetry store. Jira is not read yet.
- **Perspectives have no rules yet.** Security, operability and maintainability are written as methods and
  steer every stage, and accessibility does the same for any change touching a user interface, but none has
  a rule of its own, so everything they find is a suggestion; nothing proves yet that a perspective's reader
  catches what it is meant to (`CONCEPT.md` §6.5). UI/UX and the four others are not written.

## Not goals

Enforcing a coverage percentage. Making the local approval unforgeable — it cannot be, and the pull
request is where accountability lives. Governing ai-sdlc with ai-sdlc. Resolving merge conflicts as the
primary strategy. Tooling for splitting work. A device end-to-end layer. `CONCEPT.md` §18 has the full
list with reasons.

## License

MIT. See [LICENSE](LICENSE).
