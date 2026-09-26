---
cycle-id: 20260926-26a1
spec: 20260926-26a1
charter-version: 4
pack-version: null
predicted-tier: 1
---

# Plan — one predicate, and the first execution of a script that has never run

Four files. The predicate is a few lines; the weight of this cycle is in making
`Complete-SdlcCycle.ps1` actually execute, which no test has ever done.

## Files

| Path | Change |
|---|---|
| tools/Get-SdlcCycleState.ps1 | modify |
| tools/Complete-SdlcCycle.ps1 | modify |
| tests/Sdlc.Tests/CycleStateTests.cs | modify |
| tests/Sdlc.Tests/CycleLandingTests.cs | new |
| tools/Get-SdlcFacts.ps1 | modify |
| tests/Sdlc.Tests/FactGatheringTests.cs | modify |

**The last two entered during build, and the spec says why.** `orphan-annotations` compares every
annotation in the suite against one spec's criteria, so this cycle's verification was blocked by
`20260926-46a0`'s 27 annotations. It was listed out of scope on the strength of being a separate
subject, which was true, and of not being a blocker, which was false. A disposition cannot clear a
mechanical check, so the only alternatives were to fix it or to abandon the cycle.

Six files, still tier 1: no flagged path, and well under the size threshold.

## What each file carries

**`tools/Get-SdlcCycleState.ps1`** is the fix. The `done` branch stops asking whether `retro.md`
exists and asks whether it is reachable from local `master`, degrading to presence where no `master`
resolves and where the lookup cannot run. Its header is corrected in the same change: the claim that
the retro *"is written only at landing"* is what licensed the wrong conclusion, and a comment that
misdescribes the code beside it is how this survived two readings.

**`tools/Complete-SdlcCycle.ps1`** carries **no change to its precondition** — that is the point, and
AC-10 is the criterion that holds the line. What it does carry is its own header, which describes the
retro's timing correctly and should say plainly that the state it requires is the state a retro on an
unmerged branch produces. One sentence, so the next reader does not have to hold two files in their
head to see why the precondition is satisfiable.

**`tests/Sdlc.Tests/CycleStateTests.cs`** takes the predicate in both directions: a retro on a branch
and not on master reads `awaiting-landing`; a retro on master reads `done`; the reason names landing;
and a repository with no master degrades rather than throwing.

**`tests/Sdlc.Tests/CycleLandingTests.cs`** is new, and it is the file that matters. It drives
`Complete-SdlcCycle.ps1` for the first time, in throwaway repositories, because a script that merges,
commits, pushes and opens a pull request should not have its first execution against this repository.
It asserts: a verified cycle with a retro is accepted rather than refused as `done`; one without a
retro is still refused; the suggestions intake is written; a repository with no remote reaches a
terminal outcome naming the remote rather than a refusal; and every non-landable state is still
refused.

## Tier

**1**, from the gatherer: four files, under the ten-file threshold, and no flagged path — no auth, no
migrations, no contracts, no secrets, no CI definition, no published API. Reversible by revert.

Worth stating, since the tier is what is being approved: this changes a **lifecycle predicate** that a
dispatcher reads. The failure mode that matters is not a broken build but a cycle that reads
`awaiting-landing` after it has landed, which would have a dispatcher offer to land what is already on
master. That mirror case is why AC-1 and AC-2 are both criteria rather than one.

## Order

1. The predicate and its four tests first, because everything else depends on the state being right.
2. The landing tests second. They are written against the corrected predicate, and until it is
   corrected the script cannot be reached at all.
3. Both headers last, since they only describe what the code now does.
