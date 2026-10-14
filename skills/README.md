# The eighteen skills

Every skill in the plugin, in depth: when it runs, why it exists, how it is invoked, what it reads and
writes, and an example.

**This directory is the shipped surface, and it is read by both plugin systems.** One subdirectory per
skill, each holding a `SKILL.md` — that is what Claude Code loads and what an
[Agent Plugins](https://agent-plugins.org/specification) client enumerates. This file describes them
and ships nothing; the skills themselves are the directories beside it.

**A command is deterministic and the binary owns it; a skill needs language and an agent runs it.**
That split is why this document exists separately from `sdlc help` — the binary's own help lists what a
file listing and a hash can decide, and everything here is what cannot be decided that way. A skill
calls commands and shows what they print verbatim.

**How a skill starts depends on the host, and this is the one place the hosts differ.** In Claude Code you
name it. On a client that loads the skills through [Agent Plugins](https://agent-plugins.org/specification)
— Copilot, VS Code, Cursor, Codex, Kiro — there is no slash form: the client matches your request
against each skill's `description` and asks before activating one. So `/sdlc:spec 20261002_1414_12` there
is "write the spec for cycle 20261002_1414_12", and the cycle id still has to be in the sentence because
nothing else carries it.

In Claude Code, `/sdlc:<name>`:

```
/sdlc:intent We have no way to see a die roll
/sdlc:interrogate 20261002_1414_12 spec
```

Typing a stage name at the binary is answered rather than refused — `sdlc spec <id>` says it is a skill
and prints the invocation — because a tool must never send somebody somewhere it does not go.

## Before the skills

**The one-line list lives in [the skills cheat sheet](../docs/cheat-sheets/skills.md).** This page is
the long form: every skill below carries the same five parts — **When**, **Why**, **How**, **What** and
**Example** — in that order, so two skills can be compared at a glance.

**"Who starts it" is not "who runs it".** Every one of these is executed by an agent — that is what a
skill is. What differs is who decides it should happen, and two of them are a person's decision in a way
the framework enforces rather than requests: `init` writes the hook configuration that constrains the
agent, so the hook denies an agent the command; `pr` is the only stage that reaches outside the
repository, so it asks before every push.

**All eighteen are model-invocable.** Nothing in the manifest stops an agent reaching for one on its
own, and that is deliberate — what protects the decisions is the hook denying the six protected files
and `sdlc init`, plus the pull-request check, rather than a flag in a skill's frontmatter. A guard that
lives in the manifest is a guard the manifest can lose. `../docs/guides/handling-refusals.md` has what
the hook actually denies today, and what it does not.

**Six of them keep a task list where the host has one.** `init`, `intent`, `spec`, `plan` and `build`
each list their own steps and nothing past the hand-back, so the list never shows a stage a person
has not yet decided to run. `batch-build` lists one line per cycle, ticked only when that cycle
passed, and `build` keeps none of its own while a batch is running it. Every one is the agent's view
of a session and never the record — what is done is what is on disk and what `sdlc doctor`, `sdlc
status` and `sdlc cycles --pr-ready` report — and a host without a task list loses nothing, because
no step depends on one. `../CONCEPT.md` D-80 has why a stage's list stops at the hand-back, why the
batch's is shaped differently, and why the other twelve have none.

**Two words are kept apart throughout.** A **refusal** is a command or a hook declining to proceed —
`sdlc gate`, `sdlc pr-ready`, a stage skill's early check, the hook denying a write. A **blocker** is a
blocking finding on an intent, spec or plan, or a `blocked` verification. A blocker usually causes a
refusal further on, but the two are answered in different places: a refusal in
[handling refusals](../docs/guides/handling-refusals.md), a blocker in
[handling blockers](../docs/guides/handling-blockers.md).

---

## Adoption

Once per repository, before any cycle.

### `init`

**When.** Once per repository, when somebody asks to set ai-sdlc up there — and again later to refresh
what the framework owns, or to add a platform once a remote exists. It needs a git repository. **A
person starts it, and the hook denies an agent the command.**

**Why.** Adopting a process is a decision about how a team works, and `sdlc init` writes the hook
configuration that constrains the agent, the pipeline that gates the merge, and the file saying where
the code came from. So the command is a person's, and the skill's part is the two halves a command
cannot do: asking what matters here, and proposing the `LOC_` rules that follow from the answers.

It asks the person rather than reading the codebase, because reading an existing repository to decide
what to forbid would make adoption a judgement on code that predates the framework — and the first cycle
would spend itself on findings about work nobody is defending.

**Adoption decides nothing about the stack, and asks one question: where the code will live.** There is
no profile to choose. `sdlc init` writes no declared stack and no `.NET` build settings; the declared
stack is drafted afterwards for a person to cut down, and no cycle opens until it is placed.

**Holding an existing codebase to the framework's conventions is not an option on adoption, and the
skill raises it only when a person does.** An adopted solution holds new work to the code already
around it, which is the default and usually right. Moving off that is two separate choices, each made
after adoption: `sdlc draft solution --standard profile` changes which patterns a plan defaults to, and
the `.NET` build settings — `.editorconfig`, the three props files, `global.json` — are rows in the
declared stack, switched off in an adopted repository's first draft. Switching one on is a person's
decision because it applies a convention to code that never agreed to one: `Directory.Build.props`
makes warnings errors, so a build can fail on files nobody has touched. A row writes nothing — a cycle
whose plan declares the file brings it, planned and approved like any other work — and a file the
repository already had is kept.

**The origin does not move.** Where the code came from (`scaffolded` or `adopted`) is a historical fact;
which conventions its new work follows (`standard`) is a decision, and those are two keys rather than
one.

**How.** `/sdlc:init [--platform github|azure-devops|local]`. The skill says plainly that it is the
interview and writes nothing, and hands the person the command to run themselves — in Claude Code as a
line with a leading `!`, so it runs as the person's own command and its output lands in the
conversation:

1. checks it is in a git repository, and whether `.sdlc/` already exists
2. asks where the code will live, then hands over `! sdlc init --platform <answer>`
3. interviews the team — three or four questions, or two on a repository holding no code
4. runs `sdlc draft solution` for the paragraph that says what the solution is for, for a person to
   rename into place
5. runs `sdlc propose constraints` and fills in a `LOC_` entry for each answer that survives the three
   admission tests — or proposes none
6. runs `sdlc draft techstack`, edits the draft down to what is true here, and shows the rename
7. reports `sdlc constraints` and the closing block of `sdlc doctor` verbatim, and stops

It follows the one next step adoption's report names rather than a list of its own, and ends on the one
`sdlc doctor` names.

**What.** Three modes, decided by what it finds: **greenfield** for a repository holding no code,
**onboard** for existing code with no `.sdlc/`, **update** when `.sdlc/` already exists. Update refreshes
only what the framework owns, which is everything not protected — hook configs, the pipeline,
`.sdlc/README.md`.

- **Writes** nothing itself. `sdlc init` writes the scaffolding; the drafts it asks for are renamed into
  place by a person, because `solution.md`, `techstack.md` and `constraints.md` are three of the six files
  only a person writes.
- **`--platform local`** is for a repository with no remote yet, and writes no pipeline. No pull-request
  check then governs a merge and nothing mechanical decides what reaches the default branch — the choice
  is recorded in `solution.md` so `doctor` reports it as a choice rather than a broken install. Once there
  is a remote, re-run adoption with a platform; the `pr` skill walks through creating one.
- **`sdlc init` refuses outside a git repository**, and the skill says so before handing the line over.
  Without git, a build cannot record the diff a verification derives from, and the gates key on a branch
  that cannot exist — so every hook would permit everything while the artifacts still looked governed.
  The skill offers `git init`.
- **`sdlc init` answers `--profile` and `--adopt-standard`** with a sentence saying why: neither choice
  is made at adoption any more.

**Example.**

```
/sdlc:init --platform local
```

Expect two or three questions, then the `sdlc init` line to run yourself, then two drafts to rename —
the solution paragraph and the declared stack — and possibly a proposed `LOC_` constraint to paste. It
ends on `sdlc doctor`'s closing block, which names what is still to place and the one command after it.

**Next.** `/sdlc:intent`, once `sdlc doctor` names nothing left to place.
[Adopting the framework](../docs/guides/adopting-the-framework.md) and
[declaring the stack](../docs/guides/declaring-the-stack.md) walk through it.

---

## The planning chain

Three artifacts, all required, always. Each is written, challenged in a fresh process against the rules
in force, and revised until a round raises nothing blocking. A stage will not run until the artifact
before it has such a round.

### `intent`

**When.** Opening a cycle: somebody wants to start new work, reports a problem, or has an idea to turn
into something the process can carry. The declared stack must be in place, or `sdlc new` refuses. A
person starts it.

**Why.** An intent says *why* the work exists, and most requests arrive shaped as a solution ("add a
cache") rather than a problem ("the dashboard takes fourteen seconds and people stop opening it"). The
person knows what they want; they do not yet know what is underspecified about it. So this is the only
artifact written by interview, and the skill's job is to elicit rather than to draft. A decision already
taken is kept rather than discarded — recorded under `Fixed decisions` with who decided it and when — so
the intent stays a problem statement while the plan can still honour the decision.

**How.** `/sdlc:intent [a sentence describing the work]`.

1. `sdlc constraints --for intent`, kept in front of it
2. `sdlc new`, which allocates the cycle id and claims it — that claim *is* the collision check — then
   `git switch -c cycle/<id>`
3. converts the request into a problem, then interviews: what happens today that shouldn't, who notices,
   what you would see once it is right, what must not change — asked in the tense the work is in, so a new
   solution is asked what nobody can do today rather than what is broken
4. where the person bounds the work with *simple*, *basic* or *nothing fancy*, asks once whether that
   means small in scope or plain in appearance
5. writes the intent, then runs the interrogate-refine loop itself until a round raises nothing blocking,
   a reader reports a conflict, or the panel's `Loop` line says it is stuck on one rule
6. shows `sdlc status <id> --stage intent --brief` verbatim and stops

**It does not chain into the spec.** That hand-back is the human-in-the-loop point, and a skill that
continued would remove it while appearing helpful.

**What.** Reads the constraints in force and the conversation. Writes `.sdlc/cycles/<id>/intent.md` —
problem, wanted outcomes, fixed decisions, what must not change, and a `## Open` section that is required
even when empty — and the findings rounds the loop produces. It never asks how to build the thing,
anything about the stack, or criteria in testable form; those belong to the plan, the declared stack and
the spec. It does not cap the rounds: a loop that is converging can take eleven.

**Example.**

```
/sdlc:intent We have no way to see a die roll
```

Expect a cycle id and branch, a handful of questions, then several interrogation rounds without your
involvement. It ends on the intent's status panel, whose last line is the next command.

**Next.** `/sdlc:spec <id>`, when you are satisfied with the intent.

### `spec`

**When.** The intent has a clean interrogation round. A person starts it.

**Why.** A spec says what *done* means, and every criterion in it has to be able to fail. A criterion
that cannot fail passes every gate and proves nothing; a criterion that narrows its outcome passes every
gate and ships the wrong thing — a die specified as a "face value" shipped as a numeral in a bare
paragraph tag, because nothing downstream compares a spec to its intent again. So the skill carries each
outcome's own words into its criterion, and puts any part it cannot check under `Out of scope` where a
person can see it went.

**How.** `/sdlc:spec <cycle id>`.

1. `sdlc constraints --for spec`
2. `sdlc status <id> --stage intent`, to refuse early
3. reads `intent.md` and derives criteria according to its `type` — a `bug fix` from the reproduction, a
   `feature change` with a criterion for each `Must not change` item
4. disposes of every entry under the intent's `## Open`
5. writes the spec and runs the same interrogate-refine loop as the intent
6. shows `sdlc status <id> --stage spec --brief` verbatim and stops

**What.** Reads `intent.md` as its only input: not the request that started the cycle, not the
conversation, not the codebase for requirements. Writes `.sdlc/cycles/<id>/spec.md` — numbered acceptance
criteria, `Out of scope` with reasons, and `## Open`. Every criterion's check has to be one this
solution's testing layers can actually run (`CON_12`). Every intent `## Open` entry ends in exactly one of
three places — a criterion, `Out of scope` with a reason, or the spec's own `## Open` citing the intent
item.

- **Refuses** when there is no intent, or the intent is not clean.
- **Refuses** when a spec already exists, and names `/sdlc:interrogate <id> spec` or `/sdlc:steer`
  instead. It never overwrites one.
- **Stops** when the intent is missing something it should have asked for, rather than writing the
  criterion — that is `steer`'s job.

**Example.**

```
/sdlc:spec 20261002_1414_12
```

Expect a short run with no questions unless a finding needs a person, ending on the spec's status panel.

**Next.** `/sdlc:plan <id>`.

### `plan`

**When.** The spec has a clean interrogation round. A person starts it. It is the last document written
before a person approves, so it is the one a reviewer reads most closely.

**Why.** A plan says *how*, and commits to what will prove each criterion. The constraint to watch is
proof adequacy (`CON_06`). *What would have to be true for this check to pass while the criterion is
still unmet?* If that question has an answer, the proof is not adequate yet — and an inadequate proof is
worse than none, because it produces a green verification instead of an obvious gap.

Unlike the spec, this stage must read the codebase — a file table invented from the spec alone is the
fastest way to a plan that cannot be built. It holds two kinds of claim and only one is judged: the
approach in `Shape and departures` is a decision, and the file table is an estimate that nothing gates.

**It reads the declared stack and compares it with what is there.** `sdlc stack` prints what the
solution declares and ends with the one difference a file listing can decide: the build settings files
the stack takes that are not on disk. The rest of the comparison — a declared default the code does not
use, or something the code uses that the stack never mentions — is the plan's to make by reading. A
build settings gap is closed only by a plan whose intent asks for it, which declares the missing files
and plans for what they break; a plan about anything else names the gap in a sentence and leaves it.

**Departing is legal; departing in silence is not.** `CON_15` asks a plan to say what the declared stack
would have cost when it departs from it, in either direction, and `CON_19` asks the same about a recorded
decision.

**How.** `/sdlc:plan <cycle id>`.

1. `sdlc constraints --for plan`
2. `sdlc status <id> --stage spec`, to refuse early
3. reads the spec and the code, then `sdlc stack`, `sdlc decisions` and `sdlc theme`
4. decides the shape, argues each departure, writes a proof for every criterion and chooses the tier —
   `local`, `contract` or `data`, by how reversible the change is
5. writes the plan and runs the interrogate-refine loop
6. shows `sdlc status <id> --stage plan --brief` and `sdlc gate <id>` — which refuses, because nobody has
   approved yet — and explains that approving is the person's act

**What.** Reads `spec.md`, the codebase, the declared stack, the decision record and the theme. Writes
`.sdlc/cycles/<id>/plan.md`: `Shape and departures`, a `Files` table, numbered `Proof` entries each
naming its test in backticks, `Risk`, and `## Open`. The pull-request check reads those test names out
and confirms each one ran and passed.

- **Refuses** when there is no spec or it is not clean.
- **Refuses** when a plan already exists, and names `/sdlc:interrogate <id> plan` or `/sdlc:steer`.
- **Never declares** one of the six files only a person writes; the schema refuses a plan that does.

**Example.**

```
/sdlc:plan 20261002_1414_12
```

Expect it to read the code before it writes, then the loop, then a panel and a gate refusal naming
approval as the missing piece.

**Next.** A person approves with `/sdlc:draft approval <id>` and a rename — see the gate below.

---

## The loop

The two skills every stage above runs in turn until a round is clean. A person can also run either
directly.

### `interrogate`

**When.** An artifact — intent, spec, plan or verify document — has just been written or changed. The
stage skill's loop starts it, or a person does.

**Why.** **The fresh process is the whole mechanism.** The reader must not have seen the artifact being
made, so the judgement runs in a new process — not in the authoring session, and not as a subagent. A
tool call runs in its own process while the model calling it is still the author; a new process creates a
new reader rather than handing data to the one you already have.

What that buys is the removal of *investment bias*: the reader never saw the interview, the drafts or the
author's reasoning, so it has nothing to defend. What it does not buy is independence from *model bias* —
a borderline judgement flips the same coin twice, so a round is a second reading by a reader with no
stake, never independent verification.

**It is also the only place a blocking finding can be cleared.** The round records that an earlier
finding this reader did not raise is now `answered`, or `withdrawn` where the objection was dropped
rather than answered. A finding the reader raises again stays open, and so does one that cannot be
placed in either case — the reader's silence is the evidence, and the authoring session's opinion of its
own corrections is not. `../CONCEPT.md` D-49 has the reasoning, and D-27 is why there is no other exit.

**How.** `/sdlc:interrogate <cycle id> <intent|spec|plan|verify>`. It runs
`sdlc constraints --for <artifact>`, starts a fresh non-interactive process of the host, hands it the
judging prompt, writes what comes back as the next round, then shows `sdlc status <id> --stage
<artifact>` verbatim with at most two lines of its own.

**What.** **What it is given is the constraints in force, the artifact, and the artifact above it — and on
a plan round, `techstack.md` as well.** The line is between a rule source and the cycle's own history: the
first predates the cycle and is what the document is judged against, the second is what the new process
exists to withhold. `CON_15` is a rule entirely about conformance to the stack file, so a reader without
it could only ever object to a departure the plan had already admitted to (D-58).

It writes `.sdlc/cycles/<id>/findings/<artifact>.r<n>.json`. Rounds accumulate and are never rewritten. A
finding keeps its id across rounds, cites exactly one rule by id and by that rule's own heading, and every
round records `upstream_sha256` — the hash of the artifact above it, as the reader saw it. That is what
makes a stale judgement detectable when an upstream document is edited afterwards. A reader that finds
two rules whose remedies exclude each other reports a conflict rather than picking a side.

**It never edits the artifact**, and it does not recommend whether to run another round.

**Example.**

```
/sdlc:interrogate 20261002_1414_12 spec
```

Expect a new findings round on disk and the spec's panel showing what it found. An empty round is a clean
reading, and it clears nothing an earlier round raised unless that finding is marked answered or
withdrawn.

### `refine`

**When.** An interrogation round raised blocking findings, and the cycle is not yet approved. The stage
skill's loop starts it, or a person does.

**Why.** The separation from `interrogate` is the point: an interrogation that could also fix things
would grade its own corrections.

It may disagree with a finding — not every finding is right. What it may not do is answer one by making
the artifact vaguer, which is the single move that removes a problem from the page while leaving the
belief in it. "Fourteen seconds" becoming "slow" is not a fix.

**How.** `/sdlc:refine <cycle id> <intent|spec|plan>`. It reads the panel's `Loop` line first — a rule
that *has come back* means look for an exit rather than edit the same words again, and *stuck on* means
do not edit at all. Then it edits the artifact, reports one line per finding, shows `sdlc status <id>
--stage <artifact>` verbatim and stops. It does not interrogate again; that is the stage skill's loop.

**What.** **Edits one planning document and nothing else, and never judges.** Every finding ends in
exactly one of three exits, and there is no fourth: **healed** (the artifact changes), **assumed**
(recorded in the decisions region, marked unconfirmed), **asked** (recorded in `## Open`, and the skill
stops and asks). Never assume silently — a report is read once, and a section is read every time the
artifact is read.

It never touches a findings round. What it does instead is **report every id it was given**, saying for
each whether it changed the document or disagreed and left it alone — because the next round cannot tell
those apart by reading the artifact, and they become different dispositions. An id left out of the
report is one the next round cannot place, and by D-49 an unplaceable finding stays open.

- **Refuses** when no blocking finding is open.
- **Refuses once the cycle is approved:** the documents are hash-locked at that point, so editing one
  would break the gate rather than satisfy it. The recovery is a recorded reopen,
  `sdlc draft approval <id> --reopen`.
- **May never edit** source, tests, `approval.md`, or any other artifact.

**Example.**

```
/sdlc:refine 20261002_1414_12 spec
```

Expect a three-column report — the finding id, the exit it took, what was done — followed by the panel.

---

## Correction

### `steer`

**When.** A person changes their mind about *how* a cycle is being done, after the spec exists and
before the gate. A person starts it.

**Why.** It exists because of cost. *"Use the existing cache abstraction rather than a new one"* arrives
mid-cycle, and doing that by hand means editing and re-interrogating three documents. Nobody pays it, so
the correction ends up in conversation where it is lost — and a path so costly that it pushes information
out of the system is worse than no path. No constraint loosens.

**Steering changes how, not what.** Which approach, which abstraction, which order, what to avoid. A
comment that changes the problem is a different intent, and the skill says so and offers a new cycle.

**How.** `/sdlc:steer <cycle id> "what to change"`. One or two questions — what to do instead, and why —
then it records the correction, edits each artifact it reaches, re-interrogates everything downstream of
what it touched, reports one line per artifact, and shows `sdlc status <id>` verbatim.

**What.** Records the correction under the intent's `Fixed decisions`, with attribution, and edits rather
than regenerates — a regenerated artifact has a findings history about a document that no longer exists.
Usually the spec needs nothing and the plan's `Shape and departures`, `Files` and often `Proof` change.
The re-interrogation is checked rather than trusted: a round records the hash of the artifact above it,
so editing the intent makes the spec's latest round stale.

- **Refuses** when there is only an intent — refine it instead.
- **Refuses** when the cycle is approved, and names `sdlc draft approval <id> --reopen` first.
- **Refuses** when the cycle is abandoned, which is terminal.

**Example.**

```
/sdlc:steer 20261002_1414_12 "use the existing cache abstraction rather than a new one"
```

Expect a question about why, a new dated and attributed line in the intent's `Fixed decisions`, edits to
the plan, and fresh rounds on everything below the intent.

---

## The gate

### `draft`

**When.** A person needs to approve or refuse a cycle at the gate, stop a cycle, or declare or change what
the solution is or is built from. A person starts it.

**Why.** Six files are written only by a person — `approval.md`, `abandoned.md`, `constraints.md`,
`decisions.md`, `solution.md` and `techstack.md` — and **a recorded decision means something precisely
because no tool could have recorded it.** Every agent write to them is denied at the tool call, by file
path and by shell command alike. There is no flag, no mode, and no approve command for an exception to be
carved out of. But a person who has to look a format up is a person who gets it wrong or puts it off, so
this skill prints or writes what belongs in four of them. Three of the four write a complete file beside
the real one so that placing it is a rename rather than a paste — which matters because a block printed
for pasting went unpasted through a whole cycle, leaving every plan in that solution reading a
placeholder.

A refusal at the gate is drafted on the same terms as an approval — `--changes-requested`, `--deny`,
`--reopen` — because rejecting is what a person does when they are least inclined to look a format up,
and helping with the easy case while withholding help from the hard one is the asymmetry people resent.

**The other two are `propose`'s, and the verbs split on it.** A constraint's wording *is* the decision,
so there is no finished entry to hand back. See `propose` below.

**How.** `/sdlc:draft approval <id> | abandon <id> | solution [--standard profile|existing] | techstack
[--empty]`. It runs the matching command and shows what it prints verbatim, including the instructions
beneath it, and adds nothing:

```
sdlc draft approval <id>     [--changes-requested | --deny | --reopen]
sdlc draft abandon <id>
sdlc draft solution          [--standard profile|existing]
sdlc draft techstack         [--empty]
```

It shows the rename that places the file and **does not run it, or offer to**: the rename is denied in
every spelling and through every shell the host offers, and the skill does not read that denial as a
configuration problem.

**What.**

- **`approval <id>`** — the gate decision, covering all three planning documents, with a hash of each.
  Written as a complete file beside the ledger, named for its decision, and placed by renaming it. A
  revisit reproduces every decision already recorded above the new one, because the ledger is
  append-only and the rename overwrites. The three refusal flags print the `reason` key empty, and the
  gate rejects an entry that refuses a cycle without saying why.
- **`abandon <id>`** — why a cycle stopped. Terminal, and the reason is left empty because the file is
  rejected without one. Printed for a person to place by hand.
- **`solution`** — the whole `solution.md`, written beside itself for a rename. Keys carried through as
  recorded and prose through unchanged, so renaming can neither reclassify the solution nor cost anybody
  what they wrote. `--standard profile|existing` moves that one key, and says so.
- **`techstack`** — the declared stack for a rename: a byte copy of it, the framework's default where
  none is placed yet, or nothing with `--empty`. **And the options menu as a report**, under
  `.sdlc/reports/`: every concern the shipped profiles know about, every stack rule the framework ships,
  and every build settings file with what it does and what it costs. What is in the placed file is
  exactly what binds. The menu is written fresh every run and never renamed into place — a declared
  stack listing answers the solution has not chosen is one a plan can reach into and truthfully not be
  departing from anything.

**`draft approval` refuses to print** while a blocking finding is open, while a document is missing,
while one has never been interrogated, and while a round judged a document that has changed since. **A
blocking finding cannot be waived by any mechanism** (D-27): there is no field for it in the entry and no
refusal code at the gate that acceptance would clear. It is answered, or the cycle does not advance. If it
genuinely cannot be answered, the move is `draft abandon` and a new cycle. There is no `draft profile`
any more, and the binary says so.

**Example.**

```
/sdlc:draft approval 20261002_1414_12
/sdlc:draft approval 20261002_1414_12 --changes-requested
```

Expect a file named for its decision written into the cycle directory, the command that renames it onto
`approval.md`, and a note of how many earlier decisions it carries. Run that rename yourself; the agent
cannot.

**Next.** `/sdlc:build <id>` after an approval; the stage that needs work after a refusal.

---

## Build and verify

### `build`

**When.** A person has approved the cycle's three planning documents and the gate is satisfied. A person
starts it, or `batch-build` does.

**Why.** It is the first stage that writes code, and the first a person is not expected to watch, so
everything a verification will rely on has to be recorded rather than remembered. It implements what the
plan decided and only that (`CON_07.2`): creep inside an approved file is what reaches review dressed as
approved work, which is where it is least likely to be questioned. Something worth fixing that the plan
did not describe is left alone and recorded as a `Noticed:` line in the commit message, where the person
reviewing the pull request will meet it.

It checks that the suite actually ran: a trial cycle lost its whole verification to `dotnet test` exiting
zero without running anything, because the runner was not configured. And **the turn end is checked as
well**, on both hosts — a hook asks the gate before the agent finishes and sends the turn back on three
things: recorded evidence showing a failing suite, code written with no evidence recorded at all, and
code that has moved since the evidence was recorded. It is not a rule the skill has to remember — it is
why there is no point reporting done over a red build.

**How.** `/sdlc:build <cycle id>`.

1. `sdlc gate <id>`, and stops if it refuses
2. `git switch cycle/<id>`
3. implements the plan — running `sdlc scaffold <id>` where the plan declares the solution skeleton or a
   build settings file the repository does not have
4. `dotnet build` and `dotnet test`, reading the test count rather than the exit code
5. commits the change, with any `Noticed:` lines
6. `sdlc evidence <id> --tests <directory> --build-log <path>`, then commits the evidence
7. shows `sdlc status <id> --brief` verbatim with at most five lines of its own, and stops

**What.** Reads `plan.md` and the code. Writes source and tests, and the evidence verify will read: the
diff, the commit it is measured from, the runner's own output, and one normalised test summary. **Verify
reads only these files**, so a verdict derived from the builder's recollection is a verdict nobody can
check (`CON_08`). `sdlc evidence` also prints what changed against what the plan declared —
`undeclared`, `undelivered`, `governing` — none of which is a refusal; a `governing` line goes in the
report and the commit message.

**`sdlc scaffold <id>` is how two kinds of file arrive, and the plan is what asks for either.** On a new
solution's first cycle it writes the skeleton and the build settings it compiles under. On a solution
adopted into existing code it writes only the build settings files the plan declares — and where
`Directory.Build.props` is one of them, the build that follows can fail on code the cycle never
touched, which the plan was required to have planned for. Warnings are errors, enforced by the build
props rather than by instruction.

- **Refuses** when the gate does. There are eight ways it can — no cycle, abandoned, a missing document,
  nobody approved, a refusal standing, a document changed since approval, a blocking finding still open,
  or a round that judged a document which has moved since — and each has a different next step.
- **Stops** on a red build or failing tests, and says so. It does not adjust a test to make it pass.
- **Keeps no task list of its own** inside a batch.

**Example.**

```
/sdlc:build 20261002_1414_12
```

Expect commits on the cycle's branch — the change, then the evidence — and a short hand-back saying what
was implemented, what the build and tests said, and how many `Noticed:` lines the commit carries.

**Next.** `/sdlc:verify <id>`.

### `verify`

**When.** The build has recorded its evidence. A person starts it, or `batch-build` does.

**Why.** Verify is the only artifact whose entire value is being derived. A claim that reaches beyond its
evidence turns an auditor into an author. So it reads the evidence rather than running the tests —
running them would produce new evidence instead of adjudicating the build's, and the next reader could
not tell which run the verdict describes. It does not read the source to decide whether a criterion is
met: reading the implementation and concluding it looks correct is the most tempting way to write a claim
with no evidence behind it.

**How.** `/sdlc:verify <cycle id>`. It runs `sdlc constraints --for verify`, adjudicates criterion by
criterion, writes the verify document, interrogates and refines it like any other artifact, then shows
`sdlc status <id>` and `sdlc pr-ready <id>` verbatim with at most two lines of its own.

Per criterion: which test did the plan name, does the summary say it ran and passed, and does that test
actually establish the criterion? The third question is where judgement lives, and where the verification
may disagree with the plan — a test that passed but does not reach its criterion is **not met**.

**What.** Reads `spec.md`, `plan.md` and the evidence — `tests.json`, `diff.patch`, `base.sha`, the
runner's output and `build.log`. Writes `.sdlc/cycles/<id>/verify.md`: an entry for every criterion,
numbered to match, each citing the evidence it rests on (`CON_08`), and `## Open` for limits of the
evidence. **Never edits code.**

It writes `pass` or `blocked`. **`escalated` is computed, not declared**: it is what a remediation loop
that stopped making progress makes of a blocked cycle (D-66). `sdlc verify-chain` checks mechanically
that every criterion has an entry and that a `pass` rests on named tests that passed. If the evidence is
missing, it says so and stops — that is a build that did not finish.

**Example.**

```
/sdlc:verify 20261002_1414_12
```

Expect `verify.md` with a verdict per criterion, and the outcome in a line or two. On `blocked` it does
not start remediating; you decide.

**Next.** `/sdlc:pr <id>` on `pass`; `/sdlc:remediate <id>` on `blocked` — see
[handling blockers](../docs/guides/handling-blockers.md).

### `remediate`

**When.** Verify recorded `blocked`. A person starts it, or `batch-build` does.

**Why.** `refine` and `remediate` are two editors with opposite guards, and the asymmetry is the whole
design: if a failing test could cause an agent to adjust the spec, the criteria become whatever the code
happens to do. So when the honest conclusion is "the criterion is wrong", the skill says it and stops.

**It stops when it is not getting anywhere, not after a set number of tries.** It stops when a pass
undoes what an earlier one answered, or when two passes in a row reduce nothing. That stopping is the
only reason an unattended batch is safe to run at all. The blind three it replaced failed an honest cycle
that needed a fourth pass and spent all three on a cycle making no progress at all, and there is
deliberately no ceiling behind the progress rule now (D-66).

**How.** `/sdlc:remediate <cycle id>`. It reads `sdlc pr-ready <id>` to know whether the loop is still
making progress, works the latest `findings/verify.r<n>.json`, then `dotnet build`, `dotnet test` and
`sdlc evidence <id> --tests <results> --build-log <log>`, commits on the cycle's branch, and shows
`sdlc status <id>` verbatim. A pass is a verify findings round after the first, so progress is read from
what happened rather than from a counter anybody could edit.

**What.** Reads the verify findings and the code. **Edits source and tests, never a planning document.**
It fixes the cause rather than the symptom — no loosened assertion, no sleep or retry, no substituted
thing under test, no skipped test.

- **Stops** when the criterion itself looks wrong, and names `sdlc draft approval <id> --reopen`, which
  withdraws the approval with a recorded reason.
- **Stops** on a regression — a pass that broke what an earlier pass fixed.
- **Does not verify its own remediation.** A verification by the session that wrote the fix is the
  investment bias the whole separation exists to remove.

**Example.**

```
/sdlc:remediate 20261002_1414_12
```

Expect one line per finding — what was wrong, what changed, and whether it is confident that was the
cause — then the panel. Verify again as its own stage.

---

## Pull requests and unattended runs

### `pr`

**When.** A verification passed. **A person starts it, and it asks before every push.**

**Why.** It is the only stage that reaches outside the repository. Everything before this is local and
reversible; a pull request notifies people, appears in a queue, and withdrawing it is something somebody
has to notice happened. So it asks `sdlc pr-ready` first, every time, and does not push past a refusal —
the pull-request check would refuse it anyway, so pushing would cost a round trip through CI to learn
what it was just told.

**It is not approving the merge.** A code owner does that, and never the agent that wrote the change. The
local approval defended against an agent that forgets; the review is what defends against a human who did
not look, and they are different jobs.

**How.** `/sdlc:pr <cycle id> | --all`. It runs `sdlc pr-ready <id>`, then `sdlc doctor` and
`git remote -v` to check there is somewhere to push, then `git push -u origin cycle/<id>` and the
platform's own tool — `gh pr create` or `az repos pr create`. With `--all` it shows
`sdlc cycles --pr-ready` verbatim and asks once before the first push; the question is about volume
rather than permission, because every cycle that ever passed is eligible, including one somebody has
forgotten.

**What.** The body is the cycle rather than a summary somebody invented: the intent's problem in the
intent's words, the criteria with the verification's verdict on each, the tier, the risk section, and
anything in an `## Open` region, because a reviewer should meet the limits before they approve. It links
the work item where the solution tracks them.

- **Refuses** when `sdlc pr-ready` does: not verified, did not pass, escalated, or the chain broken since
  verification.
- **Stops** when the solution is `local` or has no remote, and walks the person through creating one —
  then re-running adoption with a platform, and making the check required in branch policy.
- **The Azure DevOps path has never been run** (D-32), so there it shows the exact command before running
  it.
- **With `--all`**, a failure on one cycle does not stop the others.

**Example.**

```
/sdlc:pr 20261002_1414_12
/sdlc:pr --all
```

Expect a confirmation before anything is pushed, then one line per cycle with its id, branch and pull
request link. The merge is a reviewer's.

### `batch-build`

**When.** Several cycles are approved and waiting in this working copy. A person starts it.

**Why.** It completes every approved cycle in this working copy — builds and verifies each, concurrently
where their declared files do not collide. A local skill, executed rather than subscribed to: there is no
shared queue and no daemon.

**Everything here rests on the remediation bound.** A cycle that cannot be made to pass stops rather
than opening a pull request, and that stopping is the only reason this is safe to run without watching.
**There is no ceiling behind it (D-66)**, so a loop that keeps showing small improvements can run for a
long time — which is the one thing to watch on an unattended batch.

**It schedules around collisions rather than resolving them.** A merge you did not have to do is worth
more than a merge you did well.

**How.** `/sdlc:batch-build [--pr]`.

1. `sdlc cycles --pr-ready` for the work, and `sdlc cycles --claims` for the claim graph — read from the
   binary rather than worked out, because a graph derived by reading plans would come out slightly
   differently each run while deciding what runs concurrently
2. runs each group in parallel, each cycle in its own worktree; cycles in different groups collide, so
   they serialise in cycle-id order
3. caps concurrency where a plan stands up a backing service, and exports `SDLC_RUN_MODE=batch` and a
   fresh `SDLC_RUN_ID` into each cycle's worktree
4. per cycle, the build stage then the verify stage, remediating while it makes progress
5. reports one line per cycle, then `sdlc cycles --pr-ready` verbatim, and stops

**What.** Each cycle ends `passed`, `blocked`, `escalated` or not attempted. **A sibling's failure does
not discard the cycles that completed** — each can open its own pull request. Its task list is one line
per cycle, ticked only on `passed`. A conflict on a file no cycle can claim exclusively goes to
`resolve-conflict`.

**Plain `batch-build` never pushes.** `--pr` composes it with the `pr` skill and asks once before the run
rather than at the end, because the question is about volume and a person should answer it before
waiting rather than after.

**Example.**

```
/sdlc:batch-build
/sdlc:batch-build --pr
```

Expect a line per cycle that moves to done only when that cycle passed, and a closing report of one line
per cycle naming what stopped anything that did not.

### `resolve-conflict`

**When.** A batch run reports a collision on one of the few files no cycle can claim exclusively. A
person starts it, or `batch-build` does.

**Why.** **Enumerated rather than discovered.** A skill that resolved whatever happened to conflict would
quietly become the thing that merges real code, and the moment it does, scheduling around collisions
stops being the strategy. These files are additive by nature, which is why they can be resolved at all:
two cycles each add a package, a registration, a setting, and the resolution is to keep both.

**How.** `/sdlc:resolve-conflict <file path>`. It keeps both sides, then runs `dotnet build` and
`dotnet test` for both cycles, re-records `sdlc evidence <id> --tests <results>` for each cycle whose
files it touched, and reports in at most four lines.

**What.** The list: project files and `Directory.Packages.props`, dependency-injection registration,
`appsettings`, EF Core migrations, Terraform near state.

- **Declines** a file not on the list — that is a scheduling failure to report, not a merge to perform.
- **Stops** on the same key with two different values, which is not additive: that is two plans
  disagreeing about the solution, and a person's to settle.
- **Does not merge migrations.** Two branches that each add a migration snapshot the same parent model,
  and the merged chain compiles and then fails at deploy. The resolution is to rebase and regenerate, and
  a batch run should have serialised them.
- **Never pushes.**

**Example.**

```
/sdlc:resolve-conflict src/Api/Api.csproj
```

Expect both cycles' additions kept, both suites run, fresh evidence for each, and four lines saying what
was kept from each and anything declined.

---

## Reading and deciding

Five skills that read the record and change nothing in a cycle. Anyone may run them at any time.

### `status`

**When.** After any stage, or whenever somebody asks where something stands. Anyone, constantly.

**Why.** The binary prints the panel, not the model. The format is a contract, and a skill that
reformatted it would make the thing people actually read drift between hosts.

**What it will not tell you, on purpose: whether to keep interrogating.** That judgement has been wrong
before — on one real intent, round seven found the headline change was unreachable and round eleven found
that no success criterion failed without it. A tool that suggested stopping at round four would have been
confidently wrong.

**How.** `/sdlc:status <cycle id> [intent|spec|plan]`, which runs `sdlc status <id> [--stage <artifact>]`
and shows it verbatim. Without a stage, the panel is for whichever artifact the cycle is waiting on, which
is what a person wants after any stage finishes.

**What.** Reads the cycle's documents, rounds and approval. Writes nothing. The panel shows **Says** (the
document's headings), **Shape** (what the schema found), **Checks** (each round, and the open blocking
findings with the rule each cites), **Gate**, and **Next** — the literal command. Asked for its own
reading, the agent adds at most three lines under `My reading:`; unasked, nothing.

**Example.**

```
/sdlc:status 20261002_1414_12
/sdlc:status 20261002_1414_12 plan
```

Expect the panel and nothing else; its last line is the command to copy.

### `constraints`

**When.** Before writing or judging any artifact, and whenever somebody asks what the rules are. Every
stage skill runs it as its first step.

**Why.** The constraints live in the framework and are never copied into a solution, so a session that
did not run this is working from recollection. Running it is not a convenience; it is how the rules
reach the agent.

**How.** `/sdlc:constraints [intent|spec|plan|diff|verify]`, which runs
`sdlc constraints [--for <artifact>] [--brief]` and shows it verbatim, adding nothing, summarising
nothing, reordering nothing.

**What.** **Shows the rules in force for this solution, all tiers resolved** — the global `CON_` set, the
`PRO_` stack rules its declared stack switches on, and the solution's own `LOC_`, in that order, so a
solution's own rules are the last word. Each constraint has a heading, `Applies to`, `Forbids` and `Why`.
Writes nothing.

**Example.**

```
/sdlc:constraints plan
```

Expect the rules that apply to a plan in this solution. A missing profile tier means the declared stack
switches no stack rule on.

### `suggestions`

**When.** Somebody asks what has been noticed and not acted on, or is looking for small work worth
picking up. A person starts it.

**Why.** Non-blocking is not trivial. Two real ones from a single trial cycle: `dotnet test` did not
actually run the suite because the runner was not configured, and a criterion's proof rested entirely on a
substituted source. Both were substantive; neither cited a rule that forbade the current state, which is
the only thing that made them suggestions.

No ranking and no ages, because a suggestion's age says nothing about its worth and a tool that sorted by
age would quietly argue that the oldest matters most.

**How.** `/sdlc:suggestions [cycle id]`, which runs `sdlc suggestions [<id>]` and shows it verbatim. No id
lists every cycle. Asked which to pick up, the agent adds at most two lines under `My reading:`.

**What.** **Two stores, listed apart.** A suggestion was raised by a fresh reader judging a document and
carries a round and an id. A `Noticed:` line was written by a build that saw something it was not allowed
to fix, and lives in that build's commit message on the cycle's branch. Only the rounds used to be read,
so the three a dice-roller cycle's build wrote were invisible. Writes nothing.

**It aggregates nothing, on purpose.** What keeps going wrong *across* cycles is `propose constraints`,
below. This answers a narrower question — what is outstanding — and the two read the same files for
opposite halves of them.

**Example.**

```
/sdlc:suggestions
```

Expect findings and `Noticed:` lines under separate headings, unsorted. A cycle not yet built, or merged
with its branch gone, contributes no `Noticed:` lines.

### `propose`

**When.** Somebody asks what keeps going wrong across cycles, wants to add one of this solution's own
constraints, wants to record a decision, or needs a theme. A person starts it; `init` runs
`propose constraints` during adoption.

**Why.** **The licence, which is the whole design in one line:** a tool may propose a rule when it brings
the evidence, and may not shape a rule somebody just typed. Every candidate arrives with the cycles and
finding ids behind it, so a person refuses one on the record rather than on taste. There is no way to
pass wording in.

It is the second verb beside `draft`, split by who made the decision: `draft` is for one somebody already
took and hands back a finished file; `propose` is for one they have not, and argues from the record.

**How.** `/sdlc:propose constraints | decisions | theme`, which runs `sdlc propose <subject>`, shows it
verbatim, then does the reading the command cannot under headings of its own. Asked for no subject, it
asks rather than guesses.

**What.** Three subjects, three different records. **None is placed by the skill** — `constraints.md`
and `decisions.md` are written only by a person, and a person pastes what they accept.

- **`constraints` is mined backward from the findings.** A rule cited in **three or more distinct
  cycles** is a pattern — distinct *cycles*, not findings, because three findings inside one cycle is one
  artifact going round the loop. It reads what `suggestions` deliberately cannot, closed findings and
  blocking ones. Candidates come in four sections — add, reword, supersede, and *belongs below a
  constraint*, usually the biggest, routing mechanical candidates to an analyzer, an architecture test or
  `BannedSymbols.txt`, all of which fail a build. **Nothing is printed that would delete a rule**; a rule
  out of force gains `Superseded-by`. A refusal has a home under a hand-written `Considered and rejected`
  heading, which the command reads and stays quiet about. **Empty-handed is an ordinary answer**: it
  prints the next free `LOC_` id with its four parts empty and the three admission tests beside them.
- **`decisions` is captured forward**, because a choice that never collided with a rule produced no
  finding to mine. It reads what **approved** cycles stated, from an intent's `Fixed decisions` and a
  plan's `Shape and departures`, and skips a cycle whose approval was reopened or whose work was stopped.
  The approval is the whole provenance story: it hashes the three planning documents against a person's
  name. Each candidate becomes a `DEC_` entry with `Decided`, `Because`, `Instead of` and `From`.
- **`theme` is neither mined nor captured.** A theme is a register, so what gets proposed is the shape —
  the roles — and never a value: a colour is a brand's, and a tool inventing one is the only proposal
  here nobody asked for. `.sdlc/theme.json` is not protected, so a cycle may declare it and change a
  value.

**Departing is legal from two of the three, and silence is what gets caught.** `CON_19` asks a plan
contradicting a recorded decision to say what that decision would have cost, the same shape `CON_15`
takes for the declared stack. The theme is the one nothing in governance objects to, because a colour
outside the theme is mechanical and belongs to a linter.

The command writes its answer to `.sdlc/reports/`, and nothing reads a report back.

**Example.**

```
/sdlc:propose constraints
/sdlc:propose decisions
```

Expect candidates labelled `P1`, `P2` rather than numbered ids, each with the cycles and finding ids
behind it, and the next free id stated once. A young solution gets the empty entry and the three tests.

---

## Where to go next

| | |
|---|---|
| every skill in one line each | [`../docs/cheat-sheets/skills.md`](../docs/cheat-sheets/skills.md) |
| every command, on one screen | [`../docs/cheat-sheets/commands.md`](../docs/cheat-sheets/commands.md) |
| every refusal, on one screen | [`../docs/cheat-sheets/refusals.md`](../docs/cheat-sheets/refusals.md) |
| installing the plugin and the binary | [`../docs/guides/installing-the-plugin.md`](../docs/guides/installing-the-plugin.md) |
| adopting the framework into a repository | [`../docs/guides/adopting-the-framework.md`](../docs/guides/adopting-the-framework.md) |
| declaring what the solution is built from | [`../docs/guides/declaring-the-stack.md`](../docs/guides/declaring-the-stack.md) |
| a cycle from intent to pull request | [`../docs/guides/running-a-cycle.md`](../docs/guides/running-a-cycle.md) |
| a blocking finding or a blocked verification | [`../docs/guides/handling-blockers.md`](../docs/guides/handling-blockers.md) |
| a command or hook that refused | [`../docs/guides/handling-refusals.md`](../docs/guides/handling-refusals.md) |
| one whole cycle, prompt by prompt | [`../docs/samples/rolling-a-die.md`](../docs/samples/rolling-a-die.md) |
| the design, and every decision behind it | `../CONCEPT.md` §15 |
