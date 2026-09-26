# ai-sdlc

A charter-governed software development cycle, shipped as a single Claude Code plugin.

An intent becomes a spec, a plan, a build and a verification. Each artifact is **interrogated
against a charter** before it advances, every finding is dispositioned, and every landed cycle
emits a **retro** that feeds evidence back into the process itself. That last loop is the point:
other tools stop at the project boundary and carry findings about *the software* back. This one
carries findings about *the process*.

The design record is `concept.raw.md` — frozen, and the authority for **why**. The rules this
repository's own work must obey are in `.sdlc/CHARTER.md` — the authority for **what must be
obeyed**. This file is the authority for **what each piece is and how it is called**.

---

## The cycle

Seven stages, each producing one artifact and passing one human gate.

```
intent → spec → plan → build → verify → deploy → maintain
   ↑                                                  │
   └──────────────────── caused-by ───────────────────┘

        retro is cross-cutting: it constrains every stage
```

| Stage | Artifact | The gate |
|---|---|---|
| intent | `intents/<id>.md` | you merge the intent's pull request |
| spec | `cycles/<id>/spec.md` | you approve |
| plan | `cycles/<id>/plan.md` | you approve, in writing, by adding `approved:` |
| build | the code diff | the approved plan is the contract |
| verify | `cycles/<id>/verify.md` | the outcome is decided by a script, not by the builder |
| deploy | a pull request | **you merge. Never automated** |
| maintain | a new intent | you triage: does this become work? |

A cycle is identified by `<yyyymmdd>-<4 hex>` — `20260925-a7f3`. It is assigned once by `intent`
and never changes: it is the filename, the branch, the worktree directory, the prefix of every
acceptance-criterion id, and the retro's identity.

---

## The twelve skills

Each is a directory under `skills/` holding a `SKILL.md`, invoked as `/sdlc:<name>`.

### You type these, one cycle at a time

| | What it does | When | How |
|---|---|---|---|
| **`init`** | Interviews you, scaffolds `.sdlc/`, copies a charter from a stack pack | once per solution, before anything else | `/sdlc:init [pack]` |
| **`intent`** | Interviews you and writes the intent, interrogates it, opens its pull request | a cycle starts | `/sdlc:intent [a sentence]` |
| **`spec`** | Turns the intent into acceptance criteria a test could check | after the intent merges | `/sdlc:spec <id>` |
| **`plan`** | Declares the files it will touch, computes the blast-radius tier, asks for approval | after the spec | `/sdlc:plan <id>` |

These are supervised because this is where the judgment is, and because each may need to ask you
something no amount of reasoning settles. An unsupervised spec is perfectly safe and probably
wrong.

### The batch runs these

| | What it does | When | How |
|---|---|---|---|
| **`run`** | Launches approved plans unattended, one worktree per cycle | several plans are approved | `/sdlc:run <id>…` or `--all` |
| **`build`** | Implements the approved plan and checks the result against what it declared | driven by `run`, or by you | `/sdlc:build <id>` |
| **`verify`** | Assembles `verify.md` from generated evidence and transcribes the outcome | after build | `/sdlc:verify <id>` |

A plan a person approved is a contract — a declared file list and a signed-off tier — so
executing it is mechanical and checkable. That is the only half worth batching. The batch
**builds; it does not think.**

### At landing, and afterwards

| | What it does | When | How |
|---|---|---|---|
| **`retro`** | Writes the completion certificate: what happened, and every finding with its disposition | at landing, on the cycle branch, before the squash | `/sdlc:retro <id>` |
| **`absorb-retro`** | Copies landed retros into this repository's corpus and proposes charter amendments | after cycles land | `/sdlc:absorb-retro [path]` |
| **`maintain`** | Surveys for dependency drift, CVEs and pack staleness, and **files intents** | routine upkeep | `/sdlc:maintain` |

`retro` refuses to write anything for a cycle that did not pass, and that refusal is
load-bearing: **the retro is the completion certificate.** The lifecycle derivation reads "a retro
exists" as `done`, so one written for a dead cycle makes lost work read as finished.

`maintain` never fixes anything. A CVE bump is a one-line change that can break a build, alter
behaviour, or pull in a new transitive dependency, so it runs the normal cycle like everything
else. Maintenance that fixed things directly would be a second path into the codebase skipping
every gate — and the path of least resistance, precisely because the work looks trivial.

### Called at every gate

| | What it does | When | How |
|---|---|---|---|
| **`interrogate`** | **A workflow.** Four orthogonal roles read one artifact independently, each finding is triaged for a citation, and the results are merged into one schema-valid document | at every gate, on the artifact that gate is about | `/sdlc:interrogate <path>` |
| **`repair`** | Works through blocking findings, re-checking after each one and halting when progress stops | an interrogation returned blocking findings | `/sdlc:repair <path>` |

`interrogate` is the only workflow, and it is a workflow for two reasons that both have to hold:
it needs several agents that **must not see each other's conclusions**, and it can never need to
ask you anything. Everything else is a skill, because a workflow cannot ask a question at all.

`repair` is not a stage — it has no artifact of its own, it edits the artifact under
interrogation. It exists because of a measurement: in the previous version, three of five blocking
findings in a repair round were **introduced by the previous round's repairs**, replicated here as
three of four. So it halts on no progress and never "repairs until clean", which would never fire.

### Deliberately outside the cycle

| | What it does | When | How |
|---|---|---|---|
| **`grill`** | Convenes an agent team to argue a contested question to a conclusion | a genuinely ambiguous decision, or challenging the charter itself | `/sdlc:grill <question>` |

It produces **no governed artifact**: no findings, no cycle, no state change. Interrogation works
because its roles cannot contaminate each other; grilling works for the opposite reason —
teammates challenging each other directly, which orthogonal roles cannot do. Use it to work out
what you think, then bring the conclusion to `/sdlc:intent`.

Five skills have side effects real enough that **only a person may start them**: `run`, `init`,
`absorb-retro`, `grill` and `maintain` are marked `disable-model-invocation: true`, so a model
cannot decide to launch eight worktrees or propose a charter amendment on its own.

---

## What is not a skill

**`deploy` has no skill.** It is `tools/Complete-SdlcCycle.ps1` — rebase onto master, re-verify,
write the suggestions file, open the pull request — and then **you merge**. The re-verification is
mandatory rather than optional: worktrees make file collision structurally impossible but not
*semantic* collision, where one cycle renames a method and another adds a caller elsewhere, git
merges both cleanly, and the build breaks.

**Supervised mode has no verb.** It is the absence of a driver: you type the stage commands
yourself. `run` is the other driver, calling the same stages back to back. The only permitted
difference between the two is *who calls the next stage*, because two code paths would drift and
the one used less would rot silently.

---

## The scripts

Where a rule must be **mechanical rather than instructed**, it goes in `tools/`. A script can read
files and be unit-tested; prose in a skill is something a model is asked to follow.

| Script | What it owns |
|---|---|
| `Get-SdlcFacts.ps1` | Every mechanical fact an interrogation needs, gathered **once** and handed to every role. Named checks, each citable by id |
| `Get-SdlcCycleState.ps1` | The lifecycle state of every intent, **derived** from git and the filesystem. No `status:` field exists anywhere |
| `Get-SdlcVerifyOutcome.ps1` | The verification outcome, decided in code from evidence the deciding process did not produce |
| `Add-SdlcFindings.ps1` | Files an interrogation's document under its cycle, after validating it |
| `Set-SdlcDisposition.ps1` | Records what was done about one finding, and refuses what the schema would reject |
| `Test-SdlcArtifact.ps1` | Validates an artifact against its shipped schema |
| `Sdlc.Validate.cs` | The validator itself — a file-based app, so there is no project to build |
| `Format-SdlcFindings.ps1` | Renders findings for a person: the committed suggestions file, or a regenerable full view |
| `Submit-SdlcIntent.ps1` | The intent gate: a branch off master, a commit, a pull request |
| `Find-SdlcCharterIntent.ps1` | Whether a charter change has already been requested — or already refused, with the reason |
| `Block-SdlcCycle.ps1` | Blocks a cycle on a charter intent: records the link, then discards the work built under the old rules |
| `Import-SdlcRetro.ps1` | Copies landed retros into the corpus by cycle id, and **refuses to overwrite one that differs** |
| `Get-SdlcSignals.ps1` | The charter amendment signals, computed on demand and never stored |
| `Invoke-SdlcRun.ps1` | Dispatch: the launch confirmation, then one worktree per cycle |
| `Complete-SdlcCycle.ps1` | Landing: rebase, re-verify, suggestions, pull request |
| `Move-SdlcRelease.ps1` | Moves the `release` ref, bumps the plugin version, tags it |
| `Assert-SdlcCharterGate.ps1` | The `PreToolUse` hook that refuses an edit to the charter or a pack outside an approved charter intent |
| `SdlcGit.ps1` · `SdlcArtifact.ps1` | Dot-sourced helpers: git without PowerShell turning success into failure, and one answer to what kind of artifact a path is |

---

## How a finding travels

This is the path that makes the retro loop countable rather than prose to be parsed.

1. **`interrogate`** fans out four roles — *does this contradict an invariant, does it contradict
   itself, do its claims about the tree hold, what is missing* — and returns one document.
2. The calling stage writes that document out and files it with **`Add-SdlcFindings.ps1`**, which
   validates it against `schemas/findings.schema.json` and refuses an invalid one outright. One
   document per interrogation, under `.sdlc/cycles/<id>/findings/`.
3. **State is assigned in code, from the citation.** A finding reaches `blocking` only by citing an
   invariant that exists, a check that actually failed, or two locations that demonstrably
   conflict. An agent can argue for importance; it cannot grant it.
4. Each one gets a **disposition** via `Set-SdlcDisposition.ps1`. `rejected` and
   `accepted-wont-fix` are never interchangeable: the first says the finding was wrong, the second
   says it was right and nobody acted — which is the evidence an invariant may be too broad.
5. **Verification refuses to pass** a cycle carrying an open or blocking finding with no
   disposition.
6. Unresolved suggestions land in `.sdlc/suggestions/<id>.md` at landing. They are intake, not a
   courtesy bin: a recurring cluster on one theme is the signal that an invariant is missing.
7. The **retro** carries every finding with its category, attributed stage, detection, citation and
   disposition, in a table **generated** by `Format-SdlcFindings.ps1 -For retro`. The shape is a
   contract rather than a presentation: `Get-SdlcSignals.ps1` parses it back out, so a table of an
   agent's own design is a table no query can read.
8. `Import-SdlcRetro.ps1` copies each retro into `retros/<solution>/<cycle-id>.md`. An identical
   copy is a no-op; **a differing one is refused**, and that refusal is the only reason the corpus
   is permitted to exist — a copy of an immutable artifact is not a second list precisely because
   divergence is impossible.
9. `Get-SdlcSignals.ps1` computes the amendment signals across the whole corpus, partitioned by
   solution, and **stores nothing**. A signal is a prompt to investigate, not a verdict: acting on
   one means changing the charter, and only a person can grant that.

---

## Layout

```
.claude-plugin/    the plugin manifest and the marketplace catalog
skills/            twelve skills, one directory each
workflows/         interrogate.js — the only workflow
tools/             the scripts, the hook, and the schema validator
schemas/           one schema per artifact kind, plus the findings schema
packs/             stack packs. dotnet-library is the first
hooks/             hooks.json, which registers the charter gate
retros/            the corpus: one directory per solution, filled by absorption
.sdlc/             this solution's own charter and its cycles
tests/             MSTest, driving the shipped scripts as external processes
```

`.sdlc/` is where a governed solution keeps its own work:

```
.sdlc/CHARTER.md                         the authority every artifact is checked against
.sdlc/intents/<id>.md                     one per cycle, append-only
.sdlc/intents/abandoned/<id>.md           dropped, with a mandatory reason
.sdlc/cycles/<id>/spec.md · plan.md · verify.md · retro.md
.sdlc/cycles/<id>/findings/<target>.r<n>.json    one document per interrogation
.sdlc/suggestions/<id>.md                 intake, one file per cycle
```

---

## Installing

```
claude plugin marketplace add https://github.com/MofonCode/ai-sdlc.git#release
claude plugin install sdlc@ai-sdlc
```

`release` is a single moving branch with no commits of its own, plus a version tag at each move.
Subscribe once; a person decides when you see something new. Plugins are cached per version, so a
release that does not bump `plugin.json` makes an update a silent no-op — which is why CI fails a
`release` move that does not bump it.

To develop the framework itself, point the marketplace at the working tree instead, so what you
test is what you edited:

```
claude plugin marketplace add C:\path\to\ai-sdlc
claude plugin install sdlc@ai-sdlc
```

---

## Building and checking

```
dotnet test                                          the suite
claude plugin validate . --strict                    the marketplace manifest
claude plugin validate .claude-plugin/plugin.json --strict    the plugin, its skills and its hooks
```

Windows and Windows PowerShell 5.1, deliberately: the suite parses every shipped script with the
real 5.1 parser, which is the only way a whole class of encoding and continuation defects was ever
catchable. Run `claude plugin validate` too — it reads the skill frontmatter and the hook manifest
with the loader's own parsers, and it is what caught a skill whose fields were silently dropped.
