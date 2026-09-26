---
cycle-id: 20260926-26a1
intent: 20260926-26a1
charter-version: 4
pack-version: null
---

# Spec — done means landed, not written

One predicate changes. Everything else in this cycle exists to prove it changed in both directions
and to make the step it unblocks run for the first time.

## The answers

**`done` is the retro being reachable from the merge target, not the retro existing.** The current
test is `Test-Path` on the working tree, and the working tree cannot tell the two apart: a retro on an
unmerged cycle branch and a retro that merged to master are both simply *there*. So the question moves
from the filesystem to a ref lookup — is this cycle's `retro.md` in `master`'s tree — and the answer
distinguishes *written for landing* from *landed*.

**A retro on the branch and not on the target is `awaiting-landing`.** That is the state the landing
script requires, and it is the honest name for a cycle that has been verified, has written its
completion certificate, and has not merged. Nothing else about `awaiting-landing` changes: it still
requires the branch to exist and a verification to be present.

**The baseline for the lookup is local `master`, degrading explicitly.** Local `master` rather than
the remote-tracking ref, because landing is a local operation and a cycle merged locally but not yet
pushed is landed — the opposite of the choice `20260926-46a0` made for protected-path detection,
where the question was *did this reach the shared history*. Where no `master` exists the lookup cannot
be made, and the predicate falls back to the retro's mere presence, which is the behaviour it has
today. **Degrading to today's answer is deliberate**: a repository with no master has no merge target,
so there is no landing to be before or after.

**The lookup failing is not the same as the retro being absent.** A git that cannot answer must not
report a landed cycle as unlanded, because the mirror failure is the one the header already warns
about — a derivation that reads a finished cycle as unfinished would have a dispatcher offer to land
what is already on master. Where the lookup cannot run, the predicate degrades as above rather than
guessing.

**`Complete-SdlcCycle.ps1`'s precondition is not widened.** It keeps requiring exactly
`awaiting-landing`. Making it accept `done` would be the cheaper change and the wrong one: it would
leave `done` meaning *a retro was written* and add a second place that knows better. One predicate,
corrected once.

**Its first execution is against a throwaway repository.** It merges, commits, pushes and opens a
pull request, and it has never run — so its first run must be somewhere a mistake costs nothing. The
test drives it to the point where it needs a remote and asserts it got there, which is as far as a
test can go without inventing one.

**Both headers are corrected.** `Get-SdlcCycleState.ps1` says the retro *"is written only at
landing"*, and `Complete-SdlcCycle.ps1` says it *"must be on the branch before anything merges"*. The
second is precise and the first is what licensed the wrong conclusion. A comment that misdescribes
the mechanism beside it is how this defect survived two readings.

## Acceptance criteria

| ID | Verifiable | Criterion |
|---|---|---|
| AC-20260926-26a1-1 | yes | A cycle whose `retro.md` exists on its branch and not in `master` reports state `awaiting-landing`. |
| AC-20260926-26a1-2 | yes | A cycle whose `retro.md` is present in `master` reports state `done`. |
| AC-20260926-26a1-3 | yes | The reason reported alongside `done` names landing rather than the existence of a file. |
| AC-20260926-26a1-4 | yes | A repository with no `master` branch reports `done` from the retro's presence alone, and does not throw. |
| AC-20260926-26a1-5 | yes | `Complete-SdlcCycle.ps1` accepts a cycle that has a verification and a retro on its branch, rather than refusing it as `done`. |
| AC-20260926-26a1-6 | yes | `Complete-SdlcCycle.ps1` still refuses a cycle with a verification and no retro, with `refused-no-retro`. |
| AC-20260926-26a1-7 | yes | `Complete-SdlcCycle.ps1` reaches its suggestions step and writes `.sdlc/suggestions/<cycle-id>.md` for a cycle with findings. |
| AC-20260926-26a1-8 | yes | `Complete-SdlcCycle.ps1` reaches a terminal outcome that is not a refusal when run in a repository with no remote, and names the remote as what is missing. |
| AC-20260926-26a1-9 | yes | `Complete-SdlcCycle.ps1` still refuses a cycle that is `queued`, `planned`, `in-progress`, `dead` or `abandoned`. |
| AC-20260926-26a1-10 | yes | The precondition in `Complete-SdlcCycle.ps1` still requires exactly `awaiting-landing`, so the correction lives in one predicate and not in two places. |
| AC-20260926-26a1-11 | yes | `Get-SdlcCycleState.ps1`'s header no longer claims the retro is written only at landing, and says what decides `done`. |
| AC-20260926-26a1-12 | yes | No test in the suite is skipped, and none is marked inconclusive. |
| AC-20260926-26a1-14 | yes | An annotation naming a criterion of a **different** cycle is judged against that cycle's spec, not against the spec under interrogation, so a second cycle's annotations are not orphans of the first's. |
| AC-20260926-26a1-15 | yes | An annotation naming a cycle with no spec on disk is still reported as an orphan, and so is one naming a criterion its own cycle's spec does not declare. |
| AC-20260926-26a1-13 | no | Whether `Complete-SdlcCycle.ps1` behaves correctly against a real remote and a real pull request. A throwaway repository can drive it to the push, and no test can prove the GitHub half without calling GitHub. A person dispositions this from the first real landing. |

## The orphan-annotation check came into scope, and the intent was wrong to exclude it

**It blocks this cycle, and the exclusion was written before that was known.** `20260926-46a0`'s retro
predicted that `orphan-annotations` would break on the next cycle that annotated a test, and both the
intent and this spec listed it as out of scope on the strength of it being *separate* — which was true
of its subject and false of its effect. This is the next cycle that annotates a test. All 27 of
`20260926-46a0`'s annotations read as orphans of this spec, the verification outcome blocks on them,
and **a disposition cannot clear a mechanical check.** So the choice was to fix it or to stop.

That is the same shape as `20260926-46a0` itself, which had to repair two scanners to verify a change
to the charter gate. Worth naming as a pattern rather than a coincidence: **when the instrument that
measures a cycle is broken, the cycle cannot decline to fix it.** An out-of-scope line can exclude a
subject; it cannot exclude a blocker.

The fix is that **each annotation is judged against its own cycle's spec.** A criterion id carries its
cycle — `AC-<yyyymmdd>-<hhhh>-<n>` — so the annotation names the document that governs it. The check's
purpose is preserved exactly: an id naming a cycle with no spec, or a criterion its own spec does not
declare, is still an orphan. What stops being an orphan is an id that resolves correctly somewhere
else.

## Out of scope

Carried from the intent, minus the one above: the five remaining instrument defects from
`20260926-46a0`'s retro; the landing script's
re-verification running `dotnet test` directly rather than the outcome script, and hardcoding
`dotnet`; whether a plan's `approved:` field should gate landing as well as dispatch; and the
suggestion-reporting changes of `20260926-c88f`.

**This cycle lands through its own fix, and the intent was wrong to say otherwise.** The intent
claimed the repair would not be available to the cycle making it, because the fix is not on master
while it is under review. That reasoning does not hold: `Complete-SdlcCycle.ps1` resolves
`Get-SdlcCycleState.ps1` through `$PSScriptRoot`, which is the **working tree** — so a cycle branch
carrying the fix gets the fixed predicate. The claim was about the wrong location.

Corrected here rather than left standing, because it changes what this cycle proves. Landing by hand
would have demonstrated nothing; landing through the repaired script is the observable test the intent
asked for, executed on the cycle that wrote it. What remains genuinely unproven is only AC-13 — the
GitHub half — since no test can call GitHub.
