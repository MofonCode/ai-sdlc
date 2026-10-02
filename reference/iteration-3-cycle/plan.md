---
id: 20260929_4e1a
spec: .sdlc/cycles/20260929_4e1a/spec.md
tier: local
approved_by: Peter Almanzar
approved_at: 2026-09-29T01:07:00Z
---

# Plan — asking for the page returns one roll of a six-sided die, decided for that asking

## Shape, and where it departs from the defaults

**Web — Blazor, static server rendering only.** A Razor component rendered per request produces the HTML
document criterion 6 asks for, and rendering on the server is what makes the value decided per request
rather than in the browser. No interactive render mode and no WebAssembly: interactivity would add a
circuit and the state that comes with it, which works against criterion 4.

**Files are written by hand, not by `dotnet new`.** The template for a Blazor Web App emits static assets,
a layout, sample pages and a stylesheet that this cycle does not need, and a hook refuses any file this
plan did not declare. A hand-written minimal set is both smaller and the only version of this that can
pass its own gate.

**Testing — MSTest on Microsoft.Testing.Platform**, per the default, driven through
`Microsoft.AspNetCore.Mvc.Testing` so every check is an HTTP request to the page rather than a call into a
component. The spec requires exactly that.

**Departures, each deliberate:**

- **No Aspire AppHost.** One process, no backing service, nothing a developer would otherwise start by
  hand — the defaults rule it out in that case, and Aspire can be added later without restructuring.
- **`MofonCode.Core` is not taken.** This cycle needs no dispatch, no result type carrying failure, no
  trace scope, no bounded parallelism and no specification composition. Taking the kernel to roll one
  integer would add a pinned dependency serving nothing in the diff.
- **`AngleSharp` is added to the test project.** Criterion 6 requires asserting that the response parses
  as an HTML document and that the value appears in the text it renders; a substring match over the
  response body would pass on a JSON payload that happened to contain the digit. Test-only, and it
  touches no production dependency.

## Files

| File | Change |
|---|---|
| `.gitignore` | Create. Ignore `bin/`, `obj/` and test output, so build artefacts never enter the diff that verify reads or the directory criterion 3 snapshots |
| `global.json` | Create. Pin the SDK to the 10.0.3xx feature band, `rollForward: latestFeature` |
| `Directory.Build.props` | Create. `net10.0`, nullable enabled, implicit usings, warnings as errors |
| `Directory.Packages.props` | Create. Central package management on, with a `PackageVersion` for each test-project package at the latest stable release compatible with `net10.0`; the build records the versions it resolves |
| `DiceTrial.slnx` | Create. Solution file listing the two projects |
| `src/DiceTrial.Web/DiceTrial.Web.csproj` | Create. `Microsoft.NET.Sdk.Web`, no `PackageReference` of any kind |
| `src/DiceTrial.Web/Program.cs` | Create. Register Razor components and `IDieRoller` → `RandomDieRoller`; map the component endpoint; no session, no authentication, no interactive render mode. Ends with `public partial class Program;` so the test host can reference it |
| `src/DiceTrial.Web/Components/App.razor` | Create. Root document: doctype, `html`, `head` with a title, `body` hosting `Routes` |
| `src/DiceTrial.Web/Components/Routes.razor` | Create. Router over this assembly |
| `src/DiceTrial.Web/Components/_Imports.razor` | Create. The `@using` directives the two components need |
| `src/DiceTrial.Web/Components/Pages/Roll.razor` | Create. `@page "/"`. Injects `IDieRoller`, calls it once while rendering, and writes the value as the text of a single element carrying `data-testid="roll"` |
| `src/DiceTrial.Web/Rolling/IDieRoller.cs` | Create. The seam criteria 2 and 5 are proven through: one member returning the rolled value |
| `src/DiceTrial.Web/Rolling/RandomDieRoller.cs` | Create. The production implementation, drawing from the framework's shared pseudorandom source over the inclusive range 1–6 |
| `tests/DiceTrial.Web.Tests/DiceTrial.Web.Tests.csproj` | Create. MSTest on Microsoft.Testing.Platform, project reference to the web project, package references to MSTest, `Microsoft.AspNetCore.Mvc.Testing` and `AngleSharp` |
| `tests/DiceTrial.Web.Tests/RollPageFactory.cs` | Create. `WebApplicationFactory<Program>` that replaces the registered `IDieRoller` with a test double, and that can run the host with its content root and resolved temp path inside directories created for the test, which is what criterion 3's snapshot observes |
| `tests/DiceTrial.Web.Tests/StubDieRoller.cs` | Create. Returns a caller-supplied sequence of values in order and counts its invocations |
| `tests/DiceTrial.Web.Tests/RollPageTests.cs` | Create. Criteria 1, 2, 5 and 6 |
| `tests/DiceTrial.Web.Tests/NoIdentityTests.cs` | Create. Criterion 4 |
| `tests/DiceTrial.Web.Tests/NoPersistenceTests.cs` | Create. Criterion 3 |

`AGENTS.md` is deliberately not in this list: it already names `dotnet build` and `dotnet test`, and its
repo map describes `.sdlc/` plus "the solution's own source, tests and build configuration", which this
change makes true rather than false.

## Order

1. `.gitignore`, `global.json` and the two `Directory.*.props` first. Central package management has to
   exist before the first restore, and the ignore file before the first build, or artefacts land in the
   diff verify reads.
2. `IDieRoller` before `Roll.razor` and before the stub — both sides of the seam compile against it.
3. The web project complete before the test project, which holds a project reference to it and cannot
   compile ahead of it.

## Proof

Every check is an HTTP request through `RollPageFactory`, so it exercises the page the way a player asks
for it. Test names below are the names that will exist.

- **Criterion 1 — one roll, in range.**
  `RollPageTests.Page_presents_exactly_one_roll_with_a_value_from_1_to_6`: GET `/`, parse the document with
  AngleSharp, select every element carrying `data-testid="roll"`, assert there is exactly one, and assert
  its text parses as an integer within 1–6 inclusive. Two roll elements, a missing one, a non-integer, `0`
  or `7` each fail it.
- **Criterion 2 — decided during the request that serves it.**
  `RollPageTests.Each_response_presents_the_value_its_own_request_rolled`: with `StubDieRoller` primed to
  return 3, then 3, then 5, issue three sequential GETs and assert the rendered values are 3, 3, 5 in that
  order and that the stub was called exactly three times. A constant, a value computed once at start-up,
  and a cached response all fail on the third request; a page that rolled twice per request fails the call
  count. The 3-then-3 pair also means a build that suppressed consecutive repeats fails here, which is the
  behaviour the spec puts out of scope.
- **Criterion 3 — nothing about a roll outlives the response.** Three checks, because no one of them
  reaches the whole criterion.
  `NoPersistenceTests.Serving_rolls_creates_modifies_or_deletes_no_file`: run the host with its content
  root in a directory created for the test, and with `TMP`, `TEMP` and `TMPDIR` pointed at a second such
  directory so that the temp path the app resolves is one only this test writes to. Record every path,
  with length and last-write time, under that content root, the process working directory, that redirected
  temp directory, and the data-protection key directories named by exact path
  (`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` on Windows, `~/.aspnet/DataProtection-Keys` elsewhere).
  Issue ten GETs, record again, assert the two sets are identical. A roll written to a log, a key file
  initialised on first use, or a temp file left behind each fail it.
  `NoPersistenceTests.No_store_is_configured_or_registered`: assert the host's configuration exposes no
  `ConnectionStrings` section and that the composed service provider registers no database context, no
  connection or connection-factory type, and no client for a database or cache — then serve a roll in that
  state. This is what checks the store half of the criterion against what the application actually has.
  `NoPersistenceTests.The_web_project_declares_no_package_reference`: read `DiceTrial.Web.csproj` and
  assert it contains no `PackageReference` element. This is a tripwire and nothing more: it establishes
  that the web project takes no third-party dependency, and it does **not** establish that no roll is
  recorded anywhere, because the framework alone can reach a remote store with no package at all.
- **Criterion 4 — no identity.**
  `NoIdentityTests.A_response_sets_no_cookie_and_asks_the_player_for_nothing`: GET `/` on a client holding
  no cookies and following no redirects; assert the response carries no `Set-Cookie` header, that the
  parsed document contains no `input` element and no element whose name or value mentions a verification
  token or session, and that the roll element is present in that same response — so a page withholding the
  roll pending sign-in fails.
- **Criterion 5 — no player-supplied input determines the value.**
  `RollPageTests.Player_supplied_values_do_not_change_the_roll`: with the stub primed to return 2 for every
  call, issue `GET /?value=6`, `GET /?roll=6`, a GET carrying header `X-Roll: 6`, and a POST whose body
  names 6. For each response that carries a page, the rendered roll is 2. A request the app refuses
  outright presents no roll at all and satisfies the criterion that way; the assertion accepts either, and
  fails if any response renders 6.
- **Criterion 6 — delivered as a page a player can open.**
  `RollPageTests.The_roll_arrives_as_an_html_document_whose_text_shows_it`: GET `/`, assert the response
  content type is `text/html`, assert AngleSharp parses a document with `html` and `body` elements, and
  assert the rolled digit appears in the body's rendered text content. A JSON or plain-text response fails
  the content type; a value present only in an attribute fails the text assertion.

## Cannot prove

- **That the production roller is impartial.** Criterion 2 and criterion 5 are proven through a substituted
  source, which is what makes them deterministic — and it means a `RandomDieRoller` that always returned 4
  would satisfy every proof above. The spec records the distribution criterion as blocked on intent Open 3,
  so there is no criterion to prove here, and inventing a sample size would put a number nobody chose into
  a test. Until Open 3 is answered, the only control on the production roller is reading it, and it is
  three lines.
- **Anything about reachability from another machine.** Blocked on intent Open 2. This plan binds no
  address and adds no launch profile, so it neither settles that question nor prejudges it.
- **That no roll leaves the process by a route the snapshot cannot see.** Criterion 3's checks cover the
  content root, the working directory, a redirected temp directory and the data-protection key paths. A
  write to a hard-coded absolute path outside that set, a write by code that ignores the temp environment
  variables, or an outbound call carrying a roll to a remote service using only framework APIs, is excluded
  by none of them. The controls on that residue are reading the diff at landing and the no-package
  tripwire — not a test, and this plan does not claim otherwise.

## Risk

- **Antiforgery may put a cookie on the response.** Mapping Razor component endpoints requires the
  antiforgery middleware, and if it emits `Set-Cookie` on a plain GET, criterion 4 fails. Criterion 4's
  test is the detector. The fallback, declared here so that taking it is not undeclared work, is to serve
  the same document from a minimal endpoint in `Program.cs` without that middleware — a change inside a
  file this plan already names, adding none.
- **Data protection may initialise and write key material on first use.** Its key directories are inside
  criterion 3's snapshot now, so if it initialises during a run of requests that test fails rather than
  passing quietly. What keeps it from initialising at all is having no form, no interactivity and no
  protected payload; if the antiforgery fallback above is taken, this goes with it.
- **The no-`PackageReference` assertion is strict on purpose.** Any future need for a production package in
  the web project fails that test, and the right response is to change this plan, not to loosen the
  assertion.
- **Package versions are not pinned in this document.** They are resolved at build time to the latest
  stable releases compatible with `net10.0` and written into `Directory.Packages.props`, so the diff
  records exactly what was taken. A plan that guessed version numbers it could not verify would be worse.
