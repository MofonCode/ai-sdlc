# Global constraints

The rules every solution governed by this framework is judged against. They live here, in the
library, and are never copied into a solution — an interrogation assembles the effective set at run
time from these, plus the profile's own, plus the solution's `.sdlc/constraints.md`.

**Citation.** Every blocking finding cites exactly one constraint, by id and revision.

| Tier | Id | Resolves in |
|---|---|---|
| global | `CON_nn` | this file |
| profile | `PRO_nn` | the profile `solution.md` records, carried by the binary |
| solution | `LOC_nn` | the solution being worked on |

**Ids are a monotonic sequence.** Assigned once, never reused, never backfilled. A removed
constraint leaves a gap, and the gap plus the commit that made it is the record.

**A change to what is forbidden is a new constraint.** The old one keeps its id, stays on this page
verbatim, and gains `Superseded-by` — so a finding that cited it remains readable against the rule
it was actually judged under.

**A change to the wording alone is a revision.** `CON_01` becomes `CON_01.2`: same `Forbids` clause,
better words. The new wording replaces the old here — unlike a supersession, a revision does not
leave its predecessor on the page. A constraint with no revision suffix is revision 1.

**A citation resolves to the number; the suffix says which wording its author read.** `CON_07` and
`CON_07.2` are the same rule. A revision cannot change what is forbidden — that is what separates it
from a supersession — so a finding raised under either was judged against the same `Forbids` clause,
and nothing about the objection changes. Older wording is in git, and the commit that revised a
constraint is where to read what it said before.

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

**Superseded-by:** CON_16

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

## CON_07.2 — A change does only what its plan declared, including inside files the plan named

**Applies to:** a diff, at verification.

**Forbids:** a change within a declared file that the plan did not describe — an unrelated
refactor, a drive-by rename, a fix to something else noticed in passing.

**Why:** the file set is mechanical, and it is reported as a signal at the gate rather than
enforced anywhere. Creep *inside* a declared file does not show up in that signal at all, and
reaches review dressed as approved work, which is precisely where it is least likely to be
questioned.

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

## CON_10.2 — A gate refusal's reason says what has to change

**Applies to:** a gate decision that is not an approval — changes requested, denied, or a standing
approval reopened.

**Forbids:** a reason that restates the finding, cites schedule pressure alone, or says only that
the finding is understood.

**Why:** the reason is the only thing the author has to work from, and it is what a reader has six
months later instead of the conversation that produced it. The subject changed in iteration 4 —
nothing is accepted at the gate any more, because a blocking finding cannot be waived — and the
rule did not: a refusal that says nothing is a refusal nobody can act on.

**Applied by a person, not by an interrogation.** Every other rule here is handed to an interrogation
of one artifact, matched by the artifact's name. This one's subject is a gate decision, which is not
an artifact and never will be — the approval ledger is the one file only a person writes, so that an
approval means something. So nothing hands this rule to anybody: a person reading the ledger applies
it, at the gate or six months later. The ledger reader checks that a refusal carries a reason at all;
whether the reason says what has to change is a judgement, and this is where the judgement is written
down. Said here rather than left as an absence, because a rule nothing applies and nothing admits to
not applying is decoration, which `CON_11` exists to prevent.

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

## CON_13 — A plan that departs from the declared defaults says why

**Superseded-by:** CON_15

**Applies to:** a plan, at approval.

**Forbids:** a plan that reaches for a package, pattern or layer the solution's `techstack.md` does
not declare, without saying in `Shape and departures` what the declared default would have cost
here.

**Why:** the declared stack is how a team's choices survive the person who made them. No constraint
may name a framework, so nothing stops a plan departing — and nothing should: a default that cannot
be departed from is a cage, and the first genuinely unsuitable case would see the whole file
ignored. What must not happen is departing in silence, because then the stack file and the codebase
disagree and only the diff knows. Naming the cost makes the departure arguable at the one gate
before code exists.

## CON_14 — An open item is one nobody can resolve yet

**Applies to:** an intent, a spec or a plan, at interrogation.

**Forbids:** recording under `Open` something that a person could answer by being asked, that the
artifact's own author could settle by deciding, or that is simply work nobody has started.

**Why:** the open region is the compliant exit for a question that genuinely cannot be settled at
this stage, which is what keeps a constraint from deadlocking an artifact. That makes it the one
place where asserting nothing satisfies every other rule, so without this an artifact can be
emptied into it — every constraint passes, and a person reading a body with nothing in it has
nothing to object to. No mechanical check can tell "cannot be resolved" from "has not been", which
is why this is a constraint rather than a schema rule.

## CON_15 — A plan says why it departs from a declared default, in either direction

**Applies to:** a plan, at approval.

**Forbids:** a plan that reaches for a package, pattern or layer the solution's `techstack.md` does
not declare, or that leaves one it declares unapplied in work that calls for it, without saying in
`Shape and departures` what the declared default would have cost here — or, for an unapplied
default, what applying it would have cost.

**Why:** a default nobody applies and nobody mentions is indistinguishable from a default nobody
has. `CON_13` covered one half of departing: a plan adding something undeclared had to argue for
it, while a plan silently skipping something declared breached nothing — so the rule caught the
author who was adding and missed the author who was leaving out, which is both the cheaper mistake
and the harder one to see afterwards. The solution then ships out of conformance with its own stack
file, and the first person to notice is whoever needed the thing that was never there. Saying why
is still enough: this makes an omission arguable at the one gate before code exists, not forbidden.
The scope is work that calls for the default, because a plan touching one endpoint is not silent
about the message bus by failing to mention it.

## CON_16 — A spec covers every outcome its intent asks for, and all of each one

**Applies to:** a spec, at approval.

**Forbids:** a spec that leaves one of the intent's stated outcomes with no criterion at all, or
whose criterion for an outcome satisfies only part of what that outcome says — with the remainder
neither carried by another criterion nor named in `Out of scope`. An intent's **must not change**
items are among those outcomes, and they are the ones most often dropped.

**Why:** a gap here is invisible downstream. Build implements the spec and verify checks the spec, so
what the intent asked for is never read again and the cycle lands green having done part of the job.
`CON_04` caught an outcome dropped whole and not an outcome quietly narrowed, which is the commoner
of the two and much the harder to see: a criterion covering half an outcome still traces to it, still
reads as derived, and still satisfies a coverage check. One real spec turned "see a die on the screen
showing a number between 1 and 6" into "displays a face value that is an integer from 1 to 6" — the
same sentence with the thing the person asked to look at taken out of it. Nothing objected, and the
solution shipped a numeral in a bare paragraph tag. Naming the remainder in `Out of scope` is the
exit, and it is what stops this deadlocking a spec whose outcome has a part no test can reach.

## CON_17 — A spec says what somebody is told when the action fails

**Applies to:** a spec, at approval.

**Forbids:** a spec carrying a criterion for an action somebody initiates and none for what that
person or caller is told when it does not succeed.

**Why:** the happy path is what a spec is written from, because it is what the intent describes, and
the unhappy path is what the people using the thing actually meet. Nothing downstream asks for it
either: build implements the spec, so a failure no criterion mentions is a failure nobody is required
to handle, and the handling that does arrive is whatever the implementer happened to think of. The
shape it takes is silence — a control that does nothing when the request behind it fails is
indistinguishable, to the person pressing it, from an application that is broken, and it passes every
criterion a spec of successes can hold. One real solution shipped exactly that: five criteria, all of
them met, and a button that did nothing at all when the api was unreachable. This asks for one more
criterion, not a design: being told nothing is a legitimate answer if a spec says so out loud.

## CON_18 — A plan that changes a rule the cycle is judged by says what the rule would have cost

**Applies to:** a plan, at approval.

**Forbids:** a plan declaring a change to a file that holds a rule this cycle is judged by — an
architecture test, a build property file, a banned-symbols list, a hook configuration, the pipeline —
without saying in `Shape and departures` what satisfying the rule unchanged would have cost here.

**Why:** the architecture suite is the only mechanical enforcement of shape the framework has, and a cycle
may amend it as ordinary declared work. That is right: a rule that cannot be amended is a cage, and the
first genuinely unsuitable case would see it edited in silence or the whole suite ignored. What must not
happen is amending it without argument, because then the one check that cannot be talked around is simply
rewritten by the thing it was checking. `CON_15` already asks a plan to say why it departs from a declared
default; this asks the same of a plan that changes the rule instead of meeting it. One real cycle turned a
project-reference rule into roughly 3,100 lines across nine projects for a single button, rejected the
simpler shape citing that rule, and then amended the rule anyway — and the amendment reached a reviewer as
one row among thirty-nine in a file table, with nothing said about what the rule had cost.

## CON_19 — A plan says why it departs from a decision the solution has recorded

**Applies to:** a plan, at approval.

**Forbids:** a plan whose approach contradicts a standing entry in the solution's `decisions.md` —
taking the alternative that entry weighed and set aside — without saying in `Shape and departures`
what the recorded decision would have cost here.

**Why:** a decision is recorded so a later cycle does not argue it again from nothing, and the cost of
the record going unread is not that somebody disagrees with it. It is that nobody knows they are
disagreeing. The entry names the alternatives that were real and what each would have cost, so a plan
reaching for one of them is either correcting a decision that has stopped fitting or repeating an
argument somebody already had and lost — and those need opposite responses from the person at the gate.
Without this the two are indistinguishable, because both arrive as a plan that simply does the other
thing. **Departing stays legal**, as it does under `CON_15`: a decision that cannot be revisited is a
cage, and the first case it genuinely does not fit would see the record ignored rather than amended. What
this forbids is departing in silence. No analyzer can decide it — whether an approach contradicts a
paragraph of prose is a judgement about language, and the record's own `Instead of` clause is what makes
the judgement possible rather than a guess.