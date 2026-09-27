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
| `tests/Ops.Dashboard.Tests/PanelQueryTests.cs` | new: unit test for the batched query |

## Proof

- Criterion 1 - a unit test on `PanelQueries` asserts the batched query is issued once.
- Criterion 2 - manual check that the URL still works.
- Criterion 3 - covered by criterion 1.

## Risk
Low.
