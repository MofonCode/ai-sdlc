---
slug: init-scaffolds-solution
status: draft
spec: ./spec.md
plan: ./plan.md
verdict: ship
verified: 2026-09-19
requirements_total: 63
requirements_tested: 52
verification_rounds: 4
---

*Frontmatter describes the fourth and current verification, at the foot of this file. The three
sections above it are earlier verifications of earlier states and stand as written.*

# verification — init produces a solution you can build

## Verdict

**DO NOT SHIP.** The build is clean and every one of the 115 tests passes with none skipped,
but three things the spec asks for are not actually held: the executor's report silently omits
any `rewrite` or `delete` that matched nothing (which is R56 — the report records every action
with an outcome — not being met, and it lets R26's removal of generator sample content go stale
without a word), and the two requirements that define what the scaffold emits (R21, the web
solution's projects, and R22, the competition solution's projects) have no check at all, so a
manifest that stopped emitting a project would still build and still pass every row.

The rest of the change is in good shape. The containment and refusal paths in the executor are
real, and I read each of their tests closely enough to be confident they can fail. The findings
below are narrow and all of them are fixable without redesigning anything.

---

## Blocking findings

### B1 — The report omits any `rewrite` or `delete` that matched nothing, so R26 can go stale silently

**What R56 asks for:** when the executor finishes or halts, it emits a report recording *every*
action with one of `created`, `skipped`, `mutated`, `removed` or `failed`, plus the path or
command it applied to.

**What happens instead:** in `plugins/sdlc-core/scripts/Invoke-ScaffoldManifest.ps1`, the
`rewrite` branch (lines 941–980) and the `delete` branch (lines 982–997) add a record only
*inside* the loop over matched files. When the glob matches nothing, the loop body never runs
and the action leaves no trace in the report — not `skipped`, not anything.

I ran the shipped executor against a three-action manifest (one `mkdir`, one `rewrite` over a
glob matching nothing, one `delete` over the same glob). It exited 0 and emitted a report with
**one** action in it:

```json
{
  "report_version": 1,
  "kind": "demo",
  "outcome": "completed",
  "actions": [ { "kind": "mkdir", "outcome": "created", "path": "src" } ]
}
```

**The concrete failure scenario.** The pack's web kind declares five `delete` actions to strip
generator sample content, one of them
`src/{{SOLUTION}}.Api/Controllers/WeatherForecastController.cs`. The day a .NET SDK update
renames or moves that file, the glob matches nothing. The executor exits 0. The report shows no
`delete` entry, so `init` — which per R59 relays every `failed`, `mutated` and `removed` entry —
has nothing to relay and tells the owner the repository is scaffolded. The sample controller
ships in the emitted solution. Nothing in the suite fails: the solution still builds
(`ScaffoldEmitsBuildableSolution_Web`), both suites still report a test count above zero
(`EmittedSuitesReportATestCount_Web`), and the names still match the coverage exclusions. R26 —
every sample page, component, endpoint and data type the generators produce is removed — is then
satisfied on paper and broken on disk, which is the exact shape this whole change exists to
eliminate.

**Why no test caught it.** `Report_CarriesEveryOutcomeWithItsPathOrCommand` asserts that each of
`created`, `skipped`, `mutated` and `removed` *appears somewhere* in the report. It never asserts
that the number of recorded actions equals the number of declared actions.
`SecondExecution_ChangesNoFileAndExitsZero` asserts every recorded action is `skipped` — and
passes precisely because the `rewrite` and `delete` rows are not there to contradict it.

**The fix shape:** record a `skipped` entry with the glob and a reason when a `rewrite` or
`delete` matches nothing, and add a test that counts declared actions against recorded ones.

### B2 — What the scaffold emits (R21, R22) and what it emits into the README and ignore file (R24, R25) have no instrument

**R21** says the web kind emits a solution file, an Aspire AppHost, service defaults, a Blazor
front end, an ASP.NET Core API, a unit test project and an integration test project under the
frozen layout. **R22** says the competition kind emits a solution file, a feature-and-scoring
library, a training runner, a unit test project and a starter notebook. Both are implemented
correctly in `plugins/stack-dotnet/skills/stack-dotnet/scaffold.json`. Neither is asserted
anywhere.

**The concrete failure scenario.** Delete the four lines in the web kind that generate and
register `src/{{SOLUTION}}.Web`. The emitted solution has no front end. `ScaffoldEmitsBuildableSolution_Web`
builds whatever is there and passes. `EmittedSuitesReportATestCount_Web` finds both test
projects and passes. `EmittedNamesMatchACoverageExclusion_Web` checks the assemblies that exist
and passes. `RestoreSucceedsUnderCentralPackageManagement_Web` passes. The whole suite is green
on a web solution with no web in it. The emitted layout is what charter v3 **froze** — changing
it is described as a migration across every downstream repository — and it is the one thing
nothing measures.

**R24** (the emitted README carries the build command, the test command, the local-run command,
where the artifact chain lives, and the AppHost-is-not-production invariant) and **R25** (a base
ignore file for the stack alongside the Terraform entries) are in the same position. Both are
implemented — `assets/solution.readme.md` and `assets/competition.readme.md` carry all six
sections, and `assets/dotnet.gitignore` is 49 lines appended alongside `assets/terraform.gitignore`.
Neither has a test. I grepped `ScaffoldEmitsBuildableSolutionTests.cs` for `README` and
`gitignore` and found nothing.

**This is a gap against the plan, not just against my judgement.** The plan's own Tests table
lists the row *"R24, R25 | `README.md` carries the build, test and local-run commands and the
chain's location; a base ignore file exists alongside the Terraform entries"* under
`ScaffoldEmitsBuildableSolutionTests.cs`. The spec's Test strategy says the same, under
**Emitted files present**, "asserted per kind in the same fixture as S1". The fixture exists;
the assertions do not.

**R36** (the repository's own `README.md` describes what `init` produces, lists the emitted
layout, and drops `/init-competition` from the not-yet-built list) is implemented and also has no
test, though the spec's Test strategy promised one under **Documentation**.

---

## Should fix

### S1 — An I/O failure inside an action produces no report at all

The script sets `$ErrorActionPreference = 'Stop'` with no `try`/`catch` around the action loop,
so a terminating error from `Copy-Item`, `New-Item`, `Remove-Item` or `File.WriteAllText` escapes
as an unhandled exception. I reproduced it by pointing a `copy` at `blocker/inside.txt` where
`blocker` is an existing file:

```
EXIT=1
--- STDOUT ---
(empty)
--- STDERR ---
Copy-Item : Could not find a part of the path '...\repo\blocker\inside.txt'.
At C:\Repos\ai-sdlc\plugins\sdlc-core\scripts\Invoke-ScaffoldManifest.ps1:869 char:17
```

It exits non-zero, so nothing reads as falsely green — but R56 binds *"completes **or halts**"*,
and the plan's frozen command-line contract says *"Every halt, refusal and unknown-version
rejection exits non-zero and still emits the report."* R20 tells `init` to report the executor's
report verbatim; here there is none, and the owner gets a raw PowerShell stack trace instead. A
`trap` or a `try`/`finally` around the action loop that routes into `Stop-Run` closes it.

### S2 — The new I1 instrument cannot catch two of the three examples the spec used to justify it

`EngineExecutablesImposeNoStack` in `tests/AiSdlc.Tests/PluginStructureTests.cs` is real, scans
all four shipped engine executables, and guards against a vacuous pass with
`Assert.IsTrue(executables.Count > 0, ...)`. It shares one term list with the instruction-file
check, exactly as R54 asks.

But that shared list is `dotnet`, `.net`, `c#`, `csharp`, `blazor`, `aspire`, `asp.net`,
`ef core`, `entity framework`, `azure`, `aws`, `terraform`, `msbuild`, `nuget`, `sql server`,
`java`, `typescript`, `pyyaml`, `redis`. The spec's own argument for adding the check was that
the executor *"could hardcode `dotnet`, `*.slnx` or `{{SOLUTION}}` and the suite would stay
green"*. Two of those three — the solution-file extension and the token spelling — are still not
in the list, so the new instrument closes one third of the hole its own justification names. The
implementation follows R54 to the letter; it is the letter that is narrow.

(I confirmed by hand that the executor is in fact clean of every term, and of `slnx`, `csproj`
and `{{SOLUTION}}` — the file genuinely imposes no stack. This is about the instrument, not the
current state.)

### S3 — R37's check is satisfied by a comment, not by the step

`EveryDeclaredPrerequisite_IsNamedByCIBeforeTheSuiteRuns` requires the workflow to *name* each
preflight subject somewhere before the line that runs the suite. In `.github/workflows/checks.yml`
the three template subjects — `aspire-apphost`, `aspire-servicedefaults`, `aspire-mstest` — appear
only on line 44, which is the comment `# Provides: aspire-apphost, aspire-servicedefaults,
aspire-mstest`. The `run:` line on line 46 says `dotnet new install Aspire.ProjectTemplates::13.5.3`
and names none of them. Delete the install step but leave the comment and the check still passes.
The test's own documentation says the rule is deliberate — it never asks *how* CI provides
something — so this is a known limit rather than a bug, but it is worth writing down that the
check cannot tell a provisioning step from a sentence about one.

### S4 — R43's "every action kind" claim is untested

R43 says `unless_exists` makes the executor skip an action *for every action kind and not only
for `run`*. The implementation is generic and correct — it sits at the top of the action loop
before the `switch`. But every test that exercises `unless_exists` declares it on a `run` action
(`SecondExecution_ChangesNoFileAndExitsZero`, `TokenInAManifestPathOrArgument_IsSubstitutedBeforeTheActionRuns`).
The `mkdir`, `copy` and `append` actions in those fixtures skip through their own
already-exists logic, not through `unless_exists`. The one sentence R43 adds over R10 is the one
sentence nothing holds.

### S5 — A refused run still leaves the target directory behind

R39 says that with no charter the executor writes nothing and exits non-zero. It creates the
target directory first (lines 550–558, before the approval gate) and then refuses. Pointed at a
path that does not exist yet, it exits 1 having created an empty directory — I confirmed this.
The code comments acknowledge the ordering and the directory is empty, so the practical harm is
small, but "write nothing" is not literally true and no test covers it.

### S6 — The token sweep reads every file under the target, including binaries and the chain

The final sweep for a surviving token (`Invoke-ScaffoldManifest.ps1`, lines 1001–1025) walks
every file under the target skipping only `.git`, and reads each one with
`File.ReadAllText`. Two consequences worth knowing before a gap-filling run in a mature
repository: it reads every DLL and image under `bin/` and `obj/` as text, and it reads the
artifact chain under `.sdlc/`. A repository whose own spec quotes the pack's token — this one
does — would fail its own sweep. Neither is in scope here; both are cheap to fix by skipping the
ignore file's own directories.

### S7 — Unchecked pack requirements

Three more requirements are implemented and correct but have no instrument: **R47** (the manifest
declares every file the pack scaffolds, so nothing but the executor writes one) — I verified by
hand that all 14 assets on disk are referenced by `scaffold.json` and none is orphaned; **R67**
(the competition kind selects the machine-learning profile) — `"profile": "dotnet-ml"` is
declared and nothing reads it; and **R68** (the starter notebook carries headings and a solution
reference only, with no executable logic) — the notebook is eight markdown cells and no code
cell, verified by reading it.

### S8 — The emitted integration suite never starts the composed system

`assets/web.IntegrationStarterTests.cs` asserts the assembly name matches the coverage exclusion
and deliberately does not start the AppHost. That satisfies R64 as written — where a container
runtime exists, the integration tests run and report at least one test executed with zero
failures — and the asset's own comment explains the choice honestly. But it means the container
runtime R64 turns on makes no difference to what is actually exercised: the same non-container
test runs either way. Worth the owner knowing, since R64's wording implies the composed system is
being reached.

---

## Requirement coverage

Live requirements only. R6 (the old report shape), R8 (the old token rule), R11 (the old
`required` rewrite), R14 (the two-argument invocation) and R33 (the old integration-test
criterion) are struck through in the spec and are correctly claimed by nobody.

| Req | What it says | Implemented | Tested | Note |
|---|---|---|---|---|
| R1 | every pack ships a scaffold manifest | yes | yes | `EveryPack_ShipsAScaffoldManifest`, with a non-vacuity guard |
| R2 | the engine names no pack string | yes | yes | `EngineExecutablesImposeNoStack`; term list is narrow — see S2 |
| R3 | actions run in order, halt at the first failure | yes | yes | `FailingAction_HaltsBeforeTheActionsAfterIt` checks the later action did not run |
| R4 | a path outside the target is refused, nothing written | yes | yes | four rows, one per action kind, each asserting the outside file is unchanged |
| R5 | an existing destination is skipped, left byte-identical | yes | yes | `ExistingDestination_IsSkippedAndLeftByteIdentical` |
| R7 | a second run changes no file and exits 0 | yes | yes | tested twice — fixture manifest and the real pack, per kind, by tree hash |
| R9 | preflight runs first; on failure nothing is written | yes | yes | `FailingPreflight_WritesNothingAndRelaysTheRemediation` |
| R10 | `unless_exists` skips the action (restated as R43) | yes | partial | see R43 |
| R12 | an unknown or missing kind lists the declared kinds | yes | yes | plus an unknown manifest version refused before any action |
| R13 | no scaffold action while the charter is draft | yes | n/a | `init` prose; the substantive half is machine-checked by R38 |
| R15 | stop and name the missing answer if kind or boundaries unsettled | yes | no | `init` prose, human-verified by design |
| R16 | a re-run offers charter amendment and gap-filling | yes | no | `init` prose, human-verified by design |
| R17 | derive the identifier from the directory name | yes | yes | four derivation rows; the confirmation half is `init` prose |
| R18 | an underivable candidate is refused, not repaired | yes | yes | `--` and `9lives` rows, asserting nothing was substituted |
| R19 | recover the identifier from an existing solution file | yes | yes | `ExistingSolutionFile_IsRecoveredRatherThanRederived` |
| R20 | relay the report verbatim; never claim success on non-zero | yes | no | `init` prose; weakened by S1 when there is no report to relay |
| R21 | the web kind emits its seven artefacts | yes | **no** | **B2** — nothing asserts which projects exist |
| R22 | the competition kind emits its five artefacts | yes | **no** | **B2** |
| R23 | emitted names match a coverage exclusion | yes | yes | patterns parsed from the emitted `coverage.runsettings`, not copied |
| R24 | the emitted README carries five things | yes | **no** | **B2** — the plan's Tests table promised this row |
| R25 | a base ignore file alongside the Terraform entries | yes | **no** | **B2** — same row |
| R26 | sample content removed; at least one executable test left | yes | partial | the "at least one test" half is held by the test count; the removal half is **not** — see B1 |
| R27 | strip every version attribute from every package reference | yes | yes | asserted over every emitted project file, plus the restore |
| R28 | the pack pins every package a generator emits | yes | yes | the restore is the only instrument, and it runs |
| R29 | rewrite the inline SDK version to the pinned one | yes | yes | indirectly — the manifest's own `assert_after` fires on the real run |
| R30 | a missing template fails preflight; nothing is installed | yes | yes | `NoManifest_InstallsItsOwnPrerequisite` plus the preflight rows |
| R31 | the emitted solution builds with exit 0 | yes | yes | `ScaffoldEmitsBuildableSolution_Web` / `_Competition` |
| R32 | the unit suite reports a count above zero and no failures | yes | yes | counts read from a TRX, not off the console |
| R34 | a partial repository is filled, everything else byte-identical | yes | yes | `tests/` deleted and re-executed, both halves compared by hash |
| R35 | the engine still satisfies I1 | yes | yes | both I1 checks |
| R36 | the repository README describes what `init` produces | yes | **no** | the spec's Test strategy promised a check; there is none |
| R37 | CI installs a prerequisite the manifest declares | yes | yes | `ScaffoldPrerequisiteTests`; satisfiable by a comment — see S3 |
| R38 | refuse a charter that is not approved | yes | yes | `DraftCharter_WritesNothingAndExitsNonZero`, asserting an empty tree |
| R39 | refuse when no charter exists | yes | yes | two rows; but see S5 on the directory left behind |
| R40 | resolve the artifact root and name it in the report | yes | yes | tested against a non-default root, so it cannot be an assumption |
| R41 | rewrite and delete only touch paths this run created | yes | yes | proven with pre-existing files that match the globs |
| R42 | `assert_after` must hold after a rewrite | yes | yes | `AssertAfterThatDoesNotHold_FailsTheRunNamingTheFile` |
| R43 | `unless_exists` binds every action kind | yes | partial | **S4** — only `run` is exercised |
| R44 | a rewrite or delete glob outside the target is refused | yes | yes | two of the four containment rows |
| R45 | `append` creates, or adds only the missing lines | yes | yes | three rows, including a UTF-8 round-trip regression |
| R46 | `append` is a no-op recorded as skipped when all lines are present | yes | yes | asserted byte-for-byte |
| R47 | the manifest declares every file the pack scaffolds | yes | no | **S7** — verified by hand, 14 of 14 assets referenced |
| R48 | `init` writes only the charter | yes | no | `init` prose, human-verified by design |
| R49 | the pack's skill cites the manifest, carries no second list | yes | yes | `PackSkillCitesItsManifest` |
| R50 | any surviving token fails the run, naming file and token | yes | yes | including a token this run did not write |
| R51 | a supplied identifier wins over derivation and recovery | yes | yes | `SuppliedIdentifier_WinsOverDerivationAndRecovery` |
| R52 | an invalid supplied identifier is refused | yes | yes | four rows: empty, leading digit, space, dash |
| R53 | more than one solution file is refused; none falls back to derivation | yes | yes | both halves |
| R54 | the executable scan reuses the existing term list | yes | yes | one shared member, no copy — see S2 on its contents |
| R55 | a stack term in a shipped engine executable fails the named check | yes | yes | `EngineExecutablesImposeNoStack` |
| R56 | the report records every action with an outcome | **partial** | partial | **B1** — no-op rewrites and deletes are absent; a crash emits nothing (S1) |
| R57 | a preflight declared inside actions is rejected | yes | yes | `PreflightDeclaredInsideActions_IsRejected` |
| R58 | a rewrite is scoped to created files and records `mutated` | yes | yes | `RewriteAndDelete_TouchOnlyWhatThisRunCreated` |
| R59 | relay every failed, mutated and removed entry, never a count | yes | no | `init` prose, human-verified by design |
| R60 | invoke the executor with all four arguments | yes | no | `init` prose; the command line is used by both test classes |
| R61 | the report carries a schema version; a consumer rejects an unknown one | yes | partial | the producer is asserted; the consumer half is tested against the test's own helper, since the real consumer is prose |
| R62 | the pack ships a starter test asserting a real property | yes | yes | indirectly, via the test count; the asset asserts the assembly-name contract |
| R63 | `run` starts inside the target; a `cwd` outside it is refused | yes | yes | `RunWhoseWorkingDirectoryEscapesTheTarget_IsRefused` |
| R64 | the integration suite runs and reports a count, or compiles and says so | yes | yes | the run branch was exercised here; the compile-only branch was not — see **Not checked** |
| R65 | an absent prerequisite fails rather than skips | yes | yes | `NoRowInThisClassCanSkip`, plus 0 skipped in the real run |
| R66 | restore succeeds with transitive pinning on | yes | yes | the property is asserted present before the restore is trusted |
| R67 | the competition kind selects the machine-learning profile | yes | no | **S7** — declared, nothing reads it |
| R68 | the starter notebook is headings only, no executable logic | yes | no | **S7** — verified by reading it |

**43 of 63 live requirements have an automated check that can fail.** Seven are `init`'s own
prose and are human-verified by design (R15, R16, R20, R48, R59, R60, and R13's ordering half).
Five are partial (R10/R43, R26, R47 as unchecked, R56, R61). The remaining gaps are R21, R22,
R24, R25, R36, R67 and R68.

---

## Manifest drift

`git diff --name-only` plus untracked files against the plan's frontmatter slice manifest.

**Files changed that no slice claims — two, both expected:**

| File | Status |
|---|---|
| `plugins/stack-dotnet/skills/stack-dotnet/assets/competition.readme.md` | accepted drift, recorded in the plan |
| `plugins/stack-dotnet/skills/stack-dotnet/assets/web.IntegrationStarterTests.cs` | accepted drift, recorded in the plan |

Both sit inside `plugins/stack-dotnet/`, which the `pack` slice owns exclusively, so slice
disjointness is unaffected. The plan's **Accepted drift** section records the owner accepting
them on 2026-09-18 and asks that they be reported as drift rather than treated as blocking. They
are reported, and they are not blocking. I checked the reasoning and agree with it: R24 requires
the emitted README to carry the orchestration-versus-infrastructure invariant, which is
meaningless in a competition repository, so one shared README could not honestly serve both
kinds; and the generated sample integration test has zero live tests, so R26 (delete sample
content) and R64 (the integration suite reports a count above zero) contradict each other unless
something replaces it.

**Files claimed by a slice that nothing touched — none.** All twenty files in the plan's
manifest are present and modified or created.

The competition kind also declares one preflight check where the plan's prose said it "carries
none". The plan records that too, and the implementer's reading was right: the spec's failure-modes
table is explicit that an absent SDK is a pack-declared preflight for every kind.

---

## Build and test output

Real output, run by me on this machine, 2026-09-18.

```
$ dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo

  Determining projects to restore...
  All projects are up-to-date for restore.
  AiSdlc.Tests -> C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll
Test run for C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   115, Skipped:     0, Total:   115, Duration: 5 m 13 s
```

Reading the counts rather than the exit code, as R65 asks: **115 executed, 0 failed, 0 skipped.**
Nothing skipped its way to green. The build produced no errors and no warnings.

This machine has a working Docker daemon (`docker info` exits 0), so the integration half of
R64 took its run branch rather than its compile-only branch.

I re-ran the twelve end-to-end rows plus the no-skip guard with detailed output, because the
evidence the spec's **End-to-end verification** section asks for — a machine creating a solution
from nothing and watching it compile and pass its own tests — is only visible there:

```
$ dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo \
    --filter "FullyQualifiedName~ScaffoldEmitsBuildableSolutionTests" \
    --logger "console;verbosity=detailed"

  Passed ScaffoldEmitsBuildableSolution_Web
    web: scaffold exit 0, restore exit 0, build exit 0
  Passed ScaffoldEmitsBuildableSolution_Competition
    competition: scaffold exit 0, restore exit 0, build exit 0
  Passed RestoreSucceedsUnderCentralPackageManagement_Web
    web: 6 emitted project(s), none carrying a version attribute
  Passed RestoreSucceedsUnderCentralPackageManagement_Competition
    competition: 3 emitted project(s), none carrying a version attribute
  Passed EmittedSuitesReportATestCount_Web
    web: container runtime detected (docker)
    web / Probe.IntegrationTests: ran 1 test(s), 0 failed
    web / Probe.UnitTests: ran 2 test(s), 0 failed
  Passed EmittedSuitesReportATestCount_Competition
    competition / Probe.UnitTests: ran 2 test(s), 0 failed
  Passed EmittedNamesMatchACoverageExclusion_Web
    web: exclusions [*.UnitTests, *.IntegrationTests, AppHost, *.AppHost]
    web: held Probe.AppHost, Probe.IntegrationTests, Probe.UnitTests
  Passed EmittedNamesMatchACoverageExclusion_Competition
    competition: held Probe.UnitTests
  Passed SecondExecutionChangesNothing_Web
    web: re-executed over 45 file(s), none changed
  Passed SecondExecutionChangesNothing_Competition
    competition: re-executed over 17 file(s), none changed
  Passed RefillsOnlyTheDeletedTestsDirectory_Web
    web: deleted 5 file(s) under tests/ and got them all back, with 40 file(s) elsewhere byte-identical
  Passed RefillsOnlyTheDeletedTestsDirectory_Competition
    competition: deleted 3 file(s) under tests/ and got them all back, with 14 file(s) elsewhere byte-identical
  Passed NoRowInThisClassCanSkip

Test Run Successful.  Total tests: 13   Passed: 13   Total time: 4.73 Minutes
```

This is the substantive claim of the whole feature and it holds: **both kinds scaffolded from an
empty directory, restored, built, and ran their own tests, in this run.** The project counts also
happen to confirm R21 and R22 on disk — six projects for the web kind (AppHost, ServiceDefaults,
Web, Api, UnitTests, IntegrationTests) and three for the competition kind (Features, Training,
UnitTests). Nothing asserts those counts, which is finding **B2**; what the output shows is that
the manifest is correct today, not that anything would notice if it stopped being.

One line is worth reading twice: `web / Probe.IntegrationTests: ran 1 test(s)`. That one test is
the assembly-name assertion in `assets/web.IntegrationStarterTests.cs`. It satisfies R64's letter
and exercises nothing about the composed system — see **S8**.

---

## Not checked

- **The compile-only branch of R64.** This machine has a container runtime, so
  `EmittedSuitesReportATestCount_*` exercised the "run it" path. The path where no runtime exists
  — the integration suite compiles, is not run, and the row says so in its output — was not
  executed here and could not be, short of hiding Docker from the process.
- **Mutation testing.** Charter invariant I9 requires every new row in its table to be
  mutation-tested — inject the fault, watch the named check fail. The spec asks for one fault per
  criterion S1–S9 plus one per executor test. I read the tests closely and am satisfied each one
  asserts real state rather than passing vacuously, and I ran the shipped executor directly to
  confirm three behaviours by hand. But I did not inject faults and re-run, so the I9 obligation
  for the eleven new charter rows is outstanding.
- **The human-verified half of S10 and all of S11.** The spec reserves these: that `init` calls
  the executor at the right point in its own prose, and that declining the module-boundary
  question stops the command rather than selecting a kind. Both need `/sdlc-core:init` run by
  hand in an empty directory. I read the skill body and the prose says the right things; I did
  not run it.
- **A clean machine without the Aspire templates.** The web kind's preflight refusal path is
  covered by fixture tests, but the real pack's real preflight against a machine that genuinely
  lacks `Aspire.ProjectTemplates` was not exercised — this machine has them.
- **CI on a hosted runner.** `.github/workflows/checks.yml` gained the template install step and
  `ScaffoldPrerequisiteTests` holds it against the manifest, but I did not push, so the workflow
  has not actually run.
- **The charter's I9 row for `EveryEngineSkill_CitesThePlainLanguageRule`.** The row names a
  check that exists in `tests/AiSdlc.Tests/SkillAndAgentTests.cs` and passes, but that check
  predates this change and neither the spec nor the plan asked for the row. It is harmless
  paperwork; I note it only because the charter amendment is slightly wider than the spec
  specified — eleven rows added where the spec listed nine.

---

# Verification 2 — 2026-09-19, of the state after the first verification's findings were acted on

*Everything above this line is the first verification, dated 2026-09-18. It describes this
change **before** its findings were acted on, and it stands as written. What follows is a
second, independent verification of a different state: commit `fd2ec17` plus the uncommitted
working tree listed under **Manifest drift** below, read and run on 2026-09-19.*

## Verdict

**DO NOT SHIP.** The suite is red — 128 tests, 127 passed, 0 skipped, **1 failed** — and the
failure is a live requirement of this change that is not implemented at all: R37, which says
this repository's own CI workflow must install a prerequisite that a pack's manifest declares
and a hosted runner does not have. The workflow file is byte-identical to the committed
version; nothing in it installs anything, and the check that holds R37 says so by name. The
repository's own `README.md`, changed by this same change, meanwhile states that the CI job
does install those templates. Charter invariant I12 — nothing is committed on a red build or a
failing test, both suites, real output seen — blocks on the red suite alone.

A second blocking finding is separate and has nothing to do with CI: the executor's final
sweep for an unsubstituted token reads **every** file under the target including the artifact
chain, so a repository whose own documents quote the pack's `{{SOLUTION}}` placeholder can
never be re-run. This repository is one. I reproduced it against the shipped script.

**The first verification's four substantive findings are genuinely closed, and I checked the
closures rather than taking them.** The report now records a rewrite or a delete that matched
nothing; an unexpected I/O failure now produces a report instead of a stack trace; the frozen
emitted layout, the emitted README and the emitted ignore file all have real checks; and the
stack-term list the two invariant-I1 instruments share now contains the file extension and the
token spelling it was missing. Each of those new checks asserts state that can actually be
wrong — I read them for the shape the lead asked me to hunt, a check added to satisfy a finding
that cannot fail, and did not find one. The one place the fix went wrong is the opposite shape:
the check that could be satisfied by a comment was correctly given teeth, and then the thing it
was supposed to bite on was removed.

---

## Blocking findings

### B3 — Nothing in CI installs the web kind's prerequisite, so R37 is unimplemented and the suite is red

**What R37 asks for:** where a solution kind's manifest declares something that must already be
present on the machine, and a hosted runner does not have it, this repository's own CI workflow
installs it as an explicit step before the suite runs — while the manifest still refuses to
install it itself. The split is the point: a global install on a person's workstation outlives
the repository that caused it and was never consented to, whereas a hosted runner is ephemeral
and declared in a reviewed file.

**What is on disk.** `.github/workflows/checks.yml` is byte-identical to the committed version.
`git status` reports it unmodified; `git log -- .github/workflows/checks.yml` shows its last
change was commit `5843719`, which predates this change entirely. The file has four steps:
check out, set up the SDK, set up Node, run the suite. Nothing installs the Aspire project
templates, and no line anywhere in the file names `aspire-apphost`, `aspire-servicedefaults` or
`aspire-mstest`. A grep across the whole repository for `Aspire.ProjectTemplates` finds it only
inside the pack's own remediation strings and one sentence of the pack's skill.

The pack's manifest declares those three template ids as preflight checks for the web kind —
`dotnet new list aspire-apphost` and its two siblings — so they are exactly the prerequisites
R37 is about.

**The check that holds R37 fails, and names all three:**

```
Failed EveryDeclaredPrerequisite_IsNamedByCIBeforeTheSuiteRuns [20 ms]
  Assert.AreEqual failed. Expected:<0>. Actual:<3>.
  .github/workflows/checks.yml does not name these prerequisites before it runs the suite,
  so the kind that needs them cannot be proven on a hosted runner - add a step that provides
  them, and name them in the step itself; a comment naming them does not count, because a
  comment survives the step's deletion:
  stack-dotnet / web: aspire-apphost  (probed by: dotnet new list aspire-apphost)
  stack-dotnet / web: aspire-servicedefaults  (probed by: dotnet new list aspire-servicedefaults)
  stack-dotnet / web: aspire-mstest  (probed by: dotnet new list aspire-mstest)
```

I ran that class on its own afterwards and got the identical failure, so it is not a
mid-run edit or a stale build: 4 tests, 3 passed, 1 failed.

**The concrete failure scenario, beyond the red suite.** Push this branch. The
`windows-latest` image has no Aspire templates. The web kind's first preflight check fails,
the executor writes nothing and relays the pack's install instruction, and
`ScaffoldEmitsBuildableSolution_Web` — the row the spec's **End-to-end verification** section
names as the single check that proves the whole feature — fails with a remediation message
instead of building anything. The one criterion this change exists to make provable is then
unprovable on every push, which is the state R37 was written to prevent.

**And the documentation now says the opposite of the code.** `README.md`, changed by this same
slice, gained this paragraph:

> The CI job installs the Aspire project templates before the suite runs, because the web
> solution kind's preflight requires them and a hosted runner does not have them.

That sentence is false as of this working tree. R36 — the repository README describes what the
setup command produces — is otherwise satisfied and checked, so the README is right about the
emitted layout and wrong about CI, which is the harder kind of wrong to notice.

**How this relates to the first verification's S3, because the shape matters.** That finding
said the check could be satisfied by a comment: the three template ids appeared only in a
`# Provides: ...` line, so deleting the install step and leaving the sentence kept the check
green. **The check was hardened correctly and that half of the fix is real.** A new helper
strips a YAML comment off a line before matching, and a new row,
`APrerequisiteNamedOnlyByAComment_IsNotNamedAtAll`, builds two fabricated workflows that differ
by exactly one thing — the step — and asserts the comment-only one fails. It passes, and it
would fail if the comment rule were relaxed. What is missing is the other half: the step the
hardened check is supposed to find. Between the first verification and this one the comment was
removed and the step went with it, leaving a check with teeth and nothing to bite.

**Fix shape:** put the install step back in `checks.yml`, with the three template ids named in
the `run:` line itself rather than in a comment beside it, before the line that runs the suite.

### B4 — The token sweep reads the artifact chain, so this repository can never be re-scaffolded

**What R50 asks for:** when the executor finishes, if any file under the target directory still
contains a token the manifest declares, it exits non-zero naming the file and the token —
whether or not this execution wrote that file. The "whether or not" is deliberate and is the
complaint the whole change started from: a placeholder left behind by an earlier run, in a file
nobody is looking at, produces assemblies literally named `{{SOLUTION}}.Foo`.

**The executor does exactly that, and that is the problem.** The sweep walks every file under
the target, skipping only `.git`, and the artifact chain under `.sdlc/` is under the target.
So any repository whose own documents quote the pack's placeholder fails its own scaffold run —
and a repository's chain is precisely where somebody writes down what the scaffold emits.

**Reproduced against the shipped script**, not predicted. I gave a temporary repository an
approved charter, a manifest whose actions all skip, and one file at `.sdlc/notes.md`
containing the text `{{SOLUTION}}.Foo`:

```
"actions":  [ ... ,
                {
                    "kind":  "token",
                    "outcome":  "failed",
                    "path":  ".sdlc/notes.md",
                    "reason":  "token {{SOLUTION}} was not substituted"
                }
            ]
EXIT=1
```

**The concrete failure scenario is this repository.** Four files in its own chain contain the
placeholder — `.sdlc/init-scaffolds-solution/intent.md`, `plan.md`, `spec.md` and this
`verification.md`. R16 says that running the setup command again on a repository that already
has an approved charter offers two things, one of which is filling scaffold gaps, and the
`init` skill now says so in its closing section. Doing that here halts with
`token {{SOLUTION}} survives in: .sdlc/init-scaffolds-solution/spec.md, ...` and, per R20, the
skill is required to relay that verbatim and not claim the repository is scaffolded. The owner
is told the scaffold failed when nothing about the scaffold is wrong.

**This is a requirement problem, not a coding mistake, and it should be fixed as one.** The
executor implements R50 to the letter; it is the letter that is too wide. The narrow version
that still catches the regression R50 exists for is to sweep what the pack emits and skip the
engine's own artifact root — which the executor already resolves and already has in hand as
`$script:ArtifactRoot`. I differ from the first verification, which recorded this as
*"neither is in scope here"* under its S6: the executor is new in this change, R50 is a live
requirement of this change, and the flow it breaks (R16's gap-filling) is one this change
introduces.

(The same sweep also reads every compiled binary under `bin/` and `obj/` as text. That is a
cost, not a defect — I confirmed a binary file does not produce a false positive — and it
disappears with the same fix if the ignore file's own directories are skipped.)

---

## Should fix

These four carry forward from the first verification unchanged, plus one that its B1 fix
reframes rather than removes. None of them is a reason on its own to hold the change.

### S9 — R43's one sentence over R10 still has no test, though it is true

R43 says `unless_exists` — skip this action if that path is already there — binds **every**
action kind and not only `run`. Every fixture in `ScaffoldManifestTests.cs` that exercises
`unless_exists` declares it on a `run` action; the fixture builder only offers it there.

I verified it by hand rather than leaving it at "implemented". A manifest declaring
`unless_exists` on one action of each of the six kinds, against a target where the sentinel
file exists, produced six `skipped` records and an empty tree:

```
{ "kind": "mkdir",   "outcome": "skipped", "path": "sentinel.txt", "reason": "sentinel.txt is already present" },
{ "kind": "copy",    "outcome": "skipped", ... },
{ "kind": "append",  "outcome": "skipped", ... },
{ "kind": "delete",  "outcome": "skipped", ... },
{ "kind": "rewrite", "outcome": "skipped", ... }
```

The behaviour is correct and generic — it sits above the `switch`, not inside it. What is
missing is only the test, and the cost of adding it is one optional argument on the fixture
builders.

### S10 — A refused run still leaves an empty target directory behind

R39 says that with no charter the executor writes nothing and exits non-zero. It creates the
target directory before the approval gate and then refuses. Pointed at a path that does not
exist, it exits 1 having created an empty directory — I confirmed this again against the
shipped script. The code comments acknowledge the ordering and the directory is empty, so the
practical harm is small, but "writes nothing" is not literally true and no test covers it.

### S11 — R47 has no instrument

R47 — the manifest declares every file the pack scaffolds, so nothing but the executor writes
one — is the requirement the whole gate-hook argument rests on, and nothing checks it. I
verified it by hand: all fourteen files under the pack's `assets/` directory are referenced by
`scaffold.json`, and none is orphaned. A check comparing the two sets is a handful of lines and
belongs beside `PackSkillCitesItsManifest`, which already walks every pack.

### S12 — The deletes that strip sample content are now visible, but still not measured

The first verification's B1 is closed: a `rewrite` or a `delete` whose glob matches nothing now
records a `skipped` entry with the glob and a reason, and
`RewriteAndDeleteThatMatchNothing_AreStillRecordedWithAnOutcome` counts declared actions against
recorded ones rather than looking for outcomes, so it would have failed against the old code.

What that closes is R56 — the report records every action — which is the requirement B1 was
raised against. It does not close R26's other half: **nothing in the suite asserts that the five
`delete` actions ever hit anything.** I grepped `ScaffoldEmitsBuildableSolutionTests.cs` for
every sample-content filename the manifest names and found none of them. So the day an SDK
renames `WeatherForecastController.cs`, the suite stays green, the run exits 0, and the only
thing that says a word is a `skipped` row in a report a human has to read. That is a genuine
improvement over silence and it is not an instrument. Asserting the emitted tree contains none
of the sample filenames would be one.

### S13 — The emitted integration suite never starts the composed system

`assets/web.IntegrationStarterTests.cs` asserts that its own assembly name matches the coverage
exclusion pattern, and deliberately does not start the orchestration host. That satisfies R64
as written — where a container runtime exists the integration tests run and report at least one
test executed with zero failures — and the asset's own comment explains the choice honestly.
The consequence worth the owner knowing is that the container runtime R64 turns on makes no
difference to what is exercised: the same non-container test runs either way, so a green
integration row is not evidence that the composed system starts.

### Closed since the first verification, and checked rather than taken

| First verification's finding | State now |
|---|---|
| **B1** — the report omitted a rewrite or delete that matched nothing | **Closed.** Both branches record `skipped` with the glob and a reason; the new test counts declared actions against recorded ones. |
| **B2** — no check on the emitted layout, README or ignore file | **Closed.** `EmittedLayoutIsTheFrozenOne_*` compares the emitted project set for *equality* against an expectation written independently of the manifest, and also asserts each project is registered in the solution file; `EmittedReadmeCarriesWhatTheOwnerNeeds_*` and `BaseIgnoreEntriesAccompanyTheTerraformOnes_*` state their expectations in the test rather than deriving them from the asset. All three guard against an empty expectation list. R36, R67 and R68 gained real checks in the same pass. |
| **S1** — an I/O failure produced no report | **Closed.** The action loop is wrapped, the exception's own message becomes the reason, and `UnexpectedFailureInsideAnAction_IsReportedAsFailedRatherThanThrown` asserts the report says `halted` and that the reason carries the underlying error rather than merely that there was one. |
| **S2** — the I1 term list missed the examples that justified the check | **Closed.** The shared list now carries `slnx`, `.sln`, `csproj`, `fsproj`, `runsettings`, `ipynb`, `{{solution}}`, `apphost`, `servicedefaults` and a dozen more, with a word-boundary rule documented for why `pip` does not fire on "pipe". I confirmed by hand that all four shipped engine executables are still clean of every term. |
| **S3** — R37's check was satisfiable by a comment | **Half closed, and the other half regressed.** The check is now correct and has its own proof; the install step it needs is gone. See **B3**. |
| **S4** → now **S9**, **S5** → now **S10**, **S6** → now **B4**, **S7** → now **S11** (R67 and R68 gained checks; R47 did not), **S8** → now **S13**. |

---

## Requirement coverage

Live requirements only. R6 (the superseded report shape), R8 (the superseded token rule), R11
(the superseded `required` rewrite), R14 (the two-argument invocation) and R33 (the superseded
integration-test criterion) are struck through in the spec and are correctly claimed by nobody.

| Req | What it says | Implemented | Tested | Note |
|---|---|---|---|---|
| R1 | every pack ships a scaffold manifest | yes | yes | `EveryPack_ShipsAScaffoldManifest`, with a non-vacuity guard |
| R2 | the engine names no pack string | yes | yes | both I1 checks; the shared term list is now wide enough — see the closed-findings table |
| R3 | actions run in order and halt at the first failure | yes | yes | `FailingAction_HaltsBeforeTheActionsAfterIt` checks the later action did not run |
| R4 | a path outside the target is refused, nothing written | yes | yes | one row per action kind, each asserting the outside file is unchanged |
| R5 | an existing destination is skipped, left byte-identical | yes | yes | `ExistingDestination_IsSkippedAndLeftByteIdentical` |
| R7 | a second run changes no file and exits 0 | yes | yes | tested twice — fixture manifest, and the real pack per kind by tree hash |
| R9 | preflight runs first; on failure nothing is written | yes | yes | `FailingPreflight_WritesNothingAndRelaysTheRemediation` |
| R10 | `unless_exists` skips the action (restated as R43) | yes | partial | see R43 |
| R12 | an unknown or missing kind lists the declared kinds | yes | yes | plus an unknown manifest version refused before any action |
| R13 | no scaffold action while the charter is draft | yes | n/a | `init` prose; the substantive half is machine-checked by R38 |
| R15 | stop and name the missing answer if the kind or the boundaries are unsettled | yes | no | `init` prose, human-verified by design |
| R16 | a re-run offers charter amendment and gap-filling | yes | no | `init` prose; **the gap-filling half is broken in practice by B4** |
| R17 | derive the identifier from the directory name | yes | yes | four derivation rows; the confirmation half is `init` prose |
| R18 | an underivable candidate is refused, not repaired | yes | yes | `--` and `9lives` rows, asserting nothing was substituted |
| R19 | recover the identifier from an existing solution file | yes | yes | `ExistingSolutionFile_IsRecoveredRatherThanRederived` |
| R20 | relay the report verbatim; never claim success on a non-zero exit | yes | no | `init` prose, human-verified by design |
| R21 | the web kind emits its seven artefacts | yes | **yes** | **new** — `EmittedLayoutIsTheFrozenOne_Web`, set equality plus solution registration |
| R22 | the competition kind emits its five artefacts | yes | **yes** | **new** — `EmittedLayoutIsTheFrozenOne_Competition` |
| R23 | emitted names match a coverage exclusion | yes | yes | patterns parsed from the emitted `coverage.runsettings`, not copied |
| R24 | the emitted README carries the build, test and local-run commands, the chain's location and the authority invariant | yes | **yes** | **new** — `EmittedReadmeCarriesWhatTheOwnerNeeds_*`, expectations stated in the test |
| R25 | a base ignore file alongside the Terraform entries | yes | **yes** | **new** — `BaseIgnoreEntriesAccompanyTheTerraformOnes_*`, both sets asserted |
| R26 | sample content removed; at least one executable test left | yes | partial | the "at least one test" half holds; the removal half is visible in the report and not measured — **S12** |
| R27 | strip every version attribute from every package reference | yes | yes | asserted over every emitted project file, plus the restore |
| R28 | the pack pins every package a generator emits | yes | yes | the restore is the only instrument, and it runs |
| R29 | rewrite the inline SDK version to the pinned one | yes | yes | indirectly — the manifest's own `assert_after` fires on the real run |
| R30 | a missing template fails preflight; nothing is installed | yes | yes | `NoManifest_InstallsItsOwnPrerequisite` plus the preflight rows |
| R31 | the emitted solution builds with exit 0 | yes | yes | `ScaffoldEmitsBuildableSolution_Web` / `_Competition` |
| R32 | the unit suite reports a count above zero and no failures | yes | yes | counts read from a TRX, not off the console |
| R34 | a partial repository is filled, everything else byte-identical | yes | yes | `tests/` deleted and re-executed, both halves compared by hash |
| R35 | the engine still satisfies I1 | yes | yes | both I1 checks |
| R36 | the repository README describes what the setup command produces | yes | **yes** | **new** — `RepositoryReadme_ListsTheLayoutItsPacksEmit` reads the expectation out of each pack's manifest; `RepositoryReadme_DoesNotOfferTheRetiredSetupCommand`. The README's *CI* paragraph is nonetheless false — see **B3** |
| R37 | CI installs a prerequisite the manifest declares | **no** | yes | **B3** — the check exists, has teeth, and fails; the workflow step does not exist |
| R38 | refuse a charter that is not approved | yes | yes | `DraftCharter_WritesNothingAndExitsNonZero` |
| R39 | refuse when no charter exists | yes | yes | two rows; see **S10** on the empty directory left behind |
| R40 | resolve the artifact root and name it in the report | yes | yes | tested against a non-default root, so it cannot be an assumption |
| R41 | rewrite and delete touch only paths this run created | yes | yes | proven with pre-existing files that match the globs |
| R42 | `assert_after` must hold after a rewrite | yes | yes | `AssertAfterThatDoesNotHold_FailsTheRunNamingTheFile` |
| R43 | `unless_exists` binds every action kind | yes | no | **S9** — correct for all six kinds, hand-verified here; only `run` is exercised by a test |
| R44 | a rewrite or delete glob outside the target is refused | yes | yes | two of the four containment rows |
| R45 | `append` creates, or adds only the missing lines | yes | yes | three rows, including a UTF-8 round-trip regression |
| R46 | `append` is a no-op recorded as skipped when all lines are present | yes | yes | asserted byte-for-byte |
| R47 | the manifest declares every file the pack scaffolds | yes | no | **S11** — verified by hand, 14 of 14 assets referenced |
| R48 | the setup command writes only the charter | yes | no | `init` prose, human-verified by design |
| R49 | the pack's skill cites the manifest and carries no second list | yes | yes | `PackSkillCitesItsManifest` |
| R50 | any surviving token fails the run, naming file and token | yes | yes | including a token this run did not write — and see **B4** on how wide "any file" turns out to be |
| R51 | a supplied identifier wins over derivation and recovery | yes | yes | `SuppliedIdentifier_WinsOverDerivationAndRecovery` |
| R52 | an invalid supplied identifier is refused | yes | yes | four rows: empty, leading digit, space, dash |
| R53 | more than one solution file is refused; none falls back to derivation | yes | yes | both halves |
| R54 | the executable scan reuses the existing term list | yes | yes | one shared member, no copy |
| R55 | a stack term in a shipped engine executable fails the named check | yes | yes | `EngineExecutablesImposeNoStack`, with a non-vacuity guard over four files |
| R56 | the report records every action with an outcome | yes | **yes** | **closed** — no-op rewrites and deletes recorded; an unexpected failure now produces a report |
| R57 | a preflight declared inside actions is rejected | yes | yes | `PreflightDeclaredInsideActions_IsRejected`, by kind and by stray field |
| R58 | a rewrite is scoped to created files and records `mutated` | yes | yes | `RewriteAndDelete_TouchOnlyWhatThisRunCreated` |
| R59 | relay every failed, mutated and removed entry, never a count | yes | no | `init` prose, human-verified by design |
| R60 | invoke the executor with all four arguments | yes | no | `init` prose; the command line is used verbatim by both test classes |
| R61 | the report carries a schema version; a consumer rejects an unknown one | yes | yes | the producer is asserted; the consumer half is tested against the test's own reader, since the real consumer is prose |
| R62 | the pack ships a starter test asserting a real property | yes | yes | via the test count; the asset asserts the assembly-name contract |
| R63 | `run` starts inside the target; a `cwd` outside it is refused | yes | yes | `RunWhoseWorkingDirectoryEscapesTheTarget_IsRefused` |
| R64 | the integration suite runs and reports a count, or compiles and says so | yes | yes | the run branch was exercised here; see **S13** on what it exercises |
| R65 | an absent prerequisite fails rather than skips | yes | yes | `NoRowInThisClassCanSkip`, plus 0 skipped across the whole run |
| R66 | restore succeeds with transitive pinning on | yes | yes | the property is asserted present before the restore is trusted |
| R67 | the competition kind selects the machine-learning profile | yes | **yes** | **new** — declaration *and* pins, with the web kind asserted not to declare it |
| R68 | the starter notebook is headings only, no executable logic | yes | **yes** | **new** — no code cell, no recorded output, no execution count, no fenced block, and a minimum heading count so an empty file cannot pass |

**52 of 63 live requirements have an automated check that can fail** — up from 43 at the first
verification. Seven are the setup command's own prose and are human-verified by design (R15,
R16, R20, R48, R59, R60, and R13's ordering half). Four are implemented and unchecked: R10/R43
(`unless_exists` on kinds other than `run`), R26's removal half, and R47. **One is not
implemented at all: R37**, and its check is what turns the suite red.

---

## Manifest drift

`git diff --name-only` plus untracked files, against the plan's frontmatter slice manifest.

**A file a slice claims that nothing touched — one, and it is finding B3:**

| File | Slice | Status |
|---|---|---|
| `.github/workflows/checks.yml` | `release` | **byte-identical to the committed version.** `git status` does not list it; `git log` shows its last change was `5843719`, before this change began. This is the slice that did not land. |

The first verification recorded this file as carrying the install step on 2026-09-18, so the
step existed and was removed between then and now. The file's modification time is 2026-09-19
06:41 with content matching the committed version, which is consistent with it having been
rewritten back rather than never written.

**Files changed that no slice claims — two, both expected:**

| File | Status |
|---|---|
| `plugins/stack-dotnet/skills/stack-dotnet/assets/competition.readme.md` | accepted drift, recorded in the plan |
| `plugins/stack-dotnet/skills/stack-dotnet/assets/web.IntegrationStarterTests.cs` | accepted drift, recorded in the plan |

Both sit inside `plugins/stack-dotnet/`, which the `pack` slice owns exclusively, so slice
disjointness is unaffected. The plan's **Accepted drift** section records the owner accepting
them on 2026-09-18 and asks that they be reported as drift rather than treated as blocking.
They are reported, and they are not blocking. The reasoning holds: the emitted README has to
carry an invariant about the orchestration host that means nothing in a competition repository,
so one shared asset could not honestly serve both kinds; and the generated sample integration
test has every test in it commented out, so R26 (delete the sample content) and R64 (the
integration suite reports a count above zero) contradict each other unless something replaces
it.

The competition kind also declares one preflight check where the plan's prose said it "carries
none". The plan records that too, and the implementer's reading was right: the spec's
failure-modes table is explicit that an absent SDK is a pack-declared preflight for every kind.

**Every other file in the plan's manifest is present and changed.** Eighteen of the twenty; the
nineteenth and twentieth are `checks.yml` above and `.sdlc/CHARTER.md`, which is changed and
carries the v4 amendment the spec deferred to ship time — I1's checkable clause widened from the
engine's instruction files to the executables it ships, and eleven new rows in the table of
silent failures and the checks that catch them. That is two rows more than the spec's list of
nine; the extra two are a row for an engine skill dropping its pointer to the plain-language
rule, and a row for CI drifting from the prerequisites a manifest declares. The second of those
is the row B3 is currently violating.

---

## Build and test output

Real output, run by me on this machine, 2026-09-19. Reading the counts rather than the exit
code, as R65 asks.

```
$ dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo

  Determining projects to restore...
  All projects are up-to-date for restore.
  AiSdlc.Tests -> C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll
Test run for C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.
  Failed EveryDeclaredPrerequisite_IsNamedByCIBeforeTheSuiteRuns [20 ms]
  Error Message:
   Assert.AreEqual failed. Expected:<0>. Actual:<3>. 'expected' expression: '0', 'actual' expression: 'missing.Count'. .github/workflows/checks.yml does not name these prerequisites before it runs the suite, so the kind that needs them cannot be proven on a hosted runner - add a step that provides them, and name them in the step itself; a comment naming them does not count, because a comment survives the step's deletion:
  stack-dotnet / web: aspire-apphost  (probed by: dotnet new list aspire-apphost)
  stack-dotnet / web: aspire-servicedefaults  (probed by: dotnet new list aspire-servicedefaults)
  stack-dotnet / web: aspire-mstest  (probed by: dotnet new list aspire-mstest)
  Stack Trace:
     at AiSdlc.Tests.ScaffoldPrerequisiteTests.EveryDeclaredPrerequisite_IsNamedByCIBeforeTheSuiteRuns() in C:\Repos\ai-sdlc\tests\AiSdlc.Tests\ScaffoldPrerequisiteTests.cs:line 112

Failed!  - Failed:     1, Passed:   127, Skipped:     0, Total:   128, Duration: 5 m 8 s - AiSdlc.Tests.dll (net10.0)
```

**128 executed, 1 failed, 0 skipped.** Nothing skipped its way to green. The build produced no
errors and no warnings. The suite grew from 115 rows at the first verification to 128.

Re-run of the failing class alone, to rule out a stale build or a file changing under the run:

```
$ dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo \
    --filter "FullyQualifiedName~ScaffoldPrerequisiteTests"

  Failed EveryDeclaredPrerequisite_IsNamedByCIBeforeTheSuiteRuns [19 ms]
  ... identical message ...

Failed!  - Failed:     1, Passed:     3, Skipped:     0, Total:     4, Duration: 40 ms
```

This machine has a working Docker daemon, so the integration half of R64 took its run branch
rather than its compile-only branch.

I re-ran the twenty-one end-to-end rows with detailed output, because the evidence the spec's
**End-to-end verification** section asks for — a machine creating a solution from nothing and
watching it compile and pass its own tests — is only visible there. This class alone is green:

```
$ dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo \
    --filter "FullyQualifiedName~ScaffoldEmitsBuildableSolutionTests" \
    --logger "console;verbosity=detailed"

  Passed ScaffoldEmitsBuildableSolution_Web
    web: scaffold exit 0, restore exit 0, build exit 0
  Passed ScaffoldEmitsBuildableSolution_Competition
    competition: scaffold exit 0, restore exit 0, build exit 0
  Passed RestoreSucceedsUnderCentralPackageManagement_Web
    web: 6 emitted project(s), none carrying a version attribute
  Passed RestoreSucceedsUnderCentralPackageManagement_Competition
    competition: 3 emitted project(s), none carrying a version attribute
  Passed EmittedSuitesReportATestCount_Web
    web: container runtime detected (docker)
    web / Probe.IntegrationTests: ran 1 test(s), 0 failed
    web / Probe.UnitTests: ran 2 test(s), 0 failed
  Passed EmittedSuitesReportATestCount_Competition
    competition / Probe.UnitTests: ran 2 test(s), 0 failed
  Passed EmittedNamesMatchACoverageExclusion_Web
    web: exclusions [*.UnitTests, *.IntegrationTests, AppHost, *.AppHost]
    web: held Probe.AppHost, Probe.IntegrationTests, Probe.UnitTests
  Passed EmittedNamesMatchACoverageExclusion_Competition
    competition: held Probe.UnitTests
  Passed EmittedLayoutIsTheFrozenOne_Web
    web: Probe.slnx with 6 project(s), all registered [src/Probe.AppHost/Probe.AppHost.csproj,
      src/Probe.ServiceDefaults/..., src/Probe.Web/..., src/Probe.Api/...,
      tests/Probe.UnitTests/..., tests/Probe.IntegrationTests/...]
    web: supporting files [README.md, .gitignore, .editorconfig, global.json,
      Directory.Build.props, Directory.Packages.props, coverage.runsettings, terraform/versions.tf]
  Passed EmittedLayoutIsTheFrozenOne_Competition
    competition: Probe.slnx with 3 project(s), all registered [src/Probe.Features/...,
      src/Probe.Training/..., tests/Probe.UnitTests/...]
    competition: supporting files [..., notebooks/Probe.ipynb]
  Passed EmittedReadmeCarriesWhatTheOwnerNeeds_Web
    web: README.md carries 7 required item(s)
  Passed EmittedReadmeCarriesWhatTheOwnerNeeds_Competition
    competition: README.md carries 6 required item(s)
  Passed BaseIgnoreEntriesAccompanyTheTerraformOnes_Web
    web: .gitignore carries 4 base entr(ies) and 3 Terraform entr(ies) over 68 line(s)
  Passed BaseIgnoreEntriesAccompanyTheTerraformOnes_Competition
    competition: .gitignore carries 4 base entr(ies) and 3 Terraform entr(ies) over 68 line(s)
  Passed CompetitionKindAppliesTheMachineLearningProfile
    competition: profile 'dotnet-ml', with 4 pin(s) present
  Passed StarterNotebookIsHeadingsAndASolutionReferenceOnly
    competition: notebook has 8 markdown cell(s), 8 heading(s), no code cell,
      no recorded output, no fenced block
  Passed SecondExecutionChangesNothing_Web
    web: re-executed over 45 file(s), none changed
  Passed SecondExecutionChangesNothing_Competition
  Passed RefillsOnlyTheDeletedTestsDirectory_Web
    web: deleted 5 file(s) under tests/ and got them all back, with 40 file(s) elsewhere
      byte-identical
  Passed RefillsOnlyTheDeletedTestsDirectory_Competition
    competition: deleted 3 file(s) under tests/ and got them all back, with 14 file(s)
      elsewhere byte-identical
  Passed NoRowInThisClassCanSkip

Test Run Successful.  Total tests: 21   Passed: 21   Total time: 4.85 Minutes
```

This is the substantive claim of the whole feature and it holds: **both kinds scaffolded from
an empty directory, restored, built, and ran their own tests, in this run** — and unlike the
first verification, the emitted layout, the emitted README, the emitted ignore file and the
emitted notebook are now *asserted* rather than merely observed. The class grew from thirteen
rows to twenty-one, and the eight new ones are B2's fix.

One line is still worth reading twice: `web / Probe.IntegrationTests: ran 1 test(s)`. That one
test is the assembly-name assertion in the pack's integration starter asset. It satisfies R64's
letter and exercises nothing about the composed system — see **S13**.

Two behaviours I drove the shipped executor for by hand, because no test covers them:

```
# R43 - unless_exists declared on one action of every kind, sentinel present
EXIT=0, five `skipped` records, empty tree           -> correct, and untested (S9)

# R39 - no artifact chain, target does not exist yet
EXIT=1, and an empty target directory left behind    -> "writes nothing" is not literal (S10)

# R50 - a file under .sdlc/ containing the pack's token
EXIT=1, "token {{SOLUTION}} was not substituted" against .sdlc/notes.md   -> B4
```

---

## Not checked

- **Mutation testing of the eleven new rows in the charter's table of silent failures.** The
  charter requires each row's check to be seen failing with the fault injected. I did see one
  fail for real — the CI-prerequisite row, which is B3 — and I read every other new check
  closely enough to be satisfied it asserts state that can be wrong rather than passing
  vacuously. I did not inject faults and re-run, so the obligation is outstanding for the other
  ten rows.
- **The compile-only branch of R64.** This machine has a container runtime, so the integration
  suite took the "run it" path. The branch where no runtime exists — the suite compiles, is not
  run, and the row says so in its output — was not exercised and could not be, short of hiding
  Docker from the process.
- **CI on a hosted runner.** I did not push. It would fail at the same row as the local run, and
  then again at the web kind's preflight, for the reasons in B3.
- **A clean machine without the Aspire templates.** The refusal path is covered by fixture
  tests; the real pack's real preflight against a machine that genuinely lacks
  `Aspire.ProjectTemplates` was not exercised, because this machine has them.
- **The human-verified half of S10 and all of S11** — that the setup command calls the executor
  at the right point in its own prose, and that declining the module-boundary question stops the
  command rather than selecting a kind. Both need `/sdlc-core:init` run by hand in an empty
  directory. I read the skill body and the prose says the right things in the right order; I did
  not run it.
- **Whether a manifest could escape the target through a directory junction.** The containment
  rule resolves a full path and compares prefixes, which does not follow a junction. That is
  inside the trust boundary the charter and the spec's F1 concern both locate at the pack — a
  manifest is read by reviewers the way a script is — so I did not pursue it, and I note it
  rather than leaving the silence to read as coverage.

---

# Verification 3 — 2026-09-19, of the state after the second verification's findings were acted on

*Everything above this line is the first two verifications, dated 2026-09-18 and 2026-09-19.
Each describes the change **before** its own findings were acted on, and both stand as written.
What follows is a third, independent verification of a different state again: commit `c894f1d`
plus the uncommitted working tree listed under **Manifest drift** below, read and run on
2026-09-19. The state differs from the second verification's in four files — the CI workflow,
the specification (now at revision 3), the executor, and the executor's test class.*

## Verdict

**SHIP.** The suite is green — **132 tests, 132 passed, 0 failed, 0 skipped** — the build
produces no errors and no warnings, and both of the second verification's blocking findings are
genuinely closed. I checked the closures by injecting faults and watching the named checks fail,
not by reading the code and agreeing with it.

The two that were blocking:

- **R37 — this repository's own CI installs a prerequisite that a pack's manifest declares and a
  hosted runner does not have.** The workflow step exists now, and the three Aspire template
  names sit in the step's own `name:` field rather than in a comment beside it, so deleting the
  step deletes the names with it. The check that holds it passes, and the second verification
  watched that same check fail for real against the previous state of this file.
- **R69 — the token sweep skips the artifact chain, and only the chain.** This is a new
  requirement added at revision 3 of the specification, superseding R50, which swept every file
  under the target and so halted on this repository's own paperwork. The carve-out is
  implemented against the artifact root the executor already resolves for the approval gate, not
  against a hardcoded directory name, and both halves are tested against two different artifact
  roots.

**I attacked the new checks the way the lead asked, on the premise that a check written to
satisfy a review is the one most likely to assert the state its author has just created.** I did
not find one that cannot fail. Three fault injections — run against copies of the shipped
executor, so nothing in the repository was modified — are in **Build and test output** below.
They show each half of the token-sweep pair failing on its own, and the first verification's
report-recording fix failing when its branch is removed.

**One new finding, and it is the shape the lead predicted: a fix that moved the problem rather
than only closing it.** The carve-out that stops the sweep reading the artifact chain is
unconditional — it skips every file under the artifact root, including one a manifest action has
just written there. A pack that copies an asset into the chain without declaring the token gets
exit 0 and an unsubstituted `{{SOLUTION}}` left on disk with nothing said. I reproduced it. It is
a should-fix rather than a blocker — no manifest does this, and it sits inside the trust boundary
that the charter and the specification's own flagged concern F1 (the engine runs command strings
a pack supplies, so the pack is where the trust is placed) both locate at the pack — but the
specification's justification for the carve-out rests on the sentence *"no manifest action writes
into it"*, and nothing enforces that sentence.

Everything else below carries forward from the second verification unchanged, and none of it is a
reason to hold the change: four requirements are implemented, correct, hand-verified across three
rounds, and still without an automated check.

---

## Blocking findings

**None.** Both of the second verification's blockers are closed, and I found no new defect that
makes a claim this change ships false.

---

## Should fix

### S14 — the sweep's carve-out is blind to what a manifest writes into the chain (new)

**What R69 asks for:** when the executor finishes, any file under the target directory *other
than those under the resolved artifact root* that still contains a token the manifest declares
fails the run, naming the file and the token — and the executor does not sweep the artifact root
at all.

**What the implementation does, exactly:** the sweep skips every file whose full path sits under
the resolved artifact root, with no regard for who put it there. The specification's argument for
why that removes nothing the sweep was for is three claims — *"no pack names it, no manifest
action writes into it, and nothing in it is ever token-substituted"*. The first and third are
properties of the packs that exist today. **The second is an assumption the executor does not
check**, and it is the one the carve-out depends on.

**Reproduced against the shipped script**, not predicted. A manifest whose only action copies a
pack asset containing `{{SOLUTION}}` to `.sdlc/note.md`, with no `tokens` field on the action:

```
EXIT=0
content of .sdlc/note.md: assembly = {{SOLUTION}}.Thing
```

Before revision 3 this run halted and named the file. Now it exits clean.

**The concrete failure scenario.** A pack author adds an action emitting a starter document into
the repository's chain — a change template, a charter stub — and forgets `"tokens": ["solution"]`
on it. The executor exits 0, the report records the file as `created`, the setup command tells the
owner the repository is scaffolded, and the emitted document says `{{SOLUTION}}` where it should
say the solution's name. That is exactly the class of defect the sweep exists for, reintroduced in
the one directory the sweep no longer looks at.

**Why it is not blocking.** No shipped manifest writes into the artifact root; the pack's own
skill points at its manifest as the single list of what it writes; and a manifest is read by
reviewers the way a script is — the specification says so and does not pretend the containment
rules bound a manifest nobody reviewed. The exposure is real and narrow.

**The fix shape is small and closes it exactly:** keep skipping files under the artifact root that
this run did not create, and still sweep the ones it did. The executor already keeps the set of
paths it created this execution, for the rule that a rewrite or a delete may only touch what this
run made, so the value is already in hand.

### S15 — the specification and the charter still describe the pre-carve-out sweep (new, documentation)

Two documents still say the sweep reads every file under the target, which stopped being true at
revision 3:

- The specification's test-strategy table, success criterion **S3 — no `{{`-delimited token
  survives** — still cites **R50**, the superseded requirement, and describes *"sweep every file
  under the target"*. R50 is struck through, with its reason, a hundred lines above. A reader
  working from that table would build the rule the revision removed.
- The charter's table of silent failures and the checks that catch them gained the row *"a token
  survives, including from an earlier run"*, described as *"the sweep of every file under the
  target"*. The same overstatement, in the document that is supposed to be the register.

Neither changes any behaviour. Both are worth one edit, because the charter's table is the one
place the register of instruments lives.

### S9 — R43's one sentence over R10 still has no test, though it is true (third round)

**R43 — the "skip this action if that path is already there" field binds every kind of action, not
only the ones that run a command.** Every fixture in the executor's test class that exercises it
declares it on a `run` action; the fixture builder only offers it there. The implementation is
generic and correct — the check sits above the switch on action kind rather than inside it — and
the second verification confirmed all six kinds by hand. What is missing is only the test, and the
cost of adding it is one optional argument on the fixture builders.

### S10 — a refused run still leaves an empty target directory behind (third round)

**R39 — with no charter, the executor writes nothing and exits non-zero.** It creates the target
directory before the approval gate and then refuses. Pointed at a path that does not exist, it
exits 1 having created an empty directory. The code comments acknowledge the ordering, the
directory is empty, and the practical harm is small — but *"writes nothing"* is not literally true
and no test covers it.

### S11 — R47 has no instrument (third round)

**R47 — the manifest declares every file the pack scaffolds, so nothing but the executor writes
one.** This is the requirement the whole gate-hook argument rests on: the scaffold is allowed to
write where the edit-time safety check cannot see it precisely because one process does all the
writing and reports all of it. Nothing checks it. I verified it by hand again: all fourteen files
under the pack's `assets/` directory are referenced by `scaffold.json`, and none is orphaned. A
check comparing the two sets is a handful of lines, and it belongs beside the one that already
walks every pack to confirm its skill cites its manifest.

### S12 — the deletes that strip generator sample content are visible, but still not measured (third round)

**R26 — every sample page, component, endpoint and data type the generators produce is removed,
and at least one executable test is left behind.** The second half holds and is checked by the
emitted suites' reported test counts. The first half is now *reported* — a delete that matches
nothing records a `skipped` row with its glob and a reason, which is the first verification's
finding closed — but nothing in the suite asserts the deletes ever hit anything.

I ran the shipped executor against the real manifest myself, and all five deletes fired today:

```
removed  src/Probe.Api/Controllers/WeatherForecastController.cs
removed  src/Probe.Api/WeatherForecast.cs
removed  src/Probe.Api/Probe.Api.http
removed  tests/Probe.UnitTests/Test1.cs
removed  tests/Probe.IntegrationTests/IntegrationTest1.cs
```

and a search of the emitted tree for those filenames found nothing. So the requirement is met on
disk. What is missing is the instrument: the day an SDK update renames one of those files, the
suite stays green, the run exits 0, and the only thing that says a word is a `skipped` row in a
report a human has to read. Asserting that the emitted tree contains none of the sample filenames
would be one, and this is the third round it has been asked for.

### S13 — the emitted integration suite never starts the composed system (third round)

**R64 — where a container runtime exists the integration suite runs and reports at least one test
with no failures; where there is none it compiles, and the check records that it was not run.**
The pack's integration starter asserts that its own assembly name matches the coverage exclusion
pattern, and deliberately does not start the orchestration host; the asset's comment explains the
choice honestly. That satisfies the requirement as written. The consequence worth the owner
knowing is that the container runtime the requirement turns on makes no difference to what is
exercised — the same non-container test runs either way, so a green integration row is not
evidence that the composed system starts.

### Closed since the second verification, and checked rather than taken

| Second verification's finding | State now |
|---|---|
| **B3** — nothing in CI installed the web kind's prerequisite, so R37 (CI provides what a hosted runner lacks) was unimplemented and the suite was red | **Closed.** `.github/workflows/checks.yml` carries the step, before the line that runs the suite, and names `aspire-apphost`, `aspire-servicedefaults` and `aspire-mstest` in the step's own `name:` — not in the comment above it. The comment explains why the step names them there and names none itself, so deleting the step removes every mention. `EveryDeclaredPrerequisite_IsNamedByCIBeforeTheSuiteRuns` passes, and it strips a comment off a line before matching, so a sentence could not satisfy it. |
| **B4** — the token sweep read the artifact chain, so a repository whose own documents quote the pack's placeholder could never be re-run | **Closed, by a specification revision rather than a local fix, which was the right way round.** R50 is struck through; R69 replaces it and carves out the artifact root only. The executor tests the resolved root rather than a literal `.sdlc`, and both test rows run against `.sdlc` *and* `docs/sdlc`. I mutation-tested both halves — see **Build and test output**. The residual is **S14** above. |

**How I satisfied myself the carve-out is not "stopped sweeping".** The specification's revision-3
note says both halves are required, *"a test proving only the first would pass on an executor that
had stopped sweeping altogether"*. There are three rows, not two: a token inside the artifact root
does not halt the run; the same token in a scaffolded file still does; and a token left by an
earlier run, in a file this run did not write, still does. The first two are the same fixture, the
same manifest and the same filename, differing only in the directory — so neither can drift away
from the other. I then injected the two faults that would break each half and watched them behave
exactly as the pair predicts.

---

## Requirement coverage

Live requirements only. R6 (the superseded report shape), R8 (the superseded token rule), R11 (the
superseded `required` rewrite), R14 (the two-argument invocation), R33 (the superseded
integration-test criterion) and now **R50** (the superseded "sweep every file under the target")
are struck through in the specification and are correctly claimed by nobody. Sixty-three live.

| Req | What it says | Implemented | Tested | Note |
|---|---|---|---|---|
| R1 | every pack ships a scaffold manifest | yes | yes | `EveryPack_ShipsAScaffoldManifest`, with a guard against an empty pack list |
| R2 | the engine names no string a pack owns | yes | yes | both invariant-I1 checks, sharing one term list that now carries the file extension and the token spelling |
| R3 | actions run in order and halt at the first failure | yes | yes | `FailingAction_HaltsBeforeTheActionsAfterIt` checks the later action did not run |
| R4 | a path outside the target is refused, nothing written | yes | yes | one row per action kind, each asserting the outside file is unchanged |
| R5 | an existing destination is skipped, left byte-identical | yes | yes | `ExistingDestination_IsSkippedAndLeftByteIdentical` |
| R7 | a second run changes no file and exits 0 | yes | yes | twice — fixture manifest, and the real pack per kind by tree hash |
| R9 | preflight runs first; on failure nothing is written | yes | yes | `FailingPreflight_WritesNothingAndRelaysTheRemediation` |
| R10 | `unless_exists` skips the action (restated as R43) | yes | partial | see R43 |
| R12 | an unknown or missing kind lists the declared kinds | yes | yes | plus an unknown manifest version refused before any action |
| R13 | no scaffold action while the charter is draft | yes | n/a | the setup command's prose; the substantive half is machine-checked by R38 |
| R15 | stop and name the missing answer if the kind or the boundaries are unsettled | yes | no | the setup command's prose, human-verified by design |
| R16 | a re-run offers charter amendment and gap-filling | yes | no | the setup command's prose; **the gap-filling half is unblocked now that R69 has landed** |
| R17 | derive the identifier from the directory name | yes | yes | four derivation rows; the confirmation half is prose |
| R18 | an underivable candidate is refused, not repaired | yes | yes | `--` and `9lives` rows, asserting nothing was substituted |
| R19 | recover the identifier from an existing solution file | yes | yes | `ExistingSolutionFile_IsRecoveredRatherThanRederived` |
| R20 | relay the report verbatim; never claim success on a non-zero exit | yes | no | the setup command's prose, human-verified by design |
| R21 | the web kind emits its seven artefacts | yes | yes | `EmittedLayoutIsTheFrozenOne_Web` — set equality against an expectation written independently of the manifest, plus registration in the solution file |
| R22 | the competition kind emits its five artefacts | yes | yes | `EmittedLayoutIsTheFrozenOne_Competition` |
| R23 | emitted names match a coverage exclusion | yes | yes | patterns parsed out of the emitted `coverage.runsettings`, not copied into the test |
| R24 | the emitted README carries the build, test and local-run commands, the chain's location and the authority invariant | yes | yes | `EmittedReadmeCarriesWhatTheOwnerNeeds_*`; seven items for the web kind, six for the competition kind |
| R25 | a base ignore file alongside the Terraform entries | yes | yes | `BaseIgnoreEntriesAccompanyTheTerraformOnes_*`, both sets asserted |
| R26 | sample content removed; at least one executable test left | yes | partial | the "at least one test" half holds; the removal half is visible in the report and not measured — **S12** |
| R27 | strip every version attribute from every package reference | yes | yes | asserted over every emitted project file, plus the restore |
| R28 | the pack pins every package a generator emits | yes | yes | the restore is the only instrument, and it runs |
| R29 | rewrite the inline SDK version to the pinned one | yes | yes | the manifest's own `assert_after` fires on the real run; I confirmed the emitted AppHost project by hand |
| R30 | a missing template fails preflight; nothing is installed | yes | yes | `NoManifest_InstallsItsOwnPrerequisite` plus the preflight rows |
| R31 | the emitted solution builds with exit 0 | yes | yes | `ScaffoldEmitsBuildableSolution_Web` / `_Competition` |
| R32 | the unit suite reports a count above zero and no failures | yes | yes | counts read from a test-results file, not off the console |
| R34 | a partial repository is filled, everything else byte-identical | yes | yes | `tests/` deleted and re-executed, both halves compared by hash |
| R35 | the engine still satisfies invariant I1 | yes | yes | both I1 checks |
| R36 | the repository README describes what the setup command produces | yes | yes | `RepositoryReadme_ListsTheLayoutItsPacksEmit` reads the expectation out of each pack's manifest; `RepositoryReadme_DoesNotOfferTheRetiredSetupCommand`. **The README's CI paragraph is now true** — see the closed-findings table |
| R37 | CI installs a prerequisite the manifest declares | yes | yes | **closed** — the step exists and names the templates in the step itself |
| R38 | refuse a charter that is not approved | yes | yes | `DraftCharter_WritesNothingAndExitsNonZero`, asserting an empty tree |
| R39 | refuse when no charter exists | yes | yes | two rows; see **S10** on the empty directory left behind |
| R40 | resolve the artifact root and name it in the report | yes | yes | tested against a non-default root, so it cannot be an assumption |
| R41 | rewrite and delete touch only paths this run created | yes | yes | proven with pre-existing files that match the globs |
| R42 | `assert_after` must hold after a rewrite | yes | yes | `AssertAfterThatDoesNotHold_FailsTheRunNamingTheFile` |
| R43 | `unless_exists` binds every action kind | yes | no | **S9** — correct for all six kinds, hand-verified in the second round; only `run` is exercised by a test |
| R44 | a rewrite or delete glob outside the target is refused | yes | yes | two of the four containment rows |
| R45 | `append` creates, or adds only the missing lines | yes | yes | three rows, including a round-trip regression for non-ASCII assets |
| R46 | `append` is a no-op recorded as skipped when all lines are present | yes | yes | asserted byte-for-byte |
| R47 | the manifest declares every file the pack scaffolds | yes | no | **S11** — verified by hand again, 14 of 14 assets referenced |
| R48 | the setup command writes only the charter | yes | no | the setup command's prose, human-verified by design |
| R49 | the pack's skill cites the manifest and carries no second list | yes | yes | `PackSkillCitesItsManifest` — it would fire on the file table this change removed |
| R51 | a supplied identifier wins over derivation and recovery | yes | yes | `SuppliedIdentifier_WinsOverDerivationAndRecovery` |
| R52 | an invalid supplied identifier is refused | yes | yes | four rows: empty, leading digit, space, dash |
| R53 | more than one solution file is refused; none falls back to derivation | yes | yes | both halves |
| R54 | the executable scan reuses the existing term list | yes | yes | one shared member, no copy |
| R55 | a stack term in a shipped engine executable fails the named check | yes | yes | `EngineExecutablesImposeNoStack`, with a guard over four files so it cannot pass on an empty set |
| R56 | the report records every action with an outcome | yes | yes | including a rewrite or delete that matched nothing, and an unexpected failure inside an action; I mutation-tested the first |
| R57 | a preflight declared inside actions is rejected | yes | yes | `PreflightDeclaredInsideActions_IsRejected`, by kind and by stray field |
| R58 | a rewrite is scoped to created files and records `mutated` | yes | yes | `RewriteAndDelete_TouchOnlyWhatThisRunCreated` |
| R59 | relay every failed, mutated and removed entry, never a count | yes | no | the setup command's prose, human-verified by design |
| R60 | invoke the executor with all four arguments | yes | no | the setup command's prose; the command line is used verbatim by both test classes |
| R61 | the report carries a schema version; a consumer rejects an unknown one | yes | yes | the producer is asserted; the consumer half is tested against the test's own reader, since the real consumer is prose |
| R62 | the pack ships a starter test asserting a real property | yes | yes | via the reported test count; I read both assets — they assert the assembly-name and namespace convention, not a tautology |
| R63 | `run` starts inside the target; a `cwd` outside it is refused | yes | yes | `RunWhoseWorkingDirectoryEscapesTheTarget_IsRefused` |
| R64 | the integration suite runs and reports a count, or compiles and says so | yes | yes | the run branch was exercised here; see **S13** on what it exercises |
| R65 | an absent prerequisite fails rather than skips | yes | yes | `NoRowInThisClassCanSkip`, plus 0 skipped across the whole run |
| R66 | restore succeeds with transitive pinning on | yes | yes | the property is asserted present before the restore is trusted |
| R67 | the competition kind selects the machine-learning profile | yes | yes | declaration *and* pins, with the web kind asserted not to declare it |
| R68 | the starter notebook is headings only, no executable logic | yes | yes | no code cell, no recorded output, no execution count, no fenced block, and a minimum heading count so an empty file cannot pass |
| R69 | the sweep skips the artifact root, and only it | yes | yes | **new** — two rows, each run against two different artifact roots; I mutation-tested both halves. See **S14** for what the carve-out does not cover |

**52 of 63 live requirements have an automated check that can fail** — unchanged from the second
verification, because the two requirements that moved (R37 gaining its implementation, and R50
being replaced by R69) were already counted as having a check. Seven are the setup command's own
prose and are human-verified by design: R15 (stop and name the missing answer), R16 (a re-run
offers amendment or gap-filling), R20 (relay the report verbatim), R48 (write only the charter),
R59 (show every failed, mutated and removed entry, never a count), R60 (invoke the executor with
all four arguments), and the ordering half of R13 (nothing generated while the charter is a
draft). Four are implemented and unchecked: R10 and R43 (`unless_exists` on kinds other than
`run`), R26's removal half, and R47. **None is unimplemented.**

---

## Manifest drift

`git diff --name-only` plus untracked files, against the plan's frontmatter slice manifest.

**Files claimed by a slice that nothing touched — none.** All twenty are present and changed.
This is the change from the second verification: `.github/workflows/checks.yml` was byte-identical
to its committed version then, which was blocking finding B3. It is modified now, and the change
is the install step.

```
 M .github/workflows/checks.yml                    release
 M .sdlc/CHARTER.md                                release
 M README.md                                       release
 M plugins/sdlc-core/.claude-plugin/plugin.json    release
 M plugins/sdlc-core/skills/init/SKILL.md          init
 M plugins/stack-dotnet/.claude-plugin/plugin.json release
 M plugins/stack-dotnet/skills/stack-dotnet/SKILL.md                        pack
 M plugins/stack-dotnet/skills/stack-dotnet/assets/Directory.Packages.props pack
 M tests/AiSdlc.Tests/PluginStructureTests.cs      structure-checks
 M tests/AiSdlc.Tests/TempRepo.cs                  executor
 ?? plugins/sdlc-core/scripts/Invoke-ScaffoldManifest.ps1                   executor
 ?? plugins/stack-dotnet/skills/stack-dotnet/scaffold.json                  pack
 ?? plugins/stack-dotnet/skills/stack-dotnet/assets/competition.StarterTests.cs   pack
 ?? plugins/stack-dotnet/skills/stack-dotnet/assets/competition.starter.ipynb     pack
 ?? plugins/stack-dotnet/skills/stack-dotnet/assets/dotnet.gitignore              pack
 ?? plugins/stack-dotnet/skills/stack-dotnet/assets/solution.readme.md            pack
 ?? plugins/stack-dotnet/skills/stack-dotnet/assets/web.StarterTests.cs           pack
 ?? tests/AiSdlc.Tests/ScaffoldEmitsBuildableSolutionTests.cs               build-tests
 ?? tests/AiSdlc.Tests/ScaffoldManifestTests.cs                             executor
 ?? tests/AiSdlc.Tests/ScaffoldPrerequisiteTests.cs                         release
```

**Files changed that no slice claims — two, both expected:**

| File | Status |
|---|---|
| `plugins/stack-dotnet/skills/stack-dotnet/assets/competition.readme.md` | accepted drift, recorded in the plan |
| `plugins/stack-dotnet/skills/stack-dotnet/assets/web.IntegrationStarterTests.cs` | accepted drift, recorded in the plan |

Both sit inside `plugins/stack-dotnet/`, which the `pack` slice owns exclusively, so slice
disjointness is unaffected. The plan's **Accepted drift** section records the owner accepting them
on 2026-09-18 and asks that they be reported as drift rather than treated as a blocking finding.
They are reported, and they are not blocking. I checked the reasoning a third time and it holds:
the emitted README has to carry an invariant about the orchestration host that means nothing in a
competition repository, so one shared asset could not honestly serve both kinds; and the generated
sample integration test contains no live test, so R26 (delete the sample content) and R64 (the
integration suite reports a count above zero) contradict each other unless something replaces it.

The competition kind also declares one preflight check where the plan's prose said it "carries
none". The plan records that too, and the implementer's reading was right: the specification's
failure-modes table is explicit that an absent SDK is a pack-declared preflight for every kind.

`.sdlc/init-scaffolds-solution/spec.md` is at revision 3 and committed, so it does not appear in
the working tree. `.sdlc/CHARTER.md` carries the version-4 amendment the specification deferred to
ship time.

---

## Build and test output

Real output, run by me on this machine, 2026-09-19. Reading the counts rather than the exit code,
as R65 asks.

```
$ dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo

  Determining projects to restore...
  All projects are up-to-date for restore.
  AiSdlc.Tests -> C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll
Test run for C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   132, Skipped:     0, Total:   132, Duration: 4 m 45 s - AiSdlc.Tests.dll (net10.0)
```

**132 executed, 0 failed, 0 skipped.** Nothing skipped its way to green, and the build produced no
errors and no warnings. The suite grew from 128 rows at the second verification; the four new ones
are the token-sweep pair, each run against two artifact roots.

This machine has a working container runtime (`docker info` exits 0), so the run branch of R64 was
taken rather than the compile-only branch.

### The end-to-end evidence the specification asks for

```
$ dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo \
    --filter "FullyQualifiedName~ScaffoldEmitsBuildableSolutionTests" \
    --logger "console;verbosity=detailed"

  Passed ScaffoldEmitsBuildableSolution_Web
  Passed ScaffoldEmitsBuildableSolution_Competition
  Passed RestoreSucceedsUnderCentralPackageManagement_Web
    web: 6 emitted project(s), none carrying a version attribute
  Passed RestoreSucceedsUnderCentralPackageManagement_Competition
    competition: 3 emitted project(s), none carrying a version attribute
  Passed EmittedSuitesReportATestCount_Web
    web: container runtime detected (docker)
    web / Probe.IntegrationTests: ran 1 test(s), 0 failed
    web / Probe.UnitTests: ran 2 test(s), 0 failed
  Passed EmittedSuitesReportATestCount_Competition
    competition / Probe.UnitTests: ran 2 test(s), 0 failed
  Passed EmittedNamesMatchACoverageExclusion_Web
    web: exclusions [*.UnitTests, *.IntegrationTests, AppHost, *.AppHost]
    web: held Probe.AppHost, Probe.IntegrationTests, Probe.UnitTests
  Passed EmittedNamesMatchACoverageExclusion_Competition
    competition: held Probe.UnitTests
  Passed EmittedLayoutIsTheFrozenOne_Web
    web: Probe.slnx with 6 project(s), all registered [src/Probe.AppHost/..., src/Probe.ServiceDefaults/...,
      src/Probe.Web/..., src/Probe.Api/..., tests/Probe.UnitTests/..., tests/Probe.IntegrationTests/...]
  Passed EmittedLayoutIsTheFrozenOne_Competition
    competition: Probe.slnx with 3 project(s), all registered [src/Probe.Features/...,
      src/Probe.Training/..., tests/Probe.UnitTests/...]
  Passed EmittedReadmeCarriesWhatTheOwnerNeeds_Web
    web: README.md carries 7 required item(s)
  Passed EmittedReadmeCarriesWhatTheOwnerNeeds_Competition
    competition: README.md carries 6 required item(s)
  Passed BaseIgnoreEntriesAccompanyTheTerraformOnes_Web
    web: .gitignore carries 4 base entr(ies) and 3 Terraform entr(ies) over 68 line(s)
  Passed BaseIgnoreEntriesAccompanyTheTerraformOnes_Competition
  Passed CompetitionKindAppliesTheMachineLearningProfile
    competition: profile 'dotnet-ml', with 4 pin(s) present
  Passed StarterNotebookIsHeadingsAndASolutionReferenceOnly
    competition: notebook has 8 markdown cell(s), 8 heading(s), no code cell, no recorded output, no fenced block
  Passed SecondExecutionChangesNothing_Web
    web: re-executed over 45 file(s), none changed
  Passed SecondExecutionChangesNothing_Competition
    competition: re-executed over 17 file(s), none changed
  Passed RefillsOnlyTheDeletedTestsDirectory_Web
    web: deleted 5 file(s) under tests/ and got them all back, with 40 file(s) elsewhere byte-identical
  Passed RefillsOnlyTheDeletedTestsDirectory_Competition
    competition: deleted 3 file(s) under tests/ and got them all back, with 14 file(s) elsewhere byte-identical
  Passed NoRowInThisClassCanSkip

Test Run Successful.  Total tests: 21
```

**Both kinds were scaffolded from an empty directory, restored, built, and ran their own tests, in
this run.** That is the claim the whole feature exists to make, and it holds.

### Fault injection: three mutations, each against a copy of the shipped executor

The lead asked me to pick the new checks apart by asking what code change would make each one
fail. For the three that matter most — the pair holding R69, and the recording fix that closed the
first verification's blocker — I did not stop at asking. **Nothing in the repository was modified:
each mutation was applied to a copy of `Invoke-ScaffoldManifest.ps1` in a temporary directory and
driven directly.**

Mutation 1 removes the line that skips the artifact root. Mutation 2 makes the skip return true
for every path, which is what "stopped sweeping" looks like.

```
                         token inside the root   token outside the root
                         (test wants exit 0)     (test wants non-zero)
SHIPPED                  exit=0  PASS            exit=1  PASS
MUT1 carve-out removed   exit=1  FAIL            exit=1  PASS
MUT2 sweep disabled      exit=0  PASS            exit=0  FAIL
```

Each mutation is caught by exactly one half of the pair and passed by the other, which is the
property the specification's revision-3 note demands and the reason two rows were written instead
of one.

Mutation 3 disables the branch that records a rewrite or a delete whose glob matched nothing — the
defect the first verification raised as B1:

```
SHIPPED   exit=0  declared=3 recorded=3  rows: mkdir/created, rewrite/skipped, delete/skipped
MUTATED   exit=0  declared=3 recorded=1  rows: mkdir/created
```

`RewriteAndDeleteThatMatchNothing_AreStillRecordedWithAnOutcome` asserts that three declared
actions produce three recorded rows, so it fails on the mutated executor. It counts rows rather
than looking for outcomes, which is what makes it able to notice an absent one.

### Behaviours I drove the shipped executor for by hand

```
# R26 - the real web manifest into an empty directory
EXIT=0, 21 created / 12 mutated / 5 removed; all five sample-content deletes fired;
        no WeatherForecast, Test1, IntegrationTest1 or .http file left in the tree

# R29 - the emitted AppHost project, read directly
<Project Sdk="Aspire.AppHost.Sdk/13.5.3">   -> the pinned version binds, and no
                                               PackageReference carries a version attribute

# S14 - a manifest action copying a token into the artifact root
EXIT=0, and .sdlc/note.md still says "assembly = {{SOLUTION}}.Thing"

# the artifact-root-is-'.' edge case, both halves - see the section below
```

---

## The edge case the lead asked for a judgement on, not an agreement

**The question.** A charter declaring its artifact root as `.` would resolve that root to the
target directory itself, and the token sweep would then skip every file. The implementer left it
unguarded, judging that inventing behaviour the specification does not describe was worse than the
honest reading, and noted that the edit-time gate hook has the same exposure.

**I ran it rather than reasoned about it, and the picture is narrower than either of us assumed.**

*Case A — a charter at `.sdlc/CHARTER.md` declaring `artifact_root: .`, which is the only way a
repository can reach this state, because the executor discovers a charter at `.sdlc/CHARTER.md` or
one directory or two directories down, and never at the repository root:*

```
no charter at ...\probe-repo\CHARTER.md - nothing has approved this scaffold.
EXIT=1
```

**The executor refuses to run at all.** Having resolved the root to the target, it looks for
`CHARTER.md` there, does not find one, and halts writing nothing. There is no silent failure — the
run never starts.

*Case B — the same repository, plus a second approved `CHARTER.md` at the target root:*

```
EXIT=0, report outcome "completed", and a pre-existing file containing {{SOLUTION}} untouched
```

Here the sweep is indeed vacuous. So the edge case is real, and it needs **two charters in one
repository, disagreeing about where the chain lives** — not one charter with an unusual field.

**My judgement: I agree with the implementer, for a stronger reason than they gave.** Guarding it
would mean the executor second-guessing a charter that says something no charter says, in a
repository that is already internally inconsistent, and the honest reading costs nothing because
the ordinary version of the mistake refuses to run rather than running blind.

**Where I differ is the comparison to the gate hook.** Its exposure is not the same. The hook
compares the *leaf* of the resolved root against the segments of the path being edited, so with a
root of `.` it skips only paths containing the repository's own directory name as a segment — it
keeps gating everything else. The executor, by contrast, skips everything under the root, which is
the whole tree. The two degrade differently, and "the hook has the same exposure" should not be
what carries the decision.

**It belongs in the record as a limitation**, in one line, with the two-charter precondition
stated — because the precondition is what makes it acceptable to leave alone, and a limitation
recorded without it reads far worse than it is. The charter's known-and-unchecked list is where it
goes, next to the parts of the interview that only a human can verify.

---

## Not checked

- **Mutation testing of the remaining rows in the charter's table of silent failures.** The
  charter requires every row's check to be seen failing with the fault injected, and the version-4
  amendment adds eleven rows. I injected three faults and watched three checks fail, and the second
  verification watched a fourth fail for real — the CI-prerequisite row. I read every other new
  check closely enough to be satisfied it asserts state that can be wrong rather than passing
  vacuously, but I did not inject a fault for each. **The obligation is outstanding for roughly
  seven rows, and it is the owner's to close or to waive** — it is a claim about work the
  implementers were asked to do, not a defect in the change, which is why it does not hold the
  ship.
- **The compile-only branch of R64.** This machine has a container runtime, so the integration
  suite took the "run it" path. The branch where none exists — the suite compiles, is not run, and
  the row says so in its output — was not exercised, and could not be short of hiding the runtime
  from the process.
- **CI on a hosted runner.** I did not push. The workflow now carries the template install step and
  the check holds it against the manifest, but the job has not actually run, so the one thing still
  unproven is whether `dotnet new install Aspire.ProjectTemplates::13.5.3` succeeds on
  `windows-latest` and whether the web kind's preflight then passes there. **That is the first
  thing to watch on the first push**, because it is the scenario R37 exists for and the only one
  this machine cannot stand in for.
- **A clean machine without the Aspire templates.** The refusal path is covered by fixture tests;
  the real pack's real preflight against a machine that genuinely lacks them was not exercised,
  because this machine has them.
- **The human-verified half of success criterion S10 and all of S11** — that the setup command
  calls the executor at the right point in its own prose, and that declining the module-boundary
  question stops the command rather than selecting a kind for the owner. Both need
  `/sdlc-core:init` run by hand in an empty directory. I read the skill body: it settles the kind
  and the boundaries before the charter, writes the charter and nothing else, and runs the executor
  after the approval, with all four arguments and the relay rules spelled out. I did not run it.
- **Whether a manifest could escape the target through a directory junction.** The containment rule
  resolves a full path and compares prefixes, which does not follow a junction. That sits inside
  the trust boundary the charter and flagged concern F1 both place at the pack, so I did not pursue
  it — noted rather than left silent, because silence here reads as coverage.
- **Whether a repository that vendors a stack pack can be gap-filled.** The carve-out fixes the
  artifact chain; a repository holding a pack's own assets outside its chain — this one does, in
  eleven files — would still halt on them. No such repository is a scaffold target, so this is an
  observation rather than a finding.

---

# Verification 4 — 2026-09-19, of the state after revision 4 of the spec

*Everything above this line is the first three verifications. Each describes the change **before**
its own findings were acted on, and all three stand as written. This is a fourth, independent
verification of a different state again: commit `9505cf2` plus the uncommitted working tree
listed under **Manifest drift** below, read and run on 2026-09-19.*

**Why there is a fourth round.** The third round returned SHIP with one should-fix: the rule that
kept the token sweep away from the artifact chain was unconditional, so a pack that wrote into the
chain without substituting went unnoticed. That finding was acted on. The token sweep changed and
the spec was revised from R69 — *skip the artifact root entirely* — to R70 — *sweep the artifact
root only where this run created the file*. So the third round's SHIP describes code that is no
longer on disk. I reached my own conclusions; the earlier verdicts are evidence about earlier
states, not a baseline.

## Verdict

**SHIP.** The suite is green — **134 tests, 134 passed, 0 failed, 0 skipped** — and the build
produces no errors and no warnings. The change the fourth round exists to judge, R70 — *after the
run, a token the pack declares that still sits in any file under the target fails the run, except
that a file inside the artifact chain is only checked when this run created it* — is implemented
as written and is held by three test rows that I mutation-tested rather than read. Each row fails
on exactly one of the two implementations R70 replaces, so the set genuinely distinguishes R70
from both of its predecessors rather than only from one.

**One new should-fix, and it is the same shape as the last two, one action kind over.** R70 draws
its boundary at *created*. The executor can also **write into a file it did not create**: the
`append` action adds lines to a file that is already there. A pack that appends an asset carrying
the placeholder into a chain file that already exists — `.sdlc/CHARTER.md`, say, which `init`
always writes first — leaves the unsubstituted placeholder on disk and exits 0. I reproduced it
against the shipped script. It is strictly narrower than the hole R70 closed and narrower again
than the one R50 closed, so this is a residual rather than a regression, and no shipped manifest
reaches it. But the spec's own justification for R70 says *"the executor is answerable for what it
wrote"*, and `append` writes without creating — so the rationale and the normative sentence
disagree, and the code correctly followed the normative one.

**On the edge case the lead asked for a judgement on rather than agreement:** R70 does not change
how reachable it is, and it makes the consequence **better**, not worse. I ran both halves. See
**The artifact-root-is `.` edge case** below.

Everything else carries forward from the third round unchanged: four requirements are implemented,
correct, hand-verified across four rounds, and still without an automated check. I formed my own
view on each and none of them holds the change.

---

## Blocking findings

**None.** The third round's should-fix is genuinely closed — I watched the previous implementation
fail the new test — and I found no defect that makes a claim this change ships false.

---

## Should fix

### S16 — the sweep's boundary is *created*, but the executor can also write to a file it did not create (new)

**What R70 asks for:** when the executor finishes, any file under the target directory still
containing a placeholder the pack declares fails the run and is named — *except* that a file under
the resolved artifact chain is only checked when this execution created it.

**What the implementation does:** exactly that. `Test-Sweepable` in
`plugins/sdlc-core/scripts/Invoke-ScaffoldManifest.ps1:332` consults `$script:Created`, which is
genuinely the same set that bounds which files a `rewrite` or a `delete` may touch — not a second
set built to look like it. I checked that specifically, because a second set that was meant to
agree with the first is a second thing that can disagree. It is one set, keyed the same way
through `Get-RelativePath` in both places.

**The gap is between *created* and *written*.** Of the action kinds that put bytes in a file:

| Action | Creates the file | Writes to one already there |
|---|---|---|
| `copy` | registered as created, so it is swept | never — it skips an existing destination |
| `append` | registered as created, so it is swept | **writes, and is not registered, so it is not swept** |
| `run` | registered by a whole-tree rescan after the command | a generator overwriting an existing file is not registered either |

**Reproduced against the shipped script**, not predicted. One manifest, one asset containing
`{{SOLUTION}}`, one `append` action pointed at `.sdlc/notes.md`, with `tokens` not declared. The
only difference between the two runs is whether the destination was already there:

```
destination absent  -> exit=1  token {{SOLUTION}} survives in: .sdlc/notes.md
destination present -> exit=0  scaffold completed for kind 'demo' in ...\t
```

And the control, to show the chain boundary is what causes it — the identical `append`, the
identical asset, pointed at `docs/notes.md` instead:

```
EXIT=1
token {{SOLUTION}} survives in: docs/notes.md
```

**The concrete failure scenario.** A pack author adds an action appending a pack-supplied section
to the repository's charter or to a chain document — a stack-specific invariant block, say — and
forgets `"tokens": ["solution"]`. On the very first run the destination already exists, because
`init` writes the charter before it calls the executor. So the append path is taken every time,
the placeholder reaches disk, the run exits 0, the report records the action as `mutated`, and the
setup command tells the owner the repository is scaffolded.

**Why it is not blocking.** Nothing in `stack-dotnet`'s `scaffold.json` writes anywhere under
`.sdlc/` at all — I read all 58 actions across both kinds. The exposure needs a pack that does,
and a manifest sits inside the trust boundary the charter and the spec's own flagged concern
F1 — *the engine runs command strings a pack supplies, so the trust is placed in the pack* — both
locate at the pack. The blast radius is a placeholder in a document rather than anything
corrupted. And this is strictly less exposure than either predecessor left: R50 — *sweep every
file under the target* — caught it but halted on the engine's own paperwork; R69 — *never sweep
the chain* — missed this **and** every `copy` into the chain.

**Worth saying plainly, because it is the third time.** R50 drew the line by location and caught
the wrong thing. R69 drew it by location again and stopped catching a pack's mistake. R70 correctly
identified that the boundary is authorship — and then spelled authorship as *created*, which is
narrower than *wrote*, so one action kind walks around it. The pattern is not carelessness; it is
that each revision fixed the case in front of it.

**The fix is a spec question before it is a code one.** R70's normative sentence says *created* and
the code implements *created* faithfully; R70's rationale says *"answerable for what it wrote"*.
Those are different sets and the difference is exactly this hole. If the intended rule is *wrote*,
the cheapest honest implementation is to register the destination of a successful `append` to an
existing file in a set the sweep consults. The executor's own comment argues against a second set,
on the grounds that *"a second set that was meant to agree with it would be a second thing that can
disagree"* — that argument is sound for `rewrite` and `delete`, whose bound must stay at R41
(*a rewrite or a delete touches only paths this run created*), and it is what produced this hole
for the sweep, which wants a different and wider question answered.

### S9 — the "skip this action if that path is already there" field binds every action kind, and only one kind is tested (fourth round)

**R43 — `unless_exists` skips the action for every action kind, not only for the ones that run a
command.** Unchanged from the second and third rounds. The implementation is generic and correct:
the check sits above the `switch` on action kind, at `Invoke-ScaffoldManifest.ps1:813`, so it
cannot be kind-specific. Every fixture in the executor's test class that exercises it declares it
on a `run` action, because the fixture builder only offers the argument there
(`ScaffoldManifestTests.cs:210`). The shipped pack manifest also declares it only on `run` actions,
so nothing anywhere exercises the other five. The cost of closing it is one optional argument on
four fixture builders. **My view: not blocking** — the code shape makes a kind-specific regression
implausible — but this is the third round it has been carried, and "implausible" is the word that
precedes every silent failure this repository has a check for.

### S10 — a refused run still leaves an empty target directory behind (fourth round)

**R39 — with no charter at the resolved root, the executor writes nothing and exits non-zero.** The
target directory is created at `Invoke-ScaffoldManifest.ps1:592`, before the approval gate at line
600, so pointing the executor at a path that does not exist yet leaves an empty directory behind
when it refuses. The comment there acknowledges the ordering and argues the target is the boundary
rather than something inside it. The directory is empty and the harm is nil, but *"writes nothing"*
is not literally true and nothing tests it.

### S11 — the requirement the whole gate-hook argument rests on has no instrument (fourth round)

**R47 — the manifest declares every file the pack scaffolds, so nothing but the executor writes
one.** This is the load-bearing one: the scaffold is allowed to write where the edit-time gate hook
cannot see it *precisely because* one process does all the writing and reports all of it. Nothing
checks it. I verified it by hand for a fourth time — every asset under the pack's `assets/`
directory is referenced by `scaffold.json`, and no action names an asset that is not there. A check
comparing the two sets in both directions is a handful of lines and belongs beside
`PackSkillCitesItsManifest`, which already walks every pack. **This is the one of the four I would
most want closed**, because it is the only one that underwrites a deliberate widening of the
ungated write surface.

### S12 — the deletes that strip generator sample content are reported, but still not measured (fourth round)

**R26 — every sample page, component, endpoint and data type a generator produces is removed, and
at least one executable test is left behind.** The second half holds and is checked, by the emitted
suites' reported test counts. The first half is *reported* — a `delete` that matches nothing records
a `skipped` row with its glob and a reason — but nothing in the suite asserts the deletes ever hit
anything. The day an SDK update renames `WeatherForecast.cs`, the suite stays green and the only
thing that says a word is a row in a report a human has to read. Asserting that the emitted tree
contains none of the sample filenames would be one line per filename.

### S13 — the emitted integration suite never starts the composed system (fourth round)

**R64 — where a container runtime exists the integration suite runs and reports at least one test
with no failures; where there is none it compiles, and the check records that it was not run.** The
pack's integration starter asserts that its own assembly name matches the coverage exclusion
pattern and deliberately does not start the orchestration host; the asset's comment says so
honestly. That satisfies the requirement as written. The consequence worth the owner knowing is
that the container runtime the requirement turns on makes no difference to what is actually
exercised, so a green integration row is not evidence that the composed system starts.

### Closed since the third verification, and checked rather than taken

| Third round's finding | State now |
|---|---|
| **S14** — the rule keeping the sweep out of the artifact chain was unconditional, so a pack that copied a placeholder-bearing asset into the chain got exit 0 and silence | **Closed.** R69 is struck through; R70 replaces it, and the executor sweeps a chain file when this run created it. I reproduced the old behaviour on a mutated copy and watched the new test row catch it — see the mutation table below. The residual is **S16** above: `append` to a file that already exists still writes without creating. |
| **S15** — the spec's success-criterion table and the charter's register of silent failures both still described the pre-carve-out sweep | **Closed, in both documents.** The spec's criterion S3 — *no `{{`-delimited placeholder survives* — now cites R70 and reads *"sweep every file under the target… under the artifact root, only files this run created"*. The charter's row now reads *"the sweep of every file under the target, and of chain files this run created"*. Neither overstates any more. |

---

## The question the lead asked: does R70 have a third failure mode?

Three sub-questions, answered separately because they have different answers.

**1. Is there a failure mode neither earlier version had?** **No — not in what it misses.** R70's
coverage is a strict superset of R69's (*never sweep the chain*) and a strict subset of R50's
(*sweep everything*). Anything R70 misses, R69 missed too. What R70 adds is a way to **halt** where
R69 would not: a chain file this run created that legitimately contains the placeholder — a pack
shipping a document *template* into the chain — now fails the run with no way to opt out. That is
new, it is the same rule that already applies everywhere outside the chain, and it fails in the
safe direction. I record it as a consequence rather than a defect.

**2. Is "this execution created it" tracked correctly for every action kind that can create a
file?** **For creation, yes. For writing, no** — that is S16. Kind by kind: `copy` registers at
line 921; `append` registers at line 950 on the create branch and not on the append branch;
`run` registers by diffing the whole tree against a pre-run snapshot at lines 873–875, and — a
detail worth crediting — it does that **before** checking the command's exit code, so files a
*failing* generator left behind are still swept; `mkdir` creates only directories; `rewrite` and
`delete` create nothing, and `delete` correctly un-registers what it removes.

**3. Is the set the sweep consults genuinely the one that bounds `rewrite` and `delete`, or only
one that looks like it?** **Genuinely the same.** `Test-Sweepable` (line 364) and `Get-GlobMatch`
(line 551) both read `$script:Created`, and both key it through `Get-RelativePath`, so there is one
normalisation and one set. There is no parallel structure to drift.

---

## The artifact-root-is `.` edge case — my judgement, having run it

**The question.** A charter declaring its artifact root as `.` resolves that root to the target
itself, and under R70 the sweep would then cover only files this run created — repo-wide. The third
round established it cannot be reached by the ordinary mistake. The lead asked whether R70 changes
that, since the consequence is now different.

**Reachability is unchanged, and the third round was right that it needs two charters.** I
re-derived it rather than taking it. The executor looks for a charter at the default chain
directory, or one or two directories down, and **never at the repository root**. So a charter at
`.sdlc/CHARTER.md` saying `artifact_root: .` resolves the root to the target, then looks for
`CHARTER.md` *there*, does not find one, and refuses:

```
Case A (one charter, declaring artifact_root: .)
  exit=1  no charter at ...\t\CHARTER.md - nothing has approved this scaffold.
```

It takes a second, approved `CHARTER.md` at the repository root — two charters in one repository
disagreeing about where the chain lives — before the run starts at all.

**The consequence is strictly better under R70 than it was under R69, which is the material
change.** Same two-charter repository, a pre-existing file holding the placeholder from an earlier
run, and a manifest action that copies a placeholder-bearing asset without substituting:

```
                      the file this run created   the file left by an earlier run
R69 (the old code)    exit=0  missed              exit=0  missed
R70 (shipped)         exit=1  named               exit=0  missed
```

Under R69 the sweep was **entirely vacuous** in this repository. Under R70 it degrades to *"check
what this run wrote"* — which is the rule the superseded R8 had, described in the spec as useless
for catching a placeholder left by an earlier run, and which is nonetheless not nothing: it still
catches a manifest that forgot to declare its placeholder, which is the mistake anyone would
actually make.

**My judgement: leave it unguarded, and for a different reason than the implementer gave.** Not
because the honest reading of a charter is preferable in the abstract, but because R70 turned this
from a silent total failure into a partial one, the precondition is a repository that is already
internally inconsistent in a way nothing else in this framework tolerates, and the version of the
mistake anyone would actually make refuses to run rather than running blind. Guarding it would mean
the executor second-guessing a charter that says something no charter says. **It belongs in the
charter's known-and-unchecked list in one line, with the two-charter precondition stated** — the
precondition is what makes it acceptable to leave alone, and recorded without it the limitation
reads far worse than it is.

---

## Requirement coverage

Live requirements only. Seven are struck through in the spec and are correctly claimed by nobody:
R6 (the report shape that had no `mutated` or `removed`), R8 (the placeholder rule bound to files
this run wrote), R11 (the `required` rewrite that contradicted re-runnability), R14 (the
two-argument invocation), R33 (the integration criterion a zero-test suite could satisfy), R50 (the
sweep of every file under the target), and now **R69** (the unconditional chain carve-out).
**Sixty-three live.**

| Req | What it says | Implemented | Tested | Note |
|---|---|---|---|---|
| R1 | every pack ships a scaffold manifest | yes | yes | `EveryPack_ShipsAScaffoldManifest`, guarded against an empty pack list |
| R2 | the engine names no string a pack owns | yes | yes | both invariant-I1 checks, one shared term list carrying the file extension and the placeholder spelling |
| R3 | actions run in order and halt at the first failure | yes | yes | `FailingAction_HaltsBeforeTheActionsAfterIt` asserts the later action did not run |
| R4 | a path outside the target is refused, nothing written | yes | yes | one row per action kind, each asserting the outside file is unchanged |
| R5 | an existing destination is skipped, left byte-identical | yes | yes | `ExistingDestination_IsSkippedAndLeftByteIdentical` |
| R7 | a second run changes no file and exits 0 | yes | yes | twice — fixture manifest, and the real pack per kind by tree hash |
| R9 | preflight runs first; on failure nothing is written | yes | yes | `FailingPreflight_WritesNothingAndRelaysTheRemediation` |
| R10 | `unless_exists` skips the action (restated as R43) | yes | partial | see R43 |
| R12 | an unknown or missing kind lists the declared kinds | yes | yes | plus an unknown manifest version refused before any action |
| R13 | no scaffold action while the charter is draft | yes | n/a | the setup command's prose; the substantive half is machine-checked by R38 |
| R15 | stop and name the missing answer if the kind or the boundaries are unsettled | yes | no | the setup command's prose, human-verified by design |
| R16 | a re-run offers charter amendment and gap-filling | yes | no | the setup command's prose; the gap-filling half stays unblocked under R70 — I confirmed a chain document quoting the placeholder does not halt the run |
| R17 | derive the identifier from the directory name | yes | yes | four derivation rows; the confirmation half is prose |
| R18 | an underivable candidate is refused, not repaired | yes | yes | `--` and `9lives`, asserting nothing was substituted |
| R19 | recover the identifier from an existing solution file | yes | yes | `ExistingSolutionFile_IsRecoveredRatherThanRederived` |
| R20 | relay the report verbatim; never claim success on a non-zero exit | yes | no | the setup command's prose, human-verified by design |
| R21 | the web kind emits its seven artefacts | yes | yes | `EmittedLayoutIsTheFrozenOne_Web` — set equality against an expectation written independently of the manifest |
| R22 | the competition kind emits its five artefacts | yes | yes | `EmittedLayoutIsTheFrozenOne_Competition` |
| R23 | emitted names match a coverage exclusion | yes | yes | patterns parsed out of the emitted `coverage.runsettings`, not copied into the test |
| R24 | the emitted README carries the build, test and local-run commands, the chain's location and the authority invariant | yes | yes | `EmittedReadmeCarriesWhatTheOwnerNeeds_*` — seven items for web, six for competition |
| R25 | a base ignore file alongside the Terraform entries | yes | yes | `BaseIgnoreEntriesAccompanyTheTerraformOnes_*`, both sets asserted |
| R26 | sample content removed; at least one executable test left | yes | partial | the "at least one test" half holds; the removal half is reported and not measured — **S12** |
| R27 | strip every version attribute from every package reference | yes | yes | asserted over every emitted project file, plus the restore |
| R28 | the pack pins every package a generator emits | yes | yes | the restore is the only instrument, and it runs |
| R29 | rewrite the inline SDK version to the pinned one | yes | yes | the manifest's own `assert_after` fires on the real run |
| R30 | a missing template fails preflight; nothing is installed | yes | yes | `NoManifest_InstallsItsOwnPrerequisite` plus the preflight rows |
| R31 | the emitted solution builds with exit 0 | yes | yes | `ScaffoldEmitsBuildableSolution_Web` / `_Competition` |
| R32 | the unit suite reports a count above zero and no failures | yes | yes | counts read from a test-results file, not off the console |
| R34 | a partial repository is filled, everything else byte-identical | yes | yes | `tests/` deleted and re-executed, both halves compared by hash |
| R35 | the engine still satisfies invariant I1 | yes | yes | both I1 checks |
| R36 | the repository README describes what the setup command produces | yes | yes | `RepositoryReadme_ListsTheLayoutItsPacksEmit` reads the expectation out of each pack's manifest; `RepositoryReadme_DoesNotOfferTheRetiredSetupCommand` |
| R37 | CI installs a prerequisite the manifest declares | yes | yes | the step exists, names the templates in its own `name:`, and the check strips comments before matching |
| R38 | refuse a charter that is not approved | yes | yes | `DraftCharter_WritesNothingAndExitsNonZero`, asserting an empty tree |
| R39 | refuse when no charter exists | yes | yes | two rows; see **S10** on the empty directory left behind |
| R40 | resolve the artifact root and name it in the report | yes | yes | tested against a non-default root, so it cannot be an assumption |
| R41 | rewrite and delete touch only paths this run created | yes | yes | proven with pre-existing files that match the globs |
| R42 | `assert_after` must hold after a rewrite | yes | yes | `AssertAfterThatDoesNotHold_FailsTheRunNamingTheFile` |
| R43 | `unless_exists` binds every action kind | yes | no | **S9** — correct for all six, hand-verified; only `run` is exercised |
| R44 | a rewrite or delete glob outside the target is refused | yes | yes | two of the four containment rows |
| R45 | `append` creates, or adds only the missing lines | yes | yes | three rows, including a round-trip regression for non-ASCII assets |
| R46 | `append` is a no-op recorded as skipped when all lines are present | yes | yes | asserted byte-for-byte |
| R47 | the manifest declares every file the pack scaffolds | yes | no | **S11** — hand-verified a fourth time; the requirement underwriting the ungated write surface |
| R48 | the setup command writes only the charter | yes | no | the setup command's prose, human-verified by design |
| R49 | the pack's skill cites the manifest and carries no second list | yes | yes | `PackSkillCitesItsManifest` — it would fire on the file table this change removed |
| R51 | a supplied identifier wins over derivation and recovery | yes | yes | `SuppliedIdentifier_WinsOverDerivationAndRecovery` |
| R52 | an invalid supplied identifier is refused | yes | yes | four rows: empty, leading digit, space, dash |
| R53 | more than one solution file is refused; none falls back to derivation | yes | yes | both halves |
| R54 | the executable scan reuses the existing term list | yes | yes | one shared member, no copy |
| R55 | a stack term in a shipped engine executable fails the named check | yes | yes | `EngineExecutablesImposeNoStack`, guarded so it cannot pass on an empty file set |
| R56 | the report records every action with an outcome | yes | yes | including a rewrite or delete that matched nothing, and an unexpected failure inside an action |
| R57 | a preflight declared inside actions is rejected | yes | yes | `PreflightDeclaredInsideActions_IsRejected`, by kind and by stray field |
| R58 | a rewrite is scoped to created files and records `mutated` | yes | yes | `RewriteAndDelete_TouchOnlyWhatThisRunCreated` |
| R59 | relay every failed, mutated and removed entry, never a count | yes | no | the setup command's prose, human-verified by design |
| R60 | invoke the executor with all four arguments | yes | no | the setup command's prose; the command line is used verbatim by both test classes |
| R61 | the report carries a schema version; a consumer rejects an unknown one | yes | yes | producer asserted; the consumer half tested against the test's own reader, since the real consumer is prose |
| R62 | the pack ships a starter test asserting a real property | yes | yes | via the reported count; both assets assert the assembly-name and namespace convention, not a tautology |
| R63 | `run` starts inside the target; a `cwd` outside it is refused | yes | yes | `RunWhoseWorkingDirectoryEscapesTheTarget_IsRefused` |
| R64 | the integration suite runs and reports a count, or compiles and says so | yes | yes | the run branch was exercised here; see **S13** on what it exercises |
| R65 | an absent prerequisite fails rather than skips | yes | yes | `NoRowInThisClassCanSkip`, plus 0 skipped across the whole run |
| R66 | restore succeeds with transitive pinning on | yes | yes | the property is asserted present before the restore is trusted |
| R67 | the competition kind selects the machine-learning profile | yes | yes | declaration *and* pins, with the web kind asserted not to declare it |
| R68 | the starter notebook is headings only, no executable logic | yes | yes | no code cell, no recorded output, no execution count, no fenced block, and a minimum heading count |
| R70 | sweep every file under the target; under the artifact root, only what this run created | yes | yes | **new** — three rows, each against two different artifact roots; I mutation-tested all three. See **S16** for what *created* does not cover |

**52 of 63 live requirements have an automated check that can fail** — unchanged from the third
round, because R70 arrived with a check and R69 left with one. Seven are the setup command's own
prose and are human-verified by design: R15 (stop and name the missing answer), R16 (a re-run
offers amendment or gap-filling), R20 (relay the report verbatim), R48 (write only the charter),
R59 (show every failed, mutated and removed entry, never a count), R60 (invoke the executor with
all four arguments), and the ordering half of R13 (nothing generated while the charter is a draft).
Four are implemented and unchecked: R10 and R43 (`unless_exists` on kinds other than `run`), R26's
removal half, and R47. **None is unimplemented.**

---

## Manifest drift

`git diff --name-only` plus untracked files, against the plan's frontmatter slice manifest.

**Files claimed by a slice that nothing touched — none.** All twenty are accounted for.

```
 M .github/workflows/checks.yml                    release
 M README.md                                       release
 M plugins/sdlc-core/.claude-plugin/plugin.json    release
 M plugins/sdlc-core/skills/init/SKILL.md          init
 M plugins/stack-dotnet/.claude-plugin/plugin.json release
 M plugins/stack-dotnet/skills/stack-dotnet/SKILL.md                        pack
 M plugins/stack-dotnet/skills/stack-dotnet/assets/Directory.Packages.props pack
 M tests/AiSdlc.Tests/PluginStructureTests.cs      structure-checks
 M tests/AiSdlc.Tests/TempRepo.cs                  executor
 ?? plugins/sdlc-core/scripts/Invoke-ScaffoldManifest.ps1                   executor
 ?? plugins/stack-dotnet/skills/stack-dotnet/scaffold.json                  pack
 ?? plugins/stack-dotnet/skills/stack-dotnet/assets/competition.StarterTests.cs   pack
 ?? plugins/stack-dotnet/skills/stack-dotnet/assets/competition.starter.ipynb     pack
 ?? plugins/stack-dotnet/skills/stack-dotnet/assets/dotnet.gitignore              pack
 ?? plugins/stack-dotnet/skills/stack-dotnet/assets/solution.readme.md            pack
 ?? plugins/stack-dotnet/skills/stack-dotnet/assets/web.StarterTests.cs           pack
 ?? tests/AiSdlc.Tests/ScaffoldEmitsBuildableSolutionTests.cs               build-tests
 ?? tests/AiSdlc.Tests/ScaffoldManifestTests.cs                             executor
 ?? tests/AiSdlc.Tests/ScaffoldPrerequisiteTests.cs                         release
```

**One difference from the third round's drift table, and it is bookkeeping rather than a defect.**
`.sdlc/CHARTER.md` is claimed by the `release` slice and no longer appears in the working tree: the
version-4 amendment was **committed** in `a5042b8`, the commit that recorded the third
verification, rather than landing with the rest of the `release` slice. The content is right — I
read it: invariant I1 — *the engine imposes no stack* — is widened from the engine's instruction
files to the executables it ships, both instruments are named, and the register of silent failures
gains its eleven rows. The plan's rollback section asks that the charter amendment and the
`structure-checks` slice be reverted together or not at all, and they are now on opposite sides of
a commit boundary, so **a revert of the working tree alone would leave the charter claiming an
instrument that is not there.** Worth knowing before anyone reverts; not a reason to hold the ship.

**Files changed that no slice claims — two, both expected:**

| File | Status |
|---|---|
| `plugins/stack-dotnet/skills/stack-dotnet/assets/competition.readme.md` | accepted drift, recorded in the plan |
| `plugins/stack-dotnet/skills/stack-dotnet/assets/web.IntegrationStarterTests.cs` | accepted drift, recorded in the plan |

Both sit inside `plugins/stack-dotnet/`, which the `pack` slice owns exclusively, so slice
disjointness is unaffected. The plan's **Accepted drift** section records the owner accepting them
on 2026-09-18 and asks that they be reported as drift rather than treated as a blocking finding.
They are reported, and they are not blocking. The reasoning holds on a fourth reading: the emitted
README must carry an invariant about the orchestration host that means nothing in a competition
repository, so one shared asset could not honestly serve both kinds; and the generated sample
integration test contains no live test, so R26 (delete the sample content) and R64 (the integration
suite reports a count above zero) contradict each other unless something replaces it.

The competition kind also declares one preflight check where the plan's prose said it "carries
none". The plan records that too, and the implementer's reading was right.

`.sdlc/init-scaffolds-solution/spec.md` is at revision 4 and committed, so it does not appear in
the working tree.

---

## Build and test output

Real output, run by me on this machine, 2026-09-19. Counts read rather than the exit code, as R65
asks.

```
$ dotnet build tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo

  Determining projects to restore...
  All projects are up-to-date for restore.
  AiSdlc.Tests -> C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```
$ dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo

  Determining projects to restore...
  All projects are up-to-date for restore.
  AiSdlc.Tests -> C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll
Test run for C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   134, Skipped:     0, Total:   134, Duration: 4 m 56 s - AiSdlc.Tests.dll (net10.0)
```

**134 executed, 0 failed, 0 skipped.** Two more rows than the third round's 132, which is exactly
the third R70 test row run against two artifact roots. This machine has a container runtime, so the
run branch of R64 was taken rather than the compile-only branch.

### Fault injection: the three R70 rows, against mutated copies of the shipped executor

The lead asked whether the three rows holding R70 could actually fail, on the premise that a check
written to satisfy a review is the one most likely to assert the state its author has just created.
I did not stop at asking. **Nothing in the repository was modified:** each mutation was one line
changed in a copy of `Invoke-ScaffoldManifest.ps1` in a temporary directory, driven directly.

- **MUT-R69** makes the chain check always answer "not mine to sweep" — the implementation R70
  replaces.
- **MUT-R50** makes it always answer "sweep it" — the implementation R69 replaced.

```
                                        row 1            row 2            row 3
                                        placeholder in   placeholder in   placeholder in
                                        a chain file     a chain file     a scaffolded file
                                        this run did     this run DID     outside the chain
                                        NOT create       create
                                        (want exit 0)    (want non-zero)  (want non-zero)
SHIPPED                                 exit=0  PASS     exit=1  PASS     exit=1  PASS
MUT-R69  chain never swept              exit=0  PASS     exit=0  FAIL     exit=1  PASS
MUT-R50  chain always swept             exit=1  FAIL     exit=1  PASS     exit=1  PASS
```

Each mutation is caught by exactly one row and passed by the other two. **Row 2 is the row that
did not exist before this revision, and it is the only thing in the suite that can tell R70 from
R69** — which is what the spec's revision-4 note demands when it says *"a test proving only the
first would pass on the code R70 replaces"*.

### Behaviours I drove the shipped executor for by hand

```
# S16 - an `append` action writing an undeclared placeholder into the chain
destination absent  -> exit=1  token {{SOLUTION}} survives in: .sdlc/notes.md
destination present -> exit=0  scaffold completed
# the control: the identical action pointed outside the chain
                       exit=1  token {{SOLUTION}} survives in: docs/notes.md

# the artifact-root-is-'.' edge case, both halves
Case A, one charter   -> exit=1  no charter at ...\t\CHARTER.md - nothing has approved this scaffold.
Case B, two charters  -> exit=1  token {{SOLUTION}} survives in: src/generated.md
                                 (and the pre-existing src/stale.md was missed)
```

### What I read rather than ran

The full text of `Invoke-ScaffoldManifest.ps1` (1,119 lines), `ScaffoldManifestTests.cs` (1,178),
the three new checks in `PluginStructureTests.cs`, `ScaffoldPrerequisiteTests.cs`, both kinds of
`scaffold.json` action by action, the `init` skill body, the pack skill body, and the charter's
version-4 amendment.

---

## Not checked

- **Mutation testing of the remaining rows in the charter's register of silent failures.** The
  charter requires every row's check to be seen failing with the fault injected. The version-4
  amendment adds eleven rows. Across four rounds, five have been watched failing for real — the
  two halves of the placeholder sweep, the report-recording fix, the CI-prerequisite check, and now
  the third sweep row. **The obligation is outstanding for roughly six rows, and it is the owner's
  to close or to waive.** It is a claim about work the implementers were asked to do rather than a
  defect in the change, which is why it does not hold the ship.
- **An independent instrument for success criterion S3 — no placeholder survives — in the
  end-to-end suite.** The plan's test table promises `ScaffoldEmitsBuildableSolutionTests` will
  assert *"no `{{`-delimited token survives anywhere under the target"*. What it actually asserts
  is that the executor exited 0, which is true *because* the executor's own sweep passed. So if the
  sweep were disabled, the end-to-end rows would stay green. The sweep has three direct tests of
  its own, so this is a missing second opinion rather than an absence of coverage — but it is not
  what the plan says is there.
- **The compile-only branch of R64.** This machine has a container runtime, so the integration
  suite took the "run it" path. The branch where none exists could not be exercised short of hiding
  the runtime from the process.
- **CI on a hosted runner.** I did not push. Whether `dotnet new install
  Aspire.ProjectTemplates::13.5.3` succeeds on `windows-latest` and the web kind's preflight then
  passes there is still unproven, and it is the scenario R37 exists for. **First thing to watch on
  the first push.**
- **A clean machine without the Aspire templates.** The refusal path is covered by fixture tests;
  the real pack's real preflight against a machine that genuinely lacks them was not exercised.
- **The human-verified half of success criterion S10 and all of S11** — that the setup command
  calls the executor at the right point in its own prose, and that declining the module-boundary
  question stops the command rather than selecting a kind. Both need `/sdlc-core:init` run by hand
  in an empty directory. I read the skill body: it settles the kind, the boundaries and the
  identifier before the charter, writes the charter and nothing else, and runs the executor after
  the approval with all four arguments and the relay rules spelled out. I did not run it.
- **Whether a manifest could escape the target through a directory junction.** Containment resolves
  a full path and compares prefixes, which does not follow a junction. That sits inside the trust
  boundary that flagged concern F1 — *the engine runs command strings a pack supplies* — places at
  the pack, so I did not pursue it; noted rather than left silent, because silence reads as
  coverage.
- **Hostile-manifest robustness outside the action loop.** The action loop is wrapped so an
  unexpected failure still produces a report. Preflight parsing is not: a manifest declaring
  `"expect_exit": "abc"` throws on the cast at line 785 and produces a stack trace with no report.
  Same trust boundary as above, and a smaller hole than the one that section already accepts.
