---
freshness: exempt
reason: design record — records decisions rather than documenting source
---

# ai-sdlc — concept

    Status: DRAFT — open for refinement
    Origin: hand-written on a clean slate, 2026-09-27
    Predecessor: origin/archive/v02 (read for evidence, not as a starting point)

This is the design record for the current rebuild: what the framework is, what moves
through it, and which decisions are settled versus still open.

**How to read it.** `DECIDED` is settled with the reason recorded — revisit only against
new evidence, and write down what changed. `OPEN` is genuinely undecided; do not guess,
resolve it with a person. Tables of versions are evidence, never authority.

---

## 1. What this is

A process framework that defines how a change moves from idea to landed code in the solutions
that adopt it.

**The single idea:** every phase ends by committing an artifact the next phase reads, and
each artifact is interrogated against a set of constraints before it may advance. Nothing
advances on assertion; it advances on evidence that survived a challenge.

**DECIDED:** the framework is developed as an ordinary product — branch, review, merge — and
does not run its own cycles. There is no `.sdlc/` directory in this repository and there never
will be. Skills are validated against checked-in fixture solutions: deterministic, disposable,
and reviewable. A framework that governed itself would couple its own history to its test
data, make every failure a two-layer debugging problem, and block work on the process behind
the very process being built.

Following the framework's own documentation conventions here — a lean `AGENTS.md`, described
pointers, checked freshness — is not self-governance. Those are good practice for any
repository. The line is the cycle: this one does not run them.

**DECIDED:** it ships as one Claude Code plugin. There are no stack packs. A single set of
stack defaults (§8) serves every solution, and a solution that needs something else says so
during intent interrogation.

**DECIDED:** global constraints live **in the library**, not in a document copied into each
solution. Nothing is shipped, copied, or scaffolded. This removes pack identity, per-pack
lineage, and every discipline that existed only to keep IDs meaningful across a copy.

**DECIDED:** engine logic and stack defaults go stale on different clocks, so they keep
separate homes. Engine logic lives in the skills and the global constraints. Stack defaults
live in `docs/techstack.md` and `Directory.Packages.props`, and can be updated on a
dependency clock without touching a skill.

---

## 2. Vocabulary

Fixed here because some of these words are overloaded in the wider ecosystem, and the
ambiguity is expensive.

| Term | Means |
|---|---|
| **cycle** | one change from intent to landing, identified as `YYYYMMDD_xxxx` |
| **intent** | why the work exists — the only artifact written by interview |
| **spec** | what "done" means for this cycle, as testable acceptance criteria |
| **criterion** | one testable statement in a spec. Per-cycle and disposable |
| **plan** | how it will be built — files, order, risk, proof |
| **constraint** | a permanent rule with a non-empty `Forbids:` clause. Without one it is decoration |
| **interrogation** | an adversarial check of an artifact against the effective constraints, producing findings |
| **finding** | a verdict on one criterion or constraint: `blocking` or `suggestion`, cited |
| **waiver** | a person's recorded, attributed acceptance of a blocking finding |
| **outcome** | a verify document's state: `pass`, `blocked`, or `escalated` |

**DECIDED:** criteria and constraints stay distinct words. Criteria are per-cycle and
disposable — they describe one change and become history when it lands. Constraints are
permanent and cumulative — they bind every future change. Sharing a name for both would make
"does it meet the criteria" ambiguous and leave a finding unable to say which kind it
breached.

**DECIDED:** phases are named after the artifact they produce, never after the activity.
Anthropic's playbook names its stages for activities (`Plan`, `Design`, `Build`) while naming
artifacts for altitude (`intent.md`, `spec.md`, `plan.md`), so the stage called *Plan*
produces the intent and the plan file is produced during *Build*. Artifact naming removes
that collision, at the cost of matching the playbook's stage vocabulary.

**DECIDED:** the severity and outcome vocabularies are defined by their JSON schemas, not by
prose and not per cycle. The enum in `schemas/findings.schema.json` is the single definition
of what severities exist; every consumer — verify, remediate, land, CI — branches on the same
closed set. A spec defines criteria; a schema defines the vocabulary of verdicts about them.

---

## 3. The cycle

```
intent -> spec -> plan -> build -> verify -> land
                            ^         |
                            +- remediate (<= 3) -+
```

| Phase | Question it answers | Artifact | Gate to pass |
|---|---|---|---|
| **intent** | why — what is wrong, for whom, under what constraints | `.sdlc/cycles/<id>/intent.md` | a person accepts it |
| **spec** | what — testable acceptance criteria | `.sdlc/cycles/<id>/spec.md` | a person accepts it |
| **plan** | how — files touched, order, blast-radius tier, proof | `.sdlc/cycles/<id>/plan.md` | **a person approves — the last gate before anything becomes irreversible** |
| **build** | the change itself | code diff, plus build and test evidence | it builds and its tests pass; machine-decided |
| **verify** | did the change do the spec justice | `.sdlc/cycles/<id>/verify.md` | outcome `pass` |
| **land** | integration | a pull request | a code owner approves the merge — never the authoring agent |

Naming of `land` and `remediate` is provisional; everything else about them is settled.

### 3.1 Each phase answers to the one before it

**DECIDED:** every phase is accountable to its predecessor, and says so explicitly.

- **spec** answers to the intent — the criteria must cover what was asked for, and nothing
  it wasn't
- **plan** answers to the spec — it does its best to meet every criterion, and declares
  where it cannot
- **build** answers to the plan — it implements what was declared, and **self-heals build
  and test failures** rather than handing a broken tree onward
- **verify** answers to the spec — it keeps the diff honest against the criteria

### 3.2 Build heals itself; verify only judges

**DECIDED:** build owns making the tree work. If it does not compile or the tests fail, build
fixes it. Nothing reaches verify that has not already built and passed.

**DECIDED:** verify writes no code. It is the double and triple check: it reads the diff
against the spec and reports **every unmet criterion** as a finding — blocking or suggestion.
Everything in a verify document is derived from evidence that already exists; verify produces
no evidence of its own, which is what makes it trustworthy as an auditor.

**DECIDED:** testing is build's exit condition, not a phase of its own. Splitting it out
would give verify two jobs — producing evidence and judging it — and an auditor that
generates its own evidence is not an auditor.

### 3.3 Remediation is bounded and its exhaustion is a state

**DECIDED:** remediation is a separate phase, not something verify does. It reads `verify.md`,
works the blocking findings, and hands back to verify. Verify re-runs after every remediation
pass, because nothing else can tell whether the fix worked.

**DECIDED:** the ceiling is **three remediation passes**, each followed by a full verify.

| Outcome | Means | What may happen next |
|---|---|---|
| `pass` | no blocking findings, or all of them waived | landing |
| `blocked` | blocking findings present, budget remains | remediation |
| `escalated` | three passes spent, blocking findings remain | a person owns it; landing refuses |

**DECIDED:** `escalated` is terminal for the machine. Landing refuses it, and reports that
remediation was attempted and how many times — so a reviewer can tell work nobody tried to
fix from work that resisted three attempts.

**DECIDED:** suggestions never block, and remediation never works them. A product owner or
tech lead reviews them, and any blocking findings that reached `escalated`.

### 3.4 The way out of `escalated` is to change verify's inputs

**DECIDED:** `escalated` is never cleared by a state transition, and `verify.md` is never
hand-edited. Verify is derived, so the way forward is always to change one of its inputs and
let a fresh verify supersede the old one. Four legal exits:

| Exit | When the blocker is | What changes | Who |
|---|---|---|---|
| **fix by hand, re-verify** | real, and the machine could not do it | the code | engineer |
| **amend the spec, re-approve, re-verify** | the *criterion* was wrong, not the code | the spec, back through its person gate | product owner / tech lead |
| **waive, re-verify** | real, understood, and accepted anyway | a waiver record | tech lead, attributably |
| **abandon** | not worth solving | the cycle closes unlanded | product owner |

The spec-amendment exit is load-bearing. Without it, a badly written criterion forces people
to contort code to satisfy it — or to edit `verify.md`, which is the one thing that must
never happen.

**DECIDED:** waivers are their own artifact, `.sdlc/cycles/<id>/waivers.md`, never a field
inside verify. A waiver records who, when, why, and which finding. Verify reads waivers as an
input and reports the finding as `blocking (waived)`; landing permits a waived blocker. The
finding is never deleted — that is what keeps the trail honest, and it makes waiver count a
measurable signal about whether a constraint is earning its place.

**DECIDED:** the three-pass counter counts *consecutive remediation passes with no change to
verify's inputs*. Any human change — code, spec, or waiver — resets it. This is not a
loophole: in unattended mode there is no human change, so the ceiling holds absolutely. The
ceiling constrains the machine, and a person in the loop is the condition under which further
attempts are warranted.

### 3.5 No retro; suggestions die with the cycle

**DECIDED:** there is no retro phase and no retro artifact. The findings in `verify.md` carry
what a retro would have carried, to the people who act on them, without a seventh phase.

**DECIDED:** suggestions are not promoted into intents automatically. An unactioned intent
queue rots in a way that is hard to detect, and is worse than no queue. A product owner who
wants to act starts an intent citing the verify document and naming the suggestions to take
up.

**DECIDED:** suggestions are therefore **findable but not queued**. A read-only view reads
the verify documents and lists open suggestions across cycles. It is a query, not an artifact:
nothing to keep in sync and nothing to go stale.

What this gives up, recorded so it is not rediscovered: nothing aggregates automatically. A
single verify document says what one change got wrong; noticing that thirty cycles are saying
the same thing depends on someone running the view and reading it.

---

## 4. Governance

### 4.1 The effective constraint set is rendered, not stored

**DECIDED:** what an interrogation checks against is assembled at run time from two sources.

| Source | Owns | Series |
|---|---|---|
| the library, `constraints.md` at the plugin root | global constraints, and the definition of landable | `CON_nn` |
| a solution's `.sdlc/constraints.md` | that solution's own constraints | `LOC_nn` |

Neither is copied into the other. A citation says where to resolve it: `CON_` in the library,
`LOC_` in the solution. A person reads the merged set by rendering it, the same way they read
a verify document — the effective set is derived.

**DECIDED:** IDs are a monotonic sequence, seeded like a database identity column. They
increase forever, are assigned once, and are never reused or backfilled. A removed constraint
leaves a gap; the gap plus the commit that made it is the record. A changed conclusion is a
new constraint, and the old one gains `Superseded-by`.

**DECIDED:** because global constraints live in the library, upgrading the plugin can change
the rules a solution is judged against with no commit in that solution. Every findings and
verify document therefore records the framework version that produced it. Without that, a
cycle's verdict is not reproducible.

**DECIDED:** global constraints are written stack-neutrally. "Every fix ships with a failing
test first" holds on any stack; "tests use MSTest" does not. Stack specifics belong in
`docs/techstack.md` and in analyzers — otherwise a solution on a non-default stack starts life
in violation of rules that never applied to it.

**DECIDED:** "definition of done" splits in two, because one phrase was doing two jobs. The
library holds the *global* bar — what makes any change landable. The spec holds the
*per-cycle* acceptance criteria — what makes this change done. Verify checks both.

### 4.2 What qualifies as a constraint

**DECIDED:** a constraint is a rule about an **artifact** that needs reading comprehension to
apply. Rules about code belong to hooks, CI, or analyzers. Three tests, all of which must
pass:

1. It has a non-empty `Forbids:` clause naming what becomes illegal
2. No hook, schema, or analyzer could decide it — it needs judgment
3. It is incompatible with at least one plausible implementation

The third is the one that kills platitudes: a rule compatible with every possible
implementation is decoration, however true it sounds.

Illustrative only — the live lists are `constraints.md` in the library and
`.sdlc/constraints.md` in a solution, and this document is not a second copy of either:

```markdown
## CON_01 — An intent states the problem, not the solution
Forbids: an intent whose content is an implementation instruction, with no stated problem.

## LOC_01 — Tenant id comes from the token, never the request
Forbids: reading a tenant identifier from route values, query string, form fields,
or a JSON body anywhere under `src/Api/`.
```

**DECIDED:** candidates that fail test 2 are moved, not admitted. Recorded because each one
reads like a constraint and is not:

| Candidate | Belongs to |
|---|---|
| the diff touches only files the plan declared | a hook — mechanical set comparison |
| every blocking finding cites a constraint | the findings schema — a required field |
| a `Superseded-by` names an ID that exists | CI — referential check |
| an amended spec is re-approved before re-verify | a hook — timestamp comparison |
| a cycle never lands while escalated | the landing gate — it already refuses |
| money is never a floating-point type | an analyzer |
| no SQL outside the repository layer | an architecture test |

**DECIDED:** the test for a *solution-scoped* constraint is narrower still — it names a
cross-cutting hazard that looks fine locally in a diff. A tenant id read from a request body
looks like ordinary parameter binding; a column drop looks like cleanup. That is the class a
reviewer misses and an analyzer cannot judge, because the violation is about context rather
than syntax.

### 4.3 A solution declares three things

**DECIDED:** a constraint is evaluated against **the change under consideration**, never against
code that predates it. This was found while building the first adoption fixture: the earlier
design had `solution.md` record a baseline of accepted pre-existing violations, and there is
nothing for it to record. Every constraint governs an artifact — an intent, a spec, a plan, a
diff, a verify document, a waiver — and a repository adopting the framework has none of those
yet. The example written here cited CON_04, a rule about whether a spec covers its intent, as
something a codebase could violate forty-seven times.

So there is no baseline and no ratchet. Adoption is free: existing code is not interrogated,
because interrogation has nothing to read until the first cycle produces an artifact. Wholesale
cleanup of existing code is an analyzer and refactoring concern, with its own tooling and its own
suppression story, and it does not belong to this framework.

**DECIDED:** `.sdlc/solution.md` carries facts an interrogation needs that are not rules — which
stack, and how the solution came to be governed. It is created by `init` and always exists.

**DECIDED:** it is markdown with YAML frontmatter, not a bare `.yml`. The frontmatter carries
the values a skill or hook looks up; the prose below carries the reasoning a reviewer needs,
which has nowhere to live in a pure data file. The same frontmatter parser already exists for
the docs `covers:` check, so this adds no new format.

```markdown
---
stack: dotnet            # drives analyzers and plan defaults
origin: adopted          # or: scaffolded
adopted_at: 3932166      # the sha the framework was adopted at
---

# Solution profile — acme-billing

Adopted into an existing repository on 2026-09-27 at `3932166`. Code written before that
commit is not interrogated: constraints govern each change as it is made.
```

**DECIDED:** `solution.md` and `docs/techstack.md` are not the same document and never merge.
The first declares *which* stack a solution is and how it came to be governed — looked up by
field, by machines. The second explains *how* the stack is used — project shapes, pins,
layout, rationale — and is read as prose by whoever is doing the work.

**DECIDED:** `.sdlc/constraints.md` is created when a solution writes its first local
constraint, not at init. Many solutions will never have one, and nothing is lost when they
don't — they are judged by the library.

### 4.4 Interrogation

Two kinds:

- **of a user** — at intent, to find what is ambiguous about the request. The user knows what
  they want and does not yet know what is underspecified; finding that out is the work. Intent
  interrogation is also where a solution declares a stack other than the default
- **of an artifact** — at every phase boundary, against the effective constraint set,
  producing findings

### 4.5 Three enforcement modes

**DECIDED:** each rule is enforced by exactly one mechanism, chosen by what a violation costs.

| Mode | Mechanism | Decided by | Use when |
|---|---|---|---|
| advisory | instructions in context | the model | a violation is embarrassing |
| adversarial | interrogation against the constraints | a challenge, recorded as findings | the rule needs judgment to apply |
| deterministic | a hook | code, before the action runs | a violation is unacceptable |

A rule that must never be violated does not belong in prose. A rule that needs judgment
cannot be a hook.

---

## 5. Documentation model

**DECIDED:** one lean entry point, with detail behind described pointers.

```
CLAUDE.md            # thin: @AGENTS.md, plus anything Claude-specific
AGENTS.md            # under 200 lines, loaded every session
docs/*.md            # opened only when its trigger applies
.sdlc/solution.md    # declarations — stack and origin; looked up, not read
.sdlc/constraints.md # this solution's constraints; read at interrogation, never ambiently
```

`AGENTS.md` carries: what this is, framework-versus-consumer, vocabulary, commands, the repo
map, the definition of landable, what not to touch, and an index of the detail docs. It
carries **no constraints** — those live in the library and the solution's own file, and
duplicating them creates sources that will eventually disagree.

**DECIDED:** the docs index describes each file by its *trigger*, not its subject, and
references it as a plain path. `@path` imports expand into context at launch, which would
defeat the point; a described path is opened only when the model judges it relevant.

| Doc | Opened when |
|---|---|
| `docs/techstack.md` | choosing a project shape, or changing package pins |
| `docs/testing.md` | writing or fixing tests — fixtures, suites, flakes |
| `docs/local-dev.md` | the orchestrator failed, or you need topology and ports |

**DECIDED:** documentation freshness is checked, not promised. Each detail doc declares what
it covers in frontmatter, and CI fails when covered source is newer than the doc.

```yaml
---
covers:
  - "src/Orchestration/**"
  - "dev.ps1"
---
```

Three assertions come free with that: every doc under `docs/` is indexed exactly once, every
index row points at a file that exists, and every indexed doc has a freshness signal. A doc
with no `covers:` glob has no such signal — which is an argument for keeping the set small.

---

## 6. Skills

| Skill | Does |
|---|---|
| `init` | adopt the framework into a repository — scaffold `.sdlc/`, `solution.md`, and a lean `AGENTS.md` |
| `intent` | interview a person into an intent, including any departure from the default stack |
| `spec` | turn an accepted intent into testable acceptance criteria |
| `plan` | turn a spec into a plan, declaring files and blast radius |
| `build` | implement an approved plan, self-healing build and test failures until it is green |
| `verify` | adjudicate the diff against the spec and report every unmet criterion |
| `remediate` | work the blocking findings in a verify document, bounded at three passes |
| `land` | open the pull request |
| `interrogate` | challenge an artifact against the effective constraint set |
| `constraints` | render the effective set — library globals merged with solution locals |
| `suggestions` | read-only view of open suggestions across cycles |
| `run` | batch mode — see below |

---

## 7. Unattended mode

**Goal:** author intents and specs interactively, then have the machine carry approved work to
a pull request without supervision — all of it, or a named subset.

**DECIDED:** unattended mode starts from an **approved plan**, never from an approved intent.
The plan gate is the one place a person sees what will change before it changes; starting a
batch earlier means the machine writes the spec, writes the plan, and executes it with nobody
between the idea and the diff. That removes the gate the whole design rests on, and it is the
failure the rest of this document exists to prevent.

**DECIDED:** the unattended loop is `build -> verify -> remediate -> verify`, ending at `pass`
and landing, or at `escalated` and a person. Both endings are legitimate; a batch that
escalates two of nine cycles has done its job.

**DECIDED:** one worktree per cycle, so parallel cycles cannot interfere and a failed cycle
leaves no debris in the main tree.

**DECIDED:** an approval prompt during a build puts a person back on the critical path and
defeats the purpose of the batch. Hooks may *refuse* during a build; hooks that *ask* belong
at the landing boundary.

---

## 8. Default tech stack

**DECIDED:** Microsoft-first. C# and .NET are the default for everything the framework builds
and for every solution it scaffolds. A solution may declare otherwise during intent
interrogation; global constraints stay stack-neutral so that departure is legal rather than an
immediate violation.

| Concern | Default |
|---|---|
| API | ASP.NET Core |
| Web | Blazor |
| Multiplatform UI | .NET MAUI Blazor Hybrid — desktop, web and Android from one codebase |
| Desktop | .NET MAUI |
| Relational data | SQL Server |
| Document data | Cosmos DB |
| Machine learning | ML.NET |
| Testing | MSTest on Microsoft.Testing.Platform |
| Local orchestration | .NET Aspire AppHost, emulators over live Azure resources wherever one exists |

**Note on ML.NET:** it is the right default for consistency with the rest of the stack, and it
costs real capability against the Python ecosystem on competitive modelling work. Name that
cost when a solution's core value is model quality rather than integration.

---

## 9. Open questions, collected

1. Names: `land` and `remediate` are provisional.
2. Does `interrogate` stay a separate skill, or become a step inside each phase's skill? It
   runs at every boundary, which argues for one callable implementation either way.
3. Who may sign a waiver, and does that depend on whether the finding cites `CON_` or `LOC_`?
   A global constraint waived locally is a different act from a local one waived locally.
4. Does an abandoned cycle keep its directory as a record, or is it removed? Keeping it means
   the repo accumulates dead cycles; removing it means the reason it died is lost.
