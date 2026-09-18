---
slug: init-scaffolds-solution
title: init produces a solution you can build, not a folder of config
status: approved
tier: feature
created: 2026-09-18
owner: peter
---

# intent — init produces a solution you can build

## Problem

`/sdlc-core:init` on an empty folder produces `.sdlc/CHARTER.md` and seven configuration
files: `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`,
`coverage.runsettings`, `terraform/versions.tf`, and Terraform lines appended to `.gitignore`.

**There is nothing for any of it to configure.** No solution file, no `src/`, no `tests/`, no
project of any kind, no README, and no base .NET `.gitignore` — only the Terraform lines.
`Directory.Build.props` sits beside nothing, still carrying an unsubstituted `{{SOLUTION}}`
token if nobody noticed. `Directory.Packages.props` pins versions for packages no project
references. `coverage.runsettings` excludes test projects that do not exist.

This holds for **every kind of solution**. A web application, a library, and a competition
repository all produce the same config-only result, because the pack describes files and never
describes structure.

The consequence is not that setup is tedious. It is that **the first real structural decisions
of every project happen outside the framework**: the owner hand-creates the solution, chooses
the project layout, names the assemblies and decides the module boundaries before
`/sdlc-core:intent` has any code to reason about. The stage built to capture a problem runs
against an empty directory, and `spec-writer` — whose method is to read the codebase and cite
the files that taught it the pattern — has no codebase to read. Stage 0 hands Stage 1 nothing.

A second, quieter consequence: **nothing verifies that what `init` wrote actually works.** There
is no build to run, so an unsubstituted token, a bad SDK pin, or a package version that does not
resolve is discovered whenever someone first tries to compile, with no signal that `init` is
where it came from.

## Affected users and systems

- **Every repository created with this framework, from its first minute.** Today the owner;
  shortly a small team, for whom "what does a new repo look like" stops being tacit.
- **`/sdlc-core:init`** — gains a scaffolding step, a hard dependency on the .NET SDK it does
  not have today, and a change to its own step order (scaffolding must follow the approval
  gate, see Proposed outcome). Its closing line *"`init` is never run again for this repo"*
  contradicts re-runnable gap-filling and has to change.
- **The `stack-dotnet` pack** — must declare structure and commands, not only files. It is the
  only place that may, because I1 forbids the engine from naming a stack.
- **`spec-writer`** — its documented method reads the codebase before designing, and its
  greenfield branch says *"ASK, do not invent"*. A scaffolded repo makes that branch largely
  dead; it needs revisiting rather than leaving to rot.
- **The gate hook** — it does **not** skip `.props`, `.runsettings` or `.tf`, so those are
  gated today, and in a fresh repo no `plan.md` exists by construction. Scaffolding multiplies
  the warning from three files to a whole solution. Two further wrinkles: the matcher is
  `Edit|Write|NotebookEdit`, so generator output written through a shell bypasses the hook
  while pack assets written through Write do not — the scaffold would be *half* gated — and a
  starter notebook is caught by `NotebookEdit`.
- **`coverage.runsettings`** — its `[*.UnitTests]*` and `[*.AppHost]*` exclusions only match
  because `Directory.Build.props` rewrites assembly names. Emitted project names are therefore
  a contract with it, currently undeclared.
- **`README.md`** — the stage table says `init` produces `CHARTER.md` and stops; the list of
  what the pack scaffolds is already stale; and `/init-competition` is listed as planned but
  is replaced by this work.

## Proposed outcome

After `/sdlc-core:init` on an empty folder, the owner has a **single solution that builds, and
whose generated tests run and pass**, laid out to match what they said they were building.

**The scaffold is declared as data, not prose.** The pack ships an ordered manifest — commands
to run, assets to copy, tokens to substitute — and something executes it. This is the decision
that makes everything below checkable: a test can execute the manifest into a temporary
directory and assert the result, where no test can drive a skill body interpreted by a model.
It is also what makes the step re-runnable to fill gaps later.

**Scaffolding runs after the charter approval gate**, not before. Nothing is generated from an
artifact the owner has not approved, and a rejected charter leaves nothing to clean up.

The shape is decided by an interview:

| Kind | Emits |
|---|---|
| **Web solution** | solution, AppHost, ServiceDefaults, Blazor front end, ASP.NET Core API, unit tests, integration tests |
| **Competition / ML** | solution, a starter C# notebook, a feature and scoring library, a training runner, unit tests |

Structure comes from the SDK's own generator wherever it has one; the pack supplies only what
the generator cannot produce, and owns reconciling generator output with the repo's central
package management.

This **replaces the planned `/init-competition`**. One command branching on solution kind,
rather than two that diverge. The competition *chain* — experiment ledger, cross-validation
scheme, submission budget — remains `sdlc-kaggle`'s job and is untouched.

## Success criteria

The subject of S1–S9 is **the scaffold manifest executed into an empty temporary directory**,
on a machine meeting the stack's prerequisites. They are machine-checkable and belong in the
test suite.

1. **`dotnet build` exits 0**, evaluated against the package versions pinned in the pack — not
   against the live advisory feed. `NuGetAuditLevel=moderate` plus `TreatWarningsAsErrors`
   means an advisory published tomorrow turns the build red with no change to what was written,
   so the criterion is about the scaffold, not about a given Tuesday.
2. **The generated unit tests run and pass**, reporting at least one test executed. A run that
   collects zero tests also exits 0, so the count is part of the criterion. **Where Docker is
   available, the generated integration tests also run and pass**; where it is not, they must
   still compile.
3. **No `{{` survives** in any file present after the manifest runs — assets and generator
   output alike. This is the regression guard for assemblies named `{{SOLUTION}}.Foo`.
4. **Restore succeeds under central package management.** No generated project carries a
   `Version` attribute on a `PackageReference`, and every package any generated project
   references has a `PackageVersion` entry. Verified failure today: `dotnet new mstest` emits
   `<PackageReference Include="MSTest" Version="4.0.2" />`, which with the pack's
   `Directory.Packages.props` fails restore with **NU1008**.
5. **Emitted test and host project names match the assembly patterns `coverage.runsettings`
   excludes.** A test project named `Tests` rather than `UnitTests` silently produces a
   coverage number that counts test code.
6. **Re-executing the manifest over its own output changes nothing and stays green** — no file
   overwritten, and no command run that fails because its output already exists.
7. **A partial repo is filled, not disturbed**: solution present and `tests/` absent yields the
   missing pieces and leaves every existing file byte-identical.
8. **Both supported kinds satisfy 1–7 independently**, each from empty.
9. **I1 still holds**: no structure, project name, or command appears in
   `plugins/sdlc-core/`, verified by the existing test.

Two further criteria are about `init`'s own flow rather than the manifest, and are **not
machine-checkable** — `init` is prose a model interprets, and the instrument that would measure
it is the charter-blocked eval suite. They are recorded here as human-verified, and belong in
I9's known-and-unchecked list rather than being quietly assumed:

10. **Nothing is generated before the charter is approved.** No project file exists on disk
    while the charter is still `draft`.
11. **The interview is required, not decorative.** With no answer to the module-boundary
    question, `init` stops and says so rather than selecting a manifest.

## Constraints

- **The .NET SDK becomes a hard dependency of `init`** in a repo using this pack. Today `init`
  writes text files and needs nothing installed. `init` must fail clearly rather than
  half-scaffolding when the SDK is absent — and note that I1 forbids the engine from naming
  the SDK, so that message has to come from the pack.
- **Docker is a prerequisite for the integration half**, not for the scaffold itself. Named
  here so "green" is never claimed on a machine that cannot run it.
- **CI cannot prove the whole criterion on one runner.** The hosted `windows-latest` image runs
  Windows containers only, while the PowerShell gate tests need Windows — so the integration
  half needs a second runner or stays local. *(Worth confirming against current runner images
  before it is load-bearing.)*
- **Central package management collides with generator output.** Verified, see criterion 4.
  Something must own stripping `Version` attributes and reconciling generated references
  against the pack's pins, including at the next SDK bump when template versions move.
- **Two charter amendments must land before the spec is written**, or the spec is built against
  invariants that are still moving: **I5** widening from "the files it writes" to the actions a
  pack declares, and the **emitted layout joining the Frozen list**, since `spec-writer`, slice
  manifests, coverage patterns and the gate hook's path matching all come to cite it.
- **Windows only**, per the charter's platform commitment. Inherited, not changed.
- **Never overwrite an existing file.** Already the rule for config; it extends to projects,
  and it is what makes gap-filling safe.

## Non-goals

- **Never hand-maintained project templates.** The SDK's generator produces anything it can;
  the pack ships assets only for what it cannot. Our own copies would drift at every SDK
  release and someone would own fixing them.
- **Never mutates the developer's machine.** No `dotnet new install` of a template pack, no
  workload install, no global tool install, no image pull. If a template the manifest needs is
  not present, it says so and stops. A global install is the least reversible thing this could
  do, and it would be done to a machine, not a repo.
- **No demo content survives.** Generator templates ship sample pages and counters. A repo
  created by this framework does not start life containing a weather forecast.
- **Never writes CI pipeline files.** A pipeline depends on where the repo is hosted and how it
  deploys; `init` knows neither.
- **Never runs Terraform or touches a cloud account.** It writes `versions.tf` and stops.
- **Never creates the git repository or the first commit.** `git init` stays the owner's act.
- **Never writes or edits `CLAUDE.md`.** `init` checks it exists and nothing more. Build and
  test commands are exactly what would leak there, and they belong to the pack.
- **Never restructures or renames anything in an existing repo.** Gap-filling adds what is
  missing; it does not move what is there.
- **One solution per repository.** The scaffold never emits a second `.sln`.
- **Not the competition *chain*.** Scaffolding a competition repo is in scope; an experiment
  ledger, a CV scheme, leakage hunting and submission-budget accounting are `sdlc-kaggle`.
- **Library-or-tool and Azure Functions kinds are out of scope for this version** — excluded to
  keep the first version to two shapes verified properly rather than four half-checked.

## Open questions

- **How is the work decomposed?** The owner has said it need not all live in `init`. Declaring
  the scaffold as data already implies a separable executor; whether that is exposed as its own
  command is still open. *Owner: peter — settle in the spec.*
- **Are the Aspire and notebook templates actually in the box?** `aspire-apphost`,
  `aspire-servicedefaults` and `aspire-mstest` resolve on this machine, but that may be because
  something installed them. If they need a template-pack install, that collides head-on with
  the "never mutates the machine" non-goal and changes what the web kind can emit.
  *Owner: Design — verify on a clean machine.*
- **Where does the solution name come from?** The directory name is the obvious default and is
  what substitutes `{{SOLUTION}}`, but directory names are not always valid identifiers.
  *Owner: Design.*
- **What is in the README it writes?** A stub, or something that earns its place — how to run
  locally, how to run tests, where the charter is. *Owner: Design.*
- **What is in the starter notebook, and who owns it?** It is the one asset with no template
  behind it, and the one place N1 could leak: a competition-specific scoring or CV snippet is a
  domain, not a defect class. Note too that notebooks sit outside the analyzer gate entirely,
  so criteria 1 and 2 say nothing about whether the shipped notebook actually runs — which
  reproduces this intent's own complaint in miniature. *Owner: peter.*
- **Does the gate hook's skip list change, or does `init` become unrunnable under
  `SDLC_GATE=block`?** Related: the half-gating above, where generator output bypasses the hook
  and pack assets do not. *Owner: Design.*
- **When do the deferred kinds land?** Library-or-tool is the smallest, and the one this very
  repository would have wanted. *Owner: peter.*
