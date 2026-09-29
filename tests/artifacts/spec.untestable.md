---
id: 20260927_0001
intent: .sdlc/cycles/20260927_0001/intent.md
---

# Spec - Totals.Sum accepts an empty sequence

## Acceptance criteria

1. `Totals.Sum` handles empty input correctly.
2. `Totals.Sum` returns the arithmetic total for a non-empty sequence, asserted over a single
   element, over several, and over a sequence mixing negatives with positives — a total correct
   at one length is not evidence for another.
3. The public signature is unchanged: the recorded public surface of `Greenfield.Core` still lists
   `Totals.Sum(IEnumerable<decimal>) -> decimal`, and any difference between that record and the
   compiled surface fails the build.
