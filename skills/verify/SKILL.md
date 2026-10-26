---
name: verify
description: Adjudicate a build against its spec, from the recorded evidence alone, in a fresh process that did not see the build. Use after a build has recorded its evidence. Never edits code.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id>"
allowed-tools: Bash(sdlc *), Bash(claude *), Read, Write, Glob, Grep
---

# Verify a build against its spec

Verify is the only artifact whose entire value is being **derived**. A claim that reaches beyond its
evidence turns an auditor into an author, and nothing downstream can tell which one wrote it.

## The verdict is written in a fresh process

**This session does not adjudicate.** It assembles what the adjudicator reads, starts it, and records
what it returns. The session running this skill is very often the one that built the change — the build
stage hands straight to this one, and `batch-build` runs both in one agent — and a verdict written by the
author of the code is the author grading their own work (D-01). It is not hypothetical: a cycle's
building session wrote *every criterion met* over 22 criteria, and then argued one of the four its
interrogation found back to met by refining the verdict, with no change to the code. A fresh adjudicator
overclaims too, and the interrogation still catches that. What it does not have is a stake in defending
the verdict once it is challenged.

```bash
sdlc constraints --for verify > <scratch>/constraints.txt
sdlc perspectives <id>
sdlc perspective show <name> --for verify > <scratch>/perspective-<name>.txt   # for each one listed as written
# compose <scratch>/verify-prompt.txt, below, with your Read and Write tools
SDLC_READER=1 claude -p --output-format text < <scratch>/verify-prompt.txt > <scratch>/verify.out.md
```

The process is started the way `interrogate` starts a reader, for the same reasons: a new process rather
than a subagent, so every host runs one path; the prompt piped in, never passed as an argument; and
`SDLC_READER=1`, so a red build's turn-end hook lets the adjudicator's turn end with its verdict (in
PowerShell, `$env:SDLC_READER = '1'` before the call). **Never switch the hooks off to get it through.**

**The prompt is, in this order:** the sections of this skill from *Read the evidence, and nothing else*
through *Write it*, verbatim, as its method; `constraints.txt`; `spec.md`; `plan.md`; each perspective's
`--for verify` output; and the evidence — `tests.json`, `diff.patch` and `base.sha` pasted in, with the
paths of `build.log` and the runner's own output for it to read when the summary is not enough. Last, the
instruction: *write `verify.md` for cycle `<id>` and return only that document.* Nothing else — not the
conversation, not what the build set out to do, not this session's view of how it went. Every one of
those is the bias the process was opened to remove.

**Record what it returns, byte for byte**, as `.sdlc/cycles/<id>/verify.md`. If the output is not a
verify document — prose around it, a refusal, a question — start it again; do not repair it yourself,
because the repair is this session writing the verdict after all. Then `sdlc status <id>` checks its
shape.

The sections from here to *Write it* are the adjudicator's method. They speak to it.

## Read the evidence, and nothing else

In `.sdlc/cycles/<id>/evidence/`:

| File | What it is |
|---|---|
| `tests.json` | the normalised test summary: name and outcome per test |
| `diff.patch` | the change itself. The cycle's own artifacts are **not** in it — their absence is by design, not a missing commit |
| `base.sha` | the commit the diff is measured from |
| the runner's own output | kept alongside, for when the summary is not enough |
| `build.log` | what the build said |

**Read `tests.json` rather than running the tests.** If you run them yourself you are producing new
evidence rather than adjudicating the build's, and the next reader cannot tell which run your verdict
describes. If the evidence is missing, say so and stop — that is a build that did not finish, not a
verification waiting to happen.

**Do not read the source to decide whether a criterion is met.** The diff tells you what changed. The
tests tell you whether it works. Reading the implementation and concluding it looks correct is the
single most tempting way to write a claim with no evidence behind it.

## Adjudicate criterion by criterion

Read `spec.md` for the criteria and `plan.md` for the proof committed to each. Then, for each
criterion in turn:

1. Which test did the plan name for it?
2. Does `tests.json` say that test ran and passed?
3. Does that test, as the diff shows it, actually establish the criterion?
4. **What would let that test pass while the criterion is unmet?** Answer it before you write Met. A test
   that asserts a value was handed to a library, where the criterion is about what the person saw; a
   keyboard test that moves focus with `FocusAsync` instead of a key; a failure test that checks one of
   the four failure paths. If you can name such a way and the diff shows nothing closing it, the
   criterion is **not met**.

Question three is where judgement lives, and where you may disagree with the plan. A test that ran and
passed but does not reach its criterion is **not met** — say so, cite the test and the diff lines, and
let the finding stand. That is `CON_06` caught late rather than never, and it is worth more here than a
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

`CON_08` forbids a statement with no cited build output, test result or diff hunk — and equally forbids
one citing evidence that does not establish what the statement claims.

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

## Then run the loop, and stop

*From here the skill speaks to the session again.*

Interrogate the verify document like any other artifact — `CON_08` applies to it, and the rounds it
accumulates are also what counts remediation passes.

**A round's blocking findings are answered by a new adjudication, not by an edit.** Changing a verdict
is adjudicating, so it happens where adjudicating happens: start a fresh adjudicator with the same prompt,
and add the current `verify.md` and the open findings as `sdlc findings <id> --stage verify` prints them,
asking it to answer each finding from the evidence — standing by a claim and citing why is an answer,
as is changing it. Record what it returns, as above. Do not run `refine` on a verify document: a
criterion moved from Not met to Met by this session's argument is the self-grading this skill exists to
prevent, however good the argument.

```
sdlc status <id>
sdlc pr-ready <id>
```

Show both verbatim. Then **at most two lines of your own**, and only the outcome in your own words.
Then stop.

**Nothing that is not already in `verify.md`.** You have just written the document that is the
adjudication; a hand-back that re-argues it produces a second, shorter verdict with no hash and no
round, and a reader who disagrees with one cannot tell which one the chain carries.

- **Passed?** The person decides whether to open a pull request.
- **Blocked?** The person decides whether to remediate. Do not start remediating; you
  have just finished judging, and the separation is the point.
