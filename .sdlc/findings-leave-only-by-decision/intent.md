---
slug: findings-leave-only-by-decision
title: A finding leaves the record by being decided, or it does not leave
status: draft
tier: feature
created: 2026-09-19
owner: peter
---

# intent — a finding leaves by decision, or not at all

## Problem

**Both mechanisms this loop needs already exist in this repository. Neither is required, and a
finding has escaped through each of them.**

The charter records the loop as *a defect found becomes a permanent named check*, and records
what is missing: *"the trigger, not the mechanism. Every one of those conversions happened
because somebody noticed."* This is that intent. What the completed chain shows is narrower and
more fixable than a missing mechanism: two mechanisms that work when used and are used by
choice.

### Escape one — a finding vanishes between rounds

Rounds two, three and four of `init-scaffolds-solution` each carry a section nobody specified —
`### Closed since the Nth verification, and checked rather than taken` at `:672`, `:1157`,
`:1723`. Each tables the previous round's findings and their state now, naming the check that
closed each, and recording carries and promotions: `S6 → now B4`.

Rounds five and six carry no such table. And a finding left:

```
round 1   S8  — The emitted integration suite never starts the composed system
round 2   S13 — same finding, renumbered
round 3   S13 — ... (third round)
round 4   S13 — ... (fourth round)
round 5   — absent
round 6   — absent
```

Never closed, never refused. Even the requirement-coverage row that pointed at it in every
earlier round — *"see S13 on what it exercises"* at `:292`, `:751`, `:1241`, `:1876` — silently
drops the pointer at `:2500` and `:2981`. A reader of the final verdict cannot tell it existed.

### Escape two — a decided finding evaporates at the ship gate

Round six raised `S13` — *the new checks have no row in the charter's silent-failure table* — and
disposed of it correctly at `:3161`, ranked third under **What I would fix next**, with the
reason and what would close it. Under any sane set of dispositions that is a well-written "still
open."

**The chain shipped, and it stayed open.** Four hours later an interrogator found it still
unfixed and it was closed in charter v5, whose amendment log records it as evidence for this
intent rather than its fix.

Nothing failed to be judged. What failed is that a ranked, reasoned "I will fix this next" lived
only in a ranked list inside a 3,200-line document nobody reopens after the verdict.

**And the destination already exists.** The charter carries a **Known and unchecked** section —
*"recorded rather than hidden"* — holding two items today. It is exactly where a finding that
ships unfixed belongs. Nothing routes anything to it.

### One problem, two faces

A finding can leave by being forgotten between rounds, or by being remembered, ranked, and then
shipped past. Both mechanisms for preventing that exist. Both are voluntary. Both were skipped
in the same chain, in the same week, by careful reviewers doing careful work.

### Two designs were drafted and rejected, and the reasons bound this one

**Severity is not the trigger.** All five blocking findings converted to a named check in the
round they were raised.

**Recurrence is worse than nothing.** A rule firing when a finding is raised *again* gives a
finding that *stops* being raised a free exit — dropping becomes cheaper than refusing, and
escape one is exactly that.

## Affected users and systems

- **`verify`**, which ends a chain without asking what became of anything, and whose gate
  section treats should-fix findings as shippable without asking where they go.
- **The verifier agent**, whose output template names no accounting section, so three reviewers
  invented one and two did not.
- **The verification schema**, which lists required sections and lists neither.
- **The charter's Known-and-unchecked section**, which is the right destination and has no
  inbound path.
- **A reader of the final verdict**, who cannot distinguish a finding that was fixed, one
  deliberately accepted, and one forgotten.

## Proposed outcome

**A finding leaves the record by one of three exits, and by no other.**

- **closed** — naming the check that now catches it;
- **refused** — pointing at where the owner recorded the decision not to fix it;
- **carried as a known gap** — recorded in the charter's Known-and-unchecked section, which
  outlives the chain.

Two rules follow, one per escape:

**Every round accounts for every finding open to it** — inherited and its own. "Absent" is not an
exit, and neither is re-listing with no state attached.

**A chain does not ship with an open finding that has no exit.** A finding still open at the
verdict takes the third exit or it is fixed. Ranking it in prose is not an exit; the ranked list
is not read again after the verdict, which is the whole of escape two.

Binding a round to the findings it *raises*, not only those it inherits, is what closes the hole
the last round falls through — the last round has no successor and is where open findings
concentrate.

### Who writes which exit

- **The verifier writes what it can observe from artifacts** — closed, and still open. It reads
  earlier rounds' findings from the committed record, which is reading an artifact and is what
  fresh context is *for*.
- **A refusal is the owner's and the verifier cannot witness it.** The precedent is the dated
  *"Accepted drift"* section in `init-scaffolds-solution/plan.md`, written after approval below
  an untouched manifest. A later round reads that and records **refused**.
- **A known gap is the owner's too**, and it lands in the charter, which is already gated by
  amendment.

An agent asserting a human decision it never saw is the failure this split prevents.

## Success criteria

- **S1.** The verification schema requires the accounting section, the verifier agent's output
  template emits it, and a named check fails when the two disagree.
- **S2.** The permitted exits are a closed, enumerated set in the schema. "Carried" and "absent"
  are not among them.
- **S3.** The verify skill instructs the verifier to read earlier rounds' findings before
  writing, and names where they are.
- **S4.** The verify skill's gate section requires every still-open finding to have taken an exit
  before the verdict is acted on. Replaying round six: its register finding takes the known-gap
  exit at ship rather than a ranking in prose.
- **S5.** Replaying the completed chain, there is no legal round five that omits round four's
  integration-suite finding.
- **S6.** A produced record declares the schema version it was written against, and a check
  validates only records declaring one.
- **S7.** Nothing is written automatically. The rule shapes the record and reports an omission; a
  person supplies every refusal, every known-gap row, and every check.

**What no check here can reach, stated rather than implied:** whether an exit is *honest*. A
verifier can write "closed" and name a check that does not exist, or one that cannot fail —
round six flagged exactly that case. The schema holds the shape; whether a model fills it
truthfully is the eval suite's question, and counting it as covered would be the vacuity this
repository keeps catching.

## Constraints

- **The charter must be amended before the spec is written.** This adds a silent-failure class
  and gives the Known-and-unchecked section an inbound contract. Precedent is this repository's
  own, twice.
- **S6 overturns a committed design decision rather than honouring it, and says so.**
  `VerificationRecordTests` states that it *"deliberately never read a produced verification.md:
  records describe what was true when they were written, and validating old ones against a newer
  schema would make history fail."* A version-gated check crosses that line. The justification is
  that the rationale is about *history*, and a version stamp makes history unreachable by
  construction rather than by avoidance — but it is a reversal, and an earlier draft of this
  intent dressed it as compliance, which is the error being corrected here.
- **The record's shape is changing underneath this.** `verification-reports-options` is approved
  and adds two required sections. This is a third and must land on top of it.
- **The verifier stays memoryless about the conversation.** Reading earlier findings from a
  committed record is reading an artifact. Being told what the last round concluded by the
  session that acted on it is not. Both clauses of the charter's rule bind any dispatch.
- **Advisory or blocking is bounded by the charter's rule** that nothing hard-blocks unless the
  owner opts in — and S4 is a gate in a skill rather than a hook, so whether that invariant binds
  by analogy is a design question.

## Non-goals

- **Not a recurrence rule, and not a severity rule.** Both drafted, both rejected against this
  record, reasons in the Problem section so nobody re-derives them.
- **Not a check that every shipped test has a register row.** Four of the six unregistered checks
  arrived with a task-tier change, which gets no verification at all, so no rule binding
  verification rounds can reach them. That gap is real, it is cheaply and mechanically checkable
  by reading the suite against the charter, and it is **a separate change** — folding it in here
  would let this intent claim evidence it does not address.
- **Not a judgement about whether a named check is real.** The charter's standard is that every
  register row is mutation-tested; enforcing that is a different instrument.
- **Not retroactive.** The completed chain is read as evidence and is not amended.
- **Not covering findings from the intent stage.** The charter's sentence says *"the verifier
  **or the interrogator**"*, and interrogator findings have no record — five interrogations of
  this intent produced findings that exist only in transcripts. Named in Open questions rather
  than quietly halved.
- **Not the retrospective, not cross-repository, not project management.**

## Open questions

- **What identifies a finding across rounds?** *Owner: Design — the largest risk in the change.*
  Numbering restarts every round: `S8`, then `S13`, then renumbered twice more. The only thing
  that ever joined them is one hand-written row. "Account for every open finding" is not
  checkable, **even by a person**, without an identity that survives renumbering. **If Design
  cannot produce one, this change does not work and should be stopped rather than shipped as
  prose.**
- **Does the third exit make shipping harder than it should be?** *Owner: peter.* A chain with
  eight should-fix findings now needs eight charter rows or eight fixes. That is either the rule
  working or the rule being routed around, and which one decides whether this survives contact.
- **Do interrogator findings need the same treatment?** *Owner: peter.*
- **What does a single-round chain owe?** *Owner: peter.* Most chains will have one round; under
  this draft it disposes of its own findings and inherits nothing.
- **Advisory, blocking, or blocking on opt-in?** *Owner: peter.*
