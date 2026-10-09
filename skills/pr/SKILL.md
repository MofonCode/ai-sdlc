---
name: pr
description: Push a verified cycle and open its pull request. The only stage that touches the remote. Use after a verification passed, or with --all to sweep every cycle that is ready.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id> | --all"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(gh *), Bash(az *), Read, Glob, Grep
---

# Open a pull request for a verified cycle

The only stage that reaches outside the repository. Everything before this is local and reversible; a
pull request notifies people, appears in a queue, and withdrawing it is something somebody has to notice
happened.

## Ask first, every time

```
sdlc pr-ready <id>
```

It refuses for four reasons, and each is final rather than advisory:

| Refusal | What it means |
|---|---|
| not verified | there is no verification yet |
| did not pass | the verdict was `blocked` or `escalated` |
| escalated | remediation stopped making progress with findings still open |
| chain broken | it verified, and something has changed since |

**Do not push past a refusal.** The chain-broken one in particular: the pull-request check would refuse
it anyway, so pushing would cost a round trip through CI to learn what you were just told.

## First: is there anywhere to push?

```
sdlc doctor
git remote -v
```

`.sdlc/solution.md` records the platform. **If it says `local`, or `git remote -v` prints nothing, stop
before pushing** — there is nowhere to push and nothing to open a pull request against. This is the one
refusal that is not about the cycle: the work may be perfectly ready and have no destination.

Say so, and offer to set one up. Walk the person through it rather than running it for them — creating a
repository under somebody's account is theirs to do:

**GitHub**

```
gh repo create <name> --private --source . --remote origin
git push -u origin <default branch>
```

**Azure DevOps**

```
az repos create --name <name> --project <project>
git remote add origin <the remoteUrl it prints>
git push -u origin <default branch>
```

**Then the part that matters, and it is not optional:** a remote alone installs no guarantee. `init` wrote
no pipeline for a local solution, so after the remote exists a person re-runs adoption to get one:

```
sdlc init --platform github
sdlc init --platform azure-devops
```

That writes the pull-request check, and **prints the four `solution.md` keys for the person to place** —
it cannot write them itself, because that file is one of the six only a person writes. Until they are
pasted the record still says `platform: local`, so `doctor` still reports no check governs a merge and
this skill still stops at the question above. Hand over what `init` printed and say it is the step that
finishes the migration.

Then **tell them to make the check
required in branch policy** — a pipeline that runs and is not required is a pipeline a merge can ignore,
and that is the difference between the guarantee and a green tick. `sdlc doctor` marks that part
`elsewhere` because no binary can confirm it.

**A local cycle can still be built, verified and committed.** Only the pull request needs a remote. What
it does not have is the pull-request check, which `CONCEPT.md` §13 calls the only non-bypassable layer —
the pre-push hook is local and `--no-verify` walks past it. Say that plainly rather than letting somebody
believe a local cycle is governed to the same standard.

## Push and open the pull request

```
git push -u origin cycle/<id>
```

Then open the pull request with the platform's own tool — `gh pr create` on GitHub, `az repos pr create`
on Azure DevOps. If neither is available, say so and give the person the branch name and the body to
paste; do not try to reach the API by hand.

**The Azure DevOps path has never been run.** The GitHub one has. So on Azure DevOps, show the person
the exact command before running it rather than after, and if it fails on an argument or a missing
extension, that is why — say so plainly instead of trying variations. The generated
`azure-pipelines.yml` carries the same note, and `CONCEPT.md` D-32 is the decision to ship it that way.

**The body is the cycle, not a summary you invented.** It should carry:

- the intent's problem, in the intent's words
- the spec's criteria, and the verification's verdict on each
- the tier, so a reviewer knows how reversible this is
- a link to or quote of the `Risk` section, which is the part written for them
- anything in a `## Open` region, because a reviewer should meet the limits before they approve

**Link the work item** if the solution tracks them, with the intent and spec details.

## What you are not doing

**You are not approving the merge.** A code owner reviews the pull request, and it is never the agent
that authored the change. The local approval defended against an agent that forgets; this review is what
defends against a human who did not look, and they are different jobs.

**You are not claiming the chain proves a second person approved.** It cannot: the approval entry names
whoever typed it. `sdlc verify-chain` says so in its own output, and if somebody asks what the green
check means, that is the honest answer.

## Sweeping

```
sdlc cycles --pr-ready
```

It lists **every** cycle and what each one is, not only the ones it would open one for. Show that list
verbatim and **ask once before the first push.**

Why ask at all, given the approval already said the plan was agreed: because the question is about
**volume**, not permission. Every cycle that ever passed is eligible, including one from last week that
somebody has forgotten, and "open 8 pull requests?" is a question nobody can answer honestly without the
list.

Then:

- open them one at a time, in cycle-id order
- **a failure on one does not stop the others.** Each cycle is independent; report the failure and carry on
- **report each pull request individually**, with its link. A count is not something a person can check

## Report

**One line per cycle**, carrying three things: its id, its branch, and the pull request's link. Then
stop. Nothing after this is yours — the merge belongs to a reviewer, and a summary of what they are
about to read is a reviewer being told what to think.
