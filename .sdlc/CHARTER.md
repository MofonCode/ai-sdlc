---
version: 2
solution: ai-sdlc
pack: none
origin: hand-written — solution zero has no pack to copy a charter from
since: 2026-09-22
restored: 2026-09-24 from e36fc4b; five deferred decisions were resolved before the restore and are recorded below as gaps rather than entries
amended: 2026-09-24 by 20260924-c4a1 — ten blocking findings from the first interrogation
---

# Charter — ai-sdlc

Solution zero. It builds the framework and consumes no stack pack, so this charter has
no producer by definition — every solution after it is scaffolded or adopted by the
init skill, which copies its pack's charter.

This is the authority every artifact in this repository is interrogated against.
It says what work here must **obey**. It does not describe what the framework
does — that is the code, the tests, and the traceability in each `verify.md`.

---

## How this document works

**Invariants are immutable and append-only.** IDs are assigned once, never reused,
never renumbered — **including across the copy from a pack charter into a solution
charter.** A changed conclusion is a *new* invariant that supersedes the old one; the
original keeps its ID and gains `Superseded-by`.

A solution that drops an inapplicable invariant leaves a **gap** — `INV-01, INV-02,
INV-03, INV-05` — and the gap is itself the record that something was removed. Local
additions use `LOC-nn`, numbered independently and never reused either. Without this,
a local invariant numbered into a vacated slot would make `cites: INV-04` mean
different things in different solutions on the same pack, and per-pack aggregation
would break.

**`Origin:` records where this charter came from** — `scaffolded from dotnet-library@v3`,
or `adopted — inferred at <sha>` with its baseline count. It tells every interrogation
how much to trust the document it is checking against, since an inferred charter is a
guess about a codebase while a scaffolded one is a copy of a known-good template. It
also makes the id gaps readable, which they are not without knowing the source.

**Every invariant carries a non-empty `Forbids:` clause.** This is the acceptance
test made mechanical:

> If a rule is compatible with every possible implementation, it is not a rule.
> If a rule does not change at least one downstream plan or diff, it is decoration.

A missing or empty `Forbids:` fails the form check. Excluded from invariants: tool
names, framework preferences, style rules, package versions, and unenforceable
aspirations. Where a rule genuinely concerns tooling, it is delegated to the stack
pack through INV-04.

**Every invariant carries a non-empty `Applies-to:` clause** naming the artifacts it
governs. This is what tells `interrogate <artifact>` which invariants its `charter`
role should load. An empty `Applies-to:` governs nothing and fails the form check for
the same reason an empty `Forbids:` does — it is decoration.

It is also what makes coverage visible: the invariants naming a given artifact kind
**are** the coverage for that kind, read off the clauses themselves.

A summary matrix used to sit here. It was deleted in v2 because it restated the
`Applies-to:` clauses with nothing binding the two, and it had already drifted three ways
— a missing row for INV-08, no column for INV-06's declared `pack` scope, and a tick for
INV-07 that contradicted its own clause. That is exactly what INV-03 forbids, and the fix
for derived data in a hand-maintained document is to delete it rather than to promise to
keep it in step.

**`every artifact` includes code.** The `, and code` suffix on INV-03 and INV-08 is
emphasis rather than extension, kept because both are invariants people try to scope to
documents alone. The authoritative list of artifact kinds is the findings schema's
`artifactKind`; this document does not restate it.

INV-01 and INV-02 were deliberately widened to reach intents. Written for specs
alone, an intent could be blocked by only two invariants, and everything else about
it — ambiguity, missing cases — would return as `suggested` no matter how serious.
The intent is the cheapest place to catch both.

**Findings cite invariants by ID.** A finding that cannot cite an invariant, a
failed mechanical check, or a location pair is capped at `suggested` and cannot
halt a cycle.

**Amendment is by counts, not opinion.** Every signal below is derived from the
findings corpus by a read-only query — never stored in a committed file, which would
be a second list beside the corpus. A signal is a **prompt to investigate, not a
verdict**: no process may act on one, because acting means changing this document and
that requires an approved charter intent (INV-08).

| Signal | Reading | Action |
|---|---|---|
| zero cites, and no violation can be constructed | decoration | delete |
| zero cites, violations possible but undetectable | unenforceable | keep; make it checkable |
| zero cites, and a verified `Prevented-by:` target still rejects violations | working as designed | keep |
| repeated **rejected** charter intents against it | unclear, not wrong | keep; clarify its wording |
| high override count — cited then dispositioned "accepted, won't fix" | too broad, or wrong | candidate for narrowing |
| recurring suggestion clusters on one theme | a missing invariant | candidate for addition |

**There is exactly one way to change this document, and no process can take it.**

> The charter changes only through a **charter intent** that a **human approves**.
> No automated process may approve one. No cycle may amend the charter governing it.

A cycle that hits a charter trigger halts, files the intent, and waits. It can
request; only a person can grant.

The same protection covers the stack pack, because INV-04 delegates the substance of
its rule there — protecting only this file would leave the rule's content in an
unlocked drawer. Two sources, so that routine maintenance does not dilute this
document's version:

| Source | Changes | Bumps |
|---|---|---|
| `charter-review` | this document — invariants, taxonomy, `DEC` register, roles, the `Inherits:` line | charter version |
| `pack-review` | `pack.yaml`, `pack.rules.yaml` | pack version |

Both are human-approved and one-way; which applies is determined by path, so neither
needs a judgment call. Creating a new pack is an ordinary cycle — nothing exists yet
to loosen. Modifying one needs the gate.

Amendments never rewrite history: a retro stamped `charter v1` stays interpretable
against v1 forever.

**Commencement.** This rule takes effect **when the first cycle runs against this
charter.** Until then the document is a draft being written by hand, and there is no
process to protect it from. The rule is enforced by INV-08 from that moment on.

**Finding an invariant nothing ever cites.** Four of the five amendment triggers
announce themselves — a blocked cycle, a growing suggestions pile, a change you made,
an arriving intent. The fifth produces no event at all: an invariant that no finding
ever points at is decoration, unenforceable, or being prevented upstream by a
template — three readings calling for opposite actions — and nothing will ever prompt
you to notice which. You can be prompted about what happens; you have to go looking for
what does not. So one query — *which invariants have zero cites, and which deferred
decisions are old with zero cites* — runs whenever a charter intent happens for any
other reason, with a floor of every ten cycles.

**This document is interrogable.** Three of the four roles apply unchanged; the
`charter` role is inapplicable to the charter and is substituted by `form`, which
checks each invariant's shape rather than judging its content.

**`Prevented-by:` is an optional invariant field.** When a template or mechanical check
guarantees compliance, name it — and name it as a **resolvable target**: a tracked path or
a mechanical check id, never prose like "the plan template".

The field alone is not the answer. Before the zero-cite query may report "working as
designed" it must verify the named target: the path or check id still exists, **and** the
mechanism still rejects an artifact that violates the invariant. Where that cannot be
verified, the invariant is reported as zero cites with no demonstrated prevention — a
question again, not an exemption. Without the verification the field would convert an
unfalsifiable claim into a permanent pass, which is the failure it exists to prevent.
Without the field at all, zero cites is a question.

**Version stamping.** Every retro records the charter version in force when its
cycle ran.

---

## Invariants

These seven were chosen by one criterion: **the minimum set that lets every blockable
finding category block.** `unclassified` is excluded by construction rather than by
omission — it can never block, so no invariant could unlock it.

Three of the seven are the only route their category has: `missing-case` (INV-01),
`ambiguity` (INV-02) and `duplicated-truth` (INV-03). Drop one of those and its category
goes permanently advisory. For `scope-drift`, `gate-bypass`, `unfounded-claim` and
`charter-violation` a mechanical check or another invariant can also carry the citation,
so there the invariant is what keeps a **reasoning-detected** instance blockable rather
than the only route at all.

They are written solution-agnostically, so promotion into a shared
`mofoncode-invariants` set is a rename rather than a rewrite.

### INV-01 · Error paths are enumerated

    Applies-to: intent, spec
    Forbids:  an intent or spec that describes behaviour without its failure
              modes.
    Evidence: the cheapest defect to prevent and the most expensive to discover
              in production. Without this, `missing-case` can never block.
              Scoped to intents as well as specs deliberately: a missing case in
              an intent propagates into the spec, the plan, the code and the
              tests, and the intent is the cheapest place to catch it.
    Unlocks:  missing-case
    Since:    v1

### INV-02 · One reading per statement

    Applies-to: intent, spec, plan
    Forbids:  a statement that admits two readings leading to materially
              different downstream work.
    Evidence: ambiguity resolved silently by an agent is a decision nobody made.
    Unlocks:  ambiguity
    Since:    v1
    Note:     the most subjective invariant here, and the one most likely to
              accumulate overrides. Under the amendment rule it will be narrowed
              or deleted on evidence. That is the intended behaviour, not a flaw.
              Titled "per statement" rather than "per criterion" so it reaches
              intents and plans, which have no acceptance criteria.

### INV-03 · No second list without a binding check

    Applies-to: every artifact, and code
    Forbids:  introducing a second representation of a fact without, in the same
              change, the check that fails when the two disagree.
    Evidence: measured in v01 — bitten four times. The failure is silent: correct
              on the day it is written, wrong weeks later, and nothing looks at it.
              "In the same change" is the load-bearing part; the check must exist
              before the drift window opens.
    Scope:    this forbids representations that can DRIFT. A copy of an
              immutable, append-only artifact is not a second list, because
              divergence is impossible — provided the mechanism that makes the
              copy refuses to overwrite with different content. The retro corpus
              is the INTENDED instance, and the exemption holds only once such a
              mechanism exists and refuses to overwrite. None is built yet, so
              the retro corpus does not currently qualify.
    Unlocks:  duplicated-truth
    Since:    v1

### INV-04 · The declared stack governs

    Applies-to: intent, spec, plan, code
    Forbids:  a runtime, framework or package choice that contradicts the
              solution's declared stack pack, unless this charter records the
              deviation.
    Evidence: defaults only save work if deviating from them is visible. One
              invariant makes every pack rule citable, so no invariant is ever
              added per tech decision — and tool names stay out of the charter.
    Scope:    what the solution SHIPS AND RUNS. Reading, running or porting
              existing work in another language is research, not a runtime
              choice, and is never a violation. Reading a Python notebook to
              understand an approach: fine. Porting it to C#: fine. Vendoring it
              into the solution and calling it at runtime: a violation.
    Unlocks:  charter-violation
    Since:    v1

### INV-05 · Plans declare their files

    Applies-to: plan
    Forbids:  a plan that does not enumerate the files it will touch.
    Evidence: makes scope drift mechanically detectable rather than a judgment,
              and is what the blast-radius escalation compares against — the same
              declaration serves both.
    Unlocks:  scope-drift
    Since:    v1

### INV-06 · No unjustified gate bypass

    Applies-to: code, verify, pack
    Forbids:  a warning suppression, a disabled diagnostic, a skipped test, or an
              excluded path without a written justification.
    Evidence: a zero-warning build is a strong signal because it is binary. The
              failure mode is not warnings appearing — it is suppressions
              accumulating until the zero means nothing. The suppression count is
              tracked in every `verify.md`, and an increase is itself a finding.
              Stated without tool syntax as of v2: the spellings belong to the
              pack under INV-04, and naming them here made this invariant fail
              the form rule's own exclusion of tool names. In this solution's
              stack they are NoWarn, `#pragma warning disable`, `[Ignore]` and an
              excluded path — illustrative only, and this solution consumes no
              pack, so nothing else carries them yet.
    Unlocks:  gate-bypass
    Since:    v1

### INV-07 · Claims about the tree are checked, not asserted

    Applies-to: every artifact
    Forbids:  an artifact asserting a fact about the repository that no
              fact-gathering check verified.
    Evidence: measured in v01 — roughly 70% of findings were mechanical: unresolved
              paths, stale counts, claims about the tree. Checking them before
              interrogation cut one round from 30 tool calls to 13, and what came
              back was reasoning instead of bookkeeping.
    Unlocks:  unfounded-claim
    Since:    v1

### INV-08 · The charter changes only by approved intent

    Applies-to: every artifact, and code
    Forbids:  amending this document, the stack pack, or the pack rules outside
              an approved charter or pack intent.
    Evidence: every other rule here is enforced by being citable, and the rule
              protecting the charter being the sole exception is backwards — it is
              the rule with the most to lose. Without it, a cycle that edits its
              way around an invariant leaves no finding behind.
    Note:     a charter intent proposing a change to THIS invariant must say so in
              its title. The self-reference cannot be closed — this rule lives in
              the document it protects — but quiet removal is the only failure mode
              that matters, so it is made loud instead.
    Unlocks:  charter-violation
    Since:    v1

INV-08 is not part of the seven above. Those seven were selected to make every
category except `unclassified` blockable; this one protects the mechanism itself. Different job,
so it neither strengthens nor weakens the reasoning behind them.

### Coverage

Every finding category except `unclassified` can block, through one of three citation
routes. A `DEC` id is a fourth kind of citation but never a blocking one — see State.

| Category | Blocks via |
|---|---|
| `unresolvable-reference` | failed check — no invariant needed |
| `internal-contradiction` | location pair — no invariant needed |
| `untestable` | failed check: marked verifiable, no test annotation |
| `unfounded-claim` | failed check, or INV-07 |
| `scope-drift` | failed check (plan vs diff), or INV-05 |
| `gate-bypass` | failed check (grep and count), or INV-06 |
| `duplicated-truth` | INV-03 |
| `ambiguity` | INV-02 |
| `missing-case` | INV-01 |
| `charter-violation` | any invariant, INV-04 among them |
| `unclassified` | never blocks, by construction |

---

## Finding taxonomy · v1

This charter is the authority for these values. The findings schema validates
against them; nothing else may restate them.

### Axis 1 — category

| Category | Definition |
|---|---|
| `unresolvable-reference` | Points at a file, section, criterion, invariant or package that does not exist |
| `unfounded-claim` | An assertion about the tree, build or dependency state that is false |
| `scope-drift` | Does more or less than the upstream artifact authorised |
| `gate-bypass` | A suppression, skip, exclusion or disabled check with no written justification |
| `internal-contradiction` | Two parts of the same artifact conflict |
| `duplicated-truth` | A second list beside a source of truth, with no check binding them |
| `ambiguity` | Admits two readings that lead to materially different downstream work |
| `untestable` | Marked verifiable, but stated so that no test could verify it |
| `missing-case` | A scenario, error path or requirement that should be covered and is not |
| `charter-violation` | Makes a choice the charter forbids and fits no other shape |
| `unclassified` | Fits none of the above. **Can never block.** Its rate is a health metric for this taxonomy |

**Tie-break:** when two categories fit, take the more mechanically checkable one.
It yields a citation and a sharper count, and the bias toward blockable findings is
deliberate.

**`charter-violation` is a last resort.** If a finding has a shape, use the shape
and put the invariant in `cites`.

### Axis 2 — attributed stage

`charter` · `intent` · `spec` · `plan` · `build` · `verify`

Which artifact should have prevented this. May be the current stage or an upstream
one; upstream attribution is the high-value signal.

### Axis 3 — detection

`check` — a mechanical check failed. Deterministic and re-runnable.
`reasoning` — an agent inferred it.

### State

Discovery and disposition are separate acts. An interrogator notices that something
is wrong before it knows whether a person is needed, so state is assigned in two
steps rather than guessed at once.

| State | Meaning | Requires |
|---|---|---|
| `open` | discovered, disposition pending. **No cycle may land with any outstanding** | nothing |
| `blocking` | must resolve before landing. Carries a `repair` → self-heal attempts it under no-progress termination; no repair, or repair fails → escalate to a person | non-empty `cites` |
| `suggested` | never halts. Accumulates as evidence in `suggestions/<cycle-id>.md` | nothing |

**Findings from a mechanical check skip `open` entirely.** The failed check *is* the
citation, so there is nothing to triage — they are born `blocking`. Only
`detection: reasoning` findings start at `open`. Since roughly 70% of findings were
mechanical in v01, most never enter triage at all.

**Promotion, not discovery, requires a citation.** An `open` finding cannot become
`blocking` without one; uncited, it becomes `suggested`. This is the anti-inflation
guard, and it belongs at promotion: at discovery an interrogator is pattern-matching,
at triage it is investigating. Asking for a citation before it has looked is asking
it to guess.

`cites` accepts exactly one of: an invariant ID; a failed check ID; a `DEC` id from the
open-decision register; or a location pair of two or more conflicting positions in the
artifact (`internal-contradiction` only).

**A `DEC` id is the one citation kind that never promotes.** It records that a finding was
dismissed against a deliberately open decision — disposition `deferred-to-dec` — so it
satisfies nothing in the guard above: a finding whose only citation is a `DEC` id cannot
reach `blocking`. Without that restriction, pointing at an unsettled question could halt a
cycle, which is the opposite of what the register is for.

**An `open` finding that will not triage is escalated to a person** — two rounds with no
progress and a person looks. They either supply a citation, which promotes it to
`blocking`, or record it as `suggested`. **Nothing reaches `blocking` uncited**, so the
escalation is an event rather than a state change. When in doubt, escalate.

**Category never determines state.** Two rules governing state would fight,
and the easier one would win.

### Disposition

State says where a finding stands and whether it halts the cycle. Disposition says
what was done about it. Note that state is deliberately not a measure of how bad
something is: a trivial unresolved path that fails a mechanical check is `blocking`,
while a deep concern with no citation is `suggested`.
Every finding carries both. Nothing is left without a recorded outcome.

| Disposition | Means | Signal it feeds |
|---|---|---|
| `fixed` | something changed; the finding no longer holds | none |
| `rejected` | the finding was **wrong** — nothing to fix | nothing about the invariant, but a high rate for one role marks that role **noisy** |
| `accepted-wont-fix` | the finding was **right** and we knowingly are not acting | **override count** → the invariant may be too broad |
| `deferred-to-dec` | real, but the subject is a deferred decision | **`DEC` cite count** → that subject needs deciding, not deferring |
| `promoted` | a `suggested` finding became its own intent with `source: suggestion` | none |

`rejected` and `accepted-wont-fix` must never be merged. Dismissing a bogus finding
would otherwise inflate the override count and make a sound invariant look too broad.

**Timing.** A `blocking` finding must be dispositioned before the cycle can land. A
`suggested` finding is dispositioned later, when the suggestions file is triaged —
requiring it at landing would defeat the purpose of suggestions.

**Where they live.** On the finding. Recorded in `verify.md` as of the gate, and
carried in `retro.md` for the corpus — the retro is what travels, so it is what the
amendment signals are computed from. **Never** stored on the `DEC` entry itself: that
would be a second list beside the corpus, and this document would grow with every
cycle.

---

## Interrogator roles

Fan out by *question asked*, never by area. Area roles overlap and dump
deduplication on the merger; question roles read different evidence and rarely
collide — which is also why they have nothing to debate.

| Role | Its one question | Evidence | Fails by |
|---|---|---|---|
| `charter` | Does this contradict an invariant? | this document | over-matching |
| `coherence` | Does this contradict itself? | the artifact alone | missing distant contradictions |
| `facts` | Do its claims about the tree hold? | the tree, via checks | not checking enough |
| `gaps` | What is missing that should be here? | domain reasoning | inventing requirements |

When the artifact under interrogation **is this document**, `charter` is replaced by:

| Role | Its one question |
|---|---|
| `form` | Does each invariant carry a non-empty `Applies-to:` and a non-empty `Forbids:`, and does the `Forbids:` forbid something an implementation could actually do? |

Fact-gathering runs **once**, in the parent, and its results are passed to every
role. Deduplication and state assignment belong to the merger, never to an
individual role.

---

## Open-decision register

**Interrogation must not report anything in this register as a violation.** These
are deliberately unsettled. An interrogator that raises one is producing noise, and
may cite the `DEC` id when dismissing such a finding.

| ID | Deliberately open |
|---|---|
| DEC-06 | Whether a living solution-level spec is ever adopted |
| DEC-07 | Artifact directory organisation once the count is annoying |
| DEC-08 | One real install proving the private-marketplace credential path |

**~~DEC-01~~ through ~~DEC-05~~ are absent deliberately**, struck through here and below so that a
mechanical check reads them as records of absence rather than as declarations. Each was resolved between
2026-09-22 and 2026-09-24, so none is deferred any longer — a register entry exists to
stop interrogation raising a question that is open, and these are answered. The
resolutions are recorded in the design record; the gaps are the record that the entries
existed, under the same rule that governs invariant ids.

| Gap | Was | Resolved as |
|---|---|---|
| ~~DEC-01~~ | retro transport | one manual `absorb-retro`, idempotent by cycle id |
| ~~DEC-02~~ | charter review cadence | five triggers, plus a zero-cite query with a ten-cycle floor |
| ~~DEC-03~~ | who moves `release` | a person, manually, on no cadence |
| ~~DEC-04~~ | concurrency cap | there is none; the launch confirmation is the control |
| ~~DEC-05~~ | a shared invariant set | rejected; per-pack charters instead |

---

## Communication

Artifacts stay indexed. Conversation does not.

When referring to an invariant, a requirement or a finding in prose, **lead with
what it says**; the identifier is trailing detail, never the whole reference. "No
second list without a check that binds them (INV-03)" — not "INV-03".

This cannot be checked, only restated. Its cost is repetition, not implementation.

---

## Calibration period

This charter is deliberately thin. Seven invariants cannot catch much, and the
`cites` rule means anything they do not cover is capped at `suggested`. That is
the correct failure mode — permissive rather than obstructive — and it is
self-correcting, because the suggestions pile is the evidence for which invariants
are missing.

**The consequence: do not run unsupervised until this charter can halt on
something.** Early cycles are calibration runs. Unsupervised mode earns its way in
as the charter thickens.
