# ai-sdlc

**Version 0.12.0** · iteration 4 · .NET 10 · MIT

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
intent -> spec -> plan -> build -> verify -> land
                            ^         |
                            +- remediate (at most 3) -+
```

| Stage | Produces | The question it answers |
|---|---|---|
| **intent** | `intent.md` | why does this change exist? |
| **spec** | `spec.md` | what does done mean, in criteria that can fail? |
| **plan** | `plan.md` | how will it be met, and what will prove each criterion? |
| **build** | code, and recorded evidence | the plan implemented, with the diff and the test output saved |
| **verify** | `verify.md` | does that evidence actually show each criterion met? |
| **land** | a pull request | a person reviews it and merges |

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
- **`/sdlc:remediate`** fixes code and tests against a verification that failed, and gets **at most three
  passes**. Then the cycle stops and escalates rather than thrashing — which is the only reason an
  unattended run is safe to start.

## What stops an agent talking its way past it

Three layers, and only the last is a guarantee:

| Layer | What it is | Who can get past it |
|---|---|---|
| advisory | the instructions the agent reads | the agent, constantly |
| local | the `sdlc` binary, a pre-tool hook, a turn-end hook, git hooks | anyone with a shell |
| **non-bypassable** | `sdlc verify-chain` as a pull-request check | nobody without repository admin |

The promise made to a team is the third one. The local layer exists to make it cheap to satisfy, not to
be the proof.

**And five files only a person ever writes:** `approval.md` the gate decision, `abandoned.md` why a cycle
stopped, `constraints.md` the solution's own rules, and `solution.md` what stack it is. A tool prints
what belongs in them and a person puts it there. Every agent write to all four is denied at the tool
call, by file path and by shell command alike, with no exception and no flag to carve one out of.

**A blocking objection cannot be waived by any mechanism.** It is answered, or the cycle does not
advance.

One rule the project holds itself to: **a thing that appears enforced and is not is worse than one that
never pretended.** That is why [Where it stands](#where-it-stands) says plainly what has not been
exercised.

## Getting started

Two halves have to be present and they fail differently. The **plugin** carries the skills and the hooks;
the **`sdlc` binary** is the gate they call. Without the plugin nothing asks for a cycle; without the
binary every skill's first command fails.

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

It should print the sixteen global constraints in force and say it read them from
`global  embedded in sdlc 0.12.0`. **Use this rather than `sdlc help` as the smoke test** — `help` and
`version` are the only commands that need nothing from the installation, so they are the two that cannot
tell you whether it worked. Then `claude plugin details sdlc@ai-sdlc` should say `Skills (17)` and
`Hooks (2)`: **a hook count below two is a failed install**, not a detail, because the deterministic layer
is simply not loaded and every cycle will still appear to work.

**4. Adopt a repository.** Installing governs nothing — the framework reaches a repository when a person
adopts it there, one repository at a time. Start a new agent session first, because skills load at
startup.

```pwsh
sdlc init
```

A person runs this and an agent cannot: adoption is a decision about how a team works, and it writes the
hook configuration that constrains the agent.

**5. Start a cycle.** In your agent, not the shell:

```
/sdlc:intent  The dashboard takes fourteen seconds and people have stopped opening it
```

That interviews you into the intent. `/sdlc:spec` and `/sdlc:plan` follow, you approve all three, and
then the build runs.

**Want the whole walkthrough, or the variations?**
[`docs/guides/first-cycle.md`](docs/guides/first-cycle.md) is one cycle from an empty directory to an
open pull request. [`docs/guides/install.md`](docs/guides/install.md) has every install path — Copilot,
updating, removing, and the four things that catch people. And if something refuses,
[`docs/guides/when-it-blocks.md`](docs/guides/when-it-blocks.md) has every refusal with its remedy.

## Where to read more

**Using it:**

| | |
|---|---|
| [`docs/guides/README.md`](docs/guides/README.md) | **the guides, indexed** — what each covers and when to read it |
| [`docs/guides/install.md`](docs/guides/install.md) | installing the plugin and the binary, updating, and removing |
| [`docs/guides/first-cycle.md`](docs/guides/first-cycle.md) | one cycle end to end, in fourteen steps |
| [`docs/guides/cycle-commands.md`](docs/guides/cycle-commands.md) | every command for one cycle, on one screen |
| [`docs/guides/when-it-blocks.md`](docs/guides/when-it-blocks.md) | every refusal, and the remedy for each |
| [`skills/README.md`](skills/README.md) | the seventeen skills: what each is, when it runs, and who starts it |
| `sdlc constraints` | the rules in force, all tiers resolved. Run it rather than reading a copy of them |

One more worth knowing about. `.sdlc/README.md`, written into your repository by `sdlc init`, is the whole flow on one page that
travels with your code.

**Understanding or changing it:**

| | |
|---|---|
| [`CONCEPT.md`](CONCEPT.md) | the design, and every decision behind it with what each one rejected |
| [`CONSTRAINTS.md`](CONSTRAINTS.md) | the sixteen global constraints, each with what it forbids and why |
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
interrogation rounds, a placed approval, recorded evidence, a verification, the chain, and the landing
decision.

What is honest to say about the rest:

- **Bounded remediation is implemented and tested, but exercised in fixtures far more than in anger.**
  Everything unattended rests on it, so the first batch runs are worth watching.
- **`land` and `batch-build` have no command behind the push.** The refusal is in the binary and tested;
  the push itself uses the platform's own CLI.
- **The Copilot install has never been run**, though nothing is guessed any more: the package conforms to
  [Agent Plugins 1.0](https://agent-plugins.org/specification) and the suite asserts that it does, so the
  skills should be discoverable — but nobody has watched them load.
- **The Antigravity adapter has never been run either**, and two of its gaps are the host's: its turn end
  is not governed at all, and its workspace configuration loads only in a folder marked trusted, so on a
  fresh clone the local layer is off until somebody marks it.
- **The Azure DevOps pipeline has never run against a real server, by decision.** It ships with a note
  saying so in the generated file, where the first person to adopt there will read it (`CONCEPT.md`
  D-32).
- **The binary is not on a feed.** Installing means cloning and packing — the single thing that would
  most improve a teammate's first hour.

## Not goals

Enforcing a coverage percentage. Making the local approval unforgeable — it cannot be, and the pull
request is where accountability lives. Governing ai-sdlc with ai-sdlc. Resolving merge conflicts as the
primary strategy. Tooling for splitting work. A device end-to-end layer. `CONCEPT.md` §18 has the full
list with reasons.

## License

MIT. See [LICENSE](LICENSE).
