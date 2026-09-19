# Talking to the owner in plain language

Artifacts number things — requirements, invariants, flagged concerns, success criteria — and
that numbering is **a feature, not a wart**. It is what lets a fresh session with no memory
pick the chain up, what downstream stages cite, and what a test names when it proves a
requirement is met. Nothing here asks for any of it to go away.

What fails is using those numbers *as the conversation*. A label is an address. An address is
not a sentence.

## The rule

**Cite the index and explain it. Never the index alone.**

Keep the label — the owner may want to look it up, and it is the shared handle between the
chat and the file. Put a short plain-language gloss beside it, every time, so the sentence
still means something to someone who has not memorised the artifact.

```
R4 — no scaffold action may write outside the target directory
I5 — a pack's own skill is the only list of what that pack does
F2 — the scaffold writes files where the safety check cannot see them
```

That is the whole shape: **label, dash, what it says.**

## What this looks like in a paragraph

Unreadable, and a real example of the failure:

```
R47 and R48 close the gap F2 names, and S10 becomes machine-checkable rather than resting on
the prose R13 already admitted nobody could verify.
```

The same content, with the labels kept and the meaning put back:

```
Two new requirements — R47, every file the pack writes is declared in one place, and R48,
the setup command writes nothing but the charter — close the hole flagged as F2, where the
scaffold was writing files the safety check never saw. That also upgrades success criterion
S10, "nothing is generated before the owner approves", from a promise in prose to something
a test can actually prove.
```

Longer, and the trade is deliberate: the owner's understanding is worth more than the
author's word count. The labels survived; they simply stopped carrying the sentence alone.

## Summarise the shape as well as the count

When presenting an artifact for approval, give the count *and* what the items are about —
*"sixty-eight requirements; the new ones are all about one thing, making the scaffold refuse
to run rather than half-finish"*. The owner reads the file for the full list. A bare count
tells them nothing; a bare list of numbers tells them to go do the reading themselves.

## Assume no prior exposure

Write every presentation for someone who has never used this process. The first time a term
appears in a session, say what it means in half a sentence:

- **the chain** — the run of committed files that carries a change from idea to shipped
- **a gate** — a point where work stops until the owner says the word
- **a slice** — one batch of files that a single implementer owns, so two of them never collide
- **a manifest** — the list of files a slice owns, checked before any fan-out
- **the charter** — the repo-wide rules every change is measured against

## This binds the conversation, not the file

Artifacts keep their numbering exactly as it is. Do not strip labels out of a spec, a plan or
a verification record to make them read more casually — downstream stages, tests and a fresh
session all depend on them being there.

The file holds the numbering. The conversation holds the numbering **and** the meaning.
