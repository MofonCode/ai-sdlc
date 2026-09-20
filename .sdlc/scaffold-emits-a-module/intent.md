---
slug: scaffold-emits-a-module
title: A scaffolded solution starts with the architecture the pack already promises
status: draft
tier: feature
created: 2026-09-19
owner: peter
---

> **PARKED, 2026-09-20, not abandoned.** The owner chose a solution-level scaffold — a modular
> monolith with an `Application` project — over per-module projects, on the grounds that module
> boundaries gathered in an opening interview are a guess frozen into a directory structure, and
> that MarketSense's eleven modules were discovered rather than designed. Splitting a grown
> solution into modules becomes a separate, explicitly-invoked skill later.
>
> **Nothing below is wrong; it is unneeded.** A second placeholder exists only because `Billing`
> has to reach `src/Billing.Api`. Without per-module projects there is no second placeholder, so
> the five closures stop blocking anything.
>
> **They remain real defects against I5** — the engine still decides which tokens can exist, in
> the executor, in `init`'s invocation, in the report schema, in recovery, and in the derivation
> transform. Seven interrogations' worth of evidence is below and every line reference in it was
> read out of the code. It will be here when a second token is genuinely wanted.


# intent — the scaffold emits the architecture the pack already commits to

> **BLOCKED, and split from a larger draft on 2026-09-20.** Four changes were bound together
> here and the bundling let the draft assert three things that were not true. They are now:
> **(A)** the engine substitutes the tokens a manifest declares rather than one it names —
> `engine-substitutes-declared-tokens`, task tier, in progress; **(B)** this intent, the module
> scaffold, blocked on A because the module name cannot reach an emitted path without it;
> **(C)** the pack referencing the published package, independent of both and blocked only on
> the package existing; **(D)** the library repository itself, elsewhere.
>
> **Findings against this draft that survive the split and must be answered before approval:**
> the architecture test does not survive transplant — `FindProject` throws when a project is
> absent, so the shared-kernel row errors rather than passes once the library is a package, and
> two of its four methods are vacuous with one module; the DDD-in-the-pack constraint as worded
> condemns `init`'s own module question, which is generic rather than DDD and should stay; and
> gap-filling over an older repository currently emits the module and skips the pin file,
> leaving a project referencing a centrally-unpinned package.

## Problem

**The pack promises one architecture, the interview demands the answer needed to build it, and
the scaffold emits something else entirely.**

### The interview blocks on an answer nothing consumes

`init` asks for the solution's module boundaries. The pack tells it to ask. A requirement makes
it **stop the command** if they are unsettled:

> **R15.** IF the interview has not settled the solution kind and its module boundaries, THEN
> THE `init` skill SHALL stop and say which answer is missing, rather than choosing a kind.

`scaffold.json` contains the word *module* **zero times**. The interview refuses to proceed
without an answer and then discards it. The spec warned about this shape in another context —
*"an interview that asks nothing is decorative"* — and this is worse: it asks, blocks, and bins
the result.

### The pack commits to a layout it does not scaffold

`stack-dotnet` instructs `init` to write this into every scaffolded repository's charter:

```
- Module layout: vertical slices, each {Module}.Api / .Application / .UnitTests
```

The scaffold emits `<Solution>.Api`, `<Solution>.Web`, `<Solution>.UnitTests` — flat,
per-solution, one of each. Those are not the same architecture. Every repository this pack
creates is told, in its own charter, to follow a structure its own scaffold did not give it.

**MarketSense is what the promise looks like when kept:** eleven modules — Analytics, Auth,
Brokerage, Execution, Fundamentals, MachineLearning, Market, Notifications, Operations, Strategy,
Subscriptions — each with `.Api`, `.Application` and `.UnitTests`, alongside solution-level
`AppHost` and `ServiceDefaults`. Built by hand, against a scaffold that emits none of it.

### And the shared code has no route in

The same library exists in two places on 2026-09-19 — a public gist holding
`CoreLib.Essential.Helpers`, and `MarketSense/src/SharedKernel`:

| Defect | Gist | SharedKernel |
|---|---|---|
| Command and query pipelines run behaviours in opposite orders | present | fixed |
| `ActivitySource` constructed per log call, so no listener subscribes and **every trace tag is silently dropped** | present | fixed |
| Metadata dictionary uses `Add`, throwing on a duplicate key inside a diagnostics path | present | fixed |
| Concurrency limit sized from *currently available* thread-pool threads | present | **present** |
| `Expression.Invoke` in the specification combinators, which EF cannot translate | present | **present** |

Three defects were found and fixed once, in one copy; the fix never reached the other. Two remain
in both, and both fail quietly — the concurrency limit collapses to serial exactly when the
system is busy and reads as a hang; the combinators either throw at query time or evaluate
client-side and pull the whole table.

A scaffolded solution gets none of it, so every new project copies, rewrites, or does without.

## Affected users and systems

- **Every repository `init` scaffolds**, which gets a flat solution and a charter describing a
  modular one.
- **`init`'s interview**, whose module answer would finally be consumed.
- **The frozen emitted layout**, which names a `<Solution>.<Role>` pattern and four roles, and
  would become a mixed shape: solution-level infrastructure plus per-module domain projects.
- **`Directory.Build.props`'s assembly-naming rewrite**, which the shipped spec observed *never
  fires* because every emitted project already carries the solution prefix. Under bare
  module-scoped names it would fire — load-bearing where it is currently inert.
- **`coverage.runsettings`**, whose exclusions match by role suffix and would need to keep
  matching once a module prefix appears.
- **`spec-writer`**, which reads the pack for committed decisions and finds a layout claim the
  scaffold contradicts.
- **MarketSense**, which the owner has decided will adopt the published package.

## Proposed outcome

**The web kind scaffolds one worked module, and the shared code arrives as a package.**

`MofonCode.Core` is published to nuget.org from its own repository — the ID is free and nothing
on nuget.org begins with `Mofon`, so the prefix is clean. Public rather than private,
deliberately: a public restore needs no credential provider, no token on a developer machine, no
feed credentials in CI, and no `nuget.config` in a scaffolded repo — which matters because `init`
may not mutate a developer's machine and so could never set that up.

**One module, not N.** The interview's answer names it. Nobody knew MarketSense would have eleven
on day one, and a module list gathered in an opening interview is a guess frozen into a layout.
One module demonstrates the shape, is trivially renamed, and does not force an architecture
decision before anyone has written code. It holds one worked vertical slice — a request, a
handler, a validator and a test — small enough to delete and complete enough to copy.

**A module boundary that is checked, not hoped for.** The scaffold emits the architecture test
alongside it. MarketSense already proves this is mechanical: no domain reaching another outside
an allowed graph, no cycles, the shared kernel referencing nothing — and a guard asserting the
allowed graph does not describe edges that no longer exist. A layout commitment with no
instrument is what this framework calls an aspiration.

**The library's own repository is a dependency, not part of this chain.** Extracting the generic
third of `SharedKernel`, fixing the two surviving defects, and building the publish pipeline are
that repository's work.

## Success criteria

- **S1.** A scaffolded solution restores and builds, on a machine with no credentials configured
  and no `nuget.config` in the repository.
- **S2.** The emitted module's slice compiles and its test passes, under the same
  warnings-as-errors and analyzer gate as everything else.
- **S3.** The module the interview named is the module that is emitted. Falsifiable, and it is
  what distinguishes this change from the current state: answer the interview with a module name,
  and assert that name appears in the emitted project paths.
- **S4.** The emitted architecture test fails when a module boundary is violated. Falsifiable by
  injection: add a reference the graph forbids and watch the named test fail.
- **S5.** The version the pack pins is the one that binds. Falsifiable the way the SDK pin already
  was: substitute an impossible version and watch the restore fail naming it.
- **S6.** No copy of the library's source is emitted anywhere.
- **S7.** Emitted assembly names still match the coverage exclusions, which match by role suffix
  and have never seen a module prefix. This is the interaction the shipped spec flagged and
  nothing has exercised.
- **S8.** A gap-filling run over a repository scaffolded before this change either completes and
  leaves that repository building, or refuses and says why. **Neither is assumed.** The mechanism
  as it stands would emit the new module, skip `Directory.Packages.props` because it already
  exists, and leave a project referencing a centrally-unpinned package — NU1008, the exact
  failure that kept seven generator packages failing restore. This is the criterion most likely
  to change the design.

## Constraints

- **DDD lives in the pack, never in the engine. Settled by the owner, 2026-09-20.** A module is a
  subdomain, and that is `stack-dotnet`'s opinion to hold. The engine's charter says it knows only
  about process — tiers, gates, requirements, slice manifests, verification. A command-line tool
  has no subdomains, a prompt library has no bounded contexts, and an engine that speaks in them
  imposes an architecture on every repository regardless of what that repository is, which is the
  same failure as imposing a stack one level up.
- **The frozen layout must be extended before the spec is written, and this extension is larger
  than adding a role.** The shape changes from `<Solution>.<Role>` to solution-level
  infrastructure plus `<Module>.<Role>` domain projects. **The charter is not the document to
  edit** — it freezes the layout as a concept. The enumeration is in the approved, shipped
  `init-scaffolds-solution` spec at `spec.md:936`, which will otherwise be the only written
  description of the frozen roles and will be wrong. **This chain's spec states the extended
  layout, and a forward pointer is added to the old one**, because a shipped spec is a record of
  what shipped rather than a document to retrofit. The owner approves both before the spec is
  written, the way two charter amendments preceded the predecessor's spec.
- **The package must exist before this ships.** A pin that resolves to nothing fails at restore
  on every scaffold.
- **A published version is permanent.** nuget.org allows unlisting and not deletion, so the first
  published version exists forever and should be numbered to say it is early.
- **Central package management binds.** An emitted project references the package with no version
  attribute; the pin lives in `Directory.Packages.props`.
- **FluentValidation is a new commitment, not a codified practice.** Four files in MarketSense use
  `AbstractValidator`, out of roughly forty projects.

## Non-goals

- **Not adding a module after `init`.** Under a per-module layout, "add a subdomain" becomes a
  recurring operation and there is no command for it — `init` is the only scaffold entry point and
  it only fills gaps. That is the next intent, and naming it here stops this one growing to meet
  it.
- **Not N modules at scaffold time.** One, named by the interview.
- **Not the library's extraction or its defects.** Its repository's work, named as a dependency.
- **Not MarketSense's adoption.** Its own work, in its own repository.
- **Not the encapsulation rules.** Private handlers, internal `DbContext`, non-public EF
  configurations — a separate intent. Two of the three are already MarketSense's practice by 221
  to 7; the third is not, with all 52 configurations public.
- **Not the competition kind — the web kind only. Settled by the owner, 2026-09-19.** That kind
  emits a class library, a console runner and a test project: no host, no dependency-injection
  container, no EF, no database. Deferred rather than refused.
- **Not a private feed**, and **not shipping the library as source**. Both considered and
  rejected; the second because the executor never overwrites a file it did not create, so a fix
  made in the pack would reach zero existing repositories.

## Open questions

- **What is in the worked slice?** *Owner: peter.* It has to demonstrate the shape without
  inventing a domain — the same constraint that made the starter notebook headings-only.
- **Do the solution-level projects keep bare names?** *Owner: peter.* MarketSense uses
  `src/AppHost`, not `src/MarketSense.AppHost`, and relies on the assembly-naming rewrite to
  prefix them. Following it makes that rewrite load-bearing where the shipped spec observed it
  never fires today.
- **Exact pin or floating range?** *Owner: peter.* The pack pins everything else exactly; an exact
  pin means every library fix needs a pack release to propagate.
- **Where does the library repository live, and does it get its own chain?** *Owner: peter.* It
  would be the first use of this framework outside the repository that defines it.
