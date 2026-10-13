---
name: draft
description: Print what belongs in one of the files only a person writes -- an approval, an abandonment, the declared stack, or the solution document. Use when a person needs to approve, refuse, stop a cycle, or declare or change what the solution is built from and which stack rules it is held to. For a solution's own constraints, the skill is propose.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "approval <id> | abandon <id> | solution | techstack [--empty]"
allowed-tools: Bash(sdlc *), Bash(git *)
---

# Draft a file only a person writes

Six protected files, one rule: **a tool never places them, a person does.** This drafter serves four
of them. The other two — this solution's own rules and its decision record — are served by `propose`
rather than drafted, for the reason at the foot of this page.

```
sdlc draft approval <id>     [--changes-requested | --deny | --reopen]
sdlc draft abandon <id>
sdlc draft solution
sdlc draft techstack         [--empty]
```

Show what it prints **verbatim**, including the instructions under the block. **Add nothing of your
own** — the printed instructions are the whole hand-back, and they are written to be followed by the
person who has to place the file. Then stop.

**An approval is written to a file beside the ledger and placed by renaming it.** `draft approval`
writes `approval.<decision>.draft.md` into the cycle directory -- a complete file, not a fragment --
and prints it with the rename that records it. **`draft solution` does the same for the solution
document**, carrying its three keys through as recorded and any prose already there through
unchanged -- so the rename can neither reclassify the solution nor cost somebody what they wrote. **And
`draft techstack` writes the declared stack beside itself** — a copy of it, or the framework's default
where none is placed yet — and writes the options menu as a report, so declaring or changing it is a
rename too: of the draft, never of the menu. `abandon` still prints for a person to place by hand. Show the rename command and
do not run it: it is denied, and it is the decision.

## You cannot place it, and should not offer to

Every agent write to `approval.md`, `abandoned.md`, `constraints.md`, `decisions.md`, `solution.md` and
`techstack.md` is denied at the tool call — by file path and by shell command alike. There is no flag, no mode, and no approve command
for an exception to be carved out of, which is why the rule can be stated in one sentence and has
nowhere for a bypass to live.

The rename that places an approval is denied too, in every spelling -- `mv`, `Move-Item`,
`Rename-Item`, `Copy-Item`, and through any shell the host offers. So do not offer to run it, do not
offer a shell command that would write any of them, and do not read the refusal as a configuration
problem. If the person asks you to place it, the answer is that you cannot, and that this is the
mechanism rather than an obstacle: a decision means something precisely because no tool could have
recorded it.

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

**`techstack`** — the declared stack, beside itself, for an edit a person renames into place. You may
edit the draft; only the real file is denied. What the draft starts as depends on what is there:

| | The draft is |
|---|---|
| a declared stack is in place | a **byte copy** of it. The rename applies what somebody changed and nothing else, with nothing merged and nothing regenerated |
| none is in place yet | the **framework's default**: every row it would pin, and every stack rule it ships switched on. Adoption writes no declared stack, so this is where the first one comes from, and no cycle opens until it is renamed into place |
| `--empty` was given | **nothing.** Renamed into place it declares that nothing is pinned, no stack rule applies, and every plan chooses freely |

**The file holds two tables, and what is in them is exactly what binds.** The first pins choices: a row
is a default every plan is read against, and no row leaves that choice to each plan. The second, under
`## Rules`, lists the stack rules — the ones that may name a framework — with `true` or `false` beside
each and what the rule holds a plan to. Only the ones marked `true` apply; a rule with no row does not.
So declining a rule is setting its row to `false`, and nothing about the stack is selected from anywhere
else.

**It writes a second file, and that one is never placed.** The menu is a report under `.sdlc/reports/`:
every concern the shipped profiles know about with what each declares for it, the alternatives this
framework has argued, the choices it refused, every stack rule it ships, and an empty table for a
solution starting over rather than amending a row. The command prints its path. Point a person at it and leave it where it is — a
declared stack carrying answers the solution has **not** chosen is one a plan can reach into and
truthfully not be departing from anything, which is the deadlock protecting the file closed.
`sdlc doctor` reports a declared stack that ended up with the menu inside it.

**A report rather than a file in `.sdlc/` itself, because it outlives the rename.** Nothing deletes it,
and an untracked copy would reach every later cycle's diff as a file no plan declared — `CON_07.2`. The
reports directory ignores itself, which is the whole reason it exists.

It is protected for the reason the solution's own constraints are. The declared stack is the second rule
source a plan is read against, and `CON_15` judges every plan against it — so an agent able to edit it
could satisfy that rule by adding the row it wanted rather than arguing for it, or drop a stack rule by
switching its row off. **What belongs in a change
here is a default the solution is adopting or dropping, not a package one cycle happened to need.** A plan
that reaches for something undeclared departs and says what the declared default would have cost, which is
an argument rather than an edit to this file.

**A plan may not declare it either.** The schema refuses a plan whose `Files` section names any of
them, because such a plan cannot be carried out as written — the build is denied the write, and the
evidence comparison would report it as undelivered, which reads as a missing edit rather than an
impossible declaration.

**Two of the six are not drafted, and that is the asymmetry here.** Every drafter above hands back a
finished file for a decision somebody already took. A rule is the opposite: its wording *is* the
decision, so there was nothing to hand back and this printed a blank form — the one member of the
family that did. A recorded decision has the same shape. Both moved to `propose`, which argues from the
record instead: `sdlc propose constraints` from the findings, `sdlc propose decisions` from what an
approved cycle stated. The licence that makes the difference: **a tool may propose when it brings the
evidence, and may not shape what somebody just typed.** If somebody asks you to draft either, send
them there.

**There is no `draft profile` any more.** A solution records no profile: which technologies are pinned
and which stack rules apply are both rows in the declared stack, so the act that used to be a profile
migration is an edit to that draft. The binary answers the old spelling with a sentence saying so.

## Two things to say when you show an approval

**A recorded hash cannot be checked by eye.** `sdlc status <id>` shows the first twelve characters of
each recorded hash beside each document's current twelve, and a gate refusal names *which* document
changed. That is the remedy for the one real cost of a decision nobody can read back.

**A revisit replaces the ledger and keeps it.** The ledger is append-only and the last entry stands, so
the draft reproduces every decision already recorded above the new one, fence to fence. Say how many it
carries -- the printed instructions do -- because the rename overwrites, and somebody is entitled to
check that what they are about to replace is in the thing replacing it.

## What a blocker refusal means

If `draft approval` refuses because a blocking finding is open, do not look for a way round it. A
blocking finding cannot be waived, accepted or overridden by any mechanism — there is no field for it in
the entry and no refusal code at the gate that acceptance would clear. The finding is answered, or the
cycle does not advance.

If it genuinely cannot be answered, the artifact is wrong rather than incomplete, and the move is
`sdlc draft abandon <id>` and a new cycle.
