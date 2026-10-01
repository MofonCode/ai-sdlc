# ai-sdlc — concept

**Iteration 4.** Last updated 2026-10-01.

The original longhand draft is preserved unedited in [`CONCEPT.raw.md`](CONCEPT.raw.md).
This document is where the concept currently stands. Open questions are at the end.

Iteration 3 shipped as `0.8.3` and is archived on the `archive/v03` branch of this
repository — eleven skills, seven schemas, a hook config, eight PowerShell gates,
two sample solutions and a test suite that asserts skill behaviour by running the
skills. Where this document says a thing already exists, that is where it exists.
Iteration 4 is largely carry-forward plus making it host-agnostic.

---

## 1. What this is

A host-agnostic plugin that governs a team's software lifecycle. A change does not
travel from a request to a diff. It moves through phases, each producing a file the
next phase reads, and each file is challenged against a fixed set of rules before it
advances. Nothing advances on assertion.

Two things it exists to make possible:

- **A person approves a plan before code exists**, and the approval is recorded
  against the exact bytes that were approved.
- **Approved plans build unattended**, one or many at a time.

It has a strong opinion about the stack it scaffolds, and that opinion is swappable.

---

## 2. Principles

### 2.1 Enforcement lives below the agent

The agent is never the thing enforcing a rule. Three layers, and only the last is a
guarantee:

| Layer | Mechanism | Bypassable by | Purpose |
|---|---|---|---|
| Advisory | skill and instruction text | the agent, constantly | guidance |
| Local deterministic | the `sdlc` gate binary, a pre-tool hook, git hooks | a human with a shell | fast feedback |
| Non-bypassable | `sdlc verify-chain` as a pull-request check, plus branch policy | nobody without repo admin | the guarantee |

The promise made to a team is the third layer: **nothing merges unless the artifact
chain is complete, approved, and hash-matched.** That is host agnostic because it
runs where no agent holds credentials.

A corollary iteration 3 states and is worth keeping as a rule of its own: **a
directory that appears enforced and is not is worse than one that never pretended.**
A check that silently does nothing — a CI job with no credentials, a hook that fails
to start — is the failure mode to design against.

### 2.2 The gate is a program, not a prompt

One executable owns every process constraint and exits non-zero with the warning
text. Every skill on every host does nothing but call it. The same binary runs in
the pre-tool hook, in git hooks, and in the pipeline. One implementation, three call
sites, no drift between hosts.

**It carries the schema evaluator with it.** Iteration 3 could not: its hooks are
PowerShell 5.1, which has no `Test-Json`, so a consumer solution got a shallow
key-name check while the real evaluator ran only in this repository's own CI. That
left schema rules enforced in the one repository that does not need them — the schema
rejects a verify document claiming `pass` with an open blocking finding, and nothing
in a live cycle ever checked it. A binary that already has to ship removes the reason
for the split, so artifacts are fully validated where they are written.

### 2.3 Constraints belong in the lowest layer that can hold them

In order of preference:

1. **The compiler.** Analyzers, `TreatWarningsAsErrors`, `.editorconfig` severities,
   central package management. An analyzer is the most host-agnostic enforcement
   that exists — the build rejects the code no matter which agent wrote it, with no
   hook and no adapter.
2. **A schema or a hook.** Anything mechanical about an artifact: a required
   section, a key that must be present, a file the plan did not declare.
3. **A constraint.** Only what needs reading comprehension — whether a spec answers
   its intent, whether a criterion is testable, whether a proof reaches its claim.

So: **analyzers police the code, schemas and hooks police artifact shape,
constraints police the reasoning.** §6.2 makes this an admission test the
constraint library enforces on itself.

### 2.4 Judging and editing are separate acts

A skill that both judges an artifact and rewrites it ends up judging its own edits,
and its second opinion is no longer independent. `interrogate` judges and never
edits. `refine` and `remediate` edit and never judge.

The corollary matters more: **nothing that reacts to a failing test may edit a
planning document.** If a red test can cause an agent to adjust the spec, the
criteria become whatever the code happens to do and the spec stops judging the
build. That is the path of least resistance, not an edge case — rewording a sentence
is easier than fixing a bug — so it is enforced by the hook rather than by
instruction. See §5.1.

### 2.5 The plugin does not govern itself

ai-sdlc is not an ai-sdlc solution. If it were, a change to a constraint would be
judged by the constraint it changes, and a defect that blocks landing would also
block the fix for that defect. The process is exercised by running cycles in
`dice-trial`.

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
| `plan` | how | `plan.md` — files it will touch, blast radius, the proof committed to per criterion |
| `build` | the change itself | a diff, plus build and test evidence |
| `verify` | whether the change did the spec justice | `verify.md` — an outcome and its findings |
| `land` | integration | a pull request |

`interrogate`, `refine` and `remediate` are not phases. They are the two loops that
run inside the phases:

```
interrogate -> refine -> interrogate         (an artifact, uncapped)
verify      -> remediate -> verify           (the code, at most 3 passes)
```

Past three remediation passes the outcome is `escalated` and landing refuses it.
**The artifact loop is never capped.** On the last intent that ran eleven rounds,
round seven found that the headline change was unreachable and round eleven found
that no success criterion failed if it were skipped entirely. A cap of three would
have stopped before either.

A stage will not run until the artifact before it has an interrogation round with no
open blocking findings — see §4.4.

### 3.1 Every class of change runs the full chain

`intent` interviews the user and establishes whether this is a new solution, a
feature addition, a feature change, or a bug fix.

**Intent, spec and plan are all required, always.** The classification informs how
each artifact is written — a bug fix's spec derives its criteria from a reproduction
rather than from a desired outcome — but it never skips an artifact.

An earlier draft of this document proposed an express path collapsing intent and
spec and making the plan optional for bug fixes. That is withdrawn. Without a plan
there is no declared file set, and the declared-versus-changed comparison is the
strongest mechanism in the design; a path that removes it to save a few minutes
trades the guarantee for convenience.

### 3.1.1 Splitting an intent

Size is identified by a person, not by a rule. There is no constraint on intent size
and no automatic detection: as someone moves from intent to spec to plan and the
perceived effort grows, they call `split`.

`split` takes the intent, asks the questions needed to divide it — which outcomes
belong together, what each one is for — and writes the smaller intents as new cycles.
The original is abandoned with `reason: split into <ids>`, and each new cycle records
its parent, so the history stays traceable rather than appearing as three intents from
nowhere.

**Splitting late throws work away, and the skill says so before it acts.** After a
spec exists, splitting discards that spec; after a plan, both. That is sometimes the
right trade, but it is the person's trade to make knowingly, so `split` reports what
will be discarded and waits.

**The part that needs machinery: a split produces cycles that depend on each other.**
If an intent divides into three and the second needs types the first creates, the
second cannot be built until the first has landed. Nothing in the design expressed
that — `.sdlc/cycles/` is flat, ids are timestamps with no ordering guarantee, and the
claim graph detects that two plans touch the same file without saying which goes
first.

So `split` records an order, and the cycles carry it:

```yaml
# in the plan's frontmatter
after: [20260929_1456_32]
```

`batch-build` respects it — a cycle whose predecessor has not landed is **not
attempted** rather than failed, the same reporting §9 uses for a plan downstream of a
failure. Without this, splitting hands `batch-build` a problem it cannot see, and the
result is a confusing build failure rather than a scheduling decision.

This is the smallest thing that makes splitting safe. It is not a general dependency
system: `after` is a declaration a person made while splitting, not something inferred.

### 3.2 Artifacts

| Artifact | Holds |
|---|---|
| `intent.md` | the problem as observable today, who has it, what done looks like, fixed decisions with attribution, bounds, assumptions, and open questions |
| `spec.md` | acceptance criteria, each naming what is observable and how it is checked, each citing the part of the intent it answers; plus out of scope, assumptions, and blocked-on |
| `plan.md` | shape and departures from the profile defaults, a file table (add / update / delete, with a change note per row), order, the proof per criterion naming the tests that will exist, `Cannot prove`, and `Risk` |
| `verify.md` | an outcome, a criterion-by-criterion table citing test names out of the test results, findings, and suggestions |

**The plan holds a file table, not diffs.** The draft asked for line adds and
removes. That is not carried forward: a plan detailed to the line means the agent
already did the work, the line numbers are stale the moment any other cycle lands,
and a 900-line diff at the last cheap gate gets rubber-stamped rather than read.
Line-level change is the build artifact — the branch and `diff.patch`.

What makes the file table binding is the comparison at verify: every changed file
must be declared and every declared file must have changed, reported as `UNDECLARED`
and `UNDELIVERED`. That check is what turns the plan from a description into a
commitment, and it already exists as `Compare-DiffToPlan.ps1`.

`Cannot prove` is a declaration, not a failure. When `verify` encounters exactly
what the plan said its proof could not reach, it confirms the limit rather than
raising a finding — the finding would be raised at the plan gate, by CON_06, if the
proof were inadequate to a criterion it *did* claim.

### 3.3 Evidence

`build` records what happened, as files rather than as prose: `diff.patch`,
`build.log`, `tests.trx`, `base.sha`. `verify` reads those and nothing else, so its
verdict cites test names and diff lines rather than recollection. Where the evidence
does not reach, `verify` says so explicitly rather than implying coverage it does
not have.

`build` treats warnings as errors — enforced by `Directory.Build.props`, not by
instruction.

---

## 4. Gates and approvals

### 4.1 One approval per cycle, taken before the build

**A cycle has exactly one gate: the planning approval.** It covers the three
planning documents together — `intent.md`, `spec.md`, `plan.md` — and is taken once,
immediately before `build`. One decision, one ledger entry, three hashes.

Approving it means agreeing to all three at once: that this is the right problem,
that this is what done means, and that this approach and its blast radius are
acceptable. There is no separate intent gate or spec gate, because a person reading
a plan is reading the spec restated inside it.

Two mechanisms, defending different things:

| | Local approval | The pull request |
|---|---|---|
| Defends against | an agent that *forgets* — proceeding without a plan a person read | a human who did not look |
| Mechanism | one ledger entry, three hashes, a hook that refuses `build` without it | code-owner review of the branch |
| Timing | before any code exists | after |

**The gate is local, and only local.** There is no remote plan approval — no
artifact-only pull request, no platform review before the build. The local paste is
the approval; the code pull request is where another person looks. The ledger
carries a `framework_version`, so if a remote decision ever has to be recorded that
is a versioned format change rather than something to design for now.

A code owner merges the pull request, never the agent that authored it.

### 4.2 The approval record

**One file per cycle**, beside the other artifacts in the cycle's own directory:
`.sdlc/cycles/20260929_1456_32/approval.md`. Nothing about approval lives outside
the cycle it belongs to.

It is an **append-only ledger of decisions** — normally one entry, and more only
where a decision was revisited. A denial is an entry. A later approval supersedes
it. The gate history of the cycle reads top to bottom in one file.

```yaml
---
cycle: 20260929_1456_32
framework_version: 1.0.0
decisions:
  - decision: changes_requested
    by: Peter Almanzar
    at: 2026-09-29T01:06:00Z
    hashes:
      intent.md: 3f9a2c7b…
      spec.md:   91c4de08…
      plan.md:   7e2b5504…
    reason: the spec has no criterion for delivery as a page at all

  - decision: approved
    by: Peter Almanzar
    at: 2026-09-29T01:12:00Z
    hashes:
      intent.md: 3f9a2c7b…
      spec.md:   5b07af31…
      plan.md:   d40e1196…
    findings_rounds:
      intent.md: 2
      spec.md:   2
      plan.md:   2
    accepted_findings: [plan.F_02]
---
```

The block between the `---` fences is **frontmatter**: a YAML header carrying the
document's machine-readable facts, with the prose body below it for people. Every
artifact here has both, and the division is deliberate — schemas and gates read the
frontmatter, humans read the body, and neither has to parse the other. Frontmatter is
read with a real YAML parser and never with a regex: iteration 2 hand-rolled one, and it
reported that a skill declared a setting while the real parser had dropped every field
in that file, with 333 tests agreeing with the regex.

Each hash is of that document's own bytes, canonicalized — CRLF normalized to LF,
trailing whitespace stripped — or Windows checkouts produce phantom mismatches.

**Three hashes in one entry, deliberately.** The three documents are approved as one
act, so they are recorded as one act. If any of the three no longer matches, the
approval no longer describes what is on disk and the gate refuses — the person
re-runs the approval skill and re-approves all three. There is no partial staleness
to reason about and no cascade to define, because there is only ever one gate.

**`accepted_findings`** names the open findings a person knowingly let through,
prefixed by which document they belong to. Without it, the gate cannot tell a plan
approved *with* a known objection from one approved because the objection was
missed. It also replaces iteration 3's `waivers.md`: a waiver is an accepted finding
on the approval entry.

**Timestamps are written into the file.** A file's last-modified time cannot serve:
git does not store mtimes, so a clone or checkout sets every file's to the moment of
checkout, and on a teammate's machine or in CI every approval would read as granted
just now.

Filename-as-state (`20260929_1456_32.approved.md`) was considered and rejected: it is
no harder to forge, it carries no hash so approve-then-edit goes unnoticed, every
reference to the artifact breaks on approval, it is binary so there is nowhere to put
a denial, and it has no room for who or when.

### 4.3 No skill writes the approval. A skill only prints it.

`sdlc cycle-approval <id>` prints a ready-made entry — three hashes computed,
timestamp filled, findings rounds and open findings listed — together with
instructions. The person copies it, opens or creates `approval.md`, pastes, saves.
The plugin never writes that file.

This keeps the property that made iteration 3 trustworthy: **a human performs the
act of approval, and no skill can perform it for them.** It also makes the hook rule
absolute rather than conditional —

- **the pre-tool hook denies every agent write to `approval.md`, with no exception.**
  There is no approve command for it to carve out.

That is the highest-value hook rule in the design, and the main reason the hook layer
earns its place.

Two costs of the copy-paste flow, both manageable:

- **A pasted hash cannot be checked by eye.** The `status` panel shows the first
  twelve characters of each recorded hash next to each document's current twelve, so
  a mismatch is diagnosable by looking. When the gate refuses, it names *which of the
  three documents* changed after the text was printed and says to print it again —
  never just "hash mismatch."
- **A first entry is a file creation; a revisit is an edit.** The usual case is
  create-paste-save once. A denial followed by an approval means opening the file and
  pasting at the bottom. The printed instructions say which case applies.

The approval skill **refuses to print** while any blocking finding is open and
unlisted. What it prints instead names them, so accepting a blocker is always a
visible act rather than a silence.

### 4.4 Structural checks hold the planning stages

With one gate before the build, the planning stages are held together mechanically
rather than by review. These carry real weight — they are the only thing standing
between a stage and the next one.

**A clean interrogation round is required to advance.** `spec` will not run until
`intent.md` has a round with no open blocking findings, and `plan` will not run
without the same on `spec.md`. Interrogation is part of the chain, not an optional
extra — which is what keeps a single approval from resting on three documents nothing
has argued with. A blocking finding may be cleared by refining the artifact or, where
a person decides to accept it, by naming it in the approval entry; it may not be
cleared by ignoring it.

The remaining checks need no reading comprehension at all:

- every intent statement maps to at least one spec criterion;
- every spec criterion maps to at least one file **and** at least one named test in
  the plan;
- every changed file is declared in the plan, and every declared file changed.

---

## 5. Interrogation and findings

`interrogate` runs against one artifact in fresh context, judges it against the
constraints in force, and writes a findings round. It never edits the artifact.

**The fresh context is a fresh process, on every host** — the host's own
non-interactive mode (`claude -p`, `copilot -p`). A new process is a new context, it
behaves the same everywhere, and iteration 3's `run` already drives each step this
way. Subagents would be more ergonomic on Claude Code and would mean two code paths
for the most important skill, so they are not used.

What this buys is **independence from investment bias**: the reader never saw the
interview, the drafts, or the author's reasoning — only the artifact, its upstream,
and the constraints. What it does not buy is independence from **model bias**: same
model, same priors, so a borderline judgement flips the same coin twice. A quorum of
readers would address that, at a multiple of the cost, and is not in v1.

Findings are data, at `findings/<artifact>.r<round>.json`:

```json
{
  "framework_version": "1.0.0",
  "cycle": "20260929_1456_32",
  "artifact": ".sdlc/cycles/20260929_1456_32/spec.md",
  "interrogated_at": "2026-09-29T01:08:39Z",
  "round": 1,
  "findings": [
    {
      "id": "F_01",
      "severity": "blocking",
      "disposition": "open",
      "cites": "CON_04.1",
      "statement": "…what is wrong, in full",
      "locus": "…where in the artifact",
      "remedy": "…what would fix it"
    }
  ]
}
```

An empty `findings` array is a clean round.

| Severity | Meaning |
|---|---|
| `blocking` | stops the artifact advancing; cites exactly one constraint |
| `suggestion` | noticed, does not block; cites a constraint |
| `advice` | improvement that violates nothing and cites nothing |

`advice` is new in iteration 4. Every finding previously had to cite a constraint,
which left no home for "this would be better as X."

| Disposition | Meaning |
|---|---|
| `open` | outstanding |
| `answered` | the artifact changed to address it |
| `waived` | accepted at the gate, recorded in `accepted_findings` |
| `withdrawn` | retracted by a later round |

Disposition transitions carry who and when. Without that, the fact that a person
accepted a finding at a gate exists nowhere but prose — which is the state iteration
3's trial cycle is in: every disposition in it reads `open`, including one the
findings themselves describe as carried forward unchanged.

**Rounds accumulate and are never rewritten.** A per-artifact summary — highest
round, count of open blocking findings — is what the gate reads, so no consumer has
to glob and merge rounds and reach its own conclusion.

**Finding ids are unique per artifact for the life of the cycle.** A finding carried
into a later round keeps its id; a new finding takes the next one. Without that,
`F_01` in round 2 may be either a new objection or an old one, and only prose
distinguishes them.

**How many rounds is the owner's call, and there is no cap.** The panel reports what
each round found and the cost of a round. It does not recommend stopping on
diminishing-returns grounds; that judgement has been wrong before, twice, and
expensively.

### 5.0 The self-healing loop

One command writes an artifact, interrogates it, works its own blocking findings, and
interrogates again — reporting once at the end. Approval stays manual throughout. It
runs for **all three planning artifacts**, intent included.

This needs no skill to invoke another skill: the constraint set reaches the skill by
the same command everything else uses (§6.3), so a skill can judge itself against the
same rules in process.

It makes a **best effort to reach a clean round**, and every finding has exactly three
exits:

| Exit | When | Where it shows |
|---|---|---|
| **healed** | the loop can fix it from information already present | the artifact changes; the report names what changed |
| **assumed** | the loop must supply something to proceed | the artifact's **Assumptions** section, naming what was assumed and which finding prompted it |
| **asked** | the loop cannot proceed without a person | the artifact's **Open** section, and the loop stops and asks |

**Assumptions are recorded in the artifact, not in a log.** This is the mechanism that
makes the owner a real backstop rather than a nominal one. A `verify`-style report is
read once; a section in the artifact is read every time the artifact is read, and it
is what a reviewer's eye lands on at the gate. Each entry says what was assumed and
what prompted it, so accepting it is a decision and correcting it is one edit.

**Why that matters more than it sounds: a healed finding produces a clean round.** If
round one objects that a figure was never measured and the loop quietly softens the
sentence, round two is clean and the person reviewing sees nothing to review. Recording
the assumption is what keeps it visible. So the rule is not "never assume" — it is
**never assume silently.**

**What cannot be assumed into existence.** An intent is written by interviewing a
person. Given "as a player I would like to roll a die" there is no problem, no who, no
done-looks-like and no must-not-change. A loop that invents them produces `CON_02`'s
exact failure, with every later phase treating the invention as approved. So an
unmeasured figure may be recorded as an assumption or raised as a question — it may
not be reworded into something vaguer that passes. Rewording is the one move that
removes the problem from the page while leaving the belief in it.

Spec and plan carry no such limit: both derive from an approved upstream artifact, so
the information exists and healing is rewriting rather than inventing.

**A stage may run with a non-blocking prior artifact.** Open questions and recorded
assumptions do not block the next stage — they travel forward, and keeping them aligned
with what was actually intended is the owner's job at the gate.

#### Why this terminates without a round cap

The loop is bounded by having a fixed target: an artifact that violates no constraint.
Blocking findings are the only thing acted on, each heal removes one, and the
constraint set is finite — so a loop that only ever closes blockers converges.

**Suggestions are never acted on automatically, and that is what keeps it finite.** A
suggestion has no satisfaction condition; there is always a further improvement, so a
loop chasing suggestions would never finish. They accumulate for `sdlc suggestions` and
a person decides.

**The one case that does not converge, and it is not hypothetical.** Two constraints
whose remedies conflict: closing one opens the other. It happened in the trial cycle.
The spec needed a criterion for demonstrable impartiality — `CON_04`, cover every
outcome the intent asks for — and writing one required a sample size and a tolerance
the intent did not contain. Supplying them violates `CON_02`; omitting the criterion
violates `CON_04`. A loop would flip between the two indefinitely.

`CON_04` against `CON_05` has the same shape: closing a coverage gap by adding a
criterion can overshoot into scope the intent never asked for, and deleting it reopens
the gap. The two are deliberately separate constraints, so the conflict is structural
rather than a bug.

**What actually resolved it in the trial is the third exit.** The spec recorded the
criterion under `Blocked on` and named exactly why neither move was available. Not
healed — handed to a person. So the loop terminates because every finding has an exit
that is not "try again", and detecting a finding that reappears after a heal is how it
knows to take the third one.

Three endings have to be distinguishable in the report: **converged**, **open —
assumptions or questions recorded**, and **conflicting — two constraints whose remedies
exclude each other, named**. None of them is a round count, and a person may run as
many further rounds by hand as they like.

### 5.1 `refine` and `remediate` — two editors with opposite guards

Applying a remedy is editing, not reasoning: the finding already carries one. What
makes these skills rather than freehand edits is the bookkeeping — dispositions move
from `open` to `answered` and what changed is recorded — plus a bound on how far the
edit may reach, a pass counter for the code loop, and identical behaviour on every
host.

They are two skills rather than one with a mode, because their guards are opposites
and a single tool with a conditional is one mistake away from inverting the system:

| | `refine <id> <artifact>` | `remediate <id>` |
|---|---|---|
| Runs after | `interrogate` | a blocked `verify` |
| May edit | the one named artifact | source and tests |
| May never edit | source, tests, `approval.md` | `intent.md`, `spec.md`, `plan.md`, `approval.md` |
| Capped | no | three passes, then `escalated` |
| Also refuses | once the cycle is approved — the hash would silently break, so re-approval is the honest path | — |

Both guards are hook rules on file paths, not instructions in a prompt.

A person editing an artifact by hand needs neither skill. The skills exist for when
the loop should be recorded.

---

## 6. Constraints

A constraint is a rule an artifact is judged against — one that needs reading
comprehension to apply. Every blocking finding cites exactly one.

### 6.1 Every constraint says what it forbids and why

Four parts, and all four are required. A constraint missing any of them is invalid
rather than merely untidy, and a test fails on it:

| Part | Holds |
|---|---|
| **name** | the id and a one-line statement of the rule |
| **Applies to** | which artifact, and at what moment — "an intent, at approval"; "a diff, at verification" |
| **Forbids** | what becomes illegal, named concretely enough that an example could be written |
| **Why** | what goes wrong if the rule is absent — the cost it avoids, not a restatement of the rule |

The **Why** is what makes a constraint arguable rather than arbitrary. It is what a
reader has six months later instead of the conversation that produced it, and it is
what tells a person at a gate whether accepting a finding is cheap or expensive in
this particular case. Iteration 3 already carries one on every constraint; iteration
4 makes it structurally required.

`Forbids` may not restate the statement. "An intent states the problem, not the
solution" forbids *"an intent whose body names an implementation and no observable
problem — 'add a cache', 'make the export asynchronous' — with nothing said about
what is wrong today."* The first is the rule; the second is what becomes illegal.

### 6.2 What is admitted to the library

Three tests, all of which must pass:

1. Its `Forbids` clause names something that becomes illegal.
2. **No analyzer, schema or hook could decide it.**
3. It is incompatible with at least one plausible implementation.

Rules about code go to analyzers — and where an existing analyzer already decides
it, that analyzer is used rather than a rule written. Mechanical rules about
artifacts go to schemas or hooks. A rule that survives every implementation is
decoration however true it sounds, and `CON_11` exists to keep the list from
becoming that. It applies to itself.

**Stack neutrality.** Constraints bind solutions on any stack, so none may name a
language, framework or package. Stack specifics live in the profile and in
analyzers. This is also what lets the whole library survive the move to other hosts
untouched.

### 6.3 Tiers

| Tier | Id | Lives in |
|---|---|---|
| global | `CON_nn` | the plugin's constraint library |
| profile | `PRO_nn` | the stack profile package |
| solution | `LOC_nn` | `.sdlc/constraints.md` |

Resolution is global, then profile, then solution. The effective set is rendered on
demand by `sdlc constraints` rather than copied into a solution — a local copy goes
stale and then disagrees with the live set, with nothing to say which one binds.

**That command is also how the set reaches a session, on every host.**
`sdlc constraints --for <artifact>` prints the effective set, and running it is the
first step of every skill that judges or writes an artifact. One source, one format,
identical everywhere.

Per-host native delivery — Claude Code load-time injection, a Copilot instructions
file, a Gemini context file — is an optimisation layered on top where a host supports
it, never the mechanism. Keeping the set in one place and rendering it on demand is
what stops the three hosts drifting into three slightly different rule sets, which is
the same failure as a stale local copy with more ways to happen.

### 6.4 Ids, and revisions

**Ids are a monotonic sequence.** Assigned once, never reused, never backfilled. A
removed constraint leaves a gap, and the gap plus the commit that made it is the
record.

**A change to what is forbidden is a new constraint.** The old one keeps its id and
gains `Superseded-by`. It stays on the page, verbatim, so a finding that cited it
remains readable against the rule it was actually judged under.

**A change to the wording alone is a revision.** `CON_01` becomes `CON_01.2` — same
`Forbids` clause, better words. Findings cite the revision: `cites: CON_01.2`. A
round judged under older wording stays distinguishable from one judged under newer
wording, which is the whole point.

So two kinds of change with two different mechanisms:

| Change | Mechanism | Example |
|---|---|---|
| the words improve, the rule is identical | revision — `CON_01.2` | tightening a vague phrase |
| what is forbidden changes | new id, old one `Superseded-by` | widening a rule to cover a new case |

The cost, accepted: every citation is three characters longer and the schema's
`cites` pattern has to admit the revision suffix.

### 6.5 The eleven carried forward

All eleven of iteration 3's global constraints come across, and `CON_12` joins them.
They are stack-neutral
by rule, each has a `Forbids` clause, and each is about an artifact rather than code
— so the host and profile work does not touch them.

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
| `CON_10` | a waiver's reason says why the finding is acceptable | a waiver |
| `CON_11` | every constraint carries a Forbids clause that rules something out | a diff adding a constraint |
| `CON_12` | a criterion's check is one the solution's testing layers can run | spec |

**`CON_12` is the first constraint added in iteration 4**, and it demonstrates §6.4's
rule in use: it is a *new* id rather than a revision of `CON_03`, because it changes what
is forbidden rather than how `CON_03` is worded. A device-only criterion can fail and can
be observed, so `CON_03` admits it; `CON_12` does not.

> **`CON_12` — a criterion's check is one the solution's testing layers can run.**
>
> **Applies to:** a spec, at approval.
>
> **Forbids:** a criterion whose only named check is a person observing something — on a
> device, in an app store, on hardware the suite cannot reach.
>
> **Why:** `verify` derives its verdict from `tests.trx` and `diff.patch`, and `CON_08`
> forbids a claim with no cited evidence. A criterion with no machine-runnable check has
> no entry in the test results, so admitting one forces a choice between `verify`
> adjudicating on attestation and `pass` no longer meaning every criterion was met. The
> outcome belongs in the intent as a bound, verified by a person outside the cycle, where
> it is honest about not being governed.

Three notes on carrying them:

- **`CON_06` is the one to watch.** It drew a blocking finding in roughly half of
  twelve runs on a golden sample. The cause was traced to the sample spec rather
  than to the constraint, and the fix was made by reading rather than by re-running
  — so whether the flake is gone is unconfirmed.
- **`CON_10` needs rewording.** Waivers are now accepted findings on the approval
  entry, not a `waivers.md`, so the rule binds the `reason` on an approval decision.
- **`CON_04` and `CON_05` stay separate.** A gap and creep fail differently and are
  remedied differently — a gap sends you back for more criteria, creep sends you to
  delete one — so they are two constraints rather than one about correspondence.

**Nothing yet constrains intent size**, and it is the known gap. Size matters
because a cycle lands whole or not at all, so cycle size decides how much work one
unmet criterion holds hostage, and a plan touching sixty files cannot be read — at
which point approval degrades into signing. The test is not how many outcomes an
intent has, since `CON_04` requires the spec to cover all of them, but whether **one
reviewable change could prove them all.**

### 6.6 Where the "must be provable" principle lives

The rule that nothing enters an artifact unless it can be proven is not one
constraint. It is four, one per artifact, each naming what becomes illegal in that
specific place:

| Artifact | Constraint | What it forbids |
|---|---|---|
| intent | `CON_02` | stating as settled fact a figure, cause or behaviour nobody has measured |
| spec | `CON_03` | a criterion that cannot fail, or an outcome with no way to observe it |
| plan | `CON_06` | a named check that could pass while its criterion is unmet |
| verify | `CON_08` | a statement with no cited evidence, or citing evidence that does not establish it |

A single general rule would read well and forbid nothing in particular, which is
what `CON_11` exists to reject. Four specific rules each fail a real artifact.

---

## 7. The solution's own files

Three files in `.sdlc/`, and the three-way split is by **mutability**, which is a
sharper line than facts-versus-rules:

| File | Changed by | Protection |
|---|---|---|
| `constraints.md` | a person, by hand | the hook denies every agent write, unconditionally |
| `techstack.md` | a cycle whose plan declares it | none beyond the ordinary declared-file gate |
| `solution.md` | `init`, once | the hook denies agent writes after creation |

### 7.1 `constraints.md` — protected, hand-edited

The solution's own `LOC_nn` constraints. The hook denies the agent any write to it,
and a person edits it directly — the same shape as `approval.md`, with the same
absolute rule and nothing to toggle.

**A configuration flag was considered and rejected.** A boolean saying "this file is
unlocked now" is not protection: an agent that can write files can flip the flag and
then edit the file, and protecting the flag needs a flag. There is also no special
cycle type for protected changes, because a cycle judged by the constraints it is
changing is the same deadlock as the plugin governing itself (§2.5).

### 7.2 `techstack.md` — a strong default, not a rule

What this solution is built from, and what a plan builds with. It is **not
enforced**: no global constraint may name a language, framework or package, so
nothing here binds by itself. A solution that wants a default to bind adds a `LOC_`
constraint or an architecture test.

**A plan may depart from any default. Silence about departing is what is illegal** —
a departure argued in the artifact a person approves is a decision; an unexplained
one is a habit. That is the whole of what "high-weighted suggestion" means
mechanically.

Because it is an ordinary governed file, changing it requires a cycle whose plan
declares it. The process governs its own stack changes through the normal mechanism,
with no new machinery.

### 7.3 `solution.md` — does it earn its place?

**Asked directly: yes, and it is two keys.** It was three; `adopted_at` is dropped.

| Key | What consumes it |
|---|---|
| `profile` | `init` and `plan`, to resolve which profile package applies — must be machine-readable, and prose in `techstack.md` is not that |
| `origin` | `intent`, to know whether "this repository holds no solution at all" is a true problem statement or a false one |

The honest case for keeping the file is **mutability, not content.** `origin` and
`adopted_at` are historical facts: a sha does not become a different sha, and a
repository that had code at adoption always did. Folding them into `techstack.md`
would put an immutable fact inside a file that a cycle is allowed to edit, and then
an agent with a plan declaring `techstack.md` could rewrite the record of what
predated governance. Keeping them in a file nothing writes after `init` makes that
structurally impossible instead of merely discouraged.

`profile` is the one key that can legitimately change — a solution migrating from
`dotnet-default` to `mofoncode` — and it changes through a cycle that declares
`solution.md`, which is the one exception to "nothing writes it after init."

**`adopted_at` is dropped, and the evidence is unambiguous.** Searching iteration 3
for every use: `init` writes it, the schema requires it when `origin` is `adopted`,
three documents mention it, and two tests assert the schema. **No skill, tool or hook
ever reads it.** It was a write-only field validated into existence.

The reason it was never needed: findings are raised against a diff, and a diff
contains only the change — so code that predates adoption is invisible to an
interrogation whether or not a boundary is recorded. The rule it existed to support
("existing code doing this is not a finding; a change that adds another one is") falls
out of looking at diffs, with no sha required.

**There is deliberately no baseline field.** Constraints govern the change, never
code that predates adoption, so an existing repository is not in violation on its
first day and there is nothing to record. Recording existing problems is analyzer and
refactoring work with its own tooling, and it is not this framework's business.

---

## 8. `init`

Adopting a process is a decision about how a team works, so `init` is human-invoked
only and never model-invoked.

### 8.1 It refuses two situations

- **Already adopted** — `.sdlc/` exists. Re-scaffolding would overwrite the
  solution's own declarations and its local constraints, the two things here that are
  nobody else's to write.
- **Not a git repository.** Without git, `build` cannot record the diff that is the
  only evidence `verify` derives from, and the gates key on a `cycle/<id>` branch that
  cannot exist — so every hook permits everything while the artifacts still look
  governed. It says so and names the two commands that fix it.

### 8.2 It interviews, then proposes

New in iteration 4. `init` asks what the solution is about and what matters in it,
and from **the answers** proposes an initial `LOC_` constraint set for the person to
accept, edit or decline.

**From the answers, not from the codebase.** Reading an existing repository to decide
what to forbid would make adoption a judgement on code that predates the framework,
and adoption is free by design — constraints govern each change as it is made. The
interview is where a person's own rules come from; the code is not.

Proposed constraints are printed for the person to put into `.sdlc/constraints.md`
themselves, because that file is hand-edited by rule (§7.1).

### 8.3 It derives the stack only where the stack is a fact

| `origin` | Where `techstack.md` comes from |
|---|---|
| `scaffolded` | the profile's defaults — the stack is being chosen, not discovered |
| `adopted` | read from the repository — target frameworks, package references, project types — then shown to the person for correction |

`origin` itself is decided by `git ls-files`, and it is **about code, not commits**:
a repository holding only an empty commit, a README or a licence is `scaffolded`.
Getting this wrong writes a false claim into a file every later cycle reads.

### 8.4 `AGENTS.md`

Lean, and under 200 lines — it loads into context every session, and a longer file
both costs more and is followed less. It holds:

- a short summary of what the solution is;
- **references** — a relative path plus a one-line description of what that file
  contains — to `techstack.md`, to the build and test procedure, and to how the
  application is run locally;
- a repo map.

Two things it must never contain:

- **Constraints.** Global ones live in the library, local ones in
  `.sdlc/constraints.md`. A third copy here would be a source that disagrees with
  both.
- **Anything that goes stale the moment a cycle runs.** Not "the repository is
  empty", not "delete this paragraph once the build exists". An instruction that
  depends on someone remembering to remove it outlives the truth it described, and is
  loaded into every session meanwhile.

**If an `AGENTS.md` already exists, `init` prints the leaned version and leaves the
file alone.** Rewriting a repository's instruction file is the one destructive thing
adoption could do, and the print-don't-write pattern is the same one the approval
uses.

### 8.5 It commits what it created

Adoption is not a change to be reviewed; it is the boundary every later cycle is
measured from.

This matters more than it looks. An uncommitted `AGENTS.md` is untracked when a
cycle takes its diff, so the diff records the whole file as new — and a plan that
says it *changes* that file can never match what the diff shows. The first cycle then
fails `CON_07` for something adoption did rather than anything the cycle chose.

### 8.6 It creates no empty directories

A cycle's directory is made by the skill that writes the first artifact into it. An
empty directory needs a placeholder to survive git, and nothing ever removes the
placeholder once real files arrive.

---

## 9. batch-build

A local skill, executed rather than subscribed to. It collects the approved cycles in
**this** working copy and builds them together, concurrently where that is safe.
There is no shared queue: approved plans are not published, and no other machine's
work enters the batch.

A local cycle index persists state — which cycles exist, their stage, and the outcome
of the last batch — so a run can be resumed and read. The index is local and is not a
coordination mechanism.

Because claims are declared in `plan.md` and plans live in the repository, a
cross-person collision report stays available later as a read over other cycle
branches, without a shared store. Nothing needs designing for that now.

**It schedules around collisions rather than resolving them.** Plans declare the
files they claim, so a collision is known before anything is built — which is the
cheap moment. Resolving a merge conflict afterwards is the expensive moment, and the
worst place to spend an agent's judgement, because by then both sides' intent has
been compressed into text.

1. Build the claim graph from the approved plans, and read each plan's `after`.
2. Hold back any cycle whose declared predecessor has not landed — reported as **not
   attempted**, never as failed.
3. Partition the rest into non-overlapping groups.
4. Run each group in parallel, each in its own git worktree.
5. Serialize overlapping plans, rebasing each on the previous result.

`after` is how a split's ordering reaches the scheduler (§3.1.1). The claim graph says
two plans touch the same file; `after` says which goes first.

`resolve-conflict` remains, scoped to files that cannot be claimed exclusively, and
those are enumerated rather than discovered: `.csproj` and
`Directory.Packages.props`, dependency-injection registration, `appsettings`, EF Core
migrations, Terraform files near state.

**EF Core migrations serialize.** Two branches that each add a migration snapshot the
same parent model, and the merged chain is broken in a way that compiles and then
fails at deploy. Either migrations are serialized, or the migration is dropped and
regenerated from the merged model after merge.

**Partial batches land.** Successes are landed rather than discarded when a sibling
fails. One thing has to be right for that to be readable: a plan downstream of a
failure in the claim graph is reported as **not attempted**, not as failed.

**Concurrency is capped by what a plan needs.** The claim graph carries a
needs-backing-services bit per plan, derived from whether the plan touches the app
host or declares service dependencies. Service-free plans run wide; plans that stand
up SQL Server, Redis and a Service Bus emulator run under a low cap, because
isolation does not make three database containers fit in memory.

### 9.1 Local run modes

Two modes that want opposite things, selected by an environment variable that
batch-build sets:

| | local dev | test / batch |
|---|---|---|
| host ports | stable, so a URL can be bookmarked | dynamic, assigned |
| container names | stable | `{app}-{shortGuid}` |
| volumes | persistent, named | ephemeral, or named per run |
| dashboard and OTLP endpoint | on, fixed | off, or overridden per run |
| databases | persistent | named per run |

**Named volumes are a worse collision than ports.** Two concurrent app hosts calling
`WithDataVolume()` share one volume — the same database files, two writers — which
fails strangely rather than loudly. The run id therefore belongs on container names,
volume names and database names, not only on ports.

Tests discover endpoints through `Aspire.Hosting.Testing` rather than knowing a port
number. Without that, dynamic ports only move the problem into test configuration.

`land` is what touches the remote: it pushes, opens the pull request, and links the
work item with the intent and spec details. batch-build never pushes.

---

## 10. Tech stack and profiles

Strongly opinionated, and the opinion is a swappable **profile**.

### 10.1 The default profile — `dotnet-default`

| Concern | Choice |
|---|---|
| language | C# |
| framework | .NET |
| unit and integration testing | MSTest on Microsoft.Testing.Platform |
| component testing | bUnit, for Blazor components |
| end-to-end testing | Playwright, against Chromium and WebKit |
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

**Shared UI is Blazor components in a Razor class library**, hosted by Blazor for web
and MAUI Blazor Hybrid for desktop and Android. The draft named MAUI for web; MAUI
does not target web. Keeping the shared layer at the component boundary is what makes
the ask for shared code real, and it keeps the outer shell replaceable.

**Service Bus and SignalR are not alternatives.** Service Bus is asynchronous
messaging between services; SignalR is server-to-client push. Most solutions want the
first, the second only when there is a live UI to push to.

**An Aspire AppHost is conditional, not automatic.** More than one process, or one
process plus a backing service a developer would otherwise start by hand — then it
earns its place and brings emulators rather than live Azure resources. A single
executable with no backing services does not get one: that is an extra project, an
extra launch profile and a slower start for no orchestration, and Aspire can be added
later without restructuring.

**`dotnet test` needs two settings or it does not run the suite.** On the .NET 10 SDK
`dotnet test` defaults to VSTest and Microsoft.Testing.Platform refuses that path, so
the profile ships both: `"test": { "runner": "Microsoft.Testing.Platform" }` in
`global.json`, and `<EnableMSTestRunner>true</EnableMSTestRunner>` in
`Directory.Build.props` rather than in a single test project. **Do not set
`TestingPlatformDotnetTestSupport`** — it is the .NET 9 mechanism, legacy on 10, and
setting it instead is what left a trial run unable to run `dotnet test` at all, so the
documented command and the working command were different commands.

On results: **ErrorOr** is the more actively released library and the common
recommendation for greenfield work. FluentResults is kept deliberately — it is not
deprecated, its multi-error accumulation is exactly the bulk-validation case
FluentValidation produces, and it is what the MofonCode kernel is built on.

A note recorded rather than buried: **ML.NET costs real capability** against the
Python ecosystem on competitive modelling work. Name that cost when a solution's core
value is model quality rather than integration.

### 10.2 The MofonCode profile

**Private.** Its own repository, published to an Azure Artifacts feed only the team
can restore. The public plugin ships `dotnet-default` and nothing house-specific, so a
public user never sees this profile and never needs credentials for it — the profile
mechanism simply has to support a configurable package source, which it needs
regardless.

It adds `MofonCode.Core` — a minimal mediator, `Result<T>` handlers over
FluentResults, `TraceResult` over OpenTelemetry activities, pipeline behaviours,
composable specifications, throttled concurrency, and **analyzers that keep a vertical
slice in shape** — plus the conventions that go with it:

- **an operation that can fail returns `Result` or `Result<T>`**, and a failed result
  becomes the response at a boundary rather than being converted back into an
  exception. Do not wrap an operation that cannot fail: `Result<int>` around
  `Random.Shared.Next(1, 7)` is a type that is always success.
- **commands and queries go through the mediator** when there is an operation to
  dispatch — more than one step, a decision, a validation. A single infallible
  expression called from one page is not an application operation.
- **one folder per feature, not one per layer.** Where a framework dictates a
  location, let it have that one file and keep the rest of the feature together.
- **name the seam and the type.** A plan that says only "use MofonCode.Core" has
  chosen nothing.

**No MediatR**, because the kernel has a mediator, declared in `techstack.md` so an
agent does not reach for it out of habit.

**Correction, from reading the package: `MofonCode.Core` ships no custom analyzers.**
There is no analyzer project, no diagnostic ids, and no source generator — seventeen
source files and nothing else. What it ships instead is an **analyzer policy** in
`Directory.Build.props`, and that policy is worth taking wholesale (§10.4). So the
three conventions above are **prose, enforced by nothing**: nothing today decides
results-not-exceptions, mediator dispatch, or one-folder-per-feature.

Per §6.2 that leaves each of them split between a mechanical half and a judgement
half, and only the judgement half may be a `PRO_nn` constraint:

| Convention | Mechanically decidable | Needs judgement |
|---|---|---|
| results, not exceptions | a handler's public methods return `Result`/`Result<T>` | whether an operation can fail at all — "do not wrap what cannot fail" |
| dispatch through the mediator | a page or endpoint injecting an application service directly | whether there is an operation to dispatch, or one infallible expression |
| one folder per feature | a type's folder matches its feature; no `Services/`, `Models/`, `Handlers/` split | where a framework dictates a location |

One-folder-per-feature is almost entirely mechanical and is the best candidate for the
first custom analyzer. The other two have a real judgement half that no analyzer
reaches.

**Custom analyzers are developed in the profile that declares the convention**, not in
`MofonCode.Core`. A convention and the analyzer that enforces it belong together: the
`mofoncode` profile ships the analyzer for a `mofoncode` rule, `dotnet-default` ships
the analyzer for a stack-wide one. The kernel stays a kernel, and a profile that states
a rule is the thing that can also decide it.

`MofonCode.Core` is `0.1.0` and depends only on FluentResults and the
`Microsoft.Extensions.*.Abstractions` packages — no EF Core, no SQL client, nothing
that decides a data story on a solution's behalf. Pin it: every solution taking it
inherits its breaking changes.

### 10.2.1 Commands and queries are the default, and `origin` decides whether that binds

**An application operation is a request and a handler.** Prefer it over a method on a
service reached by direct injection, in what the plugin scaffolds *and in the plugin
itself* — the gate binary's own surface is a set of requests (`verify-chain`,
`constraints`, `cycle-approval`) and each is a handler, so the tool is built the way it
asks solutions to be built.

It still earns its place rather than being automatic: more than one step, a decision, a
validation, something worth a behaviour around it. A single infallible expression called
from one place is not an application operation, and routing it through a request and a
handler leaves three files where one line was the whole of it.

**And this is where `origin` finally does work.** A default only binds a solution that
is being shaped:

| `origin` | What the profile's patterns mean |
|---|---|
| `scaffolded` | the defaults bind — commands and queries, results over exceptions, one folder per feature |
| `adopted` | **the existing standard wins.** Where a solution already has a pattern for application operations, the profile defers to it |

That is a general rule, not one about dispatch: the profile proposes a shape for new
work and never rewrites the conventions of a codebase that predates it. Adopting the
framework does not mean adopting a refactor, which is the same reason adoption reads
nothing to judge it (§8.2). A plan that *does* want to move an adopted solution onto a
profile default argues for it like any other departure.

### 10.3 Testing layers, and which one may prove a criterion

Four layers, and the question that matters is not which tools exist but **which layer a
given criterion needs** — because `CON_06` forbids a proof that could pass while its
criterion is unmet.

| Layer | Tool | Proves |
|---|---|---|
| unit | MSTest on Microsoft.Testing.Platform | a function's behaviour in isolation |
| component | bUnit | a Blazor component renders and reacts, with no browser |
| integration | `WebApplicationFactory<T>`, and `Aspire.Hosting.Testing` where there is an app host | the composed application answering a real request, over its own interface |
| end to end | Playwright, in Chromium **and** WebKit | what a person sees and does in a real browser engine |

**The rule for choosing:**

- **A criterion about what the server produces** is proven by requesting it and parsing
  the response. The trial cycle's "the roll is delivered as a page a player can open"
  was proven this way — an HTTP request through `WebApplicationFactory`, the body parsed
  as a document, the value found in rendered text. No browser needed, and a browser
  would have added minutes and flake for nothing.
- **A criterion about what happens *in* the browser** — interactivity, client-side
  state, a render that only occurs after hydration, anything a user clicks — cannot be
  proven by parsing a response, because the response is the input to the behaviour
  rather than the behaviour. That is where Playwright is required rather than optional.
- **Integration tests use real backing services, not in-memory substitutes.** The EF
  Core in-memory provider does not translate SQL, so it passes queries that a real
  database rejects — which is exactly a proof that could pass while its criterion is
  unmet. Aspire's containers are already the local story; tests reach them through
  `Aspire.Hosting.Testing` rather than hard-coded connection strings, and reset state
  between tests rather than sharing it.

**Coverage is collected across all layers** and displayed, per §12 — an integration test
covering a line counts the same as a unit test covering it.

### 10.3.1 There is no device end-to-end layer, deliberately

**Appium is not in the stack.** Not scheduled, not optional — absent. Three reasons,
and the first is the one that decides it:

- **A flaky proof is worse than an honest gap.** A criterion whose test fails at random
  gets rerun until green, which teaches a team to ignore red — and the framework's
  entire claim is that nothing advances on assertion. A scheduled device suite nobody
  can run locally is §2.1's failure exactly: it looks enforced and is not.
- **The prerequisite cost lands on everyone.** An Android SDK and an emulator or a
  device, on every machine running cycles and in CI, for a capability most cycles never
  touch. That breaks the adopt-with-one-command property.
- **MAUI's own device-test tooling is not packaged for consumers**, so what shipped
  would be scaffolding maintained by this project rather than something inherited.

**Two cheaper layers cover most of what it would have bought.** MAUI Blazor Hybrid hosts
the *same* Blazor components in a WebView — Edge WebView2 on Windows, Chrome WebView on
Android, WKWebView on iOS. So the components are tested once with bUnit, and Playwright
runs them in **Chromium and WebKit**, which are the two engines the hybrid targets
actually use. The second engine is the deliberate part: it costs a line of configuration
and it is the only thing standing between the suite and an iOS-only rendering
difference. Firefox is not included — no MAUI host uses Gecko, so it buys nothing here.

**What remains genuinely unproven is the native shell**, and only that: platform
permissions, app lifecycle, deep links, native file pickers, platform chrome. That is a
far smaller gap than "no device testing", and naming it precisely is what makes it
manageable.

**A device-only outcome is a bound in the intent, never a criterion in the spec.** It is
verified by a person outside the cycle. The reason is `verify`'s evidence model: it reads
`tests.trx` and `diff.patch` and nothing else, and `CON_08` forbids a claim with no cited
evidence. A criterion only a person on a phone can observe has no entry in the test
results, so admitting it would mean either `verify` adjudicating on attestation, or
`pass` no longer meaning every criterion was met. Both cost more than the gap does.

The consequence, stated plainly: **MAUI-targeted work has some outcomes this framework
does not govern.** They are recorded in the intent as bounds, and a plan's `Cannot prove`
names them where they touch the change. `CON_12` is what keeps them out of specs.

If device coverage is ever needed, the realistic route is a cloud device farm rather than
local Appium — a cost and an external dependency, and a later decision.

### 10.4 The analyzer policy every profile ships

**CI builds carry no warnings.** Taken verbatim from `MofonCode.Core`, because it is
the part of that package that is genuinely enforcement, and it is stack-wide rather
than house-specific — so it belongs in `dotnet-default`, not only in the private
profile:

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

Three things about it matter as much as the settings:

- **`WarningsNotAsErrors` is deliberately empty**, so an exception is one visible line
  in a file a reviewer reads rather than an invisible default.
- **Every suppression lives in `Directory.Build.props` with its reason**, never as a
  `#pragma warning disable` in source. `MofonCode.Core` holds three — `CA1716`,
  `CA1000`, `CA1848` — each with a paragraph saying why, and zero pragmas anywhere in
  its seventeen source files. A reviewer can therefore read the complete list of
  exceptions in one place.
- **`EnforceCodeStyleInBuild` makes formatting a build concern**, which is what keeps
  a formatting gate in CI from being the thing that fails a cycle after its work is
  done.

The no-pragma rule is itself mechanically checkable — a build-time check that fails on
`#pragma warning disable` in source — so it is a hook or an analyzer, never a
constraint.

### 10.5 What a profile is

**Templates plus constraints.** A profile declares its package set and versions, its
analyzer set, `Directory.Build.props` and `Directory.Packages.props` fragments,
project templates, app host run-mode defaults, pipeline templates, and its own
`PRO_nn` constraints.

Profiles are distributed as **NuGet packages** — versioned, restorable,
Microsoft-first, and identical to consume from nuget.org for the public profile and
from an Azure Artifacts feed for the private one.

**Versions are resolved at `init` and recorded, never guessed in a document.** The
rule iteration 3 set: resolve the latest stable release compatible with the target
framework, write it into `Directory.Packages.props`, and let the diff record exactly
what was taken.

A profile's defaults have to reach a plan, and a session working inside a governed
solution cannot read a file that lives in the plugin — so the defaults are
**extracted and injected** into the plan rather than read. Iteration 3 does this with
a marked region in its tech stack document and `Get-StackDefaults.ps1`; iteration 4's
profile package is that region, versioned.

---

## 11. Hosts

Every host under consideration can deny a tool call before it runs, so the enforcement
primitive is portable. The differences are vocabulary, not capability.

| Host | Blocking event | Config | Deny shape |
|---|---|---|---|
| Claude Code | `PreToolUse` | `.claude/settings.json` or plugin | `permissionDecision: "deny"` |
| GitHub Copilot | `preToolUse` | `.github/hooks/*.json` | `permissionDecision`: `allow`/`deny`/`ask`; takes `bash` **and** `powershell` |
| OpenAI Codex CLI | `PreToolUse` | `.codex/config.toml` | `permissionDecision` + `permissionDecisionReason` |
| Gemini CLI | `BeforeTool` | `.gemini/settings.json` | `{"decision":"deny","reason":"…"}` |
| Antigravity | `PreToolUse` | `.agents/hooks.json` or a plugin | `{"decision":"deny","reason":"…"}`, plus `ask` / `force_ask` |

**v1 ships Claude Code and GitHub Copilot.** The rest follow.

ChatGPT is not a target — Codex is OpenAI's repository-agentic surface. Antigravity
plugins are the stated successor to Gemini CLI extensions, so the Google side targets
Antigravity plugins with Gemini CLI as a separate host.

### 11.1 What the adapter normalizes

Event names; tool names (`Edit`/`Write`/`Bash` versus `edit`/`view`/`bash` versus
`write_file`/`replace`/`run_shell_command`); input key names and nesting; and the deny
shape. So `sdlc hook --host <name>` reads stdin, normalizes, asks the one policy
engine, and emits that host's deny format. Each host gets a conformance test that
feeds a captured real payload and asserts a deny.

Iteration 3's own assessment of this work, which iteration 4 is executing: the
git-reading checks already work as gates whichever agent did the work; the
action-based hooks, the skills, load-time injection and `${CLAUDE_PLUGIN_ROOT}` are
Claude Code only. **The coherent split — git-based gates as the authority, hooks as a
fast local layer — was identified and never written.** That is the architecture work.

### 11.2 MCP is the portability lever

- **an MCP server** exposes every cycle stage, the gate, and interrogation as tools —
  one implementation, an identical tool surface everywhere. This is *how the agent does
  the work*;
- **the pre-tool hook** is one small config per host pointing at the same gate binary.
  This is *what stops the agent doing the work another way*;
- **`sdlc verify-chain` in CI** is *the guarantee*;
- **one `AGENTS.md`** is the single prompt source of truth, with per-host instruction
  files as thin pointers to it.

### 11.3 Two consequences of the host choices

Copilot's cloud coding agent is asynchronous and cannot conduct an interview, so the
intent phase cannot run there. v1 targets Copilot CLI and IDE chat. The cloud agent is
a natural fit for `build` from an already-approved plan — later, not v1.

**Shipped tooling stays Windows PowerShell 5.1**, invoked as `powershell.exe`, for the
reason iteration 3 recorded: hooks are the deterministic layer, and a hook that fails
to start because an edition is not installed protects nothing while appearing to. 5.1
is on every Windows machine. The cost is real and the scripts are written for it — no
`&&` or `||`, no ternary, no `??`, `ConvertFrom-Json` returns a `PSCustomObject`, and
`Test-Json` does not exist, so schema validation lives in the test project rather than
in a script.

---

## 12. Pipelines

Generated at `init`, per host. Every pull request builds, tests, **displays code
coverage**, and runs `sdlc verify-chain`.

Coverage is displayed, not enforced — no percentage threshold. Coverlet plus
ReportGenerator; Azure DevOps has a native coverage tab, GitHub needs a PR-comment
action. One cheap addition inside the no-threshold rule: report the coverage **delta**
against the target branch, so a silent slide becomes visible without gating anything.

`verify-chain` re-establishes, from the repository rather than from any agent's word,
that:

- every artifact in the chain exists;
- the approval ledger holds an `approved` decision;
- all three recorded hashes match the bytes on disk;
- every changed file was declared in the plan;
- every spec criterion maps to a named test;
- the outcome in `verify.md` is `pass`.

**And what it must not claim.** With local-only approval it proves the entry exists
and the hashes match. It cannot prove a second person approved. The check says so in
its own output, so a green run is not read as more than it is.

### 12.1 Two hosts, one of them under-exercised

The plugin is open source on GitHub, so contributors, issues and examples all exercise
the **GitHub** adapter. The **Azure DevOps** adapter is what private MofonCode work
depends on, and without deliberate effort exactly one person ever runs it.

`dice-trial` is the trial repository — cleared and re-run as often as needed, and
where cycles are exercised against the real skills.

**It carries two remotes.** It already has a GitHub one, with the trial cycle's branch
pushed and a pull request open, so the GitHub adapter has a real exercise path. An
Azure DevOps remote is added alongside it, and the same cycle is pushed to both. One
repository, one set of cycles, both adapters meeting a real server — no second trial
repository to keep current.

Design lever that shrinks the exposure: every check lives inside `sdlc verify-chain`,
so each pipeline is about thirty lines of YAML wrapping one command.

**Before `dice-trial` is cleared the first time, keep the existing cycle.** The
`20260929_4e1a` artifacts are the best worked example of what good output looks like,
and they are the calibration target for the skills being written. Re-running a cycle
will not reproduce them by accident.

---

## 13. The experience

At the end of every stage the user sees a panel: whether the artifact's checks passed,
a brief summary of what it says, and the next step.

```
  spec · 20260929_1456_32                           round 2 · 1 blocking

  Says      Asking for the page returns one roll of a six-sided die, decided
            for that asking. 6 criteria, 6 exclusions, 2 blocked on open
            intent questions.

  Checks    r1: 2 blocking · r2: 1 blocking
            F_01  CON_04.1  blocking   the decided delivery has no criterion

  Gate      not approved - answer or waive F_01 first

  Next      sdlc refine 20260929_1456_32 spec        apply F_01
            sdlc interrogate 20260929_1456_32 spec   re-check
```

Four rules:

1. **The CLI prints it, not the model.** A skill writing this as prose produces a
   format that drifts every run and between hosts. The last step of each stage is
   `sdlc status <id> --stage <artifact>`, shown verbatim — which also gets the panel
   onto every host for free, including CI.
2. **It is not called verification.** `verify` is a phase name with a specific
   meaning; for an artifact the word is "checks."
3. **It ends with the literal next command**, copy-pasteable.
4. **ASCII-safe glyphs and a no-color mode**, because it renders in a pipeline log and
   in the default Windows console, not only in Windows Terminal.

The round history is shown and no recommendation about continuing is attached.

---

## 14. Skills

| Skill | Does |
|---|---|
| `init` | adopts the framework into a repository — see §8 |
| `intent` | interviews the user; classifies the change; writes `intent.md` |
| `spec` | derives testable criteria from the intent; writes `spec.md` |
| `plan` | reads the spec and the existing code; writes `plan.md` with its file table, proof, and claims |
| `interrogate` | judges one artifact against the constraints in force; writes a findings round; never edits |
| `refine` | applies an artifact's findings to that artifact; may not touch code; refuses once the cycle is approved |
| `remediate` | applies a blocked verify's findings to source and tests; may not touch a planning document; capped at three passes |
| `build` | implements the plan; builds; tests; records evidence; warnings are errors |
| `verify` | adjudicates the build against the spec from evidence alone; writes `verify.md` |
| `split` | divides an oversized intent into smaller ones, recording their order and abandoning the original |
| `batch-build` | collects approved cycles and builds them, concurrently where the claim graph allows |
| `resolve-conflict` | resolves collisions on files that cannot be claimed exclusively |
| `land` | pushes, opens the pull request, links the work item |
| `cycle-approval` | prints the approval entry for the three planning documents, with instructions to paste into `approval.md`; writes nothing |
| `status` | prints the stage panel, and the state of a cycle or of all cycles |
| `constraints` | renders the effective constraint set, all tiers |
| `suggestions` | lists accumulated non-blocking findings |

The draft's `pull-request` is `land`, and `interogate-artifact` is `interrogate` —
spellings are fixed because skill names become directory names, command names and
documentation.

Any skill run against a chain whose prior artifact is missing or unapproved fails and
says which artifact and which gate stopped it.

---

## 15. Layout

```
.sdlc/
  solution.md                 profile, origin, adopted_at — written by init
  techstack.md                what this solution is built from
  constraints.md              this solution's own constraints (LOC_nn), hand-edited
  README.md                   terminology, flow and commands, for whoever works here
  cycles/
    20260929_1456_32/
      intent.md
      spec.md
      plan.md
      verify.md
      approval.md             append-only ledger of this cycle's gate decisions
      abandoned.md            why this cycle stopped, if it did
      findings/
        intent.r1.json
        intent.r2.json
        spec.r1.json
        plan.r1.json
      evidence/
        diff.patch
        build.log
        tests.trx
        base.sha
```

A cycle is one governed change: an id, a directory, and a `cycle/<id>` branch.
Directories appear when the skill that writes the first file into them runs; nothing
is pre-created.

**Cycle ids are `<date 8>_<time 4>_<seconds 2>`** — `20260929_1456_32`. Purely
temporal, so they sort chronologically and say when the cycle opened, with no opaque
component. Allocation needs no coordination and works offline.

The id is load-bearing in three places — the branch name, the directory path, and
every cross-reference inside the artifacts — so it is fixed at allocation and never
rewritten. Two cycles opened in the same second would collide; the directory already
existing is what catches it.

Artifacts live on the cycle branch. `batch-build` reads approved cycles locally and
builds before anything is pushed, so the gate is passed before code exists; the pull
request then reviews code against an already-approved plan.

A cycle index exists so `batch-build` does not glob and parse directories to find
work, and each cycle records its branch and work item.

### 15.1 Abandoning a cycle

An abandoned cycle **keeps its directory**, and records why in `abandoned.md`
alongside `approval.md`. The reason is the only thing the cycle produced, and a
repository holding the intents that were tried and dropped — with the objections that
killed them — is readable history rather than clutter.

It follows the approval pattern exactly: a skill prints the entry, the person creates
the file, and **the hook denies every agent write to it.**

The reason for that is not safety — abandoning nothing lands, so it cannot forge a
result. It is history. An agent stuck on a blocking finding has an obvious escape:
abandon the cycle and start a clean one, and eleven rounds of objections vanish into a
directory nobody reads. A human-written reason is what makes abandonment a decision
rather than an exit.

```yaml
---
cycle: 20260929_1456_32
abandoned_by: Peter Almanzar
at: 2026-10-01T14:22:00Z
stage: plan
findings_rounds:
  intent.md: 2
  spec.md: 3
  plan.md: 1
reason: the spec's criteria cannot be met without the schema change in flight
        elsewhere; reopening once that lands
---
```

**Abandonment is terminal.** A cycle with an `abandoned.md` never builds, even if it
also holds an approval — `batch-build` skips it, and so does `land`. Changing your
mind means a new cycle, not resurrecting this one, because resurrection would need
rules for which decision wins and the ordering of two hand-written files is not
something to rest a gate on.

---

## 16. What iteration 3 proved, and what it did not

Carrying v03 forward means carrying its evidence and its gaps honestly.

**Exercised by a real cycle:** `init`, `intent`, `interrogate` (six rounds across
three artifacts), `spec`, `plan`, `build`, `verify`, and the diff-to-plan comparison.
`CON_07` raised two real findings on a real diff.

**Never run by any cycle or any test that invokes them:** `remediate`, `run`,
`suggestions`.

**Never reached, so never tested:** `escalated`. The bounded-remediation state machine
— three passes, then a person owns it — exists entirely on paper, and `Assert-Landable`
has never refused anything because no cycle has ever been unlandable. Iteration 3 calls
this the largest untested claim in the framework, and **`batch-build` depends on it
completely**: unattended building is only safe because a cycle that cannot be made to
pass stops instead of landing.

**A check that tested nothing while appearing to run:** the nightly job asserting
interrogation verdicts needs an API key that was never set, so it emitted a warning and
exited cleanly. This is §2.1's failure mode inside the plugin's own CI.

**Unconfirmed:** the `CON_06` flake fix, made by reading rather than by re-running.

**The implication for sequencing.** Before `batch-build` is built on bounded
remediation, drive one cycle to `escalated` on purpose — the cheapest route is a
deliberately inadequate proof, which should block, then remediate, then exhaust the
budget. That single trial also closes the brownfield half of `adopted_at` (§7.3) if it
runs in a repository with pre-existing code.

---

## 17. Non-goals

- **Enforcing a coverage percentage.** Coverage is displayed.
- **Making local approval unforgeable.** It cannot be; the pull request is where
  accountability lives. Local approval exists to keep an agent honest.
- **Governing ai-sdlc with ai-sdlc.** See §2.5.
- **Resolving merge conflicts as the primary strategy.** Collisions are scheduled
  around; resolution is the fallback for unclaimable files.
- **Remote plan approval.** See §4.1.
- **A shared batch queue.** See §9.
- **Supporting ChatGPT as a host.** Codex is the surface.
- **Plans detailed to the line.** See §3.2.
- **Baselining existing problems at adoption.** See §7.3.

---

## 18. Open questions

1. **Do the mechanical halves of the three MofonCode conventions become custom
   analyzers?** §10.2 establishes that no analyzer decides them today, and that each
   splits into a decidable half and a judgement half. One-folder-per-feature is almost
   entirely mechanical and is the obvious first one. Writing them is new work in
   `MofonCode.Core`, not something to inherit — so the question is whether it is worth
   it, or whether the judgement halves stay `PRO_nn` constraints and the mechanical
   halves stay unenforced.
2. **Does the no-pragma rule ship as a build check?** §10.3 keeps every suppression in
   `Directory.Build.props` with a reason and zero `#pragma warning disable` in source.
   The rule is mechanically checkable, so it should be a check rather than a
   convention — the question is only whether it is worth a custom analyzer or whether
   a simpler build-time grep suffices.
