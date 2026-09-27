---
id: 20260927-0003
type: bug
---

# Export times out for large accounts

## Problem
The CSV export takes 34 seconds for accounts over 50,000 rows, which is past the 30 second
gateway timeout. The cause is the N+1 query in the line-item loader.

## Who
Enterprise accounts. Four of them are large enough to hit it.

## Done looks like
The export completes for a 50,000 row account without hitting the gateway timeout.
