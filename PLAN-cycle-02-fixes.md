# Fixes from cycle-02

What the first full cycle in `C:\Repos\cycle-02` (adopt → intent → spec → plan → approve → build → verify,
2026-10-18) found, and how each is fixed. Every item below was reproduced or read in source on 2026-10-18;
line numbers are as of `cac7517`.

**Sequencing constraint:** `tests/Sdlc.Tests/PluginTests.cs`, `HookTests.cs` and `ArtifactSchemaTests.cs`
have uncommitted edits in progress. Work that touches those files (2.2, 4.1) waits until they land.

Each work package is one commit, with its documents in the same commit (AGENTS.md, *Keep the
documentation current*). One release at the end of phase 2, so cycle-02 can be unblocked on a reinstalled
binary.

---

## Phase 1 — unblock a verified cycle

### 1.1 The chain reads every backticked word in a proof entry as a test name

**Symptom.** cycle-02 verified `pass`, 62/62 tests green, and `sdlc pr-ready` refuses with only
`- every named test passed`. `sdlc verify-chain` gives the reason: `proof 8: MutationObserver did not run
and pass`, `proof 9: Deque.AxeCore.Playwright did not run and pass`. Those are a browser API and a package
the proof prose mentions, not tests.

**Cause.** `DeclaredFiles.TestsIn` (`src/Sdlc.Core/Evidence/DeclaredFiles.cs:135`) takes every backticked
token without a `/`. `ArtifactSections.NumberedItems` (`src/Sdlc.Core/Artifacts/ArtifactSections.cs:108`)
keeps only an item's first line, so a test named on a continuation line is never checked. That second
part is a silent weakening of the gate.

**Change (needs your call, see Decision A).** Recommended: **test names are the leading run of backticked
tokens** in a proof entry: `` 8. `TestA`, `TestB` installs a `MutationObserver`… `` names `TestA` and `TestB`
only. The convention the plan skill already shows (`skills/plan/SKILL.md:211-216`) conforms, and so does
every proof entry in cycle-02. Then:

- `TestsIn` reads the leading run; `NumberedItems` (or a sibling the chain uses) returns the whole item so
  the leading run is found whatever the wrapping
- the plan's shape check (`ArtifactSchema`) refuses a proof entry whose leading run is empty, so a plan with
  no checkable name fails **at plan time, in the author's session**, not after the build
- `pr-ready` prints each failed check's detail lines, not only its title. The person shouldn't need a second
  command to learn why

**Tests.** `TestsIn` on cycle-02's entries 8 and 9 (verbatim) returns only the test; a name on a wrapped
line is found; an entry with prose before its first backtick is refused by the schema; `pr-ready` output
contains the `proof N:` detail.

**Docs.** `skills/plan/SKILL.md` §Proof (the rule and the example); `CONCEPT.md` §7.7 if the wording of
"named test" moves; `docs/guides/handling-refusals.md` + `docs/reference/refusals.md` if the new schema
refusal is a refusal entry.

**Unblocks cycle-02** without a reopen: the approved plan is unchanged and re-reads clean under the new
rule. Confirm after release by running `sdlc pr-ready 20261018_1103_50` there.

### 1.2 A Files row's description cell is read as declared paths

**Symptom.** Evidence reports `undelivered Cycle02.Ui`, `Cycle02.Web`, `Cycle02.Ui.Tests`,
`Cycle02.EndToEnd.Tests`. They came from backticked project names in the description cell of the plan's
`Cycle02.slnx` row.

**Change.** `DeclaredFiles` (`:107` onward) reads only the first cell of a Files table row. A list item keeps
its current reading.

**Tests.** A Files table whose description cell backticks a project name declares only the first cell.

---

## Phase 2 — the binary says true things

### 2.1 `sdlc status`: crash, wrong panel, wrong Next

- **Crash.** `sdlc status <id> --stage verify` throws `InvalidOperationException` at `Program.cs:1258`,
  because `Artifacts` holds the three approved documents and verify is kept apart on `Response.Verify`
  (`ReadCycleStatus.cs:99`, deliberately). `skills/interrogate/SKILL.md:304` and `skills/refine/SKILL.md:139`
  call `--stage <artifact>`, and interrogate judges verify documents. Change: `--stage verify` renders the
  panel from `status.Verify`, and refuses cleanly when no verify document exists yet.
- **Unknown value.** `--stage bogus` falls back to the plan panel silently. Change: refuse and name the four.
- **Wrong default.** With nothing blocking, the panel defaults to the plan (`Program.cs:1254`), so after a
  verification it still reads "plan, round 4". Change: default to the furthest document that exists,
  verify included.
- **Wrong Next.** `StatusRenderer.AfterTheGate` (`StatusRenderer.cs:307`) switches only on the outcome:
  - evidence recorded but no verify document gets `sdlc build` again. Add that state → `verify`.
  - `pass` says "verified clean" while the chain fails. On `pass`, run the chain checks status can run
    cheaply and say `pr` only when they hold; otherwise name the failing check.

**Tests.** One per bullet, against a fixture cycle at each state.
**Docs.** `src/Sdlc.Cli/Usage.cs:255` (`--stage` now takes verify), `docs/reference/commands.md`.

### 2.2 The approval draft's rename line fails from the repository root

**Symptom.** You ran the printed `mv approval.approved.draft.md approval.md`, got `cannot stat`, and retried
with the full path. `ApprovalText.Instructions` (`src/Sdlc.Core/Approvals/ApprovalText.cs:421`) prints
`Path.GetFileName` while `sdlc init` prints root-relative paths and says "from the repository root".

**Change.** Print `.sdlc/cycles/<id>/approval.approved.draft.md` → `.sdlc/cycles/<id>/approval.md` in both
the `mv` and `Move-Item` lines.

**Docs.** `docs/guides/running-a-cycle.md:139` (shows bare names); check `docs/samples/rolling-a-die.md:403`
matches the new output exactly.

### 2.3 Small output defects

| What | Where | Change |
|---|---|---|
| "paste what it prints". The draft now writes a file you rename | `ApprovalGate.cs:203`, `ProtectedPaths.cs:60` | say "and rename the draft it writes" |
| init prints `brancheddevelopment` | `InitRenderer.cs:58`, `{"branched",-8}` | width 9, matching the other labels |
| `sdlc evidence --help` prints "Which cycle?" | `Program.cs`, evidence's argument handling | `--help` on any cycle verb prints that verb's usage |
| the first build always adds a root `.gitignore` (bin/, obj/, TestResults/), which then shows as `undeclared` | `src/Sdlc.Core/Init/Scaffold*` | scaffolded origin writes it; existing origin doesn't. Harness run + `ci.yml` cover it |

**Release** after phase 2: version bump, README *Where it stands*, `docs/guides/installing-the-plugin.md` if
anything it lists moved. Reinstall the tool, then in cycle-02: a person runs `sdlc init` (doctor's stale
framework files), then `sdlc pr-ready`.

---

## Phase 3 — the skills match what happens

### 3.1 interrogate: its own recipe is denied, overflows Windows, and leaves verify undefined

- `skills/interrogate/SKILL.md:23`, `cat .sdlc/techstack.md > /tmp/…`, is denied by the hook twice per plan
  round (a `>` plus a protected name). **The hook is right** (it matches command text, by design), so the
  skill changes: compose the prompt file with the host's Read and Write tools, which is what the agent
  ended up doing.
- `claude -p "$(cat <<'PROMPT' …)"` hit `Argument list too long` on Windows. Change: write the prompt to a
  file and pipe it, `claude -p --output-format text < prompt.txt`.
- The skill never says what a **verify** round reads as its upstream or which hash goes in
  `upstream_sha256`. The agent improvised a 278 KB bundle and used the plan's hash. Specify it, with the
  answer read from how `verify-chain` and staleness treat a verify round, not invented in the skill.

### 3.2 spec: bound a negative before the first round

"No animation" in Must not change drove blockers in spec r1 and r3, and plan r1 and r3. Every one was real,
and the loop converged only once the unobservable rest moved to Out of scope. Add to `skills/spec/SKILL.md`:
a universal negative is written with the bound it is checked within, and the rest goes to Out of scope in
the first draft.

### 3.3 init: answer the stack before placing the drafts

You renamed all three drafts at 11:00:28, before the interview asked anything. The stack went in as drafted
(storage and messaging rows on a solution that stores nothing), and the plan later had to argue those
departures. Change: the init *output* prints the rename lines last, after a line saying to answer the
techstack rows first. `skills/init/SKILL.md` §4 says to do this before the person places them.

### 3.4 Hand-back length

The plan hand-back added a "decisions worth reading" section; the build's ran ~10 lines against a limit of
5 plus a count. Probably not a skill change. Mention only if it recurs in cycle-03.

---

## Phase 4 — documents left by `cac7517`

| # | Change | File |
|---|---|---|
| 4.1 | Test that README and `docs/README.md` name `docs/cheat-sheet.md`, that every `/sdlc:<name>` in it is a skill on disk, and every `` `sdlc <verb>` `` is in `Usage`. Then name the cheat sheet as its own accepted copy of `docs/guides/running-a-cycle.md` in *Who owns what*, and fix the sentence ending "…and the cheat sheet." **After the in-flight `PluginTests.cs` edits land** | `PluginTests.cs`, `AGENTS.md:174, 199-201` |
| 4.2 | "every command for one cycle" → "every command" | `docs/reference/refusals.md:77`, `docs/reference/skills.md:55` |
| 4.3 | "Every command is there to be typed, including the ones a skill runs for you" is now false; reword. Add the concepts index and cheat sheet to See also | `docs/samples/README.md:21, ~32` |
| 4.4 | Point "the stages a perspective steers and judges" at `skills/README.md` (interrogate), not `docs/guides/running-a-cycle.md`; link the page from `docs/guides/handling-blockers.md` and the interview/interrogate steps of `docs/guides/running-a-cycle.md` | `docs/concepts/perspectives.md:120` |
| 4.5 | Cut the rule-earning paragraph that duplicates `docs/guides/handling-blockers.md` down to one home; either hold the "Which there are" table to `CONCEPT.md` §6.5 and the perspective documents under `src/Sdlc.Core/Perspectives/` by test, or cut its "On when" column to a pointer at `sdlc perspectives <id>` | `docs/concepts/perspectives.md`, `docs/guides/handling-blockers.md` |
| 4.6 | "the perspectives table in §6.5". It is a list | `AGENTS.md:175` |
| 4.7 | Drop the hand-written name lists (they drifted before) | `docs/README.md:11-12` |
| 4.8 | A cheat-sheet bullet: "with a remote: make the pipeline's check required" (lost with the short path) | `docs/cheat-sheet.md` |
| 4.9 | Decision B wording, if taken | `docs/README.md`, `AGENTS.md:36` |

---

## Decisions, before the work they gate

- **A. How a proof entry names its tests (gates 1.1).** Recommended: the leading run of backticked tokens,
  with the schema refusing an entry that has none. Alternatives weighed:
  - identifier-shaped tokens only, e.g. containing `_`, no `.`: silently drops a PascalCase test name, which
    weakens the gate
  - tokens matching any reported test: a misspelt name passes if a neighbour matches, which also weakens it
  - a marker such as `test:`: explicit, but noisier than a position the convention already uses

  The cost of the recommendation is that a plan approved before the release which names a second test
  after prose stops having that test checked. Say so in the release.
- **B. "Concept" now means three things** (`CONCEPT.md`, `docs/README.md`'s "every concept the framework
  names", and `docs/concepts/`). Rename the middle one "every term", or accept the clash.
- **C. Open suggestions only grow.** 69 on one cycle, ~80% from perspective readers; intent suggestions
  answered by the spec stay open forever. Options: the panel counts only the current stage's suggestions;
  a suggestion retires when a downstream document is clean; cap perspective suggestions per round. Needs a
  `CONCEPT.md` decision entry, so it is not in the phases above.
- **D. Status's Next prints `sdlc pr <id>`**, which the binary answers with "'pr' is a skill" (`Stages.cs:73`).
  The notation looks deliberate and host-neutral (`CONCEPT.md:1858` shows `sdlc refine`), but it costs a
  step each time. Keep it, or print the host's form (`/sdlc:pr <id>`) when the host is known.

## Not in this plan, worth a work item each

- Binary support for writing a round: `sdlc hash <file>`, and a round validator that rejects a placeholder
  statement or a citation whose heading belongs to another rule. The agent hand-rolled both, and a bad
  round (`"statement":"placeholder"`, CON_09 cited under CON_02's heading) was caught only by the agent.
- `sdlc evidence` takes no input for a vulnerable-package scan the security perspective asks for. It was
  concatenated into the build log.
- The profile's Playwright row doesn't name `Microsoft.Playwright.MSTest.v4`; the build swapped packages
  against the plan. `StackProfile.cs:173` and its rule document. **A new row reaches no existing solution
  until added there.**
- A line in `docs/guides/adopting-the-framework.md` that the LF→CRLF warnings on commit are expected.
