---
name: verify
description: Verify a completed change against its spec.md and plan.md in fresh context - real build and test output, requirement coverage, manifest drift, silent failures - and commit the verdict as verification.md. Stage 4 (Test) of the AI-Native SDLC artifact chain. Run after implementation, before shipping.
argument-hint: Slug of the slice to verify, or blank to infer from the artifact root
disable-model-invocation: true
---

# /sdlc-core:verify — Stage 4, Test

Prove the change does what the spec said, or find the reason it does not. Schema in
`references/artifact-schemas.md` (the `sdlc-router` skill).

Reachable from **every** build path. `workflows/parallel-build.js` runs this verification
itself at the end of a fan-out; agent teams and single-writer builds have no such step, so
this command is how they reach it. A change that was never verified is not done, regardless
of which of the three build paths produced it.

## 1. Locate the chain and the change

Resolve the artifact root per `sdlc-router` (charter frontmatter `artifact_root`, default
`.sdlc/`). Read `<root>/<slug>/spec.md` and `<root>/<slug>/plan.md`.

- **No plan.md** — nothing to verify against. If this was task-tier work planned from a
  conversation, say so and verify against the plan.md that tier still requires.
- **plan.md is `draft`** — the change was built ahead of its gate. Verify anyway, and say
  plainly in the report that the plan was never approved. That is itself a finding.
- **Nothing built yet** — `git diff` is empty against the base. Stop; there is nothing here.

## 2. Dispatch the verifier — do not verify it yourself

Dispatch the **verifier** agent with the slug and the resolved paths. This is not an
optimisation, it is the whole mechanism: **the session that wrote the code cannot see its own
blind spots.** If you implemented this change in this session, you are exactly the reader who
will rationalise what is there. Hand it to fresh context.

The same applies to a delivery/QA split across teammates. A teammate who watched the
implementer reason through a failure has already heard the explanation and will weigh it.
Verification is a subagent or a separate session, never a participant in the build
conversation.

Pass it: the slug, the artifact root, the diff range, and nothing else. No summary of what you
built, no note about which parts you think are fine — every one of those is a thumb on the
scale.

## 3. Read the real output

The verifier returns actual build and test output. Read it before you relay anything.

**A verification that reports success when tests failed is worse than no verification at all**,
because it converts an unknown into a false known. If the agent's report and its pasted output
disagree, the output wins and that contradiction is itself a blocking finding.

## 4. Write the record

Write `<root>/<slug>/verification.md` per the schema: the verdict, requirement coverage table,
manifest drift, blocking findings with their failure scenarios, and the real build and test
output. `status: draft` until the verdict is acted on.

This artifact is why the stage exists rather than being a chat message. It is what a
retrospective reads, what the next eval case is written from, and what tells you six weeks
later whether a requirement was ever actually tested.

## 5. Present, worst first

1. **The verdict** — SHIP or DO NOT SHIP, in the first line.
2. **Blocking findings**, each with its concrete failure scenario. A finding with no scenario
   goes below, not here.
3. **Requirement coverage** — every R-number implemented but untested, and every one tested
   only vacuously. Untested requirements are the gap that survives to production.
4. **Manifest drift** — files changed that no slice claimed (unplanned scope), and files
   claimed but untouched (a slice that did not land).
5. **What the verifier could not check**, and why.

## 6. Gate — DO NOT SHIP is a stop, not an opinion

On **DO NOT SHIP**: the change does not ship — and **the record is committed anyway**:

```
git add <root>/<slug>/verification.md && git commit -m "verify(<slug>): do not ship - <n> blocking"
```

Commit it *before* fixing anything. A failed verification is the most valuable artifact this
stage produces and the easiest to lose: it is what the retrospective reads, what the next eval
case is written from, and the only evidence that the defect was found here rather than in
production. A stage that records its successes and discards its failures is measuring the wrong
thing.

Then fix the blocking findings and re-verify from step 2 with a fresh dispatch — not by arguing
with the report, and not by asking the same agent to reconsider. A verdict that can be talked
down is not a gate, and a QA role the implementer can overrule is decoration. The re-run appends
a new verification; it does not overwrite the failed one.

If you believe a blocking finding is wrong, the resolution is evidence: a test that
demonstrates the scenario does not occur. Put that in front of the user, and let them decide.

On **SHIP**: set `status: approved` and commit.

```
git add <root>/<slug>/verification.md && git commit -m "verify(<slug>): ship - <n> requirements, <n> tests"
```

Then say what remains: anything under "should fix" that was accepted, and any requirement
carried as covered-by-inspection. Those are the things that come back.
