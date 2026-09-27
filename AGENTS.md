# ai-sdlc

A Claude Code plugin that defines how a change moves from idea to landed code in the solutions
that adopt it. A cycle runs **intent → spec → plan → build → verify → land**. Each phase commits
an artifact the next phase reads, and each artifact is interrogated against a set of constraints
before it may advance. Nothing advances on assertion.

**This repository does not run cycles on itself.** There is no `.sdlc/` directory here, and
adding one is wrong. The framework is built as an ordinary product — branch, review, merge — and
its skills are validated against fixture solutions under `tests/fixtures/`.

`docs/concept.md` is the design record and the current authority on every design question. Read it
before proposing a change to how the framework works.

## State of this repository

Clean slate, rebuilt from `archive/v02`. The design is written; nothing is built yet.

There is **no build, no test suite, no plugin manifest, no fixtures and no skills**. Do not go
looking for them and do not invent commands — if a task needs one, that is a gap to raise, not a
guess to make. `docs/buildout.md` says what comes next and what "done" means for each phase.

Earlier versions live on `archive/v01` and `archive/v02`. They are evidence, not a starting
point: read from them only when pointed at a specific file, and never copy forward without
saying why.

## Framework or consumer

Two things are easy to confuse and behave differently. Establish which one a request is about
before acting.

- **This repository is the framework.** A change here changes how every solution is governed.
- **A solution is a repository that adopted the framework.** It has a `.sdlc/` directory, a
  `solution.md` declaring its stack and origin, and possibly its own `constraints.md`.

A constraint added here binds everyone. A constraint added in a solution binds only that
solution. If a request could mean either, ask.

## Vocabulary

Short glosses for orientation. `docs/concept.md` §2 carries the reasoning, including why criteria
and constraints are deliberately different words.

| Term | Gloss |
|---|---|
| cycle | one change from intent to landing, identified `YYYYMMDD-xxxx` |
| intent | why the work exists — written by interviewing a person |
| spec | testable acceptance criteria for this cycle; disposable once landed |
| plan | the implementation plan — files, order, risk, proof. Not the intent |
| constraint | a permanent rule with a `Forbids:` clause. `CON-` global, `LOC-` per solution |
| finding | a verdict from an interrogation: `blocking` or `suggestion` |
| outcome | a verify document's state: `pass`, `blocked`, `escalated` |

**"Plan" means the implementation plan, never the planning stage.** Anthropic's playbook uses
the word both ways; this repository does not.

## Constraints

Not in this file. Global constraints will live in `constraints.md` at the plugin root; a
solution's own in its `.sdlc/constraints.md`. This file never restates them — an artifact that
restates another's content as its own authority creates two sources that will disagree.

## Commands

None yet. This section names them when a build exists.

## What makes a change landable

A change lands when CI is green, it is consistent with `docs/concept.md`, and a person has
reviewed it. When `docs/concept.md` disagrees with a change, the concept wins — or the change
includes the amendment that makes it agree, argued explicitly in the same pull request.

Until CI exists, the first two conditions collapse into the third.

## Do not touch

- `archive/v01`, `archive/v02` — history. Never commit to them
- Anything under `.sdlc/` in a consumer solution that is a generated artifact: a verify
  document is derived from evidence and is never hand-edited
- Constraint IDs, once assigned. They are a monotonic sequence, never reused or backfilled

## Where the detail lives

Open one of these only when its trigger applies; do not read them speculatively.

| Doc | Open it when |
|---|---|
| `docs/concept.md` | proposing or questioning how the framework works — phases, gates, artifacts, governance |
| `docs/buildout.md` | asking what to build next, or whether a phase is finished |

Rows are added as the documents are written. Every document under `docs/` appears here exactly
once, and every row points at a file that exists.

A doc that documents source declares a `covers:` glob and is checked for staleness against it.
A doc that records decisions declares `freshness: exempt` with a reason — a design record is
measured against the decisions it captures, not against source it was never describing.
