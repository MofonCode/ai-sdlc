---
id: 20260927-0001
spec: spec.good.md
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
- Criterion 3 - the test project compiles against `IEnumerable<decimal>` and is built by the same
  `dotnet test` run, so a signature change breaks the build rather than passing quietly.

## Risk
Seeding changes behaviour for null input from a null-reference throw to something else. The spec
does not ask about null, so the change leaves null handling exactly as it is, and criterion 2's
test does not exercise it.
