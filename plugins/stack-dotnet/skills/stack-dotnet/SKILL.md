---
name: stack-dotnet
description: The .NET/C# stack pack - net10.0, Aspire orchestration, SQL Server with a persisted volume, MSTest, coverlet coverage, and the scaffold files (.editorconfig, Directory.Build.props, Directory.Packages.props, global.json, coverage.runsettings). Includes a dotnet-ml variant using ML.NET. Use when /sdlc-core:init is choosing a stack, when a spec needs the committed stack for a .NET repo, or when the user asks what the default stack is.
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
| Orchestration | .NET Aspire — AppHost + ServiceDefaults |
| Database | SQL Server via Aspire, persisted with `WithDataVolume` |
| Tests | **MSTest** (the `MSTest` meta-package) |
| Coverage | `coverlet.collector`, Cobertura, `coverage.runsettings` |
| Integration tests | `Aspire.Hosting.Testing` against a real AppHost |
| Style | `.editorconfig` + `EnforceCodeStyleInBuild` |
| Packages | Central Package Management (`Directory.Packages.props`) |
| SDK | pinned in `global.json` |

Ask, rather than assume: the solution's module boundaries, and whether each module is a
library, an API, or a function app.

### Why the SDK pin matters here specifically

Aspire's AppHost SDK version, the Aspire NuGet package versions, and the **installed Aspire CLI
bundle** must all agree. When they don't, the AppHost fails at startup with an error that names
the wrong side of the mismatch. `global.json` pins the SDK and `Directory.Packages.props` pins
the packages, so only the CLI is left to keep in step manually. Say this out loud when writing
the charter's Technology commitments — it is the kind of thing that costs an afternoon once.

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

On confirmation, write these from `assets/` if absent. **Never overwrite an existing one** — a
repo that has one has already made the decision, and replacing it silently rewrites every file
on the next format run.

| File | Source | Purpose |
|---|---|---|
| `.editorconfig` | `assets/dotnet.editorconfig` | style, naming, EF-migration exemptions |
| `Directory.Build.props` | `assets/Directory.Build.props` | warnings as errors, NuGet audit, security analyzers, assembly naming |
| `Directory.Packages.props` | `assets/Directory.Packages.props` | one package version per solution |
| `global.json` | `assets/global.json` | pinned SDK and Aspire AppHost SDK |
| `coverage.runsettings` | `assets/coverage.runsettings` | Cobertura output, generated-code exclusions |

`Directory.Build.props` carries an assembly-naming convention of `{{SOLUTION}}.{ProjectName}`.
**Replace every `{{SOLUTION}}` token with this repo's solution name** when writing the file, and
say that you did. Leaving the token in place produces assemblies literally named
`{{SOLUTION}}.Foo`.

The charter records the **profile name and overrides**, never the contents of these files:

```markdown
## Technology commitments

- Profile: `dotnet` (C# net10.0, Aspire, MSTest, coverlet, .editorconfig)
- Persistence: SQL Server via Aspire, WithDataVolume, local password default
- Module layout: vertical slices, each {Module}.Api / .Application / .UnitTests
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
