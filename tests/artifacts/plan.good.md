---
id: 20260927_0001
spec: .sdlc/cycles/20260927_0001/spec.md
tier: local
---

# Plan - Totals.Sum accepts an empty sequence

## Files

| File | Change |
|---|---|
| `src/Greenfield.Core/Totals.cs` | seed the accumulation so an empty sequence totals to zero |
| `tests/Greenfield.Core.Tests/TotalsTests.cs` | new: empty returns zero, known sequence returns its total |
| `tests/Greenfield.Core.Tests/Greenfield.Core.Tests.csproj` | new: test project referencing Greenfield.Core |
| `Greenfield.slnx` | add the test project |

## Proof

- Criterion 1 - `TotalsTests.EmptySequenceTotalsToZero` passes `Array.Empty<decimal>()` and asserts
  the result is `0m`. It fails before the change, because the current body throws.
- Criterion 2 - `TotalsTests.KnownSequenceTotalsToItsSum` passes three known amounts and asserts
  their exact total, so a change that silently returned zero for everything would fail it.
- Criterion 3 - `PublicAPI.Shipped.txt` records `Totals.Sum(IEnumerable<decimal>) -> decimal`, and
  `Microsoft.CodeAnalysis.PublicApiAnalyzers` fails the build on any difference between it and the
  compiled surface. That file is not in this plan's file list, so the build fails if the signature
  moves at all. Neither a new test nor an existing caller would prove this: a test written in this
  change compiles against whatever the signature became, and a caller passing a narrower type keeps
  compiling when the parameter is narrowed.

## Risk
Seeding changes behaviour for null input from a null-reference throw to something else. The spec
does not ask about null, so the change leaves null handling exactly as it is, and criterion 2's
test does not exercise it.
