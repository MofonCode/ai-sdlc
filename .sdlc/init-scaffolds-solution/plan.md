---
slug: init-scaffolds-solution
status: approved
finished: true
spec: ./spec.md
tier: feature
slices:
  - id: executor
    files: ["plugins/sdlc-core/scripts/Invoke-ScaffoldManifest.ps1", "tests/AiSdlc.Tests/ScaffoldManifestTests.cs", "tests/AiSdlc.Tests/TempRepo.cs"]
    requirements: [R2, R3, R4, R5, R7, R9, R12, R17, R18, R19, R35, R38, R39, R40, R41, R42, R43, R44, R45, R46, R50, R51, R52, R53, R56, R57, R58, R61, R63]
    depends_on: []
  - id: pack
    files: ["plugins/stack-dotnet/skills/stack-dotnet/scaffold.json", "plugins/stack-dotnet/skills/stack-dotnet/SKILL.md", "plugins/stack-dotnet/skills/stack-dotnet/assets/Directory.Packages.props", "plugins/stack-dotnet/skills/stack-dotnet/assets/solution.readme.md", "plugins/stack-dotnet/skills/stack-dotnet/assets/dotnet.gitignore", "plugins/stack-dotnet/skills/stack-dotnet/assets/web.StarterTests.cs", "plugins/stack-dotnet/skills/stack-dotnet/assets/competition.StarterTests.cs", "plugins/stack-dotnet/skills/stack-dotnet/assets/competition.starter.ipynb"]
    requirements: [R1, R21, R22, R23, R24, R25, R26, R27, R28, R29, R30, R47, R49, R62, R66, R67, R68]
    depends_on: []
  - id: init
    files: ["plugins/sdlc-core/skills/init/SKILL.md"]
    requirements: [R13, R15, R16, R17, R18, R20, R48, R59, R60]
    depends_on: []
  - id: structure-checks
    files: ["tests/AiSdlc.Tests/PluginStructureTests.cs"]
    requirements: [R1, R2, R35, R49, R54, R55]
    depends_on: []
  - id: build-tests
    files: ["tests/AiSdlc.Tests/ScaffoldEmitsBuildableSolutionTests.cs"]
    requirements: [R7, R23, R31, R32, R34, R64, R65, R66]
    depends_on: [executor, pack]
  - id: release
    files: ["README.md", ".github/workflows/checks.yml", ".sdlc/CHARTER.md", "tests/AiSdlc.Tests/ScaffoldPrerequisiteTests.cs", "plugins/sdlc-core/.claude-plugin/plugin.json", "plugins/stack-dotnet/.claude-plugin/plugin.json"]
    requirements: [R36, R37, R54, R55]
    depends_on: [pack]
---

# plan — init produces a solution you can build

Six slices, two waves. Four run concurrently; the two that can only be written against
something that already exists wait for it.

The spec froze both persisted schemas (the scaffold manifest and the executor's report) in its
**Data and contracts** section, which is what lets the executor and the pack manifest be built
at the same time by different people. Those two slices must agree on field names with no
conversation between them, so the schema is quoted below rather than referenced — see
**Contracts frozen by this plan**.

---

## Contracts frozen by this plan

Three things every slice codes against. Two come from the spec; the third the spec left
implicit and this plan settles, which is called out in **Decisions this plan makes** below.

### 1. The executor's command line

```
powershell -NoProfile -ExecutionPolicy Bypass -File "<sdlc-core>/scripts/Invoke-ScaffoldManifest.ps1" -Manifest <path to the pack's manifest> -Kind <kind name> -Target <repository directory> [-Solution <Identifier>]
```

- `-Manifest`, `-Kind`, `-Target` are mandatory. `-Solution` is optional; when supplied it wins
  over derivation and over recovery (R51) and is validated by the same rule as a derived one
  (R52).
- The JSON report goes to **stdout** and nothing else does. Human-readable progress and every
  error message go to **stderr**. A caller must be able to pipe stdout into a parser.
- Exit 0 only when `outcome` is `completed`. Every halt, refusal and unknown-version rejection
  exits non-zero and still emits the report (R56 binds "completes **or halts**").

The `init` slice writes this command line into the skill body; the `executor` slice implements
it; the `build-tests` slice drives it. None of the three may change it alone.

### 2. The scaffold manifest schema — spec §Data and contracts 1

```json
{
  "version": 1,
  "solution_token": "{{SOLUTION}}",
  "solution_file_glob": "*.slnx",
  "kinds": {
    "web": {
      "description": "...",
      "profile": "...",
      "preflight": [
        { "command": "...", "args": ["..."],
          "expect_exit": 0, "expect_match": "...", "remediation": "..." }
      ],
      "actions": [
        { "kind": "run",     "command": "...", "args": ["..."], "cwd": "...", "unless_exists": "..." },
        { "kind": "mkdir",   "path": "...", "unless_exists": "..." },
        { "kind": "copy",    "asset": "...", "path": "...", "tokens": ["solution"] },
        { "kind": "append",  "asset": "...", "path": "..." },
        { "kind": "rewrite", "glob": "...", "pattern": "...", "replacement": "...", "assert_after": "..." },
        { "kind": "delete",  "glob": "..." }
      ]
    }
  }
}
```

Six action kinds, closed set. `unless_exists` is valid on every one of them (R43). `preflight`
is a separate array and a preflight declared inside `actions` is a manifest error (R57).

### 3. The report schema — spec §Data and contracts, "The executor's report"

```json
{
  "report_version": 1,
  "artifact_root": "...",
  "target": "...",
  "kind": "...",
  "solution": "...",
  "outcome": "completed | halted",
  "actions": [
    { "kind": "copy",    "outcome": "created", "path": "..." },
    { "kind": "append",  "outcome": "skipped", "path": "...", "reason": "..." },
    { "kind": "rewrite", "outcome": "mutated", "path": "..." },
    { "kind": "delete",  "outcome": "removed", "path": "..." },
    { "kind": "run",     "outcome": "failed",  "command": "...", "reason": "..." }
  ]
}
```

Outcome set is exactly `created | skipped | mutated | removed | failed` (R56). A consumer
rejects a `report_version` it does not know (R61).

---

## Decisions this plan makes

Two things the spec does not settle and the build cannot start without.

**Where the manifest lives, and how `init` finds it.** The spec says the pack ships a manifest
and that `init` passes its path to the executor, but never names the file or the lookup rule.
This plan fixes the convention as **`<plugin>/skills/<pack-name>/scaffold.json`** — beside the
pack's `SKILL.md`, the same way `assets/` already sits there. This is an engine-level naming
convention like `SKILL.md` itself, not a pack-specific filename, so I5 is not engaged: it names
a shape every pack fills in, not a string belonging to `stack-dotnet`. The `structure-checks`
slice turns the convention into the R1 check; the `init` slice states it in prose.

**Asset filenames for the four new pack assets.** `solution.readme.md`, `dotnet.gitignore`,
`web.StarterTests.cs`, `competition.StarterTests.cs`, `competition.starter.ipynb` — following
the existing `dotnet.editorconfig` / `terraform.gitignore` convention of prefixing the asset
with what it is for. Nothing outside the manifest reads these names, so they are cheap to
change; they are pinned here only so the manifest check has exact paths.

---

## Work order

### Wave 1 — four slices, concurrent

**`executor` — the engine, and the fast tests that drive it.**
`plugins/sdlc-core/scripts/Invoke-ScaffoldManifest.ps1` is new shipped engine tooling: Windows
PowerShell 5.1, `Set-StrictMode -Version 2.0`, the `Get-Field` pattern from `sdlc_gate.ps1`
copied rather than inferred, because `ConvertFrom-Json` on an object with no properties throws
under strict mode and every optional manifest field hits that path.

It resolves the artifact root the way `Get-ArtifactRoot` in the gate hook does and reads the
charter's `status:` from the file before anything else (R38–R40) — no charter, or a charter that
is not `approved`, and it writes nothing and exits non-zero. Then: reject an unknown `version`;
reject a `kind` the manifest does not declare, listing the ones it does (R12); reject a
`preflight` entry found inside `actions` (R57); run every preflight (R9, R30); then the actions
in declared order, halting at the first failure (R3).

The containment rules are one function used by every path that touches disk: a resolved path
outside `-Target` is refused, nothing is written, and the path is named (R4, R44). `run` gets a
working directory inside the target and a declared `cwd` outside it is refused (R63).

It keeps a set of every path it created this execution, and `rewrite` and `delete` apply only to
that set (R41, R58). After a rewrite, every created file matching the glob must match
`assert_after` or the run fails naming the file (R42). `copy` never overwrites (R5); `append`
creates the file or adds only the asset lines not already present, and is a no-op with a
`skipped` outcome when they all are (R45, R46).

Identifier handling lives here because it is the testable half: derive from the target
directory name (R17), refuse an underivable or invalid candidate rather than repairing it (R18,
R52), prefer a supplied identifier (R51), and recover from an existing solution file matched by
`solution_file_glob` — exiting non-zero on more than one match, falling back to derivation on
none (R19, R53).

Last, after the actions: sweep **every** file under the target for any declared token and fail
naming file and token, whether or not this run wrote the file (R50). This is the requirement
that catches the `{{SOLUTION}}.Foo` regression on a gap-filling run that wrote nothing.

`tests/AiSdlc.Tests/ScaffoldManifestTests.cs` drives the real script as an external process via
`Repo.PowerShell`, against fixture manifests with stub commands — no SDK needed, fast. It owns
one row per executor requirement, listed under **Tests**.

`tests/AiSdlc.Tests/TempRepo.cs` gains a charter-status parameter: today it hardcodes
`status: approved`, and the R38 test needs a draft one. Additive, so `GateHookTests` is
untouched.

**`pack` — the manifest, the new assets, and the skill that cites them.**
`scaffold.json` declares both kinds end to end, including the seven configuration files the
skill's table lists today — `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`,
`global.json`, `coverage.runsettings`, `terraform/versions.tf` and the appended Terraform
ignore entries. R47 is the point: after this slice no file the pack names is written by anything
but the executor, which is the whole basis of the gate-hook argument in the spec.

The web kind carries a preflight per Aspire template and never installs one (R30). The
competition kind carries none, because `sln`, `classlib`, `console`, `webapi`, `blazor`,
`mstest` and `gitignore` are in the SDK box.

Two rewrites, both pack-supplied regexes: strip every `Version` attribute from every
`PackageReference` (R27), and rewrite the AppHost's inline `Aspire.AppHost.Sdk/<version>` to the
version `global.json` pins (R29). Each carries `assert_after` so a template that changes shape
fails loudly instead of silently unpinning.

`Directory.Packages.props` gains a `PackageVersion` for every package any invoked generator
emits — the seven ServiceDefaults packages the spec verified plus whatever restore turns up —
and must restore clean with `CentralPackageTransitivePinningEnabled` on (R28, R66). The
competition kind selects the ML profile, so that profile's pins are part of the same surface
(R67).

`delete` actions remove sample pages, components, endpoints and data types (R26), and the two
starter test assets replace what they take away so the suite is never empty (R62). The notebook
ships as headings and a reference to the emitted solution, with no executable logic (R68).

`SKILL.md`'s **Files written** table is replaced by a pointer to `scaffold.json` (R49). Two
lists that nothing checks against each other is the invariant inverted; `structure-checks` holds
the replacement.

**`init` — the flow, in prose.**
Stop before any scaffold action while the charter is draft (R13). Stop, naming the missing
answer, when the interview has not settled the kind and the module boundaries (R15). Present the
derived solution identifier for confirmation, and require an explicit answer when it is
underivable rather than offering a repaired guess (R17, R18). Invoke the executor with all four
arguments — manifest, kind, target, confirmed identifier (R60). Relay the report with every
`failed` entry and its reason and every `mutated` and `removed` path, never a count, and never
claim the repository is scaffolded after a non-zero exit (R20, R59).

`init` writes the charter and nothing else (R48): the scaffold-file paragraph in §3 goes, and
the closing line that says `init` is never run again is replaced by the two things a re-run
offers — amend the charter, or fill scaffold gaps (R16).

**`structure-checks` — the instruments.**
`PluginStructureTests.cs` gains three checks and one refactor. The stack-term list moves out of
`CharterInvariantI1_EngineImposesNoStack` into a shared member so `EngineExecutablesImposeNoStack`
uses the same list rather than a copy that drifts (R54) — a copy is how an instrument quietly
stops covering what it claims to. The new check scans every shipped engine executable under
`plugins/sdlc-core/`, reusing the file set `EveryShippedPowerShellScript_Parses` already
enumerates, and fails on a stack term (R55). `EveryPack_ShipsAScaffoldManifest` asserts every
non-engine plugin ships a `scaffold.json` that parses and declares at least one kind (R1).
`PackSkillCitesItsManifest` asserts the pack's skill body references its manifest and carries no
competing file table (R49).

### Wave 2 — two slices, after wave 1

**`build-tests` — the check the whole feature exists to make possible.**
`ScaffoldEmitsBuildableSolutionTests.cs` executes the **real** pack manifest into an empty temp
directory, per kind, and asserts: the solution builds with exit 0 (R31); the unit suite reports
a test count greater than zero and zero failures, and so does the integration suite (R32, R64);
no `{{`-delimited token survives anywhere under the target (S3/R50 from the outside); restore is
clean with transitive pinning on and no reference carries a version attribute (R27, R28, R66);
every emitted test and host assembly name matches an exclusion pattern parsed out of
`coverage.runsettings` itself rather than a copy in the test (R23); a second execution changes
no file and exits 0 (R7); and deleting `tests/` and re-executing creates only `tests/` and leaves
every other file byte-identical (R34).

Prerequisites fail the run rather than skipping it — a skipped MSTest leaves `dotnet test` at
exit 0 showing nothing, which is the failure this suite exists to catch. The single exception is
the container runtime: absent, the integration tests compile, are not run, and the output says
so (R64, R65).

It waits on both wave-1 code slices because it drives them as real processes; it cannot be
iterated against a contract.

**`release` — the paperwork, and the amendment.**
`README.md` describes what `init` produces, lists the pack's emitted layout rather than only its
config files, and drops `/init-competition` from **Not yet built** (R36). `checks.yml` gains an
explicit step installing the Aspire template pack before the suite runs (R37) — a hosted runner
is ephemeral and declared in a reviewed file, which is the distinction the spec draws between a
runner and a person's workstation. `ScaffoldPrerequisiteTests.cs` parses both `checks.yml` and
the pack manifest and fails when the two disagree, so a manifest that adds a prerequisite CI
does not install fails here rather than as a confusing preflight failure on the runner.

`.sdlc/CHARTER.md` takes the amendment the spec defers to ship time: I1's checkable clause
widens from `skills/**` and `agents/**` to shipped engine executables, and I9's table gains the
nine rows the spec lists. It waits on `pack` only because the CI step must name the prerequisite
the manifest declares.

Both plugin versions are bumped. **This is the one thing in this plan no requirement asks
for** — the charter records an unbumped version as a known-and-unchecked gap, and both plugins
change materially here. Cut it if you would rather keep the plan strictly to the spec.

---

## Files

Every file, one slice each. This is the frontmatter manifest restated; the proof that it is
disjoint is pasted below.

| Slice | File | New / modified |
|---|---|---|
| `executor` | `plugins/sdlc-core/scripts/Invoke-ScaffoldManifest.ps1` | new |
| `executor` | `tests/AiSdlc.Tests/ScaffoldManifestTests.cs` | new |
| `executor` | `tests/AiSdlc.Tests/TempRepo.cs` | modified |
| `pack` | `plugins/stack-dotnet/skills/stack-dotnet/scaffold.json` | new |
| `pack` | `plugins/stack-dotnet/skills/stack-dotnet/SKILL.md` | modified |
| `pack` | `plugins/stack-dotnet/skills/stack-dotnet/assets/Directory.Packages.props` | modified |
| `pack` | `plugins/stack-dotnet/skills/stack-dotnet/assets/solution.readme.md` | new |
| `pack` | `plugins/stack-dotnet/skills/stack-dotnet/assets/dotnet.gitignore` | new |
| `pack` | `plugins/stack-dotnet/skills/stack-dotnet/assets/web.StarterTests.cs` | new |
| `pack` | `plugins/stack-dotnet/skills/stack-dotnet/assets/competition.StarterTests.cs` | new |
| `pack` | `plugins/stack-dotnet/skills/stack-dotnet/assets/competition.starter.ipynb` | new |
| `init` | `plugins/sdlc-core/skills/init/SKILL.md` | modified |
| `structure-checks` | `tests/AiSdlc.Tests/PluginStructureTests.cs` | modified |
| `build-tests` | `tests/AiSdlc.Tests/ScaffoldEmitsBuildableSolutionTests.cs` | new |
| `release` | `README.md` | modified |
| `release` | `.github/workflows/checks.yml` | modified |
| `release` | `.sdlc/CHARTER.md` | modified |
| `release` | `tests/AiSdlc.Tests/ScaffoldPrerequisiteTests.cs` | new |
| `release` | `plugins/sdlc-core/.claude-plugin/plugin.json` | modified |
| `release` | `plugins/stack-dotnet/.claude-plugin/plugin.json` | modified |

`plugins/stack-dotnet/` is touched by no slice but `pack`, so any further asset that slice turns
out to need is conflict-free by construction. Every other directory is named file by file.

---

## Tests

What proves each requirement, and which file it lives in.

**`tests/AiSdlc.Tests/ScaffoldManifestTests.cs`** — the executor, driven as an external process
against fixture manifests with stub commands. No SDK.

| Proves | Assertion |
|---|---|
| R38, R39, R40 | Draft charter → non-zero, empty tree. No charter → non-zero. The resolved root appears in the report. |
| R3 | An action that fails halts the run; later actions did not happen. |
| R4, R44 | A `copy`, `append`, `rewrite` or `delete` path resolving outside the target is refused, nothing written, path named. |
| R63 | A `run` whose `cwd` resolves outside the target is refused. |
| R5 | An existing destination is skipped and left byte-identical. |
| R7, R43 | A second execution over the same tree changes no file and exits 0. |
| R9, R30 | A failing preflight writes nothing and relays the manifest's `remediation` verbatim. |
| R57 | A preflight declared inside `actions` is rejected. |
| R12 | An unknown `kind` lists the declared kinds; an unknown `version` is refused before any action. |
| R41, R58 | `rewrite` and `delete` touch only paths this run created — proven with a pre-existing file matching the glob. |
| R42 | An `assert_after` that does not hold fails the run naming the file. |
| R45, R46 | `append` creates when absent, adds only missing lines, and is a `skipped` no-op when all are present. |
| R50 | A token left by a previous run, in a file this run did not write, fails the run naming file and token. |
| R17, R18, R19, R51, R52, R53 | Derivation; refusal of empty / leading-digit; recovery from an existing solution file; supplied wins; invalid supplied refused; multiple glob matches refused; no match falls back. |
| R56, R61 | Report carries every outcome in the set with its path or command, plus root and target; an unknown `report_version` is rejected by the consumer. |

**`tests/AiSdlc.Tests/ScaffoldEmitsBuildableSolutionTests.cs`** — the real manifest, the real
SDK, parameterised per kind (S8).

| Proves | Assertion |
|---|---|
| R31 | Execute into temp, build, exit 0. |
| R32, R64 | Unit suite: count > 0, failures 0. Integration suite: same where a container runtime exists, compile-only and said so where it does not. |
| R27, R28, R66 | No version attribute on any reference; restore clean with transitive pinning on. |
| R23 | Each emitted test/host assembly name matches an exclusion parsed from `coverage.runsettings`. |
| R24, R25 | `README.md` carries the build, test and local-run commands and the chain's location; a base ignore file exists alongside the Terraform entries. |
| R7 | Hash the tree, re-execute, compare. |
| R34 | Delete `tests/`, re-execute, only `tests/` appears, everything else byte-identical. |
| R65 | An absent prerequisite fails the run; only the container runtime downgrades, and names itself. |

**`tests/AiSdlc.Tests/PluginStructureTests.cs`** — R1, R49, R54, R55 as described in the work
order.

**`tests/AiSdlc.Tests/ScaffoldPrerequisiteTests.cs`** — R37: parse `checks.yml` and the pack
manifest, fail when the workflow does not install a prerequisite the manifest declares.

**Human-verified, and recorded as such** — R14 is superseded; R15, R16 and R20 are `init`'s own
prose, and so is the confirmation half of R17. The end-to-end hand check is in the spec:
`/sdlc-core:init` in an empty directory, confirming that declining the module-boundary question
stops the command, and that `init` invokes the executor after approval rather than before.

---

## Risks

**The executor and the pack manifest are written concurrently against a schema neither can see
the other implement.** A field-name disagreement is invisible until `build-tests` runs in
wave 2. *Early-warning sign:* the first `ScaffoldEmitsBuildableSolution_*` run fails on a
missing-property or parse error rather than on a compile error. The schema is quoted verbatim in
**Contracts frozen by this plan** precisely so both implementers copy rather than paraphrase; if
either finds it needs a field the spec does not declare, that is a spec revision, not a local fix.

**R28's package surface cannot be enumerated by reading project files.** Transitive pinning means
restore can demand a `PackageVersion` for a package no generator names. *Early-warning sign:*
NU1008 or NU1010 in the `build-tests` restore, and it will take more than one iteration to
settle. Nothing makes this self-enforcing — the restore is its only instrument.

**The web kind cannot be built on a machine without the Aspire template pack.** By design: the
preflight halts with the pack's own install instruction. *Early-warning sign:*
`ScaffoldEmitsBuildableSolution_Web` failing with a remediation message rather than a compiler
error. That is the requirement working, not the test being broken — install the templates, or
run the competition kind only, and say which happened.

**Strict-mode `ConvertFrom-Json` traps.** Every optional manifest field reaches the code path the
gate hook's `Get-Field` comment documents: strict mode throws on a property test against an
object with no properties. *Early-warning sign:* "property cannot be found on this object" from
a manifest that omits an optional field. Copy `Get-Field`; do not re-derive it.

**Wave 2 carries nearly all the real risk and none of the parallelism.** `build-tests` is the
slice that actually proves the feature, it is the slowest, and it is last. Nothing before it has
been shown to work end to end. Plan for wave 2 to take longer than wave 1.

**The executor slice is several times the size of any other.** Wall-clock saving from fanning out
wave 1 is bounded by it, which is the honest case against fanning out at all — see the note under
**Parallelism**.

---

## Rollback

Nothing here migrates data, persists state, or touches anything outside this repository. Every
slice is additive or a prose edit, so `git revert` of the slice commits restores the current
behaviour exactly: `init` writes the pack's files itself from prose, and the pack's skill carries
its own file table again.

Two things to revert together or not at all: the `.sdlc/CHARTER.md` amendment and the
`structure-checks` slice. The charter's widened I1 clause describes a check that only exists in
that slice; reverting one alone leaves the charter claiming an instrument that is not there, or
an instrument no invariant asks for.

The new pack assets and `scaffold.json` are inert once `init`'s prose no longer points at them,
so a partial revert degrades to "unused files" rather than to a broken command.

---

## Requirement coverage

Every live requirement in the spec is claimed by at least one slice. Four are struck through in
the spec and superseded, and are claimed by nobody on purpose:

| Superseded | By | Claimed via |
|---|---|---|
| R6 | R56 | `executor` |
| R8 | R50 | `executor` |
| R11 | R42, R58 | `executor` |
| R14 | R60 | `init` |
| R33 | R64 | `build-tests` |
| R10 | restated as R43 | `executor` |

**No live requirement is unclaimed.** R2 and R35 are claimed twice — by `executor`, which must
comply, and by `structure-checks`, which holds the instrument. R17, R18 and R49 are claimed
twice for the same reason: the testable half and the prose half live in different slices, which
is exactly what the spec's test strategy says about them.

---

## Parallelism

```
wave 1:  executor, pack, init, structure-checks
wave 2:  build-tests, release
```

**What is genuinely concurrent:** the four wave-1 slices touch four disjoint areas — shipped
PowerShell, the pack's own subtree, one engine skill body, one test file — and need no
conversation with each other beyond the frozen contracts above.

**What is genuinely serial:** `build-tests` drives the real executor against the real manifest,
so it cannot be written against a contract; it needs both to exist. `release` needs the
manifest's declared prerequisite before CI can install it.

**The honest caveat.** The `executor` slice is several times the size of the other three
combined, so fanning out wave 1 saves less wall-clock than four slices suggests, and it buys that
saving at the cost of the schema-agreement risk named above — which a single writer would have
caught by running the thing. A three-way fan-out of `executor` / `pack` / `init`+`structure-checks`
is reasonable; a single writer in one worktree is also reasonable and is the safer choice if the
schema risk reads as larger than the time saving. **Wave 2 should not be fanned out** — two
slices, one of which is where every failure will surface.

---

## Accepted drift — recorded 2026-09-18, after approval

**The frontmatter manifest above is deliberately NOT updated.** It is the record of what was
approved on 2026-09-18 and editing it afterwards to match what happened would destroy the only
thing it is for. What follows is the post-approval decision, dated separately so the two are
never confused.

The `pack` slice wrote three files beyond the eight its manifest lists. All three sit inside
`plugins/stack-dotnet/`, which no other slice touches, so disjointness is unaffected and no
implementer was at risk of a collision. The owner accepted the drift rather than amending and
re-approving the plan.

| File | Why it was not foreseen |
|---|---|
| `assets/competition.readme.md` | R24 mandates that the emitted README carry the orchestration-versus-infrastructure invariant. That invariant is meaningless in a competition repo, so one shared README could not honestly serve both kinds. The plan assumed one README asset; two are needed. |
| `assets/web.IntegrationStarterTests.cs` | The generated sample integration test contains zero live tests *and* a property that fails compilation under warnings-as-errors. R26 deletes sample content and R64 requires the integration suite to report a test count above zero; with nothing put in its place those two requirements contradict each other. The plan provided a starter for the unit suite only. |
| `assets/dotnet.gitignore` | Listed in the plan; recorded here only because the pack's report grouped it with the other two. Not drift. |

Two of the three are the same class of miss: **the plan counted one asset where the spec's own
requirements need two.** Neither is scope creep, and both were flagged by the implementer
rather than found later.

Also accepted, and not a file change: the competition kind declares one preflight check where
the plan's prose said it "carries none". The plan's sentence was about the project-template
prerequisites specifically; the spec's failure-modes table is explicit that an absent SDK is a
pack-declared preflight for every kind. The implementer's reading was correct and the plan's
prose was loose.

**For verification:** these files are expected. Report them as drift — that is the check
working — and record that the owner accepted them here rather than treating them as a blocking
finding.
