---
cycle-id: 20260926-46a0
plan: 20260926-46a0
charter-version: 4
pack-version: null
outcome: passed
predicted-tier: 2
actual-tier: 2
suppression-count: 1
criteria-total: 28
criteria-verifiable: 27
criteria-covered: 27
undispositioned: 0
---

# Verification — the first one in this repository that passed

`tools/Get-SdlcVerifyOutcome.ps1` reports **passed**: *every gate passed and nothing is outstanding.*
Recorded here with what it measured, because the outcome is a computed answer and the record is what
travels.

| Gate | Result |
|---|---|
| build | passed |
| tests | passed — 564, none skipped |
| verifiable criteria covered | 27 of 27 |
| unjustified suppressions | 0 |
| orphan annotations | 0 |
| unlicensed protected paths | 0 |
| undispositioned findings | 0 |
| tier | predicted 2, actual 2, not escalated |

## Why this is worth stating plainly

**Before this cycle no cycle in this repository could pass verification, including any cycle trying
to fix something else.** Two mechanical checks reported false positives on this tree —
`suppressions-justified` on a pragma spelling inside a quoted string, and `annotations-resolve` on
annotated test methods inside C# raw string literals — and the verification outcome blocks on both by
reading the gatherer's raw results. A disposition cannot clear a mechanical check, so the only way
out was to fix the scanners. That is why they were in scope for a cycle about the charter gate.

## Findings

46 blocking findings across four interrogations — the intent twice, the spec twice. **42 fixed, 4
accepted-wont-fix with reasons, 0 outstanding.** 85 suggested findings were raised and none was
promoted; they accumulate as evidence.

The four not repaired are recorded rather than quietly closed:

- **`spec-r2-reason5`** — a criterion said *the shipped gate test file* where two exist. Resolved in
  the build as `CharterGateTests.cs`, which tests this gate; `GateTests.cs` tests the pack gate
  command and is untouched. Not fixed by amending a criterion after interrogation.
- **`spec-r2-reason16` and `spec-r2-reason17`** — between merging an amendment and pushing it, a
  difference from the remote-tracking baseline is indistinguishable from an unlicensed direct commit.
  Real, now stated in the detection function's header, and distinguishable by a reader because the
  check reports which baseline it used. Narrowing it needs per-commit licence attribution.
- **`spec-r2-reason20`** — a justification named one case where no baseline can be consulted. The
  mechanism chosen, existence on disk, is correct whether or not the repository has commits, which is
  a superset of the case named.

## Three defects this cycle's own build found

Recorded because they were found by using the tool on itself, which is the only reason they surfaced:

1. **The gate denied the one case that must always work.** Asking for the branch before asking
   whether the file exists denies `init` writing a first charter, because `git rev-parse` fails in a
   repository with no commit. Caught by AC-8's test on the first run.
2. **A literal `&&` in a regex failed the build.** `PowerShellScriptTests` forbids the text anywhere
   in a 5.1 script, comments included. The check was right and the plan's expectation that the file
   needed editing was wrong.
3. **The spec miscounted its own corrections** and misattributed one of the two suppression
   spellings. Both corrected in the document.

## What verification does not cover, and should be read as unproven

- **`actual-tier` is computed from the working tree, not from the cycle's commits.** Once everything
  is committed the measurement is of an empty diff, so `tier-escalated: false` is vacuous at that
  point rather than verified. The value above was taken before the final commit, when the diff was
  the change.
- **`plan-adherence` has the same shape** and additionally reads a cycle's own `.sdlc` artifacts as
  undeclared, because no plan declares its own spec, plan or findings.
- **`orphan-annotations` is computed against one spec's criteria.** With one cycle's annotations in
  the suite it is clean; once a second cycle annotates a test, each cycle's annotations read as
  orphans of the other's spec. Found while writing AC-21's test, which is why that test asks the
  narrower question that stays true: no annotation names a criterion its own spec fails to declare.
- **Whether the harness delivers `PreToolUse` for the PowerShell tool.** AC-16, declared
  non-verifiable. The gate's decision is tested by feeding it a payload; whether such a payload ever
  arrives is dispositioned by a person from the debug log.
