---
id: 20260927-0001
intent: intent.good.md
---

# Spec - Totals.Sum accepts an empty sequence

## Acceptance criteria

1. `Totals.Sum` handles empty input correctly.
2. `Totals.Sum` returns the arithmetic total for a non-empty sequence, asserted by a test over a
   known sequence with a known total.
3. The public signature is unchanged: a test project compiled against the previous signature still
   builds without modification.
