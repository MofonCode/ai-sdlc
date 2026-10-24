---
name: pr
description: Push a cycle's branch and open its pull request into the integration branch. The only stage that touches the remote. Use after a verification passed, with --plan to admit an approved plan before it is built, with --draft for a cycle that did not pass, or with --all to sweep every cycle that is ready.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id> [--plan | --draft] | --all"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(gh *), Bash(az *), Read, Glob, Grep
---

# Open a pull request for a cycle

The only stage that reaches outside the repository. Everything before this is local and reversible; a
pull request notifies people, appears in a queue, and withdrawing it is something somebody has to notice
happened.

**Three kinds of pull request, and each has its own question first** (`CONCEPT.md` §10.4, §10.5):

| Form | Carries | Asks first |
|---|---|---|
| `pr <id>` | a verified build | `sdlc pr-ready <id>` |
| `pr <id> --plan` | an approved plan, before it is built — admission to the integration branch | `sdlc gate <id>` |
| `pr <id> --draft` | a build that did not pass: `blocked` or `escalated` | `sdlc pr-ready <id>`, which must refuse for exactly that: did not pass, or escalated |

## Where it goes

**Into the integration branch.** Read `integration_branch` in `.sdlc/solution.md` with your file-reading
tool — `development` when the key is absent — and check it exists on the remote:

```
git ls-remote --heads origin <integration branch>
```

If it does, every pull request targets it. If it does not, the solution has no integration branch yet and
the pull request targets the default branch, as before.

**From the cycle's own branch**, whichever one is checked out: `cycle/<id>/plan`, `cycle/<id>/build`,
`cycle/<id>/build-unattended`, or the bare `cycle/<id>` of an older cycle. `sdlc branch <id> <kind>` is what
put it there; do not invent a name.

## A plan: `--plan`

```
sdlc gate <id>
```

**Push only past a satisfied gate.** A plan nobody approved, or one edited since, is refused by the
pull-request check anyway. Then push `cycle/<id>/plan` and open the pull request so that **merging it
deletes the branch** — on Azure DevOps `az repos pr create --delete-source-branch true`; on GitHub that is
the repository setting *Automatically delete head branches*, so if you cannot tell it is on, say in the body
that the branch must be deleted on merge. A plan branch left on the remote holds the cycle back from the
factory, and the sitrep will say so.

The body carries the intent's problem in its words, the spec's criteria, the plan's shape and its tier.
Its check is the admission check: the plan, the approval and its hashes, and no build (D-87). **It is not a
second approval** — the local approval is the gate; this admits the plan to the factory.

## A cycle that did not pass: `--draft`

Opened for a `blocked` or `escalated` cycle, usually by `batch-build` (D-85). **Refuse if `sdlc pr-ready`
gives any other reason** — not verified, or the chain broken — because then there is nothing true to show.
Open it as a draft (`gh pr create --draft`, `az repos pr create --draft true`), and lead the body with the
outcome and the open findings, so the person picking it up starts from what stopped it. Its check will be red,
which is accurate. A person remedies it on this branch and marks it ready.

## A verified build: ask first, every time

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

That writes the pull-request check, and **prints the three `solution.md` keys for the person to place** —
it cannot write them itself, because that file is one of the six only a person writes. Until they are
pasted the record still says `platform: local`, so `doctor` still reports no check governs a merge and
this skill still stops at the question above. Hand over what `init` printed and say it is the step that
finishes the migration.

Then **tell them to make the check
required in branch policy** — a pipeline that runs and is not required is a pipeline a merge can ignore,
and that is the difference between the guarantee and a green tick. `sdlc doctor` marks that part
`elsewhere` because no binary can confirm it.

**A local cycle can still be built, verified and committed.** Only the pull request needs a remote. What
it does not have is the pull-request check, which `CONCEPT.md` §13 calls the only non-bypassable layer,
and nothing on one machine stands in for it. Say that plainly rather than letting somebody believe a
local cycle is governed to the same standard.

## Push and open the pull request

```
git push -u origin HEAD
```

`HEAD` is the cycle's own branch, checked out. Target the integration branch, as above.

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

**Link the work item.** When the intent's frontmatter records a `lead`, close it from the body: `AB#1234`
for `azuredevops:1234`, `Fixes #56` for `github:56`. When it records a `release`, the pull request targets
that branch instead of the integration branch, and after it opens, `sdlc branch <id> forward` starts the
second one that carries the same fix into the integration branch — cherry-pick the cycle's commits onto it
and open that pull request too (D-88).

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
