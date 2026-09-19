# Talking to the owner in plain language

Artifacts number things so that tooling and a fresh session can cite them exactly. **The
conversation does not inherit that numbering.** An identifier is an address, not a sentence.

Every skill in this chain presents its work to a person before a gate opens. That person is
deciding whether to approve something, and a paragraph built out of index references gives them
nothing to decide with — every claim can be correct and the paragraph still unreadable.

## The rule

**Never hand the owner a bare identifier.** Requirement numbers, charter invariants, flagged
concerns, success criteria, eval ids — none of them mean anything on their own.

Say what the thing *is* and why it matters. If the label genuinely earns its place, put a
one-line summary beside it, never the label alone.

## What this looks like

Unreadable, and a real example of the failure:

```
R47 and R48 close the gap F2 names, and S10 becomes machine-checkable rather than resting on
the prose R13 already admitted nobody could verify.
```

The same content, readable:

```
Two new requirements move every file the stack pack writes into a single program, so the
scaffold stops writing half its files where the safety check cannot see them. That also turns
"nothing gets generated before the owner approves the charter" from a promise in prose into
something a test can actually prove.
```

The second version is longer. That is the trade being made deliberately: the owner's
understanding is worth more than the author's word count.

## When a label earns its place

Only when the owner is about to open the file and look at that exact line. Then write it as
*label plus summary*:

- Good: *"the third requirement — a scaffold action may never write outside the target
  directory (R4)"*
- Bad: *"R4"*

## Summarise the shape, not the list

When presenting an artifact for approval, give the **count and the shape**, not an enumeration.
*"Sixty-eight requirements; the new ones are all about one thing — making the scaffold refuse to
run rather than half-finish"* tells the owner what to look for. A list of numbers tells them to
go read the file, which is the job they asked you to do for them.

## Assume no prior exposure

Write every presentation for someone who has never used this process. The first time a term
appears in a session, say what it means in half a sentence:

- **the chain** — the run of committed files that carries a change from idea to shipped
- **a gate** — a point where work stops until the owner says the word
- **a slice** — one batch of files that a single implementer owns, so two of them never collide
- **a manifest** — the list of files a slice owns, checked before any fan-out
- **the charter** — the repo-wide rules every change is measured against

## This binds the conversation, not the file

Keep the numbering *in* the artifacts. Downstream stages, tests and a fresh session with no
memory all depend on it. The file holds the numbering; the conversation holds the meaning.
Collapsing the two defeats the reason both exist.
