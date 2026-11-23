# Changelog

What each release changed, newest first, and what moving to it asks of a solution. **Where the project stands
today is the README's** — [Where it stands](README.md#where-it-stands) says which capabilities have met real
use and which only the suite. This file is the history behind that table, and it is written once per release
and never revised to look better afterwards.

Why each change was made is `CONCEPT.md`'s, cited here by decision id rather than restated. The rule for which
number moves is in [`AGENTS.md`](AGENTS.md), under Releasing: a **minor** is where a change that can make a
cycle block, or changes what a person or a stage writes, is announced.

## 0.55

**Three sizes of run, each ending in one pull request** (D-205). A cycle is `/sdlc:build`, as before. A **wave** is
`/sdlc:wave`, renamed from `/sdlc:next`: every successor and parked cycle ready now, planned with the person in one
sandbox, then built one after another on `wave/<id>` into one pull request; under `approval: required` it still stops
at the approvals first. A **series** is `/sdlc:series`: wave after wave into `series/<id>`, in one worktree, until the
number of waves a person set or an empty backlog. Under `approval: none` it suggests each successor's intent, plans
and builds without asking anybody; under `approval: required` it plans nothing and builds approved cycles in wave
order. A blocked verification stops only that cycle and the cycles that depend on it. Its record,
`.sdlc/series/<id>/series.json`, is written only by the binary. `sdlc build --run`, `sdlc series start|wave|status|run`
and `sdlc branch <id> wave` are new. **The factory runs only series**: its pipeline's `cycle` input is replaced by
`waves`, and its process is started by the binary with its tools named, no longer with permission checks off.

**Readiness is judged against the branch the work builds onto** (D-206): a predecessor is ready once its passing
verify is committed there — the integration branch for a cycle or a wave, `series/<id>` for a series. `--onto
<branch>` on `backlog`, `cycles --queued`, `unpark`, `lead show` and `successor start` names it, and a run's branch
implies it. In a run each cycle's evidence is measured from the cycle before it; a cycle recorded again keeps its
base, and a merge of the integration branch taken in after the base is measured out of its change — recorded as
`merged.sha` beside `base.sha`, which needs git 2.38 or later. The pull-request check for `wave/<id>` and
`series/<id>` runs the whole chain for every cycle the run changes, then requires every test named in every built
cycle's plan to have passed at the run's last recorded run, and every change outside `.sdlc/` to belong to a built
cycle's recorded change.

**One report per run** (D-207). `sdlc report --run` renders the report a wave or a series ends on: a short entry per
cycle with its outcome, deviations, choices and concerns, a section per wave of a series with the forecast beside
what ran, what stopped and what it held back, every intent line a series left `(inferred)`, and every rule, decision
and successor named with its text. It is the pull request's body below a brief summary the agent writes, and a copy
goes to `.sdlc/reports/`.

**A series is abandoned in one step** (D-208): one abandonment record beside the series' record, printed by `sdlc
draft abandon --series <id>` and placed by a person, abandons every cycle that exists only in the series; an approved
cycle already on the integration branch stays as it stands, to be built again. A rejected series' cycles are gathered
from its branch for a harvest, and `sdlc harvest close` removes the series' folder with its last cycle.

**A harvest may answer a suggestion *covered*, and hands back by theme** (D-209). Under `## Covered`, a line names
the rule, decision, successor or file that already handles a suggestion; `sdlc harvest record` checks it exists and
refuses the sitting when it does not. A covered line's `Concern:` takes a tallied concern out of the ignored and
watch tallies. Declined, covered and ignored lines may be grouped under `### <theme>` headings. `sdlc harvest summary
<proposal file>` prints each answer counted by theme and every id the file names with its text, and every harvest
skill hands back through it; `sdlc harvest --history` shows ids with their text too.

**What moving to it asks of a solution:**

- **`/sdlc:next` is now `/sdlc:wave`**, with no alias. A wave's cycles now go out in **one pull request**, not one
  each.
- **The factory pipeline's `cycle` input is replaced by `waves`.** Rewrite it: run the upgrade pipeline, or `sdlc
  factory --keep` on a branch and merge it — a pipeline still carrying the old `build the queue` step does not carry
  this release's steps, so it is rewritten and the team's pool named. Its process now runs with its tools named
  rather than with permission checks off, so **a solution whose build needs a command beyond the defaults declares it
  in `build_tools`** in `solution.md` (D-202), or the factory's run is refused it.
- **Harvest suggestions have a fourth answer.** A concern a rule, decision, successor or file already handles goes
  under `## Covered`, not `## Declined`; a covered line naming nothing real refuses the sitting at `harvest record`.
- **`.sdlc/series/` is new.** Its `series.json` is written only by the binary and denied to agents, as the harvest
  ledger is; the abandonment record beside it is a person's file.

**From 0.55.1 the upgrade pipeline can finish a move.** Until it, no run could open its pull request, on either
platform: the script ran `sdlc init` before setting a git identity, so adoption's own commit failed on any agent
without one; had it succeeded, it would have left nothing staged for the script's commit, which then ended saying
the release writes nothing new. The identity is now set first, adoption's commit is taken back into the working
tree, and only what an upgrade may carry is staged, as before. The script runs the release through the tool manifest
(`dotnet sdlc`) rather than replacing the agent's global binary, which failed with *Invalid cross-device link* on an
agent image that carried an older release in a lower layer. And `sdlc init` now commits the pipelines it moves into
`.azuredevops/` (D-203) whole, rather than leaving the old files deleted and the new ones untracked. A suite test now
runs the script under bash, with no git identity, against a throwaway remote. **A solution's upgrade pipeline is the
copy on its own integration branch, so its first move to 0.55.1 or later is made by hand, once**: `dotnet tool update --local
MofonCode.Sdlc.Cli --version <release>`, `sdlc init`, `sdlc factory --keep`, on a branch, into a pull request. The
pipeline it writes makes every move after that.

**From 0.55.2 a machine's upgrade no longer looks broken after a build past `.0`.** A plugin installed from the
marketplace says `0.55.0` whatever the build, because the build is stamped only into the package (D-197), yet `sdlc
version`, the `upgrade` skill's check and the install guide all said the plugin's number had to match the binary's,
so every upgrade to a `.1` or later reported one half behind. They now compare the first two numbers, as the
session-start check already did. `sdlc doctor` expects `Skills (29)`, not 28. Nothing changes for a solution.

## 0.54

**A harvest keeps what it found about the framework** (D-204). Notes under `## For the framework` were written into a
report git ignores and lost at close. `sdlc harvest record` now keeps them in `.sdlc/framework-harvest.json`, each
headed by the part of the framework it is about, with a kind — `cost`, `missing` or `defect` — the releases its
cycles ran on, and its evidence, which stays in the solution. `sdlc harvest close` keeps each closed cycle's
releases, what each reader raised, and what each document cost. `sdlc harvest framework` prints what of both could
be handed to the framework's maintainers, without the evidence, cycle ids, files, commits or the solution's own
rules, and `--out` writes it as a file. It sends nothing. The constraints report prints the release each cycle ran
on, and `harvest-constraints` notes a rule any solution would need as `missing` as well as proposing it locally.

**What moving to it asks of a solution:** a proposal file written by an older release whose `## For the framework`
headings carry no surface or no `Kind:` line is refused at `sdlc harvest record`. Record it before moving, or give
each heading a surface — `stage:verify`, `perspective:security`, `CON_06` — and a `Kind:` line, then record.

## 0.53

**The framework's Azure DevOps pipelines move into `.azuredevops/`** (D-203), named as on GitHub:
`sdlc-verify.yml`, `sdlc-upgrade.yml` and `sdlc-factory.yml`. A team's own `azure-pipelines.yml` is no longer in the
way, so a solution that already had CI gets the chain check written rather than printed. The chain check is kept
while it carries the release's steps, with the team's own beside them.

**What moving to it asks of a solution on Azure DevOps:** take this release by hand, not through the upgrade
pipeline, because the one an older release wrote commits only the paths it knew. On a branch, `sdlc init` moves
each framework pipeline from the root unchanged, your own steps with it; merge that by pull request. Then point each
pipeline in Azure DevOps at its new path — **Edit > ... > Settings > YAML file path** — and the build validation
policies follow. `sdlc doctor` says so while any is left at the root. On GitHub, nothing.

## 0.52

**A build's own process can run its own steps** (D-202). `sdlc build` started its process without the skills its
playbook runs, so on a machine whose own settings did not already allow them, every build was refused `implement`
and ended without a report. The process is now allowed `implement`, `verify`, `remediate`, `pr` and
`resolve-conflict`, and the suite holds that list to the skills table.

**A solution names the commands its build needs beyond the framework's own**, in a new optional key in
`solution.md`: `build_tools: pwsh, docker`. Each bare command name is allowed to the build's process, and nothing
wider. The build playbook now says how a headless process writes its commands so a permission check can read them,
and the refusals guide has an entry for a tool the build's process was not given.

**What moving to it asks of a solution:** nothing, unless its build runs a command beyond `sdlc`, `git`, `dotnet`,
`gh` and `az` — an evidence script's shell, a container. Then add it with `sdlc draft solution` and a rename. A
person who allowed the sdlc skills, PowerShell or Docker in their user-level settings to get a build through can
take those rules back out.

## 0.51

**The upgrade pipeline runs on the agent the platform names** (D-201). On Azure DevOps it runs on the pool the
pipeline variable `SDLC_AGENT_POOL` names: a pool of your own, or `Azure Pipelines` for a Microsoft-hosted agent. An
organization with no Microsoft-hosted parallel job could not run it before. On GitHub, the repository variable
`SDLC_RUNNER` names a label of your own runners, and unset it takes a GitHub-hosted runner as before.

**What moving to it asks of a solution on Azure DevOps:** set `SDLC_AGENT_POOL` on the upgrade pipeline before its
next run, and for a pool of your own, let the pipeline use it once, under the pool's Security > Pipeline
permissions. Unset, a run fails with *Could not find a pool with name $(SDLC_AGENT_POOL)* and changes nothing. A
solution taking 0.51 by a run of its 0.50 pipeline sets it before the run after.

## 0.50

**An intent is suggested and confirmed, and the interview is on request** (D-200). `/sdlc:intent` reads the
solution's decisions, rules, declared stack and the code your request touches, writes `intent.md` straight away and
prints it, with every line that is its own reading marked `(inferred)` — a premise carried from a lead you never
said included. It asks in one message about those lines, a departure from a recorded decision, and the
perspectives it added, and edits the file as you answer. `--interview` asks one question at a time as before; a new
solution is always interviewed, and the skill offers the interview when a request is too thin to read a direction
from.

**An intent that still marks a line `(inferred)` is a shape problem**: its panel names it, `to-plan` stops that
cycle, and `verify-chain` refuses it at the pull request.

**A solution takes a release by running its upgrade pipeline, and a machine by `/sdlc:upgrade`** (D-201). Adoption
now writes `.github/workflows/sdlc-upgrade.yml`, or `azure-pipelines.upgrade.yml` on Azure DevOps. A person starts it
on the platform, naming a release or leaving it empty for the newest. It moves the pin, runs adoption and
`sdlc factory` as that release, and opens the pull request the upgrade check accepts. It never runs on a schedule.
On GitHub it needs a GitHub App's id and key (`SDLC_UPGRADE_APP_ID`, `SDLC_UPGRADE_APP_KEY`), because a pull request
opened with `GITHUB_TOKEN` starts no workflow. `/sdlc:upgrade` moves this machine's binary and installed plugin to one
release and checks they agree. `sdlc upgrade` points to the pipeline where a solution has one. **From 0.50.1** the
pipeline runs `sdlc factory --keep`, which leaves a factory pipeline as the team has it, its own pool and steps
included, while it still carries every step the release writes; one that does not is rewritten, the team's pool is
named, and the pull request says to put back what the team added. Before it, every run rewrote the file and dropped them.

**A release no longer asks for an agent to be rebuilt.** Every run already installs the release its branch pins, so
the image's copy is a cache. The new **agent-image** guidance says how a solution keeps its own agent's image
current: the definition in the repository, and a rebuild a pipeline asks for and checks. Open cycles stay on the
release they opened on.

**What moving to 0.50 asks of a solution.** The move to 0.50 itself is by hand, as before, and it writes the
upgrade pipeline. Set up its token once, as the installing guide's Update section says. Every move after that is a run.

**A minor** because what the intent stage asks of a person changes, and an intent can now be refused for a mark
left in it. No intent written before this release carries one.

## 0.49

**Every merge to main that ships is a release a person approves or rejects** (D-197). The framework builds from one
branch: a feature branch is squashed into `main`, `ci.yml` builds and tests it, and `cd.yml` waits for approval and
publishes. There is no `development` branch and no preview. The version is `<major>.<minor>.<build>`: the line is
declared, and the build is counted from the merges since it moved, so `0.49.0` is the first and every release after
it the next number. The major stays 0 while the framework is alpha.

**An agent may run adoption** (D-199). The hook no longer refuses `sdlc init`, so the `init` skill runs it rather
than handing you the line, an agent refreshes a solution after an upgrade, and a commit message or search that
mentions the command is no longer refused. What adoption decides it still only drafts, and you place each draft;
the skill then commits what you placed, since the rename was the decision.

**The session-start check compares the release line.** A plugin installed from the marketplace says `0.49.0`, and a
binary from any build of 0.49 is the same release to it.

**A trunk is supported as a setting** (D-198). Name the default branch as the integration branch --
`integration_branch: main` -- and `sdlc branch-policy` prints one squash policy and no promotion, where it printed
two policies that left no way to merge. It asks the platform which branch is the default, as `sdlc doctor` already
did; unanswered, it prints both and says which a trunk skips. The pipeline guidance says how a trunk delivers to
dev and prod on GitHub and Azure DevOps, and that a build nobody promoted ends cancelled or skipped, never red.

**A proof table's Case is not read as a test.** A criterion's first row whose Case opened with a name in backticks
-- a file, `pipeline.yml` -- joined the run of test names the entry opens with, and `verify-chain` refused a
verified cycle because no test of that name had passed. The chain now reads only the Test column as names, so a
plan that met this needs no reopen: run `verify-chain` again on a build that has the fix.

**A minor** because the numbering changes what a solution pins: from here a pin names a build, and the newest build
of a line is the one to take. No cycle blocks differently.

**Moving to it.** Pin `0.49.<build>`, the newest on nuget.org, with `dotnet tool update --local
MofonCode.Sdlc.Cli --version <it>`, then `sdlc init`, as for any release. There is no preview to pin any more.

## 0.48.0

**A pull request that changes only `.sdlc/` is not built** (D-193). `sdlc admission` answers `true` whenever every
path a pull request changes is under `.sdlc/`, whatever the branch, so a harvest or a successor review skips the
build and the tests as a plan already did. The chain still judges each one, and the harvest check already parses
the records and successors it carries. A pull request carrying successors alone had taken a governed solution's
full run: thirteen minutes, ten of them one end-to-end suite.

**A promotion always builds, and skips the tests the integration branch already ran** (D-196). Both generated
pipelines ask the platform for this pipeline's green runs on the source branch and hand them to the new
`sdlc tested`, which says `true` only when the promotion merges exactly that tip and a green run of it is listed.
The build still runs, so the release carries its own version. Anything else runs everything.

**Guidance: the framework's opinion, handed to a plan, overridden by the solution** (D-195). A guidance entry is a
short document naming the files that switch it on. The framework's are in the binary; a solution's own under
`.sdlc/guidance/` replace them by name. `sdlc guidance <id>` hands a plan the entries its declared files switch on,
and the `plan` skill reads them last — after the code, the decisions and the rules — and says why in a line where
it departs. Nothing judges a plan against guidance and it never blocks. The first entry is the pipeline.

**Every concern the harvest counts keeps its reason** (D-194). A concern watched below the bar, or ignored, carries a
`Why:` line — what it prevents and what it promotes — so the case for it survives the cycle folders it came from.
`sdlc harvest --history` prints it.

**A minor** because a stage writes more than before: a plan reads guidance and says why where it departs, and a
harvest sitting writes a reason under each new concern. The pipeline templates gain a step.

**Moving to it.** **A solution on GitHub** receives the new promotion step in the upgrade pull request that moves its
pin, written by `sdlc init`. **A solution on Azure DevOps** whose `azure-pipelines.yml` was its own before adoption
gets the template printed rather than written, as always: place the step named *has the integration branch already
tested this*, and the `ne(variables.tested, 'true')` condition on its test and publishing steps. Without it,
promotions keep running the tests and nothing else changes. The records-only skip needs no pipeline change: the
pipeline already asks `sdlc admission`, which answers the wider question once the pin moves. For the promotion step
to read the runs, a GitHub workflow's token needs to read actions, and an Azure DevOps build service needs to read
builds; where it cannot, the tests run.

## 0.47.0

**The framework says what it is, in one definition, everywhere it introduces itself.** The README, `CONCEPT.md`
§1 and both plugin manifests carry the same statement word for word, and `marketplace.json` and the package page
open with its first sentence; the README leads with why to choose it and the steps a
person walks; Where it stands is one row per capability; and this file holds the release history the
README used to. The package page nuget.org shows is rewritten in the same voice, with its skill and
protected-file counts corrected and held by the suite, and the definition's first sentence is held in every place
the framework introduces itself. The documents, guides, skills and the binary's own help were read against each
other and brought into agreement. No solution that adopted the framework is named anywhere in the repository:
evidence a release rests on is described, not attributed.

**A blocking finding must stand on a rule that binds the document it judges.** A blocker citing a superseded rule,
another document's rule or an id nobody issued is drafted as a suggestion saying so, and `sdlc round` refuses a
draft that still carries one (D-174). A perspective blocks only on the rules its document lists as bullets, where
they bind: operability on `CON_20` at a spec, the others on nothing (D-175). Three rules now forbid what their
readers were already told to apply them to: `CON_25` supersedes `CON_07.2` and holds a change to what the plan or a
deviation describes in any file, `CON_26` supersedes `CON_05` and counts a criterion another rule requires as
supported, and `CON_27` supersedes `CON_24` and accepts a decision deviation as a declared departure (D-176).
`CON_12.2` and `CON_10.3` are wording revisions. A finding carries its rule's own line word for word: `round
collect` copies it in, and `sdlc round` refuses a new finding quoting it otherwise (D-177). Rounds already recorded
are not judged again.

**A person walks five steps, and *build it* goes all the way to the pull request** (D-178..D-182). Begin an
intent, take it to plan, approve (when the solution requires it), *build it*, review the pull request. *Build it*
takes the approved plan out of its sandbox itself, then, in a process of its own — the factory's path for one
cycle, asking nothing — builds, verifies, remediates while that is working and opens the pull request. A stalled
remediation may take one different approach as a deviation, never changing what *done* means or widening the
cycle, before it escalates when that stalls too or three deviations bend the same text; the every-fourth-pass
check-in is gone (D-179). Where the approved text was silent and a reviewer would want to know, the build records
a **choice** in `deviations.md` (D-180). It hands back **the report** — every deviation, every choice and every
concern, `sdlc report <id>` — which leads the pull request, and **merging accepts it**: deviations are no longer
answered in the approval ledger, the pull-request check and a reopen no longer wait on an answer, and
`draft approval --accept/--decline` is gone. To change a line of the report, a person says what instead and
remediate changes the code on the same pull request, asking until the instruction says what to do (D-181). No step
names a new session or an admission, and the pull request no longer asks about a harvest: the report's last line
suggests it once the pull request has merged (D-182).

**The binary starts every process the chain runs** (D-183). No skill starts a reader, an adjudicator or the build
itself any more, so a skill needs no permission to start one and no multi-line shell its host could not
pre-approve — which is what kept a headless build from getting past its first reader. `sdlc round run` starts a
round's readers, `sdlc harvest read` and `sdlc harvest review` a harvest's, and `sdlc build` is *build it*: it commits
the approval a person placed if it is uncommitted, admits the plan, prepares the build's worktree and starts the
build there with its tools named and permission checks on. A person's change at the pull request, and their
instruction after an escalation, reach remediate: `sdlc cycles --here --for remediate` finds a cycle `blocked`,
`escalated` or passed with its pull request open, `sdlc branch <id> remediate` prints the worktree to work in, and
`sdlc evidence --retire-pass` retires a pass a person asked to change. The panel sends a blocked cycle to
remediate. A deviation never narrows a criterion: one the code cannot meet escalates (D-179).

**Every skill is a playbook or does one job, and building has one name** (D-186..D-188). The kind is what a skill
does, never who starts it. A **playbook** composes: it runs other skills and does no stage work itself — `to-plan`,
`next`, `build`, `remediate` — and what runs what never loops back. Every other skill does one job, one kind of
output, and starts no other. **`steer` and `resolve-conflict` are single skills now**: `steer` writes a correction
and `to-plan` checks again after it — `/sdlc:to-plan <id> "what to change"`, or just saying it, is how a person
steers — and `resolve-conflict` resolves and records evidence, pushing nothing, while *fix it* (`remediate`) runs it
first on a pull request that conflicts, then verifies and updates the pull request. The skills index declares each
one's kind in a table the suite holds, with four tests, and AGENTS.md states the rule. **`build` is the one
name for building**: given an id it builds that cycle, given none every approved one, and with `--unattended` it is
the build's own process and the factory's run. `batch-build` and `sdlc build-run` are gone, with no alias: `sdlc
build [<id>]` starts the build, and is the one stage name the binary runs rather than answers. **`implement`** is a
new skill — the code, the tests, the deviations and choices, and the evidence — which `build` hands the plan and
`remediate` hands findings or a person's instruction; `remediate` is the *fix it* playbook around it. **The loop
leaves the stage skills**: `intent`, `spec` and `plan` each write their document and stop, and `to-plan` runs the
interrogate-and-refine loop on each — the intent's first — handing back once (D-188, reversing D-29).

**Mining becomes a harvest, read one aspect at a time, and only from completed cycles** (D-189..D-192). A cycle is
**completed** once its pull request merged after a passing verify, or a person abandoned it, merged or not: a cycle
stopped before any of it merged is read from its own branch, only what the harvest keeps reaches the integration
branch, and `sdlc tidy` lists the branch once the harvest merges; nothing still in flight,
and nothing that passed verify without merging, is read or counted (D-190). The `harvest` playbook runs five skills,
each one aspect and callable alone — `harvest-constraints`, `harvest-decisions`, `harvest-suggestions`,
`harvest-stack`, `harvest-areas` — one at a time, each handing back a short report for the person to mark before the
next starts (D-191). **A decision is defined** (D-189): a choice with a real alternative that future cycles will meet
again, whose reason is not in the code, and that forbids nothing. A person's own words — a fixed decision, an
abandonment or reopen reason, a declined deviation — qualify once; a plan's prose only when the same choice recurs in
two or more cycles; a fixed decision marked unconfirmed never. An abandoned cycle gives its reason, its findings and its
suggestions, never its plan's approach. **A cycle closes only once every aspect is answered**: `sdlc harvest close`
checks the ledger, records each closed cycle and commits the removal of their folders on a `harvest/<date>` branch,
and `close-cycle` pushes it and opens the pull request (D-192). The ledger is `.sdlc/harvested.json`. **`propose` is
retired**: its constraints and decisions are harvest aspects, a rule a person has in mind is `sdlc constraints --new`,
and the theme's skeleton is `sdlc theme`, now a skill of its own. `sdlc mine`, `sdlc draft mining` and `sdlc propose
...` answer with their new names. The skill count is now twenty-seven.

**A minor** because a round may now demote or refuse what it recorded as blocking before, three rules change
what is forbidden, a deviation narrowing a criterion is refused, and a person walks different steps: they no longer
admit a plan, open a session for the build, say *open the pull request*, or answer deviations in a file; and
*take it to plan* now checks the intent, which the intent skill no longer does itself; and a harvest reads only
merged or abandoned cycles and closes one only once every aspect is answered.

**Moving to it.** A cycle in flight keeps its deviations: one answered in the approval ledger before this release
is still read as answered, and one left unanswered no longer holds the pull request — merging accepts it. A
criterion narrowing a person accepted before this release still holds; a new one is refused. **A solution with
a factory pipeline** receives `/sdlc:build --unattended` in place of `/sdlc:batch-build --pr --unattended` in the
upgrade pull request that moves its pin, which `sdlc upgrade` lists; nothing else needs a hand edit.
**A solution that used `sdlc propose` or `/sdlc:mine`** runs `/sdlc:harvest` instead; the old commands answer with
their new names. No solution had a `mined.json`, so there is no ledger to move.

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
