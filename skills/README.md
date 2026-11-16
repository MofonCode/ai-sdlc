# The twenty-seven skills

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
repository, so a push needs a person's word — *build it*, which the plan's hand-back says ends in a pull
request (D-178), or the question `pr` asks when started by hand — but no hook blocks a push, and what stops an
unready cycle from merging is the pull-request check.

**All twenty-seven are model-invocable.** Nothing in the manifest stops an agent reaching for one on its
own, and that is deliberate — what protects the decisions is the hook denying the seven protected files
and `sdlc init`, plus the pull-request check, rather than a flag in a skill's frontmatter. A guard that
lives in the manifest is a guard the manifest can lose. `../docs/guides/handling-refusals.md` has what
the hook actually denies today, and what it does not.

**Six of them keep a task list where the host has one.** `init`, `intent`, `spec` and `plan` each list
their own steps and nothing past the hand-back, so the list never shows a stage a person has not yet decided
to run. `build` lists its two steps in a person's session, and one line per cycle in its own process, ticked
only when that cycle passed; `to-plan` one per cycle, ticked when its plan is clean. The skills a playbook runs
keep none of their own while it runs them. Every one is the agent's view of a session and never the record —
what is done is what is on disk and what `sdlc doctor`, `sdlc status` and `sdlc cycles --pr-ready` report — and
a host without a task list loses nothing, because no step depends on one. `../CONCEPT.md` D-80 has why a stage's
list stops at the hand-back, why a run over several cycles is shaped differently, and why the other twenty-one
have none.

**Two words are kept apart throughout.** A **refusal** is a command or a hook declining to proceed —
`sdlc gate`, `sdlc pr-ready`, a stage skill's early check, the hook denying a write. A **blocker** is a
blocking finding on an intent, spec or plan, or a `blocked` verification. A blocker usually causes a
refusal further on, but the two are answered in different places: a refusal in
[handling refusals](../docs/guides/handling-refusals.md), a blocker in
[handling blockers](../docs/guides/handling-blockers.md).

## Two kinds of skill

**Every skill is one of two kinds, decided by what it does and never by who starts it** (D-186). **A
single-responsibility skill** does one job — one kind of output: a document written, edited or judged, the code
changed, the pull request opened — and starts no other skill. **A playbook** composes: it runs other skills, in
order, and does no stage work itself. A playbook may run another playbook, and what runs what never loops back.
A person may start either kind — `intent`, `status` and `theme` are single skills a person says; `to-plan` and
`build` are playbooks — and a skill only ever run by another skill is always single.

**A job that grows a second output is split**: the second job becomes a single skill of its own, or an existing
one, and a playbook runs both. `steer` and `resolve-conflict` are the cases that showed it: each did its job and
then ran the checking that follows it, so that checking moved to the playbooks that run them — `to-plan` and
`remediate`.

Why: one job per skill keeps each one readable and replaceable, and the orchestration lives in one place rather
than copied into every stage. The interrogate-and-refine loop is the clearest case — it used to be a mode of each
stage skill (D-29), and it is now `to-plan`'s, run the same way on the intent, the spec and the plan (D-188).

| Skill | Kind | Runs |
|---|---|---|
| [`init`](#init) | single | — |
| [`intent`](#intent) | single | — |
| [`spec`](#spec) | single | — |
| [`plan`](#plan) | single | — |
| [`to-plan`](#to-plan) | playbook | `spec`, `plan`, `interrogate`, `refine`, `steer` |
| [`next`](#next) | playbook | `intent`, `to-plan` |
| [`interrogate`](#interrogate) | single | — |
| [`refine`](#refine) | single | — |
| [`steer`](#steer) | single | — |
| [`draft`](#draft) | single | — |
| [`build`](#build) | playbook | `implement`, `verify`, `remediate`, `pr` |
| [`implement`](#implement) | single | — |
| [`verify`](#verify) | single | — |
| [`remediate`](#remediate) | playbook | `resolve-conflict`, `implement`, `verify`, `pr` |
| [`pr`](#pr) | single | — |
| [`resolve-conflict`](#resolve-conflict) | single | — |
| [`status`](#status) | single | — |
| [`constraints`](#constraints) | single | — |
| [`suggestions`](#suggestions) | single | — |
| [`theme`](#theme) | single | — |
| [`harvest`](#harvest) | playbook | `harvest-constraints`, `harvest-decisions`, `harvest-suggestions`, `harvest-stack`, `harvest-areas`, `close-cycle` |
| [`harvest-constraints`](#harvest-constraints) | single | — |
| [`harvest-decisions`](#harvest-decisions) | single | — |
| [`harvest-suggestions`](#harvest-suggestions) | single | — |
| [`harvest-stack`](#harvest-stack) | single | — |
| [`harvest-areas`](#harvest-areas) | single | — |
| [`close-cycle`](#close-cycle) | single | — |

The suite holds that every skill on disk is in this table with a kind; that a playbook runs at least one skill;
that what a playbook's text says it runs and its row here name the same skills, in both directions; that a single
skill's text never tells the agent to run another skill; and that what runs what never loops back. Whether a single
skill quietly does two jobs no test can read; a reviewer does.

---

## Adoption

Once per repository, before any cycle.

### `init`

Interviews the team about what matters here and proposes the first `LOC_` rules. It does not adopt — it
hands over the command.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
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
  `techstack.md` and `constraints.md` are three of the seven files only a person writes. The drafts are
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
5. runs `sdlc constraints --new` and fills in a `LOC_` entry for each answer that survives the three
   admission tests, for the person to add to `constraints.draft.md` — or proposes none
6. edits `techstack.draft.md` down to what is true here, and shows the rename
7. hands over the commit of what was placed
8. reports `sdlc constraints` and the closing block of `sdlc doctor` verbatim, and stops

It follows the drafts and renames adoption's report names rather than a list of its own, and ends on
`sdlc doctor`'s.

#### See also

- [`intent`](#intent) — the next stage, once `sdlc doctor` names nothing left to place
- [`draft`](#draft), [`constraints`](#constraints)
- [Adopting the framework](../docs/guides/adopting-the-framework.md)
- [Declaring the stack](../docs/guides/declaring-the-stack.md)

---

## The planning chain

Three artifacts, all required, always. Each is written, challenged in a fresh process against the rules
in force, and revised until a round raises nothing blocking. A stage will not run until the artifact
before it has such a round.

### `intent`

Interviews a person into the intent, the first artifact of a cycle, and opens the cycle — first proposing how to slice the work into atomic cycles when it is more than one piece.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | the planning chain, first. It opens the cycle |
| Started by | a person: somebody wants to start new work, reports a problem, or has an idea to turn into something the process can carry |
| Reads | the constraints in force and the conversation |
| Writes | `.sdlc/cycles/<id>/intent.md`; for a slicing, one cycle per piece that needs nothing unmerged and `.sdlc/successors/<slug>.md` for each that does |

#### Syntax

```
/sdlc:intent [--type bug|add|change] [--from <lead>]
/sdlc:intent --reslice <cycle id>...
```

#### Parameters

| Parameter | Description |
|---|---|
| `--type bug\|add\|change` | the kind of change — a bug fix, a feature addition or a feature change — so the interview asks that kind's questions. Optional: without it the type comes from the lead, or a new solution is detected, or the opening answer decides it and the skill confirms. There is no `new`: a new solution is only detected. A flag the answers contradict is asked about, never followed in silence |
| `--from <lead>` | begin from a lead — `azuredevops:1234`, `github:56`, `suggestion:<cycle>/<finding>`, `successor:<slug>`, or a path — fetched by `sdlc lead show` and recorded in the intent as `lead:`. It seeds the interview and never replaces it. A lead or a description is what a slicing is proposed from; a ready successor is started rather than sliced again |
| `--reslice <cycle id>...` | slice again intents already written — one to split it, several to join or re-cut them — while none has a spec (D-141). The first id keeps its cycle and becomes the first piece; each further piece opens as a cycle or waits as a successor; a cycle emptied by the cut is removed if it was never pushed, or recommended for abandonment if it holds something worth keeping. The person accepts the cut, as for any slicing |

#### Output

A cycle id, the sandbox it lives in — `sandbox/<id>`, in `../<repository>.sandboxes/<id>`, or the one it was opened in (D-144) — and `intent.md` — problem, wanted outcomes, fixed decisions, what
must not change, and a `## Open` section that is required even when empty. It never asks how to build the
thing, anything about the stack, or criteria in testable form; those belong to the plan, the declared
stack and the spec.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| the declared stack is not in place | `sdlc new` refuses | fill in `techstack.draft.md`, which adoption wrote, and rename it into place |
| a placed file is not committed | the sandbox starts from the integration branch as committed, so it lacks the file, and a command there says so | commit it on the integration branch, and open the sandbox again |
| one working copy was asked for, and anything is uncommitted | `sdlc branch <id> plan` refuses and names the files, because the switch would carry them into the cycle | the skill asks what you want done with them — commit, stash or discard |
| `--from successor:<slug>` that is not ready | `sdlc lead show` refuses, saying what it still waits for | wait until `sdlc successors` says `ready`; the person starts it then |
| `--reslice` names a cycle with a spec | refuses on that cycle: something already derives from its intent | `/sdlc:to-plan <id>` with what should change, which carries it down instead |
| `--reslice` names a cycle approved, abandoned or missing | refuses on that cycle, saying which | — |

#### Example

```
/sdlc:intent
```

Expect a cycle id and the sandbox it opened, then one question — *what is this cycle for?*, or
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
   the person's paragraph leaves off; then, given a lead or a description, the most atomic slicing it can
   find — pieces that each build, verify and merge against the integration branch alone, side by side
   where possible, chained only where one needs another's code — asked once: accept it, merge pieces, or
   keep the work whole. Each accepted piece that needs nothing unmerged is opened below and interviewed in
   turn; each that does is written as a successor (D-125)
2. `sdlc new`, which allocates the cycle id and claims it — that claim *is* the collision check — then
   `sdlc branch <id> plan` and the `git switch` it prints, so the working copy is on `cycle/<id>/plan`. The
   person runs neither
3. settles the type first — from `--type`, from a lead, or by detecting a new solution: a scaffolded
   solution with no cycle yet approved — because the type decides the questions
4. interviews one question at a time, each with example answers tailored to the solution, pointing
   different ways and carrying no figure: an opening, a short set for the type — a bug's reproduction, a
   feature's person and job, a change's dependants, a new solution's first use — then what must keep
   working, what is already decided, which areas it touches, and which of the active perspectives'
   concerns this piece needs — that last from a file under `.sdlc/reports/`, each concern explained for
   this change with a suggested answer, answered in free text and read back. An answer shaped as a
   solution is asked what its absence causes
5. where the person bounds the work with *simple*, *basic* or *nothing fancy*, asks once whether that
   means small in scope or plain in appearance
6. writes the intent, reading it once for every claim stated as fact before anybody judges it
7. shows `sdlc status <id> --stage intent --brief` verbatim and stops

**It writes the intent and stops** (D-186, D-188). Checking it is `to-plan`'s, which runs the intent's loop
before it writes anything below it. That hand-back is the human-in-the-loop point, and a skill that continued
would remove it while appearing helpful.

#### See also

- [`to-plan`](#to-plan) — the next step, when you are satisfied with the intent: it checks it first
- [`interrogate`](#interrogate), [`refine`](#refine)
- [Running a cycle](../docs/guides/running-a-cycle.md), §1

### `spec`

Derives testable acceptance criteria from a clean intent, producing the spec of a cycle.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | the planning chain, after `intent` |
| Started by | a person, once the intent has a clean interrogation round |
| Reads | `intent.md` as its only input: not the request that started the cycle, not the conversation, not the codebase for requirements |
| Writes | `.sdlc/cycles/<id>/spec.md` |

#### Syntax

```
/sdlc:spec [<cycle id>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle whose intent the spec is derived from. Optional: without it, `sdlc cycles --here` names the one this branch carries, or the skill asks which (D-142) |

#### Output

`spec.md`: numbered acceptance criteria, `Out of scope` with reasons, and `## Open`. Every criterion's
check has to be one this solution's testing layers can actually run, and an assertion a test in the
recorded run makes rather than a property of the run itself, like its length (`CON_12`, D-167). A criterion that holds over
several cases lists them under `Cases:`, and the list is all it covers at every later stage (`CON_23`,
D-132). Every intent `## Open`
entry ends in exactly one of three places — a criterion, `Out of scope` with a reason, or the spec's own
`## Open` citing the intent item.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| there is no intent | refuses | `/sdlc:intent` |
| the intent is not clean | refuses | `/sdlc:refine <id> intent`, then a fresh interrogation |
| a spec already exists | refuses, and never overwrites one | *take it to plan* (`/sdlc:to-plan`), which finishes its check, or `/sdlc:to-plan` with what should change — it names both |
| the intent is missing something it should have asked for | writes no criterion, and names it in `Out of scope` | `/sdlc:to-plan <id> <finding id>...` when a reader raised it; a new cycle when nobody did |

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
5. writes the spec — a perspective's item only where the intent asks for it, the rest named in `Out of
   scope`
6. hands back to `to-plan`, which checks it with its loop; run on its own, shows `sdlc status <id> --stage
   spec --brief` verbatim and stops

#### See also

- [`plan`](#plan) — the next stage
- [`interrogate`](#interrogate), [`refine`](#refine), [`steer`](#steer)
- [Running a cycle](../docs/guides/running-a-cycle.md), §2

### `plan`

Decides how a spec will be met and what will prove each criterion, producing the plan of a cycle. The
last document before a person approves.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | the planning chain, after `spec`. It is the last document written before a person approves, so it is the one a reviewer reads most closely |
| Started by | a person, once the spec has a clean interrogation round |
| Reads | `spec.md`, the codebase, the declared stack, the decision record and the theme |
| Writes | `.sdlc/cycles/<id>/plan.md` |

#### Syntax

```
/sdlc:plan [<cycle id>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle whose spec the plan meets. Optional: without it, `sdlc cycles --here` names the one this branch carries, or the skill asks which (D-142) |

#### Output

`plan.md`: `Shape and departures`, opening with **Decisions this plan makes:** one line per choice — the
list `sdlc draft approval` prints at the rename — then a `Files` table, numbered `Proof` entries each naming
its test in backticks, `Risk`, and `## Open`. The pull-request check reads those test names out and
confirms each one ran and passed.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| there is no spec | refuses | `/sdlc:spec <id>` |
| the spec is not clean | refuses | `/sdlc:refine <id> spec`, then a fresh interrogation |
| a plan already exists | refuses | *take it to plan* (`/sdlc:to-plan`), which finishes its check, or `/sdlc:to-plan` with what should change — it names both |
| the plan would declare one of the seven files only a person writes | never declares one; the schema refuses a plan that does | — |

#### Example

```
/sdlc:plan 20261002_1414_12
```

Expect it to read the code before it writes, then the loop, then a panel and a gate refusal naming
approval as the missing piece, the approval drafted for a person to rename, and the offer that follows it:
*build it*, which takes the plan out of the sandbox itself and carries it to a pull request (D-178).

#### Remarks

A plan says *how*, and commits to what will prove each criterion. The constraint to watch is proof
adequacy (`CON_21`). *Which case does this check never run, and which outcome does it never read?* If
either has an answer, the proof is not adequate yet — and an inadequate proof is worse
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
would have cost when it departs from it, in either direction, and `CON_27` asks a departure from a recorded
decision to be declared where it arises: the intent for an outcome, the plan for an approach. The plan's
reader is handed the decisions, and so is the diff reader that judges the change at verify.

**It probes what the platform does before planning on it** (D-129). Each behaviour the shape rests on that
nobody in the cycle has observed is run on a throwaway `probe/<id>/<slug>` branch outside the cycle's
worktree, and the plan's `## Assumptions proven` quotes what it showed and names the commit — or says
`None.` Nothing on a probe branch is governed, merged or pushed. It tells the person which behaviours it will
try before the first probe runs, so code appearing during planning is no surprise. Its proof is a table, one row per case a
criterion states (D-130).

What it runs, in order:

1. `sdlc constraints --for plan`
2. `sdlc status <id> --stage spec`, to refuse early
3. reads the spec and the code, then `sdlc stack`, `sdlc decisions` and `sdlc theme`
4. probes any platform behaviour the shape rests on that nobody has observed, then decides the shape,
   argues each departure, writes a proof row for every case of every criterion and chooses the tier —
   `local`, `contract` or `data`, by how reversible the change is
5. writes the plan — a perspective's item that would need a criterion the spec lacks goes under `Risk`,
   not into the proof
6. hands back to `to-plan`, which checks it with its loop, runs `sdlc park`, and drafts the approval; run on
   its own, shows `sdlc status <id> --stage plan --brief` verbatim and stops

#### See also

- [`to-plan`](#to-plan) — what runs it, checks it and drafts the approval; a person approves by renaming it into place, then says *build it*
- [`interrogate`](#interrogate), [`steer`](#steer)
- [Declaring the stack](../docs/guides/declaring-the-stack.md)
- [Running a cycle](../docs/guides/running-a-cycle.md), §2

---

### `to-plan`

Takes an intent to a plan — one cycle, or every one this branch carries — checking the intent, then writing each
cycle's spec and its plan, and running **the loop** on each, picking a cycle up wherever its planning stopped.
**The one place the interrogate-and-refine loop lives** (D-188). It hands back once, with every
panel, one question about suggestions, and the approvals drafted (D-142, D-158). **It is the step a person
takes after an intent**: they never need to run `spec`, `plan`, `interrogate` or `refine` themselves. **It is
also where a correction goes**: given what should change, or suggestions by id, it runs `steer` first and checks
again from the highest document that moved.

| | |
|---|---|
| Kind | playbook — it names the skills it runs and does no stage work itself |
| Stage | the planning chain, from an intent to a plan a person can approve |
| Started by | a person happy with their intent, who says *take it to plan*; or a person saying what should change in a cycle being planned |
| Reads | `sdlc cycles --here --for to-plan` for the work, or the cycle named; then what `spec` and `plan` read, per cycle |
| Writes | what `spec` and `plan` write, for each cycle; one suggestions report under `.sdlc/reports/`; an approval draft for each planned cycle |

#### Syntax

```
/sdlc:to-plan [<cycle id>] [<finding id>...] ["what to change"]
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the one cycle to take to plan. Optional: without it, `sdlc cycles --here --for to-plan` lists every cycle this branch carries with an intent, a plan not yet clean and no standing approval, and it takes them all |
| `<finding id>...` | open suggestions of the cycle to take in or decline, handed to `steer` before the loops run |
| `"what to change"` | a correction in the person's words, handed to `steer` before the loops run |

#### Output

One line per cycle — `planned`, or stopped and why — then each planned cycle's plan panel, verbatim, and one
free-text question about every open suggestion across the cycles, grouped by cycle and theme, then each
approval drafted with its path and the rename that places it, and the offer of *build it* for each, which
takes its plan out of the sandbox and carries it to a pull request (D-178). It stops at the plan: approving is
the person's, one cycle at a time, and drafting is not approving.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| nothing this branch carries is ready to be taken to plan | takes nothing, and names each carried cycle with where it stands | `sdlc status <id>` says where each one is |
| one cycle's loop will not converge, a reader reports a conflict, or a question only the person can answer comes up | stops that cycle where it is and goes on to the next | answer it, then *take it to plan* again: it picks the cycle up where it stopped |

#### Example

```
/sdlc:to-plan
```

Expect one line per cycle it will take, then a long run with no questions, then one line per cycle, the plan
panels, and one question about suggestions.

#### Remarks

**It runs in the person's session**, so its context grows with every cycle — the cost D-122 measured. Asked
once, the person chose that over a fresh session per cycle, to keep the run able to stop and ask. It names no
new session for the build: the build runs in a process of its own (D-178).

**Suggestions are asked about once, at the end.** A suggestion taken in at the spec costs one round less than
the same one taken in after the plan; one question across every cycle is the trade, and the hand-back says so.

#### See also

- [`spec`](#spec), [`plan`](#plan), [`interrogate`](#interrogate), [`refine`](#refine), [`steer`](#steer) — what it runs for each cycle
- [`draft`](#draft) — what drafts each approval, which a person places by the rename
- [`build`](#build) — the same shape for building

---

### `next`

Pulls the next wave from the backlog — every successor ready now started, every parked cycle whose predecessor
merged taken back and read again — plans them in one sandbox, and stops at the approvals (D-147).

| | |
|---|---|
| Kind | playbook — it names the skills it runs and does no stage work itself |
| Stage | the planning chain, for whatever the last merges made ready |
| Started by | a person, after merges land, or asking what is next for a piece of work |
| Reads | `sdlc backlog` for the wave; then what `intent`, `interrogate`, `spec` and `plan` read, per item |
| Writes | what `intent`, `spec` and `plan` write, per item; removes a parked cycle's `parked.md` through `sdlc unpark` |

#### Syntax

```
/sdlc:next
```

#### Parameters

None. The wave is everything `sdlc backlog` lists as ready now.

#### Output

One line per item — planned, read again and unchanged or changed, parked, or stopped and why — then the plan panels,
one question about suggestions, and the backlog still to come. It stops at the approvals.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| nothing in the backlog is ready | shows the backlog and the queue, and stops | merge what the queue says unlocks the most, then ask again |

#### Example

```
/sdlc:next
```

Expect one line per item it will take, a short interview for each successor, a long run with no questions, then
one line per item, the plan panels, one question, and the waves still to come.

#### Remarks

**It is local, and the factory never runs it.** An intent is an interview and a plan is read before it is approved;
the factory builds what a person approved and nothing more (D-143). Run after each wave merges, it carries a sliced
piece of work to completion one wave at a time.

#### See also

- [`to-plan`](#to-plan) — what it runs for the intents it opened
- [`intent`](#intent) — `--from successor:<slug>`, what it runs for each successor
- [Running a cycle](../docs/guides/running-a-cycle.md)

---

## Judging and editing

The two skills every stage above runs in turn until a round is clean. A person can also run either
directly.

### `interrogate`

Judges one artifact against the constraints in force, in a fresh process, then once more per perspective
the cycle gets — each in a fresh process of its own — reconciles what they found, and writes one findings
round. Never edits.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | the loop, whenever an artifact — intent, spec, plan or verify document — has just been written or changed |
| Started by | the loop `to-plan` runs, or a person |
| Reads | the constraints in force, the artifact, and the artifact above it — on a plan round `techstack.md` as well, and on a verify round the spec and the evidence — each reader's prompt written by `sdlc round prompt`, every answer read back by `sdlc round collect` |
| Writes | the next round of `.sdlc/cycles/<id>/findings/<artifact>.json`, the artifact's findings ledger, through `sdlc round` |

#### Syntax

```
/sdlc:interrogate [<cycle id>] <intent|spec|plan|verify>
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle the artifact belongs to. Optional: without it, `sdlc cycles --here` names the one this branch carries, or the skill asks which (D-142) |
| `<intent\|spec\|plan\|verify>` | the artifact to judge |

#### Output

A new findings round, written by `sdlc round` — the binary numbers it, stamps the time and records the
hash of the document judged, of the one above it, of each section and, on a verify, of the evidence, and
refuses it while anything above the artifact is unsettled (D-101). So documents are interrogated top
down, one at a time. The round is appended to the artifact's findings ledger, one file holding every
round, each chained by hash to the ones before (D-107). A finding is stored once and keeps its id across
rounds. Restating one where it already stands records nothing, and restating one in other words is
refused, naming the next free id. The reconciling reader is also shown what is still open and names each
finding that restates it, so a perspective raising a standing objection again keeps its id (D-108). Each finding cites exactly one rule by id and by that rule's own heading. A reader that finds two rules whose remedies
exclude each other reports a conflict rather than picking a side. After the first round, perspective
readers raise suggestions only about the sections that changed, and on a verify they read the cycle's first
round alone (D-164).

**It never edits the artifact**, and it does not recommend whether to run another round.

**A defect found in several places is one finding naming every place** in its locus, not one finding per
round for each instance in turn.

#### Refusals

- **A round about a document whose upstream is unsettled** — `sdlc round` names what above it has no
  round, changed after its round, or still has a blocker open. Settle the document above first.

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

**The binary assembles and collects; the readers judge** (D-117). `sdlc round prompt` writes every
reader's prompt — the block they share once, as `shared.md`, and each reader's own part in a file of its
own — and **`sdlc round run` starts them**: one fresh non-interactive process per prompt with no tools, no MCP
servers and no session file (D-118), from the round's directory rather than the checkout, handing `shared.md`
over as the system prompt and starting the rules reader a few seconds ahead, so the others find it cached
(D-163). **No skill starts a reader itself**, so a skill needs no permission beyond `Bash(sdlc *)` to run a
round. `sdlc round collect` reads the answers, applies the reconciler, demotes a blocker citing a rule its reader
was not handed (D-174), copies each new finding's rule line from the library (D-177), numbers new ids and drafts
the round, with each reader's usage beside it; the skill decides the dispositions of what no reader
raised again and collects once more with `--answered`/`--withdrawn`, which copy them in from the ledger,
records with `sdlc round`, and shows `sdlc status <id> --stage <artifact>` verbatim with at
most two lines of its own. A rule marked `Judged: per criterion` or `per outcome` is answered with a row
per item, and an answer missing one is sent back (D-119).

#### See also

- [`refine`](#refine) — what answers a blocking finding
- [Handling blockers](../docs/guides/handling-blockers.md)

### `refine`

Applies an artifact's findings to that artifact. Edits one planning document and nothing else, and never
judges.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | the loop, after an interrogation round raised blocking findings and while the cycle is not yet approved |
| Started by | the loop `to-plan` runs, or a person |
| Reads | the findings, the panel's `Loop` line, and the artifact |
| Writes | that one planning document |

#### Syntax

```
/sdlc:refine [<cycle id>] <intent|spec|plan>
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle the artifact belongs to. Optional: without it, `sdlc cycles --here` names the one this branch carries, or the skill asks which (D-142) |
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

Expect a three-column report — the finding id, the exit it took, what was done and where in the document —
followed by the panel. Stuck on one rule, it edits nothing: it shows the panel, names the rule in its own
words and what it is caught against, and lists the exits.

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
interrogate again; that is the loop `to-plan` runs (D-188).

#### See also

- [`interrogate`](#interrogate), [`steer`](#steer)
- [Handling blockers](../docs/guides/handling-blockers.md)

---

## Correction

### `steer`

Carries a person's mid-cycle correction down the artifact chain, editing the intent, spec and plan rather
than regenerating them — and takes in or declines suggestions a reader raised in the cycle, by their ids.

| | |
|---|---|
| Kind | single — it writes the correction into the documents, and checking them again is `to-plan`'s loop |
| Stage | correction, from a written intent until the gate. Before the spec, the person revising their own intent (D-141); after it, a change of *how* carried down |
| Started by | `to-plan`, given a correction or the suggestions a person chose; or a person directly |
| Reads | the correction, each named finding, and each artifact it reaches |
| Writes | the intent's `Fixed decisions` and `Wanted outcomes`, and each artifact it reaches. No rounds: those are `to-plan`'s loop |

#### Syntax

```
/sdlc:steer [<cycle id>] [<finding id>...] ["what to change"]
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle in progress. Optional: without it, `sdlc cycles --here` names the one this branch carries, or the skill asks which (D-142) |
| `<finding id>...` | open suggestions from this cycle's latest rounds — `F_03`, `accessibility.F_01` — each to be taken in or declined. Name every one in one run: the chain is checked again once |
| `"what to change"` | the correction, in the person's words |

#### Output

Records the correction under the intent's `Fixed decisions`, with attribution. Before the spec exists, a correction
in words may also add, drop or reword a wanted outcome, written where it belongs rather than as a decision (D-141). A suggestion taken in
goes beneath the wanted outcome it sharpens, in the person's words, and a declined one a *not in this
cycle* fixed decision, each naming the finding. A suggestion that would add or widen an outcome is
declined into a successor that keeps its id (D-128); the next round's reader finds it settled and records it `answered`. Edits rather
than regenerates — a regenerated artifact has a findings history about a document that no longer exists.
Usually the spec needs nothing and the plan's `Shape and departures`, `Files` and often `Proof` change.
Checking again is held rather than trusted: a round records the hash of the artifact above it, so editing
the intent makes the spec's latest round stale, and the gate refuses until `to-plan`'s loop has run again from
the highest document edited.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| the request cuts the intent into pieces, or joins two | refuses: that is a scoping decision | `/sdlc:intent --reslice <id>...` while there is no spec |
| a finding id that is not an open suggestion of this cycle's latest rounds | refuses on that id, and says what it is | a blocker is `/sdlc:refine`'s; another cycle's suggestion is `/sdlc:intent --from suggestion:<cycle>/<finding>` |
| the cycle is approved | refuses | `sdlc draft approval <id> --reopen` first, which it names |
| the cycle is abandoned | refuses; abandoned is terminal | open a new cycle |
| the comment changes the problem rather than the how | says so | a new cycle, which it offers |

#### Example

```
/sdlc:to-plan 20261002_1414_12 "use the existing cache abstraction rather than a new one"
```

`to-plan` runs `steer`, which asks why, adds a dated and attributed line to the intent's `Fixed decisions` and
edits the plan; then `to-plan` checks again from the intent down.

#### Remarks

It exists because of cost. *"Use the existing cache abstraction rather than a new one"* arrives
mid-cycle, and doing that by hand means editing and re-interrogating three documents. Nobody pays it, so
the correction ends up in conversation where it is lost — and a path so costly that it pushes information
out of the system is worse than no path. No constraint loosens.

**Steering changes how, not what** — which approach, which abstraction, which order, what to avoid — **except
for a suggestion a reader raised**, which the person may take in by id (`CONCEPT.md` D-97). The spec may
only derive from the intent, so this is the one way a perspective's concern becomes a criterion: a person
choosing it. A comment that changes the problem is a different intent, a new outcome nobody raised is
a new cycle, and a raised suggestion may only sharpen an outcome the intent already wants — one that adds
or widens is a successor (D-128).

One or two questions — what to do instead, and why — then it records the correction, edits each artifact
it reaches, reports one line per artifact and the highest one it edited, and shows `sdlc status <id>` verbatim.
Run on its own, it offers *take it to plan*, which checks what changed (D-186, D-188).

#### See also

- [`to-plan`](#to-plan) — what runs it, and checks again after it
- [`intent`](#intent), [`refine`](#refine)
- [Running a cycle](../docs/guides/running-a-cycle.md)

---

## The gate

### `draft`

Prints what belongs in one of the files only a person writes — an approval, an abandonment, the declared
stack, or the solution document.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
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
| `approval <id>` | the gate decision for that cycle. `--changes-requested`, `--deny` or `--reopen` drafts a refusal on the same terms. Deviations are no longer answered here: the build reports them, and merging its pull request accepts them (D-181) |
| `abandon <id>` | why that cycle stopped |
| `solution` | the whole `solution.md`. `--standard profile\|existing`, `--approval required\|none` and `--coverage-floor <percent>` each move that one key |
| `techstack` | the declared stack. `--empty` drafts nothing |

#### Output

- **`approval <id>`** — the gate decision, covering all three planning documents, with a hash of each.
  Written as a complete file beside the ledger, named for its decision, and placed by renaming it. A
  revisit reproduces every decision already recorded above the new one, because the ledger is
  append-only and the rename overwrites. The three refusal flags print the `reason` key empty, and the
  gate rejects an entry that refuses a cycle without saying why.
- **`abandon <id>`** — why a cycle stopped. Terminal, and the reason is left empty because the file is
  rejected without one. Printed for a person to place by hand. When the cycle took suggestions in, each is
  listed as a `declining <id>:` line: deleted, it returns to the pool; kept with a reason, it is declined
  (D-115).
- **`solution`** — the whole `solution.md`, written beside itself for a rename. Keys carried through as
  recorded and prose through unchanged, so renaming can neither reclassify the solution nor cost anybody
  what they wrote. `--standard`, `--approval` and `--coverage-floor` each move that one key, and say so.
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
| a drafter that does not exist | refuses, and lists the five that do | — |
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

Seven files are written only by a person — `approval.md`, `abandoned.md`, `constraints.md`,
`decisions.md`, `areas.md`, `solution.md` and `techstack.md` — and **a recorded decision means something precisely
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

**The other three are the harvest's, and the verbs split on it.** A constraint's wording *is*
the decision, so there is no finished entry to hand back; the areas are drafted only by `sdlc draft
harvest`, from what three cycles' plans proposed (D-150, D-191).

**A blocking finding cannot be waived by any mechanism** (D-27): there is no field for it in the entry
and no refusal code at the gate that acceptance would clear. It is answered, or the cycle does not
advance.

It runs the matching command and shows what it prints verbatim, including the instructions beneath it,
and adds nothing:

```
sdlc draft approval <id>     [--changes-requested | --deny | --reopen]
sdlc draft abandon <id>
sdlc draft solution          [--standard profile|existing] [--approval required|none]
                             [--coverage-floor <percent>]
sdlc draft techstack         [--empty]
```

It shows the rename that places the file and **does not run it, or offer to**, and it does not read the
hook's denial as a configuration problem.

#### See also

- [`build`](#build) — next after an approval; after a refusal, the stage that needs work
- [`harvest`](#harvest) — the record files a harvest proposes, drafted by `sdlc draft harvest`
- [Handling refusals](../docs/guides/handling-refusals.md)

---

## Build and verify

### `build`

**A playbook.** Carries approved cycles to their pull requests — the one named, or every approved cycle when none
is (D-178, D-187). In a person's session it asks the gate and runs `sdlc build`, which commits the placed approval,
admits the plan, opens the build's worktree and starts this playbook again in a process of its own with
`--unattended`. There, for each cycle, it runs `implement`, `verify`, `remediate` while it converges — deviating
within limits before it escalates (D-179) — and `pr`. It hands back the report: every deviation, every choice the
build made that nothing approved settled, and every concern (D-181).

| | |
|---|---|
| Kind | playbook — it names the skills it runs and does no stage work itself |
| Stage | build, verify and the pull request, once the gate is satisfied |
| Started by | a person saying *build it*, after the cycle's planning documents are approved; with `--unattended`, the process `sdlc build` starts, and the factory |
| Reads | `sdlc gate`, then `sdlc cycles --queued` and `--claims` in its own process |
| Writes | nothing itself: what `implement`, `verify`, `remediate` and `pr` write, for each cycle, in its own worktree on `cycle/<id>/build-unattended` |

#### Syntax

```
/sdlc:build [<cycle id>] [--unattended]
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the approved cycle to build. Optional: without it, every approved cycle ready to build — `sdlc cycles --here --for build` says which before it starts |
| `--unattended` | run as the build's own process: ask nothing. `sdlc build` passes it, and the factory's pipeline runs it. A person never needs to type it |

#### Output

`sdlc report <id>` and `sdlc status <id> --brief`, verbatim, and the pull request's link — or, escalated, a draft
pull request and what stopped it. For several cycles, one line per cycle and `sdlc sitrep` verbatim.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| the gate refuses — no cycle, abandoned, a missing document, nobody approved, an approval ledger it cannot read, a refusal standing, a findings round it cannot read, a document never interrogated, a document changed since approval, a blocking finding still open, a round that judged a document which has moved since, or a round the approval anchored that has been rewritten | refuses | each has a different next step — [Handling refusals](../docs/guides/handling-refusals.md) |
| the build's process cannot start, or ended without a report | `sdlc build` refuses, saying why; the build is never run in the person's session instead | [Handling refusals](../docs/guides/handling-refusals.md) |
| a named cycle not queued | `sdlc cycles --queued <id>` refuses with why, and nothing else is built | — |

#### Example

```
/sdlc:build 20261002_1414_12
```

Expect one line saying the build has started in a process of its own, a wait while it runs, then the report —
deviations, choices and concerns, each by what it says — the panel, and the pull request's link.

#### Remarks

**It does no stage work itself** (D-186): code, tests and evidence are `implement`'s, the verdict is `verify`'s,
the loop after a blocked verdict is `remediate`'s, and the pull request is `pr`'s. **It never merges.**

**Everything unattended rests on the remediation bound.** A cycle that cannot be made to pass stops rather than
reaching a ready pull request; the bound is on progress, not passes, and there is deliberately no ceiling behind
it (D-66), which is why the factory's pipeline job has a timeout.

**One name for building** (D-187). `batch-build` was the many-cycle form and `sdlc build-run` the command that
started a person's build; both are gone. Given no id it builds every approved cycle, each independently in its own
worktree; with `--unattended` it asks nothing, which is the factory's run.

#### See also

- [`implement`](#implement), [`verify`](#verify), [`remediate`](#remediate), [`pr`](#pr) — what it runs
- [Running a cycle](../docs/guides/running-a-cycle.md), [Running the factory](../docs/guides/running-the-factory.md)

### `implement`

**A single-responsibility skill.** Changes code and tests to do what it is handed — the approved plan, a
verification's unmet criteria and findings, or a person's instruction — recording every deviation from the
approved text and every choice the approved text left open, then builds, tests, commits and records the evidence
a verification reads (D-186, D-187). It never verifies and never opens a pull request.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | build, inside `build` or `remediate` |
| Started by | the `build` playbook, handed the plan; the `remediate` playbook, handed findings or a person's instruction |
| Reads | `plan.md`, the decisions, the perspectives' build lists, `verify.md` and the verify findings when handed them, and the code |
| Writes | source and tests, `deviations.md` — deviations where the approved text cannot be followed, choices where it was silent — commits on the cycle's branch, and the evidence |

#### Syntax

```
/sdlc:implement <cycle id>
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle whose work it is handed. Required: a playbook always names it |

#### Output

`sdlc status <id> --brief` verbatim and at most five lines: what it implemented or changed, what the build and the
tests said, and how many `Noticed:` lines, deviations and choices the commit carries. Underneath, commits on the
cycle's branch — the change, then the evidence: the diff, the commit it is measured from, the runner's own output,
one normalised test summary, the coverage and the timings. **Verify reads only these files** (`CON_22`).

**`sdlc scaffold <id>` is how two kinds of file arrive, and the plan is what asks for either**: the skeleton and its
build settings on a new solution's first cycle, and only the build settings files the plan declares on a solution
adopted into existing code — where `Directory.Build.props` can make code the cycle never touched fail to compile,
which the plan was required to have planned for.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| a criterion the code cannot be made to meet | stops: no deviation changes what *done* means (D-179) | the playbook escalates; a person reopens the plan |
| a fix needs the intent changed, a wider scope, or a file another cycle claims | stops | a successor, or a reopen |
| a red build or failing tests | stops, and says so. It does not adjust a test to make it pass | — |
| a whole-codebase coverage fall it cannot explain from the change | stops as not finished, with what fell and where | lower `coverage_floor` through `sdlc draft solution`, or re-measure the integration branch |
| the turn ends over a failing suite, with code written and no evidence, or with code moved since the evidence | the turn-end hook sends the turn back | — |

#### Example

```
/sdlc:implement 20261002_1414_12
```

Run by a playbook rather than typed. Expect the panel and five lines.

#### Remarks

It implements what it is handed and only that (`CON_25`): creep, in an approved file or a new one, is what reaches
review dressed as approved work. Something worth fixing that it was not handed is a `Noticed:` line in the commit
message. **Where the approved text cannot be followed, it records a deviation and keeps going** (D-156), quoting the
text it departs from and the blocker with its evidence; **where the approved text was silent and a reviewer would
want to know what it chose**, a **choice** (D-180). Three limits hold every deviation: never what *done* means,
never wider, and never on its own account without end (D-179). A person's change is recorded on the entry in their
words, and a redo that still bends the approved text is a new deviation with `Replaces: DEV_nn`.

It fixes the cause rather than the symptom — no loosened assertion, no sleep or retry, no substituted thing under
test, no skipped test — and checks the suite actually ran, reading the test count rather than the exit code.

#### See also

- [`build`](#build), [`remediate`](#remediate) — the playbooks that hand it work
- [`verify`](#verify) — what judges it next, in a fresh process

### `verify`

Adjudicates a build against its spec, from the recorded evidence alone, in a fresh process that did not
see the build. Never edits code.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | build and verify, after the build has recorded its evidence |
| Started by | the `build` playbook after `implement`, `remediate` after each pass, or a person |
| Reads | `spec.md`, `plan.md` and the evidence — `tests.json`, `diff.patch`, `base.sha`, `build.log` where there is one, and `proof-sources.txt` when a proof lives in code the change did not touch, all in the adjudicator's prompt |
| Writes | `.sdlc/cycles/<id>/verify.md` |

#### Syntax

```
/sdlc:verify [<cycle id>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the built cycle to adjudicate. Optional: without it, `sdlc cycles --here` names the one this branch carries, or the skill asks which (D-142) |

#### Output

`verify.md`: an entry for every criterion, numbered to match, each citing the evidence it rests on
(`CON_22`), and `## Open` for limits of the evidence. **Never edits code.**

It writes `pass` or `blocked`. **`escalated` is computed, not declared**: it is what a remediation loop
that stopped making progress makes of a blocked cycle (D-66). `sdlc verify-chain` checks mechanically
that every criterion has an entry and that a `pass` rests on named tests that passed.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| the evidence is missing | says so and stops — that is a build that did not finish | `/sdlc:build <id>` |
| the outcome is `blocked` | does not start remediating | `/sdlc:remediate` — inside *build it*, the build goes round on its own |

#### Example

```
/sdlc:verify 20261002_1414_12
```

Expect `verify.md` with a verdict per criterion, and the outcome in a line or two — on `blocked`, with
each criterion marked Not met by its number and the first sentence of its reason.

#### Remarks

Verify is the only artifact whose entire value is being derived. A claim that reaches beyond its
evidence turns an auditor into an author. So it reads the evidence rather than running the tests —
running them would produce new evidence instead of adjudicating the build's, and the next reader could
not tell which run the verdict describes. It does not read the source to decide whether a criterion is
met: reading the implementation and concluding it looks correct is the most tempting way to write a claim
with no evidence behind it.

**The session that runs it does not adjudicate** (D-01). It is often the session that built the change,
so it has `sdlc round prompt` write the adjudicator's prompt — the plan, the spec, the evidence, the
constraints, the perspectives' questions and the method — and `sdlc round run` start a fresh process with no
tools to write the verdict criterion by criterion, and records what that returns byte for byte
with `sdlc round verdict`, which refuses an answer that is not a verify document. Before the round,
`sdlc evidence check` reports what the verdict cites that the evidence does not hold, and refuses nothing
(D-120). It interrogates the verify document like any other artifact, and
a blocking finding is answered by a new adjudication given the findings, never by `refine` or an edit in
the session. The verify round also runs the **diff reader**, which reads the change itself against the
intent, the plan and the decisions; its findings, ids under `diff.`, go to `remediate` whatever the outcome
reads (D-139). Then it shows `sdlc status <id>` and `sdlc pr-ready <id>` verbatim with at most two lines of
its own.

Per criterion: which test did the plan name, does the summary say it ran and passed, and does that test
actually establish the criterion? The third question is where judgement lives, and where the verification
may disagree with the plan — a test that passed but does not reach its criterion is **not met**.

#### See also

- [`pr`](#pr) — next on `pass`
- [`remediate`](#remediate) — next on `blocked`
- [Handling blockers](../docs/guides/handling-blockers.md)

### `remediate`

**A playbook: *fix it*.** Takes a blocked verification, an escalated cycle with the person's instruction, a
change a person asks for at the pull request, in words, or a pull request that conflicts — which `resolve-conflict`
brings up to date first — and goes round `implement` and `verify` while each pass
converges. When it stalls it may deviate once, within limits, before it escalates (D-179). Then `pr` updates the
pull request (D-185, D-187). It changes no code itself.

| | |
|---|---|
| Kind | playbook — it names the skills it runs and does no stage work itself |
| Stage | build and verify, after verify recorded `blocked`; after an escalation, with the person's instruction; or at the pull request, when a person says what to change or it conflicts |
| Started by | the `build` playbook, inside its own process; or a person, with what they want instead |
| Reads | `sdlc cycles --here --for remediate`, `sdlc pr-ready <id>`, and the verify outcome |
| Writes | nothing itself: what `resolve-conflict`, `implement`, `verify` and `pr` write, in the worktree `sdlc branch <id> remediate` prints |

#### Syntax

```
/sdlc:remediate [<cycle id>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle — `blocked`, `escalated`, or passed with its verdict not yet on the integration branch. Optional: without it, `sdlc cycles --here --for remediate` names the one this working copy carries, or the skill asks which (D-142) |

#### Output

One line per pass, each with a line per unmet criterion and finding — what was wrong, what changed, and whether
`implement` was confident that was the cause — then the panel, and on `pass` the report.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| the criterion itself looks wrong, or the code cannot be made to meet it | escalates: no deviation changes what *done* means (D-179) | reopen the plan — `sdlc draft approval <id> --reopen` |
| a regression, or two passes in a row reducing nothing, with no deviation taken since | one more pass, `implement` told to take a different approach recorded as a deviation | — |
| the same again after that | stops: escalated | — |
| three deviations bend the same approved text and the verification still blocks | stops: escalated | — |
| a person's "no" that does not say what to do instead | asks what they want in its place, until it knows | say what instead |
| an instruction that would reach the intent, the spec or the plan, or widen the cycle | says so, and does not act | a reopen, or a successor |

#### Example

```
/sdlc:remediate 20261002_1414_12
```

#### Remarks

**It stops when it is not getting anywhere, not after a set number of tries.** When a pass undoes what an earlier
one answered, or two passes in a row reduce nothing, it may take one different approach — never changing what
*done* means, never widening the cycle — and it escalates when that stalls too, or when a third deviation bends the
same approved text (D-179). That stopping is the only reason an unattended build is safe to run at all. The blind
three it replaced failed an honest cycle that needed a fourth pass, and there is deliberately no ceiling behind the
progress rule now (D-66). A pass is a verify findings round after the first, so progress is read from what happened
rather than from a counter anybody could edit.

**It does not adjudicate its own remediation**: after each pass `verify` runs its adjudicator in a fresh process
that never saw the fix (D-159). **It does not check in on a count**: the build runs with nobody to ask (D-178).

**At the pull request it takes a person's change in words** (D-181) — *change deviation 2: use X instead*, *revert
that choice* — hands it to `implement`, which records their words on the entry and retires the pass with
`--retire-pass`; then `verify`, and `pr` pushes to the same pull request with the report refreshed. After an
escalation it starts the same way, from the person's instruction.

#### See also

- [`implement`](#implement), [`verify`](#verify), [`pr`](#pr) — what it runs
- [Handling blockers](../docs/guides/handling-blockers.md)

## Pull requests and conflicts

### `pr`

Pushes a cycle's branch and opens its pull request into the integration branch. The only stage that
touches the remote.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | pull requests: after a verification passed, after a plan is approved, or after a build that did not pass |
| Started by | the build, once its verification passes — saying *build it* was the consent to push (D-178); or `remediate` updating one already open; or a person, and then **it asks before the push** |
| Reads | `sdlc pr-ready`, or `sdlc gate` and `sdlc verify-chain`, `integration_branch` in `solution.md`, and the cycle's documents for the body |
| Writes | the pushed branch — `cycle/<id>/plan`, `build`, `build-unattended`, or an older cycle's bare `cycle/<id>`, or a slicing's `slices/<slug>/plan` — and a pull request |

#### Syntax

```
/sdlc:pr [<cycle id>...] [--plan | --draft] | --all
/sdlc:pr --plan
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the verified cycle to push — optional, and without it `sdlc cycles --here --for pr` names the one whose verify passed, or it asks which, and the question before the push names it (D-158) — or, with `--plan`, every cycle of one slicing |
| `--plan` | an approved plan, before it is built: admission to the integration branch, asked of `sdlc gate`. Merging it deletes the branch. A plan already there and approved again is opened as an *amended plan*, leading with each reopen's reason (D-123). Given several ids, the approved plans of one slicing and its successors, by one pull request from `slices/<slug>/plan` that `sdlc slices` composes; a plan that needs a file only a sibling creates is refused, because that piece is a successor (D-125). Given no id, in a sandbox, every approved plan the sandbox carries and the successors it wrote, composed by `sdlc admit` onto `cycle/<id>/plan` or `slices/sandbox-<id>/plan`; what is not approved stays, and each cycle taken is named before the push is asked for (D-144) |
| `--draft` | a `blocked` or `escalated` build, as a draft carrying the attempted code, the verify and the evidence, its body leading with the criteria marked Not met and each open blocker's rule, gist, place and fix |
| `--all` | every cycle that is ready, asking once before the first push |

#### Output

The body is the cycle rather than a summary somebody invented: the intent's problem in the intent's
words, the criteria with the verification's verdict on each, the tier, the risk section, and anything in
an `## Open` region, because a reviewer should meet the limits before they approve. It links the work
item where the solution tracks them.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| `sdlc pr-ready` refuses: not verified, did not pass, escalated, the chain broken since verification, or a test run a local build judged (D-135) | refuses, and does not push past it — except `--draft`, which opens a draft for did not pass or escalated, and nothing else | [Handling refusals](../docs/guides/handling-refusals.md) |
| `--plan` and `sdlc gate` or `sdlc verify-chain` refuses, a local build's rounds included | does not push the plan | [Handling refusals](../docs/guides/handling-refusals.md) |
| the solution is `local` or has no remote | stops | it walks you through creating one, then re-running adoption with a platform, and setting the branch policy `sdlc branch-policy` prints |
| on Azure DevOps | names the organisation, project and repository rather than trusting the CLI to detect them, and keeps the body under the 4,000-character description cap (D-161) | — |
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

**It is not approving the merge.** A person does that, and never the agent that wrote the change. **The merge
is the acceptance** — of the change and of every deviation, choice and concern in the report that leads the body,
`sdlc report <id>` (D-181). The local approval defended against an agent that forgets; the review is what defends
against a human who did not look, and they are different jobs.

It runs `sdlc pr-ready <id>`, then `sdlc doctor` and `git remote -v` to check there is somewhere to push,
then `git push -u origin cycle/<id>` and the platform's own tool — `gh pr create` or
`az repos pr create`. It never asks about a harvest: its report suggests `/sdlc:harvest` once the pull request has
merged (D-182). With `--all` it shows `sdlc cycles --pr-ready` verbatim and asks once before the
first push; the question is about volume rather than permission, because every cycle that ever passed is
eligible, including one somebody has forgotten.

#### See also

- [`build`](#build)
- [Handling refusals](../docs/guides/handling-refusals.md)
- [Running a cycle](../docs/guides/running-a-cycle.md), §5

### `resolve-conflict`

Brings one cycle's pull request up to date with the integration branch when it conflicts, or goes red after
another merge — inside the cycle's declared files, ending in fresh evidence. The fresh verify is `remediate`'s,
which runs this first.

| | |
|---|---|
| Kind | single — it resolves and records evidence; the verify and the pull request's update are `remediate`'s |
| Stage | merging, when a pull request into the integration branch conflicts or its re-check fails |
| Started by | `remediate`, when a person says *fix it* on a pull request that conflicts; or a person directly |
| Reads | the cycle's plan for its declared files, the integration branch, and the failure |
| Writes | a merge commit on the cycle's branch, with the old verify removed and fresh evidence recorded, in the worktree `sdlc branch <id> remediate` prints. Pushes nothing |

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
verify removed, fresh evidence recorded, and a report of at most five lines. Run on its own, it offers *fix it*,
which verifies the resolution and updates the pull request. **It never merges the pull request.**

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

Expect what conflicted, what was kept from each side, and the offer of *fix it*.

#### Remarks

**It edits, and does not judge its own work** (D-01, D-86). A resolution changes code, so the verify that
passed is void; what proves nothing regressed is the suite and a fresh verify against the spec. That is
what answers the old reason this skill was confined to a list of additive files — *a skill that resolved
whatever happened to conflict would quietly become the thing that merges real code* — because every
resolution is rebuilt, re-verified by `remediate` and reviewed on its pull request before anybody merges it.

**It stays inside the declared files.** A conflict that cannot be resolved there is a change of plan.

**It merges rather than rebases**, because the branch is already pushed and reviewed and is squashed when it
lands.

#### See also

- [`remediate`](#remediate) — what runs it, then verifies
- [`verify`](#verify), [`build`](#build)

---

## Reading and deciding

Four skills that read the record and change nothing in a cycle. Anyone may run them at any time.

### `status`

Shows where a cycle has got to — the stage panel, its checks, the gate, and the next command.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | any, after any stage |
| Started by | anyone, constantly |
| Reads | the cycle's documents, rounds and approval |
| Writes | nothing |

#### Syntax

```
/sdlc:status [<cycle id>] [intent|spec|plan]
```

#### Parameters

| Parameter | Description |
|---|---|
| `<cycle id>` | the cycle to show. Optional: without it, `sdlc cycles --here` names the one this branch carries, or the skill asks which (D-142) |
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
| Kind | single — one job, and it starts no other skill |
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

- [`harvest-constraints`](#harvest-constraints) — adding one of the solution's own from what the cycles argued; `sdlc constraints --new` for one a person has in mind
- [Declaring the stack](../docs/guides/declaring-the-stack.md) — the stack rules

### `suggestions`

Lists the open non-blocking findings a solution has accumulated, each concern once; with `--all`, every one
and what became of it.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | any |
| Started by | a person who asks what has been noticed and not acted on, what became of a suggestion, or is looking for small work worth picking up |
| Reads | each cycle's suggestions ledger and findings ledgers, the intents, leads and abandonments that decided a suggestion, and the `Noticed:` lines in builds' commit messages on cycle branches |
| Writes | nothing |

#### Syntax

```
/sdlc:suggestions [cycle id] [--all]
```

#### Parameters

| Parameter | Description |
|---|---|
| `cycle id` | one cycle. Optional; without it, every cycle |
| `--all` | every suggestion, not only the open ones, each with one word — `open`, `accepted`, `declined`, `answered`, `withdrawn` — and the cycle that decided it |

#### Output

**Two stores, listed apart.** A suggestion was raised by a fresh reader judging a document and carries a
round and an id. A `Noticed:` line was written by a build that saw something it was not allowed to fix,
and lives in that build's commit message on the cycle's branch. Only the rounds used to be read, so the
three a dice-roller cycle's build wrote were invisible. Asked which to pick up, the agent adds at most
two lines under `My reading:`.

**Each concern once** (D-115). A suggestion is the cycle's, so one the intent and the plan both raised is
one line naming both. **A status is worked out, never stored**: from `taking in` and `declining` in an
intent, a lead of `suggestion:<cycle>/<id>`, and `declining` lines in an `abandoned.md`. A cycle abandoned
after taking one in accepts nothing, so the suggestion reads `open` again.

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

**It aggregates nothing, on purpose.** What keeps going wrong *across* cycles is `harvest-constraints`.
This answers a narrower question — what is outstanding — and the two read the same files for opposite
halves of them.

It runs `sdlc suggestions [<id>]` and shows it verbatim.

#### See also

- [`harvest-suggestions`](#harvest-suggestions) — what becomes of each one once its cycle is completed
- [Handling blockers](../docs/guides/handling-blockers.md)

### `theme`

Shows the solution's theme — the roles its surfaces use and the values that fill them — or, when it has none, the
skeleton of roles for a person to fill. Never proposes a value.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | any. `init` mentions it where a solution has a visual surface |
| Started by | a person who asks what colours or fonts the solution uses, or needs a theme |
| Reads | `.sdlc/theme.json`, if there is one |
| Writes | nothing |

#### Syntax

```
/sdlc:theme
```

#### Parameters

None.

#### Output

`sdlc theme` verbatim: each role and its value, `--` where a role is empty, and whether the theme is settled — or,
for a solution with no theme, the skeleton of roles to create `.sdlc/theme.json` from. **Roles, not values**:
`cancel` is a role, `blue-400` a value wearing a role's clothes, and a role survives a rebrand where a value does
not.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| asked to fill in a value | does not, and does not offer to | a person or a designer chooses it |

#### Example

```
/sdlc:theme
```

#### Remarks

**It never proposes a colour.** A colour is a brand's or a designer's, and a tool inventing one is the only
proposal nobody asked for. It was `propose theme` until `propose` was retired (D-191); the skeleton is now what
`sdlc theme` prints when there is no theme.

**`.sdlc/theme.json` is not protected.** Nobody is judged against a colour, so a cycle may declare the file and
change a value as declared work. **Enforcement is a linter's**, not a constraint's: a colour outside the theme is
mechanical, so it belongs below a constraint, where it can fail a build.

#### See also

- [`constraints`](#constraints), [`draft`](#draft)

---

## Harvesting

Seven skills that take back what the completed cycles settled, then close them. **A cycle is completed** once its
pull request merged after a passing verify — a person accepted it — or a person abandoned it, merged or not (D-190). A cycle in
flight, or one whose verify passed and whose pull request has not merged, is read by none of them. What a harvest
places binds the **next** cycle, never one in flight.

### `harvest`

Takes back what the completed cycles settled, one aspect at a time, each answered with the person before the next,
then closes the cycles every aspect has answered. Proposes and never places.

| | |
|---|---|
| Kind | playbook — runs `harvest-constraints`, `harvest-decisions`, `harvest-suggestions`, `harvest-stack`, `harvest-areas` and `close-cycle` |
| Stage | none. After a cycle's pull request has merged, which the build's report suggests (D-182) |
| Started by | a person, once a pull request has merged, or who asks to bring the records up to what the cycles settled or to clear out old cycles |
| Reads | `sdlc harvest`: each completed cycle not closed yet, and the aspects it still needs |
| Writes | nothing itself — each skill it runs writes its own proposal file |

#### Syntax

```
/sdlc:harvest [--cycles <id>,<id>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `--cycles <id>,<id>` | read those completed cycles alone and leave the rest for a later sitting (D-166) |

#### Output

`sdlc harvest` lists each completed cycle not closed yet with the aspects it still needs, and each finished cycle
waiting for its pull request to merge. The playbook then runs, in order and **one at a time**, each aspect still
listed — the rules, the decisions, the suggestions, the stack, the areas — and an aspect already answered for every
cycle is skipped. Each writes its proposal file and hands back a short report; the next starts only once the person
has marked that one and it has been drafted, reviewed and recorded. Then `close-cycle`.

#### Refusals

None of its own. Each skill it runs refuses for itself.

#### Example

```
/sdlc:harvest
```

Expect a two- or three-line overview, then the first aspect's short report and the path of its proposal file to
mark.

#### Remarks

**One aspect at a time, because the whole was too much to sort through** (D-191). The single report this replaced
put every rule, decision, stack row, area and suggestion in front of a person at once; one real report ran to
hundreds of kilobytes. Each hand-back is the count, one line per proposal saying what and why, and the one thing
needed from the person. The full reports are kept under `.sdlc/reports/`.

**A person may stop after any aspect.** The ledger keeps which aspects each cycle has answered, so the next
`sdlc harvest` picks up where this stopped. It was `/sdlc:mine`, one skill doing every aspect; that name and
`/sdlc:propose` are gone (D-191).

#### See also

- [`close-cycle`](#close-cycle), [`suggestions`](#suggestions), [`draft`](#draft)

---

### `harvest-constraints`

Reads what the completed cycles' findings argued and proposes the constraints the solution is missing, each with its
evidence. Proposes and never places.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | none — the first aspect `harvest` runs |
| Started by | `harvest`, or a person who asks what keeps going wrong across cycles |
| Reads | `sdlc harvest constraints`, and the fresh readers `sdlc harvest read` starts, one per cycle and document with findings |
| Writes | its proposal file under `.sdlc/reports/`. The draft by `sdlc draft harvest`, the ledger by `sdlc harvest record`; **never** `constraints.md` |

#### Syntax

```
/sdlc:harvest-constraints [--cycles <id>,<id>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `--cycles <id>,<id>` | read those completed cycles alone (D-166) |

#### Output

**Earned backward from the findings.** The report lists rules cited in three or more distinct cycles, rules argued
longest inside one, findings that rested on no rule grouped by who raised them, perspectives at the bar, abandoned
cycles' reasons, deviations by the rule behind them, criteria that failed more than one verdict, rules over their
word budget, and the concerns watched below the bar. Every item gets a proposal — `add`, `supersede` or `reword`,
and **no remove** — *already recorded*, a note for the framework, or a reason under `## Not proposed`. A concern
below the bar goes under `## Watch`, with a `Why:` line under a new one: what it prevents and what it promotes (D-194).

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| `constraints.draft.md` still beside its file at `sdlc harvest record` | records nothing | rename it into place or delete it, then record |

#### Example

```
/sdlc:harvest-constraints
```

#### Remarks

**A tool may propose a rule when it brings the evidence, and may not shape a rule somebody just typed.** Every
proposal carries the cycles and finding ids behind it. A rule a person already has in mind is `sdlc constraints
--new`: the empty entry at the next free `LOC_` id. **Three tests on every proposal**: its `Forbids` names something
that becomes illegal; no analyzer, schema or hook could decide it; it is incompatible with at least one plausible
implementation. The second disqualifies most, and sends them to a home that fails a build.

**An argued rule is usually settled by a decision**: this answers only whether the rule itself needs changing, and
`harvest-decisions` reads what the plan finally did.

#### See also

- [`harvest-decisions`](#harvest-decisions), [`constraints`](#constraints)

---

### `harvest-decisions`

Reads what the completed cycles settled and proposes the decisions the solution should record. Proposes and never
places.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | none — the second aspect `harvest` runs |
| Started by | `harvest`, or a person who wants to record a decision a cycle settled |
| Reads | `sdlc harvest decisions`: the passages each completed cycle stated, its reopens, its declined deviations, the rules it argued, and the standing decisions |
| Writes | its proposal file under `.sdlc/reports/`. The draft by `sdlc draft harvest`, the ledger by `sdlc harvest record`; **never** `decisions.md` |

#### Syntax

```
/sdlc:harvest-decisions [--cycles <id>,<id>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `--cycles <id>,<id>` | read those completed cycles alone (D-166) |

#### Output

**What a decision is** (D-189): a choice that had a real alternative, that future cycles will meet again, whose
reason is not in the code, and that steers without forbidding — departing from it is declared, not blocked. **Who
wrote a passage decides how much it takes**: a person's own words — an intent's fixed decision, an abandonment's
reason, a reopen's reason, a declined deviation's note — qualify once; a plan's shape qualifies only when the same
choice is in two or more cycles; a departure from a standing decision is proposed as a supersede. A fixed decision
marked unconfirmed is an assumption and is never offered, and from an abandoned cycle only its reason is. Each
proposal is a `DEC_` entry with `Decided`, `Because`, `Instead of` and `From`.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| `decisions.draft.md` still beside its file at `sdlc harvest record` | records nothing | rename it into place or delete it, then record |

#### Example

```
/sdlc:harvest-decisions
```

#### Remarks

**Captured forward**, because a choice that never collided with a rule produced no finding to count. **`Instead of`
is the load-bearing clause**: an entry whose alternatives were never real records something nobody chose. Where a
choice forbids something, that half is a rule — two artifacts of one choice. **A declined suggestion is a decline,
not a decision**: it stays in its sitting. A reversed decision takes a new id and the old one keeps its place.

#### See also

- [`harvest-constraints`](#harvest-constraints), [`harvest-suggestions`](#harvest-suggestions)

---

### `harvest-suggestions`

Answers every open suggestion of the completed cycles — taken further, declined, or ignored — and lists the gaps the
framework should hear about. Proposes and never places.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | none — the third aspect `harvest` runs |
| Started by | `harvest`, or a person who asks to turn what readers noticed into new work |
| Reads | `sdlc harvest suggestions`: each open suggestion as `<cycle>/<id>`, the concerns ignored before, and every successor marked `framework:` |
| Writes | its proposal file under `.sdlc/reports/`. The successors by `sdlc draft harvest`, the ledger by `sdlc harvest record` |

#### Syntax

```
/sdlc:harvest-suggestions [--cycles <id>,<id>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `--cycles <id>,<id>` | read those completed cycles alone (D-166) |

#### Output

Each suggestion is **taken further**, as a successor under `## Successors` with its text copied in; **declined**,
under `## Declined` with the reason a person would give; or **ignored**, under `## Ignored` as its concern and the
cycles it was ignored in (D-153), with a `Why:` line under a new one (D-194). A concern ignored in three cycles is at the bar and is not ignored again. An
accepted successor is written to `.sdlc/successors/<slug>.md`, ready once its cycle has merged.

#### Refusals

None of its own: it writes no protected file.

#### Example

```
/sdlc:harvest-suggestions
```

#### Remarks

**Decline is not a polite ignore**, and it is not a decision either: it stays in the sitting and never reaches
the decision record. **An ignored suggestion is counted by its concern, not its id**, so its gist is worded exactly
as the report's *ignored before* line when it is one.

#### See also

- [`suggestions`](#suggestions), [`next`](#next)

---

### `harvest-stack`

Proposes the rows `techstack.md` should set for what this release ships and the declared stack never mentions.
Proposes and never places.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | none — the fourth aspect `harvest` runs |
| Started by | `harvest` |
| Reads | `sdlc harvest stack`, and the completed cycles' plans |
| Writes | its proposal file under `.sdlc/reports/`. The draft by `sdlc draft harvest`, the ledger by `sdlc harvest record`; **never** `techstack.md` |

#### Syntax

```
/sdlc:harvest-stack [--cycles <id>,<id>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `--cycles <id>,<id>` | read those completed cycles alone (D-166) |

#### Output

A `set <concern>` proposal for each row or rule the declared stack never mentions — the choice the cycles made, or
`none` — with what in the record asks for it, or a line under `## Not proposed`. **There is no remove**: a concern no
longer used is set to `none`.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| `techstack.draft.md` still beside its file at `sdlc harvest record` | records nothing | rename it into place or delete it, then record |

#### Example

```
/sdlc:harvest-stack
```

#### Remarks

A row that says `none` is an answer; **no row is a question nobody was asked** (`CON_15` judges a plan against the
declared stack).

#### See also

- [Declaring the stack](../docs/guides/declaring-the-stack.md)

---

### `harvest-areas`

Proposes the areas to add, rename, merge, remove, re-glob or tag, from what the completed cycles' plans named.
Proposes and never places.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | none — the fifth aspect `harvest` runs |
| Started by | `harvest` |
| Reads | `sdlc harvest areas`: names plans proposed, how often each area matched, changed files in no area |
| Writes | its proposal file under `.sdlc/reports/`. The draft by `sdlc draft harvest`, the ledger by `sdlc harvest record`; **never** `areas.md` |

#### Syntax

```
/sdlc:harvest-areas [--cycles <id>,<id>]
```

#### Parameters

| Parameter | Description |
|---|---|
| `--cycles <id>,<id>` | read those completed cycles alone (D-166) |

#### Output

`add`, `rename`, `merge`, `remove`, `files` and `tag` proposals (D-150). An area is added only once three cycles'
plans name it; one every plan touches hides nothing; changed files in no area are where a new one belongs. The draft
carries a renamed or removed area onto every entry naming it.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| a `tag` naming an area nobody defines | the drafter refuses it | add the area in the same file, or drop the tag |
| `areas.draft.md` still beside its file at `sdlc harvest record` | records nothing | rename it into place or delete it, then record |

#### Example

```
/sdlc:harvest-areas
```

#### Remarks

An area may be removed, unlike a rule or a decision: nothing cites an area's name, and its entries become global.

#### See also

- [`harvest`](#harvest)

---

### `close-cycle`

Closes the completed cycles every aspect has answered: commits their folders' removal on a branch of its own, pushes
it, and opens the pull request a person merges.

| | |
|---|---|
| Kind | single — one job, and it starts no other skill |
| Stage | none — last, once `harvest` has answered every aspect |
| Started by | `harvest`, or a person who asks to clear out old cycles |
| Reads | `sdlc harvest close`, which reads the ledger and the files |
| Writes | the `harvest/<date>` branch `sdlc harvest close` commits, pushed, and its pull request |

#### Syntax

```
/sdlc:close-cycle
```

#### Parameters

None.

#### Output

`sdlc harvest close` records each closed cycle in `.sdlc/harvested.json` — its merge commit and what later harvests
read from it — and on a new `harvest/<date>` branch commits the removal of their folders with the ledger and the
records the person placed. The skill pushes that branch and opens its pull request into the integration branch.
**The check admits it as a harvest**: it carries only the records, the ledger, successors and the removed folders.

#### Refusals

| Condition | Result | What to do |
|---|---|---|
| a cycle with an aspect unanswered, and nothing that can close | names each cycle and what it still needs | run that aspect's harvest skill |
| a draft still beside its file | closes nothing | rename it into place or delete it |
| not on the integration branch | closes nothing | switch to it, where the drafts were placed |
| nowhere to push | stops before pushing | set up a remote |

#### Example

```
/sdlc:close-cycle
```

#### Remarks

**A cycle closes only once there is nothing left it can give** (D-192): every aspect answered for it, every draft
placed or refused. The binary decides that, so no skill judges it, and an aspect run on its own never closes a
cycle. Nothing reaches the integration branch unreviewed: the removal arrives by pull request, and every folder is
still in git at the merge the ledger names. A cycle abandoned before any of it merged was read from its own branch:
its folder never reaches the integration branch, the ledger names that branch's last commit, and `sdlc tidy` lists the
branch for deletion once the pull request merges (D-190).

#### See also

- [`harvest`](#harvest), [`pr`](#pr)

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
