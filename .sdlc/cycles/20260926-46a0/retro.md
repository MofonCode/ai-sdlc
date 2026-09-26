---
cycle-id: 20260926-46a0
solution: ai-sdlc
landed-at: 2026-09-26
charter-version: 4
pack-version: null
outcome: landed-with-accepted-findings
rounds: 4
caused-by: unknown
suppression-count: 1
---

# Retro — the gate that could be walked around, and the two checks that cried wolf

The cycle closed a hole in INV-08's enforcement and, to do it, had to repair the two mechanical
checks that made verification unpassable in this repository. **This is the first cycle here that
reached a passing verification**, which makes it the first honest datapoint about the loop rather
than about building the loop.

## What happened

Four interrogations — the intent twice, the spec twice — produced **46 blocking and 47 suggested
findings**. 42 blocking were fixed, 4 accepted with reasons, none left outstanding. The second round
of each artifact existed because the first round's repairs introduced new defects, which is the
measured behaviour of repair rounds rather than a surprise.

Three defects were found by the build itself, not by any interrogation:

- **The gate denied the one case that must always work.** Resolving the branch before testing whether
  the file exists denies `init` writing a first charter, because `git rev-parse` fails where there is
  no commit. The spec had already corrected this once — its first version tested the baseline instead
  of the disk — and the ordering error survived that correction. Caught by a test on the first run.
- **A literal doubled ampersand inside a regex failed the build.** The construct check reads comments
  too and is deliberately blunt. It was right; the plan's expectation that the file carrying it needed
  editing was wrong.
- **The spec miscounted its own corrections**, and attributed a false positive to a spelling the
  scanner never matched. Both corrected in the document rather than dismissed.

## What this says about the process

**The interrogator is worth its cost and the fan-out is real.** 93 findings from one cycle that
produced eleven files. Two of the three defects above would have shipped without a test; none of the
three came from an interrogation. So interrogation and testing are catching different things, and
neither substitutes for the other.

**A repair round is where findings are introduced.** Both artifacts needed a second round, and in
both the second round found defects in text written during the first repair. The no-progress rule is
what stops that becoming a loop, and it did not need to fire here.

**Mechanical false positives are more expensive than missing checks.** Two of them blocked every
cycle in this repository, including the cycles that would have fixed them, and a disposition cannot
clear a mechanical check. The scanners now prefer false negatives explicitly — masking can only lose
a finding, never invent one — and both directions are tested, because a scanner that reports nothing
passes the false-positive half and is useless.

## Defects in the instruments, found by using them

These are about the tool measuring itself, and none is fixed here:

- **`plan-adherence` and `actual-blast-radius` read the working tree, not the cycle's commits.** Once
  the work is committed both measure an empty diff, so `tier-escalated: false` is vacuous rather than
  verified at that point. They are only meaningful before the commit.
- **`plan-adherence` reads a cycle's own `.sdlc` artifacts as undeclared**, because no plan declares
  its own spec, plan, findings or retro. Every cycle will report drift it did not commit.
- **`orphan-annotations` is computed against one spec's criteria.** It is clean with one cycle's
  annotations in the suite; once a second cycle annotates a test, each cycle's annotations read as
  orphans of the other's spec. This check will break on the next cycle that annotates anything.
- **`plan-files-consistent` fails for a file declared `new` once that file exists**, so it can only
  pass before the work it describes is done — the same shape as the traceability check firing at spec
  stage, which the spec already recorded as out of scope.
- **`Test-SdlcArtifact` cannot validate a findings document.** The kind resolver does not map
  `<target>.r<n>.json` to `findings`, so it reports kind `unknown` and skips with `ok: true`. The
  documents are validated by `Add-SdlcFindings` directly, so nothing is unchecked — but the generic
  validator silently passes a document it never read.
- **`Get-SdlcFacts.ps1` calls git directly rather than through `SdlcGit.ps1`.** Under some invocation
  shapes git's ordinary stderr becomes a terminating ErrorRecord, which is exactly the failure
  `SdlcGit.ps1` exists to prevent.
- **`Set-SdlcDisposition` still mangles a reason containing a double quote.** Worked around again this
  cycle by writing reasons without them.

## Findings

Generated from the filed findings documents, not transcribed: a table of an agent's own design is a
table no query can read, and the amendment signals are computed from this.

| Finding | State | Category | Stage | Detection | Role | Cites | Disposition | Why |
|---|---|---|---|---|---|---|---|---|
| `intent-r1-reason1` | blocking | charter-violation | intent | reasoning | charter | invariant INV-01 | fixed | Licence now fails closed on any question it cannot positively establish, and the mirror failure mode is recorded. The presence-versus-approval question is answered explicitly as an accepted consequence - an edit on an unmerged branch is a proposal and the merge is the human grant - and the INV-08 wording mismatch is raised for its own charter intent rather than settled here. No new protected path is needed, so the out-of-scope line no longer rules out its own remedy. |
| `intent-r1-reason5` | blocking | internal-contradiction | intent | reasoning | coherence | locations | fixed | The licence is path-bound throughout: charter path to charter-review, pack paths to pack-review, and both cross cases named as refusals. The single licence question takes the path as well as the branch. |
| `intent-r1-reason8` | blocking | unfounded-claim | intent | reasoning | facts | invariant INV-07 | fixed | Cause corrected to the mechanism the tree has: the matcher never routes a shell call, the script re-checks the four tool names itself, and a recognised tool yielding no recognised path is DENIED - so widening the matcher without teaching the gate to read a command would deny every shell command in the repository. |
| `intent-r1-reason12` | blocking | missing-case | intent | reasoning | gaps | invariant INV-01 | fixed | The baseline on master is HEAD itself: an uncommitted difference fails, a committed one arrived through a merge, which is the grant. |
| `intent-r1-reason13` | blocking | missing-case | intent | reasoning | gaps | invariant INV-01 | fixed | The check faulting is now distinct from having no baseline. No baseline skips with a reason; a fault fails, because a check that cannot run has approved nothing. |
| `intent-r1-reason15` | blocking | missing-case | intent | reasoning | gaps | invariant INV-01 | fixed | Added an observable criterion: a test asserts both mechanisms resolve the licence through the same shared function, and a construct check fails a second implementation. |
| `intent-r1-reason2` | suggested | duplicated-truth | intent | reasoning | charter | — | — |  |
| `intent-r1-reason3` | suggested | internal-contradiction | intent | reasoning | coherence | — | — |  |
| `intent-r1-reason4` | suggested | internal-contradiction | intent | reasoning | coherence | — | — |  |
| `intent-r1-reason7` | suggested | internal-contradiction | intent | reasoning | coherence | — | — |  |
| `intent-r1-reason9` | suggested | unfounded-claim | intent | reasoning | facts | — | — |  |
| `intent-r1-reason10` | suggested | unfounded-claim | intent | reasoning | facts | — | — |  |
| `intent-r2-reason4` | blocking | internal-contradiction | intent | reasoning | coherence | locations | fixed | The spec names the baseline chain explicitly and the check reports which one it used, so the master case is stated rather than implied. |
| `intent-r2-reason5` | blocking | internal-contradiction | intent | reasoning | coherence | locations | fixed | The spec separates could-not-evaluate from no-write-found and criterions both, so the two no longer resolve in opposite directions by accident. |
| `intent-r2-reason8` | blocking | unfounded-claim | intent | reasoning | facts | invariant INV-07 | fixed | Ownership claim deleted. The line now records that the twelve are owned by nothing because that cycle reads done, and that landing with them undispositioned is its own defect. |
| `intent-r2-reason9` | blocking | unfounded-claim | intent | reasoning | facts | invariant INV-07 | fixed | The universal claim about every run is deleted rather than replaced. The bullet now states only the requirement - a command that merely reads a protected path must pass - which asserts nothing about call sites. |
| `intent-r2-reason10` | blocking | unfounded-claim | intent | reasoning | facts | invariant INV-07 | fixed | The git-tracking argument is deleted. The acceptance now rests only on the merge being the grant, which is a statement about the process rather than about what the gate inspects. |
| `intent-r2-reason12` | blocking | unfounded-claim | intent | reasoning | facts | invariant INV-07 | fixed | Corrected against the code: the shipped gate checks a flat list of permitted sources with no path pairing, so it accepts either source for any protected path. The path-binding is new behaviour this intent proposes, not existing behaviour. |
| `intent-r2-reason16` | blocking | missing-case | intent | reasoning | gaps | invariant INV-01 | fixed | AC-8 criterions creation with no licence for both a new pack file and a first charter in a repository with no commit, and both are tested. |
| `intent-r2-reason17` | blocking | missing-case | intent | reasoning | gaps | invariant INV-01 | fixed | The spec sites detection in the fact-gatherer and has it emit a fact field the verification outcome blocks on, which is what makes it gate rather than only report. |
| `intent-r2-reason18` | blocking | missing-case | intent | reasoning | gaps | invariant INV-01 | fixed | AC-9 makes every existing refusal a regression criterion, and all 26 shipped gate cases pass unchanged with none deleted or skipped. |
| `intent-r2-reason20` | blocking | missing-case | intent | reasoning | gaps | invariant INV-01 | fixed | A repository with no protected paths now passes rather than skips, and candidate enumeration runs before baseline resolution so the no-protected-paths case is decided first. |
| `intent-r2-reason1` | suggested | charter-violation | charter | reasoning | charter | — | — |  |
| `intent-r2-reason2` | suggested | duplicated-truth | intent | reasoning | charter | — | — |  |
| `intent-r2-reason3` | suggested | charter-violation | charter | reasoning | charter | — | — |  |
| `intent-r2-reason6` | suggested | internal-contradiction | intent | reasoning | coherence | — | — |  |
| `intent-r2-reason7` | suggested | internal-contradiction | intent | reasoning | coherence | — | — |  |
| `intent-r2-reason13` | suggested | unfounded-claim | intent | reasoning | facts | — | — |  |
| `intent-r2-reason14` | suggested | unfounded-claim | intent | reasoning | facts | — | — |  |
| `intent-r2-reason15` | suggested | missing-case | intent | reasoning | gaps | — | — |  |
| `intent-r2-reason19` | suggested | missing-case | intent | reasoning | gaps | — | — |  |
| `spec-r1-check1` | blocking | untestable | spec | check | facts | check traceability-complete | fixed | Traceability is complete: 27 of 27 verifiable criteria carry annotated tests. |
| `spec-r1-check2` | blocking | unresolvable-reference | spec | check | facts | check annotations-resolve | fixed | Both ids sit inside C# raw string literals as fixture source. The annotation scan now tests the match position against a literal mask, so fixture text is no longer read as traceability. |
| `spec-r1-check3` | blocking | gate-bypass | spec | check | facts | check suppressions-justified | fixed | The pragma spelling sits inside a quoted string in an assertion about suppressions. The suppression scan now matches against masked lines, where a real pragma survives and a quoted one does not. |
| `spec-r1-reason2` | blocking | missing-case | intent | reasoning | charter | invariant INV-01 | fixed | Destruction is now a write shape: removal, rename, move and truncation deny, with AC-22 pinning it, and AC-6 narrowed to name no protected path in any shape, so it cannot be read as licensing the destructive case. |
| `spec-r1-reason4` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-07 | fixed | The integer is gone. AC-9 now asserts the suite passes unchanged with no case deleted or skipped, which is more robust and asserts nothing a check has not verified. |
| `spec-r1-reason5` | blocking | internal-contradiction | spec | reasoning | coherence | locations | fixed | Title says two, matching the body and the criteria. |
| `spec-r1-reason13` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-07 | fixed | The running tally is dropped rather than corrected. The sentence keeps only the load-bearing part - a match inside a string literal is data, not code. |
| `spec-r1-reason14` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-07 | fixed | Diagnosis replaced with what the tree supports: SourceText is a C# test-assembly class the PowerShell scans cannot call, it strips PowerShell and JavaScript comments only, and both false positives sit in C# string literals. The work is new literal-aware scanning on the PowerShell side. |
| `spec-r1-reason15` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-07 | fixed | Corrected: the verification outcome consumes no check at all. Detection now emits both a named check for citability and a fact field the outcome reads, which is the pattern unjustified suppressions already use. AC-28 pins the gating. |
| `spec-r1-reason16` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-07 | fixed | Same correction. The spec no longer claims the outcome reads checks; it states that the outcome reads specific fact fields, and the out-of-scope entry about dispositions clearing a check is now explicit. |
| `spec-r1-reason18` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-02 | fixed | AC-19 reworded. The rule is not to ignore a quoted id - the annotation form always quotes its id. It is to ignore an annotation occurring inside a C# raw string literal, which is the shape both fixtures have, so a genuine annotation in code is untouched. |
| `spec-r1-reason19` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-01 | fixed | Creation is now a filesystem question - the path does not currently exist - so no baseline is consulted, it works in a repository with no commit, and the ordering is fixed: existence before licence. |
| `spec-r1-reason21` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-01 | fixed | AC-23 added: the gate loads the shared module inside its own try/catch, so a load failure is caught and denied rather than leaving a fail-open path. |
| `spec-r1-reason22` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-07 | fixed | AC-21 narrowed to the two scan-derived blockers being absent, rather than inferring a passing verdict the undispositioned findings would prevent. |
| `spec-r1-reason27` | blocking | missing-case | spec | reasoning | gaps | invariant INV-01 | fixed | AC-27 added for the non-firing direction: a difference that arrived by a merged amendment does not fail the check. |
| `spec-r1-reason28` | blocking | missing-case | spec | reasoning | gaps | invariant INV-01 | fixed | AC-24, AC-25 and AC-26 added for the three unpinned fail-closed cases: absent intent file, unreadable or invalid frontmatter, and a source that is neither permitted value. |
| `spec-r1-reason29` | blocking | missing-case | spec | reasoning | gaps | invariant INV-01 | fixed | The third defect now has an explicit out-of-scope entry with its reasoning, and it is why the two false positives had to be fixed rather than dismissed. |
| `spec-r1-reason1` | suggested | duplicated-truth | spec | reasoning | charter | — | — |  |
| `spec-r1-reason7` | suggested | ambiguity | spec | reasoning | coherence | — | — |  |
| `spec-r1-reason8` | suggested | internal-contradiction | spec | reasoning | coherence | — | — |  |
| `spec-r1-reason9` | suggested | internal-contradiction | spec | reasoning | coherence | — | — |  |
| `spec-r1-reason10` | suggested | internal-contradiction | spec | reasoning | coherence | — | — |  |
| `spec-r1-reason11` | suggested | internal-contradiction | spec | reasoning | coherence | — | — |  |
| `spec-r1-reason17` | suggested | unfounded-claim | spec | reasoning | facts | — | — |  |
| `spec-r1-reason20` | suggested | unfounded-claim | spec | reasoning | facts | — | — |  |
| `spec-r1-reason23` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r1-reason24` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r1-reason25` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r1-reason26` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r1-reason30` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r1-reason31` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r2-check1` | blocking | untestable | spec | check | facts | check traceability-complete | fixed | Traceability is complete: 27 of 27 verifiable criteria carry annotated tests. |
| `spec-r2-check2` | blocking | unresolvable-reference | spec | check | facts | check annotations-resolve | fixed | Fixed by the literal mask, same as spec-r1-check2. |
| `spec-r2-check3` | blocking | gate-bypass | spec | check | facts | check suppressions-justified | fixed | Fixed by the literal mask, same as spec-r1-check3. |
| `spec-r2-reason2` | blocking | charter-violation | spec | reasoning | charter | invariant INV-08 | fixed | Destruction is a write shape, so removing a protected path needs a licence for that path. The delete-then-recreate route therefore cannot complete through the gate, and the detection half reports a protected path that differs from its baseline however it got there. |
| `spec-r2-reason5` | blocking | ambiguity | spec | reasoning | charter | invariant INV-02 | accepted-wont-fix | Real ambiguity, resolved in the build rather than by amending a criterion after interrogation. Read as CharterGateTests.cs, which tests this gate; GateTests.cs tests the pack gate command and is untouched. |
| `spec-r2-reason9` | blocking | internal-contradiction | spec | reasoning | coherence | locations | fixed | Candidate enumeration now runs before baseline resolution, so no protected paths passes, no resolvable baseline skips, and a fresh ungoverned repository that is both passes rather than reporting a comparison nobody was owed. |
| `spec-r2-reason13` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-07 | fixed | The spec is corrected: the scan matches NoWarn only with its angle brackets, so that line never matched and only the pragma spelling was a real false positive. One spelling in one file, not two. |
| `spec-r2-reason16` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-07 | accepted-wont-fix | The limit is real and is now stated in the detection function header rather than left implied, and the check reports which baseline it used so a reader can tell an unlicensed commit from a merged-but-unpushed amendment. Narrowing further needs per-commit licence attribution, which the spec puts out of scope. |
| `spec-r2-reason17` | blocking | internal-contradiction | spec | reasoning | facts | locations | accepted-wont-fix | Same subject as spec-r2-reason16 and the same answer: the window between merging and pushing is reported, and which baseline was used is in the output so the two cases are distinguishable by a reader. |
| `spec-r2-reason6` | blocking | internal-contradiction | spec | reasoning | coherence | locations | fixed | The count is corrected to five corrected and one added, which is what the markers in the document actually say. |
| `spec-r2-reason20` | blocking | unfounded-claim | spec | reasoning | facts | invariant INV-07 | accepted-wont-fix | The justification names one case where a baseline cannot be consulted. Testing existence on disk is correct whether or not the repository has commits, which is a superset of the case named, and AC-8 tests the harder half. |
| `spec-r2-reason28` | blocking | missing-case | spec | reasoning | gaps | invariant INV-01 | fixed | Both declared fallbacks now have tests asserting the reported baseline kind: local master when there is no remote and HEAD is off master, and HEAD on master with no remote. |
| `spec-r2-reason30` | blocking | missing-case | spec | reasoning | gaps | invariant INV-01 | fixed | A test asserts the detection half stays silent for a protected path that exists on disk and in no commit, so the gate and the check agree that creation needs no licence. |
| `spec-r2-reason1` | suggested | charter-violation | spec | reasoning | charter | — | — |  |
| `spec-r2-reason3` | suggested | duplicated-truth | spec | reasoning | charter | — | — |  |
| `spec-r2-reason4` | suggested | unfounded-claim | spec | reasoning | charter | — | — |  |
| `spec-r2-reason7` | suggested | internal-contradiction | spec | reasoning | coherence | — | — |  |
| `spec-r2-reason15` | suggested | unfounded-claim | spec | reasoning | facts | — | — |  |
| `spec-r2-reason10` | suggested | internal-contradiction | spec | reasoning | coherence | — | — |  |
| `spec-r2-reason11` | suggested | internal-contradiction | spec | reasoning | coherence | — | — |  |
| `spec-r2-reason12` | suggested | internal-contradiction | spec | reasoning | coherence | — | — |  |
| `spec-r2-reason14` | suggested | unfounded-claim | spec | reasoning | facts | — | — |  |
| `spec-r2-reason21` | suggested | missing-case | spec | reasoning | facts | — | — |  |
| `spec-r2-reason22` | suggested | unfounded-claim | spec | reasoning | facts | — | — |  |
| `spec-r2-reason23` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r2-reason24` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r2-reason25` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r2-reason26` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r2-reason27` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r2-reason29` | suggested | missing-case | spec | reasoning | gaps | — | — |  |
| `spec-r2-reason31` | suggested | untestable | spec | reasoning | gaps | — | — |  |
