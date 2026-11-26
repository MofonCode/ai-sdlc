---
name: resolve-conflict
description: Bring one cycle's pull request up to date with the integration branch when it conflicts, goes red after another merge, or is behind it before it opens -- resolve inside the cycle's declared files, rebuild, and record fresh evidence. A single-responsibility skill: remediate runs it first when a pull request conflicts, then verifies. Use when a pull request into the integration branch conflicts or its re-check fails.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id>"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Read, Write, Edit, Glob, Grep
---

# Resolve a cycle's conflict with the integration branch

Every cycle builds from the same base, so two that touch the same thing meet when the second one's pull
request is merged (`CONCEPT.md` §10.5, D-86). A person merges one pull request at a time; when the next one
conflicts, or its re-check goes red because an earlier merge broke it, this brings it up to date. So does a branch
`sdlc up-to-date` finds behind before its pull request is opened (D-227): the same merge, a little earlier.

**A single-responsibility skill** (D-186): it brings the integration branch in and records what the code now is,
and nothing else. **It edits, and it does not judge its own work.** A resolution changes code, so the cycle's
verify is void the moment it is made. What proves the resolution regressed nothing is the suite and a **fresh**
verify, in a new process, against the spec — never this session's opinion of what it did (D-01). That verify, and
the pull request's update, are `remediate`'s: a person says *fix it* on a pull request that conflicts, and
`remediate` runs this first, then its loop.

## 1. Get on the cycle's branch and bring the integration branch in

```
sdlc status <id> --brief
git fetch origin
sdlc branch <id> remediate
```

**Run by `remediate`, you are already there.** Otherwise `sdlc branch` prints the path of a worktree on the
branch the cycle's pull request is from, creating it if absent; every step below runs there, and the person's own
checkout is left alone. Merge the integration branch into it:

```
git merge origin/<integration branch>
```

The integration branch is `integration_branch` in `.sdlc/solution.md` — read it with your file-reading
tool — or `development` when the key is absent. **Merge, do not rebase**: the branch is already pushed and
reviewed, and the pull request is squashed when it lands, so a merge costs nothing and rewrites nothing.

## 2. Stay inside the declared files

Read the plan's `Files` section. **Every file you edit must be one it declares.** A conflict in a declared
file is yours to resolve. A resolution that needs a file the plan never named — cycle A renamed a method in
a file this cycle's plan never mentions, and fixing this cycle means editing that file — **is not a merge, it
is a change of plan.** Abort the merge, and hand back: a person picks it up with `steer`, a reopen, or an
abandonment.

```
git merge --abort
```

## 3. Resolve

| Shape | Resolution |
|---|---|
| two additions to a project file, `Directory.Packages.props`, a registration, `appsettings`, Terraform | keep both — one version each, in the order the file already uses |
| the same setting with two different values | **stop.** Two plans disagreeing about one setting is a person's decision. Say which cycles, which key, which values |
| two EF Core migrations from the same parent | **regenerate, never merge.** Take the integration branch's, then regenerate this cycle's migration against the merged model. Two merged chains compile and fail at deploy |
| code: the integration branch changed what this cycle uses — a rename, a moved type, a new parameter | adapt this cycle's code to the integration branch as it now is, keeping what the plan said this cycle does. The integration branch's change is merged work; it is not yours to undo |
| no textual conflict, and the re-check is red | the same: read the failure, adapt this cycle's code inside its declared files |

**Keep what the plan described, nothing more.** `CON_25` still binds: a resolution is not the moment to
tidy something you noticed. Record it as a `Noticed:` line in the commit instead.

## 4. Prove it, from fresh

```
dotnet build
dotnet test
```

**Read the counts, not the exit code**, as the build stage does. Then commit the merge, and record what the
code now is — the earlier verify adjudicated code that no longer exists, so it goes first:

```
git rm .sdlc/cycles/<id>/verify.md
sdlc evidence <id> --tests TestResults --build-log <path> --coverage TestResults
```

**If the branch carries a placed record** — the person's own commit changing the solution's constraints,
decisions or declared stack after the old verify — it comes out before the verify is written again, and
goes back on top once the new one passes. Ask the person to revert it; the files are theirs. Left in, the
new verify's rounds read rules it placed — which the pull-request check reports but does not refuse, so
nothing else will catch it (D-111, D-113).

Run the tests with `--coverage --coverage-output-format cobertura --results-directory TestResults`, as the
build stage does. **A conflict on `.sdlc/coverage.json` is expected** when another cycle merged first:
take the integration branch's side, and recording the evidence writes the new value over it.

Commit the evidence. **Push nothing**: the branch has no verify now, and a pull request updated before one is
written would show a red check for no reason a reviewer could act on.

## 5. Hand back

**Report at most five lines**: the cycle, what conflicted, what you kept from each side, and anything you
declined to resolve. **Run by `remediate`**, hand straight back: it verifies, goes round while that converges,
and updates the pull request. **Run on its own**, close with one plain offer: *fix it* (`/sdlc:remediate <id>`)
verifies the resolution in a fresh process and updates the pull request. **Never merge it** — a person does,
having read it.
