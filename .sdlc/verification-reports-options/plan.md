---
slug: verification-reports-options
status: approved
finished: true
spec: none
tier: task
slices:
  - id: decision-ready-verification
    files: ["plugins/sdlc-core/skills/sdlc-router/references/artifact-schemas.md", "plugins/sdlc-core/agents/verifier.md", "plugins/sdlc-core/skills/verify/SKILL.md", "tests/AiSdlc.Tests/VerificationRecordTests.cs"]
    requirements: [V1, V2, V3, V4]
    depends_on: []
---

# plan — a verification record you can act on without reading it all

**Tier: task, stated out loud.** Four files, all prose and one test, in known code, with the
behaviour already agreed in conversation. That is one file over the router's "< ~3 files"
guideline and the stretch is deliberate rather than quiet: the alternative is an intent and a
spec for a change to the *shape of a document*, which is the ceremony-that-costs-more-than-it-
saves that I11 exists to prevent. If the build turns up a design question, that is the signal
this was mis-tiered and it should stop and re-tier rather than push through.

**No spec, so there are no R-numbers.** The requirements below are numbered `V1`–`V4` and come
from the conversation of 2026-09-19, not from a `spec.md`. The test suite cites them.

## Why

Six verification rounds landed on `init-scaffolds-solution`. The record is now ~2000 lines and
every one of those rounds was *correct*; none was *readable*. To know where the change stood,
a person had to read the whole thing — and in practice did not, because the session relayed a
hand-assembled summary instead.

Two things are missing, and the second is the one that matters.

**A summary.** The record opens with frontmatter and then goes straight into evidence. There is
nowhere to look that answers "what happened and how bad is it" in five lines.

**Options.** Every round reported *what it found*. Not one reported *what the owner can do about
it*. That gap was filled six times by the session inventing choices — "fix both, fix one, ship
and track" — from the findings. The verifier has better information at that moment than the
relaying session does, and it is simply never asked for it. A verdict the owner has to turn
into a decision themselves is a verdict that gets deferred.

## Requirements

- **V1.** `verification.md` SHALL open with a summary: the verdict, one line per blocking
  finding, and the count of everything else. It sits above the evidence and is readable without
  scrolling.
- **V2.** `verification.md` SHALL carry an **Options** section naming what the owner can do,
  each option with its cost, and SHALL NOT present a single course of action as inevitable when
  more than one exists. On a SHIP verdict with nothing outstanding, "ship it" is a complete and
  honest options list of one.
- **V3.** The verifier agent's output template, the schema's required-sections list, and the
  verify skill's presentation order SHALL agree.
- **V4.** A named check SHALL fail when they disagree.

## What is actually wrong today

**V3 is already violated and nothing noticed.** The agent's template at
`plugins/sdlc-core/agents/verifier.md` ends with:

```
## Verdict
SHIP / DO NOT SHIP — one sentence.
```

while `artifact-schemas.md` says *"Verdict — SHIP or DO NOT SHIP, first line"* and
`verify/SKILL.md` step 5 says *"The verdict — SHIP or DO NOT SHIP, in the first line."* Two say
first, one says last. Three lists, nothing comparing them — the same shape as the stack pack's
duplicate file table, which this repository has already fixed once and has a check for.

That is why V4 exists. Fixing the wording without adding the check leaves the next drift
equally silent.

## Work order

One slice, one writer. Every file here has to agree with every other file here; splitting them
across implementers would mean two people editing four documents that are each other's
contract. This is the case the plan skill names where a single writer in one worktree beats a
fan-out.

**`artifact-schemas.md`** — the `verification.md` schema gains **Summary** as the first required
section and **Options** as the last. Frontmatter is unchanged: it is Frozen, and nothing here
needs a new field.

**`agents/verifier.md`** — the output template is reordered so the verdict is first, matching
what the other two documents already claim, and gains the summary and options blocks. The agent
is told to produce options rather than leave them to be inferred, and told that on a clean SHIP
the honest list has one entry.

**`skills/verify/SKILL.md`** — step 5 relays the summary and the options rather than
reconstructing them. The existing "worst first" ordering stays; what changes is that the
options come from the record instead of from the relaying session's judgement.

**`tests/AiSdlc.Tests/VerificationRecordTests.cs`** (new) — V4. Parses the required-section list
out of the schema and the headings out of the agent's template, and fails when one names a
section the other does not. New file rather than an addition to `SkillAndAgentTests.cs`,
because that class is about definitions loading and parsing, and this is about two documents
agreeing on a contract.

## Tests

| Proves | Assertion |
|---|---|
| V3, V4 | Every section the schema requires appears in the agent's template, and vice versa. Fails naming the section and the document that omits it. |
| V1 | The schema's required list includes a summary section, and the agent's template opens with it. |
| V2 | The schema's required list includes an options section, and the agent is instructed to populate it. |

**Not checkable, and said rather than implied:** that the options a verifier writes are *good*
options. The check holds the shape, not the judgement. A verifier that writes "Options: ship it"
on a DO NOT SHIP verdict passes V4 and is useless — that is a model-behaviour failure the eval
suite is the right instrument for, not a unit test.

**Mutation-test V4** per I9: reorder or drop a section in one document, watch the named check
fail, restore.

## Risks

**This is a change to documents that instruct a model, so the real verification is the next
real verification.** A green suite proves the three documents agree; it does not prove the
resulting record is more useful. *Early-warning sign:* the next verification's summary is a
restatement of its own findings rather than a compression of them, or its options list has one
entry on a verdict with outstanding work. Read the first record this produces before trusting
it, the way I12 asks for build output.

**Adding required sections makes every past record non-conforming.** The six existing sections
in `init-scaffolds-solution/verification.md` predate this and will never have a summary. That
is correct and must not be retrofitted — those records describe what was true when they were
written. *Early-warning sign:* a check that validates existing `verification.md` files against
the schema rather than validating the two instruction documents against each other. The check
in V4 deliberately reads the schema and the agent template, never a produced record.

## Rollback

`git revert` of the slice commit. Nothing persists, nothing migrates, and no produced record
changes — the schema binds records written after it. The verify skill returns to reconstructing
options in the relaying session, which is where they come from today.

## Requirement coverage

V1, V2, V3 and V4 are each claimed by the single slice. V2's second half — that a single
course is not presented as inevitable — is prose the check cannot reach; it is listed under
**Tests** as not checkable rather than counted as covered.
