# ai-sdlc — the `sdlc` binary

**ai-sdlc holds an AI coding agent, and the people directing it, to what was written down.**

Every change is planned before any code exists, challenged by a reader that never saw it written, approved by a
person against the exact text, and proved from recorded evidence. A pull-request check refuses anything missing a
link. The solution's rules and decisions live in the repository beside its code and carry from one cycle to the
next, so every stage of every change is read against everything settled before it. Whatever contradicts them is
brought into the open, for the person and the model alike, rather than slipping through.

This package is `sdlc`, the deterministic half of [ai-sdlc](https://github.com/MofonCode/ai-sdlc): every check a
program can decide, called from the agent's hooks and from CI.

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
| this package | the `sdlc` command: what every host hook and pipeline step calls |
| the plugin | the twenty-one skills that write and judge the documents, which need language |

Install only this and you get a working gate and no stages — `sdlc help` lists what the binary does on
its own, and every stage name typed at it answers with the invocation to use instead of "unknown
command". Install only the plugin and every skill's first command is not found.

`sdlc doctor` reports on both and is explicit about which half it cannot see: no binary can ask a host
what it loaded, so that check names the command to run rather than claiming an answer.

## Why choose it

An agent can take a sentence to a merged diff in minutes, and nothing along the way records what the change was
for, what *done* meant, what the agent decided by itself, or who agreed to any of it. **The trouble is not that
the code is bad; it is that nobody can tell**, and the next session cannot either.

1. **Decisions that outlive the conversation.** `decisions.md` keeps what the solution chose and what it chose
   *instead of*, in the repository, written by a person. Every stage of every cycle reads it, and every reader is
   handed it — including the one that reads the finished code.
2. **Rules the agent cannot bend.** A constraint is a record, not an instruction: an id, four clauses, one saying
   what it *forbids*. Three tiers — global, the stack's, the solution's own — are rendered fresh into every
   reading, and every blocking finding cites one by id.
3. **Accountability runs both ways.** A departure from a decision is declared where it arises and shown to the
   person at the approval and in the pull request; a build that bends the plan records a deviation, and one that
   settles what the plan left open records a choice, and both lead the pull request in one report that merging
   accepts; and the agent cannot edit what it is judged by. Ask for something a decision set aside and you are
   told. Neither side can waive a blocking finding.
4. **Nothing grades its own work.** Every planning document is read by a fresh process that never saw it
   written, and the verification by one that never saw the build. The reader never edits; the editor never
   judges.
5. **A definition of done, fixed before the code.** An agent reviewing an agent can always find one more thing,
   so review loops. Here the spec's criteria, and the cases each lists, close *done* for the cycle: a finding
   blocks only when it breaks one of them or a rule it cites, and everything else is a suggestion, kept for the
   next cycle. The plan names the test proving each criterion, the build records the evidence, and the verdict
   traces back through the criterion to a line in the intent.
6. **Enforcement that does not depend on the agent's goodwill.** This binary decides everything a program can,
   and `sdlc verify-chain` as a required pull-request check refuses any change whose chain is not complete,
   approved and hash-matched — whichever agent wrote it.
7. **A record that learns from itself.** Mining reads finished cycles for the rule the findings keep reaching
   for and the choices the cycles settled, and proposes each for a person to adopt. It never places one.
8. **An audit trail you can read months later.** The pull-request body is the cycle; each document's findings
   are a ledger chained by hash; the approval records a hash of what it approved.

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

Seven files are a person's alone — the approval, an abandonment, the solution's own constraints, the decisions
it has settled, its areas, its declared stack, and the file saying what the solution is. No tool writes them.
This one prints what belongs in them and a person places it, and the pre-tool hook denies every agent write to
all seven, by file path and by shell command alike. **A blocking finding cannot be waived by any mechanism.**

## Hosts

The rules name no language, framework or package, so they travel to any stack — but this is a .NET
executable, so the toolchain does not. The hook adapter speaks Claude Code, GitHub Copilot and
Antigravity; `sdlc hook --host <name>` reads that host's payload, asks the one policy engine, and writes
that host's own decision shape. Claude Code is the host it has been run on in earnest.

## Requirements

.NET 10 SDK or later, and git. Without git a build cannot record the diff a verification derives from
and the gates key on a branch that cannot exist, so adoption refuses outside a repository rather than
appearing to work.

## Read more

- [The README](https://github.com/MofonCode/ai-sdlc#readme) — every feature, getting started, and where each
  capability stands
- [The changelog](https://github.com/MofonCode/ai-sdlc/blob/main/CHANGELOG.md) — what each release changed
- [The guides](https://github.com/MofonCode/ai-sdlc/tree/main/docs/guides) — one task each

## Licence

MIT.
