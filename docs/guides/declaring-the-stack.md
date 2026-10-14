# Declaring what a solution is built from

The declared stack: what it pins, which stack rules it switches on, which build settings files it
takes, where its first draft comes from, and how to change it. **You place it once, right after
adoption, and no cycle opens until you have** — `initializing-the-codebase.md` is the step before. After
that it is something you come back to when a default or a rule turns out wrong for this solution, so
read the part you need and leave the rest.

It states no rule. `sdlc constraints` renders those, all tiers resolved, which is the only place they
are complete.

## One file, and it is yours

| | What it is | Who writes it |
|---|---|---|
| `.sdlc/techstack.md` | the declared stack: what this solution chose for each concern, which stack rules it is held to, and which build settings files it takes | a person, by renaming a draft into place. Nothing else, ever |

**It is protected.** Every agent write to it is denied at the tool call, by file path and by shell
command alike. There is no flag and no mode — the declared stack is one of the two rule sources a plan
is read against, so an agent able to edit it could satisfy the rule about departing from a default by
adding the row it wanted instead of arguing for it, or drop a stack rule by switching its row off.

**Adoption does not write it.** `sdlc init` decides nothing about the stack and takes no option that
would. The file comes from `sdlc draft techstack` and a rename, and **no cycle opens until it is
there** — `sdlc new` refuses and names the command.

**What is in the file is exactly what binds.** Nothing about the stack is selected from anywhere else:
no solution records a profile, and `solution.md` says nothing about what the code is built from.

## The three tables

```markdown
## Defaults

| Concern | Choice |
|---|---|
| ORM | EF Core |

## Rules

| Rule | On | What it holds a plan to |
|---|---|---|
| `PRO_03` | true | A proof about stored data runs against the store the solution declares |

## Build settings

| File | On | What it does |
|---|---|---|
| `Directory.Build.props` | true | Makes warnings errors, sets the analyser level and wires the banned-API list, for every project |
```

| Table | A row means | No row means |
|---|---|---|
| defaults — two columns | this choice is **pinned**: a plan reaching for something else has to say what the pinned one would have cost | that choice is left to each plan |
| rules — three columns | `true`: every plan is judged against the rule. `false`: the solution was offered it and declined | the rule does not apply |
| build settings — three columns, under their own heading | `true`: this solution takes the file. `false`: it was offered and declined | the stack says nothing about that file |

**A default does not bind by itself.** What a row buys is an argument at the one gate before code
exists. To make one actually bind, a solution adds a `LOC_` constraint or an architecture test.

**A rule row does bind**, and it is the only thing that switches a stack rule on. The third column is
for the reader — nothing is judged against that sentence, and `sdlc constraints` prints the rule in
full with what it forbids and why.

**A default and a rule are told apart by their width.** A rule row has three cells and a default has
two, so neither reader counts the other's rows: a rule is not a default a plan can argue its way past,
and a default is not a rule.

**A build settings row is told apart by where it sits.** A rule has an id to recognise it by and a file
name has nothing, so those rows are read from under `## Build settings` and nowhere else. Move the
table under another heading and the stack says nothing about the files.

## An empty file is an answer

A declared stack with nothing in it pins nothing and switches on no stack rule. Every plan chooses
freely, and the global constraints still apply in full.

That is a legitimate place to start — for a solution whose stack is part of what the first cycles are
working out — and it is different from a file that is not there. Placing an empty file is a person
saying nothing is pinned. A missing file is nobody having said anything, which is why one opens a cycle
and the other does not.

**What it costs:** with no declared default there is nothing for a plan to depart from, so nothing
keeps the second cycle from choosing a different framework from the first. Consistency arrives when
somebody adds the rows.

## The first draft

```pwsh
sdlc draft techstack
```

With no declared stack in place, the draft is **the framework's default, whole**: every row the
`dotnet-default` profile declares, and every stack rule the framework ships, each marked `true`. The
framework ships three.

It also lists the five build settings files, one row each — **`true` where the repository held no code
before the framework, `false` where it did.** `Directory.Build.props` makes warnings errors, so on an
existing codebase switching it on can fail a build over files nobody has touched. That has to be
something a team chose, never something it got by renaming a draft unread.

The edit is subtraction:

| The solution | What to do to the draft |
|---|---|
| is built on the default stack | nothing. Rename it as it is |
| is .NET with different choices | change or delete the rows that are not true of it |
| is built on something else | delete what does not apply, write in what it uses, and set to `false` any rule naming a framework it does not have |
| should pin nothing yet | `sdlc draft techstack --empty`, and rename that |

**Every solution starts from the same draft, new repository or old.** An adopted repository used to
start with four empty rows, because the file was written straight into place and a guess written down
as a declaration is worse than nothing. A draft is not a declaration: it is read, and cut down, before
it is placed. **So read it** — a default renamed unread onto a codebase that is not .NET declares
something false, and every plan after it is read against that.

## Changing it

The same command. With a declared stack in place the draft is a **byte copy** of it.

| | What it is |
|---|---|
| `.sdlc/techstack.draft.md` | what you rename. A byte copy of the declared stack; or the default where there is none; or nothing, with `--empty` |
| `.sdlc/reports/<when>-stack-options.md` | the **menu**. Every concern the shipped profiles know about and what each declares, the alternatives this framework has argued, the choices it refused, every stack rule it ships, every build settings file with what it does and what it costs, and an empty table for a solution starting over. The command prints the path |

The rename is the act:

```pwsh
mv -f .sdlc/techstack.draft.md .sdlc/techstack.md
```

**A byte copy is the whole guarantee.** Renaming it applies what somebody changed and nothing else —
nothing merged, nothing regenerated, nothing silently dropped. An agent may edit the draft, because
only the real file is denied; what an agent cannot do is place it.

**The menu is never renamed into place.** It lists answers this solution has *not* chosen. In the real
file those would be reachable by a plan that could then truthfully claim not to be departing from
anything, which is the deadlock protecting the file closed in the first place. Copy what you want out of
it into the draft and leave the menu where it is.

**It is a report, which is why it lives under `reports/`.** Written fresh on every run, read back by
nothing, safe to delete — and that directory ignores itself with a `.gitignore` of its own. The menu
outlives the rename it was written for, so a copy anywhere else in `.sdlc/` would sit there untracked
and reach every later cycle's diff as a file no plan declared.

**The menu recommends nothing.** Where two profiles disagree about a row, both answers are on the page
with the profile that declares each, and its rule rows are all written `false` — listing a rule is not
choosing it. What a solution is built from is a judgement about how its team works.

**What belongs in a change here is a default this solution is adopting or dropping** — not a package one
cycle happened to need. That case is a departure, argued in the plan.

## Starting the table over

The menu's last section is an empty table of every concern the default profile declares, with the
answers taken out. Copy it into the draft, fill it in, and delete the rows this solution has no use for.

It is in the menu rather than in the draft on purpose. A draft of an existing file is a byte copy; a
blank form in its place would make the rename a wholesale replacement, and every row nobody retyped
would be dropped from the one file every plan is judged against. To pin nothing at all, `--empty` says
so on the command line, where it is a decision somebody typed.

## What a profile is, now

A named set of default rows, carried by the binary. Two ship:

| Profile | What it is for | Where it reaches a solution |
|---|---|---|
| `dotnet-default` | C# and .NET across api, web, desktop and Android | the first draft is written from it |
| `ml-tensorflow` | Python and TensorFlow for model work, on the same Azure platform | the options menu, for rows to copy across |

**No solution records a profile.** One is where a draft starts and where the menu's rows come from; it
governs nothing afterwards. There is no `--profile` option on `sdlc init` and no `sdlc draft profile` —
both are answered with a sentence pointing here.

**The two share their platform rows** — the same cloud, the same infrastructure tooling, the same data
stores — declared once inside the framework so they cannot drift apart. Nothing in a governed
repository knows that composition happened: what reaches a solution is one flat table.

## The stack rules

The constraint set a solution is judged against resolves in three tiers, in this order:

| Tier | Id | Where it comes from |
|---|---|---|
| global | `CON_nn` | the framework |
| profile | `PRO_nn` | the rules this solution's declared stack marks `true` |
| solution | `LOC_nn` | `.sdlc/constraints.md` |

So a solution's own rules are the last word.

**The middle tier is where a rule may name a language, framework or package.** The global set forbids
itself from naming one, because those rules bind solutions on any stack — which leaves a real gap, since
some judgements cannot be made without a name. That gap is what this tier fills.

**Each one already says when it applies.** One is about a criterion a browser decides, one about a plan
that reaches for the kernel, one about a proof over stored data — so a rule left on in a solution that
has no such thing never fires. Switching one off is for a solution that does have the thing and has
decided not to be held to the rule.

**Three is the honest number rather than a thin one.** A constraint has to be a rule about an artifact
that needs reading comprehension to apply: no analyzer, schema or hook could decide it. Most candidates
for a stack rule fail that test — a rule about code belongs in an analyzer, and a mechanical rule about a
document belongs in a schema or a hook.

**A rule a later release adds does not apply until you add its row.** What is listed is what binds, so
upgrading the binary never changes what a plan is judged against. `sdlc doctor` names a shipped rule the
file does not list, so it is declined rather than merely unseen.

## The build settings

Five files decide how .NET code is built. Each is created once, never rewritten, and yours to amend.

| File | What it does |
|---|---|
| `.editorconfig` | the code conventions: formatting and style a build can enforce, and naming an editor guides |
| `Directory.Build.props` | makes warnings errors, sets the analyser level and wires the banned-API list, for every project |
| `Directory.Packages.props` | central package versions, so a project cannot pick its own |
| `global.json` | pins the SDK and selects the test runner, without which the suite silently does not run |
| `tests/Directory.Build.props` | the six analyser rules relaxed for test code, each with its reason |

The options menu says more about each: what is in it, what switching it on costs a codebase that
predates it, and which other file it is useless without.

**A row declares; it writes nothing.** Adoption writes none of these, and neither does switching a row
on. The file arrives inside a cycle:

1. Set the row to `true` in a draft and rename it into place.
2. `sdlc stack` now shows the file as `declared, MISSING`.
3. A cycle whose plan declares the file is approved.
4. `sdlc scaffold <id>` writes it. A file the repository already has is kept as it is.

**Why a cycle rather than the row.** The file arriving is the moment a build can start failing on code
nobody touched. Inside a cycle that is the declared scope of one change a person approved, and the
fallout is that cycle's to fix. Written on the strength of a row, it would land in whichever build ran
next, as a file that cycle's plan never declared.

**Whether a given cycle closes the gap is its intent's to say.** A plan about something else names the
gap and leaves it. Closing it is a change with a scope of its own.

**When a newly arrived file fails the build, amend it inside that cycle.** Narrow a severity, or scope a
rule to a directory. Each file is created once and never rewritten, so your amendments survive every
later run.

### The skeleton

`sdlc scaffold <id>` also writes the .NET solution skeleton — a solution file, six projects (four
under `src/` and two test projects, one of them the architecture suite) and a reference slice — on a
solution that held no code, **together with all five build settings**,
whatever the rows say, because the skeleton does not compile without them.

A solution adopted into existing code never gets the skeleton: it has a shape already. What it can be
given is the build settings its plan declares, and nothing else.

Nothing in the declared stack decides whether either may be written. Both run only behind a plan a
person approved, and a plan declaring those files says the solution is .NET. Where the declared stack
pins something else, or switches a file's row off, that plan had to argue the departure to be approved
at all.

## What `sdlc stack` shows

```pwsh
sdlc stack
```

The declared defaults, the stack rules with which are on, and each build settings file with what the
stack says about it and whether it is on disk. It ends with the gap: the files the stack takes that are
not there.

| It says | Meaning |
|---|---|
| `declared, present` | the stack takes it and a file of that name is here |
| `declared, MISSING` | the stack takes it and a cycle has not brought it yet |
| `declined`, with or without one here | a row switches it off. One that is here is this repository's own |
| `no row`, with or without one here | the stack says nothing about it |

**Present means a file of that name exists, and nothing more.** Each is yours to amend and a repository
may have had its own first, so there is no framework's version to compare against.

**It names the gap and closes none.** The plan stage reads it before choosing a shape.

## A solution adopted before this

Its `solution.md` still carries a `profile:` line, and its declared stack lists no rule. Nothing can
take the line out — the file is protected — so **it is still read, as a fallback**: a stack file that
declares defaults and lists no rule keeps the rules that line names. Upgrading the binary changes
nothing about what such a solution is held to.

The fallback ends the moment the stack file says anything about rules, or is emptied. To make the rules
visible where they bind, run `sdlc draft techstack`, copy the rule rows out of the menu into the draft,
and rename it. `sdlc draft solution` writes the solution document without the old line.

## What `sdlc doctor` says about it

```pwsh
sdlc doctor
```

It reports a `declared stack` check, and a `build settings` check where the stack lists any, and
never repairs either — rewriting a person's file would be the gate
deciding what a team is built from.

| What it finds | Outcome |
|---|---|
| the file is not there | a problem — nothing says what the solution is built from, and no cycle opens |
| a row with a concern and no choice, named | a problem — it pins nothing while reading as a decision |
| the options menu pasted into the real file | a problem |
| a rule row naming an id this release does not ship | a problem — the row switches on nothing |
| nothing in the file at all | **sound** — it pins nothing, which is a solution's to decide |
| rows and rules | **sound**, with how many rows are declared and how many listed rules are on |
| build settings rows | a second check, `build settings`: **sound**, with how many of the five the stack takes and which are not on disk yet. A declared file that has not arrived is not a problem — a cycle brings it |
| a build settings row naming a file this release does not ship | a problem — the row takes nothing |
| a shipped rule the file does not list | **sound**, and named |
| no rule listed, and an old `profile:` line still in `solution.md` | **sound**, and it says where the rules still come from |

## Where to go next

| | |
|---|---|
| the rules in force, all tiers resolved | `sdlc constraints` |
| the step before: adoption, and placing `solution.md` | `initializing-the-codebase.md` |
| one cycle end to end | `running-a-cycle.md` |
| that walk with one solution in it | `../samples/rolling-a-die.md` |
| every command for one cycle | `../cheat-sheets/commands.md` |
| what to do when something refuses | `handling-refusals.md` |
| every design decision behind this | `../../CONCEPT.md`, §8.2, §11 and D-67, D-68, D-74, D-75, D-76, D-77 |
