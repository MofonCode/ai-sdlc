---
name: implement
description: Change code and tests to do what this skill is handed -- an approved plan, a verification's unmet criteria and findings, or a person's instruction -- recording every deviation from the approved text and every choice the approved text left open, then build, test, commit and record the evidence a verification reads. Never verifies, never opens a pull request. Use when the build or remediate playbook hands it a cycle's work.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id>"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Read, Write, Edit, Glob, Grep
---

# Implement what you are handed

The one skill that writes code (D-186). **It does one job**: make the code and the tests do what it is handed,
record where that bent the approved text, and record the evidence. It does not verify, it does not remediate on
its own account, and it opens nothing: the playbook that handed it the work — `build` or `remediate` — runs the
verification next, in a process that never saw this one (D-01, D-109).

**What you may be handed**, and only one of these at a time:

| Handed | From | Your job |
|---|---|---|
| **the approved plan** | `build` | implement what `plan.md` decided, and nothing it did not |
| **a verification's findings** | `remediate`, on `blocked` | make the code meet every criterion `verify.md` marks Not met and answer every open blocker |
| **a person's instruction** | `remediate`, on `escalated` or at the pull request | make the code say what they asked, in their words, and nothing more |

**Nobody is asked anything here.** What you cannot settle from what was written down — the intent, the
decisions, the constraints, the plan and what you were handed — goes into `deviations.md`, where the report
finds it. A question that only a person can answer is a stop: hand back, and the playbook brings it to them.

## Stay where the playbook put you

You are on the cycle's build branch, or the run's branch (`wave/<id>`, `series/<id>`) that carries it, in the worktree the playbook — or the binary, for `build` — put you in.
Stay there. Everything below measures the change from where this branch diverged, so working on the
integration or default branch directly makes the diff meaningless.

## What you may and may not touch

| | |
|---|---|
| **May edit** | source, tests, and `.sdlc/cycles/<id>/deviations.md` |
| **May never edit** | `intent.md`, `spec.md`, `plan.md`, `approval.md`, `verify.md` |

**`approval.md` is denied at the tool call. The other planning documents are yours to keep.** If a failing test
could cause an agent to adjust the spec, the criteria become whatever the code happens to do — the one inversion
the whole chain exists to stop. Editing one of them makes `sdlc gate` and `sdlc verify-chain` refuse, naming the
document. **You cannot edit your way past a criterion.**

## Read what each perspective asks of a build

```
sdlc perspectives <id>
sdlc perspective show <name> --for build --cover
```

Hardening the plan already decided, applied as you write — validation at the boundary, no secret in source,
the evidence a perspective needs recorded with the rest. **It never widens the plan**: a perspective asking
for something the plan did not describe is a `Noticed:` line, tagged with the perspective —
`Noticed: [security] the token is logged when the call fails.` — so it counts for that perspective when
`sdlc harvest constraints` reads the completed cycles.

## Handed the plan: implement it, and only it

Read `plan.md`. Implement what `Shape and departures` decided and what `Proof` committed to.

**Then run `sdlc decisions --for plan --cycle <id>`, and build to it where the plan is silent.** Where the
solution has areas, a decision outside the plan's areas comes back as its headline; `sdlc decisions show
DEC_nn` prints it in full when the code you are writing reaches it, which is also a sign the change touches
an area its plan did not declare (D-151). Most of what decisions settle — how errors are returned, what is
logged, how time is stored, what things are named — shows only in code. The diff reader at verify reads the
change against the decisions, and code that takes what a decision set aside with no departure declared is a
blocking finding under `CON_27`. **If the work cannot be done without departing, record a `decision`
deviation** quoting the decision, below.

`CON_25` forbids a change neither the plan nor a deviation in force describes, in any file — an unrelated
refactor, a drive-by rename, a fix to something else you noticed in passing. **Creep, in an approved file or a
new one, is what reaches review dressed as approved work.** So when you notice something worth fixing that the
plan did not describe: **leave it, and record it as a `Noticed:` line in the commit message** — a suggestion for
a later cycle, read by whoever reviews the pull request.

**If the plan declares the solution skeleton, run `sdlc scaffold <id>`.** The first cycle of a new solution has
no projects to change yet, so its plan declares the ones the skeleton supplies and the `.NET` build settings they
compile under. The binary writes them, and refuses unless this cycle is approved. **Run it too when the plan
declares a build settings file the repository does not have**; it writes exactly the files the plan's `Files`
section names and leaves alone any already there. **Expect the next build to fail where `Directory.Build.props`
was one of them**: it makes warnings errors, so code this cycle never touched can refuse to compile. Fix what
fails, or narrow the setting in the file with its reason — never a pragma in source, never removing the file —
and record a `plan` deviation for each place the plan's text did not hold.

**Write the tests the plan named.** The proof entries name tests in backticks, and `verify-chain` checks that
each one ran and passed. If a name has to change, record a `proof` deviation quoting the planned proof and
naming the test that replaces it.

## Handed findings: make the code meet the criteria

Two sources, and the job is both:

- **The criteria `verify.md` marks Not met** — why the outcome is `blocked`, and usually **not** findings. Read
  the Criteria section of `.sdlc/cycles/<id>/verify.md` for every entry that is not Met.
- **The open blockers**: `sdlc findings <id> --stage verify`. Each names what is wrong, where, and what would fix
  it.

A pass that answers every finding and leaves a Not met criterion standing has fixed nothing the outcome was
about.

**A finding whose id starts `diff.` is about the change, not the verdict**, and it is yours even when the outcome
reads `pass` (D-139):

- **`CON_25` — the change does something the plan did not describe.** Take it back out. If the approved text
  cannot be followed without it, keep it and record a `plan` deviation answering the finding.
- **`CON_27` — the change takes what a recorded decision set aside, and nothing declared the departure.** Make
  the code keep the decision; if the work cannot be done without departing, record a `decision` deviation.

**When the playbook says the passes have stopped converging**, take a different approach than the approved text
describes, recorded as a deviation that quotes the text it departs from and rests on the finding or the failing
test (D-179). The limits below still hold.

Fix the **cause**, not the symptom:

| Tempting | Why it is wrong |
|---|---|
| loosen the assertion | the criterion still is not met; now nothing says so |
| add a sleep or a retry | a flaky proof gets rerun until green, which teaches a team to ignore red |
| substitute the thing under test | the test then proves the substitution |
| skip the test | a skipped test is not evidence of anything, and the summary records it as skipped |
| widen the test to a case that passes | the criterion was about the case that does not |

The chain checks that each named test **ran and passed**, and skipped counts as neither.

## Handed a person's instruction: do what they said

Their words are the instruction — *change deviation 2: use the platform's retry policy instead*, *revert that
choice* — and the playbook has already made sure they say what to do instead. **Record it on the entry** it
changes, beneath the deviation or choice, in their words:

```markdown
Changed: "use the platform's retry policy instead" — at the person's request
```

If doing what they asked still has to bend the approved text, that is a new deviation with `Replaces: DEV_nn`; a
choice they reverted needs nothing more than the line. A person's words never let you edit the intent, the spec
or the plan, and never widen the cycle.

## When the approved text cannot be followed, record a deviation

Something worth doing is a `Noticed:` line. Something the plan's approved text **cannot be followed without** is
a deviation (D-156): a file the fix has to touch that the plan did not declare, a step that cannot be done as
written, a test whose name has to change, a decision the work cannot keep. Append it to
`.sdlc/cycles/<id>/deviations.md`, one section each:

```markdown
## DEV_01 — The stand-in's socket file is changed to fix a race

Kind: plan
Departs from: plan.md, Files
> src/Quotes.FeedStandIn/StandInHost.cs
Blocker: Credential_never_reaches_a_trader fails 2 runs in 3 with ObjectDisposedException (tests.json)
Deviation: StreamSocket.cs is also changed; DropSockets no longer aborts a disposed connection.
Why: the race is in StreamSocket, and no fix inside StandInHost reaches it.
Files: `src/Quotes.FeedStandIn/StreamSocket.cs`
```

**Three limits, and none bends** (D-179):

1. **Never change what *done* means.** Never a criterion: what *done* means is the spec's. Nothing quotes
   `spec.md`, and never a test weakened so it passes. A criterion the code cannot be made to meet is a stop —
   hand back, and the playbook escalates it so a person can reopen the plan.
2. **Never wider.** No new outcome, no new criterion, no more cases than the approved text reached, no file
   another cycle claims, never the intent. Those are a successor or a reopen, and a person's call.
3. **Never on your own account without end.** How many attempts a cycle gets is the playbook's to judge from
   the binary; you record each one honestly.

- **The quote decides the kind.** `plan` and `proof` quote `plan.md`; `decision` quotes `decisions.md`. Quote the
  approved text word for word. **Nothing to quote means nothing to deviate from** — that is a choice, below.
- **The blocker rests on evidence**: the failing test and its error, or the plan step and why it cannot be done.
- **Short.** A person reads every one in the report before the merge. One line each, the gist first.
- **Ids are never reused.**

**Record a choice nothing approved settled** (D-180). When you choose something the intent, the decisions, the
constraints and the plan left open, and a reviewer would want to know it — a library, a pattern, a default,
behaviour a user would see — append it as kind `choice`. It quotes nothing:

```markdown
## DEV_02 — A failed quote fetch is retried three times, backing off from 200 ms

Kind: choice
Deviation: three retries with exponential backoff, starting at 200 ms
Instead of: failing on the first error, or a fixed delay
Why: the feed drops single requests under load; the plan names retrying and not how
Files: `src/Quotes/QuoteClient.cs`
```

**Not for names, formatting or anything a reviewer would never weigh.** A choice is judged like the rest of the
change, against the recorded decisions (`CON_27`) and the plan's purpose (`CON_25`), so recording one is no leave
to take what a decision set aside.

Check what you wrote — it refuses a quote the approved text does not hold, a kind quoting the wrong document, a
narrowed criterion, and a file another cycle claims:

```
sdlc deviations <id>
```

**When what could not be followed is a gap in the framework itself** — the verify was never handed a file, the
evidence drops one — say so when you hand back; the person files it as a successor marked `framework:` (D-173).

## Build and test, and treat a warning as an error

```
dotnet build
dotnet test --coverage --coverage-output-format cobertura --results-directory TestResults
```

Warnings are errors here, enforced by `Directory.Build.props` rather than by instruction. A build that warns is a
build that failed.

**Coverage is measured, and the merge holds it** (D-103, D-157): at least 70% of the lines this change adds must
run under a test, and the whole codebase must stay at or above the solution's `coverage_floor` or, under it, no
lower than at the base commit. Add the tests that run the changed lines rather than recording and hoping. Code
no test should run is excluded in the solution's coverage settings, and excluding something new is a change to a
rule the plan has to declare.

**A whole-codebase fall is diagnosed before it is chased.** When the whole codebase falls further than the
change could explain, compare this run's report with the base commit's file by file before writing a test: a
fall spread across code the change never touched is a change in how coverage is measured, not missing tests.
**Nobody is here to ask**, so hand back as not finished, with what fell and where as the cause: the report
carries it to the person — lower `coverage_floor` through `sdlc draft solution`, or re-measure the integration
branch and commit the rate to `.sdlc/coverage.json` in a pull request of its own.

**Check that the suite actually ran.** A trial cycle lost its whole verification to `dotnet test` exiting zero
without running anything. If the test count is zero or absent, that is a failure, not a clean run — read the
count rather than the exit code. (`dotnet test --nologo` is one cause on the .NET 10 SDK: the Microsoft.Testing
Platform runner passes it through, and every project reports "Zero tests ran". Drop the flag.)

**A test that fails only sometimes is a fault, never a run to repeat** (D-216). Do not run the suite again until a
run misses it: a green run got that way hides a fault that can stop any cycle's evidence. If this change caused
it, fix the cause. If it did not, hand back as not finished, naming the test and the runs that failed: the report
carries it to the person, who has it fixed in a cycle of its own before this one records evidence again.

**The platform the pipeline tests on decides whether a test passes** (D-216). Where this machine is not that
platform, a failure seen only here is not chased unless it reproduces there; say so in the hand-back, and never
change the code to suit a platform the pipeline does not run.

**The turn end is checked.** A hook asks the gate before you finish, and sends the turn back while the recorded
evidence shows a failing suite, there are changes and no evidence, or the working tree has moved since the
evidence was recorded.

## Commit the change

Commit before recording evidence: `sdlc evidence` refuses code nobody committed (D-124). A message saying what
it does and why, referencing the cycle id, then **one `Noticed:` line each** for anything you noticed and left
alone. Commit `deviations.md` with the change: every verify reader reads it, and a change that departs from the
plan with no deviation describing it is blocked as an undeclared one.

## Record the evidence

```
sdlc evidence <id> --tests TestResults --build-log <path to the build log> --coverage TestResults
```

**Handed a person's change on a cycle that passed, add `--retire-pass`**: only a person's request retires a
pass, and without the flag `sdlc evidence` refuses it. On a blocked verdict, recording retires it and says so
(D-161): do not delete `verify.md` yourself.

It writes the diff, the two commits it is measured between, every results file, one normalised test summary,
the coverage — carrying the solution's baseline in `.sdlc/coverage.json` forward when it held — and
`timings.json` (D-169). **Verify reads only these files**, which is what `CON_22` relies on.

**Point `--tests` at the directory, not at one file**, and check the count of results it read against what the
run reported: a single path records one project and silently drops the rest.

It also prints the declared-against-changed comparison — `undeclared`, `undelivered`, `governing` — and **none
of the three is a refusal**. Undeclared is usually creep to take back out; undelivered may be over-declaration
or a part not implemented; **a `governing` line** — the change touched a file that judges it — **belongs in
the commit message**, because the person reviewing the pull request is the one who can say whether the rule
should have moved.

Then **commit the evidence directory** and `.sdlc/coverage.json` on the branch: `verify` and the pull-request
check read them from the repository. If `sdlc evidence` refused because the patch came out empty while files
changed, that is a real defect — `git status --short` is where to look.

## Hand back

```
sdlc status <id> --brief
```

Show it verbatim, then **at most five lines of your own**: what you implemented or changed, what the build and
the tests said, and **how many** `Noticed:` lines, deviations and choices the commit carries — not restated; the
report lists every deviation and choice. **Name everything by what it says, with its id trailing at most.**

**Then stop.** The playbook runs the verification. **If the build or the tests failed, say so plainly**: do not
run verify on a red build, and do not adjust a test to make it pass — a failing build is a build to fix.
