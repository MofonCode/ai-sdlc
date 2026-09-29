<!--
covers:
  - "Directory.Packages.props"
  - "Directory.Build.props"
  - "global.json"
  - "tools/Get-StackDefaults.ps1"
-->

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
| Testing | MSTest on Microsoft.Testing.Platform, wired as below |
| Shared kernel | `MofonCode.Core` |
| Local orchestration | .NET Aspire AppHost, under the condition below |

### Microsoft.Testing.Platform needs two settings, or `dotnet test` does not run

On the .NET 10 SDK, `dotnet test` defaults to VSTest and MTP refuses that path. A plan that names MSTest
on MTP must declare **both** of these, or the command every `AGENTS.md` documents fails:

`global.json` — alongside the `sdk` section:

```json
"test": {
  "runner": "Microsoft.Testing.Platform"
}
```

The test project — or `Directory.Build.props`, which is better when a solution has more than one test
project, because a project added later without it silently runs a different runner:

```xml
<EnableMSTestRunner>true</EnableMSTestRunner>
```

**Do not set `TestingPlatformDotnetTestSupport`.** It was the .NET 9 mechanism, it is legacy on 10, and
setting it instead of the two above is how a trial run ended up unable to run `dotnet test` at all — the
evidence had to come from invoking the test executable directly, so the documented command and the
working command were different commands.

Verified on the .NET 10 SDK with MSTest 4.4.1: bare `dotnet test` from the solution root passes with
both settings and fails with either one missing.

### When a solution gets an Aspire AppHost

**More than one process, or one process plus a backing service** — a database, queue, cache, or anything
a developer would otherwise start by hand. Then an AppHost earns its place: it is what makes the local
run reproducible, and it brings emulators rather than live Azure resources.

**A single executable with no backing services does not get one.** An AppHost there is an extra project,
an extra launch profile and a slower start for no orchestration. Aspire can be added later without
restructuring anything, which is the argument against adding it pre-emptively — a plan that stands one up
for a lone console app has spent the cycle on scaffolding rather than on the change.

Prefer emulators over live Azure resources wherever one exists.

### Results, not exceptions

**An operation that can fail returns a `Result` or `Result<T>`.** `FluentResults`, through
`MofonCode.Core`. A failure a caller is expected to handle — not found, rejected, conflicting, refused
by a downstream service — is a value the signature declares, not a control-flow jump the signature
hides. Exceptions are for programmer error and for conditions nothing can recover from.

At a boundary — an endpoint, a page, a handler — a failed `Result` becomes the response. Do not convert
it back into an exception to be caught somewhere else.

**Do not wrap an operation that cannot fail.** `Result<int>` around `Random.Shared.Next(1, 7)` is a type
that is always success: it adds ceremony and proves nothing, and a reader now has to check a branch that
cannot be taken. The test for this is whether you can name a failure a caller would act on. If you
cannot, return the value.

### Commands and queries go through Mediator

**An application operation is a request and a handler**, not a method on a service reached by direct
injection. `IRequest<T>` with `IRequestHandler<,>` for something that answers; the `ICommandRequest*`
equivalents where nothing is returned. Cross-cutting concerns — validation, tracing, retry — are
`IRequestBehavior<,>` implementations, so they are written once rather than repeated in every handler.

**It earns its place when there is an operation to dispatch.** More than one step, a decision, a
validation, something worth a behaviour around it. A single infallible expression called from one page is
not an application operation, and routing it through a request and a handler leaves three files where one
line was the whole of it — the same objection as an AppHost for a lone console app.

### One folder per feature, not one folder per layer

**A feature owns its request, its handler, and the types only it uses**, in one place. `Rolling/` holding
the roll request, its handler and its abstraction beats `Services/`, `Models/` and `Handlers/` each
holding a third of three unrelated features.

Where a framework dictates a location, let it have that one file and keep the rest of the feature
together: a Blazor page lives under `Components/Pages/` because routing expects it there, and everything
else it needs stays in the feature folder rather than being scattered to match the page.

**A plan departs from any of the three by arguing it, in the plan.** These are defaults, and a departure
that is reasoned in the artifact a person approves is a decision. One that is silent is a habit.

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
