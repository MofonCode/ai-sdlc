# Global constraints

The rules every solution governed by this framework is judged against. They live here, in the
library, and are never copied into a solution — an interrogation assembles the effective set at run
time from these, plus the profile's own, plus the solution's `.sdlc/constraints.md`.

**Citation.** Every blocking finding cites exactly one constraint, by id and revision.

| Tier | Id | Resolves in |
|---|---|---|
| global | `CON_nn` | this file |
| profile | `PRO_nn` | the stack profile package |
| solution | `LOC_nn` | the solution being worked on |

**Ids are a monotonic sequence.** Assigned once, never reused, never backfilled. A removed
constraint leaves a gap, and the gap plus the commit that made it is the record.

**A change to what is forbidden is a new constraint.** The old one keeps its id, stays on this page
verbatim, and gains `Superseded-by` — so a finding that cited it remains readable against the rule
it was actually judged under.

**A change to the wording alone is a revision.** `CON_01` becomes `CON_01.2`: same `Forbids` clause,
better words. Findings cite the revision, so a round judged under older wording stays
distinguishable from one judged under newer wording. A constraint with no revision suffix is
revision 1.

**Four parts, all required.** A constraint missing any of them is invalid rather than untidy, and
the suite fails on it:

| Part | Holds |
|---|---|
| heading | the id, and a one-line statement of the rule |
| `Applies to` | which artifact, and at what moment |
| `Forbids` | what becomes illegal, named concretely enough that an example could be written |
| `Why` | what goes wrong if the rule is absent — the cost it avoids, never a restatement of the rule |

**What belongs here.** A constraint is a rule about an **artifact** that needs reading comprehension
to apply. Three tests, all of which must pass:

1. Its `Forbids` clause names something that becomes illegal
2. No analyzer, schema or hook could decide it
3. It is incompatible with at least one plausible implementation

Rules about code go to analyzers, and where an existing analyzer already decides one, that analyzer
is used rather than a rule written. Mechanical rules about artifacts go to schemas or hooks. A rule
that survives every implementation is decoration however true it sounds — see `CON_11`, which exists
to keep this list from becoming that, and which applies to itself.

**Stack neutrality.** These bind solutions on any stack, so none of them may name a language,
framework or package. Stack specifics live in the profile and in analyzers.

---

## CON_01 — An intent states the problem, not the solution

**Applies to:** an intent, at approval.

**Forbids:** an intent whose body names an implementation and no observable problem — "add a
cache", "make the export asynchronous" — with nothing said about what is wrong today.

**Why:** the spec derives its criteria from the intent. An intent that names a solution gives
the spec nothing to test except that the solution now exists, and the problem it was meant to
solve is never checked. A decision already taken is legitimate; it belongs in the intent's fixed
decisions with attribution, not as the intent's content.

## CON_02 — An intent's unknowns are recorded as open questions, not answered by guess

**Applies to:** an intent, at approval.

**Forbids:** an intent stating as settled fact a figure, cause or behaviour that nobody has
measured or verified.

**Why:** every later phase treats an approved intent as settled. A guessed figure becomes a
criterion, then a test, then a green verification of something nobody established. An intent
with three open questions is sound; an intent with three invented answers is a liability.

## CON_03 — A criterion states an observable outcome and how it is checked

**Applies to:** a spec, at approval, and any criterion added later.

**Forbids:** a criterion that cannot fail — "handles large files", "performs well", "works
correctly", "is secure" — or one naming an outcome with no way to observe it.

**Why:** an unfalsifiable criterion is satisfied by construction. Build implements it, verify
passes it, and nobody ever learns whether it was met. A word-list check catches the common
phrasings and belongs in a hook; this covers the general case, which no word list reaches.

## CON_04 — A spec covers every outcome its intent asks for

**Applies to:** a spec, at approval.

**Forbids:** a spec whose criteria satisfy some of the intent's stated outcomes while leaving
others with no criterion at all. An intent's **must not change** items are among those outcomes,
and they are the ones most often dropped — dropping one is how a compatibility break lands green.

**Why:** a gap here is invisible downstream. Build implements the spec and verify checks the
spec, so an omitted outcome is never mentioned again and the cycle lands green having done part
of the job.

## CON_05 — A spec introduces no scope its intent did not ask for

**Applies to:** a spec, at approval.

**Forbids:** a criterion that no statement in the intent supports.

**Why:** scope added at spec time was never approved by whoever approved the intent, yet it
arrives carrying the authority of an approved artifact. Gaps and creep fail differently and are
remedied differently — a gap sends you back for more criteria, creep sends you to delete one —
so they are separate constraints rather than one rule about correspondence.

## CON_06 — A plan's proof is adequate to the criterion it claims to prove

**Applies to:** a plan, at approval.

**Forbids:** a plan naming a check that could pass while its criterion is unmet.

**Why:** a schema can require that every criterion has some proof named. Only judgment can tell
whether "add a unit test" proves "returns within 200ms over 50,000 rows". An inadequate proof is
worse than none, because it produces a green verification instead of an obvious gap.

## CON_07 — A change does only what its plan declared, including inside files the plan named

**Applies to:** a diff, at verification.

**Forbids:** a change within a declared file that the plan did not describe — an unrelated
refactor, a drive-by rename, a fix to something else noticed in passing.

**Why:** the file set is mechanical and a hook already enforces it. Creep *inside* an approved
file passes that hook and reaches review dressed as approved work, which is precisely where it
is least likely to be questioned.

## CON_08 — A verify claim rests on evidence that exists and supports it

**Applies to:** a verify document.

**Forbids:** a statement in a verify document with no cited build output, test result or diff
hunk — or one citing evidence that does not establish what the statement claims.

**Why:** verify is the only artifact whose entire value is being derived. A claim reaching beyond
its evidence turns an auditor into an author, and nothing downstream can tell which one wrote it.

## CON_09 — No artifact restates another's content as its own authority

**Applies to:** every artifact.

**Forbids:** an artifact reproducing a constraint, a criterion or an outcome that another
artifact owns, instead of citing it.

**Why:** two copies diverge, and nothing says which is binding. Citation keeps exactly one owner
per fact, which is the only arrangement that stays true without maintenance.

## CON_10 — An accepted finding's reason says why the finding is acceptable

**Applies to:** an approval decision that lists accepted findings.

**Forbids:** an accepted finding whose reason restates the finding, cites schedule pressure
alone, or says only that the finding is understood.

**Why:** accepting a finding at the gate is the one point where a person overrides the machine.
The reason is the only control on that override, and it is what a reader has six months later
instead of the conversation that produced it.

## CON_11 — Every constraint carries a Forbids clause that rules something out

**Applies to:** a diff that adds or changes a constraint, at any tier.

**Forbids:** a constraint compatible with every possible implementation, or one whose `Forbids`
clause restates its statement instead of naming what becomes illegal.

**Why:** a rule that forbids nothing changes no plan and no diff. It costs attention at every
interrogation and buys nothing. This is the constraint that keeps the list from growing into
decoration, and it applies to itself.

## CON_12 — A criterion's check is one the solution's testing layers can run

**Applies to:** a spec, at approval.

**Forbids:** a criterion whose only named check is a person observing something — on a device, in
an app store, on hardware the suite cannot reach.

**Why:** verify derives its verdict from the recorded test results and diff, and `CON_08` forbids
a claim with no cited evidence. A criterion with no machine-runnable check has no entry in the
test results, so admitting one forces a choice between verify adjudicating on attestation and
`pass` no longer meaning every criterion was met. The outcome belongs in the intent as a bound,
verified by a person outside the cycle, where it is honest about not being governed.
