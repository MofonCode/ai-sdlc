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
2. **Take it to plan** — *take it to plan*, or `/sdlc:to-plan`. Spec, then plan, each checked by its own readers, and one hand-back. **Read the plan closely**; to have it otherwise, *in that plan, make it … instead* (`/sdlc:to-plan` runs `steer`, then checks again)
3. **Approve** — it drafts the approval; read it and **rename it into place yourself**. That rename is the approval, skipped when the solution declares `approval: none`
4. **Build it** — *build it*, or `/sdlc:build`. In a process of its own: builds, verifies, fixes what it can, opens the pull request, and reports every deviation, choice and concern
5. **Review the pull request** — merging accepts it, report and all; to change anything, say what instead (`/sdlc:remediate`), and the same pull request updates

## When it stops

- **Where am I?** `sdlc status <id>` — names the next step
- **What did the build do on its own?** `sdlc report <id>` — every deviation, choice and concern; it leads the pull request
- **A blocking finding** the loop could not clear, or **remediation escalated** — `guides/handling-blockers.md`; after an escalation, say what to do, then *fix it* (`/sdlc:remediate`)
- **The intent is two things**, or two are one, before a spec — `/sdlc:intent --reslice <id>...`
- **The plan is wrong** after approval — *steer the plan*: it drafts the reopen and walks the rest
- **After the merge** — `/sdlc:harvest` takes back what completed cycles settled, one aspect at a time, then closes them — `guides/harvesting-the-cycles.md`; `sdlc tidy` for the worktrees and branches left over
- **A successor is ready** — `/sdlc:intent --from successor:<slug>`; `sdlc backlog` lists them, with parked cycles
- **Merges landed** — `/sdlc:next` starts what they made ready, plans it in one sandbox, and stops at the approvals
- **A plan was parked** — it waits on another's code; `sdlc unpark <id>` once that merged, then *take it to plan* again
- **Something refused** — `reference/refusals.md`
- **Stop for good** — `sdlc draft abandon <id>`, and place it yourself
