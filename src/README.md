# Architecture

What `Sdlc.Core` and `Sdlc.Cli` are, where the boundary between them runs, and where a new thing goes.

Written for somebody **changing** the framework. It describes the shape of the code and nothing about why
the design is what it is — `../CONCEPT.md` is the authority on every design question, and its decisions are
cited by id rather than restated.

A code remark citing `D-38` means the entry under that id in `../CONCEPT.md` §20. Look it up rather
than reading the file: `sed -n '/^### D-38/,/^### D-39/p' ../CONCEPT.md`. Each entry says which
sections it governs and what it rejected, and `../AGENTS.md` has the other two lookups.

## Two projects

| | |
|---|---|
| `Sdlc.Core/` | every decision the gate makes. Knows about constraints, cycles, approvals, findings, evidence, hooks and the chain. Knows nothing about a command line, a console or a host. |
| `Sdlc.Cli/` | the `sdlc` binary. Reads a command line, opens a container, dispatches one request, prints what came back, and returns an exit code. Decides nothing itself. |

`Sdlc.Core` is a **leaf — it references no other project**, because it is what everything else depends on.
That is a named test rather than an intention: `The_core_references_no_other_project`. Its package
dependencies are `MofonCode.Core` for the mediator and `YamlDotNet` for the frontmatter and the ledgers.

`Sdlc.Cli` is packed as a dotnet tool, so `sdlc` is one command on `PATH` and the same binary the pre-tool
hook, the git hooks and the pipeline all call. Its types are `internal` — an application exposes no API —
and the suite reaches them through `InternalsVisibleTo`, because going through the process boundary to test
argument parsing would test the shell more than the parser.

Two settings in `Sdlc.Cli/Sdlc.Cli.csproj` exist for one reason: **a hook invokes this binary on every tool call an
agent makes**, so start-up is on the critical path of every edit. `InvariantGlobalization` and `TieredPGO`
are there to pay for that, and the hook path below is shaped by the same cost.

## One operation, one holder

Every operation in `Sdlc.Core` is a static holder class carrying four things, and `EffectiveConstraints` is
the one to read first:

```csharp
public static class EffectiveConstraints
{
    public static IServiceCollection AddEffectiveConstraintsRequestHandler(this IServiceCollection services)
        => services.AddTransient<IRequestHandler<Query, Response>, Handler>();

    public sealed record Response(/* what it produces */);

    public sealed record Query(string? Artifact = null) : IRequest<Response>;

    private sealed class Handler(IConstraintDocuments documents) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken) { }
    }
}
```

The handler is **`private` inside the holder**, and reachable only by dispatching the request. That is what
the self-registering extension method buys: the registration names the type from inside the one place that
can see it, and a factory written out in the container could not.

`SdlcServices.AddSdlc` is a list of those calls and **holds no closure** — a property that took a
`SolutionRoot` wrapper type to get, and without which no caller could substitute a clock through the
container at all. So a test substitutes a dependency the way the application composes one: register the
fake and dispatch, which proves the wiring as well as the handler.

A request is a `Query` when it reads and a `Command` when it writes. Handlers return
`FluentResults.Result<T>`, and **a refusal is a failed result, never an exception**. An exception out of a
handler means the gate broke, which is a different outcome with a different exit code.

`Every_request_the_core_declares_has_a_registered_handler` holds the pattern, and it earns its keep: a
`HookPolicyQuery` once existed, was never registered, and so could never be dispatched — the command line
had been calling the static method all along. That test found it on its first run.

## Deciding and presenting are separate

Every folder pairs an operation with a renderer — `EffectiveConstraints` with `ConstraintRenderer`,
`ReadCycleStatus` with `StatusRenderer`, `Land` with `LandRenderer`. A renderer is a **static pure function
from a response to a string**, holding no dependency and reading no disk.

Two things follow. A test asserts exact output without building a container, and one response can be
rendered at two detail levels — `sdlc constraints --brief` is a renderer argument, not a second query. The
split is also why `Sdlc.Core` owns the renderers despite knowing nothing about a console: producing text is
not writing it anywhere.

## How one invocation flows

```
argv
  -> CommandLine.Parse               verb, positional arguments, --options
  -> Workspace.Run                   resolves the solution root and the library,
                                     builds the container, disposes it after
  -> workspace.Send<TRequest, TResponse>
       -> Mediator -> Handler        the only thing that decides
       -> Result<TResponse>
  -> XRenderer.Render(response)      a string
  -> Console.Out                     or Console.Error on a failed result
  -> ExitCode
```

`Program.cs` is a top-level switch on the verb, and each arm is a small static local function doing exactly
those steps. Three verbs are answered **before any workspace is opened**: `help`, an empty verb, and
`version` — because the question somebody asks `version` is "what have I got installed", and a version that
refused outside a governed solution would be unanswerable at exactly the moment they are checking whether
the install worked.

`Workspace` exists so that no command resolves the solution root for itself. Two commands disagreeing about
which solution they are governing is a defect with no obvious symptom.

Where its two inputs come from:

- **the solution root** — `--solution`, else the working directory.
- **the global constraint library** — `Locate.Library`: the `SDLC_CONSTRAINTS` environment variable, then
  beside the running assembly, then up the tree from it. The last is what finds the repository copy when the
  gate runs from its build output rather than from an installed tool, and the variable is what the suite
  sets to point at a scratch library.

It also exposes **`Workspace.Repository`**, the container's `IRepository`, and that is the only service any
command reaches directly rather than by dispatching a request. Asking git for the configured user name
decides nothing and cannot refuse — it pre-fills who is approving on a draft a person then edits. It is
exposed as that one service rather than a general `Service<T>()`, which would make "dispatch a request" a
convention instead of the shape. `Sdlc.Cli` previously had its own `Git.UserName` doing the same shell-out,
and the copy never got either fix `GitRepository` did.

## Exit codes are a contract

A host hook, a git hook and a pipeline all read them, so `ExitCode` is not an implementation detail.

| | | |
|---|---|---|
| `0` | `Ok` | the command did what was asked |
| `1` | `Refused` | a rule refused it — **the gate working** |
| `2` | `Usage` | the command line could not be understood |
| `3` | `Failed` | **the gate itself broke** — never a governed stop |

The distinction that earns its keep is `Refused` against `Failed`. A caller that cannot tell them apart
reports its own bug as a governed stop. `Program.cs` catches `IOException` and `UnauthorizedAccessException`
at the top and returns `Failed` for exactly that reason: the gate could not read what it needed, and that is
not a rule refusing.

## The hook path, and the one deliberate exception

`sdlc hook` reads a host's payload on standard input and writes that host's own decision shape on standard
output. Two files between them hold everything host-specific. **`SupportedHosts` is the data** — one row
per host carrying its spellings, where its hook configuration goes, who owns that file, what it calls each
event, whether it has a turn end at all, and what it calls the timeout. `Templates`, `Initialise` and the
command line all read that row rather than carrying a host each; the table is also the support claim, and
the suite asserts every row is complete and that each host denies a protected write when fed a payload in
its own shape.

**`HookProtocol` is the code** — the only file that knows a host's vocabulary:

- `TryParseHost` and `TryParseEvent` — every spelling any host's own documentation uses, so a hook config
  copied from a host works without translation. The host spellings come from `SupportedHosts`, so what the
  command line accepts and what adoption configures cannot disagree.
- `ReadCall` and `ReadTurnEnd` — pull a `ToolCall` or a `TurnEnd` out of the payload, collecting the file
  path from every key a host is known to use, and classifying the call by **what it carries** rather than
  only by its name, so an unrecognised tool is still judged on whether it is about to write a file or run a
  shell.
- `WriteDecision` and `WriteTurnEndDecision` — two different shapes, which is the reason the adapter exists
  at all. A tool call is refused with a permission, nested under `hookSpecificOutput` on Claude Code and at
  the top level on Copilot. A turn is sent back with a top-level `decision`, which Claude Code spells
  `block`. **Neither writes anything on an allow.** An explicit `permissionDecision: "allow"` bypasses the
  host's permission system rather than standing for silence, so a policy with nothing to say says nothing —
  D-39 records why, and why `ask` is not the answer either.

`HookEvent` is named for the moment rather than for what any host calls it. Claude Code spells the second
one `Stop` and Copilot `agentStop`, both of which describe that host's state machine rather than the thing
being judged, and letting either name inland would put one host's vocabulary in the policy. Those two
spellings are fields on a row, which is the only place they belong.

**The payload readers are deliberately tolerant, and they are not driven by the table.** A reader that
threw on an unexpected shape would turn a renamed field into a hook that fails to start — and a hook that
fails to start permits everything while appearing to protect. So a shape this cannot understand allows the
call and says so on standard error, and the keys are collected from every spelling any host is known to
use rather than from the row of the host that asked. Narrowing the reader to one host's set would trade
that tolerance for tidiness: Copilot documents two payload shapes, only one was read, and on the other the
hook found no path, classified every call as harmless, and allowed writes to the four files only a person
writes.

Then the one exception to the request-and-handler rule, and the only place in the gate where it is right:

- **`HookPolicy.Decide` is a static pure function over a `ToolCall`**, not a request. The pre-tool hook runs
  on every tool call an agent makes, so building a container to reach a pure function would spend the
  start-up budget on indirection. It is also not an application operation by the framework's own test: one
  expression, called from one place, that cannot fail.
- **`TurnEndGate` is a request and a handler**, because it reads git and the recorded evidence and decides.
  That is a real operation.

Which moment is governed on which host is a user-facing fact, and `../docs/guides/install.md` has it.

## What the suite holds about the structure

The two patterns above are not conventions anybody has to remember — `../tests/Sdlc.Architecture.Tests/` holds
them, along with three more properties, and [`../AGENTS.md`](../AGENTS.md) lists the set. The one worth
knowing here is `Every_directory_holding_projects_is_scanned`, because it is what stops a new project
opting out of the rest.

`../tests/Sdlc.Tests/` is the behavioural suite. It exercises the binary against scratch solutions it builds on
disk, and `PluginTests.cs` asserts this repository's own claims — the skills on disk against what
`../CONCEPT.md` inventories, the README's counts and version, and that every path a document points at
exists with the capitalisation it states.

## Where a new thing goes

| You are adding | It goes |
|---|---|
| a decision the gate makes | a new holder in the `Sdlc.Core` folder that owns the subject, with its `Query` or `Command`, its `Response`, its private `Handler` and its registration method — then one line in `SdlcServices.AddSdlc` |
| output for a decision that exists | the folder's renderer, as another method or a detail argument. Not a second query |
| a command | an arm in the `Program.cs` switch, a local function beside the others, and a block in `Usage.cs` — which is what `sdlc help` prints, so it is not optional |
| a stage that needs language | a skill, not a command. `Stages.cs` is the list the binary answers for, so a person typing `sdlc spec` is told which invocation to use rather than "unknown command" |
| support for another host | a row in `SupportedHosts`, a branch in each of `HookProtocol`'s two write methods, and a captured payload in `HookTests`. Nothing anywhere else — not `Templates`, not `Initialise`, not a message in the command line. If the change reaches `HookPolicy`, the abstraction has leaked |
| a global constraint | `../CONSTRAINTS.md`. No code — `sdlc constraints` renders it, and nothing else may hold a copy |

Two rules cut across all of those. **A refusal is a failed `Result` carrying a reason a person can act on**,
never an exception and never a bare boolean. And **nothing restates a constraint** — not a skill, not a
guide, not this file.
