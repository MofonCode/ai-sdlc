---
name: plan
description: Decide how a spec will be met and what will prove it, producing the plan of a cycle. Use after a spec has a clean interrogation round. This is the last artifact before a person approves.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id>"
allowed-tools: Bash(sdlc *), Bash(git *), Read, Write, Edit, Glob, Grep
---

# Author a plan

A plan says **how**, and commits to what will prove it. It is the last document written before a
person approves, and the approval covers all three at once — so this is the artifact a reviewer reads
most closely and the one where an inadequate claim is most expensive.

Run `sdlc constraints --for plan` first.

## Refuse early

```
sdlc status <cycle id> --stage spec
```

No spec, or a spec that is not clean: stop. A plan already there: stop, and name `/sdlc:interrogate
<id> plan` or `/sdlc:steer <id> "..."`.

## Read the spec, and then read the code

Unlike the spec, this stage **must** read the codebase. A plan that does not know what is there
cannot say which files change, and a file table invented from the spec alone is the fastest way to a
plan that cannot be built.

Read `.sdlc/techstack.md` too. It declares what this solution is built from.

## Two kinds of claim, and only one is judged

| Section | Kind | Judged by |
|---|---|---|
| `Shape and departures` | a **decision** — which approach | `CON_06`, `CON_13` |
| `Files` | an **estimate** — which files, how large | nothing; it is a signal |

That distinction matters when you are tempted to pad the file table defensively. Declaring files you
might not touch is not caught by a gate, but it makes the plan's blast radius wider than the change,
which over-serialises a batch and misleads the person at the gate about how reversible this is.

**A plan holds a file table, never diffs.** Line-level change is the build's artifact.

## Say why you departed

`CON_13`: a plan that reaches for a package, pattern or layer `techstack.md` does not declare has to
say in `Shape and departures` what the declared default would have cost here.

You may depart from anything. A default that cannot be departed from is a cage, and the first
genuinely unsuitable case would see the whole file ignored. What is forbidden is departing in
silence, because then the stack file and the codebase disagree and only the diff knows.

**If the solution's origin is `adopted`, the existing standard wins.** Check `.sdlc/solution.md`. A
profile proposes a shape for new work; it does not rewrite the conventions of a codebase that
predates it. In an adopted solution, matching the surrounding code is the default and *departing
toward the profile* is the thing that needs saying.

## Make the proof adequate

This is the constraint to watch. `CON_06` forbids a plan naming a check that could pass while its
criterion is unmet. A schema can require that every criterion has *some* proof; only judgement can
tell whether "add a unit test" proves "returns within 200ms over 50,000 rows".

**An inadequate proof is worse than none**, because it produces a green verification instead of an
obvious gap.

Work through each criterion and ask: *what would have to be true for this check to pass while the
criterion is still unmet?* If you can answer that, the proof is not adequate yet.

- A unit test over a function proves the function, not the request path.
- A test against a substituted source proves the substitution.
- A test that asserts a call was made proves the call, not its effect.
- A test over 100 rows says nothing about 50,000.

**Number the proof entries to match the criteria, and name each test in backticks.**

```markdown
1. `Rolls_within_one_and_six` draws 10,000 times and asserts every face is 1 to 6.
2. `Two_requests_are_drawn_separately` requests the page twice and asserts the draws were independent.
```

The backticks are not decoration. `verify-chain` reads the names out of them and checks the normalised
test summary to see that each one **ran and passed** — so a proof entry with no backticked name commits
to nothing a reader can check, and the chain reports it. "Covered by the integration suite" is what an
inadequate proof looks like when it is written in good faith.

The numbers are compared in both directions: a criterion with no proof fails, and a proof citing a
criterion the spec does not have fails too — because that means one was deleted or renumbered after the
plan was written, and the proof that looks complete is proving something else.

You do not need the namespace. A runner reports `Dice.Tests.RollTests.Rolls_within_one_and_six` and the
check matches on the suffix, because at planning time the namespace does not exist yet.

**If a criterion cannot be proven by anything the testing layers can run, say so in `## Open`.** That
satisfies `CON_06`, because you named no inadequate check. It does not launder the problem: the
criterion is then unproven, and verify will block on it. The exit moves *where* the problem is caught,
never *whether*.

## Choose the tier honestly

`tier` is a claim about blast radius, chosen by **reversibility** rather than by size:

| tier | What it means |
|---|---|
| `local` | contained. Reverting the commit undoes it completely |
| `contract` | something outside this code now depends on the shape — an API, a published type, a message |
| `data` | a migration, a schema change, a write that cannot be taken back by reverting |

A `data` plan is the one a reviewer should slow down on, and it is the one where "we can always
revert" stops being true.

## Write it

`.sdlc/cycles/<id>/plan.md`:

```markdown
---
framework_version: 1.0.0
cycle: <id>
tier: local
---

# <the approach, in one line>

## Shape and departures

<which approach, and why this one. Any departure from techstack.md, with what the
declared default would have cost here.>

## Files

| File | Change |
|---|---|
| `path/to/file.cs` | new / what changes in it |

<Ordering reasoning goes here too, when order matters: which file has to land first and why.>

## Proof

1. `Test_name_in_backticks` and what it asserts, concretely enough to write.
2. ...

## Risk

<what could go wrong, and what a reviewer should look at hardest>

## Open
```

## Run the loop, then hand back

Interrogate, refine, interrogate again. Then:

```
sdlc status <id> --stage plan --brief
sdlc gate <id>
```

Show the panel verbatim and add nothing to it beyond the paragraph below. The gate will refuse, because
nobody has approved yet — that is the correct state and worth showing, because it is the moment to
explain what happens next:

> The three documents are clean. Approving them is yours to do: run `sdlc draft approval <id>`, read
> the file it writes, and rename it onto `approval.md`. I cannot place that file — every agent write to
> it is denied, and so is every command that would move anything onto it — and that is what makes a
> recorded approval mean something.

Then stop.
