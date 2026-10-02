---
name: land
description: Push a verified cycle and open its pull request. The only stage that touches the remote. Use after a verification passed, or with --all to sweep every cycle that is ready.
argument-hint: "<cycle id> | --all"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(gh *), Bash(az *), Read, Glob, Grep
---

# Land a verified cycle

The only stage that reaches outside the repository. Everything before this is local and reversible; a
pull request notifies people, appears in a queue, and withdrawing it is something somebody has to notice
happened.

## Ask first, every time

```
sdlc landable <id>
```

It refuses for four reasons, and each is final rather than advisory:

| Refusal | What it means |
|---|---|
| not verified | there is no verification yet |
| did not pass | the verdict was `blocked` or `escalated` |
| escalated | the remediation budget ran out with findings still open |
| chain broken | it verified, and something has changed since |

**Do not land past a refusal.** The chain-broken one in particular: the pull-request check would refuse
it anyway, so pushing would cost a round trip through CI to learn what you were just told.

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
sdlc landable --all
```

It lists **every** cycle and what each one is, not only the ones it would land. Show that list verbatim
and **ask once before the first push.**

Why ask at all, given the approval already said the plan was agreed: because the question is about
**volume**, not permission. Every cycle that ever passed is eligible, including one from last week that
somebody has forgotten, and "land 8 cycles?" is a question nobody can answer honestly without the list.

Then:

- land them one at a time, in cycle-id order
- **a failure on one does not stop the others.** Each cycle is independent; report the failure and carry on
- **report each pull request individually**, with its link. A count is not something a person can check

## Report

Per cycle: its id, its branch, and the pull request's link. Then stop. Nothing after this is yours — the
merge belongs to a reviewer.
