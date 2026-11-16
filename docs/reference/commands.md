# Commands

Every command, grouped by when you run it, and whether the binary or an agent runs it. **One line each** —
the guides have the why.

`sdlc …` is the binary: deterministic, and it never writes a planning document. `/sdlc:…` is a skill:
an agent runs it, and it never decides a gate. `skills.md` lists the skills.

## Every command

| Command | Runs it | Does |
|---|---|---|
| **Once per machine** | | |
| `sdlc version` | binary | which version of the gate this is, and which versions it expects beside it. Inside a solution that pins another release, the pinned release answers this and every other command. With `SDLC_DEV_BUILD` set, that build answers, and calls itself `<release>-local` ([Testing a local build](../guides/testing-a-local-build.md)) |
| `sdlc claude [args]` | binary | starts Claude Code with the skills packed beside the binary, so in a pinned solution the session's skills are the pinned release's. Its arguments are Claude Code's |
| `sdlc help` | binary | every command and its options. `--help` after any command prints the same |
| `sdlc doctor` | binary | checks the install and this repository, and says how many decisions every reader is handed; names the fix for each problem, and before the first cycle names each draft to fill in and its rename. Says when adoption's files are a release behind the binary. Asks the platform, through `az` or `gh` signed in as you, whether branch policy requires the pipeline: a policy that does not is a problem, a CLI that is not signed in a warning. Exits 1 on a problem, a missing integration branch and a placeholder paragraph included. Never repairs |
| **Once per repository** | | |
| `sdlc init` | binary | adoption: commits what it writes, creates the integration branch, drafts the three files a person places. **Yours to run**; the hook denies it to an agent. `--integration-branch <name>` when it is not `development` |
| `sdlc draft solution` | binary | the solution document, to rename into place, once adoption's first draft is placed |
| `sdlc draft solution --standard profile` | binary | move new work onto the profile's conventions |
| `sdlc draft solution --approval none` | binary | stop requiring an approval before a build, or `required` to start again; `--coverage-floor 70%` moves the floor the same way |
| `sdlc draft techstack` | binary | the declared stack, to answer row by row — `none` where a row is not true — and rename into place. Adoption writes the first; this writes the next. Names what a later release added. `--empty` pins nothing |
| `sdlc constraints --new` | binary | an empty entry at the next free `LOC_` id, for a rule you have in mind. Writes nothing |
| **The cycle, in order** | | |
| `sdlc constraints` | binary | the rules in force, all tiers resolved |
| `sdlc constraints show <id>` | binary | one rule in full, in force or superseded |
| `sdlc areas` | binary | each area: the files it covers, the entries carrying it, and names nobody defined. Exits 1 when an area covers no file |
| `sdlc new` | binary | allocates the id, claims it across every worktree, and makes the directory; in a sandbox, the cycle opens beside the others. `/sdlc:intent` runs it for you |
| `/sdlc:intent` | agent | interviews you into `intent.md` |
| `/sdlc:interrogate <id> <artifact>` | agent, fresh process | judges one document, adds a round to `findings/<artifact>.json` |
| `/sdlc:refine <id> <artifact>` | agent | applies those findings to the document |
| `/sdlc:to-plan [<id>]` | agent | takes an intent to a plan — checks the intent, then spec, then plan, each with its loop — and drafts the approval. The step a person takes after an intent |
| `/sdlc:spec <id>` | agent | derives `spec.md` from the intent alone. `/sdlc:to-plan` runs it |
| `/sdlc:plan <id>` | agent | decides how, and what proves each criterion, in `plan.md`. `/sdlc:to-plan` runs it |
| `sdlc draft approval <id>` | binary | writes the approval draft. **You rename it** |
| `sdlc gate <id>` | binary | whether the build may run. Exits 1 if not |
| `sdlc stack` | binary | what the solution is built from, what this release ships that it never mentions, and the build settings not yet on disk |
| `sdlc scaffold <id>` | binary | the skeleton on a new solution's first cycle; otherwise the build settings the plan declares |
| `/sdlc:build [<id>]` | agent, own process | a playbook: asks the gate and runs `sdlc build`; in its own process, with `--unattended`, runs `/sdlc:implement`, `/sdlc:verify`, `/sdlc:remediate` while it converges, and `/sdlc:pr` for the named cycle or every approved one; hands back the report |
| `/sdlc:implement <id>` | agent | changes code and tests to do what it is handed — the plan, a verification's findings, a person's instruction — records deviations and choices, builds, tests, commits, and records the evidence. `/sdlc:build` and `/sdlc:remediate` run it |
| `sdlc evidence <id>` | binary | records the base commit, the commit the build is at, the test summary and the diff between the two, which is not committed; refuses uncommitted code outside `.sdlc/`; retires a blocked `verify.md`, a passed one only with `--retire-pass` when a person asked for a change, and refuses under any other; with `--coverage`, the changed lines' and the whole codebase's coverage against the baseline and the solution's `coverage_floor`; and the platform the tests ran on |
| `sdlc evidence check <id>` | binary | lists what `verify.md` cites that none of the recorded evidence holds, and tests it says passed that did not. Reports only, always exits 0. `/sdlc:verify` runs it |
| `/sdlc:verify <id>` | agent, fresh process | adjudicates from the evidence alone, writes `verify.md`. `/sdlc:build` and `/sdlc:remediate` run it |
| `/sdlc:remediate <id>` | agent | a playbook, *fix it*: on `blocked`, inside the build's process; after an escalation, with your instruction; or at the pull request, the change you ask for in words. Runs `/sdlc:implement` and `/sdlc:verify` while each pass converges, taking one different approach before it escalates, and `/sdlc:pr` updates the same pull request |
| `sdlc deviations <id>` | binary | where the build bent the approved documents, and the choices it made where they were silent: each one, any change you asked for, and anything wrong with it. Exits 1 while one misquotes the approved text, narrows a criterion, or takes a file another cycle claims |
| `sdlc build [<id>]` | binary | *build it*, from your checkout: commits the approval you placed if it is uncommitted, admits the plan when it is only in its sandbox, prepares the build's worktree, and starts `/sdlc:build --unattended` in a process of its own there — its tools named, permission checks on — which implements, verifies, remediates and opens the pull request; then prints the report and the link, or the escalation. With no id, every approved cycle in one process. The one stage name the binary runs rather than answers. `/sdlc:build` runs it (D-178, D-183, D-187) |
| `sdlc report <id>` | binary | the end-of-build report: every deviation, every choice the build made that nothing approved settled, and every concern its verification raised without blocking. The build hands it back, and it leads the pull request; merging accepts it |
| `sdlc pr-ready <id>` | binary | whether it may open a pull request. Exits 1 if not, and always for a test run: a cycle any round of which a local build wrote |
| `/sdlc:pr <id>` | agent | pushes and opens the pull request, or updates the one already open; the build and remediate run it for you. Asks first when you start it by hand |
| `sdlc verify-chain <id> --base <branch>` | binary | the pull-request check, and the only layer that cannot be bypassed. A plan alone gets the admission check; `slices/<slug>/plan` gets it for every cycle sliced together, with their successors and sibling plans checked; a branch that is no cycle's and changes only the pin, what `sdlc init` and `sdlc factory` write, answers in the declared stack and dependency versions gets the upgrade check |
| `sdlc tested <branch>` | binary | `true` when a promotion merges exactly that branch's tip and the runs the pipeline pipes in list a green one, so the pipeline builds and skips the tests |
| `sdlc admission <branch> --base <branch>` | binary | `true` when everything a pull request changes is under `.sdlc/` — a plan, a slicing's plans, a harvest, successors — so the pipeline skips the build and the tests; the chain still judges it |
| `sdlc promotion <source> --base <target>` | binary | `true` when a pull request promotes the integration branch or a `release/*` branch, so the pipeline skips the chain; exits 1 when any other branch opens one into the default branch |
| `sdlc branch-policy` | binary | the commands that set the default branch policy on GitHub or Azure DevOps: pull requests only, nobody bypassing, the pipeline required, squash into the integration branch and merge commits into the default branch. Applies nothing. `--approvals <n>` asks for reviewers, `--owner <who>` names who must approve, and `--self-approve` lets an author's vote count, which on GitHub is a team that may bypass on a pull request, `--self-approve <team>` |
| **At any point** | | |
| `sdlc status <id>` | binary | the panel for the document the cycle is waiting on, or its furthest one; the gate, with any recorded decision the cycle departs from beside it; and the literal next command, a stage in it named as the host invokes one (`/sdlc:pr` inside Claude Code) |
| `sdlc status <id> --brief` | binary | the same, between stages |
| `sdlc status <id> --stage spec` | binary | one artifact's panel: intent, spec, plan or verify |
| `sdlc findings <id>` | binary | what the interrogations said, each finding at the disposition that stands, with where it was found; `--blocking` for the open blockers alone, `--finding <fid>` for one |
| `sdlc harvest` | binary | the completed cycles not closed yet — merged after a passing verify, or abandoned — the aspects each still needs answered, the cycles still waiting to merge, and the next step. `/sdlc:harvest` runs it |
| `sdlc harvest <aspect>` | binary | one aspect — `constraints`, `decisions`, `suggestions`, `stack` or `areas` — for the completed cycles that have not answered it, ending with the proposal file to mark. `--cycles <id>,<id>` reads those alone and leaves the rest for a later sitting. The aspect's skill, `/sdlc:harvest-<aspect>`, runs it |
| `sdlc harvest read --dir <directory>` | binary | starts one fresh reader per completed cycle and document with findings, each grouping that document's findings by the problem underneath, and writes each answer beside its prompt. `--aspect` names the sitting's aspect, `constraints` unless given; `--cycles` its cycles. No skill starts a reader itself |
| `sdlc harvest review <aspect> --dir <directory>` | binary | starts the two fresh readers that review one aspect's sitting before its draft is placed: whether what was approved serves the evidence and the solution, and whether any item went unanswered. `--proposals <file>` names the marked file when it is not the newest |
| `sdlc draft harvest <file>` | binary | drafts of `constraints.md`, `decisions.md`, `techstack.md` and `areas.md` from the proposals marked `[x]`: copies with only those lines applied, refused if anything would go missing, a new entry is over its word budget, or an entry names an area nobody defines. Writes accepted successors to `.sdlc/successors/` |
| `sdlc harvest record <aspect> <file>` | binary | once the aspect's draft is placed: records in `.sdlc/harvested.json` that the cycles in the file's `## Cycles` have answered it, with the concerns under `## Watch` and `## Ignored` tallied, each with the `Why:` line under it, and the declines kept. Closes nothing; refuses while the draft is beside its file |
| `sdlc harvest close` | binary | closes every completed cycle whose five aspects are answered: records what later harvests need, and on a new `harvest/<date>` branch commits the removal of their folders with the ledger and the records you placed. Pushes nothing. Refuses, naming what each cycle still needs, while nothing can close, while a draft is beside its file, and anywhere but the integration branch. `/sdlc:close-cycle` runs it and opens the pull request |
| `sdlc harvest --history` | binary | the harvest ledger: closed cycles, each sitting with its aspect, the concerns watched below the bar, and the concerns ignored |
| `sdlc round <id> <artifact> --from <file>` | binary | appends the next round to the artifact's findings ledger, stamped, hashed and chained to the round before, with its suggestions in the cycle's suggestions ledger, and refuses it while anything above is unsettled, a known finding comes back in other words, a new blocker has no one-line gist or cites a rule not in force for it, a new finding quotes its rule in other words, a suggestion does not fit the cycle's, or the areas do not hold. `/sdlc:interrogate` runs it; `--changed` lists the sections moved since the last round |
| `sdlc round prompt <id> <artifact> --dir <directory>` | binary | writes every reader's prompt for one round: the block the rules reader and the perspectives share, once, as `shared.md`, and each reader's own part in its own file; `--reader` writes one, `--list` names them; a verify round adds the diff reader, which reads the change against the plan and the decisions, and a verify round after the cycle's first runs no perspective. `/sdlc:interrogate` and `/sdlc:verify` run it |
| `sdlc round run <id> <artifact\|verify> --dir <directory>` | binary | starts every reader whose prompt is in the directory and has no answer yet — the rules reader first, to warm the shared block, then the rest together — each in a fresh process outside the checkout, with no tools and no MCP servers, marked as a reader, and writes each answer beside its prompt. No skill starts a reader itself (D-183) |
| `sdlc round collect <id> <artifact> --dir <directory>` | binary | reads every reader's answer and drafts the round file: new ids numbered, restatements carried, each reader's usage beside them; lists the open findings no reader raised, for you to dispose of: `--answered <ids>` and `--withdrawn <ids>` copy them in from the ledger word for word. `/sdlc:interrogate` runs it |
| `sdlc round verdict <id> --from <file>` | binary | writes `verify.md` from the adjudicator's answer, byte for byte, and refuses one that is not a verify document. `/sdlc:verify` runs it |
| `sdlc constraints --for plan --brief` | binary | the rules one stage is judged against |
| `sdlc cycles` | binary | every cycle, newest first |
| `sdlc branch <id> <kind>` | binary | the git command that puts you on the cycle's `plan`, `build`, `build-unattended` or `forward` branch. The skills run it for you. `remediate` prints the path of a worktree on the branch the cycle's pull request is from, creating it if absent, where remediate works in your session. `sandbox` opens a worktree on `sandbox/<id>` where every cycle the work turns into is planned; `plan --worktree` opens the one cycle in a folder of its own. `plan` without a worktree refuses uncommitted work, and a local plan branch left over from earlier planning. `build` starts from the integration branch, the plan's branch or the admission branch carrying the plan, and refuses a plan only in a sandbox until it is admitted; `build-unattended` starts from a local admission branch too |
| `sdlc factory` | binary | writes the factory's pipeline for your platform, which a person starts. It never merges |
| `sdlc lead show <reference>` | binary | a work item, issue, suggestion, ready successor or file, in one shape for `intent --from` |
| `sdlc successors` | binary | every slice waiting on another's code, and whether it is waiting, ready, stuck or unresolved — worked out, never stored |
| `sdlc backlog` | binary | every successor and parked cycle, in the waves they come ready in, each with how to start it |
| `sdlc park <id>` | binary | the plan-time claim check: parks a plan that needs a file only another unbuilt plan creates, keeping its spec and plan. `/sdlc:plan` runs it |
| `sdlc unpark <id>` | binary | takes a parked cycle back once what it waits on has merged with a pass |
| `sdlc successor check` | binary | refuses a successor that cannot be read, names nothing that exists, or loops |
| `sdlc slices <slug> <id> <id>...` | binary | the git command that composes `slices/<slug>/plan` from the siblings' plan branches, so one pull request admits a slicing's plans. Refuses a plan that needs what only a sibling creates. `/sdlc:pr <id>... --plan` runs it |
| `sdlc admit` | binary | in a sandbox, the git command that takes its approved cycles and the successors it wrote onto an admission branch beside it; what is not approved stays. `sdlc build` and `/sdlc:pr --plan` run it |
| `sdlc discard` | binary | in a sandbox, what throwing it away would lose; prints the removal once nothing worth recording is left unrecorded |
| `sdlc tidy` | binary | the worktrees and local branches whose work has reached the integration branch, or whose cycle a harvest there has closed, and the commands that remove them, for you to run. Deletes nothing |
| `sdlc upgrade [<release>]` | binary | everything that moves to take a release, in order, with what each is on now: this machine's tool, the plugin, the solution's pin and what `sdlc init` writes, the factory's images, and the open cycles still pinning an older release. Answered by this machine's release, never the pinned one. Runs nothing (D-170) |
| `sdlc successor start <slug> <id>` | binary | in the new cycle's folder: refuses unless the successor is ready, then deletes its file there. `/sdlc:intent --from successor:<slug>` runs it |
| `sdlc operate --dry-run` | binary | what production raised, grouped and scrubbed, and what would be filed as a lead |
| `sdlc perspectives <id>` | binary | which perspectives a cycle gets, and why |
| `sdlc guidance [show <name> \| <id>]` | binary | the guidance a plan is handed: every entry and what switches it on, one in full, or the ones a cycle's plan declares files for. The solution's own under `.sdlc/guidance/` win by name. Advises; never blocks |
| `sdlc perspective show <name> --for <stage>` | binary | a perspective's method and questions for one stage; `--cover` for an author's list |
| `sdlc sitrep` | binary | the morning's page: ready to merge and in what order, needs a person, held back, going stale, and the backlog — successors and parked cycles ready, waiting or stuck, and what the page cannot see |
| `sdlc cycles --here [--for <step>]` | binary | which cycle a skill given no id means: the one this branch carries that is ready for the step, or the list to ask from. For `remediate`, a cycle `blocked`, `escalated`, or passed with its pull request open |
| `sdlc cycles --queued [<id>]` | binary | what a build would take now, in the order to build it, and what is held back and why; with an id, that cycle alone or why not |
| `sdlc cycles --claims` | binary | which files each cycle claims |
| `sdlc cycles --pr-ready` | binary | which cycles may open a pull request |
| `sdlc suggestions [<id>] [--all]` | binary | what was noticed and not acted on, each concern once; `--all` adds what became of each and which cycle decided it |
| `sdlc suggestions <id> --renumber` | binary | gives every suggestion id the cycle's ledger records twice — two branches each numbering a new one, and a merge keeping both — the next free id, the first keeping its own (D-171) |
| `sdlc harvest constraints` | binary | rules cited across three or more completed cycles, and what the findings argued |
| `sdlc decisions` | binary | what this solution already settled, naming the entries over the word budget. `--for <stage> --cycle <id>` gives every headline and, in full, only the entries in the areas that stage touches |
| `sdlc decisions show <id>` | binary | one decision in full, standing or superseded |
| `sdlc harvest decisions` | binary | choices completed cycles settled, as entries to adopt |
| `sdlc theme` | binary | the roles and values the solution's surfaces use, or a skeleton of roles for you to fill when it has none |
| `sdlc hook --host <name>` | binary, from a hook | judges a tool call, a turn that is ending, or a session starting on another release's skills, in the host's own shape. The plugin's copy passes `--source plugin`, and steps aside wherever the solution's own hooks answer the same event, so each check speaks once (D-172) |
| `sdlc settings` | binary | how this solution is set to run: each `solution.md` key, written or defaulted, and how to change it — `approval` among them; what this machine overrides; what this release fixes and the decision that fixes it. Points at `constraints`, `stack` and `decisions`. Writes nothing; exits 1 when `solution.md` cannot be read |
| **Stopping** | | |
| `sdlc draft approval <id> --reopen` | binary | withdraw an approval, with a reason, to change the documents. Once the plan is on the integration branch, refused on a build branch: reopen on `cycle/<id>/plan` |
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
    deviations.md          <- where the build bent the approved text, when it did (D-156)
    parked.md              <- a plan parked behind another's unbuilt code, while it is (D-145)
    verify.md
    findings/              <- one ledger per artifact, every round in it
        intent.json
        spec.json
        plan.json
        verify.json
        suggestions.json   <- the cycle's suggestions, each concern once
        usage.json         <- what each round's readers cost (D-121)
    evidence/
        base.sha
        head.sha           <- the commit the build was recorded at
        diff.patch         <- not committed: rebuilt from the two commits (D-124)
        proof-sources.txt  <- not committed: the files a proof rests on outside the change, when there are any (D-168)
        tests.json         <- the normalised summary. The only one verify reads
        timings.json       <- how long the run took and its slowest tests. No reader is handed it (D-169)
        coverage.json      <- the changed lines' and the whole codebase's coverage, with --coverage
        tests.trx          <- the runner's own results; tests.1.trx and on when there are several. Not committed
        build.log          <- not committed: the pipeline publishes its own (D-92)
```

`approval.<decision>.draft.md` sits beside the ledger until somebody renames or deletes it.
`abandoned.md` appears only on a stopped cycle.

## See also

- [Running a cycle](../guides/running-a-cycle.md) — a cycle walked end to end
- [Rolling a d6](../samples/rolling-a-die.md) — that walk with one solution in it
- [Refusals](refusals.md) — what to do when something refuses
- [Skills](skills.md), and [the skills reference](../../skills/README.md) — what each skill is, in depth
