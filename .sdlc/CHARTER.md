---
solution: ai-sdlc
artifact_root: .sdlc/
status: approved
version: 9
updated: 2026-09-21
owner: peter
---

# ai-sdlc — charter

## What this is

A Claude Code plugin marketplace that turns a rough idea into a shipped, verified change by
walking it through a chain of committed artifacts: `intent.md` → `spec.md` → `plan.md` → code
and tests → `verification.md`. Each stage reads the previous artifact and commits its own, so
the chain is the state machine and a fresh session can pick it up anywhere.

It ships as two layers. `sdlc-core` is the engine and knows only about process: tiers, gates,
requirements, slice manifests, verification. `stack-*` packs each hold one opinionated answer
to "what do we build this with", and the engine consumes whichever is installed.

It is a library of process, not a product and not an application. Nothing in it runs in
production, and it has no users other than the people invoking it from a terminal.

## Who it is for

The owner today, a small team shortly. **Written for the team, starting now** — meaning
artifacts and skill bodies must be followable by someone who was not present when they were
written, and conventions get stated rather than assumed. That is not politeness: the entire
design rests on a fresh session with no memory continuing from the artifact alone, and a new
teammate is exactly that fresh session.

The job they hire it for: *stop re-deciding the same things, and stop shipping changes nobody
verified.*

## Architectural invariants

Properties that must hold across every change. A change that breaks one is wrong regardless of
what it was trying to do. Each is written so a reviewer can catch a violation.

**I1 — The engine imposes no stack.** No language, framework, cloud, vendor, or product name
appears in `plugins/sdlc-core/skills/**`, in `plugins/sdlc-core/agents/**`, or in any
executable the engine plugin ships, as a requirement, default, or recommendation **for the
repo being built**. Stack knowledge lives in a `stack-*` pack. One carve-out: fenced examples,
where an illustrative path demonstrates path matching and imposes nothing. That carve-out
covers the instruction files and nothing else — a script has no illustrative prose, so every
line of one counts.

*Checkable: two tests over two file sets, reading one term list —
`CharterInvariantI1_EngineImposesNoStack` over the engine's instruction files, and
`EngineExecutablesImposeNoStack` over the executables it ships. Mechanical, with the term list
written down once and shared by both, so the check is reproducible rather than a matter of
which words a reviewer happened to try, and so neither instrument can quietly stop covering
what it claims to.*

**Widened in v4 from those two directories to the shipped executables as well.** The earlier
scope was justified by *"engine tooling lives outside `skills/` and `agents/`"* — true then,
and it rested on engine tooling not executing pack-supplied data. The scaffold executor does:
it reads a pack's manifest and performs the actions declared in it. That makes it the engine
file with the strongest pull towards naming a directory, a file extension, a package or a
generator command, and it was the one file outside the only instrument. The rationale did not
become wrong; the thing it described changed.

This invariant has been violated twice, both times by something that read as harmless: the
disjointness check was an inline Python script, and the interrogator used a named cache product
as an anti-example. Neither imposed anything on anyone. Both named a stack in normative prose,
and the value of a mechanical check is that it does not accept that excuse.

**I2 — Every stage that produces a judgement commits it.** A judgement that matters later and
lives only in a chat message is lost, so Plan, Design, Build-planning and Test each end in a
`git commit` of their artifact. **This binds the failure path too:** `verification.md` is
committed on DO NOT SHIP as well as on SHIP. A stage that records its successes and discards
its failures is measuring the wrong thing, and the failed verdict is the one a retrospective
actually needs.

Build is the deliberate exception: its output is the working tree, committed by the human
alongside the change. It produces code, not a judgement.
*Checkable: every gate section in every chain skill ends in a `git commit`, on every branch.*

**I3 — Nothing downstream is produced from a draft.** A spec is not written from an unapproved
intent, a plan is not written from an unapproved spec, code is not written from an unapproved
plan. *Checkable: every path that consumes an artifact reads `status:` **from the file** and
stops.*

"From the file" is the load-bearing half. `parallel-build.js` originally took its slices from
its arguments and never opened `plan.md` — it re-proved disjointness against a payload it
explicitly distrusted, then trusted that same payload for the approval gate. It now reads the
plan and refuses to build a draft, or a manifest that does not match the approved one.

**I4 — A gate is crossed only by explicit human words.** Not by the agent's own judgement that
the artifact looks fine, and not by silence. *Checkable: every gate section names the literal
approval question it asks and the `status:` transition that approval unlocks — which is on the
page, unlike "waits", which is a claim about runtime that no reviewer can inspect.*

**I5 — A pack's own skill is the only list of the actions it performs.** The engine never
names a pack's files, directories, commands, tokens, or assets. Adding a pack must require no
change to the engine. *Checkable: the engine names no filename, path or command it did not
itself create.* Phrased as a property rather than a grep, because grepping for "any pack's
filenames" only works for packs that already exist, and the case that matters is the pack
nobody has written yet.

**Widened from "files" to "actions" in v3.** A pack that scaffolds a repository declares
directories to create and commands to run, and the original wording left both outside the
invariant — so the engine could have grown a `mkdir src` or a generator invocation without
technically breaking it. Anything a pack causes to happen is the pack's to declare.

**This makes the engine an executor of pack-supplied command strings, which is worth naming
rather than leaving implicit.** I1 leaves no alternative: the engine cannot say `dotnet`, so it
cannot hold the commands. The trust boundary is therefore the pack itself — installed
deliberately, from this marketplace, and reviewed like any other code here. A pack is not
untrusted input and must never be treated as a place to put something a reviewer would not
read.

The discovery mechanism is undeclared elsewhere and belongs here: `init` finds packs by taking
every skill whose name begins with `stack-`. A pack named anything else is silently never
proposed.

**I6 — Slice manifests are pairwise disjoint.** Two implementers holding one file is a merge
conflict by construction. Overlap is a planning bug, fixed in `plan.md` and re-approved —
never reconciled during or after the build. *Checkable: the disjointness script is run and its
real output pasted.*

**Disjointness has never meant "within one plan", and saying so in v9 is instrumentation
rather than a widening.** The words above do not limit the comparison to a single plan, so two
*live* plans claiming one file has always been a violation — with nothing watching for it.
Adding that watch adds instruments, which ride the gate of the change that adds them; it does
not move a constraint. Recorded because the distinction decides whether the next change needs
its own approval conversation, and because reading it the other way was a live option.

**A live plan is one whose work is agreed and not yet finished or replaced.** Finished is a
field of its own beside `status:` — see the frozen shapes below — so a finished plan claims no
files and a superseded one claims none either. A parked chain's plan is live: parked describes
the chain, not the plan.

**I7 — Roles that must not be persuaded run in fresh context.** The interrogator and the
verifier never participate in the conversation that produced what they are checking, and are
never handed a summary of it. A reviewer who heard the author reason through a failure has
already been argued with.

*Checkable, both clauses: dispatched as a subagent rather than a teammate, **and** the dispatch
prompt carries only artifact paths — no summary, no rationale, no note about which parts are
probably fine.* The second clause is the one that decays: it is easy to dispatch correctly and
then hand over the reasoning in the prompt, which reinstates exactly the contamination the
fresh context was for.

**I8 — Hooks are advisory by default.** Nothing hard-blocks an edit unless the owner opts in by
setting `SDLC_GATE=block`. A tool that blocks by default gets uninstalled, and an uninstalled
tool enforces nothing. *Checkable: the default path exits 0 — pinned in `GateHookTests`.*

Note the opt-in is an **environment variable, not a per-session flag**: set persistently, it
enables hard-blocking in every repo until it is unset. That is a sharper instrument than it
looks.

**Sharper again from v9, and this is what it now means.** Once a plan can be finished, the
ordinary state *between* chains is that no plan is live — and the gate's no-plan-approved
branch fires on every code edit regardless of path, so with the switch set, every code edit is
refused until a chain is started. That is the gate working as designed: with nothing agreed,
you are editing outside any agreed plan. It has been masked since the hook was written by six
plans that carried `approved` permanently because nothing could retire them. Recorded rather
than discovered, because the answer for an owner who hits it is "unset it, or start a chain",
and that is easier to accept when it was written down first.

**I9 — Every known silent-failure class has a named check in CI.** "Anything that can fail
silently" was unfalsifiable: it named no set, so no reviewer could ever declare it violated.
The set is enumerated instead, and grows whenever a new class is found.

**A row is added by the change that adds the check, and that is not an amendment in the sense
the charter schema forbids** — the schema draws the line and this is the pointer to it, not a
second copy of the rule. The short of it: a row records an instrument, where an invariant or a
non-goal records a constraint an intent was approved against, and only the second kind can move
underneath work already approved. The obligation that follows sits on the change: **its plan
declares this file in its slice manifest**, which is the step versions 4 through 7 each skipped.

| Silent failure | Check |
|---|---|
| Frontmatter malformed, so the file is never loaded | `EveryDefinition_HasFrontmatterAsItsFirstLine` |
| Skill or agent name disagrees with its path | `EverySkillName_MatchesItsDirectory`, `EveryAgentName_MatchesItsFilename` |
| Skill body has an unclosed fence or a mangled command | `EveryBody_HasBalancedCodeFences`, `NoBody_ContainsALiteralBackslashN` |
| Workflow `phase()` disagrees with `meta.phases` | `EveryWorkflow_IsValid` |
| Workflow body does not parse | `EveryWorkflow_IsValid` |
| Plugin version is not semver, so an update is a no-op | `EveryPluginVersion_IsSemver` |
| Marketplace points at a directory that moved | `EveryListedPlugin_HasADirectoryThatExists` |
| Hook command points at a missing file | `EveryHookCommand_PointsAtAFileThatExists` |
| Gate stops matching, or goes quiet on a non-default root | `GateHookTests` |
| A manifest overlap slips through | `SliceManifestTests` |
| Shipped PowerShell does not parse | `EveryShippedPowerShellScript_Parses` |
| A stack leaks into the engine | `CharterInvariantI1_EngineImposesNoStack` |
| A stack leaks into a shipped engine **executable** | `EngineExecutablesImposeNoStack` |
| Python returns to the repo | `NoPythonShipsInThisRepository` |
| An engine skill drops its pointer to the plain-language rule | `EveryEngineSkill_CitesThePlainLanguageRule` |
| Scaffold produces an unbuildable solution | `ScaffoldEmitsBuildableSolutionTests` — one real build per solution kind |
| Generator output breaks central package management | `ScaffoldEmitsBuildableSolutionTests` — restore with transitive pinning on |
| Emitted names stop matching the coverage patterns | `ScaffoldEmitsBuildableSolutionTests` — each emitted assembly name against `coverage.runsettings` |
| A re-run overwrites, or a rewrite re-fires | `ScaffoldEmitsBuildableSolutionTests` — hash the tree, re-execute, compare |
| A token survives, including from an earlier run | `ScaffoldManifestTests` — the sweep of every file under the target, and of chain files changed since the run began |
| The executor scaffolds from a draft charter | `ScaffoldManifestTests` — draft charter refuses, and leaves an empty tree |
| A prerequisite is absent and the check skips rather than fails | `ScaffoldEmitsBuildableSolutionTests` — an absent prerequisite fails the run |
| The pack's skill and its manifest list different files | `PackSkillCitesItsManifest` |
| A pack promises a layout its manifest does not scaffold | `PackPromiseTests` — the pack's charter line and its `sln add` actions, per kind, both directions |
| CI stops providing a prerequisite a pack's manifest declares | `ScaffoldPrerequisiteTests` |
| A pack file is written by something other than the executor | `EveryPackAsset_IsDeclaredByItsManifest` — the manifest and the pack's assets name the same set, both directions |
| An engine skill or agent names a file a pack scaffolds | `EngineInstructions_NameNoPackFile` |
| The verification record's schema and the agent that writes it drift apart | `SchemaAndAgentTemplate_DescribeTheSameRecord`, `SchemaAndAgentTemplate_AgreeOnOrder` |
| A verification record stops opening with a summary or closing with the owner's options | `TheRecordOpensWithASummaryAndClosesWithOptions` |
| The Options section survives as a heading with no instruction behind it | `TheAgent_IsToldToWriteOptionsRatherThanLeaveThemInferred` |
| A new check reaches the suite and never reaches this register | `SilentFailureRegisterTests` — the register and the suite named against each other, both directions |

Every row is mutation-tested: the fault is injected and the named check must fail. A check that
has never been seen to fail is a check nobody should trust.

**Known and unchecked**, recorded rather than hidden: a plugin version that is valid semver but
was never *bumped* — the format is checked, the increment is not, so an update can still be a
silent no-op; and the eval suite, which has never run.

**What this list is for, stated in v9 because the invariant above has no carve-out and the list
is about to grow.** A class belongs here when its instrument is a *different change* — when
catching it means rebuilding something this change only touches in passing. It is not a place
to put a class because writing the check is tedious. **A list that grows faster than the
register is itself a finding**, and the review that notices it is the amendment conversation:
this one takes the list from two entries to seven in a single change, which is worth saying out
loud rather than letting accumulate quietly.

**I10 — A default root may be named, but never used *instead of* resolving.** Prose saying "it
defaults to `.sdlc/`" is correct and useful. Code that reads `.sdlc/` without first looking for
a declared root is the violation.

The distinction matters because the naive grep returns a dozen legitimate hits, and a check
that cries wolf twelve times is the check nobody runs. *Checkable: for each hit, does control
flow depend on the literal, or is it describing a default? The hook names the default and then
resolves — correct. `parallel-build.js` takes the root as an argument with a default and never
reads the charter — acceptable only because its caller resolves and passes it, which that file
states out loud.*

**I11 — No skill silently upgrades a request into a heavier chain.** Ceremony that costs more
than it saves gets abandoned, and then nothing is used at all — so a skill that tiers a request
states the tier out loud, and one that is ambiguous between two tiers asks, naming the cost
difference.

*Checkable: every skill that performs tiering says the tier before acting.* It binds `intent`
and `plan`, which tier; it does not bind `spec`, `verify` or `init`, which are entered at a
tier already decided. The earlier phrasing implied all five and was false for three.

**I12 — Nothing is committed on a red build or a failing test.** The build is green and both
suites pass — unit *and* integration — and the real output is seen before the commit. Not a
summary of the output, and not an assurance: the output. This binds every tier, `trivial`
included; a one-line change that breaks the build is still a broken build, and checking costs
one command.

*Checkable: a plan's Validation section names the commands and what they must show, and the
run appears in the transcript or the commit before the commit exists.*

The three ways this fails all look like success, which is why it is an invariant and not a
habit: reporting a pass without running anything; running the unit suite only, while the
composed system is broken; and reading the exit code rather than the output, so a suite that
collected zero tests reports green. A red build inherited from someone else is stated before
work starts, never folded into your own verification.

The **commands** live in the stack pack, never here and never in the engine — I1 would forbid
naming them. The engine states that they must be run; the pack states what they are.

## Permanent non-goals

Not "not yet" — never. An intent landing here is blocked outright, not negotiated.

**N1 — It never holds one product's or one business's rules.** What a system does, who its
users are, its business logic and its data model belong in that repo's own charter, never here.

**The line is between a defect class and a domain.** The verifier knows about lookahead,
train/test contamination, survivorship and restatement; `init` probes for invariants around
ordered data, money and multi-tenancy. That is not domain knowledge — it is a catalogue of bugs
that look correct and pass every test, and it applies to any repo touching ordered data. The
test: **would this help someone in an industry nobody here works in?** Lookahead bias would.
"An order settles T+2" would not, and is a violation.

N1 as first written forbade both, which the interrogator caught by pointing at twelve lines of
the verifier that had already landed in the non-goal.

**N2 — It never encodes a specific employer's conventions.** No company names, internal tool
names, or org-specific process. The library stays portable across jobs and shareable with a
team that is not the owner's current one.

**It binds `plugins/**` — what this repository ships to other people — and not the chain under
the artifact root.** The non-goal exists so the library survives a change of employer; a chain
document is this repository's own working record and never leaves with the plugin. Four chain
documents name real external repositories, and one of them is *blocked on* a named repository,
so stripping the name would delete the only thing that makes the blocker actionable. Naming a
repository you depend on is a fact. Encoding how a particular company likes its work done is
the thing forbidden here, and that distinction is the test to apply.

**Not yet checked.** The first invariant has two instruments scanning for stack names; this
non-goal has none, so it holds by review alone. Recorded rather than hidden, and the same shape
as the gaps under **Known and unchecked** in the invariants above.

**N3 — It never becomes a CI/CD or project-management system.** It guides an agent through one
change. It does not track tickets, own a board, or report status. The artifacts are files in
git, and that is the whole storage layer.

`parallel-build.js` computes dependency waves, fans out workers and halts on failure, which is
pipeline-shaped, so the boundary has to be precise: **it never acquires a trigger it owns,
never runs outside a session a human started, and never persists state anywhere but git.** A
scheduler, a webhook listener or a queue lands in this non-goal, and cannot cite the workflow
script as precedent.

### Considered and deliberately not adopted

"Never runs unattended — every gate needs a human" was offered as a non-goal and **not** taken.
Autonomy stays an open design question rather than a closed door.

**Where the line sits today**, because a door left open with no recorded position is the exact
quiet drift this paragraph exists to prevent: every *gate* needs explicit human words, and the
one unattended stretch is between an approved `plan.md` and the verification after it —
`parallel-build.js` writes code across N waves with nobody in the loop. That stretch is bounded
by a human gate on both sides, which is what makes it acceptable. Widening it is an amendment.

## Technology commitments

**This repo is not built with a stack pack**, and does not become one by having tests. There
is no Aspire, no EF Core, no Azure: it is markdown instructions, PowerShell that ships, one Node
tool, and one MSTest project. Counts are deliberately not given here — a hand-maintained tally
in a long-lived artifact is wrong by the next commit.

| Concern | Commitment | Reason |
|---|---|---|
| Skills, agents, schemas | Markdown with YAML frontmatter | What the harness loads. |
| Shipped executables | Windows PowerShell 5.1 | The hook runs on every edit in every repo and the manifest check runs at every fan-out. Present on every Windows machine with no install step, which beats 100ms: measured at 149ms against 67 for node and 51 for python. |
| Tests | **MSTest**, one project under `tests/` | The same framework as every solution this framework builds. Tests drive the real shipped artefacts as external processes — a reimplementation in C# would test the reimplementation, and the two would drift silently. |
| Repo tooling | Node, only where it earns it | `check-workflow.mjs` — about 80 lines — evaluates a workflow body the way the runtime does, which needs a JS parser. It is invoked from a test, so `dotnet test` stays the single entry point. A 220-line Node checker was ported to MSTest once four of its five check groups turned out to need no JavaScript at all. |
| Runtime preference | C# → PowerShell → Node → bash/python | For *new* code, in that order, each with the standing exception that a runtime already justified above keeps its job. Python is last and currently absent; putting it back needs a reason written in the file. |
| Platform | **Windows-first, and only Windows is supported** | The hook and the manifest check both invoke `powershell.exe`, which does not exist on macOS or Linux. A non-Windows teammate cannot run them today. This is a real limit on N2's portability claim and is recorded rather than glossed. |
| CI | GitHub Actions, `windows-latest` | One `dotnet test`. Windows specifically, because testing on `pwsh` would test something other than what ships. |
| Versioning | semver, bumped every release | Plugins are cached per version; skipping the bump makes an update a silent no-op. Not yet checked — see I9. |

**On `parallel-build.js`:** it is Node, it lives inside the plugin, and it **ships to every
user**. An earlier draft of this table claimed Node was dev-only, which was simply false. A
second workflow would be Node too, and that is covered by the runtime preference above.

## What is frozen, and what can still move

The charter commits to shapes that other repositories will persist and that three separate
parsers read. Once a second repo has a committed `.sdlc/` tree, changing these is a migration,
not an edit — so it is worth saying now which is which.

**Frozen.** The five artifact frontmatter shapes in `artifact-schemas.md`; the
`slices` / `files` / `depends_on` manifest; `R<n>` requirement numbering, which `spec` already
calls an API ("never renumber, append and mark superseded") and which downstream repos cite
from tests; and — **added in v3** — **the directory layout a pack emits when it scaffolds a
repository**: the top-level split it creates, and the project-name patterns that other config
depends on matching.

Layout is frozen because, once repositories exist that were built from it, four separate
things cite it: `spec-writer` reads the structure to ground its design, slice manifests name
paths inside it, the coverage configuration excludes assemblies by name pattern, and the gate
hook matches edited paths against manifests. Changing it afterwards is a migration across every
downstream repository, performed by whoever notices, and a coverage pattern that silently stops
matching produces a number that lies rather than an error.

**Still movable.** Skill prose, agent method sections, the tier table, and the *contents* of
`stack-*` packs — asset file contents, package versions, the prose of a pack's skill. A pack
revision does not invalidate an existing repo, because `init` never overwrites a file it did
not create.

**That reasoning covers contents and breaks for layout**, which is why v3 separates them. "We
never overwrite" protects a repo from having its files changed; it does nothing about a repo
whose structure no longer matches what the current pack assumes. The first is a promise about
writes, the second is a promise about shape, and only the first was ever made.

**The manifest is parsed in three places**, in `Assert-SliceManifest.ps1`, the gate hook, and
`parallel-build.js`. **From v9 the first two share one extracted parser**; the JavaScript one
stands alone, because it cannot source PowerShell. They have diverged before — two normalised a
leading `./` and the third did not, so a plan could pass the check that fans out and fail the
check that merely warns — and by v9 they had diverged in five ways, on anchoring, on leading
dots, on case, on where a slice begins, and on the edited path against the claimed one.

**They are kept in step by hand.** An earlier version of this paragraph said "by hand and by
test"; no test has ever compared them, and a reader of that sentence believed a safety net
existed. The trigger stands: **if a fourth consumer appears, that is the moment to stop and
give them one parser** — and the JavaScript still being outside is why it stands rather than
having been retired by the extraction.

**The manifest's grammar is narrower than YAML, deliberately, and v9 writes it down.** A
`files:` entry is a **quoted string on the `files:` line**, and it is a **file path — never a
directory, never a glob**. The readers only ever accepted that, so this legalises nothing new;
what it changes is that a plan written any other way now fails loudly instead of claiming
nothing in silence. The same goes for `status:`, which must be **unquoted and exactly one of
the declared values**. Each of the three is checked. A line-oriented parse was chosen so the
engine ships with no YAML dependency, and that choice is what makes the grammar narrow.

## Open questions

- **The default-stack mechanism.** The owner wants the framework to prefer a stack and change
  only when asked. Agreed direction: the default is declared in the owner's global `CLAUDE.md`
  so the engine stays clean and I1 holds. **Not implemented** — `init` today branches on how
  many packs are installed and asks when there are several. It is stated here and nowhere else,
  because an earlier draft asserted it as a binding commitment *and* listed it as unimplemented
  in the same document, and a commitment is what every future spec is held to. It becomes a
  commitment when it exists. *Owner: peter — needs an intent.*
- **Agent team topology — blocked on behavioural measurement.** Orchestrator, two BAs,
  delivery, QA was proposed. Open: what the second BA does that the first does not, and whether the orchestrator
  earns its relay hop when the main loop already orchestrates. Both are measurement questions,
  and the only instrument is the eval suite, which cannot run — so this is downstream of the
  blocker above and should not be picked up before it clears. I7 already settles the part that
  does not need measuring: QA is isolated by context, not by headcount. *Owner: peter.*
- **Discovery entry points.** The chain assumes a change to an existing system. A bug intent is
  a repro; a competition objective is given by its metric. Likely several entry points feeding
  the same downstream artifacts, rather than one generalised `intent`. *Owner: peter.*
- **Self-improvement loop — running, but the trigger is manual.** The loop is *a defect found
  becomes a permanent named check*, and it ran six times on 2026-09-17 alone: a shipped broken
  command became the skill-body checks, a gate that opened permanently became manifest matching
  plus fifteen tests, three manifest parsers disagreeing became a normalisation fix and a test,
  two stack leaks became `CharterInvariantI1_EngineImposesNoStack`, a lingering shim became
  `NoPythonShipsInThisRepository`, and an owner observation became I12. I9's table is the
  register of it, and it has numbers: 49 tests, 12 injected faults, 12 caught.

  **What is missing is the trigger, not the mechanism.** Every one of those conversions
  happened because somebody noticed. The loop closes when it becomes a rule — a blocking
  finding from the verifier or the interrogator becomes a named check before the change ships,
  and `verification.md` is what a retrospective reads to find the ones that were missed. That
  is unbuilt work, not blocked work. *Owner: peter — needs an intent.*

- **Behavioural measurement — BLOCKED, externally.** Separate question, and the one the eval
  suite actually answers: does the plugin change what a *model* does, against a no-plugin
  baseline? That is the Δ, and it is about agent behaviour rather than repository correctness,
  so no amount of testing here substitutes for it. Retested 2026-09-18 on Claude Code 2.1.268:
  `plugin eval` refuses with *"currently in early access"*, and `winget` reports no newer
  version, so it is not a bump away. **Trigger: 2.1.269+ AND early access on the account.**
  *Owner: peter, not actionable by the owner.*

## Amendment log

| Version | Date | Change |
|---|---|---|
| 9 | 2026-09-21 | **A plan can say its work is finished, and four silent-skip holes in the frontmatter grammar are closed.** Required before `no-two-plans-claim-one-file` could be specified: comparing plans across chains is unusable while `approved` is where a plan's status stops, which measured as 35 claims over 26 distinct paths with 8 collisions, every one false. **Finished is a field of its own beside `status:`, not a new status value** — the alternative was a new value, and it was rejected because a repository whose plans carried it, opened by an older plugin, would have zero approved plans by that hook's reading and, with `SDLC_GATE=block` set, would refuse every code edit in itself. An additive field leaves every existing reader behaving exactly as today; the cost accepted is a second lifecycle signal and every reader learning to check both. **Legal on `plan.md` alone**, not on all five shapes: that keeps the scaffold executor's charter gate untouched and the five status-branching skills unchanged. **One-way** — a defect in finished work is a new chain, because reopening a plan silently re-acquires every file it claimed. **Written by the verify skill at feature tier**; task tier is marked by hand until a closing step exists, which is `OPEN.md` item 4 and not this change. **Grammar, stated and checked rather than widened:** a `files:` entry is a quoted string on the `files:` line and is a file path, never a directory or glob; `status:` is unquoted and exactly one declared value. The readers only ever accepted these, so nothing becomes illegal that ever worked — what changes is that the other forms now fail loudly instead of claiming nothing. **I6 gains a line saying cross-chain disjointness was always inside its words**, so the checks that watch for it are instruments riding a change's gate rather than a constraint move. **I8 records how much sharper the blocking switch becomes**: between chains the ordinary state is no live plan, and the gate then refuses every code edit under that switch. **I9 records what the known-and-unchecked list is for** — a class whose instrument is a different change — and that a list growing from two entries to seven in one change is itself a finding for this conversation. **The three-parsers paragraph is corrected**: two of the three now share an extracted parser, the JavaScript stands alone, and the claim that they were "kept in step by hand and by test" was false when written — no test has ever compared them. The fourth-consumer trigger stands. **Twelve questions, one conversation**, for the reason v8 gave: an amendment costs a version and a log row, and asking separately costs twelve of each. |
| 8 | 2026-09-20 | **Settled the contradiction that versions 4, 5, 6 and 7 each recorded and none resolved**, plus two smaller things, in one conversation rather than three — an amendment costs a version and a log row, so batching them costs one of each instead of three. **The contradiction:** the charter schema forbade amending as a side effect of shipping, while the silent-failure register cannot stay complete unless it moves while a check ships. Resolved by splitting what the rule governs — moving a *constraint* an intent was approved against still takes its own approval conversation; adding a *row* that records an instrument rides the approval gate of the change that adds the check, and that change's plan must declare this file in its slice manifest. The schema carries the rule and this file points at it, so there is one statement rather than two that can drift. Four amendments' worth of evidence is what the distinction was drawn from, which is why it took four to see it. **The inert row:** `ScaffoldEmitsBuildableSolution_<kind>` named no instrument in either direction, because the register parser keeps a backticked string only when it matches an identifier and the angle brackets fail that filter — so the register built to catch silent failures contained one. Now names the test class like its three neighbours, which also keeps a list of solution kinds out of this file. **The reach of N2:** it binds `plugins/**` and not the chain under the artifact root, because the non-goal exists so the library survives a change of employer and a chain document never leaves with the plugin. No artifact edits follow; the four documents naming external repositories stay as they are. Recorded as unchecked: that non-goal has no instrument, unlike the first invariant's two. **Raised by** the cross-chain slice-manifest work, which adds a named check and could not be planned until the first of these was settled. |
| 7 | 2026-09-20 | **Registered the check that holds a pack's promise to its manifest.** `pack-promises-what-it-scaffolds` adds `PackPromiseTests`, and I9's table gains a row for it. **The same contradiction as v4, v5 and v6, now four for four:** the schema says amending the charter is *"never a side effect of shipping a feature"*, and the register cannot be complete unless it moves while one ships. The plan for this change did not list `CHARTER.md` in its slice manifest and the build could not go green without editing it — which is the clearest evidence yet that the two sentences disagree rather than that any one author keeps forgetting. Still recorded, still unresolved, and still the subject of `findings-leave-only-by-decision`. |
| 6 | 2026-09-19 | **Registered the check that enforces registration.** v5 added six rows by hand for checks that had existed for hours; `SilentFailureRegisterTests` now holds the register and the suite against each other, both directions — every test class is named by a row, and every check a row names exists. It closes four of those six. The other two were new methods on a class the register already named, and telling a new instrument from a new class of failure is a judgement no parser makes. **Recorded and not resolved:** this amendment is itself the thing the charter schema forbids — *"amending it is its own approval, never a side effect of shipping a feature"* — while I9 says its table *"grows whenever a new class is found"*, which happens while shipping. Versions 4, 5 and 6 all did it. The two sentences cannot both be right, and the sixth interrogation of `findings-leave-only-by-decision` blocked on exactly this. |
| 5 | 2026-09-19 | **Added six rows to I9's table** for checks that already existed and were never registered. Two — the pack-asset and engine-instruction checks behind R47 — were flagged by the sixth verification of `init-scaffolds-solution`, which had no seventh round to dispose of its own finding, and the chain shipped. The other four arrived with `verification-reports-options`, a task-tier change that needs no verification and therefore got none. Both routes are the same failure: a defect became a named check and the register that enumerates them did not move, because nothing requires it to. I9 says the set grows whenever a new class is found; it did not say who notices. That is the subject of the `findings-leave-only-by-decision` intent, and this amendment is the evidence for it rather than its fix. |
| 4 | 2026-09-18 | **Widened I1's checkable clause** from the engine's instruction files to the executables it ships as well, and named the second instrument beside the first. The old scope rested on engine tooling not executing pack-supplied data; the scaffold executor does, so the one engine file with the strongest reason to name a stack was the one file nothing scanned. Both instruments read one shared term list, because a second copy is how an instrument quietly stops covering what it claims to. **Added rows to I9's table** for the silent failures the scaffold checks now catch, plus one for an engine skill dropping its pointer to the plain-language rule and one for CI drifting from the prerequisites a pack's manifest declares. Deferred to ship time by the approved `init-scaffolds-solution` spec — unlike the two amendments before it, this one widens a check that change *adds* rather than an invariant it cites, so it could not move underneath the spec. |
| 3 | 2026-09-18 | **Widened I5** from the *files* a pack writes to the *actions* it performs, and named the consequence: the engine executes pack-supplied command strings, so the trust boundary is the pack. **Froze the emitted repository layout**, separating it from pack *contents*, which stay movable — "we never overwrite" is a promise about writes and says nothing about shape. Both amendments were required by the approved `init-scaffolds-solution` intent before its spec could be written, rather than discovered during it. |
| 2 | 2026-09-17 | **Added I12** — nothing is committed on a red build or a failing test, both suites, real output seen. Requested by the owner as a standing rule rather than a per-change habit. The engine states the requirement (`sdlc-router`, Gates); the stack pack states the commands, because I1 forbids the engine from naming them. |
| 1 | 2026-09-17 | Initial charter. Written by hand rather than from a stack pack, because no pack applies to a library of instructions. Attacked by `intent-interrogator` in fresh context before approval, which returned NOT READY: four invariants were contradicted by code in this repo, three statements of fact in Technology commitments were false, and N1 as first drafted forbade something the engine had already shipped. Nine of the eleven invariants were rewritten and four code defects fixed before this version was approved. The provenance matters — these invariants read as obvious now, and every one of them survived a reviewer trying to break it. |
