# The components

Every concept the framework has a name for, defined once: what it is, what uses it, and where it lives.

**This is a dictionary, not a walkthrough and not a rule set.** It says what a thing *is*; the guides say
what to *do* and in what order, the mechanism pages say how one works and why, the cheat sheet is the cycle on one page, the reference gives the line, and `sdlc constraints` renders the rules. Where something has a fuller
treatment elsewhere, the entry points at it rather than repeating it.

| | |
|---|---|
| [`guides/README.md`](guides/README.md) | **the guides** — one task each, from installing to handling a refusal. The index lists them |
| [`mechanisms/README.md`](mechanisms/README.md) | **the mechanisms** — how one works and why, one page each. The index lists them |
| [`reference/README.md`](reference/README.md) | **the reference** — every command, every skill and every refusal, one line each |
| [`cheat-sheet.md`](cheat-sheet.md) | **the cheat sheet** — one cycle, one line per step |
| [`samples/README.md`](samples/README.md) | **the samples** — one solution walked end to end, every command and every answer |
| [`../skills/README.md`](../skills/README.md) | **the skills as a reference** — syntax, parameters, output, refusals and remarks for each of the thirty |
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
| **the plugin** | the thirty skills, and on Claude Code three hooks. Without it nothing asks for a cycle | installed per machine, and packed inside every release of the binary, which `sdlc claude` loads instead |
| **the `sdlc` binary** | every deterministic check — the approval gate among them — every hook decision, every renderer, the constraint library. Without it every skill's first command fails | a .NET global tool named `sdlc`, from nuget.org |
| **the pinned release** | the release of the binary a solution runs, named in its local tool manifest. The global `sdlc` hands every command on to it, and a cycle's branch carries the pin it was cut with, so a cycle runs on the release it opened on (D-131) | `.config/dotnet-tools.json`, written by adoption |
| **a local build** / a test run | a build of the binary nobody published. Its release ends in `-local`: a build run through `SDLC_DEV_BUILD`, which every `sdlc` started from that terminal hands to instead of the pin, says so when it runs, and a pack made outside a pipeline is stamped so. A cycle any of whose rounds one wrote is a **test run**, and never opens a pull request (D-135) | `SDLC_DEV_BUILD`; [Testing a local build](guides/testing-a-local-build.md) |
| **the Agent Plugins manifest** | what Copilot, VS Code, Cursor, Codex and Kiro read | `plugin.json` at the repository root |
| **the Claude Code manifest** | what Claude Code reads. Both point at the same `skills/`, so there is no second copy | `.claude-plugin/plugin.json` |
| **the marketplace catalogue** | what a `plugin marketplace add` resolves | `.claude-plugin/marketplace.json` |

**One release version** covers the binary and the plugin together. It is set once in `Directory.Build.props`, the suite
asserts it matches `plugin.json`, and `sdlc version` prints both, so a mismatch is visible.

**See also:** [Installing the plugin](guides/installing-the-plugin.md)

## 2. The solution

A solution is one governed repository. Everything below lives in it.

| | What it is | Where |
|---|---|---|
| **solution** | the repository the framework governs, found by walking up for `.sdlc/` | — |
| **`.sdlc/`** | everything the framework keeps about this solution | the repository root |
| **`solution.md`** | what this solution is, in six keys — seven with Operate's — and a paragraph. **A person's file** | `.sdlc/solution.md` |
| **a setting** | a value in `solution.md`, read by the binary, defaulted when absent. Not a *build setting*, which is a file the declared stack takes. `sdlc settings` shows each, whether it was written or defaulted, and how to change it | `.sdlc/solution.md` |
| **`techstack.md`** / the declared stack / the stack definition | what the solution is built from, as a table of concerns and choices; which stack rules it is held to, as a table of rule ids marked `true` or `false`; and which build settings files it takes, as a table of file names marked the same way. **What is in it is exactly what binds**, and an empty one pins nothing. The second rule source a plan is read against. Adoption drafts it and places nothing: a person places it from the draft, and no cycle opens until they have. **A person's file** | `.sdlc/techstack.md` |
| **stack default** | one row of it: a concern, and what this solution chooses for it | — |
| **stack rule row** | one row of its second table: a `PRO_` id, `true` or `false`, and what the rule holds a plan to. `true` binds; `false`, or no row, does not | — |
| **build settings row** | one row of its third table, under `## Build settings`: a file name, `true` or `false`, and what the file does. `true` declares that the solution takes the file; `false`, or no row, that it does not. **A row writes nothing** — a cycle brings the file | — |
| **the gap** | the build settings files the declared stack takes that are not on disk. `sdlc stack` names it and nothing closes it but a cycle whose approved plan declares the file | — |
| **stack profile** | a named set of default rows. Two exist: `dotnet-default`, which a first stack draft is written from, and `ml-tensorflow`, which only the options menu draws on. **No solution records one** — a profile is where a draft starts, not something that governs afterwards | in the binary |
| **the options menu** | every concern the shipped profiles know about with what each declares for it, the alternatives the framework has argued, the ones it refused, every stack rule it ships, every build settings file with what it does and what it costs a codebase that predates it, and an empty table for a solution starting over. Written by `sdlc draft techstack` as a report, read by nothing, and **never renamed into place** | `.sdlc/reports/` |
| **`.sdlc/README.md`** | the whole flow on one page, inside the solution, so it travels with the code | written by adoption |
| **`AGENTS.md`** | how to work in this repository, written for an agent | the repository root |
| **the build settings** | the `.NET` conventions a build enforces: `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `tests/Directory.Build.props`. Each is a row in the declared stack. Written by `sdlc scaffold` inside a cycle — all five with the solution skeleton, or on an adopted solution the ones its approved plan declares — and never by adoption. Created once, never rewritten, a person's to amend | the repository root |
| **reports** | a copy of what `suggestions`, `harvest <aspect>`, `report --run` and `draft techstack` printed, because a person works through one rather than reading it once. **Nothing ever reads one back**, and the directory ignores itself — so a copy left behind never reaches a later cycle's diff as a file no plan declared | `.sdlc/reports/` |

### The keys in `solution.md`

| Key | Values | What it decides |
|---|---|---|
| `origin` | `scaffolded`, `adopted` | where the code came from. **A historical fact that never changes** |
| `approval` | `required`, `none`; `required` when absent | whether a person approves each cycle's planning documents before its build. Under `none` every other check still holds, and every report says no person approved (D-137) |
| `standard` | `profile`, `existing` | whether new work follows the profile's patterns or the surrounding code. The one key a person may move: `sdlc draft solution --standard` writes the change to rename into place |
| `platform` | `github`, `azure-devops`, `local` | whether a pipeline is written, and so whether a check governs a merge |
| `integration_branch` | a branch name; `development` when absent | where approved plans wait and built cycles land. The factory builds from it, and a cycle's diff is measured from where it diverged from it — or from the default branch while it does not exist |
| `coverage_floor` | a whole percentage; `80%` when absent | the whole-codebase coverage at or above which a cycle need not hold the baseline. Under it, the codebase may not fall (D-157). A person lowers it to answer a fall no test can close, such as a change in how coverage is measured |
| `telemetry` | `appinsights:<application id>`; absent for most | where Operate reads production's errors from, to file them as leads on the platform's tracker. Optional, and written only when set |
| `build_tools` | bare command names, comma-separated; absent for most | the commands a build's own process may run beyond its own, each allowed as that command and nothing wider (D-202). Optional, and written only when set |

**There is no `profile` key.** A solution adopted before the declared stack listed its own rules may still carry one, and nothing reads it: the rules come from the declared stack alone.

### Adoption

| | What it is |
|---|---|
| **adoption** | `sdlc init`: writing what governs code into a repository, and committing it. Denied to an agent |
| **greenfield** | the mode for a repository holding no code. Gets `origin: scaffolded`; the build settings arrive with the skeleton |
| **onboard** | the mode for existing code with no `.sdlc/`. Gets `origin: adopted` and `standard: existing`, and no build settings: their rows start `false` in the first stack draft |
| **update** | the mode when `.sdlc/` is already there. Refreshes what the framework owns and never a person's file, and names what the declared stack does not mention. Run after every upgrade of the binary |
| **the release stamp** | the line in `.sdlc/README.md` saying which release last wrote the framework's files. `sdlc doctor` compares it with the binary, so an upgrade that reached the machine and not the repository is named |
| **not mentioned** | a default the profile declares, or a stack rule a release ships, with no row in the declared stack at all — never asked, as against declined. A row saying `none` or `false` is an answer; `sdlc stack` lists what is not mentioned |
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
| **the steps** | what a person does, as against the stages a skill runs: begin an intent, take it to plan, approve, *build it*, review the pull request. *Build it* runs implement, verify, remediate and pr in a process of its own; steering is a sentence at plan review, not a step (D-182) | [`cheat-sheet.md`](cheat-sheet.md) |
| **artifact** | one of the four governed documents. Each is challenged against the rules before it advances | `<kind>.md` |
| **abandonment** | the record that stops a cycle for good, with a reason. Terminal. **A person's file**. One beside a series' record stops every cycle that exists only in the series; an approved cycle already on the integration branch stays as it stands, to be built again | `abandoned.md` |
| **atomic cycle** | a cycle that can be built, verified and merged against the integration branch as it stands, without any other open cycle. The one rule a slicing is held to; there is no floor on how small (D-125) | — |
| **slicing**, **a slice** | `intent` proposing, before any cycle opens, how to cut a piece of work into atomic pieces — each one a **slice**. The person accepts the slicing once, merging pieces or marking one atomic; nothing is sliced unattended. Intents already written are cut again with `--reslice` while none has a spec (D-141). Not the *reference slice* in §2, which is code | `/sdlc:intent` |
| **a successor** | a slice that needs another's code, waiting as a file rather than a cycle. `succeeds:` lists what it waits for — cycle ids, or `successor:<slug>` for one not started yet — and its state, `waiting`, `ready`, `stuck` or `unresolved`, is worked out from the record on every read. A person starts a ready one with `/sdlc:intent --from successor:<slug>`. One marked `framework:` exists because of a gap in the framework, and a harvest lists it for a person to file there (D-173) | `.sdlc/successors/<slug>.md`, `sdlc successors` |
| **a sandbox** | the worktree a piece of work is planned in, on `sandbox/<id>` beside the repository, named for its first cycle. Every cycle the work turns into opens inside it, and only what is approved leaves it, composed onto an admission branch by `sdlc admit` (D-144) | `sdlc branch <id> sandbox`, `sdlc admit` |
| **a parked cycle** | a cycle whose plan needs a file only another unbuilt plan creates, found by `sdlc park` once its plan is clean. It keeps its id, spec and plan, waits in `parked.md` on the cycle that creates the file, is held out of the build queue, and comes back with `sdlc unpark` once that merged, when its spec and plan are read again (D-145) | `sdlc park`, `sdlc unpark` |
| **the backlog** | every successor and parked cycle, in the waves they come ready in. A view worked out on every read, never a file (D-145) | `sdlc backlog` |
| **a run**, **a run branch** | several cycles built one after another into one branch and one pull request, each keeping its own folder, rounds and verification: a **wave**, `wave/<id>`, built from the sandbox it was planned in, or a **series**. A predecessor is ready once its passing verify is committed to the run's branch. A run ends in one report, never one per cycle | `sdlc build --run`, `sdlc report --run` |
| **a wave** | every successor and parked cycle ready at once, worked out from what each waits on and never written into a successor. `/sdlc:wave` plans one with a person in one sandbox and builds it on `wave/<id>` into one pull request; `sdlc backlog` forecasts the waves still to come (D-205, D-206) | `/sdlc:wave`, `sdlc backlog` |
| **a series** | waves taken one after another into `series/<id>`, in a worktree of its own, until the number of waves a person set or an empty backlog. Under `approval: none` it plans and builds what is ready; under `approval: required` it builds only approved cycles. Its record, `.sdlc/series/<id>/series.json`, written by the binary, holds what each wave took. The factory runs only series | `sdlc series` |
| **the series record** | `.sdlc/series/<id>/series.json`: a series' wave limit, the approval setting it ran under, and what each wave took. Written only by the binary — `sdlc series start` and `sdlc series wave` — and denied to agents. Membership in a series lives here and in no cycle's documents; an abandonment placed beside it stops every cycle that exists only in the series, and a close removes the folder with the last of them (D-205, D-208) | `sdlc series status` |

### The four artifact kinds

| | What it answers | Reads |
|---|---|---|
| **intent** | *why* the work exists. Suggested from the record and confirmed by a person, line by line where the agent inferred it, or written by interview on request | a person |
| **spec** | *what* must be true, as criteria that can each fail | `intent.md` and nothing else |
| **plan** | *how*, and *what proves each criterion* | the spec and the codebase |
| **verify** | whether the build met the spec | the recorded evidence, never the tests or the source |

### The artifact envelope

| | What it is |
|---|---|
| **frontmatter** | the keys above the document: `cycle`, and one body key per kind |
| **`type`** (intent) | the change type: `new solution`, `feature addition`, `feature change`, `bug fix`. A bug fix's spec derives criteria from a reproduction rather than a desired outcome. Settled before anything is written, because it decides what the intent must say: from `/sdlc:intent --type bug\|add\|change`, from a lead, or — a new solution, which has no flag — detected in a scaffolded solution with no cycle yet approved (D-95) |
| **`(inferred)`** (intent) | the mark on a line of a suggested intent that is the agent's reading rather than the person's words or the record's. It comes off when the person confirms or corrects the line, or says *go* for the rest; one answered *not sure* is recorded as unconfirmed. The schema refuses an intent that still carries one (D-200), except one a series' record lists: a series asks nobody, so its marks stay and the run's report lists each as confirmed by nobody |
| **`tier`** (plan) | blast radius, most to least reversible: `local`, `contract`, `data` |
| **`outcome`** (verify) | `pass`, `blocked`, `escalated` |
| **the `## Open` region** | where a question nobody can answer yet is recorded. **Required even when empty**, so "nothing is open" can be told from "the author forgot" |
| **fixed decisions** | a choice a person had already made, recorded with their name on it rather than argued with |
| **artifact schema** | the shape check: which keys, which values, which sections |
| **schema violation** | one thing wrong with that shape — a key, a section, or the document |
| **content hash** | an artifact's bytes, normalised for what a platform may change. What makes a stale judgement detectable |

**See also:** [Running a cycle](guides/running-a-cycle.md) · [Cheat sheet](cheat-sheet.md) · [Commands](reference/commands.md)

## 4. The rules, and the two records beside them

**Three records are rendered on demand and never copied into an artifact**, because a copy is a second
place the truth lives. Only the first of them binds.

| | What it is | Where |
|---|---|---|
| **constraint** | one rule, in four clauses. Stops a cycle doing something | rendered by `sdlc constraints` |
| **constraint id** | `CON_nn` global, `PRO_nn` stack rule, `LOC_nn` solution — resolved in that order, so a solution's rules are the last word |
| **revision** | a rule reworded without changing what it forbids keeps its number and gains one: `CON_12.2`. The new wording replaces the old, and nothing else moves |
| **supersession** | a rule whose meaning changes is replaced by a new id, and the old one stays on the page marked `Superseded-by`, out of force, so a finding that cited it is still readable against the rule it was judged under |
| **the four clauses** | the statement, what it applies to and when, what it **forbids**, and why. `CON_11` forbids a rule with no Forbids clause |
| **judged per criterion** | an optional field on a rule — `Judged: per criterion` or `per outcome` — saying it is decided one item at a time. The rules reader answers it with a row per spec criterion or intent outcome, and an answer missing a row is refused, so every instance of a repeated gap surfaces in one round. `CON_03`, `CON_21`, `CON_22` and `CON_23` are judged per criterion, `CON_16` per outcome (D-119) |
| **the shipped library** | the global set, embedded in the binary. `SDLC_CONSTRAINTS` points at another and refuses rather than falling back if it is not there |
| **stack rules** | the `PRO_` set, embedded in the binary, of which a solution is held to exactly the ones its declared stack marks `true`. **The one tier where a rule may name a language, framework or package** — the global set may not, which is what the tier is for. The framework ships five |
| **the three admission tests** | what a proposed rule must pass: it forbids something, **no analyzer could decide it**, and it is incompatible with at least one plausible implementation |
| **decision** | a choice this solution already settled, so a cycle reads what was argued rather than arguing it again: one that had a real alternative, that future cycles will meet again, whose reason is not in the code, and that steers without forbidding. Harvested from completed cycles — a person's own words once, a plan's prose only when two or more cycles chose the same, a belief marked unconfirmed never; a declined suggestion is a decline, not a decision (D-189). A standing criterion every cycle inherits: every stage that writes reads it, and every reader that judges is handed it. Departing is legal; `CON_27` asks a departure to be declared where it arises. **A person's file** | `.sdlc/decisions.md` |
| **the decisions charter** | the whole of `decisions.md`: every decision the solution has settled, each with what it chose instead of, read by the intent, the spec, the plan and the build, and handed to every reader that judges them and the change, so nobody has to prompt for it. The name the README's features list uses | `.sdlc/decisions.md` |
| **deviation** | where a build or a remediation could not follow the approved text, and did something else on the record. It quotes the approved text it departs from — the plan, a planned proof, or a recorded decision, and never a spec criterion, because no deviation changes what *done* means (D-179) — names the blocker with its evidence, and says what was done instead. It replaces that text rather than excusing the blocker, and every verify reader judges against it. A stalled remediation may take one before it escalates (D-179). Nobody answers it before the pull request: it is in the report, and merging accepts it; a person who wants it otherwise says what instead, and their words are recorded on it. Never the intent, never wider (D-156, D-181) | `.sdlc/cycles/<id>/deviations.md`, `sdlc deviations <id>` |
| **departure** | a cycle setting a recorded decision aside, declared once as one line opening `Departs from DEC_nn` — an outcome in the intent's `Fixed decisions`, with who and why; an approach in the plan's `Shape and departures`. Later stages rely on it rather than reopen it. The binary reports it first in the approval draft, beside the gate in `sdlc status`, and in the pull request, and `harvest-decisions` offers it as a supersede once the cycle is completed (D-139) | `sdlc status <id>` |
| **area** | a name for a set of this solution's files, defined once in `areas.md` by globs. A rule or decision carrying an area reaches a reader in full only when the change touches those files; one carrying none reaches every reader. The intent and spec touch none; the plan's declared files decide from the plan on, the change's files on a verify. Named in plans' `## Areas` sections, and created once three cycles name one. Absent, nothing narrows | `.sdlc/areas.md`, `sdlc areas` |
| **headline** | an entry's own title, at most fifteen words, each clause at most sixty. Every reader is handed every headline and the body only of the entries in scope, so nobody is unaware an entry exists | `sdlc decisions show <id>`, `sdlc constraints show <id>` |
| **theme** | the roles this solution's surfaces use and the values that fill them — sixteen slots, the one place a colour or a typeface lives. Binds nothing: a departure is a linter's job | `.sdlc/theme.json` |
| **a completed cycle** | one whose pull request merged after a passing verify — a person accepted it — or one a person abandoned. The only kind a harvest reads; a cycle that passed verify and has not merged waits (D-190) | `sdlc harvest` |
| **a harvest** | taking back what the completed cycles settled, outside any cycle's rounds, usually on the integration branch once a pull request has merged, which the build's report suggests. One **aspect** at a time — constraints, decisions, suggestions, stack, areas — each read by its own skill, written as a proposal file of `add`, `supersede`, `reword` and `set` lines a person marks — **never remove** a rule or a decision; an area alone may be renamed, merged or removed — drafted with only the marked lines applied, reviewed by two fresh readers, and recorded as answered for those cycles. Binds the next cycle, never one in flight (D-191) | `/sdlc:harvest`, `sdlc harvest [<aspect>]`, `sdlc draft harvest <file>`, `sdlc harvest record <aspect> <file>` |
| **covered** | the fourth answer a harvest gives a suggestion, beside taken further, declined and ignored: a rule in force, a standing decision, a successor or a file already handles it. Written under `## Covered` naming that thing, which `sdlc harvest record` checks is real; its `Concern:` line takes a tallied concern out of the ignored and watch tallies. Not a decline: nobody decided against it (D-209) | `sdlc harvest record suggestions <file>` |
| **the harvest summary** | what one proposal file asks of a person, in a few lines: the proposals to mark, each answer counted by the `### <theme>` the sitting grouped it under, and every rule, decision and successor it names with its text. The grouping is the agent's, the counts and texts the binary's; every harvest step hands back through it (D-209) | `sdlc harvest summary <file>` |
| **close** | what happens to a completed cycle once every aspect has been answered for it and every draft placed or refused: the ledger keeps what later harvests need, and its folder's removal is committed on a `harvest/<date>` branch whose pull request a person merges, which the check admits as a harvest. The cycle is then a **husk**: everything in it is in git at the merge commit the ledger names (D-192) | `/sdlc:close-cycle`, `sdlc harvest close` |
| **the harvest ledger** | `.sdlc/harvested.json`: which aspects each completed cycle has answered, each closed cycle's merge commit, files, cited rules and proposed areas, each sitting's aspect, accepted and declined, the concerns watched below the bar, and the concerns ignored. Each closed cycle also keeps the releases its rounds ran on, what each reader raised, and what each document cost (D-204). Written only by the binary | `sdlc harvest --history` |
| **a note for the framework** | what a harvest sitting found about the framework rather than the solution: a rule, reader or step that cost more than it caught (`cost`), a rule any solution would need (`missing`), or something the framework did wrong (`defect`). Written under `## For the framework` for somebody who has never seen the solution, naming a **surface** — a rule's id, `perspective:<name>`, `stage:<name>`, `command:<name>` — with its evidence beneath, which stays in the solution. Kept in `.sdlc/framework-harvest.json`, written only by the binary; `sdlc harvest framework` prints what of it may be handed over, and sends nothing (D-204) | `sdlc harvest framework` |

There are **19 global constraints in force**; eight are superseded and kept on the page — `CON_04` by
`CON_16`, `CON_05` by `CON_26`, `CON_06` by `CON_21`, `CON_07.2` by `CON_25`, `CON_08` by `CON_22`, `CON_13` by
`CON_15`, `CON_19` by `CON_24`, and `CON_24` by `CON_27`. `sdlc constraints` is the authority, and it says which.

**See also:** [`constraints`](../skills/README.md#constraints) · [`harvest`](../skills/README.md#harvest) · [Declaring the stack](guides/declaring-the-stack.md) · [`CONSTRAINTS.md`](../CONSTRAINTS.md)

## 5. Interrogation

How an artifact is challenged. Rounds are appended to one ledger per artifact, and an approved round is never rewritten.

| | What it is | Where |
|---|---|---|
| **interrogation** | judging one artifact against the rules in force and writing a findings round. **Never edits** | `/sdlc:interrogate` |
| **the interrogator** | what runs an interrogation: the fresh process, or processes, that read one artifact against the rules in force and answer with findings. It holds no tools and has no memory of the document being written, and it never edits | `/sdlc:interrogate`, `sdlc round prompt` |
| **the diff reader** | the verify round's reader of the change itself, beside the rules reader and the perspectives: handed the intent, the spec, the plan, the decisions, the deviations, the evidence and `diff.patch`, and every rule that applies to a diff — `CON_25`, `CON_09`, `CON_11`, `CON_27`. It asks whether the change did anything it was not given leave to do, judging scope by purpose and never by the plan's file list. Its findings are the round's, so `remediate` works them (D-139) | `sdlc round prompt <id> verify --reader diff` |
| **fresh process** | an interrogation's readers, the verify adjudicator, a harvest's readers and the build itself run with no memory of writing or planning what they work on. An author reviewing their own draft agrees with it. **The binary starts every one** — `sdlc round run`, `sdlc harvest read` and `review`, `sdlc build` — with its tools named and permission checks on; no skill starts one itself (D-183). A reader is started with `SDLC_READER=1`, so the turn-end hook lets its turn end on a red build; the build is not a reader |
| **findings round** | one pass's findings, plus the hashes of what it judged and what against, written by `sdlc round` — which refuses one while anything above it is unsettled. It records only what it raised and what it moved | an entry in the findings ledger |
| **findings ledger** | every round of one artifact's interrogation in one file: each finding once with its moves, each round an entry chained by hash to the ones before. The approval anchors its latest round, so the argument behind an approval cannot be rewritten (D-107). Holds the blockers; a cycle's suggestions have a ledger of their own | `findings/<kind>.json` |
| **reader prompt** | what one fresh process is handed, written by the binary rather than composed in a session: the block every reader of the round shares first, the same bytes for each and written once as `shared.md`, which the host takes as a system prompt so it is cached (D-163), and that reader's own part last. The reader judges; the binary assembles and collects (D-117) | `sdlc round prompt`, `sdlc round collect` |
| **usage record** | what each recorded round's readers cost — tokens in, cached, out, turns and seconds per reader, by role and model. Outside the chain, committed, `null` where the host did not say, and no prices (D-121) | `findings/usage.json` |
| **suggestions ledger** | every suggestion a cycle's readers raised, against any of its documents, **each concern once**, with an `S_nn` id and a history of `raised`, `answered` and `withdrawn` events, one line per document and round. Not chained, because a suggestion gates nothing (D-115) | `findings/suggestions.json` |
| **suggestion status** | what became of a suggestion, in one word — `open`, `accepted`, `declined`, `answered`, `withdrawn` — and the cycle that decided it. **Worked out on every read, never stored**: from `taking in` and `declining` in an intent, a suggestion lead, and an abandonment's `declining` lines. An abandoned cycle accepts nothing | `sdlc suggestions --all` |
| **finding** | one thing wrong: an id kept across rounds, the rule it **cites**, what is wrong, where, and the remedy. A blocker also carries a **gist** — one plain sentence saying what set it off, which the panels show with where it was found as `Where:` and its remedy as `Fix:` |
| **severity** | `blocking` stops the artifact advancing and cites exactly one rule — one in force for that document, or it is recorded as a suggestion (D-174); `suggestion` does not block. **Non-blocking is not trivial** — it means nothing forbade the current state |
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
| **gate decision** | `approved`, `changes-requested`, `denied`, `reopened`. A refusal is drafted on the same terms as an approval and is rejected if it says nothing about why. Deviations are no longer answered here: the pull request is where a person accepts them (D-181) |
| **the draft-and-rename pattern** | a drafter writes a complete file *beside* the real one and prints the rename. **Renaming is the decision**; a draft nobody renamed records nothing |
| **draft file names** | `approval.<decision>.draft.md`, `solution.draft.md`, `techstack.draft.md`, `constraints.draft.md`, `decisions.draft.md`, `areas.draft.md`. The decision is in the name, so a denial and an approval cannot look identical at the moment of the move. Adoption leaves a person the solution, stack and constraints drafts; a harvest writes the constraints, decisions, stack and areas drafts; git ignores them all. The options menu written alongside the stack's draft is **not** a draft and is not here: it is a report, nothing renames it, and a declared stack carrying it is something `sdlc doctor` reports |
| **the gate**, the hash-bound gate | `sdlc gate <id>`: whether the build may run. Exits 1 if not |
| **the coverage baseline** | `.sdlc/coverage.json`: the whole-codebase line rate the last merged cycle recorded. **Never a target**: under `coverage_floor` a cycle may not fall below it, and at or above the floor it follows the rate. The binary writes it; a person may re-measure it in a pull request of its own after a change to how coverage is measured | `.sdlc/coverage.json` |
| **`approval: none`** |
 a solution declaring that no person's approval is required. The gate asks for no ledger and keeps every other refusal, comparing each document with what its latest round judged; every report says no person approved. `required`, the default, is what an absent key reads as | `.sdlc/solution.md` |
| **the seven files only a person writes** | `approval.md`, `abandoned.md`, `constraints.md`, `decisions.md`, `areas.md`, `solution.md`, `techstack.md`. Every agent write to one is denied, by path and by command alike. `mechanisms/protected-files.md` is how |
| **the guard note** | the blockquote at the top of `solution.md`, `techstack.md` and `constraints.md`, and of their drafts, saying only a person changes the file — so a denied agent write is explained by the file a person opens |

**Twelve things refuse at the gate**, and `guides/handling-refusals.md` is where each is looked up: no
cycle · abandoned · a planning document missing · not approved · an unreadable ledger · a standing
refusal · an unreadable round · never interrogated · a hash mismatch · an open blocker · a stale round ·
a findings round it anchored has changed.

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
| **`base.sha`** | the commit the diff is measured from: where the branch diverged from the integration branch, or from the default branch when there is none |
| **`diff.patch`** | the change, measured between `base.sha` and `head.sha` with `.sdlc/` left out. A working file, never committed: the chain rebuilds it from the two commits, and a checkout without it gets it back the same way (D-124) |
| **`proof-sources.txt`** | what a plan's proofs rest on outside the change, as `head.sha` holds it: the block of each named test the diff never mentions, and whole each file a proof names by path. Written only when there are some, never committed, rebuilt like `diff.patch` and covered by the evidence hash (D-168) |
| **`tests.json`** | one normalised test summary. **The only test record a verification reads** |
| **`timings.json`** | how long the run took — its start and finish, each results file's span — and its 25 slowest tests, read from the results files. Committed, and handed to no reader: for diagnosis, never a criterion's proof (D-169) |
| **`tests.trx`**, **`build.log`** | the runner's own output, kept as it was written for the cycle's verify, and **ignored by git**: the pipeline publishes its own (D-92) |
| **test summary** / **test outcome** | what ran and what each test did. The build checks the suite **actually ran** rather than that the runner exited zero |
| **declared files** | what the plan said it would change, against what changed: **undelivered**, **undeclared**, and **governing** — a changed file holding a rule the change is judged by |
| **verification** | adjudicating the build against the spec from the evidence alone. Per criterion: which test did the plan name, did it run and pass, and **does that test actually establish the criterion?** | `verify.md` |
| **evidence check** | a string match of a verdict against the evidence: every name it cites that none of the recorded evidence holds, and every test it says passed that did not. **Reports, and never refuses** — a backticked name may be a type or a log line, so whether a claim rests on its evidence stays the reader's judgement (D-120) | `sdlc evidence check <id>` |
| **remediation** | going round `implement` and `verify` after a blocked verification — `implement` handed its unmet criteria and findings, two lists, since a Not met the readers agree with raises no finding. **Never a planning document** | `/sdlc:remediate` |
| **remediation budget** | how many passes have run and how many blockers stood after each. When a pass undoes an earlier answer or two in a row reduce nothing, one different approach may be taken as a deviation; it escalates when that stalls too, or a third deviation bends the same approved text — **no number of tries to run out of, no ceiling, and no check-in on a count** (D-66, D-179) |
| **bounded remediation** | remediation held to that budget: bounded by progress rather than by a count of passes, so a cycle that stops improving escalates rather than thrashing | `/sdlc:remediate` |
| **escalation** | what a remediation that stopped converging makes of a blocked cycle: it stalled or regressed again after one different approach, or a third deviation bent the same text (D-179). **Computed, never declared.** An escalated cycle never opens a ready pull request, only a draft; *say what to do, then fix it* is the way on |

**See also:** [`build`](../skills/README.md#build) · [`verify`](../skills/README.md#verify) · [`remediate`](../skills/README.md#remediate) · [Handling blockers](guides/handling-blockers.md)

## 8. The pull request, and the guarantee

| | What it is | Where |
|---|---|---|
| **`pr-ready`** | whether a cycle may open a pull request, without opening one. Exits 1 if not | `sdlc pr-ready <id>` |
| **the five pull-request refusals** | not verified · the verification did not pass · escalated · the chain does not hold · a test run, judged by a local build |
| **a choice** | an entry in `deviations.md` of kind `choice`: something the build settled that the intent, the decisions, the constraints and the plan left open, and a reviewer would want to know — a library, a pattern, a default. It quotes no approved text, because it departs from none; the verify readers hold it to the decisions and the plan's purpose like the rest of the change (D-180) | `.sdlc/cycles/<id>/deviations.md` |
| **the report** | what a build hands back when it ends: every deviation, every choice, and every concern its verification raised without blocking, each by what it says. It leads the pull request, and merging accepts everything in it (D-181) | `sdlc report <id>` |
| **the run report** | the one report a wave or a series ends on, never one per cycle or per wave: a short entry per cycle with its own report's facts, a section per wave of a series with the forecast beside what ran, what stopped and what it held back, every intent line a series left `(inferred)`, and every rule, decision and successor named with its text. The binary renders it; the agent writes only a brief summary above it. It is the run's pull request body, and a copy is kept in `.sdlc/reports/` (D-207) | `sdlc report --run` |
| **the pull request body** | the cycle rather than a summary somebody invented: led by the report, then the problem in the intent's own words, each criterion with the verification's verdict, the tier, the risk, and anything still open |
| **the chain check** | `sdlc verify-chain`: re-establishing from the repository that the chain is **complete, approved and hash-matched**. It runs every check rather than stopping at the first |
| **the pipeline** | where that check runs: `.github/workflows/sdlc-verify.yml`, or `.azuredevops/sdlc-verify.yml`. **A solution recorded as `local` gets none** |
| **branch policy** | what makes the check a promise. Until a policy requires it the file is advisory, and the directory looks enforced without being enforced. **The default**, which `sdlc branch-policy` prints for a person to run: pull requests only into the integration and default branches, nobody bypassing, the check required on both, squash into one and merge commits into the other, no approval required (D-136). `--approvals`, `--owner` and `--self-approve` ask for reviewers, name who must approve, and let an author stand in |
| **the claim graph** | which cycles' plans declare the same files, so which pull requests will likely conflict and the order to merge them in. Advice, read by the sitrep; it no longer schedules builds |
| **collision** | two cycles changing the same thing, met when the second one's pull request is merged | *fix it* (`/sdlc:remediate`), which runs `resolve-conflict` first |
| **the admission check** | the pull-request check on a pull request carrying one cycle's planning documents and nothing else — or, from `slices/<slug>/plan`, every plan sliced together and the successors the slicing wrote: the plan, its approval and its hashes, no build and no verify. A slicing is refused if one plan fails, or needs a file only a sibling creates. Chosen by what the pull request changes, never by its branch name | `sdlc verify-chain <id> --base <branch>` |
| **the upgrade check** | the pull-request check on a pull request that moves the solution to another release and does nothing else: the pin, what `sdlc init` writes, and answers in the declared stack to rows the release asks about. Each framework file must be exactly what the pinned release's adoption writes from the base's copy, and each stack line added must answer a row the base never mentioned. It needs no cycle, because nothing in it is a judgement. The same check passes a pull request that only moves dependency versions — in `Directory.Packages.props`, `global.json` or the tool manifest, of any size, naming each major and each move back (D-134). Chosen by what the pull request changes, never by its branch name | `sdlc verify-chain <branch> --base <branch>` |

### The integration branch and the factory

| | What it is | Where |
|---|---|---|
| **the phases** | Define (intent, spec, plan, to the approval), Build (build, verify, pull request) and Operate (reading what runs, filing leads). Labels over the stages, never commands | `CONCEPT.md` §3.3 |
| **the integration branch** | where approved plans wait and built cycles land, `development` unless `solution.md` names another. Adoption creates it from its own commit and switches to it when it does not exist, and `sdlc doctor` fails while it is missing. Every change reaches it by a pull request; main receives only what a person promotes, which is the team's process. A **promotion** is a pull request into the default branch, and only the integration branch or a `release/*` branch may open one: `sdlc promotion` decides (D-136) | `integration_branch` in `solution.md` |
| **a cycle's branches** | `cycle/<id>/plan`, `cycle/<id>/build`, `cycle/<id>/build-unattended`, `cycle/<id>/forward` — one per kind of work, each started from the right base | `sdlc branch <id> <kind>` |
| **a slicing's branch** | `slices/<slug>/plan`: the integration branch with each sibling's plan branch merged in, so one pull request admits every plan sliced together and the successors the slicing wrote. Composed, never worked on, and deleted on merge (D-125) | `sdlc slices <slug> <id>...`, `/sdlc:pr <id>... --plan` |
| **the queue** | the cycles a build would take now: approved, no verify, not abandoned, and no `cycle/<id>/...` branch on the remote. A listing, never a state file; deleting a branch re-queues its cycle | `sdlc cycles --queued` |
| **the factory** | a pipeline that runs `/sdlc:build --unattended` against the integration branch, started by a person and never on a schedule. It finds, builds, verifies and opens pull requests — the cycle the most successors wait on first, or the one cycle named — and **never merges** | `sdlc factory` |
| **the factory bundle** | what runs unattended, kept apart from what a person uses in a cycle (D-91). Today it holds only the factory agent image; the pipeline templates still live in the `sdlc` plugin and the binary | `factory/` |
| **the factory agent image** | a Linux container that is an Azure Pipelines agent carrying `sdlc`, Claude Code and the plugin, all built from one commit, so the binary and the plugin cannot come from different releases. Run as a service, a fresh container per job, on a machine that exists for it | [`../factory/README.md`](../factory/README.md) |
| **a draft pull request** | what a cycle that did not pass gets: its attempted code, verify and evidence, marked not ready, so a person picks it up rather than the factory building it again | `/sdlc:pr <id> --draft` |
| **the sitrep** | the morning's page: what is ready to merge and in what order, what needs a person, what was held back and why, what is waiting and going stale, and which successors a person can start | `sdlc sitrep` |
| **guidance** | how the framework, or the solution, would do one kind of thing — build a pipeline, say — handed to a plan whose declared files switch it on. It only guides planning: nobody judges a plan against it and it never blocks. The solution's own entries under `.sdlc/guidance/` replace the framework's by name, and its code, decisions and rules come first. `mechanisms/guidance.md` says how it differs from a perspective | `sdlc guidance`, `sdlc guidance <id>` |
| **a perspective** | a way of examining a change — security, operability, maintainability, accessibility, UI/UX and others — that steers authoring with a short list per stage and judges in a fresh reader of its own. Only a finding citing a rule it lists blocks; the rest are suggestions, recorded with the perspective's name — a finding's `perspective` field, a build's `Noticed: [name]` line — so `sdlc harvest constraints` can count which perspective keeps finding things. `mechanisms/perspectives.md` says which a cycle gets and when each runs | `sdlc perspectives <id>`, `sdlc perspective show <name> --for <stage>` |
| **a lead** | anything a person can hand `intent` to begin a cycle: `azuredevops:1234`, `github:56`, `suggestion:<cycle>/<finding>`, a ready `successor:<slug>`, or a path. Its outside text is untrusted, and the interview still happens | `sdlc lead show <reference>`, `/sdlc:intent --from <lead>` |

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
| **the pre-tool hook** | judges a tool call before it runs and denies the eight things an agent may not do: the seven protected files and a findings round |
| **hook policy** | the rules that judgement applies. Absolute rather than conditional — no mode, no flag, no check on which skill is running, **so a rule with no exception has nowhere for a bypass to live** |
| **tool kind** | `harmless`, `file-write`, `shell`. A write is judged by path; a shell command is judged by its text |
| **the command-text scan** | why a command that merely *names* a protected file beside a write verb is refused. The hook reads the command string, piece by piece, not the paths it would touch — so `git commit -F` with the message in a file is how a commit mentioning one gets through |
| **the turn-end hook** | asked before a turn ends. Sends the turn back when the recorded suite has a failure, when there are changes and no evidence, or when the tree has moved since the evidence. **Silent on a branch it cannot read as a cycle** |
| **hook host** | `claude`, `copilot`, `antigravity`. A name the binary does not know is refused rather than guessed |
| **hook event** | `pre-tool-use` and `turn-end`, each accepted under the host's own name too. Antigravity's turn end is deliberately not governed |
| **hook decision** | `allow` or `deny` |
| **config ownership** | whether the framework owns a host's hook file or merges two entries into one it shares. `.claude/settings.json` is shared; `.github/hooks/sdlc.json` is owned |
| **adoption writes every host's configuration** | a file is inert on a machine without that host, and a teammate opening the repository in a different agent is governed without adopting again |
| **exit codes** | `0` did what was asked · `1` **a rule refused it** · `2` the command line was not understood · `3` **the gate itself failed** |
| **refusal versus failure** | a refusal is the gate working and a failure is the gate broken. Different exit codes on purpose, so a hook never reports its own bug as a governed stop |

**See also:** [Handling refusals](guides/handling-refusals.md) · [Refusals](reference/refusals.md)

## 10. The skills

**Thirty skills, and the split that matters: a command is deterministic and the binary owns it; a
skill needs language and an agent runs it.** The binary never writes a planning document and a skill
never decides a gate.

| Skill | In one line |
|---|---|
| `init` | interviews the team about what matters here and proposes the first `LOC_` rules. Does not adopt — it hands over the command |
| `intent` | proposes how to slice the work into atomic cycles, opens them, and suggests the intent of each from the record and the code, asking only what it inferred — or interviews in full on request |
| `spec` | derives testable criteria from the intent alone |
| `plan` | decides how, and what proves each criterion. The last document before a person approves |
| `to-plan` | a playbook: takes an intent to a plan — one cycle or every one — checking the intent, then spec then plan, running the loop on each, picking up where each stopped, and hands back once with the approvals drafted. Given what should change, it runs `steer` first |
| `wave` | a playbook: runs the next wave from the backlog — ready successors started, ready parked cycles read again — plans it in one sandbox, then builds every cycle of it on one branch into one pull request; under `approval: required` it stops at the approvals first |
| `series` | a playbook: wave after wave, planned and built into one series branch asking nobody, ending in one pull request, until its wave limit or an empty backlog. Under `approval: required` it plans nothing and builds approved cycles in wave order. The factory runs only series |
| `interrogate` | judges one artifact against the rules, in a fresh process. Never edits |
| `refine` | applies a round's findings to that artifact. Never judges |
| `steer` | writes a mid-cycle correction, or suggestions taken in or declined, into the chain, editing rather than regenerating; `to-plan` runs it and checks again |
| `build` | a playbook: carries an approved cycle — or every one — to its pull request. `sdlc build` admits the plan and starts it again in a process of its own, which runs `implement`, `verify`, `remediate` while it converges, and `pr`, and hands back the report (D-178, D-187) |
| `implement` | changes code and tests to do what it is handed — the plan, a verification's findings, a person's instruction — records deviations and choices, and records the evidence. Never verifies |
| `verify` | adjudicates a build against its spec from the recorded evidence alone, in a fresh process |
| `remediate` | a playbook, *fix it*: a blocked verification, an escalation with a person's instruction, a change they ask for at the pull request, or a pull request that conflicts — runs `resolve-conflict` first on a conflict, then `implement` and `verify` while it converges, and `pr` updates the same pull request |
| `pr` | pushes a cycle's branch and opens its pull request into the integration branch — a verified build, an approved plan with `--plan`, a draft with `--draft`. **The only stage that touches the remote** |
| `resolve-conflict` | brings a cycle's pull request up to date with the integration branch when it conflicts or goes red, inside its declared files, and records fresh evidence; `remediate` runs it and verifies |
| `draft` | prints what belongs in a file only a person writes |
| `harvest` | a playbook: takes back what the completed cycles settled, one aspect at a time — `harvest-constraints`, `harvest-decisions`, `harvest-suggestions`, `harvest-stack`, `harvest-areas`, each answered with a person before the next — then `close-cycle` |
| `harvest-constraints` | proposes the rules the completed cycles' findings argued for, with the evidence behind each |
| `harvest-decisions` | proposes the decisions the completed cycles settled |
| `harvest-suggestions` | answers each open suggestion of a completed cycle: taken further, declined, covered, or ignored |
| `harvest-stack` | proposes the declared stack's rows for what it never mentions |
| `harvest-areas` | proposes the areas to add, rename, merge, remove, re-glob or tag |
| `close-cycle` | once every aspect is answered, commits the closed cycles' removal on a branch of its own and opens its pull request |
| `constraints` | shows the rules in force, all tiers resolved |
| `status` | shows where a cycle has got to, and the next command |
| `suggestions` | lists the open non-blocking findings a solution has accumulated, each concern once, and with `--all` whether each was accepted, declined or answered |
| `theme` | shows the roles the solution's surfaces use and their values, or a skeleton of roles to fill; never a value |
| `upgrade` | moves this machine's binary and installed plugin to a release and checks they agree. A solution moves by its upgrade pipeline or `sdlc upgrade apply`, never from here |
| `doctor` | checks this install and this repository and says what is wrong and whose it is to fix, adding the one check the binary cannot make: whether this session has the whole plugin. Never repairs |

**For what each one refuses, who starts it, and what it reads and writes, go to
[`../skills/README.md`](../skills/README.md);** [`reference/skills.md`](reference/skills.md) adds what
each takes. That file is the skills' own surface and this is a
summary of it.

## 11. Patterns worth naming

These are not files. They are the shapes that recur, and knowing them explains most of the rest.

| | What it is |
|---|---|
| **draft, harvest, suggestions** | three verbs split by who made the decision. **Draft** hands back a finished file for a decision you have already taken. **Harvest** reads what the completed cycles settled and argues for one you have not — a tool may propose a rule when it brings the evidence, and may not shape a rule somebody just typed. **Suggestions** forms no opinion: it lists what a reader raised and nobody acted on |
| **rendered, never copied** | the constraints, the decisions and the theme reach a session by being rendered, because a copy inside an artifact is a second place the truth lives |
| **derived, not declared** | `escalated` is what a remediation loop that stopped making progress makes of a cycle, not something anything writes |
| **the task list** | the host's own list of steps, which six skills open where a host has one. `init`, `intent`, `spec`, `plan` and `build` list one stage's steps and nothing past its hand-back; `build`'s own process and `to-plan` list one line per cycle, ticked only when that cycle passed or was planned. A view of one session, never the record: `sdlc doctor`, `sdlc status` and `sdlc cycles --pr-ready` say what is done |
| **a playbook** | a skill that composes others: it runs them, in order, and does no stage work itself — `to-plan`, `wave`, `series`, `build`, `remediate`. A playbook may run another, and what runs what never loops back. The kind is what a skill does, never who starts it (D-186) | [`../skills/README.md`](../skills/README.md), *Two kinds of skill* |
| **a single-responsibility skill** | every other skill: one job, one kind of output — a document written, edited or judged, the code changed, the pull request opened — and it starts no other skill. A person may start one directly; a skill only another runs is always one. A job that grows a second output is split, and a playbook runs both | [`../skills/README.md`](../skills/README.md), *Two kinds of skill* |
| **the loop** | interrogate, then refine, until a round is clean, a reader reports a conflict, or the panel's `Loop` line says it is stuck. A playbook's — `to-plan` runs it on the intent, the spec and the plan, and after `steer` from the highest document it edited (D-186, D-188, reversing D-29) | `/sdlc:to-plan` |
| **the human-in-the-loop hand-back** | a skill stops rather than chaining into the next stage. Chaining on would remove the decision point while appearing helpful |
| **`elsewhere`** | what a binary cannot answer and does not pretend to: whether a host loaded the skills, and whether the pipeline's check is required when the platform's CLI is not signed in. `doctor` marks these and names who to ask |
| **probe** | throwaway code on a `probe/<id>/<slug>` branch, outside the cycle's worktree and never merged, that makes one platform behaviour happen before a plan rests on it. The plan's `## Assumptions proven` quotes what it showed and names its commit, or says `None.` Nothing governs a probe branch (D-129) |
| **a criterion's cases** | the values a criterion's outcome holds over — failures, inputs, roles, starting states, devices — listed beneath it under `Cases:`. **The list closes the criterion**: a listed case unmet is wrong at plan and verify, and a case nobody listed is a suggestion. The list is argued once, at the spec, bounded by the intent's words (`CON_23`, D-132) |
| **the admission question for a proof** | *which case does this check never run, and which outcome does it never read?* If either has an answer, the proof is inadequate. A defect a reader can only imagine it missing is a suggestion, never a blocker (D-127) — and **an inadequate proof is worse than none**, because it produces a green verification instead of an obvious gap |

## See also

- [The guides](guides/README.md) — what to do, and in what order
- [The mechanisms](mechanisms/README.md) — how one works and why
- [The reference](reference/README.md) — every command, skill and refusal, one line each
- [The skills reference](../skills/README.md) — each skill in full
- [`CONCEPT.md`](../CONCEPT.md) — the design, and every decision behind it
