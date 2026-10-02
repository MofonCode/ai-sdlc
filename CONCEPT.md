# ai-sdlc — concept

**Iteration 4.** Rewritten 2026-10-02 from a reconciliation of the previous draft against the
implementation, which closed twenty-four contradictions. That working-through is commit `adf15cc`
and is not carried forward as a file.

The original longhand draft is preserved unedited in [`CONCEPT.raw.md`](CONCEPT.raw.md).

**How to read this.** Sections 1 to 18 state what is true. Section 20 says why, one decision at a
time, with what was rejected. No section repeats a reason, and no decision repeats a design — one
fact, one owner. A statement in the design carries a `(D-nn)` marker where a decision governs it.

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
| Local deterministic | the `sdlc` gate binary, a pre-tool hook, git hooks | a human with a shell | fast feedback |
| Non-bypassable | `sdlc verify-chain` as a pull-request check, plus branch policy | nobody without repo admin | the guarantee |

The promise made to a team is the third layer: **nothing merges unless the artifact chain is
complete, approved, and hash-matched.**

A rule of its own, carried from iteration 3: **a directory that appears enforced and is not is
worse than one that never pretended.** A check that silently does nothing — a CI job with no
credentials, a hook that fails to start — is the failure mode to design against.

### 2.2 The gate is a program, not a prompt

One executable owns every process constraint and exits non-zero with the warning text. Every skill
on every host does nothing but call it. The same binary runs in the pre-tool hook, in git hooks, and
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
can cause an agent to adjust the spec, the criteria become whatever the code happens to do. It is
enforced by the hook rather than by instruction (§6.3).

### 2.5 Side research and evaluation run in a new process

The main window keeps only what it needs to decide. Interrogation, fresh readers, and any
go-and-find-out step run in their own process (D-01).

### 2.6 Flexibility comes from making compliance cheap

Not from making constraints optional. Where the correct path is expensive, the answer is a tool that
makes it affordable — never a shortcut around it (D-17).

### 2.7 The plugin does not govern itself

ai-sdlc is not an ai-sdlc solution. If it were, a change to a constraint would be judged by the
constraint it changes, and a defect that blocks landing would also block the fix for that defect.
The process is exercised by running cycles in `dice-trial`.

---

## 3. The cycle

```
intent -> spec -> plan -> build -> verify -> land
                            ^         |
                            +- remediate (<= 3) -+
```

| Phase | Answers | Produces |
|---|---|---|
| `intent` | why this change exists | `intent.md` — the problem, not a solution |
| `spec` | what done means | `spec.md` — acceptance criteria, each testable |
| `plan` | how | `plan.md` — the approach, the files, the proof per criterion |
| `build` | the change itself | a diff, plus build and test evidence |
| `verify` | whether the change did the spec justice | `verify.md` — an outcome and its findings |
| `land` | integration | a pull request |

Two loops run inside the phases:

```
interrogate -> refine -> interrogate         an artifact, uncapped
verify      -> remediate -> verify           the code, at most 3 passes
```

Past three remediation passes the outcome is `escalated` and landing refuses it. **The artifact loop
is never capped** — on the intent that ran eleven rounds, round seven found that the headline change
was unreachable and round eleven found that no success criterion failed if it were skipped entirely.

Each stage skill runs the artifact loop itself: it writes, interrogates, refines, and interrogates
again until the round is clean or nothing further can be settled, then reports and hands back
(D-29).

A stage will not run until the artifact before it has an interrogation round with no open blocking
findings (§5.4).

### 3.1 Every class of change runs the full chain

`intent` interviews the user and establishes whether this is a new solution, a feature addition, a
feature change, or a bug fix. **Intent, spec and plan are all required, always.** The classification
informs how each artifact is written — a bug fix's spec derives its criteria from a reproduction
rather than a desired outcome — and never skips one.

**Splitting work into deliverable chunks belongs to whoever is prompting** (D-25). They write
smaller intents and run each through the cycle; no tooling accommodates splitting.

### 3.2 Steering a cycle that is already underway

`steer <id> "comment"` interviews, records the decision in the intent's `Fixed decisions` with
attribution, and carries the change down the chain — updating and re-interrogating the spec, and the
plan when one exists. It requires an intent and a spec, edits rather than regenerates, reports what
changed, and refuses on an approved cycle until a reopen is recorded (D-17).

Steering changes *how*, not *what*. A comment that changes the problem is a different intent.

---

## 4. Artifacts

### 4.1 Every artifact is an envelope carrying a body

| Part | Holds | Same across artifacts | Governed by |
|---|---|---|---|
| **envelope** | `framework_version`, `cycle`, and the `## Open` region | yes, identically | the envelope schema; `CON_14` governs `Open` |
| **body** | the artifact-specific sections | no, deliberately | a body schema per kind; the constraints bind this |

```
framework_version: 1.0.0
cycle: 20260929_1456_32

## Open
```

**The envelope is an allowlist, not a region.** Two frontmatter keys and one section. Everything else
in the file — any other key, any other section — is body, and a key or section nothing declared is a
violation rather than a tolerated extra (D-31).

**The test for where a field goes:** same on every artifact, with the same meaning and the same
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
| | `Wanted outcomes` | `CON_04` — this is its left-hand side |
| | `Fixed decisions` | `CON_01` routes decisions-already-taken here; `plan` cites them; `steer` writes here |
| | `Must not change` | `CON_04`, which names it |
| | `type` | the change classification; later artifacts read it |
| `spec` | `Acceptance criteria` | `CON_03`, `CON_04`, `CON_05`, `CON_06`, `CON_12`, and `verify` |
| | `Out of scope` | `CON_05`; a disposal destination (§5.4) |
| `plan` | `Shape and departures` | `CON_13` |
| | `Files` | the blast-radius signal and the claim graph; ordering reasoning lives here |
| | `Proof` | `CON_06`, and `verify`'s criterion table |
| | `Risk` | the person at the gate |
| | `tier` | the blast radius: `local`, `contract`, `data`, chosen by reversibility |
| `verify` | `Criteria` | the criterion-by-criterion adjudication; `CON_08` binds every claim in it |
| | `outcome` | `pass`, `blocked` or `escalated`; `land` and `verify-chain` read it |

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

### 4.4 The plan holds two kinds of claim

| Section | Kind | Judged by |
|---|---|---|
| `Shape and departures` | a **decision** — which approach | `CON_06`, `CON_13` |
| `Files` | an **estimate** — which files, how large | nothing; it is a signal |

A plan holds a file table, never diffs. Line-level change is the build artifact — the branch and
`diff.patch`.

### 4.5 Evidence

`build` records what happened as files rather than prose: the diff, the build log, a test summary,
and the base commit. `verify` reads those and nothing else, so its verdict cites test names and diff
lines rather than recollection. Where the evidence does not reach, `verify` says so.

**Evidence is normalised at the boundary** (D-13). `build` writes both what the toolchain produced
and one framework-defined summary — test name and outcome — and `verify` reads only the summary.

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

`solution.md` and the constraint set are approved by being hand-edited; neither is part of a cycle
gate.

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
    findings_rounds:
      intent.md: 2
      spec.md:   2
      plan.md:   2
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

Four files only a person writes:

| File | What is drafted |
|---|---|
| `approval.md` | a gate decision — three hashes, timestamp, findings rounds |
| `abandoned.md` | why the cycle stopped |
| `constraints.md` | a proposed `LOC_nn` |
| `solution.md` | the `profile` line, on migration |

One verb drafts them all: `sdlc draft approval <id>`, `draft abandon <id>`, `draft constraint`,
`draft profile <name>`. `draft approval` also takes `--deny` and `--changes-requested` (D-11). Each
prints a ready-made entry; the person reads it, decides, and places it.

**The pre-tool hook denies every agent write to all four, with no exception.** There is no approve
command for an exception to be carved out for, so every hook rule stays absolute (D-09).

Two costs of the paste flow: a pasted hash cannot be checked by eye, so `status` shows the first
twelve characters of each recorded hash beside each document's current twelve and a refusal names
*which* document changed; and a first entry is a file creation while a revisit is an edit, which the
printed instructions say.

`draft approval` refuses while any blocking finding is open.

### 5.4 What holds the planning stages

**A clean interrogation round is required to advance.** `spec` will not run until `intent.md` has a
round with no open blocking findings, and `plan` likewise on `spec.md` (D-02).

Three mechanical checks need no reading comprehension:

1. **every intent open item is disposed of by the spec** — as a criterion, under `Out of scope` with
   a reason, or under `Open` citing the intent item (D-05)
2. every spec criterion maps to at least one file and one named test in the plan
3. every changed file is declared in the plan, and every declared file changed — recorded as a
   signal, not a gate (§7.6)

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
      "cites": "CON_04",
      "statement": "...what is wrong, in full",
      "locus": "...where in the artifact",
      "remedy": "...what would fix it"
    }
  ]
}
```

An empty `findings` array is a clean round.

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

**How many rounds is the owner's call, and there is no cap.** The panel reports what each round found
and the cost of a round, and attaches no recommendation about continuing.

### 6.2 A round records the upstream it judged against

`upstream_sha256` is the hash of the artifact above, as the round saw it. A round is clean **as of**
that upstream, so changing an upstream makes a downstream round stale, the stage gate fails, and
re-interrogation is required (D-21).

### 6.3 `refine` and `remediate` — two editors with opposite guards

| | `refine <id> <artifact>` | `remediate <id>` |
|---|---|---|
| Runs after | `interrogate` | a blocked `verify` |
| May edit | the one named artifact | source and tests |
| May never edit | source, tests, `approval.md` | `intent.md`, `spec.md`, `plan.md`, `approval.md` |
| Capped | no | three passes, then `escalated` |
| Also refuses | once the cycle is approved | — |

Both guards are hook rules on file paths, not instructions in a prompt. They are two skills rather
than one with a mode, because their guards are opposites.

What makes them skills rather than freehand edits is the bookkeeping — dispositions move to
`answered` and what changed is recorded — plus a bound on how far an edit may reach, a pass counter,
and identical behaviour on every host. A person editing an artifact by hand needs neither.

### 6.4 The loop's three exits

Each stage skill loops until a round is clean or nothing further can be settled. Every finding has
exactly three exits:

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

**Stack neutrality.** No constraint may name a language, framework or package.

### 7.3 Tiers and resolution

| Tier | Id | Lives in |
|---|---|---|
| global | `CON_nn` | the plugin's library |
| profile | `PRO_nn` | the stack profile package |
| solution | `LOC_nn` | `.sdlc/constraints.md` |

Global, then profile, then solution. The effective set is rendered on demand by
`sdlc constraints --for <artifact>`, never copied into a solution — and **that command is also how
the set reaches a session, on every host.** Running it is the first step of every skill that judges
or writes an artifact. Per-host native delivery is an optimisation layered on top, never the
mechanism.

### 7.4 Ids and revisions

**Ids are a monotonic sequence.** Assigned once, never reused, never backfilled. A removed constraint
leaves a gap, and the gap plus the commit that made it is the record.

**A change to what is forbidden is a new constraint.** The old one keeps its id, stays on the page
verbatim, and gains `Superseded-by`.

**A change to the wording alone is a revision.** `CON_01` becomes `CON_01.2` — same `Forbids`, better
words. Findings cite the revision. **A bare id is revision 1**, and that is the canonical form.

### 7.5 The set

| Id | Rule | Applies to |
|---|---|---|
| `CON_01` | an intent states the problem, not the solution | intent |
| `CON_02` | an intent's unknowns are recorded as open questions, not answered by guess | intent |
| `CON_03` | a criterion states an observable outcome and how it is checked | spec |
| `CON_04` | a spec covers every outcome its intent asks for | spec |
| `CON_05` | a spec introduces no scope its intent did not ask for | spec |
| `CON_06` | a plan's proof is adequate to the criterion it claims to prove | plan |
| `CON_07` | a change does only what its plan declared, including inside files the plan named | diff |
| `CON_08` | a verify claim rests on evidence that exists and supports it | verify |
| `CON_09` | no artifact restates another's content as its own authority | every artifact |
| `CON_10` | a gate refusal's reason says why | an approval decision |
| `CON_11` | every constraint carries a Forbids clause that rules something out | a diff adding a constraint |
| `CON_12` | a criterion's check is one the solution's testing layers can run | spec |
| `CON_13` | a plan that departs from the declared defaults says why | plan |
| `CON_14` | an open item is one nobody can resolve yet, not one nobody has done | intent, spec, plan |

`CON_06` is the one to watch: it drew a blocking finding in roughly half of twelve runs on a golden
sample, the cause was traced to the sample rather than the constraint, and the fix was made by
reading rather than re-running.

`CON_04` and `CON_05` stay separate — a gap and creep fail differently and are remedied differently.

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

---

## 8. The solution's own files

Three files in `.sdlc/`, split by **mutability**:

| File | Changed by | Protection |
|---|---|---|
| `constraints.md` | a person, by hand | the hook denies every agent write |
| `techstack.md` | a cycle whose plan declares it | the ordinary declared-file gate |
| `solution.md` | `init`, and a cycle for `profile` alone | the hook denies every agent write |

`.sdlc/README.md` is framework-owned, regenerated by `init` in update mode, unprotected.

### 8.1 `constraints.md`

The solution's own `LOC_nn` constraints, hand-edited. There is no configuration flag unlocking it and
no special cycle type for protected changes.

### 8.2 `techstack.md`

What this solution is built from, and what a plan builds with. **Not enforced:** no global constraint
may name a language, framework or package, so nothing here binds by itself. A solution that wants a
default to bind adds a `LOC_` constraint or an architecture test.

**A plan may depart from any default. Silence about departing is what is illegal** — `CON_13`.

### 8.3 `solution.md`

Two keys.

| Key | What consumes it |
|---|---|
| `profile` | `init` and `plan`, to resolve which profile package applies |
| `origin` | `intent`, to know whether "this repository holds no solution at all" is true |

There is deliberately no baseline field. Constraints govern the change, never code that predates
adoption, so an existing repository is not in violation on its first day.

---

## 9. `init`

Adopting a process is a decision about how a team works, so `init` is human-invoked and never
model-invoked. Three modes (D-28):

| Mode | When | What it does |
|---|---|---|
| **scaffold** | a new solution | writes everything, `origin: scaffolded` |
| **onboard** | existing code, no `.sdlc/` | writes everything, `origin: adopted` |
| **update** | `.sdlc/` exists | refreshes only what the framework owns |

**Update needs no list of what it may touch: it updates what it owns, and what it owns is everything
not protected.** So it refreshes hook configs, pipelines, and `.sdlc/README.md`.

It refuses outside a git repository. Without git, `build` cannot record the diff that `verify`
derives from and the gates key on a `cycle/<id>` branch that cannot exist — so every hook would
permit everything while the artifacts still looked governed.

**It interviews, then proposes.** `init` asks what the solution is about and what matters in it, and
from **the answers** drafts an initial `LOC_` set for a person to place. From the answers, not the
codebase: reading an existing repository to decide what to forbid would make adoption a judgement on
code that predates the framework.

**`origin` is decided by `git ls-files`, and it is about code rather than commits.** A repository
holding only an empty commit, a README or a licence is `scaffolded`. `techstack.md` comes from the
profile's defaults when scaffolded, and is read from the repository when adopted.

**`AGENTS.md`** is lean and under 200 lines: a short summary of what the solution is; references —
a relative path plus a one-line description — to `techstack.md`, the build and test procedure, and
how to run the application locally; and a repo map. It never contains constraints, and never anything
that goes stale the moment a cycle runs. If one already exists, `init` prints the leaned version and
leaves the file alone.

**It commits what it created.** An uncommitted `AGENTS.md` is untracked when a cycle takes its diff,
so the diff records the whole file as new, and a plan that says it *changes* that file can never
match — the first cycle would fail `CON_07` for something adoption did.

It creates no empty directories.

---

## 10. Building and landing

### 10.1 `batch-build` completes cycles

A local skill, executed rather than subscribed to. It collects the approved cycles in **this** working
copy, builds **and verifies** each, and reports. There is no shared queue (D-22).

1. Build the claim graph from the approved plans.
2. Partition into non-overlapping groups.
3. Run each group in parallel, each in its own git worktree.
4. Serialise overlapping plans in cycle-id order, rebasing each on the previous result.

Four per-cycle outcomes: `passed`, `blocked`, `escalated`, and **not attempted** — a cycle downstream
of a failure in the claim graph.

**It schedules around collisions rather than resolving them.** Plans declare the files they claim, so
a collision is known before anything is built. `resolve-conflict` remains, scoped to files that
cannot be claimed exclusively and enumerated rather than discovered: `.csproj` and
`Directory.Packages.props`, dependency-injection registration, `appsettings`, EF Core migrations,
Terraform files near state.

**EF Core migrations serialise.** Two branches that each add a migration snapshot the same parent
model, and the merged chain breaks in a way that compiles and then fails at deploy.

**A sibling's failure does not discard the cycles that completed.** Each can be landed individually.

**Concurrency is capped by what a plan needs.** The claim graph carries a needs-backing-services bit,
derived from whether the plan touches the app host or declares service dependencies. Service-free
plans run wide; plans standing up SQL Server, Redis and a Service Bus emulator run under a low cap.

A local cycle index persists state so a run can be resumed and read. It is not a coordination
mechanism.

### 10.2 Local run modes

Selected by an environment variable that `batch-build` sets:

| | local dev | test / batch |
|---|---|---|
| host ports | stable, so a URL can be bookmarked | dynamic |
| container names | stable | `{app}-{shortGuid}` |
| volumes | persistent, named | ephemeral, or named per run |
| dashboard and OTLP endpoint | on, fixed | off, or overridden per run |
| databases | persistent | named per run |

**Named volumes are a worse collision than ports.** Two concurrent app hosts calling
`WithDataVolume()` share one volume — the same database files, two writers — which fails strangely
rather than loudly. So the run id belongs on container names, volume names and database names, not
only on ports.

Tests discover endpoints through `Aspire.Hosting.Testing` rather than knowing a port number.

### 10.3 `land`

`land` is what touches the remote: it pushes, opens the pull request, and links the work item with
the intent and spec details.

**`land --all`** sweeps every cycle whose verdict is `passed`, with five guards (D-23): it lands only
`passed`; it re-checks each cycle's gate rather than trusting the verdict; it reports what it would
land and **asks once** before the first push; a push failure on one cycle does not stop the others;
and each pull request is reported individually.

**`batch-build --land`** composes the two, invoking `land` per completed cycle, and asks before the
run rather than at the end (D-24). Plain `batch-build` never pushes.

---

## 11. Tech stack and profiles

Strongly opinionated, and the opinion is a swappable **profile**.

### 11.1 `dotnet-default`

| Concern | Choice |
|---|---|
| language, framework | C#, .NET |
| unit and integration testing | MSTest on Microsoft.Testing.Platform |
| component testing | bUnit, for Blazor components |
| end-to-end testing | Playwright, in Chromium **and** WebKit |
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
| web | Blazor |
| desktop and Android | MAUI Blazor Hybrid |

**Shared UI is Blazor components in a Razor class library**, hosted by Blazor for web and MAUI Blazor
Hybrid for desktop and Android. MAUI does not target web.

**Service Bus and SignalR are not alternatives.** Service Bus is asynchronous messaging between
services; SignalR is server-to-client push.

**An Aspire AppHost is conditional.** More than one process, or one process plus a backing service a
developer would otherwise start by hand. A single executable with no backing services does not get
one.

**`dotnet test` needs two settings or it does not run the suite.** On the .NET 10 SDK it defaults to
VSTest and Microsoft.Testing.Platform refuses that path, so the profile ships both:
`"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`, and
`<EnableMSTestRunner>true</EnableMSTestRunner>` in `Directory.Build.props` rather than one test
project. **Do not set `TestingPlatformDotnetTestSupport`** — the .NET 9 mechanism, and setting it
instead is what left a trial unable to run `dotnet test` at all.

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

**There is no device end-to-end layer.** Appium is absent, not scheduled: a flaky proof gets rerun
until green, which teaches a team to ignore red; the prerequisite lands on every machine and in CI;
and MAUI's own device-test tooling is not packaged for consumers. MAUI Blazor Hybrid hosts the same
components in a WebView, so bUnit tests them once and Playwright runs them in the two engines the
hybrid targets use. **What remains unproven is the native shell only** — platform permissions, app
lifecycle, deep links, native file pickers, platform chrome.

**A device-only outcome is a bound in the intent, never a criterion in the spec**, verified by a
person outside the cycle. `CON_12` keeps them out of specs. So MAUI-targeted work has some outcomes
this framework does not govern.

### 11.3 The MofonCode profile

Private: its own repository, published to an Azure Artifacts feed only the team can restore. The
public plugin ships `dotnet-default` and nothing house-specific.

It adds `MofonCode.Core` — a minimal mediator, `Result<T>` handlers over FluentResults, `TraceResult`
over OpenTelemetry activities, pipeline behaviours, composable specifications, throttled concurrency
— plus the conventions that go with it:

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

**`origin` decides whether a default binds.** A `scaffolded` solution takes the profile's patterns; in
an `adopted` solution **the existing standard wins.** The profile proposes a shape for new work and
never rewrites the conventions of a codebase that predates it.

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

### 11.5 What a profile is

**Templates plus constraints.** Package set and versions, analyzer set, `Directory.Build.props` and
`Directory.Packages.props` fragments, project templates, app host run-mode defaults, pipeline
templates, and its own `PRO_nn`.

Profiles are **NuGet packages** — versioned, restorable, identical to consume from nuget.org for the
public one and from an Azure Artifacts feed for the private one.

**Versions are resolved at `init` and recorded, never guessed in a document:** take the latest stable
release compatible with the target framework, write it into `Directory.Packages.props`, and let the
diff record what was taken.

A profile's defaults reach a plan by being **extracted and injected**, because a session working
inside a governed solution cannot read a file that lives in the plugin.

---

## 12. Hosts

Every candidate host can deny a tool call before it runs, so the enforcement primitive is portable.
What differs is vocabulary.

| Host | Blocking event | Config | Deny shape |
|---|---|---|---|
| Claude Code | `PreToolUse` | `.claude/settings.json` or plugin | `permissionDecision: "deny"` |
| GitHub Copilot | `preToolUse` | `.github/hooks/*.json` | `permissionDecision`; takes `bash` **and** `powershell` |
| OpenAI Codex CLI | `PreToolUse` | `.codex/config.toml` | `permissionDecision` + reason |
| Gemini CLI | `BeforeTool` | `.gemini/settings.json` | `{"decision":"deny","reason":"..."}` |
| Antigravity | `PreToolUse` | `.agents/hooks.json` or a plugin | same, plus `ask` / `force_ask` |

**v1 ships Claude Code and GitHub Copilot.**

ChatGPT is not a target — Codex is OpenAI's repository-agentic surface. Antigravity plugins are the
stated successor to Gemini CLI extensions.

### 12.1 What the adapter normalises

Event names; tool names (`Edit`/`Write`/`Bash` against `edit`/`view`/`bash` against
`write_file`/`replace`/`run_shell_command`); input key names and nesting; and the deny shape. So
`sdlc hook --host <name>` reads stdin, normalises, asks the one policy engine, and emits that host's
format. Each host has a conformance test that feeds a captured real payload and asserts a deny.

A payload the reader cannot understand **allows the call and says so on standard error.** These
surfaces are young: failing closed on a renamed field would stop a person working for a reason they
cannot act on, and failing silently would permit everything while appearing to protect.

### 12.2 One consequence of the Copilot choice

Copilot's cloud coding agent is asynchronous and cannot conduct an interview, so the intent phase
cannot run there. v1 targets Copilot CLI and IDE chat. The cloud agent suits `build` from an
already-approved plan — later.

---

## 13. Pipelines

Generated at `init`, **per platform** — GitHub Actions or Azure Pipelines. ("Host" means the agent;
"platform" means where the code lives.)

Every pull request builds, tests, **displays code coverage**, and runs `sdlc verify-chain`.

Coverage is displayed, not enforced — no threshold. Coverlet plus ReportGenerator; Azure DevOps has a
native coverage tab, GitHub needs a PR-comment action. One cheap addition inside the no-threshold
rule: report the coverage **delta** against the target branch.

`verify-chain` re-establishes, from the repository rather than any agent's word, that:

- every artifact in the chain exists
- the approval ledger's standing decision is `approved`
- all three recorded hashes match the bytes on disk
- every findings round's `upstream_sha256` matches its upstream now
- every spec criterion maps to a named test
- every changed file was declared, **and every declared file changed**
- the outcome in `verify.md` is `pass`

**And what it must not claim.** With local-only approval it proves the entry exists and the hashes
match. It cannot prove a second person approved, and it says so in its own output.

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
            F_01   CON_04      blocking
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
3. **It ends with the literal next command**, copy-pasteable.
4. **ASCII-safe glyphs and a no-colour mode**, because it renders in a pipeline log and in the default
   Windows console.

The `Says` line is read from the document — its first heading and its section headings. A
command-line tool cannot summarise content, and inventing one would be worse than showing the
headings a person wrote.

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
| `batch-build` | completes approved cycles, concurrently where the claim graph allows |
| `resolve-conflict` | resolves collisions on files that cannot be claimed exclusively |
| `land` | pushes, opens the pull request, links the work item |
| `draft` | prints a protected file's entry for a person to place — §5.3 |
| `status` | prints the stage panel, and the state of a cycle or of all cycles |
| `constraints` | renders the effective constraint set, all tiers |
| `suggestions` | lists accumulated non-blocking findings |

### 15.1 Preconditions

| Skill | Requires | Refuses when | Writes |
|---|---|---|---|
| `init` | a git repository | not a repository | the scaffolding, per mode |
| `intent` | nothing | an intent exists | `intent.md`, creating the cycle directory |
| `spec` | a clean intent | intent missing or not clean; a spec exists | `spec.md` |
| `plan` | a clean spec | spec missing or not clean; a plan exists | `plan.md` |
| `interrogate` | the artifact exists | it does not | a findings round |
| `refine` | a round with open blockers | no blockers; cycle approved | that artifact only |
| `steer` | an intent **and** a spec | only an intent; approved without a reopen | intent, spec, plan — whichever exist |
| `draft approval` | three documents, each with a round | any missing, uninterrogated, or holding an open blocker | nothing; it prints |
| `build` | the gate satisfied | the gate refuses | code, evidence |
| `verify` | evidence exists | no evidence | `verify.md` |
| `remediate` | verify blocked | not blocked; three passes spent | code and tests only |
| `land` | verify passed | not passed; escalated | push, pull request |

**A stage skill whose artifact already exists refuses and names what you probably meant** —
`interrogate` to judge it, or `steer` to change it (D-18).

`intent` creates the cycle directory as its first act and fails if it exists. Creating it **is** the
collision check, and the filesystem makes that atomic (D-10).

---

## 16. Layout

```
.sdlc/
  solution.md               profile, origin - written by init
  techstack.md              what this solution is built from
  constraints.md            this solution's own LOC_nn, hand-edited
  README.md                 terminology, flow and commands
  cycles/
    20260929_1456_32/
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
        tests.trx
        tests.json          the normalised summary verify reads
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

A cycle index exists so `batch-build` does not glob directories to find work, and each cycle records
its branch and work item.

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
entirely on paper, and landing has never refused a cycle. Iteration 3 called this its largest
untested claim, and **`batch-build` depends on it completely** — unattended building is only safe
because a cycle that cannot be made to pass stops instead of landing.

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
- **Resolving merge conflicts as the primary strategy.** Collisions are scheduled around.
- **Remote plan approval** — the local paste is the gate.
- **A shared batch queue.**
- **Tooling for splitting work** — D-25.
- **A combined design skill** — D-16.
- **Waiving a blocking finding** — D-27.
- **Supporting ChatGPT as a host.**
- **Plans detailed to the line.**
- **A device end-to-end layer** — §11.2.
- **Baselining existing problems at adoption.**

---

## 19. Open questions

1. **Which `PRO_nn` conventions become analyzers?** §11.3 establishes that nothing decides them
   today and each splits in two. One-folder-per-feature is almost entirely mechanical and is the
   obvious first. Needs the kernel's analyzers read, not a decision taken.
2. **Does the no-pragma rule ship as a build check?** §11.4 keeps every suppression in
   `Directory.Build.props` with a reason and zero pragmas in source. The rule is mechanically
   checkable, so the question is whether it is worth a custom analyzer or a build-time grep.
3. **How does the Azure DevOps adapter get exercised?** `dice-trial` has a GitHub remote and no Azure
   DevOps one, and a second trial repository was declined. Without an answer the adapter ships
   untested against a real server.
4. **`CON_15`, held rather than open.** *A plan's blast radius matches what it changes* — admissible
   on all three tests, and held until `tier` is in use so the constraint describes the rule rather
   than its first example.
5. **The cross-cycle learning loop, deferred by decision.** v02 built `retro` and `absorb-retro` with
   a corpus, an importer that refuses to overwrite differing content, and signals computed read-only;
   v03 removed them and recorded the cost — *"nothing aggregates automatically."* The data to
   aggregate already exists in the findings rounds. Three things to carry forward if it returns: the
   completion-certificate trap, where an artifact's existence means `done` and a mistaken one
   silently loses work; `caused-by` / `caused-at-stage`, which is how the process learns **which
   stage leaks**; and the private/public boundary, which v02 never had to answer.

---

## 20. Decisions

Ids are a monotonic sequence of their own, separate from `CON_nn`: a constraint judges an artifact, a
decision explains the framework. Assigned once, never reused. **A reversed decision is superseded by
a new id rather than edited**, so a commit citing `D-14` still means what it meant.

Each entry carries the decision, the cost it avoids, and what was rejected. The `Rejected` field is
half the value: in six months the question is not "why this" but "why not that".

The full working-through — what the previous draft said, how each contradiction was found, and the
code each decision changes — is in commit `adf15cc`.

### D-01 — Side research and evaluation run in a new process

**Why.** Research returns an answer and should not leave its search behind. An evaluation must not
have seen the thing it judges being made.

**Rejected.** Subagents for interrogation: better ergonomics on one host, two code paths for the most
important skill. In-process self-judgement in the healing loop: removes the only thing independence
buys.

### D-02 — Constraints bind an artifact's body; its open region is the compliant exit

**Why.** `CON_02` forbids an intent *stating as settled fact* a figure nobody measured — so an item
recorded as an open question satisfies it rather than overriding it. `CON_04` forbids leaving an
intent's *stated outcomes* uncovered, and an open question is not a stated outcome. So clean-to-advance
**stays**: an unresolvable item moves to the open region, and that is the fix.

**Rejected.** Advancing on a round merely existing; a pre-gate acceptance record, which is a second
gate under another name; requiring a clean round only of the spec and plan.

### D-03 — An open item must be one nobody can resolve yet

**Why.** `D-02` makes the open region a legitimate exit, which makes it an exploitable one: an agent
can empty the body into it and satisfy every constraint by asserting nothing. `CON_14` is the only
control, and no hook can decide it.

**Rejected.** Capping the number of open items — a number nobody chose. Leaving it ungoverned — a
person reading a body with nothing in it has nothing to object to.

### D-04 — The plan gate is about dependency, not the absence of open items

**Why.** A hard no-open-items rule would have blocked the trial cycle, whose plan carried limits
citing two open intent questions, was approved, built, and shipped working software. False positives
on a gate are what make a team route around it.

**Rejected.** "No blockers *or* open items at plan": blocks cycles that are fine, for a property the
existing constraints already deliver through `D-05`.

### D-05 — The spec disposes of every intent open item

**Why.** The check it replaces — every intent statement maps to a criterion — cannot exist, because
`CON_04` is in the library only on the grounds that no hook can decide coverage. Disposal is a
different question: *presence*, which a parser decides.

**Rejected.** Keeping the coverage check and removing `CON_04`; keeping both, which `CON_11` exists to
prevent.

### D-06 — An artifact section earns its place by having a consumer

**Why.** Decoration costs attention at every interrogation. Same test the library applies to itself,
pointed at structure.

**Applied.** `Who` dropped — no consumer of any kind, and `Problem` now carries who has it. `Order`
folded into `Files`, with the ordering *reasoning* preserved there rather than lost. `Risk` kept — the
person at the gate is a real consumer. `Done looks like` renamed `Wanted outcomes`, because it is
`CON_04`'s left-hand side and cannot move to the spec, but the old name read as a spec concern.

**Rejected.** Dropping `Risk` as unused. Giving `Risk` a constraint, which would be admissible and is
deferred until the practice settles.

### D-07, D-07a — An artifact is an envelope carrying a body

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

**The risk to watch.** Naming a pattern invites stretching it, into an envelope carrying fields that
are uniform only because somebody forced them. The test is the guard, not the pattern's gravity.

### D-08, D-08a — The open region is `## Open`, on every artifact

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

**Why.** `origin` is a historical fact that must never change and `profile` is a choice revisited
rarely, so protecting both costs one manual line per migration. The alternatives either expose
`origin` or introduce the framework's **first conditional hook rule**, which is where a bypass would
live. Every hook rule staying absolute means the whole rule set states in one sentence.

**And it completes a pattern.** Four protected files follow one rule — a tool never writes them, a
tool drafts what to put in them — so it is **one verb, not four commands**.

**Rejected.** Per-key protection: the hook sees a path, never a before-and-after. Leaving it
unprotected: makes `origin` agent-rewritable. Splitting into two files.

### D-10 — The intent skill creates the cycle directory, and fails if it exists

**Why.** The claim that an existing directory catches a collision was false, because at allocation
there was no directory. Creating it **is** the check, and the filesystem makes that atomic.

**Rejected.** Deleting the claim, which leaves a collision to surface as a confusing merge. Checking
without creating, which leaves a race.

### D-11 — A refusal is drafted too

**Why.** Rejecting is what a person does when they are least inclined to look up a format. Giving help
to the easy case and withholding it from the hard one is the asymmetry people resent.

**Rejected.** Leaving refusals hand-written. Dropping `denied` and keeping two states, which loses the
difference between "fix this and come back" and "no, not this way".

### D-12 — `SolutionFileName` becomes `SolutionConstraintsFileName`

**Why.** The constant meant *the solution's constraints file* and read as *the solution file*, and
`D-09` puts `solution.md` in the same code path — so the next person picks the wrong constant out of
autocomplete and the hook guards the wrong file.

### D-13 — Evidence is normalised at the boundary

**Why.** **The gate must not know about test platforms.** Reading evidence natively would put a `.trx`
parser inside the gate, then a JUnit parser beside it, and the thing that owns *process* constraints
starts owning *file formats*. Normalising in the build step puts the stack-specific part where the
stack already is. Keeping the original alongside the summary means `CON_08` is satisfied by both.

**Rejected.** Roles with a per-profile reader in the gate. Narrowing §6.2's neutrality claim to the
constraint text only, which reduces "swappable" to "swappable within .NET". Deferring because both
profiles are .NET.

### D-14 — `verify-chain` checks the comparison in both directions

**Superseded by D-15.** Kept because a decision is superseded, never edited.

### D-15 — The declared-versus-changed comparison is a signal, not a gate

**Why.** `CON_07` already says the file set is not its job — *"the file set is mechanical and a hook
already enforces it. Creep **inside** an approved file passes that hook"* — so the constraint exists
*because* the comparison cannot see inside a file, and the comparison was never load-bearing for
scope creep. And the counterexample dissolves: a plan whose declared file went untouched while every
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

**Why.** The separate commands are where a person decides to proceed, not code organisation. Even a
combined skill interviewing at every stage removes the moment you stop, read the intent, and *choose*
to go on. Human-in-the-loop is not only "was a person asked" but "did a person decide to continue".

**Rejected.** A chained orchestrator that interrogates between stages — which would preserve every
constraint, and was rejected on the decision points rather than the constraints.

### D-17 — `steer` re-runs the chain from an existing intent and spec

**Why.** The correct path by hand is expensive enough that people will not pay it, so steering ends up
in conversation where it is lost. **A path so costly that it pushes information out of the system is
worse than no path.** `steer` keeps the correct path and makes it affordable — compliance gets
cheaper and no constraint loosens.

**Rejected.** A `steering.md` input file, which this makes unnecessary and which would need six
answers of its own. An `advice` finding alone: it needs an artifact to attach to, and the scenario is
steering arriving before a plan exists. A proposed implementation in the spec: its criteria must
derive from the intent, so a proposal appearing there derives from nothing, which `CON_05` forbids.

### D-18 — A stage skill refuses when its artifact exists, and names what you meant

**Why.** Typing `intent <id>` must never silently overwrite an intent. A flag that re-interrogates
would be a second way to do what `interrogate` already does, and the one people type is the one that
drifts. A refusal that teaches the right command adds nothing and removes a destructive mode.

### D-19 — `reopen` is a fourth decision, and it is how a wrong plan is recovered

**Why.** Without it a wrong plan has no legal move: `remediate` may not edit a planning document,
`refine` refuses once approved, and the plan is hash-locked — so `remediate` could only keep trying to
make wrong code satisfy a wrong plan until it escalates. It also gives the design its symmetry: code
failures are capped because an unbounded fix-verify loop burns tokens unwatched; artifact failures are
not.

**Rejected.** Abandoning and starting over, which throws away an intent and spec that are probably
fine. Letting `remediate` edit the plan — the inversion §2.4 exists to prevent. Hand-editing and
relying on the hash mismatch, which leaves no record of why.

### D-20 — Each skill's preconditions are stated in one table

**Why.** Its absence is what lets a skill quietly check the wrong precondition. Drafting it surfaced
two rules nobody had written: `spec` and `plan` refuse on an existing artifact, and `refine` refuses
on an approved cycle.

### D-21 — A findings round records the upstream hash it judged against

**Why.** The approval proves *these three documents were approved*, not *the spec was derived from
this intent*. So an intent edited after a clean spec leaves a spec derived from an intent that no
longer exists, with every hash matching on disk. And it is not hypothetical — `D-17` is the mechanism
that creates it.

**Why on the round rather than in the frontmatter**, which was the original proposal: a hash in the
spec's frontmatter would be written by the spec skill and not updated by a person hand-editing, so it
could be stale *and wrong*; the round is written by the interrogation, which just read both. It also
describes the thing that goes stale — the judgement — and makes `D-02` compute staleness for nothing.

### D-22 — `batch-build` completes cycles; `land` pushes

**Why.** One word carried two meanings. And "successes" implies success is determined, so
`batch-build` must **verify** as well as build — a batch that cannot say which cycles succeeded cannot
land the ones that did.

**Rejected.** `batch-build` pushing, which makes an unattended run reach the remote. Building without
verifying, which leaves "success" undefined.

### D-23 — `land --all` sweeps every completed cycle, with one confirmation

**Why.** A batch of eight that completes and then needs eight manual invocations is a batch whose
value evaporates at the last step. One confirmation before the first push, because a pull request is
reversible but **outward-facing** — and printing the list also covers the surprise that every
`passed` cycle is eligible, including one from last week.

**Rejected.** Per-cycle confirmation. No confirmation. Landing `blocked` cycles with a warning — the
verdict is the only thing between unattended building and unreviewed work reaching a pull request.

### D-24 — `batch-build --land` composes the two; plain `batch-build` stops at completion

**Why.** Landing is not a new decision: the approval already said *this plan is agreed, build it*, and
a pull request **requests** review rather than committing anything. So the confirmation is about
**volume** rather than permission. A flag rather than the default, because the composed form touches
the remote and the plain one does not.

**Rejected.** Landing by default. A confirmation per cycle inside an unattended run. No confirmation
at all — defensible, and rejected because volume is a real cost somebody should have chosen.

### D-25 — There is no `split` skill, and no `after` ordering

**Why.** v03 reached the same conclusion: splitting is a scoping decision that belongs to a person,
and *"auto-splitting without dependency ordering produces cycles that cannot be built in isolation,
which is worse than one large cycle that at least builds."* `after` was introduced **specifically to
supply that missing concept** — machinery built to enable a feature whose value was never
established, which is `D-06`'s test pointed at a skill.

**The risk this leaves open.** Nothing pushes back on a sixty-file plan, and v03's argument still
holds: *"a plan touching sixty files across five features cannot be meaningfully read, so approval
degrades into signing."* Partially mitigated by `tier`, which signposts reversibility.

**What could return without the costs.** A **read-only advisor** that proposes how to cut an intent and
writes nothing. It creates no cycles, so it cannot create cycles that will not build.

### D-26 — The command line is the mechanism; MCP is a later surface

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

**Why.** `D-02` had already made the waiver vestigial: acceptance existed because some blockers
appeared to have no legal exit, and once every blocker has one, no legitimate case for waiving remains.
**And the exit does not launder bad work** — a criterion whose proof cannot reach it is recorded under
`Open`, so `CON_06` is satisfied, and then **verify still blocks** because every criterion must map to
a passing test. The exit moves *where* a problem is caught, never *whether*. Some blockers have no
open-region exit and must simply be fixed.

**`CON_10` rebinds rather than retiring.** Its forbids clause applies exactly to a gate refusal's
reason, which the ledger already requires.

### D-28 — `init` has three modes, and the protected set defines what update may touch

**Why.** `init` refusing when `.sdlc/` exists was correct while nothing needed refreshing, and it
leaves a solution's scaffolding to rot across framework versions with nothing to fix it. **Update
needs no list of exclusions: it updates what it owns, and what it owns is everything not protected.**

It also closes part of the maintenance gap — when the framework version moves, something now updates
a solution's scaffolding. The judgement half, proposing changes to constraints and the tech stack,
remains open.

### D-29 — The interrogate-refine loop is a mode of each stage skill

**Why.** `D-02` requires a clean round to advance, so the stage that must reach one is the stage that
should run the loop. And `D-16` is satisfied, because the loop stops on convergence and **hands back
rather than claiming to be done** — so the decision to keep interrogating stays the person's.

**Rejected.** A named skill of its own: an eighteenth entry and a second way to invoke one loop.

### D-30 — There are two severities, not three

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
