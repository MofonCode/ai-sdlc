# `dotnet-default` constraints

The rules a solution on this profile is judged against, on top of the global set. They resolve between
the global `CON_` library and the solution's own local rules, so a `LOC_` rule is still the last word.

**These may name a language, framework or package, and that is what the tier is for.** The global
library forbids itself from naming one — *"Stack neutrality. These bind solutions on any stack, so none
of them may name a language, framework or package. Stack specifics live in the profile and in
analyzers."* A rule that needs a name has nowhere else to go: an analyzer decides a question about
code, and these are questions about a plan.

**Ids are a monotonic sequence of their own.** `PRO_01` is this profile's first rule and is unrelated
to `CON_01`. Assigned once, never reused; a gap is the record of a removed rule. A bare id is
revision 1.

**Four parts, all required**, as in the global library: the heading states the rule, `Applies to` says
which artifact and at what moment, `Forbids` names what becomes illegal, and `Why` says what goes wrong
without it. The suite fails on a rule missing any of them.

**Three admission tests, all of which must pass.** Its `Forbids` clause names something that becomes
illegal; no analyzer, schema or hook could decide it; and it is incompatible with at least one
plausible implementation. The second test is what keeps this list short — a rule about code belongs in
an analyzer, and a mechanical rule about an artifact belongs in a schema or a hook. Three rules
survived it, which is the honest number rather than a disappointing one.

**This profile's declared defaults are not here.** They are in the solution's declared stack, which a
person owns, and departing from one is `CON_15`'s business rather than a rule here. What these three
govern is the case where a plan satisfies every declared default and still proves nothing.

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
