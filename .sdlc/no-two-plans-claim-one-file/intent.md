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
> **Versions 13 through 15 unblocked nothing and are named anyway**, because what they were
> spent on is the reason this document keeps being interrogated. 13 dated two stale figures,
> one a test count describing a suite a third the size of the one that runs today. 14 found
> that one of the five parser divergences was never a divergence — re-measuring a number this
> document had already called *measured*. 15 moved the register-row count after the owner
> settled a question at this gate. **Five consecutive versions on one class — a document
> asserting something about the code that is not so — and the Affected list below now carries
> an instance of it in a shipped engine file.**
>
> **This document was rewritten against those answers, and shortened.** Its first draft ran to
> 1680 lines and held the design — where the extracted parser lives, how paths normalise, what
> the exit codes are. The schema says an intent contains no implementation, and anything
> approved at this gate cannot be superseded downstream without reopening it. What remains is
> what must be *true*; how it is achieved is the spec's. The earlier draft is in git at
> `93175c5` if the reasoning behind a decision is wanted.

## Problem

Four statements, each measured on 2026-09-20 rather than assumed — nothing compares two plans,
the artifacts could not tell a live plan from a finished one, what the gap actually costs, and
how much narrower the gate hook is than the direction assumed. The paragraph under the first of
them is reasoning rather than measurement, and says so.

**Nothing compares two plans.** `Assert-SliceManifest.ps1` proves the slices inside one plan
pairwise disjoint, and the plan skill runs it at approval. The build does not run it — it
re-implements disjointness in its own JavaScript, having no filesystem access — so the same
rule is written twice in two languages, which is how the two came to normalise paths
differently. Either way both operate on a single plan. **Two separately approved plans claiming
one file is not a state anything in the framework can observe.**

**How it actually arrives, and the worse of the two is the one in scope.** Across branches or
worktrees it is a merge conflict found after both implementers have finished — expensive,
because some of the work must be thrown away, but loud. **In one working tree, which is the
only case this change's guarantee covers, there is no conflict at all:** two sessions edit the
same file, the later write wins, and the build can stay green over work that was silently
undone. The non-goals say the hook sees a collision only when both live plans are on disk in
one tree, so the symptom this change can actually prevent is the quiet one. Saying "merge
conflict" alone would argue for the case the change does not reach.

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

  **And it asserts an instrument that does not exist, which matters more than the stale line.**
  It states that *"`superseded` is written by `/sdlc-core:plan` when it replaces a plan — the
  moment the replacement is written and the only moment anyone reliably knows"*. The plan skill
  does not contain the word, and nothing under `plugins/` writes that value anywhere. **So one
  of the two ways a plan stops being live is written by nobody**, and a replaced plan reads
  `approved` forever — which is exactly the history of `engine-substitutes-declared-tokens`,
  the one plan this change marks superseded by hand. The sentence's own second half is the true
  half: supersession happens by hand. It is corrected here, for the reason the stale script
  name and the false timing claim are — a known-false line in prose this change already edits,
  and this one is load-bearing rather than cosmetic, because the whole change rests on a
  non-live plan saying so. **It is the same class charter versions 10 through 13 were each
  spent repairing — a document asserting an instrument that does not exist — and the first
  instance found outside the charter.** Which is the argument for looking: three of those four
  versions were found by interrogating this document, none by reading the document that made
  the claim.
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
- **`tests/AiSdlc.Tests/SilentFailureRegisterTests.cs`** — not edited, and named because **it
  imposes a slicing constraint nothing else here states.** It reads the live `.sdlc/CHARTER.md`
  and enumerates the live top-level test directory, both directions, so a charter row without
  its test file is red and a test file without its row is red. **The four rows and the four new
  test classes therefore land in one commit**, which cannot be split across slices without
  leaving the tree red in between — and nothing may be committed on a red build. That makes it
  a constraint on the plan's shape rather than a detail of the register.
- **`tests/AiSdlc.Tests/TempRepo.cs` and `Repo.cs`** — the fixture, which writes one chain with
  one plan and cannot express two colliding plans. Shared by three suites, so it sits in exactly
  one slice. `Repo.cs` resolves shipped scripts by walking **up to the repository root** —
  the first ancestor holding a `.claude-plugin` directory — and joining the plugin path onto it.
  An earlier version of this line said "up to the plugin directory", which names a different
  place: both the repository root and each plugin hold a `.claude-plugin`, so the distinction
  decides what the walk stops at.
- **`plugins/sdlc-core/agents/verifier.md`** — a **fourth reader of `files:` lists that is not
  a fourth parser**: it tells an agent to compare `git diff --name-only` against the manifest
  and call anything unclaimed unplanned scope. Read by a model rather than by code, so the
  charter's three-parsers paragraph is right not to count it — but it is a reader, it reads the
  plan under verification at the moment verify is also writing `finished:` to that plan, and
  leaving it off this list would be the omission the list exists to prevent. **It is also one
  of the three copies of the manifest-drift instruction**, so the fifth check reads it even
  though this change does not edit it — which is the point of that check: the copy nobody
  touches is the one that drifts.
- **`tests/AiSdlc.Tests/Frontmatter.cs`** — the harness's own frontmatter reader, claimed by no
  plan. Named because an undeclared edit is the lapse this change exists to catch, **and not
  because the grammar check is built on it** — an earlier version of this bullet called it "the
  thing the grammar check most plausibly builds on", which is the wrong half of a decision the
  constraints now settle. Worth knowing if anything does build on it: `Read` assigns each match
  into a dictionary, so **two `finished:` lines resolve last-wins with no error**, and its field
  regex anchors on a non-whitespace first character, so a `finished:` nested under `slices` is
  **invisible**. Those are the two spellings criterion 8 says should be rejected, already
  answered the other way, in a helper several suites call.
- **`tests/AiSdlc.Tests/PluginStructureTests.cs`** — holds the two checks whose file globs the
  constraint below may have to widen, **and it is claimed by `init-scaffolds-solution`**. Named
  because the constraint's own conditional requires editing it: if the shared parser does not
  ship as `.ps1`, this file does. **If that branch is taken the headline count becomes six
  files and seven claims rather than five and six** — stated here so the number moves with the
  decision instead of after it.
- **`.sdlc/CHARTER.md`** — also says the grammar check "lands with" this change, which goes
  stale the day it does, in the same way the schema's copy does. It takes **five register rows
  for the five new checks** — four until the owner settled the prose-list question at this
  document's gate — and **five known-and-unchecked entries**.

  **Each is named, and each says which change would instrument it**, because the charter admits
  a class to that list only when its instrument is a *different* change and explicitly not
  because writing the check is tedious:

  - **Two colliding live plans in this repository passing a green build.** The instrument is a
    check over the live chain tree, dropped above because after the marking this repository
    holds zero or one live plan and the check would be green by construction. It becomes
    buildable when a second repository runs this framework — which is the item about the
    framework being installable nowhere but this machine.
  - **The hook comparing against the wrong base when a session starts above the repository
    root.** The hook resolves the root from the working directory it is handed, and the handing
    is the host's, not this repository's. The instrument is a test that controls what a hook is
    invoked with, which is a change to the harness rather than to the hook.
  - **An exception inside the new comparison disabling the gate entirely**, since the hook
    swallows everything and exits 0 by contract. The instrument is fault injection into shipped
    hook code at runtime; the packaging check reaches the nearest case — a missing parser file —
    and the general one needs a harness that can make live code throw on demand, which nothing
    here has.
  - **A prose check cannot prove the verify skill follows at runtime the branch it names.** The
    instrument is a harness that executes a skill body with its agent bound — the same missing
    harness that lets a workflow with a non-terminating loop parse clean, recorded as its own
    open item. Squarely a different change.
  - **Nothing holds `finished:` one-way at all.** A person editing `true` back to `false`, or
    deleting the line, silently re-acquires every file that plan claimed — the failure
    criterion 2 calls irreversible — and no instrument sees it, because the grammar check reads
    a file's current state and has no memory. **This is the weakest of the five admissions and
    the one to argue at the gate**, because the instrument is plausible inside this change: the
    previous commit's copy of the file, compared against the current one. What stops it is that
    no check in this suite reads git history, and the hook cannot acquire the habit — it runs
    per keystroke, and shelling out to git on every edit is the cost this change is budgeting
    against. Those are reasons, not a law, and the owner may overrule them.

  An earlier version of this document claimed the verify check "holds the one-way write"; it
  holds where the write is named, which is a different thing. **One more was considered and
  does not go on the list:** two chains colliding on `OPEN.md` or `DIRECTION.md` after the
  version-10 carve-out, which the invariant records in its own words rather than here.
  Declared in the slice manifest, per the rule settled at version 8. An unnamed set cannot be
  reviewed against the invariant's own rule that a fast-growing list is itself a finding.
- **`README.md`** — its gate-hook section **enumerates the hook's two warning states**, and this
  change adds a third. The same section closes by citing `scripts/Test-SdlcGate.ps1`, a file
  that has never existed — fixed here because it is a known-false line inside prose this change
  is already rewriting. **The fix names the real instrument rather than deleting the sentence:**
  the hook's behaviour is pinned in `tests/AiSdlc.Tests/GateHookTests.cs`, eleven methods. A
  deletion would lose a true and useful pointer along with the false filename.
- **`plugins/sdlc-core/hooks/hooks.json`** — its `description` ships to every user and also
  names the hook's two states: *"either no plan is approved, or no slice claims the file."*
  Third such list, and the one that travels furthest — it reaches every user who installs the
  plugin, whether or not they ever open the README. **Nothing compared the three until now**,
  and this change adds an item to all three; the fifth check in criterion 8 holds them to each
  other from here.
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
- **And it decays from there, by two separate routes, until a closing step exists.** A plan
  stops being live in two ways and **neither is written by a tool for the common case**:

  - **Finished.** Only feature chains get `finished:` written for them, by the verify skill on
    the SHIP branch of its gate. Four of the five shipped here were task tier and pass through
    no closing stage at all, so their plans stay live after the work ships. That is recorded as
    item 4 in `.sdlc/OPEN.md`, with the closing step as its fix.
  - **Superseded.** The shipped schema says the plan skill writes this value when it replaces a
    plan. **It does not — the skill has never contained the word, and nothing under `plugins/`
    writes it.** So a replaced plan also stays `approved` forever, which is how the one
    superseded plan in this repository got that way. This change makes the schema say what is
    true, and marking stays a person's act.

  Both belong here rather than only in the open items, because the intent is what a fresh
  session reads, and because the outcome above promises the first real run is true. **It is
  true on the day it lands and decays afterwards at a rate nothing measures**, which is a
  weaker promise than "the report is trustworthy" and the one this change can keep.
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

   **"Naming" means the chain directory's own name, which is not necessarily the plan's
   `slug:`.** The hook identifies a plan today by `Split-Path -Leaf` on its directory and never
   reads the field, and nothing holds the two together the way an existing check holds a skill's
   name to its directory. The directory name is the right answer — it is what a person types to
   find the chain — but it is a choice, and a report that said `slug:` while the code said
   directory leaf would name a chain that cannot be found. Whether the two should be pinned to
   each other is a different change.

   **Comparison is case-insensitive, which also changes the gate that exists today.** A file
   claimed in one casing and edited in another currently draws an unplanned-scope warning and
   afterwards will not — a fix rather than a regression on Windows, but a change to shipped
   behaviour and not only to the new check. **Nothing pins it in either direction:** no test
   anywhere varies casing between a claim and an edit.

   **It is a chosen tradeoff, not a property the platform guarantees**, and an earlier version
   of this paragraph stated it as the latter — *"the filesystem treats them as one file"*. Git
   is case-sensitive and NTFS supports per-directory case sensitivity, so a repository genuinely
   can track `src/Foo.cs` and `src/foo.cs` as two files. The trade taken here is that two such
   files collapse into one claim, accepted because the charter commits to Windows and the
   alternative — a comparison that disagrees with the default filesystem — is the worse
   surprise. Low probability, and worth being a decision rather than an assumption.

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

   **It touches the six plans and nothing else, and the charter is deliberately not in it.**
   The five register rows go in with their five checks, inside the planned slice that declares
   `.sdlc/CHARTER.md` in its manifest, because the version-8 rule puts a register row on the
   gate of the change that adds the check — and because rows naming tests that do not exist yet
   turn the register check red, which nothing may be committed over. An earlier version of this
   paragraph said the marking commit rewrote the charter too; that would have put the rows
   before their checks and outside any manifest at once.

   **Which still raises the gate question that commit has no answer for.** It rewrites six other
   chains' approved artifacts at a moment when no approved plan authorises any of it, and the
   framework's whole posture is that a gate is crossed only by explicit human
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
5. **The gate hook, and the script's cross-plan mode, each have a stated behaviour under the
   new field, asserted rather than read.** Both must treat an approved-and-finished plan as not
   live. **The single-plan invocation is excluded and that exclusion is load-bearing:** it reads
   no status today, the Affected list promises it keeps working exactly as it does, and
   criterion 3 says finished-awareness lives only in the cross-plan mode. An earlier version of
   this criterion said "the disjointness script", full stop, which reads as an instruction to
   teach `-Plan <path>` to read status — breaking the one guarantee this change makes about
   behaviour it does not change.

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
8. **Five checks, each a new test class in its own file, each named by its own register row,
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

     **It binds `plan.md` and no other shape.** Two of the four rules are plan-only by the
     schema; the third — `status:` unquoted and one of the declared values — is stated there
     for all five artifact shapes, and binding the check to all five would be a wider promise
     than this change measured, since the conformance measurement covered the six plans and
     nothing else. **The charter and verification records are outside it**, said plainly
     because the non-goal that no check reads a real `plan.md` means nobody can settle the
     question by running something.

     **Two spellings the grammar does not yet answer, and for a one-way field that is a
     property rather than a detail:** a plan carrying **two** `finished:` lines, and one
     carrying the field **inside the `slices` block** rather than at the top level. A
     line-oriented parse can answer first-wins, last-wins or reject, and the three differ on
     whether `finished: true` followed by `finished: false` releases a plan's claims. Reject is
     the answer that matches every other rule here — the others exist so that a form no reader
     agrees on fails loudly — but it is the spec's to state and the check's to hold.
   - **The verify skill's SHIP branch**, proved by a prose check over the skill body — that the
     `finished:` transition and the `plan.md` staging appear **after the `On **SHIP**` marker
     and nowhere in the DO NOT SHIP block**. Both halves are required: presence alone passes a
     skill that names the transition in both branches, which is the fatal case. **Matching on
     headings does not work** — the gate has one heading and it contains the literal string
     `DO NOT SHIP`, so a naive match finds the wrong branch first; the branches are bold inline
     markers, not headings. An earlier version of this criterion specified a heading match and
     was unbuildable. **It also claimed precedent that does not exist:** the check requiring an
     options section to carry an instruction reads `agents/verifier.md`, not the verify skill,
     and no check in the suite reads that skill body by name — it is reached only by the generic
     sweep every skill gets. The check is still buildable and the shape is still borrowed from
     a real one; what was false was that this particular file already had an instrument.

   - **The shipped prose lists agree with each other**, in both directions, for the two lists
     this change touches. **The hook's warning states** are written out in three places — the
     script's header, the README's gate-hook section, and the `description` in `hooks.json`
     that ships to every user — and this change adds a third state to all three. **The
     manifest-drift instruction** is written out in three more — the verifier agent, the schema
     reference and the verify skill — each naming the same two conditions, files changed that
     no slice claimed and files claimed that nothing touched, and this change edits two of
     them. One class, two lists, because the register enumerates classes and "a shipped
     description of an engine behaviour disagrees with the other copies of itself" is one.

     **Decided at this gate rather than deferred to the spec**, because the rule this
     repository states about second lists is that the comparing check is part of the same
     change — and because the evidence sits four lines from the prose being edited, where a
     README line naming a test script that has never existed went undetected for the life of
     the file.

     **What it does not prove, said now rather than discovered later:** that a named state
     corresponds to a branch that exists. The two documents and the header agree on *no
     approved plan*, and that phrase appears nowhere in the hook's executable body — only in
     its own header comment. `unplanned scope` is emitted and is already asserted by the
     existing hook tests. So the check holds the copies to each other and the existing suite
     holds one of the names to the code; the link for the other is documentation agreeing with
     documentation, which is worth having and is not the same thing.

   **What the verify-skill check still cannot prove**, named rather than implied: that the
   skill, at runtime, follows the branch it names. A prose check reads a document. That residue
   joins the known-and-unchecked entries rather than passing as covered.

   **Five rather than the four this document carried until the owner decided, and rather than
   the five first proposed — which were a different five.** The original count had the grammar
   as three classes and three rows; that collapsed to one class with four methods, because the
   register enumerates *classes of silent failure* and "a chain artifact's frontmatter is
   spelled in a form no reader accepts" is one class with four instances, the same granularity
   as the existing row for malformed definition frontmatter. Two checks were then new and in
   neither count: the one holding the one-way write, and this one.

   **The shape is load-bearing because the register check is weaker than it looks:** it reads
   only the first `[TestClass]` per file, scans the top directory only, and passes a class when
   either its own name *or any of its method names* is already registered. New file, one class,
   row naming the class is the one shape it actually sees.

## Constraints

- **Windows PowerShell 5.1** for the hook and the script — the only runtime present on every
  Windows machine with no install step.
- **The hook runs on every edit in every repository**, including ones that never adopted this
  framework. It must stay silent where the framework is not in use, and never throw.
- **One parser, shared.** The two PowerShell readers disagree in **four** measured ways —
  enumerated in the charter's parsers paragraph and deliberately not restated here — and this
  change makes both answer the same question about the same file. **It was five until
  2026-09-21**, when re-running the measurement showed that one of the five, leading dots, is
  not a disagreement at all: both readers strip them with the same call, so it is a defect they
  share and the lossless-normalisation constraint below is where it is fixed. The count
  mattered because it was the argument. A check reporting
  different collisions depending on which reader ran is worse than no check. **Root resolution
  travels with it**, because getting the root wrong produces an empty comparison silently, which
  is a larger failure than a misparsed manifest.

  **Where it lives is the spec's to choose; what it must not do is choose an extension two
  existing checks cannot see.** The check that keeps a stack out of shipped engine executables
  globs `*.ps1` and `*.js`, and the one that proves shipped PowerShell parses globs `*.ps1`.
  A shared parser shipped as a `.psm1` module — the idiomatic answer for exactly this job — is
  read by neither, which would put the engine's densest piece of parsing logic in the one file
  those two instruments do not cover. Either it ships as `.ps1`, or the globs widen in the same
  change. This is the same shape as the packaging constraint below: a placement decision that
  silently turns a check off is not a placement decision.
- **Every new check drives the shipped readers as external processes, the grammar check
  included.** The charter's technology table commits to it in one sentence — *"a
  reimplementation in C# would test the reimplementation, and the two would drift silently"* —
  and the grammar check is the one most tempting to write the other way, because a C# validator
  over fixture text is quicker to write than driving PowerShell. **It would also be a fourth
  consumer of the manifest grammar**, which is the exact trigger the charter's parsers paragraph
  arms: *if a fourth consumer appears, that is the moment to stop and give them one parser*. So
  the tempting shortcut is not merely weaker evidence, it is a charter question — and the
  answer that avoids asking it is to drive the readers, which is what the non-goals already say
  this check does.
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
  pasted into `plan.md` as a validation row. **The before-figure is re-measured on that same
  500-directory fixture**, not taken from the numbers above — those came from a flat tree and a
  900-directory one, and comparing 500-after against 900-before would show an improvement the
  change did not make.

  **That is a one-time measurement and nothing watches it afterwards, which is worth saying
  because this change moves the dominant cost term.** Root discovery goes into shared code, so
  a later regression there slows every edit in every repository and no instrument notices. A
  standing performance check is not proposed — a timing assertion in continuous integration is
  flaky by nature and this repository has no baseline machine — so the honest position is that
  the budget is enforced once, by the person reading the validation row, and not after.

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
  never existed is still fixed here, because it sits inside the gate-hook section this change
  rewrites.
- **No check reads a `plan.md` under this repository's own chain tree.** Every instrument this
  change adds runs over built fixtures — **including the grammar check**, which therefore proves
  the readers reject bad spelling and never inspects a real plan. A plan written with a
  block-style `files:` list fails when a reader runs, at plan approval or on an edit, and never
  in continuous integration. The closed question below reads as if the repository is held to
  conformance; it is not, and the measurement there says only that nothing needs migrating.

  **Stated that narrowly on purpose.** "No check reads the live chain tree" would be false:
  `SilentFailureRegisterTests` reads the live `.sdlc/CHARTER.md` and enumerates the live test
  directory, and it is a register row, so it has been mutation-tested by injecting a fault into
  that live file. **So the precedent for reading the live tree exists, and the argument against
  a live-plan check is not danger.** It is that after the marking this repository normally holds
  zero or one live plan: the check would be green by construction, and a check that cannot fail
  here proves nothing here. Stated because adding such a test is the obvious next move and the
  weaker version of this reasoning — "mutation-testing it would be dangerous" — does not
  survive contact with the register row that already does it.
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

**Closed at this gate on 2026-09-21, by the owner:** *do the shipped prose lists get a check
that holds them together, or an entry saying nothing does?* **A check, covering both lists.**
It could not wait for the spec, because either answer moved a number the charter edit commits
to — a check makes it five register rows, no check made it a sixth known-and-unchecked entry
and took that list from two to eight against four rows. The answer follows the rule this
repository has been bitten by four times and built five checks for: if a second list is
genuinely necessary, the check comparing it to the first is part of the same change. Recorded
here because the arithmetic was the reason to decide early, and a reader who finds only the
criterion would not know the question had been a blocking one. It is criterion 8's fifth check.

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
- **The symptom the change prevents.** Argued as a merge conflict found after both implementers
  finish. A merge conflict needs two branches or two worktrees, which the non-goals put out of
  scope — in the one tree the guarantee covers there is no conflict at all, just a later write
  winning silently. The document was arguing for the case it cannot reach.
- **"Every instrument runs over built fixtures."** False: an existing register row reads the
  live charter and enumerates the live test directory, and has been mutation-tested by injecting
  a fault into that live file. The true claim is narrower — no check reads a `plan.md` under the
  chain tree — and the weaker one had been doing load-bearing work, standing in as the reason a
  live-plan check was dropped.
- **Case-insensitive comparison.** Stated as something the filesystem forces. Git is
  case-sensitive and NTFS can be, so it is a chosen tradeoff: two genuinely distinct files
  collapse into one claim.
- **`superseded` being written by the plan skill.** Believed because the shipped schema says so.
  The skill has never contained the word. Found by interrogation, not by reading the document
  that asserts it — which is the whole argument for interrogating in a fresh context.
- **What the pre-approval marking commit touches.** Said to rewrite six plans *and the charter*.
  It cannot: register rows landing before their checks turn the register check red, and the
  charter edit belongs in the planned slice that declares it. Two rules broken by one clause.
- **Which disjointness invocation learns about `finished:`.** One criterion said "the
  disjointness script" where another said only the cross-plan mode — opposite instructions to
  anyone building from either alone, and the wider reading breaks the promise that
  `-Plan <path>` behaves exactly as it does today.
- **Precedent for holding the verify skill's prose.** Claimed an existing check requires that
  skill to name its options. The check reads the verifier *agent*; no instrument reads the
  verify skill body by name.
- **`Frontmatter.cs` as the grammar check's foundation.** The charter commits to tests driving
  the shipped artefacts as external processes, and a C# grammar validator would also be a
  fourth consumer of the manifest grammar — the charter's own trigger for stopping and
  extracting one parser. Named as the likely basis anyway.
- **`Repo.cs` walking "up to the plugin directory".** It walks to the repository root. Both hold
  a `.claude-plugin`, so the wrong name points at the wrong place.
- **"Five measured ways" the two parsers disagree.** Four. Leading dots is not one of them —
  both readers strip them identically, so it is a shared defect rather than a divergence. **The
  new part is how it was found:** by re-running the measurement on a figure this document had
  already labelled *measured*, which every other correction here had taken as settled. Measured
  is a claim with a shelf life; renewing it takes the command, not the memory of having run it.

Two decisions were reversed on evidence: a bulk-marking tool was dropped once its user population
was measured at one repository, once; and a check reading this repository's live chain directory
was dropped once it became clear it would be green by construction. **The second reason first
given for dropping it — that it could not be mutation-tested without writing into the live
tree — was itself wrong**, and is corrected in the non-goal above: an existing register row
reads the live charter and was mutation-tested by injecting a fault into it.
