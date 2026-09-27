---
id: 20260927-0001
spec: .sdlc/cycles/20260927-0001/spec.md
tier: local
---

# Plan - Totals.Sum accepts an empty sequence

## Files

| File | Change |
|---|---|
| `src/Greenfield.Core/Totals.cs` | seed the accumulation so an empty sequence totals to zero |
| `tests/Greenfield.Core.Tests/TotalsTests.cs` | new: a test for the change |
| `tests/Greenfield.Core.Tests/Greenfield.Core.Tests.csproj` | new: test project referencing Greenfield.Core |
| `Greenfield.slnx` | add the test project |

## Proof

- Criterion 1 - a test asserts `Totals.Sum` does not throw for an empty sequence.
- Criterion 2 - covered by criterion 1.
- Criterion 3 - manual check that callers still compile.

## Risk
Low.
