---
id: 20260927_0001
intent: .sdlc/intents/20260927_0001.md
---

# Spec - Totals.Sum accepts an empty sequence

## Acceptance criteria

1. `Totals.Sum` returns `0m` when given an empty `IEnumerable<decimal>`, asserted by a test that
   passes `Array.Empty<decimal>()`.
2. `Totals.Sum` returns the arithmetic total for a non-empty sequence, asserted by a test over a
   known sequence with a known total.

## Out of scope
Performance. Nothing here changes how the sum is computed for non-empty input.
