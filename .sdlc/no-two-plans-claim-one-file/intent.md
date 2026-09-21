---
slug: no-two-plans-claim-one-file
title: No two live plans claim the same file
status: draft
tier: feature
created: 2026-09-20
owner: peter
---

# intent — no two plans claim one file

> **Unblocked on 2026-09-21 by charter versions 9 and 10**, which settled the twelve questions this
> chain could not be specified without: the mechanism that marks a plan finished, how far it
> reaches, whether it is one-way, who writes it, and three rules of the frontmatter grammar.
> Those are decided there and are not restated here. Version 10 repaired two defects in 9 that
> the first interrogation of this document found: the disjointness carve-out for the artifact
> root, and three grammar rules that claimed instruments which did not exist.
>
> **This document was rewritten against those answers, and shortened.** Its first draft ran to
> 1680 lines and held the design — where the extracted parser lives, how paths normalise, what
> the exit codes are. The schema says an intent contains no implementation, and anything
> approved at this gate cannot be superseded downstream without reopening it. What remains is
> what must be *true*; how it is achieved is the spec's. The earlier draft is in git at
> `93175c5` if the reasoning behind a decision is wanted.

## Problem

Four statements, each measured on 2026-09-20 rather than assumed.

**Nothing compares two plans.** `Assert-SliceManifest.ps1` proves the slices inside one plan
pairwise disjoint, and the plan skill runs it at approval. The build does not run it — it
re-implements disjointness in its own JavaScript, having no filesystem access — so the same
rule is written twice in two languages, which is how the two came to normalise paths
differently. Either way both operate on a single plan. **Two separately approved plans claiming
one file is not a state anything in the framework can observe**, and it arrives as a merge
conflict discovered after both implementers have finished: the most expensive moment to find
it, because the work is done and some of it must be thrown away.

**Until version 9 the artifacts could not tell a live plan from a finished one.** `approved`
was where a plan's status came to rest; `superseded` was declared and honoured by nothing. Six
plans carried `approved`, five of them done and a sixth replaced without ever being built.
Comparing them the obvious way reported **35 file claims over 26 distinct paths, with eight
paths claimed by more than one chain** and the stack pack's skill claimed by three — and not
one of those eight was a live collision. A check reporting eight false collisions on the day it
lands is a check that gets switched off. The amendment settles the marker; this change uses it.

**The cost of the gap, stated as what this change actually removes.** Two sessions cannot
safely run at once today, and this removes exactly one reason: two approved plans silently
claiming one implementation file. It does not remove the others — the direction measured the
git index as the worst, where staging everything sweeps up another session's half-finished
work — and it does nothing for two concurrent *intents*, which claim no implementation files
because neither has a design yet. The honest claim is not "two sessions can now run at once";
it is "the file collision stops being invisible, and the rest stay handled by practice".

**The gate hook is narrower than the direction assumed, in three ways.** It already reads every
approved plan under the artifact root rather than only the current chain's — but permissively,
going quiet the moment *any* plan claims the edited file, and it has no notion of which chain
an edit belongs to. It skips `.md`, `.txt`, `.json`, `.yml`, `.yaml` and `.toml` by design so
artifacts stay editable, and five of the eight measured collisions are markdown or json. And it
is registered on three tools — edit, write and notebook edit — so a file changed by a `git mv`,
a shell redirect or a formatter never reaches it at all.

## Affected users and systems

Every file below was opened while writing this, not inferred from a search.

- **`plugins/sdlc-core/scripts/Assert-SliceManifest.ps1`** — takes one mandatory `-Plan`, parses
  frontmatter with a deliberate line-oriented parse rather than a YAML dependency, proves slices
  disjoint, resolves `depends_on`, prints the wave plan. Reads no status at all today.
  Everything it does operates inside a single plan.
- **`plugins/sdlc-core/hooks/sdlc_gate.ps1`** — advisory by default, `SDLC_GATE=block`
  makes it refuse. Collects plans whose frontmatter reads exactly `status: approved`, matches the
  edited path against their claims, warns in one of two states. Returns at the *first* claiming
  plan, so it is structurally blind to a second. Swallows every error and exits 0 on purpose,
  because a broken advisor on every keystroke is worse than a silent one.
- **`plugins/sdlc-core/workflows/parallel-build.js`** — gates on the literal value `approved`.
  **Its status gate is unreachable:** the wave loop forty-six lines above filters slice objects
  against a string id, so the remaining set never shrinks and no execution reaches the gate.
  Recorded as an open item; not this change's to fix.
- **`plugins/sdlc-core/skills/sdlc-router/references/artifact-schemas.md`** — carries the plan
  shape, including the `finished:` field and the grammar rules settled at version 9.
- **`plugins/sdlc-core/skills/plan/SKILL.md`** — runs the disjointness check at a step where the
  plan is still `draft`, and approves at a later one. Gains the cross-plan invocation.
  **`Assert-SliceManifest.ps1 -Plan <path>` alone must keep working exactly as it does today**,
  including its output and its exit codes: it is invoked that way from this shipped skill, from
  the test helper, and described that way in the README, in a plugin installed per machine. The
  cross-plan comparison is a new mode beside it, never a change to that invocation.
- **`plugins/sdlc-core/skills/verify/SKILL.md`** — gains the write of `finished:` on the **SHIP**
  branch of its gate, at feature tier. Not at the stage: it runs on both verdicts.
- **`plugins/sdlc-core/.claude-plugin/plugin.json`** — the engine plugin's version, bumped
  because this change ships hook and script code and plugins are cached per version. Claimed by
  a finished plan, and one of the four files in the collision set below.
- **`tests/AiSdlc.Tests/SliceManifestTests.cs` and `GateHookTests.cs`** — the existing instruments
  for the two mechanisms being widened.
- **`tests/AiSdlc.Tests/TempRepo.cs` and `Repo.cs`** — the fixture, which writes one chain with
  one plan and cannot express two colliding plans. Shared by three suites, so it sits in exactly
  one slice. `Repo.cs` resolves shipped scripts by walking up to the plugin directory.
- **`.sdlc/CHARTER.md`** — takes the register rows for the new checks and the known-and-unchecked
  entries. Declared in the slice manifest, per the rule settled at version 8.
- **`README.md`** — its gate-hook prose cites `scripts/Test-SdlcGate.ps1`, a file that has never
  existed, four lines from a paragraph this change edits.
- **The five finished plans and the one superseded plan** — `init-scaffolds-solution`,
  `pack-promises-what-it-scaffolds`, `register-names-every-check`, `stack-dotnet-house-stack`,
  `verification-reports-options`, and `engine-substitutes-declared-tokens` — the last superseded
  in fact but not yet in its frontmatter, which still reads `approved`, and which this change
  corrects.
- **The owner**, who cannot currently run two sessions without hand-checking for overlap.

**Four of the files this change edits are claimed by another live plan today** — the schema
reference (by a superseded plan *and* a finished one), the engine plugin's version file, the
README, and the test fixture: **four files, five claims**, measured rather than recalled. All
five are freed by the marking, which is why the marking goes first.

## Proposed outcome

A file claimed by a live plan is claimed by exactly one, and the framework says so without
being asked.

- **Two live plans claiming one file is reported**, naming the file and every live plan claiming
  it. At plan approval it refuses; on a code edit it warns.
- **The gate hook is the guarantee** — the only mechanism that fires on its own, in any
  repository that merely installs the plugin. Its bounds are real and stated in the problem
  above: three tools, code files only, and only the path being edited.
- **Plan approval is a catch, not a guarantee.** It runs by an agent following an instruction,
  and that cannot be promised.
- **Fan-out is not covered**, because the build has never run.
- **The six plans that are done or replaced say so**, so the check's first real run is true.
- **And it decays from there until a closing step exists.** Only feature chains get `finished:`
  written for them; four of the five shipped here were task tier and pass through no closing
  stage at all, so their successors accumulate as live and the collision report slowly fills
  with chains that are finished. That is recorded as item 4 in `.sdlc/OPEN.md`, with the closing
  step as its fix, and it belongs here too because the intent is what a fresh session reads.
- **Between chains, with no live plan, the hook warns on every code edit** — the gate working as
  designed, masked until now by plans that could not be retired. Recorded at charter version 9,
  including that the blocking switch then refuses every edit until a chain is started.

## Success criteria

Each states what breaks if the change is not made, because a criterion that still passes with
the headline change skipped is not a criterion.

**These are properties. The algorithms, return shapes, exit codes, message wording and file
placement that satisfy them are the spec's**, and it may choose them freely provided the
properties hold.

1. **Two live plans claiming one file is reported, naming the file and every plan claiming it,
   comparing paths the way the filesystem does.** Every claiming plan, not "the other chain" —
   no mechanism here knows which chain an edit belongs to. Comparison is case-insensitive,
   because the charter commits to Windows. **Files under the artifact root are excluded, and
   that exclusion now lives in the invariant rather than in this criterion** — charter version
   10, after the first interrogation of this document found the constraint and the instrument
   disagreeing. The reason is unchanged and forced: the version-8 rule obliges any check-adding
   change to declare the charter, so two such chains must claim it by construction, and only
   one could ever be live otherwise. What moved is where it is written: a carve-out held by one
   check, while the shipped schema said "across every live plan", was a second list beside a
   source of truth. Without this criterion nothing observes the state, which is today's
   position exactly.

   **It follows that this change's own artifact claims are invisible to its own instrument.**
   The six plans it marks and the charter it declares all sit under the artifact root, so the
   most dangerous edits it makes are outside the comparison. Declaring them still buys the two
   things it always bought — the gate hook stays quiet for work that was agreed, and the
   declaration is the record of what this change touched — but it does not buy collision
   detection, and saying otherwise would be the false comfort this document keeps removing.
2. **A finished plan claims nothing; the six done-or-replaced plans say so; and the write is
   bound to the SHIP verdict, not to the verify stage.** The last clause is not a detail. The
   verify skill runs on both verdicts — the charter binds it to commit its record on DO NOT SHIP
   as well — so a write placed at the stage rather than at the SHIP branch would have a
   *rejected* change permanently release every file its plan claimed, with no way back, because
   finished is one-way and a defect in finished work is a new chain. The charter says "when a
   feature chain ships"; an earlier version of this document said "at feature tier", which is
   the same sentence with the dangerous half removed.

   **What makes a plan finished, since the marking is irreversible and the evidence is thin.**
   Only one of the six chains has a verification record; the other four shipped task-tier and
   passed through no closing stage at all. The rule used here is not recollection: **a plan is
   finished when every file its manifest claims exists in git with that chain's work in it** —
   checkable by a person against the diff, which is what a reviewable commit is for. Without it,
   the first real run reports eight collisions that are all false, and the check is switched off
   rather than trusted.
3. **The plan being checked contributes its claims whatever its own status says, and is excluded
   from the set it is compared against.** Both halves are load-bearing. Without the exclusion
   every *re*-approval refuses, because the plan is already approved on disk when the fix is
   re-checked — and re-approving after fixing an overlap is the path the invariant prescribes.
   Without the first half the check never fires on a *first* approval, because the plan is a
   draft at the moment it runs and a draft claims nothing.
4. **The check fires without anyone remembering it, in a repository that merely installs the
   plugin.** Without it this is a mechanism used by choice, which the direction rejected as the
   reason not to build a lock file. **The observable event:** the shipped hook, given an edit
   payload in a throwaway repository holding nothing but a charter and two colliding plans,
   writes the collision to standard error. The plan skill's prose is not evidence for this — it
   is where a collision is usually caught and never what guarantees it.
5. **The gate hook and the disjointness script each have a stated behaviour under the new field,
   asserted rather than read.** Both must treat an approved-and-finished plan as not live.
   **This grew rather than shrank under the amendment's answer:** because `finished:` sits
   beside `status:` rather than replacing a value in it, `status: approved` still matches
   everywhere, so each reader must learn to check a second field. The parallel build is
   excluded — its status gate is unreachable, so a clause about it would be discharged by
   reading a branch no execution arrives at.
6. **Editing a code file that more than one live plan claims produces a warning naming them.**
   Deliberately weaker than naming the other chain. Without it the hook's existing message
   reports only that *nothing* claims the file, which is the opposite state. The hook stops at
   the first claiming plan today, so moving that return is the change.
7. **Five checks, each a new test class in its own file, each named by its own register row,
   each seen to fail when its fault is injected.** One proves the comparison over built
   fixtures. One proves the shipped hook still speaks when its extracted parser is present and
   fails when it is removed — a packaging check, injected in a fixture and never in the live
   plugin directory. **Three enforce the grammar rules settled at charter version 9**: a
   `files:` entry is a quoted string on the `files:` line, it is a file path rather than a
   directory or glob, and `status:` is unquoted and one of the declared values.

   **Those three are this change's because nothing else can own them.** Charter version 9
   asserted they were checked when nothing checked them — corrected at version 10 — and they
   exist because *this* change is the one that needs the manifest read reliably. Three of the
   five have nothing to do with collisions, which is real growth in a change the direction has
   already re-scoped once; the alternative was a charter claiming an instrument that did not
   exist, which is the failure its register was built to prevent.

   **The shape is load-bearing because the register check is weaker than it looks:** it reads
   only the first `[TestClass]` per file, scans the top directory only, and passes a class when
   either its own name *or any of its method names* is already registered. New file, one class,
   row naming the class is the one shape it actually sees.

## Constraints

- **Windows PowerShell 5.1** for the hook and the script — the only runtime present on every
  Windows machine with no install step.
- **The hook runs on every edit in every repository**, including ones that never adopted this
  framework. It must stay silent where the framework is not in use, and never throw.
- **One parser, shared.** The two PowerShell readers disagree in five measured ways — anchoring,
  leading dots, case, where a slice begins, and the edited path against the claimed one — and
  this change makes both answer the same question about the same file. A check reporting
  different collisions depending on which reader ran is worse than no check. **Root resolution
  travels with it**, because getting the root wrong produces an empty comparison silently, which
  is a larger failure than a misparsed manifest.
- **The normalisation must be lossless where today's is lossy.** `TrimStart('.', '/')` strips
  *every* leading dot and slash, mapping `.sdlc/CHARTER.md` and `.github/workflows/checks.yml`
  onto strings matching nothing on disk — so two different paths can collide with each other and
  a real path can collide with nothing.

  **The spellings that must compare equal, enumerated rather than left as "the way the
  filesystem does":** `./x` against `x`; `src\x` against `src/x`; differing case; `a//x` against
  `a/x`; a trailing slash against none; `a/../a/x` against `a/x`; surrounding whitespace; and an
  absolute path inside the working directory against its relative form. **`.sdlc/CHARTER.md`
  must survive intact** rather than collapsing to a path that does not exist. Everything
  else — short names, junctions, Unicode normalisation forms, trailing dots — is out of scope,
  and saying so is what keeps this checkable rather than a matter of how far someone chose to
  go.
- **A claim the parser refuses is never silent**, and never silences anything else either.
- **A refused plan must never silence the hook.** The hook wraps everything in a catch that exits
  0 by design and has no per-plan containment, so a parser throwing on one bad plan would disable
  the gate for every file in the repository, permanently and without a word. The caller contains
  each plan.
- **A stated time budget, because "stay fast" is not falsifiable.** Measured 2026-09-20: the
  shipped hook takes 306ms with six plans in a flat tree, 317ms with ten, and **1188ms with six
  plans in a tree of 900 directories**, against ~110ms for bare PowerShell startup. Plan count
  costs about 2.75ms each; **root discovery dominates and scales with repository size**, which is
  the term this change relocates. The budget: **no regression beyond the greater of 15% or
  50ms**, median of five runs, on a fixture of 500 directories, measured before and after and
  pasted into `plan.md` as a validation row. The ~150ms figure in the hook's header, the
  charter's technology table and the README is the *runtime startup* comparison, not wall time.
- **This change writes six other chains' plans and the charter, and must declare every one.**
  Editing another chain's artifact without declaring it is the lapse this change exists to catch.

## Non-goals

- **Not a lock and not a reservation.** A claim is a side effect of a live plan existing.
- **Not a record of who is working on what.** No session identity, no activity over time.
- **Not a queue.** It detects and refuses loudly; it never serialises.
- **Not atomic.** Two sessions can both approve and the collision surfaces at the next edit or
  the next approval. Charter amendments are not serialised either — a version bump makes git
  refuse the second *push*, which is detection after both are written.
- **Not cross-branch or cross-worktree**, and the two mitigations are mutually exclusive: the
  hook only sees a collision when both live plans are on disk in one working tree, so a worktree
  per session trades this protection for separation of build output.
- **Not the other collision surfaces.** Six were measured beside this one, the git index worst
  among them. All stay handled by practice.
- **Not the artifact files.** Nothing under the artifact root is compared, for the reason in
  criterion 1.
- **Nothing rewrites a plan automatically.** The six are marked by a person in a reviewable
  commit; no tool ships that could do it unattended.
- **Nothing stages or commits on anyone's behalf.**
- **No slice manifest is rewritten on a finished plan.** Only the status line and the new field
  move. What those plans claimed is the record of what that change touched.
- **A claim is never checked against the filesystem.** Claims go stale silently when a file is
  renamed. Not to be confused with the two *corrupted* claims described above: every path
  declared in every plan exists — what does not exist is what the parsers make of them.
- **The hook's existing skip rules are unchanged**, wrong edge cases included: its artifact-root
  test is segment equality, so a root at `docs/sdlc` also skips `src/sdlc/Thing.cs`, and `bin/`,
  `obj/` and `node_modules/` are *not* skipped on edits.
- **The hook's tool matcher is not widened.** A `git mv` still never reaches it.
- **The parallel build's wave loop is not fixed here**, nor the scaffold executor's duplicated
  root resolver, nor the router's competing notion of an active chain. Each is its own work.
- **The extracted parser is not a supported extension point.**
- **The README runtime-table check is out of scope.** It would pass in full with this change
  deleted, which is what the preamble above disqualifies. The stale line naming a file that has
  never existed is still fixed here, because it sits four lines from prose this change edits.
- **No check runs over this repository's own chain tree.** Every instrument runs over built
  fixtures. A check reading the live `.sdlc/` directory was considered and dropped: after the
  marking this repository normally holds zero or one live plan, so it would be green by
  construction, and mutation-testing it means writing a colliding plan into the live tree and
  restoring it — the operation this repository's standing instruction warns has already
  destroyed uncommitted work. Stated here because adding such a test is the obvious next move
  and the reason against it is not obvious.
- **Warning volume is not in scope.** Code files that only finished plans claimed will begin
  drawing unplanned-scope warnings; those edits genuinely are outside any live plan.
- **This is not status tracking.** A plan's status is a parser input read at the moment of a
  check, never accumulated or reported over time.

## Open questions

- **Does the cross-plan check run before the plan skill's approval gate, or at it?** The skill
  writes the plan as a draft, runs the single-plan check, and approves later. Running it early
  leaves the approval window unguarded — two sessions both past it and neither past the gate each
  see the other as a draft, and both pass. Running it at the gate closes that window and moves
  the check to a step that does not exist yet. *Owner: peter, at the spec gate.*
- **Does shrinking the live set make the hook noisy enough to want softening?** Today's claims
  keep it quiet on those paths; after the marking, code files only finished plans claimed begin
  warning on every edit. Correct by the gate's definition, and a behaviour change to something
  that fires on every keystroke. *Owner: peter, at the spec gate, on measured output.*
- **Does reconciling the JavaScript parser belong here?** Both cross-plan readers are PowerShell,
  so it is off the critical path — but its comment claiming all three normalise identically is
  false today and stays false. *Owner: peter, at the spec gate, with the charter's
  fourth-consumer line as the test.*
**Closed on 2026-09-21 rather than carried:** *when is the amendment's grammar enforced on the
plans that already exist?* Measured — all six conform already. Every `status:` line is the
unquoted word, every `files:` line is a quoted flow-style list, and no entry is a directory. The
three new rules break nothing in this repository, so they can be enforced from the day they
land. Recorded because the question was worth asking and the answer took one command.

## What this document got wrong before it got it right

Recorded because the errors were the useful part, and because most were the same shape: a count
or a claim written from memory rather than from the code. **The current figures live once, in
the sections that use them.**

- **The hook's cost.** Attributed first to file reading, then to file work on top of startup.
  Neither: plan count is nearly free and root discovery dominates.
- **The number of files shared with other plans.** Restated four times from memory before anyone
  ran a command.
- **Readers gating on `approved`.** Overcounted twice — the script reads no status, and the
  scaffold executor reads the charter's rather than a plan's.
- **Tests pinning the hook's case sensitivity.** Said two existed; none does. Inherited from a
  review note and written down without opening the files.
- **The state of `engine-substitutes-declared-tokens`.** Called parked; its plan is superseded
  without having been built while its intent carries a parked banner — which is why a plan's own
  status governs its claims.
- **The script's exit contract.** Described as "1 for an overlap"; exit 1 covers seven distinct
  failures and means "any failure".
- **The register check's strength.** Claimed a new class is mechanically enforced. It is not,
  three ways over.
- **The parallel build's status gate.** Called untested; it is *unreachable*, which is a
  different and stronger claim.
- **"Approvable but blocked".** A state the framework has no mechanism for — the spec skill
  branches on `status:` alone, so a banner is prose nothing reads.

Two decisions were reversed on evidence: a bulk-marking tool was dropped once its user population
was measured at one repository, once; and a check reading this repository's live chain directory
was dropped once it became clear it would be green by construction and could not be
mutation-tested without writing into the live tree.
