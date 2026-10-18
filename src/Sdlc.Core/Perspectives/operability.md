---
name: operability
always: true
areas:
files:
---

# Operability

Whether the change can be run and diagnosed once it is in production: when it fails, whether somebody who
did not write it can tell what failed, for whom, and why, from what it emits. Always on, because Operate can
only read what the code writes down — a change that fails silently is one no lead will ever come from.

## Method

1. **List every new way the change can fail.** A dependency that times out or refuses, input it rejects,
   state it finds inconsistent, a limit it reaches, a background job that stops.
2. **For each, play the person on call at 3 a.m.** With only the logs, the traces and the metrics — no
   debugger, no source — could they tell that it happened, what it happened to, and what to do first?
3. **Follow one request through the change.** Does a trace carry it across every new boundary, so a slow or
   failed call can be found from the one before it?
4. **Check what it says about itself.** A new dependency the service needs before it can serve has a health
   check; a new capacity limit is visible before it is reached.

## Cover this

### intent

- who needs to know when this goes wrong, and how quickly

### spec

- a criterion for what is recorded when each new failure happens: enough to tell what, for whom, and why
- a criterion for what the user is told, separate from what is logged

### plan

- each new failure mode, and the log, trace or metric that records it
- a health check for any new dependency the service cannot work without
- where OpenTelemetry spans begin and end on the new paths

### build

- log a failure once, where it is handled, with its context and without personal data
- carry the trace across every new outbound call
- no swallowed exception: one caught is logged or rethrown
- a test that causes a failure reads what it recorded; one handed a logger that discards everything
  proves nothing about the record

### verify

- the failure criteria are met by tests that cause the failure and assert what was recorded

## Questions

### intent

- If this fails in production, who finds out, and how?

### spec

- For every new failure the intent implies, is there a criterion saying what is recorded and what the user is
  told?
- Could a criterion be met while the failure leaves no trace at all?

### plan

- Does the plan name, for each new failure mode, what records it?
- Does any new dependency lack a health check?
- Will a trace follow a request across every boundary the plan adds?

### build

- Is any exception caught and dropped, or logged without what would identify the failing operation?
- Does a log line carry personal data or a secret?
- Is a new outbound call made outside the trace?
- Does each test that causes a failure read the log entry, metric or span it left, or would it pass with
  the logging deleted?

### verify

- Does the evidence show each failure being caused and recorded, rather than only the success path running?

## Rules

- `CON_20` — a spec says what is recorded when the action fails. It is this perspective's spec question,
  *could a criterion be met while the failure leaves no trace at all?*, made a rule after two trial
  solutions raised it at every stage as a suggestion and both built the gap anyway.

Anything else this method finds that no rule forbids is a suggestion; one that keeps recurring across
cycles is what `sdlc propose constraints` turns into a rule.

## Evidence

- tests that cause each named failure and assert the log entry, metric or span it produces
- the health check's own test, where the plan adds one
