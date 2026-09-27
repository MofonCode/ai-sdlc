---
id: 20260927-0001
intent: intent.good.md
---

# Spec - the ops dashboard opens fast enough to use

## Acceptance criteria

1. At the production data volume of 2026-09, first meaningful paint occurs within 2 seconds,
   measured by the existing browser timing harness over 20 consecutive loads, worst case.
2. The dashboard is served from the same URL as before: a request to the runbook URL returns 200
   and renders the dashboard.
3. A load test at 20 concurrent viewers shows no panel exceeding 2 seconds at the 95th
   percentile.

## Out of scope
Panel content and layout are unchanged. No panel is added or removed.
