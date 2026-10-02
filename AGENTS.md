# ai-sdlc

A host-agnostic plugin that governs a team's software lifecycle through gated artifacts. A change moves
through **intent → spec → plan → build → verify → land**. Each phase produces a file the next phase reads,
each file is challenged against a fixed set of rules before it advances, and a person approves the three
planning documents together before any code is written. Nothing advances on assertion.

**This repository is the framework, and it does not run cycles on itself.** There is no `.sdlc/` directory
here and adding one is wrong — a framework judged by the constraints it is changing is a deadlock. It is
built as an ordinary product: branch, review, merge. The suite exercises the binary against scratch
solutions it builds on disk, and asserts the plugin's manifests and this repository's own claims;
`reference/iteration-3-cycle/` is a real cycle run in anger, and the best worked example there is.

## Framework or solution

Two things are easy to confuse and behave differently. Establish which one a request is about before
acting.

- **This repository is the framework.** A change here changes how every solution is governed.
- **A solution is a repository that adopted the framework.** It has a `.sdlc/` directory, a `solution.md`
  declaring its stack and origin, and usually its own `constraints.md`.

A constraint added here binds everyone. A constraint added in a solution binds only that solution. If a
request could mean either, ask.

## Where things are

| What | Where |
|---|---|
| the seventeen skills: what each is, how it is invoked, when it runs, who starts it | [`docs/skills.md`](docs/skills.md) |
| the design, and every decision behind it with what each one rejected | [`CONCEPT.md`](CONCEPT.md) |
| the fourteen global constraints, each with what it forbids and why | [`CONSTRAINTS.md`](CONSTRAINTS.md) |
| what documentation exists, and what each guide covers | the Documentation table in [`README.md`](README.md) |
| the guides themselves | `docs/` |
| what `Sdlc.Core` and `Sdlc.Cli` are, how one invocation flows, and where a new thing goes | [`src/README.md`](src/README.md) |
| the gate's logic: constraints, cycles, approvals, findings, hooks, evidence, the chain | `src/Sdlc.Core/` |
| the one binary every skill on every host calls | `src/Sdlc.Cli/` |
| the skills as they ship, one directory each | `skills/` |
| the plugin as it installs | `.claude-plugin/`, `hooks/` |
| the suite | `tests/` |
| a real cycle's artifacts, to calibrate against | `reference/iteration-3-cycle/` |

`CONCEPT.md` is the current authority on every design question. Read it before proposing a change to how
the framework works, and cite the decision you are revisiting.

## Building and testing

```
dotnet build
dotnet test
```

Both from the repository root. The SDK and the test runner are pinned in `global.json`; `Directory.Build.props`
holds the shared settings, `Directory.Packages.props` the versions.

**Warnings are errors**, and a build that warns is a build that failed. The relaxations for test code are
listed in `tests/Directory.Build.props`, each with its reason, and there are no pragmas in test source.

## What the suite enforces about this repository

These are properties, not style preferences, and each is a named test in `tests/Sdlc.Architecture.Tests/`:

- no source file switches off a warning with a pragma
- `Sdlc.Core` references no other project — it stays a leaf, because it is what everything else depends on
- every class is sealed, static or abstract
- every request `Sdlc.Core` declares has a registered handler
- every directory holding projects is scanned by the checks above

If one of these is in your way, the conversation is about the property, not about the test.

## The split between a command and a skill

**A command is deterministic and the binary owns it; a skill needs language and an agent runs it.**
`sdlc help` lists what a file listing and a hash can decide. Everything in `docs/skills.md` is what cannot
be decided that way. Typing a stage name at the binary — `sdlc spec <id>` — is answered rather than
refused: it says the stage is a skill and names the invocation.

## Four files only a person writes

In a governed solution: `approval.md`, `abandoned.md`, `constraints.md`, `solution.md`. No tool writes
them. A tool prints what belongs in them — `sdlc draft approval`, `draft abandon`, `draft constraint`,
`draft profile` — and a person pastes it. The pre-tool hook denies every agent write to all four, by file
path and by shell command alike, with no exception. A refusal there is the design working, not a
configuration problem to route around.

**Do not restate the constraints in this file or any other.** `sdlc constraints` renders them, all tiers
resolved. A copy is a second place they live, and the copy is always the one that goes stale.

## Keep the documentation current

Every document describes a surface, so each one goes stale the moment that surface moves. Update it in the
**same change** that moves it — never in a follow-up, because a follow-up is a thing somebody has to
remember, and the document is wrong in the meantime.

| If the change | update |
|---|---|
| adds, removes or renames a skill, or changes what one takes or refuses | [`docs/skills.md`](docs/skills.md) — the skill's row, its own section, and the count in the heading if it moved |
| changes a command's behaviour or options | `src/Sdlc.Cli/Usage.cs`, which is what `sdlc help` prints, and [`docs/guides/cycle-commands.md`](docs/guides/cycle-commands.md) |
| changes what a correct install produces — a hook, a skill count, a version | [`docs/guides/install.md`](docs/guides/install.md), **including the table at the top** |
| adds, removes or reworks a refusal | [`docs/guides/when-it-blocks.md`](docs/guides/when-it-blocks.md) |
| changes the flow a person walks through | [`docs/guides/first-cycle.md`](docs/guides/first-cycle.md) |
| moves the project boundary, an operation's shape, or where a new thing goes | [`src/README.md`](src/README.md) |
| changes the layout, the commands or the enforced properties | this file |
| changes where the project stands, its version, or what is not done | [`README.md`](README.md) |

**None of these may restate what another one owns.** The constraints live in `CONSTRAINTS.md` and are
rendered by `sdlc constraints`; the skills' surface lives in [`docs/skills.md`](docs/skills.md); the list of
what documentation exists lives in `README.md`. A second copy is the one that goes stale, and a session
working from the stale copy has no way to tell.

Much of this is asserted rather than trusted, and `tests/Sdlc.Tests/PluginTests.cs` is where: the skills on
disk are exactly the ones `CONCEPT.md` inventories, every skill names a command the binary actually has,
no skill restates a constraint instead of running the command, the README's counts and version are true,
every guide it points at exists, and every path this file links to exists with the capitalisation it
states. So a change that leaves one of those behind fails the suite before it reaches review.

**What no test holds is whether a sentence is still true.** The install guide's own table claimed one hook
for as long as the turn-end hook had existed, while the verification step eleven paragraphs down said two
and called one a failed install. Both were countable and neither was counted. If you are changing a
surface, read the document that describes it rather than trusting that the suite would have caught it.

This file is deliberately lean and carries nothing a cycle could make untrue — no status, no current work,
no version, no test counts. `README.md` holds where the project stands, and it is the one place to change
when that moves.
