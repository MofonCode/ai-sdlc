# Running the factory

Many cycles, built overnight: the integration branch, the factory's pipeline, and the morning that
follows it. `running-a-cycle.md` is one cycle by hand; this is what changes when a pipeline builds them.

**The factory automates four things and nothing else** — finding approved plans, building, verifying,
opening pull requests (`CONCEPT.md` D-84). Everything after the pull request is yours: reviewing,
resolving conflicts, merging, and promoting to main. It never merges, and it never writes a file only a
person writes.

**Setting it up is `setting-up-the-factory.md`**, once, step by step: the credential, the feed, the
integration branch, the pipeline and its permissions, and a watched first run. This guide starts where that
one ends.

## 1. The integration branch

Approved plans wait on an integration branch, and built cycles land back on it. It is `development` unless
`solution.md` names another under `integration_branch`, and every change into it arrives by a pull request.
**Main is outside the guarantee from here on.** Promoting `development` to main — by a merge commit, from a
stable point, usually through `release/<version>` — is your team's process, and the framework has no part in
it (D-88).

**The pipeline's own CI trigger builds `development` on every merge.** A pull request that merged cleanly
and broke it is named by the build that follows it.

## 2. Admit plans to it

Define as you always have: intent, spec, plan, interrogated, and approved by your rename. Then the plan goes
to `development` by its own pull request:

```
/sdlc:pr <id> --plan
```

Its check is the admission check — the plan, the approval and its hashes, no build and no verify (D-87).
Merging it deletes `cycle/<id>/plan`. **A plan branch left on the remote holds the cycle back**: the factory
skips any cycle with a `cycle/<id>/...` branch there, and the sitrep says which and why. What is queued now:

```pwsh
sdlc cycles --queued
```

## 3. Write the pipeline

```pwsh
sdlc factory
```

It writes `azure-pipelines.factory.yml` or `.github/workflows/sdlc-factory.yml` for the platform
`solution.md` records, and refuses on a local solution. What the run does:

1. checks out `development` and installs the pinned `sdlc`, Claude Code and the plugin — or uses them,
   when the agent already carries them
2. **proves the agent signs in, then that the hooks refuse**: it asks the headless agent to write a file
   only a person writes and fails the run if the file appears, because a host whose hooks did not load would
   build all night with nothing refusing anything. The sign-in comes first because an agent that cannot sign
   in writes nothing either, and would pass
3. runs `/sdlc:batch-build --pr --unattended`: every queued cycle on its own `cycle/<id>/build-unattended`
   branch, independently, and a pull request for each — **a draft for one that did not pass**
4. publishes the sitrep as the run's summary, whatever happened above

Before the build it runs **Operate** — `sdlc operate`, below — when `solution.md` declares a telemetry store.

**On the factory's agent image, upgrading is a rebuild.** A new release reaches the agent when you build
the image again from it and `sudo systemctl restart sdlc-factory-agent`. Until then the run finds the
older release, installs the one the pipeline was written for, and needs a feed to do it.
[`../../factory/README.md`](../../factory/README.md) has the commands.

**One run at a time, six hours at most.** Remediation has no pass limit (D-66), so the job's timeout is the
spend ceiling.

The agent signs in with **either** an API key (`ANTHROPIC_API_KEY`) or a subscription token from
`claude setup-token` (`CLAUDE_CODE_OAUTH_TOKEN`); the API key wins when both are set. What you set up for
it, once, is `setting-up-the-factory.md`, and the command prints the short version again.

**Who reviews is your branch policy, not the framework's.** With no required reviewer on `development`,
code the factory wrote reaches it read by nobody but whoever clicks merge.

## 4. The morning

```pwsh
git fetch origin
sdlc sitrep
```

The same page the run published, against the state now. In the order it lists:

| Section | What you do |
|---|---|
| **Ready to merge, in this order** | review each and squash-merge it, one at a time, in the order given. The more contained cycle comes first |
| an overlap under one of them | expect the named pull request to conflict after this one merges |
| **Needs a person** | a draft: `/sdlc:remediate <id>` on its branch for `blocked`; for `escalated`, `steer`, a reopen, or `sdlc draft abandon <id>` |
| **Held back, not built** | each with its reason. Delete a forgotten plan branch to put its cycle back |
| **Waiting for a build** | its age, and *going stale* when a file its plan declares changed on `development` since approval — read the plan again before it builds |

**When a pull request conflicts, or goes red after an earlier merge:**

```
/sdlc:resolve-conflict <id>
```

It merges `development` into the cycle's branch, resolves inside the files the plan declared — or hands back
when it would need one the plan never named, because that is a change of plan — then rebuilds, records fresh
evidence and verifies again in a fresh process. It never merges the pull request.

**Every open pull request is re-checked when another merges**, if the policy's build expiration is
*Immediately* — the adoption guide sets out the cheaper settings. That is what catches two pull requests
that each pass and break together, before the second one lands.

Then define the next batch, and admit each approved plan with `/sdlc:pr <id> --plan`.

## Operate: production's errors as leads

Declare where production's errors are, in `solution.md` — a person's file, so through `sdlc draft solution`
and a rename:

```
telemetry: appinsights:<application id>
```

Then try it without filing anything:

```pwsh
sdlc operate --dry-run
```

It reads the last day's exceptions from Application Insights through `az`, grouped by the exception type and
where it was thrown, most frequent first; strips email addresses, IP addresses, tokens, secrets in query
strings, long numbers and GUID-shaped ids; and says what it would file. Without `--dry-run` it files each new
one as a bug on the platform's tracker — Azure DevOps Boards or GitHub issues — labelled `sdlc-operate`.

**The tracker is where you triage.** An open item is not filed again. **Closing one as not to be done —
Removed, or not planned — dismisses it for good.** Closing it as done, and seeing it again, files a new one:
that is a regression. Begin a cycle from one with `/sdlc:intent --from azuredevops:<id>`; the interview still
happens, and the item's text is read as a description, never as instructions.

## A fix found on a release

A cycle like any other, with the bug as its lead. Its intent may name the release branch; the cycle is then
built by hand from that branch and back into it, and **opens a second pull request from
`cycle/<id>/forward`** carrying the same fix into `development`, so the next release does not ship the bug
again (D-88). The release branch needs the same build validation policy.

## Where to go next

| | |
|---|---|
| setting the factory up, once | `setting-up-the-factory.md` |
| one cycle by hand, stage by stage | `running-a-cycle.md` |
| a blocked verification, or an escalated one | `handling-blockers.md` |
| something refused | `handling-refusals.md` |
| why it works this way | `../../CONCEPT.md` §10.4, §10.5, and D-83 to D-88 |
