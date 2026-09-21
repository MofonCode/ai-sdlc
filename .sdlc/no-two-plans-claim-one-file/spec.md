---
slug: no-two-plans-claim-one-file
status: draft
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
- **R6.** IF a `files:` line's value is not a flow-style list of quoted strings, THEN THE shared
  reader SHALL record a refusal naming the plan, the slice and the line, rather than
  contributing no claims in silence.
- **R7.** IF a `files:` entry ends in a separator or contains `*` or `?`, THEN THE shared reader
  SHALL record a refusal naming it as a directory or glob entry.
- **R8.** IF frontmatter carries a `status:` value that is quoted, or that is not exactly one of
  `draft`, `approved` or `superseded`, THEN THE shared reader SHALL record a refusal naming the
  value it found.
- **R9.** IF frontmatter carries a `finished:` line whose value is not exactly `true` or `false`
  unquoted with nothing else on the line, or carries `finished: true` without
  `status: approved`, THEN THE shared reader SHALL record a refusal naming the offending line.
- **R10.** IF frontmatter carries more than one `status:` line, or more than one `finished:`
  line, THEN THE shared reader SHALL record a refusal naming the count it found.
- **R11.** THE shared reader SHALL classify a plan as live when, and only when, its frontmatter
  carries `status: approved` and does not carry `finished: true`; a `draft`, a `superseded` and
  an approved-and-finished plan are each not live, and a plan carrying no `finished:` line is
  not finished.
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
- **R15.** WHEN the gate hook reports a contested file, THE message SHALL name settling the
  claim with the owner of the other chain as the action, and SHALL NOT offer an action the
  person editing the file can take alone.
- **R16.** WHILE `SDLC_GATE` is `block`, THE gate hook SHALL exit 2 on a contested file, as it
  does on its other two warnings.
- **R17.** WHEN exactly one live plan claims the edited path, THE gate hook SHALL exit 0 and
  write nothing.
- **R18.** WHEN at least one live plan exists and none of them claims the edited path, THE gate
  hook SHALL warn with the literal label `unplanned scope`, as it does today.
- **R19.** WHEN no live plan exists, THE gate hook SHALL warn with the literal label
  `no approved plan`, and its detail SHALL distinguish how many `plan.md` files were found from
  how many of them were live.
- **R20.** IF the shared reader records a refusal for a plan, THEN THE gate hook SHALL drop that
  plan's refused claims, continue comparing every other plan, and emit no additional message.
- **R21.** IF reading or parsing one plan raises, THEN THE gate hook SHALL exclude that plan and
  continue, so that one malformed plan cannot disable the gate for every file in the repository.
- **R22.** IF the shared reader file is absent or unreadable, THEN THE gate hook SHALL exit 0,
  write nothing, and print no stack trace.
- **R23.** THE gate hook SHALL keep its existing skip rules unchanged — the same extensions, the
  same bare filenames, the same noisy directories, and the same artifact-root segment test — and
  SHALL keep its existing tool matcher unchanged.

### The disjointness script

- **R24.** WHEN invoked with `-Plan <path>` and nothing else, THE disjointness script SHALL
  accept the same parameter, emit the same output lines on success, and use the same exit-code
  vocabulary — 0 for pass and 1 for any failure — as it does today.
- **R25.** WHEN invoked in its cross-plan mode, THE disjointness script SHALL compare the claims
  of the plan under check against the claims of every live plan under the artifact root other
  than that plan, and SHALL exclude from that comparison every claim whose path lies under the
  artifact root.
- **R26.** WHEN invoked in its cross-plan mode, THE disjointness script SHALL contribute the
  claims of the plan under check whatever that plan's own `status:` says, and SHALL exclude that
  plan from the set it is compared against by resolved path rather than by slug.
- **R27.** WHEN the cross-plan comparison finds a contested path, THE disjointness script SHALL
  name the path and the chain directory of every live plan claiming it, and SHALL exit 1.
- **R28.** WHEN invoked in its cross-plan mode, THE disjointness script SHALL print how many
  `plan.md` files it read, how many were live, and how many it refused, including when any of
  those counts is zero, so that a comparison that found nothing is distinguishable from one that
  compared nothing.
- **R29.** IF the shared reader records a refusal for any plan the cross-plan mode reads, THEN
  THE disjointness script SHALL name the plan and the rule broken, and SHALL exit 1.
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
- **R47.** EVERY new check SHALL drive the shipped readers as external processes, the grammar
  check included; none SHALL reimplement the manifest grammar in C#.
- **R48.** NO new check SHALL read a `plan.md` under this repository's own chain tree; every one
  SHALL run over fixtures it builds.
- **R49.** THE gate hook's wall-clock cost SHALL not regress beyond the greater of 15% or 50ms,
  median of five runs, measured before and after on one fixture of 500 directories at the top
  level holding seven plans, with both figures taken on that same fixture and pasted into
  `plan.md`.
- **R50.** THE charter SHALL gain five register rows naming the five new classes, and eight
  known-and-unchecked entries, in the slice that declares `.sdlc/CHARTER.md`.

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

**The root comes from the plan's own path** — the parent of the chain directory — with an
optional `-Root` to override it. That is cheaper and more reliable than discovery, since the
caller already knows where the plan is, and it removes the root-discovery term the intent's cost
admission expected to dominate here. Stated as a deviation rather than slipped in: the intent
records the cross-plan cost as unbounded and dominated by root discovery; this design keeps it
unbounded in plan count and removes the discovery term. The hook keeps discovery because it has
only an edited path to start from.

Output on success, which is R28 and criterion 7:

```
3 slices, 11 files, manifests disjoint
  wave 1: parser
  wave 2: hook, script
read 6 other plan.md under .sdlc/ (1 live, 5 not live, 0 refused); no contested files
```

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
17 are the paths that go from silent to warning when the marking lands. Two of them —
`tests/AiSdlc.Tests/TempRepo.cs`, and `tests/AiSdlc.Tests/PluginStructureTests.cs` if the glob
branch had been taken — return to claimed under this chain's own plan. So roughly fifteen paths
begin drawing `unplanned scope`, and only when someone edits one.

Every one of those fifteen belongs to work that has shipped. An edit to them genuinely is outside
any live plan, which is the gate's definition working rather than misfiring. Fifteen paths is not
a volume that gets a tool uninstalled, and the remedy for anyone who disagrees already exists and
costs nothing: the hook is advisory unless the blocking switch is set.

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
| `CrossPlanCollisionTests` | the comparison, over built fixtures, in both readers | remove the second-claimant collection from the hook; make a finished plan contribute claims |
| `GateHookPackagingTests` | the hook still speaks with its parser present and goes silent without it | delete the parser from the fixture copy of the plugin tree |
| `FrontmatterGrammarTests` | all five grammar rules, by driving the shipped readers | relax one rule in the shared reader |
| `VerifyShipBranchTests` | the `finished:` write and the `plan.md` staging appear after the SHIP marker and nowhere in the DO NOT SHIP block | move the sentence into the other branch |
| `ShippedProseListTests` | the three warning-state copies and the three manifest-drift copies agree, both directions | drop one label from `hooks.json` |

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

`-AcrossPlans` is a new flag on an existing command line, so nothing anyone types today breaks.
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
| A plan's frontmatter breaks a grammar rule | The script names the plan and the rule and exits 1. The hook drops that plan's refused claims, says nothing extra, and the file then reads as unclaimed — so the warning is loud and labelled with the wrong reason. Misattribution, not silence, and stated rather than given a fourth label. |
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

Every new check drives the shipped readers as external processes (R47) and runs over fixtures it
builds (R48). No check reads a `plan.md` under this repository's own chain tree — which means a
plan written with a block-style `files:` list fails when a reader runs, at plan approval or on an
edit, and never in continuous integration.

### Requirement coverage

| Requirement | Instrument |
|---|---|
| R1 | `GateHookPackagingTests` — the hook stops speaking when the file is removed from a fixture copy of the plugin tree |
| R2, R3, R5 | `CrossPlanCollisionTests` — each enumerated equality as a claim/edit pair through the hook, and as a claim/claim pair through the script; `.sdlc/CHARTER.md` asserted to survive |
| R4 | `FrontmatterGrammarTests` — a `files:` line outside a slice, and a `profiles:` line, contribute nothing |
| R6–R10 | `FrontmatterGrammarTests` — one method per rule, each driving the script and asserting the named refusal and exit 1 |
| R11 | `CrossPlanCollisionTests` — approved-and-finished, draft and superseded plans each contribute no claims, in both readers |
| R12 | `GateHookTests` — the existing non-default-root case, unchanged, proving the move was a move |
| R13 | Covered by R21 and R29's instruments: the hook stays silent and the script names rather than throws |
| R14, R15, R17 | `CrossPlanCollisionTests` — two live plans claiming one **code** file in a throwaway repository, edited; asserts the label, the path, both chain names and the action string; one claimant stays silent |
| R16 | `CrossPlanCollisionTests` — the same fixture with the blocking switch set, exit 2 |
| R18, R19, R23 | `GateHookTests` — existing methods, with the `none live` wording updated |
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
| R41–R44 | `ShippedProseListTests` for the drift list; the rest by review of the diff |
| R45 | Reviewed; the semver *increment* has no instrument, which the charter already records |
| R46 | `SilentFailureRegisterTests`, already in the suite, both directions |
| R47, R48 | By review of the five new files |
| R49 | A validation row in `plan.md` carrying both medians, taken on the same 500-directory fixture |

### Mutation testing

Every one of the five rows is mutation-tested: the fault named in the instruments table is
injected, the named check must fail, and the real output is seen. A check that has never been
seen to fail is a check nobody should trust.

### The performance fixture

500 directories **at the top level**, holding seven plans. Breadth at the top two levels is what
root discovery globs, so depth and total count do not reproduce the figure. The before-figure is
re-measured on that same fixture rather than taken from the earlier flat-tree and 900-directory
numbers, because comparing a 500-after against a 900-before would show an improvement the change
did not make.

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
