---
name: pipeline
files: "azure-pipelines, .github/workflows/, .gitlab-ci"
---

# Building the pipeline

How the framework would shape a solution's pipeline where the solution has not said otherwise. **Where the pipeline
already does something another way, or a decision or a constraint says how, that stands** — this is for what the
solution has not decided.

## What is not opinion

The steps that run `sdlc verify-chain`, `sdlc admission` and `sdlc promotion`, and the conditions on them, are
the gate. A plan may move them, never remove or weaken them.

## The defaults

- **Measure before adding a step.** Say in the plan what the step adds to every pull request's run, from a run, not
  a guess. A governed solution's run was thirteen minutes, ten of them one end-to-end suite; the build was 43 seconds.
- **Every pull request that changes code ends in a published artifact.** Build it once per run and publish what the
  build made, so what was tested is what ships.
- **Records alone build nothing.** A pull request that changes only `.sdlc/` skips the build and the tests; the
  generated pipeline already does, through `sdlc admission`. A new build or test step takes the same condition.
- **A test project that takes minutes runs in a job of its own**, in parallel with the rest, so the run takes as long
  as the slowest suite rather than the sum.
- **Tools a step needs are in the agent image**, not installed on every run: browsers, the `sdlc` release, SDKs.
  Installing them each run is minutes nobody sees in the step's name.
- **Restored packages are cached between runs**, keyed on the lock or props files that pin them.
- **One agent queues every run behind the last.** A pull request opened while the integration branch builds waits
  for it. Size the pool to how many pull requests are open at once, not to one.
- **A promotion builds and publishes, and tests only when it must.** A pull request into the default branch rebuilds
  so the release carries its own version — a package versioned as a preview cannot be renamed to a release, because
  the version is compiled in. Where the integration branch's last merge build already passed on the same tree,
  re-running its tests finds nothing new; where the default branch carries a change of its own, it runs everything.
- **New steps go where the template puts its own**, with the same skip conditions, and a comment saying why the
  step exists. The template's comments are part of the pipeline.

## One branch or two

**A trunk has no promotion.** Where the integration branch is the default branch, every pull request into it is
judged by the chain, `sdlc promotion` never says `true`, and the promotion default above does not apply. Each merge
to the trunk is a build that may be delivered; the stages below decide how far it goes. Where the two branches are
separate, a promotion is what reaches production, and the same stages hang off the default branch.

## Delivering to dev and prod

**A build that is not promoted is not a failure.** Red means something broke. A build that passed and was simply
not chosen for production ends cancelled or skipped, never failed, so the run list shows what broke and nothing else.

- **One pipeline, three stages: build, dev, prod.** Every stage deploys the artifact the build stage made, so prod
  runs exactly what dev ran. Pull requests run the build stage alone.
- **Dev deploys every merge to the branch that delivers, on its own. Prod waits for a person**, as an approval on
  the prod environment.
- **The newest build replaces the one waiting.** A build waiting for prod stays open until a newer one arrives,
  which cancels it, so nobody approves a stale build and nothing queues behind it. Leave the approval's timeout
  unset: the platform closes an approval nobody answered after its own default, 30 days on both.
- **Reject is for a build that must never ship**, and shows red because something is wrong with it. To pass over a
  build, leave it for the next one to replace.
- **Only the delivering branch can deploy.** The environments, and what they hold — service connections, secrets —
  admit that branch alone, so a pull request that edits the deploy stages cannot run them.

**Azure DevOps.** Multi-stage YAML: a `deployment` job per environment, with an Approvals check on `prod` and an
Exclusive lock check whose `lockBehavior: runLatest` cancels an older waiting run when a newer one arrives. Branch
control on the environments and the service connections. An approval that times out marks its stage skipped.

**GitHub Actions.** Environments `dev` and `prod`, with required reviewers on `prod`. Approve in a small job of its
own that names the `prod` environment and sits in a concurrency group with `cancel-in-progress: true`, so a newer run
cancels only a waiting approval; the deploy job after it sits in a group that never cancels, so a deploy is never
stopped halfway. Deployment branch rules on both environments. A job waiting more than 30 days fails, which the
platform does not let a team change.

**Where somebody else owns prod**, prod can instead be a second pipeline that a person runs, choosing which build to
send. A build nobody sends is then a finished green run with nothing waiting, at the cost of a second pipeline and
picking the build by hand.
