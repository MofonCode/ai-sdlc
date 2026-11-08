# Cheat sheet

One cycle, one line per step. The why is in `guides/running-a-cycle.md`; every command is in
`reference/commands.md`.

**Before the first one:** the repository is adopted and its drafts are placed and committed —
`guides/adopting-the-framework.md`. `sdlc doctor` exits 0. **With a remote**, the pipeline's check is
required in branch policy — `sdlc branch-policy` prints the commands and a person runs them; until then nothing
mechanical gates a merge.

## The cycle

- `/sdlc:intent` — slices bigger work into atomic cycles once you accept, then opens each and interviews you one question at a time
- `/sdlc:spec <id>` — testable criteria, from the intent alone
- `/sdlc:plan <id>` — how, and what proves each criterion. **Read it closely**
- or `/sdlc:batch-plan` — spec then plan for every intent the working copy carries, handing back once
- `sdlc draft approval <id>`, then **rename the draft yourself** — that rename is the approval. Skipped when the solution declares `approval: none`
- `/sdlc:pr <id> --plan` — only with an integration branch: admits the plan before it is built; a slicing's plans together with `/sdlc:pr <id> <id>... --plan`
- `/sdlc:build <id>` — implements, tests, records the evidence
- `/sdlc:verify <id>` — adjudicates from the evidence alone
- `/sdlc:propose convergence` — optional, after a pass: rules, decisions and stack the cycle settled, as your own commit. Or after the merge
- `/sdlc:pr <id>` — pushes and opens the pull request. Asks first

Each document stage runs its own interrogate-and-refine loop before it hands back. **Run each step in a new session**

## When it stops

- **Where am I?** `sdlc status <id>` — prints the next command
- **A successor is ready** — `/sdlc:intent --from successor:<slug>`; `sdlc backlog` lists them, with parked cycles
- **Merges landed** — `/sdlc:next` starts what they made ready, plans it in one sandbox, and stops at the approvals
- **A plan was parked** — it waits on another's code; `sdlc unpark <id>` once that merged, then interrogate its spec and plan again
- **A blocking finding** the loop could not clear — `guides/handling-blockers.md`
- **Verify says `blocked`** — `/sdlc:remediate <id>`, then verify again
- **Changed your mind** before approval — `/sdlc:steer <id> "..."`
- **The intent is two things**, or two are one, before a spec — `/sdlc:intent --reslice <id>...`
- **Take in a suggestion** a reader raised, before approval — `/sdlc:steer <id> <finding id>...`
- **After approval** — `sdlc draft approval <id> --reopen`; once the plan is on `development`, on its plan branch (`sdlc branch <id> plan`), then `/sdlc:pr <id> --plan`
- **Something refused** — `reference/refusals.md`
- **Stop for good** — `sdlc draft abandon <id>`, and place it yourself
