---
name: stack-dotnet
description: The .NET/C# stack pack - net10.0, Blazor front ends, ASP.NET Core APIs, EF Core, Aspire orchestration with Docker-based local emulation, Azure PaaS (Web Apps, Function Apps, Azure SQL) for deployed environments with Terraform as the single source of truth, SQL Server with a persisted volume, MSTest, coverlet coverage, and the scaffold files. C# scripts and Polyglot notebooks (.ipynb on the .NET Interactive kernel) are endorsed for exploration only, because they sit outside the analyzer gate. Includes a dotnet-ml variant using ML.NET. Use when /sdlc-core:init is choosing a stack, when a spec needs the committed stack for a .NET repo, or when the user asks what the default stack is.
---

# stack-dotnet — the .NET/C# stack pack

A **stack pack**: one opinionated answer to "what do we build this with", consumed by
`/sdlc-core:init` when a repo is set up and by `spec-writer` when it needs the committed stack.
The SDLC framework itself knows nothing about .NET — this file is where all of that lives.

This owner's default for **everything**, competitions included. `init` proposes a profile so the
owner confirms rather than composing a stack from nothing. A proposal is not a decision: say
what you picked and why, and take an override without argument.

What this pack scaffolds is declared in `scaffold.json`, beside this file; the files it copies
live in `assets/`, beside both. See **What the scaffold writes**.

## Contents

- Choosing a profile
- Profile: `dotnet` (default)
  - Why the SDK pin matters here specifically
  - Infrastructure is Terraform
  - Containers are local. The cloud is PaaS.
  - Scripts and notebooks are exploration only
  - Validation — what has to be green before a commit
- Profile: `dotnet-ml`
- The local SQL password
- What the scaffold writes
- What never gets asked

## Choosing a profile

1. **An existing stack in the repo wins outright** — never propose something that contradicts
   code already there.
2. **Competition, contest, or ML repo** → `dotnet-ml` (the `dotnet` profile plus ML.NET).
3. **Otherwise** → `dotnet`.

State the signal in one line: *"No project files and no competition markers, so proposing the
dotnet profile."*

## Profile: `dotnet` (default)

| Commitment | Value |
|---|---|
| Language | C#, `net10.0`, `LangVersion latest` |
| Nullable | `enable`, warnings as errors |
| Front end | **Blazor** |
| APIs | **ASP.NET Core** |
| Data access | **EF Core**, code-first migrations |
| Orchestration (local) | .NET Aspire — AppHost + ServiceDefaults |
| Database | SQL Server via Aspire, persisted with `WithDataVolume` |
| Local emulation | Docker containers, orchestrated by Aspire — **local only** |
| Deployed compute | **Azure PaaS: Web Apps, Function Apps, Azure SQL.** Not containers |
| Exploration | **Polyglot Notebooks** (VS Code) and `.csx` scripts — `.ipynb` on the .NET Interactive kernel |
| Tests | **MSTest** (the `MSTest` meta-package) |
| Coverage | `coverlet.collector`, Cobertura, `coverage.runsettings` |
| Integration tests | `Aspire.Hosting.Testing` against a real AppHost |
| Style | `.editorconfig` + `EnforceCodeStyleInBuild` |
| Packages | Central Package Management (`Directory.Packages.props`) |
| SDK | pinned in `global.json` |
| Infrastructure | **Terraform**, remote state, provider versions pinned |
| Cloud | Azure — this pack targets .NET **on Azure**; another cloud is a different pack |

**One solution, not one per module.** Vertical slices live inside it. Splitting a grown
solution into modules is a later, explicitly-invoked act, once the seams are known rather than
guessed in an opening interview — so nothing here asks the owner where the module boundaries
fall. Asking would collect an answer nothing this pack scaffolds reads.

### Why the SDK pin matters here specifically

Aspire's AppHost SDK version, the Aspire NuGet package versions, and the **installed Aspire CLI
bundle** must all agree. When they don't, the AppHost fails at startup with an error that names
the wrong side of the mismatch. `global.json` pins the SDK and `Directory.Packages.props` pins
the packages, so only the CLI is left to keep in step manually. Say this out loud when writing
the charter's Technology commitments — it is the kind of thing that costs an afternoon once.

### Infrastructure is Terraform

Every environment is created by Terraform, never by portal clicks, and never by a script that
cannot be re-run. Two things are pinned for the same reason `global.json` pins the SDK: an
unpinned `required_version` or provider constraint upgrades itself between a plan and an apply,
and the diff that gets applied is then not the diff that was reviewed.

**State is remote, always.** Local state cannot be shared, cannot be locked, and dies with the
machine. The storage account holding state is created **out of band** — the thing that stores
state cannot be managed by the state it stores — and `versions.tf` leaves its backend block
blank for that reason.

`terraform.gitignore` keeps state out of the repository — state can hold secret values in plain
text. It deliberately does **not** ignore two things. `.terraform.lock.hcl` is committed because
provider versions are part of the build and an uncommitted lock file pins nothing. `*.tfvars` is
committed because a tfvars file holds identifiers and never secrets: ignoring it would *hide* a
leaked secret rather than prevent one, where committed-and-reviewable makes the violation
visible. If a tfvars ever needs a secret, that is the bug, not the gitignore line.

A change that creates or alters infrastructure is a change like any other: it belongs to a
slice, it names its files in the plan manifest, and `terraform plan` output is the evidence that
the review is reviewing.

### Containers are local. The cloud is PaaS.

Aspire runs containers to emulate dependencies on a developer machine — SQL Server, Redis,
storage. **Deployed environments run none of them.** Test and production are Azure PaaS: Web
Apps, Function Apps, Azure SQL. A container in a deployed environment is a decision nobody made.

This has one consequence big enough to state as an invariant in every repo built with this pack:

> **The AppHost resource graph describes local development and never production. Terraform is
> the single source of truth for anything deployed.**

Say this out loud when writing the charter's Technology commitments, because the alternative is
a team discovering it the expensive way. Aspire's own deployment story (`azd`) provisions
Container Apps, which is precisely what this stack does not use — so the two models will never
converge, and the time spent trying to make them agree is time spent on a reconciliation that
was never going to happen. Two models, one of them explicitly not authoritative, is a stable
arrangement. Two models both claiming authority is not.

What this means per change:

- Local dependency added to the AppHost → nothing is owed to Terraform. It is a dev convenience.
- A deployed resource added → **Terraform, always**, in the slice's file manifest, with the
  `terraform plan` output pasted into the review. The AppHost may gain a matching local
  emulation, but that is a second, separate act.
- A resource that exists in the AppHost and not in Terraform is **not deployed**, no matter how
  well it works locally. That failure mode reads as "works on my machine" and is the single most
  likely way this split goes wrong.

### Scripts and notebooks are exploration only

C# scripts (`.csx`) and **C# notebooks** are both endorsed for exploration: pulling a dataset
apart, checking a hypothesis, sketching an algorithm before it has a home. Use them freely.
Prefer them over Python notebooks — the language stays the same as the thing being built, so an
idea moves into a project without a rewrite.

**Polyglot Notebooks (VS Code) is the notebook frontend.** It needs nothing beyond the SDK,
gives C# completion and debugging, and sits next to the solution the notebook is exploring.

The reason it is the default rather than JupyterLab is the same reason this stack chose
PowerShell for its own tooling: **JupyterLab is a Python application**, so standardising a team
on it puts Python on every developer machine to run notebooks in a language that needs none.

Nothing is locked in by that choice, which is why it is safe to make. A C# notebook is an
ordinary **Jupyter notebook** — `.ipynb`, run by the .NET Interactive kernel — so a developer
who already has Jupyter can register the kernel and open the identical file, and any notebook
written here stays readable by the whole Jupyter ecosystem. The frontend is a preference; the
format and the kernel are the commitment.

**But be exact about what they do not get.** Neither has a project file, so
`Directory.Build.props` never reaches them. In practice that means **no analyzers, no
`EnforceCodeStyleInBuild`, and no `TreatWarningsAsErrors`** — the entire validation gate this
pack relies on is silently absent. Nullable reference warnings that would fail the build in a
project are invisible in a notebook cell.

So the rule, and it is not negotiable because the gate cannot be made to apply:

> **Nothing graduates from a script or a notebook to a deliverable by being copied.** It moves
> into a project, compiles under the same analyzers as everything else, and gets tests. Until
> then it is a sketch, regardless of how finished it looks.

Treat a spec that proposes shipping behaviour that lives in a `.csx` or a notebook as a flagged
concern, not a detail. The code is unvalidated by construction, and it will look exactly as
trustworthy as code that was validated.

### Validation — what has to be green before a commit

`sdlc-core` requires a green build and passing tests before anything is committed, at every
tier. These are the commands for this stack.

```
dotnet build                                          # warnings are errors; a warning fails this
dotnet test                                           # unit and integration together
dotnet test --settings coverage.runsettings           # when the coverage number is being claimed
```

**Both suites, not just the fast one.** Unit tests exercise a class; integration tests
exercise the composed system through `Aspire.Hosting.Testing` against a real AppHost, with the
real SQL Server container. Most of what goes wrong in this stack goes wrong *between*
components — a connection string that resolves locally and not in the composed graph, a
migration that applies to an empty database and not a persisted volume, an Aspire parameter
that silently never resolves. A unit suite cannot see any of it.

Read the output, not the exit code:

- **`dotnet build` is the first test.** `TreatWarningsAsErrors` plus `EnforceCodeStyleInBuild`
  means a nullable warning, a style violation, and a moderate-or-worse NuGet advisory each
  fail the build. That is the gate working, not the build being fussy — fix the finding rather
  than adding a `NoWarn`.
- **A run that collected zero tests exits clean.** Check the count, not just the colour. A
  renamed or mis-targeted test project reports success by finding nothing.
- **Integration tests failing to start the AppHost** usually means the Aspire CLI, the
  `Aspire.AppHost.Sdk` in `global.json`, and the Aspire package versions have drifted apart.
  The error names the wrong side of the mismatch; check all three.

If the build or a test is red before your change, say so before touching anything, and do not
fold someone else's breakage into your own verification.

## Profile: `dotnet-ml`

Everything in `dotnet`, plus:

| Commitment | Value |
|---|---|
| ML | ML.NET (`Microsoft.ML`) |
| Gradient boosting | `Microsoft.ML.LightGbm` |
| AutoML | `Microsoft.ML.AutoML` |
| Dataframes | `Microsoft.Data.Analysis` |
| Structure | `MLContext` estimator pipelines, one per experiment |

ML.NET pipelines are `IEstimator` chains — `Fit` produces an `ITransformer`, and the
transformer is the artifact worth persisting per experiment. Keep the pipeline definition and
its fitted output separable so an experiment can be replayed without retraining.

### Flag this honestly when proposing it for a Kaggle competition

Kaggle's notebook kernels are Python. A C# entry works only for competitions that accept a
plain submission file, and the public solutions, notebooks and community discussion for any
given competition will be Python end to end, so none of it is directly reusable. ML.NET does
cover the ground that matters — LightGBM is there, AutoML is there — but this is a real
handicap in a competitive setting, not a neutral choice.

Say it once, in one or two sentences, when proposing `dotnet-ml` for a competition repo. Then
record the owner's answer as a charter commitment and stop raising it.

## The local SQL password

The owner's convention: SQL Server persists via a data volume, and local development uses the
password `password1!` so no per-project secret setup is needed.

Wire it as an Aspire parameter with that value as the **default**, not as a bare literal:

```csharp
// Local development default. Overridable per developer via:
//   dotnet user-secrets --project src/AppHost set "Parameters:sql-password" "<value>"
IResourceBuilder<ParameterResource> sqlPassword = builder.AddParameter(
    "sql-password",
    builder.Configuration["Parameters:sql-password"] ?? "password1!",
    secret: true);

IResourceBuilder<SqlServerServerResource> sql = builder
    .AddSqlServer("sql", password: sqlPassword)
    .WithDataVolume();
```

Two things this shape buys that a bare literal does not:

- **A default means the parameter always resolves.** An *unresolved* Aspire parameter does not
  raise an error — the resource is silently never handed to DCP, and the AppHost starts with no
  database and no explanation. A default removes that failure mode entirely.
- **Rotation does not break the volume.** A persisted data volume keeps the auth metadata from
  when it was created. Change the password without recreating the volume and SQL rejects the
  login. A constant default is stable by construction, which is the property that matters.

**Guard it so it can never reach a deployed environment.** The default is for Development and
integration-test environments only:

```csharp
if (!builder.Environment.IsDevelopment()
    && builder.Configuration["Parameters:sql-password"] is null)
{
    throw new InvalidOperationException(
        "The local development SQL password default is not permitted outside Development. "
        + "Supply Parameters:sql-password from configuration for this environment.");
}
```

Note for the charter: `password1!` satisfies SQL Server's container password policy (10
characters; lowercase, digit and symbol is three of the required four categories) but has no
uppercase, so it clears the bar rather than comfortably passing it. It is committed to the
repository by design — that is acceptable for a value whose only job is to unlock a local
container, and unacceptable for anything else. Never reuse it for a cloud resource, and never
let the same value appear in a Key Vault entry, a connection string for a deployed service, or
a CI variable.

## What the scaffold writes

**`scaffold.json`, beside this file, is the list.** It declares everything this pack
scaffolds — the checks that run before anything is written, the directories, the generator
commands, the files copied from `assets/`, the lines appended, the post-generation rewrites,
and the generated sample content that is removed again — for each solution kind. Read it when
you need to know what this pack produces.

**Do not restate that list here, and do not write any of those files by hand.** A second list
beside the manifest is two lists nothing checks against each other, and the first thing that
happens is one of them quietly stops being true. The shipped executor performs the manifest,
and it is the only thing that writes a file this pack names:

```
powershell -NoProfile -ExecutionPolicy Bypass -File "<sdlc-core>/scripts/Invoke-ScaffoldManifest.ps1" -Manifest <this pack>/scaffold.json -Kind <kind> -Target <repository directory> [-Solution <Identifier>]
```

Two kinds are declared. **`web`** emits a Blazor front end and an ASP.NET Core API composed by
an Aspire AppHost, with service defaults, a unit suite and an integration suite. **`competition`**
emits a feature-and-scoring library, a training runner, a unit suite and a starter notebook,
and selects the `dotnet-ml` profile. The interview settles which one; a kind is not guessed.

Three properties of the executor are worth knowing before reading the manifest, because they
are what make a re-run safe:

- **An existing file is never overwritten.** A repo that already has a `.editorconfig` has
  already made that decision, and replacing it silently rewrites every file on the next format
  run. The action is skipped and reported as skipped.
- **The ignore entries are appended, not copied.** Only the lines not already present are
  added, so a `.gitignore` a generator wrote first still gets them, and a second run adds
  nothing.
- **Re-running fills gaps.** Nothing is regenerated, nothing is rewritten twice, and the run
  exits 0 having changed no file.

### The prerequisite the `web` kind has, and why it is not installed for you

The Aspire project templates are **not in the SDK box** — they resolve on a machine where
`Aspire.ProjectTemplates` was installed from nuget.org at some point, and are simply absent
otherwise. The manifest declares them as preflight checks, so on a machine without them the
run writes nothing at all and relays the install command for the owner to run themselves.

That is the "never mutates the developer's machine" rule being honoured rather than a gap: a
global template install outlives the repository that caused it, and half-scaffolding is worse
than failing clearly. Say so plainly if it happens, and do not run the install on the owner's
behalf.

The `competition` kind has no such prerequisite — `sln`, `classlib`, `console` and `mstest` are
all in the box.

### The solution token

`Directory.Build.props` and several other assets carry a `{{SOLUTION}}` token, and the manifest
declares it. The executor substitutes it and then sweeps every file under the target for it, so
a token that survives — including one left by an earlier run — fails the run naming the file.
That check exists because leaving it in place produces assemblies literally named
`{{SOLUTION}}.Foo`, which nothing else notices.

The identifier itself is derived from the repository directory name, confirmed by the owner
during the interview, and on a later run recovered from the solution file already on disk
rather than re-derived. It becomes every assembly name, every root namespace and every project
directory, so it is confirmed rather than assumed.

### Generator output has to be reconciled

Every template emits package references with an inline `Version` attribute, which central
package management rejects outright (`NU1008`), and the AppHost template hardcodes its SDK
version inline where it silently overrides the pin in `global.json`. The manifest fixes both
with regular expressions it supplies itself, each carrying an assertion about the file's end
state so that a template which changes shape fails loudly rather than quietly unpinning.

The consequence for `Directory.Packages.props` is a standing cost worth naming: **every package
any generator emits needs a version pinned there**, and the only thing that catches a missing
one is a failing restore. Expect to add entries when the template pack moves.

The charter records the **profile name and overrides**, never the contents of these files:

```markdown
## Technology commitments

- Profile: `dotnet` (C# net10.0, Blazor, ASP.NET Core, EF Core, Aspire, MSTest, coverlet)
- Persistence: SQL Server via Aspire locally, Azure SQL deployed; WithDataVolume, local
  password default
- Deployed compute: Azure PaaS (Web Apps, Function Apps). Not containers.
- Solution layout (`web` kind): `src/{Solution}.AppHost`, `src/{Solution}.ServiceDefaults`, `src/{Solution}.Web`, `src/{Solution}.Api`, `tests/{Solution}.UnitTests`, `tests/{Solution}.IntegrationTests`
- Solution layout (`competition` kind): `src/{Solution}.Features`, `src/{Solution}.Training`, `tests/{Solution}.UnitTests`
```

**Keep the layout line for the kind that was scaffolded and delete the other.** It describes
what is on disk after the run — nothing more. It is deliberately not a statement about where
the code *should* go, because a charter line saying that is read as binding by every later
spec, and the previous one committed every scaffolded repository to a per-module layout with
an `Application` project that the scaffold has never emitted.

This is the one place the pack restates something `scaffold.json` already says, against the
rule under **What the scaffold writes** that a second list beside the manifest always drifts.
It is allowed here, and only here, because `PackPromiseTests` reads the two against each other
on every run and fails naming the project and the document that omits it — a checked second
list is a different thing from an unchecked one.

And add this to that repo's **Architectural invariants**, not its commitments — it is a
property that makes a change wrong regardless of intent, which is the test for an invariant:

```markdown
- The AppHost resource graph describes local development only. Terraform is the single
  source of truth for every deployed resource. A resource present in the AppHost and absent
  from Terraform is not deployed.
```

## What never gets asked

**Code style is not interrogated and does not belong in the charter.** Brace placement, indent
width and naming are enforced by `.editorconfig` plus `EnforceCodeStyleInBuild`, which
`TreatWarningsAsErrors` promotes to build failures. That is deterministic and checkable in CI;
prose in a charter is neither.

The charter holds *invariants* — properties that make a change wrong regardless of what it was
trying to do. Style is not one, and mixing the two dilutes the section that gates future
intents.

If the owner raises style unprompted, translate it into an `.editorconfig` rule rather than
recording it as prose anywhere.
