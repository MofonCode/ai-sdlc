# Direction — the diagnosis, the order of work, and what is not yet a chain

**What this file is.** A diagnosis reached on 2026-09-20 after eleven fresh-context
interrogations of one small change, plus the order of work that follows from it, plus two ideas
that are not yet chains. None of it exists anywhere else.

**What this file is not.** It is **not a list of open chains** — each chain records its own stage
in its `status:` line and its parked or blocked banner. It is **not a list of outstanding gaps** —
`.sdlc/OPEN.md` holds those. Copying either here would make two lists with nothing comparing
them, which is the failure this repository has built five separate checks for.

---

## The diagnosis

**The engine's behaviour is split between code and prose, and nothing marks the boundary.**

The finding that produced this: the rule for deriving a solution identifier from a directory name
exists **twice**. Once as PowerShell in `Invoke-ScaffoldManifest.ps1`, which nothing on the real
path calls — `init` supplies `-Solution` on every invocation. Once as English in
`init/SKILL.md`, which a model follows to compute the candidate an owner actually confirms.

Shipped requirement R17 says the executor derives the candidate and `init` presents *that*
candidate. **It never has.** That requirement describes a system nobody built, and it shipped
through a full chain with six rounds of verification.

The drift did not merely cause a defect. **It hid one through nine interrogations**, every one of
which analysed the PowerShell while the prose was the thing in charge.

**This is not unique to derivation.** `init`'s prose describes the executor's invocation and
nothing checks the description against the executor's real parameters. Its synopsis renders
`-Solution` optional one line above an instruction to always supply it. Seven requirements of the
shipped scaffold spec are marked human-verified because no instrument exists for them.

Every one is the same shape: **a decision living in prose, where nothing can test it, quietly
diverging from code that could be.**

## The missing primitive

The executor has exactly one interaction shape: **perform a list of actions against a target.**
It creates the target directory, refuses unless it finds an approved charter, then acts.

There is no way to **ask it a question** — no call that takes inputs, returns a value, writes
nothing, creates nothing, and needs no gate, on the grounds that a gate exists to approve an
effect and a query has none.

That absence is why one change about a regular expression grew to five hundred lines across
eleven revisions. Every revision was improvising a query mechanism inside a change that should
not have contained one, and each improvisation failed differently: bypass the approval gate,
reorder the interview, add a flag, three unchosen output contracts.

**Queries and commands are different things and the engine only has commands.**

## The order of work

1. **Settle the charter's internal contradiction.** The artifact schema says amending the charter
   *"is its own approval, never a side effect of shipping a feature."* Invariant 9 says the
   register of known silent failures grows whenever a new class is found — which happens while
   shipping. Charter versions 4, 5, 6 and 7 each did it anyway and recorded the contradiction.
   It has now shaped three drafts of one intent and produced a blocking finding in two separate
   interrogations. **Small, and it unblocks a recurring pattern rather than one change.**

2. **The executor answers queries.** A query takes inputs, returns a value, touches nothing, and
   runs before any gate. It carries a defined report shape, the way the scaffold action set
   already does — that report is what makes the existing boundary testable, and it is the part
   worth copying. Add a check asserting **engine prose contains no algorithms**, which is the
   same shape as the existing check that scans engine instruction files for pack filenames.
   **This is also the first vertical** (see below), prototyped on the one case forcing it rather
   than designed in the abstract.

3. **Rewrite the dot chain short.** Once derivation is a query, `solution-identifier-can-carry-a-dot`
   is roughly thirty lines: widen the identifier grammar to permit interior dots, change the
   anchors from `^`/`$` to `\A`/`\z`, and move derivation behind the query. Most of the eleven
   rounds' findings evaporate, because they were about a query mechanism that no longer needs
   inventing there.

**The parked chain keeps its evidence.** Nine rounds of measured findings are real and worth
keeping; they belong to a chain that has not been written yet.

## Two process rules, from the failures rather than from principle

Most of what eleven interrogations caught reduces to two mechanical lapses. Neither needs a
framework change.

**Never list an affected file without opening it.** Four false entries across three rounds — a
test file, two skill documents, a README — every one added by inference. That list is what a
build's file assignments are made from.

**Every success criterion must state what fails if the change is not made.** One round found that
all seven criteria passed with the headline change skipped entirely. Another, four rounds
earlier, found a criterion aimed at code the real flow never reaches. Same defect twice.

---

## Not yet chains

Two ideas raised on 2026-09-20 and deliberately pinned rather than started, because three chains
in flight with none approved is how the parked pile reached four.

### Verticals — the engine holds abstractions, the pack holds implementations

The engine would know a closed set of **capability names**, each with a defined input and a
defined report shape. A pack implements them. `scaffold.json` is the existing proof: the engine
knows six action kinds and nothing else, so `dotnet new classlib` runs on a developer's machine
with the string `dotnet` appearing nowhere in the engine.

**The boundary that makes it work is the report, not the action list.** A capability that runs a
pack command and trusts the exit code is a hole, not an abstraction.

**The line worth drawing early:** verticals work for questions with **observable outcomes** —
did the files appear, is the repo green, do these types follow the rule. They fail for
**judgement** — where module boundaries should fall. A vertical for a judgement question looks
identical to one for an observable question until you try to test it.

Three tensions, named rather than solved:

- **Invariant 1 gets easier.** Each vertical pushes more stack knowledge out of the engine,
  which is that invariant's own stated test.
- **Invariant 9 gets much harder.** The scaffold vertical needed 44 executor tests and 23
  end-to-end tests before it was trustworthy. Each new vertical carries a comparable bill, and a
  vertical cheap to add and expensive to verify is how a framework grows surfaces nobody checks.
- **Permanent non-goal N3 is nearby.** Verticals for build, test and deploy start to resemble the
  CI/CD system this framework will never be. The workable line is probably that the engine may
  *ask* whether a repo is green but may not own a pipeline, a schedule, or status over time.
  Worth settling before the first vertical rather than after the third.

**The token closure stops being parkable.** Four of the five closures recorded in
`engine-substitutes-declared-tokens` exist because the engine decides which placeholders can
exist. Every vertical wants its own parameters, so that parked work becomes a prerequisite.

**Cheapest honest first step:** not building verticals — writing the contract that makes one
work, and checking it can describe `scaffold.json`. If it cannot, it is wrong.

### ML separated from competition, and how goals, features and labels reach the process

**Two intents hiding in one.**

**The split is real.** The `competition` kind emits a feature-and-scoring library and a training
runner — *ML-system* shaped — plus a starter notebook, which is *competition* shaped. It already
conflates them. A competition has a fixed dataset, a fixed metric, a leaderboard, a submission
format, a time box, and nothing served. An ML system has feature engineering, training
pipelines, a model registry, drift, retraining and serving. Different scaffolds.

**The second half is the valuable one and the harder one.** Carrying a goal, a feature set, a
label definition, a metric, a split strategy and a baseline means the *artifact chain* holds
ML-specific information. The chain is prose end to end today.

**The framework already knows ML is special and has no artifact for it** — the verifier agent
hunts *"contamination bugs wherever the code touches ordered data or a train/test split"*, which
is a real concern with nowhere to be declared.

**The tension to resolve before writing:** *"what is the label"* is a spec question and could
live in the engine. *"How features are computed"* is stack-shaped and belongs in a pack.
Invariant 1 makes that line load-bearing, and an intent that does not draw it deliberately will
be blocked on it.

### Automating the chain from intent to retro

Raised as: approve an intent, maybe review a spec, then let plan → build → verify → retro run
unattended and report what it could not resolve.

**It runs at the charter's premise.** The gates are the product, not overhead — the plan skill
calls its approval gate *"the one that is not negotiable"*, and invariant 4 says a gate is
crossed only by explicit human words.

**The session of 2026-09-20 is the best evidence available and it cuts both ways.** Eleven
interrogations ran with no human input and every one found real defects — that loop is already
autonomous and already works. But every time a **judgement** was needed it genuinely needed the
owner, and the assistant's recommendations were wrong at least twice: it recommended stopping
after round seven, and round seven found that six rounds had analysed a function the real flow
never calls; and it priced a design option at roughly half its true cost while recommending it.
Unattended, both would have shipped.

**So the question worth writing an intent around is not "can the pipeline run itself."** It is
**which gates can be automated, measured against what happened when a judgement was made without
a human.** That is falsifiable, and this session is a usable dataset with a known error rate.

**Overlap to avoid:** the "report what was not resolved" half is already drafted as
`findings-leave-only-by-decision`. Do not write it twice.
