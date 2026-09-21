---
slug: no-two-plans-claim-one-file
status: draft
spec: ./spec.md
tier: feature
slices:
  - id: marking
    files: [".sdlc/init-scaffolds-solution/plan.md", ".sdlc/pack-promises-what-it-scaffolds/plan.md", ".sdlc/register-names-every-check/plan.md", ".sdlc/stack-dotnet-house-stack/plan.md", ".sdlc/verification-reports-options/plan.md", ".sdlc/every-workflow-body-runs/plan.md", ".sdlc/engine-substitutes-declared-tokens/plan.md"]
    requirements: [R31, R32, R35, R49]
    depends_on: []
  - id: parser
    files: ["plugins/sdlc-core/scripts/SliceManifestParser.ps1"]
    requirements: [R1, R2, R3, R4, R5, R6, R7, R8, R9, R10, R11, R12, R13, R55, R56, R57, R60]
    depends_on: [marking]
  - id: fixtures
    files: ["tests/AiSdlc.Tests/Repo.cs", "tests/AiSdlc.Tests/TempRepo.cs", "tests/AiSdlc.Tests/ScaffoldManifestTests.cs"]
    requirements: [R46, R48]
    depends_on: [marking]
  - id: hook
    files: ["plugins/sdlc-core/hooks/sdlc_gate.ps1", "plugins/sdlc-core/hooks/hooks.json", "tests/AiSdlc.Tests/GateHookTests.cs"]
    requirements: [R14, R15, R16, R17, R18, R19, R20, R21, R22, R23, R39, R43, R55, R58, R62]
    depends_on: [parser, fixtures]
  - id: script
    files: ["plugins/sdlc-core/scripts/Assert-SliceManifest.ps1", "tests/AiSdlc.Tests/SliceManifestTests.cs"]
    requirements: [R24, R25, R26, R27, R28, R29, R30, R53, R55, R59, R61]
    depends_on: [parser, fixtures]
  - id: prose
    files: ["plugins/sdlc-core/skills/plan/SKILL.md", "plugins/sdlc-core/skills/verify/SKILL.md", "plugins/sdlc-core/skills/sdlc-router/references/artifact-schemas.md", "plugins/sdlc-core/.claude-plugin/plugin.json", "README.md", ".sdlc/OPEN.md", ".sdlc/DIRECTION.md"]
    requirements: [R33, R34, R36, R37, R38, R40, R41, R42, R44, R45, R54, R58]
    depends_on: [script]
  - id: instruments
    files: ["tests/AiSdlc.Tests/CrossPlanCollisionTests.cs", "tests/AiSdlc.Tests/GateHookPackagingTests.cs", "tests/AiSdlc.Tests/FrontmatterGrammarTests.cs", "tests/AiSdlc.Tests/VerifyShipBranchTests.cs", "tests/AiSdlc.Tests/ShippedProseListTests.cs", ".sdlc/CHARTER.md"]
    requirements: [R42, R46, R47, R48, R50, R51, R52]
    depends_on: [hook, prose]
---

# plan — no two live plans claim one file

**Tier: feature.** Seven slices from an approved `spec.md`, requirements `R1`–`R62`.

## This is a sequential build, and saying so is the first honest thing in this document

Five waves, never more than two slices at once. **The manifest's value here is collision
prevention, not speed** — that is worth stating, because an earlier version of this plan claimed
one real fan-out wave of four and the claim was wrong in a way that would have cost a red build.

**Three of those four wrote into the same C# project.** There is exactly one — `AiSdlc.Tests` —
so the mitigation that version offered, *"each scopes its build and test commands to its own
project"*, named an action that does not exist here. And the hazard was never a file lock: it was
compilation. `TempRepo.cs` is compiled by `GateHookTests`, `SliceManifestTests` **and**
`ScaffoldManifestTests`, so a fixture change red-lights sibling slices whose commits are supposed
to be independently green.

The repair is ordering rather than cleverness: the fixtures settle before anything compiles
against them.

## Why the slices fall where they do

**`marking` is first and everything depends on it.** Seven plans read `approved` while their work
is in git; until they say so, five of the files this change edits are claimed by three other
plans. One slice, because it is one reviewable act.

**`fixtures` moves up beside `parser`, not into the wave that uses it.** `Repo.cs` and
`TempRepo.cs` are compiled by three suites, one of which — `ScaffoldManifestTests` — this change
has no other reason to touch. It is claimed here anyway: a fixture change can break it, and
repairing it out-of-manifest during a build whose entire subject is that nobody edits outside
their manifest would be the worst possible demonstration.

**`prose` depends on `script`, not on `parser`.** It writes the `-AcrossPlans` invocation into
the plan skill and the schema — a flag `Assert-SliceManifest.ps1` does not accept until `script`
lands. No test compares a skill's command line to a script's parameter block, so the tree stays
green either way; the shipped plugin would simply document a command that fails.

**The charter's register rows and the five new test classes cannot be separated.** The register
check reads `.sdlc/CHARTER.md` and the test directory in both directions: a row naming a class
that does not exist is red, and a class with no row is red. Only one slice may claim the charter,
so both halves are `instruments`, and it is last.

**Each shipped file lands with the test that pins it.** `GateHookTests` in `hook`,
`SliceManifestTests` in `script` — both slices change behaviour those suites already assert.

## Work order

```
wave 1: marking
wave 2: parser, fixtures
wave 3: hook, script
wave 4: prose
wave 5: instruments
```

**Wave 1 — `marking`.** Six plans keep `status: approved` and gain `finished: true`;
`engine-substitutes-declared-tokens` takes `status: superseded` and no `finished:` line, because
its work was never built and `finished: true` beside `superseded` is refused by the grammar this
change ships. Manifests are left exactly as they are. The commit names, for each plan, the commit
that landed its files.

**`R49`'s two before-figures are measured in this wave and pasted then.** They measure the
*current* `sdlc_gate.ps1`, which `hook` replaces in wave 3 — so taken any later they cannot be
taken at all, and the spec forbids reusing the earlier flat-tree and 900-directory numbers. An
earlier version of this plan listed all four medians under final validation, by which point the
artefact being measured no longer existed.

**Wave 2 — `parser` and `fixtures`, independent of each other.**

- **`parser`** — `SliceManifestParser.ps1`: frontmatter reading, path normalisation, liveness,
  root resolution, refusals as data. `R1`–`R13`, plus containment under the artifact root
  (`R55`), plan-level refusal making a plan not live (`R56`), a `files:` line before the first
  slice dropped rather than refused (`R57`), and a nested `finished:` line refused (`R60`).
- **`fixtures`** — **the contract, stated here because no spec requirement describes it.** Two
  additions, both additive rather than changes to existing defaults: a factory producing a
  repository with **two plans claiming one code file**, and a copy of the plugin tree into a temp
  repository so the packaging check injects into a fixture rather than the live plugin
  directory. `TempRepo.Create`'s existing signature and default behaviour do not move, which is
  what keeps `ScaffoldManifestTests` green. `R48` is claimed here as well as in `instruments`,
  because it is a direct constraint on what these helpers may do: `Repo.Root` points at the live
  tree and is right there.

**Wave 3 — `hook` and `script`.**

- **`hook`** — collect every claimant instead of returning at the first; `contested file` with an
  action naming the other chain; the header rewritten to three labels with the false timing claim
  removed; `hooks.json`'s description carrying the literal labels; `GateHookTests` moved onto the
  new wording.
- **`script`** — parsing removed, `-AcrossPlans` and `-Root` added, the counts line, and
  `SliceManifestTests` updated for the verdicts that change on inputs which pass today.

**Wave 4 — `prose`.** The plan skill's two invocations and its branch when it cannot honour an
approval; the verify skill's `finished:` write on the SHIP branch; the schema's `superseded`
correction, its stale forward references, and **a rewrap so that `did not land` is not split
across a line break** — it is today, and a literal check of the shape every other prose
instrument uses would fail on it; the plugin version bump; the README's third state and its real
instrument. `.sdlc/OPEN.md` and `.sdlc/DIRECTION.md` are claimed here and **no requirement
describes them**: what they get is item 4's half-resolution by `R33` and the direction's first
step reading in the past tense.

**Wave 5 — `instruments`.** The five new test classes, each in its own file with one
`[TestClass]`, and the charter: five register rows each naming its silent-failure class in the
left column; the known-and-unchecked list converted to a bulleted list carrying **ten** entries —
the two that already exist as prose in that paragraph, which must survive the conversion, plus
the eight this change adds; the bump to version 22 with its amendment-log row; and the forward
references corrected, which are at four places in that file rather than one.

## Requirement coverage

Every requirement `R1`–`R62` is claimed. Five are claimed by more than one slice because they
bind files in more than one:

- **`R39`**, **`R58`** — the three labels and the state counts: the hook's header and
  `hooks.json` in `hook`, the README in `prose`.
- **`R42`** — the stale forward references: the schema in `prose`, the charter in `instruments`.
- **`R46`**, **`R48`** — the five classes and what may read the live chain tree: `fixtures` builds
  what they run on, `instruments` writes them.
- **`R55`** — containment under the artifact root: stated in `parser`, and claimed by `hook` and
  `script` because it binds both readers and the spec is emphatic that it is a *different test*
  from the hook's segment-equality skip.

**This chain's own `plan.md` is deliberately in no manifest.** `R49`'s medians, the manifest
output and the mutation-test results are all pasted into it, so it is written repeatedly and by
whoever closes each wave. It is the record of the build rather than a file the build produces,
and the artifact-root carve-out means no instrument would report its absence either way —
which is exactly why it is said here.

## Validation

Pasted into this file as real output, not asserted:

- `dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj` — the whole suite, green, at every wave.
- `Assert-SliceManifest.ps1 -Plan <this file>` — the single-plan mode, which this change leaves
  unchanged.
- `Assert-SliceManifest.ps1 -Plan <this file> -AcrossPlans` — **the mode that is this change's
  subject**, run on this plan once `script` lands. An earlier version of this plan named only the
  single-plan run and called it "the first time this chain's subject applies to itself", which it
  is not.
- **The spec's end-to-end verification, all five steps** — a throwaway repository, two plans
  claiming one code file, `contested file` on standard error, exit 2 under `SDLC_GATE=block`, and
  `finished: true` flipping it silent. The spec calls step 2 the criterion the intent calls the
  guarantee, and an earlier version of this plan omitted it entirely.
- `R49`'s four medians: two taken in wave 1, two after wave 3.

## Risks

**Mutation testing in wave 5 edits files wave 2 and 3 own.** Two of `CrossPlanCollisionTests`'
four faults are edits to `sdlc_gate.ps1` and `SliceManifestParser.ps1`; `ShippedProseListTests`'
fault edits `hooks.json` and the hook's header. This is the one place the plan knowingly breaks
its own discipline. *The procedure:* copy the file aside and copy it back — never `git checkout --`,
which has destroyed uncommitted work in this repository before.

**The `instruments` slice is large, last, and unsplittable.** Five classes, each mutation-tested,
against code four slices wrote. *Early-warning sign:* wave 4 finishes and the fifth class is
still being written. It cannot be split — the register check forbids it.

**`prose` edits seven files for twelve requirements and shares a theme with none of them.**
*Early-warning sign:* a reviewer cannot say in one sentence what that slice did.

**The single-plan mode changes its verdict on inputs that pass today.** Measured: all seven plans
here conform. *Early-warning sign:* a plan elsewhere failing a check that has never failed.

**Between the `hook` commit and wave 5 nothing proves the parser ships.** The existing hook check
validates the command in `hooks.json`, not the dot-source inside the hook, so a wrong relative
path leaves the gate silently dead with a green build — the failure the spec's own table calls
*the gate is silently dead*. *Early-warning sign:* the gate saying nothing where it used to speak.

**Seven full-suite runs at roughly six minutes each**, two of them concurrent in each of waves 2
and 3 against one shared `obj/`. A schedule fact rather than a race to ignore.

## Rollback

`git revert`, with two asymmetries:

**Wave 1 does not revert.** Putting seven plans back to `approved` while their work is in git is
the wrong state, not the previous one. The marking records something already true. Revert
everything *except* wave 1.

**The version bump re-bumps rather than reverts.** `prose` raises the engine plugin's version,
and the charter's own technology table says plugins are cached per version — so restoring the old
number against a warm cache is a silent no-op: the revert reads as applied and the old code does
not come back.

## What this plan got wrong before it got it right

Kept here rather than inside the slices, the way the spec and intent keep theirs.

- **The slice cut put three implementers inside one C# project** with a shared fixture and no
  edge between them, and offered a mitigation — "scope to your own project" — naming an action
  that does not exist, because one project exists. `fixtures` moved to wave 2.
- **`ScaffoldManifestTests.cs` compiled against a fixture no slice claimed**, so repairing it
  would have been an out-of-manifest edit during this change.
- **`R49`'s before-figures were scheduled after the code they measure had been replaced.**
- **`prose` depended on `parser` when it depends on `script`.**
- **`fixtures` carried `R46`, which describes a different slice's files.** No spec requirement
  covers the fixture work at all; the contract is stated in the work order instead, and the gap
  is real rather than papered over.
- **`R55` was claimed by one slice while binding three.**
- **Validation named the single-plan mode and called it this change's subject**, and omitted the
  spec's end-to-end verification entirely.
- **"Eight known-and-unchecked entries" read as eight total** where the charter will hold ten;
  an implementer would have written eight bullets and silently deleted the two that exist.
- **The claim that "everything reads the parser" was padding.** `prose` and `fixtures` have no
  functional dependency on it; `parser`'s own edge to `marking` is organisational too. Which
  edges are real matters under schedule pressure, so they are now what they are.
