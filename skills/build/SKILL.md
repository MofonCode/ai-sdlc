---
name: build
description: Carry an approved cycle to its pull request -- admit the plan, build and test it in a process of its own, record the evidence, verify, remediate while it is working, open the pull request, and report every deviation, choice and concern. Use when a person says build it, after a cycle's three planning documents are approved.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[<cycle id>]"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Bash(claude *), Read, Write, Edit, Glob, Grep
---

# Build an approved plan, through to its pull request

The first stage that writes code, and the first one a person is not expected to watch. **Saying *build it*
carries the cycle all the way** (D-178): the plan admitted, the code written and tested, the evidence recorded,
a verification by a process that never saw the build, remediation while it is working, and the pull request
opened — then one report of everything a person should know before they merge.

**This skill has two halves.** In a person's session it hands the cycle to a process of its own and reports
what came back. Inside that process — started by the `batch-build` skill, for this one cycle — it is the stage
itself: everything from *Inside the build's own process* down. You are in that process when `batch-build` is
running this stage; otherwise you are in a person's session.

## Which cycle

**Given no cycle id, ask the binary which one is meant** (D-142):

```
sdlc cycles --here --for build
```

It exits 0 printing the one cycle ready for this step, and that is the cycle. Otherwise it lists the
candidates, or says none is ready and why: ask the person which, in one question, and **never pick one
yourself** — the cycle a skill acts on is not a guess.

## The gate decides, not you

```
sdlc gate <cycle id>
```

Ask it where the plan is — in the sandbox it was planned in, or on the branch it was admitted to. If it
refuses, stop and show what it said. Do not work around it and do not ask the person to approve so you can
continue — the refusal names what is missing, and the next step belongs to them.

There are twelve ways it refuses, and each has a different next step: no cycle, abandoned, a missing
document, nobody approved, an approval ledger it cannot read, a refusal standing, a findings round it cannot
read, a document never interrogated, a document changed since it was approved, a blocking finding still open,
a round that judged a document which has moved since, or a findings round the approval anchored that has
been rewritten. Read which one. In a
solution that declares `approval: none` the gate asks for no approval, and every other refusal stands.

**A blocking finding cannot be waived.** If that is the refusal, there is no flag and no override. The
finding is answered, or the cycle does not advance.

## Keep a task list, if your host has one

If your host gives you a task list, open one once the gate has said the build may run, with these
items, and mark each done as it finishes. If it does not, carry on without one — nothing below depends
on it.

1. admit the plan, when it is only in its sandbox
2. start the build's own process
3. hand back its report

**This stage only.** Nothing the build's process does goes on this list — it runs where your list cannot see
it, and its outcome is what `sdlc report` and `sdlc status <id>` print, not a tick.
**The list is a view, never the record.** What is done is what is on disk and what the binary reports, so where the two disagree the
list is the one that is wrong.

**Inside a batch, keep none.** When the batch-build skill is running this stage, its list has one line
for this cycle and that line is the whole of what this stage shows.

## Admit the plan, when it is only in its sandbox

A plan is written in a sandbox — a folder of its own beside the repository, never merged — and the build
starts from a branch cut from the integration branch. **Moving the approved plan onto one is yours, not the
person's** (D-178): it is mechanical, local, and decides nothing. In the sandbox the plan was written in:

```
sdlc admit
```

Run the one git command it prints: it takes the sandbox's approved cycles onto an admission branch beside it,
from the integration branch, and pushes nothing. It names on standard error what stays behind. **Skip this**
when the plan is already on a branch the build can start from — admitted earlier, or merged by a plan pull
request — or when the solution has no integration branch, where the build starts from the sandbox itself.
`sdlc branch <id> build` says which: it refuses a plan that is only in a sandbox, naming `sdlc admit`.

## Start the build in a process of its own

**The build never runs in the person's session** (D-178). It reads a great deal of code, and in a session that
also carried the planning conversation every turn after it would pay for that again; and a builder that did not
sit through the planning works from what was written down — the intent, the decisions, the constraints and the
plan — which is the separation every reader already has. From the repository's root:

```
sdlc claude -p "/sdlc:batch-build <id> --pr --unattended" < /dev/null
```

It is the factory's own path, for this one cycle: a branch of its own, the stage below, the verification in a
fresh process, remediation while it is working, and — once the verification passes — the pull request into the
integration branch. **It asks nothing**: saying *build it* was the decision, and the plan's hand-back said what
it would do. Leave `--pr` out when `git remote` prints nothing; the build then stops at a verified local branch.

- **Start it in the background where your host can, and wait for it to end.** A build takes as long as it
  takes; a foreground call that times out leaves a process nobody is reading. Tell the person, in one line,
  that the build has started and that you will report when it ends.
- **No more permission than the batch skill's own.** Never pass a mode that skips the host's permission checks
  on a person's machine; the factory does that inside a container that exists for it, and this is not one.
- **On a host without `claude`**, any non-interactive invocation of the host that loads these skills will do,
  as for the readers. With none, say so and stop: the build is not run in this session instead.

## Hand back the report

When the process ends:

```
sdlc report <id>
sdlc status <id> --brief
```

Show both verbatim, and the pull request's link from what the process printed. **The report is the hand-back**
(D-181): every deviation the build recorded, every choice it made that nothing approved settled, and every
concern its verification raised without blocking — each by what it says. It leads the pull request too, so the
person reads the same thing in both places.

**Close with one plain offer**: *review the pull request — merging accepts it, report and all; to change
anything in it, say what instead* (or `/sdlc:remediate`). **If the build escalated**, its pull request is a
draft, and the report says what stopped it; the person decides what happens next. Name no new session: the
build already had one of its own.

---

# Inside the build's own process

Everything from here is the stage itself, run by `batch-build` in the process this skill started, or in the
factory's. Nobody is watching, and nobody is asked anything: what the build could not settle from what was
written down goes into `deviations.md`, where the report finds it.

## Work on the cycle's build branch

`batch-build` has already put you on `cycle/<id>/build-unattended`, created from the integration branch or the
admission branch the plan is on; stay there. Everything below measures the change from where this branch
diverged, so building on the integration or default branch directly makes the diff meaningless.

## Read what each perspective asks of a build

```
sdlc perspectives <id>
sdlc perspective show <name> --for build --cover
```

Hardening the plan already decided, applied as you write — validation at the boundary, no secret in source,
the evidence a perspective needs recorded with the rest. **It never widens the plan**: a perspective asking
for something the plan did not describe is a `Noticed:` line, as anything else is — **tagged with the
perspective**, `Noticed: [security] the token is logged when the call fails.`, so it counts for that
perspective when `sdlc propose constraints` reads the history.

## Implement the plan, and only the plan

Read `plan.md`. Implement what `Shape and departures` decided and what `Proof` committed to.

**Then run `sdlc decisions --for plan --cycle <id>`, and build to it where the plan is silent.** Where the
solution has areas, a decision outside the plan's areas comes back as its headline; `sdlc decisions show
DEC_nn` prints it in full when the code you are writing reaches it, which is also a sign the change touches
an area its plan did not declare (D-151). The plan describes an approach;
most of what decisions settle — how errors are returned, what is logged, how time is stored, what things
are named — shows only in code. The diff reader at verify reads the change against the decisions, and code
that takes what a decision set aside with no `Departs from DEC_nn` line in the intent or the plan is a
blocking finding under `CON_27`. **If the work cannot be done without departing, record a `decision`
deviation** quoting the decision, below; the report shows it, and the merge is where a person accepts it.

`CON_25` forbids a change neither the plan nor a deviation in force describes, in any file — an unrelated
refactor, a drive-by rename, a fix to something else you noticed in passing. The file set is a signal,
not a contract; **creep, in an approved file or a new one, is what reaches review dressed as approved work**, which
is exactly where it is least likely to be questioned. The diff reader at verify judges it, by what each
change is for rather than by which files it touched.

So when you notice something worth fixing that the plan did not describe: **leave it, and record it as a
`Noticed:` line in the commit message.** That is a suggestion for a later cycle, not a thing to slip in —
and the commit message is where it survives. Your report is read once, by whoever is at the keyboard; the
commit is read by whoever reviews the pull request, which is the person who can act on it.

## When the approved text cannot be followed, record a deviation

Something worth doing is a `Noticed:` line. Something the plan's approved text **cannot be followed
without** is a deviation (D-156): a file the fix has to touch that the plan did not declare, a step that
cannot be done as written, a test whose name has to change, a criterion no finite proof meets, a decision
the work cannot keep. Do the work, and write down where you bent the plan, in
`.sdlc/cycles/<id>/deviations.md`, appended, one section each:

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

- **The quote decides the kind.** `plan` and `proof` quote `plan.md`; `criterion` quotes `spec.md` and may
  only **narrow** — add `Successor: <slug> — what the dropped part makes possible`; `decision` quotes
  `decisions.md`. Quote the approved text word for word. **Nothing to quote means nothing to deviate from**:
  a choice the approved text left open is yours to make — and, when a reviewer would want to know it, a
  **choice**, below, rather than a deviation.
- **The blocker rests on evidence**: the failing test and its error, or the plan step and why it cannot be
  done. Never your say-so.
- **Never the intent, never wider.** No new outcome, no new criterion, no more cases than the approved text
  reached, and no file another cycle claims. Those are a successor or a reopen, and a person's call: stop
  and hand back.
- **Short.** A person reads every one in the report before the merge. One line each, and the gist first.
- **Ids are never reused.** A second attempt at one a person asked to change is a new deviation with
  `Replaces: DEV_nn`.

**Record a choice nothing approved settled** (D-180). When you choose something the intent, the decisions,
the constraints and the plan left open, and a reviewer would want to know it — a library, a pattern, a default,
behaviour a user would see — append it to the same file as kind `choice`. It departs from no approved text, so
it quotes none:

```markdown
## DEV_02 — A failed quote fetch is retried three times, backing off from 200 ms

Kind: choice
Deviation: three retries with exponential backoff, starting at 200 ms
Instead of: failing on the first error, or a fixed delay
Why: the feed drops single requests under load; the plan names retrying and not how
Files: `src/Quotes/QuoteClient.cs`
```

**Not for names, formatting or anything a reviewer would never weigh.** A choice is judged like the rest of the
change: the diff reader holds it against the recorded decisions (`CON_27`) and the plan's purpose (`CON_25`), so
recording one is no leave to take what a decision set aside.

Check what you wrote before you commit it — it refuses a quote the approved text does not hold, a kind
quoting the wrong document, and a file another cycle claims:

```
sdlc deviations <id>
```

Commit `deviations.md` with the change. Every verify reader reads it, and a change that departs from the plan
with no deviation describing it is blocked as an undeclared one.

**If the plan declares the solution skeleton, run `sdlc scaffold <id>`.** The first cycle of a new
solution has no projects to change yet, so its plan declares the ones the skeleton supplies — a solution
file, a core project, an HTTP project, two test projects and a reference slice — and the `.NET` build
settings they compile under: `.editorconfig`, the three props files and `global.json`. The binary writes
them, and refuses unless this cycle is approved; transcribing several hundred lines of template through
a plan document would invite corruption and prove nothing. Everything after that first cycle you write
yourself.

**Run it too when the plan declares a build settings file the repository does not have** — on a
solution adopted into existing code, that is the only thing it writes. It writes exactly the build
settings files the plan's `Files` section names and leaves alone any that are already there, so do not
write one of them by hand from memory: the binary's copy is the one the framework ships.

**Expect the next build to fail where `Directory.Build.props` was one of them.** It makes warnings
errors, so code this cycle never touched can now refuse to compile. That is what the plan declared and
a person approved, and the plan said which way it goes: fix what fails, or narrow the setting in the
file — a severity in `.editorconfig`, a line in `WarningsNotAsErrors` with its reason. **Not a pragma
in source, and not removing the file**: either would deliver something other than what was approved.
If the fallout reaches files the plan did not predict, fix them and record a `plan` deviation for each
place the plan's text did not hold.

**Write the tests the plan named.** The proof entries name tests in backticks, and `verify-chain`
checks that each one ran and passed. A test with a different name than the plan committed to will fail
that check even if it tests the right thing — so if the name has to change, record a `proof` deviation
quoting the planned proof and naming the test that replaces it.

## Build and test, and treat a warning as an error

```
dotnet build
dotnet test --coverage --coverage-output-format cobertura --results-directory TestResults
```

Warnings are errors here, enforced by `Directory.Build.props` rather than by instruction. A build that
warns is a build that failed.

**Coverage is measured, and the merge holds it** (D-103, D-157): at least 70% of the lines this change adds
must run under a test, and the whole codebase must stay at or above the solution's `coverage_floor` or,
under it, no lower than at the base commit. The plan's proof is what gets you there; if the changed lines
come up short, add the tests that run them rather than recording and hoping. Code no test should run —
orchestration wiring, generated code — is excluded in the solution's coverage settings, and excluding
something new is a change to a rule the plan has to declare and justify.

**A whole-codebase fall is diagnosed before it is chased.** The baseline is the last rate a merged cycle
recorded, not a target, so never tell the person the build is "aiming for" it. When the whole codebase
falls further than the change could explain, compare this run's report with the base commit's file by
file before writing a test: a fall spread across code the change never touched is a change in how coverage
is measured, not missing tests. Stop and tell the person what fell and where, and that the answer is
theirs: lower `coverage_floor` through `sdlc draft solution`, or re-measure the integration branch and
commit the rate to `.sdlc/coverage.json` in a pull request of its own.


**Check that the suite actually ran.** This is not paranoia: a trial cycle lost its whole verification
to `dotnet test` exiting zero without running anything, because the runner was not configured. If the
test count is zero or absent, that is a failure, not a clean run.

It happens more than once and for different reasons, so read the count rather than the exit code. A
second case, found on the .NET 10 SDK: `dotnet test --nologo` is not an option the Microsoft.Testing
Platform runner accepts, so the flag is passed through to the test application, which then runs
nothing — every project reports "Zero tests ran" in under a fifth of a second. Drop the flag.

**The turn end is checked.** A hook asks the gate before you finish, and it sends the turn back in three
cases: the recorded evidence shows a failing suite, there are changes and no evidence recorded at all, or
the working tree has moved since the build was recorded.
It is not a rule to remember — it is why there is no point reporting done over a red build.

## Commit the change on the branch

Commit before recording evidence. Message saying what it does and why, referencing the cycle id.

**Anything you noticed and left alone goes here, one `Noticed:` line each**, after the body. A file the
plan did not declare and you had to add is not one of these: it is a deviation, in `deviations.md`. This is
the destination for everything the stage was not asked about: it reaches the reviewer, and it keeps the
hand-back to the stage's own result.

**Why this order.** `sdlc evidence` refuses code nobody committed (D-124). It records the commit the build
is at as `head.sha`, and the patch is measured between that and where the branch diverged — committed
state, which is what a reviewer reads and what the pull-request check rebuilds. `diff.patch` itself is not
committed: git holds the change, and the chain rebuilds the patch from the two commits.

## Record the evidence

```
sdlc evidence <id> --tests TestResults --build-log <path to the build log> --coverage TestResults
```

It also prints the coverage of the changed lines and of the whole codebase, and carries the solution's
baseline in `.sdlc/coverage.json` forward when it held — commit that file with the evidence.

This writes the diff, the two commits it is measured between, every results file the run produced, and one
normalised test summary merged from all of them. **Verify reads only these files.** A verdict derived
from your recollection of the build is a verdict nobody can check, which is what `CON_22` forbids.

It also keeps `timings.json` — how long the run took and its 25 slowest tests — and prints the wall time
and the five slowest (D-169). Commit it with the evidence. No reader is handed it: it is there for whoever
asks where the time goes, never as a criterion's proof.

**Point `--tests` at the directory, not at one file.** Every test project a run covers writes its own
results file, so a single path records one project and silently drops the rest — a cycle recorded 14 of
26 results that way, and the 12 that vanished were the architecture tests. The command prints how many
results it read and from how many files; **check that count against what the run reported.** If a
project is missing there, its tests are missing from what the verification will read.

It also prints the declared-against-changed comparison. Read it, and read it correctly:

- **`undeclared`** means something reached the diff that the plan did not name. Usually that is the
  creep described above, and the honest move is to take it back out.
- **`undelivered`** means a file the plan named went untouched. Sometimes that is right — the plan
  over-declared. Sometimes it means part of the plan was not implemented.
- **`governing`** means the change touched a file that judges it: an architecture test, a build
  property, the banned list, a hook, the pipeline, `techstack.md`, this solution's constraints. Unlike
  the other two it appears **whether or not the plan declared it**, because a declared one is the
  common case and still the one worth a reviewer's attention. Changing a rule is not the same kind of
  act as changing the work, and the two reach review looking identical without this line.
- **None of the three is a refusal.** The comparison cannot see inside a file, which is why `CON_25`
  exists at all. What blocks is outcome-based: the build fails, tests fail, or a named test did not pass.

**A `governing` line belongs in your report and in the commit message.** The person reviewing the pull
request is the one who can say whether the rule should have moved, and they will not find it by reading
a file table.

## Commit the evidence

Commit the evidence directory too. `verify` and the pull-request check both read it from the
repository, so evidence that exists only in your working tree is evidence the chain cannot see.

If `sdlc evidence` refused because the patch came out empty while files had changed, that is a real
defect rather than a step to work around — it means git cannot see the change the comparison can.
`git status --short` is where to look.

## Report, and leave the rest to the batch

**Name every finding, deviation, criterion and rule by what it says, with its id trailing at most.** Whoever
reads it has not read the round: *"the die has no label (S_04)"*, never *"S_04"* alone.

```
sdlc status <id> --brief
```

Show it verbatim. Then **at most five lines of your own**: what you implemented, what the build and the tests
said, and **how many** `Noticed:` lines, deviations and choices the commit carries. **Do not restate them** —
the report the person reads at the end lists every deviation and choice, and the commit carries the `Noticed:`
lines.

**Then stop after the evidence: the batch carries on** (D-159, D-178). It runs the verification in a fresh
process that never saw the build, remediates while each pass is working, and opens the pull request once it
passes. The verdict is never this process's, which is what keeps the separation (D-01, D-109).

**If the build or the tests failed, say so plainly and stop there.** Do not run verify on a red build
to see what it makes of it, and do not adjust a test to make it pass — that is the inversion the whole
design exists to prevent. A failing build is a build to fix.
