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
