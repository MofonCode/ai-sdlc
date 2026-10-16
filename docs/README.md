# The components

Every concept the framework has a name for, defined once: what it is, what uses it, and where it lives.

**This is a dictionary, not a walkthrough and not a rule set.** It says what a thing *is*; the guides say
what to *do* and in what order, the cheat sheets give the line, and `sdlc constraints` renders the rules. Where something has a fuller
treatment elsewhere, the entry points at it rather than repeating it.

| | |
|---|---|
| [`guides/README.md`](guides/README.md) | **the guides** — six, one task each: installing, adopting the framework, declaring the stack, running a cycle, handling blockers, handling refusals |
| [`cheat-sheets/README.md`](cheat-sheets/README.md) | **the cheat sheets** — every command, every skill and every refusal, one line each |
| [`samples/README.md`](samples/README.md) | **the samples** — one solution walked end to end, every command and every answer |
| [`../skills/README.md`](../skills/README.md) | **the skills as a reference** — syntax, parameters, output, refusals and remarks for each of the eighteen |
| [`../CONCEPT.md`](../CONCEPT.md) | the design, and every decision behind it with what each one rejected |
| `sdlc constraints` | the rules in force, all tiers resolved. A copy here would be a second place they live |
| `sdlc help` | every command the binary answers to, with its options |
| [`../src/README.md`](../src/README.md) | what the code is and where a new thing goes |

**Counts in this document are stated where a list stands beside them and nowhere else.** A number in
prose with no list under it is the thing that goes stale first.

---

## 1. The two halves

Both have to be present and they fail differently. `guides/installing-the-plugin.md` is the whole of installing them.

| | What it is | Where |
|---|---|---|
| **the plugin** | the eighteen skills, and on Claude Code two hooks. Without it nothing asks for a cycle | installed per machine, loaded by a host at session start |
| **the `sdlc` binary** | the gate: every deterministic check, every renderer, the constraint library. Without it every skill's first command fails | a .NET global tool named `sdlc` |
| **the Agent Plugins manifest** | what Copilot, VS Code, Cursor, Codex and Kiro read | `plugin.json` at the repository root |
| **the Claude Code manifest** | what Claude Code reads. Both point at the same `skills/`, so there is no second copy | `.claude-plugin/plugin.json` |
| **the marketplace catalogue** | what a `plugin marketplace add` resolves | `.claude-plugin/marketplace.json` |

**Three versions, and only two of them are meant to match.** `sdlc version` prints all three.

| | What it tracks |
|---|---|
| the release version | the binary and the plugin. Set once in `Directory.Build.props`; the suite asserts it matches `plugin.json` |
| the artifact format version | the file envelope, written into every artifact as `framework_version`. Stays put across releases that change no format |

**See also:** [Installing the plugin](guides/installing-the-plugin.md)

## 2. The solution

A solution is one governed repository. Everything below lives in it.

| | What it is | Where |
|---|---|---|
| **solution** | the repository the framework governs, found by walking up for `.sdlc/` | — |
| **`.sdlc/`** | everything the framework keeps about this solution | the repository root |
| **`solution.md`** | what this solution is, in four keys — five with Operate's — and a paragraph. **A person's file** | `.sdlc/solution.md` |
| **`techstack.md`** / the declared stack | what the solution is built from, as a table of concerns and choices; which stack rules it is held to, as a table of rule ids marked `true` or `false`; and which build settings files it takes, as a table of file names marked the same way. **What is in it is exactly what binds**, and an empty one pins nothing. The second rule source a plan is read against. Adoption does not write it: a person places it from a draft, and no cycle opens until they have. **A person's file** | `.sdlc/techstack.md` |
| **stack default** | one row of it: a concern, and what this solution chooses for it | — |
| **stack rule row** | one row of its second table: a `PRO_` id, `true` or `false`, and what the rule holds a plan to. `true` binds; `false`, or no row, does not | — |
| **build settings row** | one row of its third table, under `## Build settings`: a file name, `true` or `false`, and what the file does. `true` declares that the solution takes the file; `false`, or no row, that it does not. **A row writes nothing** — a cycle brings the file | — |
| **the gap** | the build settings files the declared stack takes that are not on disk. `sdlc stack` names it and nothing closes it but a cycle whose approved plan declares the file | — |
| **stack profile** | a named set of default rows. Two exist: `dotnet-default`, which a first stack draft is written from, and `ml-tensorflow`, which only the options menu draws on. **No solution records one** — a profile is where a draft starts, not something that governs afterwards | in the binary |
| **the options menu** | every concern the shipped profiles know about with what each declares for it, the alternatives the framework has argued, the ones it refused, every stack rule it ships, every build settings file with what it does and what it costs a codebase that predates it, and an empty table for a solution starting over. Written by `sdlc draft techstack` as a report, read by nothing, and **never renamed into place** | `.sdlc/reports/` |
| **`.sdlc/README.md`** | the whole flow on one page, inside the solution, so it travels with the code | written by adoption |
| **`AGENTS.md`** | how to work in this repository, written for an agent | the repository root |
| **the build settings** | the `.NET` conventions a build enforces: `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `tests/Directory.Build.props`. Each is a row in the declared stack. Written by `sdlc scaffold` inside a cycle — all five with the solution skeleton, or on an adopted solution the ones its approved plan declares — and never by adoption. Created once, never rewritten, a person's to amend | the repository root |
| **reports** | a copy of what `suggestions`, `propose` and `draft techstack` printed, because a person works through one rather than reading it once. **Nothing ever reads one back**, and the directory ignores itself — so a copy left behind never reaches a later cycle's diff as a file no plan declared | `.sdlc/reports/` |

### The keys in `solution.md`

| Key | Values | What it decides |
|---|---|---|
| `origin` | `scaffolded`, `adopted` | where the code came from. **A historical fact that never changes** |
| `standard` | `profile`, `existing` | whether new work follows the profile's patterns or the surrounding code. The one key a person may move: `sdlc draft solution --standard` writes the change to rename into place |
| `platform` | `github`, `azure-devops`, `local` | whether a pipeline is written, and so whether a check governs a merge |
| `integration_branch` | a branch name; `development` when absent | where approved plans wait and built cycles land. The factory builds from it, and a cycle's diff is measured from where it diverged from it — or from the default branch while it does not exist |
| `telemetry` | `appinsights:<application id>`; absent for most | where Operate reads production's errors from, to file them as leads on the platform's tracker. Optional, and written only when set |

**There is no `profile` key.** A solution adopted before the declared stack listed its own rules may still carry one, and nothing reads it: the rules come from the declared stack alone.

### Adoption

| | What it is |
|---|---|
| **adoption** | `sdlc init`: writing what governs code into a repository, and committing it. Denied to an agent |
| **greenfield** | the mode for a repository holding no code. Gets `origin: scaffolded`; the build settings arrive with the skeleton |
| **onboard** | the mode for existing code with no `.sdlc/`. Gets `origin: adopted` and `standard: existing`, and no build settings: their rows start `false` in the first stack draft |
| **update** | the mode when `.sdlc/` is already there. Refreshes what the framework owns and never a person's file |
| **the next step** | what adoption's report and `sdlc doctor` both end with until the first cycle opens: what a person still has to place, in one sentence, and the one command that is next — the paragraph in `solution.md`, then the declared stack. Read from disk every time, because a rename runs nothing |
| **the solution skeleton** | `sdlc scaffold` on a solution that held no code: a solution file, six projects, the architecture suite, and the build settings they compile under. Refuses unless the cycle is approved, so **no code arrives without a chain behind it**. An adopted solution never gets it |
| **the reference slice** | one worked operation in the skeleton, written to be copied, because an agent takes the shape of the code beside it more reliably than an instruction |
| **the response envelope** | the one shape every response leaves through. An architecture test fails the build on an endpoint returning anything else |

**See also:** [Adopting the framework](guides/adopting-the-framework.md) · [Declaring the stack](guides/declaring-the-stack.md)

## 3. The cycle

One governed change, from why it exists to a pull request.

| | What it is | Where |
|---|---|---|
| **cycle** | one unit of governed work: an id, a directory, a branch, and the artifacts in it | `.sdlc/cycles/<id>/` |
| **cycle id** | `yyyyMMdd_HHmm_ss`, allocated by `sdlc new`. **Creating the directory is the collision check** | — |
| **cycle branch** | `cycle/<id>`. The turn-end hook and the chain check both read the cycle out of it, so a branch named anything else is not a cycle to either | git |
| **cycle index** | how the binary finds every cycle a solution holds | — |
| **stage** | one step a skill performs on a cycle: intent, spec, plan, build, verify, pr |
| **artifact** | one of the four governed documents. Each is challenged against the rules before it advances | `<kind>.md` |
| **abandonment** | the record that stops a cycle for good, with a reason. Terminal. **A person's file** | `abandoned.md` |

### The four artifact kinds

| | What it answers | Reads |
|---|---|---|
| **intent** | *why* the work exists. The only artifact written by interview | a person |
| **spec** | *what* must be true, as criteria that can each fail | `intent.md` and nothing else |
| **plan** | *how*, and *what proves each criterion* | the spec and the codebase |
| **verify** | whether the build met the spec | the recorded evidence, never the tests or the source |

### The artifact envelope

| | What it is |
|---|---|
| **frontmatter** | the keys above the document: `framework_version`, `cycle`, and one body key per kind |
| **`type`** (intent) | the change type: `new solution`, `feature addition`, `feature change`, `bug fix`. A bug fix's spec derives criteria from a reproduction rather than a desired outcome |
| **`tier`** (plan) | blast radius, most to least reversible: `local`, `contract`, `data` |
| **`outcome`** (verify) | `pass`, `blocked`, `escalated` |
| **the `## Open` region** | where a question nobody can answer yet is recorded. **Required even when empty**, so "nothing is open" can be told from "the author forgot" |
| **fixed decisions** | a choice a person had already made, recorded with their name on it rather than argued with |
| **artifact schema** | the shape check: which keys, which values, which sections |
| **schema violation** | one thing wrong with that shape — a key, a section, or the document |
| **content hash** | an artifact's bytes, normalised for what a platform may change. What makes a stale judgement detectable |

**See also:** [Running a cycle](guides/running-a-cycle.md) · [Commands](cheat-sheets/commands.md)

## 4. The rules, and the two records beside them

**Three records are rendered on demand and never copied into an artifact**, because a copy is a second
place the truth lives. Only the first of them binds.

| | What it is | Where |
|---|---|---|
| **constraint** | one rule, in four clauses. Stops a cycle doing something | rendered by `sdlc constraints` |
| **constraint id** | `CON_nn` global, `PRO_nn` stack rule, `LOC_nn` solution — resolved in that order, so a solution's rules are the last word |
| **revision** | a rule rewritten keeps its number and gains one: `CON_07.2`. The one it replaced stays on the page so a cycle that cited it is still readable |
| **the four clauses** | the statement, what it applies to and when, what it **forbids**, and why. `CON_11` forbids a rule with no Forbids clause |
| **the shipped library** | the global set, embedded in the binary. `SDLC_CONSTRAINTS` points at another and refuses rather than falling back if it is not there |
| **stack rules** | the `PRO_` set, embedded in the binary, of which a solution is held to exactly the ones its declared stack marks `true`. **The one tier where a rule may name a language, framework or package** — the global set may not, which is what the tier is for. The framework ships three |
| **the three admission tests** | what a proposed rule must pass: it forbids something, **no analyzer could decide it**, and it is incompatible with at least one plausible implementation |
| **decision** | a choice this solution already settled, so a cycle reads what was argued rather than arguing it again. Does not bind — `CON_19` only asks a plan departing from one to say what it would have cost. **A person's file** | `.sdlc/decisions.md` |
| **theme** | the roles this solution's surfaces use and the values that fill them — sixteen slots, the one place a colour or a typeface lives. Binds nothing: a departure is a linter's job | `.sdlc/theme.json` |

There are **17 global constraints in force**; `CON_04` and `CON_13` are superseded by `CON_16` and
`CON_15` and kept on the page. `sdlc constraints` is the authority, and it says which.

**See also:** [`constraints`](../skills/README.md#constraints) · [`propose`](../skills/README.md#propose) · [Declaring the stack](guides/declaring-the-stack.md) · [`CONSTRAINTS.md`](../CONSTRAINTS.md)

## 5. Interrogation

How an artifact is challenged. Rounds accumulate and are never rewritten.

| | What it is | Where |
|---|---|---|
| **interrogation** | judging one artifact against the rules in force and writing a findings round. **Never edits** | `/sdlc:interrogate` |
| **fresh process** | the interrogation runs with no memory of writing the thing it is judging. An author reviewing their own draft agrees with it |
| **findings round** | one pass's findings, plus the hash of the artifact as that reader saw it | `findings/<kind>.rN.json` |
| **finding** | one thing wrong: an id kept across rounds, the rule it **cites**, what is wrong, where, and the remedy |
| **severity** | `blocking` stops the artifact advancing and cites exactly one rule; `suggestion` does not block. **Non-blocking is not trivial** — it means nothing forbade the current state |
| **disposition** | `open`, `answered`, `withdrawn`, with who moved it and when. A finding is always shown at the disposition that stands |
| **refinement** | applying a round's findings to that artifact. **Edits one document and never judges** | `/sdlc:refine` |
| **oscillation** | a rule answered and then raised again. The panel's `Loop` line reports it — **going backwards, not going slowly.** There is no round limit |
| **conflict** | two rules no version of one artifact can satisfy. Reported as a conflict rather than resolved by picking a side |
| **stale round** | a round that judged a document which has moved since. Detected by the hash it recorded |
| **noticed** | something a build saw, left alone, and wrote down, because creep inside an approved file reaches review dressed as approved work |

**See also:** [`interrogate`](../skills/README.md#interrogate) · [`refine`](../skills/README.md#refine) · [Handling blockers](guides/handling-blockers.md)

## 6. The gate

One gate, covering all three planning documents, immediately before the build.

| | What it is | Where |
|---|---|---|
| **the approval ledger** | the record that a person approved this cycle, with a hash of each of the three documents. **A person's file, and no tool writes it** | `approval.md` |
| **gate decision** | `approved`, `changes-requested`, `denied`, `reopened`. A refusal is drafted on the same terms as an approval and is rejected if it says nothing about why |
| **the draft-and-rename pattern** | a drafter writes a complete file *beside* the real one and prints the rename. **Renaming is the decision**; a draft nobody renamed records nothing |
| **draft file names** | `approval.<decision>.draft.md`, `solution.draft.md`, `techstack.draft.md`. The decision is in the name, so a denial and an approval cannot look identical at the moment of the move. The options menu `draft techstack` writes alongside the last of these is **not** a draft and is not here: it is a report, nothing renames it, and a declared stack carrying it is something `sdlc doctor` reports |
| **the gate** | `sdlc gate <id>`: whether the build may run. Exits 1 if not |
| **the six files only a person writes** | `approval.md`, `abandoned.md`, `constraints.md`, `decisions.md`, `solution.md`, `techstack.md`. Every agent write to one is denied, by path and by command alike |

**Eleven things refuse at the gate**, and `guides/handling-refusals.md` is where each is looked up: no
cycle · abandoned · a planning document missing · not approved · an unreadable ledger · a standing
refusal · an unreadable round · never interrogated · a hash mismatch · an open blocker · a stale round.

**A blocking finding cannot be waived by any mechanism.** There is no field for it and no refusal code
that acceptance would clear. It is answered, or the cycle does not advance — `guides/handling-blockers.md`
is how.

**A blocker and a refusal are different things.** A **blocker** is something found wrong: a blocking
finding on a planning document, or a `blocked` verification. A **refusal** is a command or hook
declining to go on, exit code 1. An open blocker causes refusals; it is not one.

**See also:** [`draft`](../skills/README.md#draft) · [Handling refusals](guides/handling-refusals.md)

## 7. Evidence and verification

| | What it is | Where |
|---|---|---|
| **evidence** | what a build produced, recorded so a verification can adjudicate it rather than re-run it | `evidence/` |
| **`base.sha`** | the commit the diff is measured from: where the branch diverged from the default branch |
| **`diff.patch`** | the change. Untracked paths are staged before diffing, because a new file is part of the change and `git diff` cannot see one |
| **`tests.json`** | one normalised test summary. **The only test record a verification reads** |
| **`tests.trx`**, **`build.log`** | the runner's own output, kept as it was written |
| **test summary** / **test outcome** | what ran and what each test did. The build checks the suite **actually ran** rather than that the runner exited zero |
| **declared files** | what the plan said it would change, against what changed: **undelivered**, **undeclared**, and **governing** — a changed file holding a rule the change is judged by |
| **verification** | adjudicating the build against the spec from the evidence alone. Per criterion: which test did the plan name, did it run and pass, and **does that test actually establish the criterion?** | `verify.md` |
| **remediation** | applying a blocked verification's findings to source and tests. **Never a planning document** | `/sdlc:remediate` |
| **remediation budget** | how many passes have run and how many blockers stood after each. It stops when a pass undoes an earlier answer or two in a row reduce nothing — **no number of tries to run out of, and no ceiling** |
| **escalation** | what running out of budget makes of a blocked cycle. **Computed, never declared.** An escalated cycle never opens a pull request |

**See also:** [`build`](../skills/README.md#build) · [`verify`](../skills/README.md#verify) · [`remediate`](../skills/README.md#remediate) · [Handling blockers](guides/handling-blockers.md)

## 8. The pull request, and the guarantee

| | What it is | Where |
|---|---|---|
| **`pr-ready`** | whether a cycle may open a pull request, without opening one. Exits 1 if not | `sdlc pr-ready <id>` |
| **the four pull-request refusals** | not verified · the verification did not pass · escalated · the chain does not hold |
| **the pull request body** | the cycle rather than a summary somebody invented: the problem in the intent's own words, each criterion with the verification's verdict, the tier, the risk, and anything still open |
| **the chain check** | `sdlc verify-chain`: re-establishing from the repository that the chain is **complete, approved and hash-matched**. It runs every check rather than stopping at the first |
| **the pipeline** | where that check runs: `.github/workflows/sdlc-verify.yml`, or `azure-pipelines.yml`. **A solution recorded as `local` gets none** |
| **branch policy** | what makes the check a promise. Until a policy requires it the file is advisory, and the directory looks enforced without being enforced |
| **the claim graph** | which cycles' plans declare the same files, so which pull requests will likely conflict and the order to merge them in. Advice, read by the sitrep; it no longer schedules builds |
| **collision** | two cycles changing the same thing, met when the second one's pull request is merged | `/sdlc:resolve-conflict` |
| **the admission check** | the pull-request check on a pull request carrying one cycle's planning documents and nothing else: the plan, its approval and its hashes, no build and no verify. Chosen by what the pull request changes, never by its branch name | `sdlc verify-chain <id> --base <branch>` |

### The integration branch and the factory

| | What it is | Where |
|---|---|---|
| **the phases** | Define (intent, spec, plan, to the approval), Build (build, verify, pull request) and Operate (reading what runs, filing leads). Labels over the stages, never commands | `CONCEPT.md` §3.3 |
| **the integration branch** | where approved plans wait and built cycles land, `development` unless `solution.md` names another. Every change reaches it by a pull request; main receives only what a person promotes, which is the team's process | `integration_branch` in `solution.md` |
| **a cycle's branches** | `cycle/<id>/plan`, `cycle/<id>/build`, `cycle/<id>/build-unattended`, `cycle/<id>/forward` — one per kind of work, each started from the right base | `sdlc branch <id> <kind>` |
| **the queue** | the cycles a build would take now: approved, no verify, not abandoned, and no `cycle/<id>/...` branch on the remote. A listing, never a state file; deleting a branch re-queues its cycle | `sdlc cycles --queued` |
| **the factory** | a pipeline that runs `batch-build --pr --unattended` against the integration branch every night. It finds, builds, verifies and opens pull requests, and **never merges** | `sdlc factory` |
| **a draft pull request** | what a cycle that did not pass gets: its attempted code, verify and evidence, marked not ready, so a person picks it up rather than the factory building it again | `/sdlc:pr <id> --draft` |
| **the sitrep** | the morning's page: what is ready to merge and in what order, what needs a person, what was held back and why, what is waiting and going stale | `sdlc sitrep` |
| **a lead** | anything a person can hand `intent` to begin a cycle: `azuredevops:1234`, `github:56`, `suggestion:<cycle>/<finding>`, or a path. Its outside text is untrusted, and the interview still happens | `sdlc lead show <reference>`, `/sdlc:intent --from <lead>` |

### The three layers, and what each stops

| Layer | What it is | Who can walk past it |
|---|---|---|
| advisory | skill and instruction text | the agent, constantly |
| local | the `sdlc` binary and the two agent hooks | anyone with a shell, and anyone whose host did not load the hooks or whose workspace is untrusted |
| **non-bypassable** | the chain check as a required pull-request check | nobody without repository admin |

**See also:** [`pr`](../skills/README.md#pr) · [Running a cycle](guides/running-a-cycle.md)

## 9. Enforcement

| | What it is |
|---|---|
| **the pre-tool hook** | judges a tool call before it runs and denies the seven things an agent may not do: the six protected files, and adoption |
| **hook policy** | the rules that judgement applies. Absolute rather than conditional — no mode, no flag, no check on which skill is running, **so a rule with no exception has nowhere for a bypass to live** |
| **tool kind** | `harmless`, `file-write`, `shell`. A write is judged by path; a shell command is judged by its text |
| **the command-text scan** | why a command that merely *names* a protected file is refused. The hook reads the command string, not the paths it would touch — so `git commit -F` with the message in a file is how a commit mentioning one gets through |
| **the turn-end hook** | asked before a turn ends. Sends the turn back when the recorded suite has a failure, when there are changes and no evidence, or when the tree has moved since the evidence. **Silent on a branch it cannot read as a cycle** |
| **hook host** | `claude`, `copilot`, `antigravity`. A name the binary does not know is refused rather than guessed |
| **hook event** | `pre-tool-use` and `turn-end`, each accepted under the host's own name too. Antigravity's turn end is deliberately not governed |
| **hook decision** | `allow` or `deny` |
| **config ownership** | whether the framework owns a host's hook file or merges two entries into one it shares. `.claude/settings.json` is shared; `.github/hooks/sdlc.json` is owned |
| **adoption writes every host's configuration** | a file is inert on a machine without that host, and a teammate opening the repository in a different agent is governed without adopting again |
| **exit codes** | `0` did what was asked · `1` **a rule refused it** · `2` the command line was not understood · `3` **the gate itself failed** |
| **refusal versus failure** | a refusal is the gate working and a failure is the gate broken. Different exit codes on purpose, so a hook never reports its own bug as a governed stop |

**See also:** [Handling refusals](guides/handling-refusals.md) · [Refusals](cheat-sheets/refusals.md)

## 10. The skills

**Eighteen skills, and the split that matters: a command is deterministic and the binary owns it; a
skill needs language and an agent runs it.** The binary never writes a planning document and a skill
never decides a gate.

| Skill | In one line |
|---|---|
| `init` | interviews the team about what matters here and proposes the first `LOC_` rules. Does not adopt — it hands over the command |
| `intent` | interviews a person into the intent, and opens the cycle |
| `spec` | derives testable criteria from the intent alone |
| `plan` | decides how, and what proves each criterion. The last document before a person approves |
| `interrogate` | judges one artifact against the rules, in a fresh process. Never edits |
| `refine` | applies a round's findings to that artifact. Never judges |
| `steer` | carries a mid-cycle correction down the chain, editing rather than regenerating |
| `build` | implements an approved plan, builds, tests, and records the evidence |
| `batch-build` | completes every queued cycle, each on its own branch and independently, and reports the sitrep; `--pr` opens a pull request for each, a draft for one that did not pass |
| `verify` | adjudicates a build against its spec from the recorded evidence alone |
| `remediate` | applies a blocked verification's findings to source and tests |
| `pr` | pushes a cycle's branch and opens its pull request into the integration branch — a verified build, an approved plan with `--plan`, a draft with `--draft`. **The only stage that touches the remote** |
| `resolve-conflict` | brings a cycle's pull request up to date with the integration branch when it conflicts or goes red, inside its declared files, then verifies again |
| `draft` | prints what belongs in a file only a person writes |
| `propose` | reads the record and argues for an entry — a rule, a decision, a theme role |
| `constraints` | shows the rules in force, all tiers resolved |
| `status` | shows where a cycle has got to, and the next command |
| `suggestions` | lists the open non-blocking findings a solution has accumulated |

**For what each one refuses, who starts it, and what it reads and writes, go to
[`../skills/README.md`](../skills/README.md);** [`cheat-sheets/skills.md`](cheat-sheets/skills.md) adds what
each takes. That file is the skills' own surface and this is a
summary of it.

## 11. Patterns worth naming

These are not files. They are the shapes that recur, and knowing them explains most of the rest.

| | What it is |
|---|---|
| **draft, propose, suggestions** | three verbs split by who made the decision. **Draft** hands back a finished file for a decision you have already taken. **Propose** reads the record and argues for one you have not — a tool may propose a rule when it brings the evidence, and may not shape a rule somebody just typed. **Suggestions** forms no opinion: it lists what a reader raised and nobody acted on |
| **rendered, never copied** | the constraints, the decisions and the theme reach a session by being rendered, because a copy inside an artifact is a second place the truth lives |
| **derived, not declared** | `escalated` is what a remediation loop that stopped making progress makes of a cycle, not something anything writes |
| **the task list** | the host's own list of steps, which six skills open where a host has one. `init`, `intent`, `spec`, `plan` and `build` list one stage's steps and nothing past its hand-back; `batch-build` lists one line per cycle, ticked only when that cycle passed. A view of one session, never the record: `sdlc doctor`, `sdlc status` and `sdlc cycles --pr-ready` say what is done |
| **the human-in-the-loop hand-back** | a skill stops rather than chaining into the next stage. Chaining on would remove the decision point while appearing helpful |
| **`elsewhere`** | what a binary cannot answer and does not pretend to: whether a host loaded the skills, whether the pipeline's check is required. `doctor` marks these and names who to ask |
| **the admission question for a proof** | *what would have to be true for this to pass while the criterion is still unmet?* If that has an answer, the proof is inadequate — and **an inadequate proof is worse than none**, because it produces a green verification instead of an obvious gap |

## See also

- [The guides](guides/README.md) — what to do, and in what order
- [The cheat sheets](cheat-sheets/README.md) — one line per thing
- [The skills reference](../skills/README.md) — each skill in full
- [`CONCEPT.md`](../CONCEPT.md) — the design, and every decision behind it
