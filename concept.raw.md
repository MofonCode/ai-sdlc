# ai-sdlc v02 — raw concept

    Status: FROZEN 2026-09-23
    Origin: hand-written, before any tooling existed

**This is the last artifact written entirely by hand, and it is closed.**

It is the design record: ~91 decisions with the reasoning that produced them, the
alternatives rejected and why, and the open items that remain. That record does not
change, because the reasoning that led here does not change.

From this point the framework's own process carries the work, and each part of it has a
home that is not this document:

| What | Lives in |
|---|---|
| the rules a solution must obey | `.sdlc/CHARTER.md` |
| the work to be done | intents |
| what happened when it was done | retros |

Keeping this document alive alongside those would make it a second list beside all three
— INV-03, on the document that argued for INV-03. So it freezes, and it is read for *why*
rather than for *what is currently true*. Where it disagrees with the charter, the
charter wins.

## What this is

A process framework: it defines how a change moves from idea to shipped code, and
it is built by its own process.

It ships as a **single Claude Code plugin** containing two things that rot
differently and must not be conflated:

- **The engine** — SDLC logic. Stage-per-workflow, artifact-per-stage, gates
  between them. Improves by absorbing retros.
- **Stack packs** — tech-stack logic. Project shapes, package pins, layout
  rules, checks. Goes stale on a dependency clock, not on a retro clock.

The previous version is archived at `origin/archive/v01` (106 commits, 88 files).
Do not read from it unless pointed at a specific file. It is evidence, not a
starting point. This document is the only sanctioned channel from v01.

## How to read this document

- **DECIDED** — settled, with the reason recorded. Revisit only against new
  evidence, and write down what changed.
- **OPEN** — genuinely undecided. Do not guess; ask.
- Tables of package versions are **evidence, not authority**. The authority is
  `Directory.Packages.props` and the stack pack. Never treat a table here as a
  source of truth.

---

## 1. The cycle

**DECIDED:** adopt Anthropic's AI-Native SDLC playbook as the spine, including
its artifact names verbatim. Divergence costs shared vocabulary with the
official docs and every other tool in the space, and buys nothing.

```
intent -> spec -> plan -> build -> verify -> deploy -> maintain
   ^                                                      |
   +---------------------- caused-by ---------------------+
```

| Stage | Artifact | Human gate |
|---|---|---|
| intent | `intent.md` | merge the intent PR. The step itself is interactive — it asks you questions |
| spec | `spec.md` | approve |
| plan | `plan.md` | approve |
| build | code diff | approve `plan.md` before codegen (tier-dependent) |
| verify | `verify.md` | approve |
| deploy | PR, review passes, merge | **merge — never automated** |
| maintain | new `intent.md` | triage: does this become an intent? |

**DECIDED:** each stage is its own **dynamic workflow**. This is not a
convention — a workflow cannot ask the user anything mid-run, so the gate
boundary *is* the workflow boundary, enforced by the runtime.

`intent` is deliberately not a workflow: its entire purpose is asking questions,
so it is an ordinary skill using `AskUserQuestion`.

### Naming

**DECIDED: a stage verb is named after the artifact it produces.** `spec` →
`spec.md`, `plan` → `plan.md`, `verify` → `verify.md`, `retro` → `retro.md`,
`intent` → `intent.md`. Naming a skill after its *mechanism* rather than its
output is what made the earlier `elicit` wrong: asking questions is how the intent
stage works, not what it is.

**DECIDED: `interrogate` is not a stage and takes a target.** It runs *on* another
stage's output rather than owning a slot of its own, so it is `interrogate <artifact>`.
The word also belongs here rather than on the human-facing step: you interrogate a
document because you are trying to make it fail, and you interview a person because
you want what they know.

**DECIDED: the plugin is `sdlc`**, so commands are `/sdlc:intent`,
`/sdlc:interrogate spec`. Not `sdlc-core` — "core" would describe a distinction from
a non-core sibling that the single-plugin decision rules out. Not `ai-sdlc` — that
is the repository and the marketplace; reusing it for the plugin means typing it
twice for no extra information.

### Cycle ids

**DECIDED: `<yyyymmdd>-<4 hex>`, for example `20260922-a7f3`.** Assigned once by the
`intent` skill at creation and never changed — it is the filename, the branch name,
the worktree directory, the prefix of every acceptance-criterion id, and the retro's
identity.

```
intents/20260922-a7f3.md
cycle-20260922-a7f3                              ← branch and worktree
caused-by: 20260922-a7f3
[TestProperty("AC", "AC-20260922-a7f3-3")]
```

**Rejected: a sequential counter.** It needs coordination, and coordination here is
the `plugin.json` collision again — a shared counter file conflicts whenever two
intents are created at once, and deriving the next number by scanning races. With a
team authoring intents concurrently, sequential numbering is a guaranteed conflict
for no benefit.

Nothing is lost by dropping the sequence: chronology is carried by `landed-at` (§4),
ordering comes free from the date prefix, and gap detection was already dead —
completion order is arbitrary, and re-running absorb catches anything missed.

**Rejected: a full GUID or UUIDv7.** Correct on uniqueness and wrong on cost. The id
is not a database key here; it is typed by hand into test annotations on **every
test**, and those are assigned-once-never-reused, so they cannot be tidied later.
`AC-018f3a2b-7c41-7000-8000-a1b2c3d4e5f6-3` is the shape that would have to live in
production test code forever. A 36-character worktree directory name also eats real
budget against Windows' 260-character path limit, inside a tree that already
contains `obj/Debug/net10.0/…`.

The collision domain is small enough that this is not a compromise: ids must be
unique **within one solution**, which is hundreds of items over years authored by a
handful of people. A collision requires two intents in the same solution on the same
day drawing the same four hex digits.

ULID was the runner-up — a real spec, sortable by construction, no hyphens — and
remains the fallback if hand-rolled ids ever prove insufficient.

**DECIDED:** Deploy means PR + review + merge, always in scope. *Promotion* —
actually running the thing somewhere — is a separate optional sub-step owned by
the stack pack, which may implement it as nothing. Keeping the playbook's name
for interop even though it mostly means PR-and-merge.

**DECIDED:** Maintain files intents; it never fixes. A bug fix runs the normal
cycle with a lighter intent template. Integration testing is Test, not Maintain.
Maintain's live fuel today is dependency/CVE drift and stack-pack staleness;
telemetry-driven intent is the reserved path and is not yet fuelled because
nothing is deployed.

### Retention — the log is append-only

**DECIDED: artifacts are never deleted and never collapsed.** Two independent
reasons, and either alone would settle it:

- **Established practice.** Architecture Decision Records have converged on
  exactly this: accepted records are immutable, a changed conclusion is a *new*
  record that supersedes the old one, and nothing is ever deleted — *"even
  rejected decisions are valuable because they prevent future teams from
  reconsidering options that were already evaluated."*
- **Our own retro corpus.** Aggregate statistics are computed over the full log
  (§4). Collapsing intents deletes the dataset the framework exists to build.

Intents are a decision log, not documentation of current state. You do not
collapse a log for the same reason you do not rewrite git history.

**Two kinds of state, and they are handled differently.** ADR practice writes a
`status:` field; §7 derives lifecycle state instead. Both are right, for different
things:

| | Example | Where it lives |
|---|---|---|
| **Lifecycle** — is the work done? | queued / in progress / done | **derived** (§7). Never written |
| **Semantic** — is this decision still in force? | `superseded-by`, `caused-by` | **written.** Not derivable from anything |

**DECIDED:** cross-artifact links are **by ID, resolved by search — never by
path.** Otherwise moving a file into an archive directory breaks every
`caused-by` and `superseded-by` pointing at it, which is inflicting
`unresolvable-reference` on yourself at scale.

**DECIDED:** the log is never an interrogation input. Fact-gathering reads the
current cycle's artifacts plus the charter; the full log is read only by retro
aggregation, which is a batch job. The natural failure mode is an agent globbing
`intents/**` and exhausting its context on history it will not cite.

**OPEN:** directory organisation, which is cosmetic and reversible precisely
because state is derived. Deferred until the count is annoying. OpenSpec's
convention is the obvious model when it is: archive by date into
`changes/archive/YYYY-MM-DD-<name>/`.

**OPEN — deferred, but named so it is not stumbled into:** a *living*
solution-level spec. OpenSpec is two-tier — it archives each change **and** merges
its deltas into a canonical `specs/` tree that "represents the system's actual
state." Our design is log-only: nothing says what the solution currently does.

The argument against adding one is INV-03. A living spec describing current
behaviour is a second representation of what the code does, and without a binding
check it is the classic documentation-rot failure. The argument *for* is that the
binding check already exists: if the living spec holds the AC IDs and every ID
must map to a passing test (§8), the test suite binds spec to code. That makes it
permissible — but it is a real feature that should earn its way in through a
retro, not be assumed. Deferred, with one consequence handled now in §8.

---

## 2. The charter

**DECIDED:** one charter per solution, single file, hand-written first.

The playbook has no charter — its persistent context is `CLAUDE.md` (conventions)
and skills (policy). Neither is an authority you check an artifact *against*.
That is the layer this adds. Spec Kit calls it `constitution.md`; CAST calls it a
Solution Charter; Kiro calls the same altitude "steering". Name mapping recorded
so other people's work is readable.

**"charter" over "constitution":** constitution implies immutability, and the
retro loop exists to amend it.

### Acceptance test for the charter

> If a rule is compatible with every possible implementation, it is not a rule.
> If a rule does not change at least one downstream plan or diff, it is decoration.

Excluded from the charter: tool names, framework preferences, style rules,
package versions, unenforceable aspirations.

### What the charter holds

- **Invariants** — discrete, individually addressable, each naming something it
  forbids. A finding must be able to cite *which* invariant an artifact violates.
- **Finding taxonomy** — fixed, engine-owned, version-stamped. Stack packs may
  add sub-categories; they may never replace or extend the top level, or
  cross-project aggregation dies.
- **Open-decision register** — what is deliberately unsettled. Interrogation is
  forbidden from reporting these as violations. This is what stops clean-slate
  agents from re-litigating settled questions every round.
- **Interrogator roles** — the list of lenses (see §3).
- **Stack pack selection** — which pack, which version, and every deviation
  from it.

### Why deviations live in the charter

This is the mechanism behind "focus on the problem, specify only what differs":

1. Defaults live in a named, versioned artifact instead of prose an agent
   improvises around.
2. Deviations become interrogable, because the charter is what everything is
   checked against.
3. Deviations become retro-visible — override the same default in four solutions
   and aggregation tells you the default is wrong.

The charter names the pack and its deviations. The pack holds the versions.

**DECIDED: pack rules become blockable through one delegating invariant.** The
charter's acceptance test excludes tool names and framework preferences, so a
charter cannot say "prefer C# over Python" without violating its own rule. It
says instead that the declared pack governs, and forbids contradicting it without
a recorded deviation. A finding then cites that one invariant and *names* the pack
rule it broke.

This keeps `cites` to three kinds, keeps tool names out of the charter, leaves the
pack as the authority on tech — and means **one invariant makes every pack rule
blockable**, so no invariant is ever added per tech decision.

### The charter is not a living spec

They are different documents and neither substitutes for the other.

| | Charter | Living spec |
|---|---|---|
| Kind | normative — rules to obey | descriptive — behaviour |
| Example | "error paths are enumerated" | "POST /orders returns 201, or 409 on a duplicate key" |
| Read it and you know | the rules it was built under | what the software actually does |
| Shared across solutions | mostly yes | never |

Building code versus floor plan: one says what is permitted, the other says where
the rooms are. Two unrelated solutions can share a nearly identical charter.

This is also an argument for the deferral in §1. Charter plus code plus the
traceability table already covers rules, behaviour, and requirement→test→code. The
only thing a living spec adds is a human-readable summary of current behaviour —
exactly the part most likely to rot.

### One charter per pack, copied — not inherited

**DECIDED (2026-09-23): each pack ships its own charter, and a solution's charter is a
copy of it taken at `init` time. No inheritance.**

An earlier draft had solutions inherit a shared invariant set by version. That is
rejected for a reason stronger than simplicity: **inheritance would let an upstream
change alter the rules a solution is judged by, without that solution's approval.** It
would not even be a cycle doing it — it would be a different repository. That is worse
than the thing INV-08 forbids.

So copying is the *more* consistent choice. And improvement delivery still works; it
goes through the proper door — an improved pack charter reaches an existing solution as
a **charter intent in that solution**, which someone approves. `/sdlc:init` is that
mechanism (§6). Deliberate and local, exactly as designed.

**Drift between pack charters is expected and sometimes wanted.** Packs describe
different kinds of work on stacks that improve at different rates. INV-01 requires error
paths to be enumerated; an ML competition arguably has no error paths in that sense. A
test forcing the common invariants to be textually identical was considered and rejected
— it recreates precisely the coupling inheritance would have caused, since improving one
pack's wording would force the same change on every other.

**The real hazard was never duplication — it was ID collision**, and it is already
solved. Every retro carries `solution`, `charter-version` *and* `pack-version`, so
`cites: INV-03` is unambiguous: INV-03 of that solution's charter, at that version,
under that pack. Aggregation groups by **(pack, invariant id)**, with no ID prefixes and
no identity test.

Which surfaces something worth stating plainly: **cross-pack aggregation was always
suspect.** "Where does our process leak?" may have a genuinely different answer for a
library than for a competition, and pooling them hides that rather than revealing it.
Per-pack aggregation is the honest default; comparing across packs is a deliberate act
with the caveat understood.

### Invariant ids survive the copy

**DECIDED (2026-09-23):** ids are assigned once and never reused — **including across
the copy boundary from pack charter to solution charter.**

Without this rule: a pack ships INV-01 … INV-08, a solution copies them, drops INV-04
as inapplicable, later adds a local invariant, and if that local one is numbered INV-04
then retros citing INV-04 from this solution mean something entirely different from
retros citing INV-04 from another solution on the same pack. Per-pack aggregation breaks.

So:

- **Pack-derived invariants keep the pack's id exactly.** Gaps are preserved:
  `INV-01, INV-02, INV-03, INV-05`.
- **The gap *is* the record** that something was removed. Nothing else needs to say so.
- **Local additions use `LOC-nn`**, numbered independently and also never reused. A
  prefix rather than a suffix, because `LOC-01` is distinguishable from `INV-01` at a
  glance while a suffix has to be read to the end.
- **Neither is ever renumbered.**

Two edge cases:

- **A pack later drops an invariant the solution kept.** It stays under its original
  `INV-` id. Renaming it to `LOC-` would break every prior citation, and never-renumber
  wins. An `INV-` id the pack no longer has is mildly odd; `pack-version` disambiguates.
- **Gaps need provenance to be readable.** `INV-01, 02, 03, 05` is indistinguishable
  from a typo unless you know which pack charter it derived from — so `Origin:` carries
  the pack **and its version**.

### Start raw, thicken by evidence

**DECIDED:** the first charter is deliberately thin. It gets better by running
real cycles and absorbing retros, not by being written well up front.

One consequence, stated so it is not a surprise: the `cites` rule (§3) caps any
finding that cannot point at an invariant at `suggested`. A thin charter
therefore makes the interrogator **structurally toothless** — early cycles produce
almost nothing blocking and nearly everything lands in the suggestions pile.

That is the correct failure mode: permissive rather than obstructive, and
self-correcting, because the suggestions pile is the evidence for which invariants
are missing. But it means **the first cycles are calibration runs. Do not run
unsupervised until the charter can halt on something.** Unsupervised mode earns
its way in as the charter thickens.

The discipline that cannot be deferred: **invariant IDs are assigned once, never
reused, never renumbered.** Five invariants written that way is fine. Five
written as a bulleted list to be renumbered later poisons every retro that cited
them. Charter versions are append-only for the same reason — an old retro must
stay interpretable against the version it names.

### How the charter is interrogated

**DECIDED:** it can be, and it needs no authority above it. Three of the four
roles apply unchanged; only the `charter` role is inapplicable, and it is
substituted.

| Normal role | On the charter |
|---|---|
| charter | → **form** — does each invariant name something it forbids? |
| coherence | unchanged — can two invariants both be satisfied? |
| facts | unchanged — do its claims about the tree and stack hold? |
| gaps | unchanged — what should be governed and is not? |

The `form` role is the acceptance test above, applied per invariant, and it is a
property of the invariant's *shape* rather than a judgment about its content.

**DECIDED:** enforce it at write time instead of discovering it later. Every
invariant carries a mandatory non-empty `Forbids:` clause:

```
INV-04 · Microsoft-first runtimes
  Forbids: introducing a Python or bash script where C# or PowerShell can do the job.
  Evidence: <why this exists>
  Since:    charter v1
```

A missing or empty `Forbids:` fails the form check mechanically. This also makes
`cites: INV-04` meaningful, because a finding citing it must show the forbidden
thing happened.

### How the charter is amended

**DECIDED (2026-09-22): there is exactly one way, and no process can take it.**

> The charter changes only through a **charter intent** that a **human approves**.
> No automated process may approve one. No cycle may amend the charter governing it.

This replaced an earlier plan for a review *cadence*. A cadence answers "when do we
look?"; this answers "what justifies a change?" — which is stronger, because a
trigger cannot be forgotten and it binds the change to its evidence.

It is also what makes every trigger below safe by construction: they all funnel
through one chokepoint with a person at it. A cycle can request; only a human can
grant.

**Two layers, two answers on where editing is possible:**

| | Lives in | Editable where |
|---|---|---|
| Inherited invariant set — `mofoncode-invariants@vN` | ai-sdlc, shipped in the plugin | **only in ai-sdlc** |
| A solution's own charter — inherited version, local invariants, deviations | that solution | in that solution, human-gated |

So the shared set is read-only from inside any consuming solution. A solution's own
local invariants and deviations are its own business — but still only changeable by
an approved charter intent *there*. The one-way rule is uniform; only the venue
differs.

**Five triggers.** These replace the cadence.

| | Trigger | Signal | Timing |
|---|---|---|---|
| A | An invariant blocked work that was actually correct | a finding cited it and was dispositioned *accepted, won't fix* | **mid-cycle** |
| B | The same problem recurs with nothing citable to cover it | suggestion clusters on one theme | mid-cycle or after |
| C | An invariant never fires | zero cites across N cycles | retrospective |
| D | A pack or platform change made an invariant wrong | external | whenever |
| E | Delayed evidence points at a stage no invariant governs | `caused-by` attribution | retrospective |

A and B are the dangerous ones, because the cycle is blocked *now* and the
temptation is to amend the charter to get moving. That path is closed by the blocked
mechanism in §1: halt, file, discard the worktree, wait.

An amendment intent carries `source: charter-review`, goes through the same PR queue
as any other, bumps the charter version, and never rewrites history — a retro stamped
`charter v1` stays interpretable against v1 forever.

### Closing the delegation hole

INV-04 delegates to the pack, so the substance of the rule — which runtimes are
permitted — lives in `pack.yaml`, not in the charter. A cycle blocked by INV-04
could therefore edit the pack, satisfy INV-04 on its new terms, and never touch
`CHARTER.md` or file anything. The gate protected the file the rule is *written in*
and not the file the rule *points at*: a lock that validates names against a list
kept in an unlocked drawer.

**DECIDED (2026-09-22): the charter, the pack, and the pack rules are all protected.
Changing any of them requires an approved intent.**

Chosen over two alternatives because it replaces a *judgment* with a **path check** —
"did this diff touch these paths?" is a `facts`-role check with no interpretation —
and because it closes the hole by construction rather than by rule, leaving no
unprotected surface to route around.

Rejected: a **direction rule** (tightening free, loosening gated). Reads naturally,
but needs a judgment call on every change and goes fuzzy at the margin — is adding a
permitted project layout "loosening"? Rejected: **splitting the pack** into a
governed rules file and a free facts file. That split exists only to draw a gate
boundary, and protecting everything removes the need for the boundary.

**Two sources, two versions.** This is the one thing protecting everything gets
wrong on its own: a CVE bump is not a constitutional event, and if it bumps the
charter version then the charter version stops meaning anything. That matters
because the charter version is the *interpretation key for retros* — the amendment
analysis depends on being able to ask what changed between v3 and v4. Weekly patch
bumps would make that question unanswerable, and worse, you would stop reading
charter intents carefully because most of them are bumps. That is gate erosion, the
INV-06 failure arriving one layer up in the governance itself.

| Intent source | Changes | Bumps | Frequency |
|---|---|---|---|
| `charter-review` | `CHARTER.md` — invariants, taxonomy, `DEC`, roles, the inherited-version line | charter version | rare |
| `pack-review` | `pack.yaml`, `pack.rules.yaml` | pack version | routine |

Both human-approved, both one-way, both determined by **path** so neither needs
judgment.

**Boundary rules:**

- **Creating a pack is a normal cycle; modifying one needs the gate.** You cannot
  loosen something that does not exist yet — the tightening logic without the
  fuzziness.
- **The inherited-version line is charter content.** A cycle bumping
  `Stack pack: dotnet-library@v3` to `@v4` changes every rule governing it at once,
  so that is a `charter-review` intent in that solution. Same shape as the original
  hole, closed the same way.
- The `pack.yaml` / `pack.rules.yaml` split survives as an **evidence requirement,
  not a gate**: a `pack-review` intent touching rules must carry corpus-replay
  evidence, while one touching only facts needs just the reason — a CVE number, a
  release note.

### Charter health is a query, never a file

**DECIDED (2026-09-23):** the amendment signals are computed on demand by a read-only
skill. They are never stored in a committed document — that document would be a second
list beside the retro corpus with nothing binding them, INV-03 on the very artifact
whose job is enforcing INV-03. `facts.md` set the precedent: derivable things are
generated, uncommitted and regenerable.

You still get to look at it whenever you like. It just cannot drift.

**DECIDED: reporting and proposing are separate acts.** Reporting is read-only and has
no side effects, so it can be run casually. Proposing files a charter intent, which is
deliberate. One skill is fine; running it out of curiosity must never leave an intent
behind.

**DECIDED: no process may act on a signal.** Not a policy choice — INV-08 forbids it.
Deleting an invariant is a charter change, so the most any skill can do is file a
proposal a person approves or rejects.

What the query reports, per invariant: cite count, disposition breakdown, override rate
(`accepted-wont-fix` over total cites), and rejected-charter-intent count. Plus
per-role rejection rates for the noisy-lens signal and per-deferred-decision cite
counts. All derived, none stored.

**Reading a zero honestly.** Zero cites has three meanings calling for opposite
actions, so "candidate for deletion" was wrong:

| Why zero | Action |
|---|---|
| no violation can be constructed → **decoration** | delete |
| violations possible but undetectable → **unenforceable** | keep; make it checkable. Deleting fixes nothing and the violations continue |
| prevented upstream by a template or check → **working** | keep. Zero cites is the goal |

The third case is not hypothetical: put INV-05's file declaration in the plan template
and no plan will ever violate it. The optional `Prevented-by:` field on an invariant is
what lets the query distinguish that case instead of leaving it to interpretation.

And a caution about volume: an invariant like INV-06 fires only when someone tries to
suppress something, which may legitimately be once in fifty cycles. A low count there
says nothing. For the first months the corpus is too small for any of this to mean
anything — run the query early to build the habit, expect no conclusions before twenty
or thirty cycles.

**OPEN — four follow-ups to the amendment rule, none yet settled:**

1. ~~**The `DEC` register.**~~ **CLOSED 2026-09-23.** Adding an entry is already
   gated, because `DEC` lives in `CHARTER.md` and that path is protected. What
   remained was how entries behave, and the answer replaced blanket suppression with
   the existing state routes:

   - A deferred decision does **not** suppress findings. A finding in its area is
     capped at `suggested`, cites the entry, and accumulates as evidence — which
     gives `DEC` entries a cite count for free.
   - Unless the intent **cannot complete** without the answer. Then it is `blocking`
     and escalates for a decision.
   - A blocking `DEC` finding is always `blocking`, never repairable: no artifact
     edit resolves "nobody has decided this yet", so it skips self-heal entirely.
   - The judgment — *can this proceed without the answer?* — is safe in both
     directions. Too permissive and interrogation catches the consequence downstream
     as `missing-case` or `ambiguity`; too restrictive and you get an interruption you
     did not need. No silent failure either way.
   - Entries carry `Since:`, so an entry added once and never cited again is
     visible as dead weight rather than quietly suppressing forever.
   - Repetition across cycles is the **signal**, not noise: twenty cycles citing the
     same entry is the strongest evidence it needs deciding. Aggregation collapses it
     to one line.
   - **`DEC` entries are "deferred" decisions, never "open"** — `open` is a finding
     state, and one word cannot mean both.
2. **Corpus replay in the charter-intent template.** A blocked cycle writing its own
   charter intent is arguing for its own unblocking, and will argue well. Replaying
   the proposed change against the existing findings corpus — how many past findings
   would it convert, invalidate, or leave untouched — turns the acceptance test from
   a judgment into a measurement. Depends on aggregation tooling that does not exist
   yet.
3. ~~**Whether the amendment rule becomes INV-08.**~~ **CLOSED 2026-09-23: yes.**
   Every other rule in the charter is enforced by being citable, and the rule
   protecting the charter being the sole exception is backwards — it is the rule with
   the most to lose. Without it, a cycle that edits its way around an invariant leaves
   no finding behind. INV-08 is not part of the seven: those were selected to make
   every category blockable, this one protects the mechanism, so it neither
   strengthens nor weakens their reasoning. A charter intent touching INV-08 itself
   must say so in its title — the self-reference cannot be closed, so it is made loud.
4. ~~**Finding an invariant nothing ever cites.**~~ **CLOSED 2026-09-23.** Four of the
   five triggers announce themselves — a blocked cycle, a growing suggestions pile, a
   change you made, an arriving intent. The fifth produces no event at all. You can be
   prompted about what happens; you have to go looking for what does not. One query —
   *which invariants have zero cites, and which deferred decisions are old with zero
   cites* — runs whenever a charter intent happens for another reason, with a floor of
   every ten cycles. This is the only place a schedule survives, and only because
   absence cannot trigger anything.
5. ~~**When the amendment rule commences.**~~ **CLOSED 2026-09-23:** when the first
   cycle runs against the charter. Before that there is no process to protect it from,
   and the document is a draft being written by hand.

Note also the self-reference: this rule lives in the charter, so a charter intent
could amend it. There is no clean escape and entrenchment machinery is not worth
building at this scale — but it should be *visible*, so any charter intent touching
the amendment rule must say so in its title. Quiet removal is the only failure mode
that matters.

---

## 3. Interrogation

**DECIDED:** two distinct skills. Conflating them makes unsupervised mode
impossible, because every interrogation would need a human present.

| | `intent` | `interrogate` |
|---|---|---|
| Target | the human | the artifact |
| Direction | pulls what is in your head onto paper | pushes an artifact against the charter |
| Mechanism | `AskUserQuestion` | reads files, runs checks |
| Output | `intent.md` | schema-validated findings |
| Human required | always | never |
| Fails by | not asking enough | inventing findings |

`grill-me` exists publicly (~1M installs) and does this job well. Patterns
worth adopting: `AskUserQuestion` always, one question at a time, 2–4 realistic
options, **a strawman answer attached to every question so the human reacts
rather than composes**, and investigating the codebase rather than asking what
can be looked up. It is a poor `interrogate`: it targets the human, writes no
file, and its stopping condition is "all branches resolved" — the exact
non-convergent condition v01 measured.

**DECIDED:** `intent` asks the questions and **writes `intent.md` itself**, on
branch `intent-<id>`, then opens the PR. The PR is the review: you read the
artifact in its final form, in a diff, where review belongs. A bad interview is
cheap to discard — close the PR and re-run.

Rejected: summarising the decisions in conversation for approval *before*
writing. It approves a proxy for the artifact rather than the artifact, and it
would make this the only stage in the system with two gates. Every other gate
approves an artifact; the uniform rule is worth more than the earlier catch.

### Roles

**DECIDED:** fan out by *question asked*, not by *area*.

| Role | Its one question | Evidence it needs | Fails by |
|---|---|---|---|
| charter | Does this contradict a charter invariant? | the charter | over-matching |
| coherence | Does this contradict itself? | the artifact alone | missing distant contradictions |
| facts | Do its claims about the tree hold? | the tree, via commands | not checking enough |
| gaps | What is missing that should be here? | domain reasoning | inventing requirements |

Three reasons for separate roles:

1. **Different evidence.** Each role's entire context is one kind of evidence.
   One agent doing all four interleaves them badly — it starts checking facts,
   finds a broken path, and never reaches "what is missing". Attention is finite
   in an agent as in a person.
2. **Different failure modes.** They fail in opposite directions, so they want
   different instructions and different tuning.
3. **Separately measurable.** After twenty cycles the retro can say "the gaps
   role produced two findings, both rejected" — and you drop it on evidence.
   With one merged agent, a finding cannot be attributed to a method, so the
   method can never improve.

**Not** by area (security / performance / architecture): area roles overlap
constantly and dump deduplication on the merger. Question roles look at
different evidence entirely, so they rarely collide — which is also why they
have nothing to debate, and therefore why agent teams are not needed here.

**DECIDED:** roles are data in the charter, not hardcoded in the skill.

### State and grace

**The values are defined in `.sdlc/CHARTER.md`, not here** — they are a normative
enum the findings schema depends on, and listing them twice would be INV-03. This
section holds the reasoning. As of charter v1 there are three: `open`, `blocking`,
`suggested`.

**DECIDED: discovery and disposition are separate acts.** An interrogator notices
something is wrong *before* it knows whether a person is needed, so state is
assigned in two steps rather than guessed at once. A finding starts `open` and is
triaged on a later round into `blocking` or `suggested`.

The earlier design required a citation *at discovery*, which was backwards: an agent
notices a problem first and locates the invariant second. Under that rule a real
problem whose citation had not yet been found was filed as a suggestion and capped
there permanently.

**No cycle may land with an `open` finding outstanding.** That is the completeness
guarantee — nothing untriaged escapes.

**DECIDED: self-heal is a property of a `blocking` finding, not a state.** A
blocking finding that carries a concrete `repair` gets self-heal attempts, under
no-progress termination; one with no repair, or whose repair fails, escalates to a
person. This collapses what were four states into three, with no behaviour lost.

The cycle therefore stops only on the unambiguous. Everything arguable either heals
itself or waits for a human who is not being interrupted.

#### The anti-inflation rule

This is what decides whether the self-heal loop converges. If an interrogator
assigns its own state freely it will inflate — everything becomes urgent, the loop
runs forever, and v01's non-convergence returns wearing a new label.

**DECIDED:** state is *earned*, and the schema enforces it. Every finding carries
`cites`, which accepts exactly one of three kinds:

- a **charter invariant ID**,
- a **failed mechanical check ID**, or
- a **location pair** — two or more positions in the artifact that demonstrably
  conflict. Permitted for `internal-contradiction` only.

The third kind was added after designing the taxonomy: a contradiction you can point
at both halves of is self-evident and should be able to block, but it has no
invariant and no failed check to cite.

**DECIDED: the citation is required at promotion, not at discovery.** An `open`
finding cannot become `blocking` without one; uncited, it becomes `suggested`. Better
placed than before — at discovery an interrogator is pattern-matching, at triage it is
investigating, and asking for a citation before it has looked is asking it to guess.

**DECIDED: mechanical findings skip `open` entirely.** The failed check *is* the
citation, so there is nothing to triage and they are born `blocking`. Only
`detection: reasoning` findings start at `open`. Since roughly 70% of findings were
mechanical in v01, most never enter triage — which is what keeps the triage round's
workload bounded.

**DECIDED: an `open` finding that will not triage defaults to `blocking`** — two
rounds with no progress and a person looks. When in doubt, escalate.

This inverts the burden. An agent must point at something specific to stop the cycle;
vague unease is a suggestion by construction.

#### Dispositions

**State says where a finding stands and whether it halts the cycle; disposition says
what was done about it.**
Both are carried on every finding, and nothing is left without a recorded outcome.
The five values are defined in the charter.

They exist because **every amendment signal is computed from dispositions** — that is
their entire job. Each disposition feeds exactly one signal, which is a decent check
that the set is complete:

| Disposition | Signal |
|---|---|
| `fixed` | none — the normal case |
| `rejected` | a high rate for one role marks that **role** noisy; evidence for dropping a lens |
| `accepted-wont-fix` | **override count** → the invariant may be too broad |
| `deferred-to-dec` | **`DEC` cite count** → that subject needs deciding, not deferring |
| `promoted` | none — a suggestion became its own intent |

**`rejected` and `accepted-wont-fix` must never be merged.** Dismissing a bogus
finding would otherwise inflate the override count and narrow a perfectly sound
invariant.

**Timing.** A `blocking` finding is dispositioned before landing. A `suggested` one is
dispositioned later, when the suggestions file is triaged — requiring it at landing
would defeat the purpose of suggestions.

**Where they live.** On the finding: recorded in `verify.md` as of the gate, and
carried in `retro.md` for the corpus. The retro is what travels between repos, so it
is what the amendment signals are computed from — `verify.md` files stay in their
solution and ai-sdlc never sees them. **Never** stored on the `DEC` entry: that would
be a second list beside the corpus, and the charter would grow with every cycle.

#### Suggestions become intake

**DECIDED:** at landing, unresolved suggestions are written to
`suggestions/<cycle-id>.md` — one file per cycle, never a shared backlog file,
because a file every cycle appends to conflicts exactly the way `plugin.json`
does.

A human triages on their own schedule. A promoted suggestion becomes an ordinary
intent carrying `source: suggestion`, entering the same PR queue as a maintain
finding. Same intake, different origin, so no second mechanism.

### Finding taxonomy

**DECIDED:** three small cross-tabulating enums, not one long list. The retro
asks *where the process leaked*, not what kind of thing was wrong — a security
bug that shipped is interesting because the spec never mentioned security, or
because verify had no check for it.

| Axis | Values | Answers |
|---|---|---|
| **category** | the eleven below | what shape of defect is this? |
| **attributed stage** | charter \| intent \| spec \| plan \| build \| verify | which artifact should have prevented it? |
| **detection** | `check` \| `reasoning` | did a mechanical check fail, or did an agent infer it? |

The attributed stage may be the current stage *or an upstream one*, and upstream
attribution is the high-value signal. This is also why there is no
"upstream-defect" category — the axis already expresses it.

The detection axis makes constraint 2 measurable per cycle rather than measured
once: it tracks whether fact-gathering is absorbing more of the load over time.

**The categories themselves are defined in `.sdlc/CHARTER.md`, not here.** They are
a normative enum the findings schema depends on, and listing them in two places
would be INV-03 on its first day. This section holds the design reasoning; the
charter holds the list. As of charter v1 there are eleven, one of them
`unclassified`.

**Tie-break:** when two categories fit, take the **more mechanically checkable**
one. "Performance should be acceptable" is both `untestable` and `ambiguity` —
file it `untestable`, because that produces a citation and a sharper count. The
bias toward blockable findings is deliberate.

**`charter-violation` is a last resort.** If a finding has a shape, use the shape
and put the invariant in `cites`. A missing error path that violates an invariant
demanding error paths is `missing-case` citing that invariant. Reserve
`charter-violation` for what fits no shape and is simply forbidden.

**`unclassified` is deliberate.** With no escape hatch, agents mis-file into the
nearest category and corrupt the counts silently — worse than an honest bucket.
So it exists, it can never block, and **its rate is a health metric for the
taxonomy itself**: at 15% of findings the taxonomy is missing a category, and the
finding texts are the evidence for which one. Same growth mechanism as the
charter.

**Category never determines state.** State comes from the `cites` rule
alone. Two rules governing state would fight, and the easier one would win.

What the cross-tabs buy: `unresolvable-reference` concentrated in `plan` means the
plan template needs a path validator, not better prompting. Detection ratio over
time says whether pre-checks are working. Attributed stage restricted to
`caused-by` evidence says where the process actually leaks, on the only unbiased
signal available.

### Mechanics

**DECIDED:** dynamic workflows, not subagents and not agent teams.

| | Subagents | Agent teams | **Workflows** |
|---|---|---|---|
| Who holds the plan | Claude, turn by turn | the lead, turn by turn | **the script** |
| What is repeatable | the worker definition | the team definition | **the orchestration** |
| Structured output | no | no | **schema-validated JSON** |
| Interruption | restarts the turn | teammates keep running | **resumable** |
| Works in `-p` / SDK | yes | **no** | **yes** |
| Ships in a plugin | agent definitions | agent definitions | **`workflows/`** |

Deciding factors:

- **Schema-validated output.** `agent(prompt, { schema })` returns validated
  JSON. The finding taxonomy becomes an enum enforced at the boundary rather
  than a convention agents are trusted to follow. This is what makes the retro
  loop countable instead of prose to be parsed.
- **Agent teams cannot spawn in `-p` or the Agent SDK at all**, which alone
  rules them out for unsupervised mode. Also: experimental, no session
  resumption, no worktree isolation between teammates, task status lags, split
  panes unsupported on Windows Terminal.
- Resume maps onto the repair loop: completed agents return cached results; the
  first agent whose prompt changed re-runs, and everything after it.
- Fan-out agents matching on model/effort/type/tools/schema/cwd share a
  prompt-cache prefix, and the runtime staggers them deliberately so all but the
  first read the cache.

**DECIDED:** agent teams stay available for interactive design debate — grilling
the charter itself, a genuinely ambiguous root cause — deliberately outside the
cycle. Their unique capability is teammates challenging each other directly,
which orthogonal roles do not need.

**DECIDED:** fact-gathering runs **once, in the parent**, and is passed to every
role agent. Otherwise each clean-slate agent independently rediscovers the same
paths and counts, and clean slate becomes more expensive than one session rather
than less.

**DECIDED:** deduplication and state are the merger's job, not each agent's.
Four agents will report one problem in four wordings; a concatenated findings
file inflates the taxonomy permanently.

Known cost of clean slate: an unbiased agent is an uninformed one and will
re-litigate settled decisions. The fix is the charter plus the decision register
as *documents* — artifacts inform without anchoring, because they state
conclusions rather than reasoning. Handing over the main conversation would
reintroduce the bias being avoided.

Known limitation: parallel clean-slate agents are non-deterministic, so the
findings set differs run to run. The schema constrains shape, and the mechanical
facts are deterministic, so statistics count *categories*, never instances.

### Observed 2026-09-22 — one real workflow run

`/deep-research` was run once against the ADO question in §10, as a calibration
exercise. It is structurally this interrogator: fan out, cross-check
adversarially, filter claims that do not survive, emit one cited report.

**Cost:** 98 agents, 769 tool calls, 5.13M subagent tokens, 14 minutes, zero
errors. Calibration, not a target — a bundled workflow sizes itself, and our
per-artifact interrogation is four role agents plus a merge. Worth knowing that
the runtime will spawn 98 agents if the script says so, and that
`workflowSizeGuideline` is what bounds it.

**The finding that matters for our design:** five claims asserting something
*true* — that marketplace auth delegates to the ambient git credential stack —
were voted down 0-3 by the adversarial verifiers. They were refuted for **weak
citations, not for being wrong**. The substance survived only because it appeared
inside the evidence of other claims that did pass.

That is the `cites` rule (§3, state) operating in the wild, and it confirms
both halves of it. Requiring a citation genuinely suppresses unfounded findings —
which is what we want. It **also** suppresses true findings that happen to lack a
citable invariant, which is the cost we accepted. The consequence is that
`suggestions/<cycle-id>.md` is not a courtesy bin: it is where true-but-uncitable
findings land, and human triage of it is load-bearing rather than optional.

---

## 4. Retro — a constraint, not a stage

**DECIDED:** retro is cross-cutting. It imposes requirements on every stage
rather than arriving after them. It is not a section of the roadmap and it is not
part of Maintain.

| | Maintain | Retro |
|---|---|---|
| Subject | the **software** | the **process** |
| Asks | is it still doing what the spec said? | did the cycle that built it work? |
| Trigger | a production or dependency signal, whenever | cycle completion, once |
| Goes to | a new intent in that repo | an improvement to ai-sdlc |
| Lifetime | forever | once per intent |

If retro were part of Maintain you would only get retros for cycles that had
incidents — excluding every cycle that went well, which is the control group.

### Two classes of evidence

**Immediate** — self-assessed at cycle close. Cheap, and structurally
optimistic: "did my plan work?" asked ten minutes after the plan passed its
tests is a biased question.

**Delayed** — attributed upstream from a later intent. Objective. Weighted
higher.

Mechanism: retros are immutable, one per intent. When a maintain signal becomes a
new intent, that intent carries:

```yaml
---
id: intent-0042
source: maintain
caused-by: intent-0031
caused-at-stage: spec
---
```

Aggregating gives *"of 40 cycles, 6 produced downstream intents; 4 of those
attribute to the spec stage"* — a statement about the process that no
self-assessment could produce, because at close time all 40 looked fine.

**DECIDED:** `caused-by` accepts an explicit `unknown`, and attribution is never
forced. A guessed attribution is worse than a blank one, because it pollutes the
only unbiased number available.

### What the loop needs from day one

Retrofitting any of these is expensive, so they are requirements on cycle 1:

1. **Structured output from `interrogate`**, even before anything consumes it.
   Otherwise the output gets designed for human reading and rewritten later.
2. **A fixed, engine-owned taxonomy.** Statistics only aggregate if every retro
   uses the same categories.
3. **Charter version stamped into every retro.** A retro is only interpretable
   against the charter in force when the cycle ran.

**Nobody else does this.** Spec Kit and Kiro both stop at the project boundary;
the playbook's own loop carries findings about *the software* back, never
findings about *the process*. This is the differentiator.

### Transport

**DECIDED (2026-09-22): manual, one skill, idempotent by id.**

```
/sdlc:absorb-retro <path>
```

`absorb-retro` takes a path. If it is a file it absorbs that retro; if it is a
directory it absorbs every retro in it the corpus does not already hold. Either
way it deduplicates by cycle id, files each into `retros/<solution>/<cycle-id>.md`,
reports the count, and opens a PR.

There is one piece of logic, not two. The directory form is a loop over the
single-file case — no reconciliation, no deletion, nothing bidirectional or
stateful. Idempotence comes from the id check, not from the directory.

The single-file case is the normal one: you are standing right there when a cycle
lands. The directory form only buys convenience — land three cycles over a week
without absorbing any, and you point at the repo once instead of hunting for three
files.

Rejected: **cross-repo automation.** The landing phase pushing a PR into ai-sdlc
needs a bot identity, stored credentials and error handling, for a loop that runs
twice a month. Revisit only if manual absorption is actually being skipped.

Rejected: **a "retros processed" ledger.** It is a second list beside the `retros/`
directory, and the directory is derivable by listing it. A ledger is only necessary
if aggregation maintains running counts; **aggregation is a full recompute** over
the corpus, which is trivial at fifty files a year and needs no state of its own.

Rejected: **a gap check on cycle-id sequences.** This was the original plan and it
does not work. Cycle ids are assigned at intent *creation*, so completion order is
arbitrary — with parallel cycles, cycle 4 routinely lands before cycle 3. A gap
therefore means either "not yet absorbed" or "not yet finished", indistinguishably.
Re-running absorb removes the need for it anyway.

**DECIDED: retros stay committed in the solution repo.** Not gitignored — their
presence there is the completion certificate (§7), and gitignoring them
re-introduces the defect where a dispatcher relaunches finished cycles.

**DECIDED: identity and chronology are different fields.**

| | What it is | What it is not |
|---|---|---|
| `cycle-id` | identity, unique within a solution | any indication of completion order |
| `landed-at` | chronology — the only field aggregation orders by | derivable; ai-sdlc's git date is the *absorption* date |

**DECIDED: retro frontmatter carries five fields**, each because it cannot survive
the copy any other way:

| Field | Why it must be in the file |
|---|---|
| `cycle-id` | identity, and the target of `caused-by` links |
| `solution` | partitions the corpus |
| `landed-at` | not derivable — the landing date lives in the solution repo's history, which ai-sdlc does not have |
| `charter-version` | a retro is uninterpretable without the charter in force when it ran |
| `pack-version` | same reason, for the half of the rules that INV-04 delegates. `none` for ai-sdlc, which consumes no pack |

The general rule behind that column: **a fact that must survive being copied out of
its original repo has to be written into the artifact.** Git metadata does not
travel.

**DECIDED:** `absorb-retro` refuses to overwrite an existing corpus entry with
different content. That is what makes the standing INV-03 exception enforced rather
than merely argued: a copy of an immutable artifact is not a second list, because
the two cannot be allowed to diverge.

---

## 5. Communication — a charter clause, not a capability

Artifacts stay indexed. Conversation does not. When referring to a requirement or
invariant, lead with what it says; the label is trailing detail, never the whole
reference.

This cannot be built or tested, only restated. It is a charter clause and its
cost is repetition, not implementation. Do not let it appear in a list of
buildable things.

---

## 6. Engine and stack packs

**DECIDED:** the engine owns every verb. Stack packs contribute **data** to most
of them and **behaviour** to exactly two.

| Verb | Needs stack knowledge? |
|---|---|
| `intent` | no |
| `intent` | no |
| `spec` | barely — acceptance-criteria vocabulary at most |
| `interrogate` | **data** — the pack supplies checks, not a variant interrogator |
| `plan` | **yes, data-driven** — "a vertical slice" means specific files |
| `scaffold` | **yes, code** — creating files and running `dotnet new` is execution |
| `retro` | no, and must not (taxonomy is engine-owned) |

**DECIDED:** `plan` is data-driven, `scaffold` is code, nothing else gets code
until it proves it needs it.

Why data by default: adding a stack means writing one manifest instead of N
scripts; an engine improvement benefits every pack for free instead of being
ported to each and drifting; the pack is itself an artifact that can be
interrogated and blamed in a retro. Code-per-verb-per-stack is five lists beside
one source of truth — v01's constraint 3 in a new costume.

**DECIDED:** build the seam, not the machinery for the seam. One real pack, a
manifest the engine reads. No abstract base, no registry, no resolver, no plugin
API until a second pack forces it. The seam is justified at N=1 not by
multi-stack support but by keeping tech-stack churn out of the engine.

**DECIDED:** the term is **stack pack**, never "vertical". `MofonCode.Core`'s own
description says "the analyzers that keep a vertical slice in shape" — vertical
slice is already an architectural pattern inside the solutions, and reusing the
word guarantees permanent ambiguity in the artifacts that most need precision.

**DECIDED:** engine and stack packs need different maintenance mechanisms. Engine
defects surface in retros. Stack staleness does not — no retro says "Aspire is a
minor version behind". Packs need a refresh cadence and a CVE watch; that is a
Maintain signal for the framework itself.

### `init` — an interview that produces a charter

**DECIDED (2026-09-23): one command, `/sdlc:init`, idempotent.**

Not two verbs. An earlier draft split `scaffold` (new) from `adopt` (existing), on the
grounds that you need to know whether a charter was generated or inferred. But that is
what `Origin:` records, permanently, whichever path wrote it — solving with a command
name what a field solves better. Making `init` re-runnable turns it into one operation
rather than two sharing a name: *bring this solution into conformance.*

**It is an interview first and a generator second.** Nothing in an empty directory says
which pack governs it, so `init` cannot scaffold before it asks. Files are a side effect
of knowing the pack; the charter is the output.

| Starting state | What `init` does |
|---|---|
| empty directory | ask, then scaffold the structure and copy the pack's charter. `Origin: scaffolded from dotnet-library@v3` |
| existing code, no charter | infer, confirm, write the charter, establish the baseline. `Origin: adopted — inferred at <sha>, baseline 47 findings` |
| already initialised | reconcile against the pack's current charter |

**The two uninitialised cases differ only in how much can be proposed, not in whether
`init` asks.** On an empty directory every question is open. On an existing codebase the
`.csproj` files, `Directory.Packages.props` and the `.sln` are evidence, so `init`
proposes — *"this looks like a .NET library on net10.0 with central package management;
use `dotnet-library@v3`?"* — and you confirm or correct. That is the pattern adopted from
grill-me: investigate what can be looked up, ask only what cannot.

### `init` asks the minimum, deliberately

**DECIDED: `init` does not conduct a full interrogation.** It asks what is needed to
produce a working charter and nothing more:

- **which pack** — required, because nothing else can tell it
- **the solution name** — needed for scaffolding and for every retro's `solution` field
- **whatever the pack's manifest declares as required inputs** — a root namespace, a
  target, a package id. Stack-specific questions live in the pack, not in `init`, which
  keeps `init` generic and follows the data-not-code rule

Everything else in the charter arrives from the pack verbatim.

**Why not ask more.** The tempting extra questions are *which invariants do not apply
here* and *what deviations will this solution need*. Both are guesses at `init` time. You
discover that INV-01 does not fit on cycle three, when it blocks something
inappropriately — and at that point the amendment machinery does exactly the right thing:
the cycle halts, files a charter intent, you approve dropping it, and the `INV-` gap
records the decision.

Asking up front replaces evidence with guesswork, in the one document whose entire design
is *start raw, thicken by evidence* (§2). A full interrogation at `init` would pre-load
assumptions into the thing built to learn from what actually happens.

### Preconditions — refuse, explain, offer, never chain

**DECIDED:** running a stage whose input does not exist does not auto-create the input.

> A stage refuses when its input is missing, names what is missing, and offers the
> command that produces it. It never chains.

Two levels, failing differently:

| Level | Check | On failure |
|---|---|---|
| solution | does a charter exist? | offer `/sdlc:init` |
| stage | does this stage's input artifact exist? | offer the stage that produces it |

So `/sdlc:intent` in an ungoverned repository says *"no charter here; `/sdlc:init` creates
one — it will ask what this solution is and which pack governs it. Run it now?"* — and if
you say yes, **`init` runs and stops.** You return to the intent deliberately.

Stopping rather than chaining is not friction. `init` produces the document everything
else is judged against, and making it a side effect of wanting to write a feature means
foundational decisions taken with your attention elsewhere. **A careless charter is worse
than no charter, because it looks like authority.** And the intent you write *after* the
charter exists is a better intent, because the charter states the constraints it has to
satisfy.

Two properties fall out. The check is **mechanical** — "does the input artifact exist?"
is a `facts`-role check with no judgment. And the stage order becomes
**self-documenting**: you cannot get lost, because whatever you type tells you what is
actually next.

The charter gates everything, `/sdlc:run` included — which carries a second precondition
of its own: approved intents on master. None, and it says so.

### What `init` cannot do, and what it files

**`init` cannot edit an existing charter.** Changing a charter requires an approved
charter intent (INV-08), so reconciling means `init` **files a charter intent** carrying
the proposed diff, which someone approves or rejects. That is the improvement-delivery
path promised in §2, and it needed no new design.

Whether `init` generates intents, by case:

| Starting state | Intents filed |
|---|---|
| empty directory | **none.** It produces structure and a charter; the first intent comes from you |
| existing code | **none** — and this is the temptation to resist. Adoption produces dozens of findings, and turning them into intents would flood the queue on day one. They become a baseline plus a suggestions file, and *you* promote the ones worth doing |
| already initialised | **exactly one** — the charter intent carrying the reconciliation diff |

`scaffold` survives as the pack's file-creating capability that `init` invokes. `adopt` is
not a verb — it is what `init` does when it finds existing code.

**`init` and `intent` share the pattern, not the machinery.** Both grill a human and write
an artifact, but the questions have nothing in common — *what is this solution* versus
*what do you want to change* — and the only shared element is `AskUserQuestion`, which is
a tool rather than machinery. Forcing an abstraction over two unrelated interviews would
be the premature-abstraction mistake again.

**A charter is a precondition, not an optional extra.** There is never a cycle without
one; producing it is the first thing `init` does. A repository that has not been
initialised is simply not governed, and the absence of a charter is the signal — nothing
needs to record "not adopted".

For completeness, since it shows how much the charter carries: without one, `coherence`,
`facts` and `gaps` still work and two of the three citation routes survive — failed
mechanical checks and location pairs. You would still catch unresolvable references,
false claims about the tree, scope drift, gate bypasses and contradictions. What you lose
is everything requiring a rule: missing cases, ambiguity, duplicated truth. That is the
calibration period taken to its limit, and not a mode worth offering.

### Adoption needs a baseline

**DECIDED:** `init` on an existing codebase produces a mountain of findings — every
pre-existing second list, every unjustified suppression, every missing error path. Under
the state rules most would be `blocking`, which would mean the solution could not run a
single cycle until the whole backlog cleared. Unworkable.

So at adoption, existing violations are recorded and dispositioned `accepted-wont-fix`
with the reason *"pre-existing at adoption"*. They do not block. New violations do.

**The ratchet already exists, just narrower.** The suppression-count rule says an
*increase* in suppressions is itself a finding. Generalised: **the baseline count may not
increase.** Same mechanism, wider scope. An adopted codebase starts wherever it is and
can only improve, which is the only workable way to bring enforcement to existing code.

The baseline is derived once at adoption and immutable thereafter, so it is not a second
list.

**OPEN:** defaulted stacks to build. v01 shipped only `web` and `competition`.
Wanted: .NET API, .NET web, .NET library, ML, ML competition. The packages are
already pinned; the generators are not written.

---

## 7. Parallel execution

### Two drivers, one cycle

**DECIDED:** supervised and unsupervised are the **same stages with a different
driver**. They never diverge into two implementations.

Each stage is a workflow, and a workflow is a command. So:

- **Supervised** — you invoke one stage, read its output, invoke the next.
  `/sdlc:spec`, look, `/sdlc:interrogate`, look, `/sdlc:plan`. You are
  the driver. This is the default, and it is also the debugging mode: when
  something goes wrong unsupervised, re-run that one stage supervised and watch.
- **Unsupervised** — author N intents, merge them, hit go. `/sdlc:run` invokes the
  same stages back to back without stopping. It is the driver.

**DECIDED (2026-09-23): supervised needs no verb.** It is the absence of a driver —
you type the stage commands yourself. There is nothing to enter or exit, which is
what "same stages, different driver" already implies.

This is a charter-level invariant, not a convenience. If supervised meant
"run the verb skills by hand" and unsupervised meant "a separate automated
cycle", there would be two code paths, they would drift, and the one used less
would rot silently. The only permitted difference is **who calls the next
stage**.

### Running unsupervised

**DECIDED (2026-09-23): one command, and no concurrency cap.**

```
/sdlc:run 20260923-a7f3        one intent
/sdlc:run <id> <id> <id>       named intents
/sdlc:run --all                every approved intent
```

**No cap**, because a cap is the engine guessing at a resource question it cannot
know — your machine, what else is running, how much disk is free. Informed consent
at launch beats an invented ceiling, and it is consistent with everything else here:
gates are human decision points, not automated limits.

**A confirmation before starting**, listing the intents and **how many worktrees will
be created**. Worktree count is what predicts the thing most likely to bite, which is
disk: eight fresh checkouts each holding its own restore is the number you want in
front of you before saying go.

One command rather than two, because single and batch are the same operation with a
different N. The landing phase serializes when N > 1 and trivially when N = 1, but
that is a property of landing, not a second mode. The difference that matters to *you*
is visible in the confirmation, which is where it belongs.

Held in reserve, not built: the pack could later supply a cost hint so the
confirmation reads "8 worktrees, roughly 4 minutes of build each" rather than a bare
count. Disclosure rather than restriction — the useful residue of the cap idea.

**Consequence of having no cap.** Resource exhaustion becomes likelier, and a cycle
that dies mid-run is otherwise indistinguishable from one still working — both are
"branch exists, unmerged, no `verify.md`", so a dead cycle would sit in that state
forever while the dispatcher refuses to touch it.

**DECIDED: the worktree lock distinguishes them.** Claude Code holds a
`git worktree lock` while an agent runs and releases it when the agent finishes. So
**locked = running**, and **unlocked with no `verify.md` = dead**, and a dead cycle is
relaunchable or discardable. Derived, no status field, and it uses a mechanism that
already exists.

### Branch model

**DECIDED:**

```
Phase 0 — AUTHORING                          interactive
  intent -> intents/<id>.md on branch intent-<id>
         -> PR -> approve -> merge to master
  Gate: the merge. This is the human decision.
  No worktree: one markdown file, no build.

Phase 1 — DISPATCH                           automated
  read master for intents with no branch
  predicted tier from the intent -> Tier 1 only runs unsupervised
  launch one run per intent:  claude -p --worktree cycle-<id>

Phase 2 — THE CYCLE                          automated, in the worktree
  spec -> interrogate -> plan -> interrogate -> build -> verify
  each stage its own workflow; none can ask a question
  halts on: no-progress, or actual tier > predicted tier

Phase 3 — LANDING                            serialized, one at a time
  rebase on master
  re-run verify              <- catches semantic collisions
  bump plugin.json here      <- not in-cycle
  PR -> merge -> retro emitted

Phase 4 — ABSORPTION                         interactive
  retros flow back to ai-sdlc
```

**DECIDED:** intent lifecycle state is **derived, never written into the file**.
Writing `status:` into the artifact is a second list beside git.

The naive derivation is broken, and it would have been a live defect: *"intent on
master with no branch = queued"* also matches a **completed** intent, because the
cycle branch is gone once it has merged. An unsupervised dispatcher reading that
queue would relaunch every finished cycle, forever.

**The retro is the completion certificate.** It is written only at landing, it is
already committed, and it cannot exist for work that has not finished:

| Branch | `verify.md` on branch | `retro.md` on master | `blocked-by` | State |
|---|---|---|---|---|
| none | — | none | none | **queued** |
| exists, unmerged, worktree **locked** | absent | none | none | **in progress** |
| exists, unmerged, worktree **unlocked** | absent | none | none | **dead** — relaunch or discard |
| exists, unmerged | **present** | none | none | **awaiting landing** |
| none | — | none | **unmerged charter intent** | **blocked** |
| — | — | present | — | **done** |
| none | — | none, file under `intents/abandoned/` | — | **abandoned** |

No new field, no branch retention, no extra mechanism. The retro is not a
duplicate of a status — it is the evidence that the cycle closed, the way a
receipt is not a second copy of a price.

`verify.md` cannot serve as the certificate, for two reasons: it is written in the
worktree on the cycle branch, so a dispatcher reading master never sees it; and it
can exist for a cycle that **never landed** — escalated on blast radius, PR
rejected, or killed by a rebase. The two artifacts answer different questions.
`verify.md`: did it pass? `retro.md`: did it land?

**Awaiting landing** is the state that distinction buys, and it is the one worth
surfacing most: it is where an escalated Tier-3 cycle waits for a human. Without
it the dispatcher cannot tell "still working" from "done working, needs you".

**DECIDED:** the retro is therefore written to master *after* the merge, as its own
small commit, not carried in on the cycle branch. That is also what lets it record
the landing itself — rebase conflicts, and the result of the post-rebase re-verify.

Abandonment is the one state with no evidence of its own, so it takes one explicit
act: move the file to `intents/abandoned/`.

### Two branches per intent

**DECIDED (2026-09-23):** the intent gets its own short-lived branch, merges, and only
then does the dispatcher create the work branch.

```
1  branch  intent-20260923-a7f3
2  write   intents/20260923-a7f3.md on it
3  PR
4  approve → merge to master
5  branch  cycle-20260923-a7f3        ← created by the dispatcher
6  spec, plan, build, verify all happen here
```

Same id, different prefix. Both hyphenated, because the cycle name doubles as the
worktree directory and a slash there would nest a directory — one of the two is forced,
so matching it is free.

**Why not one branch.** Writing the intent on the work branch would leave its PR open
while spec, plan and code piled onto the same diff, so approving the intent would
implicitly approve everything that followed. The intent gate has to close before work
starts or it is not a gate. Writing the intent straight to master removes the PR, which
removes the gate entirely.

**DECIDED: the derivation keys on `cycle-<id>` specifically**, not "any branch
mentioning this id". Otherwise a lingering `intent-<id>` branch reads as work in
progress.

### Abandonment — one state, one reason

**DECIDED: `abandoned` is the only state, and it carries a mandatory `reason:`.**

`rejected` is not available: it is already a finding disposition meaning *the finding
was wrong*. Reusing it for intents would make one word mean two things at two altitudes
— the mistake already corrected for `open` and for `chain`.

Nor does the distinction earn two states. Refused and merely-dropped behave identically
to the dispatcher, to the derivation, and to the corpus; only the *reason* differs, and
that is a field. So one state, one directory, and the reason says which.

Three points where an intent can die:

| When | What happens |
|---|---|
| at the intent PR | nothing is on master yet. Close the PR |
| after merge, before work starts | move the file to `intents/abandoned/` |
| mid-cycle | delete the `cycle-<id>` branch, discarding the worktree; move the intent to `intents/abandoned/`. **No retro** — nothing landed, so it never reads as done |

One judgment call on the first row: closing a PR because the intent is badly *written*
is a draft iteration and needs no record. Rejecting the **idea** should land in
`intents/abandoned/` with its reason, on the ADR principle that a rejected idea leaving
no trace gets re-proposed by someone who does not know it was already considered.

### Blocked — when a cycle hits the charter

**DECIDED: a cycle never amends the charter governing it. It files a request and
stops.** This is the rule that keeps the charter an authority rather than advice —
the same reason a process cannot grant itself a permission.

When a cycle hits a charter trigger (§2), it:

1. **Halts** — the same machinery as a blast-radius escalation.
2. **Files a charter intent**, carrying the evidence it just generated.
3. Writes `blocked-by: <charter intent id>` into its own intent.
4. **Discards its worktree.** It was built under rules that are about to change, so
   resuming it later would be worse than re-running from spec.
5. Writes **no retro.**

Step 5 matters: a retro would read as the completion certificate and the intent
would show as done — the same defect as gitignoring retros, arriving through a
different door. The blocked cycle's evidence lives in the charter intent instead,
which is where the corpus replay needs it anyway.

**Blocked is derived, not written.** `blocked-by` is a *semantic* link, the same
category as `caused-by` and `superseded-by` — written because it is not derivable
from anything. *Blocked-ness* is then derived from it: the intent is blocked exactly
while its `blocked-by` target is unmerged.

Which means it **self-clears.** The moment the charter intent merges, the link's
target is merged, the intent is no longer blocked, and the dispatcher picks it up
again. There is no manual unblock step.

**Rejected: returning the intent to `queued`.** The dispatcher would pick it
straight back up and hit the same wall — an infinite loop. Blocked has to be its own
state.

### In a batch

Cycles are independent — separate worktrees, no shared state — so one blocking
affects nothing else. Eight intents in a batch, cycle three hits a bad invariant:

1. Cycle three halts, files a charter intent, discards its worktree.
2. Cycles one, two and four through eight **continue untouched.**
3. If cycle six hits the **same** invariant, it links to the **same** charter
   intent rather than filing a second. **DECIDED: charter intents deduplicate on
   the invariant they target**, or a batch of eight would file eight identical
   requests.
4. You approve or reject when you next look.
5. On merge, every intent blocked on it unblocks automatically.

### The charter-intent PR carries both halves

**DECIDED (2026-09-23): a charter intent's PR contains the request *and* the charter
edit, and it merges either way.**

If the intent merged alone and the charter were edited afterwards, there would be a
window where the request has landed but the rule has not changed. Blocked intents derive
their state from whether `blocked-by` has merged, so they would unblock, re-run against
the **unchanged** charter, and block again immediately. One merge closes that window.

| Outcome | The PR is amended to | Effect |
|---|---|---|
| **approved** | keep the charter edit; intent → `intents/` | rule changed; blocked intents re-run from spec against it |
| **rejected** | drop the charter edit; intent → `intents/abandoned/` with a `reason:` | rule unchanged; the reason is on master, readable by the next cycle |

One PR, one merge, opposite content. So `blocked-by` **always** resolves — nothing is
ever blocked on something that never merges — and **where the intent landed is the
answer**:

- landed in `intents/` → the charter changed. Re-run from spec.
- landed in `intents/abandoned/` → refused. This work needs re-scoping, and a human
  dispositions the intent rather than the cycle retrying.

**DECIDED: dedupe considers abandoned charter intents, not only pending ones.** Three
cases:

| What exists for this invariant | The cycle |
|---|---|
| a **pending** charter intent | links to it, blocks, waits |
| an **abandoned** charter intent | blocks, and surfaces its `reason:` |
| nothing | files one |

The middle row is the useful one. A cycle blocked on a question already answered is a
different signal from one blocked on an open question — it says the *work* needs
rethinking, not the charter, and you see the reason you already gave instead of
re-deriving it. A genuinely different case may still file a new charter intent, but it
must state what differs.

**DECIDED: repeated rejections are a clarification signal, not a deletion signal.**
Three abandoned charter intents against one invariant means agents keep bumping into it
and you keep saying no — which points at the rule being *unclear* rather than wrong. Its
wording does not let an agent tell compliance from violation without trying.

**DECIDED (2026-09-22): no develop branch. Master is the working branch, and the
marketplace is pinned to a `release` ref.**

A marketplace source can be pinned to a branch or tag (§10, finding 3), so
teammates consume `…/_git/ai-sdlc#release` rather than whatever master happens to
be. That gives staging without a second long-lived branch: one ref, moved
deliberately, instead of a parallel line with its own merges.

The earlier framing — "needing a develop branch means not trusting your own verify
gate" — was half right. Verify gates whether *a cycle* is correct. It cannot judge
whether a set of merged cycles constitutes a coherent release. Merging and
publishing are genuinely different decisions, and with unsupervised fan-out
landing several cycles at once, "is this a good moment to publish" is a real
question rather than a hedge.

Finding 4 in §10 reinforces this from the other direction: background
auto-updates against a private marketplace are intermittently unreliable, so
updates want to be **rarer and deliberate** rather than triggered by every merge.

**DECIDED (2026-09-23): squash every PR.** Intent, cycle and charter alike.

Two reasons specific to this design. The **artifacts already carry the narrative** — a
cycle branch has commits for spec, plan, code and verify, but `spec.md`, `plan.md` and
`verify.md` are in the diff, so the chain of artifacts *is* the history and the commit
graph need not duplicate it. And **one cycle becomes one commit becomes one revert**,
which is exactly the revert story already settled: `git revert` on a landed cycle does
the job, so framework-level revert buys nothing. A squash also *guarantees* the
atomicity a charter intent requires, rather than relying on it.

The squash message is structured `<intent-id>: <title>`, so master's log reads as the
cycle log.

**DECIDED: `release` is a single moving branch, plus a version tag at each move.**

```
git tag v0.2.0
git push origin master:release v0.2.0
```

`release` has no commits of its own — it is a pointer whose tip is always a commit
already on master. Teammates add `…#release` once, forever, and you decide when they
see something new.

Rejected: **a branch per release** (`release/0.2.0`). A marketplace source can be
pinned to a branch or tag but **not a commit SHA**, so version pinning means teammates
editing their marketplace URL to update — which nobody does, leaving people on 0.1.0
six months later. It only pays off for hotfixing an old release *without* master's
newer work, and where the verify gate is meant to keep master shippable, that scenario
means master is broken and the fix is master. If such a case ever genuinely arises, cut
a release branch then, for that case. Tags give the immutable per-version record
meanwhile, and the tag matches `plugin.json`.

**DECIDED:** `plugin.json` is bumped **when the `release` ref moves**, never
in-cycle and not at every merge.

- Never in-cycle: CI requires a bump on every plugin change, so two concurrent
  cycles bumping in-cycle conflict every single time — a guaranteed collision,
  not a possible one.
- At release rather than at merge: nobody consumes master, so a bump there
  signals nothing. Several merged cycles sharing one released version is normal;
  that is what a release is.
- The CI rule therefore reads: **fail a `release` move that does not bump the
  version.** More accurate than gating every merge, and much less noisy.

### Collision handling

**DECIDED:** worktree per cycle. One intent = one branch = one worktree. This
**supersedes** v01's conclusion (track file claims, serialize on overlap), which
was itself a constraint-3 violation: a registry beside the filesystem.

Isolation is enforced by the harness, not by convention. Claude Code blocks any
`Edit`/`Write`/`NotebookEdit` targeting the main checkout, any command whose
working directory resolves there, any git redirected into it via `-C`,
`--git-dir`, `GIT_DIR`, `GIT_WORK_TREE` or a `cd`, and any command whose shape
cannot be verified. The last check cannot be turned off. `claude -p --worktree`
works without the trust dialog, and resume returns a session to its worktree in
`-p` and the SDK.

Three levels of collision, distinguished because they have different answers:

| Level | Collision | Answer |
|---|---|---|
| 1 | agents inside one interrogation | impossible — read-only |
| 2 | agents inside one build | do not parallelize, or `isolation: worktree` per subagent |
| 3 | two concurrent intents | worktree per cycle |

**What worktrees do not solve**, and the mitigations:

- **Semantic collisions.** Cycle A renames a method, cycle B adds a caller in a
  different file. Git merges clean; the build breaks. Detected only at merge,
  which is why re-running verify after rebase is mandatory rather than optional.
- **Hot shared files** that nearly every cycle touches and that will conflict:
  `Directory.Packages.props`, the `.sln`, `global.json`, `.editorconfig`, and
  `plugin.json`'s version (handled above).
- **Cost.** Each worktree is a fresh checkout: separate `obj`/`bin`, full NuGet
  restore, full build. With Aspire in the graph that is real disk and real
  minutes.
- **Windows caveat.** Permission approvals normally save to the main checkout and
  apply across worktrees, but on Windows the rule stays with the worktree that
  granted it. Expect to re-approve per worktree.

This also answers what framework-level revert would buy: nothing. Cycle = branch
= worktree, so `git revert` on the branch *is* the revert — except for changes
git cannot undo, which is why migrations is a blast-radius flag.

### Blast radius

Gates should cost in proportion to risk. The playbook assumes blast radius is
computable; real computation needs a dependency graph and infra topology. A proxy
computed from the diff is directionally right and enough.

**Selection rule for a flag:** *a change whose failure cannot be corrected by
reverting the commit.*

| Flag | Why reverting does not save you |
|---|---|
| auth / authorization | a security incident, silent and externally exploitable while live |
| database migrations | `git revert` does not un-migrate; transformed data stays transformed |
| serialization / public contracts | breaks other people's code, and data already written in the old shape |
| secrets / config | you cannot un-leak a secret; rotation is the only remedy |
| CI / pipeline definitions | can disable the gates that catch everything else |
| public API of a published package | already in someone's lock file; NuGet does not recall |

Size — file count and line count — is a second, separate dimension: not
irreversibility but review attention, since a 40-file diff gets skimmed however
careful the reviewer.

| Tier | Condition | Gate |
|---|---|---|
| 1 | leaf, small, no flags | single review pass, self-approve |
| 2 | anything larger | full review |
| 3 | auth, migrations, or public API | full review + explicit human sign-off |

**DECIDED:** blast radius is read twice. **Predicted** tier, from what the intent
and plan say they will touch, decides whether a run may launch unsupervised.
**Actual** tier, from the real diff, is checked at verify. Actual exceeding
predicted **halts and escalates** instead of merging. The check is free —
`plan.md` already declares its files and `verify.md` already compares plan
against diff, so it is the same comparison used twice.

This is also what makes unsupervised mode definable rather than philosophical:
**it crosses workflow boundaries without stopping, at Tier 1 only, terminating on
pass-or-no-progress, and the merge gate never auto-passes.**

### Termination

**DECIDED:** stop on **no-progress**, never "until clean".

v01 measured that the finding rate does not decay — round 2 found 3-of-5 blocking
items inside round 1's repairs; round 3 found 4-of-5 inside round 2's. "Until
clean" therefore never fires, and a fixed round cap ships known-broken artifacts
wearing a clean label, which launders the failure. A no-progress detector fires
on exactly the measured condition.

The data argues for one more thing: **repairs are unreviewed edits**, and the
repair diff is the most dangerous surface. What gets interrogated in round N+1 is
the diff, narrowly, against the finding it claims to resolve — not the whole
artifact again.

---

## 8. Verification

**DECIDED:** `verify.md` is the post-implementation artifact. `facts.md` is the
pre-interrogation fact-gathering output, **generated and not committed** — it is
fully derivable from the tree, so committing it creates a second list. Findings
quote the facts they relied on, which gives the audit trail without the drift.

Two words, never overlapping. "Verification" previously did double duty for both
and the collision would have spread into every skill and directory.

### Contents of `verify.md`

There is no settled standard for the artifact; there is a convergent standard for
the content, and it is called **traceability**. The playbook's Test stage
produces results and logs but no named document; Spec Kit has a distinct
`converge` command; VSDD and speckeep both land on a traceability record.

1. **Traceability** — each verifiable acceptance criterion → the test proving it
   → the files implementing it. An unmapped criterion is blocking. This catches
   "we built something adjacent to what was specified."
2. **Plan adherence** — what `plan.md` said it would touch vs what the diff
   touched. Unplanned files are a finding, not a footnote. Mechanical scope-drift
   detection, and it doubles as the blast-radius escalation check.
3. **Tool gates** — build warnings, analyzers, tests, coverage, suppression count.
4. **Dispositions** — every finding from every source, resolved or explicitly
   accepted with a reason.

Rule taken verbatim from VSDD: *every finding, from adversary or tool, at any
state, is dispositioned* — and a gap between a derived artifact and its source
is **corrected, never signed off**.

### Traceability mechanism

**DECIDED:** generated from test metadata, not a stamped snapshot.

Each *verifiable* criterion gets a stable ID in `spec.md`
(`AC-20260922-a7f3-3`). Tests carry it
(`[TestProperty("AC", "AC-20260922-a7f3-3")]`). A tool collects IDs from the tests, diffs
against the spec's criteria list, and an unmapped criterion is blocking. The
table in `verify.md` is **generated output, never hand-edited**.

Criteria that cannot be annotated — "the UI should feel responsive" — are marked
**unverifiable** in `spec.md` and must be dispositioned by a human at the Deploy
gate.

Discipline this requires: criterion IDs are **assigned once and never reused**.
Renumbering criteria breaks every annotation.

**DECIDED: IDs are globally unique by construction — `AC-<cycle-id>-<n>`**, where
the cycle id is itself `<yyyymmdd>-<4 hex>` (§1).

This is the one consequence of the deferred living-spec question (§1) that has to
be settled now. A cycle-local `AC-3` would mean "the third criterion of this cycle", so
adopting a solution-level living spec later would force a renumbering — the exact
thing the rule above forbids. A composite ID is globally unique with no counter
and no shared file to conflict over, and it makes the living spec a purely additive
change whenever it earns its place.

Why it had to be decided before any template exists: an annotation on every test
in every scaffolded solution is cheap in week one and means touching every test
ever written in year two.

Rejected: a stamped snapshot written by an agent at verification time. Zero
convention burden, but it is an LLM judgment rather than a fact, it goes stale
the instant anything changes with nothing detecting it, and it is textbook
constraint 3 — a second list beside the tests with no check binding them.

### Quality baseline

**DECIDED.** The charter states the invariant: *a cycle cannot pass verification
with any build warning, any failed analyzer, or any unjustified suppression.* The
stack pack supplies the mechanism — these are tool settings and the charter
excludes tool names.

Carried from v01: `TreatWarningsAsErrors`, `Nullable enable`,
`AnalysisModeSecurity=All`, `NuGetAudit` at all/moderate.

Added:

- `EnforceCodeStyleInBuild` — without it `.editorconfig` rules are IDE-only and
  never fail a build. The most commonly missed one.
- `AnalysisLevel=latest-all` — `AnalysisModeSecurity=All` enables only the
  security category; everything else sits at default.
- `WarningsNotAsErrors`, deliberately empty — so an exception is one visible line
  in a props file rather than a pragma buried in code.
- Deterministic locked restore in CI, so the build that passed locally is the
  build CI runs.
- `dotnet format --verify-no-changes` as a gate, so formatting is never a review
  topic.

**Protecting the zero from erosion** — this is the part that matters. Zero
warnings is a strong signal *because* it is binary, and the failure mode is not
warnings appearing but suppressions quietly accumulating until the zero means
nothing. Therefore: every `NoWarn` and `#pragma warning disable` carries a
written justification; the suppression count is tracked in `verify.md`; and an
**increase** in that count is itself a finding.

---

## 9. Tech stack

Two stacks. Do not conflate them — v01's charter says so explicitly and the test
project's comments repeat it.

### Platform preference

**DECIDED: lean Microsoft — and the preference lives in the stack pack, not the
charter.** The charter only delegates (§2): it forbids contradicting the declared
pack without a recorded deviation. Naming C# or Python in the charter would
violate the charter's own exclusion of tool names.

**DECIDED: the ordering is per-context, not global.** A single global list is
wrong, because what is already installed differs by where the script runs:

| Context | Preference |
|---|---|
| Windows dev box | C# → PowerShell → JS-where-mandated → bash → Python |
| CI agent | C# → pwsh → bash → Python |
| Linux container | C# → pwsh → bash → Python |

In a container you build the image, so "no install step" is meaningless and a
three-line bash entrypoint is the honest choice over installing PowerShell to
avoid writing bash.

Elsewhere: ASP.NET Core and Blazor for web, ML.NET over TensorFlow or PyTorch,
Azure for cloud, Terraform for infra.

Two honest notes on this:

- **Workflow scripts are JavaScript, and that is not a choice.** The workflow
  runtime executes plain JS with `agent()`/`parallel()`/`pipeline()` and no
  module loading. The engine's orchestration layer is therefore JS because that
  is the mechanism's language, not a deviation anyone selected. Recording it so
  it is not mistaken for drift.
- **DECIDED (2026-09-22): .NET everywhere, including ML competition.** Every pack
  declares only C#/ML.NET. Python is not in any pack's runtime preference, so a
  plan reaching for it blocks with a `charter-violation` citing INV-04.

  The honest cost, sharper than "Python is better":
  **fine in .NET** — `ML.LightGbm` is the same LightGBM everyone else uses, and
  tabular competitions are largely gradient boosting plus feature engineering, so
  that class of problem is genuinely competitive;
  **slower in .NET** — `Data.Analysis` for wrangling has fewer operations, thinner
  docs and far fewer worked examples than pandas;
  **a real ceiling** — no PyTorch means consuming ONNX models rather than
  authoring architectures, which rules out vision and NLP on the merits.

  Reversible at low cost: a single solution that genuinely needs Python records a
  per-solution deviation. If three competition solutions record the same
  deviation, that is the amendment signal saying the pack rule is wrong — the
  charter mechanism applied to a pack.

  **Scope clarification, recorded in INV-04:** the rule governs what a solution
  *ships and runs*. Reading, running or porting existing work in another language
  is research, not a runtime choice, and is never a violation. Without this the
  `charter` role's documented failure mode — over-matching — would flag a spec
  that merely cites a Python notebook.

### PowerShell 5.1 or 7 — two stacks, probably two answers

Microsoft-first settles PowerShell over bash/Python; it does not settle which
PowerShell. v01 chose Windows PowerShell 5.1 for zero-install on any Windows box,
measured at 149ms against 67 node / 51 python — chosen despite being slowest.

Splitting it by stack, as this section's opening line demands:

| Stack | Runs where | Verdict |
|---|---|---|
| The framework's own shipped scripts | this Windows box, Windows CI | 5.1 stays viable — zero-install on Windows is a real benefit here |
| Scripts the stack pack emits into solutions | Linux containers, Linux agents | **5.1 is ruled out.** Windows PowerShell does not run there at all |

Note what happens to 5.1's justification inside a container: you are building the
image and installing what you want at build time, so "no install step" is
meaningless and the entire reason for the v01 choice evaporates.

**DECIDED (2026-09-22): the framework stays Windows-only.** So:

- **Framework's own shipped scripts: Windows PowerShell 5.1.** Zero-install on any
  Windows box is a real benefit, and the 149ms only ever mattered for a per-edit
  hook.
- **Scripts the stack pack emits: pwsh 7 or bash**, per the context table above.
  5.1 cannot run in a Linux container at all.

Two answers, because they are two stacks — which is what this section opens by
insisting on.

### What the framework itself is built with

Deliberately minimal. No Aspire, no EF Core, no Azure in this repo.

| Concern | Commitment | Why |
|---|---|---|
| Skills, agents, schemas | Markdown + YAML frontmatter | what the harness loads |
| Workflows | JavaScript | mandated by the workflow runtime |
| Shipped executables | PowerShell | no install step on a Windows box |
| Tests | MSTest, one project, `net10.0` | same framework the generated solutions use; drives shipped scripts as external processes, never reimplements them in C# |
| Platform | Windows | the hook and the manifest check invoke PowerShell |
| CI | one `dotnet test` on `windows-latest`, rewritten as an ADO pipeline | testing on `pwsh` would test something other than what ships |
| Versioning | semver, bumped every release | plugins are cached per version; a skipped bump makes an update a silent no-op |

Test project settings worth keeping: `TreatWarningsAsErrors`, `Nullable enable`,
`AnalysisModeSecurity=All`, `NuGetAudit` at all/moderate.

**OPEN:** v01 shipped two `.mjs` repo tools because parsing and executing
workflow bodies needed a JS engine. With workflows now a first-class runtime
feature, do those tools still have a job?

### What the stack pack scaffolds

Evidence only. Authority is `Directory.Packages.props`.

| Layer | Pinned in v01 |
|---|---|
| SDK | .NET SDK 10.0.100, `rollForward: latestFeature` |
| Target | `net10.0`, `LangVersion latest`, nullable enabled |
| Orchestration | Aspire 13.5.3 — Hosting, SqlServer, Redis, Azure.Storage |
| Data | EF Core 10.0.1 |
| Web | ASP.NET Core + Blazor; `Components.WebAssembly` 10.0.1, `Authentication.JwtBearer` 10.0.1 |
| API | `AspNetCore.OpenApi` 10.0.12 — forced above the template's 10.0.9 to clear CVE-2026-49451 |
| Resilience | `Extensions.Http.Resilience` 10.8.0, `Extensions.ServiceDiscovery` 10.7.0 |
| Telemetry | OpenTelemetry 1.15.x |
| ML | Microsoft.ML 4.0.2, ML.LightGbm 4.0.2, ML.AutoML 0.23.2, Data.Analysis 0.23.2 |
| Testing | MSTest 4.2.3, `coverlet.collector` 10.0.1, `Aspire.Hosting.Testing` 13.5.3 |
| Infra | Terraform ~> 1.14, `hashicorp/azurerm ~> 4.51`, azurerm remote backend |
| Conventions | Central Package Management, transitive pinning, shared `.editorconfig`, `coverage.runsettings` excluding EF migrations |

**DECIDED (2026-09-23): `bunit` 2.11.3, and bump the Blazor packages to 10.0.10+
in the same change.**

v01 pinned 1.41.1 with nothing referencing it — a pin asserting a capability no check
verified, which is INV-07's shape. Rather than drop it, it is brought current and made
real, and verification cycles confirm the combination.

The version arithmetic, because it forces a second bump: bunit 2.11.3 requires
`Microsoft.AspNetCore.Components >= 10.0.10`, and v01 pins `Components.WebAssembly` at
10.0.1 — nine patches behind, while `AspNetCore.OpenApi` sits at 10.0.12 from the CVE
fix. So the stack is already internally inconsistent and this makes it consistent. The
1.41.1 → 2.x jump is a major version with API changes, but since nothing used it, the
migration is free.

### The web pack is host-agnostic by default, so mobile stays additive

**DECIDED (2026-09-23):** the .NET web pack scaffolds a **host-agnostic Razor class
library** whether or not mobile is wanted. MAUI is not a near-term target; this is about
not foreclosing it.

Microsoft ships an official `.NET MAUI Blazor Hybrid and Web App` template targeting
Android, iOS, Mac, Windows and Web from one shared UI, and its architecture is three
projects with one hard rule:

```
Lib/        RCL — Razor components, static assets, and INTERFACES for platform
            capability. Microsoft.NET.Sdk.Razor, plain net10.0, no MAUI dependency
Lib.Maui/   references Lib; implements those interfaces with MAUI APIs
MauiApp/    references both; registers the implementations in DI
```

Dependency flow is App → MAUI library → RCL, and **the RCL never references the MAUI
library.** Anything platform-specific — device info, permissions, file system,
connectivity, geolocation — is an interface in the RCL and an implementation outside it.

The reason to adopt the structure now rather than a separate MAUI pack:

| Components live | Adding mobile later is |
|---|---|
| directly in the web app | a **refactor** — extract every component, find every platform assumption |
| in a host-agnostic RCL | **additive** — add two projects, nothing moves |

Cost is one extra project plus the discipline of abstracting platform capability. Low in
practice for a business web app, where most behaviour is HTTP calls and rendering and is
already host-agnostic; the abstractions only bite for device-ish things such an app
rarely does.

**This is also what makes the bunit decision cover everything.** The RCL is plain
`net10.0` with no MAUI dependency, so bunit tests those components with no device,
emulator or MAUI involvement — which is Microsoft's own guidance. The testing split
follows: RCL components by bunit now, the thin MAUI adapter layer by device or emulator
integration tests, deferred. Risk concentrates in the RCL, which is the part covered.

**Recorded pack constraints:**

- **iOS requires macOS to build and sign.** Apple's constraint, not .NET's. Android and
  Windows build on a Windows box; iOS needs a networked Mac or a macOS CI runner at a
  higher billing multiplier. The framework staying Windows-only is unaffected, but a
  generated iOS-targeting solution is not buildable on Windows alone.
- **Multi-targeted builds are expensive** — `net10.0-android;net10.0-ios;net10.0-maccatalyst`
  plus conditional Windows. This is a direct input to the unsupervised concurrency cap:
  one worktree per cycle each running a multi-target MAUI build costs far more than the
  pure web case.
- **MAUI's quality history.** The .NET 10 release focus is stated as "product quality",
  which is an acknowledgement of MAUI's reputation. LTS through November 2028 is the
  reassurance. Expect the strict gates — warnings-as-errors, `AnalysisLevel=latest-all` —
  to meet more friction here than in a pure web project, which argues for the pack
  shipping a known-good configuration rather than each solution rediscovering it.

---

## 10. Hosting and distribution

**DECIDED (2026-09-23): GitHub only. One home, no mirror.**

This reverses the 2026-09-21 move to Azure DevOps, which was made to match the existing
`MarketSense` project. Two reasons, and the second is the stronger:

- **A mirror is an INV-03 violation.** Two copies of a repository with nothing enforcing
  that they agree. An earlier draft of this document proposed keeping GitHub as a push
  mirror; that was wrong for exactly the reason the charter exists to catch.
- **GitHub is the documented default marketplace source, and going there deletes the
  only blocking open item.** Azure DevOps needed a special rule shipped in Claude Code
  2.1.72 (omit `.git`, `/_git/` path detection) and its credential path is still
  unverified end to end. On GitHub that question does not arise.

Also, for `MofonCode.Core` specifically: the package publishes publicly to nuget.org, and
if the source is ever made public — ordinary for a shared kernel — GitHub is its natural
home and ADO is not.

**Azure DevOps keeps nothing.** Not even a wiki: a wiki restating anything already in
`concept.md` or `CHARTER.md` is the mirror problem again at smaller scale. Boards would
be defensible if work tracking is ever wanted, since tracking work is not duplicating
facts about code — but YAGNI, and nothing needs them today.

**Three things to confirm before executing the move:**

1. **Is `github.com/MofonCode/ai-sdlc` private?** This document records that
   `MofonCode.Core` there is public and empty; it says nothing about ai-sdlc. If it is
   public, making it the home publishes everything. One command, and it must run first.
2. **Do the teammates arriving have GitHub org access?** Whichever host is already
   provisioned is the lower-friction answer.
3. **Does `MarketSense` stay on ADO?** If solutions live there and the framework lives on
   GitHub, that is a deliberate split. Defensible — a plugin consumed by developers is a
   different kind of thing from solution code, and retro absorption is a manual file copy
   that does not care about hosts — but it should be chosen rather than drifted into.

Dormant GitHub repos should be **archived** rather than left stale, `MofonCode.Core`
first: public and empty under an org name is the worst state, since it invites confusion
about what is canonical.

**Recorded history, since it explains what was considered.** Both repos were moved to
ADO on 2026-09-21, SHAs verified identical to local, both projects Agile / Git /
private. The clone URLs were
`https://dev.azure.com/MofonCode/{ai-sdlc,MofonCode.Core}/_git/…`. The ADO research
below remains accurate and is kept because it is the fallback if GitHub ever proves
unsuitable.

### How ai-sdlc is distributed

**DECIDED: the private ADO git repo IS the distribution.**

An ADO Artifacts feed cannot host a Claude Code plugin marketplace — they are
different mechanisms. A feed serves *packages*; a marketplace is *git-cloned*,
which `marketplace add`'s `--sparse` option gives away, since it is implemented
with `git sparse-checkout`.

```
claude plugin marketplace add git@ssh.dev.azure.com:v3/MofonCode/ai-sdlc/ai-sdlc#release
claude plugin install sdlc@ai-sdlc
```

Private because the repo is private. Current when `release` moves, not on every
merge to master (§7). Note the **absence of a `.git` suffix** and the SSH remote —
both deliberate, for the reasons in the verification section below. The HTTPS
equivalent is `https://dev.azure.com/MofonCode/ai-sdlc/_git/ai-sdlc#release`.

**What this deletes:** no packaging step, no publish pipeline, nothing to pack,
push or version-bump for distribution. The only pipeline ai-sdlc needs is CI —
`dotnet test` — plus the semver check. **Do not build a release pipeline for this
repo.**

Considered and deliberately not adopted: **ADO Universal Packages**. Pack the
plugin, `az artifacts universal publish`, ship a bootstrap script that downloads
and points a local-directory marketplace at the result. Buys pinned versions and
air-gapped installs; costs a pipeline, a bootstrap script, and a second install
step for every teammate. Neither benefit is a requirement. **This is the recorded
fallback if the auth check below fails.** Revisit if pinning or offline installs
become requirements, and write the reason down.

**Semver still matters, for a different reason.** Plugins are cached per version,
so a release that does not bump `plugin.json` makes an update a silent no-op for
everyone already installed. True of the git-source route too — the cache keys on
the manifest version, not the commit. v01 recorded that nothing checks this (open
item I9). **CI must fail a plugin change that does not bump its version.** This
is the one piece of release discipline that survives.

### Verified 2026-09-22 — ADO is explicitly supported

Researched against the official docs, the changelog, and a local CLI probe. Four
findings, with confidence stated.

**1. ADO is a named, deliberate case — high confidence.** Support for
suffix-less marketplace git URLs shipped in Claude Code **2.1.72**, with Azure
DevOps named in the changelog. The docs carry an ADO-specific rule:

> **Azure DevOps**: omit the suffix. Claude Code clones any URL whose path
> contains `/_git/`. If you append `.git` to a `/_git/` path, the clone fails.

So the URL above is correct **because** it has no `.git`. Adding one breaks the
clone. The `/_git/` rule is host-agnostic, so it also covers on-prem Azure
DevOps Server. Verified by probe: a suffix-less ADO URL is handed to `git clone`
rather than parsed as a hosted `marketplace.json`.

**2. Auth is the ambient git credential stack — medium confidence.** There is no
Claude-specific credential store; the marketplace schema has no token or PAT
field, so there is nowhere else for credentials to come from. `marketplace add`
succeeds exactly when `git clone` of the same URL succeeds in the same shell. On
Windows that is Git Credential Manager with an ADO PAT — and this machine is
already configured for it (`credential.helper=manager` plus
`credential.https://dev.azure.com.usehttppath=true`, both from the system
gitconfig), so no extra setup. Medium rather than high because no end-to-end test
against a real private ADO repo was performed and no first-hand success report
for a *private* ADO marketplace was found. **Still worth one real install to
confirm** — but the mechanism is now known and the risk is small.

**3. Pinning exists, and is asymmetric — high confidence.**

| Level | `ref` (branch/tag) | `sha` (commit) |
|---|---|---|
| **Marketplace source** | yes — `URL#<ref>`, or a `ref` field in `extraKnownMarketplaces` | **no** |
| **Plugin entry** inside `marketplace.json` | yes | yes |

This is the finding that settles master-vs-develop (§7).

**4. Background auto-update is flaky against a private repo — plan for it.** The
background refresh **deliberately disables git credential helpers**, so its HTTPS
check cannot authenticate against a private repo and degrades into a full
re-clone. The re-clone does use stored credentials but can time out on a large
repo, making private-marketplace auto-updates intermittently unreliable.

Two mitigations, and they are not equivalent:

- `CLAUDE_CODE_PLUGIN_KEEP_MARKETPLACE_ON_FAILURE=1` — stops a failed refresh
  from discarding the installed marketplace. A safety net, not a fix.
- **An SSH remote** — SSH keys need no credential helper, so the background
  refresh path works normally. This is the actual fix.

**Recommendation:** distribute over SSH
(`git@ssh.dev.azure.com:v3/MofonCode/ai-sdlc/ai-sdlc`) rather than HTTPS. Setup
cost is comparable — one-time key instead of one-time PAT — and it removes the
only real limitation found. **OPEN:** confirm `marketplace add` accepts an ADO
SSH URL; SSH is named as the mitigation but not demonstrated for this host.

### MofonCode.Core

**It is not that the NuGet pipeline was never finished — the library was never
started.** Zero commits, no `.csproj`, no pipeline, no `nuget.config`, nothing on
nuget.org (the registration endpoint returns 404). What exists is the stated
intent:

> Shared kernel for MofonCode .NET solutions: a minimal mediator, result and
> tracing primitives, and the analyzers that keep a vertical slice in shape.

**DECIDED:** separate solution, not in this repo's scope — but the obvious first
dogfood target. Greenfield, small, real published output, so failures are
visible. It is the framework's first *client*.

Source private in ADO; package published publicly to nuget.org from an ADO
pipeline holding a nuget.org API key. Ordinary, needs no special handling.

**OPEN:** single package is the stated requirement, and the description names
three things — mediator, primitives, analyzers. **Analyzers usually want their
own package** so consumers can take the rules without the runtime. One package
means the analyzer ships inside it as an `analyzers/dotnet/cs` asset. Workable,
but decide it deliberately rather than discovering it at pack time.

### Not verified, and mostly moot

Artifact feeds in the org could not be enumerated: the `azure-devops` CLI
extension fails version parsing on every api-version tried (`could not convert
string to float: '7.1.1'`), and the REST endpoint bounced the `az` token to a
sign-in page — the local `az` login is a personal Microsoft account, which may
not be the ADO identity. Choosing the git-repo route makes this irrelevant: no
feed is required by anything here. If one is ever wanted, run `az devops login`
with a PAT first.

---

## 11. Constraints carried from v01 — findings, not preferences

Each now has a mechanism.

| # | Finding | Mechanism |
|---|---|---|
| 1 | "Iterate until clean" does not converge. Round 2 found 3-of-5 blocking items inside round 1's repairs; round 3 found 4-of-5 inside round 2's. Repairs are the most dangerous surface. | No-progress termination; repairs interrogated as diffs against the finding they claim to fix (§7) |
| 2 | ~70% of findings were mechanical — unresolved paths, stale counts, claims about the tree. Checking them first cut one round from 30 tool calls to 13, and what came back was reasoning instead of bookkeeping. | Fact-gathering runs once in the parent and is passed to every role agent (§3) |
| 3 | Never put a second list beside a source of truth. v01 was bitten four times. If a second list is genuinely needed, the check holding the two together ships in the same change. | Worktrees replace the file-claim registry; `facts.md` uncommitted; traceability generated from test metadata; intent status derived from git |
| 4 | Concurrency: track which files each live plan claims and serialize only when they overlap — do not gate every edit. | **Superseded.** Worktree per cycle makes collision structurally impossible, enforced by the harness. Merge is the single serialization point (§7) |

Constraint 4 is worth noticing: v01's conclusion was replaced by something
strictly better, and it took removing a mechanism rather than adding one.

---

## 12. Open items

Consolidated. Do not guess at any of these.

Being worked through one at a time. Closed items move into the section they
belong to, with their reason.

**Closed 2026-09-22**

- ~~Solo or teammate?~~ **Solo now; the team arrives within about a week.** So the
  intent PR becomes real review shortly, and the distribution path has a deadline.
- ~~The ADO install proof.~~ **Ship a thin but real `v0.1.0`** — marketplace
  catalog, plugin manifest, the `intent` skill, the test project and CI — host it,
  and test install over both HTTPS and SSH. Not a throwaway stub: a built plugin
  is needed under either distribution route, including the Universal Packages
  fallback. **No artifact feed and no release pipeline** — a marketplace is
  git-cloned, not a package. The third unknown, someone else's credentials, is
  answered for free when the team installs `v0.1.0`.
- ~~ML competition.~~ **.NET everywhere** (§9), with the INV-04 scope
  clarification that reading or porting other-language work is research, not a
  runtime choice.

- ~~Retro transport.~~ **One manual `absorb-retro` skill, idempotent by cycle id** — see §4. No
  ledger, no gap check, no cross-repo automation; retros stay committed in the
  solution repo.

**Open — architecture**

1. Charter review cadence — every N cycles, or on a clock? (§2)
2. Who moves the `release` ref, and on what trigger — manual, or a cadence? (§7)
3. Concurrency cap for unsupervised fan-out. (§7)
4. When to promote ai-sdlc's seven local invariants into a shared
   `mofoncode-invariants@v1` set — becomes real at solution two. (§2)

**Closed 2026-09-23 — the five smaller items**

- ~~The two `.mjs` repo tools.~~ **Not carried forward.** v01 needed them because
  parsing and executing workflow bodies required a JS engine; dynamic workflows are now
  that engine, and nothing has been ported. Bonus: this removes Node from the framework's
  dependency list entirely — it was only ever there because nothing else could do the job.
- ~~`bunit`.~~ **2.11.3, with the Blazor packages bumped to 10.0.10+** (§9).
- ~~`MofonCode.Core` packaging.~~ **One package**, analyzers shipped inside as an
  `analyzers/dotnet/cs` asset. The deciding argument is reversibility: splitting later is
  additive and existing consumers are unaffected, while merging two already-published
  packages is breaking. One is the direction you can back out of. The usual counter — that
  analyzers want their own package so consumers can take rules without runtime — does not
  apply to a shared kernel, where every consumer takes the runtime anyway.
- ~~GitHub's fate.~~ **GitHub only, no mirror** (§10).
- ~~Which stack pack first.~~ **.NET library.** `MofonCode.Core` is the first real client
  and it is a library, so build the pack that client needs rather than the most impressive
  one. It is also the smallest — no Aspire, no EF Core, no Blazor — which proves the pack
  mechanism cheaply before committing to its shape. Then .NET API.

**Deferred deliberately, with their trigger conditions recorded**

11. A *living* solution-level spec. Permissible only if the AC-ID-to-test mapping
    is its binding check; otherwise an INV-03 violation. (§1)
12. Artifact directory organisation once the count is annoying — cosmetic and
    reversible, because lifecycle state is derived. (§1)

---

## 13. Next steps

| | Step | Why here |
|---|---|---|
| 0 | ~~Run `/deep-research` once~~ | **Done 2026-09-22.** Validated the engine choice and answered §10 (see §3, observed) |
| 1 | ~~This rewrite~~ | **Done 2026-09-22** |
| 2 | ~~Hand-write `.sdlc/CHARTER.md`~~ | **Done 2026-09-22.** Seven invariants, taxonomy v1, roles, decision register |
| 3 | Write intent I1 **by hand, against the charter** | The real test |
| 4 | Build I1 by hand; the framework starts helping from I2 |

Step 3 is the one to pause on. Writing the first intent against the charter is
the cheapest possible test of whether the charter works as an authority. If I1
cannot be cleanly interrogated against it, the charter is decoration — learned in
an afternoon rather than in cycle six.

### Intent split

| # | Intent |
|---|---|
| I1 | Artifact model, cycle layout, findings schema |
| I2 | Fact-gathering — runs once, feeds every role agent |
| I3 | Interrogator workflow, single-pass, schema output |
| I4 | Repair loop, no-progress termination |
| I5 | Retro emission + taxonomy |
| I6 | Retro absorption into ai-sdlc |
| I7 | Plugin packaging, CI semver check, ADO install proof |
| I8 | Dispatch and landing — worktrees, tiers, merge serialization |

I2 is new relative to v01's framing: the highest-leverage measured finding had no
home in the original A–G grouping. Communication is not an intent; it is a
charter clause.
