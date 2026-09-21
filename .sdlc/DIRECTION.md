# Direction — the diagnosis, the order of work, and what is not yet a chain

**What this file is.** A diagnosis reached on 2026-09-20 after eleven fresh-context
interrogations of one small change, plus the order of work that follows from it, plus two ideas
that are not yet chains. None of it exists anywhere else.

**What this file is not.** It is **not a list of open chains** — each chain records its own stage
in its `status:` line and its parked or blocked banner. It is **not a list of outstanding gaps** —
`.sdlc/OPEN.md` holds those. Copying either here would make two lists with nothing comparing
them, which is the failure this repository has been bitten by four times and built five separate
checks for — one of the four needed two, because the pair it holds together disagrees about
content and about order.

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

1. **Slice manifests are proven disjoint across chains, not only within one.** Raised on
   2026-09-20 while asking whether two sessions could each run an intent without colliding, and
   **placed first by the owner: nothing else starts until this does.** The reason is practical —
   until it exists, two lines of work cannot safely run at once, so every other item on this list
   is serialised behind one session's attention.

   **The hole:** `plan.md` declares every file each slice touches and `Assert-SliceManifest.ps1`
   proves those manifests are pairwise disjoint before a build fans out — but **only within one
   plan**. Nothing proves two *approved* plans do not claim the same file. The failure mode is a
   merge conflict discovered after both implementers have finished.

   **This was scoped as "no new mechanism; widen two that exist" and that estimate was wrong.**
   Rewritten on 2026-09-21 after the intent was repeatedly interrogated in fresh context.
   Three assumptions in the original scoping did not survive contact with the code:

   - **"Read every `plan.md` with `status: approved` and prove them disjoint"** is the rule that
     cannot work. Measured: 35 claims over 26 distinct paths, eight paths claimed by more than
     one chain — and every one of the eight is false, because `approved` is where a plan's
     status stops and five of the six plans carrying it shipped weeks ago. A way to say a plan
     is *finished* has to exist first, and that moves a shape the charter freezes.
   - **"The gate hook could say *claimed by chain X* rather than checking only the current
     one"** is wrong twice. The hook already reads every approved plan under the root, not only
     the current chain's; and it has no notion of a current chain at all, so it cannot name the
     *other* one. What it can honestly say is that more than one live plan claims the file.
   - **The hook is narrower than assumed.** It skips markdown and json by design, so five of the
     eight measured collisions are invisible to it, and it fires on three edit tools only — a
     `git mv` or a formatter never reaches it.

   **What the step actually is:** a lifecycle marker on a frozen frontmatter shape, one parser
   extracted from two that disagree in four ways, a rewritten path normalisation, and five new
   named checks with their register rows. It is still first, because the reason it was placed
   first is untouched — until two approved plans can be compared, two lines of work cannot
   safely run at once.

   **The charter amendment it was blocked on has landed**, as versions 9 through 12 on
   2026-09-21: 9 settled twelve questions in one conversation, and 10, 11 and 12 repaired five
   defects in it, each found by a fresh-context interrogation of the chain it unblocked. The
   chain is at `status: draft` and its next gate is the owner's approval, then the spec.

   **What that sequence taught, recorded in v11's log row rather than here:** an amendment's
   error rate scales with its agenda. v8 settled three questions and has needed no repair.

   **Timing wrinkle:** files are declared at *plan* time, because at intent stage the design has
   not happened and nobody knows them yet. So this protects concurrent **builds**. Two concurrent
   **intents** cannot clash on implementation files, because neither has any.

   **First prerequisite — done on 2026-09-20, charter version 8.** This step adds named checks,
   and a named check needs a row in the register of silent failures, which was blocked on a
   contradiction four charter versions had recorded and none had settled. Three questions went
   into one amendment conversation, because an amendment costs a version and a log row and
   asking them separately costs three of each. **The decisions live in the charter's amendment
   log and in the charter schema the engine ships — not here**, so that there is one statement of
   each rather than two that can drift. What this step needs from them: **a register row rides
   the approval gate of the change that adds the check, and that change's plan must declare the
   charter in its slice manifest.** So the plan for this step declares `.sdlc/CHARTER.md`.

   **Second prerequisite — done on 2026-09-21, charter versions 9 through 12.** Twelve
   questions in one conversation, each a move of a shape the charter freezes: what marks a plan
   finished and who writes it, how far that reaches, whether it is one-way, and the grammar of
   the manifest and the status line. Versions 10, 11 and 12 then repaired five defects in 9 —
   a disjointness rule stated without the carve-out it requires, three separate claims that
   instruments existed when they did not, and a grammar paragraph that listed three of its four
   rules while the shipped schema listed all four.

   **The answers are not copied here.** An earlier version of this paragraph copied the agenda
   and it went stale within the hour. They live in the charter's amendment log and in the
   schema the engine ships, which is the only place they live.

   **The collision surface, measured 2026-09-20** rather than assumed:

   | | |
   |---|---|
   | `.sdlc/<slug>/` | safe — different slugs, different directories |
   | Test fixtures | safe — every temp path is GUID-based |
   | `CHARTER.md` | real — version number, amendment log, register |
   | `OPEN.md`, `DIRECTION.md` | real — both sessions append |
   | Git index and branch | **real, and the worst** — `git add -A` stages another session's half-finished work |
   | `bin/`, `obj/` | real — two concurrent suite runs fight over build output |
   | A shipped spec's forward pointers | real — two chains both annotate one file |

   **Works today with no framework change**, and worth adopting immediately rather than waiting
   for the check: different slugs; **never `git add -A`** — stage only the paths the chain
   declares; do not run the full suite in two sessions at once, or give one a worktree.

   **Two corrections to that advice, from scoping the intent.** Charter amendments do **not**
   serialise by construction, as an earlier version of this said — a version bump makes git
   refuse the *second push*, which is detection after both amendments are fully written, and
   two sessions in separate worktrees will both write the same next version and find out
   afterwards. And
   **a worktree per session trades one protection for another**: the gate hook can only see a
   collision when both live plans are on disk in one working tree, so worktrees buy separation
   of build output at the cost of the file-collision warning this step is building.

   **Why not a lock file**, which was the first idea considered: a lock goes stale when a session
   dies and then needs arbitration for breaking it; it serialises where this framework detects and
   refuses loudly; and nothing forces a session to take one. That last point is the subject of
   `findings-leave-only-by-decision` — a mechanism used by choice is one that eventually is not.

2. **The executor answers queries.** A query takes inputs, returns a value, touches nothing, and
   runs before any gate. It carries a defined report shape, the way the scaffold action set
   already does — that report is what makes the existing boundary testable, and it is the part
   worth copying. Add a check asserting **engine prose contains no algorithms**, which is the
   same shape as the existing check that scans engine instruction files for pack filenames.
   **This is also the first vertical** (see below), prototyped on the one case forcing it rather
   than designed in the abstract.

   **Write the capability contract before building the mechanism, and check it describes
   `scaffold.json`.** If the contract cannot describe the one vertical that already ships, it is
   wrong. This ordering is the whole point: an abstraction derived from the case forcing it, then
   validated against the case already shipped — rather than designed first and discovered later to
   fit neither.

3. **Rewrite the dot chain short.** Once derivation is a query, `solution-identifier-can-carry-a-dot`
   is roughly thirty lines: widen the identifier grammar to permit interior dots, change the
   anchors from `^`/`$` to `\A`/`\z`, and move derivation behind the query. Most of the eleven
   rounds' findings evaporate, because they were about a query mechanism that no longer needs
   inventing there.

**The parked chain keeps its evidence.** Eleven rounds of measured findings are real and worth
keeping; they belong to a chain that has not been written yet. Eleven is the number in that
chain's own parked banner, and an earlier version of this line said nine.

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

**The first step is in the order of work above, inside step 2**, because the query mechanism is
the case forcing this abstraction and is the right place to derive it from.

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
