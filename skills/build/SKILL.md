---
name: build
description: Implement an approved plan, build, test, and record the evidence a verification will read. Use after a person has approved a cycle's three planning documents.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id>"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Read, Write, Edit, Glob, Grep
---

# Build an approved plan

The first stage that writes code, and the first one a person is not expected to watch.

## The gate decides, not you

```
sdlc gate <cycle id>
```

If it refuses, stop and show what it said. Do not work around it and do not ask the person to approve
so you can continue — the refusal names what is missing, and the next step belongs to them.

There are eight ways it refuses, and each has a different next step: no cycle, abandoned, a missing
document, nobody approved, a refusal standing, a document changed since it was approved, a blocking
finding still open, or a round that judged a document which has moved since. Read which one.

**A blocking finding cannot be waived.** If that is the refusal, there is no flag and no override. The
finding is answered, or the cycle does not advance.

## Keep a task list, if your host has one

If your host gives you a task list, open one once the gate has said the build may run, with these
items, and mark each done as it finishes. If it does not, carry on without one — nothing below depends
on it.

1. switch to the cycle's branch
2. implement the plan: **one item for each entry in its `Files` section**, and none for anything it
   does not declare
3. build and test, clean
4. commit the change
5. record the evidence
6. commit the evidence
7. report and hand back

**The plan decides what is on the list, not the other way round.** An item you want to add that the
plan does not declare is the plan being departed from, and that is a correction for a person to make
rather than a line to tick.

**Inside a batch, keep none.** When the batch-build skill is running this stage, its list has one line
for this cycle and that line is the whole of what this stage shows. Seven more per cycle would bury
the one thing somebody glancing at a batch is looking for.

**This stage only.** Nothing after the hand-back goes on the list: whether the next stage runs is a
person's decision, and `sdlc status <id>` names it. **The list is a view, never the record.** What is
done is what is on disk and what the binary reports, so where the two disagree the list is the one
that is wrong.

## Work on the cycle's branch

```
git switch cycle/<id>
```

If the branch does not exist, create it from the default branch. Everything below measures the change
from where this branch diverged, so building on the default branch directly makes the diff meaningless.

## Implement the plan, and only the plan

Read `plan.md`. Implement what `Shape and departures` decided and what `Proof` committed to.

`CON_07.2` forbids a change within a declared file that the plan did not describe — an unrelated
refactor, a drive-by rename, a fix to something else you noticed in passing. The file set is checked
mechanically; **creep inside an approved file is what reaches review dressed as approved work**, which
is exactly where it is least likely to be questioned.

So when you notice something worth fixing that the plan did not describe: **leave it, and record it as a
`Noticed:` line in the commit message.** That is a suggestion for a later cycle, not a thing to slip in —
and the commit message is where it survives. Your report is read once, by whoever is at the keyboard; the
commit is read by whoever reviews the pull request, which is the person who can act on it.

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
If the fallout is wider than the plan predicted, stop and say so; that is a plan that needs reopening,
not a build to push through.

**Write the tests the plan named.** The proof entries name tests in backticks, and `verify-chain`
checks that each one ran and passed. A test with a different name than the plan committed to will fail
that check even if it tests the right thing — so if the name has to change, the plan has to change
first, which means `sdlc draft approval <id> --reopen` and a re-approval.

## Build and test, and treat a warning as an error

```
dotnet build
dotnet test
```

Warnings are errors here, enforced by `Directory.Build.props` rather than by instruction. A build that
warns is a build that failed.

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
plan did not declare and you had to add belongs here too, with the reason it was forced. This is the
destination for everything the stage was not asked about: it reaches the reviewer, and it keeps the
hand-back to the stage's own result.

**Why this order.** The diff is measured from where the branch diverged, and a file git is not tracking
is a file `git diff` cannot see. `sdlc evidence` now stages untracked paths itself before diffing, so
recording first no longer produces an empty patch — but committing first means the patch comes from
committed state rather than from whatever the working tree happens to hold, and that is the thing a
reviewer is reading. Before the fix, an unstaged new file made `diff.patch` empty while the panel said
every declared file had changed.

## Record the evidence

```
sdlc evidence <id> --tests <directory holding the results files> --build-log <path to the build log>
```

This writes the diff, the commit it is measured from, every results file the run produced, and one
normalised test summary merged from all of them. **Verify reads only these files.** A verdict derived
from your recollection of the build is a verdict nobody can check, which is what `CON_08` forbids.

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
implemented, what the build and the tests said, and **how many** `Noticed:` lines the commit carries.
This is the one stage where prose earns its place, because a diff is not self-describing — but it is five
lines, not a narrative, and the panel above already carries the cycle, the counts, the gate and the next
command.

**Do not restate the `Noticed:` lines here.** They are in the commit, which is where the person who can
act on them will read them. Naming the count is what tells somebody to go and look; repeating the
contents is how a five-line hand-back becomes a page.

Then stop. The person decides whether to verify.

**If the build or the tests failed, say so plainly and stop there.** Do not run verify on a red build
to see what it makes of it, and do not adjust a test to make it pass — that is the inversion the whole
design exists to prevent. A failing build is a build to fix.
