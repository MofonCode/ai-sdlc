# ai-sdlc

A host-agnostic plugin that governs a team's software lifecycle through gated artifacts.

A change does not travel from a request to a diff. It moves through phases, each producing a file the
next phase reads, and each file is challenged against a fixed set of rules before it advances. **Nothing
advances on assertion.**

Two things it exists to make possible:

- **A person approves a plan before code exists**, and the approval is recorded against the exact bytes
  that were approved.
- **Approved plans build unattended**, one or many at a time.

## The cycle

```
intent -> spec -> plan -> build -> verify -> land
                            ^         |
                            +- remediate (at most 3) -+
```

Intent, spec and plan are all required, always. Each is written, challenged in a **fresh process** against
the rules in force, and revised until a round raises nothing blocking. A stage will not run until the
artifact before it has such a round.

One gate: a person approves all three planning documents together, immediately before the build, and the
approval records a hash of each. If any of the three changes afterwards, the gate refuses until it is
approved again.

## What is actually enforced

Three layers, and only the last is a guarantee.

| Layer | Mechanism | Who can get past it |
|---|---|---|
| advisory | skill and instruction text | the agent, constantly |
| local | the `sdlc` binary, a pre-tool hook, git hooks | anyone with a shell |
| **non-bypassable** | `sdlc verify-chain` as a pull-request check | nobody without repo admin |

The promise made to a team is the third layer: **nothing merges unless the artifact chain is complete,
approved and hash-matched.** The local layer exists to make that cheap to satisfy, not to be the proof.

A rule of its own: **a directory that appears enforced and is not is worse than one that never pretended.**
Iteration 3 shipped a nightly job that asserted interrogation verdicts, needed an API key nobody had set,
and warned and exited cleanly for weeks. That is the failure mode this design is written against.

## Four files only a person writes

`approval.md`, `abandoned.md`, `constraints.md`, `solution.md`.

No tool writes them. A tool prints what belongs in them — `sdlc draft approval`, `draft abandon`,
`draft constraint`, `draft profile` — and a person pastes it. The pre-tool hook denies every agent write
to all four, by file path and by shell command alike, with no exception. There is no approve command for
an exception to be carved out of, which is why the rule states in one sentence and has nowhere for a
bypass to live.

**A blocking finding cannot be waived by any mechanism.** Not at the gate, not with a flag, not with a
recorded override. It is answered, or the cycle does not advance.

## Host-agnostic, not stack-agnostic

Every candidate host can deny a tool call before it runs, so the enforcement primitive is portable. What
differs is vocabulary, and one adapter normalises it: `sdlc hook --host <name>` reads the host's payload,
asks the one policy engine, and writes the host's own decision shape.

**v1 ships Claude Code and GitHub Copilot.** The rules themselves name no language, framework or package,
so they travel to any stack — but the tool is a .NET executable, so the toolchain does not.

## Getting started

```
sdlc init              # adopt the framework into this repository. A person runs this
sdlc constraints       # the rules now in force
sdlc new               # open a cycle
```

Then ask your agent for the stages: `/sdlc:intent`, `/sdlc:spec`, `/sdlc:plan`. `sdlc help` lists what the
binary does on its own, and `.sdlc/README.md` — written into the solution by `init` — is the whole flow in
one page.

## Repository layout

| | |
|---|---|
| `CONCEPT.md` | the design, and every decision behind it with what each one rejected |
| `CONSTRAINTS.md` | the fourteen global constraints, each with what it forbids and why |
| `CONCEPT.raw.md` | the original longhand draft, preserved unedited |
| `src/Sdlc.Core/` | the gate: constraints, cycles, approvals, findings, hooks, evidence, the chain |
| `src/Sdlc.Cli/` | the one binary every skill on every host calls |
| `skills/` | the seventeen skills |
| `hooks/`, `.claude-plugin/` | the plugin as it installs |
| `tests/` | the suite |

## Where it stands

Iteration 4. The planning half is complete and exercised end to end: adoption, a cycle, the three
documents, interrogation rounds, a pasted approval, recorded evidence, a verification, the chain, and the
landing decision.

What is honest to say about the rest:

- **Bounded remediation is implemented and tested, and has been exercised in fixtures far more than in
  anger.** Everything unattended rests on it, so the first batch runs are worth watching.
- **`land` and `batch-build` are skills with no command behind the push.** The refusal is in the binary and
  tested; the push itself uses the platform's own CLI.
- **Copilot gets the hook, not yet the skills.** The adapter is tested against a captured Copilot payload;
  packaging the skills for Copilot is not done, and guessing at a format would produce files that silently
  do nothing — which is the failure mode above.
- **The Azure DevOps adapter has no repository to be exercised against.** `CONCEPT.md` §19 carries that as
  an open question rather than a solved problem.

## Not goals

Enforcing a coverage percentage. Making the local approval unforgeable — it cannot be, and the pull request
is where accountability lives. Governing ai-sdlc with ai-sdlc. Resolving merge conflicts as the primary
strategy. Tooling for splitting work. A device end-to-end layer. `CONCEPT.md` §18 has the full list with
reasons.
