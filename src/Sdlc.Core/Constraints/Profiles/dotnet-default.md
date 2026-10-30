# `dotnet-default` constraints

The stack rules this profile ships. A solution is judged against the ones its declared stack marks
`true`, on top of the global set. They resolve between the global `CON_` library and the solution's own
local rules, so a `LOC_` rule is still the last word.

**These may name a language, framework or package, and that is what the tier is for.** The global
library forbids itself from naming one — *"Stack neutrality. These bind solutions on any stack, so none
of them may name a language, framework or package. Stack specifics live in the profile and in
analyzers."* A rule that needs a name has nowhere else to go: an analyzer decides a question about
code, and these are questions about a plan.

**Ids are a monotonic sequence of their own, shared by every profile's document.** `PRO_01` is
unrelated to `CON_01`, and no other profile may reuse it: a solution switches a rule on by naming its id
in its declared stack, so one id must mean one rule. Assigned once, never reused; a gap is the record of
a removed rule. A bare id is revision 1.

**Four parts, all required**, as in the global library: the heading states the rule, `Applies to` says
which artifact and at what moment, `Forbids` names what becomes illegal, and `Why` says what goes wrong
without it. The suite fails on a rule missing any of them.

**Three admission tests, all of which must pass.** Its `Forbids` clause names something that becomes
illegal; no analyzer, schema or hook could decide it; and it is incompatible with at least one
plausible implementation. The second test is what keeps this list short — a rule about code belongs in
an analyzer, and a mechanical rule about an artifact belongs in a schema or a hook. Five rules
survived it, which is the honest number rather than a disappointing one.

**This profile's declared defaults are not here.** They are rows in the solution's declared stack,
which a person owns, and departing from one is `CON_15`'s business rather than a rule here. The rules
below are rows in that same file, in its second table. What the first three
govern is the case where a plan satisfies every declared default and still proves nothing.

**`PRO_04` is the one default that became a rule, and it is here because a departure was argued well
and still cost the wrong people.** The orchestration row was a default for as long as the profile
existed, and the first solution to meet its condition departed from it with a reason `CON_15`
accepted. The cost of that departure falls on everyone who runs the solution afterwards, not on the
cycle that argued it, so an argument at the gate is the wrong mechanism for it.

**`PRO_05` asks for an argument rather than forbidding the thing.** Some needs only a script can meet,
so a rule against JavaScript on a Blazor page would be wrong. What went missing in the plan it came
from was the question of what the framework already did, and the rule asks it at the one gate where the
answer is cheap.

---

## PRO_01 — A criterion about the browser is proven in a browser engine or a component harness

**Applies to:** a plan, at approval.

**Forbids:** a plan whose only named proof for a criterion about what happens in the browser — markup
rendering, an interaction, something a person sees change — is a request through
`WebApplicationFactory<T>` and an assertion over the response body.

**Why:** the response is the input to the behaviour rather than the behaviour, so such a proof passes
over a component that never renders and an interaction that never fires. This profile declares bUnit
for a component and Playwright in Chromium and WebKit for a page, and those are the layers that
execute the thing the criterion is about. `CON_06` already asks whether a proof is adequate to its
criterion and cannot name a tool, so on this stack the question it asks has no reference point until
this rule supplies one. `CON_12` is a different question: it forbids a criterion only a person can
check, where this forbids a machine check at the wrong layer — which is worse, because it goes green.

## PRO_02 — A plan reaching for the kernel names the seam and the type

**Applies to:** a plan, at approval.

**Forbids:** a plan that names `MofonCode.Core` as its approach without naming what attaches to it —
the handler, the pipeline behaviour, the specification, the result type — and where in the feature
folder it goes.

**Why:** the kernel is a mediator, `Result<T>` over FluentResults, pipeline behaviours and composable
specifications, and *"use MofonCode.Core"* selects none of them. An agent takes the shape of the code
beside it far more reliably than it follows an instruction, so a plan that leaves the seam unnamed is
settled at build time by whichever neighbouring file the session opened first — and the verification
then adjudicates a shape nobody chose. The cost of naming it is one sentence at the one gate before
code exists.

## PRO_03 — A proof about stored data runs against the store the solution declares

**Applies to:** a plan, at approval.

**Forbids:** a plan whose proof for a criterion about data being stored, queried, ordered or
constrained names an in-memory stand-in for the database — the EF Core in-memory provider, a fake
`DbContext`, a repository double.

**Why:** the in-memory provider does not translate SQL. It accepts queries MS SQL rejects, ignores
constraints the schema enforces, and orders differently, so the proof passes while the criterion is
unmet — and the disagreement surfaces in the first environment holding a real database, with a
verification already recorded as passed. An analyzer could find `UseInMemoryDatabase` in test code,
and where one exists it should; what it cannot read is a plan proposing the substitution in prose
before any code is written, which is the moment this is cheap to refuse.

## PRO_04 — A plan whose work runs more than one process starts them from an Aspire AppHost

**Applies to:** a plan, at approval.

**Forbids:** a plan that leaves the solution with more than one process a developer has to start by
hand and no AppHost under `src/` that starts them. A process is a host project the plan adds or keeps
— an api, a WebAssembly page's development server, a worker — or a backing service a developer would
otherwise start by hand, such as a database, a cache or a broker in a container. One executable with no
backing service needs no AppHost, and a plan that reaches one process by serving its page from the api
meets this rule as fully as one that adds an AppHost.

**Why:** without an AppHost, running the solution is a set of commands somebody has to know, in an
order somebody has to know, and the README that would hold them is the document nobody updates. The
first solution to meet this condition shipped two processes started by two `dotnet run` commands and a
README still saying "Replace this with how to start the application". Its traces were registered by
`AddServiceDefaults` and exported nowhere, because nothing set `OTEL_EXPORTER_OTLP_ENDPOINT` — and an
AppHost sets it for every process it starts and gives them one dashboard to read. The plan's reason
for departing was the cost of adding one, and the scaffold's architecture suite now carries the
AppHost's rows from the first day, so that cost is one project. What an AppHost does not do on its own
is hand a standalone WebAssembly page the api's address: the page reads its configuration in the
browser, where no environment variable reaches, so that address stays in the page's own settings
until a plan takes the hosting integration that serves it.

## PRO_05 — A plan adding script to a Blazor page names the Blazor mechanism it could not use

**Applies to:** a plan, at approval.

**Forbids:** a plan that adds JavaScript to a Blazor page beyond a module collocated with one component
and loaded from `OnAfterRenderAsync` — a page-wide script, a script that starts Blazor by hand, one that
rewrites, wraps or watches the framework's own files — without naming, for each need the script meets,
the Blazor mechanism that covers that need and why it falls short, or the spec criterion that no Blazor
mechanism can meet.

**Why:** Blazor ships the answer to most of what a page needs before it is live or after it loses its
connection — `RendererInfo.IsInteractive` for a control that is not ready yet, the template's
`ReconnectModal` for a lost connection, a JavaScript initializer for start-up, `[PersistentState]` for
what crosses from the server-drawn page to the live one. The first plan to prerender a Blazor page
reached past all of them for a page script that started Blazor itself, and it passed `CON_19` by
arguing the script against the decision that allowed one interop module, because nothing asked what
the framework already did. Its next fifteen findings were defects inside the script, and each remedy
grew it, until a person replaced it with the framework's own mechanisms. A script is the second
language of a Blazor solution, outside its compiler, its analyzers and its component tests, so the
cost of one nobody needed is paid by every later screen that copies it. The second exit is the one
that matters most: when a criterion is what forces the script, saying so puts the criterion in front
of a person, who can narrow it, rather than in front of the next round, which can only patch the
script.
