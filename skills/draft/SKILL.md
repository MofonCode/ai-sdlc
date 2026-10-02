---
name: draft
description: Print what belongs in one of the four files only a person writes -- an approval, an abandonment, a solution constraint, or a profile migration. Use when a person needs to approve, refuse, stop a cycle, add a rule, or change profile.
argument-hint: "approval <id> | abandon <id> | constraint | profile <name>"
allowed-tools: Bash(sdlc *), Bash(git *)
---

# Draft a file only a person writes

Four files, one rule: **a tool never writes them, a tool prints what to put in them.**

```
sdlc draft approval <id>     [--changes-requested | --deny | --reopen]
sdlc draft abandon <id>
sdlc draft constraint
sdlc draft profile <name>
```

Show what it prints **verbatim**, including the instructions under the block. Then stop.

## You cannot place it, and should not offer to

Every agent write to `approval.md`, `abandoned.md`, `constraints.md` and `solution.md` is denied at the
tool call — by file path and by shell command alike. There is no flag, no mode, and no approve command
for an exception to be carved out of, which is why the rule can be stated in one sentence and has
nowhere for a bypass to live.

So do not offer to paste it, do not offer a shell command that would write it, and do not read the
refusal as a configuration problem. If the person asks you to place it, the answer is that you cannot,
and that this is the mechanism rather than an obstacle: a pasted approval means something precisely
because no tool could have produced it.

## What each one is for

**`approval`** — the gate decision, covering `intent.md`, `spec.md` and `plan.md` together, taken once
immediately before the build. One decision, one entry, three hashes. It refuses to print while a
blocking finding is open, while a document is missing, while one has never been interrogated, and while
a round judged a document that has changed since.

A refusal is drafted on the same terms as an approval: `--changes-requested` to send it back, `--deny` to
refuse it, `--reopen` to withdraw a standing approval so the documents can be rewritten. Rejecting is
what a person does when they are least inclined to look a format up, and giving help to the easy case
while withholding it from the hard one is the asymmetry people resent.

Those three print the `reason` key with nothing after it, on purpose — the gate rejects an entry that
refuses a cycle and says nothing about why, so an unfilled one fails rather than standing quietly. Say
that when you show it.

**`abandon`** — why a cycle stopped. The reason is left empty and the file is rejected without one.
Abandonment is terminal: the cycle will never build, even if it also holds an approval, and there is no
command to revive it. The reason is history rather than safety — an agent stuck on a blocking finding has
an obvious escape in abandoning and starting clean, and a hand-written reason is what keeps the objection
that killed the cycle on the record.

**`constraint`** — the next free `LOC_` id with its four parts empty, and the three admission tests
beside them. It takes no statement, because the wording is the thing being decided and a tool accepting
one on the command line would be proposing the rule.

**`profile`** — both `solution.md` keys, not just the one that changes. The origin is a historical fact
that decides whether a profile's defaults bind, and printing one key while leaving a person to preserve
the other by hand is how it gets lost.

## Two things to say when you show an approval

**A pasted hash cannot be checked by eye.** `sdlc status <id>` shows the first twelve characters of each
recorded hash beside each document's current twelve, and a gate refusal names *which* document changed.
That is the remedy for the one real cost of the paste flow.

**A first entry is a file creation; a revisit is an edit.** The printed instructions say which, and it
matters: the ledger is append-only and the last entry stands, so pasting a second entry *over* the first
would throw away the record it exists to keep.

## What a blocker refusal means

If `draft approval` refuses because a blocking finding is open, do not look for a way round it. A
blocking finding cannot be waived, accepted or overridden by any mechanism — there is no field for it in
the entry and no refusal code at the gate that acceptance would clear. The finding is answered, or the
cycle does not advance.

If it genuinely cannot be answered, the artifact is wrong rather than incomplete, and the move is
`sdlc draft abandon <id>` and a new cycle.
