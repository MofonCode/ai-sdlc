---
slug: scaffold-emits-an-application-layer
title: A scaffolded solution has somewhere to put domain code, and one worked example in it
status: draft
tier: feature
created: 2026-09-20
owner: peter
---

> **BLOCKED on a package that does not exist, and split on 2026-09-20.**
>
> **Narrowed the same day.** `MofonCode/MofonCode.Core` now exists as an empty public repository,
> so "no repository" is no longer the blocker. What is still missing is the package: the library
> has no chain, no content and nothing published, and `Directory.Packages.props` cannot pin a
> version that resolves to nothing.
>
> The dependency is still larger than a publish pipeline. `MarketSense/src/SharedKernel` is 40
> files and most of it is that product's domain — trading clock, supported intervals,
> entitlements, batch ledger, chart ranges. Choosing the generic third is the real work and it is
> unestimated. An earlier version of this draft said "the shared code" where the parked one said
> "the generic third", which hid that.
>
> **What the sunsetting changed.** MarketSense was sunsetted on 2026-09-20. The extraction was
> partly feared because it meant two copies drifting — three fixes had already reached one copy
> and not the other. With nothing live to stay in step with, this becomes a one-way lift rather
> than a split, and the files left behind are simply left behind. Smaller, and a different shape.
> The cost that does not go away: the two live defects come with whatever is carried across, and
> no maintained codebase will fix them afterwards.
>
> **And this chain cannot be verified without it.** The worked slice must use the mediator or it
> demonstrates nothing, so the `Application` project does not compile until the package exists.
> A claim that this change is small measured smallness against the engine while the thing gating
> delivery was external.
>
> **Two things from this draft were split out because they are neither blocked nor coupled** and
> are now `pack-promises-what-it-scaffolds`: the pack promising a layout it does not emit, and
> `init` refusing to proceed without an answer to a question this very design decided should
> never be consumed. The second is a defect this draft introduced — it moved the module question
> from accidentally decorative to knowingly decorative and mentioned it nowhere.
>
> **Also unresolved here:** the replacement charter line — *"boundaries kept clean, split when the
> seams are known"* — is an aspiration rather than a description, which is the same defect as the
> line it replaces. The split chain addresses that; this one inherits whatever it decides.


# intent — a scaffolded solution starts with an architecture

## Problem

**The pack promises an architecture in writing and scaffolds a solution with nowhere to put
domain code.**

### The promise

`stack-dotnet/SKILL.md:357` instructs `init` to write this into every scaffolded repository's
charter, as a technology commitment:

```
- Module layout: vertical slices, each {Module}.Api / .Application / .UnitTests
```

### What is actually emitted

`scaffold.json`'s web kind emits `src/<Solution>.AppHost`, `src/<Solution>.ServiceDefaults`,
`src/<Solution>.Web`, `src/<Solution>.Api`, `tests/<Solution>.UnitTests` and
`tests/<Solution>.IntegrationTests`. **There is no `.Application` project, and nothing is
per-module.**

So every repository this pack creates is told, in its own charter, to follow a structure its own
scaffold did not give it. A vertical slice — a request, a handler, a validator — has no project
to live in. Putting it in the Api project makes the worked example contradict the architecture
it is supposed to demonstrate.

### And the shared code it would use has no route in

The mediator, results and tracing that a slice is built from exist in two places: a public gist
holding `CoreLib.Essential.Helpers`, and `MarketSense/src/SharedKernel`. Comparing them on
2026-09-19:

| Defect | Gist | SharedKernel |
|---|---|---|
| Command and query pipelines run behaviours in opposite orders | present | fixed |
| `ActivitySource` constructed per log call, so no listener subscribes and **every trace tag is silently dropped** | present | fixed |
| Metadata dictionary uses `Add`, throwing on a duplicate key inside a diagnostics path | present | fixed |
| Concurrency limit sized from *currently available* thread-pool threads | present | **present** |
| `Expression.Invoke` in the specification combinators, which EF cannot translate | present | **present** |

Three were found and fixed once, in one copy; the fix never reached the other. Two remain in
both, and both fail quietly — the concurrency limit collapses to serial exactly when the system
is busy and reads as a hang; the combinators either throw at query time or evaluate client-side
and pull the whole table.

A scaffolded solution gets none of it, so every new project copies, rewrites, or does without —
and copying is how the fix that reached one copy failed to reach the other.

## Affected users and systems

- **Every repository `init` scaffolds**, which gets a charter describing a structure it does not
  have.
- **`stack-dotnet`'s skill**, whose commitment line is the thing that is untrue.
- **The frozen emitted layout**, which enumerates four role suffixes and would gain a fifth.
- **`Directory.Packages.props`**, where a pinned version would live beside every other pin.
- **`spec-writer`**, which reads the pack for committed decisions and finds one the scaffold
  contradicts.
- **MarketSense**, which the owner has decided will adopt the published package.

## Proposed outcome

**One `Application` project, one worked slice in it, and the shared code as a package.**

`src/<Solution>.Application` is emitted alongside the existing projects — one new role, not a
change of shape. It holds a worked vertical slice: a request, a handler, a validator and a test,
small enough to delete and complete enough to copy.

`MofonCode.Core` is published to nuget.org from its own repository and referenced, rather than
copied in as source. The ID is free and nothing on nuget.org begins with `Mofon`, so the prefix
is clean. Public rather than a private feed, deliberately: a public restore needs no credential
provider, no token on a developer machine, no feed credentials in CI, and no `nuget.config` in a
scaffolded repo — which matters because `init` may not mutate a developer's machine and so could
never set that up. Shipping the library as scaffolded source was drafted and rejected: the
executor never overwrites a file it did not create, so a fix made in the pack would reach zero
existing repositories, and two hand-maintained copies would become one per repo.

**And the pack's promise is corrected to match.** The charter line it writes becomes a modular
monolith: one solution, boundaries kept clean inside it, split into modules when the seams are
known rather than guessed. That is the honest description of what is scaffolded, and it is the
better architecture — MarketSense's eleven modules were discovered over years, and a scaffold
that asks for module boundaries in an opening interview asks the owner to guess and then freezes
the guess into a directory structure.

## Success criteria

- **S1.** A scaffolded solution restores and builds on a machine with no credentials configured
  and no `nuget.config` in the repository.
- **S2.** The emitted slice compiles and its test passes under the same warnings-as-errors and
  analyzer gate as everything else.
- **S3.** The version the pack pins is the one that binds. Falsifiable the way the SDK pin
  already was: substitute an impossible version and watch the restore fail naming it.
- **S4.** No copy of the library's source is emitted anywhere. This is what distinguishes the
  chosen design from the one it replaced.
- **S5.** The emitted slice survives the scaffold's own sample-content deletion. The scaffold
  deletes generator samples and already ships starter tests for exactly this reason; a starter
  slice the delete step removes leaves the solution demonstrating nothing.
- **S6.** Emitted assembly names still match the coverage exclusions, which match by role suffix.
  A fifth role has never been through them.
- **S7.** The charter line the pack writes describes what the scaffold emits. Falsifiable by
  reading one against the other, and it is the defect this intent opens with.
- **S8.** A gap-filling run over a repository scaffolded before this change either completes and
  leaves that repository building, or refuses and says why. **Neither is assumed.** The mechanism
  as it stands would emit the new project and skip `Directory.Packages.props` because it already
  exists, leaving a project referencing a centrally-unpinned package — NU1008, the exact failure
  that kept seven generator packages failing restore.

## Constraints

- **The frozen role list must be extended before the spec is written.** The charter freezes the
  emitted layout as a concept; the enumeration — `AppHost`, `ServiceDefaults`, `UnitTests`,
  `IntegrationTests` — is in the approved, shipped `init-scaffolds-solution` spec at
  `spec.md:936`. `Application` is a fifth. The charter calls changing a frozen thing *"a
  migration, not an edit"*, so this chain's spec states the extended list and a forward pointer
  is added to the old one, because a shipped spec records what shipped rather than being a
  document to retrofit. The owner approves before the spec is written, the way two charter
  amendments preceded the predecessor's.
- **The package must exist before this ships.** A pin resolving to nothing fails at restore on
  every scaffold.
- **A published version is permanent.** nuget.org allows unlisting and not deletion, so the first
  version exists forever and should be numbered to say it is early.
- **Central package management binds.** The emitted project references the package with no
  version attribute; the pin lives in `Directory.Packages.props`.
- **FluentValidation is a new commitment, not a codified practice.** Four files in MarketSense
  use `AbstractValidator`, out of roughly forty projects.
- **No second placeholder is needed, and that is why this is small.** A per-module layout would
  have required the engine to stop closing the token set in five places — the executor, `init`'s
  invocation, the report schema, recovery, and the derivation transform. Those intents are parked
  with their evidence; nothing here needs them.

## Non-goals

- **Not per-module projects.** Parked with the reasoning: boundaries gathered in an opening
  interview are a guess frozen into a layout.
- **Not a skill that splits a solution into modules.** That is the deferred half of this decision
  and it is substantial work — moving projects, rewriting references, updating the solution file,
  keeping tests green. It also does what the scaffold promises never to do, *"restructures or
  renames anything in an existing repo"*, so when it is built it must say out loud that it is a
  different, explicitly-invoked act rather than quietly contradicting its neighbour.
- **Not the library's extraction, its two live defects, or its publish pipeline.** Its own
  repository's work, named as a dependency because this chain is worthless without it.
- **Not MarketSense's adoption.** Its own work, in its own repository.
- **Not the encapsulation rules.** Private handlers, internal `DbContext`, non-public EF
  configurations — a separate intent. **Corrected 2026-09-20, re-measured against
  `legacy/v02`:** handlers are 217 private and 4 internal of 221, none public; all 12
  `DbContext` classes are internal, none public. The third rule as written here was wrong. It
  said *"all 52 configurations are public"*; they are **declared** public but every one is
  nested inside an internal or private holder inside an internal context, so none of the 59 is
  reachable outside its assembly — 35 effectively private, 24 effectively internal, zero public.
  What is inconsistent is a `public` keyword the compiler overrides, and a holder spelled
  `internal` in five modules and `private` in two. A smaller problem than this bullet claimed.
- **Not the competition kind — the web kind only.** That kind emits a class library, a console
  runner and a test project: no host, no dependency-injection container, no EF, no database.
  Deferred rather than refused.
- **Not module-boundary enforcement.** With one Application project there are no edges to check,
  so MarketSense's dependency-graph tests have no subject here. Layer boundaries inside the
  solution remain checkable and are the part worth having early.

## Open questions

- **What is in the worked slice?** *Owner: peter.* It has to demonstrate the shape without
  inventing a domain — the same constraint that made the starter notebook headings-only.
- **Does the Application project get a test project of its own, or share the existing one?**
  *Owner: peter.* The frozen list already has `UnitTests`; a sixth role would be a second
  extension for one change.
- **Exact pin or floating range?** *Owner: peter.* The pack pins everything else exactly; an
  exact pin means every library fix needs a pack release to propagate.
- **Where does the library repository live, and does it get its own chain?** *Owner: peter.* It
  would be the first use of this framework outside the repository that defines it.
