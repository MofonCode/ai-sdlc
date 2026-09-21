---
slug: pack-promises-what-it-scaffolds
status: approved
finished: true
spec: none
tier: task
slices:
  - id: honest-promise
    files: ["plugins/stack-dotnet/skills/stack-dotnet/SKILL.md", "plugins/sdlc-core/skills/init/SKILL.md", "tests/AiSdlc.Tests/PackPromiseTests.cs"]
    requirements: [P1, P2, P3, P4]
    depends_on: []
---

# plan — the pack promises what it scaffolds, and init stops demanding an unused answer

**Tier: task, stated out loud.** Three files, behaviour agreed in conversation, no design
question open. It supersedes half of a requirement in a shipped spec, which is the argument for
feature tier — but the direction is settled and an intent plus a spec for "stop saying an untrue
thing" is the ceremony I11 exists to prevent. If the build turns up a design question, that is
the signal it was mis-tiered, and the plan beside `engine-substitutes-declared-tokens` is what
happens when that signal is ignored.

**No spec, so no R-numbers.** Requirements are `P1`–`P4`, from the conversation of 2026-09-20.

## Why

**Two live defects, both user-facing today, neither blocked on anything.**

**The pack promises a layout it does not scaffold.** `stack-dotnet/SKILL.md` tells `init` to
write this into every scaffolded repository's charter, as a technology commitment:

```
- Module layout: vertical slices, each {Module}.Api / .Application / .UnitTests
```

The web kind emits `src/<Solution>.AppHost`, `.ServiceDefaults`, `.Web`, `.Api`,
`tests/<Solution>.UnitTests` and `.IntegrationTests`. No `.Application`, nothing per-module. So
every repository this pack creates is told, in its own charter, to follow a structure its own
scaffold did not give it — and `spec-writer` reads that charter as binding.

**`init` refuses to proceed without an answer nothing consumes.** `skills/init/SKILL.md:82-93`
asks what the modules are and where the boundaries fall, and stops the command if the answer is
missing. The word *module* appears **zero times** in `scaffold.json`. The owner answers, and the
answer is discarded.

That was an oversight. As of 2026-09-20 it is a decision: the owner chose a modular monolith —
one solution, split into modules by a separate skill once the seams are known — on the grounds
that boundaries gathered in an opening interview are a guess frozen into a directory structure,
and that MarketSense's eleven modules were discovered over years rather than designed on day one.
**So the answer is now deliberately unconsumed, which makes continuing to demand it worse than
an oversight.**

## Requirements

- **P1.** The charter line the pack writes describes what the scaffold emits.
- **P2.** `init` no longer stops the command for want of module boundaries. The solution *kind*
  is still required and still has no default — the engine needs it to select from the manifest,
  and that half of the shipped requirement stands.
- **P3.** The pack no longer instructs `init` to ask for module boundaries.
- **P4.** A named check fails when the pack's promised layout and its scaffolded layout disagree.

## Supersession

The shipped `init-scaffolds-solution` spec governs `init`'s flow. **R15** requires the command to
stop when *"the solution kind and its module boundaries"* are unsettled. P2 supersedes the module
half and leaves the kind half standing.

**The shipped spec is not edited.** It records what shipped. A forward pointer is added to it
naming this slug — the same treatment the frozen role list will need, and the same reason: a
fresh session reading an approved document should not learn something no longer true with nothing
to contradict it.

## What the new promise must not be

The obvious replacement — *"a modular monolith: boundaries kept clean, split when the seams are
known"* — is an **aspiration about future structure, not a description of what was emitted**.
That is the same defect as the line it replaces, in new words, and P4 could not check it.

The line has to say what a reader will find on disk. Whatever prose surrounds it, the part P4
holds is the part naming projects that exist.

## Work order

One slice, one writer. The promise, the question that fed it, and the check that holds them are
one change.

**`stack-dotnet/SKILL.md`** — the commitment line becomes a description of the emitted layout.
The *"Ask, rather than assume: the solution's module boundaries"* instruction goes. The pack's
architectural position — vertical slices, split later — may stay as prose provided the part P4
reads is a description rather than a promise.

**`init/SKILL.md`** — the module half of the stop condition goes; the kind half stays, and the
section says which answers are required and why, so the next reader does not restore the other.

**`tests/AiSdlc.Tests/PackPromiseTests.cs`** (new) — P4. Derives the emitted project roles from
each pack's `scaffold.json` and the promised roles from its `SKILL.md`, and fails when one names
a role the other does not. Reads the two pack documents against each other, never a scaffolded
repository, for the reason `VerificationRecordTests` gives about validating history.

## Tests

| Proves | Assertion |
|---|---|
| P1, P4 | Every role the pack's charter line names is emitted by its manifest, and every emitted role is named. Fails naming the role and the document that omits it. |
| non-vacuity | Both sides assert they derived something, or fail saying they asserted nothing — the shape this suite already uses. |
| P2, P3 | Read rather than executed: the stop condition no longer names module boundaries, and the pack no longer asks for them. Stated under **Not checkable** below rather than counted. |

**Not checkable, and said rather than implied:** that `init` actually stops for a missing kind
and no longer stops for missing boundaries. `init` is prose read by a model; nothing in this suite
can observe a prompt, and the charter records behavioural measurement as blocked externally. P2
and P3 ship verified by reading. Claiming otherwise is what an earlier intent did before a
reviewer pointed out no such instrument exists.

**Mutation-test P4** per I9: add a role to the promise that the manifest does not emit, and watch
the named check fail; then remove one the manifest does emit.

## Risks

**The replacement line drifts back toward aspiration.** It is a charter commitment, and charter
prose attracts intentions. *Early-warning sign:* the line gains a clause about what the team
should do rather than what the scaffold did. P4 only holds the part naming roles, so everything
around it is unguarded by construction.

**The competition kind has its own promise and its own layout.** Both kinds must satisfy P4, and
the competition kind emits `Features` and `Training` roles the charter line never mentioned.
*Early-warning sign:* the check passing for `web` and failing for `competition` on its first run,
which would mean the promise was only ever written for one kind.

## Rollback

`git revert` of one commit. The pack returns to promising a layout it does not emit, and `init`
returns to demanding an answer nothing reads.
