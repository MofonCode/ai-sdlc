---
slug: no-two-plans-claim-one-file
status: draft
spec: ./spec.md
tier: feature
slices:
  - id: marking
    files: [".sdlc/init-scaffolds-solution/plan.md", ".sdlc/pack-promises-what-it-scaffolds/plan.md", ".sdlc/register-names-every-check/plan.md", ".sdlc/stack-dotnet-house-stack/plan.md", ".sdlc/verification-reports-options/plan.md", ".sdlc/every-workflow-body-runs/plan.md", ".sdlc/engine-substitutes-declared-tokens/plan.md"]
    requirements: [R31, R32, R35]
    depends_on: []
  - id: parser
    files: ["plugins/sdlc-core/scripts/SliceManifestParser.ps1"]
    requirements: [R1, R2, R3, R4, R5, R6, R7, R8, R9, R10, R11, R12, R13, R55, R56, R57, R60]
    depends_on: [marking]
  - id: hook
    files: ["plugins/sdlc-core/hooks/sdlc_gate.ps1", "plugins/sdlc-core/hooks/hooks.json", "tests/AiSdlc.Tests/GateHookTests.cs"]
    requirements: [R14, R15, R16, R17, R18, R19, R20, R21, R22, R23, R39, R43, R58, R62]
    depends_on: [parser]
  - id: script
    files: ["plugins/sdlc-core/scripts/Assert-SliceManifest.ps1", "tests/AiSdlc.Tests/SliceManifestTests.cs"]
    requirements: [R24, R25, R26, R27, R28, R29, R30, R53, R59, R61]
    depends_on: [parser]
  - id: prose
    files: ["plugins/sdlc-core/skills/plan/SKILL.md", "plugins/sdlc-core/skills/verify/SKILL.md", "plugins/sdlc-core/skills/sdlc-router/references/artifact-schemas.md", "plugins/sdlc-core/.claude-plugin/plugin.json", "README.md", ".sdlc/OPEN.md", ".sdlc/DIRECTION.md"]
    requirements: [R33, R34, R36, R37, R38, R40, R41, R42, R44, R45, R54]
    depends_on: [parser]
  - id: fixtures
    files: ["tests/AiSdlc.Tests/Repo.cs", "tests/AiSdlc.Tests/TempRepo.cs"]
    requirements: [R46]
    depends_on: [parser]
  - id: instruments
    files: ["tests/AiSdlc.Tests/CrossPlanCollisionTests.cs", "tests/AiSdlc.Tests/GateHookPackagingTests.cs", "tests/AiSdlc.Tests/FrontmatterGrammarTests.cs", "tests/AiSdlc.Tests/VerifyShipBranchTests.cs", "tests/AiSdlc.Tests/ShippedProseListTests.cs", ".sdlc/CHARTER.md"]
    requirements: [R46, R47, R48, R49, R50, R51, R52]
    depends_on: [hook, script, prose, fixtures]
---

# plan — no two live plans claim one file

**Tier: feature.** Seven slices from an approved `spec.md`, requirements `R1`–`R62`.

## Why the slices fall where they do

Three constraints decide almost the whole shape, and two of them come from the spec rather than
from preference.

**The marking is first and everything depends on it.** Seven plans read `approved` while their
work is in git; until they say so, five of the files this change edits are claimed by three
other plans, and the first real run of the mechanism being built would report collisions that
are all false. It is one slice because it is one reviewable act, and it depends on nothing.

**The charter's register rows and the five new test classes cannot be separated.** The register
check reads `.sdlc/CHARTER.md` and the test directory in both directions: a row naming a class
that does not exist is red, and a class with no row is red. Nothing is committed on a red build,
so both halves land in one slice — and since only one slice may claim the charter, that is the
same slice. It is the largest one and it is last.

**Each shipped file lands with the test that pins it.** `GateHookTests` sits in the hook slice
and `SliceManifestTests` in the script slice, not with the new classes, because both slices
change behaviour those suites already assert. Split the other way and the tree is red between
two waves.

## Work order

**Wave 1 — `marking`.** Six plans keep `status: approved` and gain `finished: true`;
`engine-substitutes-declared-tokens` takes `status: superseded` and no `finished:` line at all,
because its work was never built and `finished: true` beside `superseded` is refused by the
grammar this change ships. Slice manifests are left exactly as they are. The commit message
names, for each plan, the commit that landed its files — that is the evidence a reviewer follows
rather than an assertion they accept, and nothing checks it.

**Wave 2 — `parser`.** `SliceManifestParser.ps1`: frontmatter reading, path normalisation,
liveness, root resolution, and refusals returned as data. The whole of `R1`–`R13` plus the four
rules that were appended later — that claims and edits are judged under the artifact root by
containment (`R55`), that a plan-level refusal makes a plan not live (`R56`), that a `files:`
line before the first slice is dropped rather than refused (`R57`), and that a `finished:` line
nested in the slices block *is* refused (`R60`).

**Wave 3 — four slices in parallel**, which is the only real fan-out in this plan:

- **`hook`** — collect every claimant instead of returning at the first; the third warning state
  `contested file` with an action naming the other chain; the header rewritten to three labels
  with the false timing claim removed; `hooks.json`'s description carrying the literal labels;
  and `GateHookTests` moved onto the new wording.
- **`script`** — parsing removed, `-AcrossPlans` and `-Root` added, the counts line, and
  `SliceManifestTests` updated for the verdicts that change on inputs which pass today.
- **`prose`** — the plan skill's two invocations and its branch when it cannot honour an
  approval; the verify skill's `finished:` write on the SHIP branch; the schema's `superseded`
  correction and stale forward references; the plugin version bump; the README's third state and
  its real instrument; and the two chain documents this change makes stale.
- **`fixtures`** — `Repo.cs` and `TempRepo.cs`: two colliding plans in one fixture, and a copy of
  the plugin tree into a temp repository so the packaging check injects into a fixture rather
  than into the live plugin directory.

**Wave 4 — `instruments`.** The five new test classes, each in its own file with one
`[TestClass]`, and the charter: five register rows each naming its silent-failure class in the
left column, eight known-and-unchecked entries as a bulleted list rather than a table, the
version bump to 22 with its amendment-log row, and the third forward reference corrected.

## What runs in parallel and what does not

```
wave 1: marking
wave 2: parser
wave 3: hook, script, prose, fixtures
wave 4: instruments
```

Four slices at once in wave 3, and that is the whole of the concurrency. The graph is four deep
rather than shallow, which is worth saying plainly: this is closer to a sequential build than a
parallel one, and the reason is real rather than a planning failure. Everything reads the parser,
and the instruments read everything.

**One consequence for whoever runs it.** Wave 3's four implementers share build output even
though they share no source file, so each scopes its build and test commands to its own project.
A file lock is that race and not a code error.

## Requirement coverage

Every requirement `R1`–`R62` is claimed. Three span slices because they bind more than one file,
and each is listed by both:

- **`R39`** — the three warning-state labels: the hook's header and `hooks.json` in `hook`, the
  README in `prose`.
- **`R42`** — the stale forward references: the schema in `prose`, the charter in `instruments`.
- **`R58`** — the state counts beside the lists: the hook's header in `hook`, the README in
  `prose`.

**`R46` is claimed by two slices on purpose.** `fixtures` must give the five classes something to
run against; `instruments` writes them and their rows. Neither half satisfies it alone.

**This chain's own `intent.md` and `spec.md` are not in any manifest.** They are the record of the
change rather than files it edits, and an approved artifact should not move during its own build.

## Tests

| Proves | Instrument |
|---|---|
| The comparison, in both readers | `CrossPlanCollisionTests` — four injected faults, one per silent-failure class it carries |
| The shipped hook still speaks when its parser is present and stops when it is removed | `GateHookPackagingTests` — both callers, in a fixture copy of the plugin tree |
| All five grammar rules, and the charter against the schema | `FrontmatterGrammarTests` — two classes, two faults |
| The `finished:` write is on the SHIP branch and nowhere in the DO NOT SHIP block | `VerifyShipBranchTests` — bounded marker to marker |
| The three warning-state copies and the three manifest-drift copies agree | `ShippedProseListTests` — both directions, both lists |
| Everything the two suites already pin | `GateHookTests`, `SliceManifestTests` — moved onto the new behaviour in the slices that change it |

**Validation before the plan is called done**, with real output pasted into this file:

- `dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj` — the whole suite, green.
- `Assert-SliceManifest.ps1 -Plan .sdlc/no-two-plans-claim-one-file/plan.md` — this plan's own
  manifest, which is the first time this chain's subject applies to itself.
- The performance medians: four figures, before and after on each of the two fixtures `R49`
  prescribes.

## Risks

**The instruments slice is large and last, and it is where an underestimate lands.** Five new
test classes, each mutation-tested, against code four other slices wrote. *Early-warning sign:*
wave 3 finishes and the fifth class is still being written when the first four are green. The
mitigation is not to split it — the register check forbids that — but to treat wave 3 as
finished only when someone has read the code the instruments will have to drive.

**The `prose` slice edits seven files for eleven requirements and shares a theme with none of
them.** It is a bin for everything that is text rather than behaviour. *Early-warning sign:* a
reviewer cannot say in one sentence what that slice did. Accepted because splitting it would
claim the same files from two slices, which is the thing this change exists to prevent.

**The single-plan mode changes its verdict on inputs that pass today.** Plans that are malformed
under the five grammar rules begin failing at plan approval where they used to pass silently.
Measured: all seven plans here conform, so nothing in this repository breaks. *Early-warning
sign:* a plan in another repository failing a check that has never failed before, which reads as
a regression and is the rule arriving.

**Root resolution moves into shared code and the hook runs on every keystroke.** *Early-warning
sign:* the performance medians on fixture (b) — forty plans in a shallow tree — moving more than
the budget allows. That fixture exists because fixture (a) cannot register this regression.

## Rollback

`git revert` of the whole change, with one asymmetry worth naming: reverting the marking slice
puts seven plans back to reading `approved` when their work is in git, which is the wrong state
rather than the previous one. The marking records something already true, so the rollback to
prefer is reverting everything *except* wave 1.
