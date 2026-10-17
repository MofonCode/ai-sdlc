# Setting up the factory

From an adopted solution to a pipeline that builds your approved plans every night, step by step. Allow
an hour the first time. `running-the-factory.md` is what you do every day once it is running; this is the
part you do once.

**What you will have at the end:** a nightly pipeline that takes every plan you approved and admitted,
builds and verifies each one on its own branch, and opens a pull request per cycle — a draft for any that
did not pass. In the morning you read one page, the sitrep, and merge. **It never merges anything itself,
and it never writes a file only a person writes.**

**Step 7 is a watched first run against a cycle you do not care about.** A pipeline that is wrong
somewhere fails on its first run, and that is the run to spend on a throwaway.

The steps are written for **Azure DevOps**. GitHub differs only in step 6, which has a section of its own.

## Before you start

| You need | Check it with |
|---|---|
| an adopted solution with a remote on Azure DevOps or GitHub, and its pull-request check working | `sdlc doctor` — the `the guarantee` line is sound, and `adopting-the-framework.md` step 4 is done |
| permission to create pipelines, variables and environments in the project | the project's **Project Administrators** group has all three |
| permission to change the repository's security settings | the same group |
| the `sdlc` binary on your machine | `sdlc version` |

## 1. Choose how the agent signs in

The factory runs Claude Code headless — the same `claude` command you use, with no person at the keyboard.
It needs one credential, and **either of these works**:

| | API key | Subscription token |
|---|---|---|
| Secret name | `ANTHROPIC_API_KEY` | `CLAUDE_CODE_OAUTH_TOKEN` |
| Where it comes from | the Anthropic Console: **API keys > Create key** | run `claude setup-token` on your machine and sign in with your Claude account |
| Who pays | the Console account, per token | your Claude subscription, inside its usage limits |
| Good for | a team that depends on the nightly run | trying the factory out, on your own plan |
| Watch out for | the bill: a run is bounded by its six-hour limit, not by a budget | **the plan's usage limits** — a long night can run out partway, and those cycles end *did not finish* — and whether its terms allow a team's shared pipeline |

**Set one.** If both are set, the pipeline uses the API key and ignores the token, so who pays never
depends on anything else.

Keep the value somewhere safe until step 6; you paste it there once and never see it again.

## 2. Get the tools onto the agent

A run needs three things on the machine that runs it: the `sdlc` binary, Claude Code, and the sdlc plugin.
**The pipeline uses each one the agent already carries, and installs whatever is missing**, so there are
two ways to do this step:

| | Your own agent | Microsoft's hosted agents |
|---|---|---|
| What you do | install `sdlc`, Claude Code and the plugin on it once — `installing-the-plugin.md` — and register it in an agent pool | put the `sdlc` package on a feed the agent can read, below |
| Each run | installs nothing, so it starts faster | installs all three |
| When you upgrade | reinstall on the agent; until you do, the run installs the release it was written for | nothing |

**`sdlc` is pinned either way.** An agent carrying a different version gets the one the pipeline was written
for, because the version a pull request is judged by is not the agent's to decide. The plugin comes from the
public `MofonCode/ai-sdlc` repository and Claude Code from npm, so neither needs a feed.

On your own agent, point the pipeline at its pool — replace `vmImage: ubuntu-latest` with
`name: <your pool>` under `pool:` — and skip to step 3.

**On hosted agents, `sdlc` needs a feed**, because the package is not on a public one yet. On Azure DevOps,
that is an **Azure Artifacts** feed in your project:

1. **Artifacts > Create Feed.** Name it `sdlc`, scope it to the project.
2. Build the package from the ai-sdlc repository:

   ```pwsh
   dotnet pack src\Sdlc.Cli\Sdlc.Cli.csproj -c Release -o pack
   ```

3. Push it — **Artifacts > sdlc > Connect to feed > dotnet** shows the exact source URL and how to sign in:

   ```pwsh
   dotnet nuget push pack\MofonCode.Sdlc.Cli.<version>.nupkg --source <the feed's URL> --api-key az
   ```

4. In **your solution**, add a `nuget.config` at the root naming the feed, and commit it with the next step:

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <packageSources>
       <add key="sdlc" value="https://pkgs.dev.azure.com/<organization>/<project>/_packaging/sdlc/nuget/v3/index.json" />
     </packageSources>
   </configuration>
   ```

The pipeline signs itself in to the feed (`NuGetAuthenticate@1`), so nothing else is needed. **The
pull-request pipeline installs `sdlc` the same way**, so this also fixes that check if it has been
failing at *install sdlc*.

## 3. Push the integration branch

Approved plans wait on an integration branch, and finished cycles land back on it. It is `development`
unless `solution.md` names another under `integration_branch`. **Adoption created it locally** and
switched to it, so the factory's half is putting it on the remote:

```pwsh
git switch development
git push -u origin development
```

A solution adopted before adoption created it has no `development` yet, and `sdlc doctor` says so; there,
`git switch -c development` from your default branch first.

## 4. Write the factory's pipeline, and commit it

Still on `development`:

```pwsh
sdlc factory
```

It writes `azure-pipelines.factory.yml` — or `.github/workflows/sdlc-factory.yml` on GitHub — and prints
what step 6 sets up. Commit it with the `nuget.config` from step 2, and push:

```pwsh
git add azure-pipelines.factory.yml nuget.config
git commit -m "Add the factory's pipeline"
git push
```

**Do this before step 5.** Once `development` requires the pull-request check, a change that is not a
cycle cannot merge into it: the check refuses a branch with no intent, spec and plan behind it. If your
`development` already has that policy, add the file as a short cycle instead.

## 5. Protect the integration branch

Give `development` the same build validation policy as your default branch — `adopting-the-framework.md`
step 4 walks it. Every change into `development` now arrives by a pull request, and the check runs on each.

**Set the policy's build expiration to *Immediately*.** Then every open pull request is checked again when
another merges, which is what catches two pull requests that each pass alone and break together.

**Add a required reviewer, if you want one.** The framework does not decide who reviews; without one,
code the factory wrote reaches `development` read by nobody but whoever clicks merge.

## 6. Set up the pipeline in Azure DevOps

Four things, once.

**a. Create the pipeline.** **Pipelines > New pipeline > Azure Repos Git >** your repository **> Existing
Azure Pipelines YAML file >** branch `development`, path `/azure-pipelines.factory.yml` **> Save** (not
Run — it needs its secret first).

**b. Add the credential.** On the new pipeline: **Edit > Variables > New variable.** Name it
`ANTHROPIC_API_KEY` or `CLAUDE_CODE_OAUTH_TOKEN`, from step 1. Paste the value, tick **Keep this value
secret**, and save.

**c. Let the pipeline push branches and open pull requests.** **Project settings > Repositories >** your
repository **> Security.** Find the user named **`<project> Build Service (<organization>)`** and set:

| Permission | Set to |
|---|---|
| Contribute | Allow |
| Create branch | Allow |
| Contribute to pull requests | Allow |

**d. One run at a time.** **Pipelines > Environments > New environment**, name `sdlc-factory`, resource
*None*. Then on it: **⋮ > Approvals and checks > + > Exclusive lock > Create.** Without it, two runs could
pick the same cycles; with it, a second run waits for the first.

## 6, on GitHub instead

**Settings > Secrets and variables > Actions > New repository secret**, twice:

| Secret | What |
|---|---|
| `ANTHROPIC_API_KEY` or `CLAUDE_CODE_OAUTH_TOKEN` | from step 1 |
| `SDLC_FACTORY_TOKEN` | a fine-grained personal access token for this repository with **Contents** and **Pull requests** set to *Read and write* |

**It has to be a token of your own, not `GITHUB_TOKEN`.** A pull request opened with `GITHUB_TOKEN` starts
no workflow, so the pull-request check would never run on anything the factory opened. One run at a time is
already in the file (`concurrency`); there is nothing to set.

## 7. The first run, watched

Use a cycle you do not care about — a one-line change to a README is enough.

1. Define it as usual: `/sdlc:intent`, then spec and plan, interrogated, and approve it by your rename.
2. Admit the plan to `development`: `/sdlc:pr <id> --plan`, and merge that pull request once its check
   passes. Its branch is deleted on merge.
3. Confirm it is waiting:

   ```pwsh
   sdlc cycles --queued
   ```

4. Run the pipeline by hand: **Pipelines >** the factory **> Run pipeline**, branch `development`.

Watch the steps. Each one stops the run with a sentence saying why, so the first failure tells you which
step of this guide to revisit:

| Step | What a pass looks like |
|---|---|
| install sdlc, the agent and the plugin | each is installed at your version, or reported as already on this agent |
| prove the agent signs in and the hooks refuse | the agent answers, then is refused a write to `.sdlc/approval.md`, and the file never appears |
| operate | *This solution declares no telemetry store*, unless you declared one |
| build the queue | one cycle built on `cycle/<id>/build-unattended`, and a pull request opened into `development` |
| the sitrep | the run's summary: the cycle under **Ready to merge** — or under **Needs a person**, as a draft |

**It worked when** the pull request exists, its check has run on it, and the run's summary shows the
sitrep. Review the pull request as you would a person's, and merge it — or close it; it was a throwaway.

## 8. Let it run every night

Nothing to switch on: the schedule is in the file, at **02:00 UTC** every day, and only on `development`.
To change the time, edit the `cron` line — `'0 2 * * *'` is minute, hour, day, month, weekday, in UTC —
and commit it as a cycle, since `development` is protected now.

From here, `running-the-factory.md` is the routine: admit plans in the day, read the sitrep in the morning.

## When something goes wrong

| What you see | Why | What to do |
|---|---|---|
| *Set ANTHROPIC_API_KEY or CLAUDE_CODE_OAUTH_TOKEN as a secret* | the pipeline has no credential | step 6b — and check the variable is on **this** pipeline, spelled exactly |
| *The agent did not answer, so its credential does not work* | the key was revoked, the token expired, or the subscription is out of usage | make a new one (step 1) and replace the secret |
| *The hooks did not refuse a protected write* | the plugin's hooks did not load, so nothing would refuse anything tonight | look at the install step's output for the plugin install; the run stopped before building, which is the point |
| the `sdlc` install fails: *not found* or *401* | the feed is missing, not in `nuget.config`, or the build service cannot read it | step 2; on the feed, **⚙ > Permissions** — the build service needs *Feed Reader* |
| the plugin install fails | the agent cannot reach GitHub | the plugin comes from the public `MofonCode/ai-sdlc` repository; an agent behind a firewall needs `github.com` allowed, or the plugin installed on it beforehand |
| a push or a pull request is refused, *TF401027* | the build service lacks a permission | step 6c |
| the sitrep lists nothing to merge | no approved plan was on `development` without a verify | `sdlc cycles --queued` says what is queued and what is held back, each with its reason — a forgotten `cycle/<id>/plan` branch on the remote holds its cycle back |
| a second run is waiting | the Exclusive lock doing its job | nothing, unless the first is stuck; the six-hour limit ends it |

## Where to go next

| | |
|---|---|
| the daily routine: admitting plans, the morning's sitrep, merges and conflicts | `running-the-factory.md` |
| one cycle by hand, stage by stage | `running-a-cycle.md` |
| the pull-request check and branch policy | `adopting-the-framework.md` |
| something refused | `handling-refusals.md` |
| why it works this way | `../../CONCEPT.md` §10.4, §10.5, and D-84 |
