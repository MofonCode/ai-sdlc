---
covers:
  - "Directory.Packages.props"
  - "Directory.Build.props"
  - "global.json"
---

# Tech stack

The defaults every scaffolded solution starts from, and the reference a plan consults when choosing
a project shape. A solution may declare otherwise in `.sdlc/solution.md`; global constraints are
stack-neutral so that departure is legal rather than an immediate violation.

## Defaults

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
| Local orchestration | .NET Aspire AppHost, emulators over live Azure resources wherever one exists |

**ML.NET** is the default for consistency with the rest of the stack, and it costs real capability
against the Python ecosystem on competitive modelling work. Name that cost when a solution's core
value is model quality rather than integration.

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
model-invocable at runtime and 333 tests said otherwise. A check on markup is worth only what the
parser behind it is.

## Shell

Shipped tooling is Windows PowerShell 5.1, invoked as `powershell.exe`. Not PowerShell 7: hooks are
the deterministic layer, and a hook that fails to start because an edition is not installed protects
nothing while appearing to. 5.1 is present on every Windows machine.

That choice has a cost, and scripts in `tools/` are written for it: no `&&` or `||`, no ternary, no
`??`, `ConvertFrom-Json` returns a `PSCustomObject` rather than a hashtable, and `Test-Json` does
not exist. Schema validation therefore lives in the test project, never in a script.
