---
name: pr
description: Push a cycle's branch and open its pull request into the integration branch, or update the one already open with a new push and a refreshed report. The only stage that touches the remote. Run by the build, remediate and series playbooks, or by a person: after a verification passed, with --plan to admit an approved plan before it is built, with --draft for a cycle that did not pass, with --run for a wave's or a series' one pull request, or with --all to sweep every cycle that is ready.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[<cycle id>...] [--plan | --draft] | --plan | --run | --all"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(gh *), Bash(az *), Read, Glob, Grep
---

# Open a pull request for a cycle

**A single-responsibility skill** (D-186): the pull request, and nothing else. The `build` and `remediate`
playbooks run it; a person may too. The only stage that reaches outside the repository. Everything before this is local and reversible; a
pull request notifies people, appears in a queue, and withdrawing it is something somebody has to notice
happened.

**Three kinds of pull request, and each has its own question first** (`CONCEPT.md` §10.4, §10.5):

| Form | Carries | Asks first |
|---|---|---|
| `pr <id>` | a verified build | `sdlc pr-ready <id>` |
| `pr <id> --plan` | an approved plan, before it is built — admission to the integration branch | `sdlc gate <id>` |
| `pr <id> <id>... --plan` | every approved plan of one slicing, and the successors it wrote, by one pull request | `sdlc gate <id>` for each, then `sdlc slices` |
| `pr --plan`, in a sandbox | every approved plan the sandbox carries, and the successors it wrote, by one pull request | `sdlc admit`, which names each before you are asked to push |
| `pr <id> --draft` | a build that did not pass: `blocked` or `escalated` | `sdlc pr-ready <id>`, which must refuse for exactly that: did not pass, or escalated |
| `pr <id>`, its pull request already open | a new push after `remediate`, and the body's report refreshed | nothing: the playbook that ran it already decided |
| `pr --run`, on `wave/<id>` or `series/<id>` | every cycle a run built, by one pull request, draft or ready | `sdlc pr-ready`, with no id |

**And every one of them asks `sdlc up-to-date` before that** (D-227), on the branch about to be pushed — below,
under *Before any push*. A merge it asks for changes what the row's own question reads, so it comes first.

## Which cycle

**For a verified build the id may be left out** (D-158): a person who has just been told *open the pull
request* should not have to look up a timestamp to do it.

```
sdlc cycles --here --for pr
```

It exits 0 printing the one cycle this branch carries whose verify passed, and that is the cycle. Otherwise it
lists the candidates: ask which, in one question, and **never pick one yourself**. Either way, **the question
before the push names the cycle**, by id and headline, so the person still says which work goes out. The plan,
draft and slicing forms keep their ids: each says what it carries, and a guess there pushes the wrong thing.

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
sdlc verify-chain <id> --base <integration branch>
```

**Push only past a satisfied gate and a chain that holds.** A plan nobody approved, or one edited since, is
refused by the pull-request check anyway. The second command is that check, run here: it also refuses a plan any
round of which a local build wrote, because a cycle argued on a build nobody published is a test run, and a test
run is abandoned rather than pushed (D-135). Show its refusal and stop. Then push `cycle/<id>/plan` and open the pull request so that **merging it
deletes the branch** — on Azure DevOps `az repos pr create --delete-source-branch true`; on GitHub that is
the repository setting *Automatically delete head branches*, so if you cannot tell it is on, say in the body
that the branch must be deleted on merge. A plan branch left on the remote holds the cycle back from the
factory, and the sitrep will say so.

The body carries the intent's problem in its words, the spec's criteria, the plan's shape and its tier.
Its check is the admission check: the plan, the approval and its hashes, and no build (D-87). **It is not a
second approval** — the local approval is the gate; this admits the plan to the factory.

### An amended plan

**A plan already on the integration branch that was reopened and approved again is an amended plan**
(D-123). Tell by asking git whether the integration branch holds this cycle's plan:

```
git cat-file -e origin/<integration>:.sdlc/cycles/<id>/plan.md
```

When it does, the pull request is the amendment's, and it says so:

- **The title starts with `Amended plan:`**, so nobody reads it as a new cycle.
- **The body leads with each reopen's reason**, in the person's words, from every `reopened` entry in
  `approval.md` after the approval the integration branch holds — then what changed in the spec and the plan.
  A reviewer is reading a decision that was taken mid-cycle; the reason is the first thing they need.
- **Close with the build branch's choice**, when `git ls-remote --heads origin "cycle/<id>/build*"` lists one: merge
  the integration branch into it to continue by hand, or delete it so the factory builds the amended plan.
  Name the branch; the choice is the person's, so do neither.

### A slicing: several ids with `--plan`

**The plans sliced together go to the integration branch together** (D-125), from `slices/<slug>/plan`.
The slug is the slicing's — the successors' `sliced_from:` lead, or the one the intent skill named.

1. **Each cycle's gate, in its own folder:** `sdlc gate <id>` in `../<repository>.cycles/<id>`. A plan not
   yet approved is not admitted: open the pull request with the ones that are, and say which wait. One
   ready plan alone goes by its own plan branch, as above.
2. **Compose the branch**, from the person's checkout:

   ```
   sdlc slices <slug> <id> <id>...
   ```

   Run the one command it prints. It refuses a sibling with no plan branch and a plan that needs a file only a
   sibling's plan creates — that piece is a successor, and the refusal says so; show it and stop. Files two
   plans both name it says on standard error: put them in the body, since those pull requests may conflict
   at merge.
3. **Check it before pushing:** `sdlc verify-chain slices/<slug>/plan --base <integration>`. It is the check
   the pipeline will run — every cycle's admission, the successors sound, the siblings atomic.
4. **Push and open** as for one plan, with the branch deleted on merge.

**The body is the slicing:** one line on how the work was sliced and why, then each plan as you would write one
plan's — the problem, the criteria, the shape, the tier — and then each successor with what it waits for, from
`sdlc successors`. The reviewer is reading the rest of the work beside its first plans.

### A sandbox: `--plan` with no ids

**In a sandbox, what leaves is what the person approved** (D-144). No id is named: the sandbox's approved cycles
are the set, and a cycle still being planned stays behind for a later admission.

1. **Compose the branch**, in the sandbox:

   ```
   sdlc admit
   ```

   Run the one command it prints: it opens the admission branch — `cycle/<id>/plan` for one approved cycle,
   `slices/sandbox-<id>/plan` for several — in a folder beside the sandbox, from the integration branch, with
   each approved cycle's folder and every successor the sandbox wrote. It refuses outside a sandbox, with
   nothing approved, with uncommitted changes in an approved cycle's folder, and with a plan needing a file
   only a sibling's plan creates; show the refusal and stop. On standard error it names what stays behind and
   the files two plans share.
2. **Check it before pushing**, in that folder: `sdlc verify-chain <branch> --base <integration>`.
3. **Name every cycle it takes when you ask to push**, by id and headline, and what stays. That read-back is
   where the person sees what they are admitting, since they named no id.
4. **Push and open** as for one plan, or as for a slicing when it carries several, with the branch deleted on
   merge. Once it has merged, `git worktree remove` the folder; the sandbox carries on with what stayed.

## A cycle that did not pass: `--draft`

Opened for a `blocked` or `escalated` cycle, usually by the `build` playbook in its own process (D-85). **Refuse if `sdlc pr-ready`
gives any other reason** — not verified, or the chain broken — because then there is nothing true to show.
Open it as a draft (`gh pr create --draft`, `az repos pr create --draft true`), and lead the body with
what stopped it, so the person picking it up starts there: the outcome; each criterion `verify.md` marks
Not met, by its number with its reason as `verify.md` states it; and each open blocker as
`sdlc findings <id> --stage verify --blocking` prints it — its rule, its gist, where, and its fix. Copied,
not summarised: the reviewer reads the body, not the ledger. Its check will be red,
which is accurate. A person remedies it on this branch and marks it ready.

## A run: `--run`

On a run's branch — `wave/<id>` or `series/<id>` — **one pull request for every cycle the run built**, never one per
cycle. Run by the `build` playbook at the end of a wave, and by the `series` playbook at the end of a series.

```
sdlc pr-ready
```

With no id, on a run's branch, it answers for the run — the check its pull request will meet. **It decides draft or
ready**: ready when it holds, a draft when it refuses because a cycle did not pass, and nothing pushed when it
refuses for anything else — show that and stop.

**The body is a short summary, then the report, verbatim:**

1. **What was built and the reasoning**, in brief plain language: a sentence or two per cycle, headed by its id
   and headline so a comment can name it. Write it from the cycles' plans and their reports. **Never restate a
   deviation, and never leave one out**: those are the report's, and a summary is where one quietly goes missing
   (D-181).
2. **What `sdlc report --run` prints**, copied as it stands — each cycle's outcome, deviations, choices and
   concerns, what stopped and what it held back, and for a series every intent line nobody confirmed.

Push the branch and open the pull request into the integration branch as below, with `--draft` when `sdlc pr-ready`
said so. **An open one is refreshed, not opened again**: push, and replace the body as for one cycle. Ask nothing:
the run was the decision.

## A verified build: ask first, every time

```
sdlc pr-ready <id>
```

It refuses for five reasons, and each is final rather than advisory:

| Refusal | What it means |
|---|---|
| not verified | there is no verification yet |
| did not pass | the verdict was `blocked` or `escalated` |
| escalated | remediation stopped making progress with findings still open |
| chain broken | it verified, and something has changed since |
| a test run | a local build wrote one of its rounds, or the solution pins one: it is abandoned, and the work is done again on a published release (D-135) |

**Do not push past a refusal.** The chain-broken one in particular: the pull-request check would refuse
it anyway, so pushing would cost a round trip through CI to learn what you were just told.

## When the build opens it, ask nothing more

**Saying *build it* was the consent to push** (D-178). The `build` playbook carries a cycle all the way here, and
its process runs this skill as soon as the cycle has an outcome, with no question of its own: the plan's hand-back
told the person that *build it* implements, verifies, fixes what it can and opens the pull request, and they
said it. Asked for by hand — *open the pull request*, or `/sdlc:pr` — the one question before the push names
the cycle, by id and headline, as above.

**A harvest is never asked here.** It reads what the cycle settled, and the pull request is where a person may
still ask for a change; the report's last line suggests `/sdlc:harvest` once it has merged (D-182).

## When the pull request is already open: push and refresh its report

After `remediate` has gone round on a cycle whose pull request is open, **this skill updates it rather than
opening another**: `git push` on the branch it was opened from, then the body's report replaced with what
`sdlc report <id>` prints — write it to a file, and `gh pr edit <number> --body-file <file>` on GitHub, `az repos
pr update --id <number> --description <text>` on Azure DevOps. A draft that now passes is marked ready
(`gh pr ready <number>`, `az repos pr update --id <number> --draft false`). Ask nothing: the person asked for the
change, and the playbook ran this for them.

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
it cannot write them itself, because that file is one of the seven only a person writes. Until they are
pasted the record still says `platform: local`, so `doctor` still reports no check governs a merge and
this skill still stops at the question above. Hand over what `init` printed and say it is the step that
finishes the migration.

Then **tell them to set the branch policy**: `sdlc branch-policy` prints the commands, which they run as an
administrator — pull requests only into both branches, the check required, nobody bypassing (D-136). A
pipeline that runs and is not required is a pipeline a merge can ignore, and that is the difference between
the guarantee and a green tick. `sdlc doctor` reads the policy back when the platform's CLI is signed in.

**A local cycle can still be built, verified and committed.** Only the pull request needs a remote. What
it does not have is the pull-request check, which `CONCEPT.md` §13 calls the only non-bypassable layer,
and nothing on one machine stands in for it. Say that plainly rather than letting somebody believe a
local cycle is governed to the same standard.

## Before any push: has the branch taken the latest target?

**Every pull request this skill opens or updates asks this first, on the branch it pushes** — the cycle's own, a
slicing's or a sandbox's admission branch in its folder, a run's branch (D-227):

```
sdlc up-to-date --base <target>
```

`<target>` is where the pull request goes: the integration branch, or the release branch a cycle's `release`
names. It fetches the target from origin, so a stale local copy cannot make the branch look current, and exits 0
when the branch holds the target's head. **It refuses a branch behind the target, and a target it could not
fetch, and neither is pushed past:**

- **Behind, on a branch of records alone** — a plan, a slicing, a sandbox's admission: nothing was built, so
  bringing the target in is the pull. **When it says the merge is clean, run the merge it prints and ask again**,
  then go on to the row's own question, which now reads the merged branch. When the merge stops on a conflict,
  `git merge --abort`, show the refusal and stop: a person settles a conflict in a record.
- **Behind, on a branch of built code** — a verified build, a draft, a run: the merge changes the code the
  verification judged, so it is not yours. Show the refusal and hand back; whoever ran this routes it. The
  `build` playbook takes a clean merge through `remediate` and asks for the pull request again; a person reads
  the refusal's `/sdlc:remediate <id>`.
- **The target could not be fetched**: show it and stop. Nothing was compared.

## Push and open the pull request

```
git push -u origin HEAD
```

`HEAD` is the cycle's own branch, checked out. Target the integration branch, as above.

Then open the pull request with the platform's own tool — `gh pr create` on GitHub, `az repos pr create`
on Azure DevOps. If neither is available, say so and give the person the branch name and the body to
paste; do not try to reach the API by hand.

**On Azure DevOps, name the organisation, project and repository**, read from the remote's URL:
`az repos pr create --detect false --organization <url> --project <project> --repository <repo>`. The CLI's
own detection can resolve a cached remote to a repository id that no longer exists, and refuses with
`TF401019`. **The description holds at most 4,000 characters** there, so when the body is longer, keep its
`Deviations` section and its criteria and link the cycle's documents rather than quoting them. If it still
fails on an argument or a missing extension, say so plainly instead of trying variations.

**The body is the cycle, not a summary you invented.** It should carry:

- **first, the report**: what `sdlc report <id>` prints, copied as it stands — every deviation, every choice
  the build made that nothing approved settled, and every concern this cycle's verification raised and did not
  block on (D-181). **Merging accepts everything in it.** Nothing was answered before the pull request, so
  this section *is* the review: a reviewer merging it accepts each line, and one who wants a line otherwise
  says so in words and the agent changes it, on this pull request
- the intent's problem, in the intent's words
- the spec's criteria, and the verification's verdict on each
- the tier, so a reviewer knows how reversible this is
- a link to or quote of the `Risk` section, which is the part written for them
- anything in a `## Open` region, because a reviewer should meet the limits before they approve
- **a `Departures` section when the cycle departs from a recorded decision**: the lines `sdlc status
  <id>` prints under `Departs`, copied as they stand — the decision in its own words, its id after — and
  nothing when there are none. A reviewer is approving those too, and it is the one thing in the change that
  sets aside something a person already decided

**Link the work item.** When the intent's frontmatter records a `lead`, close it from the body: `AB#1234`
for `azuredevops:1234`, `Fixes #56` for `github:56`. **Link a sliced work item without closing it** while any
other piece of its slicing is still to come — another cycle recording the same `lead:` that has not merged, or a
successor whose `sliced_from:` names it (`sdlc successors`): write `#56` or `AB#1234` with no closing word, and
the last piece's pull request closes it. A cycle begun from `successor:<slug>` names the item in its
`## Problem`. When it records a `release`, the pull request targets
that branch instead of the integration branch, and after it opens, `sdlc branch <id> forward` starts the
second one that carries the same fix into the integration branch — cherry-pick the cycle's commits onto it
and open that pull request too (D-88).

## What you are not doing

**You are not approving the merge.** A person merges the pull request — and reviews it, with whoever branch
policy asks for — and it is never the agent that authored the change. **The merge is the acceptance**: of the
change, and of every deviation and choice the report lists (D-181). The local approval defended against an agent that forgets; this review is what
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

**One line per cycle**, carrying three things: its id, its branch, and the pull request's link. For a
verified build, two more lines: *merging accepts it, report and all; to change anything in it, say what instead
— `/sdlc:remediate` takes it in words, on this pull request*; and *once it merges, `/sdlc:harvest` brings the
rules, decisions and stack up to what it settled* (D-104, D-181). Then stop. Nothing after this is yours — the merge belongs to a
reviewer, and a summary of what they are about to read is a reviewer being told what to think.
