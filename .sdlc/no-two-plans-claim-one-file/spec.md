---
slug: no-two-plans-claim-one-file
status: approved
intent: ./intent.md
tier: feature
---

# spec — no two live plans claim one file

## What this document decides, and what it inherits

The intent settled a set of things at its own gate and this spec carries them rather than
reopening them: the marking of the seven done-or-replaced plans is the first slice of the plan;
six of those plans keep `status: approved` and gain `finished: true` while
`engine-substitutes-declared-tokens` takes `status: superseded` and no `finished:` line; the
script's behaviour wins on every axis where the two PowerShell readers disagree; the shared
parser ships as a `.ps1`; the third warning state is labelled `contested file` and carries an
action string that does not imply the editor can fix it alone; `hooks.json`'s `description` is
rewritten to carry the literal labels; there are five checks and five register rows, every check
driving the shipped readers as external processes; and `parallel-build.js` is not touched.

Two questions the intent left open are **answered here**, in **Design**, with the alternative
recorded:

- Where the cross-plan check runs inside the plan skill — answered: at the existing manifest
  step *and again* as the last act of the existing gate step, before `status: approved` is
  written.
- Whether the shrunken live set makes the gate hook noisy enough to want softening — answered:
  no, on a measurement recorded in **Design**.

Where the intent states a property, this document chooses the mechanism. Where the intent states
a decision, this document carries it.

## Requirements

Numbered `R<n>` and never renumbered — `plan.md` and the test suite cite these numbers, so
revisions append and mark the superseded entry rather than shifting anything. Each requirement
maps to one of the intent's success criteria (1–8) in the coverage table under **Test strategy**.

### The shared reader

- **R1.** THE engine SHALL ship exactly one slice-manifest reader, in a single file with a
  `.ps1` extension, which both the gate hook and the disjointness script load rather than
  each carrying its own parse.
- **R2.** THE shared reader SHALL treat two path spellings as one claim when they differ only by
  a leading `./`, by directory separator, by case, by repeated separators, by a trailing
  separator, by `.` or `..` segments that cancel textually, by surrounding whitespace, or by one
  being an absolute path inside the base directory and the other its relative form.
- **R3.** THE shared reader SHALL preserve a leading dot segment, so that `.sdlc/CHARTER.md`
  normalises to a key that still names `.sdlc/CHARTER.md` and not to a string matching nothing
  on disk.
- **R4.** THE shared reader SHALL collect a `files:` entry only from a line whose first
  non-whitespace text is `files:`, and only after a `- id:` line has opened a slice.
- **R5.** THE shared reader SHALL carry, for every claim, both the spelling written in the plan
  and the comparison key derived from it, so that a report can name the file the way its author
  wrote it.
- **R6.** IF a `files:` line's value contains no quoted string — an unquoted value, or a
  block-style list whose entries sit on later lines — THEN THE shared reader SHALL record a
  refusal naming the plan, the slice and the line, rather than contributing no claims in
  silence. A value that is one or more quoted strings is legal with or without enclosing
  brackets.
- **R7.** IF a `files:` entry ends in a separator, or contains `*` or `?`, THEN THE shared
  reader SHALL record a refusal naming it as a directory or glob entry. An ordinary directory
  entry with neither marker — `"src/Storage"` — is not detectable textually and is not covered;
  see **Design**.
- **R8.** IF frontmatter carries a `status:` value that is quoted, or that is not exactly one of
  `draft`, `approved` or `superseded`, THEN THE shared reader SHALL record a refusal naming the
  value it found.
- **R9.** IF frontmatter carries a `finished:` line whose value is not exactly `true` or `false`
  unquoted with nothing else on the line, or carries `finished: true` without
  `status: approved`, THEN THE shared reader SHALL record a refusal naming the offending line.
- **R10.** IF frontmatter carries a number of `status:` lines other than exactly one, or more
  than one `finished:` line, THEN THE shared reader SHALL record a refusal naming the count it
  found. Zero is a refusal as much as two, per the charter's fifth rule.
- **R11.** THE shared reader SHALL classify a plan as live when, and only when, it carries no
  plan-level refusal under R56 **and** its frontmatter carries `status: approved` and does not
  carry `finished: true`; a `draft`, a `superseded` and an approved-and-finished plan are each
  not live, and a plan carrying no `finished:` line is not finished.
- **R12.** THE shared reader SHALL carry the artifact-root resolution the gate hook performs
  today, with the same candidate order, the same two-level search and the same fallbacks, so
  that one question about one repository has one answer.
- **R13.** THE shared reader SHALL perform no output of its own and SHALL raise no terminating
  error for a malformed plan, returning refusals as data for its caller to report or discard.

### The gate hook

- **R14.** WHEN the gate hook is given an edit payload for a path it does not skip, and two or
  more live plans claim that path, THE gate hook SHALL write one line to standard error
  carrying the literal label `contested file`, the normalised path, and the chain directory name
  of every live plan claiming it.
- **R15.** WHEN the gate hook reports a contested file, THE message SHALL carry the literal
  action string given in **Design**, which names settling the claim with the owner of the other
  chain.
- **R16.** WHILE `SDLC_GATE` is `block`, THE gate hook SHALL exit 2 on a contested file, as it
  does on its other two warnings.
- **R17.** WHEN exactly one live plan claims the edited path, THE gate hook SHALL exit 0 and
  write nothing.
- **R18.** WHEN at least one live plan exists and none of them claims the edited path, THE gate
  hook SHALL warn with the literal label `unplanned scope`, as it does today.
- **R19.** WHEN no live plan exists, THE gate hook SHALL write the literal label
  `no approved plan` in its message, and its detail SHALL distinguish how many `plan.md` files
  were found from how many of them were live. The literal is new to the emitted text; the hook
  names that state only in its header comment today.
- **R20.** IF the shared reader records a **claim-level** refusal — R6 or R7, where a `files:`
  value or entry is malformed — THEN THE gate hook SHALL drop the refused claims, keep the
  plan's remaining claims, continue comparing every other plan, and emit no additional message.
- **R21.** IF reading or parsing one plan raises, THEN THE gate hook SHALL exclude that plan and
  continue, so that one malformed plan cannot disable the gate for every file in the repository.
- **R22.** IF the shared reader file is absent or unreadable, THEN THE gate hook SHALL exit 0,
  write nothing, and print no stack trace.
- **R23.** THE gate hook SHALL keep its existing edit-path skip rules unchanged — the same
  extensions, the same bare filenames, and the same artifact-root segment test — and SHALL keep
  its existing tool matcher unchanged. The noisy-directory list is **not** among them: it
  belongs to root resolution, which R12 moves into the shared reader.

### The disjointness script

- **R24.** WHEN invoked with `-Plan <path>` and nothing else, THE disjointness script SHALL
  accept the same parameter, emit the same output lines on success, and use the same exit-code
  vocabulary — 0 for pass and 1 for any failure — as it does today.
- **R25.** WHEN invoked in its cross-plan mode, THE disjointness script SHALL compare the claims
  of the plan under check against the claims of every live plan under the artifact root other
  than that plan, and SHALL exclude from that comparison every claim **whose path is contained
  by the artifact root** — the charter's own test, per R55.
- **R26.** WHEN invoked in its cross-plan mode, THE disjointness script SHALL contribute the
  claims of the plan under check whatever that plan's own `status:` says, and SHALL exclude that
  plan from the set it is compared against by resolved path rather than by slug.
- **R27.** WHEN the cross-plan comparison finds a contested path, THE disjointness script SHALL
  name the path and the chain directory of every live plan claiming it, and SHALL exit 1.
- **R28.** WHEN invoked in its cross-plan mode, THE disjointness script SHALL print, once and
  in its normal place after the wave plan, how many `plan.md` files it read, how many were live,
  and how many it refused — including when any of those counts is zero — so that a comparison
  that found nothing is distinguishable from one that compared nothing. The three counts are an
  **overlay and not a partition**: a claim-level refusal leaves its plan live and counted
  refused, a plan-level refusal makes a plan both not-live and refused, so they do not sum. The
  line carries counts only and never a verdict.
- **R29.** IF the shared reader records a refusal for a plan the cross-plan mode reads **other
  than the plan under check**, THEN THE disjointness script SHALL name that plan and the rule
  broken, **drop exactly what the hook drops for the same refusal** — the refused claims for a
  claim-level refusal, the whole plan for a plan-level one, which R56 has already made not live
  — and continue, exiting on the comparison's own verdict rather than on the refusal. The plan
  under check is different and keeps its hard stop: that is R30.
- **R30.** IF the shared reader records a refusal for the plan named by `-Plan`, THEN THE
  disjointness script SHALL name the rule broken and exit 1, in both modes.

### The lifecycle marking and who writes it

- **R31.** THE six plans whose work is in git — `init-scaffolds-solution`,
  `pack-promises-what-it-scaffolds`, `register-names-every-check`, `stack-dotnet-house-stack`,
  `verification-reports-options` and `every-workflow-body-runs` — SHALL keep
  `status: approved` and gain `finished: true`.
- **R32.** THE plan of `engine-substitutes-declared-tokens` SHALL carry `status: superseded` and
  no `finished:` line at all.
- **R33.** WHEN the verify skill reaches the SHIP branch of its gate on a feature-tier chain,
  THE verify skill SHALL set `finished: true` on that chain's `plan.md` and stage `plan.md` in
  the same commit as `verification.md`.
- **R34.** IF the verdict is DO NOT SHIP, THEN THE verify skill SHALL write no `finished:` value
  and stage no `plan.md`.
- **R35.** THE slice manifests of the seven marked plans SHALL be left exactly as they are; only
  the `status:` line and the `finished:` line move.

### The plan skill

- **R36.** WHEN the plan skill runs its manifest check at the step where the plan is written,
  THE plan skill SHALL invoke the cross-plan mode and paste its real output.
- **R37.** WHEN the plan skill reaches its approval gate, THE plan skill SHALL re-run the
  cross-plan mode as the last act before writing `status: approved`, and SHALL NOT write that
  status while the check reports a contested path.
- **R38.** THE plan skill SHALL gain no new step and no new gate, and SHALL keep the
  single-plan invocation available and unchanged in meaning.

### The shipped prose that ships in more than one copy

- **R39.** THE gate hook's header, the README's gate-hook section and the `description` in
  `hooks.json` SHALL each name the same three warning-state labels — `no approved plan`,
  `unplanned scope` and `contested file` — written literally in all three.
- **R40.** THE verifier agent, the artifact-schemas reference and the verify skill SHALL each
  name the same two manifest-drift conditions, keyed on the literal phrases `unplanned scope`
  and `did not land`.
- **R41.** THE artifact-schemas reference SHALL state that `superseded` is written by hand, and
  SHALL NOT claim that `/sdlc-core:plan` writes it.
- **R42.** THE artifact-schemas reference and the charter SHALL stop saying that the grammar
  check and the extracted parser arrive with this change, because both exist once it ships.
  **Three forward references, not two:** the invariant holding the silent-failure register also
  states in the present tense what this chain will do to that list, and its arithmetic SHALL be
  re-derived against whatever the list holds when the slice lands.
- **R43.** THE gate hook's header SHALL stop claiming a per-edit cost of ~150ms; the README's
  and the charter's runtime-startup comparisons SHALL be left alone.
- **R44.** THE README's gate-hook section SHALL cite `tests/AiSdlc.Tests/GateHookTests.cs` as
  where the hook's behaviour is pinned, in place of the script that has never existed, and SHALL
  cite no count of methods.
- **R45.** THE engine plugin's `version` SHALL be bumped, because this change ships hook and
  script code and plugins are cached per version.

### Instruments

- **R46.** THE change SHALL add five test classes, each in its own file, each holding exactly
  one `[TestClass]`, each named by its own row in the charter's silent-failure register, and
  each seen to fail when its fault is injected.
- **R47.** EVERY new check **that exercises the manifest or frontmatter grammar** SHALL drive
  the shipped readers as external processes, and none SHALL reimplement that grammar in C#.
  **This narrows the approved intent, which states it without the narrowing** — a deviation, not
  a repair; see **Design**.
- **R48.** NO new check SHALL read a `plan.md` under this repository's own chain tree. Three of
  the five classes read live files by necessity, so there is no wider "runs over built fixtures"
  clause; see **Design**.
- **R49.** THE gate hook's wall-clock cost SHALL not regress beyond the greater of 15% or 50ms,
  median of five runs, measured before and after on **two** fixtures, with all four figures
  taken on those same fixtures and pasted into `plan.md`:
  **(a)** 500 directories at the top level holding seven plans, which isolates root discovery —
  the term this change relocates; and **(b)** a shallow tree holding **forty** plans, which
  isolates the per-plan term — the term this change *grows*.
  **Fixture (b) is the one that can fail**; the arithmetic for both is in **Design**.
- **R50.** THE charter SHALL gain five register rows naming the five new classes, and **eight**
  known-and-unchecked entries — the eight the approved intent carries, no more — in the slice
  that declares `.sdlc/CHARTER.md`. **Each register row SHALL name the silent-failure class in
  its left column**, so a row for a class covering several failures names several;
  `CrossPlanCollisionTests` carries four. **The entries SHALL be a bulleted list and not a
  table**, because the register check harvests backticked identifiers from column 2 of every
  pipe row in the charter.
- **R51.** THE frontmatter grammar SHALL be compared, in both directions, between the charter
  and the shipped schema reference — the rule count and each rule's key phrase — by
  `FrontmatterGrammarTests`. Three copies of that grammar exist — the charter, the schema and
  R6–R10 — and the first two have drifted once already.
- **R52.** THE charter SHALL be bumped to **version 22** with an amendment-log row, in the same
  slice that adds the register rows, as every precedent does for a row recording an instrument.
- **R53.** THE disjointness script's cross-plan mode SHALL accept `-Root`, naming the
  **repository root** and defaulting to the working directory, and SHALL resolve the artifact
  root from it using the shared reader's resolution, and every claim against it. One flag, one
  meaning: the coordinate system R2's absolute-versus-relative equality and R25's carve-out both
  need.
- **R54.** IF the cross-plan check reports a contested path at the plan skill's gate step after
  a person has already approved, THEN THE skill SHALL NOT write `status: approved`, SHALL report
  the contested path and the other chain, and SHALL state that the plan needs revising and
  re-approving. This is the one place the change touches the gate invariant.
- **R55.** THE gate hook and the disjointness script SHALL decide **whether a claim is under
  the artifact root** the same way — containment, which is the charter's own wording for the
  carve-out: it binds implementation files *"and not the chain's own artifacts under the
  artifact root"*. **This is a different test from the hook's edit-path skip**, which stays
  segment equality and which the intent freezes; see **Design** for what that leaves diverging.

- **R56.** IF the shared reader records a **plan-level** refusal — R8, R9 or R10, where the
  lifecycle lines themselves are malformed or duplicated — THEN THE shared reader SHALL classify
  that plan as **not live**, so it contributes no claims at all — the requirement charter
  version 21 exists for. A file that plan claimed then reads unclaimed and draws
  `unplanned scope`, which is loud and intended.
- **R57.** R6 SHALL bind only a `files:` line R4 collects from. A `files:` line R4 does not
  collect from contributes nothing and is **not** refused; without this, R6's antecedent reaches
  it and the two requirements disagree about the same line. **Nothing closes a slice**, so what
  R4 drops is a `files:` line before the first `- id:` and only that; a line after the slices
  block is attributed to the last slice. See **Design**.
- **R58.** THE hook's header and the README SHALL stop carrying a count of the warning states
  beside the list of them. **Two copies, not three** — `hooks.json`'s description carries no
  count today. **The forbidden strings are enumerated, not described:** the header's
  `Two distinct states` and the README's `warns on **two** distinct states`, replaced by wording
  that names no number.
- **R59.** THE cross-plan mode SHALL require the plan named by `-Plan` to lie under the
  artifact root resolved from `-Root`, and IF it does not, or IF no artifact root resolves at
  all, THEN THE script SHALL say so and exit 1 rather than compare.
- **R60.** THE shared reader SHALL read `status:` and `finished:` only from the top level of
  frontmatter, and IF a `finished:` line appears nested inside the `slices` block, THEN it SHALL
  record a refusal. **Dropped for a misplaced `files:` line, refused for a misplaced
  `finished:` one** — meaninglessness is dropped, ambiguity on a one-way field fails loudly; see
  **Design**.
- **R61.** WHEN `-Root` is supplied without `-AcrossPlans`, THE script SHALL exit 1 naming the
  flag as cross-plan only, because R24 promises `-Plan <path>` and nothing else behaves exactly
  as it does today.
- **R62.** WHEN a plan-level refusal leaves the repository with no live plan at all, THE gate
  hook SHALL warn under R19 **and** name the refused plan and the rule it broke, so that the
  blocking switch does not refuse every edit on account of one duplicated `status:` line with a
  detail that says nothing about a refusal.
- **R20, revised.** The original read "IF the shared reader records a refusal for a plan, THEN
  THE gate hook SHALL drop that plan's refused claims." That covered a plan-level refusal by
  dropping claims that were not what was wrong, leaving the plan live. Superseded by the pair
  R20-as-it-now-stands and R56; recorded here rather than silently rewritten, because the
  numbering is cited by tests.

## Design

### The shape in one paragraph

One PowerShell file becomes the only thing in the engine that knows how to read a plan's
frontmatter, how to normalise a path, and where a repository keeps its chain. The gate hook and
the disjointness script both load it and stop parsing anything themselves. The hook stops
returning at the first claiming plan and instead collects all of them, which is what lets it
tell "claimed once" from "claimed twice". The script grows a second mode that reads the other
plans under the artifact root and compares. Seven plans get a lifecycle marker so the first real
run of all of this is true, and that marking is the first slice.

### The shared reader: one file, dot-sourced, `.ps1`

`plugins/sdlc-core/scripts/SliceManifestParser.ps1` — a library of functions with no top-level
side effects, loaded by both callers with dot-sourcing:

```
. (Join-Path $PSScriptRoot '..' 'scripts' 'SliceManifestParser.ps1')      # from hooks/
. (Join-Path $PSScriptRoot 'SliceManifestParser.ps1')                     # from scripts/
```

**In the hook, that load sits inside the guarded region, not at file scope.** The hook sets
`$ErrorActionPreference = 'Stop'` and wraps its work in a `try` that exits 0 on anything
unexpected; a dot-source at file scope is outside that guard, so a missing parser would be a
terminating error with a non-zero exit and a stack trace on standard error — which is precisely
what R22 forbids. The snippet above shows what is loaded, not where.

`$PSScriptRoot` rather than `${CLAUDE_PLUGIN_ROOT}`: the hook is invoked through `hooks.json`,
which does interpolate the plugin root, but the script is invoked by an agent following a
skill's command line, and a resolution that only works for one of the two callers is the shape
this change exists to remove.

**`.ps1` and not `.psm1`**, and this is a constraint rather than a taste. `EngineExecutablesImposeNoStack`
globs `*.ps1` and `*.js`; `EveryShippedPowerShellScript_Parses` globs `*.ps1`. A module file is
invisible to both, which would put the densest parsing logic the engine ships in the one file
neither instrument covers. The alternative — widening both globs in this same change — is
recorded below as rejected, and the consequence is that `PluginStructureTests.cs` is **not**
edited, so this change's collision set stays at five files and six claims rather than six and
seven.

The functions it exposes, named by what they answer:

| Function | Answers |
|---|---|
| `Get-SdlcFrontmatter` | the frontmatter block of a file, or `$null` |
| `ConvertTo-SdlcPathKey` | the comparison key for one path, given a base directory |
| `Read-SdlcPlan` | one plan: its slices, its claims, its lifecycle fields, and its refusals |
| `Test-SdlcPlanIsLive` | whether a read plan is live, per R11 |
| `Get-SdlcArtifactRoot` | where this repository keeps its chain, or `$null` |
| `Get-SdlcLivePlan` | every live plan under a root, each already read |

`Read-SdlcPlan` returns refusals as data (R13). Neither caller wants a parser that throws: the
hook must never throw at all, and the script wants to name the rule that was broken rather than
show a PowerShell error.

### Normalisation: textual, segment-wise, and deliberately short of the filesystem

The key for a path is built by: trimming surrounding whitespace; replacing `\` with `/`; if the
path is rooted and lies inside the base directory, removing that prefix; splitting on `/`;
dropping empty segments and `.` segments; popping the previous segment for each `..` that has
one to pop; rejoining with `/`; and lowercasing.

That satisfies every equality R2 lists, and it leaves `.sdlc/CHARTER.md` intact because `.sdlc`
is an ordinary segment — which is the whole difference from today's `TrimStart('.', '/')`, a
call that both readers make identically and that maps `.sdlc/CHARTER.md` and
`.github/workflows/checks.yml` onto strings naming nothing.

**Lowercasing is the comparison key only.** R5 keeps the author's spelling beside it, so a
contested-file report names `src/Storage/Writer.cs` and not `src/storage/writer.cs`. Case
folding is a chosen tradeoff and not a platform guarantee — git is case-sensitive and NTFS can
be — and the intent takes it deliberately, on the charter's Windows-only commitment.

**A trailing separator folds in normalisation and is still refused as a claim**, which reads as
a contradiction and is not. R7 refuses `"src/Storage/"` as a directory entry, because a claim
must be a file path. R2 folds a trailing separator so that an *edited* path arriving with one,
or a claim being compared defensively, does not miss a match. Grammar binds what a plan may
write; normalisation binds what comparison means.

**Rejected: `[System.IO.Path]::GetFullPath`.** It resolves `..`, repeated separators and
separators in one call, which is tempting. The two-argument overload that takes an explicit base
does not exist on .NET Framework, so on Windows PowerShell 5.1 it would anchor to
`[Environment]::CurrentDirectory`, which diverges from `Get-Location` inside PowerShell — a
defect that only appears when the hook is invoked from somewhere other than the repository root,
which is already an admitted blind spot. It also brings in behaviours the intent puts out of
scope: trailing dots, reserved device names, long-path handling. A textual normaliser matches the
enumerated scope exactly and has no hidden inputs.

### Liveness, and which reader learns about it

`Test-SdlcPlanIsLive` is the single statement of R11. It lives in the shared file, so the hook
and the cross-plan mode cannot disagree about it.

**The single-plan mode does not consult it.** `-Plan <path>` reads no status today, the intent
promises it keeps working, and criterion 3 puts finished-awareness in the cross-plan mode only.
What the single-plan mode *does* gain is grammar refusal (R30): a plan whose frontmatter is
spelled in a form no reader accepts fails loudly instead of claiming nothing. That is a verdict
change and it is the intended one — the promise is about the invocation, the output and the
exit-code vocabulary, not about preserving a wrong answer.

### The gate hook: collect, then decide

The hook's current loop returns 0 at the first plan whose claims contain the target, which makes
a second claim structurally unobservable. The new shape:

1. Skip rules, unchanged (R23).
2. Resolve the root through `Get-SdlcArtifactRoot` (R12) — moved, not repaired. Its wrong edges
   travel with it, including returning from the first candidate charter it finds.
3. Read every `<root>/*/plan.md` through `Read-SdlcPlan`, each inside its own `try` (R21), and
   keep the live ones (R11).
4. Normalise the edited path with the same `ConvertTo-SdlcPathKey` the claims went through —
   which is the fourth of the five defects the charter enumerates, the one where the hook
   disagreed with itself.
5. Collect every live plan whose claims contain that key.

| Live plans | Claimants | Result |
|---|---|---|
| none | — | `no approved plan`, detail distinguishing found from live (R19) |
| one or more | 0 | `unplanned scope`, unchanged (R18) |
| one or more | 1 | silent, exit 0 (R17) |
| one or more | 2 or more | `contested file` (R14) |

The emitted line keeps its existing shape and carries the literal label, so the label and the
branch are linked in code for the new state as they already are for `unplanned scope`:

```
[sdlc-gate] Editing Writer.cs. contested file: src/Storage/Writer.cs is claimed by 2 live
plans (audit-log, retention-purge). Settle which chain owns it with whoever owns the other
one and re-plan there; this is not something this edit can fix.
```

**The action string is the part that differs from its two neighbours** (R15). `no approved plan`
tells you to run the plan command; `unplanned scope` tells you to add the file and re-approve.
Both are things the person at the keyboard can do. A contested file is not: the fix is
re-planning a chain that may not be theirs, and under the blocking switch that file is
uneditable by anybody until a human intervenes somewhere else. Naming a remedy the reader cannot
perform is worse than naming none.

**`no approved plan` keeps its label and loses its detail wording.** The label is now a contract
across three files, so it does not move; the sentence behind it becomes "N plan.md found under
`<root>/`, none live (slugs)", because with `finished:` in play "none approved" is simply false
for a repository whose plans all shipped. That edits one assertion in `GateHookTests`.

**Containment, and what still is not proved.** R21 puts each plan read in its own `try` so one
malformed plan cannot take the gate down. Nothing in this repository can make shipped hook code
throw on demand, so that containment is held by inspection — which is one of the eight entries
this change adds to the charter's known-and-unchecked list, and is the largest caveat on calling
the hook a guarantee.

### The disjointness script: a second mode, not a changed one

```
powershell -NoProfile -ExecutionPolicy Bypass -File "${CLAUDE_PLUGIN_ROOT}/scripts/Assert-SliceManifest.ps1" -Plan <root>/<slug>/plan.md -AcrossPlans
```

`-AcrossPlans` is additive: everything the single-plan mode does, then the cross-plan
comparison. One switch rather than a separate script, because the plan skill should run one
command and see one output, and because the two comparisons share the read of the plan under
check.

**Both readers start from the repository root and resolve the artifact root the same way** —
the shared reader's resolution, which R12 moves out of the hook. `-Root` overrides the
*repository* root and defaults to the working directory, exactly as the hook's does; the
artifact root is never passed in and never inferred separately.

**An earlier version of this design inferred the artifact root from the plan's own path — the
parent of the chain directory — and called it a deviation-by-improvement.** It was a defect. The
two are the same place only when the artifact root sits one level below the repository root,
and the documented two-level case is `docs/sdlc`: the hook searches `*/CHARTER.md` and
`*/*/CHARTER.md` precisely for it, an existing test fixture uses it, and it is R55's own worked
example. Under that root the inference yields `docs/`, so a claim written `src/sdlc/Thing.cs`
resolves against `docs/` in the script and against the working directory in the hook — the two
readers disagreeing about one claim, which is the defect R55 exists to prevent and R12's whole
justification.

**It also made `-Root` two flags with one name**: an artifact-root override in Design and a
repository-root base in R53. One question, one answer, one flag.

**The cost argument that motivated the inference does not survive contact with where the cost
lands.** Root discovery dominates the hook because the hook runs on every keystroke. The script
runs once at a plan approval. Removing discovery there optimised the one caller that never
needed it, at the price of a second coordinate system.

`-Root` is the second permanent addition to a shipped command line in this change and belongs
beside `-AcrossPlans` in **Data and contracts**, which is where things expensive to reverse are
listed; it was missing from that section while its own requirement called it permanent.

Output on success, which is R28 and criterion 7:

```
3 slices, 11 files, manifests disjoint
  wave 1: parser
  wave 2: hook, script
read 6 other plan.md under .sdlc/ (1 live, 6 read, 1 refused); no contested files
```

The three counts are an **overlay**: the refused plan here is also one of the six read, and a
claim-level refusal would leave its plan among the live ones too. An earlier worked example read
`1 live, 5 not live, 0 refused`, which sums to six and teaches an implementer a partition that
does not exist.

A comparison that read nothing says `read 0 other plan.md under .sdlc/`, which is the line that
makes a nested chain directory or a renamed `plan.md` visible instead of reassuring. The
single-plan mode prints none of this, because adding counting output there would break the one
behavioural promise this change makes (R24).

On a contested path it names every claimant and exits 1 (R27):

```
CONTESTED src/Storage/Writer.cs: this plan <-> retention-purge
Two live plans claim one file. This is a planning conversation, not something to reconcile
in the merge: settle which chain owns it and re-plan the other.
```

**The artifact-root carve-out is cross-plan only** (R25). Two slices of *one* plan both claiming
`.sdlc/CHARTER.md` is still an overlap and still fails, exactly as today. The carve-out exists
because the version-8 rule makes two check-adding chains claim the charter by construction; it
was never a licence to double-claim inside one plan.

### Where the cross-plan check runs — open question 1, answered

**Both places: the step that writes the plan, and the last act of the gate step.**

The plan skill writes `plan.md` as a draft at step 4 and runs the manifest check there; approval
happens at step 6. Running the cross-plan check only at step 4 leaves a window in which two
sessions both pass the check, each seeing the other as a draft, and both then approve. That
window is the unguarded state this whole change is about, so leaving it open and writing it down
as a limitation would be spending a feature's worth of work and stopping one step short.

Step 6 already exists, and R37 adds an invocation to it rather than a step or a gate — which is
what the intent's non-goal permits. The gate keeps its shape: explicit human approval, then
`status: approved`, then the commit. What it gains is a precondition: the status line is not
written while the check reports a contested path.

**What this does not buy, stated rather than left to be found.** It is prose in a skill, followed
by an agent, with no instrument behind it. The verify skill's SHIP branch gets a prose check in
this change because a wrong write there is irreversible; a missed re-run here costs a warning at
the next edit, which the hook still gives. So the honest position is that the window is narrowed
by an instruction and not closed by a mechanism.

**It joins known-and-unchecked entry 4, which is widened to say what it was always about.** The
entry named the verify skill and its two branches; the class underneath it is larger — **a prose
check reads a document and cannot prove a skill does at runtime what its body says.** Choosing
the wrong branch and never making the call at all are two shapes of that one failure, and both
wait on the same missing instrument: a harness that executes a skill body with its agent bound.
So the entry now reads *a prose check cannot prove a skill does at runtime what its body says*,
and it covers the verify skill's branch and this invocation together.

**The count stays at eight and that is a consequence, not a reason.** An earlier draft of this
paragraph gave "rather than add a ninth entry" as the justification, which is backwards: the
charter spent version 20 establishing that a list growing because its admission rule was applied
honestly is the rule working, and protecting a number is the failure that invariant names. If
these were two classes they would be two entries. They are one, so it is one — and the wording
had to widen for that to be true rather than convenient.

**Rejected: accept the window.** It is the cheaper answer and it was the intent's other branch.
Rejected because the cost of the alternative is one line in a skill body, and because the state
it leaves — two sessions each holding a draft, both passing — is precisely the scenario the
problem section opens with.

**Rejected: run the check only at the gate.** It would report a collision after the user has read
and approved a shape of fan-out, which is the wrong moment to discover a planning bug. Running at
both is not redundant: step 4 is where it is cheap to fix, step 6 is where it is true.

### Does the shrunken live set need softening — open question 2, answered

**No.** Measured against the tree: the seven plans hold 40 claims over 30 distinct paths, of
which **17 are outside the artifact root and carry an extension the hook does not skip.** Those
17 are the paths that go from silent to warning when the marking lands. **One of them** —
`tests/AiSdlc.Tests/TempRepo.cs` — returns to claimed under this chain's own plan. So
**sixteen** paths begin drawing `unplanned scope`, and only when someone edits one.
`tests/AiSdlc.Tests/PluginStructureTests.cs` would make it fifteen, but only on the glob branch
of the `.psm1` constraint — and this spec takes the other branch, so counting it was counting a
decision this document does not make.

Every one of those sixteen belongs to work that has shipped. An edit to them genuinely is
outside any live plan, which is the gate's definition working rather than misfiring, and sixteen
paths is not a volume that gets a tool uninstalled.

**What this answer does not lean on**, because the approved intent already refused it: that the
hook is advisory unless the blocking switch is set, so anyone who disagrees can unset it. The
intent says of exactly that remedy — *"what someone reaches for when a report fills with noise
is unsetting the blocking switch or ignoring the hook, and both of those also lose the
`unplanned scope` gate that works today."* An earlier version of this paragraph offered it as
the fallback, which reinstates an assumption the intent spent a round removing. The answer
stands on the measurement alone: sixteen paths, all on shipped work, each warning only when
edited.

**Rejected: suppress the warning for a file only finished plans claimed.** It would require the
hook to remember which plans used to claim a file, which is state this change explicitly refuses
to keep, and it would hide exactly the class of edit — touching shipped work with no plan — that
the gate was built for.

**Rejected: soften the wording of `unplanned scope`.** The label is now a contract across three
shipped files; changing it costs three edits and a check, to say something less true.

### The marking, and why it is slice one

Six plans keep `status: approved` and gain `finished: true` (R31). `engine-substitutes-declared-tokens`
takes `status: superseded` with no `finished:` line (R32) — its work was never built, so nothing
about it is finished, and `finished: true` beside `status: superseded` is refused by R9, which
this same change ships. Manifests are untouched (R35): what a plan claimed is the record of what
that change touched.

The rule for deciding a plan is finished is not recollection: **every file its manifest claims
exists in git with that chain's work in it**, which a reviewer can check against the diff.

It is the first slice and everything else depends on it. That is not a moral point about
authorisation — it is that the report has to be true before the mechanism that reads it ships,
and `depends_on` expresses "no state exists in which the check is live and the marking is not"
exactly. It also means this repository never passes through a moment with zero live plans, which
under the blocking switch would refuse every code edit.

**The charter is not in the marking slice.** Five register rows naming five test classes that do
not exist yet turn `SilentFailureRegisterTests` red, and nothing is committed on a red build. The
charter edit belongs in the slice that also lands the five classes.

### What the marking cannot fix, carried forward

A plan stops being live in two ways and neither is written by a tool for the common case: only
feature chains get `finished:` written for them, and `superseded` is written by hand. Five of the
six chains that shipped here were task tier and pass through no closing stage. So the report is
true on the day it lands and decays afterwards at a rate nothing measures. The one observation
available argues the decay is slower than it feels — over one day, claims went from 35 to 40 and
distinct paths from 26 to 30 while the number of multi-claimed paths did not move — but one
observation is not a rate. The fix is the closing step, which is item 4 of the open items and not
this change.

**And a parked chain's plan is live.** Four chains carry parked or blocked banners and none has
reached plan stage, so nothing is held today; the first parked *plan* will hold its files for as
long as the park lasts, with no mechanism to release them.

### The five instruments

| Class | Proves | Fault injected to see it fail |
|---|---|---|
| `CrossPlanCollisionTests` | the comparison, over built fixtures, in both readers — **four classes, so four faults**: normalisation, liveness, the third warning state, and the counts line | remove the second-claimant collection from the hook (third state); make a finished plan contribute claims (liveness); **restore `TrimStart('.', '/')` so `.sdlc/CHARTER.md` collapses** (normalisation); **suppress the counts line when nothing was compared** (criterion 7). An earlier version gave two faults for a row R50 requires to name four classes — a row claiming to catch what nothing had been seen to catch |
| `GateHookPackagingTests` | the hook still speaks with its parser present and goes silent without it | delete the parser from the fixture copy of the plugin tree |
| `FrontmatterGrammarTests` | **two classes**: a plan spelled in a form no reader accepts — all five grammar rules, by driving the shipped readers — and **the charter and the schema disagreeing about that grammar** (R51) | relax one rule in the shared reader (the first); **change the rule count in one document only** (the second). The second class and its fault were missing, so R51 was a requirement no named fault could have failed on |
| `VerifyShipBranchTests` | the `finished:` write and the `plan.md` staging appear after the `On **SHIP**:` marker and nowhere in the DO NOT SHIP block, **where that block is bounded from its own marker to the next bold inline marker** and not “from the marker onward” — unbounded, the check fails on a correct file, because in the shipped skill DO NOT SHIP precedes SHIP | move the sentence into the other branch |
| `ShippedProseListTests` | the three warning-state copies and the three manifest-drift copies agree, both directions. **The hook's copy is its header block, bounded from `.DESCRIPTION` to the closing `#>`** — not the whole file, because R19 now has all three labels appear in the hook's *emitted* strings as well, so a grep over the file would pass with the header table deleted. Same bounding problem this document solved for the DO NOT SHIP block and had not noticed here | drop one label from `hooks.json`; delete one entry from the header's two-line state list — it is an indented list, not a table, and a fault has to be injectable by the name it is given |

Each is one class in one file with one `[TestClass]`, which is the only shape
`SilentFailureRegisterTests` actually sees — it reads the first test class per file, scans the
top directory only, and passes a class when its own name or any method name is registered.

**`VerifyShipBranchTests` matches on the bold inline markers, not on headings.** The verify
skill's gate is a single heading containing the literal `DO NOT SHIP`, so a heading match finds
the wrong branch first. The branches are `On **DO NOT SHIP**:` and `On **SHIP**:`. Both halves of
the assertion are required: presence alone passes a skill body that names the transition in both
branches, which is the fatal case.

**`ShippedProseListTests` has a key for each list.** The manifest-drift list already has one —
all three copies carry `unplanned scope` and `did not land` today, checked rather than assumed.
The warning-state list does not, because `hooks.json`'s description paraphrases both labels away,
so the description is rewritten to carry the three literal labels (R39). That is text every user
sees, and it is the intent's decision, not this document's.

What that check does not prove: that a named label corresponds to a branch that exists.
`unplanned scope` and `contested file` are both emitted by the hook and asserted elsewhere;
`no approved plan` appears only in the header comment and in prose. Two of three, and saying so is
not the same as fixing it.

### Files this change touches

Implementation:

- `plugins/sdlc-core/scripts/SliceManifestParser.ps1` — new.
- `plugins/sdlc-core/hooks/sdlc_gate.ps1` — parsing and root resolution removed, collection and
  the third state added, header rewritten (three labels, the false timing claim removed).
- `plugins/sdlc-core/scripts/Assert-SliceManifest.ps1` — parsing removed, `-AcrossPlans` added.
- `plugins/sdlc-core/hooks/hooks.json` — `description` rewritten.
- `plugins/sdlc-core/skills/plan/SKILL.md` — the cross-plan invocation at step 4 and at step 6.
- `plugins/sdlc-core/skills/verify/SKILL.md` — the `finished:` write on the SHIP branch.
- `plugins/sdlc-core/skills/sdlc-router/references/artifact-schemas.md` — the `superseded`
  correction, the stale forward references, the cross-plan invocation.
- `plugins/sdlc-core/.claude-plugin/plugin.json` — version bump.
- `README.md` — the third state, and the real instrument in place of the script that never
  existed.

Tests:

- `tests/AiSdlc.Tests/CrossPlanCollisionTests.cs`, `GateHookPackagingTests.cs`,
  `FrontmatterGrammarTests.cs`, `VerifyShipBranchTests.cs`, `ShippedProseListTests.cs` — new.
- `tests/AiSdlc.Tests/GateHookTests.cs` — the `none approved` wording, and the case-sensitivity
  behaviour nothing pins today.
- `tests/AiSdlc.Tests/SliceManifestTests.cs` — the normalisation cases that change verdict.
- `tests/AiSdlc.Tests/TempRepo.cs` and `Repo.cs` — two colliding plans in one fixture, and a copy
  of the plugin tree into a temp repository so the packaging check can inject into a fixture
  rather than into the live plugin directory. `Repo.cs` finds the repository root by walking up
  to the first ancestor holding a `.claude-plugin` directory; both the repository root and each
  plugin hold one, which is what the walk stops at.

Chain artifacts:

- The seven `plan.md` files being marked; `.sdlc/CHARTER.md`; `.sdlc/OPEN.md`;
  `.sdlc/DIRECTION.md`; this chain's own artifacts.

**Five of the files outside the artifact root are claimed by another plan today** — the schema
reference (by a superseded plan and a finished one), the engine plugin's version file, the
README, the test fixture and the verify skill: five files, six claims, three plans. All six are
freed by the marking, which is why the marking goes first.

## Data and contracts

Everything expensive to reverse in this change is a **shape other people's files must have** or
**text that ships to every user**. Nothing is persisted at runtime and there is no wire format.

### The plan frontmatter grammar — five rules, enforced from this change

Stated in the charter at versions 9, 12 and 21 and published in the artifact-schemas reference.
This change is the first thing that enforces them.

| Rule | Form | Enforced by |
|---|---|---|
| 1 | a `files:` entry is a quoted string on the `files:` line | R6 |
| 2 | a `files:` entry is a file path, never a directory, never a glob | R7 |
| 3 | `status:` is unquoted and exactly one of the declared values | R8 |
| 4 | `finished:` is `true` or `false`, unquoted, alone on its line; `true` requires `status: approved`; `false` is legal anywhere; absent means not finished | R9 |
| 5 | exactly one `status:` line and at most one `finished:` line | R10 |

**Migration: none, measured rather than assumed.** All seven plans conform to the first four
rules, and all seventeen chain artifacts carrying frontmatter conform to the fifth. Parsing all
seven manifests the strict way and the loose way yields the identical 40 claims, so nothing the
loose reading sees is lost by the strict one in this repository.

**The rules bind `plan.md` and no other shape.** Two of the five are plan-only by the schema;
the other three are stated there for all five artifact shapes. Widening the check to specs,
intents and verification records is a smaller job than this change's scope allows and belongs
with whoever next touches the grammar. The fifth rule is the one this most strains, because two
`status:` lines are as dangerous in a spec as in a plan.

**Two spellings the grammar answers here because a one-way field leaves no room not to:** two
`finished:` lines, and a `finished:` line nested inside the `slices` block. Both are **rejected**
(R10, and R4's slice-context rule for the nested case). Reject rather than first-wins or
last-wins, because the other rules exist so that a form no reader agrees on fails loudly, and
because `finished: true` followed by `finished: false` differs under the three policies on
whether a plan's claims are released. The harness's own `Frontmatter.cs` answers both the other
way — its dictionary assignment makes two lines last-wins with no error, and its field regex
anchors on a non-whitespace first character so a nested line is invisible. That is a helper
several suites call, it is not a shipped reader, and this change does not build the grammar check
on it (R47).

### The `finished:` field

Additive beside `status:`, on `plan.md` only, one-way. Every existing reader that gates on
`status: approved` keeps behaving as it does today, and a repository written by a newer plugin
stays readable by an older one — which is why it is a field and not a status value. A status
value would have made an older plugin see zero approved plans and, under the blocking switch,
refuse every code edit in the repository.

**One-way, and nothing holds it that way.** Editing `true` back to `false`, or deleting the line,
silently re-acquires every file that plan claimed. The grammar check reads a file's current state
and has no memory; no check in this suite reads git history; and the hook cannot acquire the
habit, because shelling out to git on every keystroke is the cost this change budgets against.
This is the weakest of the eight known-and-unchecked admissions and the one the intent flags as
arguable.

### Three labels, now a contract across three files

`no approved plan`, `unplanned scope`, `contested file` — written literally in the hook's header,
the README's gate-hook section and `hooks.json`'s `description`, and compared in both directions
by `ShippedProseListTests`. The cost is stated with the decision: a fourth warning state later
costs three edits plus the check, in text that ships to every user. That is the point rather than
the price — three descriptions a reader is forced to change together beat three that quietly stop
describing the same thing.

### The cross-plan invocation, and the four spellings of the existing one

`-AcrossPlans` **and `-Root`** are new flags on an existing command line, so nothing anyone types today breaks. Both are permanent: `-AcrossPlans` selects the mode, `-Root` names the repository root every claim is resolved against and defaults to the working directory. `-Root` was missing from this section while R53 called it a permanent addition to a shipped command line, which is precisely what this section exists to list.
The literal single-plan command appears in four places — the plan skill, the schema reference,
the script's own example block, and a validation row in a shipped chain's plan — and they are
four different spellings rather than one literal with whitespace noise. This change does not
reconcile them and adds no fifth list; it edits the two that need the new mode (the plan skill and
the schema reference) and leaves the others.

### What changes verdict for existing inputs

Named because "no behaviour change" would be false, and because these are the surprises:

- `-Plan <path>` now **rejects** a plan whose frontmatter breaks any of the five grammar rules.
  Today such a plan passes while claiming nothing.
- `-Plan <path>` now reports `a//x` against `a/x`, and `a/../a/x` against `a/x`, as overlaps —
  correctly, where today it does not — and stops reporting `.sdlc/CHARTER.md` against
  `sdlc/CHARTER.md` as one — correctly, where today it does.
- The hook now recognises **fewer** claims: a `files:` line outside any slice, or one matched by
  the old unanchored substring test such as `profiles:`, stops contributing. Files the hook
  quietly treated as claimed start drawing `unplanned scope`. Noisier, not less safe, and
  measured not to occur in this repository.
- The hook now compares **case-insensitively**, so a file claimed in one casing and edited in
  another stops warning. A fix on Windows, and a change to shipped behaviour that nothing pins
  today in either direction.

**No migration, and the reason has a shelf life.** The marketplace points at a local filesystem
path, so the installed population is one machine. Plugins are cached per version, so an older
engine and a newer one would disagree about which paths collide with no version gate to notice.
That is free only while the population is one, and closing open item 6 — making the framework
installable elsewhere — reopens this.

## Failure modes

| What breaks | What the system does |
|---|---|
| A plan's frontmatter breaks a grammar rule | For the plan under check the script names it and exits 1 (R30); for any other plan it names it, drops what the hook drops — claims for a claim-level refusal, the plan for a plan-level one — and continues (R29). The hook drops the refused claims (R20), or treats the plan as not live if the refusal is plan-level (R56) | The script names the plan and the rule and exits 1. The hook drops that plan's refused claims, says nothing extra, and the file then reads as unclaimed — so the warning is loud and labelled with the wrong reason. Misattribution, not silence, and stated rather than given a fourth label. |
| One plan raises while being read by the hook | That plan is excluded, the rest are compared (R21). Held by inspection: nothing here can make shipped hook code throw on demand. |
| The shared parser is missing from an install | The hook exits 0 and says nothing — the gate is silently dead. `GateHookPackagingTests` is what notices, in this repository's CI only. |
| The artifact root resolves wrongly | Empty comparison, reported green. The script's counts make it visible (R28); the hook cannot say so without speaking on every keystroke, and that is admitted rather than fixed. |
| A chain directory is nested, or `plan.md` is renamed | Same empty comparison. Same split: visible in the script, invisible in the hook. |
| A finished plan is un-finished by hand | Every file it claimed is silently re-acquired. No instrument. |
| Two sessions approve inside the same window | Narrowed by R37's re-run at the gate; not closed. The next edit to a contested file warns. |
| A claim goes stale because a file was renamed | Never noticed — claims are never checked against the filesystem, by decision. |
| The hook gets slower | Nothing watches after the one-time measurement in R49. Root discovery is now in shared code, so a regression there slows every edit in every repository. |

## Flagged concerns

- **The no-migration position has an expiry date that is not this change's to set.** The
  normalisation rewrite changes which paths two plans are judged to collide on, and plugins are
  cached per version, so two machines on two engine versions would disagree with nothing to
  detect it. This is free today only because the marketplace points at a local path and the
  installed population is one. Open item 6 — making the framework installable elsewhere — closes
  that window, and whoever closes it inherits this. *Owner: peter.* **Not blocking:** the intent
  takes this position as a non-goal and the measurement behind it stands. It is flagged so that
  the dependency is recorded in the spec and not only in the open items.

Nothing here touches security, privacy, compliance, personal data or spend. The engine runs on
one machine, from a terminal, against files in git.

## Test strategy

Every new check that exercises the grammar drives the shipped readers as external processes
(R47), and no new check reads a `plan.md` under this repository's own chain tree (R48). No check reads a `plan.md` under
this repository's own chain tree — which means a plan written with a block-style `files:` list
fails when the script runs at plan approval, and never in continuous integration. **On an edit
it does not fail at all**: per R20 the refused claims are dropped, and the file is misattributed
as `unplanned scope` — loud and wrongly labelled rather than silent, which is a weaker claim
than an earlier version of this paragraph made.

### Requirement coverage

| Requirement | Instrument |
|---|---|
| R1 | `GateHookPackagingTests` — **both callers, symmetrically**: with the reader removed from a fixture copy of the plugin tree, the hook goes quiet and the script names the absence. The one-sided version proved neither half of R1 — a hook that goes quiet is equally consistent with a script that kept its own private parse, and with a repository shipping two readers |
| R2, R3, R5 | `CrossPlanCollisionTests` — each enumerated equality as a claim/edit pair through the hook, and as a claim/claim pair through the script; `.sdlc/CHARTER.md` asserted to survive. **Except the trailing separator, which has no reachable test**: R7 refuses a claim ending in one before comparison, and an edited path arriving as a directory is not a file edit. It is folded defensively and cannot be exercised from either direction — said here rather than counted as covered |
| R4 | `FrontmatterGrammarTests` — a `files:` line outside a slice, and a `profiles:` line, contribute nothing |
| R6–R10 | `FrontmatterGrammarTests` — one method per rule, each driving the script and asserting the named refusal and exit 1 |
| R11 | `CrossPlanCollisionTests` — approved-and-finished, draft and superseded plans each contribute no claims, in both readers |
| R12 | `GateHookTests` — the existing non-default-root case, unchanged, proving the move was a move |
| R13 | **No instrument of its own**, and the delegation is to the callers rather than the reader: R21 and R29 prove the hook stays silent and the script names rather than throws |
| R14, R15, R17 | `CrossPlanCollisionTests` — two live plans claiming one **code** file in a throwaway repository, edited; asserts the label, the path, both chain names and the action string; one claimant stays silent |
| R16 | `CrossPlanCollisionTests` — the same fixture with the blocking switch set, exit 2 |
| R18, R19, R23 | `GateHookTests` — existing methods. **Two assertions move, not one**: one asserts `none approved` and the other `No plan.md exists`, and the single replacement sentence in Design collapses both branches. R19's new literal is asserted here. R23's tool-matcher clause is picked up by `ShippedProseListTests`, which already parses `hooks.json` — nothing in the suite reads `matcher` today |
| R20 | `FrontmatterGrammarTests` — a refused plan in a hook fixture draws `unplanned scope` and no extra line |
| R21 | `CrossPlanCollisionTests` — one unreadable plan beside two good ones, collision still reported |
| R22 | `GateHookPackagingTests` — parser removed, exit 0, no output, no stack trace |
| R24 | `SliceManifestTests` — every existing method, unchanged, plus a method asserting no count line in single-plan output |
| R25, R26, R27 | `CrossPlanCollisionTests` — fixtures for a contested path, for an artifact-root path that must not be reported, and for a re-approval where the plan under check is already `approved` on disk |
| R28 | `CrossPlanCollisionTests` — the counts line, including the zero-other-plans fixture |
| R29, R30 | `FrontmatterGrammarTests` |
| R31, R32, R35 | Reviewed against the diff at plan approval; the rule is that every file a marked plan claims exists in git with that chain's work in it |
| R33, R34 | `VerifyShipBranchTests` — the transition and the staging appear after the SHIP marker and nowhere in the DO NOT SHIP block |
| R36, R37, R38 | Prose in the plan skill. **No instrument** — recorded under known-and-unchecked entry 4, widened to the class: a prose check cannot prove a skill does at runtime what its body says |
| R39, R40 | `ShippedProseListTests`, both directions, both lists |
| R41–R44 | `ShippedProseListTests` for the drift list, **and for the two literal-absence cases that were going to be left to review**: that the schema no longer claims `/sdlc-core:plan` writes `superseded`, and that the hook's header no longer claims a per-edit cost of ~150ms. Both are greps over files that check already opens, and the suite has precedent for exactly this shape in `EveryEngineSkill_CitesThePlainLanguageRule` and `EngineInstructions_NameNoPackFile`. These are the known-false shipped lines this change exists to have found; leaving them to review is how the next one survives |
| R45 | Reviewed; the semver *increment* has no instrument, which the charter already records |
| R46 | `SilentFailureRegisterTests`, already in the suite, both directions |
| R47, R48 | By review of the five new files |
| R49 | A validation row in `plan.md` carrying **four** medians — before and after on each of the two fixtures, (a) 500 top-level directories with seven plans and (b) forty plans in a shallow tree. An earlier version of this row said "both medians, on the same 500-directory fixture", which described the measurement R49 had before the second fixture was added to it |
| R56 | `FrontmatterGrammarTests` — a plan carrying `status: draft` and a later `status: approved`, beside one live plan claiming the same file: the hook reports no contested file and warns `unplanned scope`, proving the malformed plan contributed nothing. **The one that closes charter v21's hole**, and the one an earlier draft left open by dropping only the refused claims |
| R57 | `FrontmatterGrammarTests` — a `files:` line outside any slice: no claims contributed and no refusal recorded, the two halves asserted together |
| R58 | `ShippedProseListTests` — no copy carries a count of the warning states; fault: put "two distinct states" back over three bullets |
| R59 | `CrossPlanCollisionTests` — `-Plan` at a temp path with the working directory elsewhere: the script refuses and names it, rather than comparing against whatever root the cwd resolves. **The shape this repository's own helper already produces** |
| R60 | `FrontmatterGrammarTests` — a `finished: true` nested under `slices` on an approved plan: refused, and the plan's claims still counted. Fault: read lifecycle lines at any indentation |
| R61 | `SliceManifestTests` — `-Plan x -Root y` without `-AcrossPlans` exits 1 naming the flag |
| R62 | `GateHookTests` — one plan, duplicated `status:`, no other plan: the warning names the refusal as well as the no-live-plan state |
| R51 | `FrontmatterGrammarTests` — the charter and the schema compared on rule count and each rule's key phrase, both directions |
| R52 | Reviewed at the gate that approves the charter edit; the version and the log row are the visible half of that approval |
| R53 | `CrossPlanCollisionTests` — a fixture with a non-default root, proving the script and the hook agree on the same claim |
| R54 | Prose in the plan skill. **No instrument** — same entry 4 as R36–R38 |
| R55 | `CrossPlanCollisionTests` — a claim on `src/sdlc/Thing.cs` under a `docs/sdlc` root, asserted identical between the two readers |
| R50, rows half | `SilentFailureRegisterTests` — the same instrument as R46, which compares rows against classes in both directions, so a row missing or naming nothing fails |
| R50, entries half | **No instrument, and deliberately none.** Nothing counts the known-and-unchecked list, because a check asserting it holds eight entries is a hand-maintained tally of the kind charter v19 removed from that same document. It is reviewed at the gate that approves the charter edit. Stated rather than left blank: an earlier version of this table omitted R50 entirely, which reads as an oversight rather than a decision |

### Mutation testing

Every one of the five rows is mutation-tested: the fault named in the instruments table is
injected, the named check must fail, and the real output is seen. A check that has never been
seen to fail is a check nobody should trust.

**The faults live in the instruments table and nowhere else.** Where a row carries more than
one silent-failure class it names more than one fault — `CrossPlanCollisionTests` names four and
`FrontmatterGrammarTests` two. An earlier draft also named faults in the coverage table, which
made two lists of injected faults with nothing comparing them, inside the document that cites
that rule as the reason for R51.

### The performance fixtures

**Two, because one of them cannot fail.**

**(a) 500 directories at the top level, holding seven plans** — isolates root discovery.
Breadth at the top two levels is what discovery globs, so depth and total count do not reproduce
the figure.

**(b) Forty plans in a shallow tree** — isolates the per-plan term, which is the term this change
*grows*: a parser dot-sourced per invocation, every claim normalised segment-wise, and all
claimants collected instead of returning at the first.

**The arithmetic, shown rather than asserted.** On (a) the intent's measurements put discovery at
roughly 880ms of an 1188ms run against about 2.75ms per plan, so the budget's headroom of
80–100ms sits over a per-plan term worth around 20ms in total — the new parsing could grow an
order of magnitude and pass. On (b) the run is a few hundred milliseconds, so the 50ms floor
still dominates the 15% term and the budget tolerates a per-plan regression on the order of 45%.
**That is far better than (a) and it is not tight**, which is worth saying plainly: (b) catches a
gross regression in the term that moves, not a subtle one. A tighter bound would need a baseline
machine this repository does not have.

Both before-figures are re-measured on their own fixture rather than taken from the earlier
flat-tree and 900-directory numbers, because comparing a 500-after against a 900-before would
show an improvement the change did not make.

## Out of scope

Every non-goal from the intent, carried forward, plus what design added.

From the intent:

- Not a lock and not a reservation; a claim is a side effect of a live plan existing.
- Not a record of who is working on what — no session identity, no activity over time.
- Not a queue: it detects and refuses loudly, never serialises.
- Not atomic. Two sessions can both approve; the collision surfaces at the next edit or the next
  approval.
- Not cross-branch or cross-worktree. The hook sees a collision only when both live plans are on
  disk in one working tree, so a worktree per session trades this protection for separation of
  build output.
- Not the other collision surfaces. Seven were measured beside this one, the git index the worst
  of them; all stay handled by practice.
- Not the artifact files. Nothing under the artifact root is compared across plans.
- Nothing rewrites a plan automatically. The seven are marked by a person in a reviewable commit.
- Nothing stages or commits on anyone's behalf.
- No slice manifest is rewritten on a finished plan.
- A claim is never checked against the filesystem; claims go stale silently when a file is
  renamed.
- The hook's existing skip rules are unchanged, wrong edge cases included — the artifact-root
  test is segment equality, and `bin/`, `obj/` and `node_modules/` are not skipped on edits.
- The hook's tool matcher is not widened. A `git mv` never reaches it, and "three tools" is this
  repository reading an unanchored alternation rather than a behaviour anyone pinned.
- `plugins/sdlc-core/workflows/parallel-build.js` is not edited and the JavaScript parser is not
  reconciled. Its comment claiming all three parsers normalise identically stays false — less
  false, which is not the same thing.
- The scaffold executor's duplicated root resolver and the router's competing notion of an active
  chain are not fixed here.
- The plan skill gains no new gate and no new step.
- No collision check runs at the intent or the spec gate.
- `slug:` is not pinned to the chain directory name; the report names a chain by its directory.
- The extracted parser is not a supported extension point.
- The README runtime-table check is out of scope.
- No check reads a `plan.md` under this repository's own chain tree, the grammar check included.
- A collision on a file nobody edits and no plan re-approves is never reported. Both mechanisms
  are event-driven.
- No migration for repositories running an older engine.
- The hook's root resolution moves unchanged, wrong edges included.
- The cross-plan mode's cost at approval is unbounded in plan count, deliberately.
- Warning volume is not in scope.
- This is not status tracking. A plan's status is a parser input at the moment of a check.

Added during design:

- **The grammar check binds `plan.md` only.** Specs, intents, charters and verification records
  are outside it, including for rule five, which is the one that most strains the boundary.
- **The four spellings of the single-plan command line are not reconciled**, and no fifth list is
  added; only the two copies that need the new mode are edited.
- **`Frontmatter.cs` is not made the grammar check's foundation**, and its last-wins and
  nested-line behaviours are not repaired. It is a harness helper, not a shipped reader.
- **`PluginStructureTests.cs` is not edited**, because the parser ships as `.ps1` and neither
  glob needs widening.
- **No standing performance check.** A timing assertion in continuous integration is flaky by
  nature and this repository has no baseline machine, so R49 is enforced once, by the person
  reading the validation row.

## Planning constraints this spec hands to `plan.md`

Not design, but the plan cannot be cut without them:

- **The marking is slice one** and every other slice depends on it.
- **`.sdlc/CHARTER.md` is not in the marking slice.** Five register rows naming classes that do
  not exist turn `SilentFailureRegisterTests` red.
- **All five rows and all five new test classes land in one commit.** That check reads the live
  charter and enumerates the live test directory in both directions, so splitting them leaves the
  tree red in between, and nothing is committed on a red build.
- **`TempRepo.cs` and `Repo.cs` are shared by three suites**, so they sit in exactly one slice.
- **Every chain artifact this change writes is declared** — the seven plans, the charter, the
  schema reference, the open items and the direction. The artifact-root carve-out means no
  instrument would ever report the omission, which is exactly why the declaration is the record.

## Open questions

None outstanding. The intent's two open questions are answered in **Design**; the remaining
uncertainty is recorded as flagged concerns, failure modes, or entries on the charter's
known-and-unchecked list rather than left as questions with no owner.

## What this document got wrong before it got it right

Recorded the way the intent records it, and gathered here rather than left inside the
requirements it corrects. **Every one of these was written into a requirement as a paragraph
beginning "an earlier version of this…"** — twenty-six of sixty-two carried one, which turned
the section an engineer builds from into a changelog and fused two requirements into one bullet
where a patch prepended prose. The corrections are worth keeping; interleaving them was not.

**Round one — four requirement pairs giving opposite instructions.**

- **R6 narrowed a shape the charter freezes**, demanding a flow-style `files:` list when the
  charter's rule is that an *entry* is a quoted string on the line and both shipped readers
  accept the bracketless form.
- **R20 and R11 left open the hole charter version 21 was amended to close.** A lifecycle
  refusal produces no refused *claims*, so dropping them left the plan live.
- **R19 ordered the hook to emit a literal the same document said it never emits**, against an
  instrument that could not have failed on the clause.
- **R28 and R29 contradicted each other** — exit on any refusal made the refused count always
  zero wherever it printed.
- **R49's budget could not fail on the fixture R49 prescribed.**
- Smaller: R47 was unsatisfiable for two of five classes; rule 5 says *exactly one* `status:`
  line and R10 caught only duplicates; R15's second clause was a judgement no test could make;
  R1's instrument proved neither half of R1; the dot-source as drawn sat outside the hook's
  guarded region.

**Round two — three of its five findings were round one's repairs.**

- **The coordinate system.** Design inferred the artifact root from the plan's path while R53
  named a repository root, so `-Root` was two flags with one name — and the inference is wrong
  for any artifact root more than one level down.
- **R48 carried the defect just fixed in R47, one line above it.**
- **R29's repair was too wide**, hard-stopping every approval in the repository on a malformed
  line in an unrelated chain.
- **R23 froze a skip rule the hook does not have** — the noisy-directory list is used once,
  inside root resolution.
- `R20a` was a new identifier shape inserted into a numbering the charter freezes as an API;
  renumbered R56.

**Round three — four of its five findings were round two's repairs.**

- **R29 and R20 gave opposite verdicts on the same repository.** The round-two narrowing removed
  the asymmetric exit code and left the asymmetric granularity: dropped by plan in the script,
  by claim in the hook, so approval passed while the file became uneditable.
- **R25 and R55 exempted implementation files charter invariant I6 binds**, by making both
  readers use segment equality "so they agree" when the charter's carve-out is containment —
  quietly performing an amendment. The fix was that the two tests are different objects.
- **The cross-plan mode was undefined for the invocation shape this repository's own test helper
  produces**, which would have compared a fixture's claims against the live chain tree.
- **The nested `finished:` spelling** the intent handed to the spec was answered by citing a
  requirement that governs `files:` collection.
- **R50 said nine in one sentence and eight in the next.**
- And, having renumbered `R20a` for its identifier shape, the next two requirements were drafted
  as `R6a` and `R39a` in the same pass.

**The pattern, which is the part worth carrying forward.** Round one attacked the document;
rounds two and three attacked the repairs, and found more there than in the original both times.
Patching a document of this size generates defects at roughly the rate it removes them, and the
signal that it is happening is structural rather than local — a requirements section turning
into a changelog, a requirement drifting eleven places from the one that references it, two
tables of injected faults with nothing comparing them. None of that is visible to a round aimed
at correctness, and all of it is visible on one cold read.

## End-to-end verification

One check, run by hand, that proves the whole feature:

1. Build a throwaway repository containing a charter, a code file `src/Storage/Writer.cs`, and
   **two** chain directories whose plans both read `status: approved` and both claim that file.
2. Feed the shipped hook an edit payload for `src/Storage/Writer.cs` with the plugin installed
   from this working tree. **Expected:** one line on standard error carrying `contested file`,
   the path, and both chain directory names, and an action naming the other chain rather than a
   fix the editor can perform. With `SDLC_GATE=block` set, exit code 2.
3. Add `finished: true` to one of the two plans and repeat. **Expected:** silence, exit 0.
4. Run the disjointness script in cross-plan mode against the still-live plan. **Expected:** the
   wave plan, then a line stating how many other plans were read, how many were live and how many
   were refused; exit 0. Remove the `finished: true` and repeat: the contested path is named with
   both chains, exit 1.
5. Run `dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj` and read the real output. **Expected:**
   green, with the five new classes present and the register check passing in both directions.

Step 2 is the criterion the intent calls the guarantee, and steps 3 and 4 are what prove the
lifecycle field reaches both readers. The extension in step 1 is load-bearing: the hook skips
markdown, text, json, yaml and toml, and skips any path with a segment equal to the artifact
root's leaf, so a fixture colliding on an artifact would pass while proving the opposite.
