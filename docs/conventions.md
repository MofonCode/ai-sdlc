<!--
covers:
  - "skills/**/SKILL.md"
  - "tools/*.ps1"
  - "tests/Sdlc.Tests/*.cs"
-->

# Conventions

How to change **this** repository. `docs/concept.md` says what the framework is and why; this says how
to work on it, and why each rule exists.

Every rule below that a machine can check names the test that checks it. That is deliberate: a
convention with no test is advice, and a convention with a named test is a rule you can verify in one
command. If you add a convention here, add the test, or say plainly that it is unenforced.

The framework governs the solutions that adopt it with **constraints**. It governs itself with **tests
and this document**, because it deliberately runs no cycles on itself — `docs/concept.md` §1 has the
reasoning.

```pwsh
dotnet test
```

---

## Writing a skill

### A skill that writes a schema-governed artifact shows the shape

Put a literal, complete example of the artifact in the skill body, with the key names and nesting the
schema requires.

This is the single strongest predictor of whether a skill's output validates. In the first trial run,
`interrogate` carried a worked JSON example and produced eight valid documents out of eight; `verify`
and `init` carried none and both produced invalid ones — `verify`'s had twelve schema errors. A skill
cannot read its schema at run time, because a session working in a governed solution is sandboxed to
that solution and cannot reach the plugin root. So the shape has to be in the body or it is nowhere.

Enforced by: `SkillTests.ASkillNamesEveryFrontmatterFieldItsArtifactRequires`

### An intent carries no technology

The four questions are a problem, who has it, what they would be able to see, and what must not change.
None needs a framework to express. The stack is declared once in `.sdlc/solution.md`; a departure is a
plan decision, argued in the plan with every other technology choice.

The skill used to ask "is anything here not our default stack?", which put a technology answer into the
one artifact whose job is to be free of one. A technology a person volunteers is still recorded under
**Fixed decisions**, attributed, so no later phase re-litigates it — but it is never asked for.

### Reference data first, the procedure last

When a skill injects reference material — the constraints, the clock — put it **before** the steps, not
after.

With the constraints injected at the end, `interrogate` reached the correct verdict and then printed it
instead of writing the file. The last thing a skill reads is what it acts on, so reference data placed
last displaces the instruction.

Unenforced. Position is not something a test can judge.

### A skill never invokes another skill

Name the next command and stop. Do not call it.

Every skill once ended with "then run `/sdlc:X`", and every one of those calls errored. A chain that
only sometimes fires is worse than none: it produces artifacts nobody challenged while appearing to
have challenged them. Chaining belongs to whoever is driving — a person, or `run`, which drives one
headless session per step.

Unenforced. Worth a test if a way to detect it appears.

### A skill whose effects reach outside the repository is human-invoked only

`disable-model-invocation: true` on anything that opens a pull request, commits a team to a process, or
runs unattended work.

Enforced by: `SkillTests.OutwardFacingSkillsAreHumanInvokedOnly`

### A description says when to use the skill, not just what it does

The description is the only part always in context, and it is how relevance gets judged. One that says
only what a skill does leaves the trigger to guesswork.

Enforced by: `SkillTests.EverySkillDescribesWhenToUseIt`, which also holds a length floor

### A skill cites a constraint, never copies it

Refer to `CON_07` and explain why it matters in your own words. Do not reproduce its `Forbids:` clause.
`CONSTRAINTS.md` owns the wording, and a second copy is a source that will disagree — CON_09 forbids
exactly this.

Enforced by: `SkillTests.NoSkillRestatesAConstraint`

### A report that repeats has one fixed shape

Counts first, then findings, then one command alone in a fence. `interrogate` and `verify` both use it,
because verify is an interrogation of a diff.

The reason is what a reader does with a stack of these: they read the first line. `blocking: 0 ·
suggestions: 2` answers the only urgent question, and a shape that moves around forces them to read the
whole thing to find it. A command wrapped in prose has to be trimmed before it can be pasted, which is
friction on the most repeated action in the process.

Both skills carry the shape as a literal example rather than a description, for the same reason every
artifact-writing skill does — a worked example is what makes output match, and prose about a format does
not.

Unenforced. A test cannot judge whether output matched a template; the calibration is the trial run.

### Anything open-ended in a report needs a cap and a stopping rule

`interrogate` was licensed to raise "observations that no constraint forbids" with no limit. A trial run
turned four rounds on a spec that had been sound since round two, because each clean verdict came with a
fresh paragraph that read like work.

Constraints converge — there is a fixed number and an artifact either breaches one or does not. Taste
does not converge. So anything a skill may raise at its own discretion is capped, and a clean verdict
says **ready to advance** in those words and names what happens next. A reader who has to infer whether
they may proceed will assume they may not.

Unenforced.

### Two instructions in tension resolve toward the one with a concrete output

A restraint — "resist the urge to add advice" — loses to a licence — "mention it as an observation" —
every time. If you write a limit, make it the thing with the concrete output: a cap, a count, a named
format.

---

## Writing a hook

### A hook fails closed only where the framework is in use

These scripts run on every edit in every directory. Refusing in an ordinary folder gets them switched
off, after which they protect nothing at all. Refuse when `.sdlc/` exists and the precondition is
genuinely broken; stay silent otherwise.

Enforced by: `HookTests.AGovernedDirectoryWithoutGitIsRefused` and
`HookTests.AnUngovernedDirectoryWithoutGitIsLeftAlone` — the pair matters more than either half

### Every hook test asserts both directions

For each refusal, test the case it must catch **and** a neighbouring case it must not. An over-refusing
hook is as broken as a permissive one, and only the second test finds it.

The first version of `Assert-DerivedArtifact.ps1` denied every write under `findings/` and blocked
`interrogate` from writing its own output. A hook cannot tell a producer from a person by looking at the
call, so it keys on what does distinguish them: Edit versus Write for a derived document, creation
versus overwrite for findings and evidence.

Enforced by: `HookTests.WritingANewFindingsRoundIsAllowed` beside
`HookTests.OverwritingAnExistingFindingsRoundIsRefused`, and
`HookTests.RederivingAVerifyDocumentIsAllowed` beside `HookTests.TargetedEditOfAVerifyDocumentIsRefused`

### A gate refuses narrowly, and misses rather than over-refuses

The fast layer inspects an action, so it is guessing. When it guesses wrong there are two directions and
they are not symmetric: a miss is caught later by `Compare-DiffToPlan.ps1`, which reads git, while a
false refusal blocks legitimate work and teaches the caller that the gate is noise. A trial run made that
concrete — the declared-file gate refused twelve times during one build, every one of them wrong, and
the caller started routing around it.

Two causes, both fixed, both the same mistake:

- **It read `>` inside text the shell never interprets.** `<noreply@example.com>` in a commit message
  yielded a file called `@`; `<LangVersion>latest</LangVersion>` in a heredoc yielded one called `latest`.
  Literal spans are blanked before the scan now, which can lose a quoted target — the safe direction.
- **It refused paths outside the repository.** A plan declares what a change does to its own repository.
  An absolute path elsewhere is not its business, and refusing one blocked `gh pr create --body-file`
  writing to a scratchpad.

Enforced by: `HookTests.AnAngleBracketInsideALiteralIsNotARedirect`,
`HookTests.AWriteOutsideTheRepositoryIsNotThePlansBusiness`, and
`HookTests.AnUndeclaredWriteInsideTheRepositoryIsStillRefused`, which is the counterweight — narrowing a
gate must not switch it off

### A shipped script is parsed, not tokenized

`[Parser]::ParseFile`, not `[PSParser]::Tokenize`. Tokenizing a file with a function declaring its
parameters twice — an inline list and a `param` block — reports it clean, and the file then fails at load
so every gate keyed on it protects nothing while appearing to.

Enforced by: `PluginTests.EveryToolScriptParses`

### Approval is printed for a person, never written by a skill

A clean interrogation ends by printing the block with **the timestamp filled and the name empty** — the
timestamp from the injected UTC clock, because it is the part a person cannot produce; the name left for
them to type, because typing it is the act of approving. A name already sitting there turns approval into
pressing return on someone else's assertion. A skill never writes either field into the artifact.

An unfilled paste is refused by every gate. `Get-SdlcApprovalField` in `tools/SdlcPlan.ps1` is the single
definition of "approved", and it strips quotes before testing for emptiness: the older per-gate check
matched `\S.*`, which refuses a bare blank but accepted `approved_by: "  "`, since the quotes are
non-whitespace and the schema's `minLength` counts them.

There is no auto-approval on a clean verdict, and the reason is not caution. The verdict is the machine's
own judgement of the artifact, so letting it approve on that basis is the machine approving its own
output; and the field records *who decided*, so a skill filling it forges the only signature in the cycle
and every artifact downstream rests on an approval nobody gave.

### A refusal says what to do instead

A hook that only says no teaches people to route around it. Name the remedy and, where the framework
itself is at fault, say that too.

### A hook signals refusal in its output, never by failing

Exit zero and print a `permissionDecision`. A crashed hook enforces nothing.

Windows PowerShell 5.1 makes this easy to get wrong: with `$ErrorActionPreference = 'Stop'`, a native
command's stderr becomes a terminating error. Outside a git repository both gates crashed rather than
deciding, and it stayed latent until a test ran them outside one.

Enforced by: the exit-code assertion inside `HookTests.Invoke`, which every hook test runs through

### A tool script is reachable from the manifest or a skill

A script nothing invokes is either dead or a guardrail somebody forgot to wire up, and the second is
indistinguishable from protection that works.

Enforced by: `PluginTests.EveryToolScriptIsReachedFromSomewhere`,
`PluginTests.EveryHookScriptTheManifestNamesExists`, `PluginTests.EveryInjectedScriptASkillNamesExists`

### A script that reads a file passes `-Encoding UTF8`

5.1's `Get-Content` assumes the system codepage for a file with no BOM, so an em dash round-trips as
mojibake. Both injectors shipped with that defect and nothing noticed, because the text stayed readable —
every interrogation was reading mangled constraint headings. Write UTF-8 explicitly too.

Every `Get-Content` in `tools/`, not just the injectors'. That gap hid three unencoded reads: the
settled-artifact gate, the artifact-shape gate, and the check deciding whether a cycle may land.

Enforced by: `PluginTests.EveryScriptReadsFilesWithAnExplicitEncoding` and
`PluginTests.AnInjectorEmitsItsContentUncorrupted`, which asserts both that no mojibake
sequence appears and that an em dash does, so the check cannot pass by the content losing its dashes

### A stack default reaches a skill by injection, never by a file read

`docs/techstack.md` is at the plugin root, which a solution session cannot read. A plan choosing project
shapes had no access to the defaults at all until `tools/Get-StackDefaults.ps1` existed — nobody noticed,
because no plan had yet needed them. The same is true of anything else a skill needs from this repository.

Enforced by: `PluginTests.TheStackDefaultsBlockIsMarkedAndSubstantial`, which also asserts the injector
does not leak the part of that file about building this repository

### Shipped PowerShell targets Windows PowerShell 5.1

`powershell.exe`, not `pwsh`. A hook that cannot start because an edition is not installed protects
nothing while appearing to, and 5.1 is on every Windows machine.

The cost is real and the scripts are written for it: no `&&` or `||`, no ternary, no `??`,
`ConvertFrom-Json` returns a `PSCustomObject`, and `Test-Json` does not exist — which is why schema
validation lives in the test project rather than in a script. `docs/techstack.md` has the detail.

---

### A measurement reads its own base, it never recomputes one

A check that compares a result to a declaration needs a fixed point to measure from, and it must read
that point rather than derive it at run time. `Compare-DiffToPlan.ps1` took its base as
`git rev-parse HEAD` when it ran — correct only while nothing had been committed since the artifacts
commit. One `git commit` during a build put the base on top of the change, so the diff came back empty,
the undeclared file in it disappeared, and the exit code said success. Build now pins the base into
`evidence/base.sha` and the comparison refuses when none is recorded.

The general rule: **when a wrong answer looks like a pass, refuse instead of guessing.** A check whose
failure mode is silent success is worse than no check, because it is also a claim.

Enforced by: `CycleScriptTests.TheComparisonIsUnchangedByHowOftenTheBuildCommits`,
`CycleScriptTests.TheComparisonRefusesWhenNoBaseWasPinned`, `CycleScriptTests.APinnedBaseIsNotMoved`

### A phase that measures asserts its preconditions first

Build is the first phase to write source and everything downstream is measured from where it starts, so
it checks the branch, the plan's approval and a clean tree before touching anything — and refuses rather
than repairing, because each failure is a person's decision. A precondition left to prose is a
precondition that holds most of the time.

Enforced by: `CycleScriptTests.BuildIsNotReadyOffTheCycleBranch`,
`CycleScriptTests.BuildIsNotReadyWithUncommittedSource`,
`CycleScriptTests.BuildIsNotReadyWithoutAnApprovedPlan`,
`CycleScriptTests.BuildIsReadyWhenOnlyTheCycleArtifactsAreDirty`

### One act, one word: a person **approves**

An intent, a spec and a plan all wait on the same act, and it is spelled `approved_by` / `approved_at` on
every one of them. It used to be `accepted_*` on the first two and `approved_*` on the plan, which meant
every reader, skill and regex had to remember which artifact used which word for one decision — and the
hook refusing findings against a settled artifact carried two code paths to match it.

"Accepted" was the wrong word to standardise on: a spec's criteria are **acceptance criteria**, so a spec
that was also "accepted" made one word mean both the criteria and the sign-off. Criteria are accepted;
people approve. A waiver is neither — it tolerates a finding rather than passing a gate — and keeps its
own wording.

Enforced by: `SchemaTests.OneWordNamesTheHumanGateOnEveryArtifact`

## Schemas and artifacts

### The complete schema check can be pointed at a live file

`dotnet run --project tools/Sdlc.Validate -- <path>` runs the same evaluator the suite uses against a
real artifact, or every artifact under a directory. It exists because the two layers of checking left a
hole: the hook compares top-level key names on every write, and the evaluator that checks values only
ever saw samples. So a verify document could claim `pass` beside an open blocking finding and nothing in
the framework would object — every key in it is a legal key.

Two things it taught immediately. Its own first run rejected a valid document because deserializing YAML
to `object` makes every scalar a string, so `passes_spent: 0` arrived as `"0"`; the conversion now follows
YAML 1.2, where quoting decides. And a schema built from conditionals reports every branch that declined
to apply, so the one real error sat under four lines belonging to a rule that did not apply — a detail
beneath a node that passed is not a failure, and is dropped.

Enforced by: `ValidatorTests.AVerifyDocumentClaimingPassWithAnOpenBlockingFindingIsRejected`,
`ValidatorTests.AnIntegerInFrontmatterStaysAnInteger`,
`ValidatorTests.TheCheckedInArtifactSamplesAreValid`, and
`ValidatorTests.AMissingArtifactIsAUsageErrorRatherThanAPass`

### A schema owns its vocabulary, and nothing else restates it

The `severity` enum in `schemas/findings.schema.json` is the single definition of what severities exist.
Every consumer branches on the same closed set.

Enforced by: `SchemaTests.SeverityEnumIsTheSingleDefinition`,
`SchemaTests.OutcomeEnumMatchesTheStateMachine`

### A stack default this repository states, this repository runs

`docs/techstack.md` named MSTest on Microsoft.Testing.Platform as the testing default while this
repository ran MSTest under VSTest — it never set `EnableMSTestRunner`, which is the only reason its own
`dotnet test` worked. So the default went out to every scaffolded solution untested by the framework
publishing it, and the first solution to follow it could not run the command its own `AGENTS.md`
documents.

Both settings are now in this repository and named in the defaults: the `test` section in `global.json`
and `EnableMSTestRunner` on the test project. The suite runs under MTP, so the configuration we prescribe
is the configuration our own 112 tests exercise.

The general rule: **a default is a claim, and a claim this repository does not run is a claim nothing
checks.** Where a default can be exercised here, exercise it here.

### A field means one thing, and a required field must mean it everywhere

`cites` was required on every finding. A suggestion that breaches no constraint had to name one anyway,
so the trial produced two suggestions that cited CON_07 and then spent their own statements explaining
that CON_07's clause was not breached — telling a reader a rule was broken and then telling them it was
not. It is required on blocking findings only now, beside `remedy`, which already worked that way.

If a suggestion really does breach a constraint, it is not a suggestion.

Enforced by: `SchemaTests.DocumentMatchesItsSchema`, through
`findings.valid-suggestion-without-cites.json` and `findings.invalid-blocking-no-cites.json`

### A schema is tested in both directions, and the rejections are the point

A schema proven only by documents that should pass has not been proven, and the asymmetry is the reason:
losing a rule only makes a schema *more* permissive, so a suite of valid-only documents stays green against
a schema whose rule was deleted, typoed, or left pointing at a `$defs` name that no longer exists. The
invalid document is the only one whose verdict depends on the rule being there.

All seven schemas are covered in both directions. `findings` and `verify` were first; `intent`, `spec`,
`plan`, `solution` and `expectation` had valid coverage only until rejecting documents were added for the
both-or-neither approval pair, the `tier` and `origin` and `kind` enums, `additionalProperties: false`,
the adopted-needs-a-sha conditional, and an expectation asserting nothing.

### A rejecting document must fail for exactly one reason

`DocumentMatchesItsSchema` asserts only valid-or-invalid, so a document with two defects passes while
proving nothing about either. **To check, flip its expected verdict to `true` and read the failure
message** — the assertion prints the schema's own errors.

That found a real one. `solution.invalid-baseline.json` was written to prove `additionalProperties: false`
refuses a `baseline` key, and it also declared `origin: adopted` with no `adopted_at`. It would have been
rejected with the additionalProperties rule removed, so it proved nothing; the missing sha became its own
document instead. Ignore the noise from a declined conditional in that output — an `if` that did not match
reports its mismatch, and that is the conditional standing aside, not a second defect.

Enforced by: `SchemaTests.DocumentMatchesItsSchema`, with `SchemaTests.EverySchemaDocumentIsCovered`
asserting no document is left unevaluated — one nobody runs is worse than none, because it looks like
coverage

### Frontmatter is read with a real parser

`YamlDotNet`, never a regex. In v02 a hand-rolled frontmatter regex reported that a skill declared
`disable-model-invocation` while the real parser had dropped every field in the file. The skill was
model-invocable at run time and 333 tests said otherwise. A check on markup is worth only what the
parser behind it is.

Enforced by: `SkillTests.EverySkillFrontmatterParsesAndNamesItself`,
`SampleTests.ArtifactFrontmatterParsesWithARealYamlParser`

### Live artifacts are validated, not just samples

Schema checking that runs only against samples is how a verify document with twelve errors shipped
looking authoritative. `tools/Assert-ArtifactShape.ps1` refuses a write whose top-level keys contradict
the schema, reading the schema at run time so there is no second definition.

It checks names, not values — the conditional rules need a real evaluator, which 5.1 cannot be. Shallow
and immediate, plus complete and slower, and between them the realistic defect classes are covered.

Enforced by: `HookTests.TheVerifyDocumentTheTrialProducedIsRefused` and
`HookTests.TheSolutionProfileTheTrialProducedIsRefused`, which replay the trial's actual malformed
documents

---

## Constraints

### The rules for an identifier live in four places, doing four jobs

| Where | Job |
|---|---|
| `CONSTRAINTS.md` preamble | the authority — it is the file the rule governs |
| `docs/concept.md` §4.1 | the decision, with its reasoning |
| `skills/constraints/SKILL.md` | how to add a local one |
| `ConstraintsTests` | enforcement |

Authority, rationale, instruction, enforcement. Not four copies — but three of them are prose, they can
drift, and nothing checks that they agree. Read all four before changing any.

Enforced by: `ConstraintsTests.IdsAreUniqueAndAscending`,
`ConstraintsTests.SupersessionsNameAnExistingConstraint`

### A constraint survives three tests or it is not one

Its `Forbids:` names something that becomes illegal; no hook, schema or analyzer could decide it; and it
is incompatible with at least one plausible implementation. Rules about code go to analyzers. Mechanical
rules about artifacts go to hooks or schemas.

One candidate was cut by this filter during phase 1 — "a suggestion names a change" forbids nothing
downstream, because nothing consumes suggestions mechanically.

Enforced by: `ConstraintsTests.EveryConstraintForbidsSomething`,
`ConstraintsTests.EveryConstraintSaysWhatItAppliesTo`, `ConstraintsTests.ConstraintsParse` — which fails
below six constraints, because fewer than that means the adversarial mode is not carrying its weight

### A global constraint names no stack

It binds solutions on any stack. Naming one puts every solution elsewhere in violation of a rule that
never applied to it.

Enforced by: `ConstraintsTests.GlobalConstraintsAreStackNeutral`

### Every constraint an interrogation can reach has a sample that trips it

A rule nobody has seen fire is a rule nobody has tested.

Enforced by: `SampleTests.EveryConstraintAboutAnArtifactHasASampleThatTripsIt`

---

## Samples

### A sample solution compiles, from a copy

Nothing compiled these for a long time, and both were broken. greenfield did not build at all: its csproj
declared `Nullable` but not `ImplicitUsings`, and its sources used `IEnumerable<decimal>` with no `using`,
so it failed on three errors the moment anything tried.

**Nothing had tried.** An earlier note here blamed the repository's `Directory.Build.props` for masking it
in-tree, and that was wrong: `tests/solutions/Directory.Build.props` is deliberately empty precisely to
stop that inheritance, so the sample never had implicit usings anywhere. The defect was not hidden by
inheritance, it was invisible for want of a build — which is the duller and more common way a thing goes
unnoticed. A skill would have hit it inside a build phase and reported it as the cycle's failure.

**A sample must be self-sufficient**, because a skill only ever sees the copy. Declare the framework, the
nullable setting and the implicit usings in the csproj; pin a package version inline rather than through
central management, since `Directory.Packages.props` is not there either — and in-tree it would reject an
inline version with NU1008, which is the second reason the build happens from a copy.

**And a sample carries a `.gitignore` for `bin/` and `obj/`.** Without one, building leaves output that
`git status` reports, so the diff a cycle takes against the sample includes the compiler's work.

greenfield's build also proves its public surface: `PublicApiAnalyzers` fails if a public member is
missing from `PublicAPI.Shipped.txt` or listed there without existing. That is what makes
`plan.good.md`'s proof honest when it cites the file, and it is why `Invoicing.cs` is load-bearing
despite no test naming it.

Enforced by: `SampleTests.ASampleSolutionBuildsCleanFromACopy`

### A golden artifact is true of the solution it sits in

An interrogation reads the code. The first sample set described an ops dashboard the sample solution
did not have; the replacement claimed two guarded call sites that did not exist, and CON_02 fired
correctly on the artifact meant to be sound.

No test can check this — it is semantic. **A new sample is not finished until it has been interrogated
once and the result read.**

### A sample isolates one constraint

A sample that trips two makes a failure ambiguous about which rule broke.

Enforced by: `SampleTests.ExpectationsCiteConstraintsThatExist`,
`SampleTests.EveryArtifactHasAnExpectationAndViceVersa`

### An expectation asserts structure, never wording

Which constraints are cited at which severity. Wording is model output, and asserting on it makes every
case brittle.

### Skill behaviour is asserted by running the skill

Everything else about a skill is checked indirectly — its frontmatter parses, the scripts it names
exist, the schema governing its output rejects what it should. None of that establishes that an
interrogation reaches the right verdict, which is the only thing the framework is for.

`HeadlessSkillTests` stages a sample artifact into a materialized workspace, runs `claude -p` against it,
and compares the findings it wrote to the expectation checked in beside the artifact. Assertions are on
structure — which constraints at which severity — never on wording, because wording is model output.

**Opt-in, because it bills the model.** `SDLC_HEADLESS=1` or the cases skip, and CI runs them nightly and
on demand rather than on every push. A gate that charges for every commit gets switched off, which is the
same failure as an advisory one.

Staging is load-bearing rather than setup: a spec names an intent and a plan names a spec, and an
interrogation that cannot resolve the reference refuses instead of quietly skipping the relational
constraints. So a broken stage shows up as a refusal, not as a false pass.

Enforced by: `HeadlessSkillTests.InterrogatingAGoldenArtifactCitesTheConstraintsItsExpectationNames`

### A sample asserting a judgement will flake, and the rate is the useful number

CON_06 asks whether a proof is *adequate* — the constraint that most needs reading comprehension, and so
the one with real variance. A disagreeing case is re-run and reported as flaky, never retried silently:
hiding variance is the same mistake as an advisory gate.

**Measure it rather than labelling it.** Two cases have been measured against `interrogate`, and the
numbers said different things:

| Case | Verdict | What the rate meant |
|---|---|---|
| `spec.good.md`, CON_03 | 2 pass / 2 fail, then 4 / 4 after a fix to the sample | a boundary case, and the artifact was at fault |
| `plan.good.md`, CON_06 | 2 pass / 2 fail on one spec, 1 / 4 on another | irreducible, and not attributable to either spec |

A rate near half says the artifact sits on the constraint's edge, and the sample is usually what to fix
— `spec.good.md`'s criterion 3 named a check that could not observe the outcome it claimed, so the model
was choosing between two defensible readings at random. A rate that stays mixed after the artifact is
unambiguous is judgement variance, and no edit to the sample will settle it.

**Do not chase a judgement by editing an artifact until the model agrees.** That tunes a sample to one
model on one day and teaches nothing. When a reading is genuinely available either way, the expectation
is what should say so.

### Skills run against a copy, never the checked-in sample

`SolutionWorkspace.Materialize` gives a disposable git repository. A test that dirties its own input
passes once and is meaningless afterwards.

Enforced by: `SampleTests.AMaterializedWorkspaceIsARepositoryAndIsDiscarded`, which asserts the
checked-in copy was not touched

---

## Documentation

### Docs metadata is an HTML comment; skill metadata is YAML frontmatter

Two styles in one repository, deliberately.

A **skill** declares YAML frontmatter because the harness parses it — name, description,
`argument-hint`, `allowed-tools`, `disable-model-invocation`. That is not ours to change.

A **doc** declares an HTML comment:

```markdown
<!--
covers:
  - "skills/**/SKILL.md"
-->
```

VS Code's markdown preview displays frontmatter rather than hiding it, so every doc opened there began
with a rule, the raw metadata and another rule. A comment renders as nothing in every renderer. And the
convention bought nothing here: these tests are the only thing that reads docs metadata, so there was no
ecosystem to be compatible with.

Enforced by: `DocsTests.EveryDocDeclaresAFreshnessSignal`, through the `Metadata` helper that every
metadata-reading test shares — it rejects a doc opening with frontmatter, with a message naming this
convention

### Every doc is indexed once and carries a freshness signal

A doc that documents source declares a `covers:` glob and is checked for staleness against it. A doc
that records decisions declares `freshness: exempt` with a reason. Exactly one of the two.

Enforced by: `DocsTests.EveryDocIsIndexedExactlyOnce`, `DocsTests.EveryDocDeclaresAFreshnessSignal`,
`DocsTests.EveryCoversGlobPointsSomewhere` — a glob matching nothing can never be stale, which makes the
check decoration

### A document that cites a constraint footnotes it

A reader should never be sent elsewhere to find out what `CON_07` says. Each document ends with a table
of the constraints it cites and a short description of each.

Inline glosses were tried first and are worse: they bloat a line, they read badly where an id repeats,
and they corrupt an id used as a **value** rather than a reference — the first attempt rewrote
`cites: CON_07` inside a worked example, which would have taught a skill to write a malformed field.

Skills are deliberately exempt. `interrogate`, `spec`, `plan`, `build` and `verify` receive the full
constraint text injected at load time, so a footnote there would restate what the reader already has.

Enforced by: `DocsTests.EveryCitedConstraintExistsAndIsFootnoted`, which also catches a citation naming a
constraint that no longer exists. It found a real gap on its first run: a possessive — `CON_03's breach` —
that the generator's own regex had excluded

### A guide is a view, and every skill appears in one

`docs/guides/` is where a person looks. A skill named in none of them has shipped invisible, however good
its own description is.

The guides restate command sequences the skills own. That is a view, not a second authority — the same
standing as the suggestions view — and it is why `cycle-commands.md` says so at the top. What keeps a view
honest is a test that it has not fallen behind.

Enforced by: `DocsTests.EverySkillAppearsInAGuide`

### Instruction files stay short enough to be followed

`AGENTS.md` and `CLAUDE.md` under 200 lines. They load every session, and a longer file both costs more
and is followed less. Detail goes behind a described pointer in the docs index.

Enforced by: `DocsTests.InstructionFilesStayShortEnoughToBeFollowed`

### A document at the root is named in capitals; everything under `docs/` is lowercase

`README.md`, `AGENTS.md`, `CLAUDE.md`, `CONSTRAINTS.md`. The capitals are what tells a reader these four
are the documents that govern the repository, and a lowercase name among them reads as a stray note
instead — which is what `constraints.md` looked like before it was renamed. A new root document
either earns the capitals or belongs under `docs/`.

Windows will not catch a stale reference to one of these on its own: a case-insensitive filesystem
resolves `constraints.md` to `CONSTRAINTS.md` and every test still passes, so the reference rots
silently until someone checks out on a case-sensitive filesystem. Rename through `git mv`, and grep for
the old spelling across `.md`, `.cs`, `.ps1` and `.json`.

Enforced by: `DocsTests.RootDocumentsAreNamedInCapitals`

### The docs index describes a trigger, not a subject

"Open it when the orchestrator failed" beats "about local development". A pointer is opened on judgement,
so the description has to be the thing that triggers it.

### `@path` means always; a plain path means when relevant

An `@path` import expands into context at launch. In the docs index that would defeat the point.

---

### Coverage here means "is each shipped thing exercised", not a percentage

The C# in this repository **is** the test suite; what ships is markdown, JSON Schema and PowerShell.
A line-coverage number over that would measure the test helpers, so there is none, and
`coverlet.collector` was removed — it was a declared dependency that nothing ever collected, which
reads as if coverage were handled.

What replaces it is direct: every constraint has a sample that trips it, every schema document is
evaluated by exactly one expectation, every skill appears in a guide, and every script is exercised
by a test. **Reachability is not coverage** — the older test asserted only that something *invokes*
each script, and `Compare-DiffToPlan.ps1` satisfied it with no test at all, which is how it shipped
taking its base from `HEAD`.

Enforced by: `PluginTests.EveryToolScriptIsExercisedByATest`, alongside
`PluginTests.EveryToolScriptIsReachedFromSomewhere`

### Style is stated in `.editorconfig` and gated in CI

Adapted from MarketSense's, which is the house style across these repositories; the three changes are
commented in the file. Two rules make it real rather than advisory: `IDE0055` is an error, so
whitespace fails the build, and CI runs `dotnet format --verify-no-changes`.

Before this existed, `Directory.Build.props` set `EnforceCodeStyleInBuild` with no `.editorconfig`
behind it — enforcement against an unwritten standard. A trailing space survived a hundred green
runs and this same hard gate, because the formatting rule defaults to silent.

Line endings: `.gitattributes` is `* text=auto` with no `eol=` pins, so git stores LF and checks out
native; `.editorconfig` states `crlf` to match the Windows working tree. Git owns what is committed,
`.editorconfig` owns what is edited, and they no longer need per-extension exceptions to agree.

## Landing a change here

A change lands when `dotnet test` is green, `claude plugin validate .` passes, it is consistent with
`docs/concept.md`, and a person has reviewed it. Where it disagrees with the concept, either the concept
wins or the change carries the amendment that makes them agree — argued in the same pull request.

Two things that are not optional:

- **Warnings are errors**, with `WarningsNotAsErrors` deliberately empty, so an exception is one visible
  line in `Directory.Build.props` rather than a pragma buried in code
- **A defect found by running the framework gets a test that replays it.** Most of the rules above exist
  because something went wrong once. The test is what stops it going wrong twice

### Bump the version when a consumer could see the change

**The bump is what makes the change reachable.** An install caches by version, so an unbumped version
means `claude plugin update` hands a consumer stale code while reporting success. It is not bookkeeping.

**Bump when the change is consumer-visible:** anything under `skills/`, `hooks/`, `tools/`, `schemas/`,
`CONSTRAINTS.md`, or a document that reaches a session by injection — `docs/techstack.md` today. Not for
tests, `.editorconfig`, CI, or a doc only contributors read; those change nothing about what a governed
solution gets.

**Minor for behaviour changed or capability added. Patch for a refinement of behaviour already shipped.**
No major before 1.0. Judged from the consumer's side: "the declared-file gate stops refusing writes
outside the repository" is minor because a cycle behaves differently; "the approval block prints an empty
name instead of a filled one" is a patch because the step was already there.

**Two files move together**, and a test enforces it: `version` in `.claude-plugin/plugin.json`, and every
`Verified as of` line in `docs/guides/`.

Enforced by: `DocsTests.GuidesNameTheRealPluginVersion`

### `Verified as of` means verified, so moving it is a claim

That line says a walkthrough was run against that version. The test forces it to equal the shipped
version, which catches the defect it was written for — the guide once claimed `0.1.0` across a whole
release, quietly telling a reader they were checking something other than what they had installed.

The cost of that equality is that a bump *moves the claim whether or not anything was re-run*. So when a
bump changes a step the guide covers, either walk it again or say in the pull request which steps the new
version has not been re-verified against. A claim that drifts upward on its own is the failure mode this
whole document is written against.

### After changing the plugin, reinstall

A directory-marketplace install caches by version, and `claude plugin update` does nothing when the
version has not moved, so a refresh is the pair below rather than an update.

```pwsh
claude plugin uninstall sdlc@ai-sdlc
```

```pwsh
claude plugin install sdlc@ai-sdlc
```

---

---

---

---

---

---

---

---

## Constraints cited here

Short descriptions, so nothing here sends you to look one up. `CONSTRAINTS.md` carries the full
statement, its `Forbids:` clause and the reasoning; `/sdlc:constraints` renders the effective set for
a solution, this list plus any `LOC_` of its own.

| | |
|---|---|
| `CON_02` | an intent's unknowns stay open questions |
| `CON_03` | a criterion can fail |
| `CON_06` | a plan's proof is adequate to its criterion |
| `CON_07` | a change does only what its plan declared |
| `CON_09` | no artifact restates another's content as its own authority |
