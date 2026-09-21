---
slug: every-workflow-body-runs
status: draft
spec: none
tier: task
slices:
  - id: run-and-prove
    files: ["plugins/sdlc-core/workflows/parallel-build.js", "scripts/run-workflow.mjs", "tests/AiSdlc.Tests/WorkflowExecutionTests.cs", ".sdlc/CHARTER.md", ".sdlc/OPEN.md"]
    requirements: [W1, W2, W3, W4, W5, W6, W7, W8]
    depends_on: []
---

# plan — a workflow body is executed before it ships

**Tier: task, stated out loud.** The behaviour was agreed in conversation on 2026-09-21: the
dependency-wave loop must terminate, and a shipped workflow body must be executed under test
rather than only parsed. No design question is open that changes the cost. Five files rather
than three because two are bookkeeping this framework mandates — a register row for the new
check, and the open item it closes.

**No spec, so no R-numbers.** Requirements are `W1`–`W8`, from that conversation.

## Why

`parallel-build.js` computes dependency waves, then removes the dispatched ones:

    remaining = remaining.filter((s) => !ready.includes(s.id))

`ready` holds slice **objects** and `s.id` is a **string**. `Array.includes` compares with
strict equality, so an array of objects never contains a string: the predicate is always true,
`remaining` never shrinks, and the loop does not end. Worse than a stall — the line above adds
every dispatched id to `done`, so on the next pass every slice looks ready again and the wave
list grows without bound. It fails this way on any plan at all, a single-slice plan included.

**Nothing below that loop has ever executed.** Not the approval gate, not one implementer
dispatch, not the verification stage. Every fan-out this framework has done was dispatched by
hand, so the parallel-build premise is unproven rather than working.

**The one-line fix is not the change.** The reason this survived is that the only instrument
over a workflow is `EveryWorkflow_IsValid`, which shells out to `check-workflow.mjs` — and that
file says in its own words *"Not executed - constructing the function is enough to parse it."*
A non-terminating loop parses perfectly. Without a harness that runs a body, the fix ships
verified by reading, which is the same defect class that let it through. So the harness is the
work and the fix rides with it.

## Requirements

- **W1.** The dependency-wave loop removes dispatched slices, and terminates for any plan whose
  dependency graph is acyclic.
- **W2.** A shipped workflow body is **executed** under test, in the wrapper shape the runtime
  uses — `export const meta` lifted out, the rest inside an async function with `args`, `agent`,
  `parallel`, `pipeline` and `phase` bound. The same five the existing parse check binds, so the
  two validators agree on what a body may call.
- **W3.** Execution is **bounded, and the bound is enforced from outside the body.** A body that
  does not terminate fails in seconds, naming that it did not terminate — not by hanging until
  something else gives up.
- **W4.** The gate is proved by execution: a plan whose `status:` is not `approved`, and a
  payload whose slice ids do not match the plan's, each return `blocked` **with no implementer
  dispatched**.
- **W5.** Wave order is proved: a slice is never dispatched in the same wave as a slice it
  depends on, and every slice is dispatched exactly once.
- **W6.** A red build in one wave halts before the next wave dispatches.
- **W7.** A do-not-ship verdict returns `blocked` rather than `verified`.
- **W8.** The new check is named in the register, per the invariant that every known
  silent-failure class has a named check.

## Design, such as it is

**The harness must be Node.** The charter commits to tests driving the real shipped artefacts as
external processes — *"a reimplementation in C# would test the reimplementation, and the two
would drift silently"* — and running JavaScript needs a JavaScript engine. It is the second
thing in this repository that earns Node, for the same reason as the first.

**It is repo tooling, not shipped.** `scripts/run-workflow.mjs`, beside the parse checker,
invoked from a test so `dotnet test` stays the single entry point. Nothing under `plugins/`
gains a file, so the engine's no-stack instruments are unaffected.

**The bound needs a worker thread, and this is the one non-obvious decision.** The wave loop is
*synchronous*. A timer never fires, a `Promise.race` never settles, and an `await` never yields
— so nothing inside the process can interrupt it. The harness therefore loads the body in a
`worker_threads` worker and the parent terminates it on overrun. Terminating a worker interrupts
the isolate even mid-loop, which a signal-free in-process timeout cannot do.

**The agent double is strict.** `agent(prompt, opts)` looks up `opts.label` in a scenario file
and **throws on an unknown label** rather than returning a default. A double that answers
anything lets a test pass while the body dispatches something nobody expected, which is the
class of failure this whole change is about.

**`parallel` records its group.** That the thunks arrived together is what proves W5; asserting
on a flat list of labels would pass for a body that dispatched everything serially.

## Work order

One slice, one writer. The fix and its proof are each other's precondition, and the register row
and the test class that satisfies it must land in the same commit — the register check reads the
charter and the test directory in both directions, so either alone is red, and nothing is
committed on a red build.

**`plugins/sdlc-core/workflows/parallel-build.js`** — one line. `!done.has(s.id)` rather than
`!ready.includes(s.id)`; `done` already holds exactly the dispatched ids at that point, so the
fix removes a second source of truth rather than adding a comparison. Nothing else in the file
changes.

**`scripts/run-workflow.mjs`** (new) — `node scripts/run-workflow.mjs <workflow> <scenario.json>`.
Lifts `meta` out the way the parse checker does, wraps the remainder, runs it in a worker with
the five globals bound, and prints one JSON object: the phases entered in order, the dispatch
groups in order, and the body's return value. Exit 0 on completion, exit 1 with a named reason
on overrun, on an unknown agent label, or on a thrown body.

**`tests/AiSdlc.Tests/WorkflowExecutionTests.cs`** (new) — drives the **real shipped**
`parallel-build.js` through that harness across the scenarios below. A new file with one class,
because the register check reads only the first `[TestClass]` per file and only the top
directory; adding methods to `WorkflowTests` would register nothing.

**`.sdlc/CHARTER.md`** — one register row: *a workflow body parses but does not terminate, or
throws, when run*. The existing row for a body that does not parse stays; they are different
failures and the second is the one that shipped.

**`.sdlc/OPEN.md`** — item 6 closes. Both halves of it: the loop is fixed and the harness that
its own text said was missing now exists.

## Tests

| Proves | Scenario | Assertion |
|---|---|---|
| W1, W3 | Two slices, one depending on the other | The body completes inside the budget. This is the check that fails today. |
| W1 | One slice, no dependencies | Completes — the loop fails on this too, and a single-slice case is the cheapest reproduction. |
| W5 | Slice `b` depends on `a` | Dispatch groups are `[gate]`, `[build:a]`, `[build:b]`, `[verify]`; each build label appears exactly once. |
| W5 | Two independent slices | Both build labels arrive in **one** group, which is the only thing that makes this a parallel build. |
| W4 | Gate reports `status: draft` | Returns `blocked`, and no `build:` label was ever dispatched. |
| W4 | Gate reports `manifestMatches: false` | Returns `blocked`, no `build:` label dispatched, and the response names both id lists. |
| W6 | Wave 1 returns `buildPassed: false` | Returns `halted`, and no wave-2 label dispatched. |
| W7 | Verify returns `ship: false` | Returns `blocked` with the blocking findings, not `verified`. |
| W2 | — | The harness binds the same five globals the parse checker binds, asserted against that file so the two cannot drift apart silently. |
| W8 | — | The new class is named in the register. Satisfied by the existing register check, which fails if it is not. |
| non-vacuity | Every scenario | The harness reports at least one dispatch, or the assertion says it compared nothing — in the shape this suite already uses. |

**Mutation-test per the invariant**, and the first one is the point of the whole change:

- Restore `!ready.includes(s.id)` and watch the termination scenarios fail on the bound, naming
  non-termination rather than timing out anonymously.
- Make the gate double return `approved` for a draft fixture and watch W4 fail.
- Collapse the dispatch groups into one flat list and watch the wave-order assertion pass when
  it should not — proving the grouping is load-bearing and not decoration.
- Remove the register row and watch the register check fail.

**Budget:** the worker bound is seconds, not the sixty this suite's process runner allows, so the
new class adds no meaningful time. The suite takes about six minutes, nearly all of it two rows
that build real solutions.

## What this makes stale elsewhere, declared rather than discovered

**`no-two-plans-claim-one-file` is at draft and three of its claims become false the day this
ships:** that fan-out is not covered *"because the build has never run"*; that the build's status
gate *"is unreachable"*, twice; and the reasoning that a guarantee asserted against a code path
that has never run is not a guarantee. **That intent is deliberately not in this manifest.** It
is under active revision, editing another chain's draft from here would be the lapse that chain
exists to catch, and it has not been approved — so the honest sequencing is that whoever next
revises it reads this plan first. Recorded here because nothing would otherwise tell them.

**And one of its open questions now has a file in it.** That intent asks *"Does reconciling the
JavaScript parser belong here?"* — the JavaScript being `parallel-build.js`, which **this plan
claims**. So that is no longer a tidiness question the spec gate can settle either way: answering
yes puts two live plans on one implementation file, which is the precise state that chain exists
to prevent, and the mechanism that would catch it is the thing it has not shipped. The answer
this plan assumes is **no** — the file is out of that chain's scope — and it should become a
non-goal there rather than remain an open question. Both chains also add a register row to the
charter, which the artifact-root carve-out makes invisible to any instrument by design.

**This is the exact collision that chain is about**, arriving while it is still a draft: two
lines of work, one making the other's prose wrong and one of them claiming a file the other is
still deciding about, with nothing watching. It is not caught here — that intent has no plan
yet, so it claims no files and there is nothing to compare. Worth stating plainly rather than
letting it read as an argument for either order.

## Risks

**The harness proves the body runs, not that the runtime runs it this way.** The wrapper shape
is copied from the parse checker's, which is itself a reading of the runtime rather than a
contract with it. If the real runtime binds a sixth global or wraps differently, both validators
agree with each other and neither agrees with production. *Early-warning sign:* a workflow that
passes both checks and fails when actually dispatched. The mitigation in scope is to assert the
two validators bind the same set, so at least they cannot drift apart unnoticed; the residue is
real and is the reason a first live fan-out still matters after this.

**Terminating a worker mid-loop is the mechanism the bound rests on.** If a body found a way to
block that termination, the harness would hang and the suite would rely on the outer
sixty-second kill. *Early-warning sign:* a workflow test taking a minute. Accepted rather than
engineered around: the alternative is a separate process per scenario, which costs more than the
failure is worth.

**A green harness invites trusting fan-out before it has ever been run for real.** The gate, the
dispatch and the verify stage will have executed against doubles, not against agents. *Early
warning sign:* someone citing this suite as evidence that parallel build works. It is evidence
that the body's control flow is right, which is strictly less.

## Rollback

`git revert` of one commit. The loop returns to not terminating and the harness goes with it,
which is the state of the world today.
