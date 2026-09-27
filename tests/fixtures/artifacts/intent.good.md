---
id: 20260927-0001
type: bug
---

# Totalling an empty set of amounts throws instead of returning zero

## Problem
`Totals.Sum` throws when given an empty sequence, and two callers now guard the call site with a
count check before using it. The guard has been copied twice and will be copied again.

## Who
Anyone calling `Totals.Sum`. Both existing callers already work around it.

## Done looks like
A caller can hand `Totals.Sum` whatever collection it has, empty or not, and delete its guard.

## Must not change
The signature. Both callers pass `IEnumerable<decimal>` and are compiled against it.

## Open
- Should an empty sum be zero, or is a null result meaningful to some caller? Nobody has asked the
  two callers.
