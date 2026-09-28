---
covers:
  - "Directory.Packages.props"
  - "Directory.Build.props"
  - "global.json"
  - "tools/Get-StackDefaults.ps1"
---

# Tech stack

Two audiences, and the split matters. The **defaults** below are shipped to every solution the framework
scaffolds — `tools/Get-StackDefaults.ps1` extracts them and a plan receives them injected, because a
session working in a governed solution cannot read this file. Everything after the defaults is about how
*this* repository is built, and stays here.

A solution may depart from any default. `.sdlc/solution.md` records the stack it chose, and a plan that
departs says why in one sentence — the departure is legal, silence about it is not. Global constraints
are stack-neutral by rule, so nothing here is enforced by the framework; a solution that wants a default
to bind adds a `LOC_` constraint or an architecture test of its own.

<!-- stack-defaults:begin -->

## Defaults for a scaffolded solution

| Concern | Default |
|---|---|
| API | ASP.NET Core |
| Web | Blazor |
| Multiplatform UI | .NET MAUI Blazor Hybrid — desktop, web and Android from one codebase |
| Desktop | .NET MAUI |
| Relational data | SQL Server |
| Document data | Cosmos DB |
| Machine learning | ML.NET |
| Testing | MSTest on Microsoft.Testing.Platform |
| Shared kernel | `MofonCode.Core` |
| Local orchestration | .NET Aspire AppHost, under the condition below |

### When a solution gets an Aspire AppHost

**More than one process, or one process plus a backing service** — a database, queue, cache, or anything
a developer would otherwise start by hand. Then an AppHost earns its place: it is what makes the local
run reproducible, and it brings emulators rather than live Azure resources.

**A single executable with no backing services does not get one.** An AppHost there is an extra project,
an extra launch profile and a slower start for no orchestration. Aspire can be added later without
restructuring anything, which is the argument against adding it pre-emptively — a plan that stands one up
for a lone console app has spent the cycle on scaffolding rather than on the change.

Prefer emulators over live Azure resources wherever one exists.

### `MofonCode.Core` — the shared kernel

One opinionated core, so every solution has the same shape for the things every solution needs. Name the
seam and the type; a plan that says only "use MofonCode.Core" has not chosen anything.

| When a cycle needs | Use |
|---|---|
| request and handler dispatch | `Mediator`, `IRequest<T>`, `IRequestHandler<,>`, `IRequestBehavior<,>`, and the `ICommandRequest*` equivalents where nothing is returned |
| a return value that carries failure | `FluentResults`, plus `ResultExtensions.ToErrorString` and `ThrowIfFailed` |
| tracing a handler or an endpoint | `TraceResult<T>`, `TraceScope`, `TraceResultAttributes`, `TracerFlushRegistry` |
| bounded parallelism | `Concurrency`, and `HandlerLockRegistry` where a handler must not overlap itself |
| composing predicates for a query | `SpecificationsHelper.And`, `.Or`, `.Not`, `.All` |

Two things a plan has to get right about it.

**The results library is `FluentResults`.** `MofonCode.Core` extends it rather than replacing it, so a
criterion or a proof that says `Result<T>` is ambiguous until it says which. Name both.

**It is version `0.1.0`.** Every solution taking it as a default inherits its breaking changes, so pin it
in `Directory.Packages.props` with the rest — central pinning is already the default, and this is the
dependency that most repays it. It depends only on `FluentResults` and the
`Microsoft.Extensions.*.Abstractions` packages: no EF Core, no SQL client, nothing that decides a data
story on a solution's behalf.

### A note on ML.NET

It is the right default for consistency with the rest of the stack, and it costs real capability against
the Python ecosystem on competitive modelling work. Name that cost when a solution's core value is model
quality rather than integration.

<!-- stack-defaults:end -->

---

## How this repository is built

Version pins are centralized in `Directory.Packages.props` and analyzer policy in
`Directory.Build.props`. Warnings are errors with `WarningsNotAsErrors` deliberately empty, so an
exception is one visible line in that file rather than a pragma buried in code.

Three packages are not Microsoft-published, each because no on-stack option exists rather than as a
departure from the defaults above:

| Package | Why |
|---|---|
| `JsonSchema.Net` | `System.Text.Json` does not validate schemas, and Microsoft publishes no validator |
| `YamlDotNet` | .NET ships no YAML reader, and frontmatter must be read by a real parser |
| `coverlet.collector` | coverage collection |

The YAML one has history. In v02 a hand-rolled frontmatter regex reported that a skill declared
`disable-model-invocation` while the real parser had dropped every field in that file. The skill was
model-invocable at runtime and 333 tests said otherwise. A check on markup is worth only what the parser
behind it is.

## Shell

Shipped tooling is Windows PowerShell 5.1, invoked as `powershell.exe`. Not PowerShell 7: hooks are the
deterministic layer, and a hook that fails to start because an edition is not installed protects nothing
while appearing to. 5.1 is present on every Windows machine.

That choice has a cost, and scripts in `tools/` are written for it: no `&&` or `||`, no ternary, no `??`,
`ConvertFrom-Json` returns a `PSCustomObject` rather than a hashtable, and `Test-Json` does not exist.
Schema validation therefore lives in the test project, never in a script.
