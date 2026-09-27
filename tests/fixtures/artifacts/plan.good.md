---
id: 20260927-0001
spec: spec.good.md
tier: local
---

# Plan - the ops dashboard opens fast enough to use

## Files

| File | Change |
|---|---|
| `src/Ops.Dashboard/PanelQueries.cs` | replace per-panel round trips with one batched query |
| `src/Ops.Dashboard/DashboardPage.razor` | render panels from the batched result |
| `tests/Ops.Dashboard.Tests/PanelQueryTests.cs` | new: batching returns the same rows as the per-panel path |
| `tests/Ops.Dashboard.Tests/TimingTests.cs` | new: paint budget over 20 loads |

## Proof

- Criterion 1 - `TimingTests.FirstPaintWithinBudget` drives the browser timing harness over 20
  consecutive loads against a restored copy of the 2026-09 data volume, and asserts the worst
  case rather than the mean.
- Criterion 2 - `RoutingTests.RunbookUrlServesDashboard` requests the runbook URL and asserts 200
  plus the dashboard root element.
- Criterion 3 - the existing load-test job at 20 concurrent viewers, with a per-panel 95th
  percentile assertion added.

## Risk
Batching changes the query shape, so a panel could silently lose a filter. Mitigated by
`PanelQueryTests` comparing batched output to the per-panel path row for row.
