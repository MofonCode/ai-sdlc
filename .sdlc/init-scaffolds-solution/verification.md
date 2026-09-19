---
slug: init-scaffolds-solution
status: approved
spec: ./spec.md
plan: ./plan.md
verdict: ship
verified: 2026-09-19
requirements_total: 63
requirements_tested: 54
verification_rounds: 6
---

*Frontmatter describes the sixth and current verification, at the foot of this file. The five
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

---

# Verification 5 — 2026-09-19, of the state after spec revision 5

**Which state this describes.** The fourth round returned SHIP against spec revision 4 and
carried two items. Both were then acted on, so that verdict describes a state that no longer
exists. This round verifies the state after:

- the post-run placeholder sweep moved from **R70** — a chain file is swept only when this run
  *created* it — to **R71**, which widens "created" to "wrote to, by creating or appending",
  because `append` writes to a file that is already on disk and creates nothing;
- **R47** — the pack's manifest is the declaration of every file the pack scaffolds, so that
  nothing but the executor writes one — gaining machine checks for the first time, after four
  rounds in which it had none.

I reached my own conclusions. The four earlier verdicts were read as evidence about earlier
states, not inherited as a baseline.

---

## Verdict

**DO NOT SHIP**, on one finding.

The suite is green — 139 tests, 0 failed, 0 skipped — and almost everything in this change is
ready. The containment paths are real, the two new checks behind R47 are genuine instruments
rather than paperwork, and the design decision the implementer made against the lead's steer is
the right one; I say so with reasons below rather than deferring to it.

The finding is this. **R71 — the placeholder sweep covers a file under the artifact chain only
when this run wrote to it, by creating or appending — still does not cover every way this run
writes to a file.** `append` was the mechanism the fourth round found missing. It was not the
only one. A `run` action's generator that modifies a chain file which already existed writes
bytes that nothing registers, so the sweep skips the file and the run exits 0 with the
placeholder sitting on disk and no entry in the report. I reproduced it in one command; the
transcript is under **Blocking findings**.

The implementation is faithful to R71 *as written* — R71 names two mechanisms and a generator
modification is neither — so this is a gap in the requirement that the code inherited honestly,
not an implementation defect. That matters for how it gets fixed, and it is why I am blocking
rather than carrying it: **this is the fourth time this one boundary has been specified by
enumerating mechanisms, and the fourth time the enumeration turned out to be short.** The spec
itself says the pattern is the lesson. A fifth mechanism word would leave the next reader to
find the sixth. The fix that ends it is to stop naming mechanisms and define the set by
observation, which the executor is already most of the way to computing.

Everything else I found is in **Should fix**. None of it is blocking, and I say plainly per item
why not.

---

## Blocking findings

### B1 — A generator that modifies an existing chain file writes a placeholder past the sweep, and the run exits 0

**What R71 asks for:** when the executor finishes, any file under the target directory holding a
placeholder the manifest declares fails the run and is named — *except* a file under the
resolved artifact chain, which is swept only when this execution wrote to it, "whether by
creating it or by appending to it".

**What R71's own rationale says**, in the sentence it inherits verbatim from the requirement it
replaces: *the executor is answerable for what it wrote and for nothing else.*

**Those two are not the same set, and the narrower one is the sentence again.** The failure
shape is identical to the one revision 5 was written to close, one action kind over.

**The scenario, run rather than argued.** A target repository with an approved charter at
`.sdlc/CHARTER.md` and one other chain file, `.sdlc/notes.md`, already on disk. A manifest with a
single `run` action whose command appends a line carrying the pack's placeholder to that file.
The placeholder is assembled from pieces inside the command so that it is not in the manifest
text — the executor substitutes its own strings before running anything, which is correct, and
is why a generator is the only way the placeholder reaches disk here:

```
$ powershell -File plugins/sdlc-core/scripts/Invoke-ScaffoldManifest.ps1 \
    -Manifest <fixture>/scaffold2.json -Kind demo -Target <fixture>/repoB

solution identifier: RepoB (derived from the directory name)
action 1 (run) powershell.exe -NoProfile -Command "$t = '{' + '{SOLUTION}' + '}'; ..."
scaffold completed for kind 'demo' in ...\repoB
EXIT=0

$ cat repoB/.sdlc/notes.md
pre-existing note
assembly = {{SOLUTION}}.Thing

$ grep outcome report.json
    "outcome":  "completed",
```

Exit 0. Outcome `completed`. The placeholder is on disk. The report carries one `run` entry
recorded as `created` and nothing else — no token entry, because the sweep never read the file.

**The control, in the same fixture, proving it is the modification and not the directory.** Add a
second line to the same command writing the same text to `.sdlc/fresh.md`, which does *not*
already exist:

```
token {{SOLUTION}} survives in: .sdlc/fresh.md
EXIT=1
   { "kind": "token", "outcome": "failed", "path": ".sdlc/fresh.md",
     "reason": "token {{SOLUTION}} was not substituted" }
```

The file the generator **created** is registered and swept. The file the generator **modified**
is not. Same command, same directory, same run.

**Why that asymmetry is the finding and not a quibble.** The executor already accepts authorship
of what a generator produces — after every `run` it walks the tree and registers everything that
was not there before (`Invoke-ScaffoldManifest.ps1:906-908`), which is what makes generator
output eligible for `rewrite` and `delete` as well as for the sweep. So the executor's own model
of "this run wrote it" is inconsistent between two outcomes of the same action: a generator
creating a chain file is this run's, a generator modifying one is not. The gap is not a
principled limit; it is the limit of what "created" can detect.

**I enumerated the write paths rather than trusting a list.** Every call in the script that puts
bytes into a file:

| Line | Path | Registered? |
|---|---|---|
| 946 | `copy`, no substitution — `Copy-Item` | yes, as created |
| 950 | `copy`, with substitution — `WriteAllText` | yes, as created |
| 981 | `append` where the file is absent — `WriteAllText` | yes, as created |
| 1012 | `append` where the file exists — `AppendAllText` | yes, as appended — **this is what R71 added** |
| 1046 | `rewrite` — `WriteAllText` | already created; `rewrite` can only reach the created set |
| 1091 | `delete` — `Remove-Item` | removed from both sets; the file is gone |
| 815, 900 | `preflight` and `run` — an external command | **new files yes, modifications no** |

The five directory creations (625, 871, 895, 939, 979) write no file bytes. So the closed set of
unregistered writes is exactly one entry: **bytes written by a command the executor started, into
a file that existed when the execution began.** There is also a narrower sibling — a file created
by a *preflight* command is registered only if some later `run` action happens to trigger the
tree walk, so a kind with preflights and no `run` action leaves preflight output unregistered
too.

**Likelihood, stated honestly, because it argues the other way.** Nothing in `stack-dotnet`
declares a `run` action that writes into `.sdlc/`, and a generator that writes there at all is
doing something unusual. This is less likely than the case R71 closed, where the realistic shape
— a pack appending a section to the charter, which the setup command always writes first — is
named in the spec. If the question were only "will this fire", the answer would be "probably
never", and carrying it would be defensible.

**Why I am blocking anyway.** Not for this one hole. For the method that keeps producing them,
and because the change that ends it is small and available now:

- **R50** — sweep every file under the target — drew the boundary by *location* and caught the
  engine's own paperwork.
- **R69** — exclude the chain — drew it by *location* again and stopped catching a pack's
  mistake.
- **R70** — sweep a chain file this run *created* — drew it by authorship and then
  under-described its own principle.
- **R71** — *created or appended* — extends the enumeration by one term. This finding is the
  next term.

The spec's own words on revision 5 are *"a requirement whose sentence and whose rationale
disagree will be implemented from the sentence."* R71's sentence and rationale still disagree, in
the same direction, and it was implemented from the sentence again — exactly as predicted.

**The fix, which is not another mechanism word.** Define the exception observationally instead of
by enumeration: *a file under the resolved artifact chain is swept unless it existed when this
execution began **and its bytes are unchanged since**.* That is complete by construction — it
cannot be short a mechanism, because it names no mechanism — and it is the same set the current
rule is trying to describe. The executor already snapshots what was on disk before the first
action (`$script:PreExisting`, line 793); this needs that snapshot to carry a content hash for
chain files only, which is a handful of files, and one comparison in `Test-Sweepable`. It wants a
requirement of its own (an R72) because it changes what the spec promises, not just how the
script computes it.

**The test that pins it**, and which no current row would catch: a manifest whose `run` action's
command modifies a pre-existing chain file to contain the placeholder must halt the run and name
that file. The four rows that exist for R71 all pass on the code as it stands.

---

## Should fix

None of these blocks. Each says why.

### S1 — The two-set design is correct, and the guard test really does pin it

Not a defect — the lead asked for an independent judgement on a decision the implementer made
against an explicit steer, so here it is, and it does not agree with the steer.

**The steer was to fold appended paths into the single set the executor already keeps.** The
implementer refused and kept two: `$script:Created`, which is what R41 — a `rewrite` or a
`delete` may touch only paths this execution created — bounds those two actions to; and
`$script:Appended`, which exists only so the sweep knows what else this run put bytes into. The
union is computed at its single point of use, `Test-Sweepable`.

**The reasoning holds, and merging would have been a regression.** The two sets answer two
different questions, and only one of them is about ownership. An `append` destination was already
on disk, so its existing content belongs to whoever put it there. Merging would make an appended
file eligible for `delete` — and the pack's own manifest appends to `.gitignore`, a file a
generator or a mature repository routinely already has. A `delete` glob broad enough to catch it
would then remove a real repository's ignore file because the pack added two lines to it, which
is precisely the harm R41 exists to prevent. The safer-looking simplification is the dangerous
one here.

**They cannot drift in a way that matters.** I traced every transition. `delete` removes a path
from both sets (lines 1092-1093), and that second removal is reachable only for a file that was
created *and* appended to in the same run, which is the only state that puts a path in both.
Nothing else writes either set after registration, nothing reads the union anywhere but
`Test-Sweepable`, and no third set caches it. There is no pair of values that can disagree,
because there is no invariant between them to violate — which is the property the implementer's
comment claims and I was able to confirm rather than take.

**The guard test is not decorative.** `AFileThisRunOnlyAppendedTo_IsNotTheRunsToRewriteOrDelete`
appends to a pre-existing `kept.gitignore`, then runs a `rewrite` and a `delete` whose globs both
match it, and asserts the file survives with its original line intact. Under the merged design
the `rewrite` would fire and the `delete` would remove the file, so both assertions fail — the
test discriminates the two designs rather than describing the one that shipped. It also asserts
the appended line is present, so it cannot pass by the append having silently not happened.

**The one residual, and it is small.** `Test-Sweepable` is the only place the union is computed.
A future requirement that needs "what did this run write to" somewhere else will have to
recompute it, and the next author has only the comment to tell them. Worth leaving as it is;
worth knowing.

### S2 — The two new checks behind R47 are real instruments, with one vacuity gap

The lead's concern was the right one to raise: a check written to close a finding tends to assert
the state its author just created, and R47 — the manifest declares every file the pack scaffolds,
so that nothing but the executor writes one — is a claim about *absence*, which is the hardest
shape to instrument. I asked of each check what change would break it and whether anyone would
make that change.

**`EveryPackAsset_IsDeclaredByItsManifest`** holds the manifest and the pack's `assets/`
directory against each other in both directions. It is not vacuous today: fourteen assets are
declared and exactly those fourteen are on disk, matched name for name. It breaks on two changes
people really make — adding an asset file without giving it a manifest action, which is the
pre-change habit this whole slice exists to end; and renaming or moving an asset the manifest
still names, which otherwise fails mid-scaffold on a user's machine. The assets directory is
derived from the declared paths rather than written into the test, so a pack that reorganises
carries the check with it.

**Its gap:** unlike its sibling it has no non-vacuity guard. A manifest that declared no assets at
all would yield an empty expectation set, contribute no directory to scan, and pass silently. The
sibling check asserts its needle set is non-empty for exactly this reason; this one should assert
the declared set is non-empty. Not blocking, because such a manifest fails several other rows.

**`EngineInstructions_NameNoPackFile`** is the half that actually speaks to R47's absence claim:
it reads the destination filenames out of each pack's manifest and fails if any engine skill or
agent file names one outside a fenced example. It has the non-vacuity guard, and it has a
precedent rather than a hypothesis — the setup skill's previous wording had the engine writing
seven of the pack's configuration files itself, which is how the half-gated write surface
survived the design that claimed to have resolved it. Re-adding one line of that prose fails this
check. That is a change someone would plausibly make.

**Its hazard, in the safe direction:** the needle set includes `README.md` and `.gitignore`,
because the pack copies and appends to those names. No engine instruction file mentions either
today. The day one legitimately does — telling an owner to update the repository's own README,
say — this check fails with a message about pack files. Erring towards firing is the right
direction for this check, and the message names the pack that declared the file, so the failure
is at least diagnosable. Worth knowing before someone hits it.

**What neither check reaches**, and both say so in their own remarks: a file written by a
generator inside a `run` action, and prose that describes a pack file without spelling its name.

### S3 — `unless_exists` binds every action kind, and only one kind is tested (carried, third round)

**R43** — where an action declares `unless_exists`, the executor skips it when that path is
present, for every action kind and not only for `run`. The implementation is right: the check
sits at the top of the action loop before the kind switch (lines 846-857), so it binds all six
kinds by construction.

The test does not hold that. `SecondExecution_ChangesNoFileAndExitsZero` declares `unless_exists`
on a `run` action only; every other action in its fixture reaches `skipped` through its own
idempotence — `mkdir` finds the directory, `copy` finds the destination, `append` finds its lines
present, `rewrite` and `delete` are bounded to what this run created. Move the check inside the
`run` branch and the test still passes.

**Why it does not block, and this is a stronger reason than the last two rounds gave.** For the
other five kinds `unless_exists` is a convenience, not the thing that makes a re-run safe — each
already refuses to act on a destination that exists, for its own reasons, and those refusals *are*
tested. Breaking R43 for `mkdir` or `copy` would change nothing observable. The fix is one fixture
row declaring `unless_exists` with a sentinel on an action of each kind; it is cheap and it
should be done, but nothing rides on it.

### S4 — Removal of generator sample content is reported, never measured (carried, second round)

**R26** — the manifest removes every sample page, component, endpoint and data type the
generators produce, and leaves at least one executable test. The second half has a real
instrument: both suites are run and a test count above zero is asserted from a TRX file, so a
suite deleted down to nothing fails loudly. The first half has none. No row asserts that
`WeatherForecastController.cs`, `WeatherForecast.cs`, `Test1.cs`, `IntegrationTest1.cs` or
`Class1.cs` are gone from the emitted tree.

What exists instead is a report entry: a `delete` that matched nothing is recorded as `skipped`
with the reason "no file this run created matched '<glob>'", which the setup command relays. That
is reporting, not measuring — it needs a human to read a line in a report and notice.

**The failure it leaves open:** a template bump renames the sample file, every glob matches
nothing, the run exits 0, the build is green, both suites pass, and every repository scaffolded
from that day forward ships the generator's demo content. Nothing in the suite says a word.

**Why it does not block:** the scaffold is still correct in every other respect when this fires,
and the report does carry the evidence. The fix is one row per kind asserting the sample paths
are absent from the emitted tree, which also gives R26's first half its I9 entry.

### S5 — A refusal creates the target directory and leaves it behind (carried, second round)

Confirmed by running it. Pointed at a directory that does not exist, the executor creates it
(line 625) before the approval gate, then refuses because there is no chain, leaving an empty
directory on disk:

```
$ powershell -File Invoke-ScaffoldManifest.ps1 ... -Target <fixture>/probeC
no artifact chain under ...\probeC - there is no charter to approve this scaffold.
EXIT=1
$ ls probeC
(empty)
```

**R38** and **R39** — the executor reads the charter's status and refuses to write anything
without an approved one — say "write nothing", and no file is written. An empty directory is
cosmetic. Not blocking; worth either creating the target after the gate, or removing it on a
refusal that created it.

### S6 — R61's consumer half is proven only by a consumer the suite wrote

**R61** — the report carries a schema version and a consumer rejects a version it does not know.
The first half is tested against the real script. The second is tested against `Accept`, a helper
inside `ScaffoldManifestTests` that the same author wrote. The production consumer is the setup
skill, and its body never mentions `report_version` at all — it is told to read the report and
relay it, with no instruction to check the version first.

Not blocking: the setup skill is prose and human-verified by design, and there is only one report
version in existence. Worth a sentence in the skill body so the requirement has a consumer
outside the test that asserts it.

### S7 — Two smaller holes in instruments, and one carried item, noted rather than left silent

- **`NoManifest_InstallsItsOwnPrerequisite`** — which holds **R30**, that a missing prerequisite
  fails with the pack's instruction and nothing is installed — scans `run` actions' arguments for
  the install verb and does not scan `preflight` commands. A manifest that installed from a
  preflight would pass. The preflight is the *first* thing that runs, so it is if anything the
  more attractive place to put one.
- **Two start-up refusals emit no report.** A `-Target` that is not a usable path, or that names
  a file, exits 1 at lines 615 and 623 before the target is resolved, with a stderr line and no
  JSON on stdout. **R56** binds "completes or halts" and **R20** has the setup command relay the
  report verbatim; on those two paths there is nothing to relay. Reachable only by passing a
  garbage path.
- **Hostile-manifest parsing outside the action loop** — carried unchanged from the fourth round.
  `"expect_exit": "abc"` still throws on the integer cast during preflight (line 818), outside the
  wrapped action loop, producing a stack trace and no report. Inside the trust boundary that
  flagged concern **F1** — the engine runs command strings a pack supplies — places at the pack.

---

## Requirement coverage

Live requirements only. **Eight are struck through in the spec and correctly claimed by nobody:**
R6 (the report outcome set with no `mutated` or `removed`), R8 (the placeholder rule bound to
files this run wrote), R11 (the `required` rewrite that contradicted re-runnability), R14 (the
two-argument invocation), R33 (the integration criterion a zero-test suite could satisfy), R50
(the sweep of every file under the target), R69 (the unconditional chain carve-out) and now
**R70** (the carve-out that said "created" where it meant "wrote to"). **Sixty-three live.**

| Req | What it says | Implemented | Tested | Note |
|---|---|---|---|---|
| R1 | every pack ships a scaffold manifest | yes | yes | `EveryPack_ShipsAScaffoldManifest`, guarded against an empty pack list |
| R2 | the engine names no string a pack owns | yes | yes | both invariant-I1 checks off one shared term list, which carries the file extension and the placeholder spelling |
| R3 | actions run in order, halting at the first failure | yes | yes | asserts the later action did not run |
| R4 | a path outside the target is refused, nothing written | yes | yes | one row per action kind |
| R5 | an existing destination is skipped and left byte-identical | yes | yes | |
| R7 | a second run changes no file and exits 0 | yes | yes | fixture manifest, and the real pack per kind by tree hash including the chain |
| R9 | preflight runs first; on failure nothing is written | yes | yes | relays the pack's remediation verbatim |
| R10 | `unless_exists` skips the action (restated as R43) | yes | partial | see R43 |
| R12 | an unknown or missing kind lists the declared kinds | yes | yes | plus an unknown manifest version refused before any action |
| R13 | no scaffold action while the charter is draft | yes | n/a | the setup command's prose; the substantive half is machine-checked by R38 |
| R15 | stop and name the missing answer when the kind or the boundaries are unsettled | yes | no | prose, human-verified by design |
| R16 | a re-run offers charter amendment and gap-filling | yes | no | prose; the gap-filling half stays unblocked — a chain document quoting the placeholder does not halt the run |
| R17 | derive the identifier from the directory name, present it for confirmation | yes | yes | derivation tested per case; the confirmation half is prose |
| R18 | an underivable candidate is refused, never repaired | yes | yes | empty and leading-digit rows |
| R19 | recover the identifier from an existing solution file, never re-derive | yes | yes | |
| R20 | relay the report verbatim; never claim the repo is scaffolded after a non-zero exit | yes | no | prose |
| R21 | the web kind emits the frozen set of projects | yes | yes | `EmittedLayoutIsTheFrozenOne_Web`, projects also checked registered in the solution |
| R22 | the competition kind emits its frozen set | yes | yes | `EmittedLayoutIsTheFrozenOne_Competition` |
| R23 | emitted test and host names match a coverage exclusion | yes | yes | patterns parsed from the emitted settings file, not copied into the test |
| R24 | the emitted README carries build, test, local-run, chain location, the authority invariant | yes | yes | expectations stated in the test rather than lifted from the asset |
| R25 | a base ignore file alongside the Terraform entries | yes | yes | both sets asserted, so one cannot stand in for the other |
| R26 | sample content removed; at least one executable test left | yes | partial | the second half only — see should-fix S4 |
| R27 | version attributes stripped from every package reference | yes | yes | asserted over the emitted projects, plus the restore |
| R28 | the pack pins every package its generators emit | yes | yes | the restore is the only instrument, and the spec says so |
| R29 | an inline SDK version rewritten to the pinned one | yes | yes | via the rewrite's `assert_after` and the build |
| R30 | a missing prerequisite fails with the pack's instruction; nothing is installed | yes | yes | with the preflight hole in should-fix S7 |
| R31 | the emitted solution builds with exit 0 | yes | yes | per kind, real SDK |
| R32 | the unit suite reports a count above zero and no failures | yes | yes | count read from a TRX, not from console text |
| R34 | a partial repository is filled, everything else byte-identical | yes | yes | `tests/` deleted and restored, rest hashed |
| R35 | invariant I1 still holds | yes | yes | both I1 checks |
| R36 | the repository README describes what setup produces | yes | yes | layout read out of each manifest, so the README tracks what is emitted |
| R37 | CI installs a prerequisite the manifest declares and refuses to install | yes | yes | workflow and manifest parsed and held against each other, with fabricated-workflow rows proving the rule can fail |
| R38 | the executor reads the charter's status and refuses a draft | yes | yes | non-zero, empty tree |
| R39 | no charter means refuse | yes | yes | both "chain without charter" and "no chain" |
| R40 | the resolved chain root is named in the report | yes | yes | tested against a non-default root |
| R41 | `rewrite` and `delete` touch only paths this run created | yes | yes | proven with a pre-existing file matching the glob |
| R42 | `assert_after` failing fails the run naming the file | yes | yes | |
| R43 | `unless_exists` binds every action kind | yes | partial | tested on `run` only — should-fix S3 |
| R44 | a `rewrite` or `delete` glob outside the target is refused | yes | yes | |
| R45 | `append` creates the file, or adds only the lines not present | yes | yes | including a non-ASCII asset surviving a re-run |
| R46 | `append` with every line present is a no-op recorded as skipped | yes | yes | |
| R47 | the manifest declares every file the pack scaffolds | yes | yes | **new this round** — `EveryPackAsset_IsDeclaredByItsManifest` and `EngineInstructions_NameNoPackFile`; assessed in should-fix S2 |
| R48 | the setup command writes the charter and nothing else | yes | yes | **newly instrumented** by the engine-instruction scan |
| R49 | the pack's skill cites the manifest and carries no competing list | yes | yes | `PackSkillCitesItsManifest`, with a narrow file-table rule |
| R51 | a supplied identifier wins over derivation and recovery | yes | yes | |
| R52 | an invalid supplied identifier is refused | yes | yes | |
| R53 | more than one solution file refused; none falls back to derivation | yes | yes | |
| R54 | the executable scan reuses the same term list | yes | yes | one shared member, asserted non-empty |
| R55 | a stack term in a shipped engine executable fails a named check | yes | yes | `EngineExecutablesImposeNoStack` |
| R56 | every action recorded with an outcome and its path or command | yes | yes | all five outcomes asserted present, plus the two that can match nothing |
| R57 | a preflight declared inside actions is rejected | yes | yes | by kind and by field |
| R58 | a rewrite records each file it changed as mutated | yes | yes | |
| R59 | failures, mutations and removals shown in full, never a count | yes | no | prose |
| R60 | the executor is invoked with all four arguments | yes | no | prose; the command line is frozen in the plan and driven by two test classes |
| R61 | the report carries a schema version and a consumer rejects an unknown one | yes | partial | see should-fix S6 |
| R62 | a starter test asset that asserts a property of the emitted solution | yes | yes | via the suite count and the emitted layout |
| R63 | `run` starts inside the target; a `cwd` outside it is refused | yes | yes | |
| R64 | the integration suite runs and reports a count, or compiles and says it was not run | yes | yes | run path exercised on this machine; the compile-only branch is in **Not checked** |
| R65 | an absent prerequisite fails rather than skips | yes | yes | `NoRowInThisClassCanSkip` scans for both `[Ignore]` and inconclusive results |
| R66 | restore succeeds with transitive pinning on | yes | yes | the setting is asserted present so the restore proves the larger surface |
| R67 | the competition kind applies the machine-learning profile | yes | yes | and asserts the web kind does not, so the row says something about one kind |
| R68 | the notebook is headings and a solution reference only | yes | yes | no code cell, no recorded output, no fenced block |
| **R71** | **the sweep covers a chain file this run wrote to, by creating or appending** | **partial** | **partial** | **four rows, all passing; the fifth mechanism is uncovered — blocking finding B1** |

**Fifty-three fully tested.** Five are prose the setup command owns and are human-verified by
design (R15, R16, R20, R59, R60); one is prose whose substantive half is machine-checked
elsewhere (R13); four are partial (R10 and R43, `unless_exists` on one kind; R26, removal not
measured; R61, the consumer is the suite's own). R71 is the finding.

---

## Manifest drift

`git diff --name-only` plus untracked files against the plan's frontmatter slice manifest.

**Every file the manifest claims was touched.** All twenty are present — the executor script and
its two test files, the pack's manifest, skill and six assets, the setup skill, the structure
checks, the build-tests class, and the release slice's six. `.sdlc/CHARTER.md` is the one that
does not appear in `git status`: its amendment is already committed (`056121b`, with the
amendment log's row 4 dated 2026-09-18 widening invariant I1's checkable clause to shipped
engine executables and adding the new rows to I9's table). That is the release slice having
landed, not a slice that did not.

**Two files beyond the manifest, and both are the recorded, accepted drift:**

| File | Status |
|---|---|
| `plugins/stack-dotnet/skills/stack-dotnet/assets/competition.readme.md` | accepted 2026-09-18 — R24's orchestration-versus-infrastructure invariant is meaningless in a competition repo, so one README asset could not honestly serve both kinds |
| `plugins/stack-dotnet/skills/stack-dotnet/assets/web.IntegrationStarterTests.cs` | accepted 2026-09-18 — the generated sample integration test has zero live tests and a property that fails compilation under warnings-as-errors, so R26 and R64 contradict each other without a replacement |

Both sit inside `plugins/stack-dotnet/`, which no other slice touches, so disjointness was never
at risk. Reporting them is the check working; the owner accepted them in the plan's dated
post-approval section. `assets/dotnet.gitignore`, listed there for completeness, is in the
manifest and is not drift.

**No unplanned file.** Nothing outside the manifest and those two.

---

## Build and test output

The whole suite, real output:

```
$ dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo

  Determining projects to restore...
  All projects are up-to-date for restore.
  AiSdlc.Tests -> C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll
Test run for C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   139, Skipped:     0, Total:   139, Duration: 5 m 10 s - AiSdlc.Tests.dll (net10.0)
```

**Read as counts, not as an exit code: 139 run, 0 failed, 0 skipped.** Nothing was ignored and
nothing reported inconclusive — `NoRowInThisClassCanSkip` asserts both for the end-to-end class,
and the zero in the Skipped column is the suite-wide confirmation.

**The integration suite really ran on this machine rather than taking R64's compile-only
downgrade**, which is what makes a green run mean what the spec's end-to-end section says it
means. Re-run with the row's own output visible:

```
$ dotnet test ... --filter "FullyQualifiedName~EmittedSuitesReportATestCount" -l "console;verbosity=detailed"

 web: scaffolded into C:\Users\peter\AppData\Local\Temp\aisdlc-scaffold-9f27a37f...\probe-repo
 web: container runtime detected (docker)
 web / Probe.IntegrationTests: ran 1 test(s), 0 failed
 web / Probe.UnitTests: ran 2 test(s), 0 failed
 competition: scaffolded into C:\Users\peter\AppData\Local\Temp\aisdlc-scaffold-430af07f...\probe-repo
 competition: container runtime detected (docker)
 competition / Probe.UnitTests: ran 2 test(s), 0 failed
```

A machine created two solutions from nothing in that run, compiled both, and watched their own
tests execute — the integration suite included, against a started container.

**The blocking finding's reproduction**, run against the shipped script with a two-line fixture
manifest, is transcribed in full under **B1**: exit 0, outcome `completed`, `{{SOLUTION}}` left in
`.sdlc/notes.md`, and the control case in the same fixture halting on `.sdlc/fresh.md`.

---

## Not checked

- **Mutation-testing the two new R47 checks by injecting a fault.** Doing it properly means
  adding an undeclared asset file or a pack filename to an engine skill, and this verification
  was instructed to modify no file but this one. I established non-vacuity a different way — the
  declared and on-disk asset sets are both fourteen and match name for name, and the
  engine-instruction scan asserts its needle set is non-empty — and I traced what each check
  reads. That is weaker than watching them fail, and I am saying so rather than implying I ran
  them red.
- **The compile-only branch of R64.** This machine has a container runtime, so the downgrade path
  could not be exercised short of hiding Docker from the process. Unchanged from the fourth
  round.
- **CI on a hosted runner.** I did not push. Whether `dotnet new install
  Aspire.ProjectTemplates::13.5.3` succeeds on `windows-latest` and the web kind's preflight then
  passes is still unproven, and it is the scenario R37 exists for. **Still the first thing to
  watch on the first push.**
- **A clean machine without the Aspire templates.** The refusal path is covered by fixture tests;
  the real pack's preflight against a machine that genuinely lacks them was not exercised.
- **The human-verified half of S10 and all of S11** — that the setup command calls the executor at
  the right point in its own prose, and that declining the module-boundary question stops the
  command rather than selecting a kind. I read the skill body and it does both: the kind, the
  boundaries and the identifier are settled before the charter, the charter is the only file it
  writes, and the executor runs after approval with all four arguments and the relay rules spelled
  out. I did not run `/sdlc-core:init` by hand.
- **The charter root declared as `.`** — the third and fourth rounds established it needs two
  charters disagreeing about where the chain lives before the run starts at all. I did not
  re-derive it a third time; the fourth round's recommendation to record it in the
  known-and-unchecked list with its two-charter precondition still stands, and B1 does not change
  it.
- **Whether a manifest could escape the target through a directory junction.** Containment
  compares resolved path prefixes, which does not follow a junction. Inside the trust boundary
  flagged concern F1 places at the pack.
- **A locked or unreadable file during the sweep.** The sweep skips a file it cannot read
  (line 1141) and says nothing. A file holding a placeholder that something else has open would
  pass. Unavoidable, and noted because silence reads as coverage.

---

# Verification 6 — 2026-09-19, of the state after spec revision 6

**Which state this describes.** The fifth round returned DO NOT SHIP on one finding and that
finding was acted on, so the fifth verdict describes a state that no longer exists. This round
verifies the state after:

- the post-run placeholder sweep changing **kind** rather than wording — from **R71**, a chain
  file is swept when this run *created or appended to* it, to **R72**, a chain file is swept
  unless it was on disk before the run began *and its content is unchanged since*. R72 names no
  write mechanism at all;
- the executor's pre-run snapshot gaining a content hash for chain files, which is what makes
  that question answerable;
- **`$script:Appended`**, the second of the two tracking sets, being deleted — it became unread
  the moment the sweep stopped asking how a file was written;
- two checks gaining instruments: a non-vacuity guard on the check that holds every pack asset
  against its manifest, and two new rows measuring that the removal of generator sample content
  actually removed something.

I reached my own conclusions and ran everything I report. The five earlier verdicts were read as
evidence about earlier states, not inherited.

---

## Verdict

**SHIP.**

The suite is green — 143 run, 0 failed, 0 skipped — and the thing the fifth round blocked on is
genuinely fixed rather than papered over. I reproduced the fifth round's defect against a mutated
copy of the shipped script and watched the shipped script catch what the mutant misses, so the
new row is an instrument and not a description.

**On the lead's first question — is R72 a rule rather than a patch.** Largely yes, and I tested
the claim rather than accepting it. R72's exception to the sweep — *a chain file is skipped only
where it existed before the run with its content unchanged* — is answered from content, so it is
complete over write mechanisms by construction: I drove the shipped executor with a generator
that modifies a pre-existing chain file (the fifth round's exact defect), with a **preflight**
command that does the same (a path nothing registers at all, and which the fifth round named as
a narrower sibling of its own finding), and both halt the run. That is the property the four earlier versions of
this rule did not have.

**But it is not the whole story, and the spec's completeness claim is overstated.** Spec revision
6 says the change-and-change-back case "is the only such case left now that the rule is
content-based". It is not the only exclusion: the executor also skips everything under `.git`
outright, by **location**, and no requirement authorises that. I ran it — a generator writing the
placeholder into `.git/probe.txt` exits 0 with the placeholder on disk. That is a real divergence
between R72's sentence ("any file under the target directory") and the code, and it is the same
*method* — exclusion by location — that the spec says it has abandoned. It is small: no pack
writes into `.git`, nothing there reaches the emitted solution, and skipping a repository's
object database is the reason the sweep is fast on a mature repo. So it is a should-fix and a
one-line spec correction, not a blocker.

**On the lead's second question — the deleted set.** Removing `$script:Appended` loosened
nothing, and I confirmed it two ways rather than by reading. Nothing reads it (the whole symbol
is gone; the only set `rewrite` and `delete` can reach is still `$script:Created`, and `append`
to an existing file has never registered there). More importantly I mutation-tested the bound:
with `append` folded into the created set, a pre-existing `kept.gitignore` that the pack appended
two lines to is **deleted** by a `delete` glob; with the shipped script it survives with its
original line intact. The test that claims to hold this really does hold it.

**On the lead's third question — the new checks.** One is a genuine instrument, one is a guard
that is correct but is not itself falsifiable by any change anyone would make. Detail under
**Should fix**.

Everything I found is in **Should fix**, and most of it has been carried across several rounds by
choice rather than by oversight. My answer to the convergence question is at the end, under
**Has this converged**.

---

## Blocking findings

**None.**

The fifth round's finding — a generator modifying a pre-existing chain file writes a placeholder
the sweep never reads — is fixed. The transcript is under **Build and test output**; the shipped
script exits 1 naming the file, and a copy of that script with R72's content comparison removed
exits 0 with the placeholder on disk.

---

## Should fix

None of these blocks. Each says why, and each says what change would make the relevant check fail.

### S1 — The sweep skips `.git` by location, which R72 does not authorise and the spec does not mention (new)

**What R72 asks for:** when the executor completes, any file under the target directory holding a
placeholder the manifest declares fails the run and is named — except a file under the resolved
artifact chain that was there before the run with its content unchanged.

**What the code does:** the file walk the sweep uses excludes every directory named `.git`
(`Invoke-ScaffoldManifest.ps1:70`, read by `Get-TargetFile` at line 412). Files under `.git` are
excluded from the pre-run snapshot for the same reason, so the exclusion is consistent — and
total. Run against the shipped script:

```
action 1 (run) powershell.exe ... Set-Content -LiteralPath '.git/probe.txt' -Value ('assembly = ' + $t)
scaffold completed for kind 'gitdir' in ...\probe_gitdir
EXIT=0

$ grep -rl SOLUTION probe_gitdir
probe_gitdir/.git/probe.txt
```

**Why it is worth writing down even though it is small.** Spec revision 6's argument is that R72
cannot be short a mechanism *because it names none*, and its stated cost is one case — a chain
file changed and changed back. There is a second exclusion sitting beside it, drawn by the method
revision 6 says it abandoned, and the spec is silent on it. A reader who takes revision 6 at its
word will believe the sweep covers everything under the target bar one case. It does not.

**Why it does not block.** Reaching it needs a manifest action or a generator that writes inside
`.git`, which is both unusual and squarely inside flagged concern F1 — the engine runs command
strings a pack supplies, and a pack that writes into `.git` can do far worse than leave a
placeholder. Nothing under `.git` becomes part of the emitted solution, and the exclusion earns
its keep: without it the sweep reads a mature repository's entire object database on every run.

**The fix I would make:** not removing the exclusion — recording it. One clause in R72 naming
`.git` as excluded, and a sentence in revision 6's cost paragraph saying there are two exclusions
and why they are drawn differently.

### S2 — A second artifact chain nested deeper in the target is swept as ordinary files (new)

R72's exception is scoped to *the resolved* artifact root, singular. The executor resolves one
root — the default chain directory, or a charter found one or two directories down — and every
other chain in the tree is swept like any scaffolded file. A monorepo whose sub-packages keep
their own chains therefore halts on a sub-package's own paperwork, which is exactly what the
second round blocked on for this repository's single chain:

```
$ ...Invoke-ScaffoldManifest.ps1 -Manifest pack/nested.json -Kind demo -Target probe_nested
action 1 (mkdir) created src
token {{SOLUTION}} survives in: packages/a/b/.sdlc/spec.md
EXIT=1
```

**The executor is faithful to R72 here** — the gap is in the requirement, which assumes one chain
per repository. **It also fails safe:** a false halt, not a silent pass, and the message names the
file so the cause is obvious.

**Why it does not block.** Nothing in this change or in the charter contemplates more than one
chain in a repository, and the failure is loud. It belongs in the charter's known-and-unchecked
list rather than in code.

### S3 — Four test comments cite requirements that have been superseded, one of them twice over (new, documentation)

The five rows that hold the placeholder sweep are correct and I watched them discriminate. Their
comments are not:

- `ScaffoldManifestTests.cs:836`, `:862`, `:870`, `:943` each open "R70, first/second/third of
  three" — R70 was superseded two revisions ago and is struck through in the spec, so a reader
  who follows the citation lands on a requirement marked dead.
- `ScaffoldManifestTests.cs:687` explains the guard on what `rewrite` and `delete` may touch as
  "the decision behind R71 keeping a second set rather than widening R41's". **That second set no
  longer exists** — it was deleted this round — so the comment describes a design that is gone,
  attached to the one test that holds the bound which survived it.

**Why it does not block.** No behaviour depends on a comment. **Why it is worth fixing anyway:**
this repository's whole premise is that a fresh session continues from the artifact and the code
alone, and its own plain-language rule is that a citation carries what it says. Line 687 is the
one to fix first — it is actively misleading about the design a future reader is most likely to
want to change.

### S4 — The non-vacuity guard on the pack-asset check is correct and is not itself falsifiable (new)

The lead asked what change would make each new check fail and whether anyone would plausibly make
it. For `EveryPackAsset_IsDeclaredByItsManifest` — the pack's manifest and its `assets/` directory
must list the same files in both directions — the answer splits in two.

**The check itself is a real instrument.** Fourteen assets are declared and exactly those fourteen
are on disk; I compared them by hand. It fails on two changes people really make: adding an asset
file without giving it a manifest action, and renaming or moving an asset the manifest still
names.

**The guard added this round is not.** `compared > 0` fires only on a manifest that declares no
assets at all, and nobody ships that. It is the shape the lead warned about — a check asserting
the state its author just created. It costs one line and it converts a degenerate silent pass
into a loud failure, so keep it; just do not count it as coverage.

### S5 — The two new sample-removal rows are genuine instruments (new, no action)

`EverySampleRemovalFiredAndTheSampleIsGone_Web` and its competition twin close the second round's
S12 and the fifth round's S4 — **R26**, the manifest removes every sample page, component,
endpoint and data type the generators produce. Three assertions, and I asked the lead's question
of each:

- **Every `delete` the manifest declares reported `removed`.** Fails when an SDK release renames a
  sample file so a glob matches nothing. That is the failure the fifth round described, template
  renames happen, and until this round nothing said a word about it.
- **Each known sample artefact is absent from the emitted tree.** Fails when a glob is widened by
  accident and removes something other than what it aimed at.
- **The declared removal count is at least the known-sample count.** Fails when somebody deletes a
  `delete` action from the manifest. The web kind declares five removals for five known samples
  and the competition kind two for two, so this guard sits exactly on its boundary rather than
  slack.

This is the one new check I would call load-bearing. No action.

### S6 — The change-and-change-back divergence the spec records is real and benign (new, no action)

Spec revision 6 states, as the price of a content-based rule, that a chain file this run changes
and changes back to byte-identical content reads as untouched. **It is correctly described and it
cannot cost anything.** I ran it: a generator that writes the placeholder into a chain file and
then restores the original bytes leaves exit 0 and no placeholder on disk. The reason is general
rather than particular to my fixture — if the end content equals the start content, then a
placeholder present at the end was present at the start, which is precisely the pre-existing
chain paperwork the exception exists for. Under an authorship rule that case would **halt on the
engine's own documents**, which is the defect R50 was superseded for.

So the divergence is worth stating for a reader's model of the rule, and it is not a cost. Saying
so because the spec presents it as one, and a later reader deciding whether to tighten the rule
should know the tightening buys nothing.

### S7 — `unless_exists` binds every action kind, and only one kind is tested (carried, third round)

**R43** — where an action declares `unless_exists`, the executor skips it when that path is
present, for every action kind and not only for `run`. The implementation is right and I
re-checked it: the test sits at the top of the action loop before the switch on kind
(`Invoke-ScaffoldManifest.ps1:880-891`), so it binds all six by construction.

No test holds that. `SecondExecution_ChangesNoFileAndExitsZero` declares `unless_exists` on a
`run` action only; the other five kinds reach `skipped` through their own idempotence. Move the
test inside the `run` branch and the suite stays green.

**Why it still does not block**, and the third, fourth and fifth rounds each reached the same
conclusion: for the other five kinds `unless_exists` is a convenience rather than what makes a
re-run safe, and each of those refusals is separately tested. The fix is one fixture row with a
sentinel on an action of each kind.

### S8 — A refusal creates the target directory and leaves it behind (carried, second round)

Confirmed again by running it. The executor creates a missing target
(`Invoke-ScaffoldManifest.ps1:650`) before the approval gate, then refuses for want of a chain,
leaving an empty directory. **R38 and R39** — the executor reads the charter's status and refuses
to write anything without an approved one — say "write nothing", and no *file* is written. An
empty directory is cosmetic. Fix by creating the target after the gate, or removing one the
refusal created.

### S9 — The report's version has no consumer outside the test that asserts it (carried, fifth round)

**R61** — the report carries a schema version and a consumer rejects a version it does not know.
The first half is tested against the real script. The second is tested against a helper the same
test class wrote. The production consumer is the setup skill, and its body still never mentions
`report_version`: I grepped it. Not blocking — the skill is prose and human-verified by design,
and one report version exists — but one sentence in the skill body would give the requirement a
consumer outside its own test.

### S10 — The check that holds "the manifest installs nothing" reads `run` actions only (carried, fifth round)

`NoManifest_InstallsItsOwnPrerequisite` holds **R30** — a missing prerequisite fails with the
pack's own instruction and nothing is installed. It scans the arguments of `run` actions for the
install verb and does not scan preflight commands (`ScaffoldPrerequisiteTests.cs:257`
filters to `kind == "run"` before looking). A manifest that installed from a preflight would
pass, and the preflight is the *first* thing that runs, so it is if anything the more attractive
place to put one. Unchanged since the fifth round.

### S11 — Two start-up refusals emit no report (carried, fifth round)

A `-Target` that is not a usable path, or that names a file, exits 1 at
`Invoke-ScaffoldManifest.ps1:639` and `:647` with a message on stderr and no JSON on stdout.
**R56** binds "completes **or** halts" and **R20** has the setup command relay the report, so on
those two paths there is nothing to relay. Reachable only by passing a garbage path.

### S12 — The fourth round's recommendation about a chain root declared as `.` is still unrecorded (carried, fourth round)

The fourth round judged, and the fifth agreed, that a charter declaring its artifact root as `.`
is acceptable to leave unguarded because it needs **two charters in one repository disagreeing
about where the chain lives** before the run starts at all — and recommended recording it in the
charter's known-and-unchecked list *with that precondition stated*, because without the
precondition the limitation reads far worse than it is. The list still holds only the unbumped
plugin version and the never-run eval suite. R72 improves the case again — with the root resolved
to the target, the whole tree becomes content-gated, so the sweep degrades to "what did this run
change" rather than "what did this run create" — which is one more reason the one-line record is
now the only thing outstanding.

### S13 — The new checks have no row in the charter's silent-failure table (new, documentation)

The charter says every row in that table is mutation-tested and that the table enumerates the
silent-failure classes this repository knows about. Three checks added over the last two rounds
have no row: `EveryPackAsset_IsDeclaredByItsManifest` and `EngineInstructions_NameNoPackFile`
(which hold **R47** — the manifest is the declaration of every file the pack scaffolds) and the
two sample-removal rows above. The last of those is the sharpest omission: *generator sample
content quietly stops being removed* is a named silent-failure class that now has an instrument
and no entry.

---

## Requirement coverage

Live requirements only. **Nine are struck through in the spec and correctly claimed by nobody:**
R6 (the report outcome set with no `mutated` or `removed`), R8 (the placeholder rule bound to
files this run wrote), R11 (the `required` rewrite that contradicted re-runnability), R14 (the
two-argument invocation), R33 (the integration criterion a zero-test suite could satisfy), R50
(the sweep of every file under the target), R69 (the unconditional chain carve-out), R70 (the
carve-out that said "created" where it meant "wrote to") and now **R71** (the carve-out that
named two mechanisms where the list is open). **Sixty-three live.**

| Req | What it says | Implemented | Tested | Note |
|---|---|---|---|---|
| R1 | every pack ships a scaffold manifest | yes | yes | `EveryPack_ShipsAScaffoldManifest`, guarded against an empty pack list |
| R2 | the engine names no string a pack owns | yes | yes | both invariant-I1 checks read one shared term list, which carries the file extension and the placeholder spelling |
| R3 | actions run in order, halting at the first failure | yes | yes | asserts the later action did not run |
| R4 | a path outside the target is refused, nothing written | yes | yes | one row per action kind |
| R5 | an existing destination is skipped and left byte-identical | yes | yes | |
| R7 | a second run changes no file and exits 0 | yes | yes | fixture manifest, and the real pack per kind by tree hash including the chain |
| R9 | preflight runs first; on failure nothing is written | yes | yes | relays the pack's remediation verbatim |
| R10 | `unless_exists` skips the action (restated as R43) | yes | partial | see R43 |
| R12 | an unknown or missing kind lists the declared kinds | yes | yes | plus an unknown manifest version refused before any action |
| R13 | no scaffold action while the charter is draft | yes | n/a | the setup command's prose; the substantive half is machine-checked by R38 |
| R15 | stop and name the missing answer when the kind or the boundaries are unsettled | yes | no | prose, human-verified by design |
| R16 | a re-run offers charter amendment and gap-filling | yes | no | prose; the gap-filling half stays unblocked — a chain document quoting the placeholder does not halt the run, and I re-ran that case |
| R17 | derive the identifier from the directory name, present it for confirmation | yes | yes | derivation tested per case; the confirmation half is prose |
| R18 | an underivable candidate is refused, never repaired | yes | yes | empty and leading-digit rows |
| R19 | recover the identifier from an existing solution file, never re-derive | yes | yes | |
| R20 | relay the report verbatim; never claim the repo is scaffolded after a non-zero exit | yes | no | prose |
| R21 | the web kind emits the frozen set of projects | yes | yes | `EmittedLayoutIsTheFrozenOne_Web`, expectation stated in the test rather than read from the manifest |
| R22 | the competition kind emits its frozen set | yes | yes | `EmittedLayoutIsTheFrozenOne_Competition` |
| R23 | emitted test and host names match a coverage exclusion | yes | yes | patterns parsed from the emitted settings file, with a guard against holding nothing |
| R24 | the emitted README carries build, test, local-run, chain location, the authority invariant | yes | yes | expectations stated in the test rather than lifted from the asset |
| R25 | a base ignore file alongside the Terraform entries | yes | yes | both sets asserted, so one cannot stand in for the other |
| R26 | sample content removed; at least one executable test left | yes | yes | **upgraded this round** — every declared removal must report `removed`, each known sample must be absent, and the declared count is held against the known count |
| R27 | version attributes stripped from every package reference | yes | yes | asserted over the emitted projects, plus the restore |
| R28 | the pack pins every package its generators emit | yes | yes | the restore is the only instrument, and the spec says so |
| R29 | an inline SDK version rewritten to the pinned one | yes | yes | via the rewrite's `assert_after` and the build |
| R30 | a missing prerequisite fails with the pack's instruction; nothing is installed | yes | partial | the instrument reads `run` actions and not preflights — should-fix S10 |
| R31 | the emitted solution builds with exit 0 | yes | yes | per kind, real SDK |
| R32 | the unit suite reports a count above zero and no failures | yes | yes | count read from a TRX file, not from console text |
| R34 | a partial repository is filled, everything else byte-identical | yes | yes | `tests/` deleted and restored, the rest hashed |
| R35 | invariant I1 still holds | yes | yes | both I1 checks |
| R36 | the repository README describes what setup produces | yes | yes | layout read out of each manifest, so the README tracks what is emitted |
| R37 | CI installs a prerequisite the manifest declares and refuses to install | yes | yes | workflow and manifest parsed and held against each other; the step's own name carries the prerequisite, not a comment |
| R38 | the executor reads the charter's status and refuses a draft | yes | yes | non-zero, empty tree |
| R39 | no charter means refuse | yes | yes | both "chain without charter" and "no chain" |
| R40 | the resolved chain root is named in the report | yes | yes | tested against a non-default root |
| R41 | `rewrite` and `delete` touch only paths this run created | yes | yes | **re-established this round by mutation** — folding appended paths into that set deletes a pre-existing ignore file, and the guard test goes red |
| R42 | `assert_after` failing fails the run naming the file | yes | yes | |
| R43 | `unless_exists` binds every action kind | yes | partial | tested on `run` only — should-fix S7 |
| R44 | a `rewrite` or `delete` glob outside the target is refused | yes | yes | |
| R45 | `append` creates the file, or adds only the lines not present | yes | yes | including a non-ASCII asset surviving a re-run |
| R46 | `append` with every line present is a no-op recorded as skipped | yes | yes | |
| R47 | the manifest declares every file the pack scaffolds | yes | yes | fourteen assets declared and exactly those fourteen on disk; the vacuity guard is assessed in should-fix S4 |
| R48 | the setup command writes the charter and nothing else | yes | yes | instrumented by the engine-instruction scan |
| R49 | the pack's skill cites the manifest and carries no competing list | yes | yes | `PackSkillCitesItsManifest` |
| R51 | a supplied identifier wins over derivation and recovery | yes | yes | |
| R52 | an invalid supplied identifier is refused | yes | yes | |
| R53 | more than one solution file refused; none falls back to derivation | yes | yes | |
| R54 | the executable scan reuses the same term list | yes | yes | one shared member, read by both instruments through one matching function |
| R55 | a stack term in a shipped engine executable fails a named check | yes | yes | `EngineExecutablesImposeNoStack` |
| R56 | every action recorded with an outcome and its path or command | yes | yes | all five outcomes asserted present, plus the two that can match nothing; two start-up refusals are outside it — should-fix S11 |
| R57 | a preflight declared inside actions is rejected | yes | yes | by kind and by field |
| R58 | a rewrite records each file it changed as mutated | yes | yes | |
| R59 | failures, mutations and removals shown in full, never a count | yes | no | prose |
| R60 | the executor is invoked with all four arguments | yes | no | prose; the command line is frozen in the plan and driven by two test classes |
| R61 | the report carries a schema version and a consumer rejects an unknown one | yes | partial | see should-fix S9 |
| R62 | a starter test asset that asserts a property of the emitted solution | yes | yes | via the suite count and the emitted layout |
| R63 | `run` starts inside the target; a `cwd` outside it is refused | yes | yes | |
| R64 | the integration suite runs and reports a count, or compiles and says it was not run | yes | yes | the run path was exercised on this machine; the compile-only branch is in **Not checked** |
| R65 | an absent prerequisite fails rather than skips | yes | yes | `NoRowInThisClassCanSkip`, and the suite reported 0 skipped |
| R66 | restore succeeds with transitive pinning on | yes | yes | the setting is asserted present so the restore proves the larger surface |
| R67 | the competition kind applies the machine-learning profile | yes | yes | and asserts the web kind does not |
| R68 | the notebook is headings and a solution reference only | yes | yes | no code cell, no recorded output, no fenced block |
| **R72** | **a chain file is swept unless it was there before the run with its content unchanged** | **yes** | **yes** | **five rows over two chain roots, including the generator-modifies-a-pre-existing-file row that distinguishes it; mutation-tested by hand. Two divergences from its sentence — should-fix S1 and S2** |

**Fifty-four fully tested.** Five are prose the setup command owns and are human-verified by
design (R15, R16, R20, R59, R60); one is prose whose substantive half is machine-checked
elsewhere (R13); three are partial — R10 and R43, the skip-if-present field tested on one action
kind; R30, the install-verb scan reading `run` actions only; and R61, whose consumer is the
suite's own helper.

---

## Manifest drift

`git diff --name-only` plus untracked files, against the plan's frontmatter slice manifest.

**Every file the manifest claims was touched.** All twenty are present — the executor script and
its two test files, the pack's manifest, skill and six assets, the setup skill, the structure
checks, the build-tests class, and the release slice's six. `.sdlc/CHARTER.md` does not appear in
`git status` because its amendment is already committed; the most recent commit also updated one
row of its silent-failure table to describe the content-based sweep. That is the release slice
having landed, not a slice that did not.

**Two files beyond the manifest, and both are the recorded, accepted drift:**

| File | Status |
|---|---|
| `plugins/stack-dotnet/skills/stack-dotnet/assets/competition.readme.md` | accepted 2026-09-18 — the orchestration-versus-infrastructure invariant R24 mandates is meaningless in a competition repo, so one README asset could not honestly serve both kinds |
| `plugins/stack-dotnet/skills/stack-dotnet/assets/web.IntegrationStarterTests.cs` | accepted 2026-09-18 — the generated sample integration test has zero live tests and a property that fails compilation under warnings-as-errors, so R26 and R64 contradict each other without a replacement |

Both sit inside `plugins/stack-dotnet/`, which no other slice touches, so disjointness was never
at risk. Reporting them is the check working; the owner accepted them in the plan's dated
post-approval section. `assets/dotnet.gitignore`, listed there for completeness, is in the
manifest and is not drift.

**One artifact file is modified and is not code drift:** `.sdlc/init-scaffolds-solution/spec.md`
carries an uncommitted paragraph added after the revision-6 commit, stating the
change-and-change-back property of a content-based rule. That is the chain being written, not a
slice overreaching. It is the paragraph should-fix S1 asks to be corrected.

**No unplanned file.** Nothing outside the manifest and those two.

---

## Build and test output

The whole suite, real output:

```
$ dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo

  Determining projects to restore...
  All projects are up-to-date for restore.
  AiSdlc.Tests -> C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll
Test run for C:\Repos\ai-sdlc\tests\AiSdlc.Tests\bin\Debug\net10.0\AiSdlc.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   143, Skipped:     0, Total:   143, Duration: 6 m 6 s - AiSdlc.Tests.dll (net10.0)
```

**Read as counts, not as an exit code: 143 run, 0 failed, 0 skipped.** Four more than the fifth
round — the two chain rows for the generator case over two chain roots, and the two
sample-removal rows.

**The integration suite really ran on this machine rather than taking the compile-only downgrade
R64 allows**, which is what makes a green run mean what the spec's end-to-end section says it
means. Re-run with the rows' own output visible, together with the two new sample-removal rows:

```
$ dotnet test ... --filter "FullyQualifiedName~EmittedSuitesReportATestCount|FullyQualifiedName~EverySampleRemovalFired" -l "console;verbosity=detailed"

 web: scaffolded into C:\Users\peter\AppData\Local\Temp\aisdlc-scaffold-54e13b09...\probe-repo
 web: container runtime detected (docker)
 web / Probe.IntegrationTests: ran 1 test(s), 0 failed
 web / Probe.UnitTests: ran 2 test(s), 0 failed
 competition: scaffolded into C:\Users\peter\AppData\Local\Temp\aisdlc-scaffold-d9e13876...\probe-repo
 competition: container runtime detected (docker)
 competition / Probe.UnitTests: ran 2 test(s), 0 failed

 web: 5 declared removal(s), 5 recorded as removed, and 5 known sample artefact(s) absent from the tree
 competition: 2 declared removal(s), 2 recorded as removed, and 2 known sample artefact(s) absent from the tree
```

A machine created two solutions from nothing in that run, compiled both, and watched their own
tests execute — the integration suite included, against a started container. Every removal the
pack declares fired on something, and every sample artefact the generators produce is gone from
both trees: that is the fifth round's S4 finding, *removal of generator sample content is
reported but never measured*, closed with numbers rather than with a claim.

### Fault injection: the fifth round's defect, against a mutated copy of the shipped executor

The lead asked me to judge R72 as a rule rather than accept it as a patch. The strongest evidence
I can offer is that the shipped script and a copy of it carrying the *previous* rule disagree on
exactly the case the fifth round reproduced. Same fixture, same manifest, same command — a `run`
action whose generator appends the placeholder to a chain file that was already on disk. The
placeholder is assembled inside the command so that the executor's own substitution cannot reach
it.

The mutation removes R72's content comparison and restores the earlier rule, *a chain file that
existed before the run is never swept*:

```
=== SHIPPED ===
EXIT=1
action 1 (run) powershell.exe -NoProfile -Command "$t='{'+'{SOLUTION}'+'}'; Add-Content ..."
token {{SOLUTION}} survives in: .sdlc/notes.md

=== MUTANT (the pre-R72 rule) ===
EXIT=0
scaffold completed for kind 'demo' in ...\probe_r72_mut
$ cat probe_r72_mut/.sdlc/notes.md
orig
assembly = {{SOLUTION}}
```

### Fault injection: the bound on what a rewrite and a delete may touch

This is the risk the lead named for this round — that deleting one of the two tracking sets
loosened what `rewrite` and `delete` can reach. The mutation puts appended paths back into the
created set, which is the merge the implementer was steered towards and refused. Fixture: a
pre-existing `kept.gitignore` holding one line, a manifest that appends to it and then declares a
`rewrite` and a `delete` whose globs both match it.

```
=== SHIPPED ===
EXIT=0
$ ls probe_bound_real           -> kept.gitignore
$ cat probe_bound_real/kept.gitignore
mine
*.tmp

=== MUTANT (append registers as created) ===
EXIT=0
$ ls probe_bound_mut            -> (empty)
$ cat probe_bound_mut/kept.gitignore
(gone)
```

A mature repository's ignore file is removed because the pack added two lines to it — which is
exactly the harm the created-set bound exists to prevent. `AFileThisRunOnlyAppendedTo_IsNotTheRunsToRewriteOrDelete`
asserts both the file's survival and its original line, so it goes red under the mutant. The
removal of the second set changed nothing about this.

### Behaviours I drove the shipped executor for by hand

```
# R72 - a preflight command modifying a pre-existing chain file (a path nothing registers)
#   -> EXIT=1, "token {{SOLUTION}} survives in: .sdlc/notes.md"
# S1  - a generator writing the placeholder into .git/probe.txt
#   -> EXIT=0, placeholder left on disk
# S2  - a second chain three directories down, its spec quoting the placeholder
#   -> EXIT=1, "token {{SOLUTION}} survives in: packages/a/b/.sdlc/spec.md"
# S6  - a generator writing the placeholder into a chain file and restoring the original bytes
#   -> EXIT=0, and no placeholder anywhere on disk
```

### What I read rather than ran

Every write path in the executor, to satisfy myself that R72 covers the open set the earlier
rules kept mis-enumerating. The pre-run snapshot is taken before preflight
(`Invoke-ScaffoldManifest.ps1:818`), so preflight output is inside the rule too — which closes the
narrower sibling the fifth round named. `Get-TargetFile` is the single file walk used by the
snapshot, by the generator-output registration and by the sweep, so the three cannot disagree
about what exists; its one exclusion is `.git`, which is should-fix S1. `Test-UnderArtifactRoot`
is the single definition of where the chain is, read by the snapshot and by the sweep.

---

## Has this converged — the lead's question, answered directly

**What I would fix before shipping: nothing.** No finding this round changes an outcome for a
realistic input.

**What I would fix next, in order.** First, the spec sentence in should-fix S1 — revision 6
claims one exclusion and the code has two, and this document has been revised five times
precisely because its sentences and its rationales kept describing different sets. Correcting a
completeness claim costs a clause. Second, the comment on line 687 of the executor tests
(should-fix S3), which describes a design that was deleted this round. Third, the two one-line
records: the chain-root-declared-as-`.` limitation the fourth round asked for (S12) and the
missing rows in the charter's silent-failure table (S13).

**What I would carry.** Everything else: the skip-if-present field tested on one action kind
(S7), the empty directory a refusal leaves behind (S8), the report version with no consumer
outside its own test (S9), the install-verb scan reading `run` actions only (S10), the two
start-up refusals that emit no report (S11), and the nested-chain case (S2). Each has been looked
at by two or more rounds and left alone with reasons; that is a decision, not a backlog.

**Would a seventh round find something? Probably yes, and that is no longer the right question.**
A fresh adversarial reader will always find another hole in a twelve-hundred-line spec and a
twelve-hundred-line script. What matters is the size, and the size has changed by an order of
magnitude. The first round found the report omitting two of its five outcomes and two
requirements with no instrument at all. The second found the CI prerequisite unimplemented and a
sweep that made this repository impossible to re-scaffold. The third, fourth and fifth each found
the same boundary specified one mechanism short. This round I found a location-based exclusion of
`.git` that needs a pack to write inside a git directory, and a monorepo shape nothing in the
charter contemplates — both of which fail in the safe direction or not at all.

**My judgement: this has converged, and the reason is structural rather than a count of rounds.**
The four failed revisions failed because they enumerated an open set. R72 does not enumerate, and
I tested that claim against the two mechanisms that beat its predecessors (a generator modifying a
pre-existing file, and a preflight command doing the same) rather than taking it on the argument.
The remaining gaps are in what the spec *says* about its own rule, not in what the rule *does* —
and a seventh round spent on that is worth less than the same effort spent on the first push,
where the one genuinely unproven thing still sits.

---

## Not checked

- **Mutation-testing the checks that hold R47 and R26 by injecting a fault into the repository.**
  Doing it properly means adding an undeclared asset file, naming a pack file in an engine skill,
  or breaking a removal glob, and this verification was instructed to modify no file but this
  one. For the sweep and for the created-set bound I got around that by mutating *copies* of the
  shipped script in a scratch directory and driving them against my own fixtures, which is
  transcribed above. For the structure and build checks I established non-vacuity by reading and
  by comparing the declared and on-disk asset sets by hand. That is weaker than watching them
  fail, and I am saying so rather than implying otherwise.
- **The compile-only branch of R64.** This machine has a container runtime, so the downgrade path
  could not be exercised short of hiding it from the process. Unchanged since the third round.
- **CI on a hosted runner.** I did not push. Whether `dotnet new install
  Aspire.ProjectTemplates::13.5.3` succeeds on `windows-latest` and the web kind's preflight then
  passes is still unproven, and it is the scenario R37 exists for. **Still the first thing to
  watch on the first push, and now the only substantial unknown left in this change.**
- **A clean machine without the Aspire templates.** The refusal path is covered by fixture tests;
  the real pack's preflight against a machine that genuinely lacks them was not exercised.
- **The human-verified half of S10 and all of S11** — that the setup command calls the executor at
  the right point in its own prose, and that declining the module-boundary question stops the
  command rather than selecting a kind. I read the skill body: it settles the kind, the boundaries
  and the identifier before the charter, writes the charter and nothing else, invokes the executor
  after approval with all four arguments, and spells out the relay rules including the prohibition
  on summarising changed and removed paths into a count. I did not run `/sdlc-core:init` by hand.
- **Whether a manifest could escape the target through a directory junction.** Containment compares
  resolved path prefixes, which does not follow a junction. Inside the trust boundary flagged
  concern F1 places at the pack.
- **A locked or unreadable file during the sweep.** The sweep skips a file it cannot read and says
  nothing, so a placeholder in a file something else has open would pass. Unavoidable, and noted
  because silence reads as coverage. The pre-run snapshot fails in the safe direction for the same
  case: a chain file it cannot hash is left out and is therefore swept.
