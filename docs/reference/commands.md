# Commands

Every command, grouped by when you run it, and whether the binary or an agent runs it. **One line each** —
the guides have the why.

`sdlc …` is the binary: deterministic, and it never writes a planning document. `/sdlc:…` is a skill:
an agent runs it, and it never decides a gate. `skills.md` lists the skills.

## Every command

| Command | Runs it | Does |
|---|---|---|
| **Once per machine** | | |
| `sdlc version` | binary | which version of the gate this is, and which versions it expects beside it |
| `sdlc help` | binary | every command and its options. `--help` after any command prints the same |
| `sdlc doctor` | binary | checks the install and this repository; names the fix for each problem, and before the first cycle names each draft to fill in and its rename. Says when adoption's files are a release behind the binary. Exits 1 on a problem, a missing integration branch and a placeholder paragraph included. Never repairs |
| **Once per repository** | | |
| `sdlc init` | binary | adoption: commits what it writes, creates the integration branch, drafts the three files a person places. **Yours to run**; the hook denies it to an agent. `--integration-branch <name>` when it is not `development` |
| `sdlc draft solution` | binary | the solution document, to rename into place, once adoption's first draft is placed |
| `sdlc draft solution --standard profile` | binary | move new work onto the profile's conventions |
| `sdlc draft techstack` | binary | the declared stack, to answer row by row — `none` where a row is not true — and rename into place. Adoption writes the first; this writes the next. Names what a later release added. `--empty` pins nothing |
| `sdlc propose constraints` | binary | the next free `LOC_` id, or rules the findings keep citing |
| **The cycle, in order** | | |
| `sdlc constraints` | binary | the rules in force, all tiers resolved |
| `sdlc new` | binary | allocates the id and the directory; refuses over uncommitted work. `/sdlc:intent` runs it for you |
| `/sdlc:intent` | agent | interviews you into `intent.md` |
| `/sdlc:interrogate <id> <artifact>` | agent, fresh process | judges one document, writes `findings/<artifact>.rN.json` |
| `/sdlc:refine <id> <artifact>` | agent | applies those findings to the document |
| `/sdlc:spec <id>` | agent | derives `spec.md` from the intent alone |
| `/sdlc:plan <id>` | agent | decides how, and what proves each criterion, in `plan.md` |
| `sdlc draft approval <id>` | binary | writes the approval draft. **You rename it** |
| `sdlc gate <id>` | binary | whether the build may run. Exits 1 if not |
| `sdlc stack` | binary | what the solution is built from, what this release ships that it never mentions, and the build settings not yet on disk |
| `sdlc scaffold <id>` | binary | the skeleton on a new solution's first cycle; otherwise the build settings the plan declares |
| `/sdlc:build <id>` | agent | implements, builds, tests, records the evidence |
| `sdlc evidence <id>` | binary | records the diff, the base commit and the test summary |
| `/sdlc:verify <id>` | agent, fresh process | adjudicates from the evidence alone, writes `verify.md` |
| `/sdlc:remediate <id>` | agent | only on `blocked`; runs while each pass reduces what is open |
| `sdlc pr-ready <id>` | binary | whether it may open a pull request. Exits 1 if not |
| `/sdlc:pr <id>` | agent | pushes and opens the pull request. Asks first |
| `sdlc verify-chain <id> --base <branch>` | binary | the pull-request check, and the only layer that cannot be bypassed. A plan alone gets the admission check |
| `sdlc admission <id> --base <branch>` | binary | `true` when a pull request carries only a plan, so the pipeline skips the build and the tests |
| **At any point** | | |
| `sdlc status <id>` | binary | the panel for the document the cycle is waiting on, or its furthest one; the gate; and the literal next command, a stage in it named as the host invokes one (`/sdlc:pr` inside Claude Code) |
| `sdlc status <id> --brief` | binary | the same, between stages |
| `sdlc status <id> --stage spec` | binary | one artifact's panel: intent, spec, plan or verify |
| `sdlc findings <id>` | binary | what the interrogations said, each finding at the disposition that stands |
| `sdlc constraints --for plan --brief` | binary | the rules one stage is judged against |
| `sdlc cycles` | binary | every cycle, newest first |
| `sdlc branch <id> <kind>` | binary | the git command that puts you on the cycle's `plan`, `build`, `build-unattended` or `forward` branch. The skills run it for you |
| `sdlc factory` | binary | writes the factory's nightly pipeline for your platform. It never merges |
| `sdlc lead show <reference>` | binary | a work item, issue, suggestion or file, in one shape for `intent --from` |
| `sdlc operate --dry-run` | binary | what production raised, grouped and scrubbed, and what would be filed as a lead |
| `sdlc perspectives <id>` | binary | which perspectives a cycle gets, and why |
| `sdlc perspective show <name> --for <stage>` | binary | a perspective's method and questions for one stage; `--cover` for an author's list |
| `sdlc sitrep` | binary | the morning's page: ready to merge and in what order, needs a person, held back, going stale |
| `sdlc cycles --queued` | binary | what a build would take now, and what is held back and why |
| `sdlc cycles --claims` | binary | which files each cycle claims |
| `sdlc cycles --pr-ready` | binary | which cycles may open a pull request |
| `sdlc suggestions` | binary | what was noticed and not acted on |
| `sdlc propose constraints` | binary | rules cited across three or more cycles |
| `sdlc decisions` | binary | what this solution already settled |
| `sdlc propose decisions` | binary | choices approved cycles settled, as entries to adopt |
| `sdlc theme` | binary | the roles and values the solution's surfaces use |
| `sdlc propose theme` | binary | the roles a theme declares, for you to fill |
| **Stopping** | | |
| `sdlc draft approval <id> --reopen` | binary | withdraw an approval, with a reason, to change the documents |
| `sdlc draft abandon <id>` | binary | stop the cycle for good. **You place it**. Terminal |

In the cycle, the interrogate-then-refine loop repeats until a round raises nothing blocking —
`../guides/handling-blockers.md`.

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

## See also

- [Running a cycle](../guides/running-a-cycle.md) — a cycle walked end to end
- [Rolling a d6](../samples/rolling-a-die.md) — that walk with one solution in it
- [Refusals](refusals.md) — what to do when something refuses
- [Skills](skills.md), and [the skills reference](../../skills/README.md) — what each skill is, in depth
