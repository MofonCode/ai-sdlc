# Perspectives

What examines a change beyond the rules, when each examination runs, and how to read what it found. Read
it when a findings round carries ids like `security.F_02`, when a build's commit says
`Noticed: [operability] …`, or before an intent interview asks what the change touches.

**A perspective is a way of looking, not a checklist.** Security follows every input to where it is used;
operability asks whether the person on call at 3 a.m. could tell from the logs what failed and for whom.
The design and the reasoning behind it are `CONCEPT.md` §6.5 and decision D-90.

## What one carries

Each perspective is a document built into the `sdlc` binary, and every one has the same four parts:

| Part | What it is | Who reads it |
|---|---|---|
| **method** | how to examine a change from this angle, step by step | the perspective's own reader at `interrogate` |
| **cover this** | a short list per stage of what the document being written should already contain, where the intent asks for it | the author at `intent`, `spec`, `plan` and `build` |
| **questions** | what a reader asks of each stage's document, intent through verify | the reader at `interrogate`, and `verify` of the evidence |
| **rules** | the only part that can block | every reader |

It also names **the evidence** it needs recorded at build — a vulnerable-package scan, an accessibility
scan, tests named for the abuse cases.

**Read one rather than a summary of it:**

```
sdlc perspective show security --for spec            # the method, the spec's questions, the rules, the evidence
sdlc perspective show security --for spec --cover    # only what a spec author should cover
```

## Which there are

| Perspective | Looks at | On when |
|---|---|---|
| `security` | whether the change can be misused — by somebody it was not meant for, with input it did not expect, or to reach what it should not | always |
| `operability` | whether somebody who did not write it can tell what failed, for whom and why, from what it emits | always |
| `maintainability` | whether the next person can find what to change, change it in one place, and trust the tests | always |
| `accessibility` | whether everybody it is for can use it — by keyboard, by screen reader, zoomed, with motion turned down — against WCAG 2.2 AA unless your recorded decisions name another target | a user interface |
| `ui-ux` | the experience of a user interface | a user interface |
| `infrastructure` | what the change costs to host and run | infrastructure |
| `data` | stored data and migrations | stored data |
| `api` | compatibility for whoever calls a public API | a public API |
| `performance` | a stated performance budget | a budget the intent states |

**Not every name has a document yet.** `sdlc perspectives <id>` marks one that a cycle asked for and
the binary does not carry as *not written yet: nothing to run* — a gap in what was examined, not a pass.

## How a cycle gets its perspectives

**Three sources, in the order a cycle meets them.**

1. **Always on** — security, operability, maintainability. Nothing turns these off.
2. **Named by the intent.** The interview's last question is *what does this touch, beyond the basics?* —
   *a page people use*, *stored data*, *a public API*. The answer becomes the intent's `perspectives` key,
   which **only adds**. It sits inside the approval's hash, so the person approving sees how much scrutiny
   the cycle asked for.
3. **Brought by the plan's declared files.** A perspective whose file patterns match something the plan
   declares joins even if the intent missed it — a `.razor`, `.cshtml` or `.css` file, or anything under
   `wwwroot/`, brings accessibility. The plan covers it, and the plan's interrogation raises that the
   intent did not name it.

**To see the list and the reason for each:**

```
sdlc perspectives <id>
```

## When they run

| Stage | What a perspective does |
|---|---|
| `intent` | **asks.** The interview's last question is the active perspectives' list for the intent, in this solution's terms. What the person picks becomes an outcome in their words; what they do not pick is not written |
| `spec` | **steers, and never widens.** A list item the intent asks for shapes its criterion — the keyboard path through an interaction the intent asked for, the hostile input to a field it asked for. An item it does not ask for gets no criterion and is named in `Out of scope`, so the approval shows the gap |
| `plan` | **steers.** The list is how the criteria are met — where a secret is kept, which log records a failure, which layer runs the scan. An item that would need a criterion the spec lacks goes under `Risk`, not into the proof |
| `interrogate` | **judges.** One fresh process per active perspective, run at the same time and blind to each other, each handed only its own method, its questions for this document, its rules, and the document with the one above it. Then **one reconciling reader** over every finding in the round, which only looks for findings that cannot both be answered |
| `build` | **steers, and never widens.** It applies the *cover this* list to how the plan is written up in code and records the evidence each perspective needs. Anything a perspective wants that the plan did not describe becomes a tagged `Noticed: [name] …` line in the commit, not code |
| `verify` | **asks of the evidence.** By now the concerns the person took in are criteria in the spec. What verify adds is the evidence questions — was the abuse actually attempted, did the scan run on every touched page |

**Why steering as well as judging.** A perspective that only judges finds what it could have prevented,
and the loop pays a refine round for every finding it raises. A perspective that steers mostly finds
nothing.

**Why it never widens.** The spec may only derive from what the intent states. A list that arrives with
every cycle is not a person asking, so a criterion written from it alone is the scope creep that rule
exists to stop — and the first time it was tried, the rule blocked five criteria the list had put there.
What a perspective finds beyond the intent comes back as a suggestion, and the person takes it in by
naming it to `/sdlc:steer`, or leaves it for a later cycle.

## Reading what it found

**A perspective's finding blocks only when it cites a rule the perspective lists.** Everything else its
method turns up is a **suggestion**, exactly as any finding that cites no rule is. Look at a perspective's
rules with `sdlc perspective show <name> --for <stage>`; a perspective with none of its own can block on
nothing.

**Where its findings appear:**

- in a findings round, with an id prefixed by the perspective — `security.F_01` — and a `perspective`
  field naming it
- in a build's commit, as `Noticed: [security] the token is logged when the call fails.`
- in `sdlc suggestions`, with every other open non-blocking finding

**When two perspectives disagree**, the reconciling reader settles it by rule, not by preference:

| The pair | becomes |
|---|---|
| neither cites a rule | both stay suggestions, neither is applied, and each says it contradicts the other — so a person decides |
| one cites a rule, the other does not | the rule's finding stands; the other becomes a suggestion naming the rule it ran into |
| both cite rules | a conflict between the two rules, answered as any conflict is |

## How a perspective earns rules

**A perspective's suggestions are its record.** `sdlc propose constraints` counts each perspective by the
distinct cycles it raised something in, across the findings rounds and the builds' tagged `Noticed:`
lines, and marks one at three. That perspective is where to read first for a rule worth adding.

**The count is a pointer, not a candidate.** Three cycles of security suggestions may be three different
things. Only the same objection across three cycles is a rule worth proposing — and when one is adopted,
it is one of your solution's own constraints, with the perspective named in its evidence.
`../guides/handling-blockers.md` covers suggestions and `propose constraints` in general.

**A rule the framework adopts goes to the global set, and the perspective lists it.** Operability's
first is `CON_20`: a spec says what is recorded when the action fails. Two trial solutions raised it at
every stage as a suggestion, and both built the gap anyway. A rule never lives in a perspective's own
document, which only names the rules its reader may block on: the three tiers are every place a rule
can live.

**A recurring suggestion is not always a missing rule.** It may be something the author was never told
to cover. Accessibility's reader kept finding a missing visible focus indicator, missing reflow and
missing non-text contrast, because the perspective's list for authors named the scan and keyboard focus
and nothing the scan cannot see. That gap was closed in the perspective's method, not with a rule.

## Where to go next

| | |
|---|---|
| a blocking finding, and how a suggestion differs | `../guides/handling-blockers.md` |
| the stages a perspective steers and judges, as a person walks them | `../guides/running-a-cycle.md`, §3 |
| the interrogate, build and propose skills in depth — what each reader is handed | `../../skills/README.md` |
| the two commands as one line each | `../reference/commands.md` |
