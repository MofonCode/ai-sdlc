# Every command for one cycle

One screen. What each one is, and whether the binary or an agent runs it.

**A command is deterministic and the binary owns it; a skill needs language and an agent runs it.** The
binary never writes a planning document and a skill never decides a gate. `../../skills/README.md` has
what each skill is and who starts it; this is the order they go in.

## Once per machine

```pwsh
sdlc version
```

## When something is not working

```pwsh
sdlc doctor
```

Checks this installation and this repository: the rules, git, adoption, every host's hook configuration,
and whether a pipeline here runs the one check that is the guarantee. **Exits 1 if anything is wrong**,
so it is usable in a script, and every problem it prints names the command that fixes it.

**It reports and never repairs.** The remedies are commands a person runs — adoption especially, which
the hook denies to an agent.

Two things it marks `elsewhere` rather than passing or failing, because they are not a binary's to
answer: whether a host actually loaded the skills, and whether the pre-push hook is installed in this
clone. Both are named with who to ask.

## Once per repository

```pwsh
sdlc init
```

In a repository that already holds code, one option is worth knowing before you run it:
`--adopt-standard` holds new work to the profile's conventions rather than to the code already there. It
writes the stack files an adopted solution otherwise never gets — `.editorconfig`, both props files,
`global.json` — and records `standard: profile`. It overwrites nothing and migrates nothing; the cost is
that `Directory.Build.props` makes warnings errors, so the next build can fail on files nobody has
touched. `sdlc help` has the whole of it.

Then, in your agent: `/sdlc:init` for the half a command cannot do — the interview about what matters in
this solution, and the `LOC_` rules it proposes.

## The cycle, in order

| | What runs it | What it does |
|---|---|---|
| `sdlc constraints` | binary | the rules in force, all tiers resolved |
| `sdlc new` | binary | allocates the id, creates the directory, names the branch |
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
| `sdlc scaffold <id>` | binary | the solution skeleton, on a new solution's first cycle only. Refuses unless the gate passes |
| `/sdlc:build <id>` | agent | implements, builds, tests |
| `sdlc evidence <id>` | binary | records the diff, the base commit, the test summary |
| `/sdlc:verify <id>` | agent, fresh process | adjudicates from the evidence alone, writes `verify.md` |
| `/sdlc:remediate <id>` | agent | only when verify wrote `blocked`. At most three passes |
| `sdlc landable <id>` | binary | whether it may be landed. Exits 1 if not |
| `/sdlc:land <id>` | agent | pushes and opens the pull request. Asks first |
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
sdlc cycles
sdlc cycles --claims
sdlc cycles --landable
```

`status --brief` is the between-stages form: the cycle, its counts, what the artifact says, and the next
command. `findings <id>` is what the interrogations said, per artifact, each finding at the disposition
that stands. `cycles` lists everything this solution holds, newest first — `--claims` and `--landable`
are the two questions that used to be their own verbs.

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
        tests.json
```

## Where to go next

| | |
|---|---|
| installing both halves | `install.md` |
| a cycle walked end to end, with what to expect | `first-cycle.md` |
| what to do when something refuses | `when-it-blocks.md` |
| what each skill is and who starts it | `../../skills/README.md` |
