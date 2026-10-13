# Every command for one cycle

One screen. What each one is, and whether the binary or an agent runs it.

**A command is deterministic and the binary owns it; a skill needs language and an agent runs it.** The
binary never writes a planning document and a skill never decides a gate. `../../skills/README.md` has
what each skill is and who starts it; this is the order they go in.

## The short path

A repository nobody has adopted, to an open pull request. **Everything below this section is the detail
behind one of these lines**, and `first-cycle.md` is the same walk with what each stage refuses.

Adopt it, once:

1. `git init` — and **create the remote first** if the code will live on one. Adopting before one exists
   costs a hand-placed platform key later, because `solution.md` is protected by then
2. `/sdlc:init`, which asks the one thing the command cannot guess — where the code will live — and
   then hands you the adoption command to run yourself
3. **make the pipeline's check required** in branch policy. Until it is required, the one guarantee is
   advisory
4. `sdlc draft solution`, then `sdlc draft techstack` — place both **before** the first cycle, in the
   order adoption's report and `sdlc doctor` name them, one at a time. A plan that
   reads *Replace this with a paragraph a newcomer could read* has been told nothing, and **`sdlc new`
   refuses until the declared stack is placed**: adoption does not write it, the draft is the
   framework's default for you to cut down, and `--empty` drafts one that pins nothing
5. back to `/sdlc:init` for the interview and the `LOC_` rules it proposes

Then the cycle:

6. `sdlc constraints`
7. `/sdlc:intent <the problem in a sentence>` — it allocates the cycle itself and runs its own
   interrogate-refine loop
8. `git switch -c cycle/<id>` — the `cycle/` prefix is load-bearing
9. `/sdlc:spec <id>`, then its loop
10. `/sdlc:plan <id>`, then its loop. The last document before you approve, so the one to read closely
11. `sdlc draft approval <id>`, **rename the draft onto the ledger yourself**, then `sdlc gate <id>`
12. `/sdlc:build <id>` — scaffolds on a new solution's first cycle, implements, tests, records the
    evidence
13. `/sdlc:verify <id>`, and `/sdlc:remediate <id>` only where it wrote `blocked`
14. `/sdlc:pr <id>`, which asks before it pushes

`sdlc status <id>` between any two of these prints the next command, and `/sdlc:steer <id> "..."` is the
change of mind before 11.

## Once per machine

```pwsh
sdlc version
```

## When something is not working

```pwsh
sdlc doctor
```

Checks this installation and this repository: the rules, git, adoption, the declared stack, every
host's hook configuration, and whether a pipeline here runs the one check that is the guarantee. **Exits 1 if anything is wrong**,
so it is usable in a script, and every problem it prints names the command that fixes it.

**It reports and never repairs.** The remedies are commands a person runs — adoption especially, which
the hook denies to an agent.

**Before the first cycle it also says what now.** It ends with what a person still has to place — the
paragraph in `solution.md`, then the declared stack — and names the one command that is next, the same
block adoption's report ends with. Once both are placed it names the first cycle, and once a cycle
exists it says nothing: `sdlc status <id>` names the next command from there.

One thing it marks `elsewhere` rather than passing or failing, because it is not a binary's to
answer: whether a host actually loaded the skills. It is named with who to ask. On a local solution
the guarantee is marked the same way — there is no pipeline, by choice, and nothing mechanical decides
what reaches the default branch.

## Once per repository

```pwsh
sdlc init
```

It decides nothing about the stack. In a repository that already holds code, two choices used to ride
on one option here and each has its own home now:

| To | Run |
|---|---|
| hold new work to the profile's conventions rather than to the code already there | `sdlc draft solution --standard profile`, and rename the draft |
| take the .NET build settings — `.editorconfig`, the three props files, `global.json` | `sdlc draft techstack`, set those rows to `true`, and rename the draft. A cycle whose plan declares the files brings them |

Neither migrates the code already there. `the-stack.md` has what each file costs.

Then, in your agent: `/sdlc:init` for the half a command cannot do — the interview about what matters in
this solution, and the `LOC_` rules it proposes.

## The cycle, in order

| | What runs it | What it does |
|---|---|---|
| `sdlc constraints` | binary | the rules in force, all tiers resolved |
| `sdlc new` | binary | allocates the id, creates the directory, names the branch. Refuses while no declared stack is placed |
| `/sdlc:intent` | agent | interviews you into `intent.md` |
| `/sdlc:interrogate <id> intent` | agent, fresh process | writes `findings/intent.r1.json` |
| `/sdlc:refine <id> intent` | agent | applies those findings to `intent.md` |
| `/sdlc:spec <id>` | agent | derives `spec.md` from the intent alone |
| `/sdlc:interrogate <id> spec` | agent, fresh process | `findings/spec.r1.json` |
| `/sdlc:refine <id> spec` | agent | applies them to `spec.md` |
| `/sdlc:plan <id>` | agent | decides how, and what proves each criterion, in `plan.md` |
| `/sdlc:interrogate <id> plan` | agent, fresh process | `findings/plan.r1.json` |
| `/sdlc:refine <id> plan` | agent | applies them to `plan.md` |
| `sdlc draft approval <id>` | binary | writes `approval.<decision>.draft.md`. **You rename it** |
| `sdlc gate <id>` | binary | whether the build may run. Exits 1 if not |
| `sdlc stack` | binary | what the solution declares it is built from, and which build settings it takes that are not on disk. The plan stage reads it |
| `sdlc scaffold <id>` | binary | the solution skeleton, on a new solution's first cycle; on an adopted one, only the build settings the plan declares. Refuses unless the gate passes |
| `/sdlc:build <id>` | agent | implements, builds, tests |
| `sdlc evidence <id>` | binary | records the diff, the base commit, the test summary |
| `/sdlc:verify <id>` | agent, fresh process | adjudicates from the evidence alone, writes `verify.md` |
| `/sdlc:remediate <id>` | agent | only when verify wrote `blocked`. Runs while each pass reduces what is open |
| `sdlc pr-ready <id>` | binary | whether it may open a pull request. Exits 1 if not |
| `/sdlc:pr <id>` | agent | pushes and opens the pull request. Asks first |
| `sdlc verify-chain <id>` | binary | the pull-request check. The only non-bypassable layer |

The interrogate-refine loop repeats until a round raises nothing blocking. Round numbers climb;
`findings/intent.r2.json` sits beside `r1` and neither is rewritten.

## At any point

```pwsh
sdlc status <id>
```

The stage panel, its checks, the gate, and the literal next command. Without `--stage` it shows whichever
artifact the cycle is waiting on.

```pwsh
sdlc status <id> --stage spec
sdlc status <id> --brief
sdlc constraints --for plan --brief
sdlc findings <id>
sdlc suggestions
sdlc propose constraints
sdlc decisions
sdlc theme
sdlc stack
sdlc cycles
sdlc cycles --claims
sdlc cycles --pr-ready
```

`status --brief` is the between-stages form: the cycle, its counts, what the artifact says, and the next
command. `findings <id>` is what the interrogations said, per artifact, each finding at the disposition
that stands. `cycles` lists everything this solution holds, newest first — `--claims` and `--pr-ready`
are the two questions that used to be their own verbs.

**The last four belong to the solution rather than to a cycle.** `suggestions` is what was raised and
never acted on. `propose constraints` reads the whole history and says which rules the findings keep
citing — recurrence across cycles is its entire signal, so there is nothing to ask it about one. Both
print and keep a copy under `.sdlc/reports/`, which nothing reads back.

`decisions` and `theme` are the other two records a stage reads: one so it does not re-decide something
already argued, the other so it does not invent a colour. **Only the constraints bind** — a plan may
depart from a decision or a theme and break no rule. What reading them buys is that the departure is
taken knowingly. `sdlc propose decisions` and `sdlc propose theme` are how entries arrive.

## Stopping

```pwsh
sdlc draft abandon <id>
```

Prints the record. **You place it**, and the reason is left empty because the file is rejected without
one. Terminal.

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
        tests.1.trx        <- one per test project, kept as the runner wrote them
        build.log
```

Two more names appear only where something happened. `approval.<decision>.draft.md` sits beside the
ledger until somebody renames it or deletes it — a draft nobody renamed records nothing, including a
`denied` one. And `abandoned.md` replaces the rest of the cycle's future: it is terminal, and it is a
person's file like the approval.

## Where to go next

| | |
|---|---|
| installing both halves | `install.md` |
| a cycle walked end to end, with what to expect | `first-cycle.md` |
| that walk with one solution in it, from install to a pull request | `dice-worked-example.md` |
| what to do when something refuses | `when-it-blocks.md` |
| what this solution is built from, and how to change it | `the-stack.md` |
| what each skill is and who starts it | `../../skills/README.md` |
