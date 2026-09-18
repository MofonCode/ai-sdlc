---
name: stack-dotnet
description: The .NET/C# stack pack - net10.0, Blazor front ends, ASP.NET Core APIs, EF Core, Aspire orchestration with Docker-based local emulation, Azure PaaS (Web Apps, Function Apps, Azure SQL) for deployed environments with Terraform as the single source of truth, SQL Server with a persisted volume, MSTest, coverlet coverage, and the scaffold files. C# scripts and notebooks are endorsed for exploration only, because they sit outside the analyzer gate. Includes a dotnet-ml variant using ML.NET. Use when /sdlc-core:init is choosing a stack, when a spec needs the committed stack for a .NET repo, or when the user asks what the default stack is.
---

# stack-dotnet — the .NET/C# stack pack

A **stack pack**: one opinionated answer to "what do we build this with", consumed by
`/sdlc-core:init` when a repo is set up and by `spec-writer` when it needs the committed stack.
The SDLC framework itself knows nothing about .NET — this file is where all of that lives.

This owner's default for **everything**, competitions included. `init` proposes a profile so the
owner confirms rather than composing a stack from nothing. A proposal is not a decision: say
what you picked and why, and take an override without argument.

Assets referenced below live in `assets/`, beside this file.

## Contents

- Choosing a profile
- Profile: `dotnet` (default)
  - Why the SDK pin matters here specifically
  - Infrastructure is Terraform
  - Containers are local. The cloud is PaaS.
  - Scripts and notebooks are exploration only
- Profile: `dotnet-ml`
- The local SQL password
- Files written
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
| Tests | **MSTest** (the `MSTest` meta-package) |
| Coverage | `coverlet.collector`, Cobertura, `coverage.runsettings` |
| Integration tests | `Aspire.Hosting.Testing` against a real AppHost |
| Style | `.editorconfig` + `EnforceCodeStyleInBuild` |
| Packages | Central Package Management (`Directory.Packages.props`) |
| SDK | pinned in `global.json` |
| Infrastructure | **Terraform**, remote state, provider versions pinned |
| Cloud | Azure — this pack targets .NET **on Azure**; another cloud is a different pack |

Ask, rather than assume: the solution's module boundaries, and whether each module is a
library, an API, or a function app.

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

C# scripts (`.csx`) and Polyglot notebooks running the C# kernel are both endorsed for
exploration: pulling a dataset apart, checking a hypothesis, sketching an algorithm before it
has a home. Use them freely. Prefer them over Python notebooks — the language stays the same as
the thing being built, so an idea moves into a project without a rewrite.

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

## Files written

On confirmation, write these from `assets/` if absent. The `.gitignore` entries are **appended** if absent rather than replacing the file. Otherwise, **never overwrite an existing one** — a
repo that has one has already made the decision, and replacing it silently rewrites every file
on the next format run.

| File | Source | Purpose |
|---|---|---|
| `.editorconfig` | `assets/dotnet.editorconfig` | style, naming, EF-migration exemptions |
| `Directory.Build.props` | `assets/Directory.Build.props` | warnings as errors, NuGet audit, security analyzers, assembly naming |
| `Directory.Packages.props` | `assets/Directory.Packages.props` | one package version per solution |
| `global.json` | `assets/global.json` | pinned SDK and Aspire AppHost SDK |
| `coverage.runsettings` | `assets/coverage.runsettings` | Cobertura output, generated-code exclusions |
| `terraform/versions.tf` | `assets/terraform/versions.tf` | pinned Terraform and provider versions, remote backend stub |
| `.gitignore` (append) | `assets/terraform.gitignore` | state and tfvars never reach the repo |

`Directory.Build.props` carries an assembly-naming convention of `{{SOLUTION}}.{ProjectName}`.
**Replace every `{{SOLUTION}}` token with this repo's solution name** when writing the file, and
say that you did. Leaving the token in place produces assemblies literally named
`{{SOLUTION}}.Foo`.

The charter records the **profile name and overrides**, never the contents of these files:

```markdown
## Technology commitments

- Profile: `dotnet` (C# net10.0, Blazor, ASP.NET Core, EF Core, Aspire, MSTest, coverlet)
- Persistence: SQL Server via Aspire locally, Azure SQL deployed; WithDataVolume, local
  password default
- Deployed compute: Azure PaaS (Web Apps, Function Apps). Not containers.
- Module layout: vertical slices, each {Module}.Api / .Application / .UnitTests
```

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
