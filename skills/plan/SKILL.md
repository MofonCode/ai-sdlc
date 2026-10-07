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
| `Shape and departures` | a **decision** — which approach | `CON_06`, `CON_15` |
| `Files` | an **estimate** — which files, how large | nothing; it is a signal |

That distinction matters when you are tempted to pad the file table defensively. Declaring files you
might not touch is not caught by a gate, but it makes the plan's blast radius wider than the change,
which over-serialises a batch and misleads the person at the gate about how reversible this is.

**A plan holds a file table, never diffs.** Line-level change is the build's artifact.

## Say why you departed

`CON_15`: a plan departs from `techstack.md` in either direction, and both have to be said out loud
in `Shape and departures`.

- **Reaching for something it does not declare** — a package, a pattern, a layer. Say what the
  declared default would have cost here.
- **Leaving something it declares unapplied, in work that calls for it.** Say what applying it would
  have cost. This is the half that used to breach nothing, and it is the one that goes unnoticed: a
  solution shipped without the api description and the tracing its own stack file declares, and no
  rule had anything to say about it.

**"In work that calls for it" is the scope.** A plan touching one endpoint is not silent about the
message bus by failing to mention it. A plan standing up an HTTP surface is silent about the api
description row by failing to mention that.

You may depart from anything. A default that cannot be departed from is a cage, and the first
genuinely unsuitable case would see the whole file ignored. What is forbidden is departing in
silence, because then the stack file and the codebase disagree and only the diff knows.

**Which standard wins is `standard` in `.sdlc/solution.md`, not `origin`.** Read that key.

| `standard` | The default for new work | What needs saying |
|---|---|---|
| `existing` | match the surrounding code | departing *toward* the profile |
| `profile` | the profile's patterns | departing *from* them |

`existing` is where an adopted solution starts: a profile proposes a shape for new work and does not
rewrite the conventions of a codebase that predates it. `profile` is where a scaffolded one starts, and
where an adopted one arrives when its team opted in with `sdlc init --adopt-standard`.

**If the key is absent, derive it from `origin`** — `scaffolded` means `profile`, `adopted` means
`existing`. That is what the framework decided on origin alone before the key existed, so an older
solution reads exactly as it already behaved.

**Neither value says anything about the code already there.** Constraints govern the change, and
`standard: profile` is not a finding against every file that predates it. Migrating existing code is its
own cycle, with its own intent and approval, like any other work.

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

**You do not need the cases either.** Name a parameterised test once, without its arguments: a runner
records one result per case and decorates the name — `Page_shows_a_face ("chromium")` — and the check
finds every case of the test you named and requires **all** of them to have passed. So a test run across
two browser engines is one proof entry, and a proof that passed in one engine and failed in the other is
reported as absent rather than as proven. Do not collapse cases into a loop inside one test to make the
name match; that costs the per-case result your own verify document would otherwise cite.

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
declared default would have cost here.

And if the Files section changes a rule this cycle is judged by -- an architecture test, a build
property file, a banned-symbols list, a hook configuration, the pipeline -- what satisfying that
rule unchanged would have cost. CON_18. Amending such a rule is legitimate declared work; doing it
without saying what it bought is not, because then the one check that cannot be talked around is
rewritten by the thing it was checking.>

## Files

| File | Change |
|---|---|
| `path/to/file.cs` | new / what changes in it |

<Ordering reasoning goes here too, when order matters: which file has to land first and why.>

<Never name one of the six files only a person writes -- approval.md, abandoned.md, constraints.md,
decisions.md, solution.md, techstack.md. The schema refuses a plan that declares one, because the build
is denied the write and the plan could not be carried out as written. Changing the declared stack is its
own act, outside a cycle: 'sdlc draft techstack' and a rename. The palette is not one of them -- a
cycle may declare .sdlc/palette.json and change a value like any other file.

A file holding a rule is different: you may declare one and change it. The evidence comparison reports
every such file under GOVERNING whether or not the plan declared it, and CON_18 asks Shape and
departures to say what the rule would have cost. Those are an architecture test, Directory.Build.props,
BannedSymbols.txt, .editorconfig, a hook configuration and the pipeline.>

## Proof

1. `Test_name_in_backticks` and what it asserts, concretely enough to write.
2. ...

## Risk

<what could go wrong, and what a reviewer should look at hardest>

## Open
```

## Run the loop, then hand back

Interrogate, refine, interrogate again — until a round is clean, a reader reports a conflict, or the
panel's `Loop` line says the loop is stuck on one rule. It is the same loop the intent and spec skills
run, and the intent skill's loop section has the table. Nothing caps the rounds; what stops the loop is
a rule coming back three times, which is the loop going backwards rather than going slowly.

Then:

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
