---
slug: register-names-every-check
status: approved
finished: true
spec: none
tier: task
slices:
  - id: register-check
    files: ["tests/AiSdlc.Tests/SilentFailureRegisterTests.cs", ".sdlc/CHARTER.md"]
    requirements: [K1, K2, K3, K4]
    depends_on: []
---

# plan — the register and the suite check each other

**Tier: task, stated out loud.** Two files, behaviour agreed in conversation, no design question
open. The charter edit is one row.

**No spec, so no R-numbers.** Requirements are `K1`–`K4`, from the conversation of 2026-09-19.

## Why

Charter v5 added six rows for checks that had existed for hours and in one case for days.
`VerificationRecordTests` shipped four checks at `525a4f1` with no row at all. Two others were
raised by the sixth verification of `init-scaffolds-solution`, which had no seventh round, and
the chain shipped with the finding open.

I9 says *every known silent-failure class has a named check in CI* and that the set *grows
whenever a new class is found*. **It does not say who notices, and nothing compares the register
against the suite in either direction.**

This is the cheap, mechanical half of that. The expensive half — *does this new check cover a
new failure class* — is a judgement and is not in scope; see **What this does not catch**.

## Requirements

- **K1.** Every test class in the suite is named by at least one row of the register, by class
  name or by the name of one of its methods.
- **K2.** Every check the register names exists in the suite, as a class or as a test method.
- **K3.** Check names are read from the register table's **Check** column only. Any backticked
  identifier elsewhere in the charter is not a check name — `depends_on` appears in backticks in
  the plan schema discussion and is not an instrument.
- **K4.** The check is itself named in the register. An instrument that enforces registration and
  is not registered is the defect it exists to prevent.

## What this does not catch, stated rather than implied

**A new method added to an already-registered class, covering a new failure class.** Two of the
six gaps charter v5 closed were exactly that: `EveryPackAsset_IsDeclaredByItsManifest` and
`EngineInstructions_NameNoPackFile` joined `PluginStructureTests`, which was already named in
the register by other methods. K1 is satisfied by the class's existing rows and says nothing
about the new ones.

**So this closes four of the six gaps it was motivated by, not six.** The other two need someone
to judge that a new check covers a new class of failure, which no parser can do. That judgement
is the subject of `findings-leave-only-by-decision`, which is unapproved and currently blocked on
an unsolved problem.

Claiming six here would be the overclaiming that six interrogations of that intent kept finding.

## A tension worth surfacing rather than resolving quietly

**The charter schema and I9 disagree about whether the register may grow while shipping.**

`artifact-schemas.md` says of the charter: *"amending it is its own approval, never a side effect
of shipping a feature."* I9 says its table *"grows whenever a new class is found"* — which, in
practice, is while shipping the change that found it. Versions 4 and 5 both did precisely that.

This plan adds one row and proceeds, because that is what I9 prescribes and the row is the
instrument's own registration. But the two sentences cannot both be right, and a change that
routes more traffic to the charter would hit this harder — which is why the sixth interrogation
of `findings-leave-only-by-decision` blocked on it. Recorded here as evidence, not fixed here.

## Work order

One slice, one writer. The test and the row it requires are each other's precondition.

**`tests/AiSdlc.Tests/SilentFailureRegisterTests.cs`** (new) — parses I9's table out of
`CHARTER.md`, taking the second column of each row and extracting backticked identifiers from
it; enumerates test classes and `[TestMethod]` names from `tests/AiSdlc.Tests/*.cs`; asserts both
directions with a non-vacuity guard on each, in the shape this suite already uses — a count
asserted above zero with a message ending *"asserted nothing"*.

New file rather than an addition to `PluginStructureTests`, which is about the marketplace's own
structure and already carries ten methods. This is about the charter and the suite agreeing.

**`.sdlc/CHARTER.md`** — one row for the new check, per K4, and a bump with an amendment-log
entry naming the tension above.

## Tests

| Proves | Assertion |
|---|---|
| K1 | Every discovered test class appears in the register by class or method name; fails naming the class. |
| K2 | Every register-named check resolves to a class or method in the suite; fails naming the row. |
| K3 | Names come from the Check column; a backticked identifier in charter prose does not register a phantom check. Asserted by the K2 direction passing today, since `depends_on` would otherwise dangle. |
| K4 | The new check's own name appears in the register. |
| non-vacuity | Both directions assert they compared something, or fail saying they asserted nothing. |

**Mutation-test all four** per I9: delete the row for a class and watch K1 fail; add a row naming
a check that does not exist and watch K2 fail; point the parser at the whole charter rather than
the Check column and watch `depends_on` dangle; remove the new check's own row and watch K4 fail.

## Risks

**A test class that genuinely is not a silent-failure instrument would be forced into the
register.** Every one of the ten today is, so the risk is future. *Early-warning sign:* someone
adds an ordinary unit-test class and has to invent a failure class to satisfy K1. If that
happens, the rule is wrong and needs an exemption with a written reason, not a fabricated row —
and a fabricated row is worse than no rule, because it dilutes a register whose value is that
every entry is real.

**The register's format becomes load-bearing.** It is a markdown table today, parsed by nothing.
After this, a formatting change breaks the build. *Early-warning sign:* a charter edit failing a
test for reasons that read as pedantic. That is the cost of making prose checkable and it is the
trade this repository has made repeatedly.

## Rollback

`git revert` of one commit. The register returns to being maintained by attention.
