# Plan: make the AppHost bind, and give the perspectives what two cycles kept raising

## Outcome (2026-10-18)

Both parts are on `feat/apphost-and-perspectives` (worktree `C:\Repos\ai-sdlc-aspire`): `d24f566`
for the AppHost, `dbe8be9` for the operability rule and the perspective methods. 1007 tests pass,
and the scaffold harness builds and tests (25).

- **The rule is `CON_20`, not `CON_17.2`.** The global library's id rules say a revision may only
  reword, so a rule that forbids something new gets a new id. It sits beside `CON_17`.
- **A3 result:** one `dotnet run` of the AppHost starts both processes and the dashboard. The page still
  reads the api's address from its own settings. Delivering it needs `Aspire.Hosting.Blazor`, which is
  prerelease. Aspire 13.4.6 also needs a pin to a patched MessagePack (2.5.303 or later).
- **Not done (needs a person):**
  - add the `PRO_04` row to cycle-01's and cycle-02's techstack;
  - run an interrogation of a plan with `PRO_04` on, to see it block;
  - release and reinstall the tool.

**Follow-ups** (not on the ADO board, which has been dormant since September):

- The Greet reference slice survives a cycle that adds real work.
- "Running it locally" in a solution's AGENTS.md is never filled in. The build skill could update it
  when a plan adds or removes a process.
- Carried from the earlier plan:
  - `sdlc hash` and a validator for findings rounds;
  - a place in the evidence for the vulnerable-package scan;
  - the Playwright `MSTest.v4` profile row.

---

Working document, not part of the repository. It covers two pieces of work:

- **Part A**: the binding Aspire rule (option 2) and the pre-seeded AppHost role (option 3).
- **Part B**: the perspective changes the cycle-02 suggestions point to. Part B is a proposal: it waits for
  decisions B1–B3 at the end.

**Base.** Branch from `fix/cycle-02-findings` after it is merged into `development/iteration-4-foundation`.
Part A changes `StructureTests.cs.template` and the scaffold, and those templates are already different
on that branch.

---

## Part A — An AppHost binds once there is more than one process

### What went wrong in cycle-02

The techstack row "Aspire, where there is more than one process" is a default. `CON_15` only asks a plan
to say why it departs from a default, and cycle-02's plan said why, so no reader could block it. The
reason it gave was the cost of amending the scaffolded architecture suite. The suite has no AppHost
role, so an AppHost meant a `CON_18` rule change, two packages and a project.

### A1. `PRO_04` on `dotnet-default`

Add to `src/Sdlc.Core/Constraints/Profiles/dotnet-default.md`:

> **PRO_04 — A plan whose work runs more than one process starts them from an Aspire AppHost**
>
> **Applies to:** a plan, at approval.
>
> **Forbids:** a plan that leaves the solution with more than one process a developer starts by hand,
> and no AppHost under `src/` that starts them. A process is a host project the plan adds or keeps
> (an api, a WebAssembly dev server, a worker), or a backing service a developer would otherwise start
> by hand (a database, a cache, a broker in a container).
>
> **Why:** two `dotnet run` commands and two fixed ports written into two projects' configuration are
> the shape that drifts silently. cycle-02's maintainability reader found that the api and page
> addresses were each written twice, and that a missed edit shows up only as a refused CORS request at
> run time. Without an AppHost there is also no dashboard to read the traces `ServiceDefaults` already
> emits. A default is departed from whenever it is inconvenient, and it was departed from on the first
> solution that met the condition.

**Run it through the three admission tests** at the top of that document:

- **Forbids something:** yes, a multi-process plan with no AppHost.
- **No analyzer, schema or hook could decide it:** yes. It judges a plan's prose before any project exists.
- **Incompatible with a plausible implementation:** yes. cycle-02's approved plan is that implementation.

**The rule has to say what it does not ask for.** A single-process solution is untouched: the rule's
condition is the techstack note's own condition. Collapsing to one process is a legitimate way to meet
it. cycle-02's `plan.r4` F_13 suggested serving the page from the api, and that option should stay the
cheaper answer, not be pushed aside by "add an AppHost". Say so in the rule's Why, so a reader does not
block a single-process plan for lacking an AppHost.

**Edits that come with the rule:**

| File | Change |
|---|---|
| `src/Sdlc.Core/Constraints/Profiles/dotnet-default.md` | the rule. "Three rules survived it" in the preamble becomes four |
| `src/Sdlc.Core/Solutions/StackProfile.cs` | the "An Aspire AppHost is conditional" note names `PRO_04` as what makes the condition bind. The orchestration row's wording stays |
| `CONCEPT.md` §11.1 | the same note, plus the new decision **D-96** below. Line 1620 ("ships three rules") and §20's "Why three rules" paragraph (~5518) need their counts and reasoning updated, not just the number |
| `CONCEPT.md` D-96 | **Decision:** the AppHost condition binds as `PRO_04`. **Why:** cycle-02's departure and the cost argument it rested on. **Rejected:** (a) *a LOC_ rule in each solution*: every solution would rediscover the same rule; (b) *scaffolding an AppHost always*: D-55 and F-24 stand, because a single-process solution would get a host it does not need; (c) *leaving it a default*: cycle-02 shows the outcome. It must answer D-55's rejection of "a `PRO_nn` on `dotnet-default`" head on. D-55 rejected a rule about an AppHost's container labels, which needs an AppHost to exist before it can judge anything. `PRO_04` judges the plan, which always exists, so this is a different rule and does not relitigate D-55 |
| `docs/guides/declaring-the-stack.md` | the rules table gains the row. **It must say that a new rule reaches no existing solution until a person adds its row to that solution's techstack**, and that `sdlc stack` lists it as one a later release adds |
| `skills/plan/SKILL.md` | check, but expect no change: the plan skill reads the rules in force. Add a line only if it says "AppHost is conditional" somewhere |
| suite | `ProfileLibraryTests` / `StackProfileTests` / `ConstraintIdTests` pick up the new id; fix any count they hold. Check that `StackCoverageTests` does not need a mapping row |

**Release note line:** "`PRO_04` reaches cycle-01 and cycle-02 only when a person adds
`| PRO_04 | true | ... |` to the solution's techstack."

### A2. The scaffold's architecture suite knows the AppHost role from the start

`src/Sdlc.Core/Init/Scaffold/StructureTests.cs.template`:

- `_allowedProjectReferences["AppHost"]` = the hosts it starts: `["Api", "Web"]`. **Open: check how Aspire
  references a project resource.** `<ProjectReference>` with `IsAspireProjectResource` is still a project
  reference, so the direction rule reads it. Grow the row when a new host role is added.
- `_allowedPackages["AppHost"]` = `["Aspire.Hosting"]`, prefix-matched like the Web row. The comment says
  why it is a prefix: each backing service's hosting integration is its own `Aspire.Hosting.*` package,
  and listing them would fail on the cycle that adds the first one.
- **Check `Every_project_under_src_is_named_in_both_rules`** runs one way only: project → row. If any test
  fails on a row that names no project, the AppHost row needs an exemption, with a comment saying it
  is waiting for its project.
- **Check `ConventionTests.Assemblies`** (see B4): an AppHost assembly that isn't listed escapes the
  sealed and naming rules.

**What this buys:** a cycle adding an AppHost adds a project. It no longer amends a rule table, so it
is no longer a `CON_18` change, and `DeclaredFiles.Governs` stops flagging it as a rule change.
**What it costs:** a row describing a project that does not exist yet. That is the same cost the `Ui`
row already pays, and the techstack notes argue for it.

**Not in A2:** an AppHost template. D-55 and F-24 rejected shipping one, and nothing here changes that.

### A3. Aspire and a standalone WebAssembly page — check before claiming it works

**This is the risk in Part A.** Aspire passes addresses to a process through environment variables.
A standalone Blazor WebAssembly app reads its configuration from `wwwroot/appsettings.json` in the
browser, and the DevServer's environment never reaches the page. So an AppHost starts both processes
and shows them on one dashboard, but it does not by itself remove the fixed api address in the page's
configuration. Before the rule's Why claims less drift:

- try it in a scratch copy of cycle-02, with an AppHost starting Api and Web;
- record what Aspire can and cannot inject into the page;
- if it cannot, the Why claims a single start command and the dashboard, not service discovery. An
  over-claim here is the "looks enforced" failure that D-55 describes.

### A4. Verification

- `dotnet build` and `dotnet test`: clean, with any counts the suite holds updated.
- The scaffold harness (`tools/Sdlc.ScaffoldHarness`) builds and tests the generated solution with the
  new rows.
- **Prove the rule's value:** in the scratch copy from A3, add an AppHost project and confirm the
  generated architecture suite passes with no table edit.
- **Prove the rule binds:** run `/sdlc:interrogate` over a copy of cycle-02's plan with `PRO_04 | true`
  in its techstack, and confirm a blocking finding cites `PRO_04`.

---

## Part B — What the perspectives kept raising

### The evidence, honestly sized

cycle-02 left 69 open suggestions. Grouped by the objection behind them, not by finding id, they come
down to five. Each was raised at **three or four stages** of one cycle and stopped nothing, because a
suggestion is not supposed to stop anything. **cycle-01 raised the same objections** in a separate
solution: a visible focus indicator, reflow at 320 px, non-text contrast, forced colours, failures that
leave no record, a disclosure test at the wrong layer, and CORS failing silently.

The framework's own threshold (`docs/concepts/perspectives.md`, How a perspective earns rules) is the
same objection **across three cycles**. This is two cycles, and both built the same die, so part of the
recurrence is the same problem being solved twice. **That argues for changing the methods now and
waiting on the rules.** Changing a perspective's Cover this or Questions is not a rule and needs no
threshold. A rule should wait for a third, different cycle, unless you decide otherwise (B1).

### B1. Operability — a failure that is logged and never checked *(strongest)*

| Stage | What the reader raised |
|---|---|
| intent | only the player is told; nobody else would ever know |
| spec | criterion 5 can be met while the failure leaves no trace (the perspective's own spec question) |
| plan | nothing tests that the boundary logs the exception; `RollClient`'s three causes are not told apart |
| verify | four log calls in the diff, every test built with `NullLogger`, so deleting them passes everything |

**The method already asks for this.** Cover this at spec: "a criterion for what is recorded when each
new failure happens". Evidence: "tests that cause each named failure and assert the log entry". The
author was told and did not do it, and the reader saw it and could only suggest. So this is a rule
gap, not a method gap. `CON_17` ("A spec says what somebody is told when the action fails") covers the
user and says nothing about the operator.

**Candidate:** `CON_17.2`, "A spec says what somebody is told **and what is recorded** when the action
fails." It names no stack, so it is a global rule. It blocks at spec, which is the cheapest stage; the
plan and verify then follow from `CON_06` and `CON_08`, because a criterion now exists for them to prove.

### B2. Accessibility — what the scan cannot see *(method change, no threshold needed)*

Raised at every stage in **both** cycles: visible focus (2.4.7 and 2.4.11), reflow at 320 CSS px or
400% zoom (1.4.10), 3:1 non-text contrast for graphics that carry meaning (1.4.11), pips disappearing
under forced colours, focus and keyboard on the failure path, and 24 px pointer targets (2.5.8).

The techstack note says "The scan is the floor, not the proof", but the perspective's Cover this lists
only the scan and keyboard focus placement. **The author is never told about the rest, so the reader
finds it every round.** Change `src/Sdlc.Core/Perspectives/accessibility.md`:

- **spec, Cover this:** a criterion that focus is visible on every new interactive element; a criterion
  for reflow at 320 CSS px wherever the change lays something out; non-text contrast for any graphic that
  carries meaning.
- **plan, Cover this:** a browser test for each of these, because the scan checks none of them; forced
  colours emulated where meaning is drawn with background colour.
- **verify, Questions:** "Does the evidence show focus visible, reflow and non-text contrast, or only the
  scan?"
- **Evidence:** add those three tests.

Then the "Questions" spec item "Could every criterion be met while the page is unusable by keyboard?"
gains its visual twin.

**A rule later, if a third cycle raises it:** a `PRO_` rule, because it would have to name the scan tool
to say what the scan misses ("a criterion about seeing the page is not proven by an axe-core scan
alone").

### B3. Security — a control proven only by what it lets through

| Stage | Raised |
|---|---|
| plan, verify | CORS is the only access control on `/roll`, and no test sends a refused origin. The routed tests set their own `Access-Control-Allow-Origin`, so the policy never ran |
| intent, spec, plan | "no internal detail" is tested in a component, below the host. The host's error page, the failure envelope's body and the browser console are never checked |

Both have the same shape: **the proof exercises the success path of a control.** Candidate method change
to `src/Sdlc.Core/Perspectives/security.md`:

- **plan, Cover this:** for each access control, a test that sends what it must refuse.
- **plan, Cover this:** for a disclosure criterion, a check where the outsider reads the response:
  the raw response body, not the rendered component.

**Candidate rule (wait for a third cycle):** a global rule, "A plan's proof of a control includes a case
the control refuses". It may already fit under `CON_06` (proof adequate to its criterion). Check
before adding a rule.

### B4. Maintainability — two framework defects, not rules

Most of the 22 maintainability findings are "one fact written in two places": the wording, the face
range, the envelope shape, the ports. That is what the plan's "where the knowledge lives" section
already claims to cover. cycle-02 claimed it and the claim was wrong, which the reader caught as a
suggestion. That is a judgement about one plan, not a missing rule.

Two items are **framework defects**:

- **`ConventionTests.Assemblies` in the scaffold lists two assemblies by type**: Application and Api.
  `Cycle02.Ui` was added and escaped the sealed and naming rules, and its code already breaks
  `_camelCase` in two places. The same gap applies to an AppHost (A2). Fix: load every `src/`
  project's assembly from the test output, or add a structure test that every `src/` project is
  in `Assemblies`. This is the same property this repository's own suite enforces: every directory
  holding projects is scanned.
- **The Greet reference slice survives** a cycle that adds real work. This is the build's own
  `Noticed:` line. The scaffold says Greet is replaced by real work, and nothing makes that happen.
  It should go on the board, not into a rule.

### B5. Evidence — already on the board

`verify.r1` security F_02 is the vulnerable-package scan with nowhere to live in the evidence. That
item is in the earlier plan's "Not in this plan" list. Part B adds nothing new for it.

### Also surfaced, small

The solution's AGENTS.md still says "Replace this with how to start the application" after a cycle
that added the second process. Neither the build skill nor verify asks anyone to fill it in. With
Part A, the answer becomes one line (`dotnet run --project src/<Name>.AppHost`). Candidate: the build
skill updates "Running it locally" whenever the plan adds or removes a process.

---

## Decided (2026-10-18)

- **B1:** add `CON_17.2` now, on two cycles' evidence. Record the departure from the three-cycle
  threshold in the rule's entry.
- **B2, B3:** apply the accessibility and security method changes in this branch.
- **B4:** the `ConventionTests` fix goes in this branch. The Greet item was not decided, so it goes on
  the board.

## Decisions as first put

- **B1.** Add `CON_17.2` now, on two cycles of evidence from the same problem, or put it on the board
  and wait for a third, different cycle? I lean towards now. It is the only one where the method already
  says it and the author still did not do it, which is what a rule is for. Acting on two cycles departs
  from the framework's own threshold, and D-96 or the rule's entry should record why.
- **B2 and B3.** Apply the method changes (Cover this, Questions, Evidence) in this branch? I recommend
  yes. They are authoring guidance, not rules, so the threshold does not apply.
- **B4.** The `ConventionTests` fix and the Greet item: in this branch, or as a separate work item?

## Order

1. Merge `fix/cycle-02-findings`, then branch.
2. A3 first. It decides what the rule's Why may claim.
3. A2, then A1. The rule is written with the scaffold already able to meet it.
4. B2 and B3, the method changes, each with a perspective-suite run.
5. B1 and B4, as decided.
6. Docs in the same commits as each change, per AGENTS.md.
