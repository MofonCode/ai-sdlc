# Cheat sheet

One cycle, one line per step. The why is in `guides/running-a-cycle.md`; every command is in
`reference/commands.md`.

**Before the first one:** the repository is adopted and its drafts are placed and committed —
`guides/adopting-the-framework.md`. `sdlc doctor` exits 0.

## The cycle

- `/sdlc:intent` — opens the cycle and its branch, interviews you one question at a time
- `/sdlc:spec <id>` — testable criteria, from the intent alone
- `/sdlc:plan <id>` — how, and what proves each criterion. **Read it closely**
- `sdlc draft approval <id>`, then **rename the draft yourself** — that rename is the approval
- `/sdlc:pr <id> --plan` — only with an integration branch: admits the plan before it is built
- `/sdlc:build <id>` — implements, tests, records the evidence
- `/sdlc:verify <id>` — adjudicates from the evidence alone
- `/sdlc:pr <id>` — pushes and opens the pull request. Asks first

Each document stage runs its own interrogate-and-refine loop before it hands back.

## When it stops

- **Where am I?** `sdlc status <id>` — prints the next command
- **A blocking finding** the loop could not clear — `guides/handling-blockers.md`
- **Verify says `blocked`** — `/sdlc:remediate <id>`, then verify again
- **Changed your mind** before approval — `/sdlc:steer <id> "..."`
- **After approval** — `sdlc draft approval <id> --reopen`
- **Something refused** — `reference/refusals.md`
- **Stop for good** — `sdlc draft abandon <id>`, and place it yourself
