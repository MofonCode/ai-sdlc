---
id: 20260927_0003
type: bug
---

# Totalling large sets is too slow

## Problem
`Totals.Sum` takes 40 milliseconds for a 10,000 element sequence, because LINQ's `Sum` boxes each
decimal on the way through the enumerator.

## Who
The nightly rollup, which totals about 200 sequences of that size.

## Done looks like
The rollup finishes in the window it has.
