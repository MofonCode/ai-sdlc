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

**What it gives you that ordinary development does not.** A careful team writes requirements, reviews
designs and checks that tests prove something, and relies on discipline to do it. Here each habit is a
mechanism: every change carries an intent, a spec whose criteria can fail and a plan naming what proves
each one; every document is read against a fixed set of rules by a fresh process that never saw it
written; a person's approval is recorded against a hash of each document, so an edit afterwards voids
it; the build is judged from its recorded evidence alone, by a process that did not see it; and a fix
that stops making progress escalates to a person instead of thrashing, which is what makes building
approved work unattended safe.

**Why this rather than another AI development process.** Spec-driven workflows for agents usually stop at
the documents: nothing outside the agent checks them, the session that wrote the plan judges it, approval
is a reply in chat, and the merge never learns any of it happened. ai-sdlc assumes the agent will talk its
way past any instruction, so the gate is this program rather than a prompt, the reader is never the
writer, a blocking finding cannot be waived, the files that record a person's decisions cannot be written
by the agent, and the guarantee is a pull-request check the agent cannot reach. One binary serves Claude
Code, GitHub Copilot and Antigravity, so the same check governs the merge whichever agent wrote the code.

**What it costs.** More time and more tokens per change, by design: every change runs the whole chain. If
you want an agent that moves faster, this is the wrong tool; if you want to trust and audit what an agent
merged, that cost is the point.

### How it holds

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
