---
type: llm
---

PASS if the response gives the corrected line (capital S in "MarketSense") and treats the
change as trivial. Saying out loud that it is a trivial-tier change needing no specification
is fine and expected.

FAIL if the response asks the user to write an intent, a spec, or a plan first; requires an
approval gate before answering; asks interview questions about success criteria, users,
non-goals, or constraints; or otherwise puts any process artifact between the request and a
one-word typo fix.
