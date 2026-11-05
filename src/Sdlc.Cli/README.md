# ai-sdlc — the gate

`sdlc` is the deterministic half of [ai-sdlc](https://github.com/MofonCode/ai-sdlc): a constraint-governed
lifecycle for software written with an agent.

A change moves through **intent → spec → plan → build → verify → pr**. Each phase produces a file the
next phase reads, each file is challenged against a fixed set of rules before it advances, and a person
approves the three planning documents together — against a hash of each — before any code is written.
Nothing merges unless the chain is complete, approved and hash-matched.

```
dotnet tool install --global MofonCode.Sdlc.Cli
sdlc constraints        # the rules in force. Also the smoke test: it needs the install to have worked
sdlc init               # adopt the framework into a repository. A person runs this
sdlc doctor             # check this installation and this repository
```

## Two halves, and they fail differently

**This package decides. It does not write documents.**

| | |
|---|---|
| this package | the `sdlc` command: the gate every host hook, git hook and pipeline step calls |
| the plugin | the eighteen skills that write and judge the artifacts, which need language |

Install only this and you get a working gate and no stages — `sdlc help` lists what the binary does on
its own, and every stage name typed at it answers with the invocation to use instead of "unknown
command". Install only the plugin and every skill's first command is not found.

`sdlc doctor` reports on both and is explicit about which half it cannot see: no binary can ask a host
what it loaded, so that check names the command to run rather than claiming an answer.

## Why use it

The problem is not that an agent writes bad code. It is that a change can travel from a request to a
diff with nothing recorded about what it was for, what done meant, or who agreed to it — and that a
process which *looks* enforced and is not is worse than one that never pretended.

## Features

Each is the problem it answers, then what it does. Most are the plugin's skills; what makes them hold is
this binary.

- **Intent interview** — the agent codes from one sentence and its assumptions never surface. A person is
  interviewed into why the change exists, and what nobody can answer yet is recorded rather than guessed.
- **Slicing** — large work becomes one unreviewable pull request. It is cut into pieces that each build and
  merge alone, and a piece needing another's code waits until that code merges.
- **Spec with cases** — *done* is vague and edge cases are argued at review. Every criterion can fail and
  lists the cases it covers.
- **Plan with proofs** — tests pass and prove nothing. The plan names the test proving each criterion, and
  a throwaway probe proves any platform behaviour it rests on.
- **Steer** — a mid-course correction means regenerating everything. It is carried down the chain as edits.
- **Interrogator** — the author reviews its own document and agrees. A fresh process with no tools reads
  it against the rules, never edits, and the loop runs until nothing blocking is left.
- **Findings** — review comments cite nothing and get dismissed. Each cites one rule by id; a blocking one
  cannot be waived; the rounds are a ledger chained by hash.
- **Suggestions** — good non-blocking observations are lost. They are kept, each concern once, with what
  became of each, and any can start the next cycle.
- **Perspectives** — security, operability, maintainability and accessibility depend on somebody
  remembering them. Each steers the author and judges in a reader of its own.
- **Constraints** — a rule in a prompt is forgotten next session. Rules have ids and a *forbids* clause,
  in three tiers, rendered fresh into every reading.
- **Decisions charter** — each session re-argues what was settled, unless somebody prompts it. Settled
  choices and what they beat are read by every cycle without asking, and a plan departing from one must say
  what it costs.
- **Stack definition** — the agent picks packages ad hoc. The declared stack is exactly what binds a plan.
- **Propose** — the same mistake recurs and never becomes a rule. The record is read for the rule and the
  decisions a solution keeps reaching for, and each is argued for a person to adopt.
- **Hash-bound gate** — approval is a reply in chat and the plan changes afterwards. Approval records a hash
  of each planning document, and an edit voids it.
- **Protected files** — the agent edits its own approval or rules. Six files are a person's alone, and the
  pre-tool hook denies every agent write to them.
- **Recorded evidence and verify** — "the tests pass" is a claim. The build records its diff, tests and
  coverage; a fresh process judges each criterion from that evidence alone.
- **Bounded remediation** — a fix loop thrashes or quits at an arbitrary count. It runs while each pass
  makes progress, then escalates to a person and opens no pull request.
- **Chain check** — the process is skipped and merged anyway. `sdlc verify-chain` as a required
  pull-request check refuses a chain that is incomplete, unapproved or hash-mismatched.
- **Batch build and the factory** — approved work waits for supervision. It builds unattended overnight,
  never merges, and leaves a morning sitrep.
- **Leads** — work is retyped from a tracker. A cycle starts from an Azure DevOps or GitHub item, a
  suggestion, or production's errors.
- **Init** — adopting governance is setup nobody finishes. One interview and one command write the hooks,
  the pipeline and the drafts a person places.
- **Pinned release** — an upgrade moves the rules under a change in flight. A cycle runs on the release it
  opened on.
- **More than one agent** — governance for one agent fails when a teammate uses another. One adapter speaks
  Claude Code, GitHub Copilot and Antigravity, and the merge check needs none of them.

**What it costs.** More time and more tokens per change, by design: every change runs the whole chain. If
you want an agent that moves faster, this is the wrong tool; if you want to trust and audit what an agent
merged, that cost is the point.

## How it holds

Three layers, and only the last is a guarantee:

| Layer | Mechanism | Who can get past it |
|---|---|---|
| advisory | skill and instruction text | the agent, constantly |
| local | this binary, a pre-tool hook, a turn-end hook | anyone with a shell |
| **non-bypassable** | `sdlc verify-chain` as a pull-request check | nobody without repository admin |

Six files are a person's alone — the approval, the abandonment, a solution's own constraints, the decisions
it has settled, the declared stack, and the file saying what the solution is. No tool writes them. This one prints what belongs in them and a
person pastes it, and the pre-tool hook denies every agent write to all six, by file path and by shell
command alike. **A blocking finding cannot be waived by any mechanism.**

## Hosts

The rules name no language, framework or package, so they travel to any stack — but this is a .NET
executable, so the toolchain does not. The hook adapter speaks Claude Code, GitHub Copilot and
Antigravity; `sdlc hook --host <name>` reads that host's payload, asks the one policy engine, and writes
that host's own decision shape.

## Requirements

.NET 10 SDK or later, and git. Without git a build cannot record the diff a verification derives from
and the gates key on a branch that cannot exist, so adoption refuses outside a repository rather than
appearing to work.

## Licence

MIT.
