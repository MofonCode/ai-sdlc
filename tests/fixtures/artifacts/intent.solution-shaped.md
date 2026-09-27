---
id: 20260927-0002
type: feature
---

# Add a Redis cache to the dashboard

## What to build
Put Redis in front of the dashboard queries. Cache each panel for 60 seconds and invalidate on
write. Use the existing connection multiplexer.

## Done looks like
Redis is deployed and the dashboard reads through it.
