# Cheat sheet

One cycle, one line per step. The why is in `guides/running-a-cycle.md`; every command is in
`reference/commands.md`.

**Before the first one:** the repository is adopted and its drafts are placed and committed —
`guides/adopting-the-framework.md`. `sdlc doctor` exits 0. **With a remote**, the pipeline's check is
required in branch policy — `sdlc branch-policy` prints the commands and a person runs them; until then nothing
mechanical gates a merge.

## The cycle

Each step can be said in a sentence; the slash form is there if you prefer it. Every hand-back ends by offering
the next one.

1. **Begin an intent** — say what you want to add, fix or change, or `/sdlc:intent`. It slices bigger work into atomic cycles once you accept, then interviews you one question at a time
2. **Take it to plan** — *take it to plan*, or `/sdlc:to-plan`. Spec, then plan, each checked by its own readers, and one hand-back. **Read the plan closely**
3. **Steer the plan** — *in that plan, make it … instead*, or `/sdlc:steer`. Carried through the intent, spec and plan, before you approve
4. **Approve** — it drafts the approval; read it and **rename it into place yourself**. That rename is the approval. Skipped when the solution declares `approval: none`
5. **Build it** — in a new session, *build it*, or `/sdlc:build`. Implements, tests, records the evidence, and verifies from the evidence alone
6. **Fix it** — on `blocked`, *fix it*, or `/sdlc:remediate`. Goes round until it passes, checking in every fourth pass
7. **Open the pull request** — *open the pull request*, or `/sdlc:pr`. Asks first, and whether to mine the cycle now or after the merge

## When it stops

- **Where am I?** `sdlc status <id>` — names the next step
- **With an integration branch** — admit the approved plan before building: `/sdlc:pr <id> --plan`; a slicing's plans together with `/sdlc:pr <id> <id>... --plan`
- **The build bent the plan** — `sdlc deviations <id>`, then answer each with `sdlc draft approval <id>`; `guides/handling-blockers.md`
- **A blocking finding** the loop could not clear, or **remediation escalated** — `guides/handling-blockers.md`
- **The intent is two things**, or two are one, before a spec — `/sdlc:intent --reslice <id>...`
- **The plan is wrong** after approval — answer every deviation, then *steer the plan*: it drafts the reopen and walks the rest
- **After the merge** — `/sdlc:mine`, if not mined before; `sdlc mine record <file>` once its drafts are placed; `sdlc tidy` for the worktrees and branches left over
- **A successor is ready** — `/sdlc:intent --from successor:<slug>`; `sdlc backlog` lists them, with parked cycles
- **Merges landed** — `/sdlc:next` starts what they made ready, plans it in one sandbox, and stops at the approvals
- **A plan was parked** — it waits on another's code; `sdlc unpark <id>` once that merged, then *take it to plan* again
- **Something refused** — `reference/refusals.md`
- **Stop for good** — `sdlc draft abandon <id>`, and place it yourself
