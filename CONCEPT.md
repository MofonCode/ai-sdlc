# ai-sdlc — concept

**Iteration 4.** Rewritten 2026-10-02 from a reconciliation of the previous draft against the
implementation, which closed twenty-four contradictions. That working-through is commit `adf15cc`
and is not carried forward as a file.

The original longhand draft is preserved unedited in [`CONCEPT.raw.md`](CONCEPT.raw.md).

**How to read this.** Sections 1 to 18 state what is true. Section 20 says why, one decision at a
time, with what was rejected. No section repeats a reason, and no decision repeats a design — one
fact, one owner. A statement in the design carries a `(D-nn)` marker where a decision governs it.

**Where a term is defined.** This document argues; it does not define. [`docs/README.md`](docs/README.md)
is the components document — every concept the framework names, with what it is, what uses it and where
it lives, in one or two sentences each. Read it first if a word here is unfamiliar, and put a new
concept's definition there rather than in the section that happens to introduce it.

Iteration 3 shipped as `0.8.3` and is archived on the `archive/v03` branch of this repository.
Where this document says a thing already exists, that is where it exists.

---

## 1. What this is

A host-agnostic plugin that governs a team's software lifecycle. A change does not travel from a
request to a diff. It moves through phases, each producing a file the next phase reads, and each
file is challenged against a fixed set of rules before it advances. Nothing advances on assertion.

Two things it exists to make possible:

- **A person approves a plan before code exists**, and the approval is recorded against the exact
  bytes that were approved.
- **Approved plans build unattended**, one or many at a time.

It has a strong opinion about the stack it scaffolds, and that opinion is swappable. The rules are
stack-neutral and travel to any stack; **the tool itself needs .NET**, so host-agnostic is not
stack-agnostic.

---

## 2. Principles

### 2.1 Enforcement lives below the agent

The agent is never the thing enforcing a rule. Three layers, and only the last is a guarantee:

| Layer | Mechanism | Bypassable by | Purpose |
|---|---|---|---|
| Advisory | skill and instruction text | the agent, constantly | guidance |
| Local deterministic | the `sdlc` gate binary, a pre-tool hook, a turn-end hook | a human with a shell | fast feedback |
| Non-bypassable | `sdlc verify-chain` as a pull-request check, plus branch policy | nobody without repo admin | the guarantee |

The promise made to a team is the third layer: **nothing merges unless the artifact chain is
complete, approved, and hash-matched.**

A rule of its own, carried from iteration 3: **a directory that appears enforced and is not is
worse than one that never pretended.** A check that silently does nothing — a CI job with no
credentials, a hook that fails to start — is the failure mode to design against.

### 2.2 The gate is a program, not a prompt

One executable owns every process constraint and exits non-zero with the warning text. Every skill
on every host does nothing but call it. The same binary runs in the pre-tool hook, in the turn-end hook, and
in the pipeline — one implementation, three call sites. It carries the schema evaluator with it, so
artifacts are fully validated where they are written.

**The command line is the mechanism** (D-26). If an MCP server ships, it is a surface over the same
commands rather than a second implementation.

### 2.3 Constraints belong in the lowest layer that can hold them

1. **The compiler.** Analyzers, `TreatWarningsAsErrors`, `.editorconfig` severities, central package
   management. An analyzer is the most host-agnostic enforcement there is — the build rejects the
   code whichever agent wrote it, with no hook and no adapter.
2. **A schema or a hook.** Anything mechanical about an artifact: a required section, a key that must
   be present, a file the plan did not declare.
3. **A constraint.** Only what needs reading comprehension — whether a spec answers its intent,
   whether a criterion is testable, whether a proof reaches its claim.

**Analyzers police the code, schemas and hooks police artifact shape, constraints police the
reasoning.** §7.2 makes this an admission test the library applies to itself.

### 2.4 Judging and editing are separate acts

`interrogate` judges and never edits. `refine` and `remediate` edit and never judge.

The corollary: **nothing that reacts to a failing test may edit a planning document.** If a red test
can cause an agent to adjust the spec, the criteria become whatever the code happens to do.

**What enforces it is the gate, not the hook.** The hook denies every agent write to `approval.md` and
cannot do more than that: it sees a tool call, which carries no skill identity, so it cannot tell an
edit to the spec made by `refine`, where it is the whole job, from the same edit made by `remediate`,
where it is the inversion (D-43). What catches the inversion is slower and certain — a planning
document edited after approval no longer matches the hash the ledger recorded, so `sdlc gate` and
`sdlc verify-chain` both refuse and name the document. So the inversion cannot reach a merge; it can
waste a cycle before the gate catches it. §6.3 says which guard is which.

### 2.5 Side research and evaluation run in a new process

The main window keeps only what it needs to decide. Interrogation, fresh readers, and any
go-and-find-out step run in their own process (D-01).

### 2.6 Flexibility comes from making compliance cheap

Not from making constraints optional. Where the correct path is expensive, the answer is a tool that
makes it affordable — never a shortcut around it (D-17).

### 2.7 The plugin does not govern itself

ai-sdlc is not an ai-sdlc solution. If it were, a change to a constraint would be judged by the
constraint it changes, and a defect that blocks the pull request would also block the fix for that defect.
The process is exercised by running cycles in `dice-trial`.

---

## 3. The cycle

```
intent -> spec -> plan -> build -> verify -> pr
                            ^         |
                            +- remediate (while reducing) -+
```

| Phase | Answers | Produces |
|---|---|---|
| `intent` | why this change exists | `intent.md` — the problem, not a solution |
| `spec` | what done means | `spec.md` — acceptance criteria, each testable |
| `plan` | how | `plan.md` — the approach, the files, the proof per criterion |
| `build` | the change itself | a diff, plus build and test evidence |
| `verify` | whether the change did the spec justice | `verify.md` — an outcome and its findings |
| `pr` | integration | a pull request |

Two loops run inside the phases:

```
interrogate -> refine -> interrogate         an artifact, until it stops converging
verify      -> remediate -> verify           the code, while each pass reduces what is open
```

**Neither loop counts rounds, and both stop on oscillation.** An artifact loop stops when one rule has
been answered and raised again three times (D-65); a remediation loop stops when a pass undoes earlier
work, or two in a row reduce nothing (D-66). **A remediation halt sets the verify outcome to `escalated`,
and `pr` refuses it. An artifact-loop halt reports and hands back to a person:** what decides whether
the artifact advances is still its open blockers, which already refuse the build (D-65). The
reason neither is a count: on the intent that ran eleven rounds, round seven found that the headline
change was unreachable and round eleven found that no success criterion failed if it were skipped
entirely — and a cap low enough to catch a loop going in circles would have stopped it at four.

Each stage skill runs the artifact loop itself: it writes, interrogates, refines, and interrogates
again until the round is clean, a reader reports a conflict, or the loop is stuck, then reports and hands
back
(D-29).

A stage will not run until the artifact before it has an interrogation round with no open blocking
findings (§5.4).

### 3.1 Every class of change runs the full chain

`intent` interviews the user and establishes whether this is a new solution, a feature addition, a
feature change, or a bug fix. **Intent, spec and plan are all required, always.** The classification
informs how each artifact is written — a bug fix's spec derives its criteria from a reproduction
rather than a desired outcome — and never skips one.

**The type is settled before the interview's first question, because it decides the questions** (D-95).
A person may name it — `--type bug`, `add` or `change` — and a lead may carry it; a new solution has no
flag and is detected, as a scaffolded solution with no cycle yet approved. The interview then asks one
question at a time, by type, each with example answers, and *not sure* is an answer with a place to go.
The intent's sections are the same for every type: the questions vary, the contract downstream does not.

**Splitting work into deliverable chunks belongs to whoever is prompting** (D-25). They write
smaller intents and run each through the cycle; no tooling accommodates splitting.

**And a change that arrives without a chain is refused rather than waved through** (D-45). The
pull-request check runs on every pull request, so it also meets the ones nobody opened as a cycle — a
dependency bump, a revert, a hotfix branch. It reports that there is no chain and exits non-zero,
because that is true and because the alternative would make the branch name the bypass. A team that
wants a class of branch to merge anyway says so in branch policy by not requiring the check on it,
which is a decision somebody can read; deleting the step is a different decision that reads as
nothing.

### 3.2 Steering a cycle that is already underway

`steer <id> "comment"` interviews, records the decision in the intent's `Fixed decisions` with
attribution, and carries the change down the chain — updating and re-interrogating the spec, and the
plan when one exists. It requires an intent and a spec, edits rather than regenerates, reports what
changed, and refuses on an approved cycle until a reopen is recorded (D-17).

Steering changes *how*, not *what* — with one exception: **a suggestion a reader raised in this cycle,
named by its id, may be taken in or declined**, several in one run, and with only an intent when every
part of the request is a finding id. A suggestion taken in becomes a wanted outcome in the person's words
and a declined one a fixed decision, each attributed and naming the finding; the intent, spec and plan
hand-backs each ask once which to take in. A comment that changes the problem is a different intent, and
a new outcome nobody raised is a new cycle (D-97).

### 3.3 Three phases: Define, Build, Operate

| Phase | Stages | Who | Ends at |
|---|---|---|---|
| **Define** | intent, spec, plan | a person, locally | the planning approval (§5.1) |
| **Build** | build, verify, pull request | the factory unattended, or a person by hand | a person merging the pull request |
| **Operate** | — | a scheduled run | a lead a person may take into Define (§3.4) |

**A phase is a label, never a command** (D-82). Nothing runs Define: each stage is still a moment where a
person decides to continue, which is why D-16 has no `design` skill.

### 3.4 Leads

**A lead is anything a person can hand `intent` to begin a cycle** (D-89): a work item, a grouped error
from production, an open suggestion, or a file. `intent --from <reference>` takes one, with a prefix spelled
in full — `azuredevops:1234`, `github:56`, `jira:PROJ-123`, `suggestion:<cycle>/<finding>` — or a path.
The binary fetches and normalises it, and the skill interviews from what it prints; the person still
answers the interview. The intent records the reference, so the pull request can link and close the item.

**Operate files work items; it writes no artifact of its own.** A scheduled run reads the solution's
telemetry store, groups errors by a signature, strips personal data, and files a bug for each signature
with no open item. The tracker then answers review, dismissal and deduplication, and nothing Operate does
lands on `development`. The tracker and the store are declared in `solution.md`.

**A work item is untrusted text** handed to an agent: the binary marks it as data, and the planning
approval stands between it and any code.

**What is built.** `sdlc lead show` reads `azuredevops:` and `github:` items through `az` and `gh`, so the
binary never holds a token, and reads suggestions and files; `intent --from` interviews from it and records
`lead:` in the intent's frontmatter. `sdlc operate` reads Application Insights through `az`, ranks by
frequency, scrubs, deduplicates against what it filed before — an open item, or one a person dismissed,
stops a signature being filed again; one closed as done and recurring is filed again as a regression — and
files bugs on the platform's tracker; the factory runs it before it builds. Jira, and telemetry stores
other than Application Insights, are not built yet.

---

## 4. Artifacts

### 4.1 Every governed document is an envelope carrying a body

**Five artifacts, four of them documents.** A constraint's `Applies to` names one of five subjects, and
`sdlc constraints --for` narrows on the same five: `intent`, `spec`, `plan` and `verify` — the governed
documents — plus `diff`, which is the change itself. **The rest of §4 is about the four.** The envelope,
the body and `## Open` are properties of a document; a diff has none of them, and `CON_07.2` and
`CON_11` are the two rules whose subject it is.

| Part | Holds | Same across documents | Governed by |
|---|---|---|---|
| **envelope** | `framework_version`, `cycle`, and the `## Open` region | yes, identically | the envelope schema; `CON_14` governs `Open` on three of the four (§19) |
| **body** | the sections particular to that document | no, deliberately | a body schema per kind; the constraints bind this |

```
framework_version: 1.0.0
cycle: 20260929_1456_32

## Open
```

**The envelope is an allowlist, not a region.** Two frontmatter keys and one section. Everything else
in the file — any other key, any other section — is body, and a key or section nothing declared is a
violation rather than a tolerated extra (D-31).

**The test for where a field goes:** same on every document, with the same meaning and the same
consumer, goes in the envelope. Differs per artifact because a different consumer reads it, goes in
the body (D-07, D-07a).

`cycle` duplicates what the path carries, and the gate checks the two agree. `## Open` is required
even when empty, or the gate cannot tell "nothing is open" from "the author forgot the section".

The **body is flat.** Every section in it is evaluated content, so there is no sub-structure
separating "the part that is judged".

### 4.2 The body, per artifact

| Artifact | Body | What consumes each |
|---|---|---|
| `intent` | `Problem` | `CON_01`; `spec` derives criteria from it |
| | `Wanted outcomes` | `CON_16` — this is its left-hand side |
| | `Fixed decisions` | `CON_01` routes decisions-already-taken here; `plan` cites them; `steer` writes here |
| | `Must not change` | `CON_16`, which names it |
| | `type` | the change classification; later artifacts read it |
| `spec` | `Acceptance criteria` | `CON_03`, `CON_05`, `CON_06`, `CON_12`, `CON_16`, `CON_17`, `CON_20`, and `verify` |
| | `Out of scope` | `CON_05`, `CON_16`; a disposal destination (§5.4) |
| `plan` | `Shape and departures` | `CON_15`, `CON_18`, `CON_19` |
| | `Files` | the blast-radius signal and the claim graph; ordering reasoning lives here |
| | `Proof` | `CON_06`, and `verify`'s criterion table |
| | `Risk` | the person at the gate |
| | `tier` | the blast radius: `local`, `contract`, `data`, chosen by reversibility |
| `verify` | `Criteria` | the criterion-by-criterion adjudication; `CON_08` binds every claim in it |
| | `outcome` | `pass`, `blocked` or `escalated`; `pr` and `verify-chain` read it |

A section exists because something reads it: a constraint, a mechanical check, a downstream skill, or
a person at the gate (D-06).

### 4.3 `## Open` — what an artifact deliberately does not assert

Questions nobody has answered, criteria that cannot be written yet, and limits of the proof. One
name on every artifact (D-08).

An entry is something **in scope** that this artifact cannot settle, with the reason it cannot. Three
things it is not, each with its own home: work nobody has done yet (a later cycle), something wrong
(a finding), something chosen out (`Out of scope`).

**An assumption is not an open item** — it is a provisional answer rather than the absence of one. It
goes in the artifact's decisions region marked unconfirmed: `Fixed decisions` on an intent, `Shape
and departures` on a plan. A spec has neither and needs neither.

**`CON_14` governs this region on an intent, a spec and a plan, and no constraint governs it on a
verify — a chain check does.**
D-03 is what makes that a gap rather than a detail: the open region is the compliant exit, which makes
it the exploitable one, and `CON_14` is the only control on the three it binds. On a verify the
temptation is specific — recording a criterion as a limit of the proof rather than as unmet — and every
other check in the chain would still pass, because the plan has its proof entry and the outcome reads
`pass`. `CON_08` does not reach it either: it governs claims *in* `Criteria`, not absences recorded
elsewhere.

**A mechanical check rather than a rule, which is the library's own routing rather than a preference.**
§7.2's second admission test refuses a constraint that a schema or hook could decide, and *whether every
criterion is adjudicated* is presence — which D-05 already establishes a parser decides. It lives in
`verify-chain` rather than the body schema because a schema validates one document and has no access to
the spec, while correlating two documents is what that layer already does for the proof entries. §13
carries the check.

### 4.4 The plan holds two kinds of claim

| Section | Kind | Judged by |
|---|---|---|
| `Shape and departures` | a **decision** — which approach | `CON_15`, `CON_18`, `CON_19` |
| `Files` | an **estimate** — which files, how large | nothing; it is a signal |

A plan holds a file table, never diffs. Line-level change is the build artifact — the branch and
`diff.patch`.

### 4.5 Evidence

`build` records what happened as files rather than prose: the diff, the build log, a test summary,
and the base commit. `verify` reads those and nothing else, so its verdict cites test names and diff
lines rather than recollection. Where the evidence does not reach, `verify` says so.

**Evidence is normalised at the boundary** (D-13). `build` writes both what the toolchain produced
and one framework-defined summary — test name and outcome — and `verify` reads only the summary.

**What is committed and what is published** (D-92). The summary (`tests.json`), the base commit and the
chain are committed with the code. The toolchain's raw output — `build.log`, `tests.trx` — is ignored by
git and published by the pipeline, the trx to the platform's test tab.

`build` treats warnings as errors, enforced by `Directory.Build.props` rather than by instruction.

---

## 5. Gates and approvals

### 5.1 One approval per cycle, taken before the build

A cycle has exactly one gate: the planning approval. It covers `intent.md`, `spec.md` and `plan.md`
together and is taken once, immediately before `build`. One decision, one ledger entry, three
hashes.

Approving means agreeing to all three at once. There is no separate intent gate or spec gate,
because a person reading a plan is reading it against the spec it cites.

| | Local approval | The pull request |
|---|---|---|
| Defends against | an agent that *forgets* | a human who did not look |
| Mechanism | a ledger entry, three hashes, a hook that refuses `build` | code-owner review |
| Timing | before any code exists | after |

**The gate is local, and only local.** A code owner merges the pull request, never the agent that
authored it.

`constraints.md`, `decisions.md`, `techstack.md` and `solution.md` are approved by being hand-edited — a
person places each, and none is part of a cycle gate (§5.3). `theme.json` is the exception and is changed
by an ordinary cycle that declares it, so the planning approval covers it like any other declared file
(D-71).

### 5.2 The approval ledger

One file per cycle, `.sdlc/cycles/<id>/approval.md`, an append-only ledger of decisions. The last
entry stands.

```yaml
---
cycle: 20260929_1456_32
framework_version: 1.0.0
decisions:
  - decision: changes_requested
    by: Peter Almanzar
    at: 2026-10-01T11:06:00Z
    hashes:
      intent.md: 3f9a2c7b...
      spec.md:   91c4de08...
      plan.md:   7e2b5504...
    reason: the spec has no criterion for delivery as a page at all

  - decision: approved
    by: Peter Almanzar
    at: 2026-10-01T11:12:00Z
    hashes:
      intent.md: 3f9a2c7b...
      spec.md:   5b07af31...
      plan.md:   d40e1196...
---
```

Four decision values: `approved`, `changes_requested`, `denied`, and `reopened` (D-19). A refusal
carries a reason, which `CON_10` governs. A reopen supersedes an approval, returning the cycle to
pre-approval so the documents can be rewritten and re-approved — the recovery path when a plan turns
out to be wrong.

The block between the `---` fences is **frontmatter**: a YAML header carrying machine-readable facts,
with the prose body below it for people. Schemas and gates read the frontmatter; humans read the
body. Frontmatter is read with a real YAML parser and never a regular expression.

**Three hashes in one entry, of each document's canonicalised bytes** — CRLF normalised to LF,
trailing whitespace stripped. They are an attestation that a person read these three documents, not
a chain; derivation is the findings round's job (§6.2). If any of the three no longer matches, the
gate refuses and the approval is taken again.

**Timestamps are written into the file.** A file's modified time cannot serve: git does not store
mtimes, so a clone sets every file's to the moment of checkout.

### 5.3 No skill writes a protected file. A skill drafts it.

Six files only a person writes:

| File | What is drafted |
|---|---|
| `approval.md` | a gate decision — three hashes, who, when, and a reason on anything but an approval |
| `abandoned.md` | why the cycle stopped |
| `constraints.md` | a proposed `LOC_nn` — **proposed rather than drafted**, D-69. Adoption drafts the file itself with its header and no rule, D-94 |
| `decisions.md` | a proposed `DEC_nn` — proposed from the approved record, D-70 |
| `techstack.md` | the declared stack — the file copied beside itself, or the framework's default where none is placed yet, for a person to rename, D-67 and D-76. Adoption writes the first draft, D-94 |
| `solution.md` | the whole document, beside itself — its four keys carried through and its prose unchanged, D-62. Adoption writes the first draft, D-94 |

**Each of the three a solution places at adoption carries a note at its top** saying that only a person
changes it, so the file a person opens explains a denied agent write before the denial reads as a broken
install (D-94).

One verb drafts four of them: `sdlc draft approval <id>`, `draft abandon <id>`, `draft solution`,
`draft techstack`. There was a fifth drafter, `draft profile`, until no solution recorded a profile
(D-76). `draft approval` also takes `--deny` and `--changes-requested`
(D-11). Each produces a ready-made entry; the person reads it, decides, and places it.

**Two of the six are the exception, and the verbs split on them (D-69, D-70).** A constraint's wording
*is* the decision, so there is no finished entry to hand back and the drafter printed a blank form — the
one member of the family that did. A recorded decision has the same shape, and is proposed from the
approved record rather than drafted. `sdlc propose constraints` replaces the drafter: it reads the solution's whole
history and argues from the record, and prints the empty form only when the record holds no pattern.
What separates the two verbs is who made the decision — **`draft` is for one already taken, `propose`
for one the record is pushing toward** — and the licence that makes proposing legitimate is that it
arrives with its evidence: a tool may propose a rule when it brings the record, and may not shape a
rule somebody just typed.

**A gate decision is placed by renaming a file, and the other three by hand (D-52).** `draft approval`
writes the complete ledger — `approval.<decision>.draft.md`, named for what it records — beside the
ledger it would replace, and prints it with the rename. Where a decision already stands, the draft
reproduces it above the new entry fence to fence, so the rename cannot lose history.

**The pre-tool hook denies every agent write to all six, with no exception** — and every agent command
that would move anything onto one. There is no approve command for an exception to be carved out for, so
every hook rule stays absolute (D-09).

One cost remains: a recorded hash cannot be checked by eye, so `status` shows the first twelve
characters of each recorded hash beside each document's current twelve and a refusal names *which*
document changed.

**The approval pins the documents and not the argument, and that is deliberate.** The three hashes say
exactly what was agreed to, and nothing records which interrogation rounds had happened — there was
such a field, `findings_rounds`, and it was left over from the model D-27 removed: while an approval
could *accept* a finding, the acceptance was only meaningful against a round, because a finding keeps
its id across rounds. Nothing accepts anything now, nothing read the number, and it was a hand-pasted
copy of something already on disk.

Nothing needs it. **A round raising a blocker after approval already refuses the build** — the gate
reads the round files each time rather than trusting the ledger — and a round raising only a
suggestion is not supposed to block, by D-30. Rounds accumulate and are never rewritten, so the only
way the argument can change behind an approval is a round file going missing, which the gate refuses on
(§5.4). And "what argument had been had when this was approved" is derivable: each round records
`interrogated_at` and the entry records `at`.

**If the argument ever does need pinning, hash the rounds** the way the documents are hashed, rather
than recording a count. A count is a claim; a hash is a check.

`draft approval` refuses while any blocking finding is open.

### 5.4 What holds the planning stages

**A clean interrogation round is required to advance.** `spec` will not run until `intent.md` has a
round with no open blocking findings, and `plan` likewise on `spec.md` (D-02).

Three mechanical checks need no reading comprehension:

1. **every intent open item is disposed of by the spec** — as a criterion, under `Out of scope` with
   a reason, or under `Open` citing the intent item (D-05)
2. every spec criterion maps to at least one file and one named test in the plan
3. every changed file is declared in the plan, and every declared file changed — recorded as a
   signal, not a gate (§7.7)

With every intent item disposed, anything still open at plan time is by construction out of scope or
explicitly deferred: anything in scope got a criterion, and `CON_06` and `CON_12` require every
criterion to have a proof that can run. **So the gate before build is about dependency, not the
absence of open items** (D-04).

---

## 6. Interrogation and findings

`interrogate` runs against one artifact in fresh context, judges it against the constraints in force,
and writes a findings round. It never edits.

**The fresh context is a fresh process** — the host's own non-interactive mode. That buys
independence from *investment bias*: the reader never saw the interview, the drafts, or the author's
reasoning. It does not buy independence from *model bias*, so a borderline judgement flips the same
coin twice.

**What the reader is given is the constraints, the artifact, and the artifact above it — plus
`techstack.md` when the artifact is a plan.** The line being drawn is between a rule source and the
cycle's own history: the first is what the document is judged against and predates the cycle, the
second is what the new process was opened to withhold. `CON_15` is a rule entirely about conformance
to the stack file, so a reader without it could object neither to a departure the plan admitted to nor
to a declared default the plan silently left unapplied (D-58).

### 6.1 Findings are data

`findings/<artifact>.r<round>.json`:

```json
{
  "framework_version": "1.0.0",
  "cycle": "20260929_1456_32",
  "artifact": "spec.md",
  "upstream_sha256": "3f9a2c7b...",
  "interrogated_at": "2026-10-01T11:08:39Z",
  "round": 1,
  "findings": [
    {
      "id": "F_01",
      "severity": "blocking",
      "disposition": "open",
      "cites": {
        "id": "CON_16",
        "heading": "a spec covers every outcome its intent asks for, and all of each one"
      },
      "statement": "...what is wrong, in full",
      "locus": "...where in the artifact",
      "remedy": "...what would fix it"
    }
  ]
}
```

An empty `findings` array is a clean round.

A round may also carry the loop's third ending:

```json
"conflicts": [
  {
    "between": [
      { "id": "CON_05", "heading": "a spec introduces no scope its intent did not ask for" },
      { "id": "CON_16", "heading": "a spec covers every outcome its intent asks for, and all of each one" }
    ],
    "statement": "...why each remedy excludes the other",
    "locus": "...where the two rules meet"
  }
]
```

**A conflict is not a finding.** A finding is an objection to the artifact: a correct artifact exists
and this is not it. A conflict is an objection to the rule set as it lands on this situation: no
correct artifact exists, because every edit satisfying one rule breaks the other. It carries two
citations or more — the one place in the model where multiplicity is right — and no id and no
disposition, because it is a per-round observation rather than a tracked objection. The conflicts that
stand are the latest round's: one nobody re-declares has been resolved, by the same reader's-silence
rule that moves a finding to `answered`. Absent and empty mean the same thing, unlike `findings`
(D-64).

**A citation is the id and the rule's own line, as one object.** §7.1 calls that pair a constraint's
*name*, and this is that pair rather than a second arrangement of it. The heading is copied verbatim
from the set the round was judged against and frozen there (D-63), so a finding can be read without
resolving its id against a library that has since moved. One citation and not a list: a finding is one
objection resting on one rule. Two rules whose remedies exclude each other is a conflict, which §6
makes an ending of the loop rather than something a finding carries.

| Severity | Meaning |
|---|---|
| `blocking` | stops the artifact advancing. **Cannot be waived by any mechanism** (D-27) |
| `suggestion` | non-blocking. Recorded, accumulated, surfaced by `sdlc suggestions` |

A non-blocking finding is *non-blocking*, not *trivial*.

| Disposition | Meaning |
|---|---|
| `open` | outstanding |
| `answered` | the artifact changed to address it |
| `withdrawn` | retracted by a later round |

Disposition transitions carry who and when.

**Rounds accumulate and are never rewritten.** A finding keeps its id across rounds for the life of
the cycle, so a carried finding is distinguishable from a new one, and the latest round that mentions
one says where it has got to. A per-artifact summary — highest round, count of open blockers — is
what the gate reads.

**A disposition moves off `open` only on a fresh reader's silence** (D-49). The reader judges the
document in front of it and has no history; the round records that an earlier finding it did not raise
is now `answered` — or `withdrawn`, where the objection was dropped rather than answered. A finding the
reader raises again stays open whatever anyone says about it, and so does one nobody can place in
either case. This is the only exit from a blocking finding, because D-27 removes every other one.

**How many rounds is the owner's call, and there is no cap** — but the loop is not unwatched. The panel
reports what each round found and the cost of a round, and its `Loop` line names a rule that has been
answered and raised again: at two points it tells the refiner it is refighting old ground and what the
rule is caught against, and at three the loop stops and hands back (D-65). What it never says is that the
rules conflict, which the counter cannot tell from a refiner repeating a mistake.

### 6.2 A round records what it judged, and what against

`upstream_sha256` is the hash of the artifact above, as the round saw it. A round is clean **as of**
that upstream, so changing an upstream makes a downstream round stale, the stage gate fails, and
re-interrogation is required (D-21). `subject_sha256` is the hash of the document the round judged, so a
document edited after its clean round is stale as well; a verify round adds `evidence_sha256`, the run it
read, which `verify-chain` compares with the evidence recorded now.

**The binary writes the round** (D-101). `sdlc round <id> <artifact> --from <file>` takes the readers'
findings and the session's dispositions, stamps the round number, the time and every hash, and refuses
while anything above the artifact is unsettled — interrogated as it stands, against its upstream as it
stands, with no blocker open. So the chain is interrogated top down, one document at a time, and a
round judged against a document still moving is refused when it is written rather than discovered at the
gate. The hook denies every agent write to a round file, which is what makes the command the only way
one lands.

### 6.3 `refine` and `remediate` — two editors with opposite guards

| | `refine <id> <artifact>` | `remediate <id>` | Enforced by |
|---|---|---|---|
| Runs after | `interrogate` | a blocked `verify` | the skill |
| May edit | the one named artifact | source and tests | the skill |
| May never edit `approval.md` | yes | yes | **the hook**, at the tool call |
| May never edit source or tests | yes | — | the skill |
| May never edit a planning document | — | yes | the skill, and **the gate** afterwards |
| Capped | not on rounds; stops when one rule has come back three times | not on passes; stops when a pass undoes earlier work or two reduce nothing | the skill |
| Also refuses | once the cycle is approved | — | the skill |

**Read the right-hand column before the other three.** Only one row is denied at the tool call, and
the rest are the skills' own discipline — which is what `skills/refine/SKILL.md` has always said and
what this section claimed otherwise: *"Both guards are hook rules on file paths, not instructions in a
prompt."* They are not. The hook receives a tool call and a tool call does not say which skill made
it, so a per-skill rule has nothing to read; D-43 records why inventing something for it to read makes
things worse rather than better.

**The row that matters has a second enforcer.** `remediate` editing a planning document is the one
inversion §2.4 exists to prevent, and it is caught after the fact rather than at the keystroke: the
three documents' hashes are in the approval ledger, and `sdlc gate` refuses on a mismatch and names
the document. That is a weaker guarantee than a denial — the edit happens, and a cycle is wasted —
and a real one, in that nothing merges on an inverted chain.

They are two skills rather than one with a mode because their guards are opposites, which holds
whether the guards are hook rules or not.

What makes them skills rather than freehand edits is the bookkeeping — dispositions move to
`answered` and what changed is recorded — plus a bound on how far an edit may reach, a pass counter,
and identical behaviour on every host. A person editing an artifact by hand needs neither.

### 6.4 The loop's three exits

Each stage skill loops until the round is clean, a reader reports a conflict, or the loop is stuck —
with "stuck" decided by the oscillation counter rather than by the session doing the work (D-65). Every
finding has exactly three exits:

| Exit | When | Where it shows |
|---|---|---|
| **healed** | fixable from information already present | the artifact changes; the report says what changed |
| **assumed** | something had to be supplied to proceed | the decisions region, marked unconfirmed |
| **asked** | it cannot proceed without a person | `## Open`, and the loop stops and asks |

**Assumptions are recorded in the artifact, not a log.** A report is read once; a section is read
every time the artifact is read, and it is what a reviewer's eye lands on at the gate. The rule is
not "never assume" — it is **never assume silently.**

**What cannot be assumed into existence.** An intent is written by interviewing a person. An
unmeasured figure may be recorded as an assumption or raised as a question; it may not be reworded
into something vaguer that passes, because rewording is the one move that removes the problem from
the page while leaving the belief in it.

**It stops on convergence or on conflict, not on a count.** A round producing no new findings is
convergence. Two constraints whose remedies exclude each other is a conflict, named and handed back.
Three endings have to be distinguishable: converged, open, conflicting.

### 6.5 Perspectives

**A perspective is a method, not a label** (D-90): security, operability, maintainability, accessibility,
UI/UX, infrastructure and cost, data and migrations, API compatibility, performance. Each is a document
embedded in the binary, carrying four things:

1. **a method** — how to examine. Security lists the trust boundaries and follows every input to where it
   is used; operability asks, of each new way to fail, whether somebody could tell from the logs at 3 a.m.
   what failed and for whom
2. **the questions it asks at each stage**, intent through verify
3. **its rules** — the only part that can block
4. **the evidence it requires**, from the profile's build settings

**It steers as well as judges, and never widens.** `intent`, `spec`, `plan` and `build` read a short
*cover this* list per active perspective. The intent asks the person its list rather than writing it in;
the spec writes an item where the intent asks for it and names the rest in `Out of scope`; the plan and
the build apply theirs to how the criteria are met. What a perspective wants beyond the intent comes back
as a suggestion, which a person takes in through `steer` (D-97). `interrogate` runs one fresh
process per active perspective, handed that perspective's full method and rules, then **one reconciling
reader** over the round's findings. `verify` asks each perspective's questions of the evidence.

**Which apply.** Security, operability and maintainability always. The intent names any others, seen in
the interview and covered by the approval's hash; at plan, the declared files add any the intent missed,
with a finding.

**Blocking or a suggestion, as every finding is.** Only a finding citing a rule blocks; what a method finds
beyond the rules is a suggestion, and a suggestion that keeps recurring is what `propose constraints` turns
into a rule. **So a suggestion carries the perspective that raised it** — a finding's `perspective` field,
checked against the perspectives the binary carries, and a build's `Noticed: [name]` line — and
`propose constraints` counts each perspective by the distinct cycles it raised something in, across both
stores. A perspective starts with no rules, and this count is how it earns them. **Two perspectives contradicting** apply neither: both become suggestions, each referring to the
other. A rule beats a method finding; two rules that cannot both be met are a conflict (D-64).

**What is built.** The document format, embedded under `Perspectives/`; `sdlc perspectives <id>` and
`sdlc perspective show`; the intent's `perspectives` key; `intent`, `spec`, `plan` and `build` reading the
cover lists, `interrogate` running a reader per perspective and a reconciling reader, `verify` asking each
perspective's questions of the evidence. **Security, operability and maintainability are written** — the
three always on — **and so is accessibility**, which an intent naming a user interface or a plan declaring
a `.razor`, `.cshtml` or stylesheet file switches on, and which judges against WCAG 2.2 AA unless a solution
records another target. None has rules of its own yet, so everything they find is a suggestion until rules
are adopted. The other five are not written. The fixtures that prove a perspective catches its planted
defect need a model to run, so they are an evaluation outside the suite and are not built.

---

## 7. Constraints

A constraint is a rule an artifact is judged against — one that needs reading comprehension to apply.
Every blocking finding cites exactly one.

### 7.1 Four parts, all required

| Part | Holds |
|---|---|
| **name** | the id and a one-line statement of the rule |
| **Applies to** | which artifact, and at what moment |
| **Forbids** | what becomes illegal, named concretely enough that an example could be written |
| **Why** | what goes wrong if the rule is absent — the cost it avoids, never a restatement |

The **Why** is what makes a constraint arguable rather than arbitrary, and what tells a person at a
gate whether a finding is cheap or expensive in this case. `Forbids` may not restate the statement.

### 7.2 What is admitted

Three tests, all of which must pass:

1. its `Forbids` clause names something that becomes illegal
2. **no analyzer, schema or hook could decide it**
3. it is incompatible with at least one plausible implementation

Rules about code go to analyzers — and where an existing analyzer already decides it, that analyzer
is used rather than a rule written. Mechanical rules about artifacts go to schemas or hooks.

**Stack neutrality, and it belongs to the global tier alone.** No `CON_nn` may name a language,
framework or package: they bind solutions on any stack, so one that named a tool would be a rule most
solutions are judged against for no reason. That is a constraint on the global library rather than on
the idea of a constraint — **`PRO_nn` is where a rule may name one, and that is what the tier is for.**
Said unqualified here until D-74, which is the wording that would have had somebody reject the first
profile rule written. A judgement that cannot be made without naming a tool — which layer proves a
criterion about a browser, which substitute makes a proof of stored data worthless — has the profile
tier or nowhere.

### 7.3 Tiers and resolution

| Tier | Id | Lives in |
|---|---|---|
| global | `CON_nn` | the plugin's library |
| profile | `PRO_nn` | the stack rules `techstack.md` marks `true`, carried by the binary (D-74, D-76) |
| solution | `LOC_nn` | `.sdlc/constraints.md` |

Global, then profile, then solution. The effective set is rendered on demand by
`sdlc constraints --for <artifact>`, never copied into a solution — and **that command is also how
the set reaches a session, on every host.** The binary carries the library, so that holds for an
installed tool and not only for one run out of a clone — D-41. Running it is the first step of every skill that judges
or writes an artifact. Per-host native delivery is an optimisation layered on top, never the
mechanism.

**One constraint is applied by a person rather than by an interrogation, and the set is exactly one.**
The effective set is matched by artifact name, so a rule whose subject is not an artifact is handed to
nobody. `CON_10.2` is that rule: it governs what a gate refusal's reason has to say, and a gate
decision is a person's own act rather than a document — the approval ledger is the one file only a
person writes, which is what makes an approval mean anything. `sdlc constraints --for` returns it for
none of the five artifacts, by construction.

That is stated in the constraint itself and asserted by a test, rather than left as an absence. A rule
nothing applies looks identical to a rule somebody forgot to wire up, and `CON_11` is the rule that
keeps the library from growing into decoration — so the human-applied set is declared and the next
rule that drifts out of reach fails the build instead of quietly joining it. F-27.

### 7.4 Ids and revisions

**Ids are a monotonic sequence.** Assigned once, never reused, never backfilled. A removed constraint
leaves a gap, and the gap plus the commit that made it is the record.

**A change to what is forbidden is a new constraint.** The old one keeps its id, stays on the page
verbatim, and gains `Superseded-by`.

**A change to the wording alone is a revision.** `CON_01` becomes `CON_01.2` — same `Forbids`, better
words. The new wording **replaces** the old on the page; a revision does not leave its predecessor
behind the way a supersession does.

**A citation resolves to the number, and the suffix is provenance.** `CON_07` and `CON_07.2` are the
same rule, because a revision cannot change what is forbidden — that is the definition separating it
from a supersession. So a finding judged under revision 1 was judged against exactly the `Forbids`
clause revision 2 carries, and the suffix records which wording its author read rather than which rule
bound them. Older wording lives in git. §7.4 said findings cite the revision *"so a round judged under
older wording stays distinguishable"*, which promised something the page could not deliver — both
revisions had left nothing readable at revision 1 (F-40).

**The alternative was rejected on evidence.** Keeping each predecessor on the page would honour that
promise literally and would have kept `CON_07`'s original `Why`, which claimed a hook enforced the
declared file set when no hook ever has. A rule library is read as current; preserving a corrected
error in it forever costs more than the distinguishability buys, when the thing being distinguished
cannot change the verdict.

**What this rests on, and what cannot check it.** It rests on a revision really leaving what is
forbidden alone — if somebody changes that and calls it a revision, the old rule is lost for real,
because a revision leaves no predecessor behind. That is **not mechanically decidable**: "did anything
become legal or illegal" is a judgement about prose, and the obvious proxy — comparing the `Forbids`
clause for equality — is wrong in the case that has already happened. `CON_10`'s prohibition survived
intact while its *subject* was deleted, so the clause was rewritten to name what was left. A check
calling that a violation would teach somebody to phrase a real change carefully rather than catch one.

So the judgement is argued in the commit that makes it, and what the suite guards instead is the half
where a check is honest: **a superseded constraint stays on the page and out of force.** That is where
loss would matter, and deleting such an entry is a regression nothing else would notice — the
replacement is still there, the count still looks right, and only a finding nobody is reading today
becomes unanswerable.

**A bare id is revision 1**, and that is the canonical form.

**Which of the two a change is, is a judgement, and the author makes it in the commit.** `CON_10` is
the worked example and a third shape: its subject — a reason for *waiving* a blocking finding — was
deleted outright by D-27, while the prohibition it carried about reasons that restate, plead schedule
or merely acknowledge applied unchanged to the one kind of reason left. So the rule was re-pointed
rather than retired, which D-27 argued and recorded.

Nothing was lost by not superseding it. A supersession keeps an old rule readable so a **live** finding
stays judgeable against it, and there is no such finding: no finding has ever cited `CON_10`, and the
act it governed cannot occur, so none can be written. Preserving its wording would have protected
nobody.

### 7.5 The set

| Id | Rule | Applies to |
|---|---|---|
| `CON_01` | an intent states the problem, not the solution | intent |
| `CON_02` | an intent's unknowns are recorded as open questions, not answered by guess | intent |
| `CON_03` | a criterion states an observable outcome and how it is checked | spec |
| `CON_05` | a spec introduces no scope its intent did not ask for | spec |
| `CON_06` | a plan's proof is adequate to the criterion it claims to prove | plan |
| `CON_07.2` | a change does only what its plan declared, including inside files the plan named | diff |
| `CON_08` | a verify claim rests on evidence that exists and supports it | verify |
| `CON_09` | no artifact restates another's content as its own authority | every artifact |
| `CON_10.2` | a gate refusal's reason says what has to change | a gate decision that is not an approval |
| `CON_11` | every constraint carries a Forbids clause that rules something out | a diff adding a constraint |
| `CON_12` | a criterion's check is one the solution's testing layers can run | spec |
| `CON_14` | an open item is one nobody can resolve yet, not one nobody has done | intent, spec, plan |
| `CON_15` | a plan says why it departs from a declared default, in either direction | plan |
| `CON_16` | a spec covers every outcome its intent asks for, and all of each one | spec |
| `CON_17` | a spec says what somebody is told when the action fails | spec |
| `CON_18` | a plan that changes a rule the cycle is judged by says what the rule would have cost | plan |
| `CON_19` | a plan says why it departs from a decision the solution has recorded | plan |
| `CON_20` | a spec says what is recorded when the action fails | spec |

**Two ids are absent because they no longer bind**, and each is explained once below.

`CON_04` required every
stated outcome to have a criterion and said nothing about a criterion covering only part of one,
which is the commoner failure and the harder to see: a narrowed criterion still traces to its
outcome and still passes a coverage check. `CON_16` replaces it and covers both, with the
remainder going in `Out of scope` where no test can reach it (D-61).

`CON_13` forbade a plan reaching for an
undeclared package, pattern or layer without saying what the declared default would have cost, and
said nothing about a plan leaving a declared default unapplied. That half was the one that went
unnoticed — the rule objected to the author who was adding and not to the author who was leaving
out. `CON_15` replaces it and covers both directions, scoped to work that calls for the default.
`CON_13` stays on the library's page verbatim, so a finding that cited it is still readable against
the rule it was judged under (D-59).

`CON_06` is the one to watch: it drew a blocking finding in roughly half of twelve runs on a golden
sample, the cause was traced to the sample rather than the constraint, and the fix was made by
reading rather than re-running.

`CON_16` and `CON_05` stay separate — a gap, creep and narrowing fail differently and are remedied
differently: a gap sends you back for another criterion, creep sends you to delete one, and narrowing
sends you to widen the one that is already there (D-61).

### 7.6 Where the "must be provable" principle lives

Four constraints, one per artifact, each naming what becomes illegal in that place:

| Artifact | Constraint | Forbids |
|---|---|---|
| intent | `CON_02` | stating as settled fact a figure nobody measured |
| spec | `CON_03` | a criterion that cannot fail, or an outcome with no way to observe it |
| plan | `CON_06` | a named check that could pass while its criterion is unmet |
| verify | `CON_08` | a statement with no cited evidence |

### 7.7 What blocks, and what is only a signal

**What blocks:** the build fails, tests fail, or the proof is not met — every criterion maps to a
named test and that test passed.

**What is a signal:** the comparison of declared files against changed files (D-15). `UNDELIVERED`
means the plan's blast radius was wider than the change; `UNDECLARED` means something reached the
diff without the hook seeing it. Both are recorded as suggestions. Their real uses are estimating
blast radius and detecting collisions for the claim graph.

`GOVERNING` is the third line and the only one that does not depend on the plan: it names a changed
file holding a rule the change is judged by — an architecture test, a build property,
`BannedSymbols.txt`, a hook, the pipeline, `techstack.md`, `constraints.md`. It reports a **declared**
file as readily as an undeclared one, because the architecture suite is the only mechanical
enforcement of shape the framework has and a cycle may amend it as ordinary declared work. The
dice-roller cycle did exactly that, correctly, and the edit reached review as one row among thirty-nine
in a file table. The set is what the framework writes or scaffolds, for D-40's reason — it can name
those with confidence, and a guess at what else a team treats as a rule is a list that is wrong
somewhere and trusted everywhere. A signal like the other two, under D-15: it refuses nothing.

---

## 8. The solution's own files

Five files in `.sdlc/`, split by **protection** — four only a person writes, and one an ordinary cycle
changes. Mutability was the stated axis for a while and stopped separating anything once D-67 and D-70
protected two more:

| File | Changed by | Protection |
|---|---|---|
| `constraints.md` | a person, by hand | the hook denies every agent write |
| `decisions.md` | a person, by hand | the hook denies every agent write (D-70) |
| `techstack.md` | a person, by renaming a draft — `init` does not create it (D-76) | the hook denies every agent write (D-67) |
| `solution.md` | `init` creates it; a person, by hand, after that | the hook denies every agent write |
| `theme.json` | a cycle whose plan declares it | the ordinary declared-file gate (D-71) |

**Protection is decided by two questions, and the rows split four to one.**

*Is something judged against it?* `constraints.md` and `techstack.md` are the two rule sources a plan is
read against, so an agent able to write either could satisfy a rule by editing it rather than arguing for
it. *Does a stage take its brief from it?* `decisions.md`, which is the sharper case — nothing judges a
plan against the record, and an agent that could write it could invent doctrine and then align to it.

**`solution.md` answers no to both and is protected anyway, for the reason that makes every hook rule
absolute (D-09, generalised by D-43).** The origin is a historical fact that must never change, and the
alternative is per-key protection — the framework's first conditional hook rule, which is where a bypass
would live. That is a rule about the mechanism rather than about this file: a hook rule with a condition
is a rule somebody can arrange to be exempt from, so the whole file is protected and the rule set still
states in one sentence. It is the third question a candidate for protection has to answer.

`theme.json` answers no to all three. Nothing judges a colour, no stage takes its brief from one, and a
hook rule nothing needs is one somebody later carves an exception into — which is the same argument from
the other end.

`.sdlc/README.md` is framework-owned, regenerated by `init` in update mode, unprotected.

**There is no git hook.** Adoption writes none and asks for none to be installed, and a cycle branch may
be pushed at any time, ready or not. What may merge is the pull-request check's to decide, and before
the `pr` skill pushes it asks `sdlc pr-ready` (D-78).

### 8.1 `constraints.md`

The solution's own `LOC_nn` constraints, hand-edited. There is no configuration flag unlocking it and
no special cycle type for protected changes.

### 8.1.1 `decisions.md`

The solution's own `DEC_nn` entries, hand-edited. **A constraint stops a cycle doing something; a
decision stops it re-deciding something.** Cycle forty cannot otherwise see that cycle seven chose an
outbox over two-phase commit, because the reasoning sits in one plan's `Shape and departures` and nothing
reads across cycles.

Four parts, and `Instead of` is the load-bearing one: an entry whose alternatives were never real records
something nobody chose. **A decision forbids nothing** — where a choice does, that half is a constraint
or an architecture test, and the two are separate artifacts of one choice. Ids are monotonic and a
reversed entry takes a new id while the old one stays on the page, as §20's own preamble has it.

**Departing is legal; departing in silence is not.** `CON_19` asks a plan whose approach contradicts a
standing entry to say in `Shape and departures` what the recorded decision would have cost here — the
same shape `CON_15` takes for the declared stack, and a separate rule rather than a reworded one because
what is forbidden changed (§7.4). D-72, which reverses the half of D-70 that shipped the record binding
nothing.

### 8.1.2 `theme.json`

A **register**, not an argument: the roles a solution's surfaces use, and the one place their values live.
Roles rather than colour names, so a value may change and a role may not. Machine-readable, because one
source is the point — a stylesheet imports it rather than copying out of it.

Not protected, and changed by an ordinary cycle that declares it. Enforcement, where a solution wants it,
is a linter under §2.3 rather than a constraint.

### 8.2 `techstack.md`

What this solution is built from, and what a plan builds with. **No global constraint names anything in
it** — stack neutrality (§7.2) forbids that — so no row here is mandatory by itself. What binds is the
**arguing**: `CON_15` makes a plan say why it departs from a declared default, in either direction —
reaching for something undeclared, or leaving something declared unapplied in work that calls for it.
Departing stays legal; departing in silence does not.

**A solution that wants a row to be mandatory adds a `LOC_` constraint or an architecture test.** One row
already is: liveness, which the scaffold writes and a fitness function keeps, because the cost of
omitting it is paid by somebody who did not omit it (§11.1, D-54).

**The file itself is a person's, not a cycle's** (D-67). A plan may not declare it, and the plan schema
refuses one that does; `sdlc draft techstack` copies the file beside itself for a person to rename.

**It also lists the stack rules the solution is held to, and that table does bind** (D-76). Under
`## Rules`, one row a rule: a `PRO_` id, `true` or `false`, and what the rule holds a plan to. A
solution is judged against exactly the ones marked `true`; `false`, or no row, is a rule that does not
apply. So what a person reads in this file is the whole of what the stack contributes to a plan's
judgement — nothing is selected by a name recorded elsewhere. Those two tables are told apart by
their width: a rule row has three cells and a default has two.

**And it lists the build settings files the solution takes** (D-77). Under `## Build settings`, one row
a file: its name, `true` or `false`, and what it does. `true` declares that the solution takes the file;
`false`, or no row, that it does not. Those rows are read from under that heading and nowhere else,
because a file name has no prefix to recognise it by. **A row declares and writes nothing** — the file
arrives inside a cycle whose approved plan declares it (§9), and `sdlc stack` reports which declared
files are not on disk.

**`init` drafts it and does not place it.** The first draft is the default profile's rows with every
shipped rule switched on, written by adoption beside where the file goes — and by the drafter where
neither is in place; a person cuts it down and renames it, and `sdlc new` refuses until it is there
(D-94). Its build settings rows start `true` on a scaffolded solution
and `false` on an adopted one (D-77). **An empty file is a legitimate answer** — nothing pinned, no
stack rule in force, every plan choosing freely — and is different from a missing one, which is nobody
having decided.

**A row is declined by answering it, never by deleting it** (D-93). A default not true here says `none`,
which pins using nothing; a rule says `false`. So a default the profile declares or a rule a release
ships, with **no row at all**, is one nobody was asked about — and `sdlc stack`, `doctor`, the drafter
and an update's report name it, while writing nothing. That is how a release reaches a person's file.

### 8.3 `solution.md`

Four keys. There was another, `profile`, until the declared stack listed its own rules (D-76); a
solution adopted before that may still carry the line, and nothing reads it (D-81).

| Key | What consumes it |
|---|---|
| `origin` | `intent`, to know whether "this repository holds no solution at all" is true |
| `standard` | `plan`, to know whether new work follows the profile's conventions or the surrounding code's. Absent derives from `origin` (D-56). A person moves it by renaming a draft from `sdlc draft solution --standard` (D-77) |
| `platform` | `doctor`, to tell a local solution from a broken install, and `pr`, to know whether there is anywhere to push. Absent reads as `github` (D-53) |
| `telemetry` | `operate`, which reads production's errors from it and files them as leads on the platform's tracker. Optional and written only when set; most solutions have none (D-89) |
| `integration_branch` | the factory, which builds from it, `pr`, which targets it, and evidence, which measures a cycle's diff from where it diverged from it. Absent reads as `development`; a solution with no such branch measures from its default branch (D-83) |

**`origin` and `standard` are two keys because they answer two questions and only one of them may
move.** Where the code came from is a historical fact. Which conventions its new work follows is a
decision a team is entitled to revisit, and an adopted solution that wants the profile's conventions
needs somewhere to say so — D-56. The origin did both jobs until then, which left the opt-in
inexpressible: a team could want it and the file could only record that the code was old.

**The key says which patterns a plan defaults to, and nothing about which files are on disk.** The
build settings are a separate declaration, in `techstack.md` (§8.2). An option on adoption used to make
both at once, and they were never one decision — D-77.

There is deliberately no baseline field. Constraints govern the change, never code that predates
adoption, so an existing repository is not in violation on its first day. **`standard: profile` is not
a baseline either** — it declares what new work follows, marks nothing forgiven, and records no
existing violation.

---

## 9. `init`

Adopting a process is a decision about how a team works, so `init` is human-invoked and never
model-invoked. Three modes (D-28):

| Mode | When | What it does |
|---|---|---|
| **greenfield** | a new solution | writes the governance only, `origin: scaffolded`, `standard: profile` |
| **onboard** | existing code, no `.sdlc/` | writes the governance only, `origin: adopted`, `standard: existing` |
| **update** | `.sdlc/` exists | refreshes only what the framework owns (D-40) |

**`init` decides nothing about the stack** — D-76, D-77. It takes no profile, places no
`techstack.md`, and writes no .NET build settings in any mode. What a solution is built from is declared
afterwards, in a file a person places. An option `init` does not take is refused, never ignored (D-81).
It takes two: `--platform`, and `--integration-branch`, `development` when absent, which it refuses
when the solution document already records another (D-94).

**`init` places none of the files only a person writes; it drafts three** — D-94. `solution.draft.md`
carries the keys it observed and a prompt where the paragraph goes, `techstack.draft.md` the
framework's default, and `constraints.draft.md` the header and no rule. Each carries the guard note
(§5.3), a draft already there is never written over, and `.sdlc/.gitignore` ignores all three so what
is left of one never reaches a commit. Until the document is placed, the draft's keys are what a
second run of `init` reads as recorded — otherwise it would find the hooks it committed, no document,
and decide a new solution was adopted.

**An onboarded solution that wants the framework's conventions makes two choices, after adoption**
(D-77). Which patterns a plan defaults to is `standard`, set in the solution document's draft before
it is placed, or moved afterwards by renaming a draft from `sdlc draft solution --standard profile`. Which build settings files it takes is a row each in
`techstack.md`, and they start `false` there because they apply a convention to code that never agreed
to one: `Directory.Build.props` makes warnings errors, so a build can fail on files nobody has touched.

**`init` writes no code, and no projects** — D-35. The solution itself — a solution file, its projects,
the banned-API list, the architecture tests and a reference slice — is `sdlc scaffold`, which refuses
unless a cycle's plan has been approved, and which also writes the files that constrain how that code
is written (`.editorconfig`, the three props files, `global.json`): they constrain nothing until code
exists, so they arrive with it. Code arriving at adoption would be the one thing in a governed
repository with no chain behind it.

**The build settings reach an adopted solution the same way, and only the ones its plan declares**
(D-77). `sdlc scaffold` never writes the skeleton there — the repository has a shape already — and
writes exactly the build settings files the approved plan's `Files` section names, leaving alone any
already present. A row in `techstack.md` is not enough to write one, and a row saying `false` does not
forbid a plan that argues for it: the plan is what a person approved for that cycle. So the moment a
build can start failing on untouched code is the declared scope of one approved change, rather than a
side effect of adoption or of whichever build ran next.

**What update may touch is decided by whose name is on the file — D-40.** It rewrites what the
framework named: `.sdlc/README.md`, `.github/hooks/sdlc.json` and `.github/workflows/sdlc-verify.yml`.
In a file the host and the team also own it owns its own entries and nothing else,
so `.claude/settings.json` is merged — the two hook entries are added or refreshed and every other key,
including a team's permissions and their own hooks, is kept. And where the path belongs to the platform
it writes only into an empty space: an existing `azure-pipelines.yml` is a repository's own CI, so the
step is printed for a person to place rather than written over.

**Update stamps what it wrote, and is run after every upgrade** (D-93). `.sdlc/README.md` carries the
release that last wrote the framework's files, and `doctor` compares it with the binary — older or
missing asks a person to run `init` here, newer asks for the binary. On an update the report also names
what the release ships that the declared stack never mentions; that file is a person's, so the `init`
skill walks each item and the answers land in a draft they rename.

A file it rewrites has to be one it named, which is why the GitHub workflow is `sdlc-verify.yml`. It was
`pull-request.yml`, and adoption replaced whatever was at that path and then committed it.

It refuses outside a git repository. Without git, `build` cannot record the diff that `verify`
derives from and the gates key on a `cycle/<id>` branch that cannot exist — so every hook would
permit everything while the artifacts still looked governed.

**It interviews, then proposes.** `init` asks what the solution is about and what matters in it, and
from **the answers** drafts an initial `LOC_` set for a person to place. From the answers, not the
codebase: reading an existing repository to decide what to forbid would make adoption a judgement on
code that predates the framework.

**`origin` is decided by `git ls-files`, and it is about code rather than commits.** A repository
holding only an empty commit, a README or a licence is `scaffolded`. `techstack.md` is drafted from the
default profile either way, and on an adopted repository the skill edits that draft from what the
repository actually uses before a person places it (D-76).

**`AGENTS.md`** is lean and under 200 lines: a short summary of what the solution is; references —
a relative path plus a one-line description — to `techstack.md`, the build and test procedure, and
how to run the application locally; and a repo map. It never contains constraints, and never anything
that goes stale the moment a cycle runs. If one already exists, `init` prints the leaned version and
leaves the file alone.

**It commits what it created, then creates the integration branch.** An uncommitted `AGENTS.md` is
untracked when a cycle takes its diff, so the diff records the whole file as new, and a plan that says it
*changes* that file can never match — the first cycle would fail `CON_07.2` for something adoption did.
After the commit, when the integration branch exists neither locally nor on the remote, it is created
there and switched to, so the default branch and the integration branch both hold the hooks and what a
person places next is committed where approved plans wait (D-94). The drafts are not committed.

**Its report ends with every draft left to place, and how.** What a person still has to place before
the first cycle is said in a sentence, then each draft to fill in, the rename for each — in `sh` and
the PowerShell spelling — and the commit after. `sdlc doctor` ends with the same block from the same
list, because each step finishes with a rename and a rename runs nothing. The constraints draft rides
along while it waits and is never a step left: a solution with no rules of its own is sound. Once the
other two are placed the step is the first cycle, and once a cycle exists the block is gone (D-79,
D-94). **A placed solution document still carrying the prompt is a `doctor` problem**, not a step left
(D-94).

It creates no empty directories.

---

## 10. Building and pull requests

### 10.1 `batch-build` completes cycles

A skill, executed rather than subscribed to. It collects the queued cycles, builds **and verifies** each,
and reports. Run by a person it works from that working copy; run by the factory, from a fresh checkout of
the integration branch (D-83, §10.5).

1. `sdlc cycles --queued` lists the work: approved, no verify, not abandoned, and no `cycle/<id>/...` branch
   on the remote (D-85).
2. Every cycle builds **independently**, from the same base, in its own git worktree on its own
   `cycle/<id>/build-unattended` branch. None waits for another or is rebased onto another (D-86).
3. With `--pr`, every cycle that reached an outcome gets a pull request — a draft when it did not pass —
   and the run ends with the sitrep.

**The graph is computed by the binary, not derived by the skill.** It is a set intersection over a file
table, so the binary does it the same way every time — and a graph a session worked out by reading plans
would come out slightly differently each run, while what it decides is what runs concurrently.

**Four per-cycle outcomes, and they are the run's rather than the document's.** `passed` means the
cycle's verify outcome was `pass`; `blocked` and `escalated` borrow that document's meaning; and **did not
finish** is a cycle whose build or verify could not run to an outcome, which reaches no verify document at
all. That last value is why the two sets are not one: a verify outcome is `pass`, `blocked` or
`escalated` and nothing else — the schema holds those three and `verify-chain` compares against the set
rather than a literal — so a run needs its own vocabulary for a cycle it never finished. *Not attempted*,
a cycle downstream of a failure in a serialised schedule, went with the schedule (D-86).

**Collisions are met at merge rather than scheduled around** (D-86). The claim graph still intersects
the declared files, and the sitrep reads it as advice: which pull requests will likely conflict, and the
order that keeps the conflicts fewest. A person merges one pull request at a time and resolves a conflict
with `resolve-conflict`, inside the cycle's declared files.

**EF Core migrations are regenerated, never merged.** Two branches that each add a migration snapshot the
same parent model, and the merged chain breaks in a way that compiles and then fails at deploy.

**A sibling's outcome changes nothing about another cycle.** Each gets its own pull request.

**Concurrency is capped by what a plan needs.** The claim graph carries a needs-backing-services bit,
derived from whether the plan touches the app host or declares service dependencies. Service-free
plans run wide; plans standing up SQL Server, Redis and a Service Bus emulator run under a low cap.

`CycleIndex` finds work by listing `.sdlc/cycles/`, which is the whole of it: a manifest would be a
second place the truth lives, and the one that goes stale is always the one a tool trusts. **Nothing
persists a run's own progress yet**, so a long batch that dies halfway has no record of which cycles
completed — §19 item 5. This section claimed otherwise for a while.

### 10.2 Local run modes

**Two variables, and the framework sets them without ever reading them.** `batch-build` exports
`SDLC_RUN_MODE` — `local` or `batch` — and `SDLC_RUN_ID`, unique per cycle invocation rather than per
batch, because the collision below is between two app hosts running at the same time and a id shared
across a run would not separate them.

**A solution's AppHost reads them and implements this table. Nothing in the framework can.** An
AppHost is conditional (§11.1) and arrives with the cycle that needs more than one process, so it is
solution code the framework does not write — the same reason EF and a `Web` project stay out of the
default profile (D-38). So this is a contract with the half the framework owns and a half it does not:

| | `local` | `batch` |
|---|---|---|
| host ports | stable, so a URL can be bookmarked | dynamic |
| container names | stable | `{app}-{SDLC_RUN_ID}` |
| container group | solution name, stable | `{solution}-{SDLC_RUN_ID}` |
| volumes | persistent, named | ephemeral, or named per run |
| dashboard and OTLP endpoint | on, fixed | off, or overridden per run |
| databases | persistent | named per run |

**Named volumes are a worse collision than ports.** Two concurrent app hosts calling
`WithDataVolume()` share one volume — the same database files, two writers — which fails strangely
rather than loudly. So the run id belongs on container names, volume names and database names, not
only on ports.

**The group is two labels, and one of them is undocumented.** Docker Desktop draws containers as
one collapsible row when they share `com.docker.compose.project`, which is what makes an app host's
services read as a solution rather than as six unrelated containers. The label alone does nothing:
Desktop recognises the set as a project only when `com.docker.compose.config-hash` is present too,
and its value is never read — `not-a-hash-at-all` registers. `com.docker.compose.service` is worth
setting as well, or the rows fall back to container names. No compose file has to exist.

**The run id belongs on the group for the same reason it belongs on the volumes.** A group that
stays the solution name under `batch` puts two concurrent app hosts in one row, so the developer
who opens Docker Desktop to see what is running cannot tell which cycle owns which container — the
collision the run id exists to prevent, reintroduced at the one place somebody actually looks.

**Two costs, both named.** `config-hash` is a Compose internal nobody promised us: it is in no
published label schema, and an app host setting it is really satisfying Docker Desktop's own
recognition logic, which a Desktop release may change without deprecating. That one is benign — the
containers stop grouping and nothing else moves. The second is not. Compose itself declines to act
on a set it did not create, where `down` reports *"No resource found to remove"* and `stop` and
`restart` are silent no-ops — but Docker Desktop's own group buttons bypass Compose and act on the
containers directly, so the grouping hands a developer a one-click Delete that removes a running
app host's containers without Aspire knowing. That is the right way round, because the button does
what it says and an app host whose containers vanished fails visibly, but it is a thing the
grouping buys rather than a thing it avoids.

**Nothing reads either variable today, and that is a gap rather than a design.** The default profile
ships no AppHost, so the collision above is unguarded until a solution builds one — and the first
solution to do so is the first thing this table has ever constrained. What the framework does now is
name the variables and export them, so that AppHost finds a contract instead of inventing
`ASPNETCORE_ENVIRONMENT=Batch` and every other solution inventing something else. The names are the
part worth having early; a count of solutions that agree on them is the part that cannot be had early.
The group label is in the same position — named here, implemented by the first app host that exists.

This section said *"Selected by an environment variable that `batch-build` sets"* and named no
variable, while nothing anywhere set or read one. The warning above had nothing acting on it, which is
the shape of enforcement §2.1 is written against — and the fix is not to claim less but to say which
half is missing.

Tests discover endpoints through `Aspire.Hosting.Testing` rather than knowing a port number.

### 10.3 `pr`

`pr` is what touches the remote: it pushes, opens the pull request, and links the work item with
the intent and spec details.

**`pr --all`** sweeps every cycle whose verdict is `passed`, with five guards (D-23): it opens one only
for `passed`; it re-checks each cycle's gate rather than trusting the verdict; it reports what it would
push and **asks once** before the first push; a push failure on one cycle does not stop the others;
and each pull request is reported individually.

**`batch-build --pr`** composes the two, invoking `pr` per completed cycle — `pr --draft` for one that
is `blocked` or `escalated` (D-85) — and asks before the run rather than at the end (D-24); with
`--unattended` the schedule was that question (D-84). Plain `batch-build` never pushes.

**`pr --plan`** admits an approved plan to the integration branch before it is built, asked of the gate
rather than of `pr-ready`, and merging it deletes the plan branch (D-83, D-87). Every pull request pushes the
cycle's own branch and targets the integration branch, or the default branch while there is none.

### 10.4 The integration branch

**Approved plans wait on `development`** (D-83), the name a solution records in `solution.md`. A person
pull-requests a cycle's planning documents into it from `cycle/<id>/plan`, and the branch is deleted on
merge; the factory builds from it; every built cycle is pull-requested back into it. Main receives only
what a person promotes, and the framework has no part in that (D-88).

**The local approval is still the gate.** The plan's pull request admits a plan to the factory; it does not
approve it. Its check is the admission check, chosen by the diff (D-87).

**Line endings need nothing.** Cycle files travel between branches now, and a checkout may turn LF into
CRLF on the way; the approval's hash is taken over canonicalised text for exactly that reason (§5.2), so
a plan approved on one machine still matches on another.

### 10.5 The factory

**The factory finds approved plans, builds, verifies and opens pull requests — and nothing else** (D-84).
It is `batch-build --pr` on a schedule, against `development`: the schedule is the confirmation, one run at
a time holds a lock, and the job's timeout is the spend ceiling.

**Every cycle it touches gets a pull request** (D-85): ready when it passed, a draft when it is `blocked` or
`escalated`. A cycle's branches sit under `cycle/<id>/` — `plan`, `build`, `build-unattended`, `forward` —
and any of them on the remote holds the cycle back. Deleting the branch puts it back in the queue.

**Every cycle builds from the same base** (D-86). The claim graph predicts which pull requests will
conflict and suggests a merge order; it no longer schedules. A person merges one pull request at a time,
and resolves a conflict with `resolve-conflict` inside the cycle's declared files.

**The morning starts from the sitrep**: what is ready to merge and in what order, what needs a person, what
was held back and why, what is going stale, and the newest stable point on `development`. It is a binary
command, and the factory publishes it as the run's summary.

**The factory never merges, and it never writes a person's file.** A blocked cycle is recorded as blocked;
abandoning it is a person's decision.

**The factory's agent is an image** built from one commit, carrying the binary and the plugin together,
and run as a fresh container per job on a machine that exists for it (D-96). It is the first thing the
factory bundle in `factory/` holds.

**What is built and what is not.** The queue, the branches, the admission check, the sitrep, `batch-build`
and `pr` working this way, the factory's pipeline template (`sdlc factory`: the schedule, the lock, the
headless run) and its agent image are built. Neither the template nor the image has run a real night's
build yet.

---

## 11. Tech stack and profiles

Strongly opinionated, and the opinion is a **profile** a solution starts from and then owns: the
default is what a first stack draft is written from, and nothing in a governed solution records one
(D-76).

### 11.1 `dotnet-default`

| Concern | Choice |
|---|---|
| language, framework | C#, .NET |
| unit and integration testing | MSTest on Microsoft.Testing.Platform |
| component testing | bUnit, for Blazor components |
| end-to-end testing | Playwright, in Chromium **and** WebKit |
| accessibility testing | axe-core, through Deque.AxeCore.Playwright, against WCAG 2.2 AA |
| ORM | EF Core |
| orchestration | Aspire, under the condition below |
| results | FluentResults |
| validation | FluentValidation, through a pipeline behaviour |
| infrastructure as code | Terraform |
| relational data | MS SQL |
| document data | Cosmos DB |
| cache | Redis |
| cloud | Azure |
| async messaging | Azure Service Bus |
| realtime push | SignalR, only where a live UI needs it |
| machine learning | ML.NET |
| api | ASP.NET Core |
| api description | OpenAPI, through Microsoft.AspNetCore.OpenApi |
| tracing, metrics and logs | OpenTelemetry, exported to Azure Monitor |
| liveness | ASP.NET Core health checks, at `/health` |
| web | Blazor |
| desktop and Android | MAUI Blazor Hybrid |
| ui components | MudBlazor |

**Shared UI is Blazor components in a Razor class library**, hosted by Blazor for web and MAUI Blazor
Hybrid for desktop and Android. MAUI does not target web.

**MudBlazor is one dependency, and the shared Razor class library is where it goes.** Taking it there
is what makes web and hybrid the same UI rather than two that resemble each other. It needs an
interactive render mode — which `Web` has by D-38 and a hybrid WebView has by definition — so a
statically rendered page would get its markup and none of its behaviour.
`Microsoft.FluentUI.AspNetCore.Components` is the alternative, and the choice turns on the breadth of
the component set an application shell needs rather than on who maintains it: this row is the profile
stepping off its own Microsoft-first habit, which is worth saying out loud rather than leaving a
reader to notice.

**A bUnit test of a component that renders one needs `AddMudServices` and loose JSInterop.** MudBlazor's
providers call into JavaScript, and bUnit's strict default throws on the first unplanned call — which
reads as a broken component rather than a missing registration.

**Liveness is one of two rows here that bind — D-54.** Every other row but orchestration is a default a
plan may depart from by saying why (`CON_15`); this one the scaffold writes and a fitness function keeps, because the cost of
omitting it is paid in production by somebody who did not omit it. It is deliberately not an `ApiResult`
endpoint: the caller is a load balancer, which has never heard of this solution's envelope.

**Observability is not the error envelope.** A boundary response already carries a trace id, which is what
lets somebody asking about one request find what was withheld. That is correlation, not tracing: a solution
with no OpenTelemetry registration collects no spans and exports none, so there is nothing to ask.

**Service Bus and SignalR are not alternatives.** Service Bus is asynchronous messaging between
services; SignalR is server-to-client push.

**An Aspire AppHost is conditional, and `PRO_04` makes the condition bind — D-98.** More than one
process, or one process plus a backing service a developer would otherwise start by hand. A single
executable with no backing services does not get one. The scaffold's architecture suite carries the
AppHost's rows from the first day, so the cycle that adds one adds a project and amends no rule. An
AppHost starts the processes and gives them one dashboard; it does not hand a standalone WebAssembly
page the api's address, which the page reads in the browser.

**`dotnet test` needs two settings or it does not run the suite.** On the .NET 10 SDK it defaults to
VSTest and Microsoft.Testing.Platform refuses that path, so the profile ships both:
`"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`, and
`<EnableMSTestRunner>true</EnableMSTestRunner>` in `Directory.Build.props` rather than one test
project. **Do not set `TestingPlatformDotnetTestSupport`** — the .NET 9 mechanism, and setting it
instead is what left a trial unable to run `dotnet test` at all.

**And run `dotnet test` with no flags.** On Microsoft.Testing.Platform, VSTest-style flags are not
options the runner accepts, so they are forwarded to the test application — which runs nothing and
reports `Zero tests ran` with exit 5, in under a fifth of a second. `--nologo` and `-v q` both do it.
This is the same symptom as the paragraph above by a different route, and it is the more expensive of
the two because the build looks fine and the output says `failed: 0` on the way past: it cost one
review an hour and one later session another. `skills/build/SKILL.md`, `AGENTS.md` and
`.github/workflows/ci.yml` all say so too; the section that owns the fact should be one of them. F-39.

A note recorded rather than buried: **ML.NET costs real capability** against the Python ecosystem on
competitive modelling work.

### 11.2 Testing layers, and which may prove a criterion

| Layer | Tool | Proves |
|---|---|---|
| unit | MSTest | a function in isolation |
| component | bUnit | a Blazor component renders and reacts, no browser |
| integration | `WebApplicationFactory<T>`, plus `Aspire.Hosting.Testing` | the composed application answering a real request |
| end to end | Playwright, Chromium and WebKit | what a person sees in a real browser engine |

- **A criterion about what the server produces** is proven by requesting it and parsing the response.
- **A criterion about what happens *in* the browser** cannot be — the response is the input to the
  behaviour rather than the behaviour.
- **Integration tests use real backing services.** The EF Core in-memory provider does not translate
  SQL, so it passes queries a real database rejects — a proof that could pass while its criterion is
  unmet.
- **A criterion about accessibility is proven end to end.** The axe-core scan reads computed styles and the
  accessibility tree, which a component harness never builds, so it runs in Playwright against WCAG 2.2 AA
  with zero violations expected. It finds about half of what WCAG asks; keyboard operation and focus
  placement are Playwright tests of their own, and the accessibility perspective is what asks for them.

**There is no device end-to-end layer.** Appium is absent, not scheduled: a flaky proof gets rerun
until green, which teaches a team to ignore red; the prerequisite lands on every machine and in CI;
and MAUI's own device-test tooling is not packaged for consumers. MAUI Blazor Hybrid hosts the same
components in a WebView, so bUnit tests them once and Playwright runs them in the two engines the
hybrid targets use. **What remains unproven is the native shell only** — platform permissions, app
lifecycle, deep links, native file pickers, platform chrome.

**A device-only outcome is a bound in the intent, never a criterion in the spec**, verified by a
person outside the cycle. `CON_12` keeps them out of specs. So MAUI-targeted work has some outcomes
this framework does not govern.

### 11.3 The kernel, and the conventions that come with it

**`MofonCode.Core` is public** — MIT, on nuget.org, published from its own repository by trusted
publishing with no stored key. So `dotnet-default` references it (D-34). An earlier draft had it
behind an Azure Artifacts feed and described the operation shape as something a private profile added;
that was wrong about the package and, following from it, wrong about the shape. The shape in §11.3.1 is
the framework's, not a house preference, and a public profile that could not scaffold it would leave
the most effective lever unbuilt — an agent copies neighbouring code far more reliably than it follows
instructions.

**What stays private is undetermined.** The `mofoncode` profile exists for whatever is genuinely
house-specific, and the honest position is that nothing has been identified yet. It is a mechanism
waiting for content rather than a profile with content waiting for a feed.

The kernel is a minimal mediator, `Result<T>` handlers over FluentResults, `TraceResult` over
OpenTelemetry activities, pipeline behaviours, composable specifications, throttled concurrency, and
the one shape allowed across a boundary (§11.6) — plus the conventions that go with it:

- **an operation that can fail returns `Result` or `Result<T>`**, and a failed result becomes the
  response at a boundary. Do not wrap an operation that cannot fail.
- **commands and queries go through the mediator** when there is an operation to dispatch, in what
  the plugin scaffolds *and in the plugin itself*.
- **one folder per feature, not one per layer.**
- **name the seam and the type.** A plan saying only "use MofonCode.Core" has chosen nothing.

**`MofonCode.Core` ships no custom analyzers** — no analyzer project, no diagnostic ids, no source
generator. What it ships is an analyzer policy (§11.4). So the three conventions above are prose
enforced by nothing, and each splits between a mechanically decidable half and a judgement half, of
which only the judgement half may be a `PRO_nn` constraint.

**Custom analyzers are developed in the profile that declares the convention**, not in the kernel.

**No MediatR** — the kernel has a mediator, declared in `techstack.md` so an agent does not reach for
it out of habit.

**`standard` decides whether a default binds, and `origin` only decides what `standard` means when it
is absent.** On `profile` a solution takes the profile's patterns; on `existing` **the surrounding code
wins**, and the profile proposes a shape for new work without rewriting the conventions of a codebase
that predates it. A scaffolded solution starts on `profile`, an adopted one on `existing`.

That extends to what the profile *writes*: an `.editorconfig`, a `BannedSymbols.txt` and a fitness
function arrive with the skeleton, from `sdlc scaffold` (D-76). An adopted solution gets the build
settings when its team asks — by switching their rows on in `techstack.md` and running a cycle whose
plan declares them — and not otherwise (D-77).

**What is still not done is a baseline**, which remains a non-goal (§18) for the same reason constraints
carry no baseline field (§8.3): an existing repository is not in violation on its first day, and nothing
anywhere records a violation as forgiven. A convention is nearly free to declare and expensive to apply
to code that predates it, so the cost belongs with whoever asked for it rather than with adoption — and
with the cycle that migrates the code, which is ordinary work with an intent and an approval. The opt-in
buys the declaration and the warnings it raises on old code; it buys no sweep of that code, and the
report says so.

**The framework runs the bans it ships.** It did not, for a while, and nothing could see it: the profile
shipped `BannedSymbols.txt` and this repository had no banned list at all, so the two drafters that stamp
a person's record — an approval entry and an abandonment — both read the clock from a static, in a
repository whose whole argument is that a rule nobody can enforce is one somebody reaches for at the
moment it is inconvenient. `ai-sdlc` now has its own list and its own wiring, and a fitness function
compares the two: a symbol banned in every solution the framework scaffolds and not in the framework is
a failing test naming the symbol. It compares symbols rather than files, because the reasons are written
for different readers and the framework may ban more of its own than it imposes on anybody else — the
asymmetry that is wrong is the other one.

### 11.3.1 The shape of one operation

Settled by building it. **One static holder per application operation, named for the operation**, with
everything that operation owns inside it:

```csharp
public static class ApprovalGate
{
    public static IServiceCollection AddApprovalGateRequestHandler(this IServiceCollection services) => …

    public enum GateRefusal { … }
    public sealed record Response(…);
    public sealed record Query(CycleId Id) : IRequest<Response>;

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response> { … }
}
```

A caller writes `new ApprovalGate.Query(id)` and gets back `ApprovalGate.Response`. Four things about it
are load-bearing rather than stylistic:

- **`Query` or `Command`**, so the call site says which kind of operation it is without anyone checking
  the handler.
- **The handler is `private`.** Nothing in the program can name it, so the mediator is the only way in
  and no caller can skip the behaviour pipeline. This is also why **the registration lives inside the
  holder**: a registration written anywhere else could not name a private type. Self-registration is
  part of the shape, not a preference.
- **Every handler is registered by type, with nothing captured in a closure.** That requires the
  container to be able to supply everything a handler takes — a solution root has to be a type rather
  than a `string` parameter. While it was a string, fourteen of the gate's own registrations were
  factories closing over it and two also closed over the clock, so no caller could substitute a clock
  at all.
- **The request always nests in its holder. Where the response lives depends on who reads it** —
  and there are two answers under this heading, so which one applies has to be said rather than
  inferred. **A response nests with the query that produces it** when nothing outside the process
  consumes it, and so do the records and enums that exist only to describe it. **A response moves to a
  contracts assembly when a client deserialises it**, because a nested type leaves that client with no
  name to bind to. D-38 decides the second case and names the rule a scaffolded solution's architecture
  tests enforce.

  `sdlc` itself is the first case throughout — a CLI with no wire, where `Response` nests in its holder
  and `ApprovalGate.Response` is the clearest name available. A scaffolded solution is the second: its
  `Api` and `Web` projects both need the shape, `Web` ships every referenced assembly to a browser, and
  `Every_response_that_crosses_the_wire_lives_in_the_contracts_project` fails the build of a
  solution written the way the first case teaches. A reader of either codebase should know which rule
  they are under from this paragraph rather than from a failing test.

  The remaining exception is the same in both: a response that is a shared type rather than one
  operation's payload — a core domain entity, or a result four operations produce and one renderer
  reads. Those stay top-level, because `GetBundle.Response` says less than a name that already says
  what the thing is.

**What it costs, stated rather than discovered later.** `CA1034` forbids a publicly visible nested type
and `CA1812` cannot see a handler the container instantiates by reflection, so both are suppressed in
`Directory.Build.props` with their reasons. Neither fires at `latest-recommended`, so a consumer on the
profile's own analysis level pays nothing; the cost belongs to a repository running `latest-all`.

**A nested handler cannot construct another operation's handler**, which is a forcing function rather
than a limitation: it dispatches through the mediator instead, so the behaviour pipeline applies to the
nested call where a direct construction skipped it.

**Private nested types stay PascalCase.** camelCase was tried and the compiler refused it — `CS8981`
reserves an all-lower-case type name for the language. It could not have been a convention either: the
warning fires on one word and not on two, so `handler` is refused where `ledgerShape` is accepted, and a
rule that covers some type names and not others is not a rule. Private methods and fields carry the
accessibility-in-the-name convention instead.

**Where a component lives, which is the same question one layer out.** A Blazor component is read by a
host the way a response is read by a client, and a solution may have two hosts — Blazor for web, MAUI
Blazor Hybrid for desktop and Android. So the components live in `src/<solution>.Ui`, a Razor class
library, and both hosts reference it. `Ui` references `Contracts` and nothing else, for the reason the
`Web` row exists: whatever it references is what a browser downloads. MudBlazor is referenced there and
in no other project, which is what lets a solution follow the component library its own `techstack.md`
declares — before this the architecture test forbade every way of doing it. Neither `Ui` nor `Web` is
written by the skeleton; both rows wait in the tables and cost nothing until a cycle needs a UI (D-57).

### 11.4 The analyzer policy every profile ships

**CI builds carry no warnings.**

```xml
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<WarningsNotAsErrors></WarningsNotAsErrors>
<AnalysisLevel>latest-recommended</AnalysisLevel>
<AnalysisModeSecurity>All</AnalysisModeSecurity>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<NuGetAudit>true</NuGetAudit>
<NuGetAuditMode>all</NuGetAuditMode>
<NuGetAuditLevel>moderate</NuGetAuditLevel>
```

- **`WarningsNotAsErrors` is deliberately empty**, so an exception is one visible line a reviewer
  reads.
- **Every suppression lives in `Directory.Build.props` with its reason**, never as a pragma in source.
- **`EnforceCodeStyleInBuild` makes formatting a build concern**, so a CI formatting gate never fails
  a cycle after its work is done.

**The no-pragma rule is a fitness function, not an analyzer.** It was an open question and is
answered: a test that reads every source file and fails on `#pragma warning disable` costs one method,
and a custom analyzer would have cost a project, a diagnostic id and a packaging story for the same
decision. The rule's own first run reported its own source, because a rule that names the construct it
forbids necessarily contains it — composing the token rather than exempting the file was the fix, since
an exemption would have been the one place the rule could not see.

**`EnforceCodeStyleInBuild` does not cover naming.** Naming surfaces as `IDE1006`, which is IDE-only,
so the naming section of an `.editorconfig` decides nothing at build time — nineteen fields in this
repository broke its own rule while the build reported zero warnings. Anything in that section a
profile actually wants enforced needs a fitness function beside it.

### 11.5 What a profile is

**Templates plus constraints.** Package set and versions, analyzer set, `Directory.Build.props` and
`Directory.Packages.props` fragments, project templates, pipeline templates, and its own `PRO_nn`.

**The `PRO_nn` half was empty for three iterations, and three documents said otherwise.**
`FileConstraintDocuments` took a profile path, nothing in the product ever passed it, no profile shipped
a document, and so the profile tier resolved to nothing for every solution on every profile — while
`draft profile` told a person that a different profile resolved a different set of rules, the component
dictionary said the `profile` key decided which rules resolved, and the first-cycle guide listed the
tier as though it had content. D-74 fills it with three rules and D-98 adds `PRO_04`, so
`dotnet-default` ships four rules; a profile's document is found by its name, and `ml-tensorflow` ships none deliberately. **D-76 changes what selects them**: not a
profile a solution records, but the rows of its own declared stack, one a rule, each marked `true` or
`false`. What makes the tier worth having is
§7.2's stack-neutrality clause — a global constraint may not name a language, framework or package, so a
judgement that cannot be made without a name has nowhere else to live.

**No profile carries AppHost run-mode defaults, and this listed them until F-24.** An AppHost is
conditional (§11.1) and no profile ships one, so there was nothing for a default to be a default of.
§10.2 is where the run-mode contract lives; a profile that one day ships an AppHost template would
carry defaults for it as part of that template rather than as a category of its own.

**Every profile the framework ships is carried by the binary** — D-33, as D-68 widened it. The reason is
about adoption rather than about which profile is which: a profile adoption writes has to work on a
machine with no feed configured and no network, and a restore that can fail is a first command that can
fail. That holds for `dotnet-default` and for `ml-tensorflow` equally, which is why the second one could
be added without touching the argument.

**A profile from outside would be a NuGet package — restorable, versioned, on its own cadence — and
there is no such profile.** The mechanism is recorded rather than built: there is no feed, and §11.3 says
of the private profile that nothing house-specific has been identified yet. So this is a mechanism
waiting for content, stated as one, and the first profile that genuinely wants its own release cadence is
what would build it.

The cost of embedding, named: a change to a shipped profile is a release of the binary rather than of a
package. That is the right way round. A shipped profile is what solutions start from, so it moves rarely
and in step with the gate that reads it.

**Versions are resolved at `init` and recorded, never guessed in a document:** take the latest stable
release compatible with the target framework, write it into `Directory.Packages.props`, and let the
diff record what was taken.

A profile's defaults reach a plan by being **extracted and injected**, because a session working
inside a governed solution cannot read a file that lives in the plugin.

**What a declared default is worth is whether the scaffold applies it.** `dotnet-default` declared an
api description and OpenTelemetry for two iterations and wrote neither, so every solution adopted from
it began out of conformance with its own stack file, and nothing said so — the plan rule covered only
departure by addition until `CON_15`. The scaffold now writes both: `ServiceDefaults` holds the
observability registration every host calls, and the api generates its description, renders it in
Swagger UI and sends `/` there, all three in development only. The source and meter names the kernel
publishes under are pinned rather than derived, and `ObservabilityTests` asserts the registration names
the same constant — because a span written to a source nothing collects from is discarded in silence,
and that failure has already happened once in a real codebase at the scale of a hundred and fifty-eight
handlers (D-60).

---

## 12. Hosts

Every candidate host can deny a tool call before it runs, so the enforcement primitive is portable.
What differs is vocabulary.

| Host | Blocking event | Turn end | Config | Deny shape |
|---|---|---|---|---|
| Claude Code | `PreToolUse` | `Stop` | `.claude/settings.json` or plugin | `permissionDecision: "deny"` |
| GitHub Copilot | `preToolUse` | `agentStop` | `.github/hooks/*.json` | `permissionDecision`; takes `bash` **and** `powershell` |
| OpenAI Codex CLI | `PreToolUse` | not checked | `.codex/config.toml` | `permissionDecision` + reason |
| Antigravity | `PreToolUse` | `Stop`, ungoverned | `.agents/hooks.json` or a plugin | same, plus `ask` / `force_ask` |

**The turn-end column takes three values, not two.** An event name means the framework governs the
moment. **"Not checked"** means nobody has read that host's documentation, and **is never filled in by
inference** — this table said Copilot had no turn-end event, which was never true, and the claim
travelled into the generated configuration, the command line, the guides and the readme before anybody
re-read the host's documentation (D-46). **"Ungoverned"** means the event was read and deliberately left
alone: Antigravity's `Stop` has no `stop_hook_active` equivalent, so a refusal there could loop forever,
and the command line refuses the combination by name (D-51).

**Claude Code and Copilot govern both moments; Antigravity governs the tool call only.** The first two
use the same two permission fields — nested on Claude Code, at the top level on Copilot — and send a turn
back with a top-level `decision` they both spell `block`. That agreement is a coincidence of two hosts
rather than a property of the surface. Antigravity does not frame a tool call as a permission at all, its
turn-end refusal is the inverse word (`continue` re-enters the loop; anything else lets the turn go), and
it requires a `decision` on every call including the ones the policy has no opinion about. Three shapes
from three hosts is why the two write methods stay typed switches rather than fields on a row (D-50).

**A host whose turn end cannot be guarded is not given one.** Sending a turn back is only safe where the
payload says the agent is already continuing from a previous refusal — `stop_hook_active` on the two hosts
that have it. Antigravity's `Stop` carries no equivalent, so the moment is left ungoverned and the command
line refuses it by name rather than installing a check that could loop forever (D-51). The pull-request
check is unaffected, because it depends on no host.

**v1 ships Claude Code, GitHub Copilot and Antigravity**, and what that means is a row in
`SupportedHosts` plus a conformance test fed a captured payload — not a judgement somebody makes by
reading four files (D-46). **And a host is removed when its product stops serving**, which is what
happened to Gemini CLI (D-50).

ChatGPT is not a target — Codex is OpenAI's repository-agentic surface. Antigravity plugins are the
stated successor to Gemini CLI extensions.

### 12.1 What the adapter normalises

Event names; tool names (`Edit`/`Write`/`Bash` against `edit`/`view`/`bash` against
`write_file`/`replace`/`run_shell_command`); input key names and nesting; and the deny shape. So
`sdlc hook --host <name>` reads stdin, normalises, asks the one policy engine, and emits that host's
format. Each host has a conformance test that feeds a captured real payload and asserts a deny, and the
tests are driven off the host enum rather than a list, so a host added without one is a failure rather
than an omission.

**A host with two payload shapes needs a captured payload of each.** Copilot documents a camelCase shape
for its CLI and a VS Code-compatible one, and only the second had a test — so the reader looked for
`tool_input` on a payload that writes `toolArgs`, found no path and no command, classified every call as
harmless, and allowed an agent to write the four files only a person writes. One captured payload per host
had sounded like enough; it is one per shape (D-46).

A payload the reader cannot understand **allows the call and says so on standard error.** These
surfaces are young: failing closed on a renamed field would stop a person working for a reason they
cannot act on, and failing silently would permit everything while appearing to protect. **So the reader
collects keys from every spelling any host is known to use, rather than from the row of the host that
asked** — the one piece of host vocabulary deliberately not driven by the table, because narrowing it
would turn a renamed field back into a rule that quietly stops applying.

### 12.2 How the skills reach a host

**Two plugin systems, one `skills/` directory.** The root `plugin.json` makes the package an
[Agent Plugins 1.0](https://agent-plugins.org/specification) plugin — the open packaging standard
published in August 2026 by OpenAI with AWS, Microsoft, Cursor, Vercel and Google — and
`.claude-plugin/plugin.json` makes it a Claude Code plugin. Both discover skills in `skills/` at the
package root, so there is one copy and no translation (D-47).

**That standard covers skills and MCP servers, and says so about the rest:** commands, hooks and custom
agents *"lack stable portable contracts"* and are outside v1. So nothing above changes — the hook
adapter and `SupportedHosts` are the answer to a problem no standard addresses, which is why four
unrelated projects have built the same adapter.

**What does not travel is explicit invocation.** Claude Code names a skill, `/sdlc:spec <id>`. Every
Agent Plugins client matches a request against the skill's `description` and asks before activating, so
there is no slash form and the description is the whole of how a skill gets chosen. That makes the
descriptions load-bearing off Claude Code rather than merely helpful, and it is why `§15.1`'s
preconditions are stated in each skill rather than assumed from an argument.

### 12.3 One consequence of the Copilot choice

Copilot's cloud coding agent is asynchronous and cannot conduct an interview, so the intent phase
cannot run there. v1 targets Copilot CLI and IDE chat. The cloud agent suits `build` from an
already-approved plan — later.

---

## 13. Pipelines

Generated at `init`, **per platform** — GitHub Actions or Azure Pipelines. ("Host" means the agent;
"platform" means where the code lives.)

**The Azure Pipelines template has never run against a real server** (D-32). It carries a note saying so
in the generated file, because a pipeline nobody has run looks exactly like a gate.

**On Azure Repos the pipeline runs only from a build validation branch policy.** A `pr:` trigger in YAML
is ignored there — the keyword is for GitHub and Bitbucket — so the template has `trigger: none`, no
`pr:` block, and a comment naming the policy a person must add. Until 2026-10-15 it carried a `pr:`
block, and so ran on nothing while reading as a gate: exactly the shape the note above warns about, found
by reading the platform's documentation rather than by a run. `sdlc doctor` reports the policy as
`elsewhere`, on both platforms.

Every pull request installs the `sdlc` binary, builds, tests, **displays code coverage**, and runs
`sdlc verify-chain`.

**The install is the first step and it is pinned** (D-37). Without it the templates called `sdlc` on a
runner where nothing had put it there, so the one step establishing the guarantee would have reported
command-not-found. The package is not on a public feed yet, and the generated step says so.

Coverage is displayed, not enforced — no threshold. Collected with Microsoft.Testing.Platform's own
`--coverage`, which needs no extra package: GitHub summarises it in the job summary, and Azure DevOps
publishes it to its native coverage tab with `PublishCodeCoverageResults@2`. This said Coverlet plus
ReportGenerator for a while, which was the plan before the runner carried it; the shipped choice is
simpler and the sentence was stale rather than wrong-headed.

The coverage **delta** against the target branch is **not** reported, and it is in §19 rather than
here: it needs a base-branch run to compare against, which is more than the one step this section
implies.

`verify-chain` re-establishes, from the repository rather than any agent's word, that:

- every artifact in the chain exists
- every artifact's shape is sound — the envelope, its body sections, and nothing undeclared
- the approval ledger's standing decision is `approved`
- all three recorded hashes match the bytes on disk
- every findings round's `upstream_sha256` matches its upstream now
- every spec criterion has a proof entry that cites it
- **every spec criterion is adjudicated in `verify.md`'s `Criteria`**, rather than recorded under
  `## Open` as a limit of the proof — the one exit `CON_14` does not cover, because it binds an intent,
  a spec and a plan and not a verify (§4.3)
- the outcome in `verify.md` is `pass`

**What it deliberately does not check.** The comparison of declared files against changed files.
`D-15` moved that to the hook and made it a suggestion, because `CON_07.2` exists precisely *because*
the comparison cannot see inside a file — so it was never load-bearing for scope creep, and a noisy
blocker gets routed around. What blocks instead is outcome-based: the build fails, tests fail, or the
proof is not met.

**And what it must not claim.** With local-only approval it proves the entry exists and the hashes
match. It cannot prove a second person approved, and it says so in its own output.

**A plan's pull request gets the admission check** (D-87). When a pull request changes only one cycle's
planning documents, `verify-chain` checks those documents, the approval and the hashes, and asks for no
verify — so a plan can be admitted to `development` before it is built. Anything else gets the full chain.

**Every open pull request is re-checked when another merges** (D-86). On Azure DevOps that is the build
validation policy's expiration set to *Immediately*, which re-queues on its own; it is a platform setting
and the adoption guide lays out the cheaper ones. `development` also builds on every merge, which names the
merge that broke it.

**The guarantee covers `development`** (D-88). Main's branch policy, and a release branch's, are the team's.

Keeping every check inside `sdlc verify-chain` means each pipeline is about thirty lines of YAML
wrapping one command.

### 13.1 `dice-trial`

The trial repository — cleared and re-run as often as needed, and where cycles are exercised against
the real skills. It has a GitHub remote with the trial cycle's branch pushed and a pull request open,
so the GitHub adapter has a real exercise path.

**Before clearing it the first time, keep the existing cycle.** The `20260929_4e1a` artifacts are the
best worked example of what good output looks like, and they are the calibration target for the
skills being written.

---

## 14. The experience

At the end of every stage the user sees a panel: whether the artifact's checks passed, what the
document says about itself, and the next step.

```
  spec - 20260929_1456_32                               round 2 - 1 blocking

  Says      Asking for the page returns one roll of a six-sided die, decided
            for that asking
            3 sections: Acceptance criteria, Out of scope, Open

  Checks    r1: 2 blocking - r2: 1 blocking
            F_01   CON_16      blocking
                a spec covers every outcome its intent asks for, and all of each one
                the decided delivery has no criterion at all

  Gate      not approved yet

  Next      sdlc refine 20260929_1456_32 spec        apply F_01
            sdlc interrogate 20260929_1456_32 spec   re-check
```

Four rules:

1. **The command line prints it, not the model**, so the format cannot drift between hosts. The last
   step of each stage is `sdlc status <id> --stage <artifact>`, shown verbatim.
2. **It is not called verification.** `verify` is a phase name with a specific meaning; for an
   artifact the word is "checks".
3. **It ends with the literal next command**, copy-pasteable, and a stage in it is named the way the
   host it is printed in invokes one: `/sdlc:refine …` inside Claude Code, `sdlc refine …` where the
   binary cannot tell, which the binary answers by naming the invocation (D-100). The binary's own
   commands print as commands everywhere.
4. **ASCII-safe glyphs and a no-colour mode**, because it renders in a pipeline log and in the default
   Windows console.

The `Says` line is read from the document — its first heading and its section headings. A
command-line tool cannot summarise content, and inventing one would be worse than showing the
headings a person wrote.

**A citation is shown as its id and the rule's own line, never the id alone** (D-63). This panel is
where somebody meets a blocker when deciding what to do about it, and an id with no words beside it says
a rule was broken without saying which.

**A `Loop` row appears only when the oscillation counter has something to say** — a rule answered and
raised again, with what it is caught against (D-65). It is absent above because nothing has relapsed,
which is the ordinary case.

---

## 15. Skills

| Skill | Does |
|---|---|
| `init` | adopts the framework into a repository — §9 |
| `intent` | interviews; classifies the change; writes `intent.md`; runs the loop |
| `spec` | derives testable criteria from the intent; writes `spec.md`; runs the loop |
| `plan` | reads the spec and the existing code; writes `plan.md`; runs the loop |
| `steer` | re-runs the chain from an existing intent and spec — §3.2 |
| `interrogate` | judges one artifact against the constraints in force; writes a round; never edits |
| `refine` | applies an artifact's findings to that artifact |
| `remediate` | applies a blocked verify's findings to source and tests |
| `build` | implements the plan; builds; tests; records evidence |
| `verify` | adjudicates the build against the spec from evidence alone |
| `batch-build` | completes the queued cycles, each on its own branch and independently, and reports the sitrep — §10.1, D-86 |
| `resolve-conflict` | brings a cycle's pull request up to date with the integration branch, inside its declared files, ending in a fresh verify — D-86 |
| `pr` | pushes the cycle's branch, opens the pull request into the integration branch, links the work item; `--plan` admits an approved plan, `--draft` a build that did not pass — D-85 |
| `draft` | prints a protected file's entry for a person to place — §5.3 |
| `propose` | reads the whole history and proposes `LOC_` entries, with the evidence — D-69 |
| `status` | prints the stage panel, and the state of a cycle or of all cycles |
| `constraints` | renders the effective constraint set, all tiers |
| `suggestions` | lists accumulated non-blocking findings |

**Six of them keep a task list where the host has one.** `init`, `intent`, `spec`, `plan` and `build`
each list their own steps and nothing past the hand-back; `batch-build` lists one line per cycle
(D-80).

**Three documents list these, and the suite holds all three against disk.** This inventory,
[`skills/README.md`](skills/README.md) — which is the skills' own surface, with a row and a section
each — and the summary in [`docs/README.md`](docs/README.md). A skill added to one and not the others
reads as undocumented to the next session, which then works from recollection, so the agreement is
mechanical rather than promised.

### 15.1 Preconditions

| Skill | Requires | Refuses when | Writes |
|---|---|---|---|
| `init` | a git repository | not a repository | the scaffolding, per mode |
| `intent` | nothing | an intent exists, or the id it was handed is already claimed | `intent.md`, into the directory `sdlc new` opened |
| `spec` | a clean intent | intent missing or not clean; a spec exists | `spec.md` |
| `plan` | a clean spec | spec missing or not clean; a plan exists | `plan.md` |
| `interrogate` | the artifact exists | it does not | a findings round |
| `refine` | a round with open blockers | no blockers; cycle approved | that artifact only |
| `steer` | an intent **and** a spec | only an intent; approved without a reopen | intent, spec, plan — whichever exist |
| `draft approval` | three documents, each with a round | any missing, uninterrogated, holding an open blocker, or judged against an upstream that has changed | nothing; it prints |
| `build` | the gate satisfied | the gate refuses | code, evidence |
| `verify` | evidence exists | no evidence | `verify.md` |
| `remediate` | verify blocked | not blocked; the loop has regressed or stalled (D-66) | code and tests only |
| `pr` | verify passed; with `--plan`, the gate satisfied; with `--draft`, verify blocked or escalated | not passed; escalated — except as `--draft`; the chain broken | push, pull request |
| `batch-build` | a queued cycle (§10.1) | nothing; a cycle held back by a branch on the remote is not built, and the sitrep says why | code, evidence and a verify document per cycle, each on its own branch. Never pushes without `--pr` (D-22) |
| `status` | nothing; a cycle id for the per-cycle form | the cycle does not exist | nothing; it prints |
| `constraints` | nothing | nothing | nothing; it prints |
| `suggestions` | nothing | nothing | nothing; it prints, and keeps a copy under `.sdlc/reports/` (D-69) |
| `resolve-conflict` | a cycle whose pull request conflicts with the integration branch or went red after another merge | a resolution needing a file the plan never declared — a change of plan, handed back; or two settings carry one key with different values | the cycle's declared files, its evidence and its verify (D-86) |
| `propose` | nothing | nothing; an unreadable rule file, decision record or theme is reported rather than worked around | nothing that binds; it prints, and keeps a copy under `.sdlc/reports/` (D-69) |

**Where a skill requires nothing, the row says so rather than being left out.** An absent row and a row
saying `nothing` are not the same claim, and the first is what lets a skill quietly check the wrong
precondition — which is the whole of D-20. It is the same rule `## Open` follows when it is empty (§4.1),
that `Rejected` follows with "nothing" as a real answer (§20), and that D-64 settled for `conflicts`.

**`propose` is one row and not three.** Its subjects read different corpora — findings rounds for a
rule, approved cycles for a decision, the theme for a theme — and none of them refuses on an empty one.
A solution with no history, no approved cycle or no theme gets an empty report or a skeleton, which is
D-69's statelessness rather than a precondition: nothing records what has been absorbed, so there is
nothing for a missing corpus to invalidate.

**A stage skill whose artifact already exists refuses and names what you probably meant** —
`interrogate` to judge it, or `steer` to change it (D-18).

### 15.2 What the binary does, and what a skill does

The command line is the mechanism (D-26), so the split matters: a **command** is deterministic and the
binary owns it; a **skill** needs language and an agent runs it. A skill calls commands and shows what
they print verbatim.

| Command | Does |
|---|---|
| `init` | adopts the framework — three modes, §9. Human-invoked; the hook denies an agent running it |
| `new` | opens a cycle: allocates the id, creates its directory, and **claims** the id with a marker the filesystem refuses to create twice — the claim is the collision check, not the directory (D-10) |
| `constraints [--for <artifact>]` | renders the effective set, all tiers. How the rules reach a session |
| `status <id> [--stage] [--brief]` | the stage panel — §14. `--brief` is the between-stages form: the counts, what the artifact says, and the next command |
| `findings <id> [--stage] [--round]` | what the interrogations said about one cycle, per artifact, each finding at the disposition that stands |
| `round <id> <artifact> --from <file>` | writes the next findings round: the readers' findings and the session's dispositions, with the round number, the time, and the hashes of what was judged, what it was judged against, each section, and on a verify the evidence, stamped by the binary. Refuses while anything above it in the chain is unsettled. `--changed` lists the sections moved since the last round instead — D-101 |
| `gate <id>` | whether a cycle's build may run. Exits 1 if not |
| `draft approval\|abandon\|profile\|solution\|techstack` | prints or writes what belongs in a file only a person writes — §5.3 |
| `propose constraints` | what the history says the rules are missing, with the cycles and finding ids behind each — D-69 |
| `evidence <id>` | records the diff, its base commit, and the normalised test summary — §4.5 |
| `cycles [--stage\|--approved\|--verified]` | every cycle this solution holds, newest first, with what each is about and where it stands |
| `cycles --claims` | which cycles may be built together, and what collides — §10.1 |
| `cycles --pr-ready` | every cycle, saying which may open a pull request |
| `branch <id> <kind>` | the one git command that puts the working copy on the branch a kind of work happens on — `plan`, `build`, `build-unattended`, `forward` — from the right base (D-85) |
| `factory` | writes the factory's pipeline for the recorded platform: nightly `batch-build --pr --unattended` against the integration branch, one run at a time, inside a time limit, proving the gate refuses before it builds (D-84) |
| `lead show <reference>` | a lead — `azuredevops:`, `github:`, `suggestion:` or a path — fetched and printed in one shape, its outside text fenced as untrusted, for `intent --from` (D-89) |
| `operate [--since <hours>h] [--dry-run]` | Operate: production's errors from the declared telemetry store, grouped by signature, scrubbed of personal data, deduplicated against what is filed, and filed as bugs on the platform's tracker (D-89) |
| `perspective show <name> --for <stage> [--cover]` | one perspective for one stage: its method and the questions a fresh reader asks, or with `--cover` the author's short list (D-90) |
| `perspectives <id>` | which perspectives a cycle gets and why — always on, named by the intent, or brought by the plan's declared files (D-90) |
| `sitrep` | the morning's page: what is ready to merge and in what order, what needs a person, what was held back, what is waiting and going stale (D-84, D-86) |
| `cycles --queued` | what a build would take now, and every cycle held back with why — a `cycle/<id>/...` branch on the remote, or an approval the gate no longer accepts (D-85) |
| `suggestions [<id>]` | the open non-blocking findings, across cycles |
| `decisions [--brief\|--all]` | the choices this solution has settled, so a cycle reads what was argued rather than arguing it again — D-70 |
| `theme` | the roles this solution's surfaces use and the values that fill them — D-71 |
| `stack` | what the solution declares it is built from, and which build settings files it takes that are not on disk. Names the gap and closes none — D-77 |
| `propose decisions` | what an approved cycle stated, handed over for a person to record — D-70 |
| `propose theme` | the roles a theme declares, or the ones left empty in the one there is — D-71 |
| `pr-ready <id>` | whether one cycle may open a pull request, without opening one |
| `verify-chain <id> [--base <branch>]` | the whole chain, as the pull-request check runs it — §13. With the target branch, a pull request carrying only the cycle's planning documents gets the admission check (D-87) |
| `admission <id> --base <branch>` | `true` when a pull request carries only the cycle's planning documents, so a pipeline skips the build and the tests; `false` otherwise. Always exits 0 (D-87) |
| `scaffold <id>` | writes the solution skeleton into an approved cycle — D-35 — or, on an adopted solution, only the build settings files that cycle's plan declares — D-77 |
| `hook --host <name>` | judges one tool call — §12 |
| `version` | the three versions and which is which — the package, the plugin, and the artifact envelope |
| `doctor` | checks this install and this repository, and says which half is missing. Exits 1 if anything is wrong — a missing integration branch and a placeholder paragraph included. Before the first cycle it ends with every draft left to place and its rename — §9 |

**`doctor` reports and never repairs**, and the third of its three outcomes is the point: what a binary
cannot see — whether a host loaded the skills, whether a pipeline step is a *required* check — is
marked `elsewhere` and names who to ask, rather than being
counted as sound. A command that said an install worked when nobody had established it is the failure
this document opens by naming.

**Typing a stage name is answered rather than refused.** The panel ends with the literal next command,
and for a stage that is a skill — `sdlc spec <id>` — the binary cannot do the work. So it says what the
stage is and names the invocation. A tool must never send somebody somewhere it does not go: the
alternative is the most-read output in the plugin pointing at "Unknown command", and a person concluding
the install is broken.

**`pr-ready` and `pr` are deliberately two things.** The refusal has to be identical whether one cycle
opens one or eight, so the binary owns it; pushing and writing a pull-request body needs the platform's
own tool and a sentence somebody wrote, so the skill owns that.

`intent` opens the cycle as its first act by running `sdlc new`, rather than allocating an id itself.
That command creates the directory and then **claims** the id with a `.opened` marker opened so that the
filesystem refuses a second one — which is the collision check, and is atomic. Creating the directory is
not: it succeeds on one that is already there (D-10). The skill then switches to the plan branch
`sdlc branch` names; a person runs neither. **`sdlc new` refuses while anything git does not ignore is
uncommitted**, the framework's own ignore files under `.sdlc/` aside, because the switch would carry it
into the cycle as work no plan declared (D-94).

---

## 16. Layout

```
.sdlc/
  solution.md               origin, standard, platform, integration branch, and a paragraph -
                            drafted by init, placed by a person
  .gitignore                the drafts beside the files a person places
  techstack.md              what this solution is built from
  constraints.md            this solution's own LOC_nn, hand-edited
  decisions.md              its own DEC_nn: the choices it has settled, hand-edited (D-70)
  theme.json              the roles its surfaces use and the values that fill them (D-71)
  README.md                 terminology, flow and commands
  reports/                  what suggestions, propose and `draft techstack` printed last, kept
                            because a person works through one of these rather than reading it
                            once. Ignored by a .gitignore the directory carries itself, and read
                            back by nothing -- which is the whole difference between a report and a
                            record (D-69). The stack options menu is one of these: it outlives the
                            rename it was written for, so a copy anywhere else would reach a later
                            cycle's diff as a file no plan declared (D-75)
  cycles/
    .gitignore              ignores every cycle's .opened, and keeps itself tracked. Written by
                            `sdlc new` when the first cycle is opened, so an existing solution
                            gets it without an adoption re-run (D-10, the shape D-69 settled)
    20260929_1456_32/
      .opened               the marker that claims this id. Created so that the filesystem refuses
                            a second one, which **is** the collision check -- creating the
                            directory is not, because that succeeds on one already there. Nothing
                            reads its contents; its existence is the whole of the information, and
                            it is local state rather than history, so it is not committed (D-10)
      intent.md
      spec.md
      plan.md
      verify.md
      approval.md           append-only ledger of gate decisions
      abandoned.md          why this cycle stopped, if it did
      findings/
        intent.r1.json
        spec.r1.json
        plan.r1.json
      evidence/
        diff.patch
        build.log
        tests.trx           the runner's own output, under a fixed name. A run that wrote
                            several results files keeps them as tests.1.trx, tests.2.trx,
                            because one of several cannot be the one
        tests.json          the normalised summary verify reads, merged from every one of them
        base.sha
```

A cycle is one governed change: an id, a directory, and a `cycle/<id>` branch. Directories appear
when the skill that writes the first file into them runs; nothing is created speculatively, ahead of
the step that owns it.

**Cycle ids are `<date 8>_<time 4>_<seconds 2>`** — `20260929_1456_32`. Purely temporal, so they sort
chronologically and say when the cycle opened. Allocation needs no coordination and works offline.
The id is load-bearing in the branch name, the directory path, and every cross-reference, so it is
fixed at allocation and never rewritten.

Artifacts live on the cycle branch. `batch-build` reads approved cycles locally and completes them
before anything is pushed, so the gate is passed before code exists.

Finding work **is** a directory listing: `CycleIndex` globs `.sdlc/cycles/`, deliberately, because
*"a manifest is a second place the truth lives, and the one that goes stale is always the one a tool
trusts."* Nothing records a branch or a work item — a branch is derived from the id, and no cycle has
needed a work item yet. Resumable state for a long `batch-build` run is genuinely absent and is in
§19.

### 16.1 Abandoning a cycle

An abandoned cycle **keeps its directory** and records why in `abandoned.md`. The reason is the only
thing the cycle produced, and a repository holding the intents that were tried and dropped — with the
objections that killed them — is readable history.

It follows the draft pattern: a skill prints the entry, a person places it, and the hook denies every
agent write. The reason is history rather than safety — an agent stuck on a blocking finding has an
obvious escape in abandoning and starting clean, and a hand-written reason is what keeps the
objections on the record.

**Abandonment is terminal.** A cycle with an `abandoned.md` never builds, even if it also holds an
approval.

---

## 17. What iteration 3 proved, and what it did not

**Exercised by a real cycle:** `init`, `intent`, `interrogate` (six rounds across three artifacts),
`spec`, `plan`, `build`, `verify`, and the diff-to-plan comparison. `CON_07` raised two real findings
on a real diff.

**Never run by any cycle or test:** `remediate`, `run`, `suggestions`.

**Never reached, so never tested:** `escalated`. The bounded-remediation state machine exists
entirely on paper, and the check has never refused a cycle. Iteration 3 called this its largest
untested claim, and **`batch-build` depends on it completely** — unattended building is only safe
because a cycle that cannot be made to pass stops instead of opening a pull request.

**A check that tested nothing while appearing to run:** the nightly job asserting interrogation
verdicts needed an API key that was never set, so it warned and exited cleanly. §2.1's failure mode
inside the plugin's own CI.

**Unconfirmed:** the `CON_06` flake fix, made by reading rather than by re-running.

**The implication for sequencing.** Before `batch-build` is built on bounded remediation, drive one
cycle to `escalated` on purpose — the cheapest route is a deliberately inadequate proof, which should
block, then remediate, then exhaust the budget.

---

## 18. Non-goals

- **Enforcing a coverage percentage.** Coverage is displayed.
- **Making local approval unforgeable.** It cannot be; the pull request is where accountability lives.
- **Governing ai-sdlc with ai-sdlc** — §2.7.
- ~~**Resolving merge conflicts as the primary strategy.**~~ Reversed by D-86: collisions are resolved at
  merge, by a person, inside the cycle's declared files.
- **Remote plan approval** — the local paste is the gate. A plan's pull request into `development`
  admits it to the factory and approves nothing (D-83).
- ~~**A shared batch queue.**~~ Reversed by D-83: the integration branch is one.
- **Tooling for splitting work** — D-25.
- **A combined design skill** — D-16.
- **Waiving a blocking finding** — D-27.
- **Supporting ChatGPT as a host.**
- **Plans detailed to the line.**
- **A device end-to-end layer** — §11.2.
- **Baselining existing problems at adoption.** Taking the build settings into an existing codebase is
  not this: the cycle that brings them records nothing as forgiven, and what they break is that cycle's
  to fix or to narrow in the file (D-56, D-77).
- **Migrating existing code as part of adoption.** `standard` declares what new work follows; a cycle
  moves the code.

---

## 19. Open questions

**A candidate here never carries an id.** D-69 settled this for the rules `propose constraints`
reports and the reasoning is the same wherever a rule is waiting: *"§7.4 makes a gap the record of a
constraint that was **removed**. A proposal nobody took would be indistinguishable from a rule somebody
retired."* An id on a candidate is worse than a gap, because it can also be issued to something else
while the candidate waits — which is what happened to item 2 below. So a held rule is described, the
next free id is looked up when somebody adopts it, and nothing here reserves a number.

1. **Which `PRO_nn` conventions become analyzers?** Narrowed from three to two, and the blocker is
   gone: the kernel has been read and it ships no analyzers (§11.3). **Mediator dispatch no longer
   needs one** — a handler is private inside its holder (§11.3.1), so nothing can reach past the
   mediator and encapsulation decides it for free. What remains is one-folder-per-feature, which is
   almost entirely mechanical, and results-not-exceptions, whose decidable half is a handler's return
   type and whose judgement half is whether an operation can fail at all. The question is now narrower
   than "analyzer or not": a fitness function over the assemblies decides the mechanical part of both
   with no analyzer project, no diagnostic ids and no packaging — which is what the no-pragma rule
   turned out to want when it was built (§11.4).
2. **A blast-radius constraint, held and unnumbered.** *A plan's blast radius matches what it
   changes* — held until `tier` is in use, so the constraint would describe the rule rather than its
   first example. **Deliberately carries no id**, by the rule above. It held `CON_15` for a while,
   which D-59 then issued to the declared-default rule — two rules under one number, which §7.4
   forbids outright and which no later edit can repair once a finding has cited the ambiguous id.

   **Before anybody adopts it, it has to be reconciled with D-15, which appears to foreclose it.**
   That entry demoted the declared-versus-changed comparison from a gate to a signal and said what
   replaces it: *"build fails, tests fail, or the proof is not met. Outcome-based rather than
   process-based."* A rule requiring a plan's blast radius to match what it changes is that
   comparison promoted back to a process-based blocker. There is a second problem with the wording:
   a constraint judges a plan **at approval**, when there is no change to compare the declared set
   against — and the comparison after the fact is `CON_07.2`, which already binds. So this is either
   a reversal of D-15, which §20 says needs a new id and an argument, or it is already covered. It is
   recorded here rather than dropped because deciding which is a person's call; what is not open is
   that it may not quietly become a rule.
3. **The cross-cycle learning loop, half closed.** v02 built `retro` and `absorb-retro` with a
   corpus, an importer that refuses to overwrite differing content, and signals computed read-only;
   v03 removed them and recorded the cost — *"nothing aggregates automatically."* `propose
   constraints` closes the half that pays for itself (D-69): the findings rounds are aggregated
   across cycles, and a rule cited in three distinct cycles is reported with its evidence. The
   completion-certificate trap is answered by being stateless — nothing records which cycles have
   been absorbed, so there is no artifact whose existence means `done`.

   **Two of the three carried-forward items are still open, and one is now narrower.** `caused-by` /
   `caused-at-stage` is how the process would learn **which stage leaks**, and nothing records it:
   this reports which *rule* recurs, which is a different question. The private/public boundary is
   untouched and stays untouched while nothing leaves the solution — a report is local and read by
   nothing, so no corpus exists to have a boundary. What is newly visible is that **a decision cannot
   be mined**: a colour, a layout convention or a code-style choice never collides with a constraint,
   so it produces no finding and no amount of history review will surface it. Constraints are mined
   backward from findings; decisions would have to be captured forward at the moment of choice, from
   an intent's `Fixed decisions` and a plan's `Shape and departures`. That is a second mechanism and
   it is not built.
4. **The coverage delta against the target branch.** §13 described this as taken and nothing reported
   it. It is cheap only in the sense that it breaks no rule: a delta needs the target branch's own
   coverage to compare against, so it is a second run or a stored artifact rather than one more step
   in the pull-request workflow. Worth doing when somebody wants it; dishonest to describe as shipped.
5. **Resumable `batch-build` state.** §10.1 said a local cycle index persists state so a run can be
   resumed, and §16 said a cycle index exists so `batch-build` does not glob directories. Neither is
   built, and the second is the opposite of what `CycleIndex` does deliberately — it globs, because *"a
   manifest is a second place the truth lives, and the one that goes stale is always the one a tool
   trusts."* Finding work is a directory listing and should stay one. What is genuinely absent is
   resume: a long batch run that dies halfway has nothing recording which cycles completed. That wants
   a run journal rather than an index of cycles, which is a different thing in a different place.
6. **An intent's bounds are read by nothing.** A fixed decision like *"keep it very simple — nothing
   fancy"* is recorded with attribution, survives every gate, and no stage acts on it. That is half of
   what the dice-roller cycle got wrong: a project-reference rule produced roughly 3,100 lines across
   nine projects for one button, and `CON_18` now makes a plan argue about a rule it changes — but
   nothing asks whether a shape is proportionate to what was actually wanted. The hard part is that
   proportion has no bright line, so a constraint about it would fire inconsistently; the narrower
   version worth considering first is having the plan stage show the bound beside the shape it chose,
   and leave the judgement to the person at the gate. Open because the mechanism is undecided, not
   because the gap is in doubt.
7. **A frozen citation heading can drift from the library, and nothing checks.** D-63 freezes the rule's
   own line into a findings round deliberately, so a cycle read months later shows the wording it was
   judged under. The cost is that a reader told to copy the line verbatim could paraphrase it, and
   nothing would notice. There is no home for the check on the write path, because no command writes a
   round — the interrogate skill does. `sdlc doctor` is where it belongs: it already reports and never
   repairs, and a heading that no longer matches the constraint it cites is exactly the quiet kind of
   wrong it exists to surface.
8. **Where an adopted codebase's first rules come from.** `propose constraints` mines cycle history
   (D-69), and a repository adopted with existing code has none — so on the day it most wants rules it
   gets the empty form. Three paths, none chosen. *The interview that already exists*: §9 has `init` ask
   what matters and propose a `LOC_` set from the answers, which is the only honest source on day one,
   and the cheap reconciliation is for the file to land empty **unless** the interview produced
   something. *The profile's rules as a menu*: an adopted solution runs `standard: existing`, so `PRO_`
   conventions do not apply to it, and "which of these do you want as `LOC_` for new work" beats a blank
   page and costs nothing — every item was already argued by somebody. *Evidence-sourced questions,
   never evidence-sourced rules*: §9 rightly refuses to read the repository to decide what to forbid,
   but reading it to ask a better question is a different act — *"there are forty suppressions of CA1062
   here; do you want a rule about null-checking at boundaries?"* leaves the person deciding, which is the
   line §9 actually draws. Open because the choice is between three defensible shapes rather than
   because the gap is in doubt.
9. **Whether a theme is per solution or shared across them.** D-71 ships it per solution, at
   `.sdlc/theme.json`. A design system shared by several solutions is the obvious case for the other
   answer, and it is a profile question rather than a solution one: a profile already carries the
   conventions new work follows, and a shared register would sit beside them. Nothing blocks it; no
   solution has wanted it yet.

10. **Nothing judges a verify document's open region — closed, as a chain check.** `CON_14` binds an
    intent, a spec and a plan, so a verify could record a criterion as a limit of the proof rather than
    adjudicate it unmet and still hold the chain. §7.2 refuses a constraint for a question a parser
    decides, so it became a `verify-chain` check: every spec criterion is adjudicated in `Criteria`
    (§13). Exercised against a real cycle's five-criterion spec in both directions. What remains open
    is narrower and belongs to `CON_14` rather than here: the region is still unjudged on a verify for
    anything that is **not** a criterion, and whether that wants a rule is undecided — nobody has seen
    it abused.

---

## 20. Decisions

Ids are a monotonic sequence of their own, separate from `CON_nn`: a constraint judges an artifact, a
decision explains the framework. Assigned once, never reused. **A reversed decision is superseded by
a new id rather than edited**, so a commit citing `D-14` still means what it meant. **An entry whose
reasoning stands while a fact inside it has gone stale is corrected and dated instead** — a different
act, and D-73 says which is which and why confusing them puts a live decision out of force.

**This is reference material, not reading.** Most of this document, so the way in is a lookup rather
than the top. The count is deliberately not stated here — it drifts every time an entry is added, which
is why the readme does not state it either; D-44 carries the proportion and what it costs. `AGENTS.md` carries the two commands; in short, a section's
trailing `(D-nn)` marker answers "what constrains this", and an entry's `Governs` answers the reverse.
**Read `Rejected` first when proposing something** — it is where the alternative you are reaching for
has usually already been weighed, which is the whole reason the field exists.

**The record shape, which a test holds.** Every entry carries `Governs`, `Why` and `Rejected`. The
**heading is the decision** — `**Decision.**` is an optional elaboration for the entries whose heading
cannot carry it, and restating a heading under it would be a line of nothing. `Rejected` is half the
value: in six months the question is not "why this" but "why not that".

**`Rejected` is never omitted, and "nothing" is a real answer.** An absent field cannot be told apart
from an unwritten one, so an entry that genuinely foreclosed nothing says so and says why it is here
anyway. That makes the test below mechanical rather than a judgement, and it is how the two entries
that foreclose nothing — D-12 and D-20 — are visible as what they are instead of looking unfinished.

**Why `Governs` is required.** It went missing from every entry once, and the appendix became
something you could only enter from the front — which is most of what makes a long one feel
unmanageable, and why a reader who had a `D-nn` and a question had to read until they found it.

**What earns an id: foreclosing an alternative somebody would otherwise reach for.** An entry whose
`Rejected` is empty is not a decision, it is a section of §1–18 in the wrong place — the sections
state what is true, and this explains why the alternatives are not. The test is deliberately about
the alternative rather than the importance: a small choice that closes a road worth closing belongs
here, and a large truth nobody would argue with does not.

**And what does not earn one: the consequence of a decision already recorded.** Removing something a
previous entry had already made vestigial is that entry taking effect, not a new decision; it goes in
the commit message and in whichever section described the thing. An id restating another id's
reasoning is a second place the truth lives, and the one that goes stale is the one somebody trusts.

The full working-through — what the previous draft said, how each contradiction was found, and the
code each decision changes — is in commit `adf15cc`.

### D-01 — Side research and evaluation run in a new process

**Governs:** §2.5

**Why.** Research returns an answer and should not leave its search behind. An evaluation must not
have seen the thing it judges being made.

**Rejected.** Subagents for interrogation: better ergonomics on one host, two code paths for the most
important skill. In-process self-judgement in the healing loop: removes the only thing independence
buys.

### D-02 — Constraints bind an artifact's body; its open region is the compliant exit

**Governs:** §5.4

**Why.** `CON_02` forbids an intent *stating as settled fact* a figure nobody measured — so an item
recorded as an open question satisfies it rather than overriding it. `CON_04` forbids leaving an
intent's *stated outcomes* uncovered, and an open question is not a stated outcome. So clean-to-advance
**stays**: an unresolvable item moves to the open region, and that is the fix.

**Rejected.** Advancing on a round merely existing; a pre-gate acceptance record, which is a second
gate under another name; requiring a clean round only of the spec and plan.

### D-03 — An open item must be one nobody can resolve yet

**Governs:** §4.3, §7.5

**Why.** `D-02` makes the open region a legitimate exit, which makes it an exploitable one: an agent
can empty the body into it and satisfy every constraint by asserting nothing. `CON_14` is the only
control, and no hook can decide it.

**Rejected.** Capping the number of open items — a number nobody chose. Leaving it ungoverned — a
person reading a body with nothing in it has nothing to object to.

### D-04 — The plan gate is about dependency, not the absence of open items

**Governs:** §5.4

**Why.** A hard no-open-items rule would have blocked the trial cycle, whose plan carried limits
citing two open intent questions, was approved, built, and shipped working software. False positives
on a gate are what make a team route around it.

**Rejected.** "No blockers *or* open items at plan": blocks cycles that are fine, for a property the
existing constraints already deliver through `D-05`.

### D-05 — The spec disposes of every intent open item

**Governs:** §5.4

**Why.** The check it replaces — every intent statement maps to a criterion — cannot exist, because
`CON_04` is in the library only on the grounds that no hook can decide coverage. Disposal is a
different question: *presence*, which a parser decides.

**Rejected.** Keeping the coverage check and removing `CON_04`; keeping both, which `CON_11` exists to
prevent.

### D-06 — An artifact section earns its place by having a consumer

**Governs:** §4.2

**Why.** Decoration costs attention at every interrogation. Same test the library applies to itself,
pointed at structure.

**Applied.** `Who` dropped — no consumer of any kind, and `Problem` now carries who has it. `Order`
folded into `Files`, with the ordering *reasoning* preserved there rather than lost. `Risk` kept — the
person at the gate is a real consumer. `Done looks like` renamed `Wanted outcomes`, because it is
`CON_04`'s left-hand side and cannot move to the spec, but the old name read as a spec concern.

**Rejected.** Dropping `Risk` as unused. Giving `Risk` a constraint, which would be admissible and is
deferred until the practice settles.

### D-07, D-07a — An artifact is an envelope carrying a body

**Governs:** §4.1

**Why.** It makes the governance rule one sentence, gives a durable test for where a future field
goes, and puts the seam in the code where an envelope reader can be artifact-agnostic. It also
removes a duplication the schemas would carry: iteration 3 had four schemas each restating the common
frontmatter, which is `CON_09` pointed at schemas.

**The audit that corrected it.** The first version named "id, upstream reference, framework version"
— which described no artifact that existed. The trial's common part was a cycle id under two
different names. The upstream *reference* is dropped: not universal, since the intent has none, and
derivable, since the chain order is fixed.

**`type` and `tier` go in the body**, and that killed a proposed `metadata` bag — both candidates for
it turned out to be *claims* rather than bookkeeping, and a claim in the envelope escapes the
constraints. Three further reasons against a bag: it defeats the schema, which v03 set
`additionalProperties: false` on deliberately; it institutionalises what `D-06` removes; and "custom"
names no owner.

**Deferred, with its shape recorded:** a team will want to stamp a ticket number on every artifact,
and the answer is a **declared extension** — the solution declares which extra keys it carries and
the schema validates against the declaration. Same pattern as `LOC_nn`. Not built: a mechanism with
no user is how a bag gets built badly later.

**Rejected.** A `metadata` bag for per-solution fields: it defeats the schema's closed envelope,
institutionalises what D-06 removes, names no owner, and both candidates for it turned out to be
claims rather than bookkeeping — a claim in the envelope escapes the constraints. An upstream
reference in the envelope: not universal, since the intent has none, and derivable, since the chain
order is fixed. `type` and `tier` in the envelope rather than the body, for the same reason as the
bag. A declared extension is **deferred rather than rejected**: a solution declaring which extra keys
it carries, validated against the declaration, is the right shape for the ticket number a team will
eventually want, and a mechanism with no user is how a bag gets built badly later.

**The risk to watch.** Naming a pattern invites stretching it, into an envelope carrying fields that
are uniform only because somebody forced them. The test is the guard, not the pattern's gravity.

### D-08, D-08a — The open region is `## Open`, on every artifact

**Governs:** §4.3

**Why.** A three-name mapping is free for the gate and not free for the interrogation skill, which is
a model reading a prompt. Its failure has two directions: a finding raised about something correctly
recorded as open (annoying, self-correcting), or **a section in the body going unjudged** (silent, and
the exact failure the design prevents). And the three names bought less than they appeared to — the
information is in the entries, which already declare their own kind.

**Kept as `Open`** after exploring alternatives. `Cannot settle` and `Unsettled` resist backlog
content better, because they force the author to assert impossibility — the trick that made v03's
`Cannot prove` a good name. `Open` was kept for brevity, and the cost is accepted: `CON_14` does the
work the name does not.

**Rejected.** `Open questions` — two of three entry kinds are not questions. `Unresolved` — implies it
could have been resolved. `Deferred` — implies a later, which evidence limits do not have. `Limits` —
collides with `Must not change` and with "constraints".

### D-09 — `solution.md` is protected, and a profile change is drafted

**Governs:** §5.3

**Superseded in part by D-76.** No solution records a profile, so there is no profile change to draft
and `draft profile` is gone. What this entry is about — the file is protected whole, and a tool drafts
what belongs in it — stands, and `draft solution` (D-62) is the drafter.

**Why.** `origin` is a historical fact that must never change and `profile` is a choice revisited
rarely, so protecting both costs one manual line per migration. The alternatives either expose
`origin` or introduce the framework's **first conditional hook rule**, which is where a bypass would
live. Every hook rule staying absolute means the whole rule set states in one sentence.

**And it completes a pattern.** The protected files follow one rule — a tool never writes them, a tool
drafts what to put in them — so it is **one verb, not a command each**. **Corrected 2026-10-09:** this
said five files and four commands, which was the set when it was written; D-67 and D-70 made it six
files and five drafting commands, and two of the six are proposed rather than drafted (D-69, D-70).
§5.3 owns both counts. The pattern is what this entry is about and it is unchanged by the arithmetic.

**Rejected.** Per-key protection: the hook sees a path, never a before-and-after. Leaving it
unprotected: makes `origin` agent-rewritable. Splitting into two files.

### D-10 — Opening a cycle claims its id, and the filesystem decides the collision

**Governs:** §15.2, §16

**Why.** The claim that an existing directory catches a collision was false, because at allocation
there was no directory. Claiming the id as it is allocated **is** the check, and the filesystem makes
that atomic.

**Corrected 2026-10-09.** The decision stands and so does its reasoning; two facts in it did not.

*The mechanism.* This entry said *"Creating it **is** the check"*, and creating a directory cannot be:
`Directory.CreateDirectory` succeeds on a directory that is already there, so it has no failure to
collide on. The claim is a `.opened` marker opened with `FileMode.CreateNew`, which throws when the file
exists — one atomic operation the filesystem decides, which is what this decision always asked for. The
directory is created first and harmlessly; an existing one is not the collision, the claim is.

*The component.* The heading said the **intent skill** creates the directory. `sdlc new` does, and the
intent skill runs it (§15.2). The heading is corrected because §20 makes the heading the decision, and a
heading naming a mechanism that cannot work is the worst place in the entry for it to sit.

The `Rejected` list gained the option that was actually taken and had to be undone — see below. §16 owns
the marker and is now named in `Governs`.

**Rejected.** Deleting the claim, which leaves a collision to surface as a confusing merge. Checking
without creating, which leaves a race. **Creating the directory and treating that as the claim** —
which is what shipped for a while and is equally racy by a different route: it cannot fail on an
existing directory, so two cycles opened in the same second both proceed and write into one. That it
read as satisfying this entry is the reason the correction above exists.

### D-11 — A refusal is drafted too

**Governs:** §5.3

**Why.** Rejecting is what a person does when they are least inclined to look up a format. Giving help
to the easy case and withholding it from the hard one is the asymmetry people resent.

**Rejected.** Leaving refusals hand-written. Dropping `denied` and keeping two states, which loses the
difference between "fix this and come back" and "no, not this way".

### D-12 — `SolutionFileName` becomes `SolutionConstraintsFileName`

**Governs:** §11.3

**Why.** The constant meant *the solution's constraints file* and read as *the solution file*, and
`D-09` puts `solution.md` in the same code path — so the next person picks the wrong constant out of
autocomplete and the hook guards the wrong file.

**Rejected.** Nothing — a rename with one correct answer, recorded because the collision it avoids is
invisible once the name is right, and a future reader shortening it back would reintroduce it.

### D-13 — Evidence is normalised at the boundary

**Governs:** §4.5

**Superseded in part by D-92.** The original output is still kept beside the summary while a build runs;
it is no longer committed. The pipeline publishes it.

**Why.** **The gate must not know about test platforms.** Reading evidence natively would put a `.trx`
parser inside the gate, then a JUnit parser beside it, and the thing that owns *process* constraints
starts owning *file formats*. Normalising in the build step puts the stack-specific part where the
stack already is. Keeping the original alongside the summary means `CON_08` is satisfied by both.

**Rejected.** Roles with a per-profile reader in the gate. Narrowing §6.2's neutrality claim to the
constraint text only, which reduces "swappable" to "swappable within .NET". Deferring because both
profiles are .NET.

### D-14 — `verify-chain` checks the comparison in both directions

**Governs:** nothing — superseded by D-15.

**Why.** It made the declared-versus-changed comparison a gate in both directions: an undeclared file
in the diff and a declared file left untouched would each fail the chain.

**Superseded by D-15**, which makes the comparison a signal rather than a gate — the reasoning is
there. Kept because a decision is superseded, never edited, so a commit citing this id still means
what it meant.

**Rejected.** Recorded under D-15, which carries both this decision's alternatives and its own.

### D-15 — The declared-versus-changed comparison is a signal, not a gate

**Governs:** §7.7, §13

**Why.** `CON_07` already says the file set is not its job — *"the file set is mechanical and a hook
already enforces it. Creep **inside** an approved file passes that hook"* — so the constraint exists
*because* the comparison cannot see inside a file, and the comparison was never load-bearing for
scope creep.

**The quotation above is revision 1, and half of it was not true.** No hook ever enforced the declared
file set; the clause was corrected in `CON_07.2`, which says the set is reported as a signal at the
gate rather than enforced anywhere. It is left here as it was read, because this is the wording the
decision was taken against and replacing it would make the record describe a conversation nobody had.
The decision survives the correction and is strengthened by it: the half that was load-bearing — that
creep inside a declared file is invisible to the comparison — is the half that was always true, and
the false half was an argument for the comparison being *less* than a gate, which is what this decided. And the counterexample dissolves: a plan whose declared file went untouched while every
test passed is an **inadequate proof**, caught by `CON_06` at the plan gate before code existed.

**What blocks instead:** build fails, tests fail, or the proof is not met. Outcome-based rather than
process-based.

**Rejected.** `UNDECLARED` blocking while `UNDELIVERED` does not: a noisy blocker gets routed around,
and all three ways an undeclared file appears show in the pull request diff.

**What it gives up.** The claim *"no file reached this change without a reviewer"* moves from the
non-bypassable layer to the hook, so a human hand-editing an undeclared file lands it with a note.
And wide claims over-serialise cycles in `batch-build` that did not need ordering — a performance
cost, self-correcting.

### D-16 — There is no `design` skill

**Governs:** §18

**Why.** The separate commands are where a person decides to proceed, not code organisation. Even a
combined skill interviewing at every stage removes the moment you stop, read the intent, and *choose*
to go on. Human-in-the-loop is not only "was a person asked" but "did a person decide to continue".

**Rejected.** A chained orchestrator that interrogates between stages — which would preserve every
constraint, and was rejected on the decision points rather than the constraints.

### D-17 — `steer` re-runs the chain from an existing intent and spec

**Governs:** §2.6, §3.2

**Why.** The correct path by hand is expensive enough that people will not pay it, so steering ends up
in conversation where it is lost. **A path so costly that it pushes information out of the system is
worse than no path.** `steer` keeps the correct path and makes it affordable — compliance gets
cheaper and no constraint loosens.

**Rejected.** A `steering.md` input file, which this makes unnecessary and which would need six
answers of its own. An `advice` finding alone: it needs an artifact to attach to, and the scenario is
steering arriving before a plan exists. A proposed implementation in the spec: its criteria must
derive from the intent, so a proposal appearing there derives from nothing, which `CON_05` forbids.

**Since D-97**, steering also takes in or declines suggestions a reader raised, by id. It is still not a
way to add a new outcome nobody raised.

### D-18 — A stage skill refuses when its artifact exists, and names what you meant

**Governs:** §15.1

**Why.** Typing `intent <id>` must never silently overwrite an intent. A refusal that teaches the
right command adds nothing and removes a destructive mode.

**Rejected.** A `--force` or `--rerun` flag on the stage skill: a second way to do what `interrogate`
already does, and the one people type is the one that drifts. Overwriting silently, which is the
behaviour a flag would eventually be added to restore.

### D-19 — `reopen` is a fourth decision, and it is how a wrong plan is recovered

**Governs:** §5.2

**Why.** Without it a wrong plan has no legal move: `remediate` may not edit a planning document,
`refine` refuses once approved, and the plan is hash-locked — so `remediate` could only keep trying to
make wrong code satisfy a wrong plan until it escalates. It also gives the design its symmetry: code
failures are capped because an unbounded fix-verify loop burns tokens unwatched; artifact failures are
not.

**Rejected.** Abandoning and starting over, which throws away an intent and spec that are probably
fine. Letting `remediate` edit the plan — the inversion §2.4 exists to prevent. Hand-editing and
relying on the hash mismatch, which leaves no record of why.

### D-20 — Each skill's preconditions are stated in one table

**Governs:** §15.1

**Why.** Its absence is what lets a skill quietly check the wrong precondition. Drafting it surfaced
two rules nobody had written: `spec` and `plan` refuse on an existing artifact, and `refine` refuses
on an approved cycle.

**Rejected.** Nothing — no alternative was weighed, because the table is the statement of a rule
rather than a choice between two. §15.1 is where that rule now lives, and this entry records only
that writing it down was what found the two missing preconditions.

### D-21 — A findings round records the upstream hash it judged against

**Governs:** §6.2

**Why.** The approval proves *these three documents were approved*, not *the spec was derived from
this intent*. So an intent edited after a clean spec leaves a spec derived from an intent that no
longer exists, with every hash matching on disk. And it is not hypothetical — `D-17` is the mechanism
that creates it.

**Why on the round rather than in the frontmatter**, which was the original proposal: a hash in the
spec's frontmatter would be written by the spec skill and not updated by a person hand-editing, so it
could be stale *and wrong*; the round is written by the interrogation, which just read both. It also
describes the thing that goes stale — the judgement — and makes `D-02` compute staleness for nothing.

**Rejected.** The upstream hash in the downstream artifact's frontmatter, for the reason above: it is
written by the skill that cannot see a later hand-edit, so it can be stale and wrong at once, which is
worse than absent. Recomputing staleness at the gate from the documents alone: it cannot say what a
round *judged*, only what the files are now.

### D-22 — `batch-build` completes cycles; `pr` pushes

**Governs:** §10.1

**Why.** One word carried two meanings. And "successes" implies success is determined, so
`batch-build` must **verify** as well as build — a batch that cannot say which cycles succeeded cannot
open pull requests for the ones that did.

**Rejected.** `batch-build` pushing, which makes an unattended run reach the remote. Building without
verifying, which leaves "success" undefined.

### D-23 — `pr --all` sweeps every completed cycle, with one confirmation

**Governs:** §10.3

**Superseded in part by D-85.** The factory opens a **draft** pull request for a `blocked` or
`escalated` cycle, which the last rejected option below foreclosed. `pr --all` by hand still opens one
only for `passed`, and the check still refuses any chain that did not pass.

**Why.** A batch of eight that completes and then needs eight manual invocations is a batch whose
value evaporates at the last step. One confirmation before the first push, because a pull request is
reversible but **outward-facing** — and printing the list also covers the surprise that every
`passed` cycle is eligible, including one from last week.

**Rejected.** Per-cycle confirmation. No confirmation. Opening one for a `blocked` cycle with a warning
— the verdict is the only thing between unattended building and unreviewed work reaching a pull request.

### D-24 — `batch-build --pr` composes the two; plain `batch-build` stops at completion

**Governs:** §10.3

**Why.** A pull request is not a new decision: the approval already said *this plan is agreed, build it*,
and a pull request **requests** review rather than committing anything. So the confirmation is about
**volume** rather than permission. A flag rather than the default, because the composed form touches
the remote and the plain one does not.

**Rejected.** Opening one by default. A confirmation per cycle inside an unattended run. No confirmation
at all — defensible, and rejected because volume is a real cost somebody should have chosen.

### D-25 — There is no `split` skill, and no `after` ordering

**Governs:** §3.1, §18

**Why.** v03 reached the same conclusion: splitting is a scoping decision that belongs to a person,
and *"auto-splitting without dependency ordering produces cycles that cannot be built in isolation,
which is worse than one large cycle that at least builds."* `after` was introduced **specifically to
supply that missing concept** — machinery built to enable a feature whose value was never
established, which is `D-06`'s test pointed at a skill.

**The risk this leaves open.** Nothing pushes back on a sixty-file plan, and v03's argument still
holds: *"a plan touching sixty files across five features cannot be meaningfully read, so approval
degrades into signing."* Partially mitigated by `tier`, which signposts reversibility.

**Rejected.** A `split` skill that cuts an intent into cycles: splitting is a scoping decision that
belongs to a person, and auto-splitting without dependency ordering produces cycles that cannot be
built in isolation — worse than one large cycle that at least builds. An `after` field ordering
cycles: machinery built to supply the concept auto-splitting was missing, which is D-06's test
pointed at a skill rather than at an artifact section.

**What could return without the costs.** A **read-only advisor** that proposes how to cut an intent and
writes nothing. It creates no cycles, so it cannot create cycles that will not build. Deferred rather
than rejected.

### D-26 — The command line is the mechanism; MCP is a later surface

**Governs:** §2.2, §15.2

**Why.** Two reasons, the second of which decides it. A pipeline and a git hook **cannot speak MCP**,
so the command line must exist regardless. And **MCP cannot deliver `D-01`'s independence for
interrogation**: a tool runs in its own process, but the model calling it is the same session, so
either the tool judges — needing its own model calls, credentials and bill — or the calling model
judges, which is the session that wrote the artifact grading its own homework. **A tool call hands
data to the reader you already have; a new process creates a new reader.**

This is specific to interrogation. Everything else the gate does is deterministic, and MCP would serve
it fine — which makes it a convenience over commands that must exist anyway.

**Rejected.** MCP as the mechanism. Both with no precedence, which was the contradiction. Dropping MCP
entirely, which loses a real future benefit for a sentence's cost.

### D-27 — A blocking finding cannot be waived, by any mechanism

**Governs:** §5.3, §6.1, §18

**Why.** `D-02` had already made the waiver vestigial: acceptance existed because some blockers
appeared to have no legal exit, and once every blocker has one, no legitimate case for waiving remains.
**And the exit does not launder bad work** — a criterion whose proof cannot reach it is recorded under
`Open`, so `CON_06` is satisfied, and then **verify still blocks** because every criterion must map to
a passing test. The exit moves *where* a problem is caught, never *whether*. Some blockers have no
open-region exit and must simply be fixed.

**`CON_10` rebinds rather than retiring.** Its forbids clause applies exactly to a gate refusal's
reason, which the ledger already requires.

**Rejected.** An `accepted_findings` field on the approval, which is what this removes: it made a
blocking finding waivable by the one act the chain exists to constrain. A severity between blocking
and suggestion to hold "blocking but allowed" — the same waiver with a different name, and D-30
removes the third severity for its own reasons. Retiring `CON_10` along with acceptance: its rule
about a refusal's reason survives the subject it was written for.

### D-28 — `init` has three modes, and the protected set defines what update may touch

**Governs:** §9

**Why.** `init` refusing when `.sdlc/` exists was correct while nothing needed refreshing, and it
leaves a solution's scaffolding to rot across framework versions with nothing to fix it. **Update
needs no list of exclusions: it updates what it owns.**

**Rejected.** `init` refusing outright when `.sdlc/` exists, which is what it did: correct while
nothing needed refreshing, and it leaves a solution's scaffolding to rot across framework versions
with nothing able to fix it. A list of what update may touch: a list is a second place the truth
lives, and the protected set already answers the question from the other side.

**Superseded in part by D-40.** This entry defined what update owns as everything not protected, which
is a file-level rule and was true only while every unprotected file was one the framework had named.
Two were not — `.claude/settings.json` is the host's and the team's, and `pull-request.yml` is a name a
repository plausibly already uses — so adoption destroyed both. D-40 is the ownership rule; the three
modes and the reasoning for them are unchanged.

It also closes part of the maintenance gap — when the framework version moves, something now updates
a solution's scaffolding. The judgement half, proposing changes to constraints and the tech stack,
remains open.

### D-29 — The interrogate-refine loop is a mode of each stage skill

**Governs:** §3

**Why.** `D-02` requires a clean round to advance, so the stage that must reach one is the stage that
should run the loop. And `D-16` is satisfied, because the loop stops on convergence and **hands back
rather than claiming to be done** — so the decision to keep interrogating stays the person's.

**Rejected.** A named skill of its own: an eighteenth entry and a second way to invoke one loop.

### D-30 — There are two severities, not three

**Governs:** §5.3

**Decision.** A finding is `blocking` or a `suggestion`. The third value, for an improvement that
violates nothing and cites nothing, is removed. A citation is required on a blocking finding and
optional on a suggestion, so an uncited non-blocking finding is already a suggestion.

**Why.** It had no behaviour of its own — nothing read the distinction, and nothing could, because the
only rule about citation already draws the line at blocking. Its other purpose was a home for human
steering, which `D-17` reassigned to the intent's `Fixed decisions`, where a reviewer meets it at the
gate rather than in a findings file.

**Rejected.** Requiring a citation on suggestions, which would have earned the third tier its place
back. It makes the cheap finding expensive, and the trial's two suggestions — that `dotnet test` did
not run the suite, and that a criterion's proof rested on a substituted source — were both
substantive without a constraint to hang on.

### D-31 — The envelope is an allowlist, and an undeclared key or section is a violation

**Governs:** §4.1

**Decision.** Two frontmatter keys — `framework_version` and `cycle` — plus one section, `## Open`.
Everything else in the file is body. A key or section the artifact does not declare is a schema
violation, not a tolerated extra. `verify`'s body is one section, `Criteria`, plus an `outcome` key;
its findings are data in `findings/verify.rN.json` like every other artifact's.

**Why.** Writing it as an allowlist rather than as "the envelope holds these, the body holds the rest"
is what makes `D-07a` enforceable instead of merely intended. `D-07a` rejected a `metadata` bag
because a claim in the envelope escapes the constraints — and that only holds if the envelope cannot
grow. Naming the body parts and leaving the envelope open would let the next universal-looking field
in through the side the constraints do not reach.

Rejecting an undeclared key is the same argument as `D-06` pointed at the schema: a field nothing
declared is a field nothing reads, and v03 set `additionalProperties: false` deliberately. `D-07a`
already recorded the escape hatch this needs — a **declared extension**, where the solution says which
extra keys it carries and the schema validates against the declaration — and until that exists, extra
is wrong rather than ignored.

**Rejected.** Warning on an undeclared key instead of failing: the warning is read once and the key
stays forever. Allowing unknown sections while policing keys, which splits one rule into two with no
reason a reader could reconstruct.

### D-32 — The Azure DevOps adapter ships unexercised, and says so

**Governs:** §13

**Decision.** The Azure Pipelines template and the `az repos pr create` path ship without ever having
run against a real Azure DevOps server. The template itself carries a note saying so, and §13 and the
README say it too. No second trial repository is created and `dice-trial` gets no Azure DevOps remote.

**Why.** It is one pipeline file and one command invocation, the failure is loud and immediate, and
nothing else in the framework depends on either. Against that, a second trial repository is a thing to
keep alive, keep credentialed and keep in step with the first — and the cost of one person hitting a
broken pipeline once is smaller than the cost of maintaining a repository nobody uses for anything else.

**What makes this honest rather than a shortcut.** The note is in the generated file, not only in the
concept. §2.1's rule is that a thing which appears enforced and is not is worse than one that never
pretended, and a pipeline nobody has run is exactly that shape: it looks like a gate. So the first
person to adopt on Azure DevOps reads, in their own repository, that this path is untried.

**Rejected.** Giving `dice-trial` an Azure DevOps remote as well as its GitHub one — declined once
already, and it makes the trial repository answer to two platforms when its job is exercising the
cycle. A throwaway Azure DevOps project — the same maintenance in a form nobody owns. Dropping the
Azure DevOps template until it can be tested, which would make the private half of the stack, where
the work actually lives, the half with no pipeline.

### D-33 — The default profile is carried by the binary; every other profile is a package

**Governs:** §11.5

**Decision.** `dotnet-default` ships inside the gate binary, as the pipeline templates already do, and
`init` writes its files directly. The NuGet mechanism in §11.5 stands for every other profile — a
private house profile, a profile for another stack — and those are restorable and versioned on their
own cadence.

**Superseded in part by D-68.** This entry drew the line at the *default* profile, which was the whole
of the embedded set when it was written. D-68 ships a second embedded profile, `ml-tensorflow`, so
"every other profile is a package" is no longer true as stated — what survives is the reasoning, which
is about a restore that can fail rather than about which profile is which: a profile adoption depends on
cannot need a feed. The package mechanism stays open and has no user yet. §11.5 carries the current
rule.

**Why.** The two halves of a profile reach a solution differently and only one of them can be a
package today. Prose defaults are *extracted and injected* into a plan, because a session inside a
governed solution cannot read a file that lives in the plugin. But `.editorconfig`,
`Directory.Build.props`, `Directory.Packages.props`, `global.json` and a `BannedSymbols.txt` have to
physically land in the repository — the first four at adoption, the ban list with the skeleton — and
there is no published feed, as the README says.
A default profile that needed a restore would make `sdlc init` a first command that can fail on a
machine with no feed configured, for the one profile every solution starts from.

**What it costs.** A change to `dotnet-default` is a release of the binary rather than of a package,
so the default cannot move independently of the gate. That is the right way round: the default is what
every solution starts from, so it should move rarely and in step with the thing that reads it. A
profile wanting its own cadence is precisely the case the package mechanism exists for, and it keeps
that case open rather than closing it.

**Rejected.** *Profiles are packages, including the default* — §11.5 as written, and it cannot be
built today: no feed exists, and adoption would acquire a failure mode in its first command. *Templates
in the binary, full stop* — simpler, and it closes the door on a private profile with its own release
cycle, which is the whole reason the private half exists. *A profile as a git submodule or a cloned
directory* — no versioning, and it puts a network dependency in the same place while being harder to
reason about than a package.

**The honest part.** §11.5 claimed something untrue, and the claim was load-bearing: it is what made
the profile package look like the next thing to build. Writing the files from the binary is what
actually unblocks `init` scaffolding a solution, which is what every stated convention — sealed types,
an internal data context, validation through the pipeline, one way out of an error boundary — needs in
order to be enforced by anything.

### D-34 — `dotnet-default` references the kernel

**Governs:** §11.3

**Decision.** The public profile takes `MofonCode.Core`, so a scaffolded solution gets the operation
shape of §11.3.1 and a reference slice written in it.

**Why.** The package is MIT and on nuget.org, published by trusted publishing from its own repository,
so nothing about taking it needs credentials or a private feed — which is what an earlier draft assumed
and got wrong. And the shape it enables is the framework's own settled convention rather than a house
preference: a public profile that shipped the analyzer policy and the fitness functions but not the
shape would leave the most effective lever unbuilt, because an agent copies the code beside it far more
reliably than it follows an instruction.

**What it costs, and it is a real cost.** Someone adopting the public plugin acquires a dependency on a
pre-1.0 package that this team controls and versions, and inherits its breaking changes. Three things
make that acceptable rather than merely convenient: the package is pinned at adoption rather than
floated, its public surface is now listed and a change to it fails that package's own suite, and it
depends on nothing but FluentResults and the `Microsoft.Extensions.*.Abstractions` — so it decides no
data story and no hosting story on a consumer's behalf.

**Rejected.** *A kernel-free public profile*, whose reference slice would return `Result` without a
mediator — honest, and it demotes §11.3.1 from the framework's shape to one house's, which is not what
it is. *Vendoring the mediator into each scaffolded solution as source* — no versioning, no fixes, and
four files every consumer then owns and must maintain. *Publishing a second, differently-named kernel
for public use* — the same code under two names, which is the kind of duplication that diverges.

**What this makes honest.** §11.3 claimed the kernel was private and the shape was house-specific. Both
were false, and the first caused the second — the belief that the shape could not be public is what
made a kernel-free scaffold look necessary.

### D-35 — `init` writes no code; the solution comes from an approved plan

**Governs:** §9, §15.2

**Superseded in part by D-76.** The table below puts the build settings at adoption. They are written
by `sdlc scaffold` now, with the skeleton, because adoption is no longer told what a solution is built
from. The line this entry draws — adoption writes no code — is unchanged.

**Decision.** Adoption writes the governance and the files that constrain how code is written, and
stops there. The solution file, the projects, the architecture test project and the reference slice
are written by `sdlc scaffold <id>`, which refuses unless `gate` would let that cycle build — so the
first code in a repository arrives behind an intent, a spec, a plan and an approval, like every line
after it.

**Why.** Adoption had briefly written the whole solution, which made the scaffold the only code in a
governed repository with no chain behind it. The headline promise is *a person approves a plan before
code exists*; an adoption that writes a project and commits it exempts itself from the one thing the
design exists to guarantee, and `verify-chain` can re-establish every other line from an approved
plan but not those.

It was also deciding too early. The scaffold fixes project names, a layout and a mediator-based core
before any intent has been written, so a first intent that wants a different shape inherits one
somebody then deletes — and the reference slice's own "delete it once there is real work here" was
admitting that rather than solving it.

**Where the line falls.** Between constraining how code is written and writing it.

| Written at adoption | Written by an approved cycle |
|---|---|
| `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `tests/Directory.Build.props` | the solution file, the projects, `BannedSymbols.txt`, the architecture tests, the reference slice |

Everything on the left constrains nothing until there is code to constrain, so it carries no decision
a cycle should have taken. Everything on the right *is* the solution.

**`BannedSymbols.txt` is on the right, and this entry said otherwise for as long as it existed.** The
code has always written it with the skeleton; the table above claimed adoption did, and nothing could
fail on the disagreement. The skeleton is where it belongs, by D-36's rule that a ban ships with the
surface it governs: the list carries the bans on `Results` and `TypedResults`, which are a decision
about an Api project that does not exist until the skeleton writes one. The clock bans in the same
file would sit happily on the left, and splitting the list across both moments would mean two writes
to one path where the second only writes what is absent — so the file ships whole, with the surface
that needs most of it.

**Why a command rather than the plan carrying the templates.** A profile's prose defaults reach a plan
by being extracted and injected (§11.5), because a session inside a governed solution cannot read a
file that lives in the plugin. Files are different: transcribing five hundred lines through a plan
invites corruption and proves nothing. So the binary writes them, and the approval is what permits it
— `sdlc scaffold` dispatches the same `ApprovalGate` query the build stage does, which means a plan
declaring those files has already been approved, and the declared-against-changed comparison then
holds the build to it.

**What it costs.** The architecture tests cannot exist at adoption, because their own guard throws
when a scan finds nothing to inspect. They now arrive in the first cycle with the project they police,
which is where an invariant belongs anyway — v03's rule was that an adopted invariant ships with its
architecture test in the same change.

**Rejected.** *Keeping it, with a recorded exemption* — defensible, and it turns the headline promise
into a qualified one that would then have to be qualified in the README too. *An opt-in flag* — the
default would still be a choice nobody made deliberately, and a flag is where the exemption would
quietly live. *A skill that writes the files during the build stage* — the templates live in the
plugin, so the skill would be transcribing them, which is the corruption risk with none of the
benefit.

**What this makes honest.** Nothing in the concept forbade adoption writing code; the contradiction
was with §2's promise rather than with a rule, which is why it survived being built, committed and
verified end to end before anybody noticed. Stated here so the next thing that writes a file at
adoption has a rule to fail against rather than a principle to be weighed against.

### D-36 — a ban ships with the surface it governs, and never alone

**Governs:** §11.4

**Decision.** The profile scaffolds an Api project with exactly one conversion from a `Result` to a
response, bans `Microsoft.AspNetCore.Http.Results` and `TypedResults` outright, and ships two
architecture tests beside them: every endpoint returns `ApiResult<T>`, and the solution declares no
second `IResult`. The four are one change. A profile that bans a response factory without scaffolding
a boundary is shipping a rule that cannot fire.

**Why, measured rather than reasoned.** A banned-API entry naming a symbol the compilation cannot
resolve is reported by **nothing** — no warning, no diagnostic, no hint. A library banning
`T:Totally.Made.Up.Type` builds clean. So the scaffold had no HTTP project, and banning
`Results.Problem` in it would have produced a file that looks enforced and enforces nothing, which is
worse than no file at all.

Three more things came out of testing it rather than reading about it:

- **A bare method name bans nothing, silently.** `M:…Http.Results.Problem` with no signature matches
  no overload. Only the full documentation-comment id does.
- **`Problem` has three overloads in .NET 10**, and a hand-written ban carrying two of them was inert,
  because the one omitted was the one every call site binds to. The framework's own XML docs in the
  reference pack are the only reliable source for the ids.
- **Banning `Results` alone leaves `TypedResults.Problem` compiling**, doing exactly what the ban was
  written to prevent. A ban on one spelling is not a ban on the behaviour.

So the ban is on the **types**, not the methods: it cannot be defeated by an overload nobody listed,
and it also catches `Results.Ok`, which sends a bare payload with no notices for a front end to read.

**Why a fitness function as well.** The ban cannot see an endpoint that returns a domain type
directly — that compiles, calls nothing banned, and sends a payload with no notices and no trace id.
`Every_endpoint_returns_the_envelope` reads the return types, so it holds for spellings nobody has
thought of yet: a hand-set status code, a plain string, a factory a later framework version adds.
Proven by writing such an endpoint, watching the build go green, and watching the rule name it.

**Why the conversion writes its own JSON.** It implements `IResult` and calls `WriteAsJsonAsync`
rather than a framework factory, so the ban needs no exemption for the one place allowed to build a
response. An exemption is where the next leak goes, and a boundary that needed one would not be a
boundary.

**And the ban is itself guarded.** `The_frameworks_response_factories_are_still_banned` reads
`BannedSymbols.txt` and fails when either line is gone, because two lines in a text file can be
deleted in the same commit as the call somebody wanted to make.

**Rejected.** Banning the response factories without scaffolding the Api project: a banned-API entry
naming a symbol the compilation cannot resolve is reported by nothing, so it is a rule that looks
enforced and cannot fire — measured, not reasoned. Shipping the boundary without the ban: the one
conversion becomes a convention, and a second `IResult` arrives the first time somebody is in a hurry.
Shipping either without the two architecture tests: two lines in a text file can be deleted in the
same commit as the call somebody wanted to make. The framework taking the bans itself: `sdlc` is a
command-line binary with no HTTP surface, so the parity rule carries that exemption explicitly.

**What this costs.** The scaffold is four files larger and the profile now takes a position on HTTP,
which an adopted solution with a different boundary will not want — it does not get one: `origin`
decides, as it does for everything else the profile writes. And the framework itself does not take
these two bans, because `sdlc` is a command-line binary with no such surface; the parity rule carries
that exemption explicitly, on the single ground that the surface does not exist here.

### D-37 — The pipeline installs the gate it calls, pinned, and says the feed is not there yet

**Governs:** §13

**Decision.** Both generated pipelines gain one step: `dotnet tool install --global
MofonCode.Sdlc.Cli --version <release>`, placed immediately after the SDK step and before the build.
The version is stamped from `Sdlc.Core.Release.Version`, which is the same `VersionPrefix` the plugin
carries. The step says in the generated file that the package is not on a public feed yet. No tool
manifest is written into an adopted solution.

**Why.** The templates called `sdlc verify-chain` on a runner where nothing had put `sdlc` on the
path. The guarantee is that nothing merges unless the chain holds, and the only thing establishing it
would have reported command-not-found — from the single step a reader trusts most, in a file that
otherwise looks complete. That is §2.1's failure mode reached by omission rather than by design.

**Why pinned.** An unpinned governance tool can change what a repository is judged by between two
runs, with nobody having chosen the new rules. `latest` on an ordinary build tool is a convenience; on
the thing that decides whether a change may merge it is a silent policy change.

**Why before the build.** A gate that cannot be fetched should fail in seconds rather than after a
full build and test run. The pull request stops either way, and the cheaper failure says the same
thing.

**Why it says the feed is not there yet.** It is D-32's rule applied one step earlier. The package is
unpublished, so this install does not resolve, and the person whose first pull request fails on it
reads the generated file rather than this document. Writing no step at all was the quiet version of
the same gap — and the quiet version is the one the whole design is written against.

**Rejected.** A tool manifest and `dotnet tool restore`, which is the more idiomatic .NET shape and
pins per solution for developers as well as CI. It changes the local invocation to `dotnet sdlc`,
while every skill, the status panel's next line and every guide say `sdlc` — so
adopting it means one command with two spellings depending on where it is read, or rewriting all of
them. It also adds a file to what adoption writes, which is a larger decision than the one this
closes. Worth revisiting when the package is published, because the pinning argument above is an
argument for a manifest.

Also rejected: leaving the step out and documenting the gap, which is what the guides did before this.
A documented gap in a generated file is still a generated file that looks like a gate.

### D-38 — `Application` is the solutioning layer, and its data access is internal to it

**Governs:** §11.3.1

**Decision.** A scaffolded solution's `src/` holds four projects, each prefixed with the solution's own
name, and exactly one of them can reach data or a third party.

| Project | Holds | Public surface |
|---|---|---|
| `Contracts` | the wire shapes — a response record per operation, a request record for each command that needs a body, and the enums they carry | all of it; that is the whole point of it |
| `Application` | commands, queries, handlers, validators, the `DbContext`, every third-party SDK wrapper | the requests and the registration extension |
| `Api` | endpoints, and the one conversion from a `Result` to a response | nothing; `Program` is internal |
| `Web` | Blazor WebAssembly — shapes queried data, sends commands | nothing |

```
Contracts   -> nothing, and no package reference either
Application -> Contracts
Api         -> Application
Web         -> Contracts
```

**The `DbContext` is `internal` to `Application`, and so is every SDK wrapper.** No other assembly can
query, and none can acquire the ability without a change a rule refuses.

**Why one project rather than an application layer over an infrastructure layer.** The split insures
against swapping the ORM, and that is not the event that happens. Changing the *provider* happens, and
EF already abstracts it, so the layer earns nothing against the real case while charging a project and
an interface per capability against the imaginary one. Worse, it relocates the failure it claims to
prevent: with a port per capability, the uncontrolled surface is the set of ports, and ten developers
under a deadline produce ten ports rather than ten queries.

**Why `internal` rather than a port.** A port is a convention — nothing stops a fortieth method on an
`IOrderStore`, or a second interface reading the same table a different way, which is exactly what a
timeline produces. `internal` is decided by the compiler: no other assembly can query, full stop. The
framework's own rule is that a constraint belongs in the lowest layer that can hold it, and an access
modifier is lower than any test. It is also the reason the operation shape keeps its handler
`private` — the boundary is enforced by what can be named, not by what was agreed.

**Why no repository.** `DbContext` is already a unit of work and `DbSet<T>` is already a repository. A
wrapper over them is a hand-maintained surface strictly worse than the one it hides, and it buys the
swappability that the paragraph above establishes nobody uses.

**Why the front end is WebAssembly, and why that is a decision rather than a solution's choice.** A
same-process front end can inject the mediator and dispatch a command directly, which makes "the UI
holds no business logic" a convention. WebAssembly cannot reference the application project even by
accident, and the reason is concrete rather than architectural: **WASM ships every referenced assembly
to the browser**, so a reference from `Web` to `Application` would download EF Core, the database
provider and every vendor SDK to each client. The access modifiers stop the UI *calling* the data
layer; they do not stop the bytes shipping. That is what `Contracts` exists to prevent.

**What the rules are.** Three, and none of them is a convention. All live in the architecture test
project:

- **the direction rule** — the adjacency above, read from `.csproj` files. It is what stops `Web`
  reaching past the wire into `Application`.
- **the package rule** — only `Application` may reference `Microsoft.EntityFrameworkCore*`, a database
  provider, or a vendor SDK; `Contracts` may reference nothing at all. `internal` stops another project
  *using* the context; this is what stops it declaring a second one. Both are needed, and the second is
  the one somebody reaches for when the first is in the way.
- **the response rule** — every `IRequest<T>` that something outside `Application` can dispatch has
  its `T` declared in `Contracts`, unless `T` belongs to an assembly the solution does not own.
  Without it, the first operation written from muscle memory nests its response in its holder and the
  client cannot see it. The suite already enumerates `IRequest<>` implementations to check that each
  has a registered handler, so this is a few lines on machinery that exists.

  **The two exemptions are the rule, not holes in it, and F-16 is why they exist.** It read "every
  `IRequest<T>`" with no exception, which fails `IRequest<int>` — the assembly is `CoreLib`, and a
  primitive cannot be moved into contracts — and fails the first internal dispatch, which §11.3.1
  makes a nested handler reach for deliberately. A rule that fires on legitimate work is one somebody
  deletes in the cycle it gets in the way of, and deleting this one reopens the gap it exists to close.
  So it asks the two questions that actually decide whether a response crosses a wire: can anything
  outside dispatch the request, and is the type this solution's to place. An `internal` request is the
  declaration that an operation has no wire — the access modifier again, decided by the compiler
  rather than agreed.

None passes vacuously: each enumerates the projects or requests it knows about and asserts a negative
of each, so a repository with no EF reference anywhere still has something to inspect.

**Where a shape lives, and the one existing pattern this changes.**

A response record *is* the wire shape, so it lives in `Contracts`. The query or command stays nested in
its holder, because it is internal to the operation and nothing outside dispatches it. The folders
mirror, so finding a response is mechanical rather than a search:

```
src/<solution>.Contracts/Greetings/GreetResponse.cs
src/<solution>.Application/Greetings/Greet.cs      Query, Validator, Handler, registration
```

**The response is named flatly — `GreetResponse`, not nested in a second `Greet` holder in
`Contracts`.** Nesting it would put two static classes called `Greet` in two namespaces, and the one
file that must name both is the holder, where the ambiguity would land. The asymmetry is deliberate and
the template says so, or somebody tidies it into a collision.

This is a departure from the shape the reference slice teaches — *"the response nests with the query
that produces it"* — and its remarks have to state the new rule rather than leave the old sentence
standing.

**Inbound shapes are asymmetric, for a reason.** A response always has to be shared, because the client
deserialises it. A request often does not: for a query the endpoint binds from route and query string
and constructs the request itself, so no inbound type exists at all. A request record goes in
`Contracts` only when a command carries a body the client has to serialise, and the endpoint maps it to
the command in one line. That mapping can drift like any other, but it exists on a minority of
operations rather than all of them, and a field missed there surfaces as a validation failure rather
than as silence.

**What it costs, stated plainly.**

- **`internal` buys "only this project", not "only one way".** Two hundred handlers touching
  `DbContext` directly is the same failure at a smaller radius, with one team and one review surface
  instead of several. No layer fixes that; a convention about where queries are composed might, and it
  is not decided here.
- **The wire shape and the handler's response are one type.** Renaming a field is a breaking change to
  the client, and there is no seam at which to version one without the other. This is the cost of
  rejecting a mapping layer, and the condition that would reverse it is named under `Rejected`.
- **An operation is two files rather than one.** Every decision, rule and line of logic stays in the
  holder; what leaves is a flat record's field list. Reading one file still says what the operation
  does.
- **The registration extension takes an options delegate.** `Program` cannot name an internal
  `DbContext`, so registration moves inside the project that owns the schema — which is also what keeps
  the connection string there. The services file must distinguish this from the factory-closure it
  already forbids, which is about handlers rather than configuration.
- **Migrations need a design-time factory inside `Application`.** EF's tooling reflects over the
  assembly and finds an internal one; the `--startup-project` form is the fallback if it ever does not.

**Testing, and what is deferred.** Containers against the real provider are the eventual answer and are
**not** adopted now: the resources are not there, and a deferral is honest where a weaker substitute is
not. Until then the suite dispatches through the test gate as it already does, and state is seeded
directly through the context — which the existing `InternalsVisibleTo` to the test project already
permits.

Two rules survive the deferral. **The in-memory EF provider is not used**, now or later: it enforces no
relational constraint, has no transactions, and translates differently, so it is a provider that lets a
test pass where production fails — the vacuous success this framework's own suite is written against,
arriving through the data layer. SQLite in-memory is the defensible fast option; whatever gates in CI
runs against the real provider.

And **a test that cannot find its key fails rather than skips.** §17 records the nightly job that
needed an API key nobody had set, warned, and exited cleanly — a check that tested nothing while
appearing to run. The rule is cheapest to adopt before the first such test exists.

**The deferral has a deadline rather than a date.** The default profile ships no EF, so there is nothing
to integration-test on the first day. The first cycle that adds EF is the one that must answer what it
tests against, and this entry is what it answers to.

**What the skeleton writes, and what waits for a cycle.** The contracts, application and api projects
and the two test projects, with the reference slice spanning them. **The web project is written by
neither adoption nor the skeleton** — the same reasoning as EF: a UI in the default profile commits
every scaffolded solution to having one. The direction rule covering `Web` ships anyway and costs
nothing while no such project exists, because it asserts a negative over the projects it finds.

**No `Core` until it has two occupants in two projects.** Cross-cutting helpers are a real category and
an empty project is not; a project created before it has an inhabitant gets filled with whatever
arrives, and the reference slice states the mechanism — shape beats instruction. `Contracts` passes that
test on the first day, with the response record that `Api` and `Web` both need. A helpers project does
not, and the bar for creating one is two things, used by two projects, that exist.

**Rejected.**

- *A separate infrastructure project* — the layered shape. Rejected for the reason above: it insures
  against the swap that does not occur, is no help with the one that does, and converts a query surface
  into a port surface rather than closing it.
- *A repository abstraction over `DbContext`* — rejected as a worse copy of what EF already provides.
- *A `Core` project on the first day* — rejected as a project without an occupant. Not rejected as an
  idea.
- *EF in the default profile* — rejected because it decides a data story for every solution this
  scaffolds, and the kernel is deliberately chosen so that adopting it decides none.
- *Blazor Server* — rejected for the reason given above: it makes the dumb front end a convention,
  where WebAssembly makes it structural.
- *`Web` referencing `Application` directly, with no contracts project* — rejected because WASM would
  ship the data layer and every vendor SDK to the browser. It is the option that looks free.
- *The in-memory EF provider* — rejected above.
- *Mapping a handler's response to a separate wire record, with the response staying nested* — rejected
  because the mapping **drifts silently**: a field added to a response and missed in the mapping
  produces no build error and no test failure, only a client that never sees it, and the test that would
  catch it is machinery written to defend machinery. **What would reverse this:** deployed clients
  running against a newer Api. A cached WASM bundle plus rolling deploys means an internal rename breaks
  yesterday's client, and then the mapping is the seam that absorbs it. Client and Api deploying
  together is the assumption this rests on, and it is the thing to re-read when that stops being true.
- *Generating the client from the OpenAPI document* — Kiota or NSwag, with the response staying nested
  and no hand-written contracts at all. It satisfies both instincts and was rejected for its cost: a
  generation step in the build and in CI, and generated code that fails quietly when the generator
  no-ops. Worth revisiting if `Contracts` ever becomes large enough to be its own maintenance burden.
- *Containers now* — deferred, not rejected.

**What this makes honest.** Nothing recorded the scaffolded layout before this. It existed as the
contents of `Solution.slnx.template` and two project files, which made it a convention that read as a
decision — the category this framework exists to eliminate, sitting inside the framework. It also gives
two helpers in the scaffolded `RepoFiles` a job: `ProjectReferences` and `PackageReferences` were both
written and never called, which is the previous pass having intended roughly this and stopping short.

### D-39 — The pre-tool hook refuses or says nothing; it never grants a permission

**Governs:** §12.1

**Decision.** `sdlc hook` writes a decision only when the policy refuses. On an allow it writes an
empty object, for every host.

**What was there before.** `HookProtocol.WriteDecision` had no allow branch — anything that was not a
deny was spelled `allow` — so every tool call the policy had no opinion about came back as an explicit
`permissionDecision: "allow"`. `HookPolicy.Decide` refuses six protected paths and the shell commands
that touch them and permits everything else, so that was nearly every call.

**Why that was wrong.** On these surfaces an explicit allow is not silence. Claude Code documents it as
*bypassing the permission system*, and the hook is configured against
`Edit|Write|MultiEdit|NotebookEdit|Bash` — exactly the tools a person wants to be asked about. So
adopting the framework stopped the host prompting on file writes and shell commands, and overrode
whatever allow and deny rules the team had written for them. §2.1's standing rule is that a directory
which appears enforced and is not is worse than one that never pretended; a governance tool that
removes the host's own gate as a side effect is the same failure with the sign flipped, and it arrived
through the layer whose whole purpose is enforcement.

The turn-end path already had this right and said why: *"An allow writes an empty object rather than an
explicit permission. There is nothing to say, and a host that gains a field for saying it will not
break on silence."* The same reasoning applies one event over, and the asymmetry between the two paths
was an oversight rather than a distinction.

**Why not `ask`.** It is the available third value on Claude Code and it is an override in the other
direction: it forces a prompt on calls a person's own settings auto-approve, so the hook would decide
the permission question for them restrictively instead of permissively. The gate's job is to refuse
protected files, not to re-answer a question the host has already been configured to answer.

**Why silence on Copilot too, where the spec was not checked.** Copilot's `preToolUse` output shape
could not be verified when this was decided, and the failure directions are not symmetric. A host that
ignores an empty object falls back to asking a person, which is safe and visible. A host that honours an
explicit allow the way Claude Code does gets the bypass shipped to it. One behaviour for both also keeps
the allow path out of the host conditional, where only vocabulary belongs.

**What it costs.** The hook can no longer say "I looked, and I am content". Nothing wanted that, and the
only reader was the host's permission engine, which is the thing being handed back its own decision.

**What this does not change.** A payload the reader cannot understand still allows the call and says so
on standard error (§12.1), and still exits zero. What changes is that allowing it now means declining to
answer rather than granting a permission — so the fail-open path no longer widens anything while it
fails open.

**Rejected.**

- *An explicit allow, with the hook matcher narrowed to the protected paths instead* — a matcher is a
  tool-name pattern, not a path filter, so it cannot express the rule. Narrowing it to `Write|Edit`
  would also drop the shell rule, which is the one `ProtectedPaths.CommandTouchesProtected` exists for.
- *`ask` on anything the policy permits* — rejected above.
- *An explicit allow for Copilot only* — rejected for the asymmetry above, and because it would put a
  decision rather than a vocabulary difference inside the host switch.

### D-40 — The framework owns files it named, and entries inside files it shares

**Governs:** §9

**Superseded in part by D-81.** Adoption no longer names a `pull-request.yml` an earlier adoption left
behind. Everything else stands.

**Decision.** Adoption rewrites a file only where the framework's own name is in the path. Where the
path belongs to the host or the platform, it owns its entries and merges them in, or it writes nothing
and prints them.

| | Adoption's claim | How |
|---|---|---|
| `.sdlc/**`, `.github/hooks/sdlc.json`, `.github/workflows/sdlc-verify.yml` | the whole file | rewritten on every run |
| `.claude/settings.json` | two hook entries | merged; every other key and sibling hook kept |
| `azure-pipelines.yml`, `AGENTS.md` | nothing, once one exists | printed for a person to place |
| `.gitattributes` | nothing, once one exists | kept if it normalises line endings, otherwise the one line printed |
| `.sdlc/solution.md`, the stack files | nothing, once one exists | created, then never touched |

**Corrected 2026-10-13:** the last row named the stack files as something adoption creates, which was
true when written. Adoption writes none of them now; a cycle brings them, from `sdlc scaffold` (D-76,
D-77), and §9 owns where they come from. The rule this entry states is unchanged, and they are still
created once and never rewritten.

**What this replaces.** D-28 said update *"needs no list of exclusions: it updates what it owns, and
what it owns is everything not protected"*, and §9 repeated it. That defines ownership as the
file-level complement of the protected set, which holds only while every unprotected file is one the
framework named. Two were not.

`.claude/settings.json` is the host's shared project settings — permissions, environment, the team's
own hooks. Adoption wrote the whole file from a template, in every mode and on every run, so a team
that added a `deny` rule or a hook of their own lost it at the next upgrade. **Losing a hook is §2.1's
own failure mode, caused by the thing installed to prevent it.**

`pull-request.yml` is a name a repository plausibly already uses, and adoption rewrote whatever was
there. Because §9 also says *"It commits what it created"* and the commit stages everything written,
a team's existing pull-request workflow was replaced **and committed** — and both were announced as
`updated`, in the same list as six genuine creations.

**Why renaming rather than merging, for the pipeline.** YAML has no entry the framework can own: a
workflow is one document, and a merge would mean understanding somebody's job graph. So the collision
is removed instead of handled — `sdlc-verify.yml` is framework-named, which makes `Owned` truthful,
and a team's own `pull-request.yml` is never in the line of fire. Azure DevOps gets no such escape,
because `azure-pipelines.yml` is the path that platform looks for, so an existing one is printed and
`init` says the guarantee is not installed until somebody places it. A pipeline nobody wired up looks
exactly like a gate (D-32), so saying it is the whole of the remedy there.

**Why a merge rather than printing, for the settings file.** Printing is safer and it is the wrong
trade here: the pre-tool hook is the fast local layer a person meets on their first edit, and an
adoption that silently did not install it would be the directory that appears enforced and is not.
JSON also has an entry to own, which YAML does not. The merge works on the inner hook objects rather
than the matcher groups, because a team can legitimately put their hook and this one under one
matcher; every hook whose command starts with `sdlc hook` is taken out and the framework's entries are
appended. Prefix-matched, so a release that changes a flag replaces its entry rather than appending a
second that also runs.

**What it costs, stated plainly.**

- **The first merge reformats the file.** Property order survives and whitespace does not, so a team
  sees a larger diff than the two entries they expected. The alternative is a hand-rolled JSON editor
  that preserves formatting, which is a great deal of machinery for one file.
- **A settings file that is not valid JSON is not merged at all.** Relaxed parsing would accept a
  comment or a trailing comma and then drop it silently on the way back out, so the rule is
  strict-parse-or-print: nothing is written, the entries are printed, and the file keeps every byte.
  The cost is that a team who hand-edited their settings has to place two entries themselves.
- **A framework hook somebody moved into their own matcher group moves back out.** The entry is the
  framework's; where it sits is not negotiated.
- **Renaming the GitHub workflow orphans the old one.** An existing `pull-request.yml` written by an
  earlier adoption is left behind, still running, and nothing deletes it — deleting a workflow is not
  a thing adoption should do. `init` names it, and a person removes it.
- **One more concept to hold.** "What it owns is everything not protected" was one sentence. This is
  a table.

**Rejected.**

- *Printing the settings file on every collision* — rejected above: the local layer would silently not
  be installed, which is the shape of enforcement this framework is written against.
- *Writing no settings file at all, since the plugin ships `hooks/hooks.json`* — the redundancy is
  real, and for a plugin user every matched tool call pays two process launches. Rejected because
  adoption cannot reliably tell a plugin install from a global one, and guessing wrong leaves the hook
  uninstalled. After D-39 both launches answer with silence, so the cost is start-up time rather than
  behaviour. Worth revisiting if a marker appears.
- *Merging the pipeline YAML* — rejected above. There is no entry to own.
- *Keeping `pull-request.yml` and printing on a collision* — rejected as the worse half of both
  options: it leaves a conventional name claimed by the framework, and the first collision is a
  pipeline nobody installed.
- *Naming the Azure DevOps file `sdlc-verify.yml` too, for symmetry* — not rejected, deferred. It
  would need the pipeline pointed at a non-default path in the portal, which is a step a person takes
  there anyway, and D-32 says that adapter has never run against a real server. The first team to use
  it is the one who should decide.

### D-41 — The binary carries the constraint library

**Governs:** §7.3

**Decision.** `CONSTRAINTS.md` is an embedded resource in `Sdlc.Core`. A file found through
`SDLC_CONSTRAINTS`, beside the gate, or above it still wins; the embedded copy is what everything
falls through to. The library can no longer be missing.

**What was there before.** `Locate.Library` looked at `SDLC_CONSTRAINTS`, then beside the running
assembly, then up the tree, and `Workspace.Run` refused to open the container when all three came up
empty. Nothing packed the file, and a global tool install satisfies none of the three.

So the installed tool answered `version` and `help` and nothing else. `constraints`, `init`, `status`,
`gate`, `draft`, `evidence`, `claims`, `pr-ready` and `verify-chain` all exited 3 with the same message
about a missing file, the pinned `dotnet tool install` D-37 put in both generated pipelines installed a
gate whose one non-bypassable check could never pass — **so nothing could ever merge, for a reason
unrelated to the chain** — and the turn-end gate printed the error and let the turn through. §7.3's
claim that `sdlc constraints` "is also how the set reaches a session, on every host" held only for a
binary run out of a clone.

**Why a resource rather than packing the file.** `Sdlc.Core.csproj` already makes this argument for the
scaffold templates: *"a template read from a path beside the binary is a template that is missing in
exactly that case; a resource is inside the assembly."* Packing the file repairs the global tool and
leaves the shape — a binary needing a file it does not control, three places it might be, and a failure
when it is in none of them. There is no arrangement of a copied file in which "the library is missing"
cannot happen. With a resource the only route to that state is pointing `SDLC_CONSTRAINTS` at nothing,
which is a person's own instruction and refuses by name.

**Why the file still wins.** Editing `CONSTRAINTS.md` at the root of this repository and running the
build output takes effect with no rebuild, because the output directory is inside the tree and the
up-the-tree search finds the live file. That is the framework's own development loop and constraints are
what it edits most. Keeping the search ahead of the resource preserves it exactly. The cost is a stray
`CONSTRAINTS.md` somewhere above an installed tool would be preferred over the embedded one — accepted,
because the search ends at the filesystem root and the alternative was making every contributor set an
environment variable or rebuild to see a rule change.

**Why a bad override refuses rather than falling back.** `SDLC_CONSTRAINTS` is a person saying which
rules bind. Falling back to the embedded copy would judge their work against rules they did not choose,
and say nothing about it. One wrong path is a configuration error with a one-line fix; a silent
substitution of the rule set is the kind of thing nobody finds.

**What it costs, stated plainly.**

- **A person cannot read the rules as a file on their machine.** Nothing depends on it — no skill reads
  the global library, every one of them runs `sdlc constraints`, and §7.3 already says rendering is how
  the set reaches a session. What is lost is informal: `grep` over the raw text, and diffing two
  installed versions to see what a revision changed. That sharpens F-40's point that git is where older
  wording lives.
- **`sdlc constraints` names a release rather than a path for the global tier.** `ConstraintDocument.Path`
  became `Source` for that reason: a member named Path holding `embedded in sdlc 0.9.0` is one a caller
  eventually hands to `File.Exists`.
- **A parse failure in the global tier has no file to open.** Near-zero: that is a framework bug rather
  than a person's, and `ShippedLibraryTests` makes it a build failure here before it can ship.
- **The library is fixed at build time.** True of a packed file too, and the thing a release is for.

**Rejected.** Packing `CONSTRAINTS.md` into the `.nupkg` beside the binary, which was the obvious fix
and repairs only the instance: it leaves a binary needing a file it does not control, three places it
might be, and a failure when it is in none of them. There is no arrangement of a copied file in which
the library cannot be missing. Dropping the up-the-tree search in favour of the resource alone: it
would end this repository's own loop, where editing the library and running the build output takes
effect with no rebuild. Falling back to the embedded copy when `SDLC_CONSTRAINTS` names a file that is
not there: it would judge somebody's work against rules they did not choose, silently.

**How it is kept honest.** Three tests assert the embedded copy is the file in this repository, that it
parses to the same set, and that a file still wins over it. None of those would have caught the original
fault, because all of them run inside the tree — so CI now packs the tool, installs it to a temporary
tool path and runs `sdlc constraints` from a directory that is not this repository. That is the only
check in the suite that exercises the artifact a person actually installs.

### D-42 — The framework runs CI on itself, and does not govern itself with itself

**Governs:** §13, §16

**Decision.** `.github/workflows/ci.yml` builds this repository, runs its suite, generates a
scaffolded solution and builds and tests that, and packs the tool and runs the packed tool from
outside the tree. It deliberately does **not** run `sdlc verify-chain` on this repository.

**Why it does not govern itself.** There is no `.sdlc/` directory here and no cycle branch, so the
chain check has nothing to read — `verify-chain` reads hashes, file existence and an approval ledger.
Pointing it at itself would mean adopting the framework into the framework, which sounds principled
and buys nothing: the thing being tested is the gate's own code, and the gate's own tests are what
test that. §2.7 is the related rule.

**Why the repository has ordinary CI at all.** The framework is a product, and a product with no
pipeline is one whose build state is whatever the last person's machine said. Three of the four jobs
exist because the suite cannot see what they see:

- **the scaffolded solution** — every scaffold test asserts strings, so a template generating code
  that does not compile passes the whole suite. It has happened three times now: a `--` inside an XML
  comment (twice), and a `CA1859` that warnings-as-errors turns into an error. F-31.
- **the packed tool, installed and run from outside the tree** — every other check runs the binary
  from a build output inside this repository, where the constraint library is always found by walking
  up. That is how a release shipped a package with no library in it at all. D-41, F-03.
- **`tools/`** — a top-level directory holding build-time tools rather than product code, so that the
  harness is not a test project pretending to be one and not a source project shipping to anybody.

**What this replaces.** All of this lived in `AGENTS.md` and in a YAML header comment, and
`AGENTS.md:45` says `CONCEPT.md` is the current authority on every design question. Whether the
framework governs itself is a design question with real reasoning behind it, and it was recorded in
the one file that says it is not the authority. F-34.

**Rejected.**

- *Adopting ai-sdlc into ai-sdlc* — rejected above. It tests the framework's conclusions against
  themselves rather than testing the framework.
- *No CI, on the grounds that the gate is the enforcement* — rejected: the gate governs a solution's
  changes, and nothing was watching this repository's own build.
- *Putting the harness in `tests/`* — rejected. It is not a test: it writes files and exits, and the
  suite's own rules about test projects would have had to grow an exception for it.

### D-43 — A hook rule is absolute or it is not a rule

**Governs:** §2.4, §6.3

**Decision.** Every rule in the pre-tool hook applies to every agent write, with no mode, no flag and
no check on which skill is running. Guards that need to know the stage live in the skills, and the
documents say so rather than claiming a hook.

**What this settles.** §2.4 said the no-planning-document-edits-from-a-failing-test rule was *"enforced
by the hook rather than by instruction"*, and §6.3 said *"Both guards are hook rules on file paths, not
instructions in a prompt."* Neither was true. `HookPolicy.Decide` has two rules — the protected
files in a file write, and the same four named in a shell command — and no skill-awareness anywhere.
The inversion §2.4 exists to prevent was enforced by nothing, and the concept asserted otherwise twice.
F-08.

**Why the hook cannot do it, which is not the same as nobody having built it.** A hook fires on a tool
call, and a tool call carries no skill identity. There is no field to read. The guards are opposites —
`refine` may edit the spec and not the source, `remediate` may edit the source and not the spec — so a
rule distinguishing them needs to know which stage is running, and nothing in the payload says.

**Why inventing a channel makes it worse.** Every mechanism for telling the hook which stage is running
is forgeable by the thing it constrains, because the agent runs the shell, writes the files and invokes
the commands:

| Mechanism | Why it is not enforcement |
|---|---|
| An environment variable the skill sets | The agent runs the shell, so it can set it. |
| A lock or marker file in the cycle directory | The agent writes files, so it can write that one. |
| A `sdlc stage begin` / `end` pair | The agent invokes commands, so it can invoke the one that lies. |

A conditional rule with a forgeable condition is §2.1's own failure mode — a check that appears to
enforce and does not — and it is worse than the prose it would replace, because prose at least reads as
prose. D-09 rejected the framework's first conditional hook rule for this reason and said where a
bypass would live; this generalises that from one file to the mechanism.

**What the guarantee actually is, stated so nobody has to rediscover it.** Three layers, and the first
is the only one at the keystroke:

- `approval.md`, `abandoned.md`, `constraints.md`, `decisions.md`, `techstack.md` and `solution.md` —
  denied absolutely, in file writes and in shell commands alike. These work *because* they have no
  exception: there is no approve command for one to be carved out for. **Corrected 2026-10-09:** this
  list named four, which was the protected set when it was written; D-67 added the declared stack and
  D-70 the decision record. §5.3 owns the set, and a reader who took this list at its word — which the
  heading above invites — learned a guarantee a third short.
- Everything else in §6.3's table — the skills' own discipline, stated in each skill as a rule rather
  than a railing.
- A planning document edited after approval — caught by the gate, because the ledger records the three
  hashes and `ApprovalGate` refuses on a mismatch and names the document. Slower, certain, and it is
  what stops the inversion reaching a merge.

**What it costs.** The framework claims less than it did. A reader of §6.3 now learns that four of its
rows are discipline, which is the honest position and a weaker one. The alternative was leaving two
sections asserting a mechanism that does not exist, which §2.1's own rule forbids more strongly than it
forbids a weak guarantee.

**Rejected.**

- *A per-skill hook rule, by any of the three mechanisms above* — rejected as forgeable by the
  constrained party. Not rejected as an idea: a host that one day tells a hook which skill invoked the
  tool call, unforgeably, would reopen this.
- *Dropping the guards from the documents altogether* — rejected. They are real rules that the skills
  follow and a person should know; what was wrong was the claim about who enforces them.
- *Making the hook deny every write to a planning document outright* — rejected, and this is the near
  miss. It would enforce `remediate`'s guard absolutely, and it would also stop `refine`, `steer` and
  the three stage skills doing their jobs, since writing those documents is the whole of the process.

### D-44 — The decisions are an appendix to this document, not a file of their own

**Governs:** §20

**Decision.** Every `D-nn` lives at the end of `CONCEPT.md`, sorted by id, with §1–18 carrying
trailing `(D-nn)` markers and each entry carrying `Governs` back. There is no `DECISIONS.md`.

**Why.** Reading order. §1–18 state what is true and should read front to back for somebody new,
which they cannot do if the reasoning is interleaved; and the appendix is reference material you
reach for when challenged, which is a different mode of reading rather than a different document. One
file also means one authority: `AGENTS.md` says this file is the authority on every design question,
and two files would need that sentence to name both and a reader to guess which.

**Why this is recorded late, which is the interesting part.** It was settled on 2026-10-02, in the
session that designed this appendix's own format — before there was an appendix to record it in. A
format decision cannot be written in the format it is establishing, so it went into the conversation
and into the structure and nowhere else. Then on 2026-10-03 somebody reasoned from the same premise
that makes it right — a newcomer should be able to read the design cleanly — and proposed splitting
the decisions into their own file, with §20 at 43% of the document as the evidence. The argument was
sound and the conclusion was already settled, which is exactly the relitigation the `Rejected` field
exists to prevent.

**That is also the argument for the entry.** The test above is whether a competent reader would reach
for the alternative, and this one is not hypothetical: it was reached for, with the whole repository
available, because the record was missing. The cost of the omission was nearly a 1093-line
restructure of the document the framework is specified in.

**What it costs, stated plainly.** §20 is most of `CONCEPT.md` and the longest entry (D-38) is longer
than most sections it governs. A reader scrolling past it to reach §19 pays for the choice, and the file
will keep growing in the half nobody reads front to back. `Governs` is what makes that bearable rather
than solved — if it stops being bearable, the thing to re-examine is whether entries whose rule §1–18
now states can be reduced to a statement, a pointer and their `Rejected` list, which is leaning without
moving anything.

**Corrected 2026-10-09: the threshold fired, the remedy was measured, and it does not work.** This said
43% when it was decided and the appendix reached 61%, so the condition above was met. The lean it names
was then measured: the seven entries whose rule §1–18 now states in full — D-02, D-05, D-06, D-07, D-08,
D-20 and D-31 — come to 131 lines of §20's 3,138. Reducing all seven to a statement and a `Rejected`
list recovers about 4%, taking 61% to roughly 59%.

So the trigger was designed against a cause that turned out not to be the cause. The size is not in old
absorbed entries; it is in the recent long ones, and those are long because they carry arguments worth
keeping — D-38 is the scaffolded layout entire. **The proportion is therefore not the thing to watch,
and this entry no longer states one**: a ratio that cannot be acted on is a number somebody will argue
about instead of the thing that matters, which is the same objection D-66 makes to a ceiling nobody
reaches. What keeps the appendix navigable is `Governs` and the one-fact-one-owner rule, both of which
hold at any size. A length cap on an entry was the other candidate and is refused under `Rejected`.

**Rejected.**

- *`DECISIONS.md` beside `CONCEPT.md`* — rejected for reading order and for the one-authority point
  above. Nothing technical stands in the way: all 36 files that cite a `D-nn` cite it by id and not by
  path, so the move would break no reference. The reason is editorial, which is why it needs writing
  down — a reader can see that nothing would break and conclude, wrongly, that nothing was decided.
- *Interleaving each decision with the section it governs* — rejected: a newcomer would meet the
  reasoning before the rule, and §1–18 would stop being a statement of what is true.
- *Deleting absorbed entries to shorten the appendix* — rejected. Ids are permanent, and the
  `Rejected` lists are the half that exists nowhere else.
- *Renumbering or merging ids* — rejected for the same reason, more strongly.
- *A cap on how long one entry may be* — the candidate that targets the real cause, and refused. The
  length of an entry is the length of its argument: D-38 is 191 lines because it decides the whole
  scaffolded layout, and the `Rejected` lists are the half that exists nowhere else. A cap would be met
  by splitting one argument across two ids, which costs a reader the thing `Governs` exists to give them
  and buys nothing — the same shape as the ceiling D-66 removed, where the number gets argued about
  instead of the rule that actually decides.

### D-45 — A pull request with no governed cycle is refused, and exempting one is branch policy

**Governs:** §3.1, §13

**Decision.** `sdlc verify-chain` on a branch that is not `cycle/<id>` reports that there is no chain
and exits `Refused`. A team that wants such a branch to merge removes the check's *required* status
for it in branch policy, rather than removing the step.

**Why this needed deciding at all.** §3.1 says every class of change runs the full chain and that
intent, spec and plan are always required. It never said what the gate does when a change turns up
without them — and the generated pipeline triggers on every pull request, so it meets those changes on
the first day of adoption. Dependabot, a revert, a hotfix: none of them is named `cycle/<id>`.

**What it did before.** It exited `Usage` — "the command line could not be understood" — because the
branch name failed to parse as a cycle id, on the shared path every typed cycle command uses. So a
bot's pull request showed a complaint about argument syntax.

**Why the exit code mattered more than the message.** Reported as a usage error, a red check on a
dependabot pull request looks like a misconfigured tool, and the reasonable response to a misconfigured
tool is to delete the step that is complaining. That step is the one layer nobody can bypass (§2.1), so
the worst consequence of the wrong exit code was never the red check — it was the step being removed to
stop it, which turns the guarantee off everywhere and leaves nothing recording that anybody chose to.

**Why `Refused` and not `Ok`.** `Refused` is defined as "a rule refused what was asked, the gate
working as intended", which is exactly what happened. Exiting `Ok` would have been frictionless and
would have made the branch name the bypass: anything merges unverified if you do not call it
`cycle/<id>`, and §2.1's promise would hold only for the people who opted in.

**Why branch policy is the right place for an exemption.** §2.1 already names the non-bypassable layer
as two things — this check **plus branch policy**. So the tool states what is true and the team decides
what to require of which branches. An exemption recorded there is visible, reviewable and scoped to a
branch pattern; the same exemption made by deleting a YAML step is invisible and total.

**What it costs, stated plainly.** Every dependency bump is red from the day a team adopts, until they
either open cycles for bumps or exempt the pattern. That is friction arriving at the worst moment —
first impressions — and it is the price of the promise being the product. Two softer things make it
bearable rather than solved: the refusal explains both ways on, and the generated pipeline carries a
comment saying that excluding the check is a decision and deleting it is a different one.

**Rejected.**

- *Exiting `Ok` on a non-cycle branch* — rejected above: it makes the branch name a bypass, and it
  would require §3.1 to be rewritten to say changes may go ungoverned, which is a different framework.
- *Keeping `Usage` and improving only the message* — rejected. Exit 2 means the gate was misused, so CI
  and anybody reading it still sees a broken tool, and the step stays the thing most likely to be
  deleted. The message was the smaller half of the defect.
- *A branch filter in the generated workflow, skipping bot branches* — rejected: it hard-codes one
  team's exemption into every adopted repository, puts the policy in a file the framework owns and
  rewrites on update (D-40), and makes the quiet default "ungoverned changes merge".
- *Teaching the framework which branch patterns are ordinarily ungoverned* — rejected as the same
  thing with more machinery. The list would be a guess about other people's conventions, and a wrong
  guess is an exemption nobody asked for.

### D-46 — A host is a row in a table, and what it supports is asserted rather than claimed

**Governs:** §12, §12.1

**Decision.** Everything the framework needs to know about an agent host is one row in
`SupportedHosts`: the names it answers to, where its hook configuration goes, whether the framework owns
that file or only its entries, what it calls the pre-tool event, what it calls the turn end **or that it
has none**, and what it calls the per-hook timeout. The configuration is assembled from the row,
adoption loops the table, and the command line reads the row rather than naming a host. Adding a host is
that row, a branch in each of `HookProtocol`'s two write methods, and a captured payload per documented
payload shape in the conformance test.

**Why this needed deciding at all.** Those facts were spread over four mechanisms that each hardcoded
hosts separately — the spellings in `HookProtocol`, the configuration's content in a string-keyed switch
in `Templates`, its path and its ownership written out twice in `Initialise`, and the turn end's
availability as a refusal naming Copilot in the command line. Nothing joined them, so "do we support host
X" was a judgement somebody made by reading four files, and a host half-added looked exactly like a host
supported.

**What it got wrong, which is the actual argument.** Two defects, both of the shape this framework exists
to refuse — a directory that appears enforced and is not.

- **The turn end was claimed to be Claude-only.** Copilot has had `agentStop` throughout, with the same
  `{"decision": "block", "reason": ...}` shape Claude Code's `Stop` uses. `WriteTurnEndDecision` had even
  been written against the day the event appeared. Meanwhile the command line refused
  `--event turn-end --host copilot` by name, the generated configuration wrote no block, and six
  documents asserted the absence. Every one of those was downstream of one unverified sentence.
- **The pre-tool hook read one of Copilot's two payload shapes.** The CLI writes `toolArgs`; the reader
  looked for `tool_input` and `toolInput`. So on Copilot CLI it found no path and no command, classified
  every call as harmless, and allowed an agent to write an approval ledger. The conformance test passed,
  because the captured payload was the VS Code one.

Neither was a coding mistake in a hard place. Both were a fact about a host living somewhere nothing
checked it.

**Why a table rather than better discipline.** The table makes the support claim mechanical: the suite
enumerates the host enum, asserts every member has a complete row, asserts each host's generated
configuration declares exactly the events its row names and no others, and asserts each host denies a
write to a protected file when fed a payload in its own shape. A row that lies now fails a test. The
previous arrangement had no place for such a test to stand, which is why five releases of documentation
agreed with each other and not with the host.

**Why the payload reader is deliberately left out of it.** `ReadCall` collects keys from every spelling
any host is known to use, not from the asking host's row. That is the opposite of normalising, and on
purpose: these surfaces are young, and a reader narrowed to one host's documented set turns a renamed
field into a rule that silently stops applying. The fix for the `toolArgs` defect was a third spelling in
the shared list, not a per-host key set.

**Why the decision shapes stay as code.** The two write methods remain typed switches on `HookHost`
rather than data on the row. Nesting, key names and the refusal word are JSON structure, and Gemini
already breaks the pattern by spelling its turn-end refusal `deny` where both current hosts spell it
`block`. Modelling that as fields would produce a shape language nobody can read, and a switch has the
better failure mode anyway: a row added without a branch throws from `SupportedHosts.Of` or falls through
the switch, and a test catches it, where a missing data field would quietly write an empty object — which
is how an allow is spelled.

**What the row deliberately does not carry yet.** Where a host reads skills from, and its non-interactive
invocation for the fresh reader `interrogate` needs. Both are real facts about a host and neither has a
consumer in the binary, so adding them now would be adding fields nothing reads — the thing this change
was paired with removing elsewhere. They go in with the work that consumes them.

**Rejected.**

- *A branch per supported host, with a shared core branch* — rejected, and it is worth recording why
  because it is the reach somebody has already had. Branches model divergence intended to converge, and a
  host branch never converges, so it is permanent merge debt across the whole repository to separate
  about one percent of it. Worse, it removes the place the assertions stand: `PluginTests` and the
  conformance suite hold cross-cutting claims about every host at once, and on separate branches those
  either live on the core branch where they cannot see the hosts, or are duplicated per branch with N
  chances to drift. There is one binary by design (§12), so either the host branches hold no code — in
  which case they wanted to be directories — or there are N packages of one tool and `sdlc version` stops
  having an answer. And a gap would stop being visible: today a missing Copilot line sits next to the
  Claude line in a file somebody reads, where on a branch it is an absence in a tree nobody checked out.
- *A separate holder per host — `ClaudeHost`, `CopilotHost`, each with its own methods* — rejected. It
  reads better at one host and worse at three, because the question people actually ask is "what differs
  between them", and the answer is then spread across as many files as there are hosts. A table answers
  it by being read down a column.
- *Driving the payload reader from the row too* — rejected above. Consistency would cost the tolerance
  that keeps a renamed field from disabling a rule, which is the more expensive failure by a wide margin.
- *Modelling nesting and command spelling as two orthogonal fields* rather than one `HookEntryShape` —
  rejected as abstraction ahead of a second user. Two hosts, two shapes, and a third host reuses whichever
  matches; a combination nobody has asked for costs a reader two questions where one will do.
- *Leaving the generated configuration as hand-written lines* — rejected once the content became
  table-driven anyway. A configuration assembled a line at a time can carry a stray comma, and a hook
  configuration that fails to parse is a hook that never runs; there was a test for exactly that risk,
  which assembling from objects retires.
- *Writing a turn-end block for a host whose event is unverified* — rejected, and it is the one field here
  that must never be guessed. `TurnEndEvent` being null is a real answer, the command line refuses that
  combination by name, and a plausible-looking block for an event that cannot fire is the failure the whole
  design is written against. The correction above is not a licence to assume the next host has one.

### D-47 — The package carries both plugin manifests, and conformance is not an install

**Governs:** §12.2

**Decision.** A root `plugin.json` makes this an Agent Plugins 1.0 plugin alongside the Claude Code
plugin it already was. Both systems discover skills in `skills/` at the package root, so there is one
copy of them and nothing translates between them. Every skill additionally declares the
specification's optional `compatibility` field, naming the binary it needs. The readme says the Copilot
install has never been run, because it has not.

**Why this needed deciding at all.** The readme said packaging the skills for Copilot was not done, and
that *"guessing at a format would produce files that silently do nothing"*. That was the right call when
written and stopped being true in August 2026: Agent Plugins 1.0 is an open standard from OpenAI with
AWS, Microsoft, Cursor, Vercel and Google, with a published closed schema and six implementing clients.
There is nothing left to guess.

**Why it cost one file.** The skills were written for one host and already satisfy the open
Agent Skills specification — names lowercase-hyphen and matching their directories, descriptions well
inside the limits — and Claude Code puts plugin component folders at the package root, exactly where the
standard looks. So the whole of the work was a manifest with two required fields. Nothing moved, which is
the strongest evidence available that neither format is doing anything exotic.

**Why both manifests rather than migrating to one.** Anthropic is not in the coalition and Claude Code's
plugin format is unchanged and documents no awareness of the standard. Claude Code is also the host this
framework is developed and exercised on. Dropping `.claude-plugin/` to be standards-pure would trade a
working install for a conformant one; carrying both costs a file and a test that they describe the same
plugin.

**Why `compatibility` is used where the specification says most skills do not need it.** Every one of
these skills shells out to a binary the package does not carry, and §12.2's point about explicit
invocation is why that matters here: on Claude Code the install guide is read because somebody ran a
plugin command, while a client that discovered the skill by matching its description has nothing that
would ever mention a .NET tool. The field is the only place that fact can travel with the skill.

**Why conformance is reported as untried.** The package now satisfies a schema, and a schema is not a
session. Nobody has run `copilot plugin marketplace add` against this repository and watched the
skills appear, and the gap between "conforms" and "works" is exactly where D-32 put the Azure DevOps
pipeline — so it is recorded on the same terms, in the readme, where somebody deciding whether to adopt
reads it.

**Rejected.**

- *Copying the skills into each governed solution's `.agents/skills/`* — rejected, and it was the plan
  until the standard turned up. `.agents/skills/` is the cross-client path for skills belonging to **one
  repository**; the framework is installed once and governs many, so per-solution copies would put
  a directory of framework text per skill into every adopted repository, to be refreshed by `init`
  forever. A package installed once is the shape the thing already has.
- *`.github/skills/` instead* — rejected as the same idea, narrower: it is Copilot-only where
  `.agents/skills/` is at least a cross-client convention, and both are beaten by a package.
- *Rewriting `allowed-tools` into the specification's syntax* — rejected. The specification defines it as
  a space-separated string and marks it experimental with support varying by client; Claude Code's is
  comma-separated with a different inner form, and no single string satisfies both. Changing the syntax
  that works on the host this is developed on, for a field the standard itself calls experimental, trades
  a working pre-approval for a hypothetical one. The cost of leaving it is a permission prompt per command
  off Claude Code, which is friction rather than failure, and §12.2 says so out loud.
- *Dropping `argument-hint`* — rejected for the mirror reason. It is not a specification field, clients
  that do not know it warn and load the skill anyway, and it is genuinely useful on the host that reads
  it. An unknown field costs a log line; removing it costs the hint.
- *Claiming Copilot support in the readme on the strength of conformance* — rejected, and this is the
  entry's real point. "A host that is listed is a host that was exercised" is the rule D-46 established
  one commit earlier, and a manifest matching a schema is not an exercise. Writing it as supported would
  be the same defect that decision was written to close, committed knowingly rather than by accident.
- *A second marketplace catalogue for Copilot* — rejected as unnecessary rather than wrong. Copilot looks
  for `marketplace.json` in `.github/plugin/` and falls back to `.claude-plugin/`, so the catalogue
  already in the repository is the one it reads. A second copy would be a second place the plugin list
  lives.

### D-48 — Used only by the suite is not the same as dead

**Governs:** §16

**Decision.** A member nothing in the binary calls is removed **unless** it is the single statement of a
convention the binary depends on, or the inverse of something the binary produces. Those two stay, and
each says in its own remarks why it has no production caller — so the next reader, or the next dead-code
pass, does not have to work it out again.

**Why this needed deciding at all.** A reference analysis over the whole repository turned up around
fifty members in `Sdlc.Core` that nothing outside the suite names. "Delete what only tests use" would
have taken two things with it that are load-bearing:

- `Cycle.FindingsPath` is where the name of a findings file is decided. Rounds are written by skills and
  read back by glob, so the binary never needs one by name — but the suite writes its fixture rounds
  through it, which is what makes a change to the convention break the glob's tests instead of silently
  passing against files the reader no longer finds.
- `AbandonRecord.Read` parses a file the binary only ever tests for existence, because presence is what
  stops a cycle. What it is for is reading back what `sdlc draft abandon` prints: a drafter whose output
  cannot be parsed prints something nobody can use, and this is the only thing that would notice.

**And the opposite case, which is the common one.** `FindingsRound.IsClean` answered "did this round
raise a blocker" and only an assertion ever asked. It was also *wrong* as a way to answer the question
anybody cares about: a finding keeps its id across rounds, so a blocker opened in round one and answered
in round two is not open because round one said so. `FindingsReader.StandingBlockers` is the fold the
gate and the status panel both read, and its own remarks say there must be exactly one of it. So the
suite was exercising a second definition of "open" that no caller used, which is worse than no test —
it reads as coverage.

**The test for which is which.** Does something outside the suite depend on this being right? For
`FindingsPath` the glob does; for `AbandonRecord.Read` the drafter's output does. For `IsClean`,
`RemediationState.Remaining` and `HookVerdict.Path`, nothing did.

**Why the duplicates were the valuable half.** Four of the removals were not unused code but a *second*
implementation whose unused copy was the better one: `Sdlc.Cli` had its own `Git.UserName` that never
received the deadlock fix or the error-reporting fix `GitRepository` got; `ApprovalGate` wrote out the
comparison `ApprovalLedger.IsApproved` already names; `PullRequest` assembled the escalation sentence
`RemediationBudget.Describe` exists to build, down to its own pluralisation; and adoption's commit went
through `new GitRepository()` rather than the container, which made the one step that writes to git the
one step no test could substitute. In each case the fix was to use the thing already there, which is why
a dead-code pass is worth running at all — the dead member is the symptom and the live duplicate is the
defect.

**Rejected.**

- *Deleting everything the binary does not call* — rejected above. It would have moved a naming
  convention into test code and removed the only check that a drafter's output can be read back.
- *Keeping everything, on the grounds that a test is a caller* — rejected. A test over a member nothing
  else uses asserts a behaviour no caller has, and it reads as coverage of the real path while the real
  path goes unexercised. `IsClean` is exactly that, and the assertions now go through
  `StandingBlockers`, which is what the gate reads.
- *An attribute or a naming convention marking "exists for the suite"* — rejected. It is a handful of
  members, the reason differs in each case, and a marker would say "deliberate" where the remarks say
  why. A reader who disagrees with a sentence can argue with it; a reader who meets an attribute can
  only obey it.
- *Removing the record fields a renderer never reads* — rejected for now rather than decided. Several
  responses carry a field nothing prints — `PullRequest.Response.Chain` holds which chain check failed while
  the renderer says only that the chain does not hold — and in those cases the honest reading is that
  something *should* read it, so removing the field would be deleting the evidence of a thin output
  rather than cleaning up. They are listed where the work is tracked, not quietly dropped.
- *Removing the artifact-format fields nothing reads* — rejected. `Finding.Locus`, `Finding.Remedy` and
  `Disposition.Withdrawn` are §7's documented format, written by the interrogate skill; a reader of a
  findings file wants them whether or not the gate does.
  `DispositionBy` and `DispositionAt` are the live edge of that: §7 says disposition transitions carry
  who and when, and no skill asks for either field, so the gap is in the skill rather than the record.

### D-49 — A blocking finding is cleared by a fresh reader's silence, never by the author's claim

**Governs:** §6.1

**Decision.** A finding leaves `open` when the next round's fresh reader, judging the current document,
does not raise it: `answered` where the document changed to address it, `withdrawn` where it was dropped
because the objection was wrong. A finding the reader raises again stays open. A finding nobody can
place in either case stays open. The round records the transition because it holds the history; the
reader's silence is the evidence, and the reader is never asked for a disposition.

**Why this needed deciding at all.** §6.1 said a disposition exists, what its three values mean, and
that the latest round mentioning a finding says where it has got to. It never said what moves one — and
no skill said either. `interrogate` documented the silence rule in one direction only, that a round
mentioning nothing about a finding has not answered it, which is correct and is half a mechanism.
`refine` correctly refused to touch a round file. So nothing anywhere told any round to write
`answered`, and `answered` is the only exit D-27 leaves open.

**What it cost, which is how it was found.** A cycle run for real under iteration 3, the worked example
this repository calibrated against, carried three blocking findings — one each on the intent, the spec
and the plan — raised in round one, answered in the documents, and never recorded as answered. Rebuilt
inside a governed solution and put to the current binary, its intent panel read `round 2 - 1 blocking`
with `r1: 1 blocking - r2: clean` beside it. The cycle completed in anger and
could not pass today's gate. A record that only accumulates objections is not a record of a cycle.

**Why the reader's silence rather than the refiner's report.** The refiner is the session that may have
written the document, and a disposition it could set on its own reading is a blocker an author clears by
asserting it is cleared — the one inversion the whole interrogate-refine split exists to prevent (D-01,
D-29). Silence from a reader with no stake is weaker evidence than an explicit verdict and it is the
strongest available, because a reader that was asked "is F_01 answered?" would have been told F_01
exists, which is the investment bias the fresh process was bought to remove.

**Why the refiner's report still matters.** It is what distinguishes `answered` from `withdrawn`, which
reader silence alone cannot: both look like a reader not objecting, and the difference is whether the
document changed for that finding. So `refine` names every id it was given in three groups — changed,
disagreed-with, or asked — and a report that omits an id leaves the next round unable to place it, which
by the rule above means it stays open.

**Why unsure stays open.** The two failure directions are not symmetrical. A finding wrongly left open
stops a build and somebody reads it and acts; a finding wrongly cleared is a blocker that stopped
existing with nothing recording that it ever did. The second is the shape this framework refuses, so
ambiguity resolves to the expensive side.

**Rejected.**

- *Letting the refiner set dispositions* — rejected above, and it is the reach somebody has when a
  blocker will not clear. It would make the author the judge of its own corrections, which is the one
  thing the fresh process is for.
- *Letting a person waive a blocker at the gate* — rejected by D-27 and not reopened here. The gate is
  the place a person accepts three documents, not the place an objection to them is dismissed. The
  recovery path for a cycle that cannot answer a finding is abandonment or a recorded reopen, both of
  which leave a record.
- *Treating a round's silence about a finding as clearing it* — rejected, and this is the rule that was
  already right. A round that forgot to mention an objection and one that cleared it would be the same
  file, so the cheapest possible mistake would read as the strongest possible outcome.
- *Having the fresh reader report dispositions directly* — rejected. Giving it the earlier findings to
  disposition would hand it the ids, the statements and the history, which is most of the context the
  new process was opened to withhold. It judges the document; the round does the bookkeeping.
- *A counter or a flag on the cycle recording "blockers answered"* — rejected for the reason D-27's own
  entry gives about waivers, and the reason `RemediationBudget` counts rounds rather than keeping a
  tally: a count kept beside the thing it counts can disagree with it, and the cheap one to forge is the
  one that would be trusted.

### D-50 — A host is supported only while its product serves requests, and Antigravity is not a row

**Governs:** §12

**Decision.** A row in `SupportedHosts` requires two things, not one: a surface held by a conformance
test, **and** a product that still answers. Gemini CLI is removed because the second stopped being true.
Antigravity CLI is not added in its place, because its surface breaks three assumptions the table does
not carry and one the whole hook design rests on — it is a piece of work, not a row.

**Why Gemini is out.** Google retired Gemini CLI on 18 June 2026, consolidating its tooling under
Antigravity, and its own documentation says so. The row had been committed on 3 October 2026, which is
three and a half months after the product stopped serving requests, so adoption was writing
`.gemini/settings.json` to configure a hook for a program that could not run. That is the framework's own
central failure — a directory that appears enforced and is not — reached from a direction nobody had
thought to check: the vocabulary was researched carefully from the host's live documentation, and the
question "is this product still shipping" was never asked.

**What the episode is worth keeping.** Two things, and this is why the entry exists rather than the row
simply being reverted.

- **A key's name is not a description of a field.** Gemini spelled the per-hook timeout `timeout`,
  identically to Claude Code, and read it as milliseconds against Claude's seconds. One shared number
  under one shared key would have given it a ten-millisecond budget: a hook killed before it can answer,
  on every tool call, with the configuration looking correct. The unit was briefly a field for that
  reason and is gone with the host, because both remaining hosts read seconds and a field with one value
  is the dead schema D-48 is about. `SupportedHost.TimeoutKey`'s own remarks carry the lesson so the next
  such host is met with a field rather than an assumption.
- **The write methods were right to stay code.** D-46 kept the two decision shapes as typed switches
  rather than data, on the argument that nesting and field names are structure. Gemini then produced a
  third shape no arrangement of the first two's field names could express, and Antigravity produces a
  fourth. A row of shape-fields would by now be a format language with three dialects and no reader.

**Why Antigravity is not a row.** Its hook surface, from Google's own reference, differs from every host
the table describes in four ways, and the last one inverts a rule rather than adding a case:

- **The payload is nested.** `toolCall.name` and `toolCall.args`, where every host so far puts the tool
  name and its arguments at the top level under some spelling. `ReadCall` reads top-level keys, so this
  is the first host to need a path-aware reader.
- **The config has a named-group wrapper.** `{"<hook-name>": {"enabled": true, "PreToolUse": [...]}}`,
  where the three hosts handled so far put the events directly under a `hooks` object. `HookSettings.Merge`
  works on that object, so merging into `.agents/hooks.json` is not the merge that exists.
- **The turn end is inverted and has no loop guard.** `Stop` sends the turn back with
  `decision: "continue"` and lets it go on any other value — the opposite of writing a refusal and
  staying silent otherwise. And it carries no `stop_hook_active`: the fields are `executionNum`,
  `terminationReason` and `fullyIdle`, so the guard that stops a refused turn becoming an endless one has
  to be rebuilt from a different fact.
- **`decision` is required on every tool call, including the ones the policy has no opinion about.** This
  is the one that matters. D-39 decided that a refusal is written and an allow says nothing at all,
  because an explicit allow bypasses the host's own permission system. On Antigravity the empty object is
  not silence — reports of agy denying every tool call on a hook that returns `{}` are exactly that
  shape — and the neutral value is `ask`, documented as prompting while respecting cached permissions.
  So D-39's *principle* survives, because `ask` there is "defer to the host's own permission flow", but
  its *spelling* inverts: the thing to write when there is nothing to say is a value, not an absence.
  That is a field on the row and a paragraph in D-39, not a branch.

**What would be needed, stated so the next person does not re-derive it.** A path-aware payload reader,
an allow-shape on the row, a third entry shape with a file-level wrapper, a merge that understands the
named-group layout, a turn-end inversion with a guard built on `executionNum` or `fullyIdle`, and
conformance payloads for all of it. Comparable in size to the commit that introduced the table.

**Rejected.**

- *Keeping the Gemini row because the adapter is harmless* — rejected. It is not harmless: adoption
  writes the file, the readme listed the host, and a person could reasonably read all that as support.
  An adapter for a dead product is the most expensive kind of dishonesty here, because nobody will ever
  get a failure from it.
- *Adding Antigravity in the same change that removes Gemini* — rejected, and this is the discipline the
  episode argues for. Its surface needs the same verification the others got, from its own reference
  rather than from the claim that hooks "migrate unchanged", and most of the evidence about the
  empty-object behaviour is version-specific bug reports rather than documentation. Writing a row from
  that would repeat the mistake with a different fact.
- *Trusting "hooks, subagents and skills migrate unchanged"* — rejected as the specific sentence that
  would have caused it. It is Google's migration guidance and it is true at the level of concepts; the
  event names, the config path, the payload shape and the allow value all changed.
- *Treating the retirement as a reason to narrow the framework to one host* — rejected. The adapter cost
  Copilot one row and found two real defects doing it. The lesson is to check that a product serves, not
  to stop being portable.

### D-51 — Silence is the principle; where a host forbids silence the neutral word stands in

**Governs:** §12, §12.1

**Decision.** D-39 said a refusal is written and an allow says nothing at all. Antigravity requires a
`decision` on every pre-tool call, so nothing is not an available answer there. The framework writes its
**neutral** value instead — `ask`, documented as prompting while respecting the person's existing
grants — which is what silence accomplishes on the other two hosts: the question goes back to the
person's own permission flow rather than being answered by the gate. The word is a field on the host's
row, `NeutralDecision`, and null on that field means silence. D-39's principle is unchanged; only its
spelling turned out to be host-specific.

**Why D-39 could not simply be applied.** Its reasoning was about *overriding the host*: an explicit
`permissionDecision: "allow"` is not silence, because Claude Code documents it as bypassing the
permission system — so a policy with no opinion would stop the host asking a person about the very tools
the hook matches. On Antigravity the five values are `allow`, `deny`, `ask`, `force_ask` and
`deny_unless_prior_grant`, and `decision` is mandatory. `allow` is the same bypass D-39 refuses.
`force_ask` is the same override in the restrictive direction, which D-39 also considered and rejected.
`ask` is the only value that leaves the person's own settings deciding, so it is the only one that means
what the empty object means elsewhere.

**Why this is not a loophole in D-39.** The thing D-39 protects is the host's own gate, not the literal
emptiness of the output. Writing `ask` where the host has no empty option preserves what D-39 is for; the
alternative — writing `allow` because it is nominally the "nothing is wrong" answer — would destroy it,
on a host where it is also reported not to work.

**Why the turn end is not governed there.** Antigravity has a `Stop` event, and sending a turn back is
`decision: "continue"`. But a turn-end refusal is only safe where the payload says the agent is *already*
continuing from a previous refusal: without that, the gate refuses, the host re-enters the loop, the gate
refuses again, and the turn never ends. Claude Code and Copilot both supply `stop_hook_active`.
Antigravity supplies `executionNum`, and its own documentation says that increments per attempt without
signalling prior continuations, plus `fullyIdle`, which is about background work rather than about
retries. Neither is a guard somebody should infer. So `TurnEndEvent` is null for that host — the first row
to use that value for its documented purpose — adoption writes no such block, and the command line
refuses the combination by name. The shape for the day a guard is documented is written and tested
anyway, so the change then is a row field rather than a new branch.

**Why a third ownership mode.** Antigravity's `hooks.json` is a map of named hook groups rather than one
hooks object, so the framework owns the key it named and every sibling key is a hook somebody else wrote.
That is cleaner than Claude Code's case, where the framework owns entries *inside* a shared object and has
to find them by the command they run. `ConfigOwnership.Section` replaces one top-level key and leaves the
rest of the file untouched, which also makes an uninstall the removal of a key.

**What is not established.** No Antigravity session has had a tool call refused by this. The adapter is
conformance-tested against Google's hooks reference and the generated configuration matches the documented
schema, and that is a different claim — the same distinction D-32 draws for the Azure DevOps pipeline and
D-47 for the Copilot install. Two host properties also sit outside the framework's control and are
recorded in the install guide rather than worked around: the workspace configuration loads only in a
folder the person has marked trusted, and the skills arrive through that host's own plugin mechanism
rather than through Agent Plugins.

**Rejected.**

- *Writing `allow` on a call the policy has no opinion about* — rejected twice over. It is the bypass
  D-39 exists to refuse, and it is independently reported not to permit the call on that host, so it would
  be both wrong in principle and broken in fact.
- *Writing `force_ask`* — rejected. It prompts even where a person has already granted, which overrides
  the host in the restrictive direction. D-39 weighed exactly this for `ask` on Claude Code, where `ask`
  has that behaviour; on Antigravity the two values are distinct and `ask` is the deferential one.
- *Treating the empty object as good enough and letting the host decide what to do with it* — rejected.
  Reports of that host denying every tool call on a hook returning `{}` are the shape of the risk, and a
  gate that refuses every call is not a safer failure than one that permits them: it makes the agent
  unusable, and the response to an unusable hook is to delete it.
- *Governing the turn end on an inferred guard* — rejected. `executionNum > 1` is a plausible reading of
  an undocumented integer, and a wrong reading is an endless turn: the worst possible failure for a hook
  that fires when somebody is waiting for their work to finish.
- *Owning `.agents/hooks.json` outright, because the framework writes a whole section of it* — rejected
  by D-40. The path belongs to the host and a team's own groups live beside the framework's, so owning the
  file would destroy them and report it as "updated".
- *A fourth entry shape for Antigravity's flat-list events* — rejected as unnecessary. `PreInvocation`,
  `PostInvocation` and `Stop` take a flat list of handlers where `PreToolUse` takes the grouped form, and
  the framework configures none of the flat ones. A shape with no writer is a shape nothing checks.

### D-52 — A gate decision is drafted to a file and placed by renaming it

**Governs:** §5.3

**Decision.** `sdlc draft approval` writes the **complete** ledger it would produce to
`approval.<decision>.draft.md` in the cycle directory, beside the ledger it would replace, and prints
exactly those bytes with the rename that records them. The person renames it. Nothing else changes: the
pre-tool hook still denies every agent write to the ledger, now including every spelling of a move onto
it, and there is still no command that places one.

**Why.** The flow it replaces asked a person to splice two blocks printed at two different
indentations — the entry at its real YAML indent, a header template at a four-space display indent, with
a `<the entry above>` placeholder between them. Placing a first approval therefore meant dedenting one
block and not the other and substituting by hand, through a terminal and a clipboard. A header at indent
two above a `decisions:` at indent nought reads correctly and parses as nothing, which is what arrived:
`Did not find expected <document end>`, no line number, on the one file in the system a person writes.

**What made it a trap rather than a nuisance.** The drafter read the ledger before printing, so a
mis-placed entry took `draft approval` and `status` down with it — the one command that would have
handed over the correct text refused to run until the YAML was fixed by hand. The fix for that is
separate and stands on its own: an unreadable ledger is a gate **refusal**, not a failed result, for the
same reason `UnreadableRound` is. "Could not tell" must never read as "clean", and the recovery path has
to survive the most likely malformation.

**Why the binary writes the draft and not the skill.** An agent allowed to write it would be authoring
approval text with its own hands, and the hook would need a rule naming which file beside the ledger an
agent may write — the first conditional hook rule, which D-09 says is where a bypass would live. A
command a person's shell ran moves no rule.

**Why the decision is in the file name.** The rename is the act, so it is where the decision has to be
legible: somebody moving `approval.denied.draft.md` types `denied` on the way. A single
`approval.draft.md` would let an approval and a denial look identical at the moment of recording one,
and a rename is a lower-attention act than transcribing ten lines — which is the one real thing the
clipboard bought.

**Why a revisit reproduces what stands, verbatim.** The ledger is append-only and the last entry stands,
so a rename that replaced a one-entry ledger with a one-entry file would discard a decision. The draft
therefore carries every recorded entry above the new one, copied fence to fence rather than re-emitted
from the parsed entries: a reason is prose a person wrote, and round-tripping it through a YAML emitter
is how a quoted colon becomes a different document. This also retires the second cost §5.3 used to
name — a first entry being a creation and a revisit an edit — because both are now one rename.

**Rejected.** *A skill writing the draft* — D-09's conditional rule, above. *An `sdlc approve` command* —
precisely the exception D-09 refuses to create; the hook rule set is absolute because there is nothing
for an exception to live in. *Keeping the paste and merely printing the whole file at one indentation* —
considered, and it would have prevented this particular failure. Rejected as the lesser half: the
clipboard round trip adds no governance, and a terminal that soft-wraps or a copy that picks up a prompt
prefix reintroduces the same class of defect. The file is the artifact; printing a picture of it and
asking for a transcription was the mistake. *A single `approval.draft.md`* — above. *Re-emitting carried
entries from the parsed ledger* — above.

### D-53 — A solution may be local, and the platform is recorded rather than inferred

**Governs:** §5.3, §9, §15.2

**Decision.** `--platform` takes a third value, `local`, for a repository with no remote. It writes no
pipeline. The solution document gains a third key, `platform`, which adoption writes; absent, it reads as
GitHub. `doctor` reports the guarantee as `elsewhere` on a local solution, naming what is missing and the
command that installs it, instead of reporting a problem.

**Why.** Adoption offered two platforms and defaulted to one, so somebody trying the framework on a
repository with no remote had to name a service they were not using. That wrote a pipeline nothing would
ever send a request to — the same failure D-32 names about an unrun pipeline, which "looks exactly like a
gate" — and then `pr` reached for `gh` or `az` with nowhere to push.

**Why the key, and not inference.** A solution with no pipeline is either local by choice or a GitHub
install that is broken, and nothing on disk tells those apart. Inferring meant `doctor` reporting every
local repository as missing the one check that is the guarantee: true, useless, and indistinguishable from
the report that means something. Recording it is what lets the diagnosis be a sentence rather than a
guess.

**Why absent reads as GitHub, which is the strict reading.** Every solution adopted before the key existed
chose GitHub by default, so any other reading would silently reclassify them and exempt them from a check
they are relying on. A *misspelled* value still fails — `gitub` is a typo, and reading it as GitHub would
hide it. The same reasoning puts GitHub in the fallback when the document cannot be read at all: an
unreadable file must not buy an exemption.

**What this deliberately does not do.** It does not make the guarantee optional in a solution that has a
remote, and it does not soften what is lost. §13's pull-request check is still the only non-bypassable
layer, and since D-78 nothing on one machine stands in for it. So `doctor` says so on every run, the
`init` skill says it once when the choice is made, and the `pr` skill says it again when somebody
reaches the point of wanting a remote — three places, because a cost stated only at adoption is a cost
nobody remembers paying.

**Rejected.** *Inferring the platform from the git remote* — a remote can be added without adoption being
re-run, so the inference would be stale exactly when it mattered, and a repository with two remotes has no
single answer. *Writing a pipeline anyway and letting it sit unreachable* — D-32's failure. *Treating local
as a `Problem` in `doctor`* — a check that reports a correct install as broken is a check people learn to
skip, and it would take the real problem with it. *Making it a `Sound` pass* — the opposite error: the
guarantee genuinely is not installed, and a green tick saying otherwise is the one thing this document
opens by naming. `Elsewhere` is the outcome that already means "real, and not the binary's to settle".
*A fourth key recording the remote URL* — adoption does not need it, `git` already owns it, and a copy
would go stale.

### D-54 — The scaffold maps a liveness route, and a fitness function keeps it

**Governs:** §11.1, §11.5

**Decision.** The scaffolded host registers health checks and maps `/health`. An architecture test,
`The_host_still_answers_whether_it_is_up`, reads the host's source and fails the build when either the
registration or the route is gone. `techstack.md` declares it, and it is the one row there that binds.

**Why enforced rather than declared.** A `techstack.md` row is a default a plan may depart from by saying
why (`CON_13` — named here because that is the rule this decision was taken against; `CON_15` is the rule
today, and D-59 records why), which is right for a database or a UI framework and wrong here, because the cost of the
omission is not paid by the person who chose it. A process with no liveness route is one an orchestrator
cannot distinguish from a process that is up and broken, so it keeps routing traffic to it — and that is
discovered in production by somebody who did not delete the line. That asymmetry is exactly what a
fitness function is for, and it is the same argument D-36 makes about a ban that ships with the surface it
governs.

**Why it is not an `ApiResult` endpoint, and has no `Endpoints` holder.** `Every_endpoint_returns_the_envelope`
requires any holder named `*Endpoints` to return the envelope. A health route's caller is a load balancer
or an orchestrator, which understands a status code and the framework's own payload and has never heard of
this solution's envelope — so routing liveness through it would make the one caller nobody can change
parse something unfamiliar. Going through the framework's own `MapHealthChecks` means the envelope rule
needs no exception, and an exception is where the next leak goes.

**Why the registration is checked as well as the route.** `MapHealthChecks` without `AddHealthChecks`
throws at **startup**, not at build, so a half-removal would pass the build and fail when the process
came up — the failure mode furthest from the edit that caused it.

**Why it reads source rather than reflecting.** The route is wired inside `Main` and no type records that
it happened. The same shape as `The_frameworks_response_factories_are_still_banned`, for the same reason:
what is worth defending is a line somebody can delete, and a rule that cannot see the deletion is prose.
The rule asserts the host file exists before reading it, so a moved host fails loudly instead of
inspecting nothing and passing.

**Rejected.** *A `LOC_` constraint* — binds one solution, and this belongs to every solution the profile
scaffolds. *A global `CON_`* — forbidden: no global constraint may name a framework, and health checks are
ASP.NET Core's. *Readiness checks alongside liveness* — a readiness probe asserts dependencies are
reachable, and the scaffold has no dependencies to assert; shipping an empty one would be a check that
cannot fail, which is the vacuous success this framework spends most of its rules avoiding. The comment on
the registration says where a real check goes. *An HTTP test that boots the host and calls `/health`* —
better evidence, and it needs a test host package the profile does not carry yet; the cycle that adds an
integration layer should add it then rather than this one adding a package for one assertion.

### D-55 — An app host groups its containers under one Compose project label, run id included

**Governs:** §10.2

**Decision.** A solution's AppHost labels every container it starts with `com.docker.compose.project`
— the solution's name under `local`, `{solution}-{SDLC_RUN_ID}` under `batch` — plus
`com.docker.compose.config-hash`, without which Docker Desktop does not recognise the set as a
project, and `com.docker.compose.service`, without which the rows fall back to container names.
§10.2 carries the row. Nothing in the framework implements it, which is the arrangement the run-mode
variables already have and for the same reason.

**The solution's own `AGENTS.md` names both labels, because it is the only one of these documents a
cycle can read.** §11.5 is explicit that a session inside a governed solution cannot read a file that
lives in the plugin, so this page is not a carrier however carefully it is written. The first draft of
the template named `com.docker.compose.project` alone and left `config-hash` here — which would have
had a cycle produce the ungrouped configuration this decision was written to rule out, with the page
claiming the grouping worked. A contract is worth what the document that reaches its implementer says,
and `AGENTS.md`'s rule that no document restates what another owns does not extend to one a reader
cannot open.

**Why this was measured rather than reasoned.** The project label alone was tried first and does not
work: four containers sharing it stayed flat in Docker Desktop 4.49.0. A real three-service Compose
stack ran as the control, and a hand-labelled imitation was indistinguishable from it in the
container list — which is what established that `config-hash` is the missing piece, that its value is
opaque, and that no compose file has to exist on disk. A decision written from the project label
alone would have been wrong in the direction that looks right: the containers would have stayed
ungrouped while this page said they were grouped, which is the shape of enforcement §2.1 is written
against.

**Rejected.** *Shipping an AppHost template to carry the labels* — F-24 and D-38 stand: an AppHost is
conditional (§11.1), and a scaffold that wrote one would either give every solution a host it does
not need or make `Scaffold.Files` conditional for the first time, which is a large change to buy a
label. *A `PRO_nn` on `dotnet-default`* — a constraint needs an artifact to judge and no profile
ships an AppHost, so the rule would bind nothing while sitting on the page looking enforced.
*Setting `com.docker.compose.project.config_files`* — optional, buys no grouping, and its only effect
is to silence a `docker compose ls` warning; pointing a contract at a deliberately absent file to
quiet a CLI costs more in the reading than the warning costs in the using.

### D-56 — A fourth key records the standard, and an adopted solution can opt in to the profile's

**Governs:** §8.3, §9, §11.3

**Superseded in part by D-77.** The key, its two values and what it decides all stand. The flag does
not: `sdlc init --adopt-standard` is gone, the stack files it wrote are rows in `techstack.md` brought
by a cycle, and the key is moved by a draft. Two things below read differently as a result — the one
incoherent state no longer exists, and the rejected alternative of declaring this in `techstack.md` is
what D-77 does, for reasons that entry gives.

**Decision.** `solution.md` takes a fourth key, `standard`, with values `existing` and `profile`. It
decides which conventions a plan holds new work to; `origin` no longer decides that and only supplies
the value when the key is absent — `scaffolded` reads as `profile`, `adopted` as `existing`, so every
solution adopted before the key existed reads exactly as it already behaved. `sdlc init
--adopt-standard` records `profile` on an existing codebase and writes the stack files an adopted
solution otherwise never gets.

**Why two keys rather than a mutable origin.** The origin is a historical fact and D-09 protects the
whole file to keep every hook rule absolute. Letting the origin move to express an opt-in would make
*where this code came from* a preference, and the first solution to flip it would have a file claiming
the framework wrote code it did not. Two keys cost one line in a protected file and leave the fact
alone.

**Why a flag rather than a default.** These files apply a convention to code that never agreed to one.
`Directory.Build.props` makes warnings errors, so adoption writing it unasked would hand a team a
repository that no longer builds, on day one, over files nobody touched — the worst possible first
impression of a framework whose whole argument is that it does not break what it governs. A flag puts
that cost with whoever chose it. `Once` semantics do the rest: an `.editorconfig` the repository
already had is kept, so the opt-in fills gaps and overwrites nothing.

**What the flag does not do.** It does not migrate existing code, and §18 keeps both that and
baselining as non-goals. Nothing is recorded as forgiven, no existing violation is listed, and the
warnings the new files raise on old code are the cost of the opt-in rather than a finding against
anybody. Moving the code is a cycle, with an intent, a plan and an approval.

**The one incoherent state, named rather than designed away.** On an update the flag writes the files
and cannot write the key, because `solution.md` is protected and adoption creates protected files
without ever modifying them. So the keys are printed for a person to place, exactly as every other
protected file works, and `Response.StandardPending` carries it to the report — which says that until
the paste happens the recorded standard stands and every plan is still held to the surrounding code.
Keying the stack files on what the run *asks for* rather than on what the document *records* is what
makes the flag do anything on an update, which is where a migration is actually decided, months after
adoption.

**Rejected.** *Letting `--adopt-standard` write `solution.md` on an update* — per-key writes to a
protected file would be the framework's first conditional hook rule, which is where a bypass would
live (D-09). *Declaring the migrated scope in `techstack.md` instead* — unprotected and cycle-owned,
so it needs no paste, and it could carry a per-area migration the single key cannot; rejected because
the stack files are repository-wide, so a per-area record would describe a precision the mechanism does
not have, and `techstack.md` declares what a solution *is* rather than which rules bind it. Worth
revisiting if per-directory `.editorconfig` scoping is ever built. *Reading the presence of the stack
files as the record* — an old repository plausibly has its own `.editorconfig`, which `Once` keeps, so
presence proves nothing. *A `baseline` key suppressing warnings on existing files* — §18, and it is the
thing this design is written against: a file that looks enforced and is not.

### D-57 — The components live in a Razor class library, and `Ui` is a role the rules name

**Governs:** §11.3.1

**Decision.** A solution's Blazor components live in `src/<solution>.Ui`, a Razor class library, and
that is where MudBlazor is referenced. `Web` references it and holds no component package of its own.
Both roles sit in the architecture test's two tables and neither project is written by the skeleton,
the same way `Web` already was.

```
Contracts   -> nothing, and no package reference either
Application -> Contracts
Api         -> Application
Ui          -> Contracts          MudBlazor lives here
Web         -> Contracts, Ui      the WebAssembly packages, and nothing else
```

**Why, and the honest version is that this was already decided and could not be done.**
`techstack.md` has declared MudBlazor for ui components and a Razor class library for shared UI since
the profile shipped. The architecture test it ships beside admitted only
`Microsoft.AspNetCore.Components.WebAssembly*` in a `Web` project, allowed `Web -> Contracts` and
nothing else, and had no role for a class library at all. So every road the stack file pointed down
failed a test the same profile wrote: MudBlazor in `Web` fails the package rule, a `Ui` project fails
`Every_project_under_src_is_named_in_both_rules` on arrival, and `Web -> Ui` fails the direction rule.
A plan following the declared stack had to amend the scaffolded test to do it.

That is not a defect in either file. They were written separately and never read against each other,
which is the failure mode the framework exists to catch in a cycle's documents and had no instrument
pointed at its own. It surfaced in a real run: a cycle dropped MudBlazor and argued the departure
honestly, citing the package rule as one of its two reasons, and produced a page of unstyled markup
while every gate stayed green.

**Why the library from the first component rather than the second host.** The alternative was to let
`Web` hold components until a second host appears and move them then. It is cheaper on day one and the
move is the expensive part: every component, every namespace, every test that renders one, done by the
cycle that was trying to add a phone app. The cost of deciding it now is a project with one consumer
until a second host exists, which is a project that compiles and has an inhabitant rather than an empty
one waiting to be filled.

**This is the one place the layout departs from D-38's bar for a new project**, which is two occupants
in two projects, and the departure is narrower than it looks. That bar refuses a project with no
inhabitant, because it fills with whatever arrives. `Ui` has its inhabitant on the first day. What it
lacks at first is a second *consumer*, and a project defined by what it holds does not drift the way
one defined by what is left over does.

**Why MudBlazor is permitted rather than required.** `techstack.md` says of itself that it is a
declaration of defaults rather than a set of rules and that nothing in it binds by itself. A test that
*mandated* MudBlazor would contradict the document it exists to agree with, and a trivial surface is
entitled to plain markup. What makes an unused default visible is the plan saying so, which is
`CON_13`'s job rather than this one's.

**What it costs, stated rather than discovered.** A solution with one host carries a project with one
consumer. The name is load-bearing in a way nothing else in the layout is — the role is read off the
project name, so a library called `Components` or `Shared` builds nothing and fails asking for a table
row, which is why `techstack.md` now names the project rather than describing it. And `Web` keeps a
package row that admits the WebAssembly family by prefix, so the grant there stays wider than the exact
matches elsewhere.

**Rejected.** *Admitting MudBlazor in `Web` and making the library conditional on a second host*, the
shape the AppHost row already uses — rejected on the move: components are not processes, and relocating
them later touches every file that renders one. *Leaving `techstack.md` as the thing that changes* —
demoting MudBlazor to available-rather-than-default, which removes the contradiction by removing the
declaration, and returns the profile to having no opinion about what a screen looks like. *A role for a
MAUI host at the same time* — nothing prescribes one yet, and a table row for a project nobody has is
how a table starts describing a repository that does not exist.

### D-58 — The plan's reader is given the stack file, because the rule about it is otherwise undecidable

**Governs:** §6

**Decision.** The fresh reader judging a plan is handed four things: the constraints in force, the plan,
the spec above it, and the solution's `techstack.md`. On an intent or a spec round it is handed the
first three, unchanged.

**Why.** `CON_13` forbids a plan that reaches for a package, pattern or layer `techstack.md` does not
declare without saying what the declared default would have cost. The reader was never given
`techstack.md`. So the only departures it could object to were the ones the plan itself named and
explained — which means the rule fired on the document that confessed and was silent on the document
that did not. A rule that catches the honest author and misses the careless one is worse than no rule,
because the panel still says it was checked.

**How it was found, which is the part worth keeping.** Round one of a real plan raised the gap itself,
in the findings record, and then declined to act on it: it observed that nothing in the document said
whether the component-test package was the declared default, an undeclared addition, or a replacement
for something the stack named, and recorded that it was "not raised as blocking because the document
does not itself establish that `techstack.md` fails to declare bUnit". The mechanism named its own
blindfold in writing and the round went green. Four further rounds followed on the same document, none
of which could see the file either, and the solution shipped missing two things its stack declares.

**Why this is not the context D-49 refuses.** That entry rejects handing the reader the earlier
findings — their ids, statements and history — on the grounds that it is most of what the new process
was opened to withhold, and the rejection is specific to the cycle's own record. `techstack.md` is not
the cycle's record. It predates the cycle, it is not written by the author being judged, and it is the
document one of the constraints is *about*. It is the same category as the constraint set the reader is
already handed; withholding it was never a decision, only an omission that read like one.

**Why a plan only.** `CON_13` applies to a plan and nothing else. On an intent or a spec round the file
would be material with no rule to serve, and material with no rule to serve is the definition of what
this prompt excludes. The boundary is the rule's `Applies to` rather than a judgement about how much
context is too much, which is what keeps it from widening one artifact at a time.

**An empty stack is pasted rather than omitted.** An adopted solution's table starts empty, and a
reader handed no block cannot distinguish "this solution declares nothing" from "nobody showed me". The
first is a fact about the solution that makes `CON_13` vacuous; the second is a reader that should not
have judged the rule. The same reasoning as `## Open` being required when empty (D-08).

**What it does not fix.** `CON_13` still only forbids *reaching for* something undeclared. A plan that
silently fails to apply a declared default breaks no rule, and the reader now holding the stack file can
see that gap without being able to call it one. That is a separate question about what the rule should
forbid, and this entry does not answer it — D-59 does, and `CON_15` is the rule the reader is handed
`techstack.md` to judge today. The paragraphs above name `CON_13` because that is the rule this decision
was taken against.

**Rejected.** *Giving it to every round* — the reach that turns a fourth input into a general licence,
and there is no rule on an intent or a spec it would serve. *Having the binary assemble the prompt* —
the skill composes it today and a command that built it would need its own model and credentials to be
worth anything, which is D-01's argument about why a tool call is not a fresh reader. *Leaving `CON_13`
undecidable and deleting it instead* — considered, and rejected because the rule is the only thing
standing between a declared stack and a stack nobody consults; the fix is to let it be judged.

### D-59 — Departing from a declared default is a departure in both directions

**Governs:** §7.5

**Decision.** `CON_15` replaces `CON_13`. A plan departs from `techstack.md` either by reaching for
something it does not declare or by leaving something it declares unapplied in work that calls for it,
and both have to be said out loud in `Shape and departures`. `CON_13` keeps its id and stays on the
library's page verbatim, gaining `Superseded-by`.

**Why.** `CON_13` forbade only the first half. A plan adding an undeclared package had to argue for it;
a plan silently skipping a declared one breached nothing. So the rule objected to the author who was
adding and said nothing to the author who was leaving out — which is both the cheaper mistake and the
one nobody sees afterwards, because an absence leaves no diff hunk to read.

**What it cost, which is how it was found.** A solution adopted on the default profile shipped with no
api description and no tracing registration, each of which `techstack.md` declares and one of which the
same file calls not conditional. Its plan asserted that nothing in its shape departed from the declared
stack, enumerated four concerns and generalised from them. Five interrogation rounds raised no
objection and were right not to: no rule had been broken. The gap started earlier than the plan — the
scaffold templates write neither — so the cycle inherited a non-conformance it had no instrument to
see and no rule obliging it to look.

**Why a supersession rather than a revision.** The library's own front matter settles it: a revision is
new wording for the same `Forbids` clause, and a change to what is forbidden is a new constraint. This
changes what is forbidden. The mechanism was built and had never been used — this is the first
supersession the library has carried, and it is also what separated two numbers that had been equal by
coincidence: entries on the page, and rules in force. `sdlc constraints` already distinguished them.

**Why "in work that calls for it" is in the clause.** Without a scope, a plan touching one endpoint
would owe a paragraph on why it did not use the message bus, the cache, the document store and the
machine-learning runtime. The scope is also what keeps the rule admissible: deciding whether work calls
for a default needs reading comprehension, which is the second of the three tests a constraint passes,
and a mechanical version of this rule would fire on every plan.

**Saying why remains enough.** This does not make a declared default mandatory, and `CON_13`'s argument
survives intact: a default that cannot be departed from is a cage, and the first genuinely unsuitable
case would see the whole file ignored. What changes is that an omission is now a departure like any
other, arguable at the one gate before code exists.

**Depends on D-58.** Widening the rule while the fresh reader could not see `techstack.md` would have
added an obligation nobody could judge. The reader was given the file first, deliberately.

**Rejected.** *Revising `CON_13` in place* — forbidden by the library's own rules, and it would make
every finding that cited `CON_13` unreadable against the rule it was actually judged under. *A second
constraint covering omission, leaving `CON_13` live* — two rules for one idea, and a plan would have to
be read twice against what is plainly a single question. *Making the declared defaults binding instead*
— that is an architecture test's job where a row is worth enforcing, as liveness already is, and it is
a different decision from this one.

### D-60 — The scaffold ships observability and an api description, and a fitness function holds the names together

**Governs:** §11.5

**Decision.** A scaffolded solution gains a fifth project under `src/`, `ServiceDefaults`, holding the
OpenTelemetry registration and the kernel's `AddTraceResultLogging` configured with pinned source and
meter names. Every host calls `AddServiceDefaults`. The api generates an OpenAPI document, renders it
with Swagger UI and redirects `/` there, all three in development only. `ObservabilityTests` is a new
architecture test holding the pieces together.

**Why the names are pinned rather than defaulted, which is the whole of this entry.** The kernel writes
spans to an `ActivitySource` whose name comes from `TraceResultOptions`, and OpenTelemetry collects only
from sources named in `AddSource`. Those are two strings in two calls, and when they disagree every
span is discarded at the point it is created. There is no exception, no warning, and nothing to see — a
dashboard with no data is indistinguishable from a service with no traffic.

That is not a hypothetical. In the codebase this kernel was lifted from, the source was named
`TraceResult` and the registration asked for the application's name. A hundred and fifty-eight handlers
were instrumented, every one correctly, and not one span was ever collected. The meter went the same
way: the operation counter, the failure counter and the duration histogram were recorded into a meter
nothing subscribed to, while the instrumentation packages' own meters kept the dashboard looking
populated. Nothing failed, so nothing was fixed.

**Why not the kernel's default.** It defaults both names to the process name, which coincides with what
`AddSource(builder.Environment.ApplicationName)` asks for — usually. Measured rather than assumed:
under `dotnet run` and as a built executable the process name is the assembly name and they match;
under `dotnet app.dll`, which is how a container starts one, the process name is `dotnet` and they do
not. Working in development and silent in production is the worst shape this failure can take, so the
scaffold names both explicitly and a test asserts that the same constant is used on both sides.

**Why a project rather than an extension in the api.** The names have to be identical in every host or
traces from two of them do not join. One project is the only arrangement where that is structural
rather than remembered, and a solution's second host is exactly the moment nobody is thinking about it.
It takes only the observability half of Aspire's service defaults: liveness stays in the api, where it
already is and where `EndpointTests` already guards it (D-54), because two files answering for one
route would leave the fitness function watching the wrong one.

**The viewer and the generator are separate packages.** `Microsoft.AspNetCore.OpenApi` produces the
document, which is the declared default and what a client generator reads; Swagger UI only renders it.
Microsoft stopped shipping a UI in the templates at .NET 9, so a browsable page needs a third-party
package either way — and keeping the two apart means swapping the page never changes the document.

**Development only.** The document names every route, field and status the process answers, which is a
map for anybody who should not have one. Publishing it is a decision a plan can make and say so.

**What it costs.** Five packages a solution did not carry before, and a project with one consumer until
it has two hosts — the same cost D-57 took for the component library, for the same reason. The exporter
is registered and inert unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set, so a solution with no collector
pays registration and nothing else.

**Rejected.** *Leaving the row declared and unapplied* — which is what `CON_15` now forbids and what
this entry exists to end. *Taking Aspire's service defaults whole* — service discovery and HTTP
resilience are real, and neither is something the profile has decided; adopting them by copying a
template would be the silent half of a departure. *Binding the Azure Monitor exporter* — the stack file
names it as the destination, and OTLP is how a process reaches it, so the exporter choice belongs to
whatever deploys the solution rather than to the scaffold. *Asserting the names match by comparing two
string literals* — a test that compares two literals passes the day somebody changes both; the constant
is what makes them unable to drift, and the test guards the constant being bypassed.

### D-61 — Covering an outcome means covering all of it

**Governs:** §7.5

**Decision.** `CON_16` replaces `CON_04`. A spec breaches it by leaving a stated outcome with no
criterion at all, as before, and now also by covering only part of one — unless the remainder is
carried by another criterion or named in `Out of scope`. `CON_04` keeps its id and stays on the
library's page verbatim with `Superseded-by`.

**Why.** `CON_04` asked whether each outcome had a criterion. That catches the outcome dropped whole,
which is the loud failure. It does not catch the outcome quietly narrowed, which is the common one and
much the harder to see: a criterion covering half of an outcome still traces to it, still reads as
derived, and satisfies a coverage check exactly as well as one covering all of it.

**What it cost, which is how it was found.** An intent asked for "a die on the screen showing a number
between 1 and 6". The spec's criterion read "displays a face value that is an integer from 1 to 6" —
the same sentence with the thing the person asked to look at removed. The plan then dropped the
component library for defensible reasons, and the build rendered a numeral in a bare paragraph tag.
Every gate was green at every step, correctly, because no rule had been broken. The person who asked
for it opened the page and asked whether the application was working.

**Why this is not `CON_05` in reverse.** `CON_05` forbids a criterion no statement in the intent
supports, and the narrowed criterion is fully supported — it is derived from the outcome, just not from
all of it. Gaps, creep and narrowing fail differently and are remedied differently: a gap sends you
back for another criterion, creep sends you to delete one, and narrowing sends you to widen the one
that is already there. Keeping them separate is the same reasoning `CON_05` records about itself.

**`Out of scope` is the exit, and naming it is what keeps the rule from deadlocking a spec.** An
outcome can have a part no test can reach — the usual case is judging how something looks, which
`CON_12` already keeps out of a criterion. The rule does not demand that part be tested. It demands
that dropping it be written down, which turns a word going missing into a decision a reader can
disagree with at the gate.

**And the mechanism is in the spec skill rather than the rule.** A criterion carries the outcome's own
words rather than a paraphrase of them, so the difference between what was asked for and what is being
checked is visible on the page instead of resting on a reader remembering the intent. Nothing
downstream compares the two again: build implements the spec and verify checks the spec.

**Rejected.** *Revising `CON_04` in place* — forbidden by the library's own rules, and it would make
every finding that cited `CON_04` unreadable against the rule it was judged under. *A mechanical check
that every significant word of an outcome appears in some criterion* — it fails on synonyms and passes
on a criterion that quotes the words and tests something narrower, which is the failure dressed as the
fix. *Having verify compare the spec to the intent* — verify derives from the evidence and the diff
(`CON_08`), and giving it the intent would make it an author of criteria rather than an auditor of
them; the comparison belongs at the spec gate, before anybody has built anything.

### D-62 — The solution document is drafted to a file, like a gate decision

**Governs:** §8

**Superseded in part by D-94.** Adoption no longer writes the document with a placeholder of its own: it
writes the drafter's prompt as a draft, and a person places it. Everything here about the drafter —
carrying the keys and the prose through, a prompt rather than invented prose — stands.

**Decision.** `sdlc draft solution` writes a complete solution document beside the real one and prints
the rename that places it, the same shape D-52 gave a gate decision. It carries the four keys through
exactly as recorded and any prose already written through unchanged; only the shipped placeholder is
replaced, and by a prompt rather than by invented prose.

**Why.** Adoption printed the document for a person to paste, and in a real adoption it was never
pasted. The file sat on `Replace this with a paragraph a newcomer could read` for the life of the
cycle, so every plan in that solution was written against a document that said nothing about what the
solution was for or what good looked like in it. Nothing failed — nothing reads the prose — and the
cost landed on the person who later wanted to know. A block to paste is a step somebody has to
remember; a rename is one command.

**Why it carries the prose through rather than regenerating it.** A draft that replaced the body would
make the rename destructive for every solution that had written one, which is most of them after the
first week. Carrying it through is what makes "rename this over that" safe to say without qualification
— and it is the reason this can be offered at all rather than only at adoption.

**Why it carries the keys through rather than recomputing them.** The origin is a historical fact, the
standard decides which conventions new work follows, and the platform decides whether a pull-request
check governs the merge. A draft that defaulted any of them would reclassify a solution by being
renamed: a `local` solution would come back as `github`, and `doctor` would then report a broken
install rather than a recorded choice (D-53). Changing a key stays a separate act with its own drafter.

**Why a prompt where the placeholder was.** This knows the four keys and knows nothing about what the
solution is for. Writing a plausible paragraph would be the tool asserting something only a person can
say, and a sentence nobody wrote is worse than a blank because it reads as settled.

**The protection does not move.** Every agent write to the real file stays denied, the binary writes
only the draft, and nothing here knows how to place it — the same division D-09 and D-52 already rest
on. What changes is the cost of doing it right, not who is allowed to.

**Rejected.** *Letting adoption write the file outright* — it is one of the four a person writes, and
an exception for "only the first time" is the conditional rule D-09 says is where a bypass would live.
*Folding this into `draft profile`* — that drafter changes a key, this one gives you the whole file;
one prints a decision and the other preserves what is already there. *Having the draft fill the prose
from the adoption interview's answers* — tempting, and it would put words in the person's mouth at the
one moment they are least likely to read them closely.

### D-63 — A citation is the rule's id and its own line, frozen into the round

**Governs:** §6.1, §7.1

**Decision.** A finding's `cites` is one object carrying the constraint's id and the one line its
heading states, copied verbatim from the set the round was judged against. Every output that shows a
citation shows the line with it — the status panel, the findings history, the suggestions listing, a
gate refusal and an approval draft. A citation without a heading is rejected when the round is read
back, at every severity. It stays one citation and does not become a list.

**Why.** A bare `CON_03` is an index entry. It says a rule was breached without saying which, so the
only way to read the finding is to resolve the id against the library — which nobody does, and which
nothing downstream obliges them to do. Five renderers printed the id alone, and the one that matters
most is the status panel, because that is where a person meets a blocker when deciding what to do about
it. `CON_10.2` requires a gate refusal's reason to say what has to change, and that is not a judgement
anybody can make from an id.

**Why it is one object rather than two fields.** §7.1 already calls a constraint's id and its one-line
statement together the constraint's *name*, so this is the pair the design describes rather than a
second arrangement of it. Two sibling fields can be written apart and drift; one object cannot be
half-filled without the reader noticing.

**Why it is frozen rather than resolved at render time.** A round is already a historical record pinned
to the hash of the artifact above it, and the heading is the wording the reader actually judged against
— which is what somebody reading a finished cycle months later needs, not whatever the library says by
then. Nothing in a repository records which build of the global library judged a cycle:
`framework_version` is deliberately the artifact format and not the package version, so the wording was
otherwise unrecoverable. Resolving at render time would also have made `sdlc status` and the gate fail
whenever the library did not parse, which is a new way for two commands that do not read it today to
break.

**Why this is not the copy `CON_09` forbids.** That rule forbids an artifact reproducing a constraint
*instead of* citing it, and the cost it names is two live copies diverging with nothing to say which
binds. Neither applies: the heading sits beside the citation rather than in place of it, and it is
evidence of what was read rather than a second authority. What keeps the copy from becoming a
paraphrase is that the reader is told to copy the line verbatim and a round without one is rejected.

**Why it stays one citation.** A finding is one objection resting on one rule, which is what keeps the
refiner's three dispositions meaning something: a finding answered against two of its three rules would
be neither open nor answered, and a gate refusal would stop naming one thing to fix. Two rules whose
remedies exclude each other is a conflict, which §6 already makes an ending of the loop — so
multiplicity belongs to a conflict, where it is the whole point, and not to a finding. Note that §6's
three endings are stated and none of them is implemented; that gap is not closed here.

**The format change is additive and `framework_version` does not move.** A round written before this
carries `cites` as a string, which no longer reads — so the suite's fixtures were updated rather than a
compatibility path being kept. Nothing in a governed solution needed migrating: the rule lands with the
binary that enforces it, and the rounds it rejects are ones no shipped reader had yet written.

**Rejected.** *An array of citations* — considered first, and it buys a more precise way to express a
conflict that was already spotted, at the cost of the cardinality rule stated in five places; the
oscillation it was meant to catch is a cross-round phenomenon that no within-finding field can see,
because each round's reader is handed only the current document. *Resolving the id to a heading when
something renders it* — no copy anywhere and the wording can never be wrong, but it threads the
constraint library into five responses and makes two commands depend on it parsing. *Instructing the
skills to name the rule in prose and changing no format* — the smallest option, and it leaves every
rendered output exactly as unreadable as it was.

### D-64 — A conflict is recorded as its own thing, and settled by the next reader's silence

**Governs:** §6, §6.1

**Decision.** A round may carry a `conflicts` array beside `findings`. A conflict names two rules or
more, each as the `{id, heading}` citation D-63 defined, with a statement of why each remedy excludes
the other. It has no id and no disposition. The conflicts that stand are the latest round's, and the
status panel and the findings history show them under their own heading, apart from the blockers. A
conflict that names fewer than two rules, or a rule without its heading, or no statement at all, is
rejected when the round is read back. Nothing halts on one yet; this makes the ending exist.

**Why.** §6 has said since iteration 3 that a loop "stops on convergence or on conflict, not on a
count", and that "three endings have to be distinguishable: converged, open, conflicting". The code
distinguished none of them. A conflict landed as prose inside one finding's `statement`, where it was
indistinguishable from any other objection to the status panel, the gate and the suggestions listing —
so nothing counted it, nothing stopped on it, and nothing told a person it had happened. The promise was
in the design document and the mechanism was nowhere.

**Why not a finding, and why not a third severity.** A finding objects to the artifact and is answered
by editing it. A conflict cannot be: its whole claim is that no version of the artifact satisfies both
rules, so an agent that read one as a blocker would edit the document in a circle — which is the loop
this exists to end. D-30 deliberately reduced three severities to two, and a `conflict` severity would
resurrect exactly what it removed while leaving the two things conflated.

**Why multiplicity belongs here and not on a finding.** D-63 kept `cites` a single citation because a
finding is one objection resting on one rule, which is what keeps the three dispositions meaning
something. A conflict is about more than one rule by definition. So `between` is a list, two entries or
more: one rule cannot conflict with itself, and a single-entry conflict is a finding wearing the wrong
shape. More than two is permitted — three mutually unsatisfiable rules is conceivable, nobody has seen
one, and capping it at two would be a limit with no argument behind it.

**Why no id and no disposition.** A conflict is a per-round observation, and it is settled the way
everything else here is settled: by the next fresh reader's silence (D-49). A conflict declared in round
two and not declared in round three is one the refiner found an exit for — `CON_16`'s `Out of scope` and
`CON_14`'s `## Open` exist to be exactly that. Accumulating conflicts across rounds would report a
resolved one for the life of the cycle, which is the mistake the standing fold exists to avoid for
findings. The interrogate skill is told not to carry a previous round's conflicts forward: if it still
holds, this round's reader will have said so independently, and that independence is the whole value of
the declaration.

**Why one declaration settles nothing on its own.** A declared conflict is an assertion, and nothing in
this framework advances on assertion. It is also an escape hatch — an agent stuck on a blocker it cannot
answer has an obvious out in calling it a conflict, the same hazard the abandonment record is written to
prevent. What makes it usable is that a reader is handed only the constraints, the artifact and the
artifact above it, and never the prior rounds: two readers declaring the same conflict are two uninvested
processes agreeing with no knowledge of each other. That corroboration is counted rather than trusted,
and the counting is not part of this decision.

**Why absent and empty mean the same thing, where `findings` makes them differ.** D-31's reasoning about
`## Open` applies to `findings`: "nothing is wrong" and "the author forgot" need different responses, so
a clean round records an empty array rather than omitting it. A reader that found no conflict is the
overwhelming default, so requiring the key would put something on every round that is empty in almost all
of them — the decoration `CON_11` exists to refuse.

**One bug this found, worth recording because the test found it rather than a reader.** The findings
history printed "nothing raised" and skipped the rest of the artifact when no finding stood, so a round
whose only content was a declared conflict reported nothing raised above the one line that mattered. A
conflict counts as something raised.

**Rejected.** *A `conflict` severity on a finding* — resurrects what D-30 removed and leaves the two
conflated. *Accumulating conflicts across rounds* — reports a resolved conflict forever, for a thing with
no disposition to clear it. *Requiring the `conflicts` key on every round* — decoration on almost every
round, where the equivalent rule for `findings` buys a real distinction. *Halting the loop on one
declaration* — that is an assertion halting a cycle, and the corroboration that fixes it needs a counter,
which is its own decision.

### D-65 — The interrogation loop stops on oscillation, never on a count

**Governs:** §6

**Decision.** Points are counted per rule, per artifact, from the rounds alone. A **relapse** — one rule
going `open -> answered -> open`, where the rule was answered by going quiet or one finding was answered
and came back by its id — is one point. A **conflict declaration** naming that rule is one
point. At two points the panel's `Loop` line tells the refiner it is refighting old ground and names
what the rule is caught against; at three the loop stops and hands back to a person. There is no limit
on rounds. A halt reports and does not refuse the gate.

**Why there is no round cap.** `RemediationBudget` said the cap was on code and not on
artifacts because "an unbounded interrogation loop is a person deciding to keep reading, which has been
worth eleven rounds before now". The premise was false: the stage skills run the loop themselves, and
`skills/spec/SKILL.md` told the session to continue "until a round is clean or nothing further can be
settled" — a judgement the same session made about its own work, which is the inversion the
interrogate/refine split exists to prevent. So the loop needed a guard. A cap on rounds is the wrong
one: it kills the long honest reading and permits the short pathological one, which is backwards.

**Why a sequence and not a tally.** One rule cited in rounds one, three and five for three different
mistakes is a sloppy author, and the loop is converging fine. One rule cited, *answered*, and cited
again is the loop going backwards. The two have the same tally, so counting citations would conflate
them. What is counted is the transition.

**Whose transition.** The rule's, or one finding's — never one finding's answer and another's
objection. A rule is answered in a round that raises nothing open against it; a finding is answered by
its own id. A round that answers one finding and raises a different one under the same rule is the
reader moving on to the next place the same mistake was made, which is the sloppy author above, and the
counter first got this wrong: keyed on the rule alone, it armed on any answered finding and scored on any
open one. A plan whose browser proofs each waited on an HTTP response rather than the rendered page
halted after five rounds that had each fixed one proof and found the next, with nothing ever raised
twice. Counting the rule's own quiet keeps what keying on the rule was for — a fresh reader that raises
the old objection under a new id still scores, because the rule went quiet first — and the id
transition catches the one finding refought while another under the same rule stays open throughout.

**Why these two signals and not others.** Both are corroborated rather than asserted, which is the
property that makes them usable at all. D-49 derives `answered` from a fresh reader declining to raise
something, so a relapse is two independent readers — one silent, one not — and nobody in the loop can
fabricate either half. A reader is never shown the prior rounds, so two readers declaring one conflict
are two uninvested processes agreeing with no knowledge of each other. Neither number is one the loop
can move directly, which is what separates this from a self-reported counter.

**Why per rule and not per artifact.** One rule coming back three times is the same ground refought. Two
different rules relapsing once each is two unrelated incomplete fixes, and aggregating them would halt a
loop that is converging on both. Per-rule counting also makes constraint-pair bookkeeping unnecessary:
nothing relapses repeatedly on one rule without something else pushing back each time, so the pair is
implied, and it is recorded as what the rule is `Against`.

**Why two and three.** One relapse is an incomplete fix and those are ordinary, so one point says
nothing. Three points is roughly six rounds of genuine ping-pong — deliberate room for a loop that
oscillates *on its way to* converging, which is the normal shape of a refiner finding a boundary: narrow,
overshoot, broaden, overshoot, then find the wording that satisfies both. Halting at two would kill that
one round before it succeeds. Constants rather than configuration, for the reason
`RemediationBudget.Limit` gives: the number a team would raise it to is the number at which the bound
stops meaning anything.

**Why the warning changes what the refiner is told, and is not just a display.** This is what makes the
extra room worth paying for. The refiner is handed findings and no history, so it cannot know it is
refighting old ground, and more laps at the same approach is only more spend. At two points the `Loop`
line names the rule, the rounds, and the rule it is trading with, and the refine skill is told to look
for an exit — a remainder named in `Out of scope`, a question that belongs under `## Open` — rather than
editing the same sentence again.

**Why a halt reports rather than refusing the gate.** A relapse requires an open blocking finding, and
`GateRefusal.OpenBlocker` already refuses a build on one. A gate refusal would therefore be redundant in
the common case and wrong in the only case where it would bite — the counter reaching three, then the
fix landing and the next reader going silent, which is a loop that converged. A halt is evidence that
automation got stuck, not evidence the artifact is wrong, and what decides whether the artifact advances
is still the blockers.

**What a halt must not claim.** The counter cannot tell a genuine conflict from a refiner repeating a
mistake. Both need a person, so the distinction changes nothing about what happens — but the line says
what it knows (this rule has come back three times, against these rules, in these rounds) and never that
the rules conflict.

**What clears a halt.** Steering, which already edits the intent, spec and plan together, records the
correction as a fixed decision with attribution, and leaves the downstream rounds stale so the loop runs
again. The counter moves with the history. **Nothing mechanical stops an agent steering itself**, and
there is nowhere to put such a guard: the hook denies writes to the protected files, and `steer` is
a skill rather than a file. What the reset rests on is downstream — a fabricated steer is a fabricated
attribution sitting in the intent, which a person reads at the approval gate before any code exists.
That is the same layer every advisory rule here has, and it is written down rather than assumed.

**Rejected.** *A cap on rounds* — kills the eleven-round honest case and permits the four-round
pathological one. *A configurable patience* — the argument against a configurable `Limit` applies
unchanged. *Counting per artifact* — aggregates unrelated incomplete fixes into a halt. *Counting
citations rather than transitions* — conflates a sloppy author with a loop going backwards. *Counting
per finding id alone* — ids are carried across rounds by the interrogate skill's judgement, so the same
objection under a new id would escape. *A separate warning for a rule raising many distinct findings* —
proposed as a backstop for that, and unneeded once the rule's quiet is counted; the repeated mistake is
answered upstream instead, by a reader naming every instance and refine healing every instance. *Halting on
one conflict declaration* — an assertion stopping a cycle, and an escape hatch for an agent stuck on a
blocker it cannot answer. *A new `GateRefusal` value* — redundant where it would fire and wrong where it
would bite.

### D-66 — The remediation loop is bounded by progress, with no ceiling behind it

**Governs:** §6

**Decision.** `RemediationBudget.Limit` is removed. Remediation continues while each pass strictly
reduces the open blocking findings. It stops when a pass undoes what an earlier one answered — a relapse
on the verification, read by the same counter the artifact loop uses — or when two consecutive passes
reduce nothing. There is no pass ceiling. An escalated cycle still never opens a pull request.

**Why the count had to go.** Three was a judgement, and both of its failure modes showed up: it failed an
honest cycle that needed a fourth pass, and it let a cycle making no progress at all spend all three
before stopping. A count cannot tell those apart, because the thing that matters is not how many passes
there have been but whether they are working.

**Why these two signals.** A regression is the code-side shape of oscillation, and it is the one that
stops the loop on the spot: a loop breaking what it already fixed is not going to finish, and every
further pass is spend against a moving target. A stall takes two passes rather than one, because a pass
that fixes one thing and uncovers another of equal size has made real progress the count cannot see —
one flat pass is ordinary, two in a row is not.

**Why it reuses the artifact loop's counter.** A remediation pass is a verify findings round, and a
finding answered by one pass and raised again by the next is exactly the relapse D-65 defines. So
`Oscillation.Count` over the verify rounds is the regression check, and nothing new had to be invented
for it. This is what split `StuckRule.Points` into `Relapses` and `Declarations`: a declaration is a
judgement about the rules and a relapse is a measurement of what happened, and only the measurement
belongs in a decision about code.

**What no ceiling costs, recorded because it is the reason this was a decision and not a detail.**
`RemediationBudget` said the cap was what made the unattended half safe: *"A batch is only safe to run
unwatched because a cycle that cannot be made to pass stops instead of opening a pull request."* That
property now rests entirely on the progress predicate, which is a better signal and **not a guarantee**.
A loop that closes one finding while opening another satisfies "strictly reduces" on alternate passes and
never terminates.
The judgement is that a blind three was failing honest cycles often enough to cost more than that tail
risk, and that the regression and stall rules catch the realistic bad loops. If an unattended batch is
ever seen running away, the remedy is a ceiling put back deliberately rather than this rule quietly
tightened.

**Why the skill is told what that costs it.** The bound used to stop the agent; now it mostly does not,
so `skills/remediate/SKILL.md` says in as many words that nothing will stop it at pass nine, and that the
test is whether it can say what the next pass will fix. A guard that moved from the tool to the operator
without telling the operator is worse than either.

**Why a regression says something different from a stall.** They want different things from a person. A
regression means looking at what the last pass actually did; a stall means asking whether the finding is
answerable at all. So `Describe` names which of the two stopped the cycle rather than reporting one
escalated state.

**Rejected.** *Keeping a high ceiling behind the predicate* — recommended, and declined: a ceiling nobody
reaches is a number that will be argued about instead of the rule that actually stops the loop. *Counting
a conflict declaration as a regression* — a declaration is a judgement about the rules and remediation is
about the code. *Stopping on one flat pass* — punishes a pass that traded one finding for another of the
same size, which is ordinary progress.

### D-67 — The declared stack is a person's file, created once from the profile's template

**Governs:** §5.3, §8

**Superseded in part by D-76.** Adoption no longer creates the file: a person places it from a draft,
which starts from the framework's default where no file is in place. The drafter proposing no rows
holds for every draft after the first. Why the file is protected, and the byte copy, stand.

**Decision.** `techstack.md` becomes the fifth protected file. Adoption creates it from the profile's
template and nothing writes it again: every agent write is denied by file path and by shell command,
exactly as for the other four. A change after that is `sdlc draft techstack`, which copies the file beside
itself for an edit a person renames into place. And the plan schema refuses a plan whose `Files` section
declares any of them.

**Why.** `DeclaredFiles` says it in as many words: *"The solution's own constraints and its declared stack
are the two rule sources a plan is read against."* Two rule sources, and only one of them was protected.
The reason given for protecting the other applies here unchanged — a cycle judged by the rule it is
changing is the same deadlock as the framework governing itself. `CON_15` judges every plan against the
declared stack, so an agent able to write that file could satisfy the rule by adding the row it wanted and
then truthfully not be departing from anything. The asymmetry was the whole finding.

**What it makes `CON_15` mean, which is better than what it meant.** The rule never asked a plan to change
the stack file; it asked a plan to *argue*. Protected, the only path inside a cycle is to depart and say
what the declared default would have cost, and declaring a new default becomes a separate deliberate act
with a person's hand on it. Before this, a cycle could make the argument unnecessary by editing what the
argument was about.

**The draft is a byte copy, which is the whole guarantee.** Renaming it applies what somebody changed and
nothing else — nothing merged, nothing regenerated, nothing silently dropped. An agent may edit the draft,
because only `techstack.md` is denied and `techstack.draft.md` is an ordinary file; what an agent cannot do
is place it. The drafter proposes no rows, for the reason a constraint's wording is never typed into a tool: which
packages a solution declares is a judgement about how a team works, and a tool filling them in would be
making that judgement while looking like bookkeeping.

**Why the schema refuses a plan that declares one, rather than the comparison tolerating it.** Such a plan
cannot be carried out as written. The build is denied the write, so the file never changes and the evidence
comparison reports it as `UNDELIVERED` — which means "the plan's blast radius was wider than the change"
and sends the author looking for an edit they forgot rather than at a declaration that was never
executable. Same symptom, wrong diagnosis. It is mechanically decidable, so §2.3 puts it in the schema
rather than a prompt, and the schema runs before the build, which is the one moment the mistake is still
free to fix. **It asks the hook rather than keeping a second list**: two lists of protected paths would
eventually disagree, and the one that mattered would be whichever the author had not read. A
`techstack.md` outside `.sdlc` is therefore an ordinary file to both.

**What this costs.** A stack change is now a person's act rather than a cycle's. That is the point, and the
cost is real: `.sdlc/README.md` said the file was owned by "a cycle whose plan declares it", and the
adoption skill offered to fill it in directly. Both are now wrong and both were rewritten. The drafter is
what keeps the cost to one command and one rename rather than a hand-edit, which is the same reason D-62
gave for drafting the solution document to a file.

**Rejected.** *Leaving it writable and strengthening the signal* — recommended first and then declined: it
treats a closed hole as a reporting problem, and the plan's reader is already handed the stack file, so the
signal would be advising a reader that the thing it was handed may have been edited by the cycle it is
judging. *Filtering protected paths out of the file comparison* — silent; the impossible declaration stays
in the plan with nothing saying so. *A drafter that proposes rows from the codebase* — that is adoption's
job once, and after that it would be a tool declaring a stack.

### D-68 — A profile selects one flat stack table, and says when it has no skeleton

**Governs:** §8.3, §11.5

**Superseded in part by D-76.** The `profile` key, the `--profile` option, the refusal of an unknown
name and `Scaffolds` are gone: no solution records a profile. One flat table a person owns, no
stacking, and overlap handled in the framework all stand.

**Decision.** `StackProfiles` ships two embedded profiles, `dotnet-default` and `ml-tensorflow`. The
`profile` key selects which declared stack adoption writes. Rows are composed in the framework from a
shared platform set plus the profile's own, and emitted as **one flat table** a person then owns. A profile
declares whether it ships a solution skeleton: `sdlc scaffold` refuses for one that does not, and the
convention files are written only for one that does. An unknown profile name is refused before anything is
written, by adoption and by `sdlc draft profile` alike.

**Why.** The `profile` key existed from iteration 3 and selected nothing. `request.Profile` was used in
exactly one place — writing the name into `solution.md` — and never read to choose anything, while the
declared stack, the scaffold, the convention files and the architecture suite were all .NET with no
profile anywhere near them. A key that records a choice nothing acts on is a key that will eventually be
wrong about the solution it describes.

**Why one profile per solution, and no stacking.** Merging two profiles' tables needs a precedence rule on
a concern key, and the cost that disqualifies it is worse: `techstack.md` would become a **derived** file.
The thing every plan is judged against would then be generated rather than written, and D-67's "a person
places it" would stop meaning anything. A solution that wants rows another profile declares copies them
in — a one-time edit against permanent merge machinery.

**Where overlap is handled instead.** In the framework, while authoring, which is where it is cheap. Two
profiles sharing their platform choices share one set of rows rather than repeating twenty that drift
apart when edited separately. Nothing in a governed repository knows a composition happened, and the suite
asserts that both profiles name the same cloud rather than that they happen to agree today.

**Why a profile says whether it scaffolds.** The scaffold is twenty-five .NET templates and an
architecture suite written against them. Letting it run for a profile declaring Python would hand a
solution a layout its own stack file contradicts — a profile lying about itself, which is worse than a
profile with no scaffold, because the declarations are what a plan is judged against. So `Scaffolds` is
false on `ml-tensorflow`, `sdlc scaffold` refuses with one sentence instead of writing twenty-five
misleading files, and `--adopt-standard` writes nothing for it either: warnings-as-errors in a props file
and an SDK pin in `global.json` are .NET, and a profile declaring another language has nothing to put in
them.

**Why the TensorFlow profile ships declarations and no skeleton, deliberately.** The .NET skeleton earned
its shape from a solution that was actually built and a review that followed it. Writing a Python
equivalent before anything has been built from this profile would be guesswork carrying the authority of a
default, and the first real solution here is what the skeleton should be written from. **The material gap
is the architecture suite**, which is the only mechanical enforcement of shape the framework has — so the
profile's own notes say that out loud, in the file a plan reads first, rather than leaving it to be
discovered.

**Why an unknown name is refused rather than recorded.** Nothing resolves an external profile, so a
solution recording one would carry a name selecting nothing and declare a stack it never got — and the
first plan judged against it would be judged against an empty table nobody chose. The refusal names every
profile that exists, because one that only says no sends somebody looking for the flag that makes it work.
Easy to relax the day a profile can come from a package; a solution that already records an unresolvable
name still reads, since adoption validates what was asked for rather than what was recorded.

**Rejected.** *Stacking profiles* — makes the stack file derived, which is the one thing it must not be.
*A `dotnet-ml` profile sharing the .NET scaffold* — the cheaper second profile, and it answers a different
question: this one is for model work in Python, and pretending it is .NET-shaped would be the dishonesty
`Scaffolds` exists to prevent. *Twenty-four Python templates now* — guesswork with the authority of a
default. *Composing the help text from the profile list* — `Usage.Text` is a const, so the suite holds the
two together instead, the same way the readme's counts are asserted rather than trusted.

### D-69 — A constraint is proposed from the record, never drafted, and never written by a tool

**Governs:** §5.3, §8.1, §15, §15.2, §16, §19

**Decision.** `sdlc draft constraint` is retired and `sdlc propose constraints` replaces it. The verbs
split on **who made the decision**: `draft` hands back a finished file for a decision somebody already
took; `propose` reads the record and argues for one they have not. Identical grammar — a subject after
the verb, one skill behind each — so the only thing to remember is which side of the decision you are on.

The new command reads every findings round in the cycles directory, blocking and suggestion alike and
closed included, plus every stopped cycle's hand-written reason and the existing rule set. It reports
rules cited in **three or more distinct cycles**, findings that rested on no rule, the stopped cycles'
reasons, the next free id, and what a person has already refused under a `## Considered and rejected`
heading in the solution's own rule file. It proposes; it writes nothing that binds, and the hook still
denies every agent write to that file. Where the record holds no pattern it prints the empty form at the
next free id with the three admission tests — the shape, when it cannot propose the rule. Both that
command and `suggestions` keep a copy of what they printed under `.sdlc/reports/`.

**Why the drafter had to go rather than sit beside it.** It was the one member of its family that handed
back a blank. Every other drafter produces finished bytes and a rename or a paste; that one produced an
empty form, which is why nobody could remember what it did — the name described the family it was filed
under rather than the thing it performed. Two commands both producing constraint text, one of them empty,
is the confusion the split fixes.

**Why a tool may now propose wording at all, having refused to before.** The old refusal was *"a tool
accepting a statement on the command line would be proposing the rule"*, and it was right about the thing
it was looking at: a drafter taking `"no direct bus publish"` as an argument would have rubber-stamped a
sentence somebody typed into the shape of a rule, with no evidence and no argument. An evidenced proposal
is a different act. So the rule that replaces it is narrower and statable: **a tool may propose a rule
when it brings the evidence, and may not shape a rule somebody just typed.** `init` has always worked
this way — §9 has it interview and then propose a `LOC_` set from the answers — so the licence is not
new, only its second source.

**Why distinct cycles, and why three.** Three findings against one rule inside a single cycle is one
artifact going round the loop; the same rule blocking three separate cycles is a solution walking into the
same wall. Two is coincidence, three is a pattern. There is deliberately **no floor** on how much history
a solution must have first: three blocked cycles out of a solution's first four *is* a pattern, and a
floor would suppress the most useful report this can produce at exactly the moment a rule is cheapest to
add. A floor was in the plan and was dropped for that reason.

**Why it reads what `suggestions` cannot.** That command folds a history down to what is still **open**
and non-blocking, which is right for a list of work to pick up. A closed blocker that recurred across
three cycles is the strongest evidence here, and folding first would discard every recurrence that was
dealt with properly — which is most of them. The two commands therefore read the same files and can never
be one command.

**Why candidates are labelled and never numbered.** Pre-assigning `LOC_07`, `LOC_08` and `LOC_09` to
three proposals and having a person accept only the middle one leaves a gap, and §7.4 makes a gap the
record of a constraint that was **removed**. A proposal nobody took would be indistinguishable from a rule
somebody retired. So the next free id is stated once and the person numbers what they accept.

**Why removal is never printed.** A rule out of force keeps its place on the page with `Superseded-by`, so
a finding that cited it stays readable. Whatever the report prints is what gets pasted, which makes the
printed text the place that rule is really enforced — a report offering an entry to cut would get the
entry cut. §7.4 already names deleting a superseded entry as the regression nothing else would notice.

**Why a refusal needs a home.** Without one the same two candidates return on every run forever, which
teaches people to skim the report. The section is hand-written like everything else in that file, and the
parser ignores it — it recognises only `## LOC_nn — statement` headings. The refusals are **echoed**
rather than silently applied, because a suppressed candidate would make the report's silence ambiguous:
nothing found, or something found and hidden.

**Why it is stateless, and why a report is not a record.** Nothing records which cycles have been
absorbed. Such a marker is §19 item 3's completion-certificate trap — a mistaken one silently loses the
learning and looks identical to a correct one — and it is the same reason `CycleIndex` globs rather than
keeping a manifest. The kept reports are the same judgement applied to output: **nothing reads one back,
ever.** The directory carries its own ignore file rather than an entry merged into the repository's, which
keeps the whole thing a file the framework named under D-40 and means an existing solution gets the ignore
the first time it writes a report rather than needing an adoption re-run. Tracking them would be worse
than untidy — an untracked report in the tree reaches a build's diff as a file no plan declared, which is
`CON_07.2`.

**Why `LOC_` only.** A `CON_` proposal in somebody else's repository is advice they cannot act on: only a
plugin contributor can change the library, and there is no channel from a solution to it (§2.7). Where a
recurrence meets the bar and no solution-level rule could reach it, the report says so in one line and
points at the plugin's issue tracker — rare by construction, because the bar gates it.

**What it does not do, stated plainly.** It proposes no decisions and no theme, though `propose` is a
family and both are the obvious next subjects. **A decision cannot be mined** — see §19 item 3 — so that is
different mechanism rather than a second section of this one. It also says nothing about which *stage* a
problem was caused at, which is the `caused-by` half of §19 item 3 and still open.

**Rejected.**

- *Regenerating the rule file from the cycles* — the first shape proposed, and the dangerous one. A
  generator writing "the correct current rule set" drops superseded entries, because they are not current
  rules and read as clutter, and closes id gaps into a tidy sequence. Nothing notices: the replacements are
  all present, the file looks right, the count looks plausible. §7.4 names exactly this as the regression
  nothing else would catch.
- *Copy-then-amend, with the file written for a rename* — the improved version of the same idea, and still
  refused. `draft techstack` is safe as a rename because the draft is a **byte copy** and the rename
  applies one edit with nothing merged and nothing regenerated; a regenerated rule file is merged and
  regenerated by definition. It would also make whole-file replacement the review step for the one file
  whose rules are typed by the person accountable for them.
- *Add, change and remove as three symmetric operations* — `remove` is not a legal operation on this file,
  and `change` is two different acts that §7.4 says cannot be told apart mechanically: a revision replaces
  wording and leaves no predecessor, a supersession keeps the old entry. An agent calling a real change a
  revision loses the old rule for good.
- *Deriving rules from one cycle's plan, at the end of a cycle or on `verify`* — three reasons. A plan is a
  proposal about one change, so a rule derived from it is a local choice promoted to bind all future work.
  There is a loop in it: plans are written by an agent and judged by the constraints, so an agent's choice
  this cycle would become the rule its own work is judged by next cycle — the same loop the declared stack
  is protected to prevent, one hop longer. And `verify` is the stage that reacts to test results, which is
  the one place that must never reach a rule document.
- *A mode of `suggestions`* — "suggestion" is already a finding severity (D-30), and that command lists
  findings *of that severity*. A proposed constraint is not one, so the name would collide in exactly the
  conversations where finding vocabulary has to be precise.
- *Renaming today's `suggestions` to free the name* — it is the one command whose name is an exact match
  for a domain term. Renaming it would take a precisely-named command and make it vague in order to give
  the word to something it does not describe.
- *Keeping the evidence and the form in two commands* — the report argues, the drafter supplies the frame,
  the person assembles. Refused on reflection: the only thing the drafter uniquely knew was the next free
  id, which this has to read anyway, so the second step contained no decision and bought nothing but
  friction.
- *A floor of roughly ten merged cycles* — in the plan, then dropped. See above.

### D-70 — A decision is captured forward from an approved cycle, and recorded in a person's file

**Governs:** §5.3, §8, §8.1.1, §15.2, §16, §19

**Decision.** A solution records its settled choices in `.sdlc/decisions.md`, a sixth protected file.
`sdlc decisions` renders what stands so a stage can read it; `sdlc propose decisions` reads what the
solution's **approved** cycles stated — an intent's `Fixed decisions`, a plan's `Shape and departures` —
and hands the passages over for a person to record. Entries carry `DEC_nn` ids, monotonic and never
reused, with four parts: `Decided`, `Because`, `Instead of`, `From`. A reversed decision takes a new id
and the old entry stays on the page. **The record binds nothing.**

**Reversed in part by D-72**, which makes a plan say what a recorded decision would have cost before
departing from it. Everything else in this entry stands; what was overtaken is the last sentence above
and the *why it binds nothing* paragraph below, and both are left as written because a commit citing this
id should still mean what it meant.

**Why a record at all.** A constraint stops a cycle doing something; a decision stops it *re-deciding*
something. Cycle forty cannot see that cycle seven chose an outbox over two-phase commit and why, because
the reasoning sits in one plan's `Shape and departures` and nothing reads across cycles. That is the gap,
and it is the one a person asking for a "charter" is pointing at.

**Why it is captured rather than mined, which is the whole reason it is not D-69's second section.** A
constraint is earned backward from findings: the same rule blocking three cycles says a rule is missing. A
decision never collides with a rule at all. A colour, a layout convention, a code-style choice, a pattern
like draft-then-rename produces **no finding**, so no amount of history review surfaces it. The two need
opposite mechanisms, and bundling them would have made one command that was good at neither.

**Why only approved cycles.** An approval records a hash of the three planning documents against a
person's name, so a passage from an approved cycle is one a person signed off on those exact bytes. That
is the answer to the authorship problem that killed the first version of this idea: a hand-maintained
charter goes stale, and an agent-written one is doctrine nobody approved that later cycles then align to.
A cycle whose approval was **reopened** states nothing either — the ledger is append-only and the last
entry stands, so a passage from one would be prose a person explicitly sent back — and nor does a
**stopped** cycle, because a choice lifted out of abandoned work is the opposite of a settled one.

**Why the file is protected, and for a sharper reason than the rule file.** `constraints.md` and
`techstack.md` are protected because a plan is judged against them, so an agent able to write either could
satisfy a rule by editing it. Nothing judges a plan against this record — and it still must not be
agent-written, because a **stage reads it for alignment**. An agent that could write it could invent
doctrine and then align to it, which is a tighter loop than the one the other two guard against.

**Why it binds nothing, stated plainly because the plan expected otherwise.** The working plan and the
open question that became D-72 both claimed an entry would be covered by `CON_15` as a declared
default. That is wrong:
`CON_15`'s `Forbids` clause names `techstack.md` specifically, so a plan departing from a recorded
decision breaks no rule today. Making it bind takes a **new** global constraint rather than a reworded
one, because what is forbidden would change (§7.4), and a global rule is a person's to write. The gap is
recorded rather than papered over. The enforcement was wanted, and D-72 is where it was decided.

**What the four parts are for, and why `Instead of` is required.** It is what separates a decision from a
fact. An entry whose alternatives were never real records something nobody chose, and reading it costs
attention at every cycle for nothing — the same job the `Forbids` clause does for a constraint. `Because`
is the half that cannot be recovered later: a passage usually says *what* without saying *why*.

**Why there is no revision suffix, unlike a constraint id.** A constraint carries one because its wording
can improve without changing what it forbids, so a finding judged under the old words was judged under the
same rule. A decision forbids nothing, so there is no equivalent invariant holding two wordings together —
a rewritten reason *is* a different reason. Either the entry stands as written or it is superseded.

**Rejected.**

- *A derived view over the approved artifacts, with no file* — the shape an open question leaned toward, and it
  cannot go stale, which is a real advantage. Refused because it can only ever hold what some cycle
  happened to say, and most of what wants recording never passes through a cycle: a theme, a code-style
  choice, a convention invented once and applied everywhere since. A record that structurally cannot hold
  those is not the record anybody asked for.
- *Reading unapproved cycles too* — more material, and the material is unsigned. It would make the record
  a place where an agent's draft prose becomes doctrine, which is the failure the approval requirement
  exists to prevent.
- *Leaving the file unprotected* — it reads like a courtesy until you notice a stage consults it. Then an
  agent writing it is an agent writing its own brief.
- *Folding this into `propose constraints` as a second section* — one command reading two corpora for two
  purposes. The mining and the capture have nothing in common but the verb.
- *Deriving a decision from `CON_15` departures automatically* — a repeated departure is a real signal
  that a default has moved, and recognising that two prose passages are the same departure is a judgement.
  The proposer hands the passages over; it does not claim they match.

### D-71 — A theme is a register of roles, machine-readable, and protected by nothing

**Named `theme` rather than `palette`, and the name is load-bearing in one direction.** It was
`palette.json` when it shipped, which undersold it: the register carries `font-body`, `font-heading` and
`font-mono` as well as colours, and a palette is colours. **The cost is that "theme" invites light and
dark.** The shape is one flat map of role to value and cannot express two themes; supporting them means a
level above the roles — `themes.light.surface` — which is a change to the file rather than a flag. So
when somebody asks for dark mode the honest answer is that the shape changes, and that is a consequence
of the name chosen here rather than something discovered later.

**Governs:** §8, §8.1.2, §15.2, §16, §19

**Decision.** A solution with a front-end surface or a report keeps `.sdlc/theme.json`: semantic
**roles** mapped to values, with a `settled` flag. `sdlc theme` renders it; `sdlc propose theme`
prints the skeleton of roles for a solution that has none and names the empty roles in one that does. It
**never proposes a value.** The file is not protected, nothing enforces its existence, and enforcement
where a solution wants it is a linter.

**Why roles rather than values.** `cancel` is a role; `blue-400` is a value wearing a role's clothes. Roles
survive a rebrand — the values change and the roles do not — and they give a stage something to *look up*
when it builds a page, which a flat list of hex codes does not.

**Why JSON rather than prose, and why that is the whole design.** A markdown page of hex codes that a
stylesheet also holds is two places the truth lives, and the one that goes stale is always the one a tool
trusts. JSON is importable by every front-end toolchain, so a stylesheet reads *this* file rather than
copying out of it, and the same bytes feed a linter. The *reason* for a theme — a brand, a contrast
target — is a decision and belongs in `decisions.md`, so values and reasons sit in one place each.

**Why a tool never proposes a colour.** A colour is a brand's or a designer's. A skeleton of roles is
bookkeeping; a theme of invented values is a tool making the one decision nobody delegated to it, and it
would be adopted by somebody skimming.

**Why `settled` exists.** An empty theme could mean *not decided yet* or *deliberately none*, and those
read identically in a file of empty values. One flag separates them, and it also covers the ordinary case
of a role left empty because it does not apply to this solution.

**Why it is not protected, unlike the other two records.** Nobody is judged against a colour. No
constraint reads this file, so an agent writing it cannot satisfy a rule by doing so, and no stage takes
its brief from it the way one does from the decision record. There is no loop to close — and a hook rule
nothing needs is a rule somebody later carves an exception into, which is how an absolute rule stops being
absolute. So a cycle changes a value by declaring the file, like any other declared work.

**Why `init` could write the skeleton without breaking D-35.** A tokens file is data, not code — the same
class as `.editorconfig` and the props files, which constrain nothing until code exists.
**Corrected 2026-10-13:** this said adoption already writes those files, which was true when written.
Adoption writes none of them now and a cycle brings them (D-76, D-77); §9 owns where they come from.
The argument is that a tokens file is data rather than code, and it is unchanged. It is not written today because adoption cannot tell whether a solution has a
front-end surface, and §2.1 is the rule against a check that cannot really check.

**Rejected.**

- *A markdown theme beside the other governance files* — the first shape proposed. Refused because the
  values would be duplicated into a stylesheet within a month, and the prose copy is the one a tool
  trusts.
- *Generating CSS custom properties from it* — the honest way to have one source and native CSS. It is
  build machinery, and inventing a generator for every front-end toolchain is a much larger thing than
  this earns. Importing JSON is already native everywhere that matters.
- *A `solution.md` key saying whether a solution has surfaces* — a fifth key to let a check assert the
  theme exists. The check would then have to decide what counts as a UI, which nothing here can do
  honestly: §2.1's failure mode is a directory that appears enforced and is not.
- *A long skeleton covering every role a design system might want* — guesswork with the authority of a
  default. The roles a solution needs are the ones it adds; the sixteen shipped are the ones a surface
  cannot avoid choosing.
- *A constraint forbidding a literal colour outside the theme* — admissible on the first and third
  admission tests and refused by the second: a linter decides it, and a linter fails a build where a
  constraint cannot.

### D-72 — A plan says why it departs from a recorded decision, and that is a new constraint

**Governs:** §7.5, §8.1.1, §19

**Decision.** `CON_19` is added to the global library: a plan whose approach contradicts a standing entry
in the solution's `decisions.md` says in `Shape and departures` what the recorded decision would have cost
here. **Departing stays legal**; departing in silence does not. This reverses the half of D-70 that
shipped the record binding nothing, and D-70 is left as written.

**Why a new constraint and not a reworded `CON_15`.** §7.4 decides it with no judgement needed: *a change
to what is forbidden is a new constraint.* Today a plan may contradict a recorded decision and breach
nothing; under this rule it may not do so silently. That is something becoming illegal, which is the
definition separating a supersession from a revision — a revision is better words for the same `Forbids`.

**Why two parallel rules rather than one covering both records.** `CON_15` could have been superseded by a
rule naming "a declared default or a recorded decision". Refused on §7.2's first test: `CON_15`'s
`Forbids` names `techstack.md` concretely enough that an example could be written, and the merged version
would have to generalise to the point where a reader has to decide which record is meant before they can
tell whether the rule fires. It would also have put `CON_15` out of force across the drafter's text, the
plan skill and three documents that cite it — churn with nothing bought, since a finding has to say which
record it is about either way.

**Why departing stays legal, which is the half worth stating.** A decision that cannot be revisited is a
cage, and the first case it genuinely does not fit would see the record ignored rather than amended —
exactly the failure `CON_15`'s own `Why` describes for a declared default. What this catches is nobody
noticing. An entry names the alternatives that were real, so a plan reaching for one of them is either
correcting a decision that has stopped fitting or repeating an argument somebody already had and lost, and
those need opposite responses from the person at the gate. Without the rule both arrive as a plan that
simply does the other thing.

**Why `Instead of` is what makes the rule decidable.** A reader judging a plan against an entry has to
know which alternatives that entry weighed. Without the clause the rule would ask whether a plan
contradicts a paragraph, with nothing saying what contradicting it would look like — so the clause being
required (D-70) is what this rule rests on, and the two are a pair.

**Why no analyzer could decide it.** Whether an approach contradicts a paragraph of prose is a judgement
about language. §7.2's second test is the one that disqualifies most candidates and it passes here, which
is also why this is a constraint rather than a schema rule about the record.

**What it costs, stated plainly.** The global library grows to seventeen rules in force, and every one is
read at every interrogation of the artifact it applies to. This one applies to a plan, which already
carries `CON_06`, `CON_14`, `CON_15` and `CON_18` — the most-governed document in the chain. It also fires
only in a solution that has a decision record, so for most solutions it is a rule that can never trigger,
which is the shape `CON_11` exists to be suspicious of. It is admitted anyway because the record it
governs is the one a stage reads for alignment: a rule that fires rarely and prevents a silent
contradiction of something a person wrote is not decoration.

**Rejected.**

- *Leaving the record advisory* — what shipped in D-70, and reversed on a request. The argument for it was
  that reading the record is what prevents an unknowing departure. True, and it rests on a stage choosing
  to read something nothing requires it to read, which is the advisory layer §2.1 puts at the bottom for
  a reason.
- *Rewording `CON_15` to cover both records* — refused above, on §7.4 and on concreteness.
- *A schema rule instead* — a schema can check that a plan has a `Shape and departures` section, which it
  already does. It cannot tell whether what is in there addresses the decision the plan contradicts, which
  is the whole content of this rule.
- *Making the theme bind the same way* — refused for the reason D-71 gives: *a colour outside the
  theme* is mechanically decidable, so §2.3 puts it in a linter, which fails a build where a constraint
  cannot.

### D-73 — A decision whose facts went stale is corrected and dated, not superseded

**Governs:** §20

**Decision.** Four forms, and which applies turns on **what moved**:

| Form | When | What happens to the entry |
|---|---|---|
| `Superseded by` / `Superseded in part by` | a later entry **decided differently** | text untouched; the later id carries the reasoning |
| `Reversed in part by` | the **conclusion** was reversed | text untouched; the later id says what reversed and why |
| **`Corrected <date>`** | the decision and its reasoning stand, and a **fact inside the entry** — a list, a count, a mechanism, a path — no longer describes what is true | the fact is brought current and a dated line says what changed and which section owns it. No reasoning is rewritten and no id is issued |
| a quotation left as read | the entry **quotes** something that was wrong when quoted | the quotation stays, annotated: it is the wording the decision was taken against |

**And an entry states a fact that lives elsewhere at its peril.** Where a count or a list has an owner
in §1–18, prefer naming the section over repeating it — `Governs` already points that way. Applied when
an entry is next touched rather than swept through seventy-two of them: the value is per entry, and a
sweep is the large edit D-44 declined.

**Why.** The preamble covered reversal and nothing else, so an entry whose *reasoning* stood while a
list inside it went short had no recorded form — and three entries improvised three different ones.
D-15 annotated a wrong quotation, D-28 wrote `Superseded in part by D-40`, D-70 wrote
`Reversed in part by D-72`. None of the three describes the case that actually recurs: D-43 states what
the keystroke layer denies *"so nobody has to rediscover it"* and named four files, because D-67 and
D-70 had not yet protected the other two. Nothing was decided differently and nothing was reversed. The
list was simply true when written, and a reader who did what the heading invited learned a guarantee a
third short.

**Why the distinction is load-bearing rather than tidy.** The other three forms all say *the decision
moved*, and a reader meeting one goes looking for the entry that replaced it. On a stale fact there is
no such entry, so a supersession note sends them hunting for something that does not exist and quietly
puts a live decision out of force on the page. A correction says the opposite thing — this still binds,
and one fact in it has been brought current.

**Why dated.** The date is what separates a correction from an edit. §20's whole promise is that a
commit citing an id still means what it meant, and an undated change to an entry's text is
indistinguishable from the silent rewrite the preamble forbids. A date and a named cause make the
change auditable against the commit that made it.

**Why a quotation is the exception.** D-15 quotes `CON_07` revision 1, which claimed a hook enforced
the declared file set when none ever had. That is not a stale fact — it was false when written, and it
is the wording the decision was taken against. Correcting it would make the record describe a
conversation nobody had. The test is whether the entry asserts the fact or reports having read it.

**Why this earns an id at all.** §20's test is *"foreclosing an alternative somebody would otherwise
reach for"*, and three entries reached for three different alternatives — which is the strongest
evidence available that the alternative is reachable. The preamble does carry format rules without ids,
but those state what the fields *are*; this is a procedure for a recurring situation with a `Rejected`
list worth keeping, which is what an entry is for.

**Rejected.**

- *No id, in the preamble beside the other format rules* — the first answer, and withdrawn. It reads as
  a field convention where it is a procedure, and it would leave the three forms already improvised
  with nowhere to be recorded as weighed.
- *Calling it a supersession* — it says a decision changed when none did, and it puts a binding entry
  out of force on the page. A reader then looks for the replacement and finds nothing.
- *Calling it a reversal* — the same, more strongly: a commit citing the id would stop meaning what it
  meant, which is the one thing the monotonic rule exists to guarantee.
- *Editing the stale fact in place with no date and no cause* — the cheapest, and it is the silent
  rewrite the preamble already forbids. The record would describe a conversation nobody had.
- *Deleting the stale fact and leaving no trace* — loses why the entry said it, and makes a corrected
  entry indistinguishable from one that never claimed anything.
- *A sweep removing every count and list from all seventy-two entries now* — the thorough version, and
  declined on D-44's grounds: a 72-entry restructure of the file the framework is specified in, for a
  benefit that arrives per entry. The rule above applies it as entries are touched instead.

### D-74 — The profile tier is filled, and a profile's rules are found by its name

**Governs:** §7.2, §7.3, §11.5

**Superseded in part by D-76.** The tier no longer resolves from a profile a solution records. A
solution is judged against the rules its declared stack marks `true`, and ids are unique across every
profile's document rather than per profile. The three rules and the reasons for them stand.

**Decision.** `PRO_nn` constraints become real. Each profile may ship one document, embedded in the
binary and resolved by the profile's own name, so that giving a profile its first rule is adding a file
and a profile with no file has no rules rather than an empty set. The profile tier resolves from the
profile a solution records, between the global library and the solution's own. An explicit path still
wins, which is the seam an external profile package would arrive through. `dotnet-default` ships three
rules; `ml-tensorflow` ships none.

**And stack neutrality is narrowed to the tier it belongs to.** §7.2 said *"No constraint may name a
language, framework or package"* unqualified. That is true of the global library and false of the tier
whose whole purpose is to hold the rules that need a name — and stated that broadly it is the sentence
that would have had somebody reject the first profile rule written as a violation of the framework.

**Why.** The tier was wired and empty, and three documents told a person otherwise: `draft profile`
printed *"a different profile resolves a different set of PRO_ constraints and templates, so run
'sdlc constraints' afterwards"*, the component dictionary said the `profile` key decided which rules
resolved, and the first-cycle guide listed the tier as though it had content. None of it was true. The
parameter existed, no caller passed it, no profile shipped a document, and `sdlc constraints` printed an
identical set before and after a migration. **A tier that resolves to nothing is indistinguishable from
one whose document was dropped from the build** — the same class of failure as D-41's packed tool
shipping no library at all, and it survived three iterations because every test read the library from
disk.

The alternative was to correct the wording and leave the tier reserved, which §10.2 does honestly for
the AppHost environment variables. Rejected because the two cases are not alike: §10.2 says in as many
words that nothing reads them, and these three said the opposite at the one moment a person is deciding.

**Why three rules, which is the part that looks thin and is not.** §7.2's second admission test is that
no analyzer, schema or hook could decide it, and most candidates for a stack rule fail it — *one folder
per feature* is a fitness function, *commands go through the mediator* is an analyzer, *liveness is
mapped* is already an architecture test (§11.1). What survives is the judgement half of three
convictions this framework has paid for: a criterion about the browser proven by parsing an HTTP
response, a plan naming the kernel and selecting none of it, and a proof about stored data run against
an in-memory provider. Each needs a package name in its `Forbids` clause and so could not have been
written anywhere else.

**Why no file-wins fallback, unlike the global library.** D-41 keeps a discovered file ahead of the
embedded copy so that editing the global library here takes effect with no rebuild. A profile's rules
get no such chain: the global set is edited constantly and a profile carries three rules that move
rarely, and a second discovery order would be two ways to answer one question — which is exactly how
`ProtectedPaths` ended up with a front door and an open back door. The cost, named: changing a profile's
rules during the framework's own development needs a rebuild.

**Rejected.**

- *Rules in `StackProfile` as string arrays*, beside the `Notes` already there. A constraint document is
  parsed, validated against the four-part shape and rendered; as a C# array it would be the one
  constraint library no reader could read as a document, and the suite's shape assertions would run over
  something no author ever saw whole.
- *One document for every profile, with the applicable rules selected inside it.* A selection rule, in a
  file, about which of its own entries apply — and the first profile wanting a rule the others must not
  have would need a syntax for that. A file per profile makes the question disappear.
- *Writing rules for `ml-tensorflow` too, so the tier looks uniform.* Nothing has been built from that
  profile. A rule written ahead of the first real solution is a guess carrying the authority of a
  constraint, which is the argument that already keeps a skeleton out of it (D-68).

### D-75 — The stack options menu is a second file, and nothing ever renames it

**Governs:** §8, §11.5, §16

**Superseded in part by D-76.** `doctor` no longer counts rows differing from a recorded profile —
there is none — and an empty declared stack is sound rather than a problem. The menu also lists every
stack rule, and its empty table is the default profile's. The menu being a report nothing renames stands.

**Decision.** `sdlc draft techstack` writes two files. The draft stays what it was — a byte copy of the
declared stack, which a person renames into place. The second is **a report** under `.sdlc/reports/`:
every concern the shipped profiles know about with what each declares for it, the alternatives this framework has
argued, the choices it refused, and an empty table of the recorded profile's concerns for a solution
starting over rather than amending a row. The menu is derived from the shipped profiles, rewritten on
every run, read back by nothing, and never renamed into place. `sdlc doctor` gains a `declared stack`
check that reports a row declaring nothing, a file with no rows, a file carrying the menu, and how far
the table has moved from the recorded profile.

**Why a menu at all.** D-68 settled that a solution wanting rows another profile declares copies them
in — a one-time edit against permanent merge machinery. What it did not settle was how anybody finds out
what those rows say: the tables live in the framework's source, and an adopted solution's declared stack
opens with four empty rows and an instruction to fill them in from what the repository actually uses.
Open question 8 makes the same argument for a profile's rules as a menu — every item was already argued
by somebody, which beats a blank page and costs nothing.

**Why it is a report rather than a file beside the draft, which is where this first put it.** The menu
outlives the rename — nothing deletes it — so an untracked copy would sit in `.sdlc/` indefinitely. And
`evidence` stages untracked paths with `--intent-to-add` before diffing, excluding only ignored ones, so
that copy would reach **every later cycle's diff as a file no plan declared** — which is what `CON_07.2`
fires on. D-69's reports directory already carries exactly that argument and the `.gitignore` that
answers it, and the menu matches its definition without stretching it: a copy of what a command printed,
worked through rather than read once, and read back by nothing. The cost is one directory of separation
from the draft, paid for by the drafter printing the full path.

**Why it is a separate file rather than a section of the draft.** Two reasons, and the second decides
it. The draft is a byte copy, and that is the whole guarantee of the rename (D-67): candidates inside it
would make placing it apply more than what somebody changed. Worse, they would survive into the declared
stack — and a declared stack listing answers the solution has **not** chosen is one a plan can reach into
and truthfully not be departing from anything, which is precisely the deadlock protecting that file
closed. The marker at the top of the menu is what lets `doctor` report the one remaining route in, a
person pasting it.

**Why the empty table is in the menu and not the draft.** It was asked for as the draft itself — a blank
form to fill in. Refused, and the refusal is the useful part of this entry: the declared stack is never
blank, because adoption writes it from the template, so a blank draft would make the rename a wholesale
replacement — and every row nobody retyped would be dropped from the one file every plan is judged
against, silently, discovered by a later plan that had stopped departing from anything. In the menu it is
a deliberate copy sitting in the draft in front of the person before they place it.

**Why derived rather than authored.** Every answer in the menu is a row some profile actually declares,
so the menu cannot come to disagree with the tables it was built from. Two short sections are authored —
an alternative the framework weighed and a choice it refused — because those are argued in §11 and no
profile declares them, so derivation would lose them. Each carries its reasoning: D-67's argument against
a drafter proposing rows is that a tool filling a table in makes a judgement while looking like
bookkeeping, and a candidate with only a name beside it in a menu reads as an endorsement.

**Why drift is information and not a fault.** A profile migration moves the solution document and leaves
the declared stack exactly as it was, by design, and nothing reconciles the two — so a solution can
record one profile and declare another's stack indefinitely. Worth saying, because a person expects a
migration that does not happen; not worth failing, because the file is theirs and departing is what
`CON_15` exists to let them argue for. A panel that cried wolf at every customised stack would teach
somebody to stop reading it, which is the failure `CheckOutcome.Elsewhere` already exists to avoid.

**Rejected.**

- *A resolution chain — one default profile plus override bundles, merged when the stack is read.* The
  shape this started as. It makes the declared stack a derived file, which is the whole of what D-68
  refused: the thing every plan is judged against would be generated rather than written, and *a person
  places it* would stop meaning anything.
- *An overlay applied once at adoption.* Materialised, so nothing is derived at read time — but the
  precedence rule then lives in code and the person inherits a merge they did not make and cannot see.
  Strictly worse than the menu for more machinery.
- *A third composed profile per useful combination.* Admissible, and where overlap between profiles is
  already handled (D-68). Declined for now because it needs a new profile per combination and nothing
  has asked for one; the menu serves the same need without a new name.
- *Printing the menu to the terminal instead of writing it.* Keeps the byte-copy guarantee absolutely
  intact and was the safe answer. Refused because a person then edits one surface while reading another,
  and a file is what makes the menu available while the draft sits open.
- *Gating the rename on a check that the menu is absent.* Nothing is in the write path: a person renames
  by hand, so there is no command to refuse. `doctor` reports it instead, which is what `doctor` is for.
- *Keeping the menu at `.sdlc/techstack.options.md` and ignoring it with an entry of its own.* The shape
  the first implementation had, and the ignore would have been a new framework-owned file in every
  governed solution — `.sdlc/` holds tracked records, so it could not use the reports directory's blanket
  `*` and would need a named entry maintained alongside the constant. D-69's directory already exists and
  already argues the case.

### D-76 — The declared stack lists its own rules, a person places it, and no solution records a profile

**Governs:** §5.3, §7.3, §8.2, §8.3, §9, §11.5

**Superseded in part by D-81.** `sdlc init --profile` and `sdlc draft profile` are no longer answered
with a sentence: the first is refused as an option `init` does not take, the second as a drafter that
does not exist. And an old `profile` line is no longer read as a fallback for a stack file that lists no
rule: such a solution is held to none. Everything else stands.

**Corrected 2026-10-13:** this says the file holds two tables, which was the count when it was written.
D-77 added a third, for the build settings files, and §8.2 owns the count. Everything this entry
decides about the first two stands, including that the skeleton brings the build settings.

**Decision.** `techstack.md` holds two tables and nothing about the stack is selected from anywhere
else. The first pins choices, as before. The second, under `## Rules`, lists stack rules one row each —
a `PRO_` id, `true` or `false`, and what the rule holds a plan to — and a solution is judged against
exactly the ones marked `true`. Adoption does not write the file: `sdlc draft techstack` writes a first
draft of the default profile's rows with every shipped rule switched on, a person cuts it down and
renames it, and `sdlc new` refuses to open a cycle until it is there. A file with nothing in it is a
legitimate declared stack: nothing pinned, no stack rule in force. `solution.md` drops to three keys;
`sdlc init --profile` and `sdlc draft profile` are gone and answered with a sentence; the .NET build
settings are written by `sdlc scaffold` with the skeleton rather than by adoption.

**Why.** The `profile` key did three jobs and was visible in none of them. It seeded the declared stack
once, at adoption; it selected the `PRO_` tier on every interrogation; and it was the yes-or-no that let
the .NET skeleton and build settings be written. A person reading their own stack file could not see the
second or third — recording `dotnet-default` brought three rules with nothing in the solution saying so,
and the file's own header said nothing in it bound by itself. And the key was asked for at the wrong
moment: on the adoption command line, before anybody had said what the solution was built from, with
the answer protected the instant it was written.

**It was also two sources of truth, and they had already disagreed.** A refresh resolved the profile
from the command line rather than from the recorded document, so a plain `sdlc init` on a scaffolded
solution recording `ml-tensorflow` would have written `.editorconfig`, the props files and `global.json`
into a Python repository. Nothing failed on it because no test re-ran adoption without the flag on that
profile. Removing the option removes the second source rather than reconciling it.

**Why the rules are rows rather than a name.** A flag such as `rules: dotnet-default` in the stack file
was the first shape considered, and it has the fault the profile had: it is a pointer somebody has to
look up. The expanded form puts what binds on the page, makes declining one rule an edit to the line
that says it is on, and costs one thing — an id has to be unique across every shipped document, because
a row names a rule by id alone. D-74 numbered each profile's rules from 01; one document ships rules
today, so the change is free now and a constraint on the next.

**Why the approval stands in for the skeleton flag.** `StackProfile.Scaffolds` existed so a profile
declaring Python could not be handed twenty-five .NET templates. There is no profile to ask, and nothing
needs to be: `sdlc scaffold` already runs only behind a plan a person approved, and a plan declaring
those files says the solution is .NET. Where the declared stack pins something else, that plan had to
argue the departure under `CON_15` to be approved at all. The same reasoning moves the build settings:
D-35 put them at adoption because they constrain nothing until code exists, which is equally the reason
nothing is lost by their arriving with the code — and at adoption they are a decision about a stack
nobody has declared.

**Why every draft starts from the default, adopted repositories included.** D-67 gave an adopted
solution four empty rows, because the file was written straight into place and a guess written down as
a declaration is worse than nothing. A draft is not a declaration. It is read and cut down before it is
placed, and the adoption skill edits it from the repository first — the one place reading the codebase
is right. The risk that remains is a default renamed unread onto a codebase that is not .NET, and it is
said out loud in the skill and in the drafter's own output rather than designed away.

**Why an empty file is the open state, and needs no marker.** While adoption wrote the file, empty meant
nobody had got round to it, and `doctor` was right to call it a problem. Now a person places it, so the
rename is the statement: present and empty is a decision that nothing is pinned, absent is nobody
having decided. That is the distinction D-53 drew for `platform: local` — recorded rather than inferred
— and here the record is the file's existence. The cost, named: on an empty stack nothing keeps the
second cycle from choosing a different framework from the first.

**Why `sdlc new` is where the missing file is refused.** A cycle that reached its plan with no declared
stack would be judged against nothing, with no stack rule in force and every gate green — the failure
D-74 describes, a tier that resolves to nothing looking exactly like one that was dropped. Refusing at
the plan would spend an intent and a spec first. Refusing at the open spends nothing, and the remedy is
one command and one rename.

**What an existing solution keeps.** Its `solution.md` still carries a `profile:` line no tool may take
out, and its stack file lists no rule. That line is read as a fallback — only for a stack file that
declares defaults and lists no rule — so upgrading the binary drops nothing a solution was held to. It
ends the moment the stack file says anything about rules, or is emptied. `doctor` says where such a
solution's rules still come from, and `draft solution` writes the document without the line.

**What this costs.** A rule a later release adds reaches no existing solution until somebody adds its
row; `doctor` names it so it is declined rather than unseen, and that is the right way round — an
upgrade of the binary should not change what a plan is judged against. The third column of a rule row
can go stale against the rule's wording; nothing binds on it, and `sdlc constraints` prints the rule
itself. And the declared stack is no longer only "a declaration of defaults": one table of it selects
rules, which every sentence that said nothing in the file binds had to be rewritten to say.

**What is left undone, deliberately.** `standard: profile` is still spelled that way, though no solution
records a profile. The key means what it meant — new work follows the framework's conventions rather
than the surrounding code — and renaming the value would touch every existing protected `solution.md`
for a word. Worth doing with the next change to that file's keys, not for its own sake.

**Rejected.**

- *Dropping the skill's question and keeping the `--profile` option.* Delivers the default without
  asking and leaves both sources of truth, the invisible rule selection and the refresh defect.
- *A named flag in the stack file — `rules: dotnet-default`, or `dotnet: true`.* The profile key moved
  one file over. Still a pointer, and the boolean bakes one stack into the file's format.
- *Inferring the rules from the `language, framework` row.* A typo or a reworded cell silently turns
  the tier off, in a file a person edits by hand — a tier that resolves to nothing and says nothing.
- *A marker line for the open state.* The placed file already is the statement; a marker would be a
  second one that could disagree with it.
- *Keeping the build settings at adoption, on the default.* A solution on another stack deletes five
  files once. Cheaper to build, and it is adoption deciding the stack on the way past.
- *A new skill between adoption and the first cycle.* Everything that happens there already has a home
  — the stack in `draft`, rules and a theme in `propose` — so it would orchestrate three skills and add
  a nineteenth to every index. Worth revisiting if adoption still reads as overloaded.
- *Removing `ml-tensorflow`.* The options menu is derived from the profiles, so its rows would leave
  the menu with it. It stays as menu content and governs nothing.
- *Empty rows for an adopted repository's first draft.* The argument for them was about a file written
  into place, and no longer applies to a draft.

### D-77 — The build settings are rows in the declared stack, and a cycle brings them

**Governs:** §8.2, §8.3, §9, §11.3, §15.2, §18

**Superseded in part by D-81.** `--adopt-standard` is no longer answered with a sentence; it is refused
as an option `init` does not take. Everything else stands.

**Decision.** `techstack.md` takes a third table, `## Build settings`: one row for each of the five
.NET build settings files, with `true` or `false` and what the file does. `sdlc init --adopt-standard`
is removed and answered with a sentence. `sdlc scaffold <id>` writes build settings on an adopted
solution — never the skeleton — and only the files the cycle's approved plan declares. The first stack
draft starts those rows `true` on a scaffolded solution and `false` on an adopted one. `sdlc stack`
prints the declared stack and which of the files it takes are not on disk. `standard` stays in
`solution.md` and is moved by `sdlc draft solution --standard`.

**Why.** D-76 said adoption decides nothing about the stack and moved these files to the skeleton, and
left one thing behind: a flag on adoption that wrote five .NET files into an existing codebase. That
flag ran before the stack was declared, so it chose .NET settings for a solution nobody had described
yet, and it never read the stack once there was one — `sdlc init --adopt-standard` in a repository
declared on another language wrote a `global.json` into it. D-76 named that defect for a plain refresh
and fixed it there; the flag kept it.

**The flag also did two things that were never one decision.** It recorded which conventions a plan
defaults to, which applies to any stack, and it wrote files that only mean something on .NET. One
switch for both meant a team could not have the first without the second.

**Why the files arrive through a cycle rather than on the strength of a row.** `Directory.Build.props`
makes warnings errors, so the file landing is the moment a build can fail on code nobody touched.
Written at adoption, or by whatever ran next after a row was switched on, that failure belongs to
nobody: no intent asked for it, no plan declared the file, and the evidence comparison reports it as
undeclared in a cycle about something else. Inside a cycle whose plan declares it, the same failure is
the planned scope of one change a person approved. This is D-35's argument for code, applied to the
files that judge code.

**Why the plan is the authority and the binary does not read the row.** D-76 already made the approval
stand in for a flag: a plan declaring .NET files says the solution is .NET, and where the declared
stack says otherwise the plan had to argue the departure under `CON_15` to be approved. A second check
in the binary would be a second answer to a question the gate has answered. So a row switched off does
not forbid a plan that argues for the file, and a row switched on writes nothing without one.

**Why rows rather than one switch.** D-76 rejected `dotnet: true` in the stack file as a pointer
somebody has to look up, which bakes one stack into the file's format. A row per file puts what a
solution takes on the page, makes keeping your own `.editorconfig` an edit to one line, and carries
what each file does beside its name. The options menu carries the rest — what is in each file, what it
costs a codebase that predates it, and which other file it is useless without — because a draft that
starts five rows at `false` is otherwise five names and nothing to choose by.

**Why the rows start `false` on an adopted repository.** Every other part of the first draft starts
switched on, and D-76 accepted that a default renamed unread is a risk said out loud rather than
designed away. This one is different in kind: a rule switched on by accident produces a finding
somebody reads; warnings-as-errors switched on by accident produces a repository that does not build.

**Why this takes up what D-56 rejected.** D-56 weighed declaring the opt-in in `techstack.md` and
refused, for two reasons: the file was unprotected and cycle-owned, and it declared what a solution
*is* rather than which rules bind it. D-67 protected the file and D-76 made one of its tables select
rules, so neither holds. The third reason — that a per-area record would describe a precision the
mechanism does not have — is not disturbed: the rows are whole-repository, as the files are.

**Why `sdlc stack` is a command.** Whether a declared file is on disk is a file listing, and what a file
listing can decide the binary owns (§15.2). A plan stage working it out by reading would get it right
most of the time, and this is the comparison that decides whether a plan declares a file that makes
warnings errors. The rest of the comparison — whether the code uses what the defaults declare — needs
reading the code, and stays with the stage that reads it.

**Why a plan closes the gap only when its intent asks.** A declared file that is missing is visible to
every plan written afterwards. If each one picked it up, the first cycle after a row was switched on
would absorb a repair of every warning in the codebase, whatever it was about. `CON_05` keeps that out
of a spec whose intent did not ask for it, so no criterion would stand behind the work and nothing in
the cycle would prove it. So a plan about something else names the gap and leaves it.

**What the incoherent state became.** D-56 named one state where the files on disk and the rule a plan
reads disagreed: the flag on an update wrote the files and could only print the key. That state is
gone, because nothing writes both. What replaces it is deliberate and visible: a row switched on with
no file yet, which `sdlc stack` and `doctor` both report, and which `doctor` does not call a fault —
the interval before the cycle lands is the design working.

**What this costs.** Taking the build settings into an existing codebase is slower: a draft, a rename
and a cycle, where it was one flag. A scaffolded solution's skeleton still arrives with all five files
whatever its rows say, because it does not compile without them — so on that path a row switched off
is a statement the skeleton overrides, and the draft's own text says so. And `scaffold` now means two
things, which its refusal on an adopted solution has to explain.

**Found on the way.** A plan declaring `.editorconfig` was reported as not declaring it: the reader
recognised a path by an extension or a slash, and a dotfile at the root has neither. The same
comparison lists that file as governing. Fixed in the reader, because `scaffold` reads the same
section to decide what an adopted solution's plan asked for.

**Rejected.**

- *Running `sdlc scaffold` before every build, writing whatever is missing.* The first shape proposed.
  The files would reach a cycle's diff undeclared, the fallout would land in whichever cycle built
  next, concurrent builds would each write the same five files, and the skeleton would come back every
  time a team deleted its reference slice. Fixing the first of those needs an exemption from the
  declared-files comparison, which is an exception carved into a signal that has none.
- *One switch in the stack file.* D-76 rejected it for the rules, and the reasons transfer.
- *A standalone command that reads the stack and writes the files outside a cycle.* Closes the
  ordering fault and the other-language fault, and leaves the one that matters: a file that makes
  warnings errors, arriving with no plan behind it.
- *Making the binary refuse a plan that declares a file whose row is `false`.* A second gate on a
  question `CON_15` already puts to the plan, and the first place a declared default would bind
  mechanically — which §8.2 says is a `LOC_` constraint's or an architecture test's job.
- *Moving `standard` into the stack file as well.* It would put every stack choice in one file, and it
  is the one key every solution has whatever it is built on. Left where it is, with D-76's note that
  the value `profile` is misnamed still standing.
- *Starting the rows `true` everywhere, like the rest of the draft.* The cost of an unread rename is
  not a finding here; it is a broken build.
- *Reading the presence of the files as the declaration.* D-56 rejected it and the reason stands: an
  old repository plausibly has its own `.editorconfig`, so presence proves nothing about intent.

### D-78 — There is no pre-push hook, and a cycle branch may be pushed at any time

**Governs:** §2.1, §8, §9

**Decision.** Adoption writes no git hook and names none to install. `.sdlc/hooks/pre-push` and the
`git config core.hooksPath .sdlc/hooks` line are gone from `init`, from `doctor` and from the
solution's `README.md`, and `.sdlc/hooks/` is no longer a governing path in the declared-files
comparison. A cycle branch may be pushed whenever somebody wants it on the remote. On a `local`
solution `doctor` says that nothing mechanical decides what reaches the default branch, where it used
to name the hook as the one layer that did.

**Why.** The hook ran `sdlc pr-ready` before a push of `cycle/*` and refused when the cycle was not
ready. Three things were wrong with that, and each is enough alone.

*It was written and almost never ran.* Installing it is `core.hooksPath`, which is per clone and
untracked, so it ran only where somebody had remembered a line printed once at adoption. A file under
`.sdlc/hooks` that looks like enforcement and does nothing is §2.1's own failure mode — "a directory
that appears enforced and is not" — written by the framework into every solution.

*Where it did run, it refused the wrong thing.* A push is not a merge. It refused every push of a cycle
branch that had not yet verified, which is every push of work in progress: a backup, a second machine,
a draft pull request opened for an early look. Pushing often is a habit worth having, and the hook made
governed work the one place it was refused.

*What it guarded has two guards already.* The `pr` skill asks `sdlc pr-ready` before it pushes, every
time, and that is the stage that pushes in order to open a pull request. The pull-request check refuses
an unready chain at the merge, which is the guarantee. Between them the hook saved one round trip
through CI, for a person pushing by hand, on a clone where it happened to be installed.

**On a local solution it guarded nothing at all.** `local` means no remote, so there is no push for a
pre-push hook to see, and a `git merge` into the default branch never passed through it. The
guides and `doctor` called it "the only mechanical layer" there. The true sentence is that a local
solution has no mechanical layer over a merge, and that is what they say now.

**What it costs.** A person who pushes an unready cycle branch by hand and opens a pull request learns
it from a red check rather than at the prompt. That is the check working. A solution adopted before
this still has the file, and a clone that set `core.hooksPath` still runs it — `sdlc pr-ready` has not
moved, so it works as it did — until somebody deletes the file or unsets the key. Update does not
remove it: it was never installed by the framework, so removing it is not the framework's either.

**Rejected.**

- *`init` running `git config core.hooksPath` itself.* It installs on the one clone that ran adoption
  and on no other, since git gives a repository no way to switch hooks on for whoever clones it. The
  adopter would see it working and every teammate would have the silent absence. It also sets a key
  that disables whatever a team keeps in `.git/hooks`, and it automates a check that refuses pushes
  nobody wants refused.
- *A skill setting `core.hooksPath`.* The `pr` skill is the only one that pushes, and its first
  instruction is the command the hook runs. It would install a hook to repeat what it had just done.
- *The pre-tool hook refusing an agent's `git push` of an unready cycle.* It needs no per-clone
  install, which is the hook's real defect answered. But `HookPolicy` is a pure function over one tool
  call, on the critical path of every edit, and verifying a chain there reads and hashes a cycle from
  inside it. It matches command text, so a bare `git push` on a cycle branch says nothing it can
  match. And it would still refuse the push of work in progress.
- *Keeping the hook and narrowing it to the default branch.* What reaches the default branch on a
  remote is branch policy's to decide, and the remote is the only place that can enforce it. A local
  copy of that rule is the same uninstalled file with a different pattern in it.
- *Removing the file from existing solutions on update.* Update rewrites what the framework owns and
  deletes nothing. A stale hook that still works is not worth the first deletion.

### D-79 — Adoption ends with one next step, and `doctor` names the one after it

**Governs:** §9, §15.2

**Superseded in part by D-94.** Adoption drafts every file now, so the block names each draft and its
rename rather than one command, and a placed paragraph still carrying the prompt is a `doctor` problem.
That `doctor` prints the same block from the same list, read from disk, stands.

**Decision.** `sdlc init` ends its report with a sentence saying what a person still has to place
before the first cycle, and one command: the first step not yet done. There are two steps, in this
order — a paragraph in `solution.md` saying what the solution is, from `sdlc draft solution`, then the
declared stack, from `sdlc draft techstack`. `sdlc doctor` ends with the same block, read from the same
list. When both are placed and no cycle exists the step is the first cycle; once a cycle exists
neither command prints the block. `Solutions/Setup` is the one list, and it reads disk every time.

**Why.** The report ended with four commands under "Next", and a fifth instruction below them. One was
the step. `sdlc constraints` and `sdlc decisions` are views, and in a repository adopted a minute ago
the second shows nothing. `sdlc propose constraints` was described as "what the history says the rules
are missing" to somebody with no history. A person reading that has five things and has to work out
which one is theirs to do — and the one thing they had to do that was not there at all was the
paragraph, which a cycle once went through with the placeholder still in it.

**Why the paragraph comes first, when the stack is the one that blocks.** What a solution is built from
follows from what it is. It is also the `init` skill's first question, so with the stack named first
the command said one thing and the skill, in the same conversation, asked another. The stack is still
named in the sentence, and the block says no cycle opens without it when it is the step left.

**Why `doctor` and not something adoption remembers.** Each step ends with a person renaming a draft
into place, and a rename runs nothing. So "what now" after the first step cannot come from what
adoption printed. `doctor` already reads this repository's state and already reports a missing stack;
it prints the block from disk, which also makes it right when somebody places the stack first.

**A missing stack is still a problem in `doctor`, and the block does not change that.** The check is
the fault and exits 1; the block is the order to do things in. The placeholder paragraph is not a
check: no stage reads that prose, so calling it a problem would fail every freshly adopted repository
for something that breaks nothing.

**Rejected.**

- *The stack first, since it is the only step that blocks a cycle.* The order the report had. It puts
  the answer that depends on the other one first, and it disagrees with the skill.
- *Listing every step with a number against each.* A shorter list is still a list, and the report's
  job at that moment is to say what to do now.
- *Keeping the views under "Next" as things worth knowing.* They are in the command sheet adoption
  writes into `.sdlc/README.md`, which is where somebody looking for a command looks.
- *A new command for it — `sdlc next`.* `doctor` is already what a person runs to ask what state the
  repository is in, and a second command would be a second answer to keep in step with it.
- *`sdlc status` with no cycle id.* The status panel is about one cycle, and before the first one there
  is no cycle for it to be about.
- *Making the placeholder paragraph a `doctor` problem so the exit code carries it.* Nothing reads the
  prose. A non-zero exit for it would make `doctor` unusable in a script on every new repository.
- *Proposing constraints as a step.* It is a branch the `init` skill takes when an interview answer
  survives the admission tests, and for most solutions none does (§9). A step that is usually skipped
  teaches people to skip steps.

### D-80 — Six skills keep a task list where the host has one, and a stage's stops at the hand-back

**Governs:** §15

**Decision.** `init`, `intent`, `spec`, `plan` and `build` each tell the agent to open a task list of
that skill's own steps, if its host has one, and to mark each done as it finishes. The list holds
nothing after the skill's hand-back. `batch-build` opens one too, shaped differently: one line per
cycle, ticked only when that cycle's outcome is `passed`, and `build` keeps none of its own while a
batch is running it. No list is ever the record: what is done is what is on disk and what `sdlc
doctor`, `sdlc status` and `sdlc cycles --pr-ready` report. A host with no task list runs every step
unchanged.

**Why.** These five are the long ones a person sits through — an interview, a loop of rounds, an
implementation — and the steps that get dropped in them are the quiet ones: the paragraph left on its
placeholder, the constraints never considered, the evidence recorded and not committed. A visible
list makes a skipped step something a person can see while there is still time to say so.

**Why it stops at the hand-back.** A skill stops and a person decides whether the next stage runs. A
list showing spec, plan and build under an intent that has just converged invites the agent to carry
on, and tells the person the decision is already made. So each list ends on the hand-back, and the
next stage is named where it always was, by the status panel.

**Why it is never the record.** The agent writes the list and the agent ticks it, in one session, on
one host. That is the advisory layer of §2.1 with a tick box on it. In `init` two items are closed
only when `doctor` stops naming them, because a draft written is not a draft placed; in `build` the
items come from the plan's `Files` section, so the list cannot widen what was approved.

**Why the batch has one, when it is meant to run unattended.** Unattended is not unobserved. The
first batches are worth watching, and remediation has no ceiling (D-66), so the question somebody
glancing in has is which cycle is still going — and without a list that is answered only by the report
at the end. There is no hand-back to protect inside a batch either: starting the run was the
decision, so every cycle on the list is one a person already agreed to.

**Why its tick means `passed` and not "finished".** A batch has four outcomes and a tick box has two
states. Ticking a cycle that escalated would show a column of ticks over a run that needs a person,
so the other three outcomes are written onto an unticked line. And the build stage stands down
inside a batch because seven lines per cycle would bury the one line per cycle that is the point —
which also bounds the list at the size of the batch.

**Why "if your host has one".** Each host has its own tool for this under its own name, some have
none, and a skill ships to all of them unchanged (D-47). Naming a tool would make a skill fail or
quietly do less on every other host.

**Rejected.**

- *A task list for the whole cycle, opened by `intent`.* The shape first asked for. It shows six stages
  nobody has yet agreed to run, and it is a second answer to what `sdlc status` already says.
- *The binary writing a checklist file into the cycle.* A file is a record, and this one would be ticked
  by the thing it is checking. The record of a stage is its artifact, its rounds and its evidence.
- *Every skill keeping one.* `status`, `constraints`, `draft`, `suggestions` and `propose` are one
  command and a report. `interrogate`, `refine`, `verify` and `remediate` each already write what
  they did as findings or a verdict, and `pr`, `steer` and `resolve-conflict` are short or already
  report per cycle. A list on a two-step skill is noise that trains people to stop reading lists.
- *No list for `batch-build`, because it already reports one line per cycle.* Where this entry first
  stood. The report arrives when the run ends, and the run is the long thing.
- *Nesting each cycle's build steps under its line in a batch.* Seven or more lines per cycle, so a
  batch of nine is a list nobody scans.
- *One item per interrogation round.* The list would grow while the artifact went in circles, which
  reads as progress at exactly the moment it is not.
- *Naming the host's tool in the skill.* Three hosts, three names, and a fourth host tomorrow.

### D-81 — What a release removed is gone, and `init` refuses an option it does not take

**Governs:** §8.3, §9, §15.2

**Decision.** The binary keeps no answer for a command or option it no longer has. `sdlc init --profile`,
`sdlc init --adopt-standard`, `sdlc draft profile`, `sdlc draft constraint` and `sdlc pr-ready --all`
each printed a sentence naming where the thing went; all five are gone. `sdlc draft` refuses a drafter
that does not exist, and `sdlc pr-ready` asks for a cycle id. `init` refuses any option other than
`--platform` and `--solution`, naming what it was given and pointing at `sdlc help`.

The same goes for two readings kept for solutions adopted before a change. An old `profile` line in
`solution.md` is no longer read as a fallback for a stack file that lists no rule (D-76): the rules come from
the declared stack alone, and a stack that lists none holds the solution to none — `doctor` says so, as
it does for any such stack. And adoption no longer names a `pull-request.yml` an earlier adoption left
behind (D-40).

**Why.** Each answer was written for somebody with a line kept from before the change, and every one of
them dates from a release this framework has moved several past. A redirect is code, a test surface and a
paragraph in three documents, and it describes something a new reader never met — the refusals sheet
listed two options nobody adopting today would think to type, and a reader took it for a live command.
What made the answers necessary for `init` was that the parser accepts any option, so removing one
would have left `init --profile dotnet-default` adopting the repository while saying nothing about the
part it dropped. Refusing what `init` does not take closes that without the binary remembering what
used to be there. The other three removals fall through to refusals that already existed.

**Why the two readings go too.** Both kept a solution adopted several releases ago working exactly as it
had, at the cost of a branch in the rule resolution, a branch in `doctor`, a notice in adoption and the
paragraphs explaining each. The project is moving from one person's machine to a team, and every
solution that matters has either moved to a stack file that lists its rules or can do so with one draft
and one rename.

**Why only `init`.** Adoption is the one command whose run is hard to take back: it writes the hook
configuration, the pipeline and the record of where the code came from, and it is a person's to run
(§5.3). Every other verb that ignores an option it does not read costs a re-run at most.

**Rejected.**

- *Keeping the answers indefinitely.* Each is a small kindness and they accumulate; the documents
  describing them read as a list of commands to anybody who never used the old ones.
- *Removing the answers and letting `init` ignore unknown options.* Adoption would run on a line that
  asked for something it no longer does, which is the failure the answers existed to prevent.
- *Keeping the profile fallback until every solution has moved.* Nothing can tell when that has
  happened, so it would never be removed. `doctor` reporting a stack held to no rule is the notice.
- *Refusing to read a `solution.md` that still carries the line.* Every such solution would look corrupted
  on upgrade, over a key that now means nothing. The reader ignores keys it does not know.
- *Refusing unknown options on every verb.* The right shape eventually, but it needs each verb's
  options declared in one place, and `sdlc help` is the only list today. Worth doing when a second verb
  has the same cost.

### D-82 — The cycle is grouped into three phases, Define, Build and Operate, and a phase is never a command

**Governs:** §3.3

**Decision.** **Define** is intent, spec and plan, ending at the planning approval. **Build** is build,
verify and the pull request. **Operate** watches what is running and feeds new work back into Define as
leads (D-89). The phases are labels over the stages that already exist: they appear in the documents and
the stage panel, and nothing runs a phase.

**Why.** The framework has grown work on both sides of the cycle it was written around — a factory that
builds unattended (D-84) and a run that reads production (D-89) — and both need a name a person can hold
for where they sit. The boundaries are ones that already exist: Define ends at the one gate (§5.1), and
Build ends where a person takes the result.

**Rejected.**

- *"Design" for the first phase.* D-16 decided there is no `design` skill, and the word would read as
  its return. It also collides with the UI/UX perspective (D-90).
- *A command per phase* — `sdlc define` running intent, spec and plan. It is the chained orchestrator
  D-16 rejected: the separate stages are where a person decides to continue.
- *"Maintain" for the third.* Operate is narrower: it reads what runs and proposes; it maintains
  nothing itself.

### D-83 — Approved plans wait on an integration branch, `development`, and reach it by a reviewed pull request

**Governs:** §10.4, §18

**Decision.** A solution has an integration branch, `development` by default and named in `solution.md`.
An approved cycle's planning documents reach it by a pull request from `cycle/<id>/plan`, which deletes
its branch on merge. Every built cycle is pull-requested back into it. It is the working head: main
receives only what a person promotes from it (D-88). The local approval remains **the** gate (§5.1); the
plan's pull request is admission to the factory, not a second approval.

**This reverses a non-goal.** §18 listed *"a shared batch queue"*, and D-22's section said there is no
shared queue. The integration branch is one, on purpose: a scheduled build needs one place to find
approved work, and a pipeline checks out one ref.

**Why a branch rather than a queue service.** It needs nothing that isn't already there. Admission is a
pull request the platform already reviews, the queue's contents are files the binary already reads, and
"queued" is "merged into `development` with no verify" — a listing, not a state somebody maintains.

**Why the name is a setting.** The factory checks it out and targets it, so the framework has to know
it, and teams name it differently. A person's file holds it because it decides where governed code lands.

**Rejected.**

- *Merging plans straight into the default branch.* The pull-request check refuses a chain with no
  verify, correctly — a plan with no code is not mergeable there.
- *A dedicated `planned` branch holding only plans, with code delivered elsewhere.* Plans and code would
  live on different branches, and keeping them in step became most of the design: status commits pushed
  onto it, resets, recreating it from main.
- *`develop`, GitFlow's name.* Weighed for familiarity; `development` was chosen, with `feature/...` and
  `cycle/...` beside it, since a branch `development` cannot coexist with branches named `development/...`.
- *Approving remotely, on the plan's pull request.* *"Remote plan approval"* stays a non-goal: the
  pull request admits, the local paste approves.

### D-84 — The factory automates four things — finding approved plans, building, verifying, opening pull requests — and nothing else

**Governs:** §10.5

**Decision.** The factory is `batch-build --pr` run on a schedule against `development`. It finds the
approved cycles with no verify, builds and verifies each, and opens a pull request per cycle. Review,
conflicts, merging and promotion are a person's, with tools for each (§10.5). **The factory never
merges.** The schedule is the confirmation D-24 asks for, given non-interactively; one run holds an
exclusive lock, so two never pick the same cycle; and the pipeline job's timeout is the spend ceiling.

**Why the schedule can be the confirmation.** D-24 asked once before an unattended run because volume is
a cost somebody should choose, and rejected no confirmation at all on exactly that ground. A schedule is
that choice, made once, by a person, where it can be read. D-80 already argued the same of the batch's
task list: starting the run was the decision.

**Why the timeout is the ceiling, and not the gate.** D-66 removed the remediation ceiling and said that
if an unattended batch is ever seen running away, the remedy is a ceiling put back deliberately. The job
timeout is that, at the layer that owns the money: it stops the run, it writes no outcome, and it leaves
the gate's progress rule as it is.

**Why it never merges.** Merging what passed was weighed. Code would reach `development` read by nobody,
the factory would need permission to push past branch policy, and two pull requests that pass apart and
break together would land with nobody watching. Merging stays a person's act until practice shows it
can be trusted to something else.

**Rejected.**

- *The factory merging the cycles that passed, leaving only conflicts and escalations as branches.* Above.
- *A state file — `queued.md` — recording what the factory took.* Merging the plan queues it, and the
  factory's branches and pull requests hold the rest (D-85). A file would mean the factory committing to
  `development` every night, unreviewed.
- *Claim and outcome commits pushed onto the shared branch*, and *a `factory/<id>` ref marking a claim.*
  Both were the factory writing to a shared branch; under this decision it writes to none.
- *One delivery branch per night, with a multi-cycle `verify-chain`.* A cycle is the unit of review and
  of rejection; batching them makes rejecting one cost the rest.
- *A scheduler or queue service.* A pipeline schedule and an exclusive lock are the whole of it.

### D-85 — Every cycle the factory touches gets a pull request — a draft when it did not pass — and a cycle's branches sit under `cycle/<id>/`

**Governs:** §10.3, §10.5, §16

**Decision.** A cycle that passed gets a pull request ready for review. A `blocked` or `escalated` cycle
gets a **draft** pull request carrying the attempted code, the verify and the evidence; its check goes
red, which is accurate. A cycle's branches are named by the kind of work:

| Branch | Who | What |
|---|---|---|
| `cycle/<id>/plan` | a person | the planning documents; deleted when its pull request merges |
| `cycle/<id>/build` | a person | a build run by hand |
| `cycle/<id>/build-unattended` | the factory | a batched build |
| `cycle/<id>/forward` | the cycle | a release-targeted fix carried into `development` (D-88) |

Any `cycle/<id>/...` branch on the remote holds the cycle back from the factory, and the sitrep names
it and why. Deleting the branch puts the cycle back in the queue. The bare `cycle/<id>` stays readable
for cycles already on one; nothing new creates it, since git cannot hold both forms at once.

**Superseded in part: D-23.** That entry rejected *"opening one for a `blocked` cycle with a warning — the
verdict is the only thing between unattended building and unreviewed work reaching a pull request."*
The fear was work that should not merge being presented as though it might. A draft is the platform's
own statement that it is not ready, and the check refuses a blocked or escalated chain at the merge
regardless — so the verdict still stands between the work and the branch, and what changed is that the
work is now somewhere a person looks.

**Why a blocked cycle needs one.** Without it, its verify lives only on a throwaway pipeline agent, so
nobody sees it, and the next run builds it again, every night, at full cost — `development` shows an
approved cycle with no verify until something merges.

**Why the branch is the claim.** The factory writes to no shared branch (D-84), so the only durable mark
that it took a cycle is something it pushed. A branch costs nothing, needs no cleanup the pull request
does not already do, and its name records how the cycle moved — which the pull request keeps after the
branch is gone.

**Rejected.**

- *`build/<id>` for the factory beside `cycle/<id>` for the author.* Two roots for one cycle; under
  `cycle/<id>/` everything about it is in one place.
- *The factory reusing `cycle/<id>`.* It is the author's planning branch and may still exist after its
  pull request merged, so its existence could not say whether the factory had taken the cycle.
- *No pull request for a cycle that did not pass.* Above.

### D-86 — Collisions are resolved at merge, one pull request at a time, by a person — the claim graph advises rather than schedules

**Governs:** §10.1, §10.5, §15, §18

**Decision.** Every cycle in a batch builds independently from the same base. The claim graph no longer
serialises groups or rebases one onto another; it predicts which pull requests will conflict and suggests
a merge order, in the sitrep. A person merges each pull request in turn, and resolves a conflict with
`resolve-conflict`, which is widened from its enumerated list to any conflict on a cycle's pull request,
**inside the cycle's declared files**, ending in fresh evidence and a fresh `verify`. Two of its rules
stay: the same setting with two values stops, and migrations are regenerated, never merged. Every open
pull request into `development` is re-checked when another merges (§13), and `development` itself builds
on every merge.

**This reverses a non-goal and a scope.** §18 listed *"resolving merge conflicts as the primary
strategy"*, and `resolve-conflict` was enumerated because *"a skill that resolved whatever happened to
conflict would quietly become the thing that merges real code."* That fear was an agent merging code
nobody checks afterwards. Here every resolution is rebuilt, re-verified in a fresh process (D-01) and
reviewed on its pull request before anything merges.

**Why build independently.** Serialising and rebasing made a batch a stack: rejecting one pull request
invalidated every one built on it, and a person reviewing the morning's work could not take them in any
order. Independent builds cost a conflict at merge, which is where a person is.

**Why it stays inside the declared files.** If fixing B after A's rename means editing a file B's plan
never named, that is a change of plan, not a merge. It stops and hands back, and the person reaches for
`steer`, a reopen or an abandonment.

**Rejected.**

- *Keeping the serialised schedule under the factory.* Above.
- *The factory resolving conflicts unattended.* Deferred, not rejected: what practice should show first
  is which conflicts recur and how often a resolution stays inside the declared files.
- *Re-checking a pull request only when it conflicts.* It misses the case that matters — two pull
  requests that merge cleanly and break together — until after both have landed.

### D-87 — The pull-request check reads the diff: a plan alone gets the admission check, anything else the full chain

**Governs:** §13

**Decision.** When a pull request changes only one cycle's planning documents — intent, spec, plan,
their findings rounds and the approval — `verify-chain` runs the **admission check**: every planning
document's shape is sound, the standing decision is `approved`, the three hashes match, and every round's
upstream matches. It builds nothing and asks for no verify. A pull request that changes anything else gets
the full chain, as before.

**Why the diff and not the branch name.** Whoever opens the pull request picks the branch name. One file
outside the planning documents, and the full chain runs and fails — so a plan's pull request cannot carry
code past the check.

**Why there is a check at all.** The first instinct was none, since there is nothing to build. But a
required check that never reports blocks the merge on GitHub, and the cost of a hash comparison is
seconds. The factory refuses an unapproved or edited plan on its own, so what this guards is code arriving
on a plan's pull request.

**Rejected.**

- *No check on a plan's pull request.* Above.
- *Choosing by branch name — `cycle/<id>/plan` gets the light check.* A label the author chooses.
- *Azure DevOps path filters excluding the planning documents from the policy.* Native, and correct on
  that platform; GitHub has no equivalent, and the rule would live in two platforms' settings rather
  than the binary.

### D-88 — Main is outside the guarantee, and a fix for a release is carried forward into `development`

**Governs:** §13, §18

**Decision.** The guarantee covers what reaches `development`. Promotion to main — by merge commit, from
a stable point, usually through `release/<version>` — is the team's process, and the framework has no
part in it beyond saying, in the adoption guide, that main's branch policy is theirs. A fix found on a
release is a cycle like any other, with the bug as its lead (D-89). Its intent may name a release branch;
the cycle then runs from that branch and back into it, built by hand, and **opens a second pull request,
from `cycle/<id>/forward`, carrying the same fix into `development`.**

**Why the framework stops at `development`.** Promotion is where a team's release practice lives — QA,
release branches, sign-off — and every team's is different. The framework's promise is about how a change
is admitted and verified; once that has happened on `development`, what is promoted and when is a
decision about the product, not the change.

**Why the forward pull request is opened by the cycle.** The next release is cut from `development`, so a
fix that reached only the release branch ships its bug again. A list of fixes awaiting a forward port is
a list people learn to stop reading. A clean cherry-pick is checked by build and tests, as a clean merge
is; one that conflicts goes through `resolve-conflict`, which ends in a fresh verify — because the record
it carries verified the release's code, not `development`'s.

**Rejected.**

- *Fixing on `development` and cherry-picking onto the release.* Workable, and it remains open to a team;
  it makes an urgent release wait for a full pass through `development`.
- *A separate "sub-intent" artifact for a bug.* A bug is a small intent; a second kind of document would
  need a second set of rules.
- *`init` writing a step for main's branch policy.* It would put the framework back into the step it has
  stepped out of.
- *Squashing `development` into main.* Main and `development` would stop sharing history, and every
  promotion would need a reset or a merge back.

### D-89 — A lead is a reference to something in a tracker, and Operate files work items rather than writing its own artifact

**Governs:** §3.4, §15

**Decision.** A lead is anything a person can hand `intent` to begin a cycle, named by a prefix spelled in
full: `azuredevops:1234`, `github:56`, `jira:PROJ-123`, `suggestion:<cycle>/<finding>`, or a path. Only
those prefixes are read as one; anything else is a path. The binary fetches and normalises a lead —
deterministic, so the binary's — and the skill interviews from that; the intent records the reference so
the cycle's pull request can link and close the item. Operate is a scheduled run that queries the
solution's telemetry store, groups errors by a signature, strips personal data, and files a work item for
each signature with no open one. The tracker and the store are declared in `solution.md`; credentials
never are.

**Why the tracker is the intake.** A lead file of Operate's own would have needed three answers the
tracker already has: review is triage on the board people already use, dismissal is closing the item,
and deduplication is the signature on open items. And Operate files nothing into the repository, so
nothing unreviewed lands on `development`.

**Why through `intent` and not a new skill.** `intent --from` seeds the interview and the person still
answers it. A lead-to-intent skill would skip the interview, which is the shape D-16 rejected.

**Why a work item is untrusted.** A public issue is written by anybody, and it is handed to an agent. The
binary marks it as data in what it prints, and the planning approval stands between it and any code.

**Rejected.**

- *A lead file committed to `development`.* Unreviewed commits on the shared branch, and dismissal would
  need a person-only file of its own.
- *A lead report under `.sdlc/reports/`.* Read back by nothing, so it cannot deduplicate across runs — kept
  only for a solution with no tracker.
- *Short prefixes — `ado:`, `gh:`.* Chosen against in favour of names a reader does not have to decode.
- *Operate as a skill.* Querying, grouping, deduplicating and filing are all deterministic, so by §15.2
  the run is the binary's.

### D-90 — A perspective is a method that steers authoring and judges in a fresh reader of its own

**Governs:** §6.5

**Decision.** A perspective — security, operability, maintainability, accessibility, UI/UX, infrastructure
and cost, data and migrations, API compatibility, performance — is a document embedded in the binary, the
way a stack profile is. It carries a **method** for examining the work, the **questions** it asks at each
stage from intent through verify, the **rules** it enforces, and the **evidence** it requires. Authoring
stages read a short *cover this* list per active perspective; `interrogate` runs one fresh process per
active perspective with its full method, then **one reconciling reader** over that round's findings;
`verify` asks each perspective's questions of the evidence. Security, operability and maintainability are
always on; the intent names any others, and the plan's declared files add what the intent missed. Only a
finding citing a rule blocks; what a method finds beyond the rules is a suggestion.

**When two perspectives' findings contradict**, neither is applied: both become suggestions, each with
its reasoning and a reference to the other. A rule beats a method finding. Two rules that cannot both be
met are a conflict, as D-64 already records one, and go to a person.

**Why a method and not a label.** A filter over the rule set makes nobody look harder: a rule says what
is forbidden, not how to find it. What a security reviewer brings is the method — trust boundaries traced,
every input followed to where it is used.

**Why it steers as well as judges.** A missing threat model found at verify costs a rebuild; written in at
plan it costs nothing. A perspective that only judges finds what it could have prevented.

**Why a reader per perspective.** One reader holding every rule spreads its attention, and a reader that
has found three security problems tends to stop before the missing empty state. The reconciling reader
comes after, so no reader is anchored by another's findings and order decides nothing.

**Why it never widens.** The first version said the spec was where perspectives add the most, and the
spec skill wrote each list in. In a trading solution's first cycle the no-new-scope rule (`CON_05`) then
blocked five criteria the lists had put there — keyboard use, an accessibility scan, announcements,
failure logging, the provider's key — because nothing in the intent asked for them. The rule was right:
a list that arrives with every cycle is not a person asking. So a list steers the criteria the intent
asked for, and what it would add instead reaches the intent through a person (D-97).

**Why the intent names them.** The spec is where perspectives shape the most, and declared files first
exist in the plan. An automatic step choosing them would decide unseen how much scrutiny a cycle gets; a line in
the intent is seen in the interview and covered by the approval's hash.

**Every perspective proves its cost.** It ships with fixtures — an artifact with a defect planted in it —
and the suite holds that its reader finds it.

**Rejected.**

- *A skill per perspective.* Nine skills differing in what they read, with methods moved into skill text.
- *One reader with every perspective.* Above.
- *Subagents.* A subagent is spawned by the session that wrote the artifact; the fresh process exists so
  the reader has no stake (D-01).
- *A perspective as a tag on rules.* The first proposal: it changes what a reader is shown, not how it
  looks.
- *Each reader seeing the earlier readers' findings and removing what conflicts.* Order would decide whose
  finding survives, later readers would be anchored, and the readers would run one at a time.
- *A new tier for perspective rules.* The three tiers and the declared stack are already every place a rule
  may live (§7.3).
- *Counting an active perspective's list as support for a criterion under `CON_05`.* It would end the
  contradiction by letting every list item into every spec unasked — removing the guard rather than
  routing round it, since nobody would ever have chosen those criteria.

### D-91 — One repository, one binary, two plugins: the sdlc plugin is the engine and the factory runs it unattended

**Governs:** §12, §16

**Decision.** The factory ships as a second plugin from this repository, on the same release, using the
same binary. Anything a person uses in a cycle — every stage, `intent --from`, `resolve-conflict`, the
sitrep read locally — belongs to the sdlc plugin and works with no factory. The factory adds no stage and
no rule: it adds what runs unattended or on a schedule — the pipeline templates, the headless runner,
Operate's scheduled run. It requires the sdlc plugin rather than copying it.

**Why one binary.** The factory depends on the core contracts — `verify-chain`, the claim graph, the
evidence format, the artifact schemas — and two binaries would put version skew on exactly those.

**Rejected.**

- *Two repositories.* The same skew, across a release boundary. Worth revisiting only if a different team
  owns the factory.
- *One plugin.* A solution that never runs unattended would carry pipeline templates and a scheduler it
  does not use.

### D-92 — The toolchain's raw evidence is a pipeline artifact; the summary, the base and the chain stay committed

**Governs:** §4.5

**Superseded in part: D-13**, which kept the original beside the summary in the cycle's evidence. That
stands locally, while a build runs; what changes is what is committed. `build.log` and `tests.trx` are
ignored by git and published by the pipeline — the trx to the platform's test tab, so Azure DevOps shows
the tests and the coverage. `tests.json`, `base.sha` and the chain stay committed, because the
pull-request check reads `tests.json` and verify's citations rest on it.

**Why.** Every cycle committed a build log nobody reads after the merge and a trx file the platform
displays better than a diff does. The record that matters to a later reader — which tests ran and what
they concluded — is the summary, and that stays with the code.

**What it costs.** A verify statement citing the build output points at a pipeline artifact, which the
platform keeps for a retention period rather than forever. `CON_08` is still met when the claim is made;
a reader long after may not be able to open what it cited.

**Rejected.**

- *Committing everything, as before.* Noise in every cycle's diff, kept forever, for a file nobody reopens.
- *Moving the summary out too.* `verify-chain` reads it, and `propose` and `suggestions` read the record.

### D-93 — A row is declined by answering it, a release names what was never asked, and adoption stamps its release

**Governs:** §8.2, §9

**Closes the gap D-28 left open** — *"the judgement half, proposing changes to constraints and the tech
stack, remains open"* — for the declared stack. Update mode refreshed what the framework owns; nothing
told a solution what a release had added to the profile or the stack rules, and nothing told a person
that a repository's framework files were a release behind the binary on their machine.

**Decision.** Three parts.

1. **A default is declined by writing `none`, never by deleting its row.** A rule was already declined
   by `false`. So *no row at all* means one thing: nobody was asked.
2. **What was never asked is named, and nothing writes it.** A default the default profile declares, or
   a stack rule this release ships, with no row in the declared stack is listed by `sdlc stack`, named
   on `doctor`'s `declared stack` check — sound, because it is information — named under the drafter's
   rename command, and named at the end of an update's report. The `init` skill, on an update, reads the
   repository and proposes an answer for each; the person decides, and the answers go into a draft they
   rename. The draft stays a byte copy.
3. **`.sdlc/README.md` carries the release that last wrote the framework's files**, and `doctor`'s
   `framework files` check compares it with the binary: older or missing is a problem a person fixes by
   running `init`; newer is a problem fixed by upgrading the binary, since re-running the older one would
   roll the files back.

**Why `none` rather than an "added in" version on every row.** A version history would let a tool say
what is new since a solution last looked, at the cost of release metadata on every row and rule and a
version recorded in a person's file — which adoption cannot write after creating it, so it would freeze
at adoption while reading as current. Declining by answering needs neither: absence is exact. It also
records the decision. *We use no ORM* was invisible when the row was deleted; now it is a row a plan
reaching for one departs from.

**Why against the default profile only.** It is the one profile a draft is written from (D-76). Every
other profile's rows are in the options menu to copy across; a .NET solution asked about experiment
tracking at every release would learn to stop reading the list.

**Why the stamp lives in `.sdlc/README.md`.** Adoption rewrites every file it owns in one run, so one
stamp speaks for all of them; that file is the framework's outright and is markdown. A hook configuration
is a host's JSON with a schema of its own, and the pipeline already pins the version it installs.

**What it costs.** A stack placed before this release was told to delete what was not true, so the first
comparison names every row it deleted — a deleted row and an unasked one look the same. Each is answered
once with `none`, and the list is quiet after that. The `init` skill says so before showing the list.

**Rejected.**

- *Reporting new rules only, and leaving new rows to the options menu.* No convention changes and nothing
  is ever noisy — and it misses most of what a release adds, which is rows. The axe row in 0.23.0, the
  case that raised this, would never have reached a solution adopted before it.
- *The drafter adding unmentioned rows to the draft.* The draft is a byte copy so the rename applies only
  what somebody changed (D-67); a tool filling in rows makes the judgement while looking like bookkeeping.
  The skill proposes and a person answers instead.
- *Migrations with up and down steps, as EF Core has.* EF owns the data it migrates; here a migration
  could write only the framework's files, which update mode already regenerates whole, and could only
  print a draft for a person's. A cycle's artifacts are signed — an approval hashes the planning
  documents — so rewriting them into a new format would invalidate every approval; a format change is
  read by the binary instead, which is what `framework_version` is for. And *down* is reinstalling the
  older binary and running `init`, so a down step is compatibility code D-81 already declined to carry.
  Revisit when a release changes the *shape* of a person's file — and then as a drafter and a `doctor`
  check, not a numbered chain.
- *A version in the solution document, or in the declared stack.* Adoption creates the solution document
  once and never rewrites it, and a version in the stack is a field a person must remember to move. The
  stack needs no version to say what is new.
- *A migration skill of its own.* The judgement — does this row apply here — is the one the `init` skill
  already makes when it edits a first draft from the repository. A second skill would make it twice.
- *`init` interviewing again on an update.* Update mode is valuable because it is mechanical and
  repeatable; the walk is the skill's, after the command, not the command's.

### D-94 — Adoption drafts all three files a person places, commits, and creates the integration branch

**Governs:** §5.3, §8.2, §9, §15.2, §16

**Decision.** Five parts, from one walked adoption.

1. **`init` places none of the files only a person writes; it drafts three.** `solution.draft.md`,
   `techstack.draft.md` and `constraints.draft.md`, beside where each goes, never over a draft already
   there, and ignored by git. The constraints draft has its header and no rule.
2. **Each carries a note at its top saying only a person changes the file**, and why an agent's write is
   denied — and `docs/mechanisms/protected-files.md` says how the guard works, once.
3. **The report and `doctor` name every draft left, its rename and the commit**, not one command.
4. **A placed solution document still carrying the prompt is a `doctor` problem**, and the check knows
   the drafter's prompt as well as the old placeholder.
5. **After its commit, `init` creates the integration branch and switches to it** when it exists neither
   locally nor on the remote — `development`, or `--integration-branch <name>`, which is refused when the
   document records another. `doctor` fails while it is missing. And **`sdlc new` refuses while anything
   is uncommitted**, naming the files; the intent skill asks the person what to do with them.

**Recovering a draft is running `init` again.** The drafts are ignored, so a clone taken before anything
was placed has none of them and nothing recording the origin. `init` drafts again whatever is neither
placed nor drafted, and decides the origin as a first run would — with the files adoption itself commits
not counted as code, so its own hooks cannot reclassify a new solution as adopted. `.sdlc/README.md`
says so in the solution.

**Why: the walk.** Adoption wrote `solution.md` with one placeholder, then named `sdlc draft solution`,
whose draft carried a *different* one — the first said nothing reads the prose, the second that every
plan does — and the person was told to rename the second over the first. The rename changed nothing but
which placeholder was there, and the check knew only the first, so `doctor` said *nothing is left to
place* over a paragraph that said nothing. Meanwhile no `development` existed, so the cycle's plan branch
started from `master`, and the placed stack and paragraph — uncommitted — rode onto it as the first
cycle's work. And the guide had the person run `sdlc branch` after the skill already had.

**Why drafts and not the file, when D-62 rejected adoption writing it.** It did write it, despite that
entry: create-but-never-modify on the one file adoption observes. Drafting it is what D-62's rejection
actually asked for, and the keys adoption observed are still recorded — in the draft, which a second
`init` reads as recorded until the file is placed.

**Why fail on the placeholder, when D-79 rejected it.** D-79's reason was that nothing read the prose, so
failing every fresh repository on it would make `doctor` unusable in a script. Both halves moved: the
plan reads the paragraph for what good looks like, and every fresh repository now fails `doctor` anyway
until two files are placed — so the exit code is accurate rather than noisy.

**Why the refusal over uncommitted work is in the binary.** A file listing decides it, which is the
binary's by the rule every command follows, and a person opening a cycle by hand gets the same answer as
the skill. What to do with the files is the person's; the refusal decides nothing about them.

**Rejected.**

- *A `sdlc place` command that renames the drafts.* The rename is the decision, and a command that made
  it would be the exception D-09 refuses to create.
- *Committing the drafts.* A draft is a proposal; committed, placing it reads as a delete and a create,
  and a draft left behind would reach a later cycle's diff.
- *Requiring `constraints.md`.* A solution with no rules of its own is sound, and a step that is usually
  skipped teaches people to skip steps (D-79).
- *The integration branch as a row in the declared stack.* The declared stack is what a plan is judged
  against, and a branch name gives `CON_15` nothing to judge. It decides where governed code lands, which
  is the solution document's (D-83).
- *Removing `sdlc branch` and having the intent skill run git itself.* `build`, `batch-build` and the
  forward branch need the same base calculation, and the binary is where it lives once. The person's path
  no longer names it; the skills do.
- *The intent skill stashing uncommitted work on the person's behalf.* Work moved out of sight is work
  somebody has to find.

### D-95 — The intent interview settles the type first and asks by type, one question at a time, with examples

**Governs:** §3.1

**Decision.** Four parts.

1. **The type comes before the first question.** `/sdlc:intent --type bug|add|change` names it; a lead may
   carry it; a new solution has no flag and is detected — `origin: scaffolded` and no cycle yet approved.
   Otherwise the opening answer decides it and the skill confirms in a line. A flag wins over detection,
   and a flag the answers contradict is asked about rather than followed.
2. **The interview opens with one question** — *what is this cycle for?*, or for a new solution *what
   should someone be able to do first?* — **then asks a short set for the type, one question per turn**:
   a bug's reproduction, a feature's person and job, a change's dependants, a new solution's users and
   bounds; then what must keep working, what is decided, and what it touches.
3. **Every question carries example answers**, all tailored from the solution's paragraph and what the
   person has said, pointing different ways and carrying no figure — **and *not sure* is an answer**,
   recorded by what it is about: an unmeasured figure as unconfirmed, an unanswerable question under
   `Open`, something nobody has found out nowhere.
4. **The skill takes no sentence.** Text typed after its name is read as the answer to the opening.

**The sections do not change.** `## Problem` opens with who has it, and for a bug fix follows the
reproduction's order, but the set of sections is the same for every type.

**Why.** The interview asked the intent's section headings as questions — *what happens today that
shouldn't, what must not change* — so it read as a form and got abstract answers, and a person starting
work honestly could not answer most of it. The type shapes the whole conversation, yet was settled last.
Asking by type, one question at a time, with examples, is the refinement conversation an agile team
already has; the intent's sections are the story it produces — who, under `Problem`; what they want, under
`Wanted outcomes`; why, under `Problem`; and the type says whether it is a bug, an addition or a change.

**Rejected.**

- *The "As a… I want… so that…" template as the intent's shape.* Not because "I want" is a solution —
  wanting is the problem space — but because the intent already holds every part of a story, and a
  one-line story on top would be a second place to say the same thing that fits a bug fix or a new
  solution poorly. An answer that names a mechanism is caught by the interview's probe, *what does not
  having that cause?*, whatever the format.
- *Generic examples beside the tailored ones, to loosen the tailored one's anchor.* The first version.
  In a trading solution's first cycle the person was offered a die, a sales chart, paper, no
  multiplayer, a six-sided die and one button, picked none of them, and found them no help — while the
  tailored *"watch it move minute by minute"* still reached the intent as an unattributed granularity
  and became its only blocker. The generic ones did not loosen the anchor; tailored examples that point
  different ways and carry no figure do the same work without the noise.
- *A sentence argument.* It skipped the guided opening and was where a solution got typed. `--from`
  covers starting from text somebody already wrote.
- *`--type new`.* A new solution is a fact about the repository, which the skill can read; in an adopted
  one it could not be true.
- *Detecting a new solution from "no intent yet".* An adopted solution's first cycle has none either.
- *Offering a new solution's first cycles as choices.* It is the one place the skill would draft rather
  than elicit; the opening question with examples does the same work without putting the agent's idea of
  the first cycle in front of the person's.
- *Sections per type, or a section for who.* The sections are what the schema checks, the spec derives
  from and the approval hashes, and they are the same for every type — which is what lets the questions
  vary. Specs or interrogations that keep failing to *find* something would be the evidence for a section.

### D-96 — The factory's agent is an image built from one commit, and the first thing the factory bundle ships

**Governs:** §10.5

**Decision.** `factory/` holds a Dockerfile that builds an Azure Pipelines agent carrying `sdlc`, Claude
Code and the `sdlc` plugin, with the binary packed and the plugin copied from the commit being built. It
runs on a Linux host as a systemd service, one fresh container per job, registering into a pool of its
own with a token scoped to agent pools. It is the headless runner D-91 named, and the first piece of the
factory bundle; the bundle's manifest, the pipeline templates and Operate's scheduled run stay where they
are until there is a second template to ship.

**Why from one commit.** D-91 kept one binary so the factory and the engine could not drift, and an agent
assembled by hand reopens exactly that gap: the binary from one install, the plugin from another. Built
together they cannot disagree, and the image carries no version number of its own to go stale.

**Why a fresh container per job.** A factory run ends in branches and pull requests and nothing else
(D-84), so whatever a job leaves on the agent is debris the next one may read. Exiting after one job
makes "nothing carries over" a property of the agent rather than a cleanup step.

**Why a machine of its own.** The factory runs the agent with its permission prompts off, and a solution's
integration tests need the host's Docker, which is control of that host. On a person's own computer, or
under their account, a run could reach every repository and credential they have, and would build with
whatever their working copy of the plugin held that night.

**Rejected.**

- *Installing the tools on an agent by hand.* The two halves drift, and nothing says so until a run
  reinstalls from a feed that may not exist.
- *An image written into each solution.* A copy per solution drifts from the binary it has to match, and
  a solution does not own the factory's agent.
- *A container that restarts in place.* Docker's restart policy keeps the container's filesystem, so a job
  would inherit the last one's work directory.
- *Docker Desktop as the host.* It runs only while somebody is signed in, and sits beside that person's
  own credentials.
- *A registry before a second machine needs one.* The image is built where it runs; a registry is an
  account, a credential and a push step for one host.
- *The whole second plugin now.* A manifest and a moved command with one file behind them; it comes with
  the second template.

### D-97 — A person takes a raised suggestion into the intent through `steer`, by its id

**Governs:** §3.2, §6.5

**Decision.** A spec's criteria derive only from the intent (`CON_05`), and that rule does not loosen.
What a perspective finds beyond the intent reaches it one way: **a person names the suggestion's id to
`steer`**, and decides it.

| The person | Written into the intent | What the next round does |
|---|---|---|
| takes it in | `Wanted outcomes` gains it, in their words, with their name, the date and the finding's id | its reader finds the outcome stated, and the round records the finding `answered` |
| declines it | `Fixed decisions` gains *not in this cycle*, with their name, the date, the finding's id and why | its reader finds it decided, and the round records it `answered` |
| leaves it | nothing | it stays open, and stays in `sdlc suggestions` for a later cycle |

**Several in one run**, because each run re-interrogates the chain from the intent down to where the
cycle has got to; one at a time would pay that once per suggestion. With only an intent, `steer` takes
finding ids alone. The intent, spec and plan hand-backs each ask once, grouped by perspective, which to
take in, decline or leave, and skip the question when nothing is open. Only an open, non-blocking
finding of this cycle's latest rounds qualifies: a blocker is `refine`'s, and another cycle's suggestion
is a lead for a cycle of its own (`--from suggestion:`).

**Why.** In a trading solution's first cycle the spec skill wrote each perspective's list in, and
`CON_05` blocked five criteria — keyboard use, an accessibility scan, announcements, failure logging and
the provider's key — because nothing in the intent asked for them. The intent's own rounds had raised the
same concerns as twenty suggestions, and nothing asked the person to take them or leave them, so the
author used them anyway. The agent then recommended `steer` for new outcomes, which `steer` refused; the
spec skill sent a missing outcome there all the same. The rule was right and the routes around it were
wrong. A person choosing from what a fresh reader found is neither an agent widening scope nor a new
piece of work arriving midway, and it is the path that keeps `CON_05` whole.

**Why the intent and not the spec.** The spec alone defines *done*, and it derives from the intent; an
obligation written straight into the spec would be one the approval covers but nothing upstream asked
for. Written into the intent it is attributed, hashed by the approval, and every stage below it derives
from it as from anything else.

**Why no new disposition.** A disposition moves only on a fresh reader's silence (D-49). Writing the
person's verdict into the intent lets the next round record `answered` the ordinary way — and a reader
that raises the finding again is saying the wording did not settle it.

**The trust layer** is the one every steer rests on: nothing mechanical stops an agent writing a
fabricated attribution, and what catches it is a person reading the intent at the approval gate before
any code exists.

**Standing properties are not for this.** A suggestion taken in cycle after cycle — every screen usable
by keyboard — is a rule for the solution's own constraints, which `sdlc propose constraints` counts.

**Rejected.**

- *Counting an active perspective's list as support under `CON_05`.* Ends the contradiction by letting
  every list item into every spec unasked (D-90).
- *`adopted` and `declined` dispositions a person sets.* The first proposal. It would be the only
  disposition not moved by a reader, breaking D-49, and the intent already has the places to say both.
- *A decide mode on the `suggestions` skill.* It would turn a read-only listing into an editor of the
  intent and copy the chain-carrying `steer` already does.
- *A new skill for it.* The same edit and the same re-interrogation as `steer`, under a second name.
- *`steer` taking any new outcome.* An outcome nobody raised has had no reader's scrutiny and no
  interview; that is a new cycle.
- *Writing plan-stage suggestions into the plan as unapproved extra proof.* A plan proves the spec;
  what it would add goes under `Risk`, and its reader raises it for the person to decide.

### D-98 — The AppHost condition binds as `PRO_04`, and the scaffold carries the AppHost's rows from the start

**Governs:** §11.1, §11.3.1

**Decision.** Three parts.

1. **`PRO_04` on `dotnet-default`:** a plan whose work runs more than one process starts them from an
   Aspire AppHost. A process is a host project, or a backing service a developer would otherwise start by
   hand. Reaching one process, for instance by serving the page from the api, meets the rule as fully as
   an AppHost does.
2. **The scaffolded `StructureTests` carries an `AppHost` row in both tables**: it may reference the
   hosts it starts, `Api` and `Web`, and take `Aspire.Hosting` packages by prefix. The row waits for its
   project the way the `Ui` and `Web` rows do.
3. **The scaffolded `ConventionTests` fails when a project under `src/` is neither read nor named as
   exempt.** `ServiceDefaults` joins the assemblies it reads; `Contracts` and `AppHost` are exempt by name,
   each with its reason.

**Why.** The first solution to meet the condition, a die rolled from a standalone WebAssembly page and an
api, departed from the orchestration default with a reason `CON_15` accepted. The reason was cost: an
AppHost had no row in the architecture suite, so adding one was a change to the rules the cycle was
judged by. It shipped two processes started by two commands and a README still asking somebody to say
how to start the application. The traces `AddServiceDefaults` registers were exported nowhere, because
nothing set the endpoint an AppHost sets for every process it starts. A default is the right mechanism
when the cost of departing falls on the cycle that argues it. This cost fell on everybody who ran the
solution afterwards, so the condition binds. The rows remove the cost the departure rested on, which is
what keeps the rule from charging every two-process cycle a rule change. The convention guard came from
the same solution: its component library was outside every convention rule, two of its fields broke the
naming rule, and the suite was green.

**Measured rather than reasoned.** An AppHost was added to a copy of that solution with Aspire 13.4.6.
It built once a patched MessagePack was pinned, because the hosting package took a version NuGet's audit
flags and warnings are errors. It started both processes from one command and showed both on its
dashboard. With the two rows in place, every test passed with no other table edit. **It did not hand the
page the api's address.** A standalone WebAssembly page reads its configuration in the browser, where an
environment variable does not reach. The integration that does is `Aspire.Hosting.Blazor`, which is
prerelease and carries a diagnostic the build would refuse. So the rule's `Why` claims one command, the
dashboard and the exporter, and not service discovery.

**Rejected.**

- *A `LOC_` rule in each solution.* Every solution with two processes would rediscover the same rule
  after paying for its absence once.
- *Scaffolding an AppHost always.* D-55 and F-24 stand: a single-process solution would get a host it
  does not need, and `Scaffold.Files` would become conditional.
- *Leaving it a default.* The first solution to meet the condition is the outcome.
- *D-55's rejection of "a `PRO_nn` on `dotnet-default`", read as covering this.* That was a rule about
  an AppHost's container labels, which has nothing to judge until an AppHost exists. `PRO_04` judges the
  plan, which always exists.
- *Taking `Aspire.Hosting.Blazor` so the page gets the address.* A prerelease package with an unstable
  API, in every solution with a page, to remove one line of configuration.
- *Loading every `src/` assembly in `ConventionTests` automatically.* It needs every project referenced
  from the test project anyway. And it would turn the AppHost's generated `Projects` types and every
  Razor component's unsealed generated half into failures nobody chose. A guard that asks is the same
  shape `Every_project_under_src_is_named_in_both_rules` already has.

### D-99 — Operability earns `CON_20` on two cycles, and two perspectives' methods name what their readers kept finding

**Governs:** §6.5, §7.5

**Decision.** `CON_20` joins the global set: a spec says what is recorded when the action fails. The
operability perspective lists it as the rule its reader may block on. The accessibility perspective's
*Cover this*, *Questions* and *Evidence* name what an automated scan cannot decide: a visible focus
indicator, reflow at 320 CSS pixels, 3:1 contrast for a graphic that carries meaning, forced colours,
and focus on the failure path. The security perspective's name a control proven by what it refuses, and
disclosure checked where an outsider reads it rather than in a component's markup.

**Why.** Two trial solutions left 251 open suggestions between them. Grouped by the objection rather than
by finding, the same few came back at three or four stages of each cycle. **Operability's was the
strongest**: a failure that leaves nothing behind for whoever investigates it. The perspective's method
already asked for a criterion and a test that reads the record, the author built the gap anyway, and the
reader could only suggest. That is what a rule is for. `CON_17` covers the person told and says nothing
about the record, so this is a new rule beside it. It is not a revision, because a revision may not change
what is forbidden. **Accessibility's and security's were not missing rules**: the authors were never told
to cover them. A perspective's *Cover this* is what an authoring stage reads, and it named the scan and
keyboard focus and nothing the scan misses, so the reader found the rest every round.

**Departing from the three-cycle threshold, stated.** How a perspective earns rules says the same
objection across three cycles. This is two, and both built the same die, so part of the recurrence is
the same problem solved twice. It was adopted on two because the method had already asked and been
ignored. That is a different failure from a reader with an opinion, and a third cycle would only measure
the cost of waiting. The method changes need no threshold: they are guidance for authors, not rules.

**Rejected.**

- *`CON_17.2`.* A revision cannot change what is forbidden (§7.4).
- *Superseding `CON_17` with one rule covering both audiences.* Every blocking finding cites one rule. A
  finding about the player's message and one about the record are different remedies, and one rule
  covering both would make every citation ambiguous about which half failed.
- *A `LOC_` rule in each solution*, as How a perspective earns rules says for a solution's own pattern.
  Both solutions met it, and nothing about the objection depends on either one.
- *A `PRO_` rule for what the scan cannot see, now.* It would have to name the scan engine, which makes it
  stack-tier, and the method change is untested. A third cycle that still misses them is the evidence for
  one.
- *A rule that a control's proof includes a refusal.* It may already be `CON_06`'s question, a proof
  adequate to its criterion. The method change asks it first.

### D-100 — The panel names a stage the way the host it is printed in invokes one

**Governs:** §14

**Decision.** The Next line prints a stage — a skill — in the host's own form when the binary can tell
which host it runs in, and the host-neutral `sdlc <stage>` when it cannot. Each host's row carries both
facts: how a stage is invoked, and the environment variable the host sets in its shells. Claude Code has
both (`/sdlc:{0}`, `CLAUDECODE`); Copilot and Antigravity have neither, because a stage there is asked for
in a sentence and has nothing to type. The binary's own commands — `verify-chain`, `status`,
`draft approval`, `suggestions` — print as commands on every host.

**Why.** cycle-02 ended on `Next  sdlc pr <id>`, and typing it was answered with "'pr' is a skill" and the
`/sdlc:pr` the panel could have printed. That is one step for nothing at the end of every lap, on the line
§14 calls the most-read output in the plugin. The neutral form was deliberate, and it is kept wherever it
is the only honest one.

**Why the environment and not a flag.** The call that needs it is the one every stage skill makes when it
hands back. A `--host` flag is one more argument for every skill on every host to remember, and the one
that forgets prints the wrong form while nothing fails. The marker is set in every shell Claude Code
starts, the factory's headless runs included, and in no person's own terminal, where the neutral form and
its refusal still apply.

**Rejected.**

- *`/sdlc:<stage>` always.* Wrong on two of the three hosts, and in a person's own terminal it names
  something they cannot type there.
- *Keeping `sdlc <stage>` everywhere.* Correct and host-neutral, and costs a refusal each time.
- *Guessing a marker for Copilot or Antigravity.* Neither documents one; a guessed marker is a panel that
  names one host's invocation to someone using another.

### D-101 — The binary writes every findings round, and refuses one out of the chain's order

**Governs:** §6, §6.2, §12, §15.2

**Decision.** A round is written by `sdlc round <id> <artifact> --from <file>`. The file holds the
readers' findings and the session's dispositions (D-49 is unchanged: the fresh reader judges, the session
holding the history records the transitions). The binary owns the envelope: the round number, the time,
`upstream_sha256`, a new `subject_sha256` for the document judged, `section_sha256` per second-level
section, and on a verify round `evidence_sha256` for the run it read. It refuses a round while any
document above the artifact is unsettled — no round, a latest round that judged a different version of
it or judged it against an upstream that has since moved, or a blocker still open. The hook denies every
agent write to a round file, by path and by shell command. The approval gate adds the subject to its
staleness check, and `verify-chain` checks the verify round read the evidence recorded now. A round may
carry a `note`, which a steer uses to say what moved above the document and what the document did about
it. `sdlc round --changed` lists the sections moved since the last round, and perspective readers raise
suggestions about those sections alone after the first round.

**Why.** MarketSense's first cycle ran its readers from a harness of its own and wrote the rounds itself,
dispositions included. During its first steer it edited the intent, the spec and the plan in one pass and
interrogated all three at once: intent, spec and plan rounds carry the same second, and plan rounds 7 to
13 each judged a different spec, none of them the one later approved. The person caught it by asking
why the three ran together. Nothing refused any of it, because the only thing writing the round was the
thing the round checks, and the gate looks at staleness once, at approval, through the latest round's
upstream alone. Seven rounds of work were spent on documents that stopped existing.

**Why drafting ahead stays allowed.** An author may edit the plan while the spec is being judged. What
must not happen is a *round* about the plan before the spec is settled, and once rounds are bound to the
hash of what they judged, a premature edit costs only the round that has to be repeated. Refusing the
edit would add friction without adding a guarantee.

**Why a round written before this has nothing to compare.** Every cycle in flight when this ships has
rounds without a subject hash. Counting a missing subject as stale would refuse every one of them at
approval until each document took another round, for no defect of their own. The upstream rule is
unchanged, so those rounds are judged exactly as before.

**Why sections are hashed.** Perspective readers raised about thirty suggestions a round whether or not
the document had moved — one plan reached 797 open, and none was ever read. A suggestion about text that
did not change is one the previous round already had the chance to raise.

**Rejected.**

- *Leaving the round to the session and the order to the skill.* That is what was in force, and the
  skill's instruction to re-interrogate downstream said nothing about one at a time. A rule an agent
  breaks to save time, with nothing to refuse it, is advice.
- *Catching it at the gate alone.* The gate did catch the stale rounds, eventually — after seven of them
  had been written, argued over and refined against. A refusal at write time costs one command; one at
  approval costs every round since.
- *Refusing an edit to a downstream document while an upstream one is unsettled.* See above: the round
  is what matters, and the hash makes a premature edit cheap.
- *Signing a round.* The binary has no secret the agent running it cannot read, so a signature would
  prove the binary ran on a machine the agent controls, which is nothing. The hook and the pipeline's
  checks are the layers that exist.
- *Treating a missing subject hash as stale.* See above.
