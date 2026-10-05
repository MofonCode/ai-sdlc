# Findings from the dice-roll-rev-7 run

As of 2026-10-05 · Peter Almanzar

## Scope and verdict

The plugin enforced everything it claims to enforce. Every gap below is in what the rules ask for, not in whether they were applied.

Reviewed: `C:\Repos\dice-roll-rev-7`, cycle `20261005_0424_24`, run from adoption (`/sdlc:init`) through `/sdlc:build`. Verify, remediate and land were never invoked, so this is an init-to-build run, not a full cycle.

I did not take the recorded evidence on trust. I rebuilt the solution (0 warnings), started the Aspire AppHost, and drove the page in Chromium:

- `POST http://localhost:5080/rolls` returned `{"succeeded":true,"data":{"face":4}}`; `/health` returned `Healthy`.
- Three clicks on the page produced face `2` and a history of `4, 2, 2` in roll order.
- `localStorage`, `sessionStorage` and cookies were all empty. No console errors.

So every acceptance criterion in the spec is genuinely met, and the application is not broken. Two things produced the impression that it was. The blank page was `:5080/` — the api root, which has no route and returns a bodiless 404; the page is `:5081`. And the page shows a numeral where the intent asked for a die. The first is a misread. The second is finding 3, and it is the one that matters.

## The decision map

Four of the eight need a call from you. All four are about how much a rule should bind, which is not a question the code answers. One of them — pasting `solution.md` — takes a minute and is not really a decision at all.

| # | Finding | Remedy | Yours to decide |
| --- | --- | --- | --- |
| 1 | A declared default can be silently skipped; no constraint covers omission | Widen `CON_13` to both directions, or bind the defaults that matter the way liveness is bound | **Yes** — which of the two, and for which rows |
| 2 | The plan's fresh reader is never given `techstack.md`, so `CON_13` is unjudgeable | Pass it as a fourth input on plan rounds | No — one edit to the interrogate skill |
| 3 | A criterion may narrow the outcome it traces to, and nothing catches it | Tighten `CON_04` to cover partial satisfaction within one outcome | **Yes** — global constraint edit; it will make specs harder to pass |
| 4 | Nothing asks a spec what a person sees when the action fails | A profile note, a `LOC_` constraint, or a global one | **Yes** — which tier it lives at |
| 5 | A cycle may edit the tests that police it; only a file-table row shows it | Flag a governing file in the evidence comparison | No — extends `DeclaredFiles`, which already splits declared from undeclared |
| 6 | `Noticed:` lines and `sdlc suggestions` are different stores | Have one read the other, or say in the skill that it does not | No — the command is currently wrong about its own coverage |
| 7 | Appearance has no home: `solution.md` is a placeholder, and the adoption interview has no greenfield branch | A greenfield branch in the interview; the design posture in `solution.md` and a `LOC_` | **Yes** — and paste `solution.md` either way |
| 8 | The kernel expects an OpenTelemetry host; none is registered, so its spans go nowhere | Part of finding 1: bind the row, or demote it | **Yes** — same call as finding 1 |

### What has been done since

Four of the no-decision items were implemented on 2026-10-05, in `development/iteration-4-foundation`.
Where this document says a thing *is* broken, read it as what was found; the remedies below record what
was done about it.

| Item | State |
| --- | --- |
| 5a — a governing file is flagged in the evidence comparison | done. `FileComparison` has a third list, `DeclaredFiles.Governs` decides it, and the evidence output names it whether or not the plan declared it |
| 6 — `sdlc suggestions` reads `Noticed:` lines | done. `IRepository.CommitMessages` reads the cycle branch, `Noticed.ReadAll` unwraps each line, and the listing shows them under their own heading. Run against this cycle it now lists all three, which it previously showed none of |
| 7 — a greenfield branch in the adoption interview | done. Three replacement questions, plus a table of where each kind of answer goes, applying R2 |
| 7 — ask once what *simple* bounds | done. A new step in the intent skill, before the artifact is written |

Still open and unchanged: findings 1, 2, 3, 4, 8 and 5b, plus R1, which gates three of them.

Findings 2, 5 and 6 are small, mechanical and low-risk. I would do them without asking. Findings 1, 3, 4 and 7 each change what every future cycle has to satisfy, and 1 and 3 change the library the binary ships, so they reach every adopter of the plugin — not just this repository. Finding 8 is finding 1 with the cost made concrete; it needs no separate decision.

## 1. A declared default can be skipped in silence

`CON_13` forbids a plan that **reaches for** a package, pattern or layer `techstack.md` does not declare without saying what the declared default would have cost. It says nothing about a plan that quietly **does not apply** one.

`techstack.md` declares `api description | OpenAPI, through Microsoft.AspNetCore.OpenApi` and `tracing, metrics and logs | OpenTelemetry, exported to Azure Monitor`, and its own note says "Observability is not conditional". Neither appears anywhere in the built solution. I grepped the source, the project files and the package props: no match for either.

The plan asserted the opposite in `Shape and departures`: *"Nothing in the shape itself departs from the stack `.sdlc/techstack.md` declares"*, then enumerated four concerns — web, boundary, orchestration, end-to-end testing — and generalised from them. Five interrogation rounds did not object, and were right not to: no rule was broken.

It starts earlier than the plan. `src/Sdlc.Core/Init/Scaffold/Program.cs.template` registers health checks and maps `/health`, and contains no `AddOpenApi`, no `MapOpenApi` and no OpenTelemetry registration. **Every solution adopting this profile begins out of conformance with its own declared stack on two rows.** The cycle did not introduce the gap; it inherited it and had no way to see it.

### Remedy

Two routes, and `techstack.md` already names the second one itself: *"To make a default bind, add a `LOC_` constraint or an architecture test."*

1. **Widen `CON_13` to both directions.** One clause: a plan that does not apply a declared default says why, in the same section. Costs a conformance paragraph in every plan, and the judge cannot check it without finding 2 being fixed first.
2. **Bind the rows that matter.** Liveness is the working precedent: the scaffold maps `/health` and `EndpointTests.cs.template` fails the build if the route or its registration goes. The same shape for OpenAPI is two lines in the template, one `PackageVersion` entry and one test.

My reading: do 2 for OpenAPI regardless — it is cheap, and the precedent is already written. Do 1 as well only if you want the general case closed, because the rows nobody binds will keep drifting.

### What needs you

OpenTelemetry is the one I will not choose for you. Binding it means every scaffolded solution carries instrumentation packages and an exporter configuration from its first commit, including a dice roller with no Azure subscription. The alternative is to demote the row from "not conditional" to conditional, which is an edit to what the profile claims rather than to what it does. Either is defensible; they say different things about what the profile is for.

## 2. The plan's judge is never shown the stack it is judging against

`../skills/interrogate/SKILL.md` is explicit: *"Give it exactly three things: the constraints in force, the artifact, and the artifact above it in the chain. Nothing else."* `techstack.md` is not one of the three. So `CON_13` — a rule entirely about conformance to `techstack.md` — cannot be decided by the only reader asked to decide it.

The reader said so itself. Round 1 on the plan, finding `F_01`:

> …nothing in the section says whether `techstack.md` declares a component-test package at all … a reader cannot tell from this document whether bUnit is the declared default, an undeclared addition, or a replacement for something the stack names. **Not raised as blocking because the document does not itself establish that `techstack.md` fails to declare bUnit** — if it is declared, there is nothing to say.

That is the mechanism naming its own blindfold, in the findings record, in round one. A departure gets caught only when the plan confesses to it — which means `CON_13` catches the honest plan and misses the careless one, the exact inversion of what a rule is for.

### Remedy

Add `techstack.md` as a fourth input, on plan rounds only. An intent and a spec never reach for a package, so they have no use for it and the prompt stays as it is.

The three-input rule is there to strip investment bias: no conversation, no drafts, no authorial reasoning. `techstack.md` is none of those. It is a rule source, the same category as the output of `sdlc constraints`, which the reader is already handed. Adding it removes nothing the rule was protecting.

### Why this needs no decision

It is one paragraph in one skill file, it closes a rule that is currently undecidable, and it has no blast radius beyond plan rounds. It should also land **before** finding 1: widening `CON_13` to cover omission while the judge still cannot see `techstack.md` would add a rule nobody can enforce.

## 3. A criterion may narrow the outcome it traces to

This is the important one. The chain preserved traceability and lost fidelity, and every gate stayed green while it happened.

Follow the word *die* down the documents:

| Document | What it says | What was lost |
| --- | --- | --- |
| `intent.md` | "see a **die** on the screen showing a number between 1 and 6" | — |
| `spec.md`, criterion 1 | "displays a **face value** that is an integer from 1 to 6" | the die as a thing you look at |
| `plan.md` | MudBlazor declared and not used; "the roll control, the face it shows" | the means of drawing one |
| `RollPage.razor` | `<p class="face">@face</p>` | everything but the number |

Each step is individually defensible. The spec's criterion does trace to the outcome. The MudBlazor departure is argued honestly and at length — bundle weight, an architecture package rule, a one-consumer Razor library. The page does what the plan says. The cumulative effect is that the person who asked to see a die got a numeral in a `<p>`, and asked whether the application was working.

### Why nothing caught it

- **`CON_04`** forbids *"a spec whose criteria satisfy some of the intent's stated outcomes while leaving others with no criterion at all."* Every outcome here has a criterion. Partial satisfaction *within* one outcome is outside the words.
- **`CON_12` is narrower than it first looks, and nothing here was in its way.** It forbids a criterion whose only check is a person observing something *the suite cannot reach* — a device, an app store, hardware. A page in a browser is reachable: *"displays a die face showing that many pips"* is a perfectly good Playwright assertion. The narrowing was catchable and simply nobody's job.
- **`verify`** is forbidden from reading source or running the app. No stage ever asks whether this looks like a die.

### Remedy

Tighten `CON_04` so it covers narrowing as well as omission — roughly: *a criterion that satisfies only part of the outcome it traces to, without the remainder appearing as another criterion or in `Out of scope`*.

The reason to put it there rather than anywhere else is that **the fresh reader already holds the intent when it judges the spec.** The upstream artifact is one of its three inputs. So unlike finding 2, this rule is decidable today, with no change to what the reader is given. The spec skill should pair with it: have each criterion quote the words of the outcome it derives from, so a narrowing shows up as a visible difference rather than a paraphrase nobody compares.

The cheaper half, worth doing either way: have `sdlc draft approval` list the intent outcomes with no machine-checkable criterion, under a plain heading saying these are yours to look at. That turns the `CON_12` escape hatch into an artifact instead of a silence.

### What needs you

This edits the shipped library, so it reaches every adopter, and it will make specs harder to pass — deliberately. A reader given this rule would have blocked criterion 1 of this cycle, and the right outcome would have been a spec that said either "rendered as a die face" or "a numeral is acceptable". Whether that is worth the extra rounds on every future spec is your call, not mine.

## 4. Nothing asks a spec what a person sees when it fails

`RollPage.razor` rolls like this:

```csharp
using HttpResponseMessage response = await Http.PostAsync("rolls", content: null);
response.EnsureSuccessStatusCode();
```

No `catch`, no message, no state the page can render. If the api is down, the CORS origin is wrong, or the request times out, `EnsureSuccessStatusCode` throws, `_rolling` resets in the `finally`, and **the button does nothing at all** — no error, no spinner, no indication the click was received. That is indistinguishable from a broken application, and it is the likeliest real-world version of the complaint that started this review.

The build is not at fault. No criterion asked for it, so writing one in would have been creep inside a declared file, which `CON_07.2` forbids. The spec is not at fault either: it covers five criteria, all of them happy paths, and no rule requires otherwise. The gap is that **the rule set has nothing to say about unhappy paths at all.**

That matters more than one dice roller. The same silence applies to every form, every save, every submit any solution built on this profile will ever ship.

### Remedy

Three tiers, in order of how hard they bind:

1. **A note in `techstack.md`** — declarative, non-binding, caught only if finding 2 lands and the judge can read it. Cheapest, weakest.
2. **A `LOC_` constraint**, per solution, for solutions with a user interface. This is the tier the framework was built for, and `sdlc draft constraint` already exists to write one. But it is a person-written file, so it binds only where somebody remembers to add it.
3. **A global constraint** — a spec with a criterion for a user-initiated action carries one for what the person sees when it fails. Binds everywhere, and makes every spec longer.

### What needs you

Which tier. My reading is 2, drafted now for this repository and carried into the profile later if it earns its place — it is reversible, it tests the wording on real cycles before it reaches anyone else, and a `LOC_` is exactly what the `techstack.md` preamble says this mechanism is for.

Separately and needing nothing from you: the dice roller should get a visible failure path, as its own cycle. I have not touched it.

## 5. The architecture tests chose the design, and the cycle edited them

Two separable things sit here. One is small and obvious. The other is the deepest question in this document.

### 5a. A cycle may amend the tests that police it, and only a file-table row shows it

`StructureTests.cs` arrives from the scaffold and decides which project may reference which. This cycle added an `AppHost` role to both of its rule tables, because a project under `src/` named in neither table fails the suite on its own. That is legitimate, it is declared in the plan's file table, and the build did it openly.

But it reaches a reviewer as one row among thirty-nine, reading `new by sdlc scaffold; then edited to add the AppHost role`. A cycle changing the rules that judge it should be louder than a cycle changing a page.

**Remedy, no decision needed:** `src/Sdlc.Core/Evidence/DeclaredFiles.cs` already splits what a change touched into declared, undelivered and undeclared. Add a fourth category — governing files touched — over a known set: the architecture test project, `Directory.Build.props`, `BannedSymbols.txt`, the hook configuration. It reuses the comparison that exists and changes no rule.

### 5b. A plan may depart from `techstack.md`. It may not depart from an architecture test

The scaffolded direction rule lets a `Web` project reference `Contracts` and nothing else. Read what that single rule produced for a d6:

- the draw cannot happen in the page, so it moves behind an api route
- that is two processes, so an Aspire AppHost appears
- a standalone WebAssembly app is a second origin, so a CORS policy appears
- a browser app cannot read an environment, so the ports are pinned as constants in two files
- the page cannot reference the api's envelope, so it **re-declares the envelope record** inside itself
- the suite cannot run while the app is running, because both want port 5080

The plan considered a single-process alternative, which would have hosted the components inside the api, and rejected it — citing the rule. Roughly 3,100 lines across nine projects, for one button.

Meanwhile the intent's third fixed decision reads: *"Keep it very simple — nothing fancy. Peter, 2026-10-05: stated as a bound on the whole of this work rather than a preference about any one part of it."* No constraint reads that bound, no stage may act on it, and no artifact anywhere in the cycle is the place where a shape gets called disproportionate to its problem.

So the asymmetry is this: `CON_13` lets a plan depart from a declared default by saying what it costs. An architecture test has no such door. You satisfy it, or you edit it — and editing it is what this cycle did, since that was the only move available.

### What needs you

Whether a plan may depart from an architecture rule the way it departs from `techstack.md` — stating what the rule would have cost here, recording it, and narrowing the rule for that cycle rather than amending it permanently.

I can argue it both ways, which is why it is yours. **For:** the same reasoning the framework already accepts about `techstack.md` — a default that cannot be departed from is a cage, and the first genuinely unsuitable case gets the whole file ignored. **Against:** the architecture suite is the only *mechanical* enforcement of shape in the entire framework. Everything else is a document judged by a reader. Put a reasoned-departure door in it and the one thing that cannot be talked around becomes something that can.

There is a narrower version worth considering first: keep the rules inviolable, but have the plan stage flag when the declared shape is heavy relative to the intent — so the disproportion becomes a thing a person is shown at the gate, rather than a thing nobody is asked about.

## 6. The cycle caught two real things itself — and then lost them

The build noticed three things it was not allowed to fix and wrote each as a `Noticed:` line in the commit, exactly as `../skills/build/SKILL.md` instructs:

1. `ApiResponse<T>` lives in the api, which the direction rule stops any front end referencing, so the page re-declares the envelope fields it reads. Moving it to `Contracts` would remove the duplicate.
2. The page's api address is a compile-time constant matched to the port the AppHost pins. Works locally, will not survive a deployment.
3. The scaffold says real work replaces its reference slice; the approved plan declared the `Greet` files untouched, so the slice is still there for a later cycle to remove.

A fourth was found in conversation and never written down at all: the pinned ports mean **the end-to-end suite cannot run while the application is running**, which is what you hit. It is a true consequence of the design and it exists nowhere in the repository.

This is the mechanism working — the build left all of them alone rather than slipping fixes into approved files. Both halves of that are right.

### The gap: two stores, and the command reads the wrong one

`../skills/suggestions/SKILL.md` says `sdlc suggestions` lists "the open non-blocking findings a solution has accumulated", and that *"the data is in the findings rounds, which is what this reads"*. The findings rounds are the interrogation JSON. **`Noticed:` lines are in commit messages.** They are never read by the command that answers "what have we noticed and not acted on".

So the build's own observations survive only in git history. Ask the framework what this solution has outstanding and you get the interrogation leftovers — here, one open spec suggestion about the Chromium-and-WebKit matrix — and not one of the four above.

### Remedy, no decision needed

Either have `sdlc suggestions` read `Noticed:` lines out of the commits on the cycle's branch and list them beside the findings, or stop the skill claiming coverage it does not have. The first is better and is not large: the lines have a fixed prefix and the cycle already knows its base commit, which is recorded in `evidence/base.sha`.

I would not change where the build writes them. The commit is the right home — it is read by whoever reviews the pull request. The defect is only that nothing else looks there.

## 7. Appearance was never anybody's job

None of the six fixes above would put a design on that page. Worth saying plainly, because it is the thing you actually noticed.

Finding 3 would get you a die shape, because the intent said the word *die*, and a die with pips is machine-checkable. Nothing anywhere would get you a **styled** page, because nothing anywhere asked for one — and aesthetic judgement is the one thing `CON_12` genuinely does keep out of a spec, since no test can make it.

### Two meanings of simple

You said *"nothing fancy. very simple"*. The interview recorded it faithfully, as a bound on the whole of the work. The plan then applied it to **appearance**: *"The whole surface is one control and a list of numbers, so the page uses plain markup."*

You almost certainly meant **scope** — one die, no multiplayer, no persistence — not unstyled HTML. Nobody misread anything; the word carries both meanings and nothing in the interview separates them. That is a one-question fix in the intent skill: when somebody bounds the work with *simple*, *basic* or *nothing fancy*, ask once whether they mean small in scope or plain in appearance, and record which.

### But appearance does not belong in an intent at all

If you write "use MudBlazor, make it look like a product" into this intent, you have to write it into the next one, and the one after that. Anything that must be remembered every cycle will eventually not be.

It is a property of the **solution**, not of this piece of work, and the framework already has the right home for it: `.sdlc/solution.md`, read by every future plan.

**In this repository, that file is still the placeholder.** It reads *"Replace this with a paragraph a newcomer could read"*. The adoption skill did its half — it drafted the paragraph and handed it to you to paste, because the file is protected and no agent may write it. It was never pasted.

### Why the interview found nothing

The adoption interview asks four questions. Three of them are about history:

1. What is this solution for?
2. **What has gone wrong here before that you would not want to happen again?**
3. **What is there a right way to do here that a newcomer would get wrong?**
4. **Is there anything that must never change?**

On a greenfield repository the honest answers are *nothing*, *nothing*, *nothing* — which is what you gave. The skill concluded, defensibly, that no `LOC_` constraint survived its admission tests. **So a brand-new solution gets zero solution constraints by construction**, and the only question that could have captured a design posture is not among the four.

### Remedy

Add a greenfield branch to the adoption interview — two or three questions that have answers when there is no history: what should every screen in this solution look and behave like; what does "done to a good standard" mean here; what will somebody judge this on that no test will check. Those answers go into `solution.md` and, where they are firm, into a `LOC_` constraint.

### What needs you

Two things, and the first takes a minute: paste `solution.md`, with a sentence about the standard you expect, not only what the solution does.

The second is whether MudBlazor is this solution's default or merely available. If it is the default, that is an architecture-test question rather than a constraint — see R2 below — and it is worth knowing it currently collides with the scaffolded package rule, which admits only `Microsoft.AspNetCore.Components.WebAssembly*` in a `Web` project. The plan named that collision as one of its two reasons for dropping MudBlazor.

## 8. The kernel is instrumented. Nothing collects it

You asked whether `MofonCode.Core` was used and whether tracing works. Yes to the first, no to the second, and the gap is sharper than "OpenTelemetry is missing".

**The kernel is used properly.** `Mediator`, `IRequest`/`IRequestHandler`, `IRequestBehavior` through the validation behaviour, `FluentResults`, `ToApiResult`, and the `ResultEnvelope` the boundary returns. The roll operation follows the reference shape exactly.

**The kernel also ships a tracing layer that this solution never switches on.** Its public surface includes `TraceResult<T>` and `TraceResultOptions`, carrying an `ActivitySourceName` and a `MeterName`, plus `TracerFlushRegistry`, whose own documentation says:

> Host startup registers the TracerProvider's `ForceFlush` callback … TraceResult invokes it via `Flush` on terminal outcomes.

That is a kernel written expecting an OpenTelemetry host. This solution registers no `TracerProvider`, so the hook is never registered and the flush never fires. `TraceResult` appears nowhere — not in the solution, and not in any scaffold template either.

**The trace id you can see leads nowhere.** The `traceId` in a roll response — `0HNP2LE42B49Q:00000001` — is ASP.NET's `HttpContext.TraceIdentifier`: a connection id and a request counter. It is a correlation token with nothing to correlate against. No exporter, no collector, and the only sink is the Aspire console while the dashboard happens to be open.

`techstack.md` called this exact outcome in advance:

> Every boundary response already carries a trace id … That is not tracing: nothing collects the spans and nothing exports them, so there is nothing to ask.

So the profile documented the failure, the kernel was built to avoid it, and the solution landed in it anyway — because nothing bound the row. It is finding 1 with the cost made concrete: this is not a missing nicety, it is a diagnostic surface the kernel already pays for and nobody can read.

## What held, and must survive the remedies

None of the six findings is a mechanism that failed. Everything the framework claims to enforce, it enforced:

- **The gate held the build behind a person.** The approval carries three hashes, placed by a rename you ran, and `sdlc gate` refused to be worked around.
- **Every findings round recorded its upstream hash**, which is what makes a stale judgement detectable rather than merely unlikely.
- **The stop hook caught the tree moving.** Your `aspire run` dropped `aspire.config.json` into the root; the hook noticed the recorded diff no longer described what was on disk and forced a rebuild and a re-record. Nobody asked it to.
- **`/sdlc:verify` invoked before the build correctly refused.** You ran it a minute early, at 06:03, and it declined rather than inventing a verdict.
- **The fresh-reader rounds did real work.** Round 3 found that the end-to-end ordering check is a coin flip when two of three faces repeat — about one run in six — and the plan's final text now says so and rests the ordering guarantee on the bUnit half instead. Round 4 found that the browser-storage proof never said what distinguishes a page-written key from a framework one. Those are findings a good reviewer would be pleased to have made, and both visibly changed the document.

### One thing to watch rather than fix

Seven rounds across three artifacts produced **zero blocking findings**. Every one was a suggestion. That may be correct — the documents are unusually careful. But it means the refusal path, the one that cannot be waived, was never exercised in this run, and a judge that never blocks looks identical to a judge that cannot. Worth watching across the next few cycles rather than acting on now.

One consequence is visible already: `spec.r1` finding `F_01`, about the Chromium-and-WebKit matrix being a support obligation the spec added on its own authority, is still `open`. There was no round 2 on the spec, so it was carried into the approval as an open suggestion — which is by design, and which is also the only record anywhere that it was ever considered.

## Reconcile these four before acting

The remedies are not independent. Four of them collide — with each other, with the framework's own admission rules, or with something I got wrong above.

### R1. The profile contradicts itself on MudBlazor, and finding 1 would make the contradiction binding

Three documents written by the same profile disagree:

| Source | What it says about UI |
| --- | --- |
| `techstack.md` | `ui components \| MudBlazor`, and shared UI lives in a Razor class library |
| `StructureTests.cs.template`, package rule | a `Web` project may take `Microsoft.AspNetCore.Components.WebAssembly*` and nothing else |
| `StructureTests.cs.template`, direction rule | `["Web"] = ["Contracts"]`; **no role exists for a Razor class library at all** |

The template knows it is ahead of itself — its own comment reads *"`Web` is listed and is not written by the skeleton"*, and the package rule is a prefix match because *"listing them exactly would fail on the day the first real Web project arrives"*.

So the first solution that follows the declared stack properly — MudBlazor in a Razor class library, consumed by a Blazor host — **must amend the architecture test for its projects to be admissible at all.** That is not finding 5a happening by accident. It is structural, and it is waiting for every solution that comes after this one.

**Order:** settle this before finding 2 and before finding 1. Hand the judge a `techstack.md` that contradicts the test suite and every plan from then on earns a finding about a conflict no cycle can resolve. Bind the declared rows first and you have bound one that cannot be satisfied.

### R2. "Use MudBlazor" cannot be a `LOC_` constraint — the admission tests reject it

`sdlc draft constraint` admits a candidate only if all three hold, and the second is the one that bites:

1. its `Forbids` clause names something that becomes illegal
2. **no analyzer, schema or hook could decide it**
3. it is incompatible with at least one plausible implementation

An architecture test can decide "this project references MudBlazor" — one already decides the opposite. So the design posture splits in two, and my finding 7 remedy was half wrong:

- **Which component library, and where it may be referenced** → the package rule in the architecture test. Not a constraint.
- **Whether a page's visual treatment was considered against the solution's standard** → needs reading comprehension, so it passes test 2 and can be a `LOC_`.

Finding 4's failure-path rule passes all three cleanly: no analyzer can read a spec and decide whether it covers an unhappy path. That route is sound as written.

### R3. I overstated `CON_12`, and the correction makes finding 3 cheaper

I said `CON_12` pushes appearance out of a spec. It does not. It forbids a criterion whose only check is a person observing something **the suite cannot reach** — a device, an app store, hardware.

A page in a browser is reachable. *"Activating the roll control displays a die face showing that many pips"* is an ordinary Playwright assertion, and would have been admissible as criterion 1 with no rule in the way.

Two consequences. Finding 3's remedy no longer depends on the approval-draft half — that is now a nice-to-have for genuinely unreachable outcomes, not load-bearing. And there is no `CON_04`/`CON_12` deadlock to design around: a narrowed criterion can almost always be un-narrowed into something testable. What stays outside any spec is aesthetic judgement — *looks professional* — which no test can make and which therefore remains yours, permanently, by design rather than by omission.

### R4. Your two notes have different causes, and neither fix reaches the other

| What you noticed | Root cause | Fixed by | Still true afterwards |
| --- | --- | --- | --- |
| No Swagger | The profile declares it, the scaffold omits it, nothing binds it, the judge cannot see it | 1, 2, 8 | nothing — this one closes properly |
| No design | Appearance has no home: `solution.md` is a placeholder, the adoption interview has no greenfield branch | 7, plus R1 and R2 above | **verification still never checks it** |

That last cell is the one to be clear-eyed about. `solution.md` is read by **plans**, never by `verify`. Putting a design standard there changes what gets built and argued for; it does not add a gate. After every remedy in this document, "does this look like a product" is still a person opening the page — and the honest framework answer is that it should be, because the alternative is a test asserting taste.

### The order that actually matters

R1, then finding 2, then findings 1 and 8, then finding 3. Everything else is independent and can go in any order.

## Suggested order

Smallest blast radius first, and the dependency matters: finding 2 has to land before finding 1, or you add a rule the judge still cannot enforce.

| Step | Work | Touches | Waiting on |
| --- | --- | --- | --- |
| 1 | Resolve the MudBlazor contradiction — which of the three sources is wrong (R1) | `techstack.md` or `StructureTests.cs.template` | **your call, and it gates 2–4** |
| 2 | Give the plan's judge `techstack.md` (2) | `../skills/interrogate/SKILL.md` | step 1 |
| 3 | OpenAPI in the scaffold template plus the test that binds it (1) | `Program.cs.template`, `EndpointTests.cs.template`, `Directory.Packages.props` | step 1 |
| 4 | OpenTelemetry: bind it, or demote the row (1, 8) | profile template or `techstack.md` | **your call**, after step 1 |
| 5 | Paste `solution.md` with the standard you expect (7) | `.sdlc/solution.md` | **you, a minute** |
| 6 | Flag governing files in the evidence comparison (5a) | `src/Sdlc.Core/Evidence/DeclaredFiles.cs` | **done** |
| 7 | `sdlc suggestions` reads `Noticed:` lines (6) | the suggestions command and its skill | **done** |
| 8 | A greenfield branch in the adoption interview (7) | `../skills/init/SKILL.md` | **done** |
| 9 | Ask once what *simple* bounds — scope or appearance (7) | `../skills/intent/SKILL.md` | **done** |
| 10 | Tighten `CON_04` against narrowing (3) | `CONSTRAINTS.md`, shipped library, spec skill | **your call** |
| 11 | A failure-path `LOC_`, worded to demand an observable (4, R2) | `.sdlc/constraints.md` here, or the global library | **your call** |
| 12 | Departure from an architecture rule, or the narrower flag (5b) | the plan skill, possibly `CON_13` | **your call**, and worth sitting on |

Steps 6 to 9 are done — each was self-contained and none changed what a cycle has to satisfy. Steps 2 to 4 are equally mechanical but sit behind step 1, which is a judgement only you can make. Step 5 is yours and unblocks nothing, but every plan from here reads that file.

Step 12 is the one I would deliberately leave alone for a while. It is the only finding here that could weaken something rather than tighten it, and the case for it rests on one cycle — a d6 is a small enough problem that any profile would look heavy against it. Two or three more real cycles will say whether the shape is genuinely disproportionate or whether this was simply the worst case to measure it with.

### On the dice roller itself

It works and it is verifiable as it stands. Three things belong to it rather than to the framework, each its own cycle: a visible failure path, a die that looks like a die, and ports that let the suite and the application run at the same time. None of them is urgent, and none should be slipped into the cycle that is already approved and evidenced.
