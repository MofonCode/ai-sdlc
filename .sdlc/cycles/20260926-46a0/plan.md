---
cycle-id: 20260926-46a0
spec: 20260926-46a0
charter-version: 4
pack-version: null
predicted-tier: 2
---

# Plan — the shell route, and two literal-aware scans

Eleven files. The three changes to the gate are **one change**: the matcher, the script's own
tool-name check and the path extraction have to move together, because the gate denies a recognised
tool that yields no recognised path — so widening any two of the three without the third turns it
into a total block on shell commands.

## Files

| Path | Change |
|---|---|
| tools/SdlcProtectedPaths.ps1 | new |
| tools/Assert-SdlcCharterGate.ps1 | modify |
| hooks/hooks.json | modify |
| tools/Get-SdlcFacts.ps1 | modify |
| tools/Get-SdlcVerifyOutcome.ps1 | modify |
| workflows/interrogate.js | modify |
| tests/Sdlc.Tests/CharterGateTests.cs | modify |
| tests/Sdlc.Tests/FactGatheringTests.cs | modify |
| tests/Sdlc.Tests/VerifyOutcomeTests.cs | modify |
| tests/Sdlc.Tests/WorkflowScriptTests.cs | modify |
| README.md | modify |

**Corrected during build, and the correction is in the table above rather than only in prose.** The
plan declared `PowerShellScriptTests.cs` and it needed no edit: its construct check is data-driven
over every shipped script, so `SdlcProtectedPaths.ps1` came under it the moment the file existed —
and it immediately failed the build over a literal `&&` inside a regex, which is the check working
as designed. What did need an edit was `WorkflowScriptTests.cs`, because the criterion requiring the
new check id to be classified is satisfied by the binding test that already lives there, and a
criterion covered by an existing test needs that test to say so or traceability reads it as unproven.

Same count, one file swapped. Recorded here because the alternative is a plan that says something
untrue about the change it describes, and `plan-adherence` reports both halves — an undeclared file
and a declared one untouched — so hiding it was never available.

## What each file carries

**`tools/SdlcProtectedPaths.ps1`** is the one licence answer, dot-sourced by both mechanisms. It holds
the four protected-path patterns, the path-bound licence test — *does this branch carry an intent
licensing a write to THIS path* — the creation test, and the write-shape classifier for a command
string. It fails closed: anything it cannot positively establish as a licence is no licence.

**`tools/Assert-SdlcCharterGate.ps1`** accepts the shell tools, extracts protected paths from a
`command` string rather than only from `file_path`, classifies write shapes including destruction, and
loads the shared module **inside its try/catch** so a load failure is caught and denied. Its four
existing refusals are untouched.

**`hooks/hooks.json`** widens the matcher. `Bash` is a name an official plugin already matches;
`PowerShell` is included because it can write files and is this stack's preferred shell, with the
caveat the spec records — whether the harness routes it is unverified and is not this cycle's to prove.

**`tools/Get-SdlcFacts.ps1`** carries three separate changes: the new protected-path check and its fact
field; the suppression scan ignoring a match inside a quoted string; and the annotation scan ignoring
an annotation inside a C# raw string literal. The second and third are what unblock verification for
every cycle in this repository.

**`tools/Get-SdlcVerifyOutcome.ps1`** blocks on the new fact field. Without this the detection half
reports and gates nothing, which is the correction the spec's second version rests on.

**`workflows/interrogate.js`** classifies the new check id, or it falls through to `unclassified` and
can never block — the rule that was already violated once by nine of fourteen checks.

**The four test files** carry the criteria. `CharterGateTests` takes the shell route and the fail-closed
cases by feeding the gate payloads directly, which is how it already works and why it can test a
decision the harness may never deliver. `FactGatheringTests` takes the new check and both scanner
fixes, in both directions — a false positive gone **and** a genuine finding still reported, because a
scanner that reports nothing passes the first half and is useless.

**`README.md`** names the new script, because a test binds that table to what ships in both directions.

## Tier

**2**, from the gatherer: eleven declared files, over the ten-file threshold, and no flagged path — no
auth, no migrations, no contracts, no secrets, no CI definition, no published API. Nothing here is
irreversible by revert.

Worth stating plainly, since the tier is the thing being approved: this changes a **protection
mechanism**. The failure mode that matters is not a broken build — the suite catches that — but a gate
that looks stricter and is weaker, or one that denies so much it gets switched off. That is why the
regression criterion and both directions of each scanner are in the spec rather than left to judgment.

## Order

1. `SdlcProtectedPaths.ps1` first — both mechanisms depend on it.
2. The gate's three changes together, with its tests, since none is safe alone.
3. The two scanner fixes with their tests. These unblock verification, so they land before anything
   needs to verify.
4. The new check, its fact field, the outcome blocker, and the category mapping — the detection half,
   which is inert until all four exist.
5. README and the construct check last, since both only describe what the others did.
