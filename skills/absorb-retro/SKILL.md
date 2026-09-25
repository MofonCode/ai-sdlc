---
name: absorb-retro
description: Copy landed retros into the ai-sdlc corpus and propose charter amendments from what they show. Use when cycles have landed since the last absorption, or when the user asks what the retros are saying.
argument-hint: [solution path or retro path, optional]
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(powershell.exe *)
---

# Absorb retros into ai-sdlc

This is the cross-project retro loop — the reason this framework exists rather than being a
set of conventions. Retros from every solution flow back here and become evidence about the
process itself.

## Copy first, and never overwrite

A retro is **immutable and append-only**. Copy each one in, keyed by cycle id. If a retro for
that id already exists, **compare rather than replace**:

- Identical → nothing to do. The copy is idempotent, which is what makes re-running safe.
- **Different → stop and report it.** Do not overwrite and do not merge. A retro that changed
  after landing means either the source was edited or two cycles share an id, and both are
  worse problems than a missed absorption.

That refusal is load-bearing. INV-03 forbids a second list without a check binding it, and
exempts a copy of an immutable artifact **only** while the copying mechanism refuses to
overwrite with differing content. This skill is that mechanism. Without the refusal the
corpus is just a second list and the exemption does not hold — which is exactly why the
charter currently says the retro corpus does not yet qualify.

## Then compute the signals — and do not store them

Every amendment signal is **derived from the corpus by a read-only query**. Never write one
to a committed file: that would be a second list beside the corpus, and the charter would
grow with every cycle.

One command computes them all:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcSignals.ps1
```

It is read-only and it stores nothing, which is the point. Read the charter's amendment table
alongside it, because the table is what says how to read each signal:

| Signal | Reading |
|---|---|
| zero cites, and no violation can be constructed | decoration — delete it |
| zero cites, violations possible but undetectable | unenforceable — keep, make it checkable |
| zero cites, and a verified `Prevented-by:` target still rejects violations | working as designed — keep |
| repeated **rejected** charter intents against it | unclear, not wrong — clarify the wording |
| high override count, cited then `accepted-wont-fix` | too broad, or wrong — candidate for narrowing |
| recurring suggestion clusters on one theme | a missing invariant — candidate for addition |

**`rejected` and `accepted-wont-fix` must never be merged.** They feed opposite signals:
`rejected` means the finding was wrong and says nothing about the invariant, while
`accepted-wont-fix` means the finding was right and we knowingly did not act. Merging them
inflates the override count and makes a sound invariant look too broad.

For a `Prevented-by:` claim, verify the named target before reporting "working as designed":
the path or check id must still exist **and** still reject a violating artifact. Where you
cannot verify it, report zero cites with no demonstrated prevention — a question again, not
an exemption.

Run the zero-cite query whenever you are here for any other reason. It is the one signal
that produces no event of its own: nothing will ever prompt you to notice an invariant that
no finding points at.

## Propose, one at a time. Never apply.

**A signal is a prompt to investigate, not a verdict.** No process may act on one, because
acting means changing the charter, and that takes an approved charter intent (INV-08).

So for each signal worth acting on, draft a `charter-review` intent and put it to the user
**individually**. Do not bundle several amendments into one intent: a person must be able to
approve one and reject another, and a bundle forces a single answer to several questions.

For each, say what the evidence is, how many cycles it spans, and what you would change.
Then wait. You can request; only a person can grant.

A charter intent proposing a change to **INV-08 itself** must say so in its title. The
self-reference cannot be closed — that rule lives in the document it protects — but quiet
removal is the only failure mode that matters, so it is made loud instead.
