---
id: 20260927_0001
intent: .sdlc/cycles/20260927_0001/intent.md
---

# Spec - Totals.Sum accepts an empty sequence

## Acceptance criteria

1. `Totals.Sum` returns `0m` when given an empty `IEnumerable<decimal>`, asserted by a test that
   passes `Array.Empty<decimal>()`.
2. `Totals.Sum` returns the arithmetic total for a non-empty sequence, asserted by a test over a
   known sequence with a known total.
3. The public signature is unchanged: the recorded public surface of `Greenfield.Core` still lists
   `Totals.Sum(IEnumerable<decimal>) -> decimal`, and any difference between that record and the
   compiled surface fails the build.

## Out of scope
Performance. Nothing here changes how the sum is computed for non-empty input.
