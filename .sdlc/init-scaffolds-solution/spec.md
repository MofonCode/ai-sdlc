---
slug: init-scaffolds-solution
status: approved
intent: ./intent.md
tier: feature
---

# spec — init produces a solution you can build

## Requirements

Numbered `R<n>` and never renumbered; `plan.md` and the test suite cite these. Each maps to
one of the intent's success criteria (S1–S11) in the coverage table under **Test strategy**.

### The scaffold manifest and its executor

- **R1.** THE stack pack SHALL declare everything it scaffolds — preflight checks, directories,
  commands, asset copies and post-generation rewrites — in a single machine-readable manifest
  file shipped inside the pack.
- **R2.** THE engine SHALL name no directory, project, assembly, command, template, package or
  file extension belonging to any pack; every such string SHALL originate in a manifest the
  pack ships.
- **R3.** WHEN the executor is given a manifest and a target directory, THE executor SHALL
  perform the manifest's actions in declared order and stop at the first action that fails.
- **R4.** IF an action would write to a path that resolves outside the target directory, THEN
  THE executor SHALL refuse the action, write nothing, and exit non-zero naming the path.
- **R5.** IF a destination file already exists, THEN THE executor SHALL skip that action,
  leave the file byte-identical, and record it as skipped.
- **R6.** ~~WHEN the executor completes, THE executor SHALL emit a machine-readable report
  listing every action as `created`, `skipped` or `failed`, with the path or command for
  each.~~ **Superseded by R56** — the outcome set was missing `mutated` and `removed`, so a
  rewrite or a delete left no trace in the only record R20 puts on the owner's screen.
- **R7.** WHEN the executor runs a second time over its own output, THE executor SHALL change
  no file and exit 0.
- **R8.** ~~THE executor SHALL substitute every token declared in the manifest before a file is
  written, and IF any `{{`-delimited token remains in a file the executor wrote, THEN THE
  executor SHALL exit non-zero naming the file and the token.~~ **Superseded by R50** — bound
  to files this run wrote, it never inspects a token left behind by a previous run, which is
  the intent's opening complaint.
- **R9.** WHERE a manifest declares a preflight check, THE executor SHALL run every preflight
  check before any other action, and IF a preflight check fails, THEN THE executor SHALL write
  nothing at all and exit non-zero, relaying the remediation message the manifest supplies.
- **R10.** WHERE a manifest action declares an `unless_exists` path, THE executor SHALL skip
  that action when the path is present. *Restated as R43, which says explicitly that this binds
  every action kind — revision 1's design table listed the field only under `run`, and the two
  read as disagreeing.*
- **R11.** ~~WHERE a manifest declares a rewrite action, THE executor SHALL apply the pack's
  regular expression to each file matching the pack's glob, and SHALL exit non-zero if a
  rewrite declared as `required` matches nothing.~~ **Superseded by R42 and R58** — it
  contradicted R7. Both rewrites this spec declares are one-way, so on a second run nothing
  matches and `required` failed the run the spec's own S6 requires to be green.
- **R12.** THE executor SHALL select the manifest's action set from a `kind` supplied by its
  caller, and IF the caller supplies no `kind` or one the manifest does not declare, THEN THE
  executor SHALL list the declared kinds and exit non-zero without writing.

### `init`'s flow

- **R13.** WHILE the charter is `status: draft`, THE `init` skill SHALL run no scaffold action.
- **R14.** ~~WHEN the charter is approved and a stack pack was chosen, THE `init` skill SHALL
  invoke the executor with the manifest path and the kind the interview settled.~~
  **Superseded by R60** — it named two arguments, so the confirmed solution identifier had no
  declared path into the executor and the confirmation step was decorative.
- **R15.** IF the interview has not settled the solution kind and its module boundaries, THEN
  THE `init` skill SHALL stop and say which answer is missing, rather than choosing a kind.
- **R16.** WHEN `init` runs in a repository that already has an approved charter, THE `init`
  skill SHALL offer charter amendment and scaffold gap-filling as the two things it can do,
  and SHALL no longer state that it is never run again.
- **R17.** WHEN given a repository directory, THE executor SHALL derive a candidate solution
  identifier from its name by splitting on `-`, `_`, `.` and space, discarding every character
  outside `A-Za-z0-9`, capitalising each segment and joining them; and THE `init` skill SHALL
  present that candidate for confirmation before any token is substituted.
- **R18.** IF the derived candidate is empty or begins with a digit, THEN THE executor SHALL
  report it as underivable and substitute nothing, and THE `init` skill SHALL require an
  explicit answer rather than offering a repaired guess.
- **R19.** WHEN the target directory already carries a solution file the pack's manifest
  declares as its solution-file glob, THE executor SHALL take the solution identifier from that
  file's name and SHALL NOT re-derive it from the directory name.
- **R20.** IF the executor exits non-zero, THEN THE `init` skill SHALL report the executor's
  report verbatim and SHALL NOT claim the repository is scaffolded.

### What is emitted

- **R21.** WHERE the kind is the web solution, THE manifest SHALL emit a solution file, an
  Aspire AppHost, service defaults, a Blazor front end, an ASP.NET Core API, a unit test
  project and an integration test project, under the frozen layout in **Data and contracts**.
- **R22.** WHERE the kind is the competition solution, THE manifest SHALL emit a solution file,
  a feature-and-scoring library, a training runner, a unit test project and a starter notebook,
  under that same frozen layout.
- **R23.** THE manifest SHALL name every emitted test and host project so that its assembly
  name matches an exclusion pattern in `coverage.runsettings`.
- **R24.** THE manifest SHALL emit a `README.md` carrying the build command, the test command,
  the local-run command, the location of the artifact chain, and the AppHost/Terraform
  authority invariant — all supplied by the pack, none by the engine.
- **R25.** THE manifest SHALL emit a base ignore file for the stack in addition to the
  Terraform entries already appended.
- **R26.** THE manifest SHALL remove every sample page, sample component, sample endpoint and
  sample data type the generators produce, and SHALL leave at least one executable test.

### Reconciling generator output

- **R27.** WHEN a generator has produced project files, THE manifest SHALL strip every version
  attribute from every package reference in them.
- **R28.** THE pack SHALL declare a central package version for every package any generator it
  invokes emits a reference to.
- **R29.** WHEN a generator emits a project whose SDK attribute carries an inline version, THE
  manifest SHALL rewrite that version to the one the pack pins, so the pinned value is the one
  that binds.
- **R30.** IF a required template is not installed on the machine, THEN THE manifest's preflight
  SHALL fail with the pack's own installation instruction, and no install SHALL be performed.

### Verification of the result

- **R31.** WHEN the manifest for a kind is executed into an empty directory, THE resulting
  solution SHALL build with exit code 0 against the versions the pack pins.
- **R32.** WHEN the resulting solution's unit tests are run, THE run SHALL report at least one
  test executed and zero failures.
- **R33.** ~~WHERE a container runtime is available, THE resulting integration tests SHALL run
  and pass; WHERE it is not, they SHALL still compile.~~ **Superseded by R64** — a suite that
  collects zero tests runs and passes, which is I12's third failure mode written into a
  requirement.
- **R34.** WHEN the manifest is executed into a directory already holding part of its output,
  THE executor SHALL create only the missing parts and SHALL leave every pre-existing file
  byte-identical.
- **R35.** THE engine SHALL continue to satisfy charter invariant I1 after this change.

### Documentation

- **R36.** THE repository `README.md` SHALL describe what `init` produces, list the pack's
  emitted layout rather than only its config files, and remove `/init-competition` from the
  not-yet-built list.
- **R37.** WHERE the check suite executes a kind whose manifest declares a prerequisite that is
  not present on a hosted runner, THE repository's CI workflow SHALL install that prerequisite
  as an explicit step before the suite runs, and the manifest SHALL still refuse to install it
  itself.

### Added at revision 2

R38–R67 close the findings from the fresh-context attack on revision 1. Superseded requirements
are struck through above with the reason; nothing is renumbered.

**The approval gate, moved out of prose**

- **R38.** WHEN invoked, THE executor SHALL resolve the artifact root, read the charter's
  `status:` from the charter file, and IF that status is not `approved`, THEN THE executor
  SHALL write nothing and exit non-zero naming the charter path and the status it found.
- **R39.** IF no charter exists at the resolved artifact root, THEN THE executor SHALL write
  nothing and exit non-zero, because a scaffold with no approved charter behind it is the state
  R13 exists to prevent.
- **R40.** THE executor SHALL resolve the artifact root rather than assuming the default, in the
  manner the gate hook already does, and SHALL name the root it resolved in its report.

**Idempotence, and what a rewrite is allowed to touch**

- **R41.** THE executor SHALL record, for the current execution, every path it created, and
  SHALL apply a `rewrite` or a `delete` action only to paths in that set.
- **R42.** WHERE a rewrite declares `assert_after`, THE executor SHALL verify after rewriting
  that every file it created matching the rewrite's glob matches `assert_after`, and IF one does
  not, THEN THE executor SHALL exit non-zero naming the file.
- **R43.** WHERE an action declares `unless_exists`, THE executor SHALL skip that action when
  the path is present, for every action kind and not only for `run`.
- **R44.** IF a `rewrite` or `delete` action names a path that resolves outside the target
  directory, THEN THE executor SHALL refuse the action, change nothing, and exit non-zero
  naming the path.

**The seventh action kind**

- **R45.** WHERE a manifest declares an `append` action, THE executor SHALL create the
  destination file with the asset's content when the file is absent, and SHALL append only those
  of the asset's lines that the file does not already contain when it is present.
- **R46.** WHEN an `append` action finds every one of its lines already present, THE executor
  SHALL change no byte of the file and SHALL record the action as skipped.

**The executor is the only writer**

- **R47.** THE manifest SHALL declare every file the pack scaffolds, its style, build, package,
  SDK, coverage and infrastructure configuration included, so that no file the pack names is
  written by any path other than the executor.
- **R48.** THE `init` skill SHALL write no file that the pack names; the charter is the only
  file `init` itself writes.
- **R49.** THE pack's skill SHALL cite the manifest as the list of what the pack writes rather
  than carrying a second list of its own.

**Tokens**

- **R50.** ~~WHEN the executor completes, IF any file under the target directory contains a
  token the manifest declares, THEN THE executor SHALL exit non-zero naming the file and the
  token — whether or not this execution wrote that file.~~ **Superseded by R69** — "any file
  under the target" includes the artifact chain, whose own documents quote the token while
  specifying it, so the sweep halted on the engine's paperwork and made R16's gap-filling re-run
  impossible in any repository that had used this process.

**The identifier reaches the executor**

- **R51.** WHERE the caller supplies a solution identifier, THE executor SHALL use it in
  preference to derivation and in preference to recovery.
- **R52.** IF a caller-supplied solution identifier is empty, begins with a digit, or contains
  any character outside `A-Za-z0-9`, THEN THE executor SHALL refuse it and exit non-zero,
  applying the same rule to a supplied identifier that R18 applies to a derived one.
- **R53.** IF the manifest's solution-file glob matches more than one file in the target
  directory, THEN THE executor SHALL exit non-zero naming every match; IF it matches none, THEN
  THE executor SHALL fall back to derivation.
- **R60.** WHEN the charter is approved and a stack pack was chosen, THE `init` skill SHALL
  invoke the executor with the manifest path, the kind the interview settled, the target
  directory, and the solution identifier the owner confirmed.

**The report**

- **R56.** WHEN the executor completes or halts, THE executor SHALL emit a report recording
  every action with one of the outcomes `created`, `skipped`, `mutated`, `removed` or `failed`,
  the path or command it applied to, and the resolved artifact root and target directory.
- **R61.** THE report SHALL carry a schema version, and a consumer SHALL reject a version it
  does not know.
- **R59.** WHEN `init` relays the executor's report, THE `init` skill SHALL show every `failed`
  entry with its reason and every `mutated` and `removed` path, and SHALL NOT summarise them
  into a count.

**What `run` can and cannot be bounded to**

- **R63.** THE executor SHALL run every `run` action with its working directory set inside the
  target directory, and SHALL refuse a `run` action whose declared `cwd` resolves outside it.
  THE spec SHALL state that this bounds where a generator starts and not what it can reach.

**Leaving a real test behind**

- **R62.** THE pack SHALL ship a starter test asset that asserts a property of the emitted
  solution, and THE manifest SHALL emit it, so that R26's sample-content deletion cannot reduce
  the suite to zero tests.

**Preflight shape**

- **R57.** THE manifest SHALL carry each kind's preflight checks in an array separate from its
  actions, and THE executor SHALL reject a manifest that declares a preflight check inside the
  actions array.

**Rewrites, restated**

- **R58.** WHERE a manifest declares a rewrite action, THE executor SHALL apply the pack's
  regular expression to each file it created during the current execution that matches the
  pack's glob, and SHALL record each file it changed as `mutated`.

**The I1 instrument**

- **R54.** THE check suite SHALL scan every shipped engine executable for stack terms with the
  same term list the existing skills-and-agents check uses, so that R2 has an instrument rather
  than a claim.
- **R55.** IF a shipped engine executable contains a stack term, THEN THE named check SHALL
  fail, and that check SHALL be mutation-tested like every other row in the charter's table.

**Counting what ran**

- **R64.** WHERE a container runtime is available, THE resulting integration tests SHALL run,
  report at least one test executed, and report zero failures; WHERE it is not, they SHALL
  compile, and the check SHALL record that they were not run.
- **R65.** IF a prerequisite a check needs is absent, THEN THE check SHALL fail rather than be
  skipped, except for the container runtime of R64, whose absence SHALL be named in the output.

**Documentation and scope**

- **R66.** THE pack's `Directory.Packages.props` SHALL satisfy central package management with
  transitive pinning enabled, which is a larger surface than the packages generators name
  directly.
- **R67.** WHERE the kind is the competition solution, THE pack SHALL apply the profile whose
  machine-learning package pins that kind's emitted projects require.

**The starter notebook's content — settled by the owner, 2026-09-18**

- **R68.** THE starter notebook SHALL contain section headings and a reference to the emitted
  solution only, and SHALL contain no executable scoring, cross-validation, feature-engineering
  or other domain logic, so that the one emitted file outside the analyzer gate carries nothing
  that could be wrong and nothing a permanent non-goal forbids.

### Added at revision 3

R69 closes the finding from the second fresh-context verification: the token sweep halted on
this repository's own artifact chain, so the gap-filling re-run R16 exists to allow was
impossible in any repository that had ever used this process.

- **R69.** ~~WHEN the executor completes, IF any file under the target directory **other than
  those under the resolved artifact root** contains a token the manifest declares, THEN THE
  executor SHALL exit non-zero naming the file and the token — whether or not this execution
  wrote that file. THE executor SHALL NOT sweep the artifact root.~~ **Superseded by R70** —
  the carve-out was unconditional, and it rested on "no manifest action writes into the chain",
  which nothing enforces.

**Why the carve-out is not a weakening.** R50 said "any file under the target" deliberately:
a token surviving from an earlier run is the intent's opening complaint, and binding the sweep
to files this run wrote is exactly what made R8 useless. That reasoning is unchanged for
everything a pack writes.

What it did not anticipate is that the artifact chain lives under the target too, and that the
chain's own documents quote the token while *specifying* it — this spec contains it four times.
So the sweep halted on the engine's own paperwork rather than on a defect.

The chain is the engine's territory, not scaffolded output: no pack names it, no manifest
action writes into it, and nothing in it is ever token-substituted. Excluding it removes
nothing the sweep was for.

**The gate hook already follows this rule**, which is what makes the carve-out consistent
rather than a special case: it resolves the artifact root and then ignores any path inside it,
on the same grounds. The executor already resolves that root for R38 and R40, so the value is
in hand and no new resolution logic is introduced.

*Checkable: a token inside the artifact root does not halt the run, AND the same token in a
scaffolded file still does.* Both halves are required — a test proving only the first would
pass on an executor that had stopped sweeping altogether.

### Added at revision 4

R70 closes the finding from the third fresh-context verification: revision 3's carve-out moved
part of the defect it was fixing rather than removing it.

- **R70.** ~~WHEN the executor completes, IF any file under the target directory contains a token
  the manifest declares, THEN THE executor SHALL exit non-zero naming the file and the token —
  whether or not this execution wrote that file — EXCEPT that a file under the resolved
  artifact root SHALL be swept only when this execution created it.~~ **Superseded by R71** —
  it said "created" where its own rationale said "answerable for what it wrote", and `append`
  writes to a file it did not create.

**What revision 3 got wrong.** R69 excluded the artifact root unconditionally, and justified it
on the grounds that the chain is the engine's territory: *no pack names it, no manifest action
writes into it.* The first half is a fact about the engine. The second is an assumption about
every pack that will ever exist, and nothing enforces it. A manifest that copied an asset into
the chain without declaring `tokens` would leave a token on disk and exit 0 — which is R50's
defect exactly, one directory in, and now with a requirement authorising the silence.

This spec has now made the same mistake twice in the same place, which is worth stating rather
than quietly correcting: R50 drew the boundary by *location* and caught the engine's own
paperwork; R69 drew it by *location* again and stopped catching a pack's mistake. **The boundary
is authorship, not location** — the executor is answerable for what it wrote and for nothing
else, and it already tracks that set for R41.

*Checkable in three parts, all required:* a token in a chain file this run did **not** create
does not halt it; a token in a chain file this run **did** create halts it; a token in a
scaffolded file outside the chain still halts it. The first two together are what distinguish
this from R69 — a test proving only the first would pass on the code R70 replaces.

### Added at revision 5

R71 closes the finding from the fourth fresh-context verification: R70's sentence and R70's
own rationale described different sets, and the sentence was the narrower one.

- **R71.** ~~WHEN the executor completes, IF any file under the target directory contains a token
  the manifest declares, THEN THE executor SHALL exit non-zero naming the file and the token —
  whether or not this execution wrote that file — EXCEPT that a file under the resolved
  artifact root SHALL be swept only when this execution **wrote to** it, whether by creating it
  or by appending to it.~~ **Superseded by R72** — `append` was not the only unregistered
  write path. A `run` action's generator modifying a chain file that already existed writes
  bytes nothing registers, and the fourth enumeration of mechanisms came up short exactly as
  the first three did.

**This is a one-word correction to R70, not a new idea.** R70 already located the boundary
correctly — *the executor is answerable for what it wrote* — and then wrote "created" in the
requirement sentence. `append` writes to a file that is already on disk and never creates one,
so an appended file under the chain was never swept. Reproduced with an identical manifest and
asset, varying only whether the destination existed: absent, the run halts on the surviving
token; present, it exits 0 and the token stays.

The realistic shape is a pack appending a section to the charter, which `init` always writes
first — so the append path is taken on the very first run rather than on some unlikely later one.

**Three attempts at this boundary, and the pattern is the lesson.** R50 drew it by location and
caught the engine's own paperwork. R69 drew it by location again and stopped catching a pack's
mistake. R70 drew it by authorship and then under-described its own principle. A requirement
whose sentence and whose rationale disagree will be implemented from the sentence.

*Checkable: the three parts R70 already required, plus a fourth — a token in a chain file this
run **appended to** halts the run.* The fourth is the only one that distinguishes R71 from R70;
a suite without it passes on the code R71 replaces.

### Added at revision 6

R72 closes the finding from the fifth fresh-context verification. It is not a fifth word for
the same rule — it is a different **kind** of rule, and that is the point.

- **R72.** WHEN the executor completes, IF any file under the target directory contains a token
  the manifest declares, THEN THE executor SHALL exit non-zero naming the file and the token —
  EXCEPT that a file under the resolved artifact root SHALL NOT be swept WHERE it existed
  before this execution began AND its content is unchanged since.

**Four revisions failed the same way, and the failure was the method.** R50 swept by location.
R69 excluded by location. R70 named one mechanism, `create`. R71 named two, `create` and
`append`. Each was a better word than the last, and each was short a mechanism: a `run` action's
generator modifying a pre-existing chain file writes bytes that no registration path records.
Reproduced — the same fixture and command halts the run when the generator *creates* the file
and exits 0 when it *modifies* one that was already there, with the token still on disk. The
executor already accepts authorship of generator-created files, so its own model of "this run
wrote it" was inconsistent between two outcomes of a single action.

**R72 names no mechanism, so it cannot be short one.** The question stops being *how was this
file written* and becomes *is it different from before*. Any write path — declared, generated,
or added to the executor next year — is covered by construction rather than by remembering to
register it.

This is what the earlier rules were reaching for. "The executor is answerable for what it
wrote" was always the right principle; enumerating the ways it can write was the wrong way to
express it, because that list is open and the spec kept treating it as closed.

**Cost, stated rather than discovered:** the executor must hash the chain files it snapshots
before the run and compare after, where before it only recorded paths. That is a real cost on a
large chain and it is the price of a rule that cannot be short a mechanism.

*Checkable, and the fourth part is the one that distinguishes R72 from R71:* a chain file this
run did not touch does not halt it; a chain file this run created does; a scaffolded file
outside the chain does; **and a pre-existing chain file that a `run` action's generator modified
does.** A suite without the fourth passes on the code R72 replaces.

---

## Design

### The shape of the decision

Three things had to be settled before anything else, because everything else follows from them:
**where the scaffold's knowledge lives**, **what runtime executes it**, and **what format it is
written in**. Charter I1 and I5 (widened in v3 to cover actions and commands) decide the first:
the engine may not say `dotnet`, `src/`, `aspire-apphost` or `Directory.Packages.props`, so every
one of those strings lives in the pack and reaches the engine only as data.

That makes the engine a general-purpose executor of pack-declared actions. Charter v3 already
names the consequence and the trust boundary; this spec inherits it and adds the containment
rules (R4, R5, R9) rather than re-opening it.

### Runtime: Windows PowerShell 5.1

The executor ships to every user, so the charter's runtime table binds it. C# is first
preference but cannot ship — installing the engine would then require a .NET SDK, and the
engine must work for a pack that has nothing to do with .NET. PowerShell 5.1 is next, is what
`sdlc_gate.ps1` and `Assert-SliceManifest.ps1` already are, is present with no install step, and
is already covered by `EveryShippedPowerShellScript_Parses`. It also inherits the charter's
Windows-only platform limit, which this change does not widen.

**Rejected: Node.** `parallel-build.js` is Node only because workflow scripts *are* JavaScript;
that exemption does not extend to a new tool, and the charter's preference order puts Node third.

**Rejected: the model performs the scaffold by following the pack's prose.** This is what
happens today and it is precisely what the intent objects to: a skill body interpreted by a
model cannot be executed by a test, so none of S1–S9 could ever be checked. The intent's
"declared as data, not prose" is the decision that makes the rest of this spec falsifiable.

### Format: JSON

PowerShell 5.1 has `ConvertFrom-Json` in the box and no YAML parser — the gate hook says so in
a comment and hand-rolls a regex instead. A scaffold manifest is nested and has more than one
action kind, so a regex parser is not an option, and taking a YAML module dependency for
something that ships is not justified.

JSON also gets two properties free. The gate hook's `SkipExtensions` already contains `.json`,
so the manifest file is never itself gated; and `ConvertFrom-Json` is already the parser this
repository trusts on hostile input, with the strict-mode traps documented in `Get-Field`.

**This does not add a fourth slice-manifest parser.** The charter warns that
`Assert-SliceManifest.ps1`, the gate hook and `parallel-build.js` already hand-roll three
parsers of the *slice* manifest and that a fourth is the moment to consolidate. The scaffold
manifest is a different document with a different schema and one parser; the warning is not
triggered. It is worth stating because the words are similar enough to be confused later.

### Action kinds, and how a rewrite stays stack-neutral

A manifest has a preflight array and an ordered action array per kind. Six action kinds, plus
`preflight` which is not an action:

| Kind | Fields | What the engine does |
|---|---|---|
| `preflight` | `command`, `args`, `expect_exit`, `expect_match`, `remediation` | Its own array, not an action. Runs before anything else; on failure writes nothing and relays `remediation` (R9, R30, R57). |
| `mkdir` | `path`, `unless_exists` | Creates the directory. |
| `run` | `command`, `args`, `cwd`, `unless_exists` | Runs a generator, with `cwd` inside the target (R63). |
| `copy` | `asset`, `path`, `tokens`, `unless_exists` | Copies a pack asset, substituting tokens. Never overwrites (R5). |
| `append` | `asset`, `path` | Creates the file, or appends only the asset lines it does not already contain (R45, R46). |
| `rewrite` | `glob`, `pattern`, `replacement`, `assert_after` | Applies the pack's regex to files **this run created** (R41, R58), then checks `assert_after` (R42). |
| `delete` | `glob` | Removes generator sample content, restricted to paths this run created (R26, R41). |

`unless_exists` is available on every action kind, not only `run` (R43) — revision 1's table
implied otherwise while R10 said the opposite.

**`append` exists because the pack already needs it.** The pack's skill appends the Terraform
ignore entries rather than replacing the file. With only `copy`, a generator that creates
`.gitignore` first makes the Terraform copy a skip, the run exits 0, and the entries are
silently absent — R25 satisfied on paper and broken on disk. `rewrite` cannot stand in for it,
because a rewrite that fires on every run is the thing R7 forbids.

**Why `rewrite` is scoped to files this run created.** Revision 1 had `required: true` fail the
run when a rewrite matched nothing. Both declared rewrites are one-way, so the second run —
where `unless_exists` skips every generator and nothing is regenerated — matched nothing and
failed, contradicting R7 and failing S6. R29 also failed on the *first* run on any machine whose
installed template pack already emits the pinned version. Scoping to created files fixes the
first; `assert_after` fixes the second by asserting the file's end state rather than that the
regex fired. A template that changes shape still fails loudly, which is what `required` was
for.

`rewrite` is the load-bearing one for I1. "Strip the `Version` attribute from a
`PackageReference`" is a .NET sentence, and an engine that implements it by name has a stack in
it. The engine instead implements *"apply this regular expression to these files"*, and the pack
supplies `pattern` and `replacement`. The same mechanism handles R27 and R29 with no new code
and no new engine vocabulary.

`assert_after` is the guard against the silent variant of this: a template changes shape, the
regex stops matching, nothing fails, and the pinned version quietly stops binding. A file this
run created that does not end in the declared state is an error (R42) — which catches the
template drift `required: true` was aiming at, without failing on the runs where there was
legitimately nothing to change.

**Rejected: named transforms (`kind: "strip-package-versions"`).** Readable, and an outright I1
violation — the engine would carry a .NET concept under a stack-neutral spelling, which is the
"neither imposed anything on anyone" excuse the charter records I1 as having twice rejected.

**Rejected: letting the pack ship a script the engine runs.** Simplest of all, and it makes the
engine an arbitrary-code runner rather than an executor of a closed set of declared actions. The
containment rules (R4, R5) would then have nothing to contain.

### Where the executor is exposed — *Open question in the intent, settled here*

**Inside `init`, not as its own command.** `init` invokes
`plugins/sdlc-core/scripts/Invoke-ScaffoldManifest.ps1` with the manifest path, the kind, the
target directory and the confirmed solution identifier (R60); the script is shipped engine
tooling, not a skill.

The path matters and revision 1 got it wrong. Repo-root `scripts/` holds `check-workflow.mjs`
and is dev-only — nothing there reaches a user, and `EveryShippedPowerShellScript_Parses`
enumerates `plugins/` recursively and would never have seen it. An executor that ships to
nobody and is never parse-checked defeats the reason PowerShell was chosen for it.

The re-runnability the intent wants comes from `init` becoming re-enterable rather than from a
second command (R16). Today `init` closes by saying it is never run again for this repo; that
line goes, and a run in an initialised repo offers two things: amend the charter, or fill
scaffold gaps.

**Rejected: `/sdlc-core:scaffold` as a sibling command.** A second Stage-0 entry point is a
second thing a new teammate must learn and a second place the charter-approval ordering (R13)
could be bypassed — a command that scaffolds without going through `init` has no gate in front
of it. Gap-filling is rare enough that it does not earn its own verb.

**Rejected: the executor as a workflow.** Workflows are for dependency waves and fan-out. This
is an ordered list that halts on first failure, run once.

### Where the solution name comes from — *Open question in the intent, settled here*

Derived, confirmed, then recovered:

1. **Derive** a candidate from the repository directory name: split on `-`, `_`, `.` and space,
   drop everything outside `A-Za-z0-9`, PascalCase each segment, join. `ai-sdlc` → `AiSdlc`.
2. **Confirm** it in the interview, as one question alongside the kind and the module
   boundaries. It is the single most expensive string in the repository — it becomes every
   assembly name, every root namespace and every project directory — and it is unreachable by
   the "never restructures or renames" non-goal once written.
3. **Stop and ask** when the derivation yields nothing usable (R18): empty after filtering, or
   leading with a digit. A repaired guess — prefixing an underscore, say — puts a name nobody
   chose into every assembly in the repository.
4. **Recover, never re-derive**, on a later gap-filling run (R19): take it from the name of the
   solution file already on disk, found by the manifest's `solution_file_glob`. Re-deriving
   would produce a second identifier if the directory has since been renamed, and the repo
   would end up with two assembly prefixes.

Steps 1, 3 and 4 are the executor's, so they are testable; step 2 is `init`'s, and is one of
the parts S11 covers by hand.

**Rejected: the charter's `solution:` frontmatter field.** It is free prose today — this
repository's own charter says `solution: ai-sdlc`, which is not a valid identifier — and the
five artifact frontmatter shapes are Frozen, so tightening it or adding a sibling field is a
charter amendment plus a migration for every existing chain. Not worth it for a value the
emitted files already persist.

**Rejected: deriving silently with no confirmation.** Cheapest, and it puts the repository's
most-repeated string beyond review. It also collides with S11: an interview that asks nothing
is decorative.

### What the README contains — *Open question in the intent, settled here*

A pack asset with tokens, not a stub, and not written by the engine — I1 forbids the engine
naming a build command, which is most of what a useful README here holds. Six sections (R24):
what this solution is (one token-substituted line), how to build, how to run tests, how to run
locally, where the artifact chain lives and what `CHARTER.md` is for, and the
AppHost-is-not-production invariant.

The last one earns its place on its own. The pack already states that a resource present in the
AppHost and absent from Terraform is not deployed, and that this is the single most likely way
the split goes wrong; a README is where somebody reads it before making that mistake rather
than after.

**Rejected: a one-line stub.** The intent asks whether the README earns its place, and a stub
does not — it is a file that exists so a checklist can be ticked.

**Rejected: generating it from the charter.** The charter is not written yet in a useful form
at scaffold time, and it is the engine that holds it — so the engine would be composing prose
about build commands. Straight into I1.

### The gate hook — *Open question in the intent, settled here*

**The skip list does not change, and `init` becomes runnable under `SDLC_GATE=block` — because
the executor is the only thing that writes.**

The intent names the half-gating problem exactly: the hook matches `Edit|Write|NotebookEdit`, so
generator output written by a shell is invisible to it while pack assets written through `Write`
are gated, and under `SDLC_GATE=block` the second half refuses. Making the whole scaffold pass
through one process resolves the asymmetry in the only direction available: uniformly outside
the hook. The starter notebook comes from the executor too, so `NotebookEdit` never sees it.

This is a real widening of the ungated write surface and is stated rather than buried. What
makes it acceptable is not the mechanism but the position: at scaffold time there is by
construction no `plan.md` in the repository, so the gate's question — *does an approved slice
claim this file* — has no satisfiable answer, and a warning on every emitted file is noise on
the one occasion the user cannot act on it. The human gate that does apply is the charter
approval immediately before (R13).

**The executor is the only writer only because R47 and R48 say so.** Revision 1 asserted it in
prose while no requirement moved the pack's seven configuration files off `init`'s own `Write`
calls — and `.props`, `.runsettings` and `.tf` are not in the hook's skip list, so the exact
half-gating asymmetry this section claims to resolve survived the design that claimed to resolve
it. R47 puts every pack-named file in the manifest; R48 leaves `init` writing the charter and
nothing else.

### What actually stands in the hook's place — stated in one paragraph

Revision 1 argued the compensating controls in three separate sections, which let the document
avoid confronting them together. Together:

| Control | Covers | Does not cover |
|---|---|---|
| R4, R44, R63 | `copy`, `append`, `rewrite`, `delete` paths, and where a `run` starts | what a generator reaches once started — `run` takes an arbitrary command |
| R5 | overwriting an existing file via `copy` | nothing, now that R41 restricts `rewrite` and `delete` to files this run created |
| R56, R59 | every path created, mutated or removed, on the owner's screen | a file a `run` action's generator wrote outside the target |
| **R38, R39** | **the approval gate, as a check rather than as prose** | — |

R38 is revision 2's answer to the weakest argument in revision 1. The section leaned on R13 —
*`init` does not scaffold before approval* — which is one of only two requirements the spec
declares not machine-checkable. An ungated write surface justified by prose a model interprets
is not justified.

**The executor now reads the charter's `status:` from the file and refuses to run on a draft**
(R38, R39, R40). This is not a new idea in this repository: I3's checkable clause is *every path
that consumes an artifact reads `status:` from the file and stops*, and the charter records
`parallel-build.js` — engine tooling, not a skill — being fixed for precisely this shape of bug,
having trusted its arguments for the approval gate. The executor is the same kind of tool making
the same class of decision. The charter is the engine's own artifact, so reading it imposes no
stack and names no pack file: I1 and I5 do not bite.

**This converts S10 from human-verified into a test.** "No project file exists on disk while the
charter is `draft`" becomes: run the executor against a temp repo whose charter says `draft`,
assert exit non-zero and an empty tree. The residual human-verified part is narrower and
honest — that `init` *calls* the executor at the right point in its own prose. S11 is unchanged.

The remaining gap is stated rather than argued away: **`run` executes a pack-supplied command
and nothing here bounds what that command touches.** R63 sets its working directory; it does not
sandbox it. That is F1's trust boundary, and no containment rule in this spec is a substitute
for reading the manifest.

**Rejected: adding the emitted extensions to `SkipExtensions`.** `.csproj`, `.props`,
`.runsettings`, `.tf`, `.cs`, `.slnx` — that is most of a .NET repository, so the gate would go
quiet for every edit for the rest of the repository's life to stay quiet for ninety seconds at
init. It also puts a pack's file extensions in the engine, violating I5.

**Rejected: a hook skip for "charter exists, no slug directory yet".** Narrow, and it encodes a
lifecycle assumption in a file whose stated design rule is to exit 0 on anything it did not
expect. It also does nothing for gap-filling in a mature repo, which is the case the scoped
exemption would need to cover and cannot.

### Templates: what is in the box, and what is not — *Open question in the intent, answered empirically*

Verified on this machine, 2026-09-18, SDK 10.0.301:

```
$ dotnet new uninstall
   Aspire.ProjectTemplates
      Version: 13.4.6
      NuGetSource: https://api.nuget.org/v3/index.json
      Templates: aspire-apphost, aspire-servicedefaults, aspire-mstest, ...
```

**The Aspire templates are not in the SDK box.** They resolve here because
`Aspire.ProjectTemplates` was installed from nuget.org at some earlier point. On a clean machine
they are absent, and installing them is exactly the `dotnet new install` the "never mutates the
developer's machine" non-goal forbids.

**Chosen: preflight and stop.** The web kind's manifest declares the templates it needs, the
executor probes each one before any other action, and a missing one halts the run with the
pack's own installation command for the owner to run themselves (R9, R30). The manifest never
installs anything. The consequence is stated plainly rather than softened: **on a clean machine
the web kind does not scaffold at all until the owner installs the Aspire template pack
themselves.** That is the non-goal being honoured, not a gap — and it is the difference the
intent's Constraints section demands, between failing clearly and half-scaffolding.

`sln`, `classlib`, `console`, `webapi`, `blazor`, `mstest` and `gitignore` *are* in the box, so
the competition kind carries no such prerequisite and scaffolds on a clean machine unaided.

**Rejected: the pack ships the Aspire projects as its own assets.** It removes the prerequisite
entirely, and it walks straight into the "never hand-maintained project templates" non-goal.
AppHost and ServiceDefaults are the two files in this stack that change most at every Aspire
release — ServiceDefaults alone carries seven package references that moved between 13.4.6 and
13.5.4 — so this trades a one-time install the owner performs knowingly for a permanent
maintenance burden nobody is assigned.

**Rejected: narrow the web kind to what in-box templates cover.** Emit the Blazor front end, the
API and the test projects, and treat Aspire orchestration as optional. It scaffolds anywhere,
and it guts the kind: without an AppHost there is no composed system, so `Aspire.Hosting.Testing`
has nothing to start and S2's integration half can promise nothing. The intent's web kind is
defined by the orchestration; a web kind without it is the library-or-tool kind wearing its
name, and that kind is explicitly deferred.

**How the probe stays engine-neutral.** The `preflight` action carries `command`, `args`,
`expect_exit`, `expect_match` and `remediation` — all pack strings. The engine runs the command,
compares the exit code, matches the pattern against stdout, and on failure prints `remediation`
verbatim. It never learns what a template is, never spells `dotnet new`, and never spells a
template id, which is what keeps I5 intact for a pack whose prerequisite is a compiler toolchain
or a container image rather than a template pack.

**Whether the non-goal binds CI: it does not, and the distinction is drawn here because nobody
had drawn it.** "Never mutates the developer's machine" protects a *person's* workstation — a
global template install is the least reversible thing this could do, it outlives the repository
that caused it, and the owner never consented to it. A GitHub-hosted runner is none of those: it
is ephemeral, declared in a file under review, and discarded at the end of the job. So the
repository's own CI installs the prerequisite as an explicit workflow step (R37), and the
manifest still refuses to (R30). The rule is about consent and reversibility, not about the
verb `install`.

**No notebook generator exists.** Verified alongside the above: `dotnet new list` matches nothing
for `notebook`, `ipynb` or `polyglot`. The starter notebook is therefore a pack-shipped asset
with no template behind it — the one emitted file with no generator, no analyzer gate, and no
build step that would catch it being wrong. That is F3, and it is peter's to settle.

### Four defects in the current assets, found by running the scaffold

All four are verified, not predicted. They are requirements above because each one is a way the
scaffold reports success while being wrong.

**1. `dotnet new sln` emits `.slnx`, not `.sln`.** Verified: `dotnet new sln --name Probe1`
produced `Probe1.slnx`. The frozen layout below says `.slnx`, and R19's recovery reads that
extension. (The pack's `.editorconfig` already lists `slnx` in its indent group, so this was
half-known.)

**2. Central package management rejects every generator's output.** Verified for two templates:

```
error NU1008: The following PackageReference items cannot define a value for Version: MSTest.
error NU1008: The following PackageReference items cannot define a value for Version:
  Microsoft.Extensions.Http.Resilience, Microsoft.Extensions.ServiceDiscovery,
  OpenTelemetry.Exporter.OpenTelemetryProtocol, OpenTelemetry.Extensions.Hosting,
  OpenTelemetry.Instrumentation.AspNetCore, OpenTelemetry.Instrumentation.Http,
  OpenTelemetry.Instrumentation.Runtime.
```

The intent knew about the first. The second is larger and changes the shape of the fix:
**stripping version attributes is not enough**, because seven of those packages have no
`PackageVersion` entry in the pack's `Directory.Packages.props` at all. R28 requires the pack to
pin every package its generators emit, which is new content in that asset and a standing
maintenance cost at every template bump — kept honest by the restore in R31 rather than by
memory.

**3. `global.json`'s Aspire SDK pin is inert.** The AppHost template emits
`<Project Sdk="Aspire.AppHost.Sdk/13.4.6">` — an inline version. Proved by substituting an
impossible version and building:

```
error MSB4236: The SDK 'Aspire.AppHost.Sdk/99.0.0' specified could not be found.
```

`global.json` pins `13.5.3` and was ignored; the inline attribute wins. The pack's own stated
rule — the AppHost SDK version, the Aspire package versions and the CLI bundle must agree, and
a mismatch fails at AppHost startup naming the wrong side — is therefore broken by the scaffold
itself, silently, in the default configuration. Worse, the version that binds is whatever the
*installed template pack* happens to be: 13.4.6 here, 13.5.4 on a machine that updated
yesterday. R29 rewrites it to the pinned value.

**4. "No demo content survives" and "at least one test executed" pull against each other.**
`dotnet new mstest` emits `Test1.cs` with one passing sample test; deleting all sample content
leaves a suite that collects zero tests, which exits 0 and reads as green — the exact failure
mode the charter's I12 and the pack's own validation section warn about. R26 resolves it in the
only direction that satisfies both: sample *pages, components, endpoints and data types* are
deleted, and the pack ships one real starter test asset that asserts something about the
solution rather than `Assert.IsTrue(true)`. A scaffold whose test suite is empty cannot satisfy
S2, and a scaffold that ships `TestMethod1` has demo content in it.

### Order of operations inside `init`

Unchanged up to the gate, then:

1. Interview — kind, module boundaries, solution identifier (R15, R17, R18).
2. Write the charter, attack it, get approval. `init` writes nothing else (R48).
3. Run the executor with the manifest, the kind, the target and the confirmed identifier (R60).
   **Nothing is generated before approval** (R13) — and the executor refuses a draft charter of
   its own accord rather than relying on step 2 having happened (R38).
4. Relay the executor's report, failures and mutations in full (R56, R59, R20).
5. Run the pack's build and test commands and show the real output, per I12.

---

## Data and contracts

Three persisted or public shapes change. All three outlive this change.

### 1. The scaffold manifest — a new persisted contract

New file, shipped in every pack, parsed by shipped engine code. Once a second pack exists it is
a format, not a file.

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
        { "kind": "run", "command": "...", "args": ["..."],
          "cwd": "...", "unless_exists": "..." },
        { "kind": "copy", "asset": "...", "path": "...", "tokens": ["solution"] },
        { "kind": "append", "asset": "...", "path": "..." },
        { "kind": "rewrite", "glob": "...", "pattern": "...",
          "replacement": "...", "assert_after": "..." },
        { "kind": "delete", "glob": "..." }
      ]
    },
    "competition": { "description": "...", "profile": "...",
                     "preflight": [], "actions": [] }
  }
}
```

`version` is present from the first release so a later schema change is detectable rather than
a mystery parse failure. The executor refuses a `version` it does not know.

`solution_token` and `solution_file_glob` are the two top-level fields the identifier handling
needs (R17–R19, R53): the token the pack wants substituted, and how the pack spells the solution
file the executor recovers an existing identifier from. Both are the pack's strings, which is
what keeps `.slnx` and `{{SOLUTION}}` out of the engine.

`preflight` is a separate array from `actions` (R57) so that R9's *"before any other action"* is
a property of the document's shape rather than of the executor's care. Revision 1 modelled
preflights as ordered actions while requiring them to be hoisted, which is a rule only the
implementation could break and only a reader could notice.

`profile` per kind answers a question revision 1 left open: the pack already branches on a
*profile*, and the spec added a *kind*, with no stated relationship. **The kind determines the
profile** — the competition kind selects the machine-learning profile (R67), which is what makes
that profile's package pins part of R28's and R66's surface rather than an unrelated choice. A
kind whose emitted projects need pins its profile does not carry is a manifest bug.

### The executor's report — a second new contract

Three classes of consumer read it: `init` relays it to the owner (R20, R59), four test rows
parse it, and a future gap-filling run reads what a previous one did. That makes it a contract,
and revision 1 declared no fields for it.

```json
{
  "report_version": 1,
  "artifact_root": "...",
  "target": "...",
  "kind": "...",
  "solution": "...",
  "outcome": "completed | halted",
  "actions": [
    { "kind": "copy", "outcome": "created", "path": "..." },
    { "kind": "append", "outcome": "skipped", "path": "...", "reason": "..." },
    { "kind": "rewrite", "outcome": "mutated", "path": "..." },
    { "kind": "delete", "outcome": "removed", "path": "..." },
    { "kind": "run", "outcome": "failed", "command": "...", "reason": "..." }
  ]
}
```

`report_version` is present for the same reason the manifest's is (R61). `mutated` and `removed`
are the two outcomes revision 1 omitted — without them a rewrite or a delete left no trace in
the one record the owner sees, which is precisely where an over-broad glob would have hidden.

**"Verbatim" in R20 means every `failed` entry with its reason and every `mutated` and `removed`
path, not a count** (R59). A relay that says "4 files changed" hands the owner nothing they can
check.

**Migration:** none — nothing exists yet. **Forward cost:** every future pack implements this
schema, and the engine's parser is the only thing that may define it.

### 2. The emitted layout — Frozen by charter v3

Frozen on the day this ships. Four things cite it: `spec-writer` reads the structure to ground a
design, slice manifests name paths inside it, `coverage.runsettings` excludes assemblies by name
pattern, and the gate hook matches edited paths against manifests. Changing it later is a
migration across every downstream repository.

Web kind:

```
<repo>/
  <Solution>.slnx
  README.md  .gitignore  .editorconfig  global.json
  Directory.Build.props  Directory.Packages.props  coverage.runsettings
  .sdlc/CHARTER.md
  src/<Solution>.AppHost/           src/<Solution>.ServiceDefaults/
  src/<Solution>.Web/               src/<Solution>.Api/
  tests/<Solution>.UnitTests/       tests/<Solution>.IntegrationTests/
  terraform/versions.tf
```

Competition kind — same top-level split, different projects:

```
  src/<Solution>.Features/   src/<Solution>.Training/
  tests/<Solution>.UnitTests/
  notebooks/<Solution>.ipynb
```

The frozen parts are: the `src/` + `tests/` + `terraform/` + `notebooks/` top-level split; the
`<Solution>.<Role>` project-name pattern; the role suffixes `AppHost`, `ServiceDefaults`,
`UnitTests`, `IntegrationTests`; and `.slnx` as the solution extension. Project *contents* stay
movable per the charter's contents/layout split.

### 3. `coverage.runsettings` ↔ emitted project names

Already a contract, previously undeclared. `Directory.Build.props` rewrites each project's
assembly name to `<Solution>.<ProjectName>`, and `coverage.runsettings` excludes
`[*.UnitTests]*,[*.IntegrationTests]*,[AppHost]*,[*.AppHost]*`. A test project named `Tests`
instead of `UnitTests` produces a coverage number that counts test code and raises nothing.
R23 states the contract; the S5 row under **Test strategy** is what holds it.

Verified for the current assets: `Probe1.UnitTests` builds to `Probe1.UnitTests.dll`, matching
`[*.UnitTests]*`; `Probe2.AppHost` matches `[*.AppHost]*`.

**Revision 1 attributed that to the wrong mechanism.** The `AssemblyName` rewrite is conditioned
on `!StartsWith('{{SOLUTION}}.')`, and under the frozen layout every project name already
carries the prefix — so the rewrite never fires, and the assembly name is simply the project
name. The coverage patterns match because of the naming convention in R23, and for no other
reason. The distinction matters: if a future kind emits an unprefixed project name, the rewrite
*would* fire, and it is then the only thing keeping the coverage pattern matching. Neither
mechanism is checked by reading the props file; the S5 row is.

### 4. `Directory.Packages.props` gains entries

Not a format change, but public in the sense that every downstream repository inherits it: the
seven ServiceDefaults packages plus whatever the other generators emit (R28). These are pinned
versions a downstream repo receives and then owns.

**R28's surface is larger than "packages a generator names."** The asset sets
`CentralPackageTransitivePinningEnabled` alongside `ManagePackageVersionsCentrally`, so a
transitive dependency can demand a `PackageVersion` too. R66 states the real requirement:
restore must succeed *with transitive pinning on*, which is not a list anyone can enumerate by
reading csproj files. **Nothing makes R28 self-enforcing** — its only honesty mechanism is the
restore inside R31, which is why R65 matters: if that check can silently skip for want of a
prerequisite, R28 and R66 have no instrument at all.

### What is frozen here, and what is only pinned

The charter freezes emitted *layout* and name patterns while leaving pack *contents* movable, so
the requirements above split accordingly:

| Frozen — changing it is a migration | Pinned until superseded — a revision, not a migration |
|---|---|
| R21, R22 (the top-level split and which roles exist), R23 (name patterns), the layout in §2 | R24, R25 (what the README and ignore file contain), R26, R27, R29, R30 (which templates and rewrites a given SDK generation needs), R28, R66 (version pins) |

Without this split the next template bump reads as a spec amendment. It is not: it is a pack
revision that the tests catch and the requirements already permit.

---

## Failure modes

| Failure | What the system does |
|---|---|
| A required template is missing | Preflight fails; nothing is written; the pack's install instruction is relayed; nothing is installed (R9, R30). |
| The .NET SDK is absent | Same path — a preflight check declared by the pack. The engine never names the SDK (I1); the message is the pack's. |
| A generator command fails midway | Executor halts at that action, reports what was already created, exits non-zero. The repository is left partially scaffolded — a re-run fills the rest (R7, R34), which is why idempotence is a requirement and not a nicety. |
| A destination file exists | Skipped and reported. Never overwritten (R5). |
| A rewrite stops matching after a template bump | `assert_after` fails the run on a file this run created that does not end in the declared state (R42). Without this, the pinned SDK version silently stops binding — defect 3 above, re-introduced. |
| A token is never substituted, or survives from an earlier run | Post-run sweep of every file under the target fails the run naming file and token (R72). A file under the artifact chain is swept unless it existed before the run with its bytes unchanged - a rule naming no write mechanism, so it cannot be short one: the chain's own documents quote the token while specifying it, and a pack that wrote there without substituting would otherwise go unnoticed. This is the `{{SOLUTION}}.Foo` assembly regression, including the gap-filling case where this run wrote nothing. |
| A rewrite or delete glob is too broad in a mature repo | Both are restricted to paths this run created (R41), and both appear in the report as `mutated` or `removed` (R56, R59). |
| The charter is still a draft | The executor reads `status:` from the file and refuses (R38, R39) — not left to `init`'s prose. |
| Generator output references an unpinned package | Restore fails with NU1008 at R31's build. Loud, and it is the check that keeps R28 honest as templates move. |
| Sample content deleted down to zero tests | R32 requires a reported count, not an exit code. A suite that collects nothing exits 0. |
| Manifest declares a path outside the target | Refused, nothing written, path named (R4). |
| Manifest `version` is unknown | Refused before any action. |
| Container runtime absent | Integration tests compile and are not run, and the output names it, so "green" is never claimed on a machine that could not run half of it (R64). Every other absent prerequisite fails the run instead of skipping (R65). |

---

## Flagged concerns

**F1 — The engine executes pack-supplied command strings. *Owner: peter.*** Charter v3 names
this and locates the trust boundary at the pack: installed deliberately, from this marketplace,
reviewed like any other code. This spec does not re-open it, but it adds the rule that follows
from it and should be in the pack-authoring guidance: **a manifest is read by reviewers the way
a script is, and a command nobody would approve in a script is not approvable in a manifest.**
The containment rules (R4, R5, R9) bound what an *accidental* manifest can do; they do not bound
a hostile one, and nothing in this design pretends otherwise.

**F2 — The scaffold writes outside the gate hook entirely. *Owner: peter.*** Consequence of the
design above, and still the one to look at hardest. Under `SDLC_GATE=block` today the scaffold
would be half-refused; after this change it is wholly unobserved.

Revision 2 strengthened this rather than restating it. The compensating controls are now
R4/R44/R63 for paths, R5 and R41 for what may be touched, R56 and R59 for what the owner sees,
and — the material change — **R38, the executor reading the charter's `status:` from the file
and refusing a draft**, which replaces a prose requirement nobody could check. The honest
residual is in the table in the Design section: `run` executes a pack-supplied command, and
nothing here bounds what that command reaches once started.

**F3 — The starter notebook has no template, no analyzer gate, and is where N1 leaks.
*Owner: peter — the intent assigns this and Design does not overrule it.*** Two distinct
problems, both real. A competition-specific scoring or cross-validation snippet is a *domain*,
not a defect class, and N1 forbids it outright; and the pack's own rule is that notebooks sit
outside `Directory.Build.props`, so R31 and R32 say nothing about whether the shipped notebook
runs — the intent's own complaint reproduced in miniature, inside the fix for it. There is also
no notebook template in the SDK (verified: `dotnet new list` matches nothing for notebook,
ipynb or polyglot), so this asset is hand-written by definition.

**Resolved by the owner, 2026-09-18: headings only.** The notebook ships as section headings
and a reference to the emitted solution, with no executable scoring, cross-validation or
feature-engineering logic — recorded as R68. That closes both halves of this concern at once:
there is no domain logic for the permanent non-goal to catch, and nothing unverified ships in
the one emitted file that the compiler and analyzers never see. R22 is unblocked.

The residual, recorded rather than hidden: a headings-only notebook is a file whose usefulness
depends entirely on whoever fills it in. If a later revision puts working code in it, this
concern reopens and needs a verification story before it can close again.

**F4 — CI cannot prove S1–S9 on the existing runner. *Owner: peter.*** The hosted
`windows-latest` image runs Windows containers only, so the SQL Server Linux container the
integration half needs cannot start there; the PowerShell gate tests need Windows. R33 splits
the criterion so the compile half runs everywhere and the run half is machine-dependent, but the
standing question of a second runner is a cost decision.

The web kind additionally needs the Aspire template pack installed on the runner.
**Resolved by the owner, 2026-09-18: a disposable runner is not a developer's machine.** The
"never mutates the machine" non-goal binds a person's workstation — a global install there is
irreversible, outlives the repository that caused it, and the owner never consented. A hosted
runner is ephemeral, declared in a reviewed file, and destroyed at the end of the job. So CI
installs the prerequisite as an explicit workflow step (R37) while the manifest still refuses to
(R30), and the check that proves the web kind generates and compiles runs on every push.

What remains unresolved here is only the container question, which is a cost decision and not a
rule: the hosted Windows image cannot start the Linux SQL Server container, so the integration
tests compile there and run only where a container runtime exists. A second runner would close
that; nobody has agreed to pay for one.

**F5 — `spec-writer`'s greenfield branch is now mostly unreachable. *Owner: peter.*** Its method
says to ASK rather than invent on an empty repo; after this change the repo is never empty when
a spec is written. The intent lists it as affected. Not in this change's scope, and it should
not be left to rot — it is a paragraph that will read as correct and be wrong.

**F6 — The local SQL password reaches every scaffolded repository. *Owner: peter.*** The pack
commits `password1!` as an Aspire parameter default, with a guard that throws outside
Development. Scaffolding makes that guard emitted code rather than prose in a skill, in every
web repo. The guard is the thing to review once, now, rather than per repo.

---

## Test strategy

All new tests go in `tests/AiSdlc.Tests/`, driving the shipped executor as an external process —
the house rule, because a C# reimplementation would test the reimplementation.

Two classes: `ScaffoldManifestTests` (the executor, fast, no SDK needed — a fixture manifest
with stub commands) and `ScaffoldEmitsBuildableSolutionTests` (the real pack manifest, the real
SDK, slow, and conditional on prerequisites).

| Criterion | Requirements | Check |
|---|---|---|
| S1 build exits 0 | R28, R29, R31, R66 | `ScaffoldEmitsBuildableSolution_<kind>` — execute into temp, build, assert exit 0 |
| S2 tests run and pass, count > 0 | R26, R32, R62, R64 | Assert the reported test count > 0 and 0 failed, for the unit suite **and** the integration suite; integration compiled-or-run per container runtime |
| S3 no `{{` survives | R72 | Sweep every file under the target, including files this run did not write; under the artifact root, every file except those unchanged since the run began |
| S4 restore under CPM | R27, R28, R66 | No version attribute on any reference; restore clean with transitive pinning on — the NU1008 regression test |
| S5 names match coverage patterns | R23 | Assert each emitted test/host assembly name matches an exclusion in `coverage.runsettings`, parsed from the asset — not from a copy in the test |
| S6 re-run is a no-op | R7, R41, R42, R43, R46 | Hash the tree, re-execute, compare. This is the row revision 1's `required` rewrite would have failed |
| S7 partial repo is filled | R5, R34, R45 | Delete `tests/`, re-execute, assert only `tests/` appeared and every other file is byte-identical |
| S8 both kinds | R12, R21, R22, R67 | Every row above, parameterised per kind |
| S9 I1 holds | R2, R35, R54, R55 | `CharterInvariantI1_EngineImposesNoStack` for skills and agents, **plus a new check over shipped engine executables** — see below |
| S10 nothing before approval | R13, R38, R39, R40 | **Now machine-checkable.** Executor against a temp repo whose charter is `draft`: assert non-zero exit and an empty tree. Residual human-verified part: that `init` calls it at the right point |
| S11 interview is required | R15, R17, R51, R52 | **Human-verified** for the asking; the executor's refusal of an invalid supplied identifier (R52) is tested |

Requirements with no S-criterion behind them get their own tests, all SDK-free:

- **Executor behaviour** — halt-on-first-failure (R3), containment (R4), overwrite refusal (R5),
  report shape and its outcome set (R56, R61), preflight aborts writing nothing and relays the
  pack's remediation (R9, R30), a preflight declared inside `actions` is rejected (R57), an
  `assert_after` that does not hold fails the run (R42), a rewrite or delete confined to paths
  this run created (R41, R44), `append` adding only absent lines and being a no-op the second
  time (R45, R46), an unknown kind lists the declared kinds (R12), an unknown manifest
  `version` is refused.
- **The approval gate** — a draft charter refuses and leaves an empty tree, a missing charter
  refuses, and the resolved artifact root appears in the report (R38, R39, R40). This is the
  S10 row and it is the reason F2's argument now stands on a check rather than on prose.
- **Pack structure** — every pack in the marketplace ships a manifest that parses and declares
  at least one kind (R1). This is the check that makes R1 a property of packs rather than of
  `stack-dotnet` alone.
- **Emitted files present** — a `README.md` carrying the pack's build, test and local-run
  commands and the chain's location (R24), and a base ignore file alongside the Terraform
  entries (R25), asserted per kind in the same fixture as S1.
- **Identifier handling** — derivation, the refusal path, and recovery from an existing solution
  file (R17, R18, R19). These live in the executor precisely so they can be tested; only the
  *confirmation* half of R17 is `init`'s prose and therefore human-verified.
- **Documentation** — the repository `README.md` no longer lists `/init-competition` and its
  stage table names what `init` produces (R36).
- **CI prerequisite** — `checks.yml` carries the install step the web kind's preflight requires,
  and the step names the same prerequisite the manifest declares (R37). Asserted by parsing both
  files, so the two cannot drift silently: a manifest that adds a prerequisite CI does not
  install fails here rather than as a confusing preflight failure on the runner.

R14, R15, R16 and R20 are `init`'s prose and are human-verified with S10 and S11.

**S9 needed a second check, because the one revision 1 cited cannot fail for R2.**
`CharterInvariantI1_EngineImposesNoStack` enumerates `plugins/sdlc-core/skills/**` and
`agents/**` for `*.md` only. The executor is PowerShell under `plugins/sdlc-core/scripts/`, so it
is outside that scan entirely: it could hardcode `dotnet`, `*.slnx` or `{{SOLUTION}}` and the
suite would stay green while the spec claimed S9 covered it. R54 and R55 add
`EngineExecutablesImposeNoStack` over shipped engine executables, reusing the same term list —
the file set already exists, since `EveryShippedPowerShellScript_Parses` enumerates `plugins/`
recursively.

**This is a charter amendment, not just a test.** I1's checkable clause names those two
directories and justifies the scope with *"engine tooling lives outside skills/ and agents/"* —
a rationale that held while engine tooling did not execute pack-supplied data and stops holding
the day it does. **The owner settled this on 2026-09-18: widen the check, and amend the charter
at ship time** — the amendment lands with the I9 rows below, alongside the rest of the change,
rather than ahead of it. This is the third charter amendment this one feature has required; the
first two landed before the spec was written because invariants the spec cites must not move
underneath it, and this one does not have that property — it widens a check the spec adds.

**S11 remains human-verified, and so does the part of S10 about `init`'s own prose.** They
belong in the charter's known-and-unchecked list next to the unbumped-plugin-version gap. What
changed is that S10's substantive half is no longer among them: revision 1 justified an ungated
write surface with a requirement it admitted nobody could check, and R38 fixes that rather than
arguing around it.

**Prerequisites fail the run; they do not skip it** (R65). Revision 1 called
`ScaffoldEmitsBuildableSolutionTests` "conditional on prerequisites" without saying what an
absent one does, while End-to-end verification claimed the suite *"must show … passing"* — and a
skipped MSTest leaves `dotnet test` at exit 0 showing neither. A missing template fails with the
preflight's remediation. The single declared exception is the container runtime, whose absence
is named in the output (R64), because that is a genuine machine capability rather than an
install somebody forgot.

**Per I9, every new check is mutation-tested**: inject the fault, watch the named check fail.
One fault per row S1–S9, plus one per executor test.

**New rows for I9's table**, which is a charter amendment at ship time, not now:

| Silent failure | Check |
|---|---|
| Scaffold produces an unbuildable solution | `ScaffoldEmitsBuildableSolution_<kind>` |
| Generator output breaks central package management | the S4 row |
| Emitted names stop matching the coverage patterns | the S5 row |
| A re-run overwrites, or a rewrite re-fires | the S6 row |
| A token survives, including from an earlier run | the S3 row |
| A stack leaks into a shipped engine **executable** | `EngineExecutablesImposeNoStack` |
| The executor scaffolds from a draft charter | the S10 row |
| A prerequisite is absent and the check skips rather than fails | R65's own test |
| The pack's skill and its manifest list different files | `PackSkillCitesItsManifest` |

That last row is W5's substance and is worth stating plainly: **I5 says a pack's skill is *the
only* list of the actions it performs.** If `stack-dotnet`'s seven-row file table survives
alongside the manifest, there are two lists, and nothing checks they agree — a pack could add a
manifest action its skill never mentions, which is the invariant inverted. R49 makes the skill
cite the manifest; the check holds it.

---

## Out of scope

Carried forward from the intent unchanged, plus what Design added.

From the intent: hand-maintained project templates; mutating the developer's machine (no
template-pack install, no workload, no global tool, no image pull); demo content surviving;
**CI pipeline files written into a scaffolded repository** — the intent's non-goal is about what
the scaffold emits, and R37 edits *this* repository's own `checks.yml`, which the intent never
spoke to; the same workstation-versus-runner distinction drawn for the install applies here, and
is separated for the same reason; running Terraform or touching a cloud account; `git init` or
the first commit;
writing or editing `CLAUDE.md`; restructuring or renaming anything in an existing repo; a second
solution file per repository; the competition *chain*, which is `sdlc-kaggle`'s job; and the
library-or-tool and Azure Functions kinds.

Added during design:

- **The executor is not a general task runner.** Six action kinds plus a preflight array, closed
  set. A pack that needs a seventh amends this spec's schema — as revision 2 did, when `append`
  turned out to be required by the pack that already exists rather than by a hypothetical one.
- **No `spec-writer` change.** Its greenfield branch is F5, owned by peter, and is its own change.
- **No gate hook change.** Settled above; the skip list and the matcher are untouched.
- **No charter frontmatter change.** The solution identifier is not persisted in the chain.
- **No second CI runner in this change.** F4 is a cost decision; R33 is written so the suite is
  honest on one runner.
- **No verification of the starter notebook.** F3, owned by peter, and R22 depends on the answer.

---

## Open questions

- **~~What is in the starter notebook, and who owns it?~~** **Answered 2026-09-18: headings and
  a solution reference only, no executable logic** — recorded as R68 and in F3. It reopens only
  if a later revision wants working code in it.
- **When do the deferred kinds land?** *Owner: peter.* Library-or-tool is the smallest and is
  the one this repository would itself have wanted. It is not in this version's scope, and the
  frozen layout above is what it will have to fit.

---

## End-to-end verification

One check proves the whole feature, and it is the thing the intent complained does not exist:

```
dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj --nologo
```

must show `ScaffoldEmitsBuildableSolution_Web` and `ScaffoldEmitsBuildableSolution_Competition`
passing — **passing, not skipped** (R65). Each executes the real pack manifest into an empty
temporary directory, builds the result, runs its tests, and asserts a test count greater than
zero for both suites — so a green suite means a machine has, in that run, created a solution
from nothing and watched it compile and pass its own tests. An absent template fails the run
with the preflight's remediation; only an absent container runtime downgrades the integration
half to compile-only, and says so in the output.

Done by hand once before that is trusted, for the part that is still prose: `/sdlc-core:init` in
an empty directory, confirming that declining to answer the module-boundary question stops the
command rather than selecting a kind (S11), and that `init` invokes the executor after the
approval rather than before (the residual half of S10). The other half of S10 — that nothing is
generated from a draft charter — is no longer a matter of trusting the prose: R38 makes the
executor refuse, and the test asserts an empty tree.
