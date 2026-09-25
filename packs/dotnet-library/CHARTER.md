---
version: 1
solution: TEMPLATE
pack: dotnet-library
pack-version: 1
origin: TEMPLATE — init replaces this line with how the copy was made
since: TEMPLATE
---

# Charter — TEMPLATE

> **This is a pack charter: a template.** `init` copies it into a solution and replaces
> every `TEMPLATE` above. It is not in force anywhere by itself.

This is the authority every artifact in the solution is interrogated against. It says what
work there must **obey**. It does not describe what the solution does — that is the code,
the tests, and the traceability in each `verify.md`.

    Stack pack: dotnet-library@v1

That line is **charter content**, not metadata. Bumping it changes every rule governing the
solution at once, so it takes a `charter-review` intent in that solution — the same gate as
any other charter change, for the same reason.

---

## How this document works

**A copy, never an inheritance.** This charter was copied from the pack at `init` time and
is now the solution's own. An upstream pack change does not reach it, and that is the point:
inheritance would let someone else's edit alter the rules this solution is judged by without
its approval. The cost is drift between solutions, which is accepted.

**Invariants are immutable and append-only.** IDs are assigned once, never reused, never
renumbered — **including across the copy from this pack.** A changed conclusion is a *new*
invariant that supersedes the old one; the original keeps its ID and gains `Superseded-by`.

A solution that drops an inapplicable invariant leaves a **gap**, and the gap is itself the
record that something was removed. Local additions use `LOC-nn`, numbered independently.
Renumber into a vacated slot and `cites: INV-04` means different things in two solutions on
this pack, which breaks aggregation across the retro corpus.

**Every invariant carries a non-empty `Forbids:` and a non-empty `Applies-to:`.** This is the
acceptance test made mechanical:

> If a rule is compatible with every possible implementation, it is not a rule.
> If a rule does not change at least one downstream plan or diff, it is decoration.

Excluded from invariants: tool names, framework preferences, style rules and package
versions. Those live in the pack — `pack.yaml` for facts, `pack.rules.yaml` for rules — and
INV-04 is what makes every one of them citable.

**The taxonomy, the state model and the disposition set are engine-owned and are not restated
here.** `schemas/findings.schema.json` is their authority. A pack charter that copied them
would be a second list with nothing binding it, and the engine's vocabulary is shared by
every solution using it whether this document repeats it or not.

**There is exactly one way to change this document, and no process can take it.**

> The charter changes only through a **charter intent** that a **human approves**. No
> automated process may approve one. No cycle may amend the charter governing it.

The same protection covers the pack and the pack rules, because INV-04 delegates the
substance of its rule there.

---

## Invariants

### INV-01 · Error paths are enumerated

    Applies-to: intent, spec
    Forbids:  an intent or spec that describes behaviour without its failure
              modes.
    Evidence: the cheapest defect to prevent and the most expensive to discover
              in production. Reaches intents as well as specs deliberately: a
              missing case in an intent propagates into the spec, the plan, the
              code and the tests.
    Unlocks:  missing-case
    Since:    v1

### INV-02 · One reading per statement

    Applies-to: intent, spec, plan
    Forbids:  a statement that admits two readings leading to materially
              different downstream work.
    Evidence: ambiguity resolved silently by an agent is a decision nobody made.
    Unlocks:  ambiguity
    Since:    v1

### INV-03 · No second list without a binding check

    Applies-to: every artifact, and code
    Forbids:  introducing a second representation of a fact without, in the same
              change, the check that fails when the two disagree.
    Evidence: the failure is silent — correct on the day it is written, wrong
              weeks later, and nothing looks at it. "In the same change" is the
              load-bearing part: the check must exist before the drift window
              opens.
    Scope:    this forbids representations that can DRIFT. A copy of an
              immutable, append-only artifact is not a second list, provided the
              mechanism making the copy refuses to overwrite with differing
              content.
    Unlocks:  duplicated-truth
    Since:    v1

### INV-04 · The declared stack governs

    Applies-to: intent, spec, plan, code
    Forbids:  a runtime, framework or package choice that contradicts
              `pack.yaml`, or a change that violates a rule in
              `pack.rules.yaml`, unless this charter records the deviation.
    Evidence: defaults only save work if deviating from them is visible. ONE
              invariant makes every pack rule citable, so no invariant is ever
              added per tech decision and tool names stay out of this document.
    Scope:    what the solution SHIPS AND RUNS. Reading, running or porting
              existing work in another language is research, not a runtime
              choice, and is never a violation.
    Unlocks:  charter-violation
    Since:    v1

### INV-05 · Plans declare their files

    Applies-to: plan
    Forbids:  a plan that does not enumerate the files it will touch.
    Evidence: makes scope drift mechanically detectable rather than a judgment,
              and is what the blast-radius escalation compares against. The same
              declaration serves both.
    Unlocks:  scope-drift
    Since:    v1

### INV-06 · No unjustified gate bypass

    Applies-to: code, verify, pack
    Forbids:  a warning suppression, a disabled diagnostic, a skipped test, or an
              excluded path without a written justification.
    Evidence: a zero-warning build is a strong signal because it is binary. The
              failure mode is not warnings appearing — it is suppressions
              accumulating until the zero means nothing. Stated without tool
              syntax: the spellings for this stack are PR-04's, which INV-04
              makes citable.
    Unlocks:  gate-bypass
    Since:    v1

### INV-07 · Claims about the tree are checked, not asserted

    Applies-to: every artifact
    Forbids:  an artifact asserting a fact about the repository that no
              fact-gathering check verified.
    Evidence: most findings are mechanical — unresolved paths, stale counts,
              claims about the tree. Checking them before interrogation returns
              reasoning instead of bookkeeping.
    Unlocks:  unfounded-claim
    Since:    v1

### INV-08 · The charter changes only by approved intent

    Applies-to: every artifact, and code
    Forbids:  amending this document, `pack.yaml`, or `pack.rules.yaml` outside
              an approved charter or pack intent.
    Evidence: every other rule here is enforced by being citable, and the rule
              protecting the charter being the sole exception is backwards — it
              is the rule with the most to lose. Without it, a cycle that edits
              its way around an invariant leaves no finding behind.
    Note:     a charter intent proposing a change to THIS invariant must say so
              in its title. The self-reference cannot be closed, but quiet
              removal is the only failure mode that matters, so it is made loud.
    Unlocks:  charter-violation
    Since:    v1

INV-08 is not one of the seven above. Those seven make the blockable finding categories
blockable; this one protects the mechanism itself.

---

## Interrogator roles

Fan out by *question asked*, never by area. Area roles overlap and dump deduplication on the
merger; question roles read different evidence and rarely collide.

| Role | Its one question | Evidence | Fails by |
|---|---|---|---|
| `charter` | Does this contradict an invariant? | this document | over-matching |
| `coherence` | Does this contradict itself? | the artifact alone | missing distant contradictions |
| `facts` | Do its claims about the tree hold? | the tree, via checks | not checking enough |
| `gaps` | What is missing that should be here? | domain reasoning | inventing requirements |

When the artifact under interrogation **is this document**, `charter` is replaced by `form`,
which judges each invariant's shape rather than its content: does it carry a non-empty
`Applies-to:` and a non-empty `Forbids:`, and does the `Forbids:` forbid something an
implementation could actually do?

---

## Open-decision register

**Interrogation must not report anything in this register as a violation.** These are
deliberately unsettled; an interrogator that raises one is producing noise, and may cite the
`DEC` id when dismissing such a finding. A `DEC` citation never promotes a finding to
`blocking`.

| ID | Deliberately open |
|---|---|
| DEC-01 | TEMPLATE — `init` records what this solution has not yet decided |

---

## Calibration period

This charter is deliberately thin, and anything the invariants do not cover is capped at
`suggested`. That is the correct failure mode — permissive rather than obstructive — and it
is self-correcting, because the suggestions pile is the evidence for which invariants are
missing.

**Do not run unsupervised until this charter can halt on something.** Early cycles are
calibration runs.
