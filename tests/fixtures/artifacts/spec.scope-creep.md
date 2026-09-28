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
3. The public signature is unchanged: a test project compiled against the previous signature still
   builds without modification.
4. `Totals.Sum` throws `ArgumentNullException` with the parameter name when given null, asserted by
   a test.
5. A new `Totals.Average` returns the mean of a sequence, or zero when it is empty.

## Out of scope
Performance.
