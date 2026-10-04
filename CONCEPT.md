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
| Local deterministic | the `sdlc` gate binary, a pre-tool hook, a pre-push hook | a human with a shell | fast feedback |
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
| `approval.md` | a gate decision — three hashes, who, when, and a reason on anything but an approval |
| `abandoned.md` | why the cycle stopped |
| `constraints.md` | a proposed `LOC_nn` |
| `solution.md` | the `profile` line, on migration |

One verb drafts them all: `sdlc draft approval <id>`, `draft abandon <id>`, `draft constraint`,
`draft profile <name>`. `draft approval` also takes `--deny` and `--changes-requested` (D-11). Each
produces a ready-made entry; the person reads it, decides, and places it.

**A gate decision is placed by renaming a file, and the other three by hand (D-52).** `draft approval`
writes the complete ledger — `approval.<decision>.draft.md`, named for what it records — beside the
ledger it would replace, and prints it with the rename. Where a decision already stands, the draft
reproduces it above the new entry fence to fence, so the rename cannot lose history.

**The pre-tool hook denies every agent write to all four, with no exception** — and every agent command
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

**A disposition moves off `open` only on a fresh reader's silence** (D-49). The reader judges the
document in front of it and has no history; the round records that an earlier finding it did not raise
is now `answered` — or `withdrawn`, where the objection was dropped rather than answered. A finding the
reader raises again stays open whatever anyone says about it, and so does one nobody can place in
either case. This is the only exit from a blocking finding, because D-27 removes every other one.

**How many rounds is the owner's call, and there is no cap.** The panel reports what each round found
and the cost of a round, and attaches no recommendation about continuing.

### 6.2 A round records the upstream it judged against

`upstream_sha256` is the hash of the artifact above, as the round saw it. A round is clean **as of**
that upstream, so changing an upstream makes a downstream round stale, the stage gate fails, and
re-interrogation is required (D-21).

### 6.3 `refine` and `remediate` — two editors with opposite guards

| | `refine <id> <artifact>` | `remediate <id>` | Enforced by |
|---|---|---|---|
| Runs after | `interrogate` | a blocked `verify` | the skill |
| May edit | the one named artifact | source and tests | the skill |
| May never edit `approval.md` | yes | yes | **the hook**, at the tool call |
| May never edit source or tests | yes | — | the skill |
| May never edit a planning document | — | yes | the skill, and **the gate** afterwards |
| Capped | no | three passes, then `escalated` | the skill |
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
nobody — and the only wording a revision has actually replaced was `CON_07`'s original `Why`, which
claimed a hook enforced the declared file set when no hook ever has. A rule library is read as current;
carrying a corrected error in it forever is the worse of the two losses.

### 7.5 The set

| Id | Rule | Applies to |
|---|---|---|
| `CON_01` | an intent states the problem, not the solution | intent |
| `CON_02` | an intent's unknowns are recorded as open questions, not answered by guess | intent |
| `CON_03` | a criterion states an observable outcome and how it is checked | spec |
| `CON_04` | a spec covers every outcome its intent asks for | spec |
| `CON_05` | a spec introduces no scope its intent did not ask for | spec |
| `CON_06` | a plan's proof is adequate to the criterion it claims to prove | plan |
| `CON_07.2` | a change does only what its plan declared, including inside files the plan named | diff |
| `CON_08` | a verify claim rests on evidence that exists and supports it | verify |
| `CON_09` | no artifact restates another's content as its own authority | every artifact |
| `CON_10.2` | a gate refusal's reason says what has to change | a gate decision that is not an approval |
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

`.sdlc/README.md` is framework-owned, regenerated by `init` in update mode, unprotected. So is
`.sdlc/hooks/pre-push`, which refuses to push a cycle branch that is not landable.

**The pre-push hook is written and not installed.** It lands under `.sdlc`, which is tracked, and `init`
prints `git config core.hooksPath .sdlc/hooks` for a person to run per clone. Writing into `.git/hooks`
would put the check on one machine and silently not on anybody else's — §2.1's failure mode, caused by the
thing meant to prevent it.

### 8.1 `constraints.md`

The solution's own `LOC_nn` constraints, hand-edited. There is no configuration flag unlocking it and
no special cycle type for protected changes.

### 8.2 `techstack.md`

What this solution is built from, and what a plan builds with. **Not enforced:** no global constraint
may name a language, framework or package, so nothing here binds by itself. A solution that wants a
default to bind adds a `LOC_` constraint or an architecture test.

**A plan may depart from any default. Silence about departing is what is illegal** — `CON_13`.

### 8.3 `solution.md`

Four keys.

| Key | What consumes it |
|---|---|
| `profile` | `init` and `plan`, to resolve which profile package applies |
| `origin` | `intent`, to know whether "this repository holds no solution at all" is true |
| `standard` | `plan`, to know whether new work follows the profile's conventions or the surrounding code's, and `init`, to know whether to write the stack files. Absent derives from `origin` (D-56) |
| `platform` | `doctor`, to tell a local solution from a broken install, and `land`, to know whether there is anywhere to push. Absent reads as `github` (D-53) |

**`origin` and `standard` are two keys because they answer two questions and only one of them may
move.** Where the code came from is a historical fact. Which conventions its new work follows is a
decision a team is entitled to revisit, and an adopted solution that wants the profile's conventions
needs somewhere to say so — D-56. The origin did both jobs until then, which left the opt-in
inexpressible: a team could want it and the file could only record that the code was old.

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
| **greenfield** | a new solution | writes the governance and the stack settings, `origin: scaffolded`, `standard: profile` |
| **onboard** | existing code, no `.sdlc/` | writes the governance only, `origin: adopted`, `standard: existing` |
| **update** | `.sdlc/` exists | refreshes only what the framework owns (D-40) |

**`--adopt-standard` is how an onboarded solution asks for the stack settings anyway**, and it records
`standard: profile` rather than touching the origin — D-56. It is a flag rather than a default because
it applies a convention to code that never agreed to one: `Directory.Build.props` makes warnings
errors, so the next build can fail on files nobody has touched. Nothing is overwritten, because each
stack file is created once and never rewritten. On **update** the files are written and the key is
printed for a person to place, since `solution.md` is protected — and the report says which half
happened, because that is the one state where the files on disk and the rule a plan reads disagree.

**`init` writes no code, and no projects** — D-35. A scaffolded solution gets the files that constrain
how code is written (`.editorconfig`, the two props files, `global.json`), and every one of them
constrains nothing until code exists. The solution itself — a solution file, its projects, the
banned-API list, the architecture tests and a reference slice — is `sdlc scaffold`, which refuses
unless a cycle's plan has been approved. Code arriving at adoption would be the one thing in a
governed repository with no chain behind it.

**What update may touch is decided by whose name is on the file — D-40.** It rewrites what the
framework named: `.sdlc/README.md`, `.github/hooks/sdlc.json`, `.github/workflows/sdlc-verify.yml` and
the pre-push hook. In a file the host and the team also own it owns its own entries and nothing else,
so `.claude/settings.json` is merged — the two hook entries are added or refreshed and every other key,
including a team's permissions and their own hooks, is kept. And where the path belongs to the platform
it writes only into an empty space: an existing `azure-pipelines.yml` is a repository's own CI, so the
step is printed for a person to place rather than written over.

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
holding only an empty commit, a README or a licence is `scaffolded`. `techstack.md` comes from the
profile's defaults when scaffolded, and is read from the repository when adopted.

**`AGENTS.md`** is lean and under 200 lines: a short summary of what the solution is; references —
a relative path plus a one-line description — to `techstack.md`, the build and test procedure, and
how to run the application locally; and a repo map. It never contains constraints, and never anything
that goes stale the moment a cycle runs. If one already exists, `init` prints the leaned version and
leaves the file alone.

**It commits what it created.** An uncommitted `AGENTS.md` is untracked when a cycle takes its diff,
so the diff records the whole file as new, and a plan that says it *changes* that file can never
match — the first cycle would fail `CON_07.2` for something adoption did.

It creates no empty directories.

---

## 10. Building and landing

### 10.1 `batch-build` completes cycles

A local skill, executed rather than subscribed to. It collects the approved cycles in **this** working
copy, builds **and verifies** each, and reports. There is no shared queue (D-22).

1. `sdlc cycles --claims` builds the claim graph from the declared plans and partitions it.
2. Run each group in parallel, each cycle in its own git worktree.
3. Serialise across groups in cycle-id order, rebasing each on the previous result.

**The graph is computed by the binary, not derived by the skill.** It is a set intersection over a file
table, so the binary does it the same way every time — and a graph a session worked out by reading plans
would come out slightly differently each run, while what it decides is what runs concurrently.

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
| api description | OpenAPI, through Microsoft.AspNetCore.OpenApi |
| tracing, metrics and logs | OpenTelemetry, exported to Azure Monitor |
| liveness | ASP.NET Core health checks, at `/health` |
| web | Blazor |
| desktop and Android | MAUI Blazor Hybrid |

**Shared UI is Blazor components in a Razor class library**, hosted by Blazor for web and MAUI Blazor
Hybrid for desktop and Android. MAUI does not target web.

**Liveness is the one row here that binds — D-54.** Every other row is a default a plan may depart from
by saying why (`CON_13`); this one the scaffold writes and a fitness function keeps, because the cost of
omitting it is paid in production by somebody who did not omit it. It is deliberately not an `ApiResult`
endpoint: the caller is a load balancer, which has never heard of this solution's envelope.

**Observability is not the error envelope.** A boundary response already carries a trace id, which is what
lets somebody asking about one request find what was withheld. That is correlation, not tracing: a solution
with no OpenTelemetry registration collects no spans and exports none, so there is nothing to ask.

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
function are written only where the standard is the profile's. An adopted solution gets them when its
team asks, with `--adopt-standard`, and not otherwise — D-56.

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

**No profile carries AppHost run-mode defaults, and this listed them until F-24.** An AppHost is
conditional (§11.1) and no profile ships one, so there was nothing for a default to be a default of.
§10.2 is where the run-mode contract lives; a profile that one day ships an AppHost template would
carry defaults for it as part of that template rather than as a category of its own.

**`dotnet-default` is carried by the binary; any other profile is a NuGet package** — D-33. The
default has to work on a machine with no feed configured and no network, because it is what adoption
uses, and a restore that can fail is a first command that can fail. Everything beyond the default is
restorable, versioned, from nuget.org for a public profile and an Azure Artifacts feed for a private
one — which is the mechanism the `mofoncode` profile needs and the default does not.

The cost, named: a change to `dotnet-default` is a release of the binary rather than of a package. That
is the right way round. The default is the thing every solution starts from, so it moves rarely and in
step with the gate that reads it; a profile that wants its own cadence is exactly the case the package
mechanism is for.

**Versions are resolved at `init` and recorded, never guessed in a document:** take the latest stable
release compatible with the target framework, write it into `Directory.Packages.props`, and let the
diff record what was taken.

A profile's defaults reach a plan by being **extracted and injected**, because a session working
inside a governed solution cannot read a file that lives in the plugin.

---

## 12. Hosts

Every candidate host can deny a tool call before it runs, so the enforcement primitive is portable.
What differs is vocabulary.

| Host | Blocking event | Turn end | Config | Deny shape |
|---|---|---|---|---|
| Claude Code | `PreToolUse` | `Stop` | `.claude/settings.json` or plugin | `permissionDecision: "deny"` |
| GitHub Copilot | `preToolUse` | `agentStop` | `.github/hooks/*.json` | `permissionDecision`; takes `bash` **and** `powershell` |
| OpenAI Codex CLI | `PreToolUse` | not checked | `.codex/config.toml` | `permissionDecision` + reason |
| Antigravity | `PreToolUse` | not checked | `.agents/hooks.json` or a plugin | same, plus `ask` / `force_ask` |

**"Not checked" is a real entry and is never filled in by inference.** This table said Copilot had no
turn-end event, which was never true, and the claim travelled into the generated configuration, the
command line, the guides and the readme before anybody re-read the host's documentation. D-46.

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

Every pull request installs the gate, builds, tests, **displays code coverage**, and runs
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
- the outcome in `verify.md` is `pass`

**What it deliberately does not check.** The comparison of declared files against changed files.
`D-15` moved that to the hook and made it a suggestion, because `CON_07.2` exists precisely *because*
the comparison cannot see inside a file — so it was never load-bearing for scope creep, and a noisy
blocker gets routed around. What blocks instead is outcome-based: the build fails, tests fail, or the
proof is not met.

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
| `draft approval` | three documents, each with a round | any missing, uninterrogated, holding an open blocker, or judged against an upstream that has changed | nothing; it prints |
| `build` | the gate satisfied | the gate refuses | code, evidence |
| `verify` | evidence exists | no evidence | `verify.md` |
| `remediate` | verify blocked | not blocked; three passes spent | code and tests only |
| `land` | verify passed | not passed; escalated | push, pull request |

**A stage skill whose artifact already exists refuses and names what you probably meant** —
`interrogate` to judge it, or `steer` to change it (D-18).

### 15.2 What the binary does, and what a skill does

The command line is the mechanism (D-26), so the split matters: a **command** is deterministic and the
binary owns it; a **skill** needs language and an agent runs it. A skill calls commands and shows what
they print verbatim.

| Command | Does |
|---|---|
| `init` | adopts the framework — three modes, §9. Human-invoked; the hook denies an agent running it |
| `new` | allocates a cycle id and creates its directory, which **is** the collision check (D-10) |
| `constraints [--for <artifact>]` | renders the effective set, all tiers. How the rules reach a session |
| `status <id> [--stage] [--brief]` | the stage panel — §14. `--brief` is the between-stages form: the counts, what the artifact says, and the next command |
| `findings <id> [--stage] [--round]` | what the interrogations said about one cycle, per artifact, each finding at the disposition that stands |
| `gate <id>` | whether a cycle's build may run. Exits 1 if not |
| `draft approval\|abandon\|constraint\|profile` | prints what belongs in a file only a person writes — §5.3 |
| `evidence <id>` | records the diff, its base commit, and the normalised test summary — §4.5 |
| `cycles [--stage\|--approved\|--verified]` | every cycle this solution holds, newest first, with what each is about and where it stands |
| `cycles --claims` | which cycles may be built together, and what collides — §10.1 |
| `cycles --landable` | every cycle, saying which may be landed |
| `suggestions [<id>]` | the open non-blocking findings, across cycles |
| `landable <id>` | whether one cycle may be landed, without landing it |
| `verify-chain <id>` | the whole chain, as the pull-request check runs it — §13 |
| `scaffold <id>` | writes the solution skeleton into an approved cycle — D-35 |
| `hook --host <name>` | judges one tool call — §12 |
| `version` | the three versions and which is which — the package, the plugin, and the artifact envelope |
| `doctor` | checks this install and this repository, and says which half is missing. Exits 1 if anything is wrong |

**`doctor` reports and never repairs**, and the third of its three outcomes is the point: what a binary
cannot see — whether a host loaded the skills, whether a git hook is installed in this clone, whether a
pipeline step is a *required* check — is marked `elsewhere` and names who to ask, rather than being
counted as sound. A command that said an install worked when nobody had established it is the failure
this document opens by naming.

**Typing a stage name is answered rather than refused.** The panel ends with the literal next command,
and for a stage that is a skill — `sdlc spec <id>` — the binary cannot do the work. So it says what the
stage is and names the invocation. A tool must never send somebody somewhere it does not go: the
alternative is the most-read output in the plugin pointing at "Unknown command", and a person concluding
the install is broken.

**`landable` and `land` are deliberately two things.** The refusal has to be identical whether one cycle
is landed or eight, so the binary owns it; pushing and writing a pull-request body needs the platform's
own tool and a sentence somebody wrote, so the skill owns that.

`intent` creates the cycle directory as its first act and fails if it exists. Creating it **is** the
collision check, and the filesystem makes that atomic (D-10).

---

## 16. Layout

```
.sdlc/
  solution.md               profile, origin, platform - written by init
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
- **Baselining existing problems at adoption.** `--adopt-standard` is not this: it writes the files
  that declare a convention and records nothing as forgiven (D-56).
- **Migrating existing code as part of adoption.** The opt-in declares the standard; a cycle moves the
  code.

---

## 19. Open questions

1. **Which `PRO_nn` conventions become analyzers?** Narrowed from three to two, and the blocker is
   gone: the kernel has been read and it ships no analyzers (§11.3). **Mediator dispatch no longer
   needs one** — a handler is private inside its holder (§11.3.1), so nothing can reach past the
   mediator and encapsulation decides it for free. What remains is one-folder-per-feature, which is
   almost entirely mechanical, and results-not-exceptions, whose decidable half is a handler's return
   type and whose judgement half is whether an operation can fail at all. The question is now narrower
   than "analyzer or not": a fitness function over the assemblies decides the mechanical part of both
   with no analyzer project, no diagnostic ids and no packaging — which is what the no-pragma rule
   turned out to want when it was built (§11.4).
2. **`CON_15`, held rather than open.** *A plan's blast radius matches what it changes* — admissible
   on all three tests, and held until `tier` is in use so the constraint describes the rule rather
   than its first example.
3. **The cross-cycle learning loop, deferred by decision.** v02 built `retro` and `absorb-retro` with
   a corpus, an importer that refuses to overwrite differing content, and signals computed read-only;
   v03 removed them and recorded the cost — *"nothing aggregates automatically."* The data to
   aggregate already exists in the findings rounds. Three things to carry forward if it returns: the
   completion-certificate trap, where an artifact's existence means `done` and a mistaken one
   silently loses work; `caused-by` / `caused-at-stage`, which is how the process learns **which
   stage leaks**; and the private/public boundary, which v02 never had to answer.
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

---

## 20. Decisions

Ids are a monotonic sequence of their own, separate from `CON_nn`: a constraint judges an artifact, a
decision explains the framework. Assigned once, never reused. **A reversed decision is superseded by
a new id rather than edited**, so a commit citing `D-14` still means what it meant.

**This is reference material, not reading.** Fifty-one entries and about half this document, so the
way in is a lookup rather than the top. `AGENTS.md` carries the two commands; in short, a section's
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

**Why.** `origin` is a historical fact that must never change and `profile` is a choice revisited
rarely, so protecting both costs one manual line per migration. The alternatives either expose
`origin` or introduce the framework's **first conditional hook rule**, which is where a bypass would
live. Every hook rule staying absolute means the whole rule set states in one sentence.

**And it completes a pattern.** Four protected files follow one rule — a tool never writes them, a
tool drafts what to put in them — so it is **one verb, not four commands**.

**Rejected.** Per-key protection: the hook sees a path, never a before-and-after. Leaving it
unprotected: makes `origin` agent-rewritable. Splitting into two files.

### D-10 — The intent skill creates the cycle directory, and fails if it exists

**Governs:** §15.2

**Why.** The claim that an existing directory catches a collision was false, because at allocation
there was no directory. Creating it **is** the check, and the filesystem makes that atomic.

**Rejected.** Deleting the claim, which leaves a collision to surface as a confusing merge. Checking
without creating, which leaves a race.

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

### D-22 — `batch-build` completes cycles; `land` pushes

**Governs:** §10.1

**Why.** One word carried two meanings. And "successes" implies success is determined, so
`batch-build` must **verify** as well as build — a batch that cannot say which cycles succeeded cannot
land the ones that did.

**Rejected.** `batch-build` pushing, which makes an unattended run reach the remote. Building without
verifying, which leaves "success" undefined.

### D-23 — `land --all` sweeps every completed cycle, with one confirmation

**Governs:** §10.3

**Why.** A batch of eight that completes and then needs eight manual invocations is a batch whose
value evaporates at the last step. One confirmation before the first push, because a pull request is
reversible but **outward-facing** — and printing the list also covers the surprise that every
`passed` cycle is eligible, including one from last week.

**Rejected.** Per-cycle confirmation. No confirmation. Landing `blocked` cycles with a warning — the
verdict is the only thing between unattended building and unreviewed work reaching a pull request.

### D-24 — `batch-build --land` composes the two; plain `batch-build` stops at completion

**Governs:** §10.3

**Why.** Landing is not a new decision: the approval already said *this plan is agreed, build it*, and
a pull request **requests** review rather than committing anything. So the confirmation is about
**volume** rather than permission. A flag rather than the default, because the composed form touches
the remote and the plain one does not.

**Rejected.** Landing by default. A confirmation per cycle inside an unattended run. No confirmation
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
while every skill, the pre-push hook, the status panel's next line and every guide say `sdlc` — so
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
`permissionDecision: "allow"`. `HookPolicy.Decide` refuses four protected paths and the shell commands
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
four files, not to re-answer a question the host has already been configured to answer.

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

**Decision.** Adoption rewrites a file only where the framework's own name is in the path. Where the
path belongs to the host or the platform, it owns its entries and merges them in, or it writes nothing
and prints them.

| | Adoption's claim | How |
|---|---|---|
| `.sdlc/**`, `.github/hooks/sdlc.json`, `.github/workflows/sdlc-verify.yml` | the whole file | rewritten on every run |
| `.claude/settings.json` | two hook entries | merged; every other key and sibling hook kept |
| `azure-pipelines.yml`, `AGENTS.md` | nothing, once one exists | printed for a person to place |
| `.sdlc/solution.md`, the stack files | nothing, once one exists | created, then never touched |

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
`gate`, `draft`, `evidence`, `claims`, `landable` and `verify-chain` all exited 3 with the same message
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
instructions in a prompt."* Neither was true. `HookPolicy.Decide` has two rules — the four protected
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

- `approval.md`, `abandoned.md`, `constraints.md` and `solution.md` — denied absolutely, in file writes
  and in shell commands alike. These work *because* they have no exception: there is no approve command
  for one to be carved out for.
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

**What it costs, stated plainly.** §20 is 43% of `CONCEPT.md` and the longest entry (D-38) is 188
lines, which is longer than most sections it governs. A reader scrolling past it to reach §19 pays
for the choice, and the file will keep growing in the half nobody reads front to back. `Governs` is
what makes that bearable rather than solved — if it stops being bearable, the thing to re-examine is
whether entries whose rule §1–18 now states can be reduced to a statement, a pointer and their
`Rejected` list, which is leaning without moving anything.

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
copy of the seventeen and nothing translates between them. Every skill additionally declares the
specification's optional `compatibility` field, naming the binary it needs. The readme says the Copilot
install has never been run, because it has not.

**Why this needed deciding at all.** The readme said packaging the skills for Copilot was not done, and
that *"guessing at a format would produce files that silently do nothing"*. That was the right call when
written and stopped being true in August 2026: Agent Plugins 1.0 is an open standard from OpenAI with
AWS, Microsoft, Cursor, Vercel and Google, with a published closed schema and six implementing clients.
There is nothing left to guess.

**Why it cost one file.** The seventeen skills were written for one host and already satisfy the open
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
session. Nobody has run `copilot plugin marketplace add` against this repository and watched seventeen
skills appear, and the gap between "conforms" and "works" is exactly where D-32 put the Azure DevOps
pipeline — so it is recorded on the same terms, in the readme, where somebody deciding whether to adopt
reads it.

**Rejected.**

- *Copying the skills into each governed solution's `.agents/skills/`* — rejected, and it was the plan
  until the standard turned up. `.agents/skills/` is the cross-client path for skills belonging to **one
  repository**; the framework is installed once and governs many, so per-solution copies would put
  seventeen directories of framework text into every adopted repository, to be refreshed by `init`
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
comparison `ApprovalLedger.IsApproved` already names; `Land` assembled the escalation sentence
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
  responses carry a field nothing prints — `Land.Response.Chain` holds which chain check failed while
  the renderer says only that the chain does not hold — and in those cases the honest reading is that
  something *should* read it, so removing the field would be deleting the evidence of a thin output
  rather than cleaning up. They are listed where the work is tracked, not quietly dropped.
- *Removing the artifact-format fields nothing reads* — rejected. `Finding.Locus`, `Finding.Remedy` and
  `Disposition.Withdrawn` are §7's documented format, written by the interrogate skill and present in
  `reference/iteration-3-cycle/`; a reader of a findings file wants them whether or not the gate does.
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

**What it cost, which is how it was found.** `reference/iteration-3-cycle/` is the worked example this
repository tells people to calibrate against, and it carries three blocking findings — one each on the
intent, the spec and the plan — raised in round one, answered in the documents, and never recorded as
answered. Rebuilt inside a governed solution and put to the current binary, its intent panel reads
`round 2 - 1 blocking` with `r1: 1 blocking - r2: clean` beside it. The cycle completed in anger and
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
gate" — and then `land` reached for `gh` or `az` with nowhere to push.

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
layer; the pre-push hook is local and `--no-verify` walks past it. So `doctor` says so on every run, the
`init` skill says it once when the choice is made, and the `land` skill says it again when somebody
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
why (`CON_13`), which is right for a database or a UI framework and wrong here, because the cost of the
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
