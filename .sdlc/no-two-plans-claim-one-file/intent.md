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
> **The charter is at 19 and the four versions past 15 are not this chain's.** 16 swept every
> claim the charter makes about the code rather than waiting for a sixth reader to trip over
> one, and 17 rode the gate of `every-workflow-body-runs` — the chain that repaired the parallel
> build's wave loop and built the harness that executes a workflow body instead of parsing it.
> 18 corrected the divergence count this document quotes — three disagreements between the two
> readers, not four — and 19 removed two tallies that had gone stale within hours of being
> dated. **Only 18 moves anything this document says**, and it is followed here.
>
> **Where the intent/spec boundary actually sits in this document, admitted rather than
> claimed.** The rule below says an intent holds no implementation. This one fixes a label
> string, rewrites a shipped description, specifies a performance fixture down to its shape, and
> picks the winning reader on each of three parser axes. Each has a local reason — every one is
> text a user sees, a number the charter commits to, or a decision that changes shipped
> behaviour, and none of them is an algorithm. **But the rule as stated is not the rule being
> kept**, and pretending otherwise would leave the next writer guessing. The working boundary is
> narrower and worth saying plainly: *what a reader or a user is promised belongs here; how the
> code achieves it belongs in the spec.*
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
pairwise disjoint, and the plan skill runs it at a step before approval, with the plan still a
draft — which is not the same moment and matters later. The build does not run it — it
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
was where a plan's status came to rest; `superseded` was declared and honoured by nothing.
**Seven plans carry `approved` and every one of them is done or replaced** — six finished and
one replaced without ever being built. Comparing them the obvious way reports **40 file claims
over 30 distinct paths, with eight paths claimed by more than one chain**, the stack pack's
skill and the charter each claimed by three — and not one of those eight is a live collision.
**Seven, strictly**, once the charter is carved out of the comparison. A check reporting seven
false collisions on the day it lands is a check that gets switched off. The amendment settles
the marker; this change uses it.

**Re-measured on 2026-09-21 and the figures moved, which is the point of re-measuring.** It was
six plans, 35 claims and 26 paths a day earlier. A seventh chain shipped in between and added
five claims and four paths of its own, and **the number of multi-claimed paths did not move** —
new work tends to claim new files, and what collides is the small set every chain has to touch.
That is the shape of the problem stated in numbers rather than in adjectives.

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
  because a broken advisor on every keystroke is worse than a silent one. **Its header carries
  the first of the three copies of the warning-state list** — a two-row table naming
  *no approved plan* and *unplanned scope* — which this change adds a third row to, and which
  the fifth check holds against the other two copies from here.
- **`plugins/sdlc-core/workflows/parallel-build.js`** — gates on the literal value `approved`,
  reading it from the file through an agent rather than from its payload. **Its status gate was
  unreachable until 2026-09-21**, because the wave loop above it removed dispatched slices by
  comparing slice objects against a string id, so the set never shrank and no execution arrived.
  `every-workflow-body-runs` repaired that and built the harness that executes a workflow body
  instead of parsing it, so the gate now runs — against doubles, never yet against agents.
  **Out of scope here by choice, not by claim.** That chain is finished and its claim has
  lapsed with it, so nothing makes this file out of bounds — the non-goal below gives the
  reason that survives. Its `approved` reading will need to learn about `finished:` eventually;
  that is its own work.
- **`plugins/sdlc-core/skills/sdlc-router/references/artifact-schemas.md`** — carries the plan
  shape, including the `finished:` field and the five grammar rules settled at versions 9, 12
  and 21. **It is edited by this change**, not merely read: it says the grammar check "lands with
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

  **Two things about that four-copy list are worth stating rather than leaving to be found.**
  **It has not so much drifted as never agreed:** the four are four different spellings, not one
  literal with whitespace noise. The skill carries the full command; the schema carries the same
  thing with doubled spaces before `-File` and `-Plan`; the script's own example block omits both
  `-ExecutionPolicy Bypass` and `${CLAUDE_PLUGIN_ROOT}`; and a shipped chain's validation row is
  the bare script name with no runtime at all. An earlier version of this bullet reported only
  the doubled spaces, which understated it — and understating matters, because this bullet is
  offered as a third instance of the list-beside-a-list shape the fifth check now holds for two.
  Named here so the next reader finds it recorded rather than fresh. And **the new cross-plan invocation is
  prose in this skill with no instrument behind it**, which is the defect the direction
  diagnoses and which criterion 8 builds a prose check for in the verify skill. It gets no
  equivalent here. Whether it should is a spec question; that it does not is stated now.
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
  its test file is red and a test file without its row is red. **All five rows and all five new
  test classes therefore land in one commit**, which cannot be split across slices without
  leaving the tree red in between — and nothing may be committed on a red build. That makes it
  a constraint on the plan's shape rather than a detail of the register. The number is five
  everywhere in this document; it said four here until the owner's decision at this gate was
  carried through, which would have produced a commit of four rows and five classes and a red
  register check that nothing could be committed over.
- **`tests/AiSdlc.Tests/TempRepo.cs`, claimed by `init-scaffolds-solution`, and `Repo.cs`,
  claimed by nobody** — the fixture, which writes one chain with
  one plan and cannot express two colliding plans. Shared by three suites, so it sits in exactly
  one slice. **There is a second reason they are edited and it was implied rather than said:**
  the packaging check in criterion 8 injects a missing parser "in a fixture, never in the live
  plugin directory", and today these helpers resolve the hook and the script only to the live
  plugin paths. Injecting into a fixture therefore needs a copy of the plugin tree into a temp
  repository, which does not exist yet. Two reasons, one slice; naming only the first would
  leave the second looking like scope discovered late. `Repo.cs` resolves shipped scripts by walking **up to the repository root** —
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
  touches is the one that drifts. **It is claimed by `verification-reports-options`**, a
  finished plan, so if the spec ever does decide to edit it the collision set grows by one file
  and one claim. Reading a file is not claiming it, so it stays outside the count today.
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
  stale the day it does, in the same way the schema's copy does. **It carries a second forward
  reference of the same kind**, naming this chain as *"the change that extracts one parser for
  the first two"* — same paragraph family, same staleness the day this ships, and both are
  edited here. It takes **five register rows
  for the five new checks** — four until the owner settled the prose-list question at this
  document's gate — and **eight known-and-unchecked entries**, which was five until the
  invariant's own admission test was applied honestly to the last three.

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
  - **A prose check cannot prove a skill does at runtime what its body says.** The instrument is
    a harness that executes a skill body with its agent bound — the same missing harness that
    lets a workflow with a non-terminating loop parse clean, recorded as its own open item.
    Squarely a different change. **Worded at the level of the class rather than at one instance
    of it**: choosing the wrong branch in the verify skill and never making a call at all in the
    plan skill are two shapes of one failure and wait on one instrument. An earlier version named
    the verify skill alone, which would have left the second shape as the unlisted one.
  - **Nothing holds `finished:` one-way at all.** A person editing `true` back to `false`, or
    deleting the line, silently re-acquires every file that plan claimed — the failure
    criterion 2 calls irreversible — and no instrument sees it, because the grammar check reads
    a file's current state and has no memory. **This is the weakest of the five admissions and
    the one to argue at the gate**, because the instrument is plausible inside this change: the
    previous commit's copy of the file, compared against the current one. What stops it is that
    no check in this suite reads git history, and the hook cannot acquire the habit — it runs
    per keystroke, and shelling out to git on every edit is the cost this change is budgeting
    against. Those are reasons, not a law, and the owner may overrule them.
  - **A nested chain directory, a renamed `plan.md`, or a refused parse leaving the hook with
    nothing to compare** — three separate classes, admitted together because they share an
    instrument and a reason. Criterion 7 binds the script to tell "found nothing" from
    "compared nothing" and deliberately does not bind the hook, which must stay silent on every
    keystroke where the framework is not in use. So in the hook all three are invisible by
    design. **These were left off the list until 2026-09-21**, on the grounds that only the
    fourth cause — a wrongly resolved root — is touched by this change's own work. That is not
    the invariant's test. The test is whether the instrument is a different change, and it is:
    a hook that can report what it compared without speaking on every edit is a behavioural
    decision about a mechanism that fires constantly, and it deserves its own.

  **Three more entries against five rows moves a ratio the owner weighed an hour ago**, when
  settling whether the shipped prose lists get a check. Said out loud rather than absorbed,
  because the invariant holding this list says a list growing faster than the register is
  itself a finding — and a list that grows because someone applied its own admission rule
  properly is the good version of that finding, not the bad one.

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
  the hook's behaviour is pinned in `tests/AiSdlc.Tests/GateHookTests.cs`. A deletion would lose
  a true and useful pointer along with the false filename. **The file and not a count** — an
  earlier version of this bullet said "eleven methods", which was true when written and would
  have been false in the same commit, because criterion 6 adds one. The charter's own technology
  section says why: a hand-maintained tally in a long-lived artifact is wrong by the next commit.
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
- **The six finished plans and the one superseded plan** — `init-scaffolds-solution`,
  `pack-promises-what-it-scaffolds`, `register-names-every-check`, `stack-dotnet-house-stack`,
  `verification-reports-options`, `every-workflow-body-runs`, and
  `engine-substitutes-declared-tokens` — the last superseded in fact but not yet in its
  frontmatter, which still reads `approved`, and which this change corrects.

  **`every-workflow-body-runs` is on that list because the rule above puts it there, not because
  it feels done.** It was approved and shipped on 2026-09-21, after this document was written,
  and every file its manifest claims is in git with its work in it. An earlier version of this
  document called it "the only other live plan" an hour after its own commit landed — **which
  is the staleness this change exists to make visible, committed by the person writing the
  change that makes it visible.** Nothing caught it; a re-read against the tree did.
- **`.sdlc/OPEN.md` and `.sdlc/DIRECTION.md`** — both describe this change in the future tense
  and both are false the day it ships. The open items say nothing writes a task-tier plan's
  finished state, that a replaced plan is retired by nobody, and that closing the install item
  reopens this change's no-migration decision; the direction's first step says what this change
  will be. **Declared because the constraint below binds every artifact this change writes, and
  the artifact-root carve-out means no instrument would ever report them.** The one that most
  needs the edit is the item recording that an approved plan was produced from a draft intent —
  it exists precisely to outlive the marking that erases its machine-readable trace.
- **`plugins/sdlc-core/skills/sdlc-router/SKILL.md`** — reads `plan.md` and says not to write
  implementation code "until a `plan.md` exists and the user has approved it". **A finished plan
  still exists and still reads `approved`**, so the router's notion of an active chain keeps
  answering yes after the work is done. Named rather than edited: the non-goals already put the
  router's competing notion of an active chain out of scope, and the list's purpose is that
  omission is the lapse.
- **The tree holds seven plans and no other live one.** Every `plan.md` under the artifact root
  reads `status: approved`, and every one of them is done or replaced — which is why the
  enumeration above is both the marking set and, at this moment, the whole list. **That
  coincidence is temporary and the document does not rely on it:** the moment anything else is
  planned there will be a live plan this change must compare against, and nothing here assumes
  otherwise. Stated because a reader checking the tree will find seven approved plans and needs
  to know that is the state being fixed rather than the state being described.
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
  it. On a code edit it warns; where the plan skill runs its manifest check, it refuses.
  **Not "at plan approval"** — that is where the check runs *after* open question 1 is answered
  one particular way, and today it runs a step earlier, with the plan still a draft. An earlier
  version of this bullet asserted the answer while the question below called it open.
- **The gate hook is the guarantee**, with one qualification that belongs beside the word and
  not two hundred lines below it. It is the only mechanism that fires on its own, in any
  repository that merely installs the plugin; its bounds are three tools, code files only, and
  only the path being edited. **And it can switch itself off without saying so** — it wraps
  everything in a catch that exits 0 by design, so an exception inside the new comparison
  disables the gate for every file in the repository, silently and permanently. That is on the
  known-and-unchecked list and it is a larger caveat than the three bounds, because the three
  are limits on what it covers and this one is a limit on whether it runs at all. A guarantee
  that can quietly stop being one is still the best thing here, and saying so plainly is the
  difference between a guarantee and a claim.
- **Plan approval is a catch, not a guarantee.** It runs by an agent following an instruction,
  and that cannot be promised.
- **Fan-out is not covered**, and the reason changed on 2026-09-21. It used to be that the build
  had never run at all; it now runs, against doubles, because `every-workflow-body-runs` fixed
  its loop and built a harness. What is still true is that **it has never dispatched a real
  agent**. Covering fan-out is a scope decision and not something disjointness forbids: the
  chain that owned that file is finished, so nothing stands in the way but the size of the
  work. The non-goals say why it is declined.
- **All seven plans that are done or replaced say so**, so the check's first real run is true.
- **And it decays from there, by two separate routes, until a closing step exists.** A plan
  stops being live in two ways and **neither is written by a tool for the common case**:

  - **Finished.** Only feature chains get `finished:` written for them, by the verify skill on
    the SHIP branch of its gate. Five of the six that shipped here were task tier and pass through
    no closing stage at all, so their plans stay live after the work ships. That is recorded as
    item 4 in `.sdlc/OPEN.md`, with the closing step as its fix.
  - **Superseded.** The shipped schema says the plan skill writes this value when it replaces a
    plan. **It does not — the skill has never contained the word, and nothing under `plugins/`
    writes it.** So a replaced plan also stays `approved` forever, which is how the one
    superseded plan in this repository got that way. This change makes the schema say what is
    true, and marking stays a person's act.

  **And there is a third exit that is not an exit at all: a parked chain.** The charter says
  plainly that *a parked chain's plan is live — parked describes the chain, not the plan*, so a
  parked plan holds every file it claims for as long as it is parked, with no mechanism to
  release them and no reason to expect one. Four chains carry parked or blocked banners today
  and none has reached plan stage, so nothing is holding anything right now — but parking is
  ordinary practice here, not an exception, and the first parked *plan* is a live claim that
  will outlast everyone's memory of why. Named because the two routes above are described as
  though they were the whole set, and they are not.

  Both belong here rather than only in the open items, because the intent is what a fresh
  session reads, and because the outcome above promises the first real run is true. **It is
  true on the day it lands and decays afterwards at a rate nothing measures**, which is a
  weaker promise than "the report is trustworthy" and the one this change can keep.

  **And the decay ends where this change started, which has to be said rather than left for
  someone to notice.** The problem section rejects the direction's rule because *"a check
  reporting seven false collisions on the day it lands is a check that gets switched off."* If
  plans accumulate as live, the report returns to exactly that state — the same failure, on a
  delay.

  **The one measurement available argues for a slower decay than the fear suggests, and it is
  quoted here because it cuts against the point it was first offered for.** Over one day, one
  chain shipped: claims went from 35 to 40, distinct paths from 26 to 30, and **the number of
  multi-claimed paths did not move — eight both times.** A flat numerator over a growing
  denominator. New work mostly claims new files; what collides is the small set every chain has
  to touch. An earlier version of this paragraph read "eight of twenty-six, near a third" as
  evidence that successive plans overlap heavily, using a denominator the problem section had
  already retired. One observation is not a rate, and the one observation there is says the
  opposite of what it was used for.

  **The decay is still real and still unmeasured.** A finished plan that is never retired holds
  its claims forever, so false collisions accumulate monotonically while true ones do not. How
  fast that bites depends on how often new chains reach into the small shared set, and nothing
  counts it.

  **So the honest shape of this change is that it buys time, and the closing step is what
  spends it well.** Not a reason to hold it — the marking makes the report true now, the
  instruments are real, and a check that degrades beats a rule that could never have been
  switched on. **"Strictly" would overstate it, though, and the overstatement is worth
  removing:** the degraded state is not "no check". What someone reaches for when a report fills
  with noise is unsetting the blocking switch or ignoring the hook, and both of those also lose
  the `unplanned scope` gate that works today. Degradation is shared with shipped functionality
  rather than confined to the new part, which is a real cost and not one this comparison can
  assume away. But "how long until this is as bad as doing nothing" is a
  question with a real answer, nothing measures it, and an intent that left the reader to
  work it out would be hiding the weakest part behind the strongest.
- **Between chains, with no live plan, the hook warns on every code edit** — the gate working as
  designed, masked until now by plans that could not be retired. Recorded at charter version 9,
  including that the blocking switch then refuses every edit until a chain is started.

## Success criteria

Each states what breaks if the change is not made, because a criterion that still passes with
the headline change skipped is not a criterion.

**These are properties. The algorithms, return shapes, exit codes, message wording and file
placement that satisfy them are the spec's**, and it may choose them freely provided the
properties hold.

1. **Two live plans claiming one file is reported — at the moment one of those files is edited,
   or the plan skill runs its manifest check — naming the file and every plan claiming it,
   comparing paths the way the filesystem does.** Every claiming plan, not "the other chain": no
   mechanism here knows which chain an edit belongs to. **The trigger belongs in the sentence**,
   because without it this reads as a standing property, and the non-goals below say plainly
   that it is not — two live plans overlapping on ten files say nothing until one of the ten is
   touched or that check runs. An engineer implementing the literal reading would build
   something that scans on a schedule, which is not what this is.

   **"When the plan skill runs its check" and "at approval" are not the same moment today**,
   and which one this becomes is open question 1. The skill runs the manifest check at a step
   where the plan is still `draft` and approves at a later one, so as things stand the trigger
   is the earlier step — which is exactly the unguarded window that question is about. The
   criterion is written to the mechanism that exists rather than to the one the question might
   choose, so that it stays true either way.

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
   The seven plans it marks and the charter it declares all sit under the artifact root, so the
   most dangerous edits it makes are outside the comparison. Declaring them still buys the two
   things it always bought — the gate hook stays quiet for work that was agreed, and the
   declaration is the record of what this change touched — but it does not buy collision
   detection, and saying otherwise would be the false comfort this document keeps removing.
2. **A finished plan claims nothing; all seven done-or-replaced plans say so; and the write is
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
   Only one of the seven chains has a verification record; five shipped task-tier and passed
   through no closing stage at all, and the seventh was superseded without ever being built. The
   rule used here is not recollection: **a plan is finished when every file its manifest claims
   exists in git with that chain's work in it** — checkable by a person against the diff, which
   is what a reviewable commit is for.

   **What each of the seven actually carries afterwards, in one place, because reading criterion 2
   alone produces an illegal plan.** Six of them keep `status: approved` and gain
   `finished: true`. The seventh, `engine-substitutes-declared-tokens`, takes `status: superseded`
   and **no `finished:` line at all** — its work was never built, so there is nothing finished
   about it, and `finished: true` beside `status: superseded` is rejected by the fourth grammar
   rule this same change ships. A reader who took "all seven done-or-replaced plans say so" at
   face value would write exactly that and be refused by the instrument in the next slice.

   **It is about the plan's files and not the chain's
   residue**, which is worth saying because it marks `init-scaffolds-solution` finished while
   seven clauses of that chain's shipped spec have still never been exercised — recorded as the
   first of the open items. Both are true at once: the plan's work landed, and the command it
   describes has never been hand-run. A plan is finished when its files are written, not when
   every consequence of them has been observed, or nothing would ever be finished. Without it, the first real run reports seven collisions
   that are all false, and the check is switched off rather than trusted.
3. **The plan being checked contributes its claims whatever its own status says, and is excluded
   from the set it is compared against.** **This governs the plan under check; the liveness rule
   in criterion 5 governs every *other* plan.** Without that tiebreak the two criteria give
   opposite answers for a plan that is approved and finished and is itself the one being
   checked — and the single-plan invocation keeps today's behaviour, which reads no status at
   all, so finished-awareness lives only in the cross-plan mode.

   **The marking is the first slice of this plan, not a commit that precedes it — and that is a
   change from what this document said until 2026-09-21.** It said the marking must land before
   this plan is approved, and gave two reasons in turn. Both are gone.

   **The first reason was tooling** — that a later re-approval would be refused by the check this
   change ships. It does not hold: the check is not in the *installed* plugin at this chain's own
   approval moment, because plugins are cached per version and the bump is a late slice. Retracted
   a round ago.

   **The second was that charter invariant I6 is already violated the instant this plan is
   approved.** That does not hold either, and the charter says why in its own words: *a live plan
   is one whose work is agreed and not yet finished or replaced.* **Live is a property of the
   work, not of the frontmatter.** Six of those plans' work is in git and the seventh was
   replaced without being built, so none of them is live, whatever their `status:` line still
   says. Nothing is violated, and there is nothing for an early commit to make legal.

   **It was also wrong by a count, which is how the error surfaced.** The paragraph said "six live
   plans claiming five of this plan's files". Three plans claim them — `init-scaffolds-solution`
   holds the README, the engine plugin's version file and the fixture; `verification-reports-options`
   holds the schema reference and the verify skill; `engine-substitutes-declared-tokens` holds the
   schema reference too. Five files, six claims, **three plans**, and the document's own summary
   says so correctly two hundred lines above.

   **What the marking is actually for is criterion 2's reason, and that reason does not need a
   gate crossed early:** the report has to be true before the mechanism that reads it ships.
   **So the marking is slice one, everything else depends on it, and the whole question of an
   irreversible act taken before any gate authorises it disappears.** What the ordering must
   guarantee is only that no state exists in which the check is live and the marking is not,
   which `depends_on` expresses exactly.

   **Three things get better and one gets worse, and the one is worth naming.** Better: the act
   is inside an approved plan, so it is authorised the way everything else is; the owner is no
   longer asked to take an unrecoverable step at the intent gate and judge its consequences two
   gates later; and this repository never passes through a moment with **zero** live plans,
   which would have made the gate hook warn on every code edit and, under the blocking switch,
   refuse every one. Worse: the marking now sits behind this document's approval, so the
   artifacts keep saying seven plans are live for as long as the chain takes. That is the state
   today and has been for weeks, so it costs nothing new.

   **The one-way rule is not in tension with any of this.** It forbids reopening a plan whose
   work is finished. If a plan turns out to have been marked wrongly — its work did not in fact
   ship — putting it back is a correction of the record rather than a reopening of finished
   work, and the distinction is worth having written down before anyone needs it.

   **The charter is not in the marking slice.** The five register rows go in with their five
   checks, in the slice that declares `.sdlc/CHARTER.md`, because the version-8 rule puts a
   register row on the gate of the change that adds the check — and because rows naming tests
   that do not exist yet turn the register check red, which nothing may be committed over.

   **Both halves of this criterion are load-bearing.** Without the exclusion every
   *re*-approval refuses, because the plan is already approved on disk when the fix is
   re-checked — and re-approving after fixing an overlap is the path the invariant prescribes.
   Without the first half the check never fires on a *first* approval, because the plan is a
   draft at the moment it runs and a draft claims nothing.
4. **The check fires without anyone remembering it, in a repository that merely installs the
   plugin.** Without it this is a mechanism used by choice, which the direction rejected as the
   reason not to build a lock file. **The observable event:** the shipped hook, given an edit
   payload for **a code file both plans claim**, in a throwaway repository holding nothing but a
   charter and two colliding plans, writes the collision to standard error. **The extension is
   load-bearing rather than incidental** — the hook skips markdown, text, json, yaml and toml by
   design, so a fixture that collides on an artifact would prove it stays silent rather than
   that it speaks, and would pass while proving the opposite of the criterion. **One more
   property the fixture needs, for the same reason:** the hook skips any path with a segment
   equal to the artifact root's leaf, anywhere in it — so a code file under a directory that
   happens to be called `.sdlc` is skipped too, and a fixture built without noticing that would
   also pass vacuously. The plan skill's prose is not evidence for this — it
   is where a collision is usually caught and never what guarantees it.
5. **A plan that is approved and finished contributes no claims, in either reader, proved by
   an observable event rather than by a sentence.** The event: two plans claiming one code file
   in a throwaway repository, one of them marked finished; editing that file draws no collision
   warning, and the same fixture with the marking removed draws one. Criterion 4's shape, on the
   field instead of on the mechanism. **An earlier version of this criterion said only that each
   reader "has a stated behaviour, asserted rather than read"**, which names nothing anyone
   could write a test against and was the least testable line in this section.

   Both readers must treat an approved-and-finished plan as not
   live — **and, among the *other* plans, a `draft` or `superseded` one as not live either.**
   The qualifier matters: criterion 3 says the plan being checked contributes its claims
   whatever its own status says, precisely because it is a draft at the moment the check runs.
   Read without it, these two criteria give opposite answers for the commonest case there is.
   It is not a detail for a second reason too: one of the seven plans retires by `superseded`
   rather than by the new field, and the cross-plan mode is new code with no behaviour to
   inherit. The hook happens to get this right already,
   because its pattern demands the bare word `approved` and nothing else matches; the new reader
   must be told. The schema states the rule once — *a live plan is one that is `approved` and
   not finished; `superseded` is not live either* — and this criterion points at it rather than
   restating it. **The single-plan invocation is excluded and that exclusion is load-bearing:** it reads
   no status today, the Affected list promises it keeps working exactly as it does, and
   criterion 3 says finished-awareness lives only in the cross-plan mode. An earlier version of
   this criterion said "the disjointness script", full stop, which reads as an instruction to
   teach `-Plan <path>` to read status — breaking the one guarantee this change makes about
   behaviour it does not change.

   **This grew rather than shrank under the amendment's answer:** because `finished:` sits
   beside `status:` rather than replacing a value in it, `status: approved` still matches
   everywhere, so each reader must learn to check a second field. **The parallel build is
   excluded by a scope decision, and it is worth saying that twice over because the two
   reasons this document gave before have both lapsed.** Its gate can run now, since
   `every-workflow-body-runs` repaired the loop above it; and that chain is finished, so its
   claim on the file has lapsed too. **Nothing forces the exclusion — it is chosen**, for the
   reason the non-goals give, and an engineer reading this criterion alone would otherwise
   conclude disjointness compelled it.
6. **Editing a code file that more than one live plan claims produces a warning naming them** —
   and, under the blocking switch, refuses, like every other warning the hook emits. Saying
   "warns" alone would be false for anyone who has that switch set. Deliberately weaker than
   naming the other chain. Without it the hook's existing message reports only that *nothing*
   claims the file, which is the opposite state. The hook stops at the first claiming plan
   today, so moving that return is the change.

   **The new state gets a label, because this change has just made labels a contract.** The two
   that exist are `no approved plan` and `unplanned scope`; the third is **`contested file`**,
   and it goes in all three copies the fifth check compares. Without one, the criterion adds a
   row to three shipped documents with nothing to compare it on — the exact defect that made the
   `hooks.json` description need rewriting.

   **And it gets an action string, which is where it differs from its two neighbours.** Each of
   those tells the editor something they can do alone: run the plan command, or add the file to
   a manifest and re-approve. **A contested file cannot be remedied by the person editing it** —
   the fix is re-planning a chain that may not be theirs, and a non-goal here forbids any tool
   doing that unattended. Under the blocking switch that file is uneditable by anybody until a
   human intervenes elsewhere. So the action names the other chain and says to settle it with
   whoever owns it, rather than implying a fix the reader can perform. **Stated at this gate
   because it is text every user sees and a dead end nobody has been shown before.**
7. **In the script's cross-plan mode, a comparison that found nothing is distinguishable from
   one that compared nothing.** **The mode is named because the distinction is load-bearing
   everywhere else in this document**: `-Plan <path>` keeps its output and its exit codes
   exactly, and two of the four causes below — a nested chain directory, a renamed `plan.md` —
   exist only when more than one plan is being read. Implemented against the script as a whole
   it would add counting output to the single-plan mode and break the one behavioural promise
   this change makes. The constraint below fixes one cause of an empty set — a wrongly resolved root —
   but a nested chain directory, a renamed `plan.md` or a refused parse produce the same silence
   and report green. The single-plan script already models it, printing what it counted. Without
   this, the check's most reassuring output is also what it prints when it is broken.

   **It binds the script and deliberately not the hook, and that is a cost decision rather than
   an impossibility.** The hook writes only when it has something to say and is otherwise silent
   by contract, so a hook that compared six plans and a hook that compared none produce
   identical output — nothing. Making it announce its counts would put a line on standard error
   on every edit in every repository, which the constraints below forbid.

   **What that leaves, stated as a debt rather than as a difference.** All four causes above
   apply to the hook exactly as they do to the script, and the hook is the mechanism this
   document calls the guarantee. The known-and-unchecked list admits **one** of the four for it
   — the wrongly resolved root — and the reason given was that it is the one this change's own
   normalisation work touches. **That is not the invariant's test and the three are admitted
   properly below**: the rule is that a class belongs on the list when its instrument is a
   different change, which is true of all four, and "we happened to be near this one" is not a
   qualification. Listing them honestly takes the list from five entries to eight against five
   rows, which moves the ratio the owner weighed when settling the prose-list question at this
   gate — and saying so is the point of a rule that calls a fast-growing list a finding. The
   middle option nobody is taking is a hook that speaks only when it compared nothing *and*
   found a chain directory it could not read; it is not proposed here because it is a new
   behaviour on a mechanism that fires on every keystroke, and that deserves its own decision
   rather than riding in on this one.
8. **Five checks, each a new test class in its own file, each named by its own register row,
   each seen to fail when its fault is injected.**

   - **The comparison**, proved over built fixtures.
   - **The packaging check** — the shipped hook still speaks when its extracted parser is
     present, and fails when it is removed. Injected in a fixture, never in the live plugin
     directory.
   - **The frontmatter grammar**, covering all five rules: a `files:` entry is a quoted string
     on its line, it is a file path rather than a directory or glob, `status:` is unquoted and
     one of the declared values, and `finished:` is `true` or `false` alone on its line — with
     **`true` requiring `status: approved`, `false` legal anywhere, and absent meaning not
     finished**. The last three clauses are load-bearing: an earlier version of this criterion
     said the *field* was approved-only, which makes the schema's own plan template illegal
     (it carries `finished: false` on a draft, because a plan is authored as one) and, read
     literally, requires the field on every plan when not one carries it today.

     **It binds `plan.md` and no other shape.** Two of the five rules are plan-only by the
     schema; the other three — the `status:` value, and the singularity of both lifecycle
     lines — are stated there for all five artifact shapes, and binding the check to all five
     would be a wider promise than this change measured, since the conformance measurement
     covered the seven plans and nothing else. **The fifth rule is the one this most strains**,
     because an artifact carrying two `status:` lines is as dangerous in a spec as in a plan;
     widening the check is a smaller job than this document's scope allows and belongs with
     whoever next touches the grammar. **The charter and verification records are outside it**, said plainly
     because the non-goal that no check reads a real `plan.md` means nobody can settle the
     question by running something.

     **The same hole existed for `status:`, it was the worse of the two, and charter version 21
     closed it.** Nothing used to say an artifact carries exactly one `status:` line, and every
     reader matches *a* line rather than *the* line — the hook's pattern is unanchored to
     position — so a plan carrying `status: draft` and, further down, `status: approved` read as
     approved to the hook, to the new cross-plan reader, and to anything else that looked. **A
     route to producing downstream work from a draft that no instrument would ever have seen**,
     in a repository that already has one plan approved over a draft intent recorded as an open
     item.

     **It took an amendment rather than a decision here, and that distinction is the point.**
     The other four rules legalise nothing that ever worked; this one rejects an artifact the
     shipped readers accept today, which narrows a shape the charter freezes and the schema
     publishes. The version-8 rule puts a narrowing in its own approval conversation. **An
     earlier version of this criterion simply decided it**, which would have shipped a check
     enforcing five rules while two documents enumerated four — the version-12 defect exactly,
     in the same paragraph. The owner amended instead, so the grammar check covers five rules
     and both documents say five.

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
     reference and the verify skill. **The three agree today and this
     change does not touch the instruction**; what it does is edit two of the three files, for
     unrelated reasons, which is precisely the situation in which the untouched third drifts.

     **This list needs a canonical form too, and unlike the hook's it already has one.** The
     two conditions are *files changed that no slice claimed*, called **unplanned scope**, and
     *files claimed but untouched*, meaning a slice that did not land. All three copies carry
     both phrases today — checked, not assumed — so the comparison has a key without anything
     being rewritten. The hook's warning states needed one invented because the shipped
     description paraphrased both labels away; this one did not, which is why only one of the
     two lists costs a change to text users see.
     One class, two lists, because the register enumerates classes and "a shipped description of
     an engine behaviour disagrees with the other copies of itself" is one.

     **Decided at this gate rather than deferred to the spec**, because the rule this
     repository states about second lists is that the comparing check is part of the same
     change — and because the evidence sits four lines from the prose being edited, where a
     README line naming a test script that has never existed went undetected for the life of
     the file.

     **The three copies have no shared key today, and making one is part of this.** The hook's
     header and the README both use the labels `no approved plan` and `unplanned scope`; the
     `description` in `hooks.json` — the copy that travels furthest — paraphrases them as
     *"either no plan is approved, or no slice claims the file"* and contains neither label.
     A both-directions comparison needs something to compare on, so **the labels are the
     canonical form and the shipped description is rewritten to carry them.** That is a change
     to text every user sees, which is why it is stated here rather than left to the spec. An
     earlier version of this criterion asserted the three already agreed on the labels; they do
     not, and the check would have been unbuildable as written.

     **The cost of that decision, stated with it:** the two labels become a contract across
     three files, so a fourth warning state later costs three edits and the check, in text that
     ships to every user. Accepted — three edits a reader is forced to make together is the
     whole point, and the alternative is three descriptions that quietly stop describing the
     same thing.

     **What it does not prove, said now rather than discovered later:** that a named state
     corresponds to a branch that exists. `unplanned scope` is emitted by the hook and already
     asserted by the existing hook tests; `no approved plan` appears nowhere in the hook's
     executable body, only in its own header comment. So the check holds the copies to each
     other, and the link from a label to the code exists for one of the two names. That is
     worth having and it is not the same thing.

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
- **One parser, shared, because one question about one file should have one answer.** That is
  the property; the count of things wrong with the two readers today is evidence for it and is
  not the argument. **Five defects, enumerated in the charter's parsers paragraph and not
  restated here:** three where the two readers genuinely disagree, one where the hook disagrees
  with itself, and one they share. A check reporting different collisions depending on which
  reader ran is worse than no check, and so is a reader that answers differently depending on
  which side of its own comparison a path arrived on.

  **The count was stated as a divergence count and moved twice — five, four, three — which is
  why it is not the argument any more.** Each recount found another item that was a real defect
  and not a disagreement: leading dots, which both readers strip identically, and the edited
  path, which only the hook has. Reframed as *one question, one answer*, there is nothing left
  to recount. **Root resolution travels with the parser**, because getting the root wrong
  produces an empty comparison silently, which is a larger failure than a misparsed manifest.

  **Which reader's behaviour becomes the shared behaviour, said for each rather than left to
  the spec.** On all three disagreements it is the script's: it anchors `files:` to the start of
  a line where the hook matches the substring anywhere, so a `profiles:` key feeds the hook
  claims that are not claims; it collects a path only inside a slice where the hook has no
  notion of slices; and it compares case-insensitively, which criterion 1 already adopts. The
  fourth, the hook's own asymmetry, has no competing behaviour to choose between — the shared
  parser normalises the edited path the same way it normalises a claim, which is the whole
  point.

  **So the parser is an extraction and the hook's behaviour change is the price**, and calling
  the whole thing an extraction would be the comfortable half of that sentence. Nothing new is
  invented: the shared reader is the script's reader, lifted. But **the hook gets stricter on
  three axes at once**, and one consequence is worth stating because it is a behaviour change to
  a shipped advisor. A plan that today gets claims recognised by the loose reading — a `files:`
  line outside any slice, or one the script's anchor would reject — loses them, so files the
  hook quietly treated as claimed start drawing an unplanned-scope warning. **Noisier, not less
  safe**, and it cannot happen in this repository — **measured on the axes that actually move,
  not borrowed from the grammar measurement.** Parsing all seven manifests both ways, anchored
  and inside a slice against unanchored and context-free, yields the identical set of forty
  claims: nothing the loose reading sees is lost by the strict one. An earlier version of this
  sentence cited the conformance check, which covers quoting and directory entries and says
  nothing about either axis here. It can happen in another one, which is where the no-migration
  non-goal has nothing to say, because this is not version skew — it is the same version
  reading the same file more carefully.

  **And "`-Plan <path>` keeps working exactly as it does today" means the invocation, the output
  and the exit-code vocabulary — not the verdict on every input.** The lossless-normalisation
  constraint below changes that mode's answer on real plans, in both directions, and it has to:
  today `.sdlc/CHARTER.md` and `sdlc/CHARTER.md` collapse onto one string and are reported as an
  overlap that is not one, while `a//x` against `a/x` and `a/../a/x` against `a/x` are two
  strings and are not reported when they are. **Both of those verdicts are wrong today and both
  change.** The promise is that nobody's invocation breaks and no output has to be re-learned;
  promising the same verdict would be promising to keep the defect.

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

  **In the script that means a named refusal; in the hook it means something weaker, and the
  difference is not a fourth warning state.** The hook must never throw and must say nothing it
  does not have to, so a refused claim there is simply not a claim — and the file it belonged to
  then appears unclaimed, which draws the existing `unplanned scope` warning. **Loud, and
  labelled with the wrong reason.** That is the honest description: the hook does not go quiet
  on a refused claim, it misattributes it, and a reader chasing the warning finds a manifest
  that looks complete. Worth stating because the alternative reading — that a refusal needs a
  state of its own — would add a fourth label to three shipped documents and to the check that
  compares them, which is a cost this change does not take.
- **A refused plan must never silence the hook.** The hook wraps everything in a catch that exits
  0 by design and has no per-plan containment, so a parser throwing on one bad plan would disable
  the gate for every file in the repository, permanently and without a word. The caller contains
  each plan.

  **Nothing proves that it does, and the admission is two hundred lines away, so it is repeated
  here.** Making shipped hook code throw on demand needs a harness this repository does not
  have; the packaging check reaches only the nearest case, a missing parser file. So this is a
  constraint held by prose, in the mechanism this document calls the guarantee — which is the
  defect class the direction diagnoses, stated about itself.
- **A stated time budget, because "stay fast" is not falsifiable.** Measured 2026-09-20: the
  shipped hook takes 306ms with six plans in a flat tree, 317ms with ten, and **1188ms with six
  plans in a tree of 900 directories**, against ~110ms for bare PowerShell startup. Plan count
  costs about 2.75ms each; **root discovery dominates and scales with repository size**, which is
  the term this change relocates. The budget: **no regression beyond the greater of 15% or
  50ms**, median of five runs, measured before and after on one fixture and pasted into
  `plan.md` as a validation row.

  **The fixture is specified by shape, not by a count, because the count alone does not
  reproduce.** Root discovery globs one and two levels below the working directory, so its cost
  is driven by **breadth at those two levels** and not by depth or by total directories: 500
  directories side by side at the top and 500 nested ten deep give different numbers on the same
  code. The fixture is **500 directories at the top level, holding seven plans** — seven because
  that is what this repository has and plan count is the other measured term, at roughly 2.75ms
  each. **The before-figure is re-measured on that same fixture**, not taken from the numbers
  above: those came from a flat tree and a 900-directory one, and comparing a 500-after against
  a 900-before would show an improvement the change did not make.

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
- **This change writes seven other chains' plans, the charter, the shipped schema, the open
  items and the direction, and must declare every one.** Editing another chain's artifact
  without declaring it is the lapse this change exists to catch, and the artifact-root carve-out
  means no instrument would ever report the omission.

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
- **Nothing rewrites a plan automatically.** The seven are marked by a person in a reviewable
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
- **The hook's tool matcher is not widened.** A `git mv` still never reaches it. **And "three
  tools" is this repository reading an alternation, not a behaviour anyone pinned:** the matcher
  is `Edit|Write|NotebookEdit`, unanchored, and whether the host also matches a name containing
  one of those — a multi-edit variant, say — is a mechanic nothing here documents or tests. It
  is the same shape as the admitted gap about the hook's base directory: a claim about the host
  rather than about this code. Widening the matcher deliberately is out of scope; knowing that
  the number three was never verified is not.
- **`plugins/sdlc-core/workflows/parallel-build.js` is not edited here and the JavaScript
  parser is not reconciled.** This was an open question until 2026-09-21. **The reason first
  given for closing it has since lapsed and the decision survives on a better one.** That reason
  was that `every-workflow-body-runs` claims the file — but that plan is finished, so its claim
  has lapsed with it, and a finished plan cannot be the argument for anything being out of
  bounds.

  **What the argument actually is:** the lossless-normalisation constraint moves the two
  PowerShell readers *toward* the JavaScript, which is already correct on the case that matters
  — it strips one optional leading `./` and leaves `.sdlc/CHARTER.md` intact, where the
  PowerShell strips every leading dot and slash and destroys it. So the gap that costs this
  change something closes as a side effect. What remains is the rest: the JavaScript does not
  fold `a//x`, a trailing slash, `a/../a/x`, case or surrounding whitespace, and nothing holds
  the three readers together in either direction. **Reconciling that is a third reader in a
  third language with a check to match**, and it now has an instrument it did not have this
  morning — the harness that executes a workflow body. It is its own work, and the thing that
  makes it real work is no longer unavailable, which is a better place to leave it than the
  scope argument this bullet used to make.

  **What stays true and unfixed:** the comment in that file claiming all three parsers normalise
  identically is false, and stays false after this change — less false, which is not the same
  thing. Recorded for whoever reconciles it.
- **The scaffold executor's duplicated root resolver and the router's competing notion of an
  active chain are not fixed here.** Each is its own work.
- **The plan skill gains no new gate and no new step.** The open question about where the
  cross-plan check runs may be answered "at the approval gate", and if the answer needs a step
  that does not exist, that is a change to the tier contract and a different chain. This change
  adds an invocation to a step that already exists, or it does not land here.
- **No collision check runs at the intent or the spec gate.** Files are declared at plan time,
  so there is nothing to compare at either — stated because the document names plan approval and
  edit time repeatedly and never forecloses the other two.
- **`slug:` is not pinned to the chain directory name.** The report names a chain by its
  directory, the two are not held together by anything, and making them so is a different change
  — said as a non-goal because saying it in prose reads as an invitation.
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

- **Can the approval window be closed without a new step, and if not, is the window accepted?**
  The skill writes the plan as a draft, runs the single-plan check, and approves later. Running
  the cross-plan check at the existing early step leaves that window unguarded: two sessions
  both past it and neither past the gate each see the other as a draft, and both pass.

  **The question is narrower than it was, because a non-goal above forecloses half of it.**
  "Running it at the gate" means running it at a step that does not exist, and this change adds
  no step to the plan skill — so that branch is out of scope here by a decision already taken,
  not by argument. **An earlier version of this question offered both branches while that
  non-goal ruled one out**, which made the document contradict itself about its own scope.

  What is left is genuinely open: accept the window and record it as a limitation, or find
  something that narrows it inside the step that exists — re-running the check as the last act
  before the status line is written is the obvious candidate and is not free, because it is
  prose in a skill with no instrument behind it. *Owner: peter, at this document's gate, because
  the answer decides whether a limitation gets written down or a mechanism gets specified.*
- **Does shrinking the live set make the hook noisy enough to want softening?** Today's claims
  keep it quiet on those paths; after the marking, code files only finished plans claimed begin
  warning on every edit. Correct by the gate's definition, and a behaviour change to something
  that fires on every keystroke. *Owner: peter, at the spec gate, on measured output.*
**Closed on 2026-09-21 as a scope decision, after the reason first given for closing it
lapsed:** *does reconciling the JavaScript parser belong here?* **No, and it is a non-goal
above rather than an open question.** The first answer leaned on `every-workflow-body-runs`
claiming `parallel-build.js` — but that plan is finished and its claim lapsed with it, so
nothing about disjointness forbids touching the file. What decides it is size and sequence,
which the non-goal sets out. The reason for asking has not gone away: that file's comment
claiming all three parsers normalise identically is false today and is less false, not false-free,
after this change.

**Closed at this gate on 2026-09-21 by an amendment, not by an answer:** *does the frontmatter
grammar gain a fifth rule — exactly one `status:` line — and therefore a charter amendment?*
**Yes to both, as charter version 21.** The question could not be settled here whichever way it
went: adding the rule narrows a shape the charter freezes and the schema publishes, and under
the version-8 rule a narrowing takes its own conversation. **What made it worth the version** is
that every reader matches a line rather than the line, so `status: draft` followed by
`status: approved` read as approved to all of them — the quietest route this repository has to
producing downstream work from a draft, against the invariant written to forbid it, with nothing
that would ever have noticed. Counted before it was written: all seventeen chain artifacts
already conform, so nothing needs migrating. The `finished:` half rides along and takes nothing
away, since no plan carries that field yet.

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
plans that already exist?* Measured again on 2026-09-21 — **all seven conform**, the seventh
included, which was written after the question was first answered for six. Every `status:` line is the
unquoted word, every `files:` line is a quoted flow-style list, and no entry is a directory; and
no plan carries `finished:` at all, which the field's own rule reads as not finished.

**Measured a third time when the fifth rule arrived, and over a wider set**, because that rule
binds every artifact shape rather than plans alone: all **seventeen** chain artifacts carrying
frontmatter have exactly one `status:` line and no more than one `finished:` line. So all five
rules break nothing in this repository and can be enforced from the day they land — including
the one that takes something away, which is the only one where that answer was not obvious in
advance. Recorded because the question was worth asking three times and the answer took one
command each time.

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
- **The parallel build's status gate.** Called untested; it was *unreachable*, which was a
  different and stronger claim — and it stopped being either one on 2026-09-21, which is the
  next entry but one.
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
- **The whole argument for an irreversible commit before any gate.** Two reasons were given in
  turn and both were wrong. The tooling one did not hold. The invariant one contradicted the
  charter's own definition — *a live plan is one whose work is agreed and not yet finished* makes
  live a property of the work, so six plans whose work is in git were never live and nothing was
  violated. It was also wrong by a count: three plans collide with this one, not six. **The
  marking is now the first slice of the plan and the entire problem disappears** — no ungated
  act, no reversal question, and no moment when this repository holds zero live plans and the
  hook refuses every edit.
- **Three silent-failure classes left off a list by a test the invariant does not state.** They
  were kept off because this change happened not to touch them, where the rule is whether their
  instrument is a different change. Five entries became eight.
- **A retracted reason still governing four sections.** The parallel build's exclusion was
  argued from a claim that lapsed the moment that chain finished. The exclusion survives as a
  scope decision; it was reading as a constraint in a success criterion.
- **Calling a plan live an hour after committing its work.** `every-workflow-body-runs` was
  described here as "the only other live plan" while every file it claims was already in git,
  and two decisions rested on that claim. **Found by re-reading the document against the tree
  rather than by argument**, which is the pass that now runs before an interrogation instead of
  after five of them.
- **Every count in this document, which is why they are now derived rather than written.** Seven
  plans, not six. Forty claims over thirty paths, not thirty-five over twenty-six. Five of six
  shipped chains task tier, not four of five. Every one was true when written and false within a
  day, and every one was a command away.
- **Counting divergences at all.** Five, then four, then three — and each recount removed an
  item that was a real defect but not a disagreement between the two readers. The frame was
  wrong, not the arithmetic: what the shared parser is for is that one question about one file
  has one answer, which two readers differing violates and one reader disagreeing with itself
  violates equally. **Found by reading for bad reasoning rather than for false facts**, after
  five consecutive charter versions spent on the second kind.
- **"At plan approval it refuses."** Asserted in the outcome while the open questions called the
  same thing undecided. Today the check runs a step earlier, with the plan still a draft.
- **"The gate hook is the guarantee", with the caveats two hundred lines away.** Its three bounds
  were beside the word; the one that matters more — that an exception disables it silently and
  permanently — was not.
- **Calling the whole parser change an extraction.** The parser is extracted; the hook's
  behaviour change is real and is the price, and it gets stricter on three axes at once.
- **"The parallel build's status gate is unreachable."** True when written and false a day
  later: another chain repaired the loop above it and built the harness that runs a workflow
  body. Three places in this document said it. **Not a mistake so much as the thing this change
  is about** — a claim about a file a second chain was working on, with nothing to tell either
  side.
- **Which reader's behaviour the shared parser adopts.** Absent, while the same section promised
  one of the two readers does not change. All four resolve the script's way; what the promise
  about `-Plan <path>` protects is the invocation, the output and the exit codes, not the
  verdict — the verdict changes exactly where today's is wrong.
- **What the superseded plan's frontmatter becomes.** Never stated. The obvious reading of one
  criterion produces `finished: true` beside `status: superseded`, which the grammar rule this
  same change ships would reject.
- **"Eleven methods" in the README's replacement line.** True when written, false in the same
  commit, and a hand-maintained tally beside its own source of truth — the thing the charter
  tells this repository not to write.
- **The performance fixture, specified by a count.** Root discovery scales with breadth at the
  top two levels, so "500 directories" does not reproduce until it says where they sit.
- **Why the marking commit goes first — twice, and the second answer was no better than the
  first.** It was argued from tooling, that a later re-approval would be refused by the check;
  it would not. Then from the invariant, that approval beside live plans violates disjointness;
  those plans were never live, because the charter makes *live* a property of the work and their
  work is in git. The entry above records where that ended: the marking is the first slice of
  the plan and needs no justification for going early, because it no longer does.
- **That the three warning-state copies already agree on the labels.** They do not — the
  shipped `hooks.json` description paraphrases both and contains neither, so the check had no
  key to compare on. Asserted the day after deciding to build the check, which is the shortest
  distance yet between a claim and its refutation in this document.
- **Four rows where the rest of the document said five.** In the one bullet that states the
  slicing constraint, so an engineer following it would have produced the red build the same
  bullet says nothing may be committed over.
- **What the marking commit touches.** Said to rewrite the other plans *and the charter*. It
  cannot: register rows landing before their checks turn the register check red, and the charter
  edit belongs in the slice that declares it. Two rules broken by one clause — and worth keeping
  in this list even though the commit has since moved inside the plan, because the constraint on
  what it may touch survived the move.
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
