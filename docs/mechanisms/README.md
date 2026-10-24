# The mechanisms

Two pages, each explaining one mechanism of the framework: what it is, how it decides, and why it works
that way. This file is the index: what each one covers, when you would read it, and what it leaves to
another.

**A mechanism page asks nothing of you.** A guide walks you through a task; a mechanism page is what you read when a
task has met something you want to understand — a refusal you did not expect, a finding with a name you
do not know. Where a mechanism touches a task, it points at the guide that does it rather than carrying a
second copy of the steps. **For the one-line definition of a term**, `../README.md` defines every concept
the framework names, and its row links here when there is a page.

| Mechanism | Covers | Read it |
|---|---|---|
| [`protected-files.md`](protected-files.md) | the six files only a person changes and the adoption command: what is guarded, how the pre-tool hook decides, and what it deliberately does not guard | when an agent says it cannot write a file, or before you look for a way to let it |
| [`perspectives.md`](perspectives.md) | what examines a change beyond the rules: what a perspective carries, which a cycle gets and why, when each steers and judges, and how to read what it found | when a finding carries a perspective's name, or before an interview asks what a change touches |

---

## `protected-files.md`

**The guard, explained once.** Which six files only a person changes, and the adoption command beside
them; the pre-tool hook on each host and the two rules it applies — one to file writes, one to the text
of a shell command; and what it deliberately does not guard, including you.

What it does not cover: making a change to one of the six, which is What your solution owns in
`../guides/adopting-the-framework.md`; why each file in particular is a person's, which is the denial
table in `../guides/handling-refusals.md`; and every refusal the hook gives, which is the same.

## `perspectives.md`

**What examines a change beyond the rules.** The four parts every perspective carries, the perspectives
by name and what switches each on, the three ways a cycle gets one, what each does at every stage — steer,
judge, ask of the evidence — how its findings appear and when they block, how a disagreement between two
is settled, and how a perspective's suggestions earn it rules.

What it does not cover: any perspective's method, which `sdlc perspective show` prints from the document
the binary carries; or answering a finding, which is `../guides/handling-blockers.md`.

## What these are not

**None of them states a rule or a decision.** The rules are rendered by `sdlc constraints`; the design
and every decision behind it is [`../../CONCEPT.md`](../../CONCEPT.md), which a mechanism page cites rather than
restates. A mechanism page is that design seen from a solution, for the person using it.

**None of them reports where the project stands.** That is the Where it stands section of
[`../../README.md`](../../README.md). A mechanism page says how a thing works, as true on the day it is read as
on the day it was written.

## See also

- [The guides](../guides/README.md) — one task each
- [The components](../README.md) — every concept the framework names, defined once
- [The reference](../reference/README.md) — every command, skill and refusal, one line each
