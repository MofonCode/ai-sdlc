---
name: build
description: Implement an approved plan, build, test, and record the evidence a verification will read. Use after a person has approved a cycle's three planning documents.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[<cycle id>]"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Bash(claude *), Read, Write, Edit, Glob, Grep
---

# Build an approved plan

The first stage that writes code, and the first one a person is not expected to watch.

## Which cycle

**Given no cycle id, ask the binary which one is meant** (D-142):

```
sdlc cycles --here --for build
```

It exits 0 printing the one cycle ready for this step, and that is the cycle. Otherwise it lists the
candidates, or says none is ready and why: ask the person which, in one question, and **never pick one
yourself** — the cycle a skill acts on is not a guess.

## Work on the cycle's build branch, first

```
sdlc branch <id> build
```

Run the one command it prints. It puts the working copy on `cycle/<id>/build` — created from the
integration branch once the plan has been admitted to it, or before that from the plan's own branch or the
admission branch carrying it — or on the bare `cycle/<id>` of an older cycle. A plan only in a sandbox is
refused with what to do: admit it first. Inside a batch, `batch-build` has already put you on
`cycle/<id>/build-unattended`; stay there. Everything below measures the change from where this branch
diverged, so building on the integration or default branch directly makes the diff meaningless.

**Switch before you ask the gate.** The gate reads the cycle's documents as the branch you are on holds
them, so asked from another branch it refuses over a plan that is only missing there (D-162).

## The gate decides, not you

```
sdlc gate <cycle id>
```

If it refuses, stop and show what it said. Do not work around it and do not ask the person to approve
so you can continue — the refusal names what is missing, and the next step belongs to them.

There are eight ways it refuses, and each has a different next step: no cycle, abandoned, a missing
document, nobody approved, a refusal standing, a document changed since it was approved, a blocking
finding still open, or a round that judged a document which has moved since. Read which one. In a
solution that declares `approval: none` the gate asks for no approval, and every other refusal stands.

**A blocking finding cannot be waived.** If that is the refusal, there is no flag and no override. The
finding is answered, or the cycle does not advance.

## Keep a task list, if your host has one

If your host gives you a task list, open one once the gate has said the build may run, with these
items, and mark each done as it finishes. If it does not, carry on without one — nothing below depends
on it.

1. implement the plan: **one item for each entry in its `Files` section**, and none for anything it
   does not declare
2. build and test, clean
3. commit the change
4. record the evidence
5. commit the evidence
6. report and hand back

**The plan decides what is on the list, not the other way round.** An item you want to add that the
plan does not declare is the plan being departed from: leave it, unless the approved text cannot be
followed without it — then it is a deviation, below.

**Inside a batch, keep none.** When the batch-build skill is running this stage, its list has one line
for this cycle and that line is the whole of what this stage shows. Six more per cycle would bury
the one thing somebody glancing at a batch is looking for.

**This stage only.** Nothing after the hand-back goes on the list: whether the next stage runs is a
person's decision, and `sdlc status <id>` names it. **The list is a view, never the record.** What is
done is what is on disk and what the binary reports, so where the two disagree the list is the one
that is wrong.

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
blocking finding under `CON_24`. **If the work cannot be done without departing, record a `decision`
deviation** quoting the decision, below, and the person answers it before the merge.

`CON_07.2` forbids a change within a declared file that the plan did not describe — an unrelated
refactor, a drive-by rename, a fix to something else you noticed in passing. The file set is a signal,
not a contract; **creep inside an approved file is what reaches review dressed as approved work**, which
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
> src/MarketSense.MassiveStandIn/StandInHost.cs
Blocker: Credential_never_reaches_a_trader fails 2 runs in 3 with ObjectDisposedException (tests.json)
Deviation: StreamSocket.cs is also changed; DropSockets no longer aborts a disposed connection.
Why: the race is in StreamSocket, and no fix inside StandInHost reaches it.
Files: `src/MarketSense.MassiveStandIn/StreamSocket.cs`
```

- **The quote decides the kind.** `plan` and `proof` quote `plan.md`; `criterion` quotes `spec.md` and may
  only **narrow** — add `Successor: <slug> — what the dropped part makes possible`; `decision` quotes
  `decisions.md`. Quote the approved text word for word. **Nothing to quote means nothing to deviate from**:
  a choice the plan left open is yours to make, and is not recorded.
- **The blocker rests on evidence**: the failing test and its error, or the plan step and why it cannot be
  done. Never your say-so.
- **Never the intent, never wider.** No new outcome, no new criterion, no more cases than the approved text
  reached, and no file another cycle claims. Those are a successor or a reopen, and a person's call: stop
  and hand back.
- **Short.** A person reads every one before the merge. One line each, and the gist first.
- **Ids are never reused.** A second attempt at one a person declined is a new deviation with
  `Replaces: DEV_nn`.

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
- **None of the three is a refusal.** The comparison cannot see inside a file, which is why `CON_07.2`
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

## Report and hand back

```
sdlc status <id> --brief
```

Show it verbatim. Then **at most five lines of your own**, and only these three things: what you
implemented, what the build and the tests said, and **how many** `Noticed:` lines and deviations the commit carries.
This is the one stage where prose earns its place, because a diff is not self-describing — but it is five
lines, not a narrative, and the panel above already carries the cycle, the counts, the gate and the next
command.

**Do not restate the `Noticed:` lines here.** They are in the commit, which is where the person who can
act on them will read them. Naming the count is what tells somebody to go and look; repeating the
contents is how a five-line hand-back becomes a page.

**Then verify, on a green build** (D-159). A green build always goes to verify next, so stopping to ask
would ask the person nothing: follow the `verify` skill for this cycle, from its fresh adjudicator on. The
verdict is not this session's — verify writes it in a fresh process that never saw the build, which is what
keeps the separation (D-01, D-109) — so carrying on here costs it nothing. Your five lines come first, then
verify's hand-back, as one message. **Inside a batch, the batch does this**: stop after the evidence, as it
expects.

**If the build or the tests failed, say so plainly and stop there.** Do not run verify on a red build
to see what it makes of it, and do not adjust a test to make it pass — that is the inversion the whole
design exists to prevent. A failing build is a build to fix.
