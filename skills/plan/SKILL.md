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

**Then what each perspective asks you to cover** (D-90):

```
sdlc perspectives <id>
sdlc perspective show <name> --for plan --cover
```

For each perspective written for this cycle, its short list of what this plan must cover. Write those in
as you go — they are how the criteria are met, which is the plan's to choose: where a secret is kept,
which log records a failure, which test layer runs the scan. **An item that would need a criterion the
spec does not have is not the plan's to add.** Say it under `Risk` with the perspective's name; a plan
proves the spec, and proof of something the spec never asked for is work nobody approved. That
perspective's reader raises it as a suggestion, and the person takes it in by id with `/sdlc:steer`, or
leaves it for a later cycle.

**A declared file can bring a perspective the intent did not name.** `sdlc perspectives <id>` says so — a
`.razor` file brings UI/UX and accessibility. Cover it here, and expect the plan's interrogation to raise
that the intent missed it.

## Refuse early

```
sdlc status <cycle id> --stage spec
```

No spec, or a spec that is not clean: stop. A plan already there: stop, and name `/sdlc:interrogate
<id> plan` or `/sdlc:steer <id> "..."`.

## Keep a task list, if your host has one

If your host gives you a task list, open one once nothing above refuses, with these items, and mark
each done as it finishes. If it does not, carry on without one — nothing below depends on it.

1. read the spec, then the code
2. compare what the stack declares with what is there
3. decide the shape, and argue each departure
4. a proof for every criterion
5. the tier
6. write the plan
7. interrogate and refine until a round is clean
8. hand back

**The loop is one item, however many rounds it takes.** And the approval is not an item: a person
places it, after this skill has stopped.

**This stage only.** Nothing after the hand-back goes on the list: whether the next stage runs is a
person's decision, and `sdlc status <id>` names it. **The list is a view, never the record.** What is
done is what is on disk and what the binary reports, so where the two disagree the list is the one
that is wrong.

## Read the spec, and then read the code

Unlike the spec, this stage **must** read the codebase. A plan that does not know what is there
cannot say which files change, and a file table invented from the spec alone is the fastest way to a
plan that cannot be built.

**Then read the two records this stage is judged against, and the one it is not.**

```
sdlc stack
sdlc decisions
sdlc theme
```

`sdlc stack` prints what this solution declares it is built from — `CON_15` judges you against it. It
is `.sdlc/techstack.md` as the binary reads it: the defaults it pins, the stack rules switched on, and
each build settings file with whether the stack takes it and whether it is on disk.
`sdlc decisions` prints the choices somebody already settled and **`CON_19` judges you against those**:
a plan whose approach contradicts a standing entry says what the recorded decision would have cost. Read
it before you choose a shape rather than after, because a plan that has not read it cannot tell whether
it is departing — and that is the whole failure the rule exists to catch.

`sdlc theme` is the one nothing judges. Read it anyway if this change touches a surface or a report:
it holds the roles and values somebody chose, and a value invented beside them is the drift a register
exists to prevent. It is also not protected, so changing one is ordinary declared work — just declare
the file.

## Compare what is declared with what is there

**You have now read both halves — the declared stack and the code — so say where they differ before
you choose a shape.** A plan written without that comparison cannot tell whether it is following a
default, departing from one, or building on something the stack never mentioned.

| What to compare | Who decides it | What you do with a difference |
|---|---|---|
| a build settings file the stack takes that is not on disk | the binary: `sdlc stack` ends with it, as `Gap` | see the two cases below |
| a declared default the code does not use — the stack says EF Core and the repository talks to its database another way | you, by reading | it is a departure waiting to happen. If this work touches that concern, `CON_15` applies and you say which way you are going and what the other would have cost |
| something the code uses that the stack does not declare | you, by reading | not a finding against the code. Name it in `Shape and departures` if this work builds on it, so the person at the gate sees a choice the stack never recorded |

**A gap is closed by a plan, and only by one whose intent asks for it.**

| The intent | What the plan does about a build settings gap |
|---|---|
| asks for it — adopting the build settings, or the first cycle of a new solution, whose skeleton needs them | declares the missing files in `Files`, by the exact paths `sdlc stack` prints, and plans for what they break. `sdlc scaffold <id>` writes the ones the plan declares once it is approved |
| is about something else | names the gap in `Shape and departures` in one sentence, and leaves it. `CON_05` kept it out of the spec, so no criterion asks for it and a plan that brought it would be building what nothing here proves |

**Why the second row is strict.** `Directory.Build.props` makes warnings errors. Declared by a plan
about one endpoint, it turns that cycle into a repair of every warning in the codebase, for a reason no
criterion in its spec mentions. Closing the gap is a change with its own intent.

**When you do declare one, plan for the fallout.** State which files it brings, what each will start
refusing — existing warnings, package references that carry their own version, formatting — and whether
this cycle fixes those or narrows the setting so they are not errors yet. Either is legitimate; saying
neither is a plan whose first build fails for a reason it did not predict. Two of the five hold rules
this cycle is then judged by, so `CON_18` applies to declaring them: `sdlc constraints --for plan` has
what it asks `Shape and departures` to say. Changing one of these files
later is governed work too: the template's own comment on the `Files` section, below, names them.

**"In work that calls for it" still bounds the other two rows.** A solution that declares a message bus
it has not built yet is not a finding against a plan for a static page.

## List the decisions first

`Shape and departures` opens with **Decisions this plan makes:** and one line per choice. The person
approving reads that list at the rename — `sdlc draft approval` prints it — and in one real cycle both
reopens were a choice found in the plan only after it was approved: the hosting left out, and the chart
tables drawn by hand, each a clause in a hundred-kilobyte document. If a person would be surprised to learn
it after the build, it belongs on the list.

## Two kinds of claim, and only one is judged

| Section | Kind | Judged by |
|---|---|---|
| `Shape and departures` | a **decision** — which approach | `CON_06`, `CON_15`, `CON_19` |
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

`CON_19` is the same shape, pointed at the decision record. If your approach takes an alternative a
standing `DEC_` entry weighed and set aside, say what the recorded decision would have cost here.

**Departing is legal — that is the half to hold on to.** A decision that cannot be revisited is a cage,
and a record nobody may contradict would be ignored the first time it genuinely did not fit. What the
rule forbids is taking the alternative without saying so, because then the person at the gate cannot
tell whether you are correcting a decision that has stopped fitting or re-running an argument somebody
already had. Those need opposite responses, and both arrive looking identical.

**An entry's `Instead of` clause is how you tell.** It names the alternatives that were weighed, so if
your approach is one of them you are departing, whatever else has changed. If it is not, you are not —
a decision about publishing does not constrain a plan about pagination.

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
where an adopted one arrives when its team moved it, by renaming a draft from
`sdlc draft solution --standard profile`.

**The key and the build settings are separate.** `standard: profile` says which patterns a plan
defaults to. It does not mean the build settings files are there — those are rows in the declared
stack, and `sdlc stack` says which are on disk.

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

**Number the proof entries to match the criteria, and open each with its test's name in backticks.**

```markdown
1. `Rolls_within_one_and_six` draws 10,000 times and asserts every face is 1 to 6.
2. `Two_requests_are_drawn_separately` requests the page twice and asserts the draws were independent.
3. `Keyboard_rolls`, `Keyboard_keeps_focus` press Enter on the `Roll` button and assert where focus is.
```

The opening backticks are not decoration. `verify-chain` reads the names an entry **opens with** — one,
or several joined by commas or "and" — and checks the normalised test summary to see that each one
**ran and passed**. Backticks after the first plain word are formatting: entry 3's `Roll` is a button,
not a test. So a test named mid-sentence is never checked, and an entry that opens with prose commits to
nothing a reader can check — the plan's shape check refuses it. "Covered by the integration suite" is
what an inadequate proof looks like when it is written in good faith.

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

Decisions this plan makes:

- <one line per choice a person approving this would want to know before the build: the approach,
  each package or library it reaches for, how the processes are hosted, what it builds by hand that a
  declared library would draw, each departure from techstack.md or decisions.md. Plain words, no
  section references. 'sdlc draft approval' prints this list at the rename, so a choice left out of it
  is one the person approves without reading.>

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
own act, outside a cycle: 'sdlc draft techstack' and a rename. The theme is not one of them -- a
cycle may declare .sdlc/theme.json and change a value like any other file.

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

**First, the suggestions the plan's rounds left open, if there are any.** Ask once: show them grouped
by perspective, each with its statement, and ask which to take in, which to decline, and which to leave.
Leaving them is an answer, and the commonest one — skip the question entirely when nothing is open. What
the person names goes to `/sdlc:steer <id> <finding id>...` in one run, which writes each into the intent
with their name on it and re-interrogates down to here (D-97). **Never take one in on their behalf**: a
suggestion nobody chose is exactly what the spec may not derive from.

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
