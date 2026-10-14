# Handling refusals

Every refusal the framework produces, what it means, and what to do next. `../cheat-sheets/refusals.md`
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

## `sdlc new` — the one way a cycle is refused

### no declared stack

`.sdlc/techstack.md` is not there. Adoption does not write it — a person places it — so this is where
every solution stands between adopting and renaming the first draft, and where a deleted file leaves
one. A cycle opened now would reach its plan with nothing to be read against and no stack rule in
force: every gate green, and the argument the stack file exists to force never made.

**Next:** `sdlc draft techstack`. The draft is the framework's default — cut it down to what is true of
this solution — or run it with `--empty` to pin nothing. Then rename the draft into place yourself. An
empty file opens a cycle; a missing one does not, because one is a decision and the other is nobody
having made it.

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
challenged is not a clean artifact, and deleting the directory used to leave the gate, `verify-chain`
and `pr-ready` all passing with no round having ever existed. The drafter refuses this too, but that is
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

A round judged its artifact against an upstream document that has since changed. The three approval
hashes prove the three documents were approved; they do not prove the spec was derived from *this*
intent. An intent edited after a clean spec leaves a spec derived from an intent that no longer exists,
with every approval hash matching. Only the round knows what it read, which is why it records it.

**Next:** re-interrogate the downstream artifact — `/sdlc:interrogate <id> spec`, and the plan after it
if the spec changed. `/sdlc:steer` does this for you, which is the reason to use it rather than editing
documents by hand.

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

## `sdlc init` — two options that went away

Both are answered with a sentence rather than run, because a line kept from before would otherwise do
something different from what it says.

| Option | What it did | Where that lives now |
|---|---|---|
| `--profile` | chose the stack at adoption | `sdlc draft techstack`, and a rename |
| `--adopt-standard` | wrote the five .NET build settings into an existing codebase and recorded `standard: profile` | the files are rows in the declared stack, brought by a cycle whose plan declares them; the key is `sdlc draft solution --standard profile`, and a rename |

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

The diff is measured from the merge base with the default branch. Without it there is nothing to measure.

**Read the branch name in the message before anything else, because the commonest cause is that you are
not on it.** The branch is derived from the cycle id rather than from where you are standing, so the
message names `cycle/<id>` whether or not such a branch exists — and a cycle whose work went onto a
branch spelled any other way lands here. The remedy is to rename the branch you are on, not to hunt for
what happened to one you never created.

That misspelling costs more than this refusal: the turn-end hook reads the cycle out of the current
branch too, and a branch it cannot parse is not governed work, so it allows every turn in the meantime.
Nothing was being checked while the building happened, and this is the first thing that says so.

### an empty patch beside a changed file

Files outside `.sdlc/` changed and the patch came back empty, which cannot both be true. This was a real
defect once — `git diff` cannot see an untracked file — and it recorded an empty `diff.patch` beside a
panel saying everything was in order. It is fixed; if you see it, the evidence is wrong rather than the
code. **Next:** `git status --short`.

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
the only thing standing between unattended building and unreviewed work reaching a pull request.

**Next:** `/sdlc:remediate <id>`.

### escalated

Remediation stopped getting anywhere with blockers still open: either a pass undid what an earlier one
answered, or two passes in a row reduced nothing. **An escalated cycle never opens a pull request**, and
that stopping is the only reason an unattended batch is safe to run at all. There is no pass count to run
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
passed, every criterion is adjudicated, the change was verified.

**"every named test passed" is the one that surprises people.** Each proof in the plan names its test in
backticks, and this reads those names out of the plan and confirms each one ran and passed according to
the normalised summary — not according to anything the build said about itself.

**"every criterion is adjudicated" is the other one.** Each criterion the spec states needs an entry
under `Criteria` in `verify.md`, numbered to match. A criterion whose proof could not be reached is
adjudicated **not met**; moving it to `## Open` as a limit of the proof is what this refuses, because
that region is for what a document cannot settle and whether a criterion was met is the one thing a
verify is for. A real limit — what the evidence does not reach *beyond* a criterion that was met — still
belongs there.

---

## The pre-tool hook — the seven things an agent is denied

These are denied at the tool call, by file path and by shell command alike. The rules are absolute rather
than conditional, and that is the point: there is no mode, no flag, and no check on which skill is
running, so a rule with no exception has nowhere for a bypass to live.

| What | Why it is a person's |
|---|---|
| writing `approval.md`, or moving anything onto it | an approval is a person's act, and a recorded one means something because no tool could have recorded it. `sdlc draft approval` writes a draft beside it; renaming that draft is the decision |
| writing `abandoned.md` | an agent stuck on a blocking finding has an obvious escape — abandon and start clean. A hand-written reason keeps the objections that killed it on the record |
| writing `constraints.md` | a cycle judged by the constraints it is changing is the same deadlock as the framework governing itself. `sdlc propose constraints` reads the history and proposes an entry; pasting it is the decision |
| writing `decisions.md` | a stage reads the decision record for alignment, so an agent that could write it could invent doctrine and then align to it. `sdlc propose decisions` reads the approved cycles and proposes an entry; pasting it is the decision |
| writing `solution.md` | the origin is a historical fact that must never change. Per-key protection would be the first conditional rule, which is where a bypass would live |
| writing `techstack.md` | it is the second rule source a plan is read against, and `CON_15` judges every plan against it — so an agent able to edit it could satisfy that rule by adding the row it wanted instead of arguing for it, or drop a stack rule by switching its row off. `sdlc draft techstack` writes a draft beside it; renaming the draft is the decision |
| running the adoption command | adoption writes the hook configuration that constrains the agent, the pipeline that gates the merge, and the file saying where this code came from |

**If your agent reports one of these as a broken tool, it is reading the mechanism as a malfunction.** The
refusal message names the command to run instead — a drafter for four of these files, `propose` for the
rule file and the decision record. Run it yourself, and place what it produces.

**The last one matches the verb rather than parsing the line**, so even asking the adoption command for
its own help is refused and `sdlc help` is the way to read its options. The same scan is why a refusal
can arrive for a command that only *mentions* a protected file — the hook reads the shell command, not
the paths it would touch, so a commit message naming one is denied. `git commit -F` with the message in
a file is the way past that, and it is the rule working rather than a hole.

**The theme is deliberately not on this list.** `.sdlc/theme.json` is a solution file like the ones
above and no agent write to it is denied, because nobody is judged against a colour: no constraint reads
it and no stage takes its brief from it. A cycle changes a value by declaring the file, like any other
declared work.

**Reading these files is allowed.** Refusing `cat approval.md` would make the gate obstructive without
making it safer.

The shell rule is deliberately broad — it looks for the file names near a write indicator rather than
parsing a command line. A false refusal on a command that merely mentions one of these files costs you a
rephrasing; a miss costs the guarantee.

**Seven is the whole list, and the limit is structural.** The hook sees a tool call and not which skill
made it, so it cannot deny an edit that is wrong only because of where it came from — `remediate`
touching `spec.md`, or `refine` touching source. Those rules are real and the skills state them, but
they are the skill's own discipline rather than something denied at the call. What catches a breach is
slower and certain: an approved cycle's three documents are hash-locked, so a planning document edited
after approval makes `sdlc gate` refuse the build and `sdlc verify-chain` refuse the merge, each naming
which document moved. **You cannot edit your way past a criterion; you can only turn a cycle that would
have opened a pull request into one that cannot.**

---

## The turn-end hook — the three ways a turn is sent back

Asked before an agent finishes, on a `cycle/<id>` branch only. It is what makes a warnings-as-errors
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
ungoverned — see the paragraph below. Copilot was left out of this for a
release on the belief that its hook surface had no such event; it has had one all along, and
`CONCEPT.md` D-46 records how the claim survived. A host that genuinely has no turn end is still refused
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
check that is the guarantee, and exits 1 if any of it is wrong. The two causes it cannot see are marked
`elsewhere` rather than passed, and they are the first two below.

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
pretended. Iteration 3 shipped a nightly job that needed an API key nobody had set, and warned and exited
cleanly for weeks. That is the failure mode this design is written against.

**One more, on the generated pipeline:** its first real step installs the gate, pinned to the release
that generated it, and **that install does not resolve yet** because the package is not on a public feed.
So a first pull request fails at `install the gate` rather than at `verify the artifact chain`. That is
the intended shape — the failure names the missing thing, before a build is spent, in a step whose
comment says so (D-37). Name a feed holding the package in `nuget.config`, or wait for it to be
published.

---

## Where to go next

| | |
|---|---|
| a blocking finding or a blocked verification, and how to answer it | `handling-blockers.md` |
| every refusal as one line | `../cheat-sheets/refusals.md` |
| a cycle walked end to end | `running-a-cycle.md` |
| the same walk with one solution in it | `../samples/rolling-a-die.md` |
| every command for one cycle, on one screen | `../cheat-sheets/commands.md` |
| what this solution is built from, and how to change it | `declaring-the-stack.md` |
| what each skill is and who starts it | `../../skills/README.md` |
| installing, updating, removing | `installing-the-plugin.md` |
| the seventeen global constraints, each with what it forbids | `../../CONSTRAINTS.md` |
