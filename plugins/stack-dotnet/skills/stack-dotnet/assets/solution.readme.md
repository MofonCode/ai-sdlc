# {{SOLUTION}}

A .NET solution scaffolded by `/sdlc-core:init` with the `stack-dotnet` pack, web kind:
a Blazor front end and an ASP.NET Core API composed by a .NET Aspire AppHost, with a unit
suite and an integration suite.

Replace this paragraph with what the solution is for. Everything below it is the pack's, and
stays true whoever owns the repository.

## Layout

```
{{SOLUTION}}.slnx
src/{{SOLUTION}}.AppHost/           local orchestration - never production
src/{{SOLUTION}}.ServiceDefaults/   telemetry, health checks, service discovery
src/{{SOLUTION}}.Web/               Blazor front end
src/{{SOLUTION}}.Api/               ASP.NET Core API
tests/{{SOLUTION}}.UnitTests/       one class at a time
tests/{{SOLUTION}}.IntegrationTests/ the composed system, through Aspire.Hosting.Testing
terraform/                          every deployed resource
.sdlc/                              the artifact chain
```

## Build

```
dotnet build
```

Warnings are errors here, and `EnforceCodeStyleInBuild` pulls the `.editorconfig` style rules
into the same gate, so **the build is the first test**. A nullable warning, a style violation
and a moderate-or-worse NuGet advisory each fail it. Fix the finding rather than adding a
`NoWarn`.

## Test

```
dotnet test
dotnet test --settings coverage.runsettings
```

Both suites, not just the fast one. Most of what goes wrong in this stack goes wrong *between*
components, and a unit suite cannot see any of it.

**Read the count, not the colour.** A run that collected zero tests exits clean. A renamed or
mis-targeted test project reports success by finding nothing, which is why each suite ships
with a starter test that asserts something real about this solution.

The integration suite needs a container runtime for anything that starts the composed
application. Where there is none, it still has to compile.

## Run locally

```
dotnet run --project src/{{SOLUTION}}.AppHost
```

The AppHost starts the front end, the API and their local dependencies together and prints
the dashboard URL. Running a single project directly works, but it gets none of the wiring.

## The artifact chain

`.sdlc/` holds the SDLC artifacts, and it is where a change starts rather than in the code:

- `.sdlc/CHARTER.md` - what this repository is, what it commits to, and the invariants a
  change may not break. It is read before a spec is written and it is the thing an intent is
  argued against.
- `.sdlc/<slug>/` - one directory per change, holding `intent.md`, `spec.md` and `plan.md`.
  Each carries a `status:` field, and the next stage reads that field from the file rather
  than trusting that approval happened.

## The invariant this solution cannot break

> **The AppHost resource graph describes local development and never production. Terraform is
> the single source of truth for anything deployed. A resource present in the AppHost and
> absent from `terraform/` is not deployed, no matter how well it works locally.**

Aspire runs containers to emulate dependencies on a developer machine. Deployed environments
run none of them - they are Azure PaaS. Adding a local dependency to the AppHost owes nothing
to Terraform; adding a deployed resource means Terraform, always, with the `terraform plan`
output in the review.

That failure mode reads as "works on my machine" and is the single most likely way this split
goes wrong.
