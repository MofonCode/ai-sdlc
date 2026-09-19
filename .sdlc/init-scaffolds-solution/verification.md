---
slug: init-scaffolds-solution
status: draft
spec: ./spec.md
plan: ./plan.md
verdict: do-not-ship
verified: 2026-09-18
requirements_total: 63
requirements_tested: 43
---

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
