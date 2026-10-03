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

So when you notice something worth fixing that the plan did not describe: **leave it, and say so in
your report.** That is a suggestion for a later cycle, not a thing to slip in.

**If the plan declares the solution skeleton, run `sdlc scaffold <id>`.** The first cycle of a new
solution has no projects to change yet, so its plan declares the ones the profile supplies — a solution
file, a core project, an HTTP project, two test projects and a reference slice. The binary writes them,
and refuses unless this cycle is approved; transcribing several hundred lines of template through a plan
document would invite corruption and prove nothing. Everything after that first cycle you write
yourself.

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

**The turn end is checked.** A hook asks the gate before you finish, and it sends the turn back when the
recorded evidence shows a failing suite or when the working tree has moved since the build was recorded.
It is not a rule to remember — it is why there is no point reporting done over a red build.

## Commit the change on the branch

Commit before recording evidence. Message saying what it does and why, referencing the cycle id.

**Why this order.** The diff is measured from where the branch diverged, and a file git is not tracking
is a file `git diff` cannot see. `sdlc evidence` now stages untracked paths itself before diffing, so
recording first no longer produces an empty patch — but committing first means the patch comes from
committed state rather than from whatever the working tree happens to hold, and that is the thing a
reviewer is reading. Before the fix, an unstaged new file made `diff.patch` empty while the panel said
every declared file had changed.

## Record the evidence

```
sdlc evidence <id> --tests <path to the results file> --build-log <path to the build log>
```

This writes the diff, the commit it is measured from, the runner's output, and one normalised test
summary. **Verify reads only these files.** A verdict derived from your recollection of the build is a
verdict nobody can check, which is what `CON_08` forbids.

It also prints the declared-against-changed comparison. Read it, and read it correctly:

- **`undeclared`** means something reached the diff that the plan did not name. Usually that is the
  creep described above, and the honest move is to take it back out.
- **`undelivered`** means a file the plan named went untouched. Sometimes that is right — the plan
  over-declared. Sometimes it means part of the plan was not implemented.
- **Neither is a refusal.** The comparison cannot see inside a file, which is why `CON_07.2` exists at
  all. What blocks is outcome-based: the build fails, tests fail, or a named test did not pass.

## Commit the evidence

Commit the evidence directory too. `verify` and the pull-request check both read it from the
repository, so evidence that exists only in your working tree is evidence the chain cannot see.

If `sdlc evidence` refused because the patch came out empty while files had changed, that is a real
defect rather than a step to work around — it means git cannot see the change the comparison can.
`git status --short` is where to look.

## Report and hand back

Say what you implemented, what the tests did, and anything you left alone deliberately. Then:

```
sdlc status <id>
```

Show it verbatim and stop. The person decides whether to verify.

**If the build or the tests failed, say so plainly and stop there.** Do not run verify on a red build
to see what it makes of it, and do not adjust a test to make it pass — that is the inversion the whole
design exists to prevent. A failing build is a build to fix.
