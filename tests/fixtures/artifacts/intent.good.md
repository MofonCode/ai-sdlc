---
id: 20260927-0001
type: feature
---

# The ops dashboard is too slow to use during an incident

## Problem
The dashboard takes long enough to load that the on-call engineer gives up and reads raw logs
instead. It has been abandoned in the last four incidents.

## Who
The on-call rotation, during every incident. Roughly twice a week.

## Done looks like
Someone opens it mid-incident and stays in it rather than switching to logs.

## Must not change
The dashboard URL. It is printed in the runbook.

## Open
- Is the delay in the query or the render? Nobody has measured.
- Which panel is slowest? Unknown until the above is answered.
