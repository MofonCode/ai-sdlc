# Handling refusals

Every refusal the framework produces, what it means, and what to do next. `../reference/refusals.md`
is the same list as one line each.

**A refusal is a command or hook declining to go on.** It is not the same thing as a blocker — a
blocking finding on a document, or a `blocked` verification — though an open blocker is one of the
things that causes refusals. Where the remedy for a refusal is answering a blocker, the entry below says
so and `handling-blockers.md` has the walk.

**A refusal is the gate working. A failure is the gate broken.** Those are different exit codes on
purpose, so a hook never reports its own bug as a governed stop:

| Exit | Means | |
|---|---|---|
| `0` | the command did what was asked | |
| `1` | **a rule refused it** | the gate working as intended |
| `2` | the command line could not be understood | a typo, or a missing argument |
| `3` | **the gate itself failed** | never a governed stop. Read the message on standard error |

A refusal goes to standard error and a pass to standard output, so a host that forwards only one stream
still shows you why you were stopped.

**One refusal has no recovery, and it is not a bug.** A blocking finding cannot be waived, accepted or
overridden by any mechanism — not at the gate, not with a flag, not with a recorded override. There is no
field for it in an approval entry and no refusal code that acceptance would clear. It is answered, or the
cycle does not advance.

---

## `sdlc new` — the two ways a cycle is refused

**Both refuse before an id is spent**, so nothing is left behind to clean up.

### no declared stack

`.sdlc/techstack.md` is not there. Adoption drafts it and a person places it, so this is where every
solution stands between adopting and renaming the draft, and where a deleted file leaves one. A cycle
opened now would reach its plan with nothing to be read against and no stack rule in force: every gate
green, and the argument the stack file exists to force never made.

**Next:** fill in `.sdlc/techstack.draft.md` — the framework's default, cut down to what is true of this
solution — and rename it into place yourself, then commit it. If the draft is gone, `sdlc draft
techstack` writes it again, or `sdlc draft techstack --empty` writes one that pins nothing. An empty file
opens a cycle; a missing one does not, because one is a decision and the other is nobody having made it.

### uncommitted work

Something in the working tree is not committed, and the refusal names the files. Opening a cycle is
followed by switching to its plan branch, and a switch carries uncommitted changes with it — so they
would ride into the cycle and reach its diff as work no plan declared. The commonest case is the first
cycle, straight after placing the files adoption drafted.

**Next:** decide what each file is — commit it, stash it, or discard it — and open the cycle again. The
intent skill asks you rather than choosing, because work moved out of sight is work you have to find. The
framework's own ignore files under `.sdlc/` never count, and neither does anything git ignores, which
includes the drafts.

## `sdlc gate` — every way a build is refused

`sdlc gate <id>` exits 1 and says which of these it is. In rough order of how early you hit them, and
this is the whole list:

| | |
|---|---|
| no cycle · abandoned · a planning document is missing | nothing to gate |
| not approved · the ledger cannot be read · the standing decision refuses it | the gate was not passed |
| a findings round cannot be read · a document was never interrogated | **the loop cannot be shown to have run** |
| a document changed since it was approved · a blocking finding is open · a findings round is stale | the approval no longer covers what is there |

### no cycle

The cycle has no directory. Nothing has been written yet.

**Next:** `sdlc new`, which allocates the id and creates the directory. If you expected a cycle to be
there, you have the wrong id — `sdlc cycles` lists every cycle this solution holds.

### abandoned

The cycle was stopped, which is terminal.

**Next:** open a new cycle. An abandoned cycle does not reopen, and the record says why it stopped —
which is the point of it being a file a person wrote rather than an exit an agent took.

### a planning document is missing

One of `intent.md`, `spec.md`, `plan.md` is not there. All three are required, always.

**Next:** `sdlc status <id>` names which, and ends with the literal command to produce it.

### not approved

There is no approval ledger. Nobody has been to the gate.

**Next:** `sdlc draft approval <id>`, then rename the draft it writes onto the ledger beside it.
**Asking an agent to rename it will be denied** — see the hook section below. That denial is the
mechanism, not a configuration problem.

### the ledger cannot be read

There is an `approval.md` and it is not valid YAML — almost always a decision placed by hand, or a
half-finished edit.

**Next:** `sdlc draft approval <id>`. It still runs, and the draft it writes is a complete replacement.
Nothing is carried forward from a file that will not parse, because that is a failed placement rather
than a decision somebody took — so read the draft, and if the broken file did hold a decision worth
keeping, recover it from git rather than from the file.

This refuses rather than failing, which is the point: the one file a person writes is the one most
likely to be malformed, so `status` and the drafter both have to survive one. A malformed ledger never
reads as approved.

### the standing decision refuses it

Somebody was at the gate and the decision was `changes-requested`, `denied` or `reopened` rather than an
approval.

**Next:** read the reason in `approval.md`. A refusal is rejected without one, so there is always
something there. Then `/sdlc:refine` or `/sdlc:steer` as the reason calls for, and back to the gate.

### a document changed since it was approved

The approval recorded a hash of each of the three documents, and one of them no longer matches.

**Next:** either put the document back as approved, or approve again — `sdlc draft approval <id>` prints
a fresh entry with the new hashes. Approving again is usually right: the edit happened for a reason, and
the gate's job is to make sure somebody saw it.

This is also what you hit after editing a planning document post-approval to "just fix a typo". There is
no size threshold, deliberately.

### a findings round cannot be read

A round file exists and will not parse.

**Next:** fix the file, or delete that round and interrogate again. What it will not do is skip the
artifact — **"could not tell" must never read as "clean".** That is not hypothetical: overwriting one
round file with `{}` once made every blocker for that document disappear from the gate and from
`verify-chain`, which is a one-command waiver for the one thing that cannot be waived by any mechanism.

### a document was never interrogated

A planning document has no findings round at all — most often because `findings/` was deleted.

**Next:** `/sdlc:interrogate <id> <artifact>` for whichever the message names. An artifact nobody
challenged is not a clean artifact, and without this refusal deleting the directory would leave the gate,
`verify-chain` and `pr-ready` all passing with no round having ever existed. The drafter refuses this too, but that is
advice a person can decline to take; this is the layer that does not negotiate.

### a blocking finding is open

The one with no way out but to answer it.

**Next:** `/sdlc:refine <id> <artifact>`, then a fresh interrogation. `handling-blockers.md` is the
whole walk: the three exits a finding can leave by, what refine may not do, and what to do when the loop
hands back.

Reaching this refusal at all means the blocking finding was raised after the approval entry was printed,
or the entry was written by hand — the printer refuses to produce one while a blocking finding is open. The check lives
in both places because the printer is advice a person can decline to take and the gate is the layer that
does not negotiate.

### a findings round is stale

A round judged its artifact against an upstream document that has since changed — or judged a version
of the artifact itself that has since been edited. The three approval hashes prove the three documents
were approved; they do not prove the spec was derived from *this* intent, or that the plan approved is
the plan a reader cleared. Only the round knows what it read, which is why it records both.

**Next:** re-interrogate the downstream artifact — `/sdlc:interrogate <id> spec`, and the plan after it
if the spec changed. `/sdlc:steer` does this for you, which is the reason to use it rather than editing
documents by hand.

## `sdlc round` — the one way a round is refused

`sdlc round` writes every findings round (D-101). `/sdlc:interrogate` runs it; you meet this when the
order of the chain was skipped.

### something above it is unsettled

A round about a document is only worth recording once every document above it was read as it stands and
nothing there still blocks. The refusal names, for each document above, what is wrong: it has no round,
it changed after its latest round, that round judged it against an upstream that has since moved, or a
blocking finding is still open. A spec judged while its intent is still moving is a clean reading of a
document that is about to stop existing — in one real cycle, seven plan rounds were spent that way.

**Next:** settle the highest document the refusal names — `/sdlc:interrogate <id> <artifact>`, and
`/sdlc:refine` until its round is clean — then come back down, one document at a time.

## `sdlc scaffold` — the two ways it is refused

`sdlc scaffold <id>` writes the solution skeleton on a solution that held no code, and on one adopted
into existing code only the build settings files its plan declares.

### the gate refuses the cycle

It asks the gate the same question the build stage asks, so the message is the gate's own and the
remedy is whichever one above matches it. Nothing is written for a cycle that is not approved.

### an adopted solution whose plan declares no build settings

The solution's origin is `adopted`, so it already has a shape and the skeleton would lay a second one
over it. What scaffold can write for it is the build settings, and only the ones the approved plan
names in its `Files` section. This plan named none.

**It is not refused for the row being `false`, and a row being `true` is not enough.** The plan is what
a person approved for this cycle. A file that makes warnings errors does not arrive on the strength of
a row, in a cycle about something else.

**Next:** `sdlc stack` shows which files the declared stack takes and which are missing. If this cycle
is meant to bring one, the plan declares it — `sdlc draft approval <id> --reopen`, add the file, and
approve again. If it is not, there is nothing for scaffold to do here: build without it.

---

## `sdlc init` — the two ways adoption is refused

### an option it does not take

`sdlc init` takes `--platform`, `--integration-branch` and `--solution`, and nothing else. Any other
option is refused before anything is written, with exit code `2`, rather than ignored — adoption writes
the hook configuration and the pipeline, so a run that silently dropped part of the line would leave you
believing you had asked for something you had not.

**Next:** run it without the option. `sdlc help`, under *Options for init*, lists what it takes. Which
stack the solution is built from is not adoption's: fill in the draft it writes.

### an integration branch the record disagrees with

`--integration-branch` names a branch other than the one `solution.md` records. That file is a person's,
so adoption cannot change the key, and a run that quietly kept the recorded branch would leave you
believing you had moved it.

**Next:** to move it, `sdlc draft solution`, change `integration_branch` in the draft, rename it into
place, and create the branch. Otherwise run adoption without the option.

---

## `sdlc evidence` — every way a recording is refused

`sdlc evidence <id>` writes what the build produced. It refuses rather than writing something partial,
because verify reads what it writes and nothing else, so a half-recorded run is a verdict nobody can
check.

### no cycle, or no plan

There is no directory for the id, or the cycle has no `plan.md`. The recording compares what changed
against what the plan declared, so without a plan there is nothing to compare against.

### a verdict already stands

`verify.md` exists, and it adjudicates the run recorded now. Recording a new run would leave every claim
in that document citing evidence that is gone — and a stale verdict is the one a reader trusts, because
it looks finished.

**Next:** delete `verify.md`, record the new run, and verify again. The findings rounds under `findings/`
are kept either way, so the history of what was challenged survives.

### git cannot say where the branch diverged

The diff is measured from where the branch checked out diverged from the integration branch, or from the
default branch when there is none. Without that commit there is nothing to measure. The commonest cause is
a branch with no history in common with either — a fresh clone that never fetched them, or work started
from somewhere unrelated.

**Next:** `git fetch origin`, then ask again. If it still refuses, put the work on the branch
`sdlc branch <id> build` prints, which starts from the right base.

A misspelt branch costs more than this refusal: the turn-end hook reads the cycle out of the current branch,
and a branch it cannot parse is not governed work, so it allows every turn in the meantime. Nothing was
being checked while the building happened.

### an empty patch beside a changed file

Files outside `.sdlc/` changed and the patch came back empty, which cannot both be true — recording it
would put an empty `diff.patch` beside a panel saying everything was in order. If you see it, the
evidence is wrong rather than the code. **Next:** `git status --short`.

### no results file

The `--tests` path does not exist, or the directory holds no `.trx`, or the file holds no results. A run
that produced no results is a run that did not happen, which is not the same as a run with nothing to
report.

### one test name with two outcomes

Two projects reported the same test name, one passing and one failing. The chain check matches a plan's
named test on the end of what the runner reported, so the passing result would answer for the failing
one. **Next:** rename one of them, or run the projects separately.

---

## `sdlc pr-ready` — the four ways a pull request is refused

### not verified

There is no cycle, or no `verify.md`.

**Next:** `/sdlc:verify <id>`, which needs recorded evidence — so `sdlc evidence <id>` before it if the
build has not recorded any.

### the verification did not pass

The verdict is `blocked`. Not "no failures recorded" — an actual pass is required, because the verdict is
the only thing standing between unattended building and work presented as ready for review.

**Next:** `/sdlc:remediate <id>`. A batch run with `--pr` opens a **draft** pull request for it instead
(D-85): marked not ready, its check red, so the attempt is somewhere a person looks rather than built again
the next night. Remedy it on that branch and mark the draft ready once it passes.

### escalated

Remediation stopped getting anywhere with blockers still open: either a pass undid what an earlier one
answered, or two passes in a row reduced nothing. **An escalated cycle never opens a ready pull
request** — a batch run opens a draft, which the check refuses to merge — and that stopping is the only
reason an unattended batch is safe to run at all. There is no pass count to run
out of — the refusal says which of the two stopped it, and they want different things from you: a
regression means looking at what the last pass actually did, and a stall means asking whether the finding
is answerable. `handling-blockers.md` has what to do with an escalated cycle.

**Next:** a person decides. Either the criterion is wrong, in which case
`sdlc draft approval <id> --reopen` withdraws the approval with a recorded reason and the documents can be
rewritten, or the work is harder than the plan thought and the cycle is abandoned with
`sdlc draft abandon <id>`.

**There is no budget to raise.** The bound is progress rather than a pass count (D-66), and it is what
everything unattended rests on.

### the chain does not hold

The pull-request check would refuse it anyway, so pushing would cost a round trip through CI to learn what
you were just told.

**Next:** `sdlc verify-chain <id>`, which prints every check — passing and failing both, because a reader
looking at a failed check needs to know what held. The checks are: the cycle exists, no shape problems,
the chain is complete, the approval, the gate is satisfied, every criterion has a proof, every named test
passed, every criterion is adjudicated, the verify was judged against this evidence, the changed lines are
covered, the codebase is no less covered than it was, the verdict follows the approval that stands (only
after a reopen), the change was verified.

**The two coverage checks are arithmetic** (D-103). At least 70% of the lines the change adds ran under a
test, and the whole codebase's rate is no lower than the baseline recorded at the base commit in
`.sdlc/coverage.json`. The first catches a change that arrived untested; the second catches a deleted or
weakened test uncovering code nobody changed. **Next:** add the tests that run the uncovered lines, build,
and record again with `--coverage`. Code no test should run is excluded in the solution's coverage
settings, declared in the plan with its reason.

**The two about evidence and reopening are the stale-verdict checks.** Evidence is recorded again after
each remediation, and a verify round records which run it read, so a verdict judged on the run before
last fails. And a reopen leaves the old evidence and verdict in place while the documents change: once
the cycle is approved again, a verify round older than that approval adjudicated documents nobody
approves any more. Delete `verify.md`, build, record the evidence, and verify again.

**"every named test passed" is the one that surprises people.** Each proof in the plan opens with its
test's name in backticks, and this reads those opening names out of the plan and confirms each one ran
and passed according to the normalised summary — not according to anything the build said about itself.
Backticks later in an entry are formatting and are not read.

**"every criterion is adjudicated" is the other one.** Each criterion the spec states needs an entry
under `Criteria` in `verify.md`, numbered to match. A criterion whose proof could not be reached is
adjudicated **not met**; moving it to `## Open` as a limit of the proof is what this refuses, because
that region is for what a document cannot settle and whether a criterion was met is the one thing a
verify is for. A real limit — what the evidence does not reach *beyond* a criterion that was met — still
belongs there.

---

## The pre-tool hook — the eight things an agent is denied

These are denied at the tool call, by file path and by shell command alike. `../mechanisms/protected-files.md` says
how the hook decides and what it deliberately leaves alone; this is why each one is a person's. The rules are absolute rather
than conditional, and that is the point: there is no mode, no flag, and no check on which skill is
running, so a rule with no exception has nowhere for a bypass to live.

| What | Why it is a person's |
|---|---|
| writing `approval.md`, or moving anything onto it | an approval is a person's act, and a recorded one means something because no tool could have recorded it. `sdlc draft approval` writes a draft beside it; renaming that draft is the decision |
| writing `abandoned.md` | an agent stuck on a blocking finding has an obvious escape — abandon and start clean. A hand-written reason keeps the objections that killed it on the record |
| writing `constraints.md` | a cycle judged by the constraints it is changing is the same deadlock as the framework governing itself. `sdlc propose constraints` reads the history and proposes an entry; pasting it is the decision |
| writing `decisions.md` | a stage reads the decision record for alignment, so an agent that could write it could invent doctrine and then align to it. `sdlc propose decisions` reads the approved cycles and proposes an entry; pasting it is the decision |
| writing `solution.md` | the origin is a historical fact that must never change. Per-key protection would be the first conditional rule, which is where a bypass would live. Adoption drafts it; after that `sdlc draft solution` does |
| writing `techstack.md` | it is the second rule source a plan is read against, and `CON_15` judges every plan against it — so an agent able to edit it could satisfy that rule by adding the row it wanted instead of arguing for it, or drop a stack rule by switching its row off. `sdlc draft techstack` writes a draft beside it; renaming the draft is the decision |
| running the adoption command | adoption writes the hook configuration that constrains the agent, the pipeline that gates the merge, and the file saying where this code came from |
| writing a findings round | not a person's file but the binary's: `sdlc round` numbers it, stamps when it ran and what it judged, and refuses it out of the chain's order. A round the session wrote itself carried whatever it typed (D-101). Put the findings in a scratch file and hand it to `sdlc round` |

**If your agent reports one of these as a broken tool, it is reading the mechanism as a malfunction.** The
refusal message names the command to run instead — a drafter for four of these files, `propose` for the
rule file and the decision record. Run it yourself, and place what it produces.

**The last one matches the verb rather than parsing the line**, so even asking the adoption command for
its own help is refused and `sdlc help` is the way to read its options. The same scan is why a refusal
can arrive for a command that only *mentions* a protected file beside a write verb — the hook reads the
shell command, not the paths it would touch, so a commit message reading *Move techstack.md into place*
is denied. `git commit -F` with the message in a file is the way past that, and it is the rule working
rather than a hole.

**The theme is deliberately not on this list.** `.sdlc/theme.json` is a solution file like the ones
above and no agent write to it is denied, because nobody is judged against a colour: no constraint reads
it and no stage takes its brief from it. A cycle changes a value by declaring the file, like any other
declared work.

**Reading these files is allowed.** Refusing `cat approval.md` would make the gate obstructive without
making it safer.

The shell rule judges each piece of a command on its own — the parts between `;`, `&&`, `||`, `|` and
newlines, outside quotes. A redirect is a write of the file it points at, so `grep x decisions.md
2>/dev/null` and `cat techstack.md > /tmp/x` are reads. A write verb — a move, a copy, a delete, an
in-place edit — counts when the same piece names one of these files as a name of its own, so
a draft, or a file whose longer name ends in one of them, is not mistaken for the record. Beyond that it is
deliberately broad rather than a parser: a false refusal costs you a rephrasing, and a miss costs the
guarantee.

**Eight is the whole list, and the limit is structural.** The hook sees a tool call and not which skill
made it, so it cannot deny an edit that is wrong only because of where it came from — `remediate`
touching `spec.md`, or `refine` touching source. Those rules are real and the skills state them, but
they are the skill's own discipline rather than something denied at the call. What catches a breach is
slower and certain: an approved cycle's three documents are hash-locked, so a planning document edited
after approval makes `sdlc gate` refuse the build and `sdlc verify-chain` refuse the merge, each naming
which document moved. **You cannot edit your way past a criterion; you can only turn a cycle that would
have opened a pull request into one that cannot.**

---

## The turn-end hook — the three ways a turn is sent back

Asked before an agent finishes, on a cycle branch only — `cycle/<id>/<kind>`, or an older cycle's bare
`cycle/<id>`. It is what makes a warnings-as-errors
build and an architecture test enforcement rather than something a reviewer finds afterwards: nothing can
report done over a red suite.

**It never runs a build.** It reads what `sdlc evidence` recorded, which every stack normalises to the
same shape — so the gate stays out of the business of knowing what a test platform is.

### the recorded suite has a failure

The evidence for this cycle carries a failing test. Fix the code, run the suite, record it again. **Do not
adjust the test**: that is the inversion the whole design exists to prevent, and it is also the easier
fix, which is why the hook says so rather than trusting anyone to remember.

### there are changes and no recorded evidence

Code was written on this branch and nothing recorded what the build did with it. Verify reads only what
the build records, so a change nothing recorded is a change nothing can judge.

```
sdlc evidence <id> --tests <results file> --build-log <log>
```

### the working tree has moved since the evidence

The recorded diff no longer matches what is on disk, so the evidence describes something other than the
code. Run the build and the suite again over what is there now, and record it.

### when it says nothing

Deliberately most of the time. It is silent off a cycle branch, on a cycle with no directory, on an
abandoned cycle, before the cycle has a plan, when nothing has changed, and when the host is already
continuing because it was sent back once already. A governance hook that met a person finishing a thought
would be switched off within a day, and then the one case it catches would go uncaught too.

**It is bypassable.** Running `sdlc evidence` against a stale results file satisfies it. That is the local
layer behaving as designed — `sdlc verify-chain` in the pipeline re-reads the evidence and checks that
every test the plan named actually passed, and that is the guarantee.

**Two of the three hosts.** Claude Code calls the moment `Stop` and Copilot `agentStop`, and adoption
configures whichever name the host uses. Antigravity has such an event and is deliberately left
ungoverned — see the paragraph below. A host that genuinely has no turn end is still refused
by name — `sdlc hook --event turn-end` says so and configures nothing, because a plausible-looking
configuration for an event that cannot fire is a check that appears to enforce and does not.

**The send-back needs the host to say it is already retrying**, or a refusal is an endless turn rather
than a governed stop. Claude Code and Copilot both carry that flag and both spell it `stop_hook_active`.
**Antigravity does not**, and that is why its turn end is not governed at all:
`sdlc hook --event turn-end --host antigravity` refuses by name rather than installing a check that
could loop. Its tool calls are governed normally.

---

## When nothing refuses and it should have

The dangerous direction. Five ways the gate goes quiet — and **`sdlc doctor` is the command for this
section**: it checks every host's hook configuration, adoption, and whether a pipeline here runs the one
check that is the guarantee, and exits 1 if any of it is wrong. What it cannot see is marked `elsewhere`
rather than passed: whether a host loaded the plugin, which is the first two causes below, and — when `az`
or `gh` is not signed in — whether branch policy requires the check, which is the fifth.

**A hook is not loaded.** `claude plugin details sdlc@ai-sdlc` must say `Hooks (2) PreToolUse, Stop`. **A
count below two is a failed install, not a detail** — and the two fail differently. Without `PreToolUse`
nothing is denied; without `Stop` a turn can end on a red build. Either way every cycle still appears to
work. No binary can check this for you, which is why `doctor` names the command instead of answering it.

**The workspace is not trusted.** Project hooks do not run until it is. Same silence, different cause.

**The session predates the install.** Skills and hooks load at startup. Reinstall, then start a new
session.

**The declared stack says less than it looks like it says.** `CON_15` asks a plan to say why it departs
from a declared default, so a row with a concern and no choice is a rule with nothing to bite on, and a
rule row naming an id this release does not ship switches on nothing while reading as if it did. Every
stage passes, every gate opens, and the only thing missing is the argument. `sdlc doctor` reports both as
`declared stack` and names the row. **An empty file is not this**: a person placing a stack with nothing
in it has decided that nothing is pinned, and `doctor` says so as sound. `sdlc draft techstack` writes
the options menu — what each shipped profile declares for every concern, and every stack rule — beside
the draft to edit.

**The check is not required.** `sdlc verify-chain` in the pipeline is the only non-bypassable layer, and
it is only a guarantee while branch policy makes it required. A pipeline that runs it and a merge that
does not wait for it is a directory that appears enforced and is not — which is worse than one that never
pretended.

**On Azure DevOps it is worse: without the policy the check does not run at all.** Azure Repos ignores a
`pr:` trigger in YAML, so `azure-pipelines.yml` runs on a pull request only when a build validation
policy names it — Repos > Branches > the default branch > Branch policies > Build validation. Until then
every pull request merges with nothing built, tested or verified, and nothing says so. `sdlc doctor`
asks the platform, on both, and reports a problem while the check is not required. Signed out of `az` or
`gh`, it marks the policy `elsewhere` and names this step instead.

**One more, on the generated pipeline:** its first real step installs the `sdlc` binary, pinned to the
release that generated it, and **that install does not resolve yet** because the package is not on a public
feed. So a first pull request fails at `install sdlc` rather than at `verify the artifact chain`. That is
the intended shape — the failure names the missing thing, before a build is spent, in a step whose
comment says so (D-37). Name a feed holding the package in `nuget.config`, or wait for it to be
published.

---

## Where to go next

| | |
|---|---|
| a blocking finding or a blocked verification, and how to answer it | `handling-blockers.md` |
| every refusal as one line | `../reference/refusals.md` |
| a cycle walked end to end | `running-a-cycle.md` |
| the same walk with one solution in it | `../samples/rolling-a-die.md` |
| every command on one screen | `../reference/commands.md` |
| what this solution is built from, and how to change it | `declaring-the-stack.md` |
| what each skill is and who starts it | `../../skills/README.md` |
| installing, updating, removing | `installing-the-plugin.md` |
| the eighteen global constraints, each with what it forbids | `../../CONSTRAINTS.md` |
