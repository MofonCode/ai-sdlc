# Changelog

What each release changed, newest first, and what moving to it asks of a solution. **Where the project stands
today is the README's** — [Where it stands](README.md#where-it-stands) says which capabilities have met real
use and which only the suite. This file is the history behind that table, and it is written once per release
and never revised to look better afterwards.

Why each change was made is `CONCEPT.md`'s, cited here by decision id rather than restated. The rule for which
number moves is in [`AGENTS.md`](AGENTS.md), under Releasing: a **minor** is where a change that can make a
cycle block, or changes what a person or a stage writes, is announced.

## 0.47.0

**The framework says what it is, in one definition, everywhere it introduces itself.** The README, `CONCEPT.md`
§1 and the three plugin manifests carry the same statement; the README leads with why to choose it and the seven
steps a person walks; Where it stands is one row per capability; and this file holds the release history the
README used to. The package page nuget.org shows is rewritten in the same voice, with its skill and
protected-file counts corrected and held by the suite, and the definition's first sentence is held in every place
the framework introduces itself. No solution that adopted the framework is named anywhere in the repository:
evidence a release rests on is described, not attributed. Nothing a cycle does changed. It is a minor as a planned milestone, not
because anything blocks that passed before.

## 0.46.0

**Four design gaps found by mining a real solution's cycles are closed.** A criterion only the run as a whole
could show, such as how long the suite takes, now breaks `CON_12` at spec, and the spec readers are told what the
evidence is (D-167). A proof living in code the change did not touch is handed to the verify as that test's
block, or a file the proof names (D-168); on one real cycle that is 98 KB per reader, cached with the shared
block. A run keeps its timings in `timings.json`, which no reader sees (D-169). `sdlc upgrade` prints everything
an upgrade moves, in order (D-170), and `sdlc suggestions <id> --renumber` mends an id a merge recorded twice
(D-171). In an adopted solution the plugin's hooks step aside for the ones adoption wrote, so each check speaks
once (D-172), and a successor can be marked as a gap in the framework, which mining lists to be filed there
(D-173). **A minor:** the widened rule can block a spec that passed before.

## 0.45.0

**A round costs about half.** The block every reader shares is handed over as a system prompt with one reader
started first, so the host caches it, and readers start outside the checkout, so they are no longer handed the
project's memory and git status (D-163). On one real plan round the blind readers went from about $0.79 to $0.33;
a verify round has not been measured. Perspectives read a cycle's first verify round only (D-164), a verdict
conceding on evidence a round already judged goes to remediate without another round (D-165), and `sdlc mine
--cycles` mines a backlog in batches (D-166). **A minor:** the prompt files changed shape, so a 0.44 skill
driving this binary would hand its readers only their own part.

## 0.44.1

**The binary does the hand work mining found.** A session settles earlier findings with `round collect
--answered/--withdrawn` instead of editing the round file. A person's answers to deviations are drafted in with
`draft approval --accept/--decline`, `draft solution` moves the approval and the coverage floor, and the approval
rename uses absolute paths, so it works from any worktree. `sdlc branch <id> build` finds a plan on an admission
branch. `sdlc tidy` prints the removal of what has merged, and the panel stops offering a pull request after the
merge (D-162). Nothing a cycle passed before blocks now, so it is a patch.

## 0.44.0

**What mining found in the plumbing is fixed.** The adjudicator reads the coverage, so a criterion about the
floor can be met. `verify-chain` fails when git would leave an evidence file out of the commit, and a new cycle's
ignore file keeps them in. Recording a new run retires a blocked verdict instead of refusing at the end of the
run. A fenced verdict is taken, a plan's areas are checked before its readers run, and the status panel stops
listing every successor (D-161). **A minor:** the chain has a check it did not have.

## 0.43.0

**No artifact declares a format version.** Every file a cycle wrote said `framework_version: 1.0.0`, which read
as the framework's release and was never true, and nothing read it; the format moved twice without it. It is no
longer written or required, and a file written before carries it harmlessly: an older cycle reads, verifies and
stays signed as it was (D-160). The release a round ran on is in its findings ledger, as before. **A minor:**
what a stage writes changed; nothing that passed now blocks.

## 0.42.0

**The seven-step cycle.** A person walks seven steps — begin an intent, take it to plan, steer, approve, build,
fix, open the pull request — and can say each in a sentence; every hand-back offers the next one in words, and
the panel names the step rather than `interrogate` or `refine` (D-158). `batch-plan` is now `to-plan`, takes one
cycle or every one, picks a cycle up where it stopped, and drafts the approvals. A green build verifies itself,
and remediation verifies again after each pass and goes round while it is working, checking in every fourth pass
(D-159). The new session is named once, before the build. **A minor:** a person types different things, and
`batch-plan` is gone.

## 0.41.0

**The coverage floor.** The whole codebase was held to the highest rate it ever reached, with no slack, and one
real solution at 99% refused a cycle that did nothing wrong once its test harness changed how coverage was
measured. Now a codebase at or above `coverage_floor` in `solution.md` holds whatever its baseline, and the
baseline follows it; under the floor it may still not fall (D-157). The key is new and defaults to 80%, so an
existing solution needs no edit. **A minor:** what a cycle is refused for changes, and a person has a key to
write.

## 0.40.0

**Deviations.** A build or a remediation that cannot follow the approved text records a deviation in
`deviations.md` and keeps going, instead of stopping for a reopen; every verify reader judges against it, and a
person answers each one with the same approval drafter before the pull request (D-156). **A minor:** build and
remediate write a new file, the verify readers read it, a change departing from the plan with no deviation
describing it is blocked, a reopen is refused while a deviation is unanswered, and the pull-request check waits
on the answers.

## 0.39.0

**Mining, areas and husks.** Reading the record across cycles is now `/sdlc:mine`; `converge` means only a
document's rounds settling (D-148). A recorded entry's title is its headline, held to fifteen words and each
clause to sixty. A solution may name the parts of its code in `areas.md`, built up from what plans propose, and
every reader then gets every headline but in full only what its change reaches (D-150, D-151). `sdlc mine record`
turns mined cycles into husks whose folders a person deletes through a pull request the check admits as mining
(D-152). **A minor:** a plan of a cycle opened on it names its areas, or its round is refused. Since 0.39.1 the
report lists the finished cycles' open suggestions, and a suggestion mining ignores is tallied by concern so one
ignored in three cycles reaches the bar (D-153). 0.39.2 lets an upgrade pull request keep a solution's own
factory pool while taking the release's other sections (D-154). 0.39.3 fixed `sdlc admit` crashing when it sorted
two approved plans.

## 0.38.0

**Planning in a sandbox, parking, and an intent that can still change.** The intent skill opens one worktree on
`sandbox/<id>` for every cycle a piece of work turns into, and `sdlc admit` composes what is approved onto an
admission branch the check already knows (D-144). Before the spec, `/sdlc:steer` takes a correction in words that
may add, drop or reword an outcome, and `/sdlc:intent --reslice` splits an intent or joins several (D-141). A
skill acting on one cycle takes its id as optional, and `sdlc cycles --here` names the cycle or lists the ones to
ask about (D-142). `sdlc park` keeps a plan that needs another's unbuilt code, `sdlc backlog` shows the parked and
the successors in the waves they come ready in, `sdlc unpark` takes one back once its predecessor merged, and
`/sdlc:next` plans whatever the last merges made ready (D-145..D-147). The factory is started by a person, never
on a schedule, and has no Operate step (D-143); a solution with a factory pipeline from an earlier release keeps
its schedule until somebody runs `sdlc factory` again and commits it, and `sdlc doctor` names it a problem until
then. `sdlc new` no longer refuses uncommitted work, which the plan switch refuses instead. `sdlc discard` says
what throwing a sandbox away would lose, and an abandoned cycle reaches the integration branch as a record by
`sdlc admit`.

## 0.37.0

**The decisions run through the whole cycle.** Every reader of an intent, a spec, a plan and a verify is handed
the decision record, a departure is declared where it arises and reported at the gate (`CON_24`), and a new
reader at verify holds the change itself to the plan and the decisions. **A minor:** a round may now block what
it passed before, since the rule against changes outside the plan's purpose reached no reader until this.

## 0.36.x

- **0.36.10 — running without approvals.** `approval: none` in `solution.md` lets a cycle build once its rounds
  are clean; a blocker, an uninterrogated document and a document edited after its round still refuse. Under
  it, nothing local anchors a findings ledger: an agent could rewrite one, and git history and the pull request
  are what remain (D-137). `sdlc settings` shows each setting and whether it was written or defaulted.
- **0.36.8 — branch policy has a default, printed rather than applied, on both platforms.** Pull requests only
  into the integration and default branches, nobody bypassing, the check required on both, squash into one and
  merge commits into the other, and only the integration branch or a `release/*` branch promoting, which the
  pipeline's `sdlc promotion` decides. `sdlc branch-policy` prints the commands and `doctor` reads them back
  (D-136); `--approvals`, `--owner` and `--self-approve` add reviewers, a required approver, and an author
  standing in for one.
- **0.36.7 — a cycle can run on a change to the framework nobody has published.** `SDLC_DEV_BUILD` hands every
  `sdlc` in a terminal to a local build, which calls itself `-local`; a cycle any round of which it wrote is a
  test run, and `pr-ready` and the pull-request check refuse it (D-135).
- **0.36.6 — dependency moves need no cycle.** A pull request that only moves versions in
  `Directory.Packages.props`, `global.json` or the tool manifest is checked by comparing everything else; a
  package added or removed is still a cycle's (D-134).
- **0.36.5 — the upgrade check compares a pipeline step by step.** 0.36.4's word-for-word comparison refused a
  real solution's move to it, which merged on a policy override; 0.36.5's comparison passes that same change
  replayed in a copy of the repository, and refuses an edited step of its own (D-133).
- **0.36.1 onward — a preview per patch.** `.github/workflows/ci.yml` publishes to nuget.org: a preview from each
  merge into development that ships, and a release from each merge to main once a reviewer approves it (D-135).
- **0.36.0 — a cycle runs on the release it opened on.** A solution pins the binary in
  `.config/dotnet-tools.json`, and the global `sdlc` hands every command on to that release. The release carries
  its own skills and hooks, and `sdlc claude` starts a session with them; plain `claude` loads the installed
  plugin, and a session-start hook says when that is another release (D-131).

## Before 0.36

- **The convergence changes.** The proof rules block what a test never reaches rather than what a reader can
  imagine (`CON_21` and `CON_22` supersede `CON_06` and `CON_08`, D-127); a suggestion may only sharpen an
  outcome (D-128); a plan probes the platform behaviour it rests on (D-129), proves in a table, and a verify is
  rewritten in place (D-130); each round records the release that wrote it (D-131); a criterion lists the cases
  it covers, so a case nobody listed is a suggestion at every later stage (`CON_23`, D-132). They were drawn from
  one real cycle of 88 rounds and six reopens. D-128's, D-129's and D-132's shape rules hold only cycles opened
  under them.
- **0.35.0 — `PRO_05`**, which asks a plan adding script to a Blazor page what Blazor already does. It reaches no
  solution adopted before 0.35.0 until a person adds its row to that solution's `techstack.md`.
- **0.32.0 — test output stays out of the commit.** A cycle opened before 0.32.0 keeps its committed `.trx` and
  `build.log` tracked: the ignore patterns stop new ones only, and `git rm --cached` removes the old.
- **0.31.0 — the findings ledger.** An artifact's rounds are one file, `findings/<artifact>.json`: each finding
  stored once, each round chained by hash, and an approval anchoring the round each ledger had reached (D-107). A
  cycle still holding round files is converted by its next round, with nothing to run. On a copy of one real
  cycle that took the planning documents from 17 MB of round files to 1.9 MB of ledger, with the same findings
  standing.
- **0.30.0** adds host tests to the skeleton, which a solution scaffolded earlier does not have, and `doctor`
  asks the platform whether branch policy requires the check (D-106).
- **0.29.0 — rounds through the binary, and coverage on the change.** A round is written by `sdlc round`, which
  refuses one out of the chain's order, and the hook denies writing one any other way (D-101). `verify-chain`
  refuses a change whose added lines are under 70% covered or that lowers the codebase's coverage, so a build
  records `--coverage` (D-103). The run mode `batch` is now `test`: an AppHost reading `SDLC_RUN_MODE=batch` has
  to read `test`, and the architecture suite a solution was scaffolded with before 0.29.0 does not carry the
  launch-profile test (D-102). `/sdlc:intent` plans each cycle in a worktree of its own (D-105). The .NET
  profile's *fake data and seeding* row reaches no existing stack until a person answers it.
- **0.28.0 — `PRO_04`**, an Aspire AppHost once a solution runs more than one process. It reaches no solution
  adopted before 0.28.0 until a person adds its row to that solution's `techstack.md`. 0.28.1 gave the factory's
  `claude` a credential, so a verify run unattended is interrogated.
- **0.20.0 — the declared stack is the only source of stack rules.** A solution adopted before 0.16.0 that never
  listed its stack rules is held to none: its old `profile:` line in `solution.md` is no longer read (D-81).
  `sdlc doctor` says so; `sdlc draft techstack` and a rename put the rules back.
- **0.18.0 — the pre-push hook is gone** (D-78): it was written and never installed, and where it ran it refused
  a push of work in progress. A solution adopted before 0.18.0 still has the file, and a clone that set
  `core.hooksPath` still runs it until somebody deletes the file or unsets the key.
- **`standard: profile` is named after something no solution records any more.** The key still means what it
  meant — new work follows the framework's conventions rather than the surrounding code — and the value was left
  alone because every existing `solution.md` is protected and already says it.
- **Iteration 3** shipped as `0.8.3` and is archived on the `archive/v03` branch.
