---
cycle-id: 20260926-26a1
plan: 20260926-26a1
charter-version: 4
pack-version: null
outcome: passed
predicted-tier: 1
actual-tier: 1
suppression-count: 1
criteria-total: 15
criteria-verifiable: 14
criteria-covered: 14
undispositioned: 0
---

# Verification — the landing script ran

`tools/Get-SdlcVerifyOutcome.ps1` reports **passed**: *every gate passed and nothing is outstanding.*

| Gate | Result |
|---|---|
| build | passed |
| tests | passed — 576, none skipped |
| verifiable criteria covered | 14 of 14 |
| unjustified suppressions | 0 |
| orphan annotations | 0 |
| unlicensed protected paths | 0 |
| undispositioned findings | 0 |
| tier | predicted 1, actual 1, not escalated |

## What this cycle proved

**`Complete-SdlcCycle.ps1` executed.** Four tests drive it in throwaway repositories, and it reaches
`verified-no-remote` — the terminal outcome for a repository with nowhere to push. Before this cycle
every reference to that script in the suite read it as text, because no input could satisfy both its
precondition and its own step 4.

The predicate is asserted in both directions, which is the point: a retro on an unmerged branch reads
`awaiting-landing`, a retro present on master reads `done`, and a repository with no master degrades
to presence rather than throwing. The mirror error is the dangerous one — a finished cycle reading as
unfinished would have an unsupervised dispatcher offer to land what is already on master.

## Two defects the build found

Both caught within a minute of writing them, and both are the same shape as defects this repository
has already recorded:

1. **`git cat-file -e` cannot tell an absent path from an unresolvable ref.** Both exit 128. The first
   draft treated 128 as *lookup failed* and degraded to presence — which would have read every
   unlanded retro as landed and left the deadlock exactly where it was, while passing a
   quickly-written test. `ls-tree` answers in its output instead: exit 0 with a line means present,
   exit 0 with nothing means absent, non-zero means the lookup could not be made. It is also silent
   on stderr for a resolvable ref, which matters under this script's `ErrorActionPreference = 'Stop'`.
2. **`$home` is read-only in PowerShell.** Variable names are case-insensitive, so a local named
   `$home` collides with `$HOME` and the assignment throws. Renamed.

## The scope change, and why it was not optional

`orphan-annotations` compared every annotation in the suite against **one** spec's criteria. The
previous cycle's retro predicted this would break on the next cycle that annotated a test; this is
that cycle, and all 27 of `20260926-46a0`'s annotations read as orphans of this spec. The verification
outcome blocks on them and **a disposition cannot clear a mechanical check**, so the choice was to fix
it or to abandon the cycle.

Both the intent and the spec had listed it out of scope — correctly as a *subject* and wrongly as a
*blocker*. The pattern is worth naming, because this is the second consecutive cycle it has appeared
in: **when the instrument that measures a cycle is broken, the cycle cannot decline to fix it.** An
out-of-scope line can exclude a subject; it cannot exclude a blocker.

## One claim the intent got wrong

The intent said this cycle would have to land by hand, because the fix is not on master while under
review. That reasoning was about the wrong location: `Complete-SdlcCycle.ps1` resolves
`Get-SdlcCycleState.ps1` through `$PSScriptRoot`, which is the working tree, so a cycle branch
carrying the fix gets the fixed predicate. Corrected in the spec, and it changes what this cycle
demonstrates — landing through the repaired script rather than around it.

## What remains unproven

- **AC-13, declared non-verifiable.** Whether the landing script behaves correctly against a real
  remote and a real pull request. A throwaway repository drives it to the push; no test can prove the
  GitHub half without calling GitHub. This cycle's own landing is the first real evidence, and a person
  dispositions it from that.
- **`actual-tier` and `plan-adherence` still read the working tree**, not the cycle's commits, so both
  are vacuous once everything is committed. Recorded in the previous cycle's retro and still true.
- **Six instrument defects** from `20260926-46a0`'s retro remain. That retro listed seven; this cycle
  took one of them, the orphan-annotation check. Left: `plan-adherence` and `actual-blast-radius`
  reading the working tree rather than the cycle's commits; `plan-adherence` reading a cycle's own
  `.sdlc` artifacts as undeclared; `plan-files-consistent` failing for a file declared `new` once that
  file exists; `Test-SdlcArtifact` unable to validate a findings document, so the generic validator
  skips with `ok: true`; `Get-SdlcFacts` calling git without the wrapper that keeps stderr from
  becoming a terminating error; and `Set-SdlcDisposition` mangling a reason containing a double quote.
