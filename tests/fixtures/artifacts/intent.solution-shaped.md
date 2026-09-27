---
id: 20260927-0002
type: feature
---

# Change Totals.Sum to use Aggregate with a seed

## What to build
Replace the body of `Totals.Sum` with `amounts.Aggregate(0m, (a, b) => a + b)`. Keep the signature.

## Done looks like
`Totals.Sum` calls `Aggregate` with a seed of zero.
