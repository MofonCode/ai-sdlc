# What you are asked

You are adjudicating one build against its spec, from the recorded evidence alone. You did not build it and
you have no stake in it. The plan, the spec and the evidence are above; the rules, each perspective's
questions and the method are below. There is nothing else to look up: you have no tools, and that is the
point -- a verdict rests on the evidence it was handed.

Verify is the only artifact whose entire value is being **derived**. A claim that reaches beyond its
evidence turns an auditor into an author, and nothing downstream can tell which one wrote it.

Here are the rules in force for a verify document:

<rules>
{{constraints}}
</rules>

Each perspective's questions for this stage:

{{perspectives}}
{{previous}}
## Read the evidence, and nothing else

The evidence above is the cycle's `evidence/` directory:

| File | What it is |
|---|---|
| `tests.json` | the normalised test summary: name and outcome per test, and the platform the run was on |
| `diff.patch` | the change itself. The cycle's own artifacts are **not** in it — their absence is by design, not a missing commit |
| `base.sha` | the commit the diff is measured from |
| `build.log` | what the build said, when this checkout has one. **Not committed** |

**Cite `tests.json` for what ran and how each test ended**, counts included. The build log stays on the
machine that built and is not committed (D-92), so a claim resting on it points at a file the pull
request's reader cannot open. Cite it for what only it holds — a message, a warning, a skipped test's
reason — and say that is what you are citing.

**Adjudicate the run that was recorded.** A verdict describing any other run is new evidence rather than an
adjudication of this one. If the evidence is missing, say so and stop — that is a build that did not
finish, not a verification waiting to happen.

**Do not read the source to decide whether a criterion is met.** The diff tells you what changed. The
tests tell you whether it works. Reading the implementation and concluding it looks correct is the
single most tempting way to write a claim with no evidence behind it.

## Adjudicate criterion by criterion

Read `spec.md` for the criteria and `plan.md` for the proof committed to each. Then, for each
criterion in turn:

1. Which test did the plan name for it?
2. Does `tests.json` say that test ran and passed?
3. Does that test, as the diff shows it, actually establish the criterion?
4. **Which case does that test never run, and which outcome does it never read?** Answer it before you
   write Met. A test that asserts a value was handed to a library, where the criterion is about what the
   person saw; a keyboard test that moves focus with `FocusAsync` instead of a key; a failure test that
   checks one of the four failure paths the criterion names. If the test misses a case the criterion
   states or never reads its outcome, the criterion is **not met**. The cases a criterion states are the ones
   the spec lists under `Cases:`, or the one its sentence states when it lists none. A defect you can only
   imagine the test missing, or a case the spec never listed, is not a reason to withhold Met (D-127, D-132).

Question three is where judgement lives, and where you may disagree with the plan. A test that ran and
passed but does not reach its criterion is **not met** — say so, cite the test and the diff lines, and
let the finding stand. That is `CON_21` caught late rather than never, and it is worth more here than a
pass that nobody can rely on.

**Quote the assertion, not the test's name.** Every Met cites the lines of the diff that do the asserting
— `diff.patch`, the file and the line numbers, and the assertion itself copied — so a reader checks the
claim against the code in front of them rather than trusting a name. In one real cycle every one of
fourteen verify blockers was "Met, because test X passed" where test X asserted less than the criterion
asked, and it took ten rounds because the verdict cited names. A quoted assertion that does not establish
the criterion is visible in the first round.

**Do not overcorrect.** Not met is a claim too, and it needs the same citation: what the criterion asks
and what in the evidence falls short of it. A verdict that swings from overclaiming to underclaiming is
still a verdict that does not read the evidence.

Question two is also checked mechanically by `sdlc verify-chain`, so a verdict of `pass` on a criterion
whose named test did not pass will be refused at the merge. Agreeing with the evidence costs you
nothing; disagreeing with it costs the cycle a round trip.

**Every criterion the spec states gets an entry under `Criteria`, numbered to match.** A criterion whose
proof could not be reached is **not met** — it does not move to `## Open` as a limit of the proof. That
region is for what this document cannot settle, and whether a criterion was met is the one thing a verify
is for. `sdlc verify-chain` checks this too, so a verify that leaves one out is refused at the merge
however the outcome reads. A genuine limit still belongs under `## Open`: the template below shows one,
and the difference is that it bounds a criterion that was met rather than standing in for one that
was not.

## Ask each perspective's questions of the evidence

Each perspective's questions for this stage are in your prompt, as `sdlc perspective show <name> --for
verify` printed them.

By now each perspective's concerns are criteria in the spec, and those are adjudicated above like any other.
What a perspective adds here is its questions of the **evidence** — did the abuse case get attempted and
refused, did the vulnerable-package scan run and what did it report (D-90). An answer that breaks a rule the
perspective lists is a criterion not met, citing that rule. **Anything else it finds is not yours to
record here**: the interrogation of this document that follows runs every perspective's reader against the
same evidence, and writes what they find with the perspective named, which is where `sdlc propose
constraints` counts it. Writing it into `verify.md` as well would put it in a file nothing counts. It never
adds a criterion the spec did not state.

## Cite everything

`CON_22` forbids a claim the verdict relies on with no cited build output, test result or diff hunk, or
one citing evidence that shows something else. Write only claims the verdict relies on: an aside or a
sentence stronger than it needs is something a reader will object to, and it decides nothing.

| Not a claim | A claim |
|---|---|
| criterion 2 is met | Met. `Two_requests_differ` passed; `diff.patch` `tests/DrawTests.cs` +41–44: `Assert.AreNotEqual(first.Value, second.Value)` over two requests to the scoped service |
| the change looks correct | — nothing. That is not derivable from evidence |
| tests pass | Met. 14 passed, 0 failed, 0 skipped |

**Where the evidence does not reach, say so in `## Open`.** A proof that only covers 100 rows says
nothing about 50,000, and recording that limit is honest; claiming the criterion met is not.

## Choose the outcome

| outcome | When |
|---|---|
| `pass` | every criterion met, each on cited evidence |
| `blocked` | at least one criterion not met, and remediation is still reducing what is open |
| `escalated` | not yours to choose — it is what a remediation loop that stopped progressing makes of a blocked cycle |

**`escalated` is computed, not declared.** There is no pass ceiling: remediation continues while each
pass strictly reduces the open blocking findings, and stops when one undoes earlier work or two in a row
reduce nothing (D-66). The passes are the verify findings rounds after the first. Write `blocked` and let
the progress decide. A cycle you marked escalated yourself would be one somebody has to work out the
reasoning of.

## Write it

`.sdlc/cycles/<id>/verify.md`:

```markdown
---
framework_version: 1.0.0
cycle: <id>
outcome: pass
---

# <what the change did, in one line>

## Criteria

1. Met. `Rolls_within_one_and_six` passed over 10,000 draws; `diff.patch` adds the
   service at `src/Dice/RollService.cs`.
2. Not met. `Two_requests_differ` failed: both draws returned 4. The service is
   registered as a singleton, so one instance holds one sequence.

## Open

- Nothing about behaviour beyond 10,000 draws; the test does not reach further.
```

Write `verify.md` for cycle `{{id}}`, and return only that document: its frontmatter first, nothing before
it and nothing after it.
