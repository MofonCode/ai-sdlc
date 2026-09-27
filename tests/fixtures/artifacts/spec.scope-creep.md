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
3. Each panel exposes a CSV export button that downloads the panel's current data set.
4. Dashboard layout is user-configurable and the arrangement persists per user.
