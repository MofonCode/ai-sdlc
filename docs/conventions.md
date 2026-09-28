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
`constraints.md` owns the wording, and a second copy is a source that will disagree — CON_09 forbids
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

Enforced by: `PluginTests.AnInjectorEmitsItsContentUncorrupted`, which asserts both that no mojibake
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

## Schemas and artifacts

### A schema owns its vocabulary, and nothing else restates it

The `severity` enum in `schemas/findings.schema.json` is the single definition of what severities exist.
Every consumer branches on the same closed set.

Enforced by: `SchemaTests.SeverityEnumIsTheSingleDefinition`,
`SchemaTests.OutcomeEnumMatchesTheStateMachine`

### A schema is tested in both directions, and the rejections are the point

A schema proven only by documents that should pass has not been proven. Nine fixtures cover the findings
and verify schemas, four valid and five invalid.

Enforced by: `SchemaTests.FixtureMatchesItsSchema`, with `SchemaTests.EverySchemaFixtureIsCovered`
asserting no fixture is left unevaluated — one nobody runs is worse than none, because it looks like
coverage

### Frontmatter is read with a real parser

`YamlDotNet`, never a regex. In v02 a hand-rolled frontmatter regex reported that a skill declared
`disable-model-invocation` while the real parser had dropped every field in the file. The skill was
model-invocable at run time and 333 tests said otherwise. A check on markup is worth only what the
parser behind it is.

Enforced by: `SkillTests.EverySkillFrontmatterParsesAndNamesItself`,
`FixtureTests.ArtifactFrontmatterParsesWithARealYamlParser`

### Live artifacts are validated, not just fixtures

Schema checking that runs only against fixtures is how a verify document with twelve errors shipped
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
| `constraints.md` preamble | the authority — it is the file the rule governs |
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

### Every constraint an interrogation can reach has a fixture that trips it

A rule nobody has seen fire is a rule nobody has tested.

Enforced by: `FixtureTests.EveryConstraintAboutAnArtifactHasAFailingFixture`

---

## Fixtures

### A golden artifact is true of the solution it sits in

An interrogation reads the code. The first fixture set described an ops dashboard the fixture solution
did not have; the replacement claimed two guarded call sites that did not exist, and CON_02 fired
correctly on the artifact meant to be sound.

No test can check this — it is semantic. **A new fixture is not finished until it has been interrogated
once and the result read.**

### A fixture isolates one constraint

A fixture that trips two makes a failure ambiguous about which rule broke.

Enforced by: `FixtureTests.ExpectationsCiteConstraintsThatExist`,
`FixtureTests.EveryArtifactHasAnExpectationAndViceVersa`

### An expectation asserts structure, never wording

Which constraints are cited at which severity. Wording is model output, and asserting on it makes every
case brittle.

### A fixture asserting a judgement will flake, and should say so

CON_06 asks whether a proof is *adequate* — the constraint that most needs reading comprehension, and so
the one with real variance. `plan.good.md` drew a finding on one run and none on the next with nothing
changed. A disagreeing case should be re-run and reported as flaky, never retried silently: hiding
variance is the same mistake as an advisory gate.

### Skills run against a copy, never the checked-in fixture

`FixtureWorkspace.Materialize` gives a disposable git repository. A test that dirties its own input
passes once and is meaningless afterwards.

Enforced by: `FixtureTests.FixtureMaterializesAsAGitRepositoryAndIsDiscarded`, which asserts the
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

### The docs index describes a trigger, not a subject

"Open it when the orchestrator failed" beats "about local development". A pointer is opened on judgement,
so the description has to be the thing that triggers it.

### `@path` means always; a plain path means when relevant

An `@path` import expands into context at launch. In the docs index that would defeat the point.

---

## Landing a change here

A change lands when `dotnet test` is green, `claude plugin validate .` passes, it is consistent with
`docs/concept.md`, and a person has reviewed it. Where it disagrees with the concept, either the concept
wins or the change carries the amendment that makes them agree — argued in the same pull request.

Two things that are not optional:

- **Warnings are errors**, with `WarningsNotAsErrors` deliberately empty, so an exception is one visible
  line in `Directory.Build.props` rather than a pragma buried in code
- **A defect found by running the framework gets a test that replays it.** Most of the rules above exist
  because something went wrong once. The test is what stops it going wrong twice

### After changing the plugin, reinstall

A directory-marketplace install caches by version, and `claude plugin update` does nothing when the
version has not moved.

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

Short descriptions, so nothing here sends you to look one up. `constraints.md` carries the full
statement, its `Forbids:` clause and the reasoning; `/sdlc:constraints` renders the effective set for
a solution, this list plus any `LOC_` of its own.

| | |
|---|---|
| `CON_02` | an intent's unknowns stay open questions |
| `CON_03` | a criterion can fail |
| `CON_06` | a plan's proof is adequate to its criterion |
| `CON_07` | a change does only what its plan declared |
| `CON_09` | no artifact restates another's content as its own authority |
