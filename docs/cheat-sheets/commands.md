# Commands

Every command for one cycle, in order, and whether the binary or an agent runs it. **One line each** —
the guides have the why.

`sdlc …` is the binary: deterministic, and it never writes a planning document. `/sdlc:…` is a skill:
an agent runs it, and it never decides a gate. `skills.md` lists the skills.

## The short path

A repository nobody has adopted, to an open pull request.

Adopt it, once — `../guides/initializing-the-codebase.md`:

1. `git init` — and **create the remote first** if the code will live on one
2. `/sdlc:init` — asks where the code lives, then hands you the adoption command to run yourself
3. **make the pipeline's check required** in branch policy
4. `sdlc draft solution`, then `sdlc draft techstack` — rename each into place, in the order
   `sdlc doctor` names them. **`sdlc new` refuses until the declared stack is placed**
5. back to `/sdlc:init` for the interview and the `LOC_` rules it proposes

Then the cycle — `../guides/running-a-cycle.md`:

6. `sdlc constraints`
7. `/sdlc:intent <the problem in a sentence>` — opens the cycle and runs its own loop
8. `git switch -c cycle/<id>` — the `cycle/` prefix is load-bearing
9. `/sdlc:spec <id>`
10. `/sdlc:plan <id>` — the last document before you approve; read it closely
11. `sdlc draft approval <id>`, **rename the draft yourself**, then `sdlc gate <id>`
12. `/sdlc:build <id>`
13. `/sdlc:verify <id>`, and `/sdlc:remediate <id>` only where it wrote `blocked`
14. `/sdlc:pr <id>` — asks before it pushes

`sdlc status <id>` between any two prints the next command. `/sdlc:steer <id> "..."` is a change of mind
before step 11.

## Once per machine

| | |
|---|---|
| `sdlc version` | which version of the gate this is, and which versions it expects beside it |
| `sdlc doctor` | checks the install and this repository; names the fix for each problem, and before the first cycle names what to place next. Exits 1 on a problem. Never repairs |

## Once per repository

| | |
|---|---|
| `sdlc init` | adoption. **Yours to run**; the hook denies it to an agent |
| `sdlc draft solution` | the solution document, to rename into place |
| `sdlc draft solution --standard profile` | move new work onto the profile's conventions |
| `sdlc draft techstack` | the declared stack, to cut down and rename into place. `--empty` pins nothing |
| `sdlc propose constraints` | the next free `LOC_` id, or rules the findings keep citing |

## The cycle, in order

| | Runs it | Does |
|---|---|---|
| `sdlc constraints` | binary | the rules in force, all tiers resolved |
| `sdlc new` | binary | allocates the id and the directory. `/sdlc:intent` runs it for you |
| `/sdlc:intent` | agent | interviews you into `intent.md` |
| `/sdlc:interrogate <id> <artifact>` | agent, fresh process | judges one document, writes `findings/<artifact>.rN.json` |
| `/sdlc:refine <id> <artifact>` | agent | applies those findings to the document |
| `/sdlc:spec <id>` | agent | derives `spec.md` from the intent alone |
| `/sdlc:plan <id>` | agent | decides how, and what proves each criterion, in `plan.md` |
| `sdlc draft approval <id>` | binary | writes the approval draft. **You rename it** |
| `sdlc gate <id>` | binary | whether the build may run. Exits 1 if not |
| `sdlc stack` | binary | what the solution is built from, and the build settings not yet on disk |
| `sdlc scaffold <id>` | binary | the skeleton on a new solution's first cycle; otherwise the build settings the plan declares |
| `/sdlc:build <id>` | agent | implements, builds, tests, records the evidence |
| `sdlc evidence <id>` | binary | records the diff, the base commit and the test summary |
| `/sdlc:verify <id>` | agent, fresh process | adjudicates from the evidence alone, writes `verify.md` |
| `/sdlc:remediate <id>` | agent | only on `blocked`; runs while each pass reduces what is open |
| `sdlc pr-ready <id>` | binary | whether it may open a pull request. Exits 1 if not |
| `/sdlc:pr <id>` | agent | pushes and opens the pull request. Asks first |
| `sdlc verify-chain <id>` | binary | the pull-request check, and the only layer that cannot be bypassed |

The interrogate-then-refine loop repeats until a round raises nothing blocking —
`../guides/handling-blockers.md`.

## At any point

| | |
|---|---|
| `sdlc status <id>` | the stage panel, the gate, and the literal next command |
| `sdlc status <id> --brief` | the same, between stages |
| `sdlc status <id> --stage spec` | one artifact's panel |
| `sdlc findings <id>` | what the interrogations said, each finding at the disposition that stands |
| `sdlc constraints --for plan --brief` | the rules one stage is judged against |
| `sdlc cycles` | every cycle, newest first |
| `sdlc cycles --claims` | which files each cycle claims |
| `sdlc cycles --pr-ready` | which cycles may open a pull request |
| `sdlc suggestions` | what was noticed and not acted on |
| `sdlc propose constraints` | rules cited across three or more cycles |
| `sdlc decisions` | what this solution already settled |
| `sdlc propose decisions` | choices approved cycles settled, as entries to adopt |
| `sdlc theme` | the roles and values the solution's surfaces use |
| `sdlc propose theme` | the roles a theme declares, for you to fill |

## Stopping

| | |
|---|---|
| `sdlc draft approval <id> --reopen` | withdraw an approval, with a reason, to change the documents |
| `sdlc draft abandon <id>` | stop the cycle for good. **You place it**. Terminal |

## What a cycle leaves on disk

```
.sdlc/cycles/20260929_1456_32/
    intent.md
    spec.md
    plan.md
    approval.md            <- only a person writes this
    verify.md
    findings/
        intent.r1.json
        intent.r2.json
        spec.r1.json
        plan.r1.json
    evidence/
        base.sha
        diff.patch
        tests.json         <- the normalised summary. The only one verify reads
        tests.1.trx        <- one per test project, as the runner wrote them
        build.log
```

`approval.<decision>.draft.md` sits beside the ledger until somebody renames or deletes it.
`abandoned.md` appears only on a stopped cycle.

## Where to go next

| | |
|---|---|
| a cycle walked end to end | `../guides/running-a-cycle.md` |
| that walk with one solution in it | `../samples/rolling-a-die.md` |
| what to do when something refuses | `refusals.md` |
| what each skill is, in depth | `../../skills/README.md` |
