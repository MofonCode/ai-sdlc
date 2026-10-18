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
/sdlc:intent --type bug
/sdlc:interrogate 20261002_1414_12 spec
```

Typing a stage name at the binary is answered rather than refused — `sdlc spec <id>` says it is a skill
and prints the invocation — because a tool must never send somebody somewhere it does not go.

## Before the skills

**The one-line list lives in [the skills list](../docs/reference/skills.md).** This page is
the long form, laid out like an API reference: every skill below opens with a one-line summary and a
table of its stage, who starts it, and what it reads and writes, then carries the same sections —
**Syntax**, **Parameters**, **Output**, **Refusals**, **Example**, **Remarks** and **See also** — in that
order, so two skills can be compared at a glance. Remarks is where the reasoning lives.

**"Who starts it" is not "who runs it".** Every one of these is executed by an agent — that is what a
skill is. What differs is who decides it should happen, and two of them are a person's decision. Only
one of those is enforced: `init` writes the hook configuration that constrains the agent, so the hook
denies an agent the command. The other is requested: `pr` is the only stage that reaches outside the
repository, so it asks before every push — but no hook blocks a push, and what stops an unready cycle
from merging is the pull-request check.

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

Interviews the team about what matters here and proposes the first `LOC_` rules. It does not adopt — it
hands over the command.

| | |
|---|---|
| Stage | adoption, once per repository — and again later to refresh what the framework owns, or to add a platform once a remote exists |
| Started by | a person. **The hook denies an agent the `sdlc init` command** |
| Reads | the repository: whether it is a git repository, whether `.sdlc/` already exists, and whether it holds code |
| Writes | nothing itself — see Output |

#### Syntax

```
/sdlc:init [--platform github|azure-devops|local] [--integration-branch <name>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `--platform github\|azure-devops\|local` | where the code will live. Optional; the skill asks when it is not given. `local` is for a repository with no remote yet |
| `--integration-branch <name>` | where approved plans wait and built cycles land. Optional, `development` by default; drafted into `solution.md` and created from adoption's commit when it does not exist |

#### Output

Three modes, decided by what it finds: **greenfield** for a repository holding no code, **onboard** for
existing code with no `.sdlc/`, **update** when `.sdlc/` already exists. Update refreshes only what the
framework owns, which is everything not protected — hook configs, the pipeline, `.sdlc/README.md`. On an
update the skill then walks what the release ships that the declared stack never mentions, deciding each
with the person from what the repository holds, in a draft they rename.

- **Writes** nothing itself. `sdlc init` writes the scaffolding and commits it, creates the integration
  branch and switches to it, and drafts `solution.draft.md`, `techstack.draft.md` and
  `constraints.draft.md` beside where each goes — each with a note saying only a person changes the real
  file. A person fills them in, renames them into place and commits them, because `solution.md`,
  `techstack.md` and `constraints.md` are three of the six files only a person writes. The drafts are
  ignored by git.
- **`--platform local`** writes no pipeline. No pull-request check then governs a merge and nothing
  mechanical decides what reaches the default branch — the choice is recorded in `solution.md` so
  `doctor` reports it as a choice rather than a broken install. Once there is a remote, re-run adoption
  with a platform; the `pr` skill walks through creating one.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| outside a git repository | `sdlc init` refuses, and the skill says so before handing the line over | `git init`, which the skill offers |
| any option other than `--platform` and `--integration-branch` | `sdlc init` refuses before writing anything, rather than ignoring it | run it without; the stack is declared afterwards — see Remarks |
| `--integration-branch` names a branch other than the one `solution.md` records | `sdlc init` refuses: only a person changes that file | change `integration_branch` in the draft `sdlc draft solution` writes, rename it, and create the branch |

#### Example

```
/sdlc:init --platform local
```

Expect two or three questions, then the `sdlc init` line to run yourself, then drafts to fill in and
rename — the solution paragraph, the declared stack, and a proposed `LOC_` constraint if one survived —
and a commit of what you placed. It ends on `sdlc doctor`'s closing block, which names what is still to
place and the rename for each.

#### Remarks

Adopting a process is a decision about how a team works, and `sdlc init` writes the hook configuration
that constrains the agent, the pipeline that gates the merge, and the file saying where the code came
from. So the command is a person's, and the skill's part is the two halves a command cannot do: asking
what matters here, and proposing the `LOC_` rules that follow from the answers.

It asks the person rather than reading the codebase, because reading an existing repository to decide
what to forbid would make adoption a judgement on code that predates the framework — and the first cycle
would spend itself on findings about work nobody is defending.

**Adoption decides nothing about the stack, and asks one question: where the code will live.** There is
no profile to choose. `sdlc init` places no declared stack and writes no `.NET` build settings; it
drafts the declared stack for a person to cut down, and no cycle opens until it is placed.

**Holding an existing codebase to the framework's conventions is not an option on adoption, and the
skill raises it only when a person does.** An adopted solution holds new work to the code already
around it, which is the default and usually right. Moving off that is two separate choices, each made
adoption: `standard: profile` in the solution document's draft — or `sdlc draft solution --standard
profile` once it is placed — changes which patterns a plan defaults to, and
the `.NET` build settings — `.editorconfig`, the three props files, `global.json` — are rows in the
declared stack, switched off in an adopted repository's first draft. Switching one on is a person's
decision because it applies a convention to code that never agreed to one: `Directory.Build.props`
makes warnings errors, so a build can fail on files nobody has touched. A row writes nothing — a cycle
whose plan declares the file brings it, planned and approved like any other work — and a file the
repository already had is kept.

**The origin does not move.** Where the code came from (`scaffolded` or `adopted`) is a historical fact;
which conventions its new work follows (`standard`) is a decision, and those are two keys rather than
one.

Git is required because without it a build cannot record the diff a verification derives from, and the
gates key on a branch that cannot exist — so every hook would permit everything while the artifacts
still looked governed.

The skill says plainly that it is the interview and writes nothing, and hands the person the command to
run themselves — in Claude Code as a line with a leading `!`, so it runs as the person's own command and
its output lands in the conversation. What it runs, in order:

1. checks it is in a git repository, and whether `.sdlc/` already exists
2. asks where the code will live, then hands over `! sdlc init --platform <answer>`
3. interviews the team — three or four questions, or two on a repository holding no code
4. has the person write the paragraph that says what the solution is for into `solution.draft.md`, and
   shows the rename
5. runs `sdlc propose constraints` and fills in a `LOC_` entry for each answer that survives the three
   admission tests, for the person to add to `constraints.draft.md` — or proposes none
6. edits `techstack.draft.md` down to what is true here, and shows the rename
7. hands over the commit of what was placed
8. reports `sdlc constraints` and the closing block of `sdlc doctor` verbatim, and stops

It follows the drafts and renames adoption's report names rather than a list of its own, and ends on
`sdlc doctor`'s.

#### See also

- [`intent`](#intent) — the next stage, once `sdlc doctor` names nothing left to place
- [`draft`](#draft), [`propose`](#propose)
- [Adopting the framework](../docs/guides/adopting-the-framework.md)
- [Declaring the stack](../docs/guides/declaring-the-stack.md)

---

## The planning chain

Three artifacts, all required, always. Each is written, challenged in a fresh process against the rules
in force, and revised until a round raises nothing blocking. A stage will not run until the artifact
before it has such a round.

### `intent`

Interviews a person into the intent, the first artifact of a cycle, and opens the cycle.

| | |
|---|---|
| Stage | the planning chain, first. It opens the cycle |
| Started by | a person: somebody wants to start new work, reports a problem, or has an idea to turn into something the process can carry |
| Reads | the constraints in force and the conversation |
| Writes | `.sdlc/cycles/<id>/intent.md`, and the findings rounds the loop produces |

#### Syntax

```
/sdlc:intent [--type bug|add|change] [--from <lead>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `--type bug\|add\|change` | the kind of change — a bug fix, a feature addition or a feature change — so the interview asks that kind's questions. Optional: without it the type comes from the lead, or a new solution is detected, or the opening answer decides it and the skill confirms. There is no `new`: a new solution is only detected. A flag the answers contradict is asked about, never followed in silence |
| `--from <lead>` | begin from a lead — `azuredevops:1234`, `github:56`, `suggestion:<cycle>/<finding>`, or a path — fetched by `sdlc lead show` and recorded in the intent as `lead:`. It seeds the interview and never replaces it |

#### Output

A cycle id, its plan branch `cycle/<id>/plan`, and `intent.md` — problem, wanted outcomes, fixed decisions, what
must not change, and a `## Open` section that is required even when empty. It never asks how to build the
thing, anything about the stack, or criteria in testable form; those belong to the plan, the declared
stack and the spec.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| the declared stack is not in place | `sdlc new` refuses | fill in `techstack.draft.md`, which adoption wrote, and rename it into place |
| anything is uncommitted | `sdlc new` refuses and names the files, because the cycle's branch would carry them into its diff | the skill asks what you want done with them — commit, stash or discard — and opens the cycle again |

#### Example

```
/sdlc:intent
```

Expect a cycle id and the branch the skill switched to, then one question — *what is this cycle for?*, or
for a new solution *what should someone be able to do first?* — with example answers under it, and then
one question at a time, each depending on your last answer. *Not sure* is always an answer, and the skill
says where it records it. Nothing is written until the interview is done, because the intent is written
from the answers. Then several interrogation rounds
without your involvement. It ends on the intent's status panel, whose last line is the next command.

#### Remarks

An intent says *why* the work exists, and most requests arrive shaped as a solution ("add a cache")
rather than a problem ("the dashboard takes fourteen seconds and people stop opening it"). The person
knows what they want; they do not yet know what is underspecified about it. So this is the only artifact
written by interview, and the skill's job is to elicit rather than to draft. A decision already taken is
kept rather than discarded — recorded under `Fixed decisions` with who decided it and when — so the
intent stays a problem statement while the plan can still honour the decision.

What it runs, in order:

1. `sdlc constraints --for intent`, kept in front of it, and `.sdlc/solution.md`, so it asks from where
   the person's paragraph leaves off
2. `sdlc new`, which allocates the cycle id and claims it — that claim *is* the collision check — then
   `sdlc branch <id> plan` and the `git switch` it prints, so the working copy is on `cycle/<id>/plan`. The
   person runs neither
3. settles the type first — from `--type`, from a lead, or by detecting a new solution: a scaffolded
   solution with no cycle yet approved — because the type decides the questions
4. interviews one question at a time, each with example answers: an opening, a short set for the type —
   a bug's reproduction, a feature's person and job, a change's dependants, a new solution's first use —
   then what must keep working, what is already decided, and which areas it touches. An answer shaped as
   a solution is asked what its absence causes
5. where the person bounds the work with *simple*, *basic* or *nothing fancy*, asks once whether that
   means small in scope or plain in appearance
6. writes the intent, then runs the interrogate-refine loop itself until a round raises nothing blocking,
   a reader reports a conflict, or the panel's `Loop` line says it is stuck on one rule
7. shows `sdlc status <id> --stage intent --brief` verbatim and stops

**It does not chain into the spec.** That hand-back is the human-in-the-loop point, and a skill that
continued would remove it while appearing helpful. It does not cap the rounds: a loop that is converging
can take eleven.

#### See also

- [`spec`](#spec) — the next stage, when you are satisfied with the intent
- [`interrogate`](#interrogate), [`refine`](#refine)
- [Running a cycle](../docs/guides/running-a-cycle.md), §3

### `spec`

Derives testable acceptance criteria from a clean intent, producing the spec of a cycle.

| | |
|---|---|
| Stage | the planning chain, after `intent` |
| Started by | a person, once the intent has a clean interrogation round |
| Reads | `intent.md` as its only input: not the request that started the cycle, not the conversation, not the codebase for requirements |
| Writes | `.sdlc/cycles/<id>/spec.md` |

#### Syntax

```
/sdlc:spec <cycle id>
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle whose intent the spec is derived from |

#### Output

`spec.md`: numbered acceptance criteria, `Out of scope` with reasons, and `## Open`. Every criterion's
check has to be one this solution's testing layers can actually run (`CON_12`). Every intent `## Open`
entry ends in exactly one of three places — a criterion, `Out of scope` with a reason, or the spec's own
`## Open` citing the intent item.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| there is no intent | refuses | `/sdlc:intent` |
| the intent is not clean | refuses | `/sdlc:refine <id> intent`, then a fresh interrogation |
| a spec already exists | refuses, and never overwrites one | `/sdlc:interrogate <id> spec` or `/sdlc:steer`, which it names |
| the intent is missing something it should have asked for | stops rather than writing the criterion | `/sdlc:steer` — that is its job |

#### Example

```
/sdlc:spec 20261002_1414_12
```

Expect a short run with no questions unless a finding needs a person, ending on the spec's status panel.

#### Remarks

A spec says what *done* means, and every criterion in it has to be able to fail. A criterion that cannot
fail passes every gate and proves nothing; a criterion that narrows its outcome passes every gate and
ships the wrong thing — a die specified as a "face value" shipped as a numeral in a bare paragraph tag,
because nothing downstream compares a spec to its intent again. So the skill carries each outcome's own
words into its criterion, and puts any part it cannot check under `Out of scope` where a person can see
it went.

What it runs, in order:

1. `sdlc constraints --for spec`
2. `sdlc status <id> --stage intent`, to refuse early
3. reads `intent.md` and derives criteria according to its `type` — a `bug fix` from the reproduction, a
   `feature change` with a criterion for each `Must not change` item
4. disposes of every entry under the intent's `## Open`
5. writes the spec and runs the same interrogate-refine loop as the intent
6. shows `sdlc status <id> --stage spec --brief` verbatim and stops

#### See also

- [`plan`](#plan) — the next stage
- [`interrogate`](#interrogate), [`refine`](#refine), [`steer`](#steer)
- [Running a cycle](../docs/guides/running-a-cycle.md), §3

### `plan`

Decides how a spec will be met and what will prove each criterion, producing the plan of a cycle. The
last document before a person approves.

| | |
|---|---|
| Stage | the planning chain, after `spec`. It is the last document written before a person approves, so it is the one a reviewer reads most closely |
| Started by | a person, once the spec has a clean interrogation round |
| Reads | `spec.md`, the codebase, the declared stack, the decision record and the theme |
| Writes | `.sdlc/cycles/<id>/plan.md` |

#### Syntax

```
/sdlc:plan <cycle id>
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle whose spec the plan meets |

#### Output

`plan.md`: `Shape and departures`, a `Files` table, numbered `Proof` entries each naming its test in
backticks, `Risk`, and `## Open`. The pull-request check reads those test names out and confirms each one
ran and passed.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| there is no spec | refuses | `/sdlc:spec <id>` |
| the spec is not clean | refuses | `/sdlc:refine <id> spec`, then a fresh interrogation |
| a plan already exists | refuses | `/sdlc:interrogate <id> plan` or `/sdlc:steer`, which it names |
| the plan would declare one of the six files only a person writes | never declares one; the schema refuses a plan that does | — |

#### Example

```
/sdlc:plan 20261002_1414_12
```

Expect it to read the code before it writes, then the loop, then a panel and a gate refusal naming
approval as the missing piece.

#### Remarks

A plan says *how*, and commits to what will prove each criterion. The constraint to watch is proof
adequacy (`CON_06`). *What would have to be true for this check to pass while the criterion is still
unmet?* If that question has an answer, the proof is not adequate yet — and an inadequate proof is worse
than none, because it produces a green verification instead of an obvious gap.

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

What it runs, in order:

1. `sdlc constraints --for plan`
2. `sdlc status <id> --stage spec`, to refuse early
3. reads the spec and the code, then `sdlc stack`, `sdlc decisions` and `sdlc theme`
4. decides the shape, argues each departure, writes a proof for every criterion and chooses the tier —
   `local`, `contract` or `data`, by how reversible the change is
5. writes the plan and runs the interrogate-refine loop
6. shows `sdlc status <id> --stage plan --brief` and `sdlc gate <id>` — which refuses, because nobody has
   approved yet — and explains that approving is the person's act

#### See also

- [`draft`](#draft) — next, a person approves with `/sdlc:draft approval <id>` and a rename
- [`interrogate`](#interrogate), [`steer`](#steer)
- [Declaring the stack](../docs/guides/declaring-the-stack.md)
- [Running a cycle](../docs/guides/running-a-cycle.md), §3

---

## The loop

The two skills every stage above runs in turn until a round is clean. A person can also run either
directly.

### `interrogate`

Judges one artifact against the constraints in force, in a fresh process, then once more per perspective
the cycle gets — each in a fresh process of its own — reconciles what they found, and writes one findings
round. Never edits.

| | |
|---|---|
| Stage | the loop, whenever an artifact — intent, spec, plan or verify document — has just been written or changed |
| Started by | the stage skill's loop, or a person |
| Reads | the constraints in force, the artifact, and the artifact above it — on a plan round `techstack.md` as well, and on a verify round the spec and the evidence; `sdlc perspectives` and each perspective's `sdlc perspective show` for its readers |
| Writes | `.sdlc/cycles/<id>/findings/<artifact>.r<n>.json` |

#### Syntax

```
/sdlc:interrogate <cycle id> <intent|spec|plan|verify>
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle the artifact belongs to |
| `<intent\|spec\|plan\|verify>` | the artifact to judge |

#### Output

A new findings round. Rounds accumulate and are never rewritten. A finding keeps its id across rounds,
cites exactly one rule by id and by that rule's own heading, and every round records `upstream_sha256` —
the hash of the artifact above it, as the reader saw it. That is what makes a stale judgement detectable
when an upstream document is edited afterwards. A reader that finds two rules whose remedies exclude each
other reports a conflict rather than picking a side.

**It never edits the artifact**, and it does not recommend whether to run another round.

**A defect found in several places is one finding naming every place** in its locus, not one finding per
round for each instance in turn.

#### Refusals

None of its own.

#### Example

```
/sdlc:interrogate 20261002_1414_12 spec
```

Expect a new findings round on disk and the spec's panel showing what it found. An empty round is a clean
reading, and it clears nothing an earlier round raised unless that finding is marked answered or
withdrawn.

#### Remarks

**The fresh process is the whole mechanism.** The reader must not have seen the artifact being made, so
the judgement runs in a new process — not in the authoring session, and not as a subagent. A tool call
runs in its own process while the model calling it is still the author; a new process creates a new
reader rather than handing data to the one you already have.

What that buys is the removal of *investment bias*: the reader never saw the interview, the drafts or the
author's reasoning, so it has nothing to defend. What it does not buy is independence from *model bias* —
a borderline judgement flips the same coin twice, so a round is a second reading by a reader with no
stake, never independent verification.

**It is also the only place a blocking finding can be cleared.** The round records that an earlier
finding this reader did not raise is now `answered`, or `withdrawn` where the objection was dropped
rather than answered. A finding the reader raises again stays open, and so does one that cannot be
placed in either case — the reader's silence is the evidence, and the authoring session's opinion of its
own corrections is not. `../CONCEPT.md` D-49 has the reasoning, and D-27 is why there is no other exit.

**What it is given is a rule source, never the cycle's own history.** The first predates the cycle and is
what the document is judged against, the second is what the new process exists to withhold. `CON_15` is
a rule entirely about conformance to the stack file, so a reader without `techstack.md` could only ever
object to a departure the plan had already admitted to (D-58).

It runs `sdlc constraints --for <artifact>`, starts a fresh non-interactive process of the host, hands it
the judging prompt, writes what comes back as the next round, then shows `sdlc status <id> --stage
<artifact>` verbatim with at most two lines of its own.

#### See also

- [`refine`](#refine) — what answers a blocking finding
- [Handling blockers](../docs/guides/handling-blockers.md)

### `refine`

Applies an artifact's findings to that artifact. Edits one planning document and nothing else, and never
judges.

| | |
|---|---|
| Stage | the loop, after an interrogation round raised blocking findings and while the cycle is not yet approved |
| Started by | the stage skill's loop, or a person |
| Reads | the findings, the panel's `Loop` line, and the artifact |
| Writes | that one planning document |

#### Syntax

```
/sdlc:refine <cycle id> <intent|spec|plan>
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle the artifact belongs to |
| `<intent\|spec\|plan>` | the planning document to edit |

#### Output

Every finding ends in exactly one of three exits, and there is no fourth: **healed** (the artifact
changes), **assumed** (recorded in the decisions region, marked unconfirmed), **asked** (recorded in
`## Open`, and the skill stops and asks). Never assume silently — a report is read once, and a section is
read every time the artifact is read.

It never touches a findings round. What it does instead is **report every id it was given**, saying for
each whether it changed the document or disagreed and left it alone — because the next round cannot tell
those apart by reading the artifact, and they become different dispositions. An id left out of the
report is one the next round cannot place, and by D-49 an unplaceable finding stays open.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| no blocking finding is open | refuses | — |
| the cycle is approved | refuses: the documents are hash-locked at that point, so editing one would break the gate rather than satisfy it | a recorded reopen, `sdlc draft approval <id> --reopen` |
| source, tests, `approval.md`, or any other artifact | may never edit them | — |

#### Example

```
/sdlc:refine 20261002_1414_12 spec
```

Expect a three-column report — the finding id, the exit it took, what was done — followed by the panel.

#### Remarks

The separation from `interrogate` is the point: an interrogation that could also fix things would grade
its own corrections.

It may disagree with a finding — not every finding is right. What it may not do is answer one by making
the artifact vaguer, which is the single move that removes a problem from the page while leaving the
belief in it. "Fourteen seconds" becoming "slow" is not a fix.

**It answers the mistake, not only the line it was pointed at.** Before healing a finding it reads the
rest of the artifact for the same defect under the same rule, heals every instance in the same pass, and
names the others on that finding's report line. Fixing one instance at a time hands each round the next
one. Anything other than that defect is still the next reader's to raise.

It reads the panel's `Loop` line first — a rule that *has come back* means look for an exit rather than
edit the same words again, and *stuck on* means do not edit at all. Then it edits the artifact, reports
one line per finding, shows `sdlc status <id> --stage <artifact>` verbatim and stops. It does not
interrogate again; that is the stage skill's loop.

#### See also

- [`interrogate`](#interrogate), [`steer`](#steer)
- [Handling blockers](../docs/guides/handling-blockers.md)

---

## Correction

### `steer`

Carries a person's mid-cycle correction down the artifact chain, editing the intent, spec and plan rather
than regenerating them.

| | |
|---|---|
| Stage | correction, after the spec exists and before the gate |
| Started by | a person who has changed their mind about *how* a cycle is being done |
| Reads | the correction, and each artifact it reaches |
| Writes | the intent's `Fixed decisions`, each artifact it reaches, and fresh rounds on everything downstream of what it touched |

#### Syntax

```
/sdlc:steer <cycle id> "what to change"
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle in progress |
| `"what to change"` | the correction, in the person's words |

#### Output

Records the correction under the intent's `Fixed decisions`, with attribution, and edits rather than
regenerates — a regenerated artifact has a findings history about a document that no longer exists.
Usually the spec needs nothing and the plan's `Shape and departures`, `Files` and often `Proof` change.
The re-interrogation is checked rather than trusted: a round records the hash of the artifact above it,
so editing the intent makes the spec's latest round stale.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| there is only an intent | refuses | `/sdlc:refine <id> intent` instead |
| the cycle is approved | refuses | `sdlc draft approval <id> --reopen` first, which it names |
| the cycle is abandoned | refuses; abandoned is terminal | open a new cycle |
| the comment changes the problem rather than the how | says so | a new cycle, which it offers |

#### Example

```
/sdlc:steer 20261002_1414_12 "use the existing cache abstraction rather than a new one"
```

Expect a question about why, a new dated and attributed line in the intent's `Fixed decisions`, edits to
the plan, and fresh rounds on everything below the intent.

#### Remarks

It exists because of cost. *"Use the existing cache abstraction rather than a new one"* arrives
mid-cycle, and doing that by hand means editing and re-interrogating three documents. Nobody pays it, so
the correction ends up in conversation where it is lost — and a path so costly that it pushes information
out of the system is worse than no path. No constraint loosens.

**Steering changes how, not what.** Which approach, which abstraction, which order, what to avoid. A
comment that changes the problem is a different intent.

One or two questions — what to do instead, and why — then it records the correction, edits each artifact
it reaches, re-interrogates everything downstream of what it touched, reports one line per artifact, and
shows `sdlc status <id>` verbatim.

#### See also

- [`intent`](#intent), [`refine`](#refine)
- [Running a cycle](../docs/guides/running-a-cycle.md)

---

## The gate

### `draft`

Prints what belongs in one of the files only a person writes — an approval, an abandonment, the declared
stack, or the solution document.

| | |
|---|---|
| Stage | the gate, and whenever the solution's own declaration changes |
| Started by | a person who needs to approve or refuse a cycle at the gate, stop a cycle, or declare or change what the solution is or is built from |
| Reads | what the matching `sdlc draft` command reads |
| Writes | a complete file beside the real one for `approval`, `solution` and `techstack`, placed by a person's rename; `abandon` is printed |

#### Syntax

```
/sdlc:draft approval <id> | abandon <id> | solution [--standard profile|existing] | techstack [--empty]
```

#### Parameters

| Parameter | Description |
|---|---|
| `approval <id>` | the gate decision for that cycle. `--changes-requested`, `--deny` or `--reopen` drafts a refusal on the same terms |
| `abandon <id>` | why that cycle stopped |
| `solution` | the whole `solution.md`. `--standard profile\|existing` moves that one key |
| `techstack` | the declared stack. `--empty` drafts nothing |

#### Output

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

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| a blocking finding is open, a document is missing, one has never been interrogated, or a round judged a document that has changed since | `draft approval` refuses to print | answer it; if it genuinely cannot be answered, `draft abandon` and a new cycle |
| a drafter that does not exist | refuses, and lists the four that do | — |
| `draft solution` before the document is placed, or `draft techstack` while a draft is waiting | refuses, and leaves the waiting draft as it is: what is in it may be somebody's work | fill in the waiting draft and rename it — or delete it, and draft again |
| the rename that places the file | shown, and never run or offered: the hook denies it in every spelling and through every shell | run it yourself |

#### Example

```
/sdlc:draft approval 20261002_1414_12
/sdlc:draft approval 20261002_1414_12 --changes-requested
```

Expect a file named for its decision written into the cycle directory, the command that renames it onto
`approval.md`, and a note of how many earlier decisions it carries. Run that rename yourself; the agent
cannot.

#### Remarks

Six files are written only by a person — `approval.md`, `abandoned.md`, `constraints.md`,
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
so there is no finished entry to hand back.

**A blocking finding cannot be waived by any mechanism** (D-27): there is no field for it in the entry
and no refusal code at the gate that acceptance would clear. It is answered, or the cycle does not
advance.

It runs the matching command and shows what it prints verbatim, including the instructions beneath it,
and adds nothing:

```
sdlc draft approval <id>     [--changes-requested | --deny | --reopen]
sdlc draft abandon <id>
sdlc draft solution          [--standard profile|existing]
sdlc draft techstack         [--empty]
```

It shows the rename that places the file and **does not run it, or offer to**, and it does not read the
hook's denial as a configuration problem.

#### See also

- [`build`](#build) — next after an approval; after a refusal, the stage that needs work
- [`propose`](#propose)
- [Handling refusals](../docs/guides/handling-refusals.md)

---

## Build and verify

### `build`

Implements an approved plan, builds, tests, and records the evidence a verification will read.

| | |
|---|---|
| Stage | build and verify, once the gate is satisfied |
| Started by | a person, or `batch-build`, after a person has approved the cycle's three planning documents |
| Reads | `plan.md` and the code |
| Writes | source and tests, and the evidence verify will read |

#### Syntax

```
/sdlc:build <cycle id>
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the approved cycle to build |

#### Output

Commits on the cycle's branch — the change, then the evidence: the diff, the commit it is measured from,
the runner's own output, and one normalised test summary. **Verify reads only these files**, so a
verdict derived from the builder's recollection is a verdict nobody can check (`CON_08`).
`sdlc evidence` also prints what changed against what the plan declared — `undeclared`, `undelivered`,
`governing` — none of which is a refusal; a `governing` line goes in the report and the commit message.

**`sdlc scaffold <id>` is how two kinds of file arrive, and the plan is what asks for either.** On a new
solution's first cycle it writes the skeleton and the build settings it compiles under. On a solution
adopted into existing code it writes only the build settings files the plan declares — and where
`Directory.Build.props` is one of them, the build that follows can fail on code the cycle never
touched, which the plan was required to have planned for. Warnings are errors, enforced by the build
props rather than by instruction.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| the gate refuses — no cycle, abandoned, a missing document, nobody approved, a refusal standing, a document changed since approval, a blocking finding still open, or a round that judged a document which has moved since | refuses | each has a different next step — [Handling refusals](../docs/guides/handling-refusals.md) |
| a red build or failing tests | stops, and says so. It does not adjust a test to make it pass | — |
| the turn ends over a failing suite, with code written and no evidence, or with code moved since the evidence | the turn-end hook sends the turn back | — |

#### Example

```
/sdlc:build 20261002_1414_12
```

Expect commits on the cycle's branch — the change, then the evidence — and a short hand-back saying what
was implemented, what the build and tests said, and how many `Noticed:` lines the commit carries.

#### Remarks

It is the first stage that writes code, and the first a person is not expected to watch, so everything a
verification will rely on has to be recorded rather than remembered. It implements what the plan decided
and only that (`CON_07.2`): creep inside an approved file is what reaches review dressed as approved
work, which is where it is least likely to be questioned. Something worth fixing that the plan did not
describe is left alone and recorded as a `Noticed:` line in the commit message, where the person
reviewing the pull request will meet it.

It checks that the suite actually ran: a trial cycle lost its whole verification to `dotnet test` exiting
zero without running anything, because the runner was not configured. And **the turn end is checked as
well**, on both hosts — a hook asks the gate before the agent finishes. It is not a rule the skill has to
remember — it is why there is no point reporting done over a red build.

What it runs, in order:

1. `sdlc gate <id>`, and stops if it refuses
2. `git switch cycle/<id>`
3. implements the plan — running `sdlc scaffold <id>` where the plan declares the solution skeleton or a
   build settings file the repository does not have
4. `dotnet build` and `dotnet test`, reading the test count rather than the exit code
5. commits the change, with any `Noticed:` lines
6. `sdlc evidence <id> --tests <directory> --build-log <path>`, then commits the evidence
7. shows `sdlc status <id> --brief` verbatim with at most five lines of its own, and stops

It keeps no task list of its own inside a batch.

#### See also

- [`verify`](#verify) — the next stage
- [`batch-build`](#batch-build)
- [Handling refusals](../docs/guides/handling-refusals.md)

### `verify`

Adjudicates a build against its spec, from the recorded evidence alone. Never edits code.

| | |
|---|---|
| Stage | build and verify, after the build has recorded its evidence |
| Started by | a person, or `batch-build` |
| Reads | `spec.md`, `plan.md` and the evidence — `tests.json`, `diff.patch`, `base.sha`, the runner's output and `build.log` |
| Writes | `.sdlc/cycles/<id>/verify.md` |

#### Syntax

```
/sdlc:verify <cycle id>
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the built cycle to adjudicate |

#### Output

`verify.md`: an entry for every criterion, numbered to match, each citing the evidence it rests on
(`CON_08`), and `## Open` for limits of the evidence. **Never edits code.**

It writes `pass` or `blocked`. **`escalated` is computed, not declared**: it is what a remediation loop
that stopped making progress makes of a blocked cycle (D-66). `sdlc verify-chain` checks mechanically
that every criterion has an entry and that a `pass` rests on named tests that passed.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| the evidence is missing | says so and stops — that is a build that did not finish | `/sdlc:build <id>` |
| the outcome is `blocked` | does not start remediating; you decide | `/sdlc:remediate <id>` |

#### Example

```
/sdlc:verify 20261002_1414_12
```

Expect `verify.md` with a verdict per criterion, and the outcome in a line or two.

#### Remarks

Verify is the only artifact whose entire value is being derived. A claim that reaches beyond its
evidence turns an auditor into an author. So it reads the evidence rather than running the tests —
running them would produce new evidence instead of adjudicating the build's, and the next reader could
not tell which run the verdict describes. It does not read the source to decide whether a criterion is
met: reading the implementation and concluding it looks correct is the most tempting way to write a claim
with no evidence behind it.

It runs `sdlc constraints --for verify`, adjudicates criterion by criterion, writes the verify document,
interrogates and refines it like any other artifact, then shows `sdlc status <id>` and
`sdlc pr-ready <id>` verbatim with at most two lines of its own.

Per criterion: which test did the plan name, does the summary say it ran and passed, and does that test
actually establish the criterion? The third question is where judgement lives, and where the verification
may disagree with the plan — a test that passed but does not reach its criterion is **not met**.

#### See also

- [`pr`](#pr) — next on `pass`
- [`remediate`](#remediate) — next on `blocked`
- [Handling blockers](../docs/guides/handling-blockers.md)

### `remediate`

Applies a blocked verification's findings to source and tests. Edits code, never a planning document.

| | |
|---|---|
| Stage | build and verify, after verify recorded `blocked` |
| Started by | a person, or `batch-build` |
| Reads | the verify findings, `sdlc pr-ready <id>`, and the code |
| Writes | source and tests, fresh evidence, and a commit on the cycle's branch |

#### Syntax

```
/sdlc:remediate <cycle id>
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the blocked cycle |

#### Output

**Edits source and tests, never a planning document.** It fixes the cause rather than the symptom — no
loosened assertion, no sleep or retry, no substituted thing under test, no skipped test.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| the criterion itself looks wrong | stops | `sdlc draft approval <id> --reopen`, which it names, and which withdraws the approval with a recorded reason |
| a regression — a pass that broke what an earlier pass fixed | stops | — |
| two passes in a row reduce nothing | stops | — |

#### Example

```
/sdlc:remediate 20261002_1414_12
```

Expect one line per finding — what was wrong, what changed, and whether it is confident that was the
cause — then the panel. Verify again as its own stage.

#### Remarks

`refine` and `remediate` are two editors with opposite guards, and the asymmetry is the whole design: if
a failing test could cause an agent to adjust the spec, the criteria become whatever the code happens to
do. So when the honest conclusion is "the criterion is wrong", the skill says it and stops.

**It stops when it is not getting anywhere, not after a set number of tries.** It stops when a pass
undoes what an earlier one answered, or when two passes in a row reduce nothing. That stopping is the
only reason an unattended batch is safe to run at all. The blind three it replaced failed an honest cycle
that needed a fourth pass and spent all three on a cycle making no progress at all, and there is
deliberately no ceiling behind the progress rule now (D-66).

It reads `sdlc pr-ready <id>` to know whether the loop is still making progress, works the latest
`findings/verify.r<n>.json`, then `dotnet build`, `dotnet test` and
`sdlc evidence <id> --tests <results> --build-log <log>`, commits on the cycle's branch, and shows
`sdlc status <id>` verbatim. A pass is a verify findings round after the first, so progress is read from
what happened rather than from a counter anybody could edit.

**It does not verify its own remediation.** A verification by the session that wrote the fix is the
investment bias the whole separation exists to remove.

#### See also

- [`verify`](#verify) — again, as its own stage
- [`refine`](#refine)
- [Handling blockers](../docs/guides/handling-blockers.md)

---

## Pull requests and unattended runs

### `pr`

Pushes a cycle's branch and opens its pull request into the integration branch. The only stage that
touches the remote.

| | |
|---|---|
| Stage | pull requests: after a verification passed, after a plan is approved, or after a build that did not pass |
| Started by | a person, and **it asks before every push**; or `batch-build --pr` |
| Reads | `sdlc pr-ready` or `sdlc gate`, `integration_branch` in `solution.md`, and the cycle's documents for the body |
| Writes | the pushed branch — `cycle/<id>/plan`, `build`, `build-unattended`, or an older cycle's bare `cycle/<id>` — and a pull request |

#### Syntax

```
/sdlc:pr <cycle id> [--plan | --draft] | --all
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the verified cycle to push |
| `--plan` | an approved plan, before it is built: admission to the integration branch, asked of `sdlc gate`. Merging it deletes the branch |
| `--draft` | a `blocked` or `escalated` build, as a draft carrying the attempted code, the verify and the evidence |
| `--all` | every cycle that is ready, asking once before the first push |

#### Output

The body is the cycle rather than a summary somebody invented: the intent's problem in the intent's
words, the criteria with the verification's verdict on each, the tier, the risk section, and anything in
an `## Open` region, because a reviewer should meet the limits before they approve. It links the work
item where the solution tracks them.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| `sdlc pr-ready` refuses: not verified, did not pass, escalated, or the chain broken since verification | refuses, and does not push past it — except `--draft`, which opens a draft for did not pass or escalated, and nothing else | [Handling refusals](../docs/guides/handling-refusals.md) |
| `--plan` and `sdlc gate` refuses | does not push the plan | [Handling refusals](../docs/guides/handling-refusals.md) |
| the solution is `local` or has no remote | stops | it walks you through creating one, then re-running adoption with a platform, and making the check required in branch policy |
| on Azure DevOps | shows the exact command before running it, because that path has never been run (D-32) | — |
| with `--all`, one cycle fails | the others carry on | — |

#### Example

```
/sdlc:pr 20261002_1414_12
/sdlc:pr 20261002_1414_12 --plan
/sdlc:pr --all
```

Expect a confirmation before anything is pushed, then one line per cycle with its id, branch and pull
request link. The merge is a reviewer's.

#### Remarks

It is the only stage that reaches outside the repository. Everything before this is local and
reversible; a pull request notifies people, appears in a queue, and withdrawing it is something somebody
has to notice happened. So it asks `sdlc pr-ready` first, every time, and does not push past a refusal —
the pull-request check would refuse it anyway, so pushing would cost a round trip through CI to learn
what it was just told.

**It is not approving the merge.** A code owner does that, and never the agent that wrote the change. The
local approval defended against an agent that forgets; the review is what defends against a human who did
not look, and they are different jobs.

It runs `sdlc pr-ready <id>`, then `sdlc doctor` and `git remote -v` to check there is somewhere to push,
then `git push -u origin cycle/<id>` and the platform's own tool — `gh pr create` or
`az repos pr create`. With `--all` it shows `sdlc cycles --pr-ready` verbatim and asks once before the
first push; the question is about volume rather than permission, because every cycle that ever passed is
eligible, including one somebody has forgotten.

#### See also

- [`batch-build`](#batch-build)
- [Handling refusals](../docs/guides/handling-refusals.md)
- [Running a cycle](../docs/guides/running-a-cycle.md), §7

### `batch-build`

Completes every queued cycle — builds and verifies each on its own branch, independently — and reports the
sitrep. Run by the factory, it is the whole of what the factory does (D-84).

| | |
|---|---|
| Stage | unattended runs, when several cycles are approved and waiting |
| Started by | a person, or the factory on a schedule |
| Reads | `sdlc cycles --queued` for the work, and `sdlc cycles --claims` for which plans stand up services |
| Writes | what `build`, `verify` and `remediate` write, for each cycle, each in its own worktree on `cycle/<id>/build-unattended` |

#### Syntax

```
/sdlc:batch-build [--pr] [--unattended]
```

#### Parameters

| Parameter | Description |
|---|---|
| `--pr` | opens a pull request for every cycle that reached an outcome — ready when it passed, a draft when it is `blocked` or `escalated` — asking once before the run |
| `--unattended` | asks nothing: the schedule that started the run was the decision. Used by the factory's pipeline |

#### Output

Each cycle ends `passed`, `blocked`, `escalated` or did not finish. **A sibling's outcome changes nothing
about another cycle.** Its task list is one line per cycle, ticked only on `passed`. It ends with one line
per cycle and the sitrep, verbatim.

**Plain `batch-build` never pushes.** `--pr` asks once before the run rather than at the end, because the
question is about volume and a person should answer it before waiting rather than after.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| a cycle cannot be made to pass | stops that cycle, and with `--pr` opens a draft rather than a ready pull request | [Handling blockers](../docs/guides/handling-blockers.md) |
| a cycle is held back by a branch on the remote | does not build it; the sitrep says why | delete the branch to put it back in the queue |

#### Example

```
/sdlc:batch-build
/sdlc:batch-build --pr
/sdlc:batch-build --pr --unattended
```

Expect a line per cycle that moves to done only when that cycle passed, and a closing report of one line
per cycle, then the sitrep.

#### Remarks

**Everything here rests on the remediation bound.** A cycle that cannot be made to pass stops rather
than reaching a ready pull request, and that stopping is the only reason this is safe to run without
watching. **There is no ceiling behind it (D-66)**, so the factory's pipeline job has a timeout.

**It builds; it never merges** (D-84). Every cycle builds from the same base, so two that touch the same
file meet at merge, where a person resolves it with `resolve-conflict` (D-86).

What it runs, in order:

1. `git fetch`, `sdlc cycles --queued` for the work, and `sdlc cycles --claims` for the plans that stand up
   services — read from the binary rather than worked out
2. per cycle, a worktree and `sdlc branch <id> build-unattended`
3. caps concurrency where a plan stands up a backing service, and exports `SDLC_RUN_MODE=batch` and a
   fresh `SDLC_RUN_ID` into each cycle's worktree
4. per cycle, the build stage then the verify stage, remediating while it makes progress
5. with `--pr`, `pr <id>` or `pr <id> --draft` for each
6. reports one line per cycle, then `sdlc sitrep` verbatim, and stops

#### See also

- [`build`](#build), [`verify`](#verify), [`remediate`](#remediate)
- [`pr`](#pr), [`resolve-conflict`](#resolve-conflict)

### `resolve-conflict`

Brings one cycle's pull request up to date with the integration branch when it conflicts, or goes red after
another merge — inside the cycle's declared files, ending in fresh evidence and a fresh verify.

| | |
|---|---|
| Stage | merging, when a pull request into the integration branch conflicts or its re-check fails |
| Started by | a person |
| Reads | the cycle's plan for its declared files, the integration branch, and the failure |
| Writes | a merge commit on the cycle's branch, fresh evidence, and a fresh verify; pushes the branch |

#### Syntax

```
/sdlc:resolve-conflict <cycle id>
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle whose pull request conflicts or went red |

#### Output

The integration branch merged into the cycle's branch and the conflict resolved, the suite run, the old
verify removed, fresh evidence recorded and a fresh verify run in its own process, the branch pushed, and a
report of at most five lines. **It never merges the pull request.**

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| the resolution needs a file the plan never declared | aborts the merge and hands back: that is a change of plan, not a merge | `steer`, a reopen, or an abandonment |
| the same setting with two different values | stops: two plans disagreeing about the solution | a person settles it |
| two migrations from the same parent | does not merge them: takes the integration branch's and regenerates this cycle's | — |

#### Example

```
/sdlc:resolve-conflict 20261015_2002_17
```

Expect what conflicted, what was kept from each side, and the new verify outcome.

#### Remarks

**It edits, and does not judge its own work** (D-01, D-86). A resolution changes code, so the verify that
passed is void; what proves nothing regressed is the suite and a fresh verify against the spec. That is
what answers the old reason this skill was confined to a list of additive files — *a skill that resolved
whatever happened to conflict would quietly become the thing that merges real code* — because every
resolution is rebuilt, re-verified and reviewed on its pull request before anybody merges it.

**It stays inside the declared files.** A conflict that cannot be resolved there is a change of plan.

**It merges rather than rebases**, because the branch is already pushed and reviewed and is squashed when it
lands.

#### See also

- [`verify`](#verify), [`remediate`](#remediate), [`batch-build`](#batch-build)

---

## Reading and deciding

Five skills that read the record and change nothing in a cycle. Anyone may run them at any time.

### `status`

Shows where a cycle has got to — the stage panel, its checks, the gate, and the next command.

| | |
|---|---|
| Stage | any, after any stage |
| Started by | anyone, constantly |
| Reads | the cycle's documents, rounds and approval |
| Writes | nothing |

#### Syntax

```
/sdlc:status <cycle id> [intent|spec|plan]
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle to show |
| `intent\|spec\|plan` | the artifact whose panel to show. Optional; without it, the panel is for whichever artifact the cycle is waiting on, which is what a person wants after any stage finishes |

#### Output

The panel shows **Says** (the document's headings), **Shape** (what the schema found), **Checks** (each
round, and the open blocking findings with the rule each cites), **Gate**, and **Next** — the literal
command. Asked for its own reading, the agent adds at most three lines under `My reading:`; unasked,
nothing.

#### Refusals

None of its own.

#### Example

```
/sdlc:status 20261002_1414_12
/sdlc:status 20261002_1414_12 plan
```

Expect the panel and nothing else; its last line is the command to copy.

#### Remarks

The binary prints the panel, not the model. The format is a contract, and a skill that reformatted it
would make the thing people actually read drift between hosts.

**What it will not tell you, on purpose: whether to keep interrogating.** That judgement has been wrong
before — on one real intent, round seven found the headline change was unreachable and round eleven found
that no success criterion failed without it. A tool that suggested stopping at round four would have been
confidently wrong.

It runs `sdlc status <id> [--stage <artifact>]` and shows it verbatim.

#### See also

- [`constraints`](#constraints), [`suggestions`](#suggestions)
- [Running a cycle](../docs/guides/running-a-cycle.md)

### `constraints`

Shows the rules in force for this solution, all tiers resolved.

| | |
|---|---|
| Stage | any. Every stage skill runs it as its first step |
| Started by | anyone, before writing or judging any artifact, and whenever somebody asks what the rules are |
| Reads | the global `CON_` set, the `PRO_` stack rules the declared stack switches on, and the solution's own `LOC_` |
| Writes | nothing |

#### Syntax

```
/sdlc:constraints [intent|spec|plan|diff|verify]
```

#### Parameters

| Parameter | Description |
|---|---|
| `intent\|spec\|plan\|diff\|verify` | only the rules that apply to that artifact. Optional |

#### Output

**The rules in force for this solution, all tiers resolved** — the global `CON_` set, the `PRO_` stack
rules its declared stack switches on, and the solution's own `LOC_`, in that order, so a solution's own
rules are the last word. Each constraint has a heading, `Applies to`, `Forbids` and `Why`.

#### Refusals

None of its own.

#### Example

```
/sdlc:constraints plan
```

Expect the rules that apply to a plan in this solution. A missing profile tier means the declared stack
switches no stack rule on.

#### Remarks

The constraints live in the framework and are never copied into a solution, so a session that did not
run this is working from recollection. Running it is not a convenience; it is how the rules reach the
agent.

It runs `sdlc constraints [--for <artifact>] [--brief]` and shows it verbatim, adding nothing,
summarising nothing, reordering nothing.

#### See also

- [`propose`](#propose) — adding one of the solution's own
- [Declaring the stack](../docs/guides/declaring-the-stack.md) — the stack rules

### `suggestions`

Lists the open non-blocking findings a solution has accumulated.

| | |
|---|---|
| Stage | any |
| Started by | a person who asks what has been noticed and not acted on, or is looking for small work worth picking up |
| Reads | the findings rounds, and the `Noticed:` lines in builds' commit messages on cycle branches |
| Writes | nothing |

#### Syntax

```
/sdlc:suggestions [cycle id]
```

#### Parameters

| Parameter | Description |
|---|---|
| `cycle id` | one cycle. Optional; without it, every cycle |

#### Output

**Two stores, listed apart.** A suggestion was raised by a fresh reader judging a document and carries a
round and an id. A `Noticed:` line was written by a build that saw something it was not allowed to fix,
and lives in that build's commit message on the cycle's branch. Only the rounds used to be read, so the
three a dice-roller cycle's build wrote were invisible. Asked which to pick up, the agent adds at most
two lines under `My reading:`.

#### Refusals

None of its own.

#### Example

```
/sdlc:suggestions
```

Expect findings and `Noticed:` lines under separate headings, unsorted. A cycle not yet built, or merged
with its branch gone, contributes no `Noticed:` lines.

#### Remarks

Non-blocking is not trivial. Two real ones from a single trial cycle: `dotnet test` did not actually run
the suite because the runner was not configured, and a criterion's proof rested entirely on a
substituted source. Both were substantive; neither cited a rule that forbade the current state, which is
the only thing that made them suggestions.

No ranking and no ages, because a suggestion's age says nothing about its worth and a tool that sorted by
age would quietly argue that the oldest matters most.

**It aggregates nothing, on purpose.** What keeps going wrong *across* cycles is `propose constraints`.
This answers a narrower question — what is outstanding — and the two read the same files for opposite
halves of them.

It runs `sdlc suggestions [<id>]` and shows it verbatim.

#### See also

- [`propose`](#propose)
- [Handling blockers](../docs/guides/handling-blockers.md)

### `propose`

Reads what a solution's record says it is missing and proposes entries a person may adopt. Proposes and
never places.

| | |
|---|---|
| Stage | any. `init` runs `propose constraints` during adoption |
| Started by | a person who asks what keeps going wrong across cycles, wants to add one of this solution's own constraints, wants to record a decision, or needs a theme |
| Reads | a different record for each subject — see Output |
| Writes | a report under `.sdlc/reports/`, which nothing reads back. **Never** `constraints.md` or `decisions.md` |

#### Syntax

```
/sdlc:propose constraints | decisions | theme
```

#### Parameters

| Parameter | Description |
|---|---|
| `constraints` | candidate rules for this solution, mined from the findings |
| `decisions` | candidate `DEC_` entries, from what approved cycles stated |
| `theme` | the roles a theme declares, never a value |

#### Output

Three subjects, three different records. **None is placed by the skill** — `constraints.md` and
`decisions.md` are written only by a person, and a person pastes what they accept.

- **`constraints` is mined backward from the findings.** A rule cited in **three or more distinct
  cycles** is a pattern — distinct *cycles*, not findings, because three findings inside one cycle is one
  artifact going round the loop. It reads what `suggestions` deliberately cannot, closed findings and
  blocking ones. Candidates come in four sections — add, reword, supersede, and *belongs below a
  constraint*, usually the biggest, routing mechanical candidates to an analyzer, an architecture test or
  `BannedSymbols.txt`, all of which fail a build. **A perspective's suggestions are counted too**: each
  perspective that raised something no rule forbade is listed with the distinct cycles it did so in, across
  the findings rounds and the builds' tagged `Noticed:` lines, and marked at three — perspectives have no
  rules until their suggestions earn them (D-90). **Nothing is printed that would delete a rule**; a rule
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

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| no subject | asks rather than guesses | — |
| wording passed in | there is no way to pass wording in | — |

#### Example

```
/sdlc:propose constraints
/sdlc:propose decisions
```

Expect candidates labelled `P1`, `P2` rather than numbered ids, each with the cycles and finding ids
behind it, and the next free id stated once. A young solution gets the empty entry and the three tests.

#### Remarks

**The licence, which is the whole design in one line:** a tool may propose a rule when it brings the
evidence, and may not shape a rule somebody just typed. Every candidate arrives with the cycles and
finding ids behind it, so a person refuses one on the record rather than on taste.

It is the second verb beside `draft`, split by who made the decision: `draft` is for one somebody already
took and hands back a finished file; `propose` is for one they have not, and argues from the record.

**Departing is legal from two of the three, and silence is what gets caught.** `CON_19` asks a plan
contradicting a recorded decision to say what that decision would have cost, the same shape `CON_15`
takes for the declared stack. The theme is the one nothing in governance objects to, because a colour
outside the theme is mechanical and belongs to a linter.

It runs `sdlc propose <subject>`, shows it verbatim, then does the reading the command cannot under
headings of its own.

#### See also

- [`draft`](#draft), [`suggestions`](#suggestions), [`constraints`](#constraints)

---

## See also

- [Skills list](../docs/reference/skills.md) — every skill in one line each
- [Commands list](../docs/reference/commands.md) — every command, on one screen
- [Refusals list](../docs/reference/refusals.md) — every refusal, on one screen
- [Installing the plugin](../docs/guides/installing-the-plugin.md) — the plugin and the binary
- [Adopting the framework](../docs/guides/adopting-the-framework.md) — into a repository
- [Declaring the stack](../docs/guides/declaring-the-stack.md) — what the solution is built from
- [Running a cycle](../docs/guides/running-a-cycle.md) — from intent to pull request
- [Handling blockers](../docs/guides/handling-blockers.md) — a blocking finding or a blocked verification
- [Handling refusals](../docs/guides/handling-refusals.md) — a command or hook that refused
- [Rolling a d6](../docs/samples/rolling-a-die.md) — one whole cycle, prompt by prompt
- [`CONCEPT.md`](../CONCEPT.md) §15 — the design, and every decision behind it
