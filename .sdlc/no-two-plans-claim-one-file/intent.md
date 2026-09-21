---
slug: no-two-plans-claim-one-file
title: No two live plans claim the same file
status: draft
tier: feature
created: 2026-09-20
owner: peter
---

# intent — no two plans claim one file

> **Unblocked on 2026-09-21 by charter versions 9 through 12**, which settled the twelve questions this
> chain could not be specified without: the mechanism that marks a plan finished, how far it
> reaches, whether it is one-way, who writes it, and the frontmatter grammar.
> Those are decided there and are not restated here. Version 10 repaired two defects in 9 that
> the first interrogation of this document found: the disjointness carve-out for the artifact
> root, and grammar rules that claimed instruments which did not exist. Version 11 corrected two
> more of the same kind, and recorded why an amendment that settles twelve things produces them.
> Version 12 completed the grammar paragraph, which listed three of its four rules while the
> shipped schema listed all four — the fifth defect traceable to that one conversation.
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
one of those eight was a live collision. **Seven, strictly**, once the charter is carved out of
the comparison; the eighth is `.sdlc/CHARTER.md`, which the invariant now excludes. A check
reporting seven false collisions on the day it lands is a check that gets switched off. The amendment settles the marker; this change uses it.

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
  **Its status gate is unreachable:** the wave loop forty-five lines above filters slice objects
  against a string id, so the remaining set never shrinks and no execution reaches the gate.
  Recorded as an open item; not this change's to fix.
- **`plugins/sdlc-core/skills/sdlc-router/references/artifact-schemas.md`** — carries the plan
  shape, including the `finished:` field and the four grammar rules settled at versions 9 and
  12. **It is edited by this change**, not merely read: it says the grammar check "lands with
  `no-two-plans-claim-one-file`", which goes stale the day this ships.
- **`plugins/sdlc-core/skills/plan/SKILL.md`** — runs the disjointness check at a step where the
  plan is still `draft`, and approves at a later one. Gains the cross-plan invocation.
  **`Assert-SliceManifest.ps1 -Plan <path>` alone must keep working exactly as it does today**,
  including its output and its exit codes, in a plugin installed per machine. **The literal
  command line appears in four places**, counted rather than recalled: this skill, the schema
  reference, the script's own example block, and a validation row in a shipped chain's plan. The
  README names the script but carries no invocation, which an earlier version of this bullet
  claimed it did. The cross-plan comparison is a new mode beside that one, never a change to it.
- **`plugins/sdlc-core/skills/verify/SKILL.md`** — gains the write of `finished:` on the **SHIP**
  branch of its gate, at feature tier. Not at the stage: it runs on both verdicts.
- **`plugins/sdlc-core/.claude-plugin/plugin.json`** — the engine plugin's version, bumped
  because this change ships hook and script code and plugins are cached per version. Claimed by
  a finished plan, and one of the five files in the collision set below.
- **`tests/AiSdlc.Tests/SliceManifestTests.cs` and `GateHookTests.cs`** — the existing instruments
  for the two mechanisms being widened.
- **`tests/AiSdlc.Tests/TempRepo.cs` and `Repo.cs`** — the fixture, which writes one chain with
  one plan and cannot express two colliding plans. Shared by three suites, so it sits in exactly
  one slice. `Repo.cs` resolves shipped scripts by walking up to the plugin directory.
- **`tests/AiSdlc.Tests/Frontmatter.cs`** — the existing frontmatter-parsing helper, claimed by
  no plan, and the thing the grammar check most plausibly builds on. Named either way, because
  an undeclared edit is the lapse this change exists to catch.
- **`.sdlc/CHARTER.md`** — also says the grammar check "lands with" this change, which goes
  stale the day it does, in the same way the schema's copy does. It takes four register rows
  for the four new checks, and **five
  known-and-unchecked entries, named rather than left as a count**: two colliding live plans in
  this repository passing a green build, since no instrument reads the live chain tree; the hook
  comparing against the wrong base when a session starts above the repository root; and any
  exception inside the new comparison disabling the gate entirely, since the hook swallows
  everything by design. **Two more, and the second is the sharpest gap in the change:** that a
  prose check cannot prove the verify skill follows at runtime the branch it names; and that
  **nothing holds `finished:` one-way at all**. A person editing `true` back to `false`, or
  deleting the line, silently re-acquires every file that plan claimed — the failure criterion 2
  calls irreversible — and no instrument sees it, because the grammar check reads a file's
  current state and has no memory. Git history is the only record, and nothing consults it.
  An earlier version of this document claimed the verify check "holds the one-way write"; it
  holds where the write is named, which is a different thing. **One more was considered and
  does not go on the list:** two chains colliding on `OPEN.md` or `DIRECTION.md` after the
  version-10 carve-out, which the invariant records in its own words rather than here.
  Declared in the slice manifest, per the rule settled at version 8. An unnamed set cannot be
  reviewed against the invariant's own rule that a fast-growing list is itself a finding.
- **`README.md`** — its gate-hook section **enumerates the hook's two warning states**, and this
  change adds a third. The same section closes by citing `scripts/Test-SdlcGate.ps1`, a file
  that has never existed — fixed here because it is a known-false line inside prose this change
  is already rewriting.
- **`plugins/sdlc-core/hooks/hooks.json`** — its `description` ships to every user and also
  names the hook's two states: *"either no plan is approved, or no slice claims the file."*
  Third such list, and the one that travels furthest. **Nothing compares any of the three to
  the strings the hook emits**, and this change adds an item to all three — carried as an open
  question below rather than settled here.
- **One false timing claim, in the hook's own header**: *"It costs ~150ms per edit."* It is a
  claim about wall time and the measurement in Constraints falsifies it. Corrected here for the
  same reason the stale script name is — a known-false line beside prose this change already
  edits. The README's and the charter's copies are left alone, because they frame the figure as
  a startup comparison and are correct; the reasoning and the numbers are in Constraints, once.
- **The five finished plans and the one superseded plan** — `init-scaffolds-solution`,
  `pack-promises-what-it-scaffolds`, `register-names-every-check`, `stack-dotnet-house-stack`,
  `verification-reports-options`, and `engine-substitutes-declared-tokens` — the last superseded
  in fact but not yet in its frontmatter, which still reads `approved`, and which this change
  corrects.
- **The owner**, who cannot currently run two sessions without hand-checking for overlap.

**Five of the files this change edits outside the artifact root are claimed by another live
plan today** — the schema reference (by a superseded plan *and* a finished one), the engine
plugin's version file, the README, the test fixture, and the verify skill: **five files, six
claims**. All six are freed by the marking, which is why the marking goes first. `.sdlc/CHARTER.md`
is claimed twice more and is deliberately not in this count, because the version-10 carve-out
puts the artifact root outside the comparison entirely — which is the whole reason two
check-adding chains may both claim it.

**This count has now been wrong five times**, and the fifth was in a paragraph that said
"measured rather than recalled" while omitting the verify skill — a file the second success
criterion requires this change to edit. The figure above came from comparing the touch set
against the claim set with a command. It is the single most re-derived number in this document
and the one most worth distrusting.

## Proposed outcome

Two live plans claiming one file stops being invisible — reported at the moments the framework
already has, and by the one mechanism that fires on its own.

**Not "a file claimed by a live plan is claimed by exactly one".** That was the earlier
headline and the four bullets below contradict it: three tools, code files only, the path being
edited, and approval a catch rather than a guarantee. It is the sentence that would be quoted
back, so it says what the change does rather than what it would be nice for it to do.

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
   no mechanism here knows which chain an edit belongs to.

   **Comparison is case-insensitive, which also changes the gate that exists today.** A file
   claimed in one casing and edited in another currently draws an unplanned-scope warning and
   afterwards will not — a fix rather than a regression, since the filesystem treats them as one
   file, but a change to shipped behaviour and not only to the new check. **Nothing pins it in
   either direction:** no test anywhere varies casing between a claim and an edit. The rule
   itself follows from the charter committing to Windows.

   **Files under the artifact root are excluded, and
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

   **And the write is held by an instrument, not by the sentence above.** The verify skill's
   branch is prose, which is the defect the direction diagnoses, and this is the most expensive
   prose in the change: a write on the wrong branch releases a rejected plan's claims
   permanently. The check in criterion 8 holds it — including that `plan.md` is staged in the
   SHIP commit, since that commit names only the verification record today and a marking left
   unstaged means a fresh session reads a live plan.

   **What makes a plan finished, since the marking is irreversible and the evidence is thin.**
   Only one of the six chains has a verification record; four shipped task-tier and passed
   through no closing stage at all, and the sixth was superseded without ever being built. The
   rule used here is not recollection: **a plan is finished when every file its manifest claims
   exists in git with that chain's work in it** — checkable by a person against the diff, which
   is what a reviewable commit is for. Without it, the first real run reports seven collisions
   that are all false, and the check is switched off rather than trusted.
3. **The plan being checked contributes its claims whatever its own status says, and is excluded
   from the set it is compared against.** **This governs the plan under check; the liveness rule
   in criterion 5 governs every *other* plan.** Without that tiebreak the two criteria give
   opposite answers for a plan that is approved and finished and is itself the one being
   checked — and the single-plan invocation keeps today's behaviour, which reads no status at
   all, so finished-awareness lives only in the cross-plan mode.

   **The bootstrap, stated because an engineer will hit it.** This plan is approved before the
   check it introduces exists, at a moment when all six plans still read `approved` and this one
   claims five files they claim. The marking commit therefore lands **before this plan is
   approved**, not as a slice inside its build — otherwise the first re-approval after the
   check ships refuses the plan that shipped it.

   **Which raises the gate question that commit has no answer for.** It rewrites six other
   chains' approved artifacts and the charter at a moment when no approved plan authorises any
   of it, and the framework's whole posture is that a gate is crossed only by explicit human
   words. The honest position: **that commit is the owner's act, taken against this intent at
   its approval gate, and this paragraph is where it is authorised.** Naming it here is what
   makes it a decision rather than six undeclared edits — and it is the one place in the chain
   where work precedes the plan that declares it, which is worth flagging rather than smoothing.

   **Both halves of this criterion are load-bearing.** Without the exclusion every
   *re*-approval refuses, because the plan is already approved on disk when the fix is
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
6. **Editing a code file that more than one live plan claims produces a warning naming them** —
   and, under the blocking switch, refuses, like every other warning the hook emits. Saying
   "warns" alone would be false for anyone who has that switch set. Deliberately weaker than
   naming the other chain. Without it the hook's existing message reports only that *nothing*
   claims the file, which is the opposite state. The hook stops at the first claiming plan
   today, so moving that return is the change.
7. **In the script, a comparison that found nothing is distinguishable from one that compared
   nothing.** The constraint below fixes one cause of an empty set — a wrongly resolved root —
   but a nested chain directory, a renamed `plan.md` or a refused parse produce the same silence
   and report green. The single-plan script already models it, printing what it counted. Without
   this, the check's most reassuring output is also what it prints when it is broken.

   **It binds the script and deliberately not the hook.** The hook writes only when it has
   something to say and is otherwise silent by contract, so a hook that compared six plans and a
   hook that compared none produce identical output — nothing. Making it announce its counts
   would put a line on standard error on every edit in every repository, which is the constraint
   below that it stay quiet where the framework is not in use.
8. **Four checks, each a new test class in its own file, each named by its own register row,
   each seen to fail when its fault is injected.**

   - **The comparison**, proved over built fixtures.
   - **The packaging check** — the shipped hook still speaks when its extracted parser is
     present, and fails when it is removed. Injected in a fixture, never in the live plugin
     directory.
   - **The frontmatter grammar**, covering all four rules: a `files:` entry is a quoted string
     on its line, it is a file path rather than a directory or glob, `status:` is unquoted and
     one of the declared values, and `finished:` is `true` or `false` alone on its line — with
     **`true` requiring `status: approved`, `false` legal anywhere, and absent meaning not
     finished**. The last three clauses are load-bearing: an earlier version of this criterion
     said the *field* was approved-only, which makes the schema's own plan template illegal
     (it carries `finished: false` on a draft, because a plan is authored as one) and, read
     literally, requires the field on every plan when not one carries it today.
   - **The verify skill's SHIP branch**, proved by a prose check over the skill body — that the
     `finished:` transition and the `plan.md` staging appear **after the `On **SHIP**` marker
     and nowhere in the DO NOT SHIP block**. Both halves are required: presence alone passes a
     skill that names the transition in both branches, which is the fatal case. **Matching on
     headings does not work** — the gate has one heading and it contains the literal string
     `DO NOT SHIP`, so a naive match finds the wrong branch first; the branches are bold inline
     markers, not headings. An earlier version of this criterion specified a heading match and
     was unbuildable. There is precedent for holding this skill's prose: an existing check
     requires it to name its options.

   **What that check still cannot prove**, named rather than implied: that the skill, at
   runtime, follows the branch it names. A prose check reads a document. That residue joins the
   known-and-unchecked entries rather than passing as covered.

   **Four rather than the five first proposed, and the difference is worth naming.** The
   grammar had three rules when that count was written and has four now. The three were going
   to be three classes and three rows; the four are one class with four
   methods, because the register enumerates *classes of silent failure* and "a chain artifact's
   frontmatter is spelled in a form no reader accepts" is one class with four instances — the
   same granularity as the existing row for malformed definition frontmatter. The fourth check
   is new and was not in that count at all: it holds the one-way write.

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
  pasted into `plan.md` as a validation row.

  **Which of the three places that cite ~150ms are wrong: one.** The charter's technology table
  and the README both frame it as a *runtime startup* comparison against node and python, which
  is what the number is, and are correct. The hook's own header says *"It costs ~150ms per
  edit"* — a claim about wall time, and false by the measurement above. That one is corrected
  here; the other two are left alone.
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
- **Not the other collision surfaces.** The direction measured seven beside this one, two of
  them already safe — different slugs, GUID-based fixture paths — and the git index the worst
  of the five that are real. All five stay handled by practice.
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
  fixtures — **including the grammar check**, which therefore proves the readers reject bad
  spelling and never inspects a real plan. A plan written with a block-style `files:` list fails
  when a reader runs, at plan approval or on an edit, and never in continuous integration. The
  closed question below reads as if the repository is held to conformance; it is not, and the
  measurement there says only that nothing needs migrating. A check reading the live `.sdlc/` directory was considered and dropped: after the
  marking this repository normally holds zero or one live plan, so it would be green by
  construction, and mutation-testing it means writing a colliding plan into the live tree and
  restoring it — the operation this repository's standing instruction warns has already
  destroyed uncommitted work. Stated here because adding such a test is the obvious next move
  and the reason against it is not obvious.
- **A collision on a file nobody edits and no plan re-approves is never reported.** Both
  mechanisms are event-driven — the hook per edited path on three tools, the script once per
  approval — so two live plans overlapping on ten files say nothing until one of those ten is
  touched or one plan is re-approved. Criterion 1 reads as a standing property; it is not, in
  the same way fan-out is not covered.
- **No migration for repositories running an older engine**, and the reason costs nothing
  today: the marketplace points at a local filesystem path, so no second machine has this
  installed. The normalisation rewrite changes which paths two plans are judged to collide on
  and plugins are cached per version, so an older engine and a newer one would disagree with no
  version gate. That is free only while the installed population is one, which is recorded as
  an open item — and closing that item reopens this.
- **The hook's root resolution moves unchanged, wrong edges included.** It returns from the
  first candidate charter it finds, so a repository with an unrelated `docs/CHARTER.md` gets
  `docs/` as its artifact root whether or not that charter declares one. Extracting it is a
  move, not a repair; fixing it would change which plans every existing repository sees.
- **The cross-plan mode's cost at approval is unbounded, deliberately.** It walks every chain
  directory and parses every plan on each approval, and root discovery dominates and scales
  with repository size. The hook has a budget because it runs on every keystroke; this runs
  once, when a person is already waiting at a gate. Stated as a decision rather than left as
  the one measured cost nobody bounded.
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

- **Does the third warning state get a check that holds its three descriptions together, or an
  entry saying nothing does?** The hook's two states are written out in three places — the
  script's own header, the README's gate-hook section, and the `description` in `hooks.json`
  that ships to every user — and **nothing compares any of them to the strings the hook
  actually emits.** One existing test asserts that `unplanned scope` reaches stderr; no test
  reads the three prose copies at all. This change adds a state to all three, which is the
  repository's own cardinal failure in its live form: a list beside a source of truth, with the
  rule that the comparing check is part of the same change. Against that stands the argument
  this document already makes for four checks rather than five — the register enumerates
  classes, and "a shipped description disagrees with the hook it describes" is a class of its
  own rather than an instance of an existing one. *Owner: peter, at the spec gate. It is the
  one question here whose wrong answer is a new second list, not a deferred improvement.*

**Closed on 2026-09-21 rather than carried:** *when is the amendment's grammar enforced on the
plans that already exist?* Measured — all six conform already. Every `status:` line is the
unquoted word, every `files:` line is a quoted flow-style list, and no entry is a directory; and
no plan carries `finished:` at all, which the field's own rule reads as not finished. So all
four rules break nothing in this repository and can be enforced from the day they land.
Recorded because the question was worth asking and the answer took one command.

## What this document got wrong before it got it right

Recorded because the errors were the useful part, and because most were the same shape: a count
or a claim written from memory rather than from the code. **The current figures live once, in
the sections that use them.**

- **The hook's cost.** Attributed first to file reading, then to file work on top of startup.
  Neither: plan count is nearly free and root discovery dominates.
- **The number of files shared with other plans.** Wrong five times, every one of them restated
  from memory before anyone ran a command. The figure in the problem section is the one that
  came from comparing the touch set against the claim set mechanically.
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
