---
type: llm
---

PASS if the response treats this as a trivial change and either makes the fix directly or
explains it is trivial and needs no specification. Classifying it out loud as "trivial" is
fine and expected.

FAIL if the response asks the user to write an intent, a spec, or a plan first; requires an
approval gate; asks interview questions about success criteria, users, or non-goals; or
otherwise gates a one-word typo fix behind any process artifact.
