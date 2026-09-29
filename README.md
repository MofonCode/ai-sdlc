# ai-sdlc

A Claude Code plugin that gives a repository a governed way to make changes.

Instead of asking for a change and reviewing the diff, a change moves through six phases — each one
commits a file the next reads, and each file is challenged against a fixed set of rules before it
advances. Nothing advances on assertion.

```
intent -> spec -> plan -> build -> verify -> land
                            ^         |
                            +- remediate (<= 3) -+
```

| Phase | Answers | Produces | Gate |
|---|---|---|---|
| intent | why | `intent.md` | interrogated, then a person approves |
| spec | what done means | `spec.md` | interrogated, then a person approves |
| plan | how | `plan.md` | interrogated, then **a person approves — the last gate before anything is irreversible** |
| build | the change | a diff, plus build and test evidence | it compiles and passes |
| verify | did it do the spec justice | `verify.md` | outcome `pass` |
| land | integration | a pull request | a code owner merges, never the authoring agent |

## Interrogation, the step between writing and approving

An artifact is challenged before anyone is asked to approve it:

```
/sdlc:interrogate <cycle-id> <intent|spec|plan>
```

It reads the artifact against the constraint set and writes `findings/<kind>.r1.json`. **Blocking**
findings name a constraint the artifact breaches and what would resolve it; **suggestions** name nothing
broken. Work the blocking ones and run it again — the round number increments, and the sequence stays as
the record of what was challenged and when.

**It is adversarial on purpose.** The gates are human, but a person should not be an artifact's first
reader. The interrogation catches breaches that are mechanical to describe and take judgement to spot —
an intent that states a solution instead of a problem, a criterion that cannot fail, a plan whose proof
would pass while its criterion went unmet — so a person's attention goes to the questions only a person
can answer.

That is also why it is one command rather than six: the same challenge, the same rules, at every
artifact. `/sdlc:constraints` shows the rules it judges against.

## Start here

```pwsh
claude plugin marketplace add ./
```

```pwsh
claude plugin install sdlc@ai-sdlc
```

Then `docs/guides/first-cycle.md` walks a change from an empty directory to a pull request in eighteen
numbered steps. `docs/guides/cycle-commands.md` is the same sequence with no explanation, for once you
have done it. `docs/guides/install.md` covers updating and removing.

## Why it works this way

Agents made writing code fast, which moved the bottleneck to deciding what to build, reviewing it, and
trusting it. Reading every line was a real control when a person wrote every line; it is not one when an
agent writes most of them.

So this replaces line-by-line trust with three things: artifacts that say what was asked and why,
evidence that proves it was met, and human gates only where judgement is genuinely required. Correctness
is not a gate — that is what the evidence is for. The gates are the questions a machine cannot be
accountable for: is this the right problem, is this the right solution, is this approach sound, is this
risk acceptable.

The other half is timing. Killing a bad idea costs a paragraph at intent and a week after it is built.

## Layout

| Path | Holds |
|---|---|
| `CONSTRAINTS.md` | the global constraints, `CON_nn` — rules about artifacts that need judgement to apply |
| `schemas/` | JSON Schema per artifact. They own the vocabularies and the verify state machine |
| `skills/` | one skill per phase, plus `init`, `run` and two read-only views |
| `hooks/`, `tools/` | the deterministic layer: what may not happen, whatever a model concludes |
| `docs/` | the design record, the delivery plan, the conventions, the guides |
| `tests/` | asserts the properties the design claims, and the fixtures it claims them against |

`docs/concept.md` is the design record and the authority on every design question.
`docs/conventions.md` is how to change this repository, with each rule naming the test that enforces it.

## State

Walked end to end on a real repository: `init`, `intent`, `interrogate`, `spec`, `plan`, `build`,
`verify` and `land` — a third trial carried a change from an empty directory to an open pull request,
with a verify document reporting `pass`. `constraints` is exercised throughout.

`interrogate` is also asserted mechanically: nine sample artifacts, each with a checked-in statement of
which constraints it must raise, run against the real skill on a schedule.

Not yet run: `remediate`, `run`, and the `suggestions` view. Treat their behaviour as unproven — in
particular nothing has driven a cycle to `escalated` and watched landing refuse it, which is the
behaviour that makes unattended work safe to trust.

`docs/conventions.md` carries the rules each of those phases taught us, every one naming the test that
enforces it.

```pwsh
dotnet test
```
