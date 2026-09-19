---
solution: ai-sdlc
artifact_root: .sdlc/
status: approved
version: 4
updated: 2026-09-18
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

**I9 — Every known silent-failure class has a named check in CI.** "Anything that can fail
silently" was unfalsifiable: it named no set, so no reviewer could ever declare it violated.
The set is enumerated instead, and grows whenever a new class is found.

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
| Scaffold produces an unbuildable solution | `ScaffoldEmitsBuildableSolution_<kind>` |
| Generator output breaks central package management | `ScaffoldEmitsBuildableSolutionTests` — restore with transitive pinning on |
| Emitted names stop matching the coverage patterns | `ScaffoldEmitsBuildableSolutionTests` — each emitted assembly name against `coverage.runsettings` |
| A re-run overwrites, or a rewrite re-fires | `ScaffoldEmitsBuildableSolutionTests` — hash the tree, re-execute, compare |
| A token survives, including from an earlier run | `ScaffoldManifestTests` — the sweep of every file under the target, and of chain files this run wrote to |
| The executor scaffolds from a draft charter | `ScaffoldManifestTests` — draft charter refuses, and leaves an empty tree |
| A prerequisite is absent and the check skips rather than fails | `ScaffoldEmitsBuildableSolutionTests` — an absent prerequisite fails the run |
| The pack's skill and its manifest list different files | `PackSkillCitesItsManifest` |
| CI stops providing a prerequisite a pack's manifest declares | `ScaffoldPrerequisiteTests` |

Every row is mutation-tested: the fault is injected and the named check must fail. A check that
has never been seen to fail is a check nobody should trust.

**Known and unchecked**, recorded rather than hidden: a plugin version that is valid semver but
was never *bumped* — the format is checked, the increment is not, so an update can still be a
silent no-op; and the eval suite, which has never run.

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

**The manifest is parsed three times by three hand-rolled parsers**, in
`Assert-SliceManifest.ps1`, the gate hook, and `parallel-build.js`. None shares code with the
others, and they have already diverged once: two normalised a leading `./` and the third did
not, so a plan could pass the check that fans out and fail the check that merely warns. They
are kept in step by hand and by test. If a fourth consumer appears, that is the moment to stop
and give them one parser.

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
| 4 | 2026-09-18 | **Widened I1's checkable clause** from the engine's instruction files to the executables it ships as well, and named the second instrument beside the first. The old scope rested on engine tooling not executing pack-supplied data; the scaffold executor does, so the one engine file with the strongest reason to name a stack was the one file nothing scanned. Both instruments read one shared term list, because a second copy is how an instrument quietly stops covering what it claims to. **Added rows to I9's table** for the silent failures the scaffold checks now catch, plus one for an engine skill dropping its pointer to the plain-language rule and one for CI drifting from the prerequisites a pack's manifest declares. Deferred to ship time by the approved `init-scaffolds-solution` spec — unlike the two amendments before it, this one widens a check that change *adds* rather than an invariant it cites, so it could not move underneath the spec. |
| 3 | 2026-09-18 | **Widened I5** from the *files* a pack writes to the *actions* it performs, and named the consequence: the engine executes pack-supplied command strings, so the trust boundary is the pack. **Froze the emitted repository layout**, separating it from pack *contents*, which stay movable — "we never overwrite" is a promise about writes and says nothing about shape. Both amendments were required by the approved `init-scaffolds-solution` intent before its spec could be written, rather than discovered during it. |
| 2 | 2026-09-17 | **Added I12** — nothing is committed on a red build or a failing test, both suites, real output seen. Requested by the owner as a standing rule rather than a per-change habit. The engine states the requirement (`sdlc-router`, Gates); the stack pack states the commands, because I1 forbids the engine from naming them. |
| 1 | 2026-09-17 | Initial charter. Written by hand rather than from a stack pack, because no pack applies to a library of instructions. Attacked by `intent-interrogator` in fresh context before approval, which returned NOT READY: four invariants were contradicted by code in this repo, three statements of fact in Technology commitments were false, and N1 as first drafted forbade something the engine had already shipped. Nine of the eleven invariants were rewritten and four code defects fixed before this version was approved. The provenance matters — these invariants read as obvious now, and every one of them survived a reviewer trying to break it. |
