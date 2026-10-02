# CONCEPT.contradictions.md

Contradictions found in [`CONCEPT.md`](CONCEPT.md) (1595 lines, iteration 4, last updated
2026-10-01), read against [`CONSTRAINTS.md`](CONSTRAINTS.md) and
[`CONCEPT.raw.md`](CONCEPT.raw.md). Identification only — nothing in the concept was
changed, and no remedy is proposed beyond naming which of two statements the evidence
favours where that is clear.

Section numbers are the concept's own; line numbers are of `CONCEPT.md` as of this
reading.

**The two that block building:** S1 and S2. In-process judging removes the independence
the whole interrogation design rests on, and the pre-gate acceptance gap deadlocks any
cycle that hits the constraint conflict §5.0 already says is not hypothetical.

| | Finding | Severity |
|---|---|---|
| S1 | interrogation is both a fresh process and in-process | blocks building |
| S2 | accepting a blocking finding before the gate is impossible | blocks building |
| S3 | the single gate is justified by something `CON_09` forbids | structural |
| S4 | one mechanical check duplicates `CON_04` | structural |
| S5 | the gate binary and PowerShell 5.1 are mutually exclusive | structural |
| S6 | `solution.md` cannot be changed by the one thing allowed to change it | structural |
| S7 | `batch-build` never pushes, and lands partial batches | structural |
| S8 | `after` lives in an artifact that does not exist when it is written | structural |
| S9 | the self-healing loop's exits have nowhere to land in a plan | structural |
| S10 | the evidence contract is stack-bound while the library claims stack neutrality | structural |
| S11 | `verify-chain` checks less than the local layer it is meant to guarantee | structural |
| T12 | `adopted_at` is dropped three times and alive twice | stale prose |
| T13 | `MofonCode.Core` both ships analyzers and ships none | stale prose |
| T14 | `CON_10` binds a thing the design deleted | stale prose |
| N1–N9 | nine smaller concrete ones | minor |

---

## Structural — the design disagrees with itself

### S1 — interrogation is both a fresh process and in-process

Interrogation runs "against one artifact in fresh context," and the fresh context "is a
fresh process, on every host" — subagents are rejected specifically to avoid two code
paths for the most important skill (§5, ~line 396).

The self-healing loop says the opposite: one command writes the artifact, interrogates
it, works its own blocking findings and interrogates again (line 470), and "a skill can
judge itself against the same rules **in process**" (line 476).

A judge sharing the writer's context is exactly the investment bias §5 names as the only
thing independence buys — "the reader never saw the interview, the drafts, or the
author's reasoning." It also re-merges the two acts that judging-and-editing-are-separate
(§2.4) exists to split, and §2.4's stated reason applies verbatim: a skill that both
judges and rewrites "ends up judging its own edits."

### S2 — accepting a blocking finding before the gate is impossible

Advancing a stage requires a clean interrogation round, and a blocking finding may be
cleared "by refining the artifact or, where a person decides to accept it, by naming it
in the approval entry" — and not by ignoring it (§4.4).

The approval entry does not exist until immediately before `build`, after the plan: "a
cycle has exactly one gate" (§4.1).

So a blocking finding on `intent.md` that refinement cannot close has no legal exit, and
`spec` will not run. That is not hypothetical — it is the `CON_04`-versus-`CON_02`
conflict the self-healing loop describes as structural and as having happened in the
trial cycle (§5.0). The status panel shows the same gap in miniature: `Gate not approved
- answer or waive F_01 first`, printed at the **spec** stage (§13), where no approval
entry can yet exist.

### S3 — the single gate is justified by something `CON_09` forbids

There is no separate intent or spec gate "because a person reading a plan is reading the
spec restated inside it" (§4.1, line 246).

`CON_09` forbids "an artifact reproducing a constraint, a criterion or an outcome that
another artifact owns, instead of citing it."

Either the plan restates the spec — and violates the library — or it cites the spec, and
the single gate loses the argument it rests on. The plan's "proof per criterion" (§3.2)
is compatible with citation; the §4.1 sentence is not.

### S4 — one mechanical check duplicates `CON_04`

The structural checks that "need no reading comprehension at all" lead with "every intent
statement maps to at least one spec criterion" (§4.4).

`CON_04` — a spec covers every outcome its intent asks for — is in the library only
because admission test two holds: "no analyzer, schema or hook could decide it" (§6.2).

The two cannot both be true. The criterion-to-test check beside it is fine, because
existence is mechanical where `CON_06`'s adequacy is not; the intent-to-spec one is the
same judgement `CON_04` makes.

### S5 — the gate binary and PowerShell 5.1 are mutually exclusive

The gate "is a program, not a prompt" — one executable owning every process constraint,
carrying the schema evaluator with it, with iteration 3's missing `Test-Json` named as
the exact defect it fixes so that "artifacts are fully validated where they are written"
(§2.2, lines ~55–70).

Then: "**Shipped tooling stays Windows PowerShell 5.1**, invoked as `powershell.exe`" —
and "`Test-Json` does not exist, so schema validation lives in the test project rather
than in a script" (§11.3, line 1325). That is the carried-forward defect, carried forward
with its consequence intact.

The underlying argument is also unresolved rather than merely duplicated: 5.1 is chosen
because a hook that fails to start protects nothing and 5.1 is on every Windows machine.
Nothing says how a .NET binary meets that bar. `src/Sdlc.Cli` indicates the binary won in
practice; the document still says both. Related: the diff-to-plan comparison "already
exists as `Compare-DiffToPlan.ps1`" (§3.2), against §2.2's one implementation, three call
sites.

### S6 — `solution.md` cannot be changed by the one thing allowed to change it

The mutability table gives `solution.md` the protection "the hook denies agent writes
after creation", unconditionally (§7, line 756).

`profile` "is the one key that can legitimately change — a solution migrating from
`dotnet-default` to `mofoncode` — and it changes through a cycle that declares
`solution.md`, which is the one exception to 'nothing writes it after init'" (§7.3).

A cycle is run by an agent, whose write the hook denies. This is the same carve-out
problem that the unlock flag was rejected for: "an agent that can write files can flip
the flag and then edit the file, and protecting the flag needs a flag" (§7.1). There is
no way to grant the exception that the hook does not already forbid.

### S7 — `batch-build` never pushes, and lands partial batches

"**Partial batches land.** Successes are landed rather than discarded when a sibling
fails" (§9, line 950).

Thirty lines later: "`land` is what touches the remote: it pushes, opens the pull
request, and links the work item… batch-build never pushes" (line 982).

If landing is part of a batch run, the batch pushes. If it is not, "partial batches land"
describes something `batch-build` does not do, and nothing says who lands the successes.

### S8 — `after` lives in an artifact that does not exist when it is written

Splitting is cheapest early — "splitting late throws work away", and after a spec exists
splitting discards it (§3.1.1) — so the normal case is a split at intent stage.

The ordering a split produces is recorded as `after: [20260929_1456_32]`, "in the plan's
frontmatter" (§3.1.1).

A split before a plan exists has nowhere to put the order, and nothing says where it
lives until a plan appears. `batch-build` reads it from the plan (§9, step 1).

### S9 — the self-healing loop's exits have nowhere to land in a plan

The loop "runs for **all three** planning artifacts, intent included" (§5.0, ~line 468).
Its **assumed** exit writes to the artifact's **Assumptions** section and its **asked**
exit to the artifact's **Open** section (line 484).

The plan's contents are shape and departures, a file table, order, proof per criterion,
`Cannot prove`, and `Risk` (§3.2) — neither section exists. The spec's equivalent is
called `blocked-on`, and §5.0's own trial narrative uses `Blocked on`, not `Open`, for
the third exit on a spec. So the three exits name two sections that exist on one artifact
under one spelling, on another under a different one, and on the plan not at all.

### S10 — the evidence contract is stack-bound while the library claims stack neutrality

No constraint "may name a language, framework or package", and that neutrality "is also
what lets the whole library survive the move to other hosts untouched" (§6.2).

- Evidence is fixed as `diff.patch`, `build.log`, `tests.trx`, `base.sha` (§3.3, §15).
  `tests.trx` is an MSTest/VSTest format.
- `CON_12`'s **Why**, as quoted in §6.5, cites `tests.trx` and `diff.patch` by name. The
  shipped `CONSTRAINTS.md` writes the same clause neutrally — "the recorded test results
  and diff" — so the library is clean and the concept's own copy of it is not.
- `build` "treats warnings as errors — enforced by `Directory.Build.props`" (§3.3), a
  framework-level claim in MSBuild terms.
- "The analyzer policy **every profile** ships" is MSBuild XML (§10.4), which only a .NET
  profile can ship.

Against §1's "it has a strong opinion about the stack it scaffolds, and that opinion is
swappable", the evidence contract that `verify`, `CON_08` and `CON_12` all derive from is
not swappable.

### S11 — `verify-chain` checks less than the local layer it is meant to guarantee

The third layer is the promise: "nothing merges unless the artifact chain is complete,
approved, and hash-matched", and it is the only non-bypassable one (§2.1).

What turns the plan from a description into a commitment is a two-way comparison,
reported as `UNDECLARED` **and** `UNDELIVERED` (§3.2), and §4.4 states both halves:
"every changed file is declared in the plan, and every declared file changed."

`verify-chain` checks only "every changed file was declared in the plan" (§12). The
undelivered half — a plan that claimed a file and never touched it — is enforced only in
the layer §2.1 marks as bypassable by a human with a shell.

---

## Textual — stale or self-correcting prose

### T12 — `adopted_at` is dropped three times and alive twice

Dropped with evidence: `solution.md` "is two keys. It was three; `adopted_at` is dropped"
(§7.3, line 788), and "**`adopted_at` is dropped, and the evidence is unambiguous**… No
skill, tool or hook ever reads it" (line 807).

Still alive in the layout: `solution.md  profile, origin, adopted_at — written by init`
(§15, line 1451).

Still alive in the sequencing note: the escalation trial "also closes the brownfield half
of `adopted_at` (§7.3) if it runs in a repository with pre-existing code" (§16, line
1561) — closing the brownfield half of a field that no longer exists.

### T13 — `MofonCode.Core` both ships analyzers and ships none

"It adds `MofonCode.Core` — a minimal mediator… and **analyzers that keep a vertical
slice in shape**" (§10.2, line 1059).

Forty lines later: "**Correction, from reading the package: `MofonCode.Core` ships no
custom analyzers.** There is no analyzer project, no diagnostic ids, and no source
generator." The correction never reached the sentence it corrects.

The same section then settles where new analyzers go — "**Custom analyzers are developed
in the profile that declares the convention**, not in `MofonCode.Core`" (line 1097) — and
open question 1 contradicts it: "Writing them is new work in `MofonCode.Core`, not
something to inherit" (§18, line 1588).

### T14 — `CON_10` binds a thing the design deleted

Waivers are gone: `accepted_findings` "replaces iteration 3's `waivers.md`: a waiver is
an accepted finding on the approval entry" (§4.2).

The carried-forward table still reads "`CON_10` | a waiver's reason says why the finding
is acceptable | a waiver" (§6.5), and the section flags it as needing rewording. The
shipped `CONSTRAINTS.md` has already been reworded — "An accepted finding's reason says
why the finding is acceptable", applying to "an approval decision that lists accepted
findings" — so the concept is behind its own library rather than ahead of it.

The old vocabulary also survives as the `waived` disposition (§5) and as "waive" in the
status panel (§13).

---

## N1–N9 — smaller, still concrete

**N1. Citation format for revision 1 is given two ways.** Revision 1 is bare and becomes
`CON_01.2` on rewording (§6.4), and `CONSTRAINTS.md` states it outright: "A constraint
with no revision suffix is revision 1." Both worked examples cite `CON_04.1` — the
findings JSON (line 422) and the status panel (line 1392). The schema's `cites` pattern
cannot satisfy both, and §6.4's accepted cost ("the schema's `cites` pattern has to admit
the revision suffix") assumes the bare form.

**N2. "Three files in `.sdlc/`"** (§7, line 749) against a layout showing four —
`README.md` is in the layout (§15) and absent from the mutability table, so its
mutability and protection are unstated.

**N3. The id collision detector does not exist at allocation.** "Two cycles opened in the
same second would collide; the directory already existing is what catches it" (§15),
while directories "appear when the skill that writes the first file into them runs"
(§8.6, §15). At allocation there is no directory to collide with.

**N4. "host" means two things.** Defined precisely as the agent host, with a table of
five (§11), then reused for the CI platform: "Generated at `init`, per host" with GitHub
and Azure DevOps as the instances (§12, §12.1).

**N5. Three delivery paths, no stated precedence.** MCP is "the portability lever",
exposing every cycle stage, the gate and interrogation as tools, and is "*how the agent
does the work*" (§11.2). Rendering through `sdlc constraints` is "also how the set
reaches a session, on every host", with native delivery "an optimisation layered on top…
never the mechanism" (§6.3). The panel comes from `sdlc status` (§13), and fresh-context
interrogation runs through the host's own `-p` mode (§5). Nothing says which is
authoritative.

**N6. The panel is not ASCII-safe.** Rule four requires "ASCII-safe glyphs and a no-color
mode" (§13); the example panel uses `·` (U+00B7) in two places, and the ledger example
uses `…` (U+2026) in its hashes.

**N7. Nothing makes an unargued stack departure illegal.** "**A plan may depart from any
default. Silence about departing is what is illegal**" (§7.2) — in a section that opens
by stating `techstack.md` is not enforced and "nothing here binds by itself", and with no
constraint among the twelve covering it. A constraint about arguing departures would be
stack-neutral and therefore admissible; none exists.

**N8. No skill prints a denial, and the hashes cannot be hand-written.** The approval
skill "**refuses to print** while any blocking finding is open and unlisted" (§4.3), so a
person accepting a blocker gets no entry to paste — yet the same section concedes "a
pasted hash cannot be checked by eye", and the plugin never writes the file (§4.2). The
ledger's `changes_requested` entry (§4.2) has no printer named either, and §4.3's "a
revisit is an edit" assumes one exists.

**N9. The self-healing loop owns no skill.** "One command writes an artifact, interrogates
it, works its own blocking findings, and interrogates again" (§5.0, line 470). The skill
inventory (§14) lists seventeen skills and none of them is that command; iteration 3's
nearest equivalent, `run`, is recorded as never run by any cycle or test (§16).
