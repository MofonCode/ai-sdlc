---
cycle-id: 20260926-26a1
solution: ai-sdlc
landed-at: 2026-09-26
charter-version: 4
pack-version: null
outcome: landed
caused-by: unknown
suppression-count: 1
---

# Retro — the last step of the loop runs, and this cycle is the one that used it

`tools/Complete-SdlcCycle.ps1` had never executed. Not once, in any cycle. It refused anything but
`awaiting-landing` and it also refused a cycle with no `retro.md` on the branch — while the lifecycle
derivation called a cycle `done` the moment a retro existed. No input satisfied both. **This cycle
fixed that and then landed through the repaired script**, which is the only evidence that actually
counts for a step nothing had ever run.

## What happened

**No interrogation.** Decided deliberately and recorded rather than skipped quietly: the defect was
already precisely diagnosed — found by trying to land the previous cycle — the fix is one predicate,
and the previous cycle's four interrogation rounds produced 93 findings of which 85 remain untriaged.
The cost did not look like it would earn its return on a three-file change to a lifecycle predicate.
Fifteen criteria carry tests instead.

**The build found two defects within a minute of each other**, and both are shapes this repository has
already recorded:

- **`git cat-file -e` returns 128 both for a path that is absent and for a ref that does not resolve.**
  The first draft read 128 as *lookup failed* and degraded to presence — which would have called every
  unlanded retro landed, leaving the deadlock exactly where it was while passing a hastily written
  test. `ls-tree` distinguishes the three cases in its output instead. **This is the most instructive
  thing in the cycle**: the fix would have been indistinguishable from the bug, and only checking git's
  actual exit codes before relying on them caught it.
- **`$home` is read-only in PowerShell**, and variable names are case-insensitive.

## Running it found three more defects, all in the script being repaired

The strongest argument in this cycle for executing a thing rather than reading it. None of these was
visible in four readings of the file; all three appeared in its first two runs.

1. **Its success message announced a suggestions intake that does not exist.** Printed
   unconditionally, for a cycle that filed no interrogation. And the path was rendered
   `.sdlc/suggestions/ + 20260926-26a1.md + ,` because `'a' + "$b" + 'c'` passes three positional
   arguments to `Write-Host`, which joins them with spaces.
2. **The first pull request it ever opened was titled `<id>: <id>: done means landed`.** The script
   prefixes the cycle id to a commit subject that house convention already prefixes, so the doubled
   form was the normal case rather than an edge one.
3. **The retro schema refuses `rounds: 0`** — see below.

All three are fixed here. Worth stating why they belong in this cycle rather than a follow-up: a
landing step whose output misreports what it did is a landing step that misreports, and this cycle's
subject is that step working.

## The pattern that has now appeared twice

**When the instrument that measures a cycle is broken, the cycle cannot decline to fix it.**

`20260926-46a0` had to repair two scanners to verify a change to the charter gate. This cycle had to
repair `orphan-annotations` to verify a change to the landing predicate — and it had listed that check
out of scope, on the strength of being a separate subject. True of its subject, false of its effect: a
disposition cannot clear a mechanical check, so a broken check is a blocker whatever the spec says
about it.

**The previous retro predicted this one exactly.** It recorded that `orphan-annotations` would break on
the next cycle that annotated a test. It did, on the next cycle, and it blocked verification until it
was fixed. That is the retro corpus doing the job it exists for, one cycle after being written — which
is the first evidence in this repository that the signal half of the loop works at all.

## A third occurrence of one defect shape

A blunt source check matching the code that explains it. Three times now:

1. A literal doubled ampersand inside a regex, in the previous cycle.
2. A header quoting the sentence it was correcting, earlier in this cycle.
3. The suite-hygiene check added in this cycle reporting its own file, because the attribute names it
   searches for appear in the search.

`SourceText`'s own remarks document this shape and say the rule is that a construct check reads
stripped code. The rule exists; it is not reached by checks written outside that helper, and two of the
three above were in PowerShell and markdown where the helper does not apply. **A rule that holds in
one of the three places it is needed reads as enforced and is not.**

## What the intent got wrong

It said this cycle would land by hand, because the fix is not on master while under review. The
reasoning was about the wrong location — the landing script resolves the state script through
`$PSScriptRoot`, which is the working tree — so the cycle carrying the fix gets the fix. Corrected in
the spec. It mattered: landing by hand would have demonstrated nothing about the repair.

## Instrument defects still open

Six, from the previous retro's seven, this cycle having taken one:

- `plan-adherence` and `actual-blast-radius` read the working tree, not the cycle's commits, so both
  are vacuous once the work is committed and meaningful only before it.
- `plan-adherence` reads a cycle's own `.sdlc` artifacts as undeclared, because no plan declares its
  own spec, plan or findings. Both this cycle and the last reported drift they did not commit.
- `plan-files-consistent` fails for a file declared `new` once that file exists, so it can only pass
  before the work it describes is done.
- `Test-SdlcArtifact` cannot validate a findings document: the kind resolver does not map
  `<target>.r<n>.json` to `findings`, so it reports `unknown` and skips with `ok: true`.
- `Get-SdlcFacts.ps1` calls git directly rather than through `SdlcGit.ps1`, so under some invocation
  shapes git's ordinary stderr becomes a terminating error.
- `Set-SdlcDisposition` mangles a reason containing a double quote. Not hit this cycle, because there
  were no findings to disposition.

## A defect in the retro schema, found by being the first cycle with nothing to report

`rounds: 0` is refused: the schema requires at least 1. **A cycle that ran no interrogation cannot
state so in its retro.** The field is optional, so this retro omits it rather than writing 1, which
would be false — but omission and zero are different claims, and the schema currently permits only
the one that hides the fact.

Two honest answers and this cycle picks neither, because both are outside it: either a cycle may
legitimately run no interrogation, in which case `0` is a valid count and the schema is wrong, or the
process requires at least one, in which case the charter should say so and this cycle skipped a
required step rather than an optional one. **The charter says nothing either way** — checked, not
assumed. That is the question to settle, and it is charter-level.

## Findings

No interrogation was run, so there is nothing to render. Recorded as an absence rather than omitted:
this cycle produced no findings because none was sought, which is not the same as a clean cycle.
