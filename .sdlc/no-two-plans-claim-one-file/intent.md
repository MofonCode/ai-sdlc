---
slug: no-two-plans-claim-one-file
title: No two approved plans claim the same file
status: draft
tier: feature
created: 2026-09-20
owner: peter
---

# intent — no two plans claim one file

> **BLOCKED ON A CHARTER AMENDMENT, 2026-09-20 — and it stays at `status: draft` until that
> amendment lands.** An earlier version of this banner said the intent was approvable while the
> spec was blocked. **There is no such state.** The only machine-readable signal is `status:`,
> and the spec skill's branch is binary — draft stops, approved proceeds. A banner is prose
> nothing reads. Approving this would set the one flag that unblocks the spec stage against the
> questions below, and every other blocked chain in this repository holds itself at draft for
> exactly this reason. **Holding at draft is the mechanism; a banner is the explanation.**
>
> The spec cannot be written until an amendment settles **eight** things, because
> the design differs depending on each answer: the mechanism that marks a plan finished (a
> reused status value or an additive marker) and who writes it; whether that marker is legal on
> all five artifact shapes or on plans alone; whether finished is one-way; whether a
> block-style `files:` list is legal; whether a manifest entry may be a directory or a glob;
> what an abandoned chain is; whether the blocking switch may mean what this change makes it
> mean; and whether the register's known-and-unchecked list may grow for a class this change
> creates. Most move shapes the charter freezes, so they take their own approval conversation
> rather than riding this change's gate. See **Prerequisite** below for the reasoning on each
> and for the questions that can be batched without holding the spec.
>
> **This change does *not* block on the closing step, and that is a decision rather than an
> omission.** Nothing writes the finished value after the one-time marking performed here,
> because no stage exists that every tier passes through at ship time. Creating one is its own
> change and this one is deliberately not held for it — the step is placed first precisely so
> other work can start. Until that chain lands, every new plan stays live after its work ships:
> a known gap, recorded as item 4 in `.sdlc/OPEN.md`, accepted so the comparison can land now.

## Problem

Four statements, each measured on 2026-09-20 rather than assumed.

**Nothing compares two plans.** `Assert-SliceManifest.ps1` proves the slices inside one plan
are pairwise disjoint, and the plan skill runs it at approval. The build does **not** run it —
it re-implements disjointness in its own JavaScript, because it has no filesystem access — so
the same rule is written twice in two languages, which is how the two came to normalise paths
differently. Either way both operate on a single plan: two separately approved plans claiming
the same file is not a state anything in the framework can observe. The failure arrives as a merge conflict discovered after both
implementers have finished — the most expensive moment to find it, because the work is done
and some of it has to be thrown away.

**The artifacts cannot tell a live plan from a finished one — and the vocabulary that could
say so is declared but not implemented.** Three status values are declared: `draft`,
`approved`, `superseded`. Only two are ever read. The gate hook matches
`^status:\s*approved\s*$` and nothing else, the parallel build blocks anything that is not
literally `approved`, and the disjointness script never reads status at all. So `superseded`
is a word in the schema that no reader honours, and `approved` is where every plan's status
comes to rest in practice.

Six plans carry `approved`. Five are done — committed between 2026-09-17 and 2026-09-20, in a
repository whose first commit is 2026-09-16 — and a sixth was superseded without ever being
built. Run the check the obvious way — every plan reading `approved`, compared pairwise
— and today's repository reports **35 file claims over 26 distinct paths, with eight paths
claimed by more than one chain** and the stack pack's skill claimed by three. Not one of those
eight is a live collision. The data the check would run on is unusable until something
separates the states, and a check that reports eight false collisions on the day it lands is a
check that gets switched off.

**The cost of the gap, stated as what this change actually removes.** Two sessions cannot
safely run at once today, and this removes exactly one of the reasons: two approved plans
silently claiming the same implementation file. It does not remove the others, and the
direction measured the worst of them as the git index — staging everything sweeps up another
session's half-finished work. Nor does it help two concurrent *intents*, which claim no
implementation files because neither has a design yet. So the honest claim is not "two sessions
can now run at once"; it is "the file collision stops being invisible, and the rest stay
handled by practice."

**A fourth thing, found while scoping this and contradicting the assumption it started
from.** The gate hook already reads every approved plan under the artifact root, not only the
current chain's — but permissively. It goes quiet the moment *any* approved plan claims the
edited file, and it has no notion of which chain the edit belongs to. It also skips `.md`,
`.txt`, `.json`, `.yml`, `.yaml` and `.toml` outright and deliberately, so that artifacts stay
editable while a repository is being set up. Five of the eight measured collisions are
markdown or json. **The hook cannot see them and is not supposed to.** Whatever this change
does to the hook reaches code files only, and the honest coverage claim is smaller than the
direction assumed.

## Affected users and systems

Every file below was opened while writing this, not inferred from a search.

- **`plugins/sdlc-core/scripts/Assert-SliceManifest.ps1`** — takes one mandatory `-Plan`,
  parses the frontmatter with a deliberate line-oriented parse rather than a YAML dependency,
  proves the slices disjoint, resolves `depends_on`, and prints the wave plan so the approver
  sees the shape of the fan-out. Everything it does operates inside a single plan.
- **`plugins/sdlc-core/hooks/sdlc_gate.ps1`** — 212 lines, advisory by default, `SDLC_GATE=block`
  makes it refuse. Collects every plan whose frontmatter reads exactly `status: approved`,
  matches the edited path against their claimed files, and warns in one of two states: no
  approved plan exists at all, or a plan is approved but nothing claims this file. Skips six
  file extensions, three file *names*, and the chain itself — though that last skip is **not**
  a prefix test: it splits the edited path and asks whether any *segment* equals the artifact
  root's leaf name, so a repository rooted at `docs/sdlc` also silently skips
  `src/sdlc/Thing.cs`. Worth stating exactly in a document whose subject is comparing paths.
  It also holds a
  list of seven directory names, but that list is applied only when discovering the charter —
  **an edit under `bin/`, `obj/` or `node_modules/` is not skipped**, which matters because
  build output is named below as a separate collision surface. Swallows all errors on purpose,
  because a broken advisor on every keystroke is worse than a silent one.
- **`plugins/sdlc-core/workflows/parallel-build.js`** — gates on the literal frontmatter value
  being `approved` and on the passed-in slices matching the plan, and blocks otherwise. **Under
  the reused-status answer it needs no edit**: a plan marked finished is already refused, by
  code that exists, which is the wanted behaviour rather than a breakage. Under an additive
  marker it must be taught, because the status still reads approved. **It has never run:** its
  wave loop filters slice objects against
  a string id, so the remaining set never shrinks and the loop does not terminate. That is
  recorded outside this chain and is not this change's to fix, but it is why fan-out is not one
  of this change's homes.
- **`plugins/sdlc-core/skills/sdlc-router/references/artifact-schemas.md`** — declares the
  status vocabulary once, in a single line above all five artifact schemas, so it is not a
  plan-only vocabulary and cannot be changed as though it were.
- **`plugins/sdlc-core/scripts/Invoke-ScaffoldManifest.ps1`** — the fourth reader of that
  vocabulary. It refuses to scaffold unless the
  *charter's* status is literally `approved`, and that refusal has a register row behind it.
  Whether the amendment makes a finished value legal on all five shapes or only on plans
  decides whether this file needs a clause.
- **The five finished plans** — `init-scaffolds-solution`, `pack-promises-what-it-scaffolds`,
  `register-names-every-check`, `stack-dotnet-house-stack`, `verification-reports-options`.
  Each carries `status: approved` and each is done.
- **`engine-substitutes-declared-tokens/plan.md`** — a third state. Its frontmatter says
  `approved` over a draft intent and no spec at all, which is a standing violation of the rule
  that nothing downstream is produced from a draft. **Marking it superseded erases the only
  machine-readable trace of that**, so this change records it as an open item rather than
  merely noting it here — otherwise a fresh session reading the artifacts alone, which is the
  design premise, would never learn an approved plan was produced from a draft intent. Its body says **superseded on
  2026-09-20 without being built**, re-tiered to feature; its intent is a draft under a parked
  banner. So it is approved, never built, and replaced — and it still claims three files,
  one of which is the schema file this change must edit.
- **`tests/AiSdlc.Tests/SliceManifestTests.cs` and `GateHookTests.cs`** — the existing
  instruments for the two mechanisms being widened.
- **`tests/AiSdlc.Tests/SilentFailureRegisterTests.cs`** — holds the register and the suite
  against each other in both directions, which the two new checks must satisfy. **It needs no
  edit:** it discovers test classes by scanning the suite directory, so new classes are picked
  up on their own. Said explicitly because it is claimed by `register-names-every-check`, and
  if it *did* need editing the set would be seven files and eight claims — no new chain joins
  the marking ordering, because that one is already among the five being marked.
- **`plugins/sdlc-core/skills/plan/SKILL.md`** — holds the disjointness invocation and the
  gate that sets a plan to `approved`, which is the transition the finished-plan question is
  really about.
- **`plugins/sdlc-core/skills/verify/SKILL.md`** — the other candidate for moving a plan to
  finished, since it is the stage that knows the work shipped.
- **`README.md`** — names the script and describes what it rejects, which is a third and looser
  description beside the two exact invocations, and goes stale the same way when the script
  gains cross-plan behaviour. It is itself claimed by a finished plan.
- **`.github/workflows/checks.yml`** — **not** affected, and listed only to record why it was
  once thought to be. A dropped design added a check reading this repository's live chain
  directory, which would have made chain state a gate on every push and pull request and, on a
  merge-result build, would have compared plans across branches — behaviour a non-goal below
  explicitly disclaims. With that check dropped, none of that applies. Worth knowing separately
  that a finished plan declares this file correctly and the parsers turn the claim into
  `github/workflows/checks.yml`, dot stripped — a string matching nothing on disk. The plan is
  right; the reading of it is wrong.
- **`plugins/sdlc-core/.claude-plugin/plugin.json`** — this change ships hook and workflow
  code, and the charter commits to bumping the version every release because plugins are
  cached per version and skipping the bump makes an update a silent no-op. The last change to
  ship engine code declared this file in its slice manifest; an undeclared version bump is the
  same lapse as an undeclared artifact edit.
- **`plugins/sdlc-core/agents/verifier.md`** — reads the `files` lists in plan frontmatter and
  compares them against what the diff actually touched. It reads the manifest and does **not**
  read plan status. It is a *consumer* of the
  manifest without being a *parser* of it — an agent reading a document, not code implementing
  the grammar — and that is the distinction the non-goal below turns on. If the owner reads it
  as a fourth parser, the charter's trigger has already fired and this change inherits it.
- **`tests/AiSdlc.Tests/TempRepo.cs` and `Repo.cs`** — the fixture every named test builds on.
  It writes one chain with one plan from a fixed template and takes the plan's status as a
  string. It does have an escape hatch for arbitrary plan content, but that writes at the
  repository root rather than inside a chain directory, so it still cannot express two colliding
  plans — worth stating precisely, or an engineer goes looking for a shape that is not there. Every cross-plan test needs it
  changed, and it is shared by **three** suites — the gate-hook tests use it thirteen times,
  the slice-manifest tests eleven, the scaffold-manifest tests five — which makes it a
  serialisation point in this change's own fan-out: it sits in exactly one slice.

  **The fixture change is additive, with defaults**, so those three suites keep compiling
  unchanged and stay out of the affected set. If it cannot be additive, two more files move —
  and one of them, the scaffold-manifest tests, is itself one of the eight measured collisions,
  claimed by a finished plan and the superseded one. Said now rather than discovered at plan
  time, in a change whose own appendix is about miscounting affected files.
- **The script's invocation is written down five times** — the plan skill, the schema
  reference, a looser description in the README, the script's own example block, and a
  validation row in a shipped chain's plan. Earlier drafts said two, then three. If the
  invocation changes, all five go stale. That is this repository's own rule about a second list
  beside a source of truth, applied to this change rather than observed about someone else's.
- **Four shipped documents describe the gate hook's behaviour**, and this change adds a third
  state to all of them: `README.md`, `plugins/sdlc-core/hooks/hooks.json`, the hook's own
  header block which sets the two states out as a table, and the engine plugin's own
  `description`, which names the unplanned-scope warning this change widens. Four second lists
  beside one source of truth, in a change that invokes that rule repeatedly.

- **`plugins/sdlc-core/skills/sdlc-router/SKILL.md`** — a fourth place describing the
  disjointness rule, including the claim that the build asserts it before fan-out. It also
  holds a *competing* notion of which chain is active: if exactly one slice has no plan yet,
  that is probably the current one. This change introduces a status-derived notion of live, and
  leaving the two side by side unreconciled is the same second-list failure.
- **Five of the six engine skills branch on `status:` in prose** — `init`, `intent`, `plan`,
  `spec` and `verify`; only the router does not. If the amendment makes a finished value legal
  on all five artifact shapes rather than on plans alone, each becomes a reader with no branch
  for it.
- **`.sdlc/CHARTER.md`** — carries the invariant that slice manifests are pairwise disjoint,
  and the register of silent failures that a new named check must join.
- **`.sdlc/OPEN.md`** — this change edits it. The item recording that nothing writes a plan's
  finished state is written as *"once this ships"* and the item about the parallel build cites
  this chain's scoping decision; both go stale the day this lands, and the file's header counts
  its own items. Declared for the same reason the direction is.
- **`README.md`** — two separate things, and an earlier version of this conflated them. Its
  **runtime table** names shipped files by hand with nothing comparing it to the filesystem,
  and the new parser must join it. Its **gate-hook prose**, sixty lines earlier, says the
  hook's behaviour is pinned in `scripts/Test-SdlcGate.ps1` — a file that exists nowhere. That
  is the live drift, and **a check scoped to the table would ship green while that line stayed
  wrong**, which is worth knowing before the check becomes a register row.

  **The table is also messier than "a list of three scripts":** one row packs three
  plugin-relative paths into a single cell, another names a script that resolves at the
  *repository* root rather than under `plugins/`, and a third names a directory. Two different
  base paths behind one `scripts/` prefix, in the file the check has to parse.

  **So the check's scope is stated here rather than left to be assumed:** every backticked path
  in the runtime table resolves to something that exists, whether it is a file or a directory,
  resolved against the repository root and then against the engine plugin. Not "the table lists
  every shipped script" — that is a stronger claim needing a definition of *shipped* the table
  does not carry.

  **And the drift at line 139 is fixed by this change**, not disclaimed. It is four lines from
  a paragraph this change already edits, it names a file that has never existed, and leaving a
  known-false line in place while adding a check that ships green beside it is the posture this
  whole change argues against.
- **`.sdlc/DIRECTION.md`** — **already edited, on 2026-09-21, ahead of this chain.** The step's
  scoping line and its prerequisite line were rewritten the moment they were found false,
  because leaving the one file a fresh session is told to read first saying this step was
  unblocked and cheap was worse than an undeclared edit. **The residual delta this change still
  owes it** is the prerequisite line again, when the amendment lands and stops blocking.
  Declared for that, not for the edits already made. It is under the artifact root so it never
  reaches the comparison, but that is what makes it *safe*, not a reason to leave it out of the
  manifest — editing another chain's artifact without declaring it is the lapse this change
  exists to catch.
- **The owner**, who cannot currently run two sessions without hand-checking for overlap.

## Proposed outcome

A file claimed by a live plan is claimed by exactly one, and the framework says so without
being asked.

**A live plan is one whose own frontmatter says its work is agreed and not yet finished or
replaced.** Said once, here, as a property rather than a field test, because the term carries
the whole change and the amendment has not yet named the fields. Under a reused status value
that reads as "status is `approved`"; under an additive marker it is two conditions, because
the status still reads `approved` and a separate field says the work is done. Draft plans are
not live and claim nothing; a parked chain's plan is live, because parked describes the chain
and not the plan.

**Stating it as a one-field test would have quietly picked one of the amendment's two answers**
— which the criteria below are careful not to do, while four of them rest on this term.

Concretely, the end state a person would observe:

- A plan whose manifest overlaps another live plan's is reported, naming every live plan that
  claims the file, at two moments — and **exactly one of them is a guarantee.**

  **The gate hook is the guarantee** — with five bounds, all stated, because a guarantee whose
  edges are vague is worse than an advisory whose edges are clear. It fires on its own, in any
  repository that merely installs the plugin, with nobody remembering anything.

  **The fifth bound is the largest and is the one this change enlarges.** The hook wraps
  everything in a catch that exits 0, deliberately, so *any* exception inside the new
  comparison — a plan shaped in a way the new code did not expect, a strict-mode property
  access, a failed resolve — silently disables the entire gate, in every repository on the
  machine, until someone notices the absence of warnings. The packaging check covers exactly
  one cause of that silence, a missing parser file, and none of the others. This is not a new
  failure mode; it is an existing one whose surface area this change increases, and it belongs
  on the known-and-unchecked list beside the rest.

  **Its reach is narrower than "every edit", and the bound belongs on the page because the word
  guarantee is what rules out the prose alternative.** It is registered on three tools —
  edit, write, and notebook edit — so a file changed any other way never reaches it: a `git mv`,
  a shell redirect, a formatter, a generator, or any tool added later whose name is not one of
  those three. And it sees code files only, never the markdown and json where five of the eight
  measured collisions live. What it guarantees is narrow and real; what it does not cover is
  most of how a file can change.

  **Plan approval is a catch, not a guarantee.** The plan skill runs the check in its prose,
  which is where a collision is usually found and is not something that can be promised.

- **Between chains, with no live plan, the hook warns on every code edit — and that is the gate
  working, not a regression.** This is a consequence worth stating because nobody has ever seen
  it: six permanently-`approved` plans have kept the hook out of that branch since it was
  written. Once finished plans stop being live, zero live plans becomes the normal state
  between chains, and the zero-approved branch fires on *every* code edit regardless of path —
  it never looks at claims at all. With no plan agreed, you genuinely are editing outside any
  plan, which is the case the gate exists for. **Accepted deliberately**, including the sharp
  end: anyone running with the blocking switch set — a persistent environment variable, not a
  per-session flag — is refused every code edit between chains. The hook's own header warns
  that a tool which blocks everything gets uninstalled, so this is the trade being made with
  eyes open rather than a surprise on the first quiet afternoon. **Accepted here and open to
  the amendment**, which may overrule it: the switch's practical reach is what makes the
  hook-is-advisory invariant bearable, and this widens it. Provisional in the same way the
  finished-marker proposal is, rather than settled in two places by two owners.

  **With that switch set, a real two-plan collision refuses the file to *both* sessions**,
  including the one entitled to it — the hook has no notion of which chain an edit belongs to
  and no precedence rule, so neither can proceed. The intended resolution is to fix the plans:
  editing a `plan.md` is never gated, because the hook skips markdown, so the way out is always
  open.

  **And that branch's message becomes wrong, which no criterion would otherwise catch.** It
  reports the number of plans found and says *none approved* — so after the marking this
  repository would announce "6 plan.md found under .sdlc/ but none approved" about six plans
  that are finished, not unapproved. That is the message a person will see most often after
  this change. It needs rewording to distinguish "nothing has been agreed here" from "nothing
  is currently in flight".

  **No existing test moves; a new one is needed.** The one test asserting that phrase builds a
  repository with a *draft* plan — the case that keeps the original sentence. Nothing covers
  the branch this change makes common, where plans exist and none is live. An earlier version
  of this paragraph said the test moved with the reword, which described the same test doing
  two opposite things.

  **That needs parsing the hook does not do today**, which is why it is stated here rather than
  left to the build: it tests only for the literal approved value and never classifies the
  others, so telling "nothing agreed" from "nothing in flight" means reading non-live statuses
  too. And a genuinely unapproved draft still wants the original sentence — there is a test
  asserting exactly that — so this is two branches where there is one.

  **Three inputs, not two, and the third is the ordinary case.** After the marking, a session
  writing a new plan sits in a repository holding *one draft and five finished* — both
  conditions true at once, against a single message slot. That is not an edge: it is the shape
  of nearly every session here from the day this lands. **The draft sentence wins**, because a
  draft is a thing someone is actively working on and "get it approved" is the actionable half;
  the finished plans are just history and saying so adds nothing a person can act on.

  **Fan-out is not covered, and the reason is not a design preference.** The parallel build's
  wave loop filters slice objects against a string id, so the set of remaining slices never
  shrinks and the loop cannot terminate. It has never completed a build. Putting this change's
  headline behaviour on a code path that has never run would be asserting a guarantee against
  dead code. The defect is recorded outside this chain; fixing it needs the one-line change
  *and* a harness that executes a workflow body, since nothing here does — and that is a larger
  piece of work than this change should carry.
- A plan whose work is finished claims nothing, and the five already-finished plans say so.
- **The five finished plans are marked by hand, and no tool ships to do it.** Three drafts of
  this intent carried a bulk-marking tool, justified by adopting repositories inheriting the
  flood of false collisions. Measured, that justification does not hold: the tool only helps a
  repository holding plans written by the *old* plugin, a freshly initialised one has no plans
  at all, and the framework installs nowhere but this machine. The population is this
  repository, once. A repository initialised after this ships writes the new vocabulary from
  the start and never needs migrating.

  **What dropping it removes:** a runtime question, a confirmation protocol, an idempotency
  guarantee, a question about non-interactive input, and two obligations that attach to every
  shipped engine executable. **What it costs:** five status lines edited by hand across five
  committed artifacts — each visible in the diff, and the check that lands immediately
  afterwards proves the result is clean, which is stronger evidence than a tool run would give.
  If a repository with legacy plans ever adopts this, the tool gets written then, against
  requirements that exist rather than imagined ones.

  **This rests on an open item, which is worth tying together.** The population is one because
  the framework installs nowhere but this machine — an item whose closure is pointing the
  marketplace at the remote and tagging releases. **Closing it reopens this question**, and the
  first repository to adopt the framework from a tag is the moment to ask whether the tool is
  now worth writing.

- A plan that was **superseded** claims nothing either. A plan replaced without being built has
  no work in flight, so it holds no files. That makes honouring the already-declared
  `superseded` part of this change rather than a separate question, and it is why this change
  does not refuse its own plan: the one plan claiming the schema file here is superseded, and
  its status line currently disagrees with its own body.
- A plan belonging to a **parked or blocked** chain keeps its claims, because parked means
  paused and that chain resumes rather than restarts. Parked and superseded are different
  states and this is the line between them: paused work still owns its files, replaced work
  does not.

  **The marking comes first, and this is not a detail.** Measuring who claims the files this
  change must declare settles the order. The schema reference is claimed by a superseded plan
  *and* by a finished one; the charter by two finished plans, though that one never reaches the
  comparison because it sits under the artifact root; the plugin version file by a finished one.

  **Both rules are needed and neither is sufficient.** The schema reference is freed only when
  the superseded plan stops claiming *and* the finished one does. Marking the five alone leaves
  it colliding; honouring `superseded` alone leaves it colliding too. So both land **before**
  the check does, never after, and the plan sequences it that way — written here because a plan
  cannot be written without it, and because the alternative is a check that refuses the change
  that introduced it.

  **Two commits in order.** The five finished plans and the superseded one are marked first, by
  hand, in their own commit. The check lands second, against a tree that is already clean.

  **This change's own plan is approved without the check it introduces, and that is not a
  loophole to close.** At its approval the cross-plan check does not exist, and four of the
  files it declares are still claimed by live plans. The marking cannot precede approval either,
  since editing six other chains' plans is itself work this plan must declare. So the check's
  first real run is at validation time, against the already-marked tree — which is the ordering
  argument above, stated from the other end. A bootstrap has to start somewhere; what matters is
  that it is written down rather than discovered by whoever wonders why the gate was quiet.

  **The reason is not the one an earlier version of this gave.** That version said the hook
  would warn on every edit to the shared files while those plans were still live. It would not:
  five of the six are markdown or json, which the hook returns on before it looks at a claim at
  all, and today's hook stops at the *first* plan claiming a path, so it is structurally silent
  on multi-plan collisions until this change lands. The pre-change hook cannot report this.

  **The real reason is the plan's own validation.** Nothing is committed on a red build, and a
  plan's validation section names the commands that must be seen to pass — including, for this
  change, the new cross-plan check. Running it against a tree it disagrees with means the
  change cannot commit itself. Marking first is what makes the check's first real run true. Marking first makes the change quiet to work on and means the check arrives against a
  tree it agrees with, which is worth doing for its own sake rather than to avoid a red build.

  **This needs no mechanism, and the report does not say "parked".** Parked exists only as a
  prose banner at the top of an `intent.md`; no frontmatter field anywhere carries it, and the
  check reads `plan.md`. A parked chain's plan is simply `approved`, so it is live and keeps
  its claims by default — the outcome without any machine-readable notion of parked at all.
  Saying *which* state a claiming chain is in would mean adding a field to a frozen shape: a
  second constraint amendment, for a distinction the check does not need in order to be right.
- **When a chain's two documents disagree, the plan's own status governs its claims.** This is
  not hypothetical. Of the four parked or blocked chains, three have no `plan.md` at all; the
  fourth, `engine-substitutes-declared-tokens`, has an intent banner reading parked and a plan
  banner reading superseded, with frontmatter still saying `approved`. The check reads plans,
  so a plan's status decides that plan's claims: parked describes how far the chain got,
  superseded describes what happened to this particular plan. That chain therefore claims
  nothing. The rule is written here because the two documents visibly contradict each other and
  the next reader will find whichever one this does not mention.
- Editing a code file that more than one live plan claims produces a warning saying so. It
  respects the existing blocking switch exactly as every other warning does — off warns, on
  refuses — because someone who turned that switch on asked for precisely that, and a second
  grade of warning would make one setting mean two things.

## Success criteria

Each states what breaks if the change is not made, because a criterion that still passes with
the headline change skipped is not a criterion — a lapse this repository has made twice.

**What binds the spec, and what does not.** The engine's own schema says an intent contains no
implementation and that a proposed outcome is still not a design. Repeated rounds of
interrogation pulled a great deal of mechanism into this document, because each round asked
"what exactly?" and this was the only artifact open. That has been pulled back out: **what
remains binding is the properties** — one parser shared by both readers, a normalisation that
neither conflates distinct paths nor misses two spellings of one, three distinguishable
outcomes, a refused plan that never silences the hook, the comparison unit being the plan.
**The algorithms, return shapes, exit codes, message wording and source placement are the
spec's**, and it may choose them freely provided the properties hold. Said here because
anything approved at this gate cannot be superseded downstream without reopening this document,
and several of those mechanisms depend on amendment answers that do not exist yet.

**Criterion 1 is *contingent* on the fifth blocking question, which is the second reason this
document holds at draft.** There is no "approved subject to" in the schema, no field recording
which criteria are provisional, and no check that fires when the amendment answers the other
way — so a contingent criterion inside an approved intent is a condition nothing carries. The
condition is discharged the same way the block is: the amendment answers, this document is
re-read against the answer, and only then does the status move. It settles the comparison — exact equality after a stated normalisation,
case-insensitive — while the prerequisite section argues that if a manifest entry may be a
directory, exact equality stops being a correct comparison and misses real collisions. Both
cannot stand. So criterion 1 holds as written **under the answer that an entry is a file path**,
and if the amendment legalises directories or globs it is reopened rather than quietly
satisfied by a comparison that no longer works. An earlier version of this preamble marked two
criteria provisional and left this one unmarked, which was the wrong one to miss.

**Criteria 2 and 5 are *shaped* by the amendment and not contingent on it.** An earlier version
of this paragraph called them void if the amendment answered differently, which would have
deleted the change's core property without anyone noticing. Both are mechanism-neutral
properties — a finished plan claims nothing, every reader of a plan's status has a stated
behaviour — and neither depends on the marker being a status value.

**What is *not* a criterion here, and why:** that some stage writes the finished value every
time a chain ships. There is no such stage — verification is feature-tier only and
human-invoked, the plan skill runs at plan time, and task tier passes through nothing after
approval, which is how four of the five plans being marked here shipped. Creating one binds the
tier contract, needs its own gate, and is impossible at trivial tier, so it is a separate
change. **Making it a criterion of this one produced a deadlock**: this step is placed first
with nothing else starting until it lands, and the criterion blocked it on a chain that does
not exist. So it leaves, and **the decay it guarded against is recorded as a known gap
instead** — until a closing step exists, every new plan stays live after its work ships, and
the collision report slowly fills with chains that are finished. That is the honest cost of
shipping the comparison first.

**Under an additive marker, criterion 5 becomes larger rather than void.** `status: approved`
would still match in every reader, so a finished plan stays live until each of the four is
taught to read a second field. Declaring the criterion void under that answer would have
deleted the only requirement that those readers get reconciled, in exactly the case where
reconciling them is the whole job. What the amendment changes is the *shape* of the work behind
these two, never whether they hold.

1. **Two live plans claiming one file is reported, naming every live plan that claims it and
   the path, comparing paths the way the filesystem does.** Every claiming plan, not "the other
   chain" — no mechanism here knows which chain an edit belongs to, and this criterion and the
   hook's below must not describe two different messages.
   On a code edit the hook warns, subject to the blocking
   switch like every other warning. At plan approval the check refuses rather than reporting
   and continuing, because overlap is a planning bug — fixed in the plan and re-approved, never
   reconciled during or after the build.
   **The plan being checked contributes its claims whatever its own status says; only the
   *other* plans are filtered to live, and the plan itself is excluded from that set by
   resolved path.** Both halves are load-bearing.

   Without the exclusion, every *re*-approval refuses: the plan is already `approved` on disk
   when the fix is re-checked, so it collides with itself on every file it claims — and
   re-approving after fixing an overlap is the path the invariant prescribes, not an edge case.

   Without the first half, the check never fires at all on a *first* approval. The plan skill
   writes the plan as a draft, runs the check, and only sets `approved` at the gate afterwards
   — so at the moment of checking, the plan under test is a draft, and "a draft plan claims
   nothing" would exclude the very manifest being approved. That non-goal is about *other*
   plans sitting in the tree, never about the one being checked, and saying so here is what
   stops an engineer implementing a check that silently never fires.

   The hook needs neither rule: its test is already "more than one live plan claims this".

   **Which half of this is testable, said plainly.** The comparison itself is executable and
   gets a test. That the plan skill *invokes* it at approval is prose with nothing behind it —
   the same class of thing the direction diagnoses as the reason this work exists, and it is
   not made better by being in this change's favour. The verification record says which half
   was proved and which was read.

   **And it assumes the session's working directory is the repository root — as the base of
   comparison, not only for discovery.** Started a level *below* the root, discovery finds
   nothing and the hook exits silently. Started a level *above* it, the failure is worse and is
   reproducible: discovery's two-level glob still finds the charter and reads every plan, but
   the edited path is then made relative to the wrong base, so no claim can ever match. Run
   from the parent directory today, the hook reports `ai-sdlc/src/Foo.cs is claimed by no slice
   of the approved plan(s)`. After this change the same mismatch means two live plans claiming
   one file produce no collision warning at all — the guarantee failing silently, which is the
   failure this change exists to remove.

   **Naming it makes it a known silent-failure class, and the charter requires those to be
   accounted for**, so it goes on the known-and-unchecked list rather than getting a row. The
   existing row about the gate going quiet covers a *missing or non-default root*; this is a
   correctly resolved root with a wrongly based comparison, which that check does not reach.
   It is left unchecked rather than instrumented because the fix is about how the hook
   establishes its base at all — a change to cwd handling that touches every path it compares,
   which is its own work and not a widening of this one. Recorded so it is a decision.
   Comparison is **case-insensitive**: the charter commits to Windows only, where two chains
   claiming one file in different casing collide on disk, and the script already compares that
   way while the hook does not.
   **Files under the artifact root are excluded from the comparison entirely** — see the
   non-goal below for why that is forced rather than convenient. The exclusion is the caller's,
   applied to the normalised set against a root it resolved rather than assumed, which the new
   normalisation makes possible: a single leading `./` is removed and nothing else, so
   `.sdlc/CHARTER.md` survives as itself instead of collapsing to a path that does not exist.
   Without any of this, nothing in the framework observes the state, which is today's position
   exactly.
2. **A finished plan claims nothing, the five finished plans say so, and what is written
   matches what the hook and the script match on — exactly.** The spelling clause earns its place
   because nothing in the suite validates a chain artifact's status against the declared
   vocabulary: every reader treats "not exactly the live value" identically, so a mis-spelled
   marking still de-lives the plan and still passes the plan-time validation run, held only by
   whoever reads the diff. Low consequence under a reused status value, higher under an
   additive marker, where a field spelled wrong is a field nobody sees. Without it, this
   repository's first real run reports eight collisions that are all false, and the check is
   switched off rather than trusted. No claim is made here about other repositories: a
   repository initialised after this ships writes the new vocabulary from the start and has
   nothing to migrate, and one holding plans from the older plugin cannot exist yet, because
   the framework installs nowhere but this machine.
3. **A superseded plan claims nothing.** Without it, this change is refused by its own plan on
   the day it lands. The schema file it must edit is claimed by a superseded plan *and* a
   finished one, so this rule and the marking are both required and neither alone is enough.

   **A parked chain's plan keeping its claims is deliberately not a criterion here.** It needs
   no mechanism — such a plan reads `approved` and is live already — so as a criterion it would
   pass today, with the headline change skipped, which this document's own standard disqualifies.
   It is pinned by a built fixture instead: a plan whose chain carries a parked banner and whose
   own status is `approved` still claims its files.
4. **The check runs without anyone remembering to run it, in a repository that merely installs
   the plugin.** Without it, this becomes a mechanism used by choice, which the direction
   already rejected as the reason not to build a lock file: a mechanism used by choice is one
   that eventually is not.

   **One home satisfies this, and it is the gate hook** — named as a constraint on the design
   rather than a choice made for it, because the property "fires without being invoked" rules
   out everything else the framework currently has. The hook already reads every plan under the
   artifact root on every edit, so comparing them pairwise costs almost nothing and happens
   without anyone asking. It is not prose and it acquires no trigger of its own: it runs inside
   a session a human started, which is the line the charter's non-goal about pipelines draws.

   **Fan-out is a *future* home, not a second one.**
   The build would have to dispatch an agent
   that runs the script and relays its output, since it has no filesystem access of its own —
   a shape no workflow here has ever used, on a code path whose wave loop cannot terminate and
   has therefore never run. It is out of scope, and this criterion rests on the hook alone.

   **The observable event that discharges this**, so it is falsifiable rather than a matter of
   where a reviewer thinks the code landed: *the shipped hook, invoked with an edit payload in
   a throwaway repository containing nothing but a charter and two colliding plans, writes the
   collision to standard error.* No project setup, no test harness of the framework's own, no
   one remembering anything. The existing gate-hook tests already drive the shipped hook this
   way in a temporary repository, and the charter's own commitment is that tests drive the real
   shipped artifacts as external processes — so this is testable today by a method already in
   use.

   **What does not satisfy it:** the plan skill's prose, which is exactly the defect the
   direction diagnoses — a decision living where nothing can test it — and a green register
   row on its own, which says a check exists in this repository's suite and nothing about
   whether it fires anywhere else.

   **The trigger is a code edit or a build, never editing `plan.md` itself.** The hook returns
   immediately on markdown and on anything inside the artifact root, so it will never fire *on*
   a plan. It does not need to: the collision it reports is between plans.

   **And it reports only for the path being edited**, not by sweeping the claim set — so a
   collision on one file surfaces when someone touches *that* file and at no other moment. Far
   earlier than a merge conflict after both implementers have finished, which is the point, but
   not the continuous watch the phrase "fires on every edit" suggests.
5. **The gate hook and the disjointness script each have a stated behaviour under the new
   value — and the scaffold executor under one amendment answer — with the behaviour asserted
   rather than somebody's reading of it.** Named as a list rather than "every reader", because
   the open question below admits the readers cannot be enumerated by any check, and a
   criterion quantifying over an unenumerable set cannot fail. **The hook must treat a finished
   plan as not live, so it claims nothing.** Two readers exist today — the hook and the parallel
   build — but the build is excluded, for the reason below, so asserting a "must" over both and
   then withdrawing one of them three paragraphs later would leave an engineer with a
   requirement that has no instrument and no scope.

   **The parallel build is excluded from this criterion's reader set, and the reason is
   stronger than "it has never run".** Its status gate is **unreachable**: the wave loop sits
   some forty-six lines above it and cannot terminate, so no execution ever arrives at the branch that
   refuses a non-approved plan. A clause asserting the build refuses a finished plan would be
   discharged by reading a branch nothing reaches — requiring zero work, provable by nobody,
   and passing with the headline change skipped, which is exactly what this section's preamble
   disqualifies.

   **It rejoins the set when the loop is fixed**, which is a recorded open item and not this
   change's work. Until then the honest statement is that the build reads a plan's status in
   source and not in fact. **The disjointness script becomes a third, because this change makes it one** — it
   reads no status at all today, which the problem statement says and an earlier version of
   this criterion contradicted by counting it among existing readers. **The scaffold executor
   is not a reader of plan status at all:** it gates on the *charter's*, so it moves only if
   the amendment makes the value legal on all five artifact shapes, and then only to gain a
   clause saying a charter is never finished. A review activity is not a criterion; these are
   behaviours a test can hold. Without checking all of them, adding a status value
   breaks the one gate the framework calls non-negotiable, and it breaks silently, because a
   blocked build looks like a careful build.

   **What can actually run this, stated because the answer is uncomfortable.** Nothing in this
   repository executes a workflow body — the only instrument over the parallel build asserts
   that it *parses*, which is how a non-terminating loop shipped in it unnoticed. So the
   build's clause here is verified by reading, and stays that way until someone builds a
   harness, which is not this change. The hook, the script and the scaffold executor are all
   executable today. Say which is which in the verification record rather than letting a green
   suite imply the whole criterion was proved.
6. **Editing a code file that more than one live plan claims produces a warning saying so.**
   Deliberately weaker than naming the other chain, because the hook has no notion of which
   chain an edit belongs to and inventing one is not this change's work. Without it, the hook's
   existing message lists every approved chain and reports only that *nothing* claims the file,
   which is the opposite state and says nothing about the collision. Honest about its reach:
   the hook sees code files only, so the five markdown and json collisions measured above are
   caught by comparing plans or not at all. The existing test asserting the hook stays silent
   on a claimed file pins the current early return — the hook stops at the *first* plan that
   claims a path and structurally cannot notice a second. Moving that return is the change.
   **No existing test moves with it:** the test that
   asserts silence on a claimed file builds a repository with exactly one chain, so under the
   new behaviour it is still silent and still passes. Nothing pins the multi-plan case because
   the fixture cannot construct one — which is the same limitation named two bullets above and
   is why that fixture is on the affected list. **What the warning says:** it names every live
   plan claiming the file, because the hook does not know which chain the edit belongs to, and
   naming all of them is the only honest message it can write.
7. **Two checks: one proving the mechanism over built fixtures, and one the extraction creates.** It builds temporary repositories
   holding colliding and non-colliding plans and asserts what the comparison reports, the way
   the rest of the suite works.

   **A second check reading this repository's own chain directory was considered and dropped**,
   and the reasons are worth keeping because they are not obvious. It would be **green by
   construction** most of the time: after the marking, this repository's normal state between
   chains is zero or one live plan, so the check would assert an empty set and pass without
   ever exercising anything. And every register row must be mutation-tested — the fault
   injected, the named check seen to fail — which for this one means writing a colliding plan
   into the live chain directory and restoring it, the operation this repository's own
   standing instruction warns has already destroyed uncommitted work. A row that cannot be
   mutation-tested is inert on arrival, which is the defect charter version 8 was partly spent
   correcting. **What is given up:** two colliding live plans here would pass a green build —
   though the hook still warns on the first edit to either file, which is the guarantee anyway.

   **Two register rows, for two different silent failures.** The first is the collision check
   above. The second is the one this change *creates*: the gate going permanently quiet because
   the parser file it sources has moved, failed to parse, or is missing from a half-updated
   plugin cache. The hook swallows every error and exits 0 by design, so it would say nothing,
   forever. Nothing existing catches that, because the check that hook commands point at real
   files reads the command strings in the hook manifest and a sourced file is invisible to it.

   **A third check holding the README's runtime table to the filesystem was considered and
   dropped, on this section's own test.** It would pass in full with this change deleted — it
   has nothing to do with cross-plan collisions — which is exactly what the preamble
   disqualifies. It also brings a test class, a register row, a mutation test and machinery to
   parse a table this document already calls messier than it looks, into a change the direction
   has re-scoped once for being three times its estimate. **The stale line is still fixed
   here**, because it names a file that has never existed and sits four lines from prose this
   change edits; what moves out is the *check*, as a documentation-integrity chain of its own.

   **That is a new class, not a new instrument for an old one**, which is the judgement the
   charter says no parser can make and this document therefore has to. The existing row about
   the gate going quiet covers the gate *not matching*; this is the gate not *running*, from a
   cause that does not exist until this change introduces it.

   **It is caught in CI, not at runtime, and the difference resolves a contradiction.** The
   hook cannot warn about its own missing parser, because root resolution lives *in* that
   parser — so without it the hook cannot tell a framework repository from any other, and the
   only safe thing it can do is exactly what it does today: exit 0 and say nothing. Making it
   warn instead would put a line on standard error in every repository on the machine, breaking
   the constraint two sections above that it stays silent where the framework is not in use.
   Giving it a second, cheaper root test of its own would be a second implementation of the one
   thing this change argues must have a single one.

   **So the check is a packaging check.** It drives the shipped hook, with the shipped parser,
   in a fixture repository that does have a charter, and asserts output appears; with the
   parser removed, it must fail. The fault is injected in a fixture, never in the live plugin
   directory — which is what makes this row mutation-testable where the dropped one was not.

   **That needs machinery no helper has today, and it is not covered by "the fixture change is
   additive".** The suite resolves the hook by walking up from the test binary to the plugin
   directory — not a hard-coded path, as an earlier version of this said, but still a path into
   the *live* plugin tree — and the temp-repo helper builds a *target* repository, not a copy of
   the plugin. Injecting a missing parser without touching the live directory means **copying the
   engine plugin into a temporary tree and driving the copy** — a new helper, and a real piece
   of work that the affected-systems entry understates. The path constants file also gains a
   third entry, for the parser, beside the two it holds for the hook and the script.

   **What it does not cover, named rather than implied:** a plugin cache that is half-updated on
   someone's machine. CI proves the plugin ships consistent with itself; it cannot prove what a
   given installation has on disk. That residue belongs to plugin distribution, not here.

   **After the marking this repository has exactly one live plan — this change's own** — so
   nothing here exercises the cross-plan path against real data: every one of the other six
   becomes finished or superseded, and no parked chain has a live plan. That one plan also has
   no closing step to retire it, which is the recorded open item arriving on the day this lands
   rather than later. The comparison is proved against built fixtures only.
   That belongs in the verification record explicitly, so a green suite is not read as evidence
   that this repository's own chains were compared.

   **Four uncovered residues go into the charter's own list for them** — that one; the fact
   that two colliding live plans in this repository now pass a green build; the hook comparing
   against the wrong base when a session starts above the repository root, which after this
   change turns a noisy wrong answer into a silent one; and any exception inside the new
   comparison disabling the gate entirely, since the hook swallows everything by design. The charter keeps a paragraph of known-and-unchecked failures beside the register so
   gaps are recorded rather than implied.

   **And that edit has to be tested against the version-8 rule rather than assumed to ride.**
   That rule lets a *row recording an instrument* ride a change's gate. A known-and-unchecked
   line records a failure class with **no** instrument, which is neither a row of that kind nor
   a constraint — a third case the rule does not name. Left unexamined this is the failure that
   charter versions 4 through 7 each recorded and none resolved, repeating at the plan gate.
   It goes to the amendment conversation unless that conversation rules otherwise.
8. **Both new checks are *new test classes*, each named by its own register row, and each is
   seen to fail when its fault is injected.** Without it, the register stops enumerating the
   set it claims to, which is the failure charter versions 5 and 6 were both spent on.

   **Each in its own new file, with the register row naming the class** — and the reason is
   sharper than "a new class is enforced": the register check is weaker than an earlier version
   of this claimed. It reads only the **first** `[TestClass]` in each file, scans the suite's
   top directory only, and passes a class when *either* its own name **or any of its method
   names** already appears in the register. So a second class in an existing file is invisible
   to it, a class in a subdirectory is invisible to it, and a new class carrying a method whose
   name an existing row mentions slips through. The shape this criterion demands — new file,
   one class, row naming the class — is the one shape the check actually sees.

   **Those three blind spots are a silent-failure class of their own**, in the instrument whose
   whole job is catching that class. This change relies on the check and does not repair it:
   the repair is a change to `SilentFailureRegisterTests` with its own mutation tests, and
   inheriting it here would be the scope creep this document refuses elsewhere. It goes on the
   known-and-unchecked list with the rest — the fifth this change adds, and one more reason the
   amendment should look at that list growing.

## Prerequisite: a charter amendment before the spec is written

**A way to say a plan is finished moves a shape the charter freezes**, and the charter freezes
it for a stated reason: other repositories persist these frontmatter shapes and three separate
parsers read them, so changing one is a migration rather than an edit. Under the rule settled
at charter version 8, moving a constraint takes its own approval conversation and only a
register row rides this change's gate.

So the amendment lands **before the spec**, not with the change. The precedent is exact: two
amendments were required by the approved `init-scaffolds-solution` intent before its spec could
be written, and the one amendment that was deferred to ship time was deferred because it
*widened a check that change added* rather than a constraint the spec cited. This is the first
kind, not the second.

The amendment conversation settles **what mechanism marks a plan finished** and who moves it
there — deliberately phrased as a mechanism rather than a value, because the obvious framing
forecloses the option that removes this change's worst consequence.

**Two mechanisms, both costed, because the agenda must not assume one.** Reusing `status:` is
what this document is written against and it is the simpler shape — one lifecycle field, one
thing to read. But it is *also* the direct cause of the worst case in the reversibility section:
a repository whose plans carry the new value, opened with an older plugin, has zero approved
plans by that hook's reading, and with the blocking switch set the old hook then refuses every
code edit in it. An **additive marker** — `status: approved` unchanged, plus a separate field
saying the work is done — leaves every existing reader behaving exactly as it does today and
that failure disappears entirely. Its cost is a second lifecycle signal beside `status:`, which
is a real objection under this repository's rule about a second list beside a source of truth.

That trade is the amendment's to weigh. This document has a preference and not the authority,
and an agenda reading "what *value* means finished" would have removed the alternative without
anyone noticing it had been removed.

**It also has to settle who writes `superseded`, because nothing does.** The value is declared
in the schema and set by no skill anywhere; the one plan that carries the state carries it as a
prose banner while its frontmatter still reads `approved`. A state this change depends on
reading needs a moment and an owner for the writing, or it stays a word in a schema.

**And it has to settle how far the new value reaches.** The status vocabulary is declared once,
in a single line above all five artifact schemas — charter, intent, spec, plan and
verification. A value meaning finished either becomes legal on all five or the schema grows its
first per-artifact exception. Neither is obviously right and the spec will otherwise decide it
by accident.

**Two more frozen-shape questions belong in the same conversation, not at the spec gate.** The
charter freezes the `slices` / `files` / `depends_on` manifest alongside the five frontmatter
shapes. So declaring that a manifest entry is a file path and never a directory or a glob, and
deciding whether a block-style list is legal, are both moves of a frozen shape — and the schema
forbids neither today, it merely shows one style by example. Declaring block style illegal
invalidates plans that are valid right now in any adopting repository, which is a migration
rather than an edit, which is the reason the shape is frozen.

**And the blocking switch quietly changes meaning, which belongs on this agenda rather than
only in this intent's prose.** Today it refuses *unplanned* edits. Afterwards it refuses
*every* code edit whenever no chain is in flight — the normal state between chains, in every
repository that adopts this. The invariant that makes the switch opt-in notes it is set
persistently and is a sharper instrument than it looks; it now cuts deeper than when that was
written.

**And it has to settle what an *abandoned* chain is.** Finished, superseded, parked and draft
all have answers now. A chain simply dropped — never shipped, never replaced, nobody returns to
it — keeps `approved` and stays live forever, claiming its files and recreating in miniature
the flood this change exists to remove. It is the same question as who writes `superseded`,
approached from the other side, and neither this document nor the framework names the case.

**It has to settle whether finished is one-way.** A chain ships, its plan is marked finished,
and then a defect in that work needs fixing. Does the plan return to `approved` — re-acquiring
five to eight file claims that may now collide with live work — or is the fix a new chain?
This is a transition in a persisted format and belongs beside the forward transition rather
than after it.

**The agenda, and one thing that is not on it.**

**The closing step is not on it, and this change does not block on it either.** No stage
exists that every tier passes through at ship time, so the value nothing writes has nowhere to
be written. Creating one binds the tier contract, needs its own gate, and is impossible at
trivial tier; it is its own change. It satisfies no criterion here — it satisfies the property
the criteria preamble names as deliberately not one, and the decay in the meantime is recorded
as an open item rather than blocking this chain, which is placed first precisely so that other
work can start.

**Eight block the spec**, by the test this section uses throughout — the design differs
depending on the answer:

1. The mechanism that marks a plan finished, a reused status field or an additive marker, and
   who writes it.
2. Whether that marker is legal on all five artifact shapes or on plans alone.
3. Whether finished is one-way.
4. **Whether a block-style `files:` list is legal — and whether an *unquoted* flow-style one
   is.** Both are the same silent-skip class and the second was unnamed for twenty-nine rounds:
   both readers harvest only *quoted* strings from the `files:` line, so `files: [src/A.cs]` —
   valid YAML, forbidden nowhere in the schema — makes a plan claim nothing, without a word.
   The asymmetry is visible four lines apart in one file: `depends_on` is parsed
   unquoted-tolerant, `files:` is not. An earlier version of this section called
   it a frozen-shape move in one paragraph and a wording change in the next. The first is
   right, and it blocks: both readers today harvest quoted strings from the single line
   containing `files:`, so legalising block style is a *different parse*, written into the very
   file this change extracts and whose signature becomes a contract the moment two callers
   depend on it.
5. **Whether a manifest entry may be a directory or a glob.** Also blocking, and for a sharper
   reason: every reader compares by exact string equality after normalising, so if a directory
   entry is legal then one plan claiming a directory and another claiming a file inside it
   collide on disk and not in the check. This document already calls that "a false negative in
   the very mechanism being built". A spec written before the answer would specify a comparison
   the answer invalidates.

**Two more that block, added after they were argued for above and then left off both lists:**

6. **What an *abandoned* chain is.** A chain never shipped, never replaced and never returned
   to keeps `approved` and stays live forever, claiming its files — recreating in miniature the
   flood this change exists to remove. Blocking, because the definition of live depends on it.
7. **Whether the blocking switch may mean what this change makes it mean.** It refuses
   *unplanned* edits today; afterwards it refuses *every* code edit whenever no chain is in
   flight, which becomes the ordinary state between chains. Blocking, because if the amendment
   declines, the zero-live-plans branch needs a different design and that is design, not wording.
8. **Whether the register's known-and-unchecked list may grow for a class *this change
   creates*.** An earlier version routed this as batchable in one section and blocking in
   another. It blocks: the invariant says every known silent-failure class has a named check
   and the set grows whenever one is found, with no carve-out. If the amendment declines, the
   exception silencing the gate must be instrumented rather than recorded — which is design.
   The count also matters on its own, and it is counted rather than recalled: the charter's list
   holds **two** entries today — a plugin version that is valid semver but was never bumped, and
   the eval suite that has never run. This change adds **five** — no real-data exercise of the
   cross-plan path here, two colliding live plans passing a green build, the hook comparing
   against the wrong base from above the root, any exception in the new comparison disabling the
   gate entirely, and the register check's own three blind spots. **Two plus five is seven**, and
   an earlier version of this line said six, in the paragraph arguing that the count matters.

**Batchable without holding the spec:** who writes `superseded`, whether a status line may be
quoted, whether the disjointness invariant widens, and the rewording of the charter's
three-parsers paragraph. **Not** the known-and-unchecked question, which an earlier version
listed here while arguing it blocked, two hundred lines apart.
Said plainly because an amendment with ten agenda items is one that gets deferred, and this
change cannot start until it happens.

**Expect this to cost a charter version even on the instrumentation reading.** The invariant's
checkable clause says the disjointness script is run and its real output pasted — a human
pasting output, which is exactly what this change argues is insufficient. Widening a checkable
clause is what charter version 4 did, and it bumped the version and took a log row even though
it was deferred to ship time. Saying so now is cheaper than discovering it when the build will
not go green.

**One thing this prerequisite does not cover, stated so it is not assumed.** Widening the
disjointness check from one plan to many is read here as **instrumentation, not a wider
constraint**: the invariant says slice manifests are pairwise disjoint and does not say "within
one plan", so cross-chain overlap was always inside those words and merely had nothing watching
it. That makes it a register row riding this change's gate rather than a second amendment. If
the owner reads the invariant as having meant one plan all along, this becomes a second
constraint amendment and belongs in the same conversation.

## Constraints

- **Windows PowerShell 5.1** for the hook and the script. Not a preference: it is the only
  runtime present on every Windows machine with no install step, and the roughly 150ms per
  edit was measured and accepted rather than assumed.
- **The hook runs on every edit in every repository**, including ones that never adopted this
  framework. It must stay silent where the framework is not in use, and never throw.
- **A stated time budget, because "stay fast" is not falsifiable.** Today the hook stops at the
  *first* plan claiming the edited path, and warning when more than one claims it means
  removing that early return — which costs almost nothing, because the hook already reads and
  frontmatter-matches every plan before any claim comparison.

  **The figure three documents quote is not the hook's.** The roughly 150ms in the hook's own
  header, the charter's technology table and the README is the *runtime startup* number from
  the PowerShell-versus-node-versus-python comparison, not the hook's wall time.

  **Correcting all three is *not* in this change**, and the reason is the rule settled at
  charter version 8. One of the three is the charter's technology commitments table, and that
  rule lets a *register row recording an instrument* ride a change's gate — it does not let an
  edit to a commitment ride one. So the figure correction is either part of the amendment
  conversation or a small chain of its own. Named rather than left as an instruction with
  nobody holding it.

  **Measured on temporary repositories built for the purpose**, five runs each, worst case — a
  path no plan claims, which defeats every early exit:
  six plans in a flat tree is **306ms**, ten plans in the same flat tree is **317ms** — about
  **2.75ms per plan** — and six plans in a tree holding 900 directories is **1188ms**. Plan
  count is nearly free. The dominant term is **root discovery**, which globs the working tree
  at two depths to find the charter, and it scales with the size of the repository rather than
  with anything this change adds.

  **So the budget binds the term that dominates, and binds it relatively.** An absolute ceiling
  measured on a fixture with an empty tree would report about 317ms and pass forever no matter
  what happened to root discovery — an unfalsifiable criterion in a different costume, which is
  the exact phrase used above to justify writing a budget at all. Instead: **measure the
  shipped hook before and after this change, on one fixture with a realistic directory count in
  the low hundreds, and require no regression beyond the greater of 15% or 50ms**, median of
  five runs. **The fixture is 500 directories**, written here rather than left to the plan: the
  knee was measured here — 306ms flat against 1188ms at 900 — and this document's own argument
  is that a margin nobody wrote down is an unfalsifiable criterion in a different costume. A
  fixture chosen later at the bottom of "low hundreds" would satisfy the letter and, by this
  section's own admission, might not surface a regression in the term the budget binds. The margin is a figure rather than "small", because this document's own argument
  is that an unfalsifiable criterion is the defect and an unwritten margin is exactly that.
  A relative budget survives being run on another machine or a slower shared runner, where an
  absolute number either blocks unrelated work or is set so loose it cannot fail.

  **This is a verification measurement, not a suite test, and saying so avoids promising
  something impossible.** A test in the suite can only run the hook as it exists at that
  commit; the "before" arm is deleted by the very change that would add the test. So the
  before-and-after pair is taken **at plan time and pasted into `plan.md` as a named validation
  row** — the "before" arm has to be measured while the unmodified hook is still the one on
  disk, and recovering it afterwards would mean pulling an old file out of git, which this
  repository's standing rule makes an operation to avoid rather than a routine step. A
  validation row is what gives it a gate: the invariant requiring a plan to name its commands
  and what they must show is the only thing that would otherwise hold a budget that is
  deliberately not a test. It binds this change and not future ones. Binding future ones needs a committed baseline number,
  which is the absolute ceiling this paragraph already rejects. Named as a limitation rather
  than left as an implied test that could not exist.

  **This matters more than a benchmark detail, because this change moves root resolution into
  the extracted file.** It relocates the dominant cost.

  **Method, recorded because the original figure's never was:** invoke the shipped hook as an
  external process with an edit payload naming a path no plan claims — the worst case, since it
  defeats every early exit — and **a second arm on a skipped extension**, because the extraction
  changes that case too and the worst-case arm cannot see it. The hook returns on markdown and
  on three filenames *before* it locates anything, so **loading the parser must not cost
  anything on the paths the hook already returns on cheaply** — a property, not a placement:
  where the source goes is the spec's, and both file scope and inside the gate function are
  available for a script invoked with `-File`. The second arm is what makes the property
  falsifiable, because the worst-case arm cannot see it. Take the median of five runs on each. **No test in the suite**, for
  the reason given above: the only arm available at that commit is the "after" one, so a test
  could assert nothing but an absolute ceiling, and a test class that asserts nothing is a
  failure this repository already built an instrument to catch.
- **The suite's own runtime has no budget, and that is an accepted risk rather than an
  oversight.** Two new test classes land on a suite that already takes about six minutes and
  gates every commit, and one of them copies the engine plugin into a temporary tree per
  fixture. No number is set: the copy is small, and a suite budget is a framework-wide question
  rather than this change's. Named so the plan measures it rather than discovering it.
- **The script's cost in cross-plan mode is explicitly out of scope.** It goes from reading one
  plan to reading every plan and comparing the union pairwise, which grows with the number of
  chains. No budget is set for it, deliberately: it runs once at a plan approval rather than
  once per keystroke,
  and a repository with enough chains for it to matter is a problem nobody here has. Stated so
  it is a decision rather than an omission.
- **The extracted file is a shipped engine executable and inherits every obligation that
  carries.** Any new PowerShell under the plugins directory is picked up automatically by the
  check that every shipped script parses and by the instrument that scans shipped executables
  for stack names, where — unlike prose — every line counts.
- **Root resolution moves into the extracted file alongside the manifest grammar.** A charter
  invariant says a default root may be named but never used *instead of* resolving it, and the
  script has never needed a root at all — it takes one plan path today. After this change it
  needs one, and there would otherwise be three resolvers: the hook's, which is 47 lines of
  non-obvious behaviour with an ordered candidate list, globs at two depths, a directory
  filter and two fallbacks; and the script's new one.

  **And a second copy already ships, which an earlier count of this missed.** The scaffold
  executor carries its own `Get-ArtifactRoot` — same candidate list, same two-depth glob, same
  fallbacks — under a comment reading *"Lifted from the gate hook, and for the same reason"*.
  It duplicates the frontmatter reader too, and the two skip lists have **already diverged**:
  one holds seven directory names, the other holds `.git` alone. So this is not a divergence
  this change might create; it is one that exists.

  The parallel build is deliberately **not** counted: it takes the root as an argument with a
  default and never reads the charter, which a charter invariant has already ruled acceptable
  because its caller resolves and passes it, and that file says so out loud. Reopening a
  settled judgement in passing is not this change's to do. **Root
  resolution decides which plans are in the live set at all**, which is a larger input than the
  grammar: getting it wrong produces an empty or wrong comparison silently, the precise failure
  this change exists to remove. Shipping a third resolver while arguing at length that three
  parsers of one thing is the defect would be the change contradicting itself.
- **A new named check needs a row in the charter's register**, and under the rule settled at
  charter version 8 that row rides this change's approval gate — so this change's plan must
  declare `.sdlc/CHARTER.md` in its slice manifest.
- **This change writes six other chains' plans, and must declare every one.** Marking the five
  finished touches `init-scaffolds-solution`, `pack-promises-what-it-scaffolds`,
  `register-names-every-check`, `stack-dotnet-house-stack` and `verification-reports-options`;
  marking the sixth superseded touches `engine-substitutes-declared-tokens`. Editing another
  chain's artifact without declaring it is the same lapse this change exists to catch, and two
  of those six already claim the charter — though that one does **not** force the ordering,
  because the charter is under the artifact root and this design never compares it.

  **Measured, listed in full.** Of the files this change edits, four are claimed by another
  live plan: the schema reference (claimed by a superseded plan *and* a finished one), the
  engine plugin's version file, the README, and the test fixture — **four files, five claims.**
  The charter is claimed by two more but never reaches the comparison, being under the artifact
  root.

  **The verify skill and the verifier agent are *not* in this set**, though earlier counts put
  them there. Neither receives an edit: the verify skill was named only as a candidate for
  writing the finished value, which the closing step took out of scope, and the verifier agent
  is named only because its classification as parser-or-consumer is an open question — it reads
  the `files` lists and no status at all. A file this change does not write is not in its
  manifest.

  **The set grows under one amendment answer.** If a finished value becomes legal on all five
  artifact shapes, the engine skills branching on `status:` each gain a branch — the setup skill
  among them, claimed by two live plans, and the verify skill by one — and the scaffold executor
  gains a clause saying a charter is never finished, claimed by two more, one being the
  superseded plan. That answer makes it **seven files and ten claims**. The other three
  status-branching skills gain branches too and collide with nothing, so they add files and no
  claims.

  Both figures were measured against the six plans rather than recalled, which is the fourth
  time this count has been restated and the first time either number came from a command.
- **No second list beside a source of truth.** Whatever marks a plan finished is read from the
  plan itself, never kept in a separate index of live chains.
- **The engine names no stack**, which binds both the script and the hook, and every line of a
  shipped executable counts rather than only its prose.
- **The three existing parsers disagree on the exact path class this change must use, and the
  hook disagrees with itself.** The script and the hook both normalise *claimed* paths with
  `TrimStart('.', '/')`, which strips every leading dot and slash, so `.sdlc/CHARTER.md`
  becomes `sdlc/CHARTER.md`. The parallel build strips at most one optional dot followed by one
  slash, leaving that path untouched — under a comment claiming all three normalise
  identically. Worse, the hook normalises the *edited* path with `TrimStart('\', '/')`, with no
  dot, so a dot-prefixed claim can never match the edit that fulfils it.

  **Latent today, reachable after this change.** Every dot-prefixed path currently claimed has
  an extension the hook already skips, so the mismatch never runs. The existing test pins only
  `./src/A.cs` against `src/A.cs` and does not reach it. This change is required to declare
  `.sdlc/CHARTER.md` and to report claimed paths back to a person, which is exactly where a
  path that does not exist stops being harmless.

  **And a fourth disagreement, on case, which matters more here than it looks.** The script
  collects owners in a PowerShell hashtable, which compares keys case-insensitively. The hook
  collects claims in a hash set with the default comparer, which is case-sensitive, and the
  parallel build uses a case-sensitive map. On a framework the charter commits to Windows
  only, where the filesystem is case-insensitive, two chains claiming one file with different
  casing genuinely collide on disk, are caught by the single-plan script, and are invisible to
  both of the homes this change is choosing.

  **Making the hook case-insensitive also changes the gate that exists today, in the silencing
  direction.** A file claimed in one casing and edited in another currently produces an
  unplanned-scope warning; afterwards it will not. That is a fix rather than a regression,
  since the filesystem treats them as one file, but it is a change to shipped behaviour and not
  only to the new check.

  **Nothing pins it in either direction.** No test anywhere varies casing between a claim and
  an edit, so this behaviour change is unpinned on both sides.

  **A fifth disagreement, on where the manifest is read from at all.** The script matches an
  anchored `files:` and only once a slice header has been seen, so a `files:` key outside a
  slice is ignored. The hook tests whether the line *contains* `files:` anywhere, unanchored
  and slice-agnostic, and harvests quoted strings from it. The two can therefore derive
  different claim sets from one plan.

  **So both readers call one extracted parser** — settled at the intent gate rather than left to
  the spec, because two success criteria depend on it and a criterion cannot be settled after
  the design that satisfies it.

  **The argument for it stands on its own and does not need the charter's fourth-consumer
  line.** That line says a *fourth* consumer is the moment to stop and give them one parser; a
  fourth has not appeared, and extracting the two PowerShell readers leaves the JavaScript one
  outside, so the trigger has still not fired afterwards.
  The real argument is narrower and sufficient: these two readers
  demonstrably disagree in five ways, this change makes both of them answer the same question
  about the same file, and a check that reports different collisions depending on which reader
  ran is worse than no check. The charter also records this exact failure happening once
  before — two readers normalised a leading `./` and a third did not, so a plan could pass the
  check that fans out and fail the check that merely warns.

  **The cost, so it is rejected or accepted with the price visible.** The script is a
  command-style script with a mandatory parameter and bare `exit` calls, so it cannot be
  sourced as it stands and must be restructured to separate parsing from exiting. The new file
  is a shipped engine executable and inherits the parse check and the stack-name scan. It adds
  a silent-failure class of its own, below. And its signature becomes a contract the moment two
  shipped callers depend on it.

  **The alternative, if rejected:** leave both parsers where they are and add a test asserting
  they derive identical claim sets from the same plan. Cheaper now, and it is a second
  implementation watched by a comparison rather than one implementation — which is the pattern
  this repository has been bitten by four times, in a different costume.

  **The charter's paragraph about those parsers becomes false, and it splits into two edits of
  different kinds.** It reads: three hand-rolled parsers, *"None shares code with the others…
  They are kept in step by hand and by test. If a fourth consumer appears, that is the moment
  to stop and give them one parser."*

  **The whole paragraph goes to the amendment, not just its last sentence.** An earlier version
  of this split it — the factual half riding this change's gate as an enumeration, the trigger
  sentence going to the amendment — and that split does not survive the test applied elsewhere
  in this document to the known-and-unchecked paragraph. Both are charter prose that becomes
  false and that the version-8 rule does not name: that rule splits *constraints* from *rows
  recording an instrument*, and neither of these is a row. Routing two structurally identical
  cases opposite ways is how versions 4 through 7 each ended up making an unauthorised charter
  edit the build then needed — a red build, with nothing committable until it was resolved.

  **What becomes false, for the amendment to fix in one pass:** after the extraction two of the
  three parsers share code, so the count and the none-shares-code sentence stop describing the
  repository; *"kept in step by hand and by test"* is **already** false, since no test anywhere
  compares the three and the only pin is a single slice-manifest case exercising the script
  alone; and the fourth-consumer trigger is a constraint on future work.

  **A sixth statement of the same fact sits in a shipped spec, and it stays as written.**
  `init-scaffolds-solution/spec.md` records that the charter warns of three hand-rolled parsers
  and that a fourth is the moment to consolidate. That was true on the day it was approved and
  the spec is a record of that day — the same reasoning that leaves the validation row alone.
  Named here rather than left for the next reader to find and wonder about.

  **It batches rather than blocking**, since no design here depends on how that paragraph is
  reworded — unlike the two manifest-grammar questions, which do.

  **And it creates a new way for the gate to go permanently quiet, which needs its own named
  check.** The hook wraps everything in a catch that exits 0, deliberately, because a broken
  advisor on every keystroke is worse than a silent one. If it sources a parser file that has
  moved, fails to parse, or is missing from a half-updated plugin cache, the gate exits 0
  forever and says nothing — the "gate opened permanently" failure the charter's amendment log
  records as having cost fifteen tests. Nothing existing catches it: the check that hook
  commands point at real files reads the command strings in the hook manifest, and a
  dot-source is invisible to it. A change whose subject is *the framework says so without being
  asked* must not introduce a way for it to stop saying anything.

  **Its signature is a contract the moment two callers depend on it, and nothing here fixes
  one.** What it is called, what it returns, and what it does with a plan whose frontmatter is
  malformed or whose file list is block-style are all unsettled — and the last two are open
  questions below whose answers land *inside* this file. The reversibility statement in this
  document covers the frontmatter format and not this, which is the more reversibility-
  sensitive of the two: a frontmatter value can be migrated, a function two shipped callers
  source cannot be changed without changing both. *Owner: peter, at the spec gate — but note
  that two of its inputs are owned by the amendment, which comes first, so the signature cannot
  be fixed before that conversation happens.*

  **The parser normalises, and these are the properties the normalisation must have.** The
  rules that satisfy them are the spec's to choose and to number; what follows binds the spec,
  the algorithm does not.

  - **One rule, applied in one place**, so both readers derive the same claims from one plan.
  - **Lossless where the current strip is lossy.** `TrimStart('.', '/')` removes *every* leading
    dot and slash, mapping `.sdlc/CHARTER.md` and `.github/workflows/checks.yml` onto strings
    that match nothing on disk, so two different paths can collide with each other and a real
    path can collide with nothing. Whatever replaces it must not conflate distinct paths.
  - **Complete enough that two spellings of one file compare equal.** Separator style, a
    leading `./`, repeated or trailing slashes, `..` segments and absolute forms are all ways
    to write one path; missing any of them is a false negative in the mechanism being built.
  - **Based on the process working directory**, not on the plan's own directory and not on a
    "repository root", which nothing shipped computes.
  - **Case-insensitive**, because the charter commits to Windows and the filesystem is.
  - **Never silent on a claim it refuses.** The failure reaches the exit contract rather than
    dropping the claim from the set, since a dropped claim reports no collision — the silence
    this change exists to remove, reappearing at the normalisation step. Whether such a claim
    is *legal* is the amendment's, per the line drawn in the non-goals.

  **The hook is deliberately *not* held to this contract, and getting the mechanism right
  matters more than the principle.** The script refuses, because a person is standing at a gate
  waiting for an answer. The hook cannot, because nobody asked it anything and its standing
  rule is that a broken advisor on every keystroke is worse than a silent one.

  **But "the hook just carries on" is not what would happen, and an earlier version of this
  assumed it was.** That version reasoned from the hook's *own* frontmatter reader returning
  empty on failure — which stops being true the moment the hook sources the shared parser. The
  hook has no per-plan error containment: one catch wraps the whole run and exits 0. So a
  parser that throws on a claim resolving outside the repository would, on a single bad plan
  anywhere under the root, **silence the gate for every file in the repository, permanently and
  without a word** — the guarantee destroyed by a routine input, not by an unexpected one.

  **So the hook's caller contains each plan individually.** A plan the parser refuses drops out
  of *that* run's live set and the hook goes on comparing the rest. The claim is lost, which is
  a real cost and is why the script refuses instead; what is not lost is every other collision
  in the repository. Stated as a requirement on the caller rather than a mode on the parser,
  so the grammar stays in one place and the policy stays with the caller that wants it.

  **Nothing reads an exit code today, so the contract is half a prose change.** The plan skill
  says only to run the check and paste the real output; a model pasting output sees text, not a
  code. The distinction between "collision" and "could not read a plan" buys the only caller
  nothing until that prose is rewritten to branch on it — and nothing in the suite can hold
  prose to branching, which this document is scrupulous about naming elsewhere and should be
  here. The verification record says which half was proved and which was read.

  **And the recovery path, because refusing is only half a decision.** One unreadable plan
  anywhere under the root refuses *every* approval until it is fixed — including approvals for
  chains with nothing to do with it. That is the intended severity, since a plan silently
  dropping out of the live set is the failure being removed, but it needs a way out that is not
  "edit the other chain's artifact and hope": the report names the offending plan and the line,
  so the fix is bounded and obvious. There is deliberately **no flag to approve past it**. A
  switch that skips unreadable plans is a switch that restores the silence.

  **"Working directory" is deliberate, not loose.** Nothing shipped computes a repository root —
  there is no such concept in the engine, only the *artifact* root resolved from a charter. Using
  that phrase would import something that does not exist, so the base is the process working
  directory, inheriting the assumption the guarantee already records: the session starts at the
  repository root. The set it
  returns compares case-insensitively, so both callers inherit one answer rather than the
  hook's case-sensitive set disagreeing with the script's case-insensitive one.

  **Both directions matter.** Being too aggressive maps
  two different files onto one string and manufactures a collision; being too timid leaves two
  spellings of one file comparing unequal and misses a real one. The lossy direction is spelled
  out below; the list above is the other half.

  **Why the current stripping has to go.** `TrimStart('.', '/')` removes *every* leading dot
  and slash, which is lossy rather than merely inconsistent: `.sdlc/CHARTER.md` becomes
  `sdlc/CHARTER.md` and `.github/workflows/checks.yml` becomes `github/workflows/checks.yml`,
  so two genuinely different paths can map onto one string and the check reports a collision
  that does not exist — the "false collision on the day it lands" that the problem statement
  says gets a check switched off.

  **And that is why the artifact-root exclusion is the caller's, applied after normalisation.**
  It could instead be applied to the declared path before normalising, on the grounds
  that the root prefix does not survive the strip. Under the rule above it does survive:
  `.sdlc/CHARTER.md` stays `.sdlc/CHARTER.md`, so the caller can resolve the root and drop what
  is under it from the normalised set. The exclusion stays out of the parser because it is a
  policy only one caller wants, not because the parser destroys the information.

  **The stripping is lossy as well as inconsistent, so it can manufacture a collision rather
  than only miss one.** Removing every leading dot and slash maps two genuinely different paths
  onto one string, and this repository's claim set already contains `sdlc/CHARTER.md` — a path
  that does not exist. Harmless while nothing reported collisions; not harmless once reporting
  them is the entire point.

**Reversibility, stated plainly because this commits to a persisted format.** Whatever marks a
plan finished lives in `plan.md` frontmatter, which is one of the shapes the charter freezes,
in every repository that adopts this framework, read by two shipped readers today and three
after this change — the gate hook, the parallel build, and the disjointness script, which reads
no status at all until this makes it one.

**The mode's exit contract is the other thing that becomes hard to move.** Exit 0, 1 and 2, and
the line format naming a path and its claimants, are what the plan skill branches on and what a
person reads at the gate. Once a skill branches on it, changing it changes both — the same
argument this section makes about the parser signature, applied to the thing a human sees.

**The worst consequence is the reverse direction:** a
repository whose plans carry the new finished value, opened with an *older* plugin, has zero
approved plans by that hook's reading — so with the blocking switch set, the old hook refuses
every code edit in an updated repository. Skew in that direction bricks the repo rather than
merely blocking a build.

Two further consequences, and neither is hypothetical: a plan written by today's plugin reads
`approved` forever and would be live forever under the new rule, and a plan written by a newer
plugin is blocked by an older parallel build, which gates on the literal string. Plugin
versions are bumped but never tagged, and a second repository is about to be initialised, so
version skew is a thing that will happen rather than a thing that might.

## Non-goals

- **Not a lock and not a reservation.** Nobody takes a claim out and nothing needs releasing.
  A claim is a side effect of a live approved plan existing. Locks go stale when a session
  dies and then need arbitration to break, which was the argument against them.
- **Not a record of who is working on what.** No session identity, no person, no view of
  activity over time. That is the project-management system this framework will never be.
- **Not a queue.** It detects and refuses loudly; it never schedules, serialises, or makes one
  session wait for another. Two sessions may run at once — the point is that they find out
  immediately when they should not both be touching one file.
- **Not the other collision surfaces.** Six were measured beside this one: the git index and
  branch, build output, the charter's version and register, the open-items and direction
  documents, and a shipped spec's forward pointers. The git index is the worst of them. All
  stay handled by practice — different slugs, never staging everything, one suite run at a
  time — and none is in scope here.
- **Not the artifact files — and this one is forced, not chosen.** Nothing under the artifact
  root is compared across plans. The reason is that the rule settled at charter version 8
  obliges any change adding a named check to declare the charter in its slice manifest, so two
  such chains *must* both claim it while both are live. Comparing artifacts would therefore
  report a collision by construction on every pair of check-adding chains — and since a
  blocking finding becoming a named check is this repository's own self-improvement loop, that
  is most changes. With nothing committed on a red build, the effect would be a rule that only
  one check-adding chain may be live at a time, which is the opposite of the purpose.

  **What protects the artifacts instead — late detection, not serialisation.** The direction
  says charter amendments serialise by construction because an amendment bumps a version and
  git refuses the second push. That is true and it is weaker than the word serialise suggests:
  it catches the clash at *push* time, after both amendments are fully written, and two
  sessions in separate worktrees — the direction's own recommended mitigation, cited elsewhere
  in this document — will both write version 9 and find out afterwards. The charter also has no
  edit-time protection at all: the hook skips it twice over, once for being markdown and again
  for sitting under the root. **So the charter is a known uncovered surface**, alongside the
  other six, rather than a covered one. The remaining artifact collisions are prose documents
  that merge and are reviewed, which is the cheap kind of conflict. Code is the expensive kind,
  and that is what this compares.

  The hook separately skips markdown and json so the chain stays editable, and that is not
  being reopened either.
- **No slice manifest is rewritten on a finished plan.** Marking the five moves a status line
  and nothing else. What those plans claimed is the historical record of what that change
  touched, and editing it would falsify the record to make a new check pass.
- **No *automatic* migration of chains in other repositories.** Nothing rewrites a plan behind
  anyone's back, and no plan is rewritten as a side effect of installing a new plugin version.
  A person runs the marking, once, deliberately — see the outcome above.
- **Not cross-branch and not cross-worktree — and the two mitigations are mutually exclusive,
  which is worth knowing before choosing one.** The direction's advice is narrower than it is
  often paraphrased: *do not run the full suite in two sessions at once, or give one a
  worktree*, which is about suite runs. Taken as a general practice, a worktree per session
  removes the very condition this change's guarantee depends on — the hook only sees a
  collision when both live plans are on disk in one working tree. So worktrees trade this
  protection for the build-output one.
  Every mechanism named here reads the plans on
  disk in one working tree. Two sessions on separate branches, or in separate worktrees, each
  holding an approved plan, see nothing of each other. That is a real hole and it is the same
  hole the direction already mitigates by recommending a worktree per session — this change
  narrows the file collision, not the branch one, which the direction measured as the worst of
  the seven surfaces.
- **Not atomic, and not mutual exclusion.** This is detection at two moments, not a lock.
  Nothing serialises two approvals: both sessions can read the plan set, both pass, and both
  write `approved`.

  **And for the artifact collisions it may be found by nothing, ever.** The plan skill writes
  the plan as a draft, runs the check, and only approves at its gate — so two sessions both
  past the check and neither past the gate each see the other as a draft, and a draft claims
  nothing. Both pass. Afterwards the hook is the only watcher, and it returns on markdown and
  json before reading any claim, which is five of the eight collisions measured here. With no
  third approval to trip over them, those are reported by nobody. **Where the cross-plan
  invocation sits — before the gate or at it — sets the width of that window**, and it is an
  open question with an owner below rather than something the spec inherits. Charter
  amendments serialise because a version bump makes git refuse the second push; plan approvals
  have no such property and this change does not give them one.
- **The cross-plan check does not become a fourth manifest parser.** Three already read the
  `files:` manifest and the charter names a fourth as the moment to stop and give them one.
  This reuses an existing parser rather than adding to the count.
- **`Assert-SliceManifest.ps1` keeps its single-plan *interface*, and its path semantics change
  deliberately.** A caller passing `-Plan` alone gets the same invocation, the same output
  format and the same exit codes, and the test helper that invokes it that way does not move.

  **What does change, because an earlier version of this promised otherwise and could not:**
  the paths it compares. The new normalisation is lossless on a leading dot, so two slices
  claiming `.sdlc/CHARTER.md` and `sdlc/CHARTER.md` stop collapsing onto one string and stop
  reporting an overlap that is not real; a claim resolving outside the repository becomes an
  error rather than silence. Promising "exactly today's behaviour" would have meant keeping the
  lossy strip on the single-plan path — two parsers with two answers, which is precisely what
  the extraction exists to end.

  **What binds, stated as a property: the same fault reports the same way in both modes.** An
  earlier version of this settled the codes concretely here while a paragraph four hundred lines
  above delegated exit codes to the spec — the two could not both stand.

  **The context the spec needs, since it is easy to get wrong:** exit 1 today does not mean "an
  overlap". The script exits 1 for seven distinct conditions — plan not found, no frontmatter,
  no slices block, a slice owning nothing, an overlap, an unresolved dependency, a dependency
  cycle — so it means "any failure". A scheme that distinguishes a planning fault the person
  can fix in the plan in front of them from "a plan could not be read", which is a different
  repair in a different file, therefore **re-cuts today's failure codes rather than adding to
  them**. That re-cut is what this document expects and the spec may choose otherwise, provided
  the property holds and a clean plan still exits 0. What is *not* negotiable is assigning one
  meaning in cross-plan mode and a different one to the same fault in single-plan mode.

  **The cross-plan comparison is a new mode, not a change to that one.** The plan skill gains a
  *second* invocation for it, so the approval-time refusal in the first success criterion has a
  call site.

  **What its contract must achieve**, stated as properties because the output is what a person
  reads at a gate and an approved intent is the wrong artifact to freeze a format in. The codes
  and the wording are the spec's; these bind it:

  - **Three outcomes are distinguishable, not two.** A clean comparison, a collision, and "a
    plan could not be read or carries a claim the parser refuses" are three different repairs
    in three different places. Folding the third into either of the others restores the silence
    this change exists to remove.
  - **A pass that compared nothing is distinguishable from a pass that found nothing.** With
    finished plans excluded, those look identical unless the output says which.
  - **The caller does not open the gate on either failure.** Reporting the unreadable case and
    continuing undoes the only reason it is separate.
  - **A report names the path and every live plan claiming it**, by one identifier used
    consistently. The hook already prints the chain directory name; plans also carry a `slug:`
    key that the script never reads and nothing holds to its directory, so two shipped readers
    would otherwise print names derived two different ways from one file.
  - **The comparison unit is the plan, not the slice.** Each live plan's slices are unioned
    first, so an overlap *inside* another plan stays the single-plan check's business rather
    than surfacing as a cross-chain collision. This one is a property rather than a detail: it
    decides what the check means.

  **Consequence for the ordering.** The invocation is written down in five places, listed in
  the affected systems above, and all five go stale together. Two of them are claimed by other
  chains — the README by a finished plan, the schema reference by a superseded one — so they
  join the marking-comes-first ordering. Two others sit in files this change protects for other
  reasons: the script's own example block, and a validation row inside one of the five plans
  being marked finished.

  **That fifth one is a genuine conflict and it is resolved here rather than noticed.** The row
  records a command that was actually run at approval time. Rewriting it to match a new
  invocation would falsify a historical record to make a present-day rule tidy — which is the
  same reasoning as the non-goal forbidding any rewrite of a finished plan's manifest. **So it
  stays as written**, and "all five go stale together" is wrong: four move, one is a record of
  what happened and is left alone. A verification record that stops matching today's command is
  not stale; it is accurate about a different day.
- **A draft plan claims nothing** — other than the plan being checked at its own approval gate,
  which contributes its claims whatever its status says. It carries a full slice manifest and
  no agreement behind it, and nothing downstream is produced from a draft.
- **The hook's existing skip rules for edited paths are not changed.** Its segment-equality test
  for the artifact root stays as it is, wrong edge case included — a repository rooted at
  `docs/sdlc` will still skip `src/sdlc/Thing.cs`. So will the fact that `bin/`, `obj/` and
  `node_modules/` are *not* skipped on edits, despite build output being named as a collision
  surface. Both are real, both are pre-existing, and fixing either is a different change. The
  new exclusion this change adds applies to *declared* paths and leaves those alone.
- **The scaffold executor keeps its own copy of root resolution, and that is a known gap rather
  than an oversight.** The extraction reaches the hook and the script — the two readers this
  change makes answer the same question — and stops there. A later fix to the extracted
  resolver will not reach the executor, which is the same divergence this change argues against
  at length. Accepted because bringing a third caller onto a brand-new signature contract, in
  the file with the most tests behind it, is real growth in a change already carrying a lot.
  Named here so the next person finds a decision rather than an omission.
- **The parallel build's wave loop is not fixed here, whether or not the file is touched.**
  Under the reused-status answer it needs no edit and is not in the manifest; under an additive
  marker it is edited, and the non-terminating loop then sits some forty-six lines above the edit site.
  Touching the file is not licence to fix it: that defect is its own open item and needs a
  harness that executes a workflow body, which does not exist. Named in this section because
  Non-goals is what an implementer reads for scope.
- **The hook's tool matcher is not widened.** It is registered on edit, write and notebook
  edit, which is why a `git mv` or a formatter never reaches it — stated above as a bound on
  the guarantee. That bound reads like a defect to anyone who has just internalised it, and the
  matcher sits three lines from the description this change rewrites in the same small file.
  Widening the hook's reach is a different change with its own cost.
- **The router's competing notion of an active chain is not reconciled here.** It says that a
  chain with no plan yet is probably the current one; this change introduces a status-derived
  notion of live. Leaving two definitions is the second-list pattern, and fixing it is a
  different change — but that file is **not** edited here, so it is out of scope rather than
  undeclared.
- **`spec.md` and `verification.md` statuses are never read.** The check reads plans. If the
  amendment makes a finished value legal on all five artifact shapes, that does not make those
  two inputs to this mechanism.
- **The extracted parser is not a supported extension point.** It ships under the engine's
  scripts directory, so a stack pack or a downstream repository *can* source it — and if one
  does, its signature is frozen far harder than "two shipped callers depend on it". Nothing
  here invites that, and nothing here promises it will keep working for anyone who tries.
- **The extracted parser serves both callers without either re-deriving the manifest.** The
  single-plan check owns files per slice, the cross-plan check compares whole plans, and only a
  return carrying the finer grain can serve both — but what that return *looks like* is the
  spec's, not this document's. The `requirements:` and `depends_on:` keys stay outside the
  contract.

  **But the slice walk itself cannot stay outside it**, and saying otherwise would make the
  extraction unify nothing. The fifth disagreement above is precisely about *where a slice
  begins*: the script honours a `files:` line only after a slice header, the hook matches the
  substring anywhere. If the script keeps its own frontmatter walk to build the dependency
  graph, it keeps a second notion of slice boundaries and the two can still derive different
  sets from one plan — the exact defect the extraction exists to remove, surviving it.
- **Nothing rewrites a plan automatically.** No status line moves on plugin installation, on
  repository setup, or as a side effect of any other command. The five are marked by a person,
  in a reviewable commit, and no tool ships that could do it unattended.
- **The check never reads `intent.md` to infer a chain's state.** It reads plans. A chain's
  intent may say parked, blocked or anything else; the plan's own status governs its claims.
- **No chain directory is renamed, archived or moved** when its plan is marked finished. Only
  the status line moves.
- **Nothing in this change stages or commits on anyone's behalf.** The non-goals above name the
  git index as the worst of the seven collision surfaces and staging everything as the specific
  failure, so building any automatic staging into the fix for a lesser surface would be the
  wrong trade.
- **A claim is never checked against the filesystem.** Whether the path exists, still exists,
  or was renamed by some other change is out of scope: this compares declarations to
  declarations. The consequence accepted rather than hidden: claims go stale silently when a
  file is renamed or deleted.

  **Not to be confused with the two corrupted claims described above**, which an earlier
  version of this document repeatedly got backwards. Every path declared in every plan exists
  on disk. What does not exist is `sdlc/CHARTER.md` and `github/workflows/checks.yml` — strings
  the parsers manufacture at read time by stripping every leading dot. **No plan file is wrong;
  the parsers are.** The distinction matters because the first reading sends someone to correct
  two artifacts that are already correct.
- **The line between what the amendment owns and what this document may settle**, because two
  decisions of apparently the same kind are routed opposite ways and the rule was implicit.
  **What a manifest entry may legally *be* is the frozen shape** and goes to the amendment: a
  path against a directory or a glob, and whether a claim resolving outside the repository is
  legal — both make something valid today invalid tomorrow, which is a migration in every
  adopting repository. **How two entries are *compared* is parser behaviour** and is settled
  here: case-insensitivity, and resolving `..` against the working directory rather than the
  plan's own. Those change which claims are found equal; they make nothing illegal.
- **A manifest entry is a file path, never a directory or a glob — *proposed*, and settled in
  the amendment.** Every parser does exact string equality after normalising, so one plan
  claiming a directory and another claiming a file inside it collide on disk and not in the
  check: a false negative in the very mechanism being built. The schema shows only flow-style
  file lists by example and forbids nothing, so declaring it moves a frozen shape and cannot be
  decided here. Written as a non-goal because that is the answer this change wants; marked
  provisional because the prerequisite section says the amendment owns it

- **The marking touches `plan.md` and nothing else.** If the amendment makes a finished value
  legal on all five artifact shapes, nothing else here would stop the same pass from also
  marking intents, specs and verification records on those chains. It does not: six status
  lines move and no other artifact is edited.
- **`parallel-build.js` gains no filesystem access and no cross-plan check.** An earlier shape
  of this change had it dispatch an agent to run the script at fan-out; that was dropped once
  the wave loop turned out never to terminate. The build is not a home here.
- **This is not status tracking.** The permanent non-goal about never becoming a
  project-management system forbids tracking tickets, owning a board and reporting status. A
  plan's status is a parser input that decides whether a manifest is live — read at the moment
  of a check and never accumulated, reported over time, or shown on a board. The distinction is
  worth one sentence precisely because a lifecycle value meaning "finished" sits near that line.

## Open questions

- **What marks a plan finished, and who moves it there?** Settled in the charter amendment that
  precedes the spec, per the prerequisite section above — not here and not by the Design stage,
  because it moves a frozen shape. *Owner: peter, in the amendment conversation.*
- **How far does fixing the path normalisation reach?** Both cross-plan readers are PowerShell —
  the hook, and the script the plan skill runs at approval — so the disagreement with the parallel
  build's JavaScript is not on this change's critical path. The hook's disagreement *with
  itself* is: it strips leading dots from claimed paths and not from the edited path, so a
  dot-prefixed claim can never match the edit that fulfils it, and this change is required to
  declare exactly such a path. Fixing that is not optional.

  **The JavaScript is off the critical path now that fan-out is not a home**, but its comment
  claiming all three parsers normalise identically is false today and stays false. Whether
  reconciling it belongs here is open and has a cost only the owner can accept. *Owner: peter,
  at the spec gate, with the charter's fourth-consumer line as the test.*
- **Warning volume is not in scope, and that is a non-goal rather than an open question.**
  Decided above for the blocking switch — refusing every edit between chains is accepted, and
  the answer for an owner who has it set is "unset it, or start a chain". The *warning* half —
  code files that only finished plans claimed beginning to draw unplanned-scope warnings — is
  correct behaviour by the gate's own definition: those edits genuinely are outside any live
  plan. Left as an open question it would be the one thing that fails if nobody answers it,
  which is not what an open question is for. If the volume turns out to be intolerable in
  practice, softening it is a change with its own evidence.

  **It does not make the second success criterion contingent**, which an earlier version of
  this claimed while the criteria section called that criterion non-contingent. A finished plan
  claiming nothing holds either way; what is open is only whether the resulting warning volume
  is tolerable or wants a separate softening. *Owner: peter, at the spec gate, on measured
  output rather than argument.*
- **Does the cross-plan check run before the plan skill's approval gate, or at it?** The skill
  writes the plan as a draft, runs the single-plan check, and approves at a later step. Running
  the cross-plan check at the earlier point leaves the whole approval window unguarded — two
  sessions both past it and neither past the gate each see the other as a draft, and a draft
  claims nothing, so both pass. Running it at the gate closes that window and moves the check
  to a step that does not exist yet. *Owner: peter, at the spec gate.*
- **What happens to a plan whose frontmatter is malformed or missing?** Every reader treats it
  as having no frontmatter, so the plan drops out of the live set and its claims vanish without
  a word — no collision reported. That is the same silent-skip class as the quoted status line
  and the block-style manifest below, and the framework already has a named check for exactly
  this failure on its own definition files and none for chain artifacts. *Owner: peter, at the
  spec gate.*
- **Must a status line be spelled exactly one way?** Every reader matches the bare word — the
  hook requires `status:` followed by `approved` and nothing else on the line — so a plan
  written `status: "approved"` is silently not live, claims nothing, and reports no collision.
  The schema shows the unquoted form by example and forbids the quoted one nowhere. This is the
  same silent-skip class as the two questions below it, and it lands on the hand-marking as
  well: what gets written has to match what every reader matches on, exactly. *Owner: peter, in
  the amendment conversation, since it is the same frozen shape.*
- **Are chain directories flat? — answered here, because the report's identifier depends on
  it.** They are. The hook discovers plans exactly one level under the artifact root, nothing
  in the schema says otherwise, and the collision line names claimants by chain directory name.
  If chains could nest, `a/b/plan.md` and `c/b/plan.md` would both print as `b` and the report
  would name two chains identically — while the discovery glob would find neither. Leaving this
  at the spec gate would have settled an identifier before settling what makes it unique, which
  is the rule this document applies elsewhere.

  **And nothing detects a violation, including the exit contract.** A plan at a second level is
  never *discovered*, so it is not live, claims nothing, and produces neither a collision nor
  the unreadable-plan exit — quieter than the case that contract was written to remove, because
  there is no file to report. Flatness is a property the check relies on; it is held by the
  schema saying so and by nothing else. Named as a gap rather than presented as safe.
- **Do both parsers only read a single-line `files:` manifest, and does that matter here?**
  Both PowerShell readers extract quoted strings from the one line containing `files:`, so a
  plan written with a block-style YAML list claims nothing. The single-plan script catches that
  as an empty manifest; the hook does not, and neither would a cross-plan comparison — it would
  simply see a plan that claims no files and report no collision. The schema shows only the
  flow style by example and forbids nothing. Either answer — rejecting block style or reading
  it — moves the manifest shape the charter freezes, so it cannot be settled at the spec gate.
  *Owner: peter, in the amendment conversation, alongside the status value.*
- **Do the five finished plans need any judgement, or is marking them mechanical?** They are
  being edited after the fact, which is the thing this repository is careful about.
  *Owner: peter, at the plan gate.*
- **Is `verifier.md` already the fourth consumer of the manifest, and has the charter's trigger
  therefore already fired?** It reads the `files` lists without implementing the grammar, which
  is why this intent calls it a consumer and not a parser. If the owner reads it the other way,
  the charter's instruction to stop and give them one parser applies now, and this change
  inherits that rather than adding to the count. *Owner: peter, at the spec gate.*
- **Is the parser extracted? — settled at the intent gate: yes.** It sat here as an open
  question for one round, which was wrong by this document's own rule: two success criteria
  depend on the answer, and a criterion cannot be settled after the design that satisfies it.
  The owner chose extraction on the argument that the two readers demonstrably disagree in five
  ways and this change makes both answer the same question about the same file. The alternative
  considered and rejected was leaving both in place with a test asserting they agree, which is
  two implementations watched by a comparison rather than one implementation.
- **Does the disjointness invariant widen, or is it only newly instrumented?** This intent
  reads it as instrumentation, on the grounds that the invariant never said "within one plan".
  Read the other way it is a constraint widening and takes its own approval conversation,
  which would make the prerequisite two amendments rather than one. *Owner: peter, in the
  amendment conversation, since that is where the cost lands either way.*
- **What stops the sixth reader?** The failure class here is a reader gating on the literal
  string `approved` that nobody teaches about the new value. It has demonstrated itself three
  times inside this one document, on a reader that ships and has a register row behind it. The
  register gains a row for the collision check and none for this. Whether a named check can
  enumerate the readers — and what it reads to do so — is unsettled. *Owner: peter, at the spec
  gate.*
- **Does this step still belong where the direction put it? — answered at the intent gate on
  2026-09-20: yes.** The question was real rather than hygienic. The direction places this
  first, with *"nothing else starts until this does"*, and describes it as *"no new mechanism;
  widen two that exist"*. As scoped here it adds a lifecycle mechanism on a frozen shape, a
  charter amendment that blocks the spec, a new shipped engine executable and two register
  rows. **The owner's answer: the estimate was wrong, not the ordering.** Until two approved
  plans can be compared, two lines of work cannot safely run at once and everything else is
  serialised behind one session's attention — which is exactly why it was placed first, and
  that reason is untouched by the work costing more than the direction assumed.

  **What follows is a `DIRECTION.md` edit, not a note.** Both stale lines are rewritten: the
  scope line to describe what this actually is, and the prerequisite line to say the first
  amendment landed at charter version 8 while a second blocks the spec. *Owner: peter, when
  the amendment conversation settles.*

## What changed across drafts, and why it is here

This intent was attacked repeatedly in fresh context — the count is deliberately not stated, because a hand-maintained tally in a document that invokes the second-list rule six times is exactly that failure. Most findings changed the design, and
those changes are simply the document above. A handful were **factual claims that were wrong**,
and they are collected here rather than annotated in place: a correction sitting next to an
instruction is a thing a builder can act on by mistake, and this document is meant to be
followable by someone who was not present.

- **The hook's cost.** Said first to be dominated by file reading, then to be ~200ms of file
  work on top of startup. Neither. Plan count costs ~2.75ms each; root discovery globbing the
  working tree dominates, and scales with repository size rather than with anything here.
**These entries record only that a figure was wrong and how it was got wrong. The current
figures live once, in the sections that use them** — repeating them here would be a second list
beside a source of truth with nothing comparing the two, which is the rule this document
invokes six times.

- **The number of files this change shares with other plans.** Restated four times from memory
  — three, four, "seven" above a list of six, then six and seven counting two files this change
  never edits — before anyone ran a command. See Constraints.
- **The script's invocation sites.** Said two, then three, before they were counted.
- **Readers gating on the literal `approved`.** Overcounted twice: the disjointness script
  reads no status at all, and the scaffold executor reads the charter's rather than a plan's.
- **Engine skills branching on `status:` in prose.** Miscounted, and named a skill that does
  not exist.
- **Tests pinning the hook's case sensitivity.** Said two existed; none does. Inherited from a
  review note and written down without opening the files — the lapse the direction records as
  having produced four false entries across three rounds, repeated here.
- **The state of `engine-substitutes-declared-tokens`.** Called parked. Its plan is superseded
  without having been built, while its intent carries a parked banner — which is why this
  document now states that a plan's own status governs its claims.
- **Whether the superseded rule frees this change's own collisions.** Said it frees none. Both
  rules are needed and neither is sufficient, because the schema file is claimed by one
  superseded plan and one finished one.
- **The charter's fourth-consumer line.** Cited as authorising the extracted parser; it does
  not, since a fourth consumer has not appeared. The argument that does stand is in Constraints.
- **The script's existing exit-code contract.** Described as "1 for an overlap"; exit 1 covers
  seven distinct failures and means "any failure".
- **The hook's behaviour on a plan the parser refuses.** Assumed to carry on, reasoning from a
  frontmatter reader the hook stops using the moment it sources the shared parser.

Two design decisions were also reversed on evidence, and are recorded because the reasoning is
not obvious from the result: a bulk-marking tool was dropped once its user population was
measured at one repository, once; and a check reading this repository's live chain directory was
dropped once it became clear it would be green by construction and could not be mutation-tested
without writing into the live tree.
