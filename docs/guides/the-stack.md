# What a solution is built from

The declared stack, the profile that seeds it, and how to change either. **This is reference rather
than a walkthrough** — read the part you need and leave the rest.

It states no rule. `sdlc constraints` renders those, all tiers resolved, which is the only place they
are complete.

## The two files, and which one is yours

| | What it is | Who writes it |
|---|---|---|
| `.sdlc/techstack.md` | the declared stack: a table of concerns and what this solution chose for each | adoption creates it once; a person changes it after |
| `.sdlc/solution.md` | four keys, one of which is the `profile` | a person, by hand |

**Both are protected.** Every agent write to either is denied at the tool call, by file path and by
shell command alike. There is no flag and no mode — the declared stack is one of the two rule sources a
plan is read against, so an agent able to edit it could satisfy the rule about departing from a default
by adding the row it wanted instead of arguing for it.

**The declared stack is a declaration of defaults, not a set of rules.** Nothing in it binds by itself.
What it buys is that a plan reaching for something undeclared has to say what the declared default
would have cost — an argument at the one gate before code exists. To make a row actually bind, a
solution adds a `LOC_` constraint or an architecture test.

## What a profile is

A named set of declared defaults, templates and rules. Two ship, and both are carried by the binary, so
adoption works on a machine with no feed configured and no network.

| Profile | What it is for | Skeleton |
|---|---|---|
| `dotnet-default` | C# and .NET across api, web, desktop and Android | yes — a solution file, five projects, an architecture suite |
| `ml-tensorflow` | Python and TensorFlow for model work, on the same Azure platform | **none** |

`sdlc help` lists them; the `--profile` option on `sdlc init` chooses one.

**`ml-tensorflow` ships no skeleton deliberately, and that is load-bearing.** The .NET skeleton earned
its shape from a solution that was actually built and a review that followed it. Writing a Python
equivalent before anything has been built from this profile would be guesswork carrying the authority of
a default, so `sdlc scaffold` refuses with one sentence instead of writing twenty-four misleading files.
**The material gap is the architecture suite** — the only mechanical enforcement of shape the framework
has. Until that profile ships one, shape there is held by review and by whatever a plan's proof names,
which is weaker and worth knowing rather than discovering.

**The two profiles share their platform rows** — the same cloud, the same infrastructure tooling, the
same data stores. Where data lives and how infrastructure is described are decisions about the platform
rather than about the language, so they are declared once inside the framework and the two profiles
cannot drift apart on them. Nothing in a governed repository knows that composition happened: what
reaches a solution is always one flat table.

## Day one

What adoption writes depends on where the code came from.

| Origin | The declared stack starts as |
|---|---|
| `scaffolded` — a new solution | the profile's full table, filled in |
| `adopted` — code that existed first | four empty rows, and an instruction to fill them in from what the repository actually uses |

**The empty rows are deliberate.** Reading a repository to guess what it is built from would write a
guess down as a declaration, and the first plan to depart from a wrong guess would be judged against it.
An empty row declares nothing, which is honest; a wrong row declares something false.

**But empty is not a resting place.** The rule about departing from a declared default has nothing to
bite on while the table says nothing, so every plan departs from no default and the argument never
happens. `sdlc doctor` reports it — see below.

## Changing it

```pwsh
sdlc draft techstack
```

Two files are written beside the real one:

| | What it is |
|---|---|
| `.sdlc/techstack.draft.md` | a **byte copy** of the declared stack. Edit this, then rename it over the real file |
| `.sdlc/reports/<when>-stack-options.md` | the **menu**. Every concern the shipped profiles know about, what each declares for it, the alternatives this framework has argued, the choices it refused, and an empty table for a solution starting over. The command prints the path |

The rename is the act:

```pwsh
mv -f .sdlc/techstack.draft.md .sdlc/techstack.md
```

**The draft is a byte copy, and that is the whole guarantee.** Renaming it applies what somebody
changed and nothing else — nothing merged, nothing regenerated, nothing silently dropped. An agent may
edit the draft, because only the real file is denied; what an agent cannot do is place it.

**The menu is never renamed into place.** It lists answers this solution has *not* chosen. In the real
file those would be reachable by a plan that could then truthfully claim not to be departing from
anything, which is the deadlock protecting the file closed in the first place. Copy what you want out of
it into the draft and leave the menu where it is.

**It is a report, which is why it lives under `reports/`.** Written fresh on every run, read back by
nothing, safe to delete — and that directory ignores itself with a `.gitignore` of its own. The menu
outlives the rename it was written for, so a copy anywhere else in `.sdlc/` would sit there untracked
and reach every later cycle's diff as a file no plan declared.

**The menu recommends nothing.** Where two profiles disagree about a row, both answers are on the page
with the profile that declares each, and the person decides. What a solution is built from is a
judgement about how its team works, which is why no command fills the table in — a tool doing that
would be making the one decision nobody delegated to it while looking like bookkeeping.

**What belongs in a change here is a default this solution is adopting or dropping** — not a package one
cycle happened to need. That case is a departure, argued in the plan.

## Starting the table over

The menu's last section is an empty table of every concern this solution's profile declares, with the
answers taken out. Copy it into the draft, fill it in, and delete the rows this solution has no use for.

It is in the menu rather than in the draft on purpose. A blank draft would make the rename a wholesale
replacement, and every row nobody retyped would be dropped from the one file every plan is judged
against — silently, discovered later by a plan that stopped departing from anything.

## Moving to another profile

```pwsh
sdlc draft profile <name>
```

It prints all four `solution.md` keys for a person to place by hand, and writes nothing. It prints the
whole block rather than the one line that changes, because a hand-edit over a partial block drops
whichever key was left out — and the one that silently reclassifies a solution is the platform.

**What moves, and what does not:**

| | |
|---|---|
| the `profile` key in `solution.md` | you place it |
| which `PRO_` rules bind | moves with the key |
| which templates a scaffold would write | moves with the key |
| the declared stack | **does not move** |

The declared stack is a person's file, created once. A profile migration and a stack change are two
separate acts, and **nothing reconciles them**: a solution can record one profile and declare another's
stack indefinitely with no complaint from any gate. That is by design — the file is yours — but it means
a migration is not finished until you have run `sdlc draft techstack` as well. `sdlc doctor` says how
far the two have moved apart.

**The `origin` key must not change.** It says where this code came from and it is a historical fact.

## The profile's own rules

The constraint set a solution is judged against resolves in three tiers, in this order:

| Tier | Id | Where it comes from |
|---|---|---|
| global | `CON_nn` | the framework |
| profile | `PRO_nn` | the profile `solution.md` records |
| solution | `LOC_nn` | `.sdlc/constraints.md` |

So a solution's own rules are the last word.

**The profile tier is where a rule may name a language, framework or package.** The global set forbids
itself from naming one, because those rules bind solutions on any stack — which leaves a real gap, since
some judgements cannot be made without a name. That gap is what the profile tier fills.

**Not every profile fills it.** `dotnet-default` ships three rules; `ml-tensorflow` ships none, for the
reason it ships no skeleton. So a migration between them changes what binds, and `sdlc constraints` is
the only place to read what now applies.

**Three rules is the honest number rather than a thin one.** A constraint has to be a rule about an
artifact that needs reading comprehension to apply: no analyzer, schema or hook could decide it. Most
candidates for a stack rule fail that test — a rule about code belongs in an analyzer, and a mechanical
rule about a document belongs in a schema or a hook.

## One profile per solution, and no stacking

A solution records one profile. There is no overlay, no override bundle and no merge, and that is a
decision rather than an omission — `CONCEPT.md` D-68 has the whole argument.

Merging two profiles' tables would need a precedence rule on a concern key, and worse, it would make the
declared stack a **derived** file: the thing every plan is judged against would be generated rather than
written, and "a person places it" would stop meaning anything.

**So the menu is the answer to wanting another profile's rows.** A solution that wants them copies them
in — a one-time edit, visible in the diff, against permanent merge machinery. Where the overlap is
between the profiles themselves it is handled inside the framework while authoring, which is where it is
cheap.

## What `sdlc doctor` says about it

```pwsh
sdlc doctor
```

It reports a `declared stack` check and never repairs it — rewriting a person's file to agree with a
profile would be the gate deciding what a team is built from.

| What it finds | Outcome |
|---|---|
| rows that declare nothing, named | a problem |
| no rows at all | a problem |
| the options menu pasted into the real file | a problem |
| the file gone | a problem — `sdlc init` recreates a protected file that has vanished |
| rows differing from the recorded profile | **sound**, with the count. Departing is a solution's to do |

The last row is information rather than fault on purpose. Departing from the profile is legitimate, and
a panel that cried wolf at every customised stack would teach somebody to stop reading it.

## Where to go next

| | |
|---|---|
| the rules in force, all tiers resolved | `sdlc constraints` |
| one cycle end to end | `first-cycle.md` |
| that walk with one solution in it | `dice-worked-example.md` |
| every command for one cycle | `cycle-commands.md` |
| what to do when something refuses | `when-it-blocks.md` |
| every design decision behind this | `../../CONCEPT.md`, §11 and D-67, D-68 |
